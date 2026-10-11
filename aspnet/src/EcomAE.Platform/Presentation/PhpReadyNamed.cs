using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Next ready named-function helpers. PHP identifiers kept for the inventory:
/// <c>epc_acc_taxonomy</c>, <c>epc_acc_classify</c>, <c>epc_acc_warehouse_regions</c>,
/// <c>docpart_synonym_normalize_brand</c>, <c>docpart_load_manufacturer_synonym_map</c>,
/// <c>docpart_synonym_names_for_brand</c>, <c>docpart_synonym_brands_equivalent</c>,
/// <c>docpart_load_manufacturer_canonical_map</c>, <c>docpart_synonym_canonical_brand</c>,
/// <c>epc_tdp_policy_sections</c>, <c>epc_tdp_render_policy_html</c>,
/// <c>epc_config_group_meta</c>, <c>epc_config_item_frontend_effects</c>,
/// <c>epc_config_item_frontend_effect</c>, <c>epc_config_group_meta_for</c>,
/// <c>epc_config_item_label</c>, <c>epc_order_guide_snapshot</c>,
/// <c>epc_order_guide_checklist</c>.
/// </summary>
public static class PhpReadyNamed
{
    public const string AccessoriesTaxonomyPath = "content/general_pages/epc_accessories_taxonomy.php";
    public const string ManufacturerSynonymsPath = "content/shop/docpart/docpart_manufacturer_synonyms.php";
    public const string TenantDataPolicyPath = "content/general_pages/epc_tenant_data_policy.php";
    public const string ConfigEditMetaPath = "cp/content/control/epc_config_edit_meta.php";
    public const string OrderGuidePath = "content/shop/usefull/epc_order_fulfilment_guide_data.php";
    public const string TdpVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonElement EpcAccTaxonomy()
        => JsonDocument.Parse(AccTaxonomyJson).RootElement.Clone();

