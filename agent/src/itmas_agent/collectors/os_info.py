"""OS info collector — shared logic, backed by whatever PlatformBackend is
injected. OS name/version/build/kernel/hostname rarely change, so this runs
on the slow (daily) tier.
"""

from __future__ import annotations

from itmas_agent.models import OSSnapshot, measured_from_optional
from itmas_agent.platforms.base import PlatformBackend

_UNAVAILABLE_REASON = "not reported by platform backend"


class OSInfoCollector:
    key = "os_info"
    default_interval_seconds = 24 * 60 * 60

    def __init__(self, backend: PlatformBackend) -> None:
        self._backend = backend

    def collect(self) -> OSSnapshot:
        raw = self._backend.os_info()
        return OSSnapshot(
            name=measured_from_optional(raw.name, _UNAVAILABLE_REASON),
            version=measured_from_optional(raw.version, _UNAVAILABLE_REASON),
            build=measured_from_optional(raw.build, _UNAVAILABLE_REASON),
            kernel_version=measured_from_optional(raw.kernel_version, _UNAVAILABLE_REASON),
            architecture=measured_from_optional(raw.architecture, _UNAVAILABLE_REASON),
            hostname=measured_from_optional(raw.hostname, _UNAVAILABLE_REASON),
        )
