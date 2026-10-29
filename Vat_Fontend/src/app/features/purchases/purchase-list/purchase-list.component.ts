import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PurchaseApiService } from '../../../core/services/api/purchase-api.service';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { CsvExportService } from '../../../core/services/csv-export.service';
import { ToastService } from '../../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { PurchaseInvoice, DocumentStatus, Branch } from '../../../core/models/vat-models';
import { MockDataService } from '../../../core/services/mock-data.service';

@Component({
  selector: 'app-purchase-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, BdtCurrencyPipe, StatusBadgeComponent],
  template: `
    <div class="purchase-page">
      <!-- Breadcrumb & Header -->
      <div class="page-header">
        <div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'ক্রয় চালান ও ইনভয়েস রেজিস্টার' : 'Purchase Invoices & BOE Register' }}
          </h1>
          <p class="page-subtitle">
            {{ context.locale() === 'bn' 
              ? 'স্থানীয় ক্রয়, আমদানি বিল অব এন্ট্রি এবং মূসক ৬.১ ভ্যাট হিসাব'
              : 'Local purchases, import bills of entry, and Section 46 input tax credits' }}
          </p>
        </div>
        <div class="header-actions">
          <button class="btn btn-secondary" (click)="exportToCsv()" title="Export Excel / CSV">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            {{ context.locale() === 'bn' ? 'এক্সেল এক্সপোর্ট' : 'Export Excel' }}
          </button>
          <a routerLink="/app/purchases/mushak-6-1" class="btn btn-secondary">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"></path></svg>
            {{ context.locale() === 'bn' ? 'মূসক ৬.১ ক্রয় বই' : 'Mushak 6.1 Register' }}
          </a>
          <a routerLink="/app/purchases/new" class="btn btn-primary">
            <span>+</span> {{ context.locale() === 'bn' ? 'নতুন ক্রয় এন্ট্রি' : '+ New Purchase Invoice' }}
          </a>
        </div>
      </div>

      <!-- Filters & Search Toolbar -->
      <div class="vat-card filter-card">
        <div class="filter-grid">
          <div class="filter-item search-item">
            <label class="vat-label">{{ context.locale() === 'bn' ? 'অনুসন্ধান' : 'Search' }}</label>
            <input
              type="text"
              class="vat-input"
              [(ngModel)]="searchQuery"
              (ngModelChange)="applyFilters()"
              [placeholder]="context.locale() === 'bn' ? 'চালান নম্বর বা সরবরাহকারীর নাম...' : 'Invoice / Supplier / BIN...'"
            />
          </div>

          <div class="filter-item">
            <label class="vat-label">{{ context.locale() === 'bn' ? 'ক্রয়ের ধরন' : 'Purchase Type' }}</label>
            <select class="vat-select" [(ngModel)]="selectedType" (ngModelChange)="applyFilters()">
              <option value="All">All Types</option>
              <option value="Local">Local Purchase</option>
              <option value="Import">Import (Bill of Entry)</option>
            </select>
          </div>

          <div class="filter-item">
            <label class="vat-label">{{ context.locale() === 'bn' ? 'স্ট্যাটাস' : 'Status' }}</label>
            <select class="vat-select" [(ngModel)]="selectedStatus" (ngModelChange)="applyFilters()">
              <option value="All">All Statuses</option>
              <option value="Draft">Draft</option>
              <option value="Submitted">Submitted</option>
              <option value="Approved">Approved</option>
              <option value="Posted">Posted</option>
            </select>
          </div>

          <div class="filter-item" *ngIf="context.isSuperAdmin()">
            <label class="vat-label">{{ context.locale() === 'bn' ? 'শাখা ফিল্টার' : 'Branch Filter' }}</label>
            <select class="vat-select" [(ngModel)]="selectedBranchFilter" (ngModelChange)="applyFilters()">
              <option value="All">All Branches (সকল শাখা)</option>
              <option *ngFor="let b of branches" [value]="b.id">{{ b.name }}</option>
            </select>
          </div>
        </div>
      </div>

      <!-- Invoices Data Table -->
      <div class="vat-card">
        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th>Invoice / BOE No</th>
                <th>Branch</th>
                <th>Supplier & BIN</th>
                <th>Type</th>
                <th>Date</th>
                <th class="text-right">Taxable Value</th>
                <th class="text-right">VAT (15%)</th>
                <th class="text-right">Total Payable</th>
                <th>Status</th>
                <th class="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let inv of filteredInvoices">
                <td>
                  <strong class="font-mono text-emerald">{{ inv.invoiceNumber }}</strong>
                  <br>
                  <small class="text-dim">Supp Ref: {{ inv.supplierInvoiceNumber }}</small>
                </td>
                <td>
                  <span class="branch-pill">
                    🏢 {{ getBranchName(inv.branchId) }}
                  </span>
                </td>
                <td>
                  <strong>{{ inv.supplierName }}</strong>
                  <br>
                  <span class="bin-tag">BIN: {{ inv.supplierBin }}</span>
                </td>
                <td>
                  <span class="badge-type" [class.import]="inv.purchaseType === 'Import'">
                    {{ inv.purchaseType }}
                  </span>
                  <div *ngIf="inv.billOfEntryNo" class="boe-meta">BOE: {{ inv.billOfEntryNo }}</div>
                </td>
                <td>{{ inv.invoiceDate }}</td>
                <td class="text-right numeric">{{ inv.subTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric text-emerald">{{ inv.totalVat | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ inv.grandTotal | bdtCurrency:false }}</td>
                <td><app-status-badge [status]="inv.status"></app-status-badge></td>
                <td class="text-right">
                  <div class="row-actions">
                    <button class="btn-action" (click)="viewInvoice(inv)" title="View Details">View</button>
                    <ng-container *ngIf="inv.status === 'Submitted'">
                      <button 
                        class="btn-action btn-approve" 
                        *ngIf="canApprove()" 
                        (click)="approveInvoice(inv.id)" 
                        title="Approve Invoice">
                        Approve
                      </button>
                      <span 
                        class="badge-restricted" 
                        *ngIf="!canApprove()" 
                        [title]="'Approval requires Checker/Approver (Active: ' + context.userRole() + ')'">
                        Read Only
                      </span>
                    </ng-container>
                    <ng-container *ngIf="inv.status === 'Approved'">
                      <button 
                        class="btn-action btn-post" 
                        *ngIf="canPost()" 
                        (click)="postInvoiceToLedger(inv.id)" 
                        title="Post to VAT & Financial Ledger">
                        Post to Ledger
                      </button>
                    </ng-container>
                  </div>
                </td>
              </tr>
              <tr *ngIf="filteredInvoices.length === 0">
                <td colspan="9" class="empty-state">No purchase invoices found matching your filters.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Invoice Detailed View Modal -->
      <div class="modal-backdrop" *ngIf="showViewModal && selectedInvoice">
        <div class="modal-card modal-lg">
          <div class="modal-header">
            <div>
              <div class="modal-badge-row">
                <span class="font-mono text-emerald font-bold text-lg">{{ selectedInvoice.invoiceNumber }}</span>
                <app-status-badge [status]="selectedInvoice.status"></app-status-badge>
                <span class="badge-type" [class.import]="selectedInvoice.purchaseType === 'Import'">
                  {{ selectedInvoice.purchaseType }} Purchase
                </span>
              </div>
              <p class="modal-subtitle">
                Supplier Ref: <strong class="font-mono">{{ selectedInvoice.supplierInvoiceNumber }}</strong> &bull; Date: {{ selectedInvoice.invoiceDate }} &bull; Period: {{ selectedInvoice.fiscalPeriod }}
              </p>
            </div>
            <button class="btn-close" (click)="closeViewModal()">✕</button>
          </div>

          <div class="modal-body">
            <!-- Supplier & Compliance Metadata Box -->
            <div class="detail-meta-box">
              <div class="meta-item">
                <span class="meta-label">Supplier Business:</span>
                <strong class="text-main">{{ selectedInvoice.supplierName }}</strong>
              </div>
              <div class="meta-item">
                <span class="meta-label">13-Digit NBR BIN:</span>
                <strong class="font-mono text-emerald">{{ selectedInvoice.supplierBin }}</strong>
              </div>
              <div class="meta-item" *ngIf="selectedInvoice.billOfEntryNo">
                <span class="meta-label">Bill of Entry (BOE):</span>
                <strong class="font-mono">{{ selectedInvoice.billOfEntryNo }}</strong>
              </div>
              <div class="meta-item">
                <span class="meta-label">Banking Payment Ref:</span>
                <span class="font-mono text-dim">{{ selectedInvoice.bankPaymentRef || 'Pending EFT/Cheque' }}</span>
              </div>
              <div class="meta-item">
                <span class="meta-label">Created By:</span>
                <span>{{ selectedInvoice.createdBy }} ({{ selectedInvoice.createdAt }})</span>
              </div>
              <div class="meta-item" *ngIf="selectedInvoice.approvedBy">
                <span class="meta-label">Authorized By:</span>
                <span class="text-emerald font-bold">{{ selectedInvoice.approvedBy }} ({{ selectedInvoice.approvedAt }})</span>
              </div>
            </div>

            <!-- Invoice Line Items Table -->
            <h4 class="modal-section-title">পণ্য ও উপকরণের বিবরণী (Line Items & Tax Breakdown)</h4>
            <div class="vat-table-container">
              <table class="vat-table">
                <thead>
                  <tr>
                    <th>Item Description</th>
                    <th>HS Code</th>
                    <th>UOM</th>
                    <th class="text-right">Qty</th>
                    <th class="text-right">Rate</th>
                    <th class="text-right">Taxable (BDT)</th>
                    <th class="text-center">VAT %</th>
                    <th class="text-right">VAT (BDT)</th>
                    <th class="text-right">VDS (BDT)</th>
                    <th>Rebate</th>
                    <th class="text-right">Total</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let l of selectedInvoice.lines">
                    <td>
                      <strong>{{ l.productName }}</strong>
                      <br><small class="text-dim font-bengali">{{ l.productNameBn }}</small>
                    </td>
                    <td><span class="hs-tag font-mono">{{ l.hsCode }}</span></td>
                    <td>{{ l.uom }}</td>
                    <td class="text-right numeric font-bold">{{ l.quantity | number }}</td>
                    <td class="text-right numeric">{{ l.unitPrice | bdtCurrency:false }}</td>
                    <td class="text-right numeric font-bold">{{ l.taxableAmount | bdtCurrency:false }}</td>
                    <td class="text-center">{{ l.vatRatePercent }}%</td>
                    <td class="text-right numeric text-emerald">{{ l.vatAmount | bdtCurrency:false }}</td>
                    <td class="text-right numeric">{{ l.vdsAmount | bdtCurrency:false }}</td>
                    <td>
                      <span class="rebate-tag" [class.yes]="l.isCreditEligible">
                        {{ l.isCreditEligible ? 'Sec 46 Eligible' : 'Ineligible' }}
                      </span>
                    </td>
                    <td class="text-right numeric font-bold">{{ l.totalAmount | bdtCurrency:false }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Financial Totals Summary -->
            <div class="modal-summary-grid">
              <div class="summary-remarks">
                <span class="meta-label">Invoice Remarks / NBR Compliance Note:</span>
                <p class="remarks-text">{{ selectedInvoice.remarks || 'Standard local purchase under Mushak 6.3 challan.' }}</p>
              </div>

              <div class="summary-breakdown">
                <div class="sum-row">
                  <span>Taxable Subtotal (মূল্য):</span>
                  <span class="font-mono font-bold">{{ selectedInvoice.subTotal | bdtCurrency }}</span>
                </div>
                <div class="sum-row">
                  <span>Input VAT (15% মূসক):</span>
                  <span class="font-mono font-bold text-emerald">{{ selectedInvoice.totalVat | bdtCurrency }}</span>
                </div>
                <div class="sum-row" *ngIf="selectedInvoice.totalVds > 0">
                  <span>VDS Withholding (উৎসে কর্তন):</span>
                  <span class="font-mono font-bold text-amber">- {{ selectedInvoice.totalVds | bdtCurrency }}</span>
                </div>
                <div class="sum-divider"></div>
                <div class="sum-row grand-total-row">
                  <span>Grand Total (সর্বমোট প্রদেয়):</span>
                  <span class="font-mono font-bold text-emerald text-lg">{{ selectedInvoice.grandTotal | bdtCurrency }}</span>
                </div>
              </div>
            </div>
          </div>

          <div class="modal-footer">
            <a routerLink="/app/purchases/mushak-6-1" class="btn btn-secondary" (click)="closeViewModal()">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"></path></svg>
              View in Mushak 6.1 (৬.১ ক্রয় বহিতে দেখুন)
            </a>
            <button 
              class="btn btn-success" 
              *ngIf="selectedInvoice.status === 'Submitted' && canApprove()" 
              (click)="approveFromModal(selectedInvoice.id)">
              Approve Invoice (অনুমোদন)
            </button>
            <span 
              class="badge-restricted" 
              *ngIf="selectedInvoice.status === 'Submitted' && !canApprove()">
              🔒 Approval requires Checker / Approver role (Active: {{ context.userRole() }})
            </span>
            <button class="btn btn-secondary" (click)="closeViewModal()">Close</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .purchase-page {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 16px;
    }

    .page-title {
      font-size: 1.5rem;
      font-weight: 800;
    }

    .page-subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
    }

    .header-actions {
      display: flex;
      gap: 12px;
    }

    .filter-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }

    @media (max-width: 768px) {
      .filter-grid {
        grid-template-columns: 1fr;
      }
    }

    .bin-tag {
      font-family: var(--font-mono);
      font-size: 0.72rem;
      color: var(--vat-teal);
    }

    .badge-type {
      font-size: 0.7rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: var(--radius-sm);
      background: rgba(59, 130, 246, 0.15);
      color: #60a5fa;
    }
    .badge-type.import {
      background: rgba(139, 92, 246, 0.15);
      color: #a78bfa;
    }

    .branch-pill {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: var(--radius-sm);
      background: rgba(14, 165, 233, 0.12);
      border: 1px solid rgba(14, 165, 233, 0.25);
      color: #38bdf8;
      display: inline-flex;
      align-items: center;
      gap: 4px;
      white-space: nowrap;
    }

    .boe-meta {
      font-size: 0.68rem;
      color: var(--text-dim);
      font-family: var(--font-mono);
      margin-top: 2px;
    }

    .font-bold { font-weight: 700; }

    .row-actions {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 6px;
    }

    .btn-action {
      background: var(--bg-card-subtle);
      border: 1px solid var(--border-card);
      color: var(--text-main);
      padding: 4px 10px;
      border-radius: var(--radius-sm);
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .btn-action:hover {
      background: var(--bg-card-hover);
      border-color: var(--border-subtle);
    }
    .btn-approve {
      background: rgba(16, 185, 129, 0.15);
      border-color: rgba(16, 185, 129, 0.3);
      color: #34d399;
    }
    .btn-approve:hover {
      background: #059669;
      color: #ffffff;
    }
    .btn-post {
      background: rgba(14, 165, 233, 0.15);
      border-color: rgba(14, 165, 233, 0.35);
      color: #38bdf8;
      font-weight: 700;
    }
    .btn-post:hover {
      background: #0ea5e9;
      color: #ffffff;
      box-shadow: 0 0 10px rgba(14, 165, 233, 0.4);
    }

    .empty-state {
      text-align: center;
      padding: 30px;
      color: var(--text-muted);
    }

    /* Modal Styles */
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.75);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
      padding: 20px;
    }
    .modal-card {
      background: var(--bg-card);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-xl);
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .modal-lg {
      width: 950px;
      max-width: 95vw;
      max-height: 90vh;
    }
    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      padding: 20px 24px;
      border-bottom: 1px solid var(--border-card);
      background: var(--bg-card-subtle);
    }
    .modal-badge-row {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-bottom: 4px;
    }
    .modal-subtitle {
      font-size: 0.8rem;
      color: var(--text-muted);
    }
    .modal-body {
      padding: 24px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 18px;
    }
    .btn-close {
      background: transparent;
      border: none;
      font-size: 1.3rem;
      color: var(--text-muted);
      cursor: pointer;
    }
    .btn-close:hover { color: #ffffff; }

    .detail-meta-box {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 12px;
      background: var(--bg-card-subtle);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 16px;
      font-size: 0.85rem;
    }
    .meta-item { display: flex; flex-direction: column; gap: 2px; }
    .meta-label { font-size: 0.75rem; color: var(--text-dim); }
    .modal-section-title { font-size: 0.95rem; font-weight: 700; color: var(--vat-teal); margin-bottom: 4px; }
    .hs-tag { background: rgba(59, 130, 246, 0.15); color: #60a5fa; padding: 2px 6px; border-radius: var(--radius-sm); font-size: 0.75rem; }
    .rebate-tag { font-size: 0.72rem; font-weight: 700; padding: 2px 8px; border-radius: 9999px; background: var(--bg-card-subtle); color: var(--text-dim); }
    .rebate-tag.yes { background: rgba(16, 185, 129, 0.15); color: #34d399; }

    .modal-summary-grid {
      display: grid;
      grid-template-columns: 1.2fr 1fr;
      gap: 20px;
      align-items: flex-start;
    }
    @media (max-width: 768px) {
      .modal-summary-grid { grid-template-columns: 1fr; }
    }
    .summary-remarks {
      background: var(--bg-card-subtle);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 14px;
      font-size: 0.85rem;
    }
    .remarks-text { margin-top: 4px; color: var(--text-main); font-style: italic; }
    .summary-breakdown {
      background: var(--bg-card-subtle);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      font-size: 0.85rem;
    }
    .sum-row { display: flex; justify-content: space-between; }
    .sum-divider { height: 1px; background: var(--border-card); margin: 4px 0; }
    .grand-total-row { font-size: 1rem; font-weight: 800; color: var(--text-main); }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      padding: 16px 24px;
      border-top: 1px solid var(--border-card);
      background: var(--bg-card-subtle);
    }

    .badge-restricted {
      font-size: 0.72rem;
      font-weight: 700;
      color: var(--text-dim);
      background: rgba(255, 255, 255, 0.05);
      border: 1px dashed var(--border-card);
      padding: 3px 8px;
      border-radius: var(--radius-sm);
      display: inline-flex;
      align-items: center;
    }
  `]
})
export class PurchaseListComponent implements OnInit {
  private csvExport = inject(CsvExportService);
  private toast = inject(ToastService);

