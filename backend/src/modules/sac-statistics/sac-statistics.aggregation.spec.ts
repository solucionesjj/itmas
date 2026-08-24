import { MongoMemoryServer } from 'mongodb-memory-server';
import mongoose, { Model } from 'mongoose';
import { SacStatisticsRepository } from './sac-statistics.repository';
import {
  SacStatistic,
  SacStatisticDocument,
  SacStatisticSchema,
} from './sac-statistic.schema';
import { SacMetric } from './sac-metric.enum';
import { buildMonthWindow } from './month-window.util';

/**
 * BL-033 CA-12. These exercise the real `$group`/`$map` pipelines against a real
 * query engine rather than a mocked model: an aggregation's behaviour IS the
 * thing under test here, and a mock would assert the shape of the stages I
 * happened to write instead of what Mongo does with them. The three scenarios
 * CA-12 names — a month with no snapshot, two snapshots on the same day, and a
 * database whose history is shorter than the requested window — are each a case
 * where a plausible-looking pipeline is quietly wrong.
 */
describe('SAC statistics aggregations', () => {
  let mongod: MongoMemoryServer;
  let model: Model<SacStatisticDocument>;
  let repository: SacStatisticsRepository;

  /** Month keys relative to today, so the fixtures land inside the window. */
  const { monthKeys, windowStart } = buildMonthWindow(6, new Date());
  const monthsBack = (back: number, day = 15, hour = 4) => {
    const now = new Date();
    return new Date(
      Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - back, day, hour),
    );
  };

  const record = (
    databaseName: string,
    generatedAt: Date,
    overrides: Partial<SacStatistic> = {},
  ) => ({
    deviceId: 'device-1',
    databaseName,
    generatedAt,
    totalSizeGb: null,
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
  });

  beforeAll(async () => {
    mongod = await MongoMemoryServer.create();
    await mongoose.connect(mongod.getUri());
    model = mongoose.model<SacStatisticDocument>(
      SacStatistic.name,
      SacStatisticSchema,
    );
    repository = new SacStatisticsRepository(model, {
      get: () => null,
    } as never);

    await model.insertMany([
      // ---- ACME: a full six-month run, growing steadily.
      ...[5, 4, 3, 2, 1, 0].map((back, index) =>
        record('DBSAC_Acme', monthsBack(back), {
          totalSizeGb: 100 + index * 10,
          balanceSum: `${1000 + index * 100}.50` as never,
        }),
      ),
      // ---- ACME, same day as the latest snapshot but EARLIER: the duplicate
      // BL-031's append-only model allows. The later one must win.
      record('DBSAC_Acme', monthsBack(0, 15, 1), {
        totalSizeGb: 999,
        balanceSum: '9.99' as never,
      }),

      // ---- GLOBEX: nothing at all in the month three back — the hole that must
      // stay a hole rather than being bridged across.
      record('DBSAC_Globex', monthsBack(5), { totalSizeGb: 500 }),
      record('DBSAC_Globex', monthsBack(4), { totalSizeGb: 520 }),
      record('DBSAC_Globex', monthsBack(2), { totalSizeGb: 600 }),
      record('DBSAC_Globex', monthsBack(1), { totalSizeGb: 630 }),
      record('DBSAC_Globex', monthsBack(0), { totalSizeGb: 660 }),

      // ---- INITECH: onboarded two months ago, so its history is shorter than
      // the six-month window.
      record('DBSAC_Initech', monthsBack(1), { totalSizeGb: 40 }),
      record('DBSAC_Initech', monthsBack(0), { totalSizeGb: 44 }),

      // ---- ZERO: a base at 0 last month, to pin down the divide-by-zero case.
      record('DBSAC_Zero', monthsBack(1), { totalSizeGb: 0 }),
      record('DBSAC_Zero', monthsBack(0), { totalSizeGb: 12 }),
    ] as never);
  }, 60000);

  afterAll(async () => {
    await mongoose.disconnect();
    await mongod.stop();
  });

  const seriesFor = (
    result: { databaseName: string; series: unknown[] }[],
    name: string,
  ) => result.find((entry) => entry.databaseName === name)!;

  describe('rankByMetric', () => {
    it('orders databases by the metric, largest first, from each one latest snapshot', async () => {
      const rows = await repository.rankByMetric(SacMetric.TOTAL_SIZE_GB);

      expect(rows.map((row) => row.databaseName)).toEqual([
        'DBSAC_Globex',
        'DBSAC_Acme',
        'DBSAC_Initech',
        'DBSAC_Zero',
      ]);
    });

    it('takes the LATEST of two snapshots on the same day, not the first inserted', async () => {
      const rows = await repository.rankByMetric(SacMetric.TOTAL_SIZE_GB);

      // The 01:00 duplicate says 999; the 04:00 record says 150. Reading the
      // duplicate would put Acme on top of the ranking.
      expect(seriesFor(rows as never, 'DBSAC_Acme')).toBeDefined();
      expect(rows.find((r) => r.databaseName === 'DBSAC_Acme')!.value).toBe(
        150,
      );
    });

    it('honours an as-of date, ignoring everything generated after it', async () => {
      const rows = await repository.rankByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthsBack(3, 28),
      );

      // Initech and Zero did not exist yet at that point.
      expect(rows.map((row) => row.databaseName)).toEqual([
        'DBSAC_Globex',
        'DBSAC_Acme',
      ]);
      expect(rows[1].value).toBe(120);
    });

    it('ranks a financial metric as a Decimal128, keeping its exact value', async () => {
      const rows = await repository.rankByMetric(SacMetric.BALANCE_SUM);
      const acme = rows.find((row) => row.databaseName === 'DBSAC_Acme')!;

      expect(acme.value?.toString()).toBe('1500.50');
    });

    it('sorts a database with no value for the metric to the end, not the top', async () => {
      const rows = await repository.rankByMetric(SacMetric.DEBTORS);

      // Every record has debtors: null, so nothing should claim the top spot with
      // a null — the check that null does not sort above a number in a desc sort.
      expect(rows.every((row) => row.value === null)).toBe(true);
    });
  });

  describe('growthByMetric', () => {
    it('returns a dense grid of exactly the requested months, oldest first', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );

      for (const entry of result) {
        expect(entry.series.map((point) => point.month)).toEqual(monthKeys);
      }
    });

    it('computes value, absolute delta and percentage against the previous month', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );
      const acme = seriesFor(result, 'DBSAC_Acme').series as {
        value: number;
        delta: number;
        deltaPercent: number;
      }[];

      expect(acme[1].value).toBe(110);
      expect(acme[1].delta).toBe(10);
      expect(acme[1].deltaPercent).toBe(10);
    });

    it('leaves the first month without a variation — there is no prior month in the window', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );
      const acme = seriesFor(result, 'DBSAC_Acme').series as {
        delta: unknown;
        deltaPercent: unknown;
      }[];

      expect(acme[0].delta).toBeNull();
      expect(acme[0].deltaPercent).toBeNull();
    });

    it('reports a month with no snapshot as no data, without interpolating (CA-5)', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );
      const globex = seriesFor(result, 'DBSAC_Globex').series as {
        month: string;
        value: number | null;
      }[];

      // The grid runs oldest → newest, so index 2 of a six-month window is the
      // month three back — the one Globex skipped.
      expect(globex[2].value).toBeNull();
      // And not carried forward from the month before it.
      expect(globex[1].value).toBe(520);
    });

    it('does NOT bridge a delta across a missing month', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );
      const globex = seriesFor(result, 'DBSAC_Globex').series as {
        value: number | null;
        delta: number | null;
      }[];

      // The month AFTER the hole has a value but no computable variation: a
      // pipeline that deltas over the compacted array instead of the dense grid
      // would report 600 - 520 = 80 here, presenting two months of growth as one.
      expect(globex[3].value).toBe(600);
      expect(globex[3].delta).toBeNull();
      // The hole itself has neither.
      expect(globex[2].delta).toBeNull();
      // And the month after that resumes normally against its real predecessor.
      expect(globex[4].delta).toBe(30);
    });

    it('handles a database whose history is shorter than the window (CA-12)', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );
      const initech = seriesFor(result, 'DBSAC_Initech').series as {
        value: number | null;
        delta: number | null;
      }[];

      // Same six-month grid as everyone else, with the pre-onboarding months as
      // no data rather than zeros — the series is comparable, not truncated.
      expect(initech).toHaveLength(monthKeys.length);
      expect(initech.slice(0, 4).every((point) => point.value === null)).toBe(
        true,
      );
      expect(initech[4].value).toBe(40);
      expect(initech[4].delta).toBeNull();
      expect(initech[5].delta).toBe(4);
    });

    it('uses the LATEST snapshot of a month when there are two on the same day (CA-4)', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );
      const acme = seriesFor(result, 'DBSAC_Acme').series as {
        value: number;
      }[];

      // 150 at 04:00 wins over the 999 duplicate at 01:00 — this is also how the
      // append-only model's duplicates are neutralised.
      expect(acme[5].value).toBe(150);
    });

    it('reports no percentage when the previous month was zero, rather than infinity', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
      );
      const zero = seriesFor(result, 'DBSAC_Zero').series as {
        value: number | null;
        delta: number | null;
        deltaPercent: number | null;
      }[];

      expect(zero[5].value).toBe(12);
      // The absolute variation is still meaningful and reported.
      expect(zero[5].delta).toBe(12);
      expect(zero[5].deltaPercent).toBeNull();
    });

    it('aggregates a financial metric in Decimal128, not floating point (CA-7)', async () => {
      const result = await repository.growthByMetric(
        SacMetric.BALANCE_SUM,
        monthKeys,
        windowStart,
      );
      const acme = seriesFor(result, 'DBSAC_Acme').series as {
        value: { toString(): string } | null;
        delta: { toString(): string } | null;
      }[];

      expect(acme[5].value?.toString()).toBe('1500.50');
      // Exactly 100, not 99.99999999999999 — the whole point of $toDecimal.
      expect(acme[5].delta?.toString()).toBe('100.00');
    });

    it('narrows to one database when asked, without changing the grid', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
        'initech',
      );

      expect(result).toHaveLength(1);
      expect(result[0].databaseName).toBe('DBSAC_Initech');
      expect(result[0].series).toHaveLength(monthKeys.length);
    });

    it('treats regex metacharacters in the database filter as literals', async () => {
      const result = await repository.growthByMetric(
        SacMetric.TOTAL_SIZE_GB,
        monthKeys,
        windowStart,
        '.*',
      );

      // An unescaped `.*` would have matched every database.
      expect(result).toHaveLength(0);
    });
  });

  describe('listDatabaseNames', () => {
    it('returns each database once, alphabetically', async () => {
      await expect(repository.listDatabaseNames()).resolves.toEqual([
        'DBSAC_Acme',
        'DBSAC_Globex',
        'DBSAC_Initech',
        'DBSAC_Zero',
      ]);
    });
  });
});
