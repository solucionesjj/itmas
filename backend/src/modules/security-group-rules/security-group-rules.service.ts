import {
  ConflictException,
  ForbiddenException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import {
  DistinctGroup,
  PagedResult,
  SecurityGroupRulesRepository,
} from './security-group-rules.repository';
import { SecurityGroupRuleDocument } from './security-group-rule.schema';
import { SecurityGroupRuleStatus } from './security-group-rule-status.enum';
import { SecurityGroupRuleSortField } from './security-group-rule-sort-field.enum';
import {
  QuerySecurityGroupRulesDto,
  SortDirection,
} from './dto/query-security-group-rules.dto';
import { ReviewSecurityGroupRuleDto } from './dto/review-security-group-rule.dto';
import { AuthorizeSecurityGroupRuleDto } from './dto/authorize-security-group-rule.dto';
import { ExportSecurityGroupRulesDto } from './dto/export-security-group-rules.dto';
import { ReportFormat } from '../reports/report-format.enum';
import { toCsv } from '../reports/csv.util';
import { buildPdfReport } from '../reports/pdf.util';

export interface SecurityGroupRulesExportFile {
  buffer: Buffer;
  contentType: string;
  filename: string;
}

const EXPORT_HEADERS = [
  'securityGroupName',
  'securityGroupId',
  'attachedResources',
  'ruleName',
  'ruleId',
  'source',
  'destination',
  'protocol',
  'portRange',
  'status',
  'createdAt',
];

@Injectable()
export class SecurityGroupRulesService {
  constructor(private readonly repository: SecurityGroupRulesRepository) {}

  findAll(
    query: QuerySecurityGroupRulesDto,
  ): Promise<PagedResult<SecurityGroupRuleDocument>> {
    return this.repository.findPaged(
      query,
      query.sortBy ?? SecurityGroupRuleSortField.SECURITY_GROUP_NAME,
      query.sortDir ?? SortDirection.ASC,
      query.page ?? 1,
      query.limit ?? 20,
    );
  }

  listGroups(): Promise<DistinctGroup[]> {
    return this.repository.listDistinctGroups();
  }

  /** RF-25: CSV/PDF export of the filtered catalog, group-id-then-rule-id sorted. */
  async generateExport(
    query: ExportSecurityGroupRulesDto,
  ): Promise<SecurityGroupRulesExportFile> {
    const rules = await this.repository.findAllFiltered(query);

    const rows = rules.map((rule) => [
      rule.securityGroupName,
      rule.securityGroupId,
      rule.attachedResources
        .map((r) => r.resourceName ?? r.resourceId)
        .join('; '),
      rule.ruleName ?? '',
      rule.ruleId,
      rule.source,
      rule.destination,
      rule.protocol,
      rule.portRange ?? '',
      rule.status,
      rule.createdAt.toISOString(),
    ]);

    const buffer =
      query.format === ReportFormat.CSV
        ? Buffer.from(toCsv(EXPORT_HEADERS, rows), 'utf-8')
        : await buildPdfReport(
            'Reglas de Security Groups AWS',
            EXPORT_HEADERS,
            rows,
          );

    return {
      buffer,
      contentType:
        query.format === ReportFormat.CSV
          ? 'text/csv; charset=utf-8'
          : 'application/pdf',
      filename: `security-group-rules-report.${query.format}`,
    };
  }

  /** Auditor marks a `pendiente` rule as `revisado` (RF-23/CA-16). */
  async review(
    id: string,
    dto: ReviewSecurityGroupRuleDto,
    reviewerId: string,
  ): Promise<SecurityGroupRuleDocument> {
    const existing = await this.repository.findById(id);
    if (!existing) {
      throw new NotFoundException(`No security group rule found with id ${id}`);
    }
    if (existing.status !== SecurityGroupRuleStatus.PENDIENTE) {
      throw new ConflictException(
        `Rule is "${existing.status}" — only a "pendiente" rule can be marked as revisado.`,
      );
    }

    const updated = await this.repository.setReviewed(
      id,
      dto.observation,
      reviewerId,
    );
    // Only reachable if another request changed the status between the
    // check above and this call (same vanishingly-unlikely-race handling as
    // AlertRulesService.update()).
    if (!updated) {
      throw new ConflictException(
        'Rule status changed before the review could be recorded — reload and retry.',
      );
    }

    return updated;
  }

  /**
   * Administrador marks a `revisado` rule as `autorizado` (RF-24/CA-17).
   * `@Roles(ADMINISTRATOR)` on the endpoint already makes it structurally
   * impossible for the Auditor who reviewed a rule to also authorize it
   * (this app's UserRole is one role per user) — the same-actor check below
   * is a defensive, currently-unreachable backstop that documents that
   * two-person-integrity intent directly in code (ADR-0013).
   */
  async authorize(
    id: string,
    dto: AuthorizeSecurityGroupRuleDto,
    authorizerId: string,
  ): Promise<SecurityGroupRuleDocument> {
    const existing = await this.repository.findById(id);
    if (!existing) {
      throw new NotFoundException(`No security group rule found with id ${id}`);
    }
    if (existing.status !== SecurityGroupRuleStatus.REVISADO) {
      throw new ConflictException(
        `Rule is "${existing.status}" — only a "revisado" rule can be marked as autorizado.`,
      );
    }
    if (existing.reviewedBy?.toString() === authorizerId) {
      throw new ForbiddenException(
        'The same user cannot both review and authorize a rule.',
      );
    }

    const updated = await this.repository.setAuthorized(
      id,
      dto.observation,
      authorizerId,
    );
    if (!updated) {
      throw new ConflictException(
        'Rule status changed before the authorization could be recorded — reload and retry.',
      );
    }

    return updated;
  }
}
