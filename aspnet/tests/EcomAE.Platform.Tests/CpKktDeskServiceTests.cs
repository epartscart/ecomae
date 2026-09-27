using System;
using System.Collections.Generic;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpKktDeskServiceTests
{
    [Fact]
    public void NormaliseSortField_KeepsWhitelistedColumns()
    {
        Assert.Equal("time_created", CpKktDeskService.NormaliseSortField("time_created"));
        Assert.Equal("check_sum", CpKktDeskService.NormaliseSortField("check_sum"));
    }

    [Theory]
    [InlineData("time_created`; DROP TABLE `shop_kkt_checks")]
    [InlineData("unknown_column")]
    [InlineData("")]
    [InlineData(null)]
    public void NormaliseSortField_RejectsEverythingElse(string? field)
        => Assert.Equal("check_id", CpKktDeskService.NormaliseSortField(field));

    [Fact]
    public void BuildFilter_DefaultFilterMatchesEveryCheck()
    {
        var (where, values) = CpKktDeskService.BuildFilter(CpKktCheckFilter.Default);

        Assert.Equal("", where);
        Assert.Empty(values);
    }

    [Fact]
    public void BuildFilter_ParameterisesEqualityAndRangeFields()
    {
        var filter = CpKktCheckFilter.Default with
        {
            CheckId = 14,
            DeviceId = 3,
            TaxationSystem = 1,
            TimeFrom = 100,
            TimeTo = 200,
            SentFlag = 1,
            CorrectionFlag = 0
        };

        var (where, values) = CpKktDeskService.BuildFilter(filter);

        Assert.Contains("`check_id` = ?", where, StringComparison.Ordinal);
        Assert.Contains("`kkt_device_id` = ?", where, StringComparison.Ordinal);
        Assert.Contains("`time_created` > ?", where, StringComparison.Ordinal);
        Assert.Contains("`time_created` < ?", where, StringComparison.Ordinal);
        Assert.Contains("`is_correction_flag` = ?", where, StringComparison.Ordinal);
        Assert.Equal(new object?[] { 14L, 3L, 1L, 100L, 200L, 1L, 0L }, values);
    }

    [Fact]
    public void BuildFilter_UsesLikeForTextFields()
    {
        var filter = CpKktCheckFilter.Default with { CustomerContact = "+971", ProductText = "brake" };

        var (where, values) = CpKktDeskService.BuildFilter(filter);

        Assert.Contains("`customerContact` LIKE ?", where, StringComparison.Ordinal);
        Assert.Contains("`check_product_text` LIKE ?", where, StringComparison.Ordinal);
        Assert.Equal(new object?[] { "%+971%", "%brake%" }, values);
    }

    [Fact]
    public void BuildFilter_OrderLinkageUsesChildCounters()
    {
        var filter = CpKktCheckFilter.Default with { OrderId = 5, OrderItemId = 9 };

        var (where, values) = CpKktDeskService.BuildFilter(filter);

        Assert.Contains("`count_order_item_id` > 0", where, StringComparison.Ordinal);
        Assert.Contains("`count_order_id` > 0", where, StringComparison.Ordinal);
        Assert.Empty(values);
    }

    [Fact]
    public void ParseDefaults_ReadsPhpFieldNames()
    {
        var defaults = CpKktWriteService.ParseDefaults(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = "1",
            ["taxationSystem"] = "0",
            ["kkt_device_id"] = "4",
            ["check_product_tax"] = "6",
            ["check_product_paymentMethodType"] = "4",
            ["check_product_paymentSubjectType"] = "1",
            ["check_payment_type"] = "2",
            ["print"] = "1"
        });

        Assert.Equal(new CpKktDefaults(1, 0, 4, 6, 4, 1, 2, true), defaults);
    }

    [Fact]
    public void ParseDefaults_TreatsMissingCheckboxAsNoPrint()
    {
        var defaults = CpKktWriteService.ParseDefaults(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = "2"
        });

        Assert.Equal(CpKktDefaults.Empty(2), defaults);
    }

    [Fact]
    public void ChecksPage_PageCountRoundsUp()
    {
        var page = new CpKktChecksPage([], 0, CpKktDeskService.PageLimit, 41, "check_id", false);

        Assert.Equal(3, page.PageCount);
    }
}
