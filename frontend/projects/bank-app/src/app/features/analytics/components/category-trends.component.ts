import { CurrencyPipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { EChartsCoreOption } from 'echarts/core';

import { formatMonth } from '../../../core/util/dates';
import { CategoryTrends } from '../../../data-access/models/finance.models';
import { ChartComponent } from '../../../shared/chart/chart.component';
import { injectChartPalette } from '../../../shared/chart/chart-palette';
import { axisStyle, tooltipStyle, formatTry, formatTryCompact } from '../../../shared/chart/chart-theme';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';

/** Spending per category: a bar chart of totals and a month-by-month table that doubles as its accessible view. */
@Component({
  selector: 'app-category-trends',
  imports: [ChartComponent, EmptyStateComponent, CurrencyPipe, PercentPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './category-trends.component.html',
})
export class CategoryTrendsComponent {
  private readonly palette = injectChartPalette();

  readonly trends = input.required<CategoryTrends>();

  protected readonly monthLabels = computed(() => this.trends().months.map((m) => formatMonth(m)));

  protected readonly chart = computed<EChartsCoreOption | null>(() => {
    const c = this.palette();
    const axis = axisStyle(c);
    const tooltip = tooltipStyle(c);

    const categories = this.trends().categories;
    if (categories.length === 0) return null;

    const bottomUp = [...categories].reverse(); // horizontal bars are drawn from the bottom
    return {
      textStyle: { fontFamily: c.font },
      grid: { left: 8, right: 24, top: 8, bottom: 8, containLabel: true },
      tooltip: { ...tooltip, trigger: 'item', valueFormatter: (v: number) => formatTry(v) },
      xAxis: { ...axis, type: 'value', axisLabel: { ...axis.axisLabel, formatter: formatTryCompact } },
      yAxis: { ...axis, type: 'category', data: bottomUp.map((c) => c.category), splitLine: { show: false } },
      series: [
        {
          name: 'Toplam harcama',
          type: 'bar',
          barMaxWidth: 20,
          itemStyle: { color: c.series[0], borderRadius: [0, 4, 4, 0] },
          data: bottomUp.map((c) => c.total),
        },
      ],
    };
  });
}
