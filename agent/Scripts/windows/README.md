# Windows — not implemented yet

There is no `install.ps1`/`build.ps1` here yet, deliberately — `itmas_agent/platforms/windows/backend.py` is a documented stub (every method raises `NotImplementedError` with the intended approach in its docstring), not a working implementation. Building the actual scripts for this platform means, in order:

1. Implement `WindowsBackend` (see the docstrings in `backend.py` for the concrete API/tool per method — WMI/CIM for hardware, `winreg` for installed applications, `Get-LocalUser`/`Win32_UserAccount` for local users, Credential Locker/DPAPI for the node API key).
2. Add a `service_installer()` backed by Windows Task Scheduler (`schtasks.exe`), with `<StartWhenAvailable>True</StartWhenAvailable>` in the task definition — this is Task Scheduler's own direct equivalent of the `StartCalendarInterval` wake-catch-up behavior the macOS LaunchDaemon relies on, so `scheduling.is_daily_run_due()` needs no changes.
3. `build.ps1` — PyInstaller `--onefile` targeting `win_amd64` (and `win_arm64` if Windows-on-ARM support is ever needed), run on a `windows-latest` GitHub Actions runner (already wired into `.github/workflows/ci.yml`'s `agent-windows` job, currently only running the shared-core test suite).
4. `install.ps1`/`uninstall.ps1` — register/deregister the Task Scheduler task, mirroring `Scripts/macos/install.sh`'s structure (provision check, config write, credential store, service install).

None of the core (`collectors/`, `normalization/`, `networking/`, `persistence/`, `scheduling.py`, `cli.py`) needs to change for any of this — that's the entire point of the `PlatformBackend` seam.
