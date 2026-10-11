using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-form helpers. PHP identifiers kept for the inventory:
/// the SKU-media manager page body and the storefront storage panel markup.
/// </summary>
public static class PhpPlanQ1Form
{
    public const string SkuMediaManagerPath = "cp/content/shop/catalogue/epc_sku_media_manager.php";
    public const string StoragePanelPath = "cp/content/shop/prices_upload/epc_storefront_storage_panel.php";

    public static string DocumentRoot { get; set; } = "";
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<Dictionary<string, object?>?> GetAdminSession { get; set; } = () => null;
    public static Func<string> PageAssetVersion { get; set; } = () => "20260721";
    public static Action<List<string>, List<string>>? RegisterPageAssets { get; set; }
    public static Func<Dictionary<string, object?>, string>? FrameOpen { get; set; }
    public static Func<string>? FrameClose { get; set; }
    public static Func<PhpPlanQ1Open.OpenStore, List<Dictionary<string, object?>>>? ListStorageRows { get; set; }
    public static Func<PhpPlanQ1Open.OpenStore, List<Dictionary<string, object?>>>? ListStorageAudit { get; set; }

    public static List<string> LastCss { get; private set; } = [];
    public static List<string> LastJs { get; private set; } = [];

    public static void Reset()
    {
        DocumentRoot = "";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        GetAdminSession = () => null;
        PageAssetVersion = () => "20260721";
        RegisterPageAssets = null;
        FrameOpen = null;
        FrameClose = null;
        ListStorageRows = null;
        ListStorageAudit = null;
        LastCss = [];
        LastJs = [];
    }

    public static string ConfigScriptName() => "epc_sku_media_cp_config" + ".php";

