import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { EChartsCoreOption } from 'echarts/core';

import { formatMonth } from '../../../core/util/dates';
import { ChartComponent } from '../../../shared/chart/chart.component';
import { injectChartPalette } from '../../../shared/chart/chart-palette';
import { axisStyle, tooltipStyle, formatTry, formatTryCompact } from '../../../shared/chart/chart-theme';
import { ComparisonRow, rowColour } from '../comparison-rows';

/** Each option's lowest balance per month, against a zero line. */
@Component({
  selector: 'app-scenario-chart',
  imports: [ChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `<app-chart class="h-72" [option]="option()" label="Seçeneklere göre aylık en düşük bakiye. Değerler karşılaştırma tablosunda." />`,
})
export class ScenarioChartComponent {
  private readonly palette = injectChartPalette();

  readonly rows = input.required<readonly ComparisonRow[]>();

  protected readonly option = computed<EChartsCoreOption>(() => {
    const c = this.palette();
    const axis = axisStyle(c);
    const tooltip = tooltipStyle(c);

    const rows = this.rows();
    // Up to four series are also labelled at their line end, so identity never depends on colour alone.
    const labelEnds = rows.length <= 4;

    return {
      textStyle: { fontFamily: c.font },
      legend: { top: 0, left: 0, icon: 'roundRect', itemWidth: 12, itemHeight: 12, textStyle: { color: c.textSecondary } },
      grid: { left: 8, right: labelEnds ? 72 : 16, top: 40, bottom: 8, containLabel: true },
      tooltip: { ...tooltip, trigger: 'axis', valueFormatter: (v: number) => formatTry(v) },
      xAxis: {
        ...axis,
        type: 'category',
        data: rows[0]?.metrics.months.map((m) => formatMonth(m)) ?? [],
        boundaryGap: false,
        splitLine: { show: false },
      },
      yAxis: { ...axis, type: 'value', axisLabel: { ...axis.axisLabel, formatter: formatTryCompact } },
      series: rows.map((row, i) => ({
        name: row.label,
        type: 'line',
        symbol: 'circle',
        symbolSize: 8,
        lineStyle: { width: 2, color: rowColour(row, c) },
        itemStyle: { color: rowColour(row, c), borderColor: c.surface, borderWidth: 2 },
        endLabel: { show: labelEnds, formatter: '{a}', color: c.textSecondary, fontFamily: c.font },
        data: row.metrics.months.map((m) => m.lowestBalance),
        ...(i === 0
          ? {
              markLine: {
                symbol: 'none',
                silent: true,
                lineStyle: { color: c.critical, width: 1, type: 'solid' },
                label: { formatter: '0 ₺', color: c.textSecondary, position: 'insideStartTop' },
                data: [{ yAxis: 0 }],
              },
            }
          : {}),
      })),
    };
  });
}
