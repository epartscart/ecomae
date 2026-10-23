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

    /// <summary>Persisted on/off switch for the live connector (epc_erp_d365_parameters area='sync', key='enabled'). Off by default — pulls and applies refuse while off.</summary>
    Task<bool> SyncEnabledAsync(DbConnection connection, CancellationToken cancellationToken = default);
    Task SetSyncEnabledAsync(DbConnection connection, bool enabled, CancellationToken cancellationToken = default);
}

public sealed record ErpD365ConnectionStatus(
    bool Configured,
    string? EnvironmentUrl,
    string? LastRunStatus,
    DateTimeOffset? LastRunAt,
    int StagedObjectCount,
    bool SyncEnabled);

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
            ["SalesOrderLines"] = "Sales order lines",
            ["PurchaseOrderHeadersV2"] = "Purchase order headers",
            ["PurchaseOrderLinesV2"] = "Purchase order lines",
            ["SalesInvoiceHeadersV2"] = "Sales invoice headers",
            ["SalesInvoiceLines"] = "Sales invoice lines",
            ["VendorInvoiceHeaders"] = "Purchase invoice headers",
            ["VendorInvoiceLines"] = "Purchase invoice lines",
            ["CustomerPaymentJournalHeaders"] = "Receipt vouchers (customer payments) — headers",
            ["CustomerPaymentJournalLines"] = "Receipt vouchers (customer payments) — lines",
            ["VendorPaymentJournalHeaders"] = "Payment vouchers (vendor payments) — headers",
            ["VendorPaymentJournalLines"] = "Payment vouchers (vendor payments) — lines",
            ["LedgerJournalHeaders"] = "General journal — headers",
            ["LedgerJournalLines"] = "General journal — lines",
            ["GeneralLedgerBalanceCDREntities"] = "GL balances by dimension (trial balance source)",
            ["TrialBalanceFiscalYearSnapshots"] = "Trial balance snapshots (per dimension set)",
            ["WarehousesOnHandV2"] = "Inventory on-hand by warehouse",
            ["GeneralJournalEntryBiEntities"] = "Posted GL vouchers — headers",
            ["GeneralJournalAccountEntryBiEntities"] = "Posted GL entries — lines (TB detail)",
            ["CustTransBiEntities"] = "Posted customer transactions",
            ["VendTransBiEntities"] = "Posted vendor transactions",
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
    private const string CursorsTable = "epc_d365_pull_cursors";
    /// <summary>One pull call stays under the web proxy timeout — the cursor resumes on the next call.</summary>
    private static readonly TimeSpan PullBudget = TimeSpan.FromSeconds(75);
    /// <summary>OData page size per request — F&O accepts large tops; 1000 keeps each page fast.</summary>
    private const int PageSize = 1000;

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
        var enabled = await SyncEnabledAsync(connection, cancellationToken).ConfigureAwait(false);
        return new ErpD365ConnectionStatus(IsConfigured, _environmentUrl, status, at, staged, enabled);
    }

    public async Task<bool> SyncEnabledAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await ErpLazySchema.EnsureD365ParametersAsync(connection, cancellationToken).ConfigureAwait(false);
        var v = Convert.ToString(await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `param_value` FROM `epc_erp_d365_parameters` WHERE `company_id` = 1 AND `area` = 'sync' AND `param_key` = 'enabled'"),
            cancellationToken).ConfigureAwait(false)) ?? "";
        return v == "1";
    }

    public async Task SetSyncEnabledAsync(DbConnection connection, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await ErpLazySchema.EnsureD365ParametersAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_d365_parameters` (`company_id`,`area`,`param_key`,`caption`,`param_value`,`value_kind`,`created_by`,`time_created`,`time_updated`) " +
                "VALUES (1,'sync','enabled','D365 sync enabled',?,'flag',0,?,?) " +
                "ON DUPLICATE KEY UPDATE `param_value`=VALUES(`param_value`),`time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            enabled ? "1" : "0",
            now,
            now).ConfigureAwait(false);
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

        // top=0 means "all rows" — big entities stream over repeated calls via the pull cursor.
        top = top <= 0 ? int.MaxValue : Math.Clamp(top, 1, int.MaxValue);
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

        // Resume: a finished or never-started pull begins at the first page; a partial pull
        // continues from the stored nextLink. Each page stages in its own transaction together
        // with the cursor so a killed request never loses or double-fetches committed work.
        var resumeUrl = await ErpDb.ScalarAsync(
            connection,
            null,
            $"SELECT next_url FROM {CursorsTable} WHERE entity = ? AND done = 0",
            cancellationToken,
            entitySet).ConfigureAwait(false) as string;

        var budget = System.Diagnostics.Stopwatch.StartNew();
        var fetched = 0;
        var staged = 0;
        var done = false;
        try
        {
            using var client = _http.CreateClient(HttpClientName);
            var envUri = new Uri(_environmentUrl!);
            var nextLink = !string.IsNullOrEmpty(resumeUrl)
                ? resumeUrl
                : $"{_environmentUrl}/data/{entitySet}?$top={PageSize}&$count=true";
            // A stored cursor gets the same check as a fresh nextLink — the bearer token
            // must never go to a destination we did not verify this run.
            if (!IsSafeContinuation(envUri, nextLink))
            {
                nextLink = $"{_environmentUrl}/data/{entitySet}?$top={PageSize}&$count=true";
            }
            long remaining = top;
            long skip = 0;
            long? totalCount = null;
            while (nextLink is not null && fetched < top)
            {
                // Re-check the persisted switch each page — switching sync off mid-pull stops
                // further staging instead of letting the in-flight loop run to its page budget.
                if (!await SyncEnabledAsync(connection, cancellationToken).ConfigureAwait(false))
                {
                    var offMsg = $"D365 sync switched off mid-pull — stopped after {fetched} row(s).";
                    await FinishRunAsync(connection, runId, "cancelled", fetched, staged, offMsg, cancellationToken).ConfigureAwait(false);
                    return new ErpD365PullResult(false, "sync_off", offMsg, fetched, staged, runId);
                }
                var skipMatch = System.Text.RegularExpressions.Regex.Match(nextLink, @"[$]skip=(\d+)");
                if (skipMatch.Success)
                {
                    skip = long.Parse(skipMatch.Groups[1].Value);
                }
                using var request = new HttpRequestMessage(HttpMethod.Get, nextLink);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var error = $"HTTP {(int)response.StatusCode}: {Trim(body, 300)}";
                    await FinishRunAsync(connection, runId, "error", fetched, staged, error, cancellationToken).ConfigureAwait(false);
                    return new ErpD365PullResult(false, "odata_error", error, fetched, staged, runId);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var pageRows = doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array
                    ? value.EnumerateArray().Select(e => e.Clone()).ToList()
                    : new List<JsonElement>();
                var fullPageCount = pageRows.Count;
                if (pageRows.Count > remaining)
                {
                    pageRows = pageRows.Take((int)remaining).ToList();
                }
                var followLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) && nl.ValueKind == JsonValueKind.String
                    ? nl.GetString()
                    : null;
                // Only follow a continuation back to the configured F&O endpoint — an
                // off-host, non-https, or different-port link must not receive the token.
                if (!IsSafeContinuation(envUri, followLink))
                {
                    followLink = null;
                }
                // SHAM's F&O returns no @odata.nextLink at all and caps each page below
                // our $top, so page manually with $skip while rows remain.
                if (doc.RootElement.TryGetProperty("@odata.count", out var oc) && oc.ValueKind == JsonValueKind.Number)
                {
                    totalCount = oc.GetInt64();
                }
                if (followLink is null && pageRows.Count > 0)
                {
                    var absolute = skip + fullPageCount;
                    var more = totalCount.HasValue ? absolute < totalCount.Value : pageRows.Count == PageSize;
                    if (more)
                    {
                        followLink = $"{_environmentUrl}/data/{entitySet}?$top={PageSize}&$count=true&$skip={absolute}";
                    }
                }

                // Stage this page + advance the cursor atomically. Rows go in as one
                // multi-row INSERT per chunk — a per-row INSERT costs most of the pull
                // budget on large entities (SO/SI lines run ~1M rows).
                var tuples = new List<object?>(pageRows.Count * 7);
                var marks = new List<string>(pageRows.Count);
                foreach (var row in pageRows)
                {
                    var key = ExternalKeyOf(row);
                    var payload = row.GetRawText();
                    var journalNo = JournalScopedNo(row);
                    var lineRef = Str(row, "LineNumber", "JournalLineNumber", "InvoiceLineNumber", "VoucherLineNumber");
                    marks.Add("(?, ?, ?, ?, ?, ?, ?)");
                    tuples.Add(entitySet);
                    tuples.Add(key);
                    tuples.Add(payload);
                    tuples.Add(runId);
                    tuples.Add(now);
                    tuples.Add(journalNo);
                    tuples.Add(lineRef.Length > 64 ? lineRef[..64] : lineRef);
                    staged++;
                }
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                for (var off = 0; off < marks.Count; off += 200)
                {
                    var take = Math.Min(200, marks.Count - off);
                    var sql = $"INSERT INTO {ObjectsTable} (entity, ext_key, payload, run_id, synced_at, journal_no, line_ref) VALUES " +
                              string.Join(", ", marks.GetRange(off, take)) +
                              " ON DUPLICATE KEY UPDATE payload = VALUES(payload), run_id = VALUES(run_id), synced_at = VALUES(synced_at), journal_no = VALUES(journal_no), line_ref = VALUES(line_ref), applied_at = IF(payload <> VALUES(payload), 0, applied_at)";
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        sql,
                        cancellationToken,
                        tuples.GetRange(off * 7, take * 7).ToArray()).ConfigureAwait(false);
                }
                fetched += pageRows.Count;
                remaining -= pageRows.Count;
                // "Done" means F&O has no more rows — hitting the caller's `top`
                // limit mid-entity keeps the cursor so a later pull resumes here
                // instead of losing the rest of the page.
                var finished = followLink is null;
                var cursorUrl = finished ? null : followLink;
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    $"INSERT INTO {CursorsTable} (entity, next_url, done, updated_at) VALUES (?, ?, ?, ?) " +
                    "ON DUPLICATE KEY UPDATE next_url = VALUES(next_url), done = VALUES(done), updated_at = VALUES(updated_at)",
                    cancellationToken,
                    entitySet,
                    cursorUrl,
                    finished ? 1 : 0,
                    now).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                if (finished)
                {
                    done = true;
                    break;
                }
                nextLink = followLink;
                if (remaining <= 0)
                {
                    break; // caller's row limit — cursor stored; next pull resumes
                }
                if (budget.Elapsed > PullBudget)
                {
                    break; // partial — cursor stored; caller re-invokes to continue
                }
            }

            var status = done ? "ok" : "partial";
            var message = done
                ? $"Pulled {fetched} {label} row(s) into staging."
                : $"Pulled {fetched} {label} row(s) this pass — more available; run the pull again to continue.";
            await FinishRunAsync(connection, runId, status, fetched, staged, string.Empty, cancellationToken).ConfigureAwait(false);
            return new ErpD365PullResult(true, status, message, fetched, staged, runId);
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
            "applied_at BIGINT NOT NULL DEFAULT 0," +
            "journal_no VARCHAR(64) NOT NULL DEFAULT ''," +
            "line_ref VARCHAR(64) NOT NULL DEFAULT ''," +
            "UNIQUE KEY uq_entity_ext (entity, ext_key)," +
            "KEY ix_entity_applied (entity, applied_at)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);
        // Columns added after first release — ALTERs are no-ops once present (MySQL DDL commits anyway,
        // so they must stay outside any transaction).
        await ErpDb.TryExecuteAsync(connection,
            $"ALTER TABLE {ObjectsTable} ADD `applied_at` BIGINT NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            $"ALTER TABLE {ObjectsTable} ADD `journal_no` VARCHAR(64) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            $"ALTER TABLE {ObjectsTable} ADD `line_ref` VARCHAR(64) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            $"ALTER TABLE {ObjectsTable} ADD KEY `ix_entity_applied` (`entity`, `applied_at`)",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            $"CREATE TABLE IF NOT EXISTS {CursorsTable} (" +
            "entity VARCHAR(128) NOT NULL PRIMARY KEY," +
            "next_url MEDIUMTEXT NULL," +
            "done TINYINT(1) NOT NULL DEFAULT 0," +
            "updated_at BIGINT NOT NULL" +
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
        foreach (var candidate in new[]
        {
            // Business keys — same document stays one staged row on re-pull.
            "dataAreaId", "ItemNumber", "CustomerAccount", "CustomerAccountNumber", "VendorAccount", "VendorAccountNumber", "SalesOrderNumber", "PurchaseOrderNumber", "InvoiceNumber", "JournalBatchNumber", "Voucher", "MainAccountId", "CompanyId",
            // Line/dimension/period discriminators — distinct rows sharing the same document key must not overwrite each other.
            "LineNumber", "JournalLineNumber", "InvoiceLineNumber", "VoucherLineNumber", "LineRef", "RecId", "LineId", "SourceKey",
            "InventorySiteId", "InventoryWarehouseId", "ProductColorId", "ProductSizeId", "ProductStyleId", "ProductVersionId", "ProductConfigurationId",
            "SnapshotHeader", "DimensionHierarchyId", "FocusDimensionHierarchy", "PostingLayer", "PeriodName", "PeriodType", "YearName", "LedgerRecId", "LedgerName",
            // TB snapshot grain is account × dimension values × period — without these every
            // row in a period collapses onto one staged key (15k rows → 528 keys on SHAM).
            "DimensionValue1", "DimensionValue2", "DimensionValue3", "DimensionValue4", "DimensionValue5",
            "DimensionValue6", "DimensionValue7", "DimensionValue8", "DimensionValue9", "DimensionValue10", "DimensionValue11",
            "LedgerDimension", "Dimension", "DimensionSet", "AccountDisplayValue",
            "FiscalYear", "FiscalPeriod", "Period", "AccountingDate",
        })
        {
            if (row.TryGetProperty(candidate, out var p))
            {
                // F&O sends ids like LineNumber as JSON numbers, not strings — accept both.
                if (p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } s)
                {
                    parts.Add(s);
                }
                else if (p.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    parts.Add(p.GetRawText());
                }
            }
        }
        if (parts.Count == 0)
        {
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(row.GetRawText())))[..40];
        }
        var key = string.Join('|', parts);
        if (key.Length <= 200)
        {
            return key;
        }
        // Keep a hash suffix so over-width keys never collapse distinct rows (e.g. snapshot
        // dimension sets whose trailing DimensionValueN differ past the 200-char cut).
        var keyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..8].ToLowerInvariant();
        return key[..191] + "|" + keyHash;
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

    /// <summary>Continuation URLs the bearer token may be sent to: absolute https on the configured host AND port.</summary>
    private static bool IsSafeContinuation(Uri envUri, string? url)
        => url is not null
           && Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
           && uri.Host.Equals(envUri.Host, StringComparison.OrdinalIgnoreCase)
           && uri.Port == envUri.Port;

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

    /// <summary>Reads a non-empty string/number field from a staged F&O row (same semantics as the mapper).</summary>
    private static string Str(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString() ?? string.Empty;
                    if (text.Trim().Length > 0)
                    {
                        return text.Trim();
                    }
                }
                else if (value.ValueKind == JsonValueKind.Number)
                {
                    return value.GetRawText();
                }
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// The journal's company-scoped number for journal-LINE staged rows (JournalBatchNumber|dataAreaId),
    /// identical to what ApplyJournalHeaderAsync writes on gl_journals.journal_no — lets reconcile join
    /// stored GL lines to staged line refs in pure SQL. Empty for rows without a journal number.
    /// </summary>
    private static string JournalScopedNo(JsonElement row)
    {
        var natural = Str(row, "JournalNumber", "JournalBatchNumber", "JournalId", "Voucher");
        if (natural.Length == 0)
        {
            return string.Empty;
        }
        var company = Str(row, "dataAreaId", "CompanyId");
        var scoped = company.Length > 0 ? company + "|" + natural : natural;
        const int max = 32; // matches gl_journals.journal_no clipping in ApplyJournalHeaderAsync
        if (scoped.Length <= max)
        {
            return scoped;
        }
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(scoped)))[..8].ToLowerInvariant();
        var room = max - hash.Length - 1;
        return room > 0 ? scoped[..room] + "-" + hash : hash;
    }

    private static string Trim(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}
