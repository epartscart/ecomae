using System.Net;
using System.Net.Http;
using System.Text;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Mail;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public class PlatformMailTests
{
    [Fact]
    public void Routes_and_first_mailbox_are_platform_owned()
    {
        Assert.Equal("/cp/super-mail-app", EcomAeRoutes.ControlPanelSuperMailApp);
        Assert.Equal("/cp/super-mail/write", EcomAeRoutes.ControlPanelSuperMailWrite);
        Assert.Equal("/cp/mailbox-app", EcomAeRoutes.ControlPanelMailboxApp);
        Assert.Equal("/cp/mailbox/write", EcomAeRoutes.ControlPanelMailboxWrite);
        Assert.Equal("/erp/mailbox-app", EcomAeRoutes.ErpMailboxApp);
        Assert.Equal("/erp/mailbox/write", EcomAeRoutes.ErpMailboxWrite);
        Assert.Equal("/bos/mail-app", EcomAeRoutes.BosMailApp);
        Assert.Equal("yawer@ecomae.com", PurelymailDns.FirstMailbox);
        Assert.Equal("ecomae.com", PurelymailDns.FirstDomain);
        Assert.Equal("platform", PurelymailDns.PlatformSiteKey);
    }

    [Fact]
    public void Pages_post_confirmWrites_and_never_echo_secrets()
    {
        var root = FindRepoRoot();
        var super = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpSuperMailApp.razor"));
        Assert.Contains("@page \"/cp/super-mail-app\"", super);
        Assert.Contains("@page \"/bos/mail-app\"", super);
        Assert.Contains("action=\"/cp/super-mail/write\"", super);
        Assert.Contains("name=\"confirmWrites\"", super);
        Assert.Contains("value=\"true\"", super);
        Assert.Contains("yawer@ecomae.com", super);
        Assert.Contains("setup_tenants", super);
        Assert.Contains("setup_fleet", super);
        Assert.Contains("sync_domains", super);
        Assert.Contains("data-mail-fleet", super);
        Assert.Contains("purelymail.com", super);
        Assert.Contains("setup_business", super);
        Assert.Contains("CloudflareDnsPack", super);
        Assert.Contains("epartscart.com", super);
        Assert.Contains("register_login", super);
        Assert.Contains("succeed_operator", super);
        Assert.Contains("MailboxVisualGuide", super);
        Assert.Contains("ecomaedxb@gmail.com", super);
        Assert.Contains("SuperCpHostGate.IsAllowed", super);
        Assert.Contains("type=\"password\"", super);
        Assert.Contains("Sessions.ValidateAsync", super);
        Assert.Contains("data-mail-workspace", super);
        Assert.Contains("data-mail-next", super);
        Assert.Contains("epc-mail-hub", super);
        Assert.DoesNotContain("mint", super, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_token\" value=", super);
        Assert.DoesNotContain("password\" value=", super);

        var inbox = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/MailboxInboxWorkspace.razor"));
        Assert.Contains("action=\"@WritePath\"", inbox);
        Assert.Contains("create_staff", inbox);
        Assert.Contains("setup_business", inbox);
        Assert.Contains("name=\"confirmWrites\"", inbox);
        Assert.Contains("action\" value=\"send\"", inbox);
        Assert.Contains("MailboxVisualGuide", inbox);
        Assert.Contains("data-mail-workspace", inbox);
        Assert.Contains("Compose from", inbox);
        Assert.DoesNotContain("mint", inbox, StringComparison.OrdinalIgnoreCase);

        var cp = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpTenantMailboxApp.razor"));
        Assert.Contains("@page \"/cp/mailbox-app\"", cp);
        Assert.Contains("site_key", cp);
        Assert.Contains("PreferLoginAddress", cp);
        Assert.Contains("Sessions.ValidateAsync", cp);
        Assert.Contains("WritePath=\"/cp/mailbox/write\"", cp);
        Assert.Contains("ownInboxOnly", cp);
        Assert.Contains("MailboxInboxWorkspace", cp);
        Assert.Contains("CreateHint=\"@_createHint\"", cp);
        Assert.DoesNotContain("CreateHint=\"Creates", cp, StringComparison.Ordinal);
        Assert.DoesNotContain("mint", cp, StringComparison.OrdinalIgnoreCase);

        var guide = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/MailboxVisualGuide.razor"));
        Assert.Contains("Mailbox visual guide", guide);
        Assert.Contains("data-mail-guide", guide);
        Assert.Contains("Operator steps", guide);
        Assert.Contains("Tenant steps", guide);
        Assert.Contains("ERP customer steps", guide);
        Assert.Contains("Publish Cloudflare DNS", guide);
        Assert.Contains("purelymail.com", guide);
        Assert.Contains("Compose and send", guide);
        Assert.Contains("own inbox", guide);
        Assert.Contains("never Gmail", guide);
        Assert.DoesNotContain("mint", guide, StringComparison.OrdinalIgnoreCase);

        var erp = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpTenantMailboxApp.razor"));
        Assert.Contains("@page \"/erp/mailbox-app\"", erp);
        Assert.Contains("site_key", erp);
        Assert.Contains("PreferLoginAddress", erp);
        Assert.Contains("WritePath=\"/erp/mailbox/write\"", erp);
        Assert.Contains("ownInboxOnly", erp);
        Assert.Contains("MailboxInboxWorkspace", erp);
        Assert.Contains("ecomae.com", erp);
        Assert.Contains("Subtitle=\"@_subtitle\"", erp);
        Assert.Contains("CreateHint=\"@_createHint\"", erp);
        Assert.DoesNotContain("Subtitle=\"Own inbox", erp, StringComparison.Ordinal);
    }

    [Fact]
    public void Mailbox_time_labels_are_operator_readable()
    {
        Assert.Equal("ok", MailboxTime.DnsTone("on_purelymail"));
        Assert.Equal("warn", MailboxTime.DnsTone("needs_cloudflare"));
        Assert.Equal("Cloudflare", MailboxTime.DnsLabel("needs_cloudflare"));
        Assert.Equal("just now", MailboxTime.Rel(DateTimeOffset.UtcNow.AddSeconds(-10)));
    }

    [Fact]
    public void Unwritable_persist_path_does_not_throw_and_keeps_in_memory_seed()
    {
        var parent = Path.Combine(Path.GetTempPath(), "ecomae-mail-ro-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(parent);
        var blocker = Path.Combine(parent, "App_Data");
        File.WriteAllText(blocker, "not-a-directory");
        try
        {
            var path = Path.Combine(blocker, "platform-mail.json");
            var dir = new PlatformMailDirectory(null, path);
            Assert.Contains(dir.ListMailboxes("platform", superCp: false), m => m.Address == "yawer@ecomae.com");
            dir.UpsertDomain(new PlatformMailDomain("client.test", "client", DateTimeOffset.UtcNow));
            Assert.NotNull(dir.FindDomain("client.test"));
            Assert.True(File.Exists(blocker));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Data_file_prefers_ECOMAE_APP_DATA_over_release_App_Data()
    {
        var previous = Environment.GetEnvironmentVariable(PlatformMailDirectory.DataDirectoryEnv);
        try
        {
            Environment.SetEnvironmentVariable(PlatformMailDirectory.DataDirectoryEnv, "/var/lib/ecomae-aspnet");
            var path = PlatformMailDirectory.ResolveDataFile(null, "platform-mail.json");
            Assert.Equal("/var/lib/ecomae-aspnet/platform-mail.json", path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PlatformMailDirectory.DataDirectoryEnv, previous);
        }
    }

    [Fact]
    public void Domain_and_site_key_normalize()
    {
        Assert.Equal("client.com", PlatformMailNormalize.Domain("WWW.Client.COM."));
        Assert.Equal("ops@client.com", PlatformMailNormalize.Address("Ops", "www.client.com"));
        Assert.Equal("platform", PlatformMailNormalize.SiteKey("", superCpHost: true, requestHost: "ecomae.com"));
        Assert.Equal("acme", PlatformMailNormalize.SiteKey("acme", superCpHost: true, requestHost: "ecomae.com"));
        Assert.Equal("acme-test", PlatformMailNormalize.SiteKey(null, superCpHost: false, requestHost: "www.acme.test"));
        Assert.True(PlatformMailNormalize.IsEmail("yawer@ecomae.com"));
        Assert.False(PlatformMailNormalize.IsEmail("not-an-email"));
    }

    [Fact]
    public async Task Seed_yawer_is_first_and_smtp_defaults_to_purelymail()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        var first = Assert.Single(dir.ListMailboxes("platform", superCp: false), m => m.FirstWorking);
        Assert.Equal("yawer@ecomae.com", first.Address);
        Assert.Equal("platform", first.SiteKey);
        Assert.Contains(dir.ListDomains(), d => d.Domain == "ecomae.com" && d.FirstPlatform);

        var mail = new PlatformMailService(dir, new InMemoryPurelymailApi(), Options.Create(new PurelymailOptions()));
        var smtp = mail.SmtpFor("ops@client.com");
        Assert.Equal("smtp.purelymail.com", smtp.Host);
        Assert.Equal("587", smtp.Port);
        Assert.Equal("tls", smtp.Encryption);
        Assert.Equal("ops@client.com", smtp.Username);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Tenant_cannot_list_or_read_another_site_key()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        var api = new InMemoryPurelymailApi();
        var mail = new PlatformMailService(dir, api, Options.Create(new PurelymailOptions()));
        Assert.True((await mail.WriteAsync(new PlatformMailWriteRequest("add_domain", Domain: "acme.test", SiteKey: "acme"), CancellationToken.None)).Succeeded);
        Assert.True((await mail.WriteAsync(new PlatformMailWriteRequest("create_mailbox", LocalPart: "ops", Domain: "acme.test", SiteKey: "acme", Password: "acme-secret"), CancellationToken.None)).Succeeded);
        Assert.True((await mail.WriteAsync(new PlatformMailWriteRequest("add_domain", Domain: "beta.test", SiteKey: "beta"), CancellationToken.None)).Succeeded);
        Assert.True((await mail.WriteAsync(new PlatformMailWriteRequest("create_mailbox", LocalPart: "ops", Domain: "beta.test", SiteKey: "beta", Password: "beta-secret"), CancellationToken.None)).Succeeded);

        var imap = new InMemoryMailboxImapClient();
        imap.Boxes["ops@acme.test"] = [new MailboxMessage("1", "a@x", "ops@acme.test", "acme only", DateTimeOffset.UtcNow, "secret-acme", true)];
        imap.Boxes["ops@beta.test"] = [new MailboxMessage("2", "b@x", "ops@beta.test", "beta only", DateTimeOffset.UtcNow, "secret-beta", true)];
        var inbox = new MailboxInboxService(dir, imap);

        var acme = inbox.ListForCaller("acme", superCp: false);
        Assert.Contains(acme, m => m.Address == "ops@acme.test");
        Assert.DoesNotContain(acme, m => m.Address == "ops@beta.test");
        Assert.DoesNotContain(acme, m => m.Address == "yawer@ecomae.com");

        var stolen = await inbox.ReadAsync("acme", superCp: false, "ops@beta.test", 10, CancellationToken.None);
        Assert.False(stolen.Ok);
        Assert.Equal("isolated", stolen.Code);
        Assert.Empty(stolen.Messages);

        var ownOnly = inbox.ListForCaller("platform", superCp: false, "yawer@ecomae.com", ownInboxOnly: true);
        Assert.Contains(ownOnly, m => m.Address == "yawer@ecomae.com");
        Assert.DoesNotContain(ownOnly, m => m.Address == "ops@acme.test");
        var peeked = await inbox.ReadAsync("platform", superCp: false, "yawer@ecomae.com", 10, CancellationToken.None, "other@ecomae.com", ownInboxOnly: true);
        Assert.False(peeked.Ok);
        Assert.Equal("own_inbox", peeked.Code);

        var own = await inbox.ReadAsync("acme", superCp: false, "ops@acme.test", 10, CancellationToken.None);
        Assert.True(own.Ok);
        Assert.Contains(own.Messages, m => m.Subject == "acme only");

        var all = inbox.ListForCaller("platform", superCp: true);
        Assert.Contains(all, m => m.Address == "ops@acme.test");
        Assert.Contains(all, m => m.Address == "ops@beta.test");
        Assert.Contains(all, m => m.Address == "yawer@ecomae.com");

        Assert.Equal("ops@acme.test", inbox.PreferLoginAddress(acme, "ops@acme.test", ""));
        Assert.Equal("ops@beta.test", inbox.PreferLoginAddress(all, "ops@beta.test", ""));
        Assert.Equal("ops@acme.test", inbox.PreferLoginAddress(all, "ops@beta.test", "ops@acme.test"));
    }

    [Fact]
    public async Task Setup_tenants_adds_every_shop_domain_and_skips_platform_host()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        var mail = new PlatformMailService(dir, new InMemoryPurelymailApi(), Options.Create(new PurelymailOptions()));
        var plan = mail.PlanTenantDomains(
        [
            ("www.ecomae.com", "platform"),
            ("acme.test", "acme"),
            ("www.beta.test", "beta"),
            ("acme.test", "acme"),
        ]);
        Assert.DoesNotContain(plan, p => p.Domain == "ecomae.com");
        Assert.Contains(plan, p => p.Domain == "acme.test" && p.SiteKey == "acme" && !p.DomainReady);
        Assert.Contains(plan, p => p.Domain == "beta.test" && p.SiteKey == "beta");

        var written = await mail.WriteAsync(
            new PlatformMailWriteRequest(
                "setup_tenants",
                Tenants: [("acme.test", "acme"), ("www.beta.test", "beta"), ("ecomae.com", "platform")]),
            CancellationToken.None);
        Assert.True(written.Succeeded);
        Assert.True(written.Writes >= 2);
        Assert.NotNull(dir.FindDomain("acme.test"));
        Assert.NotNull(dir.FindDomain("beta.test"));

        var login = await mail.WriteAsync(new PlatformMailWriteRequest("register_login", Address: "ops@acme.test", SiteKey: "acme"), CancellationToken.None);
        Assert.True(login.Succeeded);
        Assert.NotNull(dir.FindMailbox("ops@acme.test"));
    }

    [Fact]
    public async Task Fleet_lists_shop_domains_before_purelymail_accepts_them()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        var api = new InMemoryPurelymailApi { Domains = { "ecomae.com" } };
        var mail = new PlatformMailService(dir, api, Options.Create(new PurelymailOptions()));
        var fleet = mail.ListFleet([]);
        Assert.Contains(fleet, r => r.Domain == "ecomae.com");
        Assert.Contains(fleet, r => r.Domain == "epartscart.com" && r.SiteKey == "epartscart");
        Assert.Contains(fleet, r => r.Domain == "taxofinca.com");
        Assert.Contains(fleet, r => r.Domain == "electronicae.com");
        Assert.Contains(fleet, r => r.Domain == "stylenlook.com");
        Assert.Contains(fleet, r => r.Domain == "thejewellerytrend.com");
        Assert.DoesNotContain(fleet, r => r.Domain == "epartscart.com" && r.OnPurelymail);

        var synced = await mail.WriteAsync(new PlatformMailWriteRequest("sync_domains"), CancellationToken.None);
        Assert.True(synced.Succeeded);
        Assert.True(dir.FindDomain("ecomae.com")?.ApiSynced);

        api.FailNext = true;
        var added = await mail.WriteAsync(new PlatformMailWriteRequest("add_domain", Domain: "epartscart.com", SiteKey: "epartscart"), CancellationToken.None);
        Assert.True(added.Succeeded);
        Assert.True(dir.FindDomain("epartscart.com")?.ApiSynced);

        Assert.Contains(
            PurelymailHttpApi.ParseDomainNames("""{"type":"success","result":{"domains":[{"name":"ecomae.com"},{"name":"epartscart.com"}]}}"""),
            d => d == "epartscart.com");
    }

    [Fact]
    public async Task Setup_business_creates_standard_locals_and_routes_aliases()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        var api = new InMemoryPurelymailApi();
        var mail = new PlatformMailService(dir, api, Options.Create(new PurelymailOptions()));
        var written = await mail.WriteAsync(new PlatformMailWriteRequest("setup_business", Domain: "ecomae.com", SiteKey: "platform"), CancellationToken.None);
        Assert.True(written.Succeeded);
        foreach (var local in PurelymailDns.BusinessLocals)
        {
            Assert.NotNull(dir.FindMailbox(local + "@ecomae.com"));
            Assert.Contains(api.Users, u => string.Equals(u, local + "@ecomae.com", StringComparison.OrdinalIgnoreCase));
        }

        Assert.Contains(api.Calls, c => c.StartsWith("createRoutingRule:ecomae.com:->info@ecomae.com", StringComparison.Ordinal));
        Assert.Contains(api.Calls, c => c.Contains("contact->info@ecomae.com", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Http_api_sends_token_header_and_parses_errors()
    {
        var handler = new StubHandler();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"type":"success","result":{"code":"purelymail-ownership-abc"}}""", Encoding.UTF8, "application/json")
        };
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        dir.SaveApiToken("test-token-1234");
        var api = new PurelymailHttpApi(new StubFactory(handler), dir, Options.Create(new PurelymailOptions()));
        var ownership = await api.GetOwnershipCodeAsync(CancellationToken.None);
        Assert.True(ownership.Ok);
        Assert.Equal("purelymail-ownership-abc", ownership.Message);
        Assert.Equal("getOwnershipCode", handler.LastPath);
        Assert.Equal("test-token-1234", handler.LastToken);

        var parsed = PurelymailHttpApi.Parse(false, """{"type":"error","code":"exists","message":"already"}""");
        Assert.False(parsed.Ok);
        Assert.Equal("exists", parsed.Code);
        Assert.Equal("already", parsed.Message);

        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"type":"success","result":{}}""", Encoding.UTF8, "application/json")
        };
        var created = await api.CreateUserAsync("info@ecomae.com", "secret-box", CancellationToken.None);
        Assert.True(created.Ok);
        Assert.Equal("createUser", handler.LastPath);
        Assert.Contains("\"userName\":\"info\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"domainName\":\"ecomae.com\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"password\":\"secret-box\"", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("newPassword", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Token_hint_never_returns_the_secret()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        dir.SaveApiToken("super-secret-token");
        var hint = dir.TokenHint(new PurelymailOptions());
        Assert.Contains("set", hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-token", hint, StringComparison.Ordinal);
        Assert.EndsWith("oken", hint);
    }

    [Fact]
    public void Module_maps_super_mail_write()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelSuperMailWrite", src);
        Assert.Contains("ControlPanelMailboxWrite", src);
        Assert.Contains("ErpMailboxWrite", src);
        Assert.Contains("IPlatformMailService", src);
        Assert.Contains("ITenantMailboxSelfServe", src);
        Assert.Contains("succeed_operator", src);
        Assert.Contains("SuperCpHostGate.IsAllowed", src);
        Assert.Contains("platformErp", src);
        Assert.Contains("AllowPlatformErp", src);
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("TenantTransactionalMailer", program);
    }

    [Fact]
    public void Tenant_guard_keeps_mailboxes_on_the_caller_domain()
    {
        Assert.True(TenantMailboxGuard.TryBind("create_staff", "acme.test", "ops@acme.test", "ops", "www.acme.test", out var domain, out var siteKey, out var address, out _, out _));
        Assert.Equal("acme.test", domain);
        Assert.Equal("acme-test", siteKey);
        Assert.Equal("ops@acme.test", address);

        Assert.False(TenantMailboxGuard.TryBind("create_mailbox", "beta.test", null, "ops", "www.acme.test", out _, out _, out _, out var isolated, out _));
        Assert.Equal("isolated", isolated);

        Assert.False(TenantMailboxGuard.TryBind("create_mailbox", null, "ops@ecomae.com", "ops", "www.acme.test", out _, out _, out _, out var stolen, out _));
        Assert.Equal("isolated", stolen);

        Assert.False(TenantMailboxGuard.TryBind("create_mailbox", null, "ops@acme.test", "ops", "www.ecomae.com", out _, out _, out _, out var platform, out _));
        Assert.Equal("platform_domain", platform);

        Assert.True(TenantMailboxGuard.TryBind("create_staff", null, null, "buyer1", "www.ecomae.com", out var erpDomain, out var erpKey, out var erpAddress, out _, out _, allowPlatformErp: true));
        Assert.Equal("ecomae.com", erpDomain);
        Assert.Equal("platform", erpKey);
        Assert.Equal("buyer1@ecomae.com", erpAddress);

        Assert.False(TenantMailboxGuard.TryBind("create_staff", null, null, "info", "www.ecomae.com", out _, out _, out _, out var reserved, out _, allowPlatformErp: true));
        Assert.Equal("reserved_mailbox", reserved);
        Assert.False(TenantMailboxGuard.TryBind("create_staff", "epartscart.com", null, "ops", "www.ecomae.com", out _, out _, out _, out var shopFromErp, out _, allowPlatformErp: true));
        Assert.Equal("isolated", shopFromErp);
        Assert.False(TenantMailboxGuard.TryBind("setup_business", null, null, "ops", "www.ecomae.com", out _, out _, out _, out var fleetFromErp, out _, allowPlatformErp: true));
        Assert.Equal("tenant_forbidden", fleetFromErp);

        Assert.False(TenantMailboxGuard.TryBind("save_token", null, null, "ops", "www.acme.test", out _, out _, out _, out var forbidden, out _));
        Assert.Equal("tenant_forbidden", forbidden);
        Assert.False(TenantMailboxGuard.TryBind("setup_tenants", null, null, "ops", "www.acme.test", out _, out _, out _, out _, out _));
        Assert.False(TenantMailboxGuard.TryBind("succeed_operator", null, null, "ops", "www.acme.test", out _, out _, out _, out _, out _));
    }

    [Fact]
    public async Task Tenant_self_serve_creates_only_on_its_domain()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        var mail = new PlatformMailService(dir, new InMemoryPurelymailApi(), Options.Create(new PurelymailOptions()));
        var users = new RecordingUsers();
        var smtp = new InMemoryMailboxSmtpClient();
        var serve = new TenantMailboxSelfServeService(mail, users, new UnconfiguredWrites(), new MailboxComposer(dir, smtp));

        var stolen = await serve.WriteAsync(new TenantMailboxSelfServeRequest("create_staff", "www.acme.test", "ops", "ops@beta.test", "box-secret", "[3]"), CancellationToken.None);
        Assert.False(stolen.Succeeded);
        Assert.Equal("isolated", stolen.Code);
        Assert.Empty(users.Created);

        var created = await serve.WriteAsync(new TenantMailboxSelfServeRequest("create_staff", "www.acme.test", "ops", null, "box-secret", "[3]"), CancellationToken.None);
        Assert.True(created.Succeeded);
        Assert.NotNull(dir.FindMailbox("ops@acme.test"));
        Assert.Equal("acme-test", dir.FindMailbox("ops@acme.test")!.SiteKey);
        Assert.DoesNotContain(dir.ListMailboxes("platform", superCp: false), m => m.Address == "ops@acme.test");
        Assert.Contains(users.Created, e => e == "ops@acme.test");

        var stolenSend = await serve.WriteAsync(new TenantMailboxSelfServeRequest("send", "www.acme.test", null, "ops@beta.test", null, null, "boss@beta.test", "x", "y"), CancellationToken.None);
        Assert.False(stolenSend.Succeeded);
        Assert.Equal("isolated", stolenSend.Code);

        var sent = await serve.WriteAsync(new TenantMailboxSelfServeRequest("send", "www.acme.test", null, "ops@acme.test", null, null, "buyer@example.com", "Quote", "Hello"), CancellationToken.None);
        Assert.True(sent.Succeeded);
        Assert.Contains(smtp.Sent, m => m.From == "ops@acme.test" && m.To == "buyer@example.com" && m.Subject == "Quote");

        var erpBox = await serve.WriteAsync(new TenantMailboxSelfServeRequest("create_staff", "www.ecomae.com", "buyer1", null, "erp-secret", "[3]", AllowPlatformErp: true, LoginEmail: "yawer@ecomae.com"), CancellationToken.None);
        Assert.True(erpBox.Succeeded);
        Assert.NotNull(dir.FindMailbox("buyer1@ecomae.com"));
        Assert.Equal("platform", dir.FindMailbox("buyer1@ecomae.com")!.SiteKey);

        var reserved = await serve.WriteAsync(new TenantMailboxSelfServeRequest("create_staff", "www.ecomae.com", "info", null, "erp-secret", "[3]", AllowPlatformErp: true), CancellationToken.None);
        Assert.False(reserved.Succeeded);
        Assert.Equal("reserved_mailbox", reserved.Code);

        var stolenOwn = await serve.WriteAsync(new TenantMailboxSelfServeRequest("send", "www.ecomae.com", null, "buyer1@ecomae.com", null, null, "x@y.test", "Hi", "Body", "other@ecomae.com", true), CancellationToken.None);
        Assert.False(stolenOwn.Succeeded);
        Assert.Equal("own_inbox", stolenOwn.Code);

        var ownSend = await serve.WriteAsync(new TenantMailboxSelfServeRequest("send", "www.ecomae.com", null, "buyer1@ecomae.com", null, null, "x@y.test", "Hi", "Body", "buyer1@ecomae.com", true), CancellationToken.None);
        Assert.True(ownSend.Succeeded);
    }

    [Fact]
    public async Task Setup_fleet_creates_business_boxes_on_known_shop_domains()
    {
        var dir = new PlatformMailDirectory(null, Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json"));
        var api = new InMemoryPurelymailApi();
        var mail = new PlatformMailService(dir, api, Options.Create(new PurelymailOptions()));
        var written = await mail.WriteAsync(new PlatformMailWriteRequest("setup_fleet"), CancellationToken.None);
        Assert.True(written.Succeeded);
        foreach (var (hostname, siteKey) in KnownShopMailTenants.Rows)
        {
            Assert.NotNull(dir.FindDomain(hostname));
            Assert.Equal(siteKey, dir.FindDomain(hostname)!.SiteKey);
            Assert.NotNull(dir.FindMailbox("info@" + hostname));
            Assert.Equal(siteKey, dir.FindMailbox("info@" + hostname)!.SiteKey);
            Assert.DoesNotContain(dir.ListMailboxes("platform", superCp: false), m => m.Address == "info@" + hostname);
        }

        Assert.Contains(KnownShopMailTenants.Rows, r => r.Hostname == "epartscart.com");
        Assert.Contains(KnownShopMailTenants.Rows, r => r.Hostname == "electronicae.com");
        Assert.Contains(KnownShopMailTenants.Rows, r => r.Hostname == "stylenlook.com");
        Assert.Contains(KnownShopMailTenants.Rows, r => r.Hostname == "thejewellerytrend.com");
        Assert.Contains(KnownShopMailTenants.Rows, r => r.Hostname == "taxofinca.com");
        Assert.NotNull(dir.FindMailbox("info@ecomae.com"));
        Assert.Equal("platform", dir.FindMailbox("info@ecomae.com")!.SiteKey);
        var dns = CloudflareDnsPack.ForDomain("epartscart.com", "purelymail-ownership-test");
        Assert.Contains(dns, r => r.Type == "MX" && r.Content.Contains(PurelymailDns.MxHost, StringComparison.Ordinal));
        Assert.Contains(dns, r => r.Type == "TXT" && r.Content.Contains("spf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dns, r => r.Hint.Contains("ownership", StringComparison.OrdinalIgnoreCase));
        var csv = CloudflareDnsPack.ToCsv();
        Assert.Contains("ecomae.com", csv, StringComparison.Ordinal);
        Assert.Contains("epartscart.com", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("password", csv, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnconfiguredWrites : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }

    private sealed class RecordingUsers : ICpUserWriteService
    {
        public List<string> Created { get; } = [];

        public Task<ErpSimpleWriteResult> SetCommentAsync(long userId, string? comment, CancellationToken cancellationToken = default)
            => Task.FromResult(ErpSimpleWriteResult.Fail("no", "no"));

        public Task<ErpSimpleWriteResult> SetVinViewedAsync(IReadOnlyList<long> requestIds, int viewedFlag, CancellationToken cancellationToken = default)
            => Task.FromResult(ErpSimpleWriteResult.Fail("no", "no"));

        public Task<ErpSimpleWriteResult> SendVinMessageAsync(long vinId, string? text, CancellationToken cancellationToken = default)
            => Task.FromResult(ErpSimpleWriteResult.Fail("no", "no"));

        public Task<ErpSimpleWriteResult> SetUnlockedAsync(long userId, int unlockedFlag, long actorUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ErpSimpleWriteResult.Fail("no", "no"));

        public Task<ErpSimpleWriteResult> CreateAsync(string? email, int emailConfirmed, string? phone, int phoneConfirmed, string? password, int unlocked, int regVariant, string? fieldsJson, string? groupsJson, CancellationToken cancellationToken = default)
        {
            Created.Add(email ?? "");
            return Task.FromResult(ErpSimpleWriteResult.Ok("created", 9));
        }

        public Task<ErpSimpleWriteResult> SetPasswordAsync(long userId, string? password, string? keepSession, CancellationToken cancellationToken = default)
            => Task.FromResult(ErpSimpleWriteResult.Fail("no", "no"));

        public Task<ErpSimpleWriteResult> UpdateAsync(long userId, string? email, int emailConfirmed, string? phone, int phoneConfirmed, string? password, int unlocked, int regVariant, string? fieldsJson, string? groupsJson, long actorUserId, string? actorSession, CancellationToken cancellationToken = default)
            => Task.FromResult(ErpSimpleWriteResult.Fail("no", "no"));

        public Task<ErpSimpleWriteResult> DeleteAsync(string? idsJson, long actorUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(ErpSimpleWriteResult.Fail("no", "no"));
    }

    [Fact]
    public async Task Transactional_mail_uses_tenant_domain_not_gmail()
    {
        var path = Path.Combine(Path.GetTempPath(), "ecomae-mail-" + Guid.NewGuid().ToString("n") + ".json");
        var dir = new PlatformMailDirectory(null, path);
        dir.UpsertMailbox(new PlatformMailbox(
            "orders@epartscart.com",
            "orders",
            "epartscart.com",
            "epartscart",
            DateTimeOffset.UtcNow));
        var stored = await new PlatformMailService(dir, new InMemoryPurelymailApi(), Options.Create(new PurelymailOptions()))
            .WriteAsync(new PlatformMailWriteRequest("set_mailbox_password", Address: "orders@epartscart.com", Password: "box-pass"), CancellationToken.None);
        Assert.True(stored.Succeeded);

        var smtp = new InMemoryMailboxSmtpClient();
        var inner = new RecordingInnerMailer();
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        http.HttpContext.Request.Host = new HostString("www.epartscart.com");
        var mailer = new TenantTransactionalMailer(inner, dir, smtp, http);

        using (TenantTransactionalMail.UseNotification("new_order_to_manager"))
        {
            var config = mailer.ReadConfig();
            Assert.Equal("orders@epartscart.com", config["from_email"]);
            Assert.Equal("smtp.purelymail.com", config["smtp_host"]);
            var sent = await mailer.SendHtmlAsync("buyer@client.example", "Order", "<p>hi</p>");
            Assert.True(sent.Ok);
        }

        var row = Assert.Single(smtp.Sent);
        Assert.Equal("orders@epartscart.com", row.From);
        Assert.Equal("buyer@client.example", row.To);
        Assert.Empty(inner.Sent);
        Assert.Equal("orders", TenantTransactionalMail.LocalFor("lpo_to_supplier"));
        Assert.Equal("info", TenantTransactionalMail.LocalFor("new_order_to_user"));
        Assert.Equal("orders@epartscart.com", TenantTransactionalMail.PreferDomainInbox("epartscart@gmail.com", "www.epartscart.com"));
    }

    private sealed class RecordingInnerMailer : ICpPlatformMailer
    {
        public List<string> Sent { get; } = [];

        public IReadOnlyDictionary<string, string> ReadConfig()
            => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["from_email"] = "epartscart@gmail.com",
                ["smtp_host"] = "smtp.gmail.com",
            };

        public Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Sent.Add(to);
            return Task.FromResult(new CpSmsSendOutcome(true, "inner"));
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri(PurelymailDns.ApiBaseUrl)
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public string? LastPath { get; private set; }
        public string? LastToken { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.ToString().TrimEnd('/').Split('/').Last();
            LastToken = request.Headers.TryGetValues(PurelymailDns.ApiTokenHeader, out var values) ? values.FirstOrDefault() : null;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root from " + AppContext.BaseDirectory);
    }
}
