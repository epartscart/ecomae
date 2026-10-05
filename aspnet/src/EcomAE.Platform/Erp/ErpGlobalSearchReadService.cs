using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twin of the read-only ajax_erp.php case <c>erp_global_search</c>
/// (<c>epc_erp_global_search</c>): tenant-filtered module/tab label matches first, then
/// per-type record matches (customers, e-invoices, purchase orders, GL journals,
/// insurance policies, tickets). Optional tables are skipped per query exactly as PHP's
/// per-block <c>try/catch</c>; nothing is ever written or created.
/// </summary>
public interface IErpGlobalSearchReadService
{
    Task<ErpAjaxRowsResult> SearchAsync(
        string? query,
        int limit,
        IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> nav,
        long companyHint,
        CancellationToken cancellationToken = default);
}

public sealed partial class ErpGlobalSearchReadService : IErpGlobalSearchReadService
{
    public const string PhpMainPage = "/cp/content/shop/finance/erp/erp_main_page.php";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpGlobalSearchReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpAjaxRowsResult> SearchAsync(
        string? query,
        int limit,
        IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> nav,
        long companyHint,
        CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();
        limit = Math.Max(1, Math.Min(50, limit));
        if (new StringInfo(q).LengthInTextElements < 2)
        {
            return new(ErpSimpleWriteResult.Ok("OK", 0), []);
        }

        if (!_connections.IsConfigured)
        {
            return new(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), []);
        }

        var perType = Math.Max(4, limit / 4);
        var modules = MatchModules(q, nav);
        var records = new List<IReadOnlyDictionary<string, object?>>();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ErpLazySchema.EnsureInsuranceAsync(connection, cancellationToken).ConfigureAwait(false);

        await ErpLazySchema.EnsureTicketsAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpFinAdvancedCompany.ResolveAsync(connection, companyHint, cancellationToken).ConfigureAwait(false);
        var like = "%" + q.Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        var numericId = long.TryParse(q, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1L;

        await TryAsync(connection,
            "SELECT `id`, `firstname`, `lastname`, `email` FROM `dp_users` WHERE (CONCAT(`firstname`,' ',`lastname`) LIKE ? OR `email` LIKE ? OR `id` = ?) LIMIT ?",
            [like, like, numericId, perType],
            r =>
            {
                var name = ((Str(r, "firstname") ?? string.Empty) + " " + (Str(r, "lastname") ?? string.Empty)).Trim();
                var email = Str(r, "email") ?? string.Empty;
                records.Add(Record("customer", name.Length > 0 ? name : email, email, "receivables", "ar", "fa-user"));
            }, cancellationToken).ConfigureAwait(false);

        await TryAsync(connection,
            "SELECT `id`, `invoice_number`, `buyer_name` FROM `epc_einvoice_documents` WHERE `active` = 1 AND (`invoice_number` LIKE ? OR `buyer_name` LIKE ?) ORDER BY `id` DESC LIMIT ?",
            [like, like, perType],
            r =>
            {
                var inv = Or(Str(r, "invoice_number"), "INV #" + Id(r));
                var buyer = Str(r, "buyer_name") ?? string.Empty;
                records.Add(Record("invoice", Join(inv, buyer), buyer, "invoices", "sales", "fa-file-text-o"));
            }, cancellationToken).ConfigureAwait(false);

        await TryAsync(connection,
            "SELECT p.`id`, p.`po_no`, s.`name` AS supplier_name FROM `epc_erp_purchase_orders` p LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id` WHERE p.`po_no` LIKE ? OR s.`name` LIKE ? ORDER BY p.`id` DESC LIMIT ?",
            [like, like, perType],
            r =>
            {
                var po = Or(Str(r, "po_no"), "PO #" + Id(r));
                var supplier = Str(r, "supplier_name") ?? string.Empty;
                records.Add(Record("po", Join(po, supplier), supplier, "purchase_orders", "purchasing", "fa-clipboard"));
            }, cancellationToken).ConfigureAwait(false);

        await TryAsync(connection,
            "SELECT `id`, `reference`, `description` FROM `epc_erp_gl_journals` WHERE `active` = 1 AND (`reference` LIKE ? OR `description` LIKE ?) ORDER BY `id` DESC LIMIT ?",
            [like, like, perType],
            r =>
            {
                var reference = Or(Str(r, "reference"), "Journal #" + Id(r));
                var desc = Truncate(Str(r, "description") ?? string.Empty, 60);
                records.Add(Record("journal", Join(reference, desc), desc, "gl", "finance", "fa-book"));
            }, cancellationToken).ConfigureAwait(false);

        await TryAsync(connection,
            "SELECT `id`, `policy_no`, `insurer` FROM `epc_erp_ins_policies` WHERE `company_id` = ? AND (`policy_no` LIKE ? OR `insurer` LIKE ?) ORDER BY `id` DESC LIMIT ?",
            [companyId, like, like, perType],
            r =>
            {
                var pol = Or(Str(r, "policy_no"), "Policy #" + Id(r));
                var ins = Str(r, "insurer") ?? string.Empty;
                records.Add(Record("policy", Join(pol, ins), ins, "insurance", "risk", "fa-shield"));
            }, cancellationToken).ConfigureAwait(false);

        var ticketArea = TabToArea("tickets", nav);
        await TryAsync(connection,
            "SELECT `id`, `ticket_no`, `subject` FROM `epc_tickets` WHERE `company_id` = ? AND (`ticket_no` LIKE ? OR `subject` LIKE ?) ORDER BY `id` DESC LIMIT ?",
            [companyId, like, like, perType],
            r =>
            {
                var tno = Or(Str(r, "ticket_no"), "Ticket #" + Id(r));
                var subj = Truncate(Str(r, "subject") ?? string.Empty, 80);
                records.Add(Record("ticket", Join(tno, subj), subj, "tickets", ticketArea, "fa-life-ring"));
            }, cancellationToken).ConfigureAwait(false);

        var combined = new List<IReadOnlyDictionary<string, object?>>(modules.Count + records.Count);
        combined.AddRange(modules);
        combined.AddRange(records);
        return new(ErpSimpleWriteResult.Ok("OK", 0), combined.Take(limit).ToList());
    }

