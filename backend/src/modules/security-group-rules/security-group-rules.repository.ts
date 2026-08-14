import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import {
  AttachedResource,
  RemoteEndpoint,
  SecurityGroupRule,
  SecurityGroupRuleDocument,
} from './security-group-rule.schema';
import { SecurityGroupRuleStatus } from './security-group-rule-status.enum';
import { SecurityGroupRuleDirection } from './security-group-rule-direction.enum';
import { SecurityGroupRuleSortField } from './security-group-rule-sort-field.enum';
import { SortDirection } from './dto/query-security-group-rules.dto';
import { escapeRegex } from '../../common/util/escape-regex.util';

export interface SecurityGroupRulesFilter {
  q?: string;
  securityGroupId?: string;
  status?: SecurityGroupRuleStatus;
  createdFrom?: string;
  createdTo?: string;
  reviewedFrom?: string;
  reviewedTo?: string;
  authorizedFrom?: string;
  authorizedTo?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

export interface DistinctGroup {
  securityGroupId: string;
  securityGroupName: string;
}

/** Everything the sync engine observes about a rule from AWS on a given run. */
export interface ObservedSecurityGroupRule {
  awsAccountId: string;
  region: string;
  vpcId: string;
  securityGroupId: string;
  securityGroupName: string;
  attachedResources: AttachedResource[];
  ruleId: string;
  ruleName: string | null;
  direction: SecurityGroupRuleDirection;
  remoteEndpoint: RemoteEndpoint;
  source: string;
  destination: string;
  protocol: string;
  portRange: string | null;
}

export interface UpsertObservedResult {
  created: boolean;
}

@Injectable()
export class SecurityGroupRulesRepository {
  constructor(
    @InjectModel(SecurityGroupRule.name)
    private readonly ruleModel: Model<SecurityGroupRuleDocument>,
  ) {}

  findById(id: string): Promise<SecurityGroupRuleDocument | null> {
    return this.ruleModel.findById(id).exec();
  }

  /**
   * Shared by `findPaged` (portal listing) and `findAllFiltered` (export) —
   * only ever populated from validated, whitelisted DTO fields
   * (QuerySecurityGroupRulesDto), never raw client-supplied keys, to avoid
   * NoSQL operator injection (agent.md §6.7). Free text is regex-escaped
   * before use in a $regex (ReDoS-safe), same idiom as DevicesRepository.
   */
  private buildQuery(
    filter: SecurityGroupRulesFilter,
  ): Record<string, unknown> {
    const query: Record<string, unknown> = {};

    if (filter.securityGroupId) query.securityGroupId = filter.securityGroupId;
    if (filter.status) query.status = filter.status;

    if (filter.q) {
      const regex = { $regex: escapeRegex(filter.q), $options: 'i' };
      query.$or = [
        { securityGroupName: regex },
        { securityGroupId: regex },
        { ruleName: regex },
        { ruleId: regex },
        { source: regex },
        { destination: regex },
      ];
    }

    if (filter.createdFrom || filter.createdTo) {
      const createdAt: Record<string, Date> = {};
      if (filter.createdFrom) createdAt.$gte = new Date(filter.createdFrom);
      if (filter.createdTo) createdAt.$lte = new Date(filter.createdTo);
      query.createdAt = createdAt;
    }
    if (filter.reviewedFrom || filter.reviewedTo) {
      const reviewedAt: Record<string, Date> = {};
      if (filter.reviewedFrom) reviewedAt.$gte = new Date(filter.reviewedFrom);
      if (filter.reviewedTo) reviewedAt.$lte = new Date(filter.reviewedTo);
      query.reviewedAt = reviewedAt;
    }
    if (filter.authorizedFrom || filter.authorizedTo) {
      const authorizedAt: Record<string, Date> = {};
      if (filter.authorizedFrom) {
        authorizedAt.$gte = new Date(filter.authorizedFrom);
      }
      if (filter.authorizedTo)
        authorizedAt.$lte = new Date(filter.authorizedTo);
      query.authorizedAt = authorizedAt;
    }

    return query;
  }

  async findPaged(
    filter: SecurityGroupRulesFilter,
    sortBy: SecurityGroupRuleSortField,
    sortDir: SortDirection,
    page: number,
    limit: number,
  ): Promise<PagedResult<SecurityGroupRuleDocument>> {
    const query = this.buildQuery(filter);
    const sort: Record<string, 1 | -1> = {
      [sortBy]: sortDir === SortDirection.DESC ? -1 : 1,
    };

    const [items, total] = await Promise.all([
      this.ruleModel
        .find(query)
        .sort(sort)
        .skip((page - 1) * limit)
        .limit(limit)
        .exec(),
      this.ruleModel.countDocuments(query).exec(),
    ]);

    return { items, total, page, limit };
  }

  /**
   * Full filtered set, no pagination, used by export (RF-25) — the sort here
   * is ALWAYS group-id-then-rule-id regardless of the portal's current
   * column sort, per the user's explicit export-ordering requirement.
   */
  findAllFiltered(
    filter: SecurityGroupRulesFilter,
  ): Promise<SecurityGroupRuleDocument[]> {
    return this.ruleModel
      .find(this.buildQuery(filter))
      .sort({ securityGroupId: 1, ruleId: 1 })
      .exec();
  }

