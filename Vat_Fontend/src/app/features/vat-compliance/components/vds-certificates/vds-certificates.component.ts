import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { ComplianceApiService } from '../../../../core/services/api/compliance-api.service';
import { PurchaseApiService } from '../../../../core/services/api/purchase-api.service';
import { PdfExportService } from '../../../../core/services/pdf-export.service';
import { CsvExportService } from '../../../../core/services/csv-export.service';
import { ToastService } from '../../../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { VdsCertificate, PurchaseInvoice } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-vds-certificates',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe],
  template: `
    <div class="vds-tab-container">
      <!-- VDS KPI Metrics Bar -->
      <div class="metrics-grid no-print mb-4">
        <div class="metric-card">
          <span class="metric-title font-bengali">মোট ইস্যুকৃত মূসক ৬.৬ সনদপত্র</span>
          <span class="metric-value font-mono text-emerald">{{ vdsCertificates.length }} টি</span>
          <small class="text-dim">সরবরাহকারী চালান হতে কর্তিত</small>
        </div>
        <div class="metric-card">
          <span class="metric-title font-bengali">মোট কর্তিত উৎসে মূসক (VDS)</span>
          <span class="metric-value font-mono text-amber">{{ totalVdsWithheldAmount | bdtCurrency }}</span>
          <small class="text-dim">রিটার্ন অংশ-৫ বৃদ্ধিকারী সমন্বয়</small>
        </div>
        <div class="metric-card">
          <span class="metric-title font-bengali">কোষাগারে জমাকৃত ট্রেজারি ই-চালান</span>
          <span class="metric-value font-mono text-teal">{{ totalVdsDepositedAmount | bdtCurrency }}</span>
          <small class="text-dim">কোড: ১/১১৩৩/০০১০/০৩১১</small>
        </div>
        <div class="metric-card action-card">
          <span class="metric-title font-bengali">উৎসে কর অ্যাকশন</span>
          <div class="btn-group mt-2">
            <button class="btn btn-primary btn-sm" (click)="openIssueVdsModal()">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="12" y1="5" x2="12" y2="19"></line>
                <line x1="5" y1="12" x2="19" y2="12"></line>
              </svg>
              + Issue Mushak 6.6
            </button>
            <button class="btn btn-secondary btn-sm" (click)="exportToCsv()">
              Export CSV
            </button>
            <button class="btn btn-secondary btn-sm" (click)="printDocument()">
              Print
            </button>
            <button class="btn btn-secondary btn-sm" (click)="downloadPdf()">
              Download PDF
            </button>
          </div>
        </div>
      </div>

      <!-- VDS Register Table -->
      <div class="vat-card no-print mb-4">
        <div class="table-header-row">
          <div>
            <h3 class="form-title font-bengali" style="margin-bottom: 2px;">উৎস কর্তন সনদপত্র রেজিস্টার (Mushak 6.6 Register)</h3>
            <p class="rule-ref font-bengali">[বিধি ৪০ এর উপ-বিধি (১) এর দফা (ঙ) ও ধারা ৪৯ দ্রষ্টব্য]</p>
          </div>
        </div>
        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th style="width: 45px;" class="text-center">ক্র.</th>
                <th>সনদপত্র নম্বর</th>
                <th>ইস্যুর তারিখ</th>
                <th>সরবরাহকারীর নাম ও বিআইএন</th>
                <th>মূল চালানপত্র ও তারিখ</th>
                <th class="text-right">মোট মূল্য</th>
                <th class="text-right">কর্তিত মূসক (VDS)</th>
                <th>ট্রেজারি ই-চালান</th>
                <th class="text-center">অবস্থা</th>
                <th class="text-center">অ্যাকশন</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let cert of vdsCertificates; let i = index" [class.highlight-row]="selectedVdsCertificate?.id === cert.id">
                <td class="text-center font-bengali">{{ i + 1 }}</td>
                <td>
                  <strong class="font-mono text-emerald">{{ cert.certificateNumber }}</strong>
                </td>
                <td>{{ cert.issueDate }}</td>
                <td>
                  <strong>{{ cert.supplierName }}</strong>
                  <br><small class="font-mono text-dim">BIN: {{ cert.supplierBin }}</small>
                </td>
                <td>
                  <span class="font-mono">{{ cert.invoiceNumber }}</span>
                  <br><small class="text-dim">{{ cert.invoiceDate }}</small>
                </td>
                <td class="text-right numeric font-bold">{{ cert.totalAmount | bdtCurrency }}</td>
                <td class="text-right numeric font-bold text-amber">
                  {{ cert.vdsAmount | bdtCurrency }}
                  <br><small class="text-dim">({{ cert.vdsRate }}%)</small>
                </td>
                <td>
                  <ng-container *ngIf="cert.treasuryChallanNo; else pendingChallan">
                    <span class="font-mono text-teal font-bold">{{ cert.treasuryChallanNo }}</span>
                    <br><small class="text-dim">{{ cert.treasuryChallanDate }}</small>
                  </ng-container>
                  <ng-template #pendingChallan>
                    <span class="badge badge-warning">অপেক্ষমান</span>
                  </ng-template>
                </td>
                <td class="text-center">
                  <span class="badge" [ngClass]="cert.status === 'Deposited' ? 'badge-success' : 'badge-info'">
                    {{ cert.status === 'Deposited' ? 'জমাকৃত' : 'ইস্যুকৃত' }}
                  </span>
                </td>
                <td class="text-center">
                  <div class="btn-group">
                    <button class="btn btn-secondary btn-xs" (click)="selectVdsCertificate(cert)" title="View Statutory Mushak 6.6 Form">
                      প্রিভিউ
                    </button>
                    <button *ngIf="cert.status !== 'Deposited'" class="btn btn-primary btn-xs" (click)="openDepositModal(cert)" title="Link Treasury e-Challan">
                      চালান লিংক
                    </button>
                  </div>
                </td>
              </tr>
              <tr *ngIf="vdsCertificates.length === 0">
                <td colspan="10" class="text-center py-4 text-dim">কোন মূসক ৬.৬ সনদপত্র পাওয়া যায়নি।</td>
              </tr>
            </tbody>
            <tfoot *ngIf="vdsCertificates.length > 0">
              <tr class="total-row">
                <th colspan="6" class="text-right font-bengali">সর্বমোট কর্তিত উৎসে মূসক:</th>
                <th class="text-right numeric text-amber font-bold">{{ totalVdsWithheldAmount | bdtCurrency }}</th>
                <th colspan="3"></th>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      <!-- STATUTORY NBR MUSHAK 6.6 FORM PREVIEW -->
      <div id="vds-66-statutory-preview" *ngIf="selectedVdsCertificate" class="statutory-paper">
        <div class="govt-heading">
          <h3 class="font-bengali">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4 class="font-bengali">জাতীয় রাজস্ব বোর্ড</h4>
          <div class="form-badge font-bengali">মূসক-৬.৬</div>
          <h2 class="register-title font-bengali">উৎস কর্তন সনদপত্র</h2>
          <p class="rule-ref font-bengali">[বিধি ৪০ এর উপ-বিধি (১) এর দফা (ঙ) দ্রষ্টব্য]</p>
        </div>

        <div class="taxpayer-meta-box">
          <div class="meta-row">
            <span class="meta-label">সনদপত্র নম্বর:</span>
            <strong class="font-mono text-emerald text-lg">{{ selectedVdsCertificate.certificateNumber }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">ইস্যুর তারিখ:</span>
            <strong>{{ selectedVdsCertificate.issueDate }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">কর্তনকারী সত্ত্বার নাম:</span>
            <strong class="font-bengali">{{ context.activeCompany().nameBn || context.activeCompany().name }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">কর্তনকারীর বিআইএন (BIN):</span>
            <strong class="font-mono text-emerald">{{ context.activeCompany().bin }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">কর্তনকারীর ঠিকানা:</span>
            <span>{{ context.activeCompany().addressBn || context.activeCompany().address }}</span>
          </div>
          <div class="meta-row">
            <span class="meta-label">ইস্যুকারী শাখা / ডিপো:</span>
            <strong>{{ context.activeBranch().name }}</strong>
          </div>
        </div>

        <!-- Supplier Meta Box -->
        <div class="part-section">
          <div class="part-header font-bengali">যে ব্যক্তির নিকট হইতে মূসক কর্তন করা হইয়াছে তাহার বিবরণ:</div>
          <div class="taxpayer-meta-box" style="margin-bottom: 12px;">
            <div class="meta-row">
              <span class="meta-label">সরবরাহকারীর নাম:</span>
              <strong>{{ selectedVdsCertificate.supplierName }}</strong>
            </div>
            <div class="meta-row">
              <span class="meta-label">সরবরাহকারীর বিআইএন:</span>
              <strong class="font-mono">{{ selectedVdsCertificate.supplierBin }}</strong>
            </div>
            <div class="meta-row" style="grid-column: 1 / -1;">
              <span class="meta-label">সরবরাহকারীর ঠিকানা:</span>
              <span>{{ selectedVdsCertificate.supplierAddress || 'হেমায়েতপুর, সাভার, ঢাকা' }}</span>
            </div>
          </div>
        </div>

        <!-- Statutory Deductions Details Table -->
        <div class="vat-table-container">
          <table class="vat-table mushak-grid">
            <thead>
              <tr>
                <th class="text-center font-bengali">ক্রমিক</th>
                <th>সরবরাহকারীর চালানপত্র নম্বর ও তারিখ</th>
                <th class="text-right">মোট পরিশোধিত মূল্য (টাকা)</th>
                <th class="text-right">চালানে উল্লিখিত মূসকের পরিমাণ (টাকা)</th>
                <th class="text-right">কর্তিত মূসকের পরিমাণ (টাকা)</th>
                <th>ট্রেজারি ই-চালান নম্বর ও জমার তারিখ</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td class="text-center font-bengali">১</td>
                <td>
                  <strong class="font-mono">{{ selectedVdsCertificate.invoiceNumber }}</strong>
                  <br><small class="text-dim">তারিখ: {{ selectedVdsCertificate.invoiceDate }}</small>
                </td>
                <td class="text-right numeric font-bold">{{ selectedVdsCertificate.totalAmount | bdtCurrency }}</td>
                <td class="text-right numeric">{{ selectedVdsCertificate.vatAmount | bdtCurrency }}</td>
                <td class="text-right numeric font-bold text-amber">{{ selectedVdsCertificate.vdsAmount | bdtCurrency }}</td>
                <td>
                  <ng-container *ngIf="selectedVdsCertificate.treasuryChallanNo; else unlinked">
                    <strong class="font-mono text-teal">{{ selectedVdsCertificate.treasuryChallanNo }}</strong>
                    <br><small class="text-dim">{{ selectedVdsCertificate.treasuryChallanDate }}</small>
                  </ng-container>
                  <ng-template #unlinked>
                    <span class="badge badge-warning">কোষাগারে জমা অপেক্ষমান</span>
                  </ng-template>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div style="margin-top: 40px; display: flex; justify-content: space-between; align-items: flex-end;">
          <div class="font-bengali" style="font-size: 0.8rem; color: var(--text-muted); max-width: 450px;">
            "প্রত্যয়ন করা যাইতেছে যে, মূল্য সংযোজন কর ও সম্পূরক শুল্ক আইন, ২০১২ এর ধারা ৪৯ এবং বিধি ৪০(১)(ঙ) মোতাবেক উৎসে মূসক কর্তনপূর্বক সরকারি কোষাগারে জমা প্রদান করা হইয়াছে/হইবে।"
          </div>
          <div style="display: flex; flex-direction: column; align-items: center; gap: 6px;">
            <div style="width: 200px; border-top: 1px dashed var(--border-subtle);"></div>
            <span class="font-bengali font-bold" style="font-size: 0.85rem;">কর্তনকারী কর্মকর্তার স্বাক্ষর ও সীল</span>
            <small class="text-dim">ম্যানেজার (ট্যাক্স অ্যান্ড কমপ্লায়েন্স)</small>
          </div>
        </div>
      </div>

      <!-- Modal 1: Issue New Mushak 6.6 VDS Certificate -->
      <div class="modal-backdrop" *ngIf="isIssueVdsModalOpen">
        <div class="modal-card modal-lg">
          <div class="modal-header">
            <div>
              <div class="modal-badge-row">
                <span class="form-badge font-bengali" style="margin: 0; padding: 2px 10px; font-size: 0.85rem;">মূসক-৬.৬</span>
                <h3 class="font-bengali" style="margin: 0; font-size: 1.15rem; font-weight: 800;">নতুন উৎস কর্তন সনদপত্র ইস্যু (Issue VDS Certificate)</h3>
              </div>
              <p class="modal-subtitle font-bengali">[বিধি ৪০(১)(ঙ) দ্রষ্টব্য] — সরবরাহকারীর বিল হতে উৎসে মূসক কর্তনপূর্বক সনদ ইস্যু</p>
            </div>
            <button class="btn-close" (click)="isIssueVdsModalOpen = false">✕</button>
          </div>

          <div class="modal-body">
            <div class="form-group" *ngIf="livePurchases.length > 0">
              <label class="form-label font-bengali">ক্রয় চালান হতে স্বয়ংক্রিয় পূরণ করুন (Auto-fill from Purchase Invoice):</label>
              <select class="form-select font-mono" (change)="onSelectPurchaseForVds($any($event.target).value)">
                <option value="">-- ক্রয় চালান নির্বাচন করুন (বা ম্যানুয়ালি এন্ট্রি দিন) --</option>
                <option *ngFor="let p of livePurchases" [value]="p.id">
                  {{ p.supplierName }} | চালান: {{ p.supplierInvoiceNumber || p.invoiceNumber }} | মূল্য: ৳{{ p.grandTotal | number:'1.2-2' }} | ভ্যাট: ৳{{ p.totalVat | number:'1.2-2' }}
                </option>
              </select>
            </div>

            <div class="form-grid-2">
              <div class="form-group">
                <label class="form-label">সরবরাহকারীর নাম (Supplier Name) *</label>
                <input type="text" class="form-control" [(ngModel)]="newVds.supplierName" placeholder="e.g. Meghna Petrochemicals Corp" required>
              </div>

              <div class="form-group">
                <label class="form-label">সরবরাহকারীর বিআইএন (13-Digit BIN) *</label>
                <input type="text" class="form-control font-mono" [(ngModel)]="newVds.supplierBin" placeholder="e.g. 001928374-0101" required>
              </div>

              <div class="form-group" style="grid-column: 1 / -1;">
                <label class="form-label">সরবরাহকারীর ঠিকানা (Supplier Address)</label>
                <input type="text" class="form-control" [(ngModel)]="newVds.supplierAddress" placeholder="e.g. Plot #12, Tejgaon I/A, Dhaka">
              </div>

              <div class="form-group">
                <label class="form-label">চালানপত্র নম্বর (Supplier Invoice / Challan) *</label>
                <input type="text" class="form-control font-mono" [(ngModel)]="newVds.invoiceNumber" placeholder="e.g. BEN-INV-8910" required>
              </div>

              <div class="form-group">
                <label class="form-label">চালানের তারিখ (Invoice Date) *</label>
                <input type="date" class="form-control font-mono" [(ngModel)]="newVds.invoiceDate" required>
              </div>

              <div class="form-group">
                <label class="form-label">মোট চালানের মূল্য (Total Invoice Amount BDT) *</label>
                <input type="number" class="form-control font-mono" [(ngModel)]="newVds.totalAmount" placeholder="0.00" required>
              </div>

              <div class="form-group">
                <label class="form-label">চালানে উল্লিখিত মূসক (Invoice 15% VAT BDT) *</label>
                <input type="number" class="form-control font-mono" [(ngModel)]="newVds.vatAmount" (ngModelChange)="calculateVdsAmount()" placeholder="0.00" required>
              </div>

              <div class="form-group">
                <label class="form-label">উৎস কর্তনের হার (Statutory VDS Rate %) *</label>
                <select class="form-select font-mono" [(ngModel)]="newVds.vdsRate" (ngModelChange)="calculateVdsAmount()">
                  <option [ngValue]="100">100% (পূর্ণ মূসক কর্তন - ক্রয়মূল্য নির্বিশেষে)</option>
                  <option [ngValue]="50">50% (প্রমিত ৫০% উৎসে কর্তন)</option>
                  <option [ngValue]="43.33">43.33% (নির্দিষ্ট সেবা খাতে)</option>
                  <option [ngValue]="33.33">33.33% (নির্মাণ ও পরিবহন সেবা)</option>
                </select>
              </div>

              <div class="form-group">
                <label class="form-label font-bold text-amber">কর্তনযোগ্য মূসকের পরিমাণ (VDS Deducted BDT) *</label>
                <input type="number" class="form-control font-mono font-bold text-amber text-lg" [(ngModel)]="newVds.vdsAmount" required>
              </div>
            </div>

            <div class="vds-info-notice font-bengali">
              ℹ️ উৎসে কর্তন সনদপত্র (মূসক ৬.৬) ইস্যু করার ৩ কার্যদিবসের মধ্যে সরবরাহকারীকে সরবরাহ করিতে হইবে এবং পরবর্তী মেয়াদের রিটার্নে বৃদ্ধিকারী সমন্বয় হিসেবে অন্তর্ভুক্ত হইবে।
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" (click)="isIssueVdsModalOpen = false">বাতিল (Cancel)</button>
            <button class="btn btn-primary" (click)="submitIssueVds()" [disabled]="!newVds.supplierName || !newVds.invoiceNumber || newVds.vdsAmount <= 0">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>
              সনদপত্র ইস্যু করুন (Confirm & Issue)
            </button>
          </div>
        </div>
      </div>

      <!-- Modal 2: Link Treasury e-Challan Deposit (VDS) -->
      <div class="modal-backdrop" *ngIf="isDepositModalOpen && targetDepositCert">
        <div class="modal-card" style="width: 580px; max-width: 95vw;">
          <div class="modal-header">
            <div>
              <h3 class="font-bengali" style="margin: 0; font-size: 1.15rem; font-weight: 800;">ট্রেজারি ই-চালান জমা বিবরণী লিংক</h3>
              <p class="modal-subtitle font-bengali">সরকারি কোষাগারে কর্তিত মূসক জমা প্রদান তথ্য সংযোজন</p>
            </div>
            <button class="btn-close" (click)="isDepositModalOpen = false">✕</button>
          </div>

          <div class="modal-body">
            <div class="deposit-cert-summary">
              <div>
                <span class="text-dim font-bengali">সনদপত্র নং:</span>
                <strong class="font-mono text-emerald ml-1">{{ targetDepositCert.certificateNumber }}</strong>
              </div>
              <div>
                <span class="text-dim font-bengali">সরবরাহকারী:</span>
                <strong class="ml-1">{{ targetDepositCert.supplierName }}</strong>
              </div>
              <div>
                <span class="text-dim font-bengali">কর্তিত উৎসে মূসক:</span>
                <strong class="font-mono text-amber ml-1">{{ targetDepositCert.vdsAmount | bdtCurrency }}</strong>
              </div>
            </div>

            <div class="form-grid-1" style="display: flex; flex-direction: column; gap: 14px; margin-top: 14px;">
              <div class="form-group">
                <label class="form-label">ট্রেজারি ই-চালান নম্বর (e-Challan No) *</label>
                <input type="text" class="form-control font-mono" [(ngModel)]="depositForm.treasuryChallanNo" placeholder="e.g. TR-2026-10-Sonali-00512" required>
              </div>

              <div class="form-group">
                <label class="form-label">কোষাগারে জমার তারিখ (Deposit Date) *</label>
                <input type="date" class="form-control font-mono" [(ngModel)]="depositForm.treasuryChallanDate" required>
              </div>

              <div class="form-group">
                <label class="form-label">ব্যাংক ও শাখা (Bank & Branch Name) *</label>
                <input type="text" class="form-control" [(ngModel)]="depositForm.bankBranch" placeholder="Sonali Bank Ltd, Custom House Corporate Branch" required>
              </div>

              <div class="form-group">
                <label class="form-label font-bengali">অর্থনৈতিক কোড (Economic Code - Statutory):</label>
                <input type="text" class="form-control font-mono" value="১/১১৩৩/০০১০/০৩১১ (VAT Withholding at Source)" disabled>
              </div>
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" (click)="isDepositModalOpen = false">বাতিল (Cancel)</button>
            <button class="btn btn-primary" (click)="submitDeposit()" [disabled]="!depositForm.treasuryChallanNo || !depositForm.treasuryChallanDate">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              ট্রেজারি চালান লিংক করুন (Confirm Deposit)
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .vds-tab-container { display: flex; flex-direction: column; gap: 16px; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; }
    .metric-card { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 18px; display: flex; flex-direction: column; gap: 4px; }
    .action-card { justify-content: center; align-items: flex-start; }
    .metric-title { font-size: 0.82rem; font-weight: 700; color: var(--text-muted); }
    .metric-value { font-size: 1.35rem; font-weight: 800; }
    .table-header-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
    .highlight-row { background: rgba(16, 185, 129, 0.08) !important; outline: 1px solid var(--vat-emerald); }
    .btn-group { display: inline-flex; gap: 8px; flex-wrap: wrap; }
    .btn-xs { padding: 4px 8px; font-size: 0.72rem; border-radius: var(--radius-sm); }
    .badge { padding: 3px 8px; border-radius: var(--radius-sm); font-size: 0.75rem; font-weight: 700; }
    .badge-success { background: rgba(16, 185, 129, 0.15); color: #10b981; border: 1px solid rgba(16, 185, 129, 0.3); }
    .badge-warning { background: rgba(245, 158, 11, 0.15); color: #f59e0b; border: 1px solid rgba(245, 158, 11, 0.3); }
    .badge-info { background: rgba(14, 165, 233, 0.15); color: #0ea5e9; border: 1px solid rgba(14, 165, 233, 0.3); }
    .total-row { background: rgba(15, 23, 42, 0.6); }

    .statutory-paper { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-lg); padding: 30px; }
    .govt-heading { text-align: center; margin-bottom: 24px; display: flex; flex-direction: column; align-items: center; gap: 3px; }
    .form-badge { border: 2px solid var(--vat-emerald); color: var(--vat-emerald); padding: 3px 14px; font-weight: 800; font-size: 1rem; border-radius: var(--radius-sm); margin: 6px 0; }
    .register-title { font-size: 1.4rem; font-weight: 800; }
    .rule-ref { font-size: 0.75rem; color: var(--text-dim); }
    .taxpayer-meta-box { border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 16px; margin-bottom: 20px; display: grid; grid-template-columns: 1fr 1fr; gap: 10px; background: var(--bg-card-subtle); font-size: 0.85rem; }
    .meta-row { display: flex; gap: 8px; }
    .meta-label { color: var(--text-muted); font-weight: 600; }
    .part-section { margin-top: 14px; }
    .part-header { font-size: 0.95rem; font-weight: 800; color: var(--vat-teal); margin-bottom: 8px; padding-bottom: 4px; border-bottom: 1px solid var(--border-card); }
    .mushak-grid th { text-align: center; font-size: 0.75rem; padding: 8px 6px; }

    .modal-backdrop { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.75); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 1000; padding: 20px; }
    .modal-card { background: var(--bg-card); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); box-shadow: var(--shadow-xl); display: flex; flex-direction: column; overflow: hidden; }
    .modal-lg { width: 850px; max-width: 95vw; max-height: 90vh; }
    .modal-header { display: flex; justify-content: space-between; align-items: flex-start; padding: 18px 24px; border-bottom: 1px solid var(--border-card); background: var(--bg-card-subtle); }
    .modal-badge-row { display: flex; align-items: center; gap: 10px; }
    .modal-subtitle { font-size: 0.8rem; color: var(--text-muted); margin: 0; }
    .modal-body { padding: 24px; overflow-y: auto; display: flex; flex-direction: column; gap: 16px; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; padding: 16px 24px; border-top: 1px solid var(--border-card); background: var(--bg-card-subtle); }
    .btn-close { background: transparent; border: none; font-size: 1.2rem; color: var(--text-muted); cursor: pointer; }
    .btn-close:hover { color: #ffffff; }
    .form-grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-label { font-size: 0.8rem; font-weight: 600; color: var(--text-muted); }
    .form-control { background: var(--bg-card-subtle); border: 1px solid var(--border-card); color: var(--text-main); border-radius: var(--radius-sm); padding: 8px 12px; font-size: 0.85rem; color-scheme: dark; }
    .form-select { background-color: var(--bg-card-subtle, #162032); border: 1px solid var(--border-card, #293548); color: var(--text-main, #f8fafc); border-radius: var(--radius-sm); padding: 8px 36px 8px 12px; font-size: 0.85rem; appearance: none; -webkit-appearance: none; background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%2334d399' stroke-width='2.2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpolyline points='6 9 12 15 18 9'%3E%3C/polyline%3E%3C/svg%3E"); background-repeat: no-repeat; background-position: calc(100% - 10px) center; background-size: 14px 14px; cursor: pointer; color-scheme: dark; }
    .form-select option { background-color: #1e293b; color: #f8fafc; padding: 8px 12px; }
    .vds-info-notice { background: rgba(16, 185, 129, 0.08); border-left: 3px solid var(--vat-emerald); padding: 10px 14px; border-radius: var(--radius-sm); font-size: 0.82rem; color: var(--text-muted); }
    .deposit-cert-summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 10px; background: var(--bg-card-subtle); padding: 12px; border-radius: var(--radius-md); border: 1px solid var(--border-card); font-size: 0.85rem; }
    .ml-1 { margin-left: 6px; }
  `]
})
export class VdsCertificatesComponent implements OnInit {
  private complianceApi = inject(ComplianceApiService);
  private purchaseApi = inject(PurchaseApiService);
  private pdfExport = inject(PdfExportService);
  private csvExport = inject(CsvExportService);
  private toast = inject(ToastService);
  public context = inject(CompanyContextService);

