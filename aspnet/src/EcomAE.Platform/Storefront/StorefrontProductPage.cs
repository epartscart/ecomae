using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Customer product page owned by <c>content/shop/catalogue/product_page_for_customer.php</c> and
/// <c>printProduct_Info.php</c>. The offer table and basket JavaScript remain in their dedicated ports.
/// Unsafe values which PHP printed raw are deliberately HTML/attribute encoded here.
/// </summary>
public static class StorefrontProductPage
{
    public sealed record Request(
        long ProductId,
        long UserId,
        bool IsFrontMode,
        bool IsAdmin,
        int CurrentProductBlockType,
        string LangHref,
        string DomainPath,
        string BackendDir,
        string? CityCookie,
        string? SessionToken,
        string? SessionUserId,
        IReadOnlySet<long> Bookmarks,
        IReadOnlySet<long> Compare,
        IReadOnlyDictionary<string, string> Config);

    private sealed record Product(long Id, string CaptionKey, long CategoryId, string Alias);
    private sealed record Image(long Id, string FileName);
    private sealed record Property(long Id, string CaptionKey, int TypeId, long ListId);

    public static async Task<string> RenderAsync(
        DbConnection connection,
        Request request,
        Func<string, CancellationToken, Task<string>> translate,
        CancellationToken cancellationToken)
    {
        async Task<string> T(object value) =>
            await translate(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, cancellationToken).ConfigureAwait(false);

        var product = await LoadProductAsync(connection, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return "<div class=\"product_info_wrap\" id=\"product_info_wrap_div\"><span>"
                   + H(await T(4171).ConfigureAwait(false))
                   + "</span></div>";
        }

        var caption = await T(product.CaptionKey).ConfigureAwait(false);
        var images = await LoadImagesAsync(connection, product.Id, cancellationToken).ConfigureAwait(false);
        var html = request.IsFrontMode
            ? await RenderFrontInfoAsync(connection, request, product, caption, images, T, cancellationToken).ConfigureAwait(false)
            : await RenderCatalogueInfoAsync(connection, product, caption, images, T, cancellationToken).ConfigureAwait(false);

        if (!request.IsFrontMode)
        {
            return html;
        }

        // This is the separate lower "offers" slice from product_page_for_customer.php.
        html += await StorefrontProductOffers.RenderPageAsync(
            connection,
            product.Id,
            request.UserId,
            request.CityCookie,
            request.LangHref,
            request.SessionToken,
            request.SessionUserId,
            request.Config,
            cancellationToken).ConfigureAwait(false);
        html += await RenderTabsAsync(connection, request, product, T, cancellationToken).ConfigureAwait(false);
        html += await RenderVariantsAsync(connection, request, product, T, cancellationToken).ConfigureAwait(false);
        html += await RenderRelatedAsync(connection, request, product, T, cancellationToken).ConfigureAwait(false);
        return html;
    }

