import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { ComplianceApiService } from '../../../../core/services/api/compliance-api.service';
import { SalesApiService } from '../../../../core/services/api/sales-api.service';
import { PdfExportService } from '../../../../core/services/pdf-export.service';
import { CsvExportService } from '../../../../core/services/csv-export.service';
import { ToastService } from '../../../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { VatLedgerEntry, VatLedgerSummary, CreditNote, DebitNote, SalesInvoice } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-tax-ledger',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe],
  template: `
    <div class="tax-ledger-container">
      <!-- Top KPI Metrics for Current VAT Account -->
      <div class="metrics-grid no-print mb-4">
        <div class="metric-card">
          <span class="metric-title font-bengali">প্রারম্ভিক জের (Opening B/F)</span>
          <span class="metric-value font-mono text-teal">{{ (ledgerSummary?.openingBalance || 1000000) | bdtCurrency }}</span>
          <small class="text-dim">অক্টোবর ২০২৬ শুরুর ব্যালেন্স</small>
        </div>
        <div class="metric-card">
          <span class="metric-title font-bengali">মোট কোষাগারে জমা (Treasury Deposits)</span>
          <span class="metric-value font-mono text-emerald">{{ (ledgerSummary?.totalTreasuryDeposited || 0) | bdtCurrency }}</span>
          <small class="text-dim">সোনালী ব্যাংক চালান কোড: ১/১১৩৩/০০১০/০৩১১</small>
        </div>
        <div class="metric-card">
          <span class="metric-title font-bengali">মোট উপকরণ কর রেয়াত (Input Credits)</span>
          <span class="metric-value font-mono text-emerald">{{ (ledgerSummary?.totalInputCreditClaimed || 0) | bdtCurrency }}</span>
          <small class="text-dim">অনুমোদিত ক্রয় চালান হতে যুক্ত</small>
        </div>
        <div class="metric-card">
          <span class="metric-title font-bengali">মোট প্রদেয় উৎপাদ কর (Output Tax Debited)</span>
          <span class="metric-value font-mono text-rose">{{ (ledgerSummary?.totalOutputTaxDebited || 0) | bdtCurrency }}</span>
          <small class="text-dim">বিক্রয় সরবরাহ চালান হতে ডেবিট</small>
        </div>
        <div class="metric-card" [ngClass]="(ledgerSummary?.closingBalance || 0) >= 0 ? 'card-positive' : 'card-negative'">
          <span class="metric-title font-bengali">চলতি সমাপনী স্থিতি (Available Balance)</span>
          <span class="metric-value font-mono font-bold" [ngClass]="(ledgerSummary?.closingBalance || 0) >= 0 ? 'text-emerald' : 'text-rose'">
            {{ (ledgerSummary?.closingBalance || 0) | bdtCurrency }}
          </span>
          <small class="text-dim">{{ (ledgerSummary?.closingBalance || 0) >= 0 ? '✅ পর্যাপ্ত ক্রেডিট স্থিতি বিদ্যমান' : '⚠️ ঋণাত্মক ব্যালেন্স - অবিলম্বে ট্রেজারি চালান জমা দিন' }}</small>
        </div>
      </div>

      <!-- Section Switcher Bar -->
      <div class="section-switch-row no-print mb-4">
        <div class="tab-pill-box">
          <button class="tab-btn" [class.active-tab]="ledgerView === 'mushak18'" (click)="ledgerView = 'mushak18'">
            📋 মূসক-১৮ চলতি হিসাব খতিয়ান (Current Account)
          </button>
          <button class="tab-btn" [class.active-tab]="ledgerView === 'adjustments'" (click)="ledgerView = 'adjustments'">
            📑 মূসক ৬.৭ ও ৬.৮ সমন্বয় নোট (Credit & Debit Notes)
          </button>
        </div>

        <div class="btn-group">
          <button class="btn btn-secondary btn-sm" (click)="exportToCsv()">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Export CSV
          </button>
          <button class="btn btn-secondary btn-sm" (click)="printDocument()">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="6 9 6 2 18 2 18 9"></polyline>
              <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path>
              <rect x="6" y="14" width="12" height="8"></rect>
            </svg>
            Print
          </button>
          <button class="btn btn-primary btn-sm" (click)="openLedgerDepositModal()">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 5v14M5 12h14"></path>
            </svg>
            + Record Treasury e-Challan (চালান জমা)
          </button>
          <button class="btn btn-secondary btn-sm" (click)="openCreditNoteModal()">
            + মূসক ৬.৭ ক্রেডিট নোট (হ্রাস)
          </button>
          <button class="btn btn-secondary btn-sm" (click)="openDebitNoteModal()">
            + মূসক ৬.৮ ডেবিট নোট (বৃদ্ধি)
          </button>
        </div>
      </div>

      <!-- SUB-VIEW 1: MUSHAK 18 CURRENT ACCOUNT REGISTER -->
      <div *ngIf="ledgerView === 'mushak18'" class="statutory-paper" id="mushak-18-doc">
        <div class="govt-heading">
          <h3 class="font-bengali">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4 class="font-bengali">জাতীয় রাজস্ব বোর্ড</h4>
          <div class="form-badge font-bengali">মূসক-১৮</div>
          <h2 class="register-title font-bengali">মূল্য সংযোজন কর চলতি হিসাব পুস্তক</h2>
          <p class="rule-ref font-bengali">[বিধি ৪৫ দ্রষ্টব্য] — অর্থনৈতিক কোড: ১/১১৩৩/০০১০/০৩১১ — কর মেয়াদ: অক্টোবর ২০২৬</p>
        </div>

        <div class="taxpayer-meta-box">
          <div class="meta-row">
            <span class="meta-label">করদাতার নাম:</span>
            <strong class="font-bengali">{{ context.companyName() }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">বিআইএন (BIN):</span>
            <strong class="font-mono text-emerald">{{ context.activeCompany().bin }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">ঠিকানা:</span>
            <span>{{ context.activeCompany().addressBn || context.activeCompany().address }}</span>
          </div>
          <div class="meta-row">
            <span class="meta-label">শাখা / ডিপো:</span>
            <strong class="text-emerald font-bengali">{{ context.activeBranch().name }}</strong>
          </div>
        </div>

        <div class="vat-table-container">
          <table class="vat-table mushak-grid">
            <thead>
              <tr>
                <th style="width: 45px;" class="text-center font-bengali">ক্রমিক</th>
                <th>তারিখ</th>
                <th>চালান / সূত্র নং</th>
                <th>লেনদেনের ধরন</th>
                <th>বিবরণ ও সংবিধিবদ্ধ ফরম</th>
                <th class="text-right">ডেবিট / উৎপাদ কর (টাকা)</th>
                <th class="text-right">ক্রেডিট / জমা / রেয়াত (টাকা)</th>
                <th class="text-right">চলতি জের (টাকা)</th>
                <th>ব্যাংক ও শাখা</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of ledgerEntries; let i = index">
                <td class="text-center font-bengali">{{ i + 1 }}</td>
                <td class="font-mono">{{ row.entryDate }}</td>
                <td><strong class="font-mono text-emerald">{{ row.referenceNumber }}</strong></td>
                <td>
                  <span class="badge" [ngClass]="(row.debitAmount > 0 || row.transactionType === 'SalesOutputVat' || row.transactionType === 'DebitNote') ? 'badge-rose' : 'badge-success'">
                    {{ (row.debitAmount > 0 || row.transactionType === 'SalesOutputVat' || row.transactionType === 'DebitNote') ? 'ডেবিট (প্রদেয়)' : 'ক্রেডিট (জমা/রেয়াত)' }}
                  </span>
                </td>
                <td>
                  <strong>{{ row.description }}</strong>
                  <br><small class="text-dim font-mono">{{ row.statutoryForm }}</small>
                </td>
                <td class="text-right numeric text-rose">
                  {{ row.debitAmount ? (row.debitAmount | bdtCurrency) : '—' }}
                </td>
                <td class="text-right numeric text-emerald font-bold">
                  {{ row.creditAmount ? (row.creditAmount | bdtCurrency) : '—' }}
                </td>
                <td class="text-right numeric font-mono font-bold" [ngClass]="row.balance >= 0 ? 'text-emerald' : 'text-rose'">
                  {{ row.balance | bdtCurrency }}
                </td>
                <td>
                  <small class="text-dim">{{ row.bankBranch || '—' }}</small>
                </td>
              </tr>
              <tr *ngIf="ledgerEntries.length === 0">
                <td colspan="9" class="text-center py-4 text-dim">কোন চলতি হিসাব এন্ট্রি পাওয়া যায়নি।</td>
              </tr>
            </tbody>
            <tfoot *ngIf="ledgerEntries.length > 0">
              <tr class="total-row">
                <th colspan="5" class="text-right font-bengali">সর্বমোট লেনদেন ও সমাপনী চলতি জের:</th>
                <th class="text-right numeric text-rose font-bold">{{ (ledgerSummary?.totalOutputTaxDebited || 0) | bdtCurrency:false }}</th>
                <th class="text-right numeric text-emerald font-bold">{{ ((ledgerSummary?.totalTreasuryDeposited || 0) + (ledgerSummary?.totalInputCreditClaimed || 0) + (ledgerSummary?.openingBalance || 1000000)) | bdtCurrency:false }}</th>
                <th class="text-right numeric font-mono font-bold text-xl" [ngClass]="(ledgerSummary?.closingBalance || 0) >= 0 ? 'text-emerald' : 'text-rose'">
                  {{ (ledgerSummary?.closingBalance || 0) | bdtCurrency }}
                </th>
                <th></th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- Signature block -->
        <div style="margin-top: 40px; display: flex; justify-content: space-between; align-items: flex-end;">
          <div class="font-bengali" style="font-size: 0.8rem; color: var(--text-muted); max-width: 480px;">
            "বিধি ৪৫ অনুযায়ী এই চলতি হিসাব পুস্তক প্রতিদিনের করযোগ্য সরবরাহ, উপকরণ রেয়াত এবং ট্রেজারি চালানের তথ্যের ভিত্তিতে নিয়মিতভাবে হালনাগাদ ও রক্ষণাবেক্ষণ করা হইতেছে।"
          </div>
          <div style="display: flex; flex-direction: column; align-items: center; gap: 6px;">
            <div style="width: 220px; border-top: 1px dashed var(--border-subtle);"></div>
            <span class="font-bengali font-bold" style="font-size: 0.85rem;">হিসাব রক্ষকের স্বাক্ষর ও সীল</span>
            <small class="text-dim">ডেপুটি ম্যানেজার (ট্যাক্স অ্যান্ড কমার্শিয়াল)</small>
          </div>
        </div>
      </div>

      <!-- SUB-VIEW 2: CREDIT & DEBIT NOTES (MUSHAK 6.7 & 6.8) -->
      <div *ngIf="ledgerView === 'adjustments'" class="vat-card">
        <!-- Credit Notes Section -->
        <div class="mb-4">
          <div class="table-header-row">
            <div>
              <h3 class="form-title font-bengali" style="margin-bottom: 2px;">
                মূসক-৬.৭ ক্রেডিট নোট রেজিস্টার (Credit Notes - Decreasing Adjustments)
              </h3>
              <p class="rule-ref font-bengali">[ধারা ৫০ ও বিধি ৪০(১)(চ) দ্রষ্টব্য] — সরবরাহকৃত পণ্য ফেরত বা মূল্য হ্রাসের প্রেক্ষিতে হ্রাসকারী সমন্বয় (রিটার্ন অংশ-৬ লাইন ৩৬)</p>
            </div>
            <button class="btn btn-primary btn-sm" (click)="openCreditNoteModal()">
              + Issue Mushak 6.7
            </button>
          </div>

          <div class="vat-table-container">
            <table class="vat-table">
              <thead>
                <tr>
                  <th style="width: 45px;" class="text-center">ক্র.</th>
                  <th>ক্রেডিট নোট নম্বর</th>
                  <th>মূল চালানপত্র ও তারিখ</th>
                  <th>সংশোধন তারিখ</th>
                  <th>কারণ ও বিবরণ</th>
                  <th class="text-right">মূল্য হ্রাস (টাকা)</th>
                  <th class="text-right">হ্রাসকারী মূসক সমন্বয় (টাকা)</th>
                  <th class="text-center">অবস্থা</th>
                  <th>অনুমোদনকারী</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let cn of creditNotes; let i = index">
                  <td class="text-center font-bengali">{{ i + 1 }}</td>
                  <td><strong class="font-mono text-emerald">{{ cn.documentNumber }}</strong></td>
                  <td><span class="font-mono">{{ cn.originalDocumentNumber }}</span></td>
                  <td>{{ cn.correctionDate }}</td>
                  <td>{{ cn.reason }}</td>
                  <td class="text-right numeric font-bold">{{ cn.netAmount | bdtCurrency }}</td>
                  <td class="text-right numeric font-bold text-emerald">{{ cn.outputTaxAmount | bdtCurrency }}</td>
                  <td class="text-center">
                    <span class="badge badge-success">{{ cn.status }}</span>
                  </td>
                  <td><small class="text-dim">{{ cn.approvedBy || 'TaxManager' }}</small></td>
                </tr>
                <tr *ngIf="creditNotes.length === 0">
                  <td colspan="9" class="text-center py-4 text-dim">কোন ক্রেডিট নোট ইস্যু করা হয়নি।</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- Debit Notes Section -->
        <div class="mt-4">
          <div class="table-header-row">
            <div>
              <h3 class="form-title font-bengali" style="margin-bottom: 2px;">
                মূসক-৬.৮ ডেবিট নোট রেজিস্টার (Debit Notes - Increasing Adjustments)
              </h3>
              <p class="rule-ref font-bengali">[ধারা ৫০ ও বিধি ৪০(১)(ছ) দ্রষ্টব্য] — মূল্য বৃদ্ধি বা দূরত্বজনিত অতিরিক্ত মূসকের বৃদ্ধিকারী সমন্বয় (রিটার্ন অংশ-৫ লাইন ৩০)</p>
            </div>
            <button class="btn btn-primary btn-sm" (click)="openDebitNoteModal()">
              + Issue Mushak 6.8
            </button>
          </div>

          <div class="vat-table-container">
            <table class="vat-table">
              <thead>
                <tr>
                  <th style="width: 45px;" class="text-center">ক্র.</th>
                  <th>ডেবিট নোট নম্বর</th>
                  <th>মূল চালানপত্র ও তারিখ</th>
                  <th>সংশোধন তারিখ</th>
                  <th>কারণ ও বিবরণ</th>
                  <th class="text-right">মূল্য বৃদ্ধি (টাকা)</th>
                  <th class="text-right">বৃদ্ধিকারী মূসক সমন্বয় (টাকা)</th>
                  <th class="text-center">অবস্থা</th>
                  <th>অনুমোদনকারী</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let dn of debitNotes; let i = index">
                  <td class="text-center font-bengali">{{ i + 1 }}</td>
                  <td><strong class="font-mono text-amber">{{ dn.documentNumber }}</strong></td>
                  <td><span class="font-mono">{{ dn.originalDocumentNumber }}</span></td>
                  <td>{{ dn.correctionDate }}</td>
                  <td>{{ dn.reason }}</td>
                  <td class="text-right numeric font-bold">{{ dn.netAmount | bdtCurrency }}</td>
                  <td class="text-right numeric font-bold text-amber">{{ dn.outputTaxAmount | bdtCurrency }}</td>
                  <td class="text-center">
                    <span class="badge badge-warning">{{ dn.status }}</span>
                  </td>
                  <td><small class="text-dim">{{ dn.approvedBy || 'TaxManager' }}</small></td>
                </tr>
                <tr *ngIf="debitNotes.length === 0">
                  <td colspan="9" class="text-center py-4 text-dim">কোন ডেবিট নোট ইস্যু করা হয়নি।</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Modal 1: Record Treasury Deposit in Current VAT Account (Mushak 18) -->
      <div class="modal-backdrop" *ngIf="isLedgerDepositModalOpen">
        <div class="modal-card" style="width: 580px; max-width: 95vw;">
          <div class="modal-header">
            <div>
              <div class="modal-badge-row">
                <span class="form-badge font-bengali" style="margin: 0; padding: 2px 10px; font-size: 0.85rem;">মূসক-১৮</span>
                <h3 class="font-bengali" style="margin: 0; font-size: 1.15rem; font-weight: 800;">চলতি হিসাবে ট্রেজারি চালান জমা প্রদান</h3>
              </div>
              <p class="modal-subtitle font-bengali">[বিধি ৪৫ দ্রষ্টব্য] — সরকারি কোষাগারে মূসক জমা দিয়ে চলতি হিসাবের জের বৃদ্ধি</p>
            </div>
            <button class="btn-close" (click)="isLedgerDepositModalOpen = false">✕</button>
          </div>

          <div class="modal-body">
            <div class="form-grid-1" style="display: flex; flex-direction: column; gap: 14px;">
              <div class="form-group">
                <label class="form-label">জমাকৃত টাকার পরিমাণ (Deposit Amount BDT) *</label>
                <input type="number" class="form-control font-mono font-bold text-emerald text-lg" [(ngModel)]="ledgerDepositForm.depositAmount" placeholder="e.g. 500000.00" required>
              </div>

              <div class="form-group">
                <label class="form-label">ট্রেজারি ই-চালান নম্বর (e-Challan Reference No) *</label>
                <input type="text" class="form-control font-mono" [(ngModel)]="ledgerDepositForm.treasuryChallanNo" placeholder="e.g. TR-2026-10-Sonali-00998" required>
              </div>

              <div class="form-group">
                <label class="form-label">জমার তারিখ (Deposit Date) *</label>
                <input type="date" class="form-control font-mono" [(ngModel)]="ledgerDepositForm.depositDate" required>
              </div>

              <div class="form-group">
                <label class="form-label">ব্যাংক ও শাখা (Bank & Branch Name) *</label>
                <input type="text" class="form-control" [(ngModel)]="ledgerDepositForm.bankBranch" required>
              </div>

              <div class="form-group">
                <label class="form-label font-bengali">অর্থনৈতিক হিসাব কোড (Statutory Economic Code):</label>
                <input type="text" class="form-control font-mono" [(ngModel)]="ledgerDepositForm.economicCode" disabled>
              </div>
            </div>

            <div class="vds-info-notice font-bengali">
              ℹ️ এই ট্রেজারি চালান জমা নিশ্চিত করার সাথে সাথে চলতি হিসাবের প্রারম্ভিক স্থিতির সাথে এই পরিমাণ ক্রেডিট হিসেবে যোগ হবে এবং সমাপনী জের স্বয়ংক্রিয়ভাবে বৃদ্ধি পাবে।
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" (click)="isLedgerDepositModalOpen = false">বাতিল (Cancel)</button>
            <button class="btn btn-primary" (click)="submitLedgerDeposit()" [disabled]="ledgerDepositForm.depositAmount <= 0 || !ledgerDepositForm.treasuryChallanNo">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>
              ট্রেজারি জমা নিশ্চিত করুন (Confirm Deposit)
            </button>
          </div>
        </div>
      </div>

      <!-- Modal 2: Issue Mushak 6.7 Credit Note (হ্রাসকারী সমন্বয়) -->
      <div class="modal-backdrop" *ngIf="isCreditNoteModalOpen">
        <div class="modal-card modal-lg">
          <div class="modal-header">
            <div>
              <div class="modal-badge-row">
                <span class="form-badge font-bengali" style="margin: 0; padding: 2px 10px; font-size: 0.85rem; border-color: var(--vat-teal); color: var(--vat-teal);">মূসক-৬.৭</span>
                <h3 class="font-bengali" style="margin: 0; font-size: 1.15rem; font-weight: 800;">মূসক ৬.৭ ক্রেডিট নোট ইস্যু (হ্রাসকারী সমন্বয়)</h3>
              </div>
              <p class="modal-subtitle font-bengali">[ধারা ৫০ ও বিধি ৪০(১)(চ) দ্রষ্টব্য] — সরবরাহকৃত পণ্য ফেরত বা মূল্য হ্রাসের প্রেক্ষিতে ক্রেতাকে ক্রেডিট নোট প্রদান</p>
            </div>
            <button class="btn-close" (click)="isCreditNoteModalOpen = false">✕</button>
          </div>

          <div class="modal-body">
            <div class="form-group" *ngIf="liveSales.length > 0">
              <label class="form-label font-bengali">মূল বিক্রয় চালান হতে স্বয়ংক্রিয় পূরণ করুন (Select Sales Invoice):</label>
              <select class="form-select font-mono" (change)="onSelectSaleForCreditNote($any($event.target).value)">
                <option value="">-- বিক্রয় চালান নির্বাচন করুন (বা ম্যানুয়ালি এন্ট্রি দিন) --</option>
                <option *ngFor="let s of liveSales" [value]="s.id">
                  {{ s.customerName }} | চালান: {{ s.challanNumber63 || s.invoiceNumber }} | মূল্য: ৳{{ s.grandTotal | number:'1.2-2' }} | ভ্যাট: ৳{{ s.totalVat | number:'1.2-2' }}
                </option>
              </select>
            </div>

            <div class="form-grid-2">
              <div class="form-group">
                <label class="form-label">ক্রেতার নাম (Customer Name) *</label>
                <input type="text" class="form-control" [(ngModel)]="creditNoteForm.customerName" placeholder="e.g. ACI Logistics Ltd (Shwapno)" required>
              </div>

              <div class="form-group">
                <label class="form-label">মূল চালানপত্র নম্বর (Original Mushak 6.3) *</label>
                <input type="text" class="form-control font-mono" [(ngModel)]="creditNoteForm.originalDocumentNumber" placeholder="e.g. CH-DHK-2026-00142" required>
              </div>

              <div class="form-group" style="grid-column: 1 / -1;">
                <label class="form-label">ক্রেডিট নোট ইস্যুর কারণ (Reason for Credit Note) *</label>
                <input type="text" class="form-control" [(ngModel)]="creditNoteForm.reason" placeholder="e.g. ক্ষতিগ্রস্ত বা মেয়াদোত্তীর্ণ পণ্য ফেরত (Damaged goods return)" required>
              </div>

              <div class="form-group">
                <label class="form-label">হ্রাসকৃত পণ্যের মূল্য (Pre-VAT Amount BDT) *</label>
                <input type="number" class="form-control font-mono text-right" [(ngModel)]="creditNoteForm.netAmount" (ngModelChange)="calculateCreditNoteVat()" placeholder="0.00" required>
              </div>

              <div class="form-group">
                <label class="form-label font-bold text-emerald">হ্রাসকারী সমন্বয় মূসক (15% Output VAT Rebate BDT) *</label>
                <input type="number" class="form-control font-mono text-right font-bold text-emerald" [(ngModel)]="creditNoteForm.vatAmount" readonly>
              </div>

              <div class="form-group">
                <label class="form-label">সংশোধনের তারিখ (Correction Date) *</label>
                <input type="date" class="form-control font-mono" [(ngModel)]="creditNoteForm.correctionDate" required>
              </div>
            </div>

            <div class="vds-info-notice font-bengali">
              ℹ️ ধারা ৫০ মোতাবেক মূসক ৬.৭ ক্রেডিট নোট ইস্যু হলে প্রদেয় কর কমবে (হ্রাসকারী সমন্বয়) এবং চলতি হিসাব (মূসক ১৮)-এ এই পরিমাণ ক্রেডিট হিসেবে জমা হবে।
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" (click)="isCreditNoteModalOpen = false">বাতিল (Cancel)</button>
            <button class="btn btn-primary" (click)="submitCreditNote()" [disabled]="!creditNoteForm.customerName || !creditNoteForm.originalDocumentNumber || creditNoteForm.vatAmount <= 0">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>
              ক্রেডিট নোট ইস্যু করুন (Confirm Credit Note)
            </button>
          </div>
        </div>
      </div>

      <!-- Modal 3: Issue Mushak 6.8 Debit Note (বৃদ্ধিকারী সমন্বয়) -->
      <div class="modal-backdrop" *ngIf="isDebitNoteModalOpen">
        <div class="modal-card modal-lg">
          <div class="modal-header">
            <div>
              <div class="modal-badge-row">
                <span class="form-badge font-bengali" style="margin: 0; padding: 2px 10px; font-size: 0.85rem; border-color: var(--vat-amber); color: var(--vat-amber);">মূসক-৬.৮</span>
                <h3 class="font-bengali" style="margin: 0; font-size: 1.15rem; font-weight: 800;">মূসক ৬.৮ ডেবিট নোট ইস্যু (বৃদ্ধিকারী সমন্বয়)</h3>
              </div>
              <p class="modal-subtitle font-bengali">[ধারা ৫০ ও বিধি ৪০(১)(ছ) দ্রষ্টব্য] — সরবরাহকৃত পণ্যের মূল্য বৃদ্ধি বা দূরত্বজনিত অতিরিক্ত মূসক সমন্বয়</p>
            </div>
            <button class="btn-close" (click)="isDebitNoteModalOpen = false">✕</button>
          </div>

          <div class="modal-body">
            <div class="form-group" *ngIf="liveSales.length > 0">
              <label class="form-label font-bengali">মূল বিক্রয় চালান হতে স্বয়ংক্রিয় পূরণ করুন (Select Sales Invoice):</label>
              <select class="form-select font-mono" (change)="onSelectSaleForDebitNote($any($event.target).value)">
                <option value="">-- বিক্রয় চালান নির্বাচন করুন (বা ম্যানুয়ালি এন্ট্রি দিন) --</option>
                <option *ngFor="let s of liveSales" [value]="s.id">
                  {{ s.customerName }} | চালান: {{ s.challanNumber63 || s.invoiceNumber }} | মূল্য: ৳{{ s.grandTotal | number:'1.2-2' }} | ভ্যাট: ৳{{ s.totalVat | number:'1.2-2' }}
                </option>
              </select>
            </div>

            <div class="form-grid-2">
              <div class="form-group">
                <label class="form-label">ক্রেতার নাম (Customer Name) *</label>
                <input type="text" class="form-control" [(ngModel)]="debitNoteForm.customerName" placeholder="e.g. Pran Dairy & Consumer Ltd" required>
              </div>

              <div class="form-group">
                <label class="form-label">মূল চালানপত্র নম্বর (Original Mushak 6.3) *</label>
                <input type="text" class="form-control font-mono" [(ngModel)]="debitNoteForm.originalDocumentNumber" placeholder="e.g. CH-DHK-2026-00140" required>
              </div>

              <div class="form-group" style="grid-column: 1 / -1;">
                <label class="form-label">ডেবিট নোট ইস্যুর কারণ (Reason for Debit Note) *</label>
                <input type="text" class="form-control" [(ngModel)]="debitNoteForm.reason" placeholder="e.g. দূরবর্তী পরিবহন চার্জ ও কাঁচামাল মূল্য বৃদ্ধি (Freight & raw material surcharge)" required>
              </div>

              <div class="form-group">
                <label class="form-label">বৃদ্ধিকৃত মূল্য (Pre-VAT Amount BDT) *</label>
                <input type="number" class="form-control font-mono text-right" [(ngModel)]="debitNoteForm.netAmount" (ngModelChange)="calculateDebitNoteVat()" placeholder="0.00" required>
              </div>

              <div class="form-group">
                <label class="form-label font-bold text-amber">বৃদ্ধিকারী সমন্বয় মূসক (15% Output VAT Debit BDT) *</label>
                <input type="number" class="form-control font-mono text-right font-bold text-amber" [(ngModel)]="debitNoteForm.vatAmount" readonly>
              </div>

              <div class="form-group">
                <label class="form-label">সংশোধনের তারিখ (Correction Date) *</label>
                <input type="date" class="form-control font-mono" [(ngModel)]="debitNoteForm.correctionDate" required>
              </div>
            </div>

            <div class="vds-info-notice font-bengali">
              ℹ️ ধারা ৫০ মোতাবেক মূসক ৬.৮ ডেবিট নোট ইস্যু হলে প্রদেয় মূসক বৃদ্ধি পাবে (বৃদ্ধিকারী সমন্বয়) এবং চলতি হিসাব (মূসক ১৮)-এ এই পরিমাণ ডেবিট হিসেবে কর্তন হবে।
            </div>
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" (click)="isDebitNoteModalOpen = false">বাতিল (Cancel)</button>
            <button class="btn btn-primary" (click)="submitDebitNote()" [disabled]="!debitNoteForm.customerName || !debitNoteForm.originalDocumentNumber || debitNoteForm.vatAmount <= 0">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>
              ডেবিট নোট ইস্যু করুন (Confirm Debit Note)
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; }
    .metric-card { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 18px; display: flex; flex-direction: column; gap: 4px; }
    .card-positive { border-left: 4px solid var(--vat-emerald); }
    .card-negative { border-left: 4px solid var(--vat-rose); }
    .metric-title { font-size: 0.82rem; font-weight: 700; color: var(--text-muted); }
    .metric-value { font-size: 1.35rem; font-weight: 800; }
    .section-switch-row { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 12px; }
    .tab-pill-box { display: flex; gap: 4px; background: var(--bg-card); padding: 4px; border-radius: var(--radius-md); border: 1px solid var(--border-card); }
    .tab-btn { background: transparent; border: none; color: var(--text-muted); padding: 6px 14px; border-radius: var(--radius-sm); font-size: 0.78rem; font-weight: 700; cursor: pointer; transition: all 0.2s ease; }
    .active-tab { background: var(--vat-emerald); color: #ffffff; }
    .btn-group { display: inline-flex; gap: 8px; flex-wrap: wrap; }
    .statutory-paper { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-lg); padding: 30px; }
    .govt-heading { text-align: center; margin-bottom: 24px; display: flex; flex-direction: column; align-items: center; gap: 3px; }
    .form-badge { border: 2px solid var(--vat-emerald); color: var(--vat-emerald); padding: 3px 14px; font-weight: 800; font-size: 1rem; border-radius: var(--radius-sm); margin: 6px 0; }
    .register-title { font-size: 1.4rem; font-weight: 800; }
    .rule-ref { font-size: 0.75rem; color: var(--text-dim); }
    .taxpayer-meta-box { border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 16px; margin-bottom: 20px; display: grid; grid-template-columns: 1fr 1fr; gap: 10px; background: var(--bg-card-subtle); font-size: 0.85rem; }
    .meta-row { display: flex; gap: 8px; }
    .meta-label { color: var(--text-muted); font-weight: 600; }
    .table-header-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
    .badge { padding: 3px 8px; border-radius: var(--radius-sm); font-size: 0.75rem; font-weight: 700; }
    .badge-success { background: rgba(16, 185, 129, 0.15); color: #10b981; border: 1px solid rgba(16, 185, 129, 0.3); }
    .badge-warning { background: rgba(245, 158, 11, 0.15); color: #f59e0b; border: 1px solid rgba(245, 158, 11, 0.3); }
    .badge-rose { background: rgba(244, 63, 94, 0.15); color: #f43f5e; border: 1px solid rgba(244, 63, 94, 0.3); }
    .total-row { background: rgba(15, 23, 42, 0.6); }

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
    .form-control, .form-select { background: var(--bg-card-subtle); border: 1px solid var(--border-card); color: var(--text-main); border-radius: var(--radius-sm); padding: 8px 12px; font-size: 0.85rem; }
    .vds-info-notice { background: rgba(16, 185, 129, 0.08); border-left: 3px solid var(--vat-emerald); padding: 10px 14px; border-radius: var(--radius-sm); font-size: 0.82rem; color: var(--text-muted); }
  `]
})
export class TaxLedgerComponent implements OnInit {
  private complianceApi = inject(ComplianceApiService);
  private salesApi = inject(SalesApiService);
  private pdfExport = inject(PdfExportService);
  private csvExport = inject(CsvExportService);
  private toast = inject(ToastService);
  public context = inject(CompanyContextService);

