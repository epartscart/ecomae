using System.Data.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Erp;

/// <summary>
/// One-way Microsoft Dynamics 365 Finance &amp; Operations → EcomAE connector.
/// Authenticates via Entra ID client-credentials (app registration) and pulls
/// data-entity rows over the F&amp;O OData endpoint into <c>epc_d365_sync_*</c>
/// staging tables; mapping into the operational masters happens per entity on
/// top of the staged payload so the pull can never half-write a master.
/// </summary>
public interface IErpD365ConnectorService
{
    bool IsConfigured { get; }
    string? EnvironmentUrl { get; }
    Task<ErpD365ConnectionStatus> GetStatusAsync(DbConnection connection, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<ErpD365PullResult> PullEntityAsync(DbConnection connection, string entitySet, int top, long adminId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErpD365SyncRun>> RecentRunsAsync(DbConnection connection, int limit, CancellationToken cancellationToken = default);
    Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);
}

public sealed record ErpD365ConnectionStatus(
    bool Configured,
    string? EnvironmentUrl,
    string? LastRunStatus,
    DateTimeOffset? LastRunAt,
    int StagedObjectCount);

public sealed record ErpD365PullResult(bool Succeeded, string Code, string Message, int Fetched, int Staged, long RunId);

public sealed record ErpD365SyncRun(
    long Id,
    string Entity,
    string Status,
    int Fetched,
    int Staged,
    string Error,
    long StartedAt,
    long FinishedAt);

public sealed class ErpD365ConnectorService : IErpD365ConnectorService
{
    /// <summary>Canonical F&amp;O public data entities supported by the sync console.</summary>
    public static readonly IReadOnlyDictionary<string, string> KnownEntities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReleasedProductsV2"] = "Released products (items)",
            ["CustomersV3"] = "Customers",
            ["VendorsV2"] = "Vendors",
            ["SalesOrderHeadersV2"] = "Sales order headers",
            ["PurchaseOrderHeadersV2"] = "Purchase order headers",
            ["SalesInvoiceHeadersV2"] = "Sales invoice headers",
            ["MainAccounts"] = "Chart of accounts (main accounts)",
            ["LegalEntities"] = "Legal entities / data areas",
        };

    private const string TokenPath = "/oauth2/v2.0/token";
    private const string RunsTable = "epc_d365_sync_runs";
    private const string ObjectsTable = "epc_d365_sync_objects";

    private readonly IHttpClientFactory _http;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly ILogger<ErpD365ConnectorService> _logger;
    private readonly string? _tenantId;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _environmentUrl;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public ErpD365ConnectorService(
        IHttpClientFactory http,
        IErpWriteConnectionFactory connections,
        IConfiguration configuration,
        ILogger<ErpD365ConnectorService> logger)
    {
        _http = http;
        _connections = connections;
        _logger = logger;
        _tenantId = FirstNonEmpty(configuration["D365:TenantId"], Environment.GetEnvironmentVariable("ECOMAE_D365_TENANT_ID"));
        _clientId = FirstNonEmpty(configuration["D365:ClientId"], Environment.GetEnvironmentVariable("ECOMAE_D365_CLIENT_ID"));
        _clientSecret = FirstNonEmpty(configuration["D365:ClientSecret"], Environment.GetEnvironmentVariable("ECOMAE_D365_CLIENT_SECRET"));
        _environmentUrl = NormalizeEnvUrl(
            FirstNonEmpty(configuration["D365:EnvironmentUrl"], Environment.GetEnvironmentVariable("ECOMAE_D365_ENVIRONMENT_URL")));
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_tenantId)
        && !string.IsNullOrWhiteSpace(_clientId)
        && !string.IsNullOrWhiteSpace(_clientSecret)
        && !string.IsNullOrWhiteSpace(_environmentUrl);

    public string? EnvironmentUrl => _environmentUrl;

