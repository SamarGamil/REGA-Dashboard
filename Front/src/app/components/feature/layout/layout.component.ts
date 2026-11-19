import { Component, inject, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopFilterBarComponent } from '../top-filter-bar/top-filter-bar.component';
import { PageHeaderComponent, BreadcrumbItem } from '../page-header/page-header.component';
import { ElmFooterComponent } from '../elm-footer/elm-footer.component';
import { DataAssistantComponent } from '../data-assistant/data-assistant.component';
import { SidebarService } from '../../../services/sidebar.service';
import { AssistantService } from '../../../services/assistant.service';
import { AuthService } from '../../../services/auth.service';
import { ReportingPeriodService } from '../../../services/reporting-period.service';
import { ProductService } from '../../../services/product.service';
import { ExcelPageData } from '../../../services/export.service';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, SidebarComponent, TopFilterBarComponent, PageHeaderComponent, ElmFooterComponent, DataAssistantComponent],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {
  @Input() title = '';
  @Input() subtitle = '';
  @Input() reportingPeriod?: string;
  @Input() lastUpdated?: string;
  @Input() breadcrumbs?: BreadcrumbItem[];
  // New rental-indicators dashboards manage their own filters and hide the global filter bar.
  @Input() showGlobalFilter = true;
  // Pages with their own in-page header (e.g. city-analysis) can hide the shared page header.
  @Input() showPageHeader = true;
  // Pages whose data follows the selected portal period (app-period-filter) show it in the header.
  @Input() portalPeriod = false;
  // Deprecated: export deferred; inputs retained so existing page bindings compile.
  @Input() showExport = false;
  @Input() excelData: ExcelPageData | null = null;

  readonly sidebar = inject(SidebarService);
  readonly assistant = inject(AssistantService);
  readonly auth = inject(AuthService);
  readonly showDataAssistant = environment.showDataAssistant;
  private readonly reportingPeriodSvc = inject(ReportingPeriodService);
  private readonly product = inject(ProductService);

  constructor() {
    this.reportingPeriodSvc.ensureLoaded();
  }

  // Explicit page-provided period wins; otherwise use the dynamic per-product reporting period.
  get effectiveReportingPeriod(): string | undefined {
    return this.reportingPeriod ?? this.reportingPeriodSvc.current();
  }

  get effectiveLastUpdated(): string | undefined {
    // Data-freshness line is a foreign-ownership concept only.
    if (this.product.activeProductId() !== 'foreign-ownership') return undefined;
    return this.lastUpdated ?? this.reportingPeriodSvc.currentLastUpdated();
  }
}