    public static Dictionary<string, string> EpcAccClassify(string name, string brand = "")
    {
        var hay = (name + " " + brand).Trim().ToLowerInvariant();
        using var tax = JsonDocument.Parse(AccTaxonomyJson);
        string? bestCat = null;
        string? bestSub = null;
        string? bestCatLabel = null;
        string? bestSubLabel = null;
        var bestScore = 0;
        foreach (var cat in tax.RootElement.EnumerateObject())
        {
            if (cat.Name == "other")
            {
                continue;
            }

            foreach (var sub in cat.Value.GetProperty("subs").EnumerateObject())
            {
                if (!sub.Value.TryGetProperty("keywords", out var keywords))
                {
                    continue;
                }

                foreach (var kwEl in keywords.EnumerateArray())
                {
                    var kw = (kwEl.GetString() ?? "").Trim();
                    if (kw.Length == 0 || hay.IndexOf(kw, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    if (kw.Length > bestScore)
                    {
                        bestScore = kw.Length;
                        bestCat = cat.Name;
                        bestSub = sub.Name;
                        bestCatLabel = cat.Value.GetProperty("label").GetString();
                        bestSubLabel = sub.Value.GetProperty("label").GetString();
                    }
                }
            }
        }

        return bestCat is null
            ? new Dictionary<string, string>
            {
                ["category"] = "other",
                ["subcategory"] = "general",
                ["category_label"] = "Other Parts",
                ["subcategory_label"] = "General"
            }
            : new Dictionary<string, string>
            {
                ["category"] = bestCat,
                ["subcategory"] = bestSub!,
                ["category_label"] = bestCatLabel!,
                ["subcategory_label"] = bestSubLabel!
            };
    }

    public static Dictionary<string, string> EpcAccWarehouseRegions()
        => new(StringComparer.Ordinal)
        {
            ["S-UAE"] = "Dubai / Sharjah stock",
            ["R-UAE"] = "Ras Al Khaimah stock",
            ["RK-UAE"] = "Ras Al Khaimah stock",
            ["FJ-UAE"] = "Fujairah stock",
            ["L-UAE"] = "UAE local stock",
            ["APAI"] = "Catalogue stock"
        };

    public static string DocpartSynonymNormalizeBrand(string? brand)
    {
        brand = (brand ?? "").Replace("\"", "'", StringComparison.Ordinal).Trim();
        return brand.Length == 0 ? "" : brand.ToUpperInvariant();
    }

    public static Dictionary<string, string[]> DocpartLoadManufacturerSynonymMap(
        IReadOnlyList<(int Id, string Name)> manufacturers,
        IReadOnlyList<(int ManufacturerId, string Synonym)> synonyms)
    {
        var groups = new Dictionary<int, Dictionary<string, bool>>();
        foreach (var row in manufacturers)
        {
            var name = DocpartSynonymNormalizeBrand(row.Name);
            if (name.Length == 0)
            {
                continue;
            }

            if (!groups.TryGetValue(row.Id, out var set))
            {
                set = new Dictionary<string, bool>(StringComparer.Ordinal);
                groups[row.Id] = set;
            }

            set[name] = true;
        }

        foreach (var row in synonyms)
        {
            var synonym = DocpartSynonymNormalizeBrand(row.Synonym);
            if (synonym.Length == 0)
            {
                continue;
            }

            if (!groups.TryGetValue(row.ManufacturerId, out var set))
            {
                set = new Dictionary<string, bool>(StringComparer.Ordinal);
                groups[row.ManufacturerId] = set;
            }

            set[synonym] = true;
        }

        var brandToNames = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var namesSet in groups.Values)
        {
            var names = namesSet.Keys.ToArray();
            foreach (var name in names)
            {
                brandToNames[name] = names;
            }
        }

        return brandToNames;
    }

    public static string[] DocpartSynonymNamesForBrand(string? brand, IReadOnlyDictionary<string, string[]> synonymMap)
    {
        var normalized = DocpartSynonymNormalizeBrand(brand);
        if (normalized.Length == 0)
        {
            return [""];
        }

        return synonymMap.TryGetValue(normalized, out var names) ? names.Distinct(StringComparer.Ordinal).ToArray() : [normalized];
    }

    public static bool DocpartSynonymBrandsEquivalent(string? left, string? right, IReadOnlyDictionary<string, string[]> synonymMap)
    {
        left = DocpartSynonymNormalizeBrand(left);
        right = DocpartSynonymNormalizeBrand(right);
        if (left.Length == 0 || right.Length == 0)
        {
            return left == right;
        }

        if (left == right)
        {
            return true;
        }

        var leftNames = synonymMap.TryGetValue(left, out var names) ? names : [left];
        return leftNames.Contains(right, StringComparer.Ordinal);
    }

    public static Dictionary<string, string> DocpartLoadManufacturerCanonicalMap(
        IReadOnlyList<(int Id, string Name)> manufacturers,
        IReadOnlyList<(int ManufacturerId, string Synonym)> synonyms)
    {
        var idToCanonical = new Dictionary<int, string>();
        var brandToCanonical = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in manufacturers)
        {
            var canonical = DocpartSynonymNormalizeBrand(row.Name);
            if (canonical.Length == 0)
            {
                continue;
            }

            idToCanonical[row.Id] = canonical;
            brandToCanonical[canonical] = canonical;
        }

        foreach (var row in synonyms)
        {
            var synonym = DocpartSynonymNormalizeBrand(row.Synonym);
            if (synonym.Length == 0 || !idToCanonical.TryGetValue(row.ManufacturerId, out var canonical))
            {
                continue;
            }

            brandToCanonical[synonym] = canonical;
        }

        return brandToCanonical;
    }

    public static string DocpartSynonymCanonicalBrand(string? brand, IReadOnlyDictionary<string, string> canonicalMap)
    {
        var normalized = DocpartSynonymNormalizeBrand(brand);
        if (normalized.Length == 0)
        {
            return "";
        }

        return canonicalMap.TryGetValue(normalized, out var canonical) ? canonical : normalized;
    }

    public static JsonElement EpcTdpPolicySections()
        => JsonDocument.Parse(TdpSectionsJson).RootElement.Clone();

