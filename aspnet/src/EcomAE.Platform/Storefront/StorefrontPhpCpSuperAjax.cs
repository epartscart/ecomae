using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string GovernanceRulesMissing = "Governance-rules table is missing — schema-ensure stays Classic.";
    public const string FreeToolsUsageMissing = "Free-tools usage tables are missing — schema-ensure stays Classic.";
    public const string GovernanceDbError = "DB error";
    public const string FreeToolsDbError = "Database connection failed";

    public sealed record GovernanceSavedBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("rule_key")] string RuleKey,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message = null);

    public sealed record GovernanceListBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("rules")] IReadOnlyList<GovernanceRuleBody> Rules);

    public sealed record GovernanceRuleBody(
        [property: JsonPropertyName("rule_key")] string RuleKey,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("enforcement")] string Enforcement,
        [property: JsonPropertyName("active")] int Active,
        [property: JsonPropertyName("scope")] string Scope);

    public sealed record FreeToolsToggleBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("active")] bool Active,
        [property: JsonPropertyName("message")] string Message);

    public sealed record FreeToolsStatsBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("stats")] FreeToolsStatsPayload Stats);

    public sealed record FreeToolsStatsPayload(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("accounts")] long Accounts,
        [property: JsonPropertyName("with_password")] long WithPassword,
        [property: JsonPropertyName("active_30d")] long Active30d,
        [property: JsonPropertyName("saves")] long Saves);

    public static async Task<object> PlatformGovernanceAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        string? requestHost,
        ICpPlatformGovernanceWriteService rules,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(
            connection,
            adminSession,
            adminUser,
            new CodedJson(403, new FlagBody(false, IntegrationsAdminRequired)),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        if (!PlatformHostPolicy.IsSuperCpHost(requestHost))
        {
            return new CodedJson(403, new FlagBody(false, IntegrationsSuperOnly));
        }

        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action == "save_rule")
        {
            var key = CpPlatformGovernanceWriteService.NormalizeRuleKey(OmsField(fields, "rule_key"));
            if (key.Length == 0)
            {
                return new FlagBody(false, "Invalid rule_key");
            }

            var written = await rules.SaveRuleAsync(
                new CpPlatformGovernanceSaveRuleRequest(key, PostedOn(OmsField(fields, "active")), OmsField(fields, "enforcement")),
                cancellationToken).ConfigureAwait(false);
            if (!written.Succeeded)
            {
                return new FlagBody(false, written.Message);
            }

            return new GovernanceSavedBody(true, key, written.Message);
        }

        if (action == "list_rules")
        {
            try
            {
                var listed = new List<GovernanceRuleBody>();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    SELECT `rule_key`, IFNULL(`category`,'') AS category, IFNULL(`title`,'') AS title,
                           IFNULL(`enforcement`,'') AS enforcement, IF(`active`=1,1,0) AS active, IFNULL(`scope`,'') AS scope
                    FROM `epc_platform_governance_rules`
                    ORDER BY `category` ASC, `rule_key` ASC
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    listed.Add(new GovernanceRuleBody(
                        Convert.ToString(reader["rule_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["enforcement"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["scope"], CultureInfo.InvariantCulture) ?? string.Empty));
                }

                return new GovernanceListBody(true, listed);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new FlagBody(false, GovernanceRulesMissing);
            }
        }

        return new CodedJson(400, new FlagBody(false, "Unknown action"));
    }

    public static async Task<object> FreeToolsAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        string? requestHost,
        ICpFreeToolsWriteService tools,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(
            connection,
            adminSession,
            adminUser,
            new CodedJson(403, new BroadcastBody(false, IntegrationsAdminRequired)),
            cancellationToken).ConfigureAwait(false);
        if (denied is FlagBody flag)
        {
            return new BroadcastBody(false, flag.Message);
        }

        if (denied is not null)
        {
            return denied;
        }

        if (!PlatformHostPolicy.IsSuperCpHost(requestHost))
        {
            return new CodedJson(403, new BroadcastBody(false, IntegrationsSuperOnly));
        }

        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action.Length == 0)
        {
            action = "toggle";
        }

        if (action == "toggle")
        {
            var tool = CpFreeToolsWriteService.NormalizeTool(OmsField(fields, "tool"));
            var active = PostedOn(OmsField(fields, "active"));
            if (tool.Length == 0 || !CpFreeToolsWriteService.IsKnownTool(tool))
            {
                return new BroadcastBody(false, "Unknown tool");
            }

            var written = await tools.ToggleAsync(new CpFreeToolsToggleRequest(tool, active), cancellationToken).ConfigureAwait(false);
            return written.Succeeded
                ? new FreeToolsToggleBody(true, active, written.Message)
                : new BroadcastBody(false, written.Message);
        }

        if (action == "stats")
        {
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var accounts = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_free_tool_accounts`", cancellationToken).ConfigureAwait(false);
                var withPassword = await ErpDb.LongAsync(
                    connection, null,
                    "SELECT COUNT(*) FROM `epc_free_tool_accounts` WHERE `pass_hash` IS NOT NULL AND `pass_hash`<>''",
                    cancellationToken).ConfigureAwait(false);
                var active = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_free_tool_accounts` WHERE `time_last_seen`>=?"),
                    cancellationToken, now - 30L * 86400L).ConfigureAwait(false);
                var saves = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_free_tool_saves`", cancellationToken).ConfigureAwait(false);
                return new FreeToolsStatsBody(true, new FreeToolsStatsPayload(true, accounts, withPassword, active, saves));
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new BroadcastBody(false, FreeToolsUsageMissing);
            }
        }

        return new BroadcastBody(false, "Unknown action");
    }

    private static bool PostedOn(string raw)
    {
        var value = raw.Trim();
        return value.Length > 0 && value != "0";
    }
}
