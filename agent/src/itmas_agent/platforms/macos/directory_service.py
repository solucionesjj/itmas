"""Wrapper around macOS's own Open Directory command-line tool (`dscl`) and
`id` — both native macOS/BSD tools, not foreign commands. Two verified
findings from testing against a real machine that changed the original
approach:

1. The `IsHidden` directory attribute — originally planned as the primary
   signal for "is this a system account" — is simply not populated on any
   account (real or system) on current macOS. The UID >= 500 threshold
   alone correctly separates real accounts from every `_service` account
   plus `root`/`daemon`/`nobody` (all UID < 500), so it's used as the only
   filter.
2. `dscl -plist . -list /Users <attr>` silently ignores `-plist` and falls
   back to plain two-column text — only `-read` honors `-plist` reliably.
   So the cheap first pass (list every username+UID to find real accounts)
   parses that plain text with a regex, and only the per-user detail read
   for the handful of surviving real accounts uses `-plist` (via
   `plistlib`, avoiding `-read`'s ambiguous multi-line value format).
   Also: `-read /Users/<u>` with no attribute names dumps EVERYTHING,
   including a large binary avatar image blob — always name exact
   attributes.
"""

from __future__ import annotations

import plistlib
import re
import subprocess
from typing import Optional

_TIMEOUT_SECONDS = 10.0
_REAL_ACCOUNT_MIN_UID = 500
_ATTR_PREFIX = "dsAttrTypeStandard:"
_LIST_LINE_PATTERN = re.compile(r"^(\S+)\s+(-?\d+)$")


def list_real_user_uids() -> dict[str, int]:
    """username -> UID, filtered to UID >= 500 (real accounts; excludes
    every `_service` account and root/daemon/nobody).
    """
    try:
        result = subprocess.run(
            ["dscl", ".", "-list", "/Users", "UniqueID"],
            capture_output=True,
            text=True,
            timeout=_TIMEOUT_SECONDS,
            check=True,
        )
    except (subprocess.SubprocessError, OSError):
        return {}

    accounts: dict[str, int] = {}
    for line in result.stdout.splitlines():
        match = _LIST_LINE_PATTERN.match(line.strip())
        if not match:
            continue
        username, uid_str = match.groups()
        uid = int(uid_str)
        if uid >= _REAL_ACCOUNT_MIN_UID:
            accounts[username] = uid
    return accounts


def read_user_attributes(username: str) -> Optional[dict]:
    """RealName/NFSHomeDirectory/UserShell for one user, via -plist -read
    (reliable structured parsing) requesting ONLY these attributes (never a
    blanket -read, which pulls in avatar images and other unrelated data).
    """
    try:
        result = subprocess.run(
            [
                "dscl",
                "-plist",
                ".",
                "-read",
                f"/Users/{username}",
                "RealName",
                "NFSHomeDirectory",
                "UserShell",
            ],
            capture_output=True,
            text=True,
            timeout=_TIMEOUT_SECONDS,
            check=True,
        )
    except (subprocess.SubprocessError, OSError):
        return None

    try:
        parsed = plistlib.loads(result.stdout.encode("utf-8"))
    except Exception:
        return None

    def first(attr: str) -> Optional[str]:
        values = parsed.get(f"{_ATTR_PREFIX}{attr}")
        return values[0] if values else None

    return {
        "full_name": first("RealName"),
        "home_directory": first("NFSHomeDirectory"),
        "shell": first("UserShell"),
    }


def list_groups(username: str) -> list[str]:
    """All group names this user belongs to (including nested/system
    groups) — `id -Gn` resolves group membership the same way the kernel
    would, unlike a flat GroupMembership attribute lookup which misses
    groups the user belongs to via primary-GID-only membership.
    """
    try:
        result = subprocess.run(
            ["id", "-Gn", username],
            capture_output=True,
            text=True,
            timeout=_TIMEOUT_SECONDS,
            check=True,
        )
    except (subprocess.SubprocessError, OSError):
        return []
    return result.stdout.split()
