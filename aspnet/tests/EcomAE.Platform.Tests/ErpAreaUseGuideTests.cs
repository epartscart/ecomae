using EcomAE.Platform.Components.Shared;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAreaUseGuideTests
{
    [Fact]
    public void EveryProcessAreaHasOneLaneAndARealPage()
    {
        var lanes = ErpAreaUseGuide.Lanes;
        Assert.Equal(ErpProcessFlowCatalog.Areas.Count, lanes.Count);
        Assert.Equal(lanes.Count, lanes.Select(lane => lane.AreaId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(ErpAreaUseGuide.Boards.Sum(board => board.Lanes.Count), lanes.Count);
        Assert.All(lanes, lane =>
        {
            Assert.False(string.IsNullOrWhiteSpace(lane.Title));
            Assert.False(string.IsNullOrWhiteSpace(lane.Use));
            Assert.True(lane.Steps.Count >= 3, lane.AreaId);
            Assert.Equal(Enumerable.Range(1, lane.Steps.Count), lane.Steps.Select(step => step.Number));
            Assert.All(lane.Steps.Concat(lane.JewellerySteps), step =>
            {
                Assert.StartsWith("/erp", step.Href, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("fa-", step.Icon, StringComparison.Ordinal);
                Assert.DoesNotContain("epc_erp_shell", step.Href, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("php-reference", step.Href, StringComparison.OrdinalIgnoreCase);
            });
        });

        var purchasing = ErpAreaUseGuide.Find("purchasing");
        Assert.NotNull(purchasing);
        Assert.Contains("pay", purchasing!.Use, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(purchasing.Steps, step => step.Href == "/erp/purchase-orders-app");
        Assert.True(purchasing.JewellerySteps.Count > 0);
        Assert.Equal("area-purchasing", ErpAreaUseGuide.Anchor("purchasing"));
        Assert.Equal(ErpAreaUseGuide.Fallback, ErpAreaUseGuide.UseFor("not-a-real-area"));
    }

    [Fact]
    public void GuideOpensTheAreaBoardAndKeepsTheModuleBook()
    {
        var guide = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpGuideApp.razor"));
        Assert.Contains("ErpAreaUseGuideBoard", guide, StringComparison.Ordinal);
        Assert.Contains("ErpGuideBooks.Areas", guide, StringComparison.Ordinal);
        Assert.Contains("ErpGuideBooks.Modules", guide, StringComparison.Ordinal);
        var board = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpAreaUseGuideBoard.razor"));
        Assert.Contains("How to use every area", board, StringComparison.Ordinal);
        Assert.Contains("data-mode=\"stage\"", board, StringComparison.Ordinal);
        Assert.Contains("data-eg-play", board, StringComparison.Ordinal);
        Assert.Contains("ClientErpPath.PrefixIfBound", board, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", board, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_shell", board, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", board, StringComparison.Ordinal);
        var home = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpHomeProcessMap.razor"));
        Assert.Contains("How to use every area", home, StringComparison.Ordinal);
        Assert.Contains("ErpAreaUseGuide.UseFor", home, StringComparison.Ordinal);
        var workspace = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        Assert.Contains("How to use.", workspace, StringComparison.Ordinal);
        Assert.Contains("ErpAreaUseGuide.UseFor", workspace, StringComparison.Ordinal);
        Assert.Equal(ErpGuideBooks.Areas, ErpGuideBooks.Get(null).Key);
        Assert.Equal(ErpGuideBooks.Modules, ErpGuideBooks.Get(ErpGuideBooks.Modules).Key);
    }

    [Fact]
    public async Task BoardRendersEveryAreaOnceWithWorkingLinks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        await using var renderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var view = await renderer.RenderComponentAsync<ErpAreaUseGuideBoard>(ParameterView.Empty);
            return view.ToHtmlString();
        });

        Assert.Contains("How to use every area", html, StringComparison.Ordinal);
        Assert.Contains("id=\"area-purchasing\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"area-finance\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"area-sales\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/erp/purchase-orders-app\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/erp/gl-journals-app\"", html, StringComparison.Ordinal);
        Assert.Contains("match the bill, then pay it", html, StringComparison.Ordinal);
        Assert.Contains(">Jewellery<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_shell", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ErpAreaUseGuide.Lanes.Count, Count(html, "class=\"eg-guide__lane\""));
        Assert.Equal(ErpAreaUseGuide.Lanes.Count, Count(html, "id=\"area-"));
        Assert.Equal(1, Count(html, "id=\"area-purchasing\""));
    }

    private static int Count(string html, string token)
    {
        var count = 0;
        var from = 0;
        while (from < html.Length)
        {
            var at = html.IndexOf(token, from, StringComparison.Ordinal);
            if (at < 0)
            {
                break;
            }

            count++;
            from = at + token.Length;
        }

        return count;
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, relative);
            if (File.Exists(path))
            {
                return path;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
