import { mockClient } from 'aws-sdk-client-mock';
import {
  DescribeInstancesCommand,
  DescribeNetworkInterfacesCommand,
  DescribeRegionsCommand,
  DescribeSecurityGroupRulesCommand,
  DescribeSecurityGroupsCommand,
  EC2Client,
} from '@aws-sdk/client-ec2';
import { Types } from 'mongoose';
import { SchedulerRegistry } from '@nestjs/schedule';
import { SecurityGroupSyncService } from './security-group-sync.service';
import { AwsEc2ClientFactory } from './aws-ec2-client.factory';
import { AwsSyncRunsRepository } from './aws-sync-runs.repository';
import { AwsSyncTriggerType } from './aws-sync-trigger-type.enum';
import { AwsSyncRunStatus } from './aws-sync-run-status.enum';
import { SecurityGroupRulesRepository } from '../security-group-rules/security-group-rules.repository';
import { AuditLogService } from '../audit-log/audit-log.service';
import { ConfigService } from '@nestjs/config';
import { JsonLoggerService } from '../../common/logger/json-logger.service';

const ec2Mock = mockClient(EC2Client);

describe('SecurityGroupSyncService', () => {
  let service: SecurityGroupSyncService;
  let rulesRepository: jest.Mocked<
    Pick<
      SecurityGroupRulesRepository,
      'upsertObserved' | 'markMissingAsDeleted' | 'countPendingSummary'
    >
  >;
  let runsRepository: jest.Mocked<
    Pick<
      AwsSyncRunsRepository,
      'create' | 'findRecent' | 'findLatestNonFailure'
    >
  >;
  let auditLogService: jest.Mocked<Pick<AuditLogService, 'record'>>;
  let configService: jest.Mocked<Pick<ConfigService, 'get'>>;
  let schedulerRegistry: jest.Mocked<Pick<SchedulerRegistry, 'addCronJob'>>;

  const triggeredBy = new Types.ObjectId().toString();

  beforeEach(() => {
    ec2Mock.reset();

    rulesRepository = {
      upsertObserved: jest.fn().mockResolvedValue({ created: true }),
      markMissingAsDeleted: jest.fn().mockResolvedValue(0),
      countPendingSummary: jest
        .fn()
        .mockResolvedValue({ total: 0, sinceLastRun: 0 }),
    };
    runsRepository = {
      create: jest.fn((data) =>
        Promise.resolve({ ...data, _id: new Types.ObjectId() } as never),
      ),
      findRecent: jest.fn(),
      findLatestNonFailure: jest.fn().mockResolvedValue(null),
    };
    auditLogService = { record: jest.fn().mockResolvedValue(undefined) };
    configService = { get: jest.fn().mockReturnValue(undefined) };
    schedulerRegistry = { addCronJob: jest.fn() };
    const logger = {
      setContext: jest.fn(),
      log: jest.fn(),
      error: jest.fn(),
    } as unknown as JsonLoggerService;

    service = new SecurityGroupSyncService(
      new AwsEc2ClientFactory(),
      rulesRepository as unknown as SecurityGroupRulesRepository,
      runsRepository as unknown as AwsSyncRunsRepository,
      auditLogService as unknown as AuditLogService,
      configService as unknown as ConfigService,
      schedulerRegistry as unknown as SchedulerRegistry,
      logger,
    );
  });

  it('discovers enabled regions and upserts every observed rule with derived fields', async () => {
    ec2Mock
      .on(DescribeRegionsCommand)
      .resolves({ Regions: [{ RegionName: 'us-east-1' }] });
    ec2Mock.on(DescribeSecurityGroupsCommand).resolves({
      SecurityGroups: [
        {
          GroupId: 'sg-1',
          GroupName: 'web',
          VpcId: 'vpc-1',
          OwnerId: '123456789012',
        },
      ],
    });
    ec2Mock.on(DescribeSecurityGroupRulesCommand).resolves({
      SecurityGroupRules: [
        {
          SecurityGroupRuleId: 'sgr-1',
          GroupId: 'sg-1',
          IsEgress: false,
          IpProtocol: 'tcp',
          FromPort: 443,
          ToPort: 443,
          CidrIpv4: '0.0.0.0/0',
          Description: 'HTTPS desde internet',
        },
      ],
    });
    ec2Mock
      .on(DescribeNetworkInterfacesCommand)
      .resolves({ NetworkInterfaces: [] });

    const run = await service.runSync(AwsSyncTriggerType.MANUAL, triggeredBy);

    expect(rulesRepository.upsertObserved).toHaveBeenCalledWith(
      expect.objectContaining({
        awsAccountId: '123456789012',
        region: 'us-east-1',
        securityGroupId: 'sg-1',
        ruleId: 'sgr-1',
        direction: 'ingress',
        source: '0.0.0.0/0',
        destination: 'sg-1',
        protocol: 'tcp',
        portRange: '443',
        ruleName: 'HTTPS desde internet',
      }),
    );
    expect(run.status).toBe(AwsSyncRunStatus.SUCCESS);
    expect(run.summary).toMatchObject({
      groupsProcessed: 1,
      groupsFailed: 0,
      rulesProcessed: 1,
      rulesCreated: 1,
    });
    expect(auditLogService.record).toHaveBeenCalledWith(
      'sync_run',
      triggeredBy,
      expect.any(String),
      expect.objectContaining({ trigger: AwsSyncTriggerType.MANUAL }),
    );
  });

  it('isolates a region-level failure as partial_failure without aborting other regions', async () => {
    ec2Mock.on(DescribeRegionsCommand).resolves({
      Regions: [{ RegionName: 'us-east-1' }, { RegionName: 'us-west-2' }],
    });
    ec2Mock
      .on(DescribeSecurityGroupsCommand)
      .rejectsOnce(new Error('region unreachable'))
      .resolves({
        SecurityGroups: [
          {
            GroupId: 'sg-2',
            GroupName: 'db',
            VpcId: 'vpc-2',
            OwnerId: '123456789012',
          },
        ],
      });
    ec2Mock
      .on(DescribeSecurityGroupRulesCommand)
      .resolves({ SecurityGroupRules: [] });
    ec2Mock
      .on(DescribeNetworkInterfacesCommand)
      .resolves({ NetworkInterfaces: [] });

    const run = await service.runSync(AwsSyncTriggerType.AUTOMATED);

    expect(run.status).toBe(AwsSyncRunStatus.PARTIAL_FAILURE);
    expect(run.groupResults).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ region: 'us-east-1', outcome: 'error' }),
        expect.objectContaining({
          region: 'us-west-2',
          groupId: 'sg-2',
          outcome: 'success',
        }),
      ]),
    );
    // The failed region never had an observed set — its prior rules must
    // not be touched (ADR-0013/CA-15).
    expect(rulesRepository.markMissingAsDeleted).not.toHaveBeenCalledWith(
      expect.anything(),
      ['us-east-1'],
      expect.anything(),
    );
    expect(rulesRepository.markMissingAsDeleted).toHaveBeenCalledWith(
      '123456789012',
      ['us-west-2'],
      [],
    );
  });

  it('isolates a single group-level failure within an otherwise-successful region', async () => {
    ec2Mock
      .on(DescribeRegionsCommand)
      .resolves({ Regions: [{ RegionName: 'us-east-1' }] });
    ec2Mock.on(DescribeSecurityGroupsCommand).resolves({
      SecurityGroups: [
        {
          GroupId: 'sg-bad',
          GroupName: 'broken',
          VpcId: 'vpc-1',
          OwnerId: '123456789012',
        },
        {
          GroupId: 'sg-ok',
          GroupName: 'fine',
          VpcId: 'vpc-1',
          OwnerId: '123456789012',
        },
      ],
    });
    ec2Mock
      .on(DescribeSecurityGroupRulesCommand, {
        Filters: [{ Name: 'group-id', Values: ['sg-bad'] }],
      })
      .rejects(new Error('rate limited'));
    ec2Mock
      .on(DescribeSecurityGroupRulesCommand, {
        Filters: [{ Name: 'group-id', Values: ['sg-ok'] }],
      })
      .resolves({ SecurityGroupRules: [] });
    ec2Mock
      .on(DescribeNetworkInterfacesCommand)
      .resolves({ NetworkInterfaces: [] });

    const run = await service.runSync(AwsSyncTriggerType.MANUAL);

    expect(run.status).toBe(AwsSyncRunStatus.PARTIAL_FAILURE);
    expect(run.groupResults).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          groupId: 'sg-bad',
          outcome: 'error',
          errorMessage: 'rate limited',
        }),
        expect.objectContaining({ groupId: 'sg-ok', outcome: 'success' }),
      ]),
    );
    // The whole region still contributes its successfully-observed group to
    // the deletion-diff, even though one of its groups errored.
    expect(rulesRepository.markMissingAsDeleted).toHaveBeenCalledWith(
      '123456789012',
      ['us-east-1'],
      [],
    );
  });

  it('resolves an attached EC2 instance by its Name tag', async () => {
    ec2Mock
      .on(DescribeRegionsCommand)
      .resolves({ Regions: [{ RegionName: 'us-east-1' }] });
    ec2Mock.on(DescribeSecurityGroupsCommand).resolves({
      SecurityGroups: [
        {
          GroupId: 'sg-1',
          GroupName: 'web',
          VpcId: 'vpc-1',
          OwnerId: '123456789012',
        },
      ],
    });
    ec2Mock
      .on(DescribeSecurityGroupRulesCommand)
      .resolves({ SecurityGroupRules: [] });
    ec2Mock.on(DescribeNetworkInterfacesCommand).resolves({
      NetworkInterfaces: [
        {
          NetworkInterfaceId: 'eni-1',
          Attachment: { InstanceId: 'i-abc123' },
        },
      ],
    });
    ec2Mock.on(DescribeInstancesCommand).resolves({
      Reservations: [
        {
          Instances: [
            {
              InstanceId: 'i-abc123',
              Tags: [{ Key: 'Name', Value: 'web-01' }],
            },
          ],
        },
      ],
    });

    await service.runSync(AwsSyncTriggerType.MANUAL);

    // No rules were returned this run, so upsertObserved isn't the
    // assertion point — attached-resource resolution is exercised via the
    // DescribeInstances call actually happening with the ENI's instance id.
    expect(
      ec2Mock.commandCalls(DescribeInstancesCommand)[0].args[0].input,
    ).toEqual(expect.objectContaining({ InstanceIds: ['i-abc123'] }));
  });

  it('classifies a load balancer ENI and falls back to "other" for anything else', async () => {
    ec2Mock
      .on(DescribeRegionsCommand)
      .resolves({ Regions: [{ RegionName: 'us-east-1' }] });
    ec2Mock.on(DescribeSecurityGroupsCommand).resolves({
      SecurityGroups: [
        {
          GroupId: 'sg-1',
          GroupName: 'web',
          VpcId: 'vpc-1',
          OwnerId: '123456789012',
        },
      ],
    });
    ec2Mock.on(DescribeSecurityGroupRulesCommand).resolves({
      SecurityGroupRules: [
        {
          SecurityGroupRuleId: 'sgr-1',
          IsEgress: false,
          IpProtocol: '-1',
          CidrIpv4: '0.0.0.0/0',
        },
      ],
    });
    ec2Mock.on(DescribeNetworkInterfacesCommand).resolves({
      NetworkInterfaces: [
        {
          NetworkInterfaceId: 'eni-lb',
          InterfaceType: 'network_load_balancer',
          Description: 'ELB app/my-alb/abc123',
        },
        {
          NetworkInterfaceId: 'eni-rds',
          InterfaceType: 'interface',
          Description: 'RDSNetworkInterface',
        },
      ],
    });

    await service.runSync(AwsSyncTriggerType.MANUAL);

    expect(rulesRepository.upsertObserved).toHaveBeenCalledWith(
      expect.objectContaining({
        attachedResources: [
          {
            resourceType: 'load-balancer',
            resourceId: 'eni-lb',
            resourceName: 'ELB app/my-alb/abc123',
          },
          {
            resourceType: 'other',
            resourceId: 'eni-rds',
            resourceName: 'RDSNetworkInterface',
          },
        ],
      }),
    );
  });

  it('records a failure run instead of throwing when AWS is unreachable before any region loads (e.g. no credentials)', async () => {
    ec2Mock
      .on(DescribeRegionsCommand)
      .rejects(new Error('Could not load credentials from any providers'));

    const run = await service.runSync(AwsSyncTriggerType.MANUAL, triggeredBy);

    expect(run.status).toBe(AwsSyncRunStatus.FAILURE);
    expect(run.groupResults).toEqual([]);
    expect(run.summary).toEqual({
      groupsProcessed: 0,
      groupsFailed: 0,
      rulesProcessed: 0,
      rulesCreated: 0,
      rulesMarkedDeleted: 0,
    });
    expect(runsRepository.create).toHaveBeenCalledWith(
      expect.objectContaining({ status: AwsSyncRunStatus.FAILURE }),
    );
    expect(auditLogService.record).toHaveBeenCalledWith(
      'sync_run',
      triggeredBy,
      expect.any(String),
      expect.objectContaining({
        status: AwsSyncRunStatus.FAILURE,
        error: 'Could not load credentials from any providers',
      }),
    );
  });

  it('uses the AWS_SYNC_REGIONS override instead of auto-discovering regions', async () => {
    configService.get.mockReturnValue(['eu-west-1']);
    ec2Mock.on(DescribeSecurityGroupsCommand).resolves({ SecurityGroups: [] });

    const run = await service.runSync(AwsSyncTriggerType.MANUAL);

    expect(ec2Mock.commandCalls(DescribeRegionsCommand)).toHaveLength(0);
    expect(run.regionsChecked).toEqual(['eu-west-1']);
  });

  describe('onModuleInit', () => {
    // `onModuleInit()` starts a real `CronJob` timer (SchedulerRegistry is
    // mocked here, but the job itself is real) — always stop it after
    // asserting, or the test process leaks an active timer.
    afterEach(() => {
      for (const [, job] of schedulerRegistry.addCronJob.mock.calls) {
        (job as { stop: () => void }).stop();
      }
    });

    it('registers a daily cron job at the configured hour/minute (ADR-0015)', () => {
      configService.get.mockImplementation((key: string) =>
        key === 'aws.syncHour' ? 7 : key === 'aws.syncMinute' ? 30 : undefined,
      );

      service.onModuleInit();

      expect(schedulerRegistry.addCronJob).toHaveBeenCalledTimes(1);
      const [name, job] = schedulerRegistry.addCronJob.mock.calls[0];
      expect(name).toBe('aws-security-group-sync');
      expect(job.cronTime.source).toBe('30 7 * * *');
    });

    it('defaults to 02:00 when no schedule is configured', () => {
      service.onModuleInit();

      const [, job] = schedulerRegistry.addCronJob.mock.calls[0];
      expect(job.cronTime.source).toBe('0 2 * * *');
    });
  });

  describe('getSummary', () => {
    it('anchors "since last run" to the latest non-failure run, not a calendar cutoff', async () => {
      const lastRunStartedAt = new Date('2026-08-09T02:00:00.000Z');
      runsRepository.findLatestNonFailure.mockResolvedValue({
        startedAt: lastRunStartedAt,
        status: AwsSyncRunStatus.SUCCESS,
      } as never);
      rulesRepository.countPendingSummary.mockResolvedValue({
        total: 12,
        sinceLastRun: 3,
      });

      const summary = await service.getSummary();

      expect(rulesRepository.countPendingSummary).toHaveBeenCalledWith(
        lastRunStartedAt,
      );
      expect(summary).toEqual({
        pendienteCount: 12,
        pendienteSinceLastRun: 3,
        lastRunAt: lastRunStartedAt,
        lastRunStatus: AwsSyncRunStatus.SUCCESS,
      });
    });

    it('returns nulls when no run has ever succeeded', async () => {
      runsRepository.findLatestNonFailure.mockResolvedValue(null);
      rulesRepository.countPendingSummary.mockResolvedValue({
        total: 5,
        sinceLastRun: 0,
      });

      const summary = await service.getSummary();

      expect(rulesRepository.countPendingSummary).toHaveBeenCalledWith(null);
      expect(summary.lastRunAt).toBeNull();
      expect(summary.lastRunStatus).toBeNull();
    });
  });
});
