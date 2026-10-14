using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

public sealed record ErpD365ParameterWriteRequest(
    string? Area = null,
    string? Key = null,
    string? Value = null,
    long CreatedBy = 0);

public interface IErpD365ParameterWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365ParameterWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpD365ParameterWriteService : IErpD365ParameterWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpD365ParameterWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpD365ParameterWriteRequest request,
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

        ErpD365ProcessCatalog.TryGetParameter(request.Area, request.Key, out var parameter);
        var value = NormalizeValue(request.Value, parameter);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureD365ParametersAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_d365_parameters` (" +
                "`company_id`,`area`,`param_key`,`caption`,`param_value`,`value_kind`,`param_group`," +
                "`created_by`,`time_created`,`time_updated`" +
                ") VALUES (1,?,?,?,?,?,?,?,?,?) " +
                "ON DUPLICATE KEY UPDATE `caption`=VALUES(`caption`),`param_value`=VALUES(`param_value`)," +
                "`value_kind`=VALUES(`value_kind`),`param_group`=VALUES(`param_group`)," +
                "`time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            parameter.Area,
            parameter.Key,
            parameter.Caption,
            value,
            parameter.ValueKind,
            parameter.Group,
            request.CreatedBy,
            now,
            now);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Saved " + parameter.Caption, id);
    }

    public static ErpSimpleWriteResult? Validate(ErpD365ParameterWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ErpD365ProcessCatalog.TryGetParameter(request.Area, request.Key, out var parameter))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown official parameter.");
        }

        var value = (request.Value ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", parameter.Caption + " is required.");
        }

        if (parameter.ValueKind == "yesno")
        {
            var norm = value.ToLowerInvariant();
            if (norm is not ("yes" or "no" or "1" or "0" or "true" or "false"))
            {
                return ErpSimpleWriteResult.Fail("invalid", parameter.Caption + " must be yes or no.");
            }
        }

        if (parameter.ValueKind == "list" && parameter.Options is { Count: > 0 }
            && !parameter.Options.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", parameter.Caption + " is not an official option.");
        }

        if (parameter.ValueKind == "number" && !decimal.TryParse(value, out _))
        {
            return ErpSimpleWriteResult.Fail("invalid", parameter.Caption + " must be a number.");
        }

        return null;
    }

    public static string NormalizeValue(string? raw, ErpD365ProcessCatalog.ParameterDef parameter)
    {
        var value = (raw ?? string.Empty).Trim();
        if (parameter.ValueKind == "yesno")
        {
            var norm = value.ToLowerInvariant();
            return norm is "yes" or "1" or "true" ? "yes" : "no";
        }

        if (parameter.ValueKind == "list" && parameter.Options is { Count: > 0 })
        {
            return parameter.Options.FirstOrDefault(o => o.Equals(value, StringComparison.OrdinalIgnoreCase))
                ?? parameter.Default;
        }

        return value.Length <= 120 ? value : value[..120];
    }
}
