from __future__ import annotations

from itmas_agent.collectors.resources import ResourceCollector


def test_maps_real_psutil_readings():
    # Runs against the real machine — asserts plausible shape, not exact
    # values (matches the project's integration-smoke philosophy for
    # anything backed by live system state).
    snapshot = ResourceCollector().collect()

    assert snapshot.cpu_usage_percent.value is not None
    assert 0.0 <= snapshot.cpu_usage_percent.value <= 100.0
    assert snapshot.memory_usage_percent.value is not None
    assert snapshot.memory_total_bytes.value > 0
    assert snapshot.uptime_seconds.value > 0


def test_virtual_memory_failure_degrades_independently_of_cpu(mocker):
    mocker.patch("itmas_agent.collectors.resources.psutil.cpu_percent", return_value=12.4)
    mocker.patch(
        "itmas_agent.collectors.resources.psutil.virtual_memory",
        side_effect=OSError("psutil internals failed"),
    )
    mocker.patch("itmas_agent.collectors.resources.psutil.boot_time", return_value=0.0)

    snapshot = ResourceCollector().collect()

    assert snapshot.cpu_usage_percent.value == 12.4
    assert snapshot.memory_usage_percent.value is None
    assert snapshot.memory_usage_percent.reason is not None
