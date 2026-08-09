from __future__ import annotations

from itmas_agent.platforms.macos import directory_service

_LIST_OUTPUT = """\
_spotlight               89
daemon                   1
root                     0
nobody                   -2
jmartinez                501
jmartinez.personal       502
"""


def test_list_real_user_uids_filters_by_uid_threshold(mocker):
    fake_result = mocker.Mock(stdout=_LIST_OUTPUT)
    mocker.patch(
        "itmas_agent.platforms.macos.directory_service.subprocess.run",
        return_value=fake_result,
    )

    accounts = directory_service.list_real_user_uids()

    assert accounts == {"jmartinez": 501, "jmartinez.personal": 502}


def test_list_real_user_uids_returns_empty_on_subprocess_failure(mocker):
    mocker.patch(
        "itmas_agent.platforms.macos.directory_service.subprocess.run",
        side_effect=OSError("dscl not found"),
    )

    assert directory_service.list_real_user_uids() == {}
