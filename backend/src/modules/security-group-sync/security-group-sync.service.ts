import { Injectable, OnModuleInit } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { SchedulerRegistry } from '@nestjs/schedule';
import { CronJob } from 'cron';
import { Types } from 'mongoose';
import {
  DescribeInstancesCommand,
  DescribeNetworkInterfacesCommand,
  DescribeRegionsCommand,
  DescribeSecurityGroupRulesCommand,
  DescribeSecurityGroupsCommand,
  EC2Client,
  Instance,
  NetworkInterface,
  SecurityGroup,
  SecurityGroupRule as AwsSecurityGroupRule,
} from '@aws-sdk/client-ec2';
import { AwsEc2ClientFactory } from './aws-ec2-client.factory';
import { AwsSyncRunDocument, GroupSyncResult } from './aws-sync-run.schema';
import { AwsSyncRunsRepository } from './aws-sync-runs.repository';
import { AwsSyncTriggerType } from './aws-sync-trigger-type.enum';
import { AwsSyncRunStatus } from './aws-sync-run-status.enum';
import {
  SecurityGroupRulesRepository,
  ObservedSecurityGroupRule,
} from '../security-group-rules/security-group-rules.repository';
import { AttachedResource } from '../security-group-rules/security-group-rule.schema';
import { SecurityGroupRuleDirection } from '../security-group-rules/security-group-rule-direction.enum';
import {
  deriveSourceDestination,
  mapRemoteEndpoint,
  normalizePortRange,
  normalizeProtocol,
} from './security-group-rule-normalizer.util';
import { AuditLogService } from '../audit-log/audit-log.service';
import { JsonLoggerService } from '../../common/logger/json-logger.service';

const DISCOVERY_REGION = 'us-east-1';
const CRON_JOB_NAME = 'aws-security-group-sync';

@Injectable()
export class SecurityGroupSyncService implements OnModuleInit {
  constructor(
    private readonly clientFactory: AwsEc2ClientFactory,
    private readonly rulesRepository: SecurityGroupRulesRepository,
    private readonly runsRepository: AwsSyncRunsRepository,
    private readonly auditLogService: AuditLogService,
    private readonly configService: ConfigService,
    private readonly schedulerRegistry: SchedulerRegistry,
    private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(SecurityGroupSyncService.name);
  }

  // ADR-0015: cron time is read from config at boot (never a static
  // @Cron() decorator, which can't read ConfigService) — same
  // "onModuleInit reads config" idiom already used for TTL indexes. The
  // automated run calls the exact same runSync() as the manual endpoint, so
  // a fix to the sync algorithm can never accidentally apply to only one
  // trigger path.
  onModuleInit(): void {
    const hour = this.configService.get<number>('aws.syncHour') ?? 2;
    const minute = this.configService.get<number>('aws.syncMinute') ?? 0;
    const cronTime = `${minute} ${hour} * * *`;

    const job = new CronJob(cronTime, () => {
      this.runSync(AwsSyncTriggerType.AUTOMATED).catch((error: unknown) => {
        this.logger.error('Automated AWS security group sync failed', {
          error: this.errorMessage(error),
        });
      });
    });
    this.schedulerRegistry.addCronJob(CRON_JOB_NAME, job);
    job.start();
  }

