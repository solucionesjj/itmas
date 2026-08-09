from __future__ import annotations

from collections import namedtuple
from datetime import datetime, timezone

from itmas_agent.collectors.users import UsersCollector
from itmas_agent.platforms.base import RawUserInfo
from tests.fakes.fake_platform_backend import FakePlatformBackend

_Session = namedtuple("_Session", ["name", "terminal", "host", "started", "pid"])


def test_maps_admin_and_standard_accounts(mocker):
    backend = FakePlatformBackend(
        users=[
            RawUserInfo(
                username="jmartinez",
                uid=501,
                full_name="John Martinez",
                home_directory="/Users/jmartinez",
                shell="/bin/zsh",
                is_admin=True,
                groups=["staff", "admin", "everyone"],
            ),
            RawUserInfo(
                username="guest",
                uid=503,
                full_name="Guest User",
                home_directory="/Users/guest",
                shell="/bin/zsh",
                is_admin=False,
                groups=["staff", "everyone"],
            ),
        ]
    )
    mocker.patch("itmas_agent.collectors.users.psutil.users", return_value=[])

    snapshot = UsersCollector(backend).collect()

    admin, standard = snapshot.users
    assert admin.account_type.value == "admin"
    assert standard.account_type.value == "standard"
    assert admin.groups == ["staff", "admin", "everyone"]


def test_last_login_from_currently_active_session(mocker):
    backend = FakePlatformBackend(
        users=[
            RawUserInfo(
                username="jmartinez",
                uid=501,
                full_name="John Martinez",
                home_directory="/Users/jmartinez",
                shell="/bin/zsh",
                is_admin=True,
                groups=["admin"],
            )
        ]
    )
    started_epoch = datetime(2026, 8, 8, 8, 55, 10, tzinfo=timezone.utc).timestamp()
    mocker.patch(
        "itmas_agent.collectors.users.psutil.users",
        return_value=[
            _Session(name="jmartinez", terminal="console", host="localhost", started=started_epoch, pid=1),
        ],
    )

    snapshot = UsersCollector(backend).collect()

    assert snapshot.users[0].last_login.value == datetime(
        2026, 8, 8, 8, 55, 10, tzinfo=timezone.utc
    )


def test_takes_the_most_recent_session_when_a_user_has_several(mocker):
    backend = FakePlatformBackend(
        users=[
            RawUserInfo(
                username="jmartinez",
                uid=501,
                full_name="John Martinez",
                home_directory="/Users/jmartinez",
                shell="/bin/zsh",
                is_admin=True,
                groups=["admin"],
            )
        ]
    )
    older = datetime(2026, 8, 1, 8, 0, 0, tzinfo=timezone.utc).timestamp()
    newer = datetime(2026, 8, 8, 8, 55, 10, tzinfo=timezone.utc).timestamp()
    mocker.patch(
        "itmas_agent.collectors.users.psutil.users",
        return_value=[
            _Session(name="jmartinez", terminal="ttys000", host="localhost", started=older, pid=1),
            _Session(name="jmartinez", terminal="console", host="localhost", started=newer, pid=2),
        ],
    )

    snapshot = UsersCollector(backend).collect()

    assert snapshot.users[0].last_login.value == datetime(
        2026, 8, 8, 8, 55, 10, tzinfo=timezone.utc
    )


def test_no_active_session_is_unavailable_never_inferred(mocker):
    backend = FakePlatformBackend(
        users=[
            RawUserInfo(
                username="jmartinez.personal",
                uid=502,
                full_name="Personal Account",
                home_directory="/Users/jmartinez.personal",
                shell="/bin/zsh",
                is_admin=True,
                groups=["admin"],
            )
        ]
    )
    mocker.patch("itmas_agent.collectors.users.psutil.users", return_value=[])

    snapshot = UsersCollector(backend).collect()

    assert snapshot.users[0].last_login.value is None
    assert snapshot.users[0].last_login.reason is not None


def test_missing_backend_fields_degrade_independently(mocker):
    backend = FakePlatformBackend(
        users=[
            RawUserInfo(
                username="jmartinez",
                uid=501,
                full_name=None,
                home_directory=None,
                shell=None,
                is_admin=None,
                groups=[],
            )
        ]
    )
    mocker.patch("itmas_agent.collectors.users.psutil.users", return_value=[])

    snapshot = UsersCollector(backend).collect()

    user = snapshot.users[0]
    assert user.username == "jmartinez"  # always present, never wrapped
    assert user.full_name.value is None
    assert user.account_type.value is None
    assert user.account_type.reason is not None
