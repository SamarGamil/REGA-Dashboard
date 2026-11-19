import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LayoutComponent } from '@/components/feature/layout/layout.component';

// Methodology page: growth ranges + control phases per range.
// Value split into parts so numeric segments render LTR inside RTL text.
interface BidiValue {
  pre?: string;
  num: string;
  post?: string;
}

interface ThresholdRow {
  label: string;
  cadence?: string;
  value: BidiValue;
}

interface ControlPhase {
  num: number;
  title: string;
  badge: string;
  desc: string;
  rows: ThresholdRow[];
  note?: string;
}

interface GrowthRange {
  name: string;
  pathLabel: string;
  color: string;
  headBg: string;
  headBorder: string;
  cardBorder: string;
  pillBg: string;
  captionColor: string;
  range: BidiValue;
  rangeCaption: string;
  desc: string;
  interventionType: string;
  interventionDesc: string;
  cadenceChip: string;
  statusChip: string;
  phaseCadence: string;
  phases: ControlPhase[];
  closingNote?: string;
}

@Component({
  selector: 'app-methodology',
  standalone: true,
  imports: [CommonModule, LayoutComponent],
  template: `
    <app-layout
      title="منهجية التصنيف"
      subtitle="نطاقات النمو الإيجاري ومساراتها، ونوع التدخل لكل نطاق، وعتبات تفعيل الضوابط وتصعيدها وإيقافها على مستوى المدينة"
      [breadcrumbs]="[{ label: 'مؤشرات الإيجار', href: '/rental-indicators/city-status' }, { label: 'منهجية التصنيف' }]"
      [showGlobalFilter]="false"
      [showPageHeader]="true"
    >
      <div dir="rtl" class="flex flex-col gap-6">
        <!-- Intro callout -->
        <div class="bg-white border border-[#E6ECF2] border-r-[3px] border-r-[#0E8C79] rounded-xl px-5 py-4">
          <p class="m-0 text-[13.5px] leading-[1.95] text-[#3E5165]">
            بناءً على المعايير الدولية والمدخلات المحلية، تم تحديد ثلاثة نطاقات للنمو الإيجاري تعكس واقع
            السوق المحلي وتمكّن من تطبيق تدخلات متدرجة تتناسب مع حالة نمو كل مدينة بالمملكة: النمو المستدام
            <span dir="ltr">6%</span> أو أقل، النمو المرتفع <span dir="ltr">7% – 9%</span>، والنمو المتسارع
            <span dir="ltr">10%</span> فأكثر.
          </p>
        </div>

        <!-- Growth ranges -->
        <section class="flex flex-col gap-3.5">
          <div class="flex items-center gap-2.5 flex-wrap">
            <span class="w-[3px] h-[17px] rounded-sm bg-[#0E8C79]"></span>
            <h2 class="m-0 text-base font-bold text-[#0F2A44]">نطاقات النمو الإيجاري</h2>
            <span class="text-xs text-[#5B6C7E]">معدل النمو التراكمي السنوي المعدّل 2022–2025م</span>
          </div>

          <div class="grid grid-cols-[repeat(auto-fit,minmax(280px,1fr))] gap-4">
            @for (r of ranges; track r.name) {
              <article class="bg-white rounded-[14px] overflow-hidden flex flex-col border" [style.borderColor]="r.cardBorder">
                <div class="px-5 py-[18px] flex flex-col gap-2" [style.background]="r.headBg">
                  <div class="flex items-center justify-between gap-3">
                    <span class="text-[15px] font-bold" [style.color]="r.color">{{ r.name }}</span>
                    <span class="text-[10.5px] font-semibold rounded-full px-2.5 py-1 whitespace-nowrap" [style.color]="r.color" [style.background]="r.pillBg">
                      {{ r.pathLabel }}
                    </span>
                  </div>
                  <div class="text-[28px] font-bold leading-tight" [style.color]="r.color">
                    <span dir="ltr">{{ r.range.num }}</span>{{ r.range.post ? ' ' + r.range.post : '' }}
                  </div>
                  <span class="text-[11.5px]" [style.color]="r.captionColor">{{ r.rangeCaption }}</span>
                </div>
                <div class="px-5 py-[18px] flex flex-col gap-3.5">
                  <p class="m-0 text-[13px] leading-[1.85] text-[#47586B]">{{ r.desc }}</p>
                  <div class="flex flex-col gap-1.5 border-t border-[#F2F5F8] pt-3.5">
                    <span class="text-[11px] text-[#5B6C7E]">نوع التدخل</span>
                    <span class="text-[13.5px] font-bold text-[#0F2A44]">{{ r.interventionType }}</span>
                    <span class="text-[12.5px] leading-[1.8] text-[#5B6C7E]">{{ r.interventionDesc }}</span>
                  </div>
                  <div class="flex gap-2 flex-wrap">
                    <span class="text-[11px] text-[#5B6C7E] bg-[#F4F7FA] rounded-full px-[11px] py-[5px]">{{ r.cadenceChip }}</span>
                    <span class="text-[11px] rounded-full px-[11px] py-[5px]" [style.color]="r.color" [style.background]="r.headBg">{{ r.statusChip }}</span>
                  </div>
                </div>
              </article>
            }
          </div>
        </section>

        <!-- Control phases per range -->
        <section class="flex flex-col gap-3.5">
          <div class="flex items-center gap-2.5 flex-wrap">
            <span class="w-[3px] h-[17px] rounded-sm bg-[#0E8C79]"></span>
            <h2 class="m-0 text-base font-bold text-[#0F2A44]">مراحل الضوابط ومؤشراتها</h2>
            <span class="text-xs text-[#5B6C7E]">العتبات المطلوب بلوغها في كل مرحلة، لكل نطاق نمو</span>
          </div>

          <div class="grid grid-cols-[repeat(auto-fit,minmax(310px,1fr))] gap-4 items-start">
            @for (r of ranges; track r.name) {
              <article class="bg-white border border-[#E6ECF2] rounded-[14px] overflow-hidden flex flex-col">
                <div class="px-5 py-3.5 flex items-center justify-between gap-2.5 border-b" [style.background]="r.headBg" [style.borderColor]="r.headBorder">
                  <span class="text-[13.5px] font-bold" [style.color]="r.color">
                    {{ r.name }} — <span dir="ltr">{{ r.range.num }}</span>{{ r.range.post ? ' ' + r.range.post : '' }}
                  </span>
                  <span class="text-[10.5px] whitespace-nowrap" [style.color]="r.captionColor">{{ r.phaseCadence }}</span>
                </div>
                <div class="px-5 py-[18px] flex flex-col gap-[18px]">
                  @for (ph of r.phases; track ph.num; let first = $first) {
                    <div class="flex flex-col gap-2.5" [class]="first ? '' : 'border-t border-[#F2F5F8] pt-4'">
                      <div class="flex items-center gap-2">
                        <span class="w-5 h-5 rounded-md text-white text-[11px] font-bold flex items-center justify-center shrink-0" [style.background]="ph.badge">
                          {{ ph.num }}
                        </span>
                        <span class="text-[13px] font-bold text-[#0F2A44]">{{ ph.title }}</span>
                      </div>
                      <span class="text-[11.5px] leading-[1.75] text-[#5B6C7E]">{{ ph.desc }}</span>
                      <div class="flex flex-col gap-[7px]">
                        @for (row of ph.rows; track row.label) {
                          <div class="flex items-center justify-between gap-3 bg-[#F8FAFC] rounded-lg px-3 py-[9px]">
                            <span class="text-[12.5px] text-[#47586B]">
                              {{ row.label }}
                              @if (row.cadence) {
                                <span class="text-[#5B6C7E]">· {{ row.cadence }}</span>
                              }
                            </span>
                            <span class="text-[12.5px] font-bold whitespace-nowrap" [style.color]="ph.badge">
                              {{ row.value.pre ? row.value.pre + ' ' : '' }}<span dir="ltr">{{ row.value.num }}</span>{{ row.value.post ? ' ' + row.value.post : '' }}
                            </span>
                          </div>
                        }
                      </div>
                      @if (ph.note) {
                        <span class="text-[11px] leading-[1.7] text-[#47586B]">{{ ph.note }}</span>
                      }
                    </div>
                  }
                  @if (r.closingNote) {
                    <div class="border-t border-[#F2F5F8] pt-3.5">
                      <span class="text-[11.5px] leading-[1.8] text-[#5B6C7E]">{{ r.closingNote }}</span>
                    </div>
                  }
                </div>
              </article>
            }
          </div>
        </section>
      </div>
    </app-layout>
  `,
})
export class MethodologyComponent {
  ranges: GrowthRange[] = [
    {
      name: 'نمو متسارع',
      pathLabel: 'مسار تفاعلي — Reactive',
      color: '#A81E36',
      headBg: '#FDF1F3',
      headBorder: '#F6DDE2',
      cardBorder: '#F0C9D1',
      pillBg: '#FBDDE2',
      captionColor: '#8E4353',
      range: { num: '10%', post: 'فأكثر' },
      rangeCaption: 'معدل النمو السنوي المركب للإيجارات',
      desc: 'يعكس هذا النطاق ضغطاً مرتفعاً في أسعار الإيجارات يتوافق مع البيانات الدولية التي يرتبط فيها النمو ثنائي الرقم بارتفاع السوق، مما يستدعي تدخلاً تنظيمياً لوجود ضغوط كبيرة.',
      interventionType: 'كامل المدينة',
      interventionDesc: 'تثبيت أجرة العقود القائمة مع فرض حد أقصى على زيادة الأجرة، وتطبيق سقف على الزيادة الإيجارية بنسبة 0% لمدة 3 سنوات، وإلزامية التجديد التلقائي للمستأجر.',
      cadenceChip: 'دورية القياس: شهري',
      statusChip: 'تفعيل كامل للضوابط كون النمو متسارع ويحتاج توازن',
      phaseCadence: 'قياس شهري',
      phases: [
        {
          num: 1,
          title: 'مرحلة تفعيل ضوابط الإيجار',
          badge: '#A81E36',
          desc: 'يجب بلوغ نسبة التفعيل لكي يتم تطبيق نظام الضوابط الإيجارية على المدينة.',
          rows: [
            { label: 'الزيادة الإيجارية', value: { num: '10%', post: 'فأكثر' } },
            { label: 'نسبة الإيجار إلى الدخل', value: { pre: 'أكبر من', num: '30%' } },
            { label: 'العبء السكني لفئات الدخل المحدود', value: { pre: 'أكبر من', num: '30%' } },
          ],
        },
        {
          num: 2,
          title: 'مرحلة التصعيد إلى ضوابط أكثر صرامة',
          badge: '#8C1B2E',
          desc: 'يجب بلوغ نسبة التفعيل لكي يتم التصعيد لضوابط إيجارية أكثر صرامة على المدينة، ويُقيَّم احتمال التصعيد سنويًّا بدءًا من العام الثاني بعد تطبيق ضوابط الإيجار.',
          rows: [{ label: 'نسبة الإيجار إلى الدخل', value: { pre: 'أكبر من', num: '30%' } }],
          note: 'مع تصاعد الوضع خلال السنتين الماضيتين.',
        },
        {
          num: 3,
          title: 'مرحلة إيقاف ضوابط الإيجار',
          badge: '#0A6455',
          desc: 'يجب بلوغ نسبة التفعيل قبل إلغاء نظام الضوابط الإيجارية على المدينة، ويُقيَّم احتمال رفع ضوابط الإيجار كل ثلاث سنوات.',
          rows: [
            { label: 'الزيادة الإيجارية', cadence: 'شهري', value: { pre: 'أقل من', num: '7%' } },
            { label: 'نسبة الإيجار إلى الدخل', cadence: 'عند القياس', value: { num: '30%', post: 'أو أقل' } },
            { label: 'الشواغر الإيجارية', cadence: 'شهري', value: { pre: 'أكبر من', num: '5%' } },
          ],
          note: 'تقييم رفع الضوابط خلال ثلاث سنوات.',
        },
      ],
    },
    {
      name: 'نمو مرتفع',
      pathLabel: 'مسار تفاعلي — Reactive',
      color: '#8A5E10',
      headBg: '#FEF8E9',
      headBorder: '#F5E6C4',
      cardBorder: '#F1DFAE',
      pillBg: '#FAEDC8',
      captionColor: '#7A6528',
      range: { num: '7% – 9%' },
      rangeCaption: 'معدل النمو السنوي المركب للإيجارات',
      desc: 'يشير هذا النطاق إلى ضغط في أسعار الإيجارات يتجاوز نمو الإيجارات مع معدلات التضخم المعتادة، وقد يبدأ بالضغط على نمو دخل الأسرة والقدرة على تحمل التكاليف.',
      interventionType: 'جزئي (نطاق جغرافي)',
      interventionDesc: 'تطبيق الضوابط الإيجارية بصورة جزئية على أجزاء من المدينة (الأحياء المرتفعة).',
      cadenceChip: 'دورية القياس: ربعي',
      statusChip: 'تفعيل جزئي للضوابط كون النمو مرتفع ويحتاج مراقبة',
      phaseCadence: 'قياس ربعي',
      phases: [
        {
          num: 1,
          title: 'مرحلة تفعيل ضوابط الإيجار',
          badge: '#8A5E10',
          desc: 'يجب بلوغ نسبة التفعيل لكي يتم تطبيق نظام الضوابط الإيجارية على المدينة.',
          rows: [
            { label: 'الزيادة الإيجارية', value: { num: '7% – 9%' } },
            { label: 'نسبة الإيجار إلى الدخل', value: { pre: 'أكبر من', num: '30%' } },
            { label: 'العبء السكني لفئات الدخل المحدود', value: { pre: 'أكبر من', num: '30%' } },
          ],
        },
        {
          num: 2,
          title: 'مرحلة التصعيد إلى ضوابط أكثر صرامة',
          badge: '#8C1B2E',
          desc: 'يجب بلوغ نسبة التفعيل لكي يتم التصعيد لضوابط إيجارية أكثر صرامة على المدينة، ويُقيَّم احتمال التصعيد سنويًّا بدءًا من العام الثاني بعد تطبيق ضوابط الإيجار.',
          rows: [{ label: 'نسبة الإيجار إلى الدخل', value: { pre: 'أكبر من', num: '30%' } }],
          note: 'مع تصاعد الوضع خلال السنتين الماضيتين.',
        },
        {
          num: 3,
          title: 'مرحلة إيقاف ضوابط الإيجار',
          badge: '#0A6455',
          desc: 'يجب بلوغ نسبة التفعيل قبل إلغاء نظام الضوابط الإيجارية على المدينة، ويُقيَّم احتمال رفع ضوابط الإيجار كل ثلاث سنوات.',
          rows: [
            { label: 'الزيادة الإيجارية', cadence: 'ربعي', value: { pre: 'أقل من', num: '7%' } },
            { label: 'نسبة الإيجار إلى الدخل', cadence: 'عند القياس', value: { num: '30%', post: 'أو أقل' } },
            { label: 'الشواغر الإيجارية', cadence: 'ربعي', value: { pre: 'أكبر من', num: '5%' } },
          ],
          note: 'تقييم رفع الضوابط خلال ثلاث سنوات.',
        },
      ],
    },
    {
      name: 'نمو مستدام',
      pathLabel: 'مسار استباقي — Proactive',
      color: '#0A6455',
      headBg: '#EDF8F5',
      headBorder: '#D3EDE6',
      cardBorder: '#C8E7DF',
      pillBg: '#D7EFE8',
      captionColor: '#3D7268',
      range: { num: '6%', post: 'أو أقل' },
      rangeCaption: 'نمو المدينة + معدل التضخم',
      desc: 'يعكس هذا النطاق نمواً سنوياً طبيعياً لأسعار الإيجارات يتماشى مع اتجاهات التضخم بأرقام أحادية منخفضة، مما يشير إلى استقرار السوق العقاري وعدم وجود ضغوط على تحمل التكاليف.',
      interventionType: 'لا يوجد',
      interventionDesc: 'لا يتطلب أي تدخل أثناء النمو المستدام، مع مراقبة النمو المستدام للزيادة الإيجارية.',
      cadenceChip: 'مراقبة استباقية',
      statusChip: 'لا يستدعي تفعيل الضوابط الإيجارية كون النمو مستدام',
      phaseCadence: 'مراقبة استباقية',
      phases: [
        {
          num: 1,
          title: 'مرحلة تفعيل ضوابط الإيجار',
          badge: '#0A6455',
          desc: 'يجب بلوغ نسبة التفعيل لكي يتم تطبيق نظام الضوابط الإيجارية على المدينة.',
          rows: [
            { label: 'الزيادة الإيجارية', value: { num: '8%', post: 'أو أقل' } },
            { label: 'نسبة الإيجار إلى الدخل', value: { pre: 'أكبر من', num: '30%' } },
            { label: 'العبء السكني لفئات الدخل المحدود', value: { pre: 'أكبر من', num: '30%' } },
          ],
        },
        {
          num: 2,
          title: 'مرحلة التصعيد إلى ضوابط أكثر صرامة',
          badge: '#8C1B2E',
          desc: 'يجب بلوغ نسبة التفعيل لكي يتم التصعيد لضوابط إيجارية أكثر صرامة على المدينة.',
          rows: [{ label: 'نسبة الإيجار إلى الدخل', value: { pre: 'أكبر من', num: '30%' } }],
          note: 'مع تصاعد الوضع خلال السنتين الماضيتين.',
        },
        {
          num: 3,
          title: 'مرحلة إيقاف ضوابط الإيجار',
          badge: '#0A6455',
          desc: 'يجب بلوغ نسبة التفعيل قبل إلغاء نظام الضوابط الإيجارية على المدينة.',
          rows: [
            { label: 'الزيادة الإيجارية', cadence: 'ربعي', value: { pre: 'أقل من', num: '7%' } },
            { label: 'نسبة الإيجار إلى الدخل', cadence: 'عند القياس', value: { num: '30%', post: 'أو أقل' } },
            { label: 'الشواغر الإيجارية', cadence: 'ربعي', value: { pre: 'أكبر من', num: '7%' } },
          ],
          note: 'تقييم رفع الضوابط خلال ثلاث سنوات.',
        },
      ],
    },
  ];
}
