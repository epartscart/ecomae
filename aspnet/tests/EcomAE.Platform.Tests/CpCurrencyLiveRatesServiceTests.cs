using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>PHP <c>epc_currency_live_rates.php</c> twin: provider parsing, rate inversion, schedule due-window.</summary>
public sealed class CpCurrencyLiveRatesServiceTests
{
    [Fact]
    public void Routes_exposed_for_every_php_ajax_action()
    {
        Assert.Equal("/cp/currencies/save-rates", EcomAeRoutes.CpCurrenciesSaveRates);
        Assert.Equal("/cp/currencies/live-rates/preview", EcomAeRoutes.CpCurrenciesLivePreview);
        Assert.Equal("/cp/currencies/live-rates/apply", EcomAeRoutes.CpCurrenciesLiveApply);
        Assert.Equal("/cp/currencies/schedule", EcomAeRoutes.CpCurrenciesScheduleGet);
        Assert.Equal("/cp/currencies/schedule-run-now", EcomAeRoutes.CpCurrenciesScheduleRunNow);
    }

    [Fact]
    public void Parses_erapi_v6()
    {
        var bundle = CpCurrencyLiveRatesService.ParseProvider(
            "erapi_v6",
            "AED",
            """{"result":"success","time_last_update_utc":"Mon, 01 Sep 2025 00:00:01 +0000","base_code":"AED","rates":{"AED":1,"USD":0.2723,"EUR":0.2501}}""");
        Assert.True(bundle.Ok, bundle.Error);
        Assert.Equal(0.2723m, bundle.Rates["USD"]);
        Assert.Equal(1m, bundle.Rates["AED"]);
        Assert.NotEqual("", bundle.Date);
    }

    [Fact]
    public void Parses_erapi_v4_and_floatrates()
    {
        var v4 = CpCurrencyLiveRatesService.ParseProvider("erapi_v4", "aed", """{"base":"AED","date":"2025-09-01","rates":{"USD":0.2723}}""");
        Assert.True(v4.Ok, v4.Error);
        Assert.Equal(0.2723m, v4.Rates["USD"]);
        Assert.Equal("2025-09-01", v4.Date);

        var fr = CpCurrencyLiveRatesService.ParseProvider(
            "floatrates",
            "AED",
            """{"usd":{"code":"USD","rate":0.2723,"date":"Mon, 1 Sep 2025 00:00:01 GMT"},"eur":{"code":"EUR","rate":0.2501}}""");
        Assert.True(fr.Ok, fr.Error);
        Assert.Equal(0.2723m, fr.Rates["USD"]);
        Assert.Equal(0.2501m, fr.Rates["EUR"]);
    }

    [Fact]
    public void Rejects_error_payload_and_invalid_json()
    {
        Assert.False(CpCurrencyLiveRatesService.ParseProvider("erapi_v6", "AED", """{"result":"error","error-type":"unsupported-code"}""").Ok);
        Assert.False(CpCurrencyLiveRatesService.ParseProvider("erapi_v4", "AED", "not json").Ok);
        Assert.False(CpCurrencyLiveRatesService.ParseProvider("erapi_v4", "AED", """{"base":"AED","rates":{}}""").Ok);
    }

    [Fact]
    public void ToShopRates_inverts_foreign_per_base_and_forces_main_to_one()
    {
        var shop = CpCurrencyLiveRatesService.ToShopRates(
            new Dictionary<string, decimal> { ["AED"] = 1m, ["USD"] = 0.2723m, ["XXX"] = 0m, ["eur"] = 0.25m },
            "AED");
        Assert.Equal(1m, shop["AED"]);
        Assert.Equal(decimal.Round(1m / 0.2723m, 6, MidpointRounding.AwayFromZero), shop["USD"]);
        Assert.Equal(4m, shop["EUR"]);
        Assert.False(shop.ContainsKey("XXX"));
    }

    [Fact]
    public void DiffPct_matches_php_rounding()
    {
        Assert.Equal(10m, CpCurrencyLiveRatesService.DiffPct(1m, 1.1m));
        Assert.Null(CpCurrencyLiveRatesService.DiffPct(1m, null));
        Assert.Null(CpCurrencyLiveRatesService.DiffPct(0m, 1m));
        Assert.Equal(-0.001m, CpCurrencyLiveRatesService.DiffPct(100m, 99.999m));
    }

    [Fact]
    public void Schedule_due_only_after_hour_and_once_per_local_day()
    {
        var utcNow = new DateTimeOffset(2025, 9, 1, 23, 30, 0, TimeSpan.Zero); // 03:30 Asia/Dubai on 2025-09-02

        var due = CpCurrencyLiveRatesService.BuildSchedule(true, "Asia/Dubai", 2, 0, "", "", "", utcNow);
        Assert.True(due.Due);
        Assert.Equal("2025-09-02", due.LocalDate);
        Assert.Equal("Asia/Dubai", due.Timezone);

        var alreadyRan = CpCurrencyLiveRatesService.BuildSchedule(true, "Asia/Dubai", 2, utcNow.AddMinutes(-30).ToUnixTimeSeconds(), "ok", "p", "", utcNow);
        Assert.False(alreadyRan.Due);

        var tooEarly = CpCurrencyLiveRatesService.BuildSchedule(true, "Asia/Dubai", 5, 0, "", "", "", utcNow);
        Assert.False(tooEarly.Due);

        var disabled = CpCurrencyLiveRatesService.BuildSchedule(false, "Asia/Dubai", 2, 0, "", "", "", utcNow);
        Assert.False(disabled.Due);
    }

    [Fact]
    public void Schedule_falls_back_to_dubai_for_unknown_timezone()
    {
        var s = CpCurrencyLiveRatesService.BuildSchedule(true, "Not/AZone", 2, 0, "", "", "", DateTimeOffset.UtcNow);
        Assert.Equal("Asia/Dubai", s.Timezone);
    }

    [Fact]
    public void NormalizeAlpha_letters_only_uppercase()
    {
        Assert.Equal("AED", CpCurrencyLiveRatesService.NormalizeAlpha(" aed "));
        Assert.Equal("USD", CpCurrencyLiveRatesService.NormalizeAlpha("us-d"));
    }

    [Fact]
    public void Module_gates_live_writes_with_confirm_and_cp_capability()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        foreach (var route in new[] { "CpCurrenciesSaveRates", "CpCurrenciesLivePreview", "CpCurrenciesLiveApply", "CpCurrenciesScheduleGet", "CpCurrenciesScheduleRunNow" })
        {
            Assert.Contains($"EcomAeRoutes.{route}", module, StringComparison.Ordinal);
        }

        Assert.Contains("Set confirmWrites=true to apply live FX rates on ASP.NET.", module, StringComparison.Ordinal);
        Assert.Contains("Set confirmWrites=true to run the nightly FX apply now on ASP.NET.", module, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCurrencyLiveRatesService, EcomAE.Platform.Cp.CpCurrencyLiveRatesService", program, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<EcomAE.Platform.Cp.CpCurrencyFxScheduleHostedService>", program, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "aspnet")) && Directory.Exists(Path.Combine(dir.FullName, "cp")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
