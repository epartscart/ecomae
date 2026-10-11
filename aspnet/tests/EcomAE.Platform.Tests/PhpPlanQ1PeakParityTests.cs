using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1PeakParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Peak");
    private const string Frozen = "2026-10-10 00:00:00";

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
    public void PlanQ1Peak_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Peak.IsolationPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Peak.IsolationPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Peak.Reset();
        Assert.Contains("epc_commerce_isolation.php", PhpPlanQ1Peak.IsolationPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Peak.IsolationPath, StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Peak.Reset();
        PhpPlanQ1Peak.Clock = () => Frozen;
        PhpPlanQ1Peak.PhpSapi = "cli";
        return name switch
        {
            "pure" => Pure(),
            "price" => Price(),
            "audit" => Audit(),
            "scan" => Scan(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        var db = new PhpPlanQ1Peak.PeakStore();
        var logs = new List<string>();
        PhpPlanQ1Peak.ErrorLog = s => logs.Add(s);
        var ids = PhpPlanQ1Peak.EpcCiTenantPriceIds(db, "acme");
        var ok = PhpPlanQ1Peak.EpcCiAssertPriceIds(db, "acme", [99, 5], "pure");
        var platNull = PhpPlanQ1Peak.EpcCiPlatformPdo() is null;
        PhpPlanQ1Peak.EpcCiLogViolation("acme", "cli", "no platform pdo detail");
        var latest = PhpPlanQ1Peak.EpcCiLatestAuditRun(db);
        var scan = WriteScanTree();
        try
        {
            PhpPlanQ1Peak.Server["DOCUMENT_ROOT"] = scan;
            var found = RelFiles(scan, PhpPlanQ1Peak.EpcCiFindPhpFiles(scan));
            var en = PhpPlanQ1Peak.EpcCiEnforcementScan(db, scan);
            SortViolations(en);
            return new Rendered(new object?[]
            {
                ids,
                ok,
                platNull,
                logs.Count > 0 ? logs[0] : "",
                latest,
                found,
                en
            });
        }
        finally
        {
            TryDelete(scan);
        }
    }

    private static Rendered Price()
    {
        var db = new PhpPlanQ1Peak.PeakStore();
        PhpPlanQ1Peak.UsePlatform(db);
        PhpPlanQ1Peak.Server["REMOTE_ADDR"] = "198.51.100.10";
        var before = PhpPlanQ1Peak.EpcCiTenantPriceIds(db, "before");
        SeedPrice(db);
        var ids = PhpPlanQ1Peak.EpcCiTenantPriceIds(db, "acme");
        var again = PhpPlanQ1Peak.EpcCiTenantPriceIds(db, "acme");
        var other = PhpPlanQ1Peak.EpcCiTenantPriceIds(db, "other");
        var pass = PhpPlanQ1Peak.EpcCiAssertPriceIds(db, "acme", [5, 7, 0, -1, "0"], "price-ok");
        var zeroOk = PhpPlanQ1Peak.EpcCiAssertPriceIds(db, "acme", ["0"], "price-zero");
        var caught = "";
        try
        {
            PhpPlanQ1Peak.EpcCiAssertPriceIds(db, "acme", [5, 99], "price-bad");
        }
        catch (Exception ex)
        {
            caught = ex.Message;
        }

        var scopedWhere = PhpPlanQ1Peak.EpcCiScopedQuery(db, "SELECT price_id, sku FROM shop_docpart_prices_data WHERE sku = ?", ["ABC"], "acme");
        var scopedAll = PhpPlanQ1Peak.EpcCiScopedQuery(db, "SELECT price_id, sku FROM shop_docpart_prices_data", [], "acme");
        var emptyPrep = PhpPlanQ1Peak.EpcCiGetScopedPdoPrepare(db, "before", "SELECT price_id, sku FROM shop_docpart_prices_data WHERE sku = ?");
        var wrapPrep = PhpPlanQ1Peak.EpcCiGetScopedPdoPrepare(db, "acme", "SELECT price_id, sku FROM shop_docpart_prices_data WHERE sku = ?");
        var already = PhpPlanQ1Peak.EpcCiGetScopedPdoPrepare(db, "acme", "SELECT price_id, sku FROM shop_docpart_prices_data WHERE price_id = 99");
        var injected = PhpPlanQ1Peak.EpcCiGetScopedPdoPrepare(db, "acme", "SELECT sku FROM shop_docpart_prices_data WHERE sku = ?");
        var abc = db.PriceData.Where(r => r.Sku == "ABC").Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["price_id"] = r.PriceId,
            ["sku"] = r.Sku
        }).ToList();
        var viol = db.Violations.Select(v => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = v.SiteKey,
            ["actor"] = v.Actor,
            ["detail"] = v.Detail,
            ["ip"] = v.Ip
        }).ToList();
        return new Rendered(new object?[]
        {
            before,
            ids,
            again,
            other,
            pass,
            zeroOk,
            caught,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["sql"] = scopedWhere["sql"], ["rows"] = scopedWhere["rows"] },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["sql"] = scopedAll["sql"], ["rows"] = scopedAll["rows"] },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["sql"] = emptyPrep["sql"], ["rows"] = abc },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["sql"] = wrapPrep["sql"], ["rows"] = abc },
            already["sql"],
            injected["sql"],
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 1 },
            viol
        });
    }

    private static Rendered Audit()
    {
        var db = new PhpPlanQ1Peak.PeakStore();
        SeedAudit(db);
        PhpPlanQ1Peak.UsePlatform(db);
        PhpPlanQ1Peak.ConnectTenant = (database, user, pass) =>
            database == "TENANT_DB" && user == "ecomae" && !string.IsNullOrEmpty(pass);
        var scan = WriteScanTree();
        try
        {
            PhpPlanQ1Peak.Server["DOCUMENT_ROOT"] = scan;
            var erp = PhpPlanQ1Peak.EpcCiAuditErpDbIsolation(db);
            var client = PhpPlanQ1Peak.EpcCiAuditClientDocpartIsolation(db);
            var own = PhpPlanQ1Peak.EpcCiAuditPriceIdOwnership(db);
            var orph = PhpPlanQ1Peak.EpcCiAuditOrphanPriceData(db);
            var scope = PhpPlanQ1Peak.EpcCiAuditQueryScoping();
            SortUnscoped(scope);
            var cred = PhpPlanQ1Peak.EpcCiAuditRegistryCredentials(db);
            var full = PhpPlanQ1Peak.EpcCiRunFullAudit(db, db);
            if (full["checks"] is Dictionary<string, object?> checks
                && checks.TryGetValue("query_scoping", out var qs)
                && qs is Dictionary<string, object?> qsd)
            {
                SortUnscoped(qsd);
            }

            var latest = PhpPlanQ1Peak.EpcCiLatestAuditRun(db);
            return new Rendered(new object?[] { erp, client, own, orph, scope, cred, full, latest });
        }
        finally
        {
            TryDelete(scan);
        }
    }

    private static Rendered Scan()
    {
        var db = new PhpPlanQ1Peak.PeakStore();
        PhpPlanQ1Peak.UsePlatform(db);
        PhpPlanQ1Peak.Server["REMOTE_ADDR"] = "203.0.113.9";
        var scan = WriteScanTree();
        try
        {
            PhpPlanQ1Peak.Server["DOCUMENT_ROOT"] = scan;
            var found = RelFiles(scan, PhpPlanQ1Peak.EpcCiFindPhpFiles(scan));
            var en = PhpPlanQ1Peak.EpcCiEnforcementScan(db, scan);
            SortViolations(en);
            PhpPlanQ1Peak.EpcCiLogViolation("acme", new string('A', 140), new string('D', 2010));
            PhpPlanQ1Peak.EpcCiLogViolation("beta", "ops", "second");
            var viol = db.Violations.Select(v => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = v.SiteKey,
                ["actor"] = v.Actor,
                ["dlen"] = v.Detail.Length,
                ["ip"] = v.Ip
            }).ToList();
            var scope = PhpPlanQ1Peak.EpcCiAuditQueryScoping();
            SortUnscoped(scope);
            return new Rendered(new object?[] { found, en, viol, scope });
        }
        finally
        {
            TryDelete(scan);
        }
    }

    private static void SeedPrice(PhpPlanQ1Peak.PeakStore db)
    {
        db.Offices.Add(new PhpPlanQ1Peak.OfficeRow { Id = db.NextOfficeId++, Caption = "Alpha" });
        db.Offices.Add(new PhpPlanQ1Peak.OfficeRow { Id = db.NextOfficeId++, Caption = "Beta" });
        void Storage(int iface, int hidden, string opts)
            => db.Storages.Add(new PhpPlanQ1Peak.StorageRow
            {
                Id = db.NextStorageId++,
                InterfaceType = iface,
                Hidden = hidden,
                ConnectionOptions = opts
            });

        Storage(2, 0, "{\"price_id\": 5}");
        Storage(2, 0, "{\"price_id\": \"7\"}");
        Storage(2, 0, "{\"price_id\": \"0\"}");
        Storage(2, 0, "{\"price_id\": 0}");
        Storage(2, 1, "{\"price_id\": 9}");
        Storage(1, 0, "{\"price_id\": 11}");
        Storage(2, 0, "not-json");
        Storage(2, 0, "{\"price_id\": 13}");
        Storage(2, 0, "{\"foo\": 1}");
        foreach (var sid in new[] { 1, 2, 3, 4, 5, 6, 7, 9 })
        {
            db.Maps.Add(new PhpPlanQ1Peak.MapRow { StorageId = sid, OfficeId = 1 });
        }

        db.Prices.Add(new PhpPlanQ1Peak.PriceRow { Id = 5 });
        db.Prices.Add(new PhpPlanQ1Peak.PriceRow { Id = 7 });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 5, Sku = "ABC" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 5, Sku = "ZZZ" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 7, Sku = "ABC" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 99, Sku = "ABC" });
    }

    private static void SeedAudit(PhpPlanQ1Peak.PeakStore db)
    {
        void Tenant(
            string key, string host, string database, string user, string pass, string trade,
            string industry, int dedicated, string scale, int erp, string hosted, string status)
            => db.Tenants.Add(new PhpPlanQ1Peak.TenantRow
            {
                SiteKey = key,
                Hostname = host,
                DbName = database,
                DbUser = user,
                DbPassword = pass,
                TradeName = trade,
                IndustryCode = industry,
                DedicatedDb = dedicated,
                ScalePolicy = scale,
                ErpOnlyShared = erp,
                HostedOn = hosted,
                Status = status
            });

        Tenant("epartscart", "www.epartscart.com", "docpart", "u", "p", "eParts", "auto", 0, "", 0, "", "live");
        Tenant("taxo", "taxo.example.com", "docpart", "u", "p", "Taxo", "tax", 0, "shared", 0, "", "live");
        Tenant("boutique", "Boutique.Example.com", "boutique_db", "u", "p", "Boutique", "retail", 1, "dedicated", 0, "", "live");
        Tenant("platformy", "plat.example.com", "ecomae", "u", "p", "Plat", "x", 0, "", 0, "platform", "live");
        Tenant("nohost", "", "x", "u", "p", "NoHost", "x", 0, "", 0, "", "live");
        Tenant("erpbads", "erp-bad.example.com", "docpart", "ecomae", "", "ERP Bad", "erp", 0, "", 1, "", "live");
        Tenant("erpgood", "erp-good.example.com", "TENANT_DB", "ecomae", "secret", "ERP Good", "erp", 1, "", 1, "", "live");
        Tenant("erpempty", "erp-empty.example.com", "", "", "", "ERP Empty", "erp", 0, "", 1, "", "live");
        Tenant("pending", "pend.example.com", "ecomae", "u", "p", "Pending", "x", 0, "", 0, "", "dns_pending");
        Tenant("erpold", "old.example.com", "okdb", "ecomae", "secret", "Old", "erp", 0, "", 1, "", "paused");

        db.Offices.Add(new PhpPlanQ1Peak.OfficeRow { Id = db.NextOfficeId++, Caption = "Alpha" });
        db.Offices.Add(new PhpPlanQ1Peak.OfficeRow { Id = db.NextOfficeId++, Caption = "Beta" });
        db.Storages.Add(new PhpPlanQ1Peak.StorageRow { Id = db.NextStorageId++, InterfaceType = 2, Hidden = 0, ConnectionOptions = "{\"price_id\": 5}" });
        db.Storages.Add(new PhpPlanQ1Peak.StorageRow { Id = db.NextStorageId++, InterfaceType = 2, Hidden = 0, ConnectionOptions = "{\"price_id\": 5}" });
        db.Storages.Add(new PhpPlanQ1Peak.StorageRow { Id = db.NextStorageId++, InterfaceType = 2, Hidden = 0, ConnectionOptions = "{\"price_id\": 8}" });
        db.Storages.Add(new PhpPlanQ1Peak.StorageRow { Id = db.NextStorageId++, InterfaceType = 2, Hidden = 1, ConnectionOptions = "{\"price_id\": 9}" });
        db.Maps.Add(new PhpPlanQ1Peak.MapRow { StorageId = 1, OfficeId = 1 });
        db.Maps.Add(new PhpPlanQ1Peak.MapRow { StorageId = 2, OfficeId = 2 });
        db.Maps.Add(new PhpPlanQ1Peak.MapRow { StorageId = 3, OfficeId = 1 });
        db.Maps.Add(new PhpPlanQ1Peak.MapRow { StorageId = 4, OfficeId = 1 });
        db.Prices.Add(new PhpPlanQ1Peak.PriceRow { Id = 1 });
        db.Prices.Add(new PhpPlanQ1Peak.PriceRow { Id = 2 });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 1, Sku = "A" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 1, Sku = "B" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 99, Sku = "X" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 99, Sku = "Y" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 99, Sku = "Z" });
        db.PriceData.Add(new PhpPlanQ1Peak.PriceDataRow { PriceId = 100, Sku = "Q" });
    }

    private static string WriteScanTree()
    {
        var scan = Directory.CreateTempSubdirectory("ecomae_cpw_q1p_").FullName;
        Directory.CreateDirectory(Path.Combine(scan, "vendor"));
        Directory.CreateDirectory(Path.Combine(scan, "node_modules"));
        Directory.CreateDirectory(Path.Combine(scan, ".git"));
        Directory.CreateDirectory(Path.Combine(scan, ".agents"));
        Directory.CreateDirectory(Path.Combine(scan, "content", "files"));
        Directory.CreateDirectory(Path.Combine(scan, "admin"));
        File.WriteAllText(Path.Combine(scan, "vendor", "skip.php"), "<?php echo 'vendor';\n");
        File.WriteAllText(Path.Combine(scan, "node_modules", "skip.php"), "<?php echo 'nm';\n");
        File.WriteAllText(Path.Combine(scan, ".git", "skip.php"), "<?php echo 'git';\n");
        File.WriteAllText(Path.Combine(scan, ".agents", "skip.php"), "<?php echo 'agents';\n");
        File.WriteAllText(Path.Combine(scan, "content", "files", "leak.php"), "<?php SELECT * FROM shop_docpart_prices_data;\n");
        File.WriteAllText(Path.Combine(scan, "shop.php"), "<?php SELECT * FROM shop_docpart_prices_data;\n");
        File.WriteAllText(Path.Combine(scan, "ok.php"), "<?php SELECT * FROM shop_docpart_prices_data WHERE price_id IN (1);\n");
        File.WriteAllText(Path.Combine(scan, "mention.php"), "<?php // shop_docpart_prices_data catalog note\n");
        File.WriteAllText(Path.Combine(scan, "other.php"), "<?php SELECT 1;\n");
        File.WriteAllText(Path.Combine(scan, "admin", "epc-site-health.php"), "<?php SELECT * FROM shop_docpart_prices_data;\n");
        File.WriteAllText(Path.Combine(scan, "sitekey.php"), "<?php SELECT * FROM shop_docpart_prices_data; SELECT * FROM shop_docpart_prices WHERE site_key = 1;\n");
        File.WriteAllText(Path.Combine(scan, "prices_only.php"), "<?php SELECT * FROM shop_docpart_prices;\n");
        return scan;
    }

    private static List<string> RelFiles(string scan, List<string> files)
    {
        var root = scan.TrimEnd('/');
        return files
            .Select(f => f.StartsWith(root, StringComparison.Ordinal) ? f[root.Length..].TrimStart('/') : f)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    private static void SortUnscoped(Dictionary<string, object?> check)
    {
        if (check.TryGetValue("unscoped", out var raw) && raw is List<Dictionary<string, object?>> rows)
        {
            check["unscoped"] = rows
                .OrderBy(r => Convert.ToString(r["file"]), StringComparer.Ordinal)
                .ToList();
        }
        else if (raw is List<object?> boxed)
        {
            check["unscoped"] = boxed
                .OfType<Dictionary<string, object?>>()
                .OrderBy(r => Convert.ToString(r["file"]), StringComparer.Ordinal)
                .ToList();
        }
    }

    private static void SortViolations(Dictionary<string, object?> results)
    {
        if (results.TryGetValue("violations", out var raw) && raw is List<object?> boxed)
        {
            results["violations"] = boxed
                .OfType<Dictionary<string, object?>>()
                .OrderBy(r => Convert.ToString(r["file"]), StringComparer.Ordinal)
                .ThenBy(r => Convert.ToString(r["table"]), StringComparer.Ordinal)
                .Cast<object?>()
                .ToList();
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
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