    private static async Task<string> RenderFrontInfoAsync(
        DbConnection connection,
        Request request,
        Product product,
        string caption,
        IReadOnlyList<Image> images,
        Func<object, Task<string>> t,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.Append("\t<link href=\"/lib/Lightbox/css/lightbox.css\" rel=\"stylesheet\" type=\"text/css\" />\n")
            .Append("\t<script type=\"text/javascript\" src=\"/lib/Lightbox/js/lightbox.js\"></script>\n")
            .Append("\t<div class=\"col-md-5\">\n\t\t<div class=\"div_product_img_big\">\n");
        if (images.Count == 0)
        {
            sb.Append("\t\t\t<div class=\"epc-product-placeholder\">\n")
                .Append("\t\t\t\t<i class=\"fa fa-cogs\" aria-hidden=\"true\"></i>\n")
                .Append("\t\t\t\t<span>Product image coming soon</span>\n")
                .Append("\t\t\t\t<strong>").Append(H(caption)).Append("</strong>\n\t\t\t</div>\n");
        }
        else
        {
            var src = ImageUrl(images[0].FileName);
            sb.Append("\t\t\t<a href=\"").Append(A(src)).Append("\" data-lightbox=\"product_").Append(product.Id)
                .Append("\"><img src=\"").Append(A(src)).Append("\"/></a>\n");
        }

        sb.Append("\t\t</div>\n\t\t<p style=\"text-align:center;\"><small style=\"color:#999; font-size: 75%;\">")
            .Append(H(await t(4112).ConfigureAwait(false))).Append("</small></p>\n")
            .Append("\t\t<div class=\"div_product_img_small\">\n");
        foreach (var image in images.Skip(1))
        {
            var src = ImageUrl(image.FileName);
            sb.Append("\t\t\t<a href=\"").Append(A(src)).Append("\" data-lightbox=\"product_").Append(product.Id)
                .Append("\" style=\"background:url('").Append(Css(src)).Append("') #fff;\"></a>\n");
        }

        sb.Append("\t\t</div>\n\t</div>\n\t<div class=\"col-md-7\">\n\t\t<div class=\"div_product_price\">\n");
        var offer = await LoadMainOfferAsync(connection, request, product.Id, cancellationToken).ConfigureAwait(false);
        if (offer is null)
        {
            sb.Append("\t\t\t<div class=\"epc-product-availability-card epc-product-availability-card--empty\">\n")
                .Append("\t\t\t\t<span class=\"epc-product-availability-card__eyebrow\">Price on request</span>\n")
                .Append("\t\t\t\t<h3>").Append(H(caption)).Append("</h3>\n")
                .Append("\t\t\t\t<p>This product page is active, but no current online offer is connected to it. Send a request and we will confirm price, stock and delivery time.</p>\n")
                .Append("\t\t\t\t<div class=\"epc-product-availability-card__chips\">\n")
                .Append("\t\t\t\t\t<span><i class=\"fa fa-check-circle\" aria-hidden=\"true\"></i> Active product page</span>\n")
                .Append("\t\t\t\t\t<span><i class=\"fa fa-clock-o\" aria-hidden=\"true\"></i> Availability check</span>\n")
                .Append("\t\t\t\t</div>\n\t\t\t\t<a class=\"btn btn-ar btn-primary\" href=\"")
                .Append(A(request.LangHref)).Append("/zapros-prodavczu\" target=\"_blank\">")
                .Append(H(await t(4115).ConfigureAwait(false))).Append("</a>\n\t\t\t</div>\n")
                .Append(RenderMarketBlock(caption, request.LangHref));
        }
        else
        {
            var office = await OfficeAsync(connection, offer.OfficeId, cancellationToken).ConfigureAwait(false);
            var divId = offer.OfficeId + "_" + offer.StorageId + "_" + offer.StorageRecordId;
            sb.Append("\t\t\t<div class=\"price_div\"><div class=\"price_div_header\">")
                .Append(H(await t(2751).ConfigureAwait(false))).Append(":</div><div class=\"price_div_text\">")
                .Append(H(FormatCurrency(offer.Price, offer.CurrencyIso, offer.CurrencySign, offer.CurrencyShort, offer.CurrencyRate, Config(request, "currency_show_mode"))))
                .Append("</div></div>\n")
                .Append("\t\t\t<div class=\"office_info_div\"><div class=\"ooffice_info_div_header\">")
                .Append(H(await t(3248).ConfigureAwait(false))).Append(":</div><div class=\"office_info_div_text\"><span>")
                .Append(H((await t(office.Caption).ConfigureAwait(false)).Trim())).Append("<br/><small>")
                .Append(H((await t(office.City).ConfigureAwait(false)).Trim())).Append(", ")
                .Append(H((await t(office.Address).ConfigureAwait(false)).Trim())).Append("</small></span></div></div>\n")
                .Append("\t\t\t<div class=\"product_div_manufacturer\">").Append(H(await t(offer.Manufacturer).ConfigureAwait(false))).Append("</div>\n")
                .Append("\t\t\t<div class=\"product_div_article\">").Append(H(await t(offer.Article).ConfigureAwait(false))).Append("</div>\n")
                .Append("\t\t\t<div class=\"product_div_tile\"><div class=\"product_div_exist_info\">");
            if (offer.Exist > 0)
            {
                sb.Append(offer.TimeToExe == 0
                    ? "<span class=\"green\">" + H(await t(4094).ConfigureAwait(false)) + "</span>"
                    : "<span class=\"orange\">" + H(await t(3550).ConfigureAwait(false)) + " " + offer.TimeToExe + " " + H(await t(4097).ConfigureAwait(false)) + ".</span>");
                sb.Append("<span class=\"exist\">").Append(offer.Exist).Append(" ").Append(H(await t(4095).ConfigureAwait(false))).Append(".</span>");
            }
            else
            {
                sb.Append(offer.Reserved > 0
                    ? "<span class=\"blue\">" + H(await t(4098).ConfigureAwait(false) + "</span>")
                    : "<span class=\"red\">" + H(await t(4099).ConfigureAwait(false) + "</span>"));
            }

            sb.Append("</div></div>\n\t\t\t<div class=\"btn_cart_div\"><div class=\"btn_cart_div_header\"></div><div class=\"btn_cart_div_text\">");
            if (offer.Exist > 0)
            {
                sb.Append(PurchaseButton(divId, offer.Exist, await t(4096).ConfigureAwait(false)));
            }
            else
            {
                sb.Append(RequestButton(request.LangHref, await t(4115).ConfigureAwait(false)));
            }

            sb.Append("</div></div>\n");
            if (offer.Price <= 0 || offer.Exist <= 0)
            {
                sb.Append("\t\t\t<div class=\"epc-product-quote-note\"><div><strong>Need this product?</strong>")
                    .Append("<span>Send a request and our team will confirm price, availability and delivery time.</span></div>")
                    .Append("<a class=\"btn btn-ar btn-primary\" href=\"").Append(A(request.LangHref))
                    .Append("/zapros-prodavczu\" target=\"_blank\">").Append(H(await t(4115).ConfigureAwait(false))).Append("</a></div>\n");
            }
        }

        sb.Append(RenderEvaluationMarkScript(product.Id, request.SessionToken ?? string.Empty));
        if (request.IsAdmin)
        {
            sb.Append("\t\t\t<div class=\"product_div_admin\"><a href=\"").Append(A(request.DomainPath + request.BackendDir))
                .Append("/shop/catalogue/products/product?category_id=").Append(product.CategoryId).Append("&amp;product_id=").Append(product.Id)
                .Append("\" target=\"_blank\"><i class=\"fa fa-external-link\"></i></a></div>\n");
        }

        sb.Append(RenderBookmark(product.Id, request.Bookmarks.Contains(product.Id), request.CurrentProductBlockType, t).Result)
            .Append(RenderCompare(product.Id, request.Compare.Contains(product.Id), request.CurrentProductBlockType, t).Result)
            .Append("\t\t</div>\n\t\t<div style=\"color:#999; font-size: 75%; text-align:center; line-height: 1.3em; margin-top: 8px; margin-bottom: 5px;\">")
            .Append(H(await t(4116).ConfigureAwait(false))).Append("</div>\n\t</div>\n");
        return sb.ToString();
    }

