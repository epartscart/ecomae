using System.Text.RegularExpressions;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpLiveWriteCatalogStatusTests
{
    private static readonly string[] LiveRoutes =
    [
        "/erp/cash-accounts/create",
        "/erp/coa-accounts/create",
        "/erp/gl-journals/manual",
        "/erp/gl-journals/reverse",
        "/erp/sales-orders/delete",
        "/erp/purchase-orders/delete",
        "/erp/ajax/save-rfq",
        "/erp/ajax/petty-cash-save",
        "/erp/ajax/expense-report-save",
        "/erp/ajax/bank-import",
        "/erp/ajax/qm-order-create",
        "/erp/ajax/mfgr-mrp-run",
        "/erp/ajax/delivery-note-create",
        "/erp/ajax/invoice-save",
        "/erp/ajax/einvoice-submit",
        "/erp/ajax/opening-post-batch",
        "/erp/purchases/from-order",
        "/erp/purchases/adjust",
        "/erp/ajax/fx-revaluation-preview",
        "/erp/ajax/fx-post-revaluation",
        "/erp/ajax/fin-fx-revalue",
    ];

    [Fact]
    public void RoutesWithLiveWriteHandlersAreCatalogedAsLiveGated()
    {
        foreach (var route in LiveRoutes)
        {
            var row = SurfacePayloadContractCatalog.Functions.Single(f => f.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
            Assert.Contains("confirm_writes=true", row.Notes, StringComparison.Ordinal);
            Assert.DoesNotContain("confirm_writes refused", row.Notes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CatalogRowsStillMarkedDryRunHaveNoWriteServiceInHandler()
    {
        var root = RepoRoot();
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var routes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var constByRoute = Regex.Matches(routes, "(\\w+)\\s*=\\s*\"(/erp/[^\"]+)\"")
            .ToDictionary(m => m.Groups[2].Value, m => m.Groups[1].Value);

        foreach (var route in LiveRoutes)
        {
            Assert.True(constByRoute.TryGetValue(route, out var constant), route);
            var map = Regex.Match(module, "MapPost\\(EcomAeRoutes\\." + constant + "\\b[^\\n]*");
            Assert.True(map.Success, route);
            var handler = Regex.Match(map.Value, "Handle\\w+Async");
            var start = handler.Success
                ? Regex.Match(module, "Task<IResult> " + handler.Value + "\\(").Index
                : map.Index;
            var window = module.Substring(start, Math.Min(6000, module.Length - start));
            Assert.Matches("IErp\\w*Write\\w*Service", window);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
