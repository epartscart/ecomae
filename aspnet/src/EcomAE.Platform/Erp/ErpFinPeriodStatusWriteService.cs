namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fin_period_set_status</c> twin. Schema ensure and period generate stay PHP.
/// Distinct from year-end <c>epc_fy_set_period_status</c> on <c>epc_fy_periods</c>.
/// </summary>
public interface IErpFinPeriodStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(
        long companyId,
        int fy,
        int periodNo,
        string? status,
        CancellationToken cancellationToken = default);
}

public sealed class ErpFinPeriodStatusWriteService : IErpFinPeriodStatusWriteService
{
    internal static readonly string[] Allowed = ["open", "on_hold", "closed"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpFinPeriodStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long companyId,
        int fy,
        int periodNo,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (fy <= 0 || periodNo <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "fy and periodNo must be positive.");
        }

        var next = NormalizeStatus(status);
        if (next is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid period status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var resolvedCompany = companyId > 0
            ? companyId
            : await ResolveCompanyAsync(connection, cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_fin_periods` SET `status`=? WHERE `company_id`=? AND `fy`=? AND `period_no`=?"),
            cancellationToken,
            next, resolvedCompany, fy, periodNo);
        return ErpSimpleWriteResult.Ok("Period status updated", resolvedCompany);
    }

    public static string? NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Allowed.Contains(status, StringComparer.Ordinal) ? status : null;
    }

    private static async Task<long> ResolveCompanyAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id` LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return 0;
        }
    }
}
