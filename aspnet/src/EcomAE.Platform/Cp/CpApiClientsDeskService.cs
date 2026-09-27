using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Typed twin of PHP <c>cp/content/control/portal/epc_api_clients_manage.php</c> (list side) and the
/// catalogue helpers in <c>content/general_pages/epc_api_clients.php</c>. Key hashes are never read.
/// </summary>
public interface ICpApiClientsDeskService
{
    Task<CpApiClientsDeskView> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record CpApiClientRow(
    long Id,
    string Label,
    string ContactEmail,
    string Product,
    string KeyPrefix,
    int DailyLimit,
    int CallsToday,
    IReadOnlyList<string> AllowedActions,
    bool Active,
    long TimeCreated,
    long TimeUpdated)
{
    public string ScopeLabel => AllowedActions.Count == 0 ? "all" : string.Join(", ", AllowedActions);

    public string Quota => CallsToday.ToString(CultureInfo.InvariantCulture)
        + " / "
        + Math.Max(1, DailyLimit).ToString(CultureInfo.InvariantCulture);
}

public sealed record CpApiClientsDeskView(
    IReadOnlyList<CpApiClientRow> Clients,
    int ActiveCount,
    string Source,
    string Error)
{
    public static CpApiClientsDeskView Empty(string source, string error) => new([], 0, source, error);
}

public sealed class CpApiClientsDeskService : ICpApiClientsDeskService
{
    private static readonly Regex ActionSafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Separators = new(@"[\s,]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpApiClientsDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>epc_api_clients_catalog_actions()</c>.</summary>
    public static IReadOnlyList<string> CatalogActions { get; } =
    [
        "manufacturers", "models", "modifications", "categories", "products", "articles",
        "article", "analogs", "brands", "vin", "engines", "engine_search", "status",
    ];

    public static IReadOnlyList<string> Products { get; } = ["catalog", "price_pro", "both"];

    private static readonly IReadOnlyDictionary<string, int> CatalogActionOrder =
        CatalogActions.Select((action, index) => (action, index))
            .ToDictionary(pair => pair.action, pair => pair.index, StringComparer.Ordinal);

    /// <summary>PHP <c>epc_api_clients_parse_allowed_actions()</c> — JSON array, or a comma/space list.</summary>
    public static IReadOnlyList<string> ParseAllowedActions(string? json)
    {
        var text = (json ?? string.Empty).Trim();
        if (text.Length == 0 || text == "*")
        {
            return [];
        }

        if (text[0] == '[')
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                return document.RootElement.EnumerateArray()
                    .Select(element => Slug(element.ValueKind == JsonValueKind.String
                        ? element.GetString()
                        : element.ToString()))
                    .Where(action => action.Length > 0)
                    .ToList();
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return Separators.Split(text)
            .Select(Slug)
            .Where(action => action.Length > 0)
            .ToList();
    }

    /// <summary>PHP scope persistence: Price PRO keys and empty selections store <c>[]</c> (full access).</summary>
    public static string AllowedActionsJson(string product, IEnumerable<string> selected)
    {
        if (product == "price_pro")
        {
            return "[]";
        }

        var actions = selected
            .Select(Slug)
            .Where(action => CatalogActions.Contains(action))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(action => CatalogActionOrder[action])
            .ToList();

        return actions.Count == 0 ? "[]" : JsonSerializer.Serialize(actions);
    }

    public static string NormalizeProduct(string? product)
    {
        var text = (product ?? string.Empty).Trim().ToLowerInvariant();
        return Products.Contains(text) ? text : "catalog";
    }

    /// <summary>PHP <c>max(1, min(1000000, (int) $_POST['daily_limit']))</c>.</summary>
    public static int NormalizeDailyLimit(long posted) => (int)Math.Max(1, Math.Min(1_000_000, posted));

    public async Task<CpApiClientsDeskView> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpApiClientsDeskView.Empty("unconfigured", "Platform database unavailable.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpApiClientsSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            var rows = await ListAsync(connection, cancellationToken).ConfigureAwait(false);
            return new CpApiClientsDeskView(rows, rows.Count(row => row.Active), "database", string.Empty);
        }
        catch (DbException)
        {
            return CpApiClientsDeskView.Empty("database-error", "Platform database unavailable.");
        }
    }

    private static async Task<IReadOnlyList<CpApiClientRow>> ListAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpApiClientRow>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `id`, `label`, `contact_email`, `product`, `client_key_prefix`, `active`, `daily_limit`, "
            + "`calls_today`, `allowed_actions_json`, `time_created`, `time_updated` "
            + "FROM `epc_api_clients` ORDER BY `active` DESC, `id` DESC";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpApiClientRow(
                Number(reader, "id"),
                Text(reader, "label"),
                Text(reader, "contact_email"),
                NormalizeProduct(Text(reader, "product")),
                Text(reader, "client_key_prefix"),
                (int)Number(reader, "daily_limit"),
                (int)Number(reader, "calls_today"),
                ParseAllowedActions(Text(reader, "allowed_actions_json")),
                Number(reader, "active") != 0,
                Number(reader, "time_created"),
                Number(reader, "time_updated")));
        }

        return rows;
    }

    private static string Slug(string? value)
        => ActionSafe.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    private static string Text(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal)?.ToString() ?? string.Empty;
    }

    private static long Number(DbDataReader reader, string column)
        => long.TryParse(Text(reader, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
