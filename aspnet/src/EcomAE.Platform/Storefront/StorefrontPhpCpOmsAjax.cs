using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string OmsLogsMissing = "Order logs are not in this database.";
    public const string OmsErpMapNotRead = "ERP document map stays on the ERP read.";

    public static async Task<object> OrdersOmsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        IReadOnlyDictionary<string, string> fields,
        ICpOmsWriteService orders,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Forbidden"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (adminId, token) => OmsBodyAsync(connection, adminId, fields, orders, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> OmsBodyAsync(
        DbConnection connection,
        int adminId,
        IReadOnlyDictionary<string, string> fields,
        ICpOmsWriteService orders,
        CancellationToken cancellationToken)
    {
        var orderId = ParseId(OmsField(fields, "order_id"));
        if (orderId <= 0)
        {
            return new FlagBody(false, "Invalid order");
        }

        try
        {
            var found = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false);
            if (found <= 0)
            {
                return new FlagBody(false, "Order not found");
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, OrdersMissing);
        }

        var action = OmsField(fields, "action").Trim();
        try
        {
            var written = action switch
            {
                "set_item_status" => await orders.SetItemStatusAsync(orderId, ParseId(OmsField(fields, "item_id")), ParseId(OmsField(fields, "status")), adminId, cancellationToken).ConfigureAwait(false),
                "set_items_status" => await orders.SetItemsStatusAsync(orderId, ParseId(OmsField(fields, "status")), await ItemIdsAsync(connection, orderId, OmsField(fields, "item_ids"), cancellationToken).ConfigureAwait(false), adminId, cancellationToken).ConfigureAwait(false),
                "send_message" => await orders.SendMessageAsync(orderId, OmsField(fields, "text"), ParseId(OmsField(fields, "item_id")), adminId, cancellationToken).ConfigureAwait(false),
                "set_courier" => await orders.SetCourierAsync(orderId, ParseDecimal(OmsField(fields, "delivery_price")), OmsField(fields, "country"), adminId, cancellationToken).ConfigureAwait(false),
                "update_item" => await orders.UpdateItemAsync(orderId, ItemPatch(fields), adminId, cancellationToken).ConfigureAwait(false),
                "update_items" => await orders.UpdateItemsAsync(orderId, ItemPatches(OmsField(fields, "items")), adminId, cancellationToken).ConfigureAwait(false),
                "refresh_item_cost" => await orders.RefreshItemCostAsync(orderId, ParseId(OmsField(fields, "item_id")), adminId, cancellationToken).ConfigureAwait(false),
                "supplier_fulfillment_set_stage" => await orders.SetFulfillmentStageAsync(orderId, OmsField(fields, "supplier_key"), OmsField(fields, "stage"), OmsField(fields, "notes"), adminId, cancellationToken).ConfigureAwait(false),
                "supplier_fulfillment_advance" => await orders.AdvanceFulfillmentAsync(orderId, OmsField(fields, "supplier_key"), adminId, cancellationToken).ConfigureAwait(false),
                "lookup_warehouse_price" => ErpSimpleWriteResult.Fail("classic", "Warehouse price lookup stays on the price list."),
                "list_messages" => ErpSimpleWriteResult.Fail("classic", "Order messages stay on the classic reader."),
                "erp_document_map" => ErpSimpleWriteResult.Fail("classic", OmsErpMapNotRead),
                "supplier_fulfillment_status" => ErpSimpleWriteResult.Fail("classic", "Supplier fulfillment status stays on the classic reader."),
                _ => ErpSimpleWriteResult.Fail("unknown", "Unknown action"),
            };
            if (!written.Succeeded)
            {
                return new FlagBody(false, written.Message);
            }

            return new JsonObject { ["status"] = true };
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            if (ex.Message.Contains("shop_orders_logs", StringComparison.OrdinalIgnoreCase))
            {
                return new FlagBody(false, OmsLogsMissing);
            }

            if (ex.Message.Contains("shop_orders_items", StringComparison.OrdinalIgnoreCase))
            {
                return new FlagBody(false, OrderItemsMissing);
            }

            return new FlagBody(false, OrdersMissing);
        }
    }

    private static async Task<IReadOnlyList<long>> ItemIdsAsync(
        DbConnection connection,
        int orderId,
        string raw,
        CancellationToken cancellationToken)
    {
        var ids = ReadIds(raw);
        if (ids.Count > 0)
        {
            return ids;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_orders_items` WHERE `order_id` = ?");
        ErpDb.AddParameters(command, orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return ids;
    }

    private static CpOmsItemWritePatch ItemPatch(IReadOnlyDictionary<string, string> fields)
    {
        var reprice = fields.ContainsKey("reprice_from_warehouse") || fields.ContainsKey("apply_warehouse_price");
        return new CpOmsItemWritePatch(
            ParseId(OmsField(fields, "item_id")),
            fields.ContainsKey("price") ? ParseDecimal(OmsField(fields, "price")) : null,
            fields.ContainsKey("count_need") ? ParseId(OmsField(fields, "count_need")) : null,
            fields.ContainsKey("t2_price_purchase") ? ParseDecimal(OmsField(fields, "t2_price_purchase")) : null,
            fields.ContainsKey("t2_storage_id") ? ParseId(OmsField(fields, "t2_storage_id")) : null,
            fields.ContainsKey("t2_name") ? OmsField(fields, "t2_name") : null,
            fields.ContainsKey("t2_manufacturer") ? OmsField(fields, "t2_manufacturer") : null,
            fields.ContainsKey("t2_article") ? OmsField(fields, "t2_article") : null,
            fields.ContainsKey("t2_article_show") ? OmsField(fields, "t2_article_show") : null,
            reprice);
    }

    private static IReadOnlyList<CpOmsItemWritePatch> ItemPatches(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var patches = new List<CpOmsItemWritePatch>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in row.EnumerateObject())
                {
                    fields[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.ToString();
                }

                patches.Add(ItemPatch(fields));
            }

            return patches;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<long> ReadIds(string raw)
    {
        var ids = new List<long>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ids;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ids;
            }

            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.TryGetInt64(out var id) && id > 0)
                {
                    ids.Add(id);
                }
            }
        }
        catch (JsonException)
        {
            return ids;
        }

        return ids;
    }

    private static string OmsField(IReadOnlyDictionary<string, string> fields, string name)
        => fields.TryGetValue(name, out var value) ? value : string.Empty;

    private static decimal ParseDecimal(string raw)
        => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
}
