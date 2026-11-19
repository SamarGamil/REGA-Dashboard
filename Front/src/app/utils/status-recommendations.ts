export interface StatusCity { name: string; operationalStatus: string; }
export interface StatusRecommendation { city: string; text: string; }

const RULES: { status: string; text: string; label: (names: string) => string }[] = [
  {
    status: 'الضوابط مفعلة',
    text: 'الاستمرار في متابعة أثر الضوابط المفعلة وقياس التغير في الزيادة الإيجارية والعبء السكني خلال الربع القادم.',
    label: (names) => names,
  },
  {
    status: 'يتطلب دراسة تفصيلية',
    text: 'يتطلب دراسة تفصيلية لتقييم أسباب الضغط ومدى مناسبة تطبيق ضوابط إيجارية مرنة.',
    label: (names) => names,
  },
  {
    status: 'يتطلب دراسة استباقية',
    text: 'يتطلب مراقبة دورية للمدن التي تجمع بين ارتفاع العبء السكني وانخفاض العائد الاستثماري.',
    label: (names) => `المدن التي تتطلب دراسة استباقية: ${names}`,
  },
];

/** One executive recommendation per operational status, listing the selected period's cities in that status. */
export function statusRecommendations(cities: readonly StatusCity[]): StatusRecommendation[] {
  return RULES.flatMap((rule) => {
    const names = cities.filter((c) => c.operationalStatus === rule.status).map((c) => c.name);
    return names.length ? [{ city: rule.label(names.join('، ')), text: rule.text }] : [];
  });
}