  /**
   * RF-27's in-portal "pending since the last run" indicator: total pending
   * count, plus how many of those were created no earlier than `since` (the
   * last non-`failure` sync run's `startedAt` — see
   * SecurityGroupSyncService.getSummary()). A tie-break on the exact
   * boundary timestamp only ever mildly over-counts by including the run's
   * own newly-inserted rows, which is the intended behavior, not an error.
   */
  async countPendingSummary(
    since: Date | null,
  ): Promise<{ total: number; sinceLastRun: number }> {
    const [total, sinceLastRun] = await Promise.all([
      this.ruleModel
        .countDocuments({ status: SecurityGroupRuleStatus.PENDIENTE })
        .exec(),
      since
        ? this.ruleModel
            .countDocuments({
              status: SecurityGroupRuleStatus.PENDIENTE,
              createdAt: { $gte: since },
            })
            .exec()
        : Promise.resolve(0),
    ]);
    return { total, sinceLastRun };
  }

  /** Distinct groups for the filter dropdown, alphabetical by name (RF-25). */
  async listDistinctGroups(): Promise<DistinctGroup[]> {
    const results = await this.ruleModel
      .aggregate<{ _id: string; securityGroupName: string }>([
        { $sort: { lastSeenAt: -1 } },
        {
          $group: {
            _id: '$securityGroupId',
            securityGroupName: { $first: '$securityGroupName' },
          },
        },
        { $sort: { securityGroupName: 1 } },
      ])
      .exec();

    return results.map((r) => ({
      securityGroupId: r._id,
      securityGroupName: r.securityGroupName,
    }));
  }

  /**
   * Upserts a rule the sync engine just observed from AWS. On first
   * observation, inserts a new `pendiente` record. On a repeat observation,
   * updates ONLY the AWS-derived fields — status, review fields, and
   * authorization fields are never touched here, so an in-progress or
   * completed review survives every subsequent sync (RF-22/CA-15).
   */
  async upsertObserved(
    observed: ObservedSecurityGroupRule,
  ): Promise<UpsertObservedResult> {
    const now = new Date();
    const naturalKey = {
      awsAccountId: observed.awsAccountId,
      region: observed.region,
      securityGroupId: observed.securityGroupId,
      ruleId: observed.ruleId,
    };

    const result = await this.ruleModel
      .updateOne(
        naturalKey,
        {
          $set: {
            vpcId: observed.vpcId,
            securityGroupName: observed.securityGroupName,
            attachedResources: observed.attachedResources,
            ruleName: observed.ruleName,
            direction: observed.direction,
            remoteEndpoint: observed.remoteEndpoint,
            source: observed.source,
            destination: observed.destination,
            protocol: observed.protocol,
            portRange: observed.portRange,
            lastSeenAt: now,
          },
          // A rule that reappears after being marked eliminado (should not
          // happen per ADR-0013's rule-ID-stability assumption, but handled
          // defensively) is revived as pendiente, never left eliminado.
          $setOnInsert: {
            ...naturalKey,
            status: SecurityGroupRuleStatus.PENDIENTE,
            createdAt: now,
            reviewObservation: null,
            reviewedAt: null,
            reviewedBy: null,
            authorizationObservation: null,
            authorizedAt: null,
            authorizedBy: null,
            deletedAt: null,
          },
        },
        { upsert: true },
      )
      .exec();

    return { created: result.upsertedCount > 0 };
  }

  /**
   * Marks as `eliminado` every non-eliminado rule within the given regions
   * that was NOT part of this run's observed set — the diff/upsert
   * algorithm's other half (ADR-0013/CA-15). Scoped to `regionsScanned` so a
   * partial run (e.g. one region excluded via AWS_SYNC_REGIONS, or a region
   * that errored and was skipped) never deletes rules outside what it
   * actually checked.
   */
  async markMissingAsDeleted(
    awsAccountId: string,
    regionsScanned: string[],
    seenRuleIds: string[],
  ): Promise<number> {
    const result = await this.ruleModel
      .updateMany(
        {
          awsAccountId,
          region: { $in: regionsScanned },
          ruleId: { $nin: seenRuleIds },
          status: { $ne: SecurityGroupRuleStatus.ELIMINADO },
        },
        {
          $set: {
            status: SecurityGroupRuleStatus.ELIMINADO,
            deletedAt: new Date(),
          },
        },
      )
      .exec();

    return result.modifiedCount;
  }

  async setReviewed(
    id: string,
    observation: string,
    reviewedBy: string,
  ): Promise<SecurityGroupRuleDocument | null> {
    return this.ruleModel
      .findOneAndUpdate(
        { _id: id, status: SecurityGroupRuleStatus.PENDIENTE },
        {
          $set: {
            status: SecurityGroupRuleStatus.REVISADO,
            reviewObservation: observation,
            reviewedAt: new Date(),
            reviewedBy: new Types.ObjectId(reviewedBy),
          },
        },
        { returnDocument: 'after' },
      )
      .exec();
  }

  async setAuthorized(
    id: string,
    observation: string,
    authorizedBy: string,
  ): Promise<SecurityGroupRuleDocument | null> {
    return this.ruleModel
      .findOneAndUpdate(
        { _id: id, status: SecurityGroupRuleStatus.REVISADO },
        {
          $set: {
            status: SecurityGroupRuleStatus.AUTORIZADO,
            authorizationObservation: observation,
            authorizedAt: new Date(),
            authorizedBy: new Types.ObjectId(authorizedBy),
          },
        },
        { returnDocument: 'after' },
      )
      .exec();
  }
}
