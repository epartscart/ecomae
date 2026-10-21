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
    /// <summary>Append a new MSP version. When <paramref name="itemCode"/> is set the version
    /// applies to that item only; empty means the entity×BU default. New versions land
    /// <c>pending</c> and only take effect after <see cref="ApproveAsync"/>.</summary>
    Task<ErpShamMspResult> SetAsync(string entityCode, string? buCode, decimal unit, decimal offer, decimal actual, decimal msp, string userEmail, string? itemCode = null, string? note = null, CancellationToken cancellationToken = default);
    /// <summary>Approve or reject a pending version ('msp-approve' grant). Approved versions
    /// supersede older active ones for the same item/scope.</summary>
    Task<ErpShamMspResult> ApproveAsync(string entityCode, string? buCode, string? itemCode, int version, bool approve, string? note, string userEmail, CancellationToken cancellationToken = default);
    /// <summary>Effective version = latest 'active' for the item (falling back to the scope
    /// default) plus the full version/approval history.</summary>
    Task<ErpShamMspResult> CheckAsync(string entityCode, string? buCode, string userEmail, string? itemCode = null, CancellationToken cancellationToken = default);
}

public sealed record ErpShamMspVersion(int Version, string ItemCode, decimal Unit, decimal Offer, decimal Actual, decimal Msp, string Status, string SetBy, string ApprovedBy, long TimeCreated, long TimeApproved);
public sealed record ErpShamMspResult(bool Ok, string Message, string Entity, string Bu, string Item, int Version, decimal Unit, decimal Offer, decimal Actual, decimal Msp, int Pending, IReadOnlyList<ErpShamMspVersion>? History);

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
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_msp_versions` (`id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `entity_id` int(11) NOT NULL DEFAULT 0, `bu_code` varchar(40) NOT NULL DEFAULT '',"
            + " `item_code` varchar(64) NOT NULL DEFAULT '',"
            + " `version` int(11) NOT NULL DEFAULT 1, `unit_margin` decimal(8,2) NOT NULL DEFAULT 0,"
            + " `offer_margin` decimal(8,2) NOT NULL DEFAULT 0, `actual_margin` decimal(8,2) NOT NULL DEFAULT 0,"
            + " `msp_margin` decimal(8,2) NOT NULL DEFAULT 0, `status` varchar(12) NOT NULL DEFAULT 'active',"
            + " `set_by` varchar(190) NOT NULL DEFAULT '', `approved_by` varchar(190) NOT NULL DEFAULT '',"
            + " `note` varchar(255) NOT NULL DEFAULT '',"
            + " `time_created` int(11) NOT NULL DEFAULT 0, `time_approved` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_bu_item_ver` (`entity_id`,`bu_code`,`item_code`,`version`), KEY `x_entity` (`entity_id`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Versioned MSP margin structure per entity×BU×item (§8)'",
            cancellationToken).ConfigureAwait(false);
        // In-place upgrades for tenants that already have the table.
        foreach (var ddl in new[]
        {
            "ALTER TABLE `epc_erp_org_msp_versions` ADD COLUMN `item_code` varchar(64) NOT NULL DEFAULT '' AFTER `bu_code`",
            "ALTER TABLE `epc_erp_org_msp_versions` ADD COLUMN `status` varchar(12) NOT NULL DEFAULT 'active' AFTER `msp_margin`",
            "ALTER TABLE `epc_erp_org_msp_versions` ADD COLUMN `approved_by` varchar(190) NOT NULL DEFAULT '' AFTER `set_by`",
            "ALTER TABLE `epc_erp_org_msp_versions` ADD COLUMN `note` varchar(255) NOT NULL DEFAULT '' AFTER `approved_by`",
            "ALTER TABLE `epc_erp_org_msp_versions` ADD COLUMN `time_approved` int(11) NOT NULL DEFAULT 0 AFTER `time_created`",
            // The original (entity_id,bu_code,version) unique key reserves v1 for
            // the whole BU — per-item versioning needs the item in the key.
            "ALTER TABLE `epc_erp_org_msp_versions` DROP INDEX `ux_entity_bu_ver`",
            "ALTER TABLE `epc_erp_org_msp_versions` ADD UNIQUE KEY `ux_entity_bu_item_ver` (`entity_id`,`bu_code`,`item_code`,`version`)",
        })
        {
            await ErpDb.TryExecuteAsync(connection, ddl, cancellationToken).ConfigureAwait(false);
        }

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_msp_approvals` (`id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `version_id` int(11) NOT NULL DEFAULT 0, `action` varchar(12) NOT NULL DEFAULT '',"
            + " `actor` varchar(190) NOT NULL DEFAULT '', `note` varchar(255) NOT NULL DEFAULT '',"
            + " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_version` (`version_id`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='MSP version approval log (§8)'",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpShamMspResult> SetAsync(string entityCode, string? buCode, decimal unit, decimal offer, decimal actual, decimal msp, string userEmail, string? itemCode = null, string? note = null, CancellationToken cancellationToken = default)
    {
        var item = (itemCode ?? string.Empty).Trim();
        if (unit < 0 || offer < 0 || actual < 0 || msp < 0 || unit > 100 || offer > 100 || actual > 100 || msp > 100)
        {
            return Fail("Margins must be percentages between 0 and 100.", entityCode, buCode, item);
        }
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "msp-set", cancellationToken).ConfigureAwait(false))
        {
            return Fail("msp-set grant required for this entity×BU scope.", entityCode, buCode, item);
        }

        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return Fail("Unknown entity code.", entityCode, buCode, item);
        }
        var bu = await _foundation.NormalizeBuCodeAsync(entityId, buCode ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var version = Convert.ToInt32(await ErpDb.LongAsync(conn, tx,
            "SELECT COALESCE(MAX(`version`),0)+1 FROM `epc_erp_org_msp_versions` WHERE `entity_id`=? AND `bu_code`=? AND `item_code`=?",
            cancellationToken, entityId, bu, item).ConfigureAwait(false));
        await ErpDb.ExecuteAsync(conn, tx,
            "INSERT INTO `epc_erp_org_msp_versions` (`entity_id`,`bu_code`,`item_code`,`version`,`unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`status`,`set_by`,`note`,`time_created`) VALUES (?,?,?,?,?,?,?,?,'pending',?,?,?)",
            cancellationToken, entityId, bu, item, version, unit, offer, actual, msp, userEmail, note ?? string.Empty, now).ConfigureAwait(false);
        var versionId = await ErpDb.LongAsync(conn, tx, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(conn, tx,
            "INSERT INTO `epc_erp_org_msp_approvals` (`version_id`,`action`,`actor`,`note`,`time_created`) VALUES (?,?,?,?,?)",
            cancellationToken, versionId, "requested", userEmail, note ?? string.Empty, now).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        var scope = item.Length > 0 ? $"item {item}" : "scope default";
        return new ErpShamMspResult(true, $"MSP v{version} submitted for approval ({scope}).", entityCode, bu, item, version, unit, offer, actual, msp, 1, null);
    }

    public async Task<ErpShamMspResult> ApproveAsync(string entityCode, string? buCode, string? itemCode, int version, bool approve, string? note, string userEmail, CancellationToken cancellationToken = default)
    {
        var item = (itemCode ?? string.Empty).Trim();
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "msp-approve", cancellationToken).ConfigureAwait(false))
        {
            return Fail("msp-approve grant required for this entity×BU scope.", entityCode, buCode, item);
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return Fail("Unknown entity code.", entityCode, buCode, item);
        }
        var bu = await _foundation.NormalizeBuCodeAsync(entityId, buCode ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        long versionId = 0; string status;
        decimal unit = 0, offer = 0, actual = 0, msp = 0;
        await using (var find = conn.CreateCommand())
        {
            find.Transaction = (System.Data.Common.DbTransaction)tx;
            find.CommandText = ErpDb.Positional(
                "SELECT `id`,`status`,`unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`"
                + " FROM `epc_erp_org_msp_versions` WHERE `entity_id`=? AND `bu_code`=? AND `item_code`=? AND `version`=? LIMIT 1");
            ErpDb.AddParameters(find, entityId, bu, item, version);
            await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Fail("MSP version not found for this scope.", entityCode, bu, item);
            }
            versionId = reader.GetInt64(0);
            status = reader.GetString(1);
            unit = reader.GetDecimal(2); offer = reader.GetDecimal(3); actual = reader.GetDecimal(4); msp = reader.GetDecimal(5);
        }
        if (status != "pending")
        {
            return Fail($"MSP v{version} is already {status} — only pending versions can be decided.", entityCode, bu, item);
        }

        var next = approve ? "active" : "rejected";
        await ErpDb.ExecuteAsync(conn, tx,
            "UPDATE `epc_erp_org_msp_versions` SET `status`=?, `approved_by`=?, `time_approved`=? WHERE `id`=?",
            cancellationToken, next, userEmail, approve ? now : 0, versionId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(conn, tx,
            "INSERT INTO `epc_erp_org_msp_approvals` (`version_id`,`action`,`actor`,`note`,`time_created`) VALUES (?,?,?,?,?)",
            cancellationToken, versionId, next, userEmail, note ?? string.Empty, now).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ErpShamMspResult(true, $"MSP v{version} {next}.", entityCode, bu, item, version, unit, offer, actual, msp, 0, null);
    }

    public async Task<ErpShamMspResult> CheckAsync(string entityCode, string? buCode, string userEmail, string? itemCode = null, CancellationToken cancellationToken = default)
    {
        var item = (itemCode ?? string.Empty).Trim();
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "msp-view", cancellationToken).ConfigureAwait(false))
        {
            return Fail("msp-view grant required for this entity×BU scope.", entityCode, buCode, item);
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return Fail("Unknown entity code.", entityCode, buCode, item);
        }
        var bu = await _foundation.NormalizeBuCodeAsync(entityId, buCode ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var history = new List<ErpShamMspVersion>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "(SELECT `version`,`item_code`,`unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`status`,`set_by`,`approved_by`,`time_created`,`time_approved`"
                + " FROM `epc_erp_org_msp_versions` WHERE `entity_id`=? AND `bu_code`=? AND `item_code`=? ORDER BY `version` DESC LIMIT 50)"
                + " UNION ALL"
                + " (SELECT `version`,`item_code`,`unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`status`,`set_by`,`approved_by`,`time_created`,`time_approved`"
                + " FROM `epc_erp_org_msp_versions` WHERE `entity_id`=? AND `bu_code`=? AND `item_code`='' ORDER BY `version` DESC LIMIT 5)"
                + " ORDER BY (`item_code` = ?) DESC, `version` DESC");
            ErpDb.AddParameters(cmd, entityId, bu, item, entityId, bu, item);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                history.Add(new ErpShamMspVersion(
                    reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3),
                    reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6),
                    reader.GetString(7), reader.GetString(8), reader.GetInt64(9), reader.GetInt64(10)));
            }
        }
        if (history.Count == 0)
        {
            return new ErpShamMspResult(true, "No MSP versions yet for this scope.", entityCode, bu, item, 0, 0, 0, 0, 0, 0, history);
        }
        // Effective = newest ACTIVE row; an item-specific active row wins over the
        // scope default regardless of version number (item rules override scope).
        var effective = history.Where(v => v.ItemCode == item).Concat(history).FirstOrDefault(v => v.Status == "active");
        var pending = history.Count(v => v.Status == "pending");
        if (effective is null)
        {
            return new ErpShamMspResult(true, pending > 0 ? $"{pending} version(s) awaiting approval — none effective yet." : "No active MSP version for this scope.", entityCode, bu, item, 0, 0, 0, 0, 0, pending, history);
        }
        var note = effective.ItemCode.Length > 0 ? $"item {effective.ItemCode}" : "scope default";
        return new ErpShamMspResult(true, $"Effective MSP v{effective.Version} ({note}).", entityCode, bu, item, effective.Version, effective.Unit, effective.Offer, effective.Actual, effective.Msp, pending, history);
    }

    private static ErpShamMspResult Fail(string message, string entityCode, string? buCode, string item)
        => new(false, message, entityCode, buCode ?? "", item, 0, 0, 0, 0, 0, 0, null);

    private async Task<int> EntityIdAsync(string entityCode, CancellationToken cancellationToken)
    {
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (int)await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1",
            cancellationToken, entityCode.Trim()).ConfigureAwait(false);
    }
}
