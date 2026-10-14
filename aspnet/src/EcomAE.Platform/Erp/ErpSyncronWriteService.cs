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
    int StockoutEvents = 0,
    string? LineGrid = null,
    string? LineKeys = null,
    long SupplierId = 0,
    string? Sku = null,
    decimal OrderQty = 0m);

/// <summary>Writes for <c>/erp/syncron-app</c>: policies, forecast, service level, and recommended-order decisions.</summary>
public interface IErpSyncronWriteService
{
    Task<ErpSimpleWriteResult> ApplyAsync(int adminId, ErpSyncronActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ErpSyncronWriteService : IErpSyncronWriteService
{
    public static readonly IReadOnlyList<string> Actions =
    [
        "policy_save", "policy_deactivate", "run_forecast", "record_service_level",
        "update_lines", "confirm_lines", "unconfirm_lines", "confirm_all", "unconfirm_all",
        "reject_lines", "manual_order", "draft_orders",
    ];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;
    private readonly TimeProvider _clock;
    private readonly IErpPurchaseOrderWriteService? _purchaseOrders;

    public ErpSyncronWriteService(
        IErpWriteConnectionFactory connections,
        IErpAuditLogWriter audit,
        TimeProvider? clock = null,
        IErpPurchaseOrderWriteService? purchaseOrders = null)
    {
        _connections = connections;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
        _purchaseOrders = purchaseOrders;
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

                case "update_lines":
                    return await UpdateLinesAsync(connection, adminId, request, cancellationToken).ConfigureAwait(false);
                case "confirm_lines":
                    return await ChangeSelectedAsync(connection, adminId, request, "confirmed", ["pending"], "Confirmed", cancellationToken).ConfigureAwait(false);
                case "unconfirm_lines":
                    return await ChangeSelectedAsync(connection, adminId, request, "pending", ["confirmed"], "Unconfirmed", cancellationToken).ConfigureAwait(false);
                case "reject_lines":
                    return await ChangeSelectedAsync(connection, adminId, request, "rejected", ["pending", "confirmed"], "Rejected", cancellationToken).ConfigureAwait(false);
                case "confirm_all":
                    return await ConfirmAllAsync(connection, adminId, cancellationToken).ConfigureAwait(false);
                case "unconfirm_all":
                    return await UnconfirmAllAsync(connection, adminId, cancellationToken).ConfigureAwait(false);
                case "manual_order":
                    return await ManualOrderAsync(connection, adminId, request, cancellationToken).ConfigureAwait(false);
                case "draft_orders":
                    return await DraftOrdersAsync(connection, adminId, request, cancellationToken).ConfigureAwait(false);
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

    private async Task<ErpSimpleWriteResult> UpdateLinesAsync(System.Data.Common.DbConnection connection, int adminId, ErpSyncronActionRequest request, CancellationToken cancellationToken)
    {
        var grid = ErpSyncronPolicy.ParseLineGrid(request.LineGrid);
        if (grid.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No order quantities to update.");
        }

        foreach (var (itemId, warehouseId, qty) in grid)
        {
            await ErpSyncronPolicy.UpsertOrderLineAsync(connection, itemId, warehouseId, qty, 0, "", 0m, null, null, cancellationToken).ConfigureAwait(false);
        }

        await LogAsync(connection, adminId, request.Action, 0, "Recommended order quantities updated", new Dictionary<string, string?>
        {
            ["rows"] = grid.Count.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", "Updated " + grid.Count.ToString(CultureInfo.InvariantCulture) + " order lines.", 0, grid.Count);
    }

    private async Task<ErpSimpleWriteResult> ChangeSelectedAsync(
        System.Data.Common.DbConnection connection,
        int adminId,
        ErpSyncronActionRequest request,
        string status,
        IReadOnlyList<string> from,
        string verb,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.LineGrid))
        {
            foreach (var (itemId, warehouseId, qty) in ErpSyncronPolicy.ParseLineGrid(request.LineGrid))
            {
                await ErpSyncronPolicy.UpsertOrderLineAsync(connection, itemId, warehouseId, qty, 0, "", 0m, null, null, cancellationToken).ConfigureAwait(false);
            }
        }

        var keys = ErpSyncronPolicy.ParseLineKeys(request.LineKeys);
        if (keys.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select at least one order line.");
        }

        var changed = await ErpSyncronPolicy.SetOrderLineStatusAsync(connection, keys, status, from, cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, request.Action, changed, verb + " recommended order lines", new Dictionary<string, string?>
        {
            ["rows"] = changed.ToString(CultureInfo.InvariantCulture),
            ["status"] = status,
        }, cancellationToken).ConfigureAwait(false);
        if (changed == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No order lines were " + verb.ToLowerInvariant() + ". Check the line status and try again.");
        }

        return new ErpSimpleWriteResult(true, "ok", verb + " " + changed.ToString(CultureInfo.InvariantCulture) + " order lines.", 0, changed);
    }

    private async Task<ErpSimpleWriteResult> ConfirmAllAsync(System.Data.Common.DbConnection connection, int adminId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var recommendations = await ErpSyncronPolicy.RecommendAsync(connection, 0, today, cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var row in recommendations)
        {
            if (row.Status != "pending" || row.OrderQty <= 0)
            {
                continue;
            }

            await ErpSyncronPolicy.UpsertOrderLineAsync(
                connection, row.ItemId, row.WarehouseId, row.OrderQty, row.SupplierId, row.SupplierName, row.UnitCost, "confirmed", row.DueDate, cancellationToken).ConfigureAwait(false);
            changed++;
        }

        await LogAsync(connection, adminId, "confirm_all", changed, "Confirmed all pending recommended orders", new Dictionary<string, string?>
        {
            ["rows"] = changed.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", "Confirmed " + changed.ToString(CultureInfo.InvariantCulture) + " pending order lines.", 0, changed);
    }

    private async Task<ErpSimpleWriteResult> UnconfirmAllAsync(System.Data.Common.DbConnection connection, int adminId, CancellationToken cancellationToken)
    {
        var changed = await ErpSyncronPolicy.SetAllOrderLineStatusAsync(connection, "pending", "confirmed", cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, "unconfirm_all", changed, "Unconfirmed recommended orders", new Dictionary<string, string?>
        {
            ["rows"] = changed.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", "Unconfirmed " + changed.ToString(CultureInfo.InvariantCulture) + " order lines.", 0, changed);
    }

    private async Task<ErpSimpleWriteResult> ManualOrderAsync(System.Data.Common.DbConnection connection, int adminId, ErpSyncronActionRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderQty <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Order quantity must be above zero.");
        }

        if (!await ErpSyncronPolicy.WarehouseExistsAsync(connection, request.WarehouseId, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Warehouse is required.");
        }

        var item = await ErpSyncronPolicy.FindItemAsync(connection, request.ItemId, request.Sku ?? "", cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Item not found.");
        }

        var policies = await ErpSyncronPolicy.ListPoliciesAsync(connection, cancellationToken).ConfigureAwait(false);
        var policy = ErpSyncronPolicy.PolicyFor(policies, item.Value.Sku, item.Value.ItemType);
        var lead = policy?.LeadTimeDays ?? ErpSyncronPolicy.DefaultLeadTimeDays;
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var due = today.AddDays(Math.Max(lead, 0)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var supplierName = await ErpSyncronPolicy.SupplierNameAsync(connection, request.SupplierId, cancellationToken).ConfigureAwait(false);
        await ErpSyncronPolicy.UpsertOrderLineAsync(
            connection,
            item.Value.Id,
            request.WarehouseId,
            request.OrderQty,
            request.SupplierId,
            supplierName,
            0m,
            "pending",
            due,
            cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, request.Action, item.Value.Id, "Manual recommended order for " + item.Value.Sku, new Dictionary<string, string?>
        {
            ["warehouse_id"] = request.WarehouseId.ToString(CultureInfo.InvariantCulture),
            ["order_qty"] = request.OrderQty.ToString(CultureInfo.InvariantCulture),
            ["supplier_id"] = request.SupplierId.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Manual order added for " + item.Value.Sku + ".", item.Value.Id);
    }

    private async Task<ErpSimpleWriteResult> DraftOrdersAsync(System.Data.Common.DbConnection connection, int adminId, ErpSyncronActionRequest request, CancellationToken cancellationToken)
    {
        if (_purchaseOrders is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Purchase orders are not available from this screen yet.");
        }

        var keys = ErpSyncronPolicy.ParseLineKeys(request.LineKeys);
        var lines = await ErpSyncronPolicy.ConfirmedOrderLinesAsync(connection, keys.Count == 0 ? null : keys, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Confirm order lines before drafting a purchase order.");
        }

        var skipped = lines.Count(line => line.SupplierId <= 0);
        var groups = lines.Where(line => line.SupplierId > 0).GroupBy(line => line.SupplierId).ToList();
        if (groups.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", skipped.ToString(CultureInfo.InvariantCulture) + " confirmed lines have no supplier and were not drafted.");
        }

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var drafted = 0;
        var notes = new List<string>();
        foreach (var group in groups)
        {
            if (group.Key > int.MaxValue)
            {
                notes.Add("Supplier " + group.Key.ToString(CultureInfo.InvariantCulture) + " is not a purchase-order supplier.");
                continue;
            }

            var poLines = group.Select(line =>
            {
                var ex = decimal.Round(line.OrderQty * line.UnitCost, 2, MidpointRounding.AwayFromZero);
                var description = line.ItemName.Length == 0 ? line.Sku : line.ItemName;
                if (line.WarehouseName.Length > 0)
                {
                    description = description + " · " + line.WarehouseName;
                }

                return new ErpPurchaseOrderLineInput(line.Sku, description, line.OrderQty, line.UnitCost, ex);
            }).ToList();
            var supplierName = group.Select(line => line.SupplierName).FirstOrDefault(name => name.Length > 0) ?? ("Supplier " + group.Key.ToString(CultureInfo.InvariantCulture));
            var saved = await _purchaseOrders.SaveAsync(
                new ErpPurchaseOrderInput
                {
                    SupplierId = (int)group.Key,
                    Title = "Recommended orders " + today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    AmountExVat = poLines.Sum(line => line.LineExVat),
                    Status = "draft",
                    Notes = "Drafted from recommended orders.",
                    Lines = poLines,
                },
                adminId,
                cancellationToken).ConfigureAwait(false);
            foreach (var line in group)
            {
                await ErpSyncronPolicy.StampDraftedAsync(connection, line.ItemId, line.WarehouseId, saved.Id, cancellationToken).ConfigureAwait(false);
            }

            drafted += group.Count();
            notes.Add(saved.PoNo + " for " + supplierName + " (" + group.Count().ToString(CultureInfo.InvariantCulture) + " lines).");
            await LogAsync(connection, adminId, request.Action, saved.Id, "Draft purchase order from recommended orders", new Dictionary<string, string?>
            {
                ["po_no"] = saved.PoNo,
                ["supplier_id"] = group.Key.ToString(CultureInfo.InvariantCulture),
                ["lines"] = group.Count().ToString(CultureInfo.InvariantCulture),
            }, cancellationToken).ConfigureAwait(false);
        }

        if (skipped > 0)
        {
            notes.Add(skipped.ToString(CultureInfo.InvariantCulture) + " confirmed lines have no supplier and were left confirmed.");
        }

        if (drafted == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", string.Join(" ", notes));
        }

        return new ErpSimpleWriteResult(true, "ok", "Drafted " + string.Join(" ", notes), 0, drafted);
    }

    private Task LogAsync(System.Data.Common.DbConnection connection, int adminId, string action, long entityId, string summary, IReadOnlyDictionary<string, string?>? detail, CancellationToken cancellationToken)
        => _audit.LogAsync(connection, null, adminId, "syncron_" + action, "inv_policy", entityId, summary, detail, cancellationToken);
}
