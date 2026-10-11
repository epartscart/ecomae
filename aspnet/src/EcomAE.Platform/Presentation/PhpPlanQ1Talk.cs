using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-talk helpers. PHP identifiers kept for the inventory:
/// <c>epc_comm_test_definitions</c>, <c>epc_comm_test_last_json_path</c>,
/// <c>epc_comm_test_load_last</c>, <c>epc_comm_test_save_last</c>,
/// <c>epc_comm_notify_row</c>, <c>epc_comm_answer_summary</c>,
/// <c>epc_comm_record_test</c>, <c>epc_comm_test_ensure_customer</c>,
/// <c>epc_comm_test_create_order</c>,
/// <c>epc_storefront_anti_crawl_client_ip</c>, <c>epc_storefront_anti_crawl_is_bot</c>,
/// <c>epc_storefront_anti_crawl_has_tech_key</c>, <c>epc_storefront_anti_crawl_session_user_id</c>,
/// <c>epc_storefront_anti_crawl_rate_limit</c>, <c>epc_storefront_anti_crawl_enforce</c>,
/// <c>epc_storefront_anti_crawl_deny</c>, <c>epc_storefront_anti_crawl_redact_cross_stock</c>,
/// <c>epc_storefront_anti_crawl_resolve_pricing_identity</c>.
/// </summary>
public static class PhpPlanQ1Talk
{
    public const string CommTestPath = "content/shop/usefull/epc_order_communication_test.php";
    public const string AntiCrawlPath = "content/shop/docpart/epc_storefront_anti_crawl.php";
    public const string CommPasswordPlain = "EpcCommTest2026!";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions PrettyJsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private static readonly Regex RateFileKeep = new("[^a-zA-Z0-9._-]", RegexOptions.CultureInvariant);
    private static readonly Regex SentLog = new(@": sent\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex FailedLog = new(@": FAILED\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] BotNeedles =
    [
        "bot", "crawl", "spider", "slurp", "scrapy", "curl/", "wget", "python-requests",
        "python-urllib", "httpclient", "libwww", "httpunit", "nutch", "httrack",
        "phantomjs", "headlesschrome", "headless", "selenium", "puppeteer", "playwright",
        "axios/", "go-http-client", "java/", "okhttp", "node-fetch", "postmanruntime",
        "insomnia", "apache-httpclient", "mechanize", "beautifulsoup", "http.rb",
        "aiohttp", "facebookexternalhit", "bytespider", "gptbot", "claudebot",
        "ccbot", "anthropic", "petalbot", "semrush", "ahrefs", "mj12bot", "dotbot",
        "dataforseo", "serpstat", "screaming frog", "siteauditbot", "bingpreview",
        "yandex", "baiduspider", "duckduckbot", "applebot", "ia_archiver",
        "googlebot", "adsbot-google", "mediapartners-google", "apis-google",
        "storebot-google", "google-inspectiontool", "chrome-lighthouse",
        "pingdom", "uptimerobot", "statuscake", "monitor"
    ];

