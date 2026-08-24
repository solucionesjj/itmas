import { SacStatisticsRepository } from './sac-statistics.repository';
import { SacStatisticInput } from './sac-statistics.repository';
import * as ttl from '../../common/mongo/ensure-ttl-index.util';

function makeRepository(
  retentionDays: number | null,
  model: Record<string, unknown> = {},
) {
  const configService = { get: jest.fn().mockReturnValue(retentionDays) };
  return new SacStatisticsRepository(
    { collection: {}, ...model } as never,
    configService as never,
  );
}

function input(overrides: Partial<SacStatisticInput> = {}): SacStatisticInput {
  return {
    deviceId: 'device-1',
    databaseName: 'DBSAC_Acme',
    generatedAt: new Date('2026-03-15T04:00:00.000Z'),
    totalSizeGb: 10,
    debtors: null,
    debtorActiveAccounts: null,
    activeAccounts: null,
    avgActiveUsersLast3Months: null,
    activities: null,
    activitiesLast30Days: null,
    activeUsers: null,
    logSizeGb: null,
    dataSizeGb: null,
    balanceSum: null,
    overdueSum: null,
    principalSum: null,
    ...overrides,
  };
}

describe('SacStatisticsRepository.onModuleInit (retention)', () => {
  let ensureTtlIndex: jest.SpyInstance;

  beforeEach(() => {
    ensureTtlIndex = jest
      .spyOn(ttl, 'ensureTtlIndex')
      .mockResolvedValue(undefined);
  });

  afterEach(() => jest.restoreAllMocks());

  it('creates NO TTL index when retention is unconfigured — the growth analysis needs the full history', async () => {
    await makeRepository(null).onModuleInit();

    expect(ensureTtlIndex).not.toHaveBeenCalled();
  });

  it('creates the TTL index on generatedAt when an operator opts in', async () => {
    await makeRepository(730).onModuleInit();

    expect(ensureTtlIndex).toHaveBeenCalledWith(
      expect.anything(),
      'generatedAt',
      730 * 24 * 60 * 60,
    );
  });
});

describe('SacStatisticsRepository.insertMany', () => {
  it('inserts the batch unordered, so one rejected record does not discard the rest', async () => {
    const insertMany = jest.fn().mockResolvedValue([{}, {}]);
    const repository = makeRepository(null, { insertMany });

    const inserted = await repository.insertMany([input(), input()]);

    expect(inserted).toBe(2);
    expect(insertMany).toHaveBeenCalledWith(expect.any(Array), {
      ordered: false,
    });
  });

  it('short-circuits an empty batch instead of round-tripping to Mongo', async () => {
    const insertMany = jest.fn();
    const repository = makeRepository(null, { insertMany });

    await expect(repository.insertMany([])).resolves.toBe(0);
    expect(insertMany).not.toHaveBeenCalled();
  });
});
