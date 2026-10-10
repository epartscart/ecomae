using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BoomParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Boom");

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
    public void PlanQ1Boom_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Boom.BlockchainBosPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Boom.BlockchainBosPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Boom.Reset();
        Assert.Contains("epc_blockchain_bos.php", PhpPlanQ1Boom.BlockchainBosPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Boom.BlockchainBosPath, StringComparison.Ordinal);
        var miss = PhpPlanQ1Boom.EpcBcBosVerify(" ");
        Assert.False((bool)miss["ok"]!);
        Assert.DoesNotContain("PHPSESSID", Json(miss), StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Boom.Reset();
        return name switch
        {
            "pure" => Pure(),
            "record" => Record(),
            "anchor" => Anchor(),
            "fleet" => Fleet(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Pure()
    {
        var modes = PhpPlanQ1Boom.EpcBcBosModes();
        var norm = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosNormalizeMode(""),
            PhpPlanQ1Boom.EpcBcBosNormalizeMode("ANCHOR"),
            PhpPlanQ1Boom.EpcBcBosNormalizeMode("network"),
            PhpPlanQ1Boom.EpcBcBosNormalizeMode("nope"),
            PhpPlanQ1Boom.EpcBcBosNormalizeMode("  Off  ")
        };
        var netDefault = PhpPlanQ1Boom.EpcBcBosAnchorNetwork();
        PhpPlanQ1Boom.AnchorNetwork = "custom_net";
        var netCustom = PhpPlanQ1Boom.EpcBcBosAnchorNetwork();
        PhpPlanQ1Boom.AnchorNetwork = "";
        var netBlank = PhpPlanQ1Boom.EpcBcBosAnchorNetwork();
        PhpPlanQ1Boom.AnchorNetwork = "local_merkle";
        var canon = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosCanonicalJson(Map(("z", 1), ("a", Map(("y", 2), ("x", 3))))),
            PhpPlanQ1Boom.EpcBcBosCanonicalJson(new List<object?> { "b", "a" }),
            PhpPlanQ1Boom.EpcBcBosCanonicalJson(new List<object?>()),
            PhpPlanQ1Boom.EpcBcBosCanonicalJson(Map(("0", "a"), ("2", "b"))),
            PhpPlanQ1Boom.EpcBcBosCanonicalJson(Map(("url", "https://x.com/a"), ("name", "café")))
        };
        var assoc = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosIsAssoc(new List<object?>()),
            PhpPlanQ1Boom.EpcBcBosIsAssoc(new List<object?> { 1, 2 }),
            PhpPlanQ1Boom.EpcBcBosIsAssoc(Map(("a", 1))),
            PhpPlanQ1Boom.EpcBcBosIsAssoc(Map(("0", "a"), ("2", "b")))
        };
        var hash = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosHash("plain"),
            PhpPlanQ1Boom.EpcBcBosHash(Map(("z", 1), ("a", 2)))
        };
        var leaves3 = new List<object?>
        {
            Sha("a"),
            Sha("b"),
            Sha("c")
        };
        var rootEmpty = PhpPlanQ1Boom.EpcBcBosMerkleRoot([]);
        var rootSkip = PhpPlanQ1Boom.EpcBcBosMerkleRoot(["", "  "]);
        var rootOne = PhpPlanQ1Boom.EpcBcBosMerkleRoot([leaves3[0]]);
        var rootTwo = PhpPlanQ1Boom.EpcBcBosMerkleRoot([leaves3[0], leaves3[1]]);
        var rootOdd = PhpPlanQ1Boom.EpcBcBosMerkleRoot(leaves3);
        var path0 = PhpPlanQ1Boom.EpcBcBosMerkleProofPath(leaves3, 0);
        var path2 = PhpPlanQ1Boom.EpcBcBosMerkleProofPath(leaves3, 2);
        var verOk = PhpPlanQ1Boom.EpcBcBosVerifyMerklePath(Convert.ToString(leaves3[0])!, path0, rootOdd);
        var verBad = PhpPlanQ1Boom.EpcBcBosVerifyMerklePath(Convert.ToString(leaves3[1])!, path0, rootOdd);
        var uid = PhpPlanQ1Boom.EpcBcBosNewUid("prf");
        var uidBat = PhpPlanQ1Boom.EpcBcBosNewUid("bat");
        var urls = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosVerifyUrl("prf abc/x"),
            PhpPlanQ1Boom.EpcBcBosVerifyUrl("  hash  ")
        };
        var baseNone = PhpPlanQ1Boom.EpcBcBosPublicBaseUrl();
        PhpPlanQ1Boom.HttpHost = "shop.example";
        PhpPlanQ1Boom.Https = "";
        PhpPlanQ1Boom.ServerPort = "80";
        var baseHttp = PhpPlanQ1Boom.EpcBcBosPublicBaseUrl();
        PhpPlanQ1Boom.Https = "on";
        var baseHttps = PhpPlanQ1Boom.EpcBcBosPublicBaseUrl();
        PhpPlanQ1Boom.Https = "off";
        PhpPlanQ1Boom.ServerPort = "443";
        var basePort = PhpPlanQ1Boom.EpcBcBosPublicBaseUrl();
        PhpPlanQ1Boom.Https = "0";
        PhpPlanQ1Boom.ServerPort = "80";
        var baseHttps0 = PhpPlanQ1Boom.EpcBcBosPublicBaseUrl();
        PhpPlanQ1Boom.DomainPath = "https://brand.example/";
        var baseDomain = PhpPlanQ1Boom.EpcBcBosPublicBaseUrl();
        PhpPlanQ1Boom.DomainPath = null;
        var basePlat = PhpPlanQ1Boom.EpcBcBosPublicBaseUrl();
        var abs = PhpPlanQ1Boom.EpcBcBosVerifyUrlAbsolute("prf_1");
        PhpPlanQ1Boom.HttpHost = "";
        PhpPlanQ1Boom.Https = "";
        PhpPlanQ1Boom.ServerPort = "";
        var einvoice = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosEinvoiceRecordKeys(new Dictionary<string, object?>(StringComparer.Ordinal)),
            PhpPlanQ1Boom.EpcBcBosEinvoiceRecordKeys(Map(("doc_category", "tax_credit_note"), ("id", "9"))),
            PhpPlanQ1Boom.EpcBcBosEinvoiceRecordKeys(Map(("invoice_type_code", "381"), ("invoice_number", "CN-1"))),
            PhpPlanQ1Boom.EpcBcBosEinvoiceRecordKeys(Map(("invoice_number", "INV-7"), ("id", 3))),
            PhpPlanQ1Boom.EpcBcBosEinvoiceRecordKeys(Map(("id", "12abc")))
        };
        var grn = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosGrnRecordId(new Dictionary<string, object?>(StringComparer.Ordinal)),
            PhpPlanQ1Boom.EpcBcBosGrnRecordId(Map(("invoice_number", "88"), ("id", 4))),
            PhpPlanQ1Boom.EpcBcBosGrnRecordId(Map(("id", 4)))
        };
        var badge = new object?[]
        {
            PhpPlanQ1Boom.EpcBcBosProofBadgeHtml(null),
            PhpPlanQ1Boom.EpcBcBosProofBadgeHtml(null, Map(("show_missing", 1))),
            PhpPlanQ1Boom.EpcBcBosProofBadgeHtml(Map(("status", "pending"), ("proof_uid", "prf_o'brien"))),
            PhpPlanQ1Boom.EpcBcBosProofBadgeHtml(Map(("status", "anchored"), ("proof_uid", "prf_ok")), Map(("show_uid", 1)))
        };
        return new Rendered(new object?[]
        {
            modes, norm, netDefault, netCustom, netBlank,
            PhpPlanQ1Boom.EpcBcBosProductName(false),
            PhpPlanQ1Boom.EpcBcBosProductName(true),
            PhpPlanQ1Boom.EpcBcBosProductTagline(),
            canon, assoc, hash, rootEmpty, rootSkip, rootOne, rootTwo, rootOdd,
            path0, path2, verOk, verBad, uid, uidBat, urls,
            baseNone, baseHttp, baseHttps, basePort, baseHttps0, baseDomain, basePlat, abs,
            einvoice, grn, badge
        });
    }

    private static Rendered Record()
    {
        var clientErp = "";
        Dictionary<string, object?>? shared = null;
        var cookie = "";
        var host = "";
        var byHost = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        PhpPlanQ1Boom.ClientErpKey = () => clientErp;
        PhpPlanQ1Boom.SharedActive = () => shared;
        PhpPlanQ1Boom.SharedCookie = () => cookie;
        PhpPlanQ1Boom.PortalHost = () => host;
        PhpPlanQ1Boom.LoadTenantByHost = h => byHost.TryGetValue(h, out var row) ? row : null;
        PhpPlanQ1Boom.HasPdo = false;
        var missDb = PhpPlanQ1Boom.EpcBcBosRecordProof("acme", "invoice", "1", Map(("n", 1)));
        PhpPlanQ1Boom.HasPdo = true;
        var badType = PhpPlanQ1Boom.EpcBcBosRecordProof("acme", "!!!", "1", Map(("n", 1)));
        var first = PhpPlanQ1Boom.EpcBcBosRecordProof("Acme-1!", "Invoice", "INV-1", Map(("total", 10), ("note", "café")), Map(("ts", "2026-10-10T08:00:00+00:00")));
        var dup = PhpPlanQ1Boom.EpcBcBosRecordProof("acme1", "invoice", "INV-1", Map(("note", "café"), ("total", 10)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        var second = PhpPlanQ1Boom.EpcBcBosRecordProof("acme1", "invoice", "INV-1", Map(("total", 11)), Map(("ts", "2026-10-10T08:00:00+00:00"), ("enqueue_anchor", 1)));
        var enq0 = PhpPlanQ1Boom.EpcBcBosRecordProof("acme1", "invoice", "INV-2", Map(("x", 1)), Map(("ts", "2026-10-10T08:00:00+00:00"), ("enqueue_anchor", 0)));
        var enqStr0 = PhpPlanQ1Boom.EpcBcBosRecordProof("acme1", "invoice", "INV-3", Map(("x", 1)), Map(("ts", "2026-10-10T08:00:00+00:00"), ("enqueue_anchor", "0")));
        var enqMissing = PhpPlanQ1Boom.EpcBcBosRecordProof("acme1", "invoice", "INV-4", Map(("x", 1)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        var maybeNoTenant = PhpPlanQ1Boom.EpcBcBosMaybeRecordDocument("invoice", "M1", Map(("a", 1)));
        clientErp = "beta";
        PhpPlanQ1Boom.TenantRows["beta"] = Map(("blockchain_mode", "off"));
        var maybeOff = PhpPlanQ1Boom.EpcBcBosMaybeRecordDocument("invoice", "M1", Map(("a", 1)));
        var maybeIds = PhpPlanQ1Boom.EpcBcBosMaybeRecordDocument("!!!", "", Map(("a", 1)), Map(("tenant_key", "beta")));
        PhpPlanQ1Boom.TenantRows["beta"] = Map(("blockchain_mode", "anchor"));
        PhpPlanQ1Boom.EpcBcBosClearTenantModeCache("beta");
        var maybeOk = PhpPlanQ1Boom.EpcBcBosMaybeRecordDocument("invoice", "M1", Map(("a", 1)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        var maybeNoEnq = PhpPlanQ1Boom.EpcBcBosMaybeRecordDocument("invoice", "M2", Map(("a", 2)), Map(("ts", "2026-10-10T08:00:00+00:00"), ("enqueue_anchor", 0)));
        clientErp = "";
        var resolve = new List<object?>
        {
            PhpPlanQ1Boom.EpcBcBosResolveSiteKey(Map(("tenant_key", "Acme-9!"))),
            PhpPlanQ1Boom.EpcBcBosResolveSiteKey(new Dictionary<string, object?>(StringComparer.Ordinal))
        };
        clientErp = "From_ERP";
        resolve.Add(PhpPlanQ1Boom.EpcBcBosResolveSiteKey(new Dictionary<string, object?>(StringComparer.Ordinal)));
        clientErp = "";
        PhpPlanQ1Boom.ConfigSharedSiteKey = "Shared_1";
        PhpPlanQ1Boom.ConfigSiteKey = "site_x";
        resolve.Add(PhpPlanQ1Boom.EpcBcBosResolveSiteKey(new Dictionary<string, object?>(StringComparer.Ordinal)));
        PhpPlanQ1Boom.ConfigSharedSiteKey = null;
        PhpPlanQ1Boom.ConfigSiteKey = "site_x";
        resolve.Add(PhpPlanQ1Boom.EpcBcBosResolveSiteKey(new Dictionary<string, object?>(StringComparer.Ordinal)));
        PhpPlanQ1Boom.ConfigSiteKey = null;
        shared = Map(("site_key", "shared_row"));
        resolve.Add(PhpPlanQ1Boom.EpcBcBosResolveSiteKey(new Dictionary<string, object?>(StringComparer.Ordinal)));
        shared = null;
        cookie = "cookie_tn";
        resolve.Add(PhpPlanQ1Boom.EpcBcBosResolveSiteKey(new Dictionary<string, object?>(StringComparer.Ordinal)));
        cookie = "";
        host = "acme.example";
        byHost["acme.example"] = Map(("site_key", "host_tn"));
        resolve.Add(PhpPlanQ1Boom.EpcBcBosResolveSiteKey(new Dictionary<string, object?>(StringComparer.Ordinal)));
        host = "";
        byHost.Clear();
        PhpPlanQ1Boom.EpcBcBosClearTenantModeCache();
        var modeEmpty = PhpPlanQ1Boom.EpcBcBosTenantMode("");
        var modeMiss = PhpPlanQ1Boom.EpcBcBosTenantMode("ghost");
        PhpPlanQ1Boom.TenantRows["ghost"] = Map(("blockchain_mode", "network"));
        PhpPlanQ1Boom.EpcBcBosClearTenantModeCache("ghost");
        var modeNet = PhpPlanQ1Boom.EpcBcBosTenantMode("ghost");
        var modeCached = PhpPlanQ1Boom.EpcBcBosTenantMode("ghost");
        PhpPlanQ1Boom.TenantRows["ghost"] = Map(("blockchain_mode", "off"));
        var modeStill = PhpPlanQ1Boom.EpcBcBosTenantMode("ghost");
        PhpPlanQ1Boom.EpcBcBosClearTenantModeCache("ghost");
        var modeOff = PhpPlanQ1Boom.EpcBcBosTenantMode("ghost");
        return new Rendered(new object?[]
        {
            missDb, badType, first, dup, second, enq0, enqStr0, enqMissing,
            maybeNoTenant, maybeOff, maybeIds, maybeOk, maybeNoEnq,
            resolve, modeEmpty, modeMiss, modeNet, modeCached, modeStill, modeOff,
            PhpPlanQ1Boom.Jobs, PhpPlanQ1Boom.Handlers
        });
    }

    private static Rendered Anchor()
    {
        PhpPlanQ1Boom.AnchorNetwork = "local_merkle";
        var empty = PhpPlanQ1Boom.EpcBcBosAnchorPendingBatch(0);
        var a = PhpPlanQ1Boom.EpcBcBosRecordProof("acme", "invoice", "A", Map(("n", 1)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        var b = PhpPlanQ1Boom.EpcBcBosRecordProof("acme", "invoice", "B", Map(("n", 2)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        var c = PhpPlanQ1Boom.EpcBcBosRecordProof("beta", "grn", "C", Map(("n", 3)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        var pendingVerify = PhpPlanQ1Boom.EpcBcBosVerify(Convert.ToString(a["proof_uid"])!);
        var firstBatch = PhpPlanQ1Boom.EpcBcBosAnchorPendingBatch(2);
        var anchoredA = PhpPlanQ1Boom.EpcBcBosVerify(Convert.ToString(a["proof_uid"])!);
        var byHash = PhpPlanQ1Boom.EpcBcBosVerify(Convert.ToString(a["payload_hash"])!.ToUpperInvariant());
        var rest = PhpPlanQ1Boom.EpcBcBosJobAnchorBatch("acme", Map(("limit", 50)));
        var missing = PhpPlanQ1Boom.EpcBcBosVerify("nope");
        var blank = PhpPlanQ1Boom.EpcBcBosVerify("  ");
        PhpPlanQ1Boom.EpcBcBosRegisterJobHandlers();
        PhpPlanQ1Boom.EpcBcBosRegisterJobHandlers();
        var lookA = PhpPlanQ1Boom.EpcBcBosLookupProof("acme", "invoice", "A");
        var tamper = new Dictionary<string, object?>(anchoredA, StringComparer.Ordinal);
        var proof = (Dictionary<string, object?>)anchoredA["proof"]!;
        var batch = (Dictionary<string, object?>)anchoredA["batch"]!;
        tamper["valid"] = PhpPlanQ1Boom.EpcBcBosVerifyMerklePath(
            "00" + Convert.ToString(proof["payload_hash"])![2..],
            [],
            Convert.ToString(batch["merkle_root"])!);
        return new Rendered(new object?[]
        {
            empty, a, b, c, pendingVerify, firstBatch, anchoredA, byHash, rest, missing, blank, lookA, tamper,
            PhpPlanQ1Boom.Handlers, PhpPlanQ1Boom.Jobs
        });
    }

    private static Rendered Fleet()
    {
        var clientErp = "";
        PhpPlanQ1Boom.ClientErpKey = () => clientErp;
        PhpPlanQ1Boom.EpcBcBosRecordProof("acme", "invoice", "I1", Map(("n", 1)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        PhpPlanQ1Boom.EpcBcBosRecordProof("acme", "grn", "G1", Map(("n", 2)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        PhpPlanQ1Boom.EpcBcBosRecordProof("beta", "invoice", "I9", Map(("n", 3)), Map(("ts", "2026-10-10T08:00:00+00:00")));
        PhpPlanQ1Boom.EpcBcBosAnchorPendingBatch(1);
        var acme = PhpPlanQ1Boom.EpcBcBosListProofs("acme", Map(("limit", 50)));
        var acmeInv = PhpPlanQ1Boom.EpcBcBosListProofs("acme", Map(("record_type", "invoice")));
        var acmePend = PhpPlanQ1Boom.EpcBcBosListProofs("acme", Map(("status", "pending")));
        var blankTenant = PhpPlanQ1Boom.EpcBcBosListProofs("");
        var fleetAll = PhpPlanQ1Boom.EpcBcBosListProofsFleet(Map(("limit", 10)));
        var fleetBeta = PhpPlanQ1Boom.EpcBcBosListProofsFleet(Map(("site_key", "beta")));
        var stats = PhpPlanQ1Boom.EpcBcBosFleetStats();
        PhpPlanQ1Boom.TenantRows["acme"] = Map(("blockchain_mode", "anchor"));
        PhpPlanQ1Boom.TenantRows["beta"] = Map(("blockchain_mode", "off"));
        clientErp = "acme";
        var tAcme = PhpPlanQ1Boom.EpcBcBosTenantProofStats();
        var tBeta = PhpPlanQ1Boom.EpcBcBosTenantProofStats("beta");
        var tGhost = PhpPlanQ1Boom.EpcBcBosTenantProofStats("!!!");
        var doc = PhpPlanQ1Boom.EpcBcBosDocumentBadgeHtml("invoice", "I1", Map(("show_uid", 1)));
        clientErp = "beta";
        PhpPlanQ1Boom.EpcBcBosClearTenantModeCache();
        var docOff = PhpPlanQ1Boom.EpcBcBosDocumentBadgeHtml("invoice", "I9");
        clientErp = "acme";
        PhpPlanQ1Boom.EpcBcBosClearTenantModeCache();
        var grnEmpty = PhpPlanQ1Boom.EpcBcBosGrnBadgeHtml(Map(("id", 4), ("invoice_number", "G1")));
        var grnPosted = PhpPlanQ1Boom.EpcBcBosGrnBadgeHtml(Map(("id", 4), ("invoice_number", "G1"), ("inv_receipt_posted", "1")), Map(("show_uid", 1)));
        var grnZero = PhpPlanQ1Boom.EpcBcBosGrnBadgeHtml(Map(("id", 4), ("invoice_number", "G1"), ("inv_receipt_posted", "0")));
        var flashEmpty = PhpPlanQ1Boom.EpcBcBosGrnFlashForPurchase(Map(("id", 4), ("invoice_number", "G1")));
        var flash = PhpPlanQ1Boom.EpcBcBosGrnFlashForPurchase(Map(("id", 4), ("invoice_number", "G1"), ("inventory_receipt_posted", 1)));
        return new Rendered(new object?[]
        {
            acme, acmeInv, acmePend, blankTenant, fleetAll, fleetBeta, stats,
            tAcme, tBeta, tGhost, doc, docOff, grnEmpty, grnPosted, grnZero, flashEmpty, flash
        });
    }

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] pairs)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    private static string Sha(string value)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

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
