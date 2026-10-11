using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hope BOC vendor-control and warehouse-control wrappers.
/// PHP identifiers kept for the inventory: <c>epc_boc_render_vendor_control</c>,
/// <c>epc_boc_render_warehouse_control</c>, <c>epc_boc_h</c>, <c>epc_boc_nav</c>,
/// <c>epc_boc_nav_for_user</c>.
/// Paths: <c>cp/content/control/portal/epc_boc_vendor_control.php</c>,
/// <c>cp/content/control/portal/epc_boc_warehouse_control.php</c>.
/// GET never mints a session cookie. Leftover unique portal, leftover unique console
/// parent, leftover unique user helper, and Super-CP host check stay injected and
/// are not named here.
/// </summary>
public static class PhpPlanQ1Hope
{
    public const string BocVendorControlPath = "cp/content/control/portal/epc_boc_vendor_control.php";
    public const string BocWarehouseControlPath = "cp/content/control/portal/epc_boc_warehouse_control.php";
    public const string DeniedVendor =
        "<div class=\"alert alert-warning\">Vendor control is available on <strong>BOC</strong> (Super CP) only.</div>";
    public const string DeniedWarehouse =
        "<div class=\"alert alert-warning\">Warehouse control is available on <strong>BOC</strong> (Super CP) only.</div>";
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

    public static void EpcBocRenderVendorControl(object? db, string basePath)
        => LastOutput += "VENDOR:" + basePath;

    public static void EpcBocRenderWarehouseControl(object? db, string basePath)
        => LastOutput += "WAREHOUSE:" + basePath;

    public static string EpcBocVendorControl()
        => Render("vendor_control", "Vendor & Sourcing", DeniedVendor, EpcBocRenderVendorControl);

    public static string EpcBocWarehouseControl()
        => Render("warehouse_control", "Warehouse & Inventory", DeniedWarehouse, EpcBocRenderWarehouseControl);

    private static string Render(
        string active,
        string title,
        string deniedSuper,
        Action<object?, string> renderBody)
    {
        if (IsSuperCp?.Invoke() != true)
        {
            LastBase = "";
            LastOutput = deniedSuper;
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
        LastOutput = "CONSOLE_OPEN:" + active + "|" + title + "|" + basePath + "|" + operatorName + "|All units · Fleet|" + navUsed;
        renderBody(null, basePath);
        LastOutput += "CONSOLE_CLOSE";
        return LastOutput;
    }

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
