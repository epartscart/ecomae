using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpUserControlGrantTests
{
    [Fact]
    public void PostedGrantsLimitAreasAndActionsWithoutChangingOpenUsers()
    {
        var open = new LegacySessionContext(
            LegacySessionKind.Admin,
            10,
            "session",
            [EcomAePermissions.TenantErpAccess]);
        var limited = open with
        {
            AreaGrants =
            [
                new ErpSessionAreaGrant("sales", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "View", "Delete" }, false),
            ],
        };
        var super = limited with { Permissions = [EcomAePermissions.SuperErpAccess] };

        Assert.True(ErpCapabilityCatalog.CanAction(open, "sales", "New"));
        Assert.False(ErpCapabilityCatalog.CanAction(open, "sales", "Delete"));
        Assert.True(ErpCapabilityCatalog.CanAction(limited, "sales", "View"));
        Assert.True(ErpCapabilityCatalog.CanAction(limited, "sales", "Delete"));
        Assert.False(ErpCapabilityCatalog.CanAction(limited, "sales", "New"));
        Assert.False(ErpCapabilityCatalog.CanAccess(limited, "finance"));
        Assert.True(ErpCapabilityCatalog.CanAction(super, "finance", "Reverse"));
    }

    [Fact]
    public void ControlledPathsFollowTheModuleAndTheAction()
    {
        var session = new LegacySessionContext(
            LegacySessionKind.Admin,
            4,
            "session",
            [EcomAePermissions.TenantErpAccess],
            AreaGrants:
            [
                new ErpSessionAreaGrant("inventory", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "View", "Post" }, false),
            ]);

        Assert.Equal("finance", ErpControlledAccess.AreaForPath("/erp/finance-app?company=1"));
        Assert.Equal("purchasing", ErpControlledAccess.AreaForPath("/erp/purchase-orders-app"));
        Assert.Equal("inventory", ErpControlledAccess.AreaForPath("/erp/product-info-app"));
        Assert.Equal("jewellery", ErpControlledAccess.AreaForPath("/erp/jw-metal-stock-app"));
        Assert.Equal("administration", ErpControlledAccess.AreaForPath("/erp/user-control-app"));
        Assert.Equal("sales", ErpControlledAccess.AreaForPath("/cp/client-erp/asap/erp/sales-orders-app"));
        Assert.Null(ErpControlledAccess.AreaForPath("/erp"));
        Assert.Null(ErpControlledAccess.AreaForPath("/erp/guide-app"));
        Assert.False(ErpControlledAccess.Allows(session, "/erp/finance-app", "GET"));
        Assert.True(ErpControlledAccess.Allows(session, "/erp/product-info-app", "GET"));
        Assert.False(ErpControlledAccess.Allows(session, "/erp/ajax/po-status", "POST"));
        Assert.True(ErpControlledAccess.Allows(session, "/erp/inventory-stock/post", "POST"));
        Assert.False(ErpControlledAccess.Allows(session, "/erp/stock-transfers/delete", "POST"));
        Assert.True(ErpControlledAccess.Allows(session, "/erp", "GET"));

        var open = session with { AreaGrants = null };
        Assert.True(ErpControlledAccess.Allows(open, "/erp/finance-app", "GET"));
    }

    [Fact]
    public void GrantFormParsesModulesAndRefusesSelfLockout()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["user_id"] = "15",
            ["limit_user"] = "1",
            ["grant_all"] = "sales",
            ["grant"] = new StringValues(["inventory|View", "inventory|Post"]),
            ["company_ids"] = "7",
            ["approval_limit"] = "1000",
        });

        Assert.True(ErpPermissionGrantWriteService.TryParse(form, 9, out var request, out var error), error);
        Assert.Equal(15, request.UserId);
        Assert.True(request.LimitUser);
        Assert.Contains(request.Areas, area => area.GroupKey == "sales" && area.AllActions);
        Assert.Contains(request.Areas, area => area.GroupKey == "inventory" && area.Actions is not null && area.Actions.Contains("Post"));
        Assert.Null(ErpPermissionGrantWriteService.Validate(request));

        var self = request with { ActorUserId = 15 };
        Assert.Equal("self_lockout", ErpPermissionGrantWriteService.Validate(self)?.Code);

        var kept = self with
        {
            Areas = self.Areas.Append(new ErpPermissionAreaGrant("administration", null, true)).ToArray(),
        };
        Assert.Null(ErpPermissionGrantWriteService.Validate(kept));

        var clear = request with { LimitUser = false };
        Assert.Null(ErpPermissionGrantWriteService.Validate(clear));
    }

    [Fact]
    public void UserControlPagePostsGrantsAndTheServerEnforcesThem()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpUserControlApp.razor"));
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpPermissionGrantWriteService.cs"));
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));

        Assert.Contains("name=\"user_id\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"limit_user\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"grant\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"grant_all\"", page, StringComparison.Ordinal);
        Assert.Contains("intent\" value=\"post\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/user-control/grant\"", page, StringComparison.Ordinal);
        Assert.Contains("Choose any user", page, StringComparison.Ordinal);
        Assert.Contains("ErpUserControlGrant", module, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `epc_erp_permission_grant`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_erp_permission_grant`", service, StringComparison.Ordinal);
        Assert.Contains("rbac_grant", service, StringComparison.Ordinal);
        Assert.Contains("IErpPermissionGrantWriteService", program, StringComparison.Ordinal);
        Assert.Contains("ErpControlledAccessMiddleware", program, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
