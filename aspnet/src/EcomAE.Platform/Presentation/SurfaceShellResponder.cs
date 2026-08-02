using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;

namespace EcomAE.Platform.Presentation;

public static class SurfaceShellResponder
{
    public static IResult Respond(
        HttpContext context,
        string surfaceKey,
        ISurfaceShellCatalog shells,
        ILegacyHtmlShellRenderer html,
        TenantContext? tenant,
        object sessionPayload,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceKey);
        ArgumentNullException.ThrowIfNull(shells);
        ArgumentNullException.ThrowIfNull(html);

        var shell = shells.Build(surfaceKey, tenant);
        var format = PresentationFormatNegotiator.Resolve(context.Request);
        if (format == PresentationFormat.Html)
        {
            var document = html.Render(surfaceKey, shell, sessionPayload, note);
            return Results.Content(document, "text/html; charset=utf-8");
        }

        return Results.Ok(new
        {
            shell,
            session = sessionPayload,
            note,
            presentation = new
            {
                format = "json",
                html_available = true,
                chrome_source = LegacyPresentationAssets.LegacyChromeSourceFor(surfaceKey),
                stylesheets = LegacyPresentationAssets.StylesheetsFor(surfaceKey),
                hint = "Request Accept: text/html or ?format=html for the presentation-preserving legacy chrome shell."
            }
        });
    }
}
