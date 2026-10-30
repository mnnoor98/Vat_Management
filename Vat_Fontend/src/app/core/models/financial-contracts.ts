export interface CalculationLine {
  productReference: string;
  productId: string;
  description: string;
  unitOfMeasure: string;
  quantity: number;
  unitPrice: number;
  netAmount: number;
  ratePercent: number;
  taxAmount?: number;
  outputTaxAmount?: number;
  recoverableTaxAmount?: number;
  recoverablePercent?: number;
  taxRuleId: string;
  taxRuleVersion: number;
  legalBasis: string;
  evidenceReference: string;
}
export interface CalculationSnapshot {
  lines: CalculationLine[];
  netAmount: number;
  taxAmount?: number;
  outputTaxAmount?: number;
  grossAmount: number;
  supplierName?: string;
}
export interface FinancialPlan {
  id?: string;
  warehouseId: string;
  status: string;
  sourceVersion: number;
  snapshot: CalculationSnapshot;
  preparedBy?: string;
  reservedBy?: string;
}
export interface InvoicePage { before?: string; take?: number; }
