using System.Globalization;
using System.Text.Json.Serialization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using EcomAE.Platform.Surfaces;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Modules;

public sealed class StorefrontModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "storefront",
        "Storefront / Marketing",
        "/",
        "content/shop/, content/general_pages/, templates/",
        "presentation-shell-scaffolded",
        []);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.StorefrontParity, (IStorefrontParityReporter reporter) => Results.Ok(reporter.BuildReport()));

        endpoints.MapGet("/storefront/migration-placeholder", (
            HttpContext context,
            ISurfaceShellCatalog shells,
            ILegacyHtmlShellRenderer html) =>
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            return SurfaceShellResponder.Respond(
                context,
                "storefront",
                shells,
                html,
                tenant,
                new { kind = "anonymous", note = "migration placeholder" },
                "Presentation-preserving storefront placeholder. PHP storefront remains authoritative.");
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontAccount, async (
            HttpContext context,
            ISurfaceShellCatalog shells,
            ILegacyHtmlShellRenderer html,
            ILegacySessionValidator validator,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront account shell.");
            }

            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            return SurfaceShellResponder.Respond(
                context,
                "storefront",
                shells,
                html,
                tenant,
                SessionPayload(session),
                "Customer-gated account shell only. PHP storefront remains authoritative.");
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontAccountSummary, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront account summary.");
            }

            var result = await dashboards.BuildStorefrontAccountAsync(session.UserId, 10, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                summary = result.Summary,
                recentOrders = result.RecentOrders,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only account KPIs + recent orders. PHP customer account remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontOrders, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront orders digest.");
            }

            var result = await dashboards.ListStorefrontOrdersAsync(session.UserId, limit ?? 25, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = result.UserId,
                orders = result.Orders,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only recent shop_orders digest. PHP customer orders remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontGarage, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront garage digest.");
            }

            var result = await dashboards.ListStorefrontGarageAsync(session.UserId, limit ?? 50, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = result.UserId,
                vehicles = result.Vehicles,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only shop_docpart_garage digest. PHP garage remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontProfile, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront profile digest.");
            }

            var result = await dashboards.BuildStorefrontProfileAsync(session.UserId, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = result.UserId,
                email = result.Email,
                email_confirmed = result.EmailConfirmed,
                phone = result.Phone,
                phone_confirmed = result.PhoneConfirmed,
                reg_variant = result.RegVariant,
                profile_fields = result.ProfileFields,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only users/users_profiles digest. PHP DP_User::getUserProfile remains authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontSearch, async (
            HttpContext context,
            string? article,
            string? brand,
            string? brend,
            int? limit,
            ILegacySessionValidator validator,
            IStorefrontPriceAccess priceAccess,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            // PHP part_search / warehouse offers are public; attach session when present.
            // Prefer brand= (ASP.NET); accept brend= (PHP legacy typo).
            var session = await validator.ValidateAsync(context, cancellationToken);
            var access = await priceAccess.ResolveAsync(context, cancellationToken);
            var manufacturer = string.IsNullOrWhiteSpace(brand) ? brend : brand;
            var result = await dashboards.SearchStorefrontPartsAsync(
                article ?? string.Empty,
                manufacturer,
                limit ?? 25,
                cancellationToken);
            var rows = access.PricesVisible ? result.Rows : priceAccess.RedactOffers(result.Rows);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                article = result.Article,
                brand = manufacturer,
                rows,
                count = rows.Count,
                prices_visible = access.PricesVisible,
                show_purchase_cost = access.ShowPurchaseCost,
                access_state = access.StateToken,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Public warehouse digest with PHP price-visibility gate (guest/wholesale approval)."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontSearchBrands, async (
            HttpContext context,
            string? article,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            // PHP ajax_epc_article_brands is public (guest brand picker).
            var session = await validator.ValidateAsync(context, cancellationToken);
            var result = await dashboards.ListStorefrontArticleBrandsAsync(article ?? string.Empty, limit ?? 100, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                article = result.Article,
                brands = result.Brands,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Public article brand picker (PHP ajax_epc_article_brands: warehouse + CP crosses)."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontCart, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateCustomerAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Please log in or register to continue.");
            }

            var result = await dashboards.ListStorefrontCartAsync(session.UserId, limit ?? 50, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = result.UserId,
                summary = result.Summary,
                lines = result.Lines,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only authenticated shop_carts digest. Qty/guest cart/checkout writes remain PHP /shop/cart."
            });
        });

        endpoints.MapGet("/storefront/quotes", async (
            HttpContext context,
            int? limit,
            int? id,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for storefront quotes digest.");
            }

            if (id is > 0)
            {
                var detail = await dashboards.GetStorefrontQuoteAsync(session.UserId, id.Value, cancellationToken);
                if (detail is null)
                {
                    return Results.NotFound(new { ok = false, message = "Quote not found for this customer." });
                }

                return Results.Ok(new
                {
                    ok = true,
                    surface = "storefront",
                    quote = detail,
                    session = SessionPayload(session),
                    note = "Read-only quote detail. Submit/accept remain PHP."
                });
            }

            var list = await dashboards.ListStorefrontQuotesAsync(session.UserId, limit ?? 50, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = list.UserId,
                rows = list.Rows,
                count = list.Count,
                source = list.Source,
                message = list.Message,
                session = SessionPayload(session),
                note = "Read-only customer shop_quote_requests digest. Submit/accept remain PHP."
            });
        });

        endpoints.MapGet("/storefront/product", async (
            int? id,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var result = await dashboards.GetStorefrontProductAsync(id ?? 0, cancellationToken);
            return Results.Ok(new
            {
                ok = result.Product is not null,
                surface = "storefront",
                product = result.Product,
                source = result.Source,
                message = result.Message,
                note = "Read-only catalogue product digest with media/specs. Cart/bookmark/compare writes remain PHP."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontCatalogueTree, async (
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var result = await dashboards.ListStorefrontCatalogueTreeAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = result.Source is "database" or "empty",
                surface = "storefront",
                tree = result.Tree,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                note = "Read-only shop_catalogue_categories tree for Catalog of products mega menu (PHP dp_menu). APAI aliases filtered."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontCatalogueProducts, async (
            int? category_id,
            string? url,
            string? search_string,
            string? q,
            int? limit,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var search = !string.IsNullOrWhiteSpace(search_string) ? search_string : q;
            var result = await dashboards.ListStorefrontCatalogueProductsAsync(
                category_id ?? 0,
                url,
                search,
                limit ?? 48,
                cancellationToken);
            return Results.Ok(new
            {
                ok = result.Source is "database" or "empty",
                surface = "storefront",
                category_id = result.CategoryId,
                category_url = result.CategoryUrl,
                category_value = result.CategoryValue,
                search = result.Search,
                products = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                note = "Read-only own-catalogue products. Cart/writes remain PHP-authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontGenuineBrands, async (
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var result = await dashboards.ListStorefrontGenuineBrandsAsync(cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                brands = result.Brands,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                note = "Genuine OE manufacturer keys (UMAPI passenger/commercial/motorbike + synonyms)."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontSearchBunches, async (
            string? article,
            string? brand,
            string? brend,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var manufacturer = string.IsNullOrWhiteSpace(brand) ? brend : brand;
            var result = await dashboards.ListStorefrontOfficeStorageBunchesAsync(
                article ?? string.Empty,
                manufacturer,
                cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                article = result.Article,
                brand = result.Brand,
                bunches = result.Bunches,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                note = "Office/storage bunches for progressive ajax_getProductsOfBunch poll."
            });
        });

        // PHP ajax_epc_sku_media_public lookup — CHPU Spec splash + Photos gallery + row photos.
        async Task<IResult> SkuMediaLookup(
            string? brand,
            string? brend,
            string? article,
            IStorefrontSkuMediaService skuMedia,
            CancellationToken cancellationToken)
        {
            var manufacturer = string.IsNullOrWhiteSpace(brand) ? brend : brand;
            var result = await skuMedia.LookupAsync(
                manufacturer ?? string.Empty,
                article ?? string.Empty,
                cancellationToken);
            return Results.Ok(new
            {
                ok = result.Ok,
                url = result.Url,
                photos = result.Photos.Select(p => new
                {
                    url = p.Url,
                    alt = p.Alt,
                    caption = p.Caption,
                    photo_type = p.PhotoType,
                    is_primary = p.IsPrimary
                }),
                specs = result.Specs.Select(g => new
                {
                    name = g.Name,
                    icon = g.Icon,
                    rows = g.Rows.Select(r => new
                    {
                        label = r.Label,
                        value = r.Value,
                        value_type = r.ValueType
                    })
                }),
                profile = result.Profile is null
                    ? null
                    : new
                    {
                        id = result.Profile.Id,
                        brand = result.Profile.Brand,
                        article = result.Profile.Article,
                        title = result.Profile.Title
                    },
                source = result.Source,
                message = result.Message,
                note = "PHP ajax_epc_sku_media_public twin (CP sku_media, then UMAPI article cache)."
            });
        }

        endpoints.MapGet(EcomAeRoutes.StorefrontSkuMedia, SkuMediaLookup);
        endpoints.MapGet(EcomAeRoutes.StorefrontProductImage, SkuMediaLookup);

        // PHP part_search fitment: umapi analogs→article_links, else epartscross widget.
        endpoints.MapGet(EcomAeRoutes.StorefrontFitment, async (
            string? article,
            string? brand,
            string? brend,
            string? language,
            IStorefrontFitmentService fitment,
            CancellationToken cancellationToken) =>
        {
            var manufacturer = string.IsNullOrWhiteSpace(brand) ? brend : brand;
            var result = await fitment.LookupAsync(
                article ?? string.Empty,
                manufacturer ?? string.Empty,
                language,
                cancellationToken);
            return Results.Ok(new
            {
                ok = result.Ok,
                surface = "storefront",
                article = result.Article,
                brand = result.Brand,
                source = result.Source,
                message = result.Message,
                art_id = result.ArticleId,
                fallback_widget = result.FallbackWidget,
                PC = result.PC,
                CV = result.CV,
                Motorcycle = result.Motorcycle,
                note = "PHP umapi fitment twin (cache first). When empty, client loads /storefront/fitment-widget.js."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontFitmentWidgetJs, async (
            string? n,
            string? article,
            string? lang,
            string? language,
            IStorefrontFitmentService fitment,
            CancellationToken cancellationToken) =>
        {
            var art = string.IsNullOrWhiteSpace(n) ? article : n;
            var jsLang = string.IsNullOrWhiteSpace(lang) ? language : lang;
            var js = await fitment.GetWidgetJsAsync(art ?? string.Empty, jsLang, cancellationToken);
            return Results.Text(js, "application/javascript; charset=utf-8");
        });

        endpoints.MapMethods(EcomAeRoutes.StorefrontFitmentTable, new[] { "GET", "POST" }, async (
            HttpContext context,
            string? n,
            string? article,
            string? lang,
            string? language,
            string? cartype,
            IStorefrontFitmentService fitment,
            CancellationToken cancellationToken) =>
        {
            var art = string.IsNullOrWhiteSpace(n) ? article : n;
            var jsLang = string.IsNullOrWhiteSpace(lang) ? language : lang;
            var html = await fitment.GetTableHtmlAsync(
                art ?? string.Empty,
                jsLang,
                cartype,
                context.Request.Body,
                cancellationToken);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontCrossSearch, async (
            HttpContext context,
            string? article,
            string? brand,
            string? brend,
            int? limit,
            string? include_crossbase,
            ISurfaceDashboardSummaryReporter dashboards,
            IStorefrontPriceAccess priceAccess,
            CancellationToken cancellationToken) =>
        {
            var manufacturer = string.IsNullOrWhiteSpace(brand) ? brend : brand;
            var wantCrossbase = string.Equals(include_crossbase, "1", StringComparison.Ordinal)
                || string.Equals(include_crossbase, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(include_crossbase, "yes", StringComparison.OrdinalIgnoreCase);
            var result = await dashboards.BuildStorefrontCrossSearchAsync(
                article ?? string.Empty,
                manufacturer,
                limit ?? 600,
                cancellationToken,
                includeCrossbase: wantCrossbase);
            var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
            var references = result.References.Select(r => new
            {
                brand = r.Brand,
                article = r.Article,
                article_norm = EcomAE.Platform.Api.Catalog.PriceLookupRequest.NormalizeArticle(r.Article),
                source = string.IsNullOrWhiteSpace(r.Source) ? "cp" : r.Source,
                name = string.IsNullOrWhiteSpace(r.Brand) ? r.Article : $"{r.Brand} {r.Article}",
                in_stock = r.InStock
            }).ToList();
            // PHP ajax_epc_cross_search stock[] — guest redacts price/qty/warehouse.
            var stock = result.Stock.Select(r => new
            {
                brand = r.Brand,
                article = r.Article,
                article_norm = string.IsNullOrWhiteSpace(r.ArticleNorm)
                    ? EcomAE.Platform.Api.Catalog.PriceLookupRequest.NormalizeArticle(r.Article)
                    : r.ArticleNorm,
                name = string.IsNullOrWhiteSpace(r.Name)
                    ? (string.IsNullOrWhiteSpace(r.Brand) ? r.Article : $"{r.Brand} {r.Article}")
                    : r.Name,
                price = access.PricesVisible ? r.Price : 0m,
                currency = "",
                qty = access.PricesVisible ? (decimal?)r.Qty : null,
                exist = access.PricesVisible ? r.Qty : (r.Qty > 0 ? 1m : 0m),
                delivery = access.PricesVisible ? r.Delivery : "",
                warehouse = access.PricesVisible ? r.Warehouse : "**",
                storage_id = access.PricesVisible ? r.StorageId : 0,
                price_id = access.PricesVisible ? r.PriceId : 0,
                prices_visible = access.PricesVisible
            }).ToList();
            return Results.Ok(new
            {
                status = result.Source.Contains("aspnet-cross", StringComparison.Ordinal)
                    || result.Source is "database"
                    || result.References.Count > 0
                    || result.Stock.Count > 0,
                source = result.Source,
                article = result.Article,
                brand = result.Brand,
                local_count = result.LocalCount,
                crossbase_count = result.CrossbaseCount,
                reference_count = result.UniqueReferenceCount,
                references_loaded = references.Count,
                unique_reference_count = Math.Max(result.UniqueReferenceCount, result.LocalCount + result.CrossbaseCount),
                stock_count = stock.Count,
                references,
                stock,
                prices_visible = access.PricesVisible,
                message = result.Message,
                note = wantCrossbase
                    ? "ASP.NET local CP + crossbase.ru (cache/HTTP) — PHP ajax_epc_cross_search parity."
                    : "Fast ASP.NET CP cross network. Pass include_crossbase=1 for full crossbase merge."
            });
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontProductsOfBunch, async (
            HttpContext context,
            IStorefrontPriceAccess priceAccess,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            var article = form["article"].ToString();
            var brand = form["brand"].ToString();
            if (string.IsNullOrWhiteSpace(brand))
            {
                brand = form["brend"].ToString();
            }

            _ = int.TryParse(form["office_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var officeId);
            _ = int.TryParse(form["storage_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var storageId);
            _ = int.TryParse(form["geo_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var geoId);
            var queryJson = form["query"].ToString();
            if (string.IsNullOrWhiteSpace(article) && !string.IsNullOrWhiteSpace(queryJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(queryJson);
                    if (doc.RootElement.TryGetProperty("article", out var art))
                    {
                        article = art.GetString() ?? article;
                    }

                    if (string.IsNullOrWhiteSpace(brand)
                        && doc.RootElement.TryGetProperty("manufacturer", out var mfr))
                    {
                        brand = mfr.GetString() ?? brand;
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // keep form article/brand
                }
            }

            var access = await priceAccess.ResolveAsync(context, cancellationToken);
            var result = await dashboards.PollStorefrontProductsOfBunchAsync(
                article,
                brand,
                officeId,
                storageId,
                string.IsNullOrWhiteSpace(queryJson) ? null : queryJson,
                geoId,
                cancellationToken);
            // ASP.NET session gate wins over native path hardcoding prices_visible=true
            // (PHP twin: epc_storefront_prices_redact_products when not visible).
            var products = access.PricesVisible
                ? result.Products
                : priceAccess.RedactOffers(result.Products);

            return Results.Ok(new
            {
                ok = result.Result == 1,
                surface = "storefront",
                result = result.Result,
                office_id = result.OfficeId,
                storage_id = result.StorageId,
                products,
                count = products.Count,
                prices_visible = access.PricesVisible,
                show_purchase_cost = access.ShowPurchaseCost,
                access_state = access.StateToken,
                login_cta = access.LoginCtaPlain,
                source = result.Source,
                message = result.Message,
                note = "Progressive supplier poll with PHP price-visibility gate (guest/wholesale approval)."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontBulkUploadHistory, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for bulk-upload history.");
            }

            var result = await dashboards.ListStorefrontBulkUploadHistoryAsync(
                session.UserId,
                limit ?? 10,
                cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = result.UserId,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only epc_bulk_upload_history. Process/cross/cart writes remain PHP ajax_process."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontBulkUploadSample, () =>
        {
            var csv = StorefrontBulkUploadFileParser.SampleCsv();
            return Results.Text(csv, "text/csv; charset=utf-8", System.Text.Encoding.UTF8);
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontBulkUploadCheck, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontBulkUploadCheckService checker,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!CanBulkUpload(session))
            {
                return Unauthorized("Please log in first.");
            }

            if (!context.Request.HasFormContentType)
            {
                return Results.Json(new { status = false, message = "Upload file is required." }, statusCode: StatusCodes.Status400BadRequest);
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var file = form.Files["bulk_file"];
            if (file is null || file.Length <= 0)
            {
                return Results.Json(new { status = false, message = "Upload file is required." }, statusCode: StatusCodes.Status400BadRequest);
            }

            if (file.Length > StorefrontBulkUploadFileParser.MaxFileBytes)
            {
                return Results.Json(new { status = false, message = "File is larger than 8 MB. Split the list or save as CSV." }, statusCode: StatusCodes.Status400BadRequest);
            }

            await using var stream = file.OpenReadStream();
            var result = await checker.ProcessAsync(stream, file.FileName, form["priority"].ToString(), cancellationToken);
            return Results.Json(new
            {
                status = result.Status,
                message = result.Message,
                rows = result.Rows,
                summary = result.Summary,
                csv = result.Csv,
                upload_id = result.UploadId,
                source = result.Source
            }, statusCode: result.Status ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontBulkUploadCross, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontBulkUploadCheckService checker,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!CanBulkUpload(session))
            {
                return Unauthorized("Please log in first.");
            }

            string article;
            int qty = 1;
            var priority = "price";
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                article = form["article"].ToString();
                _ = int.TryParse(form["qty"].ToString(), out qty);
                priority = form["priority"].ToString();
            }
            else
            {
                article = context.Request.Query["article"].ToString();
                _ = int.TryParse(context.Request.Query["qty"].ToString(), out qty);
                priority = context.Request.Query["priority"].ToString();
            }

            var result = await checker.CrossAsync(article, qty, priority, cancellationToken);
            return Results.Json(new
            {
                status = result.Status,
                message = result.Message,
                exact = result.Exact,
                cross = result.Cross
            }, statusCode: result.Status ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontBulkUploadAddSelected, async (
            HttpContext context,
            StorefrontBulkUploadAddSelectedBody? body,
            ILegacySessionValidator validator,
            IStorefrontCartAddService cartAdd,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required to add parts to cart.");
            }

            body ??= new StorefrontBulkUploadAddSelectedBody(null, true);
            var items = body.Items ?? [];
            if (items.Count == 0)
            {
                return Results.Json(new { status = false, message = "Select at least one available item.", added = 0, failed = 0 }, statusCode: StatusCodes.Status400BadRequest);
            }

            var added = 0;
            var errors = new List<string>();
            foreach (var item in items)
            {
                var request = ToCartAddRequest(item, body.ConfirmWrites);
                if (request is null)
                {
                    errors.Add("Manufacturer and article are required.");
                    continue;
                }

                var written = await cartAdd.AddAsync(session.UserId, request, cancellationToken);
                if (written.Ok)
                {
                    added++;
                }
                else
                {
                    errors.Add(written.Message);
                }
            }

            var failed = items.Count - added;
            var ok = added > 0;
            var message = ok
                ? (failed == 0 ? "Items added to cart." : added + " added. Some items were not added.")
                : (errors.Count > 0 ? errors[0] : "Some items were not added. They may already be in cart.");
            return Results.Json(new
            {
                status = ok,
                message,
                added,
                failed,
                errors,
                session = SessionPayload(session)
            }, statusCode: ok ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.StorefrontCheckout, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for storefront checkout digest.");
            }

            var result = await dashboards.ListStorefrontCartAsync(session.UserId, limit ?? 50, cancellationToken);
            var checkedCount = result.Lines.Count(l => l.CheckedForOrder);
            var readiness = result.Summary.Count > 0
                ? (checkedCount > 0 ? "ready-for-php-how-get" : "cart-has-lines")
                : "empty-cart";
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                user_id = result.UserId,
                summary = result.Summary,
                checked_for_order = checkedCount,
                readiness,
                php_steps = new[]
                {
                    new { id = "how_get", href = "https://epartscart.com/shop/checkout/how_get" },
                    new { id = "login_offer", href = "https://epartscart.com/shop/checkout/login_offer" },
                    new { id = "confirm", href = "https://epartscart.com/shop/checkout/confirm" },
                },
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Wave B read-only checkout readiness over shop_carts. Obtain/confirm/payment writes remain PHP."
            });
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontCartChangeCountNeed, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontCartChangeCountNeedDryRun dryRun,
            IStorefrontCartWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateCustomerAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/cart-app", "Please log in or register to continue.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontCartChangeCountNeedBody>(context, cancellationToken) ?? new();
            var id = (long)body.Id;
            var qty = body.CountNeed;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id");
                qty = LiveWriteFormBinder.Dec(form, "countNeed", "count_need");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.ChangeCountNeedAsync(session.UserId, id, qty, cancellationToken);
                var status = written.Code is "auth" or "unauthorized"
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status400BadRequest;
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/cart-app",
                    written.Ok,
                    written.Message,
                    written.ToPayload(SessionPayload(session)),
                    status);
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCartChangeCountNeedRequest(id > int.MaxValue ? 0 : (int)id, qty, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontCartCheckForOrder, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontCartCheckForOrderDryRun dryRun,
            IStorefrontCartWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateCustomerAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/cart-app", "Please log in or register to continue.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontCartCheckForOrderBody>(context, cancellationToken) ?? new();
            var records = body.Records ?? [];
            var id = body.Id;
            var checkedForOrder = body.CheckedForOrder ?? body.Checked;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                id = LiveWriteFormBinder.Long(form, "id");
                checkedForOrder = LiveWriteFormBinder.Int(form, "checked", "checkedForOrder", "checked_for_order");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                var rawRecords = LiveWriteFormBinder.Text(form, "records", "recordsToDel");
                if (rawRecords.Length > 0 && long.TryParse(rawRecords, NumberStyles.Integer, CultureInfo.InvariantCulture, out var one))
                {
                    records = [one];
                }
            }

            if (confirm)
            {
                StorefrontCartWriteResult written;
                if (id > 0)
                {
                    written = await writes.CheckForOrderAsync(session.UserId, id, checkedForOrder != 0, cancellationToken);
                }
                else
                {
                    written = new(false, "error", "invalid", "Cart line is required.", 0);
                    foreach (var cartId in records.Where(x => x > 0))
                    {
                        written = await writes.CheckForOrderAsync(session.UserId, cartId, checkedForOrder != 0, cancellationToken);
                        if (!written.Ok)
                        {
                            break;
                        }
                    }
                }

                var status = written.Code is "auth" or "unauthorized"
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status400BadRequest;
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/cart-app",
                    written.Ok,
                    written.Message,
                    written.ToPayload(SessionPayload(session)),
                    status);
            }

            var dryIds = id > 0 ? new[] { id } : (records ?? []).ToArray();
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCartCheckForOrderRequest(dryIds, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontCartDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontCartDeleteDryRun dryRun,
            IStorefrontCartWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateCustomerAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/cart-app", "Please log in or register to continue.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontCartDeleteBody>(context, cancellationToken) ?? new();
            var ids = (body.RecordsToDel ?? []).ToList();
            var confirm = body.ConfirmWrites;
            if (body.Id > 0)
            {
                ids.Add(body.Id);
            }

            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                var formId = LiveWriteFormBinder.Long(form, "id", "recordsToDel", "records_to_del");
                if (formId > 0)
                {
                    ids.Add(formId);
                }

                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.DeleteAsync(session.UserId, ids, cancellationToken);
                var status = written.Code is "auth" or "unauthorized"
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status400BadRequest;
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/cart-app",
                    written.Ok,
                    written.Message,
                    written.ToPayload(SessionPayload(session)),
                    status);
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCartDeleteRequest(ids, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontCartAdd, async (
            HttpContext context,
            StorefrontCartAddBody? body,
            ILegacySessionValidator validator,
            IStorefrontCartAddDryRun dryRun,
            IStorefrontCartAddService cartAdd,
            CancellationToken cancellationToken) =>
        {
            // PHP DP_User::getUserId — customer cookies even if admin cookies also present.
            var session = await validator.ValidateCustomerAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Please log in or register to continue.");
            }

            body ??= new StorefrontCartAddBody(2, null, null, 0, 0, 0, 0, false);
            var request = new StorefrontCartAddRequest(
                body.ProductType,
                body.Manufacturer,
                body.Article,
                body.CountNeed,
                body.Price,
                body.MinOrder,
                body.Exist,
                body.ConfirmWrites,
                body.ArticleShow,
                body.Name,
                body.TimeToExe,
                body.TimeToExeGuaranteed,
                body.Storage,
                body.Probability,
                body.PricePurchase,
                body.Markup,
                body.OfficeId,
                body.StorageId,
                body.JsonParams,
                body.CheckHash);

            // Live write path (PHP ajax_add_to_basket type-2). Dry-run only when confirmWrites=false.
            if (body.ConfirmWrites)
            {
                var written = await cartAdd.AddAsync(session.UserId, request, cancellationToken);
                if (!written.Ok)
                {
                    var status = written.Code is "auth" or "unauthorized"
                        ? StatusCodes.Status401Unauthorized
                        : StatusCodes.Status400BadRequest;
                    return Results.Json(written.ToPayload(SessionPayload(session)), statusCode: status);
                }

                return Results.Ok(written.ToPayload(SessionPayload(session)));
            }

            var result = await dryRun.EvaluateAsync(session.UserId, request, cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontGarageNotepadAdd, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontGarageNotepadAddDryRun dryRun,
            IStorefrontGarageWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/garage-app", "Customer session required for garage notepad-add.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontGarageNotepadAddBody>(context, cancellationToken)
                       ?? new(0, null, null, null, 0, 0, false);
            var garageId = body.GarageId;
            var manufacturer = body.Manufacturer;
            var article = body.Article;
            var name = body.Name;
            var exist = body.Exist;
            var price = body.Price;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                garageId = LiveWriteFormBinder.Long(form, "garageId", "garage_id", "garage");
                manufacturer = LiveWriteFormBinder.Text(form, "manufacturer", "brend", "brand");
                article = LiveWriteFormBinder.Text(form, "article");
                name = LiveWriteFormBinder.Text(form, "name");
                exist = LiveWriteFormBinder.Int(form, "exist");
                price = LiveWriteFormBinder.Dec(form, "price");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.AddNotepadAsync(session.UserId, garageId, manufacturer, article, name, exist, price, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/garage-app" + (garageId > 0 ? "?garage=" + garageId.ToString(CultureInfo.InvariantCulture) : ""),
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontGarageNotepadAddRequest(garageId, manufacturer, article, name, exist, price, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontGarageSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontGarageWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/garage-app", "Customer session required for garage save.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontGarageSaveBody>(context, cancellationToken)
                       ?? new(0, null, null, null, 0, null, null, false);
            var carId = body.CarId;
            var caption = body.Caption;
            var make = body.Make;
            var model = body.Model;
            var year = body.Year;
            var vin = body.Vin;
            var frame = body.Frame;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                carId = LiveWriteFormBinder.Long(form, "carId", "car_id");
                caption = LiveWriteFormBinder.Text(form, "caption");
                make = LiveWriteFormBinder.Text(form, "make", "marka");
                model = LiveWriteFormBinder.Text(form, "model");
                year = LiveWriteFormBinder.Int(form, "year");
                vin = LiveWriteFormBinder.Text(form, "vin");
                frame = LiveWriteFormBinder.Text(form, "frame");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes")
                          || LiveWriteFormBinder.Flag(form, "save_action");
            }

            if (!confirm)
            {
                return Results.Json(new { ok = false, validation_code = "confirm_required", message = "Set confirmWrites=true to save the vehicle on ASP.NET." }, statusCode: StatusCodes.Status400BadRequest);
            }

            var written = await writes.SaveVehicleAsync(
                session.UserId,
                new StorefrontGarageSaveRequest(carId, caption, make, model, year, vin, frame),
                cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/storefront/garage-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteSubmit, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontQuoteSubmitDryRun dryRun,
            IStorefrontQuoteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/quotes-app", "Customer session required for quote submit.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontQuoteSubmitBody>(context, cancellationToken) ?? new(0, null, false);
            var quoteId = body.QuoteId;
            var note = body.CustomerNote;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                quoteId = LiveWriteFormBinder.Long(form, "quoteId", "quote_id");
                note = LiveWriteFormBinder.Text(form, "customerNote", "customer_note");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SubmitAsync(session.UserId, quoteId, note, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/quotes-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteSubmitRequest(quoteId, note, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteAccept, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontQuoteAcceptDryRun dryRun,
            IStorefrontQuoteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/quotes-app", "Customer session required for quote accept.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontQuoteAcceptBody>(context, cancellationToken) ?? new(0, false);
            var quoteId = body.QuoteId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                quoteId = LiveWriteFormBinder.Long(form, "quoteId", "quote_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.AcceptAsync(session.UserId, quoteId, cancellationToken);
                var dest = written.Succeeded
                    ? "/storefront/cart-app"
                    : "/storefront/quotes-app?id=" + quoteId.ToString(CultureInfo.InvariantCulture);
                return LiveWriteFormBinder.Complete(
                    context,
                    dest,
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteAcceptRequest(quoteId, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteAddItem, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontQuoteAddItemDryRun dryRun,
            IStorefrontQuoteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/quotes-app", "Customer session required for quote add-item.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontQuoteAddItemBody>(context, cancellationToken)
                       ?? new(2, null, null, 1, false);
            var productType = body.ProductType;
            var manufacturer = body.Manufacturer;
            var article = body.Article;
            var countNeed = body.CountNeed;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                productType = LiveWriteFormBinder.Int(form, "productType", "product_type");
                if (productType <= 0)
                {
                    productType = 2;
                }

                manufacturer = LiveWriteFormBinder.Text(form, "manufacturer", "brand");
                article = LiveWriteFormBinder.Text(form, "article");
                countNeed = LiveWriteFormBinder.Int(form, "countNeed", "count_need");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.AddItemAsync(
                    session.UserId,
                    new StorefrontQuoteAddItemWriteRequest(productType, manufacturer, article, countNeed < 1 ? 1 : countNeed),
                    cancellationToken);
                var dest = written.Succeeded && written.Id > 0
                    ? "/storefront/quotes-app?id=" + written.Id.ToString(CultureInfo.InvariantCulture)
                    : "/storefront/quotes-app";
                return LiveWriteFormBinder.Complete(
                    context,
                    dest,
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, quote_id = written.Id, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteAddItemRequest(productType, manufacturer, article, countNeed < 1 ? 1 : countNeed, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteAddManual, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontQuoteAddManualDryRun dryRun,
            IStorefrontQuoteWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/quotes-app", "Customer session required for quote add-manual.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontQuoteAddManualBody>(context, cancellationToken)
                       ?? new(null, null, 1, false);
            var manufacturer = body.Manufacturer;
            var article = body.Article;
            var countNeed = body.CountNeed;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                manufacturer = LiveWriteFormBinder.Text(form, "manufacturer", "brand");
                article = LiveWriteFormBinder.Text(form, "article");
                countNeed = LiveWriteFormBinder.Int(form, "countNeed", "count_need");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.AddManualAsync(session.UserId, manufacturer, article, countNeed, cancellationToken);
                var dest = written.Succeeded && written.Id > 0
                    ? "/storefront/quotes-app?id=" + written.Id.ToString(CultureInfo.InvariantCulture)
                    : "/storefront/quotes-app";
                return LiveWriteFormBinder.Complete(
                    context,
                    dest,
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, quote_id = written.Id, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteAddManualRequest(manufacturer, article, countNeed < 1 ? 1 : countNeed, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontGarageSetActive, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontGarageSetActiveDryRun dryRun,
            IStorefrontGarageWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/garage-app", "Customer session required for garage set-active.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontGarageSetActiveBody>(context, cancellationToken) ?? new(0, false);
            var carId = body.CarId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                carId = LiveWriteFormBinder.Long(form, "carId", "car_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetActiveAsync(session.UserId, carId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/garage-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontGarageSetActiveRequest(carId, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontGarageDelete, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontGarageDeleteDryRun dryRun,
            IStorefrontGarageWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/garage-app", "Customer session required for garage delete.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontGarageDeleteBody>(context, cancellationToken) ?? new(0, false);
            var carId = body.CarId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                carId = LiveWriteFormBinder.Long(form, "carId", "car_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.DeleteAsync(session.UserId, carId, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/garage-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontGarageDeleteRequest(carId, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontGarageCheckCar, async (
            HttpContext context,
            StorefrontGarageCheckCarBody? body,
            ILegacySessionValidator validator,
            IStorefrontGarageCheckCarDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for garage check-car dry-run.");
            }

            body ??= new StorefrontGarageCheckCarBody(0, 0, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontGarageCheckCarRequest(body.CarId, body.OrderId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontCheckoutCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontCheckoutCreateDryRun dryRun,
            IStorefrontCheckoutWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/checkout-app", "Customer session required for checkout create.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontCheckoutCreateBody>(context, cancellationToken) ?? new(0);
            var howGet = body.HowGetMode;
            var officeId = body.OfficeId ?? 0;
            var confirm = body.ConfirmWrites;
            var orderMessage = body.OrderMessage;
            var buyerPo = body.BuyerPoNumber;
            var agreement = body.UsersAgreement;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                howGet = LiveWriteFormBinder.Int(form, "howGetMode", "how_get_mode", "how_get");
                officeId = LiveWriteFormBinder.Int(form, "officeId", "office_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                agreement = LiveWriteFormBinder.Flag(form, "usersAgreement", "users_agreement");
                orderMessage = LiveWriteFormBinder.Text(form, "orderMessage", "order_message");
                buyerPo = LiveWriteFormBinder.Text(form, "buyerPoNumber", "buyer_po_number");
            }

            if (confirm)
            {
                var written = await writes.CreateAsync(
                    session.UserId,
                    new StorefrontCheckoutWriteRequest(howGet, officeId, agreement, orderMessage, buyerPo),
                    cancellationToken);
                var dest = written.Ok && written.OrderId > 0
                    ? "/storefront/orders-app?order_id=" + written.OrderId.ToString(CultureInfo.InvariantCulture)
                    : "/storefront/checkout-app?step=confirm";
                return LiveWriteFormBinder.Complete(
                    context,
                    dest,
                    written.Ok,
                    written.Message,
                    written.ToPayload(SessionPayload(session)));
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCheckoutCreateRequest(howGet, officeId, null, null, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontPaymentCreateOperation, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontPaymentWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/payment-app", "Customer session required to create a payment operation.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontPaymentCreateBody>(context, cancellationToken) ?? new(0, 0, null, false);
            var amount = body.Amount;
            var orderId = body.OrderId;
            var handler = body.PayHandler;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                amount = LiveWriteFormBinder.Dec(form, "amount");
                orderId = LiveWriteFormBinder.Long(form, "order_id", "orderId");
                handler = LiveWriteFormBinder.Text(form, "pay_handler", "payHandler", "pay_system");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = true,
                    surface = "storefront",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    validation_code = amount > 0 ? "ok" : "invalid",
                    message = amount > 0 ? "Payment operation validated; write blocked until confirmWrites=true." : "Forbidden",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CreateOperationAsync(session.UserId, amount, orderId, handler, cancellationToken);
            var dest = written.Ok
                ? EcomAeRoutes.StorefrontPaymentGoToPay + "?operation=" + written.Id.ToString(CultureInfo.InvariantCulture)
                  + "&pay_system=" + Uri.EscapeDataString(written.PaySystem ?? "epc_demo")
                : "/storefront/payment-app";
            return LiveWriteFormBinder.Complete(context, dest, written.Ok, written.Message, written.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.StorefrontPaymentGoToPay, async (
            HttpContext context,
            ILegacySessionValidator validator,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/payment-app", "Customer session required for go-to-pay.");
            }

            var operation = context.Request.Query["operation"].ToString();
            var handler = StorefrontPaymentWriteService.SanitizeHandler(context.Request.Query["pay_system"].ToString());
            if (handler.Length == 0)
            {
                handler = "epc_demo";
            }

            var html = $"""
                <!DOCTYPE html><html><head><meta charset="utf-8"><title>Pay</title></head>
                <body style="font-family:sans-serif;padding:2rem">
                <h1>Confirm payment</h1>
                <p>Demo gateway {System.Net.WebUtility.HtmlEncode(handler)} — operation {System.Net.WebUtility.HtmlEncode(operation)}.</p>
                <form method="post" action="{EcomAeRoutes.StorefrontPaymentNotify}">
                  <input type="hidden" name="confirmWrites" value="true" />
                  <input type="hidden" name="operation_id" value="{System.Net.WebUtility.HtmlEncode(operation)}" />
                  <input type="hidden" name="demo_token" value="{StorefrontPaymentWriteService.DemoToken}" />
                  <input type="hidden" name="handler" value="{System.Net.WebUtility.HtmlEncode(handler)}" />
                  <input type="hidden" name="returnUrl" value="/storefront/orders-app" />
                  <button type="submit">Pay now</button>
                </form>
                </body></html>
                """;
            return Results.Content(html, "text/html; charset=utf-8");
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontPaymentNotify, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontPaymentWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontPaymentNotifyBody>(context, cancellationToken) ?? new(0, 0, null, null, false);
            var operationId = body.OperationId;
            var sum = body.Sum;
            var token = body.DemoToken;
            var handler = body.Handler;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                operationId = LiveWriteFormBinder.Long(form, "operation_id", "operationId");
                sum = LiveWriteFormBinder.Dec(form, "sum", "amount");
                token = LiveWriteFormBinder.Text(form, "demo_token", "demoToken");
                handler = LiveWriteFormBinder.Text(form, "handler", "pay_system");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = true,
                    surface = "storefront",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    validation_code = "ok",
                    message = "Notify validated; write blocked until confirmWrites=true.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.NotifyAsync(session.UserId, operationId, sum, token, handler, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/storefront/orders-app",
                written.Ok,
                written.Message,
                written.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontVinDecode, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ILaximoVinDecodeService decode,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontVinDecodeBody>(context, cancellationToken) ?? new(null);
            var vin = body.Vin;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                vin = LiveWriteFormBinder.Text(form, "vin", "identString", "ident_string");
            }

            var decoded = await decode.DecodeAsync(vin, cancellationToken);
            var dest = "/storefront/vin-app?vin=" + Uri.EscapeDataString(decoded.Vin);
            if (decoded.Ok)
            {
                dest += "&make=" + Uri.EscapeDataString(decoded.Manufacturer ?? "")
                    + "&model=" + Uri.EscapeDataString(decoded.ModelLabel ?? "");
            }

            return LiveWriteFormBinder.Complete(
                context,
                dest,
                decoded.Ok,
                decoded.Message,
                decoded.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontVinRequestCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontVinRequestWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/seller-request-app", "Customer session required to send a VIN request.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontVinRequestCreateBody>(context, cancellationToken)
                       ?? new(null, null, null, null, null, false);
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["client_fio"] = body.ClientFio ?? "",
                ["client_email"] = body.ClientEmail ?? "",
                ["client_phone"] = body.ClientPhone ?? "",
                ["client_vin"] = body.ClientVin ?? "",
                ["client_mark"] = body.ClientMark ?? "",
                ["client_model"] = body.ClientModel ?? "",
                ["client_year"] = body.ClientYear ?? "",
                ["client_engine"] = body.ClientEngine ?? "",
                ["client_body"] = body.ClientBody ?? "",
                ["client_kpp"] = body.ClientKpp ?? "",
                ["client_city"] = body.ClientCity ?? "",
                ["client_drive"] = body.ClientDrive ?? "",
            };
            var parts = body.ClientParts;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                foreach (var field in PhpSellerRequest.Fields)
                {
                    fields[field.Name] = LiveWriteFormBinder.Text(form, field.Name);
                }

                parts = LiveWriteFormBinder.Text(form, "client_parts", "parts");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = true,
                    surface = "storefront",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    validation_code = "ok",
                    message = "VIN request validated; write blocked until confirmWrites=true.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.CreateAsync(session.UserId, fields, parts, cancellationToken);
            var dest = written.Ok
                ? "/storefront/customer-requests-app?id=" + written.Id.ToString(CultureInfo.InvariantCulture)
                : "/storefront/seller-request-app";
            return LiveWriteFormBinder.Complete(context, dest, written.Ok, written.Message, written.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontVinRequestSendMessage, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontVinRequestWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/customer-requests-app", "Customer session required to message a VIN request.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontVinRequestMessageBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var vinId = body.VinId;
            var text = body.Text;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                vinId = LiveWriteFormBinder.Long(form, "vin_id", "vinId", "id");
                text = LiveWriteFormBinder.Text(form, "text");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = true,
                    surface = "storefront",
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    validation_code = "ok",
                    message = "VIN request message validated; write blocked until confirmWrites=true.",
                    session = SessionPayload(session)
                });
            }

            var written = await writes.SendMessageAsync(session.UserId, vinId, text, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/storefront/customer-requests-app?id=" + vinId.ToString(CultureInfo.InvariantCulture),
                written.Ok,
                written.Message,
                written.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontNewsletterSubscribe, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontNewsletterSubscribeDryRun dryRun,
            IStorefrontCustomerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontNewsletterSubscribeBody>(context, cancellationToken)
                       ?? new(null, false);
            var email = body.Email;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                email = LiveWriteFormBinder.Text(form, "email");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SubscribeNewsletterAsync(email, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/newsletter-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new StorefrontNewsletterSubscribeRequest(email, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontAddEvaluation, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontAddEvaluationDryRun dryRun,
            IStorefrontCustomerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login", "Customer session required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontAddEvaluationBody>(context, cancellationToken)
                       ?? new(0, 0, false, null);
            var productId = body.ProductId;
            var rating = body.Rating;
            var text = body.Text;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                productId = LiveWriteFormBinder.Long(form, "productId", "product_id");
                rating = LiveWriteFormBinder.Int(form, "rating", "mark");
                text = LiveWriteFormBinder.Text(form, "text");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.AddEvaluationAsync(session.UserId, productId, rating <= 0 ? 5 : rating, text, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/product-app?id=" + productId.ToString(CultureInfo.InvariantCulture),
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new StorefrontAddEvaluationRequest(productId, rating, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontCreateOperation, async (
            HttpContext context,
            StorefrontCreateOperationBody? body,
            ILegacySessionValidator validator,
            IStorefrontCreateOperationDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
                return Unauthorized("Customer session required.");
            body ??= new StorefrontCreateOperationBody(0,null,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontCreateOperationRequest(body.Amount, body.Kind, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.StorefrontCheckOrderNotAuthorized, async (
            HttpContext context,
            StorefrontCheckOrderNotAuthorizedBody? body,
            ILegacySessionValidator validator,
            IStorefrontCheckOrderNotAuthorizedDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
                return Unauthorized("Customer session required.");
            body ??= new StorefrontCheckOrderNotAuthorizedBody(0,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontCheckOrderNotAuthorizedRequest(body.OrderId, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.StorefrontSetUserOption, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontSetUserOptionDryRun dryRun,
            IStorefrontCustomerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login", "Customer session required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontSetUserOptionBody>(context, cancellationToken)
                       ?? new(null, null, false);
            var key = body.OptionKey;
            var value = body.OptionValue;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                key = LiveWriteFormBinder.Text(form, "optionKey", "key");
                value = LiveWriteFormBinder.Text(form, "optionValue", "value");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SetUserOptionAsync(session.UserId, key, value, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/profile-app",
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new StorefrontSetUserOptionRequest(key, value, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontSetMyCity, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontSetMyCityDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontSetMyCityBody>(context, cancellationToken)
                       ?? new(0, false);
            var cityId = body.CityId;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                cityId = LiveWriteFormBinder.Long(form, "cityId", "city_id", "geo_id");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                if (cityId <= 0)
                {
                    return LiveWriteFormBinder.Complete(
                        context,
                        "/",
                        false,
                        "City is required.",
                        new { ok = false, validation_code = "invalid", message = "City is required.", session = SessionPayload(session) });
                }

                context.Response.Cookies.Append(
                    "my_city",
                    cityId.ToString(CultureInfo.InvariantCulture),
                    new CookieOptions
                    {
                        Path = "/",
                        Expires = DateTimeOffset.UtcNow.AddYears(10),
                        IsEssential = true,
                    });
                return LiveWriteFormBinder.Complete(
                    context,
                    "/",
                    true,
                    "City saved.",
                    new { ok = true, writes = 1, phpAuthoritative = false, validation_code = "ok", message = "City saved.", city_id = cityId, session = SessionPayload(session) });
            }

            return Results.Ok(dryRun.Evaluate(new StorefrontSetMyCityRequest(cityId, false)).ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontWishlistAdd, (HttpContext context, ILegacySessionValidator validator, CancellationToken cancellationToken)
            => MapWishlistCookieAsync(context, validator, add: true, cancellationToken)).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontWishlistRemove, (HttpContext context, ILegacySessionValidator validator, CancellationToken cancellationToken)
            => MapWishlistCookieAsync(context, validator, add: false, cancellationToken)).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontCompareAdd, (HttpContext context, ILegacySessionValidator validator, CancellationToken cancellationToken)
            => MapCompareCookieAsync(context, validator, add: true, cancellationToken)).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontCompareRemove, (HttpContext context, ILegacySessionValidator validator, CancellationToken cancellationToken)
            => MapCompareCookieAsync(context, validator, add: false, cancellationToken)).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontProfileSave, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontCustomerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/profile-app", "Customer session required.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontProfileSaveBody>(context, cancellationToken)
                       ?? new(null, false);
            var fields = body.Fields is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(body.Fields, StringComparer.OrdinalIgnoreCase);
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
                foreach (var key in StorefrontCustomerWriteService.AllowedProfileFieldNames)
                {
                    var value = LiveWriteFormBinder.Text(form, key);
                    if (value.Length > 0)
                    {
                        fields[key] = value;
                    }
                }
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = false,
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    message = "Set confirmWrites=true to save the profile on ASP.NET.",
                    session = SessionPayload(session),
                });
            }

            var written = await writes.SaveProfileAsync(session.UserId, fields, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/storefront/profile-app",
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();
        endpoints.MapPost(EcomAeRoutes.StorefrontLoginSendCode, async (
            HttpContext context,
            StorefrontLoginSendCodeBody? body,
            ILegacySessionValidator validator,
            IStorefrontLoginSendCodeDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            // Public/customer dry-run gate; PHP remains authoritative.
            body ??= new StorefrontLoginSendCodeBody(null,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontLoginSendCodeRequest(body.Phone, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.StorefrontLoginCheckCode, async (
            HttpContext context,
            StorefrontLoginCheckCodeBody? body,
            ILegacySessionValidator validator,
            IStorefrontLoginCheckCodeDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            // Public/customer dry-run gate; PHP remains authoritative.
            body ??= new StorefrontLoginCheckCodeBody(null,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontLoginCheckCodeRequest(body.Code, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontBulkUploadProcess, async (HttpContext context, StorefrontBulkUploadProcessBody? body, ILegacySessionValidator validator, IStorefrontBulkUploadProcessDryRun dryRun, CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0) return Unauthorized("Customer session required.");
            body ??= new StorefrontBulkUploadProcessBody(null, false);
            return Results.Ok(dryRun.Evaluate(new StorefrontBulkUploadProcessRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontGetArticleList, async (HttpContext context, StorefrontGetArticleListBody? body, ILegacySessionValidator validator, IStorefrontGetArticleListDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); body ??= new StorefrontGetArticleListBody(null,false); return Results.Ok(dryRun.Evaluate(new StorefrontGetArticleListRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });
        endpoints.MapPost(EcomAeRoutes.StorefrontLoadReturnsData, async (HttpContext context, StorefrontLoadReturnsDataBody? body, ILegacySessionValidator validator, IStorefrontLoadReturnsDataDryRun dryRun, CancellationToken cancellationToken) =>
        { var session = await validator.ValidateAsync(context, cancellationToken); body ??= new StorefrontLoadReturnsDataBody(null,false); return Results.Ok(dryRun.Evaluate(new StorefrontLoadReturnsDataRequest(body.Action, body.ConfirmWrites)).ToPayload(SessionPayload(session))); });

        endpoints.MapPost(EcomAeRoutes.StorefrontOrderSendMessage, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontOrderSendMessageDryRun dryRun,
            IStorefrontCustomerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/orders-app", "Customer session required for order send-message.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontOrderSendMessageBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var orderId = body.OrderId;
            var text = body.Text;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                text = LiveWriteFormBinder.Text(form, "text", "message");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (confirm)
            {
                var written = await writes.SendOrderMessageAsync(session.UserId, orderId, text, cancellationToken);
                return LiveWriteFormBinder.Complete(
                    context,
                    "/storefront/orders-app?order_id=" + orderId.ToString(CultureInfo.InvariantCulture),
                    written.Succeeded,
                    written.Message,
                    new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
            }

            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontOrderSendMessageRequest(orderId, text, false),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontReturnsSendMessage, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontCustomerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/returns-app", "Customer session required for return send-message.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontReturnSendMessageBody>(context, cancellationToken)
                       ?? new(0, null, false);
            var returnId = body.ReturnId;
            var text = body.Text;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                returnId = LiveWriteFormBinder.Long(form, "returnId", "return_id");
                text = LiveWriteFormBinder.Text(form, "text", "message");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = false,
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    message = "Set confirmWrites=true to send the return message on ASP.NET.",
                    session = SessionPayload(session),
                });
            }

            var written = await writes.SendReturnMessageAsync(session.UserId, returnId, text, cancellationToken);
            return LiveWriteFormBinder.Complete(
                context,
                "/storefront/returns-app?return_id=" + returnId.ToString(CultureInfo.InvariantCulture),
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, session = SessionPayload(session) });
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.StorefrontReturnsCreate, async (
            HttpContext context,
            ILegacySessionValidator validator,
            IStorefrontCustomerWriteService writes,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/storefront/login?returnUrl=/storefront/returns-app", "Customer session required for create return.");
            }

            var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontReturnCreateBody>(context, cancellationToken)
                       ?? new(0, 0, 0, 0, null, false);
            var orderId = body.OrderId;
            var itemId = body.ItemId;
            var reasonId = body.ReasonId;
            var count = body.Count;
            var comment = body.Comment;
            var confirm = body.ConfirmWrites;
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken);
                orderId = LiveWriteFormBinder.Long(form, "orderId", "order_id");
                itemId = LiveWriteFormBinder.Long(form, "itemId", "item_id");
                reasonId = LiveWriteFormBinder.Int(form, "reasonId", "reason_id", "reason");
                count = LiveWriteFormBinder.Int(form, "count", "countNeed", "count_need");
                comment = LiveWriteFormBinder.Text(form, "comment", "text");
                confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
            }

            if (!confirm)
            {
                return Results.Ok(new
                {
                    ok = false,
                    writes = 0,
                    writesBlocked = true,
                    phpAuthoritative = false,
                    message = "Set confirmWrites=true to create the return on ASP.NET.",
                    session = SessionPayload(session),
                });
            }

            var written = await writes.CreateReturnAsync(session.UserId, orderId, itemId, reasonId, count, comment, cancellationToken);
            var dest = written.Succeeded && written.Id > 0
                ? "/storefront/returns-app?return_id=" + written.Id.ToString(CultureInfo.InvariantCulture)
                : "/storefront/returns-app?order_id=" + orderId.ToString(CultureInfo.InvariantCulture);
            return LiveWriteFormBinder.Complete(
                context,
                dest,
                written.Succeeded,
                written.Message,
                new { ok = written.Succeeded, writes = written.Writes, phpAuthoritative = false, validation_code = written.Code, message = written.Message, return_id = written.Id, session = SessionPayload(session) });
        }).DisableAntiforgery();
    }

    private sealed record StorefrontCartChangeCountNeedBody(int Id = 0, decimal CountNeed = 0, bool ConfirmWrites = false);
    private sealed record StorefrontCartCheckForOrderBody(
        IReadOnlyList<long>? Records = null,
        long Id = 0,
        int Checked = 1,
        int? CheckedForOrder = null,
        bool ConfirmWrites = false);
    private sealed record StorefrontCartDeleteBody(IReadOnlyList<long>? RecordsToDel = null, long Id = 0, bool ConfirmWrites = false);
    private sealed record StorefrontCartAddBody(
        int ProductType,
        string? Manufacturer,
        string? Article,
        decimal CountNeed,
        decimal Price,
        decimal MinOrder = 0,
        decimal Exist = 0,
        bool ConfirmWrites = false,
        string? ArticleShow = null,
        string? Name = null,
        string? TimeToExe = null,
        string? TimeToExeGuaranteed = null,
        string? Storage = null,
        int Probability = 100,
        decimal PricePurchase = 0,
        int Markup = 0,
        int OfficeId = 0,
        int StorageId = 0,
        string? JsonParams = null,
        string? CheckHash = null);
    private sealed record StorefrontGarageSaveBody(
        long CarId = 0,
        string? Caption = null,
        string? Make = null,
        string? Model = null,
        int Year = 0,
        string? Vin = null,
        string? Frame = null,
        bool ConfirmWrites = false);
    private sealed record StorefrontGarageNotepadAddBody(
        long GarageId,
        string? Manufacturer,
        string? Article,
        string? Name = null,
        int Exist = 0,
        decimal Price = 0,
        bool ConfirmWrites = false);
    private sealed record StorefrontQuoteSubmitBody(long QuoteId, string? CustomerNote = null, bool ConfirmWrites = false);
    private sealed record StorefrontQuoteAcceptBody(long QuoteId, bool ConfirmWrites = false);
    private sealed record StorefrontQuoteAddItemBody(
        int ProductType,
        string? Manufacturer,
        string? Article,
        int CountNeed = 1,
        bool ConfirmWrites = false);
    private sealed record StorefrontQuoteAddManualBody(
        string? Manufacturer,
        string? Article,
        int CountNeed = 1,
        bool ConfirmWrites = false);
    private sealed record StorefrontGarageSetActiveBody(long CarId, bool ConfirmWrites = false);
    private sealed record StorefrontGarageDeleteBody(long CarId, bool ConfirmWrites = false);
    private sealed record StorefrontGarageCheckCarBody(long CarId, long OrderId, bool ConfirmWrites = false);
    private sealed record StorefrontCheckoutCreateBody(
        int HowGetMode,
        int? OfficeId = null,
        string? PhoneNotAuth = null,
        string? EmailNotAuth = null,
        bool ConfirmWrites = false,
        bool UsersAgreement = false,
        string? OrderMessage = null,
        string? BuyerPoNumber = null);
    private sealed record StorefrontPaymentCreateBody(
        decimal Amount,
        long OrderId = 0,
        string? PayHandler = null,
        bool ConfirmWrites = false);
    private sealed record StorefrontPaymentNotifyBody(
        long OperationId,
        decimal Sum = 0,
        string? DemoToken = null,
        string? Handler = null,
        bool ConfirmWrites = false);
    private sealed record StorefrontVinDecodeBody(string? Vin);
    private sealed record StorefrontVinRequestCreateBody(
        string? ClientFio,
        string? ClientEmail,
        string? ClientPhone,
        string? ClientVin,
        string? ClientParts,
        bool ConfirmWrites = false,
        string? ClientMark = null,
        string? ClientModel = null,
        string? ClientYear = null,
        string? ClientEngine = null,
        string? ClientBody = null,
        string? ClientKpp = null,
        string? ClientCity = null,
        string? ClientDrive = null);
    private sealed record StorefrontVinRequestMessageBody(long VinId, string? Text, bool ConfirmWrites = false);
    private sealed record StorefrontOrderSendMessageBody(long OrderId, string? Text, bool ConfirmWrites = false);
    private sealed record StorefrontReturnSendMessageBody(long ReturnId, string? Text, bool ConfirmWrites = false);
    private sealed record StorefrontReturnCreateBody(
        long OrderId,
        long ItemId,
        int ReasonId,
        int Count,
        string? Comment = null,
        bool ConfirmWrites = false);
    private sealed record StorefrontGetArticleListBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record StorefrontLoadReturnsDataBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record StorefrontBulkUploadProcessBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record StorefrontBulkUploadAddSelectedBody(
        IReadOnlyList<StorefrontBulkUploadCartItem>? Items,
        bool ConfirmWrites = true);
    private sealed record StorefrontBulkUploadCartItem(
        [property: JsonPropertyName("product_type")] int ProductType = 2,
        [property: JsonPropertyName("manufacturer")] string? Manufacturer = null,
        [property: JsonPropertyName("article")] string? Article = null,
        [property: JsonPropertyName("article_show")] string? ArticleShow = null,
        [property: JsonPropertyName("name")] string? Name = null,
        [property: JsonPropertyName("exist")] decimal Exist = 0,
        [property: JsonPropertyName("price")] decimal Price = 0,
        [property: JsonPropertyName("count_need")] decimal CountNeed = 1,
        [property: JsonPropertyName("min_order")] decimal MinOrder = 1,
        [property: JsonPropertyName("time_to_exe")] int TimeToExe = 0,
        [property: JsonPropertyName("time_to_exe_guaranteed")] int TimeToExeGuaranteed = 0,
        [property: JsonPropertyName("storage")] string? Storage = null,
        [property: JsonPropertyName("probability")] int Probability = 100,
        [property: JsonPropertyName("price_purchase")] decimal PricePurchase = 0,
        [property: JsonPropertyName("markup")] int Markup = 0,
        [property: JsonPropertyName("office_id")] int OfficeId = 0,
        [property: JsonPropertyName("storage_id")] int StorageId = 0,
        [property: JsonPropertyName("json_params")] string? JsonParams = null,
        [property: JsonPropertyName("check_hash")] string? CheckHash = null);
    private sealed record StorefrontNewsletterSubscribeBody(string? Email, bool ConfirmWrites = false);
    private sealed record StorefrontAddEvaluationBody(long ProductId, int Rating = 5, bool ConfirmWrites = false, string? Text = null);
    private sealed record StorefrontCreateOperationBody(decimal Amount, string? Kind, bool ConfirmWrites = false);
    private sealed record StorefrontCheckOrderNotAuthorizedBody(long OrderId, bool ConfirmWrites = false);
    private sealed record StorefrontSetUserOptionBody(string? OptionKey, string? OptionValue, bool ConfirmWrites = false);
    private sealed record StorefrontSetMyCityBody(long CityId, bool ConfirmWrites = false);
    private sealed record StorefrontCookieProductBody(long ProductId = 0, bool ConfirmWrites = false);
    private sealed record StorefrontProfileSaveBody(Dictionary<string, string>? Fields = null, bool ConfirmWrites = false);
    private sealed record StorefrontLoginSendCodeBody(string? Phone, bool ConfirmWrites = false);
    private sealed record StorefrontLoginCheckCodeBody(string? Code, bool ConfirmWrites = false);

    private static async Task<IResult> MapWishlistCookieAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        bool add,
        CancellationToken cancellationToken)
        => await MapIntListCookieAsync(
            context,
            validator,
            add,
            StorefrontIntListCookie.BookmarksName,
            StorefrontIntListCookie.BookmarksMax,
            "/storefront/wishlist-app",
            add ? "Saved to bookmarks." : "Removed from bookmarks.",
            cancellationToken);

    private static async Task<IResult> MapCompareCookieAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        bool add,
        CancellationToken cancellationToken)
        => await MapIntListCookieAsync(
            context,
            validator,
            add,
            StorefrontIntListCookie.CompareName,
            StorefrontIntListCookie.CompareMax,
            "/storefront/compare-app",
            add ? "Added to compare." : "Removed from compare.",
            cancellationToken);

    private static async Task<IResult> MapIntListCookieAsync(
        HttpContext context,
        ILegacySessionValidator validator,
        bool add,
        string cookieName,
        int maxItems,
        string fallbackReturnUrl,
        string okMessage,
        CancellationToken cancellationToken)
    {
        var session = await validator.ValidateAsync(context, cancellationToken);
        var body = await LiveWriteFormBinder.ReadJsonOrDefaultAsync<StorefrontCookieProductBody>(context, cancellationToken)
                   ?? new();
        var productId = body.ProductId;
        var confirm = body.ConfirmWrites;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            productId = LiveWriteFormBinder.Long(form, "productId", "product_id", "id");
            confirm = LiveWriteFormBinder.Flag(form, "confirmWrites", "confirm_writes");
        }

        if (productId <= 0)
        {
            return LiveWriteFormBinder.Complete(
                context,
                fallbackReturnUrl,
                false,
                "A product is required.",
                new { ok = false, validation_code = "invalid", message = "A product is required.", session = SessionPayload(session) });
        }

        if (!confirm)
        {
            return Results.Ok(new
            {
                ok = false,
                writes = 0,
                writesBlocked = true,
                phpAuthoritative = false,
                message = "Set confirmWrites=true to update the " + cookieName + " cookie on ASP.NET.",
                product_id = productId,
                session = SessionPayload(session),
            });
        }

        var current = StorefrontIntListCookie.Read(context.Request, cookieName, maxItems);
        var next = add
            ? StorefrontIntListCookie.Add(current, (int)productId, maxItems)
            : StorefrontIntListCookie.Remove(current, (int)productId);
        context.Response.Cookies.Append(cookieName, StorefrontIntListCookie.Serialize(next), StorefrontIntListCookie.Options());
        return LiveWriteFormBinder.Complete(
            context,
            fallbackReturnUrl,
            true,
            okMessage,
            new
            {
                ok = true,
                writes = 1,
                phpAuthoritative = false,
                validation_code = "ok",
                message = okMessage,
                product_id = productId,
                count = next.Count,
                session = SessionPayload(session),
            });
    }

    private static bool CanBulkUpload(LegacySessionContext session)
        => session.UserId > 0
           && (session.Kind == LegacySessionKind.Customer || session.Kind == LegacySessionKind.Admin);

    private static StorefrontCartAddRequest? ToCartAddRequest(StorefrontBulkUploadCartItem item, bool confirmWrites)
    {
        if (string.IsNullOrWhiteSpace(item.Manufacturer) || string.IsNullOrWhiteSpace(item.Article))
        {
            return null;
        }

        return new StorefrontCartAddRequest(
            item.ProductType == 0 ? 2 : item.ProductType,
            item.Manufacturer,
            item.Article,
            item.CountNeed > 0 ? item.CountNeed : 1,
            item.Price,
            item.MinOrder,
            item.Exist,
            confirmWrites,
            item.ArticleShow,
            item.Name,
            item.TimeToExe.ToString(CultureInfo.InvariantCulture),
            item.TimeToExeGuaranteed > 0
                ? item.TimeToExeGuaranteed.ToString(CultureInfo.InvariantCulture)
                : item.TimeToExe.ToString(CultureInfo.InvariantCulture),
            item.Storage,
            item.Probability,
            item.PricePurchase,
            item.Markup,
            item.OfficeId,
            item.StorageId,
            item.JsonParams,
            item.CheckHash);
    }

    private static IResult Unauthorized(string message) => Results.Json(
        new { ok = false, error = new { code = "unauthorized", message } },
        statusCode: StatusCodes.Status401Unauthorized);

    private static object SessionPayload(LegacySessionContext session) => new
    {
        kind = session.Kind.ToString(),
        user_id = session.UserId,
        email = session.Email,
        group_ids = session.Groups,
        capabilities = session.Capabilities,
        permissions = session.Permissions
    };
}
