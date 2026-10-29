# Enterprise VAT Management System (ভ্যাট ব্যবস্থাপনা সিস্টেম)
## Project Architecture & Operational Blueprint

> **System Standard**: National Board of Revenue (NBR), Bangladesh — Value Added Tax and Supplementary Duty Act, 2012 (মূসক ও সম্পূরক শুল্ক আইন, ২০১২) and Rules 2016.  
> **Backend Architecture**: Microservices Architecture (**Database-per-Service** pattern) with ASP.NET Core 10 & Entity Framework Core 10.  
> **Frontend Architecture**: Angular 22 (Standalone Components, Signals, Reactive Forms, SCSS).  
> **Database Engine**: PostgreSQL 18 (Local Instance: `127.0.0.1:5432`).  
> **Messaging / Event Bus**: RabbitMQ 4 (AMQP 5672 / Management UI 15672) with Outbox & Idempotent Consumer Patterns.

---

## 1. Network Topology & Port Mapping (নেটওয়ার্ক ও পোর্ট তালিকা)

### 1.1 Microservices & Dedicated Databases (১২টি স্বাধীন মাইক্রোসার্ভিস)

| Service Name | Port | Direct URL | Dedicated Database | Database Schema | Primary API Route | NBR Statutory Domain |
| :--- | :---: | :--- | :--- | :--- | :--- | :--- |
| **Identity** | `5101` | `http://localhost:5101` | `vat_identity` | `identity` | `/api/v1/auth`, `/api/v1/users` | Authentication, JWT, RBAC, Menus |
| **Company** | `5102` | `http://localhost:5102` | `vat_company` | `company` | `/api/v1/companies`, `/api/v1/branches` | Company Profile, 13-digit BIN, Fiscal Tax Periods |
| **MasterData** | `5103` | `http://localhost:5103` | `vat_masterdata` | `masterdata` | `/api/v1/products`, `/api/v1/suppliers` | Products, 8-digit HS Codes, Vendors, Clients |
| **Purchase** | `5104` | `http://localhost:5104` | `vat_purchase` | `purchasing` | `/api/v1/purchases` | **Mushak 6.1** (ক্রয় হিসাব পুস্তক / Purchase Register) |
| **Sales** | `5105` | `http://localhost:5105` | `vat_sales` | `sales` | `/api/v1/sales`, `/api/v1/invoices` | **Mushak 6.2, 6.3, 6.7, 6.8** (বিক্রয়, চালান, ক্রেডিট/ডেবিট নোট) |
| **Inventory** | `5106` | `http://localhost:5106` | `vat_inventory` | `inventory` | `/api/v1/inventory`, `/api/v1/transfers` | **Mushak 6.5** (পণ্য স্থানান্তর চালান / Transfer Challan) |
| **Manufacturing** | `5107` | `http://localhost:5107` | `vat_manufacturing` | `manufacturing` | `/api/v1/manufacturing` | **Mushak 4.3** (উপকরণ-উৎপাদ সহগ / Coefficient Declaration) |
| **VatEngine** | `5108` | `http://localhost:5108` | `vat_vatengine` | `tax` | `/api/v1/tax`, `/api/v1/vds` | **Mushak 6.6** (উৎসে কর কর্তন সনদপত্র / VDS Deductions) |
| **Compliance** | `5109` | `http://localhost:5109` | `vat_compliance` | `compliance` | `/api/v1/compliance`, `/api/v1/returns` | **Mushak 9.1 & 6.10** (ভ্যাট দাখিলপত্র ও উচ্চমূল্য লেনদেন) |
| **Workflow** | `5110` | `http://localhost:5110` | `vat_workflow` | `workflow` | `/api/v1/workflows` | Approval Chains, State Machine Transitions |
| **Reporting** | `5111` | `http://localhost:5111` | `vat_reporting` | `reporting` | `/api/v1/reports`, `/api/v1/snapshots` | Audit-Proof Snapshots, Analytics & Ledger Statements |
| **Integration** | `5112` | `http://localhost:5112` | `vat_integration` | `integration` | `/api/v1/integration`, `/api/v1/webhooks` | External ERP/Customs Inbound Sync & Dead-Letter |
| *FinancialOutbox* | `5113` | `http://localhost:5113` | `vat_management` | `public` | `/health` | Outbox Message Relay & Event Publishing |

### 1.2 Gateway & Infrastructure Ports

