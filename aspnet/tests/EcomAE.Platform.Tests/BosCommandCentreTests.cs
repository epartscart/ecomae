using EcomAE.Platform.Auth;
using EcomAE.Platform.Bos;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosCommandCentreTests
{
    private static readonly OperatingCompanyRef Dubai = new(1, "MAIN", "Dubai Main");
    private static readonly OperatingCompanyRef AbuDhabi = new(2, "DXB", "Abu Dhabi");

    [Theory]
    [InlineData("cp", OperatingNavigationContext.JobAdministers, OperatingNavigationContext.StatementCp)]
    [InlineData("erp", OperatingNavigationContext.JobOperates, OperatingNavigationContext.StatementErp)]
    [InlineData("bos", OperatingNavigationContext.JobAttention, OperatingNavigationContext.StatementBos)]
    public void Resolve_StatesTheProductJob(string surface, string job, string statement)
    {
        var context = OperatingNavigationContext.Resolve(surface, "www.epartscart.com", null, [Dubai, AbuDhabi], "buyer@example.com", null);

        Assert.Equal(job, context.ProductJob);
        Assert.Equal(statement, context.JobStatement);
        Assert.Equal("epartscart.com", context.Host);
        Assert.Equal(OperatingNavigationContext.NoCompanyLabel, context.ActiveCompanyLabel);
        Assert.Equal(string.Empty, context.ActiveCompanyIdText);
    }

    [Fact]
    public void Resolve_CompanySwitchDropsThePreviousLegalEntity()
    {
        var first = OperatingNavigationContext.Resolve("bos", "epartscart.com", 1, [Dubai, AbuDhabi], "buyer@example.com", "sales");
        var second = OperatingNavigationContext.Resolve("bos", "epartscart.com", 2, [Dubai, AbuDhabi], "buyer@example.com", "cfo");

        Assert.Equal("MAIN · Dubai Main", first.ActiveCompanyLabel);
        Assert.Equal("1", first.ActiveCompanyIdText);
        Assert.Equal("DXB · Abu Dhabi", second.ActiveCompanyLabel);
        Assert.Equal("2", second.ActiveCompanyIdText);
        Assert.DoesNotContain("MAIN", second.ActiveCompanyLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("Dubai", second.ActiveCompanyLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("1", second.ActiveCompanyIdText, StringComparison.Ordinal);
        Assert.Equal("cfo", second.Role);
    }

    [Fact]
    public void Resolve_RejectsACompanyOutsideTheTenantList()
    {
        var context = OperatingNavigationContext.Resolve("erp", "www.epartscart.com", 99, [Dubai, AbuDhabi], null, "hacker");

        Assert.True(context.RequestedCompanyRejected);
        Assert.Null(context.CompanyId);
        Assert.Equal(OperatingNavigationContext.NoCompanyLabel, context.ActiveCompanyLabel);
        Assert.DoesNotContain("99", context.ActiveCompanyLabel, StringComparison.Ordinal);
        Assert.Equal(string.Empty, context.ActiveCompanyIdText);
        Assert.Null(context.Role);
    }

    [Fact]
    public void ClearCompanyHref_RemovesOnlyTheCompanyQuery()
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/attention";
        http.Request.QueryString = new QueryString("?company=1&role=sales&companyId=9");

        var href = OperatingNavigationContext.ClearCompanyHref(http.Request);

        Assert.Equal("/attention?role=sales", href);
        Assert.DoesNotContain("company", href, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AttentionHref_KeepsAnInScopeCompanyAndAKnownRole()
    {
        Assert.Equal("/attention?company=2&role=purchasing", OperatingNavigationContext.AttentionHref(2, "Purchasing"));
        Assert.Equal("/attention", OperatingNavigationContext.AttentionHref(null, "not-a-role"));
    }

    [Fact]
    public void Project_CompanySwitchDropsTheOtherCompanysRowsAndAmounts()
    {
        var load = Load(
            Doc("erp.sales_order", "Sales", 9, "SO-1", "Dubai order", 10.5m, companyId: 1),
            Doc("erp.sales_order", "Sales", 10, "SO-2", "Abu order", 20m, companyId: 2),
            Doc("erp.sales_order", "Sales", 11, "SO-3", "Tenant order", 5m, companyId: null));
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", 2, [Dubai, AbuDhabi], "buyer@example.com", "operations");

        var view = BosCommandCentreComposer.Project(context, load, canReadErp: true, canReadCrm: true);

        Assert.Equal("DXB · Abu Dhabi", view.Context.ActiveCompanyLabel);
        Assert.Contains(view.CompanyRows, row => row.Reference == "SO-2");
        Assert.DoesNotContain(view.CompanyRows, row => row.Reference == "SO-1");
        Assert.DoesNotContain(view.TenantRows, row => row.Reference == "SO-1");
        Assert.Contains(view.TenantRows, row => row.Reference == "SO-3");
        Assert.Contains("20.00", view.DisplayedNumbers);
        Assert.Contains("5.00", view.DisplayedNumbers);
        Assert.DoesNotContain("10.50", view.DisplayedNumbers);
        Assert.Equal("/erp/sales-orders-app?company=2", view.CompanyRows[0].DrillHref);
        Assert.Equal("/erp/sales-orders-app", view.TenantRows[0].DrillHref);
        Assert.DoesNotContain("Dubai", string.Join(' ', view.TenantRows.Select(row => row.Title)), StringComparison.Ordinal);
    }

    [Fact]
    public void Project_OmitsWeightedPipelineUnlessItEqualsTheOpenOpportunities()
    {
        var complete = Load(
            Doc("crm.opportunity", "Relationship", 1, "Brake kits", "proposal", 100m, probability: 50),
            Doc("crm.opportunity", "Relationship", 2, "Filters", "qualified", 40m, probability: 25));
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [Dubai], null, "sales");

        var view = BosCommandCentreComposer.Project(context, complete, canReadErp: false, canReadCrm: true);

        Assert.Equal(60.00m, view.WeightedPipeline);
        Assert.Equal("60.00", view.WeightedPipelineText);
        Assert.Equal(2, view.TenantRows.Count);
        Assert.DoesNotContain(view.TenantRows, row => row.Source.StartsWith("erp.", StringComparison.Ordinal));

        var partial = new BosCommandCentreLoad(true, DateTimeOffset.UnixEpoch,
        [
            new BosSourceSlice("crm.opportunity", "Relationship", true, false, complete.Slices[0].Rows),
        ]);
        var truncated = BosCommandCentreComposer.Project(context, partial, false, true);
        Assert.Null(truncated.WeightedPipeline);
        Assert.Null(truncated.WeightedPipelineText);

        var cfo = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [Dubai], null, "cfo");
        var finance = BosCommandCentreComposer.Project(cfo, complete, true, true);
        Assert.Null(finance.WeightedPipeline);
        Assert.Empty(finance.TenantRows);
    }

    [Fact]
    public void Project_DoesNotRenderAMissingSourceAsZero()
    {
        var load = new BosCommandCentreLoad(true, DateTimeOffset.UnixEpoch,
        [
            new BosSourceSlice("erp.sales_order", "Sales", true, true, []),
            new BosSourceSlice("erp.gl_journal", "Finance", false, false, []),
        ]);
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [], null, null);

        var view = BosCommandCentreComposer.Project(context, load, true, true);

        Assert.Empty(view.DisplayedNumbers);
        Assert.Null(view.WeightedPipeline);
        Assert.Contains("Journals could not be read.", view.Unread);
        Assert.False(view.SourcesEmpty);
        Assert.DoesNotContain(view.Unread, line => line.Any(char.IsDigit));
    }

    [Fact]
    public void Project_EmptyReadableSourcesStayEmpty()
    {
        var load = new BosCommandCentreLoad(true, null,
        [
            new BosSourceSlice("erp.purchase_order", "Procurement", true, true, []),
        ]);
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [], null, "purchasing");

        var view = BosCommandCentreComposer.Project(context, load, true, false);

        Assert.True(view.SourcesEmpty);
        Assert.Empty(view.DisplayedNumbers);
        Assert.Empty(view.TenantRows);
        Assert.Empty(view.CompanyRows);
    }

    [Fact]
    public void Project_DeniesReadsWithoutErpOrCrmCapability()
    {
        var load = Load(Doc("erp.sales_order", "Sales", 1, "SO-9", "Hidden", 99m, companyId: null));
        var context = OperatingNavigationContext.Resolve("bos", "epartscart.com", null, [], null, "ceo");

        var view = BosCommandCentreComposer.Project(context, load, canReadErp: false, canReadCrm: false);

        Assert.True(view.ReadDenied);
        Assert.Empty(view.DisplayedNumbers);
        Assert.Empty(view.TenantRows);
    }

    [Fact]
    public void AttentionRouteIsLoginWalledAndNotTheFleetGate()
    {
        Assert.True(AdminSurfaceAuthGateMiddleware.RequiresAdmin("/attention"));
        Assert.False(PlatformHostPolicy.IsProductBosPath("/attention"));
        Assert.False(PlatformHostPolicy.IsProductBosPath("/bos/command"));
    }

    [Fact]
    public async Task AttentionOnATenantHostRedirectsToErpLogin()
    {
        var middleware = new AdminSurfaceAuthGateMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("www.epartscart.com");
        context.Request.Path = "/attention";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, new AnonymousSession());

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        var location = context.Response.Headers.Location.ToString();
        Assert.StartsWith("/erp/login", location, StringComparison.Ordinal);
        Assert.DoesNotContain("/bos/login", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttentionOnATenantHostIsNotBlockedAsFleetBos()
    {
        var next = false;
        var middleware = new BosHostGateMiddleware(_ =>
        {
            next = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("www.epartscart.com");
        context.Request.Path = "/attention";

        await middleware.InvokeAsync(context);

        Assert.True(next);
        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public void ShellsShareTheNavigationContextAndDoNotInventMeasures()
    {
        var bar = Read("aspnet/src/EcomAE.Platform/Components/Shared/OperatingNavigationContextBar.razor");
        var attention = Read("aspnet/src/EcomAE.Platform/Components/Pages/AttentionApp.razor");
        var reader = Read("aspnet/src/EcomAE.Platform/Bos/BosCommandCentreReadService.cs");
        var cp = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor");
        var erp = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor");
        var bos = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor");

        Assert.Contains("OperatingNavigationContext.Resolve", bar, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureSwitchableCompanies", bar, StringComparison.Ordinal);
        Assert.Contains("Surface=\"cp\"", cp, StringComparison.Ordinal);
        Assert.Contains("Surface=\"erp\"", erp, StringComparison.Ordinal);
        Assert.Contains("Surface=\"bos\"", bos, StringComparison.Ordinal);
        Assert.Contains("href=\"/attention\"", cp, StringComparison.Ordinal);
        Assert.Contains("href=\"/attention\"", erp, StringComparison.Ordinal);
        Assert.DoesNotContain("coming soon", attention, StringComparison.OrdinalIgnoreCase);
        foreach (var measure in BosCommandCentreComposer.OmittedMeasures)
        {
            Assert.DoesNotContain(measure, attention, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("INSERT", reader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", reader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", reader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", reader, StringComparison.OrdinalIgnoreCase);
    }

    private static BosCommandCentreLoad Load(params BosSourceDocument[] documents)
    {
        var slices = documents
            .GroupBy(document => document.Source)
            .Select(group => new BosSourceSlice(group.Key, group.First().Category, true, true, group.ToList()))
            .ToList();
        return new BosCommandCentreLoad(true, DateTimeOffset.UnixEpoch, slices);
    }

    private static BosSourceDocument Doc(
        string source,
        string category,
        long id,
        string reference,
        string title,
        decimal? amount,
        int? probability = null,
        int? companyId = null)
        => new(source, category, id, reference, title, "open", amount, probability, companyId);

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private sealed class AnonymousSession : ILegacySessionValidator
    {
        public ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new LegacySessionContext(LegacySessionKind.Anonymous, 0, null, []));

        public ValueTask<LegacySessionContext> ValidateCustomerAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
            => ValidateAsync(httpContext, cancellationToken);
    }
}
