from __future__ import annotations

from datetime import datetime, timezone

from itmas_agent.composition import AgentRunner
from itmas_agent.config import AgentConfiguration
from itmas_agent.models import (
    Measured,
    NetworkSnapshot,
    ResourceSnapshot,
    StorageSnapshot,
)
from itmas_agent.networking.errors import ApiError
from itmas_agent.persistence.retry_queue_store import RetryQueueStore
from itmas_agent.persistence.run_state_store import RunStateStore
from itmas_agent.platforms.base import HardwareRawInfo, OSRawInfo
from tests.fakes.fake_platform_backend import FakePlatformBackend


class _FakeClock:
    def __init__(self, fixed_now: datetime) -> None:
        self._fixed_now = fixed_now

    def now(self) -> datetime:
        return self._fixed_now


class _FakeApiClient:
    def __init__(self, fail_with: ApiError = None) -> None:
        self.sent_inventory_calls: list[dict] = []
        self._fail_with = fail_with

    def send_inventory(self, request) -> None:
        self.send_inventory_json(request.to_json_dict())

    def send_inventory_json(self, json_body: dict) -> None:
        if self._fail_with:
            raise self._fail_with
        self.sent_inventory_calls.append(json_body)


class _NoOpLogger:
    def info(self, *a, **k):
        pass

    def warning(self, *a, **k):
        pass

    def error(self, *a, **k):
        pass


# Fast, offline fakes for the three collectors that DON'T take a
# PlatformBackend (so FakePlatformBackend alone can't isolate a test from
# real psutil timing or a real network call to the public-IP service).
class _FakeStorageCollector:
    def collect(self) -> StorageSnapshot:
        return StorageSnapshot(volumes=[])


class _FakeResourceCollector:
    def collect(self) -> ResourceSnapshot:
        return ResourceSnapshot(
            cpu_usage_percent=Measured.ok(10.0),
            memory_usage_percent=Measured.ok(50.0),
            memory_total_bytes=Measured.ok(1),
            memory_used_bytes=Measured.ok(1),
            uptime_seconds=Measured.ok(1.0),
        )


class _FakeNetworkCollector:
    def collect(self) -> NetworkSnapshot:
        return NetworkSnapshot(local_ip=Measured.ok("10.0.0.1"), public_ip=Measured.ok("1.2.3.4"))


_VALID_HARDWARE = HardwareRawInfo(
    manufacturer="Apple",
    model="MacBook Pro",
    model_identifier="Mac16,7",
    processor_model="Apple M4 Pro",
    gpu_model="Apple M4 Pro GPU",
    gpu_cores=16,
    gpu_memory_bytes=None,
    gpu_memory_unavailable_reason="unified memory",
    storage_total_bytes=549_755_813_888,
    storage_type="SSD",
    disk_count=1,
)
_VALID_OS = OSRawInfo(
    name="macOS",
    version="15.1.0",
    build="24B83",
    kernel_version="Darwin 24.1.0",
    architecture="arm64",
    hostname="test-host.local",
)


def _make_runner(
    tmp_path,
    backend,
    clock,
    api_client_factory=None,
    config_overrides=None,
    retry_queue=None,
):
    config_kwargs = dict(api_base_url="https://api.example.com", category="collaborator")
    config_kwargs.update(config_overrides or {})
    return AgentRunner(
        backend=backend,
        config=AgentConfiguration(**config_kwargs),
        run_state=RunStateStore(tmp_path / "run_state.json"),
        retry_queue=retry_queue or RetryQueueStore(tmp_path / "retry_queue.json"),
        extended_cache_path=tmp_path / "latest_inventory.json",
        clock=clock,
        logger=_NoOpLogger(),
        api_client_factory=api_client_factory or (lambda *a, **k: _FakeApiClient()),
        storage_collector=_FakeStorageCollector(),
        resource_collector=_FakeResourceCollector(),
        network_collector=_FakeNetworkCollector(),
    )


def test_not_due_skips_collection_entirely(tmp_path, mocker):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    collect_spy = mocker.spy(backend, "hardware_info")
    clock = _FakeClock(datetime(2026, 8, 8, 7, 0, 0, tzinfo=timezone.utc))
    runner = _make_runner(tmp_path, backend, clock)

    report = runner.run_due_work(force=False)

    assert report.ran is False
    collect_spy.assert_not_called()


