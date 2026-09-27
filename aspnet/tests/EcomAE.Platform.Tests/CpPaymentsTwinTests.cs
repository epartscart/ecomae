using System.Text.Json;
using EcomAE.Platform.Cp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPaymentsTwinTests
{
    [Fact]
    public void Catalogue_mirrors_php_gateway_set()
    {
        Assert.True(PhpPaymentGatewayCatalog.Definitions.ContainsKey("stripe"));
        Assert.True(PhpPaymentGatewayCatalog.Definitions.ContainsKey("nowpayments"));
        Assert.True(PhpPaymentGatewayCatalog.Definitions.ContainsKey("jazzcash"));
        Assert.Equal("Crypto (NOWPayments)", PhpPaymentGatewayCatalog.Title("nowpayments"));
        Assert.Equal("epc_pay_stripe", PhpPaymentGatewayCatalog.NameKey("stripe"));
        Assert.Equal("epc_pay_stripe_desc", PhpPaymentGatewayCatalog.DescriptionKey("stripe"));
        Assert.Equal(PhpPaymentGatewayCatalog.RegionPakistan, PhpPaymentGatewayCatalog.Region("jazzcash"));
        Assert.Equal(PhpPaymentGatewayCatalog.RegionCrypto, PhpPaymentGatewayCatalog.Region("nowpayments"));
        Assert.Equal(PhpPaymentGatewayCatalog.RegionLegacy, PhpPaymentGatewayCatalog.Region("unknown_handler"));
        Assert.True(PhpPaymentGatewayCatalog.IsModern("telr"));
        Assert.False(PhpPaymentGatewayCatalog.IsModern("robokassa"));
        Assert.Equal("Robokassa", PhpPaymentGatewayCatalog.Title("robokassa"));
    }

    [Fact]
    public void Region_labels_follow_php_order()
    {
        Assert.Equal(
            new[] { "gcc", "pakistan", "crypto", "international", "legacy" },
            PhpPaymentGatewayCatalog.RegionLabels.Select(pair => pair.Key).ToArray());
        Assert.Equal("GCC & MENA", PhpPaymentGatewayCatalog.RegionLabel("gcc"));
    }

    [Fact]
    public void Account_input_is_normalised_like_php()
    {
        Assert.Equal("platform", CpPaymentsWriteService.NormaliseOwnerType("PLATFORM!"));
        Assert.Equal("office", CpPaymentsWriteService.NormaliseOwnerType("office"));
        Assert.Equal("vendor", CpPaymentsWriteService.NormaliseOwnerType("vendor "));
        Assert.Equal("platform", CpPaymentsWriteService.NormaliseOwnerType("Vendor"));
        Assert.Equal("direct", CpPaymentsWriteService.NormaliseMode("nonsense"));
        Assert.Equal("payout", CpPaymentsWriteService.NormaliseMode("payout"));
        Assert.Equal("connected", CpPaymentsWriteService.NormaliseMode("connected"));
        Assert.Equal("active", CpPaymentsWriteService.NormaliseStatus(""));
        Assert.Equal("disabled", CpPaymentsWriteService.NormaliseStatus("disabled"));
        Assert.True(CpPaymentsWriteService.IsValidJson("{\"a\":1}"));
        Assert.False(CpPaymentsWriteService.IsValidJson("{oops"));
        Assert.False(CpPaymentsWriteService.IsValidJson(""));
    }

    [Fact]
    public void Form_binder_collects_configure_parameters()
    {
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["param_secret_key"] = "sk_test",
            ["param_demo_mode"] = "1",
            ["unrelated"] = "x",
        });

        var json = CpPaymentsFormBinder.ParametersValues(form);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("sk_test", parsed.RootElement.GetProperty("secret_key").GetString());
        Assert.Equal("1", parsed.RootElement.GetProperty("demo_mode").GetString());
        Assert.False(parsed.RootElement.TryGetProperty("unrelated", out _));
    }

    [Fact]
    public void Form_binder_maps_owner_and_account_fields()
    {
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["owner_type"] = "vendor",
            ["vendor_id"] = "42",
            ["office_id"] = "7",
            ["title"] = "Vendor payouts",
            ["handler"] = "stripe",
            ["mode"] = "payout",
            ["platform_fee_pct"] = "2.5",
            ["status"] = "active",
            ["demo_mode"] = "on",
        });

        var account = CpPaymentsFormBinder.Account(form);
        Assert.Equal("vendor", account.OwnerType);
        Assert.Equal(42, account.OwnerId);
        Assert.Equal("payout", account.Mode);
        Assert.Equal(2.5m, account.PlatformFeePct);
        Assert.True(account.DemoMode);
        Assert.False(account.IsDefault);
        Assert.Equal("{}", account.CredentialsJson);
    }

    [Fact]
    public void Desk_groups_gateways_by_region()
    {
        var gateway = new CpPaymentGateway(
            1, "Stripe", "stripe", "desc", true, true,
            PhpPaymentGatewayCatalog.RegionInternational,
            [],
            new Dictionary<string, string> { ["demo_mode"] = "1" });
        var desk = new CpPaymentsDesk(true, "", [gateway], [], [], [], []);

        Assert.Same(gateway, desk.ActiveGateway);
        Assert.Equal(1, desk.CountInRegion(PhpPaymentGatewayCatalog.RegionInternational));
        Assert.Empty(desk.InRegion(PhpPaymentGatewayCatalog.RegionCrypto));
        Assert.True(gateway.DemoMode);
        Assert.Equal("Stripe", gateway.DisplayName);
        Assert.False(CpPaymentsDesk.Unavailable("no db").Available);
    }
}
