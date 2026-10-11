using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1WorkParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Work");

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
    public void PlanQ1Work_MatchPhpGolden()
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
            if (!Same(actual, expected))
            {
                failures.Add(name + " expected=" + Truncate(expected.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Work.OrdersWorkspacePath,
                PhpPlanQ1Work.MarketingHelpersPath,
                PhpPlanQ1Work.BreadcrumbPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Work.OrdersWorkspacePath,
                PhpPlanQ1Work.MarketingHelpersPath,
                PhpPlanQ1Work.BreadcrumbPath
            });

    [Fact]
    public void TabFromCookie_DoesNotStartASession()
    {
        PhpPlanQ1Work.Reset();
        Assert.Equal("open", PhpPlanQ1Work.EpcOrdersWsTabFromCookie());
    }

    private static string Render(string name)
    {
        PhpPlanQ1Work.Reset();
        PhpPlanQ1Work.Translate = PhpPlanQ1Work.TranslateStub;
        return name switch
        {
            "ws_static" => Json(WsStatic()),
            "ws_db" => Json(WsDb()),
            "mkt_pure" => Json(MktPure()),
            "mkt_db" => Json(MktDb()),
            "crumb" => Json(Crumb()),
            _ => "unknown:" + name
        };
    }

    private static object?[] WsStatic()
    {
        PhpPlanQ1Work.OrdersTabCookie = null;
        var tab1 = PhpPlanQ1Work.EpcOrdersWsTabFromCookie();
        PhpPlanQ1Work.OrdersTabCookie = "Completed";
        var tab2 = PhpPlanQ1Work.EpcOrdersWsTabFromCookie();
        PhpPlanQ1Work.OrdersTabCookie = "nope";
        var tab3 = PhpPlanQ1Work.EpcOrdersWsTabFromCookie();
        var n1 = PhpPlanQ1Work.EpcOrdersWsNormalizeFilterForTab(new Dictionary<string, object?>(StringComparer.Ordinal), "open", new[] { 1, 2 }, new[] { 9 }, true);
        var n2 = PhpPlanQ1Work.EpcOrdersWsNormalizeFilterForTab(new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = 5, ["paid"] = 1 }, "open", new[] { 1, 2 }, new[] { 9 }, false);
        var n3 = PhpPlanQ1Work.EpcOrdersWsNormalizeFilterForTab(new Dictionary<string, object?>(StringComparer.Ordinal) { ["paid"] = 1 }, "completed", new[] { 1 }, new[] { 8, 9 }, true);
        var n4 = PhpPlanQ1Work.EpcOrdersWsNormalizeFilterForTab(new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = 3 }, "all", new[] { 1 }, new[] { 9 }, true);
        return new object?[]
        {
            PhpPlanQ1Work.EpcOrdersWsH("a&b<'x'>"),
            PhpPlanQ1Work.EpcOrdersWsStorageLabel(""),
            PhpPlanQ1Work.EpcOrdersWsStorageLabel(" S-UAE "),
            PhpPlanQ1Work.EpcOrdersWsStorageLabel("12"),
            PhpPlanQ1Work.EpcOrdersWsStorageLabel("99"),
            PhpPlanQ1Work.EpcOrdersWsAedUsd(367.25, 3.6725),
            PhpPlanQ1Work.EpcOrdersWsAedUsd(10, 0),
            tab1, tab2, tab3,
            PhpPlanQ1Work.EpcOrdersWsFilterHasSearch(new Dictionary<string, object?>(StringComparer.Ordinal)),
            PhpPlanQ1Work.EpcOrdersWsFilterHasSearch(new Dictionary<string, object?>(StringComparer.Ordinal) { ["order_id"] = "", ["customer"] = "0" }),
            PhpPlanQ1Work.EpcOrdersWsFilterHasSearch(new Dictionary<string, object?>(StringComparer.Ordinal) { ["phone"] = "05" }),
            n1["status"], n1["paid"], n2["status"], n2["paid"], n3["status"], n3["paid"], n4["status"],
            PhpPlanQ1Work.EpcOrdersWsUsdRate(null, null)
        };
    }

    private static object?[] WsDb()
    {
        var db = new PhpPlanQ1Work.WsStore();
        db.Statuses.AddRange(new[]
        {
            new PhpPlanQ1Work.WsStatusRow { Id = 1, ForCreated = 1, Name = "10" },
            new PhpPlanQ1Work.WsStatusRow { Id = 2, Name = "20" },
            new PhpPlanQ1Work.WsStatusRow { Id = 3, ForFinish = 1, Name = "30" },
            new PhpPlanQ1Work.WsStatusRow { Id = 4, ForInverse = 1, Name = "40" }
        });
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.Zero).UtcDateTime.Date;
        var todayStart = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeSeconds();
        PhpPlanQ1Work.TodayStart = () => todayStart;
        db.Orders.AddRange(new[]
        {
            new PhpPlanQ1Work.WsOrderRow { OfficeId = 7, Status = 1, Time = todayStart + 60, Paid = 0 },
            new PhpPlanQ1Work.WsOrderRow { OfficeId = 7, Status = 2, Time = todayStart + 120, Paid = 1 },
            new PhpPlanQ1Work.WsOrderRow { OfficeId = 7, Status = 3, Time = todayStart - 86400, Paid = 1 },
            new PhpPlanQ1Work.WsOrderRow { OfficeId = 8, Status = 2, Time = todayStart + 10, Paid = 2 }
        });
        var statuses = new Dictionary<int, IReadOnlyDictionary<string, object?>>
        {
            [2] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "20" }
        };
        PhpPlanQ1Work.CurrencyRecords = () => new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["840"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["rate"] = 2.0 }
        };
        return new object?[]
        {
            PhpPlanQ1Work.EpcOrdersWsOpenStatusIds(db),
            PhpPlanQ1Work.EpcOrdersWsInProcessStatusIds(db),
            PhpPlanQ1Work.EpcOrdersWsCompletedStatusIds(db),
            PhpPlanQ1Work.EpcOrdersWsBadgeClass(4, db),
            PhpPlanQ1Work.EpcOrdersWsBadgeClass(3, db),
            PhpPlanQ1Work.EpcOrdersWsBadgeClass(1, db),
            PhpPlanQ1Work.EpcOrdersWsBadgeClass(2, db),
            PhpPlanQ1Work.EpcOrdersWsBadgeClass(99, db),
            PhpPlanQ1Work.EpcOrdersWsStatusBadge(2, statuses, db),
            PhpPlanQ1Work.EpcOrdersWsPaidBadge(0),
            PhpPlanQ1Work.EpcOrdersWsPaidBadge(1),
            PhpPlanQ1Work.EpcOrdersWsPaidBadge(2),
            PhpPlanQ1Work.EpcOrdersWsKpi(db, new Dictionary<int, string> { [7] = "A", [8] = "B" }, 9),
            PhpPlanQ1Work.EpcOrdersWsKpi(db, new Dictionary<int, string>(), 9),
            PhpPlanQ1Work.EpcOrdersWsCountByStatuses(db, new[] { 7, 8 }, new[] { 2 }),
            PhpPlanQ1Work.EpcOrdersWsCountByStatuses(db, Array.Empty<int>(), new[] { 2 }),
            PhpPlanQ1Work.EpcOrdersWsUsdRate(db, new object())
        };
    }

    private static object?[] MktPure()
    {
        var strategies = PhpPlanQ1Work.DefaultStrategies();
        return new object?[]
        {
            PhpPlanQ1Work.EpcMarketingH("a&b<'x'>"),
            PhpPlanQ1Work.EpcMarketingResolveLink("https://x.test/a", "cp", "https://shop.test"),
            PhpPlanQ1Work.EpcMarketingResolveLink("/cp/shop/x", "ops", "https://shop.test/"),
            PhpPlanQ1Work.EpcMarketingResolveLink("/en/about", "cp", "https://shop.test"),
            PhpPlanQ1Work.EpcMarketingResolveLink("relative", "cp", "https://shop.test"),
            PhpPlanQ1Work.EpcMarketingCompletionStats(strategies, new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)),
            PhpPlanQ1Work.EpcMarketingCompletionStats(
                strategies,
                new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
                {
                    ["measurement"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["gsc_verify"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["done"] = true },
                        ["ga_conversions"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["done"] = 1 }
                    }
                })
        };
    }

    private static object?[] MktDb()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PhpPlanQ1Work.Clock = () => now;
        var db = new PhpPlanQ1Work.MktStore();
        var none = PhpPlanQ1Work.EpcMarketingTableExists(db, "shop_orders");
        db.Tables.Add("shop_orders");
        db.Tables.Add("users");
        db.Orders.Add(new PhpPlanQ1Work.MktOrderRow { SuccessfullyCreated = 1, Time = now - 3600 });
        db.Orders.Add(new PhpPlanQ1Work.MktOrderRow { SuccessfullyCreated = 1, Time = now - 10 * 86400 });
        db.Orders.Add(new PhpPlanQ1Work.MktOrderRow { SuccessfullyCreated = 0, Time = now });
        db.Users = 2;
        PhpPlanQ1Work.EpcMarketingEnsureSchema(db);
        var yes = PhpPlanQ1Work.EpcMarketingTableExists(db, "shop_orders");
        var snap = PhpPlanQ1Work.EpcMarketingLiveSnapshot(db);
        snap.Remove("generated_at");
        PhpPlanQ1Work.EpcMarketingToggleTask(db, "measurement", "gsc_verify", true, 4);
        PhpPlanQ1Work.EpcMarketingToggleTask(db, "measurement", "ga_conversions", false, 4);
        var prog = PhpPlanQ1Work.EpcMarketingLoadProgress(db);
        foreach (var sk in prog.Keys.ToList())
        {
            var bag = prog[sk];
            foreach (var tk in bag.Keys.ToList())
            {
                var row = (Dictionary<string, object?>)bag[tk]!;
                var doneAt = Convert.ToInt64(row["done_at"], System.Globalization.CultureInfo.InvariantCulture);
                row["done_at"] = Convert.ToBoolean(row["done"], System.Globalization.CultureInfo.InvariantCulture) ? (doneAt > 0 ? 1 : 0) : 0;
            }
        }

        PhpPlanQ1Work.EpcMarketingSaveKpi(db, "measurement", "monthly_sessions", 12.5, "note", 7);
        PhpPlanQ1Work.EpcMarketingSaveKpi(db, "measurement", "monthly_sessions", "n/a", "x", 7);
        var strategies = PhpPlanQ1Work.DefaultStrategies();
        var latest = PhpPlanQ1Work.EpcMarketingLatestKpis(db, strategies);
        var hist = PhpPlanQ1Work.EpcMarketingKpiHistory(db, "monthly_sessions", 12);
        PhpPlanQ1Work.EpcMarketingSaveReview(db, "seo", "weekly", 9, "ok", 3);
        PhpPlanQ1Work.EpcMarketingSaveReview(db, "seo", "weekly", -1, "bad", 3);
        var rev = PhpPlanQ1Work.EpcMarketingRecentReviews(db, 20);
        var demo = PhpPlanQ1Work.EpcMarketingDemoReport(db);
        demo.Remove("generated_at");
        ((Dictionary<string, object?>)demo["live"]!).Remove("generated_at");
        return new object?[] { none, yes, snap, prog, latest, hist, rev, demo };
    }

    private static object?[] Crumb()
    {
        var db = new PhpPlanQ1Work.CrumbStore { NextContentId = 3 };
        db.Content.Add(new PhpPlanQ1Work.CrumbContentRow { Id = 1, Url = "shop", Value = "100", Level = 0 });
        db.Content.Add(new PhpPlanQ1Work.CrumbContentRow { Id = 2, Url = "shop/orders", Value = "200", Level = 1 });
        db.ControlItems.Add(new PhpPlanQ1Work.CrumbControlRow { Caption = "300", Url = "/ops/shop/users" });
        db.Groups.Add(new PhpPlanQ1Work.CrumbGroupRow { Id = 5, Parent = 0, ForBackend = 1 });
        db.Groups.Add(new PhpPlanQ1Work.CrumbGroupRow { Id = 6, Parent = 5, ForBackend = 1 });
        PhpPlanQ1Work.BackendDir = "ops";
        var id = PhpPlanQ1Work.EpcCpBreadcrumbEnsureFolderContent(db, "shop", "shop/procurement", "procurement_folder", "epc_cp_group_procurement", "Procurement", 87);
        var again = PhpPlanQ1Work.EpcCpBreadcrumbEnsureFolderContent(db, "shop", "shop/procurement", "procurement_folder", "epc_cp_group_procurement", "Procurement", 87);
        var repair = PhpPlanQ1Work.EpcCpBreadcrumbRepairIntermediateFolders(db);
        var acc = db.Access.Where(a => a.ContentId == id).Select(a => a.GroupId).OrderBy(g => g).ToList();
        var row = db.Content.First(c => c.Id == id);
        return new object?[]
        {
            PhpPlanQ1Work.EpcCpBreadcrumbHumanizeSegment("shop/procurement"),
            PhpPlanQ1Work.EpcCpBreadcrumbHumanizeSegment("---"),
            PhpPlanQ1Work.EpcCpBreadcrumbCaptionForNode(db, "shop/orders", "404", true, false),
            PhpPlanQ1Work.EpcCpBreadcrumbCaptionForNode(db, "shop/users", "404", true, false),
            PhpPlanQ1Work.EpcCpBreadcrumbCaptionForNode(db, "shop/missing", "Missing!", true, true),
            PhpPlanQ1Work.EpcCpBreadcrumbCaptionForNode(db, "", "x", true, true),
            id, again, repair, acc,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = row.Url,
                ["alias"] = row.Alias,
                ["value"] = row.Value,
                ["published_flag"] = row.PublishedFlag,
                ["parent"] = row.Parent,
                ["level"] = row.Level
            }
        };
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
        => value.Length <= 500 ? value : value[..500] + "…";
}
