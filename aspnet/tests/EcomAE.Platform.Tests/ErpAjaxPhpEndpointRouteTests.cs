using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the legacy ERP AJAX entry (<c>content/general_pages/ajax_epc_erp.php</c>).
/// Nginx exact-routes the whole <c>/content/general_pages/</c> prefix to Kestrel while
/// PHP serving is paused, so the PHP shells' POST must be answered by ASP.NET — not by
/// Kestrel's synthetic "405 HTTP Method Not Supported".
/// </summary>
public sealed class ErpAjaxPhpEndpointRouteTests
{
    private const string LegacyPath = "/content/general_pages/ajax_epc_erp.php";

    [Fact]
    public void Route_IsTheLegacyPhpPath()
    {
        Assert.Equal(LegacyPath, EcomAeRoutes.ErpAjaxPhpEndpoint);
    }

    [Fact]
    public void ErpModule_AnswersPostOnLegacyPath()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains($"MapPost(EcomAeRoutes.ErpAjaxPhpEndpoint", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpShell_StillBuildsTheLegacyPath()
    {
        var text = File.ReadAllText(FindRepoFile("content/shop/finance/epc_erp_access.php"));
        Assert.Contains("'/content/general_pages/ajax_epc_erp.php'", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Nginx_SendsThePrefixToKestrel()
    {
        var text = File.ReadAllText(FindRepoFile(
            "deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf"));
        Assert.Contains("location ^~ /content/general_pages/", text, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://127.0.0.1:5100;", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownAction_StaysDryRunAndPhpAuthoritative()
    {
        var result = new ErpAjaxWriteRegistryDryRun(new ErpAjaxWriteCatalog())
            .Evaluate(new ErpAjaxWriteRegistryRequest("not_a_real_action"));

        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.False(result.CutoverAllowed);
        Assert.True(result.PhpAuthoritative);
        Assert.Equal("dry-run-unknown-action", result.Status);
    }

    [Fact]
    public void ConfirmWrites_IsRefusedUntilPortCompletes()
    {
        var catalog = new ErpAjaxWriteCatalog();
        var action = catalog.All.First().Action;
        var result = new ErpAjaxWriteRegistryDryRun(catalog)
            .Evaluate(new ErpAjaxWriteRegistryRequest(action, ConfirmWrites: true));

        Assert.Equal(0, result.Writes);
        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.True(result.PhpAuthoritative);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}