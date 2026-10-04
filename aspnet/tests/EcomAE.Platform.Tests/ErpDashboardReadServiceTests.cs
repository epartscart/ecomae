using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDashboardReadServiceTests
{
    [Fact]
    public void CompletionSqlMatchesPhpOrderStatusBranch()
    {
        var cmp = ErpDashboardReadService.CompletionSql(true, [5, 7], [3], [9]);
        Assert.Equal("`shop_orders`.`status` IN (5,7)", cmp.OrderCompleteExpr);
        Assert.Equal(" AND `status` != 9", cmp.ItemWhereAnd);
        Assert.Equal("1=1 AND `status` != 9", cmp.ItemWherePlain);
        Assert.Equal(" AND `status` IN (3)", cmp.ItemFinishWhere);
    }

    [Fact]
    public void CompletionSqlMatchesPhpItemBranchAndFailSafe()
    {
        Assert.Equal("0", ErpDashboardReadService.CompletionSql(false, [], [], []).OrderCompleteExpr);
        Assert.Equal(" AND 1=0", ErpDashboardReadService.CompletionSql(true, [1], [], []).ItemFinishWhere);
        var cmp = ErpDashboardReadService.CompletionSql(false, [], [3, 4], [9]);
        Assert.Contains("`status` NOT IN (3,4)) = 0)", cmp.OrderCompleteExpr);
        Assert.Contains("WHERE `order_id` = `shop_orders`.`id` AND `status` != 9) > 0", cmp.OrderCompleteExpr);
    }

    [Fact]
    public void OrderSumAndExcludeSqlMatchPhp()
    {
        var cmp = ErpDashboardReadService.CompletionSql(true, [5], [3], []);
        var (sale, purchase, paid) = ErpDashboardReadService.OrderSumSql(cmp);
        Assert.Equal("CAST(IF(`shop_orders`.`status` IN (5), IFNULL((SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` AND `status` IN (3)), 0), 0) AS DECIMAL(20,2))", sale);
        Assert.Contains("SUM(`t2_price_purchase`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` AND 1=1 AND `status` IN (3)", purchase);
        Assert.Contains("`income` = 0 AND `order_id` = `shop_orders`.`id`), 0) - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1", paid);
        Assert.DoesNotContain("1.05", sale);
        Assert.Equal(string.Empty, ErpDashboardReadService.IncompleteOrderExcludeSql(ErpDashboardReadService.CompletionSql(false, [], [], [])));
        Assert.Contains("`purchase_id` IN (SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` > 0 AND `order_id` IN (SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 AND NOT (`shop_orders`.`status` IN (5))))", ErpDashboardReadService.IncompleteOrderExcludeSql(cmp));
    }

    [Fact]
    public void CustomerVatTypeAndDisplayModeMatchPhp()
    {
        Assert.Equal("tax_exempt", ErpDashboardReadService.ResolveVatType("AE", "wholesale", true));
        Assert.Equal("gcc", ErpDashboardReadService.ResolveVatType("SA", "retail", false));
        Assert.Equal("export", ErpDashboardReadService.ResolveVatType("GB", "wholesale", false));
        Assert.Equal("local_b2b", ErpDashboardReadService.ResolveVatType("UAE", "wholesale", false));
        Assert.Equal("local_b2c", ErpDashboardReadService.ResolveVatType("", "retail", false));
        Assert.Equal("inclusive", ErpDashboardReadService.DisplayMode("local_b2c"));
        Assert.Equal("exclusive", ErpDashboardReadService.DisplayMode("gcc"));
        Assert.Equal("inclusive", ErpDashboardReadService.DisplayMode("bogus"));
        Assert.Equal("AE", ErpDashboardReadService.NormalizeCountry("u.a.e."));
    }

    [Fact]
    public void InclusiveSplitNeverDoubleTaxesAndExclusiveGrossesUp()
    {
        var incl = ErpDashboardReadService.LineAmounts(105m, 2m, 5m, true, true);
        Assert.Equal(200m, incl.LineNet);
        Assert.Equal(10m, incl.VatAmount);
        Assert.Equal(210m, incl.Gross);
        var excl = ErpDashboardReadService.LineAmounts(100m, 1m, 5m, false, true);
        Assert.Equal(100m, excl.LineNet);
        Assert.Equal(5m, excl.VatAmount);
        Assert.Equal(105m, excl.Gross);
        var zero = ErpDashboardReadService.LineAmounts(100m, 1m, 0m, true, true);
        Assert.Equal(100m, zero.LineNet);
        Assert.Equal(0m, zero.VatAmount);
        var disabled = ErpDashboardReadService.LineAmounts(105m, 1m, 5m, true, false);
        Assert.Equal(105m, disabled.LineNet);
        Assert.Equal(0m, disabled.VatAmount);
    }

    [Fact]
    public void SupplyRateIsZeroForExportsAndNonAeBuyers()
    {
        Assert.Equal(5m, ErpDashboardReadService.SupplyRate("AE", false, 5m));
        Assert.Equal(0m, ErpDashboardReadService.SupplyRate("AE", true, 5m));
        Assert.Equal(0m, ErpDashboardReadService.SupplyRate("SA", false, 5m));
    }

    [Fact]
    public void DestinationAndCourierFollowHowGetJson()
    {
        Assert.Equal("GB", ErpDashboardReadService.DestinationCountry("{\"country\":\"gb\",\"delivery_price\":\"25.5\"}", "AE"));
        Assert.Equal("SA", ErpDashboardReadService.DestinationCountry("{\"country\":\"UAE\"}", "SA"));
        Assert.Equal("AE", ErpDashboardReadService.DestinationCountry("{\"country_code\":\"ae\"}", "SA"));
        Assert.Equal("SA", ErpDashboardReadService.DestinationCountry("not json", "SA"));
        Assert.Equal(25.5m, ErpDashboardReadService.CourierAmount("{\"country\":\"gb\",\"delivery_price\":\"25.5\"}"));
        Assert.Equal(7m, ErpDashboardReadService.CourierAmount("{\"rate\":7}"));
        Assert.Equal(0m, ErpDashboardReadService.CourierAmount(string.Empty));
    }

    [Fact]
    public void SalesVatOnlyAppliesToRegisteredAeTenant()
    {
        Assert.True(new ErpDashboardReadService.TenantVat("AE", true, true, 5m).SalesEnabled);
        Assert.False(new ErpDashboardReadService.TenantVat("SA", true, true, 15m).SalesEnabled);
        Assert.False(new ErpDashboardReadService.TenantVat("AE", false, true, 5m).SalesEnabled);
        Assert.False(new ErpDashboardReadService.TenantVat("AE", true, false, 5m).SalesEnabled);
    }

    [Fact]
    public void ServiceNeverWritesOrProvisionsSchema()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))) dir = dir.Parent;
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "aspnet/src/EcomAE.Platform/Erp/ErpDashboardReadService.cs"));
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("* 1.05", src);
        foreach (var key in new[] { "\"date_from\"", "\"revenue_ex_vat\"", "\"receivable_due_orders\"", "\"customer_ledger_balance\"", "\"payable_balance\"", "\"cash_bank_total\"", "\"vat_net_status\"", "\"sales_incl_vat\"", "\"kpi_tiles\"", "\"approval_queue\"" })
        {
            Assert.Contains(key, src);
        }

        var module = File.ReadAllText(Path.Combine(dir.FullName, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("MapPost(EcomAeRoutes.ErpAjaxDashboard, HandleDashboardAsync)", module);
        Assert.Contains("LiveWriteFormBinder.Flag(form, \"confirmWrites\", \"confirm_writes\")", module.Substring(module.IndexOf("HandleDashboardAsync(", StringComparison.Ordinal)));
        Assert.Contains("writes = 0, phpAuthoritative = false, validation_code = r.Result.Code, message = r.Result.Message, data = r.Data", module);
    }
}
