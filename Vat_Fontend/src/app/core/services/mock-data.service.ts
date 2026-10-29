import { Injectable } from '@angular/core';
import {
  Company, Branch, FiscalPeriod, UserProfile, MenuItem,
  ProductItem, BusinessParty, PurchaseInvoice, SalesInvoice,
  ApprovalTask, Coefficient43, Mushak91Return
} from '../models/vat-models';

@Injectable({
  providedIn: 'root'
})
export class MockDataService {

  public readonly companies: Company[] = [
    {
      id: 'COMP-001',
      name: 'Padma Consumer Goods & Manufacturing Ltd',
      nameBn: 'পদ্মা কনজিউমার গুডস অ্যান্ড ম্যানুফ্যাকচারিং লিমিটেড',
      bin: '001234567-0101',
      tin: '198273645123',
      address: 'Tejgaon Industrial Area, Dhaka-1208, Bangladesh',
      addressBn: 'তেজগাঁও শিল্প এলাকা, ঢাকা-১২০৮, বাংলাদেশ',
      vatCircle: 'Circle-12 (Tejgaon)',
      vatDivision: 'Dhaka North',
      vatCommissioner: 'Customs, Excise & VAT Commissionerate, Dhaka North',
      currency: 'BDT',
      isActive: true
    },
    {
      id: 'COMP-002',
      name: 'Meghna Trading & Distribution Corp',
      nameBn: 'মেঘনা ট্রেডিং অ্যান্ড ডিস্ট্রিবিউশন কর্পোরেশন',
      bin: '002345678-0102',
      tin: '287364519823',
      address: 'Agrabad Commercial Area, Chittagong-4100, Bangladesh',
      addressBn: 'আগ্রাবাদ বাণিজ্যিক এলাকা, চট্টগ্রাম-৪১০০, বাংলাদেশ',
      vatCircle: 'Circle-04 (Agrabad)',
      vatDivision: 'Chittagong South',
      vatCommissioner: 'Customs, Excise & VAT Commissionerate, Chittagong',
      currency: 'BDT',
      isActive: true
    }
  ];

  public readonly branches: Branch[] = [
    {
      id: 'BR-01',
      companyId: 'COMP-001',
      code: 'DHK-HQ',
      name: 'Dhaka Central Plant & HQ',
      nameBn: 'ঢাকা সেন্ট্রাল প্ল্যান্ট ও প্রধান কার্যালয়',
      binExtension: '0101',
      address: 'Plot 45, Tejgaon I/A, Dhaka',
      warehouseId: 'WH-01',
      isHeadOffice: true
    },
    {
      id: 'BR-02',
      companyId: 'COMP-001',
      code: 'CTG-DEPOT',
      name: 'Chittagong Port Depot',
      nameBn: 'চট্টগ্রাম পোর্ট ডিপো',
      binExtension: '0102',
      address: 'Port Access Road, Halishahar, Chittagong',
      warehouseId: 'WH-02',
      isHeadOffice: false
    },
    {
      id: 'BR-03',
      companyId: 'COMP-001',
      code: 'BGR-DEPOT',
      name: 'Bogura Regional Warehouse',
      nameBn: 'বগুড়া আঞ্চলিক গুদাম',
      binExtension: '0103',
      address: 'Sherpur Road, Bogura',
      warehouseId: 'WH-03',
      isHeadOffice: false
    }
  ];

  public readonly fiscalPeriods: FiscalPeriod[] = [
    {
      id: 'FP-2026-10',
      fiscalYear: '2026-2027',
      month: 'October',
      monthBn: 'অক্টোবর ২০২৬',
      periodCode: '2026-10',
      startDate: '2026-10-01',
      endDate: '2026-10-31',
      isClosed: false,
      dueDate: '2026-11-15'
    },
    {
      id: 'FP-2026-09',
      fiscalYear: '2026-2027',
      month: 'September',
      monthBn: 'সেপ্টেম্বর ২০২৬',
      periodCode: '2026-09',
      startDate: '2026-09-01',
      endDate: '2026-09-30',
      isClosed: true,
      dueDate: '2026-10-15'
    }
  ];

