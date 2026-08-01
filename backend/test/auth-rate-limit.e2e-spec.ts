import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Auth brute-force protection (e2e)', () => {
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
    process.env.LOGIN_RATE_LIMIT_MAX = '3';
    process.env.LOGIN_RATE_LIMIT_WINDOW_SEC = '60';

    // Deferred require so this file's low rate-limit env vars are in place
    // before AppModule's decorator (ConfigModule.forRoot) evaluates them.
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

  it('returns 429 after exceeding the configured attempt limit for /auth/login', async () => {
    const attempt = () =>
      request(app.getHttpServer())
        .post('/api/v1/auth/login')
        .send({ username: 'admin', password: 'wrong' });

    const results = [];
    for (let i = 0; i < 4; i += 1) {
      results.push(await attempt());
    }

    expect(results.slice(0, 3).every((r) => r.status === 401)).toBe(true);
    expect(results[3].status).toBe(429);
  });
});