    public static string EpcTdpRenderPolicyHtml(string version = TdpVersion, string monthYear = "October 2026")
    {
        using var sections = JsonDocument.Parse(TdpSectionsJson);
        var html = new StringBuilder();
        html.Append("<div class=\"epc-tdp-policy\">")
            .Append("<div class=\"epc-tdp-policy__header\">")
            .Append("<h2><i class=\"fa fa-shield\" style=\"color: #3b82f6;\"></i> Tenant Data Protection Policy</h2>")
            .Append("<p class=\"text-muted\">Version ").Append(version).Append(" &mdash; Last updated: ").Append(monthYear).Append("</p>")
            .Append("</div>");
        foreach (var section in sections.RootElement.EnumerateArray())
        {
            html.Append("<div class=\"epc-tdp-policy__section\" id=\"tdp-").Append(H(section.GetProperty("id").GetString())).Append("\">")
                .Append("<h3>").Append(H(section.GetProperty("title").GetString())).Append("</h3>")
                .Append("<p>").Append(H(section.GetProperty("content").GetString())).Append("</p>");
            if (section.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                html.Append("<ul class=\"epc-tdp-policy__list\">");
                foreach (var item in items.EnumerateArray())
                {
                    html.Append("<li>").Append(H(item.GetString())).Append("</li>");
                }

                html.Append("</ul>");
            }

            if (section.TryGetProperty("controls", out var controls) && controls.ValueKind == JsonValueKind.Array)
            {
                html.Append("<div class=\"epc-tdp-policy__controls\"><strong>Controls:</strong><ul>");
                foreach (var ctrl in controls.EnumerateArray())
                {
                    html.Append("<li><i class=\"fa fa-check-circle\" style=\"color: #10b981; margin-right: 6px;\"></i>").Append(H(ctrl.GetString())).Append("</li>");
                }

                html.Append("</ul></div>");
            }

            html.Append("</div>");
        }

        return html.Append("</div>").ToString();
    }

    public static JsonElement EpcConfigGroupMeta()
        => JsonDocument.Parse(ConfigMetaJson).RootElement.GetProperty("groups").Clone();

    public static JsonElement EpcConfigItemFrontendEffects()
        => JsonDocument.Parse(ConfigMetaJson).RootElement.GetProperty("effects").Clone();

    public static string EpcConfigItemFrontendEffect(string name)
        => EpcConfigItemFrontendEffects().TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

    public static Dictionary<string, string> EpcConfigGroupMetaFor(int groupId)
    {
        var groups = EpcConfigGroupMeta();
        if (groups.TryGetProperty(groupId.ToString(CultureInfo.InvariantCulture), out var row))
        {
            return new Dictionary<string, string>
            {
                ["icon"] = row.GetProperty("icon").GetString() ?? "",
                ["blurb"] = row.GetProperty("blurb").GetString() ?? "",
                ["tone"] = row.GetProperty("tone").GetString() ?? "",
                ["frontend"] = row.GetProperty("frontend").GetString() ?? ""
            };
        }

        return new Dictionary<string, string>
        {
            ["icon"] = "fa-cog",
            ["blurb"] = "Configuration group for this site.",
            ["tone"] = "slate",
            ["frontend"] = ""
        };
    }

    public static string EpcConfigItemLabel(string name, string? translatedCaption = "")
    {
        translatedCaption = (translatedCaption ?? "").Trim();
        return name switch
        {
            "epc_contact_phone" => "Frontend phone number",
            "epc_whatsapp_number" => "Frontend WhatsApp number",
            "epc_whatsapp_api_enabled" => "WhatsApp API — automated notifications (0/1)",
            "epc_whatsapp_api_token" => "WhatsApp Cloud API token",
            "epc_whatsapp_phone_number_id" => "WhatsApp phone_number_id",
            "epc_whatsapp_api_version" => "WhatsApp Graph API version",
            "epc_whatsapp_notify_names" => "WhatsApp notify events (comma-separated)",
            "epc_whatsapp_bilingual_notify" => "WhatsApp bilingual notify (0/1)",
            "umapi_api_key" => "Epart catalog API key (key only, not full URL)",
            "epc_head_office_title" => "Footer head office title",
            "epc_head_office_address" => "Footer head office address",
            "epc_head_office_email" => "Footer head office email",
            "epc_head_office_map_url" => "Footer head office map URL",
            "epc_global_locations_summary" => "Footer global locations summary",
            "epc_global_locations_countries" => "Footer countries / locations text",
            "epc_global_locations_map_url" => "Footer global map URL",
            _ => translatedCaption
        };
    }