  public readonly availableUsers: UserProfile[] = [
    {
      id: 'USR-8801',
      username: 'tariqul.vat',
      fullName: 'Tariqul Islam, FCA',
      fullNameBn: 'তারিকুল ইসলাম, এফসিএ',
      email: 'tariqul.vat@padmagroup.com.bd',
      role: 'TaxManager',
      accessibleCompanyIds: ['COMP-001', 'COMP-002'],
      permissions: [
        'dashboard.read', 'purchase.invoice.read', 'purchase.invoice.create', 'purchase.invoice.approve',
        'sales.invoice.read', 'sales.invoice.create', 'sales.challan.print',
        'manufacturing.bom.read', 'manufacturing.coefficient.read', 'manufacturing.coefficient.create',
        'inventory.stock.read', 'inventory.transfer.create',
        'vat.ledger.read', 'compliance.return.read', 'compliance.return.submit',
        'workflow.task.read', 'workflow.task.approve',
        'reports.statutory.read', 'security.admin.read'
      ]
    },
    {
      id: 'USR-8802',
      username: 'rafiqul.maker',
      fullName: 'Rafiqul Islam',
      fullNameBn: 'রফিকুল ইসলাম',
      email: 'rafiqul.maker@padmagroup.com.bd',
      role: 'Maker',
      accessibleCompanyIds: ['COMP-001'],
      permissions: [
        'dashboard.read',
        'purchase.invoice.read', 'purchase.invoice.create',
        'sales.invoice.read', 'sales.invoice.create', 'sales.challan.print',
        'inventory.stock.read', 'inventory.transfer.create',
        'workflow.task.read'
      ]
    },
    {
      id: 'USR-8803',
      username: 'nusrat.checker',
      fullName: 'Nusrat Jahan',
      fullNameBn: 'নুসরাত জাহান',
      email: 'nusrat.checker@padmagroup.com.bd',
      role: 'Checker',
      accessibleCompanyIds: ['COMP-001', 'COMP-002'],
      permissions: [
        'dashboard.read',
        'purchase.invoice.read',
        'sales.invoice.read', 'sales.challan.print',
        'inventory.stock.read',
        'workflow.task.read', 'workflow.task.approve'
      ]
    },
    {
      id: 'USR-8804',
      username: 'mahbub.approver',
      fullName: 'Engr. Mahbubur Rahman',
      fullNameBn: 'প্রকৌ. মাহবুবুর রহমান',
      email: 'mahbub.production@padmagroup.com.bd',
      role: 'Approver',
      accessibleCompanyIds: ['COMP-001'],
      permissions: [
        'dashboard.read',
        'manufacturing.bom.read', 'manufacturing.coefficient.read', 'manufacturing.coefficient.create',
        'workflow.task.read', 'workflow.task.approve'
      ]
    },
    {
      id: 'USR-8805',
      username: 'shamsul.auditor',
      fullName: 'Shamsul Huda',
      fullNameBn: 'শামসুল হুদা',
      email: 'auditor@external-tax.com.bd',
      role: 'Auditor',
      accessibleCompanyIds: ['COMP-001', 'COMP-002'],
      permissions: [
        'dashboard.read',
        'purchase.invoice.read',
        'sales.invoice.read',
        'vat.ledger.read', 'compliance.return.read',
        'reports.statutory.read'
      ]
    }
  ];

  public readonly currentUser: UserProfile = this.availableUsers[0];

