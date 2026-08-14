from __future__ import annotations

from itmas_agent.collectors.hardware import HardwareCollector
from itmas_agent.platforms.base import HardwareRawInfo
from tests.fakes.fake_platform_backend import FakePlatformBackend


def test_merges_backend_fields_with_psutil_sourced_fields():
    backend = FakePlatformBackend()

    snapshot = HardwareCollector(backend).collect()

    # From the backend (genuinely OS-specific)
    assert snapshot.manufacturer.value == "Apple"
    assert snapshot.model.value == "MacBook Pro"
    assert snapshot.processor_model.value == "Apple M4 Pro"
    assert snapshot.gpu.cores.value == 16
    # From psutil/platform — real values from the machine running the test,
    # just asserted as present (this suite runs on real CI hardware too).
    assert snapshot.architecture.value
    assert snapshot.cpu_cores_physical.value and snapshot.cpu_cores_physical.value > 0
    assert snapshot.ram_total_bytes.value and snapshot.ram_total_bytes.value > 0


def test_apple_silicon_gpu_memory_is_unavailable_with_reason_not_an_error():
    backend = FakePlatformBackend()

    snapshot = HardwareCollector(backend).collect()

    assert snapshot.gpu.memory_bytes.value is None
    assert snapshot.gpu.memory_bytes.reason == "unified memory"


def test_missing_backend_hardware_fields_degrade_independently():
    backend = FakePlatformBackend(
        hardware=HardwareRawInfo(
            manufacturer="Apple",
            model=None,
            model_identifier=None,
            processor_model="Apple M4 Pro",
            gpu_model=None,
            gpu_cores=None,
            gpu_memory_bytes=None,
        )
    )

    snapshot = HardwareCollector(backend).collect()

    assert snapshot.model.value is None
    assert snapshot.model.reason is not None
    # A missing model never blocks collection of the fields that ARE available.
    assert snapshot.manufacturer.value == "Apple"
    assert snapshot.processor_model.value == "Apple M4 Pro"
