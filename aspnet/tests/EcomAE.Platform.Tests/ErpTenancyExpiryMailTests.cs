using System.Net.Mail;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTenancyExpiryMailTests
{
    [Fact]
    public void NoticeOpensThreeMonthsBeforeTheEndAndClosesTheDayAfter()
    {
        var end = new DateOnly(2026, 5, 31);
        Assert.Equal(new DateOnly(2026, 2, 28), ErpTenancyExpiryMail.NoticeDueOn(end));
        Assert.Equal(ErpTenancyExpiryWindow.Waiting, ErpTenancyExpiryMail.Window(end, new DateOnly(2026, 2, 27)));
        Assert.Equal(ErpTenancyExpiryWindow.Due, ErpTenancyExpiryMail.Window(end, new DateOnly(2026, 2, 28)));
        Assert.Equal(ErpTenancyExpiryWindow.Due, ErpTenancyExpiryMail.Window(end, end));
        Assert.Equal(ErpTenancyExpiryWindow.Ended, ErpTenancyExpiryMail.Window(end, new DateOnly(2026, 6, 1)));
        Assert.Equal(new DateOnly(2024, 2, 29), ErpTenancyExpiryMail.NoticeDueOn(new DateOnly(2024, 5, 31)));
    }

    [Fact]
    public void OnlyAnActiveTenancyInsideTheWindowIsPrepared()
    {
        var end = new DateOnly(2026, 12, 1);
        var dueDay = ErpTenancyExpiryMail.NoticeDueOn(end);
        Assert.True(ErpTenancyExpiryMail.AcceptsAutomaticNotice("active", end, dueDay));
        Assert.False(ErpTenancyExpiryMail.AcceptsAutomaticNotice("active", end, dueDay.AddDays(-1)));
        Assert.False(ErpTenancyExpiryMail.AcceptsAutomaticNotice("active", end, end.AddDays(1)));
        Assert.False(ErpTenancyExpiryMail.AcceptsAutomaticNotice("renewed", end, dueDay));
        Assert.False(ErpTenancyExpiryMail.AcceptsAutomaticNotice("terminated", end, dueDay));
        Assert.False(ErpTenancyExpiryMail.CanPrepareManually("active", end, dueDay.AddDays(-1)));
        Assert.True(ErpTenancyExpiryMail.CanPrepareManually("active", end, end.AddDays(1)));
    }

    [Fact]
    public void DraftUsesEveryTenantAddressAndNamesEveryReplyMailbox()
    {
        var draft = ErpTenancyExpiryMail.Draft(
            new ErpTenancyLeaseFact(
                9,
                "LC-12",
                "active",
                new DateOnly(2025, 12, 1),
                new DateOnly(2026, 12, 1),
                120000m,
                "Al Noor",
                "A@Tenant.test",
                "ops@tenant.test, a@tenant.test",
                "Labour Camp",
                "R-12"),
            new ErpTenancyExpiryTemplate(
                ErpTenancyExpiryMail.DefaultSubject,
                ErpTenancyExpiryMail.DefaultBody,
                "desk@landlord.test\nowner@landlord.test",
                false));

        Assert.Equal(new DateOnly(2026, 9, 1), draft.DueOn);
        Assert.Equal("ready", draft.Status);
        Assert.Equal("A@Tenant.test, ops@tenant.test", draft.ToEmails);
        Assert.Equal("desk@landlord.test, owner@landlord.test", draft.ReplyEmails);
        Assert.Contains("Al Noor", draft.Body, StringComparison.Ordinal);
        Assert.Contains("Labour Camp", draft.Body, StringComparison.Ordinal);
        Assert.Contains("R-12", draft.Subject, StringComparison.Ordinal);
        Assert.Contains("2026-12-01", draft.Body, StringComparison.Ordinal);
        Assert.Contains("2026-09-01", draft.Body, StringComparison.Ordinal);
        Assert.Contains("desk@landlord.test, owner@landlord.test", draft.Body, StringComparison.Ordinal);
        Assert.Contains("120000.00", draft.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingTenantEmailStaysUnsentAndUnknownTokensStayVisible()
    {
        var draft = ErpTenancyExpiryMail.Draft(
            new ErpTenancyLeaseFact(1, "L1", "active", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), 10m, "", "not-an-email", "", "P", ""),
            new ErpTenancyExpiryTemplate("Hello {{tenant_name}} {{missing}}", "Body {{missing}}", "", false));
        Assert.Equal("needs_email", draft.Status);
        Assert.Equal("", draft.ToEmails);
        Assert.Equal("Hello Tenant {{missing}}", draft.Subject);
        Assert.Equal("Body {{missing}}", draft.Body);
    }

    [Fact]
    public void AddressesKeepSeveralMailboxesAndDropTheRest()
    {
        var raw = "Ann <ann@x.test>, bad, ann@x.test; bob@x.test\nown@x.test " + string.Join(", ", Enumerable.Range(0, 20).Select(i => "p" + i.ToString() + "@x.test"));
        var emails = ErpTenancyExpiryMail.Addresses(raw);
        Assert.Equal(12, emails.Count);
        Assert.Equal("ann@x.test", emails[0]);
        Assert.Equal("bob@x.test", emails[1]);
        Assert.DoesNotContain(emails, email => email.Contains(' ', StringComparison.Ordinal));
    }

    [Fact]
    public void OneMessageCarriesEveryRecipientAndEveryReplyAddress()
    {
        using var mail = new MailMessage();
        var error = CpTenantEmailWriteService.BindRecipients(
            mail,
            "tenant@x.test, no, second@x.test",
            "desk@x.test; owner@x.test",
            "desk@x.test, accounts@x.test");
        Assert.Null(error);
        Assert.Equal(2, mail.To.Count);
        Assert.Equal("tenant@x.test", mail.To[0].Address);
        Assert.Equal("second@x.test", mail.To[1].Address);
        Assert.Equal(2, mail.CC.Count);
        Assert.Equal(2, mail.ReplyToList.Count);
        Assert.Equal("accounts@x.test", mail.ReplyToList[1].Address);

        using var empty = new MailMessage();
        Assert.Equal("Valid recipient email required", CpTenantEmailWriteService.BindRecipients(empty, "not-an-email", "desk@x.test", "desk@x.test"));
        Assert.Empty(empty.To);
    }

    [Fact]
    public void LetterIsHtmlButTheWordsStayText()
    {
        var html = ErpTenancyExpiryMail.ToHtml("Hello <script>\nLine");
        Assert.Contains("Hello &lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("<br>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Equal("Subject line", ErpTenancyExpiryMail.CleanSubject("Subject\r\nline"));
        Assert.Equal("The company mail server is not turned on, so this notice was not sent.", ErpTenancyExpiryMail.SendFailure("disabled", "Use tenant SMTP is off."));
    }

    [Fact]
    public void GulfDayChangesAfterTwentyHundredUtc()
    {
        Assert.Equal(new DateOnly(2026, 10, 15), ErpTenancyExpiryMail.GulfToday(new DateTime(2026, 10, 15, 19, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 10, 16), ErpTenancyExpiryMail.GulfToday(new DateTime(2026, 10, 15, 20, 30, 0, DateTimeKind.Utc)));
        Assert.Contains("three months", ErpTenancyExpiryMail.DefaultBody, StringComparison.Ordinal);
    }

    [Fact]
    public void OneFileAtATimeStaysInsideTheTenantFolder()
    {
        Assert.True(ErpTenancyDocumentFiles.AllowedExtension("pdf"));
        Assert.True(ErpTenancyDocumentFiles.AllowedExtension("webp"));
        Assert.False(ErpTenancyDocumentFiles.AllowedExtension("exe"));
        Assert.Equal("passport.pdf", ErpTenancyDocumentFiles.DisplayName("../../passport.pdf"));
        Assert.Equal("Passport", ErpTenancyDocumentFiles.TypeLabel("passport"));
        Assert.Equal(1, ErpTenancyDocumentFiles.Sensitive("emirates_id"));
        Assert.Equal(0, ErpTenancyDocumentFiles.Sensitive("ejari"));
        Assert.Equal("Emirates ID", ErpTenancyDocumentFiles.TitleOrName("  ", "Emirates ID.pdf"));
        var root = Path.Combine(Path.GetTempPath(), "re-docs-" + Guid.NewGuid().ToString("N"));
        var path = ErpTenancyDocumentFiles.Resolve(root, ErpTenancyDocumentFiles.StorageKey(12, "abc.pdf"));
        Assert.NotNull(path);
        Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
        Assert.Null(ErpTenancyDocumentFiles.Resolve(root, "../secret.pdf"));
        Assert.Null(ErpTenancyDocumentFiles.Resolve(root, "/etc/passwd"));
    }

    [Fact]
    public void SeveralDocumentsAttachTogetherOrNone()
    {
        var dir = Path.Combine(Path.GetTempPath(), "re-mail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var one = Path.Combine(dir, "a.pdf");
        var two = Path.Combine(dir, "b.pdf");
        File.WriteAllText(one, "a");
        File.WriteAllText(two, "b");
        try
        {
            using var mail = new MailMessage();
            var error = CpTenantEmailWriteService.BindAttachments(mail, new CpTenantEmailMessage(
                "a@x.test",
                "s",
                "b",
                Attachments: [new CpTenantEmailAttachment(one, "Passport.pdf"), new CpTenantEmailAttachment(two, "Visa.pdf")]));
            Assert.Null(error);
            Assert.Equal(2, mail.Attachments.Count);
            Assert.Equal("Passport.pdf", mail.Attachments[0].Name);
            Assert.Equal("Visa.pdf", mail.Attachments[1].Name);

            using var missing = new MailMessage();
            var blocked = CpTenantEmailWriteService.BindAttachments(missing, new CpTenantEmailMessage(
                "a@x.test",
                "s",
                "b",
                Attachments: [new CpTenantEmailAttachment(one, "Passport.pdf"), new CpTenantEmailAttachment(Path.Combine(dir, "gone.pdf"), "Gone.pdf")]));
            Assert.Equal("Attachment file not found.", blocked);
            Assert.Empty(missing.Attachments);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PageKeepsTheDraftAndPostsTheNoticeActions()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpLandlordApp.razor"));
        var panel = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/ErpTenancyExpiryPanel.razor"));
        Assert.Contains("ErpTenancyExpiryPanel", page, StringComparison.Ordinal);
        Assert.Contains("save_expiry_template", panel, StringComparison.Ordinal);
        Assert.Contains("send_expiry_due", panel, StringComparison.Ordinal);
        Assert.Contains("file_expiry_reply", panel, StringComparison.Ordinal);
        Assert.Contains("name=\"replyEmails\"", panel, StringComparison.Ordinal);
        Assert.Contains("name=\"autoSend\"", panel, StringComparison.Ordinal);
        Assert.Contains("name=\"csrf_guard_key\"", panel, StringComparison.Ordinal);
        Assert.Contains("{{tenant_name}}", panel, StringComparison.Ordinal);
        Assert.Contains("attach_expiry_document", panel, StringComparison.Ordinal);
        Assert.Contains("attach_tenant_document", panel, StringComparison.Ordinal);
        Assert.Contains("type=\"file\" name=\"document\"", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("multiple", panel, StringComparison.Ordinal);
        Assert.Equal("real_estate", EcomAE.Platform.Middleware.IndustrySpecificRoutePolicy.RequiredIndustry("/erp/landlord/document"));
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("ErpLandlordDocument", module, StringComparison.Ordinal);
        Assert.Contains("IErpTenancyExpiryMail expiry", module, StringComparison.Ordinal);
        Assert.Contains("save_expiry_template", module, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root was not found.");
    }
}
