export default () => ({
  port: parseInt(process.env.PORT ?? '3000', 10),
  mongoUri: process.env.MONGO_URI,
  jwt: {
    accessSecret: process.env.JWT_ACCESS_SECRET,
    refreshSecret: process.env.JWT_REFRESH_SECRET,
    accessTtl: process.env.JWT_ACCESS_TTL ?? '15m',
    refreshTtl: process.env.JWT_REFRESH_TTL ?? '7d',
  },
  adminSeed: {
    username: process.env.ADMIN_SEED_USERNAME,
    email: process.env.ADMIN_SEED_EMAIL,
    password: process.env.ADMIN_SEED_PASSWORD,
  },
  loginRateLimit: {
    max: parseInt(process.env.LOGIN_RATE_LIMIT_MAX ?? '5', 10),
    windowSec: parseInt(process.env.LOGIN_RATE_LIMIT_WINDOW_SEC ?? '60', 10),
  },
  // General API-wide rate limit (agent.md §6.8) — deliberately separate from
  // loginRateLimit above: this one applies globally via APP_GUARD, the
  // login-specific one stays scoped to POST /auth/login. A request to login
  // is checked against both, so the stricter login limit still wins there.
  apiRateLimit: {
    max: parseInt(process.env.API_RATE_LIMIT_MAX ?? '100', 10),
    windowSec: parseInt(process.env.API_RATE_LIMIT_WINDOW_SEC ?? '60', 10),
  },
  retention: {
    inventoryDays: parseInt(process.env.INVENTORY_RETENTION_DAYS ?? '180', 10),
    accessEventsDays: parseInt(
      process.env.ACCESS_EVENTS_RETENTION_DAYS ?? '180',
      10,
    ),
    auditLogDays: parseInt(process.env.AUDIT_LOG_RETENTION_DAYS ?? '365', 10),
  },
  // Timezone the off_hours_access rule evaluates habitualHours in (agent.md
  // Assumption #6: configurable per installation, UTC until configured).
  habitualHoursTz: process.env.HABITUAL_HOURS_TZ ?? 'UTC',
});
