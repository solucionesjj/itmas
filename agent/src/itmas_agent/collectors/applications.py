"""Installed-applications collector — entirely OS-specific (bundle/package
formats have nothing in common across OSes), so this is a thin pass-through
over the injected PlatformBackend. Deduping happens in the backend (by
absolute path, the natural key for "one installed copy of an app").
"""

from __future__ import annotations

from itmas_agent.models import ApplicationsSnapshot, InstalledApplication, measured_from_optional
from itmas_agent.platforms.base import PlatformBackend

_UNAVAILABLE_REASON = "not reported by platform backend"


class ApplicationsCollector:
    key = "applications"
    default_interval_seconds = 24 * 60 * 60

    def __init__(self, backend: PlatformBackend) -> None:
        self._backend = backend

    def collect(self) -> ApplicationsSnapshot:
        apps = [
            InstalledApplication(
                name=raw.name,
                version=measured_from_optional(raw.version, _UNAVAILABLE_REASON),
                bundle_id=measured_from_optional(raw.bundle_id, _UNAVAILABLE_REASON),
                path=raw.path,
            )
            for raw in self._backend.installed_applications()
        ]
        return ApplicationsSnapshot(applications=apps)
