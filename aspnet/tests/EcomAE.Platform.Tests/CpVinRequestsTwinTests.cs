using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpVinRequestsTwinTests
{
    [Fact]
    public void Filter_cookie_and_query_follow_php_requests_page()
    {
        var ctx = new DefaultHttpContext();
        var empty = CpVinRequestsService.ReadFilter(ctx.Request);
        Assert.True(empty.IsEmpty);
        Assert.Equal(-1, empty.Viewed);
        Assert.Equal(0, CpVinRequestsService.ReadPage(ctx.Request));

        ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "vin_filter=" + Uri.EscapeDataString("{\"viewed\":\"0\",\"customer_id\":\"42\"}") + "; vin_need_page=2";
        var f = CpVinRequestsService.ReadFilter(ctx.Request);
        Assert.Equal(0, f.Viewed);
        Assert.Equal("42", f.CustomerId);
        Assert.Equal(2, CpVinRequestsService.ReadPage(ctx.Request));

        ctx.Request.QueryString = new QueryString("?viewed=1&customer_id=abc&s_page=5");
        f = CpVinRequestsService.ReadFilter(ctx.Request);
        Assert.Equal(1, f.Viewed);
        Assert.Equal("", f.CustomerId);
        Assert.Equal(5, CpVinRequestsService.ReadPage(ctx.Request));

        ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "vin_filter=not-json";
        Assert.True(CpVinRequestsService.ReadFilter(ctx.Request).IsEmpty);

        Assert.Equal("{\"viewed\":1,\"customer_id\":\"7\"}", CpVinRequestsService.FilterCookieValue(new CpVinFilter(1, "7")));
    }

    [Fact]
    public void Vin_fields_tree_json_is_validated_like_php_save_tree()
    {
        var (items, error) = CpVinFieldsService.ParseTree(
            "[{\"id\":3,\"is_new\":0,\"name\":\"client_vin\",\"value\":\"VIN\",\"maxlen\":\"17\",\"show\":\"1\",\"required\":\"1\",\"value_lang_str_id\":\"2200\"}," +
            "{\"id\":\"1695000\",\"is_new\":true,\"name\":\"engine_code\",\"value\":\"Engine \\\"code\\\"\",\"maxlen\":0,\"show\":0,\"required\":0}]");
        Assert.Null(error);
        Assert.Equal(2, items.Count);
        Assert.Equal("client_vin", items[0].Name);
        Assert.Equal(17, items[0].MaxLen);
        Assert.True(items[0].Show);
        Assert.True(items[0].Required);
        Assert.Equal("2200", items[0].ValueLangStrId);
        Assert.True(items[1].IsNew);
        Assert.Equal("Engine &quot;code&quot;", items[1].Value);

        Assert.NotNull(CpVinFieldsService.ParseTree("").Error);
        Assert.NotNull(CpVinFieldsService.ParseTree("{}").Error);
        Assert.NotNull(CpVinFieldsService.ParseTree("[{\"name\":\"Bad-Key\",\"value\":\"x\"}]").Error);
        Assert.NotNull(CpVinFieldsService.ParseTree("[{\"name\":\"email\",\"value\":\"x\"}]").Error);
        Assert.NotNull(CpVinFieldsService.ParseTree("[{\"name\":\"a\",\"value\":\"x\"},{\"name\":\"a\",\"value\":\"y\"}]").Error);
        Assert.False(CpVinFieldsService.IsValidName("user_id"));
        Assert.True(CpVinFieldsService.IsValidName("client_vin"));
    }

    [Fact]
    public void Routes_pages_and_registration_exist()
    {
        Assert.Equal("/cp/requests/set-vin-viewed", EcomAeRoutes.CpSetUsersVinViewed);
        Assert.Equal("/cp/requests/send-message", EcomAeRoutes.CpSendVinMessage);
        Assert.Equal("/cp/requests/vin-fields/save", EcomAeRoutes.CpVinFieldsSave);

        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpVinRequestsService", program, StringComparison.Ordinal);
        Assert.Contains("ICpVinFieldsService", program, StringComparison.Ordinal);

        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpSendVinMessage", module, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.CpVinFieldsSave", module, StringComparison.Ordinal);

        var fieldsPage = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpVinFieldsApp.razor"));
        Assert.Contains("@page \"/cp/vin-fields-app\"", fieldsPage, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/requests/vin-fields/save\"", fieldsPage, StringComparison.Ordinal);
        Assert.Contains("name=\"tree_json\"", fieldsPage, StringComparison.Ordinal);
        Assert.Contains("client_vin", fieldsPage, StringComparison.Ordinal);

        var listPage = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpSystemRequestsApp.razor"));
        Assert.Contains("vin_filter", listPage, StringComparison.Ordinal);
        Assert.Contains("vin_need_page", listPage, StringComparison.Ordinal);
        Assert.Contains("/cp/vin-fields-app", listPage, StringComparison.Ordinal);
        Assert.Contains("HasUserManagerAccessAsync", listPage, StringComparison.Ordinal);
    }

    [Fact]
    public void Services_use_parameterized_sql()
    {
        var root = FindRepoRoot();
        foreach (var f in new[] { "CpVinRequestsService.cs", "CpVinFieldsService.cs" })
        {
            var src = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp", f));
            Assert.DoesNotContain("' + ", src, StringComparison.Ordinal);
            Assert.DoesNotContain("$\"SELECT", src, StringComparison.Ordinal);
            Assert.DoesNotContain("$\"UPDATE", src, StringComparison.Ordinal);
            Assert.DoesNotContain("$\"INSERT", src, StringComparison.Ordinal);
            Assert.DoesNotContain("$\"DELETE", src, StringComparison.Ordinal);
        }

        var requests = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpVinRequestsService.cs"));
        Assert.Contains("ORDER BY `viewed` ASC, `id` DESC", requests, StringComparison.Ordinal);
        Assert.Contains("UPDATE `users_vin` SET `viewed` = 1 WHERE `id` = ?", requests, StringComparison.Ordinal);
        Assert.Contains("`alias` = 'usermanager'", requests, StringComparison.Ordinal);

        var users = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpUserWriteService.cs"));
        Assert.Contains("INSERT INTO `users_vin_messages`", users, StringComparison.Ordinal);
        Assert.Contains("SET `viewed_customer` = 0", users, StringComparison.Ordinal);

        var fields = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpVinFieldsService.cs"));
        Assert.Contains("UPDATE `vin_fields` SET `order` = 0", fields, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `vin_fields` WHERE `order` = 0", fields, StringComparison.Ordinal);
        Assert.Contains("CpCustomTranslationWriter", fields, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}