  public ledgerEntries: VatLedgerEntry[] = [];
  public ledgerSummary: VatLedgerSummary | null = null;
  public ledgerView: 'mushak18' | 'adjustments' = 'mushak18';
  public creditNotes: CreditNote[] = [];
  public debitNotes: DebitNote[] = [];
  public liveSales: SalesInvoice[] = [];

  public isLedgerDepositModalOpen = false;
  public isCreditNoteModalOpen = false;
  public isDebitNoteModalOpen = false;

  public ledgerDepositForm = {
    depositAmount: 500000,
    treasuryChallanNo: '',
    depositDate: '2026-10-24',
    bankBranch: 'Sonali Bank Ltd, Custom House Corporate Branch',
    economicCode: '1/1133/0010/0311'
  };

  public creditNoteForm = {
    originalDocumentNumber: '',
    customerName: '',
    reason: 'ডেলিভারিতে ক্ষতিগ্রস্ত পণ্য ফেরত (Damaged Goods Return)',
    netAmount: 0,
    vatAmount: 0,
    correctionDate: '2026-10-24'
  };

  public debitNoteForm = {
    originalDocumentNumber: '',
    customerName: '',
    reason: 'দূরবর্তী পরিবহন চার্জ ও কাঁচামাল মূল্য বৃদ্ধি (Fuel & Distance Surcharge)',
    netAmount: 0,
    vatAmount: 0,
    correctionDate: '2026-10-24'
  };

