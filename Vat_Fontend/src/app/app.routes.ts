import { TaxRulesComponent } from './features/vat-compliance/tax-rules.component';
import { CostingPoliciesComponent } from './features/inventory/costing-policies.component';
import { CostPreviewComponent } from './features/inventory/cost-preview.component';
import { StockValuationComponent } from './features/inventory/stock-valuation.component';
import { ProductionExecutionComponent } from './features/manufacturing/production-execution.component';
import { PaymentsComponent } from './features/payments/payments.component';
import { NetLedgerComponent } from './features/reports/net-ledger.component';
import { Routes } from '@angular/router';
import { AppShellComponent } from './layout/app-shell/app-shell.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { PurchaseListComponent } from './features/purchases/purchase-list/purchase-list.component';
import { PurchaseEntryComponent } from './features/purchases/purchase-entry/purchase-entry.component';
import { Mushak61Component } from './features/purchases/mushak-6-1/mushak-6-1.component';
import { SalesListComponent } from './features/sales/sales-list/sales-list.component';
import { SalesEntryComponent } from './features/sales/sales-entry/sales-entry.component';
import { Mushak63ChallanComponent } from './features/sales/mushak-6-3/mushak-6-3.component';
import { Mushak62Component } from './features/sales/mushak-6-2/mushak-6-2.component';
import { ManufacturingComponent } from './features/manufacturing/manufacturing.component';
import { InventoryComponent } from './features/inventory/inventory.component';
import { VatComplianceComponent } from './features/vat-compliance/vat-compliance.component';
import { WorkflowComponent } from './features/workflow/workflow.component';
import { MasterDataComponent } from './features/master-data/master-data.component';
import { ReportsComponent } from './features/reports/reports.component';
import { AdminComponent } from './features/admin/admin.component';
import { LoginComponent } from './features/auth/login/login.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { PortalComponent } from './features/portal/portal.component';
import { ModuleViewComponent } from './features/portal/module-view.component';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent,
    canActivate: [guestGuard]
  },
  { path: 'register', redirectTo: 'login', pathMatch: 'full' },
  { path: 'auth/callback', component: LoginComponent },
  {
    path: '',
    redirectTo: '/app/portal',
    pathMatch: 'full'
  },
  {
    path: 'app',
    component: AppShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'portal', pathMatch: 'full' },
      { path: 'portal', component: PortalComponent },
      { path: 'portal/:code', component: ModuleViewComponent },
      { path: 'dashboard', component: DashboardComponent },
      
      // Purchases
      { path: 'purchases', redirectTo: 'purchases/invoices', pathMatch: 'full' },
      { path: 'purchases/invoices', component: PurchaseListComponent },
      { path: 'purchases/new', component: PurchaseEntryComponent },
      { path: 'purchases/mushak-6-1', component: Mushak61Component },

      // Sales & Billing (Mushak 6.3)
      { path: 'sales', redirectTo: 'sales/invoices', pathMatch: 'full' },
      { path: 'sales/payments', component: PaymentsComponent },
      { path: 'sales/invoices', component: SalesListComponent },
      { path: 'sales/new', component: SalesEntryComponent },
      { path: 'sales/challan-preview', component: Mushak63ChallanComponent },
      { path: 'sales/mushak-6-2', component: Mushak62Component },

      // Manufacturing & Mushak 4.3
      { path: 'manufacturing', redirectTo: 'manufacturing/coefficient-4-3', pathMatch: 'full' },
      { path: 'manufacturing/coefficient-4-3', component: ManufacturingComponent },
      { path: 'manufacturing/bom', component: ManufacturingComponent },

      // Inventory & Stock
      { path: 'inventory', redirectTo: 'inventory/summary', pathMatch: 'full' },
      { path: 'inventory/summary', component: InventoryComponent },
      { path: 'inventory/costing-policies', component: CostingPoliciesComponent },
      { path: 'inventory/cost-preview', component: CostPreviewComponent },
      { path: 'inventory/valuation', component: StockValuationComponent },
      { path: 'manufacturing/execution', component: ProductionExecutionComponent },
      { path: 'inventory/transfers', component: InventoryComponent },

      // VAT Returns & Compliance (Mushak 9.1 & 6.6)
      { path: 'compliance', redirectTo: 'compliance/return-9-1', pathMatch: 'full' },
      { path: 'compliance/return-9-1', component: VatComplianceComponent },
      { path: 'compliance/tax-rules', component: TaxRulesComponent },
      { path: 'compliance/tax-ledger', component: VatComplianceComponent },
      { path: 'compliance/vds-certificates', component: VatComplianceComponent },
      { path: 'compliance/vds-66', redirectTo: 'compliance/vds-certificates', pathMatch: 'full' },
      { path: 'compliance/mushak-6-10', component: VatComplianceComponent },

      // Workflow & Maker-Checker
      { path: 'workflow/my-tasks', component: WorkflowComponent },

      // Master Data & RBAC
      { path: 'master-data', component: MasterDataComponent },
      { path: 'master-data/products', component: MasterDataComponent },
      { path: 'master-data/new-product', component: MasterDataComponent },
      { path: 'master-data/parties', component: MasterDataComponent },
      { path: 'master-data/new-party', component: MasterDataComponent },
      { path: 'reports/net-ledger', component: NetLedgerComponent },
      { path: 'reports', component: ReportsComponent },
      { path: 'admin', component: AdminComponent }
    ]
  },
  {
    path: '**',
    redirectTo: '/app/dashboard'
  }
];
