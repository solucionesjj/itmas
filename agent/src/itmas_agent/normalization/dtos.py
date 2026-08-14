"""Field-for-field mirror of the REAL current backend contract — verified
directly against backend/src/modules/ingestion/dto/inventory-ingest.dto.ts
and access-event-ingest.dto.ts, not paraphrased. The backend's global
ValidationPipe has `whitelist: true, forbidNonWhitelisted: true`, so any
field here that doesn't exist there causes a 400 — never add a field to
these dataclasses without checking that DTO first.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass


@dataclass
class OsDto:
    name: str
    version: str


@dataclass
class CpuDto:
    model: str
    cores: int


@dataclass
class RamDto:
    totalGB: float


@dataclass
class DiskDto:
    name: str
    sizeGB: float


@dataclass
class InventoryIngestRequest:
    hostname: str
    category: str  # 'collaborator' | 'infrastructure'
    os: OsDto
    cpu: CpuDto
    ram: RamDto
    disks: list[DiskDto]  # min 1 element, enforced server-side
    timestamp: str  # ISO 8601 — the node's own collection time, never re-stamped on retry

    def to_json_dict(self) -> dict:
        return asdict(self)


@dataclass
class AccessEventIngestRequest:
    level: str  # 'os' | 'database' — this agent always sends 'os'
    user: str
    timestamp: str
    action: str  # 'login' | 'logout'

    def to_json_dict(self) -> dict:
        return asdict(self)
