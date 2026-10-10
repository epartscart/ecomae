using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-cove social publish helpers. PHP identifiers kept for the inventory:
/// <c>epc_social_graph_version</c>, <c>epc_social_http_json</c>,
/// <c>epc_social_account_credentials</c>, <c>epc_social_account_public_meta</c>,
/// <c>epc_social_mark_account_test</c>, <c>epc_social_compose_caption</c>,
/// <c>epc_social_is_video_url</c>, <c>epc_social_is_image_url</c>,
/// <c>epc_social_test_account_live</c>, <c>epc_social_meta_test_facebook</c>,
/// <c>epc_social_meta_test_instagram</c>, <c>epc_social_tiktok_test</c>,
/// <c>epc_social_publish_draft</c>, <c>epc_social_publish_now</c>,
/// <c>epc_social_meta_publish_facebook</c>, <c>epc_social_meta_publish_instagram</c>,
/// <c>epc_social_tiktok_publish_video</c>.
/// </summary>
public static class PhpPlanQ1Cove
{
    public const string SocialPublishPath = "content/social_media/epc_social_publish.php";

    public static string GraphVersionConfig { get; set; } = "";
    public static Func<long> UnixNow { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Action<int>? Sleep { get; set; }
    public static Func<string, string, string>? Decrypt { get; set; }
    public static Func<CoveStore, string, Dictionary<string, object?>, Dictionary<string, object?>>? SaveDraft { get; set; }
    public static Func<CurlReq, CurlHit>? Curl { get; set; }
    public static CurlReq? LastCurl { get; set; }

    public sealed record CurlReq(string Method, string Url, List<string> Headers, string? Body, bool Form, int Timeout);
    public sealed record CurlHit(int Http, string Raw, int Errno, string Error);

    public sealed class AccountRow
    {
        public string SiteKey { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Username { get; set; } = "";
        public string EncryptedCredentials { get; set; } = "";
        public string Status { get; set; } = "pending";
        public int LastTestAt { get; set; }
        public int LastTestOk { get; set; }
        public string MetaJson { get; set; } = "";
        public int UpdatedAt { get; set; }
    }

    public sealed class DraftRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Title { get; set; } = "";
        public string Caption { get; set; } = "";
        public string Hashtags { get; set; } = "";
        public string MediaUrl { get; set; } = "";
        public string Status { get; set; } = "draft";
        public string ExternalPostId { get; set; } = "";
        public int PublishedAt { get; set; }
        public string LastError { get; set; } = "";
        public int UpdatedAt { get; set; }
    }

    public sealed class CoveStore
    {
        public bool AccountsMissing { get; set; }
        public bool DraftsMissing { get; set; }
        public int NextDraftId { get; set; } = 1;
        public List<AccountRow> Accounts { get; } = [];
        public List<DraftRow> Drafts { get; } = [];
    }

    private static CoveStore Store { get; set; } = new();

    public static void UseStore(CoveStore store) => Store = store;

    public static void Reset()
    {
        GraphVersionConfig = "";
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Sleep = null;
        Decrypt = null;
        SaveDraft = null;
        Curl = null;
        LastCurl = null;
        Store = new();
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            0f => true,
            "" => true,
            "0" => true,
            JsonElement je when je.ValueKind is JsonValueKind.Null or JsonValueKind.False => true,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0 => true,
            JsonElement je when je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0") => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static Dictionary<string, object?>? AsDict(object? value)
    {
        if (value is Dictionary<string, object?> d)
        {
            return d;
        }

        if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var p in je.EnumerateObject())
            {
                map[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.Clone();
            }

            return map;
        }

        return null;
    }

    private static List<object?> AsList(object? value)
    {
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            return je.EnumerateArray().Select(x => (object?)(x.ValueKind == JsonValueKind.String ? x.GetString() : x.Clone())).ToList();
        }

        if (value is System.Collections.IEnumerable en and not string)
        {
            return en.Cast<object?>().ToList();
        }

        return [];
    }

    private static string PlatformKey(string raw)
        => Regex.Replace(raw.ToLowerInvariant(), "[^a-z0-9_]", "");

