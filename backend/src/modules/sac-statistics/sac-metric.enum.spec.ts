import {
  DECIMAL_METRICS,
  SacMetric,
  isDecimalMetric,
  metricExpression,
} from './sac-metric.enum';

describe('SacMetric whitelist', () => {
  it('holds exactly the nine metrics BL-033 CA-3 names', () => {
    expect(Object.values(SacMetric)).toHaveLength(9);
  });

  it('names the three financial sums as the decimal metrics', () => {
    expect(DECIMAL_METRICS).toEqual([
      SacMetric.BALANCE_SUM,
      SacMetric.OVERDUE_SUM,
      SacMetric.PRINCIPAL_SUM,
    ]);
  });
});

describe('metricExpression', () => {
  it('casts a financial metric with $toDecimal rather than aggregating in floating point (CA-7)', () => {
    expect(metricExpression(SacMetric.BALANCE_SUM)).toEqual({
      $toDecimal: '$balanceSum',
    });
  });

  it('reads an integer gauge directly', () => {
    expect(metricExpression(SacMetric.TOTAL_SIZE_GB)).toBe('$totalSizeGb');
  });

  it('only ever produces a field path built from an enum member, never client text', () => {
    // The guarantee CA-3 rests on: every expression's field path comes from the
    // enum, so no caller-supplied string can reach the pipeline.
    for (const metric of Object.values(SacMetric)) {
      const expression = metricExpression(metric);
      const path = isDecimalMetric(metric)
        ? (expression as { $toDecimal: string }).$toDecimal
        : (expression as string);
      expect(path).toBe(`$${metric}`);
    }
  });
});
