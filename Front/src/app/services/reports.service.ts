import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ReportFormat, ReportHistoryItem, ReportPeriods, ReportStatus } from '../models/reports';

interface ReportHistoryRecord {
  id: string;
  name: string;
  type: string;
  scope: string;
  period: string;
  periodId?: string;
  format: ReportFormat;
  status: ReportStatus;
  fileName: string;
  requestedBy?: string;
  requestedAt: string;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/reports`;

  getPeriods(): Observable<ReportPeriods> {
    return this.http.get<ReportPeriods>(`${this.baseUrl}/periods`);
  }

  getHistory(): Observable<ReportHistoryItem[]> {
    return this.http.get<ReportHistoryRecord[]>(`${this.baseUrl}/history`).pipe(
      map((records) =>
        records.map((r) => ({
          id: r.id,
          name: r.name,
          type: r.type,
          scope: r.scope,
          period: r.period,
          periodId: r.periodId ?? undefined,
          format: r.format,
          date: new Date(r.requestedAt).toLocaleDateString('ar-SA'),
          status: r.status ?? 'ready',
          fileName: r.fileName,
        })),
      ),
    );
  }

  /** period: yyyy-MM (monthly) or yyyy-Qn (quarterly); omitted = current quarter. */
  generateNational(format: ReportFormat, record = true, period?: string): Observable<{ blob: Blob; fileName: string }> {
    return this.http
      .post(`${this.baseUrl}/national`, null, {
        params: reportParams(format, record, period),
        responseType: 'blob',
        observe: 'response',
      })
      .pipe(
        map((res) => ({
          blob: res.body as Blob,
          fileName:
            parseContentDisposition(res.headers.get('content-disposition')) ??
            fallbackName('national-report', format, period),
        })),
      );
  }

  generateCity(cityId: string, format: ReportFormat, record = true, period?: string): Observable<{ blob: Blob; fileName: string }> {
    return this.http
      .post(`${this.baseUrl}/city/${encodeURIComponent(cityId)}`, null, {
        params: reportParams(format, record, period),
        responseType: 'blob',
        observe: 'response',
      })
      .pipe(
        map((res) => ({
          blob: res.body as Blob,
          fileName:
            parseContentDisposition(res.headers.get('content-disposition')) ??
            fallbackName(`city-report-${cityId}`, format, period),
        })),
      );
  }
}

function reportParams(format: ReportFormat, record: boolean, period?: string): Record<string, string | boolean> {
  return period ? { format, record, period } : { format, record };
}

/** Used only if the server's file name can't be read; still carries the period (e.g. city-report-riyadh-2026-Q3.pdf). */
function fallbackName(base: string, format: ReportFormat, period?: string): string {
  return `${base}-${period || 'current'}.${format === 'word' ? 'docx' : 'pdf'}`;
}

function parseContentDisposition(header: string | null): string | undefined {
  if (!header) return undefined;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (star) return decodeURIComponent(star[1].trim());
  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : undefined;
}
