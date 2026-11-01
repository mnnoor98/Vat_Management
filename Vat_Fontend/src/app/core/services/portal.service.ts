import { Injectable, computed, inject, signal } from '@angular/core';
import { CompanyContextService } from './company-context.service';

export interface ModuleSubmenu {
  code: string;
  labelEn: string;
  labelBn: string;
  descriptionEn: string;
  descriptionBn: string;
  route: string;
  icon: string;
  badge?: string;
  requiredPermission?: string;
  allowedRoles?: string[];
}

export interface ModuleCard {
  id: string;
  code: string;
  titleEn: string;
  titleBn: string;
  descriptionEn: string;
  descriptionBn: string;
  icon: string;
  colorClass: string;
  route: string;
  allowedRoles: string[];
  requiredPermissions: string[];
  statutoryRef: string;
  submenus: ModuleSubmenu[];
}

export interface RoleMenuConfig {
  role: string;
  allowedModuleCodes: string[];
  allowedSubmenuCodes: string[];
}

const STORAGE_KEY_ROLE_MENU_MATRIX = 'vat_role_menu_matrix';

const DEFAULT_ROLE_MENU_MATRIX: Record<string, RoleMenuConfig> = {
  SuperAdmin: {
    role: 'SuperAdmin',
    allowedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY',
      'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS', 'MOD_ADMIN'
    ],
    allowedSubmenuCodes: [
      'SUB_PURCHASE_LIST', 'SUB_PURCHASE_NEW', 'SUB_MUSHAK_61',
      'SUB_SALES_LIST', 'SUB_SALES_NEW', 'SUB_MUSHAK_63', 'SUB_MUSHAK_62',
      'SUB_COEFF_43', 'SUB_BOM',
      'SUB_STOCK_SUMMARY', 'SUB_MUSHAK_65',
      'SUB_MUSHAK_91', 'SUB_TAX_LEDGER', 'SUB_MUSHAK_66', 'SUB_MUSHAK_610',
      'SUB_WORKFLOW_TASKS',
      'SUB_MASTER_PRODUCTS', 'SUB_MASTER_PARTIES',
      'SUB_REPORTS_AUDIT',
      'SUB_ADMIN_USERS', 'SUB_ADMIN_RBAC'
    ]
  },
  TaxManager: {
    role: 'TaxManager',
    allowedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY',
      'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS'
    ],
    allowedSubmenuCodes: [
      'SUB_PURCHASE_LIST', 'SUB_PURCHASE_NEW', 'SUB_MUSHAK_61',
      'SUB_SALES_LIST', 'SUB_SALES_NEW', 'SUB_MUSHAK_63', 'SUB_MUSHAK_62',
      'SUB_COEFF_43', 'SUB_BOM',
      'SUB_STOCK_SUMMARY', 'SUB_MUSHAK_65',
      'SUB_MUSHAK_91', 'SUB_TAX_LEDGER', 'SUB_MUSHAK_66', 'SUB_MUSHAK_610',
      'SUB_WORKFLOW_TASKS',
      'SUB_MASTER_PRODUCTS', 'SUB_MASTER_PARTIES',
      'SUB_REPORTS_AUDIT'
    ]
  },
  Maker: {
    role: 'Maker',
    allowedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY', 'MOD_MASTERDATA'
    ],
    allowedSubmenuCodes: [
      'SUB_PURCHASE_LIST', 'SUB_PURCHASE_NEW', 'SUB_MUSHAK_61',
      'SUB_SALES_LIST', 'SUB_SALES_NEW', 'SUB_MUSHAK_63', 'SUB_MUSHAK_62',
      'SUB_COEFF_43', 'SUB_BOM',
      'SUB_STOCK_SUMMARY', 'SUB_MUSHAK_65',
      'SUB_MASTER_PRODUCTS', 'SUB_MASTER_PARTIES'
    ]
  },
  Checker: {
    role: 'Checker',
    allowedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_INVENTORY', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS'
    ],
    allowedSubmenuCodes: [
      'SUB_PURCHASE_LIST', 'SUB_MUSHAK_61',
      'SUB_SALES_LIST', 'SUB_MUSHAK_63', 'SUB_MUSHAK_62',
      'SUB_STOCK_SUMMARY',
      'SUB_WORKFLOW_TASKS',
      'SUB_MASTER_PRODUCTS', 'SUB_MASTER_PARTIES',
      'SUB_REPORTS_AUDIT'
    ]
  },
  Approver: {
    role: 'Approver',
    allowedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_REPORTS'
    ],
    allowedSubmenuCodes: [
      'SUB_PURCHASE_LIST',
      'SUB_SALES_LIST', 'SUB_MUSHAK_63',
      'SUB_COEFF_43',
      'SUB_MUSHAK_91', 'SUB_TAX_LEDGER',
      'SUB_WORKFLOW_TASKS',
      'SUB_REPORTS_AUDIT'
    ]
  },
  Auditor: {
    role: 'Auditor',
    allowedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_INVENTORY', 'MOD_COMPLIANCE', 'MOD_REPORTS'
    ],
    allowedSubmenuCodes: [
      'SUB_PURCHASE_LIST', 'SUB_MUSHAK_61',
      'SUB_SALES_LIST', 'SUB_MUSHAK_62',
      'SUB_STOCK_SUMMARY',
      'SUB_TAX_LEDGER',
      'SUB_REPORTS_AUDIT'
    ]
  }
};

