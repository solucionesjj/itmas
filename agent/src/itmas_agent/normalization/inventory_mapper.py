"""Maps the rich internal InventorySnapshot to the narrow InventoryIngestRequest
matching the CURRENT backend contract (see normalization/dtos.py). This is
the ONLY place that knows the current contract is strict — extending it
later (once the backend accepts more fields) means adding cases here, never
touching a collector.

If a field the current contract REQUIRES is unavailable, mapping fails
loudly (MappingError) rather than fabricating a placeholder. These fields
(hostname, os.name/version, cpu.model/cores, ram.totalGB, disks) come from
sysctl/system_profiler/psutil calls that essentially never fail on real
hardware — unlike GPU memory, which is legitimately null on Apple Silicon
and never enters this contract at all. A mapping failure means this cycle
sends nothing; the caller logs it as a genuine anomaly and it's retried
naturally on the next scheduled run.
"""

from __future__ import annotations

from typing import Optional, TypeVar

from itmas_agent.models import InventorySnapshot, iso_z
from itmas_agent.normalization.dtos import CpuDto, DiskDto, InventoryIngestRequest, OsDto, RamDto

T = TypeVar("T")

_BYTES_PER_GB = 1024**3


class MappingError(Exception):
    """A contract-required field was unavailable — nothing gets sent this cycle."""


def _require(value: Optional[T], field_name: str) -> T:
    if value is None:
        raise MappingError(
            f"required field '{field_name}' is unavailable — cannot build inventory payload"
        )
    return value


def map_to_inventory_request(snapshot: InventorySnapshot) -> InventoryIngestRequest:
    hostname = _require(snapshot.device.hostname.value, "hostname")
    category = snapshot.device.category  # set locally from config, never collected

    os_name = _require(snapshot.os.name.value, "os.name")
    os_version = _require(snapshot.os.version.value, "os.version")

    cpu_model = _require(snapshot.hardware.processor_model.value, "cpu.model")
    cpu_cores = _require(snapshot.hardware.cpu_cores_physical.value, "cpu.cores")

    ram_total_bytes = _require(snapshot.hardware.ram_total_bytes.value, "ram.totalGB")

    disks = _build_disks(snapshot)
    if not disks:
        raise MappingError("disks[] is empty — the backend requires at least 1 entry")

    return InventoryIngestRequest(
        hostname=hostname,
        category=category,
        os=OsDto(name=os_name, version=os_version),
        cpu=CpuDto(model=cpu_model, cores=cpu_cores),
        ram=RamDto(totalGB=round(ram_total_bytes / _BYTES_PER_GB, 2)),
        disks=disks,
        timestamp=iso_z(snapshot.collected_at),
    )


def _build_disks(snapshot: InventorySnapshot) -> list[DiskDto]:
    """Prefer the per-volume storage snapshot (matches `df`-style output);
    fall back to the hardware summary's single aggregate figure if the
    storage collector hasn't produced anything this cycle.
    """
    volumes = snapshot.storage.volumes
    if volumes:
        return [
            DiskDto(name=v.mount_point, sizeGB=round(v.total_bytes / _BYTES_PER_GB, 2))
            for v in volumes
        ]
    total_bytes = snapshot.hardware.storage_total_bytes.value
    if total_bytes:
        return [DiskDto(name="TotalStorage", sizeGB=round(total_bytes / _BYTES_PER_GB, 2))]
    return []