  public readonly menuItems: MenuItem[] = [
    {
      id: 1, parentId: null, code: 'MENU_DASHBOARD',
      labelEn: 'Executive Dashboard', labelBn: 'ড্যাশবোর্ড',
      route: '/app/dashboard', icon: 'dashboard', order: 1
    },
    {
      id: 2, parentId: null, code: 'MENU_PURCHASES',
      labelEn: 'Purchases & Import', labelBn: 'ক্রয় ও আমদানি',
      route: '/app/purchases', icon: 'shopping_cart', order: 2,
      children: [
        { id: 21, parentId: 2, code: 'SUB_PURCHASE_LIST', labelEn: 'Purchase Invoices', labelBn: 'ক্রয় চালান তালিকা', route: '/app/purchases/invoices', icon: 'receipt', order: 1 },
        { id: 22, parentId: 2, code: 'SUB_PURCHASE_NEW', labelEn: '+ New Purchase Entry', labelBn: '+ নতুন ক্রয় এন্ট্রি', route: '/app/purchases/new', icon: 'add_circle', order: 2 },
        { id: 23, parentId: 2, code: 'SUB_MUSHAK_61', labelEn: 'Mushak 6.1 (Purchase Book)', labelBn: 'মূসক ৬.১ ক্রয় রেজিস্টার', route: '/app/purchases/mushak-6-1', icon: 'menu_book', order: 3 }
      ]
    },
    {
      id: 3, parentId: null, code: 'MENU_SALES',
      labelEn: 'Sales & Billing (6.3)', labelBn: 'বিক্রয় ও বিলিং (৬.৩)',
      route: '/app/sales', icon: 'point_of_sale', order: 3,
      children: [
        { id: 31, parentId: 3, code: 'SUB_SALES_LIST', labelEn: 'Sales Invoices', labelBn: 'বিক্রয় ইনভয়েস', route: '/app/sales/invoices', icon: 'format_list_bulleted', order: 1 },
        { id: 32, parentId: 3, code: 'SUB_SALES_NEW', labelEn: '+ Create Sales Challan', labelBn: '+ নতুন বিক্রয় চালান', route: '/app/sales/new', icon: 'post_add', order: 2 },
        { id: 33, parentId: 3, code: 'SUB_MUSHAK_63', labelEn: 'Mushak 6.3 Challan Preview', labelBn: 'মূসক ৬.৩ কর চালান প্রিন্ট', route: '/app/sales/challan-preview', icon: 'print', order: 3 },
        { id: 34, parentId: 3, code: 'SUB_MUSHAK_62', labelEn: 'Mushak 6.2 (Sales Book)', labelBn: 'মূসক ৬.২ বিক্রয় রেজিস্টার', route: '/app/sales/mushak-6-2', icon: 'auto_stories', order: 4 }
      ]
    },
    {
      id: 4, parentId: null, code: 'MENU_MANUFACTURING',
      labelEn: 'Manufacturing & 4.3', labelBn: 'উৎপাদন ও সহগ (৪.৩)',
      route: '/app/manufacturing', icon: 'factory', order: 4,
      children: [
        { id: 41, parentId: 4, code: 'SUB_COEFF_43', labelEn: 'Mushak 4.3 Coefficient', labelBn: 'মূসক ৪.৩ সহগ ঘোষণা', route: '/app/manufacturing/coefficient-4-3', icon: 'calculate', order: 1 },
        { id: 42, parentId: 4, code: 'SUB_BOM', labelEn: 'Bill of Materials (BOM)', labelBn: 'বিওএম তালিকা', route: '/app/manufacturing/bom', icon: 'account_tree', order: 2 }
      ]
    },
    {
      id: 5, parentId: null, code: 'MENU_INVENTORY',
      labelEn: 'Inventory & Stock (6.5)', labelBn: 'ইনভেন্টরি ও স্টক (৬.৫)',
      route: '/app/inventory', icon: 'inventory_2', order: 5,
      children: [
        { id: 51, parentId: 5, code: 'SUB_STOCK_SUMMARY', labelEn: 'Live Stock Balance', labelBn: 'বর্তমান স্টক হিসাব', route: '/app/inventory/summary', icon: 'analytics', order: 1 },
        { id: 52, parentId: 5, code: 'SUB_MUSHAK_65', labelEn: 'Mushak 6.5 (Branch Transfer)', labelBn: 'মূসক ৬.৫ স্থানান্তর চালান', route: '/app/inventory/transfers', icon: 'local_shipping', order: 2 }
      ]
    },
    {
      id: 6, parentId: null, code: 'MENU_COMPLIANCE',
      labelEn: 'VAT Returns & NBR (9.1)', labelBn: 'ভ্যাট রিটার্ন ও এনবিআর (৯.১)',
      route: '/app/compliance', icon: 'verified_user', order: 6,
      children: [
        { id: 61, parentId: 6, code: 'SUB_MUSHAK_91', labelEn: 'Mushak 9.1 Monthly Return', labelBn: 'মূসক ৯.১ মাসিক রিটার্ন', route: '/app/compliance/return-9-1', icon: 'assignment', order: 1 },
        { id: 62, parentId: 6, code: 'SUB_TAX_LEDGER', labelEn: 'Current Tax Ledger', labelBn: 'চলতি ভ্যাট লেজার', route: '/app/compliance/tax-ledger', icon: 'account_balance', order: 2 },
        { id: 63, parentId: 6, code: 'SUB_MUSHAK_66', labelEn: 'Mushak 6.6 (VDS Certificate)', labelBn: 'মূসক ৬.৬ উৎস কর্তন সনদ', route: '/app/compliance/vds-certificates', icon: 'badge', order: 3 },
        { id: 64, parentId: 6, code: 'SUB_MUSHAK_610', labelEn: 'Mushak 6.10 (>2 Lakh BDT)', labelBn: 'মূসক ৬.১০ (২ লক্ষাধিক চালান)', route: '/app/compliance/mushak-6-10', icon: 'warning', order: 4 }
      ]
    },
    {
      id: 7, parentId: null, code: 'MENU_WORKFLOW',
      labelEn: 'Approval Workbench', labelBn: 'অনুমোদন ওয়ার্কবেঞ্চ',
      route: '/app/workflow/my-tasks', icon: 'task_alt', order: 7, badgeCount: 14
    },
    {
      id: 8, parentId: null, code: 'MENU_MASTER_DATA',
      labelEn: 'Master Data Catalog', labelBn: 'মাস্টার ডাটা',
      route: '/app/master-data', icon: 'dataset', order: 8,
      children: [
        { id: 81, parentId: 8, code: 'SUB_MASTER_PRODUCTS', labelEn: 'Items & Products', labelBn: 'পণ্য ও কাঁচামাল ক্যাটালগ', route: '/app/master-data', icon: 'category', order: 1 },
        { id: 82, parentId: 8, code: 'SUB_MASTER_PROD_NEW', labelEn: '+ New Product / Item', labelBn: '+ নতুন পণ্য এন্ট্রি', route: '/app/master-data/new-product', icon: 'add_circle', order: 2 },
        { id: 83, parentId: 8, code: 'SUB_MASTER_PARTIES', labelEn: 'Customers & Suppliers', labelBn: 'গ্রাহক ও সরবরাহকারী', route: '/app/master-data/parties', icon: 'people', order: 3 },
        { id: 84, parentId: 8, code: 'SUB_MASTER_PARTY_NEW', labelEn: '+ Register Business Party', labelBn: '+ নতুন পার্টনার এন্ট্রি', route: '/app/master-data/new-party', icon: 'person_add', order: 4 }
      ]
    },
    {
      id: 9, parentId: null, code: 'MENU_REPORTS',
      labelEn: 'Statutory Reports Hub', labelBn: 'রিপোর্ট সেন্টার',
      route: '/app/reports', icon: 'bar_chart', order: 9
    },
    {
      id: 10, parentId: null, code: 'MENU_ADMIN',
      labelEn: 'Administration & Users', labelBn: 'প্রশাসন ও ব্যবহারকারী',
      route: '/app/admin', icon: 'settings', order: 10,
      children: [
        { id: 101, parentId: 10, code: 'SUB_ADMIN_USERS', labelEn: 'Users & Logins', labelBn: 'ব্যবহারকারী ও লগইন', route: '/app/admin', icon: 'people', order: 1 },
        { id: 102, parentId: 10, code: 'SUB_ADMIN_COMPANY', labelEn: 'Company Profile & BIN', labelBn: 'কোম্পানি পরিচিতি ও বিআইএন', route: '/app/admin', icon: 'business', order: 2 }
      ]
    }
  ];