  public invoices: PurchaseInvoice[] = [];
  public filteredInvoices: PurchaseInvoice[] = [];
  public branches: Branch[] = [];
  public searchQuery: string = '';
  public selectedType: string = 'All';
  public selectedStatus: string = 'All';
  public selectedBranchFilter: string = 'All';

  // Invoice Detailed View State
  public selectedInvoice: PurchaseInvoice | null = null;
  public showViewModal: boolean = false;

  private mockData = inject(MockDataService);

  constructor(
    public context: CompanyContextService,
    private purchaseApi: PurchaseApiService
  ) {}

  ngOnInit(): void {
    this.branches = this.mockData.branches;
    this.loadInvoices();
  }

  loadInvoices(): void {
    this.purchaseApi.getInvoices().subscribe(data => {
      this.invoices = data;
      this.applyFilters();
    });
  }

  applyFilters(): void {
    let result = [...this.invoices];

    // Branch isolation: Non-superadmin users only see invoices for their assigned branch
    if (!this.context.isSuperAdmin()) {
      const curBranchId = this.context.activeBranch().id;
      const curBranchGuid = this.context.branchGuid();
      result = result.filter(i => 
        !i.branchId || 
        i.branchId === curBranchId || 
        i.branchId === curBranchGuid ||
        (curBranchId === 'BR-01' && i.branchId.includes('31')) ||
        (curBranchId === 'BR-02' && i.branchId.includes('32')) ||
        (curBranchId === 'BR-03' && i.branchId.includes('33'))
      );
    } else if (this.selectedBranchFilter !== 'All') {
      const targetGuid = this.context.resolveBranchGuid(this.selectedBranchFilter);
      result = result.filter(i => 
        i.branchId === this.selectedBranchFilter || 
        i.branchId === targetGuid ||
        (this.selectedBranchFilter === 'BR-01' && i.branchId?.includes('31')) ||
        (this.selectedBranchFilter === 'BR-02' && i.branchId?.includes('32')) ||
        (this.selectedBranchFilter === 'BR-03' && i.branchId?.includes('33'))
      );
    }

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      result = result.filter(i => 
        i.invoiceNumber.toLowerCase().includes(q) ||
        i.supplierName.toLowerCase().includes(q) ||
        i.supplierBin.includes(q)
      );
    }
    if (this.selectedType !== 'All') {
      result = result.filter(i => i.purchaseType === this.selectedType);
    }
    if (this.selectedStatus !== 'All') {
      result = result.filter(i => i.status === this.selectedStatus);
    }
    this.filteredInvoices = result;
  }

  viewInvoice(inv: PurchaseInvoice): void {
    this.selectedInvoice = inv;
    this.showViewModal = true;
  }

  closeViewModal(): void {
    this.showViewModal = false;
    this.selectedInvoice = null;
  }

  getBranchName(branchId?: string): string {
    if (!branchId) return 'Dhaka HQ';
    if (branchId === 'BR-02' || branchId.includes('32')) return 'Chittagong Depot';
    if (branchId === 'BR-03' || branchId.includes('33')) return 'Bogura Warehouse';
    return 'Dhaka HQ';
  }

  canApprove(): boolean {
    return this.context.hasPermission('purchase.invoice.approve');
  }

  canPost(): boolean {
    return this.context.isSuperAdmin() || this.context.userRole() === 'TaxManager' || this.context.userRole() === 'Checker';
  }

  postInvoiceToLedger(id: string): void {
    if (!this.canPost()) {
      this.toast.error(
        this.context.locale() === 'bn' ? 'লেজারে পোস্ট করার অনুমতি নেই।' : 'Access Denied: You lack permissions to post to ledger.',
        'অননুমোদিত অ্যাকশন (RBAC Restriction)'
      );
      return;
    }

    const isBn = this.context.locale() === 'bn';
    this.purchaseApi.postPurchase(id).subscribe({
      next: () => {
        this.toast.success(
          isBn ? `চালান ${id} সফলভাবে ভ্যাট ও ইনভেন্টরি লেজারে পোস্ট করা হয়েছে!` : `Invoice ${id} successfully posted to VAT & inventory ledger!`,
          isBn ? 'পোস্টিং সম্পন্ন (Posted)' : 'Ledger Posting Complete'
        );
        this.loadInvoices();
      },
      error: (err) => {
        this.toast.error(err?.message || 'Posting failed', 'পোস্টিং ব্যর্থ');
      }
    });
  }

  approveInvoice(id: string): void {
    if (!this.canApprove()) {
      this.toast.error(
        this.context.locale() === 'bn'
          ? `অনুমোদন করার অনুমতি নেই। বর্তমান ভূমিকা: ${this.context.userRole()}`
          : `Access Denied: Your current role (${this.context.userRole()}) lacks approval privileges.`,
        'অননুমোদিত অ্যাকশন (RBAC Restriction)'
      );
      return;
    }

    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);

    const onApproved = () => {
      this.toast.success(
        this.context.locale() === 'bn'
          ? `চালান ${id} সফলভাবে অনুমোদিত হয়েছে!`
          : `Purchase Invoice ${id} successfully approved!`,
        'অনুমোদন সম্পন্ন (Approved)'
      );
      this.loadInvoices();
    };

    if (isGuid) {
      this.purchaseApi.approveTransition(id, 2).subscribe({
        next: onApproved,
        error: () => {
          this.purchaseApi.approveTransition(id, 1).subscribe({
            next: onApproved,
            error: () => {
              this.purchaseApi.updateStatus(id, 'Approved').subscribe(onApproved);
            }
          });
        }
      });
    } else {
      this.purchaseApi.updateStatus(id, 'Approved').subscribe(onApproved);
    }
  }

  approveFromModal(id: string): void {
    this.approveInvoice(id);
    this.closeViewModal();
  }

  exportToCsv(): void {
    const headers = [
      'Invoice / BOE No',
      'Supplier Invoice No',
      'Supplier Name',
      'Supplier BIN',
      'Type',
      'Invoice Date',
      'Fiscal Period',
      'Taxable Value (BDT)',
      'Total VAT (BDT)',
      'Supplementary Duty (BDT)',
      'Advance Tax (BDT)',
      'VDS Withholding (BDT)',
      'Grand Total (BDT)',
      'Bank Ref (Sec 46)',
      'Status'
    ];

    const rows = this.filteredInvoices.map(i => [
      i.invoiceNumber,
      i.supplierInvoiceNumber || '',
      i.supplierName,
      i.supplierBin,
      i.purchaseType,
      i.invoiceDate,
      i.fiscalPeriod,
      i.subTotal,
      i.totalVat,
      i.totalSupplementaryDuty || 0,
      i.totalAdvanceTax,
      i.totalVds,
      i.grandTotal,
      i.bankPaymentRef || 'N/A',
      i.status
    ]);

    const compId = this.context.activeCompany().id || 'PADMA';
    const filename = `Purchase_Invoices_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