  async runSync(
    triggerType: AwsSyncTriggerType,
    triggeredBy?: string,
  ): Promise<AwsSyncRunDocument> {
    const startedAt = new Date();
    let regions: string[];
    try {
      regions = await this.resolveRegions();
    } catch (error) {
      // Total failure before a single region could even be listed (no AWS
      // credentials reachable, network unreachable, etc.) — this is the one
      // failure mode outside the per-region/per-group try/catch below, so it
      // needs its own handling to still satisfy RF-26/CA-20 ("every run gets
      // recorded") instead of an uncaught 500 with no run ever written.
      return this.recordFailedRun(triggerType, triggeredBy, startedAt, error);
    }
    const groupResults: GroupSyncResult[] = [];
    const vpcsChecked = new Set<string>();
    let awsAccountId: string | undefined;
    let rulesProcessed = 0;
    let rulesCreated = 0;
    let rulesMarkedDeleted = 0;

    for (const region of regions) {
      const client = this.clientFactory.create(region);
      let groups: SecurityGroup[];
      try {
        groups = await this.listSecurityGroups(client);
      } catch (error) {
        groupResults.push({
          region,
          vpcId: '',
          groupId: '',
          groupName: '',
          outcome: 'error',
          ruleCount: 0,
          errorMessage: `Failed to list security groups: ${this.errorMessage(error)}`,
        });
        continue;
      }

      const seenRuleIdsThisRegion: string[] = [];
      for (const group of groups) {
        const groupId = group.GroupId ?? '';
        const groupName = group.GroupName ?? '';
        awsAccountId ??= group.OwnerId;
        if (group.VpcId) vpcsChecked.add(group.VpcId);

        try {
          const [rules, attachedResources] = await Promise.all([
            this.listSecurityGroupRules(client, groupId),
            this.resolveAttachedResources(client, groupId),
          ]);

          for (const rule of rules) {
            const observed = this.toObservedRule(
              awsAccountId ?? '',
              region,
              group,
              rule,
              attachedResources,
            );
            const { created } =
              await this.rulesRepository.upsertObserved(observed);
            if (created) rulesCreated++;
            seenRuleIdsThisRegion.push(observed.ruleId);
          }

          rulesProcessed += rules.length;
          groupResults.push({
            region,
            vpcId: group.VpcId ?? '',
            groupId,
            groupName,
            outcome: 'success',
            ruleCount: rules.length,
          });
        } catch (error) {
          groupResults.push({
            region,
            vpcId: group.VpcId ?? '',
            groupId,
            groupName,
            outcome: 'error',
            ruleCount: 0,
            errorMessage: this.errorMessage(error),
          });
        }
      }

      // Only mark rules deleted for a region whose group list we actually
      // retrieved — a region-level failure above `continue`s before this
      // point, so its previously known rules are left untouched rather than
      // wrongly wiped out by an empty observed set (ADR-0013/CA-15).
      if (awsAccountId) {
        rulesMarkedDeleted += await this.rulesRepository.markMissingAsDeleted(
          awsAccountId,
          [region],
          seenRuleIdsThisRegion,
        );
      }
    }

    const groupsFailed = groupResults.filter(
      (r) => r.outcome === 'error',
    ).length;
    const status =
      groupResults.length === 0
        ? AwsSyncRunStatus.FAILURE
        : groupsFailed > 0
          ? AwsSyncRunStatus.PARTIAL_FAILURE
          : AwsSyncRunStatus.SUCCESS;

    const run = await this.runsRepository.create({
      triggerType,
      triggeredBy: triggeredBy ? new Types.ObjectId(triggeredBy) : null,
      startedAt,
      finishedAt: new Date(),
      status,
      regionsChecked: regions,
      vpcsChecked: [...vpcsChecked],
      groupResults,
      summary: {
        groupsProcessed: groupResults.length,
        groupsFailed,
        rulesProcessed,
        rulesCreated,
        rulesMarkedDeleted,
      },
    });

    this.logger.log('AWS security group sync run finished', {
      trigger: triggerType,
      status,
      groupsProcessed: groupResults.length,
      rulesProcessed,
    });
    await this.auditLogService.record(
      'sync_run',
      triggeredBy,
      (run._id as { toString(): string }).toString(),
      {
        trigger: triggerType,
        status,
        groupsProcessed: groupResults.length,
        rulesProcessed,
      },
    );

    return run;
  }

  listRuns(limit: number): Promise<AwsSyncRunDocument[]> {
    return this.runsRepository.findRecent(limit);
  }

