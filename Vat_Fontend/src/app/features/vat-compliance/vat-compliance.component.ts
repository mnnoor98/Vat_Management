import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { CompanyContextService } from '../../core/services/company-context.service';
import { Mushak91Component } from './components/mushak-9-1/mushak-9-1.component';
import { TaxLedgerComponent } from './components/tax-ledger/tax-ledger.component';
import { VdsCertificatesComponent } from './components/vds-certificates/vds-certificates.component';
import { Mushak610Component } from './components/mushak-6-10/mushak-6-10.component';

export type ComplianceTab = 'return91' | 'ledger' | 'vds66' | 'mushak610';

@Component({
  selector: 'app-vat-compliance',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    Mushak91Component,
    TaxLedgerComponent,
    VdsCertificatesComponent,
    Mushak610Component
  ],
  template: `
    <div class="compliance-page">
      <!-- Header & Tab Navigation -->
      <div class="page-header no-print">
        <div class="header-titles">
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'ভ্যাট রিটার্ন ও এনবিআর কমপ্লায়েন্স' : 'VAT Returns & Statutory Compliance Hub' }}
          </h1>
          <p class="page-subtitle">
            Mushak 9.1 Monthly Filing, Tax Ledger (Mushak 18), VDS (6.6), and High-Value Invoices (6.10)
          </p>
        </div>

        <div class="tab-pill-box">
          <button 
            type="button"
            class="tab-btn" 
            [class.active-tab]="activeTab === 'return91'" 
            (click)="selectTab('return91')">
            মূসক ৯.১ মাসিক রিটার্ন
          </button>
          <button 
            type="button"
            class="tab-btn" 
            [class.active-tab]="activeTab === 'ledger'" 
            (click)="selectTab('ledger')">
            চলতি ভ্যাট হিসাব (Ledger)
          </button>
          <button 
            type="button"
            class="tab-btn" 
            [class.active-tab]="activeTab === 'vds66'" 
            (click)="selectTab('vds66')">
            মূসক ৬.৬ (উৎস কর্তন)
          </button>
          <button 
            type="button"
            class="tab-btn" 
            [class.active-tab]="activeTab === 'mushak610'" 
            (click)="selectTab('mushak610')">
            মূসক ৬.১০ (২ লাখ+)
          </button>
        </div>
      </div>

      <!-- Modular Sub-Components -->
      <main class="compliance-content">
        <app-mushak-9-1 *ngIf="activeTab === 'return91'"></app-mushak-9-1>
        <app-tax-ledger *ngIf="activeTab === 'ledger'"></app-tax-ledger>
        <app-vds-certificates *ngIf="activeTab === 'vds66'"></app-vds-certificates>
        <app-mushak-6-10 *ngIf="activeTab === 'mushak610'"></app-mushak-6-10>
      </main>
    </div>
  `,
  styles: [`
    .compliance-page {
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      width: 100%;
    }

    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 1rem;
      padding: 0.5rem 0;
    }

    .page-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: var(--text-main);
      margin: 0 0 0.25rem 0;
      letter-spacing: -0.01em;
    }

    .page-subtitle {
      font-size: 0.875rem;
      color: var(--text-muted);
      margin: 0;
    }

    .tab-pill-box {
      display: inline-flex;
      background: var(--bg-surface);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-lg, 12px);
      padding: 4px;
      gap: 4px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
      flex-wrap: wrap;
    }

    .tab-btn {
      background: transparent;
      border: none;
      color: var(--text-muted);
      padding: 0.5rem 1rem;
      font-size: 0.85rem;
      font-weight: 600;
      border-radius: 8px;
      cursor: pointer;
      transition: all 0.2s ease;
      white-space: nowrap;
    }

    .tab-btn:hover {
      color: var(--text-main);
      background: var(--bg-hover);
    }

    .tab-btn.active-tab {
      background: var(--primary);
      color: #ffffff;
      box-shadow: 0 2px 6px rgba(16, 185, 129, 0.35);
    }

    .compliance-content {
      width: 100%;
    }

    @media print {
      .no-print {
        display: none !important;
      }
    }
  `]
})
export class VatComplianceComponent implements OnInit {
  public context = inject(CompanyContextService);
  private router = inject(Router);

  public activeTab: ComplianceTab = 'return91';

  private readonly routeMap: Record<ComplianceTab, string> = {
    return91: '/app/compliance/return-9-1',
    ledger: '/app/compliance/tax-ledger',
    vds66: '/app/compliance/vds-certificates',
    mushak610: '/app/compliance/mushak-6-10'
  };

  ngOnInit(): void {
    this.detectRoute();

    // Keep active tab in sync if URL changes externally
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.detectRoute();
      });
  }

  public detectRoute(): void {
    const url = this.router.url;
    if (url.includes('mushak-6-10') || url.includes('6-10')) {
      this.activeTab = 'mushak610';
    } else if (url.includes('vds-certificates') || url.includes('vds-66') || url.includes('vds')) {
      this.activeTab = 'vds66';
    } else if (url.includes('tax-ledger') || url.includes('ledger')) {
      this.activeTab = 'ledger';
    } else {
      this.activeTab = 'return91';
    }
  }

  public selectTab(tab: ComplianceTab): void {
    this.activeTab = tab;
    const targetUrl = this.routeMap[tab];
    if (targetUrl && this.router.url !== targetUrl) {
      this.router.navigateByUrl(targetUrl);
    }
  }
}