| Component | Port | Host / URL | Purpose |
| :--- | :---: | :--- | :--- |
| **Unified API Gateway** | **`5080`** | `http://localhost:5080` | Frontend entry point proxying requests to all 12 microservices (`/api/v1/...`) |
| **Frontend UI (Angular)** | **`4200`** | `http://localhost:4200` | Web application UI (`proxy.conf.json` maps `/api` and `/health` to `5080`) |
| **PostgreSQL Database** | **`5432`** | `127.0.0.1:5432` | Database engine (`postgres:123456`) hosting all 12 isolated databases |
| **RabbitMQ AMQP** | **`5672`** | `localhost:5672` | Message Broker connection port (`guest:guest`) |
| **RabbitMQ Management UI**| **`15672`**| `http://localhost:15672` | Browser dashboard for exchanges, queues, and message traces |

---

## 2. NBR Statutory Bangladesh VAT Forms Mapping (মূসক ফরম ও বিধান)

| NBR Form | Bengali Name | English Description | Governing Rule/Act | Microservice Owner |
| :--- | :--- | :--- | :--- | :--- |
| **Mushak 4.3** | উপকরণ-উৎপাদ সহগ ঘোষণা | Input-Output Coefficient / Price Declaration | Rule 21(1) | `Vat.Manufacturing` (`vat_manufacturing`) |
| **Mushak 6.1** | ক্রয় হিসাব পুস্তক | Purchase Account Book / Register | Rule 40(1) | `Vat.Purchase` (`vat_purchase`) |
| **Mushak 6.2** | বিক্রয় হিসাব পুস্তক | Sales Account Book / Register | Rule 40(1) | `Vat.Sales` (`vat_sales`) |
| **Mushak 6.2.1** | ক্রয়-বিক্রয় হিসাব পুস্তক | Combined Purchase & Sales Register | Rule 40(1) | `Vat.Sales` (`vat_sales`) |
| **Mushak 6.3** | কর চালানপত্র | Tax Invoice (with Vehicle Tracking & Driver License) | Rule 40(1) | `Vat.Sales` (`vat_sales`) |
| **Mushak 6.5** | পণ্য স্থানান্তর চালানপত্র | Central to Branch Goods Transfer Challan | Rule 40(1) | `Vat.Inventory` (`vat_inventory`) |
| **Mushak 6.6** | উৎসে কর কর্তন সনদপত্র | Tax Deduction at Source (VDS Certificate) | Rule 40(2) | `Vat.VatEngine` (`vat_vatengine`) |
| **Mushak 6.7** | ক্রেডিট নোট | Credit Note (Sales Return & Adjustment) | Rule 40(1) | `Vat.Sales` (`vat_sales`) |
| **Mushak 6.8** | ডেবিট নোট | Debit Note (Purchase Return & Adjustment) | Rule 40(1) | `Vat.Sales` (`vat_sales`) |
| **Mushak 6.10** | ২ লক্ষ টাকার অধিক বিক্রয় | High Value Sales/Purchase Register (> BDT 2,00,000) | Section 51 | `Vat.Compliance` (`vat_compliance`) |
| **Mushak 9.1** | মূল্য সংযোজন কর দাখিলপত্র | Monthly VAT Return (Subsections, Rebates, Net Tax) | Section 64 | `Vat.Compliance` (`vat_compliance`) |

---

## 3. Statutory Business Rules & Validations (এনবিআর সংবিধিবদ্ধ নিয়ম)

1. **১৩ ডিজিট বিআইএন (13-Digit BIN Validation)**:
   - ফরম্যাট: `XXXXXXXXX-XXXX` (৯ ডিজিট রেজিস্ট্রেশন + ৪ ডিজিট শাখা কোড)।
   - হাইফেন ছাড়া ১৩ ডিজিট এবং হাইফেনসহ উভয় ফরম্যাট গ্রহণযোগ্য।
2. **৮ ডিজিট এইচএস কোড (8-Digit HS Code Validation)**:
   - ফরম্যাট: `XXXX.XX.XX` (ওয়ার্ল্ড কাস্টমস অর্গানাইজেশন ডব্লিউসিও সাব-হেডিং কমপ্লায়েন্ট)।
3. **ধারা ৪৬(১)(ঙ) ব্যাংকিং চ্যানেল বাধ্যবাধকতা (Section 46(1)(e) Banking Channel Rule)**:
   - চালান মূল্য **১,০০,০০০ টাকা বা তার বেশি** হলে অবশ্যই অনুমোদিত ব্যাংকিং চ্যানেলে (চেক / পে-অর্ডার / ব্যাংক ট্রান্সফার) পরিশোধ করতে হবে। অন্যথায় রেয়াত (Rebate / Input Tax Credit) বাতিল হবে।
4. **ধারা ৫১ উচ্চমূল্য লেনদেন রেজিস্টার (Section 51 Mushak 6.10 Threshold)**:
   - একক চালান বা সরবরাহের মূল্য **২,০০,০০০ টাকার বেশি** হলে তা মূসক ৬.১০ রেজিস্টারে স্বয়ংক্রিয়ভাবে রিপোর্ট হতে হবে।