  ngOnInit(): void {
    this.loadVatLedger();
    this.loadAdjustments();
    this.loadSales();
  }

  public loadVatLedger(): void {
    this.complianceApi.getVatLedger('2026-10').subscribe(res => {
      if (res) {
        this.ledgerEntries = res.entries || [];
        this.ledgerSummary = res.summary || null;
      }
    });
  }

  public loadAdjustments(): void {
    this.complianceApi.getAdjustments('2026-10').subscribe(res => {
      if (res) {
        this.creditNotes = res.creditNotes || [];
        this.debitNotes = res.debitNotes || [];
      }
    });
  }

  public loadSales(): void {
    this.salesApi.getInvoices().subscribe(sales => {
      this.liveSales = sales;
    });
  }

  public openLedgerDepositModal(): void {
    this.ledgerDepositForm = {
      depositAmount: 500000,
      treasuryChallanNo: 'TR-2026-10-Sonali-00' + Math.floor(600 + Math.random() * 350),
      depositDate: '2026-10-24',
      bankBranch: 'Sonali Bank Ltd, Custom House Corporate Branch',
      economicCode: '1/1133/0010/0311'
    };
    this.isLedgerDepositModalOpen = true;
  }

  public submitLedgerDeposit(): void {
    if (this.ledgerDepositForm.depositAmount <= 0 || !this.ledgerDepositForm.treasuryChallanNo) {
      this.toast.error('ট্রেজারি চালান নম্বর ও জমার পরিমাণ সঠিকভাবে প্রদান করুন।', 'তথ্য অসম্পূর্ণ');
      return;
    }

    this.complianceApi.recordLedgerDeposit(this.ledgerDepositForm).subscribe({
      next: (res) => {
        this.toast.success(
          `ট্রেজারি চালান (${res.referenceNumber}) অনুযায়ী ৳${res.creditAmount} চলতি হিসাবে সফলভাবে জমা হয়েছে!`,
          'ট্রেজারি জমা সম্পন্ন (Current Account Credited)'
        );
        this.isLedgerDepositModalOpen = false;
        this.loadVatLedger();
      },
      error: () => {
        this.toast.error('ট্রেজারি চালান জমা লিপিবদ্ধ করতে ত্রুটি হয়েছে।', 'ত্রুটি');
      }
    });
  }

