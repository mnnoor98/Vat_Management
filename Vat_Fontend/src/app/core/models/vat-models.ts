// Enterprise VAT Models - Bangladesh NBR Compliance

export type DocumentStatus = 'Draft' | 'Submitted' | 'Verified' | 'Approved' | 'Posted' | 'Rejected' | 'Cancelled' | 'PostingPrepared' | 'Reserved' | 'ReversalPending' | 'Reversed' | 'CreditPending' | 'Credited' | 'PartiallyCredited' | 'DebitPending';

export interface Company {
  id: string;
  name: string;
  nameBn: string;
  bin: string; // Business Identification Number (13 digits or 9 digits)
  tin: string;
  address: string;
  addressBn: string;
  vatCircle: string;
  vatDivision: string;
  vatCommissioner: string;
  currency: string;
  isActive: boolean;
}

export interface Branch {
  id: string;
  companyId: string;
  code: string;
  name: string;
  nameBn: string;
  binExtension: string; // e.g., 0101, 0102
  address: string;
  warehouseId?: string;
  isHeadOffice: boolean;
}

export interface FiscalPeriod {
  id: string;
  fiscalYear: string; // e.g., "2026-2027"
  month: string;      // e.g., "October"
  monthBn: string;    // "অক্টোবর"
  periodCode: string; // "2026-10"
  startDate: string;
  endDate: string;
  isClosed: boolean;
  dueDate: string;
}

export type EnterpriseRole = 'User' | 'Maker' | 'Checker' | 'Approver' | 'TaxManager' | 'Auditor' | 'SuperAdmin';

export interface UserProfile {
  id: string;
  username: string;
  fullName: string;
  fullNameBn: string;
  email: string;
  role: EnterpriseRole;
  roles?: EnterpriseRole[];
  accessibleCompanyIds: string[];
  permissions: string[];
  branchId?: string;
  branchName?: string;
  assignedModuleCodes?: string[];
}

export interface MenuItem {
  id: number;
  parentId: number | null;
  code: string;
  labelEn: string;
  labelBn: string;
  route: string;
  icon: string;
  permission?: string;
  order: number;
  badgeCount?: number;
  children?: MenuItem[];
}

export interface ProductItem {
  taxCode?: string;
  metadataOnly?: boolean;
  id: string;
  code: string;
  name: string;
  nameBn: string;
  hsCode: string; // Harmonized System Code e.g. "8471.30.00"
  uom: string;    // e.g., "PCS", "KG", "LTR", "BAG"
  uomBn: string;
  standardCost: number;
  salesPrice: number;
  vatRatePercent: number; // 15%, 7.5%, 5%, 0%
  supplementaryDutyPercent: number; // SD %
  isExempt: boolean;
  isZeroRated: boolean;
  vdsApplicable: boolean;
  stockQty: number;
}

export interface BusinessParty {
  version?: number;
  isActive?: boolean;
  id: string;
  partyType: 'Customer' | 'Supplier';
  code: string;
  name: string;
  nameBn: string;
  bin: string;
  tin?: string;
  address: string;
  addressBn: string;
  contactPerson: string;
  phone: string;
  email: string;
  isVdsEntity: boolean; // Government, Bank, Large Taxpayer Unit
}

export interface PurchaseLine {
  productReference?: string;
  id: string;
  productId: string;
  productName: string;
  productNameBn: string;
  hsCode: string;
  uom: string;
  quantity: number;
  unitPrice: number;
  taxableAmount: number;
  vatRatePercent: number;
  vatAmount: number;
  supplementaryDutyAmount: number;
  advanceTaxAmount: number; // AT usually 5% on import
  vdsAmount: number;
  totalAmount: number;
  isCreditEligible: boolean; // Section 46 eligibility
}

export interface PurchaseInvoice {
  version?: number;
  taxCalculated?: boolean;
  warehouseId?: string;
  postingPlanId?: string;
  id: string;
  invoiceNumber: string;
  supplierInvoiceNumber: string;
  supplierId: string;
  supplierName: string;
  supplierBin: string;
  purchaseType: 'Local' | 'Import';
  billOfEntryNo?: string;
  billOfEntryDate?: string;
  customsStation?: string;
  invoiceDate: string;
  fiscalPeriod: string;
  companyId: string;
  branchId: string;
  status: DocumentStatus;
  lines: PurchaseLine[];
  subTotal: number;
  totalVat: number;
  totalSupplementaryDuty: number;
  totalAdvanceTax: number;
  totalVds: number;
  grandTotal: number;
  bankPaymentRef?: string;
  createdAt: string;
  createdBy: string;
  approvedBy?: string;
  approvedAt?: string;
  remarks?: string;
}

