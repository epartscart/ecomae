using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpVoiceCommandTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    [Fact]
    public void Receipt_sentence_keeps_customer_account_and_amount()
    {
        var draft = ErpVoiceInterpreter.Interpret(
            "create a receipt voucher for customer Ahmed for 1500.00 from the main cash account",
            Today);

        Assert.Equal("receipt", draft.Intent);
        Assert.Equal("Ahmed", draft.Slots["customer"]);
        Assert.Equal("Main Cash", draft.Slots["account"]);
        Assert.Equal("1500.00", draft.Slots["amount"]);
        Assert.Empty(draft.Missing);
        Assert.True(draft.Ready);
        Assert.DoesNotContain("PHP", draft.Say, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", draft.Say, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", draft.Say, StringComparison.Ordinal);
    }

    [Fact]
    public void Receipt_without_amount_stays_incomplete()
    {
        var draft = ErpVoiceInterpreter.Interpret(
            "create a receipt voucher for customer Ahmed from the main cash account",
            Today);

        Assert.Contains("amount", draft.Missing);
        Assert.False(draft.Ready);
    }

    [Fact]
    public void Receipt_requirements_name_the_fields()
    {
        var draft = ErpVoiceInterpreter.Interpret("what do I need for a receipt voucher", Today);

        Assert.Equal("requirements", draft.Intent);
        Assert.Contains("customer", draft.Say, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("account", draft.Say, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("amount", draft.Say, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/erp/cash-entries-app#receipt-voucher", draft.Href, StringComparison.Ordinal);
        AssertClean(draft.Say);
    }

    [Fact]
    public void Spoken_amount_and_payment_supplier_are_read()
    {
        var spoken = ErpVoiceInterpreter.Interpret(
            "receipt voucher for customer Ahmed amount one thousand five hundred account main cash",
            Today);
        Assert.Equal("1500.00", spoken.Slots["amount"]);

        var payment = ErpVoiceInterpreter.Interpret(
            "payment voucher for supplier Gulf Parts amount 800 account operating bank bill 12 purchase 9",
            Today);
        Assert.Equal("payment", payment.Intent);
        Assert.Equal("Gulf Parts", payment.Slots["supplier"]);
        Assert.Equal("Operating Bank", payment.Slots["account"]);
        Assert.Equal("800.00", payment.Slots["amount"]);
        Assert.Equal("12", payment.Slots["bill"]);
        Assert.Equal("9", payment.Slots["purchase"]);
        Assert.Empty(payment.Missing);
    }

    [Fact]
    public void Uae_sales_report_uses_country_period_and_detail()
    {
        var draft = ErpVoiceInterpreter.Interpret(
            "United Arab Emirates sales report for last month by customer",
            Today);

        Assert.Equal("AE", draft.Slots["country"]);
        Assert.Equal("customer", draft.Slots["detail"]);
        Assert.Equal("2026-09-01", draft.Slots["from"]);
        Assert.Equal("2026-09-30", draft.Slots["to"]);
        Assert.Contains("/erp/sales-workspace-app", draft.Href, StringComparison.Ordinal);
        Assert.Contains("tab=sm_performance", draft.Href, StringComparison.Ordinal);
        Assert.Contains("country=AE", draft.Href, StringComparison.Ordinal);
        Assert.True(draft.Ready);

        var period = ErpVoiceInterpreter.FindPeriod("last month", Today);
        Assert.Equal(new DateOnly(2026, 9, 1), period.From);
        Assert.Equal(new DateOnly(2026, 9, 30), period.To);

        var byCode = ErpVoiceInterpreter.Interpret("sales report for AE last month by item", Today);
        Assert.Equal("AE", byCode.Slots["country"]);
        Assert.Equal("item", byCode.Slots["detail"]);
    }

    [Fact]
    public void Country_aliases_match_the_stored_profile()
    {
        Assert.True(ErpVoiceInterpreter.CountryMatches("AE", "UAE"));
        Assert.True(ErpVoiceInterpreter.CountryMatches("UAE", "United Arab Emirates"));
        Assert.True(ErpVoiceInterpreter.CountryMatches("AE", "AE"));
        Assert.False(ErpVoiceInterpreter.CountryMatches("SA", "UAE"));
        Assert.Contains("united arab emirates", ErpVoiceInterpreter.CountryAliases("AE"));
    }

    [Fact]
    public void Open_commands_reach_the_existing_pages()
    {
        var purchase = ErpVoiceInterpreter.Interpret("open purchase orders", Today);
        Assert.Contains("purchase-orders-app", purchase.Href, StringComparison.Ordinal);
        Assert.True(purchase.Ready);

        var sales = ErpVoiceInterpreter.Interpret("open sales orders", Today);
        Assert.Contains("sales-orders-app", sales.Href, StringComparison.Ordinal);

        var cash = ErpVoiceInterpreter.Interpret("open cash entries", Today);
        Assert.Contains("cash-entries-app", cash.Href, StringComparison.Ordinal);
        AssertClean(purchase.Say);
        AssertClean(sales.Say);
    }

    [Fact]
    public async Task Prepare_resolves_masters_and_refuses_an_ambiguous_name()
    {
        var lookups = new FakeLookups
        {
            Rows =
            [
                new ErpMasterOption(41, "customer", "AHM", "Ahmed", ""),
                new ErpMasterOption(7, "account", "CASH", "Main cash", ""),
            ],
        };
        var ready = await new ErpVoicePrepareService(lookups).PrepareAsync(
            "create a receipt voucher for customer Ahmed for 1500 from the main cash account",
            Today);

        Assert.True(ready.Ready);
        Assert.Empty(ready.Missing);
        Assert.Equal("41", ready.Slots["user_id"]);
        Assert.Equal("7", ready.Slots["account_id"]);
        Assert.Contains("rv_user=41", ready.Href, StringComparison.Ordinal);
        Assert.Contains("rv_account=7", ready.Href, StringComparison.Ordinal);
        Assert.Contains("rv_amount=1500.00", ready.Href, StringComparison.Ordinal);
        Assert.Contains("#receipt-voucher", ready.Href, StringComparison.Ordinal);
        Assert.DoesNotContain("account_id=", ready.Href, StringComparison.Ordinal);
        Assert.Contains("Nothing is posted by voice alone", ready.Say, StringComparison.Ordinal);

        var ambiguous = await new ErpVoicePrepareService(new FakeLookups
        {
            Rows =
            [
                new ErpMasterOption(1, "customer", "A1", "Ahmed Trading", ""),
                new ErpMasterOption(2, "customer", "A2", "Ahmed Parts", ""),
                new ErpMasterOption(7, "account", "CASH", "Main cash", ""),
            ],
        }).PrepareAsync(
            "create a receipt voucher for customer Ahmed for 1500 from the main cash account",
            Today);

        Assert.False(ambiguous.Ready);
        Assert.Contains("customer", ambiguous.Missing);
        Assert.DoesNotContain("user_id", ambiguous.Slots.Keys);
    }

    private static void AssertClean(string say)
    {
        Assert.DoesNotContain("PHP", say, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", say, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", say, StringComparison.Ordinal);
        Assert.DoesNotContain("php-reference", say, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeLookups : IErpMasterLookupReadService
    {
        public IReadOnlyList<ErpMasterOption> Rows { get; init; } = [];

        public Task<ErpMasterLookupResult> SearchAsync(
            string? kind,
            string? query,
            int limit = 80,
            CancellationToken cancellationToken = default)
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
