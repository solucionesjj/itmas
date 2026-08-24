import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema, Types } from 'mongoose';

export type SacStatisticDocument = HydratedDocument<SacStatistic>;

/**
 * One daily statistics record per SAC database, as reported by the database
 * engine of a client cloud (BL-031 / ADR-0018). Field names are the English
 * camelCase contract names, mapped from the SQL Server source columns —
 * `[Base]` → `databaseName`, `[tamanoTotalBaseDatos]` → `totalSizeGb`, and so
 * on; the full mapping table is in docs/backlog.md's BL-031 entry.
 *
 * Deliberately append-only WITHOUT a unique natural key, unlike `inventories`
 * (unique `(deviceId, timestamp)`) and `access_events`: a re-send from the same
 * engine stores an additional record rather than being absorbed as an
 * idempotent retry. That trade-off is recorded in ADR-0018 — the monthly
 * aggregations of BL-033 neutralise duplicates by taking the LAST snapshot of
 * each period per database, and adding the unique index later is purely
 * additive.
 */
@Schema({ collection: 'sac_statistics' })
export class SacStatistic {
  /**
   * Resolved from the reporting node's API key by NodeApiKeyGuard, never taken
   * from the payload — it answers "which engine sent this" without a second
   * lookup, same provenance idiom as `inventories.deviceId`.
   */
  @Prop({ type: String, required: true })
  deviceId!: string;

  @Prop({ type: String, required: true, maxlength: 128 })
  databaseName!: string;

  /**
   * Generation time reported by the engine itself (source `[fechaGeneracion]`),
   * never server-stamped: it is the axis every BL-033 aggregation groups on, so
   * a delayed or retried upload must not be filed under the time it arrived.
   */
  @Prop({ type: Date, required: true })
  generatedAt!: Date;

  // Integer gauges. All optional and nullable, mirroring the source columns,
  // which are themselves nullable — a partial row is stored as reported rather
  // than rejected or zero-filled.
  @Prop({ type: Number, required: false, default: null })
  totalSizeGb!: number | null;

  @Prop({ type: Number, required: false, default: null })
  debtors!: number | null;

  @Prop({ type: Number, required: false, default: null })
  debtorActiveAccounts!: number | null;

  @Prop({ type: Number, required: false, default: null })
  activeAccounts!: number | null;

  @Prop({ type: Number, required: false, default: null })
  avgActiveUsersLast3Months!: number | null;

  /** Cumulative lifetime counter — never resets (see ADR-0018 on its growth semantics). */
  @Prop({ type: Number, required: false, default: null })
  activities!: number | null;

  /** Rolling 30-day window total — already comparable month over month. */
  @Prop({ type: Number, required: false, default: null })
  activitiesLast30Days!: number | null;

  @Prop({ type: Number, required: false, default: null })
  activeUsers!: number | null;

  @Prop({ type: Number, required: false, default: null })
  logSizeGb!: number | null;

  @Prop({ type: Number, required: false, default: null })
  dataSizeGb!: number | null;

  // The three financial sums are Decimal128, not Number: the source columns are
  // numeric(18,2), and 18 significant digits exceed the ~15.9 a JavaScript
  // double represents exactly (2^53). Stored as a double, aggregated totals
  // would silently drift by cents. They are serialised as strings on the way
  // out (sac-statistic-response.mapper.ts) for the same reason.
  @Prop({
    type: MongooseSchema.Types.Decimal128,
    required: false,
    default: null,
  })
  balanceSum!: Types.Decimal128 | null;

  @Prop({
    type: MongooseSchema.Types.Decimal128,
    required: false,
    default: null,
  })
  overdueSum!: Types.Decimal128 | null;

  @Prop({
    type: MongooseSchema.Types.Decimal128,
    required: false,
    default: null,
  })
  principalSum!: Types.Decimal128 | null;
}

export const SacStatisticSchema = SchemaFactory.createForClass(SacStatistic);

// Per-database filter and time series — the shape both the BL-032 listing
// filter and every BL-033 aggregation reads.
SacStatisticSchema.index({ databaseName: 1, generatedAt: -1 });
// Global date-range scans (the listing's default sort, and the growth window).
SacStatisticSchema.index({ generatedAt: -1 });
