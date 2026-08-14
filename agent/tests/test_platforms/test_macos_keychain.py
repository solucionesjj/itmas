from __future__ import annotations

import subprocess

import pytest

from itmas_agent.platforms.macos.keychain_credential_store import KeychainCredentialStore

_SECRET = "6a776b57-uuid.super-secret-value-must-never-leak"


def test_get_api_key_returns_stripped_stdout(mocker):
    mocker.patch(
        "itmas_agent.platforms.macos.keychain_credential_store.subprocess.run",
        return_value=mocker.Mock(returncode=0, stdout=f"{_SECRET}\n"),
    )

    assert KeychainCredentialStore().get_api_key() == _SECRET


def test_get_api_key_returns_none_when_entry_not_found(mocker):
    mocker.patch(
        "itmas_agent.platforms.macos.keychain_credential_store.subprocess.run",
        return_value=mocker.Mock(returncode=44, stdout=""),
    )

    assert KeychainCredentialStore().get_api_key() is None


def test_set_api_key_failure_never_leaks_the_secret_in_the_raised_exception(mocker):
    """The whole point of this test: security.CalledProcessError.cmd would
    contain the raw secret (it's a subprocess argument) — set_api_key must
    never let that propagate. Confirmed both in the exception message AND
    via str()/repr() of the exception (which would reach a log if this were
    ever accidentally logged upstream).
    """
    failing_call = subprocess.CalledProcessError(
        returncode=1,
        cmd=["security", "add-generic-password", "-w", _SECRET, "-U", "System.keychain"],
        output="",
        stderr="SecKeychainItemCreateFromContent: some error",
    )
    mocker.patch(
        "itmas_agent.platforms.macos.keychain_credential_store.subprocess.run",
        side_effect=failing_call,
    )

    with pytest.raises(RuntimeError) as excinfo:
        KeychainCredentialStore().set_api_key(_SECRET)

    assert _SECRET not in str(excinfo.value)
    assert _SECRET not in repr(excinfo.value)
    # The original CalledProcessError (whose .cmd holds the secret) must not
    # be chained as __cause__/__context__, or a full traceback print would
    # still expose it.
    assert excinfo.value.__cause__ is None
    assert excinfo.value.__suppress_context__ is True


def test_set_api_key_succeeds_silently_on_success(mocker):
    mock_run = mocker.patch(
        "itmas_agent.platforms.macos.keychain_credential_store.subprocess.run",
        return_value=mocker.Mock(returncode=0),
    )

    KeychainCredentialStore().set_api_key(_SECRET)

    assert mock_run.called


def test_clear_api_key_does_not_raise_when_entry_is_missing(mocker):
    mocker.patch(
        "itmas_agent.platforms.macos.keychain_credential_store.subprocess.run",
        side_effect=subprocess.CalledProcessError(returncode=44, cmd=["security"]),
    )

    KeychainCredentialStore().clear_api_key()  # must not raise
