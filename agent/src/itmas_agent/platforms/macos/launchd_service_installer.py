"""LaunchDaemon-based ServiceInstaller for macOS.

Runs as a LaunchDaemon, not a LaunchAgent — see agent/docs/ARCHITECTURE.md
for the full justification: a LaunchAgent only exists inside a logged-in
session, which can't satisfy "runs at 9am regardless of who (if anyone) is
logged in" or "sees every local user's data, not just one session's".

`StartCalendarInterval` (9:00) is the normal daily trigger — launchd itself
gives this anacron-like wake catch-up if the Mac was asleep at that time.
`RunAtLoad` covers what that doesn't: a fresh install/reboot happening
after 9am the same day. `scheduling.is_daily_run_due()` is the actual gate
deciding whether either trigger should really run right now — see that
module for why launchd's own behavior alone isn't a complete solution.

Deliberately no `KeepAlive` — each invocation is meant to be short-lived
and exit (the ephemeral-per-invocation model, see
agent/docs/ARCHITECTURE.md), not a persistent resident process.
"""

from __future__ import annotations

import plistlib
import subprocess
from pathlib import Path

_LABEL = "com.ecs-la.itmas.agent"
_PLIST_PATH = Path(f"/Library/LaunchDaemons/{_LABEL}.plist")
_LOG_DIR = Path("/Library/Logs/ITMasAgent")
_TIMEOUT_SECONDS = 15.0


class LaunchdServiceInstaller:
    def install(self, executable_path: Path, config_path: Path) -> None:
        _LOG_DIR.mkdir(parents=True, exist_ok=True)
        _PLIST_PATH.parent.mkdir(parents=True, exist_ok=True)
        plist = {
            "Label": _LABEL,
            "ProgramArguments": [str(executable_path), "run"],
            "StartCalendarInterval": {"Hour": 9, "Minute": 0},
            "RunAtLoad": True,
            "StandardOutPath": str(_LOG_DIR / "launchd.out.log"),
            "StandardErrorPath": str(_LOG_DIR / "launchd.err.log"),
        }
        with _PLIST_PATH.open("wb") as f:
            plistlib.dump(plist, f)
        self._run(["launchctl", "bootstrap", "system", str(_PLIST_PATH)])

    def uninstall(self) -> None:
        self._run(["launchctl", "bootout", f"system/{_LABEL}"], check=False)
        _PLIST_PATH.unlink(missing_ok=True)

    def run_now(self) -> None:
        self._run(["launchctl", "kickstart", "-k", f"system/{_LABEL}"])

    def status(self) -> str:
        result = subprocess.run(
            ["launchctl", "print", f"system/{_LABEL}"],
            capture_output=True,
            text=True,
            timeout=_TIMEOUT_SECONDS,
        )
        return "loaded" if result.returncode == 0 else "not loaded"

    @staticmethod
    def _run(args: list[str], check: bool = True) -> None:
        subprocess.run(
            args, check=check, capture_output=True, text=True, timeout=_TIMEOUT_SECONDS
        )
