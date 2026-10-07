/**
 * Build-time configuration for production builds.
 * PUBLIC VALUES ONLY: everything here is shipped to the browser. Never add secrets, tokens,
 * connection strings or identity-provider credentials (constitution §4).
 */
export const environment = {
  production: true,
  appName: 'ITMAS3',
  apiBaseUrl: '/api',
  authBaseUrl: '/auth',
};