    public static string DocumentRoot { get; set; } = "";
    public static string TempDir { get; set; } = "";
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Dictionary<string, string> Server { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, string> Post { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, string> Get { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, string> Request { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static TalkConfig? GlobalConfig { get; set; }
    public static Func<int> SessionUser { get; set; } = () => 0;
    public static Func<Dictionary<string, object?>?> UserProfile { get; set; } = () => null;
    public static Func<int, bool> PricesVisible { get; set; } = _ => false;
    public static Func<string> SensitiveMask { get; set; } = () => "**";
    public static Func<object?, string, bool?>? NotifyStatus { get; set; } = DefaultNotifyStatus;
    public static Func<object?, int, string> TradeStatus { get; set; } = (_, _) => "approved";
    public static List<object?[]> TradeSets { get; } = new();
    public static bool HeadersSent { get; set; } = true;
    public static List<string> LastHeaders { get; } = new();
    public static string LastBody { get; set; } = "";
    public static int LastStatus { get; set; }
    public static bool LastExited { get; set; }

    public static void Reset()
    {
        DocumentRoot = "";
        TempDir = "";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Server.Clear();
        Post.Clear();
        Get.Clear();
        Request.Clear();
        GlobalConfig = null;
        SessionUser = () => 0;
        UserProfile = () => null;
        PricesVisible = _ => false;
        SensitiveMask = () => "**";
        NotifyStatus = DefaultNotifyStatus;
        TradeStatus = (_, _) => "approved";
        TradeSets.Clear();
        HeadersSent = true;
        LastHeaders.Clear();
        LastBody = "";
        LastStatus = 0;
        LastExited = false;
    }

    public sealed class TalkConfig
    {
        public string TechKey { get; set; } = "";
        public string SecretSuccession { get; set; } = "";
    }

    public sealed class CommStore
    {
        public int NextUserId { get; set; } = 1;
        public int NextOrderId { get; set; } = 1;
        public int NextItemId { get; set; } = 1;
        public int NextNotifyId { get; set; } = 1;
        public List<UserRow> Users { get; } = new();
        public List<GroupRow> Groups { get; } = new();
        public List<BindRow> Binds { get; } = new();
        public List<NotifyRow> Notifications { get; } = new();
        public List<StatusRow> OrderStatuses { get; } = new();
        public List<StatusRow> ItemStatuses { get; } = new();
        public List<StorageRow> Storages { get; } = new();
        public List<OrderRow> Orders { get; } = new();
        public List<ItemRow> Items { get; } = new();
        public List<DetailRow> Details { get; } = new();
        public List<LogRow> Logs { get; } = new();
    }

    public sealed class UserRow
    {
        public int UserId { get; set; }
        public int RegVariant { get; set; }
        public string Email { get; set; } = "";
        public int EmailConfirmed { get; set; }
        public string Phone { get; set; } = "";
        public int PhoneConfirmed { get; set; }
        public string Password { get; set; } = "";
        public int Unlocked { get; set; }
        public long TimeRegistered { get; set; }
        public int AdminCreated { get; set; }
    }

    public sealed class GroupRow
    {
        public int Id { get; set; }
        public int ForRegistrated { get; set; }
    }

    public sealed class BindRow
    {
        public int UserId { get; set; }
        public int GroupId { get; set; }
    }

    public sealed class NotifyRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Caption { get; set; } = "";
        public int EmailOn { get; set; }
        public int SmsOn { get; set; }
        public int SendForNotConfirmed { get; set; }
    }

    public sealed class StatusRow
    {
        public int Id { get; set; }
        public int ForCreated { get; set; }
        public int Order { get; set; }
    }

    public sealed class StorageRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class OrderRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SessionId { get; set; }
        public long Time { get; set; }
        public int SuccessfullyCreated { get; set; }
        public int Status { get; set; }
        public int Paid { get; set; }
        public int HowGet { get; set; }
        public string HowGetJson { get; set; } = "";
        public string PhoneNotAuth { get; set; } = "";
        public string EmailNotAuth { get; set; } = "";
        public int OfficeId { get; set; }
    }

    public sealed class ItemRow
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductType { get; set; }
        public double Price { get; set; }
        public int CountNeed { get; set; }
        public int ProductId { get; set; }
        public int Status { get; set; }
        public string Brand { get; set; } = "";
        public string Article { get; set; } = "";
        public string ArticleShow { get; set; } = "";
        public string Name { get; set; } = "";
        public int Exist { get; set; }
        public string Storage { get; set; } = "";
        public double PricePurchase { get; set; }
        public int OfficeId { get; set; }
        public int StorageId { get; set; }
    }

    public sealed class DetailRow
    {
        public int OrderId { get; set; }
        public int OrderItemId { get; set; }
        public int OfficeId { get; set; }
        public int StorageId { get; set; }
        public double PricePurchase { get; set; }
    }

    public sealed class LogRow
    {
        public int OrderId { get; set; }
        public long Time { get; set; }
        public int UserId { get; set; }
        public int IsManager { get; set; }
        public string Text { get; set; } = "";
        public int IsRobot { get; set; }
    }

    public static List<Dictionary<string, object?>> EpcCommTestDefinitions()
        =>
        [
            Def("new_order_to_manager", "E-mail", "Checkout — new order", "Admin + office managers + CRM", "Control panel → Notifications"),
            Def("new_order_to_user", "E-mail", "Checkout — new order", "Customer (registered or guest e-mail)", "Control panel → Notifications"),
            Def("lpo_to_supplier", "E-mail", "Checkout — new order", "Supplier inbox per warehouse (LPO # = order ID)", "Logistics → Warehouses → Supplier order email (LPO)"),
            Def("order_message_to_manager", "E-mail", "Customer sends message on order page", "Admin + office managers", "Order card → Messages"),
            Def("order_message_to_customer", "E-mail", "Manager replies on order page", "Customer", "Order card → Messages"),
            Def("order_status_to_manager", "E-mail", "Order status changed (if enabled on status)", "Managers", "Orders → Statuses → order status flags"),
            Def("order_status_to_customer", "E-mail", "Order status changed (if enabled on status)", "Customer", "Orders → Statuses → order status flags"),
            Def("order_item_status_to_manager", "E-mail", "Line item status changed (if enabled)", "Managers", "Orders → Statuses → line item status flags"),
            Def("order_item_status_to_customer", "E-mail", "Line item status changed (if enabled)", "Customer", "Orders → Statuses → line item status flags"),
            Def("order_pay_to_manager", "E-mail", "Payment recorded on order", "Managers", "Order card → Payment"),
            Def("order_pay_to_customer", "E-mail", "Payment recorded on order", "Customer", "Order card → Payment"),
            Def("reg_notify_admin", "E-mail", "New customer registration", "Admin / staff", "Storefront registration"),
            Def("vin_zapros", "E-mail", "VIN request form submitted", "Admin", "VIN request page")
        ];

    public static string EpcCommTestLastJsonPath()
        => DocRoot() + "/content/files/epc_communication_test_last.json";

    public static Dictionary<string, object?>? EpcCommTestLoadLast()
    {
        var path = EpcCommTestLastJsonPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
            return data.ValueKind == JsonValueKind.Object || data.ValueKind == JsonValueKind.Array
                ? JsonElementToPlain(data) as Dictionary<string, object?>
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void EpcCommTestSaveLast(object? report)
    {
        var path = EpcCommTestLastJsonPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(report, PrettyJsonOpts));
    }

    public static Dictionary<string, object?>? EpcCommNotifyRow(CommStore db, string name)
    {
        var row = db.Notifications.FirstOrDefault(n => n.Name == name);
        return row is null
            ? null
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = row.Id,
                ["name"] = row.Name,
                ["caption"] = row.Caption,
                ["email_on"] = row.EmailOn,
                ["sms_on"] = row.SmsOn,
                ["send_for_not_confirmed"] = row.SendForNotConfirmed
            };
    }

