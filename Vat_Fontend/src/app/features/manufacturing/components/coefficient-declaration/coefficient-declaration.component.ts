import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ManufacturingApiService } from '../../../../core/services/api/manufacturing-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { ToastService } from '../../../../core/services/toast.service';
import { PdfExportService } from '../../../../core/services/pdf-export.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../../shared/pipes/bengali-number.pipe';
import { Coefficient43 } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-coefficient-declaration',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="mushak-wrapper">
      <!-- Declaration Switcher Card -->
      <div class="declaration-selector-card no-print" *ngIf="declarations.length > 0">
        <div class="selector-left">
          <label class="vat-label-inline font-bengali">সহগ ঘোষণা নির্বাচন করুন:</label>
          <select class="vat-select" [ngModel]="activeDeclaration.declarationNumber" (ngModelChange)="onSelectDeclaration($event)">
            <option *ngFor="let d of declarations" [value]="d.declarationNumber">
              📜 {{ d.declarationNumber }} — {{ d.finishedProductName }} ({{ d.batchSize }} {{ d.uom }}) — [{{ d.status }}]
            </option>
          </select>
        </div>
        <div class="selector-right">
          <span class="status-badge" [class.approved]="activeDeclaration.status === 'Approved'" [class.submitted]="activeDeclaration.status === 'Submitted'">
            {{ activeDeclaration.status || 'Approved' }}
          </span>
          <button 
            *ngIf="activeDeclaration.status === 'Submitted' && canApprove()" 
            class="btn btn-sm btn-approve" 
            (click)="approveActiveDeclaration()"
          >
            ✓ Approve Declaration (অনুমোদন করুন)
          </button>
          <button class="btn btn-secondary btn-sm" (click)="printClean()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="6 9 6 2 18 2 18 9"></polyline>
              <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path>
              <rect x="6" y="14" width="12" height="8"></rect>
            </svg>
            Print
          </button>
          <button class="btn btn-primary btn-sm" (click)="downloadPdf()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Download PDF
          </button>
        </div>
      </div>

      <div class="statutory-paper" id="mushak-4-3-doc" *ngIf="activeDeclaration">
        <div class="govt-heading">
          <h3 class="font-bengali">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4 class="font-bengali">জাতীয় রাজস্ব বোর্ড</h4>
          <div class="form-badge font-bengali">মূসক-৪.৩</div>
          <h2 class="register-title font-bengali">উপকরণ-উৎপাদ সহগ (Input-Output Coefficient) ঘোষণা</h2>
          <p class="rule-ref font-bengali">[বিধি ২১ এর উপ-বিধি (১) দ্রষ্টব্য]</p>
        </div>

        <div class="taxpayer-meta-box">
          <div class="meta-row">
            <span class="meta-label">ঘোষণা নম্বর:</span>
            <strong class="font-mono text-emerald">{{ activeDeclaration.declarationNumber }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">উৎপাদিত পণ্যের নাম:</span>
            <strong class="font-bengali">{{ activeDeclaration.finishedProductName }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">প্রতিষ্ঠানের নাম ও বিআইএন:</span>
            <strong class="font-mono">{{ context.activeCompany().bin }} ({{ context.activeCompany().nameBn || context.activeCompany().name }})</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">এইচএস কোড ও ব্যাচ সাইজ:</span>
            <strong class="font-mono">{{ activeDeclaration.hsCode }} (ব্যাচ সাইজ: {{ activeDeclaration.batchSize }} {{ activeDeclaration.uom }})</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">কার্যকরের তারিখ:</span>
            <strong>{{ activeDeclaration.effectiveFrom }} (জমা: {{ activeDeclaration.submissionDate }})</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">উৎপাদন কারখানা / শাখা:</span>
            <strong class="text-emerald font-bengali">🏢 {{ activeDeclaration.branchName || context.branchName() }}</strong>
          </div>
          <div class="meta-row" *ngIf="activeDeclaration.approvedBy">
            <span class="meta-label">অনুমোদনকারী কর্মকর্তা:</span>
            <strong class="font-mono">{{ activeDeclaration.approvedBy }}</strong>
          </div>
        </div>

        <!-- Raw Materials & Wastage Ratio Table -->
        <h3 class="section-title">১. কাঁচামাল ও প্যাকিং উপকরণের বিবরণ (Raw Material Details)</h3>
        <div class="vat-table-container">
          <table class="vat-table mushak-grid">
            <thead>
              <tr>
                <th style="width: 40px;" class="text-center font-bengali">ক্রমিক</th>
                <th>উপকরণের নাম ও বিবরণ</th>
                <th class="text-center">এইচএস কোড</th>
                <th class="text-center">একক</th>
                <th class="text-right">নিট ব্যবহারের পরিমাণ</th>
                <th class="text-right">স্বাভাবিক অপচয় (%)</th>
                <th class="text-right">অপচয়সহ পরিমাণ</th>
                <th class="text-right">ক্রয় মূল্য (টাকা)</th>
                <th class="text-right">মোট খরচ (টাকা)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let m of activeDeclaration.materials; let i = index">
                <td class="text-center font-bengali">{{ (i + 1) | bengaliNumber }}</td>
                <td><strong>{{ m.rawMaterialName }}</strong></td>
                <td class="font-mono text-center">{{ m.hsCode }}</td>
                <td class="text-center">{{ m.uom }}</td>
                <td class="text-right numeric font-mono">{{ m.qtyRequired }}</td>
                <td class="text-right numeric text-amber font-mono">{{ m.wastagePercent }}%</td>
                <td class="text-right numeric font-bold font-mono">{{ (m.qtyRequired + (m.wastageQty || (m.qtyRequired * m.wastagePercent / 100))) }}</td>
                <td class="text-right numeric">{{ m.unitPrice | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold text-emerald">{{ (m.totalCost || ((m.qtyRequired + (m.qtyRequired * m.wastagePercent / 100)) * m.unitPrice)) | bdtCurrency:false }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row">
                <th colspan="8" class="text-right font-bengali">(ক) উপকরণের সর্বমোট ক্রয়মূল্য:</th>
                <th class="text-right numeric font-bold text-emerald">{{ activeDeclaration.totalRawMaterialCost | bdtCurrency:false }}</th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- Value Addition & Cost Structure -->
        <h3 class="section-title mt-4">২. মূল্য সংযোজন ও লাভ মার্জিন (Value Addition & Profit Margin)</h3>
        <div class="cost-breakdown-card">
          <div class="cost-row">
            <span>(খ) প্রত্যক্ষ শ্রম ব্যয় (Direct Labor):</span>
            <span class="font-mono font-bold">{{ activeDeclaration.directLaborCost | bdtCurrency }}</span>
          </div>
          <div class="cost-row">
            <span>(গ) বিদ্যুৎ ও জ্বালানি ইউটিলিটি খরচ (Utilities):</span>
            <span class="font-mono font-bold">{{ activeDeclaration.utilityCost | bdtCurrency }}</span>
          </div>
          <div class="cost-row">
            <span>(ঘ) কারখানা উপরি-ব্যয় (Overheads):</span>
            <span class="font-mono font-bold">{{ activeDeclaration.overheadCost | bdtCurrency }}</span>
          </div>
          <div class="cost-row highlight-margin">
            <span>(ঙ) মুনাফা মার্জিন (Profit Margin - {{ activeDeclaration.marginPercent }}%):</span>
            <span class="font-mono font-bold text-emerald">
              {{ ((activeDeclaration.totalRawMaterialCost + activeDeclaration.directLaborCost + activeDeclaration.utilityCost + activeDeclaration.overheadCost) * activeDeclaration.marginPercent / 100) | bdtCurrency }}
            </span>
          </div>
          <div class="divider"></div>
          <div class="cost-row total-selling-price">
            <span>ঘোষিত প্রমিত বিক্রয় মূল্য প্রতি ইউনিট (মূসক ব্যতীত):</span>
            <span class="font-mono text-emerald font-bold text-xl">{{ activeDeclaration.finalSellingPrice | bdtCurrency }} / {{ activeDeclaration.uom }}</span>
          </div>
        </div>

        <div class="declaration-auth-footer">
          <div class="declaration-text font-bengali">
            "আমি ঘোষণা করিতেছি যে, এই উপকরণ-উৎপাদ সহগ ঘোষণায় বর্ণিত তথ্যাদি আমার জ্ঞান ও বিশ্বাসমতে সত্য ও সঠিক। কোনো অসত্য তথ্য প্রদান করা হইলে তাহা মূসক আইনের অধীন দণ্ডনীয় হইবে।"
          </div>
          <div class="sig-section">
            <div class="sig-box">
              <div class="sig-line"></div>
              <span class="font-bengali">ঘোষণাকারীর স্বাক্ষর, নাম ও পদবী সিল</span>
              <small class="text-dim">Authorized Signatory</small>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .mushak-wrapper { display: flex; flex-direction: column; gap: 16px; }
    .declaration-selector-card { display: flex; align-items: center; justify-content: space-between; background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 12px 20px; flex-wrap: wrap; gap: 12px; }
    .selector-left { display: flex; align-items: center; gap: 12px; flex: 1; max-width: 650px; }
    .selector-right { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .btn-approve { background: var(--vat-emerald); color: #fff; border: none; padding: 6px 12px; border-radius: var(--radius-sm); font-size: 0.78rem; font-weight: 700; cursor: pointer; }
    .btn-approve:hover { filter: brightness(1.1); }
    .vat-label-inline { font-size: 0.85rem; color: var(--text-muted); font-weight: 600; white-space: nowrap; }

    .statutory-paper { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-lg); padding: 30px; }
    .govt-heading { text-align: center; margin-bottom: 24px; display: flex; flex-direction: column; align-items: center; gap: 3px; }
    .form-badge { border: 2px solid var(--vat-emerald); color: var(--vat-emerald); padding: 3px 14px; font-weight: 800; font-size: 1rem; border-radius: var(--radius-sm); margin: 6px 0; }
    .register-title { font-size: 1.4rem; font-weight: 800; }
    .rule-ref { font-size: 0.75rem; color: var(--text-dim); }
    .taxpayer-meta-box { border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 16px; margin-bottom: 20px; display: grid; grid-template-columns: 1fr 1fr; gap: 10px; background: var(--bg-card-subtle); font-size: 0.85rem; }
    .meta-row { display: flex; gap: 8px; }
    .meta-label { color: var(--text-muted); font-weight: 600; }
    .section-title { font-size: 0.95rem; font-weight: 700; color: var(--vat-teal); margin-bottom: 12px; }
    .mushak-grid th { text-align: center; font-size: 0.75rem; padding: 8px 6px; }
    .cost-breakdown-card { background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 20px; display: flex; flex-direction: column; gap: 10px; max-width: 650px; }
    .cost-row { display: flex; justify-content: space-between; font-size: 0.85rem; }
    .highlight-margin { color: var(--vat-emerald); }
    .divider { height: 1px; background: var(--border-card); margin: 4px 0; }
    .total-selling-price { font-size: 1rem; font-weight: 800; color: var(--text-main); }
    .text-xl { font-size: 1.25rem; }
    .declaration-auth-footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid var(--border-card); display: flex; justify-content: space-between; align-items: flex-end; }
    .declaration-text { font-size: 0.8rem; color: var(--text-muted); max-width: 450px; font-style: italic; }
    .sig-section { display: flex; justify-content: flex-end; }
    .sig-box { display: flex; flex-direction: column; align-items: center; gap: 6px; font-size: 0.8rem; color: var(--text-muted); }
    .sig-line { width: 220px; border-top: 1px dashed var(--border-subtle); }
    .status-badge { padding: 2px 8px; border-radius: 9999px; font-size: 0.75rem; font-weight: 700; }
    .status-badge.approved { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .status-badge.submitted { background: rgba(245, 158, 11, 0.15); color: #fbbf24; }
  `]
})
export class CoefficientDeclarationComponent implements OnInit {
  private manufacturingApi = inject(ManufacturingApiService);
  private pdfExportService = inject(PdfExportService);
  private toast = inject(ToastService);
  public context = inject(CompanyContextService);

  public declarations: Coefficient43[] = [];
  public activeDeclaration!: Coefficient43;

  ngOnInit(): void {
    this.loadDeclarations();
  }

  public loadDeclarations(): void {
    this.manufacturingApi.getCoefficients().subscribe({
      next: (list) => {
        this.declarations = list;
        if (list.length > 0) {
          this.activeDeclaration = list[0];
        }
      },
      error: (err) => console.warn('Failed to load coefficients:', err)
    });
  }

  public onSelectDeclaration(decNo: string): void {
    const found = this.declarations.find(d => d.declarationNumber === decNo);
    if (found) {
      this.activeDeclaration = found;
    }
  }

  public setDeclaration(dec: Coefficient43): void {
    this.activeDeclaration = dec;
  }

  public canApprove(): boolean {
    const role = this.context.userRole();
    return role === 'TaxManager' || role === 'Approver' || role === 'SuperAdmin';
  }

  public approveActiveDeclaration(): void {
    if (!this.activeDeclaration?.id) return;
    this.manufacturingApi.approveCoefficient(this.activeDeclaration.id).subscribe({
      next: (res) => {
        this.activeDeclaration.status = 'Approved';
        this.activeDeclaration.approvedBy = res.approvedBy || 'USR-8803';
        this.toast.success(
          `সহগ ঘোষণা '${this.activeDeclaration.declarationNumber}' সফলভাবে অনুমোদিত হয়েছে!`,
          'অনুমোদন সম্পন্ন (Mushak 4.3 Approved)'
        );
      },
      error: (err) => {
        console.error('Approval failed:', err);
        this.toast.error('সহগ অনুমোদন ব্যর্থ হয়েছে।', 'Approval Failed');
      }
    });
  }

  public downloadPdf(): void {
    const decNo = this.activeDeclaration?.declarationNumber.replace(/[\/\\]/g, '_') || 'Mushak_4_3';
    this.pdfExportService.downloadElementAsPdf('mushak-4-3-doc', `Mushak_4.3_${decNo}.pdf`, 'portrait');
  }

  public printClean(): void {
    this.pdfExportService.printClean();
  }
}
