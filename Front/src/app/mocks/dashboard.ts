export const monthlySummary = [
  { month: '2026-01', label: 'يناير', purchases: 118, sales: 5, gifts: 38, total: 161, value: 248500000 },
  { month: '2026-02', label: 'فبراير', purchases: 132, sales: 6, gifts: 41, total: 179, value: 271200000 },
  { month: '2026-03', label: 'مارس', purchases: 145, sales: 4, gifts: 44, total: 193, value: 298700000 },
  { month: '2026-04', label: 'أبريل', purchases: 128, sales: 7, gifts: 37, total: 172, value: 262100000 },
  { month: '2026-05', label: 'مايو', purchases: 138, sales: 5, gifts: 42, total: 185, value: 285400000 },
  { month: '2026-06', label: 'يونيو', purchases: 98, sales: 5, gifts: 31, total: 134, value: 234100000 },
];

export const growthRates = [
  { month: '2026-02', label: 'فبراير', rate: 11.2 },
  { month: '2026-03', label: 'مارس', rate: 7.8 },
  { month: '2026-04', label: 'أبريل', rate: -10.9 },
  { month: '2026-05', label: 'مايو', rate: 7.6 },
  { month: '2026-06', label: 'يونيو', rate: -27.6 },
];

export const kpiSummary = {
  totalTransactions: 1024,
  totalValue: 1600000000,
  totalPurchases: 759,
  totalSales: 32,
  totalGifts: 233,
  growthRate: 5.7,
  avgTransactionValue: 2010000,
  topNationality: 'اليمن',
  topMarket: 'مدينة الرياض',
  topPropertyCategory: 'شقة سكنية',
};

export const regions = [
  { id: 1, name: 'منطقة الرياض', transactions: 342, value: 548000000, purchasePct: 74, salePct: 3, giftPct: 23 },
  { id: 2, name: 'المنطقة الشرقية', transactions: 218, value: 336000000, purchasePct: 75, salePct: 3, giftPct: 22 },
  { id: 3, name: 'منطقة مكة المكرمة', transactions: 195, value: 292000000, purchasePct: 73, salePct: 4, giftPct: 23 },
  { id: 4, name: 'منطقة المدينة المنورة', transactions: 98, value: 148000000, purchasePct: 76, salePct: 2, giftPct: 22 },
  { id: 5, name: 'منطقة القصيم', transactions: 72, value: 112000000, purchasePct: 77, salePct: 3, giftPct: 20 },
  { id: 6, name: 'منطقة عسير', transactions: 48, value: 72000000, purchasePct: 75, salePct: 4, giftPct: 21 },
  { id: 7, name: 'منطقة تبوك', transactions: 32, value: 52000000, purchasePct: 74, salePct: 3, giftPct: 23 },
  { id: 8, name: 'منطقة حائل', transactions: 19, value: 40000000, purchasePct: 76, salePct: 2, giftPct: 22 },
];

export const nationalities = [
  { id: 1, name: 'اليمن', transactions: 248, value: 372000000, avgValue: 1500000, marketShare: 24.2 },
  { id: 2, name: 'سوريا', transactions: 185, value: 296000000, avgValue: 1600000, marketShare: 18.1 },
  { id: 3, name: 'مصر', transactions: 162, value: 243000000, avgValue: 1500000, marketShare: 15.8 },
  { id: 4, name: 'الأردن', transactions: 128, value: 208000000, avgValue: 1625000, marketShare: 12.5 },
  { id: 5, name: 'فلسطين', transactions: 95, value: 133000000, avgValue: 1400000, marketShare: 9.3 },
  { id: 6, name: 'السودان', transactions: 72, value: 122400000, avgValue: 1700000, marketShare: 7.0 },
  { id: 7, name: 'باكستان', transactions: 52, value: 88400000, avgValue: 1700000, marketShare: 5.1 },
  { id: 8, name: 'الهند', transactions: 38, value: 68400000, avgValue: 1800000, marketShare: 3.7 },
  { id: 9, name: 'لبنان', transactions: 28, value: 47600000, avgValue: 1700000, marketShare: 2.7 },
  { id: 10, name: 'كندا', transactions: 16, value: 22400000, avgValue: 1400000, marketShare: 1.6 },
];

