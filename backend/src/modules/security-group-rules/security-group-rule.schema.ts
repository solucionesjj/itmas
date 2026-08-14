import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Types } from 'mongoose';
import { SecurityGroupRuleStatus } from './security-group-rule-status.enum';
import { SecurityGroupRuleDirection } from './security-group-rule-direction.enum';

export type SecurityGroupRuleDocument = HydratedDocument<SecurityGroupRule>;

/**
 * An AWS resource attached to the security group (via its ENI) — an EC2
 * instance, RDS instance, ALB/NLB, or Lambda-in-VPC. Not a reference to
 * IT-MAS's own `devices` collection (ADR-0013): a security group can be
 * attached to 0, 1, or many resources, and there is no guaranteed
 * relationship between an AWS resource and a device that reports inventory
 * via IT-MAS's own collection agent.
 */
export interface AttachedResource {
  resourceType:
    'ec2-instance' | 'rds-instance' | 'load-balancer' | 'lambda' | 'other';
  resourceId: string;
  resourceName?: string;
}

/**
 * The single endpoint AWS actually configures on a rule — exactly one of
 * these kinds is ever populated per rule (AWS itself enforces this; a rule
 * never carries more than one remote-endpoint reference).
 */
export interface RemoteEndpoint {
  kind: 'cidr_ipv4' | 'cidr_ipv6' | 'security_group' | 'prefix_list';
  value: string;
  description?: string;
}

@Schema({ collection: 'security_group_rules' })
export class SecurityGroupRule {
  @Prop({ type: String, required: true })
  awsAccountId!: string;

  @Prop({ type: String, required: true })
  region!: string;

  @Prop({ type: String, required: true })
  vpcId!: string;

  @Prop({ type: String, required: true })
  securityGroupId!: string;

  @Prop({ type: String, required: true })
  securityGroupName!: string;

  @Prop({ type: [Object], required: true, default: [] })
  attachedResources!: AttachedResource[];

  @Prop({ type: String, required: true })
  ruleId!: string;

  @Prop({ type: String, required: false, default: null })
  ruleName!: string | null;

  @Prop({ type: String, required: true, enum: SecurityGroupRuleDirection })
  direction!: SecurityGroupRuleDirection;

  @Prop({ type: Object, required: true })
  remoteEndpoint!: RemoteEndpoint;

  // Derived from direction + remoteEndpoint (ADR-0013's directional mapping)
  // and persisted, not recomputed at read time — the columns spec.md §21
  // literally names ("Origen"/"Destino"), sortable/filterable like any other
  // field.
  @Prop({ type: String, required: true })
  source!: string;

  @Prop({ type: String, required: true })
  destination!: string;

  @Prop({ type: String, required: true })
  protocol!: string;

  @Prop({ type: String, required: false, default: null })
  portRange!: string | null;

  @Prop({
    type: String,
    required: true,
    enum: SecurityGroupRuleStatus,
    default: SecurityGroupRuleStatus.PENDIENTE,
  })
  status!: SecurityGroupRuleStatus;

  // Set once at first observation, never touched again (ADR-0013: the EC2
  // API exposes no rule-creation timestamp).
  @Prop({ required: true, default: () => new Date() })
  createdAt!: Date;

  // Updated on every sync run that still observes this rule — distinct from
  // createdAt, which never changes after the first insert.
  @Prop({ required: true, default: () => new Date() })
  lastSeenAt!: Date;

  @Prop({ type: String, required: false, default: null })
  reviewObservation!: string | null;

  @Prop({ type: Date, required: false, default: null })
  reviewedAt!: Date | null;

  @Prop({ type: Types.ObjectId, ref: 'User', required: false, default: null })
  reviewedBy!: Types.ObjectId | null;

  @Prop({ type: String, required: false, default: null })
  authorizationObservation!: string | null;

  @Prop({ type: Date, required: false, default: null })
  authorizedAt!: Date | null;

  @Prop({ type: Types.ObjectId, ref: 'User', required: false, default: null })
  authorizedBy!: Types.ObjectId | null;

  @Prop({ type: Date, required: false, default: null })
  deletedAt!: Date | null;
}

export const SecurityGroupRuleSchema =
  SchemaFactory.createForClass(SecurityGroupRule);

// Natural key — same idempotency idiom as inventories/access_events
// (agent.md Assumption #15): a sync re-observing the same rule upserts in
// place instead of duplicating.
SecurityGroupRuleSchema.index(
  { awsAccountId: 1, region: 1, securityGroupId: 1, ruleId: 1 },
  { unique: true },
);
// Status filter + "new since last run" queries (RF-27).
SecurityGroupRuleSchema.index({ status: 1, createdAt: -1 });
// Export sort requirement (RF-25: ordered by group id, then rule id).
SecurityGroupRuleSchema.index({ securityGroupId: 1, ruleId: 1 });
// The other two independent date-range filters (RF-25) — sparse since most
// records have neither set until reviewed/authorized.
SecurityGroupRuleSchema.index({ reviewedAt: 1 }, { sparse: true });
SecurityGroupRuleSchema.index({ authorizedAt: 1 }, { sparse: true });
