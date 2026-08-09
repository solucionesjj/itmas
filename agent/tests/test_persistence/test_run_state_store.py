from __future__ import annotations

from datetime import datetime, timezone

from itmas_agent.persistence.run_state_store import RunStateStore


def test_no_recorded_run_returns_none(tmp_path):
    store = RunStateStore(tmp_path / "run_state.json")

    assert store.last_success("hardware") is None


def test_record_then_read_round_trips(tmp_path):
    store = RunStateStore(tmp_path / "run_state.json")
    when = datetime(2026, 8, 8, 9, 0, 3, tzinfo=timezone.utc)

    store.record_success("hardware", when)

    assert store.last_success("hardware") == when


def test_different_collectors_track_independently(tmp_path):
    store = RunStateStore(tmp_path / "run_state.json")
    hardware_time = datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc)
    users_time = datetime(2026, 8, 7, 9, 0, 0, tzinfo=timezone.utc)

    store.record_success("hardware", hardware_time)
    store.record_success("users", users_time)

    assert store.last_success("hardware") == hardware_time
    assert store.last_success("users") == users_time
