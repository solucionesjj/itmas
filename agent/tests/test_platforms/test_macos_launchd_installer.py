from __future__ import annotations

import plistlib

from itmas_agent.platforms.macos import launchd_service_installer as installer_module
from itmas_agent.platforms.macos.launchd_service_installer import LaunchdServiceInstaller


def _redirect_paths(mocker, tmp_path):
    """Never touch the real /Library paths during tests — redirect the
    module's path constants to tmp_path, same idea as pointing a database
    client at a throwaway test database.
    """
    plist_path = tmp_path / "LaunchDaemons" / "com.ecs-la.itmas.agent.plist"
    log_dir = tmp_path / "Logs"
    mocker.patch.object(installer_module, "_PLIST_PATH", plist_path)
    mocker.patch.object(installer_module, "_LOG_DIR", log_dir)
    return plist_path, log_dir


def test_install_writes_a_valid_plist_with_calendar_interval_and_run_at_load(mocker, tmp_path):
    plist_path, log_dir = _redirect_paths(mocker, tmp_path)
    run_mock = mocker.patch.object(installer_module.subprocess, "run")

    LaunchdServiceInstaller().install(
        executable_path=tmp_path / "bin" / "itmas-agent", config_path=tmp_path / "config.json"
    )

    assert plist_path.exists()
    with plist_path.open("rb") as f:
        plist = plistlib.load(f)
    assert plist["Label"] == "com.ecs-la.itmas.agent"
    assert plist["StartCalendarInterval"] == {"Hour": 9, "Minute": 0}
    assert plist["RunAtLoad"] is True
    assert "KeepAlive" not in plist  # ephemeral-per-invocation, not a resident daemon
    assert plist["ProgramArguments"][-1] == "run"
    assert log_dir.exists()

    run_mock.assert_called_once()
    assert run_mock.call_args[0][0][:2] == ["launchctl", "bootstrap"]


def test_uninstall_bootouts_and_removes_the_plist(mocker, tmp_path):
    plist_path, _ = _redirect_paths(mocker, tmp_path)
    plist_path.parent.mkdir(parents=True)
    plist_path.write_text("placeholder")
    run_mock = mocker.patch.object(installer_module.subprocess, "run")

    LaunchdServiceInstaller().uninstall()

    assert not plist_path.exists()
    run_mock.assert_called_once()
    assert run_mock.call_args[0][0][:2] == ["launchctl", "bootout"]


def test_run_now_uses_kickstart(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    run_mock = mocker.patch.object(installer_module.subprocess, "run")

    LaunchdServiceInstaller().run_now()

    args = run_mock.call_args[0][0]
    assert args[:3] == ["launchctl", "kickstart", "-k"]


def test_status_reports_loaded_on_zero_exit_code(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    mocker.patch.object(
        installer_module.subprocess, "run", return_value=mocker.Mock(returncode=0)
    )

    assert LaunchdServiceInstaller().status() == "loaded"


def test_status_reports_not_loaded_on_nonzero_exit_code(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    mocker.patch.object(
        installer_module.subprocess, "run", return_value=mocker.Mock(returncode=3)
    )

    assert LaunchdServiceInstaller().status() == "not loaded"
