# ADR-0012: Proposed extension of the `/inventory` contract for the full agent payload

- **Status**: Proposed — **not implemented**. No `backend/` code changes accompany this ADR; it exists so the gap between what `agent/` collects and what `POST /api/v1/inventory` currently accepts is documented and actionable, not silently absorbed or forgotten.
- **Related**: ADR-0003 (node authentication), ADR-0005 (ingestion idempotency), ADR-0011 (agent architecture); `backend/src/modules/ingestion/dto/inventory-ingest.dto.ts`; `agent/src/itmas_agent/normalization/extended_schema.py`

## Context

`agent/` collects seven categories (hardware, OS, applications, storage, local users with groups/last login, resource state, network) into a rich internal model, and defines a canonical cross-platform JSON contract (`itmas_agent/normalization/extended_schema.py` — reproduced below) meant to be shared by the Windows/Linux agents built later. Verified directly against `backend/src/modules/ingestion/dto/inventory-ingest.dto.ts`: the **current** `POST /api/v1/inventory` DTO only accepts `{hostname, category, os:{name,version}, cpu:{model,cores}, ram:{totalGB}, disks:[{name,sizeGB}], timestamp}`, and the app's global `ValidationPipe({whitelist: true, forbidNonWhitelisted: true})` rejects any request carrying fields outside that exact shape with a `400` — it does not silently ignore extras. `agent/`'s `InventoryPayloadMapper` therefore only ever sends this narrow subset today; everything else is collected and cached locally, unused by the backend.

## Proposed decision

Extend `InventoryIngestDto` **additively** — every new field is optional, so this is not a breaking change to `/v1` per agent.md §5.4's versioning rule (no `/v2` needed; existing callers sending only the current fields keep working unchanged):

```ts
export class InventoryIngestDto {
  // ...existing required fields unchanged...

  @IsOptional() @IsString() hardwareSerial?: string;
  @IsOptional() @ValidateNested() @Type(() => GpuDto) gpu?: GpuDto;         // { model, cores, memoryBytes }
  @IsOptional() @IsString() storageType?: string;
  @IsOptional() @IsInt() diskCount?: number;
  @IsOptional() @ValidateNested({ each: true }) @Type(() => VolumeDto) volumes?: VolumeDto[];
  @IsOptional() @ValidateNested({ each: true }) @Type(() => ApplicationDto) applications?: ApplicationDto[];
  @IsOptional() @ValidateNested({ each: true }) @Type(() => LocalUserDto) localUsers?: LocalUserDto[];
  @IsOptional() @ValidateNested() @Type(() => ResourceUsageDto) resources?: ResourceUsageDto;
  @IsOptional() @ValidateNested() @Type(() => NetworkInfoDto) network?: NetworkInfoDto;
}
```

with nested DTOs mirroring `agent/`'s extended JSON contract field-for-field (`VolumeDto: {filesystem, mountPoint, totalBytes, usedBytes, availableBytes, usagePercent}`, `ApplicationDto: {name, version, bundleId, path}`, `LocalUserDto: {username, uid, fullName, homeDirectory, shell, accountType, lastLogin, groups}`, etc. — see `agent/src/itmas_agent/normalization/extended_schema.py` for the exact shape and field names to copy verbatim).

`Inventory` (Mongoose schema, `inventories.schema.ts`) would need matching optional sub-schemas for whatever subset of this the product actually wants to *query* later (agent.md §5.3 forbids renaming existing fields/collections without an ADR — this one covers the addition). Not every collected field necessarily needs to be persisted per-snapshot the way `cpu`/`ram`/`disks` are today; e.g. `applications`/`localUsers` might reasonably live on the `devices` document instead (most-recent-known-state, like `os`/`hostname` already do via `touchOnIngest`) rather than duplicated on every append-only `inventories` snapshot — that's a product/query-pattern decision for whoever picks this up, not resolved here.

`hardwareSerial` is proposed as a field **on the inventory payload**, not as a change to the node-authentication `deviceId` (ADR-0003's `<deviceId>.<secret>` scheme is unaffected) — it's informational, letting an administrator correlate a `deviceId` to a physical asset, and should probably land on `devices` (most-recent-known value) rather than repeated on every inventory snapshot.

## Consequences if adopted

- `agent/`'s `InventoryPayloadMapper` gains new mapping cases (per Fase 1's design — the mapper is the only place that knows the current contract's shape); no collector changes.
- Needs its own CA/RF traceability entry and Swagger regeneration (the CLI plugin already infers schema from decorators, per agent.md's sub-phase 1.7 notes — no manual `@ApiProperty()` work expected).
- `GET /devices`/`GET /stats/*` may want new filters/fields (e.g. filter by installed application, by admin-account presence) — out of scope for this ADR; a follow-up if the product wants to query on this data, not just store it.
- Windows/Linux agents (when built, per ADR-0011) target this same extended contract once implemented — no separate negotiation per platform.
