namespace EcomAE.Platform.Erp;

/// <summary>
/// Stamps the workspace AML pack and checks the FATF public list.
/// A reached page is recorded. Country lists are not overwritten from HTML.
/// When the page is not reached, the built-in pack stays in force.
/// </summary>
public sealed record ErpAmlLegislationFetchBody(bool ConfirmWrites, bool Force);

public interface IErpAmlLegislationFetchWriteService
{
    Task<ErpSimpleWriteResult> FetchAsync(bool force, CancellationToken cancellationToken = default);
}

public sealed class ErpAmlLegislationFetchWriteService : IErpAmlLegislationFetchWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpUaeFtaHttpClient _http;

    public ErpAmlLegislationFetchWriteService(IErpWriteConnectionFactory connections, IErpUaeFtaHttpClient http)
    {
        _connections = connections;
        _http = http;
    }

    public async Task<ErpSimpleWriteResult> FetchAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Tenant database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fetchedAt = await ErpAmlShared.SettingGetAsync(connection, "legislation_fetched_at", "0", cancellationToken).ConfigureAwait(false);
        if (!force
            && long.TryParse(fetchedAt, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var stamp)
            && stamp > 0
            && now - stamp < 86_400)
        {
            var version = await ErpAmlShared.SettingGetAsync(connection, "legislation_version", ErpAmlProgramme.PackVersion, cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(
                true,
                "ok",
                "AML pack " + version + " was stamped less than a day ago. Tick Force to check the FATF public list again.",
                0,
                0);
        }

        var source = ErpAmlProgramme.OfficialSources[0];
        var html = await _http.GetAsync(source.Url, cancellationToken).ConfigureAwait(false);
        var reached = html.Contains(source.Marker, StringComparison.OrdinalIgnoreCase);
        var message = reached
            ? "AML pack " + ErpAmlProgramme.PackVersion + " is stamped. The FATF public list was reached and still names " + source.Marker + ". Country lists in this workspace were not overwritten. " + ErpAmlProgramme.Decree + " and " + ErpAmlProgramme.Resolution + " remain the programme."
            : "AML pack " + ErpAmlProgramme.PackVersion + " is stamped from the workspace library. The FATF public list was not reached, so the built-in countermeasure, call-for-action, and increased-monitoring lists were kept.";

        await ErpAmlShared.SettingSetAsync(connection, "legislation_version", ErpAmlProgramme.PackVersion, cancellationToken).ConfigureAwait(false);
        await ErpAmlShared.SettingSetAsync(connection, "legislation_fetched_at", now.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        await ErpAmlShared.SettingSetAsync(connection, "legislation_note", message, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(message, 0);
    }
}
