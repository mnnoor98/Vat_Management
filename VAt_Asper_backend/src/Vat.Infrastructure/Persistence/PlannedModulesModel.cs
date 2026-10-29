using Microsoft.EntityFrameworkCore;

namespace Vat.Infrastructure.Persistence;

// Relational foundations only. Property bags keep inactive module persistence out of
// the executable domain until its business workflows are implemented.
internal static class PlannedModulesModel
{
    public static void Configure(ModelBuilder model)
    {
        Table(model, "identity.users", "Issuer:s500 Subject:s200 DisplayName:s200 Disabled:b", "Issuer,Subject", "");
        Table(model, "identity.roles", "Code:s100 Name:s200", "Code", "");
        Table(model, "identity.permissions", "Code:s200 Description:s500", "Code", "");
        Table(model, "identity.user_roles", "UserId:g->identity.users RoleId:g->identity.roles BranchId:g?->company.branches", "UserId,RoleId,BranchId", "");
        Table(model, "identity.role_permissions", "RoleId:g->identity.roles PermissionId:g->identity.permissions", "RoleId,PermissionId", "");
        Table(model, "identity.menu_items", "Code:s100 Label:s200 Route:s500 ParentId:g?->identity.menu_items", "Code", "(\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\")");
        Table(model, "identity.role_menus", "RoleId:g->identity.roles MenuId:g->identity.menu_items", "RoleId,MenuId", "");
        Table(model, "identity.refresh_sessions", "UserId:g->identity.users TokenDigest:s128 FamilyId:g ExpiresAt:t RevokedAt:t? ReplacedById:g?->identity.refresh_sessions", "TokenDigest", "(\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\") AND (\"ReplacedById\" IS NULL OR \"ReplacedById\" <> \"Id\"))");
        Table(model, "identity.security_audit", "UserId:g?->identity.users Action:s100 Resource:s500 OccurredAt:t CorrelationId:s100 Outcome:s40", "", "");
        Table(model, "company.registrations", "BranchId:g?->company.branches RegistrationType:s80 Reference:s100 EffectiveFrom:d EffectiveUntil:d? ApprovedBy:s200?", "RegistrationType,Reference,EffectiveFrom", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
        Table(model, "company.company_settings", "Key:s100 Value:j", "Key", "");
        Table(model, "company.branch_access", "UserId:g->identity.users BranchId:g->company.branches PermissionId:g->identity.permissions", "UserId,BranchId,PermissionId", "");
        Table(model, "masterdata.uoms", "Code:s30 Name:s100 DecimalPlaces:i", "Code", "(\"DecimalPlaces\" BETWEEN 0 AND 6)");
        Table(model, "masterdata.hs_codes", "Code:s30 Description:s1000 EffectiveFrom:d EffectiveUntil:d?", "Code,EffectiveFrom", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\")");
        Table(model, "masterdata.product_versions", "ProductId:g->masterdata.products Revision:i Description:s500 UomId:g->masterdata.uoms EffectiveFrom:d EffectiveUntil:d? ApprovedBy:s200?", "ProductId,Revision", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
        Table(model, "masterdata.product_tax_classifications", "ProductVersionId:g->masterdata.product_versions HsCodeId:g->masterdata.hs_codes TaxCode:s100 EffectiveFrom:d EffectiveUntil:d?", "ProductVersionId,EffectiveFrom", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\")");
        Table(model, "masterdata.counterparty_registrations", "CustomerId:g?->sales.customers SupplierId:g?->masterdata.suppliers RegistrationType:s80 Reference:s100 EffectiveFrom:d EffectiveUntil:d?", "", "((\"CustomerId\" IS NULL) <> (\"SupplierId\" IS NULL)) AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\")");
        Table(model, "purchasing.purchase_attachments", "PurchaseId:g->purchasing.purchases ObjectKey:s1000 Sha256:s64 ContentType:s200 SizeBytes:l", "PurchaseId,Sha256", "(\"SizeBytes\" > 0 AND length(\"Sha256\") = 64)");
        Table(model, "purchasing.purchase_posting_sagas", "PurchaseId:g->purchasing.purchases SourceVersion:i State:s40 LastError:s2000? NextAttemptAt:t? AttemptCount:i", "PurchaseId,SourceVersion", "(\"SourceVersion\" > 0 AND \"AttemptCount\" >= 0 AND \"State\" IN ('Pending','Running','Completed','Failed'))");
        Table(model, "sales.delivery_references", "InvoiceId:g->sales.sales_invoices Reference:s100 DeliveredAt:t? EvidenceKey:s1000?", "InvoiceId,Reference", "");
        Table(model, "sales.challan_numbers", "BranchId:g->company.branches FiscalYear:i Series:s40 Number:l InvoiceId:g?->sales.sales_invoices VoidedAt:t? VoidReason:s1000?", "BranchId,FiscalYear,Series,Number", "(\"Number\" > 0 AND \"FiscalYear\" BETWEEN 1900 AND 9999 AND ((\"VoidedAt\" IS NULL AND \"VoidReason\" IS NULL) OR (\"VoidedAt\" IS NOT NULL AND \"VoidReason\" IS NOT NULL AND length(trim(\"VoidReason\")) > 0)))");
        Table(model, "inventory.transfers", "FromWarehouseId:g->inventory.warehouses ToWarehouseId:g->inventory.warehouses Reference:s100 BusinessDate:d State:s40 ApprovedBy:s200?", "Reference", "(\"FromWarehouseId\" <> \"ToWarehouseId\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "inventory.transfer_lines", "TransferId:g->inventory.transfers ProductId:g->masterdata.products Quantity:n", "TransferId,ProductId", "\"Quantity\" > 0");
        Table(model, "inventory.adjustments", "WarehouseId:g->inventory.warehouses Reference:s100 BusinessDate:d Reason:s1000 State:s40 ApprovedBy:s200?", "Reference", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "inventory.adjustment_lines", "AdjustmentId:g->inventory.adjustments ProductId:g->masterdata.products Quantity:n", "AdjustmentId,ProductId", "(\"Quantity\" <> 0)");
        Table(model, "manufacturing.bom_headers", "ProductId:g->masterdata.products Code:s100 Name:s200", "Code", "");
        Table(model, "manufacturing.coefficient_versions", "BomId:g->manufacturing.bom_headers Revision:i OutputQuantity:n EffectiveFrom:d EffectiveUntil:d? State:s40 ApprovedBy:s200?", "BomId,Revision", "(\"OutputQuantity\" > 0) AND \"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "manufacturing.bom_lines", "CoefficientVersionId:g->manufacturing.coefficient_versions ProductId:g->masterdata.products Quantity:n WastagePercent:n", "CoefficientVersionId,ProductId", "(\"Quantity\" > 0 AND \"WastagePercent\" BETWEEN 0 AND 100)");
        Table(model, "manufacturing.production_orders", "CoefficientVersionId:g->manufacturing.coefficient_versions WarehouseId:g->inventory.warehouses Reference:s100 BusinessDate:d PlannedQuantity:n State:s40 ApprovedBy:s200?", "Reference", "(\"PlannedQuantity\" > 0) AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "manufacturing.material_issues", "ProductionOrderId:g->manufacturing.production_orders ProductId:g->masterdata.products WarehouseId:g->inventory.warehouses Quantity:n BusinessDate:d SourceReference:s100", "SourceReference", "\"Quantity\" > 0");
        Table(model, "manufacturing.production_receipts", "ProductionOrderId:g->manufacturing.production_orders ProductId:g->masterdata.products WarehouseId:g->inventory.warehouses Quantity:n BusinessDate:d SourceReference:s100", "SourceReference", "\"Quantity\" > 0");
        Table(model, "manufacturing.wastage_records", "ProductionOrderId:g->manufacturing.production_orders ProductId:g->masterdata.products Quantity:n Reason:s1000 ApprovedBy:s200?", "", "(\"Quantity\" > 0) AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
        Table(model, "tax.tax_treatments", "Code:s100 Description:s1000 EffectiveFrom:d EffectiveUntil:d? ApprovedBy:s200?", "Code,EffectiveFrom", "(\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
        Table(model, "tax.tax_calculation_snapshots", "TreatmentId:g->tax.tax_treatments RuleVersionId:g->tax.tax_rule_versions SourceType:s80 SourceId:g SourceVersion:i Calculation:j", "SourceType,SourceId,SourceVersion", "\"SourceVersion\" > 0");
        Table(model, "tax.tax_posting_batches", "SourceType:s80 SourceId:g SourceVersion:i BusinessDate:d State:s40 ExpectedTax:n", "SourceType,SourceId,SourceVersion", "\"SourceVersion\" > 0 AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "tax.vds_entries", "BatchId:g->tax.tax_posting_batches SupplierId:g->masterdata.suppliers BusinessDate:d Amount:n EvidenceReference:s500 ReversalOfId:g?->tax.vds_entries", "", "");
        Table(model, "compliance.form_templates", "Code:s100 Name:s200", "Code", "");
        Table(model, "compliance.form_versions", "TemplateId:g->compliance.form_templates Revision:i EffectiveFrom:d EffectiveUntil:d? Mapping:j ApprovedBy:s200?", "TemplateId,Revision", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
        Table(model, "compliance.return_periods", "FiscalPeriodId:g->company.fiscal_periods Code:s100 From:d Until:d", "Code", "(\"Until\" >= \"From\")");
        Table(model, "compliance.vat_returns", "ReturnPeriodId:g->compliance.return_periods FormVersionId:g->compliance.form_versions Revision:i State:s40 LedgerWatermark:t? SubmittedAt:t? ApprovedBy:s200? CorrectsId:g?->compliance.vat_returns", "ReturnPeriodId,FormVersionId,Revision", "(\"CorrectsId\" IS NULL OR \"CorrectsId\" <> \"Id\") AND \"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "compliance.return_lines", "ReturnId:g->compliance.vat_returns LineCode:s100 Amount:n SourceSnapshot:j", "ReturnId,LineCode", "");
        Table(model, "compliance.reconciliation_runs", "ReturnId:g->compliance.vat_returns StartedAt:t CompletedAt:t? State:s40 Differences:j", "", "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "compliance.filing_evidence", "ReturnId:g->compliance.vat_returns ObjectKey:s1000 Sha256:s64 FilingReference:s200 FiledAt:t", "ReturnId,Sha256", "(length(\"Sha256\") = 64)");
        Table(model, "compliance.period_close_requests", "FiscalPeriodId:g->company.fiscal_periods ReturnId:g?->compliance.vat_returns State:s40 RequestedBy:s200 ApprovedBy:s200? DecisionAt:t?", "", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "workflow.workflow_definitions", "Code:s100 Name:s200", "Code", "");
        Table(model, "workflow.workflow_versions", "DefinitionId:g->workflow.workflow_definitions Revision:i State:s40 Definition:j EffectiveFrom:d EffectiveUntil:d? ApprovedBy:s200?", "DefinitionId,Revision", "\"Revision\" > 0 AND (\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\") AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\") AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "workflow.approval_instances", "WorkflowVersionId:g->workflow.workflow_versions SourceType:s80 SourceId:g SourceVersion:i RequestedBy:s200 State:s40", "SourceType,SourceId,SourceVersion", "\"SourceVersion\" > 0 AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "workflow.approval_tasks", "InstanceId:g->workflow.approval_instances AssignedTo:s200 StepNumber:i State:s40 DueAt:t? DecisionAt:t?", "InstanceId,StepNumber,AssignedTo", "(\"StepNumber\" > 0 AND \"State\" IN ('Pending','Approved','Rejected','Cancelled'))");
        Table(model, "workflow.approval_actions", "TaskId:g->workflow.approval_tasks Actor:s200 Decision:s40 Reason:s1000 OccurredAt:t IdempotencyKey:s100", "TaskId,IdempotencyKey", "(\"Decision\" IN ('Approved','Rejected','Cancelled'))");
        Table(model, "workflow.delegations", "Delegator:s200 Delegate:s200 StartsAt:t ExpiresAt:t RevokedAt:t?", "", "(\"Delegator\" <> \"Delegate\" AND \"ExpiresAt\" > \"StartsAt\")");
        Table(model, "workflow.escalation_rules", "WorkflowVersionId:g->workflow.workflow_versions StepNumber:i AfterMinutes:i EscalateTo:s200", "WorkflowVersionId,StepNumber,AfterMinutes", "(\"StepNumber\" > 0 AND \"AfterMinutes\" > 0)");
        Table(model, "reporting.report_templates", "Code:s100 Revision:i Definition:j ApprovedBy:s200?", "Code,Revision", "\"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
        Table(model, "reporting.report_jobs", "TemplateId:g->reporting.report_templates Parameters:j State:s40 RequestedBy:s200 Watermark:t? StartedAt:t? CompletedAt:t? Error:s2000?", "", "\"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "reporting.report_exports", "JobId:g->reporting.report_jobs ObjectKey:s1000 Sha256:s64 ContentType:s200 SizeBytes:l ExpiresAt:t", "JobId,ObjectKey", "(\"SizeBytes\" > 0 AND length(\"Sha256\") = 64 AND \"ExpiresAt\" > \"CreatedAt\")");
        Table(model, "reporting.reconciliation_alerts", "SourceType:s80 SourceId:g Severity:s40 Message:s2000 ResolvedAt:t? ResolvedBy:s200?", "", "");
        Table(model, "integration.integration_connections", "Code:s100 Provider:s100 Endpoint:s2000 SecretReference:s1000 Enabled:b", "Code", "");
        Table(model, "integration.field_mappings", "ConnectionId:g->integration.integration_connections DocumentType:s100 Revision:i Mapping:j ApprovedBy:s200?", "ConnectionId,DocumentType,Revision", "\"Revision\" > 0 AND (\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"CreatedBy\")");
        Table(model, "integration.inbound_batches", "ConnectionId:g->integration.integration_connections MappingId:g->integration.field_mappings ExternalId:s200 State:s40 PayloadKey:s1000 PayloadDigest:s64 ReceivedAt:t", "ConnectionId,ExternalId", "(length(\"PayloadDigest\") = 64) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "integration.inbound_rows", "BatchId:g->integration.inbound_batches RowNumber:i ExternalId:s200 State:s40 Error:s2000? SourceId:g?", "BatchId,RowNumber", "(\"RowNumber\" > 0) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "integration.external_references", "ConnectionId:g->integration.integration_connections SourceType:s80 ExternalId:s200 SourceId:g", "ConnectionId,SourceType,ExternalId", "");
        Table(model, "integration.webhook_subscriptions", "ConnectionId:g->integration.integration_connections EventType:s200 Endpoint:s2000 SigningSecretReference:s1000 Enabled:b", "ConnectionId,EventType,Endpoint", "");
        Table(model, "integration.delivery_attempts", "SubscriptionId:g->integration.webhook_subscriptions EventId:g AttemptNumber:i State:s40 NextAttemptAt:t? LeaseUntil:t? HttpStatus:i? Error:s2000?", "SubscriptionId,EventId,AttemptNumber", "(\"AttemptNumber\" > 0 AND (\"HttpStatus\" IS NULL OR \"HttpStatus\" BETWEEN 100 AND 599)) AND \"State\" IN ('Draft','Pending','Approved','Rejected','Cancelled','Running','Completed','Failed','Submitted','Posted')");
        Table(model, "integration.dead_letters", "ConnectionId:g->integration.integration_connections BatchId:g?->integration.inbound_batches EventId:g? PayloadKey:s1000 Error:s2000 FailedAt:t ReplayedAt:t?", "", "");
        // Bind a batch's frozen mapping to its own connection, not just its company.
        model.SharedTypeEntity<Dictionary<string, object>>("integration.inbound_batches")
            .HasOne("integration.field_mappings", null).WithMany()
            .HasForeignKey("TenantId", "CompanyId", "ConnectionId", "MappingId")
            .HasPrincipalKey("TenantId", "CompanyId", "ConnectionId", "Id")
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void Table(ModelBuilder model, string name, string columns, string unique, string check)
    {
        var parts = name.Split('.');
        var b = model.SharedTypeEntity<Dictionary<string, object>>(name);
        b.IndexerProperty<Guid>("TenantId");
        b.IndexerProperty<Guid>("CompanyId");
        b.IndexerProperty<Guid>("Id").ValueGeneratedNever();
        b.IndexerProperty<DateTimeOffset>("CreatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.IndexerProperty<DateTimeOffset>("UpdatedAt").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.IndexerProperty<string>("CreatedBy").HasMaxLength(200).IsRequired();
        b.IndexerProperty<int>("Version").HasDefaultValue(1).IsConcurrencyToken();
        b.HasKey("TenantId", "CompanyId", "Id");
        b.ToTable(parts[1], parts[0], t =>
        {
            t.HasCheckConstraint("ck_" + parts[1] + "_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            if (check.Length > 0) t.HasCheckConstraint("ck_" + parts[1] + "_rules", check);
        });
        var references = new List<(string Column, string Target)>();
        foreach (var column in columns.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = column.Split(':');
            var spec = pair[1].Split("->");
            var optional = spec[0].EndsWith('?');
            var code = spec[0].TrimEnd('?');
            Type type = code[0] switch
            {
                'g' => typeof(Guid), 'n' => typeof(decimal), 'd' => typeof(DateOnly),
                't' => typeof(DateTimeOffset), 'i' => typeof(int), 'l' => typeof(long),
                'b' => typeof(bool), _ => typeof(string)
            };
            if (optional && type.IsValueType) type = typeof(Nullable<>).MakeGenericType(type);
            var property = b.IndexerProperty(type, pair[0]).IsRequired(!optional);
            if (code[0] == 's') property.HasMaxLength(int.Parse(code[1..]));
            if (code == "j") property.HasColumnType("jsonb");
            if (code == "n") property.HasPrecision(24, 6);
            if (spec.Length == 2) references.Add((pair[0], spec[1]));
        }
        // All foundations are company-scoped, including the external identity directory.
        b.HasOne(typeof(Vat.Domain.Company.Company)).WithMany()
            .HasForeignKey("TenantId", "CompanyId").HasPrincipalKey("TenantId", "Id")
            .OnDelete(DeleteBehavior.Restrict);
        foreach (var reference in references)
        {
            var target = reference.Target.Split('.');
            var principal = model.Model.GetEntityTypes().FirstOrDefault(x =>
                x.GetSchema() == target[0] && x.GetTableName() == target[1]);
            // Forward declarations create only a named shared type; its full definition follows.
            var principalName = principal?.Name ?? reference.Target;
            if (principal is null) model.SharedTypeEntity<Dictionary<string, object>>(principalName);
            b.HasOne(principalName, null).WithMany()
                .HasForeignKey("TenantId", "CompanyId", reference.Column)
                .HasPrincipalKey("TenantId", "CompanyId", "Id")
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex("TenantId", "CompanyId", reference.Column);
        }
        if (unique.Length > 0)
            b.HasIndex(new[] { "TenantId", "CompanyId" }.Concat(unique.Split(',')).ToArray()).IsUnique()
                .AreNullsDistinct(false);
        b.HasIndex("TenantId", "CompanyId", "CreatedAt");
        if (b.Metadata.FindProperty("BusinessDate") is not null)
            b.HasIndex("TenantId", "CompanyId", "BusinessDate");
        if (b.Metadata.FindProperty("State") is not null)
            b.HasIndex("TenantId", "CompanyId", "State", "CreatedAt");
    }
}
