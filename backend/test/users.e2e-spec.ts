import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Users (e2e)', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let adminToken: string;
  let adminId: string;
  let usuarioToken: string;

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
    const usuario = await usersRepository.create({
      username: 'usuario1',
      email: 'usuario1@itmas.local',
      passwordHash,
      role: UserRole.USER,
      active: true,
      mustChangePassword: false,
      tokenVersion: 0,
    });
    void usuario;

    const login = async (username: string, password: string) => {
      const res = await request(app.getHttpServer())
        .post('/api/v1/auth/login')
        .send({ username, password });
      return res.body as { accessToken: string; refreshToken: string };
    };

    const adminLogin = await login('admin', 'InitialAdmin1');
    adminToken = adminLogin.accessToken;
    const adminPayload = JSON.parse(
      Buffer.from(adminToken.split('.')[1], 'base64').toString('utf8'),
    );
    adminId = adminPayload.sub;

    usuarioToken = (await login('usuario1', 'TestPassword1')).accessToken;
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects an unauthenticated request (401)', async () => {
    const res = await request(app.getHttpServer()).get('/api/v1/users');
    expect(res.status).toBe(401);
  });

  it('rejects a non-Administrador with 403 on all three endpoints (CA-09)', async () => {
    const get = await request(app.getHttpServer())
      .get('/api/v1/users')
      .set('Authorization', `Bearer ${usuarioToken}`);
    expect(get.status).toBe(403);

    const post = await request(app.getHttpServer())
      .post('/api/v1/users')
      .set('Authorization', `Bearer ${usuarioToken}`)
      .send({
        username: 'x',
        email: 'x@itmas.local',
        password: 'Passw0rd1',
        role: 'user',
      });
    expect(post.status).toBe(403);

    const patch = await request(app.getHttpServer())
      .patch(`/api/v1/users/${adminId}`)
      .set('Authorization', `Bearer ${usuarioToken}`)
      .send({ email: 'new@itmas.local' });
    expect(patch.status).toBe(403);
  });

  let createdUserId: string;

  it('lets an Administrador create a user, who can then log in (CA-10)', async () => {
    const createRes = await request(app.getHttpServer())
      .post('/api/v1/users')
      .set('Authorization', `Bearer ${adminToken}`)
      .send({
        username: 'auditor1',
        email: 'auditor1@itmas.local',
        password: 'InitialPass1',
        role: 'auditor',
      });

    expect(createRes.status).toBe(201);
    expect(createRes.body.passwordHash).toBeUndefined();
    expect(createRes.body.tokenVersion).toBeUndefined();
    expect(createRes.body.mustChangePassword).toBe(true);
    createdUserId = createRes.body._id;

    const loginRes = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: 'auditor1', password: 'InitialPass1' });
    expect(loginRes.status).toBe(200);

    const payload = JSON.parse(
      Buffer.from(loginRes.body.accessToken.split('.')[1], 'base64').toString(
        'utf8',
      ),
    );
    expect(payload.role).toBe('auditor');
    expect(payload.mustChangePassword).toBe(true);
  });

  it('rejects creating a user with a duplicate username or email (409)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/users')
      .set('Authorization', `Bearer ${adminToken}`)
      .send({
        username: 'auditor1',
        email: 'someone-else@itmas.local',
        password: 'InitialPass1',
        role: 'user',
      });
    expect(res.status).toBe(409);
  });

  it('rejects an Administrador deactivating their own account (403)', async () => {
    const res = await request(app.getHttpServer())
      .patch(`/api/v1/users/${adminId}`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ active: false });
    expect(res.status).toBe(403);
  });

  it('rejects an Administrador demoting their own account (403)', async () => {
    const res = await request(app.getHttpServer())
      .patch(`/api/v1/users/${adminId}`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ role: 'user' });
    expect(res.status).toBe(403);
  });

  it("resets another user's password, forcing mustChangePassword and invalidating their refresh token", async () => {
    const login = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: 'auditor1', password: 'InitialPass1' });
    const staleRefreshToken = login.body.refreshToken as string;

    const patchRes = await request(app.getHttpServer())
      .patch(`/api/v1/users/${createdUserId}`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ password: 'BrandNewPass1' });
    expect(patchRes.status).toBe(200);
    expect(patchRes.body.mustChangePassword).toBe(true);

    const refreshAttempt = await request(app.getHttpServer())
      .post('/api/v1/auth/refresh')
      .send({ refreshToken: staleRefreshToken });
    expect(refreshAttempt.status).toBe(401);

    const oldPasswordLogin = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: 'auditor1', password: 'InitialPass1' });
    expect(oldPasswordLogin.status).toBe(401);

    const newPasswordLogin = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: 'auditor1', password: 'BrandNewPass1' });
    expect(newPasswordLogin.status).toBe(200);
  });

  it('deactivates a user, who can no longer log in (RF-12)', async () => {
    const patchRes = await request(app.getHttpServer())
      .patch(`/api/v1/users/${createdUserId}`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ active: false });
    expect(patchRes.status).toBe(200);
    expect(patchRes.body.active).toBe(false);

    const loginAttempt = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: 'auditor1', password: 'BrandNewPass1' });
    expect(loginAttempt.status).toBe(401);
  });

  it('lists users to an Administrador (CA-12 audit trail check)', async () => {
    const res = await request(app.getHttpServer())
      .get('/api/v1/users')
      .set('Authorization', `Bearer ${adminToken}`);
    expect(res.status).toBe(200);
    expect(Array.isArray(res.body)).toBe(true);
    expect(res.body.length).toBeGreaterThanOrEqual(3);
    expect(
      res.body.every((u: { passwordHash?: unknown }) => !u.passwordHash),
    ).toBe(true);
  });
});
