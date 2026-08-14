"""Basic resource snapshot — CPU/memory utilization at the moment of
collection (a point-in-time reading, not a time series — matches the
once-daily/on-demand cadence). Pure psutil, no backend needed.
"""

from __future__ import annotations

import time

import psutil

from itmas_agent.models import Measured, ResourceSnapshot


def _safe(fn, reason_prefix: str) -> Measured:
    try:
        return Measured.ok(fn())
    except Exception as exc:  # collectors must never raise
        return Measured.unavailable(f"{reason_prefix}: {exc}")


class ResourceCollector:
    key = "resources"
    default_interval_seconds = 24 * 60 * 60

    def collect(self) -> ResourceSnapshot:
        # A blocking 0.5s sample — psutil.cpu_percent() without an interval
        # would return a meaningless 0.0 on a single one-shot call. This
        # cost is negligible at once-daily/on-demand cadence.
        cpu = _safe(lambda: psutil.cpu_percent(interval=0.5), "psutil.cpu_percent() failed")

        try:
            vm = psutil.virtual_memory()
            mem_percent = Measured.ok(vm.percent)
            mem_total = Measured.ok(vm.total)
            mem_used = Measured.ok(vm.used)
        except Exception as exc:
            reason = f"psutil.virtual_memory() failed: {exc}"
            mem_percent = Measured.unavailable(reason)
            mem_total = Measured.unavailable(reason)
            mem_used = Measured.unavailable(reason)

        uptime = _safe(lambda: time.time() - psutil.boot_time(), "uptime calculation failed")

        return ResourceSnapshot(
            cpu_usage_percent=cpu,
            memory_usage_percent=mem_percent,
            memory_total_bytes=mem_total,
            memory_used_bytes=mem_used,
            uptime_seconds=uptime,
        )
