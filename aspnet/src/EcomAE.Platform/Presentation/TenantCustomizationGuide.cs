namespace EcomAE.Platform.Presentation;

/// <summary>
/// Step-by-step graphical walkthroughs for tenant-facing customization areas.
/// Each area renders a colorful step track plus a mini visual preview of where
/// the setting surfaces in CP / ERP / storefront chrome.
/// </summary>
public static class TenantCustomizationGuide
{
    public sealed record Step(int Number, string Icon, string Label, string Detail, string? Href);
    public sealed record Area(
        string Slug,
        string Title,
        string Intro,
        string PreviewKind,
        string PreviewCaption,
        IReadOnlyList<Step> Steps);

    public static readonly IReadOnlyDictionary<string, Area> Areas =
        new Dictionary<string, Area>(StringComparer.OrdinalIgnoreCase)
        {
            ["design-tokens"] = new(
                "design-tokens",
                "Brand & theme tokens",
                "Tenant-controlled presentation settings: branding, dashboard style, density and labels. Saved tokens repaint this tenant's CP and ERP chrome within about a minute.",
                "theme",
                "Preview: your colors and logo repaint the CP / ERP / storefront chrome like this.",
                new List<Step>
                {
                    new(1, "fa-paint-brush", "Pick a setting key", "Choose what to change — brand color, accent, density, labels — from the allowed key list.", null),
                    new(2, "fa-eyedropper", "Enter the value", "Type the token value (e.g. a hex color or layout flag). Only allowlisted keys are writable; platform contracts stay locked.", null),
                    new(3, "fa-floppy-o", "Save", "Submit with confirmWrites — the token is stored against your tenant (and industry/super defaults still cascade under it).", null),
                    new(4, "fa-television", "See it live", "Open any CP or ERP page — the chrome repaints in ~1 minute. No deploy, no code.", "/cp/app"),
                }),
            ["portal-settings"] = new(
                "portal-settings",
                "Portal settings",
                "Tenant-level portal configuration: the switches that shape how your Control Panel behaves for your staff and customers.",
                "settings",
                "Preview: each toggle lands on a real CP behavior — nothing is cosmetic-only.",
                new List<Step>
                {
                    new(1, "fa-list", "Review the settings list", "Every row is a live tenant setting with its current value.", null),
                    new(2, "fa-pencil", "Edit a value", "Change what your tenant needs — values are scoped to this tenant only.", null),
                    new(3, "fa-floppy-o", "Save", "Writes are gated by confirmWrites so nothing changes silently.", null),
                    new(4, "fa-check-circle", "Verify in CP", "Open the affected CP area to confirm the behavior you set.", "/cp/app"),
                }),
            ["menus"] = new(
                "menus",
                "Menus & navigation",
                "Build and order the menus your users see — CP side and storefront side — with items, parameters and ordering.",
                "menu",
                "Preview: menu items you create here appear in the navigation tree like this.",
                new List<Step>
                {
                    new(1, "fa-bars", "Pick a menu", "Choose the storefront (site) menu or a Control Panel menu to edit.", null),
                    new(2, "fa-plus", "Add menu items", "Create items with labels and links; set parameters per item.", null),
                    new(3, "fa-sort", "Order & nest", "Arrange order and hierarchy — the top menu follows your ordering.", null),
                    new(4, "fa-eye", "Preview live", "Open the storefront or CP top menu to see the items you published.", "/storefront/app"),
                }),
            ["industry-settings"] = new(
                "industry-settings",
                "Industry settings",
                "Enable the industry pack for your tenant: navigation, workspaces and terminology tailored to your vertical.",
                "industry",
                "Preview: the industry pack re-shapes ERP navigation and workspaces.",
                new List<Step>
                {
                    new(1, "fa-industry", "Choose your industry", "Select the pack that matches your business (auto-parts, jewellery, fit-out, real-estate…).", null),
                    new(2, "fa-toggle-on", "Enable features", "Turn on the modules and terminology the pack ships.", null),
                    new(3, "fa-floppy-o", "Save", "The pack applies to your tenant only — layered under super and industry defaults.", null),
                    new(4, "fa-th-large", "Check the workspace", "Open ERP — your industry workspace and menu entries are now visible.", "/erp"),
                }),
            ["custom-fields"] = new(
                "custom-fields",
                "Custom fields",
                "Define your own request fields: labels, types, required flags and parameters — no code needed.",
                "fields",
                "Preview: fields you define render on the request forms like this.",
                new List<Step>
                {
                    new(1, "fa-plus-square", "Add a field", "Create a field key, label and type in the request-fields list.", null),
                    new(2, "fa-sliders", "Set parameters", "Required, order, options — per-field parameters live on the field.", null),
                    new(3, "fa-floppy-o", "Save", "Saved fields apply to this tenant's forms immediately.", null),
                    new(4, "fa-wpforms", "See it on the form", "Open the request form — your field appears with your label.", null),
                }),
            ["tenant-config"] = new(
                "tenant-config",
                "Tenant configuration & security",
                "ERP settings plus the security model: roles, duties and privileges that control every area down to field level.",
                "security",
                "Preview: roles → duties → privileges chain controls what each user sees and can do.",
                new List<Step>
                {
                    new(1, "fa-cogs", "Review ERP settings", "Tenant-level ERP switches live in the settings panel.", "/erp/tenant-config-app?tab=erp_setup"),
                    new(2, "fa-user-shield", "Create a role", "Define a role for a job function — e.g. 'Sales clerk', 'Store keeper'.", "/erp/tenant-config-app?tab=security_roles"),
                    new(3, "fa-link", "Attach duties & privileges", "Duties bundle tasks; privileges gate each area — down to individual fields (hide/read-only per user or group).", "/erp/tenant-config-app?tab=security_roles"),
                    new(4, "fa-users", "Assign to users", "Assign the role — the user's menus, pages and fields reshape instantly.", "/erp/tenant-config-app?tab=security_roles"),
                }),
            ["mobile-apps"] = new(
                "mobile-apps",
                "Mobile apps (Android / iOS / PWA)",
                "ERP tenant admin controls the ERP mobile app here inside ERP Setup. An ERP-only tenant manages the ERP app; a full tenant manages ERP here and CP from CP → Mobile apps.",
                "settings",
                "Preview: the app config (name, bundle id, deep links, store links) drives the Capacitor/PWA build.",
                new List<Step>
                {
                    new(1, "fa-toggle-on", "Enable the app", "Turn on the mobile app for this tenant and set the display name + bundle id.", null),
                    new(2, "fa-link", "Wire deep links", "Set the API base URL, deep-link scheme and universal link domain for this ERP host.", null),
                    new(3, "fa-floppy-o", "Save", "Saved into the tenant's integrations settings — confirmWrites gated.", null),
                    new(4, "fa-mobile", "Publish", "Build the Capacitor shell (Android/iOS) or install the PWA — the app loads this ERP live.", "/erp/mobile-apps-app"),
                }),
        };

    /// <summary>Cross-links shown under every board so admins can hop between areas.</summary>
    public static readonly IReadOnlyList<(string Slug, string Label, string Href, string Icon)> RelatedLinks =
    [
        ("design-tokens", "Brand & theme", "/cp/design-tokens-app", "fa-paint-brush"),
        ("menus", "Menus", "/cp/menus-app", "fa-bars"),
        ("industry-settings", "Industry pack", "/cp/industry-settings-app", "fa-industry"),
        ("custom-fields", "Custom fields", "/cp/vin-fields-app", "fa-plus-square"),
        ("tenant-config", "Tenant config & security", "/erp/tenant-config-app?tab=security_roles", "fa-user-shield"),
        ("dashboard", "Dashboard layout", "/erp/dashboard-app", "fa-th-large"),
        ("mobile-apps", "Mobile apps", "/erp/mobile-apps-app", "fa-mobile"),
        ("guide", "Full tenant guide", "/erp/tenant-guide", "fa-map-signs"),
    ];
}
