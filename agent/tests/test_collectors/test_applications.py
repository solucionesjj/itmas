from __future__ import annotations

from itmas_agent.collectors.applications import ApplicationsCollector
from itmas_agent.platforms.base import RawAppInfo
from tests.fakes.fake_platform_backend import FakePlatformBackend


def test_maps_every_field_when_available():
    backend = FakePlatformBackend(
        applications=[
            RawAppInfo(
                name="Visual Studio Code",
                version="1.95.3",
                bundle_id="com.microsoft.VSCode",
                path="/Applications/Visual Studio Code.app",
            )
        ]
    )

    snapshot = ApplicationsCollector(backend).collect()

    app = snapshot.applications[0]
    assert app.name == "Visual Studio Code"
    assert app.version.value == "1.95.3"
    assert app.bundle_id.value == "com.microsoft.VSCode"
    assert app.path == "/Applications/Visual Studio Code.app"


def test_missing_version_degrades_independently():
    backend = FakePlatformBackend(
        applications=[
            RawAppInfo(name="Mystery", version=None, bundle_id=None, path="/Applications/Mystery.app")
        ]
    )

    snapshot = ApplicationsCollector(backend).collect()

    app = snapshot.applications[0]
    assert app.name == "Mystery"
    assert app.version.value is None
    assert app.version.reason is not None