    /// <summary>PHP step 3: case-insensitive substring match on plain tab or area label; external links skipped.</summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> MatchModules(
        string query,
        IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> nav)
    {
        var qLower = query.ToLowerInvariant();
        var results = new List<IReadOnlyDictionary<string, object?>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in nav)
        {
            foreach (var column in group.Columns ?? Array.Empty<LegacyDesktopChromeCatalog.MegaAreaColumn>())
            {
                var areaLabel = Plain(column.Label);
                var areaLower = areaLabel.ToLowerInvariant();
                foreach (var tab in column.Tabs)
                {
                    if (IsExternal(tab) || !seen.Add(tab.Id))
                    {
                        continue;
                    }

                    var tabKey = TabKey(tab.Id);
                    var tabLabel = Plain(tab.Label);
                    if (!tabLabel.ToLowerInvariant().Contains(qLower, StringComparison.Ordinal)
                        && !areaLower.Contains(qLower, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    results.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["type"] = "module",
                        ["label"] = tabLabel,
                        ["sub"] = areaLabel,
                        ["tab"] = tabKey,
                        ["area"] = column.Id,
                        ["icon"] = string.IsNullOrWhiteSpace(tab.Icon) ? "fa-circle-o" : tab.Icon,
                        ["url"] = TabUrl(tabKey, column.Id),
                    });
                }
            }
        }

        return results;
    }

    /// <summary>PHP <c>epc_erp_tab_url($base, $tab, '', '', $area)</c> without the shell query (AJAX context).</summary>
    public static string TabUrl(string tab, string area)
        => PhpMainPage + "?area=" + WebUtility.UrlEncode(area) + "&tab=" + WebUtility.UrlEncode(tab) + "&from=&to=";

    /// <summary>PHP <c>epc_erp_tab_to_area</c>: canonical pins, then first area owning the tab, else overview.</summary>
    public static string TabToArea(string tab, IReadOnlyList<LegacyDesktopChromeCatalog.MegaGroup> nav)
    {
        switch (tab)
        {
            case "bank_recon": return "banking";
            case "fixed_assets": return "fixed_assets";
            case "fin_advanced": return "finance";
            case "hr_ops": return "people";
            case "wms": return "warehouse";
            case "contracts": return "enterprise";
        }

        foreach (var group in nav)
        {
            foreach (var column in group.Columns ?? Array.Empty<LegacyDesktopChromeCatalog.MegaAreaColumn>())
            {
                if (column.Tabs.Any(t => string.Equals(TabKey(t.Id), tab, StringComparison.Ordinal)))
                {
                    return column.Id;
                }
            }
        }

        return "overview";
    }

    private static bool IsExternal(PhpModuleCatalog.ModuleLink tab)
        => string.Equals(tab.Icon, "fa-external-link", StringComparison.Ordinal);

    private static string TabKey(string id)
    {
        var slash = id.IndexOf('/', StringComparison.Ordinal);
        return slash >= 0 ? id[(slash + 1)..] : id;
    }

    /// <summary>PHP <c>epc_erp_nav_label_plain</c>: strip tags, decode entities.</summary>
    public static string Plain(string? label)
        => WebUtility.HtmlDecode(Tags().Replace(label ?? string.Empty, string.Empty));

    private static IReadOnlyDictionary<string, object?> Record(string recordType, string label, string sub, string tab, string area, string icon)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "record",
            ["record_type"] = recordType,
            ["label"] = label,
            ["sub"] = sub,
            ["tab"] = tab,
            ["area"] = area,
            ["icon"] = icon,
            ["url"] = TabUrl(tab, area),
        };

    private static async Task TryAsync(
        DbConnection connection,
        string sql,
        object?[] parameters,
        Action<IReadOnlyDictionary<string, object?>> onRow,
        CancellationToken ct)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(command, parameters);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var map = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    map[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                onRow(map);
            }
        }
        catch (DbException)
        {
            // PHP wraps each block in try/catch: optional tables may be absent in a tenant.
        }
    }

    private static string Join(string head, string tail) => tail.Length > 0 ? head + " — " + tail : head;

    private static string Or(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private static string Truncate(string value, int max)
    {
        var info = new StringInfo(value);
        return info.LengthInTextElements <= max ? value : info.SubstringByTextElements(0, max);
    }

    private static string Id(IReadOnlyDictionary<string, object?> row)
        => Convert.ToInt64(row.GetValueOrDefault("id") ?? 0L, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    private static string? Str(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();
}