  public vdsCertificates: VdsCertificate[] = [];
  public selectedVdsCertificate: VdsCertificate | null = null;
  public livePurchases: PurchaseInvoice[] = [];

  public isIssueVdsModalOpen = false;
  public isDepositModalOpen = false;
  public targetDepositCert: VdsCertificate | null = null;

  public newVds = {
    supplierName: '',
    supplierBin: '',
    supplierAddress: '',
    invoiceNumber: '',
    invoiceDate: '2026-10-20',
    totalAmount: 0,
    vatAmount: 0,
    vdsRate: 50,
    vdsAmount: 0
  };

  public depositForm = {
    treasuryChallanNo: '',
    treasuryChallanDate: '2026-10-24',
    bankBranch: 'Sonali Bank Ltd, Custom House Corporate Branch'
  };

  ngOnInit(): void {
    this.loadVdsCertificates();
    this.loadPurchases();
  }

  public loadVdsCertificates(): void {
    this.complianceApi.getVdsCertificates().subscribe(certs => {
      this.vdsCertificates = certs || [];
      if (this.vdsCertificates.length > 0) {
        if (!this.selectedVdsCertificate || !this.vdsCertificates.find(c => c.id === this.selectedVdsCertificate?.id)) {
          this.selectedVdsCertificate = this.vdsCertificates[0];
        }
      }
    });
  }

