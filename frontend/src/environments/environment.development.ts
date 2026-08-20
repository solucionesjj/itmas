export const environment = {
  production: false,
  // Relative, exactly like environment.ts — the API is reached same-origin in dev
  // too. `ng serve` proxies /api to http://localhost:3000 via proxy.conf.json,
  // playing the role frontend/nginx.conf's `location /api/` plays in production.
  // An absolute cross-origin URL here cannot work: the backend enables no CORS
  // (it never needs to, being same-origin behind the reverse proxy), so the
  // preflight OPTIONS 404s and every request fails before it is sent.
  apiBaseUrl: '/api/v1'
};
