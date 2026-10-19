using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Month-end bridge from the fit-out workspace into the general ledger: posts
/// IFRS 15 over-time journals for project costs, POC revenue, certified claims
/// and client advances so the income statement, balance sheet and dashboard
/// insight figures reflect the same project P&amp;L/WIP the workspace shows.
/// Idempotent — rows already journaled (matching source_type + source_id) are skipped.
/// </summary>
public interface IErpFitOutGlPostWriteService
{
    Task<string> PostAsync(long projectId, string? period, CancellationToken cancellationToken = default);
}

public sealed class ErpFitOutGlPostWriteService : IErpFitOutGlPostWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpFitOutGlPostWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public async Task<string> PostAsync(long projectId, string? period, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — GL posting skipped.";
        }

        var (fromUtc, toUtc) = PeriodWindow(period);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureProjectAccountingAsync(connection, cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpFitOutGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        var costPosted = 0;
        var revenuePosted = 0;
        var claimsPosted = 0;
        var advancesPosted = 0;
        var skipped = 0;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, `txn_type`, `amount`, `txn_date`, `project_id`, `description` FROM `epc_prja_txn`"
                + " WHERE (?<=0 OR `project_id`=?) AND (?=0 OR `txn_date`>=?) AND (?=0 OR `txn_date`<?)"
                + " ORDER BY `id`");
            ErpDb.AddParameters(command, projectId, projectId, fromUtc, fromUtc, toUtc, toUtc);
            var rows = new List<(long Id, string Type, decimal Amount, long Date, long ProjectId, string Description)>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add((
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetDecimal(2),
                        reader.GetInt64(3),
                        reader.GetInt64(4),
                        reader.IsDBNull(5) ? "" : reader.GetString(5)));
                }
            }

            foreach (var row in rows)
            {
                var sourceType = "fitout_prja_" + (row.Type is "cost" ? "cost" : row.Type is "billing" ? "bill" : "rev");
                if (await ErpFitOutGl.PostedJournalIdAsync(connection, sourceType, row.Id, cancellationToken).ConfigureAwait(false) > 0)
                {
                    skipped++;
                    continue;
                }

                var reference = "FO-P" + row.ProjectId.ToString(CultureInfo.InvariantCulture) + "-TXN-" + row.Id.ToString(CultureInfo.InvariantCulture);
                var description = "Fit-out " + row.Type + " — " + row.Description;
                var journalId = row.Type switch
                {
                    "cost" => await ErpFitOutGl.PostAsync(_gl, connection, null, 0, companyId, row.Id, sourceType, reference, description, ErpFitOutIfrs.CostIncurred(row.Amount), row.Date, cancellationToken).ConfigureAwait(false),
                    "billing" => await ErpFitOutGl.PostAsync(_gl, connection, null, 0, companyId, row.Id, sourceType, reference, description, ErpFitOutIfrs.ClaimCertified(row.Amount, 0m), row.Date, cancellationToken).ConfigureAwait(false),
                    "revenue" => await ErpFitOutGl.PostAsync(_gl, connection, null, 0, companyId, row.Id, sourceType, reference, description, ErpFitOutIfrs.RevenueEarned(row.Amount), row.Date, cancellationToken).ConfigureAwait(false),
                    _ => 0,
                };
                if (journalId <= 0)
                {
                    skipped++;
                    continue;
                }

                if (row.Type == "cost")
                {
                    costPosted++;
                }
                else if (row.Type == "billing")
                {
                    claimsPosted++;
                }
                else if (row.Type == "revenue")
                {
                    revenuePosted++;
                }
            }
        }

        var claims = await PostApprovedClaimsAsync(connection, projectId, fromUtc, toUtc, companyId, cancellationToken).ConfigureAwait(false);
        claimsPosted += claims;
        advancesPosted = await PostContractAdvancesAsync(connection, projectId, companyId, cancellationToken).ConfigureAwait(false);

        return "Posted to GL (IFRS 15): "
               + costPosted + " cost journals, " + revenuePosted + " POC revenue journals, "
               + claimsPosted + " certification journals, " + advancesPosted + " advance journals"
               + (skipped > 0 ? " (" + skipped + " already posted skipped)" : "")
               + " — income statement, balance sheet and dashboard figures now include the fit-out numbers.";
    }

    /// <summary>Approved client progress claims → Dr AR / Cr contract asset (+ output VAT split when the claim payload carries it).</summary>
    private async Task<int> PostApprovedClaimsAsync(
        DbConnection connection,
        long projectId,
        long fromUtc,
        long toUtc,
        long companyId,
        CancellationToken cancellationToken)
    {
        var count = 0;
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`, `project_id`, `amount`, `description`, UNIX_TIMESTAMP(`event_date`) FROM `ecomae_fitout_delivery_records`"
            + " WHERE `record_type`='client_progress_claim' AND `status`='approved'"
            + " AND (?<=0 OR `project_id`=?) AND (?=0 OR UNIX_TIMESTAMP(`event_date`)>=?) AND (?=0 OR UNIX_TIMESTAMP(`event_date`)<?)"
            + " ORDER BY `id`");
        ErpDb.AddParameters(command, projectId, projectId, fromUtc, fromUtc, toUtc, toUtc);
        var rows = new List<(long Id, long ProjectId, decimal Amount, string Description, long Date)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetDecimal(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? 0 : reader.GetInt64(4)));
            }
        }

        foreach (var row in rows)
        {
            if (await ErpFitOutGl.PostedJournalIdAsync(connection, "fitout_claim", row.Id, cancellationToken).ConfigureAwait(false) > 0)
            {
                continue;
            }

            var reference = "FO-P" + row.ProjectId.ToString(CultureInfo.InvariantCulture) + "-IPC-" + row.Id.ToString(CultureInfo.InvariantCulture);
            var vat = ExtractAmount(row.Description, "VAT");
            var journalId = await ErpFitOutGl.PostAsync(
                _gl, connection, null, 0, companyId, row.Id, "fitout_claim", reference,
                "Certified client progress claim — " + row.Description,
                ErpFitOutIfrs.ClaimCertified(row.Amount, vat), row.Date, cancellationToken).ConfigureAwait(false);
            if (journalId > 0)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Contract terms carrying an advance amount → Dr bank / Cr contract liability, once per terms row.</summary>
    private async Task<int> PostContractAdvancesAsync(
        DbConnection connection,
        long projectId,
        long companyId,
        CancellationToken cancellationToken)
    {
        var count = 0;
        await using var command = connection.CreateCommand();
        // contract_id is the project id; the advance amount is contract_value * advance_percent.
        command.CommandText = ErpDb.Positional(
            "SELECT t.`contract_id`, p.`contract_value`, t.`advance_percent`"
            + " FROM `ecomae_fitout_contract_terms` t"
            + " INNER JOIN `epc_prj_projects` p ON p.`id`=t.`contract_id`"
            + " WHERE t.`advance_percent`>0 AND (?<=0 OR t.`contract_id`=?) ORDER BY t.`contract_id`");
        ErpDb.AddParameters(command, projectId, projectId);
        var rows = new List<(long Id, long ProjectId, decimal Advance)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var contractId = reader.GetInt64(0);
                rows.Add((contractId, contractId, reader.GetDecimal(1) * reader.GetDecimal(2) / 100m));
            }
        }

        foreach (var row in rows)
        {
            if (await ErpFitOutGl.PostedJournalIdAsync(connection, "fitout_advance", row.Id, cancellationToken).ConfigureAwait(false) > 0)
            {
                continue;
            }

            var reference = "FO-P" + row.ProjectId.ToString(CultureInfo.InvariantCulture) + "-ADV-" + row.Id.ToString(CultureInfo.InvariantCulture);
            var journalId = await ErpFitOutGl.PostAsync(
                _gl, connection, null, 0, companyId, row.Id, "fitout_advance", reference,
                "Client advance on contract terms",
                ErpFitOutIfrs.AdvanceReceived(row.Advance), 0, cancellationToken).ConfigureAwait(false);
            if (journalId > 0)
            {
                count++;
            }
        }
        return count;
    }

    private static decimal ExtractAmount(string description, string label)
    {
        var idx = description.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return 0m;
        }

        var tail = description[(idx + label.Length)..].TrimStart(' ', '=', ':');
        var end = 0;
        while (end < tail.Length && (char.IsDigit(tail[end]) || tail[end] is '.' or '-' or ','))
        {
            end++;
        }
        return decimal.TryParse(tail[..end], NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }

    private static (long FromUtc, long ToUtc) PeriodWindow(string? period)
    {
        if (!string.IsNullOrWhiteSpace(period)
            && DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var from = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var to = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).ToUnixTimeSeconds();
            return (from, to);
        }
        return (0, 0);
    }
}
