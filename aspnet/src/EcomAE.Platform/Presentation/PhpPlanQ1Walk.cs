using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-walk helpers. PHP identifiers kept for the inventory:
/// <c>epc_mv_min_price_acl_ensure</c>, <c>epc_mv_min_price_acl_defaults</c>,
/// <c>epc_mv_min_price_acl_get</c>, <c>epc_mv_min_price_acl_save</c>,
/// <c>epc_mv_min_price_is_admin_viewer</c>, <c>epc_mv_min_price_viewer_may_see</c>,
/// <c>epc_mv_min_price_is_min_row</c>, <c>epc_mv_min_price_is_max_row</c>,
/// <c>epc_mv_min_price_display_storage</c>, <c>epc_mv_min_price_list_is_typed</c>,
/// <c>epc_mv_min_price_should_hide_row</c>, <c>epc_mv_min_price_list_customer_groups</c>,
/// <c>EPC_MV_MIN_TIER</c>, <c>EPC_MV_MAX_TIER</c>.
/// </summary>
public static class PhpPlanQ1Walk
{
    public const string MinPriceAclPath = "content/shop/docpart/epc_multivendor_min_price_acl.php";
    public const string EpcMvMinTier = "epc_mv_min";
    public const string EpcMvMaxTier = "epc_mv_max";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<bool> IsAdmin { get; set; } = () => false;
    public static Func<bool> IsAdminGroup { get; set; } = () => false;
    public static Func<int> AdminId { get; set; } = () => 0;
    public static Func<int> SessionUser { get; set; } = () => 0;
    public static Func<int, List<int>> ProfileGroups { get; set; } = _ => new List<int>();
    public static bool HasUserClass { get; set; } = true;
    private static bool _ensureDone;

    public static void Reset()
    {
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IsAdmin = () => false;
        IsAdminGroup = () => false;
        AdminId = () => 0;
        SessionUser = () => 0;
        ProfileGroups = _ => new List<int>();
        HasUserClass = true;
        _ensureDone = false;
    }

    public sealed class AclStore
    {
        public bool ThrowOnWrite { get; set; }
        public AclRow? Row { get; set; }
        public List<GroupRow> Groups { get; } = new();
    }

    public sealed class AclRow
    {
        public int RestrictMin { get; set; } = 1;
        public string GroupIdsJson { get; set; } = "[]";
        public string UserIdsJson { get; set; } = "[]";
        public long UpdatedAt { get; set; }
        public int UpdatedBy { get; set; }
    }

    public sealed class GroupRow
    {
        public int Id { get; set; }
        public string Value { get; set; } = "";
        public int ForBackend { get; set; }
    }

    public static Dictionary<string, object?> EpcMvMinPriceAclDefaults()
        => new(StringComparer.Ordinal)
        {
            ["restrict"] = true,
            ["group_ids"] = new List<int>(),
            ["user_ids"] = new List<int>()
        };

    public static void EpcMvMinPriceAclEnsure(AclStore db)
    {
        if (_ensureDone)
        {
            return;
        }

        _ensureDone = true;
        try
        {
            if (db.ThrowOnWrite)
            {
                throw new InvalidOperationException("ro");
            }

            if (db.Row is null)
            {
                db.Row = new AclRow
                {
                    RestrictMin = 1,
                    GroupIdsJson = "[]",
                    UserIdsJson = "[]",
                    UpdatedAt = Clock(),
                    UpdatedBy = 0
                };
            }
        }
        catch
        {
            // Table create may fail on read-only replicas; callers fall back to defaults.
        }
    }

    public static Dictionary<string, object?> EpcMvMinPriceAclGet(AclStore db)
    {
        var output = EpcMvMinPriceAclDefaults();
        EpcMvMinPriceAclEnsure(db);
        try
        {
            var row = db.Row;
            if (row is null)
            {
                return output;
            }

            output["restrict"] = !IsEmpty(row.RestrictMin);
            output["group_ids"] = PositiveInts(row.GroupIdsJson);
            output["user_ids"] = PositiveInts(row.UserIdsJson);
        }
        catch
        {
            return output;
        }

        return output;
    }

