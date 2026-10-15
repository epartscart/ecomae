using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Turns a spoken or typed ERP command into a plan: which page, which fields,
/// and what is still missing. It does not post a voucher. The clerk confirms on the form.
/// </summary>
public static class ErpVoiceInterpreter
{
    public sealed record Draft(
        string Intent,
        string Document,
        string Say,
        string Href,
        bool Ready,
        IReadOnlyList<string> Missing,
        IReadOnlyDictionary<string, string> Slots);

    public static Draft Interpret(string? transcript, DateOnly today)
    {
        var text = Normalize(transcript);
        if (text.Length == 0)
        {
            return new("help", "", HelpSay(), "", false, [], new Dictionary<string, string>());
        }

        if (IsHelp(text))
        {
            return new("help", "", HelpSay(), "", false, [], new Dictionary<string, string>());
        }

        var tool = ToolRequest(text);
        if (tool is not null)
        {
            return tool;
        }

        if (IsRequirements(text))
        {
            var doc = DetectDocument(text);
            if (doc.Length == 0)
            {
                return new(
                    "requirements",
                    "",
                    "Say which document. For example: what do I need for a receipt voucher, a payment voucher, a sales order, or a sales report.",
                    "",
                    false,
                    [],
                    new Dictionary<string, string>());
            }

            return Requirements(doc);
        }

        if (IsSalesReport(text))
        {
            return SalesReport(text, today);
        }

        if (IsReceipt(text))
        {
            return Voucher("receipt", text, today);
        }

        if (IsPayment(text))
        {
            return Voucher("payment", text, today);
        }

        var created = DetectDocument(text);
        if (created.Length > 0 && Rx(text, @"\b(create|new|make|prepare|raise|book|enter)\b"))
        {
            return Requirements(created) with { Intent = "guide" };
        }

        return Open(text);
    }

    public static string Normalize(string? transcript)
    {
        var raw = (transcript ?? string.Empty).ToLowerInvariant();
        raw = raw.Replace('’', '\'').Replace('‘', '\'');
        raw = Regex.Replace(raw, @"[^\p{L}\p{N}\s,./'-]", " ");
        raw = Regex.Replace(raw, @"\s+", " ").Trim();
        return raw;
    }

