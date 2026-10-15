using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOperatorAiTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);
    private const string Key = "sk-test-operator-key-123456";

    [Fact]
    public void Spoken_tool_requests_keep_the_work_and_leave_plain_receipts_alone()
    {
        var chat = ErpVoiceInterpreter.Interpret(
            "ask chat gpt to create a receipt voucher for customer Ahmed for 1500 from the main cash account",
            Today);
        Assert.Equal("tool", chat.Intent);
        Assert.Equal("chatgpt", chat.Slots["provider"]);
        Assert.StartsWith("create a receipt voucher", chat.Slots["task"], StringComparison.Ordinal);

        var cursor = ErpVoiceInterpreter.Interpret("please ask cursor to add a stock check", Today);
        Assert.Equal("cursor", cursor.Slots["provider"]);
        Assert.Equal("add a stock check", cursor.Slots["task"]);

        var devin = ErpVoiceInterpreter.Interpret("tell devin to review the sales order page", Today);
        Assert.Equal("devin", devin.Slots["provider"]);
        Assert.Equal("review the sales order page", devin.Slots["task"]);

        var bare = ErpVoiceInterpreter.Interpret("ask chatgpt", Today);
        Assert.Contains("task", bare.Missing);
        Assert.False(bare.Ready);

        var receipt = ErpVoiceInterpreter.Interpret(
            "create a receipt voucher for customer Ahmed for 1500 from the main cash account",
            Today);
        Assert.Equal("receipt", receipt.Intent);

        var help = ErpVoiceInterpreter.Interpret("what can I say", Today);
        Assert.Contains("ChatGPT", help.Say, StringComparison.Ordinal);
        Assert.Contains("Cursor", help.Say, StringComparison.Ordinal);
        Assert.Contains("Devin", help.Say, StringComparison.Ordinal);
        AssertClean(help.Say);
        AssertClean(chat.Say);
    }

    [Fact]
    public void Gateway_only_calls_the_three_account_hosts()
    {
        Assert.True(ErpOperatorAiGateway.IsAllowed("https://api.openai.com/v1/chat/completions"));
        Assert.True(ErpOperatorAiGateway.IsAllowed("https://api.openai.com/v1/models"));
        Assert.True(ErpOperatorAiGateway.IsAllowed("https://api.cursor.com/v1/agents"));
        Assert.True(ErpOperatorAiGateway.IsAllowed("https://api.cursor.com/v1/me"));
        Assert.True(ErpOperatorAiGateway.IsAllowed("https://api.devin.ai/v3/self"));
        Assert.True(ErpOperatorAiGateway.IsAllowed("https://api.devin.ai/v3/organizations/org_acme/sessions"));
        Assert.False(ErpOperatorAiGateway.IsAllowed("http://api.openai.com/v1/models"));
        Assert.False(ErpOperatorAiGateway.IsAllowed("https://api.openai.com/v1/files"));
        Assert.False(ErpOperatorAiGateway.IsAllowed("https://evil.example/v1/chat/completions"));
        Assert.False(ErpOperatorAiGateway.IsAllowed("https://api.devin.ai/v3/organizations/../sessions"));
        Assert.False(ErpOperatorAiGateway.IsAllowed("https://user:pass@api.cursor.com/v1/me"));
        Assert.True(ErpOperatorAiDispatcher.IsOperatorUrl("https://cursor.com/agents/bc-1"));
        Assert.True(ErpOperatorAiDispatcher.IsOperatorUrl("https://app.devin.ai/sessions/devin-abc"));
        Assert.False(ErpOperatorAiDispatcher.IsOperatorUrl("https://evil.example/agents/bc-1"));
    }

    [Fact]
    public void Saved_key_is_unreadable_without_the_same_protector()
    {
        var protector = new ErpOperatorAiProtector(new EphemeralDataProtectionProvider());
        var hidden = protector.Protect(Key);
        Assert.DoesNotContain(Key, hidden, StringComparison.Ordinal);
        Assert.Equal(Key, protector.Unprotect(hidden));
        Assert.Null(protector.Unprotect("not-a-key"));
        Assert.Null(new ErpOperatorAiProtector(new EphemeralDataProtectionProvider()).Unprotect(hidden));
    }

    [Fact]
    public async Task Missing_key_does_not_call_the_tool()
    {
        var gateway = new FakeGateway();
        var dispatcher = new ErpOperatorAiDispatcher(new MemoryStore(), gateway, new FakeLookups());
        var reply = await dispatcher.AskAsync(9, "chatgpt", "explain customer ageing", Today, CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Contains("not connected", reply.Say, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ChatGPT", reply.Say, StringComparison.Ordinal);
        Assert.Empty(gateway.Calls);
        AssertClean(reply.Say);
    }

    [Fact]
    public async Task Chatgpt_answer_can_fill_a_receipt_without_echoing_the_key()
    {
        var store = new MemoryStore();
        var gateway = new FakeGateway();
        var dispatcher = new ErpOperatorAiDispatcher(store, gateway, new FakeLookups
        {
            Rows =
            [
                new ErpMasterOption(41, "customer", "AHM", "Ahmed", ""),
                new ErpMasterOption(7, "account", "CASH", "Main cash", ""),
            ],
        });
        gateway.Reply = call => call.Url.EndsWith("/models", StringComparison.Ordinal)
            ? new ErpAiHttpResult(200, "{\"data\":[]}")
            : new ErpAiHttpResult(200, JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "Prepared. The key was " + Key + "\nERP: create a receipt voucher for customer Ahmed for 1500 from the main cash account",
                        },
                    },
                },
            }));

        var connected = await dispatcher.ConnectAsync(9, "openai", "Bearer " + Key, "", "", "", CancellationToken.None);
        Assert.True(connected.Ok);
        Assert.Contains("chatgpt", connected.Connected);
        Assert.DoesNotContain(Key, connected.Say, StringComparison.Ordinal);
        Assert.Equal("GET", gateway.Calls[0].Method);
        Assert.Equal("https://api.openai.com/v1/models", gateway.Calls[0].Url);
        Assert.Equal(Key, gateway.Calls[0].Bearer);

        var reply = await dispatcher.AskAsync(9, "chatgpt", "create the receipt", Today, CancellationToken.None);
        Assert.True(reply.Ready);
        Assert.Contains("rv_user=41", reply.Href, StringComparison.Ordinal);
        Assert.Contains("rv_account=7", reply.Href, StringComparison.Ordinal);
        Assert.Contains("rv_amount=1500.00", reply.Href, StringComparison.Ordinal);
        Assert.Contains("Nothing is posted by voice alone", reply.Say, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, reply.Say, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.Equal("https://api.openai.com/v1/chat/completions", gateway.Calls[^1].Url);
        AssertClean(reply.Say);
    }

    [Fact]
    public async Task Cursor_starts_an_agent_and_drops_a_foreign_address()
    {
        var gateway = new FakeGateway();
        var dispatcher = new ErpOperatorAiDispatcher(new MemoryStore(), gateway, new FakeLookups());
        gateway.Reply = call => call.Method == "GET"
            ? new ErpAiHttpResult(200, "{}")
            : new ErpAiHttpResult(200, "{\"agent\":{\"id\":\"bc-1\",\"url\":\"https://evil.example/agents/bc-1\"}}");

        var refused = await dispatcher.ConnectAsync(4, "cursor", Key, "", "http://example.com/repo", "", CancellationToken.None);
        Assert.False(refused.Ok);
        Assert.Empty(gateway.Calls);

        gateway.Reply = call => call.Method == "GET"
            ? new ErpAiHttpResult(200, "{}")
            : new ErpAiHttpResult(201, "{\"agent\":{\"id\":\"bc-1\",\"url\":\"https://cursor.com/agents/bc-1\",\"status\":\"ACTIVE\"}}");
        var connected = await dispatcher.ConnectAsync(4, "cursor", Key, "", "https://github.com/acme/erp", "", CancellationToken.None);
        Assert.True(connected.Ok);

        var reply = await dispatcher.AskAsync(4, "cursor", "add a stock check", Today, CancellationToken.None);
        Assert.False(reply.Ready);
        Assert.Equal("https://cursor.com/agents/bc-1", reply.ExternalUrl);
        Assert.Contains("Nothing is posted", reply.Say, StringComparison.Ordinal);
        Assert.Contains("https://github.com/acme/erp", gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.Equal("https://api.cursor.com/v1/agents", gateway.Calls[^1].Url);

        gateway.Reply = _ => new ErpAiHttpResult(200, "{\"agent\":{\"url\":\"https://evil.example/agents/bc-1\"}}");
        var foreign = await dispatcher.AskAsync(4, "cursor", "do something else", Today, CancellationToken.None);
        Assert.Equal(string.Empty, foreign.ExternalUrl);
        Assert.DoesNotContain("evil.example", foreign.Say, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Devin_uses_the_organisation_from_the_account()
    {
        var gateway = new FakeGateway();
        var dispatcher = new ErpOperatorAiDispatcher(new MemoryStore(), gateway, new FakeLookups());
        gateway.Reply = call =>
        {
            if (call.Url.EndsWith("/v3/self", StringComparison.Ordinal))
            {
                return new ErpAiHttpResult(200, "{\"principal_type\":\"service_user\",\"org_id\":\"org_acme\"}");
            }

            return new ErpAiHttpResult(200, "{\"session_id\":\"devin-abc\",\"url\":\"https://app.devin.ai/sessions/devin-abc\",\"status\":\"running\"}");
        };

        var connected = await dispatcher.ConnectAsync(3, "devin", "cog_operator_key_1234567890", "", "", "", CancellationToken.None);
        Assert.True(connected.Ok);
        Assert.Contains("devin", connected.Connected);

        var reply = await dispatcher.AskAsync(3, "devin", "review the sales order page", Today, CancellationToken.None);
        Assert.Equal("https://app.devin.ai/sessions/devin-abc", reply.ExternalUrl);
        Assert.Contains("https://api.devin.ai/v3/organizations/org_acme/sessions", gateway.Calls.Select(call => call.Url));
        Assert.DoesNotContain("cog_operator_key_1234567890", reply.Say, StringComparison.Ordinal);
        Assert.False(reply.Ready);
        AssertClean(reply.Say);
    }

    [Fact]
    public async Task A_short_key_is_refused_before_any_call()
    {
        var gateway = new FakeGateway();
        var dispatcher = new ErpOperatorAiDispatcher(new MemoryStore(), gateway, new FakeLookups());
        var reply = await dispatcher.ConnectAsync(3, "chatgpt", "short", "", "", "", CancellationToken.None);
        Assert.False(reply.Ok);
        Assert.Empty(gateway.Calls);
        AssertClean(reply.Say);
    }

    [Fact]
    public void Brief_names_every_area_and_report_without_stack_words()
    {
        var knowledge = ErpOperatorAiBrief.Knowledge;
        Assert.Contains("Accounts payable", knowledge, StringComparison.Ordinal);
        Assert.Contains("Profit and loss", knowledge, StringComparison.Ordinal);
        Assert.Contains("check the books", knowledge, StringComparison.Ordinal);
        Assert.Contains("does not post money", knowledge, StringComparison.Ordinal);
        foreach (var area in PhpModuleCatalog.ErpAreas)
        {
            Assert.Contains(area.Label, knowledge, StringComparison.Ordinal);
        }

        foreach (var report in ErpReportCenterRegistry.All)
        {
            Assert.Contains(report.Name, knowledge, StringComparison.Ordinal);
        }

        Assert.True(ErpOperatorAiBrief.TryReport("build the profit and loss", out var name, out var href));
        Assert.Equal("Profit and loss", name);
        Assert.Equal("/erp/report-center-app?tab=pl&key=gl_profit_loss", href);
        Assert.True(ErpOperatorAiBrief.TryReport("customer ageing", out _, out var ageing));
        Assert.Contains("key=exec_ar_aging", ageing, StringComparison.Ordinal);
        Assert.False(ErpOperatorAiBrief.TryReport("open purchase orders", out _, out _));
        Assert.Equal("issue", ErpOperatorAiBrief.Kind("identify any issue in the books"));
        Assert.Equal("guide", ErpOperatorAiBrief.Kind("how do I record a customer receipt"));
        Assert.Equal("report", ErpOperatorAiBrief.Kind("United Arab Emirates sales report for last month"));
        AssertClean(knowledge);
        Assert.DoesNotContain("php-reference", knowledge, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chatgpt_opens_a_named_report_and_reads_book_issues()
    {
        var gateway = new FakeGateway();
        var issues = new FakeIssues();
        var dispatcher = new ErpOperatorAiDispatcher(new MemoryStore(), gateway, new FakeLookups(), issues);
        gateway.Reply = call => call.Url.EndsWith("/models", StringComparison.Ordinal)
            ? new ErpAiHttpResult(200, "{\"data\":[]}")
            : new ErpAiHttpResult(200, JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = "The income statement is the profit and loss.\nERP: report Profit and loss" } } },
            }));
        Assert.True((await dispatcher.ConnectAsync(9, "chatgpt", Key, "", "", "", CancellationToken.None)).Ok);

        var report = await dispatcher.AskAsync(9, "chatgpt", "build the profit and loss", Today, CancellationToken.None);
        Assert.True(report.Ready);
        Assert.Equal("/erp/report-center-app?tab=pl&key=gl_profit_loss", report.Href);
        Assert.Contains("Profit and loss", report.Say, StringComparison.Ordinal);
        Assert.Contains("Nothing is posted", report.Say, StringComparison.Ordinal);
        Assert.Contains("Accounts payable", gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.Contains("check the books", gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        AssertClean(report.Say);

        gateway.Reply = _ => new ErpAiHttpResult(200, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "I will look at the books.\nERP: check the books" } } },
        }));
        var found = await dispatcher.AskAsync(9, "chatgpt", "identify any issue in the books", Today, CancellationToken.None);
        Assert.False(found.Ready);
        Assert.Equal("/erp/command-center-app", found.Href);
        Assert.Contains("Active journals whose lines do not balance", found.Say, StringComparison.Ordinal);
        Assert.Contains("difference 12.50", found.Say, StringComparison.Ordinal);
        Assert.DoesNotContain("posted", found.Say, StringComparison.OrdinalIgnoreCase);
        AssertClean(found.Say);
    }

    [Fact]
    public async Task Guide_still_answers_when_chatgpt_is_down_and_cursor_hears_the_issue()
    {
        var gateway = new FakeGateway();
        var issues = new FakeIssues { Text = "Book check is clear. Journals balance, and the customer, supplier, and cash totals match their control accounts." };
        var dispatcher = new ErpOperatorAiDispatcher(new MemoryStore(), gateway, new FakeLookups(), issues);
        gateway.Reply = call => call.Method == "GET"
            ? new ErpAiHttpResult(200, "{\"data\":[]}")
            : new ErpAiHttpResult(503, "{\"error\":{\"message\":\"busy\"}}");
        Assert.True((await dispatcher.ConnectAsync(9, "chatgpt", Key, "", "", "", CancellationToken.None)).Ok);

        var guide = await dispatcher.AskAsync(9, "chatgpt", "how do I record a customer receipt", Today, CancellationToken.None);
        Assert.True(guide.Ok);
        Assert.False(guide.Ready);
        Assert.Contains("customer", guide.Say, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("amount", guide.Say, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/erp/cash-entries-app", guide.Href, StringComparison.Ordinal);
        AssertClean(guide.Say);

        gateway.Calls.Clear();
        gateway.Reply = call => call.Method == "GET"
            ? new ErpAiHttpResult(200, "{}")
            : new ErpAiHttpResult(201, "{\"agent\":{\"id\":\"bc-2\",\"url\":\"https://cursor.com/agents/bc-2\",\"status\":\"ACTIVE\"}}");
        Assert.True((await dispatcher.ConnectAsync(4, "cursor", Key, "", "", "", CancellationToken.None)).Ok);
        var cursor = await dispatcher.AskAsync(4, "cursor", "identify any issue in the books", Today, CancellationToken.None);
        Assert.Equal("https://cursor.com/agents/bc-2", cursor.ExternalUrl);
        Assert.Contains("Book check is clear", cursor.Say, StringComparison.Ordinal);
        Assert.Contains("Nothing is posted", cursor.Say, StringComparison.Ordinal);
        Assert.Contains("Procurement and sourcing", gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.Contains("Book check is clear", gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, gateway.Calls[^1].JsonBody, StringComparison.Ordinal);
        Assert.False(cursor.Ready);
        AssertClean(cursor.Say);
    }

    [Fact]
    public void Voice_panel_offers_the_three_accounts_on_the_existing_bar()
    {
        var root = FindRepoRoot();
        var bar = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpVoiceBar.razor"));
        var chrome = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("data-erp-ai-ajax", bar, StringComparison.Ordinal);
        Assert.Contains("data-erp-ai-tool=\"chatgpt\"", bar, StringComparison.Ordinal);
        Assert.Contains("data-erp-ai-tool=\"cursor\"", bar, StringComparison.Ordinal);
        Assert.Contains("data-erp-ai-tool=\"devin\"", bar, StringComparison.Ordinal);
        Assert.Contains("cursor.com", bar, StringComparison.Ordinal);
        Assert.Contains("app.devin.ai", bar, StringComparison.Ordinal);
        Assert.Contains("ToolsAjaxHref", chrome, StringComparison.Ordinal);
        Assert.Contains("color: #0f172a !important", bar, StringComparison.Ordinal);
        Assert.Contains("-webkit-text-fill-color: #0f172a", bar, StringComparison.Ordinal);
        Assert.Contains("-webkit-text-fill-color: #64748b", bar, StringComparison.Ordinal);
        AssertClean(bar);
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/ErpOperatorAi.cs"));
        Assert.Contains("https://api.openai.com/v1/chat/completions", service, StringComparison.Ordinal);
        Assert.Contains("https://api.cursor.com/v1/agents", service, StringComparison.Ordinal);
        Assert.Contains("https://api.devin.ai/v3/organizations/", service, StringComparison.Ordinal);
        Assert.Contains("does not post money", service, StringComparison.Ordinal);
        Assert.DoesNotContain("php-reference", service, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertClean(string say)
    {
        Assert.DoesNotContain("PHP", say, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", say, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", say, StringComparison.Ordinal);
        Assert.DoesNotContain("php-reference", say, StringComparison.OrdinalIgnoreCase);
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

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class FakeIssues : IErpOperatorIssueScan
    {
        public string Text { get; init; } = "Book check found 1 issue. Active journals whose lines do not balance, difference 12.50.";

        public Task<string> DescribeAsync(CancellationToken cancellationToken)
            => Task.FromResult(Text);
    }

    private sealed class MemoryStore : IErpOperatorAiSecretStore
    {
        private readonly Dictionary<(int User, string Provider), (string Token, string Extras)> _rows = new();

        public Task<IReadOnlyList<string>> ListAsync(int userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(_rows.Where(row => row.Key.User == userId).Select(row => row.Key.Provider).ToArray());

        public Task<ErpOperatorAiSecret> ReadAsync(int userId, string provider, CancellationToken cancellationToken)
            => Task.FromResult(_rows.TryGetValue((userId, provider), out var row)
                ? new ErpOperatorAiSecret(true, true, row.Token, row.Extras)
                : new ErpOperatorAiSecret(false, false, string.Empty, string.Empty));

        public Task<bool> SaveAsync(int userId, string provider, string token, string extras, CancellationToken cancellationToken)
        {
            _rows[(userId, provider)] = (token, extras);
            return Task.FromResult(true);
        }

        public Task RemoveAsync(int userId, string provider, CancellationToken cancellationToken)
        {
            _rows.Remove((userId, provider));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGateway : IErpOperatorAiGateway
    {
        public List<ErpAiHttpCall> Calls { get; } = [];

        public Func<ErpAiHttpCall, ErpAiHttpResult> Reply { get; set; } = _ => new ErpAiHttpResult(200, "{}");

        public Task<ErpAiHttpResult> SendAsync(ErpAiHttpCall call, CancellationToken cancellationToken)
        {
            Calls.Add(call);
            return Task.FromResult(Reply(call));
        }
    }

    private sealed class FakeLookups : IErpMasterLookupReadService
    {
        public IReadOnlyList<ErpMasterOption> Rows { get; init; } = [];

        public Task<ErpMasterLookupResult> SearchAsync(string? kind, string? query, int limit = 80, CancellationToken cancellationToken = default)
        {
            var text = query ?? string.Empty;
            var matched = Rows
                .Where(row => string.Equals(row.Kind, kind, StringComparison.OrdinalIgnoreCase)
                    && (row.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || row.Code.Contains(text, StringComparison.OrdinalIgnoreCase)))
                .Take(limit)
                .ToArray();
            return Task.FromResult(new ErpMasterLookupResult(kind ?? string.Empty, text, matched, matched.Length, "test", string.Empty));
        }
    }
}
