from __future__ import annotations

import plistlib

from itmas_agent.platforms.macos.backend import MacOSBackend


def _make_app_bundle(root, name: str, info: dict | None) -> None:
    bundle = root / name
    contents = bundle / "Contents"
    contents.mkdir(parents=True)
    if info is not None:
        with (contents / "Info.plist").open("wb") as f:
            plistlib.dump(info, f)


def test_prefers_display_name_over_bundle_name(tmp_path):
    _make_app_bundle(
        tmp_path,
        "Google Chrome.app",
        {
            "CFBundleName": "Chrome",
            "CFBundleDisplayName": "Google Chrome",
            "CFBundleShortVersionString": "151.0.7922.76",
            "CFBundleIdentifier": "com.google.Chrome",
        },
    )

    apps = MacOSBackend._scan_app_bundles(tmp_path)

    assert len(apps) == 1
    assert apps[0].name == "Google Chrome"
    assert apps[0].version == "151.0.7922.76"
    assert apps[0].bundle_id == "com.google.Chrome"


def test_falls_back_to_bundle_name_when_no_display_name(tmp_path):
    _make_app_bundle(
        tmp_path,
        "Docker.app",
        {"CFBundleName": "Docker", "CFBundleShortVersionString": "4.72.0"},
    )

    apps = MacOSBackend._scan_app_bundles(tmp_path)

    assert apps[0].name == "Docker"
    assert apps[0].bundle_id is None


def test_falls_back_to_folder_name_when_info_plist_missing(tmp_path):
    _make_app_bundle(tmp_path, "Mystery.app", info=None)

    apps = MacOSBackend._scan_app_bundles(tmp_path)

    assert apps[0].name == "Mystery"
    assert apps[0].version is None


def test_ignores_non_app_entries(tmp_path):
    (tmp_path / "not-an-app.txt").write_text("hello")
    _make_app_bundle(tmp_path, "Real.app", {"CFBundleName": "Real"})

    apps = MacOSBackend._scan_app_bundles(tmp_path)

    assert [a.name for a in apps] == ["Real"]


def test_missing_root_directory_returns_empty_without_raising(tmp_path):
    apps = MacOSBackend._scan_app_bundles(tmp_path / "does-not-exist")

    assert apps == []


def test_installed_applications_dedupes_across_scan_roots(mocker):
    backend = MacOSBackend()
    duplicate_path = "/Applications/Same.app"
    mocker.patch.object(
        MacOSBackend,
        "_application_scan_roots",
        return_value=["/root-a", "/root-b"],
    )
    from itmas_agent.platforms.base import RawAppInfo

    mocker.patch.object(
        MacOSBackend,
        "_scan_app_bundles",
        side_effect=[
            [RawAppInfo(name="Same", version="1.0", bundle_id="x", path=duplicate_path)],
            [RawAppInfo(name="Same", version="1.0", bundle_id="x", path=duplicate_path)],
        ],
    )

    apps = backend.installed_applications()

    assert len(apps) == 1
