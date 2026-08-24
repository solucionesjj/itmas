import { SacStatisticsService } from './sac-statistics.service';
import { SacStatisticInput } from './sac-statistics.repository';
import { SacStatisticIngestDto } from './dto/sac-statistic-ingest.dto';

const logger = { setContext: jest.fn(), log: jest.fn() };

function makeService(insertMany = jest.fn().mockResolvedValue(0)) {
  const service = new SacStatisticsService(
    { insertMany } as never,
    logger as never,
  );
  return { service, insertMany };
}

function fullRecord(): SacStatisticIngestDto {
  return {
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
}

describe('SacStatisticsService.ingest', () => {
  it('reports received and inserted counts for a whole engine run', async () => {
    const { service, insertMany } = makeService(jest.fn().mockResolvedValue(3));

    const ack = await service.ingest('device-1', [
      fullRecord(),
      fullRecord(),
      fullRecord(),
    ]);

    expect(ack).toEqual({ received: 3, inserted: 3 });
    expect(insertMany).toHaveBeenCalledTimes(1);
  });

  it('stamps deviceId from the authenticated node, never from the payload', async () => {
    const { service, insertMany } = makeService(jest.fn().mockResolvedValue(1));

    await service.ingest('device-7', [
      { ...fullRecord(), deviceId: 'spoofed' } as SacStatisticIngestDto,
    ]);

    const [documents] = insertMany.mock.calls[0] as [SacStatisticInput[]];
    expect(documents[0].deviceId).toBe('device-7');
  });

  it("keeps the engine's own generatedAt as a Date rather than server-stamping now", async () => {
    const { service, insertMany } = makeService(jest.fn().mockResolvedValue(1));

    await service.ingest('device-1', [fullRecord()]);

    const [documents] = insertMany.mock.calls[0] as [SacStatisticInput[]];
    expect(documents[0].generatedAt).toEqual(
      new Date('2026-03-15T04:00:00.000Z'),
    );
  });

  it('hands the financial sums to the repository as decimal strings, not numbers', async () => {
    const { service, insertMany } = makeService(jest.fn().mockResolvedValue(1));

    await service.ingest('device-1', [fullRecord()]);

    const [documents] = insertMany.mock.calls[0] as [SacStatisticInput[]];
    // A number here would round 1234567890123456.78 — the exact case the
    // Decimal128 column exists to prevent.
    expect(documents[0].balanceSum).toBe('1234567890123456.78');
    expect(typeof documents[0].balanceSum).toBe('string');
  });

  it('normalises every absent optional metric to null, mirroring the nullable source columns', async () => {
    const { service, insertMany } = makeService(jest.fn().mockResolvedValue(1));

    await service.ingest('device-1', [
      { databaseName: 'DBSAC_Min', generatedAt: '2026-03-15T04:00:00.000Z' },
    ]);

    const [documents] = insertMany.mock.calls[0] as [SacStatisticInput[]];
    expect(documents[0]).toMatchObject({
      deviceId: 'device-1',
      databaseName: 'DBSAC_Min',
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
    });
  });

  it('accepts an empty run without a round trip — an engine with nothing to report is not an error', async () => {
    const { service, insertMany } = makeService(jest.fn().mockResolvedValue(0));

    await expect(service.ingest('device-1', [])).resolves.toEqual({
      received: 0,
      inserted: 0,
    });
    expect(insertMany).toHaveBeenCalledWith([]);
  });

  it('reports a partially rejected batch honestly instead of claiming everything landed', async () => {
    const { service } = makeService(jest.fn().mockResolvedValue(2));

    await expect(
      service.ingest('device-1', [fullRecord(), fullRecord(), fullRecord()]),
    ).resolves.toEqual({ received: 3, inserted: 2 });
  });
});
