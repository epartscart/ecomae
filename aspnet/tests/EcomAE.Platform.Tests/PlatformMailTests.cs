using System.Net;
using System.Net.Http;
using System.Text;
using EcomAE.Platform.Mail;
using EcomAE.Platform.Routing;
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
        Assert.Equal("/erp/mailbox-app", EcomAeRoutes.ErpMailboxApp);
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
        Assert.Contains("SuperCpHostGate.IsAllowed", super);
        Assert.Contains("type=\"password\"", super);
        Assert.Contains("Sessions.ValidateAsync", super);
        Assert.DoesNotContain("mint", super, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_token\" value=", super);
        Assert.DoesNotContain("password\" value=", super);

        var cp = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpTenantMailboxApp.razor"));
        Assert.Contains("@page \"/cp/mailbox-app\"", cp);
        Assert.Contains("site_key", cp);
        Assert.Contains("Sessions.ValidateAsync", cp);
        Assert.DoesNotContain("mint", cp, StringComparison.OrdinalIgnoreCase);

        var erp = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpTenantMailboxApp.razor"));
        Assert.Contains("@page \"/erp/mailbox-app\"", erp);
        Assert.Contains("site_key", erp);
        Assert.DoesNotContain("confirmWrites", erp);
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

        var own = await inbox.ReadAsync("acme", superCp: false, "ops@acme.test", 10, CancellationToken.None);
        Assert.True(own.Ok);
        Assert.Contains(own.Messages, m => m.Subject == "acme only");

        var all = inbox.ListForCaller("platform", superCp: true);
        Assert.Contains(all, m => m.Address == "ops@acme.test");
        Assert.Contains(all, m => m.Address == "ops@beta.test");
        Assert.Contains(all, m => m.Address == "yawer@ecomae.com");
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
        Assert.Contains("IPlatformMailService", src);
        Assert.Contains("SuperCpHostGate.IsAllowed", src);
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.ToString().TrimEnd('/').Split('/').Last();
            LastToken = request.Headers.TryGetValues(PurelymailDns.ApiTokenHeader, out var values) ? values.FirstOrDefault() : null;
            return Task.FromResult(Response);
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