    private static async Task<string> RenderCatalogueInfoAsync(
        DbConnection connection,
        Product product,
        string caption,
        IReadOnlyList<Image> images,
        Func<object, Task<string>> t,
        CancellationToken cancellationToken)
    {
        var primary = images.Count == 0 ? "/content/files/images/no_image.png" : ImageUrl(images[0].FileName);
        var current = images.Count == 0 ? 0 : images[0].Id;
        var sb = new StringBuilder();
        sb.Append("<div class=\"product_info_wrap\" id=\"product_info_wrap_div\">\n")
            .Append("\t<div class=\"product_galery\" id=\"product_galery_div\">\n\t\t<div class=\"main_image\" id=\"main_image_div\">\n")
            .Append("\t\t\t<img onload=\"setBlocksHeight();\" id=\"main_image\" src=\"").Append(A(primary)).Append("\" alt=\"").Append(A(caption)).Append("\" />\n")
            .Append("\t\t</div>\n\t\t<div class=\"all_product_images\" id=\"all_product_images_div\">\n");
        for (var i = 0; i < images.Count; i++)
        {
            var image = images[i];
            var src = ImageUrl(image.FileName);
            sb.Append("\t\t\t<img onload=\"setBlocksHeight();\" onclick=\"selectImage(").Append(image.Id).Append(",'").Append(Js(src))
                .Append("');\" id=\"product_image_select_").Append(image.Id).Append("\" class=\"product_image_select ")
                .Append(i == 0 ? "current_image" : "other_image").Append("\" src=\"").Append(A(src)).Append("\" />\n");
        }

        sb.Append("\t\t</div>\n\t</div>\n\t<div class=\"product_genaral_info\" id=\"product_genaral_info_div\">\n\t\t<table>\n")
            .Append(await RenderPropertyRowsAsync(connection, product, t, detailed: false, cancellationToken).ConfigureAwait(false))
            .Append("\t\t</table>\n")
            .Append(await ProductTextAsync(connection, product.Id, t, 2971, cancellationToken).ConfigureAwait(false))
            .Append("\n\t</div>\n</div>\n")
            .Append("<script>var current_image_id = ").Append(current).Append(";\n")
            .Append("function selectImage(id,image_url){if(current_image_id==id){return;}var previous=document.getElementById(\"product_image_select_\"+current_image_id);")
            .Append("if(previous){previous.setAttribute(\"class\",\"product_image_select other_image\");}var next=document.getElementById(\"product_image_select_\"+id);")
            .Append("if(next){next.setAttribute(\"class\",\"product_image_select current_image\");}current_image_id=id;document.getElementById(\"main_image\").setAttribute(\"src\",image_url);}</script>\n");
        return sb.ToString();
    }

