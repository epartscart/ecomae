using System.Data.Common;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Contract tests for the prices_send.php twin: cookies/filters, SQL, tree, request binding, CSV, files, gates, dispatcher, surface.</summary>
public sealed class CpPricesSendTwinTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    private sealed class NoEmail : ICpTenantEmailWriteService
    {
        public Task<ErpSimpleWriteResult> SaveAsync(CpTenantEmailSaveRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ErpSimpleWriteResult> SendTestAsync(string? testTo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ErpSimpleWriteResult> SendAsync(CpTenantEmailMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static CpPricesSendWriteService Writes(string root) => new(new UnconfiguredConnections(), new NoEmail(), root);

    [Fact]
    public void Filter_and_sort_cookies_follow_php_json_shape()
    {
        var f = CpPricesSendUserFilter.FromCookieJson("{\"user_id\":\"12\",\"group_id\":4,\"email\":\" a@b.c \",\"cellphone\":\"\",\"surname\":\"Smith\"}");
        Assert.Equal("12", f.UserId);
        Assert.Equal(4, f.GroupId);
        Assert.Equal("a@b.c", f.Email);
        Assert.Equal("Smith", f.Surname);
        Assert.Equal(CpPricesSendUserFilter.Empty, CpPricesSendUserFilter.FromCookieJson("not json"));

        var s = CpPricesSendUserSort.FromCookieJson("{\"field\":\"email\",\"asc_desc\":\"asc\"}");
        Assert.Equal("email", s.Field);
        Assert.True(s.Ascending);
        Assert.Equal(CpPricesSendUserSort.Default, CpPricesSendUserSort.FromCookieJson("[1]"));
        Assert.Equal("user_id", CpPricesSendUserSort.Normalize("drop table", "asc").Field);
        Assert.False(s.Toggle("email").Ascending);
        Assert.True(s.Toggle("fio").Ascending);
    }

    [Fact]
    public void Users_where_and_sql_are_parameterised_like_php()
    {
        var (where, args) = CpPricesSendDeskService.BuildUserWhere(new CpPricesSendUserFilter("7", 3, "x@y", "050", "Ali"));
        Assert.Contains("?", where, StringComparison.Ordinal);
        Assert.DoesNotContain("x@y", where, StringComparison.Ordinal);
        Assert.DoesNotContain("Ali", where, StringComparison.Ordinal);
        Assert.Contains(args, a => a is string v && v.Contains("x@y", StringComparison.Ordinal));
        Assert.Contains(args, a => a is string v && v.Contains("Ali", StringComparison.Ordinal));

        var sql = CpPricesSendDeskService.UsersSql(CpPricesSendUserFilter.Empty, new CpPricesSendUserSort("fio", true));
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        Assert.Contains("ASC", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("`drop`", CpPricesSendDeskService.UsersSql(CpPricesSendUserFilter.Empty, CpPricesSendUserSort.Normalize("drop", "desc")), StringComparison.Ordinal);
    }

    [Fact]
    public void Category_tree_nests_children_under_parents()
    {
        var tree = CpPricesSendDeskService.BuildTree(
        [
            (1, 0, "Engine"),
            (2, 1, "Filters"),
            (3, 2, "Oil filters"),
            (4, 0, "Body"),
            (5, 99, "Orphan"),
        ]);
        Assert.Equal(3, tree.Count);
        var engine = tree.Single(n => n.Id == 1);
        Assert.Single(engine.Children);
        Assert.Equal("Oil filters", engine.Children[0].Children[0].Value);
        Assert.Contains(tree, n => n.Id == 5);
    }

    [Fact]
    public void Request_binds_php_request_object_aliases_and_form_fields()
    {
        var raw = "{\"action\":\"check_office_storages_map\",\"offices\":\"2\",\"arr_storages\":[\"5\",7],\"arr_category\":[11],\"users_list\":[3],\"emails_list\":\"a@b.c, d@e.f\",\"group_id_my_list_emails\":\"4\",\"profile_group_ids\":[6],\"filter_brand\":\" toyota \",\"filter_article\":\"81-145\"}";
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.Ordinal)
        {
            ["request_object"] = raw,
            ["csrf_guard_key"] = "k",
        });
        var req = CpPricesSendRequest.FromInput(CpCrmActionInput.FromForm(form));
        Assert.Equal("check_office_storages_map", req.Action);
        Assert.True(req.IsKnown);
        Assert.Equal(2, req.OfficeId);
        Assert.Equal([5, 7], req.StorageIds);
        Assert.Equal([11L], req.CategoryIds);
        Assert.Equal([3L], req.UserIds);
        Assert.Equal(4, req.GroupIdMyListEmails);
        Assert.Equal([6], req.ProfileGroupIds);
        Assert.Equal("toyota", req.FilterBrand);
        Assert.Equal(2, req.Emails.Count);

        var encoded = CpPricesSendRequest.FromRequestObjectJson(Uri.EscapeDataString("{\"action\":\"brands\",\"limit\":5}"));
        Assert.NotNull(encoded);
        Assert.Equal("list_brands", encoded!.Action);
        Assert.Equal(5, encoded.Limit);

        var plain = CpPricesSendRequest.FromInput(CpCrmActionInput.FromForm(new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.Ordinal)
        {
            ["action"] = "send_prices",
            ["users_list"] = "1,2",
        })));
        Assert.Equal("send_prices", plain.Action);
        Assert.Equal([1L, 2L], plain.UserIds);
        Assert.False(CpPricesSendRequest.FromInput(CpCrmActionInput.FromForm(new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.Ordinal) { ["action"] = "drop" }))).IsKnown);
    }

    [Fact]
    public void Csv_rows_markup_currency_rounding_and_filters_follow_php()
    {
        Assert.Equal(115m, CpPricesSendCsv.FinalPrice(100m, 1m, 0.15m, 0));
        Assert.Equal(367m, CpPricesSendCsv.FinalPrice(100m, 3.67m, 0m, 1));
        Assert.Equal(368m, CpPricesSendCsv.FinalPrice(100m, 3.671m, 0m, 1));
        Assert.Equal(375m, CpPricesSendCsv.FinalPrice(372m, 1m, 0m, 2));
        Assert.Equal(380m, CpPricesSendCsv.FinalPrice(377m, 1m, 0m, 2));
        Assert.Equal(380m, CpPricesSendCsv.FinalPrice(371m, 1m, 0m, 3));
        Assert.Equal("12.5", CpPricesSendCsv.PriceCell(12.499m));

        Assert.Equal("8114560Q51", CpPricesSendCsv.NormalizeArticle("81-145 60q51"));
        Assert.True(CpPricesSendCsv.BrandMatches("TOYOTA", "toy"));
        Assert.False(CpPricesSendCsv.BrandMatches("HONDA", "toy"));
        Assert.True(CpPricesSendCsv.ArticleMatches("81-14560Q51", CpPricesSendCsv.NormalizeArticle("14560")));
        Assert.False(CpPricesSendCsv.ArticleMatches("81-14560Q51", CpPricesSendCsv.NormalizeArticle("99999")));

        var header = CpPricesSendCsv.HeaderLine(CpPricesSendCsv.DefaultColumns);
        Assert.StartsWith("Manufacturer;", header, StringComparison.Ordinal);
        Assert.EndsWith(";;;;", header, StringComparison.Ordinal);

        var line = CpPricesSendCsv.RowLine(CpPricesSendCsv.DefaultColumns, new CpPricesSendCsv.Row("Toyota Motor", "81-1", "Lamp", "10", 3, 12.5m, "0", "https://x/p/1"), docpart: true);
        var cells = line.Split(';');
        Assert.Equal(12, cells.Length);
        Assert.Equal(string.Empty, cells[11]);
        Assert.Equal("811\t", cells[1]);
        Assert.Equal("3", cells[4]);
        Assert.Equal("12.5", cells[5]);
        Assert.Equal("1", cells[6]);
        Assert.Equal("811_TOYOTA_MOTOR", cells[7]);
        Assert.Equal("https://x/p/1", cells[8]);

        Assert.Equal(2, CpPricesSendCsv.DocpartDays(0, 2));
        Assert.Equal(5, CpPricesSendCsv.DocpartDays(3, 2));
        Assert.Equal(2, CpPricesSendCsv.AdditionalDays(60));
    }

    [Fact]
    public void File_names_urls_and_paths_stay_inside_prices_tmp()
    {
        Assert.Equal("prices_4.csv", CpPricesSendCsv.FileName(4, null));
        Assert.Equal("dealer-A.csv", CpPricesSendCsv.FileName(4, "dealer-A"));
        Assert.Equal("prices_4.csv", CpPricesSendCsv.FileName(4, "../etc/passwd"));
        Assert.Equal("/content/files/Documents/prices_tmp/prices%204.csv", CpPricesSendCsv.PublicUrl("prices 4.csv"));

        var root = Path.Combine(Path.GetTempPath(), "epc_ps_" + Guid.NewGuid().ToString("N"));
        var svc = Writes(root);
        Assert.Equal(Path.Combine(root, "Documents", "prices_tmp"), svc.PricesTmpDir);
        Assert.NotNull(svc.SafeFilePath("prices_2.csv"));
        Assert.Null(svc.SafeFilePath("../prices_2.csv"));
        Assert.Null(svc.SafeFilePath("prices_2.txt"));
        Assert.Null(svc.SafeFilePath("..%2Fx.csv"));
    }

    [Fact]
    public void Groups_merge_in_php_order_without_duplicates()
    {
        Assert.Equal([3, 5, 4, 6], CpPricesSendWriteService.MergeGroups([3, 5, 3], 5, [4, 6, 3]));
        Assert.Equal([2, 4, 5, 6, 7], CpPricesSendWriteService.DefaultLinkGroups);
    }

    [Fact]
    public async Task Writes_report_no_database_and_validation_explicitly()
    {
        var svc = Writes(Path.GetTempPath());
        var req = CpPricesSendRequest.FromRequestObjectJson("{\"action\":\"create_prices\",\"offices\":1,\"arr_storages\":[1],\"users_list\":[1]}")!;
        Assert.Equal("db", (await svc.CreatePricesAsync(req)).Code);
        Assert.Equal("db", (await svc.CheckOfficeStoragesMapAsync(req)).Code);
        Assert.Equal("db", (await svc.EnsureOfficeStorageLinksAsync(req)).Code);
        Assert.Equal("db", (await svc.SendPricesAsync(req)).Code);

        var noStorage = CpPricesSendRequest.FromRequestObjectJson("{\"action\":\"ensure_office_storage_links\",\"offices\":1}")!;
        Assert.Equal("invalid", (await svc.EnsureOfficeStorageLinksAsync(noStorage)).Code);

        var desk = await new CpPricesSendDeskService(new UnconfiguredConnections()).LoadAsync(CpPricesSendUserFilter.Empty, CpPricesSendUserSort.Default);
        Assert.False(desk.DatabaseAvailable);
        Assert.Equal("Recipients", desk.Label("3662"));
        Assert.Empty(desk.Users);
    }

    [Fact]
    public void Answer_payload_carries_php_status_files_and_brands()
    {
        var a = new CpPricesSendAnswer(true, "done", "ok", Files: [new CpPricesSendGeneratedFile(4, "prices_4.csv", 12, "/content/files/Documents/prices_tmp/prices_4.csv")], Brands: [new CpPricesSendBrand("TOYOTA", 9)], RowsTotal: 12);
        var p = a.ToPayload();
        Assert.Equal(true, p["status"]);
        Assert.Equal("ok", p["validation_code"]);
        Assert.NotNull(p["files"]);
        Assert.NotNull(p["brands"]);
        Assert.Equal(12, p["rows_total"]);
    }

    [Fact]
    public void Dispatcher_covers_php_ajax_operations_with_csrf_and_confirm_gate()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpPricesSendAction", module, StringComparison.Ordinal);
        Assert.Equal("/cp/prices-send/action", EcomAeRoutes.CpPricesSendAction);
        foreach (var action in CpPricesSendRequest.Actions)
        {
            Assert.Contains("\"" + action + "\"", module, StringComparison.Ordinal);
        }

        var idx = module.IndexOf("EcomAeRoutes.CpPricesSendAction", StringComparison.Ordinal);
        var block = module.Substring(idx, Math.Min(9000, module.Length - idx));
        Assert.Contains("ICpPricesSendWriteService", block, StringComparison.Ordinal);
        Assert.Contains("CpCsrfGuard.FieldName", block, StringComparison.Ordinal);
        Assert.Contains("dry_run", block, StringComparison.Ordinal);
        Assert.Contains("LegacySessionKind.Admin", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Surface_renders_php_hero_native_ids_and_assets()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpPricesSendApp.razor"));
        Assert.Contains("@page \"/cp/prices-send-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("<h2>Send price lists</h2>", razor, StringComparison.Ordinal);
        Assert.Contains("Build CSV price profiles with group markup, optional brand/article filters, then download or email customers.", razor, StringComparison.Ordinal);
        Assert.Contains("--ps-accent:#0f766e", razor, StringComparison.Ordinal);
        Assert.Contains("--ps-accent-2:#b45309", razor, StringComparison.Ordinal);
        foreach (var id in new[] { "user_id", "group_id", "email", "cellphone", "surname", "my_list_emails", "group_id_my_list_emails", "offices", "epc_ps_profile_group", "epc_ps_filter_brand", "epc_ps_filter_article", "storages", "container_A", "create_prices_status", "send_prices_btn" })
        {
            Assert.Contains("id=\"" + id + "\"", razor, StringComparison.Ordinal);
        }

        foreach (var step in new[] { "<b>1</b> Recipients", "<b>2</b> Sources", "<b>3</b> Filters", "<b>4</b> Generate" })
        {
            Assert.Contains(step, razor, StringComparison.Ordinal);
        }

        Assert.Contains("onclick=\"create_prices();\"", razor, StringComparison.Ordinal);
        Assert.Contains("onclick=\"send_prices();\" disabled", razor, StringComparison.Ordinal);
        Assert.Contains("onclick=\"epcPsLinkStorages();\"", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Send stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_prices_send.js", razor, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(root, "cp/content/shop/prices_send/epc_prices_send.js")));
        var bridge = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("cp/content/shop/prices_send/epc_prices_send.js", bridge, StringComparison.Ordinal);
        var js = File.ReadAllText(Path.Combine(root, "cp/content/shop/prices_send/epc_prices_send.js"));
        Assert.Contains("check_office_storages_map", js, StringComparison.Ordinal);
        Assert.Contains("request_object=", js, StringComparison.Ordinal);
        Assert.Contains("csrf_guard_key=", js, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
