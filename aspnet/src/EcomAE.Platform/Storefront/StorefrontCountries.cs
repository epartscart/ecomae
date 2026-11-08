using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Caching.Memory;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// One row per country storefront a tenant operates (ae.domain / qa.domain / sa.domain …).
/// Lives in the tenant shop schema — the same DB the rest of the storefront reads, so a
/// country host binds the SAME tenant data and only the presentation layer differs.
/// </summary>
public sealed record StorefrontCountry(
    string CountryCode,
    string CountryName,
    string Subdomain,
    string CurrencyCode,
    decimal FxRate,
    decimal PriceMarkupPercent,
    decimal PriceRoundTo,
    decimal VatPercent,
    string LanguageCode,
    string Direction,
    string SeoTitle,
    string SeoDescription,
    string SeoKeywords,
    string AddressText,
    bool IsDefault,
    bool IsActive,
    int SortOrder)
{
    /// <summary>Convert a base-currency amount into this country's display currency.</summary>
    public decimal ConvertPrice(decimal baseAmount)
    {
        var converted = baseAmount * (FxRate <= 0m ? 1m : FxRate);
        if (PriceMarkupPercent != 0m)
        {
            converted *= 1m + (PriceMarkupPercent / 100m);
        }

        var step = PriceRoundTo <= 0m ? 0.01m : PriceRoundTo;
        return Math.Round(converted / step, MidpointRounding.AwayFromZero) * step;
    }

    public string FormatPrice(decimal baseAmount)
        => ConvertPrice(baseAmount).ToString("#,0.00", CultureInfo.InvariantCulture) + " " + CurrencyCode;
}

public sealed record StorefrontCountryWrite(
    string CountryCode,
    string CountryName,
    string Subdomain,
    string CurrencyCode,
    decimal FxRate,
    decimal PriceMarkupPercent,
    decimal PriceRoundTo,
    decimal VatPercent,
    string LanguageCode,
    string Direction,
    string SeoTitle,
    string SeoDescription,
    string SeoKeywords,
    string AddressText,
    bool IsDefault,
    bool IsActive,
    int SortOrder);

public interface IStorefrontCountryService
{
    /// <summary>Country context for the current request host — built-in defaults when unconfigured.</summary>
    Task<StorefrontCountry> ResolveAsync(string host, CancellationToken cancellationToken = default);

    /// <summary>Every configured country (falls back to the built-in GCC set when the table is empty/missing).</summary>
    Task<IReadOnlyList<StorefrontCountry>> ListAsync(CancellationToken cancellationToken = default);

    Task<(bool Ok, string Message)> SaveAsync(StorefrontCountryWrite write, CancellationToken cancellationToken = default);

    Task<(bool Ok, string Message)> DeleteAsync(string countryCode, CancellationToken cancellationToken = default);
}

public static class StorefrontCountryDefaults
{
    /// <summary>Two-letter country subdomains that alias to the tenant's base domain.</summary>
    public static readonly IReadOnlySet<string> Subdomains =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ae", "qa", "sa", "kw", "bh", "om", "eu", "us", "uk", "ru", "pk" };

