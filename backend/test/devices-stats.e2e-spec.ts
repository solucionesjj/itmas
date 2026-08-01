import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Devices & Stats (e2e) — GET /devices, /stats/devices, /stats/os', () => {
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

    const devicesService = app.get(DevicesService);

    const deviceA = await devicesService.provision({
      hostname: 'PC-WIN-001',
      category: 'collaborator',
    });
    await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', deviceA.apiKey)
      .send({
        hostname: 'PC-WIN-001',
        category: 'collaborator',
        os: { name: 'Windows', version: '11' },
        cpu: { model: 'Intel i7', cores: 8 },
        ram: { totalGB: 16 },
        disks: [{ name: 'C', sizeGB: 512 }],
        timestamp: '2026-01-01T10:00:00.000Z',
      });

    const deviceB = await devicesService.provision({
      hostname: 'SRV-LINUX-001',
      category: 'infrastructure',
    });
    await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', deviceB.apiKey)
      .send({
        hostname: 'SRV-LINUX-001',
        category: 'infrastructure',
        os: { name: 'Linux', version: '22.04' },
        cpu: { model: 'Xeon', cores: 16 },
        ram: { totalGB: 64 },
        disks: [{ name: 'sda', sizeGB: 1024 }],
        timestamp: '2026-01-01T10:00:00.000Z',
      });

    // Never ingested — os stays unset, exercises the "unknown" stats bucket.
    await devicesService.provision({
      hostname: 'PC-NEW-002',
      category: 'collaborator',
    });
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects an unauthenticated request (401)', async () => {
    const res = await request(app.getHttpServer()).get('/api/v1/devices');
    expect(res.status).toBe(401);
  });

  it('allows all three roles (Administrador/Usuario/Auditor) to list devices', async () => {
    for (const token of [adminToken, usuarioToken, auditorToken]) {
      const res = await request(app.getHttpServer())
        .get('/api/v1/devices')
        .set('Authorization', `Bearer ${token}`);
      expect(res.status).toBe(200);
    }
  });

  it('lists and paginates all devices', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/devices?limit=2&page=1')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    expect(res.body.total).toBe(3);
    expect(res.body.items).toHaveLength(2);
    expect(res.body.page).toBe(1);
    expect(res.body.limit).toBe(2);
    expect(res.body.items[0]).not.toHaveProperty('apiKeyHash');
  });

  it('filters by category', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/devices?category=collaborator')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    expect(res.body.total).toBe(2);
  });

  it('filters by osName (case-insensitive partial match)', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/devices?osName=win')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    expect(res.body.total).toBe(1);
    expect(res.body.items[0].hostname).toBe('PC-WIN-001');
  });

  it('treats regex metacharacters in hostname search as literal text (no injection)', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/devices?hostname=' + encodeURIComponent('.*'))
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    // A literal ".*" substring matches none of the seeded hostnames — if the
    // metacharacters weren't escaped this would instead match everything.
    expect(res.body.total).toBe(0);
  });

  it('CA-04: GET /stats/devices returns counts by category', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/stats/devices')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    expect(res.body).toEqual({ total: 3, collaborator: 2, infrastructure: 1 });
  });

  it('CA-05: GET /stats/os groups by os.name, bucketing never-ingested devices as unknown', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/stats/os')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    expect(res.body).toEqual(
      expect.arrayContaining([
        { os: 'Windows', count: 1 },
        { os: 'Linux', count: 1 },
        { os: 'unknown', count: 1 },
      ]),
    );
    const totalAcrossBuckets = res.body.reduce(
      (sum: number, entry: { count: number }) => sum + entry.count,
      0,
    );
    expect(totalAcrossBuckets).toBe(3);
  });

  it('/stats endpoints are open to Usuario and Auditor too', async () => {
    for (const token of [usuarioToken, auditorToken]) {
      const devicesRes = await request(app.getHttpServer())
        .get('/api/v1/stats/devices')
        .set('Authorization', `Bearer ${token}`);
      expect(devicesRes.status).toBe(200);

      const osRes = await request(app.getHttpServer())
        .get('/api/v1/stats/os')
        .set('Authorization', `Bearer ${token}`);
      expect(osRes.status).toBe(200);
    }
  });
});
