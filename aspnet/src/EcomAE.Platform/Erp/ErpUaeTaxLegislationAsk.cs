using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>PHP <c>epc_uae_tax_legislation_tokenize</c> / score / synthesize twin. Search + extract only — no external AI.</summary>
public static class ErpUaeTaxLegislationAsk
{
    public const string Disclaimer = "Indicative only; confirm with tax advisor and FTA EmaraTax.";
    public const string HistoryCacheKey = "legislation_qa_history";

    public static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "from",
        "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did",
        "will", "would", "should", "could", "may", "might", "must", "shall", "can", "need", "d",
        "i", "me", "my", "we", "our", "you", "your", "he", "she", "it", "they", "them", "their",
        "this", "that", "these", "those", "what", "which", "who", "whom", "how", "when", "where", "why",
        "about", "into", "through", "during", "before", "after", "above", "below", "between", "under",
        "over", "again", "further", "then", "once", "here", "there", "all", "each", "few", "more",
        "most", "other", "some", "such", "no", "nor", "not", "only", "own", "same", "so", "than",
        "too", "very", "just", "also", "now", "tell", "explain", "please", "give", "know", "want",
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> FamilyBullets
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["vat"] =
            [
                "Federal Decree-Law No. 8 of 2017 — 5% VAT on taxable supplies in the UAE.",
                "Mandatory VAT registration when taxable supplies exceed AED 375,000 in 12 months.",
                "Tax invoices, credit notes (381), input VAT recovery, and export zero-rating (category Z).",
            ],
            ["corporate_tax"] =
            [
                "Federal Decree-Law No. 47 of 2022 — 9% Corporate Tax on adjusted taxable income.",
                "Small business relief: no CT on taxable income up to AED 375,000 (threshold configurable in ERP).",
            ],
            ["excise"] =
            [
                "Federal Decree-Law No. 7 of 2017 — excise on tobacco, energy drinks, carbonated/sweetened beverages.",
            ],
            ["procedures"] =
            [
                "Cabinet Decision No. 74 of 2023 — Tax Procedures Law executive regulation.",
            ],
            ["einvoicing"] =
            [
                "PINT-AE structured tax invoice standard — mandatory fields and XML schema.",
            ],
        };

    public static IReadOnlyList<string> Tokenize(string text)
    {
        var cleaned = Regex.Replace((text ?? "").Trim().ToLowerInvariant(), @"[^a-z0-9\s%]", " ");
        var parts = Regex.Split(cleaned, @"\s+");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outList = new List<string>();
        foreach (var p in parts)
        {
            if (p.Length < 2 || Stopwords.Contains(p) || !seen.Add(p)) continue;
            outList.Add(p);
        }

        return outList;
    }

    public static Dictionary<string, int> IntentBoosts(string question)
    {
        var q = question ?? "";
        var boosts = new Dictionary<string, int>(StringComparer.Ordinal);
        void Hit(string pattern, string[] keys, int bonus, string tax)
        {
            if (!Regex.IsMatch(q, pattern, RegexOptions.IgnoreCase)) return;
            foreach (var pk in keys)
            {
                boosts[pk] = Math.Max(boosts.GetValueOrDefault(pk), bonus);
            }

            if (tax.Length > 0)
            {
                var tk = "__tax_" + tax;
                boosts[tk] = Math.Max(boosts.GetValueOrDefault(tk), bonus - 5);
            }
        }

        Hit(@"\b(vat\s*rate|standard\s*rate|5\s*%|five\s*percent)\b", ["vat-decree-8-2017"], 25, "vat");
        Hit(@"\b(advance\s*payment|deposit|pre.?payment|customer\s*advance)\b", ["vat-decree-8-2017"], 22, "vat");
        Hit(@"\b(export|zero.?rat|zero\s*rating|outside\s*uae|international\s*supply)\b", ["vat-decree-8-2017", "vat-executive-regulation"], 22, "vat");
        Hit(@"\b(trn|tax\s*registration\s*number|0235)\b", ["einvoicing-decision", "tax-procedures"], 20, "einvoicing");
        Hit(@"\b(corporate\s*tax|ct\s*rate|9\s*%|nine\s*percent|taxable\s*income|375\s*000|375000)\b", ["corporate-tax-47-2022", "ct-executive-regulation"], 24, "corporate_tax");
        Hit(@"\b(excise|tobacco|energy\s*drink|sweetened|carbonated)\b", ["excise-decree", "excise-executive"], 22, "excise");
        Hit(@"\b(registr|register|mandatory\s*registration|375\s*000)\b", ["tax-procedures", "vat-decree-8-2017"], 18, "procedures");
        Hit(@"\b(filing|deadline|return\s*due|tax\s*period|emaratax)\b", ["tax-procedures"], 18, "procedures");
        Hit(@"\b(input\s*vat|recover|recovery|deduction|credit\s*note)\b", ["vat-decree-8-2017", "vat-executive-regulation"], 20, "vat");
        Hit(@"\b(entertainment|blocked|non.?deduct|add.?back)\b", ["corporate-tax-47-2022", "ct-executive-regulation"], 20, "corporate_tax");
        Hit(@"\b(e.?invoice|peppol|pint|asp)\b", ["einvoicing-decision"], 20, "einvoicing");
        Hit(@"\b(reverse\s*charge|import\s*vat|customs)\b", ["vat-decree-8-2017", "vat-executive-regulation"], 16, "vat");
        return boosts;
    }

    public static float ScoreItem(ErpUaeTaxFtaItem item, IReadOnlyList<string> tokens, IReadOnlyDictionary<string, int> boosts)
    {
        if (tokens.Count == 0) return 0f;
        var actions = item.ComplianceActions ?? [];
        var fields = new Dictionary<string, (string Hay, float Weight)>(StringComparer.Ordinal)
        {
            ["title"] = ((item.Title ?? "").ToLowerInvariant(), 3f),
            ["erp_summary"] = ((item.ErpSummary ?? "").ToLowerInvariant(), 2f),
            ["category"] = ((item.Category ?? "").ToLowerInvariant(), 1.5f),
            ["tax_category"] = ((item.TaxCategory ?? "").ToLowerInvariant(), 2f),
        };
        var actionHay = string.Join(' ', actions).ToLowerInvariant();
        var score = 0f;
        foreach (var tok in tokens)
        {
            foreach (var field in fields.Values)
            {
                if (tok.Length > 0 && field.Hay.Contains(tok, StringComparison.Ordinal))
                    score += field.Weight;
            }

            if (tok.Length > 0 && actionHay.Contains(tok, StringComparison.Ordinal))
                score += 1.5f;
        }

        var pk = item.PatternKey ?? "";
        if (pk.Length > 0 && boosts.TryGetValue(pk, out var b)) score += b;
        var tt = item.TaxCategory ?? "";
        if (tt.Length > 0 && boosts.TryGetValue("__tax_" + tt, out var tb)) score += tb;
        var title = (item.Title ?? "").ToLowerInvariant();
        if (Regex.IsMatch(title, @"decree.?law\s*no\.?\s*8|value\s*added\s*tax", RegexOptions.IgnoreCase))
        {
            foreach (var kw in new[] { "vat", "rate", "5", "percent", "taxable" })
            {
                if (tokens.Contains(kw, StringComparer.Ordinal)) score += 2f;
            }
        }

        return (float)Math.Round(score, 2);
    }

    public static IReadOnlyList<(ErpUaeTaxFtaItem Item, float Score)> Rank(
        IReadOnlyList<ErpUaeTaxFtaItem> items,
        IReadOnlyList<string> tokens,
        IReadOnlyDictionary<string, int> boosts)
    {
        var scored = new List<(ErpUaeTaxFtaItem Item, float Score)>();
        foreach (var item in items)
        {
            var s = ScoreItem(item, tokens, boosts);
            if (s > 0) scored.Add((item, s));
        }

        scored.Sort((a, b) =>
        {
            var c = b.Score.CompareTo(a.Score);
            return c != 0 ? c : string.Compare(a.Item.Title, b.Item.Title, StringComparison.Ordinal);
        });
        var topN = Math.Min(5, Math.Max(3, scored.Count));
        var top = scored.Take(topN).ToList();
        if (top.Count == 0)
        {
            var fallbackPk = boosts.Keys.Where(k => !k.StartsWith("__tax_", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item.PatternKey.Length > 0 && fallbackPk.Contains(item.PatternKey))
                    top.Add((item, boosts.GetValueOrDefault(item.PatternKey)));
            }

            top.Sort((a, b) => b.Score.CompareTo(a.Score));
            top = top.Take(5).ToList();
        }

        if (top.Count == 0)
        {
            foreach (var item in items.Take(3))
                top.Add((item, 1f));
        }

        return top;
    }

    public static IReadOnlyList<string> ExtractBullets(string text, int maxBullets = 3)
    {
        text = Regex.Replace((text ?? "").Trim(), @"\s+", " ");
        if (text.Length == 0) return [];
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+(?=[A-Z0-9""\(])");
        var bullets = new List<string>();
        foreach (var raw in sentences.Length == 0 ? [text] : sentences)
        {
            var s = raw.Trim();
            if (s.Length < 20) continue;
            bullets.Add(s);
            if (bullets.Count >= maxBullets) break;
        }

        return bullets;
    }

    public static IReadOnlyList<string> Synthesize(string question, IReadOnlyList<ErpUaeTaxFtaItem> matches, IReadOnlyDictionary<string, int> boosts)
    {
        var bullets = new List<string>();
        var q = question ?? "";
        if (Regex.IsMatch(q, @"\b(vat\s*rate|standard\s*rate|what.*rate)\b", RegexOptions.IgnoreCase))
        {
            bullets.Add("UAE standard VAT rate is 5% on taxable supplies (Federal Decree-Law No. 8 of 2017).");
            bullets.Add("Zero-rated (0%) and exempt supplies are treated separately — see export and designated-zone rules in the matched legislation.");
        }

        if (Regex.IsMatch(q, @"\b(advance\s*payment|deposit|pre.?payment)\b", RegexOptions.IgnoreCase))
            bullets.Add("Output VAT is due on taxable advance payments received before supply — record in ERP and credit against the final tax invoice.");
        if (Regex.IsMatch(q, @"\b(export|zero.?rat)\b", RegexOptions.IgnoreCase))
            bullets.Add("Exports of goods/services outside the GCC implementing states may be zero-rated (category Z) when conditions in VAT law and executive regulations are met.");
        if (Regex.IsMatch(q, @"\b(corporate\s*tax|ct\s*rate|9\s*%|threshold|375)\b", RegexOptions.IgnoreCase))
            bullets.Add("UAE Corporate Tax is 9% on taxable income above AED 375,000 (Federal Decree-Law No. 47 of 2022); small business relief applies at or below the threshold.");
        if (Regex.IsMatch(q, @"\b(trn)\b", RegexOptions.IgnoreCase))
            bullets.Add("UAE TRN is 15 digits; mandatory on tax invoices and Peppol endpoint 0235:TIN for e-invoicing.");
        if (Regex.IsMatch(q, @"\b(input\s*vat|recover)\b", RegexOptions.IgnoreCase))
            bullets.Add("Input VAT is recoverable on business expenses with valid tax invoices, subject to blocked categories (e.g. entertainment) per VAT law.");
        if (Regex.IsMatch(q, @"\b(entertainment|blocked)\b", RegexOptions.IgnoreCase))
            bullets.Add("Entertainment expenses are generally blocked for input VAT recovery; Corporate Tax may require add-back adjustments in ERP P&L CT fields.");

        foreach (var m in matches)
        {
            if (m.PatternKey.Length > 0 && ErpUaeTaxFtaLegislation.Catalog.TryGetValue(m.PatternKey, out var cat))
                bullets.Add(cat.Summary);
            foreach (var sb in ExtractBullets(m.ErpSummary, 2))
                bullets.Add(sb);
            foreach (var act in (m.ComplianceActions ?? []).Take(2))
                bullets.Add(act);
            var tt = m.TaxCategory ?? "";
            if (tt.Length > 0 && FamilyBullets.TryGetValue(tt, out var fam) && fam.Count > 0)
                bullets.Add(fam[0]);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<string>();
        foreach (var b in bullets)
        {
            var t = Regex.Replace((b ?? "").Trim(), @"\s+", " ");
            if (t.Length < 15) continue;
            var key = t.Length > 80 ? t[..80].ToLowerInvariant() : t.ToLowerInvariant();
            if (!seen.Add(key)) continue;
            unique.Add(t);
            if (unique.Count >= 8) break;
        }

        if (unique.Count == 0)
            unique.Add("No strong match in the legislation library — try rephrasing or fetch the latest FTA legislation updates.");
        return unique;
    }

    public static ErpUaeTaxLegislationAskAnswer Answer(string question, IReadOnlyList<ErpUaeTaxFtaItem> items)
    {
        question = (question ?? "").Trim();
        if (question.Length == 0)
        {
            return new(false, ["Please enter a tax question about UAE VAT, Corporate Tax, excise, or FTA procedures."],
                [], 0, Disclaimer, "Empty question", 0, []);
        }

        if (items.Count == 0)
        {
            return new(false, ["Legislation library is empty — fetch updates from tax.gov.ae/legislation.aspx first."],
                [], 0, Disclaimer, "No legislation items", 0, []);
        }

        var tokens = Tokenize(question);
        var boosts = IntentBoosts(question);
        var top = Rank(items, tokens, boosts);
        var citations = top.Select(tm =>
        {
            var key = tm.Item.Slug.Length > 0 ? tm.Item.Slug : tm.Item.ItemKey;
            var excerpt = (tm.Item.ErpSummary ?? "").Trim();
            if (excerpt.Length > 280) excerpt = excerpt[..280];
            return new ErpUaeTaxLegislationCitation(tm.Item.Title, tm.Item.IssueDate, tm.Item.PdfUrl, key, tm.Score, excerpt);
        }).ToList();
        var confidence = top.Count == 0 ? 0f : (float)Math.Round(top[0].Score, 2);
        var answer = Synthesize(question, top.Select(t => t.Item).ToList(), boosts);
        return new(true, answer, citations, confidence, Disclaimer, "Answer from legislation library", top.Count, tokens);
    }
}

public sealed record ErpUaeTaxLegislationCitation(
    string Title,
    string IssueDate,
    string PdfUrl,
    string LegislationKey,
    float Score,
    string SummaryExcerpt);

public sealed record ErpUaeTaxLegislationAskAnswer(
    bool Ok,
    IReadOnlyList<string> Answer,
    IReadOnlyList<ErpUaeTaxLegislationCitation> Citations,
    float Confidence,
    string Disclaimer,
    string Message,
    int MatchCount,
    IReadOnlyList<string> Tokens);
