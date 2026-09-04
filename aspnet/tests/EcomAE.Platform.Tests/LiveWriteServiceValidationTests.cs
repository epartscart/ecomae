using System.Data.Common;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveWriteServiceValidationTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    private sealed class ConfiguredNeverOpened : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task Cart_rejects_guest_and_invalid_qty_without_db()
    {
        var guest = await new StorefrontCartWriteService(new UnconfiguredConnections())
            .ChangeCountNeedAsync(0, 10, 2);
        Assert.False(guest.Ok);
        Assert.Equal("auth", guest.Code);

        var missingDb = await new StorefrontCartWriteService(new UnconfiguredConnections())
            .ChangeCountNeedAsync(1, 10, 2);
        Assert.False(missingDb.Ok);
        Assert.Equal("db", missingDb.Code);

        var invalid = await new StorefrontCartWriteService(new ConfiguredNeverOpened())
            .ChangeCountNeedAsync(1, 0, 0);
        Assert.False(invalid.Ok);
        Assert.Equal("invalid", invalid.Code);

        var emptyDelete = await new StorefrontCartWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(1, []);
        Assert.False(emptyDelete.Ok);
        Assert.Equal("invalid", emptyDelete.Code);
    }

    [Fact]
    public async Task Payroll_rejects_invalid_run_before_open()
    {
        var missingDb = await new ErpPayrollWriteService(new UnconfiguredConnections())
            .ApproveRunAsync(9);
        Assert.False(missingDb.Succeeded);
        Assert.Equal("db", missingDb.Code);

        var invalid = await new ErpPayrollWriteService(new ConfiguredNeverOpened())
            .ApproveRunAsync(0);
        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid", invalid.Code);
    }

    [Fact]
    public async Task Oms_credit_po_and_forecast_reject_invalid_input()
    {
        var oms = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SetItemStatusAsync(0, 0, 0, 1);
        Assert.False(oms.Succeeded);
        Assert.Equal("invalid", oms.Code);

        var credit = await new CpCreditLimitWriteService(new ConfiguredNeverOpened())
            .SetLimitAsync("", 0, -1, "AED", 1);
        Assert.False(credit.Succeeded);
        Assert.Equal("invalid", credit.Code);

        var po = await new CpPoApprovalWriteService(new ConfiguredNeverOpened())
            .ApproveAsync(0, 0, 1, "");
        Assert.False(po.Succeeded);
        Assert.Equal("invalid", po.Code);

        var forecast = await new ErpInventoryForecastWriteService(new ConfiguredNeverOpened())
            .RecomputeSkuAsync("", "", 0, "", 7);
        Assert.False(forecast.Succeeded);
        Assert.Equal("invalid", forecast.Code);

        var message = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SendMessageAsync(0, "", 0, 1);
        Assert.False(message.Succeeded);
        Assert.Equal("invalid", message.Code);

        var courier = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .SetCourierAsync(0, -1, null, 1);
        Assert.False(courier.Succeeded);
        Assert.Equal("invalid", courier.Code);

        var delete = await new CpOmsWriteService(new ConfiguredNeverOpened())
            .DeleteUnpaidOrdersAsync([]);
        Assert.False(delete.Succeeded);
        Assert.Equal("invalid", delete.Code);
    }

    [Fact]
    public async Task Checkout_quote_and_garage_reject_before_open()
    {
        var guest = await new StorefrontCheckoutWriteService(new UnconfiguredConnections())
            .CreateAsync(0, new StorefrontCheckoutWriteRequest(1, 1, true));
        Assert.False(guest.Ok);
        Assert.Equal("auth", guest.Code);

        var missingDb = await new StorefrontCheckoutWriteService(new UnconfiguredConnections())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(1, 1, true));
        Assert.False(missingDb.Ok);
        Assert.Equal("db", missingDb.Code);

        var agreement = await new StorefrontCheckoutWriteService(new ConfiguredNeverOpened())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(1, 1, false));
        Assert.False(agreement.Ok);
        Assert.Equal("agreement", agreement.Code);

        var howGet = await new StorefrontCheckoutWriteService(new ConfiguredNeverOpened())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(0, 1, true));
        Assert.False(howGet.Ok);
        Assert.Equal("how_get_missing", howGet.Code);

        var pickup = await new StorefrontCheckoutWriteService(new ConfiguredNeverOpened())
            .CreateAsync(1, new StorefrontCheckoutWriteRequest(1, 0, true));
        Assert.False(pickup.Ok);
        Assert.Equal("office_required", pickup.Code);

        var quoteGuest = await new StorefrontQuoteWriteService(new UnconfiguredConnections())
            .SubmitAsync(0, 9, null);
        Assert.False(quoteGuest.Succeeded);
        Assert.Equal("auth", quoteGuest.Code);

        var quoteInvalid = await new StorefrontQuoteWriteService(new ConfiguredNeverOpened())
            .SubmitAsync(1, 0, null);
        Assert.False(quoteInvalid.Succeeded);
        Assert.Equal("invalid", quoteInvalid.Code);

        var garageInvalid = await new StorefrontGarageWriteService(new ConfiguredNeverOpened())
            .SetActiveAsync(1, 0);
        Assert.False(garageInvalid.Succeeded);
        Assert.Equal("invalid", garageInvalid.Code);

        var garageDelete = await new StorefrontGarageWriteService(new ConfiguredNeverOpened())
            .DeleteAsync(1, 0);
        Assert.False(garageDelete.Succeeded);
        Assert.Equal("invalid", garageDelete.Code);
    }
}
