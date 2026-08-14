"""Rich internal data model shared by every collector, regardless of platform.

Every leaf value is wrapped in `Measured` so a single unavailable reading
(e.g. GPU memory on Apple Silicon) can never abort collection of anything
else. `reason` is diagnostic-only — it stays in local logs, never in the
wire-format JSON sent to the API (see normalization/extended_schema.py).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Generic, Optional, TypeVar

T = TypeVar("T")


@dataclass(frozen=True)
class Measured(Generic[T]):
    value: Optional[T]
    reason: Optional[str] = None

    @classmethod
    def ok(cls, value: T) -> "Measured[T]":
        return cls(value=value, reason=None)

    @classmethod
    def unavailable(cls, reason: str) -> "Measured[T]":
        return cls(value=None, reason=reason)

    @property
    def is_available(self) -> bool:
        return self.value is not None


def iso_z(dt: Optional[datetime]) -> Optional[str]:
    """UTC ISO-8601 with a 'Z' suffix, e.g. "2026-08-08T09:00:03Z" — the one
    timestamp format used on the wire, everywhere (extended JSON contract
    and the current backend's `@IsDateString()` inventory/access-event DTOs
    both accept it).
    """
    if dt is None:
        return None
    return dt.isoformat().replace("+00:00", "Z")


def measured_from_optional(value: Optional[T], reason: str) -> Measured[T]:
    """The one-line pattern every collector uses to wrap a raw Optional
    reading: present it as .ok if non-None, otherwise .unavailable(reason).
    """
    return Measured.ok(value) if value is not None else Measured.unavailable(reason)


@dataclass
class DeviceIdentity:
    device_id: Measured[str]
    hardware_serial: Measured[str]
    hostname: Measured[str]
    category: str  # 'collaborator' | 'infrastructure' — set by local config, not collected


@dataclass
class GpuInfo:
    model: Measured[str]
    cores: Measured[int]
    memory_bytes: Measured[int]  # expected .unavailable("unified memory") on Apple Silicon


@dataclass
class HardwareSnapshot:
    manufacturer: Measured[str]
    model: Measured[str]
    model_identifier: Measured[str]
    processor_model: Measured[str]
    architecture: Measured[str]
    cpu_cores_physical: Measured[int]
    cpu_cores_logical: Measured[int]
    ram_total_bytes: Measured[int]
    gpu: GpuInfo
    storage_total_bytes: Measured[int]
    storage_type: Measured[str]
    disk_count: Measured[int]


@dataclass
class OSSnapshot:
    name: Measured[str]
    version: Measured[str]
    build: Measured[str]
    kernel_version: Measured[str]
    architecture: Measured[str]
    hostname: Measured[str]


@dataclass
class StorageVolume:
    filesystem: str
    mount_point: str
    total_bytes: int
    used_bytes: int
    available_bytes: int
    usage_percent: float


@dataclass
class StorageSnapshot:
    volumes: list[StorageVolume] = field(default_factory=list)


@dataclass
class LocalUserAccount:
    username: str
    uid: int
    full_name: Measured[str]
    home_directory: Measured[str]
    shell: Measured[str]
    account_type: Measured[str]  # 'admin' | 'standard'
    last_login: Measured[datetime]
    groups: list[str] = field(default_factory=list)


@dataclass
class UsersSnapshot:
    users: list[LocalUserAccount] = field(default_factory=list)


@dataclass
class InstalledApplication:
    name: str
    version: Measured[str]
    bundle_id: Measured[str]
    path: str


@dataclass
class ApplicationsSnapshot:
    applications: list[InstalledApplication] = field(default_factory=list)


@dataclass
class ResourceSnapshot:
    cpu_usage_percent: Measured[float]
    memory_usage_percent: Measured[float]
    memory_total_bytes: Measured[int]
    memory_used_bytes: Measured[int]
    uptime_seconds: Measured[float]


@dataclass
class NetworkSnapshot:
    local_ip: Measured[str]
    public_ip: Measured[str]


@dataclass
class InventorySnapshot:
    """Everything collected in one run — the rich, full-fidelity model."""

    schema_version: str
    agent_platform: str
    agent_version: str
    device: DeviceIdentity
    collected_at: datetime
    os: OSSnapshot
    hardware: HardwareSnapshot
    storage: StorageSnapshot
    applications: ApplicationsSnapshot
    users: UsersSnapshot
    resources: ResourceSnapshot
    network: NetworkSnapshot
