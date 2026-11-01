using Vat.Application.Security;
namespace Vat.Services.Identity.Controllers;

public static class LocalAccountPolicy
{
    public static readonly string[] Modules = ["MOD_PURCHASES", "MOD_SALES", "MOD_MANUFACTURING", "MOD_INVENTORY", "MOD_COMPLIANCE", "MOD_WORKFLOW", "MOD_MASTERDATA", "MOD_REPORTS", "MOD_ADMIN"];
    public static readonly Dictionary<string, string[]> Roles = new()
    {
        ["SuperAdmin"] = Permissions.All,
        ["Maker"] = [
                Permissions.PurchaseRead, Permissions.PurchaseCreate, Permissions.PurchaseSubmit, Permissions.PurchasePrepare, Permissions.PurchaseCancel,
                Permissions.SalesInvoiceRead, Permissions.SalesInvoiceCreate, Permissions.SalesInvoiceSubmit, Permissions.SalesInvoiceCancel, Permissions.SalesReserve,
                Permissions.SalesDebitRequest, Permissions.SalesCreditRequest,
                Permissions.PaymentRead, Permissions.PaymentRequest, Permissions.PaymentAllocate, Permissions.PaymentReverseRequest, Permissions.PaymentRefundRequest,
                Permissions.InventoryRead, Permissions.InventoryManage,
                Permissions.ManufacturingRead, Permissions.ManufacturingRequest,
                Permissions.MasterDataRead, Permissions.MasterDataManage,
                Permissions.CompanyRead, Permissions.TaxRead, Permissions.PeriodRead, Permissions.ReportRead,
                Permissions.ComplianceRead, Permissions.WorkflowRead, Permissions.IdentityRead
            ],
        ["Checker"] = [
                Permissions.PurchaseRead, Permissions.PurchaseApprove, Permissions.PurchasePost, Permissions.PurchaseCancel, Permissions.PurchaseReverseApprove,
                Permissions.SalesInvoiceRead, Permissions.SalesInvoiceApprove, Permissions.SalesPost, Permissions.SalesInvoiceCancel,
                Permissions.SalesDebitApprove, Permissions.SalesCreditApprove,
                Permissions.PaymentRead, Permissions.PaymentApprove, Permissions.PaymentReverseApprove, Permissions.PaymentRefundApprove,
                Permissions.InventoryRead, Permissions.InventoryApprove, Permissions.InventoryPost,
                Permissions.ManufacturingRead, Permissions.ManufacturingApprove,
                Permissions.PeriodRead, Permissions.PeriodCloseApprove,
                Permissions.ComplianceRead, Permissions.ComplianceManage, Permissions.ComplianceSubmit,
                Permissions.MasterDataRead, Permissions.CompanyRead, Permissions.TaxRead, Permissions.ReportRead, Permissions.WorkflowRead, Permissions.IdentityRead
            ],
        ["Auditor"] = [
                Permissions.CompanyRead, Permissions.PeriodRead, Permissions.MasterDataRead, Permissions.PurchaseRead, Permissions.SalesInvoiceRead,
                Permissions.InventoryRead, Permissions.ManufacturingRead, Permissions.TaxRead, Permissions.ComplianceRead, Permissions.ReportRead,
                Permissions.PaymentRead, Permissions.WorkflowRead, Permissions.IdentityRead, Permissions.IntegrationRead, Permissions.MessagingRead
            ],
        ["TaxManager"] = Permissions.All.Where(p => p.StartsWith("tax.") || p.StartsWith("period.") || p.StartsWith("compliance.") || p == "company.read" || p == "report.read").ToArray(),
        ["Approver"] = Permissions.All.Where(p => p.EndsWith(".read") || p.EndsWith(".approve")).ToArray()
    };
    public static string[] PermissionsFor(string[] roles, string[] modules) => roles.SelectMany(role => Roles[role]).Distinct().Where(permission =>
    {
        var module = permission.Split('.')[0] switch
        {
            "purchase" => "MOD_PURCHASES", "sales" or "payment" => "MOD_SALES",
            "inventory" or "costing" => "MOD_INVENTORY", "manufacturing" => "MOD_MANUFACTURING",
            "tax" or "period" or "compliance" => "MOD_COMPLIANCE", "workflow" => "MOD_WORKFLOW",
            "masterdata" => "MOD_MASTERDATA", "report" => "MOD_REPORTS", "identity" or "integration" or "messaging" => "MOD_ADMIN",
            _ => ""
        };
        var invoiceReferenceRead = (modules.Contains("MOD_PURCHASES") || modules.Contains("MOD_SALES")) &&
            permission is "inventory.read" or "masterdata.read" or "tax.read" or "period.read";
        return invoiceReferenceRead || permission == "company.read" || modules.Contains(module) || (module == "" && modules.Contains("MOD_ADMIN"));
    }).ToArray();
    public static bool Reserved(string username) => new[] { "admin", "imran", "maker", "tanvir", "checker", "farhana", "auditor", "shafiqul" }.Contains(username.Split('@')[0]);
}