    public async Task<ErpD365ConnectionStatus> GetStatusAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var (status, at) = await LastRunAsync(connection, cancellationToken).ConfigureAwait(false);
        var staged = Convert.ToInt32(await ErpDb.ScalarAsync(
            connection,
            null,
            $"SELECT COUNT(*) FROM {ObjectsTable}",
            cancellationToken).ConfigureAwait(false) ?? 0L);
        return new ErpD365ConnectionStatus(IsConfigured, _environmentUrl, status, at, staged);
    }

    public async Task<ErpSimpleWriteResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("not_configured", "D365 connection is not configured. Set D365:TenantId / ClientId / ClientSecret / EnvironmentUrl (or ECOMAE_D365_* env).");
        }

        var token = await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return ErpSimpleWriteResult.Fail("auth_failed", "Entra token request failed — check tenant id, client id and client secret.");
        }

        try
        {
            using var client = _http.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_environmentUrl}/data/LegalEntities?$top=1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new ErpSimpleWriteResult(true, "ok", "Connected: token acquired and OData /data/LegalEntities returned rows.", 0, 0);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("odata_error", $"OData returned {(int)response.StatusCode} {response.ReasonPhrase}: {Trim(body, 200)}");
        }
        catch (HttpRequestException ex)
        {
            return ErpSimpleWriteResult.Fail("network", $"Cannot reach {_environmentUrl}: {ex.Message}");
        }
    }

    public async Task<ErpD365PullResult> PullEntityAsync(
        DbConnection connection,
        string entitySet,
        int top,
        long adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!IsConfigured)
        {
            return new ErpD365PullResult(false, "not_configured", "D365 connection is not configured.", 0, 0, 0);
        }

        if (!KnownEntities.TryGetValue(entitySet, out var label))
        {
            return new ErpD365PullResult(false, "unknown_entity", $"Unknown entity set '{entitySet}'.", 0, 0, 0);
        }

        top = Math.Clamp(top, 1, 5000);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long runId = 0;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            $"INSERT INTO {RunsTable} (entity, direction, status, fetched, staged, error, started_at, finished_at, admin_id) " +
            "VALUES (@entity, 'pull', 'running', 0, 0, '', @started, 0, @admin)",
            cancellationToken,
            new MySqlConnector.MySqlParameter("@entity", entitySet),
            new MySqlConnector.MySqlParameter("@started", now),
            new MySqlConnector.MySqlParameter("@admin", adminId)).ConfigureAwait(false);
        runId = Convert.ToInt64(await ErpDb.ScalarAsync(connection, null, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false));

        var token = await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            await FinishRunAsync(connection, runId, "auth_failed", 0, 0, "Entra token request failed", cancellationToken).ConfigureAwait(false);
            return new ErpD365PullResult(false, "auth_failed", "Entra token request failed — check credentials.", 0, 0, runId);
        }

        try
        {
            using var client = _http.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_environmentUrl}/data/{entitySet}?$top={top}&$count=true");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var error = $"HTTP {(int)response.StatusCode}: {Trim(body, 300)}";
                await FinishRunAsync(connection, runId, "error", 0, 0, error, cancellationToken).ConfigureAwait(false);
                return new ErpD365PullResult(false, "odata_error", error, 0, 0, runId);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var rows = doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Select(e => e.Clone()).ToList()
                : new List<JsonElement>();

            var staged = 0;
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                var key = ExternalKeyOf(row);
                var payload = row.GetRawText();
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    $"INSERT INTO {ObjectsTable} (entity, ext_key, payload, run_id, synced_at) " +
                    "VALUES (@entity, @ext, @payload, @run, @now) " +
                    "ON DUPLICATE KEY UPDATE payload = VALUES(payload), run_id = VALUES(run_id), synced_at = VALUES(synced_at)",
                    cancellationToken,
                    new MySqlConnector.MySqlParameter("@entity", entitySet),
                    new MySqlConnector.MySqlParameter("@ext", key),
                    new MySqlConnector.MySqlParameter("@payload", payload),
                    new MySqlConnector.MySqlParameter("@run", runId),
                    new MySqlConnector.MySqlParameter("@now", now)).ConfigureAwait(false);
                staged++;
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            await FinishRunAsync(connection, runId, "ok", rows.Count, staged, string.Empty, cancellationToken).ConfigureAwait(false);
            return new ErpD365PullResult(true, "ok", $"Pulled {rows.Count} {label} row(s) into staging.", rows.Count, staged, runId);
        }
        catch (HttpRequestException ex)
        {
            await FinishRunAsync(connection, runId, "error", 0, 0, ex.Message, cancellationToken).ConfigureAwait(false);
            return new ErpD365PullResult(false, "network", $"Cannot reach {_environmentUrl}: {ex.Message}", 0, 0, runId);
        }
        catch (JsonException ex)
        {
            await FinishRunAsync(connection, runId, "error", 0, 0, $"Bad JSON: {ex.Message}", cancellationToken).ConfigureAwait(false);
            return new ErpD365PullResult(false, "json", $"Unparseable OData response: {ex.Message}", 0, 0, runId);
        }
    }

    public async Task<IReadOnlyList<ErpD365SyncRun>> RecentRunsAsync(DbConnection connection, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var runs = new List<ErpD365SyncRun>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id, entity, status, fetched, staged, error, started_at, finished_at FROM {RunsTable} ORDER BY id DESC LIMIT @limit";
        var limitParam = command.CreateParameter();
        limitParam.ParameterName = "@limit";
        limitParam.Value = Math.Clamp(limit, 1, 100);
        command.Parameters.Add(limitParam);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            runs.Add(new ErpD365SyncRun(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                reader.GetInt64(6),
                reader.GetInt64(7)));
        }
        return runs;
    }

    public async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            $"CREATE TABLE IF NOT EXISTS {RunsTable} (" +
            "id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "entity VARCHAR(128) NOT NULL," +
            "direction VARCHAR(16) NOT NULL DEFAULT 'pull'," +
            "status VARCHAR(24) NOT NULL," +
            "fetched INT NOT NULL DEFAULT 0," +
            "staged INT NOT NULL DEFAULT 0," +
            "error TEXT NULL," +
            "started_at BIGINT NOT NULL," +
            "finished_at BIGINT NOT NULL DEFAULT 0," +
            "admin_id BIGINT NOT NULL DEFAULT 0," +
            "KEY idx_entity_started (entity, started_at)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            $"CREATE TABLE IF NOT EXISTS {ObjectsTable} (" +
            "id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "entity VARCHAR(128) NOT NULL," +
            "ext_key VARCHAR(255) NOT NULL DEFAULT ''," +
            "payload MEDIUMTEXT NOT NULL," +
            "run_id BIGINT NOT NULL DEFAULT 0," +
            "synced_at BIGINT NOT NULL," +
            "UNIQUE KEY uq_entity_ext (entity, ext_key)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> AcquireTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _cachedToken;
        }

        try
        {
            using var client = _http.CreateClient();
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId!,
                ["client_secret"] = _clientSecret!,
                ["scope"] = $"{_environmentUrl}/.default",
            });
            using var response = await client.PostAsync(
                $"https://login.microsoftonline.com/{_tenantId}{TokenPath}",
                form,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("D365 token request failed: {Status}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds) ? seconds : 3600;
            _cachedToken = token;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return token;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "D365 token request errored");
            return null;
        }
    }

    private static string ExternalKeyOf(JsonElement row)
    {
        foreach (var candidate in new[] { "dataAreaId", "ItemNumber", "CustomerAccount", "VendorAccount", "SalesOrderNumber", "PurchaseOrderNumber", "InvoiceNumber", "MainAccountId", "CompanyId" })
        {
            if (row.TryGetProperty(candidate, out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } s)
            {
                return s.Length > 200 ? s[..200] : s;
            }
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(row.GetRawText())))[..40];
    }

    private async Task FinishRunAsync(DbConnection connection, long runId, string status, int fetched, int staged, string error, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            $"UPDATE {RunsTable} SET status = @status, fetched = @fetched, staged = @staged, error = @error, finished_at = @done WHERE id = @id",
            cancellationToken,
            new MySqlConnector.MySqlParameter("@status", status),
            new MySqlConnector.MySqlParameter("@fetched", fetched),
            new MySqlConnector.MySqlParameter("@staged", staged),
            new MySqlConnector.MySqlParameter("@error", error.Length > 65000 ? error[..65000] : error),
            new MySqlConnector.MySqlParameter("@done", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            new MySqlConnector.MySqlParameter("@id", runId)).ConfigureAwait(false);
    }

    private async Task<(string? Status, DateTimeOffset? At)> LastRunAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var value = await ErpDb.ScalarAsync(
            connection,
            null,
            $"SELECT CONCAT(status, '|', finished_at) FROM {RunsTable} ORDER BY id DESC LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        if (value is not string text || text.Length == 0)
        {
            return (null, null);
        }
        var parts = text.Split('|', 2);
        DateTimeOffset? at = parts.Length == 2 && long.TryParse(parts[1], out var ts) && ts > 0
            ? DateTimeOffset.FromUnixTimeSeconds(ts)
            : null;
        return (parts[0], at);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? NormalizeEnvUrl(string? url)
        => string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/');

    private static string Trim(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
