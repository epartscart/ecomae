using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-ask helpers. PHP identifiers kept for the inventory:
/// <c>epc_copilot_ensure_schema</c>, <c>epc_copilot_intents</c>,
/// <c>epc_copilot_parse_intent</c>, <c>epc_copilot_generate_sql</c>,
/// <c>epc_copilot_execute</c>, <c>epc_copilot_history</c>,
/// <c>epc_copilot_fleet_stats</c>,
/// <c>epc_ai_ensure_schema</c>, <c>epc_ai_strip_pii</c>,
/// <c>epc_ai_service_query</c>, <c>epc_ai_route_query</c>,
/// <c>epc_ai_copilot_respond</c>, <c>epc_ai_classify_parts</c>,
/// <c>epc_ai_detect_anomaly</c>, <c>epc_ai_nl_report</c>,
/// <c>epc_ai_detect_intent</c>, <c>epc_ai_service_stats</c>,
/// <c>epc_ai_service_recent</c>.
/// </summary>
public static class PhpPlanQ1Ask
{
    public const string CopilotPath = "content/general_pages/epc_ai_copilot.php";
    public const string AiServicePath = "content/general_pages/epc_ai_service.php";

    private static readonly (string Type, string Pattern)[] PiiPatterns =
    {
        ("email", @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}"),
        ("phone_ae", @"\+?971[\s\-]?\d{1,2}[\s\-]?\d{7}"),
        ("phone_intl", @"\+\d{1,3}[\s\-]?\d{6,14}"),
        ("trn_ae", @"\d{15}"),
        ("credit_card", @"\b\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}\b"),
        ("iban", @"[A-Z]{2}\d{2}[A-Z0-9]{4}\d{7}([A-Z0-9]?){0,16}"),
        ("passport", @"\b[A-Z]\d{7,8}\b")
    };

