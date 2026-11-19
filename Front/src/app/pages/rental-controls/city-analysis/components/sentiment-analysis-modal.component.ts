import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  OnDestroy,
  HostListener,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MediaMonitoring } from '@/models/rental-controls';

interface Insight {
  title: string;
  percent: number;
  color: string;
  softBg: string;
  textColor: string;
  items: string[];
}

interface BarSegment {
  label: string;
  percent: number;
  color: string;
}

@Component({
  selector: 'app-sentiment-analysis-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sentiment-analysis-modal.component.html',
})
export class SentimentAnalysisModalComponent implements OnInit, OnDestroy {
  @Input({ required: true }) cityName!: string;
  @Input({ required: true }) data!: MediaMonitoring;
  @Output() close = new EventEmitter<void>();

  isVisible = signal(false);

  private closeTimer: number | null = null;
  private rafId: number | null = null;

  get insightColumns(): Insight[] {
    return [
      {
        title: 'الانطباعات الإيجابية',
        percent: this.data.positivePct,
        color: '#059669',
        softBg: '#F0FDF4',
        textColor: '#059669',
        items: this.data.positiveImpressions ?? [],
      },
      {
        title: 'الانطباعات المحايدة',
        percent: this.data.neutralPct,
        color: '#CBD5E1',
        softBg: '#F8FAFC',
        textColor: '#0F172A',
        items: this.data.neutralImpressions ?? [],
      },
      {
        title: 'الانطباعات السلبية',
        percent: this.data.negativePct,
        color: '#DC2626',
        softBg: '#FEF2F2',
        textColor: '#DC2626',
        items: this.data.negativeImpressions ?? [],
      },
    ];
  }

  get barSegments(): BarSegment[] {
    return [
      { label: 'إيجابي', percent: this.data.positivePct, color: '#059669' },
      { label: 'محايد', percent: this.data.neutralPct, color: '#CBD5E1' },
      { label: 'سلبي', percent: this.data.negativePct, color: '#DC2626' },
    ];
  }

  ngOnInit(): void {
    this.rafId = requestAnimationFrame(() => this.isVisible.set(true));
    document.body.style.overflow = 'hidden';
  }

  ngOnDestroy(): void {
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
    if (this.closeTimer !== null) window.clearTimeout(this.closeTimer);
    document.body.style.overflow = '';
  }

  @HostListener('document:keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Escape') this.handleClose();
  }

  handleClose(): void {
    this.isVisible.set(false);
    if (this.closeTimer !== null) window.clearTimeout(this.closeTimer);
    this.closeTimer = window.setTimeout(() => this.close.emit(), 280);
  }
}