  public products: ProductItem[] = [
    {
      id: 'PROD-01',
      code: 'RM-PET-01',
      name: 'Virgin PET Resin Pellets (Food Grade)',
      nameBn: 'ভার্জিন পিইটি রেজিন পেলেট (ফুড গ্রেড)',
      hsCode: '3907.61.00',
      uom: 'KG',
      uomBn: 'কেজি',
      standardCost: 140.00,
      salesPrice: 0,
      vatRatePercent: 15,
      supplementaryDutyPercent: 0,
      isExempt: false,
      isZeroRated: false,
      vdsApplicable: true,
      stockQty: 85400
    },
    {
      id: 'PROD-02',
      code: 'RM-OIL-02',
      name: 'Raw Unrefined Mustard Seeds',
      nameBn: 'অপরিশোধিত সরিষা বীজ',
      hsCode: '1207.50.90',
      uom: 'KG',
      uomBn: 'কেজি',
      standardCost: 95.00,
      salesPrice: 0,
      vatRatePercent: 15,
      supplementaryDutyPercent: 0,
      isExempt: false,
      isZeroRated: false,
      vdsApplicable: false,
      stockQty: 42000
    },
    {
      id: 'PROD-03',
      code: 'FG-WTR-500',
      name: 'Padma Pure Bottled Water 500ml (24-Pack)',
      nameBn: 'পদ্মা পিওর বোতলজাত পানি ৫০০ মি.লি. (২৪ প্যাক)',
      hsCode: '2201.10.00',
      uom: 'CTN',
      uomBn: 'কার্টুন',
      standardCost: 180.00,
      salesPrice: 360.00,
      vatRatePercent: 15,
      supplementaryDutyPercent: 0,
      isExempt: false,
      isZeroRated: false,
      vdsApplicable: false,
      stockQty: 5400
    },
    {
      id: 'PROD-04',
      code: 'FG-OIL-1L',
      name: 'Padma Gold Mustard Oil 1 Litre Bottle',
      nameBn: 'পদ্মা গোল্ড খাঁটি সরিষার তেল ১ লিটার বোতল',
      hsCode: '1514.91.00',
      uom: 'BTL',
      uomBn: 'বোতল',
      standardCost: 185.00,
      salesPrice: 280.00,
      vatRatePercent: 15,
      supplementaryDutyPercent: 0,
      isExempt: false,
      isZeroRated: false,
      vdsApplicable: false,
      stockQty: 3200
    },
    {
      id: 'PROD-05',
      code: 'TR-FILTER-01',
      name: 'Commercial RO Membrane Filter 4040',
      nameBn: 'বাণিজ্যিক আরও মেমব্রেন ফিল্টার ৪০৪০',
      hsCode: '8421.21.00',
      uom: 'PCS',
      uomBn: 'পিস',
      standardCost: 8500.00,
      salesPrice: 13500.00,
      vatRatePercent: 15,
      supplementaryDutyPercent: 0,
      isExempt: false,
      isZeroRated: false,
      vdsApplicable: true,
      stockQty: 120
    }
  ];

