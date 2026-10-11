using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1SafeParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Safe");

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
    public void PlanQ1Safe_MatchPhpGolden()
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
                PhpPlanQ1Safe.SitemapWarehousePath,
                PhpPlanQ1Safe.TenantDataProtectionPath,
                PhpPlanQ1Safe.CustomerMgmtHelpersPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Safe.SitemapWarehousePath,
                PhpPlanQ1Safe.TenantDataProtectionPath,
                PhpPlanQ1Safe.CustomerMgmtHelpersPath
            });

    [Fact]
    public void CheckAccess_DoesNotStartASession()
    {
        PhpPlanQ1Safe.Reset();
        Assert.False((bool)PhpPlanQ1Safe.EpcTdpCheckAccess("", PhpPlanQ1Safe.EpcTdpAccessRead)["allowed"]!);
    }

    private static string Render(string name)
    {
        PhpPlanQ1Safe.Reset();
        return name switch
        {
            "tdp_pure" => Json(TdpPure()),
            "tdp_db" => Json(TdpDb()),
            "sm_pure" => Json(SmPure()),
            "sm_db" => Json(SmDb()),
            "cm_pure" => Json(CmPure()),
            "cm_db" => Json(CmDb()),
            _ => "unknown:" + name
        };
    }

    private static object? TdpPure()
    {
        PhpPlanQ1Safe.Server["HTTP_CF_CONNECTING_IP"] = " 1.1.1.1, 9.9.9.9";
        PhpPlanQ1Safe.Server["HTTP_X_FORWARDED_FOR"] = "8.8.8.8";
        var a1 = PhpPlanQ1Safe.EpcTdpCheckAccess("", PhpPlanQ1Safe.EpcTdpAccessRead, Dict(("role", "provider"), ("user_id", 1)));
        var a2 = PhpPlanQ1Safe.EpcTdpCheckAccess("Acme-Site!", PhpPlanQ1Safe.EpcTdpAccessRead, Dict(("role", "guest"), ("user_id", 0)));
        var a3 = PhpPlanQ1Safe.EpcTdpCheckAccess("acme", PhpPlanQ1Safe.EpcTdpAccessRead, Dict(("role", "provider"), ("user_id", 9)));
        var a4 = PhpPlanQ1Safe.EpcTdpCheckAccess("acme", PhpPlanQ1Safe.EpcTdpAccessExport, Dict(("role", "provider"), ("user_id", 9)));
        var a5 = PhpPlanQ1Safe.EpcTdpCheckAccess("acme", PhpPlanQ1Safe.EpcTdpAccessWrite, Dict(("role", "tenant"), ("user_id", 3), ("tenant_key", "other")));
        var a6 = PhpPlanQ1Safe.EpcTdpCheckAccess("acme", PhpPlanQ1Safe.EpcTdpAccessWrite, Dict(("role", "tenant"), ("user_id", 3), ("tenant_key", "acme")));
        var a7 = PhpPlanQ1Safe.EpcTdpCheckAccess("acme", PhpPlanQ1Safe.EpcTdpAccessAdmin, Dict(("role", "tenant"), ("user_id", 3), ("tenant_key", "acme")));
        var a8 = PhpPlanQ1Safe.EpcTdpCheckAccess("acme", PhpPlanQ1Safe.EpcTdpAccessRead, Dict(("role", "ops"), ("user_id", 4)));
        var red = PhpPlanQ1Safe.EpcTdpRedactSensitive(
            Dict(("name", "Ann"), ("password", "secret"), ("nested", Dict(("iban", "GB00"), ("ok", 1))), ("TRN", "100")),
            ["ok"]);
        PhpPlanQ1Safe.EpcTdpApplySecurityHeaders();
        return new object?[]
        {
            a1, a2, a3, a4, a5, a6, a7, a8, red,
            PhpPlanQ1Safe.EpcTdpClassifyData("GL"),
            PhpPlanQ1Safe.EpcTdpClassifyData("unknownX"),
            PhpPlanQ1Safe.EpcTdpRetentionPolicy().Keys.ToList(),
            PhpPlanQ1Safe.EpcTdpClientIp(),
            Array.Empty<string>(),
            PhpPlanQ1Safe.EpcTdpPlatformPdo() is null,
            PhpPlanQ1Safe.EpcTdpVersion,
            PhpPlanQ1Safe.EpcTdpProviderDefault,
            PhpPlanQ1Safe.EpcTdpTenantDefault
        };
    }

    private static object? TdpDb()
    {
        PhpPlanQ1Safe.HasPlatformPdo = true;
        PhpPlanQ1Safe.TenantRow = DemoTenant;
        PhpPlanQ1Safe.TenantConnect = row =>
        {
            var db = Convert.ToString(row["db_name"]) ?? "";
            return db is "tenant_live" or "docpart" ? (true, "") : (false, "denied");
        };
        PhpPlanQ1Safe.ListTenants = () =>
        [
            Dict(("site_key", "liveok")),
            Dict(("site_key", "")),
            Dict(("site_key", "missing"))
        ];
        var db = new PhpPlanQ1Safe.TdpStore();
        PhpPlanQ1Safe.EpcTdpEnsureAuditTable(db);
        PhpPlanQ1Safe.EpcTdpLogAccess(db, "acme", "orders", "list", 7, Dict(("q", "x")));
        PhpPlanQ1Safe.EpcTdpLogAccess(db, "acme", "orders", "list", 7, new Dictionary<string, object?>());
        PhpPlanQ1Safe.EpcTdpLogViolation(db, "cross_tenant_attempt", 3, "acme", Dict(("ip", "1.1.1.1")));
        var meta = db.Audit.OrderBy(a => a.Id).Select(a => a.MetaJson).ToList();
        var r1 = FixTs(PhpPlanQ1Safe.EpcTdpVerifyIsolation(db, "missing"));
        var r2 = FixTs(PhpPlanQ1Safe.EpcTdpVerifyIsolation(db, "liveok"));
        var r3 = FixTs(PhpPlanQ1Safe.EpcTdpVerifyIsolation(db, "shared"));
        var r4 = FixTs(PhpPlanQ1Safe.EpcTdpVerifyIsolation(db, "ecomaedb"));
        var r5 = FixTs(PhpPlanQ1Safe.EpcTdpVerifyIsolation(db, "nopass"));
        var all = PhpPlanQ1Safe.EpcTdpVerifyAllTenants(db);
        foreach (var key in all.Keys.ToList())
        {
            all[key] = FixTs((Dictionary<string, object?>)all[key]!);
        }

        return new object?[]
        {
            db.Audit.Count, db.Violations.Count, meta, r1, r2, r3, r4, r5, all, PhpPlanQ1Safe.EpcTdpPlatformPdo() is not null
        };
    }

    private static object? SmPure()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1s_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            PhpPlanQ1Safe.DocumentRoot = root;
            var dir = PhpPlanQ1Safe.EpcSitemapWarehouseCacheDir();
            var stale = PhpPlanQ1Safe.EpcSitemapWarehouseStalePath();
            PhpPlanQ1Safe.EpcSitemapWarehouseMarkStale("upload", 12);
            var marked = PhpPlanQ1Safe.EpcSitemapWarehouseIsStale();
            var raw = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(stale))!;
            raw["marked_at"] = "FIXED";
            if (raw["price_id"] is JsonElement je)
            {
                raw["price_id"] = je.GetInt32();
            }

            PhpPlanQ1Safe.EpcSitemapWarehouseClearStale();
            var cleared = !PhpPlanQ1Safe.EpcSitemapWarehouseIsStale();
            var meta0 = PhpPlanQ1Safe.EpcSitemapWarehouseMetaRead();
            PhpPlanQ1Safe.EpcSitemapWarehouseMetaWrite(new Dictionary<string, object?> { ["shards"] = 2, ["urls"] = 9 });
            var meta1 = PhpPlanQ1Safe.EpcSitemapWarehouseMetaRead();
            meta1["generated_at"] = "FIXED";
            var xml = PhpPlanQ1Safe.EpcSitemapWarehouseUrlXml(new object(), "en", "Bosch", "0 123", "2026-10-09");
            var bad = PhpPlanQ1Safe.EpcSitemapWarehouseUrlXml(new object(), "en", "brands", "ABC", "2026-10-09");
            var body = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" + xml + "</urlset>\n";
            PhpPlanQ1Safe.EpcSitemapWarehouseWriteShardFile(0, body);
            File.WriteAllText(PhpPlanQ1Safe.EpcSitemapWarehousePublicPath(1), "short");
            var s0 = PhpPlanQ1Safe.EpcSitemapWarehouseServeCached(0);
            var out0 = PhpPlanQ1Safe.LastBody;
            var s1 = PhpPlanQ1Safe.EpcSitemapWarehouseServeCached(1);
            var sNeg = PhpPlanQ1Safe.EpcSitemapWarehouseServeCached(-1);
            PhpPlanQ1Safe.EpcSitemapWarehouseMetaRefreshFromFiles();
            var meta2 = PhpPlanQ1Safe.EpcSitemapWarehouseMetaRead();
            meta2["generated_at"] = "FIXED";
            return new object?[]
            {
                PhpPlanQ1Safe.EpcSitemapWarehouseShardSize(),
                PhpPlanQ1Safe.EpcSitemapWarehouseMaxShards(),
                Path.GetFileName(dir),
                Path.GetFileName(stale),
                Path.GetFileName(PhpPlanQ1Safe.EpcSitemapWarehouseMetaPath()),
                Path.GetFileName(PhpPlanQ1Safe.EpcSitemapWarehouseCachePath(3)),
                Path.GetFileName(PhpPlanQ1Safe.EpcSitemapWarehousePublicPath(3)),
                marked, raw, cleared, meta0, meta1, xml, bad, s0, out0.Length, out0.Length >= 40 ? out0[..40] : out0,
                s1, sNeg, PhpPlanQ1Safe.EpcSitemapWarehouseExistingShardCount(), meta2,
                PhpPlanQ1Safe.EpcSitemapWarehouseEstimateShards(new PhpPlanQ1Safe.SmStore())
            };
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* leftover temp */ }
        }
    }

    private static object? SmDb()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1s_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            PhpPlanQ1Safe.DocumentRoot = root;
            var db = new PhpPlanQ1Safe.SmStore();
            db.Prices.AddRange(
            [
                new() { Manufacturer = " Bosch ", Article = "0 123", Exist = 2, Price = 10.5 },
                new() { Manufacturer = "Bosch", Article = "0 123", Exist = 3, Price = 9.0 },
                new() { Manufacturer = "Valeo", Article = "ABC", Exist = 1, Price = 4 },
                new() { Manufacturer = "", Article = "X", Exist = 1, Price = 1 },
                new() { Manufacturer = "Zed", Article = "", Exist = 1, Price = 1 },
                new() { Manufacturer = "Zero", Article = "Z1", Exist = 0, Price = 8 },
                new() { Manufacturer = "Free", Article = "F1", Exist = 1, Price = 0 },
                new() { Manufacturer = "brands", Article = "SKIP", Exist = 1, Price = 2 }
            ]);
            PhpPlanQ1Safe.NeedPrice = true;
            var f1 = PhpPlanQ1Safe.EpcSitemapWarehousePriceFilters(db);
            PhpPlanQ1Safe.NeedPrice = false;
            var f2 = PhpPlanQ1Safe.EpcSitemapWarehousePriceFilters(db);
            PhpPlanQ1Safe.NeedPrice = true;
            var cfg = new object();
            var r0 = PhpPlanQ1Safe.EpcSitemapWarehouseRegenerateShard(cfg, db, 0);
            var rBad = PhpPlanQ1Safe.EpcSitemapWarehouseRegenerateShard(cfg, db, 80);
            var r1 = PhpPlanQ1Safe.EpcSitemapWarehouseRegenerateShard(cfg, db, 1);
            var all = PhpPlanQ1Safe.EpcSitemapWarehouseRegenerateAll(cfg, db);
            var meta = PhpPlanQ1Safe.EpcSitemapWarehouseMetaRead();
            meta["generated_at"] = "FIXED";
            File.Delete(PhpPlanQ1Safe.EpcSitemapWarehouseMetaPath());
            var est = PhpPlanQ1Safe.EpcSitemapWarehouseEstimateShards(db);
            return new object?[] { f1, f2, r0, rBad, r1, all, meta, est, PhpPlanQ1Safe.EpcSitemapWarehouseExistingShardCount() };
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* leftover temp */ }
        }
    }

    private static object? CmPure()
        => new object?[]
        {
            PhpPlanQ1Safe.EpcCmH("O'Reilly & Co<"),
            PhpPlanQ1Safe.EpcCmMoney(1234.5),
            PhpPlanQ1Safe.EpcCmMoney("-"),
            PhpPlanQ1Safe.EpcCmCustomerDisplayName(Dict(("company", "Acme"))),
            PhpPlanQ1Safe.EpcCmCustomerDisplayName(Dict(("buyer_name", "Buyer"))),
            PhpPlanQ1Safe.EpcCmCustomerDisplayName(Dict(("fname", "Jean"), ("sname", "Luc"))),
            PhpPlanQ1Safe.EpcCmCustomerDisplayName(Dict(("email", "ann@x.com"))),
            PhpPlanQ1Safe.EpcCmCustomerDisplayName(Dict(("email", "noreply"))),
            PhpPlanQ1Safe.EpcCmCustomerDisplayName(Dict(("user_id", 5))),
            PhpPlanQ1Safe.EpcCmCustomerInitials(Dict(("company", "Acme LLC"))),
            PhpPlanQ1Safe.EpcCmCustomerInitials(Dict(("fname", "Jean"), ("sname", "Luc"))),
            PhpPlanQ1Safe.EpcCmCustomerInitials(new Dictionary<string, object?>()),
            PhpPlanQ1Safe.EpcCmTabUrl("/cp/customers?", "orders", "q=1"),
            PhpPlanQ1Safe.EpcCmTabUrl("/cp/customers", "open")
        };

    private static object? CmDb()
    {
        var db = new PhpPlanQ1Safe.CmStore { NextProfileId = 5, NextOrderId = 5, NextReturnId = 3 };
        db.Users.AddRange(
        [
            new() { UserId = 1, Email = "ann@x.com", Phone = "97150", TimeRegistered = 1770000000 },
            new() { UserId = 2, Email = "bob@x.com", Phone = "97151", TimeRegistered = 1770000000 },
            new() { UserId = 3, Email = "cara@x.com", Phone = "", TimeRegistered = 1770000000 }
        ]);
        db.Profiles.AddRange(
        [
            new() { Id = 1, UserId = 1, DataKey = "name", DataValue = "Ann" },
            new() { Id = 2, UserId = 1, DataKey = "surname", DataValue = "Lee" },
            new() { Id = 3, UserId = 1, DataKey = "company", DataValue = "Acme" },
            new() { Id = 4, UserId = 2, DataKey = "name", DataValue = "Bob" }
        ]);
        db.Buyers.AddRange(
        [
            new() { UserId = 1, Trn = "100234567890003", PeppolEndpoint = "pep", BuyerOnboarded = 1, BuyerName = "Acme Buyer", City = "Dubai", CountryCode = "AE" },
            new() { UserId = 2 }
        ]);
        db.Orders.AddRange(
        [
            new() { Id = 1, UserId = 1, SuccessfullyCreated = 1, Paid = 0, Time = 1770000000 },
            new() { Id = 2, UserId = 1, SuccessfullyCreated = 1, Paid = 1, Time = 1770000000 },
            new() { Id = 3, UserId = 2, SuccessfullyCreated = 1, Paid = 2, Time = 1770000000 },
            new() { Id = 4, UserId = 2, SuccessfullyCreated = 0, Paid = 0, Time = 1770000000 }
        ]);
        db.Items.AddRange(
        [
            new() { OrderId = 1, Price = 10.5, CountNeed = 2 },
            new() { OrderId = 2, Price = 3, CountNeed = 1 }
        ]);
        db.Advances.AddRange(
        [
            new() { UserId = 1, Amount = 12.5, Active = 1, Income = 1, Time = 1770000000 },
            new() { UserId = 1, Amount = 4, Active = 1, Income = 0, Time = 1770000000 },
            new() { UserId = 2, Amount = 9, Active = 0, Income = 1, Time = 1770000000 }
        ]);
        db.Einvoices.AddRange(
        [
            new() { UserId = 1, Active = 1, IssueDate = "2026-10-01", Doc = "A" },
            new() { UserId = 1, Active = 0, IssueDate = "2026-09-01", Doc = "B" }
        ]);
        db.Returns.AddRange(
        [
            new() { Id = 1, OrderId = 1 },
            new() { Id = 2, OrderId = 2 }
        ]);
        var dash = PhpPlanQ1Safe.EpcCmDashboard(db);
        var list = PhpPlanQ1Safe.EpcCmListCustomers(db, "1", 10, 0)
            .Select(r => new object?[] { r["user_id"], r["display_name"], r["order_count"], r["trn"] }).ToList();
        var list2 = PhpPlanQ1Safe.EpcCmListCustomers(db, "Acme", 10, 0)
            .Select(r => new object?[] { r["user_id"], r["display_name"] }).ToList();
        var one = PhpPlanQ1Safe.EpcCmGetCustomer(db, 1)!;
        var none = PhpPlanQ1Safe.EpcCmGetCustomer(db, 0);
        var ords = PhpPlanQ1Safe.EpcCmCustomerOrders(db, 1, 10);
        var adv = PhpPlanQ1Safe.EpcCmCustomerAdvances(db, 1, 10);
        var inv = PhpPlanQ1Safe.EpcCmCustomerEinvoices(db, 1, 10);
        var ret = PhpPlanQ1Safe.EpcCmRecentReturns(db, 1, 10);
        var rec = PhpPlanQ1Safe.EpcCmRecentOrders(db, 5);
        PhpPlanQ1Safe.EpcCmSaveCustomerProfile(db, Dict(("user_id", 1), ("country_code", "ae"), ("trn", "100-234"), ("company", "Acme2"), ("city", "Sharjah"), ("phone", "55")));
        var prof = db.Profiles.Where(p => p.UserId == 1).OrderBy(p => p.DataKey, StringComparer.Ordinal)
            .Select(p => new Dictionary<string, object?>(StringComparer.Ordinal) { ["data_key"] = p.DataKey, ["data_value"] = p.DataValue }).ToList();
        return new object?[]
        {
            dash, PhpPlanQ1Safe.EpcCmCountCustomers(db, ""), PhpPlanQ1Safe.EpcCmCountCustomers(db, "ann"),
            list, list2, one["display_name"], one["order_count"], one["trn"], none is null,
            ords.Count, Convert.ToDouble(ords[0]["sale_ex"]), adv.Count, Convert.ToDouble(adv[0]["amount"]),
            inv.Count, ret.Count, rec.Count, Convert.ToDouble(rec[0]["sale_ex"]),
            PhpPlanQ1Safe.SavedBuyers[0]["trn"], PhpPlanQ1Safe.TradeSets, PhpPlanQ1Safe.VatSyncs, prof
        };
    }

    private static Dictionary<string, object?> DemoTenant(string key)
        => key switch
        {
            "liveok" => Dict(("db_name", "tenant_live"), ("db_password", "pw"), ("hostname", "shop.example.com"), ("status", "live"), ("erp_only_shared", 0)),
            "shared" => Dict(("db_name", "docpart"), ("db_password", ""), ("hostname", "www.ecomae.com"), ("status", "live"), ("erp_only_shared", 1)),
            "ecomaedb" => Dict(("db_name", "ecomae"), ("db_password", "pw"), ("hostname", "x.example.com"), ("status", "draft"), ("erp_only_shared", 0)),
            "nopass" => Dict(("db_name", "tenant_x"), ("db_password", ""), ("hostname", "x.example.com"), ("status", "weird"), ("erp_only_shared", 0)),
            _ => null!
        };

    private static Dictionary<string, object?> FixTs(Dictionary<string, object?> row)
    {
        row["timestamp"] = "FIXED";
        return row;
    }

    private static Dictionary<string, object?> Dict(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs)
        {
            d[k] = v;
        }

        return d;
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
        => value.Length <= 800 ? value : value[..800] + "…";
}
