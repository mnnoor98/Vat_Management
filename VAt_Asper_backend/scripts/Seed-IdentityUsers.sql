-- ============================================================
-- Enterprise VAT Management - Identity Users and Permissions Seed
-- ============================================================

DO $$
DECLARE
    v_tenant_id uuid := '00000000-0000-0000-0000-000000000001';
    v_company_id uuid := '11111111-1111-1111-1111-111111111111';
    v_branch_id uuid := '33333333-3333-3333-3333-333333333331';
    
    v_role_admin uuid := 'aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa';
    v_role_maker uuid := 'bbbbbbbb-2222-2222-2222-bbbbbbbbbbbb';
    v_role_checker uuid := 'cccccccc-3333-3333-3333-cccccccccccc';
    v_role_auditor uuid := 'dddddddd-4444-4444-4444-dddddddddddd';
    
    v_user_admin uuid := '99999999-1111-1111-1111-111111111111';
    v_user_maker uuid := '99999999-2222-2222-2222-222222222222';
    v_user_checker uuid := '99999999-3333-3333-3333-333333333333';
    v_user_auditor uuid := '99999999-4444-4444-4444-444444444444';

    p_code text;
    p_id uuid;
    
    permissions text[] := ARRAY[
        'identity.read', 'manufacturing.read', 'manufacturing.request', 'manufacturing.approve',
        'compliance.read', 'compliance.manage', 'compliance.submit', 'workflow.read', 'integration.read',
        'payment.reverse.request', 'payment.reverse.approve', 'payment.refund.request', 'payment.refund.approve',
        'payment.read', 'payment.request', 'payment.approve', 'payment.allocate', 'company.read', 'company.manage',
        'purchase.read', 'purchase.create', 'purchase.submit', 'purchase.approve', 'masterdata.manage',
        'masterdata.read', 'inventory.manage', 'inventory.read', 'inventory.approve', 'inventory.post',
        'tax.rule.manage', 'tax.rule.publish', 'tax.read', 'purchase.prepare', 'purchase.post', 'period.read',
        'period.manage', 'period.close.request', 'period.close.approve', 'purchase.reverse.request',
        'purchase.reverse.approve', 'purchase.cancel', 'report.read', 'report.manage', 'messaging.read',
        'messaging.retry', 'sales.debit.request', 'sales.debit.approve', 'sales.credit.request',
        'sales.credit.approve', 'sales.reserve', 'sales.post', 'sales.read', 'sales.create', 'sales.submit',
        'sales.approve', 'sales.cancel'
    ];
BEGIN
    -- 1. Create Roles
    INSERT INTO identity.roles ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "Code", "Name")
    VALUES 
        (v_tenant_id, v_company_id, v_role_admin, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'ADMIN', 'System Administrator'),
        (v_tenant_id, v_company_id, v_role_maker, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'MAKER', 'VAT Entry Maker'),
        (v_tenant_id, v_company_id, v_role_checker, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'CHECKER', 'VAT Approval Checker'),
        (v_tenant_id, v_company_id, v_role_auditor, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'AUDITOR', 'VAT Compliance Auditor')
    ON CONFLICT ("TenantId", "CompanyId", "Id") DO UPDATE SET "Name" = EXCLUDED."Name";

    -- 2. Create Users
    INSERT INTO identity.users ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "Issuer", "Subject", "DisplayName", "Disabled")
    VALUES
        (v_tenant_id, v_company_id, v_user_admin, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'https://localhost:5080', 'admin', 'System Administrator', false),
        (v_tenant_id, v_company_id, v_user_maker, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'https://localhost:5080', 'maker', 'Rahim Ahmed (VAT Maker)', false),
        (v_tenant_id, v_company_id, v_user_checker, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'https://localhost:5080', 'checker', 'Karim Ullah (VAT Checker)', false),
        (v_tenant_id, v_company_id, v_user_auditor, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, 'https://localhost:5080', 'auditor', 'Nasreen Akter (Auditor)', false)
    ON CONFLICT ("TenantId", "CompanyId", "Id") DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName", "Disabled" = false;

    -- 3. Link User Roles
    INSERT INTO identity.user_roles ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "UserId", "RoleId", "BranchId")
    VALUES
        (v_tenant_id, v_company_id, '88888888-1111-1111-1111-111111111111', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_user_admin, v_role_admin, v_branch_id),
        (v_tenant_id, v_company_id, '88888888-2222-2222-2222-222222222222', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_user_maker, v_role_maker, v_branch_id),
        (v_tenant_id, v_company_id, '88888888-3333-3333-3333-333333333333', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_user_checker, v_role_checker, v_branch_id),
        (v_tenant_id, v_company_id, '88888888-4444-4444-4444-444444444444', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_user_auditor, v_role_auditor, v_branch_id)
    ON CONFLICT ("TenantId", "CompanyId", "Id") DO NOTHING;

    -- 4. Create and Link Permissions
    FOREACH p_code IN ARRAY permissions
    LOOP
        p_id := md5(p_code)::uuid;
        
        -- Insert Permission
        INSERT INTO identity.permissions ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "Code", "Description")
        VALUES (v_tenant_id, v_company_id, p_id, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, p_code, 'Permission for ' || p_code)
        ON CONFLICT ("TenantId", "CompanyId", "Id") DO NOTHING;
        
        -- ADMIN gets all permissions
        INSERT INTO identity.role_permissions ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "RoleId", "PermissionId")
        VALUES (v_tenant_id, v_company_id, md5('admin_' || p_code)::uuid, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_role_admin, p_id)
        ON CONFLICT ("TenantId", "CompanyId", "Id") DO NOTHING;
        
        -- MAKER gets create, submit, manage, request, read permissions (no approve)
        IF p_code NOT LIKE '%.approve' AND p_code NOT LIKE '%.close' AND p_code NOT LIKE '%.publish' THEN
            INSERT INTO identity.role_permissions ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "RoleId", "PermissionId")
            VALUES (v_tenant_id, v_company_id, md5('maker_' || p_code)::uuid, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_role_maker, p_id)
            ON CONFLICT ("TenantId", "CompanyId", "Id") DO NOTHING;
        END IF;

        -- CHECKER gets read, approve, close, cancel permissions
        IF p_code LIKE '%.read' OR p_code LIKE '%.approve' OR p_code LIKE '%.close.approve' OR p_code LIKE '%.cancel' THEN
            INSERT INTO identity.role_permissions ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "RoleId", "PermissionId")
            VALUES (v_tenant_id, v_company_id, md5('checker_' || p_code)::uuid, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_role_checker, p_id)
            ON CONFLICT ("TenantId", "CompanyId", "Id") DO NOTHING;
        END IF;

        -- AUDITOR gets read-only permissions
        IF p_code LIKE '%.read' THEN
            INSERT INTO identity.role_permissions ("TenantId", "CompanyId", "Id", "CreatedAt", "UpdatedAt", "CreatedBy", "Version", "RoleId", "PermissionId")
            VALUES (v_tenant_id, v_company_id, md5('auditor_' || p_code)::uuid, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 'SystemSeed', 1, v_role_auditor, p_id)
            ON CONFLICT ("TenantId", "CompanyId", "Id") DO NOTHING;
        END IF;
    END LOOP;
END $$;
