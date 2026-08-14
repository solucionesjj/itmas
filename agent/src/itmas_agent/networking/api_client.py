"""HTTPS client for the backend's POST /inventory and POST /access-events.

Sends the X-Node-Api-Key header in the exact `<deviceId>.<secret>` format
verified against backend/src/modules/devices/node-api-key.guard.ts. The API
key is never logged here or anywhere it's passed through — see
logging_setup.py's redaction filter, which this module's callers rely on.
"""

from __future__ import annotations

from typing import Union

import requests

from itmas_agent.networking.errors import ApiError
from itmas_agent.normalization.dtos import AccessEventIngestRequest, InventoryIngestRequest

_TIMEOUT_SECONDS = 15.0
_API_KEY_HEADER = "X-Node-Api-Key"


class ApiClient:
    def __init__(
        self, base_url: str, api_key: str, verify: Union[bool, str] = True
    ) -> None:
        self._base_url = base_url.rstrip("/")
        self._api_key = api_key
        self._verify = verify

    def send_inventory(self, request: InventoryIngestRequest) -> None:
        self.send_inventory_json(request.to_json_dict())

    def send_inventory_json(self, json_body: dict) -> None:
        """Also used to resend an already-serialized payload straight from
        RetryQueueStore, without reconstructing an InventoryIngestRequest.
        """
        self._post("/inventory", json_body)

    def send_access_event(self, request: AccessEventIngestRequest) -> None:
        self.send_access_event_json(request.to_json_dict())

    def send_access_event_json(self, json_body: dict) -> None:
        self._post("/access-events", json_body)

    def _post(self, path: str, json_body: dict) -> None:
        try:
            response = requests.post(
                f"{self._base_url}{path}",
                json=json_body,
                headers={_API_KEY_HEADER: self._api_key},
                timeout=_TIMEOUT_SECONDS,
                verify=self._verify,
            )
        except requests.RequestException as exc:
            raise ApiError(f"network error calling {path}: {exc}", retryable=True) from exc

        if response.status_code == 201:
            return

        # 429/5xx/network are transient — worth retrying later. Any other
        # 4xx means the payload or auth itself is wrong; retrying it
        # unchanged would just fail again, so it's not retryable.
        retryable = response.status_code == 429 or response.status_code >= 500
        raise ApiError(
            f"{path} returned {response.status_code}: {response.text[:200]}",
            status_code=response.status_code,
            retryable=retryable,
        )