  public loadPurchases(): void {
    this.purchaseApi.getInvoices().subscribe(purchases => {
      this.livePurchases = purchases;
    });
  }

  public selectVdsCertificate(cert: VdsCertificate): void {
    this.selectedVdsCertificate = cert;
  }

  get totalVdsWithheldAmount(): number {
    return this.vdsCertificates.reduce((acc, c) => acc + (c.vdsAmount || 0), 0);
  }

  get totalVdsDepositedAmount(): number {
    return this.vdsCertificates
      .filter(c => c.status === 'Deposited')
      .reduce((acc, c) => acc + (c.vdsAmount || 0), 0);
  }

  public openIssueVdsModal(): void {
    if (this.livePurchases.length > 0) {
      const p = this.livePurchases[0];
      this.newVds = {
        supplierName: p.supplierName,
        supplierBin: p.supplierBin,
        supplierAddress: 'হেমায়েতপুর, সাভার, ঢাকা',
        invoiceNumber: p.supplierInvoiceNumber || p.invoiceNumber,
        invoiceDate: p.invoiceDate,
        totalAmount: p.grandTotal,
        vatAmount: p.totalVat,
        vdsRate: 50,
        vdsAmount: Math.round((p.totalVat * 0.5) * 100) / 100
      };
    } else {
      this.newVds = {
        supplierName: '',
        supplierBin: '',
        supplierAddress: '',
        invoiceNumber: '',
        invoiceDate: '2026-10-20',
        totalAmount: 0,
        vatAmount: 0,
        vdsRate: 50,
        vdsAmount: 0
      };
    }
    this.isIssueVdsModalOpen = true;
  }

