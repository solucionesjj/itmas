"""Confirms the Windows/Linux stub backends fail loudly (NotImplementedError)
rather than returning fabricated or partially-correct data — the whole
point of leaving them as documented stubs is that "not built yet" must
never be mistaken for a real (even if empty) answer.
"""

from __future__ import annotations

import pytest

from itmas_agent.platforms.linux.backend import LinuxBackend
from itmas_agent.platforms.windows.backend import WindowsBackend

_METHODS = [
    "hardware_info",
    "hardware_serial",
    "os_info",
    "installed_applications",
    "local_users",
    "credential_store",
    "service_installer",
]


@pytest.mark.parametrize("method_name", _METHODS)
def test_windows_backend_every_method_raises_not_implemented(method_name):
    backend = WindowsBackend()

    with pytest.raises(NotImplementedError):
        getattr(backend, method_name)()


@pytest.mark.parametrize("method_name", _METHODS)
def test_linux_backend_every_method_raises_not_implemented(method_name):
    backend = LinuxBackend()

    with pytest.raises(NotImplementedError):
        getattr(backend, method_name)()


def test_windows_backend_declares_its_platform_name():
    assert WindowsBackend().platform_name == "windows"


def test_linux_backend_declares_its_platform_name():
    assert LinuxBackend().platform_name == "linux"
