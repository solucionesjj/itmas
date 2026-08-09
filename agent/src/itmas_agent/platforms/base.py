"""The seam between OS-agnostic collectors and OS-specific implementations.

Anything `psutil` already handles uniformly (storage, CPU/memory, active
sessions, local IP) never goes through `PlatformBackend` — it stays directly
in the shared collectors. Only what genuinely differs per OS (hardware
inspection, installed-application enumeration, local user/group directory,
credential storage, service installation) is behind this Protocol.

Adding a new OS means implementing this Protocol once — zero changes to
collectors, normalization, networking, persistence, or the CLI.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional, Protocol


@dataclass
class HardwareRawInfo:
    """Only what genuinely needs an OS-specific lookup. CPU core counts, RAM
    total, and architecture are deliberately NOT here — `psutil`/`platform`
    already resolve those identically on macOS/Windows/Linux, so
    `collectors/hardware.py` reads them directly instead of routing through
    a backend that would just be a thin, redundant wrapper around psutil.
    """

    manufacturer: Optional[str]
    model: Optional[str]
    model_identifier: Optional[str]
    processor_model: Optional[str]
    gpu_model: Optional[str]
    gpu_cores: Optional[int]
    gpu_memory_bytes: Optional[int]
    # Set when memory is legitimately not applicable (e.g. Apple Silicon's
    # unified memory) rather than genuinely unknown — lets the collector
    # produce a more honest Measured.unavailable(reason) than a bare None.
    gpu_memory_unavailable_reason: Optional[str] = None
    storage_total_bytes: Optional[int] = None
    storage_type: Optional[str] = None
    disk_count: Optional[int] = None


@dataclass
class OSRawInfo:
    name: Optional[str]
    version: Optional[str]
    build: Optional[str]
    kernel_version: Optional[str]
    architecture: Optional[str]
    hostname: Optional[str]


@dataclass
class RawAppInfo:
    name: str
    version: Optional[str]
    bundle_id: Optional[str]
    path: str


@dataclass
class RawUserInfo:
    username: str
    uid: int
    full_name: Optional[str]
    home_directory: Optional[str]
    shell: Optional[str]
    is_admin: Optional[bool]
    groups: list[str] = field(default_factory=list)


class CredentialStore(Protocol):
    """Storage for the one genuinely sensitive local artifact: the node API key."""

    def get_api_key(self) -> Optional[str]: ...

    def set_api_key(self, api_key: str) -> None: ...

    def clear_api_key(self) -> None: ...


class ServiceInstaller(Protocol):
    """Wraps whatever this OS's native scheduler/service manager is."""

    def install(self, executable_path: Path, config_path: Path) -> None: ...

    def uninstall(self) -> None: ...

    def run_now(self) -> None: ...

    def status(self) -> str: ...


class PlatformBackend(Protocol):
    """Everything a collector needs that genuinely differs by OS."""

    platform_name: str  # 'macos' | 'windows' | 'linux'

    def hardware_info(self) -> HardwareRawInfo: ...

    def hardware_serial(self) -> Optional[str]: ...

    def os_info(self) -> OSRawInfo: ...

    def installed_applications(self) -> list[RawAppInfo]: ...

    def local_users(self) -> list[RawUserInfo]: ...

    def credential_store(self) -> CredentialStore: ...

    def service_installer(self) -> ServiceInstaller: ...
