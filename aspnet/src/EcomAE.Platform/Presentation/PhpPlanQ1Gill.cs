using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-gill BOC channel-control wrapper.
/// PHP identifiers kept for the inventory: <c>epc_boc_render_channel_control</c>,
/// <c>epc_boc_h</c>, <c>epc_boc_nav</c>, <c>epc_boc_nav_for_user</c>.
/// Path: <c>cp/content/control/portal/epc_boc_channel_control.php</c>.
/// GET never mints a session cookie. Leftover unique portal, leftover unique console
/// parent, leftover unique user helper, and Super-CP host check stay injected and
/// are not named here.
/// </summary>
public static class PhpPlanQ1Gill
{
    public const string BocChannelControlPath = "cp/content/control/portal/epc_boc_channel_control.php";
    public const string DeniedSuper =
        "<div class=\"alert alert-warning\">Channel control is available on <strong>BOC</strong> (Super CP) only.</div>";
    public const string DeniedDb =
        "<div class=\"alert alert-danger\">Platform database unavailable.</div>";

    public static Func<bool>? IsSuperCp { get; set; }
    public static Func<bool>? IsAdmin { get; set; }
    public static object? BackendDir { get; set; }
    public static bool HasPdo { get; set; }
    public static bool HasUserClass { get; set; }
    public static bool HasGetName { get; set; }
    public static bool HasGetUserId { get; set; }
    public static bool HasNavForUser { get; set; }
    public static string OperatorName { get; set; } = "";
    public static int OperatorId { get; set; }
    public static string LastBase { get; private set; } = "";
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        IsSuperCp = null;
        IsAdmin = null;
        BackendDir = null;
        HasPdo = false;
        HasUserClass = false;
        HasGetName = false;
        HasGetUserId = false;
        HasNavForUser = false;
        OperatorName = "";
        OperatorId = 0;
        LastBase = "";
        LastOutput = "";
    }

    public static string EpcBocH(object? value)
        => Str(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static object EpcBocNav()
        => new Dictionary<string, object?>(StringComparer.Ordinal) { ["fleet"] = 1 };

    public static object EpcBocNavForUser(object? db, int userId)
        => new Dictionary<string, object?>(StringComparer.Ordinal) { ["user"] = userId };

    public static void EpcBocRenderChannelControl(object? db, string basePath, object? rollup = null)
        => LastOutput += "CHANNEL:" + basePath;

    public static string EpcBocChannelControl()
    {
        if (IsSuperCp?.Invoke() != true)
        {
            LastBase = "";
            LastOutput = DeniedSuper;
            return LastOutput;
        }

        if (IsAdmin?.Invoke() != true)
        {
            LastBase = "";
            LastOutput = "<div class=\"alert alert-warning\">Please <a href=\"/" + EpcBocH(BackendDir) + "/\">log in to BOC</a>.</div>";
            return LastOutput;
        }

        if (!HasPdo)
        {
            LastBase = "";
            LastOutput = DeniedDb;
            return LastOutput;
        }

        var backend = Str(BackendDir ?? "cp");
        var basePath = "/" + backend.Trim('/');
        var operatorName = HasUserClass && HasGetName && Str(OperatorName) != "" ? Str(OperatorName) : "Operator";
        var opId = HasUserClass && HasGetUserId ? OperatorId : 0;
        _ = HasNavForUser ? EpcBocNavForUser(null, opId) : EpcBocNav();
        var navUsed = HasNavForUser ? "user:" + opId.ToString(CultureInfo.InvariantCulture) : "fleet";
        LastBase = basePath;
        LastOutput = "CONSOLE_OPEN:channel_control|Channels & Orders|" + basePath + "|" + operatorName + "|All units · Fleet|" + navUsed;
        EpcBocRenderChannelControl(null, basePath);
        LastOutput += "CONSOLE_CLOSE";
        return LastOutput;
    }

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
