using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>obtaining_modes.php</c> activation and <c>obtaining_mode.php</c> save twins.</summary>
public interface ICpObtainingModeWriteService
{
    Task<ErpSimpleWriteResult> SetAvailableAsync(long modeId, int available, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(CpObtainingModeSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpObtainingModeSaveRequest(
    long ModeId = 0,
    string? Caption = null,
    string? CaptionLangStrId = null,
    int SortOrder = 0,
    int Available = 1,
    string? ParametersValues = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed class CpObtainingModeWriteService : ICpObtainingModeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpObtainingModeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetAvailableAsync(long modeId, int available, CancellationToken cancellationToken = default)
    {
        if (modeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A delivery method id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var flag = available == 1 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_obtaining_modes` SET `available` = ? WHERE `id` = ?"),
            cancellationToken,
            flag,
            modeId).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok(flag == 1 ? "Delivery method activated." : "Delivery method deactivated.", modeId)
            : ErpSimpleWriteResult.Fail("not_found", "Delivery method was not updated.");
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpObtainingModeSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ModeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A delivery method id is required.");
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

        var caption = (request.Caption ?? string.Empty).Trim();
        var lang = NormalizeLang(request.LangCode);
        var available = request.Available == 1 ? 1 : 0;
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
                    UPDATE `shop_obtaining_modes`
                    SET `caption` = ?, `order` = ?, `available` = ?, `parameters_values` = ?
                    WHERE `id` = ?
                    """),
                cancellationToken,
                captionKey,
                request.SortOrder,
                available,
                parameters.Json,
                request.ModeId).ConfigureAwait(false);
            if (rows <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "Delivery method was not updated.");
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
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the delivery method.");
        }

        return ErpSimpleWriteResult.Ok("Delivery method saved.", request.ModeId);
    }

    /// <summary>PHP <c>obtaining_mode.php</c> <c>parameters_values</c> JSON object.</summary>
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
                "DELIVERY METHOD EDITING",
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

        throw new ErpWriteException("Could not allocate a delivery-method translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