  public openCreditNoteModal(): void {
    if (this.liveSales.length > 0) {
      const s = this.liveSales[0];
      const partialNet = Math.round(s.subTotal * 0.1);
      this.creditNoteForm = {
        originalDocumentNumber: s.challanNumber63 || s.invoiceNumber,
        customerName: s.customerName,
        reason: 'ডেলিভারিতে ক্ষতিগ্রস্ত পণ্য ফেরত (Damaged Goods Return)',
        netAmount: partialNet,
        vatAmount: Math.round(partialNet * 0.15),
        correctionDate: '2026-10-24'
      };
    } else {
      this.creditNoteForm = {
        originalDocumentNumber: 'CH-DHK-2026-00142',
        customerName: 'ACI Logistics Ltd (Shwapno)',
        reason: 'ডেলিভারিতে ক্ষতিগ্রস্ত পণ্য ফেরত (Damaged Goods Return)',
        netAmount: 50000,
        vatAmount: 7500,
        correctionDate: '2026-10-24'
      };
    }
    this.isCreditNoteModalOpen = true;
  }

  public onSelectSaleForCreditNote(saleId: string): void {
    const s = this.liveSales.find(item => item.id === saleId);
    if (!s) return;
    const partialNet = Math.round((s.subTotal || s.grandTotal) * 0.1);
    this.creditNoteForm.originalDocumentNumber = s.challanNumber63 || s.invoiceNumber;
    this.creditNoteForm.customerName = s.customerName;
    this.creditNoteForm.netAmount = partialNet;
    this.creditNoteForm.vatAmount = Math.round(partialNet * 0.15);
  }