5. **বাংলাদেশী মুদ্রা ফরম্যাট (Lakh & Crore Formatting)**:
   - পশ্চিমা মিলিয়ন-বিলিয়ন ফরম্যাটের পরিবর্তে দক্ষিণ এশীয় ৩-২-২ কমা গ্রুপিং (যেমন: `৳ ২৫,০০,০০০.০০` বা `৳ ১,০০,০০,০০০.০০`)।
6. **দ্বিভাষিক সংখ্যা রূপান্তর (Dual English/Bengali Numerals)**:
   - চালানে একই সাথে ইংরেজি ও বাংলা সংখ্যার যথাযথ প্রিন্টিং রূপান্তর।

---

## 4. Database-per-Service Architecture & Tables (ডাটাবেজ ও টেবিল বিবরণ)

প্রতিটি ডাটাবেজ স্বতন্ত্র এবং মোট **১১০টি টেবিল** দ্বারা গঠিত:

```
PostgreSQL Server (127.0.0.1:5432)
├── vat_identity       -> identity schema (10 tables: users, roles, permissions, menus, user_roles, role_permissions, ...)
├── vat_company        -> company schema  (6 tables: companies, branches, fiscal_periods, audits, commands, outbox)
├── vat_masterdata     -> masterdata schema (5 tables: products, suppliers, customers, audits, commands)
├── vat_purchase       -> purchasing schema (6 tables: purchases, purchase_lines, posting_plans, reversals, audits, commands)
├── vat_sales          -> sales schema    (15 tables: invoices, invoice_lines, credit_notes, debit_notes, payments, allocations, ...)
├── vat_inventory      -> inventory schema (10 tables: warehouses, stock_balances, stock_movements, transfers, transfer_lines, ...)
├── vat_manufacturing  -> manufacturing schema (11 tables: bom_headers, coefficient_versions, bom_lines, production_orders, ...)
├── vat_vatengine      -> tax schema      (8 tables: tax_rule_versions, tax_ledger, tax_treatments, calculation_snapshots, vds_entries, ...)
├── vat_compliance     -> compliance schema (9 tables: returns, return_lines, adjustments, schedules, certificates, audits, ...)
├── vat_workflow       -> workflow schema (8 tables: workflow_definitions, instances, transitions, step_logs, approvals, ...)
├── vat_reporting      -> reporting schema (13 tables: reports, report_definitions, snapshot_generations, audit_trails, ...)
└── vat_integration    -> integration schema (9 tables: connections, field_mappings, inbound_batches, external_references, ...)
```

---

## 5. Project Directory Structure (প্রজেক্ট ফোল্ডার কাঠামো)

```
Vat_Management/
├── project-info.md                                # This Master Documentation File
│
├── VAt_Asper_backend/                             # ASP.NET Core 10 Solution (81 Projects)
│   ├── EnterpriseVat.sln                          # Master Solution (Zero Errors, Zero Warnings)
│   ├── services.json                              # Microservice Registry & Port Specifications
│   ├── src/
│   │   ├── Services/                              # 12 Independent Microservices
│   │   │   ├── Identity/                          # Domain, Application, Infrastructure, Api
│   │   │   ├── Company/                           # Domain, Application, Infrastructure, Api
│   │   │   ├── MasterData/                        # Domain, Application, Infrastructure, Api
│   │   │   ├── Purchase/                          # Domain, Application, Infrastructure, Api
│   │   │   ├── Sales/                             # Domain, Application, Infrastructure, Api
│   │   │   ├── Inventory/                         # Domain, Application, Infrastructure, Api
│   │   │   ├── Manufacturing/                     # Domain, Application, Infrastructure, Api
│   │   │   ├── VatEngine/                         # Domain, Application, Infrastructure, Api
│   │   │   ├── Compliance/                        # Domain, Application, Infrastructure, Api
│   │   │   ├── Workflow/                          # Domain, Application, Infrastructure, Api
│   │   │   ├── Reporting/                         # Domain, Application, Infrastructure, Api
│   │   │   └── Integration/                       # Domain, Application, Infrastructure, Api
│   │   ├── Vat.Api/                               # Gateway / Unified API Host (Port 5080)
│   │   ├── Vat.Messaging/                         # Domain Event Bus & Outbox Contracts
│   │   └── Vat.SharedKernel/                      # Domain Primitives, Result Pattern, Value Objects
│   ├── tools/                                     # 12 Dedicated Database Migrators
│   │   ├── Vat.Company.Migrator/
│   │   ├── Vat.Inventory.Migrator/
│   │   ├── Vat.VatEngine.Migrator/
│   │   └── ... (All 12 Services)
│   ├── scripts/
│   │   ├── Migrate-AllDatabases.ps1               # 1-Click Master Migration for all 12 databases
│   │   ├── Start-Services.ps1                     # Multi-process service runner
│   │   └── Test-Local.ps1                         # Local test runner
│   └── tests/                                     # Backend Test Suites (100% Passed)
│
└── Vat_Fontend/                                   # Angular 22 Frontend Application
    ├── package.json                               # Scripts, Angular 22 & Dependencies
    ├── proxy.conf.json                            # Proxies /api and /health to port 5080
    ├── src/
    │   ├── app/
    │   │   ├── core/                              # NBR Validators, Interceptors, Guards, Currency
    │   │   │   ├── nbr-validators.ts              # 13-digit BIN, 8-digit HS code, Banking rules
    │   │   │   └── vat-calculations.ts            # Lakh/Crore formatting & VAT math
    │   │   └── features/                          # Feature Modules (Sales, Purchase, Manufacturing, etc.)
    │   └── environments/                          # environment.ts, environment.development.ts
    └── tests/                                     # Frontend Unit & Validation Test Suites (42 Tests)
```

