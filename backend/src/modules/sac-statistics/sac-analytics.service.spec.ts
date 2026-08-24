import { Types } from 'mongoose';
import { SacAnalyticsService } from './sac-analytics.service';
import { SacMetric } from './sac-metric.enum';

function makeService(
  overrides: Partial<{
    rankByMetric: jest.Mock;
    growthByMetric: jest.Mock;
    listDatabaseNames: jest.Mock;
  }> = {},
) {
  const repository = {
    rankByMetric: jest.fn().mockResolvedValue([]),
    growthByMetric: jest.fn().mockResolvedValue([]),
    listDatabaseNames: jest.fn().mockResolvedValue([]),
    ...overrides,
  };
  return { service: new SacAnalyticsService(repository as never), repository };
}

describe('SacAnalyticsService.ranking', () => {
  it('echoes the metric and the as-of date back, so a client can tell the two query kinds apart', async () => {
    const { service } = makeService();

    const result = await service.ranking({
      metric: SacMetric.TOTAL_SIZE_GB,
      at: '2026-06-30T00:00:00.000Z',
    });

    expect(result.metric).toBe(SacMetric.TOTAL_SIZE_GB);
    expect(result.at).toBe('2026-06-30T00:00:00.000Z');
  });

  it('reports a null `at` rather than omitting it when no date was given', async () => {
    const { service, repository } = makeService();

    const result = await service.ranking({ metric: SacMetric.DEBTORS });

    expect(result.at).toBeNull();
    expect(repository.rankByMetric).toHaveBeenCalledWith(
      SacMetric.DEBTORS,
      undefined,
    );
  });

  it('emits a financial metric as a decimal string, never as a JSON number', async () => {
    const { service } = makeService({
      rankByMetric: jest.fn().mockResolvedValue([
        {
          databaseName: 'DBSAC_Acme',
          value: Types.Decimal128.fromString('1234567890123456.78'),
          generatedAt: new Date('2026-03-15T04:00:00.000Z'),
        },
      ]),
    });

    const result = await service.ranking({ metric: SacMetric.BALANCE_SUM });

    // A number here would round to …456.8 — the exact loss the Decimal128
    // storage exists to prevent, reintroduced at the last step.
    expect(result.entries[0].value).toBe('1234567890123456.78');
  });

  it('emits an integer metric as a number', async () => {
    const { service } = makeService({
      rankByMetric: jest.fn().mockResolvedValue([
        {
          databaseName: 'DBSAC_Acme',
          value: 512,
          generatedAt: new Date('2026-03-15T04:00:00.000Z'),
        },
      ]),
    });

    const result = await service.ranking({ metric: SacMetric.TOTAL_SIZE_GB });

    expect(result.entries[0].value).toBe(512);
  });

  it('passes a missing value through as null rather than coercing it to zero', async () => {
    const { service } = makeService({
      rankByMetric: jest.fn().mockResolvedValue([
        {
          databaseName: 'DBSAC_Acme',
          value: null,
          generatedAt: new Date('2026-03-15T04:00:00.000Z'),
        },
      ]),
    });

    const result = await service.ranking({ metric: SacMetric.DEBTORS });

    expect(result.entries[0].value).toBeNull();
  });
});

describe('SacAnalyticsService.growth', () => {
  it('defaults to a twelve-month window and returns the grid it used', async () => {
    const { service, repository } = makeService();

    const result = await service.growth({ metric: SacMetric.TOTAL_SIZE_GB });

    expect(result.months).toHaveLength(12);
    // The same grid goes into the pipeline as constants (CA-8).
    expect(repository.growthByMetric).toHaveBeenCalledWith(
      SacMetric.TOTAL_SIZE_GB,
      result.months,
      expect.any(Date),
      undefined,
    );
  });

  it('honours a requested window length', async () => {
    const { service } = makeService();

    const result = await service.growth({
      metric: SacMetric.TOTAL_SIZE_GB,
      months: 6,
    });

    expect(result.months).toHaveLength(6);
  });

  it('forwards the database filter', async () => {
    const { service, repository } = makeService();

    await service.growth({
      metric: SacMetric.ACTIVITIES,
      months: 3,
      databaseName: 'acme',
    });

    expect(repository.growthByMetric).toHaveBeenCalledWith(
      SacMetric.ACTIVITIES,
      expect.any(Array),
      expect.any(Date),
      'acme',
    );
  });

  it('converts both the value AND the delta of a financial metric to strings', async () => {
    const { service } = makeService({
      growthByMetric: jest.fn().mockResolvedValue([
        {
          databaseName: 'DBSAC_Acme',
          series: [
            {
              month: '2026-07',
              value: Types.Decimal128.fromString('1000.00'),
              delta: null,
              deltaPercent: null,
            },
            {
              month: '2026-08',
              value: Types.Decimal128.fromString('1100.00'),
              delta: Types.Decimal128.fromString('100.00'),
              deltaPercent: 10,
            },
          ],
        },
      ]),
    });

    const result = await service.growth({ metric: SacMetric.BALANCE_SUM });

    expect(result.series[0].series[1].value).toBe('1100.00');
    // The delta is just as much a currency figure as the value is.
    expect(result.series[0].series[1].delta).toBe('100.00');
    expect(result.series[0].series[1].deltaPercent).toBe(10);
  });

  it('keeps a month with no snapshot as null in value, delta and percentage (CA-5)', async () => {
    const { service } = makeService({
      growthByMetric: jest.fn().mockResolvedValue([
        {
          databaseName: 'DBSAC_Globex',
          series: [
            { month: '2026-07', value: null, delta: null, deltaPercent: null },
          ],
        },
      ]),
    });

    const result = await service.growth({ metric: SacMetric.TOTAL_SIZE_GB });

    expect(result.series[0].series[0]).toEqual({
      month: '2026-07',
      value: null,
      delta: null,
      deltaPercent: null,
    });
  });

  it('normalises an absent deltaPercent to null rather than leaving it undefined', async () => {
    const { service } = makeService({
      growthByMetric: jest.fn().mockResolvedValue([
        {
          databaseName: 'DBSAC_Acme',
          series: [{ month: '2026-08', value: 10, delta: 5 }],
        },
      ]),
    });

    const result = await service.growth({ metric: SacMetric.TOTAL_SIZE_GB });

    // `undefined` disappears from JSON entirely, so the client would see a
    // missing key instead of an explicit "no data".
    expect(result.series[0].series[0].deltaPercent).toBeNull();
  });
});