    private static async Task<string> RenderTabsAsync(
        DbConnection connection,
        Request request,
        Product product,
        Func<object, Task<string>> t,
        CancellationToken cancellationToken)
    {
        var specs = await RenderPropertyRowsAsync(connection, product, t, detailed: true, cancellationToken).ConfigureAwait(false);
        var description = await ProductTextAsync(connection, product.Id, t, 4147, cancellationToken).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.Append("<div class=\"col-md-12\">\n<h2 class=\"section-title\">").Append(H(await t(2069).ConfigureAwait(false))).Append("</h2>\n")
            .Append("<ul class=\"nav nav-tabs\"><li class=\"active\"><a href=\"#tab_product_1\" data-toggle=\"tab\">")
            .Append(H(await t(3164).ConfigureAwait(false))).Append("</a></li><li><a href=\"#tab_product_2\" data-toggle=\"tab\">")
            .Append(H(await t(2073).ConfigureAwait(false))).Append("</a></li><li><a href=\"#tab_product_3\" data-toggle=\"tab\">")
            .Append(H(await t(4148).ConfigureAwait(false))).Append("</a></li></ul>\n<div class=\"tab-content navbar-inverse\">\n")
            .Append("<div class=\"tab-pane active\" id=\"tab_product_1\">");
        if (specs.Length == 0)
        {
            sb.Append(H(await t(4171).ConfigureAwait(false)));
        }
        else
        {
            sb.Append("<table class=\"table\"><tr><th>").Append(H(await t(2102).ConfigureAwait(false))).Append("</th><th>")
                .Append(H(await t(4170).ConfigureAwait(false))).Append("</th></tr>").Append(specs).Append("</table>");
        }

        sb.Append("</div>\n<div class=\"tab-pane\" id=\"tab_product_2\">").Append(description).Append("</div>\n")
            .Append("<div class=\"tab-pane\" id=\"tab_product_3\">").Append(await RenderEvaluationsAsync(request, product.Id, t, cancellationToken).ConfigureAwait(false))
            .Append("</div>\n</div>\n</div>\n");
        return sb.ToString();
    }

    private static async Task<string> RenderEvaluationsAsync(
        Request request,
        long productId,
        Func<object, Task<string>> t,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var csrf = Js(request.SessionToken ?? string.Empty);
        var sb = new StringBuilder();
        sb.Append("<h3>").Append(H(await t(4148).ConfigureAwait(false))).Append("</h3>\n")
            .Append("<div id=\"make_evaluation\" class=\"col-xs-12 col-sm-12 col-md-12 col-lg-12\">");
        if (request.UserId <= 0)
        {
            sb.Append("<p>").Append(H(await t(4158).ConfigureAwait(false))).Append("</p>");
        }
        else
        {
            sb.Append("<div class=\"panel panel-primary\" id=\"evaluations_form\"><div class=\"panel-heading\">")
                .Append(H(await t(4149).ConfigureAwait(false))).Append("</div><div class=\"panel-body\">")
                .Append("<div class=\"evaluations_mark\">");
            for (var i = 1; i <= 5; i++)
            {
                sb.Append("<i onclick=\"onStarPush(").Append(i).Append(");\" class=\"fa fa-star-o em-primary star_evaluation\"></i>");
            }

            sb.Append("</div><textarea class=\"form-control\" rows=\"2\" id=\"text_plus\"></textarea>")
                .Append("<textarea class=\"form-control\" rows=\"2\" id=\"text_minus\"></textarea>")
                .Append("<textarea class=\"form-control\" rows=\"2\" id=\"text\"></textarea>")
                .Append("<input type=\"checkbox\" id=\"hide_user_data\" /><button id=\"sendEvaluation_Button\" onclick=\"sendEvaluation();\" class=\"btn btn-ar btn-primary\">")
                .Append(H(await t(4152).ConfigureAwait(false))).Append("</button></div></div>");
        }

        sb.Append("</div><div id=\"evaluations_general_mark\"></div><div id=\"evaluations_area\"></div>\n<script>")
            .Append("var evaluation_product_id=").Append(productId).Append(";var evaluation_csrf_guard_key='").Append(csrf).Append("';")
            .Append("/* Existing PHP mutation endpoints stay authoritative. */")
            .Append("var evaluation_add_url='/content/shop/catalogue/evaluations/ajax_add_evaluation.php';")
            .Append("var evaluation_mark_url='/content/shop/catalogue/evaluations/ajax_get_product_general_mark.php';")
            .Append("var evaluation_list_url='/content/shop/catalogue/evaluations/ajax_get_product_evaluations.php';</script>\n");
        return sb.ToString();
    }

