import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { ComplianceApiService } from '../../../../core/services/api/compliance-api.service';
import { PurchaseApiService } from '../../../../core/services/api/purchase-api.service';
import { SalesApiService } from '../../../../core/services/api/sales-api.service';
import { PdfExportService } from '../../../../core/services/pdf-export.service';
import { CsvExportService } from '../../../../core/services/csv-export.service';
import { ToastService } from '../../../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { Mushak91Return, PurchaseInvoice, SalesInvoice } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-mushak-9-1',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe],
  template: `
    <div class="mushak-9-1-container" *ngIf="returnDoc; else unavailable">
      <div class="action-bar no-print">
        <div class="action-bar-left">
          <span class="status-indicator-badge" [ngClass]="returnDoc.status === 'Submitted' ? 'badge-success' : 'badge-warning'">
            {{ returnDoc.status === 'Submitted' ? 'Local submission; filing receipt required' : '📝 খসড়া দাখিলপত্র (Draft)' }}
          </span>
          <span class="revision-chip" *ngIf="returnDoc.revision && returnDoc.revision > 1">
            Revision {{ returnDoc.revision }}
          </span>
        </div>
        <div class="action-bar-right">
          <button class="btn btn-secondary btn-sm" (click)="exportToCsv()" title="Export CSV Summary">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Export CSV
          </button>
          <button class="btn btn-secondary btn-sm" (click)="printDocument()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="6 9 6 2 18 2 18 9"></polyline>
              <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path>
              <rect x="6" y="14" width="12" height="8"></rect>
            </svg>
            Print
          </button>
          <button class="btn btn-secondary btn-sm" (click)="downloadPdf()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Download PDF
          </button>
          <button class="btn btn-primary btn-sm" (click)="submitReturn()">
            External filing unavailable
          </button>
        </div>
      </div>

      <div id="mushak-9-1-print-doc" *ngIf="returnDoc" class="statutory-paper">
        <div class="govt-heading">
          <h3 class="font-bengali">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4 class="font-bengali">জাতীয় রাজস্ব বোর্ড</h4>
          <div class="form-badge font-bengali">মূসক-৯.১</div>
          <h2 class="register-title font-bengali">মূল্য সংযোজন কর দাখিলপত্র</h2>
          <p class="rule-ref font-bengali">[বিধি ৪৭ এর উপ-বিধি (১) দ্রষ্টব্য] — কর মেয়াদ: {{ returnDoc.fiscalPeriod }}</p>
        </div>

        <!-- Part 1 & 2: Taxpayer & Submission Overview -->
        <div class="taxpayer-meta-box">
          <div class="meta-row">
            <span class="meta-label">করদাতার নাম:</span>
            <strong class="font-bengali">{{ returnDoc.companyName }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">বিআইএন (BIN):</span>
            <strong class="font-mono text-emerald font-bold">{{ returnDoc.bin }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">দাখিলপত্রের ধরন:</span>
            <span><strong>{{ returnDoc.revision && returnDoc.revision > 1 ? 'সংশোধিত দাখিলপত্র (Amended Return - Rev ' + returnDoc.revision + ')' : 'মূল দাখিলপত্র (Main Return)' }}</strong></span>
          </div>
          <div class="meta-row">
            <span class="meta-label">দাখিলের অবস্থা:</span>
            <span><strong>{{ returnDoc.status === 'Submitted' ? 'Local submission; not proof of external filing' : '📝 খসড়া (Draft)' }}</strong></span>
          </div>
          <div class="meta-row">
            <span class="meta-label">দাখিলের শেষ সময়:</span>
            <span class="text-amber font-bold">১৫-নভেম্বর-২০২৬</span>
          </div>
          <div class="meta-row">
            <span class="meta-label">দাখিলকারী শাখা / ডিপো:</span>
            <strong class="text-emerald">🏢 {{ context.activeBranch().name }} (BIN Ext: {{ context.activeBranch().binExtension }})</strong>
          </div>
        </div>

        <!-- Part 3: Output Tax Breakdown -->
        <div class="part-section">
          <div class="part-header font-bengali">অংশ-৩: সরবরাহ—প্রদেয় কর (Output Tax on Supplies)</div>
          <table class="vat-table part-table">
            <thead>
              <tr>
                <th>সরবরাহের প্রকৃতি</th>
                <th class="text-right">মূল্য (টাকা)</th>
                <th class="text-right">সম্পূরক শুল্ক (টাকা)</th>
                <th class="text-right">মূসক (টাকা)</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>আদর্শ হারে করযোগ্য পণ্য/সেবা সরবরাহ (১৫%)</td>
                <td class="text-right numeric font-bold">{{ returnDoc.totalStandardSales | bdtCurrency:false }}</td>
                <td class="text-right numeric">০.০০</td>
                <td class="text-right numeric font-bold text-rose">{{ returnDoc.totalStandardOutputVat | bdtCurrency:false }}</td>
              </tr>
              <tr>
                <td>অব্যাহতিপ্রাপ্ত পণ্য/সেবা সরবরাহ</td>
                <td class="text-right numeric">{{ returnDoc.totalExemptSales | bdtCurrency:false }}</td>
                <td class="text-right numeric">০.০০</td>
                <td class="text-right numeric">০.০০</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row">
                <th>সর্বমোট প্রদেয় উৎপাদ কর (কলাম ৩-এর মোট):</th>
                <th class="text-right numeric">{{ (returnDoc.totalStandardSales + returnDoc.totalExemptSales) | bdtCurrency:false }}</th>
                <th class="text-right numeric">০.০০</th>
                <th class="text-right numeric text-rose font-bold">{{ returnDoc.totalOutputVat | bdtCurrency:false }}</th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- Part 4: Input Tax Credit Breakdown -->
        <div class="part-section mt-4">
          <div class="part-header font-bengali">অংশ-৪: ক্রয়—উপকরণ কর রেয়াত (Input Tax Credit)</div>
          <table class="vat-table part-table">
            <thead>
              <tr>
                <th>ক্রয়ের প্রকৃতি</th>
                <th class="text-right">মূল্য (টাকা)</th>
                <th class="text-right">রেয়াতযোগ্য মূসক (টাকা)</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>স্থানীয় আদর্শ হারে ক্রয় (১৫% রেয়াতযোগ্য)</td>
                <td class="text-right numeric font-bold">{{ returnDoc.totalStandardPurchases | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold text-emerald">{{ returnDoc.totalInputVatClaimable | bdtCurrency:false }}</td>
              </tr>
              <tr>
                <td>আমদানি ক্রয় (শুল্ক স্টেশন ও বিল অব এন্ট্রি ভ্যাট)</td>
                <td class="text-right numeric font-bold">{{ returnDoc.totalImportPurchases | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold text-emerald">{{ returnDoc.totalImportVatClaimable | bdtCurrency:false }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row">
                <th>সর্বমোট উপকরণ কর রেয়াত (কলাম ৩-এর মোট):</th>
                <th class="text-right numeric">{{ (returnDoc.totalStandardPurchases + returnDoc.totalImportPurchases) | bdtCurrency:false }}</th>
                <th class="text-right numeric text-emerald font-bold">{{ returnDoc.totalInputVatCredit | bdtCurrency:false }}</th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- Part 5, 6, 7: Adjustments & Net Tax Payable -->
        <div class="part-section mt-4">
          <div class="part-header font-bengali">অংশ-৫, ৬ ও ৭: কর সমন্বয় ও নীট কর হিসাব (Net Tax Payable)</div>
          <div class="reconciliation-grid">
            <div class="recon-row">
              <span>(ক) মোট উৎপাদ কর (Part 3 Output VAT):</span>
              <span class="font-mono font-bold text-rose">+ {{ returnDoc.totalOutputVat | bdtCurrency }}</span>
            </div>
            <div class="recon-row">
              <span>(খ) মোট উপকরণ কর রেয়াত (Part 4 Input Credit):</span>
              <span class="font-mono font-bold text-emerald">- {{ returnDoc.totalInputVatCredit | bdtCurrency }}</span>
            </div>
            <div class="recon-row">
              <span>(গ) বৃদ্ধিকারী সমন্বয় (উৎস কর্তন ও ডেবিট নোট):</span>
              <span class="font-mono font-bold text-amber">+ {{ returnDoc.vdsDeductedFromSuppliers | bdtCurrency }}</span>
            </div>
            <div class="recon-row">
              <span>(ঘ) হ্রাসকারী সমন্বয় (ক্রেতা কর্তৃক কর্তিত মূসক ও ক্রেডিট নোট):</span>
              <span class="font-mono font-bold text-emerald">- {{ returnDoc.vdsDeductedByCustomers | bdtCurrency }}</span>
            </div>
            <div class="recon-divider"></div>
            <div class="recon-row net-tax-row">
              <span>লাইন ৪৬: ট্রেজারিতে নীট প্রদেয় কর (Net VAT Payable):</span>
              <span class="font-mono text-emerald font-bold text-xl">{{ returnDoc.netTaxPayable | bdtCurrency }}</span>
            </div>
          </div>
        </div>

        <!-- Part 8: Treasury Challan Linkage -->
        <div class="part-section mt-4">
          <div class="part-header font-bengali">অংশ-৮: ট্রেজারি চালানে কর পরিশোধের বিবরণী (e-Challan Deposit)</div>
          <div class="treasury-box">
            <div class="tr-item">
              <span class="tr-label">ট্রেজারি চালান নম্বর:</span>
              <strong class="font-mono">{{ returnDoc.treasuryChallanNo }}</strong>
            </div>
            <div class="tr-item">
              <span class="tr-label">জমার তারিখ:</span>
              <strong>{{ returnDoc.treasuryDepositDate }}</strong>
            </div>
            <div class="tr-item">
              <span class="tr-label">পরিশোধিত জমার পরিমাণ:</span>
              <strong class="font-mono text-emerald">{{ returnDoc.treasuryDepositAmount | bdtCurrency }}</strong>
            </div>
            <div class="tr-item">
              <span class="tr-label">পরবর্তী মেয়াদের উদ্বৃত্ত জের:</span>
              <strong class="font-mono text-teal">{{ returnDoc.closingBalance | bdtCurrency }}</strong>
            </div>
          </div>
        </div>
      </div>
    </div>
    <ng-template #unavailable><p role="status">{{ loadMessage }}</p></ng-template>
  `,
  styles: [`
    .action-bar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; flex-wrap: wrap; gap: 12px; }
    .action-bar-left { display: flex; align-items: center; gap: 10px; }
    .action-bar-right { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .status-indicator-badge { font-size: 0.8rem; font-weight: 700; padding: 4px 10px; border-radius: 9999px; }
    .badge-success { background: rgba(16, 185, 129, 0.15); color: #10b981; border: 1px solid rgba(16, 185, 129, 0.3); }
    .badge-warning { background: rgba(245, 158, 11, 0.15); color: #f59e0b; border: 1px solid rgba(245, 158, 11, 0.3); }
    .revision-chip { font-size: 0.75rem; background: var(--bg-card); color: var(--vat-teal); padding: 3px 8px; border-radius: 4px; border: 1px solid var(--border-card); }

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
    .part-table th { font-size: 0.75rem; padding: 8px 10px; }
    .reconciliation-grid { background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 20px; display: flex; flex-direction: column; gap: 12px; max-width: 650px; }
    .recon-row { display: flex; justify-content: space-between; font-size: 0.88rem; }
    .recon-divider { height: 1px; background: var(--border-card); margin: 4px 0; }
    .net-tax-row { font-size: 1.05rem; font-weight: 800; color: var(--text-main); }
    .treasury-box { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 14px; background: var(--bg-card-subtle); padding: 16px; border-radius: var(--radius-md); border: 1px solid var(--border-card); }
    .tr-item { display: flex; flex-direction: column; gap: 4px; font-size: 0.85rem; }
    .tr-label { font-size: 0.75rem; color: var(--text-dim); }
    .text-xl { font-size: 1.25rem; }
    .mt-4 { margin-top: 24px; }
  `]
})
export class Mushak91Component implements OnInit {
  private complianceApi = inject(ComplianceApiService);
  private purchaseApi = inject(PurchaseApiService);
  private salesApi = inject(SalesApiService);
  private pdfExport = inject(PdfExportService);
  private csvExport = inject(CsvExportService);
  private toast = inject(ToastService);
  public context = inject(CompanyContextService);

