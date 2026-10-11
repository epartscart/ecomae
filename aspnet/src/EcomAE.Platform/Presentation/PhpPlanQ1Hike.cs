using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hike Super-CP customer board page.
/// PHP identifiers kept for the inventory: <c>epc_scp_h</c>, <c>epc_scp_backend</c>,
/// <c>epc_scp_guard_super_admin</c>, <c>epc_scp_customer_board_search</c>,
/// <c>epc_scp_tenant_options</c>, <c>epc_scp_render_hero</c>,
/// <c>epc_scp_render_workspace_intro</c>, <c>epc_scp_render_empty_state</c>,
/// <c>epc_scp_operator_guide_url</c>.
/// Path: <c>cp/content/control/portal/epc_super_cp_customer_board.php</c>.
/// GET never mints a session cookie. Leftover unique portal, leftover unique
/// operator-guide page, leftover unique user helper, and leftover portal PDO
/// stay injected and are not named here. Helpers already closed in Gulf are
/// stubbed, not included.
/// </summary>
public static class PhpPlanQ1Hike
{
    public const string CustomerBoardPath = "cp/content/control/portal/epc_super_cp_customer_board.php";
    public const string DeniedSuper =
        "<div class=\"alert alert-warning\">This module is available on ECOM AE Super CP only.</div>";
    public const string DeniedAdminPrefix =
        "<div class=\"alert alert-warning\">Please <a href=\"/";
    public const string DeniedAdminSuffix = "/\">log in to Super CP</a>.</div>";
    public const string DbUnavailable =
        "<div class=\"alert alert-danger\">Database unavailable.</div>";
    public const string EmDash = "\u2014";

