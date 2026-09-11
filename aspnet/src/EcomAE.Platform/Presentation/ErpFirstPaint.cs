using System.Data.Common;
using System.Diagnostics;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// SSR first-paint budget: 3s wall clock for ERP, CP, BOS, and storefront.
/// Caps SQL to remaining time, clamps list pages, and memoizes companies per
/// request so chrome does not open the shop DB three times. Does not flip PHP
/// serving or cutover flags. Cloudflare 524 is an origin timeout — this budget
/// fails-soft instead of hanging.
/// </summary>
public static class ErpFirstPaint
{
    public const int WallClockMilliseconds = 3000;
    public const int CommandTimeoutSeconds = 1;
    public const int ListLimit = 50;
    public const int PickerLimit = 80;
    public const int AgingScanLimit = 200;
    public const int PhpBridgeTimeoutSeconds = 1;
    public const string CompaniesCacheKey = "ecomae.erp.companies-digest";
    public const string StartedItemKey = "ecomae.first-paint.started";

    private static readonly AsyncLocal<HttpContext?> Bound = new();

    public static HttpContext? Current => Bound.Value;

    public static bool IsErpPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/erp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Product HTML that must paint within <see cref="WallClockMilliseconds"/>:
    /// ERP, CP, BOS, storefront home, and lang-prefixed storefront.
    /// </summary>
    public static bool IsPaintPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.Length == 0 || value == "/")
        {
            return true;
        }

        if (IsErpPath(path)
            || value.StartsWith("/cp", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/bos", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var lang in StorefrontLangPrefix.All)
        {
            if (value.Equals(lang, StringComparison.OrdinalIgnoreCase)
                || value.Equals(lang + "/", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith(lang + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void Observe(HttpContext? context)
    {
        if (context is null || !IsPaintPath(context.Request.Path))
        {
            Bound.Value = null;
            return;
        }

        if (!context.Items.ContainsKey(StartedItemKey))
        {
            context.Items[StartedItemKey] = Stopwatch.GetTimestamp();
        }

        Bound.Value = context;
    }

    public static bool IsActive => Current is not null;

    public static int ElapsedMilliseconds
    {
        get
        {
            var context = Current;
            if (context?.Items.TryGetValue(StartedItemKey, out var boxed) != true || boxed is not long started)
            {
                return 0;
            }

            return (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    public static int RemainingMilliseconds => IsActive
        ? Math.Max(0, WallClockMilliseconds - ElapsedMilliseconds)
        : WallClockMilliseconds;

    /// <summary>True when the 3s wall clock is spent — skip more SQL/bridge work and paint.</summary>
    public static bool IsExpired => IsActive && RemainingMilliseconds <= 0;

    public static int RemainingCommandTimeoutSeconds
    {
        get
        {
            if (!IsActive)
            {
                return CommandTimeoutSeconds;
            }

            var ms = RemainingMilliseconds;
            if (ms <= 0)
            {
                return 1;
            }

            return Math.Clamp((ms + 999) / 1000, 1, CommandTimeoutSeconds);
        }
    }

    /// <summary>Apply the remaining first-paint budget (force-cap even when a caller set 8–30s).</summary>
    public static void ApplyIfUnset(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsActive)
        {
            if (command.CommandTimeout is 0 or 30)
            {
                command.CommandTimeout = CommandTimeoutSeconds;
            }

            return;
        }

        var cap = RemainingCommandTimeoutSeconds;
        if (command.CommandTimeout <= 0 || command.CommandTimeout > cap)
        {
            command.CommandTimeout = cap;
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

    /// <summary>PHP warehouse bridge stays Classic, but SSR must not wait past the 3s wall clock.</summary>
    public static int ClampPhpBridgeTimeout(int requestedSeconds)
    {
        var n = Math.Clamp(requestedSeconds, 1, 60);
        return IsActive ? Math.Min(n, Math.Min(PhpBridgeTimeoutSeconds, RemainingCommandTimeoutSeconds)) : n;
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
