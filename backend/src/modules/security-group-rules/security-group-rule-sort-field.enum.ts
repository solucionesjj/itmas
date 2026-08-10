/**
 * Allowlist of columns the catalog can be sorted by (RF-25: "ordenar por
 * cualquier columna") — the client sends one of these string values, never a
 * raw field name, so `sortBy` can safely reach a Mongo `.sort()` call without
 * risking operator injection via an attacker-chosen key.
 */
export enum SecurityGroupRuleSortField {
  SECURITY_GROUP_NAME = 'securityGroupName',
  SECURITY_GROUP_ID = 'securityGroupId',
  RULE_NAME = 'ruleName',
  RULE_ID = 'ruleId',
  SOURCE = 'source',
  DESTINATION = 'destination',
  PROTOCOL = 'protocol',
  PORT_RANGE = 'portRange',
  STATUS = 'status',
  CREATED_AT = 'createdAt',
  REVIEWED_AT = 'reviewedAt',
  AUTHORIZED_AT = 'authorizedAt',
  REGION = 'region',
}
