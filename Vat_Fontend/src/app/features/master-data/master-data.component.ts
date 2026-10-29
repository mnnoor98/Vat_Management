import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { MasterDataApiService } from '../../core/services/api/master-data-api.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { CsvExportService } from '../../core/services/csv-export.service';
import { ToastService } from '../../core/services/toast.service';
import { NbrValidators } from '../../shared/validators/nbr-validators';
import { BdtCurrencyPipe } from '../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../shared/pipes/bengali-number.pipe';
import { ProductItem, BusinessParty } from '../../core/models/vat-models';

@Component({
  selector: 'app-master-data',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="master-data-page">
      <!-- Page Header -->
      <div class="page-header">
        <div>
          <div class="breadcrumb-trail">
            <span>Master Data</span> / 
            <span>{{ getTabTitle() }}</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'মাস্টার ডাটা ক্যাটালগ ও এন্ট্রি সেন্টার' : 'Master Data Catalog & Tax Code Registries' }}
          </h1>
          <p class="page-subtitle">
            NBR 13-Digit BIN Registrations, Harmonized System (HS) Codes, Raw Materials & Customer/Vendor Profiles
          </p>
        </div>

        <div class="header-actions">
          <div class="tab-pill-box">
            <button class="tab-btn" [class.active-tab]="activeTab === 'products'" (click)="setTab('products')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path></svg>
              Items ({{ products.length }})
            </button>
            <button class="tab-btn highlight-tab" [class.active-tab]="activeTab === 'newProduct'" (click)="setTab('newProduct')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg>
              + New Item Entry
            </button>
            <button class="tab-btn" [class.active-tab]="activeTab === 'parties'" (click)="setTab('parties')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><path d="M23 21v-2a4 4 0 0 0-3-3.87"></path><path d="M16 3.13a4 4 0 0 1 0 7.75"></path></svg>
              Parties ({{ parties.length }})
            </button>
            <button class="tab-btn highlight-tab" [class.active-tab]="activeTab === 'newParty'" (click)="setTab('newParty')">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><line x1="20" y1="8" x2="20" y2="14"></line><line x1="23" y1="11" x2="17" y2="11"></line></svg>
              + New Party Entry
            </button>
          </div>
        </div>
      </div>

      <!-- Success Notification Toast / Banner -->
      <div *ngIf="successMessage" class="alert-banner alert-success">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
        <span>{{ successMessage }}</span>
        <button class="alert-close" (click)="successMessage = ''">&times;</button>
      </div>

      <!-- ======================================================== -->
      <!-- TAB 1: PRODUCT / ITEM CATALOG (LIST VIEW)                 -->
      <!-- ======================================================== -->
      <div *ngIf="activeTab === 'products'" class="vat-card">
        <div class="card-toolbar">
          <div class="search-box">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
            <input 
              type="text" 
              class="search-input" 
              [(ngModel)]="productSearchTerm" 
              placeholder="Search by Item Code, Product Name, or HS Code..." 
            />
          </div>

          <div class="toolbar-actions">
            <button class="btn btn-secondary" (click)="exportProductsToCsv()" title="Export Items to CSV">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Export Items (.csv)
            </button>
            <button class="btn btn-primary" (click)="setTab('newProduct')">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg>
              + Add New Item / Product (পণ্য যোগ করুন)
            </button>
          </div>
        </div>

        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th>Item Code</th>
                <th>Product Description</th>
                <th>HS Code (NBR)</th>
                <th>UOM</th>
                <th class="text-right">Standard Cost</th>
                <th class="text-right">Sales Price</th>
                <th class="text-center">VAT Rate</th>
                <th>VDS Applicable</th>
                <th>Stock Qty</th>
                <th class="text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of filteredProducts">
                <td><strong class="font-mono text-emerald">{{ p.code }}</strong></td>
                <td>
                  <strong>{{ p.name }}</strong>
                  <br>
                  <small class="text-dim font-bengali">{{ p.nameBn }}</small>
                </td>
                <td><span class="hs-tag font-mono">{{ p.hsCode }}</span></td>
                <td><span class="uom-badge">{{ p.uom }}</span></td>
                <td class="text-right numeric">{{ p.standardCost | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ p.salesPrice | bdtCurrency:false }}</td>
                <td class="text-center">
                  <span class="rate-badge">{{ p.vatRatePercent }}%</span>
                </td>
                <td>
                  <span class="vds-tag" [class.yes]="p.vdsApplicable">{{ p.vdsApplicable ? 'Yes' : 'No' }}</span>
                </td>
                <td class="numeric">{{ p.stockQty | bengaliNumber }} {{ p.uom }}</td>
                <td class="text-center">
                  <button class="btn-icon" title="View / Edit Details" (click)="editProduct(p)">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"></path><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"></path></svg>
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- ======================================================== -->
      <!-- TAB 2: NEW PRODUCT / ITEM ENTRY FORM                      -->
      <!-- ======================================================== -->
      <div *ngIf="activeTab === 'newProduct'" class="entry-card vat-card">
        <div class="entry-header">
          <div>
            <div class="entry-badge font-bengali">পণ্য ও উপকরণ নিবন্ধন ফরম</div>
            <h2 class="entry-title">New Item / Product Master Entry</h2>
            <p class="entry-subtitle">
              Bangladesh NBR Section 46 (Input Tax Credit) & Section 51 (VAT Invoice) Compliant Master Definition
            </p>
          </div>

          <button class="btn btn-secondary btn-sm" (click)="setTab('products')">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
            Back to Item Catalog
          </button>
        </div>

        <form (ngSubmit)="submitProductForm()" class="entry-form">
          <div class="form-layout-grid">
            <!-- Left Column: Form Controls -->
            <div class="form-fields-column">
              <!-- Section 1: Item Identity -->
              <h3 class="form-section-title">১. মৌলিক তথ্য ও বিবরণ (Basic Identification)</h3>
              <div class="field-row-3">
                <div class="form-group">
                  <label class="vat-label">Item Code (আইটেম কোড) *</label>
                  <input type="text" class="vat-input font-mono" [(ngModel)]="newProd.code" name="prodCode" required placeholder="e.g. RM-PET-02" />
                </div>

                <div class="form-group">
                  <label class="vat-label">Category / Type (ধরন) *</label>
                  <select class="vat-select" [(ngModel)]="selectedProductCategory" name="prodCategory">
                    <option value="RawMaterial">Raw Material (কাঁচামাল - Input Credit)</option>
                    <option value="FinishedGood">Finished Good (উৎপাদিত বিক্রয় পণ্য)</option>
                    <option value="Trading">Trading Merchandise (বাণিজ্যিক পণ্য)</option>
                    <option value="Packaging">Packaging Material (প্যাকিং উপকরণ)</option>
                    <option value="Service">Service / Expense (সেবা খাত)</option>
                  </select>
                </div>

                <div class="form-group">
                  <label class="vat-label">Unit of Measure (একক) *</label>
                  <select class="vat-select font-mono" [(ngModel)]="newProd.uom" name="prodUom">
                    <option value="KG">KG (কেজি)</option>
                    <option value="PCS">PCS (পিস)</option>
                    <option value="LTR">LTR (লিটার)</option>
                    <option value="MTR">MTR (মিটার)</option>
                    <option value="TON">TON (মেট্রিক টন)</option>
                    <option value="BAG">BAG (ব্যাগ)</option>
                    <option value="PACK">PACK (প্যাকেট)</option>
                    <option value="DOZEN">DOZEN (ডজন)</option>
                  </select>
                </div>
              </div>

              <div class="field-row-2">
                <div class="form-group">
                  <label class="vat-label">Product Name (English) *</label>
                  <input type="text" class="vat-input" [(ngModel)]="newProd.name" name="prodNameEn" required placeholder="e.g. High Density Polyethylene Granules" />
                </div>
                <div class="form-group">
                  <label class="vat-label">Product Name (বাংলা) *</label>
                  <input type="text" class="vat-input font-bengali" [(ngModel)]="newProd.nameBn" name="prodNameBn" required placeholder="যেমন: হাই ডেনসিটি পলিইথিলিন দানা" />
                </div>
              </div>

              <!-- Section 2: NBR Tariff & Valuation -->
              <h3 class="form-section-title mt-4">২. এনবিআর এইচএস কোড ও মূল্য নির্ধারণ (Tariff & Valuation)</h3>
              <div class="field-row-3">
                <div class="form-group">
                  <div class="label-with-badge">
                    <label class="vat-label">NBR 8-Digit HS Code *</label>
                    <span class="nbr-status-badge" [class.valid]="isValidHsCode(newProd.hsCode)">
                      {{ isValidHsCode(newProd.hsCode) ? '✓ ৮-ডিজিট সঠিক' : '⚠ ৮ সংখ্যা আবশ্যক' }}
                    </span>
                  </div>
                  <input 
                    type="text" 
                    class="vat-input font-mono" 
                    [(ngModel)]="newProd.hsCode" 
                    (input)="onHsCodeInput($event)" 
                    name="prodHsCode" 
                    required 
                    placeholder="e.g. 3901.20.00" 
                  />
                  <small class="field-hint">Quick pick: 
                    <a (click)="newProd.hsCode = '3907.61.00'">3907.61.00</a>, 
                    <a (click)="newProd.hsCode = '1209.91.00'">1209.91.00</a>, 
                    <a (click)="newProd.hsCode = '8421.21.00'">8421.21.00</a>
                  </small>
                </div>

                <div class="form-group">
                  <label class="vat-label">Standard Purchase Cost (BDT) *</label>
                  <input type="number" step="0.01" class="vat-input font-mono" [(ngModel)]="newProd.standardCost" name="prodCost" required placeholder="0.00" />
                </div>

                <div class="form-group">
                  <label class="vat-label">Sales Price / MRP (BDT)</label>
                  <input type="number" step="0.01" class="vat-input font-mono" [(ngModel)]="newProd.salesPrice" name="prodPrice" placeholder="0.00" />
                </div>
              </div>

              <!-- Section 3: VAT & Duty Rates -->
              <h3 class="form-section-title mt-4">৩. ভ্যাট ও সম্পূরক শুল্কের হার (VAT & SD Structure)</h3>
              <div class="field-row-3">
                <div class="form-group">
                  <label class="vat-label">Standard VAT Rate (%) *</label>
                  <select class="vat-select" [(ngModel)]="newProd.vatRatePercent" name="prodVatRate">
                    <option [value]="15">15% (Standard Rate / আদর্শ হার)</option>
                    <option [value]="10">10% (Reduced Rate / হ্রাসকৃত হার)</option>
                    <option [value]="7.5">7.5% (Reduced Rate / হ্রাসকৃত হার)</option>
                    <option [value]="5">5% (Truncated Rate / সুনির্দিষ্ট হার)</option>
                    <option [value]="0">0% (Zero-Rated Export / শূন্য হার)</option>
                  </select>
                </div>

                <div class="form-group">
                  <label class="vat-label">Supplementary Duty (SD %)</label>
                  <input type="number" step="0.01" class="vat-input font-mono" [(ngModel)]="newProd.supplementaryDutyPercent" name="prodSd" placeholder="0%" />
                </div>

                <div class="form-group">
                  <label class="vat-label">Opening Stock Quantity</label>
                  <input type="number" class="vat-input font-mono" [(ngModel)]="newProd.stockQty" name="prodStock" placeholder="0" />
                </div>
              </div>

              <!-- Section 4: Compliance Flags -->
              <h3 class="form-section-title mt-4">৪. সংবিধিবদ্ধ কমপ্লায়েন্স নীতি (Statutory Compliance Flags)</h3>
              <div class="checkbox-row">
                <label class="checkbox-label">
                  <input type="checkbox" [(ngModel)]="newProd.vdsApplicable" name="prodVds" />
                  <span>VDS Applicable (উৎস কর্তন প্রযোজ্য - Section 49)</span>
                </label>
                <label class="checkbox-label">
                  <input type="checkbox" [(ngModel)]="newProd.isExempt" name="prodExempt" />
                  <span>VAT Exempted (অব্যাহতিপ্রাপ্ত - First Schedule)</span>
                </label>
                <label class="checkbox-label">
                  <input type="checkbox" [(ngModel)]="newProd.isZeroRated" name="prodZero" />
                  <span>Zero-Rated Export Supply (রপ্তানি পণ্য)</span>
                </label>
              </div>
            </div>

            <!-- Right Column: Live Tax Card Preview -->
            <div class="form-preview-column">
              <div class="preview-card">
                <div class="preview-header">
                  <div class="preview-badge">NBR Tax Simulation</div>
                  <h4>Item Summary & Calculation</h4>
                </div>

                <div class="preview-details">
                  <div class="prev-row">
                    <span>Item Code:</span>
                    <strong class="font-mono text-emerald">{{ newProd.code || 'PENDING' }}</strong>
                  </div>
                  <div class="prev-row">
                    <span>HS Code:</span>
                    <strong class="font-mono">{{ newProd.hsCode || 'Not Set' }}</strong>
                  </div>
                  <div class="prev-row">
                    <span>Declared Name:</span>
                    <span class="prev-val">{{ newProd.name || 'Untitled Item' }}</span>
                  </div>
                  <div class="divider"></div>
                  <div class="prev-row">
                    <span>Purchase Base:</span>
                    <span class="font-mono">{{ (newProd.standardCost || 0) | bdtCurrency }}</span>
                  </div>
                  <div class="prev-row">
                    <span>Input VAT ({{ newProd.vatRatePercent }}%):</span>
                    <span class="font-mono font-bold text-emerald">
                      {{ ((newProd.standardCost || 0) * (newProd.vatRatePercent || 0) / 100) | bdtCurrency }}
                    </span>
                  </div>
                  <div class="prev-row">
                    <span>Gross Unit Cost:</span>
                    <strong class="font-mono">
                      {{ ((newProd.standardCost || 0) * (1 + (newProd.vatRatePercent || 0) / 100)) | bdtCurrency }}
                    </strong>
                  </div>
                  <div class="divider"></div>
                  <div class="prev-row">
                    <span>Sales Price (Pre-VAT):</span>
                    <span class="font-mono font-bold">{{ (newProd.salesPrice || 0) | bdtCurrency }}</span>
                  </div>
                  <div class="prev-row">
                    <span>Output VAT (Mushak 6.3):</span>
                    <span class="font-mono text-emerald font-bold">
                      {{ ((newProd.salesPrice || 0) * (newProd.vatRatePercent || 0) / 100) | bdtCurrency }}
                    </span>
                  </div>
                </div>

                <div class="preview-actions">
                  <button type="submit" class="btn btn-primary btn-block">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path><polyline points="17 21 17 13 7 13 7 21"></polyline><polyline points="7 3 7 8 15 8"></polyline></svg>
                    Save & Register Item (পণ্য সংরক্ষণ)
                  </button>
                  <button type="button" class="btn btn-secondary btn-block mt-2" (click)="resetProductForm()">
                    Reset Form
                  </button>
                </div>
              </div>
            </div>
          </div>
        </form>
      </div>

      <!-- ======================================================== -->
      <!-- TAB 3: BUSINESS PARTIES CATALOG (CUSTOMERS & SUPPLIERS)  -->
      <!-- ======================================================== -->
      <div *ngIf="activeTab === 'parties'" class="vat-card">
        <div class="card-toolbar">
          <div class="search-box">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
            <input 
              type="text" 
              class="search-input" 
              [(ngModel)]="partySearchTerm" 
              placeholder="Search by 13-Digit BIN, Business Name, or Phone..." 
            />
          </div>

          <div class="filter-pills">
            <button class="filter-pill" [class.active-pill]="partyFilterType === 'ALL'" (click)="partyFilterType = 'ALL'">
              All Parties ({{ parties.length }})
            </button>
            <button class="filter-pill" [class.active-pill]="partyFilterType === 'Supplier'" (click)="partyFilterType = 'Supplier'">
              Suppliers
            </button>
            <button class="filter-pill" [class.active-pill]="partyFilterType === 'Customer'" (click)="partyFilterType = 'Customer'">
              Customers
            </button>
          </div>

          <div class="toolbar-actions">
            <button class="btn btn-secondary" (click)="exportPartiesToCsv()" title="Export Parties to CSV">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Export Parties (.csv)
            </button>
            <button class="btn btn-primary" (click)="setTab('newParty')">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><line x1="20" y1="8" x2="20" y2="14"></line><line x1="23" y1="11" x2="17" y2="11"></line></svg>
              + Register Business Party (নতুন পার্টনার)
            </button>
          </div>
        </div>

        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th>Type</th>
                <th>Party Code</th>
                <th>Registered Business Name</th>
                <th>NBR 13-Digit BIN</th>
                <th>Address</th>
                <th>Contact Person</th>
                <th>Phone</th>
                <th>VDS Status</th>
                <th class="text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of filteredParties">
                <td>
                  <span class="party-type-badge" [class.cust]="p.partyType === 'Customer'">{{ p.partyType }}</span>
                </td>
                <td><strong class="font-mono text-dim">{{ p.code }}</strong></td>
                <td>
                  <strong>{{ p.name }}</strong>
                  <br>
                  <small class="text-dim font-bengali">{{ p.nameBn }}</small>
                </td>
                <td>
                  <strong class="font-mono text-emerald">{{ p.bin }}</strong>
                  <span class="bin-verified-icon" title="NBR Verified">&#10003;</span>
                </td>
                <td>{{ p.address }}</td>
                <td>{{ p.contactPerson }}</td>
                <td class="font-mono">{{ p.phone }}</td>
                <td>
                  <span class="vds-tag" [class.yes]="p.isVdsEntity">
                    {{ p.isVdsEntity ? 'Yes (Govt/LTU)' : 'Standard' }}
                  </span>
                </td>
                <td class="text-center">
                  <button class="btn-icon" title="View / Edit Details" (click)="editParty(p)">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 20h9"></path><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"></path></svg>
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- ======================================================== -->
      <!-- TAB 4: NEW BUSINESS PARTY (CUSTOMER / SUPPLIER) ENTRY FORM -->
      <!-- ======================================================== -->
      <div *ngIf="activeTab === 'newParty'" class="entry-card vat-card">
        <div class="entry-header">
          <div>
            <div class="entry-badge font-bengali">নতুন গ্রাহক / সরবরাহকারী নিবন্ধন</div>
            <h2 class="entry-title">Business Party Registration & NBR e-BIN Validation</h2>
            <p class="entry-subtitle">
              Enroll vendors and enterprise customers with 13-digit BIN, VAT circle, and withholding status
            </p>
          </div>

          <button class="btn btn-secondary btn-sm" (click)="setTab('parties')">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"></line><polyline points="12 19 5 12 12 5"></polyline></svg>
            Back to Party List
          </button>
        </div>

        <form (ngSubmit)="submitPartyForm()" class="entry-form">
          <div class="form-layout-grid">
            <!-- Left Column: Fields -->
            <div class="form-fields-column">
              <!-- Section 1: Entity Type & Code -->
              <h3 class="form-section-title">১. পার্টনার ক্যাটাগরি ও পরিচয় (Entity Profile)</h3>
              <div class="field-row-3">
                <div class="form-group">
                  <label class="vat-label">Party Role (ভূমিকা) *</label>
                  <select class="vat-select font-bold" [(ngModel)]="newParty.partyType" name="ptyType">
                    <option value="Supplier">Supplier / Vendor (সরবরাহকারী)</option>
                    <option value="Customer">Customer / Buyer (গ্রাহক)</option>
                  </select>
                </div>

                <div class="form-group">
                  <label class="vat-label">Party Code *</label>
                  <input type="text" class="vat-input font-mono" [(ngModel)]="newParty.code" name="ptyCode" required placeholder="e.g. SUP-2026-004" />
                </div>

                <div class="form-group">
                  <label class="vat-label">TIN (১২ ডিজিট ই-টিআইএন)</label>
                  <input type="text" class="vat-input font-mono" [(ngModel)]="newParty.tin" name="ptyTin" placeholder="123456789012" />
                </div>
              </div>

              <!-- Section 2: Name in EN & BN -->
              <div class="field-row-2">
                <div class="form-group">
                  <label class="vat-label">Registered Business Name (English) *</label>
                  <input type="text" class="vat-input" [(ngModel)]="newParty.name" name="ptyNameEn" required placeholder="e.g. Apex Polymer & Chemical Ltd" />
                </div>
                <div class="form-group">
                  <label class="vat-label">Registered Business Name (বাংলা) *</label>
                  <input type="text" class="vat-input font-bengali" [(ngModel)]="newParty.nameBn" name="ptyNameBn" required placeholder="যেমন: অ্যাপেক্স পলিমার অ্যান্ড কেমিক্যাল লি." />
                </div>
              </div>

              <!-- Section 3: NBR BIN & Addresses -->
              <h3 class="form-section-title mt-4">২. এনবিআর ১৩-ডিজিট ই-বিআইএন ও ঠিকানা (NBR BIN & Address)</h3>
              <div class="field-row-2">
                <div class="form-group">
                  <div class="label-with-badge">
                    <label class="vat-label">NBR 13-Digit BIN (ই-বিআইএন) *</label>
                    <span class="nbr-status-badge" [class.valid]="isValidBin(newParty.bin)">
                      {{ isValidBin(newParty.bin) ? '✓ ১৩-ডিজিট এনবিআর বিআইএন' : '⚠ ১৩ সংখ্যা আবশ্যক' }}
                    </span>
                  </div>
                  <input 
                    type="text" 
                    class="vat-input font-mono text-emerald" 
                    [(ngModel)]="newParty.bin" 
                    (input)="onBinInput($event)" 
                    name="ptyBin" 
                    required 
                    placeholder="001234567-0101" 
                  />
                  <small class="field-hint">এনবিআর ৯ বা ১৩ ডিজিটের স্ট্যান্ডার্ড বিজনেস আইডেন্টিফিকেশন নম্বর</small>
                </div>

                <div class="form-group">
                  <label class="vat-label">Registered VAT Circle & Division</label>
                  <input type="text" class="vat-input" placeholder="e.g. Circle-14 (Tejgaon), Dhaka North" />
                </div>
              </div>

              <div class="field-row-2">
                <div class="form-group">
                  <label class="vat-label">Registered Address (English) *</label>
                  <textarea rows="2" class="vat-input" [(ngModel)]="newParty.address" name="ptyAddrEn" required placeholder="Factory/Office Address in English..."></textarea>
                </div>
                <div class="form-group">
                  <label class="vat-label">Registered Address (বাংলা)</label>
                  <textarea rows="2" class="vat-input font-bengali" [(ngModel)]="newParty.addressBn" name="ptyAddrBn" placeholder="কারখানা বা অফিসের ঠিকানা বাংলায়..."></textarea>
                </div>
              </div>

              <!-- Section 4: Contact Person & Details -->
              <h3 class="form-section-title mt-4">৩. ফোকাল পয়েন্ট ও যোগাযোগ (Contact & Governance)</h3>
              <div class="field-row-3">
                <div class="form-group">
                  <label class="vat-label">Contact Person Name *</label>
                  <input type="text" class="vat-input" [(ngModel)]="newParty.contactPerson" name="ptyContact" required placeholder="e.g. Md. Zahid Hassan" />
                </div>
                <div class="form-group">
                  <label class="vat-label">Phone / Mobile Number *</label>
                  <input type="text" class="vat-input font-mono" [(ngModel)]="newParty.phone" name="ptyPhone" required placeholder="+880 1712-345678" />
                </div>
                <div class="form-group">
                  <label class="vat-label">Official Email</label>
                  <input type="email" class="vat-input" [(ngModel)]="newParty.email" name="ptyEmail" placeholder="accounts@business.com.bd" />
                </div>
              </div>

              <!-- Section 5: Withholding Tax Entity Status -->
              <div class="checkbox-row mt-3">
                <label class="checkbox-label">
                  <input type="checkbox" [(ngModel)]="newParty.isVdsEntity" name="ptyVds" />
                  <span>Withholding Entity / VDS Authority (সরকারি সংস্থা, ব্যাংক, আর্থিক প্রতিষ্ঠান, এলটিইউ বা নিবন্ধিত বৃহৎ করদাতা - মূসক ৬.৬ প্রযোজ্য)</span>
                </label>
              </div>
            </div>

            <!-- Right Column: Verification & Summary Preview -->
            <div class="form-preview-column">
              <div class="preview-card">
                <div class="preview-header">
                  <div class="preview-badge">BIN Verification</div>
                  <h4>Party Validation Status</h4>
                </div>

                <div class="preview-details">
                  <div class="prev-row">
                    <span>Party Role:</span>
                    <strong class="party-type-badge" [class.cust]="newParty.partyType === 'Customer'">{{ newParty.partyType }}</strong>
                  </div>
                  <div class="prev-row">
                    <span>Party Code:</span>
                    <strong class="font-mono">{{ newParty.code || 'PENDING' }}</strong>
                  </div>
                  <div class="prev-row">
                    <span>Company Name:</span>
                    <span class="prev-val">{{ newParty.name || 'Untitled Entity' }}</span>
                  </div>
                  <div class="divider"></div>
                  <div class="prev-row">
                    <span>13-Digit BIN:</span>
                    <strong class="font-mono text-emerald">{{ newParty.bin || '---' }}</strong>
                  </div>
                  <div class="prev-row">
                    <span>NBR Portal Status:</span>
                    <span class="text-emerald font-bold">&#9679; Active Taxpayer</span>
                  </div>
                  <div class="prev-row">
                    <span>Withholding / VDS:</span>
                    <span class="font-bold">{{ newParty.isVdsEntity ? 'Mandatory Withholder' : 'Standard Entity' }}</span>
                  </div>
                </div>

                <div class="preview-actions">
                  <button type="submit" class="btn btn-primary btn-block">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path><polyline points="17 21 17 13 7 13 7 21"></polyline><polyline points="7 3 7 8 15 8"></polyline></svg>
                    Register Business Party (পার্টনার নিবন্ধন)
                  </button>
                  <button type="button" class="btn btn-secondary btn-block mt-2" (click)="resetPartyForm()">
                    Reset Form
                  </button>
                </div>
              </div>
            </div>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .master-data-page { display: flex; flex-direction: column; gap: 20px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; }
    .page-title { font-size: 1.5rem; font-weight: 800; }
    .page-subtitle { font-size: 0.85rem; color: var(--text-muted); }
    .breadcrumb-trail { font-size: 0.8rem; color: var(--text-dim); margin-bottom: 4px; }
    
    .tab-pill-box { display: flex; gap: 4px; background: var(--bg-card); padding: 4px; border-radius: var(--radius-md); border: 1px solid var(--border-card); }
    .tab-btn { display: inline-flex; align-items: center; gap: 6px; background: transparent; border: none; color: var(--text-muted); padding: 7px 14px; border-radius: var(--radius-sm); font-size: 0.8rem; font-weight: 700; cursor: pointer; transition: all 0.2s ease; }
    .tab-btn:hover { color: var(--text-main); background: var(--bg-card-hover); }
    .active-tab { background: var(--vat-emerald) !important; color: #ffffff !important; }
    .highlight-tab { color: var(--vat-teal); }

    .alert-banner { display: flex; align-items: center; gap: 10px; padding: 12px 18px; border-radius: var(--radius-md); font-size: 0.9rem; }
    .alert-success { background: rgba(16, 185, 129, 0.15); border: 1px solid #10b981; color: #34d399; }
    .alert-close { margin-left: auto; background: transparent; border: none; font-size: 1.2rem; color: inherit; cursor: pointer; }

    .card-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 14px; margin-bottom: 16px; }
    .search-box { display: flex; align-items: center; gap: 8px; background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 8px 14px; flex: 1; max-width: 450px; }
    .search-input { background: transparent; border: none; color: var(--text-main); font-size: 0.85rem; outline: none; width: 100%; }
    .toolbar-actions { display: flex; gap: 10px; }

    .filter-pills { display: flex; gap: 6px; }
    .filter-pill { background: var(--bg-card-subtle); border: 1px solid var(--border-card); color: var(--text-muted); padding: 6px 12px; border-radius: var(--radius-sm); font-size: 0.78rem; font-weight: 700; cursor: pointer; }
    .active-pill { background: #2e3e57; border-color: #64748b; color: var(--text-main); }

    .hs-tag { background: rgba(59, 130, 246, 0.15); color: #60a5fa; padding: 2px 6px; border-radius: var(--radius-sm); font-size: 0.75rem; }
    .uom-badge { background: var(--bg-card-subtle); border: 1px solid var(--border-card); padding: 2px 6px; border-radius: var(--radius-sm); font-size: 0.75rem; font-family: var(--font-mono); }
    .rate-badge { background: rgba(16, 185, 129, 0.12); color: #34d399; font-weight: 800; padding: 2px 8px; border-radius: var(--radius-sm); font-size: 0.78rem; }
    .vds-tag { font-size: 0.72rem; font-weight: 700; padding: 2px 8px; border-radius: 9999px; background: var(--bg-card-subtle); color: var(--text-dim); }
    .vds-tag.yes { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .party-type-badge { font-size: 0.72rem; font-weight: 800; text-transform: uppercase; padding: 2px 8px; border-radius: var(--radius-sm); background: rgba(139, 92, 246, 0.15); color: #a78bfa; }
    .party-type-badge.cust { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .bin-verified-icon { color: #34d399; margin-left: 4px; font-weight: bold; }
    .btn-icon { background: transparent; border: 1px solid var(--border-card); color: var(--text-muted); padding: 5px 8px; border-radius: var(--radius-sm); cursor: pointer; }
    .btn-icon:hover { color: var(--text-main); background: var(--bg-card-hover); }

    /* Entry Form Styling */
    .entry-card { padding: 24px; }
    .entry-header { display: flex; align-items: flex-start; justify-content: space-between; border-bottom: 1px solid var(--border-card); padding-bottom: 16px; margin-bottom: 20px; }
    .entry-badge { display: inline-block; background: rgba(16, 185, 129, 0.15); color: #34d399; font-size: 0.78rem; font-weight: 800; padding: 2px 8px; border-radius: var(--radius-sm); margin-bottom: 4px; }
    .entry-title { font-size: 1.3rem; font-weight: 800; color: var(--text-main); }
    .entry-subtitle { font-size: 0.8rem; color: var(--text-muted); }

    .form-layout-grid { display: grid; grid-template-columns: 2.2fr 1fr; gap: 24px; }
    @media (max-width: 1024px) {
      .form-layout-grid { grid-template-columns: 1fr; }
    }

    .form-section-title { font-size: 0.95rem; font-weight: 700; color: var(--vat-teal); margin-bottom: 12px; }
    .field-row-3 { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 14px; margin-bottom: 14px; }
    .field-row-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 14px; }
    @media (max-width: 768px) {
      .field-row-3, .field-row-2 { grid-template-columns: 1fr; }
    }

    .form-group { display: flex; flex-direction: column; gap: 4px; }
    .field-hint { font-size: 0.72rem; color: var(--text-dim); margin-top: 3px; }
    .field-hint a { color: var(--vat-teal); cursor: pointer; text-decoration: underline; margin-right: 4px; }
    .checkbox-row { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
    .checkbox-label { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; color: var(--text-main); cursor: pointer; }
    .btn-block { width: 100%; }

    /* Live Preview Column */
    .preview-card { background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 20px; display: flex; flex-direction: column; gap: 16px; position: sticky; top: 20px; }
    .preview-header { display: flex; flex-direction: column; gap: 4px; }
    .preview-badge { font-size: 0.72rem; font-weight: 800; color: #60a5fa; text-transform: uppercase; letter-spacing: 0.05em; }
    .preview-details { display: flex; flex-direction: column; gap: 10px; font-size: 0.85rem; }
    .prev-row { display: flex; justify-content: space-between; align-items: center; }
    .prev-val { font-weight: 600; text-align: right; max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .divider { height: 1px; background: var(--border-card); margin: 4px 0; }
    .mt-4 { margin-top: 24px; }
    .mt-2 { margin-top: 8px; }
    .mt-3 { margin-top: 12px; }

    .label-with-badge { display: flex; justify-content: space-between; align-items: center; }
    .nbr-status-badge { font-size: 0.68rem; font-weight: 700; color: var(--vat-rose); }
    .nbr-status-badge.valid { color: var(--vat-emerald); }
  `]
})
export class MasterDataComponent implements OnInit {
  private csvExport = inject(CsvExportService);
  private toast = inject(ToastService);

  public activeTab: 'products' | 'newProduct' | 'parties' | 'newParty' = 'products';
  public products: ProductItem[] = [];
  public parties: BusinessParty[] = [];
  
  public productSearchTerm = '';
  public partySearchTerm = '';
  public partyFilterType: 'ALL' | 'Customer' | 'Supplier' = 'ALL';
  public selectedProductCategory = 'RawMaterial';

  public successMessage = '';

  // New Product Model Initial State
  public newProd: ProductItem = {
    id: '',
    code: 'RM-NEW-01',
    name: '',
    nameBn: '',
    hsCode: '3901.20.00',
    uom: 'KG',
    uomBn: 'কেজি',
    standardCost: 150.00,
    salesPrice: 0,
    vatRatePercent: 15,
    supplementaryDutyPercent: 0,
    isExempt: false,
    isZeroRated: false,
    vdsApplicable: true,
    stockQty: 5000
  };

  // New Business Party Initial State
  public newParty: BusinessParty = {
    id: '',
    partyType: 'Supplier',
    code: 'SUP-2026-003',
    name: '',
    nameBn: '',
    bin: '001234567-0101',
    tin: '123456789012',
    address: '',
    addressBn: '',
    contactPerson: '',
    phone: '+880 1712-345678',
    email: '',
    isVdsEntity: true
  };

  constructor(
    public context: CompanyContextService,
    private masterApi: MasterDataApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadData();
    this.detectRoute();
  }

  public detectRoute(): void {
    const url = this.router.url;
    if (url.includes('new-product')) {
      this.activeTab = 'newProduct';
    } else if (url.includes('new-party')) {
      this.activeTab = 'newParty';
    } else if (url.includes('parties')) {
      this.activeTab = 'parties';
    } else {
      this.activeTab = 'products';
    }
  }

  public loadData(): void {
    this.masterApi.getProducts().subscribe(data => this.products = data);
    this.masterApi.getParties().subscribe(data => this.parties = data);
  }

  public setTab(tab: 'products' | 'newProduct' | 'parties' | 'newParty'): void {
    this.activeTab = tab;
    this.successMessage = '';
  }

  public getTabTitle(): string {
    switch (this.activeTab) {
      case 'products': return 'Items & Products Catalog';
      case 'newProduct': return 'New Item Entry Form';
      case 'parties': return 'Customers & Suppliers';
      case 'newParty': return 'New Business Party Registration';
    }
  }

  // Filtered Products
  get filteredProducts(): ProductItem[] {
    if (!this.productSearchTerm) return this.products;
    const term = this.productSearchTerm.toLowerCase();
    return this.products.filter(p => 
      p.code.toLowerCase().includes(term) ||
      p.name.toLowerCase().includes(term) ||
      p.hsCode.toLowerCase().includes(term)
    );
  }

  // Filtered Parties
  get filteredParties(): BusinessParty[] {
    let result = this.parties;
    if (this.partyFilterType !== 'ALL') {
      result = result.filter(p => p.partyType === this.partyFilterType);
    }
    if (this.partySearchTerm) {
      const term = this.partySearchTerm.toLowerCase();
      result = result.filter(p => 
        p.name.toLowerCase().includes(term) ||
        p.bin.toLowerCase().includes(term) ||
        p.phone.toLowerCase().includes(term) ||
        p.code.toLowerCase().includes(term)
      );
    }
    return result;
  }

  // NBR Input Masking & Validation
  public onHsCodeInput(event: Event): void {
    NbrValidators.handleHsCodeInput(event);
    const input = event.target as HTMLInputElement;
    if (input) {
      this.newProd.hsCode = input.value;
    }
  }

  public onBinInput(event: Event): void {
    NbrValidators.handleBinInput(event);
    const input = event.target as HTMLInputElement;
    if (input) {
      this.newParty.bin = input.value;
    }
  }

  public isValidHsCode(hs: string): boolean {
    if (!hs) return false;
    const clean = String(hs).replace(/\D/g, '');
    return clean.length === 8;
  }

  public isValidBin(bin: string): boolean {
    if (!bin) return false;
    const clean = String(bin).replace(/\D/g, '');
    return clean.length === 13;
  }

  // Submit Product Form
  public submitProductForm(): void {
    if (!this.newProd.code || !this.newProd.name || !this.newProd.hsCode) {
      this.toast.warning('অনুগ্রহ করে আইটেম কোড, নাম এবং এইচএস কোড পূরণ করুন।', 'তথ্য অসম্পূর্ণ (Missing Fields)');
      return;
    }

    if (!this.isValidHsCode(this.newProd.hsCode)) {
      this.toast.error('এইচএস কোড অবশ্যই ৮ সংখ্যার এনবিআর ফরম্যাটে হতে হবে (যেমন: 3901.20.00)', 'ভুল এইচএস কোড (Invalid HS Code)');
      return;
    }

    const itemToSave: ProductItem = {
      ...this.newProd,
      id: `PROD-${Date.now().toString().slice(-4)}`
    };

    this.masterApi.createProduct(itemToSave).subscribe(saved => {
      this.loadData();
      const msg = `পণ্য '${saved.name}' (${saved.code}) সফলভাবে এনবিআর মাস্টার ক্যাটালগে যুক্ত হয়েছে!`;
      this.successMessage = msg;
      this.toast.success(msg, 'পণ্য সংরক্ষণ সম্পন্ন (Item Registered)');
      this.resetProductForm();
      this.activeTab = 'products';
    });
  }

  public resetProductForm(): void {
    this.newProd = {
      id: '',
      code: `RM-ITEM-${Math.floor(100 + Math.random() * 900)}`,
      name: '',
      nameBn: '',
      hsCode: '3901.20.00',
      uom: 'KG',
      uomBn: 'কেজি',
      standardCost: 100.00,
      salesPrice: 0,
      vatRatePercent: 15,
      supplementaryDutyPercent: 0,
      isExempt: false,
      isZeroRated: false,
      vdsApplicable: true,
      stockQty: 1000
    };
  }

  public editProduct(prod: ProductItem): void {
    this.newProd = { ...prod };
    this.activeTab = 'newProduct';
  }

  // Submit Party Form
  public submitPartyForm(): void {
    if (!this.newParty.name || !this.newParty.bin || !this.newParty.code) {
      this.toast.warning('অনুগ্রহ করে পার্টনার কোড, প্রতিষ্ঠানের নাম এবং ১৩-ডিজিট বিআইএন পূরণ করুন।', 'তথ্য অসম্পূর্ণ (Missing Fields)');
      return;
    }

    if (!this.isValidBin(this.newParty.bin)) {
      this.toast.error('বিআইএন অবশ্যই ১৩ সংখ্যার এনবিআর ফরম্যাটে হতে হবে (যেমন: 001234567-0101)', 'ভুল বিআইএন (Invalid BIN)');
      return;
    }

    const partyToSave: BusinessParty = {
      ...this.newParty,
      id: `PTY-${Date.now().toString().slice(-4)}`
    };

    this.masterApi.createParty(partyToSave).subscribe(saved => {
      this.loadData();
      const msg = `নতুন ${saved.partyType} '${saved.name}' (BIN: ${saved.bin}) সফলভাবে নিবন্ধিত হয়েছে!`;
      this.successMessage = msg;
      this.toast.success(msg, 'পার্টনার নিবন্ধন সম্পন্ন (Party Enrolled)');
      this.resetPartyForm();
      this.activeTab = 'parties';
    });
  }

  public resetPartyForm(): void {
    this.newParty = {
      id: '',
      partyType: 'Supplier',
      code: `PTY-${Math.floor(1000 + Math.random() * 9000)}`,
      name: '',
      nameBn: '',
      bin: '001234567-0101',
      tin: '123456789012',
      address: '',
      addressBn: '',
      contactPerson: '',
      phone: '+880 1712-345678',
      email: '',
      isVdsEntity: true
    };
  }

  public editParty(party: BusinessParty): void {
    this.newParty = { ...party };
    this.activeTab = 'newParty';
  }

  public exportProductsToCsv(): void {
    const headers = [
      'Item Code',
      'Product Name (EN)',
      'Product Name (BN)',
      'NBR HS Code',
      'UOM',
      'Standard Cost (BDT)',
      'Sales Price (BDT)',
      'VAT Rate (%)',
      'Supplementary Duty (%)',
      'VDS Applicable',
      'Exempted',
      'Zero-Rated',
      'Stock Qty'
    ];

    const rows = this.filteredProducts.map(p => [
      p.code,
      p.name,
      p.nameBn || '',
      p.hsCode,
      p.uom,
      p.standardCost,
      p.salesPrice,
      p.vatRatePercent,
      p.supplementaryDutyPercent,
      p.vdsApplicable ? 'Yes' : 'No',
      p.isExempt ? 'Yes' : 'No',
      p.isZeroRated ? 'Yes' : 'No',
      p.stockQty
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `Master_Item_Catalog_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }

  public exportPartiesToCsv(): void {
    const headers = [
      'Party Code',
      'Role',
      'Business Name (EN)',
      'Business Name (BN)',
      '13-Digit BIN',
      'TIN',
      'Address',
      'Contact Person',
      'Phone',
      'Email',
      'VDS Withholding Entity'
    ];

    const rows = this.filteredParties.map(p => [
      p.code,
      p.partyType,
      p.name,
      p.nameBn || '',
      p.bin,
      p.tin || '',
      p.address,
      p.contactPerson,
      p.phone,
      p.email,
      p.isVdsEntity ? 'Yes' : 'No'
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `Master_Business_Parties_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}
