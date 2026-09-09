using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_epc_free_tools_admin.php</c> twin of <c>epc_free_tools_set_active</c>.
/// Audit, schema-ensure, and send stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpFreeToolsWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        CpFreeToolsToggleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpFreeToolsToggleRequest(string? Tool, bool Active);

public sealed class CpFreeToolsWriteService : ICpFreeToolsWriteService
{
    private static readonly Regex ToolSafe = new("[^a-z]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["vat"] = "VAT / GST Return",
        ["ct"] = "Corporate Tax",
        ["payroll"] = "Payroll & Gratuity",
        ["ifrs"] = "IFRS Financials",
        ["einvoice"] = "Electronic Invoicing",
        ["extreport"] = "External Reporting",
        ["customs"] = "Customs & Logistics",
        ["insurance"] = "Insurance",
        ["docexpiry"] = "Document Expiry",
        ["valuation"] = "Business Valuation",
        ["finmodel"] = "Financial Model",
        ["taxkit"] = "Tax Worldwide Kit",
        ["hrcompliance"] = "HR Compliance Worldwide",
        ["workflow"] = "Approval Workflow",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpFreeToolsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z]/', '', $tool)</c>.</summary>
    public static string NormalizeTool(string? tool)
        => ToolSafe.Replace((tool ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static bool IsKnownTool(string tool) => Catalog.ContainsKey(tool);

    public static IReadOnlyList<string> ApplyToggle(IEnumerable<string>? disabled, string tool, bool active)
    {
        var next = new SortedSet<string>(StringComparer.Ordinal);
        if (disabled is not null)
        {
            foreach (var key in disabled)
            {
                var clean = NormalizeTool(key);
                if (clean.Length > 0)
                {
                    next.Add(clean);
                }
            }
        }

        if (active)
        {
            next.Remove(tool);
        }
        else
        {
            next.Add(tool);
        }

        return next.ToArray();
    }

    public static string ToggleMessage(string tool, bool active)
        => Catalog[tool] + (active ? " is now active." : " is now deactivated (shown as unavailable to visitors).");

    public static IReadOnlyList<string> ParseDisabled(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            return parsed is null ? [] : ApplyToggle(parsed, "", true);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        CpFreeToolsToggleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tool = NormalizeTool(request.Tool);
        if (tool.Length == 0 || !IsKnownTool(tool))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown tool");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var raw = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `val` FROM `epc_free_tool_settings` WHERE `name`=? LIMIT 1"),
                cancellationToken, "disabled_tools").ConfigureAwait(false);
            var next = ApplyToggle(ParseDisabled(raw), tool, request.Active);
            var json = JsonSerializer.Serialize(next);

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("REPLACE INTO `epc_free_tool_settings` (`name`,`val`,`time_updated`) VALUES (?,?,?)"),
                cancellationToken, "disabled_tools", json, now).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(ToggleMessage(tool, request.Active), 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Free-tools settings table is missing — schema-ensure stays Classic.");
        }
    }
}
