using System.Data.Common;
using System.Text;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Contract tests for the crm_main.php twin: scoring, forecast, funnel, quote helpers, input binding, CSRF, dispatcher, surface.</summary>
public sealed class CpCrmEnterpriseTwinTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    [Fact]
    public void Score_lead_bands_match_php_thresholds()
    {
        var cold = CpCrmDeskService.ScoreLead("new", "", "", 0m, 0);
        Assert.Equal("cold", cold.Band);
        Assert.Contains(cold.Reasons, r => r.StartsWith("New lead", StringComparison.Ordinal));

        var hot = CpCrmDeskService.ScoreLead("qualified", "a@b.c", "+971", 60000m, 5);
        Assert.Equal("hot", hot.Band);
        Assert.True(hot.Score >= 70 && hot.Score <= 100);
        Assert.Contains(hot.Reasons, r => r.StartsWith("Has email", StringComparison.Ordinal));
        Assert.Contains(hot.Reasons, r => r.StartsWith("Has phone", StringComparison.Ordinal));
        Assert.Contains(hot.Reasons, r => r.StartsWith("5 activities", StringComparison.Ordinal));

        var warm = CpCrmDeskService.ScoreLead("contacted", "a@b.c", "", 12000m, 0);
        Assert.Equal("warm", warm.Band);
    }

    [Fact]
    public void Forecast_and_funnel_aggregate_like_php()
    {
        var f = CpCrmDeskService.Forecast(
        [
            ("prospect", 2, 1000m, 100m),
            ("negotiation", 1, 5000m, 4000m),
            ("won", 3, 9000m, 9000m),
            ("lost", 1, 500m, 0m),
        ]);
        Assert.Equal(3, f.OpenCount);
        Assert.Equal(6000m, f.OpenValue);
        Assert.Equal(4100m, f.WeightedValue);
        Assert.Equal(9000m, f.WonValue);
        Assert.Equal(500m, f.LostValue);
        Assert.Equal(75m, f.WinRate);
        Assert.Equal(4, f.ByStage.Count);

        var funnel = CpCrmDeskService.Funnel(40, 10, 8, 4, 2);
        Assert.Equal(20m, funnel.LeadToOppPct);
        Assert.Equal(25m, funnel.OppToWonPct);
        Assert.Equal(0m, CpCrmDeskService.Funnel(0, 0, 0, 0, 0).LeadToOppPct);
    }

    [Fact]
    public void Quote_preview_filename_and_html_follow_php()
    {
        Assert.Equal("quote_Q-202609-0001.html", CpCrmQuoteWriteService.PreviewFileName("Q-202609-0001"));
        Assert.Equal("quote_Q_1_x.html", CpCrmQuoteWriteService.PreviewFileName("Q/1 x"));
        Assert.Equal("/content/files/epc_crm_quotes/quote_Q-1.html", CpCrmQuoteWriteService.PreviewUrl("Q-1"));

        var quote = new CpCrmQuoteRow(1, 0, 0, 0, "Q-1", "draft", "USD", 30m, 0, "Thanks & regards", 0, 0, "");
        var html = CpCrmQuoteWriteService.PreviewHtml(quote, [new CpCrmQuoteLine(1, "Brake <pad>", 2m, 15m, 0)], "USD", new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));
        Assert.Contains("Commercial proposal Q-1", html, StringComparison.Ordinal);
        Assert.Contains("Brake &lt;pad&gt;", html, StringComparison.Ordinal);
        Assert.Contains("Thanks &amp; regards", html, StringComparison.Ordinal);
        Assert.Contains("30.00 USD", html, StringComparison.Ordinal);
        Assert.Contains("Generated 2026-09-01 10:00", html, StringComparison.Ordinal);
        Assert.DoesNotContain("AED", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Action_input_binds_form_aliases_and_json()
    {
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.Ordinal)
        {
            ["action"] = "crm_save_lead",
            ["confirmWrites"] = "true",
            ["expected_value"] = "1250,50",
            ["lead_id"] = "7",
        });
        var input = CpCrmActionInput.FromForm(form);
        Assert.Equal("crm_save_lead", input.Text("action"));
        Assert.True(input.Flag("confirmWrites", "confirm_writes"));
        Assert.Equal(1250.50m, input.Dec("expected_value"));
        Assert.Equal(7, input.Long("id", "lead_id"));

        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"action\":\"update_stage\",\"id\":3,\"stage\":\"won\",\"confirmWrites\":1}"));
        var json = await CpCrmActionInput.FromJsonAsync(ctx, CancellationToken.None);
        Assert.Equal("update_stage", json.Text("action"));
        Assert.Equal(3, json.Long("id"));
        Assert.Equal("won", json.Text("stage"));
        Assert.True(json.Flag("confirmWrites"));
    }

    [Fact]
    public async Task Csrf_guard_distinguishes_missing_no_session_mismatch_and_pass()
    {
        var guard = new CpCsrfGuard(new UnconfiguredConnections(), Options.Create(new EcomAeOptions { SecretSuccession = "s3cret" }));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.UserAgent = "xunit";
        var admin = new LegacySessionContext(LegacySessionKind.Admin, 1, "tok", ["cp"]);
        var anon = new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, []);

        Assert.Equal("csrf_missing", (await guard.VerifyAsync(ctx, admin, "")).Code);
        Assert.Equal("csrf_no_session", (await guard.VerifyAsync(ctx, anon, "abc")).Code);

        var key = await guard.KeyForAsync(ctx, admin);
        Assert.Equal(LegacySessionTokenFactory.CsrfGuardKey("s3cret", "tok", null, "xunit"), key);
        Assert.Equal("csrf_mismatch", (await guard.VerifyAsync(ctx, admin, key + "x")).Code);
        Assert.True((await guard.VerifyAsync(ctx, admin, " " + key + " ")).Ok);
    }

    [Fact]
    public void Dispatcher_covers_every_php_ajax_crm_action_with_csrf_gate()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        foreach (var action in new[]
        {
            "save_lead", "delete_lead", "save_opportunity", "update_stage", "convert_lead", "won_hint", "get_timeline",
            "save_activity", "toggle_activity", "dashboard", "adv_dashboard", "pipeline", "save_quote", "accept_quote",
            "quote_preview", "quote_email", "quote_tax", "save_ticket", "update_ticket_status", "save_project",
            "save_project_task", "save_contract", "save_expense", "approve_expense", "get_lead", "score_lead",
            "get_opportunity", "get_ticket", "get_project", "customer_360",
        })
        {
            Assert.Contains("case \"" + action + "\"", module, StringComparison.Ordinal);
        }

        Assert.Contains("key.StartsWith(\"crm_\", StringComparison.Ordinal) ? key.Substring(4) : key", module, StringComparison.Ordinal);
        Assert.Contains("ICpCsrfGuard", module, StringComparison.Ordinal);
        Assert.Contains("CpCsrfGuard.FieldName", module, StringComparison.Ordinal);
        Assert.Contains("Headers.XRequestedWith", module, StringComparison.Ordinal);
        Assert.Contains("IErpCashWriteService", File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmExpenseWriteService.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_cash_accounts`", File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmExpenseWriteService.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Surface_renders_all_twelve_php_tabs_native_forms_and_assets()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmBoardApp.razor"));
        Assert.Contains("@page \"/cp/crm-board-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/crm-board-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("@layout Layout.PhpChromeLayout", razor, StringComparison.Ordinal);
        foreach (var tab in CpCrmDeskService.Tabs)
        {
            Assert.Contains("case \"" + tab.Key + "\":", razor, StringComparison.Ordinal);
        }

        Assert.Equal(12, CpCrmDeskService.Tabs.Count);
        foreach (var cls in new[] { "epc-crm-shell epc-crm-enterprise", "epc-crm-hero", "epc-crm-kpi", "epc-crm-nav epc-erp-subnav epc-cp-tabs--pill", "epc-crm-panel", "epc-crm-pipeline", "epc-crm-card", "epc-crm-drawer", "epc-crm-timeline-modal" })
        {
            Assert.Contains(cls, razor, StringComparison.Ordinal);
        }

        foreach (var field in new[] { "name=\"line_description\"", "name=\"line_qty\"", "name=\"line_unit_price\"", "name=\"customer_user_id\"", "name=\"order_id\"", "name=\"priority\"", "name=\"opportunity_id\"", "name=\"status\"" })
        {
            Assert.Contains(field, razor, StringComparison.Ordinal);
        }

        Assert.Contains("epc-crm-approve-expense", razor, StringComparison.Ordinal);

        Assert.Contains("/platform-assets/epc_crm_enterprise.css", razor, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/epc_crm_board.js", razor, StringComparison.Ordinal);
        Assert.Contains("window.EPC_CRM", razor, StringComparison.Ordinal);
        Assert.Contains("Csrf.KeyForAsync(ctx, session", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);

        var bridge = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("\"/platform-assets/epc_crm_enterprise.css\"", bridge, StringComparison.Ordinal);
        Assert.Contains("\"cp/content/shop/crm/epc_crm_board.js\"", bridge, StringComparison.Ordinal);

        var js = File.ReadAllText(Path.Combine(root, "cp/content/shop/crm/epc_crm_board.js"));
        Assert.Contains("csrf_guard_key", js, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", js, StringComparison.Ordinal);
        Assert.Contains("epc-crm-convert-form", js, StringComparison.Ordinal);
        Assert.Contains("act('delete_lead')", js, StringComparison.Ordinal);
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
