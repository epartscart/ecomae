namespace EcomAE.Platform.LifeOs.Spec;

/// <summary>
/// Canonical LifeOS™ / IP frontend + backend link inventory for CP operators.
/// Paths are product-primary ASP.NET routes (Super-CP + lifeos.ecomae.com).
/// </summary>
public static class LifeOsLinkCatalog
{
    public sealed record LinkRow(
        string Kind,
        int Part,
        string Title,
        string Path,
        string Method,
        string HostHint,
        string Chapters);

    public sealed record HostRow(string Host, string EntryPath, string Role, string Access);

    /// <summary>Public hosts and Super-CP entry points.</summary>
    public static IReadOnlyList<HostRow> Hosts { get; } =
    [
        new("https://lifeos.ecomae.com/", "/lifeos", "LifeOS™ customer product home", "Public product host → ASP.NET /lifeos"),
        new("https://www.ecomae.com/lifeos", "/lifeos", "LifeOS™ preview on Super-CP host", "Super-CP www preview"),
        new("https://www.ecomae.com/ip", "/ip", "Intelligence Platform ecosystem hub", "Super-CP only (tenant 404)"),
        new("https://www.ecomae.com/ip/login", "/ip/login", "IP operator login (shared Super-CP bridge)", "Super-CP only"),
        new("https://www.ecomae.com/cp/lifeos-guide-app", "/cp/lifeos-guide-app", "CP visual system guide (this page)", "CP admin chrome"),
        new("https://www.ecomae.com/bos", "/bos", "Business Operating System fleet", "Super-CP only"),
        new("https://www.ecomae.com/cp", "/cp", "Control Panel command centre", "CP admin"),
        new("https://www.ecomae.com/erp", "/erp", "ERP shell under BOS", "ERP admin"),
    ];

