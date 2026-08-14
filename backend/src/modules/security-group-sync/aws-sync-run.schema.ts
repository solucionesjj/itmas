import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Types } from 'mongoose';
import { AwsSyncTriggerType } from './aws-sync-trigger-type.enum';
import { AwsSyncRunStatus } from './aws-sync-run-status.enum';

export type AwsSyncRunDocument = HydratedDocument<AwsSyncRun>;

export interface GroupSyncResult {
  region: string;
  vpcId: string;
  groupId: string;
  groupName: string;
  outcome: 'success' | 'error';
  ruleCount: number;
  errorMessage?: string;
}

export interface AwsSyncRunSummary {
  groupsProcessed: number;
  groupsFailed: number;
  rulesProcessed: number;
  rulesCreated: number;
  rulesMarkedDeleted: number;
}

@Schema({ collection: 'aws_sync_runs' })
export class AwsSyncRun {
  @Prop({ type: String, required: true, enum: AwsSyncTriggerType })
  triggerType!: AwsSyncTriggerType;

  @Prop({ type: Types.ObjectId, ref: 'User', required: false, default: null })
  triggeredBy!: Types.ObjectId | null;

  @Prop({ required: true })
  startedAt!: Date;

  @Prop({ required: true })
  finishedAt!: Date;

  @Prop({ type: String, required: true, enum: AwsSyncRunStatus })
  status!: AwsSyncRunStatus;

  @Prop({ type: [String], required: true, default: [] })
  regionsChecked!: string[];

  @Prop({ type: [String], required: true, default: [] })
  vpcsChecked!: string[];

  @Prop({ type: [Object], required: true, default: [] })
  groupResults!: GroupSyncResult[];

  @Prop({ type: Object, required: true })
  summary!: AwsSyncRunSummary;
}

export const AwsSyncRunSchema = SchemaFactory.createForClass(AwsSyncRun);
// Run history is read newest-first (RF-26: /runs listing, and the summary
// endpoint's "latest run" lookup) — no other query pattern needed.
AwsSyncRunSchema.index({ startedAt: -1 });
