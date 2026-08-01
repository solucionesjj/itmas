import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Types } from 'mongoose';
import { AlertRuleType } from './alert-rule-type.enum';
import { AlertableResource } from './alertable-resource.enum';

export type AlertRuleDocument = HydratedDocument<AlertRule>;

export interface HabitualHours {
  from: string;
  to: string;
}

export interface ResourceChangeConfig {
  resources: AlertableResource[];
}

export interface OffHoursAccessConfig {
  habitualHours: HabitualHours;
}

export type AlertRuleConfig = ResourceChangeConfig | OffHoursAccessConfig;

@Schema({ collection: 'alert_rules' })
export class AlertRule {
  @Prop({ type: String, required: true, enum: AlertRuleType })
  type!: AlertRuleType;

  @Prop({ required: true, default: true })
  enabled!: boolean;

  @Prop({ type: Object, required: true })
  config!: AlertRuleConfig;

  @Prop({ type: Types.ObjectId, ref: 'User', required: false })
  createdBy?: Types.ObjectId;

  @Prop({ required: true, default: () => new Date() })
  updatedAt!: Date;
}

export const AlertRuleSchema = SchemaFactory.createForClass(AlertRule);
// One active rule per type — the engine reads "the enabled rule for this
// type" as a singular concept throughout spec.md/agent.md; a unique index
// keeps that assumption true instead of leaving it ambiguous.
AlertRuleSchema.index({ type: 1 }, { unique: true });
