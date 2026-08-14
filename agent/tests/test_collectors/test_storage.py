from __future__ import annotations

from collections import namedtuple

from itmas_agent.collectors.storage import StorageCollector

_Partition = namedtuple("_Partition", ["device", "mountpoint", "fstype", "opts"])
_Usage = namedtuple("_Usage", ["total", "used", "free", "percent"])


def test_excludes_hidden_system_volumes_and_pseudo_filesystems(mocker):
    partitions = [
        _Partition("/dev/disk3s3s1", "/", "apfs", "ro,local,rootfs,journaled"),
        _Partition("devfs", "/dev", "devfs", "rw,local,dontbrowse"),
        _Partition("/dev/disk3s6", "/System/Volumes/VM", "apfs", "rw,local,dontbrowse"),
        _Partition("map auto_home", "/System/Volumes/Data/home", "autofs", "rw,dontbrowse"),
        _Partition("/dev/disk4s1", "/Volumes/External", "hfs", "ro,local"),
    ]
    mocker.patch("itmas_agent.collectors.storage.psutil.disk_partitions", return_value=partitions)
    mocker.patch(
        "itmas_agent.collectors.storage.psutil.disk_usage",
        return_value=_Usage(total=1000, used=400, free=600, percent=40.0),
    )

    snapshot = StorageCollector().collect()

    mount_points = {v.mount_point for v in snapshot.volumes}
    assert mount_points == {"/", "/Volumes/External"}


def test_skips_a_volume_whose_usage_lookup_fails_without_aborting_the_rest(mocker):
    partitions = [
        _Partition("/dev/disk1", "/", "apfs", "local"),
        _Partition("/dev/disk2", "/Volumes/Unreachable", "hfs", "local"),
    ]
    mocker.patch("itmas_agent.collectors.storage.psutil.disk_partitions", return_value=partitions)

    def fake_disk_usage(mountpoint):
        if mountpoint == "/Volumes/Unreachable":
            raise OSError("stale mount")
        return _Usage(total=1000, used=400, free=600, percent=40.0)

    mocker.patch(
        "itmas_agent.collectors.storage.psutil.disk_usage", side_effect=fake_disk_usage
    )

    snapshot = StorageCollector().collect()

    assert [v.mount_point for v in snapshot.volumes] == ["/"]
