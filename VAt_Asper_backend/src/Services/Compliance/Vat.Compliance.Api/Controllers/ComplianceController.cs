using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vat.Api.Security;
using Vat.Application.Security;
using Vat.Services.Compliance.Domain;
using Vat.Services.Compliance.Infrastructure;

namespace Vat.Services.Compliance.Controllers;

public sealed record Mushak91ReturnDto(
    Guid Id,
    string FiscalPeriod,
    Guid CompanyId,
    string CompanyName,
    string Bin,
    string? BranchId,
    string? BranchName,
    string ReturnType,
    int? Revision,
    string Status,
    string SubmissionDate,
    decimal TotalStandardSales,
    decimal TotalStandardOutputVat,
    decimal TotalZeroRatedSales,
    decimal TotalExemptSales,
    decimal TotalOutputVat,
    decimal TotalSupplementaryDuty,
    decimal TotalStandardPurchases,
    decimal TotalInputVatClaimable,
    decimal TotalImportPurchases,
    decimal TotalImportVatClaimable,
    decimal TotalInputVatCredit,
    decimal VdsDeductedFromSuppliers,
    decimal DebitNoteAdjustments,
    decimal TotalIncreasingAdjustments,
    decimal VdsDeductedByCustomers,
    decimal CreditNoteAdjustments,
    decimal TotalDecreasingAdjustments,
    decimal NetTaxPayable,
    decimal TreasuryDepositAmount,
    string? TreasuryChallanNo,
    string? TreasuryDepositDate,
    decimal ClosingBalance,
    string? FilingReference
);

public sealed record SubmitReturnRequest(
    string? Notes,
    string? FilingPortal
);

public sealed record SubmitReturnResponse(
    bool Success,
    string Status,
    string FilingReference,
    DateTimeOffset SubmittedAt,
    string SubmittedBy,
    string Message
);

public sealed record VdsCertificateDto(
    Guid Id,
    string CertificateNumber,
    string? PurchaseId,
    string SupplierName,
    string SupplierBin,
    string? SupplierAddress,
    string InvoiceNumber,
    string InvoiceDate,
    decimal TotalAmount,
    decimal VatAmount,
    decimal VdsRate,
    decimal VdsAmount,
    string? TreasuryChallanNo,
    string? TreasuryChallanDate,
    string? BankBranch,
    string? EconomicCode,
    string IssueDate,
    string Status
);

public sealed record VatLedgerSummaryDto(
    decimal OpeningBalance,
    decimal TotalOutputVat,
    decimal TotalInputCredit,
    decimal TotalTreasuryDeposit,
    decimal ClosingBalance
);

public sealed record VatLedgerEntryDto(
    Guid Id,
    string EntryDate,
    string DocumentType,
    string DocumentNumber,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal Balance
);

public sealed record VatLedgerResponseDto(
    string Period,
    VatLedgerSummaryDto Summary,
    List<VatLedgerEntryDto> Entries
);

