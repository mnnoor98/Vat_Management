using Vat.Domain;

namespace Vat.Application.Security;

public sealed record Actor(Guid TenantId, string Subject, IReadOnlySet<Guid> CompanyIds,
    IReadOnlySet<string> Permissions)
{
    public void RequireCompany(Guid companyId)
    {
        if (!CompanyIds.Contains(companyId))
            throw new DomainException("company_access_denied", "Access to this company is not granted.", 403);
    }
}

public static class Permissions
{
    public const string IdentityRead = "identity.read";
    public const string ManufacturingRead = "manufacturing.read";
    public const string ManufacturingRequest = "manufacturing.request";
    public const string ManufacturingApprove = "manufacturing.approve";
    public const string ComplianceRead = "compliance.read";
    public const string WorkflowRead = "workflow.read";
    public const string IntegrationRead = "integration.read";

    public const string PaymentReverseRequest = "payment.reverse.request";
    public const string PaymentReverseApprove = "payment.reverse.approve";
    public const string PaymentRefundRequest = "payment.refund.request";
    public const string PaymentRefundApprove = "payment.refund.approve";
    public const string PaymentRead = "payment.read";
    public const string PaymentRequest = "payment.request";
    public const string PaymentApprove = "payment.approve";
    public const string PaymentAllocate = "payment.allocate";
    public const string CompanyRead = "company.read";
    public const string CompanyManage = "company.manage";
    public const string PurchaseRead = "purchase.read";
    public const string PurchaseCreate = "purchase.create";
    public const string PurchaseSubmit = "purchase.submit";
    public const string PurchaseApprove = "purchase.approve";
    public const string MasterDataManage = "masterdata.manage";
    public const string MasterDataRead = "masterdata.read";
    public const string InventoryManage = "inventory.manage";
    public const string InventoryRead = "inventory.read";
    public const string InventoryApprove = "inventory.approve";
    public const string InventoryPost = "inventory.post";
    public const string TaxRuleManage = "tax.rule.manage";
    public const string TaxRulePublish = "tax.rule.publish";
    public const string TaxRead = "tax.read";
    public const string PurchasePrepare = "purchase.prepare";
    public const string PurchasePost = "purchase.post";
    public const string PeriodRead = "period.read";
    public const string PeriodManage = "period.manage";
    public const string PeriodCloseRequest = "period.close.request";
    public const string PeriodCloseApprove = "period.close.approve";
    public const string PurchaseReverseRequest = "purchase.reverse.request";
    public const string PurchaseReverseApprove = "purchase.reverse.approve";
    public const string PurchaseCancel = "purchase.cancel";
    public const string ReportRead = "report.read";
    public const string ReportManage = "report.manage";
    public const string MessagingRead = "messaging.read";
    public const string MessagingRetry = "messaging.retry";
    public const string SalesDebitRequest = "sales.debit.request";
    public const string SalesDebitApprove = "sales.debit.approve";
    public const string SalesCreditRequest = "sales.credit.request";
    public const string SalesCreditApprove = "sales.credit.approve";
    public const string SalesReserve = "sales.reserve";
    public const string SalesPost = "sales.post";
    public const string SalesInvoiceRead = "sales.read";
    public const string SalesInvoiceCreate = "sales.create";
    public const string SalesInvoiceSubmit = "sales.submit";
    public const string SalesInvoiceApprove = "sales.approve";
    public const string SalesInvoiceCancel = "sales.cancel";
    public static readonly string[] All = [IdentityRead, ManufacturingRead, ManufacturingRequest, ManufacturingApprove, ComplianceRead, WorkflowRead, IntegrationRead, PaymentReverseRequest, PaymentReverseApprove, PaymentRefundRequest, PaymentRefundApprove, PaymentRead, PaymentRequest, PaymentApprove, PaymentAllocate, SalesDebitRequest, SalesDebitApprove, SalesCreditRequest, SalesCreditApprove, SalesReserve, SalesPost, SalesInvoiceRead, SalesInvoiceCreate, SalesInvoiceSubmit, SalesInvoiceApprove, SalesInvoiceCancel, CompanyRead, CompanyManage, PurchaseRead, PurchaseCreate, PurchaseSubmit, PurchaseApprove,
        MasterDataManage, MasterDataRead, InventoryManage, InventoryRead, InventoryApprove, InventoryPost, TaxRuleManage, TaxRulePublish, TaxRead, PurchasePrepare, PurchasePost,
        PeriodRead, PeriodManage, PeriodCloseRequest, PeriodCloseApprove, PurchaseReverseRequest, PurchaseReverseApprove, PurchaseCancel,
        ReportRead, ReportManage, MessagingRead, MessagingRetry];
}
