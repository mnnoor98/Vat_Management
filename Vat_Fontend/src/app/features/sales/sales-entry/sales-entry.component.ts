import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { SalesApiService } from '../../../core/services/api/sales-api.service';
import { MasterDataApiService } from '../../../core/services/api/master-data-api.service';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { ProductItem, BusinessParty, SalesLine, SalesInvoice, Branch } from '../../../core/models/vat-models';
import { ToastService } from '../../../core/services/toast.service';
import { MockDataService } from '../../../core/services/mock-data.service';
import { NbrValidators } from '../../../shared/validators/nbr-validators';

@Component({
  selector: 'app-sales-entry',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, BdtCurrencyPipe],
  template: `
    <div class="sales-entry-page">
      <!-- Header -->
      <div class="page-header">
        <div>
          <div class="breadcrumb-trail">
            <a routerLink="/app/sales/invoices">Sales</a> / <span>Create Tax Invoice</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'নতুন বিক্রয় চালানপত্র তৈরি (মূসক ৬.৩)' : 'Create New Sales Challan (Mushak 6.3)' }}
          </h1>
          <p class="page-subtitle">
            {{ context.locale() === 'bn'
              ? 'কারখানা বা ডিপো হতে পণ্য অপসারণের পূর্বে সংবিধিবদ্ধ ১৫% মূসক চালানপত্র ইস্যু'
              : 'Issue statutory 15% VAT Challan prior to goods dispatch from factory or warehouse' }}
          </p>
        </div>

        <div class="header-actions">
          <a routerLink="/app/sales/invoices" class="btn btn-secondary">Cancel</a>
          <button 
            type="button"
            class="btn btn-primary" 
            (click)="saveInvoice()" 
            [disabled]="lines.length === 0 || isSubmitting">
            <span *ngIf="isSubmitting" class="spinner-small"></span>
            {{ isSubmitting 
              ? (context.locale() === 'bn' ? 'প্রসেসিং হচ্ছে...' : 'Processing...') 
              : (context.locale() === 'bn' ? 'সংরক্ষণ ও মূসক ৬.৩ চালান ইস্যু' : 'Generate Mushak 6.3 & Dispatch') }}
          </button>
        </div>
      </div>

      <!-- Statutory Rule 40(1) Notice Banner -->
      <div class="statutory-notice-banner">
        <div class="banner-icon">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
            <polyline points="14 2 14 8 20 8"></polyline>
            <line x1="16" y1="13" x2="8" y2="13"></line>
            <line x1="16" y1="17" x2="8" y2="17"></line>
            <polyline points="10 9 9 9 8 9"></polyline>
          </svg>
        </div>
        <div class="banner-body">
          <strong>মূসক ও সম্পূরক শুল্ক বিধিমালা, ২০১৬ — বিধি ৪০(১) অনুপালন:</strong>
          পণ্য অপসারণের পূর্বে ক্রেতার বিআইএন, গন্তব্যস্থল ও পরিবহন যানবাহনের নম্বর সম্বলিত মূসক-৬.৩ চালানপত্র ইস্যু বাধ্যতামূলক।
        </div>
      </div>

      <!-- Header Form (Reactive Form) -->
      <form [formGroup]="salesForm" class="vat-card">
        <h2 class="form-title">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
            <line x1="16" y1="2" x2="16" y2="6"></line>
            <line x1="8" y1="2" x2="8" y2="6"></line>
            <line x1="3" y1="10" x2="21" y2="10"></line>
          </svg>
          Buyer & Delivery Vehicle Details
        </h2>

        <div class="form-grid">
          <!-- Customer Selection -->
          <div class="form-group">
            <label class="vat-label">
              Customer (ক্রেতা) <span class="required-star">*</span>
            </label>
            <select 
              class="vat-select" 
              formControlName="customerId" 
              (change)="onCustomerChange()"
              [class.is-invalid]="isFieldInvalid('customerId')">
              <option value="">-- Choose Registered Customer --</option>
              <option *ngFor="let c of customers" [value]="c.id">
                {{ c.name }} (BIN: {{ c.bin }})
              </option>
            </select>
            <div *ngIf="selectedCustomer" class="customer-meta-badge">
              <span class="font-mono text-emerald font-bold">BIN: {{ selectedCustomer.bin }}</span> | 
              <span class="text-dim">{{ selectedCustomer.address }}</span>
            </div>
            <div *ngIf="isFieldInvalid('customerId')" class="vat-field-error">
              ক্রেতা নির্বাচন করা বাধ্যতামূলক।
            </div>
          </div>

          <!-- Delivery Date -->
          <div class="form-group">
            <label class="vat-label">
              Delivery Date & Time <span class="required-star">*</span>
            </label>
            <input 
              type="date" 
              class="vat-input" 
              formControlName="deliveryDate"
              [class.is-invalid]="isFieldInvalid('deliveryDate')" />
            <div *ngIf="isFieldInvalid('deliveryDate')" class="vat-field-error">
              ডেলিভারির তারিখ প্রদান আবশ্যক।
            </div>
          </div>

          <!-- Destination Address -->
          <div class="form-group">
            <label class="vat-label">
              Destination Address (গন্তব্যস্থল) <span class="required-star">*</span>
            </label>
            <input 
              type="text" 
              class="vat-input" 
              formControlName="destinationAddress" 
              placeholder="e.g. Shwapno Central Hub, Savar"
              [class.is-invalid]="isFieldInvalid('destinationAddress')" />
            <div *ngIf="isFieldInvalid('destinationAddress')" class="vat-field-error">
              গন্তব্যস্থলের সঠিক ঠিকানা আবশ্যক।
            </div>
          </div>

          <!-- Vehicle Number -->
          <div class="form-group">
            <label class="vat-label">
              Transport Vehicle Type & No <span class="required-star">*</span>
            </label>
            <input 
              type="text" 
              class="vat-input" 
              formControlName="vehicleNo" 
              placeholder="e.g. Covered Van (Dhaka Metro-U-14-8921)"
              [class.is-invalid]="isFieldInvalid('vehicleNo')" />
            <div *ngIf="isFieldInvalid('vehicleNo')" class="vat-field-error">
              {{ getFieldError('vehicleNo') || 'পরিবহন যানবাহনের নম্বর প্রদান আবশ্যক (বিধি ৪০(১))।' }}
            </div>
          </div>

          <!-- Branch Wise Selection -->
          <div class="form-group">
            <label class="vat-label">
              {{ context.locale() === 'bn' ? 'ইস্যুকারী শাখা / ডিপো (Issuing Branch) *' : 'Issuing Branch / Depot *' }}
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
        </div>
      </form>

      <!-- Line Item Add Form (Reactive Form) -->
      <form [formGroup]="lineDraftForm" class="vat-card">
        <h2 class="form-title">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          Select Finished Goods to Sell
        </h2>

        <div class="line-grid">
          <div class="form-group">
            <label class="vat-label">Product (পণ্য) <span class="required-star">*</span></label>
            <select 
              class="vat-select" 
              formControlName="productId" 
              (change)="onProductSelect()"
              [class.is-invalid]="isLineFieldInvalid('productId')">
              <option value="">-- Select Product --</option>
              <option *ngFor="let p of products" [value]="p.id">
                {{ p.name }} (HS: {{ p.hsCode }} | Rate: ৳ {{ p.salesPrice }})
              </option>
            </select>
            <div *ngIf="isLineFieldInvalid('productId')" class="vat-field-error">
              পণ্য নির্বাচন করুন।
            </div>
          </div>

          <div class="form-group">
            <label class="vat-label">Quantity <span class="required-star">*</span></label>
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
            <label class="vat-label">Selling Price (Excl. VAT) <span class="required-star">*</span></label>
            <input 
              type="number" 
              class="vat-input" 
              formControlName="unitPrice" 
              min="0"
              [class.is-invalid]="isLineFieldInvalid('unitPrice')" />
            <div *ngIf="isLineFieldInvalid('unitPrice')" class="vat-field-error">
              সঠিক বিক্রয় মূল্য দিন।
            </div>
          </div>

          <div class="form-group line-btn-box">
            <label class="vat-label">&nbsp;</label>
            <button 
              type="button" 
              class="btn btn-secondary w-full" 
              (click)="addLine()" 
              [disabled]="lineDraftForm.invalid">
              + Add Product
            </button>
          </div>
        </div>
      </form>

      <!-- Lines Grid Table -->
      <div class="vat-card">
        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th style="width: 40px;">#</th>
                <th>Description</th>
                <th>HS Code</th>
                <th>UOM</th>
                <th class="text-right">Qty</th>
                <th class="text-right">Unit Price</th>
                <th class="text-right">Taxable BDT</th>
                <th class="text-right">VAT Rate</th>
                <th class="text-right">VAT Amount</th>
                <th class="text-right">Total BDT</th>
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
                <td class="text-right numeric text-rose">{{ l.vatAmount | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ l.totalAmount | bdtCurrency:false }}</td>
                <td class="text-right">
                  <button type="button" class="btn-remove" (click)="removeLine(i)">Remove</button>
                </td>
              </tr>
              <tr *ngIf="lines.length === 0">
                <td colspan="11" class="empty-state">
                  No products added yet. Select a product above and click "+ Add Product".
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Financial Totals Summary -->
      <div class="totals-summary-wrapper">
        <div class="vat-card totals-card">
          <div class="totals-row">
            <span class="totals-label">Total Taxable Value:</span>
            <span class="totals-value">{{ subTotal | bdtCurrency }}</span>
          </div>
          <div class="totals-row">
            <span class="totals-label">Total Output VAT (15%):</span>
            <span class="totals-value text-rose">+ {{ totalVat | bdtCurrency }}</span>
          </div>
          <div class="totals-divider"></div>
          <div class="totals-row grand-total-row">
            <span class="grand-label">Invoice Total Payable:</span>
            <span class="grand-value">{{ grandTotal | bdtCurrency }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .sales-entry-page {
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

    .statutory-notice-banner {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 12px 16px;
      background: rgba(16, 185, 129, 0.08);
      border: 1px solid rgba(16, 185, 129, 0.25);
      border-radius: var(--radius-md, 8px);
      font-size: 0.85rem;
      color: var(--text-main);
    }

    .form-title {
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

    .line-grid {
      display: grid;
      grid-template-columns: 2fr 1fr 1fr 1fr;
      gap: 12px;
      align-items: flex-start;
    }

    @media (max-width: 768px) {
      .line-grid {
        grid-template-columns: 1fr;
      }
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

    .vat-input, .vat-select {
      width: 100%;
      padding: 8px 12px;
      background: var(--bg-surface);
      border: 1px solid var(--border-subtle);
      border-radius: 6px;
      color: var(--text-main);
      font-size: 0.875rem;
      transition: border-color 0.15s ease;
    }

    .vat-input:focus, .vat-select:focus {
      outline: none;
      border-color: var(--primary, #10b981);
    }

    .vat-input.is-invalid, .vat-select.is-invalid {
      border-color: #ef4444;
      background: rgba(239, 68, 68, 0.03);
    }

    .vat-field-error {
      font-size: 0.75rem;
      color: #ef4444;
      margin-top: 2px;
    }

    .customer-meta-badge {
      font-size: 0.75rem;
      margin-top: 4px;
    }

    .w-full {
      width: 100%;
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

    .text-rose {
      color: #fb7185;
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
export class SalesEntryComponent implements OnInit {
  public context = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private masterApi = inject(MasterDataApiService);
  private salesApi = inject(SalesApiService);
  private mockData = inject(MockDataService);
  private toast = inject(ToastService);
  private router = inject(Router);

  public customers: BusinessParty[] = [];
  public products: ProductItem[] = [];
  public branches: Branch[] = [];

  public lines: SalesLine[] = [];
  public subTotal: number = 0;
  public totalVat: number = 0;
  public grandTotal: number = 0;

  public isSubmitting: boolean = false;
  public formSubmitted: boolean = false;
  public lineSubmitted: boolean = false;

  // Reactive Header Form
  public salesForm: FormGroup = this.fb.group({
    customerId: ['', [Validators.required]],
    deliveryDate: [new Date().toISOString().split('T')[0], [Validators.required]],
    destinationAddress: ['', [Validators.required, Validators.minLength(4)]],
    vehicleNo: ['Covered Van (Dhaka Metro-U-14-8921)', [Validators.required, NbrValidators.vehicleNo(true)]],
    selectedBranchId: ['', [Validators.required]]
  });

  // Reactive Line Item Draft Form
  public lineDraftForm: FormGroup = this.fb.group({
    productId: ['', [Validators.required]],
    quantity: [1000, [Validators.required, NbrValidators.positiveNumber(1)]],
    unitPrice: [0, [Validators.required, NbrValidators.positiveNumber(0.01)]]
  });

  ngOnInit(): void {
    this.branches = this.mockData.branches;
    const initialBranchId = this.context.activeBranch().id;
    this.salesForm.patchValue({ selectedBranchId: initialBranchId });

    this.masterApi.getParties('Customer').subscribe({
      next: (data) => {
        this.customers = data || [];
        if (this.customers.length > 0 && !this.salesForm.get('customerId')?.value) {
          this.salesForm.patchValue({ customerId: this.customers[0].id });
          this.onCustomerChange();
        }
      }
    });

    this.masterApi.getProducts().subscribe({
      next: (data) => {
        this.products = (data || []).filter(p => p.salesPrice > 0);
        if (this.products.length > 0 && !this.lineDraftForm.get('productId')?.value) {
          this.lineDraftForm.patchValue({ productId: this.products[0].id });
          this.onProductSelect();
        }
      }
    });
  }

  get selectedCustomer(): BusinessParty | undefined {
    const id = this.salesForm.get('customerId')?.value;
    return this.customers.find(c => c.id === id);
  }

  onCustomerChange(): void {
    const cust = this.selectedCustomer;
    if (cust && cust.address) {
      this.salesForm.patchValue({ destinationAddress: cust.address });
    }
  }

  onProductSelect(): void {
    const prodId = this.lineDraftForm.get('productId')?.value;
    const prod = this.products.find(p => p.id === prodId);
    if (prod) {
      this.lineDraftForm.patchValue({
        unitPrice: prod.salesPrice || 0
      });
    }
  }

  isFieldInvalid(fieldName: string): boolean {
    const control = this.salesForm.get(fieldName);
    if (!control) return false;
    return control.invalid && (control.dirty || control.touched || this.formSubmitted);
  }

  getFieldError(fieldName: string): string | null {
    const control = this.salesForm.get(fieldName);
    if (!control || !control.errors) return null;
    const errors = control.errors;
    if (errors['required']) return 'এই তথ্যটি প্রদান করা আবশ্যক।';
    if (errors['invalidVehicle']) return errors['invalidVehicle'].message;
    if (errors['minlength']) return 'ঠিকানা কমপক্ষে ৪ অক্ষরের হতে হবে।';
    return null;
  }

  isLineFieldInvalid(fieldName: string): boolean {
    const control = this.lineDraftForm.get(fieldName);
    if (!control) return false;
    return control.invalid && (control.dirty || control.touched || this.lineSubmitted);
  }

  addLine(): void {
    this.lineSubmitted = true;
    if (this.lineDraftForm.invalid) {
      this.lineDraftForm.markAllAsTouched();
      return;
    }

    const { productId, quantity, unitPrice } = this.lineDraftForm.value;
    const prod = this.products.find(p => p.id === productId);
    if (!prod) return;

    const qty = Number(quantity);
    const rate = Number(unitPrice);
    const vatPct = 15;

    const taxableAmount = Math.round(qty * rate * 100) / 100;
    const vatAmount = Math.round(taxableAmount * (vatPct / 100) * 100) / 100;
    const totalAmount = taxableAmount + vatAmount;

    const line: SalesLine = {
      id: `SL-${Date.now()}`,
      productId: prod.id,
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
      vdsAmount: 0,
      totalAmount
    };

    this.lines.push(line);
    this.recomputeTotals();

    // Reset line form for next item
    this.lineSubmitted = false;
    this.lineDraftForm.patchValue({
      quantity: 1000
    });
  }

  removeLine(index: number): void {
    this.lines.splice(index, 1);
    this.recomputeTotals();
  }

  recomputeTotals(): void {
    this.subTotal = this.lines.reduce((acc, l) => acc + l.taxableAmount, 0);
    this.totalVat = this.lines.reduce((acc, l) => acc + l.vatAmount, 0);
    this.grandTotal = this.subTotal + this.totalVat;
  }

  saveInvoice(): void {
    this.formSubmitted = true;

    if (this.salesForm.invalid) {
      this.salesForm.markAllAsTouched();
      this.toast.error(
        this.context.locale() === 'bn' 
          ? 'অনুগ্রহ করে ক্রেতার তথ্য ও যানবাহন বিবরণ সঠিকভাবে পূরণ করুন।' 
          : 'Please complete customer and vehicle transport details.',
        'ত্রুটি (Validation)'
      );
      return;
    }

    if (this.lines.length === 0) {
      this.toast.error(
        this.context.locale() === 'bn' 
          ? 'কমপক্ষে একটি বিক্রয় পণ্য লাইন যোগ করুন।' 
          : 'Please add at least one product line.',
        'ত্রুটি (Validation)'
      );
      return;
    }

    const cust = this.selectedCustomer;
    if (!cust) return;

    this.isSubmitting = true;

    const formVal = this.salesForm.value;
    const branchIdGuid = this.context.resolveBranchGuid(formVal.selectedBranchId || this.context.activeBranch().id);

    const invoice: Partial<SalesInvoice> & { customerCode?: string } = {
      customerId: cust.id,
      customerCode: cust.code,
      customerName: cust.name,
      customerNameBn: cust.nameBn,
      customerBin: cust.bin,
      customerAddress: cust.address,
      destinationAddress: formVal.destinationAddress || cust.address,
      vehicleTypeAndNo: formVal.vehicleNo,
      deliveryDate: formVal.deliveryDate,
      deliveryTime: '11:00 AM',
      salesType: 'Standard',
      branchId: branchIdGuid,
      lines: this.lines,
      subTotal: this.subTotal,
      totalVat: this.totalVat,
      grandTotal: this.grandTotal,
      remarks: 'Issued statutory Mushak 6.3 Tax Challan via Reactive Form Engine.'
    };

    this.salesApi.createSalesInvoice(invoice).subscribe({
      next: (created) => {
        this.isSubmitting = false;
        this.toast.success(
          this.context.locale() === 'bn'
            ? `মূসক ৬.৩ কর চালানপত্র ${created?.challanNumber63 || ''} সফলভাবে প্রস্তুত করা হয়েছে!`
            : `Mushak 6.3 Tax Challan ${created?.challanNumber63 || ''} created successfully!`,
          this.context.locale() === 'bn' ? 'চালানপত্র প্রস্তুত সম্পন্ন' : 'Challan Issued'
        );
        this.router.navigate(['/app/sales/challan-preview'], {
          queryParams: { id: created?.id }
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        console.error('Failed to create sales invoice:', err);
        const msg = err?.error?.detail || err?.error?.message || (
          this.context.locale() === 'bn'
            ? 'বিক্রয় চালান সংরক্ষণ করা সম্ভব হয়নি।'
            : 'Failed to create sales invoice.'
        );
        this.toast.error(msg, 'ত্রুটি (Error)');
      }
    });
  }
}
