using System.Globalization;
using System.Net;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>office.php</c> create/edit and <c>offices.php</c> delete twins.</summary>
public interface ICpOfficeWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(CpOfficeSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UpdateAsync(CpOfficeSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string? officesJson, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveGeoAsync(long officeId, string? geoListJson, CancellationToken cancellationToken = default);
}

public sealed record CpOfficeSaveRequest(
    long OfficeId = 0,
    string? Caption = null,
    string? Country = null,
    string? Region = null,
    string? City = null,
    string? Address = null,
    string? Phone = null,
    string? Email = null,
    string? Coordinates = null,
    string? Description = null,
    string? UsersJson = null,
    string? Timetable = null,
    string? CaptionLangStrId = null,
    string? CountryLangStrId = null,
    string? RegionLangStrId = null,
    string? CityLangStrId = null,
    string? AddressLangStrId = null,
    string? DescriptionLangStrId = null,
    string? TimetableLangStrId = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed class CpOfficeWriteService : ICpOfficeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpOfficeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> CreateAsync(CpOfficeSaveRequest request, CancellationToken cancellationToken = default)
        => SaveAsync(request, create: true, cancellationToken);

    public Task<ErpSimpleWriteResult> UpdateAsync(CpOfficeSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OfficeId <= 0)
        {
            return Task.FromResult(ErpSimpleWriteResult.Fail("invalid", "An office id is required."));
        }

        return SaveAsync(request, create: false, cancellationToken);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(string? officesJson, CancellationToken cancellationToken = default)
    {
        var parsed = ParseOfficeIds(officesJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (parsed.Ids.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one office id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var placeholders = string.Join(",", parsed.Ids.Select(_ => "?"));
        var ids = parsed.Ids.Cast<object?>().ToArray();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_offices` WHERE `id` IN (" + placeholders + ")"),
            cancellationToken,
            ids).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_offices_storages_map` WHERE `office_id` IN (" + placeholders + ")"),
            cancellationToken,
            ids).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `shop_offices_geo_map` WHERE `office_id` IN (" + placeholders + ")"),
                cancellationToken,
                ids).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            // PHP reports geo-map failure; local/narrow schemas may omit the table.
        }

        return new ErpSimpleWriteResult(true, "ok", "Deleted", parsed.Ids[0], parsed.Ids.Count);
    }

    public async Task<ErpSimpleWriteResult> SaveGeoAsync(long officeId, string? geoListJson, CancellationToken cancellationToken = default)
    {
        if (officeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An office id is required.");
        }

        var parsed = ParseGeoIds(geoListJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_offices_geo_map` WHERE `office_id` = ?"),
                cancellationToken,
                officeId).ConfigureAwait(false);
            foreach (var geoId in parsed.Ids)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `shop_offices_geo_map` (`office_id`, `geo_id`) VALUES (?, ?)"),
                    cancellationToken,
                    officeId,
                    geoId).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save office geo membership.");
        }

        return new ErpSimpleWriteResult(true, "ok", "Saved", officeId, Math.Max(1, parsed.Ids.Count));
    }

    /// <summary>PHP office_geo_nodes.php <c>geo_list</c> JSON / comma list. Empty unlinks all.</summary>
    public static (IReadOnlyList<long> Ids, string? Error) ParseGeoIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0 || text == "[]")
        {
            return ([], null);
        }

        var parsed = ParseOfficeIds(text);
        return parsed.Error is null
            ? parsed
            : ([], parsed.Error.Contains("JSON", StringComparison.OrdinalIgnoreCase)
                ? "geo_list JSON is not valid."
                : "geo_list must be a JSON array of ids.");
    }

    /// <summary>PHP offices.php <c>offices</c> JSON / comma list.</summary>
    public static (IReadOnlyList<long> Ids, string? Error) ParseOfficeIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], "At least one office id is required.");
        }

        if (text[0] != '[')
        {
            var csv = new List<long>();
            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                {
                    csv.Add(id);
                }
            }

            return csv.Count == 0 ? ([], "At least one office id is required.") : (csv.Distinct().Take(80).ToList(), null);
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return ([], "offices must be a JSON array of ids.");
            }

            var ids = new List<long>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = item.ValueKind == System.Text.Json.JsonValueKind.Number && item.TryGetInt64(out var n)
                    ? n
                    : long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : 0;
                if (id > 0)
                {
                    ids.Add(id);
                }
            }

            return ids.Count == 0 ? ([], "At least one office id is required.") : (ids.Distinct().Take(80).ToList(), null);
        }
        catch (System.Text.Json.JsonException)
        {
            return ([], "offices JSON is not valid.");
        }
    }

    public static string SanitizeField(string? raw)
        => WebUtility.HtmlEncode((raw ?? string.Empty).Trim());

    public static string NextStrKey(string? domainPath, int createdCount)
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
           + "_"
           + Math.Max(1, createdCount).ToString(CultureInfo.InvariantCulture)
           + "_"
           + LegacyPasswordVerifier.Md5Hex(domainPath ?? string.Empty);

    private async Task<ErpSimpleWriteResult> SaveAsync(
        CpOfficeSaveRequest request,
        bool create,
        CancellationToken cancellationToken)
    {
        var caption = SanitizeField(request.Caption);
        if (caption.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Office caption is required.");
        }

        var users = CpStorageWriteService.NormalizeUsers(request.UsersJson);
        if (users.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", users.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lang = NormalizeLang(request.LangCode);
        var description = create ? "OFFICE CREATING" : "OFFICE EDITING";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string captionKey;
        string countryKey;
        string regionKey;
        string cityKey;
        string addressKey;
        string descriptionKey;
        string timetableKey;
        try
        {
            captionKey = await RequireTranslationAsync(connection, transaction, request.CaptionLangStrId, caption, lang, request.DomainPath, description, cancellationToken).ConfigureAwait(false);
            countryKey = await RequireTranslationAsync(connection, transaction, request.CountryLangStrId, SanitizeField(request.Country), lang, request.DomainPath, description, cancellationToken).ConfigureAwait(false);
            regionKey = await RequireTranslationAsync(connection, transaction, request.RegionLangStrId, SanitizeField(request.Region), lang, request.DomainPath, description, cancellationToken).ConfigureAwait(false);
            cityKey = await RequireTranslationAsync(connection, transaction, request.CityLangStrId, SanitizeField(request.City), lang, request.DomainPath, description, cancellationToken).ConfigureAwait(false);
            addressKey = await RequireTranslationAsync(connection, transaction, request.AddressLangStrId, SanitizeField(request.Address), lang, request.DomainPath, description, cancellationToken).ConfigureAwait(false);
            descriptionKey = await RequireTranslationAsync(connection, transaction, request.DescriptionLangStrId, SanitizeField(request.Description), lang, request.DomainPath, description, cancellationToken).ConfigureAwait(false);
            timetableKey = await RequireTranslationAsync(connection, transaction, request.TimetableLangStrId, SanitizeField(request.Timetable), lang, request.DomainPath, description, cancellationToken).ConfigureAwait(false);

            var phone = SanitizeField(request.Phone);
            var email = SanitizeField(request.Email);
            var coordinates = SanitizeField(request.Coordinates);
            if (create)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        INSERT INTO `shop_offices`
                        (`caption`, `country`, `region`, `city`, `address`, `phone`, `email`, `coordinates`, `description`, `users`, `timetable`)
                        VALUES (?,?,?,?,?,?,?,?,?,?,?)
                        """),
                    cancellationToken,
                    captionKey, countryKey, regionKey, cityKey, addressKey, phone, email, coordinates, descriptionKey, users.Json, timetableKey)
                    .ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        UPDATE `shop_offices`
                        SET `caption` = ?, `country` = ?, `region` = ?, `city` = ?, `address` = ?,
                            `phone` = ?, `email` = ?, `coordinates` = ?, `description` = ?, `users` = ?, `timetable` = ?
                        WHERE `id` = ?
                        """),
                    cancellationToken,
                    captionKey, countryKey, regionKey, cityKey, addressKey, phone, email, coordinates, descriptionKey, users.Json, timetableKey, request.OfficeId)
                    .ConfigureAwait(false);
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
            return ErpSimpleWriteResult.Fail("invalid", create ? "Could not create the office." : "Could not save the office.");
        }

        var id = create
            ? await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false)
            : request.OfficeId;
        return id > 0
            ? new ErpSimpleWriteResult(true, "ok", create ? "Office created." : "Office saved.", id, 1)
            : ErpSimpleWriteResult.Fail("invalid", "Could not create the office.");
    }

    private async Task<string> RequireTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string value,
        string langCode,
        string? domainPath,
        string description,
        CancellationToken cancellationToken)
    {
        var saved = await SaveCustomTranslationAsync(
            connection, transaction, langStrId, value, langCode, domainPath, description, cancellationToken).ConfigureAwait(false);
        if (saved.Error is not null || string.IsNullOrWhiteSpace(saved.Key))
        {
            throw new ErpWriteException(saved.Error ?? "Could not save the office translation.");
        }

        return saved.Key;
    }

    private async Task<(string? Key, string? Error)> SaveCustomTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string value,
        string langCode,
        string? domainPath,
        string description,
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
                    existingKey, langCode).ConfigureAwait(false);
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
                description, null, 0, 1, key).ConfigureAwait(false);
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
                value, key, langCode).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`value`, `str_key`, `lang_code`) VALUES (?,?,?)"),
                cancellationToken,
                value, key, langCode).ConfigureAwait(false);
        }

        return (key, null);
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
            var key = NextStrKey(domainPath, _createdStrings);
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

        throw new ErpWriteException("Could not allocate an office translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
