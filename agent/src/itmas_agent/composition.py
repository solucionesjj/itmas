"""Composition root — the one place that wires collectors, the platform
backend, persistence stores, and the API client together, and defines what
"run the agent" actually does. Adding a new collector or swapping an
implementation (e.g. a real logger for a fake in tests) only ever touches
this file — nothing else needs to know how the pieces are assembled.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Optional, Union

from itmas_agent import scheduling
from itmas_agent.collectors.applications import ApplicationsCollector
from itmas_agent.collectors.hardware import HardwareCollector
from itmas_agent.collectors.network import NetworkCollector
from itmas_agent.collectors.os_info import OSInfoCollector
from itmas_agent.collectors.resources import ResourceCollector
from itmas_agent.collectors.storage import StorageCollector
from itmas_agent.collectors.users import UsersCollector
from itmas_agent.config import AgentConfiguration
from itmas_agent.models import DeviceIdentity, InventorySnapshot, Measured, measured_from_optional
from itmas_agent.networking.api_client import ApiClient
from itmas_agent.networking.errors import ApiError
from itmas_agent.normalization.extended_schema import to_extended_json
from itmas_agent.normalization.inventory_mapper import MappingError, map_to_inventory_request
from itmas_agent.persistence.json_store import write_json_atomic
from itmas_agent.persistence.retry_queue_store import RetryQueueStore
from itmas_agent.persistence.run_state_store import RunStateStore
from itmas_agent.platforms.base import PlatformBackend

_AGENT_VERSION = "0.1.0"
_SCHEMA_VERSION = "1.0"

ApiClientFactory = Callable[[str, str, Union[bool, str]], ApiClient]


@dataclass
class RunReport:
    ran: bool
    reason: str
    sent_to_api: bool = False


def _device_id_from_api_key(api_key: Optional[str]) -> Measured[str]:
    if not api_key or "." not in api_key:
        return Measured.unavailable("node API key not configured or malformed")
    return Measured.ok(api_key.split(".", 1)[0])


def _default_api_client_factory(base_url: str, api_key: str, verify: Union[bool, str]) -> ApiClient:
    return ApiClient(base_url=base_url, api_key=api_key, verify=verify)


class AgentRunner:
    def __init__(
        self,
        backend: PlatformBackend,
        config: AgentConfiguration,
        run_state: RunStateStore,
        retry_queue: RetryQueueStore,
        extended_cache_path: Path,
        clock: scheduling.Clock,
        logger,
        api_client_factory: ApiClientFactory = _default_api_client_factory,
        storage_collector: Optional[StorageCollector] = None,
        resource_collector: Optional[ResourceCollector] = None,
        network_collector: Optional[NetworkCollector] = None,
    ) -> None:
        self._backend = backend
        self._config = config
        self._run_state = run_state
        self._retry_queue = retry_queue
        self._extended_cache_path = extended_cache_path
        self._clock = clock
        self._logger = logger
        self._api_client_factory = api_client_factory
        # Injectable so tests can avoid real psutil/network calls — these
        # three collectors take no PlatformBackend, so FakePlatformBackend
        # alone can't isolate a test from the real machine/network.
        self._storage_collector = storage_collector or StorageCollector()
        self._resource_collector = resource_collector or ResourceCollector()
        self._network_collector = network_collector or NetworkCollector(
            config.public_ip_lookup_url
        )

    def run_due_work(self, force: bool = False, dry_run: bool = False) -> RunReport:
        if not force:
            decision = scheduling.is_daily_run_due(
                self._run_state,
                self._clock,
                self._config.scheduled_hour,
                self._config.scheduled_minute,
            )
            if not decision.should_run:
                self._logger.info(f"skipping run: {decision.reason}")
                return RunReport(ran=False, reason=decision.reason)

        snapshot = self._collect_everything()
        write_json_atomic(self._extended_cache_path, to_extended_json(snapshot))

        if dry_run:
            self._logger.info(
                "dry-run: collection complete, nothing sent, cache written to "
                f"{self._extended_cache_path}"
            )
            return RunReport(ran=True, reason="dry-run")

        sent = self._send(snapshot)
        # Collection succeeded regardless of transmission outcome — a
        # network/backend problem shouldn't force re-collecting tomorrow's
        # worth of hardware/apps/users data again today; RetryQueueStore
        # (flushed on every subsequent run) is the mechanism for eventually
        # delivering data that failed to send.
        scheduling.record_daily_run_success(self._run_state, self._clock)
        return RunReport(ran=True, reason="collected", sent_to_api=sent)

    def _collect_everything(self) -> InventorySnapshot:
        backend = self._backend
        os_snapshot = OSInfoCollector(backend).collect()
        hardware_snapshot = HardwareCollector(backend).collect()
        storage_snapshot = self._storage_collector.collect()
        users_snapshot = UsersCollector(backend).collect()
        applications_snapshot = ApplicationsCollector(backend).collect()
        resources_snapshot = self._resource_collector.collect()
        network_snapshot = self._network_collector.collect()

        api_key = backend.credential_store().get_api_key()
        device = DeviceIdentity(
            device_id=_device_id_from_api_key(api_key),
            hardware_serial=measured_from_optional(
                backend.hardware_serial(), "not reported by platform backend"
            ),
            hostname=os_snapshot.hostname,
            category=self._config.category,
        )

        return InventorySnapshot(
            schema_version=_SCHEMA_VERSION,
            agent_platform=backend.platform_name,
            agent_version=_AGENT_VERSION,
            device=device,
            collected_at=self._clock.now(),
            os=os_snapshot,
            hardware=hardware_snapshot,
            storage=storage_snapshot,
            applications=applications_snapshot,
            users=users_snapshot,
            resources=resources_snapshot,
            network=network_snapshot,
        )

    def _send(self, snapshot: InventorySnapshot) -> bool:
        api_key = self._backend.credential_store().get_api_key()
        if not api_key:
            self._logger.error("no node API key configured — run 'itmas-agent configure' first")
            return False

        client = self._api_client_factory(
            self._config.api_base_url, api_key, self._config.tls_verify
        )

        self._flush_retry_queue(client)

        try:
            request = map_to_inventory_request(snapshot)
        except MappingError as exc:
            self._logger.error(f"cannot build inventory payload, nothing sent: {exc}")
            return False

        try:
            client.send_inventory(request)
        except ApiError as exc:
            if exc.retryable:
                self._logger.warning(f"inventory send failed, queued for retry: {exc}")
                self._retry_queue.enqueue("inventory", request.to_json_dict())
            else:
                self._logger.error(f"inventory send failed permanently (not retrying): {exc}")
            return False

        self._logger.info("inventory sent successfully")
        return True

    def _flush_retry_queue(self, client: ApiClient) -> None:
        pending = self._retry_queue.pending()
        if not pending:
            return
        remaining = []
        for item in pending:
            try:
                if item["kind"] == "inventory":
                    client.send_inventory_json(item["payload"])
                    self._logger.info("flushed a previously queued inventory payload")
                else:
                    remaining.append(item)
            except ApiError as exc:
                if exc.retryable:
                    remaining.append(item)
                else:
                    self._logger.error(f"dropping permanently-failed queued payload: {exc}")
        if len(remaining) != len(pending):
            self._retry_queue.requeue(remaining)
