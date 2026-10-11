using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-lead helpers. PHP identifiers kept for the inventory:
/// <c>epc_ai_class_ensure_schema</c>, <c>epc_ai_category_rules</c>,
/// <c>epc_ai_classify</c>, <c>epc_ai_classify_and_store</c>,
/// <c>epc_ai_classify_batch</c>, <c>epc_ai_hs_lookup</c>,
/// <c>epc_ai_seed_hs_codes</c>, <c>epc_ai_class_stats</c>,
/// <c>epc_ai_class_fleet_stats</c>, <c>epc_ai_review</c>,
/// <c>epc_ecomae_marketing_meta</c>, <c>epc_ecomae_marketing_faq_jsonld</c>,
/// <c>epc_ecomae_marketing_article_jsonld</c>, <c>epc_ecomae_marketing_faq_html</c>,
/// <c>epc_ecomae_marketing_crumb</c>, <c>epc_ecomae_platform_page_docs</c>,
/// <c>epc_ecomae_platform_page_compare</c>, <c>epc_ecomae_platform_page_bos</c>,
/// <c>epc_ecomae_platform_page_solution</c>,
/// <c>epc_cp_top_alerts_use_professional_header</c>,
/// <c>epc_cp_https_redirect_is_configured</c>,
/// <c>epc_cp_top_alerts_suppress_email_sms</c>,
/// <c>epc_cp_email_notification_state</c>, <c>epc_cp_sms_notification_state</c>,
/// <c>epc_cp_top_alerts_show_in_header</c>,
/// <c>epc_cp_top_alerts_render_ssl_item</c>,
/// <c>epc_cp_top_alerts_email_styles</c>, <c>epc_cp_top_alerts_sms_styles</c>,
/// <c>epc_cp_top_alerts_render_email_item</c>,
/// <c>epc_cp_top_alerts_render_sms_item</c>.
/// </summary>
public static class PhpPlanQ1Lead
{
    public const string ClassificationPath = "content/general_pages/epc_ai_classification.php";
    public const string MarketingPagesPath = "content/general_pages/epc_ecomae_marketing_pages.php";
    public const string TopAlertsPath = "content/general_pages/epc_cp_top_alerts.php";

    public static string BaseUrl { get; set; } = "https://www.ecomae.com/";
    public static Func<int, string> Translate { get; set; } = id => "T" + id.ToString(CultureInfo.InvariantCulture);
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<bool> IsSuperCp { get; set; } = () => false;
    public static Func<bool> IsPlatformOperator { get; set; } = () => false;
    public static IReadOnlyDictionary<string, object?>? DpConfig { get; set; }
    public static string DocumentRoot { get; set; } = "";
    public static Dictionary<string, string> Server { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Reset()
    {
        BaseUrl = "https://www.ecomae.com/";
        Translate = id => "T" + id.ToString(CultureInfo.InvariantCulture);
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IsSuperCp = () => false;
        IsPlatformOperator = () => false;
        DpConfig = null;
        DocumentRoot = "";
        Server.Clear();
    }

    public sealed class ClassStore
    {
        public int NextHsId { get; set; } = 1;
        public int NextClassId { get; set; } = 1;
        public List<HsRow> Hs { get; } = new();
        public List<ClassRow> Rows { get; } = new();
        public bool Schema { get; set; }
    }

    public sealed class HsRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public string Chapter { get; set; } = "";
        public string Section { get; set; } = "";
        public string DutyRate { get; set; } = "0.00";
        public string Keywords { get; set; } = "";
    }

