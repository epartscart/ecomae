using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpPricingSnapshot(
    string Source,
    ErpMarginStructure Structure,
    IReadOnlyList<ErpPriceRule> Rules);

public sealed record ErpPricingSaveResult(bool Succeeded, string Message, int Writes, string Code);

public interface IErpPricingService
{
    Task<ErpPricingSnapshot> ReadAsync(CancellationToken cancellationToken = default);

    Task<ErpPricingSaveResult> SaveAsync(ErpPricingSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record ErpPricingSaveRequest(
    string Action,
    decimal UnitPercent,
    decimal OfferPercent,
    decimal ActualPercent,
    decimal MspPercent,
    string CurrencyCode,
    string Channel,
    string CountryCode,
    string CustomerGroup,
    string CustomerCode,
    string ItemCode,
    decimal MinQty,
    decimal RuleUnitPercent,
    decimal RuleOfferPercent,
    decimal RuleActualPercent,
    decimal RuleMspPercent,
    string RuleCurrency,
    long RuleId,
    string Note);

public sealed class ErpPricingService : IErpPricingService
{
    public const string AppPath = "/erp/pricing-app";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPricingService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpPricingSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpPricingSnapshot("migration", ErpMarginStructure.Standard, []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var structure = await ReadStructureAsync(connection, cancellationToken).ConfigureAwait(false);
        var rules = await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false);
        return new ErpPricingSnapshot("live", structure, rules);
    }

    public async Task<ErpPricingSaveResult> SaveAsync(ErpPricingSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(false, "Tenant database is not configured.", 0, "db");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var action = ErpPriceMath.Clean(request.Action).ToLowerInvariant();
        if (action == "structure")
        {
            return await SaveStructureAsync(connection, request, cancellationToken).ConfigureAwait(false);
        }

        if (action == "deactivate")
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_price_rules` SET `active` = 0, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                request.RuleId).ConfigureAwait(false);
            return new(true, "Price level turned off.", 1, "ok");
        }

        return await SaveRuleAsync(connection, request, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_erp_price_structure` (
              `id` int(11) NOT NULL,
              `unit_margin` decimal(8,2) NOT NULL DEFAULT 25.00,
              `offer_margin` decimal(8,2) NOT NULL DEFAULT 12.00,
              `actual_margin` decimal(8,2) NOT NULL DEFAULT 18.00,
              `msp_margin` decimal(8,2) NOT NULL DEFAULT 8.00,
              `currency_code` char(3) NOT NULL DEFAULT 'AED',
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_erp_price_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `channel` varchar(16) NOT NULL DEFAULT 'any',
              `country_code` varchar(4) NOT NULL DEFAULT '',
              `customer_group` varchar(40) NOT NULL DEFAULT '',
              `customer_code` varchar(40) NOT NULL DEFAULT '',
              `item_code` varchar(40) NOT NULL DEFAULT '',
              `min_qty` decimal(14,3) NOT NULL DEFAULT 0.000,
              `currency_code` char(3) NOT NULL DEFAULT '',
              `unit_margin` decimal(8,2) NOT NULL DEFAULT -1.00,
              `offer_margin` decimal(8,2) NOT NULL DEFAULT -1.00,
              `actual_margin` decimal(8,2) NOT NULL DEFAULT -1.00,
              `msp_margin` decimal(8,2) NOT NULL DEFAULT -1.00,
              `active` tinyint(1) NOT NULL DEFAULT 1,
              `note` varchar(190) NOT NULL DEFAULT '',
              `time_updated` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "INSERT IGNORE INTO `epc_erp_price_structure` (`id`,`unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`currency_code`) VALUES (1,25.00,12.00,18.00,8.00,'AED')",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ErpPricingSaveResult> SaveStructureAsync(
        DbConnection connection,
        ErpPricingSaveRequest request,
        CancellationToken cancellationToken)
    {
        var currency = ErpPriceMath.Clean(request.CurrencyCode).ToUpperInvariant();
        if (currency.Length != 3)
        {
            return new(false, "Currency is three letters, such as AED.", 0, "currency");
        }

        var check = CheckMargins(request.UnitPercent, request.OfferPercent, request.ActualPercent, request.MspPercent, inheritAllowed: false);
        if (check is not null)
        {
            return check;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_price_structure` SET `unit_margin` = ?, `offer_margin` = ?, `actual_margin` = ?, `msp_margin` = ?, `currency_code` = ? WHERE `id` = 1"),
            cancellationToken,
            request.UnitPercent,
            request.OfferPercent,
            request.ActualPercent,
            request.MspPercent,
            currency).ConfigureAwait(false);
        return new(true, "Margin structure saved.", 1, "ok");
    }

    private static async Task<ErpPricingSaveResult> SaveRuleAsync(
        DbConnection connection,
        ErpPricingSaveRequest request,
        CancellationToken cancellationToken)
    {
        var channel = ErpPriceMath.Clean(request.Channel).ToLowerInvariant();
        if (channel is not ("any" or "wholesale" or "retail"))
        {
            return new(false, "Choose wholesale, retail, or any channel.", 0, "channel");
        }

        var country = ErpPriceMath.Clean(request.CountryCode).ToUpperInvariant();
        if (country.Length > 4)
        {
            return new(false, "Country is a short code, such as AE.", 0, "country");
        }

        var currency = ErpPriceMath.Clean(request.RuleCurrency).ToUpperInvariant();
        if (currency.Length > 0 && currency.Length != 3)
        {
            return new(false, "Currency is three letters, or leave it blank.", 0, "currency");
        }

        if (request.MinQty < 0m)
        {
            return new(false, "Quantity starts at zero.", 0, "qty");
        }

        var check = CheckMargins(request.RuleUnitPercent, request.RuleOfferPercent, request.RuleActualPercent, request.RuleMspPercent, inheritAllowed: true);
        if (check is not null)
        {
            return check;
        }

        if (channel == "any"
            && country.Length == 0
            && ErpPriceMath.Clean(request.CustomerGroup).Length == 0
            && ErpPriceMath.Clean(request.CustomerCode).Length == 0
            && ErpPriceMath.Clean(request.ItemCode).Length == 0
            && request.MinQty <= 0m
            && currency.Length == 0)
        {
            return new(false, "Name a channel, country, customer group, customer, item, or quantity. The company structure already covers every sale.", 0, "level");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_price_rules` (`channel`,`country_code`,`customer_group`,`customer_code`,`item_code`,`min_qty`,`currency_code`,"
                + " `unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`active`,`note`,`time_updated`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,1,?,?)"),
            cancellationToken,
            channel,
            country,
            Clip(request.CustomerGroup, 40),
            Clip(request.CustomerCode, 40),
            Clip(request.ItemCode, 40),
            request.MinQty,
            currency,
            request.RuleUnitPercent,
            request.RuleOfferPercent,
            request.RuleActualPercent,
            request.RuleMspPercent,
            Clip(request.Note, 190),
            now).ConfigureAwait(false);
        return new(true, "Price level saved.", 1, "ok");
    }

    private static ErpPricingSaveResult? CheckMargins(decimal unit, decimal offer, decimal actual, decimal msp, bool inheritAllowed)
    {
        foreach (var (name, value) in new[] { ("Unit", unit), ("Offer", offer), ("Actual", actual), ("MSP", msp) })
        {
            if (inheritAllowed && value < 0m)
            {
                continue;
            }

            if (value < 0m || value > 1000m)
            {
                return new(false, name + " margin is a percent from 0 to 1000.", 0, "margin");
            }
        }

        var floor = msp < 0m ? 0m : msp;
        foreach (var (name, value) in new[] { ("Unit", unit), ("Offer", offer), ("Actual", actual) })
        {
            if (value >= 0m && value < floor)
            {
                return new(false, name + " margin cannot sit under the minimum selling margin.", 0, "margin");
            }
        }

        return null;
    }

    private static async Task<ErpMarginStructure> ReadStructureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`currency_code` FROM `epc_erp_price_structure` WHERE `id` = 1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ErpMarginStructure.Standard;
        }

        return new ErpMarginStructure(
            reader.GetDecimal(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant() ?? "AED");
    }

    private static async Task<List<ErpPriceRule>> ReadRulesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ErpPriceRule>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `id`,`channel`,`country_code`,`customer_group`,`customer_code`,`item_code`,`min_qty`,`currency_code`,
                   `unit_margin`,`offer_margin`,`actual_margin`,`msp_margin`,`active`,`note`
            FROM `epc_erp_price_rules`
            WHERE `active` = 1
            ORDER BY `id` DESC
            LIMIT 200
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpPriceRule(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Text(reader, 1).ToLowerInvariant(),
                Text(reader, 2).ToUpperInvariant(),
                Text(reader, 3),
                Text(reader, 4),
                Text(reader, 5),
                reader.GetDecimal(6),
                Text(reader, 7).ToUpperInvariant(),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                reader.GetDecimal(11),
                Convert.ToInt32(reader.GetValue(12), CultureInfo.InvariantCulture) != 0,
                Text(reader, 13)));
        }

        return rows;
    }

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static string Clip(string? value, int max)
    {
        var text = ErpPriceMath.Clean(value);
        return text.Length <= max ? text : text[..max];
    }
}
