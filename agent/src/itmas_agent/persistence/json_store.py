"""Generic atomic JSON file helpers — write-to-temp-then-rename so a crash
mid-write never leaves a corrupted file for the next read to trip over.
"""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Optional


def read_json(path: Path) -> Optional[dict]:
    try:
        with path.open("r", encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError):
        return None


def write_json_atomic(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = path.with_suffix(path.suffix + ".tmp")
    with tmp_path.open("w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, default=str)
    os.replace(tmp_path, path)  # atomic on both POSIX and Windows
    try:
        os.chmod(path, 0o600)
    except OSError:
        pass
