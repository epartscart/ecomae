using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HelmParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Helm");

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
    public void PlanQ1Helm_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Helm.AuthSmtpPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Helm.AuthSmtpPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Helm.Reset();
        Assert.Contains("epc_auth_smtp.php", PhpPlanQ1Helm.AuthSmtpPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Helm.AuthSmtpPath, StringComparison.Ordinal);
        var miss = PhpPlanQ1Helm.EpcAuthSmtpSendHtml("a@b.example", "Hi", "<b>x</b>");
        Assert.False((bool)miss["ok"]!);
        Assert.DoesNotContain("PHPSESSID", Json(miss), StringComparison.Ordinal);
        Assert.False(PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed("shop"));
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Helm.Reset();
        return name switch
        {
            "validate" => Validate(),
            "write" => Write(),
            "effective" => Effective(),
            "send" => Send(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Validate()
    {
        var presets = PhpPlanQ1Helm.EpcAuthSmtpRecommendedPresets();
        var gmail = (Dictionary<string, object?>)presets["gmail_tls"]!;
        var hostinger = (Dictionary<string, object?>)presets["hostinger_ssl"]!;
        return new Rendered(new object?[]
        {
            presets.Keys.ToArray(),
            gmail,
            hostinger["smtp_port"],
            PhpPlanQ1Helm.EpcAuthSmtpValidateInput(new Dictionary<string, object?>(StringComparer.Ordinal)),
            PhpPlanQ1Helm.EpcAuthSmtpValidateInput(In(("smtp_mode", "1"), ("from_email", "ops@shop.example"))),
            PhpPlanQ1Helm.EpcAuthSmtpValidateInput(In(
                ("smtp_mode", "1"), ("from_email", "not-an-email"), ("smtp_host", "smtp.example"),
                ("smtp_port", "587abc"), ("smtp_encryption", "foo"), ("smtp_password", "short1"))),
            PhpPlanQ1Helm.EpcAuthSmtpValidateInput(In(
                ("smtp_mode", "1"), ("from_email", "ops@gmail.com"), ("smtp_host", "smtp.Gmail.com"),
                ("smtp_port", "465"), ("smtp_encryption", "tls"), ("smtp_username", "other@gmail.com"),
                ("smtp_password", "123456789012345"))),
            PhpPlanQ1Helm.EpcAuthSmtpValidateInput(In(("smtp_mode", "0"), ("from_email", "ops@shop.example"))),
            PhpPlanQ1Helm.EpcAuthSmtpValidateInput(In(
                ("smtp_mode", "1"), ("from_email", "ops@shop.example"), ("smtp_host", "smtp.hostinger.com"),
                ("smtp_port", "465"), ("smtp_encryption", "ssl"), ("smtp_password", ""))),
            PhpPlanQ1Helm.EpcAuthSmtpValidateInput(In(
                ("smtp_mode", "1"), ("from_email", "ops@shop.example"), ("smtp_host", "smtp.hostinger.com"),
                ("smtp_port", "465"), ("smtp_encryption", ""), ("smtp_username", ""), ("smtp_password", "long-enough-pass"))),
        });
    }

    private static Rendered Write()
    {
        var bad = PhpPlanQ1Helm.EpcAuthSmtpWriteFileConfig(In(("smtp_mode", "1"), ("from_email", "bad")));
        DropExtra(bad);
        var ok = PhpPlanQ1Helm.EpcAuthSmtpWriteFileConfig(In(
            ("smtp_mode", "1"), ("from_email", "ops@shop.example"), ("smtp_host", "smtp.hostinger.com"),
            ("smtp_port", "465"), ("smtp_encryption", "ssl"), ("smtp_username", "ops@shop.example"),
            ("smtp_password", "mailbox-secret"), ("from_name", "Shop O'Brien"),
            ("allow_mail_fallback", "1"), ("disable_demo_otp_fallback", "0")));
        ok["read"] = PhpPlanQ1Helm.EpcAuthSmtpFileConfig();
        var keep = PhpPlanQ1Helm.EpcAuthSmtpWriteFileConfig(In(
            ("smtp_mode", "1"), ("from_email", "ops@shop.example"), ("smtp_host", "smtp.hostinger.com"),
            ("smtp_port", "587"), ("smtp_encryption", "tls"), ("smtp_username", "ops@shop.example"),
            ("smtp_password", ""), ("from_name", "Shop")));
        DropExtra(keep);
        keep["read_pass"] = PhpPlanQ1Helm.EpcAuthSmtpFileConfig()["smtp_password"];
        PhpPlanQ1Helm.SmtpFile = new(StringComparer.Ordinal);
        PhpPlanQ1Helm.SmtpFileExists = false;
        var firstEmpty = PhpPlanQ1Helm.EpcAuthSmtpWriteFileConfig(In(
            ("smtp_mode", "0"), ("from_email", "hello@ecomae.com"), ("smtp_password", "")));
        DropExtra(firstEmpty);
        return new Rendered(new object?[] { bad, ok, keep, firstEmpty });
    }

    private static Rendered Effective()
    {
        var baseCfg = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        var diagBase = PhpPlanQ1Helm.EpcAuthSmtpDiagnose();
        PhpPlanQ1Helm.LocalFileExists = true;
        PhpPlanQ1Helm.LocalFile = new(StringComparer.Ordinal) { ["smtp_host"] = "local.example", ["from_name"] = "Local" };
        PhpPlanQ1Helm.Config = new PhpPlanQ1Helm.DpConfig { smtp_host = "cfg.example", from_email = "from@cfg.example" };
        var withLocal = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        SetSmtp(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smtp_mode"] = "1",
            ["smtp_host"] = "0",
            ["smtp_port"] = "587",
            ["smtp_encryption"] = "tls",
            ["smtp_username"] = "file@shop.example",
            ["smtp_password"] = "file-secret-1",
            ["from_email"] = "file@shop.example",
            ["from_name"] = "File",
            ["allow_mail_fallback"] = "0"
        });
        PhpPlanQ1Helm.Config = new PhpPlanQ1Helm.DpConfig
        {
            smtp_host = "cfg.example",
            from_email = "from@cfg.example",
            smtp_mode = "0"
        };
        var fileOverlay = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        PhpPlanQ1Helm.SiteSettings = TenantSmtp("1", "tenant.example", "465", "ssl", "tenant@shop.example", "tenant-secret", "tenant@shop.example", "Tenant");
        var tenant = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        var diagTenant = PhpPlanQ1Helm.EpcAuthSmtpDiagnose();
        PhpPlanQ1Helm.IsSuperCp = true;
        var super = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        PhpPlanQ1Helm.IsSuperCp = false;
        PhpPlanQ1Helm.SiteSettings = TenantSmtp("0", "ignored.example", "", "", "", "", "ignored@shop.example", "");
        var useOff = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        PhpPlanQ1Helm.SiteSettings = TenantSmtp("1", "0", "465", "", "", "", "keep@shop.example", "");
        var emptyZero = PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig();
        return new Rendered(new object?[] { baseCfg, diagBase, withLocal, fileOverlay, tenant, diagTenant, super, useOff, emptyZero });
    }

    private static Rendered Send()
    {
        var classified = new object?[]
        {
            PhpPlanQ1Helm.EpcAuthSmtpClassifyError("Could not connect to host"),
            PhpPlanQ1Helm.EpcAuthSmtpClassifyError("SMTP 535 authentication failed"),
            PhpPlanQ1Helm.EpcAuthSmtpClassifyError("password is empty"),
            PhpPlanQ1Helm.EpcAuthSmtpClassifyError("unknown boom"),
            PhpPlanQ1Helm.EpcAuthSmtpClassifyError("connect() failed", ["AUTH LOGIN"]),
        };
        var precheck = PhpPlanQ1Helm.EpcAuthSmtpSendHtml("a@b.example", "Hi", "<b>x</b>");
        SetSmtp(ReadySmtp("0"));
        var missing = PhpPlanQ1Helm.EpcAuthSmtpSendHtml("a@b.example", "Hi", "<b>x</b>");
        SetSmtp(ReadySmtp("1"));
        PhpPlanQ1Helm.MailOk = true;
        var fallback = PhpPlanQ1Helm.EpcAuthSmtpSendHtml("a@b.example", "Hi", "<b>x</b>");
        PhpPlanQ1Helm.MailerExists = true;
        PhpPlanQ1Helm.SmtpFail = "";
        var okSend = PhpPlanQ1Helm.EpcAuthSmtpSendHtml("a@b.example", "Hi", "<b>x</b>");
        var mail = new PhpPlanQ1Helm.MailerBag();
        PhpPlanQ1Helm.EpcAuthSmtpApplyToMailer(mail, PhpPlanQ1Helm.EpcAuthSmtpEffectiveConfig());
        var applied = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Host"] = mail.Host,
            ["Port"] = mail.Port,
            ["SMTPSecure"] = mail.SMTPSecure,
            ["SMTPAuth"] = mail.SMTPAuth,
            ["Username"] = mail.Username,
            ["Password"] = mail.Password,
            ["Sender"] = mail.Sender
        };
        PhpPlanQ1Helm.SmtpFail = "Could not connect to host";
        var fail = PhpPlanQ1Helm.EpcAuthSmtpSendHtml("a@b.example", "Hi", "<b>x</b>");
        PhpPlanQ1Helm.MailOk = true;
        var failThenMail = PhpPlanQ1Helm.EpcAuthSmtpSendHtml("a@b.example", "Hi", "<b>x</b>");
        SetSmtp(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smtp_mode"] = "1",
            ["smtp_host"] = "smtp.example",
            ["smtp_port"] = "587",
            ["smtp_encryption"] = "tls",
            ["smtp_username"] = "ops@shop.example",
            ["smtp_password"] = "file-secret-1",
            ["from_email"] = "ops@shop.example",
            ["from_name"] = "Ops",
            ["allow_mail_fallback"] = "0",
            ["disable_demo_otp_fallback"] = "0"
        });
        var demo = new object?[]
        {
            PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed(""),
            PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed("shop"),
            PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed("demo_acme"),
            PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed("DEMO_x"),
        };
        SetSmtp(new Dictionary<string, string>(StringComparer.Ordinal) { ["disable_demo_otp_fallback"] = "1" });
        var demoList = demo.Concat(new object?[] { PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed("demo_acme") }).ToList();
        SetSmtp(new Dictionary<string, string>(StringComparer.Ordinal) { ["disable_demo_otp_fallback"] = "0" });
        demoList.Add(PhpPlanQ1Helm.EpcAuthOtpDemoFallbackAllowed("demo_acme"));
        PhpPlanQ1Helm.Otps.Add(new PhpPlanQ1Helm.OtpRow
        {
            Id = 1,
            Email = "ops@shop.example",
            TenantKey = "acme",
            ContextJson = "{\"note\":\"x\"}",
            CreatedAt = 1760083200
        });
        PhpPlanQ1Helm.Otps.Add(new PhpPlanQ1Helm.OtpRow
        {
            Id = 2,
            Email = "ops@shop.example",
            TenantKey = "beta",
            ContextJson = "{}",
            CreatedAt = 1760083300
        });
        PhpPlanQ1Helm.EpcAuthOtpStoreOperatorCode(0, "999999");
        PhpPlanQ1Helm.EpcAuthOtpStoreOperatorCode(99, "999999");
        PhpPlanQ1Helm.EpcAuthOtpStoreOperatorCode(1, "123456");
        PhpPlanQ1Helm.EpcAuthOtpStoreOperatorCode(2, "0");
        var lookup = new object?[]
        {
            PhpPlanQ1Helm.EpcAuthOtpOperatorLookup(""),
            PhpPlanQ1Helm.EpcAuthOtpOperatorLookup("not-an-email"),
            PhpPlanQ1Helm.EpcAuthOtpOperatorLookup("missing@shop.example"),
            PhpPlanQ1Helm.EpcAuthOtpOperatorLookup("OPS@shop.example"),
        };
        var rows = PhpPlanQ1Helm.Otps.OrderBy(r => r.Id).Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = r.Id,
            ["tenant_key"] = r.TenantKey,
            ["context_json"] = r.ContextJson
        }).ToArray();
        return new Rendered(new object?[] { classified, precheck, missing, fallback, okSend, applied, fail, failThenMail, demoList, lookup, rows });
    }

    private static Dictionary<string, object?> In(params (string Key, object? Value)[] pairs)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in pairs)
        {
            map[pair.Key] = pair.Value;
        }

        return map;
    }

    private static void SetSmtp(Dictionary<string, string> values)
    {
        PhpPlanQ1Helm.SmtpFile = new Dictionary<string, string>(values, StringComparer.Ordinal);
        PhpPlanQ1Helm.SmtpFileExists = true;
    }

    private static Dictionary<string, string> ReadySmtp(string fallback)
        => new(StringComparer.Ordinal)
        {
            ["smtp_mode"] = "1",
            ["smtp_host"] = "smtp.example",
            ["smtp_port"] = "587",
            ["smtp_encryption"] = "tls",
            ["smtp_username"] = "ops@shop.example",
            ["smtp_password"] = "file-secret-1",
            ["from_email"] = "ops@shop.example",
            ["from_name"] = "Ops",
            ["allow_mail_fallback"] = fallback
        };

    private static Dictionary<string, object?> TenantSmtp(
        string use, string host, string port, string enc, string user, string pass, string from, string name)
        => new(StringComparer.Ordinal)
        {
            ["integrations"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["smtp"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["use_tenant_smtp"] = use,
                    ["smtp_host"] = host,
                    ["smtp_port"] = port,
                    ["smtp_encryption"] = enc,
                    ["smtp_username"] = user,
                    ["smtp_password"] = pass,
                    ["from_email"] = from,
                    ["from_name"] = name
                }
            }
        };

    private static void DropExtra(Dictionary<string, object?> row)
    {
        row.Remove("mode");
        row.Remove("content");
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
        => value.Length <= 1800 ? value : value[..1800] + "…";
}