    private static async Task<string> RenderPropertyRowsAsync(
        DbConnection connection,
        Product product,
        Func<object, Task<string>> t,
        bool detailed,
        CancellationToken cancellationToken)
    {
        var properties = await LoadPropertiesAsync(connection, product.CategoryId, cancellationToken).ConfigureAwait(false);
        var sb = new StringBuilder();
        foreach (var property in properties)
        {
            // PHP printProduct_Info only maps 1..6. New date/file/image property types are intentionally absent.
            if (property.TypeId is < 1 or > 6)
            {
                continue;
            }

            var value = await PropertyValueAsync(connection, product.Id, property, t, detailed, cancellationToken).ConfigureAwait(false);
            if (detailed && value.Length == 0)
            {
                continue;
            }

            if (detailed)
            {
                sb.Append("<tr><td>").Append(H(await t(property.CaptionKey).ConfigureAwait(false))).Append("</td><td>").Append(value).Append("</td></tr>");
            }
            else
            {
                sb.Append("\t\t\t<tr><td style=\"vertical-align:top;\"><font style=\"font-weight:bold; margin-right:10px;\">")
                    .Append(H(await t(property.CaptionKey).ConfigureAwait(false))).Append("</font></td><td style=\"vertical-align:top;\">")
                    .Append(value).Append("</td></tr>\n");
            }
        }

        return sb.ToString();
    }

    private static async Task<string> PropertyValueAsync(
        DbConnection connection,
        long productId,
        Property property,
        Func<object, Task<string>> t,
        bool detailed,
        CancellationToken cancellationToken)
    {
        var postfix = property.TypeId switch { 1 => "int", 2 => "float", 3 => "text", 4 => "bool", 5 => "list", 6 => "tree_list", _ => "" };
        var values = await ColumnAsync(connection,
            "SELECT `value` FROM `shop_properties_values_" + postfix + "` WHERE `product_id` = ? AND `property_id` = ? ORDER BY `id`",
            [productId, property.Id], cancellationToken).ConfigureAwait(false);
        if (values.Count == 0)
        {
            return string.Empty;
        }

        if (property.TypeId is 1 or 2)
        {
            return H(values[0]);
        }

        if (property.TypeId == 3)
        {
            return H(await t(values[0]).ConfigureAwait(false));
        }

        if (property.TypeId == 4)
        {
            return detailed ? H(await t(values[0] == "1" ? 2456 : 2457).ConfigureAwait(false)) : H(values[0]);
        }

        if (property.TypeId == 5)
        {
            var selected = values.ToHashSet(StringComparer.Ordinal);
            var rows = await RowsAsync(connection,
                "SELECT `id`,`value` FROM `shop_line_lists_items` WHERE `line_list_id` = ? ORDER BY `order`",
                [property.ListId], cancellationToken).ConfigureAwait(false);
            var translated = new List<string>();
            foreach (var row in rows.Where(r => selected.Contains(r[0])))
            {
                translated.Add(H(await t(row[1]).ConfigureAwait(false)));
            }

            return string.Join(", ", translated);
        }

        var selectedTree = values.Select(v => long.TryParse(v, out var id) ? id : 0).Where(id => id > 0).ToHashSet();
        var tree = await RowsAsync(connection,
            "SELECT `id`,`value`,`parent`,`level`,`count` FROM `shop_tree_lists_items` WHERE `line_list_id` = ? ORDER BY `level`,`order`",
            [property.ListId], cancellationToken).ConfigureAwait(false);
        var byId = tree.ToDictionary(r => long.Parse(r[0], CultureInfo.InvariantCulture));
        var chains = new List<string>();
        foreach (var leaf in selectedTree)
        {
            var chain = new List<string>();
            var cursor = leaf;
            while (cursor > 0 && byId.TryGetValue(cursor, out var row))
            {
                chain.Insert(0, H(await t(row[1]).ConfigureAwait(false)));
                cursor = long.TryParse(row[2], out var parent) ? parent : 0;
            }

            if (chain.Count > 0)
            {
                chains.Add(string.Join(" ", chain));
            }
        }

        return detailed ? string.Join("<br/>", chains) : (chains.Count == 0 ? string.Empty : "<br>" + string.Join("<br>", chains));
    }

