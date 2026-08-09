"""macOS implementation of PlatformBackend — complete.

Every method is backed by macOS's own CLI tools (system_profiler, dscl,
security, launchctl) or native stdlib (platform, socket, plistlib) — no
foreign/Linux commands and no TCC-gated resource, per
agent/docs/PERMISSIONS.md.
"""

from __future__ import annotations

import platform as _platform
import plistlib
import re
import socket
import subprocess
from pathlib import Path
from typing import Optional

from itmas_agent.platforms.base import HardwareRawInfo, OSRawInfo, RawAppInfo, RawUserInfo
from itmas_agent.platforms.macos import directory_service, system_profiler
from itmas_agent.platforms.macos.keychain_credential_store import KeychainCredentialStore
from itmas_agent.platforms.macos.launchd_service_installer import LaunchdServiceInstaller

_SYSCTL_TIMEOUT_SECONDS = 5.0

# Fixed, non-recursive scan roots — deliberately NOT a recursive filesystem
# walk (per spec: avoid full filesystem scans, and "aplicaciones instaladas"
# means what an admin finds in /Applications and its standard equivalents,
# not every executable on disk). Per-user `~/Applications` dirs are
# discovered separately, one level under /Users.
_APPLICATION_ROOTS = (
    Path("/Applications"),
    Path("/Applications/Utilities"),
    Path("/System/Applications"),
    Path("/System/Applications/Utilities"),
)

_SIZE_UNIT_MULTIPLIERS = {
    "TB": 1024**4,
    "GB": 1024**3,
    "MB": 1024**2,
    "KB": 1024,
}


def _parse_size_string(text: str) -> Optional[int]:
    """"1536 MB" / "16 GB" -> bytes. Returns None on anything unexpected —
    never guesses.
    """
    match = re.match(r"^\s*([\d.]+)\s*(TB|GB|MB|KB)\s*$", text.strip(), re.IGNORECASE)
    if not match:
        return None
    number, unit = match.groups()
    try:
        return int(float(number) * _SIZE_UNIT_MULTIPLIERS[unit.upper()])
    except ValueError:
        return None


def _sysctl(name: str) -> Optional[str]:
    try:
        result = subprocess.run(
            ["sysctl", "-n", name],
            capture_output=True,
            text=True,
            timeout=_SYSCTL_TIMEOUT_SECONDS,
            check=True,
        )
    except (subprocess.SubprocessError, OSError):
        return None
    value = result.stdout.strip()
    return value or None


