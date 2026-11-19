import type { IndicatorFrequency } from '@/utils/indicator-labels';

export interface ImpactData {
  available: boolean;
  beforeValue: string;
  afterValue: string;
  change: string;
  description: string;
}

export interface CityData {
  id: string;
  name: string;
  operationalStatus: string;
  classification: string;
  classificationLabel: string;
  cagr: string;
  housingBurden: string;
  investmentReturn: string;
  actualRentalGrowth: string;
  vacancyRate: string;
  rentToIncome: string;
  impactData: ImpactData;
  recommendation: string;
  priority: number;
}

export interface NationalSummaryData {
  acceleratedCities: number;
  elevatedCities: number;
  sustainableCities: number;
  regulationActivated: number;
  detailedStudy: number;
  proactiveMonitoring: number;
  routineMonitoring: number;
}

export interface OperationalStage { key: string; label: string; count: number; }
export interface ClassificationLegendItem { key: string; label: string; range: string; colorClass: string; }
export interface Recommendation { city: string; text: string; }

export interface OverviewData {
  cities: CityData[];
  nationalSummary: NationalSummaryData;
  operationalStages: OperationalStage[];
  classificationLegend: ClassificationLegendItem[];
  recommendations: Recommendation[];
}

export type IndicatorStatus = 'ضمن الحد' | 'يحتاج متابعة' | 'مرتفع' | 'ضغط' | 'منخفض' | '—' | 'دون الحد' | 'متابعة' | 'متجاوز';

export interface CityIndicatorValue {
  cityName: string;
  cityId: string;
  value: number;
  displayValue: string;
  status: IndicatorStatus;
  sortOrder: number;
}

export interface IndicatorPanelData {
  id: string;
  title: string;
  icon: string;
  definition: string;
  thresholdRule: string;
  thresholdSummary: string;
  frequency: IndicatorFrequency;
  periodLabel: string;
  cities: CityIndicatorValue[];
}

export interface HistoricalCityRow {
  cityName: string;
  values: { [year: string]: number | null };
  displayValues: { [year: string]: string };
}

export interface HistoricalThreshold { label: string; min: number; max: number; cssClass: string; }

export interface HistoricalIndicatorData {
  id: string;
  title: string;
  years: string[];
  cities: HistoricalCityRow[];
  thresholdType: 'higher_is_pressure' | 'higher_is_positive' | 'lower_is_pressure';
  thresholds: HistoricalThreshold[];
}

export interface GapYearKpi { year: number; gap: number; actualGap: number | null; }
export interface GapMonthKpi { period: string; label: string; gap: number; }
export interface GapKpis {
  firstYear: GapYearKpi | null;
  secondYear: GapYearKpi | null;
  monthExpected: GapMonthKpi | null;
  monthActual: GapMonthKpi | null;
}
export interface GapAnnualPoint { year: number; supply: number; demand: number; gap: number; expectedGap: number; isActual: boolean; }
export interface GapMonthlyPoint { period: string; label: string; supply: number; demand: number; gap: number; expectedGap: number; isActual: boolean; }

// Gap = supply − demand (negative = shortage).
export interface SupplyDemandGapCity {
  cityId: string;
  cityName: string;
  vacancyAssumptionPct: number | null;
  kpis: GapKpis;
  annual: GapAnnualPoint[];
  monthly: GapMonthlyPoint[];
  lastActualPeriod: string | null;
}

export interface IndicatorsData {
  currentPeriodIndicators: IndicatorPanelData[];
  historicalIndicators: HistoricalIndicatorData[];
  supplyDemandGap: SupplyDemandGapCity[];
  currentToHistoricalMap: { [currentId: string]: string };
}

export interface KpiItem { label: string; value: string; quarterlyChange: string; status: string; trend: 'up' | 'down' | 'stable'; frequency: IndicatorFrequency; periodLabel: string; }
export interface RentalIndicatorPoint { period: string; value: number; isLast: boolean; }
export interface RentalIndicatorChart { label: string; value: string; status: string; trend: 'up' | 'down' | 'stable'; points: RentalIndicatorPoint[]; }
export interface MarketMovementRow { indicator: string; level: string; change: string; assessment: string; }
export interface TabSeries { name: string; data: (number | null)[]; }
export interface TabThreshold { value: number; label: string; }
export interface MarketAnalysisTab {
  id: string; label: string; insight: string;
  chartType: 'bar' | 'line'; stacked: boolean;
  series: TabSeries[]; categories: string[];
  threshold?: TabThreshold;
  tableColumns: string[]; tableRows: string[][];
}
export interface ExecutiveRecommendation { priority: number; priorityLabel: string; recommendation: string; description: string; }
export interface MonitorItem { item: string; currentStatus: string; trend: 'up' | 'down' | 'stable'; priority: string; }
export interface CityJourneyStage { label: string; isCurrent: boolean; isCompleted: boolean; date?: string; }
export interface StatusBadge { label: string; color: string; bgColor: string; }
export interface MediaSentimentPoint { period: string; monthLabel: string; overallSentiment: string; positivePct: number; neutralPct: number; negativePct: number; discussionSharePct: number; topHeadlines: string[]; positiveImpressions: string[]; neutralImpressions: string[]; negativeImpressions: string[]; }
export interface MediaMonitoring { available: boolean; period: string; monthLabel: string; overallSentiment: string; positivePct: number; neutralPct: number; negativePct: number; topHeadlines: string[]; positiveImpressions: string[]; neutralImpressions: string[]; negativeImpressions: string[]; history: MediaSentimentPoint[]; }
export interface ExecutiveSummary { bullets: string[]; statusBadge: StatusBadge; }

export interface CityAnalysisData {
  cityName: string;
  operationalStatus: string;
  classification: string;
  classificationColor: string;
  period: string;
  lastUpdate: string;
  executiveSummary: ExecutiveSummary;
  mediaMonitoring: MediaMonitoring;
  kpis: KpiItem[];
  rentalIndicatorCharts: RentalIndicatorChart[];
  rentalIncreaseSeries?: RentalIndicatorPoint[];
  marketMovementTable: MarketMovementRow[];
  marketAnalysisTabs: MarketAnalysisTab[];
  recommendations: ExecutiveRecommendation[];
  whatToMonitor: MonitorItem[];
  cityJourney: CityJourneyStage[];
}

export interface CityListItem { cityId: string; cityName: string; operationalStatus: string; classification: string; classificationColor: string; }

export interface CityMarketMetric {
  id: string; name: string; operationalStatus: string; classification: string;
  cagr: number | null; rentalIncrease: number | null; housingBurden: number | null;
  investmentReturn: number | null; actualRentalGrowth: number | null; rentToIncome: number | null;
  vacancyRate: number | null; rentalTransactionCount: number | null; avgRentalTransactionValue: number | null;
  salesTransactionCount: number | null; avgSalesTransactionValue: number | null; actualRentCpi: number | null;
  generalInflationCpi: number | null; apartmentRentIndex: number | null; floorRentIndex: number | null; villaRentIndex: number | null;
}
export interface MarketIndicatorDef { id: string; label: string; unit: string; isPercentage: boolean; higherIs: 'positive' | 'negative' | 'neutral'; threshold: number; thresholdLabel: string; }
export interface MarketRecommendation { priority: number; title: string; affectedCities: string; reason: string; cta: string; action: string; }
export interface MarketAnalysisData { lastQuarter: string; cities: CityMarketMetric[]; indicators: MarketIndicatorDef[]; recommendations: MarketRecommendation[]; }
