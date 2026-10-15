using System.Globalization;
using System.Net;
using System.Net.Mail;

namespace EcomAE.Platform.Erp;

public enum ErpTenancyExpiryWindow
{
    Waiting,
    Due,
    Ended,
}

public sealed record ErpTenancyExpiryTemplate(string Subject, string Body, string ReplyEmails, bool AutoSend);

public sealed record ErpTenancyLeaseFact(
    long LeaseId,
    string LeaseCode,
    string Status,
    DateOnly Start,
    DateOnly End,
    decimal AnnualRent,
    string TenantName,
    string TenantEmail,
    string NoticeEmails,
    string PropertyName,
    string UnitCode,
    long TenantId = 0);

public sealed record ErpTenancyExpiryDraft(
    long LeaseId,
    DateOnly DueOn,
    DateOnly EndDate,
    string ToEmails,
    string ReplyEmails,
    string Subject,
    string Body,
    string Status);

public sealed record ErpTenancyExpiryReply(long Id, long NoticeId, string FromEmail, string Body, string FiledAt);

public sealed record ErpTenancyExpiryNotice(
    long Id,
    long LeaseId,
    DateOnly DueOn,
    DateOnly EndDate,
    string ToEmails,
    string ReplyEmails,
    string Subject,
    string Body,
    string Status,
    string SentAt,
    string SendError,
    string LeaseCode,
    string TenantName,
    string PropertyName,
    string UnitCode,
    bool Customized,
    IReadOnlyList<ErpTenancyExpiryReply> Replies,
    long TenantId = 0);

public sealed record ErpTenancyExpiryWatch(
    long LeaseId,
    string LeaseCode,
    string TenantName,
    string PropertyName,
    string UnitCode,
    DateOnly EndDate,
    DateOnly DueOn,
    ErpTenancyExpiryWindow Window,
    long TenantId = 0);

public sealed record ErpTenancyExpiryBook(
    ErpTenancyExpiryTemplate Template,
    IReadOnlyList<ErpTenancyExpiryNotice> Notices,
    IReadOnlyList<ErpTenancyExpiryWatch> Waiting,
    IReadOnlyList<ErpTenancyExpiryWatch> Ended,
    string Message,
    IReadOnlyList<ErpTenancyExpiryDocument>? Documents = null,
    IReadOnlyList<ErpTenancyExpiryFileLink>? Links = null)
{
    public static ErpTenancyExpiryBook Empty(string message)
        => new(ErpTenancyExpiryMail.DefaultTemplate, [], [], [], message);
}

/// <summary>
/// A tenancy notice is due on the day three months before the lease end.
/// It stays due through the end date. Earlier dates wait. Later dates are ended.
/// Only an active lease is prepared. Occupancies that are not on file are never invented.
/// </summary>
public static class ErpTenancyExpiryMail
{
    public const int MonthsBefore = 3;

    public const int MaxAddresses = 12;

    public const string DefaultSubject = "Tenancy ending on {{end_date}} - {{property}} {{unit}}";

    public const string DefaultBody =
        """
        Dear {{tenant_name}},

        Your tenancy {{lease_code}} at {{property}} {{unit}} ends on {{end_date}}.

        This notice is sent three months before that date, on {{notice_date}}, so you can confirm whether you will renew or vacate.

        Please reply to this email. The reply is delivered to: {{reply_emails}}

        Start date: {{start_date}}
        Annual rent on file: {{annual_rent}} AED

        If any detail is wrong, reply with the correction.

        Thank you.
        """;

    public static ErpTenancyExpiryTemplate DefaultTemplate { get; } = new(DefaultSubject, DefaultBody, "", false);

    public static DateOnly NoticeDueOn(DateOnly endDate)
        => endDate.AddMonths(-MonthsBefore);

    public static ErpTenancyExpiryWindow Window(DateOnly endDate, DateOnly today)
    {
        if (today > endDate)
        {
            return ErpTenancyExpiryWindow.Ended;
        }

        return today >= NoticeDueOn(endDate)
            ? ErpTenancyExpiryWindow.Due
            : ErpTenancyExpiryWindow.Waiting;
    }

    /// <summary>Gulf Standard Time is four hours ahead of UTC and does not use daylight saving.</summary>
    public static DateOnly GulfToday(DateTime utcNow)
        => DateOnly.FromDateTime(utcNow.AddHours(4));

