from __future__ import annotations

from datetime import datetime, timezone

import pytest

from itmas_agent.models import (
    ApplicationsSnapshot,
    DeviceIdentity,
    GpuInfo,
    HardwareSnapshot,
    InventorySnapshot,
    Measured,
    NetworkSnapshot,
    OSSnapshot,
    ResourceSnapshot,
    StorageSnapshot,
    StorageVolume,
    UsersSnapshot,
)


def build_valid_snapshot(**overrides) -> InventorySnapshot:
    """A fully-populated InventorySnapshot — every contract-required field
    present. Tests override individual fields to exercise failure paths.
    """
    snapshot = InventorySnapshot(
        schema_version="1.0",
        agent_platform="macos",
        agent_version="0.1.0",
        device=DeviceIdentity(
            device_id=Measured.ok("6a776b57-uuid"),
            hardware_serial=Measured.ok("TG4V0H9W0K"),
            hostname=Measured.ok("test-host.local"),
            category="collaborator",
        ),
        collected_at=datetime(2026, 8, 8, 9, 0, 3, tzinfo=timezone.utc),
        os=OSSnapshot(
            name=Measured.ok("macOS"),
            version=Measured.ok("15.1.0"),
            build=Measured.ok("24B83"),
            kernel_version=Measured.ok("Darwin 24.1.0"),
            architecture=Measured.ok("arm64"),
            hostname=Measured.ok("test-host.local"),
        ),
        hardware=HardwareSnapshot(
            manufacturer=Measured.ok("Apple"),
            model=Measured.ok("MacBook Pro"),
            model_identifier=Measured.ok("Mac16,7"),
            processor_model=Measured.ok("Apple M4 Pro"),
            architecture=Measured.ok("arm64"),
            cpu_cores_physical=Measured.ok(12),
            cpu_cores_logical=Measured.ok(12),
            ram_total_bytes=Measured.ok(25_769_803_776),
            gpu=GpuInfo(
                model=Measured.ok("Apple M4 Pro GPU"),
                cores=Measured.ok(16),
                memory_bytes=Measured.unavailable("unified memory"),
            ),
            storage_total_bytes=Measured.ok(549_755_813_888),
            storage_type=Measured.ok("SSD"),
            disk_count=Measured.ok(1),
        ),
        storage=StorageSnapshot(
            volumes=[
                StorageVolume(
                    filesystem="/dev/disk3s1",
                    mount_point="/",
                    total_bytes=494_384_795_648,
                    used_bytes=225_000_000_000,
                    available_bytes=269_384_795_648,
                    usage_percent=45.5,
                )
            ]
        ),
        applications=ApplicationsSnapshot(applications=[]),
        users=UsersSnapshot(users=[]),
        resources=ResourceSnapshot(
            cpu_usage_percent=Measured.ok(12.4),
            memory_usage_percent=Measured.ok(61.2),
            memory_total_bytes=Measured.ok(25_769_803_776),
            memory_used_bytes=Measured.ok(15_765_000_000),
            uptime_seconds=Measured.ok(348_213.0),
        ),
        network=NetworkSnapshot(
            local_ip=Measured.ok("192.168.1.42"),
            public_ip=Measured.ok("201.245.13.87"),
        ),
    )
    for key, value in overrides.items():
        setattr(snapshot, key, value)
    return snapshot


@pytest.fixture
def valid_snapshot() -> InventorySnapshot:
    return build_valid_snapshot()
