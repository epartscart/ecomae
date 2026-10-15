using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Compact map of the ERP that ChatGPT, Cursor, and Devin receive with each request.
/// Page links stay inside this process. The tools do not invent an address we then open.
/// </summary>
public static class ErpOperatorAiBrief
{
    public static string Knowledge { get; } = BuildKnowledge();

    public static string AgentPrompt(string task)
        => "You understand this ERP and you work for the signed-in operator. "
           + "Guide them through the task, help them finish it, build the report they named, or identify the issue. "
           + "Do not post vouchers, payments, or journals. The operator confirms those in the ERP. "
           + Knowledge
           + " Operator request: "
           + task;

    public static string Kind(string? task)
    {
        var text = ErpVoiceInterpreter.Normalize(task);
        if (IsIssue(text))
        {
            return "issue";
        }

        if (!Rx(text, @"\bhow do i\b") && (CountrySales(text) || TryReport(text, out _, out _)))
        {
            return "report";
        }

        if (Rx(text, @"\b(how do i|how to|guide me|walk me through|help me|what do i need|explain|show me how)\b"))
        {
            return "guide";
        }

        return "work";
    }

    public static bool IsIssue(string? task)
    {
        var text = ErpVoiceInterpreter.Normalize(task);
        return Rx(text, @"\b(check the books|check books|book check|identify issues|identify any issue|find issues|find the issue|what is wrong|issues in the books|unbalanced|out of balance)\b");
    }

    public static bool TryReport(string? task, out string name, out string href)
    {
        name = string.Empty;
        href = string.Empty;
        var text = Aging(ErpVoiceInterpreter.Normalize(task));
        if (text.Length == 0 || CountrySales(text))
        {
            return false;
        }

        if (Rx(text, @"\bopen\b") && !Rx(text, @"\breport\b"))
        {
            return false;
        }

        if (Rx(text, @"\b(p l|pnl|income statement|profit and loss|profit loss)\b"))
        {
            return Hit("Profit and loss", "gl_profit_loss", "pl", out name, out href);
        }

        if (Rx(text, @"\bbalance sheet\b"))
        {
            return Hit("Balance sheet", "gl_balance_sheet", "balance_sheet", out name, out href);
        }

        if (Rx(text, @"\btrial balance\b"))
        {
            return Hit("Trial balance", "gl_trial_balance", "enterprise_reports", out name, out href);
        }

        if (Rx(text, @"\b(ar aging|receivable aging|customer aging|debtor aging)\b"))
        {
            return Hit("AR aging summary", "exec_ar_aging", "reports", out name, out href);
        }

        if (Rx(text, @"\bworking capital\b"))
        {
            return Hit("Working capital analysis", "exec_working_capital", "revenue", out name, out href);
        }

        ErpReportCenterRegistry.Entry? best = null;
        var bestScore = 0;
        foreach (var entry in ErpReportCenterRegistry.All)
        {
            var label = Aging(ErpVoiceInterpreter.Normalize(entry.Name));
            if (label.Length < 4)
            {
                continue;
            }

            var score = 0;
            if (text.Contains(label, StringComparison.Ordinal))
            {
                score = label.Length + 20;
            }
            else
            {
                var hits = 0;
                foreach (var word in label.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word.Length > 3 && text.Contains(word, StringComparison.Ordinal))
                    {
                        hits++;
                    }
                }

                if (hits >= 2)
                {
                    score = hits * 6;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = entry;
            }
        }

        if (best is null || bestScore < 12)
        {
            if (Rx(text, @"\b(build|open|run|show|need|want)\b") && Rx(text, @"\breport\b"))
            {
                name = "Report center";
                href = "/erp/report-center-app";
                return true;
            }

            return false;
        }

        name = best.Name;
        href = Href(best.Key, TabFor(best.Key));
        return href.StartsWith("/erp/", StringComparison.Ordinal);
    }

    public static bool TryGuide(string? task, DateOnly today, out string say, out string href)
    {
        say = string.Empty;
        href = string.Empty;
        var draft = ErpVoiceInterpreter.Interpret(task, today);
        if (draft.Intent is "receipt" or "payment" or "requirements" or "guide" or "report" or "open")
        {
            say = draft.Say;
            href = SafeHref(draft.Href);
            return say.Length > 0;
        }

        var opened = ErpVoiceInterpreter.Interpret("open " + ErpVoiceInterpreter.Normalize(task), today);
        if (opened.Intent == "open" && opened.Say.Length > 0)
        {
            say = opened.Say + " Ask for the fields if you want the form filled. Nothing is posted until you press Validate and post.";
            href = SafeHref(opened.Href);
            return true;
        }

        say = "Name the area, such as purchasing, sales, bank, payroll, tax, or the general ledger. I will open that page and tell you what it needs.";
        return true;
    }

    private static string BuildKnowledge()
    {
        var text = new StringBuilder();
        text.Append("ERP map. Guide the operator in short steps. ");
        text.Append("To open a page add a line ERP: open PAGE. ");
        text.Append("To prepare a form add ERP: then the voucher or the country sales report. ");
        text.Append("To build a report add ERP: report NAME. ");
        text.Append("To find posting issues add ERP: check the books. ");
        text.Append("The ERP opens the page and fills the form. It does not post money. ");
        text.Append("Areas: ");
        var first = true;
        foreach (var area in PhpModuleCatalog.ErpAreas)
        {
            if (!first)
            {
                text.Append("; ");
            }

            first = false;
            text.Append(area.Label);
            if (!string.IsNullOrWhiteSpace(area.Description))
            {
                text.Append(" — ").Append(area.Description);
            }
        }

        text.Append(". Reports: ");
        first = true;
        foreach (var entry in ErpReportCenterRegistry.All)
        {
            if (!first)
            {
                text.Append("; ");
            }

            first = false;
            text.Append(entry.Name);
        }

        text.Append("; Country sales report. ");
        text.Append("A receipt needs the customer, the bank or cash account, and the amount. ");
        text.Append("A payment needs the supplier, the bank or cash account, and the amount. ");
        text.Append("A book check looks for unbalanced journals, cash entries with no journal, and customer, supplier, and cash totals that do not match their control accounts.");
        return text.ToString();
    }

    public static bool IsCountrySales(string? task)
    {
        var text = ErpVoiceInterpreter.Normalize(task);
        return CountrySales(text);
    }

    private static bool CountrySales(string text)
        => Rx(text, @"\b(sales report|country sales|sales by country)\b")
           || (Rx(text, @"\bsales\b") && Rx(text, @"\b(country|period)\b"));

    private static bool Hit(string name, string key, string tab, out string reportName, out string href)
    {
        reportName = name;
        href = Href(key, tab);
        return true;
    }

    private static string Href(string key, string tab)
        => "/erp/report-center-app?tab=" + tab + "&key=" + Uri.EscapeDataString(key);

    private static string TabFor(string key)
        => key switch
        {
            "gl_profit_loss" => "pl",
            "gl_balance_sheet" => "balance_sheet",
            "gl_trial_balance" => "enterprise_reports",
            "exec_working_capital" => "revenue",
            _ => "reports",
        };

    private static string Aging(string text)
        => text.Replace("ageing", "aging", StringComparison.Ordinal);

    private static string SafeHref(string href)
        => href.StartsWith("/erp", StringComparison.Ordinal) ? href : string.Empty;

    private static bool Rx(string text, string pattern)
        => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
