using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpPimActionRequest(
    string Action,
    long FieldId = 0,
    long OptionId = 0,
    string? Name = null,
    string? FieldType = null,
    string? Description = null,
    bool Required = false,
    bool ShowInventory = true,
    bool ShowSales = true,
    bool ShowPurchase = true,
    string? Options = null,
    string? OptionLabel = null);

/// <summary>Writes for the PIM attributes tab and the PIM values posted with an inventory item.</summary>
public interface IErpPimWriteService
{
    Task<ErpSimpleWriteResult> ApplyAsync(int adminId, ErpPimActionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ValidateItemPostAsync(IReadOnlyDictionary<string, IReadOnlyList<string>> post, string module, CancellationToken cancellationToken = default);

    Task<ErpPimCustomFields.SaveOutcome> SaveItemPostAsync(long itemId, IReadOnlyDictionary<string, IReadOnlyList<string>> post, string module, CancellationToken cancellationToken = default);
}

public sealed class ErpPimWriteService : IErpPimWriteService
{
    public static readonly IReadOnlyList<string> Actions = ["pim_create_field", "pim_delete_field", "pim_add_option", "pim_delete_option"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpPimWriteService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public async Task<ErpSimpleWriteResult> ApplyAsync(int adminId, ErpPimActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Actions.Contains(request.Action))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown PIM action.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpPimCustomFields.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            switch (request.Action)
            {
                case "pim_create_field":
                {
                    var type = ErpPimCustomFields.NormalizeType(request.FieldType);
                    var id = await ErpPimCustomFields.CreateFieldAsync(
                        connection,
                        new ErpPimCustomFields.FieldInput(
                            request.Name ?? "",
                            type,
                            request.Description ?? "",
                            request.Required,
                            request.ShowInventory,
                            request.ShowSales,
                            request.ShowPurchase,
                            request.Options ?? ""),
                        cancellationToken).ConfigureAwait(false);
                    var name = (request.Name ?? "").Trim();
                    await LogAsync(connection, adminId, request.Action, id, "PIM attribute created: " + name, new Dictionary<string, string?> { ["name"] = name, ["field_type"] = type }, cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("PIM attribute \"" + name + "\" created (type: " + type + ").", id);
                }

                case "pim_delete_field":
                    if (!await ErpPimCustomFields.DeactivateFieldAsync(connection, request.FieldId, cancellationToken).ConfigureAwait(false))
                    {
                        return ErpSimpleWriteResult.Fail("not_found", "Attribute not found.");
                    }

                    await LogAsync(connection, adminId, request.Action, request.FieldId, "PIM attribute deactivated", null, cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("PIM attribute deactivated.", request.FieldId);
                case "pim_add_option":
                {
                    var label = (request.OptionLabel ?? "").Trim();
                    var id = await ErpPimCustomFields.AddOptionAsync(connection, request.FieldId, label, cancellationToken).ConfigureAwait(false);
                    await LogAsync(connection, adminId, request.Action, request.FieldId, "PIM option added: " + label, new Dictionary<string, string?> { ["option_id"] = id.ToString(CultureInfo.InvariantCulture), ["label"] = label }, cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("Option \"" + label + "\" added.", id);
                }

                default:
                    if (!await ErpPimCustomFields.DeactivateOptionAsync(connection, request.OptionId, cancellationToken).ConfigureAwait(false))
                    {
                        return ErpSimpleWriteResult.Fail("not_found", "Option not found.");
                    }

                    await LogAsync(connection, adminId, request.Action, request.OptionId, "PIM option removed", null, cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("Option removed.", request.OptionId);
            }
        }
        catch (ErpWriteException ex)
        {
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> ValidateItemPostAsync(IReadOnlyDictionary<string, IReadOnlyList<string>> post, string module, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpPimCustomFields.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var fields = await ErpPimCustomFields.ListFieldsAsync(connection, module, cancellationToken).ConfigureAwait(false);
        return ErpPimCustomFields.ValidatePost(fields, post);
    }

    public async Task<ErpPimCustomFields.SaveOutcome> SaveItemPostAsync(long itemId, IReadOnlyDictionary<string, IReadOnlyList<string>> post, string module, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || itemId <= 0)
        {
            return new ErpPimCustomFields.SaveOutcome(0, []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpPimCustomFields.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        return await ErpPimCustomFields.SaveFromPostAsync(connection, itemId, post, module, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>pim_field_{id}</c> and <c>pim_field_{id}[]</c> keys of a form post, one list per field.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> PostFrom(IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> form)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, values) in form)
        {
            if (!key.StartsWith(ErpPimCustomFields.InputPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var name = key.EndsWith("[]", StringComparison.Ordinal) ? key[..^2] : key;
            if (!long.TryParse(name[ErpPimCustomFields.InputPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                continue;
            }

            map[name] = values.Select(v => v ?? "").ToList();
        }

        return map;
    }

    private Task LogAsync(System.Data.Common.DbConnection connection, int adminId, string action, long entityId, string summary, IReadOnlyDictionary<string, string?>? detail, CancellationToken cancellationToken)
        => _audit.LogAsync(connection, null, adminId, action, "pim_field", entityId, summary, detail, cancellationToken);
}
