using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twins of <c>epc_crm_update_opportunity_stage</c> and <c>epc_crm_save_opportunity</c>.
/// Convert, quote email, and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmOpportunityWriteService
{
    Task<ErpSimpleWriteResult> UpdateStageAsync(
        long id,
        string? stage,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmOpportunitySaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpCrmOpportunitySaveRequest(
    long Id,
    long LeadId,
    string? Title,
    string? Stage,
    decimal Amount,
    int Probability,
    string? CloseDate,
    long OwnerUserId,
    long LinkedUserId,
    string? Notes);

public sealed class CpCrmOpportunityWriteService : ICpCrmOpportunityWriteService
{
    public static readonly HashSet<string> Stages = new(StringComparer.Ordinal)
    {
        "prospect", "qualified", "proposal", "negotiation", "won", "lost",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmOpportunityWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeStage(string? stage)
    {
        var raw = (stage ?? string.Empty).Trim();
        return Stages.Contains(raw) ? raw : "prospect";
    }

    public static string NormalizeTitle(string? title)
    {
        var raw = (title ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            raw = "Opportunity";
        }

        return raw.Length > 255 ? raw[..255] : raw;
    }

    public static int NormalizeProbability(int probability)
        => Math.Clamp(probability, 0, 100);

    public static decimal NormalizeAmount(decimal amount)
        => amount < 0 ? 0 : amount;

    public static long ParseCloseDate(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix >= 0)
        {
            return unix;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            if (parsed.TimeOfDay == TimeSpan.Zero)
            {
                parsed = parsed.AddHours(12);
            }

            return parsed.ToUnixTimeSeconds();
        }

        return 0;
    }

    public async Task<ErpSimpleWriteResult> UpdateStageAsync(
        long id,
        string? stage,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Opportunity id is invalid.");
        }

        var rowStage = (stage ?? string.Empty).Trim();
        if (!Stages.Contains(rowStage))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid stage");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_crm_opportunities`
                    SET `stage`=?, `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                rowStage, now, id);
            return ErpSimpleWriteResult.Ok("Stage updated", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM opportunity table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmOpportunitySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Opportunity id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var title = NormalizeTitle(request.Title);
        var stage = NormalizeStage(request.Stage);
        var amount = NormalizeAmount(request.Amount);
        var probability = NormalizeProbability(request.Probability);
        var close = ParseCloseDate(request.CloseDate);
        var owner = request.OwnerUserId > 0 ? request.OwnerUserId : 0;
        var linked = request.LinkedUserId > 0 ? request.LinkedUserId : 0;
        var notes = (request.Notes ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `id`=?"),
                    cancellationToken,
                    request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Opportunity was not found.");
                }

                if (owner <= 0)
                {
                    owner = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `owner_user_id` FROM `epc_crm_opportunities` WHERE `id`=?"),
                        cancellationToken,
                        request.Id).ConfigureAwait(false);
                }

                if (notes.Length == 0)
                {
                    notes = (await ErpDb.StringAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `notes` FROM `epc_crm_opportunities` WHERE `id`=?"),
                        cancellationToken,
                        request.Id).ConfigureAwait(false) ?? string.Empty).Trim();
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_opportunities`
                        SET `lead_id`=?, `title`=?, `stage`=?, `amount`=?, `probability`=?, `close_date`=?,
                            `owner_user_id`=?, `linked_user_id`=?, `notes`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    request.LeadId, title, stage, amount, probability, close,
                    owner, linked, notes, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Opportunity saved", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_opportunities`
                    (`lead_id`, `title`, `stage`, `amount`, `probability`, `close_date`, `owner_user_id`, `linked_user_id`, `notes`, `time_created`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                request.LeadId, title, stage, amount, probability, close,
                owner, linked, notes, now, now).ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Opportunity saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM opportunity table is missing — schema-ensure stays Classic.");
        }
    }
}
