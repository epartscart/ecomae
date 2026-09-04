namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_proc_req_submit</c> / <c>epc_proc_req_decision</c> twins.
/// Schema ensure, save, add-line, and convert stay PHP.
/// </summary>
public interface IErpProcurementReqWriteService
{
    Task<ErpSimpleWriteResult> SubmitAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DecideAsync(
        long id,
        bool approve,
        string? by,
        string? note,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ConvertAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpProcurementReqWriteService : IErpProcurementReqWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpProcurementReqWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SubmitAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A requisition id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_proc_req` WHERE `id` = ?"),
            cancellationToken,
            id);
        if (status is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Requisition not found");
        }

        if (!string.Equals(status, "draft", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only a draft requisition can be submitted");
        }

        var lineCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_proc_req_line` WHERE `req_id` = ?"),
            cancellationToken,
            id);
        if (lineCount == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add at least one line before submitting");
        }

        var needsApproval = await RecalcAsync(connection, id, cancellationToken).ConfigureAwait(false);
        var next = needsApproval ? "submitted" : "approved";
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_proc_req` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            next, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), id);

        return ErpSimpleWriteResult.Ok(
            next == "approved" ? "Requisition approved (within policy)" : "Requisition submitted for approval",
            id);
    }

    public async Task<ErpSimpleWriteResult> DecideAsync(
        long id,
        bool approve,
        string? by,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A requisition id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var decidedBy = Clip(by, 160);
        var decisionNote = Clip(note, 255);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_proc_req` WHERE `id` = ?"),
            cancellationToken,
            id);
        if (status is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Requisition not found");
        }

        if (!string.Equals(status, "submitted", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only a submitted requisition can be approved or rejected");
        }

        var next = approve ? "approved" : "rejected";
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_proc_req` SET `status` = ?, `decided_by` = ?, `decision_note` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            next, decidedBy, decisionNote, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), id);

        return ErpSimpleWriteResult.Ok(approve ? "Requisition approved" : "Requisition rejected", id);
    }

    public async Task<ErpSimpleWriteResult> ConvertAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A requisition id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_proc_req` WHERE `id` = ?"),
            cancellationToken,
            id);
        if (status is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Requisition not found");
        }

        if (!string.Equals(status, "approved", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only an approved requisition can be converted to a PO");
        }

        var reqNumber = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `req_number` FROM `epc_proc_req` WHERE `id` = ?"),
            cancellationToken,
            id) ?? string.Empty;
        var poRef = "PO-" + reqNumber;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_proc_req` SET `status` = 'converted', `po_ref` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            poRef, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), id);
        return ErpSimpleWriteResult.Ok("Converted to " + poRef, id);
    }

    /// <summary>PHP <c>epc_proc_req_recalc</c> + <c>epc_proc_requires_approval</c> (no schema ensure).</summary>
    private static async Task<bool> RecalcAsync(
        System.Data.Common.DbConnection connection,
        long reqId,
        CancellationToken cancellationToken)
    {
        var total = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(SUM(`line_total`),0) FROM `epc_proc_req_line` WHERE `req_id` = ?"),
            cancellationToken,
            reqId);
        var companyId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `company_id` FROM `epc_proc_req` WHERE `id` = ?"),
            cancellationToken,
            reqId);
        var categoryId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `category_id` FROM `epc_proc_req_line` WHERE `req_id` = ? ORDER BY `id` ASC LIMIT 1"),
            cancellationToken,
            reqId);

        var thresholdObj = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `approval_threshold` FROM `epc_proc_policy` WHERE `company_id` = ? AND `active` = 1 AND `category_id` = ? ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            companyId, categoryId);
        if (thresholdObj is null)
        {
            thresholdObj = await ErpDb.ScalarAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `approval_threshold` FROM `epc_proc_policy` WHERE `company_id` = ? AND `active` = 1 AND `category_id` = 0 ORDER BY `id` DESC LIMIT 1"),
                cancellationToken,
                companyId);
        }

        bool needs;
        if (thresholdObj is null)
        {
            needs = total > 0m;
        }
        else
        {
            var threshold = Convert.ToDecimal(thresholdObj, System.Globalization.CultureInfo.InvariantCulture);
            needs = threshold > 0m && total > threshold;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_proc_req` SET `total` = ?, `requires_approval` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            total, needs ? 1 : 0, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), reqId);
        return needs;
    }

    private static string Clip(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }
}