    public static bool EpcMvMinPriceAclSave(AclStore db, IReadOnlyDictionary<string, object?> acl, int updatedBy = 0)
    {
        EpcMvMinPriceAclEnsure(db);
        var restrict = !IsEmpty(acl.TryGetValue("restrict", out var r) ? r : null) ? 1 : 0;
        var groupIds = UniquePositive(acl.TryGetValue("group_ids", out var g) ? g : null);
        var userIds = UniquePositive(acl.TryGetValue("user_ids", out var u) ? u : null);
        try
        {
            if (db.ThrowOnWrite)
            {
                throw new InvalidOperationException("ro");
            }

            db.Row = new AclRow
            {
                RestrictMin = restrict,
                GroupIdsJson = JsonSerializer.Serialize(groupIds, JsonOpts),
                UserIdsJson = JsonSerializer.Serialize(userIds, JsonOpts),
                UpdatedAt = Clock(),
                UpdatedBy = Math.Max(0, updatedBy)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool EpcMvMinPriceIsAdminViewer()
    {
        if (!HasUserClass)
        {
            return false;
        }

        try
        {
            if (IsAdmin())
            {
                return true;
            }

            if (IsAdminGroup())
            {
                return true;
            }

            if (AdminId() > 0)
            {
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static bool EpcMvMinPriceViewerMaySee(AclStore db, int? userId = null)
    {
        if (EpcMvMinPriceIsAdminViewer())
        {
            return true;
        }

        var acl = EpcMvMinPriceAclGet(db);
        if (IsEmpty(acl["restrict"]))
        {
            return true;
        }

        var uid = userId ?? (HasUserClass ? SessionUser() : 0);
        if (uid > 0 && ((List<int>)acl["user_ids"]!).Contains(uid))
        {
            return true;
        }

        var allowed = (List<int>)acl["group_ids"]!;
        if (uid > 0 && allowed.Count > 0)
        {
            var profileGroups = HasUserClass ? ProfileGroups(uid) : new List<int>();
            foreach (var g in allowed)
            {
                if (profileGroups.Contains(g))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool EpcMvMinPriceIsMinRow(string? storage)
    {
        var s = (storage ?? "").Trim().ToLowerInvariant();
        return s == EpcMvMinTier || s == "min" || s.StartsWith(EpcMvMinTier, StringComparison.Ordinal);
    }

    public static bool EpcMvMinPriceIsMaxRow(string? storage)
    {
        var s = (storage ?? "").Trim().ToLowerInvariant();
        return s == EpcMvMaxTier || s == "max" || s.StartsWith(EpcMvMaxTier, StringComparison.Ordinal);
    }

    public static string EpcMvMinPriceDisplayStorage(string? storage)
    {
        var s = (storage ?? "").Trim();
        return EpcMvMinPriceIsMinRow(s) || EpcMvMinPriceIsMaxRow(s) ? "" : s;
    }

    public static bool EpcMvMinPriceListIsTyped(string? listName)
    {
        var name = listName ?? "";
        return name.Contains(" · Sales", StringComparison.Ordinal)
               || name.Contains(" · Purchase", StringComparison.Ordinal)
               || name.Contains(" sales", StringComparison.OrdinalIgnoreCase)
               || name.Contains(" purchase", StringComparison.OrdinalIgnoreCase);
    }

    public static bool EpcMvMinPriceShouldHideRow(AclStore db, IReadOnlyDictionary<string, object?> row, int? userId = null)
    {
        var storage = row.TryGetValue("storage", out var s) ? Convert.ToString(s, CultureInfo.InvariantCulture) ?? "" : "";
        if (!EpcMvMinPriceIsMinRow(storage))
        {
            return false;
        }

        return !EpcMvMinPriceViewerMaySee(db, userId);
    }

    public static List<Dictionary<string, object?>> EpcMvMinPriceListCustomerGroups(AclStore db)
    {
        try
        {
            return db.Groups
                .Where(g => g.ForBackend == 0 && g.Id > 0 && g.Value.Trim() != "")
                .OrderBy(g => g.Value, StringComparer.Ordinal)
                .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = g.Id, ["value"] = g.Value.Trim() })
                .ToList();
        }
        catch
        {
            return new List<Dictionary<string, object?>>();
        }
    }

    private static List<int> PositiveInts(string json)
    {
        var output = new List<int>();
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrEmpty(json) ? "[]" : json);
            if (parsed.ValueKind != JsonValueKind.Array)
            {
                return output;
            }

            foreach (var el in parsed.EnumerateArray())
            {
                var n = el.ValueKind == JsonValueKind.Number ? el.GetInt32() : ToInt(el.GetString());
                if (n > 0)
                {
                    output.Add(n);
                }
            }
        }
        catch (JsonException)
        {
            return output;
        }

        return output;
    }

    private static List<int> UniquePositive(object? raw)
    {
        var seen = new Dictionary<int, int>();
        foreach (var item in AsList(raw))
        {
            var n = ToInt(item);
            if (n > 0)
            {
                seen[n] = n;
            }
        }

        return seen.Values.ToList();
    }

    private static List<object?> AsList(object? value)
        => value switch
        {
            List<object?> list => list,
            List<int> ints => ints.Cast<object?>().ToList(),
            IEnumerable<object?> e => e.ToList(),
            _ => new List<object?>()
        };

    private static int ToInt(object? value)
    {
        if (value is null or false)
        {
            return 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static bool IsEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 or 0L or 0d => true,
            "" or "0" => true,
            IReadOnlyCollection<object?> c => c.Count == 0,
            IReadOnlyCollection<int> i => i.Count == 0,
            _ => false
        };
}