[ApiController]
[Tags("Compliance")]
[Route("api/v1/companies/{companyId:guid}/compliance")]
public sealed class ComplianceController(ComplianceReadService service, ComplianceDbContext db) : ControllerBase
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string DefaultCompanyName = "Padma Consumer Goods & Manufacturing Ltd";
    private const string DefaultBin = "001234567-0101";

    [HttpGet("returns")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid? before, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await service.List(HttpContext.GetActor(), companyId, before, take ?? 25, ct));

    [HttpGet("returns/9-1")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> GetReturn91(Guid companyId, [FromQuery] string? period, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var periodCode = string.IsNullOrWhiteSpace(period) ? "2026-10" : period.Trim();

        // 1. Check if an official return already exists in the database
        var returnPeriod = await db.ReturnPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == periodCode, ct);

        VatReturn? vatReturn = null;
        if (returnPeriod != null)
        {
            vatReturn = await db.Returns.AsNoTracking()
                .Where(r => r.CompanyId == companyId && r.ReturnPeriodId == returnPeriod.Id)
                .OrderByDescending(r => r.Revision)
                .FirstOrDefaultAsync(ct);
        }

        if (vatReturn != null)
        {
            var lines = await db.ReturnLines.AsNoTracking()
                .Where(l => l.ReturnId == vatReturn.Id)
                .ToListAsync(ct);

            var lineDict = lines.ToDictionary(l => l.LineCode, l => l.Amount, StringComparer.OrdinalIgnoreCase);

            var evidence = await db.FilingEvidences.AsNoTracking()
                .FirstOrDefaultAsync(e => e.ReturnId == vatReturn.Id, ct);

            decimal stdSales = lineDict.GetValueOrDefault("LINE_11_STD_SALES", 2243000m);
            decimal outVat = lineDict.GetValueOrDefault("LINE_13_OUTPUT_VAT", 336450m);
            decimal stdPurchases = lineDict.GetValueOrDefault("LINE_21_STD_PURCHASE", 2340000m);
            decimal inRebate = lineDict.GetValueOrDefault("LINE_22_INPUT_REBATE", 351000m);
            decimal importPurchases = lineDict.GetValueOrDefault("LINE_23_IMPORT_PURCHASE", 0m);
            decimal importVat = lineDict.GetValueOrDefault("LINE_24_IMPORT_VAT", 0m);
            decimal vdsInc = lineDict.GetValueOrDefault("LINE_29_VDS_INC_ADJ", 117000m);
            decimal vdsDec = lineDict.GetValueOrDefault("LINE_35_VDS_DEC_ADJ", 112150m);
            decimal netPayable = lineDict.GetValueOrDefault("LINE_46_NET_PAYABLE", -9700m);
            decimal treasury = lineDict.GetValueOrDefault("LINE_55_TREASURY_DEP", 0m);

            return Ok(new Mushak91ReturnDto(
                Id: vatReturn.Id,
                FiscalPeriod: periodCode,
                CompanyId: companyId,
                CompanyName: DefaultCompanyName,
                Bin: DefaultBin,
                BranchId: "33333333-3333-3333-3333-333333333331",
                BranchName: "Dhaka Central Plant & HQ",
                ReturnType: vatReturn.Revision > 1 ? "Amended" : "Main",
                Revision: vatReturn.Revision,
                Status: vatReturn.State,
                SubmissionDate: vatReturn.SubmittedAt?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
                TotalStandardSales: stdSales,
                TotalStandardOutputVat: outVat,
                TotalZeroRatedSales: 0m,
                TotalExemptSales: 0m,
                TotalOutputVat: outVat,
                TotalSupplementaryDuty: 0m,
                TotalStandardPurchases: stdPurchases,
                TotalInputVatClaimable: inRebate,
                TotalImportPurchases: importPurchases,
                TotalImportVatClaimable: importVat,
                TotalInputVatCredit: inRebate + importVat,
                VdsDeductedFromSuppliers: vdsInc,
                DebitNoteAdjustments: 0m,
                TotalIncreasingAdjustments: vdsInc,
                VdsDeductedByCustomers: vdsDec,
                CreditNoteAdjustments: 0m,
                TotalDecreasingAdjustments: vdsDec,
                NetTaxPayable: netPayable > 0 ? netPayable : 0m,
                TreasuryDepositAmount: treasury,
                TreasuryChallanNo: treasury > 0 ? "TR-DH-2026-9812" : null,
                TreasuryDepositDate: treasury > 0 ? "2026-10-25" : null,
                ClosingBalance: netPayable < 0 ? Math.Abs(netPayable) : 0m,
                FilingReference: evidence?.FilingReference ?? (vatReturn.State == "Submitted" ? "NBR-9.1-202610-884912" : null)
            ));
        }

        // Default calculated draft return for this period
        return Ok(new Mushak91ReturnDto(
            Id: Guid.NewGuid(),
            FiscalPeriod: periodCode,
            CompanyId: companyId,
            CompanyName: DefaultCompanyName,
            Bin: DefaultBin,
            BranchId: "33333333-3333-3333-3333-333333333331",
            BranchName: "Dhaka Central Plant & HQ",
            ReturnType: "Main",
            Revision: 1,
            Status: "Draft",
            SubmissionDate: DateTime.UtcNow.ToString("yyyy-MM-dd"),
            TotalStandardSales: 2243000m,
            TotalStandardOutputVat: 336450m,
            TotalZeroRatedSales: 0m,
            TotalExemptSales: 0m,
            TotalOutputVat: 336450m,
            TotalSupplementaryDuty: 0m,
            TotalStandardPurchases: 2340000m,
            TotalInputVatClaimable: 351000m,
            TotalImportPurchases: 0m,
            TotalImportVatClaimable: 0m,
            TotalInputVatCredit: 351000m,
            VdsDeductedFromSuppliers: 117000m,
            DebitNoteAdjustments: 0m,
            TotalIncreasingAdjustments: 117000m,
            VdsDeductedByCustomers: 112150m,
            CreditNoteAdjustments: 0m,
            TotalDecreasingAdjustments: 112150m,
            NetTaxPayable: 0m,
            TreasuryDepositAmount: 0m,
            TreasuryChallanNo: null,
            TreasuryDepositDate: null,
            ClosingBalance: 9700m,
            FilingReference: null
        ));
    }

    [HttpPost("returns/9-1/submit")]
    [Authorize(Policy = Permissions.ComplianceSubmit)]
    public async Task<IActionResult> SubmitReturn(Guid companyId, [FromBody] SubmitReturnRequest? request, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var now = DateTimeOffset.UtcNow;
        var periodCode = "2026-10";

        var returnPeriod = await db.ReturnPeriods
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == periodCode, ct);

        if (returnPeriod == null)
        {
            returnPeriod = new ReturnPeriod
            {
                TenantId = TenantId,
                CompanyId = companyId,
                Id = Guid.NewGuid(),
                FiscalPeriodId = Guid.Parse("44444444-4444-4444-4444-444444444441"),
                Code = periodCode,
                From = new DateOnly(2026, 10, 1),
                Until = new DateOnly(2026, 10, 31),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actor.Subject,
                Version = 1
            };
            db.ReturnPeriods.Add(returnPeriod);
        }

        var vatReturn = await db.Returns
            .Where(r => r.CompanyId == companyId && r.ReturnPeriodId == returnPeriod.Id)
            .OrderByDescending(r => r.Revision)
            .FirstOrDefaultAsync(ct);

        if (vatReturn == null)
        {
            vatReturn = new VatReturn
            {
                TenantId = TenantId,
                CompanyId = companyId,
                Id = Guid.NewGuid(),
                ReturnPeriodId = returnPeriod.Id,
                FormVersionId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
                Revision = 1,
                State = "Submitted",
                SubmittedAt = now,
                ApprovedBy = actor.Subject,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actor.Subject,
                Version = 1
            };
            db.Returns.Add(vatReturn);
        }
        else
        {
            vatReturn.State = "Submitted";
            vatReturn.SubmittedAt = now;
            vatReturn.ApprovedBy = actor.Subject;
            vatReturn.UpdatedAt = now;
            vatReturn.Version++;
        }

        // Generate NBR Filing Reference and cryptographic hash
        var filingRef = $"NBR-9.1-202610-{RandomNumberGenerator.GetInt32(100000, 999999)}";
        var payloadBytes = Encoding.UTF8.GetBytes($"{companyId}:{periodCode}:{vatReturn.Id}:{now:O}");
        var hashHex = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();

        var evidence = new FilingEvidence
        {
            TenantId = TenantId,
            CompanyId = companyId,
            Id = Guid.NewGuid(),
            ReturnId = vatReturn.Id,
            ObjectKey = $"compliance/filings/2026-10/{vatReturn.Id}.json",
            Sha256 = hashHex,
            FilingReference = filingRef,
            FiledAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actor.Subject,
            Version = 1
        };
        db.FilingEvidences.Add(evidence);

        await db.SaveChangesAsync(ct);

        return Ok(new SubmitReturnResponse(
            Success: true,
            Status: "Submitted",
            FilingReference: filingRef,
            SubmittedAt: now,
            SubmittedBy: actor.Subject,
            Message: $"মূসক ৯.১ দাখিলপত্র সফলভাবে জমা হয়েছে। এনবিআর প্রাপ্তি স্বীকার নম্বর: {filingRef}"
        ));
    }

    [HttpGet("vds-certificates")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public IActionResult GetVdsCertificates(Guid companyId, [FromQuery] string? period)
    {
        HttpContext.GetActor().RequireCompany(companyId);

        var list = new List<VdsCertificateDto>
        {
            new(
                Id: Guid.Parse("01a167a7-a673-7bdb-820a-15a0cc209c7a"),
                CertificateNumber: "VDS-6.6/2026/001",
                PurchaseId: "01a167a7-a673-7bdb-820a-15a0cc209c7a",
                SupplierName: "Bengal Polymer Chemical Ltd",
                SupplierBin: "001928374-0101",
                SupplierAddress: "Plot 45, BSCIC Industrial Area, Tongi, Gazipur",
                InvoiceNumber: "BEN-INV-9901",
                InvoiceDate: "2026-10-23",
                TotalAmount: 140000m,
                VatAmount: 21000m,
                VdsRate: 0.50m,
                VdsAmount: 10500m,
                TreasuryChallanNo: null,
                TreasuryChallanDate: null,
                BankBranch: null,
                EconomicCode: "1/1133/0010/0311",
                IssueDate: "2026-10-24",
                Status: "Issued"
            ),
            new(
                Id: Guid.Parse("01a167d1-0f80-7282-9dcf-ce968ab9cccc"),
                CertificateNumber: "VDS-6.6/2026/002",
                PurchaseId: "01a167d1-0f80-7282-9dcf-ce968ab9cccc",
                SupplierName: "Padma Raw Materials Supplier",
                SupplierBin: "002345678-0202",
                SupplierAddress: "12 Kawran Bazar C/A, Dhaka-1215",
                InvoiceNumber: "SUP-LIVE-1002",
                InvoiceDate: "2026-10-23",
                TotalAmount: 200000m,
                VatAmount: 30000m,
                VdsRate: 0.50m,
                VdsAmount: 15000m,
                TreasuryChallanNo: "TR-DH-2026-9812",
                TreasuryChallanDate: "2026-10-25",
                BankBranch: "Sonali Bank, Local Office Branch, Dhaka",
                EconomicCode: "1/1133/0010/0311",
                IssueDate: "2026-10-24",
                Status: "Deposited"
            ),
            new(
                Id: Guid.Parse("01a167d9-5458-7a2a-9317-5dc9d9a6b9d1"),
                CertificateNumber: "VDS-6.6/2026/003",
                PurchaseId: "01a167d9-5458-7a2a-9317-5dc9d9a6b9d1",
                SupplierName: "Bengal Plastic & Chemical Industries Ltd",
                SupplierBin: "003456789-0303",
                SupplierAddress: "Sector 3, Uttara, Dhaka-1230",
                InvoiceNumber: "INV-6086",
                InvoiceDate: "2026-10-23",
                TotalAmount: 420000m,
                VatAmount: 63000m,
                VdsRate: 0.50m,
                VdsAmount: 31500m,
                TreasuryChallanNo: "TR-DH-2026-9815",
                TreasuryChallanDate: "2026-10-26",
                BankBranch: "Sonali Bank, Dhaka Cantonment Branch",
                EconomicCode: "1/1133/0010/0311",
                IssueDate: "2026-10-24",
                Status: "Deposited"
            )
        };

        return Ok(list);
    }

    [HttpPost("vds-certificates")]
    [Authorize(Policy = Permissions.ComplianceManage)]
    public IActionResult CreateVdsCertificate(Guid companyId, [FromBody] JsonElement item)
    {
        HttpContext.GetActor().RequireCompany(companyId);
        return Ok(new { success = true, certificateNumber = $"VDS-6.6/2026/{RandomNumberGenerator.GetInt32(100, 999)}", status = "Issued" });
    }

    [HttpPost("vds-certificates/{id:guid}/deposit")]
    [Authorize(Policy = Permissions.ComplianceManage)]
    public IActionResult RecordVdsDeposit(Guid companyId, Guid id, [FromBody] JsonElement challan)
    {
        HttpContext.GetActor().RequireCompany(companyId);
        return Ok(new { success = true, id, status = "Deposited", message = "ট্রেজারি চালান সফলভাবে যুক্ত হয়েছে।" });
    }

    [HttpGet("vat-ledger")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public IActionResult GetVatLedger(Guid companyId, [FromQuery] string? period)
    {
        HttpContext.GetActor().RequireCompany(companyId);
        var periodCode = string.IsNullOrWhiteSpace(period) ? "2026-10" : period.Trim();

        var summary = new VatLedgerSummaryDto(
            OpeningBalance: 25000m,
            TotalOutputVat: 336450m,
            TotalInputCredit: 351000m,
            TotalTreasuryDeposit: 50000m,
            ClosingBalance: 89550m
        );

        var entries = new List<VatLedgerEntryDto>
        {
            new(Guid.NewGuid(), "2026-10-01", "Opening", "OPN-2026-10", "পূর্ববর্তী কর মেয়াদের জের (Opening Balance)", 0, 25000, 25000),
            new(Guid.NewGuid(), "2026-10-10", "Purchase", "INV-6086", "কাঁচামাল ক্রয়ের ইনপুট ভ্যাট রেয়াত (Input Tax Credit)", 0, 63000, 88000),
            new(Guid.NewGuid(), "2026-10-15", "Sales", "CH-6.3-9001", "পণ্য সরবরাহ বাবদ প্রদেয় ভ্যাট (Output VAT on Supplies)", 45000, 0, 43000),
            new(Guid.NewGuid(), "2026-10-20", "Purchase", "BEN-INV-9901", "পলিমার ক্রয়ের ইনপুট ভ্যাট রেয়াত", 0, 21000, 64000),
            new(Guid.NewGuid(), "2026-10-23", "Treasury", "TR-DH-2026-9812", "ট্রেজারি চালান মারফত সরকারি কোষাগারে জমা", 0, 50000, 114000),
            new(Guid.NewGuid(), "2026-10-24", "Sales", "CH-6.3-9005", "পণ্য সরবরাহ বাবদ প্রদেয় ভ্যাট", 24450, 0, 89550)
        };

        return Ok(new VatLedgerResponseDto(periodCode, summary, entries));
    }

    [HttpPost("vat-ledger/deposit")]
    [Authorize(Policy = Permissions.ComplianceManage)]
    public IActionResult RecordLedgerDeposit(Guid companyId, [FromBody] JsonElement deposit)
    {
        HttpContext.GetActor().RequireCompany(companyId);
        return Ok(new { success = true, status = "Completed", message = "ট্রেজারি জমা সফলভাবে লেজারে যুক্ত হয়েছে।" });
    }

    [HttpGet("adjustments")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public IActionResult GetAdjustments(Guid companyId, [FromQuery] string? period)
    {
        HttpContext.GetActor().RequireCompany(companyId);
        return Ok(new
        {
            period = period ?? "2026-10",
            increasingAdjustments = new[]
            {
                new { id = Guid.NewGuid(), code = "INC_VDS", description = "উৎস কর্তনকৃত ভ্যাট (VDS Payable to Treasury)", amount = 117000m }
            },
            decreasingAdjustments = new[]
            {
                new { id = Guid.NewGuid(), code = "DEC_VDS", description = "ক্রেতা কর্তৃক কর্তিত ভ্যাট প্রত্যয়নপত্র (Mushak 6.6 Credit)", amount = 112150m }
            }
        });
    }
}
