import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Types } from 'mongoose';

export type AuditLogDocument = HydratedDocument<AuditLog>;

// spec.md's example (create_user | update_alert_rule | login |
// update_alert_status) is illustrative, not exhaustive — each sub-phase adds
// the actions it introduces. 1.1: login/login_failed/logout/change_password.
// 1.3: update_alert_rule, update_alert_status. 1.4: create_user, update_user.
// EXT-1 (ADR-0013): sync_run, review_security_group_rule,
// authorize_security_group_rule.
export type AuditLogAction =
  | 'login'
  | 'login_failed'
  | 'logout'
  | 'change_password'
  | 'update_alert_rule'
  | 'update_alert_status'
  | 'create_user'
  | 'update_user'
  | 'sync_run'
  | 'review_security_group_rule'
  | 'authorize_security_group_rule';

@Schema({ collection: 'audit_log' })
export class AuditLog {
  @Prop({ type: Types.ObjectId, ref: 'User', required: false })
  actorId?: Types.ObjectId;

  @Prop({ type: String, required: true })
  action!: AuditLogAction;

  @Prop({ required: false })
  target?: string;

  @Prop({ type: Object, required: false })
  detail?: Record<string, unknown>;

  @Prop({ required: true, default: () => new Date() })
  timestamp!: Date;
}

export const AuditLogSchema = SchemaFactory.createForClass(AuditLog);
AuditLogSchema.index({ actorId: 1, timestamp: -1 });
AuditLogSchema.index({ action: 1 });
