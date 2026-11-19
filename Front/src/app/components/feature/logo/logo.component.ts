import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-logo',
  standalone: true,
  template: `<img [src]="src" alt="الهيئة العامة للعقار" [class]="imgClass" />`,
})
export class LogoComponent {
  @Input() imgClass = 'h-12 w-auto';
  @Input() variant: 'default' | 'white' = 'default';
  get src(): string {
    return this.variant === 'white' ? 'rega-logo-white.webp' : 'logo.png';
  }
}
