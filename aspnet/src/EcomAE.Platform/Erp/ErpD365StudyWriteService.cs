using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public sealed record ErpD365StudyWriteRequest(
    string? Area = null,
    string? Topic = null,
    string? Note = null,
    long CreatedBy = 0);

public interface IErpD365StudyWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365StudyWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpD365StudyWriteService : IErpD365StudyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpD365StudyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365StudyWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = Validate(request);
        if (check is not null)
        {
            return check;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        ErpD365ModuleStudyCatalog.TryGet(request.Area, out var study);
        var topic = (request.Topic ?? string.Empty).Trim().ToLowerInvariant();
        var note = Clip(request.Note, 2000);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureD365StudyAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_d365_study` (" +
                "`company_id`,`area`,`topic`,`note`,`created_by`,`time_created`,`time_updated`" +
                ") VALUES (1,?,?,?,?,?,?) " +
                "ON DUPLICATE KEY UPDATE `note`=VALUES(`note`),`time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            study.Area,
            topic,
            note,
            request.CreatedBy,
            now,
            now);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Saved " + topic + " note for " + study.Label, id);
    }

    public static ErpSimpleWriteResult? Validate(ErpD365StudyWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ErpD365ModuleStudyCatalog.TryGet(request.Area, out _))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown module study.");
        }

        if (!ErpD365ModuleStudyCatalog.IsOfficialTopic(request.Topic))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Topic must be functionality, setup, configuration, process, or customization.");
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Study note is required.");
        }

        return null;
    }

    private static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }
}
