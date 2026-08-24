import { Types } from 'mongoose';
import {
  decimalToString,
  toSacStatisticResponse,
} from './sac-statistic-response.mapper';
import { SacStatisticDocument } from './sac-statistic.schema';

describe('decimalToString', () => {
  it('renders a Decimal128 as a plain decimal string, not BSON $numberDecimal', () => {
    expect(
      decimalToString(Types.Decimal128.fromString('1234567890123456.78')),
    ).toBe('1234567890123456.78');
  });

  it('passes an already-string value through', () => {
    expect(decimalToString('10.50')).toBe('10.50');
  });

  it.each([null, undefined])('maps %p to null', (value) => {
    expect(decimalToString(value)).toBeNull();
  });
});

describe('toSacStatisticResponse', () => {
  const base = {
    _id: 'abc123',
    deviceId: 'device-1',
    databaseName: 'DBSAC_Acme',
    generatedAt: new Date('2026-03-15T04:00:00.000Z'),
  };

  it('emits the three sums as strings and the gauges as numbers', () => {
    const response = toSacStatisticResponse({
      ...base,
      totalSizeGb: 512,
      balanceSum: Types.Decimal128.fromString('1000.55'),
      overdueSum: Types.Decimal128.fromString('10.00'),
      principalSum: Types.Decimal128.fromString('20.50'),
    } as unknown as SacStatisticDocument);

    expect(response.totalSizeGb).toBe(512);
    expect(response.balanceSum).toBe('1000.55');
    expect(response.overdueSum).toBe('10.00');
    expect(response.principalSum).toBe('20.50');
  });

  it('normalises every absent metric to null, so a field is never missing from the response', () => {
    const response = toSacStatisticResponse(
      base as unknown as SacStatisticDocument,
    );

    // An `undefined` would vanish from the JSON, turning "not reported" into
    // "field does not exist" for the client.
    expect(Object.values(response).filter((v) => v === undefined)).toHaveLength(
      0,
    );
    expect(response.debtors).toBeNull();
    expect(response.balanceSum).toBeNull();
  });

  it('stringifies the id and carries deviceId as provenance', () => {
    const response = toSacStatisticResponse({
      ...base,
      _id: { toString: () => 'objectid-as-string' },
    } as unknown as SacStatisticDocument);

    expect(response._id).toBe('objectid-as-string');
    expect(response.deviceId).toBe('device-1');
  });
});
