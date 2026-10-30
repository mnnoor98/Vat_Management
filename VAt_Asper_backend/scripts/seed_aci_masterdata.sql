-- ====================================================================
-- ACI LIMITED MASTER DATA IMPORT & CONFIGURATION SCRIPT
-- ====================================================================

DO $$
DECLARE
    v_tenant_id UUID := '00000000-0000-0000-0000-000000000001';
    v_company_id UUID := '11111111-1111-1111-1111-111111111111';
    v_branch_hq UUID := '33333333-3333-3333-3333-333333333331';
    v_branch_factory UUID := '33333333-3333-3333-3333-333333333332';
    v_branch_depot UUID := '33333333-3333-3333-3333-333333333333';
    
    v_user_imran UUID := '99999999-1111-1111-1111-111111111111';
    v_user_maker UUID := '99999999-2222-2222-2222-222222222222';
    v_user_checker UUID := '99999999-3333-3333-3333-333333333333';
    v_user_auditor UUID := '99999999-4444-4444-4444-444444444444';
BEGIN
    -- 1. UPDATE COMPANY DETAILS TO ACI LIMITED
    UPDATE company.companies
    SET "Name" = 'ACI Limited (Advanced Chemical Industries Ltd)',
        "RegistrationReference" = '000124578-0101'
    WHERE "Id" = v_company_id;

    -- 2. UPDATE / INSERT BRANCHES
    UPDATE company.branches
    SET "Name" = 'ACI Centre HQ & Central Distribution, Tejgaon, Dhaka'
    WHERE "Id" = v_branch_hq;

    UPDATE company.branches
    SET "Name" = 'ACI Godrej Narayanganj Plant, Rupganj'
    WHERE "Id" = v_branch_factory;

    UPDATE company.branches
    SET "Name" = 'ACI Agrabad Logistics Depot, Chattogram'
    WHERE "Id" = v_branch_depot;

    -- 3. SETUP USERS: IMRAN (ADMIN), MAKER, CHECKER, AUDITOR
    UPDATE identity.users
    SET "DisplayName" = 'Imran Hossain (Head of VAT & Tax - ACI)',
        "Subject" = 'imran'
    WHERE "Id" = v_user_imran;

    UPDATE identity.users
    SET "DisplayName" = 'Tanvir Ahmed (VAT Maker - ACI)',
        "Subject" = 'maker'
    WHERE "Id" = v_user_maker;

    UPDATE identity.users
    SET "DisplayName" = 'Farhana Yasmin (VAT Checker - ACI)',
        "Subject" = 'checker'
    WHERE "Id" = v_user_checker;

    UPDATE identity.users
    SET "DisplayName" = 'Shafiqul Islam, FCA (Internal Auditor - ACI)',
        "Subject" = 'auditor'
    WHERE "Id" = v_user_auditor;

    -- Update user credentials username
    UPDATE identity.user_credentials
    SET "username" = 'imran'
    WHERE "userid" = v_user_imran;

    -- Also insert/ensure 'admin' credentials remain so either 'imran' or 'admin' works
    DELETE FROM identity.user_credentials WHERE "username" = 'admin';
    INSERT INTO identity.user_credentials ("userid", "username", "passwordhash", "passwordsalt", "createdat")
    VALUES (v_user_imran, 'admin', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 'SALT_2026_ADMIN', NOW())
    ON CONFLICT DO NOTHING;

    -- 4. INSERT ACI HS CODES
    INSERT INTO masterdata.hs_codes ("TenantId", "CompanyId", "Id", "Code", "Description", "EffectiveFrom", "CreatedAt", "CreatedBy", "Version")
    VALUES 
        (v_tenant_id, v_company_id, gen_random_uuid(), '1514.91.00', 'Mustard Oil & Fractions (সরিষার তেল)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '2501.00.10', 'Pure Vacuum Evaporated Iodized Salt (আয়োডিনযুক্ত ভোজ্য লবণ)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '3004.90.99', 'Medicaments & Antiseptics - Savlon Liquid (স্যাভলন অ্যান্টিসেপ্টিক)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '3808.91.10', 'Aerosol Insecticides (এ্যারোসল মশানাশক)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '1101.00.10', 'Wheat Flour / Atta (আটা)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '1207.50.90', 'Raw Mustard Seeds (কাঁচা সরিষা বীজ)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '2925.29.00', 'Chlorhexidine Gluconate Chemical (অ্যান্টিসেপ্টিক ফর্মুলেশন)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '3923.30.00', 'PET Bottles & Plastic Closures (বোতল ও ক্যাপ)', '2026-01-01', NOW(), 'Imran (Admin)', 1),
        (v_tenant_id, v_company_id, gen_random_uuid(), '7310.29.00', 'Tinplate Aerosol Cans with Valve (টিন ক্যান)', '2026-01-01', NOW(), 'Imran (Admin)', 1)
    ON CONFLICT DO NOTHING;

    -- 5. INSERT ACI PRODUCTS (FINISHED GOODS & RAW MATERIALS)
    INSERT INTO masterdata.products ("Id", "TenantId", "CompanyId", "Reference", "Description", "UnitOfMeasure", "TaxCode")
    VALUES 
        ('66666666-aaaa-aaaa-aaaa-000000000001', v_tenant_id, v_company_id, 'PROD-ACI-01', 'ACI Pure Mustard Oil 1 Litre Bottle (সরিষার তেল ১ লিটার)', 'BTL', '1514.91.00'),
        ('66666666-aaaa-aaaa-aaaa-000000000002', v_tenant_id, v_company_id, 'PROD-ACI-02', 'ACI Pure Vacuum Evaporated Iodized Salt 1kg (আয়োডাইজড লবণ ১ কেজি)', 'PKT', '2501.00.10'),
        ('66666666-aaaa-aaaa-aaaa-000000000003', v_tenant_id, v_company_id, 'PROD-ACI-03', 'Savlon Antiseptic Liquid 500ml (স্যাভলন অ্যান্টিসেপ্টিক ৫০০ মিলি)', 'BTL', '3004.90.99'),
        ('66666666-aaaa-aaaa-aaaa-000000000004', v_tenant_id, v_company_id, 'PROD-ACI-04', 'ACI Aerosol Insecticide Spray 475ml (এ্যারোসল মশানাশক ৪৭৫ মিলি)', 'CAN', '3808.91.10'),
        ('66666666-aaaa-aaaa-aaaa-000000000005', v_tenant_id, v_company_id, 'PROD-ACI-05', 'ACI Pure Premium Atta 2kg (আটা ২ কেজি)', 'BAG', '1101.00.10'),
        ('66666666-aaaa-aaaa-aaaa-000000000006', v_tenant_id, v_company_id, 'RM-ACI-01', 'Raw Unrefined Mustard Seeds (কাঁচা সরিষা বীজ)', 'KG', '1207.50.90'),
        ('66666666-aaaa-aaaa-aaaa-000000000007', v_tenant_id, v_company_id, 'RM-ACI-02', 'Food Grade Raw Salt Crystal (অপরিশোধিত ক্রিস্টাল লবণ)', 'KG', '2501.00.10'),
        ('66666666-aaaa-aaaa-aaaa-000000000008', v_tenant_id, v_company_id, 'RM-ACI-03', 'Chlorhexidine Gluconate Solution (স্যাভলন অ্যান্টিসেপ্টিক সলিউশন)', 'LTR', '2925.29.00'),
        ('66666666-aaaa-aaaa-aaaa-000000000009', v_tenant_id, v_company_id, 'RM-ACI-04', 'Active Permethrin Formulation (অ্যারোসল অ্যাক্টিভ ফর্মুলেশন)', 'KG', '2916.20.00'),
        ('66666666-aaaa-aaaa-aaaa-000000000010', v_tenant_id, v_company_id, 'RM-ACI-05', '1 Litre Food-Grade PET Bottles & Caps (১ লিটার বোতল ও ক্যাপ)', 'PCS', '3923.30.00'),
        ('66666666-aaaa-aaaa-aaaa-000000000011', v_tenant_id, v_company_id, 'RM-ACI-06', 'Printed Aerosol Tin Cans with Valve (মুদ্রিত এ্যারোসল টিন ক্যান)', 'PCS', '7310.29.00')
    ON CONFLICT ("TenantId", "CompanyId", "Reference") DO UPDATE 
    SET "Description" = EXCLUDED."Description", "UnitOfMeasure" = EXCLUDED."UnitOfMeasure", "TaxCode" = EXCLUDED."TaxCode";

    -- 6. INSERT ACI SUPPLIERS
    INSERT INTO masterdata.suppliers ("Id", "TenantId", "CompanyId", "Reference", "Name")
    VALUES 
        ('77777777-aaaa-aaaa-aaaa-000000000001', v_tenant_id, v_company_id, 'SUP-ACI-01', 'Meghna Seed Crushing & Extraction Mills Ltd (BIN: 000234891-0101)'),
        ('77777777-aaaa-aaaa-aaaa-000000000002', v_tenant_id, v_company_id, 'SUP-ACI-02', 'Bashundhara Chemical & Polymer Industries Ltd (BIN: 000345678-0202)'),
        ('77777777-aaaa-aaaa-aaaa-000000000003', v_tenant_id, v_company_id, 'SUP-ACI-03', 'Akij Tin Container Manufacturing Ltd (BIN: 000456789-0101)'),
        ('77777777-aaaa-aaaa-aaaa-000000000004', v_tenant_id, v_company_id, 'SUP-ACI-04', 'Bengal Plastics & Packaging Ltd (BIN: 000567890-0303)'),
        ('77777777-aaaa-aaaa-aaaa-000000000005', v_tenant_id, v_company_id, 'SUP-ACI-05', 'Glencore International Chemical AG (Customs BOE Chittagong)')
    ON CONFLICT ("TenantId", "CompanyId", "Reference") DO UPDATE 
    SET "Name" = EXCLUDED."Name";

    -- 7. INSERT ACI CUSTOMERS / SUPERMARKETS
    INSERT INTO sales.customers ("Id", "TenantId", "CompanyId", "Code", "Name", "Contact", "Address", "TaxRegistrationReference", "IsActive", "Version", "CreatedAt", "UpdatedAt")
    VALUES 
        ('77777777-bbbb-bbbb-bbbb-000000000001', v_tenant_id, v_company_id, 'CUST-ACI-01', 'ACI Logistics Ltd (Shwapno Superstore / স্বপ্ন সুপারশপ)', 'Sabbir Hasan Nasir (+880 1711-555111)', 'ACI Centre, 245 Tejgaon Industrial Area, Dhaka', '000987654-0101', true, 1, NOW(), NOW()),
        ('77777777-bbbb-bbbb-bbbb-000000000002', v_tenant_id, v_company_id, 'CUST-ACI-02', 'Chaldal Limited (চালডাল অনলাইন গ্রোসারি)', 'Waseem Alim (+880 1819-666222)', 'Plot #12, Block #K, Baridhara, Dhaka-1212', '001122334-0101', true, 1, NOW(), NOW()),
        ('77777777-bbbb-bbbb-bbbb-000000000003', v_tenant_id, v_company_id, 'CUST-ACI-03', 'Agora Superstores / Rahimafrooz Superstores Ltd', 'Niaz Rahim (+880 1713-777333)', 'Gulshan South Avenue, Gulshan-1, Dhaka-1212', '001334455-0101', true, 1, NOW(), NOW()),
        ('77777777-bbbb-bbbb-bbbb-000000000004', v_tenant_id, v_company_id, 'CUST-ACI-04', 'Meena Bazar / Gemcon Food & Agricultural Products', 'Kazi Inam Ahmed (+880 1911-888444)', 'House #44, Road #16 (New), 27 (Old), Dhanmondi, Dhaka', '001556677-0101', true, 1, NOW(), NOW()),
        ('77777777-bbbb-bbbb-bbbb-000000000005', v_tenant_id, v_company_id, 'CUST-ACI-05', 'Haji Bismillah Wholesalers & Distributors', 'Haji Mohammad Rafiq (+880 1817-999555)', '142 Khatungonj Commercial Area, Kotwali, Chattogram', '001778899-0101', true, 1, NOW(), NOW())
    ON CONFLICT ("TenantId", "CompanyId", "Code") DO UPDATE 
    SET "Name" = EXCLUDED."Name", "TaxRegistrationReference" = EXCLUDED."TaxRegistrationReference", "Address" = EXCLUDED."Address";

    -- 8. INSERT ACI WAREHOUSES
    INSERT INTO inventory.warehouses ("Id", "TenantId", "CompanyId", "BranchId", "Code", "Name")
    VALUES 
        ('55555555-aaaa-aaaa-aaaa-000000000001', v_tenant_id, v_company_id, v_branch_hq, 'WH-ACI-01', 'ACI Central Finished Goods Warehouse, Tejgaon, Dhaka'),
        ('55555555-aaaa-aaaa-aaaa-000000000002', v_tenant_id, v_company_id, v_branch_factory, 'WH-ACI-02', 'ACI Godrej Narayanganj Plant Raw Material Silo, Rupganj'),
        ('55555555-aaaa-aaaa-aaaa-000000000003', v_tenant_id, v_company_id, v_branch_depot, 'WH-ACI-03', 'ACI Agrabad Depot Warehouse, Chattogram')
    ON CONFLICT ("TenantId", "CompanyId", "Code") DO UPDATE 
    SET "Name" = EXCLUDED."Name";

    RAISE NOTICE 'ACI Limited Master Data & Organization Successfully Seeded!';
END $$;
