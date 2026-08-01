import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Alerts (e2e) — engine + RBAC', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let adminToken: string;
  let usuarioToken: string;
  let auditorToken: string;
  let apiKey: string;

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
    process.env.HABITUAL_HOURS_TZ = 'UTC';

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

    const devicesService = app.get(DevicesService);
    const provisioned = await devicesService.provision({
      hostname: 'SRV-001',
      category: 'infrastructure',
    });
    apiKey = provisioned.apiKey;
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('produces a resource_change alert from a real ingestion flow (CA-02)', async () => {
    const baseInventory = {
      hostname: 'SRV-001',
      category: 'infrastructure',
      os: { name: 'Linux', version: '22.04' },
      cpu: { model: 'Xeon', cores: 16 },
      ram: { totalGB: 32 },
      disks: [{ name: 'sda', sizeGB: 1024 }],
    };

    await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', apiKey)
      .send({ ...baseInventory, timestamp: '2026-01-01T10:00:00.000Z' });

    await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', apiKey)
      .send({
        ...baseInventory,
        ram: { totalGB: 64 },
        timestamp: '2026-01-01T11:00:00.000Z',
      });

    const res = await request(app.getHttpServer())
      .get('/api/v1/alerts?type=resource_change')
      .set('Authorization', `Bearer ${adminToken}`);

    expect(res.status).toBe(200);
    expect(res.body.items).toHaveLength(1);
    expect(res.body.items[0]).toMatchObject({
      type: 'resource_change',
      status: 'open',
      detail: { changes: ['ram'] },
    });
  });

  it('produces an off_hours_access alert for a server login outside habitual hours (CA-03)', async () => {
    await request(app.getHttpServer())
      .post('/api/v1/access-events')
      .set('X-Node-Api-Key', apiKey)
      .send({
        level: 'os',
        user: 'jdoe',
        action: 'login',
        timestamp: '2026-01-01T03:00:00.000Z', // 03:00 UTC — outside 07:00-19:00
      });

    const res = await request(app.getHttpServer())
      .get('/api/v1/alerts?type=off_hours_access')
      .set('Authorization', `Bearer ${adminToken}`);

    expect(res.status).toBe(200);
    expect(res.body.items).toHaveLength(1);
    expect(res.body.items[0]).toMatchObject({
      type: 'off_hours_access',
      status: 'open',
      detail: expect.objectContaining({ user: 'jdoe' }),
    });
  });

  it('rejects an unauthenticated request (401)', async () => {
    const res = await request(app.getHttpServer()).get('/api/v1/alerts');
    expect(res.status).toBe(401);
  });

  it('rejects Usuario (403) but allows Auditor (200) — CA-09/CA-14', async () => {
    const asUsuario = await request(app.getHttpServer())
      .get('/api/v1/alerts')
      .set('Authorization', `Bearer ${usuarioToken}`);
    expect(asUsuario.status).toBe(403);

    const asAuditor = await request(app.getHttpServer())
      .get('/api/v1/alerts')
      .set('Authorization', `Bearer ${auditorToken}`);
    expect(asAuditor.status).toBe(200);
  });

  it('paginates results (CA-07 filters + pagination)', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/alerts?limit=1&page=1')
      .set('Authorization', `Bearer ${adminToken}`);

    expect(res.status).toBe(200);
    expect(res.body.items).toHaveLength(1);
    expect(res.body.total).toBe(2);
    expect(res.body.page).toBe(1);
    expect(res.body.limit).toBe(1);
  });

  it('allows Auditor to update an alert status (CA-14) and rejects Usuario (CA-09)', async () => {
    const list = await request(app.getHttpServer())
      .get('/api/v1/alerts?type=resource_change')
      .set('Authorization', `Bearer ${adminToken}`);
    const alertId = list.body.items[0]._id;

    const asUsuario = await request(app.getHttpServer())
      .patch(`/api/v1/alerts/${alertId}`)
      .set('Authorization', `Bearer ${usuarioToken}`)
      .send({ status: 'reviewed' });
    expect(asUsuario.status).toBe(403);

    const asAuditor = await request(app.getHttpServer())
      .patch(`/api/v1/alerts/${alertId}`)
      .set('Authorization', `Bearer ${auditorToken}`)
      .send({ status: 'reviewed' });
    expect(asAuditor.status).toBe(200);
    expect(asAuditor.body.status).toBe('reviewed');
  });
});
