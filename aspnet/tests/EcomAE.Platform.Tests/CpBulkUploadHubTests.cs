using System.Text.Json;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpBulkUploadHubTests
{
    [Fact]
    public void SelectedOption_FollowsPhpOrder()
    {
        static JsonElement Row(string json) => JsonDocument.Parse(json).RootElement;

        var selCross = CpBulkUploadHubService.SelectedOption(Row("{\"exact\":{\"a\":1},\"cross\":{\"a\":2,\"selected\":true}}"));
        Assert.Equal(2, selCross!.Value.GetProperty("a").GetInt32());

        var selExact = CpBulkUploadHubService.SelectedOption(Row("{\"exact\":{\"a\":1,\"selected\":true},\"cross\":{\"a\":2,\"selected\":true}}"));
        Assert.Equal(1, selExact!.Value.GetProperty("a").GetInt32());

        var exactOnly = CpBulkUploadHubService.SelectedOption(Row("{\"exact\":{\"a\":1},\"cross\":{\"a\":2}}"));
        Assert.Equal(1, exactOnly!.Value.GetProperty("a").GetInt32());

        var crossOnly = CpBulkUploadHubService.SelectedOption(Row("{\"exact\":null,\"cross\":{\"a\":2}}"));
        Assert.Equal(2, crossOnly!.Value.GetProperty("a").GetInt32());

        Assert.Null(CpBulkUploadHubService.SelectedOption(Row("{\"exact\":null,\"cross\":false}")));
    }

    [Fact]
    public void CollectProductObjects_FiltersIndexesAndSkipsEmpty()
    {
        const string json = "[{\"exact\":{\"product_object\":{\"id\":1}}},{\"cross\":{\"product_object\":{}}},{\"exact\":{\"product_object\":{\"id\":3}}},\"junk\"]";
        var all = CpBulkUploadHubService.CollectProductObjects(json, null);
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all[0].GetProperty("id").GetInt32());
        Assert.Equal(3, all[1].GetProperty("id").GetInt32());

        var some = CpBulkUploadHubService.CollectProductObjects(json, [2]);
        Assert.Single(some);
        Assert.Equal(3, some[0].GetProperty("id").GetInt32());

        Assert.Empty(CpBulkUploadHubService.CollectProductObjects("", null));
        Assert.Empty(CpBulkUploadHubService.CollectProductObjects("{not json", null));
        Assert.Empty(CpBulkUploadHubService.CollectProductObjects("{\"a\":1}", null));
    }

    [Fact]
    public void HistoryWhere_IsParameterized()
    {
        var (whereAll, argsAll) = CpBulkUploadHubService.BuildHistoryWhere(new CpBulkHistoryFilter(0, "", false, ""));
        Assert.Empty(argsAll);
        Assert.DoesNotContain("'", whereAll, StringComparison.Ordinal);

        var (where, args) = CpBulkUploadHubService.BuildHistoryWhere(new CpBulkHistoryFilter(7, "cp", true, "list.xlsx"));
        Assert.Contains("`user_id` = ?", where, StringComparison.Ordinal);
        Assert.Contains("`source` = ?", where, StringComparison.Ordinal);
        Assert.Contains("`cp_reviewed_at` IS NULL", where, StringComparison.Ordinal);
        Assert.Contains("`file_name` LIKE ?", where, StringComparison.Ordinal);
        Assert.DoesNotContain("list.xlsx", where, StringComparison.Ordinal);
        Assert.Contains(7L, args);
        Assert.Contains("cp", args);
        Assert.Contains("%list.xlsx%", args);
    }

    [Fact]
    public void Json_ScalarHelpers_TolerateStringsAndNumbers()
    {
        var el = JsonDocument.Parse("{\"price\":\"12.50\",\"exist\":\"3\",\"n\":4.7,\"t\":true}").RootElement;
        Assert.Equal(12.50m, CpBulkUploadHubService.Dec(el, "price"));
        Assert.Equal(3, CpBulkUploadHubService.Int(el, "exist"));
        Assert.Equal(4, CpBulkUploadHubService.Int(el, "n"));
        Assert.Equal("1", CpBulkUploadHubService.Str(el, "t"));
        Assert.Equal(string.Empty, CpBulkUploadHubService.Str(el, "missing"));
        Assert.Equal(0m, CpBulkUploadHubService.Dec(el, "missing"));
    }

    [Fact]
    public void Service_WritesPhpTablesWithCpSourceAndParameters()
    {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Cp/CpBulkUploadHubService.cs"));
        Assert.Contains("INSERT INTO `epc_bulk_upload_history`", service, StringComparison.Ordinal);
        Assert.Contains("'cp'", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `shop_carts`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `shop_quote_requests`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `shop_quote_items`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_quotes`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_quote_lines`", service, StringComparison.Ordinal);
        Assert.Contains("`cp_reviewed_at`", service, StringComparison.Ordinal);
        Assert.Contains("IStorefrontBulkUploadCheckService", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("' + ", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_Module_And_Page_WireHub()
    {
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpBulkUploadHubService, EcomAE.Platform.Cp.CpBulkUploadHubService", program, StringComparison.Ordinal);

        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        foreach (var action in new[] { "\"process_upload\"", "\"add_to_cart\"", "\"create_shop_quote\"", "\"create_crm_quote\"", "\"mark_reviewed\"" })
        {
            Assert.Contains(action, module, StringComparison.Ordinal);
        }

        Assert.Contains("hub.ProcessAsync(customerId, groupId, priority, stream, file.FileName", module, StringComparison.Ordinal);
        Assert.Contains("status = \"dry-run-validated\", action = key, upload_id = uploadId", module, StringComparison.Ordinal);
        Assert.Contains("form.Files.GetFile(\"bulk_file\")", module, StringComparison.Ordinal);

        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpBulkUploadApp.razor"));
        Assert.Contains("Bulk Upload Control", razor, StringComparison.Ordinal);
        Assert.Contains("epc-bu-brandbar", razor, StringComparison.Ordinal);
        Assert.Contains("epc-bu-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("Review inbox", razor, StringComparison.Ordinal);
        Assert.Contains("Process for customer", razor, StringComparison.Ordinal);
        Assert.Contains("Open upload", razor, StringComparison.Ordinal);
        Assert.Contains("Add selected to customer cart", razor, StringComparison.Ordinal);
        Assert.Contains("Create shop quote", razor, StringComparison.Ordinal);
        Assert.Contains("Create ERP quote", razor, StringComparison.Ordinal);
        Assert.Contains("enctype=\"multipart/form-data\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"bulk_file\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"indexes\"", razor, StringComparison.Ordinal);
        Assert.Contains("session.Kind == LegacySessionKind.Admin && session.Capabilities.Contains(\"cp\")", razor, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_bulk_cp.css", razor, StringComparison.Ordinal);

        var bridge = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("content/general_pages/epc_bulk_cp.css", bridge, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "content/general_pages/epc_bulk_cp.css")));
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

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
