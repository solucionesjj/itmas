"""Linux implementation of PlatformBackend — NOT YET IMPLEMENTED.

Every method below deliberately raises NotImplementedError rather than
returning fabricated or partially-correct data — "not built yet" must never
be mistaken for "genuinely unavailable on this machine" (the former is a
gap to fill in, the latter is a normal `Measured.unavailable(reason)` a
collector produces on its own). Once implemented, each method's return type
must exactly match `itmas_agent.platforms.base` — the collectors in
`itmas_agent/collectors/*` never change; only this class does.

Intended approach per method (for whoever implements this next):

- hardware_info(): `/sys/class/dmi/id/sys_vendor` (manufacturer),
  `/sys/class/dmi/id/product_name` (model) — these DMI sysfs files are the
  kernel's own exposure of the same SMBIOS data `dmidecode` reads, and are
  readable without root (unlike `dmidecode` itself, which typically needs
  root) — prefer them over shelling out where possible. CPU model:
  `/proc/cpuinfo`'s `model name` field. GPU: `lspci -d ::0300` (native
  Linux tool) or, more robustly, glob `/sys/class/drm/card*/device/` for
  vendor/device IDs. Storage: `lsblk -J` (JSON output, same structured-CLI
  approach as macOS's `system_profiler -json`) for disk count/type
  (`rota` field: 0=SSD, 1=HDD — a real, kernel-reported signal, not a
  guess).
- hardware_serial(): `/sys/class/dmi/id/product_serial` (root-readable
  only, typically) or `dmidecode -s system-serial-number` as a fallback —
  document in PERMISSIONS.md if this turns out to need root beyond what
  the LaunchDaemon-equivalent systemd service already runs as.
- os_info(): `platform.freedesktop_os_release()` (stdlib, Python ≥3.10 —
  reads `/etc/os-release`, the standard cross-distro identification file)
  for name/version; `platform.uname()` for kernel_version/architecture,
  same shared pattern as the other two backends.
- installed_applications(): distro-dependent — `dpkg-query -W -f
  '${Package}\\t${Version}\\n'` on Debian/Ubuntu, `rpm -qa
  --queryformat '%{NAME}\\t%{VERSION}\\n'` on RHEL/Fedora. No single
  command covers all distros (unlike macOS's single Info.plist convention)
  — detecting which package manager is present (checking for `dpkg`/`rpm`
  on PATH) is itself part of this implementation, not an afterthought.
- local_users(): `/etc/passwd` (stdlib `pwd` module, not even a subprocess
  call) filtered the same way as macOS — UID >= 1000 is the Linux/Debian
  convention for "real" accounts (analogous to macOS's UID >= 500), though
  this varies slightly by distro and should be verified empirically the
  same way the UID threshold was verified against real macOS behavior
  (see directory_service.py's docstring) rather than assumed from docs.
  Groups: stdlib `grp` module (`/etc/group`), no subprocess needed at all.
- credential_store(): Linux has no single always-available equivalent of
  macOS's System keychain for a system service with no desktop session —
  the Secret Service API (used by `keyring`'s default Linux backend)
  typically requires a logged-in session/D-Bus, the same problem that
  ruled out generic `keyring` on macOS. Most likely approach: a root-only
  (0600, root:root) file containing the key, optionally encrypted via the
  kernel keyring (`keyctl`) rather than Secret Service — needs a decision
  documented as an ADR when this is implemented, not assumed here.
- service_installer(): a systemd timer unit (`.timer` + `.service` pair)
  with `OnCalendar=*-*-* 09:00:00` and `Persistent=true` in the `[Timer]`
  section — `Persistent=true` is systemd's own direct equivalent of
  launchd's StartCalendarInterval wake-catch-up behavior, so
  `scheduling.is_daily_run_due()` (already OS-agnostic) needs no changes.
"""

from __future__ import annotations

from itmas_agent.platforms.base import HardwareRawInfo, OSRawInfo, RawAppInfo, RawUserInfo

_NOT_IMPLEMENTED = (
    "LinuxBackend.{method}() is not implemented yet — see the module "
    "docstring in itmas_agent/platforms/linux/backend.py for the intended approach."
)


class LinuxBackend:
    platform_name = "linux"

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