    public sealed class CopilotRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public int UserId { get; set; }
        public string QueryText { get; set; } = "";
        public string Intent { get; set; } = "";
        public string? GeneratedSql { get; set; }
        public int ResultCount { get; set; }
        public int ExecutionMs { get; set; }
        public string Status { get; set; } = "success";
        public string? ErrorMessage { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class CopilotStore
    {
        public bool SchemaReady { get; set; }
        public List<CopilotRow> Rows { get; } = new();
        public int NextId { get; set; } = 1;
        public Func<int>? ElapsedMs { get; set; }
        public int Ms() => ElapsedMs?.Invoke() ?? 0;
    }

    public sealed class AiRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "__platform__";
        public int UserId { get; set; }
        public string Service { get; set; } = "copilot";
        public string Intent { get; set; } = "";
        public string InputText { get; set; } = "";
        public string InputHash { get; set; } = "";
        public string? OutputText { get; set; }
        public int TokensUsed { get; set; }
        public int ExecutionMs { get; set; }
        public int PiiStripped { get; set; }
        public string Status { get; set; } = "success";
        public string? ErrorMessage { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class AiStore
    {
        public bool SchemaReady { get; set; }
        public List<AiRow> Rows { get; } = new();
        public int NextId { get; set; } = 1;
        public Func<int>? ElapsedMs { get; set; }
        public int Ms() => ElapsedMs?.Invoke() ?? 0;
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcCopilotIntents()
        => new(StringComparer.Ordinal)
        {
            ["revenue"] = Intent("shop_orders", "SUM(total)", "revenue", "sales", "income", "turnover"),
            ["orders"] = Intent("shop_orders", "COUNT(*)", "orders", "order count", "how many orders"),
            ["customers"] = Intent("shop_customers", "COUNT(*)", "customers", "customer count", "buyers"),
            ["products"] = Intent("shop_products", "COUNT(*)", "products", "items", "skus", "catalog"),
            ["inventory"] = Intent("shop_products", "SUM(stock_qty)", "inventory", "stock", "in stock", "out of stock"),
            ["invoices"] = Intent("epc_gl_entries", "COUNT(*)", "invoices", "invoice", "billing"),
            ["overdue"] = Intent("epc_dunning_queue", "SUM(amount_due)", "overdue", "late", "past due", "unpaid"),
            ["top"] = Intent("shop_orders", "SUM(total)", "top", "best", "highest", "most")
        };

    public static void EpcCopilotEnsureSchema(CopilotStore store) => store.SchemaReady = true;

    public static Dictionary<string, object?> EpcCopilotParseIntent(string query)
    {
        query = (query ?? "").ToLowerInvariant().Trim();
        var intents = EpcCopilotIntents();
        var bestMatch = "";
        var bestScore = 0;
        foreach (var (intent, def) in intents)
        {
            if (def["keywords"] is not IEnumerable<string> kws)
            {
                continue;
            }

            foreach (var kw in kws)
            {
                if (query.Contains(kw, StringComparison.Ordinal) && kw.Length > bestScore)
                {
                    bestScore = kw.Length;
                    bestMatch = intent;
                }
            }
        }

        var period = "month";
        if (query.Contains("today", StringComparison.Ordinal))
        {
            period = "day";
        }
        else if (query.Contains("week", StringComparison.Ordinal))
        {
            period = "week";
        }
        else if (query.Contains("year", StringComparison.Ordinal))
        {
            period = "year";
        }
        else if (query.Contains("quarter", StringComparison.Ordinal))
        {
            period = "quarter";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["intent"] = bestMatch.Length > 0 ? bestMatch : "unknown",
            ["period"] = period,
            ["raw"] = query
        };
    }

    public static Dictionary<string, object?> EpcCopilotGenerateSql(IReadOnlyDictionary<string, object?> parsed, string siteKey)
    {
        var intents = EpcCopilotIntents();
        var intent = Str(parsed, "intent");
        if (!intents.ContainsKey(intent))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "Could not understand query. Try: \"total revenue this month\" or \"how many orders today\""
            };
        }

        var def = intents[intent];
        var dateCol = "created_at";
        var period = Str(parsed, "period");
        var periodFilter = period switch
        {
            "day" => $"AND DATE(`{dateCol}`) = CURDATE()",
            "week" => $"AND `{dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 7 DAY)",
            "month" => $"AND `{dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)",
            "quarter" => $"AND `{dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)",
            "year" => $"AND `{dateCol}` >= DATE_SUB(CURDATE(), INTERVAL 365 DAY)",
            _ => ""
        };
        var siteFilter = "`site_key` = '" + AddSlashes(siteKey ?? "") + "'";
        var metric = Convert.ToString(def["metric"], CultureInfo.InvariantCulture) ?? "";
        var table = Convert.ToString(def["table"], CultureInfo.InvariantCulture) ?? "";
        var sql = $"SELECT {metric} AS `result` FROM `{table}` WHERE {siteFilter} {periodFilter}";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["sql"] = sql,
            ["intent"] = intent,
            ["period"] = period,
            ["metric"] = metric
        };
    }

