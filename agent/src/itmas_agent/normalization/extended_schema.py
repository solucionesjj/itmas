"""Builds the canonical, cross-platform rich JSON contract (schemaVersion
1.0 — see agent/docs/ARCHITECTURE.md and docs/adr/0012-...) from the
internal `InventorySnapshot` model. Every `Measured[T]` collapses to its
`.value` (already None when unavailable) — the wire format is plain
nullable JSON, deliberately dropping the internal `.reason`, which stays in
local logs only. This keeps the contract simple and stable across languages
(this agent, and future Windows/Linux agents reusing the same shape).
"""

from __future__ import annotations

from itmas_agent.models import InventorySnapshot, iso_z


def to_extended_json(snapshot: InventorySnapshot) -> dict:
    hw = snapshot.hardware
    return {
        "schemaVersion": snapshot.schema_version,
        "agent": {"platform": snapshot.agent_platform, "version": snapshot.agent_version},
        "device": {
            "deviceId": snapshot.device.device_id.value,
            "hardwareSerial": snapshot.device.hardware_serial.value,
            "hostname": snapshot.device.hostname.value,
            "category": snapshot.device.category,
        },
        "collectedAt": iso_z(snapshot.collected_at),
        "os": {
            "name": snapshot.os.name.value,
            "version": snapshot.os.version.value,
            "build": snapshot.os.build.value,
            "kernelVersion": snapshot.os.kernel_version.value,
            "architecture": snapshot.os.architecture.value,
        },
        "hardware": {
            "manufacturer": hw.manufacturer.value,
            "model": hw.model.value,
            "modelIdentifier": hw.model_identifier.value,
            "processorModel": hw.processor_model.value,
            "architecture": hw.architecture.value,
            "cpuCoresPhysical": hw.cpu_cores_physical.value,
            "cpuCoresLogical": hw.cpu_cores_logical.value,
            "ramTotalBytes": hw.ram_total_bytes.value,
            "gpu": {
                "model": hw.gpu.model.value,
                "cores": hw.gpu.cores.value,
                "memoryBytes": hw.gpu.memory_bytes.value,
            },
            "storageTotalBytes": hw.storage_total_bytes.value,
            "storageType": hw.storage_type.value,
            "diskCount": hw.disk_count.value,
        },
        "storage": {
            "volumes": [
                {
                    "filesystem": v.filesystem,
                    "mountPoint": v.mount_point,
                    "totalBytes": v.total_bytes,
                    "usedBytes": v.used_bytes,
                    "availableBytes": v.available_bytes,
                    "usagePercent": v.usage_percent,
                }
                for v in snapshot.storage.volumes
            ]
        },
        "applications": [
            {
                "name": a.name,
                "version": a.version.value,
                "bundleId": a.bundle_id.value,
                "path": a.path,
            }
            for a in snapshot.applications.applications
        ],
        "users": [
            {
                "username": u.username,
                "uid": u.uid,
                "fullName": u.full_name.value,
                "homeDirectory": u.home_directory.value,
                "shell": u.shell.value,
                "accountType": u.account_type.value,
                "lastLogin": iso_z(u.last_login.value),
                "groups": u.groups,
            }
            for u in snapshot.users.users
        ],
        "resources": {
            "cpuUsagePercent": snapshot.resources.cpu_usage_percent.value,
            "memoryUsagePercent": snapshot.resources.memory_usage_percent.value,
            "memoryTotalBytes": snapshot.resources.memory_total_bytes.value,
            "memoryUsedBytes": snapshot.resources.memory_used_bytes.value,
            "uptimeSeconds": snapshot.resources.uptime_seconds.value,
        },
        "network": {
            "localIp": snapshot.network.local_ip.value,
            "publicIp": snapshot.network.public_ip.value,
        },
    }
