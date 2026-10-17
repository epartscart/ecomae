using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCreditApplicationTests
{
    private static readonly long AsOf = ErpCreditBlock.StartOfUtcDay("2026-10-17");

    [Fact]
    public void Missing_application_blocks()
    {
        var gaps = ErpCreditApplication.Gaps(null, AsOf);
        Assert.Contains("credit application", gaps[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Company_requires_a_trade_license_and_each_partner_separately()
    {
        var file = Company(
            PartnerPaper(1, "Amina", ErpCreditApplication.Passport),
            PartnerPaper(1, "Amina", ErpCreditApplication.Visa),
            PartnerPaper(1, "Amina", ErpCreditApplication.EmiratesId),
            PartnerPaper(2, "Rashid", ErpCreditApplication.Passport),
            PartnerPaper(2, "Rashid", ErpCreditApplication.EmiratesId));
        var gaps = ErpCreditApplication.Gaps(file, AsOf);
        Assert.Contains(gaps, gap => gap.Contains("Trade license", StringComparison.Ordinal) && gap.Contains("not attached", StringComparison.Ordinal));
        Assert.Contains(gaps, gap => gap.Contains("Visa for Rashid", StringComparison.Ordinal));
        Assert.DoesNotContain(gaps, gap => gap.Contains("Visa for Amina", StringComparison.Ordinal));
        Assert.DoesNotContain(gaps, gap => gap.Contains("Passport for Rashid", StringComparison.Ordinal));
    }

    [Fact]
    public void Individual_uses_passport_visa_and_emirates_id_without_a_trade_license()
    {
        var file = new ErpCreditApplication.File(
            true,
            ErpCreditApplication.Individual,
            ErpCreditBlock.EndOfUtcDay("2026-12-01"),
            [new ErpCreditApplication.Partner(9, "Ignored")],
            [
                Paper(0, ErpCreditApplication.Passport),
                Paper(0, ErpCreditApplication.Visa),
            ]);
        var gaps = ErpCreditApplication.Gaps(file, AsOf);
        Assert.Contains(gaps, gap => gap.Contains("Emirates ID", StringComparison.Ordinal) && gap.Contains("not attached", StringComparison.Ordinal));
        Assert.DoesNotContain(gaps, gap => gap.Contains("Trade license", StringComparison.Ordinal));
        Assert.DoesNotContain(gaps, gap => gap.Contains("Ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void Expired_and_inside_one_month_both_block()
    {
        var expired = ErpCreditApplication.ExpiryReason("Passport", ErpCreditBlock.EndOfUtcDay("2026-10-01"), AsOf);
        var soon = ErpCreditApplication.ExpiryReason("Visa", ErpCreditBlock.EndOfUtcDay("2026-11-16"), AsOf);
        var clear = ErpCreditApplication.ExpiryReason("Emirates ID", ErpCreditBlock.EndOfUtcDay("2026-11-17"), AsOf);
        Assert.Contains("expired on 2026-10-01", expired, StringComparison.Ordinal);
        Assert.Contains("inside one month", soon, StringComparison.Ordinal);
        Assert.Null(clear);
    }

    [Fact]
    public void Application_period_blocks_when_it_is_missing_or_due()
    {
        var missing = new ErpCreditApplication.File(true, ErpCreditApplication.Individual, 0, [], PersonPapers("2026-12-01"));
        var due = new ErpCreditApplication.File(true, ErpCreditApplication.Individual, ErpCreditBlock.EndOfUtcDay("2026-11-01"), [], PersonPapers("2026-12-01"));
        Assert.Contains(ErpCreditApplication.Gaps(missing, AsOf), gap => gap.Contains("no expiry date", StringComparison.Ordinal));
        Assert.Contains(ErpCreditApplication.Gaps(due, AsOf), gap => gap.Contains("credit application", StringComparison.OrdinalIgnoreCase) && gap.Contains("inside one month", StringComparison.Ordinal));
    }

    [Fact]
    public void Current_papers_leave_the_invoice_allowed()
    {
        var file = new ErpCreditApplication.File(
            true,
            ErpCreditApplication.Company,
            ErpCreditBlock.EndOfUtcDay("2026-12-01"),
            [new ErpCreditApplication.Partner(1, "Amina")],
            [
                Paper(0, ErpCreditApplication.TradeLicense),
                PartnerPaper(1, "Amina", ErpCreditApplication.Passport),
                PartnerPaper(1, "Amina", ErpCreditApplication.Visa),
                PartnerPaper(1, "Amina", ErpCreditApplication.EmiratesId),
            ]);
        var credit = ErpCreditBlock.Evaluate(ClearPosition(), ErpCreditBlock.Rules.Defaults, AsOf);
        var decision = ErpCreditApplication.Apply(credit, documentsRule: true, file, AsOf);
        Assert.True(decision.Allowed);
        Assert.Empty(ErpCreditApplication.Gaps(file, AsOf));
    }

    [Fact]
    public void Document_rule_joins_the_credit_block_and_can_be_turned_off()
    {
        var credit = ErpCreditBlock.Evaluate(ClearPosition(), ErpCreditBlock.Rules.Defaults, AsOf);
        var blocked = ErpCreditApplication.Apply(credit, documentsRule: true, null, AsOf);
        var skipped = ErpCreditApplication.Apply(credit, documentsRule: false, null, AsOf);
        Assert.False(blocked.Allowed);
        Assert.StartsWith(ErpCreditBlock.BlockedPrefix, blocked.Message, StringComparison.Ordinal);
        Assert.Contains("credit application", blocked.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(skipped.Allowed);
        Assert.True(ErpCreditBlock.Rules.Defaults.Documents);
    }

    [Fact]
    public void Screen_and_invoice_check_use_the_application()
    {
        var root = RepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpCreditManagementService.cs"));
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpCreditApplicationPanel.razor"));
        var workspace = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        Assert.Contains("ErpCreditApplication.Apply", service, StringComparison.Ordinal);
        Assert.Contains("block_documents", service, StringComparison.Ordinal);
        Assert.Contains("Trade license", page, StringComparison.Ordinal);
        Assert.Contains("Emirates ID", page, StringComparison.Ordinal);
        Assert.Contains("epc-credit-app", page, StringComparison.Ordinal);
        Assert.Contains("ErpCreditApplicationPanel", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", page, StringComparison.Ordinal);
    }

    private static ErpCreditApplication.File Company(params ErpCreditApplication.Paper[] papers)
        => new(
            true,
            ErpCreditApplication.Company,
            ErpCreditBlock.EndOfUtcDay("2026-12-01"),
            [new ErpCreditApplication.Partner(1, "Amina"), new ErpCreditApplication.Partner(2, "Rashid")],
            papers);

    private static ErpCreditApplication.Paper PartnerPaper(long partnerId, string name, string kind)
        => Paper(partnerId, kind);

    private static ErpCreditApplication.Paper Paper(long partnerId, string kind)
        => new(partnerId, kind, "AE-100", ErpCreditBlock.EndOfUtcDay("2026-12-01"), true);

    private static ErpCreditApplication.Paper[] PersonPapers(string day)
        =>
        [
            new(0, ErpCreditApplication.Passport, "P1", ErpCreditBlock.EndOfUtcDay(day), true),
            new(0, ErpCreditApplication.Visa, "V1", ErpCreditBlock.EndOfUtcDay(day), true),
            new(0, ErpCreditApplication.EmiratesId, "E1", ErpCreditBlock.EndOfUtcDay(day), true),
        ];

    private static ErpCreditBlock.Position ClearPosition()
        => new(false, false, 1000m, 30, 0, 0m, 0, 0, 0, 0m, 0m, 0, 100m, 7);

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