@Injectable({
  providedIn: 'root'
})
export class PortalService {
  private context = inject(CompanyContextService);

  public readonly modules: ModuleCard[] = [
    {
      id: 'mod-purchases',
      code: 'MOD_PURCHASES',
      titleEn: 'Purchases & Import Management',
      titleBn: 'ক্রয় ও আমদানি ব্যবস্থাপনা (মূসক ৬.১)',
      descriptionEn: 'Local purchase invoices, customs Bill of Entry (BOE), Mushak 6.1 purchase book, and Section 46 input tax credit claims.',
      descriptionBn: 'স্থানীয় ক্রয় চালান, কাস্টমস বিল অব এন্ট্রি (BOE), মূসক ৬.১ ক্রয় হিসাব পুস্তক এবং ধারা ৪৬ অনুযায়ী রেয়াত হিসাব।',
      icon: 'shopping_cart',
      colorClass: 'card-cyan',
      route: '/app/purchases',
      statutoryRef: 'NBR ধারা ৪৬ ও বিধি ৪০',
      allowedRoles: ['SuperAdmin', 'Maker', 'Checker', 'TaxManager', 'Auditor'],
      requiredPermissions: ['purchase.read', 'purchase.create', 'all'],
      submenus: [
        {
          code: 'SUB_PURCHASE_LIST',
          labelEn: 'Purchase Invoices',
          labelBn: 'ক্রয় চালান ও ইনভয়েস তালিকা',
          descriptionEn: 'View and track all registered local purchases and import bills of entry.',
          descriptionBn: 'সিস্টেমে অন্তর্ভুক্ত সকল স্থানীয় ক্রয় ও আমদানি চালানের স্থিতি পর্যবেক্ষণ।',
          route: '/app/purchases/invoices',
          icon: 'receipt',
          requiredPermission: 'purchase.read'
        },
        {
          code: 'SUB_PURCHASE_NEW',
          labelEn: '+ New Purchase Entry',
          labelBn: '+ নতুন ক্রয় চালান এন্ট্রি',
          descriptionEn: 'Register a new local supplier tax invoice or customs Bill of Entry with item lines.',
          descriptionBn: 'নতুন ক্রয় চালান বা আমদানি বিল অব এন্ট্রি অন্তর্ভুক্ত করুন।',
          route: '/app/purchases/new',
          icon: 'add_circle',
          requiredPermission: 'purchase.create',
          allowedRoles: ['Maker', 'TaxManager', 'SuperAdmin']
        },
        {
          code: 'SUB_MUSHAK_61',
          labelEn: 'Mushak 6.1 Purchase Register',
          labelBn: 'মূসক ৬.১ ক্রয় হিসাব পুস্তক',
          descriptionEn: 'Statutory purchase register book under VAT Rule 40 with opening/closing balances.',
          descriptionBn: 'মূল্য সংযোজন কর বিধিমালা বিধি ৪০ অনুযায়ী বিধিবদ্ধ ক্রয় হিসাব বহি।',
          route: '/app/purchases/mushak-6-1',
          icon: 'menu_book',
          requiredPermission: 'purchase.read'
        }
      ]
    },
    {
      id: 'mod-sales',
      code: 'MOD_SALES',
      titleEn: 'Sales & Billing (Mushak 6.3)',
      titleBn: 'বিক্রয় ও কর চালান (মূসক ৬.৩)',
      descriptionEn: 'Outward supplies, Section 51 Mushak 6.3 Tax Invoices, Mushak 6.2 sales register, and credit/debit notes.',
      descriptionBn: 'কর চালানপত্র (মূসক ৬.৩) ইস্যু, মূসক ৬.২ বিক্রয় হিসাব পুস্তক এবং ক্রেডিট/ডেবিট নোট ব্যবস্থাপনা।',
      icon: 'point_of_sale',
      colorClass: 'card-blue',
      route: '/app/sales',
      statutoryRef: 'NBR ধারা ৫১ ও বিধি ৪০',
      allowedRoles: ['SuperAdmin', 'Maker', 'Checker', 'TaxManager', 'Auditor'],
      requiredPermissions: ['sales.read', 'sales.create', 'all'],
      submenus: [
        {
          code: 'SUB_SALES_LIST',
          labelEn: 'Sales Invoices',
          labelBn: 'বিক্রয় চালান ও চালানপত্র তালিকা',
          descriptionEn: 'Monitor issued tax invoices, approval statuses, and delivery particulars.',
          descriptionBn: 'সকল বিক্রয় চালান, অনুমোদনের স্থিতি ও ডেলিভারি বিবরণী।',
          route: '/app/sales/invoices',
          icon: 'format_list_bulleted',
          requiredPermission: 'sales.read'
        },
        {
          code: 'SUB_SALES_NEW',
          labelEn: '+ Create Sales Challan (6.3)',
          labelBn: '+ নতুন বিক্রয় চালান (মূসক ৬.৩)',
          descriptionEn: 'Issue a new Mushak 6.3 Tax Challan to verified BIN customer with real-time tax calculation.',
          descriptionBn: 'নতুন মূসক ৬.৩ কর চালান প্রস্তুত করুন।',
          route: '/app/sales/new',
          icon: 'post_add',
          requiredPermission: 'sales.create',
          allowedRoles: ['Maker', 'TaxManager', 'SuperAdmin']
        },
        {
          code: 'SUB_MUSHAK_63',
          labelEn: 'Mushak 6.3 Print Preview',
          labelBn: 'মূসক ৬.৩ চালান প্রিন্ট ও ভিউ',
          descriptionEn: 'View authentic Bangladesh NBR prescribed format tax invoice ready for statutory print.',
          descriptionBn: 'এনবিআর নির্ধারিত ফরম্যাটে কর চালানপত্র প্রিন্ট ও প্রিভিউ।',
          route: '/app/sales/challan-preview',
          icon: 'print',
          requiredPermission: 'sales.read'
        },
        {
          code: 'SUB_MUSHAK_62',
          labelEn: 'Mushak 6.2 Sales Register',
          labelBn: 'মূসক ৬.২ বিক্রয় হিসাব পুস্তক',
          descriptionEn: 'Statutory sales book keeping ledger under Section 51 for outward supplies and tax liability.',
          descriptionBn: 'ধারা ৫১ ও বিধি ৪০ অনুযায়ী বিধিবদ্ধ বিক্রয় হিসাব বহি।',
          route: '/app/sales/mushak-6-2',
          icon: 'auto_stories',
          requiredPermission: 'sales.read'
        }
      ]
    },
    {
      id: 'mod-manufacturing',
      code: 'MOD_MANUFACTURING',
      titleEn: 'Manufacturing & Coefficient (4.3)',
      titleBn: 'উৎপাদন ও সহগ ঘোষণা (মূসক ৪.৩)',
      descriptionEn: 'Input-Output coefficient declaration (Mushak 4.3), Bill of Materials (BOM), and production wastage analysis.',
      descriptionBn: 'পণ্য উৎপাদনের উপকরণ-উৎপাদ সহগ ঘোষণা (মূসক ৪.৩) এবং বিওএম ও অপচয় ফর্মুলেশন।',
      icon: 'factory',
      colorClass: 'card-indigo',
      route: '/app/manufacturing',
      statutoryRef: 'NBR ধারা ২১ ও বিধি ১৮',
      allowedRoles: ['SuperAdmin', 'Maker', 'Checker', 'TaxManager', 'Approver'],
      requiredPermissions: ['manufacturing.coefficient.read', 'all'],
      submenus: [
        {
          code: 'SUB_COEFF_43',
          labelEn: 'Mushak 4.3 Coefficient',
          labelBn: 'মূসক ৪.৩ সহগ ঘোষণা',
          descriptionEn: 'Declare input-output coefficient for manufacturing products to divisional VAT officer.',
          descriptionBn: 'পণ্য উৎপাদনের জন্য কাঁচামাল ও রূপান্তর ব্যয়ের সহগ ঘোষণা।',
          route: '/app/manufacturing/coefficient-4-3',
          icon: 'calculate',
          requiredPermission: 'manufacturing.coefficient.read'
        },
        {
          code: 'SUB_BOM',
          labelEn: 'Bill of Materials (BOM)',
          labelBn: 'বিওএম তালিকা ও ফর্মুলা',
          descriptionEn: 'Structured recipe of raw materials, packaging, and wastage percentages.',
          descriptionBn: 'কাঁচামাল ও প্যাকেজিং উপকরণের ফর্মুলা ও অনুপাত।',
          route: '/app/manufacturing/bom',
          icon: 'account_tree',
          requiredPermission: 'manufacturing.coefficient.read'
        }
      ]
    },
    {
      id: 'mod-inventory',
      code: 'MOD_INVENTORY',
      titleEn: 'Inventory & Stock Transfers (6.5)',
      titleBn: 'মজুদ ও স্থানান্তর চালান (মূসক ৬.৫)',
      descriptionEn: 'Live warehouse stock balances, raw material tracking, finished goods, and Mushak 6.5 central registration transfer.',
      descriptionBn: 'লাইভ গুদাম মজুদ, কাঁচামাল খতিয়ান এবং কেন্দ্রীয় নিবন্ধিত শাখা স্থানান্তর চালান (৬.৫)।',
      icon: 'inventory_2',
      colorClass: 'card-amber',
      route: '/app/inventory',
      statutoryRef: 'NBR বিধি ৫০ (কেন্দ্রীয় নিবন্ধন)',
      allowedRoles: ['SuperAdmin', 'Maker', 'Checker', 'TaxManager', 'Auditor'],
      requiredPermissions: ['inventory.read', 'inventory.manage', 'all'],
      submenus: [
        {
          code: 'SUB_STOCK_SUMMARY',
          labelEn: 'Live Stock Balance',
          labelBn: 'বর্তমান স্টক হিসাব ও মূল্যায়ন',
          descriptionEn: 'Real-time stock ledger across branch warehouses with FIFO unit costs.',
          descriptionBn: 'শাখাওয়ারী গুদামের বর্তমান মজুদ ও একক মূল্যায়ন।',
          route: '/app/inventory/summary',
          icon: 'analytics',
          requiredPermission: 'inventory.read'
        },
        {
          code: 'SUB_MUSHAK_65',
          labelEn: 'Mushak 6.5 (Branch Transfer)',
          labelBn: 'মূসক ৬.৫ স্থানান্তর চালান',
          descriptionEn: 'Issue statutory goods transfer challan between central registration branches without VAT liability.',
          descriptionBn: 'এক শাখা হতে অন্য শাখায় করবিহীন পণ্য স্থানান্তর চালানপত্র।',
          route: '/app/inventory/transfers',
          icon: 'local_shipping',
          requiredPermission: 'inventory.manage'
        }
      ]
    },
    {
      id: 'mod-compliance',
      code: 'MOD_COMPLIANCE',
      titleEn: 'NBR Returns & Statutory Compliance',
      titleBn: 'এনবিআর ভ্যাট রিটার্ন ও কমপ্লায়েন্স (৯.১)',
      descriptionEn: 'Mushak 9.1 monthly tax return filing, Current Tax Ledger (চলতি হিসাব), VDS Certificates (৬.৬), and Section 64 compliance.',
      descriptionBn: 'মূসক ৯.১ মাসিক রিটার্ন, মূসক ৬.৬ উৎসে কর্তন সনদ এবং চলতি ভ্যাট হিসাব খতিয়ান।',
      icon: 'verified_user',
      colorClass: 'card-emerald',
      route: '/app/compliance',
      statutoryRef: 'NBR ধারা ৬৪ ও বিধি ৪৭',
      allowedRoles: ['SuperAdmin', 'TaxManager', 'Approver', 'Auditor'],
      requiredPermissions: ['compliance.submit', 'tax.read', 'all'],
      submenus: [
        {
          code: 'SUB_MUSHAK_91',
          labelEn: 'Mushak 9.1 Monthly Return',
          labelBn: 'মূসক ৯.১ মাসিক ভ্যাট রিটার্ন',
          descriptionEn: 'Compute and generate statutory Mushak 9.1 return with sub-forms for submission to NBR.',
          descriptionBn: 'কর মেয়াদের জন্য এনবিআরে দাখিলযোগ্য স্বয়ংক্রিয় মাসিক মূসক রিটার্ন।',
          route: '/app/compliance/return-9-1',
          icon: 'assignment',
          requiredPermission: 'compliance.submit'
        },
        {
          code: 'SUB_TAX_RULES', labelEn: 'Tax Rules', labelBn: 'Tax Rules',
          descriptionEn: 'Review draft rates and publish tax rules.', descriptionBn: 'Review draft rates and publish tax rules.',
          route: '/app/compliance/tax-rules', icon: 'rule', requiredPermission: 'tax.read',
          allowedRoles: ['SuperAdmin', 'TaxManager']
        },
        {
          code: 'SUB_TAX_LEDGER',
          labelEn: 'Current Tax Ledger (চলতি হিসাব)',
          labelBn: 'চলতি ভ্যাট হিসাব লেজার (বকেয়া ও জমা)',
          descriptionEn: 'Statutory account balance of Input Tax Credit, Output VAT, Treasury deposits, and net payable.',
          descriptionBn: 'উপকরণ কর রেয়াত, প্রদেয় কর ও ট্রেজারি জমার স্বয়ংক্রিয় হিসাব।',
          route: '/app/compliance/tax-ledger',
          icon: 'account_balance',
          requiredPermission: 'tax.read'
        },
        {
          code: 'SUB_MUSHAK_66',
          labelEn: 'Mushak 6.6 (VDS Certificate)',
          labelBn: 'মূসক ৬.৬ উৎস কর্তন সনদ',
          descriptionEn: 'Issue and track VAT Deducted at Source (VDS) certificates under Section 49.',
          descriptionBn: 'উৎসে মূসক কর্তন সনদপত্র প্রস্তুত ও বিতরণ রেজিস্টার।',
          route: '/app/compliance/vds-certificates',
          icon: 'badge',
          requiredPermission: 'tax.read'
        },
        {
          code: 'SUB_MUSHAK_610',
          labelEn: 'Mushak 6.10 (>2 Lakh BDT)',
          labelBn: 'মূসক ৬.১০ (২ লক্ষাধিক টাকার চালান)',
          descriptionEn: 'Statutory declaration of single transaction exceeding 200,000 BDT under VAT Rule 42.',
          descriptionBn: 'দুই লক্ষ টাকার অধিক মূল্যের ক্রয়-বিক্রয় চালানের তথ্য বিবরণী।',
          route: '/app/compliance/mushak-6-10',
          icon: 'warning',
          requiredPermission: 'tax.read'
        }
      ]
    },
    {
      id: 'mod-workflow',
      code: 'MOD_WORKFLOW',
      titleEn: 'Approval Workbench (Maker-Checker)',
      titleBn: 'অনুমোদন ওয়ার্কবেঞ্চ (Maker-Checker)',
      descriptionEn: 'Pending purchase & sales authorizations, multi-tier Maker-Checker-Approver delegation matrix, and immutable audit logs.',
      descriptionBn: 'চালান অনুমোদন, যাচাই ও সিদ্ধান্ত ট্র্যাকিং অডিট ট্রেইল (Live PostgreSQL)।',
      icon: 'task_alt',
      colorClass: 'card-violet',
      route: '/app/workflow/my-tasks',
      statutoryRef: 'NBR অডিট ট্রেইল ও সুশাসন',
      allowedRoles: ['SuperAdmin', 'Checker', 'Approver', 'TaxManager'],
      requiredPermissions: ['workflow.read', 'workflow.task.approve', 'all'],
      submenus: [
        {
          code: 'SUB_WORKFLOW_TASKS',
          labelEn: 'Pending Tasks Workbench',
          labelBn: 'অপেক্ষমাণ অনুমোদন টাস্ক',
          descriptionEn: 'Review, verify calculations, and authorize purchase & sales invoices.',
          descriptionBn: 'মেকার কর্তৃক প্রেরিত চালানসমূহ যাচাই ও অনুমোদন করুন।',
          route: '/app/workflow/my-tasks',
          icon: 'task_alt',
          requiredPermission: 'workflow.task.approve'
        }
      ]
    },
    {
      id: 'mod-masterdata',
      code: 'MOD_MASTERDATA',
      titleEn: 'Master Data & HS Code Registry',
      titleBn: 'মাস্টার ডাটা ও এনবিআর এইচএস কোড',
      descriptionEn: 'Product catalog, 8-digit NBR HS Code classifications, statutory VAT/SD rates, verified 13-digit BIN suppliers, and customers.',
      descriptionBn: 'পণ্য তালিকা, ৮-সংখ্যার এইচএস কোড, ভ্যাট হার এবং অনুমোদিত সরবরাহকারী ও গ্রাহক ক্যাটালগ।',
      icon: 'dataset',
      colorClass: 'card-cyan',
      route: '/app/master-data',
      statutoryRef: 'NBR প্রথম ও তৃতীয় তফসিল',
      allowedRoles: ['SuperAdmin', 'Maker', 'Checker', 'TaxManager'],
      requiredPermissions: ['masterdata.read', 'masterdata.manage', 'all'],
      submenus: [
        {
          code: 'SUB_MASTER_PRODUCTS',
          labelEn: 'Products & HS Codes',
          labelBn: 'পণ্য ও কাঁচামাল ক্যাটালগ',
          descriptionEn: 'Browse registered products, raw materials, packaging with statutory VAT and SD rates.',
          descriptionBn: '৮-ডিজিটের এইচএস কোডসহ কাঁচামাল ও উৎপাদিত পণ্যের তালিকা।',
          route: '/app/master-data',
          icon: 'category',
          requiredPermission: 'masterdata.read'
        },
        {
          code: 'SUB_MASTER_PARTIES',
          labelEn: 'Customers & Suppliers',
          labelBn: 'সরবরাহকারী ও গ্রাহক তালিকা',
          descriptionEn: 'NBR 13-digit verified BIN registered business entities and vendors.',
          descriptionBn: '১৩ সংখ্যার বৈধ বিআইএনধারী ভেন্ডর ও গ্রাহক প্রোফাইল।',
          route: '/app/master-data/parties',
          icon: 'people',
          requiredPermission: 'masterdata.read'
        }
      ]
    },
    {
      id: 'mod-reports',
      code: 'MOD_REPORTS',
      titleEn: 'Statutory Reports Hub (বিধি ১১২)',
      titleBn: 'সংবিধিবদ্ধ প্রতিবেদন ও নিরীক্ষা লেজার',
      descriptionEn: 'Statutory NBR audit trail under VAT Rule 112, Treasury Challan reconciliations, Section 46 credit tracking, and export inspection sheets.',
      descriptionBn: 'মূল্য সংযোজন কর বিধিমালা বিধি ১১২ অনুযায়ী অডিট ট্রেইল ও ট্রেজারি চালান হিসাব।',
      icon: 'bar_chart',
      colorClass: 'card-rose',
      route: '/app/reports',
      statutoryRef: 'NBR বিধি ১১২ (নথিপত্র সংরক্ষণ)',
      allowedRoles: ['SuperAdmin', 'TaxManager', 'Auditor', 'Checker'],
      requiredPermissions: ['report.read', 'audit.read', 'all'],
      submenus: [
        {
          code: 'SUB_REPORTS_AUDIT',
          labelEn: 'Statutory Registers & Audit',
          labelBn: 'সংবিধিবদ্ধ রেজিস্টার ও নিরীক্ষা রিপোর্ট',
          descriptionEn: 'Generate consolidated tax ledger, Treasury Challan deposit reports, and credit summary.',
          descriptionBn: 'চলতি হিসাব, ট্রেজারি জমা এবং কর কর্মকর্তাদের জন্য সংবিধিবদ্ধ বিবরণী।',
          route: '/app/reports',
          icon: 'bar_chart',
          requiredPermission: 'report.read'
        }
      ]
    },
    {
      id: 'mod-admin',
      code: 'MOD_ADMIN',
      titleEn: 'Security, RBAC & User Administration',
      titleBn: 'প্রশাসন, সিকিউরিটি ও ব্যবহারকারী ব্যবস্থাপনা',
      descriptionEn: 'User accounts, password policies, branch assignment enforcement, dynamic role-based access matrix, and system security monitoring.',
      descriptionBn: 'ব্যবহারকারী ও লগইন তালিকা, রোল পারমিশন ম্যাট্রিক্স এবং এনবিআর সিকিউরিটি অডিট ট্রেইল।',
      icon: 'admin_panel_settings',
      colorClass: 'card-slate',
      route: '/app/admin',
      statutoryRef: 'NBR সিকিউরিটি ও আরবেসি গভর্ন্যান্স',
      allowedRoles: ['SuperAdmin'],
      requiredPermissions: ['all'],
      submenus: [
        {
          code: 'SUB_ADMIN_USERS',
          labelEn: 'Users & Logins Management',
          labelBn: 'ব্যবহারকারী ও লগইন ব্যবস্থাপনা',
          descriptionEn: 'Manage officer accounts, reset passwords, toggle active status, and assign branches.',
          descriptionBn: 'ব্যবহারকারী সংযোজন, নিষ্ক্রিয়করণ ও শাখা নির্ধারণ।',
          route: '/app/admin',
          icon: 'people',
          requiredPermission: 'all'
        },
        {
          code: 'SUB_ADMIN_RBAC',
          labelEn: 'RBAC Roles & Permissions Matrix',
          labelBn: 'রোল ও পারমিশন ম্যাট্রিক্স',
          descriptionEn: 'Configure statutory function access by enterprise role (Maker, Checker, Tax Manager).',
          descriptionBn: 'ভূমিকাভিত্তিক কার্যক্রমের অনুমতি ও কার্যক্ষেত্র নির্ধারণ।',
          route: '/app/admin',
          icon: 'security',
          requiredPermission: 'all'
        }
      ]
    }
  ];

