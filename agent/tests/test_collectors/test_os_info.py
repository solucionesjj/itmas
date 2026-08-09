from __future__ import annotations

from itmas_agent.collectors.os_info import OSInfoCollector
from itmas_agent.platforms.base import OSRawInfo
from tests.fakes.fake_platform_backend import FakePlatformBackend


def test_maps_every_field_when_available():
    backend = FakePlatformBackend(
        os=OSRawInfo(
            name="macOS",
            version="15.1.0",
            build="24B83",
            kernel_version="Darwin 24.1.0",
            architecture="arm64",
            hostname="test-host.local",
        )
    )

    snapshot = OSInfoCollector(backend).collect()

    assert snapshot.name.value == "macOS"
    assert snapshot.version.value == "15.1.0"
    assert snapshot.build.value == "24B83"
    assert snapshot.kernel_version.value == "Darwin 24.1.0"
    assert snapshot.architecture.value == "arm64"
    assert snapshot.hostname.value == "test-host.local"


def test_missing_build_becomes_unavailable_not_an_error():
    backend = FakePlatformBackend(
        os=OSRawInfo(
            name="macOS",
            version="15.1.0",
            build=None,
            kernel_version="Darwin 24.1.0",
            architecture="arm64",
            hostname="test-host.local",
        )
    )

    snapshot = OSInfoCollector(backend).collect()

    assert snapshot.build.value is None
    assert snapshot.build.reason is not None
    # Everything else still collected — one missing field never aborts the rest.
    assert snapshot.name.value == "macOS"
