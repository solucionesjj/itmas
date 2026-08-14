import {
  ConflictException,
  ForbiddenException,
  NotFoundException,
} from '@nestjs/common';
import { Types } from 'mongoose';
import { SecurityGroupRulesService } from './security-group-rules.service';
import { SecurityGroupRulesRepository } from './security-group-rules.repository';
import { SecurityGroupRuleStatus } from './security-group-rule-status.enum';
import { SecurityGroupRuleDirection } from './security-group-rule-direction.enum';
import { SecurityGroupRuleSortField } from './security-group-rule-sort-field.enum';
import { SortDirection } from './dto/query-security-group-rules.dto';
import { ReportFormat } from '../reports/report-format.enum';

describe('SecurityGroupRulesService', () => {
  let service: SecurityGroupRulesService;
  let repository: jest.Mocked<
    Pick<
      SecurityGroupRulesRepository,
      | 'findById'
      | 'findPaged'
      | 'findAllFiltered'
      | 'listDistinctGroups'
      | 'setReviewed'
      | 'setAuthorized'
    >
  >;

  const reviewerId = new Types.ObjectId().toString();
  const authorizerId = new Types.ObjectId().toString();

  const buildRule = (overrides: Record<string, unknown> = {}) => ({
    _id: new Types.ObjectId(),
    awsAccountId: '123456789012',
    region: 'us-east-1',
    vpcId: 'vpc-1',
    securityGroupId: 'sg-1',
    securityGroupName: 'web-servers',
    attachedResources: [
      {
        resourceType: 'ec2-instance',
        resourceId: 'i-1',
        resourceName: 'web-01',
      },
    ],
    ruleId: 'sgr-1',
    ruleName: 'HTTPS',
    direction: SecurityGroupRuleDirection.INGRESS,
    remoteEndpoint: { kind: 'cidr_ipv4', value: '0.0.0.0/0' },
    source: '0.0.0.0/0',
    destination: 'sg-1',
    protocol: 'tcp',
    portRange: '443',
    status: SecurityGroupRuleStatus.PENDIENTE,
    createdAt: new Date('2026-08-01T00:00:00.000Z'),
    reviewedBy: null,
    ...overrides,
  });

  beforeEach(() => {
    repository = {
      findById: jest.fn(),
      findPaged: jest.fn(),
      findAllFiltered: jest.fn(),
      listDistinctGroups: jest.fn(),
      setReviewed: jest.fn(),
      setAuthorized: jest.fn(),
    };
    service = new SecurityGroupRulesService(
      repository as unknown as SecurityGroupRulesRepository,
    );
  });

  describe('findAll', () => {
    it('applies default sort/pagination when the query omits them', async () => {
      repository.findPaged.mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        limit: 20,
      });

      await service.findAll({});

      expect(repository.findPaged).toHaveBeenCalledWith(
        {},
        SecurityGroupRuleSortField.SECURITY_GROUP_NAME,
        SortDirection.ASC,
        1,
        20,
      );
    });
  });

  describe('review', () => {
    it('throws NotFoundException for a missing rule', async () => {
      repository.findById.mockResolvedValue(null);

      await expect(
        service.review('missing-id', { observation: 'ok' }, reviewerId),
      ).rejects.toBeInstanceOf(NotFoundException);
    });

    it('throws ConflictException when the rule is not pendiente (CA-16)', async () => {
      repository.findById.mockResolvedValue(
        buildRule({ status: SecurityGroupRuleStatus.AUTORIZADO }) as never,
      );

      await expect(
        service.review('id', { observation: 'ok' }, reviewerId),
      ).rejects.toBeInstanceOf(ConflictException);
    });

    it('marks a pendiente rule as revisado (RF-23)', async () => {
      const existing = buildRule();
      const reviewed = buildRule({
        status: SecurityGroupRuleStatus.REVISADO,
        reviewObservation: 'looks fine',
        reviewedBy: new Types.ObjectId(reviewerId),
      });
      repository.findById.mockResolvedValue(existing as never);
      repository.setReviewed.mockResolvedValue(reviewed as never);

      const result = await service.review(
        'id',
        { observation: 'looks fine' },
        reviewerId,
      );

      expect(repository.setReviewed).toHaveBeenCalledWith(
        'id',
        'looks fine',
        reviewerId,
      );
      expect(result.status).toBe(SecurityGroupRuleStatus.REVISADO);
    });
  });

  describe('authorize', () => {
    it('throws NotFoundException for a missing rule', async () => {
      repository.findById.mockResolvedValue(null);

      await expect(
        service.authorize('missing-id', { observation: 'ok' }, authorizerId),
      ).rejects.toBeInstanceOf(NotFoundException);
    });

    it('throws ConflictException when the rule is not revisado (CA-17)', async () => {
      repository.findById.mockResolvedValue(
        buildRule({ status: SecurityGroupRuleStatus.PENDIENTE }) as never,
      );

      await expect(
        service.authorize('id', { observation: 'ok' }, authorizerId),
      ).rejects.toBeInstanceOf(ConflictException);
    });

    it('throws ForbiddenException when the authorizer already reviewed it', async () => {
      repository.findById.mockResolvedValue(
        buildRule({
          status: SecurityGroupRuleStatus.REVISADO,
          reviewedBy: new Types.ObjectId(authorizerId),
        }) as never,
      );

      await expect(
        service.authorize('id', { observation: 'ok' }, authorizerId),
      ).rejects.toBeInstanceOf(ForbiddenException);
    });

    it('marks a revisado rule as autorizado (RF-24)', async () => {
      const existing = buildRule({
        status: SecurityGroupRuleStatus.REVISADO,
        reviewedBy: new Types.ObjectId(reviewerId),
      });
      const authorized = buildRule({
        status: SecurityGroupRuleStatus.AUTORIZADO,
        authorizationObservation: 'approved',
      });
      repository.findById.mockResolvedValue(existing as never);
      repository.setAuthorized.mockResolvedValue(authorized as never);

      const result = await service.authorize(
        'id',
        { observation: 'approved' },
        authorizerId,
      );

      expect(repository.setAuthorized).toHaveBeenCalledWith(
        'id',
        'approved',
        authorizerId,
      );
      expect(result.status).toBe(SecurityGroupRuleStatus.AUTORIZADO);
    });
  });

  describe('generateExport', () => {
    it('produces a CSV sorted export with the expected header row', async () => {
      repository.findAllFiltered.mockResolvedValue([buildRule() as never]);

      const result = await service.generateExport({ format: ReportFormat.CSV });

      const text = result.buffer.toString('utf-8');
      expect(text.split('\r\n')[0]).toBe(
        'securityGroupName,securityGroupId,attachedResources,ruleName,ruleId,source,destination,protocol,portRange,status,createdAt',
      );
      expect(text).toContain('sg-1');
      expect(result.filename).toBe('security-group-rules-report.csv');
    });

    it('produces a non-trivial PDF buffer', async () => {
      repository.findAllFiltered.mockResolvedValue([buildRule() as never]);

      const result = await service.generateExport({ format: ReportFormat.PDF });

      expect(result.contentType).toBe('application/pdf');
      expect(result.buffer.subarray(0, 4).toString('ascii')).toBe('%PDF');
    });
  });
});