    public static Func<bool>? IsSuperHost { get; set; }
    public static Func<bool>? IsAdmin { get; set; }
    public static bool HasPdo { get; set; }
    public static string BackendDir { get; set; } = "cp";
    public static string Search { get; set; } = "";
    public static string TenantFilter { get; set; } = "";
    public static string PageRaw { get; set; } = "";
    public static Dictionary<string, object?> Board { get; set; } = EmptyBoard();
    public static List<Dictionary<string, object?>> Tenants { get; set; } = [];
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        IsSuperHost = null;
        IsAdmin = null;
        HasPdo = false;
        BackendDir = "cp";
        Search = "";
        TenantFilter = "";
        PageRaw = "";
        Board = EmptyBoard();
        Tenants = [];
        LastOutput = "";
    }

    public static string EpcScpH(object? value)
        => Str(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static string EpcScpBackend()
        => BackendDir.Trim('/');

    public static string EpcScpOperatorGuideUrl()
        => "/" + EpcScpBackend() + "/control/portal/epc_super_cp_operator_guide";

    public static bool EpcScpGuardSuperAdmin()
    {
        if (IsSuperHost?.Invoke() != true)
        {
            LastOutput = DeniedSuper;
            return false;
        }

        if (IsAdmin?.Invoke() != true)
        {
            LastOutput = DeniedAdminPrefix + EpcScpH(BackendDir) + DeniedAdminSuffix;
            return false;
        }

        LastOutput = "";
        return true;
    }

    public static Dictionary<string, object?> EpcScpCustomerBoardSearch()
        => Board;

    public static List<Dictionary<string, object?>> EpcScpTenantOptions()
        => Tenants;

    public static void EpcScpRenderHero(string badge, string title, List<Dictionary<string, object?>> actions)
    {
        var first = actions.Count > 0 ? Str(actions[0].GetValueOrDefault("url")) : "";
        LastOutput += "HERO:" + badge + "|" + title + "|" + actions.Count.ToString(CultureInfo.InvariantCulture) + "|" + first;
    }

    public static void EpcScpRenderWorkspaceIntro(string module)
        => LastOutput += "INTRO:" + module;

    public static string EpcScpRenderEmptyState(string title, string body, List<Dictionary<string, object?>> actions)
        => "EMPTY:" + title + "|" + body + "|" + actions.Count.ToString(CultureInfo.InvariantCulture);

    public static string EpcSuperCpCustomerBoardPage()
    {
        LastOutput = "";
        if (!EpcScpGuardSuperAdmin())
        {
            return LastOutput;
        }

        if (!HasPdo)
        {
            LastOutput += DbUnavailable;
            return LastOutput;
        }

        var search = Search.Trim();
        var tenantFilter = TenantFilter;
        var page = Math.Max(1, ToInt(PageRaw == "" ? "1" : PageRaw));
        var board = EpcScpCustomerBoardSearch();
        var tenants = EpcScpTenantOptions();
        var backend = EpcScpBackend();
        var selfUrl = "/" + backend + "/control/portal/epc_super_cp_customer_board";
        LastOutput += "<div class=\"col-lg-12 epc-scp-panel epc-scp-customer-board\">\n";
        EpcScpRenderHero(
            "Super CP",
            "Customer board",
            [
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = "Tenant hub",
                    ["icon"] = "fa-cloud",
                    ["url"] = "/" + backend + "/shop/tenant_hub/tenant_hub"
                },
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = "Communication",
                    ["icon"] = "fa-envelope",
                    ["url"] = "/" + backend + "/control/portal/epc_super_cp_communication",
                    ["primary"] = true
                },
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = "Operator guide",
                    ["icon"] = "fa-book",
                    ["url"] = EpcScpOperatorGuideUrl()
                }
            ]);
        EpcScpRenderWorkspaceIntro("customer_board");
        LastOutput += RenderBoard(board, tenants, search, tenantFilter, page, backend, selfUrl);
        return LastOutput;
    }

    public static string RenderBoard(
        Dictionary<string, object?> board,
        List<Dictionary<string, object?>> tenants,
        string search,
        string tenantFilter,
        int page,
        string backend,
        string selfUrl)
    {
        var rows = board.GetValueOrDefault("rows") as List<Dictionary<string, object?>> ?? [];
        var stats = board.GetValueOrDefault("stats") as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var total = ToInt(board.GetValueOrDefault("total"));
        var perPage = Math.Max(1, ToInt(board.GetValueOrDefault("per_page"), 50));
        var html = "\n<div class=\"epc-scp-kpi\">\n"
            + "\t<div class=\"epc-scp-kpi__card\">\n"
            + "\t\t<div class=\"epc-scp-kpi__label\">Results</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__val\">" + total.ToString(CultureInfo.InvariantCulture) + "</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__hint\">Matching customers</div>\n"
            + "\t</div>\n"
            + "\t<div class=\"epc-scp-kpi__card\">\n"
            + "\t\t<div class=\"epc-scp-kpi__label\">Platform</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__val\">" + ToInt(stats.GetValueOrDefault("platform")).ToString(CultureInfo.InvariantCulture) + "</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__hint\">ecomae DB</div>\n"
            + "\t</div>\n"
            + "\t<div class=\"epc-scp-kpi__card\">\n"
            + "\t\t<div class=\"epc-scp-kpi__label\">Tenants scanned</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__val\">" + ToInt(stats.GetValueOrDefault("tenants_scanned")).ToString(CultureInfo.InvariantCulture) + "</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__hint\">" + ToInt(stats.GetValueOrDefault("tenants_with_hits")).ToString(CultureInfo.InvariantCulture) + " with hits</div>\n"
            + "\t</div>\n"
            + "\t<div class=\"epc-scp-kpi__card\">\n"
            + "\t\t<div class=\"epc-scp-kpi__label\">Live tenants</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__val\">" + tenants.Count.ToString(CultureInfo.InvariantCulture) + "</div>\n"
            + "\t\t<div class=\"epc-scp-kpi__hint\">In registry</div>\n"
            + "\t</div>\n"
            + "</div>\n"
            + "\n<div class=\"epc-scp-filter-bar\">\n"
            + "\t<form method=\"get\" class=\"form-inline\">\n"
            + "\t\t<div class=\"form-group\">\n"
            + "\t\t\t<label class=\"sr-only\">Search</label>\n"
            + "\t\t\t<input type=\"search\" name=\"q\" class=\"form-control input-sm\" placeholder=\"Email, phone, name, company\u2026\" value=\"" + EpcScpH(search) + "\" style=\"min-width:220px\" />\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"form-group\">\n"
            + "\t\t\t<select name=\"tenant\" class=\"form-control input-sm\">\n"
            + "\t\t\t\t<option value=\"\">All sources</option>\n"
            + "\t\t\t\t<option value=\"platform\"" + (tenantFilter == "platform" ? " selected" : "") + ">Platform only</option>\n"
            + "\t\t\t\t";
        foreach (var tenant in tenants)
        {
            var key = Str(tenant.GetValueOrDefault("site_key"));
            html += "\t\t\t\t<option value=\"" + EpcScpH(key) + "\"" + (tenantFilter == key ? " selected" : "") + ">" + EpcScpH(tenant.GetValueOrDefault("label")) + "</option>\n"
                + "\t\t\t\t";
        }

        html += "\t\t\t</select>\n"
            + "\t\t</div>\n"
            + "\t\t<button type=\"submit\" class=\"btn btn-primary btn-sm\"><i class=\"fa fa-search\"></i> Search</button>\n"
            + "\t\t";
        if (search != "" || tenantFilter != "")
        {
            html += "\t\t<a class=\"btn btn-default btn-sm\" href=\"" + EpcScpH(selfUrl) + "\">Clear</a>\n"
                + "\t\t";
        }

        html += "\t</form>\n"
            + "</div>\n"
            + "\n<div class=\"epc-scp-table-card\">\n"
            + "\t<div class=\"table-responsive\">\n"
            + "\t\t<table class=\"table table-striped table-bordered table-condensed epc-scp-data-table\">\n"
            + "\t\t\t<thead>\n"
            + "\t\t\t\t<tr>\n"
            + "\t\t\t\t\t<th>Customer</th>\n"
            + "\t\t\t\t\t<th>Email / phone</th>\n"
            + "\t\t\t\t\t<th>Source</th>\n"
            + "\t\t\t\t\t<th>Registered</th>\n"
            + "\t\t\t\t\t<th>Quick actions</th>\n"
            + "\t\t\t\t</tr>\n"
            + "\t\t\t</thead>\n"
            + "\t\t\t<tbody>\n"
            + "\t\t\t";
        if (rows.Count == 0)
        {
            var emptyBody = search != "" || tenantFilter != ""
                ? "Try a broader search, clear filters, or pick another tenant from the registry."
                : "Search by email, phone, name, or company to find users across platform and tenant databases.";
            html += "\t\t\t\t<tr><td colspan=\"5\" class=\"epc-scp-empty-cell\">\n"
                + "\t\t\t\t\t"
                + EpcScpRenderEmptyState(
                    "No customers found",
                    emptyBody,
                    [
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["label"] = "Clear search",
                            ["icon"] = "fa-eraser",
                            ["url"] = selfUrl,
                            ["primary"] = search != "" || tenantFilter != ""
                        },
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["label"] = "Operator guide",
                            ["icon"] = "fa-book",
                            ["url"] = EpcScpOperatorGuideUrl()
                        },
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["label"] = "Tenant hub",
                            ["icon"] = "fa-cloud",
                            ["url"] = "/" + backend + "/shop/tenant_hub/tenant_hub"
                        }
                    ])
                + "\t\t\t\t</td></tr>\n"
                + "\t\t\t";
        }

        html += "\t\t\t";
        foreach (var row in rows)
        {
            html += RenderRow(row);
        }

        html += "\t\t\t</tbody>\n"
            + "\t\t</table>\n"
            + "\t</div>\n"
            + "\t";
        var pages = (int)Math.Ceiling(Math.Max(1, total) / (double)perPage);
        if (pages > 1)
        {
            html += "<div class=\"epc-scp-pagination\">";
            var limit = Math.Min(pages, 12);
            for (var p = 1; p <= limit; p++)
            {
                var qs = PhpQuery(search, tenantFilter, p);
                var cls = p == page ? "btn-primary" : "btn-default";
                html += "<a class=\"btn btn-xs " + cls + "\" href=\"" + EpcScpH(selfUrl + "?" + qs) + "\">" + p.ToString(CultureInfo.InvariantCulture) + "</a> ";
            }

            html += "</div>";
        }

        html += "</div>\n</div>\n";
        return html;
    }

    private static string RenderRow(Dictionary<string, object?> row)
    {
        var links = row.GetValueOrDefault("links") as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var email = Str(row.GetValueOrDefault("email"));
        var phone = Str(row.GetValueOrDefault("phone"));
        var hostname = Str(row.GetValueOrDefault("hostname"));
        var timeReg = row.GetValueOrDefault("time_reg");
        var source = Coalesce(row.GetValueOrDefault("source_label"), row.GetValueOrDefault("source"), "");
        var html = "\t\t\t\t<tr>\n"
            + "\t\t\t\t\t<td>\n"
            + "\t\t\t\t\t\t<strong>" + EpcScpH(row.GetValueOrDefault("name")) + "</strong>\n"
            + "\t\t\t\t\t\t<div class=\"text-muted small\">#" + ToInt(row.GetValueOrDefault("user_id")).ToString(CultureInfo.InvariantCulture) + "</div>\n"
            + "\t\t\t\t\t</td>\n"
            + "\t\t\t\t\t<td>\n"
            + "\t\t\t\t\t\t";
        if (!PhpEmpty(email))
        {
            html += "<div><a href=\"mailto:" + EpcScpH(email) + "\">" + EpcScpH(email) + "</a></div>";
        }

        html += "\t\t\t\t\t\t";
        if (!PhpEmpty(phone))
        {
            html += "<div class=\"text-muted small\">" + EpcScpH(phone) + "</div>";
        }

        html += "\t\t\t\t\t</td>\n"
            + "\t\t\t\t\t<td>\n"
            + "\t\t\t\t\t\t<span class=\"epc-scp-badge epc-scp-badge--tenant\">" + EpcScpH(source) + "</span>\n"
            + "\t\t\t\t\t\t";
        if (!PhpEmpty(hostname))
        {
            html += "<div class=\"text-muted small\">" + EpcScpH(hostname) + "</div>";
        }

        html += "\t\t\t\t\t</td>\n"
            + "\t\t\t\t\t<td>" + (!PhpEmpty(timeReg) ? EpcScpH(FormatDay(timeReg)) : EmDash) + "</td>\n"
            + "\t\t\t\t\t<td class=\"epc-scp-actions-cell\">\n"
            + "\t\t\t\t\t\t";
        if (!PhpEmpty(links.GetValueOrDefault("crm")))
        {
            html += "\t\t\t\t\t\t<a class=\"btn btn-xs btn-default\" href=\"" + EpcScpH(links.GetValueOrDefault("crm")) + "\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-address-book\"></i> CRM</a>\n"
                + "\t\t\t\t\t\t";
        }

        html += "\t\t\t\t\t\t";
        if (!PhpEmpty(links.GetValueOrDefault("erp")))
        {
            html += "\t\t\t\t\t\t<a class=\"btn btn-xs btn-primary\" href=\"" + EpcScpH(links.GetValueOrDefault("erp")) + "\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-university\"></i> ERP</a>\n"
                + "\t\t\t\t\t\t";
        }

        html += "\t\t\t\t\t\t";
        if (!PhpEmpty(links.GetValueOrDefault("cp")))
        {
            html += "\t\t\t\t\t\t<a class=\"btn btn-xs btn-default\" href=\"" + EpcScpH(links.GetValueOrDefault("cp")) + "\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-external-link\"></i> CP</a>\n"
                + "\t\t\t\t\t\t";
        }

        html += "\t\t\t\t\t</td>\n"
            + "\t\t\t\t</tr>\n"
            + "\t\t\t";
        return html;
    }

    private static string PhpQuery(string search, string tenant, int page)
    {
        var parts = new List<string>();
        if (search != "")
        {
            parts.Add("q=" + PhpUrlEncode(search));
        }

        if (tenant != "")
        {
            parts.Add("tenant=" + PhpUrlEncode(tenant));
        }

        parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        return string.Join("&", parts);
    }

    private static string PhpUrlEncode(string value)
        => Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);

    private static string FormatDay(object? value)
    {
        var ts = ToLong(value);
        return DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, object?> EmptyBoard()
        => new(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>(),
            ["total"] = 0,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };

    private static object? Coalesce(params object?[] values)
    {
        foreach (var value in values)
        {
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d || (value is string s && (s == "" || s == "0"));

    private static int ToInt(object? value, int fallback = 0)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value is int i)
        {
            return i;
        }

        return int.TryParse(Str(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static long ToLong(object? value)
    {
        if (value is long l)
        {
            return l;
        }

        if (value is int i)
        {
            return i;
        }

        return long.TryParse(Str(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
