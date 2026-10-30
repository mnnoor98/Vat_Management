import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { CompanyContextService } from '../../../core/services/company-context.service';
import { MockDataService } from '../../../core/services/mock-data.service';
import { SalesApiService } from '../../../core/services/api/sales-api.service';
import { BdtCurrencyPipe } from '../../../shared/pipes/bdt-currency.pipe';
import { BengaliNumberPipe } from '../../../shared/pipes/bengali-number.pipe';
import { SalesInvoice } from '../../../core/models/vat-models';
import { PdfExportService } from '../../../core/services/pdf-export.service';

@Component({
  selector: 'app-mushak-6-3',
  standalone: true,
  imports: [CommonModule, RouterModule, BdtCurrencyPipe, BengaliNumberPipe],
  template: `
    <div class="mushak-challan-page">
      <!-- Toolbar (Hidden in Print) -->
      <div class="page-header no-print">
        <div>
          <div class="breadcrumb-trail">
            <a routerLink="/app/sales/invoices">Sales</a> / <span>Mushak 6.3 Tax Invoice</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'মূসক-৬.৩: কর চালানপত্র' : 'Mushak 6.3: Tax Invoice / VAT Challan' }}
          </h1>
          <p class="page-subtitle">
            [বিধি ৪০ এর উপ-বিধি (১) এর দফা (গ) ও দফা (চ) দ্রষ্টব্য] — পণ্য খালাস ও সরবরাহের সংবিধিবদ্ধ কর চালান
          </p>
        </div>

        <div class="header-actions">
          <div class="copy-selector">
            <button class="btn btn-secondary btn-sm" [class.active-copy]="activeCopy === 'Original'" (click)="activeCopy = 'Original'">
              ১ম কপি (ক্রেতার জন্য)
            </button>
            <button class="btn btn-secondary btn-sm" [class.active-copy]="activeCopy === 'Duplicate'" (click)="activeCopy = 'Duplicate'">
              ২য় কপি (বিক্রেতার হিসাব)
            </button>
          </div>
          <button class="btn btn-primary" (click)="downloadPdf()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            Download PDF (ডাউনলোড চালান)
          </button>
          <button class="btn btn-secondary" (click)="printChallan()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"></polyline><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"></path><rect x="6" y="14" width="12" height="8"></rect></svg>
            Print (প্রিন্ট চালান)
          </button>
        </div>
      </div>

      <!-- Statutory Printable 6.3 Tax Invoice Shell -->
      <div class="challan-document" id="mushak-6-3-doc" *ngIf="invoice; else loadingTpl">
        <!-- Top Copy Indicator -->
        <div class="copy-watermark">
          {{ activeCopy === 'Original' ? 'মূল কপি (১ম কপি — ক্রেতার জন্য)' : 'অনুলিপি (২য় কপি — বিক্রেতার নিজস্ব ভ্যাট নথি)' }}
        </div>

        <!-- Official Header -->
        <div class="challan-header">
          <h3 class="font-bengali">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</h3>
          <h4 class="font-bengali">জাতীয় রাজস্ব বোর্ড</h4>
          <div class="mushak-badge font-bengali">মূসক-৬.৩</div>
          <h2 class="challan-title font-bengali">কর চালানপত্র</h2>
          <p class="rule-ref font-bengali">[বিধি ৪০ এর উপ-বিধি (১) এর দফা (গ) ও দফা (চ) দ্রষ্টব্য]</p>
        </div>

        <!-- Registered Supplier & Buyer Details Grid -->
        <div class="parties-grid">
          <!-- Supplier Details (Left) -->
          <div class="party-box supplier-box">
            <div class="box-title font-bengali">নিবন্ধিত ব্যক্তির বিবরণ (সরবরাহকারী):</div>
            <div class="meta-line">
              <span class="label font-bengali">নাম:</span>
              <strong class="font-bengali">{{ context.activeCompany().nameBn }}</strong>
            </div>
            <div class="meta-line">
              <span class="label">BIN (বিআইএন):</span>
              <strong class="font-mono text-emerald">{{ context.activeCompany().bin }}</strong>
            </div>
            <div class="meta-line">
              <span class="label font-bengali">ঠিকানা:</span>
              <span class="font-bengali">{{ context.activeCompany().addressBn }}</span>
            </div>
            <div class="meta-line">
              <span class="label font-bengali">চালান ইস্যুর ঠিকানা:</span>
              <span class="font-bengali">{{ context.activeBranch().address }}</span>
            </div>
          </div>

          <!-- Challan & Buyer Metadata (Right) -->
          <div class="party-box buyer-box">
            <div class="meta-line highlight-challan">
              <span class="label font-bengali">চালানপত্র নম্বর:</span>
              <strong class="font-mono challan-no">{{ invoice.challanNumber63 }}</strong>
            </div>
            <div class="meta-line">
              <span class="label font-bengali">ইস্যুর তারিখ ও সময়:</span>
              <strong class="font-mono">{{ invoice.deliveryDate }} | {{ invoice.deliveryTime }}</strong>
            </div>
            <div class="box-divider"></div>
            <div class="box-title font-bengali">ক্রেতার বিবরণ:</div>
            <div class="meta-line">
              <span class="label font-bengali">নাম:</span>
              <strong class="font-bengali">{{ invoice.customerNameBn }}</strong>
            </div>
            <div class="meta-line">
              <span class="label">BIN (বিআইএন):</span>
              <strong class="font-mono">{{ invoice.customerBin }}</strong>
            </div>
            <div class="meta-line">
              <span class="label font-bengali">গন্তব্যস্থল:</span>
              <span>{{ invoice.destinationAddress }}</span>
            </div>
            <div class="meta-line">
              <span class="label font-bengali">যানবাহনের প্রকৃতি ও নম্বর:</span>
              <strong>{{ invoice.vehicleTypeAndNo }}</strong>
            </div>
          </div>
        </div>

        <!-- Line Items Table -->
        <div class="vat-table-container challan-table-wrapper">
          <table class="vat-table challan-table">
            <thead>
              <tr>
                <th class="text-center font-bengali">ক্রমিক</th>
                <th class="font-bengali">পণ্য বা সেবার বর্ণনা</th>
                <th class="font-bengali">এইচএস কোড</th>
                <th class="font-bengali">সরবরাহের একক</th>
                <th class="text-right font-bengali">পরিমাণ</th>
                <th class="text-right font-bengali">একক মূল্য (টাকা)</th>
                <th class="text-right font-bengali">মোট মূল্য (টাকা)</th>
                <th class="text-right font-bengali">সম্পূরক শুল্ক</th>
                <th class="text-center font-bengali">মূসক হার</th>
                <th class="text-right font-bengali">মূসক/কর (টাকা)</th>
                <th class="text-right font-bengali">সর্বমোট মূল্য (টাকা)</th>
              </tr>
              <tr class="col-numbers font-bengali">
                <th>(১)</th><th>(২)</th><th>(৩)</th><th>(৪)</th><th>(৫)</th><th>(৬)</th><th>(৭)</th><th>(৮)</th><th>(৯)</th><th>(১০)</th><th>(১১)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let l of invoice.lines; let i = index">
                <td class="text-center font-bengali">{{ (i + 1) | bengaliNumber }}</td>
                <td>
                  <strong class="font-bengali">{{ l.productNameBn }}</strong>
                  <br>
                  <small class="text-dim">{{ l.productName }}</small>
                </td>
                <td class="font-mono text-center">{{ l.hsCode }}</td>
                <td class="text-center">{{ l.uom }}</td>
                <td class="text-right numeric font-bold">{{ l.quantity | number }}</td>
                <td class="text-right numeric">{{ l.unitPrice | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ l.taxableAmount | bdtCurrency:false }}</td>
                <td class="text-right numeric">০.০০</td>
                <td class="text-center font-bold text-emerald">১৫%</td>
                <td class="text-right numeric font-bold text-rose">{{ l.vatAmount | bdtCurrency:false }}</td>
                <td class="text-right numeric font-bold">{{ l.totalAmount | bdtCurrency:false }}</td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="total-row">
                <th colspan="6" class="text-right font-bengali">মোট (সর্বমোট টাকা):</th>
                <th class="text-right numeric font-bold">{{ invoice.subTotal | bdtCurrency:false }}</th>
                <th class="text-right numeric">০.০০</th>
                <th></th>
                <th class="text-right numeric font-bold text-rose">{{ invoice.totalVat | bdtCurrency:false }}</th>
                <th class="text-right numeric grand-total-cell">{{ invoice.grandTotal | bdtCurrency:false }}</th>
              </tr>
            </tfoot>
          </table>
        </div>

        <!-- In Words & Footer Details -->
        <div class="in-words-box">
          <span class="font-bengali label">কথায় (In Words):</span>
          <strong class="font-bengali words-text">{{ getInWords(invoice.grandTotal) }}</strong>
        </div>

        <!-- Bottom Certification & Signatures with QR Code -->
        <div class="challan-footer">
          <div class="qr-box">
            <!-- Simulated QR Code SVG -->
            <svg width="85" height="85" viewBox="0 0 100 100" class="qr-svg">
              <rect x="0" y="0" width="100" height="100" fill="#ffffff" stroke="#000000" stroke-width="2"/>
              <rect x="10" y="10" width="25" height="25" fill="#000000"/>
              <rect x="65" y="10" width="25" height="25" fill="#000000"/>
              <rect x="10" y="65" width="25" height="25" fill="#000000"/>
              <rect x="15" y="15" width="15" height="15" fill="#ffffff"/>
              <rect x="70" y="15" width="15" height="15" fill="#ffffff"/>
              <rect x="15" y="70" width="15" height="15" fill="#ffffff"/>
              <rect x="42" y="12" width="16" height="16" fill="#000000"/>
              <rect x="42" y="42" width="16" height="16" fill="#000000"/>
              <rect x="12" y="42" width="16" height="16" fill="#000000"/>
              <rect x="72" y="42" width="16" height="16" fill="#000000"/>
              <rect x="42" y="72" width="16" height="16" fill="#000000"/>
              <rect x="72" y="72" width="16" height="16" fill="#000000"/>
            </svg>
            <span class="qr-caption font-bengali">এনবিআর ভেরিফিকেশন কিউআর</span>
          </div>

          <div class="terms-note">
            <p class="font-bengali">* পণ্য সরবরাহের পূর্বেই এই কর চালানপত্র ইস্যু করা হইয়াছে।</p>
            <p class="font-bengali">* সংবিধিবদ্ধ বিধিমালা অনুযায়ী এই চালানপত্রের অনুলিপি ৫ বছর সংরক্ষণযোগ্য।</p>
          </div>

          <div class="auth-signature">
            <div class="sig-space"></div>
            <div class="sig-title font-bengali">দায়িত্বপ্রাপ্ত কর্মকর্তার স্বাক্ষর ও নামযুক্ত সিল</div>
            <div class="sig-officer">Tariqul Islam, FCA (Authorized Signatory)</div>
          </div>
        </div>
      </div>

      <ng-template #loadingTpl>
        <div class="vat-card text-center" style="padding: 40px; text-align: center;">
          <p class="font-bengali" style="color: var(--vat-teal); font-size: 1.1rem; font-weight: 700;">মূসক ৬.৩ চালান লোড হচ্ছে...</p>
        </div>
      </ng-template>
    </div>
  `,
  styles: [`
    .mushak-challan-page {
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

    .copy-selector {
      display: flex;
      gap: 6px;
      background: var(--bg-card);
      padding: 4px;
      border-radius: var(--radius-md);
      border: 1px solid var(--border-card);
    }

    .copy-selector button {
      border: none;
    }

    .active-copy {
      background: var(--vat-emerald) !important;
      color: #ffffff !important;
      font-weight: 700;
    }

    .challan-document {
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-lg);
      padding: 35px 40px;
      color: var(--text-main);
      position: relative;
    }

    .copy-watermark {
      position: absolute;
      top: 16px;
      right: 24px;
      font-size: 0.75rem;
      font-weight: 700;
      color: var(--vat-teal);
      border: 1px dashed var(--vat-teal);
      padding: 3px 10px;
      border-radius: var(--radius-sm);
    }

    .challan-header {
      text-align: center;
      margin-bottom: 22px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
    }

    .challan-header h3 { font-size: 1.15rem; }
    .challan-header h4 { font-size: 0.95rem; color: var(--text-muted); }

    .mushak-badge {
      display: inline-block;
      border: 2px solid var(--vat-emerald);
      color: var(--vat-emerald);
      padding: 2px 16px;
      font-weight: 800;
      font-size: 1.1rem;
      border-radius: var(--radius-sm);
      margin: 6px 0;
    }

    .challan-title {
      font-size: 1.45rem;
      font-weight: 800;
      color: var(--text-main);
    }

    .rule-ref {
      font-size: 0.75rem;
      color: var(--text-dim);
    }

    .parties-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 20px;
      margin-bottom: 24px;
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 16px;
      background: var(--bg-card-subtle);
    }

    .party-box {
      display: flex;
      flex-direction: column;
      gap: 6px;
      font-size: 0.85rem;
    }

    .box-title {
      font-weight: 800;
      color: var(--vat-teal);
      font-size: 0.85rem;
      text-transform: uppercase;
      margin-bottom: 4px;
    }

    .meta-line {
      display: flex;
      gap: 8px;
    }

    .meta-line .label {
      color: var(--text-dim);
      min-width: 130px;
      font-weight: 600;
    }

    .highlight-challan {
      background: rgba(16, 185, 129, 0.1);
      padding: 4px 8px;
      border-radius: var(--radius-sm);
      border: 1px solid rgba(16, 185, 129, 0.2);
    }

    .challan-no {
      color: var(--vat-emerald);
      font-size: 0.95rem;
    }

    .box-divider {
      height: 1px;
      background: var(--border-card);
      margin: 4px 0;
    }

    .challan-table th {
      font-size: 0.72rem;
      padding: 8px 6px;
      text-align: center;
      vertical-align: middle;
    }

    .col-numbers th {
      font-size: 0.65rem;
      background: var(--bg-card);
      color: var(--text-dim);
      padding: 2px;
    }

    .challan-table td {
      font-size: 0.8rem;
      padding: 10px 8px;
    }

    .total-row th {
      background: var(--bg-card-subtle);
      font-size: 0.85rem;
      padding: 10px 8px;
    }

    .grand-total-cell {
      color: var(--vat-emerald);
      font-size: 0.95rem !important;
    }

    .in-words-box {
      border: 1px solid var(--border-card);
      border-radius: var(--radius-sm);
      padding: 10px 16px;
      margin-top: 16px;
      font-size: 0.85rem;
      display: flex;
      gap: 10px;
      background: var(--bg-card-subtle);
    }

    .words-text { color: var(--vat-teal); }

    .challan-footer {
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      margin-top: 40px;
      padding-top: 20px;
      border-top: 1px solid var(--border-card);
    }

    .qr-box {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
    }

    .qr-svg {
      border-radius: 4px;
      background: #ffffff;
      padding: 4px;
    }

    .qr-caption {
      font-size: 0.68rem;
      color: var(--text-dim);
    }

    .terms-note {
      font-size: 0.72rem;
      color: var(--text-dim);
      line-height: 1.6;
    }

    .auth-signature {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
    }

    .sig-space {
      width: 220px;
      border-top: 1px dashed var(--border-subtle);
      margin-bottom: 4px;
    }

    .sig-title {
      font-size: 0.78rem;
      color: var(--text-muted);
      font-weight: 700;
    }

    .sig-officer {
      font-size: 0.7rem;
      color: var(--text-dim);
    }

    @media print {
      @page {
        size: A4 portrait;
        margin: 8mm 10mm;
      }
      .mushak-challan-page {
        gap: 0 !important;
      }
      .no-print {
        display: none !important;
      }
      .challan-document {
        background: #ffffff !important;
        color: #000000 !important;
        border: 1px solid #000000 !important;
        padding: 10mm !important;
        box-shadow: none !important;
        page-break-inside: avoid !important;
      }
      .parties-grid, .in-words-box, .challan-footer {
        background: #ffffff !important;
        border-color: #000000 !important;
        color: #000000 !important;
      }
      .challan-table {
        border: 1px solid #000000 !important;
      }
      .challan-table th, .challan-table td {
        border: 1px solid #000000 !important;
        color: #000000 !important;
      }
      .copy-watermark {
        color: #000000 !important;
        border: 1px solid #000000 !important;
      }
      .mushak-badge {
        color: #000000 !important;
        border: 2px solid #000000 !important;
      }
      .sig-space {
        border-color: #000000 !important;
      }
    }
  `]
})
export class Mushak63ChallanComponent implements OnInit {
  public activeCopy: 'Original' | 'Duplicate' = 'Original';
  public invoice!: SalesInvoice;
  public isLoading: boolean = true;