  public parties: BusinessParty[] = [
    {
      id: 'PTY-SUP-01',
      partyType: 'Supplier',
      code: 'SUP-BENGAL-01',
      name: 'Bengal Plastic & Chemical Industries Ltd',
      nameBn: 'বেঙ্গল প্লাস্টিক অ্যান্ড কেমিক্যাল ইন্ডাস্ট্রিজ লি.',
      bin: '001928374-0101',
      address: 'Hemayetpur, Savar, Dhaka',
      addressBn: 'হেমায়েতপুর, সাভার, ঢাকা',
      contactPerson: 'Md. Shafiqul Alam',
      phone: '+880 1711-234567',
      email: 'sales@bengalplastic.com.bd',
      isVdsEntity: true
    },
    {
      id: 'PTY-SUP-02',
      partyType: 'Supplier',
      code: 'SUP-AGRO-02',
      name: 'North Bengal Agro Seed Growers Ltd',
      nameBn: 'নর্থ বেঙ্গল এগ্রো সিড গ্রোয়ার্স লি.',
      bin: '003847291-0102',
      address: 'Station Road, Dinajpur',
      addressBn: 'স্টেশন রোড, দিনাজপুর',
      contactPerson: 'Zahangir Hossain',
      phone: '+880 1819-876543',
      email: 'agro@northbengal.bd',
      isVdsEntity: false
    },
    {
      id: 'PTY-CUST-01',
      partyType: 'Customer',
      code: 'CUST-SHWAPNO-01',
      name: 'ACI Logistics Ltd (Shwapno Super Shop)',
      nameBn: 'এসিআই লজিস্টিকস লি. (স্বপ্ন সুপার শপ)',
      bin: '000456123-0101',
      address: 'Tejgaon I/A, Dhaka-1208',
      addressBn: 'তেজগাঁও শিল্প এলাকা, ঢাকা-১২০৮',
      contactPerson: 'Kamrul Hassan',
      phone: '+880 1912-345678',
      email: 'procurement@shwapno.com',
      isVdsEntity: true
    },
    {
      id: 'PTY-CUST-02',
      partyType: 'Customer',
      code: 'CUST-PRAN-02',
      name: 'Pran Dairy & Consumer Distributors Ltd',
      nameBn: 'প্রাণ ডেইরি অ্যান্ড কনজিউমার ডিস্ট্রিবিউটরস লি.',
      bin: '000987321-0101',
      address: 'Middle Badda, Dhaka',
      addressBn: 'মধ্য বাড্ডা, ঢাকা',
      contactPerson: 'Tanvir Ahmed',
      phone: '+880 1714-556677',
      email: 'supply@pran.com.bd',
      isVdsEntity: true
    }
  ];

