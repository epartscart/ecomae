using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1HoldParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hold");

    private const string SamlOk = """
        <?xml version="1.0"?>
        <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
          <samlp:Status><samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Success"/></samlp:Status>
          <saml:Assertion>
            <saml:Subject><saml:NameID>ann@x.com</saml:NameID></saml:Subject>
            <saml:AuthnStatement SessionIndex="SI1"/>
            <saml:AttributeStatement>
              <saml:Attribute Name="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"><saml:AttributeValue>ann@x.com</saml:AttributeValue></saml:Attribute>
              <saml:Attribute Name="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname"><saml:AttributeValue>Ann</saml:AttributeValue></saml:Attribute>
              <saml:Attribute Name="http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname"><saml:AttributeValue>Lee</saml:AttributeValue></saml:Attribute>
              <saml:Attribute Name="http://schemas.microsoft.com/ws/2008/06/identity/claims/role"><saml:AttributeValue>buyer</saml:AttributeValue></saml:Attribute>
            </saml:AttributeStatement>
          </saml:Assertion>
        </samlp:Response>
        """;

    private const string SamlFail = """
        <?xml version="1.0"?>
        <samlp:Response xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion">
          <samlp:Status><samlp:StatusCode Value="urn:oasis:names:tc:SAML:2.0:status:Responder"/></samlp:Status>
        </samlp:Response>
        """;

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
    public void PlanQ1Hold_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Hold.SsoSamlPath, PhpPlanQ1Hold.BosHealthPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Hold.SsoSamlPath, PhpPlanQ1Hold.BosHealthPath });

    [Fact]
    public void Metadata_DoesNotStartASession()
    {
        PhpPlanQ1Hold.Reset();
        Assert.Equal("https://www.ecomae.com/saml/sp/acme", PhpPlanQ1Hold.EpcSsoSpMetadata("acme")["entity_id"]);
    }

    private static string Render(string name)
    {
        PhpPlanQ1Hold.Reset();
        return name switch
        {
            "sso_pure" => Json(SsoPure()),
            "sso_db" => Json(SsoDb()),
            "bos_pure" => Json(BosPure()),
            "bos_db" => Json(BosDb()),
            _ => "unknown:" + name
        };
    }

    private static object? SsoPure()
    {
        var m = PhpPlanQ1Hold.EpcSsoSpMetadata("Acme-Site");
        var xml = PhpPlanQ1Hold.EpcSsoSpMetadataXml("Acme-Site");
        var ar = PhpPlanQ1Hold.EpcSsoAuthnRequest("Acme-Site", new Dictionary<string, object?> { ["sso_url"] = "https://idp.example.com/sso?x=1&y=2" });
        ar["request_id"] = "FIXED";
        ar["raw_request"] = System.Text.RegularExpressions.Regex.Replace(Str(ar["raw_request"]), "ID=\"[^\"]+\"", "ID=\"FIXED\"");
        ar["raw_request"] = System.Text.RegularExpressions.Regex.Replace(Str(ar["raw_request"]), "IssueInstant=\"[^\"]+\"", "IssueInstant=\"FIXED\"");
        var redir = Str(ar["redirect_url"]);
        ar["redirect_url"] = redir.StartsWith("https://idp.example.com/sso", StringComparison.Ordinal) && redir.Contains("SAMLRequest=", StringComparison.Ordinal)
            ? "HAS_SAML"
            : "NO";
        return new object?[] { PhpPlanQ1Hold.EpcSsoSamlVersion, m, xml, ar };
    }

    private static object? SsoDb()
    {
        PhpPlanQ1Hold.UtcNow = () => new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        var db = new PhpPlanQ1Hold.SsoStore();
        PhpPlanQ1Hold.EpcSsoEnsureSchema(db);
        var miss = PhpPlanQ1Hold.EpcSsoProviderGet(db, 9);
        var c1 = PhpPlanQ1Hold.EpcSsoProviderCreate(db, "acme", new Dictionary<string, object?>
        {
            ["provider_name"] = "Zed IdP",
            ["sso_url"] = "https://idp.z/sso",
            ["active"] = 0,
            ["created_by"] = 4
        });
        var c2 = PhpPlanQ1Hold.EpcSsoProviderCreate(db, "acme", new Dictionary<string, object?>
        {
            ["provider_name"] = "Acme IdP",
            ["sso_url"] = "https://idp.a/sso",
            ["active"] = 1,
            ["jit_provision"] = 0,
            ["default_role"] = "staff"
        });
        var c3 = PhpPlanQ1Hold.EpcSsoProviderCreate(db, "other", new Dictionary<string, object?> { ["provider_name"] = "Other" });
        var hit = (Dictionary<string, object?>)PhpPlanQ1Hold.EpcSsoProviderGet(db, Convert.ToInt32(c2["provider_id"]));
        hit.Remove("created_at");
        hit.Remove("updated_at");
        hit.Remove("certificate");
        hit.Remove("metadata_xml");
        var list = PhpPlanQ1Hold.EpcSsoProviderList(db, "acme");
        foreach (var r in list)
        {
            r.Remove("updated_at");
        }

        var tog = PhpPlanQ1Hold.EpcSsoProviderToggle(db, Convert.ToInt32(c1["provider_id"]), true);
        var del = PhpPlanQ1Hold.EpcSsoProviderDelete(db, Convert.ToInt32(c3["provider_id"]));
        PhpPlanQ1Hold.Server["REMOTE_ADDR"] = "10.1.2.3";
        PhpPlanQ1Hold.Server["HTTP_USER_AGENT"] = "Mozilla/5.0";
        var okB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(SamlOk));
        var failB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(SamlFail));
        var badB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("not-xml"));
        var ok = PhpPlanQ1Hold.EpcSsoProcessResponse(db, "acme", okB64, Convert.ToInt32(c2["provider_id"]));
        var nof = PhpPlanQ1Hold.EpcSsoProcessResponse(db, "acme", okB64, 99);
        var bad = PhpPlanQ1Hold.EpcSsoProcessResponse(db, "acme", badB64, Convert.ToInt32(c2["provider_id"]));
        var fail = PhpPlanQ1Hold.EpcSsoProcessResponse(db, "acme", failB64, Convert.ToInt32(c2["provider_id"]));
        var sess = PhpPlanQ1Hold.EpcSsoActiveSessions(db, "acme");
        var lo = PhpPlanQ1Hold.EpcSsoLogout(db, Convert.ToInt32(ok["session_id"]));
        db.Sessions.Add(new PhpPlanQ1Hold.SessionRow
        {
            Id = db.NextSessionId++,
            SiteKey = "acme",
            ProviderId = 1,
            NameId = "x",
            Email = "x@x.com",
            AttributesJson = "{}",
            Status = "active",
            ExpiresAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        var exp = PhpPlanQ1Hold.EpcSsoExpireOld(db);
        var fleet = PhpPlanQ1Hold.EpcSsoFleetStats(db);
        return new object?[] { miss, c1, c2, hit, list, tog, del, ok, nof, bad, fail, sess, lo, exp, fleet };
    }

    private static object? BosPure()
        => new object?[]
        {
            PhpPlanQ1Hold.EpcBosHealthSummary(Array.Empty<Dictionary<string, object?>>()),
            PhpPlanQ1Hold.EpcBosHealthSummary(
            [
                new() { ["score"] = 0 },
                new() { ["score"] = 50 },
                new() { ["score"] = 80 },
                new() { ["score"] = 100 },
                new() { ["score"] = 49 }
            ])
        };

    private static object? BosDb()
    {
        var db = new PhpPlanQ1Hold.HealthStore();
        db.Tenants.AddRange(
        [
            new() { SiteKey = "missingx", TradeName = "ZZ", DbName = "tenant_x", Hostname = "", IsActive = 1 },
            new() { SiteKey = "nodb", TradeName = "NoDB", DbName = "", Hostname = "h.example", IsActive = 1 },
            new() { SiteKey = "failc", TradeName = "FailC", DbName = "tenant_f", Hostname = "f.example", IsActive = 1 },
            new() { SiteKey = "liveok", TradeName = "LiveOK", DbName = "tenant_live", Hostname = "shop.example.com", IsActive = 1 },
            new() { SiteKey = "off", TradeName = "Off", DbName = "tenant_off", Hostname = "o.example", IsActive = 0 },
            new() { SiteKey = "", TradeName = "Blank", DbName = "x", Hostname = "x", IsActive = 1 }
        ]);
        var live = new PhpPlanQ1Hold.TenantDb { Connected = true, AdminUsers = 1, ActiveLanguages = 1 };
        foreach (var t in new[] { "users", "sessions", "pages", "modules", "control_groups", "control_items",
                     "epc_erp_coa_accounts", "epc_erp_gl_journals", "epc_erp_cash_bank_accounts", "epc_erp_suppliers", "epc_erp_audit_log", "lang_languages" })
        {
            live.Tables[t] = t == "users" ? 2 : t == "sessions" ? 1 : t == "lang_languages" ? 2 : 0;
        }

        db.Dbs["liveok"] = live;
        PhpPlanQ1Hold.TenantConnect = key => key == "liveok" && db.Dbs.TryGetValue(key, out var tdb) ? (tdb, "") : (null, "denied");
        var r1 = PhpPlanQ1Hold.EpcBosHealthCheckTenant(db, "nope");
        var r2 = PhpPlanQ1Hold.EpcBosHealthCheckTenant(db, "nodb");
        var r3 = PhpPlanQ1Hold.EpcBosHealthCheckTenant(db, "failc");
        var r4 = PhpPlanQ1Hold.EpcBosHealthCheckTenant(db, "liveok");
        var all = PhpPlanQ1Hold.EpcBosHealthCheckAll(db);
        var sum = PhpPlanQ1Hold.EpcBosHealthSummary(all);
        var names = ((List<Dictionary<string, object?>>)r4["checks"]!).Select(c => c["name"]).ToList();
        var statuses = ((List<Dictionary<string, object?>>)r4["checks"]!).Select(c => c["status"]).ToList();
        var pairs = all.Select(r => new object?[] { r["tenant"], r["score"] }).ToList();
        return new object?[] { r1, r2, r3, r4["score"], r4["tenant"], names, statuses, pairs, sum };
    }

    private static string Str(object? value) => Convert.ToString(value) ?? "";

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
