from __future__ import annotations

import pytest

from itmas_agent.networking.api_client import ApiClient
from itmas_agent.networking.errors import ApiError
from itmas_agent.normalization.dtos import CpuDto, DiskDto, InventoryIngestRequest, OsDto, RamDto

_SAMPLE_REQUEST = InventoryIngestRequest(
    hostname="test-host.local",
    category="collaborator",
    os=OsDto(name="macOS", version="15.1.0"),
    cpu=CpuDto(model="Apple M4 Pro", cores=12),
    ram=RamDto(totalGB=24.0),
    disks=[DiskDto(name="/", sizeGB=460.43)],
    timestamp="2026-08-08T09:00:03Z",
)


def test_successful_send_sends_correct_header_and_body(requests_mock):
    requests_mock.post("https://api.example.com/inventory", status_code=201)

    client = ApiClient(base_url="https://api.example.com", api_key="deviceid.secret")
    client.send_inventory(_SAMPLE_REQUEST)

    sent = requests_mock.request_history[0]
    assert sent.headers["X-Node-Api-Key"] == "deviceid.secret"
    assert sent.json()["hostname"] == "test-host.local"


def test_4xx_other_than_429_is_not_retryable(requests_mock):
    requests_mock.post("https://api.example.com/inventory", status_code=400, text="bad request")

    client = ApiClient(base_url="https://api.example.com", api_key="deviceid.secret")

    with pytest.raises(ApiError) as excinfo:
        client.send_inventory(_SAMPLE_REQUEST)
    assert excinfo.value.retryable is False
    assert excinfo.value.status_code == 400


@pytest.mark.parametrize("status_code", [429, 500, 503])
def test_429_and_5xx_are_retryable(requests_mock, status_code):
    requests_mock.post("https://api.example.com/inventory", status_code=status_code)

    client = ApiClient(base_url="https://api.example.com", api_key="deviceid.secret")

    with pytest.raises(ApiError) as excinfo:
        client.send_inventory(_SAMPLE_REQUEST)
    assert excinfo.value.retryable is True


def test_network_failure_is_retryable(requests_mock):
    import requests

    requests_mock.post(
        "https://api.example.com/inventory", exc=requests.ConnectionError("connection refused")
    )

    client = ApiClient(base_url="https://api.example.com", api_key="deviceid.secret")

    with pytest.raises(ApiError) as excinfo:
        client.send_inventory(_SAMPLE_REQUEST)
    assert excinfo.value.retryable is True