  public returnDoc!: Mushak91Return;
  public loadMessage = 'Loading verified return data…';
  public livePurchases: PurchaseInvoice[] = [];
  public liveSales: SalesInvoice[] = [];

  ngOnInit(): void {
    this.loadReturnDoc();

  }

  public loadReturnDoc(): void {
    const period = this.context.activeFiscalPeriod().periodCode;
    if (!period) { this.loadMessage = 'Select a verified fiscal period. Return calculation and external filing are currently unavailable.'; return; }
    this.complianceApi.getReturn91(period).subscribe({
      next: data => { this.returnDoc = data; },
      error: () => { this.loadMessage = 'Verified return data is unavailable. No return has been filed.'; }
    });
  }

  public loadPurchases(): void {
    this.purchaseApi.getInvoices().subscribe(purchases => {
      this.livePurchases = purchases;
      this.recalculateReturn();
    });
  }

  public loadSales(): void {
    this.salesApi.getInvoices().subscribe(sales => {
      this.liveSales = sales;
      this.recalculateReturn();
    });
  }

  public recalculateReturn(): void {
    if (!this.returnDoc || this.returnDoc.status === 'Submitted') return;

    let relevantSales = this.liveSales;
    let relevantPurchases = this.livePurchases;
    if (!this.context.isSuperAdmin()) {
      const activeBranchId = this.context.activeBranch().id;
      relevantSales = relevantSales.filter(s => s.branchId === activeBranchId || s.branchId?.includes('31'));
      relevantPurchases = relevantPurchases.filter(p => p.branchId === activeBranchId || p.branchId?.includes('31'));
    }

    if (relevantSales.length > 0) {
      const salesSubTotal = relevantSales.reduce((acc, s) => acc + (s.subTotal || 0), 0);
      const salesVat = relevantSales.reduce((acc, s) => acc + (s.totalVat || 0), 0);
      this.returnDoc.totalStandardSales = salesSubTotal;
      this.returnDoc.totalStandardOutputVat = salesVat;
      this.returnDoc.totalOutputVat = salesVat + (this.returnDoc.totalSupplementaryDuty || 0);
    }

    if (relevantPurchases.length > 0) {
      const purchaseSubTotal = relevantPurchases.reduce((acc, p) => acc + (p.subTotal || 0), 0);
      const purchaseVat = relevantPurchases.reduce((acc, p) => acc + (p.totalVat || 0), 0);
      this.returnDoc.totalStandardPurchases = purchaseSubTotal;
      this.returnDoc.totalInputVatClaimable = purchaseVat;
      this.returnDoc.totalInputVatCredit = purchaseVat + (this.returnDoc.totalImportVatClaimable || 0);
    }

    const net = (this.returnDoc.totalOutputVat || 0) + (this.returnDoc.vdsDeductedFromSuppliers || 0) - (this.returnDoc.totalInputVatCredit || 0) - (this.returnDoc.vdsDeductedByCustomers || 0);
    this.returnDoc.netTaxPayable = net > 0 ? net : 0;
    this.returnDoc.closingBalance = net < 0 ? Math.abs(net) : 0;
  }

