import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

/** BL-033 CA-3/CA-9. */
describe('SAC analytics endpoints (e2e)', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let adminToken: string;
  let usuarioToken: string;
  let auditorToken: string;

  const monthsBack = (back: number) => {
    const now = new Date();
    return new Date(
      Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - back, 15, 4),
    ).toISOString();
  };

  beforeAll(async () => {
    mongod = await MongoMemoryServer.create();
    process.env.MONGO_URI = mongod.getUri();
    process.env.PORT = '3001';
    process.env.JWT_ACCESS_SECRET = 'test-access-secret';
    process.env.JWT_REFRESH_SECRET = 'test-refresh-secret';
    process.env.JWT_ACCESS_TTL = '15m';
    process.env.JWT_REFRESH_TTL = '7d';
    process.env.ADMIN_SEED_USERNAME = 'admin';
    process.env.ADMIN_SEED_EMAIL = 'admin@itmas.local';
    process.env.ADMIN_SEED_PASSWORD = 'InitialAdmin1';
    process.env.LOGIN_RATE_LIMIT_MAX = '1000';
    process.env.LOGIN_RATE_LIMIT_WINDOW_SEC = '60';

    /* eslint-disable @typescript-eslint/no-require-imports */
    const { AppModule } = require('../src/app.module');
    const {
      UsersRepository,
    } = require('../src/modules/users/users.repository');
    const { UserRole } = require('../src/modules/users/user-role.enum');
    const {
      DevicesService,
    } = require('../src/modules/devices/devices.service');
    /* eslint-enable @typescript-eslint/no-require-imports */

    const moduleRef: TestingModule = await Test.createTestingModule({
      imports: [AppModule],
    }).compile();

    app = moduleRef.createNestApplication();
    const logger = await app.resolve(JsonLoggerService);
    logger.setContext('Bootstrap');
    app.useGlobalPipes(
      new ValidationPipe({
        whitelist: true,
        forbidNonWhitelisted: true,
        transform: true,
      }),
    );
    app.useGlobalFilters(new AllExceptionsFilter(logger));
    app.setGlobalPrefix('api/v1');
    await app.init();

    const usersRepository = app.get(UsersRepository);
    const passwordHash = await argon2.hash('TestPassword1');
    for (const [username, role] of [
      ['usuario1', UserRole.USER],
      ['auditor1', UserRole.AUDITOR],
    ] as [string, string][]) {
      await usersRepository.create({
        username,
        email: `${username}@itmas.local`,
        passwordHash,
        role,
        active: true,
        mustChangePassword: false,
        tokenVersion: 0,
      });
    }

    const login = async (username: string, password: string) => {
      const res = await request(app.getHttpServer())
        .post('/api/v1/auth/login')
        .send({ username, password });
      return res.body.accessToken as string;
    };
    adminToken = await login('admin', 'InitialAdmin1');
    usuarioToken = await login('usuario1', 'TestPassword1');
    auditorToken = await login('auditor1', 'TestPassword1');

    const provisioned = await app.get(DevicesService).provision({
      hostname: 'SQL-SAC-01',
      category: 'infrastructure',
    });
    await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', provisioned.apiKey)
      .send([
        {
          databaseName: 'DBSAC_Acme',
          generatedAt: monthsBack(2),
          totalSizeGb: 100,
          balanceSum: '1000.00',
        },
        {
          databaseName: 'DBSAC_Acme',
          generatedAt: monthsBack(1),
          totalSizeGb: 110,
          balanceSum: '1100.00',
        },
        {
          databaseName: 'DBSAC_Acme',
          generatedAt: monthsBack(0),
          totalSizeGb: 120,
          balanceSum: '1210.00',
        },
        {
          databaseName: 'DBSAC_Globex',
          generatedAt: monthsBack(0),
          totalSizeGb: 900,
          balanceSum: '9000.00',
        },
      ]);
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  describe('GET /stats/sac/ranking', () => {
    it('rejects an unauthenticated request (401)', async () => {
      const res = await request(app.getHttpServer()).get(
        '/api/v1/stats/sac/ranking?metric=totalSizeGb',
      );

      expect(res.status).toBe(401);
    });

    it.each([
      ['Administrador', () => adminToken],
      ['Usuario', () => usuarioToken],
      ['Auditor', () => auditorToken],
    ])('allows a %s (CA-9)', async (_role, token) => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/ranking?metric=totalSizeGb')
        .set('Authorization', `Bearer ${token()}`);

      expect(res.status).toBe(200);
    });

    it('ranks largest first from each database latest snapshot', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/ranking?metric=totalSizeGb')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(
        res.body.entries.map((e: { databaseName: string }) => e.databaseName),
      ).toEqual(['DBSAC_Globex', 'DBSAC_Acme']);
      expect(res.body.entries[1].value).toBe(120);
    });

    it('serialises a financial metric as a string, not a number', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/ranking?metric=balanceSum')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body.entries[0].value).toBe('9000.00');
    });

    it('honours an as-of date', async () => {
      const res = await request(app.getHttpServer())
        .get(`/api/v1/stats/sac/ranking?metric=totalSizeGb&at=${monthsBack(1)}`)
        .set('Authorization', `Bearer ${adminToken}`);

      // Globex had not reported yet at that point.
      expect(res.body.entries).toHaveLength(1);
      expect(res.body.entries[0].value).toBe(110);
    });

    it('rejects a metric outside the whitelist (400) — never interpolated (CA-3)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/ranking?metric=deviceId')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
    });

    it('rejects a missing metric (400)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/ranking')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
    });
  });

  describe('GET /stats/sac/growth', () => {
    it('rejects an unauthenticated request (401)', async () => {
      const res = await request(app.getHttpServer()).get(
        '/api/v1/stats/sac/growth?metric=totalSizeGb',
      );

      expect(res.status).toBe(401);
    });

    it('returns the dense month grid and one series per database', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=totalSizeGb&months=3')
        .set('Authorization', `Bearer ${usuarioToken}`);

      expect(res.status).toBe(200);
      expect(res.body.months).toHaveLength(3);
      expect(res.body.series).toHaveLength(2);
      for (const entry of res.body.series) {
        expect(entry.series.map((p: { month: string }) => p.month)).toEqual(
          res.body.months,
        );
      }
    });

    it('reports value, absolute delta and percentage per month', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=totalSizeGb&months=3')
        .set('Authorization', `Bearer ${adminToken}`);

      const acme = res.body.series.find(
        (s: { databaseName: string }) => s.databaseName === 'DBSAC_Acme',
      );
      expect(acme.series[2]).toMatchObject({
        value: 120,
        delta: 10,
        deltaPercent: 9.09,
      });
    });

    it('leaves a month with no snapshot as no data (CA-5)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=totalSizeGb&months=3')
        .set('Authorization', `Bearer ${adminToken}`);

      const globex = res.body.series.find(
        (s: { databaseName: string }) => s.databaseName === 'DBSAC_Globex',
      );
      expect(globex.series[0].value).toBeNull();
      expect(globex.series[0].delta).toBeNull();
      expect(globex.series[2].value).toBe(900);
      // Nothing to compare against, so no variation is invented.
      expect(globex.series[2].delta).toBeNull();
    });

    it('keeps a financial metric exact as strings, including the delta (CA-7)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=balanceSum&months=3')
        .set('Authorization', `Bearer ${adminToken}`);

      const acme = res.body.series.find(
        (s: { databaseName: string }) => s.databaseName === 'DBSAC_Acme',
      );
      expect(acme.series[2].value).toBe('1210.00');
      expect(acme.series[2].delta).toBe('110.00');
      expect(acme.series[2].deltaPercent).toBe(10);
    });

    it('narrows to one database when asked', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=totalSizeGb&databaseName=acme')
        .set('Authorization', `Bearer ${auditorToken}`);

      expect(res.body.series).toHaveLength(1);
      expect(res.body.series[0].databaseName).toBe('DBSAC_Acme');
    });

    it('defaults to a twelve-month window', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=totalSizeGb')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body.months).toHaveLength(12);
    });

    it('rejects a window beyond the cap (400)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=totalSizeGb&months=999')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
    });

    it('accepts activitiesLast30Days — the ninth whitelisted metric (CA-3)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=activitiesLast30Days')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(200);
    });

    it('rejects a metric outside the whitelist (400)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/stats/sac/growth?metric=$where')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
    });
  });

  describe('GET /stats/sac/databases', () => {
    it('lists each database once, alphabetically, for all three roles', async () => {
      for (const token of [adminToken, usuarioToken, auditorToken]) {
        const res = await request(app.getHttpServer())
          .get('/api/v1/stats/sac/databases')
          .set('Authorization', `Bearer ${token}`);

        expect(res.status).toBe(200);
        expect(res.body).toEqual(['DBSAC_Acme', 'DBSAC_Globex']);
      }
    });

    it('rejects an unauthenticated request (401)', async () => {
      const res = await request(app.getHttpServer()).get(
        '/api/v1/stats/sac/databases',
      );

      expect(res.status).toBe(401);
    });
  });
});
