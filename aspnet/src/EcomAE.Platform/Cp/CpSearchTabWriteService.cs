using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>search_tabs.php</c> activation and <c>search_tab.php</c> save twins.</summary>
public interface ICpSearchTabWriteService
{
    Task<ErpSimpleWriteResult> SetEnabledAsync(long tabId, int enabled, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(CpSearchTabSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpSearchTabSaveRequest(
    long TabId = 0,
    string? Caption = null,
    string? CaptionLangStrId = null,
    int SortOrder = 0,
    int Enabled = 1,
    string? ParametersValues = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed class CpSearchTabWriteService : ICpSearchTabWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpSearchTabWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetEnabledAsync(long tabId, int enabled, CancellationToken cancellationToken = default)
    {
        if (tabId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A search tab id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var flag = enabled == 1 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_search_tabs` SET `enabled` = ? WHERE `id` = ?"),
            cancellationToken,
            flag,
            tabId).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok(flag == 1 ? "Search tab activated." : "Search tab deactivated.", tabId)
            : ErpSimpleWriteResult.Fail("not_found", "Search tab was not updated.");
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpSearchTabSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TabId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A search tab id is required.");
        }

        var parameters = NormalizeParameters(request.ParametersValues);
        if (parameters.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parameters.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var caption = WebUtility.HtmlEncode((request.Caption ?? string.Empty).Trim());
        var lang = NormalizeLang(request.LangCode);
        var enabled = request.Enabled == 1 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var captionKey = await RequireTranslationAsync(
                connection,
                transaction,
                request.CaptionLangStrId,
                caption,
                lang,
                request.DomainPath,
                cancellationToken).ConfigureAwait(false);
            var rows = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    UPDATE `shop_docpart_search_tabs`
                    SET `caption` = ?, `order` = ?, `enabled` = ?, `parameters_values` = ?
                    WHERE `id` = ?
                    """),
                cancellationToken,
                captionKey,
                request.SortOrder,
                enabled,
                parameters.Json,
                request.TabId).ConfigureAwait(false);
            if (rows <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "Search tab was not updated.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the search tab.");
        }

        return ErpSimpleWriteResult.Ok("Search tab saved.", request.TabId);
    }

    /// <summary>PHP search_tab.php stores a JSON object in <c>parameters_values</c> (not the whole form dump).</summary>
    public static (string Json, string? Error) NormalizeParameters(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ("{}", null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? (doc.RootElement.GetRawText(), null)
                : ("", "parameters_values must be a JSON object.");
        }
        catch (JsonException)
        {
            return ("", "parameters_values JSON is not valid.");
        }
    }

    /// <summary>PHP checkbox posts <c>tab_enabled=tab_enabled</c> when checked.</summary>
    public static int ParseEnabled(string? raw, int flag = 0)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text is "1" or "true" or "True" or "on" or "yes" or "tab_enabled")
        {
            return 1;
        }

        return flag == 1 ? 1 : 0;
    }

    private async Task<string> RequireTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string value,
        string langCode,
        string? domainPath,
        CancellationToken cancellationToken)
    {
        var existingKey = (langStrId ?? string.Empty).Trim();
        if (existingKey is "0")
        {
            existingKey = string.Empty;
        }

        var isCustom = 0L;
        var hasTranslation = 0L;
        if (existingKey.Length > 0)
        {
            isCustom = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `is_custom` FROM `lang_text_strings` WHERE `str_key` = ? LIMIT 1"),
                cancellationToken,
                existingKey).ConfigureAwait(false);
            if (isCustom == 0)
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                    cancellationToken,
                    existingKey).ConfigureAwait(false);
                if (found == 0)
                {
                    existingKey = string.Empty;
                }
            }

            if (existingKey.Length > 0)
            {
                hasTranslation = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?"),
                    cancellationToken,
                    existingKey,
                    langCode).ConfigureAwait(false);
            }
        }

        string key;
        if (existingKey.Length == 0 || isCustom == 0)
        {
            key = await AllocateStrKeyAsync(connection, transaction, domainPath, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `is_custom`, `str_key`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                "TAB EDITING",
                null,
                0,
                1,
                key).ConfigureAwait(false);
            hasTranslation = 0;
        }
        else
        {
            key = existingKey;
        }

        if (hasTranslation > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?"),
                cancellationToken,
                value,
                key,
                langCode).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`value`, `str_key`, `lang_code`) VALUES (?,?,?)"),
                cancellationToken,
                value,
                key,
                langCode).ConfigureAwait(false);
        }

        return key;
    }

    private async Task<string> AllocateStrKeyAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? domainPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            _createdStrings++;
            var key = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                      + "_"
                      + _createdStrings.ToString(CultureInfo.InvariantCulture)
                      + "_"
                      + LegacyPasswordVerifier.Md5Hex(domainPath ?? string.Empty);
            var found = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                cancellationToken,
                key).ConfigureAwait(false);
            if (found == 0)
            {
                return key;
            }
        }

        throw new ErpWriteException("Could not allocate a search-tab translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