    private static readonly StorefrontCountry[] BuiltIn =
    [
        new("AE", "United Arab Emirates", "ae", "AED", 1m, 0m, 0.01m, 5m, "en", "ltr",
            "", "", "", "", IsDefault: true, IsActive: true, 0),
        new("QA", "Qatar", "qa", "QAR", 1.0005m, 0m, 0.01m, 0m, "en", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 10),
        new("SA", "Saudi Arabia", "sa", "SAR", 1.0221m, 0m, 0.01m, 15m, "ar", "rtl",
            "", "", "", "", IsDefault: false, IsActive: true, 20),
        new("KW", "Kuwait", "kw", "KWD", 0.0837m, 0m, 0.01m, 0m, "en", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 30),
        new("BH", "Bahrain", "bh", "BHD", 0.1025m, 0m, 0.01m, 10m, "en", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 40),
        new("OM", "Oman", "om", "OMR", 0.1048m, 0m, 0.01m, 5m, "en", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 50),
        // Region-level storefronts — same one-backend model as the GCC set.
        new("EU", "European Union", "eu", "EUR", 0.25m, 0m, 0.01m, 20m, "en", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 60),
        new("US", "United States", "us", "USD", 0.2723m, 0m, 0.01m, 0m, "en", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 70),
        // CountryCode "GB" (not "UK") so hreflang emits valid en-GB; the "uk" subdomain is unaffected.
        new("GB", "United Kingdom", "uk", "GBP", 0.2089m, 0m, 0.01m, 20m, "en", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 80),
        new("RU", "Russia", "ru", "RUB", 26.0m, 0m, 0.01m, 20m, "ru", "ltr",
            "", "", "", "", IsDefault: false, IsActive: true, 90),
        // CountryCode "PK" with Urdu (ur) + rtl: Pakistan's official language is Urdu.
        new("PK", "Pakistan", "pk", "PKR", 76.15m, 0m, 0.01m, 17m, "ur", "rtl",
            "", "", "", "", IsDefault: false, IsActive: true, 100),
    ];

    public static IReadOnlyList<StorefrontCountry> All => BuiltIn;

    /// <summary>Lowercase 2-letter country subdomain (ae/qa/sa/kw/bh/om) or null.</summary>
    public static string? SubdomainOf(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var firstDot = host.IndexOf('.');
        if (firstDot != 2)
        {
            return null;
        }

        var label = host[..2];
        return Subdomains.Contains(label) ? label.ToLowerInvariant() : null;
    }

    /// <summary>Base domain a country subdomain aliases to (ae.epartscart.com → epartscart.com).</summary>
    public static string? BaseDomainOf(string? host)
        => SubdomainOf(host) is not null && host!.Length > 3 ? host[3..] : null;

    /// <summary>Canonical code for row matching: a saved "UK" row is the same country as built-in "GB".</summary>
    public static string CanonicalCode(string code)
        => string.Equals(code, "UK", StringComparison.OrdinalIgnoreCase) ? "GB" : code;

