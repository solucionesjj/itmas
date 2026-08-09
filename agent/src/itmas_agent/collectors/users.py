"""Local user account collector.

username/uid/shell/home/groups/account_type come from the injected
PlatformBackend (genuinely OS-specific — directory service lookups differ
completely between macOS/Windows/Linux). `last_login` comes from
`psutil.users()` (already uniform across OSes): the most recent currently-
active session per username, observed at collection time.

Documented limitation (required by spec — never infer, always say so): with
a once-daily collection cadence, this only sees whoever is logged in right
at that moment. A session that starts and fully ends between two daily runs
is invisible to both samples, so `last_login` can legitimately be
`unavailable` for a user who did log in that day. This is why v1 does not
attempt to emit granular login/logout events to `/access-events` from this
same source — see agent/docs/ARCHITECTURE.md.
"""

from __future__ import annotations

from datetime import datetime, timezone

import psutil

from itmas_agent.models import LocalUserAccount, Measured, UsersSnapshot, measured_from_optional
from itmas_agent.platforms.base import PlatformBackend

_BACKEND_UNAVAILABLE_REASON = "not reported by platform backend"
_NO_ACTIVE_SESSION_REASON = "no active session observed at collection time"


class UsersCollector:
    key = "users"
    default_interval_seconds = 24 * 60 * 60

    def __init__(self, backend: PlatformBackend) -> None:
        self._backend = backend

    def collect(self) -> UsersSnapshot:
        last_login_by_username = self._last_login_from_active_sessions()

        accounts: list[LocalUserAccount] = []
        for raw in self._backend.local_users():
            account_type = self._account_type(raw.is_admin)
            accounts.append(
                LocalUserAccount(
                    username=raw.username,
                    uid=raw.uid,
                    full_name=measured_from_optional(raw.full_name, _BACKEND_UNAVAILABLE_REASON),
                    home_directory=measured_from_optional(
                        raw.home_directory, _BACKEND_UNAVAILABLE_REASON
                    ),
                    shell=measured_from_optional(raw.shell, _BACKEND_UNAVAILABLE_REASON),
                    account_type=account_type,
                    last_login=measured_from_optional(
                        last_login_by_username.get(raw.username), _NO_ACTIVE_SESSION_REASON
                    ),
                    groups=raw.groups,
                )
            )
        return UsersSnapshot(users=accounts)

    @staticmethod
    def _account_type(is_admin) -> Measured[str]:
        if is_admin is None:
            return Measured.unavailable(_BACKEND_UNAVAILABLE_REASON)
        return Measured.ok("admin" if is_admin else "standard")

    @staticmethod
    def _last_login_from_active_sessions() -> dict[str, datetime]:
        try:
            sessions = psutil.users()
        except Exception:
            return {}
        latest: dict[str, datetime] = {}
        for session in sessions:
            started = datetime.fromtimestamp(session.started, tz=timezone.utc)
            if session.name not in latest or started > latest[session.name]:
                latest[session.name] = started
        return latest
