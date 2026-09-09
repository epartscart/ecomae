using System.Data.Common;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// ERP SSR first-paint budget: ~2s wall clock. Caps SQL, clamps list pages, and
/// memoizes companies per request so chrome does not open the shop DB three times.
/// Does not flip PHP serving or cutover flags.
/// </summary>
public static class ErpFirstPaint
{
    public const int CommandTimeoutSeconds = 2;
    public const int ListLimit = 50;
    public const int PickerLimit = 80;
    public const int AgingScanLimit = 200;
    public const string CompaniesCacheKey = "ecomae.erp.companies-digest";

    private static readonly AsyncLocal<HttpContext?> Bound = new();

    public static HttpContext? Current => Bound.Value;

    public static bool IsErpPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/erp", StringComparison.OrdinalIgnoreCase);
    }

    public static void Observe(HttpContext? context)
    {
        Bound.Value = context is not null && IsErpPath(context.Request.Path) ? context : null;
    }

    public static bool IsActive => Current is not null && IsErpPath(Current.Request.Path);

    /// <summary>Apply the 2s budget when the command still has the 30s pool default.</summary>
    public static void ApplyIfUnset(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CommandTimeout is 0 or 30)
        {
            command.CommandTimeout = CommandTimeoutSeconds;
        }
    }

    public static void ApplyIfErp(DbCommand command)
    {
        if (IsActive)
        {
            ApplyIfUnset(command);
        }
    }

    public static int ClampList(int requested)
    {
        var n = Math.Max(1, requested);
        return IsActive ? Math.Min(n, ListLimit) : n;
    }

    public static object ClampLimitValue(string name, object value)
    {
        if (!string.Equals(name, "@limit", StringComparison.Ordinal) || !IsActive)
        {
            return value;
        }

        if (value is int i)
        {
            return ClampList(i);
        }

        if (value is long l)
        {
            return (long)ClampList((int)Math.Min(l, int.MaxValue));
        }

        return value;
    }

    public static bool TryGetRequestCache<T>(HttpContext? context, string key, out T? value)
    {
        if (context?.Items.TryGetValue(key, out var boxed) == true && boxed is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public static void SetRequestCache<T>(HttpContext? context, string key, T value)
    {
        if (context is not null)
        {
            context.Items[key] = value!;
        }
    }
}