    /// <summary>PHP <c>epc_social_graph_version</c>.</summary>
    public static string EpcSocialGraphVersion()
    {
        var v = GraphVersionConfig.Trim();
        if (v == "")
        {
            v = "v21.0";
        }

        var clean = Regex.Replace(v, "[^a-zA-Z0-9.]", "");
        return clean == "" ? "v21.0" : clean;
    }

    /// <summary>PHP <c>epc_social_http_json</c>.</summary>
    public static Dictionary<string, object?> EpcSocialHttpJson(
        string method,
        string url,
        IReadOnlyList<string>? headers = null,
        object? body = null,
        int timeout = 45,
        Dictionary<string, object?>? options = null)
    {
        var hdr = new List<string> { "Accept: application/json" };
        if (headers != null)
        {
            hdr.AddRange(headers);
        }

        string? payload = null;
        var asForm = options != null && !PhpEmpty(options.TryGetValue("form", out var f) ? f : null);
        if (body != null)
        {
            if (asForm && body is System.Collections.IDictionary)
            {
                payload = PhpHttpBuildQuery(body);
                hdr.Add("Content-Type: application/x-www-form-urlencoded");
            }
            else if (body is System.Collections.IDictionary or System.Collections.IEnumerable and not string)
            {
                payload = PhpJson(body);
                if (!hdr.Any(h => h.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase)))
                {
                    hdr.Add("Content-Type: application/json");
                }
            }
            else
            {
                payload = PhpString(body);
            }
        }

