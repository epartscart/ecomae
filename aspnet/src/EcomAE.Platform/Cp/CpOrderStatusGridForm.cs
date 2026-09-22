using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Converts the PHP-style editable status grid (row index list + per-row fields) posted by
/// /cp/order-statuses-app into the JSON arrays accepted by <see cref="ICpOrderStatusWriteService"/>.
/// Rows absent from the grid are deleted by the writer, mirroring cp/content/shop/orders/statuses.
/// </summary>
public static class CpOrderStatusGridForm
{
    public const string OrderPrefix = "os";
    public const string ItemPrefix = "is";

    public static readonly string[] OrderFlags =
    [
        "for_created", "for_paid", "for_finish", "for_inverse",
        "to_manager_email", "to_manager_sms", "to_customer_email", "to_customer_sms",
    ];

    public static readonly string[] ItemFlags =
    [
        "for_created", "for_finish", "count_flag", "issue_flag",
        "to_manager_email", "to_manager_sms", "to_customer_email", "to_customer_sms",
        "for_return", "check_for_return", "complete_return", "reject_return",
    ];

    public static bool HasGrid(IFormCollection form)
        => form.ContainsKey(OrderPrefix + "_idx") || form.ContainsKey(ItemPrefix + "_idx");

    public static string? ToJson(IFormCollection form, string prefix, string[] flags)
    {
        if (!form.TryGetValue(prefix + "_idx", out var indexes))
        {
            return null;
        }

        var rows = new List<Dictionary<string, object>>();
        foreach (var rawIdx in indexes)
        {
            var idx = (rawIdx ?? string.Empty).Trim();
            if (idx.Length == 0 || idx.Length > 8 || !idx.All(char.IsDigit))
            {
                continue;
            }

            var key = prefix + "_" + idx + "_";
            var caption = form[key + "caption"].ToString().Trim();
            if (caption.Length == 0)
            {
                continue;
            }

            var row = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["value"] = caption,
                ["color"] = form[key + "color"].ToString().Trim(),
            };

            var id = form[key + "id"].ToString().Trim();
            if (long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId) && parsedId > 0)
            {
                row["id"] = parsedId;
                row["created_earlier"] = 1;
            }
            else
            {
                row["created_earlier"] = 0;
            }

            var nameKey = form[key + "name"].ToString().Trim();
            if (nameKey.Length > 0)
            {
                row["value_lang_str_id"] = nameKey;
            }

            foreach (var flag in flags)
            {
                row[flag] = form.ContainsKey(key + flag) ? 1 : 0;
            }

            rows.Add(row);
        }

        return JsonSerializer.Serialize(rows);
    }
}
