using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpSyncronActionRequest(
    string Action,
    ErpSyncronPolicy.Policy? Policy = null,
    long PolicyId = 0,
    long WarehouseId = 0,
    long ItemId = 0,
    string? PeriodMonth = null,
    decimal DemandQty = 0m,
    decimal FulfilledQty = 0m,
    int StockoutEvents = 0);

/// <summary>Writes for <c>/erp/syncron-app</c>: policy save/deactivate, forecast run and service-level entry.</summary>
public interface IErpSyncronWriteService
{
    Task<ErpSimpleWriteResult> ApplyAsync(int adminId, ErpSyncronActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ErpSyncronWriteService : IErpSyncronWriteService
{
    public static readonly IReadOnlyList<string> Actions = ["policy_save", "policy_deactivate", "run_forecast", "record_service_level"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;
    private readonly TimeProvider _clock;

    public ErpSyncronWriteService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit, TimeProvider? clock = null)
    {
        _connections = connections;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ErpSimpleWriteResult> ApplyAsync(int adminId, ErpSyncronActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Actions.Contains(request.Action))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown Syncron action.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpSyncronPolicy.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            switch (request.Action)
            {
                case "policy_save":
                {
                    if (request.Policy is null)
                    {
                        return ErpSimpleWriteResult.Fail("invalid", "Policy is required.");
                    }

                    var id = await ErpSyncronPolicy.SavePolicyAsync(connection, request.Policy, cancellationToken).ConfigureAwait(false);
                    if (id == 0)
                    {
                        return ErpSimpleWriteResult.Fail("not_found", "Policy not found.");
                    }

                    var verb = request.Policy.Id > 0 ? "updated" : "created";
                    await LogAsync(connection, adminId, request.Action, id, "Inventory policy " + verb + ": " + request.Policy.PolicyName.Trim(), new Dictionary<string, string?>
                    {
                        ["scope"] = request.Policy.Scope,
                        ["scope_ref"] = request.Policy.ScopeRef.Trim(),
                        ["demand_method"] = request.Policy.DemandMethod,
                        ["service_level_pct"] = request.Policy.ServiceLevelPct.ToString(CultureInfo.InvariantCulture),
                        ["lead_time_days"] = request.Policy.LeadTimeDays.ToString(CultureInfo.InvariantCulture),
                    }, cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("Policy \"" + request.Policy.PolicyName.Trim() + "\" " + verb + ".", id);
                }

                case "policy_deactivate":
                    if (!await ErpSyncronPolicy.DeactivatePolicyAsync(connection, request.PolicyId, cancellationToken).ConfigureAwait(false))
                    {
                        return ErpSimpleWriteResult.Fail("not_found", "Policy not found.");
                    }

                    await LogAsync(connection, adminId, request.Action, request.PolicyId, "Inventory policy deactivated", null, cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("Policy deactivated.", request.PolicyId);
                case "run_forecast":
                {
                    var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
                    var count = await ErpSyncronPolicy.RunForecastAsync(connection, request.WarehouseId, today, cancellationToken).ConfigureAwait(false);
                    await LogAsync(connection, adminId, request.Action, request.WarehouseId, "Demand forecast run", new Dictionary<string, string?>
                    {
                        ["rows"] = count.ToString(CultureInfo.InvariantCulture),
                        ["period_start"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    }, cancellationToken).ConfigureAwait(false);
                    return new ErpSimpleWriteResult(true, "ok", "Forecast written for " + count.ToString(CultureInfo.InvariantCulture) + " item × warehouse rows.", request.WarehouseId, count);
                }

                default:
                {
                    var month = (request.PeriodMonth ?? "").Trim();
                    await ErpSyncronPolicy.RecordServiceLevelAsync(connection, request.ItemId, request.WarehouseId, month, request.DemandQty, request.FulfilledQty, request.StockoutEvents, cancellationToken).ConfigureAwait(false);
                    await LogAsync(connection, adminId, request.Action, request.ItemId, "Service level recorded for " + month, new Dictionary<string, string?>
                    {
                        ["warehouse_id"] = request.WarehouseId.ToString(CultureInfo.InvariantCulture),
                        ["demand_qty"] = request.DemandQty.ToString(CultureInfo.InvariantCulture),
                        ["fulfilled_qty"] = request.FulfilledQty.ToString(CultureInfo.InvariantCulture),
                        ["stockout_events"] = request.StockoutEvents.ToString(CultureInfo.InvariantCulture),
                    }, cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("Service level recorded for " + month + ".", request.ItemId);
                }
            }
        }
        catch (ErpWriteException ex)
        {
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
    }

    private Task LogAsync(System.Data.Common.DbConnection connection, int adminId, string action, long entityId, string summary, IReadOnlyDictionary<string, string?>? detail, CancellationToken cancellationToken)
        => _audit.LogAsync(connection, null, adminId, "syncron_" + action, "inv_policy", entityId, summary, detail, cancellationToken);
}