    /// <summary>Blazor / HTML frontend consoles (UI).</summary>
    public static IReadOnlyList<LinkRow> FrontendApps { get; } =
    [
        new("frontend", 1, "LifeOS home infographic", "/lifeos", "GET", "lifeos.ecomae.com + www", "Purpose · Mission · Principles · Product · Cognitive Model · Brain"),
        new("frontend", 1, "LifeOS legal footer (PHP parity)", "https://www.ecomae.com/legal", "GET", "www canonical · linked from lifeos footer", "All policies · Privacy · Terms · Cookies · Copyright · …"),
        new("frontend", 1, "LifeOS login (personal gate)", "/lifeos/login", "GET", "www + lifeos", "Sign in before join / companion / results"),
        new("frontend", 1, "Join & install companion", "/lifeos/join", "GET", "www + lifeos", "Public join for new users · separate from /lifeos/login"),
        new("frontend", 1, "Mobile companion PWA", "/lifeos/mobile", "GET", "www + lifeos", "Track · Talk · Listen · Guide · login required"),
        new("frontend", 1, "My results (client self-serve)", "/lifeos/results", "GET", "www + lifeos", "Discussions + tracking · signed-in · clientId + token"),
        new("frontend", 1, "Joined clients board", "/lifeos/clients-board", "GET", "www + lifeos", "Operator/personal login · country · device · activity"),
        new("frontend", 1, "CP joined clients console", "/cp/lifeos-clients-app", "GET", "www CP", "Same board under /cp · admin login"),
        new("frontend", 1, "24/7 Daily Human Routine Matrix", "/lifeos/routine", "GET", "www + lifeos", "Purpose coverage · morning→sleep · cloned voice samples"),
        new("frontend", 1, "How it works sample demo", "/lifeos/demo-app", "GET", "www + lifeos", "Perceive→Decide→Act→Learn with sample data"),
        new("frontend", 1, "Cinematic launch film", "/lifeos/cinematic-app", "GET", "www + lifeos", "3:00 keyframe storyboard · 4K60 bible"),
        new("frontend", 1, "LifeOS console / brain / login", "/lifeos/app", "GET", "www + lifeos", "Brain engines UI · operator console"),
        new("frontend", 1, "LifeOS brain alias", "/lifeos/brain", "GET", "www + lifeos", "Nine brain engines"),
        new("frontend", 1, "IP ecosystem hub", "/ip", "GET", "www Super-CP", "IP over BOS + ambient OS"),
        new("frontend", 1, "IP app alias", "/ip/app", "GET", "www Super-CP", "Same hub"),
        new("frontend", 1, "IP ecosystem map", "/ip/ecosystem", "GET", "www Super-CP", "Ecosystem map"),
        new("frontend", 1, "IP ecosystem app", "/ip/ecosystem-app", "GET", "www Super-CP", "Ecosystem map"),
        new("frontend", 1, "IP login", "/ip/login", "GET", "www Super-CP", "Shared Super-CP credentials"),
        new("frontend", 1, "CP LifeOS system guide", "/cp/lifeos-guide-app", "GET", "www CP", "Full visual + chapter + link catalog"),
        new("frontend", 2, "Part 2 architecture console", "/lifeos/architecture-app", "GET", "www + lifeos", "Orchestrator · Event Bus · Context · Memory · Agents · Planning"),
        new("frontend", 2, "Orchestrator console", "/lifeos/orchestrator-app", "GET", "www + lifeos", "Orchestrator"),
        new("frontend", 2, "Memory console", "/lifeos/memory-app", "GET", "www + lifeos", "Memory System"),
        new("frontend", 2, "Agents console", "/lifeos/agents-app", "GET", "www + lifeos", "Multi-Agent"),
        new("frontend", 2, "Planning console", "/lifeos/planning-app", "GET", "www + lifeos", "Planning Engine"),
        new("frontend", 3, "Part 3 cognitive console", "/lifeos/cognitive-app", "GET", "www + lifeos", "AI Core · Perception · Reasoning · Decision · Ethics · Cycle"),
        new("frontend", 4, "Part 4 multimodal console", "/lifeos/multimodal-app", "GET", "www + lifeos", "Runtime · Devices · Voice · Vision · Sync · Notifications"),
        new("frontend", 5, "Part 5 platform console", "/lifeos/platform-app", "GET", "www + lifeos", "Microservices · API · Data · SDK · AI Gateway"),
        new("frontend", 6, "Part 6 infra console", "/lifeos/infra-app", "GET", "www + lifeos", "K8s · CI/CD · GPU · Backup · SRE · Readiness"),
        new("frontend", 7, "Part 7 security console", "/lifeos/security-app", "GET", "www + lifeos", "Zero Trust · IAM · Privacy · AI Governance · SOC"),
        new("frontend", 8, "Part 8 clients console", "/lifeos/clients-app", "GET", "www + lifeos", "Design System · Workspace · Modality · Continuity"),
        new("frontend", 9, "Part 9 plugins console", "/lifeos/plugins-app", "GET", "www + lifeos", "Marketplace · Agent Store · Developer Portal · Partners"),
        new("frontend", 10, "Part 10 roadmap console", "/lifeos/roadmap-app", "GET", "www + lifeos", "Mission · Vision 2035 · Phases · Portfolio · Ch.151–168"),
        new("frontend", 10, "Master Spec Parts 1–10 UI", "/lifeos/spec", "GET", "www + lifeos", "All parts registry + roadmap (/lifeos/spec-app alias)"),
    ];