class MacOSBackend:
    platform_name = "macos"

    # -- hardware -----------------------------------------------------

    def hardware_info(self) -> HardwareRawInfo:
        overview = system_profiler.get_hardware_overview() or {}
        processor_model = overview.get("chip_type") or overview.get("cpu_type")
        model = overview.get("machine_name")
        model_identifier = overview.get("machine_model")

        gpu_model, gpu_cores, gpu_memory_bytes, gpu_memory_reason = self._gpu_info()
        storage_total_bytes, storage_type, disk_count = self._internal_storage_summary()

        return HardwareRawInfo(
            manufacturer="Apple",
            model=model,
            model_identifier=model_identifier,
            processor_model=processor_model,
            gpu_model=gpu_model,
            gpu_cores=gpu_cores,
            gpu_memory_bytes=gpu_memory_bytes,
            gpu_memory_unavailable_reason=gpu_memory_reason,
            storage_total_bytes=storage_total_bytes,
            storage_type=storage_type,
            disk_count=disk_count,
        )

    def hardware_serial(self) -> Optional[str]:
        overview = system_profiler.get_hardware_overview()
        if not overview:
            return None
        return overview.get("serial_number") or None

    def _gpu_info(
        self,
    ) -> tuple[Optional[str], Optional[int], Optional[int], Optional[str]]:
        displays = system_profiler.get_displays()
        if not displays:
            return None, None, None, None

        primary = displays[0]
        model = primary.get("sppci_model") or primary.get("_name")
        cores_raw = primary.get("sppci_cores")
        cores = int(cores_raw) if cores_raw and str(cores_raw).isdigit() else None

        # Apple Silicon entries carry no VRAM key at all (unified memory).
        # Discrete-GPU Intel Macs report one of these instead.
        vram_raw = primary.get("spdisplays_vram") or primary.get("spdisplays_vram_shared")
        if vram_raw:
            return model, cores, _parse_size_string(str(vram_raw)), None

        is_apple_silicon = bool(model) and "apple" in model.lower()
        reason = "unified memory" if is_apple_silicon else "not reported by system_profiler"
        return model, cores, None, reason

    def _internal_storage_summary(
        self,
    ) -> tuple[Optional[int], Optional[str], Optional[int]]:
        entries = system_profiler.get_storage_entries()
        internal = [
            e for e in entries if e.get("physical_drive", {}).get("is_internal_disk") == "yes"
        ]
        if not internal:
            return None, None, None

        # Multiple APFS volumes (e.g. "Macintosh HD" + "Macintosh HD - Data")
        # share one physical container and report the same size_in_bytes —
        # dedupe on the physical device name before summing, or capacity
        # would be double-counted.
        by_device: dict[str, dict] = {}
        for entry in internal:
            device_name = entry.get("physical_drive", {}).get("device_name") or entry.get(
                "bsd_name", ""
            )
            by_device.setdefault(device_name, entry)

        total_bytes = sum(e.get("size_in_bytes", 0) or 0 for e in by_device.values())
        medium_types = {
            e.get("physical_drive", {}).get("medium_type") for e in by_device.values()
        }
        medium_types.discard(None)
        storage_type = next(iter(medium_types)).upper() if len(medium_types) == 1 else None

        return total_bytes or None, storage_type, len(by_device) or None

    # -- OS -------------------------------------------------------------

    def os_info(self) -> OSRawInfo:
        version, _, mac_ver_arch = _platform.mac_ver()
        uname = _platform.uname()
        return OSRawInfo(
            name="macOS",
            version=version or None,
            build=_sysctl("kern.osversion"),
            kernel_version=f"Darwin {uname.release}" if uname.release else None,
            architecture=mac_ver_arch or uname.machine or None,
            hostname=socket.gethostname() or None,
        )

    # -- applications -----------------------------------------------------

    def installed_applications(self) -> list[RawAppInfo]:
        seen_paths: set[str] = set()
        apps: list[RawAppInfo] = []
        for root in self._application_scan_roots():
            for app_info in self._scan_app_bundles(root):
                if app_info.path in seen_paths:
                    continue
                seen_paths.add(app_info.path)
                apps.append(app_info)
        return apps

    @staticmethod
    def _application_scan_roots() -> list[Path]:
        roots = list(_APPLICATION_ROOTS)
        # One level under /Users — real user home directories only live
        # there (system/service account homes live elsewhere, e.g.
        # /var/empty), so no dscl lookup is needed just to find these.
        roots.extend(sorted(Path("/Users").glob("*/Applications")))
        return roots

    @classmethod
    def _scan_app_bundles(cls, root: Path) -> list[RawAppInfo]:
        try:
            entries = sorted(root.iterdir())
        except (OSError, PermissionError):
            return []
        apps: list[RawAppInfo] = []
        for entry in entries:
            if entry.suffix == ".app" and entry.is_dir():
                app_info = cls._read_app_bundle(entry)
                if app_info:
                    apps.append(app_info)
        return apps

    @staticmethod
    def _read_app_bundle(bundle_path: Path) -> Optional[RawAppInfo]:
        info_plist = bundle_path / "Contents" / "Info.plist"
        try:
            with info_plist.open("rb") as f:
                info = plistlib.load(f)
        except (OSError, PermissionError, plistlib.InvalidFileException):
            info = {}

        # CFBundleDisplayName is the user-facing name shown in Finder/
        # Launchpad and usually matches the folder name (e.g. "Google
        # Chrome"); CFBundleName is often a shorter internal name (e.g.
        # "Chrome") — confirmed by inspecting real installed apps, not
        # assumed. Falls back to the folder name if neither is present.
        name = (
            info.get("CFBundleDisplayName")
            or info.get("CFBundleName")
            or bundle_path.stem
        )
        return RawAppInfo(
            name=name,
            version=info.get("CFBundleShortVersionString"),
            bundle_id=info.get("CFBundleIdentifier"),
            path=str(bundle_path),
        )

    # -- users ------------------------------------------------------------

    def local_users(self) -> list[RawUserInfo]:
        uid_by_username = directory_service.list_real_user_uids()
        users: list[RawUserInfo] = []
        for username, uid in uid_by_username.items():
            attrs = directory_service.read_user_attributes(username) or {}
            groups = directory_service.list_groups(username)
            users.append(
                RawUserInfo(
                    username=username,
                    uid=uid,
                    full_name=attrs.get("full_name"),
                    home_directory=attrs.get("home_directory"),
                    shell=attrs.get("shell"),
                    is_admin=("admin" in groups) if groups else None,
                    groups=groups,
                )
            )
        return users

    # -- credentials / service --------------------------------------------

    def credential_store(self) -> KeychainCredentialStore:
        return KeychainCredentialStore()

    def service_installer(self) -> LaunchdServiceInstaller:
        return LaunchdServiceInstaller()
