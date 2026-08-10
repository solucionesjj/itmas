import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Device provisioning & key rotation (e2e) — POST /devices, POST /devices/:id/rotate-key', () => {
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
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects an unauthenticated request on both endpoints (401)', async () => {
    const createRes = await request(app.getHttpServer())
      .post('/api/v1/devices')
      .send({ hostname: 'PC-NOAUTH', category: 'collaborator' });
    expect(createRes.status).toBe(401);

    const rotateRes = await request(app.getHttpServer()).post(
      '/api/v1/devices/some-id/rotate-key',
    );
    expect(rotateRes.status).toBe(401);
  });

  it('rejects Usuario and Auditor with 403 on both endpoints — Administrador-only', async () => {
    for (const token of [usuarioToken, auditorToken]) {
      const createRes = await request(app.getHttpServer())
        .post('/api/v1/devices')
        .set('Authorization', `Bearer ${token}`)
        .send({ hostname: 'PC-FORBIDDEN', category: 'collaborator' });
      expect(createRes.status).toBe(403);

      const rotateRes = await request(app.getHttpServer())
        .post('/api/v1/devices/some-id/rotate-key')
        .set('Authorization', `Bearer ${token}`);
      expect(rotateRes.status).toBe(403);
    }
  });

  let deviceId: string;
  let firstApiKey: string;

  it('lets an Administrador create a device and returns its plaintext apiKey exactly once (201)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/devices')
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ hostname: 'PC-PORTAL-001', category: 'collaborator' });

    expect(res.status).toBe(201);
    expect(res.body).toMatchObject({
      hostname: 'PC-PORTAL-001',
      category: 'collaborator',
    });
    expect(typeof res.body.deviceId).toBe('string');
    expect(typeof res.body.apiKey).toBe('string');
    // Key format is `<deviceId>.<secret>` — verify the deviceId prefix and a
    // non-empty secret half, rather than a tautological self-comparison.
    expect(res.body.apiKey.startsWith(`${res.body.deviceId}.`)).toBe(true);
    const secretPart: string = res.body.apiKey.slice(
      (res.body.deviceId as string).length + 1,
    );
    expect(secretPart.length).toBeGreaterThan(0);
    expect(res.body.apiKeyHash).toBeUndefined();

    deviceId = res.body.deviceId;
    firstApiKey = res.body.apiKey;
  });

  it('rejects a device creation payload with an empty hostname (400)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/devices')
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ hostname: '', category: 'collaborator' });
    expect(res.status).toBe(400);
  });

  it('rejects a device creation payload with an invalid category (400)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/devices')
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ hostname: 'PC-BAD-CATEGORY', category: 'not-a-real-category' });
    expect(res.status).toBe(400);
  });

  it('the newly issued apiKey actually authenticates a real ingestion request', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', firstApiKey)
      .send({
        hostname: 'PC-PORTAL-001',
        category: 'collaborator',
        os: { name: 'Windows', version: '11' },
        cpu: { model: 'Intel i7', cores: 8 },
        ram: { totalGB: 16 },
        disks: [{ name: 'C', sizeGB: 512 }],
        timestamp: '2026-01-01T10:00:00.000Z',
      });
    expect(res.status).toBe(201);
  });

  it('never exposes apiKey/apiKeyHash through GET /devices for a portal-created device', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/devices?hostname=PC-PORTAL-001')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    expect(res.body.items).toHaveLength(1);
    expect(res.body.items[0]).not.toHaveProperty('apiKey');
    expect(res.body.items[0]).not.toHaveProperty('apiKeyHash');
  });

  it('returns 404 rotating the key of a device that does not exist', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/devices/does-not-exist/rotate-key')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(404);
  });

  let secondApiKey: string;

  it('lets an Administrador rotate an existing device key (200), invalidating the old one', async () => {
    const res = await request(app.getHttpServer())
      .post(`/api/v1/devices/${deviceId}/rotate-key`)
      .set('Authorization', `Bearer ${adminToken}`);

    expect(res.status).toBe(200);
    expect(res.body).toEqual({ deviceId, apiKey: expect.any(String) });
    expect(res.body.apiKey).not.toBe(firstApiKey);
    secondApiKey = res.body.apiKey;

    // Old key no longer authenticates.
    const staleAttempt = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', firstApiKey)
      .send({
        hostname: 'PC-PORTAL-001',
        category: 'collaborator',
        os: { name: 'Windows', version: '11' },
        cpu: { model: 'Intel i7', cores: 8 },
        ram: { totalGB: 16 },
        disks: [{ name: 'C', sizeGB: 512 }],
        timestamp: '2026-01-02T10:00:00.000Z',
      });
    expect(staleAttempt.status).toBe(401);

    // New key authenticates successfully.
    const freshAttempt = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', secondApiKey)
      .send({
        hostname: 'PC-PORTAL-001',
        category: 'collaborator',
        os: { name: 'Windows', version: '11' },
        cpu: { model: 'Intel i7', cores: 8 },
        ram: { totalGB: 16 },
        disks: [{ name: 'C', sizeGB: 512 }],
        timestamp: '2026-01-02T10:00:00.000Z',
      });
    expect(freshAttempt.status).toBe(201);
  });
});