    public sealed class ClassRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public int ProductId { get; set; }
        public string Sku { get; set; } = "";
        public string InputText { get; set; } = "";
        public string HsCode { get; set; } = "";
        public string Category { get; set; } = "";
        public string Subcategory { get; set; } = "";
        public string TagsJson { get; set; } = "[]";
        public double Confidence { get; set; }
        public string Method { get; set; } = "rule";
        public int Reviewed { get; set; }
        public int ReviewedBy { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class AlertStore
    {
        public List<Dictionary<string, object?>> Debug { get; } = new();
        public int SmsActive { get; set; }
    }

    public static List<Dictionary<string, object?>> EpcAiCategoryRules()
        => Rules.Select(r => new Dictionary<string, object?>(r, StringComparer.Ordinal)).ToList();

    public static void EpcAiClassEnsureSchema(ClassStore db) => db.Schema = true;

    public static Dictionary<string, object?> EpcAiClassify(string text)
    {
        text = text.Trim().ToLowerInvariant();
        if (text == "")
        {
            return Uncategorized();
        }

        Dictionary<string, object?>? best = null;
        var bestScore = 0;
        var matchedKeywords = new List<string>();
        foreach (var rule in Rules)
        {
            var score = 0;
            var matched = new List<string>();
            foreach (var kw in (string[])rule["keywords"]!)
            {
                if (text.Contains(kw, StringComparison.Ordinal))
                {
                    score += kw.Length;
                    matched.Add(kw);
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = rule;
                matchedKeywords = matched;
            }
        }

        if (best == null || bestScore < 3)
        {
            return Uncategorized();
        }

        var maxPossible = ((string[])best["keywords"]!).Sum(k => k.Length);
        var confidence = Math.Min(99, Math.Round(bestScore / (double)Math.Max(1, maxPossible) * 100, 2, MidpointRounding.AwayFromZero));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["category"] = best["category"],
            ["subcategory"] = best["subcategory"],
            ["confidence"] = confidence,
            ["tags"] = matchedKeywords,
            ["hs_code"] = best["hs_prefix"],
            ["method"] = "keyword"
        };
    }

    public static Dictionary<string, object?> EpcAiClassifyAndStore(ClassStore db, string siteKey, IReadOnlyDictionary<string, object?> product)
    {
        EpcAiClassEnsureSchema(db);
        var parts = new[] { Str(product, "name"), Str(product, "description"), Str(product, "brand"), Str(product, "sku") }
            .Where(s => s != "")
            .ToArray();
        var text = string.Join(" ", parts);
        var result = new Dictionary<string, object?>(EpcAiClassify(text), StringComparer.Ordinal);
        var row = new ClassRow
        {
            Id = db.NextClassId++,
            SiteKey = siteKey,
            ProductId = Int(product, "id", Int(product, "product_id", 0)),
            Sku = Str(product, "sku"),
            InputText = ByteSubstr(text, 1000),
            HsCode = (string?)result["hs_code"] ?? "",
            Category = (string?)result["category"] ?? "",
            Subcategory = (string?)result["subcategory"] ?? "",
            TagsJson = JsonSerializer.Serialize(result["tags"], JsonOpts),
            Confidence = Convert.ToDouble(result["confidence"], CultureInfo.InvariantCulture),
            Method = (string?)result["method"] ?? "rule",
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        db.Rows.Add(row);
        result["classification_id"] = row.Id;
        result["ok"] = true;
        return result;
    }

    public static Dictionary<string, object?> EpcAiClassifyBatch(ClassStore db, string siteKey, IEnumerable<IReadOnlyDictionary<string, object?>> products)
    {
        var results = new List<Dictionary<string, object?>>();
        var classified = 0;
        var uncategorized = 0;
        var list = products.ToList();
        foreach (var product in list)
        {
            var r = EpcAiClassifyAndStore(db, siteKey, product);
            results.Add(r);
            if ((string?)r["category"] != "Uncategorized")
            {
                classified++;
            }
            else
            {
                uncategorized++;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["total"] = list.Count,
            ["classified"] = classified,
            ["uncategorized"] = uncategorized,
            ["results"] = results
        };
    }

    public static List<Dictionary<string, object?>> EpcAiHsLookup(ClassStore db, string query)
    {
        EpcAiClassEnsureSchema(db);
        IEnumerable<HsRow> rows;
        if (Regex.IsMatch(query, @"^\d{4,}$"))
        {
            rows = db.Hs.Where(h => h.Code.StartsWith(query, StringComparison.Ordinal)).Take(20);
        }
        else
        {
            rows = db.Hs.Where(h =>
                h.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                h.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(20);
        }

        return rows.Select(h => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = h.Id,
            ["code"] = h.Code,
            ["description"] = h.Description,
            ["chapter"] = h.Chapter,
            ["section"] = h.Section,
            ["duty_rate"] = h.DutyRate,
            ["keywords"] = h.Keywords
        }).ToList();
    }

    public static int EpcAiSeedHsCodes(ClassStore db)
    {
        EpcAiClassEnsureSchema(db);
        var inserted = 0;
        foreach (var c in HsSeed)
        {
            if (db.Hs.Any(h => h.Code == c[0]))
            {
                continue;
            }

            db.Hs.Add(new HsRow
            {
                Id = db.NextHsId++,
                Code = c[0],
                Description = c[1],
                Chapter = c[2],
                Section = c[3],
                DutyRate = "5.00",
                Keywords = c[5]
            });
            inserted++;
        }

        return inserted;
    }

    public static List<Dictionary<string, object?>> EpcAiClassStats(ClassStore db, string siteKey)
    {
        EpcAiClassEnsureSchema(db);
        return db.Rows.Where(r => r.SiteKey == siteKey)
            .GroupBy(r => r.Category, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category"] = g.Key,
                ["count"] = g.Count(),
                ["avg_confidence"] = g.Average(x => x.Confidence).ToString("0.000000", CultureInfo.InvariantCulture),
                ["reviewed"] = g.Sum(x => x.Reviewed).ToString(CultureInfo.InvariantCulture)
            })
            .OrderByDescending(r => Convert.ToInt32(r["count"], CultureInfo.InvariantCulture))
            .ThenBy(r => (string)r["category"]!, StringComparer.Ordinal)
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcAiClassFleetStats(ClassStore db)
    {
        EpcAiClassEnsureSchema(db);
        return db.Rows.GroupBy(r => r.SiteKey, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = g.Key,
                ["total_classified"] = g.Count(),
                ["uncategorized"] = g.Count(x => x.Category == "Uncategorized").ToString(CultureInfo.InvariantCulture),
                ["avg_confidence"] = g.Average(x => x.Confidence).ToString("0.000000", CultureInfo.InvariantCulture),
                ["reviewed"] = g.Sum(x => x.Reviewed).ToString(CultureInfo.InvariantCulture),
                ["last_run"] = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(r => Convert.ToInt32(r["total_classified"], CultureInfo.InvariantCulture))
            .ToList();
    }

    public static bool EpcAiReview(ClassStore db, int classificationId, string category, string subcategory, string hsCode, int reviewerId)
    {
        var row = db.Rows.FirstOrDefault(r => r.Id == classificationId);
        if (row == null)
        {
            return false;
        }

        row.Category = category;
        row.Subcategory = subcategory;
        row.HsCode = hsCode;
        row.Reviewed = 1;
        row.ReviewedBy = reviewerId;
        row.Method = "manual";
        return true;
    }

    public static object?[]? EpcEcomaeMarketingMeta(string page, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var slug = Slug(parameters);
        switch (page)
        {
            case "docs":
                var docs = PhpPlanQ1Done.EpcEcomaeDocsCatalog();
                if (slug != "" && docs.TryGetProperty(slug, out var d))
                {
                    return new object?[] { d.GetProperty("title").GetString() + " — ECOM AE documentation", d.GetProperty("summary").GetString() };
                }

                return new object?[] { "Documentation — ECOM AE", "Public documentation for the ECOM AE Blockchain BOS Enterprise System: overview, ERP modules, API, security, industry packs and user guides." };
            case "compare":
                var cmp = PhpPlanQ1Done.EpcEcomaeCompareCatalog();
                if (slug != "" && cmp.TryGetProperty(slug, out var c))
                {
                    return new object?[] { c.GetProperty("tagline").GetString() + " — comparison | ECOM AE", c.GetProperty("intro").GetString() };
                }

                return new object?[] { "ECOM AE compared vs Odoo, ERPNext, Zoho, NetSuite, Dynamics 365", "How ECOM AE — a Blockchain BOS Enterprise System — compares with leading ERP and business platforms." };
            case "bos":
                var bos = PhpPlanQ1Done.EpcEcomaeBosArticlesCatalog();
                if (slug != "" && bos.TryGetProperty(slug, out var b))
                {
                    return new object?[] { b.GetProperty("title").GetString() + " | ECOM AE", b.GetProperty("summary").GetString() };
                }

                return new object?[] { "What is a Blockchain BOS Enterprise System? | ECOM AE", "Understand the Blockchain BOS Enterprise category: Blockchain BOS vs ERP, vs CRM, and why modern businesses run on one unified verifiable system." };
            case "solution":
                var sol = PhpPlanQ1Done.EpcEcomaeSolutionsCatalog();
                if (slug != "" && sol.TryGetProperty(slug, out var s))
                {
                    return new object?[] { s.GetProperty("h1").GetString() + " | ECOM AE", s.GetProperty("lead").GetString() };
                }

                return new object?[] { "ECOM AE solutions", "Purpose-built solution pages for the platform, region and modules ECOM AE serves." };
            default:
                return null;
        }
    }

    public static string EpcEcomaeMarketingFaqJsonld(IReadOnlyList<object?[]> faqs)
    {
        if (faqs.Count == 0)
        {
            return "";
        }

        var items = faqs.Select(f => new Dictionary<string, object?>
        {
            ["@type"] = "Question",
            ["name"] = Convert.ToString(f[0], CultureInfo.InvariantCulture) ?? "",
            ["acceptedAnswer"] = new Dictionary<string, object?>
            {
                ["@type"] = "Answer",
                ["text"] = Convert.ToString(f.Length > 1 ? f[1] : "", CultureInfo.InvariantCulture) ?? ""
            }
        }).ToList();
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = items
        };
        return "<script type=\"application/ld+json\">" + JsonSerializer.Serialize(data, JsonOpts) + "</script>";
    }

    public static string EpcEcomaeMarketingArticleJsonld(string headline, string desc, string url)
    {
        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Article",
            ["headline"] = headline,
            ["description"] = desc,
            ["mainEntityOfPage"] = url,
            ["author"] = new Dictionary<string, object?> { ["@type"] = "Organization", ["name"] = "ECOM AE" },
            ["publisher"] = new Dictionary<string, object?> { ["@type"] = "Organization", ["name"] = "ECOM AE" }
        };
        return "<script type=\"application/ld+json\">" + JsonSerializer.Serialize(data, JsonOpts) + "</script>";
    }

    public static string EpcEcomaeMarketingFaqHtml(IReadOnlyList<object?[]> faqs)
    {
        if (faqs.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append("<section class=\"epm-card\" style=\"margin-top:24px\"><h2 class=\"epm-section-title\">Frequently asked questions</h2>");
        foreach (var f in faqs)
        {
            sb.Append("<div style=\"margin-top:14px\"><h3 style=\"margin:0 0 4px;font-size:17px\">").Append(H(f[0])).Append("</h3>")
                .Append("<p style=\"margin:0;color:#475569\">").Append(H(f.Length > 1 ? f[1] : "")).Append("</p></div>");
        }

        sb.Append("</section>");
        return sb.ToString();
    }

    public static string EpcEcomaeMarketingCrumb(string label, string hubUrl, string hubLabel)
        => "<nav class=\"epm-badge\" style=\"margin-bottom:14px\"><a href=\"" + H(hubUrl) + "\" style=\"color:inherit\"><i class=\"fa fa-angle-left\"></i> " + H(hubLabel) + "</a> · " + H(label) + "</nav>";

    public static string EpcEcomaePlatformPageDocs(IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        var cat = PhpPlanQ1Done.EpcEcomaeDocsCatalog();
        var slug = Slug(parameters);
        if (slug != "" && cat.TryGetProperty(slug, out var d))
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"epm-wrap\"><div class=\"epm-section\">");
            sb.Append(EpcEcomaeMarketingCrumb(d.GetProperty("title").GetString() ?? "", baseUrl + "/documentation", "Documentation"));
            sb.Append("<h1 class=\"epm-section-title\" style=\"font-size:32px\"><i class=\"fa ").Append(H(d.GetProperty("icon").GetString())).Append("\"></i> ").Append(H(d.GetProperty("title").GetString())).Append("</h1>");
            sb.Append("<p class=\"epm-section-lead\" style=\"max-width:820px\">").Append(H(d.GetProperty("summary").GetString())).Append("</p>");
            foreach (var p in d.GetProperty("body").EnumerateArray())
            {
                sb.Append("<p style=\"max-width:820px;color:#334155;line-height:1.7\">").Append(H(p.GetString())).Append("</p>");
            }

            if (d.TryGetProperty("bullets", out var bullets) && bullets.ValueKind == JsonValueKind.Array && bullets.GetArrayLength() > 0)
            {
                sb.Append("<ul class=\"epm-eco-model__list\" style=\"max-width:820px\">");
                foreach (var b in bullets.EnumerateArray())
                {
                    sb.Append("<li>").Append(H(b.GetString())).Append("</li>");
                }

                sb.Append("</ul>");
            }

            sb.Append("</div></div>");
            sb.Append(EpcEcomaeMarketingArticleJsonld((d.GetProperty("title").GetString() ?? "") + " — ECOM AE documentation", d.GetProperty("summary").GetString() ?? "", baseUrl + "/documentation/" + slug));
            return sb.ToString();
        }

        var hub = new StringBuilder();
        hub.Append("<div class=\"epm-wrap\">");
        hub.Append("<div class=\"epm-hero\" style=\"min-height:auto;padding:36px 0\"><div class=\"epm-hero__content\"><div class=\"epm-badge\"><i class=\"fa fa-book\"></i> Documentation</div>");
        hub.Append("<h1 class=\"epm-section-title\" style=\"font-size:36px;margin-top:10px\">ECOM AE documentation</h1>");
        hub.Append("<p class=\"epm-section-lead\" style=\"max-width:780px\">Public documentation for the ECOM AE Blockchain BOS Enterprise System — platform overview, ERP modules, API, security, industry packs and user guides.</p></div></div>");
        hub.Append("<div class=\"epm-eco-model__grid\" style=\"margin-top:8px\">");
        foreach (var item in cat.EnumerateObject())
        {
            hub.Append("<a class=\"epm-card epm-card--accent\" style=\"text-decoration:none;color:inherit;display:block\" href=\"").Append(H(baseUrl + "/documentation/" + item.Name)).Append("\">");
            hub.Append("<h3><i class=\"fa ").Append(H(item.Value.GetProperty("icon").GetString())).Append("\"></i> ").Append(H(item.Value.GetProperty("title").GetString())).Append("</h3>");
            hub.Append("<p style=\"color:#475569;margin:6px 0 0\">").Append(H(item.Value.GetProperty("summary").GetString())).Append("</p></a>");
        }

        hub.Append("</div></div>");
        return hub.ToString();
    }

    public static string EpcEcomaePlatformPageCompare(IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        var cat = PhpPlanQ1Done.EpcEcomaeCompareCatalog();
        var slug = Slug(parameters);
        if (slug != "" && cat.TryGetProperty(slug, out var c))
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"epm-wrap\"><div class=\"epm-section\">");
            sb.Append(EpcEcomaeMarketingCrumb(c.GetProperty("tagline").GetString() ?? "", baseUrl + "/compare", "Comparisons"));
            sb.Append("<h1 class=\"epm-section-title\" style=\"font-size:32px\">").Append(H(c.GetProperty("tagline").GetString())).Append("</h1>");
            sb.Append("<p class=\"epm-section-lead\" style=\"max-width:840px\">").Append(H(c.GetProperty("intro").GetString())).Append("</p>");
            sb.Append("<div style=\"overflow-x:auto\"><table class=\"epm-compare-table\" style=\"width:100%;border-collapse:collapse;margin-top:18px\">");
            sb.Append("<thead><tr><th style=\"text-align:left;padding:10px;border-bottom:2px solid #e2e8f0\"></th><th style=\"text-align:left;padding:10px;border-bottom:2px solid #e2e8f0\">ECOM AE</th><th style=\"text-align:left;padding:10px;border-bottom:2px solid #e2e8f0\">").Append(H(c.GetProperty("competitor").GetString())).Append("</th></tr></thead><tbody>");
            foreach (var row in c.GetProperty("rows").EnumerateArray())
            {
                var cells = row.EnumerateArray().ToList();
                sb.Append("<tr><td style=\"padding:10px;border-bottom:1px solid #eef2f7;font-weight:600\">").Append(H(cells[0].GetString())).Append("</td>")
                    .Append("<td style=\"padding:10px;border-bottom:1px solid #eef2f7;color:#0f766e\">").Append(H(cells[1].GetString())).Append("</td>")
                    .Append("<td style=\"padding:10px;border-bottom:1px solid #eef2f7;color:#475569\">").Append(H(cells[2].GetString())).Append("</td></tr>");
            }

            sb.Append("</tbody></table></div>");
            sb.Append("<div class=\"epm-highlight\" style=\"margin-top:18px\"><p><strong>When to choose which:</strong> ").Append(H(c.GetProperty("whenThem").GetString())).Append("</p></div>");
            sb.Append(FaqFromJson(c));
            sb.Append("</div></div>");
            sb.Append(FaqJsonFromJson(c));
            sb.Append(EpcEcomaeMarketingArticleJsonld(c.GetProperty("tagline").GetString() ?? "", c.GetProperty("intro").GetString() ?? "", baseUrl + "/compare/" + slug));
            return sb.ToString();
        }

