"""Selects the right PlatformBackend for the machine this process runs on."""

from __future__ import annotations

import platform as _platform

from itmas_agent.platforms.base import PlatformBackend


def get_platform_backend() -> PlatformBackend:
    system = _platform.system()
    if system == "Darwin":
        from itmas_agent.platforms.macos.backend import MacOSBackend

        return MacOSBackend()
    if system == "Windows":
        from itmas_agent.platforms.windows.backend import WindowsBackend

        return WindowsBackend()
    if system == "Linux":
        from itmas_agent.platforms.linux.backend import LinuxBackend

        return LinuxBackend()
    raise RuntimeError(f"Unsupported platform: {system!r}")