def test_force_bypasses_schedule_check(tmp_path):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    backend.credential_store().set_api_key("deviceid.secret")
    clock = _FakeClock(datetime(2026, 8, 8, 7, 0, 0, tzinfo=timezone.utc))
    runner = _make_runner(tmp_path, backend, clock)

    report = runner.run_due_work(force=True)

    assert report.ran is True
    assert report.sent_to_api is True


def test_dry_run_collects_but_sends_nothing(tmp_path):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    backend.credential_store().set_api_key("deviceid.secret")
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))
    factory_calls = []
    runner = _make_runner(
        tmp_path,
        backend,
        clock,
        api_client_factory=lambda *a, **k: factory_calls.append(1) or _FakeApiClient(),
    )

    report = runner.run_due_work(dry_run=True)

    assert report.ran is True
    assert report.sent_to_api is False
    assert factory_calls == []  # the API client factory itself was never even called
    assert (tmp_path / "latest_inventory.json").exists()


def test_successful_send_records_daily_success(tmp_path):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    backend.credential_store().set_api_key("deviceid.secret")
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))
    run_state = RunStateStore(tmp_path / "run_state.json")
    runner = _make_runner(tmp_path, backend, clock)

    report = runner.run_due_work(force=True)

    assert report.sent_to_api is True
    assert run_state.last_success("daily_full_run") == clock.now()


def test_no_api_key_configured_skips_send_without_crashing(tmp_path):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    # deliberately no set_api_key() call
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))
    runner = _make_runner(tmp_path, backend, clock)

    report = runner.run_due_work(force=True)

    assert report.ran is True
    assert report.sent_to_api is False


def test_mapping_error_still_marks_the_day_as_run(tmp_path):
    """A field the CURRENT contract requires is unavailable — nothing is
    sent, but collection itself succeeded, so the daily gate should not
    force re-collecting everything again later today.
    """
    broken_hardware = HardwareRawInfo(
        manufacturer="Apple",
        model="MacBook Pro",
        model_identifier="Mac16,7",
        processor_model=None,  # required by the current contract as cpu.model
        gpu_model=None,
        gpu_cores=None,
        gpu_memory_bytes=None,
    )
    backend = FakePlatformBackend(hardware=broken_hardware, os=_VALID_OS)
    backend.credential_store().set_api_key("deviceid.secret")
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))
    run_state = RunStateStore(tmp_path / "run_state.json")
    runner = _make_runner(tmp_path, backend, clock)

    report = runner.run_due_work(force=True)

    assert report.sent_to_api is False
    assert run_state.last_success("daily_full_run") is not None


def test_retryable_api_error_enqueues_the_payload(tmp_path):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    backend.credential_store().set_api_key("deviceid.secret")
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))
    retry_queue = RetryQueueStore(tmp_path / "retry_queue.json")
    runner = _make_runner(
        tmp_path,
        backend,
        clock,
        api_client_factory=lambda *a, **k: _FakeApiClient(
            fail_with=ApiError("network down", retryable=True)
        ),
        retry_queue=retry_queue,
    )

    report = runner.run_due_work(force=True)

    assert report.sent_to_api is False
    assert len(retry_queue.pending()) == 1


def test_non_retryable_api_error_does_not_enqueue(tmp_path):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    backend.credential_store().set_api_key("deviceid.secret")
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))
    retry_queue = RetryQueueStore(tmp_path / "retry_queue.json")
    runner = _make_runner(
        tmp_path,
        backend,
        clock,
        api_client_factory=lambda *a, **k: _FakeApiClient(
            fail_with=ApiError("bad payload", status_code=400, retryable=False)
        ),
        retry_queue=retry_queue,
    )

    runner.run_due_work(force=True)

    assert retry_queue.pending() == []


def test_pending_queue_item_is_flushed_before_sending_new_data(tmp_path):
    backend = FakePlatformBackend(hardware=_VALID_HARDWARE, os=_VALID_OS)
    backend.credential_store().set_api_key("deviceid.secret")
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))
    retry_queue = RetryQueueStore(tmp_path / "retry_queue.json")
    retry_queue.enqueue("inventory", {"hostname": "queued-from-yesterday"})

    client = _FakeApiClient()
    runner = _make_runner(
        tmp_path, backend, clock, api_client_factory=lambda *a, **k: client, retry_queue=retry_queue
    )

    runner.run_due_work(force=True)

    assert retry_queue.pending() == []
    assert client.sent_inventory_calls[0] == {"hostname": "queued-from-yesterday"}
    assert len(client.sent_inventory_calls) == 2  # the flushed one, then today's fresh payload
