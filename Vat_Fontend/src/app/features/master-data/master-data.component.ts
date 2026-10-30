import { Observable } from 'rxjs';
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MasterDataApiService } from '../../core/services/api/master-data-api.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { CsvExportService } from '../../core/services/csv-export.service';
import { ProductItem, BusinessParty } from '../../core/models/vat-models';

@Component({
  selector: 'app-master-data', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <div class="vat-card">
      <h1>Master data</h1>
      <p>Products store reference, description, unit and tax code. Suppliers store reference and name. Customer details are shown only when supplied. Stock, prices and applicable tax rates are available through their respective financial workflows.</p>
      <div class="actions">
        <button class="btn btn-secondary" *ngFor="let t of tabs" [disabled]="busy()" (click)="selectTab(t)">{{ t }}</button>
        <button class="btn btn-secondary" [disabled]="busy()" (click)="reload()">Reload</button>
        <button class="btn btn-secondary" [disabled]="busy()" (click)="exportPage()">Export current page</button>
      </div>
      <p role="alert" *ngIf="error()">{{ error() }}</p>
      <p *ngIf="busy()">Loading?</p>
      <input class="vat-input" [(ngModel)]="search" placeholder="Search this page by code or name">
      <p>Page {{ page() + 1 }} ? Search and export apply to this page.</p>
      <div class="vat-table-container"><table class="vat-table">
        <thead><tr><th>Code</th><th>Name</th><ng-container *ngIf="tab() === 'Products'"><th>Unit</th><th>Tax code</th></ng-container><ng-container *ngIf="tab() === 'Customers'"><th>Tax registration</th><th>Address</th><th>Contact</th><th>Active</th></ng-container></tr></thead>
        <tbody><tr *ngFor="let item of filteredRows()"><td>{{ item.code }}</td><td>{{ item.name }}</td>
          <ng-container *ngIf="tab() === 'Products'"><td>{{ asProduct(item).uom }}</td><td>{{ asProduct(item).taxCode }}</td></ng-container>
          <ng-container *ngIf="tab() === 'Customers'"><td>{{ asParty(item).bin || 'Not supplied' }}</td><td>{{ asParty(item).address || 'Not supplied' }}</td><td>{{ asParty(item).contactPerson || 'Not supplied' }}</td><td>{{ asParty(item).isActive ? 'Yes' : 'No' }}</td></ng-container>
        </tr><tr *ngIf="!busy() && filteredRows().length === 0"><td>No records on this page.</td></tr></tbody>
      </table></div>
      <div class="actions">
        <button class="btn btn-secondary" [disabled]="busy() || page() === 0" (click)="changePage(-1)">Previous</button>
        <button class="btn btn-secondary" [disabled]="busy() || !hasNext()" (click)="changePage(1)">Next</button>
      </div>
      <form *ngIf="canCreate()" (ngSubmit)="save()" #form="ngForm">
        <h2>Create {{ tab() === 'Products' ? 'product' : tab() === 'Suppliers' ? 'supplier' : 'customer' }}</h2>
        <label>Code / reference<input class="vat-input" name="code" [(ngModel)]="draft.code" required maxlength="100"></label>
        <label>Name / description<input class="vat-input" name="name" [(ngModel)]="draft.name" required maxlength="300"></label>
        <ng-container *ngIf="tab() === 'Products'">
          <label>Unit of measure<input class="vat-input" name="unit" [(ngModel)]="draft.uom" required></label>
          <label>Tax code<input class="vat-input" name="taxCode" [(ngModel)]="draft.taxCode" required></label>
          <p>Tax rates require separately published rules for the business date.</p>
        </ng-container>
        <ng-container *ngIf="tab() === 'Customers'">
          <label>Tax registration (optional)<input class="vat-input" name="bin" [(ngModel)]="draft.bin"></label>
          <label>Address (optional)<input class="vat-input" name="address" [(ngModel)]="draft.address"></label>
          <label>Contact (optional)<input class="vat-input" name="contact" [(ngModel)]="draft.contactPerson"></label>
        </ng-container>
        <button class="btn btn-primary" [disabled]="busy() || form.invalid" type="submit">Create</button>
      </form>
      <p *ngIf="saved()" role="status">{{ saved() }}</p>
    </div>
  `,
  styles: [`label { display: block; margin: 12px 0; } .actions { display:flex; gap:8px; margin:16px 0; flex-wrap:wrap; } form { max-width:600px; } [role=alert] { color: #dc2626; }`]
})
export class MasterDataComponent {
  private api = inject(MasterDataApiService);
  private context = inject(CompanyContextService);
  private csv = inject(CsvExportService);
  readonly tabs = ['Products', 'Suppliers', 'Customers'] as const;
  readonly tab = signal<'Products' | 'Suppliers' | 'Customers'>('Products');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly saved = signal('');
  readonly page = signal(0);
  readonly rows = signal<(ProductItem | BusinessParty)[]>([]);
  private allRows: (ProductItem | BusinessParty)[] = [];
  private cursors: (string | undefined)[] = [undefined];
  readonly hasNext = signal(false);
  search = '';
  draft = this.emptyDraft();
  ngOnInit(): void { this.reload(); }
  asProduct(item: ProductItem | BusinessParty): ProductItem { return item as ProductItem; }
  asParty(item: ProductItem | BusinessParty): BusinessParty { return item as BusinessParty; }
  filteredRows(): (ProductItem | BusinessParty)[] {
    const term = this.search.toLowerCase().trim();
    return this.rows().filter(item => !term || item.code.toLowerCase().includes(term) || item.name.toLowerCase().includes(term));
  }
  canCreate(): boolean { return this.context.hasPermission('masterdata.manage'); }
  selectTab(tab: 'Products' | 'Suppliers' | 'Customers'): void {
    this.tab.set(tab); this.page.set(0); this.cursors = [undefined]; this.search = ''; this.draft = this.emptyDraft(); this.saved.set(''); this.reload();
  }
  reload(): void {
    this.busy.set(true); this.error.set(''); this.rows.set([]); this.hasNext.set(false);
    const request: Observable<(ProductItem | BusinessParty)[]> = this.tab() === 'Products' ? this.api.getProducts() : this.api.getParties(this.tab() === 'Suppliers' ? 'Supplier' : 'Customer', { before: this.cursors[this.page()], take: 25 });
    request.subscribe({ next: rows => {
      this.allRows = rows;
      if (this.tab() === 'Customers') { this.rows.set(rows); this.hasNext.set(rows.length === 25); }
      else this.slicePage();
      this.busy.set(false);
    }, error: e => { this.error.set(e?.message || 'Unable to load master data.'); this.allRows = []; this.busy.set(false); } });
  }
  changePage(direction: number): void {
    if (this.busy() || (direction < 0 && this.page() === 0) || (direction > 0 && !this.hasNext())) return;
    if (this.tab() === 'Customers' && direction > 0) this.cursors[this.page() + 1] = this.rows().at(-1)?.id;
    this.page.update(p => p + direction);
    if (this.tab() === 'Customers') this.reload(); else this.slicePage();
  }
  private slicePage(): void {
    this.rows.set(this.allRows.slice(this.page() * 25, (this.page() + 1) * 25));
    this.hasNext.set((this.page() + 1) * 25 < this.allRows.length);
  }
  save(): void {
    if (this.busy() || !this.canCreate()) return;
    this.busy.set(true); this.error.set(''); this.saved.set('');
    const request: Observable<ProductItem | BusinessParty> = this.tab() === 'Products' ? this.api.createProduct(this.draft as unknown as ProductItem)
      : this.api.createParty({ ...this.draft, partyType: this.tab() === 'Suppliers' ? 'Supplier' : 'Customer' } as unknown as BusinessParty);
    request.subscribe({ next: item => { this.saved.set(`Created ${item.code}: ${item.name}`); this.draft = this.emptyDraft(); this.page.set(0); this.cursors = [undefined]; this.reload(); },
      error: e => { this.error.set(e?.message || 'Creation failed.'); this.busy.set(false); } });
  }
  exportPage(): void {
    const headers = this.tab() === 'Products' ? ['Code', 'Name', 'Unit', 'Tax code'] : this.tab() === 'Suppliers' ? ['Reference', 'Name'] : ['Code', 'Name', 'Tax registration', 'Address', 'Contact', 'Active'];
    const rows = this.filteredRows().map(item => this.tab() === 'Products' ? [item.code, item.name, this.asProduct(item).uom, this.asProduct(item).taxCode || ''] : this.tab() === 'Suppliers' ? [item.code, item.name] : [item.code, item.name, this.asParty(item).bin, this.asParty(item).address, this.asParty(item).contactPerson, this.asParty(item).isActive ? 'Yes' : 'No']);
    this.csv.exportToCsv(`Master_${this.tab()}_Page_${this.page() + 1}`, headers, rows);
  }
  private emptyDraft() { return { code: '', name: '', uom: '', taxCode: '', bin: '', address: '', contactPerson: '' }; }
}
