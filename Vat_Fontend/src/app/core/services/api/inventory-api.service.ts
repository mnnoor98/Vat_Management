import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, map, throwError } from 'rxjs';
import { Warehouse, StockBalance, Mushak65Transfer, InventoryValuationSummary } from '../../models/vat-models';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

export interface BackendWarehouseResponse {
  id: string;
  code: string;
  name: string;
  branchId: string;
  branchName: string;
}

export interface BackendStockBalanceResponse {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  branchId: string;
  branchName: string;
  productId: string;
  productReference: string;
  productDescription: string;
  unitOfMeasure: string;
  taxCode?: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  standardCost?: number;
  totalValuation?: number;
  hsCode?: string;
  costingMethod?: string;
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
  lines: { productId: string; quantity: number; unitPrice: number; remarks?: string }[];
}

export interface TransferResult {
  id: string;
  reference: string;
  state: string;
  version: number;
}

export type TransferAction = 'submit' | 'approve' | 'post' | 'cancel';

export interface BackendTransferResponse {
  id: string;
  transferNumber: string;
  state: string;
  version: number;
  fromWarehouseId: string;
  fromWarehouseCode: string;
  fromWarehouseName: string;
  fromBranchName: string;
  toWarehouseId: string;
  toWarehouseCode: string;
  toWarehouseName: string;
  toBranchName: string;
  vehicleNumber: string;
  driverName: string;
  remarks: string;
  dispatchDate: string;
  dispatchTime: string;
  createdBy: string;
  approvedBy?: string;
  createdAt: string;
  lines: {
    id: string;
    productId: string;
    productCode: string;
    productName: string;
    uom: string;
    quantity: number;
    unitPrice: number;
    totalValue: number;
  }[];
}

@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private http = inject(HttpClient);
  private context = inject(CompanyContextService);
  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}`;
  }

  getWarehouses(skip = 0, take = 100): Observable<Warehouse[]> {
    if (environment.useMockData) return of([]);
    return this.http.get<BackendWarehouseResponse[]>(`${this.baseUrl}/warehouses`, {
      params: { skip, take }
    }).pipe(
      map(items => items.map(w => ({
        warehouseId: w.id,
        code: w.code,
        name: w.name,
        branchId: w.branchId,
        branchName: w.branchName,
        isVirtual: false
      })))
    );
  }

  getAllStockBalances(
    warehouseId?: string,
    skip = 0,
    take = 100,
    method: 'WEIGHTED_AVERAGE' | 'FIFO' | string = 'WEIGHTED_AVERAGE'
  ): Observable<StockBalance[]> {
    if (environment.useMockData) return of([]);
    return this.http.get<BackendStockBalanceResponse[]>(`${this.baseUrl}/inventory/all-balances`, {
      params: {
        skip,
        take,
        method,
        ...(warehouseId && warehouseId !== 'ALL' ? { warehouseId } : {})
      }
    }).pipe(
      map(items => items.map(b => ({
        ...b,
        balanceId: `${b.warehouseId}:${b.productId}`
      })))
    );
  }

  getValuationSummary(method = 'WEIGHTED_AVERAGE'): Observable<InventoryValuationSummary> {
    return this.http.get<InventoryValuationSummary>(`${this.baseUrl}/inventory/valuation-summary`, {
      params: { method }
    });
  }

  getTransfers(skip = 0, take = 25): Observable<Mushak65Transfer[]> {
    if (environment.useMockData) return of([]);
    return this.http.get<BackendTransferResponse[]>(`${this.baseUrl}/transfers`, {
      params: { skip, take }
    }).pipe(
      map(items => items.map(t => ({
        id: t.id,
        version: t.version,
        transferNumber: t.transferNumber,
        status: t.state,
        dispatchDate: t.dispatchDate,
        dispatchTime: t.dispatchTime,
        fromWarehouseId: t.fromWarehouseId,
        fromWarehouseCode: t.fromWarehouseCode,
        fromWarehouseName: t.fromWarehouseName,
        fromBranch: t.fromBranchName,
        toWarehouseId: t.toWarehouseId,
        toWarehouseCode: t.toWarehouseCode,
        toWarehouseName: t.toWarehouseName,
        toBranch: t.toBranchName,
        vehicleNumber: t.vehicleNumber,
        driverName: t.driverName,
        remarks: t.remarks,
        createdBy: t.createdBy,
        approvedBy: t.approvedBy,
        createdAt: t.createdAt,
        lines: t.lines.map(l => ({
          productId: l.productId,
          productReference: l.productCode,
          productDescription: l.productName,
          productName: l.productName,
          uom: l.uom,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          totalValue: l.totalValue
        }))
      })))
    );
  }

  createTransfer(payload: CreateTransferRequest, idempotencyKey?: string): Observable<TransferResult> {
    if (environment.useMockData) return throwError(() => new Error('Transfer commands require the live API.'));
    return this.http.post<TransferResult>(
      `${this.baseUrl}/transfers`,
      payload,
      idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : {}
    );
  }

  transitionTransfer(id: string, action: TransferAction, expectedVersion: number, reason?: string): Observable<TransferResult> {
    if (environment.useMockData) return throwError(() => new Error('Transfer commands require the live API.'));
    return this.http.post<TransferResult>(
      `${this.baseUrl}/transfers/${id}/${action}`,
      { expectedVersion, ...(reason !== undefined ? { reason } : {}) }
    );
  }
}
