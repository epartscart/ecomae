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
    Task<IReadOnlyList<ErpD365StagedObject>> StagedObjectsAsync(DbConnection connection, string? entity, int limit, CancellationToken cancellationToken = default);
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

public sealed record ErpD365StagedObject(
    long Id,
    string Entity,
    string ExternalKey,
    string Payload,
    long RunId,
    long SyncedAt);

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

    /// <summary>Named HTTP client (60s timeout) registered in Program.cs.</summary>
    public const string HttpClientName = "ecomae-d365";

    /// <summary>Token-only HTTP client with auto-redirect disabled — a redirected password grant must never leave the token endpoint.</summary>
    public const string TokenHttpClientName = "ecomae-d365-token";

    private const string TokenPath = "/oauth2/v2.0/token";
    private const string RunsTable = "epc_d365_sync_runs";
    private const string ObjectsTable = "epc_d365_sync_objects";

    private readonly IHttpClientFactory _http;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly ILogger<ErpD365ConnectorService> _logger;
    private readonly string? _tenantId;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _username;
    private readonly string? _password;
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
        _username = FirstNonEmpty(configuration["D365:Username"], Environment.GetEnvironmentVariable("ECOMAE_D365_USERNAME"));
        // Passwords keep leading/trailing whitespace — FirstNonEmpty would silently corrupt them.
        _password = FirstNonEmptyRaw(configuration["D365:Password"], Environment.GetEnvironmentVariable("ECOMAE_D365_PASSWORD"));
        _environmentUrl = NormalizeEnvUrl(
            FirstNonEmpty(configuration["D365:EnvironmentUrl"], Environment.GetEnvironmentVariable("ECOMAE_D365_ENVIRONMENT_URL")));
    }

    /// <summary>Microsoft Azure CLI public client id — the well-known client that accepts ROPC grants when the tenant has not disabled them.</summary>
    private const string RopcClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

    /// <summary>True when either an Entra app registration (tenant+client+secret) or a service-account login (username+password) plus an environment URL is set.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_environmentUrl)
        && (HasClientCredentials || HasServiceAccount);

    private bool HasClientCredentials =>
        !string.IsNullOrWhiteSpace(_tenantId)
        && !string.IsNullOrWhiteSpace(_clientId)
        && !string.IsNullOrWhiteSpace(_clientSecret);

    private bool HasServiceAccount =>
        !string.IsNullOrWhiteSpace(_username)
        && !string.IsNullOrWhiteSpace(_password);

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
            return ErpSimpleWriteResult.Fail("not_configured", "D365 connection is not configured. Set D365:EnvironmentUrl plus either TenantId/ClientId/ClientSecret or Username/Password (or the ECOMAE_D365_* env vars).");
        }

        var token = await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return ErpSimpleWriteResult.Fail("auth_failed", "Entra token request failed — check the tenant id and the client credentials or the service-account username/password.");
        }

        try
        {
            using var client = _http.CreateClient(HttpClientName);
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
            "VALUES (?, 'pull', 'running', 0, 0, '', ?, 0, ?)",
            cancellationToken,
            entitySet,
            now,
            adminId).ConfigureAwait(false);
        runId = Convert.ToInt64(await ErpDb.ScalarAsync(connection, null, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false));

        var token = await AcquireTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            await FinishRunAsync(connection, runId, "auth_failed", 0, 0, "Entra token request failed", cancellationToken).ConfigureAwait(false);
            return new ErpD365PullResult(false, "auth_failed", "Entra token request failed — check credentials.", 0, 0, runId);
        }

        try
        {
            using var client = _http.CreateClient(HttpClientName);
            var rows = new List<JsonElement>();
            var nextLink = $"{_environmentUrl}/data/{entitySet}?$top={top}";
            while (nextLink is not null && rows.Count < top)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
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
                if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                {
                    rows.AddRange(value.EnumerateArray().Select(e => e.Clone()));
                }

                nextLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) && nl.ValueKind == JsonValueKind.String
                    ? nl.GetString()
                    : null;
            }

            if (rows.Count > top)
            {
                rows = rows.Take(top).ToList();
            }

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
                    "VALUES (?, ?, ?, ?, ?) " +
                    "ON DUPLICATE KEY UPDATE payload = VALUES(payload), run_id = VALUES(run_id), synced_at = VALUES(synced_at)",
                    cancellationToken,
                    entitySet,
                    key,
                    payload,
                    runId,
                    now).ConfigureAwait(false);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FinishRunAsync(connection, runId, "error", 0, 0, ex.Message, cancellationToken).ConfigureAwait(false);
            return new ErpD365PullResult(false, "error", ex.Message, 0, 0, runId);
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

    public async Task<IReadOnlyList<ErpD365StagedObject>> StagedObjectsAsync(DbConnection connection, string? entity, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = new List<ErpD365StagedObject>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT id, entity, ext_key, payload, run_id, synced_at FROM {ObjectsTable}" +
            (string.IsNullOrWhiteSpace(entity) ? string.Empty : " WHERE entity = @entity") +
            " ORDER BY synced_at DESC, id DESC LIMIT @limit";
        if (!string.IsNullOrWhiteSpace(entity))
        {
            var entityParam = command.CreateParameter();
            entityParam.ParameterName = "@entity";
            entityParam.Value = entity;
            command.Parameters.Add(entityParam);
        }
        var limitParam = command.CreateParameter();
        limitParam.ParameterName = "@limit";
        limitParam.Value = Math.Clamp(limit, 1, 500);
        command.Parameters.Add(limitParam);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpD365StagedObject(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                reader.GetInt64(4),
                reader.GetInt64(5)));
        }
        return rows;
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
            using var client = _http.CreateClient(TokenHttpClientName);
            // Prefer the Entra app registration only when the full tuple exists; otherwise use a
            // resource-owner password grant. A configured confidential ClientId cannot do a public-client
            // password grant, so the fallback always uses the well-known Azure CLI public client id.
            var fields = HasClientCredentials
                ? new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _clientId!,
                    ["client_secret"] = _clientSecret!,
                    ["scope"] = $"{_environmentUrl}/.default",
                }
                : new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = RopcClientId,
                    ["username"] = _username!,
                    ["password"] = _password!,
                    ["scope"] = $"{_environmentUrl}/.default",
                };
            using var form = new FormUrlEncodedContent(fields);
            var authority = string.IsNullOrWhiteSpace(_tenantId) ? "organizations" : _tenantId;
            var tokenEndpoint = new Uri($"https://login.microsoftonline.com/{authority}{TokenPath}");
            using var response = await client.PostAsync(tokenEndpoint, form, cancellationToken).ConfigureAwait(false);
            // The client is registered with AllowAutoRedirect=false, so a redirect response means the
            // endpoint tried to move the request — refuse instead of leaking the grant body cross-host.
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                _logger.LogWarning("D365 token request was redirected ({Status}) — refusing to follow", (int)response.StatusCode);
                return null;
            }
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

    /// <summary>Composite staging key: company (dataAreaId) plus every business key present, so records can never collide across companies.</summary>
    private static string ExternalKeyOf(JsonElement row)
    {
        var parts = new List<string>();
        foreach (var candidate in new[] { "dataAreaId", "ItemNumber", "CustomerAccount", "VendorAccount", "SalesOrderNumber", "PurchaseOrderNumber", "InvoiceNumber", "MainAccountId", "CompanyId" })
        {
            if (row.TryGetProperty(candidate, out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } s)
            {
                parts.Add(s);
            }
        }
        if (parts.Count == 0)
        {
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(row.GetRawText())))[..40];
        }
        var key = string.Join('|', parts);
        return key.Length > 200 ? key[..200] : key;
    }

    private async Task FinishRunAsync(DbConnection connection, long runId, string status, int fetched, int staged, string error, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            $"UPDATE {RunsTable} SET status = ?, fetched = ?, staged = ?, error = ?, finished_at = ? WHERE id = ?",
            cancellationToken,
            status,
            fetched,
            staged,
            error.Length > 65000 ? error[..65000] : error,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            runId).ConfigureAwait(false);
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

    /// <summary>Like <see cref="FirstNonEmpty"/> but preserves whitespace — for secrets where spaces are meaningful.</summary>
    private static string? FirstNonEmptyRaw(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Environment must be an https URL on a *.dynamics.com host — anything else is rejected so the Entra bearer token can never be sent to an untrusted endpoint.</summary>
    private static string? NormalizeEnvUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            || !uri.Host.EndsWith(".dynamics.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string Trim(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