  public calculateCreditNoteVat(): void {
    this.creditNoteForm.vatAmount = Math.round(this.creditNoteForm.netAmount * 0.15);
  }

  public submitCreditNote(): void {
    if (!this.creditNoteForm.originalDocumentNumber || this.creditNoteForm.vatAmount <= 0) {
      this.toast.error('ক্রেডিট নোটের তথ্য সঠিকভাবে প্রদান করুন।', 'তথ্য অসম্পূর্ণ');
      return;
    }

    this.complianceApi.issueCreditNote(this.creditNoteForm).subscribe({
      next: (res) => {
        this.toast.success(
          `মূসক ৬.৭ ক্রেডিট নোট (${res.documentNumber}) সফলভাবে ইস্যু ও চলতি হিসাবে হ্রাসকারী সমন্বয় করা হয়েছে!`,
          'ক্রেডিট নোট ইস্যু সম্পন্ন (Credit Note Approved)'
        );
        this.isCreditNoteModalOpen = false;
        this.loadVatLedger();
        this.loadAdjustments();
      },
      error: () => {
        this.toast.error('ক্রেডিট নোট তৈরিতে ত্রুটি হয়েছে।', 'ত্রুটি');
      }
    });
  }

  public openDebitNoteModal(): void {
    if (this.liveSales.length > 0) {
      const s = this.liveSales[0];
      const partialNet = Math.round(s.subTotal * 0.05);
      this.debitNoteForm = {
        originalDocumentNumber: s.challanNumber63 || s.invoiceNumber,
        customerName: s.customerName,
        reason: 'দূরবর্তী পরিবহন চার্জ ও কাঁচামাল মূল্য বৃদ্ধি (Fuel & Distance Surcharge)',
        netAmount: partialNet,
        vatAmount: Math.round(partialNet * 0.15),
        correctionDate: '2026-10-24'
      };
    } else {
      this.debitNoteForm = {
        originalDocumentNumber: 'CH-DHK-2026-00140',
        customerName: 'Pran Dairy & Consumer Ltd',
        reason: 'দূরবর্তী পরিবহন চার্জ ও কাঁচামাল মূল্য বৃদ্ধি (Fuel & Distance Surcharge)',
        netAmount: 30000,
        vatAmount: 4500,
        correctionDate: '2026-10-24'
      };
    }
    this.isDebitNoteModalOpen = true;
  }

