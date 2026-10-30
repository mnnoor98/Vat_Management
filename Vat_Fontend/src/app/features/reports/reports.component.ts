import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { CompanyContextService } from '../../core/services/company-context.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="reports-page">
      <div class="page-header">
        <div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'সংবিধিবদ্ধ ভ্যাট রিপোর্ট ও অডিট সেন্টার' : 'Statutory VAT Reports & Audit Hub' }}
          </h1>
          <p class="page-subtitle">
            Open register previews for review. Availability depends on verified source data; previews do not certify statutory compliance.
          </p>
        </div>
      </div>

      <a routerLink="/app/reports/net-ledger" class="btn btn-primary">Correction-inclusive net ledger and summary</a>
      <div class="reports-grid">
      <a routerLink="/app/sales/payments" class="btn btn-secondary">Payments and customer receivables</a>
        <!-- Mushak 6.1 Report Card -->
        <div class="vat-card report-card">
          <div class="report-badge">মূসক-৬.১</div>
          <h3 class="report-title">Purchase Register (ক্রয় হিসাব পুস্তক)</h3>
          <p class="report-desc">Original purchase posting snapshots across all loaded pages. Later reversals are separate.</p>
          <div class="report-actions">
            <a routerLink="/app/purchases/mushak-6-1" class="btn btn-secondary btn-sm">Interactive View</a>
            <button class="btn btn-primary btn-sm" (click)="downloadReport('6.1')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Review sources
            </button>
          </div>
        </div>

        <!-- Mushak 6.2 Report Card -->
        <div class="vat-card report-card">
          <div class="report-badge">মূসক-৬.২</div>
          <h3 class="report-title">Sales Register (বিক্রয় হিসাব পুস্তক)</h3>
          <p class="report-desc">Original sales posting snapshots across all loaded pages. Credit/debit notes and payments are separate.</p>
          <div class="report-actions">
            <a routerLink="/app/sales/mushak-6-2" class="btn btn-secondary btn-sm">Interactive View</a>
            <button class="btn btn-primary btn-sm" (click)="downloadReport('6.2')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Review sources
            </button>
          </div>
        </div>

        <!-- Mushak 6.3 Challan Hub -->
        <div class="vat-card report-card">
          <div class="report-badge">মূসক-৬.৩</div>
          <h3 class="report-title">VAT Tax Invoice (কর চালানপত্র)</h3>
          <p class="report-desc">Choose a sales invoice for operational review. Statutory invoice certification requires validated document mapping.</p>
          <div class="report-actions">
            <a routerLink="/app/sales/invoices" class="btn btn-secondary btn-sm">Choose invoice</a>
            <button class="btn btn-primary btn-sm" (click)="downloadReport('6.3')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Review sources
            </button>
          </div>
        </div>

        <!-- Mushak 4.3 Coefficient -->
        <div class="vat-card report-card">
          <div class="report-badge">মূসক-৪.৩</div>
          <h3 class="report-title">Input-Output Coefficient (সহগ ঘোষণা)</h3>
          <p class="report-desc">Saved manufacturing material/cost snapshots and internal approval workflow.</p>
          <div class="report-actions">
            <a routerLink="/app/manufacturing/coefficient-4-3" class="btn btn-secondary btn-sm">View Declaration</a>
            <button class="btn btn-primary btn-sm" (click)="downloadReport('4.3')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Review sources
            </button>
          </div>
        </div>

        <!-- Mushak 9.1 Return -->
        <div class="vat-card report-card">
          <div class="report-badge">মূসক-৯.১</div>
          <h3 class="report-title">Monthly VAT Return (মাসিক দাখিলপত্র)</h3>
          <p class="report-desc">Review available return source data. External filing is unavailable until its integration and mapping are validated.</p>
          <div class="report-actions">
            <a routerLink="/app/compliance/return-9-1" class="btn btn-secondary btn-sm">Return review</a>
            <button class="btn btn-primary btn-sm" (click)="downloadReport('9.1')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Review sources
            </button>
          </div>
        </div>

        <!-- Mushak 6.6 VDS Certificate -->
        <div class="vat-card report-card">
          <div class="report-badge">মূসক-৬.৬</div>
          <h3 class="report-title">VDS Certificate (উৎসে কর্তন সনদপত্র)</h3>
          <p class="report-desc">Review available withholding records. Certificate issuance requires validated source data and mapping.</p>
          <div class="report-actions">
            <a routerLink="/app/compliance/vds-certificates" class="btn btn-secondary btn-sm">VDS Register</a>
            <button class="btn btn-primary btn-sm" (click)="downloadReport('6.6')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Review sources
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .reports-page { display: flex; flex-direction: column; gap: 20px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; }
    .page-title { font-size: 1.5rem; font-weight: 800; }
    .page-subtitle { font-size: 0.85rem; color: var(--text-muted); }
    .reports-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 20px; }
    .report-card { display: flex; flex-direction: column; gap: 10px; position: relative; }
    .report-badge { display: inline-block; width: fit-content; background: rgba(16, 185, 129, 0.15); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); font-size: 0.8rem; font-weight: 800; padding: 2px 8px; border-radius: var(--radius-sm); }
    .report-title { font-size: 1.05rem; font-weight: 700; color: var(--text-main); }
    .report-desc { font-size: 0.8rem; color: var(--text-muted); flex: 1; line-height: 1.5; }
    .report-actions { display: flex; gap: 10px; margin-top: 10px; }
  `]
})
export class ReportsComponent implements OnInit {
  constructor(
    public context: CompanyContextService,
    private router: Router
  ) {}

  ngOnInit(): void {}

  downloadReport(type: string): void {
    const routes: Record<string, string> = {
      '6.1': '/app/purchases/mushak-6-1',
      '6.2': '/app/sales/mushak-6-2',
      '6.3': '/app/sales/invoices',
      '4.3': '/app/manufacturing/coefficient-4-3',
      '9.1': '/app/compliance/return-9-1',
      '6.6': '/app/compliance/vds-66'
    };
    if (routes[type]) {
      this.router.navigate([routes[type]]);
    }
  }
}
