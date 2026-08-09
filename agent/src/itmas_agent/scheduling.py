"""Drives "run once daily at 9am, catch up if missed, always support
on-demand". Three mechanisms combine to satisfy this (see
agent/docs/ARCHITECTURE.md for the full rationale):

  1. launchd's StartCalendarInterval (9:00) — the normal daily trigger,
     with launchd's own anacron-like wake catch-up if the Mac was asleep.
  2. launchd's RunAtLoad — catches a fresh install/reboot happening AFTER
     9am the same day (StartCalendarInterval alone wouldn't fire again
     until tomorrow).
  3. is_daily_run_due() below — the actual gate, independent of which of
     the above triggered this invocation: only run if today's LOCAL date
     has no recorded success yet AND local wall-clock time is already at
     or past the scheduled hour. This is what stops an early RunAtLoad
     firing (e.g. at boot, 6am) from "using up" the day before 9am, and
     it's the only piece of this that's testable with an injected clock —
     (1)/(2) are launchd's own behavior, verified in integration_smoke.

Deliberately uses LOCAL time, not UTC: "todos los días a las 9 am" means
the machine's own morning. This is independent of `collected_at`/timestamps
sent to the API, which stay UTC per the backend contract.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from typing import Protocol

from itmas_agent.persistence.run_state_store import RunStateStore

DAILY_RUN_KEY = "daily_full_run"


class Clock(Protocol):
    def now(self) -> datetime: ...


class SystemClock:
    def now(self) -> datetime:
        return datetime.now().astimezone()  # aware, local wall-clock time


@dataclass
class ScheduleDecision:
    should_run: bool
    reason: str


def is_daily_run_due(
    run_state: RunStateStore,
    clock: Clock,
    scheduled_hour: int = 9,
    scheduled_minute: int = 0,
) -> ScheduleDecision:
    now = clock.now()

    last_success = run_state.last_success(DAILY_RUN_KEY)
    if last_success is not None and last_success.astimezone(now.tzinfo).date() == now.date():
        return ScheduleDecision(False, "already ran successfully today")

    scheduled_time_today = now.replace(
        hour=scheduled_hour, minute=scheduled_minute, second=0, microsecond=0
    )
    if now < scheduled_time_today:
        return ScheduleDecision(
            False,
            f"before today's scheduled time ({scheduled_hour:02d}:{scheduled_minute:02d} local)",
        )

    return ScheduleDecision(
        True, "due — no successful run recorded for today at/after the scheduled time"
    )


def record_daily_run_success(run_state: RunStateStore, clock: Clock) -> None:
    run_state.record_success(DAILY_RUN_KEY, clock.now())
