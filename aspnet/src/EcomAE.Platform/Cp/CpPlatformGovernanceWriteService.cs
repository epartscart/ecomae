using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_platform_governance.php</c> twin of <c>epc_platform_governance_update_rule</c>.
/// Title, description, config, seed, and schema-ensure stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpPlatformGovernanceWriteService
{
    Task<ErpSimpleWriteResult> SaveRuleAsync(
        CpPlatformGovernanceSaveRuleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpPlatformGovernanceSaveRuleRequest(
    string? RuleKey,
    bool Active,
    string? Enforcement);

public sealed class CpPlatformGovernanceWriteService : ICpPlatformGovernanceWriteService
{
    private static readonly Regex RuleKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly HashSet<string> EnforcementLevels = new(StringComparer.Ordinal)
    {
        "advisory", "required", "blocked",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpPlatformGovernanceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', $rule_key)</c>.</summary>
    public static string NormalizeRuleKey(string? ruleKey)
        => RuleKeySafe.Replace((ruleKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    /// <summary>PHP <c>epc_platform_governance_enforcement_levels</c>. Null when the column should be skipped.</summary>
    public static string? NormalizeEnforcement(string? enforcement)
    {
        var raw = (enforcement ?? string.Empty).Trim().ToLowerInvariant();
        if (raw.Length == 0)
        {
            raw = "required";
        }

        return EnforcementLevels.Contains(raw) ? raw : null;
    }

    public async Task<ErpSimpleWriteResult> SaveRuleAsync(
        CpPlatformGovernanceSaveRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var ruleKey = NormalizeRuleKey(request.RuleKey);
        if (ruleKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid rule_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var enforcement = NormalizeEnforcement(request.Enforcement);
        var active = request.Active ? 1 : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var id = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_platform_governance_rules` WHERE `rule_key`=? LIMIT 1"),
                cancellationToken, ruleKey).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Rule not found");
            }

            if (enforcement is null)
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_platform_governance_rules` SET `active`=?, `time_updated`=? WHERE `rule_key`=?"),
                    cancellationToken, active, now, ruleKey).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_platform_governance_rules` SET `active`=?, `enforcement`=?, `time_updated`=? WHERE `rule_key`=?"),
                    cancellationToken, active, enforcement, now, ruleKey).ConfigureAwait(false);
            }

            return ErpSimpleWriteResult.Ok("Rule saved", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Governance-rules table is missing — schema-ensure stays Classic.");
        }
    }
}