    public static Dictionary<string, object?> EpcCopilotExecute(CopilotStore store, string siteKey, string queryText, int userId = 0)
    {
        EpcCopilotEnsureSchema(store);
        var parsed = EpcCopilotParseIntent(queryText);
        var sqlResult = EpcCopilotGenerateSql(parsed, siteKey);
        if (!IsTrue(sqlResult["ok"]))
        {
            store.Rows.Add(new CopilotRow
            {
                Id = store.NextId++,
                SiteKey = siteKey,
                UserId = userId,
                QueryText = queryText,
                Intent = Str(parsed, "intent"),
                Status = "refused",
                ErrorMessage = Convert.ToString(sqlResult["error"], CultureInfo.InvariantCulture)
            });
            return sqlResult;
        }

        var ms = store.Ms();
        store.Rows.Add(new CopilotRow
        {
            Id = store.NextId++,
            SiteKey = siteKey,
            UserId = userId,
            QueryText = queryText,
            Intent = Str(parsed, "intent"),
            GeneratedSql = Convert.ToString(sqlResult["sql"], CultureInfo.InvariantCulture),
            ExecutionMs = ms,
            Status = "success"
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["intent"] = parsed["intent"],
            ["period"] = parsed["period"],
            ["sql"] = sqlResult["sql"],
            ["answer"] = "Query generated for: " + parsed["intent"] + " (" + parsed["period"] + ")",
            ["chart"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "metric",
                ["label"] = Ucfirst(Convert.ToString(parsed["intent"], CultureInfo.InvariantCulture) ?? ""),
                ["period"] = parsed["period"]
            },
            ["exec_ms"] = ms
        };
    }

    public static List<Dictionary<string, object?>> EpcCopilotHistory(CopilotStore store, string siteKey, int limit = 50)
    {
        var take = limit < 0 ? 0 : limit;
        return store.Rows
            .Where(r => r.SiteKey == siteKey)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(take)
            .Select(CopilotRowDict)
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcCopilotFleetStats(CopilotStore store)
    {
        EpcCopilotEnsureSchema(store);
        return store.Rows
            .GroupBy(r => r.SiteKey, StringComparer.Ordinal)
            .Select(g =>
            {
                var queries = g.Count();
                var success = g.Count(r => r.Status == "success");
                var avg = queries == 0 ? 0d : g.Average(r => (double)r.ExecutionMs);
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["site_key"] = g.Key,
                    ["queries"] = queries,
                    ["success"] = success.ToString(CultureInfo.InvariantCulture),
                    ["avg_ms"] = avg.ToString("0.0000", CultureInfo.InvariantCulture)
                };
            })
            .OrderByDescending(r => Convert.ToInt32(r["queries"], CultureInfo.InvariantCulture))
            .ToList();
    }

    public static void EpcAiEnsureSchema(AiStore store) => store.SchemaReady = true;

    public static Dictionary<string, object?> EpcAiStripPii(string text)
    {
        var stripped = 0;
        var clean = text ?? "";
        foreach (var (type, pattern) in PiiPatterns)
        {
            var rx = new Regex(pattern);
            var count = rx.Matches(clean).Count;
            if (count > 0)
            {
                clean = rx.Replace(clean, "[REDACTED_" + type.ToUpperInvariant() + "]");
                stripped += count;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["text"] = clean,
            ["pii_count"] = stripped,
            ["had_pii"] = stripped > 0
        };
    }

    public static Dictionary<string, object?> EpcAiServiceQuery(AiStore store, string input, string service = "copilot", string siteKey = "__platform__", int userId = 0)
    {
        EpcAiEnsureSchema(store);
        var pii = EpcAiStripPii(input);
        var cleanInput = Convert.ToString(pii["text"], CultureInfo.InvariantCulture) ?? "";
        var piiCount = Convert.ToInt32(pii["pii_count"], CultureInfo.InvariantCulture);
        if (piiCount > 5)
        {
            store.Rows.Add(new AiRow
            {
                Id = store.NextId++,
                SiteKey = siteKey,
                UserId = userId,
                Service = service,
                InputText = "[BLOCKED]",
                InputHash = Sha256(input),
                PiiStripped = piiCount,
                Status = "pii_blocked",
                ErrorMessage = "Too much PII detected"
            });
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "Query contains too much personal data. Please rephrase without names, emails, or account numbers."
            };
        }

        var result = EpcAiRouteQuery(store, cleanInput, service, siteKey);
        var ms = store.Ms();
        store.Rows.Add(new AiRow
        {
            Id = store.NextId++,
            SiteKey = siteKey,
            UserId = userId,
            Service = service,
            Intent = Str(result, "intent"),
            InputText = cleanInput,
            InputHash = Sha256(input),
            OutputText = result.TryGetValue("answer", out var ans) ? Convert.ToString(ans, CultureInfo.InvariantCulture) : "",
            TokensUsed = result.TryGetValue("tokens", out var tok) ? Convert.ToInt32(tok, CultureInfo.InvariantCulture) : 0,
            ExecutionMs = ms,
            PiiStripped = piiCount,
            Status = IsTrue(result.TryGetValue("ok", out var ok) ? ok : false) ? "success" : "error"
        });
        result["exec_ms"] = ms;
        result["pii_stripped"] = piiCount;
        return result;
    }

    public static Dictionary<string, object?> EpcAiRouteQuery(AiStore store, string input, string service, string siteKey)
        => service switch
        {
            "copilot" => EpcAiCopilotRespond(input, siteKey),
            "classify" => EpcAiClassifyParts(input),
            "anomaly" => EpcAiDetectAnomaly(input),
            "nl_report" => EpcAiNlReport(input, siteKey),
            _ => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "Unknown AI service: " + service
            }
        };