    public static Dictionary<string, object?> EpcCommAnswerSummary(object? answer, string matchEmail = "")
    {
        if (NotifyStatus is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = null,
                ["detail"] = "epc_notify_email_status unavailable"
            };
        }

        var ok = NotifyStatus(answer, matchEmail);
        var detail = "";
        if (answer is IReadOnlyDictionary<string, object?> dict && dict.TryGetValue("persons", out var personsObj) && !IsEmpty(personsObj))
        {
            foreach (var personObj in AsList(personsObj))
            {
                if (personObj is not IReadOnlyDictionary<string, object?> person)
                {
                    continue;
                }

                var email = Nested(person, "contacts", "email");
                if (email is null || IsEmpty(Field(email, "tried_to_send")))
                {
                    continue;
                }

                var em = email.TryGetValue("value", out var ev) && ev is not null
                    ? Convert.ToString(ev, CultureInfo.InvariantCulture) ?? ""
                    : "user#" + (person.TryGetValue("user_id", out var uid) && uid is not null
                        ? Convert.ToString(uid, CultureInfo.InvariantCulture)
                        : "");
                detail += em + "=" + (!IsEmpty(Field(email, "status")) ? "sent" : "FAILED") + "; ";
            }
        }

        if (detail == "" && answer is IReadOnlyDictionary<string, object?> msg)
        {
            detail = msg.TryGetValue("message", out var m) && m is not null
                ? Convert.ToString(m, CultureInfo.InvariantCulture) ?? ""
                : JsonSerializer.Serialize(answer, JsonOpts);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = ok,
            ["detail"] = detail.Trim()
        };
    }

    public static void EpcCommRecordTest(Dictionary<string, object?> report, string name, object? answer, string matchEmail = "", IReadOnlyDictionary<string, object?>? extra = null)
    {
        var summary = EpcCommAnswerSummary(answer, matchEmail);
        var sent = summary["ok"];
        extra ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        if (sent is null && extra.TryGetValue("log", out var logObj) && !IsEmpty(logObj))
        {
            var log = Convert.ToString(logObj, CultureInfo.InvariantCulture) ?? "";
            if (SentLog.IsMatch(log))
            {
                sent = true;
            }
            else if (FailedLog.IsMatch(log))
            {
                sent = false;
            }
        }

        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["sent"] = sent,
            ["detail"] = summary["detail"]
        };
        foreach (var kv in extra)
        {
            row[kv.Key] = kv.Value;
        }

        if (!report.TryGetValue("tests", out var testsObj) || testsObj is not List<Dictionary<string, object?>> tests)
        {
            tests = new List<Dictionary<string, object?>>();
            report["tests"] = tests;
        }

        tests.Add(row);
    }

    public static Dictionary<string, object?> EpcCommTestEnsureCustomer(CommStore db, TalkConfig cfg, string email, string phone)
    {
        email = email.Trim().ToLowerInvariant();
        var row = db.Users.FirstOrDefault(u => u.Email.ToLowerInvariant() == email);
        var passwordHash = Md5Hex(CommPasswordPlain + cfg.SecretSuccession);
        var created = false;
        int userId;
        if (row is null)
        {
            userId = db.NextUserId++;
            db.Users.Add(new UserRow
            {
                UserId = userId,
                RegVariant = 1,
                Email = email,
                EmailConfirmed = 1,
                Phone = phone,
                PhoneConfirmed = 1,
                Password = passwordHash,
                Unlocked = 1,
                TimeRegistered = Clock(),
                AdminCreated = 1
            });
            created = true;
        }
        else
        {
            userId = row.UserId;
            row.EmailConfirmed = 1;
            row.Unlocked = 1;
            row.Phone = phone;
            row.PhoneConfirmed = 1;
        }

        TradeSets.Add([userId, "epc_customer_type", "retail"]);
        TradeSets.Add([userId, "epc_trade_approval_status", "approved"]);
        TradeSets.Add([userId, "epc_dealing_currency", "AED"]);
        TradeSets.Add([userId, "name", "EPC Comm Test Customer"]);
        TradeSets.Add([userId, "surname", "Automated"]);

        var group = db.Groups.Where(g => g.ForRegistrated == 1).OrderBy(g => g.Id).Select(g => (int?)g.Id).FirstOrDefault()
                    ?? db.Groups.OrderBy(g => g.Id).Select(g => (int?)g.Id).FirstOrDefault();
        if (group is > 0)
        {
            db.Binds.RemoveAll(b => b.UserId == userId);
            db.Binds.Add(new BindRow { UserId = userId, GroupId = group.Value });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = userId,
            ["email"] = email,
            ["phone"] = phone,
            ["password"] = CommPasswordPlain,
            ["created"] = created,
            ["trade_status"] = TradeStatus(db, userId)
        };
    }

    public static Dictionary<string, object?> EpcCommTestCreateOrder(CommStore db, int userId, int officeId, IEnumerable<object?> storageIds)
    {
        var orderStatus = db.OrderStatuses.Where(s => s.ForCreated == 1).OrderBy(s => s.Order).Select(s => s.Id).FirstOrDefault();
        if (orderStatus <= 0)
        {
            throw new InvalidOperationException("No for_created order status");
        }

        var itemStatus = db.ItemStatuses.Where(s => s.ForCreated == 1).OrderBy(s => s.Order).Select(s => s.Id).FirstOrDefault();
        if (itemStatus <= 0)
        {
            throw new InvalidOperationException("No for_created item status");
        }

        var orderId = db.NextOrderId++;
        db.Orders.Add(new OrderRow
        {
            Id = orderId,
            UserId = userId,
            SessionId = 0,
            Time = Clock(),
            SuccessfullyCreated = 1,
            Status = orderStatus,
            Paid = 0,
            HowGet = 1,
            HowGetJson = "{}",
            PhoneNotAuth = "",
            EmailNotAuth = "",
            OfficeId = officeId
        });

        var storageNames = db.Storages.ToDictionary(s => s.Id, s => s.Name);
        var lines = new List<Dictionary<string, object?>>();
        var n = 0;
        foreach (var raw in storageIds)
        {
            var storageId = ToInt(raw);
            if (storageId <= 0)
            {
                continue;
            }

            n++;
            var brand = "TESTBRAND";
            var article = "EPCCOMM" + n;
            var whLabel = storageNames.TryGetValue(storageId, out var sn) ? sn : "WH" + storageId;
            var name = "EPC communication test part " + n + " (" + whLabel + ")";
            var itemId = db.NextItemId++;
            db.Items.Add(new ItemRow
            {
                Id = itemId,
                OrderId = orderId,
                ProductType = 2,
                Price = 99.50 + n,
                CountNeed = 1,
                ProductId = 0,
                Status = itemStatus,
                Brand = brand,
                Article = article,
                ArticleShow = article,
                Name = name,
                Exist = 10,
                Storage = whLabel,
                PricePurchase = 50.00 + n,
                OfficeId = officeId,
                StorageId = storageId
            });
            db.Details.Add(new DetailRow
            {
                OrderId = orderId,
                OrderItemId = itemId,
                OfficeId = officeId,
                StorageId = storageId,
                PricePurchase = 50.00 + n
            });
            lines.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["item_id"] = itemId,
                ["storage_id"] = storageId,
                ["storage_name"] = storageNames.TryGetValue(storageId, out var named) ? named : "",
                ["article"] = article
            });
        }

        db.Logs.Add(new LogRow
        {
            OrderId = orderId,
            Time = Clock(),
            UserId = 0,
            IsManager = 0,
            Text = "EPC communication test order created (safe to delete after review).",
            IsRobot = 1
        });

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["order_id"] = orderId,
            ["lines"] = lines,
            ["office_id"] = officeId
        };
    }

    public static string EpcStorefrontAntiCrawlClientIp()
    {
        var remote = Server.TryGetValue("REMOTE_ADDR", out var ra) ? ra ?? "" : "";
        var trustProxy = remote != "" && (!PhpPublicIp(remote) || remote == "127.0.0.1" || remote == "::1");
        var keys = trustProxy
            ? new[] { "HTTP_CF_CONNECTING_IP", "HTTP_X_FORWARDED_FOR", "REMOTE_ADDR" }
            : new[] { "REMOTE_ADDR" };
        foreach (var key in keys)
        {
            if (!Server.TryGetValue(key, out var rawObj) || IsEmpty(rawObj))
            {
                continue;
            }

            var raw = rawObj ?? "";
            if (key == "HTTP_X_FORWARDED_FOR")
            {
                var parts = raw.Split(',');
                raw = parts[0].Trim();
            }

            if (PhpValidIp(raw))
            {
                return raw;
            }
        }

        return remote != "" ? remote : "0.0.0.0";
    }

    public static bool EpcStorefrontAntiCrawlIsBot(string? ua = null)
    {
        var text = (ua ?? (Server.TryGetValue("HTTP_USER_AGENT", out var header) ? header : "") ?? "").Trim().ToLowerInvariant();
        if (text == "")
        {
            return true;
        }

        if (text.Contains("epartscart cp", StringComparison.Ordinal) || text.Contains("ecomae cp", StringComparison.Ordinal))
        {
            return false;
        }

        return BotNeedles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
    }

    public static bool EpcStorefrontAntiCrawlHasTechKey(TalkConfig? cfg = null)
    {
        cfg ??= GlobalConfig;
        if (cfg is null || IsEmpty(cfg.TechKey))
        {
            return false;
        }

        var provided = "";
        if (Post.TryGetValue("tech_key", out var p))
        {
            provided = p ?? "";
        }
        else if (Get.TryGetValue("tech_key", out var g))
        {
            provided = g ?? "";
        }
        else if (Request.TryGetValue("tech_key", out var r))
        {
            provided = r ?? "";
        }

        if (provided == "")
        {
            return false;
        }

        return FixedEquals(cfg.TechKey, provided);
    }

    public static int EpcStorefrontAntiCrawlSessionUserId()
    {
        try
        {
            return SessionUser();
        }
        catch
        {
            return 0;
        }
    }

    public static Dictionary<string, object?> EpcStorefrontAntiCrawlRateLimit(string bucket, int maxRequests, int windowSeconds)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["blocked"] = false,
            ["remaining"] = maxRequests,
            ["retry_after"] = 0,
            ["count"] = 0
        };
        var ip = EpcStorefrontAntiCrawlClientIp();
        var dir = Path.Combine(string.IsNullOrEmpty(TempDir) ? Path.GetTempPath() : TempDir, "epc_anti_crawl");
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
            return output;
        }

        var file = Path.Combine(dir, RateFileKeep.Replace(bucket + "_" + ip, "_") + ".json");
        var now = Clock();
        var windowStart = now - Math.Max(1, windowSeconds);
        try
        {
            using var fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var hits = new List<long>();
            if (fs.Length > 0)
            {
                using var reader = new StreamReader(fs, Encoding.UTF8, leaveOpen: true);
                var raw = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(raw))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(raw);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object
                            && doc.RootElement.TryGetProperty("hits", out var arr)
                            && arr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var t in arr.EnumerateArray())
                            {
                                var n = t.ValueKind == JsonValueKind.Number ? t.GetInt64() : ToInt(t.GetString());
                                if (n >= windowStart)
                                {
                                    hits.Add(n);
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        hits.Clear();
                    }
                }
            }

            hits.Add(now);
            output["count"] = hits.Count;
            output["remaining"] = Math.Max(0, maxRequests - hits.Count);
            if (hits.Count > maxRequests)
            {
                output["blocked"] = true;
                var oldest = hits.Min();
                output["retry_after"] = Math.Max(1, (int)((oldest + windowSeconds) - now));
            }

            fs.SetLength(0);
            fs.Position = 0;
            using var writer = new StreamWriter(fs, new UTF8Encoding(false));
            writer.Write(JsonSerializer.Serialize(new Dictionary<string, object?> { ["hits"] = hits }, JsonOpts));
            writer.Flush();
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["blocked"] = false,
                ["remaining"] = maxRequests,
                ["retry_after"] = 0,
                ["count"] = 0
            };
        }

        return output;
    }

    public static Dictionary<string, object?> EpcStorefrontAntiCrawlEnforce(TalkConfig? cfg = null, IReadOnlyDictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var bucket = opts.TryGetValue("bucket", out var b) && b is not null ? Convert.ToString(b, CultureInfo.InvariantCulture) ?? "price_ajax" : "price_ajax";
        var guestMax = opts.TryGetValue("guest_max", out var gm) ? ToInt(gm, 30) : 30;
        var userMax = opts.TryGetValue("user_max", out var um) ? ToInt(um, 120) : 120;
        var window = opts.TryGetValue("window", out var w) ? ToInt(w, 60) : 60;
        var allowTechKey = !opts.ContainsKey("allow_tech_key") || !IsEmpty(opts["allow_tech_key"]);
        var techKeyOk = allowTechKey && EpcStorefrontAntiCrawlHasTechKey(cfg);
        var sessionUserId = EpcStorefrontAntiCrawlSessionUserId();
        var isBot = EpcStorefrontAntiCrawlIsBot();
        if (techKeyOk)
        {
            return OkGate(sessionUserId, false, true, true);
        }

        if (isBot)
        {
            EpcStorefrontAntiCrawlDeny(403, "Crawler access blocked", bucket);
            return OkGate(sessionUserId, true, false, false);
        }

        var max = sessionUserId > 0 ? userMax : guestMax;
        var rl = EpcStorefrontAntiCrawlRateLimit(bucket, max, window);
        if (!IsEmpty(rl["blocked"]))
        {
            var retry = ToInt(rl["retry_after"]);
            if (!HeadersSent)
            {
                LastHeaders.Add("Retry-After: " + retry);
            }

            EpcStorefrontAntiCrawlDeny(429, "Too many price requests — slow down", bucket, retry);
            return OkGate(sessionUserId, false, false, false);
        }

        return OkGate(sessionUserId, false, PricesVisible(sessionUserId), false);
    }

    public static void EpcStorefrontAntiCrawlDeny(int httpCode, string message, string bucket = "", int retryAfter = 0)
    {
        if (!HeadersSent)
        {
            LastStatus = httpCode;
            LastHeaders.Add("Content-Type: application/json; charset=utf-8");
            LastHeaders.Add("X-Content-Type-Options: nosniff");
            LastHeaders.Add("Cache-Control: no-store");
            LastHeaders.Add("X-Robots-Tag: noindex, nofollow, noarchive");
            if (retryAfter > 0)
            {
                LastHeaders.Add("Retry-After: " + retryAfter);
            }
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = false,
            ["code"] = httpCode == 429 ? "rate_limited" : "forbidden",
            ["message"] = message,
            ["Products"] = new List<object?>(),
            ["products"] = new List<object?>(),
            ["stock"] = new List<object?>(),
            ["references"] = new List<object?>(),
            ["prices_visible"] = false,
            ["anti_crawl"] = true
        };
        if (bucket != "")
        {
            payload["bucket"] = bucket;
        }

        LastBody = JsonSerializer.Serialize(payload, JsonOpts);
        LastExited = true;
    }

    public static void EpcStorefrontAntiCrawlRedactCrossStock(List<object?> stock)
    {
        var mask = SensitiveMask();
        for (var i = 0; i < stock.Count; i++)
        {
            if (stock[i] is not Dictionary<string, object?> row)
            {
                continue;
            }

            foreach (var k in new[] { "price", "price_purchase", "purchase", "qty", "exist", "delivery", "time_to_exe" })
            {
                if (row.ContainsKey(k))
                {
                    row[k] = k is "qty" or "exist" ? null : 0;
                }
            }

            if (row.ContainsKey("warehouse"))
            {
                row["warehouse"] = mask;
            }

            if (row.ContainsKey("storage_id"))
            {
                row["storage_id"] = 0;
            }

            if (row.ContainsKey("price_id"))
            {
                row["price_id"] = 0;
            }

            row["prices_visible"] = false;
            stock[i] = row;
        }
    }

    public static Dictionary<string, object?> EpcStorefrontAntiCrawlResolvePricingIdentity(TalkConfig? cfg = null)
    {
        _ = cfg;
        var sessionUserId = EpcStorefrontAntiCrawlSessionUserId();
        var groupId = 0;
        if (sessionUserId > 0)
        {
            var profile = UserProfile();
            if (profile is not null && profile.TryGetValue("groups", out var groupsObj) && !IsEmpty(groupsObj))
            {
                var groups = AsList(groupsObj);
                if (groups.Count > 0 && !IsEmpty(groups[0]))
                {
                    groupId = ToInt(groups[0]);
                }
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["user_id"] = sessionUserId,
                ["group_id"] = groupId
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["user_id"] = 0, ["group_id"] = 0 };
    }

    public static Dictionary<string, object?> DenyPayload(int httpCode, string message, string bucket = "")
    {
        EpcStorefrontAntiCrawlDeny(httpCode, message, bucket);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(LastBody)!;
    }

    private static Dictionary<string, object?> OkGate(int sessionUserId, bool isBot, bool pricesVisible, bool techKey)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["session_user_id"] = sessionUserId,
            ["is_bot"] = isBot,
            ["prices_visible"] = pricesVisible,
            ["tech_key"] = techKey
        };

    private static Dictionary<string, object?> Def(string key, string channel, string trigger, string recipient, string cpPath)
        => new(StringComparer.Ordinal)
        {
            ["key"] = key,
            ["channel"] = channel,
            ["trigger"] = trigger,
            ["recipient"] = recipient,
            ["cp_path"] = cpPath
        };

    public static bool? DefaultNotifyStatus(object? answer, string match)
    {
        if (answer is not IReadOnlyDictionary<string, object?> dict || !dict.TryGetValue("persons", out var personsObj) || IsEmpty(personsObj))
        {
            return null;
        }

        match = (match ?? "").Trim().ToLowerInvariant();
        foreach (var personObj in AsList(personsObj))
        {
            if (personObj is not IReadOnlyDictionary<string, object?> person)
            {
                continue;
            }

            var email = Nested(person, "contacts", "email");
            if (email is null || IsEmpty(Field(email, "tried_to_send")))
            {
                continue;
            }

            if (match != "")
            {
                var type = person.TryGetValue("type", out var t) ? Convert.ToString(t, CultureInfo.InvariantCulture) ?? "" : "";
                if (type == "direct_contact")
                {
                    var value = email.TryGetValue("value", out var ev) ? (Convert.ToString(ev, CultureInfo.InvariantCulture) ?? "").Trim().ToLowerInvariant() : "";
                    if (value != match)
                    {
                        continue;
                    }
                }
                else if (type == "user_id")
                {
                    var uid = person.TryGetValue("user_id", out var u) ? Convert.ToString(u, CultureInfo.InvariantCulture) ?? "" : "";
                    if (uid != match)
                    {
                        continue;
                    }
                }
            }

            return !IsEmpty(Field(email, "status"));
        }

        return null;
    }

    private static string DocRoot()
        => string.IsNullOrEmpty(DocumentRoot) ? Directory.GetCurrentDirectory() : DocumentRoot;

    private static bool IsEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 or 0L or 0d or 0f => true,
            "" or "0" => true,
            IReadOnlyCollection<object?> c => c.Count == 0,
            JsonElement je => je.ValueKind is JsonValueKind.Null or JsonValueKind.False
                || (je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0"))
                || (je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0)
                || (je.ValueKind == JsonValueKind.Array && je.GetArrayLength() == 0),
            _ => false
        };

    private static object? Field(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static IReadOnlyDictionary<string, object?>? Nested(IReadOnlyDictionary<string, object?> row, string a, string b)
    {
        if (!row.TryGetValue(a, out var first) || first is not IReadOnlyDictionary<string, object?> mid)
        {
            return null;
        }

        return mid.TryGetValue(b, out var second) && second is IReadOnlyDictionary<string, object?> leaf ? leaf : null;
    }

    private static List<object?> AsList(object? value)
        => value switch
        {
            List<object?> list => list,
            IEnumerable<object?> e => e.ToList(),
            _ => new List<object?>()
        };

    private static int ToInt(object? value, int fallback = 0)
    {
        if (value is null or false)
        {
            return fallback;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static string Md5Hex(string value)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedEquals(string known, string provided)
    {
        var a = Encoding.UTF8.GetBytes(known);
        var b = Encoding.UTF8.GetBytes(provided);
        if (a.Length != b.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static bool PhpValidIp(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw != raw.Trim())
        {
            return false;
        }

        return IPAddress.TryParse(raw, out var ip) && ip.ToString() == NormalizeIp(raw, ip);
    }

    private static string NormalizeIp(string raw, IPAddress parsed)
    {
        if (parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return parsed.ToString();
        }

        return raw.Contains('%', StringComparison.Ordinal) ? raw : parsed.ToString();
    }

    private static bool PhpPublicIp(string raw)
    {
        if (!IPAddress.TryParse(raw, out var ip))
        {
            return false;
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            var v = (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
            if (In(v, 0x0A000000, 8) || In(v, 0xAC100000, 12) || In(v, 0xC0A80000, 16))
            {
                return false;
            }

            if (In(v, 0x00000000, 8) || In(v, 0x7F000000, 8) || In(v, 0xA9FE0000, 16)
                || In(v, 0xC0000200, 24) || In(v, 0xC6336400, 24) || In(v, 0xCB007100, 24)
                || In(v, 0xE0000000, 4) || In(v, 0xF0000000, 4))
            {
                return false;
            }

            return true;
        }

        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        if ((bytes[0] & 0xFE) == 0xFC)
        {
            return false;
        }

        var mapped = ip.IsIPv4MappedToIPv6;
        return !mapped;
    }

    private static bool In(uint value, uint network, int prefix)
        => (value ^ network) >> (32 - prefix) == 0;

    private static object? JsonElementToPlain(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToPlain(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToPlain).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
