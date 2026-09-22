using System.Text.Json;
using EcomAE.Platform.Cp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOrderStatusGridFormTests
{
    private static IFormCollection Form(params (string Key, string[] Values)[] fields)
        => new FormCollection(fields.ToDictionary(f => f.Key, f => new StringValues(f.Values)));

    [Fact]
    public void ToJson_ReturnsNullWithoutIndexList()
    {
        Assert.Null(CpOrderStatusGridForm.ToJson(Form(("os_0_caption", ["New"])), "os", CpOrderStatusGridForm.OrderFlags));
        Assert.False(CpOrderStatusGridForm.HasGrid(Form(("ordersJson", ["[]"]))));
    }

    [Fact]
    public void ToJson_BuildsRowsInPostedOrderWithFlagsAndIds()
    {
        var form = Form(
            ("os_idx", ["7", "3"]),
            ("os_7_id", ["0"]),
            ("os_7_caption", [" Packed "]),
            ("os_7_color", ["#112233"]),
            ("os_7_for_created", ["1"]),
            ("os_3_id", ["12"]),
            ("os_3_name", ["order_status_new"]),
            ("os_3_caption", ["New"]),
            ("os_3_color", ["#3498db"]),
            ("os_3_for_paid", ["1"]),
            ("os_3_to_customer_email", ["1"]));

        var json = CpOrderStatusGridForm.ToJson(form, "os", CpOrderStatusGridForm.OrderFlags);
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        var rows = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);

        Assert.Equal("Packed", rows[0].GetProperty("value").GetString());
        Assert.Equal(0, rows[0].GetProperty("created_earlier").GetInt32());
        Assert.False(rows[0].TryGetProperty("id", out _));
        Assert.Equal(1, rows[0].GetProperty("for_created").GetInt32());
        Assert.Equal(0, rows[0].GetProperty("for_paid").GetInt32());

        Assert.Equal(12, rows[1].GetProperty("id").GetInt64());
        Assert.Equal(1, rows[1].GetProperty("created_earlier").GetInt32());
        Assert.Equal("order_status_new", rows[1].GetProperty("value_lang_str_id").GetString());
        Assert.Equal(1, rows[1].GetProperty("for_paid").GetInt32());
        Assert.Equal(1, rows[1].GetProperty("to_customer_email").GetInt32());
        Assert.Equal(0, rows[1].GetProperty("for_inverse").GetInt32());

        var parsed = CpOrderStatusWriteService.ParseOrderStatuses(json);
        Assert.Null(parsed.Error);
        Assert.Equal(2, parsed.Rows.Count);
    }

    [Fact]
    public void ToJson_SkipsBlankCaptionsAndBadIndexes()
    {
        var form = Form(
            ("is_idx", ["1", "x", "2"]),
            ("is_1_caption", ["   "]),
            ("is_2_caption", ["Reserved"]),
            ("is_2_count_flag", ["1"]));

        var json = CpOrderStatusGridForm.ToJson(form, "is", CpOrderStatusGridForm.ItemFlags);
        using var doc = JsonDocument.Parse(json!);
        var rows = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(rows);
        Assert.Equal("Reserved", rows[0].GetProperty("value").GetString());
        Assert.Equal(1, rows[0].GetProperty("count_flag").GetInt32());
        Assert.Equal(0, rows[0].GetProperty("reject_return").GetInt32());
    }

    [Fact]
    public void Endpoint_PrefersGridOverJsonWhenGridPosted()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("CpOrderStatusGridForm.HasGrid(form)", text, StringComparison.Ordinal);
        Assert.Contains("CpOrderStatusGridForm.ToJson(form, CpOrderStatusGridForm.ItemPrefix", text, StringComparison.Ordinal);
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
