using EcomAE.Platform.Services;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Super CP Cloud / on-premises / hybrid ERP matrix. Honest: Cloud is live,
/// on-premises uses the PHP pack plus license digest, Hybrid is a registered
/// kind without a live sync agent.
/// </summary>
public static class CpDeploymentModelsCatalog
{
    public sealed record Model(
        string Key,
        TenantDeploymentKind Kind,
        string Title,
        string Status,
        string StatusClass,
        string Summary,
        string WhereToOpen,
        string[] Lives,
        string[] Missing);

    public sealed record MatrixRow(string Capability, string Cloud, string OnPremises, string Hybrid);

    public static readonly IReadOnlyList<Model> Models =
    [
        new(
            "cloud",
            TenantDeploymentKind.Cloud,
            "On cloud (SaaS)",
            "Live",
            "label-success",
            "Platform hosts the database and ERP on www.ecomae.com. Commerce shops and ERP-only companies (ASAP / INDJ / FITO / REAL) are this model.",
            "/cp/tenants-app",
            [
                "Dedicated shop DB per ERP-only company",
                "Super CP tenant control and Client ERP logins",
                "Industry packs (jewellery, auto parts, fit-out, real estate)",
            ],
            [
                "Per-tenant ERP version pinning (roadmap 6.1)",
            ]),
        new(
            "on-premises",
            TenantDeploymentKind.OnPremises,
            "On premises",
            "PHP pack live · ASP.NET scaffold",
            "label-warning",
            "Customer runs ERP on their own server. Super CP issues a license and setup manifest. The PHP installer remains authoritative.",
            "/erp/on-premises-app",
            [
                "deploy/on-premises install, Docker, setup-wizard, backup, health-check",
                "Signed license activate + /erp/on-premises-app digest",
                "TenantInstallationControlPlane manifest (no DB password in the package)",
            ],
            [
                "ASP.NET installer pack is scaffold only (deploy/on-premises-aspnet)",
                "Durable enrollment / heartbeat persistence",
            ]),
        new(
            "hybrid",
            TenantDeploymentKind.Hybrid,
            "Hybrid",
            "Kind registered · sync agent not live",
            "label-default",
            "On-site ERP plus cloud Super CP. Outbound HTTPS from the site; masters in the cloud, transactions on site; conflicts queued — never silently overwritten.",
            "/cp/deployment-models-app",
            [
                "TenantDeploymentKind.Hybrid on the control plane",
                "Sync envelope shape (cloud-to-hybrid / hybrid-to-cloud)",
            ],
            [
                "Live sync agent and change-log cursor",
                "Conflict review queue",
                "Feature matrix enforced per tenant",
            ]),
    ];

    public static readonly IReadOnlyList<MatrixRow> Matrix =
    [
        new("Who hosts the database", "Platform", "Customer", "Both (split ownership)"),
        new("Staff login URL", "/cp/client-erp/{key}/ or tenant host", "Customer hostname + license", "Customer hostname + Super CP link"),
        new("Super CP fleet control", "Yes", "License / health only", "Yes + sync status"),
        new("Installer package", "Not used", "Required", "Required"),
        new("Storefront", "Optional (ERP-only has none)", "Optional", "Optional"),
        new("Sync / conflicts", "n/a", "Heartbeat only", "Changelog + queued conflicts"),
        new("Product status", "Live", "PHP pack", "Contract only"),
    ];

    public static string Classify(string? tenantType, string? hostedOn, string? storedKind)
    {
        var stored = NormalizeKind(storedKind);
        if (stored.Length > 0)
        {
            return stored;
        }

        var host = (hostedOn ?? string.Empty).Trim().ToLowerInvariant();
        if (host is "onpremises" or "on-premises" or "on_prem")
        {
            return "on-premises";
        }

        if (host is "hybrid")
        {
            return "hybrid";
        }

        return "cloud";
    }

    public static string NormalizeKind(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-');
        return raw switch
        {
            "cloud" or "saas" or "on-cloud" => "cloud",
            "on-premises" or "onpremises" or "on-prem" => "on-premises",
            "hybrid" => "hybrid",
            _ => string.Empty,
        };
    }

    public static TenantDeploymentKind ParseKind(string? value) => NormalizeKind(value) switch
    {
        "on-premises" => TenantDeploymentKind.OnPremises,
        "hybrid" => TenantDeploymentKind.Hybrid,
        _ => TenantDeploymentKind.Cloud,
    };
}
