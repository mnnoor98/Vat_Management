import { Component, OnInit, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryApiService } from '../../../../core/services/api/inventory-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { PdfExportService } from '../../../../core/services/pdf-export.service';
import { BdtCurrencyPipe } from '../../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../../shared/pipes/bengali-number.pipe';
import { Mushak65Transfer } from '../../../../core/models/vat-models';

@Component({
  selector: 'app-mushak-6-5',
  standalone: true,
  imports: [CommonModule, FormsModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="mushak-wrapper">
      <!-- Transfer Selection Bar -->
      <div class="challan-selector-card no-print" *ngIf="transfers.length > 0">
        <div class="selector-left">
          <label class="vat-label-inline font-bengali">ইস্যুকৃত চালান নির্বাচন:</label>
          <select class="vat-select" [ngModel]="activeTransfer?.transferNumber" (ngModelChange)="onSelectTransfer($event)">
            <option *ngFor="let t of transfers" [value]="t.transferNumber">
              📜 {{ t.transferNumber }} ({{ t.dispatchDate }}) — [{{ t.fromWarehouseCode || t.fromBranch }} ➔ {{ t.toWarehouseCode || t.toBranch }}] — {{ t.status }}
            </option>
          </select>
        </div>
        <div class="selector-right">
          <span class="badge-status-completed">✓ {{ activeTransfer?.status || 'Posted' }}</span>
          <button class="btn btn-sm btn-outline-primary" (click)="requestNewTransfer.emit()">
            + New Transfer
          </button>
          <button class="btn btn-secondary btn-sm" (click)="printDocument()">
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

      <div class="statutory-paper" id="mushak-6-5-doc" *ngIf="activeTransfer">
        <div class="govt-heading">
          <h3 class="font-bengali">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4 class="font-bengali">জাতীয় রাজস্ব বোর্ড</h4>
          <div class="form-badge font-bengali">মূসক-৬.৫</div>
          <h2 class="register-title font-bengali">কেন্দ্রীয় নিবন্ধিত প্রতিষ্ঠানের পণ্য স্থানান্তর চালানপত্র</h2>
          <p class="rule-ref font-bengali">[বিধি ৪০ এর উপ-বিধি (১) এর দফা (ঘ) দ্রষ্টব্য]</p>
        </div>

        <div class="taxpayer-meta-box">
          <div class="meta-row">
            <span class="meta-label">স্থানান্তর চালান নং:</span>
            <strong class="font-mono text-emerald">{{ activeTransfer.transferNumber }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">ইস্যুর তারিখ ও সময়:</span>
            <strong>{{ activeTransfer.dispatchDate }} (সময়: {{ activeTransfer.dispatchTime }})</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">কেন্দ্রীয় নিবন্ধন বিআইএন:</span>
            <strong class="font-mono">{{ context.activeCompany().bin }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">প্রতিষ্ঠানের নাম:</span>
            <strong class="font-bengali">{{ context.activeCompany().nameBn || context.activeCompany().name }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">পরিবহন যান ও চালক:</span>
            <strong>{{ activeTransfer.vehicleNumber }} (চালক: {{ activeTransfer.driverName }})</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">চালানের উদ্দেশ্য / মন্তব্য:</span>
            <strong>{{ activeTransfer.remarks || 'শাখা পণ্য স্থানান্তর' }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">প্রেরণকারী গুদাম/শাখা:</span>
            <strong class="font-bengali">{{ activeTransfer.fromBranch || activeTransfer.fromWarehouseName || activeTransfer.fromWarehouseCode }}</strong>
          </div>
          <div class="meta-row">
            <span class="meta-label">গ্রহীতা গুদাম/শাখা:</span>
            <strong class="font-bengali">{{ activeTransfer.toBranch || activeTransfer.toWarehouseName || activeTransfer.toWarehouseCode }}</strong>
          </div>
        </div>

        <div class="vat-table-container">
          <table class="vat-table mushak-grid">
            <thead>
              <tr>
                <th style="width: 45px;" class="text-center font-bengali">ক্রমিক</th>
                <th>পণ্যের বিবরণ</th>
                <th class="text-center">এইচএস কোড</th>
                <th class="text-center">একক</th>
                <th class="text-right">স্থানান্তরিত পরিমাণ</th>
                <th class="text-right">একক মূল্য (টাকা)</th>
                <th class="text-right">মোট স্থানান্তর মূল্য (টাকা)</th>
                <th>মন্তব্য / বিবরণ</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of activeTransferLines; let i = index">
                <td class="text-center font-bengali">{{ (i + 1) | bengaliNumber }}</td>
                <td>
                  <strong>{{ item.productName || item.productDescription }}</strong>
                  <br><small class="text-dim font-bengali">{{ item.productNameBn || item.productDescription }}</small>
                </td>
                <td class="font-mono text-center">{{ item.hsCode || '3901.20.00' }}</td>
                <td class="text-center font-mono">{{ item.uom || 'KG' }}</td>
                <td class="text-right numeric font-bold">{{ item.quantity | bengaliNumber }}</td>
                <td class="text-right numeric">{{ item.unitPrice | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ item.totalValue | bdtCurrency:false }}</td>
                <td class="text-dim">{{ item.remarks || '-' }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row">
                <th colspan="6" class="text-right font-bengali">সর্বমোট স্থানান্তরিত পণ্যের প্রমিত মূল্য:</th>
                <th class="text-right numeric font-bold text-emerald">{{ activeTransferTotal | bdtCurrency:false }}</th>
                <th></th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- Statutory Footer Notice & Signatures -->
        <div class="transfer-footer-notice font-bengali">
          * বিশেষ সংবিধিবদ্ধ দ্রষ্টব্য: কেন্দ্রীয়ভাবে নিবন্ধিত প্রতিষ্ঠানের ক্ষেত্রে এক শাখা/ডিপো হইতে অন্য শাখা/কারখানায় পণ্য স্থানান্তরে মূসক প্রদেয় হইবে না। এই চালানপত্র মূসক ও সম্পূরক শুল্ক বিধিমালা ২০১৬ এর বিধি ৪০(১)(ঘ) মোতাবেক অভ্যন্তরীণ নিরীক্ষা, গুদাম রেজিস্টার ও পণ্য পরিবহনের জন্য বাধ্যতামূলক সরকারি দলিল।
        </div>

        <div class="signature-section" style="display: flex; justify-content: space-between; margin-top: 45px; padding: 0 40px;">
          <div class="sig-box" style="display: flex; flex-direction: column; align-items: center; gap: 6px;">
            <div style="width: 200px; border-top: 1px dashed var(--border-subtle);"></div>
            <span class="font-bengali" style="font-size: 0.85rem; color: var(--text-muted); font-weight: 600;">প্রেরণকারী কর্মকর্তার স্বাক্ষর ও সিল</span>
            <small class="text-dim">User: {{ activeTransfer.createdBy || 'USR-8805' }}</small>
          </div>
          <div class="sig-box" style="display: flex; flex-direction: column; align-items: center; gap: 6px;">
            <div style="width: 200px; border-top: 1px dashed var(--border-subtle);"></div>
            <span class="font-bengali" style="font-size: 0.85rem; color: var(--text-muted); font-weight: 600;">গ্রহীতা ডিপো ইনচার্জের স্বাক্ষর ও সিল</span>
            <small class="text-dim">Verified & Received</small>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .challan-selector-card { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 14px; background: var(--bg-card); border: 1px solid var(--border-card); padding: 12px 18px; border-radius: var(--radius-md); margin-bottom: 20px; }
    .selector-left { display: flex; align-items: center; gap: 10px; flex: 1; min-width: 280px; }
    .selector-right { display: flex; align-items: center; gap: 10px; }
    .vat-label-inline { font-size: 0.85rem; color: var(--text-muted); font-weight: 600; white-space: nowrap; }
    .badge-status-completed { font-size: 0.75rem; font-weight: 700; color: #34d399; background: rgba(16, 185, 129, 0.15); padding: 5px 12px; border-radius: 20px; border: 1px solid rgba(16, 185, 129, 0.3); }
    .btn-outline-primary { background: transparent; border: 1px solid var(--vat-emerald); color: var(--vat-emerald); font-weight: 700; font-size: 0.78rem; padding: 6px 14px; border-radius: var(--radius-sm); cursor: pointer; }
    .btn-outline-primary:hover { background: var(--vat-emerald); color: #ffffff; }

    .statutory-paper { background: #ffffff; color: #0f172a; border-radius: var(--radius-md); padding: 36px 40px; box-shadow: 0 10px 25px rgba(0, 0, 0, 0.3); border: 1px solid #cbd5e1; }
    .govt-heading { text-align: center; margin-bottom: 24px; border-bottom: 2px solid #0f172a; padding-bottom: 12px; position: relative; }
    .govt-heading h3 { font-size: 1.15rem; font-weight: 700; margin-bottom: 2px; color: #1e293b; }
    .govt-heading h4 { font-size: 0.95rem; font-weight: 600; color: #475569; margin-bottom: 4px; }
    .form-badge { position: absolute; top: 0; right: 0; border: 2px solid #0f172a; padding: 4px 14px; font-weight: 800; font-size: 0.95rem; border-radius: 4px; color: #0f172a; }
    .register-title { font-size: 1.35rem; font-weight: 800; color: #0f172a; margin: 6px 0 2px 0; }
    .rule-ref { font-size: 0.8rem; color: #64748b; margin: 0; }

    .taxpayer-meta-box { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px 24px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 14px 18px; margin-bottom: 22px; font-size: 0.85rem; color: #1e293b; }
    .meta-row { display: flex; gap: 8px; }
    .meta-label { color: #64748b; font-weight: 500; min-width: 155px; }

    .mushak-grid { width: 100%; border-collapse: collapse; margin-top: 8px; color: #0f172a; font-size: 0.82rem; }
    .mushak-grid th, .mushak-grid td { border: 1px solid #cbd5e1; padding: 8px 10px; }
    .mushak-grid th { background: #f1f5f9; color: #0f172a; font-weight: 700; }
    .mushak-grid tfoot th { background: #f8fafc; color: #0f172a; }

    .transfer-footer-notice { margin-top: 18px; padding: 10px 14px; background: #f8fafc; border-left: 4px solid #3b82f6; font-size: 0.8rem; color: #475569; line-height: 1.5; }
  `]
})
export class Mushak65TransferComponent implements OnInit {
  @Output() requestNewTransfer = new EventEmitter<void>();

  private inventoryApi = inject(InventoryApiService);
  private pdfExport = inject(PdfExportService);
  public context = inject(CompanyContextService);

  public transfers: Mushak65Transfer[] = [];
  public activeTransfer: Mushak65Transfer | null = null;

  ngOnInit(): void {
    this.loadTransfers();
  }

  public loadTransfers(): void {
    this.inventoryApi.getTransfers().subscribe({
      next: (data) => {
        this.transfers = data;
        if (data.length > 0) {
          this.activeTransfer = data[0];
        }
      },
      error: (err) => console.warn('Failed to load transfers:', err)
    });
  }

  public onSelectTransfer(transferNumber: string): void {
    const found = this.transfers.find(t => t.transferNumber === transferNumber);
    if (found) {
      this.activeTransfer = found;
    }
  }

  get activeTransferLines(): any[] {
    if (!this.activeTransfer) return [];
    return (this.activeTransfer as any).items || this.activeTransfer.lines || [];
  }

  get activeTransferTotal(): number {
    return this.activeTransferLines.reduce((sum, it) => sum + (it.totalValue || (it.quantity * it.unitPrice)), 0);
  }

  public downloadPdf(): void {
    const trfNo = this.activeTransfer?.transferNumber || 'Mushak_6_5';
    this.pdfExport.downloadElementAsPdf('mushak-6-5-doc', `Mushak_6_5_Challan_${trfNo}.pdf`);
  }

  public printDocument(): void {
    this.pdfExport.printClean();
  }
}
