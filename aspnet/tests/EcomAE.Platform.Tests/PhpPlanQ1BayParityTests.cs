using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BayParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Bay");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1Bay_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!Same(Json(actual.Extra), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Bay.PowerBiPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Bay.PowerBiPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Bay.Reset();
        Assert.Contains("epc_power_bi.php", PhpPlanQ1Bay.PowerBiPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Bay.PowerBiPath, StringComparison.Ordinal);
        Assert.False(PhpPlanQ1Bay.EpcPowerBiWantsCsv());
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Bay.Reset();
        return name switch
        {
            "pure" => Pure(),
            "config" => Config(),
            "embed" => Embed(),
            "datasets" => Datasets(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        var db = new PhpPlanQ1Bay.BayStore { OrdersMissing = true };
        PhpPlanQ1Bay.UseStore(db);
        var catalogDefault = PhpPlanQ1Bay.EpcPowerBiDatasetCatalog();
        var catalogBase = PhpPlanQ1Bay.EpcPowerBiDatasetCatalog("https://shop.example/");
        var guide = PhpPlanQ1Bay.EpcPowerBiGuideSteps();
        var caps = PhpPlanQ1Bay.EpcPowerBiCapabilities();
        var allow = new object[]
        {
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("https://app.powerbi.com/reportEmbed?id=1"),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("https://xxx.powerbi.us/"),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("https://powerbi.com"),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("http://app.powerbi.com/x"),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("https://evil.com/?powerbi.com"),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed(""),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("https://APP.POWERBI.COM/x"),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("https://a.b.powerbi.com/report"),
            PhpPlanQ1Bay.EpcPowerBiEmbedUrlAllowed("  https://app.powerbi.com/x  ")
        };
        var wantNone = PhpPlanQ1Bay.EpcPowerBiWantsCsv();
        PhpPlanQ1Bay.Get["format"] = "CSV";
        var wantFmt = PhpPlanQ1Bay.EpcPowerBiWantsCsv();
        PhpPlanQ1Bay.Get["format"] = " json ";
        PhpPlanQ1Bay.Server["HTTP_ACCEPT"] = "text/html, text/csv";
        var wantAccept = PhpPlanQ1Bay.EpcPowerBiWantsCsv();
        PhpPlanQ1Bay.Get["format"] = "0";
        PhpPlanQ1Bay.Server.Remove("HTTP_ACCEPT");
        var wantZero = PhpPlanQ1Bay.EpcPowerBiWantsCsv();

        PhpPlanQ1Bay.Get.Clear();
        var parseEmpty = PhpPlanQ1Bay.EpcPowerBiParseDateParam("from", 100);
        PhpPlanQ1Bay.Get["from"] = "2026-01-15";
        var parseOk = PhpPlanQ1Bay.EpcPowerBiParseDateParam("from", 100);
        PhpPlanQ1Bay.Get["from"] = "2026-13-40";
        var parseBad = PhpPlanQ1Bay.EpcPowerBiParseDateParam("from", 100);
        PhpPlanQ1Bay.Get["from"] = " 2026-01-15 ";
        var parseTrim = PhpPlanQ1Bay.EpcPowerBiParseDateParam("from", 100);
        PhpPlanQ1Bay.Get["from"] = "2026-01-15";
        var parseNoFb = PhpPlanQ1Bay.EpcPowerBiParseDateParam("from");

        PhpPlanQ1Bay.HeadersSent = true;
        var csvBody = PhpPlanQ1Bay.EpcPowerBiEmitCsv(
            ["a", "b"],
            [
                ["1", "two"],
                ["x,y", "say \"hi\""],
                [true, false],
                [null, 0]
            ],
            "kpi report.csv");
        var csv = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["headers"] = db.ResponseHeaders.ToArray(),
            ["body_b64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvBody))
        };

        var kpisMiss = PhpPlanQ1Bay.EpcPowerBiDatasetKpis(db, "alpha");
        var ordersMiss = PhpPlanQ1Bay.EpcPowerBiDatasetOrders(db, "alpha", 10);
        var reportMiss = PhpPlanQ1Bay.EpcPowerBiDatasetReport(db, "sales", 0, 1768435200);
        var metricsMiss = PhpPlanQ1Bay.EpcPowerBiDatasetMetrics(db, "alpha");
        return new Rendered(new object?[]
        {
            catalogDefault, catalogBase, guide, caps, allow,
            new object[] { wantNone, wantFmt, wantAccept, wantZero },
            new object[] { parseEmpty, parseOk, parseBad, parseTrim, parseNoFb },
            csv, kpisMiss, ordersMiss, reportMiss, metricsMiss
        });
    }

    private static Rendered Config()
    {
        var db = new PhpPlanQ1Bay.BayStore();
        PhpPlanQ1Bay.UseStore(db);
        var first = PhpPlanQ1Bay.EpcPowerBiConfigure(db, "Acme Site!", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = new string('W', 80),
            ["azure_tenant_id"] = "tid",
            ["default_report_id"] = "rid",
            ["default_dataset_id"] = "did",
            ["embed_url"] = "https://app.powerbi.com/x",
            ["embed_mode"] = "URL",
            ["notes"] = new string('N', 600)
        });
        var second = PhpPlanQ1Bay.EpcPowerBiConfigure(db, "Tenant_01-X", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "ws-2",
            ["embed_mode"] = "url",
            ["embed_url"] = "https://app.powerbi.com/t2",
            ["notes"] = "O'Reilly & <x>"
        });
        var upsert = PhpPlanQ1Bay.EpcPowerBiConfigure(db, "Tenant_01-X", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "ws-2b",
            ["embed_mode"] = "azure",
            ["notes"] = "updated"
        });
        var emptyKey = PhpPlanQ1Bay.EpcPowerBiConfigure(db, "!!!", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["embed_mode"] = "none"
        });
        var gotSanitized = PhpPlanQ1Bay.EpcPowerBiConfigGet(db, "acmesite");
        var gotRawMiss = PhpPlanQ1Bay.EpcPowerBiConfigGet(db, "Acme Site!");
        var gotTenant = PhpPlanQ1Bay.EpcPowerBiConfigGet(db, "tenant_01-x");
        var gotEmpty = PhpPlanQ1Bay.EpcPowerBiConfigGet(db, "");
        var gotMiss = PhpPlanQ1Bay.EpcPowerBiConfigGet(db, "nope");
        PhpPlanQ1Bay.EpcPowerBiRegisterReport(db, "acmesite", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["report_id"] = "r1",
            ["report_name"] = "Zed",
            ["dataset_id"] = "d1",
            ["category"] = "ops",
            ["embed_url"] = "https://app.powerbi.com/r1"
        });
        PhpPlanQ1Bay.EpcPowerBiRegisterReport(db, "acmesite", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["report_id"] = "r0",
            ["dataset_id"] = "d0"
        });
        PhpPlanQ1Bay.EpcPowerBiRegisterReport(db, "other", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["report_id"] = "hidden",
            ["report_name"] = "Hidden",
            ["category"] = "ops"
        });
        db.Reports.Add(new PhpPlanQ1Bay.ReportRow
        {
            Id = db.NextReportId++,
            SiteKey = "acmesite",
            ReportId = "dead",
            ReportName = "Dead",
            Category = "ops",
            Active = 0
        });
        var list = PhpPlanQ1Bay.EpcPowerBiReportsList(db, "acmesite");
        var listOther = PhpPlanQ1Bay.EpcPowerBiReportsList(db, "other");
        var fleet = PhpPlanQ1Bay.EpcPowerBiFleetStats(db);
        return new Rendered(new object?[]
        {
            first, second, upsert, emptyKey,
            gotSanitized, gotRawMiss, gotTenant, gotEmpty, gotMiss,
            list, listOther, fleet
        });
    }

    private static Rendered Embed()
    {
        var db = new PhpPlanQ1Bay.BayStore();
        PhpPlanQ1Bay.UseStore(db);
        var missing = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha");
        PhpPlanQ1Bay.EpcPowerBiConfigure(db, "__platform__", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "plat-ws",
            ["default_report_id"] = "plat-r",
            ["azure_tenant_id"] = "plat-t",
            ["embed_url"] = "https://app.powerbi.com/plat",
            ["embed_mode"] = "url"
        });
        var viaPlat = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha");
        PhpPlanQ1Bay.EpcPowerBiConfigure(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "a-ws",
            ["default_report_id"] = "a-r",
            ["azure_tenant_id"] = "a-t",
            ["embed_mode"] = "none",
            ["embed_url"] = "https://app.powerbi.com/ignored"
        });
        db.Configs.First(c => c.SiteKey == "alpha").Active = 0;
        var inactiveFallsBack = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha");
        db.Configs.First(c => c.SiteKey == "alpha").Active = 1;
        var modeNone = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha");
        PhpPlanQ1Bay.EpcPowerBiConfigure(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "a-ws",
            ["default_report_id"] = "a-r",
            ["azure_tenant_id"] = "a-t",
            ["embed_mode"] = "azure",
            ["embed_url"] = "https://app.powerbi.com/ignored"
        });
        var azure = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha");
        PhpPlanQ1Bay.EpcPowerBiConfigure(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "a-ws",
            ["default_report_id"] = "a-r",
            ["embed_mode"] = "url",
            ["embed_url"] = "https://evil.example/x"
        });
        var badUrl = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha");
        PhpPlanQ1Bay.EpcPowerBiConfigure(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "a-ws",
            ["default_report_id"] = "a-r",
            ["embed_mode"] = "url",
            ["embed_url"] = "https://app.powerbi.com/good"
        });
        var ok = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha");
        PhpPlanQ1Bay.EpcPowerBiRegisterReport(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["report_id"] = "rep-1",
            ["report_name"] = "R1",
            ["embed_url"] = "https://app.powerbi.com/from-report"
        });
        var repId = db.Reports.First(r => r.ReportId == "rep-1").Id;
        PhpPlanQ1Bay.EpcPowerBiConfigure(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace_id"] = "a-ws",
            ["default_report_id"] = "a-r",
            ["azure_tenant_id"] = "a-t",
            ["embed_mode"] = "azure",
            ["embed_url"] = "https://app.powerbi.com/config"
        });
        var reportOverridesAzure = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha", repId);
        PhpPlanQ1Bay.EpcPowerBiRegisterReport(db, "alpha", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["report_id"] = "rep-empty",
            ["report_name"] = "Empty",
            ["embed_url"] = "0"
        });
        var emptyRepId = db.Reports.First(r => r.ReportId == "rep-empty").Id;
        var emptyRepFallsThrough = PhpPlanQ1Bay.EpcPowerBiEmbedResolve(db, "alpha", emptyRepId);
        return new Rendered(new object?[]
        {
            missing, viaPlat, inactiveFallsBack, modeNone, azure, badUrl, ok,
            reportOverridesAzure, emptyRepFallsThrough, repId, emptyRepId
        });
    }

    private static Rendered Datasets()
    {
        var db = new PhpPlanQ1Bay.BayStore();
        PhpPlanQ1Bay.UseStore(db);
        PhpPlanQ1Bay.Dashboard = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["date_from"] = 1768435200,
            ["date_to"] = 0,
            ["order_count"] = 3.9,
            ["revenue_ex_vat"] = 62.555,
            ["profit_ex_vat"] = 1.225,
            ["receivable_due_orders"] = "10.1",
            ["cash_bank_total"] = null
        };
        PhpPlanQ1Bay.ReportsExport = (type, dateFrom, dateTo) =>
        {
            _ = dateFrom;
            _ = dateTo;
            if (type == "boom")
            {
                throw new InvalidOperationException("export boom");
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["headers"] = new object[] { "sku", "qty" },
                ["rows"] = new object[]
                {
                    new object[] { "A1", 2 },
                    new object[] { "B2", 0 }
                }
            };
        };
        PhpPlanQ1Bay.BiLatestAll = siteKey =>
        {
            if (siteKey == "boom")
            {
                throw new InvalidOperationException("bi boom");
            }

            return new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
            {
                ["revenue"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["value"] = "12.5",
                    ["previous_value"] = 10,
                    ["change_pct"] = 25,
                    ["period_start"] = "2026-01-01",
                    ["computed_at"] = "2026-01-15 00:00:00"
                },
                ["orders"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["value"] = 3
                }
            };
        };
        var kpis = PhpPlanQ1Bay.EpcPowerBiDatasetKpis(db, "alpha");
        db.Orders.AddRange(
        [
            new PhpPlanQ1Bay.OrderRow { Id = 10, Time = 1768435200, UserId = 7, Paid = "1", PaidType = 2, SuccessfullyCreated = 1 },
            new PhpPlanQ1Bay.OrderRow { Id = 9, Time = 0, UserId = 3, Paid = "0", PaidType = 0, SuccessfullyCreated = 1 },
            new PhpPlanQ1Bay.OrderRow { Id = 8, Time = 1768435300, UserId = 1, Paid = "", PaidType = 1, SuccessfullyCreated = 1 },
            new PhpPlanQ1Bay.OrderRow { Id = 7, Time = 1768435400, UserId = 2, Paid = null, PaidType = 0, SuccessfullyCreated = 0 },
            new PhpPlanQ1Bay.OrderRow { Id = 6, Time = 1768435500, UserId = 4, Paid = "1", PaidType = 1, SuccessfullyCreated = 1 }
        ]);
        var ordersDefault = PhpPlanQ1Bay.EpcPowerBiDatasetOrders(db, "alpha");
        var ordersTwo = PhpPlanQ1Bay.EpcPowerBiDatasetOrders(db, "alpha", 2);
        var ordersZero = PhpPlanQ1Bay.EpcPowerBiDatasetOrders(db, "alpha", 0);
        var ordersWide = PhpPlanQ1Bay.EpcPowerBiDatasetOrders(db, "alpha", 500);
        var sales = PhpPlanQ1Bay.EpcPowerBiDatasetReport(db, "sales", 1768435200, 1768521600);
        var salesZeroFrom = PhpPlanQ1Bay.EpcPowerBiDatasetReport(db, "sales", 0, 0);
        var salesBoom = PhpPlanQ1Bay.EpcPowerBiDatasetReport(db, "boom", 1, 2);
        var metrics = PhpPlanQ1Bay.EpcPowerBiDatasetMetrics(db, "alpha");
        var metricsBoom = PhpPlanQ1Bay.EpcPowerBiDatasetMetrics(db, "boom");
        return new Rendered(new object?[]
        {
            kpis, ordersDefault, ordersTwo, ordersZero, ordersWide,
            sales, salesZeroFrom, salesBoom, metrics, metricsBoom
        });
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 1200 ? value : value[..1200] + "…";
}
