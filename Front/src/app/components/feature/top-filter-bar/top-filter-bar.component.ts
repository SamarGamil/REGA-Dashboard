import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FilterStateService, GeographyNode } from '../../../services/filter-state.service';

@Component({
  selector: 'app-top-filter-bar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './top-filter-bar.component.html',
  host: { '(document:click)': 'onDocumentClick($event)' },
})
export class TopFilterBarComponent implements OnInit {
  openDropdown: string | null = null;
  nationalitySearch = '';
  filtersExpanded = false;

  periods = [
    { label: 'شهري', value: 'monthly' as const },
    { label: 'ربع سنوي', value: 'quarterly' as const },
    { label: 'سنوي', value: 'yearly' as const },
  ];

  constructor(public fs: FilterStateService) {}

  ngOnInit() {
    this.fs.loadOptions().subscribe(options => {
      this.fs.options = options;
    });
  }

  toggleDropdown(id: string): void {
    this.openDropdown = this.openDropdown === id ? null : id;
    if (id !== 'nationality') this.nationalitySearch = '';
  }

  selectTransactionType(value: string | null): void {
    this.fs.transactionType = value;
    this.openDropdown = null;
    this.fs.emitChange();
  }

  selectOwnerType(value: string | null): void {
    this.fs.ownerType = value;
    this.openDropdown = null;
    this.fs.emitChange();
  }

  selectRegion(value: string | null): void {
    this.fs.region = value;
    this.fs.city = null;
    this.fs.district = null;
    this.openDropdown = null;
    this.fs.emitChange();
  }

  selectCity(value: string | null): void {
    this.fs.city = value;
    this.fs.district = null;
    this.openDropdown = null;
    this.fs.emitChange();
  }

  selectDistrict(value: string | null): void {
    this.fs.district = value;
    this.openDropdown = null;
    this.fs.emitChange();
  }

  toggleNationality(name: string): void {
    const idx = this.fs.nationalities.indexOf(name);
    if (idx >= 0) this.fs.nationalities.splice(idx, 1);
    else this.fs.nationalities.push(name);
    this.fs.emitChange();
  }

  clearNationalities(): void {
    this.fs.nationalities = [];
    this.openDropdown = null;
    this.fs.emitChange();
  }

  filteredNationalities(): string[] {
    if (!this.nationalitySearch) return this.fs.options.nationalities;
    return this.fs.options.nationalities.filter(n => n.includes(this.nationalitySearch));
  }

  selectPeriod(value: 'monthly' | 'quarterly' | 'yearly'): void {
    this.fs.period = value;
    this.openDropdown = null;
    this.fs.emitChange();
  }

  periodLabel(): string {
    return this.periods.find(p => p.value === this.fs.period)?.label ?? 'شهري';
  }

  periodInfoLabel(): string {
    switch (this.fs.period) {
      case 'quarterly': return 'ربع سنوي';
      case 'yearly': return 'سنوي';
      default: return 'آخر 6 فترات';
    }
  }

  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.relative')) {
      this.openDropdown = null;
    }
  }

  activeFilterCount(): number {
    let n = 0;
    if (this.fs.transactionType) n++;
    if (this.fs.ownerType) n++;
    if (this.fs.region) n++;
    if (this.fs.city) n++;
    if (this.fs.district) n++;
    if (this.fs.nationalities.length) n++;
    if (this.fs.period !== 'monthly') n++;
    return n;
  }
}
