using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_api_clients_manage.php</c> write twins: create, rotate, update, revoke / activate and
/// quota reset. Only the SHA-256 hash and the 24-char prefix of a key are stored; the plain key exists
/// once in the create / rotate result and is never persisted or re-readable.
/// </summary>
public interface ICpApiClientWriteService
{
    Task<ErpSimpleWriteResult> SetActiveAsync(long clientId, int active, CancellationToken cancellationToken = default);

    Task<CpApiClientKeyResult> CreateAsync(
        string label,
        string contactEmail,
        string product,
        long dailyLimit,
        IReadOnlyList<string> allowedActions,
        CancellationToken cancellationToken = default);

    Task<CpApiClientKeyResult> RotateAsync(long clientId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UpdateAsync(
        long clientId,
        string label,
        string contactEmail,
        string product,
        long dailyLimit,
        IReadOnlyList<string> allowedActions,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ResetQuotaAsync(long clientId, CancellationToken cancellationToken = default);
}

/// <summary>Write result that also carries the one-time plain key (create / rotate only).</summary>
public sealed record CpApiClientKeyResult(ErpSimpleWriteResult Write, string PlainKey)
{
    public static CpApiClientKeyResult Fail(string code, string message)
        => new(ErpSimpleWriteResult.Fail(code, message), string.Empty);
}

public sealed class CpApiClientWriteService : ICpApiClientWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpApiClientWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>epc_api_clients_make_key()</c> — product prefix plus 12 random bytes in hex.</summary>
    public static string MakeKey(string product)
        => (product == "price_pro" ? "epc_pricepro_" : "epc_catalog_") + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

    public static string HashKey(string plainKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainKey))).ToLowerInvariant();

    public static string KeyPrefix(string plainKey)
        => plainKey.Length <= 24 ? plainKey : plainKey[..24];

    public async Task<ErpSimpleWriteResult> SetActiveAsync(
        long clientId,
        int active,
        CancellationToken cancellationToken = default)
    {
        if (clientId <= 0 || active is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A client id and active 0 or 1 are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_api_clients` SET `active` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            active, Now(), clientId);
        return ErpSimpleWriteResult.Ok(
            active == 0 ? "Client revoked (active = 0)." : "Client re-activated.",
            clientId);
    }

    public async Task<CpApiClientKeyResult> CreateAsync(
        string label,
        string contactEmail,
        string product,
        long dailyLimit,
        IReadOnlyList<string> allowedActions,
        CancellationToken cancellationToken = default)
    {
        var name = (label ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return CpApiClientKeyResult.Fail("invalid", "Label is required.");
        }

        if (!_connections.IsConfigured)
        {
            return CpApiClientKeyResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var normalizedProduct = CpApiClientsDeskService.NormalizeProduct(product);
        var plain = MakeKey(normalizedProduct);
        var now = Now();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpApiClientsSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_api_clients` (`client_key_hash`, `client_key_prefix`, `product`, `label`, `contact_email`, "
                + "`active`, `daily_limit`, `calls_today`, `calls_reset_date`, `allowed_actions_json`, `time_created`, `time_updated`) "
                + "VALUES (?, ?, ?, ?, ?, 1, ?, 0, CURDATE(), ?, ?, ?)"),
            cancellationToken,
            HashKey(plain),
            KeyPrefix(plain),
            normalizedProduct,
            Clamp(name, 120),
            Clamp((contactEmail ?? string.Empty).Trim(), 190),
            CpApiClientsDeskService.NormalizeDailyLimit(dailyLimit),
            CpApiClientsDeskService.AllowedActionsJson(normalizedProduct, allowedActions),
            now,
            now);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new CpApiClientKeyResult(
            ErpSimpleWriteResult.Ok("Client created. Copy the key below — it will not be shown again.", id),
            plain);
    }

    public async Task<CpApiClientKeyResult> RotateAsync(long clientId, CancellationToken cancellationToken = default)
    {
        if (clientId <= 0)
        {
            return CpApiClientKeyResult.Fail("invalid", "A client id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return CpApiClientKeyResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var product = CpApiClientsDeskService.NormalizeProduct(await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `product` FROM `epc_api_clients` WHERE `id` = ?"),
            cancellationToken,
            clientId).ConfigureAwait(false));

        var plain = MakeKey(product);
        var written = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_api_clients` SET `client_key_hash` = ?, `client_key_prefix` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            HashKey(plain), KeyPrefix(plain), Now(), clientId).ConfigureAwait(false);

        return written == 0
            ? CpApiClientKeyResult.Fail("missing", "Client not found.")
            : new CpApiClientKeyResult(
                ErpSimpleWriteResult.Ok("Key rotated. Copy the new key below — it will not be shown again.", clientId),
                plain);
    }

    public async Task<ErpSimpleWriteResult> UpdateAsync(
        long clientId,
        string label,
        string contactEmail,
        string product,
        long dailyLimit,
        IReadOnlyList<string> allowedActions,
        CancellationToken cancellationToken = default)
    {
        if (clientId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A client id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var normalizedProduct = CpApiClientsDeskService.NormalizeProduct(product);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_api_clients` SET `label` = ?, `contact_email` = ?, `product` = ?, `daily_limit` = ?, "
                + "`allowed_actions_json` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            Clamp((label ?? string.Empty).Trim(), 120),
            Clamp((contactEmail ?? string.Empty).Trim(), 190),
            normalizedProduct,
            CpApiClientsDeskService.NormalizeDailyLimit(dailyLimit),
            CpApiClientsDeskService.AllowedActionsJson(normalizedProduct, allowedActions),
            Now(),
            clientId);

        return ErpSimpleWriteResult.Ok("Client updated.", clientId);
    }

    public async Task<ErpSimpleWriteResult> ResetQuotaAsync(long clientId, CancellationToken cancellationToken = default)
    {
        if (clientId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A client id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_api_clients` SET `calls_today` = 0, `calls_reset_date` = CURDATE(), `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            Now(),
            clientId);

        return ErpSimpleWriteResult.Ok("Daily quota reset.", clientId);
    }

    private static string Clamp(string value, int length)
        => value.Length <= length ? value : value[..length];

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
