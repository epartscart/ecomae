using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_erp_payroll_approve_run</c>.</summary>
public interface IErpPayrollWriteService
{
    Task<ErpSimpleWriteResult> ApproveRunAsync(long runId, CancellationToken cancellationToken = default);
}

public sealed class ErpPayrollWriteService : IErpPayrollWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPayrollWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ApproveRunAsync(long runId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (runId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payroll run id is required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT IFNULL(`status`,'') AS status FROM `epc_erp_payroll_runs` WHERE `id` = @id LIMIT 1";
        Add(select, "@id", runId);
        var statusObj = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var status = Convert.ToString(statusObj ?? string.Empty, CultureInfo.InvariantCulture) ?? string.Empty;
        if (status.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Payroll run not found.");
        }

        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("already_paid", "Already paid.");
        }

        if (!string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid_run", "Invalid run.");
        }

        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE `epc_erp_payroll_runs` SET `status` = 'approved' WHERE `id` = @id";
        Add(update, "@id", runId);
        var rows = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok("Payroll run approved.", runId)
            : ErpSimpleWriteResult.Fail("update_failed", "Could not approve payroll run.");
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}

public sealed record ErpSimpleWriteResult(bool Succeeded, string Code, string Message, long Id, int Writes)
{
    public static ErpSimpleWriteResult Ok(string message, long id)
        => new(true, "ok", message, id, 1);

    public static ErpSimpleWriteResult Fail(string code, string message)
        => new(false, code, message, 0, 0);
}
