"""Per-volume storage collector — conceptually `df -h`, structured.

Uses only `psutil` (already uniform across macOS/Windows/Linux) — no
PlatformBackend involved. Hidden/pseudo mounts are excluded using the same
signal macOS itself uses to hide them from Finder/`df`: the `dontbrowse`
mount option (macOS's MNT_DONTBROWSE flag, surfaced by psutil in `opts`),
confirmed against a real machine's APFS system volumes (Preboot, VM,
Update, Data, etc. all carry it; `/` and a real mounted disk image do not).
`devfs`/`autofs`/`nullfs` are excluded outright regardless of that flag.
"""

from __future__ import annotations

import psutil

from itmas_agent.models import StorageSnapshot, StorageVolume

_EXCLUDED_FSTYPES = {"devfs", "autofs", "nullfs"}


def _is_hidden(opts: str) -> bool:
    return "dontbrowse" in opts.split(",")


class StorageCollector:
    key = "storage"
    default_interval_seconds = 24 * 60 * 60

    def collect(self) -> StorageSnapshot:
        volumes: list[StorageVolume] = []
        for partition in psutil.disk_partitions(all=True):
            if partition.fstype in _EXCLUDED_FSTYPES or _is_hidden(partition.opts):
                continue
            try:
                usage = psutil.disk_usage(partition.mountpoint)
            except (OSError, PermissionError):
                continue
            volumes.append(
                StorageVolume(
                    filesystem=partition.device,
                    mount_point=partition.mountpoint,
                    total_bytes=usage.total,
                    used_bytes=usage.used,
                    available_bytes=usage.free,
                    usage_percent=usage.percent,
                )
            )
        return StorageSnapshot(volumes=volumes)