    /// <summary>JSON / POST backend digests (API surfaces from master spec + auth).</summary>
    public static IReadOnlyList<LinkRow> BackendFromSpec(ILifeOsMasterSpec spec) =>
        spec.ApiSurfaces
            .Select(a => new LinkRow(
                "backend",
                InferPart(a.Path, a.Purpose),
                a.Purpose,
                a.Path,
                a.Method,
                "www + lifeos (JSON)",
                ChapterHint(a.Purpose)))
            .Concat(
            [
                new LinkRow("backend", 1, "LifeOS logout", "/lifeos/logout", "POST/GET", "www + lifeos", "Session end"),
                new LinkRow("backend", 1, "IP logout", "/ip/logout", "POST/GET", "www Super-CP", "Session end"),
                new LinkRow("backend", 1, "Client directory digest", "/lifeos/directory", "GET", "www + lifeos", "Join directory · test client"),
                new LinkRow("backend", 1, "Companion session", "/lifeos/companion", "GET", "www + lifeos", "Mobile session"),
                new LinkRow("backend", 1, "Companion track", "/lifeos/companion/track", "POST", "www + lifeos", "Tracking events"),
                new LinkRow("backend", 1, "Companion talk", "/lifeos/companion/talk", "POST", "www + lifeos", "Talk / guide reply"),
            ])
            .OrderBy(r => r.Part)
            .ThenBy(r => r.Path, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<LinkRow> All(ILifeOsMasterSpec spec) =>
        FrontendApps.Concat(BackendFromSpec(spec)).ToArray();

    private static int InferPart(string path, string purpose)
    {
        if (path.Contains("cognitive", StringComparison.Ordinal)
            || path.Contains("perception", StringComparison.Ordinal)
            || path.Contains("prediction", StringComparison.Ordinal)
            || path.Contains("ethics", StringComparison.Ordinal))
            return 3;
        if (path.Contains("multimodal", StringComparison.Ordinal)
            || path.Contains("runtime", StringComparison.Ordinal)
            || path.Contains("devices", StringComparison.Ordinal)
            || path.Contains("notifications", StringComparison.Ordinal)
            || path.Contains("sync", StringComparison.Ordinal)
            || path.Contains("performance", StringComparison.Ordinal))
            return 4;
        if (path.Contains("platform", StringComparison.Ordinal)
            || path.Contains("services", StringComparison.Ordinal)
            || path.Contains("api-catalog", StringComparison.Ordinal)
            || path.Contains("event-topics", StringComparison.Ordinal)
            || path.Contains("data-stores", StringComparison.Ordinal)
            || path.Contains("knowledge", StringComparison.Ordinal)
            || path.Contains("agent-sdk", StringComparison.Ordinal)
            || path.Contains("ai-gateway", StringComparison.Ordinal))
            return 5;
        if (path.Contains("infra", StringComparison.Ordinal)
            || path.Contains("kubernetes", StringComparison.Ordinal)
            || path.Contains("cicd", StringComparison.Ordinal)
            || path.Contains("gpu", StringComparison.Ordinal)
            || path.Contains("backup", StringComparison.Ordinal)
            || path.Contains("observability", StringComparison.Ordinal)
            || path.Contains("sre", StringComparison.Ordinal)
            || path.Contains("readiness", StringComparison.Ordinal))
            return 6;
        if (path.Contains("security", StringComparison.Ordinal)
            || path.Contains("zero-trust", StringComparison.Ordinal)
            || path.Contains("iam", StringComparison.Ordinal)
            || path.Contains("authorization", StringComparison.Ordinal)
            || path.Contains("encryption", StringComparison.Ordinal)
            || path.Contains("privacy", StringComparison.Ordinal)
            || path.Contains("governance", StringComparison.Ordinal)
            || path.Contains("threat", StringComparison.Ordinal)
            || path.Contains("enterprise-deploy", StringComparison.Ordinal))
            return 7;
        if (path.Contains("clients", StringComparison.Ordinal)
            || path.Contains("design-system", StringComparison.Ordinal)
            || path.Contains("workspace", StringComparison.Ordinal)
            || path.Contains("modality", StringComparison.Ordinal)
            || path.Contains("continuity", StringComparison.Ordinal)
            || path.Contains("personalization", StringComparison.Ordinal)
            || path.Contains("ux-metrics", StringComparison.Ordinal))
            return 8;
        if (path.Contains("plugins", StringComparison.Ordinal)
            || path.Contains("marketplace", StringComparison.Ordinal)
            || path.Contains("agent-store", StringComparison.Ordinal)
            || path.Contains("developer", StringComparison.Ordinal)
            || path.Contains("billing", StringComparison.Ordinal)
            || path.Contains("partners", StringComparison.Ordinal)
            || path.Contains("ecosystem", StringComparison.Ordinal))
            return 9;
        if (path.Contains("roadmap", StringComparison.Ordinal) || purpose.Contains("Part 10", StringComparison.Ordinal))
            return 10;
        if (path.Contains("spec", StringComparison.Ordinal))
            return 10;
        return 2;
    }

    private static string ChapterHint(string purpose)
    {
        var i = purpose.IndexOf("Ch.", StringComparison.Ordinal);
        return i >= 0 ? purpose[i..] : purpose;
    }
}