    public static Dictionary<string, object?> EpcSkuMediaManagerRender(
        PhpPlanQ1Open.OpenStore db,
        List<PhpPlanQ1View.ContentRow> contents,
        List<PhpPlanQ1View.AccessRow> access,
        List<PhpPlanQ1View.LangRow> langs,
        List<PhpPlanQ1View.LangTrRow> tr,
        Dictionary<string, object?> query,
        string backendDir = "cp")
    {
        try
        {
            PhpPlanQ1View.Clock = Clock;
            PhpPlanQ1View.EpcSkuMediaCpInstall(db, contents, access, langs, tr, backendDir, true);
        }
        catch
        {
            // Schema/menu install is best-effort on first open.
        }

        var session = GetAdminSession();
        var csrf = session is not null ? Str(Field(session, "csrf_guard_key")) : "";
        backendDir = backendDir.Trim('/');
        if (backendDir == "")
        {
            backendDir = "cp";
        }

        var basePath = "/" + backendDir;
        var profileId = IntVal(Field(query, "profile_id"));
        var productId = IntVal(Field(query, "product_id"));
        var brand = Str(Field(query, "brand")).Trim();
        var article = Str(Field(query, "article")).Trim();
        var assetVer = PageAssetVersion() + "skuMedia4";
        var cssPath = Path.Combine(DocumentRoot.TrimEnd('/', '\\'), "content", "shop", "catalogue", "epc_sku_media.css");
        var jsPath = Path.Combine(DocumentRoot.TrimEnd('/', '\\'), "content", "shop", "catalogue", "epc_sku_media.js");
        var cssVer = File.Exists(cssPath)
            ? ((DateTimeOffset)File.GetLastWriteTimeUtc(cssPath)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            : assetVer;
        var jsVer = File.Exists(jsPath)
            ? ((DateTimeOffset)File.GetLastWriteTimeUtc(jsPath)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            : assetVer;
        LastCss = ["/content/shop/catalogue/epc_sku_media.css?v=" + RawUrl(cssVer)];
        LastJs =
        [
            basePath + "/content/shop/catalogue/" + ConfigScriptName() + "?v=" + RawUrl(assetVer),
            "/content/shop/catalogue/epc_sku_media.js?v=" + RawUrl(jsVer)
        ];
        RegisterPageAssets?.Invoke(LastCss, LastJs);

        var sb = new StringBuilder();
        if (FrameOpen is not null)
        {
            sb.Append(FrameOpen(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "SKU photos & specifications",
                ["sub"] = "Photos & specs for catalogue products and supplier warehouse brands / articles."
            }));
        }

        sb.Append("<div\n\tclass=\"epc-sku-media\"\n\tid=\"epc-sku-media\"\n\tdata-endpoint=\"/content/shop/catalogue/ajax_epc_sku_media.php\"\n\tdata-csrf=\"")
            .Append(H(csrf))
            .Append("\"\n\tdata-profile-id=\"")
            .Append(profileId)
            .Append("\"\n\tdata-product-id=\"")
            .Append(productId)
            .Append("\"\n\tdata-brand=\"")
            .Append(H(brand))
            .Append("\"\n\tdata-article=\"")
            .Append(H(article))
            .Append("\"\n>\n");
        sb.Append("\t<div class=\"epc-sku-media__hero\">\n\t\t<div>\n\t\t\t<h2>SKU photos &amp; specifications</h2>\n");
        sb.Append("\t\t\t<p>Search any supplier warehouse brand / article (or catalogue product), open it, and upload photos + specification sheets. Anything with a brand + article can get a photo — it then shows on the storefront part search and product pages.</p>\n");
        sb.Append("\t\t</div>\n\t\t<div style=\"display:flex;gap:8px;flex-wrap:wrap;\">\n");
        sb.Append("\t\t\t<a class=\"epc-sku-media__btn epc-sku-media__btn--ghost\" href=\"")
            .Append(H(basePath + "/shop/catalogue/products"))
            .Append("\"><i class=\"fa fa-th-large\"></i> Catalogue</a>\n");
        sb.Append("\t\t\t<a class=\"epc-sku-media__btn epc-sku-media__btn--ghost\" id=\"epc-sku-storefront-demo\" href=\"/en/parts/AISIN/CMT033\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-external-link\"></i> Example on storefront</a>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"epc-sku-media__btn\" data-sku-action=\"new\"><i class=\"fa fa-plus\"></i> New SKU profile</button>\n");
        sb.Append("\t\t</div>\n\t</div>\n");
        sb.Append("\t<p class=\"epc-sku-media__hint\" id=\"epc-sku-frontend-hint\">\n\t\tEach SKU with brand + article opens on the storefront at\n\t\t<code>/en/parts/{BRAND}/{ARTICLE}</code> — photo + specifications appear in part search.\n\t\tUse <strong>View on storefront</strong> on any library row or in the detail panel.\n\t</p>\n\n");
        sb.Append("\t<div class=\"epc-sku-media__layout\">\n\t\t<div class=\"epc-sku-media__panel\">\n\t\t\t<div class=\"epc-sku-media__panel-h\">\n\t\t\t\t<strong>SKU library</strong>\n\t\t\t\t<span style=\"font-size:12px;color:#64748b;font-weight:500;\">profiles · warehouses · catalogue</span>\n\t\t\t</div>\n");
        sb.Append("\t\t\t<div class=\"epc-sku-media__panel-b\">\n\t\t\t\t<div class=\"epc-sku-media__search\">\n\t\t\t\t\t<input type=\"text\" id=\"epc-sku-search\" placeholder=\"Search warehouse brand, article, name…\">\n");
        sb.Append("\t\t\t\t\t<button type=\"button\" class=\"epc-sku-media__btn epc-sku-media__btn--ghost\" data-sku-action=\"search\"><i class=\"fa fa-search\"></i></button>\n\t\t\t\t</div>\n\t\t\t\t<ul class=\"epc-sku-media__list\" id=\"epc-sku-list\"></ul>\n\t\t\t</div>\n\t\t</div>\n\n");
        sb.Append("\t\t<div class=\"epc-sku-media__panel\">\n\t\t\t<div class=\"epc-sku-media__panel-h\">\n\t\t\t\t<strong>SKU detail</strong>\n\t\t\t\t<div style=\"display:flex;gap:6px;flex-wrap:wrap;\">\n");
        sb.Append("\t\t\t\t\t<a class=\"epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm\" id=\"epc-sku-view-storefront\" href=\"#\" target=\"_blank\" rel=\"noopener\" style=\"display:none;\"><i class=\"fa fa-external-link\"></i> View on storefront</a>\n");
        sb.Append("\t\t\t\t\t<button type=\"button\" class=\"epc-sku-media__btn epc-sku-media__btn--sm\" data-sku-action=\"save-profile\"><i class=\"fa fa-save\"></i> Save</button>\n");
        sb.Append("\t\t\t\t\t<button type=\"button\" class=\"epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm\" data-sku-action=\"delete-profile\">Delete</button>\n");
        sb.Append("\t\t\t\t</div>\n\t\t\t</div>\n\t\t\t<div class=\"epc-sku-media__panel-b\" id=\"epc-sku-editor-body\">\n");
        sb.Append("\t\t\t\t<input type=\"hidden\" name=\"profile_id\" value=\"\">\n\t\t\t\t<div class=\"epc-sku-media__grid2\">\n");
        sb.Append(FieldBlock("Brand", "brand", "e.g. Bosch"));
        sb.Append(FieldBlock("Article / SKU", "article", "e.g. 0 986 494 053"));
        sb.Append(FieldBlock("Display title", "title", "Customer-facing product name"));
        sb.Append(FieldBlock("Subtitle", "subtitle", "Short supporting line"));
        sb.Append("\t\t\t\t\t<div class=\"epc-sku-media__field\">\n\t\t\t\t\t\t<label>Catalogue product ID (optional)</label>\n\t\t\t\t\t\t<input type=\"number\" name=\"product_id\" min=\"0\" step=\"1\" placeholder=\"Links to shop product\">\n\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t<div class=\"epc-sku-media__field\">\n\t\t\t\t\t\t<label>Status</label>\n\t\t\t\t\t\t<select name=\"status\">\n\t\t\t\t\t\t\t<option value=\"active\">Active</option>\n\t\t\t\t\t\t\t<option value=\"draft\">Draft</option>\n\t\t\t\t\t\t\t<option value=\"hidden\">Hidden</option>\n\t\t\t\t\t\t</select>\n\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t</div>\n\n\t\t\t\t<hr style=\"border:0;border-top:1px solid #e2e8f0;margin:18px 0;\">\n\n");
        sb.Append("\t\t\t\t<h3 style=\"margin:0 0 8px;font-size:16px;\"><i class=\"fa fa-camera\"></i> Photos <span style=\"color:#64748b;font-weight:500;font-size:13px;\">— unlimited</span></h3>\n");
        sb.Append("\t\t\t\t<div class=\"epc-sku-media__drop\">\n\t\t\t\t\t<strong>Add product photos</strong>\n\t\t\t\t\t<span>JPEG, PNG, GIF, WebP · primary photo shows first on the storefront</span>\n");
        sb.Append("\t\t\t\t\t<div class=\"epc-sku-media__grid2\" style=\"margin-top:10px;text-align:left;\">\n");
        sb.Append("\t\t\t\t\t\t<div class=\"epc-sku-media__field\">\n\t\t\t\t\t\t\t<label>Photo type</label>\n\t\t\t\t\t\t\t<select id=\"epc-sku-photo-type\">\n");
        sb.Append("\t\t\t\t\t\t\t\t<option value=\"product\">Product</option>\n\t\t\t\t\t\t\t\t<option value=\"packaging\">Packaging</option>\n\t\t\t\t\t\t\t\t<option value=\"detail\">Detail / close-up</option>\n\t\t\t\t\t\t\t\t<option value=\"diagram\">Diagram / drawing</option>\n\t\t\t\t\t\t\t\t<option value=\"install\">Installation</option>\n\t\t\t\t\t\t\t\t<option value=\"datasheet\">Datasheet shot</option>\n\t\t\t\t\t\t\t\t<option value=\"other\">Other</option>\n");
        sb.Append("\t\t\t\t\t\t\t</select>\n\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"epc-sku-media__field\">\n\t\t\t\t\t\t\t<label>Caption</label>\n\t\t\t\t\t\t\t<input type=\"text\" id=\"epc-sku-photo-caption\" placeholder=\"Optional caption\">\n\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"epc-sku-media__field\">\n\t\t\t\t\t\t\t<label>Alt text</label>\n\t\t\t\t\t\t\t<input type=\"text\" id=\"epc-sku-photo-alt\" placeholder=\"Accessibility text\">\n\t\t\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t\t\t<div class=\"epc-sku-media__field\" style=\"display:flex;align-items:flex-end;\">\n\t\t\t\t\t\t\t<button type=\"button\" class=\"epc-sku-media__btn\" data-sku-action=\"pick-file\"><i class=\"fa fa-upload\"></i> Upload photo</button>\n");
        sb.Append("\t\t\t\t\t\t\t<input type=\"file\" id=\"epc-sku-photo-input\" accept=\"image/jpeg,image/png,image/gif,image/webp\" style=\"display:none;\">\n\t\t\t\t\t\t</div>\n\t\t\t\t\t</div>\n\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t<div class=\"epc-sku-media__photos\" id=\"epc-sku-photos\"></div>\n\n\t\t\t\t<hr style=\"border:0;border-top:1px solid #e2e8f0;margin:18px 0;\">\n\n");
        sb.Append("\t\t\t\t<h3 style=\"margin:0 0 8px;font-size:16px;\"><i class=\"fa fa-list-alt\"></i> Specifications <span style=\"color:#64748b;font-weight:500;font-size:13px;\">— multiple types, unlimited rows</span></h3>\n");
        sb.Append("\t\t\t\t<p style=\"margin:0 0 6px;color:#64748b;font-size:13px;\">Add a specification type, then add as many labelled rows as you need (text, number, yes/no, list, rich).</p>\n");
        sb.Append("\t\t\t\t<div class=\"epc-sku-media__chips\" id=\"epc-sku-type-chips\"></div>\n");
        sb.Append("\t\t\t\t<button type=\"button\" class=\"epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm\" data-sku-action=\"add-group\"><i class=\"fa fa-plus\"></i> Custom type</button>\n");
        sb.Append("\t\t\t\t<div id=\"epc-sku-specs\" style=\"margin-top:12px;\"></div>\n\n");
        sb.Append("\t\t\t\t<div class=\"epc-sku-media__empty\" id=\"epc-sku-editor-empty\" style=\"display:none;\">Select a SKU from the left or create a new profile.</div>\n");
        sb.Append("\t\t\t</div>\n\t\t</div>\n\t</div>\n</div>\n");
        if (FrameClose is not null)
        {
            sb.Append(FrameClose());
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["html"] = sb.ToString(),
            ["csrf"] = csrf,
            ["base"] = basePath,
            ["css"] = LastCss,
            ["js"] = LastJs
        };
    }

    public static string EpcStorefrontStoragePanelRender(PhpPlanQ1Open.OpenStore db)
    {
        var rows = new List<Dictionary<string, object?>>();
        var error = "";
        try
        {
            PhpPlanQ1Open.EpcSsfEnsureSchema(db, false);
            rows = ListStorageRows is not null ? ListStorageRows(db) : PhpPlanQ1Open.EpcSsfCpListRows(db);
        }
        catch (Exception e)
        {
            error = e.Message;
            rows = [];
        }

        List<Dictionary<string, object?>> audit;
        try
        {
            audit = ListStorageAudit is not null ? ListStorageAudit(db) : db.Audits
                .OrderByDescending(a => a.Id)
                .Take(8)
                .Select(a => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["entity_type"] = a.EntityType,
                    ["entity_id"] = a.EntityId,
                    ["entity_name"] = a.EntityName,
                    ["storefront_disabled"] = a.StorefrontDisabled,
                    ["user_label"] = a.UserLabel,
                    ["created_at"] = a.CreatedAt
                })
                .ToList();
        }
        catch
        {
            audit = [];
        }

        var sb = new StringBuilder();
        sb.Append("<div class=\"col-lg-12\">\n\t<div class=\"hpanel epc-storefront-storage-panel\">\n\t\t<div class=\"panel-heading hbuilt\">\n\t\t\t<i class=\"fa fa-store\"></i> Storefront availability — warehouses &amp; price lists\n\t\t</div>\n\t\t<div class=\"panel-body\">\n\t\t\t");
        if (error != "")
        {
            sb.Append("\t\t\t<p class=\"alert alert-danger\" style=\"margin-bottom:12px;\">\n\t\t\t\tStorefront toggles unavailable right now (")
                .Append(H(error))
                .Append("). Price lists below still load.\n\t\t\t</p>\n");
        }

        sb.Append("\t\t\t<p class=\"alert alert-warning\" style=\"margin-bottom:12px;\">\n\t\t\t\t<strong>Temporary disable for storefront</strong> — hides a warehouse or price list from part search and pricing on the public site.\n");
        sb.Append("\t\t\t\tCP price management is unchanged; data is not deleted. Set the switch, then click <strong>Save</strong> — changes apply to the public storefront immediately after save.\n\t\t\t</p>\n");
        sb.Append("\t\t\t<div class=\"epc-cp-table-filter-bar\" style=\"display:flex;flex-wrap:wrap;gap:10px;align-items:center;margin:0 0 12px;\">\n");
        sb.Append("\t\t\t\t<label for=\"epc_ssf_filter\" style=\"margin:0;font-weight:700;font-size:12px;color:#64748b;\">Find warehouse / price list</label>\n");
        sb.Append("\t\t\t\t<input type=\"search\" id=\"epc_ssf_filter\" class=\"form-control input-sm\" style=\"max-width:340px;\" placeholder=\"Name, ID, type…\" autocomplete=\"off\" />\n");
        sb.Append("\t\t\t\t<span class=\"text-muted small\">Showing <strong id=\"epc_ssf_filter_count\">—</strong></span>\n\t\t\t</div>\n");
        sb.Append("\t\t\t<div class=\"table-responsive\">\n\t\t\t\t<table class=\"table table-striped table-hover\" id=\"epc_storefront_storage_table\">\n\t\t\t\t\t<thead>\n\t\t\t\t\t\t<tr>\n\t\t\t\t\t\t\t<th>ID</th>\n\t\t\t\t\t\t\t<th>Name</th>\n\t\t\t\t\t\t\t<th>Type</th>\n\t\t\t\t\t\t\t<th class=\"text-center\">Storefront status</th>\n\t\t\t\t\t\t\t<th class=\"text-center\">Temporary disable for storefront</th>\n\t\t\t\t\t\t\t<th class=\"text-center\">Save</th>\n\t\t\t\t\t\t</tr>\n\t\t\t\t\t</thead>\n\t\t\t\t\t<tbody>\n");
        foreach (var row in rows)
        {
            var disabled = !Empty(Field(row, "storefront_disabled"));
            var type = Str(Field(row, "entity_type") ?? "storage");
            var id = IntVal(Field(row, "entity_id"));
            var nameRaw = Str(Field(row, "name"));
            var name = H(nameRaw);
            var shortName = Str(Field(row, "short_name")).Trim();
            if (shortName != "" && !string.Equals(shortName, nameRaw, StringComparison.OrdinalIgnoreCase))
            {
                name += " <small class=\"text-muted\">(" + H(shortName) + ")</small>";
            }

            var typeLabel = Str(Field(row, "type_label"));
            var filter = (id + " " + nameRaw + " " + shortName + " " + type + " " + typeLabel + " " + (disabled ? "disabled" : "active")).Trim().ToLowerInvariant();
            sb.Append(new string('\t', 17)).Append("<tr data-epc-filter=\"")
                .Append(H(filter))
                .Append("\" data-entity-type=\"")
                .Append(H(type))
                .Append("\" data-entity-id=\"")
                .Append(id)
                .Append("\" data-saved-enabled=\"")
                .Append(disabled ? "0" : "1")
                .Append("\">\n\t\t\t\t\t\t\t<td>")
                .Append(id)
                .Append("</td>\n\t\t\t\t\t\t\t<td>")
                .Append(name)
                .Append("</td>\n\t\t\t\t\t\t\t<td>")
                .Append(H(typeLabel))
                .Append("</td>\n\t\t\t\t\t\t\t<td class=\"text-center\">\n\t\t\t\t\t\t\t\t<span class=\"label epc-ssf-badge ")
                .Append(disabled ? "label-warning" : "label-success")
                .Append("\">\n")
                .Append(new string('\t', 9))
                .Append(disabled ? "Temporarily disabled" : "Active")
                .Append(new string('\t', 8))
                .Append("</span>\n\t\t\t\t\t\t\t</td>\n");
            sb.Append("\t\t\t\t\t\t\t<td class=\"text-center\">\n\t\t\t\t\t\t\t\t<label class=\"epc-ssf-switch\" title=\"ON = visible on storefront, OFF = temporarily hidden\">\n");
            sb.Append("\t\t\t\t\t\t\t\t\t<input type=\"checkbox\"\n\t\t\t\t\t\t\t\t\t\tclass=\"epc-ssf-toggle-input\"\n\t\t\t\t\t\t\t\t\t\tdata-entity-type=\"")
                .Append(H(type))
                .Append("\"\n\t\t\t\t\t\t\t\t\t\tdata-entity-id=\"")
                .Append(id)
                .Append("\"\n\t\t\t\t\t\t\t\t\t\t")
                .Append(disabled ? "" : " checked")
                .Append(" />\n\t\t\t\t\t\t\t\t\t<span class=\"epc-ssf-slider\"></span>\n\t\t\t\t\t\t\t\t</label>\n\t\t\t\t\t\t\t</td>\n");
            sb.Append("\t\t\t\t\t\t\t<td class=\"text-center\">\n\t\t\t\t\t\t\t\t<button type=\"button\"\n\t\t\t\t\t\t\t\t\tclass=\"btn btn-xs btn-primary epc-ssf-save-btn\"\n\t\t\t\t\t\t\t\t\tdisabled\n\t\t\t\t\t\t\t\t\ttitle=\"Save storefront visibility for this row\">\n");
            sb.Append("\t\t\t\t\t\t\t\t\t<i class=\"fa fa-floppy-o\"></i> Save\n\t\t\t\t\t\t\t\t</button>\n\t\t\t\t\t\t\t</td>\n\t\t\t\t\t\t</tr>\n");
        }

        sb.Append(new string('\t', 10)).Append("</tbody>\n\t\t\t\t</table>\n\t\t\t</div>\n\t\t\t");
        if (!Empty(audit))
        {
            sb.Append("\t\t\t<p class=\"text-muted\" style=\"margin-top:10px;margin-bottom:4px;\">\n\t\t\t\t<small>Recent toggle activity — only real CP operator changes are recorded going forward.\n\t\t\t\tAutomated verify probes are read-only and no longer write toggle state.</small>\n\t\t\t</p>\n\t\t\t<ul class=\"list-unstyled\" style=\"font-size:12px;color:#666;\">\n");
            foreach (var item in audit)
            {
                var label = Str(Field(item, "user_label")).Trim();
                var isProbe = label is "verify-probe" or "verify-probe-restore"
                    || label.Contains("verify-probe", StringComparison.OrdinalIgnoreCase);
                var display = isProbe ? "[historical probe — no longer writes] " + label : label;
                sb.Append(new string('\t', 12)).Append("<li");
                if (isProbe)
                {
                    sb.Append(" style=\"opacity:0.65;font-style:italic;\"");
                }

                sb.Append(">\n")
                    .Append(new string('\t', 5))
                    .Append(H(Field(item, "created_at")))
                    .Append(new string('\t', 5))
                    .Append("— ")
                    .Append(H(Field(item, "entity_name")))
                    .Append(new string('\t', 5))
                    .Append("→ ")
                    .Append(IntVal(Field(item, "storefront_disabled")) == 1 ? "disabled" : "enabled");
                if (display != "")
                {
                    sb.Append(new string('\t', 11))
                        .Append("<em>(")
                        .Append(H(display))
                        .Append(")</em>");
                }

                sb.Append("\n").Append(new string('\t', 9)).Append("</li>\n");
            }

            sb.Append("\t\t\t\t\t\t\t</ul>\n\t\t\t");
        }

        sb.Append("\t\t</div>\n\t</div>\n</div>\n<script>\n(function () {\n\tfunction bindSsfFilter() {\n\t\tif (typeof window.epcCpBindTableFilter === 'function') {\n\t\t\twindow.epcCpBindTableFilter({\n\t\t\t\tinputId: 'epc_ssf_filter',\n\t\t\t\ttableId: 'epc_storefront_storage_table',\n\t\t\t\tcountId: 'epc_ssf_filter_count'\n\t\t\t});\n\t\t}\n\t}\n\tif (document.readyState === 'loading') {\n\t\tdocument.addEventListener('DOMContentLoaded', bindSsfFilter);\n\t} else {\n\t\tbindSsfFilter();\n\t}\n})();\n</script>\n");
        return sb.ToString();
    }

    private static string FieldBlock(string label, string name, string placeholder)
        => "\t\t\t\t\t<div class=\"epc-sku-media__field\">\n\t\t\t\t\t\t<label>" + label + "</label>\n\t\t\t\t\t\t<input type=\"text\" name=\"" + name + "\" placeholder=\"" + placeholder + "\">\n\t\t\t\t\t</div>\n";

    private static string RawUrl(string value)
        => Uri.EscapeDataString(value);

    private static string H(object? value)
    {
        var s = Str(value);
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);
    }

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value)
        => value switch
        {
            null => "",
            string s => s,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static int IntVal(object? value)
    {
        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static bool Empty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            int i => i == 0,
            long l => l == 0,
            string s => s is "" or "0",
            System.Collections.ICollection c => c.Count == 0,
            _ => Str(value) is "" or "0"
        };
}
