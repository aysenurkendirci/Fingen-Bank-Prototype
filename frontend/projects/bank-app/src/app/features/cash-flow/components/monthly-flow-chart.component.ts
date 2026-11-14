import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { EChartsCoreOption } from 'echarts/core';

import { formatMonth } from '../../../core/util/dates';
import { ProjectionMonth } from '../../../data-access/models/finance.models';
import { ChartComponent } from '../../../shared/chart/chart.component';
import { injectChartPalette } from '../../../shared/chart/chart-palette';
import { axisStyle, tooltipStyle, formatTry, formatTryCompact } from '../../../shared/chart/chart-theme';

/** Monthly income against all outflows: two series on one axis, both in TRY. */
@Component({
  selector: 'app-monthly-flow-chart',
  imports: [ChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `<app-chart class="h-64" [option]="option()" label="Aylık gelir ve toplam çıkış karşılaştırması. Ayrıntılar aylık özet tablosunda." />`,
})
export class MonthlyFlowChartComponent {
  private readonly palette = injectChartPalette();

  readonly months = input.required<readonly ProjectionMonth[]>();

  protected readonly option = computed<EChartsCoreOption>(() => {
    const c = this.palette();
    const axis = axisStyle(c);
    const tooltip = tooltipStyle(c);

    const months = this.months();
    const bar = (name: string, color: string, values: number[]) => ({
      name,
      type: 'bar',
      barMaxWidth: 24,
      barGap: '10%',
      itemStyle: { color, borderRadius: [4, 4, 0, 0] },
      data: values,
    });

    return {
      textStyle: { fontFamily: c.font },
      legend: { top: 0, left: 0, icon: 'roundRect', itemWidth: 12, itemHeight: 12, textStyle: { color: c.textSecondary } },
      grid: { left: 8, right: 8, top: 36, bottom: 8, containLabel: true },
      tooltip: { ...tooltip, trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: (v: number) => formatTry(v) },
      xAxis: { ...axis, type: 'category', data: months.map((m) => formatMonth(m)), splitLine: { show: false } },
      yAxis: { ...axis, type: 'value', axisLabel: { ...axis.axisLabel, formatter: formatTryCompact } },
      series: [
        bar('Gelir', c.series[0], months.map((m) => m.income)),
        bar('Çıkış', c.series[1], months.map((m) => m.outflows)),
      ],
    };
  });
}
