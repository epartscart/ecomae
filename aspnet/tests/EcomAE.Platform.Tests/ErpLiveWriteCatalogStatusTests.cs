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
        "/erp/ajax/fy-close",
        "/erp/ajax/fin-alloc-run",
        "/erp/ajax/fin-accrual-save",
        "/erp/ajax/period-list",
        "/erp/ajax/period-checklist",
        "/erp/ajax/period-summary",
        "/erp/workflow/status",
        "/erp/subscriptions/status",
        "/erp/ajax/inv-set-reorder-level",
        "/erp/ajax/hr-update-days",
        "/erp/ajax/prja-recognize",
        "/erp/ajax/transfer-voucher",
        "/erp/ajax/shortcut-delete",
        "/erp/ajax/shortcut-delete-key",
        "/erp/ajax/shortcut-reset",
        "/erp/ajax/erp-fav-add",
        "/erp/ajax/erp-fav-remove",
        "/erp/ajax/ctr-status",
        "/erp/ajax/fy-reopen",
        "/erp/ajax/fy-period-status",
        "/erp/ajax/fa-create-asset",
        "/erp/ajax/fa-run-depreciation",
        "/erp/ajax/period-log",
        "/erp/ajax/settlement-open-docs",
        "/erp/ajax/invoice-list",
        "/erp/ajax/save-contact",
        "/erp/ajax/sync-contacts",
        "/erp/ajax/customer-create",
        "/erp/ajax/save-company",
        "/erp/ajax/save-template",
        "/erp/ajax/shortcut-list",
        "/erp/ajax/inv-scan-lookup",
        "/erp/ajax/erp-global-search",
        "/erp/ajax/command-center",
        "/erp/ajax/cc-kpi-tiles",
        "/erp/ajax/concurrency-status",
        "/erp/ajax/presence-heartbeat",
        "/erp/ajax/cc-approval-queue",
        "/erp/ajax/dashboard",
        "/erp/suppliers/create",
        "/erp/purchases/create",
        "/erp/suppliers/sync",
        "/erp/gl-journals/sync-unposted",
        "/erp/gl-journals/post-sales",
        "/erp/ajax/workflow-save",
        "/erp/ajax/workflow-run",
        "/erp/ajax/automation-activate",
        "/erp/ajax/automation-install-template",
        "/erp/ajax/automation-enable-category",
        "/erp/ajax/automation-tick",
        "/erp/ajax/aml-check",
        "/erp/ajax/aml-kyc-save",
        "/erp/ajax/aml-alert-status",
        "/erp/ajax/aml-settings-save",
        "/erp/ajax/aml-report-generate",
        "/erp/ajax/aml-seed-rules",
        "/erp/ajax/invoice-from-order",
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
            Assert.Matches("I(Erp|Cp)\\w*(Write|Read)\\w*Service", window);
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
