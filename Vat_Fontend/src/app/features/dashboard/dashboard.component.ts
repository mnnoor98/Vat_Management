import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CompanyContextService } from '../../core/services/company-context.service';
import { MockDataService } from '../../core/services/mock-data.service';
import { PurchaseApiService } from '../../core/services/api/purchase-api.service';
import { SalesApiService } from '../../core/services/api/sales-api.service';
import { BdtCurrencyPipe } from '../../shared/pipes/bdt-currency.pipe';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { PurchaseInvoice, SalesInvoice } from '../../core/models/vat-models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, BdtCurrencyPipe, StatusBadgeComponent],
  template: `
    <div class="dashboard-page">
      <!-- Page Header -->
      <div class="page-header">
        <div class="header-left">
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'এক্সিকিউটিভ ভ্যাট ড্যাশবোর্ড' : 'Executive VAT Dashboard' }}
          </h1>
          <p class="page-subtitle">
            {{ context.locale() === 'bn' 
              ? 'এনবিআর সংবিধিবদ্ধ মনিটরিং ও কর দায়বদ্ধতা ওভারভিউ — ' + context.periodName()
              : 'NBR Statutory VAT Monitoring & Compliance Overview — ' + context.periodName() }}
          </p>
        </div>
        <div class="header-actions">
          <a routerLink="/app/purchases/new" class="btn btn-secondary btn-sm">
            <span>+</span> {{ context.locale() === 'bn' ? 'নতুন ক্রয়' : 'New Purchase' }}
          </a>
          <a routerLink="/app/sales/new" class="btn btn-secondary btn-sm">
            <span>+</span> {{ context.locale() === 'bn' ? 'বিক্রয় চালান (৬.৩)' : 'New Sales (6.3)' }}
          </a>
          <a routerLink="/app/compliance/return-9-1" class="btn btn-primary btn-sm">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line></svg>
            {{ context.locale() === 'bn' ? 'রিটার্ন ৯.১ ড্রাফট' : 'Return 9.1 Draft' }}
          </a>
        </div>
      </div>

      <!-- KPI Summary Cards Grid -->
      <div class="kpi-grid">
        <!-- Total Sales -->
        <div class="kpi-card sales">
          <div class="kpi-label">{{ context.locale() === 'bn' ? 'মোট বিক্রয় (টার্নওভার)' : 'Total Sales Turnover' }}</div>
          <div class="kpi-value">{{ totalSales | bdtCurrency:true:true }}</div>
          <div class="kpi-meta">
            <span class="kpi-badge-positive">↑ 8.4%</span>
            <span>vs previous month</span>
          </div>
        </div>

        <!-- Output VAT -->
        <div class="kpi-card output-vat">
          <div class="kpi-label">{{ context.locale() === 'bn' ? 'আউটপুট ভ্যাট (প্রদেয় কর)' : 'Output VAT (Gross)' }}</div>
          <div class="kpi-value">{{ outputVat | bdtCurrency:true:true }}</div>
          <div class="kpi-meta">
            <span>Standard 15% rate applied</span>
          </div>
        </div>

        <!-- Input VAT Credit -->
        <div class="kpi-card input-vat">
          <div class="kpi-label">{{ context.locale() === 'bn' ? 'ইনপুট ভ্যাট রেয়াত (দাবিকৃত)' : 'Eligible Input Tax Credit' }}</div>
          <div class="kpi-value">{{ inputVat | bdtCurrency:true:true }}</div>
          <div class="kpi-meta">
            <span class="kpi-badge-positive">Sec 46 Validated</span>
          </div>
        </div>

        <!-- Net VAT Payable -->
        <div class="kpi-card net-payable">
          <div class="kpi-label">{{ context.locale() === 'bn' ? 'নীট কর দায়বদ্ধতা (ট্রেজারি)' : 'Net VAT Payable' }}</div>
          <div class="kpi-value">{{ netPayable | bdtCurrency:true:true }}</div>
          <div class="kpi-meta">
            <span class="due-warning">Due: 15-Nov-2026</span>
          </div>
        </div>
      </div>

      <!-- Split View: Input vs Output Chart & Action Workbench -->
      <div class="dashboard-split">
        <!-- Monthly Tax Trend -->
        <div class="vat-card trend-card">
          <div class="vat-card-header">
            <h2 class="vat-card-title">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"></line><line x1="12" y1="20" x2="12" y2="4"></line><line x1="6" y1="20" x2="6" y2="14"></line></svg>
              {{ context.locale() === 'bn' ? 'ইনপুট বনাম আউটপুট ভ্যাট তুলনা' : 'Input vs Output VAT Trend (2026)' }}
            </h2>
            <span class="period-pill">Jun - Oct 2026</span>
          </div>

          <!-- Simulated CSS Bar Chart -->
          <div class="chart-container">
            <div class="chart-bars">
              <div class="chart-month" *ngFor="let m of monthlyTrend">
                <div class="bar-pair">
                  <div class="bar bar-output" [style.height.%]="(m.output / 2.2) * 100" [title]="'Output: ৳ ' + m.output + 'M'"></div>
                  <div class="bar bar-input" [style.height.%]="(m.input / 2.2) * 100" [title]="'Input: ৳ ' + m.input + 'M'"></div>
                </div>
                <span class="month-label">{{ m.month }}</span>
              </div>
            </div>
            <div class="chart-legend">
              <div class="legend-item"><span class="legend-dot dot-output"></span> Output VAT (প্রদেয় কর)</div>
              <div class="legend-item"><span class="legend-dot dot-input"></span> Input VAT Credit (রেয়াত)</div>
            </div>
          </div>
        </div>

        <!-- Compliance & Action Required Workbench -->
        <div class="vat-card action-card">
          <div class="vat-card-header">
            <h2 class="vat-card-title">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z"></path><path d="m9 12 2 2 4-4"></path></svg>
              {{ context.locale() === 'bn' ? 'জরুরি পদক্ষেপ প্রয়োজন' : 'Action Required & Work Queue' }}
            </h2>
            <span class="badge-amber">{{ mockData.approvalTasks.length }} Tasks</span>
          </div>

          <div class="action-items-list">
            <a routerLink="/app/workflow/my-tasks" class="action-item" *ngFor="let task of mockData.approvalTasks">
              <div class="action-badge-icon">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>
              </div>
              <div class="action-details">
                <span class="action-title">{{ task.documentNumber }} — {{ task.partyName }}</span>
                <span class="action-desc">{{ task.notes }}</span>
              </div>
              <div class="action-right">
                <span class="action-amount">{{ task.amount | bdtCurrency:true:true }}</span>
                <span class="priority-pill" [class.urgent]="task.priority === 'Urgent'">{{ task.priority }}</span>
              </div>
            </a>
          </div>
        </div>
      </div>

      <!-- Recent Transactions Table -->
      <div class="vat-card recent-table-card">
        <div class="vat-card-header">
          <h2 class="vat-card-title">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline></svg>
            {{ context.locale() === 'bn' ? 'সাম্প্রতিক ক্রয় ও বিক্রয় চালান' : 'Recent Purchase & Sales Challans' }}
          </h2>
          <div class="header-links">
            <a routerLink="/app/purchases/invoices" class="table-link">View All Purchases →</a>
            <a routerLink="/app/sales/invoices" class="table-link">View All Sales →</a>
          </div>
        </div>

        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th>Type</th>
                <th>Doc / Challan No</th>
                <th>Party & BIN</th>
                <th>Date</th>
                <th class="text-right">Taxable BDT</th>
                <th class="text-right">VAT BDT</th>
                <th>Status</th>
                <th class="text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of recentPurchases">
                <td><span class="type-tag purchase">PURCHASE</span></td>
                <td><strong class="font-mono">{{ p.invoiceNumber }}</strong></td>
                <td>{{ p.supplierName }} <br><small class="text-dim">BIN: {{ p.supplierBin }}</small></td>
                <td>{{ p.invoiceDate }}</td>
                <td class="text-right numeric">{{ p.subTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric text-emerald">{{ p.totalVat | bdtCurrency:false }}</td>
                <td><app-status-badge [status]="p.status"></app-status-badge></td>
                <td class="text-right">
                  <a routerLink="/app/purchases/invoices" class="btn-table-action">View</a>
                </td>
              </tr>
              <tr *ngFor="let s of recentSales">
                <td><span class="type-tag sales">SALES 6.3</span></td>
                <td><strong class="font-mono">{{ s.challanNumber63 }}</strong></td>
                <td>{{ s.customerName }} <br><small class="text-dim">BIN: {{ s.customerBin }}</small></td>
                <td>{{ s.deliveryDate }}</td>
                <td class="text-right numeric">{{ s.subTotal | bdtCurrency:false }}</td>
                <td class="text-right numeric text-rose">{{ s.totalVat | bdtCurrency:false }}</td>
                <td><app-status-badge [status]="s.status"></app-status-badge></td>
                <td class="text-right">
                  <a routerLink="/app/sales/challan-preview" class="btn-table-action print-btn">Print 6.3</a>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-page {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 16px;
    }

    .page-title {
      font-size: 1.6rem;
      font-weight: 800;
      color: var(--text-main);
    }

    .page-subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-top: 3px;
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .due-warning {
      color: var(--vat-amber);
      font-weight: 700;
    }

    .dashboard-split {
      display: grid;
      grid-template-columns: 1.3fr 1fr;
      gap: 24px;
    }

    @media (max-width: 1024px) {
      .dashboard-split {
        grid-template-columns: 1fr;
      }
    }

    .chart-container {
      padding-top: 20px;
    }

    .chart-bars {
      display: flex;
      justify-content: space-around;
      align-items: flex-end;
      height: 180px;
      border-bottom: 1px solid var(--border-card);
      padding-bottom: 8px;
    }

    .chart-month {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      height: 100%;
      justify-content: flex-end;
    }

    .bar-pair {
      display: flex;
      align-items: flex-end;
      gap: 6px;
      height: 140px;
    }

    .bar {
      width: 18px;
      border-radius: 4px 4px 0 0;
      transition: height 0.4s ease;
    }

    .bar-output {
      background: linear-gradient(180deg, #f43f5e 0%, #be123c 100%);
    }

    .bar-input {
      background: linear-gradient(180deg, #10b981 0%, #047857 100%);
    }

    .month-label {
      font-size: 0.75rem;
      color: var(--text-muted);
      font-weight: 600;
    }

    .chart-legend {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 24px;
      margin-top: 14px;
      font-size: 0.78rem;
      color: var(--text-muted);
    }

    .legend-item {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .legend-dot {
      width: 10px;
      height: 10px;
      border-radius: 2px;
    }

    .dot-output { background: #f43f5e; }
    .dot-input { background: #10b981; }

    .action-items-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .action-item {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px;
      background: var(--bg-card-subtle);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      text-decoration: none;
      color: var(--text-main);
      transition: all 0.2s ease;
    }

    .action-item:hover {
      background: var(--bg-card-hover);
      border-color: var(--border-subtle);
      transform: translateX(2px);
    }

    .action-badge-icon {
      width: 34px;
      height: 34px;
      border-radius: var(--radius-sm);
      background: rgba(245, 158, 11, 0.15);
      color: var(--vat-amber);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .action-details {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .action-title {
      font-size: 0.82rem;
      font-weight: 700;
      color: var(--text-main);
    }

    .action-desc {
      font-size: 0.72rem;
      color: var(--text-dim);
      display: -webkit-box;
      -webkit-line-clamp: 1;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .action-right {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 4px;
    }

    .action-amount {
      font-family: var(--font-mono);
      font-size: 0.8rem;
      font-weight: 700;
    }

    .priority-pill {
      font-size: 0.65rem;
      padding: 1px 6px;
      border-radius: 9999px;
      background: var(--bg-card);
      color: var(--text-muted);
      font-weight: 700;
    }

    .priority-pill.urgent {
      background: rgba(244, 63, 94, 0.15);
      color: var(--vat-rose);
    }

    .badge-amber {
      background: rgba(245, 158, 11, 0.15);
      color: var(--vat-amber);
      border: 1px solid rgba(245, 158, 11, 0.3);
      padding: 3px 8px;
      border-radius: 9999px;
      font-size: 0.72rem;
      font-weight: 700;
    }

    .type-tag {
      font-size: 0.68rem;
      font-weight: 800;
      padding: 2px 6px;
      border-radius: var(--radius-sm);
    }

    .type-tag.purchase {
      background: rgba(16, 185, 129, 0.15);
      color: var(--vat-emerald);
    }

    .type-tag.sales {
      background: rgba(59, 130, 246, 0.15);
      color: #60a5fa;
    }

    .font-mono {
      font-family: var(--font-mono);
    }

    .text-emerald { color: #34d399; }
    .text-rose { color: #fb7185; }

    .header-links {
      display: flex;
      gap: 16px;
    }

    .table-link {
      font-size: 0.78rem;
      color: var(--vat-teal);
      text-decoration: none;
      font-weight: 600;
    }
    .table-link:hover { text-decoration: underline; }

    .btn-table-action {
      font-size: 0.75rem;
      padding: 4px 10px;
      background: var(--bg-card-subtle);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-sm);
      color: var(--text-main);
      text-decoration: none;
      font-weight: 600;
      transition: all 0.15s ease;
    }
    .btn-table-action:hover {
      background: var(--bg-card-hover);
      border-color: var(--border-subtle);
    }
    .btn-table-action.print-btn {
      color: var(--vat-emerald);
      border-color: rgba(16, 185, 129, 0.3);
    }
  `]
})
export class DashboardComponent implements OnInit {
  public totalSales = 12000000;
  public outputVat = 1800000;
  public inputVat = 1177500;
  public netPayable = 592500;

