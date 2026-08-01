import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';
import { AccessEventLevel } from './access-event-level.enum';
import { AccessEventAction } from './access-event-action.enum';

export type AccessEventDocument = HydratedDocument<AccessEvent>;

@Schema({ collection: 'access_events' })
export class AccessEvent {
  @Prop({ type: String, required: true })
  deviceId!: string;

  @Prop({ type: String, required: true, enum: AccessEventLevel })
  level!: AccessEventLevel;

  @Prop({ required: true, trim: true })
  user!: string;

  // Client-supplied event time — also half of the idempotency natural key.
  @Prop({ required: true })
  timestamp!: Date;

  @Prop({ type: String, required: true, enum: AccessEventAction })
  action!: AccessEventAction;
}

export const AccessEventSchema = SchemaFactory.createForClass(AccessEvent);
// Required indexes (agent.md §5.3).
AccessEventSchema.index({ deviceId: 1, timestamp: -1 });
AccessEventSchema.index({ level: 1 });
// Idempotency natural key — a node resending the identical event must not
// create a duplicate (agent.md §4: "la ingesta debe tolerar reenvíos de nodos").
AccessEventSchema.index(
  { deviceId: 1, level: 1, user: 1, timestamp: 1, action: 1 },
  { unique: true },
);