    public static StorefrontCountry Default(string? subdomain)
    {
        foreach (var c in BuiltIn)
        {
            if (string.Equals(c.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }

        return BuiltIn[0] with { IsDefault = true };
    }
}

public sealed class StorefrontCountryService : IStorefrontCountryService
{
    private const string TableSql = """
        CREATE TABLE IF NOT EXISTS `epc_tenant_countries` (
            `country_code` VARCHAR(2) NOT NULL,
            `country_name` VARCHAR(120) NOT NULL DEFAULT '',
            `subdomain` VARCHAR(8) NOT NULL DEFAULT '',
            `currency_code` VARCHAR(3) NOT NULL DEFAULT '',
            `fx_rate` DECIMAL(18,6) NOT NULL DEFAULT 1,
            `price_markup_percent` DECIMAL(9,3) NOT NULL DEFAULT 0,
            `price_round_to` DECIMAL(18,6) NOT NULL DEFAULT 0.01,
            `vat_percent` DECIMAL(9,3) NOT NULL DEFAULT 0,
            `language_code` VARCHAR(8) NOT NULL DEFAULT 'en',
            `direction` VARCHAR(3) NOT NULL DEFAULT 'ltr',
            `seo_title` VARCHAR(255) NOT NULL DEFAULT '',
            `seo_description` VARCHAR(500) NOT NULL DEFAULT '',
            `seo_keywords` VARCHAR(500) NOT NULL DEFAULT '',
            `address_text` VARCHAR(1000) NOT NULL DEFAULT '',
            `is_default` TINYINT(1) NOT NULL DEFAULT 0,
            `is_active` TINYINT(1) NOT NULL DEFAULT 1,
            `sort_order` INT NOT NULL DEFAULT 0,
            PRIMARY KEY (`country_code`),
            UNIQUE KEY `epc_tenant_countries_subdomain` (`subdomain`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    private const string ListSql = """
        SELECT `country_code`,`country_name`,`subdomain`,`currency_code`,`fx_rate`,
               `price_markup_percent`,`price_round_to`,`vat_percent`,`language_code`,`direction`,
               `seo_title`,`seo_description`,`seo_keywords`,`address_text`,`is_default`,`is_active`,`sort_order`
        FROM `epc_tenant_countries`
        ORDER BY `is_default` DESC, `sort_order`, `country_code`
        """;

    private const string SaveSql = """
        INSERT INTO `epc_tenant_countries`
            (`country_code`,`country_name`,`subdomain`,`currency_code`,`fx_rate`,
             `price_markup_percent`,`price_round_to`,`vat_percent`,`language_code`,`direction`,
             `seo_title`,`seo_description`,`seo_keywords`,`address_text`,`is_default`,`is_active`,`sort_order`)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
        ON DUPLICATE KEY UPDATE
            `country_name`=VALUES(`country_name`), `subdomain`=VALUES(`subdomain`),
            `currency_code`=VALUES(`currency_code`), `fx_rate`=VALUES(`fx_rate`),
            `price_markup_percent`=VALUES(`price_markup_percent`), `price_round_to`=VALUES(`price_round_to`),
            `vat_percent`=VALUES(`vat_percent`), `language_code`=VALUES(`language_code`),
            `direction`=VALUES(`direction`), `seo_title`=VALUES(`seo_title`),
            `seo_description`=VALUES(`seo_description`), `seo_keywords`=VALUES(`seo_keywords`),
            `address_text`=VALUES(`address_text`), `is_default`=VALUES(`is_default`),
            `is_active`=VALUES(`is_active`), `sort_order`=VALUES(`sort_order`)
        """;

    // Soft delete: the row stays as a tombstone so the built-in fallback does not resurrect the
    // storefront as active on the next read.
    private const string DeleteSql = "UPDATE `epc_tenant_countries` SET `is_active` = 0, `is_default` = 0 WHERE `country_code` = ?";

    // Tombstone for a built-in that was never saved: copies the built-in's full settings so a
    // later reactivation keeps its real currency/VAT/locale, and keeps a subdomain value so host
    // resolution still finds the row. The subdomain column has a unique key — when a custom
    // country already claimed it, a synthetic `off-<cc>` value is used instead.
    private const string TombstoneInsertSql = """
        INSERT INTO `epc_tenant_countries`
            (`country_code`, `country_name`, `subdomain`, `currency_code`, `fx_rate`,
             `price_markup_percent`, `price_round_to`, `vat_percent`, `language_code`, `direction`,
             `seo_title`, `seo_description`, `seo_keywords`, `address_text`,
             `is_default`, `is_active`, `sort_order`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, 0, ?)
        """;

    private const string SubdomainTakenSql =
        "SELECT COUNT(*) FROM `epc_tenant_countries` WHERE `subdomain` = ?";

    private const string ClearDefaultSql = "UPDATE `epc_tenant_countries` SET `is_default` = 0 WHERE `country_code` <> ?";

    // Keyed by tenant database — a shared process-wide key would leak one tenant's
    // SEO/address/pricing onto another tenant's storefronts.
    private const string CacheKeyPrefix = "storefront-countries:list:";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IMemoryCache _cache;
    private readonly EcomAE.Platform.Data.ITenantDbConnectionFactory? _registryConnections;

    public StorefrontCountryService(
        IErpWriteConnectionFactory connections,
        IMemoryCache cache,
        EcomAE.Platform.Data.ITenantDbConnectionFactory? registryConnections = null)
    {
        _connections = connections;
        _cache = cache;
        _registryConnections = registryConnections;
    }

    public async Task<StorefrontCountry> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        var subdomain = StorefrontCountryDefaults.SubdomainOf(host);
        var countries = await ListAsync(cancellationToken).ConfigureAwait(false);
        if (subdomain is not null)
        {
            // The built-in country code for this host — a tombstone may carry a synthetic
            // subdomain, so match rows by country code as well as by subdomain.
            var builtinCode = StorefrontCountryDefaults.Default(subdomain).CountryCode;
            var hasRow = false;
            foreach (var country in countries)
            {
                if (string.Equals(country.Subdomain, subdomain, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(StorefrontCountryDefaults.CanonicalCode(country.CountryCode), builtinCode, StringComparison.OrdinalIgnoreCase))
                {
                    hasRow = true;
                    if (country.IsActive)
                    {
                        return country;
                    }
                }
            }

            // No row for this country — built-in. A deactivated row falls through to the default
            // country instead, so "removed" storefronts stop serving their own presentation.
            if (!hasRow)
            {
                return StorefrontCountryDefaults.Default(subdomain);
            }
        }
        else
        {
            // Domain-mapped storefronts (auto247.pk → PK): the registry domain map pins a
            // country code for a whole domain — same effect as a {cc}. subdomain.
            var mappedCode = await ResolveDomainMapCountryCodeAsync(host, cancellationToken).ConfigureAwait(false);
            if (mappedCode is { Length: 2 })
            {
                foreach (var country in countries)
                {
                    if (country.IsActive
                        && string.Equals(StorefrontCountryDefaults.CanonicalCode(country.CountryCode), mappedCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return country;
                    }
                }

                // Tenant has no row for the mapped country — fall back to the built-in for it.
                foreach (var builtin in StorefrontCountryDefaults.All)
                {
                    if (string.Equals(StorefrontCountryDefaults.CanonicalCode(builtin.CountryCode), mappedCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return builtin;
                    }
                }
            }
        }

        foreach (var country in countries)
        {
            if (country.IsDefault && country.IsActive)
            {
                return country;
            }
        }

        // First active row wins; a tombstone at the top must not bounce the host back to the
        // built-in for the country that was just removed.
        foreach (var country in countries)
        {
            if (country.IsActive)
            {
                return country;
            }
        }

        return StorefrontCountryDefaults.Default(null);
    }

    public async Task<IReadOnlyList<StorefrontCountry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyPrefix + await TenantKeyAsync(cancellationToken).ConfigureAwait(false);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<StorefrontCountry>? cached) && cached is not null)
        {
            return cached;
        }

        var list = await ReadRowsAsync(cancellationToken).ConfigureAwait(false);
        // Union with built-ins for codes the tenant has no row for — one save must not drop the
        // other country storefronts from hreflang and the management list. DB row wins per code;
        // a deleted/deactivated row stays (as a tombstone) so built-ins never resurrect it.
        var have = new HashSet<string>(list.Select(c => StorefrontCountryDefaults.CanonicalCode(c.CountryCode)), StringComparer.OrdinalIgnoreCase);
        foreach (var builtin in StorefrontCountryDefaults.All)
        {
            if (!have.Contains(builtin.CountryCode))
            {
                list.Add(builtin);
            }
        }
        list = [.. list.OrderByDescending(c => c.IsDefault).ThenBy(c => c.SortOrder).ThenBy(c => c.CountryCode)];

        _cache.Set(cacheKey, (IReadOnlyList<StorefrontCountry>)list, TimeSpan.FromMinutes(5));
        return list;
    }

    public async Task<(bool Ok, string Message)> SaveAsync(StorefrontCountryWrite write, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(write.CountryCode) || write.CountryCode.Trim().Length != 2)
        {
            return (false, "Country code must be two letters (AE, QA, SA…).");
        }

        if (string.IsNullOrWhiteSpace(write.CurrencyCode))
        {
            return (false, "Currency code is required.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.TryExecuteAsync(connection, TableSql, cancellationToken).ConfigureAwait(false);
            var cc = write.CountryCode.Trim().ToUpperInvariant();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                SaveSql,
                cancellationToken,
                cc,
                write.CountryName.Trim(),
                (write.Subdomain.Length > 0 ? write.Subdomain : cc).Trim().ToLowerInvariant(),
                write.CurrencyCode.Trim().ToUpperInvariant(),
                write.FxRate <= 0m ? 1m : write.FxRate,
                write.PriceMarkupPercent,
                write.PriceRoundTo <= 0m ? 0.01m : write.PriceRoundTo,
                write.VatPercent,
                string.IsNullOrWhiteSpace(write.LanguageCode) ? "en" : write.LanguageCode.Trim().ToLowerInvariant(),
                string.Equals(write.Direction, "rtl", StringComparison.OrdinalIgnoreCase) ? "rtl" : "ltr",
                write.SeoTitle.Trim(),
                write.SeoDescription.Trim(),
                write.SeoKeywords.Trim(),
                write.AddressText.Trim(),
                write.IsDefault ? 1 : 0,
                write.IsActive ? 1 : 0,
                write.SortOrder).ConfigureAwait(false);

            if (write.IsDefault)
            {
                await ErpDb.ExecuteAsync(connection, null, ClearDefaultSql, cancellationToken, cc).ConfigureAwait(false);
            }

            _cache.Remove(CacheKeyPrefix + await TenantKeyAsync(cancellationToken).ConfigureAwait(false));
            return (true, "Country saved.");
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return (false, "Country save failed: " + ex.Message);
        }
    }

    public async Task<(bool Ok, string Message)> DeleteAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.TryExecuteAsync(connection, TableSql, cancellationToken).ConfigureAwait(false);
            var cc = countryCode.Trim().ToUpperInvariant();
            var n = await ErpDb.ExecuteAsync(
                connection, null, DeleteSql, cancellationToken, cc).ConfigureAwait(false);
            if (n == 0)
            {
                // No saved row — only a known built-in may be tombstoned; anything else is a typo.
                var builtin = StorefrontCountryDefaults.All
                    .FirstOrDefault(c => string.Equals(c.CountryCode, cc, StringComparison.OrdinalIgnoreCase));
                if (builtin is null)
                {
                    return (false, "Country not found.");
                }

                var subdomain = builtin.Subdomain;
                var taken = await ErpDb.DecimalAsync(
                    connection, null, ErpDb.Positional(SubdomainTakenSql), cancellationToken,
                    subdomain).ConfigureAwait(false);
                if (taken > 0)
                {
                    subdomain = "off-" + cc.ToLowerInvariant();
                }

                await ErpDb.ExecuteAsync(
                    connection, null, TombstoneInsertSql, cancellationToken,
                    cc, builtin.CountryName, subdomain, builtin.CurrencyCode,
                    builtin.FxRate, builtin.PriceMarkupPercent, builtin.PriceRoundTo,
                    builtin.VatPercent, builtin.LanguageCode, builtin.Direction,
                    builtin.SeoTitle, builtin.SeoDescription, builtin.SeoKeywords,
                    builtin.AddressText, builtin.SortOrder).ConfigureAwait(false);
            }

            _cache.Remove(CacheKeyPrefix + await TenantKeyAsync(cancellationToken).ConfigureAwait(false));
            return (true, "Country removed.");
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return (false, "Country delete failed: " + ex.Message);
        }
    }

    /// <summary>Tenant-scope token for cache keys — the shop database name, or the host when unconfigured.</summary>
    private async Task<string> TenantKeyAsync(CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return "unconfigured";
        }
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(connection.Database) ? "default" : connection.Database;
        }
        catch (DbException)
        {
            return "default";
        }
    }

    /// <summary>
    /// Registry domain map (<c>epc_tenant_domains</c>): country pinned to a whole domain
    /// (auto247.pk → PK) — the multi-domain counterpart of the {cc}. subdomain rule.
    /// </summary>
    private async Task<string?> ResolveDomainMapCountryCodeAsync(string host, CancellationToken cancellationToken)
    {
        if (_registryConnections is null || !_registryConnections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _registryConnections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `country_code` FROM `epc_tenant_domains` "
                + "WHERE `is_active` = 1 AND `country_code` <> '' AND `hostname` IN (@h0, @h1) LIMIT 1";
            var h0 = command.CreateParameter();
            h0.ParameterName = "@h0";
            h0.Value = host.Trim().ToLowerInvariant();
            command.Parameters.Add(h0);
            var h1 = command.CreateParameter();
            h1.ParameterName = "@h1";
            var bare = h0.Value.ToString()!;
            h1.Value = bare.StartsWith("www.", StringComparison.Ordinal) ? bare[4..] : "www." + bare;
            command.Parameters.Add(h1);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var code = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant();
            return code is { Length: 2 } ? code : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<StorefrontCountry>> ReadRowsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<StorefrontCountry>();
        if (!_connections.IsConfigured)
        {
            return rows;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.TryExecuteAsync(connection, TableSql, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ListSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new StorefrontCountry(
                    GetStr(reader, "country_code"),
                    GetStr(reader, "country_name"),
                    GetStr(reader, "subdomain"),
                    GetStr(reader, "currency_code"),
                    GetDec(reader, "fx_rate"),
                    GetDec(reader, "price_markup_percent"),
                    GetDec(reader, "price_round_to"),
                    GetDec(reader, "vat_percent"),
                    GetStr(reader, "language_code"),
                    GetStr(reader, "direction"),
                    GetStr(reader, "seo_title"),
                    GetStr(reader, "seo_description"),
                    GetStr(reader, "seo_keywords"),
                    GetStr(reader, "address_text"),
                    GetInt(reader, "is_default") == 1,
                    GetInt(reader, "is_active") == 1,
                    GetInt(reader, "sort_order")));
            }
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return rows;
        }

        return rows;
    }

    private static string GetStr(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? string.Empty : Convert.ToString(reader[name], CultureInfo.InvariantCulture) ?? string.Empty;

    private static decimal GetDec(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? 0m : Convert.ToDecimal(reader[name], CultureInfo.InvariantCulture);

    private static int GetInt(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? 0 : Convert.ToInt32(reader[name], CultureInfo.InvariantCulture);
}

/// <summary>Multi-country stock support: shop_storages gets an optional country_code so
/// the product page can label which country each warehouse row belongs to.</summary>
public static class StorefrontStockCountry
{
    public static async Task<bool> EnsureColumnAsync(DbConnection connection, CancellationToken ct)
    {
        if (!await ErpDb.TableKnownAsync(connection, "shop_storages", ct).ConfigureAwait(false))
        {
            return false;
        }
        if (await ErpDb.ColumnKnownAsync(connection, "shop_storages", "country_code", ct).ConfigureAwait(false))
        {
            return true;
        }
        await ErpDb.TryExecuteAsync(connection,
            "ALTER TABLE `shop_storages` ADD COLUMN `country_code` VARCHAR(2) NULL", ct).ConfigureAwait(false);
        return await ErpDb.ColumnKnownAsync(connection, "shop_storages", "country_code", ct).ConfigureAwait(false);
    }

    /// <summary>storage_id -> upper-case country code for the storages that have one set.</summary>
    public static async Task<IReadOnlyDictionary<long, string>> LoadAsync(DbConnection connection, CancellationToken ct)
    {
        var map = new Dictionary<long, string>();
        if (!await EnsureColumnAsync(connection, ct).ConfigureAwait(false))
        {
            return map;
        }
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT `id`, UPPER(IFNULL(`country_code`,'')) FROM `shop_storages` WHERE `country_code` IS NOT NULL AND `country_code`<>''";
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            map[reader.GetInt64(0)] = reader.GetString(1);
        }
        return map;
    }
}
