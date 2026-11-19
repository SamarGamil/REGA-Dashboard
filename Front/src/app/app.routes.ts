import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

const foPages: { path: string; import: () => Promise<any>; name: string }[] = [
  { path: 'executive-summary', import: () => import('./pages/executive-summary/executive-summary.component'), name: 'ExecutiveSummaryComponent' },
  { path: 'market-overview', import: () => import('./pages/market-overview/market-overview.component'), name: 'MarketOverviewComponent' },
  { path: 'geographic-analysis', import: () => import('./pages/geographic-analysis/geographic-analysis.component'), name: 'GeographicAnalysisComponent' },
  { path: 'nationality-analysis', import: () => import('./pages/nationality-analysis/nationality-analysis.component'), name: 'NationalityAnalysisComponent' },
  { path: 'property-analysis', import: () => import('./pages/property-analysis/property-analysis.component'), name: 'PropertyAnalysisComponent' },
  { path: 'area-analysis', import: () => import('./pages/area-analysis/area-analysis.component'), name: 'AreaAnalysisComponent' },
  { path: 'financial-analysis', import: () => import('./pages/financial-analysis/financial-analysis.component'), name: 'FinancialAnalysisComponent' },
  { path: 'strategic-insights', import: () => import('./pages/strategic-insights/strategic-insights.component'), name: 'StrategicInsightsComponent' },
];

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  { path: '', redirectTo: 'rental-indicators/city-status', pathMatch: 'full' },
  ...foPages.map(p => ({
    path: `foreign-ownership/${p.path}`,
    loadComponent: () => p.import().then(m => m[p.name]),
    canActivate: [authGuard],
  })),
  ...foPages.map(p => ({ path: p.path, redirectTo: `foreign-ownership/${p.path}`, pathMatch: 'full' as const })),
  {
    path: 'rental-indicators/city-status',
    loadComponent: () => import('./pages/rental-controls/city-status/city-status.component').then(m => m.CityStatusComponent),
    canActivate: [authGuard],
  },
  {
    path: 'rental-indicators/indicators',
    loadComponent: () => import('./pages/rental-controls/indicators/indicators.component').then(m => m.IndicatorsComponent),
    canActivate: [authGuard],
  },
  {
    path: 'rental-indicators/city-analysis',
    loadComponent: () => import('./pages/rental-controls/city-analysis/city-analysis.component').then(m => m.CityAnalysisComponent),
    canActivate: [authGuard],
  },
  {
    path: 'rental-indicators/market-analysis',
    loadComponent: () => import('./pages/rental-controls/market-analysis/market-analysis.component').then(m => m.MarketAnalysisComponent),
    canActivate: [authGuard],
  },
  {
    path: 'rental-indicators/reports',
    loadComponent: () => import('./pages/rental-controls/reports/reports.component').then(m => m.ReportsComponent),
    canActivate: [authGuard],
  },
  {
    path: 'rental-indicators/methodology',
    loadComponent: () => import('./pages/rental-controls/methodology/methodology.component').then(m => m.MethodologyComponent),
    canActivate: [authGuard],
  },
  { path: 'methodology', redirectTo: 'rental-indicators/methodology', pathMatch: 'full' },
  { path: '**', loadComponent: () => import('./pages/not-found/not-found.component').then(m => m.NotFoundComponent) },
];