        var req = new CurlReq(method.ToUpperInvariant(), url, hdr, payload, asForm, timeout);
        LastCurl = req;
        var hit = Curl?.Invoke(req) ?? new CurlHit(0, "", 1, "curl error");
        if (hit.Errno != 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["http"] = hit.Http,
                ["json"] = Array.Empty<object>(),
                ["raw"] = "",
                ["error"] = hit.Error != "" ? hit.Error : "curl error"
            };
        }

        var raw = hit.Raw.Length <= 2000 ? hit.Raw : hit.Raw[..2000];
        Dictionary<string, object?> json;
        try
        {
            using var doc = JsonDocument.Parse(hit.Raw);
            json = doc.RootElement.ValueKind == JsonValueKind.Object
                ? AsDict(doc.RootElement) ?? new Dictionary<string, object?>(StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            json = new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var apiErr = "";
        if (json.TryGetValue("error", out var errObj))
        {
            var err = AsDict(errObj);
            if (err != null && !PhpEmpty(err.TryGetValue("message", out var m) ? m : null))
            {
                apiErr = PhpString(m);
            }
            else if (err != null && !PhpEmpty(err.TryGetValue("error_description", out var d) ? d : null))
            {
                apiErr = PhpString(d);
            }
        }

        if (apiErr == "" && !PhpEmpty(json.TryGetValue("message", out var msg) ? msg : null) && hit.Http >= 400)
        {
            apiErr = PhpString(msg);
        }

        var ok = hit.Http >= 200 && hit.Http < 300 && apiErr == "";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = ok,
            ["http"] = hit.Http,
            ["json"] = json,
            ["raw"] = raw,
            ["error"] = ok ? "" : (apiErr != "" ? apiErr : ("HTTP " + hit.Http.ToString(CultureInfo.InvariantCulture)))
        };
    }

    /// <summary>PHP <c>epc_social_account_credentials</c>.</summary>
    public static object EpcSocialAccountCredentials(CoveStore db, string siteKey, string platform)
    {
        platform = PlatformKey(platform);
        if (db.AccountsMissing)
        {
            return Array.Empty<object>();
        }

        var row = db.Accounts.FirstOrDefault(a => a.SiteKey == siteKey && a.Platform == platform);
        if (row == null)
        {
            return Array.Empty<object>();
        }

        var plain = (Decrypt ?? ((enc, _) => enc))(row.EncryptedCredentials, siteKey);
        Dictionary<string, object?> cred;
        try
        {
            using var doc = JsonDocument.Parse(plain == "" ? "null" : plain);
            cred = AsDict(doc.RootElement) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            cred = new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        cred["username"] = row.Username;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["username"] = PhpString(cred.TryGetValue("username", out var u) ? u : ""),
            ["access_token"] = PhpString(cred.TryGetValue("access_token", out var t) ? t : "").Trim(),
            ["api_key"] = PhpString(cred.TryGetValue("api_key", out var k) ? k : "").Trim(),
            ["api_secret"] = PhpString(cred.TryGetValue("api_secret", out var s) ? s : "").Trim(),
            ["page_id"] = PhpString(cred.TryGetValue("page_id", out var p) ? p : "").Trim(),
            ["ig_user_id"] = PhpString(cred.TryGetValue("ig_user_id", out var ig) ? ig : "").Trim(),
            ["open_id"] = PhpString(cred.TryGetValue("open_id", out var o) ? o : "").Trim(),
            ["privacy_level"] = PhpString(cred.TryGetValue("privacy_level", out var pr) ? pr : "SELF_ONLY").Trim()
        };
    }

    /// <summary>PHP <c>epc_social_account_public_meta</c>.</summary>
    public static Dictionary<string, object?> EpcSocialAccountPublicMeta(CoveStore db, string siteKey, string platform)
    {
        var c = EpcSocialAccountCredentials(db, siteKey, platform) as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var token = PhpString(c.TryGetValue("access_token", out var t) ? t : "").Trim();
        var key = PhpString(c.TryGetValue("api_key", out var k) ? k : "").Trim();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["page_id"] = PhpString(c.TryGetValue("page_id", out var p) ? p : ""),
            ["ig_user_id"] = PhpString(c.TryGetValue("ig_user_id", out var ig) ? ig : ""),
            ["open_id"] = PhpString(c.TryGetValue("open_id", out var o) ? o : ""),
            ["privacy_level"] = PhpString(c.TryGetValue("privacy_level", out var pr) ? pr : "SELF_ONLY"),
            ["has_token"] = token != "" || key != ""
        };
    }

    /// <summary>PHP <c>epc_social_mark_account_test</c>.</summary>
    public static void EpcSocialMarkAccountTest(CoveStore db, string siteKey, string platform, bool ok, string message = "")
    {
        var now = (int)UnixNow();
        var meta = PhpJson(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["last_test_message"] = message,
            ["at"] = now
        });
        var row = db.Accounts.FirstOrDefault(a => a.SiteKey == siteKey && a.Platform == platform);
        if (row == null)
        {
            return;
        }

        row.LastTestAt = now;
        row.LastTestOk = ok ? 1 : 0;
        row.Status = ok ? "verified" : "error";
        row.MetaJson = meta;
        row.UpdatedAt = now;
    }

    /// <summary>PHP <c>epc_social_compose_caption</c>.</summary>
    public static string EpcSocialComposeCaption(string caption, string hashtags)
    {
        caption = caption.Trim();
        hashtags = hashtags.Trim();
        if (hashtags == "")
        {
            return caption;
        }

        return caption == "" ? hashtags : caption + "\n\n" + hashtags;
    }

    /// <summary>PHP <c>epc_social_is_video_url</c>.</summary>
    public static bool EpcSocialIsVideoUrl(string url)
        => PathEnds(url, [".mp4", ".mov", ".m4v", ".webm"]);

    /// <summary>PHP <c>epc_social_is_image_url</c>.</summary>
    public static bool EpcSocialIsImageUrl(string url)
        => PathEnds(url, [".jpg", ".jpeg", ".png", ".gif", ".webp"]);

    /// <summary>PHP <c>epc_social_test_account_live</c>.</summary>
    public static Dictionary<string, object?> EpcSocialTestAccountLive(CoveStore db, string siteKey, string platform)
    {
        platform = PlatformKey(platform);
        var credObj = EpcSocialAccountCredentials(db, siteKey, platform);
        var cred = credObj as Dictionary<string, object?>;
        if (cred == null || (PhpString(cred["access_token"]) == "" && PhpString(cred["api_key"]) == ""))
        {
            EpcSocialMarkAccountTest(db, siteKey, platform, false, "No credentials");
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "No account credentials for " + platform + ". Save an access token first."
            };
        }

        Dictionary<string, object?> result;
        if (platform == "facebook")
        {
            result = EpcSocialMetaTestFacebook(cred);
        }
        else if (platform == "instagram")
        {
            result = EpcSocialMetaTestInstagram(cred);
        }
        else if (platform == "tiktok")
        {
            result = EpcSocialTiktokTest(cred);
        }
        else if (platform is "linkedin" or "x")
        {
            EpcSocialMarkAccountTest(db, siteKey, platform, false, "Publish not supported");
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = char.ToUpperInvariant(platform[0]) + platform[1..] + " live publish is not enabled yet. Vault storage works — use Copy → native app for now."
            };
        }
        else
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Unknown platform."
            };
        }

        EpcSocialMarkAccountTest(db, siteKey, platform, !PhpEmpty(result.TryGetValue("ok", out var ok) ? ok : null), PhpString(result.TryGetValue("message", out var msg) ? msg : ""));
        return result;
    }

    /// <summary>PHP <c>epc_social_meta_test_facebook</c>.</summary>
    public static Dictionary<string, object?> EpcSocialMetaTestFacebook(Dictionary<string, object?> cred)
    {
        var token = PhpString(cred.TryGetValue("access_token", out var t) ? t : "");
        var pageId = PhpString(cred.TryGetValue("page_id", out var p) ? p : "");
        if (token == "")
        {
            return Fail("Facebook needs a Page access token.");
        }

        var v = EpcSocialGraphVersion();
        var url = pageId != ""
            ? "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(pageId) + "?fields=id,name&access_token=" + Uri.EscapeDataString(token)
            : "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/me?fields=id,name&access_token=" + Uri.EscapeDataString(token);
        var res = EpcSocialHttpJson("GET", url);
        var json = AsDict(res["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        if (PhpEmpty(res["ok"]) || PhpEmpty(json.TryGetValue("id", out var id) ? id : null))
        {
            var err = PhpString(res["error"]);
            return Fail("Facebook Graph test failed: " + (err != "" ? err : "invalid token"));
        }

        var name = PhpString(json.TryGetValue("name", out var n) ? n : json["id"]);
        var hint = pageId == "" ? " Tip: save Page / Business ID for publishing." : "";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = "Facebook Graph OK — connected as " + name + "." + hint
        };
    }

    /// <summary>PHP <c>epc_social_meta_test_instagram</c>.</summary>
    public static Dictionary<string, object?> EpcSocialMetaTestInstagram(Dictionary<string, object?> cred)
    {
        var token = PhpString(cred.TryGetValue("access_token", out var t) ? t : "");
        var igUserId = PhpString(cred.TryGetValue("ig_user_id", out var ig) ? ig : "");
        if (igUserId == "")
        {
            igUserId = PhpString(cred.TryGetValue("page_id", out var p) ? p : "");
        }

        if (token == "" || igUserId == "")
        {
            return Fail("Instagram needs access token + Instagram Business user ID (Page / Business ID field).");
        }

        var v = EpcSocialGraphVersion();
        var url = "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(igUserId)
            + "?fields=id,username&access_token=" + Uri.EscapeDataString(token);
        var res = EpcSocialHttpJson("GET", url);
        var json = AsDict(res["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        if (PhpEmpty(res["ok"]) || PhpEmpty(json.TryGetValue("id", out var id) ? id : null))
        {
            var err = PhpString(res["error"]);
            return Fail("Instagram Graph test failed: " + (err != "" ? err : "invalid token or IG user id"));
        }

        var uname = PhpString(json.TryGetValue("username", out var u) ? u : json["id"]);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = "Instagram Graph OK — @" + uname + " (Business/Creator)."
        };
    }

    /// <summary>PHP <c>epc_social_tiktok_test</c>.</summary>
    public static Dictionary<string, object?> EpcSocialTiktokTest(Dictionary<string, object?> cred)
    {
        var token = PhpString(cred.TryGetValue("access_token", out var t) ? t : "");
        if (token == "")
        {
            return Fail("TikTok needs a user access token with video.publish scope.");
        }

        var res = EpcSocialHttpJson("POST", "https://open.tiktokapis.com/v2/post/publish/creator_info/query/", ["Authorization: Bearer " + token], Array.Empty<object>());
        if (PhpEmpty(res["ok"]))
        {
            var err = PhpString(res["error"]);
            return Fail("TikTok API test failed: " + (err != "" ? err : "token rejected"));
        }

        var json = AsDict(res["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var data = AsDict(json.TryGetValue("data", out var d) ? d : null) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var name = PhpString(data.TryGetValue("creator_username", out var cu) ? cu : data.TryGetValue("creator_nickname", out var cn) ? cn : "creator");
        var opts = AsList(data.TryGetValue("privacy_level_options", out var po) ? po : null);

        var msg = "TikTok API OK — @" + name + ".";
        if (opts.Count > 0)
        {
            msg += " Allowed privacy: " + string.Join(", ", opts.Select(PhpString)) + ".";
        }

        msg += " Unaudited apps can only post SELF_ONLY (private).";
        var outDict = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = msg
        };
        if (opts.Count > 0)
        {
            outDict["privacy_options"] = opts;
        }

        return outDict;
    }

    /// <summary>PHP <c>epc_social_publish_draft</c>.</summary>
    public static Dictionary<string, object?> EpcSocialPublishDraft(CoveStore db, string siteKey, int draftId)
    {
        if (db.DraftsMissing)
        {
            return Fail("Draft not found.");
        }

        var draft = db.Drafts.FirstOrDefault(d => d.Id == draftId && d.SiteKey == siteKey);
        if (draft == null)
        {
            return Fail("Draft not found.");
        }

        var platform = PlatformKey(draft.Platform);
        var caption = EpcSocialComposeCaption(draft.Caption, draft.Hashtags);
        var mediaUrl = draft.MediaUrl.Trim();
        var now = (int)UnixNow();
        draft.Status = "publishing";
        draft.LastError = "";
        draft.UpdatedAt = now;

        Dictionary<string, object?> result;
        if (platform == "facebook")
        {
            result = EpcSocialMetaPublishFacebook(db, siteKey, caption, mediaUrl);
        }
        else if (platform == "instagram")
        {
            result = EpcSocialMetaPublishInstagram(db, siteKey, caption, mediaUrl);
        }
        else if (platform == "tiktok")
        {
            result = EpcSocialTiktokPublishVideo(db, siteKey, caption, mediaUrl);
        }
        else
        {
            result = Fail("Live publish supports Facebook, Instagram, and TikTok only.");
        }

        now = (int)UnixNow();
        if (!PhpEmpty(result.TryGetValue("ok", out var ok) ? ok : null))
        {
            draft.Status = "published";
            draft.ExternalPostId = PhpString(result.TryGetValue("external_post_id", out var xid) ? xid : "");
            draft.PublishedAt = now;
            draft.LastError = "";
            draft.UpdatedAt = now;
            result["platform"] = platform;
            return result;
        }

        draft.Status = "error";
        draft.LastError = PhpString(result.TryGetValue("message", out var msg) ? msg : "Publish failed");
        draft.UpdatedAt = now;
        return result;
    }

    /// <summary>PHP <c>epc_social_publish_now</c>.</summary>
    public static Dictionary<string, object?> EpcSocialPublishNow(CoveStore db, string siteKey, Dictionary<string, object?> data)
    {
        var platform = PlatformKey(PhpString(data.TryGetValue("platform", out var p) ? p : ""));
        var title = PhpString(data.TryGetValue("title", out var t) ? t : "Published post").Trim();
        var caption = PhpString(data.TryGetValue("caption", out var c) ? c : "").Trim();
        var hashtags = PhpString(data.TryGetValue("hashtags", out var h) ? h : "").Trim();
        var mediaUrl = PhpString(data.TryGetValue("media_url", out var m) ? m : "").Trim();
        if (platform == "" || (caption == "" && mediaUrl == ""))
        {
            return Fail("Platform plus caption or media URL is required.");
        }

        var save = (SaveDraft ?? DefaultSaveDraft)(db, siteKey, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["platform"] = platform,
            ["title"] = title != "" ? title : ("Publish " + platform),
            ["caption"] = caption,
            ["hashtags"] = hashtags,
            ["media_url"] = mediaUrl
        });
        if (PhpEmpty(save.TryGetValue("ok", out var sok) ? sok : null))
        {
            return save;
        }

        var draftId = db.Drafts.Where(d => d.SiteKey == siteKey).Select(d => d.Id).DefaultIfEmpty(0).Max();
        if (draftId <= 0)
        {
            return Fail("Could not create draft for publish.");
        }

        var result = EpcSocialPublishDraft(db, siteKey, draftId);
        result["draft_id"] = draftId;
        return result;
    }

    /// <summary>PHP <c>epc_social_meta_publish_facebook</c>.</summary>
    public static Dictionary<string, object?> EpcSocialMetaPublishFacebook(CoveStore db, string siteKey, string caption, string mediaUrl)
    {
        var cred = EpcSocialAccountCredentials(db, siteKey, "facebook") as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var token = PhpString(cred.TryGetValue("access_token", out var t) ? t : "");
        var pageId = PhpString(cred.TryGetValue("page_id", out var p) ? p : "");
        if (token == "" || pageId == "")
        {
            return Fail("Facebook publish needs Page access token + Page ID.");
        }

        var v = EpcSocialGraphVersion();
        string url;
        Dictionary<string, object?> payload;
        if (mediaUrl != "" && EpcSocialIsVideoUrl(mediaUrl))
        {
            url = "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(pageId) + "/videos";
            payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["file_url"] = mediaUrl,
                ["description"] = caption,
                ["access_token"] = token
            };
        }
        else if (mediaUrl != "")
        {
            url = "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(pageId) + "/photos";
            payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = mediaUrl,
                ["caption"] = caption,
                ["access_token"] = token
            };
        }
        else
        {
            url = "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(pageId) + "/feed";
            payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["message"] = caption,
                ["access_token"] = token
            };
        }

        var res = EpcSocialHttpJson("POST", url, [], payload, 60, new Dictionary<string, object?>(StringComparer.Ordinal) { ["form"] = true });
        if (PhpEmpty(res["ok"]))
        {
            var err = PhpString(res["error"]);
            return Fail("Facebook publish failed: " + (err != "" ? err : "API error"));
        }

        var json = AsDict(res["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var postId = PhpString(json.TryGetValue("id", out var id) ? id : json.TryGetValue("post_id", out var pid) ? pid : "");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = "Published to Facebook Page." + (postId != "" ? " ID: " + postId : ""),
            ["external_post_id"] = postId
        };
    }

    /// <summary>PHP <c>epc_social_meta_publish_instagram</c>.</summary>
    public static Dictionary<string, object?> EpcSocialMetaPublishInstagram(CoveStore db, string siteKey, string caption, string mediaUrl)
    {
        var cred = EpcSocialAccountCredentials(db, siteKey, "instagram") as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var token = PhpString(cred.TryGetValue("access_token", out var t) ? t : "");
        var igUserId = PhpString(cred.TryGetValue("ig_user_id", out var ig) ? ig : "");
        if (igUserId == "")
        {
            igUserId = PhpString(cred.TryGetValue("page_id", out var p) ? p : "");
        }

        if (token == "" || igUserId == "")
        {
            return Fail("Instagram publish needs access token + Instagram Business user ID.");
        }

        if (mediaUrl == "" || !Regex.IsMatch(mediaUrl, "^https?://", RegexOptions.IgnoreCase))
        {
            return Fail("Instagram requires a public https media_url (image or .mp4 Reel).");
        }

        var v = EpcSocialGraphVersion();
        var createUrl = "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(igUserId) + "/media";
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["caption"] = caption,
            ["access_token"] = token
        };
        if (EpcSocialIsVideoUrl(mediaUrl))
        {
            payload["media_type"] = "REELS";
            payload["video_url"] = mediaUrl;
            payload["share_to_feed"] = true;
        }
        else
        {
            payload["image_url"] = mediaUrl;
        }

        var create = EpcSocialHttpJson("POST", createUrl, [], payload, 60, new Dictionary<string, object?>(StringComparer.Ordinal) { ["form"] = true });
        var createJson = AsDict(create["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        if (PhpEmpty(create["ok"]) || PhpEmpty(createJson.TryGetValue("id", out var cid) ? cid : null))
        {
            var err = PhpString(create["error"]);
            return Fail("Instagram media create failed: " + (err != "" ? err : "no container id"));
        }

        var containerId = PhpString(cid);
        var statusUrl = "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(containerId)
            + "?fields=status_code,status&access_token=" + Uri.EscapeDataString(token);
        var ready = false;
        var lastStatus = "";
        for (var i = 0; i < 24; i++)
        {
            var poll = EpcSocialHttpJson("GET", statusUrl);
            var pollJson = AsDict(poll["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            lastStatus = PhpString(pollJson.TryGetValue("status_code", out var sc) ? sc : pollJson.TryGetValue("status", out var st) ? st : "");
            var up = lastStatus.ToUpperInvariant();
            if (up is "FINISHED" or "PUBLISHED")
            {
                ready = true;
                break;
            }

            if (up is "ERROR" or "EXPIRED")
            {
                return Fail("Instagram container error: " + lastStatus);
            }

            Sleep?.Invoke(EpcSocialIsVideoUrl(mediaUrl) ? 2500000 : 400000);
        }

        if (!ready && EpcSocialIsVideoUrl(mediaUrl))
        {
            return Fail("Instagram video still processing (status: " + (lastStatus != "" ? lastStatus : "unknown") + "). Try Publish again in a minute.");
        }

        var pubUrl = "https://graph.facebook.com/" + Uri.EscapeDataString(v) + "/" + Uri.EscapeDataString(igUserId) + "/media_publish";
        var pub = EpcSocialHttpJson("POST", pubUrl, [], new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["creation_id"] = containerId,
            ["access_token"] = token
        }, 60, new Dictionary<string, object?>(StringComparer.Ordinal) { ["form"] = true });
        var pubJson = AsDict(pub["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        if (PhpEmpty(pub["ok"]) || PhpEmpty(pubJson.TryGetValue("id", out var mid) ? mid : null))
        {
            var err = PhpString(pub["error"]);
            return Fail("Instagram media_publish failed: " + (err != "" ? err : "no media id"));
        }

        var mediaId = PhpString(mid);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = "Published to Instagram." + (mediaId != "" ? " Media ID: " + mediaId : ""),
            ["external_post_id"] = mediaId
        };
    }

    /// <summary>PHP <c>epc_social_tiktok_publish_video</c>.</summary>
    public static Dictionary<string, object?> EpcSocialTiktokPublishVideo(CoveStore db, string siteKey, string caption, string mediaUrl)
    {
        var cred = EpcSocialAccountCredentials(db, siteKey, "tiktok") as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var token = PhpString(cred.TryGetValue("access_token", out var t) ? t : "");
        if (token == "")
        {
            return Fail("TikTok publish needs a user access token.");
        }

        if (mediaUrl == "" || !Regex.IsMatch(mediaUrl, "^https?://", RegexOptions.IgnoreCase) || !EpcSocialIsVideoUrl(mediaUrl))
        {
            return Fail("TikTok requires a public https video URL (.mp4/.mov). Verify the domain in TikTok Developer Portal.");
        }

        var privacy = PhpString(cred.TryGetValue("privacy_level", out var pr) ? pr : "SELF_ONLY").ToUpperInvariant();
        string[] allowed = ["PUBLIC_TO_EVERYONE", "MUTUAL_FOLLOW_FRIENDS", "FOLLOWER_OF_CREATOR", "SELF_ONLY"];
        if (!allowed.Contains(privacy, StringComparer.Ordinal))
        {
            privacy = "SELF_ONLY";
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["post_info"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = MbSubstr(caption, 2200),
                ["privacy_level"] = privacy,
                ["disable_duet"] = false,
                ["disable_comment"] = false,
                ["disable_stitch"] = false,
                ["video_cover_timestamp_ms"] = 1000
            },
            ["source_info"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = "PULL_FROM_URL",
                ["video_url"] = mediaUrl
            }
        };
        var init = EpcSocialHttpJson("POST", "https://open.tiktokapis.com/v2/post/publish/video/init/",
            ["Authorization: Bearer " + token, "Content-Type: application/json; charset=UTF-8"], payload, 60);
        if (PhpEmpty(init["ok"]))
        {
            var err = PhpString(init["error"]);
            return Fail("TikTok init failed: " + (err != "" ? err : "API error") + " Unaudited apps must use privacy SELF_ONLY and a private TikTok account.");
        }

        var initJson = AsDict(init["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var data = AsDict(initJson.TryGetValue("data", out var d) ? d : null) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var publishId = PhpString(data.TryGetValue("publish_id", out var pid) ? pid : "");
        if (publishId == "")
        {
            return Fail("TikTok init returned no publish_id.");
        }

        var last = "";
        Dictionary<string, object?> pollJson = new(StringComparer.Ordinal);
        for (var i = 0; i < 20; i++)
        {
            var poll = EpcSocialHttpJson("POST", "https://open.tiktokapis.com/v2/post/publish/status/fetch/",
                ["Authorization: Bearer " + token, "Content-Type: application/json; charset=UTF-8"],
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["publish_id"] = publishId });
            pollJson = AsDict(poll["json"]) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            var pollData = AsDict(pollJson.TryGetValue("data", out var pd) ? pd : null) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            last = PhpString(pollData.TryGetValue("status", out var st) ? st : "");
            if (last is "PUBLISH_COMPLETE" or "FAILED" or "SEND_TO_USER_INBOX")
            {
                break;
            }

            Sleep?.Invoke(2000000);
        }

        if (last == "FAILED")
        {
            var pollData = AsDict(pollJson.TryGetValue("data", out var pd) ? pd : null) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            var failMsg = PhpString(pollData.TryGetValue("fail_reason", out var fr) ? fr : "publish failed");
            return Fail("TikTok publish failed: " + failMsg);
        }

        if (last == "SEND_TO_USER_INBOX")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["message"] = "TikTok sent video to creator inbox (inbox mode). Open TikTok app to finish posting.",
                ["external_post_id"] = publishId
            };
        }

        if (last != "PUBLISH_COMPLETE" && last != "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["message"] = "TikTok accepted publish (status: " + last + "). Check the TikTok app if not visible yet.",
                ["external_post_id"] = publishId
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = "Published to TikTok (" + privacy + "). Publish ID: " + publishId,
            ["external_post_id"] = publishId
        };
    }

    private static Dictionary<string, object?> DefaultSaveDraft(CoveStore db, string siteKey, Dictionary<string, object?> data)
    {
        db.Drafts.Add(new DraftRow
        {
            Id = db.NextDraftId++,
            SiteKey = siteKey,
            Platform = PlatformKey(PhpString(data.TryGetValue("platform", out var p) ? p : "")),
            Title = PhpString(data.TryGetValue("title", out var t) ? t : "Untitled draft").Trim(),
            Caption = PhpString(data.TryGetValue("caption", out var c) ? c : "").Trim(),
            Hashtags = PhpString(data.TryGetValue("hashtags", out var h) ? h : "").Trim(),
            MediaUrl = PhpString(data.TryGetValue("media_url", out var m) ? m : "").Trim(),
            Status = "draft",
            UpdatedAt = (int)UnixNow()
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "Draft saved." };
    }

    private static Dictionary<string, object?> Fail(string message)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["message"] = message };

    private static bool PathEnds(string url, string[] exts)
    {
        var path = "";
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                path = uri.AbsolutePath;
            }
            else
            {
                var cut = url.IndexOfAny(['?', '#']);
                path = cut < 0 ? url : url[..cut];
            }
        }
        catch (UriFormatException)
        {
            path = url;
        }

        path = path.ToLowerInvariant();
        return exts.Any(ext => path.EndsWith(ext, StringComparison.Ordinal));
    }

    private static string MbSubstr(string value, int maxChars)
        => value.Length <= maxChars ? value : value[..maxChars];

    private static string PhpJson(object? value)
        => JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    private static string PhpHttpBuildQuery(object? body)
    {
        var pairs = new List<string>();
        if (body is System.Collections.IDictionary map)
        {
            foreach (System.Collections.DictionaryEntry kv in map)
            {
                pairs.Add(PhpUrlEncode(PhpString(kv.Key)) + "=" + PhpUrlEncode(PhpString(kv.Value)));
            }
        }

        return string.Join("&", pairs);
    }

    private static string PhpUrlEncode(string value)
        => Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);
}