  constructor(
    public context: CompanyContextService,
    public mockData: MockDataService,
    private salesApi: SalesApiService,
    private route: ActivatedRoute,
    private pdfExport: PdfExportService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const id = params['id'];
      if (id) {
        this.salesApi.getInvoiceById(id).subscribe({
          next: (inv) => {
            if (inv) this.invoice = inv;
            else this.loadFirstInvoice();
            this.isLoading = false;
          },
          error: () => {
            this.loadFirstInvoice();
            this.isLoading = false;
          }
        });
      } else {
        this.loadFirstInvoice();
      }
    });
  }

  private loadFirstInvoice(): void {
    this.salesApi.getInvoices().subscribe({
      next: (invoices) => {
        if (invoices && invoices.length > 0) {
          this.invoice = invoices[0];
        } else {
          this.invoice = this.mockData.salesInvoices[0];
        }
        this.isLoading = false;
      },
      error: () => {
        this.invoice = this.mockData.salesInvoices[0];
        this.isLoading = false;
      }
    });
  }

  public getInWords(amount: number): string {
    if (!amount || amount <= 0) return 'শূন্য টাকা মাত্র।';
    const rounded = Math.floor(amount);
    return `${this.convertNumberToBengaliWords(rounded)} টাকা মাত্র।`;
  }

  private convertNumberToBengaliWords(n: number): string {
    if (n === 0) return 'শূন্য';
    const ones = [
      '', 'এক', 'দুই', 'তিন', 'চার', 'পাঁচ', 'ছয়', 'সাত', 'আট', 'নয়', 'দশ',
      'এগারো', 'বারো', 'তেরো', 'চৌদ্দ', 'পনেরো', 'ষোলো', 'সতেরো', 'আঠারো', 'উনিশ', 'বিশ',
      'একুশ', 'বাইশ', 'তেইশ', 'চব্বিশ', 'পঁচিশ', 'ছাব্বিশ', 'সাতাশ', 'আটাশ', 'ঊনত্রিশ', 'ত্রিশ',
      'একত্রিশ', 'বত্রিশ', 'তেত্রিশ', 'চৌত্রিশ', 'পঁয়ত্রিশ', 'ছত্রিশ', 'সাঁইত্রিশ', 'আটত্রিশ', 'ঊনচল্লিশ', 'চল্লিশ',
      'একচল্লিশ', 'বিয়াল্লিশ', 'তেতাল্লিশ', 'চুয়াল্লিশ', 'পঁয়তাল্লিশ', 'ছেচল্লিশ', 'সাতচল্লিশ', 'আটচল্লিশ', 'ঊনপঞ্চাশ', 'পঞ্চাশ',
      'একান্ন', 'বায়ান্ন', 'তিপ্পান্ন', 'চুয়ান্ন', 'পঞ্চান্ন', 'ছাপ্পান্ন', 'সাতান্ন', 'আটান্ন', 'ঊনষাট', 'ষাট',
      'একষট্টি', 'বাষট্টি', 'তেষট্টি', 'চৌষট্টি', 'পঁয়ষট্টি', 'ছেষট্টি', 'সাতষট্টি', 'আটষট্টি', 'ঊনসত্তর', 'সত্তর',
      'একাত্তর', 'বাহাত্তর', 'তিয়াত্তর', 'চুয়াত্তর', 'পঁচাত্তর', 'ছিয়াত্তর', 'সাতাত্তর', 'আটাত্তর', 'ঊনআশি', 'আশি',
      'একাশি', 'বিরাশি', 'তিরাশি', 'চুরাশি', 'পঁচাশি', 'ছিয়াশি', 'সাতাশি', 'আটাশি', 'ঊননব্বই', 'নব্বই',
      'একানব্বই', 'বানব্বই', 'তিরানব্বই', 'চুরানব্বই', 'পঁচানব্বই', 'ছিয়ানব্বই', 'সাতানব্বই', 'আটানব্বই', 'নিরানব্বই'
    ];

    let result = '';
    const crore = Math.floor(n / 10000000);
    n %= 10000000;
    const lakh = Math.floor(n / 100000);
    n %= 100000;
    const thousand = Math.floor(n / 1000);
    n %= 1000;
    const hundred = Math.floor(n / 100);
    n %= 100;

    if (crore > 0) {
      result += this.convertNumberToBengaliWords(crore) + ' কোটি ';
    }
    if (lakh > 0) {
      result += (ones[lakh] || lakh.toString()) + ' লক্ষ ';
    }
    if (thousand > 0) {
      result += (ones[thousand] || thousand.toString()) + ' হাজার ';
    }
    if (hundred > 0) {
      result += (ones[hundred] || hundred.toString()) + ' শত ';
    }
    if (n > 0) {
      result += (ones[n] || n.toString()) + ' ';
    }
    return result.trim();
  }

  downloadPdf(): void {
    if (!this.invoice) return;
    const filename = `Mushak_6_3_Challan_${this.invoice.challanNumber63}_${this.activeCopy}`;
    this.pdfExport.downloadElementAsPdf('mushak-6-3-doc', filename, 'portrait');
  }

  printChallan(): void {
    this.pdfExport.printClean();
  }
}
