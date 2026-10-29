import { Injectable, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Injectable({
  providedIn: 'root'
})
export class CsvExportService {
  private toast = inject(ToastService);

  /**
   * Export structured data to CSV file with UTF-8 BOM for flawless Excel Bengali & English display
   */
  public exportToCsv(filename: string, headers: string[], rows: (string | number | boolean)[][]): void {
    try {
      const csvRows: string[] = [];

      // 1. Format Header
      csvRows.push(headers.map(h => this.escapeCsvValue(h)).join(','));

      // 2. Format Data Rows
      for (const row of rows) {
        csvRows.push(row.map(val => this.escapeCsvValue(val)).join(','));
      }

      // 3. Add UTF-8 BOM (\uFEFF) so Excel natively reads Unicode Bengali without gibberish
      const csvContent = '\uFEFF' + csvRows.join('\r\n');
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });

      // 4. Trigger Instant Download
      const link = document.createElement('a');
      const url = URL.createObjectURL(blob);
      const cleanFilename = filename.endsWith('.csv') ? filename : `${filename}.csv`;

      link.setAttribute('href', url);
      link.setAttribute('download', cleanFilename);
      link.style.visibility = 'hidden';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);

      this.toast.success(`এক্সেল/সিএসভি ফাইল '${cleanFilename}' সফলভাবে ডাউনলোড হয়েছে!`, 'ডাউনলোড সম্পন্ন (Export Complete)');
    } catch (err) {
      console.error('CSV Export Error:', err);
      this.toast.error('এক্সপোর্ট করার সময় সমস্যা হয়েছে। অনুগ্রহ করে পুনরায় চেষ্টা করুন।', 'এক্সপোর্ট ব্যর্থ (Export Failed)');
    }
  }

  private escapeCsvValue(val: string | number | boolean | null | undefined): string {
    if (val === null || val === undefined) return '""';
    const str = String(val);
    if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r')) {
      return `"${str.replace(/"/g, '""')}"`;
    }
    return `"${str}"`;
  }
}