  public purchaseInvoices: PurchaseInvoice[] = [
    {
      id: 'PUR-2026-1024',
      invoiceNumber: 'PUR-1024',
      supplierInvoiceNumber: 'BEN-INV-8910',
      supplierId: 'PTY-SUP-01',
      supplierName: 'Bengal Plastic & Chemical Industries Ltd',
      supplierBin: '001928374-0101',
      purchaseType: 'Local',
      invoiceDate: '2026-10-20',
      fiscalPeriod: '2026-10',
      companyId: 'COMP-001',
      branchId: 'BR-01',
      status: 'Submitted',
      subTotal: 1400000.00,
      totalVat: 210000.00,
      totalSupplementaryDuty: 0,
      totalAdvanceTax: 0,
      totalVds: 105000.00,
      grandTotal: 1610000.00,
      bankPaymentRef: 'EFTN-BRAC-20261020-0982',
      createdAt: '2026-10-20 11:30',
      createdBy: 'Rafiqul Islam (Maker)',
      remarks: 'Raw material procurement for 500ml water bottle preforms.',
      lines: [
        {
          id: 'PL-01',
          productId: 'PROD-01',
          productName: 'Virgin PET Resin Pellets (Food Grade)',
          productNameBn: 'ভার্জিন পিইটি রেজিন পেলেট (ফুড গ্রেড)',
          hsCode: '3907.61.00',
          uom: 'KG',
          quantity: 10000,
          unitPrice: 140.00,
          taxableAmount: 1400000.00,
          vatRatePercent: 15,
          vatAmount: 210000.00,
          supplementaryDutyAmount: 0,
          advanceTaxAmount: 0,
          vdsAmount: 105000.00,
          totalAmount: 1610000.00,
          isCreditEligible: true
        }
      ]
    },
    {
      id: 'PUR-2026-1023',
      invoiceNumber: 'PUR-1023',
      supplierInvoiceNumber: 'AGR-INV-4412',
      supplierId: 'PTY-SUP-02',
      supplierName: 'North Bengal Agro Seed Growers Ltd',
      supplierBin: '003847291-0102',
      purchaseType: 'Local',
      invoiceDate: '2026-10-18',
      fiscalPeriod: '2026-10',
      companyId: 'COMP-001',
      branchId: 'BR-01',
      status: 'Approved',
      subTotal: 950000.00,
      totalVat: 142500.00,
      totalSupplementaryDuty: 0,
      totalAdvanceTax: 0,
      totalVds: 0,
      grandTotal: 1092500.00,
      bankPaymentRef: 'BEFTN-DBBL-20261018-7711',
      createdAt: '2026-10-18 14:15',
      createdBy: 'Rafiqul Islam (Maker)',
      approvedBy: 'Tariqul Islam, FCA',
      approvedAt: '2026-10-19 09:30',
      remarks: 'Agro raw seeds batch 24/A. Section 46 rebate validated.',
      lines: [
        {
          id: 'PL-02',
          productId: 'PROD-02',
          productName: 'Raw Unrefined Mustard Seeds',
          productNameBn: 'অপরিশোধিত সরিষা বীজ',
          hsCode: '1207.50.90',
          uom: 'KG',
          quantity: 10000,
          unitPrice: 95.00,
          taxableAmount: 950000.00,
          vatRatePercent: 15,
          vatAmount: 142500.00,
          supplementaryDutyAmount: 0,
          advanceTaxAmount: 0,
          vdsAmount: 0,
          totalAmount: 1092500.00,
          isCreditEligible: true
        }
      ]
    },
    {
      id: 'PUR-2026-1022',
      invoiceNumber: 'PUR-1022',
      supplierInvoiceNumber: 'IMP-BOE-CTG-90812',
      supplierId: 'PTY-SUP-01',
      supplierName: 'Global Membrane Tech FZE (Import)',
      supplierBin: '000000000-0000',
      purchaseType: 'Import',
      billOfEntryNo: 'C-184920/2026',
      billOfEntryDate: '2026-10-10',
      customsStation: 'Chittagong Custom House',
      invoiceDate: '2026-10-12',
      fiscalPeriod: '2026-10',
      companyId: 'COMP-001',
      branchId: 'BR-02',
      status: 'Posted',
      subTotal: 850000.00,
      totalVat: 127500.00,
      totalSupplementaryDuty: 0,
      totalAdvanceTax: 42500.00,
      totalVds: 0,
      grandTotal: 1020000.00,
      createdAt: '2026-10-12 16:00',
      createdBy: 'Rafiqul Islam (Maker)',
      approvedBy: 'Tariqul Islam, FCA',
      approvedAt: '2026-10-13 10:00',
      remarks: 'Import of RO filter membrane. Advance Tax 5% claimed in Part 4.',
      lines: [
        {
          id: 'PL-03',
          productId: 'PROD-05',
          productName: 'Commercial RO Membrane Filter 4040',
          productNameBn: 'বাণিজ্যিক আরও মেমব্রেন ফিল্টার ৪০৪০',
          hsCode: '8421.21.00',
          uom: 'PCS',
          quantity: 100,
          unitPrice: 8500.00,
          taxableAmount: 850000.00,
          vatRatePercent: 15,
          vatAmount: 127500.00,
          supplementaryDutyAmount: 0,
          advanceTaxAmount: 42500.00,
          vdsAmount: 0,
          totalAmount: 1020000.00,
          isCreditEligible: true
        }
      ]
    }
  ];

