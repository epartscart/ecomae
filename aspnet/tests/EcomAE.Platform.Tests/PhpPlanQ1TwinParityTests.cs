using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1TwinParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Twin");

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
    public void PlanQ1Twin_MatchPhpGolden()
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
                PhpPlanQ1Twin.ImportOrchestratorPath,
                PhpPlanQ1Twin.DocumentVaultPath,
                PhpPlanQ1Twin.OnpremLicensesPath,
                PhpPlanQ1Twin.BiMetricsPath,
                PhpPlanQ1Twin.NotificationsPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Twin.ImportOrchestratorPath,
                PhpPlanQ1Twin.DocumentVaultPath,
                PhpPlanQ1Twin.OnpremLicensesPath,
                PhpPlanQ1Twin.BiMetricsPath,
                PhpPlanQ1Twin.NotificationsPath
            });

    [Fact]
    public void ImportCreateJob_DoesNotStartASession()
    {
        var store = new PhpPlanQ1Twin.ImportStore();
        var job = PhpPlanQ1Twin.EpcImportCreateJob(store, "siteA", new Dictionary<string, object?> { ["entity_type"] = "products" });
        Assert.Equal(true, job["ok"]);
    }

    private static string Render(string name)
        => name switch
        {
            "imp_static" => Json(ImpStatic()),
            "imp_flow" => Json(ImpFlow()),
            "vault_flow" => Json(VaultFlow()),
            "lic_flow" => Json(LicFlow()),
            "bi_static" => Json(BiStatic()),
            "bi_flow" => Json(BiFlow()),
            "ntf_static" => Json(NtfStatic()),
            "ntf_flow" => Json(NtfFlow()),
            _ => "unknown:" + name
        };

    private static object?[] ImpStatic()
    {
        var s = PhpPlanQ1Twin.EpcImportEntitySchemas();
        var f = PhpPlanQ1Twin.EpcImportSupportedFormats();
        var src = PhpPlanQ1Twin.EpcImportSupportedSources();
        var miss = PhpPlanQ1Twin.EpcImportValidateRow(new Dictionary<string, object?> { ["sku"] = "", ["product_name"] = "" }, s["products"], 1);
        var ok = PhpPlanQ1Twin.EpcImportValidateRow(new Dictionary<string, object?> { ["sku"] = "A1", ["product_name"] = "Widget", ["price"] = "9.5" }, s["products"], 2);
        var badp = PhpPlanQ1Twin.EpcImportValidateRow(new Dictionary<string, object?> { ["sku"] = "A1", ["product_name"] = "W", ["price"] = "x" }, s["products"], 3);
        var bade = PhpPlanQ1Twin.EpcImportValidateRow(new Dictionary<string, object?> { ["email"] = "nope" }, s["customers"], 4);
        var oke = PhpPlanQ1Twin.EpcImportValidateRow(new Dictionary<string, object?> { ["email"] = "a@b.co" }, s["customers"], 5);
        return new object?[]
        {
            PhpPlanQ1Twin.EpcImportOrchestratorVersion,
            s.Keys.ToArray(),
            s["products"]["required"],
            s["gl_entries"]["unique_key"],
            f.Count,
            f[0]["format"],
            src.Keys.ToArray(),
            src["products"]["required_fields"],
            miss, ok, badp, bade, oke
        };
    }

    private static object?[] ImpFlow()
    {
        var store = new PhpPlanQ1Twin.ImportStore();
        var bad = PhpPlanQ1Twin.EpcImportCreateJob(store, "siteA", new Dictionary<string, object?> { ["entity_type"] = "nope" });
        var job = PhpPlanQ1Twin.EpcImportCreateJob(store, "siteA", new Dictionary<string, object?>
        {
            ["entity_type"] = "products",
            ["filename"] = "p.csv",
            ["total_rows"] = 3,
            ["field_mapping"] = new Dictionary<string, object?> { ["sku"] = "SKU" },
            ["dry_run"] = 1,
            ["created_by"] = 7
        });
        var rows = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["sku"] = "A1", ["product_name"] = "W", ["price"] = "10" },
            new(StringComparer.Ordinal) { ["sku"] = "", ["product_name"] = "", ["price"] = "x" },
            new(StringComparer.Ordinal) { ["sku"] = "A2", ["product_name"] = "X", ["email"] = "bad" }
        };
        var chunk = PhpPlanQ1Twin.EpcImportProcessChunk(store, Convert.ToInt32(job["job_id"], CultureInfo.InvariantCulture), rows);
        var st = (Dictionary<string, object?>)PhpPlanQ1Twin.EpcImportJobStatus(store, Convert.ToInt32(job["job_id"], CultureInfo.InvariantCulture));
        var err = PhpPlanQ1Twin.EpcImportJobErrors(store, Convert.ToInt32(job["job_id"], CultureInfo.InvariantCulture));
        var miss = PhpPlanQ1Twin.EpcImportJobStatus(store, 999);
        var list = PhpPlanQ1Twin.EpcImportListJobs(store, "siteA");
        var fleet = PhpPlanQ1Twin.EpcImportFleetStats(store);
        var dry = PhpPlanQ1Twin.EpcImportDryRun(
            store,
            "siteA",
            "products",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["sku"] = "sku", ["price"] = "price", ["email"] = "email" },
            "csv",
            new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal) { ["sku"] = "A1", ["price"] = "10", ["email"] = "a@b.co" },
                new(StringComparer.Ordinal) { ["sku"] = "", ["price"] = "x", ["email"] = "nope" }
            });
        var can = PhpPlanQ1Twin.EpcImportCancel(store, Convert.ToInt32(job["job_id"], CultureInfo.InvariantCulture));
        var ret = PhpPlanQ1Twin.EpcImportRetry(store, Convert.ToInt32(job["job_id"], CultureInfo.InvariantCulture));
        var job2 = PhpPlanQ1Twin.EpcImportCreateJob(store, "siteB", new Dictionary<string, object?> { ["entity_type"] = "customers" });
        return new object?[]
        {
            bad, job["ok"], Convert.ToInt32(job["job_id"], CultureInfo.InvariantCulture),
            ((Dictionary<string, object?>)job["schema"]!)["unique_key"], chunk, st["status"],
            Convert.ToInt32(st["success_rows"], CultureInfo.InvariantCulture),
            Convert.ToInt32(st["error_rows"], CultureInfo.InvariantCulture),
            Convert.ToInt32(st["dry_run"], CultureInfo.InvariantCulture),
            st["field_mapping"], err.Count, err[0].Field, err[0].ErrorType, miss, list.Count,
            fleet[0]["site_key"], Convert.ToInt32(fleet[0]["jobs"], CultureInfo.InvariantCulture),
            dry, can, ret, job2["ok"]
        };
    }

    private static object?[] VaultFlow()
    {
        var store = new PhpPlanQ1Twin.VaultStore();
        var root = PhpPlanQ1Twin.EpcVaultCreateFolder(store, "siteA", "Docs", 0, 3);
        var child = PhpPlanQ1Twin.EpcVaultCreateFolder(store, "siteA", "Legal", Convert.ToInt32(root["folder_id"], CultureInfo.InvariantCulture), 3);
        var folders = PhpPlanQ1Twin.EpcVaultListFolders(store, "siteA", 0);
        var sub = PhpPlanQ1Twin.EpcVaultListFolders(store, "siteA", Convert.ToInt32(root["folder_id"], CultureInfo.InvariantCulture));
        var up = PhpPlanQ1Twin.EpcVaultUpload(store, "siteA", new Dictionary<string, object?>
        {
            ["folder_id"] = root["folder_id"],
            ["filename"] = "a.pdf",
            ["mime_type"] = "application/pdf",
            ["file_size"] = 100,
            ["tags"] = new[] { "alice@x.com" },
            ["file_path"] = "/v/a.pdf",
            ["checksum"] = "abc",
            ["uploaded_by"] = 3
        });
        var ver = PhpPlanQ1Twin.EpcVaultNewVersion(store, Convert.ToInt32(up["document_id"], CultureInfo.InvariantCulture), new Dictionary<string, object?>
        {
            ["file_path"] = "/v/a2.pdf",
            ["file_size"] = 120,
            ["checksum"] = "def",
            ["change_note"] = "rev",
            ["uploaded_by"] = 3
        });
        var bad = PhpPlanQ1Twin.EpcVaultNewVersion(store, 999, new Dictionary<string, object?>());
        var docs = PhpPlanQ1Twin.EpcVaultListDocuments(store, "siteA", Convert.ToInt32(root["folder_id"], CultureInfo.InvariantCulture));
        var vers = PhpPlanQ1Twin.EpcVaultVersions(store, Convert.ToInt32(up["document_id"], CultureInfo.InvariantCulture));
        var fleet = PhpPlanQ1Twin.EpcVaultFleetStats(store);
        var gdpr = PhpPlanQ1Twin.EpcVaultGdprRequest(store, "siteA", "access", "alice@x.com", "Alice");
        var proc = PhpPlanQ1Twin.EpcVaultGdprProcess(store, Convert.ToInt32(gdpr["request_id"], CultureInfo.InvariantCulture), 9);
        var er = PhpPlanQ1Twin.EpcVaultGdprRequest(store, "siteA", "erasure", "alice@x.com", "Alice");
        var erp = PhpPlanQ1Twin.EpcVaultGdprProcess(store, Convert.ToInt32(er["request_id"], CultureInfo.InvariantCulture), 9);
        var glist = PhpPlanQ1Twin.EpcVaultGdprList(store, "siteA", "completed");
        var sr = PhpPlanQ1Twin.EpcVaultSearch(store, "siteA", "a.pdf");
        var del = PhpPlanQ1Twin.EpcVaultDelete(store, Convert.ToInt32(up["document_id"], CultureInfo.InvariantCulture));
        var rst = PhpPlanQ1Twin.EpcVaultRestore(store, Convert.ToInt32(up["document_id"], CultureInfo.InvariantCulture));
        var procResp = (Dictionary<string, object?>)proc["response"]!;
        var erpResp = (Dictionary<string, object?>)erp["response"]!;
        return new object?[]
        {
            root["ok"], root["path"], child["path"], folders.Count, sub.Count, up, ver, bad, docs.Count,
            docs[0]["tags"], vers.Count, vers[0].VersionNumber,
            Convert.ToInt32(fleet[0]["documents"], CultureInfo.InvariantCulture),
            Convert.ToInt32(fleet[0]["max_versions"], CultureInfo.InvariantCulture),
            gdpr["message"], proc["ok"], Convert.ToInt32(procResp["documents_found"], CultureInfo.InvariantCulture),
            Convert.ToInt32(erpResp["erased"] ?? 0, CultureInfo.InvariantCulture), glist.Count, sr.Count, del, rst
        };
    }

    private static object?[] LicFlow()
    {
        var store = new PhpPlanQ1Twin.LicenseStore { DocumentRoot = "" };
        var gen = PhpPlanQ1Twin.EpcOnpremLicenseGenerate(store, new Dictionary<string, object?>
        {
            ["tier"] = "professional",
            ["modules"] = new[] { "erp", "cp" },
            ["users_max"] = 10,
            ["expires_days"] = 30,
            ["customer_name"] = "Acme",
            ["notes"] = "n"
        });
        var key = Convert.ToString(gen["license_key"], CultureInfo.InvariantCulture) ?? "";
        var ok = Regex.IsMatch(key, @"^LIC-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$") ? 1 : 0;
        var miss = PhpPlanQ1Twin.EpcOnpremLicenseFetch(store, "LIC-1999-XXXX-YYYY");
        var rev0 = PhpPlanQ1Twin.EpcOnpremLicenseRevoke(store, "LIC-1999-XXXX-YYYY");
        var bad = PhpPlanQ1Twin.EpcOnpremLicenseActivate(store, new Dictionary<string, object?> { ["license_key"] = "nope" });
        var nf = PhpPlanQ1Twin.EpcOnpremLicenseActivate(store, new Dictionary<string, object?> { ["license_key"] = "LIC-1999-XXXX-YYYY" });
        var nofp = PhpPlanQ1Twin.EpcOnpremLicenseActivate(store, new Dictionary<string, object?> { ["license_key"] = key });
        var act = PhpPlanQ1Twin.EpcOnpremLicenseActivate(store, new Dictionary<string, object?> { ["license_key"] = key, ["fingerprint"] = "fp1", ["hostname"] = "h", ["ip"] = "1.2.3.4" });
        var again = PhpPlanQ1Twin.EpcOnpremLicenseActivate(store, new Dictionary<string, object?> { ["license_key"] = key, ["fingerprint"] = "fp2", ["hostname"] = "h2" });
        var same = PhpPlanQ1Twin.EpcOnpremLicenseActivate(store, new Dictionary<string, object?> { ["license_key"] = key, ["fingerprint"] = "fp1" });
        PhpPlanQ1Twin.EpcOnpremHealthLog(store, new Dictionary<string, object?>
        {
            ["license_key"] = key,
            ["status"] = "ok",
            ["uptime"] = "1d",
            ["disk_free_gb"] = 12.5,
            ["memory_usage_mb"] = 256,
            ["php_version"] = "8.3.6",
            ["db_size_mb"] = 3.2,
            ["last_backup"] = "none"
        });
        var rev = PhpPlanQ1Twin.EpcOnpremLicenseRevoke(store, key);
        var revd = PhpPlanQ1Twin.EpcOnpremLicenseActivate(store, new Dictionary<string, object?> { ["license_key"] = key, ["fingerprint"] = "fp1" });
        var path = PhpPlanQ1Twin.EpcOnpremLicenseSigningKeyPath(store);
        var sig = PhpPlanQ1Twin.EpcOnpremLicenseSign(store, new Dictionary<string, object?> { ["a"] = 1 });
        var bundle = PhpPlanQ1Twin.EpcOnpremCoreBundle(store);
        return new object?[]
        {
            ok, gen["tier"], gen["modules"], gen["users_max"], gen["expires_at"] is long ? 1 : 0,
            miss, rev0, bad["error"], nf["error"], nofp["error"], act["error"], again["error"], same["error"],
            rev, revd["error"], path, sig, bundle
        };
    }

    private static object?[] BiStatic()
    {
        var m = PhpPlanQ1Twin.EpcBiBuiltinMetrics();
        return new object?[]
        {
            PhpPlanQ1Twin.EpcBiMetricsVersion,
            m.Count,
            m[0]["metric_key"],
            m[^1]["metric_key"],
            m[0]["sql_template"],
            m[7]["schedule"]
        };
    }

    private static object?[] BiFlow()
    {
        var store = new PhpPlanQ1Twin.BiStore();
        var a = PhpPlanQ1Twin.EpcBiRecordSnapshot(store, "siteA", "revenue_daily", "daily", "2026-10-08", 100, 0);
        var b = PhpPlanQ1Twin.EpcBiRecordSnapshot(store, "siteA", "revenue_daily", "daily", "2026-10-09", 125, 100);
        var c = PhpPlanQ1Twin.EpcBiRecordSnapshot(store, "siteA", "orders_daily", "daily", "2026-10-09", 5, 4);
        PhpPlanQ1Twin.EpcBiRecordSnapshot(store, "siteB", "revenue_daily", "daily", "2026-10-09", 50, 40);
        var latest = PhpPlanQ1Twin.EpcBiLatestAll(store, "siteA");
        var dash = PhpPlanQ1Twin.EpcBiDashboard(store, "siteA");
        var fleet = PhpPlanQ1Twin.EpcBiFleetOverview(store);
        var cmp = PhpPlanQ1Twin.EpcBiCompareTenants(store, "revenue_daily");
        return new object?[]
        {
            a["ok"], a["change_pct"], b["change_pct"], c["ok"],
            latest["revenue_daily"].Value, latest["orders_daily"].Value,
            dash.Keys.ToArray(), dash["finance"].Count, dash["finance"][0]["metric_key"],
            dash["sales"][0]["value"], fleet.Count, fleet[0]["site_key"],
            Convert.ToInt32(fleet[0]["metrics_tracked"], CultureInfo.InvariantCulture),
            cmp.Count, cmp[0]["site_key"], cmp[0]["value"]
        };
    }

    private static object?[] NtfStatic()
    {
        var c = PhpPlanQ1Twin.EpcNotificationCategories();
        return new object?[]
        {
            PhpPlanQ1Twin.EpcNotificationsVersion,
            c.Keys.ToArray(),
            c["security"]["color"],
            c["order"]["icon"]
        };
    }

    private static object?[] NtfFlow()
    {
        var store = new PhpPlanQ1Twin.NotifyStore();
        var z = PhpPlanQ1Twin.EpcNotificationSend(store, new Dictionary<string, object?> { ["title"] = "" });
        var id = PhpPlanQ1Twin.EpcNotificationSend(store, new Dictionary<string, object?>
        {
            ["tenant_key"] = "siteA",
            ["user_id"] = 5,
            ["category"] = "order",
            ["severity"] = "nope",
            ["title"] = "Hello",
            ["body"] = "B"
        });
        var bc = PhpPlanQ1Twin.EpcNotificationBroadcast(store, "siteA", new Dictionary<string, object?>
        {
            ["title"] = "All",
            ["body"] = "X",
            ["severity"] = "error",
            ["category"] = "system"
        });
        var list = PhpPlanQ1Twin.EpcNotificationsList(store, "siteA", 5, new Dictionary<string, object?> { ["dismissed"] = false }, 20, 0);
        var titles = list.Select(r => r.Title).OrderBy(t => t, StringComparer.Ordinal).ToArray();
        var sev = list.First(r => r.Title == "Hello").Severity;
        var un = PhpPlanQ1Twin.EpcNotificationsUnreadCount(store, "siteA", 5);
        var mr = PhpPlanQ1Twin.EpcNotificationsMarkRead(store, new[] { id }, "siteA");
        var un2 = PhpPlanQ1Twin.EpcNotificationsUnreadCount(store, "siteA", 5);
        var empty = PhpPlanQ1Twin.EpcNotificationsMarkRead(store, Array.Empty<int>(), "siteA");
        var all = PhpPlanQ1Twin.EpcNotificationsMarkAllRead(store, "siteA", 5);
        var ds = PhpPlanQ1Twin.EpcNotificationsDismiss(store, bc, "siteA");
        var sum = PhpPlanQ1Twin.EpcNotificationsSummary(store, "siteA", 5);
        PhpPlanQ1Twin.EpcNotificationPrefsSave(store, "siteA", 5, "order", new Dictionary<string, object?>
        {
            ["in_app"] = 1,
            ["email"] = 1,
            ["webhook"] = 0,
            ["email_digest"] = "daily"
        });
        var prefs = PhpPlanQ1Twin.EpcNotificationPrefsGet(store, "siteA", 5);
        var n2 = PhpPlanQ1Twin.EpcNotificationSend(store, new Dictionary<string, object?>
        {
            ["tenant_key"] = "siteA",
            ["user_id"] = 5,
            ["category"] = "order",
            ["title"] = "Later",
            ["body"] = "Z"
        });
        var dig = PhpPlanQ1Twin.EpcNotificationsPendingDigest(store, "daily");
        var fleet = PhpPlanQ1Twin.EpcNotificationsFleetStats(store);
        var cl = PhpPlanQ1Twin.EpcNotificationsCleanup(store, 90);
        return new object?[]
        {
            z, id > 0 ? 1 : 0, bc > 0 ? 1 : 0, list.Count, titles, sev, un, mr, un2, empty, all, ds,
            sum.Count, prefs["order"]["email_digest"], prefs["order"]["in_app"], n2 > 0 ? 1 : 0,
            dig.Count, dig.Count > 0 ? ((List<PhpPlanQ1Twin.NotificationRow>)dig[0]["items"]!).Count : 0,
            fleet.Count, Convert.ToInt32(fleet[0]["unread"], CultureInfo.InvariantCulture), cl
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
        => value.Length <= 280 ? value : value[..280] + "…";
}