export interface SalesLine {
  productReference?: string;
  id: string;
  productId: string;
  productName: string;
  productNameBn: string;
  hsCode: string;
  uom: string;
  quantity: number;
  unitPrice: number;
  taxableAmount: number;
  vatRatePercent: number;
  vatAmount: number;
  supplementaryDutyAmount: number;
  vdsAmount: number;
  totalAmount: number;
}

export interface SalesInvoice {
  version?: number;
  taxCalculated?: boolean;
  warehouseId?: string;
  postingPlanId?: string;
  id: string;
  invoiceNumber: string;
  challanNumber63: string; // Official Mushak 6.3 Serial
  customerId: string;
  customerName: string;
  customerNameBn: string;
  customerBin: string;
  customerAddress: string;
  destinationAddress: string;
  vehicleTypeAndNo?: string;
  deliveryDate: string;
  deliveryTime: string;
  salesType: 'Standard' | 'ZeroRated' | 'Exempt' | 'Retail' | 'DeemedExport';
  fiscalPeriod: string;
  companyId: string;
  branchId: string;
  status: DocumentStatus;
  lines: SalesLine[];
  subTotal: number;
  totalVat: number;
  totalSupplementaryDuty: number;
  totalVdsDeducted: number;
  grandTotal: number;
  createdAt: string;
  createdBy: string;
  remarks?: string;
}

export interface ApprovalTask {
  id: string;
  documentType: 'PurchaseInvoice' | 'SalesInvoice' | 'VatReturn' | 'Coefficient43' | 'CreditNote';
  documentId: string;
  documentNumber: string;
  partyName: string;
  amount: number;
  vatAmount: number;
  requestedBy: string;
  requestedAt: string;
  status: 'Pending' | 'Approved' | 'Rejected' | 'SentBack';
  priority: 'Normal' | 'High' | 'Urgent';
  notes?: string;
  branchId?: string;
}

export interface Coefficient43Line {
  id: string;
  rawMaterialId: string;
  rawMaterialName: string;
  hsCode: string;
  uom: string;
  qtyRequired: number;
  wastagePercent: number;
  wastageQty: number;
  unitPrice: number;
  totalCost: number;
}

export interface Coefficient43 {
  id: string;
  declarationNumber: string;
  finishedProductId: string;
  finishedProductName: string;
  hsCode: string;
  uom: string;
  batchSize: number;
  effectiveFrom: string;
  submissionDate: string;
  materials: Coefficient43Line[];
  totalRawMaterialCost: number;
  directLaborCost: number;
  utilityCost: number;
  overheadCost: number;
  marginPercent: number;
  finalSellingPrice: number;
  status: DocumentStatus;
  approvedBy?: string;
  branchId?: string;
  branchName?: string;
}

export interface Mushak91Return {
  id: string;
  fiscalPeriod: string;
  companyId: string;
  companyName: string;
  bin: string;
  branchId?: string;
  branchName?: string;
  returnType: 'Main' | 'Amended' | 'Final';
  revision?: number;
  status: DocumentStatus;
  submissionDate: string;
  // Part 3: Output VAT
  totalStandardSales: number;
  totalStandardOutputVat: number;
  totalZeroRatedSales: number;
  totalExemptSales: number;
  totalOutputVat: number;
  totalSupplementaryDuty: number;
  // Part 4: Input VAT Credit
  totalStandardPurchases: number;
  totalInputVatClaimable: number;
  totalImportPurchases: number;
  totalImportVatClaimable: number;
  totalInputVatCredit: number;
  // Part 5: Increasing Adjustments
  vdsDeductedFromSuppliers: number; // VDS to pay
  debitNoteAdjustments: number;
  totalIncreasingAdjustments: number;
  // Part 6: Decreasing Adjustments
  vdsDeductedByCustomers: number; // Certificate 6.6 received
  creditNoteAdjustments: number;
  totalDecreasingAdjustments: number;
  // Part 7: Net VAT Payable
  netTaxPayable: number;
  // Part 8: Treasury Payment
  treasuryDepositAmount: number;
  treasuryChallanNo?: string;
  treasuryDepositDate?: string;
  closingBalance: number;
  filingReference?: string;
}

