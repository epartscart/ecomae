using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ins_claim_save</c> twin used by ajax <c>ins_claim_add</c>.
/// Schema ensure, policy save/delete, and document add stay PHP.
/// </summary>
public interface IErpInsClaimAddWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        long policyId,
        string? claimNo,
        string? lossDate,
        string? notifiedDate,
        string? deadlineDate,
        string? description,
        decimal claimAmount,
        decimal settledAmount,
        string? surveyor,
        string? status,
        string? note,
        CancellationToken cancellationToken = default);
}

public sealed class ErpInsClaimAddWriteService : IErpInsClaimAddWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpInsClaimAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        long policyId,
        string? claimNo,
        string? lossDate,
        string? notifiedDate,
        string? deadlineDate,
        string? description,
        decimal claimAmount,
        decimal settledAmount,
        string? surveyor,
        string? status,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A claim id must be >= 0.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nextStatus = NormalizeStatus(status);
        var claimNumber = Clip(claimNo, 120);
        var desc = description ?? string.Empty;
        var survey = Clip(surveyor, 200);
        var claimNote = note ?? string.Empty;
        var lossUnix = ResolveDateUnix(lossDate);
        var notifiedUnix = ResolveNotifiedUnix(notifiedDate, now);
        var deadlineUnix = ResolveDateUnix(deadlineDate);
        var claimAmt = decimal.Round(claimAmount, 2, MidpointRounding.AwayFromZero);
        var settledAmt = decimal.Round(settledAmount, 2, MidpointRounding.AwayFromZero);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_ins_claims` SET `claim_no`=?, `loss_date`=?, `notified_date`=?, `description`=?, `claim_amount`=?, `settled_amount`=?, `surveyor`=?, `deadline_date`=?, `status`=?, `note`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                claimNumber, lossUnix, notifiedUnix, desc, claimAmt, settledAmt, survey, deadlineUnix, nextStatus, claimNote, now, id);
            return ErpSimpleWriteResult.Ok("Claim logged", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_ins_claims` (`policy_id`,`claim_no`,`loss_date`,`notified_date`,`description`,`claim_amount`,`settled_amount`,`surveyor`,`deadline_date`,`status`,`note`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            policyId, claimNumber, lossUnix, notifiedUnix, desc, claimAmt, settledAmt, survey, deadlineUnix, nextStatus, claimNote, now, now);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Claim logged", inserted);
    }

    public static string NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ErpInsClaimStatusWriteService.Allowed.Contains(status, StringComparer.Ordinal)
            ? status
            : "notified";
    }

    public static long ResolveDateUnix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return unix < 0 ? 0 : unix;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return 0;
    }

    public static long ResolveNotifiedUnix(string? raw, long nowUnix)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length == 0 ? nowUnix : ResolveDateUnix(text);
    }

    private static string Clip(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }
}
