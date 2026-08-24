import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { getModelToken } from '@nestjs/mongoose';
import { MongoMemoryServer } from 'mongodb-memory-server';
import { Model, Types } from 'mongoose';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

/** BL-031 CA-9. */
describe('SAC statistics ingestion (e2e)', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let apiKey: string;
  let deviceId: string;
  let adminToken: string;
  let statsModel: Model<{
    deviceId: string;
    databaseName: string;
    balanceSum: Types.Decimal128 | null;
  }>;

  const record = {
    databaseName: 'DBSAC_Acme',
    generatedAt: '2026-03-15T04:00:00.000Z',
    totalSizeGb: 512,
    debtors: 1200,
    debtorActiveAccounts: 1500,
    activeAccounts: 1800,
    avgActiveUsersLast3Months: 42,
    activities: 987654,
    activitiesLast30Days: 12345,
    activeUsers: 45,
    logSizeGb: 12,
    dataSizeGb: 500,
    balanceSum: '1234567890123456.78',
    overdueSum: '1000.00',
    principalSum: '2000.50',
  };

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
    // Left unset on purpose: this suite also asserts that no TTL index is
    // created without it (CA-7).
    delete process.env.SAC_STATISTICS_RETENTION_DAYS;

    // Deferred require — see auth.e2e-spec.ts for why (env must be set first).
    /* eslint-disable @typescript-eslint/no-require-imports */
    const { AppModule } = require('../src/app.module');
    const {
      DevicesService,
    } = require('../src/modules/devices/devices.service');
    const {
      SacStatistic,
    } = require('../src/modules/sac-statistics/sac-statistic.schema');
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

    // The reporting database engine is an ordinary `infrastructure` device —
    // BL-031 adds no `database` category, since changing the enum would drag in
    // the alert engine, which discriminates on category.
    const provisioned = await app.get(DevicesService).provision({
      hostname: 'SQL-SAC-01',
      category: 'infrastructure',
    });
    deviceId = provisioned.deviceId;
    apiKey = provisioned.apiKey;

    statsModel = app.get(getModelToken(SacStatistic.name));

    const login = await request(app.getHttpServer())
      .post('/api/v1/auth/login')
      .send({ username: 'admin', password: 'InitialAdmin1' });
    adminToken = login.body.accessToken;
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects ingestion without a node API key (401)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .send([record]);

    expect(res.status).toBe(401);
    expect(res.body.error.requestId).toBeDefined();
  });

  it('rejects an invalid node API key (401)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', `${deviceId}.wrong-secret`)
      .send([record]);

    expect(res.status).toBe(401);
  });

  it('rejects a user JWT (401) — this endpoint is node-authenticated only, never mixed', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('Authorization', `Bearer ${adminToken}`)
      .send([record]);

    expect(res.status).toBe(401);
  });

  it('accepts a full engine run with a valid key (201) and reports received/inserted', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([record, { ...record, databaseName: 'DBSAC_Globex' }]);

    expect(res.status).toBe(201);
    expect(res.body).toEqual({ received: 2, inserted: 2 });

    const stored = await statsModel.countDocuments({ deviceId });
    expect(stored).toBe(2);
  });

  it('stamps deviceId from the key, so an agent cannot report on another device behalf', async () => {
    const stored = await statsModel
      .findOne({ databaseName: 'DBSAC_Acme' })
      .lean();

    expect(stored?.deviceId).toBe(deviceId);
  });

  it('persists the financial sums as Decimal128 with every digit intact', async () => {
    const stored = await statsModel
      .findOne({ databaseName: 'DBSAC_Acme' })
      .lean();

    // A double would have rounded this; the whole point of the column type.
    expect(stored?.balanceSum?.toString()).toBe('1234567890123456.78');
  });

  it('is NOT idempotent by design — a resend appends a second record (CA-5)', async () => {
    const before = await statsModel.countDocuments({
      databaseName: 'DBSAC_Acme',
    });

    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([record]);

    expect(res.status).toBe(201);
    expect(
      await statsModel.countDocuments({ databaseName: 'DBSAC_Acme' }),
    ).toBe(before + 1);
  });

  it('rejects an unknown field (400) — agents must use the exact contract names', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([{ ...record, tamanoTotalBaseDatos: 512 }]);

    expect(res.status).toBe(400);
  });

  it('rejects a payload-supplied deviceId (400) — provenance is not client-assertable', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([{ ...record, deviceId: 'some-other-device' }]);

    expect(res.status).toBe(400);
  });

  it('rejects a missing databaseName (400)', async () => {
    const { databaseName, ...withoutName } = record;
    void databaseName;

    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([withoutName]);

    expect(res.status).toBe(400);
  });

  it('rejects a missing generatedAt (400) — the engine time is never server-stamped', async () => {
    const { generatedAt, ...withoutDate } = record;
    void generatedAt;

    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([withoutDate]);

    expect(res.status).toBe(400);
  });

  it('rejects a databaseName over 128 characters (400)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([{ ...record, databaseName: 'x'.repeat(129) }]);

    expect(res.status).toBe(400);
  });

  it('rejects a negative numeric (400)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([{ ...record, debtors: -1 }]);

    expect(res.status).toBe(400);
  });

  it('rejects a financial sum with more than two decimal places (400)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([{ ...record, balanceSum: '10.123' }]);

    expect(res.status).toBe(400);
  });

  it('rejects a non-array body (400) — an engine reports its whole run as a list', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send(record);

    expect(res.status).toBe(400);
  });

  it('accepts a record carrying only the two required fields, storing the rest as null', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/sac-statistics')
      .set('X-Node-Api-Key', apiKey)
      .send([
        {
          databaseName: 'DBSAC_Minimal',
          generatedAt: '2026-03-15T04:00:00.000Z',
        },
      ]);

    expect(res.status).toBe(201);
    const stored = await statsModel
      .findOne({ databaseName: 'DBSAC_Minimal' })
      .lean();
    expect(stored?.balanceSum).toBeNull();
  });

  it('creates no TTL index on generatedAt when retention is unconfigured (CA-7)', async () => {
    const indexes = await statsModel.collection.indexes();
    const ttlIndex = indexes.find(
      (index) => index.expireAfterSeconds !== undefined,
    );

    expect(ttlIndex).toBeUndefined();
  });

  it('creates the two required query indexes (CA-6)', async () => {
    const indexes = await statsModel.collection.indexes();
    const keys = indexes.map((index) => JSON.stringify(index.key));

    expect(keys).toContain(
      JSON.stringify({ databaseName: 1, generatedAt: -1 }),
    );
    expect(keys).toContain(JSON.stringify({ generatedAt: -1 }));
  });
});