    private static async Task<string> RenderVariantsAsync(
        DbConnection connection,
        Request request,
        Product product,
        Func<object, Task<string>> t,
        CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(connection,
            "SELECT `id`,`value` FROM `shop_categories_properties_map` WHERE `category_id` = ? AND `is_option` = 1 ORDER BY `order`",
            [product.CategoryId], cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        // The PHP comparison query only supports property types 1..6. Render links for explicitly related
        // option products when fixtures provide shop_related_products; this preserves URL mode and safe output.
        var variants = await RowsAsync(connection,
            "SELECT p.`id`,p.`caption`,p.`alias`,IFNULL(c.`url`,'') FROM `shop_related_products` r "
            + "JOIN `shop_catalogue_products` p ON p.`id`=r.`product_id_related` "
            + "LEFT JOIN `shop_catalogue_categories` c ON c.`id`=p.`category_id` WHERE r.`product_id`=? ORDER BY p.`id`",
            [product.Id], cancellationToken).ConfigureAwait(false);
        if (variants.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<div class=\"container\"><div class=\"row\"><div class=\"col-xs-12 col-sm-12 col-md-12 col-lg-12\"><h2 class=\"section-title\">");
        sb.Append(H(await t(4167).ConfigureAwait(false))).Append("</h2>");
        foreach (var row in variants)
        {
            var urlPart = string.Equals(Config(request, "product_url"), "alias", StringComparison.Ordinal) ? row[2] : row[0];
            sb.Append("<a class=\"product-option-variant\" href=\"").Append(A(request.LangHref)).Append("/")
                .Append(Uri.EscapeDataString(row[3])).Append("/").Append(Uri.EscapeDataString(urlPart)).Append("\">")
                .Append(H(await t(row[1]).ConfigureAwait(false))).Append("</a>");
        }

        return sb.Append("</div></div></div>\n").ToString();
    }

    private static async Task<string> RenderRelatedAsync(
        DbConnection connection,
        Request request,
        Product product,
        Func<object, Task<string>> t,
        CancellationToken cancellationToken)
    {
        var related = await RowsAsync(connection,
            "SELECT p.`id`,p.`caption`,p.`alias`,IFNULL(i.`file_name`,'') FROM `shop_related_products` r "
            + "JOIN `shop_catalogue_products` p ON p.`id`=r.`product_id_related` "
            + "LEFT JOIN `shop_products_images` i ON i.`id`=(SELECT MIN(i2.`id`) FROM `shop_products_images` i2 WHERE i2.`product_id`=p.`id`) "
            + "WHERE r.`product_id`=? ORDER BY p.`id`",
            [product.Id], cancellationToken).ConfigureAwait(false);
        var similar = await RowsAsync(connection,
            "SELECT p.`id`,p.`caption`,p.`alias`,IFNULL(i.`file_name`,'') FROM `shop_catalogue_products` p "
            + "LEFT JOIN `shop_products_images` i ON i.`id`=(SELECT MIN(i2.`id`) FROM `shop_products_images` i2 WHERE i2.`product_id`=p.`id`) "
            + "WHERE p.`category_id`=? AND p.`id`<>? AND IFNULL(p.`published_flag`,0)=1 ORDER BY p.`id` LIMIT 5",
            [product.CategoryId, product.Id], cancellationToken).ConfigureAwait(false);
        var sb = new StringBuilder();
        await AppendCardsAsync(sb, related, await t(781).ConfigureAwait(false), request, t).ConfigureAwait(false);
        await AppendCardsAsync(sb, similar, await t(4169).ConfigureAwait(false), request, t).ConfigureAwait(false);
        return sb.ToString();
    }

    private static async Task AppendCardsAsync(
        StringBuilder sb,
        IReadOnlyList<string[]> rows,
        string heading,
        Request request,
        Func<object, Task<string>> t)
    {
        if (rows.Count == 0)
        {
            return;
        }

        sb.Append("<div class=\"col-xs-12 col-sm-12 col-md-12 col-lg-12\"><h2 class=\"section-title\">").Append(H(heading)).Append("</h2>");
        foreach (var row in rows)
        {
            var target = string.Equals(Config(request, "product_url"), "alias", StringComparison.Ordinal) ? row[2] : row[0];
            var caption = await t(row[1]).ConfigureAwait(false);
            sb.Append("<div class=\"product_div_tile col-xs-12 col-sm-4 col-md-3 col-lg-1-5\"><div class=\"product_div_image_wrap\"><a href=\"")
                .Append(A(request.LangHref)).Append("/shop/catalogue/product?id=").Append(Uri.EscapeDataString(target)).Append("\"><img src=\"")
                .Append(A(row[3].Length == 0 ? "/content/files/images/no_image.png" : ImageUrl(row[3]))).Append("\" alt=\"").Append(A(caption))
                .Append("\" /></a></div><div class=\"product_div_name\"><a href=\"").Append(A(request.LangHref))
                .Append("/shop/catalogue/product?id=").Append(Uri.EscapeDataString(target)).Append("\"><div class=\"product_div_caption\">")
                .Append(H(caption)).Append("</div></a></div></div>");
        }

        sb.Append("</div>\n");
    }

    private sealed record MainOffer(
        long OfficeId, long StorageId, long StorageRecordId, decimal Price, long Exist, long Reserved,
        long TimeToExe, string Article, string Manufacturer, string CurrencyIso, string CurrencySign,
        string CurrencyShort, decimal CurrencyRate);

    private static async Task<MainOffer?> LoadMainOfferAsync(
        DbConnection connection,
        Request request,
        long productId,
        CancellationToken cancellationToken)
    {
        var offices = await StorefrontCustomerOffices.LoadAsync(connection, request.CityCookie, cancellationToken).ConfigureAwait(false);
        if (offices.Count == 0)
        {
            return null;
        }

        var rows = await RowsAsync(connection,
            "SELECT m.`office_id`,d.`storage_id`,d.`id`,d.`price`*IFNULL(c.`rate`,1)*(1+(m.`markup`/100)),d.`exist`,d.`reserved`,"
            + "d.`time_to_exe`,IFNULL(d.`article`,''),IFNULL(d.`manufacturer`,''),IFNULL(c.`iso_code`,''),IFNULL(c.`sign`,''),"
            + "IFNULL(c.`caption_short`,''),IFNULL(c.`rate`,1) FROM `shop_storages_data` d "
            + "JOIN `shop_offices_storages_map` m ON m.`storage_id`=d.`storage_id` "
            + "LEFT JOIN `shop_storages` s ON s.`id`=d.`storage_id` LEFT JOIN `shop_currencies` c ON c.`iso_code`=s.`currency` "
            + "WHERE d.`product_id`=? AND m.`office_id`=? ORDER BY (d.`price`>0) DESC,d.`price`,d.`id` LIMIT 1",
            [productId, offices[0]], cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return null;
        }

        var r = rows[0];
        return new(
            L(r[0]), L(r[1]), L(r[2]), D(r[3]), L(r[4]), L(r[5]), L(r[6]),
            r[7], r[8], r[9], r[10], r[11], D(r[12]));
    }

    private static async Task<Product?> LoadProductAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(connection,
            "SELECT `id`,IFNULL(`caption`,''),IFNULL(`category_id`,0),IFNULL(`alias`,'') FROM `shop_catalogue_products` WHERE `id`=? LIMIT 1",
            [id], cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : new(L(rows[0][0]), rows[0][1], L(rows[0][2]), rows[0][3]);
    }

    private static async Task<IReadOnlyList<Image>> LoadImagesAsync(DbConnection connection, long id, CancellationToken cancellationToken)
        => (await RowsAsync(connection, "SELECT `id`,IFNULL(`file_name`,'') FROM `shop_products_images` WHERE `product_id`=? ORDER BY `id`", [id], cancellationToken)
                .ConfigureAwait(false))
            .Select(r => new Image(L(r[0]), r[1])).ToList();

    private static async Task<IReadOnlyList<Property>> LoadPropertiesAsync(DbConnection connection, long categoryId, CancellationToken cancellationToken)
        => (await RowsAsync(connection,
                "SELECT `id`,IFNULL(`value`,''),IFNULL(`property_type_id`,0),IFNULL(`list_id`,0) FROM `shop_categories_properties_map` WHERE `category_id`=? ORDER BY `order`",
                [categoryId], cancellationToken).ConfigureAwait(false))
            .Select(r => new Property(L(r[0]), r[1], (int)L(r[2]), L(r[3]))).ToList();

    private static async Task<string> ProductTextAsync(
        DbConnection connection,
        long productId,
        Func<object, Task<string>> t,
        int fallback,
        CancellationToken cancellationToken)
    {
        var rows = await ColumnAsync(connection, "SELECT `content` FROM `shop_products_text` WHERE `product_id`=? LIMIT 1", [productId], cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 || rows[0].Length == 0 ? H(await t(fallback).ConfigureAwait(false)) : H(await t(rows[0]).ConfigureAwait(false));
    }

    private static async Task<(string Caption, string City, string Address)> OfficeAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(connection, "SELECT IFNULL(`caption`,''),IFNULL(`city`,''),IFNULL(`address`,'') FROM `shop_offices` WHERE `id`=? LIMIT 1", [id], cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? ("", "", "") : (rows[0][0], rows[0][1], rows[0][2]);
    }

    private static async Task<List<string[]>> RowsAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        var rows = new List<string[]>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new string[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task<List<string>> ColumnAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
        => (await RowsAsync(connection, sql, args, cancellationToken).ConfigureAwait(false)).Select(r => r[0]).ToList();

    private static string ImageUrl(string fileName)
        => fileName.StartsWith("auto_price/", StringComparison.Ordinal) || fileName.StartsWith("sku_media/", StringComparison.Ordinal)
            ? "/content/files/images/" + fileName
            : fileName.StartsWith("/", StringComparison.Ordinal) || Uri.TryCreate(fileName, UriKind.Absolute, out _)
                ? fileName
                : "/content/files/images/products_images/" + fileName;

    private static string FormatCurrency(decimal amount, string iso, string sign, string shortName, decimal rate, string mode)
    {
        var number = (amount / (rate <= 0 ? 1 : rate)).ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ", StringComparison.Ordinal);
        return mode switch
        {
            "no" => number,
            "sign_after" => number + " " + sign,
            "short_name_after" => number + " " + shortName,
            _ => sign + " " + number
        };
    }

    private static string PurchaseButton(string divId, long exist, string label)
        => "<div class=\"btn-ar btn-primary cart_btn_purchase_action\"><table><tr><td><div class=\"product_div_count_need\">"
           + "<a class=\"count_need_minus\" href=\"javascript:void(0);\" onclick=\"minusCountNeed('" + Js(divId) + "', " + exist + ", 1);\">-</a>"
           + "<input class=\"count_need_input count_need_" + A(divId) + "\" type=\"text\" value=\"1\" onchange=\"onKeyUpCountNeed('" + Js(divId) + "', " + exist + ", 1);\"/>"
           + "<a class=\"count_need_plus\" href=\"javascript:void(0);\" onclick=\"plusCountNeed('" + Js(divId) + "', " + exist + ", 1);\">+</a>"
           + "</div></td><td><a href=\"javascript:void(0);\" onclick=\"purchase_action('" + Js(divId) + "');\">" + H(label) + "</a></td></tr></table></div>";

    private static string RequestButton(string langHref, string label)
        => "<div class=\"btn-ar btn-primary cart_btn_purchase_action\"><table><tr><td><div class=\"product_div_count_need\"></div></td><td><a href=\""
           + A(langHref) + "/zapros-prodavczu\" target=\"_blank\">" + H(label) + "</a></td></tr></table></div>";

    private static string RenderMarketBlock(string caption, string langHref)
        => "<div class=\"epc-auto-price-market-block\"><strong>" + H(caption)
           + "</strong><a href=\"" + A(langHref) + "/zapros-prodavczu\">Request market availability</a></div>\n";

    private static string RenderEvaluationMarkScript(long id, string csrf)
        => "<div id=\"evaluations_general_mark_home\"></div><script>function getGeneralMark1(){jQuery.ajax({type:\"POST\",async:true,"
           + "url:\"/content/shop/catalogue/evaluations/ajax_get_product_general_mark.php\",dataType:\"json\",data:\"product_id="
           + id + "&csrf_guard_key=" + Js(csrf) + "\"});}getGeneralMark1();</script>\n";

    private static async Task<string> RenderBookmark(long id, bool selected, int blockType, Func<object, Task<string>> t)
    {
        if (!selected)
        {
            return "<div class=\"product_div_bookmark\"><a href=\"javascript:void(0);\" onclick=\"addToBookmarks(" + id + ", this);\" title=\""
                   + A(await t(4105).ConfigureAwait(false)) + "\"><i class=\"fa fa-bookmark-o\"></i><span>"
                   + H(await t(4106).ConfigureAwait(false)) + "</span></a></div>\n";
        }

        return blockType == 6
            ? "<div class=\"product_div_bookmark\"><a href=\"javascript:void(0);\" onclick=\"removeBookmark(" + id + ", this);\" title=\""
              + A(await t(4102).ConfigureAwait(false)) + "\"><i class=\"fa fa-remove\"></i><span>" + H(await t(2224).ConfigureAwait(false)) + "</span></a></div>\n"
            : "<div class=\"product_div_bookmark\"><a href=\"javascript:void(0);\" onclick=\"location = '/shop/zakladki';\" title=\""
              + A(await t(4103).ConfigureAwait(false)) + "\"><i class=\"fa fa-bookmark\"></i><span>" + H(await t(4104).ConfigureAwait(false)) + "</span></a></div>\n";
    }

    private static async Task<string> RenderCompare(long id, bool selected, int blockType, Func<object, Task<string>> t)
    {
        if (!selected)
        {
            return "<div class=\"product_div_compare\"><a href=\"javascript:void(0);\" onclick=\"addToCompare(" + id + ", this);\" title=\""
                   + A(await t(4110).ConfigureAwait(false)) + "\"><i class=\"fa fa-copy fa-flip-horizontal\"></i><span>"
                   + H(await t(4111).ConfigureAwait(false)) + "</span></a></div>\n";
        }

        return blockType == 7
            ? "<div class=\"product_div_compare\"><a href=\"javascript:void(0);\" onclick=\"removeCompare(" + id + ", this);\" title=\""
              + A(await t(4107).ConfigureAwait(false)) + "\"><i class=\"fa fa-remove\"></i><span>" + H(await t(2224).ConfigureAwait(false)) + "</span></a></div>\n"
            : "<div class=\"product_div_compare\"><a href=\"javascript:void(0);\" onclick=\"location = '/shop/sravneniya';\" title=\""
              + A(await t(4108).ConfigureAwait(false)) + "\"><i class=\"glyphicon glyphicon-duplicate\"></i><span>" + H(await t(4109).ConfigureAwait(false)) + "</span></a></div>\n";
    }

    private static string Config(Request request, string key)
        => request.Config.TryGetValue(key, out var value) ? value : string.Empty;

    private static long L(string value) => long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static decimal D(string value) => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string A(string? value) => H(value);
    private static string Css(string? value) => (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
    private static string Js(string? value) => Css(value).Replace("</", "<\\/", StringComparison.Ordinal);
}
