namespace EcomAE.Platform.Presentation;

/// <summary>
/// Typed twin of PHP <c>epc_portal_industries()</c> (content/general_pages/epc_portal.php): the tenant-onboardable
/// industry list with its display name, ecosystem, Font Awesome icon and theme colours. CP/ERP chrome reads the
/// label and icon from here instead of guessing per host, so every industry (jewellery, fashion, electronics,
/// construction/fit-out, …) renders exactly as the PHP reference does.
/// </summary>
public sealed record PhpPortalIndustry(
    string Code,
    string Name,
    string Ecosystem,
    string Icon,
    string Primary,
    string PrimaryDark,
    string Accent);

public static class PhpPortalIndustryCatalog
{
    private static readonly PhpPortalIndustry[] Rows =
    [
        new("auto_parts", "Auto spare parts", "commerce", "fa-car", "#dc2626", "#991b1b", "#f97316"),
        new("automotive_full_vehicles", "Vehicle sales & showrooms", "commerce", "fa-car", "#b91c1c", "#7f1d1d", "#f59e0b"),
        new("electronics", "Electronics & gadgets", "commerce", "fa-microchip", "#e10a0a", "#b00808", "#000000"),
        new("it_hardware_accessories", "IT hardware & accessories", "commerce", "fa-laptop", "#2563eb", "#1d4ed8", "#38bdf8"),
        new("fashion", "Fashion & apparel", "commerce", "fa-shopping-bag", "#be185d", "#9d174d", "#ec4899"),
        new("jewellery", "Jewellery & luxury goods", "commerce", "fa-diamond", "#b45309", "#92400e", "#fbbf24"),
        new("medical", "Medical supplies", "commerce", "fa-medkit", "#0284c7", "#0369a1", "#22d3ee"),
        new("pharmacy_retail", "Pharmacy & drugstore", "commerce", "fa-plus-square", "#0d9488", "#0f766e", "#5eead4"),
        new("nutrition_supplements", "Food supplements & nutrition", "commerce", "fa-leaf", "#15803d", "#166534", "#86efac"),
        new("food_beverage", "Food & beverage / restaurants", "commerce", "fa-cutlery", "#ea580c", "#c2410c", "#fb923c"),
        new("grocery_retail", "Grocery & supermarket", "commerce", "fa-shopping-cart", "#16a34a", "#15803d", "#facc15"),
        new("furniture_interiors", "Furniture & interiors shop", "commerce", "fa-home", "#92400e", "#78350f", "#d6d3d1"),
        new("building_materials", "Building materials trading", "commerce", "fa-cubes", "#78716c", "#57534e", "#f59e0b"),
        new("fmcg_wholesale", "FMCG & general trading", "commerce", "fa-truck", "#0369a1", "#075985", "#38bdf8"),
        new("industrial_equipment", "Industrial equipment trading", "commerce", "fa-cogs", "#475569", "#334155", "#94a3b8"),
        new("agricultural_products", "Agriculture & farm products", "commerce", "fa-pagelines", "#65a30d", "#4d7c0f", "#a3e635"),
        new("perfume_cosmetics", "Perfume & cosmetics trading", "commerce", "fa-magic", "#db2777", "#be185d", "#f9a8d4"),
        new("pet_services", "Pet shop & animal services", "commerce", "fa-paw", "#d97706", "#b45309", "#fcd34d"),
        new("health", "Health & wellness", "lifestyle_consumer", "fa-heartbeat", "#16a34a", "#15803d", "#4ade80"),
        new("beauty_skincare", "Beauty salon & skincare", "lifestyle_consumer", "fa-female", "#e11d48", "#be123c", "#fb7185"),
        new("fitness_training", "Gym & fitness centres", "lifestyle_consumer", "fa-heartbeat", "#dc2626", "#b91c1c", "#fbbf24"),
        new("hospitality_travel", "Hospitality, hotels & travel", "lifestyle_consumer", "fa-building", "#0ea5e9", "#0284c7", "#fbbf24"),
        new("clinics_telemedicine", "Clinics & telemedicine", "lifestyle_consumer", "fa-stethoscope", "#0891b2", "#0e7490", "#67e8f9"),
        new("tax_advisory", "Tax & advisory", "business_services", "fa-balance-scale", "#0d9488", "#0f766e", "#14b8a6"),
        new("consultancy", "Consultancy", "business_services", "fa-briefcase", "#7c3aed", "#6d28d9", "#a78bfa"),
        new("legal_services", "Legal services", "business_services", "fa-gavel", "#1e3a8a", "#1e40af", "#93c5fd"),
        new("accounting_auditing", "Accounting & auditing", "business_services", "fa-calculator", "#0f766e", "#115e59", "#5eead4"),
        new("hr_recruitment", "HR & recruitment", "business_services", "fa-users", "#7c3aed", "#6d28d9", "#c4b5fd"),
        new("marketing_digital", "Digital marketing & media agency", "business_services", "fa-bullhorn", "#db2777", "#be185d", "#f472b6"),
        new("education_training", "Education & training", "business_services", "fa-graduation-cap", "#4f46e5", "#4338ca", "#a5b4fc"),
        new("cleaning_facilities", "Cleaning & facilities management", "business_services", "fa-paint-brush", "#059669", "#047857", "#6ee7b7"),
        new("security_services", "Security & safety services", "business_services", "fa-shield", "#1e293b", "#0f172a", "#38bdf8"),
        new("printing_signage", "Printing & signage", "business_services", "fa-print", "#ea580c", "#c2410c", "#fdba74"),
        new("construction_contracting", "Construction & contracting", "business_services", "fa-building", "#b45309", "#92400e", "#fbbf24"),
        new("logistics_freight", "Logistics & freight", "business_services", "fa-ship", "#0369a1", "#075985", "#38bdf8"),
        new("financial_services", "Financial services", "business_services", "fa-line-chart", "#047857", "#065f46", "#34d399"),
        new("erp_standalone", "ERP standalone (no storefront)", "business_services", "fa-university", "#0369a1", "#075985", "#0ea5e9"),
        new("nonprofit_government", "Non-profit & government", "business_services", "fa-university", "#334155", "#1e293b", "#94a3b8"),
        new("rental", "Rental & leasing", "asset_sharing", "fa-key", "#ca8a04", "#a16207", "#facc15"),
        new("vehicle_leasing", "Vehicle leasing & rental", "asset_sharing", "fa-car", "#ca8a04", "#a16207", "#fde047"),
        new("machinery_rental", "Machinery & equipment rental", "asset_sharing", "fa-wrench", "#78716c", "#57534e", "#fbbf24"),
        new("it_services_saas_support", "IT services & SaaS", "digital_technology", "fa-cloud", "#2563eb", "#1d4ed8", "#60a5fa"),
        new("media_entertainment", "Media & entertainment", "digital_technology", "fa-film", "#c026d3", "#a21caf", "#e879f9"),
        new("energy_utilities", "Energy & utilities", "digital_technology", "fa-bolt", "#eab308", "#ca8a04", "#fef08a"),
        new("manufacturing_industrial", "Manufacturing & industrial", "digital_technology", "fa-industry", "#64748b", "#475569", "#cbd5e1"),
        new("platform_host", "Platform host (ecomae)", "platform", "fa-cloud", "#0ea5e9", "#0284c7", "#38bdf8"),
    ];

    private static readonly Dictionary<string, PhpPortalIndustry> ByCode =
        Rows.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PhpPortalIndustry> All => Rows;

    /// <summary>PHP <c>epc_portal_industry($code)</c>: unknown codes fall back to <c>auto_parts</c>.</summary>
    public static PhpPortalIndustry Resolve(string? code)
        => !string.IsNullOrWhiteSpace(code) && ByCode.TryGetValue(code.Trim(), out var row)
            ? row
            : ByCode["auto_parts"];

    public static string LabelFor(string? code) => Resolve(code).Name;

    /// <summary>Bare Font Awesome icon name (PHP renders it as <c>fa &lt;icon&gt;</c>).</summary>
    public static string IconFor(string? code) => Resolve(code).Icon;

    public static bool Knows(string? code)
        => !string.IsNullOrWhiteSpace(code) && ByCode.ContainsKey(code.Trim());
}