    public static object[] EpcOrderGuideChecklist(string backend)
        =>
        [
            Step("Retail / wholesale registration", "/" + backend + "/users/customer_approvals", "Register test wholesale user → status Pending → approve + set dealing currency"),
            Step("Checkout blocked until approved", "Storefront checkout", "Pending user can browse/cart but cannot complete checkout"),
            Step("Place test order", "Storefront → cart → checkout", "Order appears in CP orders list; customer redirected to order page"),
            Step("Manager e-mail (new_order_to_manager)", "/" + backend + "/control/notifications", "Admin + office managers receive order summary with CP link"),
            Step("Customer e-mail (new_order_to_user)", "Same notification settings", "Customer inbox receives confirmation with line items"),
            Step("Supplier LPO (lpo_to_supplier)", "/" + backend + "/shop/logistics/storages", "One e-mail per warehouse with lines; LPO # = order ID; log on order card"),
            Step("Process in CP", "/" + backend + "/shop/orders/order?order_id=", "Change line statuses, record payment, message customer")
        ];

    public static Dictionary<string, object?> EpcOrderGuideSnapshot(
        string backend,
        string domain,
        IReadOnlyList<Dictionary<string, object?>> notifications,
        IReadOnlyList<Dictionary<string, object?>> storages,
        IReadOnlyDictionary<string, int> orderStats,
        int pendingTradeApprovals,
        IReadOnlyList<Dictionary<string, object?>> recentLpoLogs,
        IReadOnlyList<Dictionary<string, object?>> orderStatuses,
        IReadOnlyList<Dictionary<string, object?>> itemStatuses)
    {
        var lpoReady = storages.Count(s => s.TryGetValue("lpo_ready", out var ready) && ready is true);
        return new Dictionary<string, object?>
        {
            ["backend"] = backend,
            ["domain"] = domain.TrimEnd('/'),
            ["notifications"] = notifications,
            ["storages"] = storages,
            ["storages_lpo_ready"] = lpoReady,
            ["storages_total"] = storages.Count,
            ["order_stats"] = orderStats,
            ["pending_trade_approvals"] = pendingTradeApprovals,
            ["recent_lpo_logs"] = recentLpoLogs,
            ["order_statuses"] = orderStatuses,
            ["item_statuses"] = itemStatuses,
            ["checklist"] = EpcOrderGuideChecklist(backend)
        };
    }

    private static Dictionary<string, string> Step(string step, string where, string test)
        => new() { ["step"] = step, ["where"] = where, ["test"] = test };

    private static string H(string? value) => StorefrontTinyPages.HtmlSpecialChars(value);

