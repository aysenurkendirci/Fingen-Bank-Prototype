import { Signal, computed, inject } from '@angular/core';

import dna from '../../../design/design-dna.json';
import { Theme, ThemeService } from '../../core/ui/theme.service';

/** Chart colours by role. Series keep their slot order in both themes; only the steps differ. */
export interface ChartPalette {
  series: readonly string[];
  baseline: string;
  /** Reserved for "below zero", and always shown with a label, never alone. */
  critical: string;
  textPrimary: string;
  textSecondary: string;
  grid: string;
  surface: string;
  font: string;
}

const { color, chart, typography } = dna.design_system;
const font = `'${typography.family}', ${typography.fallback}`;

export function chartPalette(theme: Theme): ChartPalette {
  const ui = color[theme];
  return {
    ...chart[theme],
    textPrimary: ui.ink.DEFAULT,
    textSecondary: ui.ink.faint,
    grid: ui.line,
    surface: ui.surface,
    font,
  };
}

/**
 * The palette of the theme on screen. Reading it inside a chart's `computed` option makes the chart redraw
 * itself when the theme changes; ECharts paints with real colours, so CSS variables cannot do this part.
 */
export function injectChartPalette(): Signal<ChartPalette> {
  const theme = inject(ThemeService).theme;
  return computed(() => chartPalette(theme()));
}
