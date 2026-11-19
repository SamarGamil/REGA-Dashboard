export interface NavPage { path: string; label: string; icon: string; }
export interface NavSection { label: string; pages: NavPage[]; }
export interface ProductConfig {
  id: string; name: string; icon: string;
  defaultRoute: string; routePrefix: string;
  sections: NavSection[] | null;
  disabled?: boolean; comingSoon?: boolean;
}

export const PRODUCTS: ProductConfig[] = [
  {
    id: 'rental-indicators', name: 'مؤشرات الإيجار', icon: 'ri-building-line',
    defaultRoute: '/rental-indicators/city-status', routePrefix: '/rental-indicators',
    sections: [
      { label: 'نظرة عامة', pages: [
        { path: '/rental-indicators/city-status', label: 'حالة المدن للضوابط الإيجارية', icon: 'ri-community-line' },
        { path: '/rental-indicators/indicators', label: 'مؤشرات الضوابط الإيجارية', icon: 'ri-bar-chart-grouped-line' },
        { path: '/rental-indicators/city-analysis', label: 'تحليل المدينة', icon: 'ri-map-pin-line' },
        { path: '/rental-indicators/market-analysis', label: 'تحليل السوق الإيجاري', icon: 'ri-bar-chart-box-line' },
        { path: '/rental-indicators/reports', label: 'التقارير', icon: 'ri-file-list-3-line' },
        { path: '/rental-indicators/methodology', label: 'منهجية التصنيف', icon: 'ri-book-open-line' },
      ]},
    ],
  },
  {
    id: 'foreign-ownership', name: 'تملك الأجانب', icon: 'ri-global-line',
    defaultRoute: '/foreign-ownership/executive-summary', routePrefix: '/foreign-ownership',
    sections: [
      { label: 'نظرة عامة', pages: [
        { path: '/foreign-ownership/executive-summary', label: 'الملخص التنفيذي', icon: 'ri-dashboard-line' },
        { path: '/foreign-ownership/market-overview', label: 'نظرة عامة على السوق', icon: 'ri-line-chart-line' },
      ]},
      { label: 'تحليلات متعددة الأبعاد', pages: [
        { path: '/foreign-ownership/geographic-analysis', label: 'التحليل الجغرافي', icon: 'ri-map-pin-line' },
        { path: '/foreign-ownership/nationality-analysis', label: 'تحليل الجنسيات', icon: 'ri-global-line' },
        { path: '/foreign-ownership/property-analysis', label: 'تحليل العقارات', icon: 'ri-building-2-line' },
        { path: '/foreign-ownership/area-analysis', label: 'تحليل المساحات', icon: 'ri-ruler-line' },
        { path: '/foreign-ownership/financial-analysis', label: 'التحليل المالي', icon: 'ri-money-dollar-circle-line' },
      ]},
      { label: 'الاستراتيجية', pages: [
        { path: '/foreign-ownership/strategic-insights', label: 'الرؤى الاستراتيجية', icon: 'ri-lightbulb-line' },
      ]},
    ],
  },
];

export function getActiveProductId(pathname: string): string {
  if (pathname.startsWith('/rental-indicators')) return 'rental-indicators';
  return 'foreign-ownership';
}

export function getProductById(id: string): ProductConfig | undefined {
  return PRODUCTS.find((p) => p.id === id);
}
