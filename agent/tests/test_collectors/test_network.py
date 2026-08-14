from __future__ import annotations

import requests

from itmas_agent.collectors.network import NetworkCollector


def test_local_ip_from_socket_trick(mocker):
    fake_socket = mocker.MagicMock()
    fake_socket.getsockname.return_value = ("192.168.1.42", 54321)
    fake_socket.__enter__.return_value = fake_socket
    mocker.patch("itmas_agent.collectors.network.socket.socket", return_value=fake_socket)
    mocker.patch(
        "itmas_agent.collectors.network.requests.get",
        return_value=mocker.Mock(text="201.245.13.87", raise_for_status=lambda: None),
    )

    snapshot = NetworkCollector().collect()

    assert snapshot.local_ip.value == "192.168.1.42"
    assert snapshot.public_ip.value == "201.245.13.87"


def test_no_outbound_route_is_unavailable_not_an_error(mocker):
    mocker.patch(
        "itmas_agent.collectors.network.socket.socket",
        side_effect=OSError("network unreachable"),
    )
    mocker.patch(
        "itmas_agent.collectors.network.requests.get",
        side_effect=requests.RequestException("no internet"),
    )

    snapshot = NetworkCollector().collect()

    assert snapshot.local_ip.value is None
    assert snapshot.local_ip.reason is not None
    assert snapshot.public_ip.value is None
    assert snapshot.public_ip.reason is not None


def test_public_ip_lookup_failure_does_not_block_local_ip(mocker):
    fake_socket = mocker.MagicMock()
    fake_socket.getsockname.return_value = ("10.0.0.5", 1234)
    fake_socket.__enter__.return_value = fake_socket
    mocker.patch("itmas_agent.collectors.network.socket.socket", return_value=fake_socket)
    mocker.patch(
        "itmas_agent.collectors.network.requests.get",
        side_effect=requests.Timeout("timed out"),
    )

    snapshot = NetworkCollector().collect()

    assert snapshot.local_ip.value == "10.0.0.5"
    assert snapshot.public_ip.value is None