export const propertyTypes = [
  { id: 1, name: 'شقة', transactions: 348, value: 487200000, avgValue: 1400000, pct: 34.0 },
  { id: 2, name: 'فيلا', transactions: 215, value: 430000000, avgValue: 2000000, pct: 21.0 },
  { id: 3, name: 'أرض فضاء', transactions: 178, value: 356000000, avgValue: 2000000, pct: 17.4 },
  { id: 4, name: 'مبنى سكني', transactions: 128, value: 179200000, avgValue: 1400000, pct: 12.5 },
  { id: 5, name: 'دور', transactions: 85, value: 85000000, avgValue: 1000000, pct: 8.3 },
  { id: 6, name: 'عقار تجاري', transactions: 42, value: 46200000, avgValue: 1100000, pct: 4.1 },
  { id: 7, name: 'أخرى', transactions: 28, value: 16400000, avgValue: 585714, pct: 2.7 },
];

export const areaSegments = [
  { id: 1, range: '0–500 م²', min: 0, max: 500, transactions: 368, value: 404800000, avgValue: 1100000 },
  { id: 2, range: '501–1,000 م²', min: 501, max: 1000, transactions: 285, value: 427500000, avgValue: 1500000 },
  { id: 3, range: '1,001–2,000 م²', min: 1001, max: 2000, transactions: 192, value: 384000000, avgValue: 2000000 },
  { id: 4, range: '2,001–5,000 م²', min: 2001, max: 5000, transactions: 118, value: 236000000, avgValue: 2000000 },
  { id: 5, range: '5,001–10,000 م²', min: 5001, max: 10000, transactions: 42, value: 105000000, avgValue: 2500000 },
  { id: 6, range: '10,000+ م²', min: 10001, max: 999999, transactions: 19, value: 42700000, avgValue: 2247368 },
];

export const propertyTypeByMonth = [
  { month: '2026-01', label: 'يناير', 'شقة': 52, 'فيلا': 34, 'أرض فضاء': 27, 'مبنى سكني': 21, 'دور': 14, 'عقار تجاري': 7, 'أخرى': 5 },
  { month: '2026-02', label: 'فبراير', 'شقة': 58, 'فيلا': 37, 'أرض فضاء': 30, 'مبنى سكني': 24, 'دور': 15, 'عقار تجاري': 8, 'أخرى': 5 },
  { month: '2026-03', label: 'مارس', 'شقة': 64, 'فيلا': 41, 'أرض فضاء': 33, 'مبنى سكني': 26, 'دور': 16, 'عقار تجاري': 8, 'أخرى': 5 },
  { month: '2026-04', label: 'أبريل', 'شقة': 56, 'فيلا': 36, 'أرض فضاء': 29, 'مبنى سكني': 22, 'دور': 14, 'عقار تجاري': 7, 'أخرى': 5 },
  { month: '2026-05', label: 'مايو', 'شقة': 62, 'فيلا': 39, 'أرض فضاء': 32, 'مبنى سكني': 24, 'دور': 15, 'عقار تجاري': 7, 'أخرى': 5 },
  { month: '2026-06', label: 'يونيو', 'شقة': 56, 'فيلا': 28, 'أرض فضاء': 27, 'مبنى سكني': 11, 'دور': 11, 'عقار تجاري': 5, 'أخرى': 3 },
];

export const propertyTypeByNationality = [
  { nationality: 'اليمن', 'شقة': 85, 'فيلا': 52, 'أرض فضاء': 42, 'مبنى سكني': 32, 'دور': 20, 'عقار تجاري': 10, 'أخرى': 7 },
  { nationality: 'سوريا', 'شقة': 65, 'فيلا': 38, 'أرض فضاء': 32, 'مبنى سكني': 24, 'دور': 15, 'عقار تجاري': 7, 'أخرى': 5 },
  { nationality: 'مصر', 'شقة': 55, 'فيلا': 34, 'أرض فضاء': 28, 'مبنى سكني': 22, 'دور': 14, 'عقار تجاري': 6, 'أخرى': 4 },
  { nationality: 'الأردن', 'شقة': 45, 'فيلا': 27, 'أرض فضاء': 22, 'مبنى سكني': 16, 'دور': 10, 'عقار تجاري': 5, 'أخرى': 3 },
  { nationality: 'فلسطين', 'شقة': 32, 'فيلا': 20, 'أرض فضاء': 16, 'مبنى سكني': 12, 'دور': 8, 'عقار تجاري': 4, 'أخرى': 3 },
  { nationality: 'السودان', 'شقة': 25, 'فيلا': 15, 'أرض فضاء': 12, 'مبنى سكني': 10, 'دور': 6, 'عقار تجاري': 3, 'أخرى': 2 },
];

