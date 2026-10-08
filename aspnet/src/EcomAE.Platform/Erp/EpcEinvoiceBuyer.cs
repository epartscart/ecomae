using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>The fields PHP <c>epc_einvoice_save_buyer_profile()</c> reads; a null field is a missing array key.</summary>
public sealed record EpcEinvoiceBuyerInput
{
    public long UserId { get; init; }
    public string? BuyerName { get; init; }
    public string? Trn { get; init; }
    public string? Tin { get; init; }
    public string? LegalRegNo { get; init; }
    public string? LegalRegType { get; init; }
    public string? AuthorityName { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? Emirate { get; init; }
    public string? CountryCode { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? PeppolEndpoint { get; init; }
    public string? ElectronicId { get; init; }
    public bool BuyerOnboarded { get; init; }
}

/// <summary>
/// PHP <c>content/shop/finance/epc_einvoice_schema.php</c> and the buyer half of <c>epc_einvoice.php</c>: the e-invoice
/// tables with their default seller settings, the TIN and Peppol endpoint of a TRN, and the buyer profile read and save.
/// </summary>
public static class EpcEinvoiceBuyer
{
    public const string ElectronicScheme = "0235";

    private static readonly (string Key, string Value)[] DefaultSettings =
    [
        ("seller_name", "ePartsCart LLC"), ("seller_trn", ""), ("seller_tin", ""), ("seller_legal_reg_no", ""), ("seller_legal_reg_type", "TL"),
        ("seller_authority_name", "Dubai Economy and Tourism"), ("seller_address_line1", ""), ("seller_city", "Dubai"), ("seller_emirate", "Dubai"),
        ("seller_country_code", "AE"), ("seller_phone", ""), ("seller_email", ""), ("seller_bank_account", ""), ("payment_means_code", "30"),
        ("payment_terms", "Within 7 days"), ("asp_name", ""), ("asp_api_mode", "manual"), ("asp_api_url", ""), ("asp_api_key", ""),
        ("einvoice_enabled", "1"), ("default_doc_category", "tax_invoice"), ("default_payment_due_days", "7"), ("auto_validate", "1"),
    ];

    private static readonly string[] SchemaStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS `epc_einvoice_settings` (
        	`setting_key` varchar(64) NOT NULL,
        	`setting_value` text,
        	`time_updated` int(11) NOT NULL DEFAULT 0,
        	PRIMARY KEY (`setting_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE e-invoice seller & ASP settings'
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_einvoice_buyer_profiles` (
        	`user_id` int(11) NOT NULL,
        	`buyer_name` varchar(255) DEFAULT NULL,
        	`trn` varchar(20) DEFAULT NULL,
        	`tin` varchar(10) DEFAULT NULL,
        	`legal_reg_no` varchar(64) DEFAULT NULL,
        	`legal_reg_type` enum('TL','EID','PAS','CD') NOT NULL DEFAULT 'TL',
        	`authority_name` varchar(255) DEFAULT NULL,
        	`address_line1` varchar(255) DEFAULT NULL,
        	`city` varchar(128) DEFAULT NULL,
        	`emirate` varchar(64) DEFAULT NULL,
        	`country_code` varchar(8) NOT NULL DEFAULT 'AE',
        	`phone` varchar(32) DEFAULT NULL,
        	`email` varchar(255) DEFAULT NULL,
        	`electronic_id` varchar(8) NOT NULL DEFAULT '0235',
        	`peppol_endpoint` varchar(32) DEFAULT NULL,
        	`buyer_onboarded` tinyint(1) NOT NULL DEFAULT 0,
        	`time_updated` int(11) NOT NULL DEFAULT 0,
        	PRIMARY KEY (`user_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Buyer Peppol / TRN profile for e-invoicing'
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_einvoice_documents` (
        	`id` int(11) NOT NULL AUTO_INCREMENT,
        	`uuid` char(36) NOT NULL,
        	`invoice_number` varchar(64) NOT NULL,
        	`order_id` int(11) NOT NULL DEFAULT 0,
        	`user_id` int(11) NOT NULL DEFAULT 0,
        	`doc_category` enum('tax_invoice','tax_credit_note','commercial_invoice','credit_note') NOT NULL DEFAULT 'tax_invoice',
        	`invoice_type_code` varchar(8) NOT NULL DEFAULT '380',
        	`issue_date` int(11) NOT NULL DEFAULT 0,
        	`payment_due_date` int(11) NOT NULL DEFAULT 0,
        	`vat_point_date` int(11) NOT NULL DEFAULT 0,
        	`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
        	`vat_currency_code` varchar(8) NOT NULL DEFAULT 'AED',
        	`transaction_type_code` char(8) NOT NULL DEFAULT '00000000',
        	`payment_means_code` varchar(8) NOT NULL DEFAULT '30',
        	`payment_terms` varchar(255) DEFAULT NULL,
        	`bank_account` varchar(64) DEFAULT NULL,
        	`billing_period_start` int(11) NOT NULL DEFAULT 0,
        	`billing_period_end` int(11) NOT NULL DEFAULT 0,
        	`seller_json` mediumtext,
        	`buyer_json` mediumtext,
        	`subtotal_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`total_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`total_incl_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`paid_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`rounding_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`amount_due` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`tax_breakdown_json` text,
        	`status` enum('draft','validated','queued','submitted','accepted','rejected','cancelled') NOT NULL DEFAULT 'draft',
        	`validation_ok` tinyint(1) NOT NULL DEFAULT 0,
        	`validation_errors_json` text,
        	`xml_content` mediumtext,
        	`asp_name` varchar(128) DEFAULT NULL,
        	`asp_reference` varchar(128) DEFAULT NULL,
        	`fta_report_status` varchar(32) DEFAULT NULL,
        	`time_created` int(11) NOT NULL DEFAULT 0,
        	`time_updated` int(11) NOT NULL DEFAULT 0,
        	`time_submitted` int(11) NOT NULL DEFAULT 0,
        	`admin_id` int(11) NOT NULL DEFAULT 0,
        	`active` tinyint(1) NOT NULL DEFAULT 1,
        	PRIMARY KEY (`id`),
        	UNIQUE KEY `x_uuid` (`uuid`),
        	UNIQUE KEY `x_invoice_no` (`invoice_number`),
        	KEY `x_order` (`order_id`),
        	KEY `x_status` (`status`,`issue_date`),
        	KEY `x_user` (`user_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE electronic invoice documents'
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_einvoice_lines` (
        	`id` int(11) NOT NULL AUTO_INCREMENT,
        	`document_id` int(11) NOT NULL,
        	`line_no` int(11) NOT NULL DEFAULT 1,
        	`item_name` varchar(255) NOT NULL,
        	`item_description` varchar(512) DEFAULT NULL,
        	`item_type` enum('G','S','B') NOT NULL DEFAULT 'G',
        	`quantity` decimal(14,4) NOT NULL DEFAULT 0.0000,
        	`uom_code` varchar(16) NOT NULL DEFAULT 'C62',
        	`unit_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
        	`line_net` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`tax_category` varchar(8) NOT NULL DEFAULT 'S',
        	`tax_rate` decimal(5,2) NOT NULL DEFAULT 5.00,
        	`tax_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`gross_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`vat_line_aed` decimal(14,2) NOT NULL DEFAULT 0.00,
        	`line_amount_aed` decimal(14,2) NOT NULL DEFAULT 0.00,
        	PRIMARY KEY (`id`),
        	KEY `x_doc` (`document_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE e-invoice line items'
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_einvoice_events` (
        	`id` int(11) NOT NULL AUTO_INCREMENT,
        	`document_id` int(11) NOT NULL,
        	`event_type` varchar(32) NOT NULL,
        	`status` varchar(32) NOT NULL DEFAULT 'info',
        	`message` text,
        	`payload_json` mediumtext,
        	`time_created` int(11) NOT NULL DEFAULT 0,
        	PRIMARY KEY (`id`),
        	KEY `x_doc` (`document_id`,`time_created`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='E-invoice transmission & FTA event log'
        """,
    ];

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    internal static string PhpTrim(string? value) => (value ?? string.Empty).Trim(' ', '\t', '\n', '\r', '\0', '\x0B');

    internal static string AsciiUpper(string value) => string.Create(value.Length, value, (span, source) =>
    {
        for (var i = 0; i < source.Length; i++)
        {
            span[i] = source[i] is >= 'a' and <= 'z' ? (char)(source[i] - 32) : source[i];
        }
    });

    /// <summary>PHP <c>epc_einvoice_ensure_schema()</c> with <c>epc_einvoice_seed_defaults()</c>. Errors propagate, as in PHP.</summary>
    public static async Task EnsureSchemaAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        foreach (var statement in SchemaStatements)
        {
            await ErpDb.ExecuteAsync(connection, transaction, statement, cancellationToken).ConfigureAwait(false);
        }

        var now = Now();
        foreach (var (key, value) in DefaultSettings)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `epc_einvoice_settings` (`setting_key`, `setting_value`, `time_updated`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `setting_key` = `setting_key`"),
                cancellationToken,
                key,
                value,
                now).ConfigureAwait(false);
        }
    }

    /// <summary>PHP <c>epc_einvoice_tin_from_trn()</c>: the first ten digits.</summary>
    public static string TinFromTrn(string? trn)
    {
        var digits = Regex.Replace(trn ?? string.Empty, "\\D", string.Empty);
        return digits.Length >= 10 ? digits[..10] : digits;
    }

    /// <summary>PHP <c>epc_einvoice_peppol_endpoint()</c>.</summary>
    public static string PeppolEndpoint(string? tin, string? electronicId = ElectronicScheme)
    {
        var scheme = PhpTrim(electronicId);
        if (scheme.Length == 0)
        {
            scheme = ElectronicScheme;
        }

        var t = TinFromTrn(tin);
        return t.Length == 0 ? string.Empty : scheme + ":" + t;
    }

    /// <summary>PHP <c>epc_uae_vat_normalize_country()</c>.</summary>
    public static string NormalizeCountry(string? code)
    {
        var c = AsciiUpper(PhpTrim(code));
        return c is "" or "UAE" or "ARE" or "UNITED ARAB EMIRATES" or "U.A.E." or "U.A.E" ? "AE" : c;
    }

    /// <summary>
    /// PHP <c>epc_einvoice_buyer_profile()</c>: the stored row (every column as text), else the profile built from the
    /// customer's <c>users_profiles</c>, with the TIN and Peppol endpoint filled in. Empty for no customer.
    /// </summary>
    public static async Task<Dictionary<string, string?>> BuyerProfileAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (userId <= 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var row = await RowAsync(connection, transaction, "SELECT * FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1", cancellationToken, userId).ConfigureAwait(false)
            ?? await ProfileFromUserAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
        var tin = !PhpEmpty(row.GetValueOrDefault("tin")) ? row["tin"] : TinFromTrn(row.GetValueOrDefault("trn"));
        row["tin"] = tin;
        row["peppol_endpoint"] = !PhpEmpty(row.GetValueOrDefault("peppol_endpoint"))
            ? row["peppol_endpoint"]
            : PeppolEndpoint(tin, row.TryGetValue("electronic_id", out var scheme) && scheme is not null ? scheme : ElectronicScheme);
        return row;
    }

    internal static bool PhpEmpty(string? value) => value is null or "" or "0";

    private static async Task<Dictionary<string, string?>> ProfileFromUserAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = string.Empty, ["surname"] = string.Empty, ["email"] = string.Empty, ["phone"] = string.Empty,
            ["company"] = string.Empty, ["address"] = string.Empty, ["city"] = string.Empty,
        };
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional("SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
                if (fields.ContainsKey(key))
                {
                    fields[key] = PhpTrim(reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture));
                }
            }
        }

        var user = await RowAsync(connection, transaction, "SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1", cancellationToken, userId).ConfigureAwait(false)
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        var name = PhpTrim(fields["company"].Length > 0 ? fields["company"] : PhpTrim(fields["name"] + " " + fields["surname"]));

        var regCountry = AsciiUpper(PhpTrim(await Storefront.EpcCustomerTrade.ProfileGetAsync(connection, transaction, userId, "epc_reg_country", cancellationToken, "AE").ConfigureAwait(false)));
        var country = NormalizeCountry(regCountry.Length == 2 ? regCountry : "AE");
        var trn = Regex.Replace(await Storefront.EpcCustomerTrade.ProfileGetAsync(connection, transaction, userId, "epc_reg_trn", cancellationToken).ConfigureAwait(false), "\\D", string.Empty);

        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["user_id"] = userId.ToString(CultureInfo.InvariantCulture),
            ["buyer_name"] = name.Length > 0 ? name : "Customer #" + userId.ToString(CultureInfo.InvariantCulture),
            ["trn"] = trn,
            ["tin"] = string.Empty,
            ["legal_reg_no"] = string.Empty,
            ["legal_reg_type"] = "TL",
            ["authority_name"] = string.Empty,
            ["address_line1"] = fields["address"],
            ["city"] = fields["city"].Length > 0 ? fields["city"] : country == "AE" ? "Dubai" : string.Empty,
            ["emirate"] = country == "AE" ? "Dubai" : string.Empty,
            ["country_code"] = country.Length > 0 ? country : "AE",
            ["phone"] = fields["phone"].Length > 0 ? fields["phone"] : user.GetValueOrDefault("phone") ?? string.Empty,
            ["email"] = fields["email"].Length > 0 ? fields["email"] : user.GetValueOrDefault("email") ?? string.Empty,
            ["electronic_id"] = ElectronicScheme,
            ["peppol_endpoint"] = string.Empty,
            ["buyer_onboarded"] = "0",
        };
    }

    /// <summary>PHP <c>epc_einvoice_save_buyer_profile()</c>: the upsert of the buyer row. A customer id of 0 or less is refused.</summary>
    public static async Task SaveBuyerProfileAsync(DbConnection connection, DbTransaction? transaction, EpcEinvoiceBuyerInput data, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (data.UserId <= 0)
        {
            throw new InvalidOperationException("Invalid customer");
        }

        var tin = PhpTrim(data.Tin);
        if (tin.Length == 0)
        {
            tin = TinFromTrn(data.Trn);
        }

        var onboarded = data.BuyerOnboarded ? 1 : 0;
        var endpoint = PhpTrim(data.PeppolEndpoint);
        if (endpoint.Length == 0 && onboarded == 1 && tin.Length > 0)
        {
            endpoint = PeppolEndpoint(tin, data.ElectronicId ?? ElectronicScheme);
        }

        var legalType = data.LegalRegType ?? "TL";
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                """
                INSERT INTO `epc_einvoice_buyer_profiles`
                (`user_id`, `buyer_name`, `trn`, `tin`, `legal_reg_no`, `legal_reg_type`, `authority_name`,
                 `address_line1`, `city`, `emirate`, `country_code`, `phone`, `email`, `electronic_id`,
                 `peppol_endpoint`, `buyer_onboarded`, `time_updated`)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                ON DUPLICATE KEY UPDATE
                `buyer_name`=VALUES(`buyer_name`), `trn`=VALUES(`trn`), `tin`=VALUES(`tin`),
                `legal_reg_no`=VALUES(`legal_reg_no`), `legal_reg_type`=VALUES(`legal_reg_type`),
                `authority_name`=VALUES(`authority_name`), `address_line1`=VALUES(`address_line1`),
                `city`=VALUES(`city`), `emirate`=VALUES(`emirate`), `country_code`=VALUES(`country_code`),
                `phone`=VALUES(`phone`), `email`=VALUES(`email`), `electronic_id`=VALUES(`electronic_id`),
                `peppol_endpoint`=VALUES(`peppol_endpoint`), `buyer_onboarded`=VALUES(`buyer_onboarded`),
                `time_updated`=VALUES(`time_updated`)
                """),
            cancellationToken,
            data.UserId,
            PhpTrim(data.BuyerName),
            PhpTrim(data.Trn),
            tin,
            PhpTrim(data.LegalRegNo),
            legalType is "TL" or "EID" or "PAS" or "CD" ? legalType : "TL",
            PhpTrim(data.AuthorityName),
            PhpTrim(data.AddressLine1),
            PhpTrim(data.City ?? "Dubai"),
            PhpTrim(data.Emirate ?? "Dubai"),
            AsciiUpper(PhpTrim(data.CountryCode ?? "AE")),
            PhpTrim(data.Phone),
            PhpTrim(data.Email),
            ElectronicScheme,
            endpoint,
            onboarded,
            Now()).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, string?>?> RowAsync(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var row = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
        }

        return row;
    }
}
