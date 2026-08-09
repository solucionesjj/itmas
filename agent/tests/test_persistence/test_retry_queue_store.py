from __future__ import annotations

from datetime import datetime, timedelta, timezone

from itmas_agent.persistence.json_store import write_json_atomic
from itmas_agent.persistence.retry_queue_store import (
    MAX_AGE_DAYS,
    MAX_QUEUE_SIZE,
    RetryQueueStore,
)


def test_enqueue_then_pending_round_trips(tmp_path):
    store = RetryQueueStore(tmp_path / "queue.json")

    store.enqueue("inventory", {"hostname": "test-host"})

    pending = store.pending()
    assert len(pending) == 1
    assert pending[0]["kind"] == "inventory"
    assert pending[0]["payload"] == {"hostname": "test-host"}


def test_clear_empties_the_queue(tmp_path):
    store = RetryQueueStore(tmp_path / "queue.json")
    store.enqueue("inventory", {"a": 1})

    store.clear()

    assert store.pending() == []


def test_entries_older_than_max_age_are_pruned(tmp_path):
    path = tmp_path / "queue.json"
    stale_queued_at = (datetime.now(timezone.utc) - timedelta(days=MAX_AGE_DAYS + 1)).isoformat()
    fresh_queued_at = datetime.now(timezone.utc).isoformat()
    write_json_atomic(
        path,
        {
            "items": [
                {"kind": "inventory", "payload": {"id": "stale"}, "queued_at": stale_queued_at},
                {"kind": "inventory", "payload": {"id": "fresh"}, "queued_at": fresh_queued_at},
            ]
        },
    )
    store = RetryQueueStore(path)

    pending = store.pending()

    assert [item["payload"]["id"] for item in pending] == ["fresh"]


def test_queue_is_bounded_to_max_size(tmp_path):
    path = tmp_path / "queue.json"
    now = datetime.now(timezone.utc).isoformat()
    items = [
        {"kind": "inventory", "payload": {"id": i}, "queued_at": now}
        for i in range(MAX_QUEUE_SIZE + 50)
    ]
    write_json_atomic(path, {"items": items})
    store = RetryQueueStore(path)

    pending = store.pending()

    assert len(pending) == MAX_QUEUE_SIZE
    # Oldest (lowest id) entries are the ones dropped, not the newest.
    assert pending[0]["payload"]["id"] == 50
    assert pending[-1]["payload"]["id"] == MAX_QUEUE_SIZE + 49
