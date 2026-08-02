using System.Net;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Surfaces;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Renders a presentation-preserving migration shell that links the same PHP CSS/chrome assets.
/// Content is hydrated from <see cref="ISurfaceShellCatalog"/> so structure stays aligned with PHP sections.
/// </summary>
public sealed class LegacyHtmlShellRenderer : ILegacyHtmlShellRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Render(
        string surfaceKey,
        SurfaceShellResponse shell,
        object? sessionPayload,
        string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceKey);
        ArgumentNullException.ThrowIfNull(shell);

        var key = surfaceKey.Trim().ToLowerInvariant();
        var title = WebUtility.HtmlEncode($"{LegacyPresentationAssets.BrandName} · {shell.Surface}");
        var brand = WebUtility.HtmlEncode(LegacyPresentationAssets.BrandName);
        var status = WebUtility.HtmlEncode(shell.ShellStatus);
        var legacyEntry = WebUtility.HtmlEncode(shell.LegacyEntry);
        var aspNetRoute = WebUtility.HtmlEncode(shell.AspNetRoute);
        var tenantMode = WebUtility.HtmlEncode(shell.TenantMode);
        var chromeSource = WebUtility.HtmlEncode(LegacyPresentationAssets.LegacyChromeSourceFor(key));
        var noteHtml = WebUtility.HtmlEncode(note ?? "Presentation-preserving ASP.NET Core shell. PHP remains authoritative until cutover approval.");
        var bodyClass = WebUtility.HtmlEncode(LegacyPresentationAssets.BodyClassFor(key));

        var sb = new StringBuilder(8_192);
        sb.Append("<!DOCTYPE html>\n");
        sb.Append("<html lang=\"en\" data-epc-surface=\"").Append(WebUtility.HtmlEncode(key)).Append("\" data-theme=\"default\">\n");
        sb.Append("<head>\n");
        sb.Append("  <meta charset=\"utf-8\">\n");
        sb.Append("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n");
        sb.Append("  <meta name=\"robots\" content=\"noindex,nofollow,noarchive\">\n");
        sb.Append("  <title>").Append(title).Append("</title>\n");
        sb.Append("  <link rel=\"icon\" type=\"image/svg+xml\" href=\"").Append(LegacyPresentationAssets.BrandMarkUrl).Append("\">\n");

        foreach (var href in LegacyPresentationAssets.StylesheetsFor(key))
        {
            sb.Append("  <link rel=\"stylesheet\" href=\"").Append(WebUtility.HtmlEncode(href)).Append("\">\n");
        }

        sb.Append("  <style>\n");
        sb.Append(InlineChromeCss(key));
        sb.Append("  </style>\n");
        sb.Append("</head>\n");
        sb.Append("<body class=\"").Append(bodyClass).Append("\">\n");
        sb.Append("  <div class=\"epc-aspnet-shell\" data-shell-status=\"").Append(status).Append("\">\n");
        sb.Append("    <header class=\"epc-aspnet-topbar\" role=\"banner\">\n");
        sb.Append("      <a class=\"epc-aspnet-brand\" href=\"").Append(aspNetRoute).Append("\">\n");
        sb.Append("        <img src=\"").Append(LegacyPresentationAssets.BrandMarkUrl).Append("\" alt=\"").Append(brand).Append("\" width=\"36\" height=\"36\">\n");
        sb.Append("        <span>").Append(brand).Append("</span>\n");
        sb.Append("      </a>\n");
        sb.Append("      <div class=\"epc-aspnet-meta\">\n");
        sb.Append("        <strong>").Append(WebUtility.HtmlEncode(shell.Surface)).Append("</strong>\n");
        sb.Append("        <span>").Append(status).Append("</span>\n");
        sb.Append("      </div>\n");
        sb.Append("    </header>\n");
        sb.Append("    <div class=\"epc-aspnet-layout\">\n");
        sb.Append("      <nav class=\"epc-aspnet-nav\" aria-label=\"Surface sections\">\n");
        sb.Append("        <p class=\"epc-aspnet-nav-label\">Sections</p>\n");
        sb.Append("        <ul>\n");
        foreach (var section in shell.Sections)
        {
            sb.Append("          <li>\n");
            sb.Append("            <a href=\"#section-").Append(WebUtility.HtmlEncode(section.Key)).Append("\">")
                .Append(WebUtility.HtmlEncode(section.Title)).Append("</a>\n");
            sb.Append("            <small>").Append(WebUtility.HtmlEncode(section.MigrationStatus)).Append("</small>\n");
            sb.Append("          </li>\n");
        }

        sb.Append("        </ul>\n");
        sb.Append("        <p class=\"epc-aspnet-nav-note\">Chrome CSS sourced from ").Append(chromeSource).Append("</p>\n");
        sb.Append("      </nav>\n");
        sb.Append("      <main class=\"epc-aspnet-main\" id=\"content\">\n");
        sb.Append("        <h1>").Append(brand).Append("</h1>\n");
        sb.Append("        <p class=\"epc-aspnet-lede\">").Append(WebUtility.HtmlEncode(shell.Surface))
            .Append(" — same visual assets as PHP; digests remain read-only until parity cutover.</p>\n");
        sb.Append("        <p class=\"epc-aspnet-note\">").Append(noteHtml).Append("</p>\n");
        sb.Append("        <dl class=\"epc-aspnet-facts\">\n");
        sb.Append("          <div><dt>Legacy entry</dt><dd>").Append(legacyEntry).Append("</dd></div>\n");
        sb.Append("          <div><dt>ASP.NET route</dt><dd>").Append(aspNetRoute).Append("</dd></div>\n");
        sb.Append("          <div><dt>Tenant mode</dt><dd>").Append(tenantMode).Append("</dd></div>\n");
        sb.Append("          <div><dt>JSON payload</dt><dd><a href=\"?format=json\">?format=json</a></dd></div>\n");
        sb.Append("        </dl>\n");

        foreach (var section in shell.Sections)
        {
            sb.Append("        <section class=\"epc-aspnet-section\" id=\"section-")
                .Append(WebUtility.HtmlEncode(section.Key)).Append("\">\n");
            sb.Append("          <h2>").Append(WebUtility.HtmlEncode(section.Title)).Append("</h2>\n");
            sb.Append("          <p>Legacy path: <code>").Append(WebUtility.HtmlEncode(section.LegacyPath)).Append("</code></p>\n");
            sb.Append("          <p>Status: <strong>").Append(WebUtility.HtmlEncode(section.MigrationStatus)).Append("</strong></p>\n");
            if (section.Capabilities.Length > 0)
            {
                sb.Append("          <ul>\n");
                foreach (var capability in section.Capabilities)
                {
                    sb.Append("            <li>").Append(WebUtility.HtmlEncode(capability)).Append("</li>\n");
                }

                sb.Append("          </ul>\n");
            }

            sb.Append("        </section>\n");
        }

        if (shell.NextParityChecks.Length > 0)
        {
            sb.Append("        <section class=\"epc-aspnet-section\" id=\"parity-checks\">\n");
            sb.Append("          <h2>Next parity checks</h2>\n");
            sb.Append("          <ul>\n");
            foreach (var check in shell.NextParityChecks)
            {
                sb.Append("            <li>").Append(WebUtility.HtmlEncode(check)).Append("</li>\n");
            }

            sb.Append("          </ul>\n");
            sb.Append("        </section>\n");
        }

        if (sessionPayload is not null)
        {
            sb.Append("        <section class=\"epc-aspnet-section\" id=\"session\">\n");
            sb.Append("          <h2>Session</h2>\n");
            sb.Append("          <pre>").Append(WebUtility.HtmlEncode(JsonSerializer.Serialize(sessionPayload, JsonOptions))).Append("</pre>\n");
            sb.Append("        </section>\n");
        }

        sb.Append("      </main>\n");
        sb.Append("    </div>\n");
        sb.Append("  </div>\n");
        sb.Append("  <script>\n");
        sb.Append("    document.documentElement.dataset.epcAspnetShell = '1';\n");
        sb.Append("    document.documentElement.dataset.epcShellReady = '1';\n");
        sb.Append("  </script>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string InlineChromeCss(string surfaceKey) => surfaceKey switch
    {
        "bos" => """
            :root { --epc-shell-accent: #111111; --epc-shell-bg: #f0f2f5; --epc-shell-ink: #0f172a; }
            body.epc-bos-shell { margin: 0; background: var(--bos-bg, var(--epc-shell-bg)); color: var(--epc-shell-ink); font-family: "Segoe UI", Tahoma, sans-serif; }
            .epc-aspnet-topbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.75rem 1.25rem; background: #000; color: #fff; }
            .epc-aspnet-brand { display: inline-flex; align-items: center; gap: 0.65rem; color: inherit; text-decoration: none; font-weight: 700; letter-spacing: 0.04em; }
            .epc-aspnet-brand img { display: block; background: #fff; border-radius: 4px; }
            .epc-aspnet-meta { display: flex; flex-direction: column; align-items: flex-end; font-size: 0.85rem; opacity: 0.9; }
            .epc-aspnet-layout { display: grid; grid-template-columns: minmax(220px, 280px) 1fr; min-height: calc(100vh - 64px); }
            .epc-aspnet-nav { background: #000; color: #d4d4d4; padding: 1.25rem; }
            .epc-aspnet-nav a { color: #fff; text-decoration: none; }
            .epc-aspnet-nav-label, .epc-aspnet-nav-note { color: #a3a3a3; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.08em; }
            .epc-aspnet-nav ul { list-style: none; margin: 0.75rem 0 1.5rem; padding: 0; }
            .epc-aspnet-nav li { margin: 0 0 0.85rem; }
            .epc-aspnet-nav small { display: block; color: #a3a3a3; }
            .epc-aspnet-main { padding: 1.5rem 1.75rem 3rem; }
            .epc-aspnet-main h1 { margin: 0 0 0.35rem; font-size: clamp(1.8rem, 3vw, 2.4rem); }
            .epc-aspnet-lede { max-width: 46rem; margin: 0 0 1rem; }
            .epc-aspnet-note { max-width: 46rem; padding: 0.85rem 1rem; background: #fff; border: 1px solid #e2e8f0; }
            .epc-aspnet-facts { display: grid; gap: 0.75rem; margin: 1.25rem 0 1.75rem; }
            .epc-aspnet-facts div { display: grid; grid-template-columns: 9rem 1fr; gap: 0.5rem; }
            .epc-aspnet-facts dt { font-weight: 600; }
            .epc-aspnet-section { margin: 0 0 1.25rem; padding: 1rem 1.1rem; background: #fff; border: 1px solid #e2e8f0; }
            .epc-aspnet-section h2 { margin-top: 0; }
            .epc-aspnet-section pre { overflow: auto; background: #0f172a; color: #e2e8f0; padding: 0.85rem; }
            @media (max-width: 900px) { .epc-aspnet-layout { grid-template-columns: 1fr; } .epc-aspnet-nav { border-bottom: 1px solid #1a1a1a; } }
            @media (prefers-reduced-motion: no-preference) {
              .epc-aspnet-shell { animation: epcShellIn 280ms ease-out; }
              .epc-aspnet-section { animation: epcSectionIn 360ms ease-out both; }
              .epc-aspnet-section:nth-child(2) { animation-delay: 40ms; }
              .epc-aspnet-section:nth-child(3) { animation-delay: 80ms; }
              .epc-aspnet-section:nth-child(4) { animation-delay: 120ms; }
              @keyframes epcShellIn { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: none; } }
              @keyframes epcSectionIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
            }
            """,
        "storefront" => """
            :root { --epc-shell-accent: #1f4b7a; --epc-shell-bg: #f7f8fa; --epc-shell-ink: #1a1a1a; }
            body.epc-storefront-shell { margin: 0; background: var(--epc-shell-bg); color: var(--epc-shell-ink); font-family: "PT Sans", "Segoe UI", sans-serif; }
            .epc-aspnet-topbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.9rem 1.4rem; background: linear-gradient(180deg, #ffffff 0%, #f0f3f7 100%); border-bottom: 1px solid #d7dee8; }
            .epc-aspnet-brand { display: inline-flex; align-items: center; gap: 0.65rem; color: var(--epc-shell-accent); text-decoration: none; font-weight: 700; }
            .epc-aspnet-brand img { display: block; }
            .epc-aspnet-meta { display: flex; flex-direction: column; align-items: flex-end; font-size: 0.85rem; color: #445; }
            .epc-aspnet-layout { display: grid; grid-template-columns: minmax(200px, 260px) 1fr; min-height: calc(100vh - 72px); }
            .epc-aspnet-nav { background: #fff; border-right: 1px solid #d7dee8; padding: 1.25rem; }
            .epc-aspnet-nav a { color: var(--epc-shell-accent); text-decoration: none; font-weight: 600; }
            .epc-aspnet-nav-label, .epc-aspnet-nav-note { color: #667; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.06em; }
            .epc-aspnet-nav ul { list-style: none; margin: 0.75rem 0 1.5rem; padding: 0; }
            .epc-aspnet-nav li { margin: 0 0 0.85rem; }
            .epc-aspnet-nav small { display: block; color: #778; }
            .epc-aspnet-main { padding: 1.5rem 1.75rem 3rem; }
            .epc-aspnet-main h1 { margin: 0 0 0.35rem; font-size: clamp(1.9rem, 3vw, 2.6rem); color: var(--epc-shell-accent); }
            .epc-aspnet-lede { max-width: 46rem; margin: 0 0 1rem; }
            .epc-aspnet-note { max-width: 46rem; padding: 0.85rem 1rem; background: #fff; border: 1px solid #d7dee8; }
            .epc-aspnet-facts { display: grid; gap: 0.75rem; margin: 1.25rem 0 1.75rem; }
            .epc-aspnet-facts div { display: grid; grid-template-columns: 9rem 1fr; gap: 0.5rem; }
            .epc-aspnet-facts dt { font-weight: 600; }
            .epc-aspnet-section { margin: 0 0 1.25rem; padding: 1rem 1.1rem; background: #fff; border: 1px solid #d7dee8; }
            .epc-aspnet-section h2 { margin-top: 0; }
            .epc-aspnet-section pre { overflow: auto; background: #12253a; color: #e8eef6; padding: 0.85rem; }
            @media (max-width: 900px) { .epc-aspnet-layout { grid-template-columns: 1fr; } }
            @media (prefers-reduced-motion: no-preference) {
              .epc-aspnet-shell { animation: epcShellIn 280ms ease-out; }
              .epc-aspnet-section { animation: epcSectionIn 360ms ease-out both; }
              .epc-aspnet-section:nth-child(2) { animation-delay: 40ms; }
              .epc-aspnet-section:nth-child(3) { animation-delay: 80ms; }
              @keyframes epcShellIn { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: none; } }
              @keyframes epcSectionIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
            }
            """,
        _ => """
            :root { --epc-shell-accent: #4f46e5; --epc-shell-bg: #f4f6fb; --epc-shell-ink: #1e293b; }
            body.epc-cp-shell, body.epc-erp-shell, body.epc-migration-shell { margin: 0; background: var(--epc-shell-bg); color: var(--epc-shell-ink); font-family: "Sora", "Segoe UI", sans-serif; }
            .epc-aspnet-topbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.75rem 1.25rem; background: #fff; border-bottom: 1px solid #e2e8f0; position: sticky; top: 0; z-index: 10; }
            .epc-aspnet-brand { display: inline-flex; align-items: center; gap: 0.65rem; color: var(--epc-shell-ink); text-decoration: none; font-weight: 700; }
            .epc-aspnet-brand img { display: block; }
            .epc-aspnet-meta { display: flex; flex-direction: column; align-items: flex-end; font-size: 0.85rem; color: #64748b; }
            .epc-aspnet-layout { display: grid; grid-template-columns: minmax(220px, 280px) 1fr; min-height: calc(100vh - 64px); }
            .epc-aspnet-nav { background: #111827; color: #cbd5e1; padding: 1.25rem; }
            .epc-aspnet-nav a { color: #fff; text-decoration: none; }
            .epc-aspnet-nav-label, .epc-aspnet-nav-note { color: #94a3b8; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.08em; }
            .epc-aspnet-nav ul { list-style: none; margin: 0.75rem 0 1.5rem; padding: 0; }
            .epc-aspnet-nav li { margin: 0 0 0.85rem; }
            .epc-aspnet-nav small { display: block; color: #94a3b8; }
            .epc-aspnet-main { padding: 1.5rem 1.75rem 3rem; }
            .epc-aspnet-main h1 { margin: 0 0 0.35rem; font-size: clamp(1.8rem, 3vw, 2.5rem); }
            .epc-aspnet-lede { max-width: 46rem; margin: 0 0 1rem; }
            .epc-aspnet-note { max-width: 46rem; padding: 0.85rem 1rem; background: #fff; border: 1px solid #e2e8f0; border-left: 4px solid var(--epc-shell-accent); }
            .epc-aspnet-facts { display: grid; gap: 0.75rem; margin: 1.25rem 0 1.75rem; }
            .epc-aspnet-facts div { display: grid; grid-template-columns: 9rem 1fr; gap: 0.5rem; }
            .epc-aspnet-facts dt { font-weight: 600; }
            .epc-aspnet-section { margin: 0 0 1.25rem; padding: 1rem 1.1rem; background: #fff; border: 1px solid #e2e8f0; }
            .epc-aspnet-section h2 { margin-top: 0; }
            .epc-aspnet-section pre { overflow: auto; background: #0f172a; color: #e2e8f0; padding: 0.85rem; }
            @media (max-width: 900px) { .epc-aspnet-layout { grid-template-columns: 1fr; } }
            @media (prefers-reduced-motion: no-preference) {
              .epc-aspnet-shell { animation: epcShellIn 280ms ease-out; }
              .epc-aspnet-section { animation: epcSectionIn 360ms ease-out both; }
              .epc-aspnet-section:nth-child(2) { animation-delay: 40ms; }
              .epc-aspnet-section:nth-child(3) { animation-delay: 80ms; }
              .epc-aspnet-section:nth-child(4) { animation-delay: 120ms; }
              @keyframes epcShellIn { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: none; } }
              @keyframes epcSectionIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
            }
            """
    };
}