  public salesInvoices: SalesInvoice[] = [
    {
      id: 'SAL-2026-0042',
      invoiceNumber: 'INV-2026-0042',
      challanNumber63: 'CH-DHK-2026-10-00142',
      customerId: 'PTY-CUST-01',
      customerName: 'ACI Logistics Ltd (Shwapno Super Shop)',
      customerNameBn: 'এসিআই লজিস্টিকস লি. (স্বপ্ন সুপার শপ)',
      customerBin: '000456123-0101',
      customerAddress: 'Tejgaon I/A, Dhaka-1208',
      destinationAddress: 'Shwapno Central Hub, Savar',
      vehicleTypeAndNo: 'Covered Van (Dhaka Metro-U-14-8921)',
      deliveryDate: '2026-10-21',
      deliveryTime: '10:30 AM',
      salesType: 'Standard',
      fiscalPeriod: '2026-10',
      companyId: 'COMP-001',
      branchId: 'BR-01',
      status: 'Approved',
      subTotal: 1800000.00,
      totalVat: 270000.00,
      totalSupplementaryDuty: 0,
      totalVdsDeducted: 135000.00,
      grandTotal: 2070000.00,
      createdAt: '2026-10-21 09:15',
      createdBy: 'Nusrat Jahan (Maker)',
      remarks: 'Standard 15% VAT Challan 6.3 issued before vehicle dispatch.',
      lines: [
        {
          id: 'SL-01',
          productId: 'PROD-03',
          productName: 'Padma Pure Bottled Water 500ml (24-Pack)',
          productNameBn: 'পদ্মা পিওর বোতলজাত পানি ৫০০ মি.লি. (২৪ প্যাক)',
          hsCode: '2201.10.00',
          uom: 'CTN',
          quantity: 5000,
          unitPrice: 360.00,
          taxableAmount: 1800000.00,
          vatRatePercent: 15,
          vatAmount: 270000.00,
          supplementaryDutyAmount: 0,
          vdsAmount: 135000.00,
          totalAmount: 2070000.00
        }
      ]
    },
    {
      id: 'SAL-2026-0041',
      invoiceNumber: 'INV-2026-0041',
      challanNumber63: 'CH-DHK-2026-10-00141',
      customerId: 'PTY-CUST-02',
      customerName: 'Pran Dairy & Consumer Distributors Ltd',
      customerNameBn: 'প্রাণ ডেইরি অ্যান্ড কনজিউমার ডিস্ট্রিবিউটরস লি.',
      customerBin: '000987321-0101',
      customerAddress: 'Middle Badda, Dhaka',
      destinationAddress: 'Ghorashal Factory Depot',
      vehicleTypeAndNo: 'Truck (Dhaka Metro-Ta-21-4432)',
      deliveryDate: '2026-10-19',
      deliveryTime: '02:00 PM',
      salesType: 'Standard',
      fiscalPeriod: '2026-10',
      companyId: 'COMP-001',
      branchId: 'BR-01',
      status: 'Posted',
      subTotal: 840000.00,
      totalVat: 126000.00,
      totalSupplementaryDuty: 0,
      totalVdsDeducted: 0,
      grandTotal: 966000.00,
      createdAt: '2026-10-19 13:00',
      createdBy: 'Nusrat Jahan (Maker)',
      remarks: 'Delivered and acknowledged by customer store.',
      lines: [
        {
          id: 'SL-02',
          productId: 'PROD-04',
          productName: 'Padma Gold Mustard Oil 1 Litre Bottle',
          productNameBn: 'পদ্মা গোল্ড খাঁটি সরিষার তেল ১ লিটার বোতল',
          hsCode: '1514.91.00',
          uom: 'BTL',
          quantity: 3000,
          unitPrice: 280.00,
          taxableAmount: 840000.00,
          vatRatePercent: 15,
          vatAmount: 126000.00,
          supplementaryDutyAmount: 0,
          vdsAmount: 0,
          totalAmount: 966000.00
        }
      ]
    }
  ];

  public approvalTasks: ApprovalTask[] = [
    {
      id: 'TSK-901',
      documentType: 'PurchaseInvoice',
      documentId: 'PUR-2026-1024',
      documentNumber: 'PUR-1024',
      partyName: 'Bengal Plastic & Chemical Industries Ltd',
      amount: 1610000.00,
      vatAmount: 210000.00,
      requestedBy: 'Rafiqul Islam (Maker)',
      requestedAt: '2026-10-20 11:30',
      status: 'Pending',
      priority: 'High',
      notes: 'Please verify EFTN payment voucher and supplier Mushak 6.3 copy before approving.'
    },
    {
      id: 'TSK-902',
      documentType: 'Coefficient43',
      documentId: 'DEC-43-2026-004',
      documentNumber: 'DEC-4.3/2026/04',
      partyName: 'Padma Gold Mustard Oil 1 Litre Bottle',
      amount: 280.00,
      vatAmount: 42.00,
      requestedBy: 'Engr. Mahbubur Rahman (Production)',
      requestedAt: '2026-10-21 16:45',
      status: 'Pending',
      priority: 'Urgent',
      notes: 'New crop seed rate revision requires updated 4.3 declaration filing with NBR Circle 12.'
    },
    {
      id: 'TSK-903',
      documentType: 'VatReturn',
      documentId: 'RET-2026-10',
      documentNumber: 'MUSHAK-9.1-DRAFT',
      partyName: 'NBR Monthly Filing (October 2026)',
      amount: 12050000.00,
      vatAmount: 750000.00,
      requestedBy: 'Tariqul Islam, FCA',
      requestedAt: '2026-10-22 17:00',
      status: 'Pending',
      priority: 'Urgent',
      notes: 'Provisional draft compiled. Net VAT payable: BDT 7,50,000 to be deposited via e-Challan.'
    }
  ];