  public readonly matrixVersion = signal<number>(0);

  /**
   * Load entire role-menu matrix from localStorage or default
   */
  public getAllRoleConfigs(): Record<string, RoleMenuConfig> {
    try {
      const raw = localStorage.getItem(STORAGE_KEY_ROLE_MENU_MATRIX);
      if (raw) {
        const parsed = JSON.parse(raw);
        return { ...DEFAULT_ROLE_MENU_MATRIX, ...parsed };
      }
    } catch {
      // ignore
    }
    return { ...DEFAULT_ROLE_MENU_MATRIX };
  }

  /**
   * Get configuration for a specific role
   */
  public getRoleMenuConfig(role: string): RoleMenuConfig {
    const all = this.getAllRoleConfigs();
    if (all[role]) {
      return all[role];
    }
    // Fallback default
    return DEFAULT_ROLE_MENU_MATRIX[role] || {
      role,
      allowedModuleCodes: ['MOD_PURCHASES', 'MOD_SALES'],
      allowedSubmenuCodes: ['SUB_PURCHASE_LIST', 'SUB_SALES_LIST']
    };
  }

  /**
   * Save / update a role's permitted modules & submenus
   */
  public saveRoleMenuConfig(role: string, allowedModuleCodes: string[], allowedSubmenuCodes: string[]): void {
    const all = this.getAllRoleConfigs();
    all[role] = {
      role,
      allowedModuleCodes,
      allowedSubmenuCodes
    };
    localStorage.setItem(STORAGE_KEY_ROLE_MENU_MATRIX, JSON.stringify(all));
    this.matrixVersion.update(v => v + 1);
  }

