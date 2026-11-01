# Stock valuation-এর পরবর্তী implementation plan

> 2026-11-01 update: physical scrap/by-product stock, the Manufacturing execution-status inbox, material-return reversal and cumulative partial output correction are implemented. The durable queue projects committed financial execution events with deduplication, monotonic versions and quarantine recovery. The next production-correction priority is the reviewed closed-execution contract, followed by Import/Export and subcontract production. See [physical output guide](VAt_Asper_backend/docs/PRODUCTION_PHYSICAL_OUTPUTS.md), [execution inbox guide](VAt_Asper_backend/docs/PRODUCTION_EXECUTION_INBOX.md) and [reversal guide](VAt_Asper_backend/docs/PRODUCTION_REVERSALS.md).

তারিখ: ৩১ অক্টোবর ২০২৬, Asia/Dhaka। Scope: VAT Management; Trading + Manufacturing, Import/Export এবং subcontract production। পূর্ণ ERP/HR/payroll এই scope-এর অংশ নয়।

## এই ধাপে বাস্তবায়িত

UOM batch-এ actual material issue/return ও finished receipt-এর alternative input unit ratio order approval-এ freeze করার ব্যবস্থা যুক্ত হয়েছে। Entered quantity/unit ও normalized stock quantity দুটোই retained থাকে; non-exact conversion rounding ছাড়াই reject হয়। Planned quantity ও BOM এখনও stock UOM-এ। [Input UOM guide](VAt_Asper_backend/docs/PRODUCTION_INPUT_UNITS.md)-এ contract ও migration দেখুন।

Production correction batch-এ Conversion, Loss ও Finished Receipt-এর full source-linked reversal যুক্ত হয়েছে। নতুন document/date/reason/independent approval দিয়ে correction হয়; closed-period snapshot ও original journal অপরিবর্তিত থাকে। বিস্তারিত: [Reversal guide](VAt_Asper_backend/docs/PRODUCTION_REVERSALS.md)। Partial correction, material-return reversal ও closed execution reopen এখনও বাকি।

Manufacturing batch-এ approved production order/BOM snapshot, actual material issue/return, WIP, actual labor/utility/overhead, partial finished receipt, reviewed WIP loss এবং zero-WIP close-এর API/UI যুক্ত হয়েছে। Stock-এর নির্বাচিত cost basis অপরিবর্তিত। বিস্তারিত: [Production/WIP guide](VAt_Asper_backend/docs/PRODUCTION_WIP_EXECUTION.md)। নিচের মূল backlog-এর Manufacturing contract-এর এই অংশ বাস্তবায়িত; BOM/planned-output unit conversion, physical scrap/by-product, advanced correction এবং reverse owner-status consumer বাকি।

বিদ্যমান financial stock workflow-এর জন্য approved opening adoption, FIFO/Moving Weighted Average-এর persisted cost state, immutable cost journal, independently approved landed-cost allocation এবং stock balance/summary valuation যুক্ত হয়েছে। নির্বাচিত basis: **net purchase + non-recoverable VAT + allocated landed cost**।

Purchase receipt, sales issue, partial/full goods return, whole purchase reversal ও warehouse transfer একই physical/source transaction-এ cost বহন করে। Reconciled অবস্থাতেই value পাওয়া যায়; policy selector দিয়ে ledger method বদলানো যায় না। Opening-এর পরবর্তী চলমান stock-এর valuation এবং ঐতিহাসিক FIFO reconstruction এক বিষয় নয়—এই implementation opening cutover থেকে journal তৈরি করে।

বাস্তবায়ন ও সীমা: [Stock valuation guide](VAt_Asper_backend/docs/STOCK_VALUATION_LEDGER.md)। Test evidence: [Implementation report](EXPERT_IMPLEMENTATION_2026-10-31.md)। Live migration/deployment করা হয়নি।

## কাজের অগ্রাধিকার

পরবর্তী execution batch-এর ক্রম:

1. **Production completion:** physical scrap/by-product quantity/value, Manufacturing-এ execution-status event consumer এবং advanced correction। Actual-entry UOM conversion ও conversion/loss/finished-receipt full reversal ইতোমধ্যে যুক্ত হয়েছে; BOM/planned-output alternative units আলাদা enhancement। Acceptance: failed delivery/retry-তে duplicate acknowledgement হবে না; correction বন্ধ period বদলাবে না; material + conversion = finished + remaining WIP + approved loss/by-product।
2. **Import/Export:** প্রথমে declaration/document model ও independently approved currency/tax/charge evidence; তারপর import receipt allocation ও export shipment linkage। Acceptance: একই charge দুবার capitalize হবে না; unknown rate বা missing evidence posting আটকাবে; dispatch cost existing ledger-এর সঙ্গে মিলবে।
3. **Subcontract:** partner custody ও ownership model, dispatch/return/consumption, service-charge approval এবং finished receipt। Acceptance: partner location-এ থাকা company-owned material quantity/value traceable থাকবে; customer-owned material company valuation-এ ঢুকবে না।
4. **Staging release:** migration/restore rehearsal, real opening dataset, role-based browser UAT, concurrent posting/close এবং representative-volume performance checks।