    private const string AccTaxonomyJson = @"{""car-care"":{""label"":""Car Care"",""icon"":""fa-shower"",""subs"":{""car-covers"":{""label"":""Car Covers"",""keywords"":[""cover"",""car cover"",""body cover""]},""air-fresheners"":{""label"":""Air Fresheners"",""keywords"":[""freshener"",""perfume"",""aroma""]},""microfiber"":{""label"":""Microfiber Cloth"",""keywords"":[""microfiber"",""cloth""]},""wax-polish"":{""label"":""Wax & Polish"",""keywords"":[""wax"",""polish"",""compound""]},""shampoo"":{""label"":""Car Shampoo"",""keywords"":[""shampoo"",""wash"",""soap""]},""coolant"":{""label"":""Coolants"",""keywords"":[""coolant"",""antifreeze""]},""cleaners"":{""label"":""Cleaners"",""keywords"":[""cleaner"",""cleaning"",""detail""]}}},""interior"":{""label"":""Interior"",""icon"":""fa-car"",""subs"":{""floor-mats"":{""label"":""Floor Mats"",""keywords"":[""floor mat"",""mat"",""carpet""]},""sun-shades"":{""label"":""Sun Shades"",""keywords"":[""sun shade"",""shade"",""visor""]},""dash-covers"":{""label"":""Dash Covers"",""keywords"":[""dash"",""dashboard""]},""steering"":{""label"":""Steering Covers"",""keywords"":[""steering""]},""seat"":{""label"":""Seat Accessories"",""keywords"":[""seat"",""cushion"",""pillow""]},""organizers"":{""label"":""Organizers"",""keywords"":[""organizer"",""holder"",""console""]}}},""exterior"":{""label"":""Exterior"",""icon"":""fa-road"",""subs"":{""wipers"":{""label"":""Wipers"",""keywords"":[""wiper"",""blade""]},""mirrors"":{""label"":""Side Mirrors"",""keywords"":[""mirror""]},""mud-flaps"":{""label"":""Mud Flaps"",""keywords"":[""mud"",""flap"",""guard""]},""stickers"":{""label"":""Stickers & Logos"",""keywords"":[""sticker"",""emblem"",""logo""]},""bumpers"":{""label"":""Bumpers"",""keywords"":[""bumper""]},""grills"":{""label"":""Grills"",""keywords"":[""grill"",""grille""]}}},""lights-electrical"":{""label"":""Lights & Electrical"",""icon"":""fa-lightbulb-o"",""subs"":{""headlights"":{""label"":""Headlights"",""keywords"":[""headlight"",""head lamp""]},""bulbs"":{""label"":""Bulbs & LED"",""keywords"":[""bulb"",""led"",""lamp""]},""fog"":{""label"":""Fog Lights"",""keywords"":[""fog""]},""spark-glow"":{""label"":""Spark & Glow Plugs"",""keywords"":[""spark plug"",""glow plug"",""ignition""]},""sensors"":{""label"":""Sensors"",""keywords"":[""sensor"",""switch""]},""batteries"":{""label"":""Batteries"",""keywords"":[""battery"",""accumulator""]}}},""brakes"":{""label"":""Brakes"",""icon"":""fa-stop-circle"",""subs"":{""pads"":{""label"":""Brake Pads"",""keywords"":[""brake pad"",""pad""]},""discs"":{""label"":""Brake Discs"",""keywords"":[""brake disc"",""rotor"",""disc""]},""shoes"":{""label"":""Brake Shoes"",""keywords"":[""brake shoe"",""shoe""]},""fluid"":{""label"":""Brake Fluid"",""keywords"":[""brake fluid""]}}},""engine-mechanical"":{""label"":""Engine & Mechanical"",""icon"":""fa-cogs"",""subs"":{""filters"":{""label"":""Filters"",""keywords"":[""filter"",""oil filter"",""air filter"",""cabin"",""fuel filter""]},""belts"":{""label"":""Belts"",""keywords"":[""belt"",""timing""]},""gaskets"":{""label"":""Gaskets"",""keywords"":[""gasket"",""seal""]},""pistons"":{""label"":""Pistons"",""keywords"":[""piston""]},""bearings"":{""label"":""Bearings"",""keywords"":[""bearing""]},""pumps"":{""label"":""Pumps"",""keywords"":[""pump"",""fuel pump"",""water pump""]},""joints"":{""label"":""Joints"",""keywords"":[""joint"",""ball joint"",""propshaft""]},""engine"":{""label"":""Engine Parts"",""keywords"":[""engine"",""valve"",""cylinder"",""radiator"",""thermostat""]}}},""oils-lubricants"":{""label"":""Oils & Lubricants"",""icon"":""fa-tint"",""subs"":{""engine-oil"":{""label"":""Engine Oil"",""keywords"":[""engine oil"",""motor oil"",""0w"",""5w"",""10w""]},""gear-oil"":{""label"":""Gear Oil"",""keywords"":[""gear oil"",""transmission oil"",""atf""]},""grease"":{""label"":""Grease"",""keywords"":[""grease"",""lubricant""]}}},""tools-gadgets"":{""label"":""Tools & Gadgets"",""icon"":""fa-wrench"",""subs"":{""chargers"":{""label"":""Chargers"",""keywords"":[""charger"",""jump""]},""scanners"":{""label"":""Scanners"",""keywords"":[""scanner"",""diagnostic"",""obd""]},""tools"":{""label"":""Hand Tools"",""keywords"":[""tool"",""socket"",""wrench"",""jack""]}}},""tyres-wheels"":{""label"":""Tyres & Wheels"",""icon"":""fa-circle-o"",""subs"":{""tyres"":{""label"":""Tyres"",""keywords"":[""tyre"",""tire""]},""rims"":{""label"":""Rims & Wheels"",""keywords"":[""rim"",""wheel"",""alloy""]}}},""ev-hybrid"":{""label"":""EV & Hybrid"",""icon"":""fa-bolt"",""subs"":{""ev-chargers"":{""label"":""EV Chargers"",""keywords"":[""ev charger"",""wallbox"",""type 2""]},""hybrid"":{""label"":""Hybrid Parts"",""keywords"":[""hybrid"",""inverter""]}}},""other"":{""label"":""Other Parts"",""icon"":""fa-cubes"",""subs"":{""general"":{""label"":""General"",""keywords"":[]}}}}";
    private const string ConfigMetaJson = @"{""groups"":{""13"":{""icon"":""fa-search"",""blurb"":""SEO titles and catalog API keys used by search and OEM catalogues."",""tone"":""sky"",""frontend"":""Site title / description / keywords appear in browser tabs and search results. Catalog APIs power storefront part lookup.""},""1"":{""icon"":""fa-globe"",""blurb"":""Storefront contacts, footer offices, WhatsApp, cookies, and CP folder."",""tone"":""teal"",""frontend"":""Phone, WhatsApp, footer address/locations, cookie banner, and SSL notice show on the public site.""},""7"":{""icon"":""fa-cloud-download"",""blurb"":""Where the control panel checks for product updates."",""tone"":""slate"",""frontend"":""No direct storefront effect — CP / updates only.""},""12"":{""icon"":""fa-user-plus"",""blurb"":""Customer registration and login rules."",""tone"":""indigo"",""frontend"":""Affects sign-up / login forms: password length, phone mask, and code-based auth.""},""3"":{""icon"":""fa-envelope"",""blurb"":""SMTP used for order and account e-mails from the site."",""tone"":""amber"",""frontend"":""Customers receive order and account e-mails from these sender / SMTP settings.""},""4"":{""icon"":""fa-shopping-cart"",""blurb"":""Checkout currency, balances, and payment behaviour."",""tone"":""rose"",""frontend"":""Controls price display, guest checkout, partial payment, and balance rules on the storefront.""},""6"":{""icon"":""fa-inbox"",""blurb"":""Mailbox used to receive supplier price-list files."",""tone"":""slate"",""frontend"":""No direct storefront effect — price upload pipeline only.""},""11"":{""icon"":""fa-th-large"",""blurb"":""Catalogue pagination and product URL style."",""tone"":""emerald"",""frontend"":""Changes how product lists and product page URLs appear to customers.""},""9"":{""icon"":""fa-list"",""blurb"":""Article / spare-parts search results layout and sorting."",""tone"":""violet"",""frontend"":""Directly shapes the part-search results table, filters, sorting, and warehouse wait time.""},""14"":{""icon"":""fa-undo"",""blurb"":""Customer return / refund rules."",""tone"":""orange"",""frontend"":""Enables refund requests and shows withholding / notification text to customers when returns are used.""}},""effects"":{""site_name"":""Browser tab title and SEO site name on the public site."",""description_tag"":""Meta description for search engines / social previews."",""keywords_tag"":""Meta keywords tag on storefront pages."",""show_page_title"":""Shows or hides the page heading on frontend pages."",""show_site_name"":""Shows or hides the site name next to page titles."",""page_title_first"":""Order of page title vs site name in the browser tab."",""umapi_api_key"":""Powers OEM / manufacturer catalogue lookup on the storefront."",""list_length"":""Default rows per page in many storefront lists."",""backend_dir"":""CP folder only — changing this can break admin URLs (not storefront)."",""epc_contact_phone"":""Phone shown in storefront header / contact areas."",""epc_whatsapp_number"":""WhatsApp number used by storefront share / contact buttons."",""epc_whatsapp_api_enabled"":""Turns automated WhatsApp notifications on/off (backend send)."",""epc_whatsapp_api_token"":""Cloud API credential — not shown on storefront."",""epc_whatsapp_phone_number_id"":""Cloud API phone id — not shown on storefront."",""epc_whatsapp_api_version"":""Graph API version for WhatsApp sends — backend only."",""epc_whatsapp_notify_names"":""Which order events trigger WhatsApp messages."",""epc_whatsapp_bilingual_notify"":""Sends WhatsApp text in English + Arabic when enabled."",""epc_head_office_title"":""Footer “head office” title on the public site."",""epc_head_office_address"":""Footer head-office address block on the storefront."",""epc_head_office_email"":""Footer head-office e-mail on the storefront."",""epc_head_office_map_url"":""Map link for the footer head office."",""epc_global_locations_summary"":""Short footer summary of global locations."",""epc_global_locations_countries"":""Full countries / locations block in the storefront footer."",""epc_global_locations_map_url"":""Global map link in the storefront footer."",""show_vin_request"":""Shows the VIN request block on the main page."",""show_ssl_checker"":""Shows SSL certificate notice on the storefront."",""show_cookie"":""Shows the cookie consent warning for visitors."",""tech_key"":""Secret for cron / scripts — never expose on frontend."",""can_edit_system_content"":""CP permission only — not a storefront setting."",""update_server"":""CP update checks only."",""min_password_length"":""Minimum password length on customer registration."",""reg_variant"":""Simplifies login / registration by one-time code on the storefront."",""show_phone_mask"":""Formats the phone field on registration / profile forms."",""country_phone_mask"":""Which country phone mask the storefront uses."",""from_name"":""“From” name on e-mails customers receive."",""from_email"":""“From” address on e-mails customers receive."",""smtp_mode"":""How the site sends customer / staff e-mails."",""shop_currency"":""Currency used for storefront prices and checkout."",""currency_show_mode"":""Where the currency sign appears next to prices."",""price_rounding"":""How storefront prices are rounded."",""order_partial_payment"":""Lets customers pay part of an order at checkout."",""order_min_partial_payment"":""Minimum partial-payment percent shown at checkout."",""client_balance_available"":""Allows negative customer balance behaviour."",""client_balance_limit"":""Overdraft / balance limit rules for customers."",""purchase_without_reg"":""Allows checkout without creating an account."",""payment_on_receipt"":""Offers pay-on-delivery style options when enabled."",""products_count_for_page"":""How many catalogue products appear per page."",""product_url"":""How product page URLs are built for customers."",""catalogue_show_mode"":""Pagination vs load-more style catalogue browsing."",""async_search"":""Loads search results asynchronously on the article search page."",""products_table_mode"":""Layout of the found-parts table for customers."",""show_manufacturers_filter"":""Shows manufacturer filter on article search."",""show_storage_filter"":""Shows warehouse / storage filter on article search."",""show_search_string"":""Shows the search string block on results."",""let_refunds"":""Allows customers to start refund / return requests."",""retention_percentage"":""Withholding % explained when a refund is created."",""retention_percentage_text"":""Customer-facing refund notification text.""}}";
    private const string TdpSectionsJson = @"[{""id"":""scope"",""title"":""1. Scope & Applicability"",""content"":""This policy applies to all data stored, processed, or transmitted by or on behalf of any tenant registered on the ecomae platform. It covers all tenant databases, backups, logs, exports, and any derived data. Every person or system that accesses tenant data must comply with this policy.""},{""id"":""classification"",""title"":""2. Data Classification"",""content"":""All tenant data is classified into four levels:"",""items"":[""Highly Confidential — Financial records (GL, AP, AR, cash/bank, payroll, tax filings). Requires encryption at rest and in transit. Access logged and reviewed."",""Confidential — Business operations (orders, customers, vendors, HR records, inventory, pricing). Access restricted to authorized personnel."",""Internal — Operational data (products, CMS content, settings, marketing campaigns). Standard access controls."",""Public — Storefront catalogue data visible to end users. No access restrictions.""]},{""id"":""isolation"",""title"":""3. Database Isolation"",""content"":""Each tenant operates on a dedicated MySQL database. Cross-tenant queries are architecturally prohibited. The platform registry database (ecomae) contains only tenant metadata (name, status, hostname, industry) — never business data. Tenant database credentials are stored encrypted in the platform registry and are never exposed in URLs, logs, or error messages."",""controls"":[""Each tenant gets a unique database name, user, and password."",""Tenant DB users have privileges scoped to their own database only."",""No SQL joins, unions, or subqueries span multiple tenant databases."",""Platform code connects to one tenant DB at a time via epc_portal_tenant_control_tenant_pdo_connect()."",""Provider console iterates tenants sequentially — never holds multiple tenant connections simultaneously.""]},{""id"":""access_control"",""title"":""4. Access Control"",""content"":""Access to tenant data follows the principle of least privilege:"",""controls"":[""Guest users: No access to any tenant data."",""Tenant users: Read + write access to their own tenant only. Cannot see, query, or export other tenants' data."",""Provider (platform operator): Metadata + read access to all tenants for support/monitoring. Write access requires explicit authorization. Delete access requires two-factor confirmation."",""Cross-tenant access attempts are logged as security violations and trigger alerts."",""Session tokens are scoped to a single tenant context. Switching tenants in BOS creates a new scoped session.""]},{""id"":""audit"",""title"":""5. Audit Trail"",""content"":""All access to tenant data is logged in the platform audit system (epc_tdp_audit_log):"",""controls"":[""Every tenant DB connection is logged with actor ID, IP address, user agent, and timestamp."",""All mutating operations (create, update, delete) are logged with the full payload (sensitive fields redacted)."",""Security violations (cross-tenant attempts, permission denials) are logged separately in epc_tdp_violations."",""Audit logs are retained for 7 years (2555 days) per legal/tax requirements."",""Audit logs themselves are append-only — no deletion or modification permitted.""]},{""id"":""encryption"",""title"":""6. Encryption & Transport Security"",""content"":""All tenant data is protected in transit and at rest:"",""controls"":[""All HTTP traffic is served over TLS 1.2+ (enforced by Cloudflare and server SSL)."",""Database connections use localhost (127.0.0.1) — no unencrypted network transit."",""Database credentials in the platform registry are stored as plain text in MySQL but protected by DB-level access control. Future: migrate to AES-256 encrypted storage."",""Backups are stored on the same server with restricted file permissions (0600)."",""API tokens and webhook secrets are never logged or displayed after creation.""]},{""id"":""retention"",""title"":""7. Data Retention"",""content"":""Data retention periods are enforced uniformly across all tenants:"",""items"":[""ERP financial records: 7 years (legal requirement in most jurisdictions)."",""Platform audit logs: 7 years."",""Security violation logs: 7 years."",""Customer PII: 5 years after last activity."",""Session/login logs: 90 days."",""Temporary exports: 7 days (auto-deleted).""]},{""id"":""breach"",""title"":""8. Incident Response & Breach Notification"",""content"":""In the event of a data breach or unauthorized access:"",""controls"":[""Immediately isolate the affected tenant database(s)."",""Review epc_tdp_violations and epc_tdp_audit_log for the scope of the breach."",""Notify the affected tenant within 72 hours with: what data was accessed, when, by whom, and remediation steps."",""If personal data of end customers was exposed, notify per applicable data protection law (e.g., GDPR Article 33, UAE PDPL)."",""Rotate all affected credentials (DB passwords, API keys, session tokens)."",""Document the incident, root cause, and corrective actions in a post-incident report.""]},{""id"":""tenant_rights"",""title"":""9. Tenant Data Rights"",""content"":""Each tenant retains full ownership of their data:"",""controls"":[""Right to export: Tenants can export all their data at any time via ERP export functions."",""Right to deletion: Tenants can request full deletion of their database and all backups. Executed within 30 days."",""Right to portability: Data is stored in standard MySQL format, exportable as SQL dump or CSV."",""Right to audit: Tenants can request an audit report of all access to their data."",""Platform provider will never sell, share, or use tenant data for purposes other than providing the platform service.""]},{""id"":""compliance"",""title"":""10. Regulatory Compliance"",""content"":""The platform supports compliance with applicable data protection regulations based on each tenant's registration country. Compliance addenda are auto-applied via epc_country_profile():"",""items"":[""UAE: Federal Decree-Law No. 45/2021 (PDPL) — data localization, consent management, DPO requirements."",""EU/EEA: GDPR — lawful basis, data minimization, right to erasure, DPA requirements."",""UK: UK GDPR + Data Protection Act 2018."",""Saudi Arabia: PDPL (2023) — data classification, cross-border transfer restrictions."",""India: DPDP Act 2023 — consent, purpose limitation, data fiduciary obligations."",""General: SOC 2 Type II controls alignment for all tenants regardless of jurisdiction.""]}]";
}
