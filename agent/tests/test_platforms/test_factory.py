from __future__ import annotations

import pytest

from itmas_agent.platforms import factory


def test_darwin_resolves_to_macos_backend(mocker):
    mocker.patch.object(factory._platform, "system", return_value="Darwin")

    backend = factory.get_platform_backend()

    assert backend.platform_name == "macos"


def test_windows_resolves_to_windows_backend(mocker):
    mocker.patch.object(factory._platform, "system", return_value="Windows")

    backend = factory.get_platform_backend()

    assert backend.platform_name == "windows"


def test_linux_resolves_to_linux_backend(mocker):
    mocker.patch.object(factory._platform, "system", return_value="Linux")

    backend = factory.get_platform_backend()

    assert backend.platform_name == "linux"


def test_unsupported_platform_raises(mocker):
    mocker.patch.object(factory._platform, "system", return_value="PlayStation")

    with pytest.raises(RuntimeError, match="Unsupported platform"):
        factory.get_platform_backend()