  public onSelectSaleForDebitNote(saleId: string): void {
    const s = this.liveSales.find(item => item.id === saleId);
    if (!s) return;
    const partialNet = Math.round((s.subTotal || s.grandTotal) * 0.05);
    this.debitNoteForm.originalDocumentNumber = s.challanNumber63 || s.invoiceNumber;
    this.debitNoteForm.customerName = s.customerName;
    this.debitNoteForm.netAmount = partialNet;
    this.debitNoteForm.vatAmount = Math.round(partialNet * 0.15);
  }

  public calculateDebitNoteVat(): void {
    this.debitNoteForm.vatAmount = Math.round(this.debitNoteForm.netAmount * 0.15);
  }

  public submitDebitNote(): void {
    if (!this.debitNoteForm.originalDocumentNumber || this.debitNoteForm.vatAmount <= 0) {
      this.toast.error('ডেবিট নোটের তথ্য সঠিকভাবে প্রদান করুন।', 'তথ্য অসম্পূর্ণ');
      return;
    }

    this.complianceApi.issueDebitNote(this.debitNoteForm).subscribe({
      next: (res) => {
        this.toast.success(
          `মূসক ৬.৮ ডেবিট নোট (${res.documentNumber}) সফলভাবে ইস্যু ও চলতি হিসাবে বৃদ্ধিকারী সমন্বয় করা হয়েছে!`,
          'ডেবিট নোট ইস্যু সম্পন্ন (Debit Note Approved)'
        );
        this.isDebitNoteModalOpen = false;
        this.loadVatLedger();
        this.loadAdjustments();
      },
      error: () => {
        this.toast.error('ডেবিট নোট তৈরিতে ত্রুটি হয়েছে।', 'ত্রুটি');
      }
    });
  }

