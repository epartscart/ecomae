using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_epc_parts_agent_cp.php</c> <c>save_config</c> / <c>epc_agent_save_config</c> UPSERT.
/// Chat, sync, export, and generate stay Classic. This service does not invent a send.
/// </summary>
public interface ICpPartsAgentWriteService
{
    Task<CpPartsAgentConfigRow> LoadConfigAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveConfigAsync(
        string? enabledRaw,
        string? agentName,
        string? subtitle,
        string? greeting,
        string? systemPrompt,
        string? teaserText,
        string? placeholder,
        string? logoUrl,
        string? domain,
        CancellationToken cancellationToken = default);
}

public sealed record CpPartsAgentConfigRow(
    bool Enabled,
    string AgentName,
    string Subtitle,
    string Greeting,
    string SystemPrompt,
    string TeaserText,
    string Placeholder,
    string LogoUrl,
    string Domain);

public sealed class CpPartsAgentWriteService : ICpPartsAgentWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPartsAgentWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpPartsAgentConfigRow> LoadConfigAsync(CancellationToken cancellationToken = default)
    {
        var empty = new CpPartsAgentConfigRow(true, "", "", "", "", "", "", "", "");
        if (!_connections.IsConfigured)
        {
            return empty;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var select = connection.CreateCommand();
            select.CommandText = ErpDb.Positional(
                """
                SELECT `enabled`, `agent_name`, `subtitle`, `greeting`, `system_prompt`,
                       `teaser_text`, `placeholder`, `logo_url`, `domain`
                FROM `epc_parts_agent_config`
                WHERE `id` = ?
                LIMIT 1
                """);
            ErpDb.AddParameters(select, 1);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return empty;
            }

            return new CpPartsAgentConfigRow(
                ReadLong(reader, 0) != 0,
                ReadText(reader, 1),
                ReadText(reader, 2),
                ReadText(reader, 3),
                ReadText(reader, 4),
                ReadText(reader, 5),
                ReadText(reader, 6),
                ReadText(reader, 7),
                ReadText(reader, 8));
        }
        catch (DbException)
        {
            return empty;
        }
    }

    public async Task<ErpSimpleWriteResult> SaveConfigAsync(
        string? enabledRaw,
        string? agentName,
        string? subtitle,
        string? greeting,
        string? systemPrompt,
        string? teaserText,
        string? placeholder,
        string? logoUrl,
        string? domain,
        CancellationToken cancellationToken = default)
    {
        var enabled = ParseEnabled(enabledRaw) ? 1 : 0;
        var name = Clip(agentName, 128);
        var sub = Clip(subtitle, 255);
        var greet = Clip(greeting, 65535);
        var prompt = Clip(systemPrompt, 65535);
        var teaser = Clip(teaserText, 255);
        var hold = Clip(placeholder, 255);
        var logo = Clip(logoUrl, 512);
        var host = Clip(domain, 255);

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_parts_agent_config`
                     (`id`, `enabled`, `agent_name`, `subtitle`, `greeting`, `system_prompt`, `teaser_text`, `placeholder`, `logo_url`, `domain`, `updated_at`)
                     VALUES (1, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                     ON DUPLICATE KEY UPDATE
                     `enabled` = VALUES(`enabled`), `agent_name` = VALUES(`agent_name`), `subtitle` = VALUES(`subtitle`),
                     `greeting` = VALUES(`greeting`), `system_prompt` = VALUES(`system_prompt`), `teaser_text` = VALUES(`teaser_text`),
                     `placeholder` = VALUES(`placeholder`), `logo_url` = VALUES(`logo_url`), `domain` = VALUES(`domain`), `updated_at` = VALUES(`updated_at`)
                    """),
                cancellationToken,
                enabled,
                name,
                sub,
                greet,
                prompt,
                teaser,
                hold,
                logo,
                host,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return ErpSimpleWriteResult.Ok("Parts Agent config saved.", 1);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Parts Agent config table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP ajax casts enabled to int first; only a non-zero number turns the agent on.</summary>
    public static bool ParseEnabled(string? raw)
    {
        var key = (raw ?? string.Empty).Trim();
        return int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n != 0;
    }

    private static string Clip(string? raw, int max)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string ReadText(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";
}
