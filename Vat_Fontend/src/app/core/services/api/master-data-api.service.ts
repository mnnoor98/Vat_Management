import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, forkJoin, throwError } from 'rxjs';
import { ProductItem, BusinessParty } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

interface BackendProductItem { id: string; reference: string; description: string; unitOfMeasure: string; taxCode: string; }
interface BackendSupplierItem { id: string; reference: string; name: string; }
interface BackendCustomerItem { id: string; code: string; name: string; contact: string; address: string; taxRegistrationReference: string; isActive: boolean; version: number; }
export interface CustomerPage { before?: string; take?: number; }

@Injectable({ providedIn: 'root' })
export class MasterDataApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);
  private get baseUrl(): string { return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}`; }

  getProducts(): Observable<ProductItem[]> {
    if (environment.useMockData) return of([...this.mockData.products]).pipe(delay(50));
    return this.http.get<BackendProductItem[]>(`${this.baseUrl}/products`).pipe(map(items => items.map(p => this.product(p))));
  }
  getParties(type?: 'Customer' | 'Supplier', page: CustomerPage = {}): Observable<BusinessParty[]> {
    if (environment.useMockData) return of(this.mockData.parties.filter(p => !type || p.partyType === type)).pipe(delay(50));
    if (!type) return forkJoin([this.getParties('Supplier'), this.getParties('Customer', page)]).pipe(map(([s, c]) => [...s, ...c]));
    if (type === 'Supplier') return this.http.get<BackendSupplierItem[]>(`${this.baseUrl}/suppliers`).pipe(map(items => items.map(s => this.supplier(s))));
    return this.http.get<BackendCustomerItem[]>(`${this.baseUrl}/customers`, { params: { take: page.take ?? 25, ...(page.before ? { before: page.before } : {}) } })
      .pipe(map(items => items.map(c => this.customer(c))));
  }
  createProduct(product: ProductItem): Observable<ProductItem> {
    if (environment.useMockData) { this.mockData.products.unshift(product); return of(product).pipe(delay(50)); }
    if (!product.code?.trim() || !product.taxCode?.trim() || !product.uom?.trim()) return throwError(() => new Error('Product code, unit and tax code are required.'));
    return this.http.post<BackendProductItem>(`${this.baseUrl}/products`, {
      reference: product.code, description: product.name, unitOfMeasure: product.uom, taxCode: product.taxCode
    }).pipe(map(p => this.product(p)));
  }
  createParty(party: BusinessParty): Observable<BusinessParty> {
    if (environment.useMockData) { this.mockData.parties.unshift(party); return of(party).pipe(delay(50)); }
    if (party.partyType === 'Supplier') return this.http.post<BackendSupplierItem>(`${this.baseUrl}/suppliers`, {
      reference: party.code, name: party.name
    }).pipe(map(s => this.supplier(s)));
    return this.http.post<BackendCustomerItem>(`${this.baseUrl}/customers`, {
      code: party.code, name: party.name, contact: party.contactPerson || '', address: party.address || '', taxRegistrationReference: party.bin || ''
    }).pipe(map(c => this.customer(c)));
  }
  private product(p: BackendProductItem): ProductItem {
    return { id: p.id, code: p.reference, name: p.description, nameBn: p.description, taxCode: p.taxCode,
      hsCode: '', uom: p.unitOfMeasure, uomBn: p.unitOfMeasure, standardCost: 0, salesPrice: 0, vatRatePercent: 0,
      supplementaryDutyPercent: 0, isExempt: false, isZeroRated: false, vdsApplicable: false, stockQty: 0, metadataOnly: true };
  }
  private supplier(s: BackendSupplierItem): BusinessParty {
    return { id: s.id, code: s.reference, name: s.name, nameBn: s.name, partyType: 'Supplier', bin: '', address: '', addressBn: '',
      contactPerson: '', phone: '', email: '', isVdsEntity: false };
  }
  private customer(c: BackendCustomerItem): BusinessParty {
    return { id: c.id, code: c.code, name: c.name, nameBn: c.name, partyType: 'Customer', bin: c.taxRegistrationReference || '',
      address: c.address || '', addressBn: c.address || '', contactPerson: c.contact || '', phone: '', email: '', isVdsEntity: false,
      version: c.version, isActive: c.isActive };
  }
}
