using System.Globalization;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public sealed record ErpD365LinkWriteRequest(
    string? Action = null,
    string? FromTab = null,
    long FromId = 0,
    string? FromCode = null,
    string? ToTab = null,
    long ToId = 0,
    string? ToCode = null,
    string? Relation = null,
    string? Note = null,
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
    long CreatedBy = 0);

public interface IErpD365LinkWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365LinkWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpD365LinkWriteService : IErpD365LinkWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpD365WorkspaceWriteService _workspace;

    public ErpD365LinkWriteService(
        IErpWriteConnectionFactory connections,
        IErpD365WorkspaceWriteService workspace)
    {
        _connections = connections;
        _workspace = workspace;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365LinkWriteRequest request,
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

        ErpD365ProcessCatalog.TryGetEdge(request.FromTab, request.ToTab, out var edge);
        var action = (request.Action ?? "link").Trim().ToLowerInvariant();
        var toId = request.ToId;
        var toCode = Clip(request.ToCode, 40);
        if (action == "follow")
        {
            ErpD365WorkspaceCatalog.TryGet(request.ToTab, out var page);
            var copied = CopyMappedFields(request, edge.FieldMap);
            var created = await _workspace.SaveAsync(
                new ErpD365WorkspaceWriteRequest(
                    page?.Area,
                    request.ToTab,
                    copied.Code,
                    copied.Name,
                    copied.Worker,
                    copied.Party,
                    copied.Kind,
                    copied.Status,
                    copied.Period,
                    copied.DateFrom,
                    copied.DateTo,
                    copied.Qty,
                    copied.Amount,
                    copied.Extra,
                    copied.Note,
                    request.CreatedBy,
                    request.FromTab,
                    request.FromId,
                    request.FromCode,
                    edge.Relation),
                cancellationToken).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                return created;
            }

            return ErpSimpleWriteResult.Ok(
                "Followed " + edge.Relation + " to " + (page?.Label ?? request.ToTab),
                created.Id);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await InsertLinkAsync(
            connection,
            ProcessCodeFor(request.FromTab, request.ToTab),
            request.FromTab!,
            request.FromId,
            request.FromCode,
            request.ToTab!,
            toId,
            toCode,
            edge.Relation,
            edge.FieldMap,
            request.Note,
            request.CreatedBy,
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Linked " + edge.Relation, id);
    }

    public static ErpSimpleWriteResult? Validate(ErpD365LinkWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ErpD365WorkspaceCatalog.TryGet(request.FromTab, out _))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown source page.");
        }

        if (!ErpD365WorkspaceCatalog.TryGet(request.ToTab, out _))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown target page.");
        }

        if (!ErpD365ProcessCatalog.TryGetEdge(request.FromTab, request.ToTab, out var edge))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Those pages are not officially interlinked.");
        }

        var action = (request.Action ?? "link").Trim().ToLowerInvariant();
        if (action is not ("link" or "follow"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Use link or follow.");
        }

        if (action == "link" && request.FromId <= 0 && string.IsNullOrWhiteSpace(request.FromCode))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Source reference is required.");
        }

        if (action == "link" && request.ToId <= 0 && string.IsNullOrWhiteSpace(request.ToCode))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Target reference is required.");
        }

        if (action == "follow" && request.FromId <= 0 && string.IsNullOrWhiteSpace(request.FromCode))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Source reference is required to follow the process.");
        }

        _ = edge;
        return null;
    }

    internal static async Task TryLinkAfterSaveAsync(
        System.Data.Common.DbConnection connection,
        string? sourceTab,
        long sourceId,
        string? sourceCode,
        string toTab,
        long toId,
        string? toCode,
        string? relation,
        long createdBy,
        CancellationToken cancellationToken)
    {
        if (!ErpD365ProcessCatalog.TryGetEdge(sourceTab, toTab, out var edge))
        {
            return;
        }

        if (sourceId <= 0 && string.IsNullOrWhiteSpace(sourceCode))
        {
            return;
        }

        await InsertLinkAsync(
            connection,
            ProcessCodeFor(sourceTab, toTab),
            sourceTab ?? string.Empty,
            sourceId,
            sourceCode,
            toTab,
            toId,
            toCode,
            string.IsNullOrWhiteSpace(relation) ? edge.Relation : relation,
            edge.FieldMap,
            null,
            createdBy,
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<long> InsertLinkAsync(
        System.Data.Common.DbConnection connection,
        string processCode,
        string fromTab,
        long fromId,
        string? fromCode,
        string toTab,
        long toId,
        string? toCode,
        string relation,
        string fieldMap,
        string? note,
        long createdBy,
        CancellationToken cancellationToken)
    {
        await ErpLazySchema.EnsureD365LinkAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_d365_link` (" +
                "`company_id`,`process_code`,`from_tab`,`from_id`,`from_code`,`to_tab`,`to_id`,`to_code`," +
                "`relation`,`field_map`,`note`,`created_by`,`time_created`" +
                ") VALUES (1,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            Clip(processCode, 40),
            Clip(fromTab, 64),
            fromId,
            Clip(fromCode, 40),
            Clip(toTab, 64),
            toId,
            Clip(toCode, 40),
            Clip(relation, 24),
            Clip(fieldMap, 255),
            Clip(note, 255),
            createdBy,
            now);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    internal static string ProcessCodeFor(string? fromTab, string? toTab)
        => ErpD365ProcessCatalog.LanesForTab(fromTab)
            .Concat(ErpD365ProcessCatalog.LanesForTab(toTab))
            .Select(l => l.Code)
            .FirstOrDefault() ?? string.Empty;

    internal static ErpD365WorkspaceWriteRequest CopyMappedFields(
        ErpD365LinkWriteRequest request,
        string fieldMap)
    {
        var fields = ErpD365ProcessCatalog.ParseFieldMap(fieldMap);
        string? Take(string key, string? value) => fields.Contains(key) ? value : null;
        decimal TakeDec(string key, decimal value) => fields.Contains(key) ? value : 0;
        var code = Take("code", request.FromCode);
        if (string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(request.FromCode))
        {
            code = request.FromCode + "-" + (request.ToTab ?? "next");
        }

        return new ErpD365WorkspaceWriteRequest(
            Tab: request.ToTab,
            Code: code,
            Name: Take("name", request.Name),
            Worker: Take("worker", request.Worker),
            Party: Take("party", request.Party),
            Kind: Take("kind", request.Kind),
            Status: Take("status", request.Status),
            Period: Take("period", request.Period),
            DateFrom: Take("date_from", request.DateFrom),
            DateTo: Take("date_to", request.DateTo),
            Qty: TakeDec("qty", request.Qty),
            Amount: TakeDec("amount", request.Amount),
            Extra: Take("extra", request.Extra),
            Note: Take("note", request.Note));
    }

    private static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }
}