  public readonly coefficient43List: Coefficient43[] = [
    {
      id: 'DEC-43-2026-004',
      declarationNumber: 'DEC-4.3/2026/04',
      finishedProductId: 'PROD-04',
      finishedProductName: 'Padma Gold Mustard Oil 1 Litre Bottle',
      hsCode: '1514.91.00',
      uom: 'BTL',
      batchSize: 1000,
      effectiveFrom: '2026-10-01',
      submissionDate: '2026-10-15',
      status: 'Submitted',
      totalRawMaterialCost: 155000.00,
      directLaborCost: 12000.00,
      utilityCost: 8000.00,
      overheadCost: 10000.00,
      marginPercent: 15,
      finalSellingPrice: 280.00,
      materials: [
        {
          id: 'CM-01',
          rawMaterialId: 'PROD-02',
          rawMaterialName: 'Raw Unrefined Mustard Seeds',
          hsCode: '1207.50.90',
          uom: 'KG',
          qtyRequired: 1500,
          wastagePercent: 4.5,
          wastageQty: 67.5,
          unitPrice: 95.00,
          totalCost: 142500.00
        },
        {
          id: 'CM-02',
          rawMaterialId: 'PROD-01',
          rawMaterialName: 'PET Bottle 1L & Cap with Label',
          hsCode: '3923.30.00',
          uom: 'PCS',
          qtyRequired: 1000,
          wastagePercent: 1.0,
          wastageQty: 10,
          unitPrice: 12.50,
          totalCost: 12500.00
        }
      ]
    }
  ];

  public readonly currentReturn91: Mushak91Return = {
    id: 'RET-2026-10',
    fiscalPeriod: '2026-10',
    companyId: 'COMP-001',
    companyName: 'Padma Consumer Goods & Manufacturing Ltd',
    bin: '001234567-0101',
    returnType: 'Main',
    status: 'Draft',
    submissionDate: '2026-11-12',
    // Part 3: Output VAT
    totalStandardSales: 12000000.00,
    totalStandardOutputVat: 1800000.00,
    totalZeroRatedSales: 0,
    totalExemptSales: 450000.00,
    totalOutputVat: 1800000.00,
    totalSupplementaryDuty: 0,
    // Part 4: Input VAT Credit
    totalStandardPurchases: 7000000.00,
    totalInputVatClaimable: 1050000.00,
    totalImportPurchases: 850000.00,
    totalImportVatClaimable: 127500.00,
    totalInputVatCredit: 1177500.00,
    // Part 5: Increasing Adjustments
    vdsDeductedFromSuppliers: 105000.00,
    debitNoteAdjustments: 0,
    totalIncreasingAdjustments: 105000.00,
    // Part 6: Decreasing Adjustments
    vdsDeductedByCustomers: 135000.00,
    creditNoteAdjustments: 0,
    totalDecreasingAdjustments: 135000.00,
    // Part 7: Net VAT Payable
    // Net Payable = Output VAT (1,800,000) - Input VAT Credit (1,177,500) + Increasing (105,000) - Decreasing (135,000)
    netTaxPayable: 592500.00,
    treasuryDepositAmount: 600000.00,
    treasuryChallanNo: 'TR-6-DHK-202611-99881',
    treasuryDepositDate: '2026-11-10',
    closingBalance: 7500.00
  };

  // Helper calculation method
  public calculateVat(taxableAmount: number, vatRatePercent: number = 15, isVdsApplicable: boolean = false): { vatAmount: number; vdsAmount: number; grandTotal: number } {
    const vatAmount = Math.round((taxableAmount * (vatRatePercent / 100)) * 100) / 100;
    const vdsAmount = isVdsApplicable ? Math.round((vatAmount * 0.5) * 100) / 100 : 0; // standard 50% or full
    const grandTotal = taxableAmount + vatAmount;
    return { vatAmount, vdsAmount, grandTotal };
  }
}
