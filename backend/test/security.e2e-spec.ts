import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import { json, urlencoded } from 'express';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Security hardening (e2e) — sub-phase 1.7', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;

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
    // Loose login limit here — this suite exercises the GLOBAL 'default'
    // throttler, not the login-specific one (that's auth-rate-limit.e2e-spec.ts).
    process.env.LOGIN_RATE_LIMIT_MAX = '1000';
    process.env.LOGIN_RATE_LIMIT_WINDOW_SEC = '60';
    process.env.API_RATE_LIMIT_MAX = '3';
    process.env.API_RATE_LIMIT_WINDOW_SEC = '60';
    process.env.HABITUAL_HOURS_TZ = 'UTC';

    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const { AppModule } = require('../src/app.module');

    const moduleRef: TestingModule = await Test.createTestingModule({
      imports: [AppModule],
    }).compile();

    app = moduleRef.createNestApplication({ bodyParser: false });
    // Mirror main.ts's explicit body-size cap exactly, so this suite tests
    // the real configured limit rather than Nest's untouched 100kb default.
    app.use(json({ limit: '1mb' }));
    app.use(urlencoded({ extended: true, limit: '1mb' }));
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

  describe('NoSQL-injection-shape payload rejection', () => {
    it('rejects a login body where username is an object instead of a string', async () => {
      const response = await request(app.getHttpServer())
        .post('/api/v1/auth/login')
        .send({ username: { $gt: '' }, password: 'whatever' });

      expect(response.status).toBe(400);
    });

    it('rejects a login body where password is an array', async () => {
      const response = await request(app.getHttpServer())
        .post('/api/v1/auth/login')
        .send({ username: 'admin', password: ['$ne', null] });

      expect(response.status).toBe(400);
    });
  });

  describe('Oversized payload rejection', () => {
    it('rejects a request body larger than the configured 1mb limit', async () => {
      const oversizedPayload = {
        username: 'admin',
        password: 'x'.repeat(2 * 1024 * 1024),
      };

      const response = await request(app.getHttpServer())
        .post('/api/v1/auth/login')
        .send(oversizedPayload);

      expect(response.status).toBe(413);
      expect(response.body.error.code).toBe('PAYLOAD_TOO_LARGE');
    });
  });

  describe('Global API rate limit', () => {
    it('returns 429 on a non-login endpoint once the global limit is exceeded', async () => {
      const results = [];
      for (let i = 0; i < 4; i += 1) {
        results.push(await request(app.getHttpServer()).get('/api/v1/health'));
      }

      expect(results.slice(0, 3).every((r) => r.status === 200)).toBe(true);
      expect(results[3].status).toBe(429);
    });
  });
});
