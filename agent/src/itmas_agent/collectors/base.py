"""Shared collector contract. Every collector is OS-agnostic — it only ever
talks to a PlatformBackend (if it needs one at all) or to psutil/stdlib
directly (if the data is already uniform across OSes). `collect()` never
raises: a failed reading becomes `Measured.unavailable(reason)`, never an
exception that would abort the rest of the run.
"""

from __future__ import annotations

from typing import Protocol


class Collector(Protocol):
    key: str
    default_interval_seconds: float

    def collect(self) -> object: ...
