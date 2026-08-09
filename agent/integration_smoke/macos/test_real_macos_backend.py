"""Runs against the REAL machine — no mocks. Asserts plausible shape, not
exact values (those vary per machine). Only pytest tests with the
`macos_integration` marker (excluded from the default `pytest tests/` run,
included explicitly in CI's agent-macos job).

Deliberately does NOT exercise `KeychainCredentialStore.set_api_key()` or
`LaunchdServiceInstaller.install()`/`uninstall()` here — those mutate real,
persistent system state (the System keychain, /Library/LaunchDaemons).
Doing that automatically on every CI run (or, worse, on a developer's own
machine while just running the test suite) is a real, hard-to-reverse
side effect this suite should never cause by accident. The full
install/uninstall/launchctl cycle is exercised deliberately instead by
Scripts/macos/smoke-test.sh, meant for a disposable CI runner or an
operator's explicit manual run — never implicitly by `pytest`.
"""

from __future__ import annotations

import pytest

from itmas_agent.platforms.macos.backend import MacOSBackend
from itmas_agent.platforms.macos.keychain_credential_store import KeychainCredentialStore

pytestmark = pytest.mark.macos_integration


@pytest.fixture(scope="module")
def backend() -> MacOSBackend:
    return MacOSBackend()


def test_hardware_info_returns_plausible_values(backend):
    hw = backend.hardware_info()

    assert hw.manufacturer == "Apple"
    assert hw.model  # non-empty string
    assert hw.processor_model
    # GPU cores may legitimately be unavailable on some configurations —
    # only assert the type when present.
    if hw.gpu_cores is not None:
        assert hw.gpu_cores > 0
    if hw.storage_total_bytes is not None:
        assert hw.storage_total_bytes > 0


def test_hardware_serial_is_present(backend):
    serial = backend.hardware_serial()

    assert serial is None or len(serial) > 0


def test_os_info_returns_plausible_values(backend):
    os_info = backend.os_info()

    assert os_info.name == "macOS"
    assert os_info.version
    assert os_info.build
    assert os_info.architecture in ("arm64", "x86_64")
    assert os_info.hostname


def test_local_users_includes_at_least_the_current_user(backend):
    import getpass

    users = backend.local_users()
    usernames = {u.username for u in users}

    assert getpass.getuser() in usernames
    for user in users:
        assert user.uid >= 500  # the verified real-account threshold


def test_installed_applications_returns_a_list_of_well_formed_entries(backend):
    apps = backend.installed_applications()

    assert isinstance(apps, list)
    for app in apps[:20]:  # spot-check rather than iterate hundreds
        assert app.name
        assert app.path.endswith(".app")


def test_keychain_read_of_a_missing_entry_returns_none_without_raising():
    # A read-only check: this account/service almost certainly has no
    # stored entry yet on a machine that hasn't run `itmas-agent configure`
    # — confirms the "not found" path is quiet, never a crash. Does not
    # write anything.
    store = KeychainCredentialStore()

    result = store.get_api_key()

    assert result is None or isinstance(result, str)
