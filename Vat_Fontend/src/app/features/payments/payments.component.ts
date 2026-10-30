import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable, Subscription, forkJoin } from 'rxjs';
import { PaymentsApiService, Receipt, ReceiptDocument, ReceiptCorrection, Receivables, Statement, Ageing } from '../../core/services/api/payments-api.service';
import { MasterDataApiService } from '../../core/services/api/master-data-api.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { CsvExportService } from '../../core/services/csv-export.service';
import { BusinessParty } from '../../core/models/vat-models';
type Action = 'create' | 'post' | 'cancel' | 'allocate' | 'refund' | 'reverse' | 'approveCorrection' | 'cancelCorrection';
@Component({ selector: 'app-payments', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="vat-card">
    <h1>Payments and customer receivables</h1>
    <p>Pending receipt ? approve and post. Allocation, refund and reversal are separate actions. Requests do not move cash until approved. Period, credit and approval rules apply.</p>
    <label>Customer<select class="vat-select" [(ngModel)]="customerId" [disabled]="busy()" (ngModelChange)="selectCustomer()"><option value="">Select customer</option><option *ngFor="let c of customers()" [value]="c.id">{{ c.code }} ? {{ c.name }}</option></select></label>
    <button class="btn btn-secondary" [disabled]="busy() || customersLoading()" (click)="loadCustomers(true)">Reload customers</button>
    <button class="btn btn-secondary" *ngIf="moreCustomers()" [disabled]="busy() || customersLoading()" (click)="loadCustomers()">Load more customers</button>
    <p role="alert" *ngIf="error()">{{ error() }}</p><p role="status" *ngIf="message()">{{ message() }}</p>
    <ng-container *ngIf="scopeValid()">
      <button class="btn btn-secondary" [disabled]="busy() || listLoading()" (click)="loadPayments()">Reload receipts</button>
      <p *ngIf="listLoading() || detailLoading()">Loading receipts?</p>
      <table class="vat-table"><thead><tr><th>Reference / date</th><th>Amount</th><th>State / version</th><th>Review</th></tr></thead><tbody><tr *ngFor="let p of payments()"><td>{{ p.reference }} / {{ p.paymentDate }}</td><td>{{ p.amount | number:'1.2-4' }} {{ p.currency }}</td><td>{{ p.status }} / {{ p.version }}</td><td><button class="btn btn-secondary" [disabled]="busy() || detailLoading()" (click)="review(p.id)">Review</button></td></tr></tbody></table>
      <button class="btn btn-secondary" [disabled]="busy() || listLoading() || page() === 0" (click)="changePage(-1)">Previous</button><span>Receipt page {{ page() + 1 }}</span><button class="btn btn-secondary" [disabled]="busy() || listLoading() || !morePayments()" (click)="changePage(1)">Next</button>
      <form *ngIf="permission('payment.request')" #createForm="ngForm" (ngSubmit)="stage('create')">
        <h2>Request receipt</h2><label>Reference<input class="vat-input" name="reference" [(ngModel)]="draft.reference" required maxlength="100"></label>
        <label>Method<select class="vat-select" name="method" [(ngModel)]="draft.method"><option>Cash</option><option>Bank</option></select></label>
        <label>Evidence reference<input class="vat-input" name="evidence" [(ngModel)]="draft.evidenceReference" required maxlength="1000"></label>
        <label>Payment date<input class="vat-input" type="date" name="date" [(ngModel)]="draft.paymentDate" required></label>
        <label>Amount<input class="vat-input" type="number" name="amount" [(ngModel)]="draft.amount" min="0.0001" step="0.0001" required></label>
        <button class="btn btn-primary" [disabled]="busy() || createForm.invalid">Review receipt request</button>
      </form>
      <ng-container *ngIf="detailReady()"><ng-container *ngIf="receipt() as r">
        <h2>{{ r.payment.reference }} ? {{ r.payment.status }} / version {{ r.payment.version }}</h2>
        <p>Requester: {{ r.payment.requestedBy }} ? Approver: {{ r.payment.approvedBy || 'Pending' }} ? Evidence: {{ r.payment.evidenceReference }}</p>
        <p>Collected net: {{ r.netCollectedAmount | number:'1.2-4' }} ? Returned: {{ r.returnedAmount | number:'1.2-4' }} ? Allocated net: {{ r.allocatedAmount | number:'1.2-4' }} ? Unallocated: {{ r.unallocatedAmount | number:'1.2-4' }} BDT</p>
        <p *ngIf="reloadRequired()" role="alert">Reload this receipt before another command.</p>
        <button class="btn btn-secondary" [disabled]="busy()" (click)="review(r.payment.id)">Reload receipt</button>
        <button class="btn btn-primary" *ngIf="allowed('post')" [disabled]="busy()" (click)="stage('post')">Review approval and posting</button>
        <label *ngIf="allowed('cancel')">Cancellation reason<input class="vat-input" [(ngModel)]="reason" maxlength="1000"></label>
        <button class="btn btn-secondary" *ngIf="allowed('cancel')" [disabled]="busy()" (click)="stage('cancel')">Cancel pending receipt</button>
        <ng-container *ngIf="allowed('allocate')">
          <h3>Allocate posted receipt</h3><label>Invoice<select class="vat-select" [(ngModel)]="invoiceId"><option value="">Select outstanding invoice</option><option *ngFor="let i of account()?.invoices" [value]="i.invoiceId" [disabled]="i.outstandingAmount <= 0">{{ i.reference }} ? Outstanding {{ i.outstandingAmount }}</option></select></label>
          <label>Allocation date<input class="vat-input" type="date" [(ngModel)]="allocationDate"></label><label>Amount<input class="vat-input" type="number" [(ngModel)]="allocationAmount" min="0.0001" step="0.0001"></label>
          <button class="btn btn-primary" [disabled]="busy()" (click)="stage('allocate')">Review allocation</button>
        </ng-container>
        <ng-container *ngIf="r.payment.status === 'Pending' && r.payment.requestedAllocations.length"><h3>Allocations included in this pending receipt</h3><p>Approving this receipt also posts these requested invoice allocations.</p><table class="vat-table"><thead><tr><th>Invoice ID</th><th>Requested allocation</th></tr></thead><tbody><tr *ngFor="let a of r.payment.requestedAllocations"><td>{{ a.invoiceId }}</td><td>{{ a.amount }}</td></tr></tbody></table></ng-container>
        <h3>Allocations and releases</h3><table class="vat-table"><thead><tr><th>Allocation / invoice</th><th>Allocated</th><th>Released</th><th>Refund release amount</th></tr></thead><tbody><tr *ngFor="let a of r.allocations"><td>{{ a.id }}<br>{{ a.invoiceId }}</td><td>{{ a.amount }}</td><td>{{ released(a.id) }}</td><td><input class="vat-input" type="number" [(ngModel)]="releaseAmounts[a.id]" min="0" [max]="a.amount - released(a.id)" step="0.0001" [disabled]="!allowed('refund') || busy()"></td></tr></tbody></table>
        <ng-container *ngIf="allowed('refund') || allowed('reverse')">
          <h3>Request correction</h3><p>Reversal returns the remaining receipt and releases its remaining allocations. Refund uses the amount and selected allocation releases below. Server checks available customer/invoice credit on both current and correction dates.</p>
          <label>Correction date<input class="vat-input" type="date" [(ngModel)]="correctionDate"></label><label>Reason<textarea class="vat-input" [(ngModel)]="reason" maxlength="1000"></textarea></label><label>Evidence<input class="vat-input" [(ngModel)]="correctionEvidence" maxlength="1000"></label>
          <label>Refund amount<input class="vat-input" type="number" [(ngModel)]="refundAmount" min="0.0001" step="0.0001"></label><label>Refund method<select class="vat-select" [(ngModel)]="refundMethod"><option>Cash</option><option>Bank</option></select></label>
          <button class="btn btn-secondary" *ngIf="allowed('refund')" [disabled]="busy()" (click)="stage('refund')">Review refund request</button><button class="btn btn-secondary" *ngIf="allowed('reverse')" [disabled]="busy()" (click)="stage('reverse')">Review full reversal request</button>
        </ng-container>
        <h3>Correction history</h3><div *ngFor="let n of corrections()" class="correction"><p>{{ n.mode }} {{ n.documentNumber || n.id }} ? {{ n.status }} ? Amount {{ n.amount }} ? Source version {{ n.sourceVersion }} ? Requested by {{ n.requestedBy }} ? Approved by {{ n.approvedBy || 'Pending' }} ? Note version {{ n.version }}</p><p>{{ n.correctionDate }} ? {{ n.reason }} ? Evidence {{ n.evidenceReference }}</p>
          <p *ngFor="let release of n.releases">Release {{ release.allocationId }} ? {{ release.invoiceId }}: {{ release.amount }}</p>
          <button class="btn btn-primary" *ngIf="allowed('approveCorrection', n)" [disabled]="busy()" (click)="stage('approveCorrection', n)">Review correction approval and posting</button>
          <ng-container *ngIf="allowed('cancelCorrection', n)"><label>Cancellation reason<input class="vat-input" [(ngModel)]="reason" maxlength="1000"></label><button class="btn btn-secondary" [disabled]="busy()" (click)="stage('cancelCorrection', n)">Cancel correction request</button></ng-container>
        </div>
      </ng-container></ng-container>
      <div *ngIf="pending() as action" class="confirmation" role="dialog" aria-label="Confirm payment command"><h3>Confirm {{ actionLabel(action) }}</h3><p>Customer {{ customerId }} ? Receipt {{ action === 'create' ? draft.reference : receipt()?.payment?.reference }} ? Receipt version {{ action === 'create' ? 'New request' : receipt()?.payment?.version }} ? Correction source version {{ pendingNote()?.sourceVersion }}</p><p>Amount: {{ action === 'create' ? draft.amount : action === 'allocate' ? allocationAmount : action === 'refund' ? refundAmount : action === 'reverse' ? receipt()?.netCollectedAmount : pendingNote()?.amount ?? receipt()?.payment?.amount }} ? Reason: {{ reason }}</p><p>Invoice allocation target: {{ invoiceId || 'Not selected' }} ? Correction date: {{ correctionDate }} ? Evidence: {{ action === 'create' ? draft.evidenceReference : correctionEvidence }}</p><p>Approval/posting commands write financial entries. Requests alone remain pending.</p><button class="btn btn-primary" [disabled]="busy()" (click)="confirm()">Confirm action</button><button class="btn btn-secondary" [disabled]="busy()" (click)="pending.set(null)">Back</button></div>
      <h2>Customer balances and reports</h2><p>Receivables show current balances. Statement and ageing use the selected dates and their own server capture timestamps; the two reads are not one combined snapshot.</p><button class="btn btn-secondary" [disabled]="busy()" (click)="loadAccount()">Refresh receivables</button>
      <p *ngIf="account() as a">Sales gross: {{ a.salesGrossAmount }} ? Net collected: {{ a.netCollectedAmount }} ? Returned: {{ a.returnedAmount }} ? Unallocated cash: {{ a.unallocatedAmount }} ? Customer balance: {{ a.balance }} {{ a.currency }}</p>
      <table class="vat-table" *ngIf="account() as a"><thead><tr><th>Invoice</th><th>Gross</th><th>Allocated</th><th>Outstanding</th></tr></thead><tbody><tr *ngFor="let i of a.invoices"><td>{{ i.reference }}</td><td>{{ i.grossAmount }}</td><td>{{ i.allocatedAmount }}</td><td>{{ i.outstandingAmount }}</td></tr></tbody></table>
      <label>Statement From (inclusive)<input class="vat-input" type="date" [(ngModel)]="from"></label><label>Until (exclusive, at most 366 days)<input class="vat-input" type="date" [(ngModel)]="until"></label><label>Ageing as of<input class="vat-input" type="date" [(ngModel)]="asOf"></label>
      <button class="btn btn-secondary" [disabled]="reportsLoading() || busy()" (click)="loadReports()">Load statement and ageing</button>
      <p role="alert" *ngIf="reportError()">{{ reportError() }}</p>
      <ng-container *ngIf="reportsReady()"><ng-container *ngIf="statement() as s"><h3>Statement</h3><p>Captured {{ s.capturedAt }} ? Opening {{ s.openingBalance }} ? Debit {{ s.totalDebit }} ? Credit {{ s.totalCredit }} ? Closing {{ s.closingBalance }} {{ s.currency }}</p><button class="btn btn-secondary" (click)="exportStatement()">Export statement lines</button><button class="btn btn-secondary" (click)="exportSummary('statement')">Export statement summary</button><table class="vat-table"><thead><tr><th>Date / document / type</th><th>Balance change</th><th>Running balance</th></tr></thead><tbody><tr *ngFor="let l of s.lines"><td>{{ l.businessDate }} / {{ l.documentNumber }} / {{ l.type }}</td><td>{{ l.balanceChange }}</td><td>{{ l.runningBalance }}</td></tr></tbody></table></ng-container>
      <ng-container *ngIf="ageing() as a"><h3>Ageing as of {{ a.asOf }}</h3><p>Captured {{ a.capturedAt }} ? Not due {{ a.buckets.notDue }} ? 1?30 {{ a.buckets.overdue1To30 }} ? 31?60 {{ a.buckets.overdue31To60 }} ? 61?90 {{ a.buckets.overdue61To90 }} ? Over 90 {{ a.buckets.overdueOver90 }} ? Unknown due date {{ a.buckets.unknownDueDate }}</p><p>Outstanding debit {{ a.outstandingDebitAmount }} ? Invoice credit {{ a.invoiceCreditAmount }} ? Balance {{ a.balance }} {{ a.currency }}</p><button class="btn btn-secondary" (click)="exportAgeing()">Export ageing invoices</button><button class="btn btn-secondary" (click)="exportSummary('ageing')">Export ageing summary</button><table class="vat-table"><thead><tr><th>Invoice</th><th>Due</th><th>Bucket</th><th>Outstanding</th></tr></thead><tbody><tr *ngFor="let i of a.invoices"><td>{{ i.reference }}</td><td>{{ i.dueDate || 'Unknown' }}</td><td>{{ i.bucket }}</td><td>{{ i.outstandingAmount }}</td></tr></tbody></table></ng-container></ng-container>
    </ng-container>
  </section>`, styles: [`label { display:inline-block; margin:10px; max-width:500px; } button { margin:8px; } form,.correction,.confirmation { border:1px solid var(--border-card); padding:14px; margin:16px 0; } [role=alert] { color:#dc2626; }`] })
export class PaymentsComponent implements OnInit, OnDestroy {
  private api = inject(PaymentsApiService); private master = inject(MasterDataApiService); readonly context = inject(CompanyContextService); private csv = inject(CsvExportService);
  readonly customers = signal<BusinessParty[]>([]); readonly customersLoading = signal(false); readonly moreCustomers = signal(false);
  readonly payments = signal<ReceiptDocument[]>([]); readonly page = signal(0); readonly morePayments = signal(false); readonly listLoading = signal(false);
  readonly receipt = signal<Receipt | null>(null); readonly corrections = signal<ReceiptCorrection[]>([]); readonly account = signal<Receivables | null>(null); readonly detailLoading = signal(false);
  readonly busy = signal(false); readonly reloadRequired = signal(false); readonly error = signal(''); readonly message = signal('');
  readonly pending = signal<Action | null>(null); readonly pendingNote = signal<ReceiptCorrection | null>(null);
  readonly statement = signal<Statement | null>(null); readonly ageing = signal<Ageing | null>(null); readonly reportsLoading = signal(false); readonly reportError = signal('');
  customerId = ''; private company = ''; private customerCursor?: string; private cursors: (string | undefined)[] = [undefined];
  private reads: Subscription[] = []; private detailRead?: Subscription; private listRead?: Subscription; private reportRead?: Subscription; private keys = new Map<string, { body: string; key: string }>(); private reportKey = '';
  readonly today = new Date().toISOString().slice(0, 10);
  draft = { reference: '', method: 'Cash', evidenceReference: '', paymentDate: this.today, amount: 0 };
  invoiceId = ''; allocationDate = this.today; allocationAmount = 0; correctionDate = this.today; correctionEvidence = ''; reason = ''; refundAmount = 0; refundMethod = 'Bank'; releaseAmounts: Record<string, number> = {};
  from = this.today.slice(0, 7) + '-01'; until = new Date(Date.UTC(Number(this.today.slice(0, 4)), Number(this.today.slice(5, 7)), 1)).toISOString().slice(0, 10); asOf = this.today;
  ngOnInit(): void { this.loadCustomers(true); }
  actionLabel(action: Action): string { return ({ create: 'receipt request', post: 'receipt approval and posting', cancel: 'receipt cancellation', allocate: 'invoice allocation', refund: 'refund request', reverse: 'reversal request', approveCorrection: 'correction approval and posting', cancelCorrection: 'correction cancellation' })[action]; }
  permission(p: string): boolean { return this.context.hasPermission(p); }
  scope(): string { return JSON.stringify([this.context.companyGuid(), this.customerId]); }
  scopeValid(): boolean { return this.company === this.context.companyGuid() && this.customers().some(c => c.id === this.customerId); }
  detailReady(): boolean { const p = this.receipt()?.payment; return this.scopeValid() && !this.detailLoading() && !!p && p.companyId === this.company && p.customerId === this.customerId; }
  loadCustomers(reset = false): void {
    if (this.customersLoading() || this.busy()) return;
    if (reset) { this.company = this.context.companyGuid(); this.customerCursor = undefined; this.customers.set([]); this.customerId = ''; this.clear(); }
    const company = this.company; this.customersLoading.set(true); this.error.set('');
    this.reads.push(this.master.getParties('Customer', { before: this.customerCursor, take: 25 }).subscribe({ next: rows => { this.customersLoading.set(false); if (company !== this.context.companyGuid()) return; this.customers.update(c => [...c, ...rows]); this.customerCursor = rows.at(-1)?.id; this.moreCustomers.set(rows.length === 25); }, error: e => { this.customersLoading.set(false); this.error.set(e?.message || 'Customer loading failed.'); } }));
  }
  private clear(): void { this.detailRead?.unsubscribe(); this.listRead?.unsubscribe(); this.reportRead?.unsubscribe(); this.payments.set([]); this.receipt.set(null); this.corrections.set([]); this.account.set(null); this.statement.set(null); this.ageing.set(null); this.pending.set(null); this.pendingNote.set(null); this.page.set(0); this.cursors = [undefined]; this.morePayments.set(false); this.reloadRequired.set(false); this.detailLoading.set(false); this.listLoading.set(false); this.reportsLoading.set(false); this.keys.clear(); this.reason = ''; this.releaseAmounts = {}; this.invoiceId = ''; this.draft = { reference: '', method: 'Cash', evidenceReference: '', paymentDate: this.today, amount: 0 }; this.allocationAmount = 0; this.refundAmount = 0; this.correctionEvidence = ''; this.allocationDate = this.today; this.correctionDate = this.today; }
  selectCustomer(): void { this.clear(); this.error.set(''); this.message.set(''); if (this.scopeValid()) { this.loadPayments(); this.loadAccount(); } }
  loadPayments(): void {
    if (!this.scopeValid()) return; this.listRead?.unsubscribe(); const scope = this.scope(); this.listLoading.set(true); this.payments.set([]); this.morePayments.set(false);
    this.listRead = this.api.list(this.customerId, this.cursors[this.page()]).subscribe({ next: rows => { if (scope !== this.scope()) return; this.listLoading.set(false); if (rows.some(p => p.companyId !== this.company || p.customerId !== this.customerId)) { this.error.set('Receipt scope mismatch.'); return; } this.payments.set(rows); this.morePayments.set(rows.length === 25); }, error: e => { this.listLoading.set(false); this.error.set(e?.message || 'Receipt loading failed.'); } });
  }
  changePage(n: number): void { if (this.listLoading() || this.busy() || (n < 0 && this.page() === 0) || (n > 0 && !this.morePayments())) return; if (n > 0) this.cursors[this.page() + 1] = this.payments().at(-1)?.id; this.page.update(p => p + n); this.loadPayments(); }
  loadAccount(): void { if (!this.scopeValid()) return; const scope = this.scope(); this.account.set(null); this.reads.push(this.api.receivables(this.customerId).subscribe({ next: a => { if (scope === this.scope() && a.customerId === this.customerId) this.account.set(a); }, error: e => this.error.set(e?.message || 'Receivables loading failed.') })); }
  review(id: string): void {
    if (!this.scopeValid() || this.busy()) return; this.detailRead?.unsubscribe(); const scope = this.scope(); this.detailLoading.set(true); this.receipt.set(null); this.corrections.set([]); this.pending.set(null); this.reloadRequired.set(true); this.error.set(''); this.reason = ''; this.releaseAmounts = {}; this.invoiceId = '';
    this.detailRead = forkJoin({ receipt: this.api.get(id), notes: this.api.corrections(id), account: this.api.receivables(this.customerId) }).subscribe({ next: r => {
      if (scope !== this.scope()) return; this.detailLoading.set(false);
      if (r.receipt.payment.id !== id || r.receipt.payment.companyId !== this.company || r.receipt.payment.customerId !== this.customerId || r.account.customerId !== this.customerId || r.notes.some(n => n.paymentId !== id || n.customerId !== this.customerId)) { this.error.set('Receipt or correction scope mismatch.'); return; }
      this.receipt.set(r.receipt); this.corrections.set(r.notes); this.account.set(r.account); this.reloadRequired.set(false);
    }, error: e => { this.detailLoading.set(false); this.error.set(e?.message || 'Receipt review failed.'); } });
  }
  allowed(action: Action, n?: ReceiptCorrection): boolean {
    if (action === 'create') return this.scopeValid() && this.customers().find(c => c.id === this.customerId)?.isActive !== false && this.permission('payment.request');
    if (!this.detailReady() || this.reloadRequired()) return false; const p = this.receipt()!.payment; if (!Number.isSafeInteger(p.version) || p.version < 1) return false;
    const actor = this.context.currentUser().id;
    if (action === 'post') return p.status === 'Pending' && p.requestedBy !== actor && this.permission('payment.approve');
    if (action === 'cancel') return p.status === 'Pending' && this.permission('payment.request');
    if (action === 'allocate') return p.status === 'Posted' && this.permission('payment.allocate');
    if (action === 'refund' || action === 'reverse') return p.status === 'Posted' && this.permission(action === 'refund' ? 'payment.refund.request' : 'payment.reverse.request');
    if (!n || !this.corrections().some(c => c.id === n.id) || n.status !== 'Pending' || p.status !== 'CorrectionPending' || n.sourceVersion !== p.version) return false;
    const mode = n.mode === 'Refund' ? 'refund' : 'reverse';
    return action === 'approveCorrection' ? actor !== n.requestedBy && actor !== p.requestedBy && this.permission(`payment.${mode}.approve`) : this.permission(`payment.${mode}.request`);
  }
  released(id: string): number { return (this.receipt()?.allocationReleases || []).filter(r => r.allocationId === id).reduce((sum, r) => sum + r.amount, 0); }
  validDate(s: string): boolean { return /^\d{4}-\d{2}-\d{2}$/.test(s) && !Number.isNaN(Date.parse(s)) && new Date(s).toISOString().slice(0, 10) === s; }
  money(n: number): boolean { return Number.isFinite(n) && n > 0 && n <= 1e15 && Number(n.toFixed(4)) === n && Number.isSafeInteger(Math.round(n * 10000)); }
  stage(action: Action, note?: ReceiptCorrection): void { if (this.busy() || !this.allowed(action, note)) return; this.pendingNote.set(note || null); this.pending.set(action); this.error.set(''); }
  private key(action: string, body: unknown): string { const text = JSON.stringify(body); const old = this.keys.get(action); if (old?.body === text) return old.key; const key = crypto.randomUUID(); this.keys.set(action, { body: text, key }); return key; }
  confirm(): void {
    const action = this.pending(); const note = this.pendingNote() || undefined; if (!action || this.busy() || !this.allowed(action, note)) return;
    const p = this.receipt()?.payment; let command: Observable<unknown>; const fail = (text: string) => this.error.set(text);
    if (action === 'create') {
      if (!this.draft.reference.trim() || !this.draft.evidenceReference.trim() || !this.validDate(this.draft.paymentDate) || !this.money(this.draft.amount) || !['Cash', 'Bank'].includes(this.draft.method)) return fail('Supply reference, evidence, date, method and a positive amount with up to four decimals in the supported numeric range.');
      const body = { ...this.draft, customerId: this.customerId, allocations: [] }; command = this.api.create(body, this.key('create', body));
    } else if (action === 'post') command = this.api.post(p!.id, p!.version);
    else if (action === 'cancel') { if (!this.reason.trim()) return fail('Cancellation reason is required.'); command = this.api.cancel(p!.id, p!.version, this.reason.trim()); }
    else if (action === 'allocate') {
      const invoice = this.account()?.invoices.find(i => i.invoiceId === this.invoiceId);
      if (!invoice || !this.money(this.allocationAmount) || !this.validDate(this.allocationDate) || this.allocationAmount > invoice.outstandingAmount || this.allocationAmount > this.receipt()!.unallocatedAmount) return fail('Choose an outstanding invoice and an allocation within invoice and receipt balances.');
      command = this.api.allocate(p!.id, p!.version, this.allocationDate, [{ invoiceId: this.invoiceId, amount: this.allocationAmount }]);
    } else if (action === 'refund' || action === 'reverse') {
      if (!this.validDate(this.correctionDate) || this.correctionDate < p!.paymentDate || !this.reason.trim() || !this.correctionEvidence.trim()) return fail('Supply correction date, reason and evidence.');
      const body = { expectedVersion: p!.version, correctionDate: this.correctionDate, reason: this.reason.trim(), evidenceReference: this.correctionEvidence.trim() };
      if (action === 'reverse') command = this.api.requestReversal(p!.id, body, this.key('reverse', body));
      else {
        const releases = Object.entries(this.releaseAmounts).filter(([, amount]) => amount !== 0 && amount != null).map(([allocationId, amount]) => ({ allocationId, amount }));
        if (!this.money(this.refundAmount) || !['Cash', 'Bank'].includes(this.refundMethod) || this.refundAmount > this.receipt()!.netCollectedAmount || releases.length > 200 || releases.some(r => !this.money(r.amount) || !this.receipt()!.allocations.some(a => a.id === r.allocationId && r.amount <= a.amount - this.released(a.id))) || releases.reduce((sum, r) => sum + r.amount, 0) > this.refundAmount) return fail('Check refund amount, method and remaining allocation releases. Server credit checks also apply.');
        const refund = { ...body, amount: this.refundAmount, method: this.refundMethod, releases }; command = this.api.requestRefund(p!.id, refund, this.key('refund', refund));
      }
    } else { if (action === 'cancelCorrection' && !this.reason.trim()) return fail('Cancellation reason is required.'); command = this.api.decide(p!.id, note!, action === 'approveCorrection' ? 'approve' : 'cancel', this.reason.trim()); }
    const scope = this.scope(); this.busy.set(true); this.error.set(''); this.message.set('');
    this.reads.push(command.subscribe({ next: response => {
      this.busy.set(false); this.pending.set(null); if (scope !== this.scope()) return;
      this.message.set('Action completed. Refreshing balances.'); this.statement.set(null); this.ageing.set(null);
      if (action === 'create') { this.draft.reference = ''; this.draft.evidenceReference = ''; this.draft.amount = 0; this.page.set(0); this.cursors = [undefined]; this.keys.delete('create'); }
      this.loadPayments(); const id = action === 'create' ? (response as Receipt).payment.id : p!.id; this.review(id);
    }, error: e => { this.busy.set(false); if (scope !== this.scope()) return; this.pending.set(null); if (action !== 'create') this.reloadRequired.set(true); this.error.set(e?.message || 'Server rejected the command. Reload before retrying.'); } }));
  }
  private currentReportKey(): string { return JSON.stringify([this.scope(), this.from, this.until, this.asOf]); }
  reportsReady(): boolean { return this.scopeValid() && !this.busy() && !this.reportsLoading() && !!this.statement() && !!this.ageing() && this.reportKey === this.currentReportKey(); }
  loadReports(): void {
    this.reportRead?.unsubscribe(); this.statement.set(null); this.ageing.set(null); this.reportError.set(''); this.reportsLoading.set(false);
    if (!this.scopeValid() || !this.validDate(this.from) || !this.validDate(this.until) || !this.validDate(this.asOf) || this.from >= this.until || (Date.parse(this.until) - Date.parse(this.from)) / 86400000 > 366) { this.reportError.set('Choose a customer, a valid statement range of at most 366 days and an ageing date.'); return; }
    const key = this.currentReportKey(); this.reportsLoading.set(true);
    this.reportRead = forkJoin({ statement: this.api.statement(this.customerId, this.from, this.until), ageing: this.api.ageing(this.customerId, this.asOf) }).subscribe({ next: r => { this.reportsLoading.set(false); if (key !== this.currentReportKey()) return; if (r.statement.customerId !== this.customerId || r.ageing.customerId !== this.customerId || r.statement.from !== this.from || r.statement.until !== this.until || r.ageing.asOf !== this.asOf) { this.reportError.set('Report customer/date scope mismatch.'); return; } this.statement.set(r.statement); this.ageing.set(r.ageing); this.reportKey = key; }, error: e => { this.reportsLoading.set(false); this.reportError.set(e?.message || 'Customer report loading failed.'); } });
  }
  exportStatement(): void { if (!this.reportsReady()) return; const s = this.statement()!; this.csv.exportToCsv(`Statement_${this.company}_${this.customerId}_${s.from}_${s.until}`, ['Captured at', 'Opening balance', 'Closing balance', 'Document ID', 'Invoice ID', 'Business date', 'Type', 'Reference', 'Net', 'Output tax', 'Balance change', 'Running balance'], s.lines.length ? s.lines.map(l => [s.capturedAt, s.openingBalance, s.closingBalance, l.documentId, l.invoiceId || '', l.businessDate, l.type, l.documentNumber, l.netAmount, l.outputTaxAmount, l.balanceChange, l.runningBalance]) : [[s.capturedAt, s.openingBalance, s.closingBalance, '', '', '', 'No activity', '', 0, 0, 0, s.closingBalance]]); }
  exportAgeing(): void { if (!this.reportsReady()) return; const a = this.ageing()!; this.csv.exportToCsv(`Ageing_${this.company}_${this.customerId}_${a.asOf}`, ['Captured at', 'As of', 'Invoice ID', 'Reference', 'Business date', 'Due date', 'Days overdue', 'Bucket', 'Gross', 'Allocated', 'Outstanding'], a.invoices.map(i => [a.capturedAt, a.asOf, i.invoiceId, i.reference, i.businessDate, i.dueDate || 'Unknown', i.daysOverdue ?? 'Unknown', i.bucket, i.grossAmount, i.allocatedAmount, i.outstandingAmount])); }
  exportSummary(kind: 'statement' | 'ageing'): void {
    if (!this.reportsReady()) return;
    if (kind === 'statement') {
      const s = this.statement()!;
      this.csv.exportToCsv(`Statement_Summary_${this.company}_${this.customerId}_${s.from}_${s.until}`, ['Customer ID', 'Currency', 'From inclusive', 'Until exclusive', 'Captured at', 'Opening balance', 'Total debit', 'Total credit', 'Closing balance'], [[s.customerId, s.currency, s.from, s.until, s.capturedAt, s.openingBalance, s.totalDebit, s.totalCredit, s.closingBalance]]);
    } else {
      const a = this.ageing()!;
      this.csv.exportToCsv(`Ageing_Summary_${this.company}_${this.customerId}_${a.asOf}`, ['Customer ID', 'Currency', 'As of', 'Captured at', 'Not due', 'Overdue 1-30', 'Overdue 31-60', 'Overdue 61-90', 'Overdue over 90', 'Unknown due date', 'Outstanding debit', 'Invoice credit', 'Collected', 'Returned', 'Net collected', 'Allocated', 'Unallocated', 'Balance'], [[a.customerId, a.currency, a.asOf, a.capturedAt, a.buckets.notDue, a.buckets.overdue1To30, a.buckets.overdue31To60, a.buckets.overdue61To90, a.buckets.overdueOver90, a.buckets.unknownDueDate, a.outstandingDebitAmount, a.invoiceCreditAmount, a.collectedAmount, a.returnedAmount, a.netCollectedAmount, a.allocatedAmount, a.unallocatedAmount, a.balance]]);
    }
  }
  ngOnDestroy(): void { this.reads.forEach(r => r.unsubscribe()); this.detailRead?.unsubscribe(); this.listRead?.unsubscribe(); this.reportRead?.unsubscribe(); }
}
