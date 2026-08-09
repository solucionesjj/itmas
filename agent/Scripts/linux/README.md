# Linux — not implemented yet

There is no `install.sh`/`build.sh` here yet, deliberately — `itmas_agent/platforms/linux/backend.py` is a documented stub (every method raises `NotImplementedError` with the intended approach in its docstring), not a working implementation. Building the actual scripts for this platform means, in order:

1. Implement `LinuxBackend` (see the docstrings in `backend.py` for the concrete API/tool per method — DMI sysfs files for hardware/serial, `/proc/cpuinfo` for CPU model, `lsblk -J` for storage, `pwd`/`grp` stdlib modules for local users/groups — no subprocess needed there at all — and a distro-aware `dpkg`/`rpm` check for installed applications, since unlike macOS there is no single package format across distros).
2. Decide and document (as an ADR, not silently) the credential-storage approach — Linux has no single always-available equivalent of macOS's System keychain for a service with no desktop session; the Secret Service API most `keyring` backends use has the same "needs a session" problem. Likely a root-only file, possibly kernel-keyring-backed.
3. Add a `service_installer()` backed by a systemd timer (`.timer` + `.service` unit pair) with `OnCalendar=*-*-* 09:00:00` and `Persistent=true` — `Persistent=true` is systemd's own direct equivalent of the `StartCalendarInterval` wake-catch-up behavior the macOS LaunchDaemon relies on, so `scheduling.is_daily_run_due()` needs no changes.
4. `build.sh` — PyInstaller `--onefile` targeting `manylinux`/`x86_64` and `aarch64`, run on an `ubuntu-latest` GitHub Actions runner (already wired into `.github/workflows/ci.yml`'s `agent-ubuntu` job, currently only running the shared-core test suite).
5. `install.sh`/`uninstall.sh` — install/remove the systemd unit files, mirroring `Scripts/macos/install.sh`'s structure.

None of the core (`collectors/`, `normalization/`, `networking/`, `persistence/`, `scheduling.py`, `cli.py`) needs to change for any of this — that's the entire point of the `PlatformBackend` seam.
