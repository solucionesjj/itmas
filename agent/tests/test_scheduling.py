from __future__ import annotations

from datetime import datetime, timedelta, timezone

from itmas_agent import scheduling
from itmas_agent.persistence.run_state_store import RunStateStore


class _FakeClock:
    def __init__(self, fixed_now: datetime) -> None:
        self._fixed_now = fixed_now

    def now(self) -> datetime:
        return self._fixed_now


def _store(tmp_path) -> RunStateStore:
    return RunStateStore(tmp_path / "run_state.json")


def test_before_scheduled_time_with_no_prior_run_does_not_run(tmp_path):
    clock = _FakeClock(datetime(2026, 8, 8, 7, 0, 0, tzinfo=timezone.utc))

    decision = scheduling.is_daily_run_due(_store(tmp_path), clock, scheduled_hour=9)

    assert decision.should_run is False
    assert "before today's scheduled time" in decision.reason


def test_at_or_after_scheduled_time_with_no_prior_run_today_runs(tmp_path):
    clock = _FakeClock(datetime(2026, 8, 8, 9, 0, 0, tzinfo=timezone.utc))

    decision = scheduling.is_daily_run_due(_store(tmp_path), clock, scheduled_hour=9)

    assert decision.should_run is True


def test_well_after_scheduled_time_still_runs_if_not_yet_run_today(tmp_path):
    """This is the catch-up case: the machine was asleep/off at 9am and
    only wakes at, say, 2pm — it must still run rather than waiting for
    tomorrow's 9am.
    """
    clock = _FakeClock(datetime(2026, 8, 8, 14, 30, 0, tzinfo=timezone.utc))

    decision = scheduling.is_daily_run_due(_store(tmp_path), clock, scheduled_hour=9)

    assert decision.should_run is True


def test_already_ran_today_does_not_run_again(tmp_path):
    store = _store(tmp_path)
    clock = _FakeClock(datetime(2026, 8, 8, 9, 5, 0, tzinfo=timezone.utc))
    scheduling.record_daily_run_success(store, clock)

    later_clock = _FakeClock(datetime(2026, 8, 8, 16, 0, 0, tzinfo=timezone.utc))
    decision = scheduling.is_daily_run_due(store, later_clock, scheduled_hour=9)

    assert decision.should_run is False
    assert "already ran" in decision.reason


def test_ran_yesterday_runs_again_today(tmp_path):
    store = _store(tmp_path)
    yesterday_clock = _FakeClock(datetime(2026, 8, 7, 9, 5, 0, tzinfo=timezone.utc))
    scheduling.record_daily_run_success(store, yesterday_clock)

    today_clock = _FakeClock(datetime(2026, 8, 8, 9, 5, 0, tzinfo=timezone.utc))
    decision = scheduling.is_daily_run_due(store, today_clock, scheduled_hour=9)

    assert decision.should_run is True


def test_respects_configured_scheduled_hour_and_minute(tmp_path):
    clock = _FakeClock(datetime(2026, 8, 8, 8, 45, 0, tzinfo=timezone.utc))

    decision = scheduling.is_daily_run_due(
        _store(tmp_path), clock, scheduled_hour=8, scheduled_minute=30
    )

    assert decision.should_run is True


def test_record_daily_run_success_persists_the_clocks_timestamp(tmp_path):
    store = _store(tmp_path)
    when = datetime(2026, 8, 8, 9, 0, 3, tzinfo=timezone.utc)
    clock = _FakeClock(when)

    scheduling.record_daily_run_success(store, clock)

    assert store.last_success(scheduling.DAILY_RUN_KEY) == when