export interface Warehouse {
  warehouseId: string;
  code: string;
  name: string;
  branchId: string;
  branchName: string;
  isVirtual: boolean;
}

export interface StockBalance {
  branchId?: string;
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
  lastMovementDate?: string;
  standardCost?: number;
  totalValuation?: number;
  hsCode?: string;
  costingMethod?: string;
}

export interface WarehouseValuationSummary {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  branchId: string;
  branchName: string;
  itemsCount: number;
  totalQuantity: number;
  totalValuation: number;
}

export interface InventoryValuationSummary {
  companyId: string;
  costingMethod: 'WEIGHTED_AVERAGE' | 'FIFO' | string;
  totalStockQuantity: number;
  totalStockValuation: number;
  totalWarehousesCount: number;
  totalUniqueProductsCount: number;
  warehouses: WarehouseValuationSummary[];
  topValuedItems: StockBalance[];
}

export interface Mushak65TransferLine {
  productId: string;
  productReference?: string;
  productDescription?: string;
  productName?: string;
  productNameBn?: string;
  hsCode?: string;
  uom?: string;
  quantity: number;
  unitPrice: number;
  totalValue: number;
  remarks?: string;
}

export interface Mushak65Transfer {
  version?: number;
  id?: string;
  transferNumber: string;
  dispatchDate: string;
  dispatchTime: string;
  fromWarehouseId?: string;
  fromWarehouseCode?: string;
  fromWarehouseName?: string;
  fromBranch?: string;
  toWarehouseId?: string;
  toWarehouseCode?: string;
  toWarehouseName?: string;
  toBranch?: string;
  vehicleNumber: string;
  driverName: string;
  remarks?: string;
  status?: string;
  createdBy?: string;
  approvedBy?: string;
  createdAt?: string;
  lines: Mushak65TransferLine[];
}

export interface VdsCertificate {
  id: string;
  certificateNumber: string;
  purchaseId?: string;
  supplierName: string;
  supplierBin: string;
  supplierAddress?: string;
  invoiceNumber: string;
  invoiceDate: string;
  totalAmount: number;
  vatAmount: number;
  vdsRate: number;
  vdsAmount: number;
  treasuryChallanNo?: string;
  treasuryChallanDate?: string;
  bankBranch?: string;
  economicCode?: string;
  issueDate: string;
  status: 'Issued' | 'Deposited' | 'Cancelled';
}

export interface VatLedgerEntry {
  id: string;
  entryDate: string;
  transactionType: 'OpeningBalance' | 'TreasuryDeposit' | 'PurchaseRebate' | 'SalesOutputVat' | 'VdsWithheld' | 'CreditNote' | 'DebitNote';
  referenceNumber: string;
  description: string;
  debitAmount: number;
  creditAmount: number;
  balance: number;
  statutoryForm: string;
  bankBranch?: string;
  economicCode?: string;
  status: string;
  createdBy?: string;
}

export interface VatLedgerSummary {
  openingBalance: number;
  totalTreasuryDeposited: number;
  totalInputCreditClaimed: number;
  totalOutputTaxDebited: number;
  totalAdjustments: number;
  closingBalance: number;
  filingReference?: string;
}

export interface VatLedgerResponse {
  period: string;
  summary: VatLedgerSummary;
  entries: VatLedgerEntry[];
}

export interface CreditNote {
  id: string;
  documentNumber: string;
  originalDocumentNumber: string;
  correctionDate: string;
  reason: string;
  netAmount: number;
  outputTaxAmount: number;
  status: string;
  requestedBy?: string;
  approvedBy?: string;
}

export interface DebitNote {
  id: string;
  documentNumber: string;
  originalDocumentNumber: string;
  correctionDate: string;
  reason: string;
  netAmount: number;
  outputTaxAmount: number;
  status: string;
  requestedBy?: string;
  approvedBy?: string;
}

export interface AdjustmentsResponse {
  period: string;
  creditNotes: CreditNote[];
  debitNotes: DebitNote[];
}


