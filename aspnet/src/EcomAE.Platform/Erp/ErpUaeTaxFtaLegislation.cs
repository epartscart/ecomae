using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Pure PHP <c>epc_uae_fta_parse_legislation_list_html</c> /
/// <c>epc_uae_fta_legislation_tax_category</c> /
/// <c>epc_uae_tax_legislation_enrich_item</c> twin. No HTTP, no DB.
/// </summary>
public static class ErpUaeTaxFtaLegislation
{
    public const string LegislationUrl = "https://tax.gov.ae/en/legislation.aspx";
    public const string UserAgent = "ePartsCart-ERP-UAE-Tax-Compliance/2.0";
    public const int CacheMaxAgeSeconds = 86400;
    public const string CacheKey = "fta_legislation_index";
    public const string CacheKeyPrev = "fta_legislation_index_prev";
    public const string CacheKeySite = "fta_site_updates";
    public const string CacheKeyOverall = "fta_overall_tax_summary";
    public const string NextEventTarget = "ctl00$ctrlContentArea$ctlOpenData$pgList$ctl00$LinkButtonNext";

    public static readonly IReadOnlyDictionary<string, (string TaxType, string Summary, string ErpApply)> Catalog
        = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["vat-decree-8-2017"] = ("vat",
                "Federal Decree-Law No. 8 of 2017 — UAE VAT law: 5% on taxable supplies, registration thresholds, tax invoices, input tax recovery, advances, exports (zero-rated), and penalties.",
                "Map sales/purchases to VAT accounts 2100/1150; advance VAT on customer deposits; zero-rated export flag on e-invoices."),
            ["vat-executive-regulation"] = ("vat",
                "Cabinet Decision / Executive Regulation of VAT — operational rules for tax invoices, credit notes, record keeping, and designated zones.",
                "Use Tax compliance invoice checklist (PINT-AE fields) before ASP submission."),
            ["tax-procedures"] = ("general",
                "Tax Procedures Law & Cabinet Decision No. 74 of 2023 — registration, tax periods, returns, assessments, reconsideration, voluntary disclosure, record retention, and administrative penalties.",
                "Retain GL, e-invoice event log, and purchase tax invoices for FTA audit periods."),
            ["corporate-tax-47-2022"] = ("corporate_tax",
                "Federal Decree-Law No. 47 of 2022 — UAE Corporate Tax 9% on taxable income above small business relief threshold; free zone and foreign PE rules.",
                "P&L provision estimate + CT adjustment fields (entertainment, exempt income, losses)."),
            ["ct-executive-regulation"] = ("corporate_tax",
                "Corporate Tax executive regulations — taxable person definition, tax periods, return filing, and transfer pricing documentation.",
                "Export adjusted profit from ERP P&L tab; file final return on EmaraTax."),
            ["excise-decree"] = ("excise",
                "Excise tax on specified goods (tobacco, energy drinks, carbonated/sweetened beverages) — registration, product listing, returns.",
                "Tag excise SKUs in inventory; excise not auto-calculated in storefront VAT — track separately."),
            ["excise-executive"] = ("excise",
                "Excise executive decisions — rates, designated zones, stock movement, and refund mechanics.",
                "Link import/customs docs for excise imports via Custom & Shipping module."),
            ["einvoicing-decision"] = ("vat",
                "E-invoicing / Peppol (PINT-AE) decisions — structured tax invoice exchange, TRN endpoints 0235:TIN, ASP mandate timelines.",
                "E-Invoicing tab: validate TRN, mandatory fields, XML export, ASP API."),
            ["fta-clarification"] = ("general",
                "FTA decisions on clarifications, public guidance, and administrative penalties — binding process for disputed treatments.",
                "Document non-standard VAT treatments in voucher legislation_ref notes."),
        };

    public static string TaxCategory(string title, string category)
    {
        var hay = (title + " " + category).ToLowerInvariant();
        if (Regex.IsMatch(hay, "excise", RegexOptions.IgnoreCase)) return "excise";
        if (Regex.IsMatch(hay, @"corporate\s*tax|decree.?law\s*no\.?\s*47", RegexOptions.IgnoreCase)) return "corporate_tax";
        if (Regex.IsMatch(hay, @"e.?invoice|peppol|pint", RegexOptions.IgnoreCase)) return "einvoicing";
        if (Regex.IsMatch(hay, @"tax\s*procedures|procedure", RegexOptions.IgnoreCase)) return "procedures";
        if (Regex.IsMatch(hay, @"value\s*added|\bvat\b|decree.?law\s*no\.?\s*8|tourist\s*refund|tax\s*group|judicial\s*expert|directive\s+on\s+tax\s+transactions", RegexOptions.IgnoreCase))
            return "vat";
        return "general";
    }

    public static string MatchPattern(string title, string category)
    {
        var hay = (title + " " + category).ToLowerInvariant();
        if (Regex.IsMatch(hay, "excise", RegexOptions.IgnoreCase))
            return Regex.IsMatch(hay, "executive|cabinet|decision", RegexOptions.IgnoreCase) ? "excise-executive" : "excise-decree";
        if (Regex.IsMatch(hay, @"corporate\s*tax|decree.?law\s*no\.?\s*47", RegexOptions.IgnoreCase))
            return Regex.IsMatch(hay, "executive|cabinet", RegexOptions.IgnoreCase) ? "ct-executive-regulation" : "corporate-tax-47-2022";
        if (Regex.IsMatch(hay, @"e.?invoice|peppol|pint", RegexOptions.IgnoreCase)) return "einvoicing-decision";
        if (Regex.IsMatch(hay, @"tax\s*procedures|procedure", RegexOptions.IgnoreCase)) return "tax-procedures";
        if (Regex.IsMatch(hay, @"clarification|fta\s*decision", RegexOptions.IgnoreCase)) return "fta-clarification";
        if (Regex.IsMatch(hay, @"decree.?law\s*no\.?\s*8|value\s*added\s*tax|\bvat\b", RegexOptions.IgnoreCase))
            return Regex.IsMatch(hay, "executive|cabinet", RegexOptions.IgnoreCase) ? "vat-executive-regulation" : "vat-decree-8-2017";
        return "tax-procedures";
    }

    public static IReadOnlyList<ErpUaeTaxFtaItem> ParseListHtml(string html)
    {
        var items = new List<ErpUaeTaxFtaItem>();
        if (string.IsNullOrEmpty(html)) return items;

        var start = html.IndexOf("headerTable", StringComparison.Ordinal);
        var end = html.IndexOf("ctrlContentArea_ctlOpenData_dvPager", StringComparison.Ordinal);
        if (end < 0) end = html.IndexOf("dvPager", StringComparison.Ordinal);
        var chunk = (start >= 0 && end > start) ? html[start..end] : html;

        var catRx = new Regex(@"<span\s+class=""tag_category"">([^<]+)</span>", RegexOptions.IgnoreCase);
        var matches = catRx.Matches(chunk);
        if (matches.Count == 0) return items;

        foreach (Match catMatch in matches)
        {
            var cat = WebUtility.HtmlDecode(catMatch.Groups[1].Value).Trim();
            var catPos = catMatch.Index;
            var blockStart = Math.Max(0, catPos - 3500);
            var blockLen = Math.Min(chunk.Length - blockStart, (catPos - blockStart) + 1200);
            var block = chunk.Substring(blockStart, blockLen);

            var title = "";
            var titleRx = new Regex(@"<div class=""d-flex"">\s*(.*?)(?:<div id=|<div class=""clear5"")", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var catInBlock = catPos - blockStart;
            Match? tm = null;
            foreach (Match candidate in titleRx.Matches(block))
            {
                if (candidate.Index <= catInBlock)
                {
                    tm = candidate;
                }
            }

            tm ??= titleRx.Match(block);
            if (tm.Success)
            {
                title = Regex.Replace(StripTags(tm.Groups[1].Value), @"\s+", " ").Trim();
            }

            if (title.Length < 8) continue;

            var issueDate = "";
            var publishDate = "";
            var issueRx = new Regex(@"Issue Date\s*:</span>\s*<span[^>]*class=""lastmodifiedDate"">([^<]+)</span>", RegexOptions.IgnoreCase);
            var pubRx = new Regex(@"Publish Date\s*:</span>\s*<span[^>]*class=""lastmodifiedDate"">([^<]+)</span>", RegexOptions.IgnoreCase);
            var im = issueRx.Match(block);
            if (im.Success) issueDate = im.Groups[1].Value.Trim();
            var pm = pubRx.Match(block);
            if (pm.Success) publishDate = pm.Groups[1].Value.Trim();

            var pdfUrl = "";
            var tailLen = Math.Min(1200, chunk.Length - catPos);
            var tail = catPos >= 0 && catPos < chunk.Length ? chunk.Substring(catPos, tailLen) : "";
            var pdfRx = new Regex(@"<a href=""([^""]+\.pdf)""", RegexOptions.IgnoreCase);
            var pdfm = pdfRx.Match(tail);
            if (pdfm.Success)
            {
                pdfUrl = WebUtility.HtmlDecode(pdfm.Groups[1].Value).Replace("//Datafolder", "/Datafolder", StringComparison.Ordinal);
            }

            var isNew = block.Contains("class=\"newicon\"", StringComparison.OrdinalIgnoreCase)
                || block.Contains(">New<", StringComparison.OrdinalIgnoreCase);
            var (slug, itemKey) = SlugAndKey(title, issueDate, publishDate, pdfUrl);
            items.Add(new ErpUaeTaxFtaItem(
                Slug: slug,
                ItemKey: itemKey,
                Title: title,
                IssueDate: issueDate,
                PublishDate: publishDate,
                Category: cat,
                TaxCategory: TaxCategory(title, cat),
                PdfUrl: pdfUrl,
                FtaNewBadge: isNew));
        }

        return items;
    }

    public static (string Slug, string ItemKey) SlugAndKey(string title, string issueDate, string publishDate, string pdfUrl)
    {
        var slugBase = Slugify(title + "-" + issueDate + "-" + publishDate, 100);
        var slug = slugBase;
        if (!string.IsNullOrEmpty(pdfUrl))
        {
            var pdfPart = Slugify(Path.GetFileName(pdfUrl.Replace('\\', '/')), 80);
            slug = TrimSlug((pdfPart + "-" + slugBase), 120);
        }

        var itemKey = !string.IsNullOrEmpty(pdfUrl) ? pdfUrl : (title + "|" + issueDate + "|" + publishDate);
        return (slug, itemKey);
    }

    public static Dictionary<string, string> ExtractFormFields(string html)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(html)) return fields;
        var rx = new Regex(@"<input[^>]+name=""([^""]+)""[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match m in rx.Matches(html))
        {
            var tag = m.Value;
            if (Regex.IsMatch(tag, @"type\s*=\s*""(?:submit|button|image)""", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var val = "";
            var vm = Regex.Match(tag, @"value=""([^""]*)""", RegexOptions.IgnoreCase);
            if (vm.Success) val = vm.Groups[1].Value;
            fields[m.Groups[1].Value] = val;
        }

        return fields;
    }

    public static (int TotalReported, int PageCount) ReadPagerMeta(string html)
    {
        var total = 0;
        var pages = 1;
        var tm = Regex.Match(html ?? "", @"(\d+)\s+Items found", RegexOptions.IgnoreCase);
        if (tm.Success) total = int.Parse(tm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var pm = Regex.Match(html ?? "", @"""_pageCount"":(\d+)");
        if (pm.Success) pages = Math.Max(1, int.Parse(pm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        return (total, pages);
    }

    public static void MarkDiffFlags(IList<ErpUaeTaxFtaItem> items, IReadOnlyDictionary<string, string> prevKeys)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.ItemKey.Length == 0) continue;
            if (!prevKeys.ContainsKey(item.ItemKey))
            {
                items[i] = item with { IsNew = true };
            }
            else if (!string.Equals(prevKeys[item.ItemKey], item.IssueDate, StringComparison.Ordinal))
            {
                items[i] = item with { IsChanged = true, IsUpdated = true };
            }
        }
    }

    public static ErpUaeTaxFtaItem Enrich(ErpUaeTaxFtaItem item)
    {
        var patternKey = MatchPattern(item.Title, item.Category);
        var taxCategory = TaxCategory(item.Title, item.Category);
        var built = BuildSummary(item, patternKey);
        var actions = BuildComplianceActions(item with { PatternKey = patternKey, TaxCategory = taxCategory, ErpSummary = built.ErpSummary });
        return item with
        {
            PatternKey = patternKey,
            TaxCategory = taxCategory,
            ErpSummary = built.ErpSummary,
            ErpApply = built.ErpApply,
            ComplianceActions = actions,
            IsUpdated = item.IsUpdated || item.IsChanged,
        };
    }

    public static IReadOnlyList<string> TopicActions(string title, string category = "", string pdfExcerpt = "")
    {
        var hay = (title + " " + category + " " + pdfExcerpt).ToLowerInvariant();
        if (Regex.IsMatch(hay, @"judicial|expert\s*service", RegexOptions.IgnoreCase))
        {
            return
            [
                "Classify judicial expert fee invoices: set sales VAT treatment to standard 5% where the directive treats the service as a taxable supply (or exempt/out-of-scope if the directive excludes it).",
                "Create/update a purchase expense type for “Judicial expert / court fees” with the correct input VAT recovery flag.",
                "Require tax invoice fields (supplier TRN, description “judicial expert services”, court/case ref in notes) before posting the AP voucher.",
                "Tag GL journals with legislation_ref = this directive number; include expert fees in the VAT return Box for standard-rated purchases/supplies in the correct period.",
                "Block posting of expert-fee bills without a valid tax invoice (or approved self-billing arrangement if applicable).",
            ];
        }

        if (Regex.IsMatch(hay, @"tax\s*group", RegexOptions.IgnoreCase) && Regex.IsMatch(hay, @"exit|leav|adjust", RegexOptions.IgnoreCase))
        {
            return
            [
                "Record the Tax Group exit date on the company/VAT profile; stop using the group representative TRN for the exiting member from that date.",
                "Raise VAT adjustment journals for open intra-group balances that become taxable supplies on exit (output VAT / corresponding input VAT).",
                "Re-code open AR/AP between exiting member and remaining members to standard-rated (or correct treatment) for supplies after exit.",
                "File the exit-period VAT return with adjustment boxes completed; attach exit calculation worksheet in records.",
                "Stamp affected GL vouchers with legislation_ref = this directive; retain group membership history for FTA audit.",
            ];
        }

        if (Regex.IsMatch(hay, @"tax\s*group", RegexOptions.IgnoreCase))
        {
            return
            [
                "Maintain Tax Group member list (TRNs) in company profile; invoice under the representative TRN while membership is active.",
                "Suppress VAT on intra-group supplies (treat as disregarded) while both parties remain in the group.",
                "Include all members’ external supplies in the representative’s VAT return consolidation.",
            ];
        }

        if (Regex.IsMatch(hay, "tourist", RegexOptions.IgnoreCase) && Regex.IsMatch(hay, "refund", RegexOptions.IgnoreCase))
        {
            return
            [
                "Enable tourist-refund eligible sales flag on retail tax invoices (passport/eligibility captured where scheme requires).",
                "Do not post tourist VAT refunds as ordinary credit notes (381) — use the scheme refund process / operator interface.",
                "Reconcile tourist refund claims to original tax invoice numbers before reducing output VAT in the return.",
                "Update invoice template notes with Tourist Refund Scheme eligibility text per FTA Decision No. 2 of 2018 (as amended).",
                "Retain refund operator acknowledgements with the tax invoice PDF for the statutory record period.",
            ];
        }

        if (Regex.IsMatch(hay, @"e.?invoice|peppol|pint", RegexOptions.IgnoreCase))
        {
            return
            [
                "Configure seller TRN (15 digits) and Peppol endpoint 0235:TIN on the E-Invoicing tab.",
                "Validate all PINT-AE mandatory fields before ASP submission; reject incomplete invoices.",
                "Map invoice type 380 / credit note 381 and test XML export against the accredited ASP.",
                "Diary the Cabinet/Ministerial go-live date; block non-compliant channels after mandate.",
            ];
        }

        if (Regex.IsMatch(hay, @"credit\s*note", RegexOptions.IgnoreCase))
        {
            return
            [
                "Issue credit notes (type 381) that reference the original tax invoice number and date.",
                "Reverse output/input VAT in the period the credit note is issued; link legislation_ref on the voucher.",
            ];
        }

        if (Regex.IsMatch(hay, @"reverse\s*charge", RegexOptions.IgnoreCase))
        {
            return
            [
                "Set purchase VAT treatment to reverse_charge for in-scope imported services/goods.",
                "Auto-post output VAT (2100) and matching input VAT (1150) on reverse-charge purchases where recoverable.",
                "Include reverse-charge lines in the correct VAT return boxes for the period.",
            ];
        }

        if (Regex.IsMatch(hay, @"designated\s*zone", RegexOptions.IgnoreCase))
        {
            return
            [
                "Flag designated-zone customers/warehouses; set place-of-supply / zero-rated or out-of-scope treatment per the decision.",
                "Block 5% VAT on qualifying designated-zone goods movements; retain zone evidence on the invoice.",
            ];
        }

        if (Regex.IsMatch(hay, @"zero.?rat|export", RegexOptions.IgnoreCase))
        {
            return
            [
                "Mark export invoices as VAT category Z; require bill of lading / export evidence before zero-rating.",
                "Exclude zero-rated exports from 5% output VAT and report them in the export box of the VAT return.",
            ];
        }

        if (Regex.IsMatch(hay, @"transfer\s*pric", RegexOptions.IgnoreCase))
        {
            return
            [
                "Document related-party transactions; post CT add-backs on the P&L Corporate Tax adjustments tab.",
                "Retain master file / local file references with the CT provision working papers.",
            ];
        }

        if (Regex.IsMatch(hay, @"voluntary\s*disclosure", RegexOptions.IgnoreCase))
        {
            return
            [
                "Prepare voluntary disclosure pack from ERP (return period, box deltas, supporting invoices).",
                "Post correcting journals with legislation_ref and file the disclosure on EmaraTax before penalty escalation.",
            ];
        }

        if (Regex.IsMatch(hay, @"penalt|administrative\s*penalt", RegexOptions.IgnoreCase))
        {
            return
            [
                "Review filing calendar (VAT/CT) and enable reminder tasks before EmaraTax deadlines.",
                "Ensure tax invoice mandatory fields are complete to avoid incorrect-invoice penalties.",
            ];
        }

        if (Regex.IsMatch(hay, @"corporate\s*tax|decree.?law\s*no\.?\s*47", RegexOptions.IgnoreCase))
        {
            return
            [
                "Estimate 9% CT on adjusted profit above AED 375,000 on the P&L tab.",
                "Complete CT add-back / deduction lines (entertainment, exempt income, losses) for the tax period.",
                "Export adjusted taxable profit and file the CT return on EmaraTax.",
            ];
        }

        if (Regex.IsMatch(hay, "excise", RegexOptions.IgnoreCase))
        {
            return
            [
                "Tag excise-affected SKUs in inventory; keep excise separate from storefront 5% VAT.",
                "Attach customs/import evidence for excise imports; file excise returns on EmaraTax.",
            ];
        }

        if (Regex.IsMatch(hay, @"directive\s+on\s+tax\s+transactions", RegexOptions.IgnoreCase))
        {
            return
            [
                "Read the directive scope and effective date; list ERP transaction types that match the scenario.",
                "Update sales/purchase VAT treatments for matching lines from the effective date; stamp legislation_ref on vouchers.",
                "Include impacted supplies/deductions in the UAE VAT return for the first period after the effective date.",
                "Retain the FTA PDF with the ERP change log until the record-keeping period ends.",
            ];
        }

        return [];
    }

    public static IReadOnlyList<string> BuildComplianceActions(ErpUaeTaxFtaItem item)
    {
        var actions = TopicActions(item.Title, item.Category).ToList();
        if (actions.Count == 0)
        {
            var tt = item.TaxCategory;
            var pk = item.PatternKey;
            if (tt is "vat" or "einvoicing" || pk == "einvoicing-decision")
            {
                actions.Add("Validate tax invoice mandatory fields (PINT-AE) before ASP submission for supplies covered by this instrument.");
                actions.Add("Include affected supplies/deductions in the UAE VAT return for the first open period after the issue date.");
            }

            if (tt == "vat")
            {
                actions.Add("Set purchase VAT treatment (standard, zero-rated, reverse charge) to match this instrument’s scope.");
                actions.Add("Post VAT/CT to GL (2100 output, 1150 input) and set legislation_ref = this instrument on vouchers.");
            }

            if (tt == "corporate_tax")
            {
                actions.Add("Review P&L Corporate Tax provision and CT adjustment fields for impacts of this instrument.");
                actions.Add("Estimate 9% CT on adjusted profit above AED 375,000 threshold (P&L tab).");
            }

            if (tt == "excise")
            {
                actions.Add("Tag excise-affected SKUs; keep excise separate from storefront VAT.");
                actions.Add("Register excise products on EmaraTax; file excise returns outside storefront VAT.");
            }

            if (pk == "tax-procedures" || tt is "procedures" or "general")
            {
                actions.Add("Retain the FTA PDF, ERP event log, and supporting docs for the statutory record-keeping period.");
                actions.Add("File returns and voluntary disclosures on EmaraTax by published deadlines.");
            }

            if (pk == "einvoicing-decision")
            {
                actions.Add("Configure seller TRN, Peppol endpoint 0235:TIN, and XML export for mandated go-live.");
            }

            if (tt == "vat" && pk == "vat-decree-8-2017")
            {
                actions.Add("Apply 5% VAT on taxable supplies; zero-rate exports (category Z) where conditions met.");
                actions.Add("Record output VAT on customer advance payments; credit on final tax invoice.");
            }
        }

        var refTitle = string.IsNullOrWhiteSpace(item.Title) ? "this FTA instrument" : item.Title;
        actions.Add("Mark each checklist step done in ERP once implemented; leave as Pending until the treatment is live for " + refTitle + ".");
        return actions.Distinct(StringComparer.Ordinal).ToList();
    }

    public static Dictionary<string, object> OverallSummaries(IReadOnlyList<ErpUaeTaxFtaItem> legislation)
    {
        var families = new[] { "vat", "corporate_tax", "excise", "procedures", "einvoicing" };
        var counts = families.ToDictionary(f => f, _ => 0, StringComparer.Ordinal);
        var newCounts = families.ToDictionary(f => f, _ => 0, StringComparer.Ordinal);
        foreach (var raw in legislation)
        {
            var leg = string.IsNullOrEmpty(raw.PatternKey) ? Enrich(raw) : raw;
            var pk = leg.PatternKey;
            var tt = string.IsNullOrEmpty(leg.TaxCategory) ? "general" : leg.TaxCategory;
            var family = "procedures";
            if (pk == "einvoicing-decision" || tt == "einvoicing") family = "einvoicing";
            else if (tt == "vat") family = "vat";
            else if (tt == "corporate_tax") family = "corporate_tax";
            else if (tt == "excise") family = "excise";
            counts[family]++;
            if (leg.IsNew) newCounts[family]++;
        }

        var titles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vat"] = "Value Added Tax (VAT)",
            ["corporate_tax"] = "Corporate Tax (CT)",
            ["excise"] = "Excise Tax",
            ["procedures"] = "Tax Procedures & Penalties",
            ["einvoicing"] = "E-Invoicing (PINT-AE)",
        };
        var outMap = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var fk in families)
        {
            var cnt = counts[fk];
            var bullets = new List<string> { cnt + " FTA instrument(s) in the legislation library." };
            if (newCounts[fk] > 0)
            {
                bullets.Add(newCounts[fk] + " new since last fetch — expand rows below for ERP checklist.");
            }

            outMap[fk] = new Dictionary<string, object>
            {
                ["key"] = fk,
                ["title"] = titles[fk],
                ["item_count"] = cnt,
                ["new_count"] = newCounts[fk],
                ["summary"] = string.Join(" ", bullets),
                ["bullets"] = bullets,
            };
        }

        return outMap;
    }

    public static Dictionary<string, string> PrevKeysFromPayloadJson(string? json)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return map;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;
            if (!doc.RootElement.TryGetProperty("legislation", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                if (!doc.RootElement.TryGetProperty("items", out arr) || arr.ValueKind != JsonValueKind.Array)
                {
                    return map;
                }
            }

            foreach (var el in arr.EnumerateArray())
            {
                var key = el.TryGetProperty("item_key", out var k) ? k.GetString() ?? "" : "";
                if (key.Length == 0 && el.TryGetProperty("slug", out var s)) key = s.GetString() ?? "";
                var issue = el.TryGetProperty("issue_date", out var d) ? d.GetString() ?? "" : "";
                if (key.Length > 0) map[key] = issue;
            }
        }
        catch (JsonException)
        {
            return map;
        }

        return map;
    }

    public static bool CacheHasLegislation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (doc.RootElement.TryGetProperty("legislation", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
            {
                return true;
            }

            return doc.RootElement.TryGetProperty("items", out arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static (string ErpSummary, string ErpApply) BuildSummary(ErpUaeTaxFtaItem item, string patternKey)
    {
        if (!Catalog.TryGetValue(patternKey, out var pat))
        {
            pat = Catalog["tax-procedures"];
        }

        var title = item.Title.Trim();
        var category = item.Category.Trim();
        var issueDate = item.IssueDate.Trim();
        var hay = (title + " " + category).ToLowerInvariant();
        var docType = "legislation";
        if (Regex.IsMatch(hay, @"directive\s+on\s+tax\s+transactions", RegexOptions.IgnoreCase)) docType = "FTA Directive on Tax Transactions";
        else if (Regex.IsMatch(hay, @"federal\s+tax\s+authority\s+decision|fta\s+decision", RegexOptions.IgnoreCase)) docType = "FTA Decision";
        else if (Regex.IsMatch(hay, @"federal\s+decree.?law", RegexOptions.IgnoreCase)) docType = "Federal Decree-Law";
        else if (Regex.IsMatch(hay, @"cabinet\s+decision|ministerial\s+decision", RegexOptions.IgnoreCase)) docType = "Cabinet/Ministerial decision";
        else if (Regex.IsMatch(hay, @"executive\s+regulation", RegexOptions.IgnoreCase)) docType = "Executive regulation";
        else if (Regex.IsMatch(hay, @"clarification|public\s+clarification", RegexOptions.IgnoreCase)) docType = "FTA clarification";

        var year = "";
        var ym = Regex.Match(title, @"\b(20\d{2})\b");
        if (ym.Success) year = ym.Groups[1].Value;
        else
        {
            var ym2 = Regex.Match(issueDate, @"\b(20\d{2})\b");
            if (ym2.Success) year = ym2.Groups[1].Value;
        }

        var baseSummary = TopicLead(title, category);
        if (baseSummary.Length == 0) baseSummary = pat.Summary;

        var specific = "";
        if (Regex.IsMatch(hay, "amend", RegexOptions.IgnoreCase) && !Regex.IsMatch(hay, "tourist", RegexOptions.IgnoreCase))
            specific = "Amends prior provisions — review whether ERP tax codes, invoice templates, or CT adjustments need updating.";
        else if (Regex.IsMatch(hay, @"penalt|fine|sanction", RegexOptions.IgnoreCase))
            specific = "Covers penalties and enforcement — ensure audit trail and voluntary disclosure process in ERP records.";
        else if (Regex.IsMatch(hay, "tourist", RegexOptions.IgnoreCase))
            specific = "Tourist refund scheme — ensure retail tax invoices capture passport/eligibility data where refunds apply; do not treat tourist refunds as ordinary credit notes.";
        else if (Regex.IsMatch(hay, @"tax\s*group|exit\s*from", RegexOptions.IgnoreCase))
            specific = "Tax group exit adjustments — re-map supplies between remaining members and the exiting registrant; correct VAT return boxes for the exit period.";
        else if (Regex.IsMatch(hay, @"judicial|expert\s*service", RegexOptions.IgnoreCase))
            specific = "Judicial expert services — confirm whether the service is a taxable supply (5%) or exempt/out-of-scope; code purchase and sales VAT treatments accordingly.";
        else if (Regex.IsMatch(hay, @"refund|recovery|credit\s*note", RegexOptions.IgnoreCase))
            specific = "Addresses refunds/credits — map credit notes (381) and input VAT recovery in purchases and VAT return.";
        else if (Regex.IsMatch(hay, @"designated\s*zone|free\s*zone", RegexOptions.IgnoreCase))
            specific = "Designated/free zone rules — verify supply location and zero-rated/exempt flags on affected transactions.";
        else if (Regex.IsMatch(hay, @"reverse\s*charge|import", RegexOptions.IgnoreCase))
            specific = "Import/reverse-charge treatment — set purchase VAT treatment to reverse_charge or import_rc where applicable.";

        var parts = new List<string>
        {
            docType + (year.Length > 0 ? " (" + year + ")" : "") + ": " + (title.Length > 0 ? title : "FTA legislation") + ".",
            baseSummary,
        };
        if (specific.Length > 0) parts.Add(specific);
        parts.Add(pat.ErpApply);
        if (issueDate.Length > 0) parts.Add("FTA issue date: " + issueDate + ".");
        if (category.Length > 0) parts.Add("Category: " + category + ".");

        var erpSummary = Regex.Replace(string.Join(" ", parts.Where(p => p.Length > 0)), @"\s+", " ").Trim();
        if (erpSummary.Length == 0)
        {
            erpSummary = "UAE FTA legislation — apply ERP tax settings per advisor guidance. Category: " + (category.Length > 0 ? category : "general") + ".";
        }

        if (item.IsNew) erpSummary = "[NEW since last fetch] " + erpSummary;
        else if (item.IsChanged || item.IsUpdated) erpSummary = "[UPDATED — issue date changed] " + erpSummary;
        return (erpSummary, pat.ErpApply);
    }

    private static string TopicLead(string title, string category)
    {
        var hay = (title + " " + category).ToLowerInvariant();
        if (Regex.IsMatch(hay, @"judicial|expert\s*service", RegexOptions.IgnoreCase))
            return "VAT on judicial expert services — sets when expert fees charged in court/arbitration proceedings are taxable supplies (5%), who is the supplier, and how tax invoices must be issued for those services.";
        if (Regex.IsMatch(hay, @"tax\s*group", RegexOptions.IgnoreCase) && Regex.IsMatch(hay, @"exit|leav|adjust", RegexOptions.IgnoreCase))
            return "VAT adjustments when a registrant exits a Tax Group — governs output/input tax corrections, supplies between the exiting member and remaining members, and VAT return reporting for the exit tax period.";
        if (Regex.IsMatch(hay, @"tax\s*group", RegexOptions.IgnoreCase))
            return "VAT Tax Group rules — single representative registrant, intra-group supplies disregarded, and joint liability; ERP must track group membership and member TRNs.";
        if (Regex.IsMatch(hay, "tourist", RegexOptions.IgnoreCase) && Regex.IsMatch(hay, "refund", RegexOptions.IgnoreCase))
            return "Tax Refunds for Tourists Scheme — conditions for VAT refunds to eligible tourists, retailer/operator obligations, and documentation that must appear on the tax invoice.";
        if (Regex.IsMatch(hay, @"e.?invoice|peppol|pint", RegexOptions.IgnoreCase))
            return "E-invoicing / PINT-AE mandate — structured tax invoice exchange via accredited ASP on Peppol; TRN endpoint 0235:TIN and mandatory field validation.";
        if (Regex.IsMatch(hay, @"credit\s*note", RegexOptions.IgnoreCase))
            return "Credit notes under UAE VAT — type 381 documents must reference the original tax invoice number and date and adjust output/input VAT in the correct period.";
        if (Regex.IsMatch(hay, @"reverse\s*charge", RegexOptions.IgnoreCase))
            return "Reverse-charge VAT — recipient accounts for output VAT and claims input VAT (where recoverable) on specified imported services/goods.";
        if (Regex.IsMatch(hay, @"designated\s*zone", RegexOptions.IgnoreCase))
            return "Designated zone VAT rules — supplies of goods in/between designated zones may be outside the scope or zero-rated; verify place of supply flags in ERP.";
        if (Regex.IsMatch(hay, @"zero.?rat|export", RegexOptions.IgnoreCase))
            return "Zero-rated supplies / exports — category Z treatment requires export evidence; ERP must flag export invoices and exclude them from 5% output VAT.";
        if (Regex.IsMatch(hay, @"transfer\s*pric", RegexOptions.IgnoreCase))
            return "Transfer pricing for Corporate Tax — related-party pricing must be arm's length; document and post CT add-backs on the P&L tab.";
        if (Regex.IsMatch(hay, @"voluntary\s*disclosure", RegexOptions.IgnoreCase))
            return "Voluntary disclosure procedures — correct under-/over-declared VAT or CT via EmaraTax and retain ERP audit evidence for the disclosure period.";
        if (Regex.IsMatch(hay, @"penalt|administrative\s*penalt", RegexOptions.IgnoreCase))
            return "Administrative penalties — late registration, late filing, and incorrect tax invoices; ERP retention and filing calendars reduce exposure.";
        if (Regex.IsMatch(hay, @"directive\s+on\s+tax\s+transactions", RegexOptions.IgnoreCase))
            return "FTA Directive on Tax Transactions — binding operational guidance for a specific VAT scenario; apply the treatment in ERP for matching supplies and purchases from the effective date.";
        return "";
    }

    public static string Slugify(string raw, int maxLen)
    {
        var slug = Regex.Replace((raw ?? "").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return TrimSlug(slug, maxLen);
    }

    private static string TrimSlug(string slug, int maxLen)
    {
        if (slug.Length > maxLen) slug = slug[..maxLen];
        return slug.Trim('-');
    }

    private static string StripTags(string html) => Regex.Replace(html ?? "", "<[^>]+>", "");
}

public sealed record ErpUaeTaxFtaItem(
    string Slug,
    string ItemKey,
    string Title,
    string IssueDate,
    string PublishDate,
    string Category,
    string TaxCategory,
    string PdfUrl,
    bool FtaNewBadge = false,
    bool IsNew = false,
    bool IsChanged = false,
    bool IsUpdated = false,
    string PatternKey = "",
    string ErpSummary = "",
    string ErpApply = "",
    IReadOnlyList<string>? ComplianceActions = null);
