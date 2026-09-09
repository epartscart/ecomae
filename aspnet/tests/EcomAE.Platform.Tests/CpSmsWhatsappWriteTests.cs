using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public class CpSmsWhatsappWriteTests
{
    [Fact]
    public void Routes_expose_activate()
    {
        Assert.Equal("/cp/sms-whatsapp-app", EcomAeRoutes.ControlPanelSmsWhatsappApp);
        Assert.Equal("/cp/sms-whatsapp/activate", EcomAeRoutes.ControlPanelSmsWhatsappActivate);
    }

    [Fact]
    public void Page_posts_native_activate_forms()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpSmsWhatsappApp.razor"));
        Assert.Contains("method=\"post\"", razor);
        Assert.Contains("action=\"/cp/sms-whatsapp/activate\"", razor);
        Assert.Contains("name=\"confirmWrites\"", razor);
        Assert.Contains("value=\"true\"", razor);
        Assert.Contains("name=\"system_id\"", razor);
        Assert.Contains("value=\"0\"", razor);
        Assert.Contains("Deactivate all", razor);
        Assert.Contains("Activate", razor);
        Assert.Contains("does not invent a send", razor);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor);
        Assert.DoesNotContain("ASP.NET", razor);
        Assert.DoesNotContain("/php-reference/", razor);
        Assert.DoesNotContain("parameters_values", razor);
    }

    [Fact]
    public void Module_maps_activate()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelSmsWhatsappActivate", src);
        Assert.Contains("ICpSmsWhatsappWriteService", src);
        Assert.Contains("CpSmsActivateRequest", src);
    }

    [Fact]
    public void Digest_omits_secrets()
    {
        var sql = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectCpSmsOperators", sql);
        var start = sql.IndexOf("SelectCpSmsOperators", StringComparison.Ordinal);
        var chunk = sql.Substring(start, Math.Min(700, sql.Length - start));
        Assert.DoesNotContain("parameters_values", chunk);
    }

    [Fact]
    public void Normalize_blank_and_empty_object_keep_existing()
    {
        Assert.True(CpSmsWhatsappWriteService.TryNormalizeParametersValues(null, out var json, out var error));
        Assert.Null(json);
        Assert.Null(error);

        Assert.True(CpSmsWhatsappWriteService.TryNormalizeParametersValues("  ", out json, out error));
        Assert.Null(json);
        Assert.Null(error);

        Assert.True(CpSmsWhatsappWriteService.TryNormalizeParametersValues("{}", out json, out error));
        Assert.Null(json);
        Assert.Null(error);
    }

    [Fact]
    public void Normalize_object_with_keys_compacts()
    {
        Assert.True(CpSmsWhatsappWriteService.TryNormalizeParametersValues(
            """{ "api_key" : "secret", "sender_number" : "+971567607011" }""",
            out var json,
            out var error));
        Assert.Null(error);
        Assert.Contains("\"api_key\":\"secret\"", json);
        Assert.Contains("+971567607011", json);
    }

    [Fact]
    public void Normalize_rejects_invalid_and_non_object()
    {
        Assert.False(CpSmsWhatsappWriteService.TryNormalizeParametersValues("not-json", out var json, out var error));
        Assert.Null(json);
        Assert.Contains("JSON", error, StringComparison.OrdinalIgnoreCase);

        Assert.False(CpSmsWhatsappWriteService.TryNormalizeParametersValues("[1]", out json, out error));
        Assert.Null(json);
        Assert.Contains("object", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_registers_write_service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpSmsWhatsappWriteService", src);
        Assert.Contains("CpSmsWhatsappWriteService", src);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root from " + AppContext.BaseDirectory);
    }
}
