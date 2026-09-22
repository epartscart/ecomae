using System.Text.Json;
using EcomAE.Platform.Cp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpSearchTabEditorTests
{
    [Fact]
    public void ParametersFromForm_MirrorsPhpJsonEncodePost_DroppingControlFields_ArraysForMultiselect()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["action"] = "save",
            ["tab_id"] = "3",
            ["confirmWrites"] = "true",
            ["params_from_form"] = "true",
            ["returnUrl"] = "/cp/search-tabs-app?tab_id=3",
            ["tab_caption"] = "Analogs",
            ["tab_caption_lang_str_id"] = "custom_12",
            ["tab_order"] = "2",
            ["tab_enabled"] = "tab_enabled",
            ["show_prices"] = "1",
            ["min_qty"] = "5",
            ["brands[]"] = new StringValues(["BOSCH", "SKF"]),
        });

        var json = CpSearchTabEditorService.ParametersFromForm(form);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("1", root.GetProperty("show_prices").GetString());
        Assert.Equal("5", root.GetProperty("min_qty").GetString());
        Assert.Equal(["BOSCH", "SKF"], root.GetProperty("brands").EnumerateArray().Select(e => e.GetString() ?? "").ToArray());
        Assert.False(root.TryGetProperty("action", out _));
        Assert.False(root.TryGetProperty("tab_caption", out _));
        Assert.False(root.TryGetProperty("confirmWrites", out _));
        Assert.False(root.TryGetProperty("tab_enabled", out _));
    }

    [Fact]
    public void ParseValues_ReadsScalarsAndArraysFromStoredParametersValues()
    {
        var values = CpSearchTabEditorService.ParseValues("""{"show_prices":"1","min_qty":5,"brands":["BOSCH","SKF"],"flag":true,"nothing":null}""");
        Assert.Equal(["1"], values["show_prices"]);
        Assert.Equal(["5"], values["min_qty"]);
        Assert.Equal(["BOSCH", "SKF"], values["brands"]);
        Assert.Equal(["1"], values["flag"]);
        Assert.Equal([""], values["nothing"]);
        Assert.Empty(CpSearchTabEditorService.ParseValues(""));
        Assert.Empty(CpSearchTabEditorService.ParseValues("not json"));
    }
}
