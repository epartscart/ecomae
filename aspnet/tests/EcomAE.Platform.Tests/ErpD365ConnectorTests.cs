using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using EcomAE.Platform.Erp;

public sealed class ErpD365ConnectorTests
{
    private static ErpD365ConnectorService NewService(IHttpClientFactory http, IConfiguration config)
        => new(http, new StubConnections(), config, NullLogger<ErpD365ConnectorService>.Instance);

    private static IConfiguration Config(IDictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static readonly IConfiguration EmptyConfig = Config(new Dictionary<string, string?>());

    [Fact]
    public void IsConfigured_requires_all_four_settings()
    {
        var svc = NewService(new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK)), EmptyConfig);
        Assert.False(svc.IsConfigured);

        var full = Config(new Dictionary<string, string?>
        {
            ["D365:TenantId"] = "t",
            ["D365:ClientId"] = "c",
            ["D365:ClientSecret"] = "s",
            ["D365:EnvironmentUrl"] = "https://env.operations.dynamics.com/",
        });
        var svc2 = NewService(new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK)), full);
        Assert.True(svc2.IsConfigured);
        Assert.Equal("https://env.operations.dynamics.com", svc2.EnvironmentUrl);
    }

    [Fact]
    public async Task TestConnection_not_configured()
    {
        var svc = NewService(new FakeHttp(_ => new HttpResponseMessage(HttpStatusCode.OK)), EmptyConfig);
        var res = await svc.TestConnectionAsync();
        Assert.False(res.Succeeded);
        Assert.Equal("not_configured", res.Code);
    }

    [Fact]
    public async Task TestConnection_success_requests_token_then_odata()
    {
        var http = new FakeHttp(req =>
        {
            if (req.RequestUri!.Host == "login.microsoftonline.com")
            {
                Assert.Equal(HttpMethod.Post, req.Method);
                return Json(HttpStatusCode.OK, "{\"access_token\":\"tok\",\"expires_in\":3600}");
            }

            Assert.Equal("/data/LegalEntities", req.RequestUri!.AbsolutePath.Split('?')[0]);
            Assert.Equal("Bearer tok", req.Headers.Authorization!.ToString());
            return Json(HttpStatusCode.OK, "{\"value\":[{\"CompanyId\":\"usmf\"}]}");
        });
        var svc = NewService(http, Config(new Dictionary<string, string?>
        {
            ["D365:TenantId"] = "t",
            ["D365:ClientId"] = "c",
            ["D365:ClientSecret"] = "s",
            ["D365:EnvironmentUrl"] = "https://env.operations.dynamics.com",
        }));

        var res = await svc.TestConnectionAsync();
        Assert.True(res.Succeeded, res.Message);
        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task TestConnection_token_failure_is_auth_failed()
    {
        var http = new FakeHttp(_ => Json(HttpStatusCode.BadRequest, "{\"error\":\"invalid_client\"}"));
        var svc = NewService(http, Config(new Dictionary<string, string?>
        {
            ["D365:TenantId"] = "t",
            ["D365:ClientId"] = "c",
            ["D365:ClientSecret"] = "s",
            ["D365:EnvironmentUrl"] = "https://env.operations.dynamics.com",
        }));

        var res = await svc.TestConnectionAsync();
        Assert.False(res.Succeeded);
        Assert.Equal("auth_failed", res.Code);
    }

    [Fact]
    public void KnownEntities_covers_master_and_document_sets()
    {
        Assert.Contains("ReleasedProductsV2", ErpD365ConnectorService.KnownEntities.Keys);
        Assert.Contains("CustomersV3", ErpD365ConnectorService.KnownEntities.Keys);
        Assert.Contains("VendorsV2", ErpD365ConnectorService.KnownEntities.Keys);
        Assert.Contains("SalesOrderHeadersV2", ErpD365ConnectorService.KnownEntities.Keys);
        Assert.Contains("PurchaseOrderHeadersV2", ErpD365ConnectorService.KnownEntities.Keys);
        Assert.Contains("MainAccounts", ErpD365ConnectorService.KnownEntities.Keys);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("no db in unit test");
    }
}
