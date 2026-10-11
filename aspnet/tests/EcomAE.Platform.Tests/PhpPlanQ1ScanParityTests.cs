using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ScanParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Scan");

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
    public void PlanQ1Scan_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Scan.CloudPanelHelpersPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Scan.CloudPanelHelpersPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Scan.Reset();
        Assert.Contains("epc_cloudpanel_helpers.php", PhpPlanQ1Scan.CloudPanelHelpersPath, StringComparison.Ordinal);
        Assert.Equal("https://127.0.0.1:8443", PhpPlanQ1Scan.EpcClpPanelUrl());
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Scan.EpcClpPanelUrl(), StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Scan.Reset();
        return name switch
        {
            "pure" => Pure(),
            "vhost" => Vhost(),
            "provision" => Provision(),
            "snippets" => Snippets(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        var slugs = PhpPlanQ1Scan.EpcClpNginxTenantBasenameSlugs();
        var hosts = PhpPlanQ1Scan.EpcClpModelCTenantHostnames();
        var tpl = PhpPlanQ1Scan.EpcClpVhostTenantDirectServerTemplate();
        var wrap = PhpPlanQ1Scan.EpcClpVhostTenantDirectTemplate();
        var groups = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["key"] = "a", ["hosts"] = new List<string> { "stylenlook.com", "www.stylenlook.com" } },
            new(StringComparer.Ordinal) { ["key"] = "b", ["hosts"] = new List<string> { "taxofinca.com" } },
            new(StringComparer.Ordinal) { ["key"] = "empty", ["hosts"] = new List<string>() }
        };
        var snip = PhpPlanQ1Scan.EpcClpVhostBuildModelCTenantSnippets(groups);
        return new Rendered(new object?[]
        {
            PhpPlanQ1Scan.EpcClpPanelUrl(),
            PhpPlanQ1Scan.EpcClpBin(),
            PhpPlanQ1Scan.EpcClpAvailable(),
            slugs,
            PhpPlanQ1Scan.EpcClpVhostModelCPlatformHosts(),
            hosts,
            PhpPlanQ1Scan.EpcClpNginxPlatformConfigBasename(""),
            PhpPlanQ1Scan.EpcClpNginxPlatformConfigBasename("cp.ecomae.com"),
            PhpPlanQ1Scan.EpcClpSslStandardPaths("www.demo.com"),
            PhpPlanQ1Scan.EpcClpSslCertificatePaths(""),
            PhpPlanQ1Scan.EpcClpSslCertificatePaths("www.demo.com"),
            PhpPlanQ1Scan.EpcClpSslCertificatePaths("www.demo.com", true),
            PhpPlanQ1Scan.EpcClpGuessDocroot("alice", "shop.test"),
            PhpPlanQ1Scan.EpcPortalSharedDocroots(),
            PhpPlanQ1Scan.EpcClpSiteExists("missing.test"),
            PhpPlanQ1Scan.EpcClpProvisionPhpSite(new(StringComparer.Ordinal)),
            PhpPlanQ1Scan.EpcClpProvisionPhpSite(new(StringComparer.Ordinal)
            {
                ["domain"] = "a.test",
                ["site_user"] = "u",
                ["site_user_password"] = "p"
            }),
            PhpPlanQ1Scan.EpcClpProvisionDatabase(new(StringComparer.Ordinal)
            {
                ["domain"] = "a.test",
                ["database_name"] = "d",
                ["database_user"] = "u",
                ["database_password"] = "p"
            }),
            PhpPlanQ1Scan.EpcClpInstallLeCertificate(""),
            PhpPlanQ1Scan.EpcClpWebSiteListed("<a href=\"/site/demo.test\">demo.test</a>", "demo.test"),
            PhpPlanQ1Scan.EpcClpWebSiteListed("nope", "demo.test"),
            Count(tpl, "TENANT_NAMES"),
            Count(tpl, "{{php_fpm_port}}"),
            wrap.StartsWith("# EPC_TENANT_DIRECT_START", StringComparison.Ordinal),
            Count(snip, "server_name www.stylenlook.com;"),
            Count(snip, "server_name taxofinca.com;"),
            Count(snip, "# EPC_TENANT_APEX_REDIRECT_START"),
            Count(snip, "return 301 https://www.stylenlook.com$request_uri;")
        });
    }

    private static Rendered Vhost()
    {
        var tenants = new[] { "www.epartscart.com", "epartscart.com", "www.stylenlook.com" };
        var src = "server {\n  listen 80;\n  server_name epartscart.com www.stylenlook.com extra.test;\n  return 301 https://www.epartscart.com$request_uri;\n}\nserver {\n  listen 443 ssl;\n  server_name www.stylenlook.com;\n  ssl_reject_handshake on;\n  return 444;\n}\nserver {\n  listen 443 ssl;\n  server_name keep.test;\n  ssl_reject_handshake on;\n  proxy_pass http://127.0.0.1:3000;\n}\n# EPC_TENANT_DIRECT_START\nserver {\n  server_name www.stylenlook.com;\n  root /old;\n}\n# EPC_TENANT_DIRECT_END\n";
        var scrub = PhpPlanQ1Scan.EpcClpVhostScrubTenantMisroutes(src, tenants);
        var rej = PhpPlanQ1Scan.EpcClpVhostStripSslRejectForHosts(src, tenants, out var rejN);
        var stand = PhpPlanQ1Scan.EpcClpVhostStripTenantStandaloneBlocks(src, tenants, out var n444, out var n3000);
        var marks = PhpPlanQ1Scan.EpcClpVhostStripTenantMarkers(src);
        var strip = PhpPlanQ1Scan.EpcClpVhostStripHostsFromServerNames(src, tenants, true);
        var stripAll = PhpPlanQ1Scan.EpcClpVhostStripHostsFromServerNames(src, tenants, false);
        var audit = PhpPlanQ1Scan.EpcClpVhostAuditServerNames(src);
        var regs = PhpPlanQ1Scan.EpcClpVhostProtectedRegions(src);
        var orph = PhpPlanQ1Scan.EpcClpVhostStripOrphanTenantServerBlocks(src, tenants);
        return new Rendered(new object?[]
        {
            scrub,
            rej,
            rejN,
            stand,
            n444,
            n3000,
            marks,
            strip.Vhost,
            strip.Log,
            stripAll.Vhost,
            audit,
            regs,
            PhpPlanQ1Scan.EpcClpVhostPositionInRegions(0, regs),
            PhpPlanQ1Scan.EpcClpVhostPositionInRegions(regs[0][0] + 1, regs),
            orph["vhost"],
            orph["removed"],
            orph["log"]
        });
    }

    private static Rendered Provision()
    {
        var cookie = "sid=1";
        return new Rendered(new object?[]
        {
            PhpPlanQ1Scan.EpcClpVhostSave(ref cookie, "www.ecomae.com", "", ""),
            PhpPlanQ1Scan.EpcClpNginxReloadWithPass(""),
            PhpPlanQ1Scan.EpcClpVhostConfigureModelCTenants(ref cookie, "www.ecomae.com", []),
            PhpPlanQ1Scan.EpcClpVhostConfigureTenantDirectPhp(ref cookie, "www.ecomae.com", []),
            PhpPlanQ1Scan.EpcClpWebCreatePhpSite(ref cookie, new(StringComparer.Ordinal)),
            PhpPlanQ1Scan.EpcClpWebSiteListed("<div>/site/Foo.Test</div>", "foo.test"),
            PhpPlanQ1Scan.EpcClpWebSiteListed(">Foo.Test<", "foo.test")
        });
    }

    private static Rendered Snippets()
    {
        const string doc = "/tmp/scan-doc/portal";
        PhpPlanQ1Scan.IsDir = p => p == doc;
        var v8080 = "server {\n  listen 8080;\n  server_name www.ecomae.com extra.test;\n}\nserver {\n  listen 80;\n  server_name www.ecomae.com extra.test;\n}\n";
        var add = PhpPlanQ1Scan.EpcClpVhostAddAliasesToPhpBackend(v8080, ["www.stylenlook.com", "extra.test"], "www.ecomae.com");
        var del = PhpPlanQ1Scan.EpcClpVhostRemoveAliasesFromPhpBackend((string)add["vhost"]!, ["www.stylenlook.com", "extra.test"], "www.ecomae.com");
        var src = "# EPC_TENANT_DIRECT_START\nserver {\n  listen 443 ssl;\n  server_name www.stylenlook.com;\n  {{root}}\n  {{ssl_certificate}}\n  {{ssl_certificate_key}}\n  location ~ \\.php$ {\n    fastcgi_pass 127.0.0.1:9000;\n  }\n}\n# EPC_TENANT_DIRECT_END\nserver {\n  listen 443 ssl;\n  server_name keep.test;\n  ssl_certificate /old.crt;\n}\n";
        var root = PhpPlanQ1Scan.EpcClpVhostPatchTenantDirectRoot(src, doc).Replace(doc, "DOCROOT", StringComparison.Ordinal);
        var fail = PhpPlanQ1Scan.EpcClpVhostPatchFailoverSplash(src, doc, []);
        var failNamed = PhpPlanQ1Scan.EpcClpVhostPatchFailoverSplash(src, doc, ["keep.test"]);
        var ssl = PhpPlanQ1Scan.EpcClpVhostPatchServerSslForHosts(src, ["www.stylenlook.com"], "www.stylenlook.com", true);
        var rm = PhpPlanQ1Scan.EpcClpVhostRemoveServerBlocksForHosts(src, ["keep.test"]);
        var emptyFail = PhpPlanQ1Scan.EpcClpVhostPatchFailoverSplash(src, "", ["www.stylenlook.com"]);
        return new Rendered(new object?[]
        {
            add["vhost"],
            add["log"],
            del["vhost"],
            del["log"],
            root,
            fail["patched"],
            fail["log"],
            Count((string)fail["vhost"]!, "epc-platform-splash.html"),
            failNamed["patched"],
            ssl["patched"],
            ssl["log"],
            ((string)ssl["vhost"]!).Contains("/etc/nginx/ssl-certificates/www.stylenlook.com.crt", StringComparison.Ordinal),
            rm["removed"],
            rm["log"],
            emptyFail
        });
    }

    private static int Count(string hay, string needle)
    {
        var n = 0;
        var i = 0;
        while (true)
        {
            var at = hay.IndexOf(needle, i, StringComparison.Ordinal);
            if (at < 0)
            {
                return n;
            }

            n++;
            i = at + needle.Length;
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
