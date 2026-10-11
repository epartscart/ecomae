using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-eddy in-CP brochure page.
/// PHP identifier kept for the inventory: <c>epc_cp_full_brochure_render_and_exit</c>.
/// Path: <c>cp/content/control/epc_cp_brochure_page.php</c>.
/// GET never mints a session cookie. Leftover unique Super-CP host check stays injected
/// and is not named here. Full-brochure HTML stays with Starboard.
/// </summary>
public static class PhpPlanQ1Eddy
{
    public const string CpBrochurePagePath = "cp/content/control/epc_cp_brochure_page.php";

    public static Func<bool>? IsSuperCp { get; set; }
    public static bool HasConfig { get; set; }
    public static object? BackendDir { get; set; }
    public static IReadOnlyDictionary<string, string>? Query { get; set; }
    public static Action<Dictionary<string, object?>>? RenderAndExit { get; set; }
    public static Dictionary<string, object?> LastOpts { get; private set; } = [];
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        IsSuperCp = null;
        HasConfig = false;
        BackendDir = null;
        Query = null;
        RenderAndExit = null;
        LastOpts = [];
        LastOutput = "";
    }

    public static string EpcCpFullBrochureRenderAndExit(Dictionary<string, object?> opts)
    {
        LastOpts = opts;
        if (RenderAndExit is not null)
        {
            RenderAndExit(opts);
            return LastOutput;
        }

        LastOutput = Format(opts);
        return LastOutput;
    }

    public static string EpcCpBrochurePage()
    {
        var isSuper = IsSuperCp?.Invoke() == true;
        var rawScope = Query is not null && Query.ContainsKey("scope")
            ? Query["scope"]
            : isSuper ? "all" : "client";
        var scope = Regex.Replace(Str(rawScope).ToLowerInvariant(), "[^a-z]", "");
        if (scope == "")
        {
            scope = isSuper ? "all" : "client";
        }

        var print = Query is not null && Query.ContainsKey("print") && Str(Query["print"]) == "1";
        var brand = isSuper ? "ecomae" : "epartscart";
        var backend = "cp";
        if (HasConfig && !PhpEmpty(BackendDir))
        {
            backend = Str(BackendDir).Trim('/');
        }

        if (backend == "")
        {
            backend = "cp";
        }

        return EpcCpFullBrochureRenderAndExit(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["brand"] = brand,
            ["scope"] = scope,
            ["print"] = print,
            ["base_path"] = "/" + backend + "/control/cp_brochure"
        });
    }

    public static string Format(Dictionary<string, object?> opts)
        => "brand=" + Str(opts.GetValueOrDefault("brand")) + "\n"
           + "scope=" + Str(opts.GetValueOrDefault("scope")) + "\n"
           + "print=" + (PhpEmpty(opts.GetValueOrDefault("print")) ? "0" : "1") + "\n"
           + "base_path=" + Str(opts.GetValueOrDefault("base_path")) + "\n";

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
