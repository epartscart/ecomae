namespace EcomAE.Platform.Presentation;

/// <summary>
/// Rewrites PHP product hrefs to ASP.NET browse routes.
/// PHP stays available only under /php-reference/* (never as a primary click target).
/// </summary>
public static class PhpSurfaceLinkMap
{
    public static string AspNetPrimaryHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "/";
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && (absolute.Host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase)))
        {
            value = string.IsNullOrEmpty(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath;
            if (!string.IsNullOrEmpty(absolute.Query))
            {
                value += absolute.Query;
            }
        }

        // Uppercase PHP shells / deep modules → ASP.NET shell browse (never /CP/ /ERP/ /BOS/).
        if (IsUpperPhpShell(value, "CP"))
        {
            return "/cp";
        }

        if (IsUpperPhpShell(value, "ERP"))
        {
            return "/erp";
        }

        if (IsUpperPhpShell(value, "BOS"))
        {
            return "/bos";
        }

        if (value.StartsWith("/php-reference/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal))
        {
            return value;
        }

        if (value.StartsWith("/shop/cart", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/cart-app";
        }

        if (value.StartsWith("/shop/checkout", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/checkout-app";
        }

        if (value.StartsWith("/shop/orders", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/orders-app";
        }

        if (value.StartsWith("/shop/part_search", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/search", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/search-app";
        }

        if (value.Contains("garage", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/garage-app";
        }

        if (value.StartsWith("/users", StringComparison.OrdinalIgnoreCase))
        {
            return "/storefront/login";
        }

        if (value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        return "/";
    }

    public static string PhpReferenceOnlyHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "/php-reference/home";
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            value = absolute.AbsolutePath;
        }

        if (IsUpperPhpShell(value, "CP")
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/cp";
        }

        if (IsUpperPhpShell(value, "ERP")
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/erp";
        }

        if (IsUpperPhpShell(value, "BOS")
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/bos";
        }

        if (value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal)
            || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/home";
        }

        return "/php-reference/storefront";
    }

    public static bool IsPhpProductHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && (absolute.Host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IsUpperPhpShell(value, "CP")
            || IsUpperPhpShell(value, "ERP")
            || IsUpperPhpShell(value, "BOS")
            || value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".php", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpperPhpShell(string value, string shell)
    {
        // Product PHP chrome uses uppercase /CP /ERP /BOS (catalog + legacy nav).
        var prefix = "/" + shell;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            || value.StartsWith(prefix + "/", StringComparison.Ordinal)
            || value.StartsWith(prefix + "?", StringComparison.Ordinal);
    }
}