  /**
   * Reset matrix to factory statutory defaults
   */
  public resetRoleMenuMatrix(): void {
    localStorage.removeItem(STORAGE_KEY_ROLE_MENU_MATRIX);
    this.matrixVersion.update(v => v + 1);
  }

  public getDefaultMatrix(): Record<string, RoleMenuConfig> {
    return { ...DEFAULT_ROLE_MENU_MATRIX };
  }

  /**
   * Returns list of module cards authorized for current user,
   * factoring in:
   * 1. User-specific assigned module codes (user wise modul dewa ache kina)
   * 2. Role-wise module permissions across ALL roles assigned to the user
   */
  public getAuthorizedModules(): ModuleCard[] {
    // Reading matrixVersion ensures reactivity when updated
    this.matrixVersion();

    const user = this.context.currentUser();
    const userRoles: string[] = (user.roles && user.roles.length > 0) 
      ? user.roles 
      : [user.role || 'Maker'];
    const isSuper = userRoles.includes('SuperAdmin');
    const assignedCodes = user.assignedModuleCodes;

    // Collect permitted module codes across ALL assigned roles
    const permittedModuleCodes = new Set<string>();
    for (const r of userRoles) {
      const cfg = this.getRoleMenuConfig(r);
      if (cfg && cfg.allowedModuleCodes) {
        cfg.allowedModuleCodes.forEach(code => permittedModuleCodes.add(code));
      }
    }

    return this.modules.filter(mod => {
      // 1. User-wise specific module assignment check:
      // If user has specific assignedModuleCodes defined and non-empty,
      // they can ONLY see those assigned modules!
      if (assignedCodes) {
        if (!assignedCodes.includes(mod.code)) {
          return false;
        }
      }

      // 2. SuperAdmin has access to all modules unless restricted by assignedCodes above
      if (isSuper) {
        return true;
      }

      // 3. Check role-wise matrix configuration across all user roles
      if (permittedModuleCodes.has(mod.code)) {
        return true;
      }

      // Fallback: check static allowedRoles
      if (mod.allowedRoles && mod.allowedRoles.some(r => userRoles.includes(r))) {
        return true;
      }
      return false;
    });
  }