  private async recordFailedRun(
    triggerType: AwsSyncTriggerType,
    triggeredBy: string | undefined,
    startedAt: Date,
    error: unknown,
  ): Promise<AwsSyncRunDocument> {
    const errorMessage = this.errorMessage(error);
    const run = await this.runsRepository.create({
      triggerType,
      triggeredBy: triggeredBy ? new Types.ObjectId(triggeredBy) : null,
      startedAt,
      finishedAt: new Date(),
      status: AwsSyncRunStatus.FAILURE,
      regionsChecked: [],
      vpcsChecked: [],
      groupResults: [],
      summary: {
        groupsProcessed: 0,
        groupsFailed: 0,
        rulesProcessed: 0,
        rulesCreated: 0,
        rulesMarkedDeleted: 0,
      },
    });

    this.logger.error(
      'AWS security group sync failed before any region could be scanned',
      { trigger: triggerType, error: errorMessage },
    );
    await this.auditLogService.record(
      'sync_run',
      triggeredBy,
      (run._id as { toString(): string }).toString(),
      {
        trigger: triggerType,
        status: AwsSyncRunStatus.FAILURE,
        error: errorMessage,
      },
    );

    return run;
  }

  /**
   * RF-27: the portal's "pending since the last run" indicator. Deliberately
   * NOT a calendar-day "yesterday" cutoff (which breaks near midnight) —
   * anchored to the last non-`failure` run's own `startedAt` instead, so it
   * stays correct regardless of exactly when the sync fired.
   */
  async getSummary(): Promise<{
    pendienteCount: number;
    pendienteSinceLastRun: number;
    lastRunAt: Date | null;
    lastRunStatus: AwsSyncRunStatus | null;
  }> {
    const lastRun = await this.runsRepository.findLatestNonFailure();
    const { total, sinceLastRun } =
      await this.rulesRepository.countPendingSummary(
        lastRun?.startedAt ?? null,
      );

    return {
      pendienteCount: total,
      pendienteSinceLastRun: sinceLastRun,
      lastRunAt: lastRun?.startedAt ?? null,
      lastRunStatus: lastRun?.status ?? null,
    };
  }

  private async resolveRegions(): Promise<string[]> {
    const override = this.configService.get<string[] | undefined>(
      'aws.syncRegions',
    );
    if (override && override.length > 0) {
      return override;
    }

    const client = this.clientFactory.create(DISCOVERY_REGION);
    const response = await client.send(
      new DescribeRegionsCommand({ AllRegions: false }),
    );
    return (response.Regions ?? [])
      .map((r) => r.RegionName)
      .filter((name): name is string => Boolean(name));
  }

  private async listSecurityGroups(
    client: EC2Client,
  ): Promise<SecurityGroup[]> {
    const groups: SecurityGroup[] = [];
    let nextToken: string | undefined;
    do {
      const response = await client.send(
        new DescribeSecurityGroupsCommand({ NextToken: nextToken }),
      );
      groups.push(...(response.SecurityGroups ?? []));
      nextToken = response.NextToken;
    } while (nextToken);
    return groups;
  }

  private async listSecurityGroupRules(
    client: EC2Client,
    groupId: string,
  ): Promise<AwsSecurityGroupRule[]> {
    const rules: AwsSecurityGroupRule[] = [];
    let nextToken: string | undefined;
    do {
      const response = await client.send(
        new DescribeSecurityGroupRulesCommand({
          Filters: [{ Name: 'group-id', Values: [groupId] }],
          NextToken: nextToken,
        }),
      );
      rules.push(...(response.SecurityGroupRules ?? []));
      nextToken = response.NextToken;
    } while (nextToken);
    return rules;
  }