export const propertyTypeByRegion = [
  { region: 'منطقة الرياض', 'شقة': 118, 'فيلا': 72, 'أرض فضاء': 58, 'مبنى سكني': 42, 'دور': 28, 'عقار تجاري': 14, 'أخرى': 10 },
  { region: 'المنطقة الشرقية', 'شقة': 74, 'فيلا': 45, 'أرض فضاء': 38, 'مبنى سكني': 28, 'دور': 18, 'عقار تجاري': 9, 'أخرى': 6 },
  { region: 'منطقة مكة المكرمة', 'شقة': 66, 'فيلا': 40, 'أرض فضاء': 34, 'مبنى سكني': 25, 'دور': 16, 'عقار تجاري': 8, 'أخرى': 5 },
  { region: 'منطقة المدينة المنورة', 'شقة': 34, 'فيلا': 20, 'أرض فضاء': 17, 'مبنى سكني': 13, 'دور': 8, 'عقار تجاري': 4, 'أخرى': 3 },
  { region: 'منطقة القصيم', 'شقة': 25, 'فيلا': 15, 'أرض فضاء': 12, 'مبنى سكني': 9, 'دور': 6, 'عقار تجاري': 3, 'أخرى': 2 },
  { region: 'منطقة عسير', 'شقة': 16, 'فيلا': 10, 'أرض فضاء': 8, 'مبنى سكني': 6, 'دور': 4, 'عقار تجاري': 2, 'أخرى': 1 },
  { region: 'منطقة تبوك', 'شقة': 11, 'فيلا': 7, 'أرض فضاء': 6, 'مبنى سكني': 4, 'دور': 3, 'عقار تجاري': 1, 'أخرى': 1 },
  { region: 'منطقة حائل', 'شقة': 7, 'فيلا': 4, 'أرض فضاء': 3, 'مبنى سكني': 2, 'دور': 1, 'عقار تجاري': 1, 'أخرى': 0 },
];

export const areaByNationality = [
  { nationality: 'اليمن', '0–500': 89, '501–1,000': 65, '1,001–2,000': 48, '2,001–5,000': 28, '5,001–10,000': 12, '10,000+': 6 },
  { nationality: 'سوريا', '0–500': 68, '501–1,000': 48, '1,001–2,000': 35, '2,001–5,000': 21, '5,001–10,000': 8, '10,000+': 4 },
  { nationality: 'مصر', '0–500': 58, '501–1,000': 42, '1,001–2,000': 32, '2,001–5,000': 18, '5,001–10,000': 7, '10,000+': 5 },
  { nationality: 'الأردن', '0–500': 46, '501–1,000': 34, '1,001–2,000': 25, '2,001–5,000': 14, '5,001–10,000': 6, '10,000+': 3 },
  { nationality: 'فلسطين', '0–500': 34, '501–1,000': 25, '1,001–2,000': 18, '2,001–5,000': 11, '5,001–10,000': 5, '10,000+': 2 },
  { nationality: 'السودان', '0–500': 26, '501–1,000': 18, '1,001–2,000': 14, '2,001–5,000': 8, '5,001–10,000': 4, '10,000+': 2 },
  { nationality: 'باكستان', '0–500': 19, '501–1,000': 14, '1,001–2,000': 10, '2,001–5,000': 6, '5,001–10,000': 2, '10,000+': 1 },
  { nationality: 'الهند', '0–500': 14, '501–1,000': 10, '1,001–2,000': 7, '2,001–5,000': 4, '5,001–10,000': 2, '10,000+': 1 },
];

export const areaByRegion = [
  { region: 'منطقة الرياض', '0–500': 118, '501–1,000': 96, '1,001–2,000': 68, '2,001–5,000': 38, '5,001–10,000': 15, '10,000+': 7 },
  { region: 'المنطقة الشرقية', '0–500': 75, '501–1,000': 62, '1,001–2,000': 42, '2,001–5,000': 24, '5,001–10,000': 10, '10,000+': 5 },
  { region: 'منطقة مكة المكرمة', '0–500': 68, '501–1,000': 54, '1,001–2,000': 38, '2,001–5,000': 22, '5,001–10,000': 9, '10,000+': 4 },
  { region: 'منطقة المدينة المنورة', '0–500': 34, '501–1,000': 28, '1,001–2,000': 18, '2,001–5,000': 12, '5,001–10,000': 4, '10,000+': 2 },
  { region: 'منطقة القصيم', '0–500': 25, '501–1,000': 20, '1,001–2,000': 14, '2,001–5,000': 8, '5,001–10,000': 3, '10,000+': 2 },
  { region: 'منطقة عسير', '0–500': 17, '501–1,000': 13, '1,001–2,000': 9, '2,001–5,000': 6, '5,001–10,000': 2, '10,000+': 1 },
];