  public submitReturn(): void {
    this.toast.warning('External filing is unavailable until validated mappings and an official filing receipt are configured. No return has been filed.');
  }

  public downloadPdf(): void {
    if (!this.returnDoc) return;
    this.pdfExport.downloadElementAsPdf('mushak-9-1-print-doc', 'Mushak_9_1_Monthly_VAT_Return_October_2026');
  }

  public printDocument(): void {
    if (!this.returnDoc) return;
    this.pdfExport.printClean();
  }

  public exportToCsv(): void {
    if (!this.returnDoc) return;
    const compId = this.context.activeCompany().id;
    const period = this.context.activeFiscalPeriod().periodCode;
    const headers = ['Tax Note / Line Ref', 'Description', 'Declared Amount (BDT)'];
    const rows = [
      ['Note 1', 'Total Standard Sales (Pre-VAT)', this.returnDoc?.totalStandardSales || 0],
      ['Note 2', 'Total Output VAT (15% Standard + Other)', this.returnDoc?.totalOutputVat || 0],
      ['Note 3', 'Total Purchases / Inward Supplies (Pre-VAT)', this.returnDoc?.totalStandardPurchases || 0],
      ['Note 4', 'Total Input Tax Credit (Section 46)', this.returnDoc?.totalInputVatCredit || 0],
      ['Note 5', 'Net Tax Payable / (Refundable)', this.returnDoc?.netTaxPayable || 0],
      ['Note 6', 'Closing Balance in Current Account', this.returnDoc?.closingBalance || 0]
    ];
    this.csvExport.exportToCsv(`Mushak_9_1_Return_Summary_${compId}_${period}`, headers, rows);
  }
}
