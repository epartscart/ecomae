using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCreditBlockTests
{
    private static ErpCreditBlock.Position Position(
        decimal permanent = 1000m,
        int permanentTerms = 30,
        decimal temporary = 0m,
        int temporaryTerms = 0,
        long temporaryFrom = 0,
        long temporaryUntil = 0,
        bool onHold = false,
        bool mandatory = false,
        long reviewUntil = 0,
        decimal open = 0m,
        decimal overdue = 0m,
        int oldest = 0,
        decimal invoice = 100m,
        int invoiceTerms = 7)
        => new(
            onHold,
            mandatory,
            permanent,
            permanentTerms,
            reviewUntil,
            temporary,
            temporaryTerms,
            temporaryFrom,
            temporaryUntil,
            open,
            overdue,
            oldest,
            invoice,
            invoiceTerms);

    [Fact]
    public void Permanent_limit_blocks_when_exposure_exceeds_it()
    {
        var decision = ErpCreditBlock.Evaluate(
            Position(permanent: 500m, open: 450m, invoice: 100m),
            ErpCreditBlock.Rules.Defaults,
            asOfUnix: 1_700_000_000);
        Assert.False(decision.Allowed);
        Assert.False(decision.TemporaryActive);
        Assert.Equal(500m, decision.EffectiveLimit);
        Assert.Contains("permanent", decision.Message, StringComparison.Ordinal);
        Assert.StartsWith(ErpCreditBlock.BlockedPrefix, decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Temporary_limit_replaces_the_permanent_limit_inside_its_dates()
    {
        var from = ErpCreditBlock.StartOfUtcDay("2026-10-01");
        var until = ErpCreditBlock.EndOfUtcDay("2026-10-31");
        var asOf = ErpCreditBlock.StartOfUtcDay("2026-10-17");
        var decision = ErpCreditBlock.Evaluate(
            Position(permanent: 5000m, permanentTerms: 30, temporary: 200m, temporaryTerms: 7, temporaryFrom: from, temporaryUntil: until, invoice: 250m, invoiceTerms: 7),
            ErpCreditBlock.Rules.Defaults,
            asOf);
        Assert.True(decision.TemporaryActive);
        Assert.Equal(200m, decision.EffectiveLimit);
        Assert.Equal(7, decision.EffectiveTermsDays);
        Assert.False(decision.Allowed);
        Assert.Contains("temporary", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Expired_temporary_limit_falls_back_to_the_permanent_limit()
    {
        var until = ErpCreditBlock.EndOfUtcDay("2026-10-16");
        var asOf = ErpCreditBlock.StartOfUtcDay("2026-10-17");
        var decision = ErpCreditBlock.Evaluate(
            Position(permanent: 1000m, temporary: 50m, temporaryUntil: until, invoice: 80m),
            ErpCreditBlock.Rules.Defaults,
            asOf);
        Assert.False(decision.TemporaryActive);
        Assert.Equal(1000m, decision.EffectiveLimit);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Named_until_date_is_still_inside_the_temporary_limit()
    {
        var until = ErpCreditBlock.EndOfUtcDay("2026-10-17");
        var decision = ErpCreditBlock.Evaluate(
            Position(permanent: 10m, temporary: 500m, temporaryUntil: until, invoice: 20m),
            ErpCreditBlock.Rules.Defaults,
            until);
        Assert.True(decision.TemporaryActive);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Every_failing_rule_is_reported()
    {
        var asOf = ErpCreditBlock.StartOfUtcDay("2026-10-17");
        var decision = ErpCreditBlock.Evaluate(
            Position(
                permanent: 100m,
                permanentTerms: 7,
                onHold: true,
                reviewUntil: ErpCreditBlock.EndOfUtcDay("2026-10-01"),
                open: 50m,
                overdue: 40m,
                oldest: 10,
                invoice: 80m,
                invoiceTerms: 30),
            ErpCreditBlock.Rules.Defaults,
            asOf);
        Assert.False(decision.Allowed);
        Assert.Contains("on hold", decision.Message, StringComparison.Ordinal);
        Assert.Contains("credit limit", decision.Message, StringComparison.Ordinal);
        Assert.Contains("expired", decision.Message, StringComparison.Ordinal);
        Assert.Contains("days overdue", decision.Message, StringComparison.Ordinal);
        Assert.Contains("Overdue amount", decision.Message, StringComparison.Ordinal);
        Assert.Contains("longer than the allowed", decision.Message, StringComparison.Ordinal);
        Assert.Equal(6, decision.Reasons.Count);
    }

    [Fact]
    public void Grace_days_delay_the_overdue_block()
    {
        var rules = ErpCreditBlock.Rules.Defaults with { DaysThreshold = 5, GraceDays = 3, AccountStatus = false, CreditLimit = false, LimitExpired = false, OverdueAmount = false, TermsOfPayment = false };
        var inside = ErpCreditBlock.Evaluate(Position(oldest: 6), rules, 1);
        var past = ErpCreditBlock.Evaluate(Position(oldest: 8), rules, 1);
        Assert.True(inside.Allowed);
        Assert.False(past.Allowed);
    }

    [Fact]
    public void Mandatory_zero_limit_blocks_a_positive_invoice()
    {
        var decision = ErpCreditBlock.Evaluate(
            Position(permanent: 0m, mandatory: true, invoice: 1m),
            ErpCreditBlock.Rules.Defaults,
            1);
        Assert.False(decision.Allowed);
        Assert.Contains("mandatory", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Zero_limit_without_the_mandatory_flag_is_unlimited()
    {
        var decision = ErpCreditBlock.Evaluate(
            Position(permanent: 0m, invoice: 5000m),
            ErpCreditBlock.Rules.Defaults,
            1);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Disabled_rules_do_not_block()
    {
        var rules = new ErpCreditBlock.Rules(false, false, 100m, 0m, false, false, 1, 0, false, 0.01m, false);
        var decision = ErpCreditBlock.Evaluate(
            Position(onHold: true, overdue: 90m, oldest: 40, invoice: 9999m, invoiceTerms: 90, permanent: 1m, permanentTerms: 1),
            rules,
            1);
        Assert.True(decision.Allowed);
    }

    [Theory]
    [InlineData("Within 7 days", 7)]
    [InlineData("Net 30", 30)]
    [InlineData("", 14)]
    public void Terms_text_uses_the_digits(string text, int expected)
    {
        Assert.Equal(expected, ErpCreditBlock.TermsDaysFromText(text, 14));
    }

    [Fact]
    public void Terms_between_dates_prefer_the_due_date()
    {
        var issue = ErpCreditBlock.StartOfUtcDay("2026-10-01");
        var due = ErpCreditBlock.StartOfUtcDay("2026-10-21");
        Assert.Equal(20, ErpCreditBlock.TermsDaysBetween(issue, due, 7));
        Assert.Equal(7, ErpCreditBlock.TermsDaysBetween(due, issue, 7));
    }

    [Fact]
    public void Invoice_writers_ask_credit_management_before_they_insert()
    {
        var root = RepoRoot();
        var sales = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpSalesInvoiceWriteService.cs"));
        var manual = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpManualInvoiceWriteService.cs"));
        var orders = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpInvoiceFromOrderWriteService.cs"));
        Assert.Contains("IErpCreditManagementService? credit = null", sales, StringComparison.Ordinal);
        Assert.Contains("EnsureAllowsInvoiceAsync", sales, StringComparison.Ordinal);
        Assert.True(sales.IndexOf("EnsureAllowsInvoiceAsync", StringComparison.Ordinal) < sales.IndexOf("BeginTransactionAsync", StringComparison.Ordinal));
        Assert.Contains("IErpCreditManagementService? credit = null", manual, StringComparison.Ordinal);
        Assert.Contains("excludeInvoiceId", manual, StringComparison.Ordinal);
        Assert.Contains("request.Id", manual, StringComparison.Ordinal);
        var draftAt = orders.IndexOf("public async Task<ErpEinvoiceCreateResult> SaveDocumentDraftAsync", StringComparison.Ordinal);
        var draft = orders[draftAt..];
        Assert.True(draft.IndexOf("EnsureAllowsInvoiceAsync", StringComparison.Ordinal) >= 0);
        Assert.True(draft.IndexOf("EnsureAllowsInvoiceAsync", StringComparison.Ordinal) < draft.IndexOf("BeginTransactionAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void Credit_screen_names_the_blocking_factors_and_posts_both_forms()
    {
        var root = RepoRoot();
        var panel = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpCreditManagementPanel.razor"));
        var workspace = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        var routes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpCreditManagementService.cs"));
        var schema = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpCreditSchema.cs"));
        Assert.Contains("ErpCreditManagementPanel", workspace, StringComparison.Ordinal);
        Assert.Contains("Account on hold", panel, StringComparison.Ordinal);
        Assert.Contains("Credit limit expiry", panel, StringComparison.Ordinal);
        Assert.Contains("Days overdue", panel, StringComparison.Ordinal);
        Assert.Contains("Overdue amount", panel, StringComparison.Ordinal);
        Assert.Contains("Payment terms", panel, StringComparison.Ordinal);
        Assert.Contains("/erp/credit-management/profile", panel, StringComparison.Ordinal);
        Assert.Contains("/erp/credit-management/rules", panel, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", panel, StringComparison.Ordinal);
        Assert.Contains("#1f4e79 !important", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("@section", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", panel, StringComparison.Ordinal);
        Assert.Contains("ErpCreditProfileSave = \"/erp/credit-management/profile\"", routes, StringComparison.Ordinal);
        Assert.Contains("ErpCreditRulesSave = \"/erp/credit-management/rules\"", routes, StringComparison.Ordinal);
        Assert.Contains("IErpCreditManagementService, EcomAE.Platform.Erp.ErpCreditManagementService", program, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.Contains("VALUES(`credit_limit`)", service, StringComparison.Ordinal);
        Assert.Contains("epc_credit_block_rules", service, StringComparison.Ordinal);
        Assert.DoesNotContain("permanent_limit", schema, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
