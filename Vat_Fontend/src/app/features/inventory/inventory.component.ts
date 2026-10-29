import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { CompanyContextService } from '../../core/services/company-context.service';
import { StockSummaryComponent } from './components/stock-summary/stock-summary.component';
import { Mushak65TransferComponent } from './components/mushak-6-5/mushak-6-5.component';
import { TransferEntryComponent } from './components/transfer-entry/transfer-entry.component';

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    StockSummaryComponent, 
    Mushak65TransferComponent, 
    TransferEntryComponent
  ],
  template: `
    <div class="inventory-page">
      <div class="page-header no-print">
        <div>
          <div class="breadcrumb-trail">
            <span>Inventory & Stock</span> / <span>{{ getTabTitle() }}</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'ইনভেন্টরি মূল্যায়ন ও মূসক ৬.৫ স্থানান্তর চালান' : 'Warehouse Stock & Mushak 6.5 Transfer Hub' }}
          </h1>
          <p class="page-subtitle">
            Live Central Stock Balances, Warehouse Ledgers, and Inter-Branch Goods Transfer under VAT Act Section 51 & Rule 40(1)(d)
          </p>
        </div>

        <div class="header-actions">
          <div class="tab-pill-box">
            <button class="tab-btn" [class.active-tab]="activeTab === 'stock'" (click)="activeTab = 'stock'">
              Live Stock Valuation
            </button>
            <button class="tab-btn" [class.active-tab]="activeTab === 'mushak65'" (click)="activeTab = 'mushak65'">
              মূসক ৬.৫ স্থানান্তর চালান
            </button>
            <button class="tab-btn highlight-tab" [class.active-tab]="activeTab === 'newTransfer'" (click)="activeTab = 'newTransfer'">
              + New 6.5 Transfer
            </button>
          </div>
        </div>
      </div>

      <!-- Success Notification Banner -->
      <div *ngIf="successMsg" class="alert-banner alert-success no-print">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
          <polyline points="22 4 12 14.01 9 11.01"></polyline>
        </svg>
        <span>{{ successMsg }}</span>
        <button class="alert-close" (click)="successMsg = ''">&times;</button>
      </div>

      <!-- TAB 1: LIVE STOCK BALANCE & VALUATION -->
      <app-stock-summary *ngIf="activeTab === 'stock'"></app-stock-summary>

      <!-- TAB 2: MUSHAK 6.5 CENTRAL BRANCH TRANSFER CHALLAN -->
      <app-mushak-6-5 
        *ngIf="activeTab === 'mushak65'"
        (requestNewTransfer)="activeTab = 'newTransfer'"
      ></app-mushak-6-5>

      <!-- TAB 3: NEW MUSHAK 6.5 TRANSFER ENTRY FORM -->
      <app-transfer-entry 
        *ngIf="activeTab === 'newTransfer'"
        (transferCreated)="onTransferCreated($event)"
        (cancelled)="activeTab = 'mushak65'"
      ></app-transfer-entry>
    </div>
  `,
  styles: [`
    .inventory-page { display: flex; flex-direction: column; gap: 20px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; }
    .page-title { font-size: 1.5rem; font-weight: 800; }
    .page-subtitle { font-size: 0.85rem; color: var(--text-muted); }
    .breadcrumb-trail { font-size: 0.8rem; color: var(--text-dim); margin-bottom: 4px; }
    .header-actions { display: flex; align-items: center; gap: 12px; }

    .tab-pill-box { display: flex; gap: 4px; background: var(--bg-card); padding: 4px; border-radius: var(--radius-md); border: 1px solid var(--border-card); }
    .tab-btn { display: inline-flex; align-items: center; gap: 6px; background: transparent; border: none; color: var(--text-muted); padding: 7px 14px; border-radius: var(--radius-sm); font-size: 0.78rem; font-weight: 700; cursor: pointer; transition: all 0.2s ease; }
    .active-tab { background: var(--vat-emerald) !important; color: #ffffff !important; }
    .highlight-tab { color: var(--vat-teal); }

    .alert-banner { display: flex; align-items: center; gap: 10px; padding: 12px 18px; border-radius: var(--radius-md); font-size: 0.9rem; }
    .alert-success { background: rgba(16, 185, 129, 0.15); border: 1px solid #10b981; color: #34d399; }
    .alert-close { margin-left: auto; background: transparent; border: none; font-size: 1.2rem; color: inherit; cursor: pointer; }
  `]
})
export class InventoryComponent implements OnInit {
  public activeTab: 'stock' | 'mushak65' | 'newTransfer' = 'stock';
  public successMsg = '';

  private router = inject(Router);
  public context = inject(CompanyContextService);

  ngOnInit(): void {
    const url = this.router.url;
    if (url.includes('transfers')) {
      this.activeTab = 'mushak65';
    } else {
      this.activeTab = 'stock';
    }
  }

  public getTabTitle(): string {
    switch (this.activeTab) {
      case 'stock': return 'Live Stock Valuation';
      case 'mushak65': return 'Mushak 6.5 Transfer Challan';
      case 'newTransfer': return 'Issue 6.5 Transfer';
    }
  }

  public onTransferCreated(transferNumber: string): void {
    this.successMsg = `মূসক ৬.৫ স্থানান্তর চালান '${transferNumber}' সফলভাবে নিবন্ধিত ও স্টক সমন্বয় করা হয়েছে!`;
    this.activeTab = 'mushak65';
  }
}
