using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP JSON for the public storefront ajax files the desktop and part search still call.
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string WarehouseOffersPath = "/content/shop/docpart/ajax_epc_warehouse_offers.php";
    public const string ArticleBrandsPath = "/content/shop/docpart/ajax_epc_article_brands.php";
    public const string CrossSearchPath = "/content/shop/docpart/ajax_epc_cross_search.php";
    public const string OfficeBunchesPath = "/content/shop/docpart/ajax_epc_office_storage_bunches.php";
    public const string ProductsOfBunchPath = "/content/shop/docpart/ajax_getProductsOfBunch.php";
    public const string CartInfoPath = "/content/shop/order_process/ajax_get_cart_info.php";
    public const string UnreadMessagesPath = "/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php";
    public const string VinInfoPath = "/content/requests/ajax_get_vin_info.php";
    public const string ManufacturersListPath = "/content/shop/docpart/ajax_getManufacturersList.php";
    public const string ManufacturersFromPricesPath = "/content/shop/docpart/ajax_getManufacturersListFromPrices.php";
    public const string ManufacturersFromCrossServerPath = "/content/shop/docpart/ajax_getManufacturersListFromCrossServer.php";
    public const string AnalogsListPath = "/content/shop/docpart/ajax_getAnalogsList.php";
    public const string AsynchronPath = "/content/shop/docpart/ajax_asynchron.php";
    public const string PartInfoPath = "/content/shop/docpart/ajax_get_info.php";
    public const string ProductsOfBunch2Path = "/content/shop/docpart/ajax_getProductsOfBunch2.php";
    public const string SkuMediaPublicPath = "/content/shop/catalogue/ajax_epc_sku_media_public.php";
    public const string AddToBasketPath = "/content/shop/order_process/ajax_add_to_basket.php";
    public const string ChangeCountPath = "/content/shop/order_process/ajax_change_count_need.php";
    public const string DeleteCartPath = "/content/shop/order_process/ajax_delete_cart_record.php";
    public const string NotepadPath = "/content/shop/docpart/garage/ajax_add_to_notepad.php";
    public const string GarageCarsPath = "/content/shop/docpart/garage/ajax_operations_cars.php";
    public const string CheckoutCreatePath = "/content/shop/order_process/ajax_checkout_create.php";
    public const string OrderMessagesPath = "/content/shop/messager/ajax_get_order_messages.php";
    public const string SendMessagePath = "/content/shop/messager/ajax_send_message.php";
    public const string CreateOperationPath = "/content/shop/finance/ajax_create_operation.php";
    public const string ReturnsCheckPath = "/content/shop/order_process/ajax_check_items_returns.php";
    public const string ArticleListPath = "/content/shop/docpart/ajax_get_article_list.php";
    public const string CheckForOrderPath = "/content/shop/order_process/ajax_check_for_order.php";
    public const string SetUserOptionPath = "/content/users/ajax_set_user_option.php";
    public const string SetMyCityPath = "/modules/shop/geo/ajax_set_my_city.php";
    public const string AddEvaluationPath = "/content/shop/catalogue/evaluations/ajax_add_evaluation.php";
    public const string ProductEvaluationsPath = "/content/shop/catalogue/evaluations/ajax_get_product_evaluations.php";
    public const string ProductMarkPath = "/content/shop/catalogue/evaluations/ajax_get_product_general_mark.php";
    public const string VinMessagesPath = "/content/requests/ajax_get_message.php";
    public const string VinSendMessagePath = "/content/requests/ajax_send_message.php";
    public const string GuestOrderLookupPath = "/content/shop/order_process/ajax_check_order_not_authorized.php";
    public const string CatalogueCountPath = "/content/shop/catalogue/ajax_get_products_count.php";
    public const string CatalogueListPath = "/content/shop/catalogue/ajax_get_products_list.php";
    public const string CataloguePagePath = "/content/shop/catalogue/ajax_get_products_page.php";
    public const string PickupTimingPath = "/content/shop/obtaining_modes/get_in_office/ajax_specify_office_info.php";
    public const string DemandMetaPath = "/content/shop/docpart/ajax_epc_demand_meta.php";
    public const string DemandShowcasePath = "/content/shop/docpart/ajax_epc_demand_showcase.php";
    public const string DemandByCountryPath = "/content/shop/docpart/ajax_epc_demand_by_country.php";
    public const string DemandCardPath = "/content/shop/docpart/ajax_epc_demand_card.php";
    public const string DemandTagsPath = "/content/shop/docpart/ajax_epc_demand_tags_index.php";
    public const string DemandVehiclesPath = "/content/shop/docpart/ajax_epc_demand_country_vehicles.php";
    public const string GarageMarkModelsPath = "/content/shop/docpart/garage/to_link/ajax_get_mark_models.php";
    public const string GarageModelTypesPath = "/content/shop/docpart/garage/to_link/ajax_get_models_types.php";
    public const string UcatsLocalFailure = "null";
    public const string UcatsNoAccess = "No access";
    public const string UcatsConfigMissing = "Configuration not loaded.";
    public const string UcatsCataloguesPath = "/content/shop/ucats/catalogues.php";
    public const string UcatsAuthControlPath = "/content/shop/ucats/ucats_auth_control.php";
    public const string QuoteAddPath = "/content/shop/order_process/ajax_add_to_quote.php";
    public const string QuoteManualPath = "/content/shop/order_process/ajax_add_to_quote_manual.php";
    public const string QuoteSubmitPath = "/content/shop/order_process/ajax_quote_submit.php";
    public const string QuoteAcceptPath = "/content/shop/order_process/ajax_quote_accept.php";
    public const string TreeBrunchPath = "/content/shop/catalogue/tree_lists/ajax/ajax_get_brunch_items.php";
    public const string TreeAsyncPath = "/content/shop/catalogue/tree_lists/ajax/ajax_async_tree_loader.php";
    public const string ToMarksPath = "/content/shop/catalogue/search_tabs/tabs_content/to_catalogue/ajax_get_to_marks.php";
    public const string QuotesMissing = "Quotes are not in this database.";
    public const string TreeListsMissing = "Catalogue tree lists are not in this database.";
    public const string QuoteDbMessage = "DB";
    public const string QuoteInvalid = "Invalid quote";
    public const string QuoteNotDraft = "Quote not found or already submitted";
    public const string QuoteNeedLine = "Add at least one line before submitting";
    public const string QuoteHashFailed = "Data validation failed. Refresh the page and try again.";
    public const string QuoteType2Only = "Only supplier price-search lines can be added to a quote.";
    public const string QuoteInvalidPayload = "Invalid payload";
    public const string QuoteBrandRequired = "Brand and part number are required.";
    public const string QuoteNotAcceptable = "Quote is not available for acceptance";
    public const string QuoteNoLines = "No lines in quote";
    public const string QuoteIncomplete = "Quote is incomplete — wait for staff pricing on all lines";
    public const string QuoteAlreadyInCart = "One or more items are already in your cart at this price — adjust the cart and try again";
    public const string QuoteAcceptFailed = "Could not complete acceptance";
    public const string ToMarksLocalFailure = "NULL\n";
    public const string UcatsAccessControlMissing = "Ucats access control is not in this database.";
    public const string BotAddressesMissing = "Bot addresses are not in this database.";
    public const string UcatsForbidden = "Forbidden";

    public static readonly string[] UcatsFragmentPaths =
    [
        "/content/shop/ucats/oil/product.php",
        "/content/shop/ucats/oil/avtoximiya.php",
        "/content/shop/ucats/akb/product.php",
        "/content/shop/ucats/akb/akb.php",
        "/content/shop/ucats/disky/product.php",
        "/content/shop/ucats/disky/disky.php",
        "/content/shop/ucats/shiny/product.php",
        "/content/shop/ucats/shiny/shiny.php",
        "/content/shop/ucats/accessories/product.php",
        "/content/shop/ucats/accessories/cars.php",
        "/content/shop/ucats/accessories/cars_models.php",
        "/content/shop/ucats/accessories/vybor_tovara.php",
        "/content/shop/ucats/kolpaki/product.php",
        "/content/shop/ucats/kolpaki/kolpaki.php",
        "/content/shop/ucats/bolty_gayki_prostavki/product.php",
        "/content/shop/ucats/bolty_gayki_prostavki/bolty_gayki_prostavki.php",
        "/content/shop/ucats/to/cars.php",
        "/content/shop/ucats/to/cars_models.php",
        "/content/shop/ucats/to/cars_models_types.php",
        "/content/shop/ucats/to/parts_list.php"
    ];

    public static readonly string[] UcatsProductPaths =
    [
        "/content/shop/ucats/oil/ajax_get_products.php",
        "/content/shop/ucats/akb/ajax_get_products.php",
        "/content/shop/ucats/disky/ajax_get_products.php",
        "/content/shop/ucats/shiny/ajax_get_products.php",
        "/content/shop/ucats/accessories/ajax_get_products.php",
        "/content/shop/ucats/kolpaki/ajax_get_products.php",
        "/content/shop/ucats/bolty_gayki_prostavki/ajax_get_products.php"
    ];

    public static readonly string[] UcatsGroupFieldPaths =
    [
        "/content/shop/ucats/oil/ajax_get_group_fields.php",
        "/content/shop/ucats/akb/ajax_get_group_fields.php",
        "/content/shop/ucats/disky/ajax_get_group_fields.php",
        "/content/shop/ucats/shiny/ajax_get_group_fields.php",
        "/content/shop/ucats/accessories/ajax_get_group_fields.php",
        "/content/shop/ucats/kolpaki/ajax_get_group_fields.php",
        "/content/shop/ucats/bolty_gayki_prostavki/ajax_get_group_fields.php"
    ];
    public const string UserOptionsMissing = "User options are not in this database.";
    public const string CatalogueCategoriesMissing = "Catalogue categories are not in this database.";
    public const string ProductEvaluationsMissing = "Product evaluations are not in this database.";
    public const string VinRequestsMissing = "VIN requests are not in this database.";
    public const string VinMessagesMissing = "VIN messages are not in this database.";
    public const string AdminSessionsMissing = "Admin sessions are not in this database.";
    public const string CatalogueProductsMissing = "Catalogue products are not in this database.";
    public const string CatalogueTextSearchMissing = "Catalogue text search is not in this database.";
    public const string ProductDescriptionsMissing = "Product descriptions are not in this database.";
    public const string CataloguePropertyFiltersMissing = "Catalogue property filters are not in this database.";
    public const string OfficesMissing = "Offices are not in this database.";
    public const string OfficeStoragesMissing = "Office storages are not in this database.";
    public const string DemandAuthMessage = "Please sign in to use Vehicle Parts intelligence AI.";
    public const string DatabaseUnavailable = "Database unavailable";
    public const string NoDemandCountry = "No demand country is assigned to your account. Contact support.";
    public const string UnknownCountryCode = "Unknown country code.";
    public const string DemandCountryOnlyPrefix = "You can only view demand intelligence for ";
    public const string WarehouseStockMissing = "Warehouse stock is not in this database.";
    public const string CartDetailsMissing = "Cart details are not in this database.";
    public const string OrdersMissing = "Orders are not in this database.";
    public const string OrderMessagesMissing = "Order messages are not in this database.";
    public const string AccountingMissing = "Accounting is not in this database.";
    public const string GarageMissing = "Garage cars are not in this database.";
    public const string ArticleQueriesMissing = "Article queries are not in this database.";
    public const string ReturnsMissing = "Return items are not in this database.";
    public const string NoDbConnect = "No DB connect";
    public const string NoDbConnectChange = "No DB Connect";
    public const string SkuMediaMissing = "SKU media is not in this database.";
    public const string CartMissing = "Cart is not in this database.";
    public const string SessionsMissing = "Sessions are not in this database.";
    public const string CatalogueReserveCode = "catalogue_reserve";
    public const string CatalogueReserve = "Catalogue warehouse reservation is not ported.";
    public const string GuestAuthMessage = "Please log in or register to continue.";
    public const string GuestLoginUrl = "/en/users/login";
    public const string HashExpiredMessage = "Unable to add to cart: price data expired. Refresh the page and try again.";
    public const string NoMarginMessage = "Unable to add this item to your cart right now. Please refresh the page and try again.";
    /// <summary>lang_text_strings has no row for these cart ids in this database.</summary>
    public const string CartStringIncorrectSession = "4460";
    public const string CartStringIncorrectData = "4461";
    public const string CartStringHash = "4462";
    public const string CartStringUnknownType = "4464";
    public const string CartStringForbidden = "4465";
    public const string CartStringNotFound = "4466";
    public const string CartStringSameCount = "4467";
    public const string CartStringNotEnough = "4468";
    public const string CartStringMinOrder = "4469";
    public const string StorageHandlerManufacturersError = "Storage handler error (get manufacturers)";
    public const string StorageHandlerError = "Storage handler error";
    public const string PricesStorageLabel = "Прайс-листы";
    public const string CrossStorageLabel = "Сервер кроссов";
    /// <summary>lang_text_strings has no row for this id in this database.</summary>
    public const string EmptyStoragesStringKey = "4192";
    /// <summary>lang_text_strings has no row for this id in this database.</summary>
    public const string UnknownActionStringKey = "4193";

    public const string SensitiveMask = "**";

    public static object CsrfFailure(string message)
        => new CsrfBody(message, message, false);

    public static object WarehouseOffers(StorefrontPartSearchResult result, string brand, bool pricesVisible)
    {
        if (string.Equals(result.Source, "empty", StringComparison.Ordinal))
        {
            return new WarehouseBody(false, string.Empty, brand ?? string.Empty, 0, [], "empty", "Empty article");
        }

        var rows = result.Rows.Select(row => WarehouseRow(row, pricesVisible)).ToList();
        var message = ClearMessage(result.Message, "Warehouse offers are not in this database.");
        return new WarehouseBody(true, result.Article, brand ?? string.Empty, rows.Count, rows, result.Source, message);
    }

    public static object ArticleBrands(StorefrontArticleBrandsResult result)
    {
        if (string.Equals(result.Source, "empty", StringComparison.Ordinal))
        {
            return new BrandsBody(false, string.Empty, "Empty article", []);
        }

        var manufacturers = result.Brands
            .Select(b => new BrandRow(b.Brand, string.IsNullOrWhiteSpace(b.Name) ? b.Brand : b.Name))
            .ToList();
        var message = ClearMessage(result.Message, "Article brands are not in this database.");
        return new BrandsBody(true, result.Article, message, manufacturers);
    }

    public static object CrossSearch(StorefrontCrossSearchResult result, bool pricesVisible)
    {
        if (string.Equals(result.Source, "empty", StringComparison.Ordinal))
        {
            return new CrossBody(false, "Empty article", [], [], false);
        }

        var references = result.References.Select(r => new CrossRefRow(r.Brand, r.Article, r.Source, r.InStock)).ToList();
        var stock = result.Stock.Select(r => CrossStockRow(r, pricesVisible)).ToList();
        var message = ClearMessage(result.Message, "Cross references are not in this database.");
        return new CrossBody(true, message, references, stock, pricesVisible);
    }

    public static object Bunches(StorefrontOfficeStorageBunchesResult result)
    {
        if (string.Equals(result.Source, "empty", StringComparison.Ordinal))
        {
            return new BunchesBody(false, string.Empty, result.Brand, 0, [], "empty", "Empty article");
        }

        var bunches = result.Bunches.Select(MapBunch).ToList();
        var message = ClearMessage(result.Message, "Office storages are not in this database.");
        return new BunchesBody(true, result.Article, result.Brand, bunches.Count, bunches, result.Source, message);
    }

    public static object ProductsOfBunch(StorefrontProductsOfBunchResult result, bool pricesVisible)
    {
        var products = result.Products.Select(p => ProductRow(p, pricesVisible)).ToList();
        var message = ClearMessage(result.Message, "Warehouse offers are not in this database.");
        return new BunchProductsBody(result.Result, products, pricesVisible, message);
    }

    public static async Task<CsrfDecision> ReadCsrfAsync(
        DbConnection connection,
        string? sessionToken,
        string? postedKey,
        CancellationToken cancellationToken)
    {
        if (postedKey is null)
        {
            return CsrfDecision.Fail("Error! CSRF 1");
        }

        if (postedKey.Length == 0)
        {
            return CsrfDecision.Fail("Error! CSRF 3");
        }

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return CsrfDecision.Fail("Error! CSRF 3.1");
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `user_id`, `id`, IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? LIMIT 1");
            ErpDb.AddParameters(command, sessionToken);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return CsrfDecision.Fail("Error! CSRF 3.1");
            }

            var userId = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            var sessionId = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            var stored = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            if (!string.Equals(stored, postedKey, StringComparison.Ordinal))
            {
                return CsrfDecision.Fail("Error! CSRF 4");
            }

            return CsrfDecision.Allow(userId, sessionId);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return CsrfDecision.Fail("Sessions are not in this database.");
        }
    }

    public static async Task<object> CartInfoAsync(
        DbConnection connection,
        int userId,
        int sessionRecordId,
        bool pricesVisible,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 && !pricesVisible)
        {
            await ClearBlockedGuestCartAsync(connection, sessionRecordId, cancellationToken).ConfigureAwait(false);
            return new CartBody(string.Empty, 0);
        }

        var sessionId = userId > 0 ? 0 : sessionRecordId;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT COUNT(`id`), IFNULL(SUM(`price` * `count_need`), 0) FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ?");
            ErpDb.AddParameters(command, userId, sessionId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new CartBody(string.Empty, 0);
            }

            var count = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            var sum = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
            var label = sum > 0
                ? sum.ToString("0.00", CultureInfo.InvariantCulture)
                : string.Empty;
            return new CartBody(label, count);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new StatusBody(false, "Cart is not in this database.", null);
        }
    }

    public static async Task<object> UnreadMessagesAsync(
        DbConnection connection,
        int userId,
        bool returns,
        CancellationToken cancellationToken)
    {
        var sql = returns
            ? "SELECT COUNT(*) FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `return_id` IN (SELECT `id` FROM `shop_orders_returns` WHERE `user_id` = ?)"
            : "SELECT COUNT(*) FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `order_id` IN (SELECT `id` FROM `shop_orders` WHERE `user_id` = ?)";
        try
        {
            var count = await ErpDb.LongAsync(connection, null, ErpDb.Positional(sql), cancellationToken, userId).ConfigureAwait(false);
            return new CountBody(true, count > int.MaxValue ? int.MaxValue : (int)count, null);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new CountBody(false, 0, "Order messages are not in this database.");
        }
    }

    public static async Task<object> VinUnreadAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return new VinBody(false, "Forbidden", 501, null);
        }

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users_vin` WHERE `user_id` = ? AND `viewed_customer` = 0"),
                cancellationToken,
                userId).ConfigureAwait(false);
            return new VinBody(true, null, null, count > int.MaxValue ? int.MaxValue : (int)count);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new VinBody(false, "VIN requests are not in this database.", null, 0);
        }
    }

    /// <summary>PHP <c>epc_storefront_clear_guest_cart</c>: delete this session's guest rows, then the cart.</summary>
    private static async Task ClearBlockedGuestCartAsync(
        DbConnection connection,
        int sessionRecordId,
        CancellationToken cancellationToken)
    {
        if (sessionRecordId <= 0)
        {
            return;
        }

        try
        {
            var ids = new List<int>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_carts` WHERE `user_id` = 0 AND `session_id` = ?");
                ErpDb.AddParameters(command, sessionRecordId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ids.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            if (ids.Count == 0)
            {
                return;
            }

            var placeholders = string.Join(",", Enumerable.Repeat("?", ids.Count));
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("DELETE FROM `shop_carts_details` WHERE `cart_record_id` IN (" + placeholders + ")"),
                    cancellationToken,
                    ids.Cast<object>().ToArray()).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `shop_carts` WHERE `user_id` = 0 AND `session_id` = ?"),
                cancellationToken,
                sessionRecordId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
        }
    }

    public static string ClearMessage(string? message, string missing)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        if (message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase))
        {
            return missing;
        }

        return message;
    }

    private static WarehouseOfferRow WarehouseRow(StorefrontPartOfferDigest row, bool pricesVisible)
    {
        if (pricesVisible)
        {
            return new WarehouseOfferRow(
                row.PriceId,
                row.PriceList,
                row.Manufacturer,
                row.Article,
                row.ArticleShow,
                row.Name,
                row.Price,
                row.Exist,
                row.Storage,
                row.TimeToExe);
        }

        return new WarehouseOfferRow(
            row.PriceId,
            string.Empty,
            row.Manufacturer,
            row.Article,
            row.ArticleShow,
            row.Name,
            null,
            null,
            string.Empty,
            null);
    }

    private static CrossStockJson CrossStockRow(StorefrontCrossStockDigest row, bool pricesVisible)
    {
        if (pricesVisible)
        {
            return new CrossStockJson(
                row.Brand,
                row.Article,
                row.Price,
                row.Qty,
                row.Delivery,
                row.Warehouse,
                row.StorageId,
                row.PriceId,
                true);
        }

        return new CrossStockJson(
            row.Brand,
            row.Article,
            0m,
            null,
            string.Empty,
            SensitiveMask,
            0,
            0,
            false);
    }

    private static BunchProductJson ProductRow(StorefrontPartOfferDigest row, bool pricesVisible)
    {
        if (pricesVisible)
        {
            return new BunchProductJson(
                row.Manufacturer,
                row.Article,
                row.Name,
                row.Price,
                row.Exist,
                row.TimeToExe,
                row.Storage,
                row.StorageId,
                row.PriceId);
        }

        return new BunchProductJson(
            row.Manufacturer,
            row.Article,
            row.Name,
            0m,
            row.Exist > 0 ? 1 : 0,
            "0",
            string.Empty,
            0,
            row.PriceId);
    }

    private static BunchJson MapBunch(StorefrontOfficeStorageBunchDigest bunch)
        => new(
            bunch.OfficeId,
            bunch.StorageId,
            bunch.ProtocolVersion,
            bunch.TreelaxCatalogue,
            bunch.NestedBunches?.Select(MapBunch).ToList() ?? []);

    public sealed record RawHttp(string Body, string ContentType);

    public sealed record CsrfDecision(bool Ok, string Message, int UserId, int SessionRecordId)
    {
        public static CsrfDecision Fail(string message) => new(false, message, 0, 0);

        public static CsrfDecision Allow(int userId, int sessionRecordId) => new(true, string.Empty, userId, sessionRecordId);
    }

    public sealed record CsrfBody(
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("status")] bool Status);

    public sealed record WarehouseBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("rows")] IReadOnlyList<WarehouseOfferRow> Rows,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("message")] string Message);

    public sealed record WarehouseOfferRow(
        [property: JsonPropertyName("price_id")] int PriceId,
        [property: JsonPropertyName("price_list")] string PriceList,
        [property: JsonPropertyName("manufacturer")] string Manufacturer,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("article_show")] string ArticleShow,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("price")] decimal? Price,
        [property: JsonPropertyName("exist")] int? Exist,
        [property: JsonPropertyName("storage")] string Storage,
        [property: JsonPropertyName("time_to_exe")] string? TimeToExe);

    public sealed record BrandsBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("manufacturers")] IReadOnlyList<BrandRow> Manufacturers);

    public sealed record BrandRow(
        [property: JsonPropertyName("manufacturer")] string Manufacturer,
        [property: JsonPropertyName("manufacturer_show")] string ManufacturerShow);

    public sealed record CrossBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("references")] IReadOnlyList<CrossRefRow> References,
        [property: JsonPropertyName("stock")] IReadOnlyList<CrossStockJson> Stock,
        [property: JsonPropertyName("prices_visible")] bool PricesVisible);

    public sealed record CrossRefRow(
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("in_stock")] bool InStock);

    public sealed record CrossStockJson(
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("qty")] decimal? Qty,
        [property: JsonPropertyName("delivery")] string Delivery,
        [property: JsonPropertyName("warehouse")] string Warehouse,
        [property: JsonPropertyName("storage_id")] int StorageId,
        [property: JsonPropertyName("price_id")] int PriceId,
        [property: JsonPropertyName("prices_visible")] bool PricesVisible);

    public sealed record BunchesBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("bunches")] IReadOnlyList<BunchJson> Bunches,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("message")] string Message);

    public sealed record BunchJson(
        [property: JsonPropertyName("office_id")] int OfficeId,
        [property: JsonPropertyName("storage_id")] int StorageId,
        [property: JsonPropertyName("protocol_version")] int ProtocolVersion,
        [property: JsonPropertyName("treelax_catalogue")] bool TreelaxCatalogue,
        [property: JsonPropertyName("office_storage_bunches")] IReadOnlyList<BunchJson> Nested);

    public sealed record BunchProductsBody(
        [property: JsonPropertyName("result")] int Result,
        [property: JsonPropertyName("Products")] IReadOnlyList<BunchProductJson> Products,
        [property: JsonPropertyName("prices_visible")] bool PricesVisible,
        [property: JsonPropertyName("message")] string Message);

    public sealed record BunchProductJson(
        [property: JsonPropertyName("manufacturer")] string Manufacturer,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("exist")] int Exist,
        [property: JsonPropertyName("time_to_exe")] string TimeToExe,
        [property: JsonPropertyName("storage")] string Storage,
        [property: JsonPropertyName("storage_id")] int StorageId,
        [property: JsonPropertyName("price_id")] int PriceId);

    public sealed record CartBody(
        [property: JsonPropertyName("cart_items_sum")] string CartItemsSum,
        [property: JsonPropertyName("cart_items_count")] int CartItemsCount);

    public sealed record StatusBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("count")] int? Count);

    public sealed record CountBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("message")] string? Message);

    public sealed record VinBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("code")] int? Code,
        [property: JsonPropertyName("count")] int? Count);
}
