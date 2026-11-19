import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MarketMovementRow } from '@/models/rental-controls';

@Component({
  selector: 'app-market-movement',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="rounded-[20px] bg-white p-4 h-full"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
    >
      <h2 class="text-sm font-extrabold text-[#0F172A] tracking-tight mb-1">حركة السوق والأسعار</h2>
      <p class="text-xs text-[#94A3B8] mb-3">قد تختلف أحدث فترة متاحة لبعض المؤشرات حسب دورية تحديث البيانات.</p>

      <div class="overflow-hidden rounded-lg" style="border: 1px solid #F1F5F9">
        <table class="w-full text-right">
          <thead>
            <tr style="background-color: #F8FAFC">
              <th class="px-3 py-2 text-sm font-bold text-[#4B5563]">المؤشر</th>
              <th class="px-3 py-2 text-sm font-bold text-[#4B5563]">القيمة / التغير</th>
              <th class="px-3 py-2 text-sm font-bold text-[#4B5563]">التقييم</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows; track $index) {
              <tr style="border-top: 1px solid #F1F5F9">
                <td class="px-3 py-2 text-sm font-medium text-[#0F172A] leading-tight">
                  {{ row.indicator }}
                  @if (periodLabel(row.level); as period) {
                    <span class="block mt-0.5 text-xs font-normal text-[#94A3B8]">{{ period }}</span>
                  }
                </td>
                <td class="px-3 py-2 text-sm font-bold text-[#0F172A] whitespace-nowrap">{{ row.change }}</td>
                <td class="px-3 py-2">
                  <span
                    class="text-sm font-bold px-2 py-0.5 rounded-full whitespace-nowrap"
                    [style.backgroundColor]="assessConfig(row.assessment).bg"
                    [style.color]="assessConfig(row.assessment).color"
                  >
                    {{ row.assessment }}
                  </span>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class MarketMovementComponent {
  @Input({ required: true }) data!: MarketMovementRow[];

  private static readonly excluded = ['نسبة الإيجار إلى الدخل', 'العبء السكني للدخل المحدود'];

  get rows(): MarketMovementRow[] {
    return (this.data ?? []).filter((r) => !MarketMovementComponent.excluded.includes(r.indicator));
  }

  private readonly assessmentConfig: Record<string, { color: string; bg: string }> = {
    'ارتفاع': { color: '#DC2626', bg: '#FEF2F2' },
    'تراجع': { color: '#D97706', bg: '#FFFBEB' },
    'مستقر': { color: '#059669', bg: '#ECFDF5' },
    'ضغط': { color: '#DC2626', bg: '#FEF2F2' },
    'ضمن الحد': { color: '#059669', bg: '#ECFDF5' },
    'يحتاج متابعة': { color: '#D97706', bg: '#FFFBEB' },
    '—': { color: '#64748B', bg: '#F1F5F9' },
  };

  assessConfig(assessment: string): { color: string; bg: string } {
    return this.assessmentConfig[assessment] || this.assessmentConfig['مستقر'];
  }

  private static readonly quarters = ['الأول', 'الثاني', 'الثالث', 'الرابع'];
  private static readonly months = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'];

  // level comes from the workbook as e.g. "مدينة - 2026-Q3" or "مدينة - 2026-08".
  periodLabel(level: string): string | null {
    const m = /(\d{4})-(?:Q([1-4])|(\d{2}))/.exec(level ?? '');
    if (!m) return null;
    if (m[2]) return `الربع ${MarketMovementComponent.quarters[+m[2] - 1]} ${m[1]}`;
    const month = MarketMovementComponent.months[+m[3] - 1];
    return month ? `${month} ${m[1]}` : null;
  }
}
