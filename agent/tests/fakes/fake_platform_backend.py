"""In-memory PlatformBackend used by every collector test — never touches
the real OS. Construct with sensible defaults, override individual fields
per-test via the constructor kwargs.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

from itmas_agent.platforms.base import (
    HardwareRawInfo,
    OSRawInfo,
    RawAppInfo,
    RawUserInfo,
)


class FakeCredentialStore:
    def __init__(self, api_key: Optional[str] = None) -> None:
        self._api_key = api_key

    def get_api_key(self) -> Optional[str]:
        return self._api_key

    def set_api_key(self, api_key: str) -> None:
        self._api_key = api_key

    def clear_api_key(self) -> None:
        self._api_key = None


class FakeServiceInstaller:
    def __init__(self) -> None:
        self.installed = False
        self.run_now_calls = 0

    def install(self, executable_path: Path, config_path: Path) -> None:
        self.installed = True

    def uninstall(self) -> None:
        self.installed = False

    def run_now(self) -> None:
        self.run_now_calls += 1

    def status(self) -> str:
        return "running" if self.installed else "not installed"


_DEFAULT_HARDWARE = HardwareRawInfo(
    manufacturer="Apple",
    model="MacBook Pro",
    model_identifier="Mac16,7",
    processor_model="Apple M4 Pro",
    gpu_model="Apple M4 Pro GPU",
    gpu_cores=16,
    gpu_memory_bytes=None,
    gpu_memory_unavailable_reason="unified memory",
    storage_total_bytes=549_755_813_888,
    storage_type="SSD",
    disk_count=1,
)

_DEFAULT_OS = OSRawInfo(
    name="macOS",
    version="15.1.0",
    build="24B83",
    kernel_version="Darwin 24.1.0",
    architecture="arm64",
    hostname="test-host.local",
)


class FakePlatformBackend:
    platform_name = "macos"

    def __init__(
        self,
        hardware: Optional[HardwareRawInfo] = None,
        os: Optional[OSRawInfo] = None,
        hardware_serial_value: Optional[str] = "C02XXXXXXX",
        applications: Optional[list[RawAppInfo]] = None,
        users: Optional[list[RawUserInfo]] = None,
    ) -> None:
        self._hardware = hardware or _DEFAULT_HARDWARE
        self._os = os or _DEFAULT_OS
        self._hardware_serial = hardware_serial_value
        self._applications = applications if applications is not None else []
        self._users = users if users is not None else []
        self._credential_store = FakeCredentialStore()
        self._service_installer = FakeServiceInstaller()

    def hardware_info(self) -> HardwareRawInfo:
        return self._hardware

    def hardware_serial(self) -> Optional[str]:
        return self._hardware_serial

    def os_info(self) -> OSRawInfo:
        return self._os

    def installed_applications(self) -> list[RawAppInfo]:
        return self._applications

    def local_users(self) -> list[RawUserInfo]:
        return self._users

    def credential_store(self) -> FakeCredentialStore:
        return self._credential_store

    def service_installer(self) -> FakeServiceInstaller:
        return self._service_installer
