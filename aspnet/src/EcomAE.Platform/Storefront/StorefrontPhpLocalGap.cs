using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string CrossbaseStatusPath = "/api/crossbase_status.php";
    public const string FitmentJsPath = "/api/epartscross_fitment.js.php";
    public const string AiPartsExpertPath = "/api/epc_ai_parts_expert.php";
    public const string PartsAgentPath = "/api/epc_parts_agent.php";
    public const string LaximoProxyPath = "/api/laximo_proxy.php";
    public const string PriceUploadPath = "/api/prices/upload_price.php";
    public const string SkuMediaAdminPath = "/content/shop/catalogue/ajax_epc_sku_media.php";
    public const string TaxExemptUploadPath = "/content/users/ajax_epc_tax_exempt_upload.php";
    public const string PricesSettingsPath = "/api/ajax_get_prices_settings.php";
    public const string PriceLookupPath = "/api/v1/price/lookup.php";
    public const string LicenseActivatePath = "/api/v1/licenses/activate.php";
    public const string OnPremHealthPath = "/api/v1/on-premises/health.php";
    public const string CpUnreadMessagesPath = "/cp/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php";
    public const string CpVinInfoPath = "/cp/content/requests/ajax_get_vin_info.php";
    public const string CpMultilangPath = "/cp/content/lang/ajax_get_multilang_value.php";
    public const string CpStringTranslationPath = "/cp/content/lang/ajax_get_string_translation.php";
    public const string CpStringInfoPath = "/cp/content/lang/ajax_get_str_info.php";
    public const string CpSetStringErrorPath = "/cp/content/lang/ajax_set_is_error.php";
    public const string CpSetStringCustomPath = "/cp/content/lang/ajax_set_is_custom.php";
    public const string CpSetStringSamePath = "/cp/content/lang/ajax_set_same.php";
    public const string CpCategoryTemplatesPath = "/cp/content/shop/catalogue/categories_templates/ajax_templates_actions.php";
    public const string CpOrderItemObjectPath = "/cp/content/shop/sao/ajax_get_order_item_object.php";
    public const string CpSaveStringDescriptionPath = "/cp/content/lang/ajax_save_string_description.php";
    public const string CpSetUsedFoundPath = "/cp/content/lang/ajax_set_used_found.php";
    public const string CpDeleteUnusedStringsPath = "/cp/content/lang/ajax_delete_not_used_found.php";
    public const string CpCreateStringPath = "/cp/content/lang/ajax_create_new_string.php";
    public const string CpSaveTranslationPath = "/cp/content/lang/ajax_save_string_translation.php";
    public const string CpSearchUsedFoundPath = "/cp/content/lang/ajax_search_used_found.php";
    public const string CpContentAliasPath = "/cp/content/content/ajax_check_alias.php";
    public const string CpManufacturersPath = "/cp/content/shop/manufacturers_synonyms/ajax_operations.php";
    public const string CpStorageGroupsPath = "/cp/content/shop/logistics/groups/ajax_operations.php";
    public const string CpReturnActionPath = "/cp/content/shop/returns/ajax/ajax_return_action.php";
    public const string CpStorageTogglePath = "/cp/content/shop/prices_upload/ajax_epc_storefront_storage_toggle.php";
    public const string CpPricePreviewPath = "/cp/content/shop/prices_upload/ajax_get_price_preview.php";
    public const string CpCompletePriceSessionPath = "/cp/content/shop/prices_upload/ajax_6_complete_session.php";
    public const string CpPaymentsPath = "/cp/content/shop/payments/ajax_payments.php";
    public const string CpPaymentsEndpointPath = "/cp/content/shop/payments/ajax_payments_endpoint.php";
    public const string CpPriceDiagnosticsPath = "/cp/content/shop/prices_upload/ajax_epc_price_upload_diagnostics.php";
    public const string CpPricesSendPath = "/cp/content/shop/prices_send/ajax_operations.php";
    public const string CpChannelsPath = "/cp/content/shop/channels/ajax_channels.php";
    public const string CpLogisticsPath = "/cp/content/shop/logistics/ajax_logistics.php";
    public const string CpAccessoryPhotosPath = "/cp/content/shop/accessories/ajax_epc_accessories_photos.php";
    public const string CpPriceHistoryPath = "/cp/content/shop/prices_upload/ajax_epc_price_upload_history.php";
    public const string CpDemandCsvPath = "/cp/content/shop/demand_countries/ajax_epc_demand_csv.php";
    public const string CpPartsAgentPath = "/cp/content/shop/parts_agent/ajax_epc_parts_agent_cp.php";

    public const string CrossbaseUnavailable = "Cross-reference lookup did not return usable data";
    public const string FitmentMissingArticle = "/* epartscross: missing article */";
    public const string FitmentUnavailable = "/* epartscross fitment temporarily unavailable */";
    public const string AiDisabled = "AI Parts Expert search is disabled.";
    public const string AiUnknown = "Unknown action.";
    public const string AiCsrfInvalid = "Invalid or expired security token. Refresh the page and try again.";
    public const string AiShortArticle = "Enter a valid part number (at least 3 characters).";
    public const string AiDatabase = "Database unavailable.";
    public const string AiCrossUnavailable = "Cross-reference data is temporarily unavailable; showing warehouse and catalog fitment only.";
    public const string AiFitmentUnavailable = "Epart catalog fitment lookup is temporarily unavailable.";
    public const string AgentDisabled = "Agent disabled";
    public const string AgentDatabase = "Database unavailable";
    public const string AgentMissingSession = "Missing session_id";
    public const string AgentUnknown = "Unknown action";
    public const string AgentEmpty = "Please type a part number, VIN, or tell me your country and vehicle.";
    public const string AgentHello = "Hello! How can I help you? **Type your question** — part number, brand, VIN, or vehicle.";
    public const string AgentVinFailed = "VIN lookup failed. Try again or send a part request.";
    public const string AgentVinShort = "Please paste your full **VIN** (11–17 characters) and I will decode it.";
    public const string AgentHelp = "I can help you find parts. Try one of these:\n\n• **Part search:** `NGK 4195` or `DENSO K20PRU`\n• **VIN:** paste 17-character VIN\n• **Market:** \"I'm from Sudan — best brand for …\"\n• **Vehicle catalog** for year/make/model browsing";
    public const string LaximoCredentials = "OEM catalog credentials not configured or service unavailable. Configure catalog settings in the control panel.";
    public const string LaximoVinShort = "VIN too short";
    public const string LaximoCatalogRequired = "catalog required";
    public const string LaximoSyncFailed = "Sync failed";
    public const string LaximoService = "Catalog service unavailable";
    public const string PriceUploadNoDb = "NO DB CONNECT";
    public const string PriceWrongKey = "2056";
    public const string PriceImportFailed = "2058";
    public const string PriceNotFound = "2060";
    public const string PriceNoFile = "No uploaded file (use document, file, or price_file)";
    public const string PricesForbidden = "Forbidden";
    public const string PricesUnavailable = "Unavailable";
    public const string SigningUnavailable = "License server signing key is not configured. Contact ecomae support.";
    public const string OnPremLicensesMissing = "On-premises licenses are not in this database.";
    public const string ApiClientsMissing = "API clients are not in this database.";
    public const string WholesaleOnly = "Tax-exempt certificate upload is available for approved wholesale accounts.";
    public const string TaxChooseFile = "Please choose a PDF or image file.";
    public const string TaxTooLarge = "File too large (max 8 MB).";
    public const string TaxBadType = "Allowed formats: PDF, JPG, PNG, WEBP.";
    public const string TaxUploaded = "Certificate uploaded. Our team will review it for tax-exempt checkout eligibility.";
    public const string TaxProfilesMissing = "Customer profiles are not in this database.";
    public const string SkuUnauthorized = "Unauthorized";
    public const string SkuCsrf = "CSRF mismatch";
    public const string SkuNoDatabase = "No database";
    public const string SkuUnknown = "Unknown action";
    public const string SkuNeedIdentity = "Brand and article (or catalogue product) required";
    public const string SkuSaveFirst = "Save the SKU profile first";
    public const string SkuNoFile = "No file";
    public const string LangEditorMissing = "Language editor pages are not in this database.";
    public const string LangStringsMissing = "Text strings are not in this database.";
    public const string LangDenied = "2388";
    public const string LangRestricted = "Editor works in restricted mode. Action was canceled";
    public const string LangTooFew = "Too few arguments";
    public const string LangNoString = "No such string";
    public const string LangSql = "SQL error";
    public const string LangInfoSql = "SQL error getting string info";
    public const string TemplatesMissing = "Category templates are not in this database.";
    public const string TemplateInsertFailed = "2829";
    public const string OrderItemWrongKey = "Wrong key";
    public const string OrderItemsMissingMessage = "Order items are not in this database.";
    public const string DbConnectErrorWord = "DB connect error";
    public const string AiFallbackSecret = "epc-ai-parts-expert";

    public sealed record CodedJson(int StatusCode, object Body, string? HeaderName = null, string? HeaderValue = null);

    public sealed record ApiErrorBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] ApiError Error);

    public sealed record ApiError(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message);

    public sealed record OkMessage(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("message")] string Message);

    public sealed record SkuErrorBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string Error);

    public static bool AgentIsDisabled(IReadOnlyDictionary<string, string> config)
        => config.TryGetValue("epc_parts_agent_enabled", out var value) && string.Equals(value, "0", StringComparison.Ordinal);

    public static bool EditorRestricted(IReadOnlyDictionary<string, string> config)
        => config.TryGetValue("multilang_editor_restricted_mode", out var value)
           && value.Length > 0
           && !string.Equals(value, "0", StringComparison.Ordinal);

    public static bool TechKeyEquals(IReadOnlyDictionary<string, string> config, string? posted)
        => config.TryGetValue("tech_key", out var expected) && string.Equals(expected, posted ?? string.Empty, StringComparison.Ordinal);

    public static string AiExpertToken(IReadOnlyDictionary<string, string> config, DateTime utc)
    {
        var secret = config.TryGetValue("secret_succession", out var configured) && configured.Length > 0
            ? configured
            : AiFallbackSecret;
        var data = Encoding.UTF8.GetBytes("epc-ai-expert|" + utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), data)).ToLowerInvariant();
    }

    public static bool AiExpertTokenValid(IReadOnlyDictionary<string, string> config, string? token, DateTime utc)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var trimmed = token.Trim();
        return string.Equals(AiExpertToken(config, utc), trimmed, StringComparison.Ordinal)
            || string.Equals(AiExpertToken(config, utc.AddDays(-1)), trimmed, StringComparison.Ordinal);
    }

    public static object CrossbaseOffline(string? sample, long crossRows, bool localCrossesOn)
    {
        var article = string.IsNullOrWhiteSpace(sample) ? "C110J" : sample.Trim();
        var offline = crossRows > 100;
        var actions = new List<string>();
        if (!offline)
        {
            actions.Add("Run /epc-offline-resilience-warm.php while the cross-reference service is online, and sync CP crosses for popular parts.");
        }
        else
        {
            actions.Add("Cross-reference service offline — storefront uses saved HTML cache + local CP crosses when available.");
        }

        if (localCrossesOn && crossRows < 500)
        {
            actions.Add("Import more crosses in CP → Shop → Crosses (sync interchange for top sellers).");
        }

        return new CrossbaseBody(
            false,
            0,
            CrossbaseUnavailable,
            article,
            null,
            0,
            0,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            false,
            new CrossCache(0, 0, 0),
            crossRows,
            localCrossesOn,
            offline,
            actions);
    }

    public static async Task<long> CrossRowCountAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                "SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list`",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 0;
        }
    }

    public static object FitmentScript(string? article)
        => new RawHttp(
            string.IsNullOrWhiteSpace(article) ? FitmentMissingArticle : FitmentUnavailable,
            "application/javascript; charset=utf-8");

    public static object AiExpertGate(IReadOnlyDictionary<string, string> config, string action, string? csrf, string? article, DateTime utc)
    {
        if (AgentIsDisabled(config))
        {
            return new OkMessage(false, AiDisabled);
        }

        if (string.Equals(action, "bootstrap", StringComparison.Ordinal))
        {
            return new AiBootstrap(true, AiExpertToken(config, utc));
        }

        if (!string.Equals(action, "search", StringComparison.Ordinal) && action.Length > 0 && !string.Equals(action, "search", StringComparison.Ordinal))
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new OkMessage(false, AiUnknown));
        }

        if (action.Length == 0)
        {
            action = "search";
        }

        if (!string.Equals(action, "search", StringComparison.Ordinal))
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new OkMessage(false, AiUnknown));
        }

        if (!AiExpertTokenValid(config, csrf, utc))
        {
            return new CodedJson(StatusCodes.Status403Forbidden, new OkMessage(false, AiCsrfInvalid));
        }

        var norm = NormalizePriceArticle(article);
        if (norm.Length < 3)
        {
            return new OkMessage(false, AiShortArticle);
        }

        return new AiReady(norm);
    }

    public static async Task<object> AiExpertSearchAsync(
        DbConnection connection,
        string article,
        string? brand,
        CancellationToken cancellationToken)
    {
        var norm = NormalizePriceArticle(article);
        var stock = await LocalStockAsync(connection, norm, cancellationToken).ConfigureAwait(false);
        var fitBrand = string.IsNullOrWhiteSpace(brand)
            ? (stock.Count > 0 ? stock[0].Brand : string.Empty)
            : brand.Trim();
        var partUrl = fitBrand.Length > 0
            ? "/en/parts/" + Uri.EscapeDataString(fitBrand.ToUpperInvariant()) + "/" + Uri.EscapeDataString(norm)
            : stock.Count > 0 ? stock[0].Url : string.Empty;
        return new AiSearchBody(
            true,
            article.Trim(),
            norm,
            fitBrand,
            partUrl,
            stock,
            [],
            null,
            [],
            new AiFitment(false, [], 0, string.Empty, string.Empty),
            new AiSources(true, false, false),
            [AiCrossUnavailable, AiFitmentUnavailable]);
    }

    public static object AgentBootstrap()
        => new AgentBootstrapBody(
            true,
            1200,
            new AgentLinks("/en/zapros-prodavczu", string.Empty),
            []);

    public static async Task<object> AgentActionAsync(
        DbConnection connection,
        string action,
        string? sessionId,
        string? message,
        CancellationToken cancellationToken)
    {
        _ = connection;
        _ = cancellationToken;
        if (string.Equals(action, "history", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return new OkMessage(false, AgentMissingSession);
            }

            return new JsonObject
            {
                ["ok"] = true,
                ["session_id"] = sessionId.Trim(),
                ["messages"] = new JsonArray(),
                ["country_code"] = string.Empty,
                ["country_name"] = string.Empty
            };
        }

        if (action.Length == 0 || string.Equals(action, "chat", StringComparison.Ordinal))
        {
            var id = string.IsNullOrWhiteSpace(sessionId)
                ? "s" + Guid.NewGuid().ToString("N")[..24]
                : sessionId.Trim();
            return new AgentChatBody(true, id, (AgentReply)AgentChatReply(message));
        }

        return new OkMessage(false, AgentUnknown);
    }

    public static object AgentChatReply(string? message)
    {
        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new AgentReply(AgentEmpty, null);
        }

        if (Regex.IsMatch(text, "^(hi|hello|hey|good morning|good evening|salam|assalam|marhaba)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return new AgentReply(AgentHello, ["Search NGK 4195", "Decode VIN", "555 brand"]);
        }

        var vin = Regex.Match(text.ToUpperInvariant(), "\\b([A-HJ-NPR-Z0-9]{11,17})\\b");
        if (vin.Success || Regex.IsMatch(text, "\\bvin\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return vin.Success
                ? new AgentReply(AgentVinFailed, null)
                : new AgentReply(AgentVinShort, null);
        }

        return new AgentReply(AgentHelp, ["Do you have Bentley parts", "NGK 4195", "Decode VIN", "WhatsApp contact"]);
    }

    public static object LaximoLocal(string action, string? vin, string? catalog, bool freshCatalogs, IReadOnlyList<LaximoCatalog> catalogs)
    {
        switch (action)
        {
            case "catalogs":
                return freshCatalogs
                    ? new CodedJson(StatusCodes.Status200OK, new LaximoCatalogsBody(true, "db", catalogs))
                    : new CodedJson(StatusCodes.Status503ServiceUnavailable, new LaximoError(false, LaximoCredentials));
            case "find_vehicle":
                return (vin ?? string.Empty).Trim().Length < 5
                    ? new CodedJson(StatusCodes.Status400BadRequest, new LaximoError(false, LaximoVinShort))
                    : new CodedJson(StatusCodes.Status503ServiceUnavailable, new LaximoError(false, LaximoService));
            case "wizard":
            case "wizard_next":
                return string.IsNullOrWhiteSpace(catalog)
                    ? new CodedJson(StatusCodes.Status400BadRequest, new LaximoError(false, LaximoCatalogRequired))
                    : new CodedJson(StatusCodes.Status503ServiceUnavailable, new LaximoError(false, LaximoService));
            case "sync":
                return new CodedJson(StatusCodes.Status503ServiceUnavailable, new LaximoError(false, LaximoSyncFailed));
            case "vehicle_info":
            case "categories":
            case "units":
            case "unit_details":
            case "quick_groups":
            case "quick_details":
            case "part_search":
            case "part_refs":
            case "applicability":
            case "aftermarket":
            case "sync_status":
            case "status":
                return new CodedJson(StatusCodes.Status503ServiceUnavailable, new LaximoError(false, LaximoService));
            default:
                return new CodedJson(StatusCodes.Status400BadRequest, new LaximoError(false, "Unknown action: " + action));
        }
    }

    public static async Task<(bool Fresh, List<LaximoCatalog> Rows)> LaximoCatalogsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var rows = new List<LaximoCatalog>();
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `code`, `brand`, `name` FROM `epc_laximo_catalogs` WHERE `updated_at` > ? ORDER BY `brand` ASC");
            ErpDb.AddParameters(command, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new LaximoCatalog(
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }

            var usable = rows.Count > 0 && rows.All(row => row.Code.Trim().Length > 0 || row.Brand.Trim().Length > 0 || row.Name.Trim().Length > 0);
            return (usable, usable ? rows : []);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return (false, []);
        }
    }

    public static object PricesSettings(IReadOnlyDictionary<string, string> config, string? techKey, string? allowIps, string? remoteIp)
    {
        if (string.IsNullOrEmpty(techKey))
        {
            return new FlagBody(false, PricesForbidden);
        }

        if (!config.TryGetValue("tech_key", out var expected) || expected.Length == 0 || !TechKeyAccepted(expected, techKey))
        {
            return new FlagBody(false, PricesForbidden);
        }

        if (!string.IsNullOrWhiteSpace(allowIps))
        {
            var allowed = allowIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (allowed.Length > 0 && !allowed.Contains(remoteIp ?? string.Empty, StringComparer.Ordinal))
            {
                return new FlagBody(false, PricesForbidden);
            }
        }

        return new PricesSettingsBody(
            new PricesDb(
                config.TryGetValue("host_external", out var host) ? host : string.Empty,
                config.TryGetValue("user", out var user) ? user : string.Empty,
                config.TryGetValue("db", out var db) ? db : string.Empty,
                null),
            new PricesEmail(
                config.TryGetValue("prices_email_server", out var server) ? server : string.Empty,
                config.TryGetValue("prices_email_encryption", out var encryption) ? encryption : string.Empty,
                config.TryGetValue("prices_email_port", out var port) ? port : string.Empty,
                config.TryGetValue("prices_email_username", out var username) ? username : string.Empty,
                null),
            "redacted",
            true,
            "Ok");
    }

    public static async Task<object> PriceUploadAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        string? techKey,
        int priceId,
        bool hasFile,
        CancellationToken cancellationToken)
    {
        var posted = techKey ?? string.Empty;
        if (posted.Length == 0 || !config.TryGetValue("tech_key", out var expected) || expected.Length == 0 || !TechKeyAccepted(expected, posted))
        {
            return new PriceDataBody(false, PriceWrongKey);
        }

        if (priceId <= 0)
        {
            return new PriceDataBody(false, PriceNotFound);
        }

        try
        {
            var found = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices` WHERE `id` = ?"),
                cancellationToken,
                priceId).ConfigureAwait(false);
            if (found != 1)
            {
                return new PriceDataBody(false, PriceNotFound);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new PriceDataBody(false, PriceListsMissing);
        }

        if (!hasFile)
        {
            return new PriceDataBody(false, PriceNoFile);
        }

        return new PriceDataBody(false, PriceImportFailed);
    }

    public static async Task<object> PriceLookupAsync(
        DbConnection connection,
        string? apiKey,
        string? brand,
        string? article,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ApiFail(StatusCodes.Status401Unauthorized, "missing_api_key", "Send X-API-Key: epc_catalog_… or epc_pricepro_… (issued on onboarding).");
        }

        var raw = apiKey.Trim();
        var keyProduct = raw.StartsWith("epc_pricepro_", StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(raw, "^epc_pricepro_[a-z0-9_]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                ? "price_pro"
                : raw.StartsWith("epc_catalog_", StringComparison.OrdinalIgnoreCase)
                  && Regex.IsMatch(raw, "^epc_catalog_[a-z0-9_]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    ? "catalog"
                    : null;
        if (keyProduct is null)
        {
            return ApiFail(StatusCodes.Status401Unauthorized, "invalid_key_format", "Key must start with epc_catalog_ or epc_pricepro_.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        try
        {
            await ErpDb.ExecuteAsync(connection, null, ApiClientsSql, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return ApiFail(StatusCodes.Status503ServiceUnavailable, "platform_db_unavailable", ApiClientsMissing);
        }

        Dictionary<string, object?>? row;
        try
        {
            row = await OneRowAsync(
                connection,
                "SELECT `id`, `client_key_prefix`, `product`, `label`, `daily_limit`, `calls_today`, `calls_reset_date`, `allowed_actions_json` FROM `epc_api_clients` WHERE `client_key_hash` = ? AND `active` = 1 LIMIT 1",
                cancellationToken,
                hash).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return ApiFail(StatusCodes.Status503ServiceUnavailable, "platform_db_unavailable", ApiClientsMissing);
        }

        if (row is null)
        {
            return ApiFail(StatusCodes.Status401Unauthorized, "invalid_api_key", "API key not recognized or revoked.");
        }

        var product = Convert.ToString(row["product"], CultureInfo.InvariantCulture) ?? string.Empty;
        if (keyProduct != "price_pro" && !string.Equals(product, "both", StringComparison.Ordinal))
        {
            return ApiFail(StatusCodes.Status403Forbidden, "wrong_product_key", "This endpoint requires a price_pro client key.");
        }

        if (!string.Equals(product, "both", StringComparison.Ordinal) && !string.Equals(product, "price_pro", StringComparison.Ordinal))
        {
            return ApiFail(StatusCodes.Status403Forbidden, "product_not_enabled", "This key is not enabled for price_pro.");
        }

        var allowed = Convert.ToString(row["allowed_actions_json"], CultureInfo.InvariantCulture) ?? string.Empty;
        if (allowed.Length > 0 && allowed != "*" && !allowed.Contains("lookup", StringComparison.OrdinalIgnoreCase))
        {
            return ApiFail(StatusCodes.Status403Forbidden, "action_not_allowed", "Action not permitted for this client: lookup");
        }

        var id = Convert.ToInt32(row["id"], CultureInfo.InvariantCulture);
        var limit = Math.Max(1, Convert.ToInt32(row["daily_limit"] ?? 1000, CultureInfo.InvariantCulture));
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var reset = Convert.ToString(row["calls_reset_date"], CultureInfo.InvariantCulture) ?? string.Empty;
        if (!string.Equals(reset, today, StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_api_clients` SET `calls_today` = 0, `calls_reset_date` = ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                today,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                id).ConfigureAwait(false);
            row["calls_today"] = 0L;
        }

        var used = Convert.ToInt32(row["calls_today"] ?? 0, CultureInfo.InvariantCulture);
        if (used >= limit)
        {
            return ApiFail(StatusCodes.Status429TooManyRequests, "daily_quota_exceeded", "Daily API quota exceeded. Contact support to raise your limit.");
        }

        var consumed = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_api_clients` SET `calls_today` = `calls_today` + 1, `time_updated` = ? WHERE `id` = ? AND `calls_today` < ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            id,
            limit).ConfigureAwait(false);
        if (consumed == 0)
        {
            return ApiFail(StatusCodes.Status429TooManyRequests, "daily_quota_exceeded", "Daily API quota exceeded.");
        }

        if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(article))
        {
            return ApiFail(StatusCodes.Status400BadRequest, "missing_params", "Query params brand and article are required.");
        }

        var offers = await PriceOffersAsync(connection, brand.Trim(), article.Trim(), cancellationToken).ConfigureAwait(false);
        return new CodedJson(
            StatusCodes.Status200OK,
            new PriceLookupBody(
                true,
                true,
                brand.Trim(),
                article.Trim(),
                offers,
                offers.Count > 0 ? string.Empty : "Price PRO beta — supplier feeds are enabled per account. Contact sales for full enablement.",
                new PriceClient(
                    Convert.ToString(row["label"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(row["client_key_prefix"], CultureInfo.InvariantCulture) ?? string.Empty)));
    }

    public static async Task<object> LicenseActivateAsync(DbConnection connection, string? rawJson, CancellationToken cancellationToken)
    {
        JsonElement input;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawJson) ? "null" : rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "invalid_json", "Body must be JSON."));
            }

            input = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "invalid_json", "Body must be JSON."));
        }

        try
        {
            await EnsureOnPremAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new CodedJson(StatusCodes.Status503ServiceUnavailable, new LicenseError(false, "platform_db_unavailable", OnPremLicensesMissing));
        }

        var key = Text(input, "license_key").Trim();
        if (!Regex.IsMatch(key, "^LIC-(\\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$"))
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "invalid_key_format", "License key format is invalid."));
        }

        var row = await OneRowAsync(connection, "SELECT * FROM `epc_onprem_licenses` WHERE `license_key` = ? LIMIT 1", cancellationToken, key).ConfigureAwait(false);
        if (row is null)
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "not_found", "License key not recognized."));
        }

        var status = Convert.ToString(row["status"], CultureInfo.InvariantCulture) ?? string.Empty;
        if (string.Equals(status, "revoked", StringComparison.Ordinal))
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "revoked", "This license has been revoked."));
        }

        var expires = ToLong(row.TryGetValue("expires_at", out var expiresValue) ? expiresValue : null);
        if (expires > 0 && expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "expired", "This license has expired."));
        }

        var fingerprint = Text(input, "fingerprint");
        if (fingerprint.Length == 0)
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "missing_fingerprint", "Server fingerprint is required."));
        }

        var bound = Convert.ToString(row.TryGetValue("fingerprint", out var boundValue) ? boundValue : null, CultureInfo.InvariantCulture) ?? string.Empty;
        if (bound.Length > 0 && !string.Equals(bound, fingerprint, StringComparison.Ordinal))
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "already_activated", "This license is already activated on another server. Contact support to transfer it."));
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_onprem_licenses` SET `status` = 'active', `fingerprint` = ?, `hostname` = ?, `ip` = ?, `activated_at` = COALESCE(`activated_at`, ?), `last_seen_at` = ? WHERE `id` = ?"),
            cancellationToken,
            fingerprint,
            Trim(Text(input, "hostname"), 190),
            Trim(Text(input, "ip"), 45),
            now,
            now,
            Convert.ToInt32(row["id"], CultureInfo.InvariantCulture)).ConfigureAwait(false);
        return new CodedJson(StatusCodes.Status400BadRequest, new LicenseError(false, "signing_unavailable", SigningUnavailable));
    }

    public static async Task<object> OnPremHealthAsync(DbConnection connection, string? rawJson, CancellationToken cancellationToken)
    {
        JsonElement input;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawJson) ? "null" : rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object || Text(document.RootElement, "license_key").Length == 0)
            {
                return new CodedJson(StatusCodes.Status400BadRequest, new HealthError(false, "invalid_payload"));
            }

            input = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return new CodedJson(StatusCodes.Status400BadRequest, new HealthError(false, "invalid_payload"));
        }

        var key = Trim(Text(input, "license_key"), 32);
        Dictionary<string, object?>? row;
        try
        {
            row = await OneRowAsync(connection, "SELECT `status` FROM `epc_onprem_licenses` WHERE `license_key` = ? LIMIT 1", cancellationToken, key).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new CodedJson(StatusCodes.Status403Forbidden, new HealthError(false, OnPremLicensesMissing));
        }

        var status = row is null ? string.Empty : Convert.ToString(row["status"], CultureInfo.InvariantCulture) ?? string.Empty;
        if (row is null || string.Equals(status, "revoked", StringComparison.Ordinal))
        {
            return new CodedJson(StatusCodes.Status403Forbidden, new HealthError(false, "unknown_or_revoked_license"));
        }

        try
        {
            await EnsureOnPremAsync(connection, cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_onprem_health_log` (`license_key`, `status`, `uptime`, `disk_free_gb`, `memory_usage_mb`, `php_version`, `db_size_mb`, `last_backup`, `reported_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                key,
                Trim(Text(input, "status"), 40),
                Trim(Text(input, "uptime"), 40),
                Decimal(input, "disk_free_gb"),
                Decimal(input, "memory_usage_mb"),
                Trim(Text(input, "php_version"), 20),
                Decimal(input, "db_size_mb"),
                Trim(Text(input, "last_backup"), 40),
                now).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_onprem_licenses` SET `last_seen_at` = ? WHERE `license_key` = ?"),
                cancellationToken,
                now,
                key).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new CodedJson(StatusCodes.Status403Forbidden, new HealthError(false, OnPremLicensesMissing));
        }

        return new CodedJson(StatusCodes.Status200OK, new HealthOk(true));
    }

    public static async Task<object> SkuMediaAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string action,
        IReadOnlyDictionary<string, string> fields,
        bool hasPhoto,
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
                ParseId(adminUser)).ConfigureAwait(false);
            if (count == 0)
            {
                return SkuErr(SkuUnauthorized);
            }

            if (count != 1)
            {
                return new RawHttp(string.Empty, "text/html; charset=utf-8");
            }

            var stored = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                adminSession ?? string.Empty,
                ParseId(adminUser)).ConfigureAwait(false) ?? string.Empty;
            if (stored.Length > 0 && !string.Equals(stored, postedCsrf ?? string.Empty, StringComparison.Ordinal))
            {
                return SkuErr(SkuCsrf);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return SkuErr(AdminSessionsMissing);
        }

        try
        {
            await EnsureSkuAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return SkuErr(SkuNoDatabase);
        }

        try
        {
            return action switch
            {
                "list" => await SkuListAsync(connection, Field(fields, "q"), cancellationToken).ConfigureAwait(false),
                "get" => await SkuGetAsync(connection, fields, cancellationToken).ConfigureAwait(false),
                "ensure" => await SkuEnsureIdentityAsync(connection, fields, cancellationToken).ConfigureAwait(false),
                "save_profile" => await SkuSaveAsync(connection, fields, cancellationToken).ConfigureAwait(false),
                "delete_profile" => await SkuDeleteAsync(connection, ParseId(Field(fields, "profile_id")), cancellationToken).ConfigureAwait(false),
                "upload_photo" => SkuUpload(ParseId(Field(fields, "profile_id")), hasPhoto),
                "add_spec_group" => await SkuAddGroupAsync(connection, fields, cancellationToken).ConfigureAwait(false),
                "meta" => SkuMeta(),
                _ => SkuErr(SkuUnknown)
            };
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return SkuErr(SkuNoDatabase);
        }
    }

    public static async Task<object> TaxExemptAsync(
        DbConnection connection,
        string? sessionToken,
        string? postedCsrf,
        string fileName,
        long fileSize,
        bool hasFile,
        Stream? file,
        string storageRoot,
        CancellationToken cancellationToken)
    {
        var csrf = await ReadCsrfAsync(connection, sessionToken, postedCsrf, cancellationToken).ConfigureAwait(false);
        if (!csrf.Ok)
        {
            return CsrfFailure(csrf.Message);
        }

        if (csrf.UserId <= 0)
        {
            return new FlagBody(false, SkuUnauthorized);
        }

        var customerType = await ProfileValueAsync(connection, csrf.UserId, "epc_customer_type", cancellationToken).ConfigureAwait(false);
        if (!string.Equals(customerType, "wholesale", StringComparison.Ordinal))
        {
            return new FlagBody(false, WholesaleOnly);
        }

        if (!hasFile || file is null)
        {
            return new FlagBody(false, TaxChooseFile);
        }

        if (fileSize > 8 * 1024 * 1024)
        {
            return new FlagBody(false, TaxTooLarge);
        }

        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (ext is not ("pdf" or "jpg" or "jpeg" or "png" or "webp"))
        {
            return new FlagBody(false, TaxBadType);
        }

        var folder = Path.Combine(storageRoot, csrf.UserId.ToString(CultureInfo.InvariantCulture));
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (IOException)
        {
            return new FlagBody(false, "Could not create upload folder.");
        }

        var safe = "tax_exempt_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "." + ext;
        var dest = Path.Combine(folder, safe);
        await using (var output = File.Create(dest))
        {
            await file.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        var rel = "/content/files/tax_exempt/" + csrf.UserId.ToString(CultureInfo.InvariantCulture) + "/" + safe;
        var wrote = await ProfileSetAsync(connection, csrf.UserId, "epc_tax_exempt_cert_path", rel, cancellationToken).ConfigureAwait(false)
            && await ProfileSetAsync(connection, csrf.UserId, "epc_tax_exempt_cert_status", "pending_review", cancellationToken).ConfigureAwait(false)
            && await ProfileSetAsync(connection, csrf.UserId, "epc_tax_exempt_cert_uploaded_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        if (!wrote)
        {
            return new FlagBody(false, TaxProfilesMissing);
        }

        return new TaxBody(true, TaxUploaded, rel);
    }

    public static async Task<object> UnreadCpAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        bool returns,
        CancellationToken cancellationToken)
        => await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodeBody(false, "Forbidden", 501),
            async _ =>
            {
                var sql = returns
                    ? "SELECT COUNT(*) FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 1 AND `return_id` > 0"
                    : "SELECT COUNT(*) FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 1 AND `order_id` > 0";
                try
                {
                    var count = await ErpDb.LongAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
                    return new CpCountBody(true, count.ToString(CultureInfo.InvariantCulture));
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, OrderMessagesMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static async Task<object> VinBadgeAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        CancellationToken cancellationToken)
        => await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodeBody(false, "Forbidden", 501),
            async _ =>
            {
                try
                {
                    var count = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `users_vin` WHERE `viewed` = 0", cancellationToken).ConfigureAwait(false);
                    return new CpCountBody(true, count.ToString(CultureInfo.InvariantCulture));
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, VinRequestsMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static object MultilangValue(IReadOnlyDictionary<string, string> config, string? postedKey)
    {
        if (postedKey is null || !TechKeyEquals(config, postedKey))
        {
            return new RawHttp("No access", "text/plain; charset=utf-8");
        }

        var on = config.TryGetValue("multilang", out var mode) && string.Equals(mode, "1", StringComparison.Ordinal);
        return new RawHttp(on ? "ON" : "OFF", "text/plain; charset=utf-8");
    }

    public static async Task<object> StringTranslationAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? strKey,
        string? langCode,
        CancellationToken cancellationToken)
        => await LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            async () =>
            {
                try
                {
                    var value = await ErpDb.StringAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1"),
                        cancellationToken,
                        strKey ?? string.Empty,
                        langCode ?? string.Empty).ConfigureAwait(false) ?? string.Empty;
                    return new LangValueBody(true, "OK", value);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, LangStringsMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static async Task<object> StringInfoAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? strKey,
        CancellationToken cancellationToken)
        => await LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            async () =>
            {
                try
                {
                    var languages = new List<string>();
                    await using (var langs = connection.CreateCommand())
                    {
                        langs.CommandText = "SELECT `lang_code` FROM `lang_languages`";
                        await using var reader = await langs.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var code = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                            if (Regex.IsMatch(code, "^[A-Za-z0-9_-]{1,16}$"))
                            {
                                languages.Add(code);
                            }
                        }
                    }

                    var sql = new StringBuilder("SELECT *");
                    foreach (var code in languages)
                    {
                        sql.Append(", (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `lang_code` = '")
                            .Append(code)
                            .Append("' AND `str_key` = `lang_text_strings`.`str_key`) AS `has_")
                            .Append(code)
                            .Append('`');
                    }

                    sql.Append(" FROM `lang_text_strings` WHERE `str_key` = ?");
                    var row = await OneRowAsync(connection, sql.ToString(), cancellationToken, strKey ?? string.Empty).ConfigureAwait(false);
                    if (row is null)
                    {
                        return new FlagBody(false, LangInfoSql);
                    }

                    return new LangInfoBody(true, "OK", RowObject(row));
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, LangStringsMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static async Task<object> SetStringFlagAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string column,
        string? rawValue,
        bool valuePresent,
        string? strKey,
        bool keyPresent,
        CancellationToken cancellationToken)
        => await LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            async () =>
            {
                if (!valuePresent || !keyPresent)
                {
                    return new FlagBody(false, LangTooFew);
                }

                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag))
                {
                    flag = 0;
                }

                if (flag is not (0 or 1))
                {
                    return new FlagBody(false, "Incorrect value of " + column);
                }

                try
                {
                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                        cancellationToken,
                        strKey ?? string.Empty).ConfigureAwait(false);
                    if (count != 1)
                    {
                        return new FlagBody(false, LangNoString);
                    }

                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `lang_text_strings` SET `" + column + "` = ? WHERE `str_key` = ?"),
                        cancellationToken,
                        flag,
                        strKey ?? string.Empty).ConfigureAwait(false);
                    return new JsonObject
                    {
                        ["status"] = true,
                        ["message"] = string.Empty,
                        ["str_key"] = strKey ?? string.Empty,
                        [column] = flag
                    };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, LangStringsMissing);
                }
                catch (DbException)
                {
                    return new FlagBody(false, LangSql);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static async Task<object> SetStringSameAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? same,
        bool samePresent,
        string? strKey,
        bool keyPresent,
        CancellationToken cancellationToken)
        => await LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            async () =>
            {
                if (!samePresent || !keyPresent)
                {
                    return new FlagBody(false, LangTooFew);
                }

                string? stored = same ?? string.Empty;
                try
                {
                    if (!string.Equals(stored, "no", StringComparison.Ordinal))
                    {
                        var languages = await ErpDb.LongAsync(
                            connection,
                            null,
                            ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?"),
                            cancellationToken,
                            stored).ConfigureAwait(false);
                        if (languages != 1)
                        {
                            return new FlagBody(false, "Incorrect value of same");
                        }
                    }
                    else
                    {
                        stored = null;
                    }

                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                        cancellationToken,
                        strKey ?? string.Empty).ConfigureAwait(false);
                    if (count != 1)
                    {
                        return new FlagBody(false, LangNoString);
                    }

                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `lang_text_strings` SET `same` = ? WHERE `str_key` = ?"),
                        cancellationToken,
                        stored ?? (object)DBNull.Value,
                        strKey ?? string.Empty).ConfigureAwait(false);
                    return new LangSameBody(true, string.Empty, strKey ?? string.Empty, stored);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, LangStringsMissing);
                }
                catch (DbException)
                {
                    return new FlagBody(false, LangSql);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static async Task<object> CategoryTemplatesAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? action,
        string? caption,
        string? categoryObject,
        string? templateId,
        CancellationToken cancellationToken)
        => await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new FlagBody(false, "Forbidden"),
            async _ =>
            {
                try
                {
                    switch (action)
                    {
                        case "create":
                            await ErpDb.ExecuteAsync(
                                connection,
                                null,
                                ErpDb.Positional("INSERT INTO `shop_catalogue_categories_templates` (`caption`, `category_object`, `image`, `image_name`) VALUES (?, ?, NULL, NULL)"),
                                cancellationToken,
                                caption ?? string.Empty,
                                categoryObject ?? string.Empty).ConfigureAwait(false);
                            var id = await ErpDb.LongAsync(connection, null, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
                            return new TemplateCreated(true, id.ToString(CultureInfo.InvariantCulture), string.Empty);
                        case "get_all":
                            return await TemplateListAsync(connection, cancellationToken).ConfigureAwait(false);
                        case "delete":
                            await ErpDb.ExecuteAsync(
                                connection,
                                null,
                                ErpDb.Positional("DELETE FROM `shop_catalogue_categories_templates` WHERE `id` = ?"),
                                cancellationToken,
                                ParseId(templateId)).ConfigureAwait(false);
                            return new FlagBody(true, string.Empty);
                        default:
                            return new FlagBody(false, "Forbidden");
                    }
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, TemplatesMissing);
                }
                catch (DbException)
                {
                    return new FlagBody(false, TemplateInsertFailed);
                }
            },
            cancellationToken).ConfigureAwait(false);

    public static async Task<object> OrderItemObjectAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        string? key,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? orderItemId,
        CancellationToken cancellationToken)
    {
        if (!TechKeyEquals(config, key))
        {
            return new FlagBody(false, OrderItemWrongKey);
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new CpCodeBody(false, "Forbidden", 501),
            async _ =>
            {
                try
                {
                    var row = await OneRowAsync(
                        connection,
                        """
                        SELECT `id`,
                        (SELECT `color` FROM `shop_orders_items_statuses_ref` WHERE `id` = `shop_orders_items`.`status`) AS `status_color`,
                        (SELECT `name` FROM `shop_orders_items_statuses_ref` WHERE `id` = `shop_orders_items`.`status`) AS `status_name`,
                        (SELECT `name` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state`) AS `sao_state_name`,
                        (SELECT `color_background` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state`) AS `sao_state_color_background`,
                        (SELECT `color_text` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state`) AS `sao_state_color_text`,
                        `sao_message`, `sao_state`, `t2_storage_id`
                        FROM `shop_orders_items` WHERE `id` = ?
                        """,
                        cancellationToken,
                        ParseId(orderItemId)).ConfigureAwait(false);
                    if (row is null)
                    {
                        return new FlagBody(false, OrderItemsMissingMessage);
                    }

                    var actions = new JsonArray();
                    try
                    {
                        await using var command = connection.CreateCommand();
                        command.CommandText = ErpDb.Positional(
                            """
                            SELECT `id`, `name`, `btn_class`, `fontawesome` FROM `shop_sao_actions`
                            WHERE `id` IN (
                              SELECT `action_id` FROM `shop_sao_states_types_actions_link`
                              WHERE `state_type_id` = (
                                SELECT `id` FROM `shop_sao_states_types_link`
                                WHERE `state_id` = ? AND `interface_type_id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = ?)
                              )
                            )
                            """);
                        ErpDb.AddParameters(command, ToLong(row["sao_state"]), ToLong(row["t2_storage_id"]));
                        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            actions.Add(new JsonObject
                            {
                                ["id"] = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                                ["name"] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                ["btn_class"] = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                ["fontawesome"] = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                            });
                        }
                    }
                    catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                    {
                        actions = [];
                    }

                    return new JsonObject
                    {
                        ["status"] = true,
                        ["order_item_id"] = orderItemId ?? string.Empty,
                        ["item"] = new JsonObject
                        {
                            ["status_color"] = Cell(row, "status_color"),
                            ["status_name"] = Cell(row, "status_name"),
                            ["sao"] = new JsonObject
                            {
                                ["state_name"] = Cell(row, "sao_state_name"),
                                ["state_color_background"] = Cell(row, "sao_state_color_background"),
                                ["state_color_text"] = Cell(row, "sao_state_color_text"),
                                ["message"] = Cell(row, "sao_message"),
                                ["actions"] = actions
                            }
                        }
                    };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, OrderItemsMissingMessage);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> LangAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        Func<Task<object>> body,
        CancellationToken cancellationToken)
        => await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            postedCsrf,
            () => new FlagBody(false, "Forbidden"),
            async adminId =>
            {
                var gate = await LangAccessAsync(connection, adminId, cancellationToken).ConfigureAwait(false);
                return gate ?? await body().ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

    private static async Task<object?> LangAccessAsync(DbConnection connection, int adminId, CancellationToken cancellationToken)
    {
        try
        {
            var allowed = new List<int>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `group_id` FROM `content_access` WHERE `content_id` = (SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1)");
                ErpDb.AddParameters(command, "lang/editor");
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    allowed.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            if (allowed.Count == 0)
            {
                return new LangAccessBody(false, LangDenied, LangDenied);
            }

            var groups = new List<int>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?");
                ErpDb.AddParameters(command, adminId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groups.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            return groups.Any(allowed.Contains) ? null : new LangAccessBody(false, LangDenied, LangDenied);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, LangEditorMissing);
        }
    }

    private static async Task<List<AiStock>> LocalStockAsync(DbConnection connection, string articleNorm, CancellationToken cancellationToken)
    {
        var rows = new List<AiStock>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `manufacturer`, `article`, `article_show`, `name`, `price`, `exist`, `storage`
                FROM `shop_docpart_prices_data`
                WHERE UPPER(REPLACE(REPLACE(REPLACE(`article`, ' ', ''), '-', ''), '_', '')) = ?
                  AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
                ORDER BY IFNULL(`exist`, 0) DESC, `price` ASC
                LIMIT 40
                """);
            ErpDb.AddParameters(command, articleNorm);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var brand = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
                var article = reader.IsDBNull(2) || reader.GetString(2).Length == 0
                    ? (reader.IsDBNull(1) ? string.Empty : reader.GetString(1))
                    : reader.GetString(2);
                rows.Add(new AiStock(
                    brand,
                    article,
                    NormalizePriceArticle(reader.IsDBNull(1) ? string.Empty : reader.GetString(1)),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    string.Empty,
                    reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    brand.Length == 0 ? string.Empty : "/en/parts/" + Uri.EscapeDataString(brand.ToUpperInvariant()) + "/" + Uri.EscapeDataString(NormalizePriceArticle(article))));
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return rows;
        }

        return rows;
    }

    private static async Task<List<PriceOffer>> PriceOffersAsync(DbConnection connection, string brand, string article, CancellationToken cancellationToken)
    {
        var offers = new List<PriceOffer>();
        var brandUpper = brand.ToUpperInvariant();
        var articleNorm = Regex.Replace(article.ToUpperInvariant(), "\\s+", string.Empty);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `manufacturer`, COALESCE(NULLIF(`article_show`, ''), `article`) AS `article`,
                       `name`, `price`, `exist`, `storage`, `time_to_exe`
                FROM `shop_docpart_prices_data`
                WHERE UPPER(TRIM(`manufacturer`)) = ?
                  AND (UPPER(REPLACE(`article`, ' ', '')) = ? OR UPPER(REPLACE(COALESCE(`article_show`, `article`), ' ', '')) = ?)
                  AND IFNULL(`price`, 0) > 0
                ORDER BY `price` ASC
                LIMIT 25
                """);
            ErpDb.AddParameters(command, brandUpper, articleNorm, articleNorm);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                offers.Add(new PriceOffer(
                    reader.IsDBNull(5) || reader.GetString(5).Length == 0 ? "default" : reader.GetString(5),
                    reader.IsDBNull(0) ? brand : reader.GetString(0),
                    reader.IsDBNull(1) ? article : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    "AED",
                    reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    reader.IsDBNull(6) ? string.Empty : Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return offers;
        }

        return offers;
    }

    private static async Task EnsureOnPremAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, OnPremLicenseSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, OnPremHealthSql, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSkuAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in new[] { SkuProfileSql, SkuPhotoSql, SkuGroupSql, SkuRowSql })
        {
            await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<object> SkuListAsync(DbConnection connection, string query, CancellationToken cancellationToken)
    {
        var items = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(query)
            ? "SELECT `id`, `brand`, `article`, `article_key`, `title`, `product_id`, `status` FROM `epc_sku_profiles` ORDER BY `id` DESC LIMIT 120"
            : ErpDb.Positional("SELECT `id`, `brand`, `article`, `article_key`, `title`, `product_id`, `status` FROM `epc_sku_profiles` WHERE `brand` LIKE ? OR `article` LIKE ? OR `title` LIKE ? ORDER BY `id` DESC LIMIT 120");
        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = "%" + query.Trim() + "%";
            ErpDb.AddParameters(command, like, like, like);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var brand = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var article = reader.IsDBNull(2) || reader.GetString(2).Length == 0
                ? (reader.IsDBNull(3) ? string.Empty : reader.GetString(3))
                : reader.GetString(2);
            items.Add(new JsonObject
            {
                ["id"] = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                ["source"] = "profile",
                ["brand"] = brand,
                ["article"] = article,
                ["title"] = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                ["has_profile"] = true,
                ["status"] = reader.IsDBNull(6) ? "active" : reader.GetString(6)
            });
        }

        return new JsonObject
        {
            ["ok"] = true,
            ["items"] = items,
            ["warehouses"] = new JsonArray()
        };
    }

    private static async Task<object> SkuGetAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var profile = await FindSkuAsync(
            connection,
            ParseId(Field(fields, "profile_id")),
            ParseId(Field(fields, "product_id")),
            Field(fields, "brand"),
            Field(fields, "article"),
            cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return new JsonObject { ["ok"] = true, ["payload"] = null };
        }

        return new JsonObject
        {
            ["ok"] = true,
            ["payload"] = await SkuPayloadAsync(connection, Convert.ToInt32(profile["id"], CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false)
        };
    }

    private static async Task<object> SkuEnsureIdentityAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var productId = ParseId(Field(fields, "product_id"));
        var brand = NormalizeSkuBrand(Field(fields, "brand"));
        var article = Field(fields, "article").Trim();
        if (productId <= 0 && (brand.Length == 0 || article.Length == 0))
        {
            return SkuErr(SkuNeedIdentity);
        }

        var existing = await FindSkuAsync(connection, 0, productId, brand, article, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var title = Field(fields, "title").Trim();
            if (title.Length == 0 && brand.Length > 0)
            {
                title = brand + " " + article;
            }

            await InsertSkuAsync(connection, productId, brand, article, title, string.Empty, "active", cancellationToken).ConfigureAwait(false);
            existing = await FindSkuAsync(connection, 0, productId, brand, article, cancellationToken).ConfigureAwait(false);
        }

        var id = existing is null ? 0 : Convert.ToInt32(existing["id"], CultureInfo.InvariantCulture);
        return new JsonObject { ["ok"] = true, ["payload"] = await SkuPayloadAsync(connection, id, cancellationToken).ConfigureAwait(false) };
    }

    private static async Task<object> SkuSaveAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var id = ParseId(Field(fields, "profile_id"));
        var productId = ParseId(Field(fields, "product_id"));
        var brand = NormalizeSkuBrand(Field(fields, "brand"));
        var article = Field(fields, "article").Trim();
        var title = Field(fields, "title").Trim();
        var subtitle = Field(fields, "subtitle").Trim();
        var status = Field(fields, "status").Trim();
        if (status.Length == 0)
        {
            status = "active";
        }

        if (id <= 0)
        {
            var existing = await FindSkuAsync(connection, 0, productId, brand, article, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                id = Convert.ToInt32(existing["id"], CultureInfo.InvariantCulture);
            }
        }

        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_sku_profiles` SET `product_id` = ?, `brand` = ?, `article` = ?, `article_key` = ?, `title` = ?, `subtitle` = ?, `status` = ?, `updated_at` = ? WHERE `id` = ?"),
                cancellationToken,
                productId > 0 ? productId : DBNull.Value,
                brand,
                article.Length > 0 ? article : NormalizeSkuArticle(article),
                NormalizeSkuArticle(article),
                title,
                subtitle,
                status,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                id).ConfigureAwait(false);
        }
        else
        {
            await InsertSkuAsync(connection, productId, brand, article, title, subtitle, status, cancellationToken).ConfigureAwait(false);
            var created = await FindSkuAsync(connection, 0, productId, brand, article, cancellationToken).ConfigureAwait(false);
            id = created is null ? 0 : Convert.ToInt32(created["id"], CultureInfo.InvariantCulture);
        }

        return new JsonObject { ["ok"] = true, ["payload"] = await SkuPayloadAsync(connection, id, cancellationToken).ConfigureAwait(false) };
    }

    private static async Task InsertSkuAsync(
        DbConnection connection,
        int productId,
        string brand,
        string article,
        string title,
        string subtitle,
        string status,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var key = NormalizeSkuArticle(article);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_sku_profiles` (`product_id`, `brand`, `article`, `article_key`, `title`, `subtitle`, `status`, `created_at`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            productId > 0 ? productId : DBNull.Value,
            brand,
            article.Length > 0 ? article : key,
            key,
            title,
            subtitle,
            status,
            now,
            now).ConfigureAwait(false);
    }

    private static async Task<object> SkuDeleteAsync(DbConnection connection, int profileId, CancellationToken cancellationToken)
    {
        if (profileId <= 0)
        {
            return new JsonObject { ["ok"] = false };
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_sku_spec_rows` WHERE `profile_id` = ?"), cancellationToken, profileId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_sku_spec_groups` WHERE `profile_id` = ?"), cancellationToken, profileId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_sku_photos` WHERE `profile_id` = ?"), cancellationToken, profileId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `epc_sku_profiles` WHERE `id` = ?"), cancellationToken, profileId).ConfigureAwait(false);
        return new JsonObject { ["ok"] = true };
    }

    private static object SkuUpload(int profileId, bool hasPhoto)
    {
        if (profileId <= 0)
        {
            return SkuErr(SkuSaveFirst);
        }

        return hasPhoto ? SkuErr("Upload failed") : SkuErr(SkuNoFile);
    }

    private static async Task<object> SkuAddGroupAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var profileId = ParseId(Field(fields, "profile_id"));
        if (profileId <= 0)
        {
            return SkuErr("Missing profile");
        }

        var name = Field(fields, "name").Trim();
        if (name.Length == 0)
        {
            return SkuErr("Group name required");
        }

        var code = Field(fields, "code").Trim();
        if (code.Length == 0)
        {
            code = Regex.Replace(name, "[^a-zA-Z0-9]+", "_").Trim('_').ToLowerInvariant();
            if (code.Length == 0)
            {
                code = "custom";
            }
        }

        var icon = Field(fields, "icon").Trim();
        if (icon.Length == 0)
        {
            icon = "fa-list";
        }

        var sort = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(MAX(`sort_order`), 0) FROM `epc_sku_spec_groups` WHERE `profile_id` = ?"),
            cancellationToken,
            profileId).ConfigureAwait(false) + 10;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_sku_spec_groups` (`profile_id`, `name`, `code`, `icon`, `sort_order`, `created_at`) VALUES (?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            profileId,
            name,
            code,
            icon,
            sort,
            now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(connection, null, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
        return new JsonObject
        {
            ["ok"] = true,
            ["group_id"] = id,
            ["payload"] = await SkuPayloadAsync(connection, profileId, cancellationToken).ConfigureAwait(false)
        };
    }

    private static JsonObject SkuMeta()
        => new()
        {
            ["ok"] = true,
            ["photo_types"] = new JsonObject
            {
                ["product"] = "Product",
                ["packaging"] = "Packaging",
                ["detail"] = "Detail / close-up",
                ["diagram"] = "Diagram / drawing",
                ["install"] = "Installation",
                ["datasheet"] = "Datasheet shot",
                ["other"] = "Other"
            },
            ["value_types"] = new JsonObject
            {
                ["text"] = "Text",
                ["number"] = "Number",
                ["bool"] = "Yes / No",
                ["list"] = "List (comma-separated)",
                ["rich"] = "Rich text"
            }
        };

    private static async Task<JsonObject?> SkuPayloadAsync(DbConnection connection, int profileId, CancellationToken cancellationToken)
    {
        var profile = await FindSkuAsync(connection, profileId, 0, string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        var groups = new JsonArray();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `id`, `name`, `code`, `icon` FROM `epc_sku_spec_groups` WHERE `profile_id` = ? ORDER BY `sort_order`, `id`");
            ErpDb.AddParameters(command, profileId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                groups.Add(new JsonObject
                {
                    ["id"] = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["name"] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    ["code"] = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    ["icon"] = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ["rows"] = new JsonArray()
                });
            }
        }

        var brand = Convert.ToString(profile["brand"], CultureInfo.InvariantCulture) ?? string.Empty;
        var article = Convert.ToString(profile["article"], CultureInfo.InvariantCulture) ?? string.Empty;
        profile["storefront_url"] = article.Length == 0
            ? string.Empty
            : "/en/parts/" + Uri.EscapeDataString(brand.ToUpperInvariant()) + "/" + Uri.EscapeDataString(NormalizeSkuArticle(article));
        return new JsonObject
        {
            ["profile"] = RowObject(profile),
            ["photos"] = new JsonArray(),
            ["spec_groups"] = groups,
            ["storefront_url"] = Convert.ToString(profile["storefront_url"], CultureInfo.InvariantCulture)
        };
    }

    private static async Task<Dictionary<string, object?>?> FindSkuAsync(
        DbConnection connection,
        int profileId,
        int productId,
        string brand,
        string article,
        CancellationToken cancellationToken)
    {
        if (profileId > 0)
        {
            return await OneRowAsync(connection, "SELECT * FROM `epc_sku_profiles` WHERE `id` = ? LIMIT 1", cancellationToken, profileId).ConfigureAwait(false);
        }

        if (productId > 0)
        {
            var byProduct = await OneRowAsync(connection, "SELECT * FROM `epc_sku_profiles` WHERE `product_id` = ? ORDER BY `id` DESC LIMIT 1", cancellationToken, productId).ConfigureAwait(false);
            if (byProduct is not null)
            {
                return byProduct;
            }
        }

        var key = NormalizeSkuArticle(article);
        var normalizedBrand = NormalizeSkuBrand(brand);
        if (normalizedBrand.Length > 0 && key.Length > 0)
        {
            return await OneRowAsync(
                connection,
                "SELECT * FROM `epc_sku_profiles` WHERE UPPER(`brand`) = ? AND `article_key` = ? ORDER BY `id` DESC LIMIT 1",
                cancellationToken,
                normalizedBrand,
                key).ConfigureAwait(false);
        }

        return key.Length == 0
            ? null
            : await OneRowAsync(connection, "SELECT * FROM `epc_sku_profiles` WHERE `article_key` = ? ORDER BY `id` DESC LIMIT 1", cancellationToken, key).ConfigureAwait(false);
    }

    private static async Task<object> TemplateListAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var templates = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `caption`, `image_name`, `category_object` FROM `shop_catalogue_categories_templates` ORDER BY `id` ASC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            templates.Add(new JsonObject
            {
                ["id"] = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                ["value"] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ["image"] = string.Empty,
                ["image_name"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                ["category_object"] = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }

        return new JsonObject
        {
            ["status"] = true,
            ["message"] = string.Empty,
            ["templates"] = templates
        };
    }

    private static async Task<string> ProfileValueAsync(DbConnection connection, int userId, string key, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1"),
                cancellationToken,
                userId,
                key).ConfigureAwait(false) ?? string.Empty;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return string.Empty;
        }
    }

    private static async Task<bool> ProfileSetAsync(DbConnection connection, int userId, string key, string value, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?"),
                cancellationToken,
                userId,
                key).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"),
                cancellationToken,
                userId,
                key,
                value).ConfigureAwait(false);
            return true;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return false;
        }
    }

    private static async Task<Dictionary<string, object?>?> OneRowAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql.Contains('?', StringComparison.Ordinal) ? ErpDb.Positional(sql) : sql;
        if (parameters.Length > 0)
        {
            ErpDb.AddParameters(command, parameters);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static JsonObject RowObject(Dictionary<string, object?> row)
    {
        var obj = new JsonObject();
        foreach (var pair in row)
        {
            obj[pair.Key] = pair.Value switch
            {
                null => null,
                string text => System.Text.Json.Nodes.JsonValue.Create(text),
                bool flag => System.Text.Json.Nodes.JsonValue.Create(flag),
                _ => System.Text.Json.Nodes.JsonValue.Create(Convert.ToString(pair.Value, CultureInfo.InvariantCulture))
            };
        }

        return obj;
    }

    private static JsonNode? Cell(Dictionary<string, object?> row, string name)
        => row.TryGetValue(name, out var value) && value is not null
            ? System.Text.Json.Nodes.JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))
            : null;

    private static CodedJson ApiFail(int status, string code, string message)
        => new(status, new ApiErrorBody(false, new ApiError(code, message)), "X-ECOM-API-Client", "1");

    private static JsonObject SkuErr(string error)
        => new() { ["ok"] = false, ["error"] = error };

    private static string Field(IReadOnlyDictionary<string, string> fields, string name)
        => fields.TryGetValue(name, out var value) ? value : string.Empty;

    private static string Text(JsonElement input, string name)
        => input.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;

    private static decimal Decimal(JsonElement input, string name)
        => input.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number) ? number : 0m;

    private static long ToLong(object? value)
        => value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);

    private static string Trim(string value, int max)
        => value.Length <= max ? value : value[..max];

    private const string ApiClientsSql = """
        CREATE TABLE IF NOT EXISTS `epc_api_clients` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `client_key_hash` CHAR(64) NOT NULL,
          `client_key_prefix` VARCHAR(32) NOT NULL DEFAULT '',
          `product` ENUM('catalog','price_pro','both') NOT NULL DEFAULT 'catalog',
          `label` VARCHAR(120) NOT NULL DEFAULT '',
          `contact_email` VARCHAR(190) NOT NULL DEFAULT '',
          `active` TINYINT(1) NOT NULL DEFAULT 1,
          `daily_limit` INT NOT NULL DEFAULT 1000,
          `calls_today` INT NOT NULL DEFAULT 0,
          `calls_reset_date` DATE NULL,
          `allowed_actions_json` TEXT NOT NULL,
          `time_created` INT NOT NULL DEFAULT 0,
          `time_updated` INT NOT NULL DEFAULT 0,
          UNIQUE KEY `client_key_hash` (`client_key_hash`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """;

    private const string OnPremLicenseSql = """
        CREATE TABLE IF NOT EXISTS `epc_onprem_licenses` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `license_key` VARCHAR(32) NOT NULL,
          `customer_name` VARCHAR(190) NOT NULL DEFAULT '',
          `tier` ENUM('standard','professional','enterprise') NOT NULL DEFAULT 'standard',
          `modules_json` TEXT NOT NULL,
          `users_max` INT UNSIGNED NOT NULL DEFAULT 25,
          `status` ENUM('issued','active','revoked','expired') NOT NULL DEFAULT 'issued',
          `fingerprint` CHAR(64) NULL DEFAULT NULL,
          `hostname` VARCHAR(190) NOT NULL DEFAULT '',
          `ip` VARCHAR(45) NOT NULL DEFAULT '',
          `issued_at` INT NOT NULL DEFAULT 0,
          `activated_at` INT NULL DEFAULT NULL,
          `last_seen_at` INT NULL DEFAULT NULL,
          `expires_at` INT NULL DEFAULT NULL,
          `notes` VARCHAR(500) NOT NULL DEFAULT '',
          UNIQUE KEY `license_key` (`license_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """;

    private const string OnPremHealthSql = """
        CREATE TABLE IF NOT EXISTS `epc_onprem_health_log` (
          `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `license_key` VARCHAR(32) NOT NULL,
          `status` VARCHAR(40) NOT NULL DEFAULT '',
          `uptime` VARCHAR(40) NOT NULL DEFAULT '',
          `disk_free_gb` DECIMAL(10,1) NOT NULL DEFAULT 0,
          `memory_usage_mb` DECIMAL(10,1) NOT NULL DEFAULT 0,
          `php_version` VARCHAR(20) NOT NULL DEFAULT '',
          `db_size_mb` DECIMAL(10,1) NOT NULL DEFAULT 0,
          `last_backup` VARCHAR(40) NOT NULL DEFAULT '',
          `reported_at` INT NOT NULL DEFAULT 0,
          KEY `license_key_date` (`license_key`, `reported_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """;

    private const string SkuProfileSql = """
        CREATE TABLE IF NOT EXISTS `epc_sku_profiles` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `product_id` INT UNSIGNED NULL DEFAULT NULL,
          `brand` VARCHAR(120) NOT NULL DEFAULT '',
          `article` VARCHAR(120) NOT NULL DEFAULT '',
          `article_key` VARCHAR(120) NOT NULL DEFAULT '',
          `title` VARCHAR(255) NOT NULL DEFAULT '',
          `subtitle` VARCHAR(255) NOT NULL DEFAULT '',
          `status` VARCHAR(24) NOT NULL DEFAULT 'active',
          `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
          `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    private const string SkuPhotoSql = """
        CREATE TABLE IF NOT EXISTS `epc_sku_photos` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `profile_id` INT UNSIGNED NOT NULL,
          `file_name` VARCHAR(255) NOT NULL DEFAULT '',
          `alt` VARCHAR(255) NOT NULL DEFAULT '',
          `caption` VARCHAR(255) NOT NULL DEFAULT '',
          `photo_type` VARCHAR(48) NOT NULL DEFAULT 'product',
          `sort_order` INT NOT NULL DEFAULT 0,
          `is_primary` TINYINT(1) NOT NULL DEFAULT 0,
          `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    private const string SkuGroupSql = """
        CREATE TABLE IF NOT EXISTS `epc_sku_spec_groups` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `profile_id` INT UNSIGNED NOT NULL,
          `name` VARCHAR(120) NOT NULL DEFAULT '',
          `code` VARCHAR(64) NOT NULL DEFAULT '',
          `icon` VARCHAR(48) NOT NULL DEFAULT 'fa-list',
          `sort_order` INT NOT NULL DEFAULT 0,
          `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    private const string SkuRowSql = """
        CREATE TABLE IF NOT EXISTS `epc_sku_spec_rows` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
          `group_id` INT UNSIGNED NOT NULL,
          `profile_id` INT UNSIGNED NOT NULL,
          `label` VARCHAR(190) NOT NULL DEFAULT '',
          `value` TEXT NULL,
          `value_type` VARCHAR(24) NOT NULL DEFAULT 'text',
          `unit` VARCHAR(48) NOT NULL DEFAULT '',
          `sort_order` INT NOT NULL DEFAULT 0,
          `created_at` INT UNSIGNED NOT NULL DEFAULT 0,
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    public sealed record CrossbaseBody(
        [property: JsonPropertyName("connected")] bool Connected,
        [property: JsonPropertyName("status_code")] int StatusCode,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("sample")] string Sample,
        [property: JsonPropertyName("references_total")] int? ReferencesTotal,
        [property: JsonPropertyName("rows_parsed")] int RowsParsed,
        [property: JsonPropertyName("response_ms")] int ResponseMs,
        [property: JsonPropertyName("last_checked")] long LastChecked,
        [property: JsonPropertyName("used_stale_cache")] bool UsedStaleCache,
        [property: JsonPropertyName("cache")] CrossCache Cache,
        [property: JsonPropertyName("cp_cross_rows")] long CpCrossRows,
        [property: JsonPropertyName("local_crosses_on")] bool LocalCrossesOn,
        [property: JsonPropertyName("offline_ready")] bool OfflineReady,
        [property: JsonPropertyName("action_required")] IReadOnlyList<string> ActionRequired);

    public sealed record CrossCache(
        [property: JsonPropertyName("files_total")] int FilesTotal,
        [property: JsonPropertyName("files_fresh")] int FilesFresh,
        [property: JsonPropertyName("files_stale")] int FilesStale);

    public sealed record AiBootstrap(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("csrf")] string Csrf);

    public sealed record AiReady(string ArticleNorm);

    public sealed record AiSearchBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("article_norm")] string ArticleNorm,
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("part_url")] string PartUrl,
        [property: JsonPropertyName("local_stock")] IReadOnlyList<AiStock> LocalStock,
        [property: JsonPropertyName("cross_refs")] IReadOnlyList<object> CrossRefs,
        [property: JsonPropertyName("cross_refs_total_hint")] int? CrossRefsTotalHint,
        [property: JsonPropertyName("umapi_brands")] IReadOnlyList<object> UmapiBrands,
        [property: JsonPropertyName("fitment")] AiFitment Fitment,
        [property: JsonPropertyName("sources")] AiSources Sources,
        [property: JsonPropertyName("messages")] IReadOnlyList<string> Messages);

    public sealed record AiStock(
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("article_norm")] string ArticleNorm,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("price")] string Price,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("qty")] decimal Qty,
        [property: JsonPropertyName("warehouse")] string Warehouse,
        [property: JsonPropertyName("url")] string Url);

    public sealed record AiFitment(
        [property: JsonPropertyName("available")] bool Available,
        [property: JsonPropertyName("rows")] IReadOnlyList<object> Rows,
        [property: JsonPropertyName("vehicle_count")] int VehicleCount,
        [property: JsonPropertyName("part_name")] string PartName,
        [property: JsonPropertyName("product_group")] string ProductGroup);

    public sealed record AiSources(
        [property: JsonPropertyName("local")] bool Local,
        [property: JsonPropertyName("crossbase")] bool Crossbase,
        [property: JsonPropertyName("umapi")] bool Umapi);

    public sealed record AgentBootstrapBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("proactive_delay_ms")] int ProactiveDelayMs,
        [property: JsonPropertyName("links")] AgentLinks Links,
        [property: JsonPropertyName("countries")] IReadOnlyList<object> Countries);

    public sealed record AgentLinks(
        [property: JsonPropertyName("part_request")] string PartRequest,
        [property: JsonPropertyName("whatsapp")] string Whatsapp);

    public sealed record AgentReply(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("suggestions")] IReadOnlyList<string>? Suggestions);

    public sealed record AgentChatBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("reply")] AgentReply Reply);

    public sealed record LaximoError(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("error")] string Error);

    public sealed record LaximoCatalog(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("name")] string Name);

    public sealed record LaximoCatalogsBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("catalogs")] IReadOnlyList<LaximoCatalog> Catalogs);

    public sealed record PricesSettingsBody(
        [property: JsonPropertyName("db")] PricesDb Db,
        [property: JsonPropertyName("prices_email")] PricesEmail PricesEmail,
        [property: JsonPropertyName("secrets")] string Secrets,
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message);

    public sealed record PricesDb(
        [property: JsonPropertyName("host")] string Host,
        [property: JsonPropertyName("user")] string User,
        [property: JsonPropertyName("db")] string Db,
        [property: JsonPropertyName("password")] string? Password);

    public sealed record PricesEmail(
        [property: JsonPropertyName("prices_email_server")] string Server,
        [property: JsonPropertyName("prices_email_encryption")] string Encryption,
        [property: JsonPropertyName("prices_email_port")] string Port,
        [property: JsonPropertyName("prices_email_username")] string Username,
        [property: JsonPropertyName("prices_email_password")] string? Password);

    public sealed record PriceDataBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("data")] string Data);

    public sealed record PriceLookupBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("beta")] bool Beta,
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("offers")] IReadOnlyList<PriceOffer> Offers,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("client")] PriceClient Client);

    public sealed record PriceOffer(
        [property: JsonPropertyName("supplier")] string Supplier,
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("stock_hint")] int StockHint,
        [property: JsonPropertyName("lead_time")] string LeadTime);

    public sealed record PriceClient(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("key_prefix")] string KeyPrefix);

    public sealed record LicenseError(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("message")] string Message);

    public sealed record HealthError(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string Error);

    public sealed record HealthOk([property: JsonPropertyName("ok")] bool Ok);

    public sealed record TaxBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("path")] string Path);

    public sealed record CpCodeBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("code")] int Code);

    public sealed record LangValueBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("value")] string Value);

    public sealed record LangInfoBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("str_info")] JsonObject StrInfo);

    public sealed record LangAccessBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("message")] string Message);

    public sealed record LangSameBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("str_key")] string StrKey,
        [property: JsonPropertyName("same")] string? Same);

    public sealed record TemplateCreated(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("template_id")] string TemplateId,
        [property: JsonPropertyName("message")] string Message);
}
