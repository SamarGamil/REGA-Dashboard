import { Injectable, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import {
  PRODUCTS, getActiveProductId, getProductById, type NavPage, type NavSection,
} from '../config/navigation';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private router = inject(Router);
  private pathname = signal<string>(this.router.url.split('?')[0] || '/');

  readonly products = PRODUCTS;
  readonly activeProductId = computed(() => getActiveProductId(this.pathname()));
  readonly activeProduct = computed(() => getProductById(this.activeProductId())!);
  readonly navSections = computed<NavSection[] | null>(() => this.activeProduct().sections ?? null);
  readonly navItems = computed<NavPage[]>(() => {
    const product = this.activeProduct();
    return product.sections ? product.sections.flatMap((s) => s.pages) : [];
  });
  readonly isFlat = computed(() => this.navSections() === null);

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.pathname.set(e.urlAfterRedirects.split('?')[0]));
  }

  switchProduct(productId: string): void {
    const product = getProductById(productId);
    if (!product || product.disabled || productId === this.activeProductId()) return;
    this.router.navigateByUrl(product.defaultRoute);
  }
}
