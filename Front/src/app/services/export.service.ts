import { Injectable } from '@angular/core';

export interface KpiEntry {
  label: string;
  value: string | number;
}

export interface ExcelPageData {
  kpis: KpiEntry[];
  tableHeaders: string[];
  tableRows: (string | number)[][];
}

@Injectable({ providedIn: 'root' })
export class ExportService {
  generateFilename(pageName: string, extension: 'pdf' | 'xlsx'): string {
    const today = new Date().toLocaleDateString('en-CA');
    return `${pageName} - ${today}.${extension}`;
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  async exportPdf(pageName: string): Promise<void> {
    const filename = this.generateFilename(pageName, 'pdf');

    // Set document title to the desired filename (browsers use this as default PDF name)
    const originalTitle = document.title;
    document.title = filename.replace('.pdf', '');

    // Inject a print stylesheet that hides everything except the main content
    const style = document.createElement('style');
    style.id = 'export-print-styles';
    style.textContent = `
      @media print {
        /* Hide sidebar and top bar completely */
        app-sidebar { display: none !important; width: 0 !important; min-width: 0 !important; }
        app-top-filter-bar { display: none !important; }
        .export-dropdown-container { display: none !important; }

        /* Force the outermost flex container to be a simple block taking full page width */
        .flex.h-screen.overflow-hidden {
          display: block !important;
          width: 100% !important;
          height: auto !important;
          overflow: visible !important;
        }

        /* Content wrapper: remove flex sizing, take full width */
        .flex.h-screen.overflow-hidden > .flex-1 {
          display: block !important;
          width: 100% !important;
          max-width: 100% !important;
          height: auto !important;
          overflow: visible !important;
          flex: none !important;
        }

        /* Main scrollable area */
        main.flex-1.overflow-y-auto {
          overflow: visible !important;
          height: auto !important;
          width: 100% !important;
          padding: 12px !important;
        }

        /* Grid items should respect print width */
        .grid { width: 100% !important; }

        /* Page header styling */
        .border-b.border-background-200.bg-background-50 {
          border: none !important;
          padding: 8px 16px !important;
        }

        /* Ensure charts and cards avoid page breaks */
        [echarts], app-chart-card, app-kpi-card { break-inside: avoid; }

        /* Print background colors and graphics */
        * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }

        /* Force body/html to not constrain */
        html, body { width: 100% !important; height: auto !important; overflow: visible !important; }
      }
    `;
    document.head.appendChild(style);

    // Trigger browser print dialog (user selects "Save as PDF")
    await new Promise<void>(resolve => {
      const afterPrint = () => {
        window.removeEventListener('afterprint', afterPrint);
        // Cleanup
        style.remove();
        document.title = originalTitle;
        resolve();
      };
      window.addEventListener('afterprint', afterPrint);
      window.print();
    });
  }

  async exportExcel(data: ExcelPageData, pageName: string): Promise<void> {
    const writeXlsxFile = (await import('write-excel-file/universal')).default;
    const filename = this.generateFilename(pageName, 'xlsx');

    const HEADER_FILL = '#E8F5F5';
    type Cell = { value: string | number; fontWeight?: 'bold'; fontSize?: number; backgroundColor?: string };
    const rows: (Cell | null)[][] = [];

    // Handle empty data
    if (data.kpis.length === 0 && data.tableRows.length === 0) {
      rows.push([{ value: 'لا توجد بيانات متاحة', fontWeight: 'bold', fontSize: 14 }]);
    } else {
      // KPI Summary section
      for (const kpi of data.kpis) {
        rows.push([
          { value: kpi.label, fontWeight: 'bold', fontSize: 11 },
          { value: kpi.value, fontSize: 11 },
        ]);
      }

      // Separator row
      rows.push([null]);

      // Data table headers + rows
      if (data.tableHeaders.length > 0) {
        rows.push(
          data.tableHeaders.map((header) => ({
            value: header,
            fontWeight: 'bold' as const,
            fontSize: 11,
            backgroundColor: HEADER_FILL,
          }))
        );

        for (const dataRow of data.tableRows) {
          rows.push(dataRow.map((val) => ({ value: val })));
        }
      }
    }

    // Column widths (in characters), one entry per column
    const columnCount = rows.reduce((max, row) => Math.max(max, row.length), 0);
    const columns = Array.from({ length: columnCount }, () => ({ width: 18 }));

    const blob = await writeXlsxFile(rows as any, {
      sheet: pageName,
      columns,
      rightToLeft: true,
    }).toBlob();

    this.downloadBlob(blob, filename);
  }
}
