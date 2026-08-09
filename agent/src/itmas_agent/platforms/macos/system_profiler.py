"""Thin, testable wrapper around Apple's own `system_profiler` CLI.

Not a foreign/Linux tool pressed into service — this is the exact source
Apple's own "About This Mac" reads, and `-json` gives structured output
instead of fragile text parsing. Shapes below were captured from a real
`system_profiler <type> -json` run, not guessed from memory.
"""

from __future__ import annotations

import json
import subprocess
from typing import Optional

_TIMEOUT_SECONDS = 10.0


def query(data_type: str) -> Optional[list[dict]]:
    """Raw item list for a given SPxxxDataType, or None on any failure
    (missing binary, timeout, malformed output). Callers must treat None as
    "unavailable" — never as a valid empty result.
    """
    try:
        result = subprocess.run(
            ["system_profiler", data_type, "-json"],
            capture_output=True,
            text=True,
            timeout=_TIMEOUT_SECONDS,
            check=True,
        )
    except (subprocess.SubprocessError, OSError):
        return None
    try:
        parsed = json.loads(result.stdout)
    except json.JSONDecodeError:
        return None
    items = parsed.get(data_type)
    if not isinstance(items, list) or not items:
        return None
    return items


def get_hardware_overview() -> Optional[dict]:
    """SPHardwareDataType — a single-item list; keys include `chip_type`
    (Apple Silicon) or `cpu_type` (Intel), `machine_name`, `machine_model`,
    `number_processors` (e.g. "proc 8:6:2:0" — total:performance:efficiency:?),
    `physical_memory` (e.g. "16 GB"), `serial_number`.
    """
    items = query("SPHardwareDataType")
    return items[0] if items else None


def get_displays() -> list[dict]:
    """SPDisplaysDataType — one entry per GPU. Apple Silicon entries carry
    `sppci_cores` (GPU core count) and no VRAM key at all (unified memory);
    discrete-GPU Intel Macs carry a VRAM-ish key instead.
    """
    return query("SPDisplaysDataType") or []


def get_storage_entries() -> list[dict]:
    """SPStorageDataType — one entry per mounted APFS/HFS+ volume, including
    disk images and simulator volumes. `size_in_bytes`/`free_space_in_bytes`
    are already integers. `physical_drive.is_internal_disk` distinguishes
    real internal disks from disk images/external volumes;
    `physical_drive.device_name` is the same across multiple APFS volumes
    sharing one physical container — dedupe on it before summing capacity.
    """
    return query("SPStorageDataType") or []
