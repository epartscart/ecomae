using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpConfigFileTests
{
    private const string Sample = "<?php\nclass DP_Config\n{\n\tpublic $site_name = '1234';//site\n\tpublic $shop_currency = '784';\n\tpublic $order_without_auth = '1';\n}";

    [Fact]
    public void Parse_ReadsParametersAndKeepsOtherLines()
    {
        var lines = PhpConfigFile.Parse(Sample);
        var values = PhpConfigFile.Values(lines);
        Assert.Equal("1234", values["site_name"]);
        Assert.Equal("784", values["shop_currency"]);
        Assert.Equal(3, lines.Count(l => l.IsParameter));
        Assert.Equal("//site", lines.First(l => l.Name == "site_name").Comment);
        Assert.Equal(Sample, PhpConfigFile.Render(lines));
    }

    [Fact]
    public void Set_ReplacesExistingKeepingCommentAndAppendsUnknownBeforeClosingBrace()
    {
        var lines = PhpConfigFile.Parse(Sample);
        var updated = PhpConfigFile.Set(lines, "shop_currency", "840");
        updated = PhpConfigFile.Set(updated, "brand_new", "x");
        var text = PhpConfigFile.Render(updated);
        Assert.Contains("\tpublic $shop_currency = '840';", text, StringComparison.Ordinal);
        Assert.Contains("\tpublic $site_name = '1234';//site", text, StringComparison.Ordinal);
        Assert.EndsWith("\tpublic $brand_new = 'x';\n}", text, StringComparison.Ordinal);
        Assert.Equal("x", PhpConfigFile.Values(updated)["brand_new"]);
    }

    [Fact]
    public void Sanitize_MatchesPhpConfigEditRules()
    {
        Assert.Equal("O&#039;Reilly &quot;x&quot;", PhpConfigFile.Sanitize(" O'Reilly \"x\" ", "site_name", 0));
        Assert.Equal("line1line2", PhpConfigFile.Sanitize("line1\r\nline2\t", "shop_name", 0));
        Assert.Equal("Dubai\\nUAE", PhpConfigFile.Sanitize("Dubai\r\nUAE", "epc_head_office_address", 0));
        Assert.Equal("[CODE]php echo 1; [/CODE]", PhpConfigFile.Sanitize("<?php echo 1; ?>", "x", 0));
        Assert.Equal("&lt;b&gt;", PhpConfigFile.Sanitize("<b>", "x", 1));
    }

    [Fact]
    public void Checkbox_CastsLikePhpFilterVar()
    {
        Assert.Equal("1", PhpConfigFile.CheckboxValue("on"));
        Assert.Equal("1", PhpConfigFile.CheckboxValue("true"));
        Assert.Equal("", PhpConfigFile.CheckboxValue(null));
        Assert.Equal("", PhpConfigFile.CheckboxValue("0"));
        Assert.True(PhpConfigFile.IsTruthy("1"));
        Assert.False(PhpConfigFile.IsTruthy(""));
    }

    [Fact]
    public void Endpoint_IsRegisteredWithCpGate()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpConfigWrite", text, StringComparison.Ordinal);
        Assert.Contains("Admin CP capability required for settings.", text, StringComparison.Ordinal);
        Assert.Contains("Set confirmWrites=true to save settings to config.php from ASP.NET.", text, StringComparison.Ordinal);
        Assert.Contains("ICpConfigEditorService, EcomAE.Platform.Cp.CpConfigEditorService", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relative)))
        {
            dir = dir.Parent;
        }

        return dir is null ? throw new FileNotFoundException(relative) : Path.Combine(dir.FullName, relative);
    }
}
