"""Durable retry queue for inventory/access-event payloads that failed to
send (network outage, 5xx, 429). Bounded so a persistent outage can't grow
this file without limit — the oldest entries are dropped first, and the
caller is expected to log that (see composition.py); it's never silent.
"""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path

from itmas_agent.persistence.json_store import read_json, write_json_atomic

MAX_QUEUE_SIZE = 500
MAX_AGE_DAYS = 7


class RetryQueueStore:
    def __init__(self, path: Path) -> None:
        self._path = path

    def enqueue(self, kind: str, payload: dict) -> None:
        data = read_json(self._path) or {"items": []}
        data["items"].append(
            {
                "kind": kind,
                "payload": payload,
                "queued_at": datetime.now(timezone.utc).isoformat(),
            }
        )
        data["items"] = self._prune(data["items"])
        write_json_atomic(self._path, data)

    def pending(self) -> list[dict]:
        data = read_json(self._path) or {"items": []}
        return self._prune(data["items"])

    def requeue(self, items: list[dict]) -> None:
        """Replaces the stored items as-is, preserving each item's original
        `queued_at` — unlike calling `enqueue()` per item, which would
        re-stamp `queued_at` to now and defeat the age-based pruning for
        anything that keeps failing across multiple flush attempts.
        """
        write_json_atomic(self._path, {"items": self._prune(items)})

    def clear(self) -> None:
        write_json_atomic(self._path, {"items": []})

    @staticmethod
    def _prune(items: list[dict]) -> list[dict]:
        cutoff = datetime.now(timezone.utc).timestamp() - MAX_AGE_DAYS * 86400
        fresh = [
            item
            for item in items
            if datetime.fromisoformat(item["queued_at"]).timestamp() >= cutoff
        ]
        return fresh[-MAX_QUEUE_SIZE:]
