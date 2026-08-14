"""Windows implementation of PlatformBackend — NOT YET IMPLEMENTED.

Every method below deliberately raises NotImplementedError rather than
returning fabricated or partially-correct data — "not built yet" must never
be mistaken for "genuinely unavailable on this machine" (the former is a
gap to fill in, the latter is a normal `Measured.unavailable(reason)` a
collector produces on its own). Once implemented, each method's return type
must exactly match `itmas_agent.platforms.base` — the collectors in
`itmas_agent/collectors/*` never change; only this class does.

Intended approach per method (for whoever implements this next):

- hardware_info(): `Get-CimInstance Win32_ComputerSystem` (manufacturer,
  model), `Win32_Processor` (name/model), `Win32_VideoController`
  (GPU name + AdapterRAM for memoryBytes — unlike Apple Silicon's unified
  memory, Windows GPUs generally do report a real VRAM figure),
  `Win32_DiskDrive` (count, model, MediaType for storage_type) — all via
  the `wmi` package (a thin COM wrapper) or by shelling out to
  `powershell -Command "Get-CimInstance ... | ConvertTo-Json"` (Windows'
  own native tooling, not a foreign command — parseable the same way
  system_profiler's `-json` output is on macOS).
- hardware_serial(): `Get-CimInstance Win32_BIOS | Select SerialNumber`
  (the direct Windows analog of macOS's `system_profiler` serial_number).
- os_info(): `platform.win32_ver()`/`platform.win32_edition()` (stdlib) for
  name/version/build; `platform.uname()` still gives architecture/hostname
  uniformly, same as the macOS backend already does in the shared code path
  where possible.
- installed_applications(): enumerate
  `HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall` (and the
  `WOW6432Node` counterpart for 32-bit apps) via the stdlib `winreg`
  module — this is Windows' own equivalent of macOS's Info.plist scan,
  giving DisplayName/DisplayVersion/InstallLocation directly, no shelling
  out needed.
- local_users(): `Get-LocalUser` / `Get-LocalGroupMember Administrators`
  (PowerShell, or `Win32_UserAccount`/`Win32_Group` via WMI) — direct
  analog of macOS's `dscl`; "admin" account_type maps to membership in the
  built-in Administrators group.
- credential_store(): Windows Credential Locker via `keyring`'s Windows
  backend (unlike macOS, a Windows *service* account's Credential Locker
  IS reliably accessible without a desktop session — the constraint that
  ruled out the generic `keyring` package on macOS doesn't apply here) or
  DPAPI directly (`CryptProtectData`/`CryptUnprotectData` via `ctypes`).
- service_installer(): Windows Task Scheduler via `schtasks.exe /Create
  /SC DAILY /ST 09:00 ...` with `<StartWhenAvailable>True</StartWhenAvailable>`
  in the underlying task XML — this is Task Scheduler's own direct
  equivalent of launchd's StartCalendarInterval wake-catch-up behavior, so
  `scheduling.is_daily_run_due()` (already OS-agnostic) needs no changes.
"""

from __future__ import annotations

from itmas_agent.platforms.base import HardwareRawInfo, OSRawInfo, RawAppInfo, RawUserInfo

_NOT_IMPLEMENTED = (
    "WindowsBackend.{method}() is not implemented yet — see the module "
    "docstring in itmas_agent/platforms/windows/backend.py for the intended approach."
)


class WindowsBackend:
    platform_name = "windows"

    def hardware_info(self) -> HardwareRawInfo:
        raise NotImplementedError(_NOT_IMPLEMENTED.format(method="hardware_info"))

    def hardware_serial(self):
        raise NotImplementedError(_NOT_IMPLEMENTED.format(method="hardware_serial"))

    def os_info(self) -> OSRawInfo:
        raise NotImplementedError(_NOT_IMPLEMENTED.format(method="os_info"))

    def installed_applications(self) -> list[RawAppInfo]:
        raise NotImplementedError(_NOT_IMPLEMENTED.format(method="installed_applications"))

    def local_users(self) -> list[RawUserInfo]:
        raise NotImplementedError(_NOT_IMPLEMENTED.format(method="local_users"))

    def credential_store(self):
        raise NotImplementedError(_NOT_IMPLEMENTED.format(method="credential_store"))

    def service_installer(self):
        raise NotImplementedError(_NOT_IMPLEMENTED.format(method="service_installer"))