  /**
   * Every EC2/RDS/ALB/NLB/Lambda-in-VPC resource attaches to a security
   * group via an ENI (agent.md/ADR-0013) — this is the one AWS call that
   * uniformly covers all of them. Only EC2 instances (via
   * `Attachment.InstanceId`) and load balancers (via `InterfaceType`) are
   * reliably classifiable from the ENI alone; anything else attached (e.g.
   * an RDS or Lambda ENI, which AWS exposes only as a generic `interface`
   * with a free-text `Description`) is recorded as `other` with that
   * description surfaced, rather than guessed at via brittle text matching.
   */
  private async resolveAttachedResources(
    client: EC2Client,
    groupId: string,
  ): Promise<AttachedResource[]> {
    const interfaces: NetworkInterface[] = [];
    let nextToken: string | undefined;
    do {
      const response = await client.send(
        new DescribeNetworkInterfacesCommand({
          Filters: [{ Name: 'group-id', Values: [groupId] }],
          NextToken: nextToken,
        }),
      );
      interfaces.push(...(response.NetworkInterfaces ?? []));
      nextToken = response.NextToken;
    } while (nextToken);

    const instanceIds = interfaces
      .map((ni) => ni.Attachment?.InstanceId)
      .filter((id): id is string => Boolean(id));
    const instanceNames = await this.resolveInstanceNames(client, instanceIds);

    const loadBalancerTypes = new Set([
      'load_balancer',
      'network_load_balancer',
      'gateway_load_balancer',
    ]);

    return interfaces.map((ni) => {
      const instanceId = ni.Attachment?.InstanceId;
      if (instanceId) {
        return {
          resourceType: 'ec2-instance',
          resourceId: instanceId,
          resourceName: instanceNames.get(instanceId),
        };
      }
      if (ni.InterfaceType && loadBalancerTypes.has(ni.InterfaceType)) {
        return {
          resourceType: 'load-balancer',
          resourceId: ni.NetworkInterfaceId ?? '',
          resourceName: ni.Description,
        };
      }
      return {
        resourceType: 'other',
        resourceId: ni.NetworkInterfaceId ?? '',
        resourceName: ni.Description,
      };
    });
  }

  private async resolveInstanceNames(
    client: EC2Client,
    instanceIds: string[],
  ): Promise<Map<string, string>> {
    const names = new Map<string, string>();
    if (instanceIds.length === 0) return names;

    const response = await client.send(
      new DescribeInstancesCommand({ InstanceIds: [...new Set(instanceIds)] }),
    );
    const instances: Instance[] = (response.Reservations ?? []).flatMap(
      (r) => r.Instances ?? [],
    );
    for (const instance of instances) {
      const nameTag = instance.Tags?.find((tag) => tag.Key === 'Name')?.Value;
      if (instance.InstanceId && nameTag) {
        names.set(instance.InstanceId, nameTag);
      }
    }
    return names;
  }

  private toObservedRule(
    awsAccountId: string,
    region: string,
    group: SecurityGroup,
    rule: AwsSecurityGroupRule,
    attachedResources: AttachedResource[],
  ): ObservedSecurityGroupRule {
    const direction = rule.IsEgress
      ? SecurityGroupRuleDirection.EGRESS
      : SecurityGroupRuleDirection.INGRESS;
    const remoteEndpoint = mapRemoteEndpoint({
      ruleId: rule.SecurityGroupRuleId ?? '',
      ipProtocol: rule.IpProtocol ?? '-1',
      fromPort: rule.FromPort,
      toPort: rule.ToPort,
      cidrIpv4: rule.CidrIpv4,
      cidrIpv6: rule.CidrIpv6,
      prefixListId: rule.PrefixListId,
      referencedGroupId: rule.ReferencedGroupInfo?.GroupId,
    });
    const securityGroupId = group.GroupId ?? '';
    const { source, destination } = deriveSourceDestination(
      direction,
      securityGroupId,
      remoteEndpoint,
    );

    return {
      awsAccountId,
      region,
      vpcId: group.VpcId ?? '',
      securityGroupId,
      securityGroupName: group.GroupName ?? '',
      attachedResources,
      ruleId: rule.SecurityGroupRuleId ?? '',
      ruleName: rule.Description ?? null,
      direction,
      remoteEndpoint,
      source,
      destination,
      protocol: normalizeProtocol(rule.IpProtocol ?? '-1'),
      portRange: normalizePortRange(rule.FromPort, rule.ToPort),
    };
  }

  private errorMessage(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
  }
}
