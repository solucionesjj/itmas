"""CLI entry point — `itmas-agent run|configure|status`. 100% OS-agnostic:
every platform-specific decision is delegated to whatever `PlatformBackend`
`get_platform_backend()` resolves.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Optional, Sequence

from itmas_agent import scheduling
from itmas_agent.composition import AgentRunner
from itmas_agent.config import ConfigurationError, load_configuration
from itmas_agent.logging_setup import configure_logging
from itmas_agent.persistence.json_store import write_json_atomic
from itmas_agent.persistence.retry_queue_store import RetryQueueStore
from itmas_agent.persistence.run_state_store import RunStateStore
from itmas_agent.platforms.factory import get_platform_backend

# macOS-only paths for now — a per-OS path helper is needed once
# Windows/Linux backends are implemented (see agent/docs/ARCHITECTURE.md).
_DATA_DIR = Path("/Library/Application Support/ITMasAgent")
_LOG_PATH = Path("/Library/Logs/ITMasAgent/agent.log")
_CONFIG_PATH = _DATA_DIR / "config.json"
_RUN_STATE_PATH = _DATA_DIR / "run_state.json"
_RETRY_QUEUE_PATH = _DATA_DIR / "retry_queue.json"
_EXTENDED_CACHE_PATH = _DATA_DIR / "latest_inventory.json"


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)

    if args.command == "run":
        return _cmd_run(args)
    if args.command == "configure":
        return _cmd_configure(args)
    if args.command == "status":
        return _cmd_status(args)
    if args.command == "service":
        return _cmd_service(args)

    parser.print_help()
    return 1


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="itmas-agent")
    subparsers = parser.add_subparsers(dest="command")

    run_parser = subparsers.add_parser("run", help="Run a collection cycle if due (or forced)")
    run_parser.add_argument(
        "--now", action="store_true", help="Run immediately, bypassing the daily schedule check"
    )
    run_parser.add_argument(
        "--dry-run", action="store_true", help="Collect and cache locally, but send nothing"
    )

    configure_parser = subparsers.add_parser(
        "configure", help="Store the node API key and base configuration"
    )
    configure_parser.add_argument(
        "--api-key",
        required=True,
        help="The <deviceId>.<secret> printed once by 'npm run device:provision'",
    )
    configure_parser.add_argument("--api-base-url", required=True)
    configure_parser.add_argument(
        "--category", required=True, choices=["collaborator", "infrastructure"]
    )
    configure_parser.add_argument("--scheduled-hour", type=int, default=9)
    configure_parser.add_argument("--scheduled-minute", type=int, default=0)

    subparsers.add_parser("status", help="Show configuration and last-run status")

    service_parser = subparsers.add_parser(
        "service", help="Manage the OS-native scheduled service (LaunchDaemon on macOS)"
    )
    service_parser.add_argument("service_action", choices=["install", "uninstall", "status"])

    return parser


def _cmd_run(args: argparse.Namespace) -> int:
    logger, redaction_filter = configure_logging(_LOG_PATH)
    try:
        config = load_configuration(_CONFIG_PATH)
    except ConfigurationError as exc:
        logger.error(f"{exc} — run 'itmas-agent configure' first")
        return 1

    backend = get_platform_backend()
    redaction_filter.add_secret(backend.credential_store().get_api_key())

    runner = AgentRunner(
        backend=backend,
        config=config,
        run_state=RunStateStore(_RUN_STATE_PATH),
        retry_queue=RetryQueueStore(_RETRY_QUEUE_PATH),
        extended_cache_path=_EXTENDED_CACHE_PATH,
        clock=scheduling.SystemClock(),
        logger=logger,
    )
    report = runner.run_due_work(force=args.now, dry_run=args.dry_run)
    logger.info(
        f"run complete: ran={report.ran} reason={report.reason!r} sent_to_api={report.sent_to_api}"
    )
    return 0


def _cmd_configure(args: argparse.Namespace) -> int:
    backend = get_platform_backend()
    backend.credential_store().set_api_key(args.api_key)

    write_json_atomic(
        _CONFIG_PATH,
        {
            "apiBaseUrl": args.api_base_url,
            "category": args.category,
            "scheduledHour": args.scheduled_hour,
            "scheduledMinute": args.scheduled_minute,
        },
    )
    print(f"Configuration written to {_CONFIG_PATH}. API key stored in the System keychain.")
    return 0


def _cmd_status(_args: argparse.Namespace) -> int:
    backend = get_platform_backend()
    has_key = backend.credential_store().get_api_key() is not None

    try:
        config = load_configuration(_CONFIG_PATH)
        print(f"Configured: apiBaseUrl={config.api_base_url} category={config.category}")
    except ConfigurationError:
        print("Not configured — run 'itmas-agent configure' first")

    print(f"API key present in Keychain: {has_key}")

    last_run = RunStateStore(_RUN_STATE_PATH).last_success(scheduling.DAILY_RUN_KEY)
    print(f"Last successful run: {last_run.isoformat() if last_run else 'never'}")

    pending = RetryQueueStore(_RETRY_QUEUE_PATH).pending()
    print(f"Pending retry queue items: {len(pending)}")
    return 0


def _cmd_service(args: argparse.Namespace) -> int:
    backend = get_platform_backend()
    installer = backend.service_installer()

    if args.service_action == "install":
        # sys.argv[0] is this binary's own invoked path — correct for a
        # PyInstaller-frozen executable (its bootloader sets this to the
        # real installed location, not a temp/build path). install.sh
        # copies the binary to its permanent home BEFORE calling
        # `service install`, so this captures the right path to embed into
        # the LaunchDaemon plist's ProgramArguments.
        executable_path = Path(sys.argv[0]).resolve()
        installer.install(executable_path=executable_path, config_path=_CONFIG_PATH)
        print(f"Service installed, running {executable_path} on schedule.")
    elif args.service_action == "uninstall":
        installer.uninstall()
        backend.credential_store().clear_api_key()
        print("Service uninstalled and node API key removed from the credential store.")
    else:
        print(installer.status())
    return 0
