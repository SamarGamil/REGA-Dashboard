export type ReportFormat = 'pdf' | 'word';
export type ReportStatus = 'ready' | 'generating' | 'failed';
export type ReportPeriodType = 'monthly' | 'quarterly' | 'yearly';

export interface ReportPeriodOption {
  id: string; // yyyy-MM | yyyy-Qn
  label: string;
}

export interface ReportPeriods {
  current: string;
  monthly: ReportPeriodOption[];
  quarterly: ReportPeriodOption[];
}

export interface ReportHistoryItem {
  id: string;
  name: string;
  type: string;
  scope: string;
  period: string;
  periodId?: string;
  format: ReportFormat;
  date: string;
  status: ReportStatus;
  blobUrl?: string;
  fileName?: string;
  cityId?: string;
}
