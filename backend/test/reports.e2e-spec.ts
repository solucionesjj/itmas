import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Reports (e2e) — GET /reports/export', () => {
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

    // Seed a device with an inventory (for the devices report) and produce a
    // real resource_change alert via two ingestions (for the alerts report) —
    // same approach as alerts.e2e-spec.ts, exercising the real flow end to end
    // rather than writing straight to the alerts collection.
    const devicesService = app.get(DevicesService);
    const provisioned = await devicesService.provision({
      hostname: 'SRV-001',
      category: 'infrastructure',
    });

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
      .set('X-Node-Api-Key', provisioned.apiKey)
      .send({ ...baseInventory, timestamp: '2026-01-01T10:00:00.000Z' });
    await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', provisioned.apiKey)
      .send({
        ...baseInventory,
        ram: { totalGB: 64 },
        timestamp: '2026-01-01T11:00:00.000Z',
      });
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects an unauthenticated request (401)', async () => {
    const res = await request(app.getHttpServer()).get(
      '/api/v1/reports/export?reportType=devices&format=csv',
    );
    expect(res.status).toBe(401);
  });

  it('rejects an invalid reportType/format (400)', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/reports/export?reportType=bogus&format=csv')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(400);
  });

  it('CA-13: Usuario exports the devices report as CSV', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/reports/export?reportType=devices&format=csv')
      .set('Authorization', `Bearer ${usuarioToken}`);

    expect(res.status).toBe(200);
    expect(res.headers['content-type']).toContain('text/csv');
    expect(res.headers['content-disposition']).toContain('devices-report.csv');
    expect(res.text).toContain('hostname,category,os.name,os.version,lastSeen');
    expect(res.text).toContain('SRV-001');
  });

  it('Usuario can also export the devices report as PDF', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/reports/export?reportType=devices&format=pdf')
      .set('Authorization', `Bearer ${usuarioToken}`)
      .buffer(true)
      .parse((response, callback) => {
        const chunks: Buffer[] = [];
        response.on('data', (chunk: Buffer) => chunks.push(chunk));
        response.on('end', () => callback(null, Buffer.concat(chunks)));
      });

    expect(res.status).toBe(200);
    expect(res.headers['content-type']).toContain('application/pdf');
    expect((res.body as Buffer).length).toBeGreaterThan(100);
  });

  it('Usuario is forbidden from exporting the alerts report (403)', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/reports/export?reportType=alerts&format=csv')
      .set('Authorization', `Bearer ${usuarioToken}`);
    expect(res.status).toBe(403);
  });

  it('Administrador and Auditor can export the alerts report', async () => {
    for (const token of [adminToken, auditorToken]) {
      const res = await request(app.getHttpServer())
        .get('/api/v1/reports/export?reportType=alerts&format=csv')
        .set('Authorization', `Bearer ${token}`);
      expect(res.status).toBe(200);
      expect(res.text).toContain('resource_change');
    }
  });

  it('filters the devices report by category', async () => {
    const res = await request(app.getHttpServer())
      .get(
        '/api/v1/reports/export?reportType=devices&format=csv&category=collaborator',
      )
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    // Only the seeded infrastructure device exists — filtering to
    // collaborator should leave just the header row.
    expect(res.text.trim().split('\r\n')).toHaveLength(1);
  });
});