  public recentPurchases: PurchaseInvoice[] = [];
  public recentSales: SalesInvoice[] = [];

  public monthlyTrend = [
    { month: 'Jun', output: 1.5, input: 0.95 },
    { month: 'Jul', output: 1.6, input: 1.02 },
    { month: 'Aug', output: 1.45, input: 0.90 },
    { month: 'Sep', output: 1.75, input: 1.10 },
    { month: 'Oct', output: 1.80, input: 1.18 }
  ];

  constructor(
    public context: CompanyContextService,
    public mockData: MockDataService,
    private purchaseApi: PurchaseApiService,
    private salesApi: SalesApiService
  ) {}

  ngOnInit(): void {
    this.purchaseApi.getInvoices().subscribe(invoices => {
      if (invoices && invoices.length > 0) {
        // Compute input VAT from live invoices
        const liveInputVat = invoices.reduce((acc, inv) => acc + (inv.totalVat || 0), 0);
        if (liveInputVat > 0) {
          this.inputVat = liveInputVat;
          this.recomputeNet();
        }
        // Merge with mock historical samples if live list is small so tables look rich
        const combined = [...invoices];
        if (combined.length < 3) {
          this.mockData.purchaseInvoices.forEach(m => {
            if (!combined.some(c => c.invoiceNumber === m.invoiceNumber || c.id === m.id)) {
              combined.push(m);
            }
          });
        }
        this.recentPurchases = combined.slice(0, 4);
      } else {
        this.recentPurchases = this.mockData.purchaseInvoices.slice(0, 4);
      }
    });

    this.salesApi.getInvoices().subscribe(invoices => {
      if (invoices && invoices.length > 0) {
        const liveSales = invoices.reduce((acc, inv) => acc + (inv.subTotal || 0), 0);
        const liveOutputVat = invoices.reduce((acc, inv) => acc + (inv.totalVat || 0), 0);
        if (liveSales > 0) {
          this.totalSales = liveSales;
          this.outputVat = liveOutputVat;
          this.recomputeNet();
        }
        const combined = [...invoices];
        if (combined.length < 3) {
          this.mockData.salesInvoices.forEach(m => {
            if (!combined.some(c => c.invoiceNumber === m.invoiceNumber || c.id === m.id)) {
              combined.push(m);
            }
          });
        }
        this.recentSales = combined.slice(0, 4);
      } else {
        this.recentSales = this.mockData.salesInvoices.slice(0, 4);
      }
    });
  }

  private recomputeNet(): void {
    this.netPayable = Math.max(0, this.outputVat - this.inputVat);
    // Update October visual trend bar
    const octTrend = this.monthlyTrend.find(m => m.month === 'Oct');
    if (octTrend) {
      octTrend.output = Math.round((this.outputVat / 1000000) * 100) / 100;
      octTrend.input = Math.round((this.inputVat / 1000000) * 100) / 100;
    }
  }
}
