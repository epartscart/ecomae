using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string PriceRowsMissing = "Price rows are not in this database.";
    public const string PriceHistoryMissing = "Price upload history is not in this database.";
    public const string PaymentAccountsMissing = "Payment accounts are not in this database.";
    public const string PaymentSettlementsMissing = "Payment settlements are not in this database.";
    public const string ContentAccessMissing = "Content access is not in this database.";
    public const string PartsAgentMissing = "Parts agent sessions are not in this database.";
    public const string MarketplaceChannelsMissing = "Marketplace channels are not in this database.";
    public const string CarriersMissing = "Carriers are not in this database.";
    public const string AccessoryPhotosMissing = "Accessory photos are not in this database.";
    public const string PartsAgentUnregistered = "CP page not registered. Run epc-parts-agent-cp-setup.php";
    public const string NoMarkupProfile = "No markup profile selected (choose customers, emails+group, or a profile group).";
    public const string CurlUnavailable = "curl not available";

    public sealed record PhotoBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string? Error);

    public static async Task<object> PricePreviewAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        int priceId,
        CancellationToken cancellationToken)
        => await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new PreviewDenied(false, "Forbidden", 501),
            async (_, token) =>
            {
                try
                {
                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?"),
                        token,
                        priceId).ConfigureAwait(false);
                    if (count == 0)
                    {
                        return new RawHttp("<div class=\"text-center\">\n\t\t3689\n\t</div>", "text/html; charset=utf-8");
                    }

                    var rows = new StringBuilderHtml();
                    await using var command = connection.CreateCommand();
                    command.CommandText = ErpDb.Positional("SELECT `manufacturer`, `article`, `name`, `price`, `exist` FROM `shop_docpart_prices_data` WHERE `price_id` = ? LIMIT 10");
                    ErpDb.AddParameters(command, priceId);
                    await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        rows.Append("<tr><td style=\"padding: 5px;\">");
                        rows.Append(Cell(reader, 0));
                        rows.Append("</td><td style=\"padding: 5px;\">");
                        rows.Append(Cell(reader, 1));
                        rows.Append("</td><td style=\"padding: 5px;\">");
                        rows.Append(Cell(reader, 2));
                        rows.Append("</td><td style=\"padding: 5px;\">");
                        rows.Append(Cell(reader, 3));
                        rows.Append("</td><td style=\"padding: 5px;\">");
                        rows.Append(Cell(reader, 4));
                        rows.Append("</td></tr>");
                    }

                    return new RawHttp(
                        "<div><h3 style=\"font-weight:bold;\">3690</h3><table style=\"border-spacing: 7px 5px;\">" + rows + "</table></div>",
                        "text/html; charset=utf-8");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, PriceRowsMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static async Task<object> CompletePriceSessionAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string postedKey,
        string techKey,
        int priceId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(techKey, postedKey, StringComparison.Ordinal))
        {
            return await WithCpAdminAsync(
                connection,
                adminSession,
                adminUser,
                csrf,
                () => new FlagBody(false, "Forbidden"),
                (_, token) => FinishPriceSessionAsync(connection, priceId, token),
                cancellationToken).ConfigureAwait(false);
        }

        return await FinishPriceSessionAsync(connection, priceId, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<object> PaymentsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        string? handlerRaw,
        int systemId,
        string? parameters,
        bool skipCsrf,
        CancellationToken cancellationToken)
    {
        if (!skipCsrf)
        {
            return await WithCpAdminAsync(
                connection,
                adminSession,
                adminUser,
                csrf,
                () => new FlagBody(false, "Forbidden"),
                (_, token) => PaymentActionAsync(connection, action, handlerRaw, systemId, parameters, token),
                cancellationToken).ConfigureAwait(false);
        }

        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await PaymentActionAsync(connection, action, handlerRaw, systemId, parameters, cancellationToken).ConfigureAwait(false);
    }

    public static Task<object> PriceDiagnosticsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        IReadOnlyDictionary<string, string> config,
        string docRoot,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            async (_, token) =>
            {
                try
                {
                    await EnsurePriceHistoryAsync(connection, token).ConfigureAwait(false);
                    var snapshot = await DiagnosticsSnapshotAsync(connection, token).ConfigureAwait(false);
                    if (string.Equals(action, "health", StringComparison.Ordinal))
                    {
                        var body = new JsonObject
                        {
                            ["status"] = true,
                            ["snapshot"] = snapshot,
                            ["health"] = HealthWithoutNetwork(config, docRoot)
                        };
                        return body;
                    }

                    return new JsonObject { ["status"] = true, ["snapshot"] = snapshot };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, PriceListsMissing);
                }
            },
            cancellationToken);

    public static Task<object> PricesSendAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string requestObject,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new CodedJson(403, new FlagBody(false, "forbidden")),
            async (_, token) =>
            {
                JsonObject? request;
                try
                {
                    request = string.IsNullOrWhiteSpace(requestObject) ? null : JsonNode.Parse(requestObject) as JsonObject;
                }
                catch (JsonException)
                {
                    request = null;
                }

                if (request is null)
                {
                    return new FlagBody(false, "bad_request");
                }

                var action = request["action"]?.ToString() ?? string.Empty;
                try
                {
                    return action switch
                    {
                        "list_brands" => await ListBrandsAsync(connection, request, token).ConfigureAwait(false),
                        "check_office_storages_map" => await CheckOfficeMapAsync(connection, request, token).ConfigureAwait(false),
                        "ensure_office_storage_links" => await EnsureOfficeLinksAsync(connection, request, token).ConfigureAwait(false),
                        "send_prices" => new JsonObject { ["status"] = true, ["sent"] = 0 },
                        "create_prices" => await CreatePricesGateAsync(connection, request, token).ConfigureAwait(false),
                        _ => new FlagBody(false, "Unknown action")
                    };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    var message = ex.Message.Contains("shop_offices_storages_map", StringComparison.OrdinalIgnoreCase)
                        ? "Office storage markups are not in this database."
                        : ex.Message.Contains("shop_storages", StringComparison.OrdinalIgnoreCase)
                            ? WarehousesMissing
                            : PriceRowsMissing;
                    return new FlagBody(false, message);
                }
            },
            cancellationToken);

    public static async Task<object> ChannelsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? action,
        string? channelCode,
        string? code,
        string? enabled,
        string? channel,
        int marketplaceOrderId,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        try
        {
            switch (action)
            {
                case "seed_channels":
                    await EnsureChannelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
                    var seeded = await SeedMarketplacesAsync(connection, cancellationToken).ConfigureAwait(false);
                    await ChannelLogAsync(connection, "seed", "Worldwide marketplace partners seeded (" + seeded.ToString(CultureInfo.InvariantCulture) + ")", "system", cancellationToken).ConfigureAwait(false);
                    return new JsonObject
                    {
                        ["status"] = true,
                        ["message"] = "Seeded " + seeded.ToString(CultureInfo.InvariantCulture) + " worldwide marketplace partners",
                        ["seeded"] = seeded
                    };
                case "seed_sample":
                    await EnsureChannelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
                    await SeedMarketplacesAsync(connection, cancellationToken).ConfigureAwait(false);
                    await SeedSampleMarketAsync(connection, cancellationToken).ConfigureAwait(false);
                    return new FlagBody(true, "Sample marketplace data loaded (Amazon, eBay, noon)");
                case "toggle_channel":
                    return await ToggleChannelAsync(connection, channelCode, code, enabled, cancellationToken).ConfigureAwait(false);
                case "sync_inventory":
                    return await SyncChannelAsync(connection, channel, cancellationToken).ConfigureAwait(false);
                case "import_order":
                    return await ImportChannelOrderAsync(connection, marketplaceOrderId, cancellationToken).ConfigureAwait(false);
                default:
                    return new FlagBody(false, "Unknown action");
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, MarketplaceChannelsMissing);
        }
        catch (ChannelStop ex)
        {
            return new FlagBody(false, ex.Message);
        }
    }

    public static async Task<object> LogisticsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? action,
        string? carrierCode,
        int orderId,
        string? weightRaw,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        try
        {
            switch (action)
            {
                case "seed_carriers":
                    await EnsureChannelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
                    var count = await SeedCarriersAsync(connection, cancellationToken).ConfigureAwait(false);
                    await ChannelLogAsync(connection, "seed", "Worldwide carrier partners seeded (" + count.ToString(CultureInfo.InvariantCulture) + ")", "logistics", cancellationToken).ConfigureAwait(false);
                    return new FlagBody(true, "Seeded " + count.ToString(CultureInfo.InvariantCulture) + " worldwide carrier partners");
                case "seed_sample":
                    await EnsureChannelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
                    await SeedCarriersAsync(connection, cancellationToken).ConfigureAwait(false);
                    await SeedSampleShipmentAsync(connection, cancellationToken).ConfigureAwait(false);
                    return new FlagBody(true, "Sample carrier shipment loaded");
                case "toggle_carrier":
                    return await ToggleCarrierAsync(connection, carrierCode, cancellationToken).ConfigureAwait(false);
                case "create_shipment":
                    return await CreateShipmentAsync(connection, orderId, carrierCode, weightRaw, cancellationToken).ConfigureAwait(false);
                default:
                    return new FlagBody(false, "Unknown action");
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            var message = ex.Message.Contains("shop_orders", StringComparison.OrdinalIgnoreCase)
                ? OrdersMissing
                : CarriersMissing;
            return new FlagBody(false, message);
        }
        catch (ChannelStop ex)
        {
            return new FlagBody(false, ex.Message);
        }
    }

    public static Task<object> AccessoryPhotosAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        int listingId,
        int photoId,
        CancellationToken cancellationToken)
        => AccessoryAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async (_, token) =>
            {
                try
                {
                    await EnsureAccessoryPhotosAsync(connection, token).ConfigureAwait(false);
                    return action switch
                    {
                        "list" => listingId <= 0
                            ? PhotoFail("listing_id required")
                            : await PhotoListBodyAsync(connection, listingId, token).ConfigureAwait(false),
                        "upload" => PhotoFail(listingId <= 0 ? "Save the listing first, then upload photos." : "No file"),
                        "delete" => await PhotoDeleteAsync(connection, listingId, photoId, token).ConfigureAwait(false),
                        "set_primary" => await PhotoPrimaryAsync(connection, listingId, photoId, token).ConfigureAwait(false),
                        _ => PhotoFail("Unknown action")
                    };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return PhotoFail(AccessoryPhotosMissing);
                }
            },
            cancellationToken);

    public static Task<object> PriceHistoryAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        int priceId,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            async (_, token) =>
            {
                var chosen = string.IsNullOrEmpty(action) ? "list" : action;
                if (string.Equals(chosen, "export_db", StringComparison.Ordinal))
                {
                    return await ExportPricesAsync(connection, priceId, token).ConfigureAwait(false);
                }

                if (string.Equals(chosen, "download", StringComparison.Ordinal) || string.Equals(chosen, "download_latest", StringComparison.Ordinal))
                {
                    return new RawHttp("<h2>Upload file not available</h2>", "text/html; charset=utf-8");
                }

                try
                {
                    await EnsurePriceHistoryAsync(connection, token).ConfigureAwait(false);
                    var names = new List<string>();
                    await using var command = connection.CreateCommand();
                    command.CommandText = priceId > 0
                        ? ErpDb.Positional("SELECT `original_filename` FROM `epc_price_upload_history` WHERE `price_id` = ? ORDER BY `id` DESC LIMIT 100")
                        : "SELECT `original_filename` FROM `epc_price_upload_history` ORDER BY `id` DESC LIMIT 100";
                    if (priceId > 0)
                    {
                        ErpDb.AddParameters(command, priceId);
                    }

                    await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        names.Add(reader.IsDBNull(0) ? string.Empty : reader.GetString(0));
                    }

                    if (names.Count == 0)
                    {
                        return new RawHttp("<div class=\"epc-hist-empty\"><h5>No upload history yet</h5></div>", "text/html; charset=utf-8");
                    }

                    return new RawHttp("<table class=\"epc-hist-table\"><tr><td>" + string.Join("</td></tr><tr><td>", names) + "</td></tr></table>", "text/html; charset=utf-8");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, PriceHistoryMissing);
                }
            },
            cancellationToken);

    public static Task<object> DemandCsvAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        string? country,
        string? filePath,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new CodedJson(403, new FlagBody(false, "forbidden")),
            async (_, token) =>
            {
                await EnsureDemandSchemaAsync(connection, token).ConfigureAwait(false);
                if (string.Equals(action, "stats", StringComparison.Ordinal))
                {
                    return await DemandStatsAsync(connection, token).ConfigureAwait(false);
                }

                if (string.Equals(action, "country_parts", StringComparison.Ordinal))
                {
                    return await DemandCountryPartsAsync(connection, country, token).ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    return new FlagBody(false, "Missing file path");
                }

                return new FlagBody(false, "Invalid file path");
            },
            cancellationToken);

    public static Task<object> PartsAgentAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        string? sessionId,
        CancellationToken cancellationToken)
        => PartsAgentGateAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async (adminId, token) =>
            {
                var chosen = string.IsNullOrEmpty(action) ? "list" : action;
                if (string.Equals(chosen, "detail", StringComparison.Ordinal))
                {
                    return await PartsAgentDetailAsync(connection, sessionId, token).ConfigureAwait(false);
                }

                if (!string.Equals(chosen, "list", StringComparison.Ordinal))
                {
                    return new FlagBody(false, "Unknown action");
                }

                try
                {
                    var sessions = new JsonArray();
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT `session_id`, `message_count`, `last_user_text` FROM `epc_parts_agent_session` ORDER BY `updated_at` DESC LIMIT 50";
                    await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        sessions.Add(new JsonObject
                        {
                            ["session_id"] = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            ["message_count"] = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                            ["last_user_text"] = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                        });
                    }

                    return new JsonObject
                    {
                        ["status"] = true,
                        ["sessions"] = sessions,
                        ["total"] = sessions.Count,
                        ["auto_synced"] = 0
                    };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, PartsAgentMissing);
                }
            },
            cancellationToken);

    private static async Task<object> FinishPriceSessionAsync(DbConnection connection, int priceId, CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                now,
                priceId).ConfigureAwait(false);
            try
            {
                var rows = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?"),
                    cancellationToken,
                    priceId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?"),
                    cancellationToken,
                    rows,
                    priceId).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                // PHP ignores a missing records_count column or a missing data table after the timestamp update.
            }

            return new SessionResult(1, null);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new SessionResult(0, PriceListsMissing);
        }
    }

    private static async Task<object> PaymentActionAsync(
        DbConnection connection,
        string? action,
        string? handlerRaw,
        int systemId,
        string? parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (action)
            {
                case "activate":
                    var handler = Regex.Replace(handlerRaw ?? string.Empty, "[^a-z0-9_]", string.Empty);
                    if (handler.Length == 0)
                    {
                        return new FlagBody(false, "Handler required");
                    }

                    var id = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `id` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1"),
                        cancellationToken,
                        handler).ConfigureAwait(false);
                    if (id <= 0)
                    {
                        return new FlagBody(false, "Gateway not found");
                    }

                    await ErpDb.ExecuteAsync(connection, null, "UPDATE `shop_payment_systems` SET `active` = 0", cancellationToken).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `shop_payment_systems` SET `active` = 1 WHERE `handler` = ? LIMIT 1"),
                        cancellationToken,
                        handler).ConfigureAwait(false);
                    return new FlagBody(true, "Activated: " + HandlerTitle(handler));
                case "save_config":
                    if (!string.IsNullOrEmpty(parameters))
                    {
                        try
                        {
                            using var parsed = JsonDocument.Parse(parameters);
                            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                            {
                                return new FlagBody(false, "Invalid parameters JSON");
                            }
                        }
                        catch (JsonException)
                        {
                            return new FlagBody(false, "Invalid parameters JSON");
                        }
                    }
                    else
                    {
                        return new FlagBody(false, "Invalid parameters JSON");
                    }

                    await ErpDb.ExecuteAsync(connection, null, "UPDATE `shop_payment_systems` SET `active` = 0", cancellationToken).ConfigureAwait(false);
                    if (systemId <= 0)
                    {
                        return new FlagBody(true, "All payment gateways disabled");
                    }

                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `shop_payment_systems` SET `active` = 1, `parameters_values` = ? WHERE `id` = ?"),
                        cancellationToken,
                        parameters,
                        systemId).ConfigureAwait(false);
                    return new FlagBody(true, "Payment gateway saved and activated");
                case "seed_dummy":
                case "save_account":
                case "disable_account":
                case "seed_platform":
                    await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_payment_systems`", cancellationToken).ConfigureAwait(false);
                    return new FlagBody(false, PaymentAccountsMissing);
                case "mark_settlement":
                    return new FlagBody(false, PaymentSettlementsMissing);
                default:
                    return new FlagBody(false, "Unknown action");
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, action is "mark_settlement" ? PaymentSettlementsMissing : PaymentSystemsMissing);
        }
    }

    private static async Task<JsonObject> DiagnosticsSnapshotAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var modes = new JsonObject
        {
            ["1"] = "Manual",
            ["2"] = "FTP",
            ["3"] = "E-mail",
            ["4"] = "URL"
        };
        try
        {
            modes = new JsonObject();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id`, `name` FROM `shop_docpart_prices_load_modes` ORDER BY `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                modes[Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "0"] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            modes = new JsonObject
            {
                ["1"] = "Manual",
                ["2"] = "FTP",
                ["3"] = "E-mail",
                ["4"] = "URL"
            };
        }

        var byMode = new JsonObject();
        var total = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT p.`id`, p.`name`, p.`load_mode`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `records_count` FROM `shop_docpart_prices` p ORDER BY p.`name`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                total++;
                var mode = Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "0";
                var records = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture);
                if (byMode[mode] is not JsonObject bucket)
                {
                    bucket = new JsonObject { ["count"] = 0, ["records"] = 0 };
                    byMode[mode] = bucket;
                }

                bucket["count"] = bucket["count"]!.GetValue<int>() + 1;
                bucket["records"] = bucket["records"]!.GetValue<int>() + records;
            }
        }

        var cron = -1;
        var links = 0;
        try
        {
            cron = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab`", cancellationToken).ConfigureAwait(false);
            links = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab_prices`", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            cron = -1;
        }

        var pending = -1;
        try
        {
            pending = (int)await ErpDb.LongAsync(
                connection,
                null,
                "SELECT COUNT(*) FROM `shop_docpart_pyprices_tasks` WHERE `status` IS NULL OR `status` = '' OR `status` NOT IN ('done','completed','error','failed')",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            pending = -1;
        }

        return new JsonObject
        {
            ["load_modes"] = modes,
            ["by_load_mode"] = byMode,
            ["price_lists_total"] = total,
            ["cron_tasks"] = cron,
            ["cron_price_links"] = links,
            ["pyprices_pending_tasks"] = pending
        };
    }

    private static JsonObject HealthWithoutNetwork(IReadOnlyDictionary<string, string> config, string docRoot)
    {
        var domain = config.TryGetValue("domain_path", out var path) ? path.TrimEnd('/') : string.Empty;
        var pyUrl = domain.Length == 0 ? "/pyprices/pyprices-api.php" : domain + "/pyprices/pyprices-api.php";
        var raw = "{\"_raw\":\"" + CurlUnavailable + "\",\"_http\":0}";
        var checks = new JsonObject
        {
            ["pyprices_api_reachable"] = new JsonObject { ["ok"] = false, ["detail"] = "Response: " + raw },
            ["pyprices_db"] = new JsonObject { ["ok"] = false, ["detail"] = raw },
            ["cron_crutch"] = new JsonObject { ["ok"] = false, ["detail"] = "HTTP 0" },
            ["tmp_upload_dir"] = new JsonObject { ["ok"] = false, ["detail"] = docRoot },
            ["history_archive_dir"] = new JsonObject { ["ok"] = false, ["detail"] = docRoot + "/content/files/price_upload_history" },
            ["deploy_upload_endpoint"] = new JsonObject { ["ok"] = false, ["detail"] = "/epc-upload-uae-prices.php" },
            ["cp_wizard_scripts"] = new JsonObject { ["ok"] = false, ["detail"] = "Missing: ajax_1_prepare_tmp_dir.php, ajax_5_import_csv_to_db.php, ajax_6_complete_session.php" }
        };
        return new JsonObject
        {
            ["all_ok"] = false,
            ["checks"] = checks,
            ["pyprices_url"] = pyUrl
        };
    }

    private static async Task<JsonObject> ListBrandsAsync(DbConnection connection, JsonObject request, CancellationToken cancellationToken)
    {
        var limit = 30;
        if (request["limit"] is JsonNode node && int.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            limit = Math.Min(50, Math.Max(1, parsed));
        }

        var brands = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `manufacturer` AS `brand`, COUNT(*) AS `cnt` FROM `shop_docpart_prices_data` WHERE `manufacturer` <> '' GROUP BY `manufacturer` ORDER BY `cnt` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            brands.Add(new JsonObject
            {
                ["brand"] = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                ["count"] = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture)
            });
        }

        return new JsonObject { ["status"] = true, ["brands"] = brands };
    }

    private static async Task<object> CheckOfficeMapAsync(DbConnection connection, JsonObject request, CancellationToken cancellationToken)
    {
        var office = request["offices"] is JsonNode officeNode ? PhpInt(officeNode.ToString()) : 0;
        var missing = new List<string>();
        if (request["arr_storages"] is JsonArray storages)
        {
            foreach (var storage in storages)
            {
                var storageId = PhpInt(storage?.ToString());
                var linked = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?"),
                    cancellationToken,
                    office,
                    storageId).ConfigureAwait(false);
                if (linked == 0)
                {
                    var name = await ErpDb.StringAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `name` FROM `shop_storages` WHERE `id` = ?"),
                        cancellationToken,
                        storageId).ConfigureAwait(false);
                    missing.Add(string.IsNullOrEmpty(name) ? "ID " + storageId.ToString(CultureInfo.InvariantCulture) : name);
                }
            }
        }

        if (missing.Count == 0)
        {
            return new JsonObject { ["status"] = true };
        }

        return new JsonObject
        {
            ["status"] = false,
            ["message"] = string.Join(", ", missing),
            ["can_link"] = true
        };
    }

    private static async Task<object> EnsureOfficeLinksAsync(DbConnection connection, JsonObject request, CancellationToken cancellationToken)
    {
        var office = request["offices"] is JsonNode officeNode ? PhpInt(officeNode.ToString()) : 0;
        var storages = request["arr_storages"] as JsonArray;
        if (office < 1 || storages is null || storages.Count == 0)
        {
            return new FlagBody(false, "Select shop and storages");
        }

        var groups = request["group_ids"] as JsonArray;
        var groupIds = new List<int>();
        if (groups is null || groups.Count == 0)
        {
            groupIds.AddRange([2, 4, 5, 6, 7]);
        }
        else
        {
            foreach (var group in groups)
            {
                var id = PhpInt(group?.ToString());
                if (id > 0)
                {
                    groupIds.Add(id);
                }
            }
        }

        var linked = 0;
        foreach (var storage in storages)
        {
            var storageId = PhpInt(storage?.ToString());
            if (storageId < 1)
            {
                continue;
            }

            foreach (var groupId in groupIds)
            {
                var inserted = await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `shop_offices_storages_map`
                        (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`)
                        SELECT ?, ?, ?, 0, 999999999, 0, 0 FROM DUAL
                        WHERE NOT EXISTS (
                            SELECT 1 FROM `shop_offices_storages_map`
                            WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ?
                            AND `min_point` = 0 AND `max_point` = 999999999
                        )
                        """),
                    cancellationToken,
                    office,
                    storageId,
                    groupId,
                    office,
                    storageId,
                    groupId).ConfigureAwait(false);
                linked += inserted;
            }
        }

        return new JsonObject
        {
            ["status"] = true,
            ["linked"] = linked,
            ["message"] = "Linked " + linked.ToString(CultureInfo.InvariantCulture) + " markup map row(s)"
        };
    }

    private static async Task<object> CreatePricesGateAsync(DbConnection connection, JsonObject request, CancellationToken cancellationToken)
    {
        var hasProfile = request["profile_group_ids"] is JsonArray profiles && profiles.Count > 0;
        var hasUsers = request["users_list"] is JsonArray users && users.Count > 0;
        var emails = request["emails_list"]?.ToString() ?? string.Empty;
        if (!hasProfile && !hasUsers && string.IsNullOrWhiteSpace(emails))
        {
            return new FlagBody(false, NoMarkupProfile);
        }

        try
        {
            await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_docpart_prices_data`", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, PriceRowsMissing);
        }

        return new FlagBody(false, NoMarkupProfile);
    }

    private static async Task<int> SeedMarketplacesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in Marketplaces)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_marketplace_channels` (`code`, `name`, `marketplace_id`, `active`, `demo_mode`, `config_json`, `time_created`)
                    VALUES (?, ?, ?, 1, 1, ?, ?)
                    ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `marketplace_id` = VALUES(`marketplace_id`)
                    """),
                cancellationToken,
                row.Code,
                row.Name,
                row.MarketplaceId,
                "{\"region_label\":\"" + row.Region + "\"}",
                now).ConfigureAwait(false);
        }

        return Marketplaces.Length;
    }

    private static async Task SeedSampleMarketAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var amazon = await ChannelIdAsync(connection, "amazon", cancellationToken).ConfigureAwait(false);
        var ebay = await ChannelIdAsync(connection, "ebay", cancellationToken).ConfigureAwait(false);
        var noon = await ChannelIdAsync(connection, "noon", cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        (long Id, string Brand, string Article, string Sku, string? Asin, string Title, decimal Price, int Qty)[] skus =
        [
            (amazon, "BOSCH", "0986424590", "AMZ-0986424590", "B0123456789", "Bosch Oil Filter", 42.00m, 25),
            (ebay, "NGK", "BKR6E", "EBY-BKR6E", null, "NGK Spark Plug BKR6E", 12.00m, 120),
            (noon, "BOSCH", "0986424590", "NOON-0986424590", null, "Bosch Oil Filter (noon)", 44.00m, 20)
        ];
        foreach (var sku in skus)
        {
            if (sku.Id <= 0)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_marketplace_sku_map`
                    (`channel_id`, `manufacturer`, `article`, `external_sku`, `external_asin`, `title`, `price`, `stock_qty`, `active`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?)
                    ON DUPLICATE KEY UPDATE `title` = VALUES(`title`)
                    """),
                cancellationToken,
                sku.Id,
                sku.Brand,
                sku.Article,
                sku.Sku,
                sku.Asin ?? string.Empty,
                sku.Title,
                sku.Price,
                sku.Qty,
                now).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_marketplace_orders`
                (`channel_id`, `external_order_id`, `status`, `customer_name`, `currency`, `total_amount`, `items_json`, `time_created`)
                VALUES (?, 'AMZ-402-8819201', 'awaiting_shipment', 'Ahmed Al Mansoori', 'AED', 156.75, '[]', ?)
                """),
            cancellationToken,
            amazon,
            now - 3600).ConfigureAwait(false);
        await ChannelLogAsync(connection, "seed", "Sample marketplace SKUs and orders loaded", "system", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> ToggleChannelAsync(
        DbConnection connection,
        string? channelCode,
        string? code,
        string? enabled,
        CancellationToken cancellationToken)
    {
        var cleaned = Regex.Replace((channelCode ?? code ?? string.Empty).ToLowerInvariant(), "[^a-z0-9_]", string.Empty);
        if (cleaned.Length == 0)
        {
            return new FlagBody(false, "Missing channel code");
        }

        var active = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT CAST(`active` AS CHAR) FROM `epc_marketplace_channels` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            cleaned).ConfigureAwait(false);
        if (active is null)
        {
            return new FlagBody(false, "Channel not found — sync partners first");
        }

        var next = enabled is "0" or "1" ? PhpInt(enabled) : (active == "1" ? 0 : 1);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_marketplace_channels` SET `active` = ? WHERE `code` = ?"),
            cancellationToken,
            next,
            cleaned).ConfigureAwait(false);
        await ChannelLogAsync(connection, "channel", (next == 1 ? "Enabled" : "Disabled") + " channel " + cleaned, cleaned, cancellationToken).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["message"] = (next == 1 ? "Enabled" : "Disabled") + " " + cleaned,
            ["active"] = next
        };
    }

    private static async Task<object> SyncChannelAsync(DbConnection connection, string? channel, CancellationToken cancellationToken)
    {
        await EnsureChannelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var cleaned = Regex.Replace((channel ?? "amazon").ToLowerInvariant(), "[^a-z0-9_]", string.Empty);
        if (cleaned.Length == 0)
        {
            cleaned = "amazon";
        }

        var id = await ChannelIdAsync(connection, cleaned, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            throw new ChannelStop("Channel not found: " + cleaned);
        }

        var updated = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_marketplace_sku_map` WHERE `channel_id` = ? AND `active` = 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_marketplace_channels` SET `last_sync_at` = ? WHERE `id` = ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            id).ConfigureAwait(false);
        await ChannelLogAsync(connection, "inventory_sync", "Demo inventory push: " + updated.ToString(CultureInfo.InvariantCulture) + " SKUs", cleaned, cancellationToken).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["message"] = "Pushed " + updated.ToString(CultureInfo.InvariantCulture) + " SKUs to " + cleaned,
            ["updated"] = updated
        };
    }

    private static async Task<object> ImportChannelOrderAsync(DbConnection connection, int marketplaceOrderId, CancellationToken cancellationToken)
    {
        await EnsureChannelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var external = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `external_order_id` FROM `epc_marketplace_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            marketplaceOrderId).ConfigureAwait(false);
        if (string.IsNullOrEmpty(external))
        {
            throw new ChannelStop("Marketplace order not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_marketplace_orders` SET `status` = 'imported', `imported_at` = ?, `shop_order_id` = NULL WHERE `id` = ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            marketplaceOrderId).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["message"] = "Demo import complete — link to shop_orders when live API credentials are configured",
            ["external_id"] = external,
            ["order_id"] = 0
        };
    }

    private static async Task<int> SeedCarriersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var carrier in Carriers)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_carrier_accounts` (`code`, `name`, `active`, `demo_mode`, `config_json`, `time_created`)
                    VALUES (?, ?, 1, 1, ?, ?)
                    ON DUPLICATE KEY UPDATE `name` = VALUES(`name`)
                    """),
                cancellationToken,
                carrier.Code,
                carrier.Name,
                "{\"track_url\":\"" + carrier.Track.Replace("\"", string.Empty, StringComparison.Ordinal) + "\"}",
                now).ConfigureAwait(false);
        }

        return Carriers.Length;
    }

    private static async Task SeedSampleShipmentAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        long orderId;
        try
        {
            orderId = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 ORDER BY `id` DESC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            throw new ChannelStop(OrdersMissing);
        }

        if (orderId <= 0)
        {
            await ChannelLogAsync(connection, "seed", "Sample carrier shipment loaded for logistics hub", "logistics", cancellationToken).ConfigureAwait(false);
            return;
        }

        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_carrier_shipments` WHERE `order_id` = ?"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (exists == 0)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_carrier_shipments`
                    (`order_id`, `carrier_code`, `service_code`, `tracking_number`, `label_url`, `status`, `weight_kg`, `cost`, `currency`, `shipped_at`, `time_created`)
                    VALUES (?, 'dhl', 'EXPRESS', 'JD014600012345678901', '', 'shipped', 1.5, 57.75, 'AED', ?, ?)
                    """),
                cancellationToken,
                orderId,
                now - 7200,
                now - 7200).ConfigureAwait(false);
        }

        await ChannelLogAsync(connection, "seed", "Sample carrier shipment loaded for logistics hub", "logistics", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> ToggleCarrierAsync(DbConnection connection, string? carrierCode, CancellationToken cancellationToken)
    {
        var cleaned = Regex.Replace((carrierCode ?? string.Empty).ToLowerInvariant(), "[^a-z0-9_]", string.Empty);
        if (cleaned.Length == 0)
        {
            return new FlagBody(false, "Missing carrier code");
        }

        var active = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT CAST(`active` AS CHAR) FROM `epc_carrier_accounts` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            cleaned).ConfigureAwait(false);
        if (active is null)
        {
            return new FlagBody(false, "Carrier not found — seed partners first");
        }

        var next = active == "1" ? 0 : 1;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_carrier_accounts` SET `active` = ? WHERE `code` = ?"),
            cancellationToken,
            next,
            cleaned).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["message"] = (next == 1 ? "Enabled" : "Disabled") + " " + cleaned,
            ["active"] = next
        };
    }

    private static async Task<object> CreateShipmentAsync(
        DbConnection connection,
        int orderId,
        string? carrierCode,
        string? weightRaw,
        CancellationToken cancellationToken)
    {
        await EnsureChannelSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var cleaned = Regex.Replace((carrierCode ?? "dhl").ToLowerInvariant(), "[^a-z0-9_]", string.Empty);
        var carrier = Carriers.FirstOrDefault(row => row.Code == cleaned);
        if (string.IsNullOrEmpty(carrier.Code))
        {
            throw new ChannelStop("Unknown carrier");
        }

        long found;
        try
        {
            found = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1"),
                cancellationToken,
                orderId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, OrdersMissing);
        }

        if (found <= 0)
        {
            throw new ChannelStop("Order not found");
        }

        var weight = 1.5m;
        if (decimal.TryParse(weightRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var posted) && posted > 0)
        {
            weight = posted;
        }

        var cost = Math.Round((carrier.Base + (weight * 8.5m)) * 1.0m, 2, MidpointRounding.AwayFromZero);
        var tracking = carrier.Code[..3].ToUpperInvariant()
            + DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture)
            + orderId.ToString("000000", CultureInfo.InvariantCulture)
            + Random.Shared.Next(100, 1000).ToString(CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_carrier_shipments`
                (`order_id`, `carrier_code`, `service_code`, `tracking_number`, `label_url`, `status`, `weight_kg`, `cost`, `currency`, `shipped_at`, `time_created`)
                VALUES (?, ?, ?, ?, ?, 'shipped', ?, ?, 'AED', ?, ?)
                """),
            cancellationToken,
            orderId,
            carrier.Code,
            carrier.Service,
            tracking,
            string.Format(CultureInfo.InvariantCulture, carrier.Track, tracking),
            weight,
            cost,
            now,
            now).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["message"] = "Label created: " + tracking
        };
    }

    private static async Task<object> PhotoDeleteAsync(DbConnection connection, int listingId, int photoId, CancellationToken cancellationToken)
    {
        if (listingId <= 0 || photoId <= 0)
        {
            return PhotoFail("Invalid photo");
        }

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1"),
            cancellationToken,
            photoId,
            listingId).ConfigureAwait(false);
        if (found <= 0)
        {
            return PhotoFail("Photo not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_photos` WHERE `id` = ?"),
            cancellationToken,
            photoId).ConfigureAwait(false);
        return await PhotoListBodyAsync(connection, listingId, cancellationToken, true).ConfigureAwait(false);
    }

    private static async Task<object> PhotoPrimaryAsync(DbConnection connection, int listingId, int photoId, CancellationToken cancellationToken)
    {
        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1"),
            cancellationToken,
            photoId,
            listingId).ConfigureAwait(false);
        if (found <= 0)
        {
            return PhotoFail("Photo not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_photos` SET `is_primary` = 0 WHERE `listing_id` = ?"),
            cancellationToken,
            listingId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_photos` SET `is_primary` = 1 WHERE `id` = ?"),
            cancellationToken,
            photoId).ConfigureAwait(false);
        return await PhotoListBodyAsync(connection, listingId, cancellationToken, true).ConfigureAwait(false);
    }

    private static async Task<JsonObject> PhotoListBodyAsync(DbConnection connection, int listingId, CancellationToken cancellationToken, bool deleted = false)
    {
        var photos = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `id`, `file_name`, CAST(`is_primary` AS CHAR) FROM `epc_acc_photos` WHERE `listing_id` = ? ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC");
        ErpDb.AddParameters(command, listingId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var file = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            photos.Add(new JsonObject
            {
                ["id"] = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                ["file_name"] = file,
                ["url"] = "/content/files/images/accessories/" + Uri.EscapeDataString(file),
                ["is_primary"] = string.Equals(reader.IsDBNull(2) ? "0" : reader.GetString(2), "1", StringComparison.Ordinal)
            });
        }

        var body = new JsonObject { ["ok"] = true, ["listing_id"] = listingId, ["photos"] = photos };
        if (deleted)
        {
            body["ok"] = true;
        }

        return body;
    }

    private static async Task<object> ExportPricesAsync(DbConnection connection, int priceId, CancellationToken cancellationToken)
    {
        if (priceId <= 0)
        {
            return new FlagBody(false, "price_id required");
        }

        try
        {
            var lines = new List<string> { "manufacturer,article,article_show,name,exist,price,time_to_exe,storage,min_order" };
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order` FROM `shop_docpart_prices_data` WHERE `price_id` = ? ORDER BY `manufacturer`,`article`");
            ErpDb.AddParameters(command, priceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var cells = new string[9];
                for (var i = 0; i < 9; i++)
                {
                    cells[i] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                }

                lines.Add(string.Join(",", cells));
            }

            return new RawHttp(string.Join("\n", lines), "text/csv; charset=utf-8");
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, PriceRowsMissing);
        }
    }

    private static async Task<JsonObject> DemandStatsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var byCountry = new JsonArray();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `country_code`, COUNT(*) AS `cnt`, COUNT(DISTINCT CONCAT(UPPER(`manufacturer`), '|', `article_norm`)) AS `parts` FROM `epc_article_demand` GROUP BY `country_code` ORDER BY `cnt` DESC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                byCountry.Add(new JsonObject
                {
                    ["code"] = code,
                    ["name"] = DemandName(code),
                    ["tags"] = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                    ["parts"] = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)
                });
            }
        }

        var tags = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_article_demand`", cancellationToken).ConfigureAwait(false);
        var markets = new JsonArray();
        foreach (var row in DemandRegistry)
        {
            if (row.Code is "ARE")
            {
                continue;
            }

            markets.Add(new JsonObject { ["code"] = row.Code, ["name"] = row.Name });
        }

        return new JsonObject
        {
            ["status"] = true,
            ["stats"] = new JsonObject
            {
                ["total_tags"] = tags,
                ["by_country"] = byCountry,
                ["markets"] = markets
            }
        };
    }

    private static async Task<object> DemandCountryPartsAsync(DbConnection connection, string? country, CancellationToken cancellationToken)
    {
        var code = NormalizeDemand(country);
        if (code.Length == 0 || DemandRegistry.All(row => row.Code != code))
        {
            return new FlagBody(false, "Unknown country code");
        }

        if (code is "ARE" or "AE")
        {
            return new FlagBody(false, "ARE is UAE stock pool — not a demand market");
        }

        var parts = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT UPPER(`manufacturer`), `article_norm` FROM `epc_article_demand` WHERE `country_code` = ? GROUP BY UPPER(`manufacturer`), `article_norm` ORDER BY 1, 2");
        ErpDb.AddParameters(command, code);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            parts.Add(new JsonObject
            {
                ["brand"] = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                ["article"] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
            });
        }

        return new JsonObject
        {
            ["status"] = true,
            ["country"] = new JsonObject { ["code"] = code, ["name"] = DemandName(code) },
            ["total"] = parts.Count,
            ["parts"] = parts
        };
    }

    private static async Task<object> PartsAgentDetailAsync(DbConnection connection, string? sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var found = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `session_id` FROM `epc_parts_agent_session` WHERE `session_id` = ? LIMIT 1"),
                cancellationToken,
                sessionId ?? string.Empty).ConfigureAwait(false);
            if (string.IsNullOrEmpty(found))
            {
                return new FlagBody(false, "Session not found");
            }

            return new JsonObject { ["status"] = true, ["detail"] = new JsonObject { ["session_id"] = found } };
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, PartsAgentMissing);
        }
    }

    private static Task<object> WithCpAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        Func<object> forbidden,
        Func<int, CancellationToken, Task<object>> body,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            forbidden,
            adminId => body(adminId, cancellationToken),
            cancellationToken);

    private static async Task<object> PartsAgentGateAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        Func<int, CancellationToken, Task<object>> body,
        CancellationToken cancellationToken)
    {
        long contentId;
        try
        {
            contentId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1"),
                cancellationToken,
                "shop/parts_agent_chats").ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            contentId = 0;
        }

        if (contentId <= 0)
        {
            return new FlagBody(false, PartsAgentUnregistered);
        }

        var access = await PageAccessAsync(connection, PhpInt(adminUser), "shop/parts_agent_chats", ContentAccessMissing, cancellationToken).ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Access denied"),
            body,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> AccessoryAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        Func<int, CancellationToken, Task<object>> body,
        CancellationToken cancellationToken)
    {
        long count;
        try
        {
            count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
                cancellationToken,
                adminSession ?? string.Empty,
                PhpInt(adminUser)).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return PhotoFail(AdminSessionsMissing);
        }

        if (count != 1)
        {
            return PhotoFail("Unauthorized");
        }

        var stored = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            adminSession ?? string.Empty,
            PhpInt(adminUser)).ConfigureAwait(false) ?? string.Empty;
        if (stored.Length > 0 && (string.IsNullOrEmpty(csrf) || !string.Equals(stored, csrf, StringComparison.Ordinal)))
        {
            return PhotoFail("CSRF mismatch");
        }

        return await body(PhpInt(adminUser), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object?> StaffAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        object denied,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
                cancellationToken,
                adminSession ?? string.Empty,
                PhpInt(adminUser)).ConfigureAwait(false);
            if (count == 0)
            {
                return denied;
            }

            if (count != 1)
            {
                return new RawHttp(string.Empty, "text/html; charset=utf-8");
            }

            return null;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, AdminSessionsMissing);
        }
    }

    private static async Task EnsurePriceHistoryAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS `epc_price_upload_history` (
              `id` INT NOT NULL AUTO_INCREMENT,
              `price_id` INT NOT NULL DEFAULT 0,
              `original_filename` VARCHAR(255) NOT NULL DEFAULT '',
              `upload_source` VARCHAR(32) NOT NULL DEFAULT '',
              `rows_imported` INT NOT NULL DEFAULT 0,
              `created_at` DATETIME NOT NULL,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureAccessoryPhotosAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS `epc_acc_photos` (
              `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
              `listing_id` INT UNSIGNED NOT NULL,
              `file_name` VARCHAR(255) NOT NULL,
              `sort_order` INT NOT NULL DEFAULT 0,
              `is_primary` TINYINT(1) NOT NULL DEFAULT 0,
              `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureChannelSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in ChannelSchema)
        {
            await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<long> ChannelIdAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_marketplace_channels` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            code).ConfigureAwait(false);

    private static Task ChannelLogAsync(DbConnection connection, string kind, string message, string code, CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_channel_sync_log` (`kind`, `channel_code`, `message`, `time_created`) VALUES (?, ?, ?, ?)"),
            cancellationToken,
            kind,
            code,
            message.Length > 512 ? message[..512] : message,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    private static string DemandName(string code)
        => DemandRegistry.FirstOrDefault(row => row.Code == code).Name is { Length: > 0 } name ? name : code;

    private static string NormalizeDemand(string? country)
    {
        var code = Regex.Replace((country ?? string.Empty).ToUpperInvariant(), "[^A-Z]", string.Empty);
        if (DemandRegistry.Any(row => row.Code == code))
        {
            return code;
        }

        return DemandRegistry.FirstOrDefault(row => row.Iso2 == code).Code ?? string.Empty;
    }

    private static string HandlerTitle(string handler)
    {
        var words = handler.Replace('_', ' ');
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static JsonObject PhotoFail(string error)
        => new() { ["ok"] = false, ["error"] = error };

    private static readonly string[] ChannelSchema =
    [
        """
        CREATE TABLE IF NOT EXISTS `epc_marketplace_channels` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(64) NOT NULL,
          `name` VARCHAR(128) NOT NULL,
          `marketplace_id` VARCHAR(64) DEFAULT NULL,
          `active` TINYINT(1) NOT NULL DEFAULT 1,
          `demo_mode` TINYINT(1) NOT NULL DEFAULT 1,
          `config_json` TEXT,
          `last_sync_at` INT UNSIGNED NOT NULL DEFAULT 0,
          `time_created` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_marketplace_sku_map` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `channel_id` INT UNSIGNED NOT NULL,
          `manufacturer` VARCHAR(128) NOT NULL DEFAULT '',
          `article` VARCHAR(128) NOT NULL DEFAULT '',
          `external_sku` VARCHAR(128) NOT NULL,
          `external_asin` VARCHAR(32) DEFAULT NULL,
          `title` VARCHAR(255) DEFAULT NULL,
          `price` DECIMAL(12,2) NOT NULL DEFAULT 0,
          `stock_qty` INT NOT NULL DEFAULT 0,
          `active` TINYINT(1) NOT NULL DEFAULT 1,
          `time_updated` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `channel_ext_sku` (`channel_id`, `external_sku`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_marketplace_orders` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `channel_id` INT UNSIGNED NOT NULL,
          `external_order_id` VARCHAR(64) NOT NULL,
          `status` VARCHAR(32) NOT NULL DEFAULT 'pending',
          `customer_name` VARCHAR(128) DEFAULT NULL,
          `currency` VARCHAR(8) NOT NULL DEFAULT 'AED',
          `total_amount` DECIMAL(12,2) NOT NULL DEFAULT 0,
          `items_json` TEXT,
          `shop_order_id` INT UNSIGNED DEFAULT NULL,
          `imported_at` INT UNSIGNED NOT NULL DEFAULT 0,
          `time_created` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `channel_ext` (`channel_id`, `external_order_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_carrier_accounts` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `code` VARCHAR(32) NOT NULL,
          `name` VARCHAR(128) NOT NULL,
          `active` TINYINT(1) NOT NULL DEFAULT 1,
          `demo_mode` TINYINT(1) NOT NULL DEFAULT 1,
          `config_json` TEXT,
          `time_created` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`),
          UNIQUE KEY `code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_carrier_shipments` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `order_id` INT UNSIGNED NOT NULL,
          `carrier_code` VARCHAR(32) NOT NULL,
          `service_code` VARCHAR(64) DEFAULT NULL,
          `tracking_number` VARCHAR(64) DEFAULT NULL,
          `label_url` VARCHAR(512) DEFAULT NULL,
          `status` VARCHAR(32) NOT NULL DEFAULT 'draft',
          `weight_kg` DECIMAL(8,3) NOT NULL DEFAULT 0,
          `cost` DECIMAL(12,2) NOT NULL DEFAULT 0,
          `currency` VARCHAR(8) NOT NULL DEFAULT 'AED',
          `shipped_at` INT UNSIGNED NOT NULL DEFAULT 0,
          `time_created` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_channel_sync_log` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `kind` VARCHAR(32) NOT NULL,
          `channel_code` VARCHAR(32) DEFAULT NULL,
          `message` VARCHAR(512) NOT NULL,
          `time_created` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """
    ];

    private static readonly (string Code, string Name, string MarketplaceId, string Region)[] Marketplaces =
    [
        ("amazon", "Amazon.ae", "A2EUQ1WTGCTBG2", "MENA"),
        ("ebay", "eBay US / Motors", "EBAY-US", "Americas"),
        ("amazon_com", "Amazon.com", "ATVPDKIKX0DER", "Americas"),
        ("amazon_ca", "Amazon.ca", "A2EUQ1WTGCTBG2", "Americas"),
        ("amazon_mx", "Amazon.com.mx", "A1AM78C64UM0Y8", "Americas"),
        ("amazon_br", "Amazon.com.br", "A2Q3Y263D00KWC", "Americas"),
        ("amazon_uk", "Amazon.co.uk", "A1F83G8C2ARO7P", "Europe"),
        ("amazon_de", "Amazon.de", "A1PA6795UKMFR9", "Europe"),
        ("amazon_fr", "Amazon.fr", "A13V1IB3VIYZZH", "Europe"),
        ("amazon_it", "Amazon.it", "APJ6JRA9NG5V4", "Europe"),
        ("amazon_es", "Amazon.es", "A1RKKUPIHCS9HS", "Europe"),
        ("amazon_nl", "Amazon.nl", "A1805IZSGTT6HS", "Europe"),
        ("amazon_sa", "Amazon.sa", "A17E79C6D8DWNP", "MENA"),
        ("amazon_eg", "Amazon.eg", "ARBP9OOSHTCHU", "MENA"),
        ("amazon_in", "Amazon.in", "A21TJRUUN4KGV", "Asia"),
        ("amazon_au", "Amazon.com.au", "A39IBJ37TRP1C6", "Asia"),
        ("amazon_jp", "Amazon.co.jp", "A1VC38T7YXB528", "Asia"),
        ("ebay_uk", "eBay UK", "EBAY-GB", "Europe"),
        ("ebay_de", "eBay Germany", "EBAY-DE", "Europe"),
        ("ebay_au", "eBay Australia", "EBAY-AU", "Asia"),
        ("ebay_ca", "eBay Canada", "EBAY-CA", "Americas"),
        ("ebay_fr", "eBay France", "EBAY-FR", "Europe"),
        ("noon", "noon UAE", "NOON-AE", "MENA"),
        ("noon_sa", "noon KSA", "NOON-SA", "MENA"),
        ("noon_eg", "noon Egypt", "NOON-EG", "MENA"),
        ("dubizzle", "dubizzle", "DUBIZZLE-AE", "MENA"),
        ("salla", "Salla", "SALLA-SA", "MENA"),
        ("jumia", "Jumia", "JUMIA", "Africa"),
        ("daraz_pk", "Daraz Pakistan", "DARAZ-PK", "Asia"),
        ("flipkart", "Flipkart", "FLIPKART-IN", "Asia"),
        ("allegro", "Allegro", "ALLEGRO-PL", "Europe"),
        ("mercadolibre", "Mercado Libre", "MELI", "Americas"),
        ("walmart", "Walmart Marketplace", "WALMART-US", "Americas"),
        ("etsy", "Etsy", "ETSY", "Global"),
        ("shopify", "Shopify Channel", "SHOPIFY", "Global")
    ];

    private static readonly (string Code, string Name, string Service, decimal Base, string Track)[] Carriers =
    [
        ("dhl", "DHL Express", "EXPRESS", 45m, "https://www.dhl.com/ae-en/home/tracking.html?tracking-id={0}"),
        ("fedex", "FedEx", "PRIORITY", 42m, "https://www.fedex.com/fedextrack/?trknbr={0}"),
        ("ups", "UPS", "STANDARD", 38m, "https://www.ups.com/track?tracknum={0}"),
        ("tnt", "TNT Express", "EXPRESS", 40m, "https://www.tnt.com/express/en_ae/site/shipping-tools/tracking.html?searchType=con&cons={0}"),
        ("aramex", "Aramex", "PPX", 28m, "https://www.aramex.com/track/results?ShipmentNumber={0}"),
        ("smsa", "SMSA Express", "EXPRESS", 26m, "https://www.smsaexpress.com/trackingdetails?tracknumbers={0}"),
        ("naqel", "Naqel Express", "EXPRESS", 25m, "https://www.naqelexpress.com/en/track?trackingNumber={0}"),
        ("emirates_post", "Emirates Post", "EMS", 22m, "https://www.emiratespost.ae/English/track-and-trace?TrackingNumber={0}"),
        ("imile", "iMile", "EXPRESS", 24m, "https://www.imile.com/track?trackingNo={0}"),
        ("dpd", "DPD", "CLASSIC", 32m, "https://www.dpd.com/tracking?parcelNumber={0}"),
        ("gls", "GLS", "BUSINESS", 31m, "https://gls-group.com/track/{0}"),
        ("postnl", "PostNL", "STANDARD", 30m, "https://jouw.postnl.nl/track-and-trace/{0}"),
        ("royal_mail", "Royal Mail", "TRACKED", 33m, "https://www.royalmail.com/track-your-item#/tracking-results/{0}"),
        ("chronopost", "Chronopost", "CHRONO", 34m, "https://www.chronopost.fr/tracking-no-cms/suivi-page?listeNumerosLT={0}"),
        ("usps", "USPS", "PRIORITY", 36m, "https://tools.usps.com/go/TrackConfirmAction?tLabels={0}"),
        ("canada_post", "Canada Post", "XPRESS", 35m, "https://www.canadapost-postescanada.ca/track-reperage/en#/details/{0}"),
        ("sf_express", "SF Express", "STANDARD", 37m, "https://www.sf-express.com/en/dynamic_function/waybill/#search/bill-number/{0}"),
        ("jt_express", "J&T Express", "EXPRESS", 23m, "https://www.jtexpress.ae/trajectoryQuery?waybillNo={0}"),
        ("yamato", "Yamato Transport", "TAQBIN", 39m, "https://toi.kuronekoyamato.co.jp/cgi-bin/tneko?number={0}"),
        ("bluedart", "Blue Dart", "EXPRESS", 29m, "https://www.bluedart.com/tracking?trackfor=0&trackNo={0}")
    ];

    public sealed record PreviewDenied(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("code")] int Code);

    public sealed record SessionResult(
        [property: JsonPropertyName("result")] int Result,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message);

    private sealed class StringBuilderHtml
    {
        private readonly System.Text.StringBuilder _builder = new();

        public void Append(string text) => _builder.Append(text);

        public override string ToString() => _builder.ToString();
    }

    private sealed class ChannelStop(string message) : Exception(message);
}
