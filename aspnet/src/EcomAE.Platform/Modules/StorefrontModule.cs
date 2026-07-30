namespace EcomAE.Platform.Modules;

public sealed class StorefrontModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "storefront",
        "Storefront / Marketing",
        "/",
        "content/shop/, content/general_pages/, templates/",
        "not_started",
        []);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/storefront/migration-placeholder", () => Results.Ok(new
        {
            surface = "Storefront / marketing",
            migration = "not_started",
            next = "Port SEO-safe storefront routes, product pages, cart, checkout, CMS, sitemaps"
        }));
    }
}