---

## 6. Operation & Execution Guide (ব্যবহার ও কমান্ড নির্দেশিকা)

### 6.0 One-Click Batch Launchers (ডাবল-ক্লিক ব্যাচ ফাইল)
প্রজেক্টের রুট ফোল্ডারে (`G:\dotnetproject\Vat_Management\`) নিচের রেডি ব্যাচ ফাইলগুলো রয়েছে:
- **`run-all.bat`**: এক ক্লিকে ব্যাকএন্ড (পোর্ট ৫০৮০ ও ৫১০১–৫১১২) এবং ফ্রন্টএন্ড (পোর্ট ৪২০০) রান করে স্বয়ংক্রিয়ভাবে ব্রাউজারে `http://localhost:4200` ওপেন করবে।
- **`run-backend.bat`**: শুধুমাত্র ব্যাকএন্ড মাইক্রোসার্ভিস ও গেটওয়ে রান করবে।
- **`run-frontend.bat`**: শুধুমাত্র Angular ফ্রন্টএন্ড রান করবে।
- **`migrate-all.bat`**: সব ১২টি মাইক্রোসার্ভিসের ডাটাবেজ এক ক্লিকে মাইগ্রেট ও ওনারশিপ ভেরিফাই করবে।
- **`stop-all.bat`**: ব্যাকএন্ড ও ফ্রন্টএন্ডের সমস্ত চালু প্রসেস এক ক্লিকে বন্ধ/কিল করবে।

### 6.1 Database Migration (সকল ডাটাবেজ এক ক্লিকে মাইগ্রেশন)
সব ১২টি ডাটাবেজ তৈরি, স্কিমা তৈরি এবং ডাটা ভেরিফিকেশন রান করতে:
```powershell
powershell -File G:\dotnetproject\Vat_Management\VAt_Asper_backend\scripts\Migrate-AllDatabases.ps1
```

### 6.2 Backend Build & Tests (ব্যাকএন্ড টেস্ট ও বিল্ড)
```powershell
# Build entire 81-project solution:
dotnet build G:\dotnetproject\Vat_Management\VAt_Asper_backend\EnterpriseVat.sln

# Run Domain & Messaging tests:
dotnet test G:\dotnetproject\Vat_Management\VAt_Asper_backend\tests\Vat.Messaging.Tests\Vat.Messaging.Tests.csproj
dotnet test G:\dotnetproject\Vat_Management\VAt_Asper_backend\tests\Vat.Messaging.Http.Tests\Vat.Messaging.Http.Tests.csproj
```

### 6.3 Start Backend Gateway / Services (ব্যাকএন্ড রান করা)
```powershell
# Option A: Start unified Gateway host (Port 5080):
cd G:\dotnetproject\Vat_Management\VAt_Asper_backend\src\Vat.Api
dotnet run

# Option B: Start all 12 microservices concurrently (Ports 5101-5112):
powershell -File G:\dotnetproject\Vat_Management\VAt_Asper_backend\scripts\Start-Services.ps1
```

### 6.4 Frontend Build & Tests (ফ্রন্টএন্ড টেস্ট ও রান করা)
```powershell
cd G:\dotnetproject\Vat_Management\Vat_Fontend

# 1. Run NBR Validation & Calculation Tests (42 Passed):
npm run test

# 2. Run TypeScript strict typecheck:
npm run typecheck

# 3. Production build:
npm run build

# 4. Start Angular development server (Port 4200):
npm start
```
ব্রাউজারে ওপেন করুন: `http://localhost:4200`

---
*Created and maintained for Enterprise VAT Management System. All rights reserved.*

