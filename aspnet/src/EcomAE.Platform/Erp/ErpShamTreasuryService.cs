namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM blueprint §13: treasury instruments per legal entity — letters
/// of credit, cash-against-documents, and FX contracts. A lifecycle
/// register: instruments are opened, then settled or expired; the row
/// keeps status + dates so exposure per entity is reportable at any
/// point. Table <c>epc_erp_org_treasury_instruments</c> (tenant-generic).
/// Grants: <c>treasury-set</c> / <c>treasury-view</c>.
/// </summary>
public interface IErpShamTreasuryService
{
    Task<ErpShamTreasuryResult> RegisterAsync(string entityCode, string kind, string reference, string counterparty, decimal amount, string currency, long? expiryDate, string userEmail, CancellationToken cancellationToken = default);
    Task<ErpShamTreasuryResult> TransitionAsync(string entityCode, string reference, string action, string userEmail, CancellationToken cancellationToken = default);
    Task<ErpShamTreasuryListResult> ListAsync(string entityCode, string? status, string userEmail, CancellationToken cancellationToken = default);
}

public sealed record ErpShamTreasuryInstrument(string Reference, string Kind, string Counterparty, decimal Amount, string Currency, string Status, long Opened, long Expiry, long Settled);
public sealed record ErpShamTreasuryResult(bool Ok, string Message, string Reference, string Status);
public sealed record ErpShamTreasuryListResult(bool Ok, string Message, decimal Exposure, IReadOnlyList<ErpShamTreasuryInstrument>? Instruments);

public sealed class ErpShamTreasuryService : IErpShamTreasuryService
{
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase) { "lc", "cad", "fx" };
    private static readonly HashSet<string> Transitions = new(StringComparer.OrdinalIgnoreCase) { "settle", "expire", "cancel" };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    public ErpShamTreasuryService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public static async Task EnsureSchemaAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_treasury_instruments` (`id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `entity_id` int(11) NOT NULL DEFAULT 0, `kind` varchar(10) NOT NULL DEFAULT 'lc',"
            + " `reference` varchar(80) NOT NULL DEFAULT '', `counterparty` varchar(190) NOT NULL DEFAULT '',"
            + " `amount` decimal(14,2) NOT NULL DEFAULT 0, `currency_code` varchar(8) NOT NULL DEFAULT '',"
            + " `status` varchar(12) NOT NULL DEFAULT 'open', `opened` int(11) NOT NULL DEFAULT 0,"
            + " `expiry` int(11) NOT NULL DEFAULT 0, `settled` int(11) NOT NULL DEFAULT 0,"
            + " `set_by` varchar(190) NOT NULL DEFAULT '', `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_ref` (`entity_id`,`reference`), KEY `x_status` (`entity_id`,`status`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Treasury instruments LC/CAD/FX per entity (§13)'",
            cancellationToken).ConfigureAwait(false);

    public async Task<ErpShamTreasuryResult> RegisterAsync(string entityCode, string kind, string reference, string counterparty, decimal amount, string currency, long? expiryDate, string userEmail, CancellationToken cancellationToken = default)
    {
        kind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        if (!Kinds.Contains(kind))
        {
            return new(false, "kind must be lc, cad, or fx.", reference ?? "", "");
        }
        if (amount <= 0)
        {
            return new(false, "amount must be positive.", reference ?? "", "");
        }
        var refx = (reference ?? string.Empty).Trim();
        if (refx.Length == 0)
        {
            return new(false, "reference is required.", "", "");
        }
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, null, "treasury-set", cancellationToken).ConfigureAwait(false))
        {
            return new(false, "treasury-set grant required for this entity.", refx, "");
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return new(false, "Unknown entity code.", refx, "");
        }

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var existing = await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_org_treasury_instruments` WHERE `entity_id`=? AND `reference`=? LIMIT 1",
            cancellationToken, entityId, refx).ConfigureAwait(false);
        if (existing > 0)
        {
            return new(false, "Instrument " + refx + " already registered.", refx, "");
        }
        await ErpDb.ExecuteAsync(conn, null,
            "INSERT INTO `epc_erp_org_treasury_instruments` (`entity_id`,`kind`,`reference`,`counterparty`,`amount`,`currency_code`,`status`,`opened`,`expiry`,`set_by`,`time_created`) VALUES (?,?,?,?,?,?,'open',?,?,?,?)",
            cancellationToken, entityId, kind, refx, (counterparty ?? string.Empty).Trim(), amount,
            (currency ?? string.Empty).Trim().ToUpperInvariant(), now, expiryDate ?? 0, userEmail, now).ConfigureAwait(false);
        return new(true, kind.ToUpperInvariant() + " " + refx + " opened.", refx, "open");
    }

    public async Task<ErpShamTreasuryResult> TransitionAsync(string entityCode, string reference, string action, string userEmail, CancellationToken cancellationToken = default)
    {
        action = (action ?? string.Empty).Trim().ToLowerInvariant();
        if (!Transitions.Contains(action))
        {
            return new(false, "action must be settle, expire, or cancel.", reference ?? "", "");
        }
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, null, "treasury-set", cancellationToken).ConfigureAwait(false))
        {
            return new(false, "treasury-set grant required for this entity.", reference ?? "", "");
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return new(false, "Unknown entity code.", reference ?? "", "");
        }
        var newStatus = action == "settle" ? "settled" : action == "expire" ? "expired" : "cancelled";
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var n = await ErpDb.ExecuteAsync(conn, null,
            "UPDATE `epc_erp_org_treasury_instruments` SET `status`=?, `settled`=? WHERE `entity_id`=? AND `reference`=? AND `status`='open'",
            cancellationToken, newStatus, now, entityId, (reference ?? string.Empty).Trim()).ConfigureAwait(false);
        return n > 0
            ? new(true, "Instrument " + reference + " → " + newStatus + ".", reference ?? "", newStatus)
            : new(false, "No open instrument " + reference + " on " + entityCode + ".", reference ?? "", "");
    }

    public async Task<ErpShamTreasuryListResult> ListAsync(string entityCode, string? status, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, null, "treasury-view", cancellationToken).ConfigureAwait(false))
        {
            return new(false, "treasury-view grant required for this entity.", 0m, null);
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return new(false, "Unknown entity code.", 0m, null);
        }
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var list = new List<ErpShamTreasuryInstrument>();
        decimal exposure = 0m;
        await using (var cmd = conn.CreateCommand())
        {
            var statusFilter = string.IsNullOrWhiteSpace(status) ? "" : " AND `status`='" + status!.Trim().ToLowerInvariant().Replace("'", "''") + "'";
            cmd.CommandText = ErpDb.Positional(
                "SELECT `reference`,`kind`,`counterparty`,`amount`,`currency_code`,`status`,`opened`,`expiry`,`settled`"
                + " FROM `epc_erp_org_treasury_instruments` WHERE `entity_id`=?" + statusFilter + " ORDER BY `id` DESC LIMIT 200");
            ErpDb.AddParameters(cmd, entityId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var st = reader.GetString(5);
                if (st == "open") exposure += reader.GetDecimal(3);
                list.Add(new ErpShamTreasuryInstrument(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetDecimal(3), reader.GetString(4), st, reader.GetInt64(6), reader.GetInt64(7), reader.GetInt64(8)));
            }
        }
        return new(true, list.Count + " instrument(s); open exposure " + exposure.ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + ".", exposure, list);
    }

    private async Task<int> EntityIdAsync(string entityCode, CancellationToken cancellationToken)
    {
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (int)await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1",
            cancellationToken, entityCode.Trim()).ConfigureAwait(false);
    }
}
