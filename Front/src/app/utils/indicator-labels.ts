export type IndicatorFrequency = 'monthly' | 'quarterly' | '';

// Mirrors the report's sign placement (PdfBidi.Ltr): a sign + figure keeps the sign on its left
// inside right-to-left text only when set as a left-to-right run.
function ltr(text: string): string {
  return /[\u0600-\u06FF]/.test(text) ? text : `\u202A${text}\u202C`;
}

// Mirrors the report's symbolic reference (CityReportModelBuilder.Symbolic):
// "أكبر من 6%" → "> 6%"; "9.5% فأكثر" → "≥ 9.5%" (ranges and dashes are kept).
export function symbolicReference(reference: string): string {
  const r = reference.trim();
  for (const [word, symbol] of [['أكبر من', '>'], ['أكثر من', '>'], ['أقل من', '<']] as const) {
    if (r.startsWith(`${word} `)) return ltr(`${symbol} ${r.slice(word.length + 1)}`);
  }
  for (const [word, symbol] of [['فأكثر', '≥'], ['فأقل', '≤'], ['أو أقل', '≤'], ['أو أكثر', '≥']] as const) {
    if (r.endsWith(` ${word}`)) return ltr(`${symbol} ${r.slice(0, r.length - word.length - 1)}`);
  }
  return r;
}

// The API keeps "الحد المرجعي: أكبر من X" (reports parse it); the indicators page shows it as a
// monitoring threshold with the sign, matching the exported reports' reference column.
export function withMonitoringLabel(text: string): string {
  const labeled = text.replace(/^الحد المرجعي(?! للمراقبة)\s*:/, 'الحد المرجعي للمراقبة:');
  const colon = labeled.indexOf(':');
  if (colon < 0) return labeled;
  return `${labeled.slice(0, colon + 1)} ${symbolicReference(labeled.slice(colon + 1))}`;
}

export function frequencyBadge(f: IndicatorFrequency): { label: string; bg: string; text: string } | null {
  if (f === 'monthly') return { label: 'شهري', bg: '#E8F7F6', text: '#00848D' };
  if (f === 'quarterly') return { label: 'ربعي', bg: '#EEF2F6', text: '#475569' };
  return null;
}