  public onSelectPurchaseForVds(purchaseId: string): void {
    const p = this.livePurchases.find(item => item.id === purchaseId);
    if (!p) return;
    this.newVds.supplierName = p.supplierName;
    this.newVds.supplierBin = p.supplierBin;
    this.newVds.supplierAddress = 'হেমায়েতপুর, সাভার, ঢাকা';
    this.newVds.invoiceNumber = p.supplierInvoiceNumber || p.invoiceNumber;
    this.newVds.invoiceDate = p.invoiceDate;
    this.newVds.totalAmount = p.grandTotal;
    this.newVds.vatAmount = p.totalVat;
    this.calculateVdsAmount();
  }

  public calculateVdsAmount(): void {
    this.newVds.vdsAmount = Math.round((this.newVds.vatAmount * (this.newVds.vdsRate / 100)) * 100) / 100;
  }

  public submitIssueVds(): void {
    if (!this.newVds.supplierName || !this.newVds.invoiceNumber || this.newVds.vdsAmount <= 0) {
      this.toast.error('সকল প্রয়োজনীয় তথ্য এবং উৎসে কর্তনের পরিমাণ সঠিকভাবে প্রদান করুন।', 'তথ্য অসম্পূর্ণ');
      return;
    }

    const payload = {
      supplierName: this.newVds.supplierName,
      supplierBin: this.newVds.supplierBin,
      supplierAddress: this.newVds.supplierAddress,
      invoiceNumber: this.newVds.invoiceNumber,
      invoiceDate: this.newVds.invoiceDate,
      totalAmount: this.newVds.totalAmount,
      vatAmount: this.newVds.vatAmount,
      vdsRate: this.newVds.vdsRate,
      vdsAmount: this.newVds.vdsAmount
    };

    this.complianceApi.issueVdsCertificate(payload).subscribe({
      next: (created) => {
        this.toast.success(
          `মূসক ৬.৬ সনদপত্র (${created.certificateNumber || 'সফল'}) ইস্যু করা হয়েছে!`,
          'সনদপত্র তৈরি সম্পন্ন (Mushak 6.6 Issued)'
        );
        this.isIssueVdsModalOpen = false;
        this.loadVdsCertificates();
      },
      error: () => {
        this.toast.error('মূসক ৬.৬ সনদপত্র তৈরিতে ত্রুটি হয়েছে। পুনরায় চেষ্টা করুন।', 'ত্রুটি');
      }
    });
  }

