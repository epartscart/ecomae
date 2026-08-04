using EcomAE.Platform.Auth;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
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

            var summary = await dashboards.BuildStorefrontAccountAsync(session.UserId, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                summary,
                session = SessionPayload(session),
                note = "Read-only migration summary. PHP customer account remains authoritative."
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
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer)
            {
                return Unauthorized("Customer session required for storefront search digest.");
            }

            var result = await dashboards.SearchStorefrontPartsAsync(article ?? string.Empty, limit ?? 25, cancellationToken);
            return Results.Ok(new
            {
                ok = true,
                surface = "storefront",
                article = result.Article,
                rows = result.Rows,
                count = result.Count,
                source = result.Source,
                message = result.Message,
                session = SessionPayload(session),
                note = "Read-only warehouse offer digest (pyapi SQL parity). PHP /shop/part_search tabs/VIN/cart remain authoritative."
            });
        });

        endpoints.MapGet(EcomAeRoutes.StorefrontCart, async (
            HttpContext context,
            int? limit,
            ILegacySessionValidator validator,
            ISurfaceDashboardSummaryReporter dashboards,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for storefront cart digest.");
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
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for cart qty dry-run.");
            }

            body ??= new StorefrontCartChangeCountNeedBody(0, 0, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCartChangeCountNeedRequest(body.Id, body.CountNeed, body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontCartCheckForOrder, async (
            HttpContext context,
            StorefrontCartCheckForOrderBody? body,
            ILegacySessionValidator validator,
            IStorefrontCartCheckForOrderDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for cart check-for-order dry-run.");
            }

            body ??= new StorefrontCartCheckForOrderBody([], false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCartCheckForOrderRequest(body.Records ?? [], body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontCartDelete, async (
            HttpContext context,
            StorefrontCartDeleteBody? body,
            ILegacySessionValidator validator,
            IStorefrontCartDeleteDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for cart delete dry-run.");
            }

            body ??= new StorefrontCartDeleteBody([], false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCartDeleteRequest(body.RecordsToDel ?? [], body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });

        endpoints.MapPost(EcomAeRoutes.StorefrontCartAdd, async (
            HttpContext context,
            StorefrontCartAddBody? body,
            ILegacySessionValidator validator,
            IStorefrontCartAddDryRun dryRun,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (session.Kind != LegacySessionKind.Customer || session.UserId <= 0)
            {
                return Unauthorized("Customer session required for cart add dry-run.");
            }

            body ??= new StorefrontCartAddBody(2, null, null, 0, 0, 0, 0, false);
            var result = await dryRun.EvaluateAsync(
                session.UserId,
                new StorefrontCartAddRequest(
                    body.ProductType,
                    body.Manufacturer,
                    body.Article,
                    body.CountNeed,
                    body.Price,
                    body.MinOrder,
                    body.Exist,
                    body.ConfirmWrites),
                cancellationToken);
            return Results.Ok(result.ToPayload(SessionPayload(session)));
        });
    }

    private sealed record StorefrontCartChangeCountNeedBody(int Id, decimal CountNeed, bool ConfirmWrites = false);
    private sealed record StorefrontCartCheckForOrderBody(IReadOnlyList<long>? Records, bool ConfirmWrites = false);
    private sealed record StorefrontCartDeleteBody(IReadOnlyList<long>? RecordsToDel, bool ConfirmWrites = false);
    private sealed record StorefrontCartAddBody(
        int ProductType,
        string? Manufacturer,
        string? Article,
        decimal CountNeed,
        decimal Price,
        decimal MinOrder = 0,
        decimal Exist = 0,
        bool ConfirmWrites = false);

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
