using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM blueprint §8: versioned margin-structure pricing (MSP) per legal
/// entity × business unit. Each set appends a new version row — never
/// mutates history — so price policy changes stay auditable and the
/// effective margins at any point are reproducible. msp-check returns
/// the effective version plus the version history.
///
/// Table <c>epc_erp_org_msp_versions</c> is tenant-generic (any ERP-only
/// tenant's entities); grants: <c>msp-set</c> / <c>msp-view</c> via
/// <see cref="IErpShamFoundationService.AccessAllowedAsync"/>.
/// </summary>
public interface IErpShamMspService
{
    Task<ErpShamMspResult> SetAsync(string entityCode, string? buCode, decimal unit, decimal offer, decimal actual, decimal msp, string userEmail, CancellationToken cancellationToken = default);
    Task<ErpShamMspResult> CheckAsync(string entityCode, string? buCode, string userEmail, CancellationToken cancellationToken = default);
}

public sealed record ErpShamMspVersion(int Version, decimal Unit, decimal Offer, decimal Actual, decimal Msp, string SetBy, long TimeCreated);
public sealed record ErpShamMspResult(bool Ok, string Message, string Entity, string Bu, int Version, decimal Unit, decimal Offer, decimal Actual, decimal Msp, IReadOnlyList<ErpShamMspVersion>? History);

public sealed class ErpShamMspService : IErpShamMspService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    public ErpShamMspService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public static async Task EnsureSchemaAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_msp_versions` (`id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `entity_id` int(11) NOT NULL DEFAULT 0, `bu_code` varchar(40) NOT NULL DEFAULT '',"
            + " `version` int(11) NOT NULL DEFAULT 1, `unit_margin` decimal(8,2) NOT NULL DEFAULT 0,"
            + " `offer_margin` decimal(8,2) NOT NULL DEFAULT 0, `actual_margin` decimal(8,2) NOT NULL DEFAULT 0,"
            + " `msp_margin` decimal(8,2) NOT NULL DEFAULT 0, `set_by` varchar(190) NOT NULL DEFAULT '',"
            + " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`),"
            + " UNIQUE KEY `ux_entity_bu_ver` (`entity_id`,`bu_code`,`version`), KEY `x_entity` (`entity_id`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Versioned MSP margin structure per entity×BU (§8)'",
            cancellationToken).ConfigureAwait(false);

    public async Task<ErpShamMspResult> SetAsync(string entityCode, string? buCode, decimal unit, decimal offer, decimal actual, decimal msp, string userEmail, CancellationToken cancellationToken = default)
    {
        if (unit < 0 || offer < 0 || actual < 0 || msp < 0 || unit > 100 || offer > 100 || actual > 100 || msp > 100)
        {
            return new ErpShamMspResult(false, "Margins must be percentages between 0 and 100.", entityCode, buCode ?? "", 0, 0, 0, 0, 0, null);
        }
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "msp-set", cancellationToken).ConfigureAwait(false))
        {
            return new ErpShamMspResult(false, "msp-set grant required for this entity×BU scope.", entityCode, buCode ?? "", 0, 0, 0, 0, 0, null);
        }

        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return new ErpShamMspResult(false, "Unknown entity code.", entityCode, buCode ?? "", 0, 0, 0, 0, 0, null);
        }
        var bu = await _foundation.NormalizeBuCodeAsync(entityId, buCode ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var version = Convert.ToInt32(await ErpDb.LongAsync(conn, tx,
            "SELECT COALESCE(MAX(`version`),0)+1 FROM `epc_erp_org_msp_versions` WHERE `entity_id`=? AND `bu_code`=?",
            cancellationToken, entityId, bu).ConfigureAwait(false));
        await ErpDb.ExecuteAsync(conn, tx,
            "INSERT INTO `epc_erp_org_msp_versions` (`entity_id`,`bu_code`,`version`,`unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`set_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)",
            cancellationToken, entityId, bu, version, unit, offer, actual, msp, userEmail, now).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ErpShamMspResult(true, $"MSP v{version} set.", entityCode, bu, version, unit, offer, actual, msp, null);
    }

    public async Task<ErpShamMspResult> CheckAsync(string entityCode, string? buCode, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "msp-view", cancellationToken).ConfigureAwait(false))
        {
            return new ErpShamMspResult(false, "msp-view grant required for this entity×BU scope.", entityCode, buCode ?? "", 0, 0, 0, 0, 0, null);
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return new ErpShamMspResult(false, "Unknown entity code.", entityCode, buCode ?? "", 0, 0, 0, 0, 0, null);
        }
        var bu = await _foundation.NormalizeBuCodeAsync(entityId, buCode ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var history = new List<ErpShamMspVersion>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT `version`,`unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`set_by`,`time_created`"
                + " FROM `epc_erp_org_msp_versions` WHERE `entity_id`=? AND `bu_code`=? ORDER BY `version` DESC LIMIT 50");
            ErpDb.AddParameters(cmd, entityId, bu);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                history.Add(new ErpShamMspVersion(
                    reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3),
                    reader.GetDecimal(4), reader.GetString(5), reader.GetInt64(6)));
            }
        }
        if (history.Count == 0)
        {
            return new ErpShamMspResult(true, "No MSP versions yet for this scope.", entityCode, bu, 0, 0, 0, 0, 0, history);
        }
        var latest = history[0];
        return new ErpShamMspResult(true, $"Effective MSP v{latest.Version}.", entityCode, bu, latest.Version, latest.Unit, latest.Offer, latest.Actual, latest.Msp, history);
    }

    private async Task<int> EntityIdAsync(string entityCode, CancellationToken cancellationToken)
    {
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (int)await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1",
            cancellationToken, entityCode.Trim()).ConfigureAwait(false);
    }
}