  public downloadPdf(): void {
    const filename = this.ledgerView === 'adjustments'
      ? 'Mushak_6_7_6_8_Adjustments_Register_October_2026'
      : 'Mushak_18_Current_VAT_Ledger_October_2026';
    this.pdfExport.downloadElementAsPdf('mushak-18-doc', filename);
  }

  public printDocument(): void {
    this.pdfExport.printClean();
  }

  public exportToCsv(): void {
    const compId = this.context.activeCompany().id;
    const period = this.context.activeFiscalPeriod().periodCode;
    const headers = [
      'Entry Date',
      'Reference No / Challan',
      'Transaction Type',
      'Description',
      'Statutory Form',
      'Debit / Output Tax (BDT)',
      'Credit / Deposit / Rebate (BDT)',
      'Running Balance (BDT)',
      'Bank & Branch',
      'Economic Code',
      'Status'
    ];

    const rows = this.ledgerEntries.map(e => [
      e.entryDate,
      e.referenceNumber,
      e.transactionType,
      e.description,
      e.statutoryForm,
      e.debitAmount,
      e.creditAmount,
      e.balance,
      e.bankBranch || '',
      e.economicCode || '1/1133/0010/0311',
      e.status
    ]);

    this.csvExport.exportToCsv(`Mushak_18_Current_VAT_Ledger_${compId}_${period}`, headers, rows);
  }
}
