from __future__ import annotations

from itmas_agent.persistence.json_store import read_json, write_json_atomic


def test_write_then_read_round_trips(tmp_path):
    path = tmp_path / "state.json"
    write_json_atomic(path, {"hello": "world", "n": 42})

    assert read_json(path) == {"hello": "world", "n": 42}


def test_read_missing_file_returns_none(tmp_path):
    assert read_json(tmp_path / "does-not-exist.json") is None


def test_write_creates_parent_directories(tmp_path):
    path = tmp_path / "nested" / "dir" / "state.json"

    write_json_atomic(path, {"a": 1})

    assert read_json(path) == {"a": 1}


def test_write_leaves_no_leftover_tmp_file(tmp_path):
    path = tmp_path / "state.json"

    write_json_atomic(path, {"a": 1})

    assert not (tmp_path / "state.json.tmp").exists()
