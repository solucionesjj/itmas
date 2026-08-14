"""Hardware summary collector.

Merges genuinely OS-specific fields (model, processor name, GPU, storage —
via the injected PlatformBackend) with fields `psutil`/`platform` already
resolve identically across macOS/Windows/Linux (CPU core counts, total RAM,
architecture) — no backend call needed for the latter, per the project's
core-vs-platform split.
"""

from __future__ import annotations

import platform

import psutil

from itmas_agent.models import GpuInfo, HardwareSnapshot, Measured, measured_from_optional
from itmas_agent.platforms.base import PlatformBackend

_BACKEND_UNAVAILABLE_REASON = "not reported by platform backend"


def _safe(fn, reason_prefix: str) -> Measured:
    try:
        value = fn()
    except Exception as exc:  # collectors must never raise — see collectors/base.py
        return Measured.unavailable(f"{reason_prefix}: {exc}")
    return measured_from_optional(value, f"{reason_prefix}: returned no value")


class HardwareCollector:
    key = "hardware"
    default_interval_seconds = 24 * 60 * 60

    def __init__(self, backend: PlatformBackend) -> None:
        self._backend = backend

    def collect(self) -> HardwareSnapshot:
        raw = self._backend.hardware_info()

        gpu = GpuInfo(
            model=measured_from_optional(raw.gpu_model, _BACKEND_UNAVAILABLE_REASON),
            cores=measured_from_optional(raw.gpu_cores, _BACKEND_UNAVAILABLE_REASON),
            memory_bytes=measured_from_optional(
                raw.gpu_memory_bytes,
                raw.gpu_memory_unavailable_reason or _BACKEND_UNAVAILABLE_REASON,
            ),
        )

        return HardwareSnapshot(
            manufacturer=measured_from_optional(raw.manufacturer, _BACKEND_UNAVAILABLE_REASON),
            model=measured_from_optional(raw.model, _BACKEND_UNAVAILABLE_REASON),
            model_identifier=measured_from_optional(
                raw.model_identifier, _BACKEND_UNAVAILABLE_REASON
            ),
            processor_model=measured_from_optional(
                raw.processor_model, _BACKEND_UNAVAILABLE_REASON
            ),
            architecture=_safe(platform.machine, "platform.machine() failed"),
            cpu_cores_physical=_safe(
                lambda: psutil.cpu_count(logical=False), "psutil.cpu_count(logical=False) failed"
            ),
            cpu_cores_logical=_safe(
                lambda: psutil.cpu_count(logical=True), "psutil.cpu_count(logical=True) failed"
            ),
            ram_total_bytes=_safe(
                lambda: psutil.virtual_memory().total, "psutil.virtual_memory() failed"
            ),
            gpu=gpu,
            storage_total_bytes=measured_from_optional(
                raw.storage_total_bytes, _BACKEND_UNAVAILABLE_REASON
            ),
            storage_type=measured_from_optional(raw.storage_type, _BACKEND_UNAVAILABLE_REASON),
            disk_count=measured_from_optional(raw.disk_count, _BACKEND_UNAVAILABLE_REASON),
        )
