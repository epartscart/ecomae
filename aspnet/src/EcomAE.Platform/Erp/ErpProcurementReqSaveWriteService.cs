using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_proc_req_save</c> twin. Schema ensure and add-line stay PHP.
/// Submit / decision / convert are <c>IErpProcurementReqWriteService</c>.
/// </summary>
public interface IErpProcurementReqSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        string? requester,
        long businessUnitId,
        string? justification,
        string? reqNumber,
        long companyId,
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class ErpProcurementReqSaveWriteService : IErpProcurementReqSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpProcurementReqSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        string? requester,
        long businessUnitId,
        string? justification,
        string? reqNumber,
        long companyId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var who = Clip(requester, 160);
        if (who.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Requester is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (id < 0)
        {
            id = 0;
        }

        if (companyId < 0)
        {
            companyId = 0;
        }

        if (businessUnitId < 0)
        {
            businessUnitId = 0;
        }

        var note = Clip(justification, 4000);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_proc_req` SET `requester`=?, `business_unit_id`=?, `justification`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                who, businessUnitId, note, now, id);
            return ErpSimpleWriteResult.Ok("Requisition saved", id);
        }

        var number = Clip(reqNumber, 40);
        if (number.Length == 0)
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_proc_req` WHERE `company_id`=?"),
                cancellationToken,
                companyId);
            number = FormatReqNumber(count + 1);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_proc_req` (`company_id`,`req_number`,`requester`,`business_unit_id`,`status`,`justification`,`total`,`time_created`,`time_updated`) VALUES (?,?,?,?,'draft',?,0,?,?)"),
            cancellationToken,
            companyId, number, who, businessUnitId, note, now, now);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Requisition saved", inserted);
    }

    public static string FormatReqNumber(long sequence)
        => "PR-" + Math.Max(1, sequence).ToString("00000", CultureInfo.InvariantCulture);

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
