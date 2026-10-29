import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, map, catchError } from 'rxjs';
import { Warehouse, StockBalance, Mushak65Transfer, Mushak65TransferLine } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

export interface BackendWarehouseResponse {
  warehouseId: string;
  code: string;
  name: string;
  branchId: string;
  branchName: string;
  isVirtual: boolean;
}

export interface BackendStockBalanceResponse {
  balanceId: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  branchName: string;
  productId: string;
  productReference: string;
  productDescription: string;
  unitOfMeasure: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  lastMovementDate: string;
}

export interface CreateTransferRequest {
  transferNumber: string;
  dispatchDate: string;
  dispatchTime: string;
  fromWarehouseId: string;
  toWarehouseId: string;
  vehicleNumber: string;
  driverName: string;
  remarks?: string;
  lines: {
    productId: string;
    quantity: number;
    unitPrice: number;
    remarks?: string;
  }[];
}

export interface BackendTransferResponse {
  id: string;
  transferNumber: string;
  status: string;
  fromWarehouseId?: string;
  fromWarehouseCode: string;
  fromWarehouseName?: string;
  toWarehouseId?: string;
  toWarehouseCode: string;
  toWarehouseName?: string;
  vehicleNumber?: string;
  driverName?: string;
  remarks?: string;
  dispatchDate: string;
  dispatchTime?: string;
  createdBy?: string;
  approvedBy?: string;
  createdAt?: string;
  lines?: {
    productId: string;
    productReference: string;
    productDescription: string;
    unitOfMeasure: string;
    quantity: number;
    unitPrice: number;
    totalValue: number;
    remarks?: string;
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class InventoryApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);

  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}`;
  }

  /**
   * Fetch all warehouses configured for active company
   */
  public getWarehouses(): Observable<Warehouse[]> {
    if (environment.useMockData) {
      return of(this.getMockWarehouses()).pipe(delay(50));
    }

    return this.http.get<BackendWarehouseResponse[]>(`${this.baseUrl}/warehouses`).pipe(
      map(items => items.map(w => ({
        warehouseId: w.warehouseId,
        code: w.code,
        name: w.name,
        branchId: w.branchId,
        branchName: w.branchName,
        isVirtual: w.isVirtual
      }))),
      catchError(err => {
        console.warn('Backend warehouses endpoint unavailable, falling back to mock:', err);
        return of(this.getMockWarehouses());
      })
    );
  }

  /**
   * Fetch real-time stock balances across all warehouses or for a specific warehouse
   */
  public getAllStockBalances(warehouseId?: string): Observable<StockBalance[]> {
    if (environment.useMockData) {
      return of(this.getMockStockBalances(warehouseId)).pipe(delay(60));
    }

    let url = `${this.baseUrl}/inventory/all-balances`;
    if (warehouseId && warehouseId !== 'ALL') {
      url += `?warehouseId=${encodeURIComponent(warehouseId)}`;
    }

    return this.http.get<BackendStockBalanceResponse[]>(url).pipe(
      map(items => items.map(b => {
        const prod = this.mockData.products.find(p => p.code === b.productReference || p.name === b.productDescription);
        const cost = prod ? prod.standardCost : 150.0;
        const hsCode = prod ? prod.hsCode : '3901.20.00';
        return {
          balanceId: b.balanceId,
          warehouseId: b.warehouseId,
          warehouseCode: b.warehouseCode,
          warehouseName: b.warehouseName,
          branchName: b.branchName,
          productId: b.productId,
          productReference: b.productReference,
          productDescription: b.productDescription,
          unitOfMeasure: b.unitOfMeasure,
          quantity: b.quantity,
          reservedQuantity: b.reservedQuantity,
          availableQuantity: b.availableQuantity,
          lastMovementDate: b.lastMovementDate,
          standardCost: cost,
          totalValuation: b.quantity * cost,
          hsCode: hsCode
        };
      })),
      catchError(err => {
        console.warn('Backend all-balances endpoint unavailable, falling back to mock:', err);
        return of(this.getMockStockBalances(warehouseId));
      })
    );
  }

  /**
   * Fetch historical Mushak 6.5 goods transfer challans
   */
  public getTransfers(): Observable<Mushak65Transfer[]> {
    if (environment.useMockData) {
      return of([this.getMockDefaultTransfer()]).pipe(delay(60));
    }

    return this.http.get<BackendTransferResponse[]>(`${this.baseUrl}/transfers`).pipe(
      map(items => items.map(t => this.mapBackendTransfer(t))),
      catchError(err => {
        console.warn('Backend transfers endpoint unavailable, falling back to mock:', err);
        return of([this.getMockDefaultTransfer()]);
      })
    );
  }

  /**
   * Post a new Mushak 6.5 Transfer Challan & execute real-time stock balance debit/credit
   */
  public createTransfer(payload: CreateTransferRequest): Observable<BackendTransferResponse> {
    if (environment.useMockData) {
      const mockResp: BackendTransferResponse = {
        id: 'mock-' + Date.now(),
        transferNumber: payload.transferNumber,
        status: 'COMPLETED',
        fromWarehouseCode: 'WH-01',
        toWarehouseCode: 'WH-02-CTG',
        dispatchDate: payload.dispatchDate,
        dispatchTime: payload.dispatchTime,
        lines: payload.lines.map(l => ({
          productId: l.productId,
          productReference: 'PROD-01',
          productDescription: 'High Density Polyethylene (HDPE)',
          unitOfMeasure: 'KG',
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          totalValue: l.quantity * l.unitPrice,
          remarks: l.remarks
        }))
      };
      return of(mockResp).pipe(delay(200));
    }

    return this.http.post<BackendTransferResponse>(`${this.baseUrl}/transfers`, payload);
  }

  private mapBackendTransfer(t: any): Mushak65Transfer {
    return {
      id: t.id,
      transferNumber: t.transferNumber,
      dispatchDate: t.dispatchDate ? t.dispatchDate.split('T')[0] : '2026-10-23',
      dispatchTime: t.dispatchTime || '12:00',
      fromWarehouseId: t.fromWarehouseId,
      fromWarehouseCode: t.fromWarehouseCode,
      fromWarehouseName: t.fromWarehouse || t.fromWarehouseName,
      fromBranch: t.fromBranch || t.fromWarehouse || t.fromWarehouseCode,
      toWarehouseId: t.toWarehouseId,
      toWarehouseCode: t.toWarehouseCode,
      toWarehouseName: t.toWarehouse || t.toWarehouseName,
      toBranch: t.toBranch || t.toWarehouse || t.toWarehouseCode,
      vehicleNumber: t.vehicleNumber || 'কাভার্ড ভ্যান (ঢাকা মেট্রো-উ-১৪-২২১১)',
      driverName: t.driverName || 'মোঃ আনোয়ার হোসেন (01711-223344)',
      remarks: t.remarks || 'শাখা পণ্য স্থানান্তর চালান',
      status: t.state || t.status || 'COMPLETED',
      createdBy: t.createdBy,
      approvedBy: t.approvedBy,
      createdAt: t.createdAt,
      lines: (t.lines || []).map((l: any) => {
        const prodCode = l.productCode || l.productReference;
        const prodDesc = l.productName || l.productDescription;
        const prod = this.mockData.products.find(p => p.code === prodCode || p.name === prodDesc);
        const qty = Number(l.quantity || l.qty || 0);
        const price = Number(l.unitPrice || 0);
        return {
          productId: l.productId,
          productReference: prodCode,
          productDescription: prodDesc,
          productName: prodDesc,
          productNameBn: prod?.nameBn || prodDesc,
          hsCode: prod?.hsCode || '3901.20.00',
          uom: l.uom || l.unitOfMeasure || 'KG',
          quantity: qty,
          unitPrice: price,
          totalValue: Number(l.totalValue || (qty * price)),
          remarks: l.remarks || ''
        };
      })
    };
  }

  private getMockWarehouses(): Warehouse[] {
    return [
      {
        warehouseId: '55555555-5555-5555-5555-555555555551',
        code: 'WH-01',
        name: 'Dhaka Central Plant Main Warehouse',
        branchId: '33333333-3333-3333-3333-333333333331',
        branchName: 'Dhaka Central Plant & HQ',
        isVirtual: false
      },
      {
        warehouseId: '55555555-5555-5555-5555-555555555552',
        code: 'WH-02',
        name: 'Raw Materials Silo',
        branchId: '33333333-3333-3333-3333-333333333331',
        branchName: 'Dhaka Central Plant & HQ',
        isVirtual: false
      },
      {
        warehouseId: '77777777-7777-7777-7777-777777777772',
        code: 'WH-02-CTG',
        name: 'Chittagong Port Depot Warehouse',
        branchId: '33333333-3333-3333-3333-333333333332',
        branchName: 'Chittagong Port Branch & Depot',
        isVirtual: false
      },
      {
        warehouseId: '77777777-7777-7777-7777-777777777773',
        code: 'WH-03',
        name: 'Bogura Regional Warehouse',
        branchId: '33333333-3333-3333-3333-333333333333',
        branchName: 'Bogura Distribution Branch',
        isVirtual: false
      }
    ];
  }

  private getMockStockBalances(warehouseId?: string): StockBalance[] {
    return this.mockData.products.map(p => ({
      balanceId: 'bal-' + p.code,
      warehouseId: warehouseId || '55555555-5555-5555-5555-555555555551',
      warehouseCode: 'WH-01',
      warehouseName: 'Dhaka Central Plant Main Warehouse',
      branchName: 'Dhaka Central Plant & HQ',
      productId: p.id,
      productReference: p.code,
      productDescription: p.name,
      unitOfMeasure: p.uom,
      quantity: p.stockQty,
      reservedQuantity: 0,
      availableQuantity: p.stockQty,
      standardCost: p.standardCost,
      totalValuation: p.stockQty * p.standardCost,
      hsCode: p.hsCode
    }));
  }

  private getMockDefaultTransfer(): Mushak65Transfer {
    return {
      transferNumber: 'TRF-6.5-2026-0001',
      dispatchDate: '2026-10-23',
      dispatchTime: '15:30',
      fromWarehouseCode: 'WH-01',
      fromBranch: 'ঢাকা সেন্ট্রাল প্ল্যান্ট ও প্রধান কার্যালয় (WH-01)',
      toWarehouseCode: 'WH-02-CTG',
      toBranch: 'চট্টগ্রাম পোর্ট ডিপো (WH-02-CTG)',
      vehicleNumber: 'কাভার্ড ভ্যান (ঢাকা মেট্রো-উ-১৪-২২১১)',
      driverName: 'মোঃ আনোয়ার হোসেন (01711-223344)',
      remarks: 'চট্টগ্রাম পোর্ট ডিপো স্টক স্থানান্তর',
      status: 'COMPLETED',
      lines: [
        {
          productId: '66666666-6666-6666-6666-666666666661',
          productReference: 'PROD-01',
          productDescription: 'High Density Polyethylene (HDPE)',
          productName: 'High Density Polyethylene (HDPE)',
          productNameBn: 'হাই-ডেনসিটি পলিইথিলিন (এইচডিপিই)',
          hsCode: '3901.20.00',
          uom: 'KG',
          quantity: 200,
          unitPrice: 165.0,
          totalValue: 33000.0,
          remarks: 'ডিপো স্টক পূরণার্থে'
        }
      ]
    };
  }
}