  public openDepositModal(cert: VdsCertificate): void {
    this.targetDepositCert = cert;
    this.depositForm = {
      treasuryChallanNo: 'TR-2026-10-Sonali-00' + Math.floor(500 + Math.random() * 400),
      treasuryChallanDate: '2026-10-24',
      bankBranch: 'Sonali Bank Ltd, Custom House Corporate Branch'
    };
    this.isDepositModalOpen = true;
  }

  public submitDeposit(): void {
    if (!this.targetDepositCert || !this.depositForm.treasuryChallanNo) {
      this.toast.error('ট্রেজারি ই-চালান নম্বর প্রদান করুন।', 'তথ্য অসম্পূর্ণ');
      return;
    }

    this.complianceApi.recordTreasuryDeposit(
      this.targetDepositCert.id,
      {
        treasuryChallanNo: this.depositForm.treasuryChallanNo,
        treasuryChallanDate: this.depositForm.treasuryChallanDate,
        bankBranch: this.depositForm.bankBranch
      }
    ).subscribe({
      next: () => {
        this.toast.success(
          `ট্রেজারি ই-চালান (${this.depositForm.treasuryChallanNo}) সফলভাবে লিংক করা হয়েছে!`,
          'ট্রেজারি জমা সম্পন্ন (Treasury Deposit Linked)'
        );
        this.isDepositModalOpen = false;
        this.loadVdsCertificates();
      },
      error: () => {
        this.toast.error('ট্রেজারি চালান লিংক করতে ত্রুটি হয়েছে।', 'ত্রুটি');
      }
    });
  }