| ধাপ | Deliverable | সম্পন্ন হওয়ার শর্ত |
|---|---|---|
| P0 — Controlled valuation pilot | Reviewed migration, company/product policies, actual opening reconciliation, landed charge evidence, role-based browser UAT | Opening quantities/source totals agree; two-person approval works; receipt → sale → partial return → transfer → reversal conserves value; backup/restore succeeds |
| P1 — Manufacturing stock posting | Approved production order/BOM snapshot, actual material issue, WIP cost, finished-goods receipt, conversion charges, wastage/scrap/by-product rules | Actual consumed material cost + approved conversion cost = finished goods + WIP + approved loss/by-product allocation; no coefficient estimate is posted as actual stock cost |
| P2 — Import/Export workflow | Import declaration/supporting documents, reviewed functional-currency/tax basis, landed allocation, shipment/export evidence | Declared source components reconcile to receipt/tax/cost journals; export dispatch reuses the cost ledger and has reviewed tax/evidence mapping |
| P3 — Subcontract production | Partner custody locations, owned-material dispatch/return, consumption confirmation, subcontract charges and finished-goods return | Owned quantity/value remains traceable at the subcontractor; unused material, consumption, loss and finished goods reconcile without double stock recognition |
| P4 — Advanced cost corrections | Late landed-cost invoices, inventory/issued-cost split, reviewed cost-method conversion, partial supplier returns, detailed legacy layer import | Corrections reference original sources, conserve value, preserve closed periods and record independent approval; no historical source rewrite |
| P5 — Valuation close/reporting | Period-end cost checkpoints, historical-as-of reporting, source-to-report reconciliation, exact exports | Closed snapshots are immutable and reproducible; current and historical totals agree with their journal checkpoints |
| P6 — Release hardening | Representative load, concurrent posting/close tests, identity/broker validation, backup restore and deployment rollback | Agreed volume and recovery targets are demonstrated in staging; all required integration tests pass, and business users sign off |

## Manufacturing contract: delivered foundation এবং বাকি কাজ

Manufacturing order approval এবং financial material issue/WIP/finished-receipt posting এখন আছে। Coefficient-এর theoretical unit cost actual inventory value হিসেবে ব্যবহার হয় না। নিচের contract-এর অবশিষ্ট advanced workflow পরবর্তী batch-এ সম্পন্ন করতে হবে।

1. **Done:** approved order/BOM revision, product stock UOM, output warehouse ও planned quantity frozen; actual input UOM ratios order evidence-এ frozen। **Next:** physical scrap/by-product-এর পৃথক quantity/value source; প্রয়োজন হলে BOM/planned quantity-এর alternative unit contract।
2. **Done:** FIFO/Moving Average material issue-এর actual value WIP-এ যায়; labor/utility/overhead নিজস্ব evidence ও approval বহন করে। **Next:** subcontract service charge ও ownership/custody reconciliation।
3. **Done:** financial owner stock/cost/WIP authority; authenticated owner-source import, idempotent command ও durable outbox আছে। **Next:** Manufacturing-এ execution-status consumer, inbox deduplication ও delivery recovery। আলাদা database-এর write cross-owner atomic নয়।
4. **Done:** finished receipt explicit reviewed actual WIP allocation থেকে value নেয়। **Next:** coefficient estimate বনাম actual variance reporting; estimate actual cost-এর বিকল্প নয়।
5. **Done:** partial completion, unused-material return, WIP cost loss, draft cancellation, period checkpoint ও conversion/loss/receipt full reversal। **Next:** physical waste, partial corrections, material-return reversal ও closed execution correction-এর reviewed contract।
6. **Done:** দুই raw material, পৃথক output warehouse, partial completion, material return, retry/rollback ও দুই costing method-এর synthetic acceptance dataset। Closed-period correction ও reserved/consumed-stock reversal tests-ও যুক্ত। **Next:** representative business dataset, UAT এবং reviewed statutory tax mapping।

## Import/Export ও subcontract-এর নির্ভরতা

Import-এ currency, reviewed conversion rate/evidence, recoverable/non-recoverable tax components এবং capitalizable landed charges নির্দিষ্ট source থেকে আসবে। একই fee tax calculation ও landed allocation-এ দুইবার যোগ করা যাবে না। Receipt posting-এর পর আসা charge P4 correction workflow ছাড়া পুরোনো source-এ বসানো যাবে না।

Export shipment/evidence approval ও tax treatment নতুন workflow; stock dispatch cost বিদ্যমান sales/stock ledger থেকে আসবে। Tax form, exemption বা rate-এর বর্তমান legal correctness এই code batch নিশ্চিত করে না।

Subcontract-এ company-owned material partner custody-তে গেলেও ownership থাকে। Custody transfer, consumption, returned unused material, approved loss, finished-goods receipt এবং service charge আলাদা evidence বহন করবে। Customer-owned material থাকলে সেটিকে company-owned stock value-তে মেশানো যাবে না; ownership rule আগে নির্ধারণ করতে হবে।

## Pilot-এর জন্য business data

Company/BIN/branch structure, actual opening quantity/value evidence, legacy FIFO layer detail প্রয়োজন কি না, landed charge categories/source documents, UOM conversions, production yield/waste rules, subcontract ownership/settlement এবং expected transaction volume প্রয়োজন। এগুলো implementation-এর acceptance dataset ও review সিদ্ধান্ত হবে; অনুপস্থিত cost বা tax rate অনুমান করে পূরণ করা হবে না।

কাজের ক্রম: **controlled valuation pilot → Manufacturing issue/WIP/receipt → Import/Export → subcontract production → advanced corrections/reporting → release hardening**। Calendar estimate দেওয়ার আগে representative dataset ও operational volume যাচাই করতে হবে।