  /**
   * Returns authorized submenus for a specific module,
   * filtered by the active role-menu matrix configuration across all user roles.
   */
  public getAuthorizedSubmenus(moduleCode: string): ModuleSubmenu[] {
    // Reading matrixVersion ensures reactivity
    this.matrixVersion();

    const mod = this.modules.find(m => m.code === moduleCode);
    if (!mod) return [];

    const user = this.context.currentUser();
    const userRoles: string[] = (user.roles && user.roles.length > 0) 
      ? user.roles 
      : [user.role || 'Maker'];
    const isSuper = userRoles.includes('SuperAdmin');

    // If user has assigned modules and this module is NOT in their assigned list, return empty
    if (user.assignedModuleCodes) {
      if (!user.assignedModuleCodes.includes(moduleCode)) {
        return [];
      }
    }

    if (isSuper) {
      return mod.submenus;
    }

    // Collect permitted submenu codes across ALL assigned roles
    const permittedSubmenuCodes = new Set<string>();
    for (const r of userRoles) {
      const cfg = this.getRoleMenuConfig(r);
      if (cfg && cfg.allowedSubmenuCodes) {
        cfg.allowedSubmenuCodes.forEach(s => permittedSubmenuCodes.add(s));
      }
    }

    return mod.submenus.filter(sub => {
      if (permittedSubmenuCodes.has(sub.code)) {
        return true;
      }
      if (sub.allowedRoles && sub.allowedRoles.some(r => userRoles.includes(r))) {
        return true;
      }
      return false;
    });
  }

  public getModuleByCode(code: string): ModuleCard | undefined {
    return this.modules.find(m => m.code === code);
  }

  public getModuleForUrl(url: string): ModuleCard | undefined {
    if (url.includes('/app/purchases')) return this.modules.find(m => m.code === 'MOD_PURCHASES');
    if (url.includes('/app/sales')) return this.modules.find(m => m.code === 'MOD_SALES');
    if (url.includes('/app/manufacturing')) return this.modules.find(m => m.code === 'MOD_MANUFACTURING');
    if (url.includes('/app/inventory')) return this.modules.find(m => m.code === 'MOD_INVENTORY');
    if (url.includes('/app/compliance')) return this.modules.find(m => m.code === 'MOD_COMPLIANCE');
    if (url.includes('/app/workflow')) return this.modules.find(m => m.code === 'MOD_WORKFLOW');
    if (url.includes('/app/master-data')) return this.modules.find(m => m.code === 'MOD_MASTERDATA');
    if (url.includes('/app/reports')) return this.modules.find(m => m.code === 'MOD_REPORTS');
    if (url.includes('/app/admin')) return this.modules.find(m => m.code === 'MOD_ADMIN');
    return undefined;
  }
}
