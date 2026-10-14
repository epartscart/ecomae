using System.Globalization;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public sealed record ErpD365WorkspaceWriteRequest(
    string? Area = null,
    string? Tab = null,
    string? Code = null,
    string? Name = null,
    string? Worker = null,
    string? Party = null,
    string? Kind = null,
    string? Status = null,
    string? Period = null,
    string? DateFrom = null,
    string? DateTo = null,
    decimal Qty = 0,
    decimal Amount = 0,
    string? Extra = null,
    string? Note = null,
    long CreatedBy = 0,
    string? SourceTab = null,
    long SourceId = 0,
    string? SourceCode = null,
    string? Relation = null);

public interface IErpD365WorkspaceWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365WorkspaceWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpD365WorkspaceWriteService : IErpD365WorkspaceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpD365WorkspaceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365WorkspaceWriteRequest request,
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

        ErpD365WorkspaceCatalog.TryGet(request.Tab, out var page);
        var area = Clip((request.Area ?? page?.Area) ?? string.Empty, 40);
        var tab = Clip(request.Tab, 64);
        var code = Clip(request.Code, 40);
        var name = Clip(request.Name, 190);
        var worker = Clip(request.Worker, 120);
        var party = Clip(request.Party, 160);
        var kind = Clip(request.Kind, 40);
        var status = NormalizeStatus(request.Status, page?.Kind ?? ErpD365Part3Catalog.FormKind.Master);
        var period = Clip(request.Period, 24);
        var dateFrom = Clip(request.DateFrom, 16);
        var dateTo = Clip(request.DateTo, 16);
        var extra = Clip(request.Extra, 2000);
        var note = Clip(request.Note, 255);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureD365WorkspaceAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_d365_workspace` (" +
                "`company_id`,`area`,`tab`,`code`,`name`,`worker`,`party`,`kind`,`status`,`period`," +
                "`date_from`,`date_to`,`qty`,`amount`,`extra`,`note`,`created_by`,`time_created`,`time_updated`" +
                ") VALUES (1,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            area, tab, code, name, worker, party, kind, status, period,
            dateFrom, dateTo, request.Qty, request.Amount, extra, note, request.CreatedBy, now, now);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id > 0 && !string.IsNullOrWhiteSpace(request.SourceTab))
        {
            await ErpD365LinkWriteService.TryLinkAfterSaveAsync(
                connection,
                request.SourceTab,
                request.SourceId,
                request.SourceCode,
                tab,
                id,
                code,
                request.Relation,
                request.CreatedBy,
                cancellationToken).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Saved " + (page?.Label ?? tab), id);
    }

    public static ErpSimpleWriteResult? Validate(ErpD365WorkspaceWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ErpD365WorkspaceCatalog.TryGet(request.Tab, out var page))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown workspace page.");
        }

        if (page.ExistingHref is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This page already has a dedicated form.");
        }

        return page.Kind switch
        {
            ErpD365Part3Catalog.FormKind.Request when string.IsNullOrWhiteSpace(request.Worker)
                => ErpSimpleWriteResult.Fail("invalid", "Worker is required."),
            ErpD365Part3Catalog.FormKind.Plan or ErpD365Part3Catalog.FormKind.Master
                when string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)
                => ErpSimpleWriteResult.Fail("invalid", "Code and name are required."),
            ErpD365Part3Catalog.FormKind.Calendar when string.IsNullOrWhiteSpace(request.Name)
                => ErpSimpleWriteResult.Fail("invalid", "Calendar name is required."),
            ErpD365Part3Catalog.FormKind.Order when string.IsNullOrWhiteSpace(request.Name)
                => ErpSimpleWriteResult.Fail("invalid", "Item is required."),
            ErpD365Part3Catalog.FormKind.Forecast when string.IsNullOrWhiteSpace(request.Name)
                => ErpSimpleWriteResult.Fail("invalid", "Item is required."),
            ErpD365Part3Catalog.FormKind.Queue when string.IsNullOrWhiteSpace(request.Code)
                => ErpSimpleWriteResult.Fail("invalid", "Queue reference is required."),
            ErpD365Part3Catalog.FormKind.Action when string.IsNullOrWhiteSpace(request.Code) && string.IsNullOrWhiteSpace(request.Name)
                => ErpSimpleWriteResult.Fail("invalid", "Reference or name is required."),
            ErpD365Part3Catalog.FormKind.Payroll when string.IsNullOrWhiteSpace(request.Worker)
                => ErpSimpleWriteResult.Fail("invalid", "Worker is required."),
            ErpD365Part3Catalog.FormKind.Procurement when string.IsNullOrWhiteSpace(request.Party) && string.IsNullOrWhiteSpace(request.Name)
                => ErpSimpleWriteResult.Fail("invalid", "Vendor or item is required."),
            _ => null,
        };
    }

    public static string NormalizeStatus(string? raw, ErpD365Part3Catalog.FormKind kind)
    {
        var status = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (status.Length == 0)
        {
            return kind switch
            {
                ErpD365Part3Catalog.FormKind.Request => "pending",
                ErpD365Part3Catalog.FormKind.Action => "queued",
                ErpD365Part3Catalog.FormKind.Order => "planned",
                _ => "active",
            };
        }

        return status.Length <= 24 ? status : status[..24];
    }

    private static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }
}
