-- Read-only quantity diagnostic used in the 2026-10-24 project review.
-- Current version requires financial migration 18 and includes paired transfer journals.
-- Matches the quantity-journal basis of PeriodStore.BalancesReconcile.
-- This does not reconcile reservations, tax, valuation, payments or all periods.
-- A mismatch is a review finding, not authorization to overwrite balances.
WITH movements AS (
    SELECT "TenantId", "CompanyId", "WarehouseId", "ProductId", "Quantity"
    FROM inventory.stock_movements
    UNION ALL
    SELECT "TenantId", "CompanyId", "WarehouseId", "ProductId", "Quantity"
    FROM sales.posting_entries WHERE "EntryType" = 'Goods'
    UNION ALL
    SELECT "TenantId", "CompanyId", "FromWarehouseId", "ProductId", -"Quantity"
    FROM inventory.transfer_journal
    UNION ALL
    SELECT "TenantId", "CompanyId", "ToWarehouseId", "ProductId", "Quantity"
    FROM inventory.transfer_journal
), expected AS (
    SELECT "TenantId", "CompanyId", "WarehouseId", "ProductId", sum("Quantity") AS quantity
    FROM movements GROUP BY "TenantId", "CompanyId", "WarehouseId", "ProductId"
), compared AS (
    SELECT b."Quantity" AS actual, e.quantity AS expected,
        b."ProductId" AS balance_product, e."ProductId" AS journal_product
    FROM inventory.stock_balances b FULL JOIN expected e
        ON b."TenantId" = e."TenantId" AND b."CompanyId" = e."CompanyId"
        AND b."WarehouseId" = e."WarehouseId" AND b."ProductId" = e."ProductId"
)
SELECT (SELECT count(*) FROM inventory.stock_balances) AS balance_rows,
    count(*) FILTER (WHERE balance_product IS NULL OR journal_product IS NULL
        OR actual <> expected) AS unreconciled_quantity_rows
FROM compared;
