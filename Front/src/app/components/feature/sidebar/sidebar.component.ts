import { Component, HostListener, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { LogoComponent } from '../logo/logo.component';
import { SidebarService } from '../../../services/sidebar.service';
import { ProductService } from '../../../services/product.service';
import { AssistantService } from '../../../services/assistant.service';
import { AuthService } from '../../../services/auth.service';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, LogoComponent],
  templateUrl: './sidebar.component.html',
})
export class SidebarComponent {
  private router = inject(Router);
  readonly sidebar = inject(SidebarService);
  readonly product = inject(ProductService);
  readonly assistant = inject(AssistantService);
  private auth = inject(AuthService);
  readonly switcherOpen = signal(false);
  readonly showDataAssistant = environment.showDataAssistant;

  @HostListener('document:mousedown', ['$event'])
  onDocMouseDown(e: MouseEvent): void {
    if (!(e.target as HTMLElement).closest('[data-switcher]')) this.switcherOpen.set(false);
  }

  toggleSwitcher(): void { this.switcherOpen.update((v) => !v); }
  selectProduct(id: string): void { this.switcherOpen.set(false); this.product.switchProduct(id); }
  toggleAssistant(): void { this.assistant.toggle(); }
  navigate(path: string): void { this.router.navigateByUrl(path); this.sidebar.close(); }
  isActive(path: string): boolean { return this.router.url.split('?')[0] === path; }
  logout(): void { this.sidebar.close(); this.auth.logout(); }
}
