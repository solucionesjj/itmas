from __future__ import annotations

import pytest

from itmas_agent.models import Measured
from itmas_agent.normalization.inventory_mapper import MappingError, map_to_inventory_request


def test_happy_path_maps_every_field_exactly(valid_snapshot):
    request = map_to_inventory_request(valid_snapshot)

    assert request.hostname == "test-host.local"
    assert request.category == "collaborator"
    assert request.os.name == "macOS"
    assert request.os.version == "15.1.0"
    assert request.cpu.model == "Apple M4 Pro"
    assert request.cpu.cores == 12
    # 25_769_803_776 bytes / 1024**3 == 24.0 GB exactly
    assert request.ram.totalGB == 24.0
    assert request.timestamp == "2026-08-08T09:00:03Z"


def test_disks_come_from_storage_volumes_when_present(valid_snapshot):
    request = map_to_inventory_request(valid_snapshot)

    assert len(request.disks) == 1
    assert request.disks[0].name == "/"
    # 494_384_795_648 / 1024**3 rounded to 2 decimals
    assert request.disks[0].sizeGB == pytest.approx(460.43, abs=0.01)


def test_disks_fall_back_to_hardware_summary_when_no_volumes(valid_snapshot):
    valid_snapshot.storage.volumes = []

    request = map_to_inventory_request(valid_snapshot)

    assert len(request.disks) == 1
    assert request.disks[0].name == "TotalStorage"
    assert request.disks[0].sizeGB == pytest.approx(512.0, abs=0.01)


@pytest.mark.parametrize(
    "mutate,missing_field",
    [
        (lambda s: setattr(s.device, "hostname", Measured.unavailable("x")), "hostname"),
        (lambda s: setattr(s.os, "name", Measured.unavailable("x")), "os.name"),
        (lambda s: setattr(s.os, "version", Measured.unavailable("x")), "os.version"),
        (lambda s: setattr(s.hardware, "processor_model", Measured.unavailable("x")), "cpu.model"),
        (
            lambda s: setattr(s.hardware, "cpu_cores_physical", Measured.unavailable("x")),
            "cpu.cores",
        ),
        (lambda s: setattr(s.hardware, "ram_total_bytes", Measured.unavailable("x")), "ram.totalGB"),
    ],
)
def test_missing_required_field_raises_mapping_error_naming_the_field(
    valid_snapshot, mutate, missing_field
):
    mutate(valid_snapshot)

    with pytest.raises(MappingError, match=missing_field):
        map_to_inventory_request(valid_snapshot)


def test_no_disks_and_no_hardware_storage_fallback_raises_mapping_error(valid_snapshot):
    valid_snapshot.storage.volumes = []
    valid_snapshot.hardware.storage_total_bytes = Measured.unavailable("x")

    with pytest.raises(MappingError, match="disks"):
        map_to_inventory_request(valid_snapshot)


def test_mapping_never_sends_unrelated_rich_fields(valid_snapshot):
    """The narrow DTO must contain ONLY the current contract's fields —
    nothing from applications/users/network/gpu leaks in, since the backend
    400s on any extra field (forbidNonWhitelisted: true).
    """
    request = map_to_inventory_request(valid_snapshot)

    json_dict = request.to_json_dict()
    assert set(json_dict.keys()) == {
        "hostname",
        "category",
        "os",
        "cpu",
        "ram",
        "disks",
        "timestamp",
    }
    assert set(json_dict["os"].keys()) == {"name", "version"}
    assert set(json_dict["cpu"].keys()) == {"model", "cores"}
    assert set(json_dict["ram"].keys()) == {"totalGB"}
