using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_credit_set_master</c> twin. Schema-ensure stays PHP —
/// missing <c>epc_credit_profiles</c> refuses instead of CREATE.
/// Posted empty strings keep the existing value (PHP <c>$get</c> parity).
/// Truthy <c>on_hold</c> / <c>tax_exempt</c> set 1; otherwise the existing flag is kept.
/// </summary>
public interface IErpCustomerMasterWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpCustomerMasterWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCustomerMasterWriteRequest(
    long CustomerId,
    string? CustomerAccount = null,
    string? CustomerName = null,
    string? CustomerGroup = null,
    long? LegalEntityId = null,
    long? BusinessUnitId = null,
    string? CurrencyCode = null,
    string? PaymentMethod = null,
    string? DeliveryTerms = null,
    string? DeliveryMode = null,
    string? Trn = null,
    bool TaxExempt = false,
    string? SalesTaxGroup = null,
    string? ContactPerson = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? Website = null,
    string? Address = null,
    string? City = null,
    string? StateRegion = null,
    string? PostalCode = null,
    string? CountryCode = null,
    decimal? CreditLimit = null,
    int? TermsDays = null,
    bool OnHold = false,
    string? RiskBand = null,
    string? Notes = null);

public sealed class ErpCustomerMasterWriteService : IErpCustomerMasterWriteService
{
    private static readonly (string Column, int MaxLen)[] StringCols =
    [
        ("customer_account", 32),
        ("customer_name", 255),
        ("customer_group", 64),
        ("currency_code", 8),
        ("payment_method", 32),
        ("delivery_terms", 32),
        ("delivery_mode", 32),
        ("trn", 64),
        ("sales_tax_group", 64),
        ("contact_person", 255),
        ("contact_email", 128),
        ("contact_phone", 64),
        ("website", 255),
        ("address", 512),
        ("city", 128),
        ("state_region", 128),
        ("postal_code", 32),
        ("country_code", 8),
        ("risk_band", 16),
        ("notes", 65535)
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCustomerMasterWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpCustomerMasterWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CustomerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A customer ID is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_credit_profiles", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Credit profile tables are not provisioned");
        }

        var columns = await LoadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!columns.Contains("customer_id"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Credit profile tables are not provisioned");
        }

        var existing = await LoadExistingAsync(connection, request.CustomerId, cancellationToken).ConfigureAwait(false);
        var merged = Merge(request, existing);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var profileId = existing is null ? 0L : ReadLong(existing, "id");

        if (profileId > 0)
        {
            await UpdateAsync(connection, profileId, columns, merged, now, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            profileId = await InsertAsync(connection, request.CustomerId, columns, merged, now, cancellationToken)
                .ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Customer master saved", profileId > 0 ? profileId : request.CustomerId);
    }

    private static Dictionary<string, object?> Merge(
        ErpCustomerMasterWriteRequest request,
        IReadOnlyDictionary<string, object?>? existing)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["credit_limit"] = request.CreditLimit.HasValue
                ? decimal.Round(request.CreditLimit.Value, 2, MidpointRounding.AwayFromZero)
                : ReadDecimal(existing, "credit_limit", 0m),
            ["terms_days"] = request.TermsDays ?? ReadInt(existing, "terms_days", 30),
            ["on_hold"] = request.OnHold ? 1 : ReadInt(existing, "on_hold", 0),
            ["risk_band"] = PostedOrExisting(request.RiskBand, existing, "risk_band", 16, "normal") ?? "normal",
            ["notes"] = PostedOrExisting(request.Notes, existing, "notes", 65535, "") ?? "",
            ["customer_account"] = PostedOrExisting(request.CustomerAccount, existing, "customer_account", 32, null),
            ["customer_name"] = PostedOrExisting(request.CustomerName, existing, "customer_name", 255, null),
            ["customer_group"] = PostedOrExisting(request.CustomerGroup, existing, "customer_group", 64, null),
            ["legal_entity_id"] = request.LegalEntityId ?? ReadLong(existing, "legal_entity_id"),
            ["business_unit_id"] = request.BusinessUnitId ?? ReadLong(existing, "business_unit_id"),
            ["currency_code"] = PostedOrExisting(request.CurrencyCode, existing, "currency_code", 8, null),
            ["payment_method"] = PostedOrExisting(request.PaymentMethod, existing, "payment_method", 32, null),
            ["delivery_terms"] = PostedOrExisting(request.DeliveryTerms, existing, "delivery_terms", 32, null),
            ["delivery_mode"] = PostedOrExisting(request.DeliveryMode, existing, "delivery_mode", 32, null),
            ["trn"] = PostedOrExisting(request.Trn, existing, "trn", 64, null),
            ["tax_exempt"] = request.TaxExempt ? 1 : ReadInt(existing, "tax_exempt", 0),
            ["sales_tax_group"] = PostedOrExisting(request.SalesTaxGroup, existing, "sales_tax_group", 64, null),
            ["contact_person"] = PostedOrExisting(request.ContactPerson, existing, "contact_person", 255, null),
            ["contact_email"] = PostedOrExisting(request.ContactEmail, existing, "contact_email", 128, null),
            ["contact_phone"] = PostedOrExisting(request.ContactPhone, existing, "contact_phone", 64, null),
            ["website"] = PostedOrExisting(request.Website, existing, "website", 255, null),
            ["address"] = PostedOrExisting(request.Address, existing, "address", 512, null),
            ["city"] = PostedOrExisting(request.City, existing, "city", 128, null),
            ["state_region"] = PostedOrExisting(request.StateRegion, existing, "state_region", 128, null),
            ["postal_code"] = PostedOrExisting(request.PostalCode, existing, "postal_code", 32, null),
            ["country_code"] = PostedOrExisting(request.CountryCode, existing, "country_code", 8, null)
        };
        return map;
    }

    private static async Task UpdateAsync(
        DbConnection connection,
        long profileId,
        HashSet<string> columns,
        IReadOnlyDictionary<string, object?> merged,
        long now,
        CancellationToken cancellationToken)
    {
        var sets = new List<string>();
        var values = new List<object?>();
        AppendSet(sets, values, columns, merged, "credit_limit");
        AppendSet(sets, values, columns, merged, "terms_days");
        AppendSet(sets, values, columns, merged, "on_hold");
        AppendSet(sets, values, columns, merged, "risk_band");
        AppendSet(sets, values, columns, merged, "notes");
        foreach (var (column, _) in StringCols)
        {
            if (column is "risk_band" or "notes")
            {
                continue;
            }

            AppendSet(sets, values, columns, merged, column);
        }

        AppendSet(sets, values, columns, merged, "legal_entity_id");
        AppendSet(sets, values, columns, merged, "business_unit_id");
        AppendSet(sets, values, columns, merged, "tax_exempt");
        if (columns.Contains("time_updated"))
        {
            sets.Add("`time_updated` = ?");
            values.Add(now);
        }

        if (sets.Count == 0)
        {
            return;
        }

        values.Add(profileId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_credit_profiles` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
            cancellationToken,
            values.ToArray()).ConfigureAwait(false);
    }

    private static async Task<long> InsertAsync(
        DbConnection connection,
        long customerId,
        HashSet<string> columns,
        IReadOnlyDictionary<string, object?> merged,
        long now,
        CancellationToken cancellationToken)
    {
        var names = new List<string> { "`customer_id`" };
        var placeholders = new List<string> { "?" };
        var values = new List<object?> { customerId };

        void Add(string column, object? value)
        {
            if (!columns.Contains(column))
            {
                return;
            }

            names.Add("`" + column + "`");
            placeholders.Add("?");
            values.Add(value);
        }

        Add("credit_limit", merged["credit_limit"]);
        Add("terms_days", merged["terms_days"]);
        Add("on_hold", merged["on_hold"]);
        Add("risk_band", merged["risk_band"]);
        Add("notes", merged["notes"]);
        foreach (var (column, _) in StringCols)
        {
            if (column is "risk_band" or "notes")
            {
                continue;
            }

            Add(column, merged[column]);
        }

        Add("legal_entity_id", merged["legal_entity_id"]);
        Add("business_unit_id", merged["business_unit_id"]);
        Add("tax_exempt", merged["tax_exempt"]);
        Add("time_created", now);
        Add("time_updated", now);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_credit_profiles` (" + string.Join(",", names) + ") VALUES (" +
                string.Join(",", placeholders) + ")"),
            cancellationToken,
            values.ToArray()).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static void AppendSet(
        List<string> sets,
        List<object?> values,
        HashSet<string> columns,
        IReadOnlyDictionary<string, object?> merged,
        string column)
    {
        if (!columns.Contains(column) || !merged.ContainsKey(column))
        {
            return;
        }

        sets.Add("`" + column + "` = ?");
        values.Add(merged[column]);
    }

    private static async Task<Dictionary<string, object?>?> LoadExistingAsync(
        DbConnection connection,
        long customerId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT * FROM `epc_credit_profiles` WHERE `customer_id` = ? LIMIT 1");
        ErpDb.AddParameters(cmd, customerId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            map[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return map;
    }

    private static async Task<HashSet<string>> LoadColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `COLUMN_NAME` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?");
        ErpDb.AddParameters(cmd, "epc_credit_profiles");
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
            if (name.Length > 0)
            {
                set.Add(name);
            }
        }

        return set;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static string? PostedOrExisting(
        string? posted,
        IReadOnlyDictionary<string, object?>? existing,
        string key,
        int maxLen,
        string? fallback)
    {
        if (!string.IsNullOrEmpty(posted))
        {
            return Truncate(posted.Trim(), maxLen);
        }

        if (existing is not null && existing.TryGetValue(key, out var raw) && raw is not null)
        {
            var text = Convert.ToString(raw, CultureInfo.InvariantCulture);
            return text is null ? fallback : Truncate(text, maxLen);
        }

        return fallback;
    }

    private static string Truncate(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static long ReadLong(IReadOnlyDictionary<string, object?>? existing, string key)
    {
        if (existing is null || !existing.TryGetValue(key, out var raw) || raw is null)
        {
            return 0;
        }

        return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?>? existing, string key, int fallback)
    {
        if (existing is null || !existing.TryGetValue(key, out var raw) || raw is null)
        {
            return fallback;
        }

        return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(IReadOnlyDictionary<string, object?>? existing, string key, decimal fallback)
    {
        if (existing is null || !existing.TryGetValue(key, out var raw) || raw is null)
        {
            return fallback;
        }

        return decimal.Round(Convert.ToDecimal(raw, CultureInfo.InvariantCulture), 2, MidpointRounding.AwayFromZero);
    }
}
