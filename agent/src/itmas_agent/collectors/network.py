"""Network info collector.

Local IP: a POSIX UDP socket trick that works identically on macOS/Windows/
Linux (no packets are actually sent — connect() on a UDP socket just makes
the OS resolve which local interface/address it would use, then
getsockname() reads that back) — no PlatformBackend needed.

Public IP: best-effort outbound HTTPS call to a configurable third-party
echo service. Documented privacy note (see agent/docs/PERMISSIONS.md): this
reveals the machine's public IP to that service — nothing else.
"""

from __future__ import annotations

import socket

import requests

from itmas_agent.models import Measured, NetworkSnapshot

_DEFAULT_PUBLIC_IP_URL = "https://api.ipify.org?format=text"
_PUBLIC_IP_TIMEOUT_SECONDS = 5.0


class NetworkCollector:
    key = "network"
    default_interval_seconds = 24 * 60 * 60

    def __init__(self, public_ip_lookup_url: str = _DEFAULT_PUBLIC_IP_URL) -> None:
        self._public_ip_lookup_url = public_ip_lookup_url

    def collect(self) -> NetworkSnapshot:
        return NetworkSnapshot(local_ip=self._local_ip(), public_ip=self._public_ip())

    @staticmethod
    def _local_ip() -> Measured[str]:
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
                sock.connect(("8.8.8.8", 80))
                return Measured.ok(sock.getsockname()[0])
        except OSError as exc:
            return Measured.unavailable(f"no outbound network route: {exc}")

    def _public_ip(self) -> Measured[str]:
        try:
            response = requests.get(self._public_ip_lookup_url, timeout=_PUBLIC_IP_TIMEOUT_SECONDS)
            response.raise_for_status()
        except requests.RequestException as exc:
            return Measured.unavailable(f"public IP lookup failed: {exc}")
        ip = response.text.strip()
        return Measured.ok(ip) if ip else Measured.unavailable("empty response from IP lookup service")