    public static bool CountryMatches(string? stored, string? requested)
    {
        var want = CountryCode(requested);
        if (want.Length == 0)
        {
            return true;
        }

        var have = CountryCode(stored);
        if (have.Length == 0)
        {
            var plain = (stored ?? string.Empty).Trim();
            return plain.Length > 0 && plain.Contains(requested?.Trim() ?? "\u0000", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(have, want, StringComparison.OrdinalIgnoreCase);
    }

    public static string CountryCode(string? text)
    {
        var value = Normalize(text);
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (value.Length == 2 && value.All(char.IsLetter))
        {
            return value.ToUpperInvariant();
        }

        foreach (var pair in Countries.OrderByDescending(pair => pair.Key.Length))
        {
            if (value.Contains(pair.Key, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        foreach (var code in ShortCodes)
        {
            if (Regex.IsMatch(value, @"\b" + code + @"\b", RegexOptions.CultureInvariant))
            {
                return code.ToUpperInvariant();
            }
        }

        return string.Empty;
    }

    public static string CountryLabel(string code)
    {
        foreach (var pair in Countries)
        {
            if (string.Equals(pair.Value, code, StringComparison.OrdinalIgnoreCase) && pair.Key.Contains(' '))
            {
                return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(pair.Key);
            }
        }

        return code;
    }

    public static IReadOnlyList<string> CountryAliases(string? text)
    {
        var code = CountryCode(text);
        if (code.Length == 0)
        {
            var plain = Normalize(text);
            return plain.Length == 0 ? [] : [plain];
        }

        var names = Countries
            .Where(pair => string.Equals(pair.Value, code, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .Append(code.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return names;
    }

    private static Draft Voucher(string document, string text, DateOnly today)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal);
        var amount = FindAmount(text);
        if (amount is > 0)
        {
            slots["amount"] = amount.Value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        var party = document == "receipt" ? "customer" : "supplier";
        var partyName = FindParty(text, party);
        if (partyName.Length > 0)
        {
            slots[party] = partyName;
        }

        var account = FindAccount(text);
        if (account.Length > 0)
        {
            slots["account"] = account;
        }

        var invoice = FindId(text, "invoice");
        if (invoice > 0)
        {
            slots["invoice"] = invoice.ToString(CultureInfo.InvariantCulture);
        }

        if (document == "payment")
        {
            var bill = FindId(text, "bill");
            if (bill > 0)
            {
                slots["bill"] = bill.ToString(CultureInfo.InvariantCulture);
            }
        }

        var orderWord = document == "receipt" ? "order" : "purchase";
        var order = FindId(text, orderWord);
        if (order > 0)
        {
            slots[orderWord] = order.ToString(CultureInfo.InvariantCulture);
        }

        if (Rx(text, @"\badvance\b"))
        {
            slots["advance"] = "1";
        }

        if (Rx(text, @"\b(allocate|allocation|settle|settlement)\b"))
        {
            slots["allocate"] = "1";
        }

        var date = FindDate(text, today);
        if (date is not null)
        {
            slots["date"] = date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var note = FindNote(text);
        if (note.Length > 0)
        {
            slots["note"] = note;
        }

        var missing = new List<string>();
        if (!slots.ContainsKey(party))
        {
            missing.Add(party);
        }

        if (!slots.ContainsKey("account"))
        {
            missing.Add("account");
        }

        if (!slots.ContainsKey("amount"))
        {
            missing.Add("amount");
        }

        var title = document == "receipt" ? "Receipt voucher" : "Payment voucher";
        var say = new StringBuilder();
        say.Append(title).Append(" needs a ").Append(party).Append(", a bank or cash account, and an amount. ");
        if (missing.Count == 0)
        {
            say.Append("I heard ");
            say.Append(party).Append(' ').Append(slots[party]);
            say.Append(", account ").Append(slots["account"]);
            say.Append(", amount ").Append(slots["amount"]);
            say.Append(". I will look those names up and open the form. Nothing is posted until you press Validate and post.");
        }
        else
        {
            say.Append("Still needed: ").Append(string.Join(", ", missing)).Append('.');
            if (slots.Count > 0)
            {
                say.Append(" I already have ");
                say.Append(string.Join(", ", slots.Select(pair => pair.Key + " " + pair.Value)));
                say.Append('.');
            }
        }

        return new(document, document, say.ToString(), "", missing.Count == 0, missing, slots);
    }

    private static Draft SalesReport(string text, DateOnly today)
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal);
        var code = CountryCode(text);
        if (code.Length > 0)
        {
            slots["country"] = code;
        }

        var period = FindPeriod(text, today);
        slots["from"] = period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        slots["to"] = period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        slots["period"] = period.Label;

        var detail = "all";
        if (Rx(text, @"\bby customer\b") || Rx(text, @"\bper customer\b"))
        {
            detail = "customer";
        }
        else if (Rx(text, @"\bby (item|sku|product)\b"))
        {
            detail = "item";
        }
        else if (Rx(text, @"\bby month\b") || Rx(text, @"\bper month\b"))
        {
            detail = "month";
        }
        else if (Rx(text, @"\b(line by line|by line)\b"))
        {
            detail = "lines";
        }
        else if (Rx(text, @"\b(summary|totals only)\b"))
        {
            detail = "summary";
        }
        else if (Rx(text, @"\b(line|lines|detailed)\b"))
        {
            detail = "lines";
        }
        else if (Rx(text, @"\b(item|sku|product)\b"))
        {
            detail = "item";
        }
        else if (Rx(text, @"\bcustomer\b"))
        {
            detail = "customer";
        }

        slots["detail"] = detail;
        var href = "/erp/sales-workspace-app?tab=sm_performance&from="
            + slots["from"] + "&to=" + slots["to"] + "&detail=" + detail
            + (code.Length > 0 ? "&country=" + code : "");
        var where = code.Length > 0 ? CountryLabel(code) : "all countries";
        var say = "Sales report for " + where + ", " + period.Label
            + ", detail " + DetailLabel(detail)
            + ". Posted sales only."
            + (code.Length > 0
                ? " Opening the report."
                : " Say the country as well, for example United Arab Emirates.");
        var missing = code.Length == 0 ? new[] { "country" } : Array.Empty<string>();
        return new("report", "sales-report", say, href, code.Length > 0, missing, slots);
    }

    private static Draft Requirements(string document)
    {
        var say = RequirementText.TryGetValue(document, out var text) && !string.IsNullOrEmpty(text)
            ? text
            : "Say the document name again, such as receipt voucher or sales report.";
        var href = RequirementHref.TryGetValue(document, out var path) && !string.IsNullOrEmpty(path) ? path : "";
        if (href.Length > 0)
        {
            say += " Opening that page.";
        }

        return new("requirements", document, say, href, false, [], new Dictionary<string, string>());
    }

    private static Draft Open(string text)
    {
        var query = Regex.Replace(
            text,
            @"\b(please|can|you|i|need|want|show|me|open|go|to|the|a|an|page|module|take|find|bring|up|display|let|see|my)\b",
            " ");
        query = Regex.Replace(query, @"\s+", " ").Trim();
        if (query.Length == 0)
        {
            query = text;
        }

        var hit = ErpGlobalSearchCatalog.Filter(ErpGlobalSearchCatalog.Build(true, true, true), query, 12)
            .OrderByDescending(row => string.Equals(row.Label, query, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(row => row.Label.Length)
            .ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (hit is null)
        {
            return new(
                "unknown",
                "",
                "I could not match that to an ERP page. Try: open sales orders, open cash entries, or create a receipt voucher for customer and amount.",
                "",
                false,
                [],
                new Dictionary<string, string> { ["query"] = query });
        }

        var href = PhpSurfaceLinkMap.AspNetPrimaryHref(hit.Href);
        return new(
            "open",
            "",
            "Opening " + hit.Label + ".",
            href,
            true,
            [],
            new Dictionary<string, string> { ["label"] = hit.Label });
    }

    private static Draft? ToolRequest(string text)
    {
        var lead = Regex.Match(
            text,
            @"^(?:please\s+)?(?:ask|tell|use|send|hand)\s+(?:to\s+)?(chat\s*gpt|chatgpt|openai|cursor|devin)(?:\.ai)?(?:\s+to)?\s*(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string rawProvider;
        string task;
        if (lead.Success)
        {
            rawProvider = lead.Groups[1].Value;
            task = lead.Groups[2].Value.Trim();
        }
        else
        {
            var verb = Regex.Match(
                text,
                @"^(?:please\s+)?(chat\s*gpt|chatgpt|openai|cursor|devin)(?:\.ai)?\s+(?:please\s+)?(?:do|handle|take|work on|complete)\s+(.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!verb.Success)
            {
                return null;
            }

            rawProvider = verb.Groups[1].Value;
            task = verb.Groups[2].Value.Trim();
        }

        if (!ErpOperatorAiNames.TryNormalize(rawProvider, out var provider))
        {
            return null;
        }

        var slots = new Dictionary<string, string>(StringComparer.Ordinal) { ["provider"] = provider };
        var label = ErpOperatorAiNames.Label(provider);
        if (task.Length == 0)
        {
            return new("tool", provider, "Say what " + label + " should do.", "", false, ["task"], slots);
        }

        slots["task"] = Regex.Replace(task, @"\s+", " ").Trim();
        return new("tool", provider, "Sending that to " + label + ".", "", false, [], slots);
    }

    private static string HelpSay()
        => "You can say: create a receipt voucher for customer Ahmed for 1500 from the main cash account. "
           + "Or: payment voucher for supplier Gulf Parts amount 800 account operating bank. "
           + "Or: United Arab Emirates sales report for last month by customer. "
           + "Or: what do I need for a receipt voucher. "
           + "Or: open purchase orders. "
           + "Or: ask ChatGPT, ask Cursor, or ask Devin to do a piece of work. "
           + "I prepare the form. You press the button to post.";

    private static bool IsHelp(string text)
        => Rx(text, @"\b(help|what can i say|voice commands|how do i speak)\b");

    private static bool IsRequirements(string text)
        => Rx(text, @"\b(what do i need|what are the requirements|what is required|what's required|requirements for|which fields|required fields)\b");

    private static bool IsSalesReport(string text)
        => Rx(text, @"\b(sales report|country sales|sales by country|profitability)\b")
           || (Rx(text, @"\bsales\b") && Rx(text, @"\b(report|country|period)\b"));

    private static bool IsReceipt(string text)
        => Rx(text, @"\b(receipt voucher|customer receipt|receipt journal|receive money|money received)\b")
           || (Rx(text, @"\breceipt\b") && Rx(text, @"\b(customer|voucher|amount|account)\b"));

    private static bool IsPayment(string text)
        => Rx(text, @"\b(payment voucher|supplier payment|payment journal|pay the supplier|pay supplier)\b")
           || (Rx(text, @"\bpayment\b") && Rx(text, @"\b(supplier|voucher|amount|account)\b"));

    private static string DetectDocument(string text)
    {
        foreach (var pair in DocumentPhrases)
        {
            if (text.Contains(pair.Key, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        return string.Empty;
    }

    private static decimal? FindAmount(string text)
    {
        var cleaned = Regex.Replace(text, @"\b(?:invoice|order|purchase|bill|so|po|id|no|number)\s*#?\s*\d+\b", " ");
        var matches = Regex.Matches(cleaned, @"\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?");
        if (matches.Count > 0)
        {
            var token = matches[matches.Count - 1].Value.Replace(",", string.Empty, StringComparison.Ordinal);
            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0)
            {
                return amount;
            }
        }

        return FindSpokenAmount(cleaned);
    }

    private static decimal? FindSpokenAmount(string text)
    {
        if (!Rx(text, @"\b(hundred|thousand|million)\b") && !Rx(text, @"\b(amount|aed|dirham|dirhams|dhs)\b"))
        {
            return null;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        long current = 0;
        long total = 0;
        var seen = false;
        foreach (var word in words)
        {
            if (!NumberWords.TryGetValue(word, out var value))
            {
                if (seen && (word is "aed" or "dirham" or "dirhams" or "dhs" or "from" or "account" or "into" or "on" or "for" or "customer" or "supplier"))
                {
                    break;
                }

                if (seen)
                {
                    current = 0;
                    total = 0;
                    seen = false;
                }

                continue;
            }

            seen = true;
            if (value == 100)
            {
                current = Math.Max(current, 1) * 100;
            }
            else if (value is 1000 or 1000000)
            {
                total += Math.Max(current, 1) * value;
                current = 0;
            }
            else
            {
                current += value;
            }
        }

        var sum = total + current;
        return seen && sum > 0 ? sum : null;
    }

    private static string FindParty(string text, string role)
    {
        var match = Regex.Match(
            text,
            @"\b" + role + @"\s+(.+?)(?=\s+\b(?:for|amount|account|bank|cash|from|into|note|dated|on|invoice|order|advance|allocate|aed|dirham|dirhams|dhs)\b|$)");
        if (!match.Success)
        {
            return string.Empty;
        }

        return CleanName(match.Groups[1].Value);
    }

    private static string FindAccount(string text)
    {
        var patterns = new[]
        {
            @"\b(?:bank|cash)\s+account\s+(.+?)(?=\s+\b(?:for|amount|note|dated|invoice|order|bill|purchase|advance|allocate|customer|supplier)\b|$)",
            @"\baccount\s+(.+?)(?=\s+\b(?:for|amount|note|dated|invoice|order|bill|purchase|advance|allocate|customer|supplier)\b|$)",
            @"\bfrom\s+(?:the\s+)?(.+?)\s+account\b",
            @"\bbank\s+(.+?)(?=\s+\b(?:for|amount|note|account|customer|supplier)\b|$)",
        };
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                var name = CleanName(match.Groups[1].Value);
                if (name.Length > 0 && name is not "the" and not "a")
                {
                    return name;
                }
            }
        }

        return string.Empty;
    }

    private static string CleanName(string value)
    {
        var name = Regex.Replace(value, @"\b(the|a|an|my|our)\b", " ");
        name = Regex.Replace(name, @"\s+", " ").Trim(' ', ',', '.', '-');
        return name.Length == 0 ? name : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }

    private static long FindId(string text, string label)
    {
        var match = Regex.Match(text, @"\b" + label + @"\s*#?\s*(\d+)\b");
        return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : 0;
    }

    private static string FindNote(string text)
    {
        var match = Regex.Match(text, @"\bnote\s+(.+)$");
        return match.Success ? CleanName(match.Groups[1].Value) : string.Empty;
    }

    private static DateOnly? FindDate(string text, DateOnly today)
    {
        if (Rx(text, @"\btoday\b"))
        {
            return today;
        }

        if (Rx(text, @"\byesterday\b"))
        {
            return today.AddDays(-1);
        }

        var iso = Regex.Match(text, @"\b(\d{4})-(\d{2})-(\d{2})\b");
        if (iso.Success
            && DateOnly.TryParse(iso.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        var named = Regex.Match(text, @"\b(\d{1,2})\s+(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|sept|oct|nov|dec)(?:\s+(\d{4}))?\b");
        if (named.Success && Months.TryGetValue(named.Groups[2].Value, out var month))
        {
            var day = int.Parse(named.Groups[1].Value, CultureInfo.InvariantCulture);
            var year = named.Groups[3].Success ? int.Parse(named.Groups[3].Value, CultureInfo.InvariantCulture) : today.Year;
            if (day is >= 1 and <= 31)
            {
                return new DateOnly(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
            }
        }

        return null;
    }

    public static (DateOnly From, DateOnly To, string Label) FindPeriod(string text, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        if (Rx(text, @"\blast month\b"))
        {
            var start = monthStart.AddMonths(-1);
            return (start, start.AddMonths(1).AddDays(-1), start.ToString("MMMM yyyy", CultureInfo.InvariantCulture));
        }

        if (Rx(text, @"\bthis month\b"))
        {
            return (monthStart, monthStart.AddMonths(1).AddDays(-1), monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture));
        }

        if (Rx(text, @"\blast year\b"))
        {
            return (new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31), (today.Year - 1).ToString(CultureInfo.InvariantCulture));
        }

        if (Rx(text, @"\bthis year\b") || Rx(text, @"\byear to date\b"))
        {
            return (new DateOnly(today.Year, 1, 1), today, today.Year.ToString(CultureInfo.InvariantCulture) + " to date");
        }

        var quarter = Regex.Match(text, @"\bq([1-4])(?:\s+(\d{4}))?\b|\bquarter\s+([1-4])(?:\s+(\d{4}))?\b");
        if (quarter.Success)
        {
            var q = int.Parse(quarter.Groups[1].Success ? quarter.Groups[1].Value : quarter.Groups[3].Value, CultureInfo.InvariantCulture);
            var yearText = quarter.Groups[2].Success ? quarter.Groups[2].Value : quarter.Groups[4].Value;
            var year = yearText.Length > 0 ? int.Parse(yearText, CultureInfo.InvariantCulture) : today.Year;
            var startMonth = (q - 1) * 3 + 1;
            var start = new DateOnly(year, startMonth, 1);
            return (start, start.AddMonths(3).AddDays(-1), "Q" + q.ToString(CultureInfo.InvariantCulture) + " " + year.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var pair in Months.OrderByDescending(pair => pair.Key.Length))
        {
            var match = Regex.Match(text, @"\b" + pair.Key + @"(?:\s+(\d{4}))?\b");
            if (!match.Success)
            {
                continue;
            }

            var year = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : today.Year;
            var start = new DateOnly(year, pair.Value, 1);
            return (start, start.AddMonths(1).AddDays(-1), start.ToString("MMMM yyyy", CultureInfo.InvariantCulture));
        }

        return (monthStart, today, "this month so far");
    }

    private static string DetailLabel(string detail) => detail switch
    {
        "lines" => "line by line",
        "item" => "by item",
        "month" => "by month",
        "customer" => "by customer",
        "summary" => "summary totals",
        _ => "summary, customer, item, and lines",
    };

    private static bool Rx(string text, string pattern)
        => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, int> Months = new(StringComparer.Ordinal)
    {
        ["january"] = 1, ["jan"] = 1, ["february"] = 2, ["feb"] = 2, ["march"] = 3, ["mar"] = 3,
        ["april"] = 4, ["apr"] = 4, ["may"] = 5, ["june"] = 6, ["jun"] = 6, ["july"] = 7, ["jul"] = 7,
        ["august"] = 8, ["aug"] = 8, ["september"] = 9, ["sep"] = 9, ["sept"] = 9, ["october"] = 10, ["oct"] = 10,
        ["november"] = 11, ["nov"] = 11, ["december"] = 12, ["dec"] = 12,
    };

    private static readonly Dictionary<string, long> NumberWords = new(StringComparer.Ordinal)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16,
        ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50,
        ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90, ["hundred"] = 100, ["thousand"] = 1000, ["million"] = 1000000,
    };

    private static readonly (string Key, string Value)[] DocumentPhrases =
    [
        ("receipt voucher", "receipt"),
        ("customer receipt", "receipt"),
        ("receipt journal", "receipt"),
        ("payment voucher", "payment"),
        ("supplier payment", "payment"),
        ("payment journal", "payment"),
        ("sales report", "sales-report"),
        ("sales order", "sales-order"),
        ("sales quotation", "quotation"),
        ("sales invoice", "invoice"),
        ("purchase order", "purchase-order"),
        ("purchase invoice", "purchase"),
        ("supplier bill", "purchase"),
        ("journal entry", "journal"),
        ("general journal", "journal"),
        ("inventory item", "item"),
        ("stock movement", "movement"),
        ("chart of accounts", "coa"),
    ];

    private static readonly Dictionary<string, string> RequirementText = new(StringComparer.Ordinal)
    {
        ["receipt"] = "A receipt voucher needs the customer, the bank or cash account the money lands in, and the amount. Optional: sales invoice, sales order, advance receipt, allocate across open invoices, narration, and date.",
        ["payment"] = "A payment voucher needs the supplier, the bank or cash account the money leaves, and the amount. Optional: purchase bill, purchase order, advance payment, allocate across open bills, reference, narration, and date.",
        ["sales-report"] = "A country sales report needs the country, the period, and the detail. Detail can be summary, by customer, by item, by month, or line by line. Example: United Arab Emirates sales report for last month by customer.",
        ["sales-order"] = "A sales order needs the customer and at least one line with item, quantity, and price.",
        ["quotation"] = "A sales quotation needs the customer and at least one line with item, quantity, and price.",
        ["invoice"] = "A sales invoice needs the customer and the lines or amount to bill.",
        ["purchase-order"] = "A purchase order needs the supplier and at least one line with item, quantity, and cost.",
        ["purchase"] = "A supplier bill needs the supplier and the amount or lines.",
        ["journal"] = "A journal needs a date and lines that balance: debits equal credits.",
        ["item"] = "An inventory item needs a name or SKU. An assembly also needs its component lines.",
        ["movement"] = "A stock movement needs the item, the warehouse, the quantity, and whether stock is coming in or going out.",
        ["coa"] = "A ledger account needs a code, a name, and an account type.",
    };

    private static readonly Dictionary<string, string> RequirementHref = new(StringComparer.Ordinal)
    {
        ["receipt"] = "/erp/cash-entries-app#receipt-voucher",
        ["payment"] = "/erp/cash-entries-app#payment-voucher",
        ["sales-report"] = "/erp/sales-workspace-app?tab=sm_performance",
        ["sales-order"] = "/erp/sales-orders-app",
        ["quotation"] = "/erp/sales-quotations-app",
        ["invoice"] = "/erp/invoices-app",
        ["purchase-order"] = "/erp/purchase-orders-app",
        ["purchase"] = "/erp/purchases-app",
        ["journal"] = "/erp/gl-journals-app",
        ["item"] = "/erp/product-info-app",
        ["movement"] = "/erp/inventory-stock-app",
        ["coa"] = "/erp/coa-accounts-app",
    };

    private static readonly Dictionary<string, string> Countries = new(StringComparer.Ordinal)
    {
        ["united arab emirates"] = "AE",
        ["u a e"] = "AE",
        ["uae"] = "AE",
        ["emirates"] = "AE",
        ["saudi arabia"] = "SA",
        ["ksa"] = "SA",
        ["united kingdom"] = "GB",
        ["britain"] = "GB",
        ["uk"] = "GB",
        ["united states of america"] = "US",
        ["united states"] = "US",
        ["usa"] = "US",
        ["america"] = "US",
        ["india"] = "IN",
        ["qatar"] = "QA",
        ["kuwait"] = "KW",
        ["oman"] = "OM",
        ["bahrain"] = "BH",
        ["egypt"] = "EG",
        ["germany"] = "DE",
        ["france"] = "FR",
        ["china"] = "CN",
        ["pakistan"] = "PK",
        ["jordan"] = "JO",
        ["lebanon"] = "LB",
        ["singapore"] = "SG",
        ["canada"] = "CA",
        ["australia"] = "AU",
    };

    /// <summary>Two-letter codes matched as whole words. India and the United States stay on the names india and united states, because in and us are ordinary English words.</summary>
    private static readonly string[] ShortCodes =
    [
        "ae", "sa", "gb", "qa", "kw", "om", "bh", "eg", "de", "fr", "cn", "pk", "jo", "lb", "sg", "ca", "au",
    ];
}
