import { TestBed } from '@angular/core/testing';
import { ComponentRef } from '@angular/core';
import { SacGrowthChartComponent } from './sac-growth-chart.component';
import { SacGrowthPoint } from './sac-analytics.model';

/**
 * The chart's geometry is where a plausible implementation goes quietly wrong: a
 * month with no snapshot must be a gap rather than a zero-height bar, and a
 * series that crosses zero needs a baseline the bars hang from.
 */
describe('SacGrowthChartComponent', () => {
  let ref: ComponentRef<SacGrowthChartComponent>;

  function render(points: SacGrowthPoint[], plot: 'value' | 'delta' = 'value') {
    ref.setInput('points', points);
    ref.setInput('plot', plot);
    ref.changeDetectorRef.detectChanges();
    // `columns` is protected — it is the component's contract with its own
    // template, which is exactly what these cases are about.
    return (ref.instance as unknown as { columns: () => Column[] }).columns();
  }

  interface Column {
    month: string;
    value: number | null;
    valueLabel: string;
    deltaLabel: string;
    trend: string;
    heightPercent: number;
    bottomPercent: number;
    negative: boolean;
  }

  const point = (
    month: string,
    value: number | null,
    delta: number | null = null,
    deltaPercent: number | null = null
  ): SacGrowthPoint => ({ month, value, delta, deltaPercent });

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [SacGrowthChartComponent] });
    const fixture = TestBed.createComponent(SacGrowthChartComponent);
    ref = fixture.componentRef;
    ref.setInput('titleKey', 'sacAnalytics.growthSize');
    ref.setInput('captionKey', 'sacAnalytics.growthSizeCaption');
  });

  it('scales bar heights against the largest value in the window', () => {
    const columns = render([
      point('2026-01', 50),
      point('2026-02', 100),
      point('2026-03', 75)
    ]);

    expect(columns[1].heightPercent).toBe(100);
    expect(columns[0].heightPercent).toBe(50);
    expect(columns[2].heightPercent).toBe(75);
  });

  it('renders a month with no snapshot as a gap, not a zero bar', () => {
    const columns = render([
      point('2026-01', 100),
      point('2026-02', null),
      point('2026-03', 120)
    ]);

    // The template branches on `value === null` to draw the gap marker, so a
    // null must survive as null rather than being coerced to 0 here.
    expect(columns[1].value).toBeNull();
    expect(columns[1].heightPercent).toBe(0);
  });

  it('keeps the axis anchored at zero so a narrow range is not exaggerated', () => {
    // 590 vs 660 is a 12% difference. An axis spanning only the data would draw
    // the first bar at 0% and the last at 100%, implying one is nothing.
    const columns = render([point('2026-01', 590), point('2026-02', 660)]);

    expect(columns[0].heightPercent).toBeCloseTo((590 / 660) * 100, 5);
    expect(columns[1].heightPercent).toBe(100);
  });

  it('hangs a negative value below a zero baseline rather than drawing it upward', () => {
    const columns = render(
      [point('2026-01', 100, 100), point('2026-02', 60, -40)],
      'delta'
    );

    expect(columns[1].negative).toBe(true);
    // Positive bar sits on the baseline; the negative one ends there.
    expect(columns[0].bottomPercent).toBeGreaterThan(0);
    expect(columns[1].bottomPercent).toBeLessThan(columns[0].bottomPercent);
    expect(columns[1].bottomPercent + columns[1].heightPercent).toBeCloseTo(
      columns[0].bottomPercent,
      5
    );
  });

  it('plots the delta instead of the value when asked (the activities case)', () => {
    const columns = render(
      [point('2026-01', 1_000_000, null), point('2026-02', 1_060_000, 60_000)],
      'delta'
    );

    // The lifetime counter's raw value is meaningless month to month; the bar
    // has to be the month's own volume.
    expect(columns[1].value).toBe(60_000);
  });

  it('signs the percentage label and marks its direction', () => {
    const columns = render([
      point('2026-01', 100),
      point('2026-02', 110, 10, 10),
      point('2026-03', 99, -11, -10)
    ]);

    expect(columns[1].deltaLabel).toContain('+');
    expect(columns[1].trend).toBe('up');
    expect(columns[2].trend).toBe('down');
  });

  it('shows an em dash and no direction when there is no comparable month', () => {
    const columns = render([point('2026-01', 100), point('2026-02', 110, 10, null)]);

    expect(columns[0].deltaLabel).toBe('—');
    expect(columns[0].trend).toBe('none');
    expect(columns[1].deltaLabel).toBe('—');
  });

  it('survives an all-null window without producing NaN geometry', () => {
    const columns = render([point('2026-01', null), point('2026-02', null)]);

    for (const column of columns) {
      expect(Number.isFinite(column.heightPercent)).toBe(true);
      expect(Number.isFinite(column.bottomPercent)).toBe(true);
    }
  });

  it('keeps a financial value exact in its label, never parsed to a double', () => {
    ref.setInput('metric', 'balanceSum');
    const columns = render([point('2026-01', '1234567890123456.78' as never)]);

    expect(columns[0].valueLabel).toContain('1234567890123456,78'.slice(-5));
    expect(columns[0].valueLabel).not.toContain('1234567890123456,8');
  });
});
