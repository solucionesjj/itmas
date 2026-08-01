import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';
import { AlertRuleType } from '../alert-rules/alert-rule-type.enum';
import { AlertStatus } from './alert-status.enum';

export type AlertDocument = HydratedDocument<Alert>;

@Schema({ collection: 'alerts' })
export class Alert {
  @Prop({ type: String, required: true, enum: AlertRuleType })
  type!: AlertRuleType;

  @Prop({ type: String, required: true })
  deviceId!: string;

  @Prop({ type: Object, required: false })
  detail?: Record<string, unknown>;

  @Prop({ required: true, default: () => new Date() })
  createdAt!: Date;

  @Prop({
    type: String,
    required: true,
    enum: AlertStatus,
    default: AlertStatus.OPEN,
  })
  status!: AlertStatus;
}

export const AlertSchema = SchemaFactory.createForClass(Alert);
// Required indexes (agent.md §5.3).
AlertSchema.index({ type: 1, createdAt: -1 });
AlertSchema.index({ status: 1 });