    public static string GulfStamp(DateTime utcNow)
        => utcNow.AddHours(4).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static bool IsActive(string? status)
        => string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);

    public static bool AcceptsAutomaticNotice(string? status, DateOnly endDate, DateOnly today)
        => IsActive(status) && Window(endDate, today) == ErpTenancyExpiryWindow.Due;

    public static bool CanPrepareManually(string? status, DateOnly endDate, DateOnly today)
        => IsActive(status) && Window(endDate, today) != ErpTenancyExpiryWindow.Waiting;

    public static bool CanEdit(string? status)
        => status is "ready" or "needs_email" or "failed";

    public static bool CanSend(string? status)
        => status is "ready" or "failed";

    public static bool CanSkip(string? status)
        => status is "ready" or "needs_email" or "failed";

    public static string StatusLabel(string? status)
        => status switch
        {
            "ready" => "Ready to send",
            "needs_email" => "Needs a tenant email",
            "sent" => "Sent",
            "failed" => "Not sent",
            "skipped" => "Skipped",
            "replied" => "Reply filed",
            _ => status ?? "",
        };

    public static IReadOnlyList<string> Addresses(string? raw, int max = MaxAddresses)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw) || max <= 0)
        {
            return list;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (list.Count >= max)
            {
                break;
            }

            if (!MailAddress.TryCreate(part, out var mail) || mail.Address.Length is 0 or > 120)
            {
                continue;
            }

            if (seen.Add(mail.Address))
            {
                list.Add(mail.Address);
            }
        }

        return list;
    }

    public static string Join(IReadOnlyList<string> emails)
        => string.Join(", ", emails);

    public static string CleanSubject(string? value)
    {
        var text = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (text.Contains("  ", StringComparison.Ordinal))
        {
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        }

        return text.Length <= 200 ? text : text[..200];
    }

    public static string Fill(string? template, IReadOnlyDictionary<string, string> tokens)
    {
        var text = template ?? "";
        foreach (var pair in tokens)
        {
            text = text.Replace("{{" + pair.Key + "}}", pair.Value ?? "", StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    public static string ToHtml(string? plain)
    {
        var encoded = WebUtility.HtmlEncode(plain ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br>\n", StringComparison.Ordinal);
        return "<div style=\"font-family:sans-serif;font-size:14px;line-height:1.5\">" + encoded + "</div>";
    }

    public static ErpTenancyExpiryDraft Draft(ErpTenancyLeaseFact lease, ErpTenancyExpiryTemplate template)
    {
        var due = NoticeDueOn(lease.End);
        var to = Addresses((lease.TenantEmail ?? "") + "\n" + (lease.NoticeEmails ?? ""));
        var reply = Addresses(template.ReplyEmails);
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tenant_name"] = string.IsNullOrWhiteSpace(lease.TenantName) ? "Tenant" : lease.TenantName.Trim(),
            ["property"] = (lease.PropertyName ?? "").Trim(),
            ["unit"] = (lease.UnitCode ?? "").Trim(),
            ["lease_code"] = (lease.LeaseCode ?? "").Trim(),
            ["end_date"] = lease.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["start_date"] = lease.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["notice_date"] = due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["annual_rent"] = lease.AnnualRent.ToString("0.00", CultureInfo.InvariantCulture),
            ["reply_emails"] = reply.Count == 0 ? "the mailbox that sent this notice" : Join(reply),
        };
        var subject = CleanSubject(Fill(string.IsNullOrWhiteSpace(template.Subject) ? DefaultSubject : template.Subject, tokens));
        if (subject.Length == 0)
        {
            subject = CleanSubject(Fill(DefaultSubject, tokens));
        }

        var body = Fill(string.IsNullOrWhiteSpace(template.Body) ? DefaultBody : template.Body, tokens).Trim();
        if (body.Length == 0)
        {
            body = Fill(DefaultBody, tokens).Trim();
        }

        if (body.Length > 8000)
        {
            body = body[..8000];
        }

        return new ErpTenancyExpiryDraft(
            lease.LeaseId,
            due,
            lease.End,
            Join(to),
            Join(reply),
            subject,
            body,
            to.Count == 0 ? "needs_email" : "ready");
    }

    public static string SendFailure(string? code, string? message)
    {
        if (code is "disabled" or "missing")
        {
            return "The company mail server is not turned on, so this notice was not sent.";
        }

        var text = (message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 240 ? text : text[..240];
    }
}
