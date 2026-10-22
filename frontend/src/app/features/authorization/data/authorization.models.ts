/** Contracts of the authorization administration API (/api/v1, SPEC-0001 §12). */

export interface Paged<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly total: number;
}

export type UserStatusFilter = 'all' | 'active' | 'inactive' | 'without-policies';
export type PolicyStatusFilter = 'all' | 'active' | 'inactive';

export interface UserSummary {
  readonly id: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly isActive: boolean;
  readonly isRoot: boolean;
  readonly policyCount: number;
  readonly createdAt: string;
  readonly lastSignInAt: string | null;
}

export interface AssignedPolicy {
  readonly id: string;
  readonly name: string;
  readonly isActive: boolean;
  readonly assignedAt: string | null;
}

export interface UserDetail {
  readonly id: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly isActive: boolean;
  readonly isRoot: boolean;
  readonly createdAt: string;
  readonly lastSignInAt: string | null;
  readonly policies: readonly AssignedPolicy[];
}

export interface PolicyReference {
  readonly id: string;
  readonly name: string;
}

export interface EffectivePermissions {
  readonly userId: string;
  readonly isActive: boolean;
  readonly isRoot: boolean;
  readonly permissions: readonly {
    readonly permission: string;
    readonly grantedBy: readonly PolicyReference[];
  }[];
}

export interface UserReference {
  readonly id: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly isActive: boolean;
}

export interface PolicySummary {
  readonly id: string;
  readonly name: string;
  readonly description: string;
  readonly isActive: boolean;
  readonly permissionCount: number;
  readonly userCount: number;
  readonly updatedAt: string;
}

export interface PolicyDetail {
  readonly id: string;
  readonly name: string;
  readonly description: string;
  readonly isActive: boolean;
  readonly revision: number;
  readonly permissions: readonly string[];
  readonly userCount: number;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface PolicyInput {
  readonly name: string;
  readonly description: string;
  readonly isActive: boolean;
  readonly permissions: readonly string[];
}

export interface Permission {
  readonly name: string;
  readonly displayName: string;
  readonly description: string;
  readonly category: string;
  readonly isGrantable: boolean;
  readonly isDeprecated: boolean;
  readonly policyCount: number;
}

export interface PermissionMetadata {
  readonly displayName: string;
  readonly description: string;
  readonly category: string;
}

export interface AuditEvent {
  readonly id: string;
  readonly occurredAt: string;
  readonly actorUserId: string | null;
  readonly actorDisplayName: string;
  readonly action: string;
  readonly resourceType: string;
  readonly resourceId: string | null;
  readonly result: 'success' | 'rejected' | 'denied';
  readonly before: string | null;
  readonly after: string | null;
  readonly traceId: string | null;
}

export interface AuditFilter {
  readonly from?: string;
  readonly to?: string;
  readonly actorUserId?: string;
  readonly action?: string;
  readonly resourceType?: string;
  readonly resourceId?: string;
}

/** Spanish labels of audit actions (SPEC-0001 §14.3). */
export const AUDIT_ACTION_LABELS: Readonly<Record<string, string>> = {
  'user.provisioned': 'Usuario dado de alta',
  'user.activated': 'Usuario activado',
  'user.deactivated': 'Usuario desactivado',
  'user.policy.assigned': 'Política asignada',
  'user.policy.revoked': 'Política retirada',
  'root.bootstrapped': 'Root inicial asignado',
  'root.granted': 'Root concedido',
  'root.revoked': 'Root revocado',
  'policy.created': 'Política creada',
  'policy.updated': 'Política modificada',
  'policy.activated': 'Política activada',
  'policy.deactivated': 'Política desactivada',
  'policy.deleted': 'Política eliminada',
  'permission.updated': 'Permiso editado',
  'permission.synced': 'Catálogo sincronizado',
  // Device inventory (SPEC-0002, SPEC-0003)
  'device.created': 'Equipo registrado',
  'device.updated': 'Equipo modificado',
  'device.enrollment-code.regenerated': 'Código de equipo regenerado',
  'device.deleted': 'Equipo eliminado',
  'device.enrolled': 'Agente enrolado',
  'device.relinked': 'Agente reinstalado',
  'device.enrollment.rejected': 'Enrolamiento rechazado',
  'sql-audit.account.validated': 'Cuenta SQL validada',
  'sql-audit.account.unvalidated': 'Cuenta SQL desmarcada',
  'sql-audit.item.validated': 'Rol o permiso SQL validado',
  'sql-audit.item.unvalidated': 'Rol o permiso SQL desmarcado',
  // Infrastructure agenda (SPEC-0006 §12.2)
  'agenda.event.created': 'Actividad creada',
  'agenda.event.updated': 'Actividad modificada',
  'agenda.event.deleted': 'Actividad eliminada',
  'agenda.event.scheduled': 'Actividad programada',
  'agenda.event.cancelled': 'Actividad cancelada',
  'agenda.event.completed': 'Ejecución registrada',
  'agenda.event.completion-updated': 'Notas o evidencia modificadas',
  'agenda.event.tags-updated': 'Etiquetas modificadas',
  'agenda.series.created': 'Serie creada',
  'agenda.series.updated': 'Serie modificada',
  'agenda.event-type.created': 'Tipo de actividad creado',
  'agenda.event-type.updated': 'Tipo de actividad modificado',
  'agenda.event.outlook-published': 'Actividad publicada en Outlook',
  'agenda.event.outlook-failed': 'Falló la publicación en Outlook',
};

export const AUDIT_RESULT_LABELS: Readonly<Record<AuditEvent['result'], string>> = {
  success: 'Correcto',
  rejected: 'Rechazado',
  denied: 'Denegado',
};
