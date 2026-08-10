import { RemoteEndpoint } from '../security-group-rules/security-group-rule.schema';
import { SecurityGroupRuleDirection } from '../security-group-rules/security-group-rule-direction.enum';

/**
 * The subset of AWS's `SecurityGroupRule` (from `DescribeSecurityGroupRules`)
 * these pure functions need — decoupled from the AWS SDK's own type so they
 * can be unit-tested without constructing real SDK response objects. The
 * sync service maps the live SDK response into this shape before calling in.
 */
export interface RawSecurityGroupRule {
  ruleId: string;
  ipProtocol: string;
  fromPort?: number;
  toPort?: number;
  cidrIpv4?: string;
  cidrIpv6?: string;
  prefixListId?: string;
  referencedGroupId?: string;
}

/** AWS uses `-1` for "all protocols" — everything else passes through as-is. */
export function normalizeProtocol(ipProtocol: string): string {
  return ipProtocol === '-1' ? 'all' : ipProtocol.toLowerCase();
}

/**
 * `undefined` fromPort/toPort means "all ports" (e.g. protocol `all`, or an
 * ICMP rule using `-1`/`-1` for "all types/codes") — rendered as `null`
 * rather than a misleading `"undefined-undefined"` string. A single port
 * (`fromPort === toPort`) renders as just that number, not a trivial range.
 */
export function normalizePortRange(
  fromPort?: number,
  toPort?: number,
): string | null {
  if (fromPort === undefined || toPort === undefined) {
    return null;
  }
  return fromPort === toPort ? String(fromPort) : `${fromPort}-${toPort}`;
}

/**
 * AWS guarantees exactly one of CidrIpv4/CidrIpv6/ReferencedGroupInfo/
 * PrefixListId is set per rule — if the SDK ever returns none (a contract
 * violation on AWS's side, or an untested new rule shape), fail loudly
 * rather than silently persist a rule with a fabricated endpoint.
 */
export function mapRemoteEndpoint(rule: RawSecurityGroupRule): RemoteEndpoint {
  if (rule.cidrIpv4) return { kind: 'cidr_ipv4', value: rule.cidrIpv4 };
  if (rule.cidrIpv6) return { kind: 'cidr_ipv6', value: rule.cidrIpv6 };
  if (rule.referencedGroupId) {
    return { kind: 'security_group', value: rule.referencedGroupId };
  }
  if (rule.prefixListId)
    return { kind: 'prefix_list', value: rule.prefixListId };
  throw new Error(
    `Security group rule ${rule.ruleId} has no recognizable remote endpoint`,
  );
}

/**
 * ADR-0013's directional mapping: an ingress rule only names its source (the
 * remote endpoint); an egress rule only names its destination. The local
 * side — "this security group's own protected resources," which AWS never
 * names as an endpoint — reuses the group's own id rather than a fabricated
 * placeholder.
 */
export function deriveSourceDestination(
  direction: SecurityGroupRuleDirection,
  securityGroupId: string,
  remoteEndpoint: RemoteEndpoint,
): { source: string; destination: string } {
  return direction === SecurityGroupRuleDirection.INGRESS
    ? { source: remoteEndpoint.value, destination: securityGroupId }
    : { source: securityGroupId, destination: remoteEndpoint.value };
}
