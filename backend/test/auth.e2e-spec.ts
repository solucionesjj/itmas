import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Auth (e2e)', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;

  const adminUsername = 'admin';
  const adminPassword = 'InitialAdmin1';

  beforeAll(async () => {
    mongod = await MongoMemoryServer.create();
    process.env.MONGO_URI = mongod.getUri();
    process.env.PORT = '3001';
    process.env.JWT_ACCESS_SECRET = 'test-access-secret';
    process.env.JWT_REFRESH_SECRET = 'test-refresh-secret';
    process.env.JWT_ACCESS_TTL = '15m';
    process.env.JWT_REFRESH_TTL = '7d';
    process.env.ADMIN_SEED_USERNAME = adminUsername;
    process.env.ADMIN_SEED_EMAIL = 'admin@itmas.local';
    process.env.ADMIN_SEED_PASSWORD = adminPassword;
    // Generous limit here: functional flows, not the rate-limit test itself.
    process.env.LOGIN_RATE_LIMIT_MAX = '1000';
    process.env.LOGIN_RATE_LIMIT_WINDOW_SEC = '60';

    // Deferred require: AppModule's decorator eagerly reads process.env via
    // ConfigModule.forRoot's validate()/load(), so it must only be
    // evaluated after the env vars above are set (a top-level static import
    // would run before beforeAll, and native dynamic import() isn't
    // supported under this CommonJS ts-jest setup).
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const { AppModule } = require('../src/app.module');

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
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects a request to a protected endpoint without a token (CA-06)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/auth/logout')
      .send();

    expect(res.status).toBe(401);
    expect(res.body.error).toBeDefined();
    expect(res.body.error.requestId).toBeDefined();
    expect(res.body.error.message).not.toMatch(/at\s.+\(.+:\d+:\d+\)/); // no stack trace leak
  });

  it('rejects login with wrong credentials without revealing whether the user exists', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: adminUsername, password: 'wrong-password' });

    expect(res.status).toBe(401);
    expect(res.body.error.code).toBe('UNAUTHORIZED');
  });

  it('logs in the seeded Administrator and reports mustChangePassword=true', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: adminUsername, password: adminPassword });

    expect(res.status).toBe(200);
    expect(res.body.accessToken).toBeDefined();
    expect(res.body.refreshToken).toBeDefined();

    const payload = JSON.parse(
      Buffer.from(res.body.accessToken.split('.')[1], 'base64').toString(
        'utf8',
      ),
    );
    expect(payload.mustChangePassword).toBe(true);
    expect(payload.role).toBe('administrator');
  });

  it('rejects malformed login payloads with 400', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({});
    expect(res.status).toBe(400);
  });

  it('rotates the access/refresh pair on refresh while the tokenVersion is unchanged', async () => {
    const login = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: adminUsername, password: adminPassword });

    const refreshRes = await request(app.getHttpServer())
      .post('/api/v1/auth/refresh')
      .send({ refreshToken: login.body.refreshToken });

    expect(refreshRes.status).toBe(200);
    expect(refreshRes.body.accessToken).toBeDefined();
    expect(refreshRes.body.refreshToken).toBeDefined();
  });

  it('invalidates outstanding refresh tokens on logout', async () => {
    const login = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: adminUsername, password: adminPassword });

    const logoutRes = await request(app.getHttpServer())
      .post('/api/v1/auth/logout')
      .set('Authorization', `Bearer ${login.body.accessToken}`)
      .send();
    expect(logoutRes.status).toBe(204);

    const refreshAfterLogout = await request(app.getHttpServer())
      .post('/api/v1/auth/refresh')
      .send({ refreshToken: login.body.refreshToken });
    expect(refreshAfterLogout.status).toBe(401);
  });

  it('changes the password, clears mustChangePassword, and invalidates old refresh tokens', async () => {
    const login = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: adminUsername, password: adminPassword });

    const changeRes = await request(app.getHttpServer())
      .post('/api/v1/auth/change-password')
      .set('Authorization', `Bearer ${login.body.accessToken}`)
      .send({ currentPassword: adminPassword, newPassword: 'BrandNewPass1' });
    expect(changeRes.status).toBe(204);

    const oldRefresh = await request(app.getHttpServer())
      .post('/api/v1/auth/refresh')
      .send({ refreshToken: login.body.refreshToken });
    expect(oldRefresh.status).toBe(401);

    const reLogin = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: adminUsername, password: 'BrandNewPass1' });
    expect(reLogin.status).toBe(200);
    const payload = JSON.parse(
      Buffer.from(reLogin.body.accessToken.split('.')[1], 'base64').toString(
        'utf8',
      ),
    );
    expect(payload.mustChangePassword).toBe(false);
  });

  it('rejects change-password when the current password is wrong (403)', async () => {
    const login = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: adminUsername, password: 'BrandNewPass1' });

    const res = await request(app.getHttpServer())
      .post('/api/v1/auth/change-password')
      .set('Authorization', `Bearer ${login.body.accessToken}`)
      .send({
        currentPassword: 'not-the-real-one',
        newPassword: 'AnotherPass1',
      });

    expect(res.status).toBe(403);
  });

  it('exposes GET /health reporting a connected Mongo', async () => {
    const res = await request(app.getHttpServer()).get('/api/v1/health');
    expect(res.status).toBe(200);
    expect(res.body).toMatchObject({ status: 'ok', mongo: 'connected' });
  });
});
