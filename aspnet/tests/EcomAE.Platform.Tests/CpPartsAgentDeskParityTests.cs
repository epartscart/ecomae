using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPartsAgentDeskParityTests
{
    [Fact]
    public void Session_id_sanitiser_matches_php_preg_replace()
    {
        Assert.Equal("abc-DEF_09", CpPartsAgentDeskService.SafeSessionId("abc-DEF_09"));
        Assert.Equal("abc", CpPartsAgentDeskService.SafeSessionId("a b/c"));
        Assert.Equal("", CpPartsAgentDeskService.SafeSessionId("../../etc/passwd".Replace("etc", "", StringComparison.Ordinal).Replace("passwd", "", StringComparison.Ordinal)));
        Assert.Equal(64, CpPartsAgentDeskService.SafeSessionId(new string('a', 200)).Length);
    }

    [Fact]
    public void Query_clamps_limit_and_offset_like_php()
    {
        Assert.Equal(200, CpPartsAgentDeskQuery.Create(null, null, null, 5000, null).Limit);
        Assert.Equal(1, CpPartsAgentDeskQuery.Create(null, null, null, 0, null).Limit);
        Assert.Equal(50, CpPartsAgentDeskQuery.Create(null, null, null, null, null).Limit);
        Assert.Equal(0, CpPartsAgentDeskQuery.Create(null, null, null, null, -20).Offset);
        Assert.True(CpPartsAgentDeskQuery.Create(" ", "", "", null, null).IsUnfiltered);
        Assert.False(CpPartsAgentDeskQuery.Create("vin", "", "", null, null).IsUnfiltered);
    }

    [Fact]
    public void Date_filters_span_whole_days()
    {
        var from = CpPartsAgentDeskService.ParseFilterDate("2026-01-05", false);
        var to = CpPartsAgentDeskService.ParseFilterDate("2026-01-05", true);
        Assert.NotNull(from);
        Assert.NotNull(to);
        Assert.Equal(86399, to!.Value - from!.Value);
        Assert.Null(CpPartsAgentDeskService.ParseFilterDate("not-a-date", false));
        Assert.Null(CpPartsAgentDeskService.ParseFilterDate("", true));
    }

    [Fact]
    public void Stats_derive_guest_sessions_like_php()
    {
        var stats = new CpPartsAgentStats(10, 3, 12, 4);
        Assert.Equal(6, stats.GuestSessions);
        Assert.Equal(0, new CpPartsAgentStats(2, 0, 0, 5).GuestSessions);
    }

    [Fact]
    public void Empty_view_paging_is_safe()
    {
        var view = CpPartsAgentDeskView.Empty(CpPartsAgentDeskQuery.Create(null, null, null, 50, 0), "unconfigured", "no db");
        Assert.Equal(1, view.Page);
        Assert.Equal(1, view.Pages);
        Assert.Empty(view.Sessions);
    }

    [Fact]
    public async Task Unconfigured_db_returns_typed_empty_view()
    {
        var desk = new CpPartsAgentDeskService(new UnconfiguredConnections());
        var view = await desk.LoadAsync(CpPartsAgentDeskQuery.Create(null, null, null, null, null));
        Assert.Equal("unconfigured", view.Source);
        Assert.Empty(view.Sessions);
        Assert.Null(await desk.LoadTranscriptAsync("abc"));
    }

    [Fact]
    public async Task Csv_export_carries_php_header_row()
    {
        var desk = new CpPartsAgentDeskService(new UnconfiguredConnections());
        var csv = await desk.ExportCsvAsync(CpPartsAgentDeskQuery.Create(null, null, null, null, null));
        Assert.Contains("session_id,updated_at_iso,message_count,user_id,customer,country,client_ip,last_user_text,last_agent_text", csv, StringComparison.Ordinal);
        Assert.StartsWith("\uFEFF", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_reads_php_tables_with_php_filters_and_order()
    {
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPartsAgentDeskService.cs"));
        Assert.Contains("FROM `epc_parts_agent_session`", service, StringComparison.Ordinal);
        Assert.Contains("FROM `epc_parts_agent_message`", service, StringComparison.Ordinal);
        Assert.Contains("ORDER BY `updated_at` DESC", service, StringComparison.Ordinal);
        Assert.Contains("(`session_id` LIKE ? OR `last_user_text` LIKE ? OR `last_agent_text` LIKE ? OR `client_ip` LIKE ?)", service, StringComparison.Ordinal);
        Assert.Contains("`updated_at` >= ?", service, StringComparison.Ordinal);
        Assert.Contains("`updated_at` <= ?", service, StringComparison.Ordinal);
        Assert.Contains("users_profiles", service, StringComparison.Ordinal);
        Assert.Contains("EnsureSchemaAsync", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_renders_php_sections_not_a_digest()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPartsAgentChatsApp.razor"));
        Assert.Contains("AI Parts Expert — chats", razor, StringComparison.Ordinal);
        Assert.Contains("Review storefront conversations", razor, StringComparison.Ordinal);
        Assert.Contains("Price list toggles", razor, StringComparison.Ordinal);
        Assert.Contains("Export CSV", razor, StringComparison.Ordinal);
        Assert.Contains("Last customer message", razor, StringComparison.Ordinal);
        Assert.Contains("Last activity", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"date_from\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"date_to\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"q\"", razor, StringComparison.Ordinal);
        Assert.Contains("href=\"/cp/prices-upload-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("href=\"/cp/product-catalogue-app\"", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/cp/shop/prices\"", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/cp/shop/catalogue/catalogue_editor\"", razor, StringComparison.Ordinal);
        Assert.Contains("ICpPartsAgentDeskService", razor, StringComparison.Ordinal);
        Assert.Contains("LoadTranscriptAsync", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCpPartsAgentDigestAsync", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("system_prompt\">", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_route_is_staff_gated_and_registered()
    {
        Assert.Equal("/cp/parts-agent/export.csv", EcomAeRoutes.CpPartsAgentExportCsv);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var index = module.IndexOf("CpPartsAgentExportCsv", StringComparison.Ordinal);
        Assert.True(index > 0);
        var window = module.Substring(index, 1200);
        Assert.Contains("LegacySessionKind.Admin", window, StringComparison.Ordinal);
        Assert.Contains("Capabilities.Contains(\"cp\")", window, StringComparison.Ordinal);
        Assert.Contains("text/csv", window, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPartsAgentDeskService, EcomAE.Platform.Cp.CpPartsAgentDeskService", program, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
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

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
