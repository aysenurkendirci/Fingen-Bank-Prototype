import { ChartPalette } from './chart-palette';

const tryFormatter = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 });
const tryCompact = new Intl.NumberFormat('tr-TR', { notation: 'compact', maximumFractionDigits: 1 });

export const formatTry = (value: number): string => tryFormatter.format(value);

/** Axis labels: "45 B" instead of "45.000,00 ₺" keeps the axis narrow; tooltips show the full amount. */
export const formatTryCompact = (value: number): string => tryCompact.format(value);

const tickMonth = new Intl.DateTimeFormat('tr-TR', { month: 'short' });
const tickDay = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short' });

/** Labels for a date axis: "Ara", "Oca 2027" where the year turns, "15 Ara" between months. */
export function formatDateTick(value: number): string {
  const date = new Date(value);
  if (date.getDate() !== 1) return tickDay.format(date);
  return date.getMonth() === 0 ? `${tickMonth.format(date)} ${date.getFullYear()}` : tickMonth.format(date);
}

/** Recessive axes and gridlines shared by every cartesian chart. */
export const axisStyle = (c: ChartPalette) => ({
  axisLine: { lineStyle: { color: c.grid } },
  axisTick: { show: false },
  axisLabel: { color: c.textSecondary, fontFamily: c.font, fontSize: 11 },
  splitLine: { lineStyle: { color: c.grid, width: 1, type: 'solid' as const } },
});

export const tooltipStyle = (c: ChartPalette) => ({
  backgroundColor: c.surface,
  borderColor: c.grid,
  textStyle: { color: c.textPrimary, fontFamily: c.font, fontSize: 12 },
});
