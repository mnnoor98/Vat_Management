import { Injectable, inject } from '@angular/core';
import { Observable, defer, EMPTY, expand, reduce } from 'rxjs';
import { CompanyContextService } from '../company-context.service';
import { PurchaseApiService } from './purchase-api.service';
import { SalesApiService } from './sales-api.service';
import { PurchaseInvoice, SalesInvoice } from '../../models/vat-models';
export type ReportInvoice = PurchaseInvoice | SalesInvoice;
@Injectable({ providedIn: 'root' })
export class RegisterReportService {
  private context = inject(CompanyContextService);
  private purchase = inject(PurchaseApiService);
  private sales = inject(SalesApiService);
  scan(kind: 'purchase' | 'sales'): Observable<ReportInvoice[]> {
    return defer(() => {
      const company = this.context.companyGuid();
      const fetch = (before?: string): Observable<ReportInvoice[]> => defer(() => {
        if (this.context.companyGuid() !== company) throw new Error('Company changed during report loading. Reload the report.');
        return kind === 'purchase' ? this.purchase.getInvoices({ before, take: 25 }) : this.sales.getInvoices({ before, take: 25 });
      });
      const seen = new Set<string>(); let pages = 1;
      return fetch().pipe(expand(page => {
        if (page.length < 25) return EMPTY;
        if (pages++ >= 100) throw new Error('Report scan limit reached. No complete report is available; use a server report for larger datasets.');
        return fetch(page.at(-1)!.id);
      }), reduce((all, page) => {
        if (this.context.companyGuid() !== company || page.some(i => i.companyId !== company)) throw new Error('Report company changed or returned mismatched data. Reload the report.');
        for (const item of page) {
          if (!item.id || seen.has(item.id)) throw new Error('Report cursor repeated a document. Reload before exporting.');
          seen.add(item.id);
        }
        all.push(...page); return all;
      }, [] as ReportInvoice[]));
    });
  }
}
