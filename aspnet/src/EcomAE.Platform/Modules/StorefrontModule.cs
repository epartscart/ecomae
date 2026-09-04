using System.Globalization;
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
            StorefrontCartChangeCountNeedBody? body,
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

            var id = (long)(body?.Id ?? 0);
            var qty = body?.CountNeed ?? 0;
            var confirm = body?.ConfirmWrites ?? false;
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
            StorefrontCartCheckForOrderBody? body,
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

            var records = body?.Records ?? [];
            var id = body?.Id ?? 0;
            var checkedForOrder = body?.Checked ?? body?.CheckedForOrder ?? 1;
            var confirm = body?.ConfirmWrites ?? false;
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
            StorefrontCartDeleteBody? body,
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

            var ids = (body?.RecordsToDel ?? []).ToList();
            var confirm = body?.ConfirmWrites ?? false;
            if (body is { Id: > 0 })
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
            StorefrontGarageNotepadAddBody? body,
            ILegacySessionValidator validator,
            IStorefrontGarageNotepadAddDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for garage notepad-add dry-run.");
            }

            body ??= new StorefrontGarageNotepadAddBody(0, null, null, null, 0, 0, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontGarageNotepadAddRequest(
                    body.GarageId,
                    body.Manufacturer,
                    body.Article,
                    body.Name,
                    body.Exist,
                    body.Price,
                    body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteSubmit, async (
            HttpContext context,
            StorefrontQuoteSubmitBody? body,
            ILegacySessionValidator validator,
            IStorefrontQuoteSubmitDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for quote submit dry-run.");
            }

            body ??= new StorefrontQuoteSubmitBody(0, null, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteSubmitRequest(body.QuoteId, body.CustomerNote, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteAccept, async (
            HttpContext context,
            StorefrontQuoteAcceptBody? body,
            ILegacySessionValidator validator,
            IStorefrontQuoteAcceptDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for quote accept dry-run.");
            }

            body ??= new StorefrontQuoteAcceptBody(0, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteAcceptRequest(body.QuoteId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteAddItem, async (
            HttpContext context,
            StorefrontQuoteAddItemBody? body,
            ILegacySessionValidator validator,
            IStorefrontQuoteAddItemDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for quote add-item dry-run.");
            }

            body ??= new StorefrontQuoteAddItemBody(2, null, null, 1, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteAddItemRequest(
                    body.ProductType,
                    body.Manufacturer,
                    body.Article,
                    body.CountNeed,
                    body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontQuoteAddManual, async (
            HttpContext context,
            StorefrontQuoteAddManualBody? body,
            ILegacySessionValidator validator,
            IStorefrontQuoteAddManualDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for quote add-manual dry-run.");
            }

            body ??= new StorefrontQuoteAddManualBody(null, null, 1, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontQuoteAddManualRequest(
                    body.Manufacturer,
                    body.Article,
                    body.CountNeed,
                    body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontGarageSetActive, async (
            HttpContext context,
            StorefrontGarageSetActiveBody? body,
            ILegacySessionValidator validator,
            IStorefrontGarageSetActiveDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for garage set-active dry-run.");
            }

            body ??= new StorefrontGarageSetActiveBody(0, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontGarageSetActiveRequest(body.CarId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontGarageDelete, async (
            HttpContext context,
            StorefrontGarageDeleteBody? body,
            ILegacySessionValidator validator,
            IStorefrontGarageDeleteDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for garage delete dry-run.");
            }

            body ??= new StorefrontGarageDeleteBody(0, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontGarageDeleteRequest(body.CarId, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

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
            StorefrontCheckoutCreateBody? body,
            ILegacySessionValidator validator,
            IStorefrontCheckoutCreateDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for checkout create dry-run.");
            }

            body ??= new StorefrontCheckoutCreateBody(0, null, null, null, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCheckoutCreateRequest(
                    body.HowGetMode, body.OfficeId, body.PhoneNotAuth, body.EmailNotAuth, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontNewsletterSubscribe, async (
            HttpContext context,
            StorefrontNewsletterSubscribeBody? body,
            ILegacySessionValidator validator,
            IStorefrontNewsletterSubscribeDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            // Public/customer dry-run gate; PHP remains authoritative.
            body ??= new StorefrontNewsletterSubscribeBody(null,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontNewsletterSubscribeRequest(body.Email, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.StorefrontAddEvaluation, async (
            HttpContext context,
            StorefrontAddEvaluationBody? body,
            ILegacySessionValidator validator,
            IStorefrontAddEvaluationDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
                return Unauthorized("Customer session required.");
            body ??= new StorefrontAddEvaluationBody(0,0,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontAddEvaluationRequest(body.ProductId, body.Rating, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
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
            StorefrontSetUserOptionBody? body,
            ILegacySessionValidator validator,
            IStorefrontSetUserOptionDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
                return Unauthorized("Customer session required.");
            body ??= new StorefrontSetUserOptionBody(null,null,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontSetUserOptionRequest(body.OptionKey, body.OptionValue, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
        endpoints.MapPost(EcomAeRoutes.StorefrontSetMyCity, async (
            HttpContext context,
            StorefrontSetMyCityBody? body,
            ILegacySessionValidator validator,
            IStorefrontSetMyCityDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            // Public/customer dry-run gate; PHP remains authoritative.
            body ??= new StorefrontSetMyCityBody(0,false);
            return Results.Ok(dryRun.Evaluate(new StorefrontSetMyCityRequest(body.CityId, body.ConfirmWrites)).ToPayload(SessionPayload(session)));
        });
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
            StorefrontOrderSendMessageBody? body,
            ILegacySessionValidator validator,
            IStorefrontOrderSendMessageDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for order send-message dry-run.");
            }

            body ??= new StorefrontOrderSendMessageBody(0, null, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontOrderSendMessageRequest(body.OrderId, body.Text, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });
    }

    private sealed record StorefrontCartChangeCountNeedBody(int Id, decimal CountNeed, bool ConfirmWrites = false);
    private sealed record StorefrontCartCheckForOrderBody(
        IReadOnlyList<long>? Records,
        long Id = 0,
        int Checked = 1,
        int? CheckedForOrder = null,
        bool ConfirmWrites = false);
    private sealed record StorefrontCartDeleteBody(IReadOnlyList<long>? RecordsToDel, long Id = 0, bool ConfirmWrites = false);
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
        bool ConfirmWrites = false);
    private sealed record StorefrontOrderSendMessageBody(long OrderId, string? Text, bool ConfirmWrites = false);
    private sealed record StorefrontGetArticleListBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record StorefrontLoadReturnsDataBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record StorefrontBulkUploadProcessBody(string? Action = null, bool ConfirmWrites = false);
    private sealed record StorefrontNewsletterSubscribeBody(string? Email, bool ConfirmWrites = false);
    private sealed record StorefrontAddEvaluationBody(long ProductId, int Rating = 5, bool ConfirmWrites = false);
    private sealed record StorefrontCreateOperationBody(decimal Amount, string? Kind, bool ConfirmWrites = false);
    private sealed record StorefrontCheckOrderNotAuthorizedBody(long OrderId, bool ConfirmWrites = false);
    private sealed record StorefrontSetUserOptionBody(string? OptionKey, string? OptionValue, bool ConfirmWrites = false);
    private sealed record StorefrontSetMyCityBody(long CityId, bool ConfirmWrites = false);
    private sealed record StorefrontLoginSendCodeBody(string? Phone, bool ConfirmWrites = false);
    private sealed record StorefrontLoginCheckCodeBody(string? Code, bool ConfirmWrites = false);

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
