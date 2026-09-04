namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>hr_update_days</c> twin. Payroll generate/pay and line recalc stay PHP.</summary>
public interface IErpHrDaysWriteService
{
    Task<ErpSimpleWriteResult> SetDaysWorkedAsync(long staffProfileId, decimal daysWorked, CancellationToken cancellationToken = default);
}

public sealed class ErpHrDaysWriteService : IErpHrDaysWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrDaysWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetDaysWorkedAsync(
        long staffProfileId,
        decimal daysWorked,
        CancellationToken cancellationToken = default)
    {
        if (staffProfileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Staff profile id is required.");
        }

        if (daysWorked < 0)
        {
            daysWorked = 0;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var days = decimal.Round(daysWorked, 1, MidpointRounding.AwayFromZero);
        var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_hr_records` SET `days_worked` = ?, `time_updated` = ? WHERE `staff_profile_id` = ?"),
            cancellationToken,
            days, updatedAt, staffProfileId);
        return ErpSimpleWriteResult.Ok("Days worked saved for next payroll run", staffProfileId);
    }
}
