-- Seed Master Data (Products, Suppliers, Warehouse, and Published Tax Rules)
BEGIN;

-- 1. Inventory Warehouse
INSERT INTO inventory.warehouses ("Id", "TenantId", "CompanyId", "BranchId", "Code", "Name")
VALUES 
  ('55555555-5555-5555-5555-555555555551', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333331', 'WH-01', 'Dhaka Central Plant Main Warehouse'),
  ('55555555-5555-5555-5555-555555555552', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333331', 'WH-02', 'Raw Materials Silo')
ON CONFLICT DO NOTHING;

-- 2. Master Data Products
INSERT INTO masterdata.products ("Id", "TenantId", "CompanyId", "Reference", "Description", "UnitOfMeasure", "TaxCode")
VALUES 
  ('66666666-6666-6666-6666-666666666661', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'PROD-01', 'High Density Polyethylene Resin (HDPE)', 'KG', 'STD-15'),
  ('66666666-6666-6666-6666-666666666662', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'PROD-02', 'Polypropylene Polymer Granules (PP)', 'KG', 'STD-15'),
  ('66666666-6666-6666-6666-666666666663', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'PROD-RAW-01', 'Raw Material Chemical Additive', 'KG', 'STD-15'),
  ('66666666-6666-6666-6666-666666666664', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'PROD-PACK-01', 'Finished Consumer Food Packaging Roll', 'PCS', 'STD-15'),
  ('66666666-6666-6666-6666-666666666665', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'PRD-FG-001', '500ml Bottled Consumer Liquid Soap', 'PCS', 'STD-15')
ON CONFLICT DO NOTHING;

-- 3. Master Data Suppliers
INSERT INTO masterdata.suppliers ("Id", "TenantId", "CompanyId", "Reference", "Name")
VALUES 
  ('77777777-7777-7777-7777-777777777771', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'PTY-SUP-01', 'Bengal Polymer Industries Ltd'),
  ('77777777-7777-7777-7777-777777777772', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'PTY-SUP-02', 'Meghna Petrochemicals Corp'),
  ('77777777-7777-7777-7777-777777777773', '00000000-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'Padma Raw Materials Supplier', 'Padma Raw Materials Supplier')
ON CONFLICT DO NOTHING;

-- 4. Published Tax Rules for STD-15 (Purchase & Sale)
INSERT INTO tax.tax_rule_versions (
  "Id", "TenantId", "CompanyId", "TaxCode", "EffectiveFrom", "EffectiveUntil",
  "RatePercent", "RecoverablePercent", "LegalBasis", "EvidenceReference",
  "CreatedBy", "PublishedBy", "PublishedAt", "Version", "Treatment"
) VALUES 
  (
    '88888888-8888-8888-8888-888888888881',
    '00000000-0000-0000-0000-000000000001',
    '11111111-1111-1111-1111-111111111111',
    'STD-15',
    '2026-01-01',
    '2030-12-31',
    15.0000,
    100.0000,
    'Value Added Tax and Supplementary Duty Act 2012, Section 15 & Section 46',
    'SRO-144-AIN/2026/VAT-Input-Rebate',
    'USR-8801',
    'USR-8804',
    NOW(),
    1,
    'Purchase'
  ),
  (
    '88888888-8888-8888-8888-888888888882',
    '00000000-0000-0000-0000-000000000001',
    '11111111-1111-1111-1111-111111111111',
    'STD-15',
    '2026-01-01',
    '2030-12-31',
    15.0000,
    0.0000,
    'Value Added Tax and Supplementary Duty Act 2012, Section 15 Standard Rate',
    'Mushak-6.3 Statutory Standard Rate Output Tax',
    'USR-8801',
    'USR-8804',
    NOW(),
    1,
    'Sale'
  )
ON CONFLICT DO NOTHING;

COMMIT;
