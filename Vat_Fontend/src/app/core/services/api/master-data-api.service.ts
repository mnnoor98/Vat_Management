import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, catchError } from 'rxjs';
import { ProductItem, BusinessParty } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

interface BackendProductItem {
  id: string;
  reference: string;
  description: string;
  unitOfMeasure: string;
  taxCode: string;
}

interface BackendSupplierItem {
  id: string;
  reference: string;
  name: string;
}

interface BackendCustomerItem {
  id: string;
  code: string;
  name: string;
  contact?: string;
  address?: string;
  taxRegistrationReference?: string;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class MasterDataApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);

  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}`;
  }

  public getProducts(): Observable<ProductItem[]> {
    if (environment.useMockData) {
      return of([...this.mockData.products]).pipe(delay(50));
    }
    return this.http.get<BackendProductItem[]>(`${this.baseUrl}/products`).pipe(
      map(items => items.map(p => {
        const matched = this.mockData.products.find(m => m.id === p.reference || m.code === p.reference);
        return {
          id: p.id || p.reference,
          code: p.reference,
          name: p.description,
          nameBn: matched?.nameBn || p.description,
          hsCode: matched?.hsCode || '3901.20.00',
          uom: p.unitOfMeasure || 'KG',
          uomBn: matched?.uomBn || p.unitOfMeasure,
          standardCost: matched?.standardCost || 140.0,
          salesPrice: matched?.salesPrice || 180.0,
          vatRatePercent: 15,
          supplementaryDutyPercent: 0,
          isExempt: false,
          isZeroRated: false,
          vdsApplicable: matched?.vdsApplicable ?? true,
          stockQty: matched?.stockQty || 50000
        } as ProductItem;
      })),
      catchError(() => of([...this.mockData.products]))
    );
  }

  public getParties(type?: 'Customer' | 'Supplier'): Observable<BusinessParty[]> {
    if (environment.useMockData) {
      const filtered = type ? this.mockData.parties.filter(p => p.partyType === type) : this.mockData.parties;
      return of([...filtered]).pipe(delay(50));
    }
    if (type === 'Supplier') {
      return this.http.get<BackendSupplierItem[]>(`${this.baseUrl}/suppliers`).pipe(
        map(items => items.map(s => {
          const matched = this.mockData.parties.find(m => m.id === s.reference || m.name === s.name);
          return {
            id: s.reference,
            partyType: 'Supplier',
            code: s.reference,
            name: s.name,
            nameBn: matched?.nameBn || s.name,
            bin: matched?.bin || '001928374-0101',
            address: matched?.address || 'Tejgaon Industrial Area, Dhaka',
            addressBn: matched?.addressBn || 'তেজগাঁও শিল্প এলাকা, ঢাকা',
            contactPerson: matched?.contactPerson || 'Md. Shafiqul Alam',
            phone: matched?.phone || '+880 1711-234567',
            email: matched?.email || 'supplier@padmagroup.com.bd',
            isVdsEntity: true
          } as BusinessParty;
        })),
        catchError(() => {
          const filtered = this.mockData.parties.filter(p => p.partyType === 'Supplier');
          return of([...filtered]);
        })
      );
    }
    return this.http.get<BackendCustomerItem[]>(`${this.baseUrl}/customers`).pipe(
      map(items => items.map(c => {
        const matched = this.mockData.parties.find(m => m.id === c.code || m.code === c.code || m.name === c.name);
        return {
          id: c.id,
          partyType: 'Customer',
          code: c.code,
          name: c.name,
          nameBn: matched?.nameBn || c.name,
          bin: c.taxRegistrationReference || matched?.bin || '000456123-0101',
          address: c.address || matched?.address || 'Dhaka, Bangladesh',
          addressBn: matched?.addressBn || c.address || 'ঢাকা, বাংলাদেশ',
          contactPerson: c.contact || matched?.contactPerson || 'Authorized Representative',
          phone: matched?.phone || '+880 1711-000000',
          email: matched?.email || `${c.code.toLowerCase()}@client.com.bd`,
          isVdsEntity: true
        } as BusinessParty;
      })),
      catchError(() => {
        const filtered = this.mockData.parties.filter(p => p.partyType === 'Customer');
        return of([...filtered]);
      })
    );
  }

  public createProduct(product: ProductItem): Observable<ProductItem> {
    if (environment.useMockData) {
      this.mockData.products.unshift(product);
      return of(product).pipe(delay(100));
    }
    const payload = {
      reference: product.id || product.code,
      description: product.name,
      unitOfMeasure: product.uom || 'PCS',
      taxCode: 'STD-15'
    };
    return this.http.post<BackendProductItem>(`${this.baseUrl}/products`, payload).pipe(
      map(res => ({
        ...product,
        id: res.reference,
        code: res.reference,
        name: res.description
      })),
      catchError(() => {
        this.mockData.products.unshift(product);
        return of(product);
      })
    );
  }

  public createParty(party: BusinessParty): Observable<BusinessParty> {
    if (environment.useMockData) {
      this.mockData.parties.unshift(party);
      return of(party).pipe(delay(100));
    }
    if (party.partyType === 'Supplier') {
      const payload = {
        reference: party.id || party.code,
        name: party.name
      };
      return this.http.post<BackendSupplierItem>(`${this.baseUrl}/suppliers`, payload).pipe(
        map(res => ({
          ...party,
          id: res.reference,
          code: res.reference,
          name: res.name
        })),
        catchError(() => {
          this.mockData.parties.unshift(party);
          return of(party);
        })
      );
    }
    return this.http.post<BusinessParty>(`${this.baseUrl}/customers`, party).pipe(
      catchError(() => {
        this.mockData.parties.unshift(party);
        return of(party);
      })
    );
  }
}
