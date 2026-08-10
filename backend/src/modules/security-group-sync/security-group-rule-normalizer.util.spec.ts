import {
  deriveSourceDestination,
  mapRemoteEndpoint,
  normalizePortRange,
  normalizeProtocol,
} from './security-group-rule-normalizer.util';
import { SecurityGroupRuleDirection } from '../security-group-rules/security-group-rule-direction.enum';

describe('normalizeProtocol', () => {
  it('maps AWS\'s "-1" sentinel to "all"', () => {
    expect(normalizeProtocol('-1')).toBe('all');
  });

  it('lowercases and passes through named protocols', () => {
    expect(normalizeProtocol('TCP')).toBe('tcp');
    expect(normalizeProtocol('icmpv6')).toBe('icmpv6');
  });
});

describe('normalizePortRange', () => {
  it('returns null when both ports are undefined (all ports / all protocols)', () => {
    expect(normalizePortRange(undefined, undefined)).toBeNull();
  });

  it('renders a single port when from equals to', () => {
    expect(normalizePortRange(443, 443)).toBe('443');
  });

  it('renders a range when from differs from to', () => {
    expect(normalizePortRange(0, 65535)).toBe('0-65535');
  });
});

describe('mapRemoteEndpoint', () => {
  const base = { ruleId: 'sgr-1', ipProtocol: 'tcp' };

  it('maps a CIDR IPv4 endpoint', () => {
    expect(mapRemoteEndpoint({ ...base, cidrIpv4: '0.0.0.0/0' })).toEqual({
      kind: 'cidr_ipv4',
      value: '0.0.0.0/0',
    });
  });

  it('maps a CIDR IPv6 endpoint', () => {
    expect(mapRemoteEndpoint({ ...base, cidrIpv6: '::/0' })).toEqual({
      kind: 'cidr_ipv6',
      value: '::/0',
    });
  });

  it('maps a referenced-security-group endpoint', () => {
    expect(
      mapRemoteEndpoint({ ...base, referencedGroupId: 'sg-peer' }),
    ).toEqual({ kind: 'security_group', value: 'sg-peer' });
  });

  it('maps a prefix-list endpoint', () => {
    expect(mapRemoteEndpoint({ ...base, prefixListId: 'pl-1' })).toEqual({
      kind: 'prefix_list',
      value: 'pl-1',
    });
  });

  it('throws when the rule has no recognizable endpoint (AWS contract violation)', () => {
    expect(() => mapRemoteEndpoint(base)).toThrow(/sgr-1/);
  });
});

describe('deriveSourceDestination', () => {
  const endpoint = { kind: 'cidr_ipv4' as const, value: '10.0.0.0/8' };

  it('an ingress rule sources from the remote endpoint, destined at the group itself', () => {
    expect(
      deriveSourceDestination(
        SecurityGroupRuleDirection.INGRESS,
        'sg-1',
        endpoint,
      ),
    ).toEqual({ source: '10.0.0.0/8', destination: 'sg-1' });
  });

  it('an egress rule sources from the group itself, destined at the remote endpoint', () => {
    expect(
      deriveSourceDestination(
        SecurityGroupRuleDirection.EGRESS,
        'sg-1',
        endpoint,
      ),
    ).toEqual({ source: 'sg-1', destination: '10.0.0.0/8' });
  });
});
