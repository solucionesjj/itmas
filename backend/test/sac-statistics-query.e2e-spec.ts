import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

/** BL-032 CA-11. */
describe('SAC statistics query and export (e2e)', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let adminToken: string;
  let usuarioToken: string;
  let auditorToken: string;

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
    await usersRepository.create({
      username: 'usuario1',
      email: 'usuario1@itmas.local',
      passwordHash,
      role: UserRole.USER,
      active: true,
      mustChangePassword: false,
      tokenVersion: 0,
    });
    await usersRepository.create({
      username: 'auditor1',
      email: 'auditor1@itmas.local',
      passwordHash,
      role: UserRole.AUDITOR,
      active: true,
      mustChangePassword: false,
      tokenVersion: 0,
    });

    const login = async (username: string, password: string) => {
      const res = await request(app.getHttpServer())
        .post('/api/v1/auth/login')
        .send({ username, password });
      return res.body.accessToken as string;
    };
    adminToken = await login('admin', 'InitialAdmin1');
    usuarioToken = await login('usuario1', 'TestPassword1');
    auditorToken = await login('auditor1', 'TestPassword1');

    // Seed through the real ingestion endpoint, so the data under test went
    // through the same validation and Decimal128 conversion production uses.
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
          generatedAt: '2026-01-15T04:00:00.000Z',
          totalSizeGb: 100,
          activeAccounts: 500,
          balanceSum: '1000.55',
        },
        {
          databaseName: 'DBSAC_Acme',
          generatedAt: '2026-02-15T04:00:00.000Z',
          totalSizeGb: 120,
          activeAccounts: 600,
          balanceSum: '2000.55',
        },
        {
          databaseName: 'DBSAC_Globex',
          generatedAt: '2026-02-16T04:00:00.000Z',
          totalSizeGb: 900,
          activeAccounts: 50,
          balanceSum: '3000.00',
        },
      ]);
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  describe('GET /sac-statistics', () => {
    it('rejects an unauthenticated request (401)', async () => {
      const res = await request(app.getHttpServer()).get(
        '/api/v1/sac-statistics',
      );

      expect(res.status).toBe(401);
    });

    it.each([
      ['Administrador', () => adminToken],
      ['Usuario', () => usuarioToken],
      ['Auditor', () => auditorToken],
    ])('allows a %s (CA-3)', async (_role, token) => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics')
        .set('Authorization', `Bearer ${token()}`);

      expect(res.status).toBe(200);
      expect(res.body.total).toBe(3);
    });

    it('returns the standard paginated envelope', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?page=1&limit=2')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body).toMatchObject({ total: 3, page: 1, limit: 2 });
      expect(res.body.items).toHaveLength(2);
    });

    it('sorts by generatedAt descending by default', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics')
        .set('Authorization', `Bearer ${adminToken}`);

      const dates = res.body.items.map(
        (item: { generatedAt: string }) => item.generatedAt,
      );
      expect(dates).toEqual([...dates].sort().reverse());
    });

    it('serialises the financial sums as strings, not BSON $numberDecimal (CA/ADR-0018)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?databaseName=globex')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body.items[0].balanceSum).toBe('3000.00');
    });

    it('never exposes the raw Mongo document shape (no __v)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body.items[0].__v).toBeUndefined();
    });

    it('filters databaseName partially and case-insensitively', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?databaseName=acme')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body.total).toBe(2);
    });

    it('treats regex metacharacters in databaseName as literals, not a pattern', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?databaseName=.%2A')
        .set('Authorization', `Bearer ${adminToken}`);

      // An unescaped `.*` would have matched everything.
      expect(res.body.total).toBe(0);
    });

    it('filters by the generatedAt range', async () => {
      const res = await request(app.getHttpServer())
        .get(
          '/api/v1/sac-statistics?from=2026-02-01T00:00:00.000Z&to=2026-02-28T00:00:00.000Z',
        )
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body.total).toBe(2);
    });

    it('sorts on a whitelisted field when asked', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?sort=totalSizeGb&order=desc')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.body.items[0].totalSizeGb).toBe(900);
    });

    it('rejects a sort field outside the whitelist (400)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?sort=deviceId')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
    });

    it('rejects an unknown query parameter (400)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?nombreBase=acme')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
    });

    it('rejects a limit above the cap (400)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/sac-statistics?limit=1000')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
    });
  });

  describe('GET /reports/export?reportType=sac-statistics', () => {
    it('rejects an unauthenticated request (401)', async () => {
      const res = await request(app.getHttpServer()).get(
        '/api/v1/reports/export?reportType=sac-statistics&format=csv',
      );

      expect(res.status).toBe(401);
    });

    it('streams a CSV with the 15 contract headers and every filtered row', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/reports/export?reportType=sac-statistics&format=csv')
        .set('Authorization', `Bearer ${adminToken}`)
        .buffer(true)
        .parse((response, callback) => {
          const chunks: Buffer[] = [];
          response.on('data', (chunk: Buffer) => chunks.push(chunk));
          response.on('end', () => callback(null, Buffer.concat(chunks)));
        });

      expect(res.status).toBe(200);
      expect(res.headers['content-type']).toContain('text/csv');
      expect(res.headers['content-disposition']).toContain(
        'sac-statistics-report.csv',
      );

      const lines = (res.body as Buffer).toString('utf-8').trim().split('\r\n');
      expect(lines[0].split(',')).toHaveLength(15);
      // Header + all three rows (no pagination — CA-6).
      expect(lines).toHaveLength(4);
      // Newest first.
      expect(lines[1]).toContain('DBSAC_Globex');
      // Exact cents, not a rounded double.
      expect(lines[1]).toContain('3000.00');
    });

    it('respects the same filters as the query endpoint', async () => {
      const res = await request(app.getHttpServer())
        .get(
          '/api/v1/reports/export?reportType=sac-statistics&format=csv&databaseName=acme',
        )
        .set('Authorization', `Bearer ${adminToken}`)
        .buffer(true)
        .parse((response, callback) => {
          const chunks: Buffer[] = [];
          response.on('data', (chunk: Buffer) => chunks.push(chunk));
          response.on('end', () => callback(null, Buffer.concat(chunks)));
        });

      const lines = (res.body as Buffer).toString('utf-8').trim().split('\r\n');
      expect(lines).toHaveLength(3);
      expect((res.body as Buffer).toString('utf-8')).not.toContain('Globex');
    });

    it('streams a real xlsx package for format=xlsx', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/reports/export?reportType=sac-statistics&format=xlsx')
        .set('Authorization', `Bearer ${adminToken}`)
        .buffer(true)
        .parse((response, callback) => {
          const chunks: Buffer[] = [];
          response.on('data', (chunk: Buffer) => chunks.push(chunk));
          response.on('end', () => callback(null, Buffer.concat(chunks)));
        });

      expect(res.status).toBe(200);
      expect(res.headers['content-type']).toContain('spreadsheetml.sheet');
      // An .xlsx is a ZIP container.
      expect((res.body as Buffer).subarray(0, 2).toString('ascii')).toBe('PK');
    });

    it('rejects format=pdf for this report type (400) with an explicit reason', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/reports/export?reportType=sac-statistics&format=pdf')
        .set('Authorization', `Bearer ${adminToken}`);

      expect(res.status).toBe(400);
      expect(res.body.error.message).toContain('15 columns');
    });

    it('is available to a Usuario — general consultation data, unlike the alerts report', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/reports/export?reportType=sac-statistics&format=csv')
        .set('Authorization', `Bearer ${usuarioToken}`);

      expect(res.status).toBe(200);
    });

    it('is available to an Auditor', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/reports/export?reportType=sac-statistics&format=csv')
        .set('Authorization', `Bearer ${auditorToken}`);

      expect(res.status).toBe(200);
    });

    it('makes format=xlsx work for the pre-existing report types too (CA-4)', async () => {
      const res = await request(app.getHttpServer())
        .get('/api/v1/reports/export?reportType=devices&format=xlsx')
        .set('Authorization', `Bearer ${usuarioToken}`)
        .buffer(true)
        .parse((response, callback) => {
          const chunks: Buffer[] = [];
          response.on('data', (chunk: Buffer) => chunks.push(chunk));
          response.on('end', () => callback(null, Buffer.concat(chunks)));
        });

      expect(res.status).toBe(200);
      expect((res.body as Buffer).subarray(0, 2).toString('ascii')).toBe('PK');
    });
  });
});
