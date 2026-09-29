using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Security;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpNavigationCoverageTests
{
    [Fact]
    public void CoveragePageIsAnAspNetPrimaryRoute()
    {
        var root = FindRepoRoot();
        var page = Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Pages",
            "ErpNavigationCoverageApp.razor");
        var text = File.ReadAllText(page);

        Assert.Contains("@page \"/erp/navigation-coverage-app\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpIndustryNav.Inspect", text, StringComparison.Ordinal);
        Assert.Contains("ErpPhpTabRouteMap.TryMapTab", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpReferenceOnlyHref", text, StringComparison.Ordinal);

        var chrome = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Shared",
            "Desktop",
            "PhpErpDesktopChrome.razor"));
        Assert.Contains("navigation-coverage-app", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPhpErpTabMapsToAnExistingAspNetPageRoute()
    {
        var root = FindRepoRoot();
        var pages = Directory
            .GetFiles(
                Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages"),
                "*.razor")
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                "@page\\s+\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value.Split('?', 2)[0]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = PhpModuleCatalog.ErpTabs
            .Select(tab => tab.Id[(tab.Id.LastIndexOf('/') + 1)..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(tab => ErpPhpTabRouteMap.TryMapTab(tab, out var href)
                && !pages.Contains(href.Split('?', 2)[0]))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryPhpErpAreaHubStaysOnAnExistingErpRoute()
    {
        var root = FindRepoRoot();
        var pages = Directory
            .GetFiles(
                Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages"),
                "*.razor")
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                "@page\\s+\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value.Split('?', 2)[0]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalid = PhpModuleCatalog.ErpAreas
            .Select(area => PhpSurfaceLinkMap.AspNetPrimaryHref(area.Href))
            .Where(href => !href.StartsWith("/erp", StringComparison.OrdinalIgnoreCase)
                || !pages.Contains(href.Split('?', 2)[0]))
            .ToArray();

        Assert.Empty(invalid);
    }

    [Fact]
    public void TaxComplianceUsesTheErpChromeOnItsErpRoute()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Pages",
            "CpUaeTaxComplianceApp.razor"));
        var chrome = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Shared",
            "Desktop",
            "TaxComplianceSurfaceChrome.razor"));

        Assert.Contains("@page \"/erp/uae-tax-compliance-app\"", page, StringComparison.Ordinal);
        Assert.Contains("<TaxComplianceSurfaceChrome", page, StringComparison.Ordinal);
        Assert.Contains("Tax operations", page, StringComparison.Ordinal);
        Assert.Contains("/erp/vat-app", page, StringComparison.Ordinal);
        Assert.Contains("/erp/einvoice-documents-app", page, StringComparison.Ordinal);
        Assert.Contains("/erp/tax-external-reporting-app", page, StringComparison.Ordinal);
        Assert.Contains("PhpErpDesktopChrome", chrome, StringComparison.Ordinal);
        Assert.Contains("PhpCpDesktopChrome", chrome, StringComparison.Ordinal);
        Assert.Contains("\"/erp/uae-tax-compliance-app\"", chrome, StringComparison.Ordinal);

        var adjustments = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Erp",
            "ErpUaeTaxSaveCtAdjustmentsWriteService.cs"));
        foreach (var field in new[]
        {
            "non_deductible_entertainment",
            "fines_penalties",
            "book_depreciation_excess",
            "related_party_adjustments",
            "other_add_backs",
            "exempt_income",
            "foreign_branch_exemption",
            "loss_carryforward",
            "qualifying_donations",
            "other_deductions",
        })
        {
            Assert.Contains("(\"" + field + "\"", adjustments, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ErpUserControlDefinesCapabilityGroupsAndStandardActions()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Pages",
            "ErpUserControlApp.razor"));
        var catalog = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Presentation",
            "ErpCapabilityCatalog.cs"));
        var chrome = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Shared",
            "Desktop",
            "PhpErpDesktopChrome.razor"));

        Assert.Contains("@page \"/erp/user-control-app\"", page, StringComparison.Ordinal);
        Assert.Contains("role → capability → area/action mappings", page, StringComparison.Ordinal);
        Assert.Contains("@inject IErpPermissionScopeReadService PermissionScopes", page, StringComparison.Ordinal);
        Assert.Contains("Company/site scope and approval controls", page, StringComparison.Ordinal);
        Assert.Contains("FormatWindow", page, StringComparison.Ordinal);
        foreach (var group in new[] { "Finance", "Purchasing", "Sales", "Inventory", "Projects / fit-out", "Jewellery", "HR / payroll", "Administration" })
        {
            Assert.Contains("new(\"" + group, catalog, StringComparison.Ordinal);
        }

        foreach (var action in new[] { "View", "New", "Edit", "Delete", "Void", "Submit", "Approve", "Reject", "Post", "Reverse", "Print", "Export" })
        {
            Assert.Contains("\"" + action + "\"", catalog, StringComparison.Ordinal);
        }

        Assert.Contains("<a href=\"/erp/user-control-app\">User control</a>", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void ErpCapabilityPolicySeparatesStandardAndPrivilegedActions()
    {
        var tenant = new LegacySessionContext(
            LegacySessionKind.Admin,
            10,
            "session",
            [EcomAePermissions.TenantErpAccess]);
        var delegatedDelete = tenant with
        {
            ModuleAcl = [new ModuleAclEntry(7, "Sales Delete", false)],
        };
        var delegatedAdministration = tenant with
        {
            ModuleAcl = [new ModuleAclEntry(8, "Administration Approve", false)],
        };
        var super = tenant with
        {
            Permissions = [EcomAePermissions.SuperErpAccess],
        };

        Assert.True(ErpCapabilityCatalog.CanAction(tenant, "sales", "New"));
        Assert.False(ErpCapabilityCatalog.CanAction(tenant, "sales", "Delete"));
        Assert.True(ErpCapabilityCatalog.CanAction(delegatedDelete, "sales", "Delete"));
        Assert.True(ErpCapabilityCatalog.CanAction(super, "finance", "Reverse"));
        Assert.False(ErpCapabilityCatalog.CanAction(tenant, "administration", "Approve"));
        Assert.True(ErpCapabilityCatalog.CanAction(delegatedAdministration, "administration", "Approve"));
        Assert.True(ErpCapabilityCatalog.CanAction(super, "administration", "Approve"));
    }

    [Fact]
    public void ErpRbacWritesRecordAuditableActorAndScopeEvidence()
    {
        var root = FindRepoRoot();
        var erpRoot = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Erp");
        foreach (var service in new[]
        {
            "ErpRbacPrivSaveWriteService.cs",
            "ErpRbacDutySaveWriteService.cs",
            "ErpRbacRoleSaveWriteService.cs",
            "ErpRbacDutyPrivWriteService.cs",
            "ErpRbacRoleDutyWriteService.cs",
            "ErpRbacUserRoleWriteService.cs",
        })
        {
            var source = File.ReadAllText(Path.Combine(erpRoot, service));
            Assert.Contains("IErpAuditLogWriter", source, StringComparison.Ordinal);
            Assert.Contains("ActorUserId", source, StringComparison.Ordinal);
        }

        var module = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Modules",
            "ErpModule.cs"));
        Assert.Contains("session.UserId)", module, StringComparison.Ordinal);
        Assert.Contains("rbac_user_role_assign", File.ReadAllText(Path.Combine(
            erpRoot,
            "ErpRbacUserRoleWriteService.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void ErpUserControlProjectsRbacAuditHistory()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Components",
            "Pages",
            "ErpUserControlApp.razor"));
        var service = File.ReadAllText(Path.Combine(
            root,
            "aspnet",
            "src",
            "EcomAE.Platform",
            "Erp",
            "ErpRbacAuditReadService.cs"));

        Assert.Contains("RBAC change history", page, StringComparison.Ordinal);
        Assert.Contains("RbacAudit.ListAsync", page, StringComparison.Ordinal);
        Assert.Contains("WHERE `entity_type` LIKE 'rbac_%'", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_audit_log", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ErpScopedPolicyEnforcesDatesCompanySiteLimitsAndDelegation()
    {
        var session = new LegacySessionContext(
            LegacySessionKind.Admin,
            25,
            "session",
            [EcomAePermissions.TenantErpAccess],
            ModuleAcl: [new ModuleAclEntry(8, "Finance Approve", false)]);
        var now = DateTimeOffset.UtcNow;
        var grants = new[]
        {
            new ErpPermissionGrant(
                25,
                "finance",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Approve" },
                new HashSet<long> { 7 },
                new HashSet<long> { 3 },
                1000m,
                now.AddMinutes(-1),
                now.AddMinutes(1)),
        };

        Assert.True(ErpPermissionScopePolicy.Evaluate(
            session,
            "finance",
            "Approve",
            new ErpPermissionRequest(7, 3, 500, now),
            grants,
            []).Allowed);
        Assert.Equal("approval_limit", ErpPermissionScopePolicy.Evaluate(
            session,
            "finance",
            "Approve",
            new ErpPermissionRequest(7, 3, 1500, now),
            grants,
            []).ReasonCode);
        Assert.Equal("company_site_scope", ErpPermissionScopePolicy.Evaluate(
            session,
            "finance",
            "Approve",
            new ErpPermissionRequest(8, 3, 500, now),
            grants,
            []).ReasonCode);
        Assert.Equal("effective_window", ErpPermissionScopePolicy.Evaluate(
            session,
            "finance",
            "Approve",
            new ErpPermissionRequest(7, 3, 500, now.AddDays(2)),
            grants,
            []).ReasonCode);

        var delegated = session with { UserId = 26 };
        var delegation = new ErpPermissionDelegation(
            25,
            26,
            "finance",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Approve" },
            new HashSet<long> { 7 },
            new HashSet<long> { 3 },
            750m,
            now.AddMinutes(-1),
            now.AddMinutes(1));
        Assert.True(ErpPermissionScopePolicy.Evaluate(
            delegated,
            "finance",
            "Approve",
            new ErpPermissionRequest(7, 3, 500, now),
            [],
            [delegation]).Allowed);
    }

    [Fact]
    public void ErpScopedPermissionReadServiceDefinesAdditiveTenantSchema()
    {
        var service = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Erp/ErpPermissionScopeReadService.cs"));

        Assert.Contains("epc_erp_permission_grant", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_permission_delegation", service, StringComparison.Ordinal);
        Assert.Contains("effective_from", service, StringComparison.Ordinal);
        Assert.Contains("effective_to", service, StringComparison.Ordinal);
        Assert.Contains("approval_limit", service, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS", service, StringComparison.Ordinal);
        Assert.Contains("IErpPermissionScopeReadService", File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void InvoiceWriteEndpointUsesPersistedScopedPermissionDecision()
    {
        var module = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));

        Assert.Contains("IErpPermissionScopeReadService permissionScopes", module, StringComparison.Ordinal);
        Assert.Contains("permissionScopes.ListForUserAsync", module, StringComparison.Ordinal);
        Assert.Contains("ErpPermissionScopePolicy.Evaluate", module, StringComparison.Ordinal);
        Assert.Contains("body.CompanyId, body.SiteId", module, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status403Forbidden", module, StringComparison.Ordinal);
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
