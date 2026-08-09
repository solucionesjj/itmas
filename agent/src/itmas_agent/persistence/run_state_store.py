"""Persists each collector's last successful-run timestamp, plus the overall
last full-run date the 9am catch-up check in scheduling.py needs. One flat
JSON file — data volume is tiny (a handful of keys), so this doesn't need
anything heavier than atomic read/write.
"""

from __future__ import annotations

from datetime import datetime
from pathlib import Path
from typing import Optional

from itmas_agent.persistence.json_store import read_json, write_json_atomic


class RunStateStore:
    def __init__(self, path: Path) -> None:
        self._path = path

    def last_success(self, collector_key: str) -> Optional[datetime]:
        data = read_json(self._path) or {}
        raw = data.get(collector_key)
        if not raw:
            return None
        try:
            return datetime.fromisoformat(raw)
        except ValueError:
            return None

    def record_success(self, collector_key: str, when: datetime) -> None:
        data = read_json(self._path) or {}
        data[collector_key] = when.isoformat()
        write_json_atomic(self._path, data)