  public downloadPdf(): void {
    const num = this.selectedVdsCertificate?.certificateNumber?.replace(/[\/\\:]/g, '_') || 'VDS-6-6-2026-10';
    this.pdfExport.downloadElementAsPdf('vds-66-statutory-preview', `Mushak_6_6_VDS_Certificate_${num}`);
  }

  public printDocument(): void {
    this.pdfExport.printClean();
  }

  public exportToCsv(): void {
    const compId = this.context.activeCompany().id;
    const period = this.context.activeFiscalPeriod().periodCode;
    const headers = [
      'Certificate Number',
      'Issue Date',
      'Supplier Name',
      'Supplier BIN',
      'Supplier Address',
      'Invoice Number',
      'Invoice Date',
      'Total Amount (BDT)',
      'Invoice VAT (BDT)',
      'VDS Rate (%)',
      'VDS Withheld Amount (BDT)',
      'Treasury e-Challan No',
      'Deposit Date',
      'Bank & Branch',
      'Status'
    ];

    const rows = this.vdsCertificates.map(c => [
      c.certificateNumber,
      c.issueDate,
      c.supplierName,
      c.supplierBin,
      c.supplierAddress || '',
      c.invoiceNumber,
      c.invoiceDate,
      c.totalAmount,
      c.vatAmount,
      c.vdsRate,
      c.vdsAmount,
      c.treasuryChallanNo || 'Pending Deposit',
      c.treasuryChallanDate || '',
      c.bankBranch || '',
      c.status
    ]);

    this.csvExport.exportToCsv(`Mushak_6_6_VDS_Register_${compId}_${period}`, headers, rows);
  }
}