export const transactionValueDistribution = [
  { range: 'أقل من 500 ألف', count: 85, avgValue: 320000 },
  { range: '500 ألف – 1 مليون', count: 192, avgValue: 720000 },
  { range: '1 – 2 مليون', count: 285, avgValue: 1480000 },
  { range: '2 – 3 مليون', count: 218, avgValue: 2450000 },
  { range: '3 – 5 مليون', count: 142, avgValue: 3850000 },
  { range: '5 – 10 مليون', count: 72, avgValue: 6800000 },
  { range: 'أكثر من 10 مليون', count: 30, avgValue: 14200000 },
];

export const financialMetrics = {
  totalValue: 1600000000,
  avgValue: 2020000,
  medianValue: 1660000,
  maxValue: 6480000,
  minValue: 101000,
};

export const strategicInsights = {
  concentrationIndex: 78,
  investmentConcentrationIndex: 65,
  nationalityDiversityIndex: 42,
  geographicSpreadIndex: 31,
  hotSpots: [
    { id: 1, city: 'شمال الرياض', district: 'حي الملقا', transactions: 68, value: 136000000, growth: 24, reason: 'ارتفاع الطلب من المستثمرين اليمنيين والسوريين مع توفر مشاريع سكنية جديدة' },
    { id: 2, city: 'شرق الرياض', district: 'حي الروضة', transactions: 52, value: 88000000, growth: 18, reason: 'توسع عمراني مدعوم بمشاريع البنية التحتية الجديدة' },
    { id: 3, city: 'جدة', district: 'حي الشاطئ', transactions: 45, value: 92000000, growth: 22, reason: 'موقع ساحلي متميز يجذب مستثمرين مصريين وأردنيين' },
    { id: 4, city: 'الدمام', district: 'حي الفيصلية', transactions: 38, value: 62000000, growth: 15, reason: 'قرب من المراكز الصناعية والتجارية في المنطقة الشرقية' },
    { id: 5, city: 'الخبر', district: 'حي العقربية', transactions: 32, value: 58000000, growth: 20, reason: 'منطقة سكنية راقية مع طلب متزايد من جنسيات متعددة' },
  ],
  emergingAreas: [
    { city: 'الرياض', district: 'حي الياسمين', transactions: 28, growth: 35, trend: 'صاعد' },
    { city: 'جدة', district: 'حي الفيحاء', transactions: 22, growth: 28, trend: 'صاعد' },
    { city: 'الدمام', district: 'حي النور', transactions: 18, growth: 32, trend: 'صاعد' },
    { city: 'مكة المكرمة', district: 'حي العوالي', transactions: 15, growth: 25, trend: 'صاعد' },
    { city: 'بريدة', district: 'حي الريان', transactions: 12, growth: 22, trend: 'صاعد' },
    { city: 'أبها', district: 'حي الموظفين', transactions: 10, growth: 18, trend: 'مستقر' },
    { city: 'القطيف', district: 'حي الشاطئ', transactions: 9, growth: 15, trend: 'صاعد' },
  ],
  highGrowthRegions: [
    { region: 'منطقة تبوك', growth: 28, reason: 'مشاريع نيوم والبحر الأحمر تدفع الاستثمار' },
    { region: 'منطقة الرياض', growth: 22, reason: 'مشاريع الرؤية 2030 والبنية التحتية' },
    { region: 'المنطقة الشرقية', growth: 18, reason: 'نمو القطاع الصناعي واللوجستي' },
    { region: 'منطقة عسير', growth: 15, reason: 'تنمية قطاع السياحة والخدمات' },
  ],
  topOpportunities: [
    { id: 1, title: 'الاستثمار في شقق شمال الرياض', description: 'شريحة الشقق السكنية (500-1000 م²) في أحياء شمال الرياض تظهر أعلى عوائد مع طلب مستدام', priority: 'عالية' },
    { id: 2, title: 'تطوير أراضي الشرقية', description: 'الأراضي الفضاء (2000-5000 م²) في الدمام والخبر تمثل فرصة استثمارية مع توقعات نمو 20% سنوياً', priority: 'عالية' },
    { id: 3, title: 'الفلل في جدة', description: 'الطلب على الفلل متوسطة المساحة في أحياء شمال جدة في ارتفاع مستمر', priority: 'متوسطة' },
    { id: 4, title: 'العقارات التجارية في القصيم', description: 'نمو القطاع التجاري في بريدة وعنيزة يوفر فرصاً للاستثمار في عقارات تجارية', priority: 'متوسطة' },
    { id: 5, title: 'وحدات سكنية في تبوك', description: 'مشاريع نيوم تدفع الطلب على الوحدات السكنية متوسطة المساحة في تبوك', priority: 'عالية' },
  ],
};