        var hub = new StringBuilder();
        hub.Append("<div class=\"epm-wrap\"><div class=\"epm-section\">");
        hub.Append("<div class=\"epm-badge\"><i class=\"fa fa-balance-scale\"></i> Comparisons</div>");
        hub.Append("<h1 class=\"epm-section-title\" style=\"font-size:34px;margin-top:10px\">ECOM AE compared</h1>");
        hub.Append("<p class=\"epm-section-lead\" style=\"max-width:760px\">How ECOM AE — a Blockchain BOS Enterprise System — compares with leading ERP and business platforms.</p>");
        hub.Append("<div class=\"epm-eco-model__grid\" style=\"margin-top:8px\">");
        foreach (var item in cat.EnumerateObject())
        {
            hub.Append("<a class=\"epm-card epm-card--accent\" style=\"text-decoration:none;color:inherit;display:block\" href=\"").Append(H(baseUrl + "/compare/" + item.Name)).Append("\">");
            hub.Append("<h3>").Append(H(item.Value.GetProperty("tagline").GetString())).Append("</h3>");
            hub.Append("<p style=\"color:#475569;margin:6px 0 0\">").Append(H(item.Value.GetProperty("intro").GetString())).Append("</p></a>");
        }

        hub.Append("</div></div></div>");
        return hub.ToString();
    }

    public static string EpcEcomaePlatformPageBos(IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        var cat = PhpPlanQ1Done.EpcEcomaeBosArticlesCatalog();
        var slug = Slug(parameters);
        if (slug != "" && cat.TryGetProperty(slug, out var a))
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"epm-wrap\"><div class=\"epm-section\">");
            sb.Append(EpcEcomaeMarketingCrumb(a.GetProperty("title").GetString() ?? "", baseUrl + "/bos", "Blockchain BOS knowledge"));
            sb.Append("<h1 class=\"epm-section-title\" style=\"font-size:32px\">").Append(H(a.GetProperty("title").GetString())).Append("</h1>");
            sb.Append("<p class=\"epm-section-lead\" style=\"max-width:840px\">").Append(H(a.GetProperty("summary").GetString())).Append("</p>");
            foreach (var p in a.GetProperty("body").EnumerateArray())
            {
                sb.Append("<p style=\"max-width:840px;color:#334155;line-height:1.7\">").Append(H(p.GetString())).Append("</p>");
            }

            sb.Append(FaqFromJson(a));
            sb.Append("</div></div>");
            sb.Append(FaqJsonFromJson(a));
            sb.Append(EpcEcomaeMarketingArticleJsonld(a.GetProperty("title").GetString() ?? "", a.GetProperty("summary").GetString() ?? "", baseUrl + "/bos/" + slug));
            return sb.ToString();
        }

        var hub = new StringBuilder();
        hub.Append("<div class=\"epm-wrap\"><div class=\"epm-section\">");
        hub.Append("<div class=\"epm-badge\"><i class=\"fa fa-lightbulb-o\"></i> Blockchain BOS knowledge</div>");
        hub.Append("<h1 class=\"epm-section-title\" style=\"font-size:34px;margin-top:10px\">The Blockchain BOS Enterprise category</h1>");
        hub.Append("<p class=\"epm-section-lead\" style=\"max-width:780px\">What a Blockchain BOS Enterprise System is, how it differs from ERP and CRM, and why modern businesses run on one unified verifiable system.</p>");
        hub.Append("<div class=\"epm-eco-model__grid\" style=\"margin-top:8px\">");
        foreach (var item in cat.EnumerateObject())
        {
            hub.Append("<a class=\"epm-card epm-card--accent\" style=\"text-decoration:none;color:inherit;display:block\" href=\"").Append(H(baseUrl + "/bos/" + item.Name)).Append("\">");
            hub.Append("<h3>").Append(H(item.Value.GetProperty("title").GetString())).Append("</h3>");
            hub.Append("<p style=\"color:#475569;margin:6px 0 0\">").Append(H(item.Value.GetProperty("summary").GetString())).Append("</p></a>");
        }

        hub.Append("</div></div></div>");
        return hub.ToString();
    }

    public static string EpcEcomaePlatformPageSolution(IReadOnlyDictionary<string, object?>? parameters = null)
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        var cat = PhpPlanQ1Done.EpcEcomaeSolutionsCatalog();
        var slug = Slug(parameters);
        if (slug == "" || !cat.TryGetProperty(slug, out var s))
        {
            var hub = new StringBuilder();
            hub.Append("<div class=\"epm-wrap\"><div class=\"epm-section\">");
            hub.Append("<div class=\"epm-badge\"><i class=\"fa fa-rocket\"></i> Solutions</div>");
            hub.Append("<h1 class=\"epm-section-title\" style=\"font-size:34px;margin-top:10px\">ECOM AE solutions</h1>");
            hub.Append("<p class=\"epm-section-lead\" style=\"max-width:760px\">Purpose-built solution pages for the platform, region and modules ECOM AE serves.</p>");
            hub.Append("<div class=\"epm-eco-model__grid\" style=\"margin-top:8px\">");
            foreach (var item in cat.EnumerateObject())
            {
                hub.Append("<a class=\"epm-card epm-card--accent\" style=\"text-decoration:none;color:inherit;display:block\" href=\"").Append(H(baseUrl + "/solutions/" + item.Name)).Append("\">");
                hub.Append("<h3>").Append(H(item.Value.GetProperty("h1").GetString())).Append("</h3>");
                hub.Append("<p style=\"color:#475569;margin:6px 0 0\">").Append(H(item.Value.GetProperty("lead").GetString())).Append("</p></a>");
            }

            hub.Append("</div></div></div>");
            return hub.ToString();
        }

        var sb = new StringBuilder();
        sb.Append("<div class=\"epm-wrap\">");
        sb.Append("<div class=\"epm-hero\" style=\"min-height:auto;padding:40px 0\"><div class=\"epm-hero__content\">");
        sb.Append(EpcEcomaeMarketingCrumb(s.GetProperty("h1").GetString() ?? "", baseUrl + "/solutions", "Solutions"));
        sb.Append("<h1 class=\"epm-section-title\" style=\"font-size:38px\">").Append(H(s.GetProperty("h1").GetString())).Append("</h1>");
        sb.Append("<p class=\"epm-section-lead\" style=\"max-width:820px\">").Append(H(s.GetProperty("lead").GetString())).Append("</p>");
        sb.Append("<div style=\"margin-top:18px\"><a class=\"epm-btn epm-btn--primary\" href=\"").Append(H(baseUrl + "/platform/demo")).Append("\">Get a demo</a> ");
        sb.Append("<a class=\"epm-btn epm-btn--outline\" href=\"").Append(H(baseUrl + "/documentation")).Append("\" style=\"margin-left:8px\">Read the docs</a></div>");
        sb.Append("</div></div><div class=\"epm-section\">");
        foreach (var p in s.GetProperty("body").EnumerateArray())
        {
            sb.Append("<p style=\"max-width:820px;color:#334155;line-height:1.7\">").Append(H(p.GetString())).Append("</p>");
        }

        if (s.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array && features.GetArrayLength() > 0)
        {
            sb.Append("<div class=\"epm-eco-model__grid\" style=\"margin-top:14px\">");
            foreach (var f in features.EnumerateArray())
            {
                sb.Append("<div class=\"epm-card\"><h3 style=\"font-size:16px;margin:0\"><i class=\"fa fa-check text-success\"></i> ").Append(H(f.GetString())).Append("</h3></div>");
            }

            sb.Append("</div>");
        }

        sb.Append(FaqFromJson(s));
        sb.Append("</div></div>");
        sb.Append(FaqJsonFromJson(s));
        sb.Append(EpcEcomaeMarketingArticleJsonld((s.GetProperty("h1").GetString() ?? "") + " — ECOM AE", s.GetProperty("lead").GetString() ?? "", baseUrl + "/solutions/" + slug));
        return sb.ToString();
    }

    public static bool EpcCpTopAlertsUseProfessionalHeader() => true;

    public static bool EpcCpHttpsRedirectIsConfigured(IReadOnlyDictionary<string, object?>? ignored = null)
    {
        var cfg = DpConfig;
        if (cfg == null)
        {
            return false;
        }

        if (!PhpEmpty(Get(cfg, "epc_https_redirect_external")) && Convert.ToString(Get(cfg, "epc_https_redirect_external"), CultureInfo.InvariantCulture) == "1")
        {
            return true;
        }

        var htaccess = Path.Combine(DocumentRoot, ".htaccess");
        if (File.Exists(htaccess))
        {
            var ht = File.ReadAllText(htaccess);
            if (ht.Contains("RewriteRule ^(.*)$ https://%{HTTP_HOST}%{REQUEST_URI} [L,R=301]", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Regex.IsMatch(ht, @"RewriteRule\s+\^\(\.\*\)\$\s+https:\/\/", RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        var domainPath = Convert.ToString(Get(cfg, "domain_path"), CultureInfo.InvariantCulture) ?? "";
        if (domainPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Server.TryGetValue("HTTPS", out var https) && !string.Equals(https, "off", StringComparison.OrdinalIgnoreCase) && https != "")
            {
                return true;
            }

            if (Server.TryGetValue("HTTP_X_FORWARDED_PROTO", out var xfwd) && string.Equals(xfwd, "https", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Server.TryGetValue("HTTP_CF_VISITOR", out var visitor) && visitor.Contains("https", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (Server.TryGetValue("HTTP_CF_RAY", out var ray) && ray != "")
            {
                return true;
            }
        }

        return File.Exists(Path.Combine(DocumentRoot, "content", "epc_https_redirect.ok"));
    }

    public static bool EpcCpTopAlertsSuppressEmailSms() => IsSuperCp() || IsPlatformOperator();

    public static int EpcCpEmailNotificationState(AlertStore db, IReadOnlyDictionary<string, object?> cfg)
    {
        string[] keys = ["from_name", "from_email", "smtp_mode", "smtp_encryption", "smtp_host", "smtp_port", "smtp_username", "smtp_password"];
        if (keys.Any(k => PhpEmpty(Get(cfg, k))))
        {
            return 3;
        }

        var row = db.Debug.FirstOrDefault(r => Convert.ToString(r["name"], CultureInfo.InvariantCulture) == "email");
        if (row == null)
        {
            return 4;
        }

        if (Convert.ToInt32(row["status"], CultureInfo.InvariantCulture) == 1)
        {
            return Clock() - Convert.ToInt64(row["time"], CultureInfo.InvariantCulture) < 86400 ? 1 : 2;
        }

        return 3;
    }

    public static int EpcCpSmsNotificationState(AlertStore db)
    {
        if (db.SmsActive == 0)
        {
            return 3;
        }

        var row = db.Debug.FirstOrDefault(r => Convert.ToString(r["name"], CultureInfo.InvariantCulture) == "sms");
        if (row == null)
        {
            return 4;
        }

        if (Convert.ToInt32(row["status"], CultureInfo.InvariantCulture) == 1)
        {
            return Clock() - Convert.ToInt64(row["time"], CultureInfo.InvariantCulture) < 86400 ? 1 : 2;
        }

        return 3;
    }

    public static bool EpcCpTopAlertsShowInHeader() => !EpcCpTopAlertsUseProfessionalHeader();

    public static string EpcCpTopAlertsRenderSslItem()
    {
        if (!EpcCpTopAlertsShowInHeader())
        {
            return "";
        }

        return "<li class=\"dropdown hidden-sm hidden-xs hidden-md epc-cp-top-connectivity epc-cp-top-connectivity--ssl\"></li>";
    }

    public static Dictionary<string, object?> EpcCpTopAlertsEmailStyles(int state) => AlertStyles(4022, state);

    public static Dictionary<string, object?> EpcCpTopAlertsSmsStyles(int state) => AlertStyles(4028, state);

    public static string EpcCpTopAlertsRenderEmailItem(AlertStore db, IReadOnlyDictionary<string, object?> cfg)
    {
        if (!EpcCpTopAlertsShowInHeader() || EpcCpTopAlertsSuppressEmailSms())
        {
            return "";
        }

        var state = EpcCpEmailNotificationState(db, cfg);
        return state == 1 ? "" : "email";
    }

    public static string EpcCpTopAlertsRenderSmsItem(AlertStore db, IReadOnlyDictionary<string, object?> cfg)
    {
        if (!EpcCpTopAlertsShowInHeader() || EpcCpTopAlertsSuppressEmailSms())
        {
            return "";
        }

        var state = EpcCpSmsNotificationState(db);
        return state == 1 ? "" : "sms";
    }

    private static Dictionary<string, object?> AlertStyles(int titleId, int state)
    {
        return state switch
        {
            1 => new Dictionary<string, object?>(StringComparer.Ordinal) { ["text"] = Translate(titleId) + "<br>" + Translate(4023), ["style"] = "", ["after"] = "", ["severity"] = "ok" },
            2 => new Dictionary<string, object?>(StringComparer.Ordinal) { ["text"] = Translate(titleId) + "<br>" + Translate(4024), ["style"] = "background-color:#f5f5f5;color:#000;", ["after"] = "<i class=\"fa fa-info-circle\"></i>", ["severity"] = "info" },
            3 => new Dictionary<string, object?>(StringComparer.Ordinal) { ["text"] = Translate(titleId) + "<br>" + Translate(4025), ["style"] = "background-color:#F00;color:#FFF;", ["after"] = "<i class=\"fa fa-exclamation-triangle\"></i>", ["severity"] = "error" },
            _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["text"] = Translate(titleId) + "<br>" + Translate(4026), ["style"] = "background-color:#ffde00;color:#000;", ["after"] = "<i class=\"fa fa-exclamation-triangle\"></i>", ["severity"] = "warn" }
        };
    }

    private static Dictionary<string, object?> Uncategorized()
        => new(StringComparer.Ordinal)
        {
            ["category"] = "Uncategorized",
            ["subcategory"] = "",
            ["confidence"] = 0,
            ["tags"] = new List<string>(),
            ["hs_code"] = "",
            ["method"] = "rule"
        };

    private static string FaqFromJson(JsonElement item)
    {
        if (!item.TryGetProperty("faq", out var faq) || faq.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var pairs = faq.EnumerateArray().Select(x =>
        {
            var cells = x.EnumerateArray().ToList();
            return new object?[] { cells[0].GetString(), cells.Count > 1 ? cells[1].GetString() : "" };
        }).ToList();
        return EpcEcomaeMarketingFaqHtml(pairs);
    }

    private static string FaqJsonFromJson(JsonElement item)
    {
        if (!item.TryGetProperty("faq", out var faq) || faq.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var pairs = faq.EnumerateArray().Select(x =>
        {
            var cells = x.EnumerateArray().ToList();
            return new object?[] { cells[0].GetString(), cells.Count > 1 ? cells[1].GetString() : "" };
        }).ToList();
        return EpcEcomaeMarketingFaqJsonld(pairs);
    }

    private static string Slug(IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters == null || !parameters.TryGetValue("slug", out var raw) || raw == null)
        {
            return "";
        }

        return Regex.Replace(Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "", "[^a-z0-9\\-]", "");
    }

    private static string H(object? value) => PhpPlanQ1Plus.EpcChannelH(value);

    private static bool PhpEmpty(object? value)
        => value == null
           || value is false
           || value is 0
           || value is 0L
           || value is 0d
           || (value is string s && (s.Length == 0 || s == "0"));

    private static object? Get(IReadOnlyDictionary<string, object?> cfg, string key)
        => cfg.TryGetValue(key, out var v) ? v : null;

    private static string Str(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";

    private static int Int(IReadOnlyDictionary<string, object?> bag, string key, int fallback)
        => bag.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : fallback;

    private static string ByteSubstr(string text, int bytes)
    {
        var raw = Encoding.UTF8.GetBytes(text);
        if (raw.Length <= bytes)
        {
            return text;
        }

        return Encoding.UTF8.GetString(raw, 0, bytes);
    }

    private static readonly List<Dictionary<string, object?>> Rules =
    [
        Kw("Auto Parts", "Engine Components", "8409", "engine", "piston", "crankshaft", "camshaft", "valve", "cylinder", "gasket", "timing belt", "oil pump"),
        Kw("Auto Parts", "Brakes", "8708", "brake", "disc", "pad", "caliper", "rotor", "brake fluid", "abs"),
        Kw("Auto Parts", "Suspension", "8708", "shock", "absorber", "strut", "spring", "suspension", "bushing", "ball joint", "tie rod", "stabilizer"),
        Kw("Auto Parts", "Electrical", "8511", "alternator", "starter", "ignition", "spark plug", "battery", "wiring", "sensor", "relay", "fuse"),
        Kw("Auto Parts", "Filters", "8421", "air filter", "oil filter", "fuel filter", "cabin filter", "filter element"),
        Kw("Auto Parts", "Body Parts", "8708", "bumper", "fender", "hood", "door", "mirror", "windshield", "grille", "panel", "trunk"),
        Kw("Electronics", "Consumer Electronics", "8471", "phone", "tablet", "laptop", "computer", "monitor", "speaker", "headphone", "camera", "charger"),
        Kw("Fashion", "Clothing", "6109", "shirt", "dress", "pants", "jeans", "jacket", "coat", "sweater", "skirt", "blouse", "t-shirt"),
        Kw("Fashion", "Footwear", "6403", "shoe", "boot", "sandal", "sneaker", "heel", "slipper", "loafer"),
        Kw("Jewellery", "Fine Jewellery", "7113", "gold", "silver", "diamond", "ring", "necklace", "bracelet", "earring", "pendant", "chain", "gemstone"),
        Kw("Jewellery", "Watches", "9101", "watch", "wristwatch", "timepiece", "chronograph")
    ];

    private static readonly string[][] HsSeed =
    [
        ["8409", "Parts for spark-ignition engines", "84", "Machinery", "5.00", "engine piston crankshaft camshaft valve cylinder"],
        ["8421", "Filtering or purifying machinery", "84", "Machinery", "5.00", "filter oil air fuel cabin"],
        ["8511", "Electrical ignition equipment", "85", "Electrical", "5.00", "alternator starter ignition spark plug"],
        ["8708", "Parts for motor vehicles", "87", "Vehicles", "5.00", "brake suspension body bumper fender"],
        ["8471", "Automatic data processing machines", "84", "Machinery", "5.00", "computer laptop tablet monitor"],
        ["6109", "T-shirts, singlets and vests", "61", "Textiles", "5.00", "shirt t-shirt vest singlet"],
        ["6403", "Footwear with outer soles of rubber", "64", "Footwear", "5.00", "shoe boot sneaker sandal"],
        ["7113", "Articles of jewellery", "71", "Precious metals", "5.00", "gold silver ring necklace bracelet earring"],
        ["9101", "Wrist-watches", "91", "Clocks/watches", "5.00", "watch wristwatch chronograph timepiece"],
        ["8544", "Insulated wire and cable", "85", "Electrical", "5.00", "wire cable wiring harness connector"]
    ];

    private static Dictionary<string, object?> Kw(string category, string subcategory, string hs, params string[] keywords)
        => new(StringComparer.Ordinal)
        {
            ["category"] = category,
            ["subcategory"] = subcategory,
            ["keywords"] = keywords,
            ["hs_prefix"] = hs
        };
}
