import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { getModelToken } from '@nestjs/mongoose';
import { MongoMemoryServer } from 'mongodb-memory-server';
import { Model } from 'mongoose';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Ingestion (e2e)', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let apiKey: string;
  let deviceId: string;
  let inventoryModel: Model<unknown>;

  const baseInventory = {
    hostname: 'PC-001',
    category: 'collaborator',
    os: { name: 'Windows', version: '11' },
    cpu: { model: 'Intel i7', cores: 8 },
    ram: { totalGB: 16 },
    disks: [{ name: 'C', sizeGB: 512 }],
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

    // Deferred require — see auth.e2e-spec.ts for why (env must be set first).
    /* eslint-disable @typescript-eslint/no-require-imports */
    const { AppModule } = require('../src/app.module');
    const {
      DevicesService,
    } = require('../src/modules/devices/devices.service');
    const {
      Inventory,
    } = require('../src/modules/inventories/inventory.schema');
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

    const devicesService = app.get(DevicesService);
    const provisioned = await devicesService.provision({
      hostname: 'PC-001',
      category: 'collaborator',
    });
    deviceId = provisioned.deviceId;
    apiKey = provisioned.apiKey;

    inventoryModel = app.get(getModelToken(Inventory.name));
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects ingestion without a node API key (401)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .send({ ...baseInventory, timestamp: '2026-01-01T00:00:00.000Z' });

    expect(res.status).toBe(401);
    expect(res.body.error.requestId).toBeDefined();
  });

  it('rejects ingestion with an invalid node API key (401)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', `${deviceId}.wrong-secret`)
      .send({ ...baseInventory, timestamp: '2026-01-01T00:00:00.000Z' });

    expect(res.status).toBe(401);
  });

  it('rejects a malformed inventory payload (400)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', apiKey)
      .send({ hostname: 'PC-001' });

    expect(res.status).toBe(400);
  });

  it('accepts a valid inventory and persists it (CA-01)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', apiKey)
      .send({ ...baseInventory, timestamp: '2026-01-01T10:00:00.000Z' });

    expect(res.status).toBe(201);
    expect(res.body).toMatchObject({
      deviceId,
      timestamp: '2026-01-01T10:00:00.000Z',
    });

    const count = await inventoryModel.countDocuments({ deviceId });
    expect(count).toBe(1);
  });

  it('is idempotent on an identical retry — no duplicate stored (agent.md §4)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', apiKey)
      .send({ ...baseInventory, timestamp: '2026-01-01T10:00:00.000Z' });

    expect(res.status).toBe(201);

    const count = await inventoryModel.countDocuments({ deviceId });
    expect(count).toBe(1);
  });

  it('detects and logs a resource change on the next inventory (CA-02 detection half)', async () => {
    const writeSpy = jest
      .spyOn(process.stdout, 'write')
      .mockImplementation(() => true);

    const res = await request(app.getHttpServer())
      .post('/api/v1/inventory')
      .set('X-Node-Api-Key', apiKey)
      .send({
        ...baseInventory,
        ram: { totalGB: 32 },
        timestamp: '2026-01-01T11:00:00.000Z',
      });

    expect(res.status).toBe(201);

    const logged = writeSpy.mock.calls
      .map((call) => String(call[0]))
      .some(
        (line) =>
          line.includes('Resource change detected') && line.includes('"ram"'),
      );
    expect(logged).toBe(true);

    writeSpy.mockRestore();

    const count = await inventoryModel.countDocuments({ deviceId });
    expect(count).toBe(2);
  });

  it('accepts a valid access event (login)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/access-events')
      .set('X-Node-Api-Key', apiKey)
      .send({
        level: 'os',
        user: 'jdoe',
        timestamp: '2026-01-01T12:00:00.000Z',
        action: 'login',
      });

    expect(res.status).toBe(201);
  });

  it('rejects an access event with an invalid enum value (400)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/access-events')
      .set('X-Node-Api-Key', apiKey)
      .send({
        level: 'os',
        user: 'jdoe',
        timestamp: '2026-01-01T12:00:00.000Z',
        action: 'not-a-real-action',
      });

    expect(res.status).toBe(400);
  });
});
