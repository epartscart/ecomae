namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-wake BOC page shell. PHP identifiers kept for the inventory:
/// <c>epc_boc_normalize_content_url</c>, <c>epc_boc_resolve_area</c>,
/// <c>epc_boc_should_use_page_shell</c>, <c>epc_boc_page_shell_open</c>,
/// <c>epc_boc_page_shell_close</c>. Path:
/// <c>content/general_pages/epc_boc_page_shell.php</c>.
/// GET never mints a session cookie. Leftover console / portal / tenant-scope parents stay injected.
/// </summary>
public static class PhpPlanQ1Wake
{
    public const string BocPageShellPath = "content/general_pages/epc_boc_page_shell.php";

    public static Func<bool>? IsSuperCpHost { get; set; }
    public static Dictionary<string, Dictionary<string, object?>> Areas { get; set; } = new(StringComparer.Ordinal);
    public static Func<object?>? Nav { get; set; }
    public static Action<Dictionary<string, object?>>? ConsoleOpen { get; set; }
    public static Action? ConsoleClose { get; set; }
    public static Action? TenantSwitch { get; set; }
    public static Func<string>? ScopeLabel { get; set; }
    public static Func<string>? OperatorName { get; set; }
    public static string ContentUrl { get; set; } = "";
    public static string ContentValue { get; set; } = "";
    public static string BackendDir { get; set; } = "cp";
    public static bool AlreadyBocPage { get; set; }
    public static bool ShellOpenFlag { get; set; }
    public static bool SkipPageHeader { get; set; }

    public static void Reset()
    {
        IsSuperCpHost = () => false;
        Areas = new(StringComparer.Ordinal);
        Nav = () => new[] { "home" };
        ConsoleOpen = _ => { };
        ConsoleClose = () => { };
        TenantSwitch = () => { };
        ScopeLabel = () => "";
        OperatorName = () => "";
        ContentUrl = "";
        ContentValue = "";
        BackendDir = "cp";
        AlreadyBocPage = false;
        ShellOpenFlag = false;
        SkipPageHeader = false;
    }

    public static string EpcBocNormalizeContentUrl(string url)
    {
        url = url.Trim().Trim('/').ToLowerInvariant();
        var q = url.IndexOf('?');
        return q >= 0 ? url[..q] : url;
    }

    public static Dictionary<string, object?>? EpcBocResolveArea(string? contentUrl = null)
    {
        var url = EpcBocNormalizeContentUrl(contentUrl ?? ContentUrl);
        if (url == "")
        {
            return null;
        }

        var bestId = "";
        var bestLen = -1;
        Dictionary<string, object?>? bestArea = null;
        foreach (var (id, area) in Areas)
        {
            var path = EpcBocNormalizeContentUrl(Convert.ToString(area.TryGetValue("path", out var p) ? p : "") ?? "");
            if (path == "")
            {
                continue;
            }

            var pathBase = path;
            var pq = pathBase.IndexOf('?');
            if (pq >= 0)
            {
                pathBase = pathBase[..pq];
            }

            if (url == pathBase || url.StartsWith(pathBase + "/", StringComparison.Ordinal))
            {
                var len = pathBase.Length;
                if (len > bestLen)
                {
                    bestLen = len;
                    bestId = id;
                    bestArea = area;
                }
            }
        }

        if (bestId == "" || bestArea == null)
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = bestId,
            ["area"] = bestArea
        };
    }

    public static bool EpcBocShouldUsePageShell(string? contentUrl = null)
    {
        if (AlreadyBocPage || ShellOpenFlag)
        {
            return false;
        }

        if (IsSuperCpHost == null || !IsSuperCpHost())
        {
            return false;
        }

        var url = EpcBocNormalizeContentUrl(contentUrl ?? ContentUrl);
        if (url == "" || url == "control")
        {
            return false;
        }

        if (url is "login" or "logout" or "control/login" or "control/logout")
        {
            return false;
        }

        return true;
    }

    public static Dictionary<string, object?> EpcBocPageShellOpen(Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        if (ShellOpenFlag || AlreadyBocPage)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["opened"] = 0, ["skip_header"] = SkipPageHeader ? 1 : 0 };
        }

        if (IsSuperCpHost == null || !IsSuperCpHost())
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["opened"] = 0, ["skip_header"] = SkipPageHeader ? 1 : 0 };
        }

        var backend = BackendDir.Trim('/');
        if (backend == "")
        {
            backend = "cp";
        }

        var resolved = EpcBocResolveArea();
        var active = Str(opts, "active");
        if (active == "" && resolved != null)
        {
            active = Convert.ToString(resolved["id"]) ?? "";
        }

        var title = Str(opts, "title");
        if (title == "" && resolved != null && resolved["area"] is Dictionary<string, object?> area)
        {
            title = Convert.ToString(area.TryGetValue("label", out var l) ? l : "") ?? "";
        }

        if (title == "")
        {
            title = ContentValue;
        }

        if (title == "")
        {
            title = "Operations";
        }

        var op = Str(opts, "operator");
        if (op == "")
        {
            op = OperatorName != null ? OperatorName() : "";
        }

        if (op == "")
        {
            op = "Operator";
        }

        object? nav;
        if (opts.TryGetValue("nav", out var navOpt) && navOpt is System.Collections.IEnumerable)
        {
            nav = navOpt;
        }
        else
        {
            nav = Nav != null ? Nav() : new[] { "home" };
        }

        TenantSwitch?.Invoke();
        var scope = Str(opts, "scope");
        if (scope == "")
        {
            scope = ScopeLabel != null ? ScopeLabel() : "";
        }

        if (scope == "")
        {
            scope = "Platform · All tenants";
        }

        var ctx = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["active"] = active,
            ["title"] = title,
            ["subtitle"] = Str(opts, "subtitle"),
            ["base"] = "/" + backend,
            ["operator"] = op,
            ["env"] = opts.TryGetValue("env", out var env) && Convert.ToString(env) is { Length: > 0 } e ? e : "Production",
            ["nav"] = nav,
            ["scope"] = scope,
            ["layout"] = "top"
        };
        ConsoleOpen?.Invoke(ctx);
        ShellOpenFlag = true;
        SkipPageHeader = true;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["opened"] = 1,
            ["skip_header"] = 1,
            ["ctx"] = ctx
        };
    }

    public static Dictionary<string, object?> EpcBocPageShellClose()
    {
        if (!ShellOpenFlag)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["closed"] = 0, ["open"] = 0 };
        }

        ConsoleClose?.Invoke();
        ShellOpenFlag = false;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["closed"] = 1, ["open"] = 0 };
    }

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v) ?? "" : "";
}