    public static Dictionary<string, object?> EpcAiCopilotRespond(string input, string siteKey)
    {
        var intent = EpcAiDetectIntent(input);
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["revenue"] = "To check revenue, go to ERP → Dashboard. Revenue is calculated from completed orders ex. VAT.",
            ["orders"] = "View orders in ERP → Sales Orders tab. Filter by date range for specific periods.",
            ["inventory"] = "Check stock levels in ERP → Inventory tab. Low stock alerts appear on the dashboard.",
            ["invoices"] = "Manage invoices in ERP → Invoices tab. E-invoicing (PINT-AE) is available for UAE.",
            ["payroll"] = "Payroll management is in ERP → Payroll tab. WPS SIF export is available for UAE.",
            ["vat"] = "UAE VAT returns are in ERP → UAE VAT tab. VAT period reports auto-calculate.",
            ["help"] = "Available modules: Dashboard, Sales, Invoices, GL, Inventory, HR, Payroll, VAT, Reports."
        };
        var answer = responses.TryGetValue(intent, out var text)
            ? text
            : "I can help with: revenue, orders, inventory, invoices, payroll, VAT. What would you like to know?";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["intent"] = intent,
            ["answer"] = answer,
            ["tokens"] = 0
        };
    }

    public static Dictionary<string, object?> EpcAiClassifyParts(string input)
    {
        var categories = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["filter"] = new[] { "oil filter", "air filter", "fuel filter", "cabin filter" },
            ["brake"] = new[] { "brake pad", "brake disc", "brake rotor", "brake shoe" },
            ["engine"] = new[] { "spark plug", "timing belt", "piston", "gasket", "valve" },
            ["suspension"] = new[] { "shock absorber", "strut", "spring", "bush", "ball joint" },
            ["electrical"] = new[] { "battery", "alternator", "starter", "ignition", "sensor" }
        };
        input = (input ?? "").ToLowerInvariant();
        foreach (var (cat, keywords) in categories)
        {
            foreach (var kw in keywords)
            {
                if (input.Contains(kw, StringComparison.Ordinal))
                {
                    return new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["ok"] = true,
                        ["intent"] = "classify",
                        ["category"] = cat,
                        ["answer"] = "Classified as: " + Ucfirst(cat),
                        ["confidence"] = 0.85
                    };
                }
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["intent"] = "classify",
            ["category"] = "unknown",
            ["answer"] = "Could not classify. Please provide more detail.",
            ["confidence"] = 0
        };
    }

    public static Dictionary<string, object?> EpcAiDetectAnomaly(string input)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["intent"] = "anomaly",
            ["answer"] = "Anomaly detection processed.",
            ["anomalies"] = new List<object?>(),
            ["risk_score"] = 0
        };

    public static Dictionary<string, object?> EpcAiNlReport(string input, string siteKey)
    {
        var intent = EpcAiDetectIntent(input);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["intent"] = "nl_report",
            ["report_type"] = intent,
            ["answer"] = "Report query parsed. Generating " + intent + " report.",
            ["tokens"] = 0
        };
    }

    public static string EpcAiDetectIntent(string input)
    {
        input = (input ?? "").ToLowerInvariant();
        var map = new (string Intent, string[] Keywords)[]
        {
            ("revenue", new[] { "revenue", "sales", "income" }),
            ("orders", new[] { "order", "orders" }),
            ("inventory", new[] { "stock", "inventory" }),
            ("invoices", new[] { "invoice", "billing" }),
            ("payroll", new[] { "payroll", "salary", "wps" }),
            ("vat", new[] { "vat", "tax" }),
            ("help", new[] { "help", "what", "how" })
        };
        foreach (var (intent, keywords) in map)
        {
            foreach (var kw in keywords)
            {
                if (input.Contains(kw, StringComparison.Ordinal))
                {
                    return intent;
                }
            }
        }

        return "help";
    }

    public static List<Dictionary<string, object?>> EpcAiServiceStats(AiStore store)
    {
        EpcAiEnsureSchema(store);
        return store.Rows
            .GroupBy(r => r.Service, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var total = g.Count();
                var success = g.Count(r => r.Status == "success");
                var pii = g.Sum(r => r.PiiStripped);
                var avg = total == 0 ? 0d : g.Average(r => (double)r.ExecutionMs);
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["service"] = g.Key,
                    ["total"] = total,
                    ["success"] = success.ToString(CultureInfo.InvariantCulture),
                    ["pii_events"] = pii.ToString(CultureInfo.InvariantCulture),
                    ["avg_ms"] = avg.ToString("0.0000", CultureInfo.InvariantCulture)
                };
            })
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcAiServiceRecent(AiStore store, string siteKey = "", int limit = 50)
    {
        EpcAiEnsureSchema(store);
        var take = limit < 0 ? 0 : limit;
        var q = store.Rows.AsEnumerable();
        if (siteKey.Length > 0)
        {
            q = q.Where(r => r.SiteKey == siteKey);
        }

        return q.OrderByDescending(r => r.Id).Take(take).Select(r =>
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = r.Id,
                ["service"] = r.Service,
                ["intent"] = r.Intent,
                ["input_text"] = r.InputText,
                ["status"] = r.Status,
                ["execution_ms"] = r.ExecutionMs,
                ["created_at"] = r.CreatedAt
            };
            if (siteKey.Length == 0)
            {
                row["site_key"] = r.SiteKey;
            }

            return row;
        }).ToList();
    }

    public static object?[] CopilotLogRows(CopilotStore store)
        => store.Rows.OrderBy(r => r.Id).Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = r.SiteKey,
            ["user_id"] = r.UserId,
            ["query_text"] = r.QueryText,
            ["intent"] = r.Intent,
            ["status"] = r.Status,
            ["error_message"] = r.ErrorMessage,
            ["generated_sql"] = r.GeneratedSql
        }).ToArray();

    public static object?[] AiLogRows(AiStore store)
        => store.Rows.OrderBy(r => r.Id).Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = r.SiteKey,
            ["user_id"] = r.UserId,
            ["service"] = r.Service,
            ["intent"] = r.Intent,
            ["input_text"] = r.InputText,
            ["status"] = r.Status,
            ["error_message"] = r.ErrorMessage,
            ["pii_stripped"] = r.PiiStripped
        }).ToArray();

    private static Dictionary<string, object?> Intent(string table, string metric, params string[] keywords)
        => new(StringComparer.Ordinal)
        {
            ["keywords"] = keywords,
            ["table"] = table,
            ["metric"] = metric
        };

    private static Dictionary<string, object?> CopilotRowDict(CopilotRow r)
        => new(StringComparer.Ordinal)
        {
            ["id"] = r.Id,
            ["site_key"] = r.SiteKey,
            ["user_id"] = r.UserId,
            ["query_text"] = r.QueryText,
            ["intent"] = r.Intent,
            ["generated_sql"] = r.GeneratedSql,
            ["result_count"] = r.ResultCount,
            ["execution_ms"] = r.ExecutionMs,
            ["status"] = r.Status,
            ["error_message"] = r.ErrorMessage,
            ["created_at"] = r.CreatedAt
        };

    private static string AddSlashes(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in value)
        {
            if (ch is '\'' or '"' or '\\' or '\0')
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? ""))).ToLowerInvariant();

    private static string Ucfirst(string value)
        => value.Length == 0 ? "" : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Str(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            : "";

    private static bool IsTrue(object? value)
        => value is true or 1 or "1" || (value is string s && s.Equals("true", StringComparison.OrdinalIgnoreCase));
}
