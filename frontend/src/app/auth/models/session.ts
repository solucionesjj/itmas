/** Session as reported by the Backend-for-Frontend. It never contains tokens. */
export interface SessionState {
  readonly isAuthenticated: boolean;
  readonly user: UserProfile | null;
  /** Root / Super Administrator: authorised for every operation. */
  readonly isRoot: boolean;
  /** Effective permissions (empty for Root). For UX only: the API authorises every request. */
  readonly permissions: readonly string[];
}

export interface UserProfile {
  readonly displayName: string;
  readonly username: string | null;
}

export const ANONYMOUS_SESSION: SessionState = {
  isAuthenticated: false,
  user: null,
  isRoot: false,
  permissions: [],
};
