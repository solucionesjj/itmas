import { INestApplication, ValidationPipe } from '@nestjs/common';
import { Test, TestingModule } from '@nestjs/testing';
import { MongoMemoryServer } from 'mongodb-memory-server';
import * as argon2 from 'argon2';
import request from 'supertest';
import { AllExceptionsFilter } from '../src/common/filters/all-exceptions.filter';
import { JsonLoggerService } from '../src/common/logger/json-logger.service';

describe('Security Group Rules (e2e) — RBAC + review/authorize workflow', () => {
  let app: INestApplication;
  let mongod: MongoMemoryServer;
  let adminToken: string;
  let adminId: string;
  let usuarioToken: string;
  let auditorToken: string;

  const seedRule = async (
    repository: {
      upsertObserved: (input: Record<string, unknown>) => Promise<unknown>;
    },
    overrides: Record<string, unknown> = {},
  ) => {
    await repository.upsertObserved({
      awsAccountId: '123456789012',
      region: 'us-east-1',
      vpcId: 'vpc-1',
      securityGroupId: 'sg-1',
      securityGroupName: 'web-servers',
      attachedResources: [
        {
          resourceType: 'ec2-instance',
          resourceId: 'i-1',
          resourceName: 'web-01',
        },
      ],
      ruleId: 'sgr-1',
      ruleName: 'HTTPS desde internet',
      direction: 'ingress',
      remoteEndpoint: { kind: 'cidr_ipv4', value: '0.0.0.0/0' },
      source: '0.0.0.0/0',
      destination: 'sg-1',
      protocol: 'tcp',
      portRange: '443',
      ...overrides,
    });
  };

  beforeAll(async () => {
    mongod = await MongoMemoryServer.create();
    process.env.MONGO_URI = mongod.getUri();
    process.env.PORT = '3002';
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

    const decoded = JSON.parse(
      Buffer.from(adminToken.split('.')[1], 'base64url').toString('utf-8'),
    );
    adminId = decoded.sub as string;
  }, 60000);

  afterAll(async () => {
    await app.close();
    await mongod.stop();
  });

  it('rejects an unauthenticated request on every endpoint (401)', async () => {
    const server = app.getHttpServer();

    expect(
      (await request(server).get('/api/v1/security-group-rules')).status,
    ).toBe(401);
    expect(
      (await request(server).get('/api/v1/security-group-rules/groups')).status,
    ).toBe(401);
    expect(
      (
        await request(server).get(
          '/api/v1/security-group-rules/export?format=csv',
        )
      ).status,
    ).toBe(401);
    expect(
      (
        await request(server)
          .patch('/api/v1/security-group-rules/x/review')
          .send({ observation: 'x' })
      ).status,
    ).toBe(401);
    expect(
      (
        await request(server)
          .patch('/api/v1/security-group-rules/x/authorize')
          .send({ observation: 'x' })
      ).status,
    ).toBe(401);
  });

  it('allows Usuario to list and export the catalog, but 403s on review/authorize (RF-25/CA-18)', async () => {
    /* eslint-disable @typescript-eslint/no-require-imports */
    const {
      SecurityGroupRulesRepository,
    } = require('../src/modules/security-group-rules/security-group-rules.repository');
    /* eslint-enable @typescript-eslint/no-require-imports */
    const repository = app.get(SecurityGroupRulesRepository);
    await seedRule(repository);

    const list = await request(app.getHttpServer())
      .get('/api/v1/security-group-rules')
      .set('Authorization', `Bearer ${usuarioToken}`);
    expect(list.status).toBe(200);
    expect(list.body.items.length).toBeGreaterThanOrEqual(1);

    const groups = await request(app.getHttpServer())
      .get('/api/v1/security-group-rules/groups')
      .set('Authorization', `Bearer ${usuarioToken}`);
    expect(groups.status).toBe(200);
    expect(groups.body).toEqual(
      expect.arrayContaining([
        { securityGroupId: 'sg-1', securityGroupName: 'web-servers' },
      ]),
    );

    const exportRes = await request(app.getHttpServer())
      .get('/api/v1/security-group-rules/export?format=csv')
      .set('Authorization', `Bearer ${usuarioToken}`);
    expect(exportRes.status).toBe(200);
    expect(exportRes.text).toContain('sg-1');

    const ruleId = list.body.items[0]._id;
    const reviewAsUsuario = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/review`)
      .set('Authorization', `Bearer ${usuarioToken}`)
      .send({ observation: 'intento no autorizado' });
    expect(reviewAsUsuario.status).toBe(403);

    const authorizeAsUsuario = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/authorize`)
      .set('Authorization', `Bearer ${usuarioToken}`)
      .send({ observation: 'intento no autorizado' });
    expect(authorizeAsUsuario.status).toBe(403);
  });

  it('enforces two-person separation: Auditor reviews, Administrador authorizes (RF-23/24, CA-16/17)', async () => {
    const list = await request(app.getHttpServer())
      .get('/api/v1/security-group-rules?status=pendiente')
      .set('Authorization', `Bearer ${adminToken}`);
    const ruleId = list.body.items[0]._id;

    // Administrador cannot review — only Auditor can (CA-16).
    const reviewAsAdmin = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/review`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ observation: 'no debería poder' });
    expect(reviewAsAdmin.status).toBe(403);

    // Auditor cannot authorize a still-pendiente rule (wrong role at all, CA-17).
    const authorizeAsAuditorTooEarly = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/authorize`)
      .set('Authorization', `Bearer ${auditorToken}`)
      .send({ observation: 'no debería poder' });
    expect(authorizeAsAuditorTooEarly.status).toBe(403);

    // Auditor reviews.
    const review = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/review`)
      .set('Authorization', `Bearer ${auditorToken}`)
      .send({ observation: 'regla revisada, luce correcta' });
    expect(review.status).toBe(200);
    expect(review.body.status).toBe('revisado');

    // Auditor cannot then authorize their own review.
    const authorizeAsAuditor = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/authorize`)
      .set('Authorization', `Bearer ${auditorToken}`)
      .send({ observation: 'no debería poder' });
    expect(authorizeAsAuditor.status).toBe(403);

    // Administrador authorizes.
    const authorize = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/authorize`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ observation: 'autorizado, expuesto intencionalmente' });
    expect(authorize.status).toBe(200);
    expect(authorize.body.status).toBe('autorizado');

    // Re-authorizing an already-autorizado rule is rejected (409).
    const reAuthorize = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${ruleId}/authorize`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ observation: 'de nuevo' });
    expect(reAuthorize.status).toBe(409);
  });

  it('blocks the same user from reviewing and authorizing the same rule (defensive check)', async () => {
    /* eslint-disable @typescript-eslint/no-require-imports */
    const {
      SecurityGroupRulesRepository,
    } = require('../src/modules/security-group-rules/security-group-rules.repository');
    /* eslint-enable @typescript-eslint/no-require-imports */
    const repository = app.get(SecurityGroupRulesRepository);
    await seedRule(repository, { ruleId: 'sgr-same-actor' });

    const list = await request(app.getHttpServer())
      .get('/api/v1/security-group-rules?securityGroupId=sg-1&status=pendiente')
      .set('Authorization', `Bearer ${adminToken}`);
    const rule = list.body.items.find(
      (r: { ruleId: string }) => r.ruleId === 'sgr-same-actor',
    );

    // Sets reviewedBy=admin directly, bypassing the Auditor-only endpoint —
    // this state is otherwise unreachable through the API (RBAC alone
    // already prevents it), so this exercises the defensive same-actor
    // guard in SecurityGroupRulesService.authorize() (ADR-0013).
    await repository.setReviewed(
      rule._id,
      'reviewed by admin directly',
      adminId,
    );

    const authorizeSameActor = await request(app.getHttpServer())
      .patch(`/api/v1/security-group-rules/${rule._id}/authorize`)
      .set('Authorization', `Bearer ${adminToken}`)
      .send({ observation: 'intentando autorizar mi propia revisión' });

    expect(authorizeSameActor.status).toBe(403);
  });

  it('sorts the export by group id then rule id regardless of the list sort (RF-25)', async () => {
    /* eslint-disable @typescript-eslint/no-require-imports */
    const {
      SecurityGroupRulesRepository,
    } = require('../src/modules/security-group-rules/security-group-rules.repository');
    /* eslint-enable @typescript-eslint/no-require-imports */
    const repository = app.get(SecurityGroupRulesRepository);
    await seedRule(repository, { securityGroupId: 'sg-9', ruleId: 'sgr-a' });
    await seedRule(repository, { securityGroupId: 'sg-2', ruleId: 'sgr-b' });

    const exportRes = await request(app.getHttpServer())
      .get('/api/v1/security-group-rules/export?format=csv')
      .set('Authorization', `Bearer ${adminToken}`);

    const lines = exportRes.text.trim().split('\r\n').slice(1);
    const groupIds = lines.map((line) => line.split(',')[1]);
    const sorted = [...groupIds].sort();
    expect(groupIds).toEqual(sorted);
  });
});
