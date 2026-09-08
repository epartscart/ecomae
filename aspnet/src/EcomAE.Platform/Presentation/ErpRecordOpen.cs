using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// ERP list "Open" and the blue Open module button must land on an ASP.NET
/// record URL. Mapping <c>/ERP/?tab=…&amp;order_id=</c> through
/// <see cref="PhpSurfaceLinkMap.AspNetPrimaryHref"/> used to strip the id, so
/// the click reloaded the same list and looked dead.
/// </summary>
public static class ErpRecordOpen
{
    public const string ModuleBodyId = "erp-module-body";
    public const string ModuleNewId = "erp-module-new";
    public const string ModuleBodyHash = "#" + ModuleBodyId;
    public const string ModuleNewHash = "#" + ModuleNewId;

    internal static readonly string[] RecordQueryKeys =
    [
        "so_id", "po_id", "inv_id", "invoice_id", "purchase_id",
        "journal_id", "entry_id", "account_id", "supplier_id",
        "customer_id", "user_id", "contract_id", "order_id",
        "pf_case", "case_id", "campaign_id", "warehouse_id",
        "rfq_id", "quote_id", "transfer_id", "period_id", "favorite_id",
        "batch_id", "expense_id", "expense_report_id", "recon_line_id",
        "delivery_note_id", "document_id", "asset_id", "contact_id",
        "event_id", "template_id", "session_id", "license_id",
        "req_id", "rq", "queue_id", "txn_id",
        "toolkit_id",
    ];

    public static string Href(string appPath, string param, long id)
    {
        if (string.IsNullOrWhiteSpace(appPath) || id <= 0 || string.IsNullOrWhiteSpace(param))
        {
            return string.IsNullOrWhiteSpace(appPath) ? "/erp" : appPath;
        }

        var path = appPath.Trim();
        var hash = "";
        var hashAt = path.IndexOf('#');
        if (hashAt >= 0)
        {
            hash = path[hashAt..];
            path = path[..hashAt];
        }

        var sep = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var rowHash = string.IsNullOrEmpty(hash) ? "#erp-row-" + id.ToString(CultureInfo.InvariantCulture) : hash;
        return path + sep + param + "=" + id.ToString(CultureInfo.InvariantCulture) + rowHash;
    }

    public static long ReadId(HttpRequest? request, params string[] keys)
    {
        if (request is null || keys.Length == 0)
        {
            return 0;
        }

        foreach (var key in keys)
        {
            if (TryParsePositive(request.Query[key].ToString(), out var id))
            {
                return id;
            }
        }

        return 0;
    }

    public static string PreserveRecordQuery(string mapped, string? originalPhpHref)
    {
        if (string.IsNullOrWhiteSpace(mapped) || string.IsNullOrWhiteSpace(originalPhpHref))
        {
            return mapped;
        }

        var pairs = new List<string>();
        foreach (var key in RecordQueryKeys)
        {
            if (!TryParsePositive(ExtractQuery(originalPhpHref, key), out var id))
            {
                continue;
            }

            if (ExtractQuery(mapped, key) is not null)
            {
                continue;
            }

            pairs.Add(key + "=" + id.ToString(CultureInfo.InvariantCulture));
        }

        if (pairs.Count == 0)
        {
            return mapped;
        }

        var hash = "";
        var path = mapped;
        var hashAt = mapped.IndexOf('#');
        if (hashAt >= 0)
        {
            hash = mapped[hashAt..];
            path = mapped[..hashAt];
        }

        var sep = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return path + sep + string.Join("&", pairs) + hash;
    }

    public static string OpenModuleHref(string? phpTabHref)
    {
        var mapped = PhpSurfaceLinkMap.AspNetPrimaryHref(phpTabHref);
        if (string.IsNullOrWhiteSpace(mapped) || mapped.Contains('#', StringComparison.Ordinal))
        {
            return mapped;
        }

        return mapped + ModuleBodyHash;
    }

    public static string NewHref(string? phpTabHref)
    {
        var mapped = PhpSurfaceLinkMap.AspNetPrimaryHref(phpTabHref);
        if (string.IsNullOrWhiteSpace(mapped) || mapped.Contains('#', StringComparison.Ordinal))
        {
            return mapped;
        }

        return mapped + ModuleNewHash;
    }

    public static string RowClass(long rowId, long openedId)
        => rowId > 0 && rowId == openedId ? "epc-erp-row-open" : "";

    internal static bool TryParsePositive(string? raw, out long id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
            && id > 0;
    }

    private static string? ExtractQuery(string href, string key)
    {
        var qIndex = href.IndexOf('?', StringComparison.Ordinal);
        if (qIndex < 0 || qIndex >= href.Length - 1)
        {
            return null;
        }

        var query = href[(qIndex + 1)..];
        var hashAt = query.IndexOf('#');
        if (hashAt >= 0)
        {
            query = query[..hashAt];
        }

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }
}
