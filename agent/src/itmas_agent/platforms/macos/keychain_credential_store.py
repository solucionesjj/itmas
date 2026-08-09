"""Stores the node API key in the macOS SYSTEM keychain — not the login
keychain. A root LaunchDaemon runs before any user session exists, so a
login keychain would never be unlocked for it; the System keychain is the
one that's unlocked at boot without a session, which is why this is a
necessity here, not just a preference. Uses the `security` CLI directly
(targeting /Library/Keychains/System.keychain explicitly), not the generic
`keyring` package, whose macOS backend resolves against the CURRENT user's
login keychain — the wrong target for a root daemon with no session.

Security note: `set_api_key`'s subprocess command line contains the raw
secret as an argument. If that call fails, `subprocess.CalledProcessError`
embeds the full argument list (including the secret) in its `.cmd`
attribute — letting that exception propagate unhandled would put the
secret in a traceback, and from there potentially in a log. Both failure
paths below deliberately raise a new, redacted exception via `from None`,
which suppresses the original exception's context on top of never
including it in the new message.
"""

from __future__ import annotations

import subprocess
from typing import Optional

_SYSTEM_KEYCHAIN_PATH = "/Library/Keychains/System.keychain"
_SERVICE_NAME = "com.ecs-la.itmas.agent"
_ACCOUNT_NAME = "node-api-key"
_TIMEOUT_SECONDS = 10.0


class KeychainCredentialStore:
    def get_api_key(self) -> Optional[str]:
        try:
            result = subprocess.run(
                [
                    "security",
                    "find-generic-password",
                    "-a",
                    _ACCOUNT_NAME,
                    "-s",
                    _SERVICE_NAME,
                    "-w",
                    _SYSTEM_KEYCHAIN_PATH,
                ],
                capture_output=True,
                text=True,
                timeout=_TIMEOUT_SECONDS,
            )
        except (subprocess.SubprocessError, OSError):
            return None
        if result.returncode != 0:
            return None
        value = result.stdout.strip()
        return value or None

    def set_api_key(self, api_key: str) -> None:
        try:
            subprocess.run(
                [
                    "security",
                    "add-generic-password",
                    "-a",
                    _ACCOUNT_NAME,
                    "-s",
                    _SERVICE_NAME,
                    "-w",
                    api_key,
                    "-U",  # update in place if an entry already exists
                    _SYSTEM_KEYCHAIN_PATH,
                ],
                check=True,
                capture_output=True,
                text=True,
                timeout=_TIMEOUT_SECONDS,
            )
        except subprocess.CalledProcessError as exc:
            raise RuntimeError(
                f"failed to store API key in System keychain (security exit code {exc.returncode})"
            ) from None
        except (subprocess.SubprocessError, OSError) as exc:
            raise RuntimeError(
                f"failed to store API key in System keychain: {type(exc).__name__}"
            ) from None

    def clear_api_key(self) -> None:
        try:
            subprocess.run(
                [
                    "security",
                    "delete-generic-password",
                    "-a",
                    _ACCOUNT_NAME,
                    "-s",
                    _SERVICE_NAME,
                    _SYSTEM_KEYCHAIN_PATH,
                ],
                capture_output=True,
                timeout=_TIMEOUT_SECONDS,
            )
        except (subprocess.SubprocessError, OSError):
            pass  # idempotent — "nothing to clear" is not an error
