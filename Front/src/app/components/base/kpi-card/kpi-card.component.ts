import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-kpi-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './kpi-card.component.html',
})
export class KpiCardComponent {
  @Input() title = '';
  @Input() value = '';
  @Input() subtitle = '';
  @Input() trend: number | null | undefined;
  @Input() trendLabel = '';
  @Input() icon = '';
  @Input() color: 'primary' | 'accent' | 'secondary' | 'default' = 'default';
  @Input() size: 'large' | 'default' = 'default';
  @Input() className = '';

  absValue(n: number): number {
    return Math.abs(n);
  }

  get isLarge(): boolean {
    return this.size === 'large';
  }

  get colors() {
    // Identity styling: primary/accent for large cards use colored icon chips on white cards
    const iconColor = this.color === 'primary' ? '#00848D' : 
                      this.color === 'accent' ? '#E8A317' : 
                      this.color === 'secondary' ? '#52C86F' : '#4B5563';
    
    return {
      bg: 'bg-white',
      border: 'border-[#E7ECF2]',
      title: 'text-[#4B5563]',
      value: 'text-[#0F172A]',
      subtitle: 'text-[#4B5563]',
      icon: iconColor,
      iconBg: iconColor + '12', // 12 = ~7% opacity hex for colored background
      trendPositive: 'text-[#52C86F]',
      trendNegative: 'text-[#EF4444]',
    };
  }

  get containerClass(): string {
    // Identity: white card, rounded-[20px], identity shadow, border #E7ECF2
    const shadow = 'shadow-[0_1px_3px_rgba(0,47,86,0.03),_0_8px_24px_rgba(0,47,86,0.04)]';
    return `${this.colors.bg} border ${this.colors.border} ${shadow} rounded-[20px] flex items-center gap-5 ${this.isLarge ? 'p-6' : 'p-5'} ${this.className}`;
  }

  get titleClass(): string {
    return `${this.isLarge ? 'text-[14px]' : 'text-sm'} ${this.colors.title} mb-0.5`;
  }

  get valueClass(): string {
    return `${this.isLarge ? 'text-[28px]' : 'text-[20px]'} font-extrabold tracking-tight ${this.colors.value}`;
  }

  get subtitleClass(): string {
    return `text-xs ${this.colors.subtitle}`;
  }

  get iconContainerClass(): string {
    return `${this.isLarge ? 'w-14 h-14 rounded-2xl' : 'w-11 h-11 rounded-xl'} flex items-center justify-center flex-shrink-0`;
  }

  get iconClass(): string {
    return this.colors.icon;
  }

  get iconSizeClass(): string {
    return this.isLarge ? 'text-xl' : 'text-base';
  }

  get trendColor(): string {
    return this.trend != null && this.trend >= 0 ? this.colors.trendPositive : this.colors.trendNegative;
  }
}
