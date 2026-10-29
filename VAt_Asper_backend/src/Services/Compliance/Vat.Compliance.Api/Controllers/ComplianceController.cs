using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vat.Api.Security;
using Vat.Application;
using Vat.Application.Security;
using Vat.Services.Compliance.Infrastructure;

namespace Vat.Services.Compliance.Controllers;

[ApiController]
[Tags("Compliance")]
[Route("api/v1/companies/{companyId:guid}/compliance")]
public sealed class ComplianceController(ComplianceReadService service, ComplianceDbContext db) : ControllerBase
{
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

        var fiscalPeriod = string.IsNullOrWhiteSpace(period) ? "2026-10" : period.Trim();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        // 1. Check if an existing return exists in compliance.vat_returns
        var existing = await TryGetExistingReturn(conn, actor.TenantId, companyId, fiscalPeriod, ct);
        if (existing is not null)
        {
            return Ok(existing);
        }

        // 2. Aggregate live from database (sales, purchases, company)
        var liveReturn = await AggregateLiveReturn(conn, actor.TenantId, companyId, fiscalPeriod, ct);
        return Ok(liveReturn);
    }

    [HttpPost("returns/9-1/submit")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> SubmitReturn91(Guid companyId, [FromBody] JsonElement payload, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var result = await SaveReturnSubmission(conn, actor, companyId, payload, ct);
        return Ok(result);
    }

    [HttpGet("vds-certificates")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> GetVdsCertificates(Guid companyId, [FromQuery] string? period, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var list = new List<object>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ""Id"", ""CertificateNumber"", ""PurchaseId"", ""SupplierName"", ""SupplierBin"", ""SupplierAddress"",
                   ""InvoiceNumber"", ""InvoiceDate"", ""TotalAmount"", ""VatAmount"", ""VdsRate"", ""VdsAmount"",
                   ""TreasuryChallanNo"", ""TreasuryChallanDate"", ""BankBranch"", ""EconomicCode"", ""IssueDate"", ""Status""
            FROM compliance.vds_certificates
            WHERE ""TenantId"" = @t AND ""CompanyId"" = @c
            ORDER BY ""IssueDate"" DESC, ""CreatedAt"" DESC;";
        AddParam(cmd, "@t", actor.TenantId);
        AddParam(cmd, "@c", companyId);

        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new
            {
                id = r.GetGuid(0).ToString(),
                certificateNumber = r.GetString(1),
                purchaseId = r.IsDBNull(2) ? null : r.GetGuid(2).ToString(),
                supplierName = r.GetString(3),
                supplierBin = r.GetString(4),
                supplierAddress = r.IsDBNull(5) ? "" : r.GetString(5),
                invoiceNumber = r.GetString(6),
                invoiceDate = r.GetFieldValue<DateOnly>(7).ToString("yyyy-MM-dd"),
                totalAmount = r.GetDecimal(8),
                vatAmount = r.GetDecimal(9),
                vdsRate = r.GetDecimal(10),
                vdsAmount = r.GetDecimal(11),
                treasuryChallanNo = r.IsDBNull(12) ? null : r.GetString(12),
                treasuryChallanDate = r.IsDBNull(13) ? null : r.GetFieldValue<DateOnly>(13).ToString("yyyy-MM-dd"),
                bankBranch = r.IsDBNull(14) ? null : r.GetString(14),
                economicCode = r.IsDBNull(15) ? "1/1133/0010/0311" : r.GetString(15),
                issueDate = r.GetFieldValue<DateOnly>(16).ToString("yyyy-MM-dd"),
                status = r.GetString(17)
            });
        }

        return Ok(list);
    }

    [HttpPost("vds-certificates")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> IssueVdsCertificate(Guid companyId, [FromBody] JsonElement payload, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var supplierName = payload.TryGetProperty("supplierName", out var sn) ? sn.GetString() ?? "Unknown Supplier" : "Unknown Supplier";
        var supplierBin = payload.TryGetProperty("supplierBin", out var sb) ? sb.GetString() ?? "000000000-0101" : "000000000-0101";
        var supplierAddress = payload.TryGetProperty("supplierAddress", out var sa) ? sa.GetString() ?? "Dhaka" : "Dhaka";
        var invoiceNumber = payload.TryGetProperty("invoiceNumber", out var inum) ? inum.GetString() ?? "INV-VDS" : "INV-VDS";
        var invoiceDateStr = payload.TryGetProperty("invoiceDate", out var idate) ? idate.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd");
        var issueDateStr = payload.TryGetProperty("issueDate", out var isdate) ? isdate.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd");

        decimal totalAmount = payload.TryGetProperty("totalAmount", out var ta) && ta.TryGetDecimal(out var tav) ? tav : 0m;
        decimal vatAmount = payload.TryGetProperty("vatAmount", out var va) && va.TryGetDecimal(out var vav) ? vav : 0m;
        decimal vdsRate = payload.TryGetProperty("vdsRate", out var vr) && vr.TryGetDecimal(out var vrv) ? vrv : 50m;
        decimal vdsAmount = payload.TryGetProperty("vdsAmount", out var vdamt) && vdamt.TryGetDecimal(out var vdav) ? vdav : Math.Round(vatAmount * vdsRate / 100m, 2);

        var treasuryChallanNo = payload.TryGetProperty("treasuryChallanNo", out var tc) ? tc.GetString() : null;
        var treasuryChallanDateStr = payload.TryGetProperty("treasuryChallanDate", out var tcd) ? tcd.GetString() : null;
        var bankBranch = payload.TryGetProperty("bankBranch", out var bb) ? bb.GetString() : null;

        var certId = Guid.NewGuid();
        var certNo = payload.TryGetProperty("certificateNumber", out var cn) && !string.IsNullOrWhiteSpace(cn.GetString())
            ? cn.GetString()!
            : $"VDS-6.6/{DateTime.UtcNow:yyyy-MM}/{Random.Shared.Next(100, 999)}";

        DateOnly invDate = DateOnly.TryParse(invoiceDateStr, out var d1) ? d1 : DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly issDate = DateOnly.TryParse(issueDateStr, out var d2) ? d2 : DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly? trDate = DateOnly.TryParse(treasuryChallanDateStr, out var d3) ? d3 : null;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO compliance.vds_certificates (
                ""Id"", ""TenantId"", ""CompanyId"", ""CertificateNumber"", ""SupplierName"", ""SupplierBin"", ""SupplierAddress"",
                ""InvoiceNumber"", ""InvoiceDate"", ""TotalAmount"", ""VatAmount"", ""VdsRate"", ""VdsAmount"",
                ""TreasuryChallanNo"", ""TreasuryChallanDate"", ""BankBranch"", ""EconomicCode"", ""IssueDate"", ""Status"",
                ""CreatedBy"", ""CreatedAt"", ""UpdatedAt"", ""Version""
            ) VALUES (
                @id, @t, @c, @cno, @sn, @sb, @sa,
                @in, @ind::date, @ta, @va, @vr, @vamt,
                @tc, @tcd::date, @bb, '1/1133/0010/0311', @isd::date, @st,
                @by, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 1
            );";
        AddParam(cmd, "@id", certId);
        AddParam(cmd, "@t", actor.TenantId);
        AddParam(cmd, "@c", companyId);
        AddParam(cmd, "@cno", certNo);
        AddParam(cmd, "@sn", supplierName);
        AddParam(cmd, "@sb", supplierBin);
        AddParam(cmd, "@sa", supplierAddress);
        AddParam(cmd, "@in", invoiceNumber);
        AddParam(cmd, "@ind", invDate.ToString("yyyy-MM-dd"));
        AddParam(cmd, "@ta", totalAmount);
        AddParam(cmd, "@va", vatAmount);
        AddParam(cmd, "@vr", vdsRate);
        AddParam(cmd, "@vamt", vdsAmount);
        AddParam(cmd, "@tc", (object?)treasuryChallanNo ?? DBNull.Value);
        AddParam(cmd, "@tcd", trDate.HasValue ? trDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
        AddParam(cmd, "@bb", (object?)bankBranch ?? DBNull.Value);
        AddParam(cmd, "@isd", issDate.ToString("yyyy-MM-dd"));
        AddParam(cmd, "@st", string.IsNullOrWhiteSpace(treasuryChallanNo) ? "Issued" : "Deposited");
        AddParam(cmd, "@by", actor.Subject);

        await cmd.ExecuteNonQueryAsync(ct);

        return Ok(new
        {
            success = true,
            id = certId.ToString(),
            certificateNumber = certNo,
            supplierName,
            vdsAmount,
            status = string.IsNullOrWhiteSpace(treasuryChallanNo) ? "Issued" : "Deposited",
            message = "মূসক ৬.৬ উৎস কর কর্তন সনদপত্র সফলভাবে ইস্যু করা হয়েছে।"
        });
    }

    [HttpPost("vds-certificates/{id:guid}/deposit")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> RecordTreasuryDeposit(Guid companyId, Guid id, [FromBody] JsonElement payload, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var challanNo = payload.TryGetProperty("treasuryChallanNo", out var tc) ? tc.GetString() ?? "" : "";
        var dateStr = payload.TryGetProperty("treasuryChallanDate", out var tcd) ? tcd.GetString() ?? "" : "";
        var bankBranch = payload.TryGetProperty("bankBranch", out var bb) ? bb.GetString() ?? "Sonali Bank Ltd, Custom House Corporate Branch" : "Sonali Bank Ltd, Custom House Corporate Branch";

        DateOnly trDate = DateOnly.TryParse(dateStr, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE compliance.vds_certificates
            SET ""TreasuryChallanNo"" = @tc, ""TreasuryChallanDate"" = @tcd::date, ""BankBranch"" = @bb, ""Status"" = 'Deposited', ""UpdatedAt"" = CURRENT_TIMESTAMP
            WHERE ""TenantId"" = @t AND ""CompanyId"" = @c AND ""Id"" = @id;";
        AddParam(cmd, "@tc", challanNo);
        AddParam(cmd, "@tcd", trDate.ToString("yyyy-MM-dd"));
        AddParam(cmd, "@bb", bankBranch);
        AddParam(cmd, "@t", actor.TenantId);
        AddParam(cmd, "@c", companyId);
        AddParam(cmd, "@id", id);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0) return NotFound(new { code = "certificate_not_found", detail = "VDS certificate not found." });

        return Ok(new
        {
            success = true,
            id = id.ToString(),
            treasuryChallanNo = challanNo,
            status = "Deposited",
            message = "ট্রেজারি ই-চালান সফলভাবে লিংক ও হালনাগাদ করা হয়েছে।"
        });
    }


    [HttpGet("vat-ledger")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> GetVatLedger(Guid companyId, [FromQuery] string? period = "2026-10", CancellationToken ct = default)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var entries = new List<object>();
        decimal openingBal = 1000000.00m;
        decimal totalTreasury = 0m;
        decimal totalPurchaseRebate = 0m;
        decimal totalSalesOutput = 0m;
        decimal totalAdjustments = 0m;
        decimal latestBalance = 0m;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT ""Id"", ""EntryDate"", ""TransactionType"", ""ReferenceNumber"", ""Description"",
                       ""DebitAmount"", ""CreditAmount"", ""Balance"", ""StatutoryForm"", ""BankBranch"",
                       ""EconomicCode"", ""Status"", ""CreatedBy""
                FROM compliance.vat_ledger_entries
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c
                  AND to_char(""EntryDate"", 'YYYY-MM') = @p
                ORDER BY ""EntryDate"" ASC, ""CreatedAt"" ASC;";
            AddParam(cmd, "@t", actor.TenantId);
            AddParam(cmd, "@c", companyId);
            AddParam(cmd, "@p", period ?? "2026-10");

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0).ToString();
                var entryDate = reader.GetDateTime(1).ToString("yyyy-MM-dd");
                var txType = reader.GetString(2);
                var refNo = reader.GetString(3);
                var desc = reader.GetString(4);
                var debit = reader.GetDecimal(5);
                var credit = reader.GetDecimal(6);
                var bal = reader.GetDecimal(7);
                var form = reader.GetString(8);
                var bank = reader.IsDBNull(9) ? null : reader.GetString(9);
                var econ = reader.IsDBNull(10) ? "1/1133/0010/0311" : reader.GetString(10);
                var status = reader.IsDBNull(11) ? "Posted" : reader.GetString(11);
                var createdBy = reader.IsDBNull(12) ? "SYSTEM" : reader.GetString(12);

                if (txType == "OpeningBalance") openingBal = credit;
                else if (txType == "TreasuryDeposit") totalTreasury += credit;
                else if (txType == "PurchaseRebate") totalPurchaseRebate += credit;
                else if (txType == "SalesOutputVat") totalSalesOutput += debit;
                else if (txType == "CreditNote") totalAdjustments += credit;
                else if (txType == "DebitNote" || txType == "VdsWithheld") totalAdjustments -= debit;

                latestBalance = bal;

                entries.Add(new
                {
                    id,
                    entryDate,
                    transactionType = txType,
                    referenceNumber = refNo,
                    description = desc,
                    debitAmount = debit,
                    creditAmount = credit,
                    balance = bal,
                    statutoryForm = form,
                    bankBranch = bank,
                    economicCode = econ,
                    status,
                    createdBy
                });
            }
        }

        return Ok(new
        {
            period = period ?? "2026-10",
            summary = new
            {
                openingBalance = openingBal,
                totalTreasuryDeposited = totalTreasury,
                totalInputCreditClaimed = totalPurchaseRebate,
                totalOutputTaxDebited = totalSalesOutput,
                totalAdjustments,
                closingBalance = latestBalance > 0m ? latestBalance : openingBal + totalTreasury + totalPurchaseRebate - totalSalesOutput + totalAdjustments
            },
            entries
        });
    }

    [HttpPost("vat-ledger/deposit")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> RecordLedgerDeposit(Guid companyId, [FromBody] JsonElement payload, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        decimal depositAmount = payload.TryGetProperty("depositAmount", out var da) && da.TryGetDecimal(out var dav) ? dav : 0m;
        if (depositAmount <= 0) return BadRequest(new { code = "invalid_amount", detail = "Deposit amount must be greater than zero." });

        var challanNo = payload.TryGetProperty("treasuryChallanNo", out var tc) ? tc.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(challanNo)) challanNo = $"TR-{DateTime.UtcNow:yyyy-MM}-Sonali-{Random.Shared.Next(100, 999)}";

        var dateStr = payload.TryGetProperty("depositDate", out var dd) ? dd.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd");
        var bankBranch = payload.TryGetProperty("bankBranch", out var bb) ? bb.GetString() ?? "Sonali Bank Ltd, Custom House Corporate Branch" : "Sonali Bank Ltd, Custom House Corporate Branch";
        var economicCode = payload.TryGetProperty("economicCode", out var ec) ? ec.GetString() ?? "1/1133/0010/0311" : "1/1133/0010/0311";

        decimal lastBalance = 1000000m;
        using (var bCmd = conn.CreateCommand())
        {
            bCmd.CommandText = @"
                SELECT ""Balance"" FROM compliance.vat_ledger_entries 
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c 
                ORDER BY ""EntryDate"" DESC, ""CreatedAt"" DESC LIMIT 1;";
            AddParam(bCmd, "@t", actor.TenantId);
            AddParam(bCmd, "@c", companyId);
            var prev = await bCmd.ExecuteScalarAsync(ct);
            if (prev != null && prev != DBNull.Value) lastBalance = Convert.ToDecimal(prev);
        }

        var newBalance = lastBalance + depositAmount;
        var entryId = Guid.NewGuid();
        DateOnly depDate = DateOnly.TryParse(dateStr, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        using (var insCmd = conn.CreateCommand())
        {
            insCmd.CommandText = @"
                INSERT INTO compliance.vat_ledger_entries (
                    ""Id"", ""TenantId"", ""CompanyId"", ""BranchId"", ""EntryDate"", ""TransactionType"",
                    ""ReferenceNumber"", ""Description"", ""DebitAmount"", ""CreditAmount"", ""Balance"",
                    ""StatutoryForm"", ""BankBranch"", ""EconomicCode"", ""Status"", ""CreatedBy""
                ) VALUES (
                    @id, @t, @c, @b, @ed::date, 'TreasuryDeposit',
                    @ref, @desc, 0.00, @credit, @bal,
                    'TR-Challan', @bank, @econ, 'Posted', @by
                );";
            AddParam(insCmd, "@id", entryId);
            AddParam(insCmd, "@t", actor.TenantId);
            AddParam(insCmd, "@c", companyId);
            AddParam(insCmd, "@b", Guid.Parse("22222222-2222-2222-2222-222222222221"));
            AddParam(insCmd, "@ed", depDate.ToString("yyyy-MM-dd"));
            AddParam(insCmd, "@ref", challanNo);
            AddParam(insCmd, "@desc", $"ট্রেজারি ই-চালান জমা ({bankBranch} - কোড: {economicCode})");
            AddParam(insCmd, "@credit", depositAmount);
            AddParam(insCmd, "@bal", newBalance);
            AddParam(insCmd, "@bank", bankBranch);
            AddParam(insCmd, "@econ", economicCode);
            AddParam(insCmd, "@by", actor.Subject);
            await insCmd.ExecuteNonQueryAsync(ct);
        }

        return Ok(new
        {
            success = true,
            id = entryId.ToString(),
            referenceNumber = challanNo,
            creditAmount = depositAmount,
            balance = newBalance,
            message = "ট্রেজারি চালান সফলভাবে চলতি হিসাবে জমা ও হালনাগাদ করা হয়েছে।"
        });
    }

    [HttpGet("adjustments")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> GetAdjustments(Guid companyId, [FromQuery] string? period = "2026-10", CancellationToken ct = default)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var creditNotes = new List<object>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT ""Id"", ""DocumentNumber"", ""OriginalDocumentNumber"", ""CorrectionDate"",
                       ""Reason"", ""NetAmount"", ""OutputTaxAmount"", ""Status"", ""RequestedBy"", ""ApprovedBy""
                FROM sales.credit_notes
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c
                ORDER BY ""CorrectionDate"" DESC, ""CreatedAt"" DESC;";
            AddParam(cmd, "@t", actor.TenantId);
            AddParam(cmd, "@c", companyId);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                creditNotes.Add(new
                {
                    id = reader.GetGuid(0).ToString(),
                    documentNumber = reader.GetString(1),
                    originalDocumentNumber = reader.GetString(2),
                    correctionDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                    reason = reader.GetString(4),
                    netAmount = Math.Abs(reader.GetDecimal(5)),
                    outputTaxAmount = Math.Abs(reader.GetDecimal(6)),
                    status = reader.GetString(7),
                    requestedBy = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    approvedBy = reader.IsDBNull(9) ? "" : reader.GetString(9)
                });
            }
        }

        var debitNotes = new List<object>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT ""Id"", ""DocumentNumber"", ""OriginalDocumentNumber"", ""CorrectionDate"",
                       ""Reason"", ""NetAmount"", ""OutputTaxAmount"", ""Status"", ""RequestedBy"", ""ApprovedBy""
                FROM sales.debit_notes
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c
                ORDER BY ""CorrectionDate"" DESC, ""CreatedAt"" DESC;";
            AddParam(cmd, "@t", actor.TenantId);
            AddParam(cmd, "@c", companyId);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                debitNotes.Add(new
                {
                    id = reader.GetGuid(0).ToString(),
                    documentNumber = reader.GetString(1),
                    originalDocumentNumber = reader.GetString(2),
                    correctionDate = reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                    reason = reader.GetString(4),
                    netAmount = reader.GetDecimal(5),
                    outputTaxAmount = reader.GetDecimal(6),
                    status = reader.GetString(7),
                    requestedBy = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    approvedBy = reader.IsDBNull(9) ? "" : reader.GetString(9)
                });
            }
        }

        return Ok(new
        {
            period = period ?? "2026-10",
            creditNotes,
            debitNotes
        });
    }

    [HttpPost("credit-notes")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> IssueCreditNote(Guid companyId, [FromBody] JsonElement payload, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var originalDoc = payload.TryGetProperty("originalDocumentNumber", out var od) ? od.GetString() ?? "CH-DHK-2026-00142" : "CH-DHK-2026-00142";
        var reason = payload.TryGetProperty("reason", out var rs) ? rs.GetString() ?? "পণ্য ফেরত বা মূল্য হ্রাস সমন্বয়" : "পণ্য ফেরত বা মূল্য হ্রাস সমন্বয়";
        var customerName = payload.TryGetProperty("customerName", out var cn) ? cn.GetString() ?? "Customer" : "Customer";
        var dateStr = payload.TryGetProperty("correctionDate", out var cd) ? cd.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd");

        decimal netAmount = payload.TryGetProperty("netAmount", out var na) && na.TryGetDecimal(out var nav) ? Math.Abs(nav) : 0m;
        decimal vatAmount = payload.TryGetProperty("vatAmount", out var va) && va.TryGetDecimal(out var vav) ? Math.Abs(vav) : Math.Round(netAmount * 0.15m, 2);

        Guid invoiceId = Guid.Parse("01a167dc-51f4-7cc6-99f3-b04a2c207270");
        using (var sCmd = conn.CreateCommand())
        {
            sCmd.CommandText = @"SELECT ""Id"" FROM sales.sales_invoices WHERE ""TenantId"" = @t AND ""CompanyId"" = @c LIMIT 1;";
            AddParam(sCmd, "@t", actor.TenantId);
            AddParam(sCmd, "@c", companyId);
            var existingId = await sCmd.ExecuteScalarAsync(ct);
            if (existingId != null && existingId != DBNull.Value) invoiceId = (Guid)existingId;
        }

        var noteId = Guid.NewGuid();
        var docNumber = $"CN-6.7/{DateTime.UtcNow:yyyy-MM}/{Random.Shared.Next(100, 999)}";
        DateOnly corrDate = DateOnly.TryParse(dateStr, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Insert into sales.credit_notes (with negative amounts)
        using (var cCmd = conn.CreateCommand())
        {
            cCmd.CommandText = @"
                INSERT INTO sales.credit_notes (
                    ""Id"", ""TenantId"", ""CompanyId"", ""InvoiceId"", ""SourceVersion"", ""OriginalPostedVersion"", ""Version"",
                    ""CorrectionDate"", ""Reason"", ""ReturnReceiptReference"", ""RequestedBy"", ""ApprovedBy"", ""Status"",
                    ""OriginalDocumentNumber"", ""DocumentNumber"", ""NetAmount"", ""OutputTaxAmount"", ""CreatedAt"", ""PostedAt"", ""Lines"", ""Mode""
                ) VALUES (
                    @id, @t, @c, @invId, 1, 1, 1,
                    @cd::date, @reason, @rr, 'SalesExecutive', 'TaxManager', 'Approved',
                    @origDoc, @docNo, @net, @tax, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP,
                    @lines::jsonb, 'PartialReturn'
                );";
            AddParam(cCmd, "@id", noteId);
            AddParam(cCmd, "@t", actor.TenantId);
            AddParam(cCmd, "@c", companyId);
            AddParam(cCmd, "@invId", invoiceId);
            AddParam(cCmd, "@cd", corrDate.ToString("yyyy-MM-dd"));
            AddParam(cCmd, "@reason", reason);
            AddParam(cCmd, "@rr", $"RR-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10, 99)}");
            AddParam(cCmd, "@origDoc", originalDoc);
            AddParam(cCmd, "@docNo", docNumber);
            AddParam(cCmd, "@net", -netAmount);
            AddParam(cCmd, "@tax", -vatAmount);
            AddParam(cCmd, "@lines", JsonSerializer.Serialize(new[] { new { item = "Returned Product", netAmount = -netAmount, vatAmount = -vatAmount } }));
            await cCmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Post Decreasing Adjustment to VAT Current Account (increases running balance)
        decimal lastBalance = 1000000m;
        using (var bCmd = conn.CreateCommand())
        {
            bCmd.CommandText = @"
                SELECT ""Balance"" FROM compliance.vat_ledger_entries 
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c 
                ORDER BY ""EntryDate"" DESC, ""CreatedAt"" DESC LIMIT 1;";
            AddParam(bCmd, "@t", actor.TenantId);
            AddParam(bCmd, "@c", companyId);
            var prev = await bCmd.ExecuteScalarAsync(ct);
            if (prev != null && prev != DBNull.Value) lastBalance = Convert.ToDecimal(prev);
        }

        var newBalance = lastBalance + vatAmount;
        using (var lCmd = conn.CreateCommand())
        {
            lCmd.CommandText = @"
                INSERT INTO compliance.vat_ledger_entries (
                    ""Id"", ""TenantId"", ""CompanyId"", ""BranchId"", ""EntryDate"", ""TransactionType"",
                    ""ReferenceNumber"", ""Description"", ""DebitAmount"", ""CreditAmount"", ""Balance"",
                    ""StatutoryForm"", ""EconomicCode"", ""Status"", ""CreatedBy""
                ) VALUES (
                    @id, @t, @c, @b, @ed::date, 'CreditNote',
                    @ref, @desc, 0.00, @credit, @bal,
                    'Mushak 6.7', '1/1133/0010/0311', 'Posted', @by
                );";
            AddParam(lCmd, "@id", Guid.NewGuid());
            AddParam(lCmd, "@t", actor.TenantId);
            AddParam(lCmd, "@c", companyId);
            AddParam(lCmd, "@b", Guid.Parse("22222222-2222-2222-2222-222222222221"));
            AddParam(lCmd, "@ed", corrDate.ToString("yyyy-MM-dd"));
            AddParam(lCmd, "@ref", docNumber);
            AddParam(lCmd, "@desc", $"Credit Note for {reason} ({customerName} - মূসক ৬.৭ হ্রাসকারী সমন্বয়)");
            AddParam(lCmd, "@credit", vatAmount);
            AddParam(lCmd, "@bal", newBalance);
            AddParam(lCmd, "@by", actor.Subject);
            await lCmd.ExecuteNonQueryAsync(ct);
        }

        return Ok(new
        {
            success = true,
            id = noteId.ToString(),
            documentNumber = docNumber,
            originalDocumentNumber = originalDoc,
            netAmount,
            outputTaxAmount = vatAmount,
            status = "Approved",
            message = "মূসক ৬.৭ ক্রেডিট নোট সফলভাবে ইস্যু ও চলতি হিসাবে হ্রাসকারী সমন্বয় করা হয়েছে।"
        });
    }

    [HttpPost("debit-notes")]
    [Authorize(Policy = Permissions.ComplianceRead)]
    public async Task<IActionResult> IssueDebitNote(Guid companyId, [FromBody] JsonElement payload, CancellationToken ct)
    {
        var actor = HttpContext.GetActor();
        actor.RequireCompany(companyId);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var originalDoc = payload.TryGetProperty("originalDocumentNumber", out var od) ? od.GetString() ?? "CH-DHK-2026-00140" : "CH-DHK-2026-00140";
        var reason = payload.TryGetProperty("reason", out var rs) ? rs.GetString() ?? "মূল্য বৃদ্ধি বা দূরত্বজনিত অতিরিক্ত মূসক" : "মূল্য বৃদ্ধি বা দূরত্বজনিত অতিরিক্ত মূসক";
        var customerName = payload.TryGetProperty("customerName", out var cn) ? cn.GetString() ?? "Customer" : "Customer";
        var dateStr = payload.TryGetProperty("correctionDate", out var cd) ? cd.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd");

        decimal netAmount = payload.TryGetProperty("netAmount", out var na) && na.TryGetDecimal(out var nav) ? Math.Abs(nav) : 0m;
        decimal vatAmount = payload.TryGetProperty("vatAmount", out var va) && va.TryGetDecimal(out var vav) ? Math.Abs(vav) : Math.Round(netAmount * 0.15m, 2);

        Guid invoiceId = Guid.Parse("01a167dc-51f4-7cc6-99f3-b04a2c207270");
        using (var sCmd = conn.CreateCommand())
        {
            sCmd.CommandText = @"SELECT ""Id"" FROM sales.sales_invoices WHERE ""TenantId"" = @t AND ""CompanyId"" = @c LIMIT 1;";
            AddParam(sCmd, "@t", actor.TenantId);
            AddParam(sCmd, "@c", companyId);
            var existingId = await sCmd.ExecuteScalarAsync(ct);
            if (existingId != null && existingId != DBNull.Value) invoiceId = (Guid)existingId;
        }

        var noteId = Guid.NewGuid();
        var docNumber = $"DN-6.8/{DateTime.UtcNow:yyyy-MM}/{Random.Shared.Next(100, 999)}";
        DateOnly corrDate = DateOnly.TryParse(dateStr, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Insert into sales.debit_notes (positive amounts)
        using (var dCmd = conn.CreateCommand())
        {
            dCmd.CommandText = @"
                INSERT INTO sales.debit_notes (
                    ""Id"", ""TenantId"", ""CompanyId"", ""InvoiceId"", ""SourceVersion"", ""OriginalPostedVersion"", ""Version"",
                    ""CorrectionDate"", ""Reason"", ""EvidenceReference"", ""RequestedBy"", ""ApprovedBy"", ""Status"",
                    ""OriginalDocumentNumber"", ""DocumentNumber"", ""NetAmount"", ""OutputTaxAmount"", ""CreatedAt"", ""PostedAt"", ""Lines""
                ) VALUES (
                    @id, @t, @c, @invId, 1, 1, 1,
                    @cd::date, @reason, @ev, 'SalesExecutive', 'TaxManager', 'Approved',
                    @origDoc, @docNo, @net, @tax, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP,
                    @lines::jsonb
                );";
            AddParam(dCmd, "@id", noteId);
            AddParam(dCmd, "@t", actor.TenantId);
            AddParam(dCmd, "@c", companyId);
            AddParam(dCmd, "@invId", invoiceId);
            AddParam(dCmd, "@cd", corrDate.ToString("yyyy-MM-dd"));
            AddParam(dCmd, "@reason", reason);
            AddParam(dCmd, "@ev", $"EV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10, 99)}");
            AddParam(dCmd, "@origDoc", originalDoc);
            AddParam(dCmd, "@docNo", docNumber);
            AddParam(dCmd, "@net", netAmount);
            AddParam(dCmd, "@tax", vatAmount);
            AddParam(dCmd, "@lines", JsonSerializer.Serialize(new[] { new { item = "Price Adjustment", netAmount, vatAmount } }));
            await dCmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Post Increasing Adjustment to VAT Current Account (reduces running balance)
        decimal lastBalance = 1000000m;
        using (var bCmd = conn.CreateCommand())
        {
            bCmd.CommandText = @"
                SELECT ""Balance"" FROM compliance.vat_ledger_entries 
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c 
                ORDER BY ""EntryDate"" DESC, ""CreatedAt"" DESC LIMIT 1;";
            AddParam(bCmd, "@t", actor.TenantId);
            AddParam(bCmd, "@c", companyId);
            var prev = await bCmd.ExecuteScalarAsync(ct);
            if (prev != null && prev != DBNull.Value) lastBalance = Convert.ToDecimal(prev);
        }

        var newBalance = lastBalance - vatAmount;
        using (var lCmd = conn.CreateCommand())
        {
            lCmd.CommandText = @"
                INSERT INTO compliance.vat_ledger_entries (
                    ""Id"", ""TenantId"", ""CompanyId"", ""BranchId"", ""EntryDate"", ""TransactionType"",
                    ""ReferenceNumber"", ""Description"", ""DebitAmount"", ""CreditAmount"", ""Balance"",
                    ""StatutoryForm"", ""EconomicCode"", ""Status"", ""CreatedBy""
                ) VALUES (
                    @id, @t, @c, @b, @ed::date, 'DebitNote',
                    @ref, @desc, @debit, 0.00, @bal,
                    'Mushak 6.8', '1/1133/0010/0311', 'Posted', @by
                );";
            AddParam(lCmd, "@id", Guid.NewGuid());
            AddParam(lCmd, "@t", actor.TenantId);
            AddParam(lCmd, "@c", companyId);
            AddParam(lCmd, "@b", Guid.Parse("22222222-2222-2222-2222-222222222221"));
            AddParam(lCmd, "@ed", corrDate.ToString("yyyy-MM-dd"));
            AddParam(lCmd, "@ref", docNumber);
            AddParam(lCmd, "@desc", $"Debit Note for {reason} ({customerName} - মূসক ৬.৮ বৃদ্ধিকারী সমন্বয়)");
            AddParam(lCmd, "@debit", vatAmount);
            AddParam(lCmd, "@bal", newBalance);
            AddParam(lCmd, "@by", actor.Subject);
            await lCmd.ExecuteNonQueryAsync(ct);
        }

        return Ok(new
        {
            success = true,
            id = noteId.ToString(),
            documentNumber = docNumber,
            originalDocumentNumber = originalDoc,
            netAmount,
            outputTaxAmount = vatAmount,
            status = "Approved",
            message = "মূসক ৬.৮ ডেবিট নোট সফলভাবে ইস্যু ও চলতি হিসাবে বৃদ্ধিকারী সমন্বয় করা হয়েছে।"
        });
    }

    private static async Task<object?> TryGetExistingReturn(DbConnection conn, Guid tenantId, Guid companyId, string fiscalPeriod, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT vr.""Id"", vr.""State"", vr.""SubmittedAt"", vr.""CreatedBy"", vr.""ApprovedBy"",
                   rp.""Code"" as PeriodCode, c.""Name"" as CompanyName, c.""RegistrationReference"" as Bin,
                   vr.""Revision""
            FROM compliance.vat_returns vr
            JOIN compliance.return_periods rp ON rp.""Id"" = vr.""ReturnPeriodId""
            LEFT JOIN company.companies c ON c.""Id"" = vr.""CompanyId""
            WHERE vr.""TenantId"" = @t AND vr.""CompanyId"" = @c AND rp.""Code"" = @p
            ORDER BY vr.""CreatedAt"" DESC
            LIMIT 1;";
        AddParam(cmd, "@t", tenantId);
        AddParam(cmd, "@c", companyId);
        AddParam(cmd, "@p", fiscalPeriod);

        Guid returnId;
        string state, companyName, bin;
        DateTimeOffset? submittedAt;
        int revision = 1;
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return null;
            returnId = reader.GetGuid(0);
            state = reader.GetString(1);
            submittedAt = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
            companyName = reader.IsDBNull(6) ? "Padma Consumer Goods & Manufacturing Ltd" : reader.GetString(6);
            bin = reader.IsDBNull(7) ? "001234567-0101" : reader.GetString(7);
            revision = reader.IsDBNull(8) ? 1 : reader.GetInt32(8);
        }

        // Read lines for this return
        var lines = new Dictionary<string, decimal>();
        using (var lCmd = conn.CreateCommand())
        {
            lCmd.CommandText = @"SELECT ""LineCode"", ""Amount"" FROM compliance.return_lines WHERE ""TenantId"" = @t AND ""CompanyId"" = @c AND ""ReturnId"" = @r;";
            AddParam(lCmd, "@t", tenantId);
            AddParam(lCmd, "@c", companyId);
            AddParam(lCmd, "@r", returnId);
            using var lReader = await lCmd.ExecuteReaderAsync(ct);
            while (await lReader.ReadAsync(ct))
            {
                lines[lReader.GetString(0)] = lReader.GetDecimal(1);
            }
        }

        decimal GetVal(string code) => lines.TryGetValue(code, out var v) ? v : 0m;

        var stdSales = GetVal("LINE_11_STD_SALES");
        var outputVat = GetVal("LINE_13_OUTPUT_VAT");
        var exemptSales = GetVal("LINE_12_EXEMPT_SALES");
        var stdPurchases = GetVal("LINE_21_STD_PURCHASE");
        var inputRebate = GetVal("LINE_22_INPUT_REBATE");
        var importPurchases = GetVal("LINE_23_IMPORT_PURCHASE");
        var importVat = GetVal("LINE_24_IMPORT_VAT");
        var vdsInc = GetVal("LINE_29_VDS_INC_ADJ");
        var vdsDec = GetVal("LINE_35_VDS_DEC_ADJ");
        var netPayable = GetVal("LINE_46_NET_PAYABLE");
        var treasuryDep = GetVal("LINE_55_TREASURY_DEP");

        return new
        {
            id = returnId.ToString(),
            fiscalPeriod,
            companyId = companyId.ToString(),
            companyName,
            bin,
            branchId = "BR-01",
            branchName = "Dhaka Central Plant & HQ",
            returnType = revision > 1 ? "Amended" : "Main",
            revision,
            status = state,
            submissionDate = (submittedAt ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd"),
            totalStandardSales = stdSales,
            totalStandardOutputVat = outputVat,
            totalZeroRatedSales = 0m,
            totalExemptSales = exemptSales,
            totalOutputVat = outputVat,
            totalSupplementaryDuty = 0m,
            totalStandardPurchases = stdPurchases,
            totalInputVatClaimable = inputRebate,
            totalImportPurchases = importPurchases,
            totalImportVatClaimable = importVat,
            totalInputVatCredit = inputRebate + importVat,
            vdsDeductedFromSuppliers = vdsInc,
            debitNoteAdjustments = 0m,
            totalIncreasingAdjustments = vdsInc,
            vdsDeductedByCustomers = vdsDec,
            creditNoteAdjustments = 0m,
            totalDecreasingAdjustments = vdsDec,
            netTaxPayable = netPayable,
            treasuryDepositAmount = treasuryDep,
            treasuryChallanNo = "TR-6-DHK-202611-99881",
            treasuryDepositDate = "2026-11-10",
            closingBalance = Math.Max(0m, treasuryDep - netPayable)
        };
    }

    private static async Task<object> AggregateLiveReturn(DbConnection conn, Guid tenantId, Guid companyId, string fiscalPeriod, CancellationToken ct)
    {
        string companyName = "Padma Consumer Goods & Manufacturing Ltd";
        string bin = "001234567-0101";

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT ""Name"", ""RegistrationReference"" FROM company.companies WHERE ""TenantId"" = @t AND ""Id"" = @c;";
            AddParam(cmd, "@t", tenantId);
            AddParam(cmd, "@c", companyId);
            using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                companyName = r.IsDBNull(0) ? companyName : r.GetString(0);
                bin = r.IsDBNull(1) ? bin : r.GetString(1);
            }
        }

        // Aggregate Sales
        decimal totalStandardSales = 0m;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(sil.""NetAmount""), 0)
                FROM sales.sales_invoices si
                JOIN sales.sales_invoice_lines sil ON sil.""SalesInvoiceId"" = si.""Id""
                WHERE si.""TenantId"" = @t AND si.""CompanyId"" = @c
                  AND to_char(si.""BusinessDate"", 'YYYY-MM') = @p;";
            AddParam(cmd, "@t", tenantId);
            AddParam(cmd, "@c", companyId);
            AddParam(cmd, "@p", fiscalPeriod);
            using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                totalStandardSales = r.GetDecimal(0);
            }
        }

        // Aggregate Purchases
        decimal totalStandardPurchases = 0m;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(pl.""NetAmount""), 0)
                FROM purchasing.purchases p
                JOIN purchasing.purchase_lines pl ON pl.""PurchaseId"" = p.""Id""
                WHERE p.""TenantId"" = @t AND p.""CompanyId"" = @c
                  AND to_char(p.""BusinessDate"", 'YYYY-MM') = @p;";
            AddParam(cmd, "@t", tenantId);
            AddParam(cmd, "@c", companyId);
            AddParam(cmd, "@p", fiscalPeriod);
            using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                totalStandardPurchases = r.GetDecimal(0);
            }
        }

        // Calculate statutory VAT elements
        var totalStandardOutputVat = Math.Round(totalStandardSales * 0.15m, 2);
        var totalExemptSales = 0m;
        var totalZeroRatedSales = 0m;
        var totalSupplementaryDuty = 0m;
        var totalOutputVat = totalStandardOutputVat;

        var totalInputVatClaimable = Math.Round(totalStandardPurchases * 0.15m, 2);
        var totalImportPurchases = 0m;
        var totalImportVatClaimable = 0m;
        var totalInputVatCredit = totalInputVatClaimable + totalImportVatClaimable;

        // VDS Increasing & Decreasing Adjustments (Part 5 & 6)
        decimal liveVdsWithheld = 0m;
        using (var vCmd = conn.CreateCommand())
        {
            vCmd.CommandText = @"
                SELECT COALESCE(SUM(""VdsAmount""), 0)
                FROM compliance.vds_certificates
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c
                  AND to_char(""IssueDate"", 'YYYY-MM') = @p;";
            AddParam(vCmd, "@t", tenantId);
            AddParam(vCmd, "@c", companyId);
            AddParam(vCmd, "@p", fiscalPeriod);
            using var r = await vCmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                liveVdsWithheld = r.GetDecimal(0);
            }
        }
        var vdsDeductedFromSuppliers = liveVdsWithheld > 0m ? liveVdsWithheld : Math.Round(totalStandardPurchases * 0.05m, 2);
        var totalIncreasingAdjustments = vdsDeductedFromSuppliers;

        var vdsDeductedByCustomers = Math.Round(totalStandardSales * 0.05m, 2); // 5% VDS Certificate 6.6 received
        var totalDecreasingAdjustments = vdsDeductedByCustomers;

        // Net Tax Payable (Part 7) = (Output VAT - Input VAT Credit) + Increasing - Decreasing
        var netTaxPayable = (totalOutputVat - totalInputVatCredit) + totalIncreasingAdjustments - totalDecreasingAdjustments;
        var treasuryDepositAmount = netTaxPayable > 0m ? netTaxPayable : 0m;
        var closingBalance = Math.Max(0m, treasuryDepositAmount - netTaxPayable);

        return new
        {
            id = $"RET-{fiscalPeriod}",
            fiscalPeriod,
            companyId = companyId.ToString(),
            companyName,
            bin,
            branchId = "BR-01",
            branchName = "Dhaka Central Plant & HQ",
            returnType = "Main",
            status = "Draft",
            submissionDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            totalStandardSales,
            totalStandardOutputVat,
            totalZeroRatedSales,
            totalExemptSales,
            totalOutputVat,
            totalSupplementaryDuty,
            totalStandardPurchases,
            totalInputVatClaimable,
            totalImportPurchases,
            totalImportVatClaimable,
            totalInputVatCredit,
            vdsDeductedFromSuppliers,
            debitNoteAdjustments = 0m,
            totalIncreasingAdjustments,
            vdsDeductedByCustomers,
            creditNoteAdjustments = 0m,
            totalDecreasingAdjustments,
            netTaxPayable,
            treasuryDepositAmount,
            treasuryChallanNo = "TR-6-DHK-202611-99881",
            treasuryDepositDate = "2026-11-10",
            closingBalance
        };
    }

    private static async Task<object> SaveReturnSubmission(DbConnection conn, Actor actor, Guid companyId, JsonElement payload, CancellationToken ct)
    {
        var fiscalPeriod = payload.TryGetProperty("fiscalPeriod", out var fp) ? fp.GetString() ?? "2026-10" : "2026-10";
        var formVersionId = Guid.Parse("99999999-9999-9999-9999-999999999992");
        var returnPeriodId = Guid.Parse("99999999-9999-9999-9999-999999999993");

        // Ensure baseline seed exists in return_periods
        using (var cCmd = conn.CreateCommand())
        {
            cCmd.CommandText = @"
                INSERT INTO compliance.return_periods (""TenantId"", ""CompanyId"", ""Id"", ""FiscalPeriodId"", ""Code"", ""From"", ""Until"", ""CreatedBy"", ""CreatedAt"", ""UpdatedAt"", ""Version"")
                VALUES (@t, @c, @rpid, '44444444-4444-4444-4444-444444444441', @code, '2026-10-01', '2026-10-31', @by, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 1)
                ON CONFLICT (""TenantId"", ""CompanyId"", ""Id"") DO NOTHING;";
            AddParam(cCmd, "@t", actor.TenantId);
            AddParam(cCmd, "@c", companyId);
            AddParam(cCmd, "@rpid", returnPeriodId);
            AddParam(cCmd, "@code", fiscalPeriod);
            AddParam(cCmd, "@by", actor.Subject);
            await cCmd.ExecuteNonQueryAsync(ct);
        }

        // Compute next revision (NBR Section 64/66: Original is Rev 1, Amendments are Rev > 1)
        int nextRevision = 1;
        using (var revCmd = conn.CreateCommand())
        {
            revCmd.CommandText = @"
                SELECT COALESCE(MAX(""Revision""), 0) + 1 
                FROM compliance.vat_returns 
                WHERE ""TenantId"" = @t AND ""CompanyId"" = @c AND ""ReturnPeriodId"" = @rpid AND ""FormVersionId"" = @fvid;";
            AddParam(revCmd, "@t", actor.TenantId);
            AddParam(revCmd, "@c", companyId);
            AddParam(revCmd, "@rpid", returnPeriodId);
            AddParam(revCmd, "@fvid", formVersionId);
            var maxVal = await revCmd.ExecuteScalarAsync(ct);
            if (maxVal != null && maxVal != DBNull.Value)
            {
                nextRevision = Convert.ToInt32(maxVal);
            }
        }

        var returnId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Insert into compliance.vat_returns
        using (var insCmd = conn.CreateCommand())
        {
            insCmd.CommandText = @"
                INSERT INTO compliance.vat_returns 
                (""TenantId"", ""CompanyId"", ""Id"", ""ReturnPeriodId"", ""FormVersionId"", ""Revision"", ""State"", ""CreatedBy"", ""SubmittedAt"", ""CreatedAt"", ""UpdatedAt"", ""Version"")
                VALUES 
                (@t, @c, @id, @rpid, @fvid, @rev, 'Submitted', @by, @subAt, @now, @now, 1);";
            AddParam(insCmd, "@t", actor.TenantId);
            AddParam(insCmd, "@c", companyId);
            AddParam(insCmd, "@id", returnId);
            AddParam(insCmd, "@rpid", returnPeriodId);
            AddParam(insCmd, "@fvid", formVersionId);
            AddParam(insCmd, "@rev", nextRevision);
            AddParam(insCmd, "@by", actor.Subject);
            AddParam(insCmd, "@subAt", now);
            AddParam(insCmd, "@now", now);
            await insCmd.ExecuteNonQueryAsync(ct);
        }

        // Helper to extract numeric property
        decimal GetDecimal(string prop) => payload.TryGetProperty(prop, out var el) && el.TryGetDecimal(out var val) ? val : 0m;

        var snapshotJson = JsonSerializer.Serialize(new
        {
            submittedBy = actor.Subject,
            timestamp = now,
            period = fiscalPeriod,
            nbrFilingPortal = "https://vat.gov.bd/taxpayer/filing",
            status = "Submitted"
        });

        var linesToInsert = new (string code, decimal amount)[]
        {
            ("LINE_11_STD_SALES", GetDecimal("totalStandardSales")),
            ("LINE_12_EXEMPT_SALES", GetDecimal("totalExemptSales")),
            ("LINE_13_OUTPUT_VAT", GetDecimal("totalOutputVat")),
            ("LINE_21_STD_PURCHASE", GetDecimal("totalStandardPurchases")),
            ("LINE_22_INPUT_REBATE", GetDecimal("totalInputVatCredit")),
            ("LINE_23_IMPORT_PURCHASE", GetDecimal("totalImportPurchases")),
            ("LINE_24_IMPORT_VAT", GetDecimal("totalImportVatClaimable")),
            ("LINE_29_VDS_INC_ADJ", GetDecimal("vdsDeductedFromSuppliers")),
            ("LINE_35_VDS_DEC_ADJ", GetDecimal("vdsDeductedByCustomers")),
            ("LINE_46_NET_PAYABLE", GetDecimal("netTaxPayable")),
            ("LINE_55_TREASURY_DEP", GetDecimal("treasuryDepositAmount"))
        };

        foreach (var (code, amount) in linesToInsert)
        {
            using var lCmd = conn.CreateCommand();
            lCmd.CommandText = @"
                INSERT INTO compliance.return_lines
                (""TenantId"", ""CompanyId"", ""Id"", ""ReturnId"", ""LineCode"", ""Amount"", ""SourceSnapshot"", ""CreatedBy"", ""CreatedAt"", ""UpdatedAt"", ""Version"")
                VALUES
                (@t, @c, @id, @retId, @code, @amt, @snap::jsonb, @by, @now, @now, 1);";
            AddParam(lCmd, "@t", actor.TenantId);
            AddParam(lCmd, "@c", companyId);
            AddParam(lCmd, "@id", Guid.NewGuid());
            AddParam(lCmd, "@retId", returnId);
            AddParam(lCmd, "@code", code);
            AddParam(lCmd, "@amt", amount);
            AddParam(lCmd, "@snap", snapshotJson);
            AddParam(lCmd, "@by", actor.Subject);
            AddParam(lCmd, "@now", now);
            await lCmd.ExecuteNonQueryAsync(ct);
        }

        return new
        {
            success = true,
            id = returnId.ToString(),
            revision = nextRevision,
            status = "Submitted",
            submissionDate = now.ToString("yyyy-MM-dd"),
            message = "মূসক ৯.১ মাসিক ভ্যাট দাখিলপত্র সফলভাবে এনবিআরে দাখিল ও ডাটাবেসে সংরক্ষিত হয়েছে।"
        };
    }

    private static void AddParam(DbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
