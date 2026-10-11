using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-beck storefront slider module.
/// Path: <c>modules/slider/slider.php</c> (basename is not unique).
/// GET never mints a session cookie. Settings and images stay injected per tenant.
/// </summary>
public static class PhpPlanQ1Beck
{
    public const string SliderModulePath = "modules/slider/slider.php";

    public static Func<Dictionary<string, object?>?>? QuerySettings { get; set; }
    public static Func<List<Dictionary<string, object?>>>? QueryImages { get; set; }
    public static Func<bool>? MainFlag { get; set; }
    public static Func<object?>? MenuCategoryShow { get; set; }
    public static Func<object?>? HeaderStyle { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        QuerySettings = null;
        QueryImages = null;
        MainFlag = null;
        MenuCategoryShow = null;
        HeaderStyle = null;
        LastOutput = "";
    }

    public static string EpcSliderModule()
    {
        var settings = QuerySettings?.Invoke();
        if (settings is null || !PhpEqOne(settings.GetValueOrDefault("connected")))
        {
            LastOutput = "";
            return LastOutput;
        }

        var images = QueryImages?.Invoke() ?? new List<Dictionary<string, object?>>();
        if (images.Count == 0)
        {
            LastOutput = "";
            return LastOutput;
        }

        var items = "";
        foreach (var img in images)
        {
            var href = Str(img.GetValueOrDefault("href"));
            var link = img.GetValueOrDefault("link");
            items += PhpEmpty(link)
                ? "<li><img class=\"navbar-inverse\" src=\"" + href + "\" /></li>"
                : "<li><a href=\"" + Str(link) + "\"><img class=\"navbar-inverse\" src=\"" + href + "\" /></a></li>";
        }

        var cnt = Str(settings.GetValueOrDefault("cnt_img"));
        var next = Str(settings.GetValueOrDefault("cnt_img_next"));
        var time = Str(settings.GetValueOrDefault("time_next"));
        var chrome = (MainFlag?.Invoke() ?? false) && !PhpEmpty(MenuCategoryShow?.Invoke());
        var noHeader = Str(HeaderStyle?.Invoke()) == "no_header";
        LastOutput =
            "\t\t\t<div class=\"slider_box\">\n" +
            "\t\t\t\t<div class=\"row\">\n" +
            "\t\t\t\t\t\t\t\t\t\t<script type=\"text/javascript\" src=\"/modules/slider/js/jquery.flexisel.js\"></script>\n" +
            "\t\t\t\t\t<div class=\"col-lg-12\">\n" +
            "\t\t\t\t\t\t<ul id=\"flexiselDemo3\">\n" +
            "\t\t\t\t\t\t\t" + items + "                                     \n" +
            "\t\t\t\t\t\t</ul>\n" +
            "\t\t\t\t\t</div>\n" +
            "\t\t\t\t</div>\n" +
            "\t\t\t</div>\n" +
            "\t\t\t<script type=\"text/javascript\">\n" +
            "\t\t\t\t$(window).load(function() {\n" +
            "\t\t\t\t\t$(\"#flexiselDemo3\").flexisel({\n" +
            "\t\t\t\t\t\tvisibleItems: " + cnt + ",// Количество отображаемых картинок на странице\n" +
            "\t\t\t\t\t\titemsToScroll: " + next + ",// Количество картинок которые нужно пролистать при клике\n" +
            "\t\t\t\t\t\tautoPlay: {\n" +
            "\t\t\t\t\t\t\tenable: true,\n" +
            "\t\t\t\t\t\t\tinterval: " + time + ",// Время через которое листать картинки\n" +
            "\t\t\t\t\t\t\tpauseOnHover: true\n" +
            "\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t});\n" +
            ChromeJs(chrome, noHeader) +
            "\t\t\t</script>\n\t\t";
        return LastOutput;
    }

    private static string ChromeJs(bool chrome, bool noHeader)
    {
        if (!chrome)
        {
            return "\t\t\t\t\t\t\t\t\t});\n";
        }

        var inner = noHeader
            ? "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\tif($(\".nav_cat\").length > 0){\n" +
              "\t\t\t\t\t\t\t\t\t$(\".nav_cat\").css('position','static');\n" +
              "\t\t\t\t\t\t\t\t\t$(\".nav_cat > li\").css('position','static');\n" +
              "\t\t\t\t\t\t\t\t\t$(\".keep_open \").css('left','0');\n" +
              "\t\t\t\t\t\t\t\t}\n" +
              "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t}\n"
            : "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t}\n";
        return
            "\t\t\t\t\t\t\t\t\t\t\tif ($(window).width() >= 1200){\n" +
            "\t\t\t\t\t\t\tif ($('.keep_open').length > 0){\n" +
            "\t\t\t\t\t\t\t\tif(!$('.keep_open').hasClass('open_t')){\n" +
            "\t\t\t\t\t\t\t\t\t$('.nav .panel-collapse').collapse('hide');\n" +
            "\t\t\t\t\t\t\t\t\t$('.nav .dropdown-menu').removeClass('open_t');\n" +
            "\t\t\t\t\t\t\t\t\t$('.keep_open').addClass('open_t');\n" +
            "\t\t\t\t\t\t\t\t\t$(\".slider_box \").css('margin','0px 0px 0px 200px');\n" +
            "\t\t\t\t\t\t\t\t}\n" +
            inner +
            "\t\t\t\t\t\t}\n" +
            "\t\t\t\t\t\t\t\t\t});\n";
    }

    private static bool PhpEqOne(object? value)
    {
        if (value is null or false or "")
        {
            return false;
        }

        if (value is true)
        {
            return true;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) == 1;
        }
        catch
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) == "1";
        }
    }

    private static bool PhpEmpty(object? value)
    {
        if (value is null or false or "")
        {
            return true;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (text is null or "" or "0")
        {
            return true;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
