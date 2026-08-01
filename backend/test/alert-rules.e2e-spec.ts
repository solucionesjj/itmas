import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Alert rules (e2e)', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let adminToken: string;
  let usuarioToken: string;
  let auditorToken: string;
  let resourceChangeRuleId: string;
  let offHoursRuleId: string;

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

  it('rejects an unauthenticated request (401)', async () => {
    const res = await request(app.getHttpServer()).get('/api/v1/alert-rules');
    expect(res.status).toBe(401);
  });

  it('rejects Usuario and Auditor with 403 — Administrador-only endpoint', async () => {
    const asUsuario = await request(app.getHttpServer())
      .get('/api/v1/alert-rules')
      .set('Authorization', `Bearer ${usuarioToken}`);
    expect(asUsuario.status).toBe(403);

    const asAuditor = await request(app.getHttpServer())
      .get('/api/v1/alert-rules')
      .set('Authorization', `Bearer ${auditorToken}`);
    expect(asAuditor.status).toBe(403);
  });

  it('returns the two seeded default rules to an Administrador', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/alert-rules')
      .set('Authorization', `Bearer ${adminToken}`);

    expect(res.status).toBe(200);
    expect(res.body).toHaveLength(2);

    const resourceChange = res.body.find(
      (rule: { type: string }) => rule.type === 'resource_change',
    );
    const offHours = res.body.find(
      (rule: { type: string }) => rule.type === 'off_hours_access',
    );

    expect(resourceChange).toMatchObject({
      enabled: true,
      config: { resources: expect.arrayContaining(['cpu', 'ram', 'disks']) },
    });
    expect(offHours).toMatchObject({
      enabled: true,
      config: { habitualHours: { from: '07:00', to: '19:00' } },
    });

    resourceChangeRuleId = resourceChange._id;
    offHoursRuleId = offHours._id;
  });

  it('rejects creating a rule for a type that already exists (409)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/alert-rules')
      .set('Authorization', `Bearer ${adminToken}`)
      .send({
        type: 'resource_change',
        config: { resources: ['cpu'] },
      });

    expect(res.status).toBe(409);
  });

  it('rejects a PATCH whose config does not match the rule type (400)', async () => {
    const res = await request(app.getHttpServer())
      .patch(`/api/v1/alert-rules/${offHoursRuleId}`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ config: { resources: ['cpu'] } });

    expect(res.status).toBe(400);
  });

  it('allows an Administrador to disable a rule (CA-11)', async () => {
    const res = await request(app.getHttpServer())
      .patch(`/api/v1/alert-rules/${resourceChangeRuleId}`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ enabled: false });

    expect(res.status).toBe(200);
    expect(res.body.enabled).toBe(false);
  });

  it('rejects Usuario from modifying a rule (403)', async () => {
    const res = await request(app.getHttpServer())
      .patch(`/api/v1/alert-rules/${resourceChangeRuleId}`)
      .set('Authorization', `Bearer ${usuarioToken}`)
      .send({ enabled: true });

    expect(res.status).toBe(403);
  });
});
