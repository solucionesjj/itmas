from __future__ import annotations

from pathlib import Path

from itmas_agent import cli
from itmas_agent.composition import RunReport
from itmas_agent.config import AgentConfiguration, load_configuration
from tests.fakes.fake_platform_backend import FakePlatformBackend


def _redirect_paths(mocker, tmp_path):
    mocker.patch.object(cli, "_DATA_DIR", tmp_path)
    mocker.patch.object(cli, "_LOG_PATH", tmp_path / "agent.log")
    mocker.patch.object(cli, "_CONFIG_PATH", tmp_path / "config.json")
    mocker.patch.object(cli, "_RUN_STATE_PATH", tmp_path / "run_state.json")
    mocker.patch.object(cli, "_RETRY_QUEUE_PATH", tmp_path / "retry_queue.json")
    mocker.patch.object(cli, "_EXTENDED_CACHE_PATH", tmp_path / "latest_inventory.json")


def test_configure_writes_config_and_stores_api_key(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    backend = FakePlatformBackend()
    mocker.patch.object(cli, "get_platform_backend", return_value=backend)

    exit_code = cli.main(
        [
            "configure",
            "--api-key",
            "deviceid.secret",
            "--api-base-url",
            "https://api.example.com",
            "--category",
            "collaborator",
        ]
    )

    assert exit_code == 0
    assert backend.credential_store().get_api_key() == "deviceid.secret"
    written_config = load_configuration(tmp_path / "config.json")
    assert written_config.api_base_url == "https://api.example.com"
    assert written_config.category == "collaborator"


def test_status_reports_not_configured_when_no_config_written(mocker, tmp_path, capsys):
    _redirect_paths(mocker, tmp_path)
    backend = FakePlatformBackend()
    mocker.patch.object(cli, "get_platform_backend", return_value=backend)

    exit_code = cli.main(["status"])

    assert exit_code == 0
    out = capsys.readouterr().out
    assert "Not configured" in out
    assert "API key present in Keychain: False" in out


def test_run_forwards_now_and_dry_run_flags_to_agent_runner(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    backend = FakePlatformBackend()
    mocker.patch.object(cli, "get_platform_backend", return_value=backend)
    mocker.patch.object(
        cli,
        "load_configuration",
        return_value=AgentConfiguration(
            api_base_url="https://api.example.com", category="collaborator"
        ),
    )
    fake_runner = mocker.Mock()
    fake_runner.run_due_work.return_value = RunReport(ran=True, reason="ok", sent_to_api=True)
    runner_class = mocker.patch.object(cli, "AgentRunner", return_value=fake_runner)

    exit_code = cli.main(["run", "--now", "--dry-run"])

    assert exit_code == 0
    fake_runner.run_due_work.assert_called_once_with(force=True, dry_run=True)
    assert runner_class.call_args.kwargs["backend"] is backend


def test_run_without_configuration_fails_gracefully(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    backend = FakePlatformBackend()
    mocker.patch.object(cli, "get_platform_backend", return_value=backend)
    # No config.json written and load_configuration() left as the real
    # implementation — it will raise ConfigurationError for the missing file.

    exit_code = cli.main(["run"])

    assert exit_code == 1


def test_service_install_passes_this_binarys_own_path_to_the_installer(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    backend = FakePlatformBackend()
    fake_installer = mocker.Mock()
    backend._service_installer = fake_installer
    mocker.patch.object(cli, "get_platform_backend", return_value=backend)
    mocker.patch.object(cli.sys, "argv", ["/usr/local/itmas-agent/bin/itmas-agent", "service", "install"])

    exit_code = cli.main(["service", "install"])

    assert exit_code == 0
    fake_installer.install.assert_called_once()
    assert fake_installer.install.call_args.kwargs["executable_path"] == Path(
        "/usr/local/itmas-agent/bin/itmas-agent"
    )


def test_service_uninstall_calls_the_installer_and_clears_the_credential(mocker, tmp_path):
    _redirect_paths(mocker, tmp_path)
    backend = FakePlatformBackend()
    backend.credential_store().set_api_key("deviceid.secret")
    fake_installer = mocker.Mock()
    backend._service_installer = fake_installer
    mocker.patch.object(cli, "get_platform_backend", return_value=backend)

    exit_code = cli.main(["service", "uninstall"])

    assert exit_code == 0
    fake_installer.uninstall.assert_called_once()
    assert backend.credential_store().get_api_key() is None


def test_service_status_prints_installer_status(mocker, tmp_path, capsys):
    _redirect_paths(mocker, tmp_path)
    backend = FakePlatformBackend()
    fake_installer = mocker.Mock()
    fake_installer.status.return_value = "loaded"
    backend._service_installer = fake_installer
    mocker.patch.object(cli, "get_platform_backend", return_value=backend)

    exit_code = cli.main(["service", "status"])

    assert exit_code == 0
    assert "loaded" in capsys.readouterr().out
