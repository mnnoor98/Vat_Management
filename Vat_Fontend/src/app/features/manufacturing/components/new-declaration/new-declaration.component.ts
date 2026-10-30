import { Component, OnInit, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ManufacturingApiService, CreateCoefficientPayload } from '../../../../core/services/api/manufacturing-api.service';
import { MasterDataApiService } from '../../../../core/services/api/master-data-api.service';
import { CompanyContextService } from '../../../../core/services/company-context.service';
import { ProductItem } from '../../../../core/models/vat-models';
@Component({
  selector: 'app-new-declaration', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <form class="vat-card" #form="ngForm" (ngSubmit)="submitNewDeclaration()">
      <h2>Create manufacturing coefficient draft</h2>
      <p>This company-level draft captures material inputs and declared costs. The server validates products and calculates an immutable cost snapshot. Submission and approval are separate actions; this does not file a statutory declaration.</p>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <p *ngIf="loading()">Loading products?</p>
      <button class="btn btn-secondary" type="button" [disabled]="loading() || busy()" (click)="loadProducts()">Reload products</button>
      <label>Declaration reference<input class="vat-input" name="reference" [(ngModel)]="draft.declarationNumber" required maxlength="100"></label>
      <label>Finished product<select class="vat-select" name="finished" [(ngModel)]="draft.finishedProductId" required><option value="">Select finished product</option><option *ngFor="let p of products()" [value]="p.id">{{ p.code }} ? {{ p.name }} ({{ p.uom }})</option></select></label>
      <label>Batch size<input class="vat-input" type="number" name="batch" [(ngModel)]="draft.batchSize" min="0.000001" max="999999999.999999" step="0.000001" required></label>
      <label>Effective from<input class="vat-input" type="date" name="from" [(ngModel)]="draft.effectiveFrom" required></label>
      <label>Effective until (optional)<input class="vat-input" type="date" name="until" [(ngModel)]="effectiveUntil"></label>
      <h3>Raw materials</h3>
      <div *ngFor="let line of draft.materials; let i = index" class="material-row">
        <label>Product<select class="vat-select" [name]="'product' + i" [(ngModel)]="line.productId" required><option value="">Select raw material</option><option *ngFor="let p of products()" [value]="p.id" [disabled]="p.id === draft.finishedProductId">{{ p.code }} ? {{ p.name }} ({{ p.uom }})</option></select></label>
        <label>Quantity<input class="vat-input" type="number" [name]="'qty' + i" [(ngModel)]="line.quantity" min="0.000001" max="999999999.999999" step="0.000001" required></label>
        <label>Wastage %<input class="vat-input" type="number" [name]="'wastage' + i" [(ngModel)]="line.wastagePercent" min="0" max="100" step="0.000001" required></label>
        <label>Declared unit price<input class="vat-input" type="number" [name]="'price' + i" [(ngModel)]="line.unitPrice" min="0" max="999999999.999999" step="0.000001" required></label>
        <button type="button" class="btn btn-secondary" [disabled]="busy() || draft.materials.length === 1" (click)="removeMaterialRow(i)">Remove line</button>
      </div>
      <button type="button" class="btn btn-secondary" [disabled]="busy() || draft.materials.length >= 100" (click)="addMaterialRow()">Add material</button>
      <label>Direct labor cost<input class="vat-input" type="number" name="labor" [(ngModel)]="draft.directLaborCost" min="0" max="999999999.999999" step="0.000001" required></label>
      <label>Utility cost<input class="vat-input" type="number" name="utility" [(ngModel)]="draft.utilityCost" min="0" max="999999999.999999" step="0.000001" required></label>
      <label>Overhead cost<input class="vat-input" type="number" name="overhead" [(ngModel)]="draft.overheadCost" min="0" max="999999999.999999" step="0.000001" required></label>
      <button class="btn btn-primary" type="submit" [disabled]="busy() || saved() || loading() || form.invalid || !context.hasPermission('manufacturing.request')">Save draft</button>
      <button class="btn btn-secondary" type="button" [disabled]="busy()" (click)="cancelled.emit()">Back to register</button>
    </form>
  `,
  styles: [`label { display:block; margin:12px 0; max-width:650px; } .material-row { padding:12px; border:1px solid var(--border-card); margin:12px 0; } button { margin:10px 8px 0 0; } [role=alert] { color:#dc2626; }`]
})
export class NewDeclarationComponent implements OnInit {
  @Output() declarationCreated = new EventEmitter<string>();
  @Output() cancelled = new EventEmitter<void>();
  private api = inject(ManufacturingApiService);
  private master = inject(MasterDataApiService);
  readonly context = inject(CompanyContextService);
  readonly products = signal<ProductItem[]>([]);
  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly saved = signal(false);
  readonly error = signal('');
  private command?: { body: string; key: string };
  effectiveUntil = '';
  draft: CreateCoefficientPayload = { declarationNumber: '', finishedProductId: '', batchSize: 1,
    effectiveFrom: new Date().toISOString().slice(0, 10), directLaborCost: 0, utilityCost: 0, overheadCost: 0,
    materials: [{ productId: '', quantity: 1, wastagePercent: 0, unitPrice: 0 }] };
  ngOnInit(): void { this.loadProducts(); }
  loadProducts(): void {
    if (this.loading()) return;
    this.loading.set(true); this.error.set(''); this.products.set([]);
    this.master.getProducts().subscribe({ next: rows => { this.products.set(rows); this.loading.set(false); }, error: e => { this.error.set(e?.message || 'Unable to load products.'); this.loading.set(false); } });
  }
  addMaterialRow(): void { if (!this.busy() && this.draft.materials.length < 100) this.draft.materials.push({ productId: '', quantity: 1, wastagePercent: 0, unitPrice: 0 }); }
  removeMaterialRow(index: number): void { if (!this.busy() && this.draft.materials.length > 1) this.draft.materials.splice(index, 1); }
  submitNewDeclaration(): void {
    if (this.busy() || this.saved() || this.loading() || !this.context.hasPermission('manufacturing.request')) return;
    const d = this.draft;
    const validNumber = (n: number) => Number.isFinite(n) && n >= 0 && n < 1000000000 && Number(n.toFixed(6)) === n;
    const registered = (id: string) => this.products().some(p => p.id === id);
    if (!d.declarationNumber.trim() || d.declarationNumber.length > 100 || !registered(d.finishedProductId) || !validNumber(d.batchSize) || d.batchSize <= 0 || !d.effectiveFrom ||
      (this.effectiveUntil && this.effectiveUntil < d.effectiveFrom) || ![d.directLaborCost, d.utilityCost, d.overheadCost].every(validNumber) ||
      d.materials.length < 1 || d.materials.length > 100 || new Set(d.materials.map(m => m.productId)).size !== d.materials.length ||
      d.materials.some(m => !registered(m.productId) || m.productId === d.finishedProductId || !validNumber(m.quantity) || m.quantity <= 0 || !validNumber(m.unitPrice) || !validNumber(m.wastagePercent) || m.wastagePercent > 100)) {
      this.error.set('Check the reference, registered products, effective dates, unique material lines, quantities and costs (up to six decimal places).'); return;
    }
    const payload: CreateCoefficientPayload = { ...d, declarationNumber: d.declarationNumber.trim(), ...(this.effectiveUntil ? { effectiveUntil: this.effectiveUntil } : {}) };
    const body = JSON.stringify(payload);
    if (this.command?.body !== body) this.command = { body, key: crypto.randomUUID() };
    this.busy.set(true); this.error.set('');
    this.api.createCoefficient(payload, this.command!.key).subscribe({ next: result => { this.busy.set(false); this.saved.set(true); this.declarationCreated.emit(result.declarationNumber); },
      error: e => { this.busy.set(false); this.error.set(e?.message || 'Draft creation failed.'); } });
  }
}
