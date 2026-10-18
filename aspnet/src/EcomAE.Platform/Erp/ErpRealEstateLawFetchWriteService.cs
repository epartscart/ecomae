using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Stamps the real-estate law pack and checks the public legislation pages.
/// A reached page is recorded. The pack text is not overwritten from HTML.
/// </summary>
public sealed record ErpRealEstateLawFetchBody(bool ConfirmWrites, bool Force);

public interface IErpRealEstateLawFetchWriteService
{
    Task<string> NoteAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> FetchAsync(bool force, CancellationToken cancellationToken = default);
}

public sealed class ErpRealEstateLawFetchWriteService : IErpRealEstateLawFetchWriteService
{
    private const string VersionKey = "re_law_version";
    private const string FetchedKey = "re_law_fetched_at";
    private const string NoteKey = "re_law_note";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpUaeFtaHttpClient _http;

    public ErpRealEstateLawFetchWriteService(IErpWriteConnectionFactory connections, IErpUaeFtaHttpClient http)
    {
        _connections = connections;
        _http = http;
    }

    public async Task<string> NoteAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return string.Empty;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(connection, NoteKey, string.Empty, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> FetchAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Tenant database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fetchedAt = await ReadAsync(connection, FetchedKey, "0", cancellationToken).ConfigureAwait(false);
        if (!force
            && long.TryParse(fetchedAt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stamp)
            && stamp > 0
            && now - stamp < 86_400)
        {
            var version = await ReadAsync(connection, VersionKey, ErpRealEstateLaw.PackVersion, cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(
                true,
                "ok",
                "Real-estate law pack " + version + " was stamped less than a day ago. Tick Force to check the public legislation pages again.",
                0,
                0);
        }

        var reached = new List<string>();
        var missed = new List<string>();
        foreach (var source in ErpRealEstateLaw.Sources)
        {
            var html = await _http.GetAsync(source.Url, cancellationToken).ConfigureAwait(false);
            if (html.Contains(source.Marker, StringComparison.OrdinalIgnoreCase))
            {
                reached.Add(source.Name);
            }
            else
            {
                missed.Add(source.Name);
            }
        }

        var message = "Real-estate law pack " + ErpRealEstateLaw.PackVersion + " is stamped. "
            + ErpRealEstateLaw.CivilCode + " remains the civil code, in force from " + ErpRealEstateLaw.CivilCodeFrom + ". "
            + (reached.Count > 0 ? "Reached: " + string.Join(", ", reached) + ". " : string.Empty)
            + (missed.Count > 0 ? "Not reached, so the built-in pack was kept: " + string.Join(", ", missed) + "." : "The pack text was not overwritten.");

        await WriteAsync(connection, VersionKey, ErpRealEstateLaw.PackVersion, cancellationToken).ConfigureAwait(false);
        await WriteAsync(connection, FetchedKey, now.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await WriteAsync(connection, NoteKey, message, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(message, 0);
    }

    private static async Task<string> ReadAsync(DbConnection connection, string key, string fallback, CancellationToken cancellationToken)
    {
        await ErpAmlShared.EnsureSettingsSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var value = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"),
            cancellationToken,
            key).ConfigureAwait(false);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static async Task WriteAsync(DbConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        await ErpAmlShared.EnsureSettingsSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_price_settings` (`setting_key`,`setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken,
            key,
            value).ConfigureAwait(false);
    }
}
