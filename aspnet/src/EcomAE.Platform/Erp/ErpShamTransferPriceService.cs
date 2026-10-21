using MySqlConnector;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM blueprint §10: effective-dated transfer pricing rules between two
/// legal entities. Each rule is a row: selling entity → buying entity,
/// optional item SKU (blank = entity-pair default), rule kind
/// (fixed_price | markup_pct | margin_floor_pct), value, currency,
/// agreement reference, effective dates. Rules never mutate — setting a
/// rule for an existing pair+SKU appends a new version so historical
/// intercompany documents stay reproducible.
/// Table <c>epc_erp_org_transfer_price_rules</c> is tenant-generic.
/// Grants: <c>transfer-price-set</c> / <c>transfer-price-view</c>.
/// </summary>
public interface IErpShamTransferPriceService
{
    Task<ErpShamTransferPriceResult> SetAsync(string fromEntityCode, string toEntityCode, string sku, string kind, decimal value, string currency, string agreementRef, long effectiveFrom, long effectiveTo, string userEmail, CancellationToken cancellationToken = default);
    Task<ErpShamTransferPriceResult> ResolveAsync(string fromEntityCode, string toEntityCode, string sku, string userEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErpShamTransferPriceRule>> ListAsync(string userEmail, CancellationToken cancellationToken = default);
}

public sealed record ErpShamTransferPriceRule(int Version, string FromEntity, string ToEntity, string Sku, string Kind, decimal Value, string Currency, string AgreementRef, long EffectiveFrom, long EffectiveTo, string SetBy, long TimeCreated);
public sealed record ErpShamTransferPriceResult(bool Ok, string Message, ErpShamTransferPriceRule? Rule, IReadOnlyList<ErpShamTransferPriceRule>? Rules);

public sealed class ErpShamTransferPriceService : IErpShamTransferPriceService
{
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase) { "fixed_price", "markup_pct", "margin_floor_pct" };
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    public ErpShamTransferPriceService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public static async Task EnsureSchemaAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_transfer_price_rules` (`id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `from_entity_id` int(11) NOT NULL DEFAULT 0, `to_entity_id` int(11) NOT NULL DEFAULT 0,"
            + " `sku` varchar(64) NOT NULL DEFAULT '', `kind` varchar(24) NOT NULL DEFAULT 'fixed_price',"
            + " `rule_value` decimal(14,4) NOT NULL DEFAULT 0, `currency` varchar(8) NOT NULL DEFAULT 'AED',"
            + " `agreement_ref` varchar(80) NOT NULL DEFAULT '', `effective_from` int(11) NOT NULL DEFAULT 0,"
            + " `effective_to` int(11) NOT NULL DEFAULT 0, `version` int(11) NOT NULL DEFAULT 1,"
            + " `set_by` varchar(190) NOT NULL DEFAULT '', `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `ux_rule_ver` (`from_entity_id`,`to_entity_id`,`sku`,`kind`,`version`),"
            + " KEY `x_pair` (`from_entity_id`,`to_entity_id`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Effective-dated intercompany transfer price rules (§10)'",
            cancellationToken).ConfigureAwait(false);

    public async Task<ErpShamTransferPriceResult> SetAsync(string fromEntityCode, string toEntityCode, string sku, string kind, decimal value, string currency, string agreementRef, long effectiveFrom, long effectiveTo, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!Kinds.Contains(kind))
        {
            return new ErpShamTransferPriceResult(false, "kind must be fixed_price, markup_pct or margin_floor_pct.", null, null);
        }
        if (value < 0 || (kind != "fixed_price" && value > 1000))
        {
            return new ErpShamTransferPriceResult(false, "value must be non-negative (pct rules ≤ 1000).", null, null);
        }
        // Pricing affects BOTH sides of the pair: the grant must cover the
        // selling entity AND the buying entity, else a seller-scoped admin
        // could rewrite a buyer's inbound prices.
        if (!await _foundation.AccessAllowedAsync(userEmail, fromEntityCode, null, "transfer-price-set", cancellationToken).ConfigureAwait(false)
            || !await _foundation.AccessAllowedAsync(userEmail, toEntityCode, null, "transfer-price-set", cancellationToken).ConfigureAwait(false))
        {
            return new ErpShamTransferPriceResult(false, "transfer-price-set grant required on BOTH the selling and the buying entity.", null, null);
        }

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var fromId = await EntityIdAsync(conn, fromEntityCode, cancellationToken).ConfigureAwait(false);
        var toId = await EntityIdAsync(conn, toEntityCode, cancellationToken).ConfigureAwait(false);
        if (fromId <= 0 || toId <= 0 || fromId == toId)
        {
            return new ErpShamTransferPriceResult(false, "from and to must be two different active legal entities.", null, null);
        }

        sku = (sku ?? string.Empty).Trim().ToUpperInvariant();
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // MAX+1 then insert is racy: two concurrent sets for the same key can
        // compute the same version → unique-key rejection. Retry the pair a
        // bounded number of times so a losing admin's rule still lands.
        var version = 0;
        var inserted = false;
        for (var attempt = 0; attempt < 3 && !inserted; attempt++)
        {
            version = (int)await ErpDb.LongAsync(conn, null,
                ErpDb.Positional("SELECT COALESCE(MAX(`version`),0)+1 FROM `epc_erp_org_transfer_price_rules` WHERE `from_entity_id`=? AND `to_entity_id`=? AND `sku`=? AND `kind`=?"),
                cancellationToken, fromId, toId, sku, kind).ConfigureAwait(false);
            try
            {
                await ErpDb.ExecuteAsync(conn, null,
                    ErpDb.Positional("INSERT INTO `epc_erp_org_transfer_price_rules` (`from_entity_id`,`to_entity_id`,`sku`,`kind`,`rule_value`,`currency`,`agreement_ref`,`effective_from`,`effective_to`,`version`,`set_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken, fromId, toId, sku, kind.ToLowerInvariant(), value,
                    string.IsNullOrWhiteSpace(currency) ? "AED" : currency.Trim().ToUpperInvariant(),
                    agreementRef?.Trim() ?? string.Empty, (int)Math.Max(0, effectiveFrom), (int)Math.Max(0, effectiveTo), version, userEmail, now).ConfigureAwait(false);
                inserted = true;
            }
            catch (MySqlException ex) when (ex.Number == 1062 && attempt < 2)
            {
                // Concurrent insert took the same version — recompute and retry.
            }
        }
        if (!inserted)
        {
            return new ErpShamTransferPriceResult(false, "Version contention — retry the set.", null, null);
        }
        var rule = new ErpShamTransferPriceRule(version, fromEntityCode, toEntityCode, sku, kind, value, currency, agreementRef ?? "", effectiveFrom, effectiveTo, userEmail, now);
        return new ErpShamTransferPriceResult(true, $"Transfer price rule v{version} set: {fromEntityCode} → {toEntityCode}" + (sku.Length > 0 ? $" SKU {sku}" : "") + $" {kind}={value}.", rule, null);
    }

    public async Task<ErpShamTransferPriceResult> ResolveAsync(string fromEntityCode, string toEntityCode, string sku, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, fromEntityCode, null, "transfer-price-view", cancellationToken).ConfigureAwait(false))
        {
            return new ErpShamTransferPriceResult(false, "transfer-price-view grant required for the selling entity.", null, null);
        }
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var fromId = await EntityIdAsync(conn, fromEntityCode, cancellationToken).ConfigureAwait(false);
        var toId = await EntityIdAsync(conn, toEntityCode, cancellationToken).ConfigureAwait(false);
        if (fromId <= 0 || toId <= 0)
        {
            return new ErpShamTransferPriceResult(false, "Unknown legal entity.", null, null);
        }
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        sku = (sku ?? string.Empty).Trim().ToUpperInvariant();
        // SKU rule wins over the pair default (''), then newest effective version.
        var rules = await ReadRulesAsync(conn,
            "WHERE r.`from_entity_id`=? AND r.`to_entity_id`=? AND r.`sku` IN (?, '')"
            + " AND (r.`effective_from`=0 OR r.`effective_from`<=?) AND (r.`effective_to`=0 OR r.`effective_to`>=?)"
            + " ORDER BY (r.`sku`<>'') DESC, r.`version` DESC LIMIT 1",
            cancellationToken, fromId, toId, sku, now, now).ConfigureAwait(false);
        return rules.Count == 0
            ? new ErpShamTransferPriceResult(false, "No effective transfer price rule for this pair/SKU.", null, null)
            : new ErpShamTransferPriceResult(true, "Effective rule resolved.", rules[0], null);
    }

    public async Task<IReadOnlyList<ErpShamTransferPriceRule>> ListAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rules = await ReadRulesAsync(conn, "ORDER BY r.`from_entity_id`, r.`to_entity_id`, r.`sku`, r.`version` DESC LIMIT 200", cancellationToken).ConfigureAwait(false);
        // Transfer prices are commercially sensitive: only rules whose selling
        // entity the caller has transfer-price-view on are returned.
        var visible = new List<ErpShamTransferPriceRule>(rules.Count);
        foreach (var rule in rules)
        {
            if (await _foundation.AccessAllowedAsync(userEmail, rule.FromEntity, null, "transfer-price-view", cancellationToken).ConfigureAwait(false))
            {
                visible.Add(rule);
            }
        }
        return visible;
    }

    private static async Task<IReadOnlyList<ErpShamTransferPriceRule>> ReadRulesAsync(System.Data.Common.DbConnection conn, string tail, CancellationToken ct, params object[] args)
    {
        var list = new List<ErpShamTransferPriceRule>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT r.`version`, fe.`code`, te.`code`, r.`sku`, r.`kind`, r.`rule_value`, r.`currency`, r.`agreement_ref`, r.`effective_from`, r.`effective_to`, r.`set_by`, r.`time_created`"
            + " FROM `epc_erp_org_transfer_price_rules` r"
            + " JOIN `epc_erp_pm_legal_entities` fe ON fe.`id`=r.`from_entity_id`"
            + " JOIN `epc_erp_pm_legal_entities` te ON te.`id`=r.`to_entity_id` " + tail;
        foreach (var a in args)
        {
            var p = cmd.CreateParameter();
            p.Value = a;
            cmd.Parameters.Add(p);
        }
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new ErpShamTransferPriceRule(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetString(6), reader.GetString(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetString(10), reader.GetInt32(11)));
        }
        return list;
    }

    private async Task<long> EntityIdAsync(System.Data.Common.DbConnection conn, string entityCode, CancellationToken ct)
        => await ErpDb.LongAsync(conn, null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
            ct, (entityCode ?? string.Empty).Trim().ToUpperInvariant()).ConfigureAwait(false);
}
