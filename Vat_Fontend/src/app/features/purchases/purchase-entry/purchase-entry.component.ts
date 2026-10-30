import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PurchaseApiService } from '../../../core/services/api/purchase-api.service';
import { MasterDataApiService } from '../../../core/services/api/master-data-api.service';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { ProductItem, BusinessParty, PurchaseLine, PurchaseInvoice, Branch } from '../../../core/models/vat-models';
import { ToastService } from '../../../core/services/toast.service';
import { MockDataService } from '../../../core/services/mock-data.service';
import { NbrValidators } from '../../../shared/validators/nbr-validators';

@Component({
  selector: 'app-purchase-entry',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, BdtCurrencyPipe],
  template: `
    <p class="vat-card">Draft tax amounts are estimates only. The server calculates applicable tax during posting preparation; review that snapshot before authorization.</p>
    <div class="purchase-entry-page">
      <!-- Breadcrumb & Header -->
      <div class="page-header">
        <div>
          <div class="breadcrumb-trail">
            <a routerLink="/app/purchases/invoices">Purchases</a> / <span>New Invoice Entry</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'নতুন ক্রয় চালান এন্ট্রি' : 'New Purchase Invoice Entry' }}
          </h1>
          <p class="page-subtitle">
            {{ context.locale() === 'bn' 
              ? 'স্থানীয় সরবরাহকারী চালান অথবা আমদানি বিল অব এন্ট্রি থেকে ভ্যাট ইনপুট রেয়াত অন্তর্ভুক্তি'
              : 'Record domestic purchase invoice or customs Bill of Entry for Section 46 input tax credit' }}
          </p>
        </div>

        <div class="header-actions">
          <a routerLink="/app/purchases/invoices" class="btn btn-secondary">Cancel</a>
          <button 
            type="button"
            class="btn btn-primary" 
            (click)="submitInvoice()" 
            [disabled]="lines.length === 0 || isSubmitting">
            <svg *ngIf="!isSubmitting" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
              <polyline points="17 21 17 13 7 13 7 21"></polyline>
              <polyline points="7 3 7 8 15 8"></polyline>
            </svg>
            <span *ngIf="isSubmitting" class="spinner-small"></span>
            {{ isSubmitting 
              ? (context.locale() === 'bn' ? 'ডাটাবেজে সংরক্ষণ হচ্ছে...' : 'Saving to Database...') 
              : (context.locale() === 'bn' ? 'সংরক্ষণ ও অনুমোদনে দাখিল' : 'Submit for Approval') }}
          </button>
        </div>
      </div>

      <!-- Live Database Status Banner -->
      <div class="db-connection-banner">
        <div class="db-status-pill">
          <span class="pulse-dot"></span>
          <span>
            <strong>PostgreSQL Live:</strong> 
            {{ suppliers.length }} {{ context.locale() === 'bn' ? 'সরবরাহকারী' : 'Suppliers' }} & 
            {{ products.length }} {{ context.locale() === 'bn' ? 'পণ্য ডাটাবেজ থেকে লোড হয়েছে' : 'Products Loaded' }}
          </span>
        </div>
        <div class="db-meta-info font-mono">
          Tenant: 00000000-0000-0000-0000-000000000001 | Company: {{ context.companyName() }}
        </div>
      </div>

      <!-- Section 46 Statutory Compliance Banner -->
      <div *ngIf="grandTotal >= 100000" class="statutory-notice-banner" [class.banner-warning]="!purchaseForm.get('bankPaymentRef')?.value">
        <div class="banner-icon">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
            <line x1="12" y1="9" x2="12" y2="13"></line>
            <line x1="12" y1="17" x2="12.01" y2="17"></line>
          </svg>
        </div>
        <div class="banner-body">
          <strong>ধারা ৪৬(১)(ঙ) ব্যাংকিং বাধ্যবাধকতা (Section 46 Banking Compliance):</strong>
          চালানের মোট মূল্য ১,০০,০০০ টাকার অধিক হওয়ায় ভ্যাট ইনপুট ট্যাক্স রেয়াত গ্রহণের জন্য ব্যাংকিং চ্যানেলে মূল্য পরিশোধের প্রমাণক রেফারেন্স (BEFTN / RTGS / Cheque Ref) বাধ্যতামূলক।
        </div>
      </div>

      <!-- Invoice Header Form Card -->
      <form [formGroup]="purchaseForm" class="vat-card form-header-card">
        <h2 class="form-section-title">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
            <line x1="16" y1="2" x2="16" y2="6"></line>
            <line x1="8" y1="2" x2="8" y2="6"></line>
            <line x1="3" y1="10" x2="21" y2="10"></line>
          </svg>
          Invoice & Supplier Details
        </h2>

        <div class="form-grid">
          <!-- Supplier Selection -->
          <div class="form-group">
            <label class="vat-label">
              Supplier (সরবরাহকারী) <span class="required-star">*</span>
            </label>
            <select 
              class="vat-select" 
              formControlName="supplierId" 
              (change)="onSupplierChange()"
              [class.is-invalid]="isFieldInvalid('supplierId')">
              <option value="">-- Select Supplier ({{ suppliers.length }} from DB) --</option>
              <option *ngFor="let s of suppliers" [value]="s.id">
                {{ s.name }} (BIN: {{ s.bin }})
              </option>
            </select>
            <div *ngIf="selectedSupplier" class="supplier-meta-badge">
              <span class="font-mono text-emerald">BIN: {{ selectedSupplier.bin || 'Not supplied' }}</span> |
              <span class="text-dim">{{ selectedSupplier.address }}</span>
            </div>
            <div *ngIf="isFieldInvalid('supplierId')" class="vat-field-error">
              সরবরাহকারী নির্বাচন করা বাধ্যতামূলক।
            </div>
          </div>

          <!-- Supplier Invoice / Challan No -->
          <div class="form-group">
            <label class="vat-label">
              Supplier Invoice / Challan No <span class="required-star">*</span>
            </label>
            <input 
              type="text" 
              class="vat-input" 
              formControlName="supplierInvoiceNumber" 
              placeholder="e.g. BEN-INV-8911"
              [class.is-invalid]="isFieldInvalid('supplierInvoiceNumber')" />
            <div *ngIf="isFieldInvalid('supplierInvoiceNumber')" class="vat-field-error">
              {{ getFieldError('supplierInvoiceNumber') || 'সরবরাহকারীর বৈধ চালান নম্বর প্রদান আবশ্যক।' }}
            </div>
          </div>

          <!-- Purchase Date -->
          <div class="form-group">
            <label class="vat-label">
              Purchase Date (চালানের তারিখ) <span class="required-star">*</span>
            </label>
            <input 
              type="date" 
              class="vat-input" 
              formControlName="invoiceDate"
              [class.is-invalid]="isFieldInvalid('invoiceDate')" />
            <div *ngIf="isFieldInvalid('invoiceDate')" class="vat-field-error">
              চালানের তারিখ প্রদান আবশ্যক।
            </div>
          </div>

          <!-- Purchase Type -->
          <div class="form-group">
            <label class="vat-label">Purchase Type (ক্রয়ের ধরন)</label>
            <select class="vat-select" formControlName="purchaseType" (change)="onPurchaseTypeChange()">
              <option value="Local">Local Domestic Purchase (স্থানীয় ক্রয়)</option>
              <option value="Import">Import via Customs BOE (আমদানি)</option>
            </select>
          </div>

          <!-- Branch Selection -->
          <div class="form-group">
            <label class="vat-label">
              {{ context.locale() === 'bn' ? 'গ্রহীতা শাখা / কারখানা (Receiving Branch) *' : 'Receiving Branch / Plant *' }}
              <span *ngIf="!context.isSuperAdmin()" class="badge-branch-locked">🔒 Assigned</span>
            </label>
            <select 
              *ngIf="context.isSuperAdmin()" 
              class="vat-select" 
              formControlName="selectedBranchId"
              [class.is-invalid]="isFieldInvalid('selectedBranchId')">
              <option *ngFor="let b of branches" [value]="b.id">
                🏢 {{ b.name }} ({{ b.code }})
              </option>
            </select>
            <div *ngIf="!context.isSuperAdmin()" class="vat-input locked-branch-field">
              🏢 {{ context.activeBranch().name }} ({{ context.activeBranch().code }})
            </div>
          </div>

          <!-- Section 46 Bank Payment Reference -->
          <div class="form-group full-width">
            <label class="vat-label">
              Bank Payment Ref / BEFTN / Cheque (ব্যাংক পেমেন্ট রেফারেন্স — Section 46 Compliance)
              <span *ngIf="grandTotal >= 100000" class="required-star">*</span>
            </label>
            <input
              type="text"
              class="vat-input"
              formControlName="bankPaymentRef"
              placeholder="e.g. BEFTN-BRAC-20261023-0012 (১ লক্ষ টাকার অধিক চালানের রেয়াত পেতে আবশ্যক)"
              [class.is-invalid]="isFieldInvalid('bankPaymentRef')"
            />
            <div *ngIf="isFieldInvalid('bankPaymentRef')" class="vat-field-error">
              ১,০০,০০০ টাকার অধিক চালানে ধারা ৪৬(১)(ঙ) অনুযায়ী ব্যাংক পেমেন্ট রেফারেন্স বাধ্যতামূলক।
            </div>
          </div>
        </div>
      </form>

      <!-- Add Product Line Section (Reactive Form) -->
      <form [formGroup]="lineDraftForm" class="vat-card">
        <div class="vat-card-header">
          <h2 class="form-section-title">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="12" y1="5" x2="12" y2="19"></line>
              <line x1="5" y1="12" x2="19" y2="12"></line>
            </svg>
            Add Line Item (পণ্য বা কাঁচামাল লাইন)
          </h2>
        </div>

        <div class="line-entry-grid">
          <div class="form-group">
            <label class="vat-label">Item / Raw Material <span class="required-star">*</span></label>
            <select 
              class="vat-select" 
              formControlName="productId" 
              (change)="onProductSelect()"
              [class.is-invalid]="isLineFieldInvalid('productId')">
              <option value="">-- Choose Item ({{ products.length }} from DB) --</option>
              <option *ngFor="let p of products" [value]="p.id">
                {{ p.name }} ({{ p.code }} | {{ p.uom }})
              </option>
            </select>
          </div>

          <div class="form-group">
            <label class="vat-label">Quantity (পরিমাণ) <span class="required-star">*</span></label>
            <input 
              type="number" 
              class="vat-input" 
              formControlName="quantity" 
              min="1" 
              [class.is-invalid]="isLineFieldInvalid('quantity')" />
            <div *ngIf="isLineFieldInvalid('quantity')" class="vat-field-error">
              পরিমাণ কমপক্ষে ১ হতে হবে।
            </div>
          </div>

          <div class="form-group">
            <label class="vat-label">Unit Price BDT (দর) <span class="required-star">*</span></label>
            <input 
              type="number" 
              class="vat-input" 
              formControlName="unitPrice" 
              min="0" 
              [class.is-invalid]="isLineFieldInvalid('unitPrice')" />
            <div *ngIf="isLineFieldInvalid('unitPrice')" class="vat-field-error">
              সঠিক একক মূল্য প্রদান করুন।
            </div>
          </div>

          <div class="form-group">
            <label class="vat-label">VAT Rate %</label>
            <select class="vat-select" formControlName="vatRate">
              <option [value]="15">Standard 15%</option>
              <option [value]="7.5">Reduced 7.5%</option>
              <option [value]="5">Reduced 5%</option>
              <option [value]="0">Zero Rate 0%</option>
            </select>
          </div>

          <div class="form-group line-add-btn-group">
            <label class="vat-label">&nbsp;</label>
            <button 
              type="button" 
              class="btn btn-secondary w-full" 
              (click)="addLineItem()" 
              [disabled]="lineDraftForm.invalid">
              + Add Line
            </button>
          </div>
        </div>
      </form>

      <!-- Line Items Data Table -->
      <div class="vat-card">
        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th style="width: 40px;">#</th>
                <th>Product Description</th>
                <th>HS Code</th>
                <th>UOM</th>
                <th class="text-right">Qty</th>
                <th class="text-right">Rate</th>
                <th class="text-right">Taxable BDT</th>
                <th class="text-right">VAT Rate</th>
                <th class="text-right">VAT Amount</th>
                <th class="text-right">Total BDT</th>
                <th>Rebate?</th>
                <th class="text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let l of lines; let i = index">
                <td>{{ i + 1 }}</td>
                <td><strong>{{ l.productName }}</strong></td>
                <td class="font-mono text-dim">{{ l.hsCode }}</td>
                <td>{{ l.uom }}</td>
                <td class="text-right numeric">{{ l.quantity }}</td>
                <td class="text-right numeric">{{ l.unitPrice | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ l.taxableAmount | bdtCurrency:false }}</td>
                <td class="text-right numeric">{{ l.vatRatePercent }}%</td>
                <td class="text-right numeric text-emerald">{{ l.vatAmount | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ l.totalAmount | bdtCurrency:false }}</td>
                <td><span class="rebate-pill">Eligible</span></td>
                <td class="text-right">
                  <button type="button" class="btn-remove" (click)="removeLine(i)">Remove</button>
                </td>
              </tr>
              <tr *ngIf="lines.length === 0">
                <td colspan="12" class="empty-state">
                  No items added yet. Choose an item above and click "+ Add Line".
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Financial Totals Summary Card -->
      <div class="totals-summary-wrapper">
        <div class="vat-card totals-card">
          <div class="totals-row">
            <span class="totals-label">Subtotal (Taxable Value):</span>
            <span class="totals-value">{{ subTotal | bdtCurrency }}</span>
          </div>
          <div class="totals-row">
            <span class="totals-label">Input VAT Amount:</span>
            <span class="totals-value text-emerald">+ {{ totalVat | bdtCurrency }}</span>
          </div>
          <div class="totals-row" *ngIf="purchaseForm.get('purchaseType')?.value === 'Import'">
            <span class="totals-label">Advance Tax AT (5%):</span>
            <span class="totals-value text-amber">+ {{ totalAdvanceTax | bdtCurrency }}</span>
          </div>
          <div class="totals-divider"></div>
          <div class="totals-row grand-total-row">
            <span class="grand-label">Grand Total Payable:</span>
            <span class="grand-value">{{ grandTotal | bdtCurrency }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .purchase-entry-page {
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

    .breadcrumb-trail {
      font-size: 0.78rem;
      color: var(--text-dim);
      margin-bottom: 4px;
    }
    .breadcrumb-trail a {
      color: var(--vat-teal, #10b981);
      text-decoration: none;
    }

    .page-title {
      font-size: 1.5rem;
      font-weight: 800;
      margin: 0;
    }

    .page-subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin: 4px 0 0 0;
    }

    .header-actions {
      display: flex;
      gap: 12px;
    }

    .db-connection-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 12px;
      padding: 10px 16px;
      background: rgba(16, 185, 129, 0.08);
      border: 1px solid rgba(16, 185, 129, 0.25);
      border-radius: var(--radius-md, 8px);
      font-size: 0.85rem;
    }

    .db-status-pill {
      display: flex;
      align-items: center;
      gap: 8px;
      color: #34d399;
    }

    .pulse-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background-color: #10b981;
      box-shadow: 0 0 8px #10b981;
      animation: pulse 1.8s infinite;
    }

    @keyframes pulse {
      0% { transform: scale(0.95); opacity: 0.8; }
      50% { transform: scale(1.3); opacity: 1; }
      100% { transform: scale(0.95); opacity: 0.8; }
    }

    .statutory-notice-banner {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px 16px;
      background: rgba(16, 185, 129, 0.08);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: var(--radius-md, 8px);
      font-size: 0.85rem;
      color: var(--text-main);
    }

    .statutory-notice-banner.banner-warning {
      background: rgba(245, 158, 11, 0.1);
      border-color: rgba(245, 158, 11, 0.4);
      color: #f59e0b;
    }

    .form-header-card {
      padding: 1.25rem;
    }

    .form-section-title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 1rem;
      font-weight: 700;
      margin: 0 0 14px 0;
    }

    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 16px;
    }

    .line-entry-grid {
      display: grid;
      grid-template-columns: 2fr 1fr 1fr 1fr 1fr;
      gap: 12px;
      align-items: flex-start;
    }

    @media (max-width: 900px) {
      .line-entry-grid {
        grid-template-columns: 1fr;
      }
    }

    .full-width {
      grid-column: 1 / -1;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .vat-label {
      font-size: 0.825rem;
      font-weight: 600;
      color: var(--text-main);
    }

    .required-star {
      color: #ef4444;
      font-weight: 700;
    }

    .vat-input {
      width: 100%;
      padding: 9px 12px;
      background: var(--bg-input, #0f172a);
      border: 1px solid var(--border-subtle, #334155);
      border-radius: 8px;
      color: var(--text-main, #f8fafc);
      font-size: 0.875rem;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
      color-scheme: dark;
    }

    .vat-select {
      width: 100%;
      padding: 9px 40px 9px 12px;
      background-color: var(--bg-input, #0f172a);
      border: 1px solid var(--border-subtle, #334155);
      border-radius: 8px;
      color: var(--text-main, #f8fafc);
      font-size: 0.875rem;
      appearance: none;
      -webkit-appearance: none;
      -moz-appearance: none;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%2334d399' stroke-width='2.2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpolyline points='6 9 12 15 18 9'%3E%3C/polyline%3E%3C/svg%3E");
      background-repeat: no-repeat;
      background-position: calc(100% - 12px) center;
      background-size: 16px 16px;
      cursor: pointer;
      color-scheme: dark;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .vat-select option {
      background-color: #1e293b;
      color: #f8fafc;
      padding: 10px 14px;
    }

    .vat-select option:checked {
      background-color: #065f46;
      color: #34d399;
      font-weight: 600;
    }

    .vat-input:focus, .vat-select:focus {
      outline: none;
      border-color: var(--vat-emerald, #10b981);
      box-shadow: 0 0 0 3px var(--vat-emerald-glow, rgba(16, 185, 129, 0.25));
    }

    .vat-input.is-invalid, .vat-select.is-invalid {
      border-color: #ef4444;
      background: rgba(239, 68, 68, 0.05);
    }

    .vat-field-error {
      font-size: 0.75rem;
      color: #ef4444;
      margin-top: 2px;
    }

    .supplier-meta-badge {
      font-size: 0.75rem;
      margin-top: 4px;
    }

    .btn-remove {
      background: rgba(244, 63, 94, 0.1);
      border: 1px solid rgba(244, 63, 94, 0.3);
      color: #fb7185;
      padding: 4px 10px;
      border-radius: var(--radius-sm, 4px);
      font-size: 0.75rem;
      cursor: pointer;
    }

    .rebate-pill {
      display: inline-block;
      font-size: 0.72rem;
      background: rgba(16, 185, 129, 0.15);
      color: #10b981;
      padding: 2px 8px;
      border-radius: 9999px;
      font-weight: 600;
    }

    .totals-summary-wrapper {
      display: flex;
      justify-content: flex-end;
    }

    .totals-card {
      width: 380px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 1.25rem;
    }

    .totals-row {
      display: flex;
      justify-content: space-between;
      font-size: 0.85rem;
    }

    .totals-label {
      color: var(--text-muted);
    }

    .totals-value {
      font-family: var(--font-mono, monospace);
      font-weight: 600;
    }

    .totals-divider {
      height: 1px;
      background: var(--border-subtle);
      margin: 4px 0;
    }

    .grand-total-row {
      font-size: 1.1rem;
      font-weight: 800;
      color: var(--text-main);
    }

    .grand-value {
      font-family: var(--font-mono, monospace);
      color: #10b981;
    }

    .badge-branch-locked {
      background: rgba(245, 158, 11, 0.15);
      color: #fbbf24;
      font-size: 0.68rem;
      font-weight: 700;
      padding: 1px 6px;
      border-radius: 9999px;
      margin-left: 6px;
    }

    .locked-branch-field {
      display: flex;
      align-items: center;
      background: rgba(255, 255, 255, 0.03);
      border-color: rgba(255, 255, 255, 0.1);
      color: var(--vat-teal, #10b981);
      font-weight: 600;
      cursor: not-allowed;
    }

    .empty-state {
      text-align: center;
      padding: 2rem;
      color: var(--text-muted);
    }

    .spinner-small {
      display: inline-block;
      width: 14px;
      height: 14px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-radius: 50%;
      border-top-color: #fff;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class PurchaseEntryComponent implements OnInit {
  public context = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private masterApi = inject(MasterDataApiService);
  private purchaseApi = inject(PurchaseApiService);
  private mockData = inject(MockDataService);
  private toast = inject(ToastService);
  private router = inject(Router);

  public suppliers: BusinessParty[] = [];
  public products: ProductItem[] = [];
  public branches: Branch[] = [];

  public lines: PurchaseLine[] = [];
  public subTotal: number = 0;
  public totalVat: number = 0;
  public totalAdvanceTax: number = 0;
  public grandTotal: number = 0;

  public isSubmitting: boolean = false;
  public formSubmitted: boolean = false;
  public lineSubmitted: boolean = false;

  // Reactive Header Form
  public purchaseForm: FormGroup = this.fb.group({
    supplierId: ['', [Validators.required]],
    supplierInvoiceNumber: ['', [Validators.required, NbrValidators.challanNumber(true)]],
    invoiceDate: [new Date().toISOString().split('T')[0], [Validators.required]],
    purchaseType: ['Local', [Validators.required]],
    selectedBranchId: ['', [Validators.required]],
    bankPaymentRef: ['']
  });

  // Reactive Line Item Draft Form
  public lineDraftForm: FormGroup = this.fb.group({
    productId: ['', [Validators.required]],
    quantity: [1000, [Validators.required, NbrValidators.positiveNumber(1)]],
    unitPrice: [140, [Validators.required, NbrValidators.positiveNumber(0, true)]],
    vatRate: [15, [Validators.required]]
  });

  ngOnInit(): void {
    this.branches = this.mockData.branches;
    const initialBranchId = this.context.activeBranch().id;
    this.purchaseForm.patchValue({ selectedBranchId: initialBranchId });

    this.masterApi.getParties('Supplier').subscribe({
      next: (data) => {
        this.suppliers = data || [];
        if (this.suppliers.length > 0 && !this.purchaseForm.get('supplierId')?.value) {
          this.purchaseForm.patchValue({ supplierId: this.suppliers[0].id });
        }
      }
    });

    this.masterApi.getProducts().subscribe({
      next: (data) => {
        this.products = data || [];
        if (this.products.length > 0 && !this.lineDraftForm.get('productId')?.value) {
          this.lineDraftForm.patchValue({ productId: this.products[0].id });
          this.onProductSelect();
        }
      }
    });

    // Dynamically apply Section 46 banking validator if total changes
    this.purchaseForm.get('purchaseType')?.valueChanges.subscribe(() => {
      this.recomputeTotals();
    });
  }

  get selectedSupplier(): BusinessParty | undefined {
    const id = this.purchaseForm.get('supplierId')?.value;
    return this.suppliers.find(s => s.id === id);
  }

  onSupplierChange(): void {
    // Supplier metadata updates automatically via selectedSupplier getter
  }

  onPurchaseTypeChange(): void {
    this.recomputeTotals();
  }

  onProductSelect(): void {
    const prodId = this.lineDraftForm.get('productId')?.value;
    const prod = this.products.find(p => p.id === prodId);
    if (prod) {
      this.lineDraftForm.patchValue({
        unitPrice: prod.standardCost ?? 0,
        vatRate: prod.vatRatePercent ?? 0
      });
    }
  }

  isFieldInvalid(fieldName: string): boolean {
    const control = this.purchaseForm.get(fieldName);
    if (!control) return false;
    return control.invalid && (control.dirty || control.touched || this.formSubmitted);
  }

  getFieldError(fieldName: string): string | null {
    const control = this.purchaseForm.get(fieldName);
    if (!control || !control.errors) return null;
    const errors = control.errors;
    if (errors['required']) return 'এই তথ্যটি প্রদান করা আবশ্যক।';
    if (errors['invalidChallan']) return errors['invalidChallan'].message;
    if (errors['section46BankingRequired']) return errors['section46BankingRequired'].message;
    return null;
  }

  isLineFieldInvalid(fieldName: string): boolean {
    const control = this.lineDraftForm.get(fieldName);
    if (!control) return false;
    return control.invalid && (control.dirty || control.touched || this.lineSubmitted);
  }

  addLineItem(): void {
    this.lineSubmitted = true;
    if (this.lineDraftForm.invalid) {
      this.lineDraftForm.markAllAsTouched();
      return;
    }

    const { productId, quantity, unitPrice, vatRate } = this.lineDraftForm.value;
    const prod = this.products.find(p => p.id === productId);
    if (!prod) return;

    const qty = Number(quantity);
    const rate = Number(unitPrice);
    const vatPct = Number(vatRate);

    const taxableAmount = Math.round(qty * rate * 100) / 100;
    const vatAmount = Math.round(taxableAmount * (vatPct / 100) * 100) / 100;
    const isImport = this.purchaseForm.get('purchaseType')?.value === 'Import';
    const advanceTax = isImport ? Math.round(taxableAmount * 0.05 * 100) / 100 : 0;
    const totalAmount = taxableAmount + vatAmount + advanceTax;

    const line: PurchaseLine = {
      id: `PL-${Date.now()}`,
      productId: prod.id,
      productReference: prod.code,
      productName: prod.name,
      productNameBn: prod.nameBn,
      hsCode: prod.hsCode,
      uom: prod.uom,
      quantity: qty,
      unitPrice: rate,
      taxableAmount,
      vatRatePercent: vatPct,
      vatAmount,
      supplementaryDutyAmount: 0,
      advanceTaxAmount: advanceTax,
      vdsAmount: 0,
      totalAmount,
      isCreditEligible: true
    };

    this.lines.push(line);
    this.recomputeTotals();

    // Reset line draft form state
    this.lineSubmitted = false;
    this.lineDraftForm.patchValue({
      quantity: 1000,
      unitPrice: 140
    });
  }

  removeLine(index: number): void {
    this.lines.splice(index, 1);
    this.recomputeTotals();
  }

  recomputeTotals(): void {
    this.subTotal = this.lines.reduce((acc, l) => acc + l.taxableAmount, 0);
    this.totalVat = this.lines.reduce((acc, l) => acc + l.vatAmount, 0);
    const isImport = this.purchaseForm.get('purchaseType')?.value === 'Import';
    this.totalAdvanceTax = isImport 
      ? this.lines.reduce((acc, l) => acc + (l.taxableAmount * 0.05), 0)
      : 0;
    this.grandTotal = this.subTotal + this.totalVat + this.totalAdvanceTax;

    // Dynamically update Section 46 banking validator
    const bankRefControl = this.purchaseForm.get('bankPaymentRef');
    if (this.grandTotal >= 100000) {
      bankRefControl?.setValidators([NbrValidators.section46Banking(() => this.grandTotal, 100000)]);
    } else {
      bankRefControl?.clearValidators();
    }
    bankRefControl?.updateValueAndValidity();
  }

  submitInvoice(): void {
    this.formSubmitted = true;

    if (this.purchaseForm.invalid) {
      this.purchaseForm.markAllAsTouched();
      this.toast.error(
        this.context.locale() === 'bn' 
          ? 'অনুগ্রহ করে ফরমের প্রয়োজনীয় তথ্য ও সংবিধিবদ্ধ শর্তাদি সঠিকভাবে পূরণ করুন।' 
          : 'Please complete all required fields and statutory conditions.',
        'ত্রুটি (Validation)'
      );
      return;
    }

    if (this.lines.length === 0) {
      this.toast.error(
        this.context.locale() === 'bn' 
          ? 'কমপক্ষে একটি পণ্য বা কাঁচামাল লাইন যোগ করুন।' 
          : 'Please add at least one line item.',
        'ত্রুটি (Validation)'
      );
      return;
    }

    const supplier = this.selectedSupplier;
    if (!supplier) return;

    this.isSubmitting = true;

    const formVal = this.purchaseForm.value;
    const branchIdGuid = this.context.resolveBranchGuid(formVal.selectedBranchId || this.context.activeBranch().id);

    const invoice: Partial<PurchaseInvoice> = {
      supplierId: supplier.code,
      supplierName: supplier.name,
      supplierBin: supplier.bin,
      supplierInvoiceNumber: formVal.supplierInvoiceNumber || 'INV-' + Math.floor(1000 + Math.random() * 9000),
      purchaseType: formVal.purchaseType,
      invoiceDate: formVal.invoiceDate,
      fiscalPeriod: this.context.activeFiscalPeriod().periodCode,
      companyId: this.context.activeCompany().id,
      branchId: branchIdGuid,
      bankPaymentRef: formVal.bankPaymentRef,
      lines: this.lines,
      subTotal: this.subTotal,
      totalVat: this.totalVat,
      totalAdvanceTax: this.totalAdvanceTax,
      grandTotal: this.grandTotal,
      remarks: 'Submitted for Section 46 rebate claim via Reactive Form Engine.'
    };

    this.purchaseApi.createInvoice(invoice).subscribe({
      next: (created) => {
        this.isSubmitting = false;
        this.toast.success(
          this.context.locale() === 'bn' 
            ? `ক্রয় চালান ${created?.invoiceNumber || ''} সফলভাবে ডাটাবেজে সংরক্ষিত ও অনুমোদনের জন্য দাখিল করা হয়েছে!`
            : `Purchase invoice ${created?.invoiceNumber || ''} saved to PostgreSQL and submitted for approval!`,
          this.context.locale() === 'bn' ? 'চালান দাখিল সম্পন্ন' : 'Invoice Submitted'
        );
        this.router.navigate(['/app/purchases/invoices']);
      },
      error: (err) => {
        this.isSubmitting = false;
        console.error('Failed to submit purchase invoice:', err);
        const msg = err?.error?.detail || err?.error?.message || (
          this.context.locale() === 'bn' 
            ? 'চালান সংরক্ষণ ব্যর্থ হয়েছে। সার্ভার সংযোগ চেক করুন।' 
            : 'Failed to create purchase in database. Please check backend connection.'
        );
        this.toast.error(msg, 'ত্রুটি (Error)');
      }
    });
  }
}
