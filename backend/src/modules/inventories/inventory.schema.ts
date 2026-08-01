import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type InventoryDocument = HydratedDocument<Inventory>;

export class Disk {
  @Prop({ required: true })
  name!: string;

  @Prop({ required: true })
  sizeGB!: number;
}

// Append-only per agent.md §5.3 — never update/overwrite a stored inventory.
@Schema({ collection: 'inventories' })
export class Inventory {
  @Prop({ type: String, required: true })
  deviceId!: string;

  // Client-supplied collection time (not server receipt time) — this is
  // also the natural-key half of the idempotency index below, so a node's
  // retried request (same timestamp) must not be overwritten by "now".
  @Prop({ required: true })
  timestamp!: Date;

  @Prop({ type: { model: String, cores: Number }, required: true })
  cpu!: { model: string; cores: number };

  @Prop({ type: { totalGB: Number }, required: true })
  ram!: { totalGB: number };

  @Prop({ type: [{ name: String, sizeGB: Number }], required: true })
  disks!: Disk[];
}

export const InventorySchema = SchemaFactory.createForClass(Inventory);
// Required query index (agent.md §5.3).
InventorySchema.index({ deviceId: 1, timestamp: -1 });
// Idempotency: a node resending the same collection timestamp for the same
// device must not create a duplicate append-only record (agent.md §4:
// "la ingesta debe tolerar reenvíos de nodos").
InventorySchema.index({ deviceId: 1, timestamp: 1 }, { unique: true });
