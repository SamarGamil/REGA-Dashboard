import { CHART_COLORS } from './chart-theme';

const OTHER_COLOR = '#94A3B8';

// One fixed color per property category so every chart and legend agrees regardless of sort order.
export const PROPERTY_TYPE_COLORS: Readonly<Record<string, string>> = {
  'مباني سكنية': CHART_COLORS.primary,
  'أرض شاغرة': CHART_COLORS.accent,
  'شقق سكنية': '#063A68',
  'دور': '#A2CD27',
  'مباني متعددة الأغراض': '#06641F',
  'فئات أخرى': OTHER_COLOR,
};

export function propertyTypeColor(name: string): string {
  return PROPERTY_TYPE_COLORS[name] ?? OTHER_COLOR;
}
