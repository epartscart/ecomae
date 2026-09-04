using System.Net;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>offices_cash.php</c> action=add and <c>offices_cash_editor.php</c> action=del twins.
/// Code add stays PHP (<c>save_custom_translation</c>).
/// </summary>
public interface IErpOfficesCashWriteService
{
    Task<ErpSimpleWriteResult> AddEntryAsync(
        long managerId,
        long officeId,
        int income,
        decimal amount,
        long operationCodeId,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteCodeAsync(
        long managerId,
        long officeId,
        long codeId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpOfficesCashWriteService : IErpOfficesCashWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpOfficesCashWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddEntryAsync(
        long managerId,
        long officeId,
        int income,
        decimal amount,
        long operationCodeId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (managerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A signed-in manager is required.");
        }

        if (officeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An office is required.");
        }

        if (amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Amount must be greater than zero.");
        }

        if (operationCodeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cash operation code is required.");
        }

        var flag = income > 0 ? 1 : 0;
        var note = SanitizeComment(comment);

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var office = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ? LIMIT 1"),
            cancellationToken,
            officeId,
            "%\"" + managerId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"%");
        if (office <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This office is not assigned to the signed-in manager.");
        }

        var code = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices_cash_codes` WHERE `income` = ? AND `id` = ? AND `office_id` = ? LIMIT 1"),
            cancellationToken,
            flag, operationCodeId, officeId);
        if (code <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "The cash operation code is not valid for this office.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_offices_cash` (`id`, `office_id`, `manager_id`, `time`, `income`, `amount`, `operation_code`, `comment`) "
                + "VALUES (NULL, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            officeId,
            managerId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            flag,
            amount,
            operationCodeId,
            note);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Cash entry saved.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteCodeAsync(
        long managerId,
        long officeId,
        long codeId,
        CancellationToken cancellationToken = default)
    {
        if (managerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A signed-in manager is required.");
        }

        if (officeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An office is required.");
        }

        if (codeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cash operation code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var office = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ? LIMIT 1"),
            cancellationToken,
            officeId,
            "%\"" + managerId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"%");
        if (office <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This office is not assigned to the signed-in manager.");
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices_cash_codes` WHERE `id` = ? AND `office_id` = ? LIMIT 1"),
            cancellationToken,
            codeId, officeId);
        if (existing <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cash operation code was not found for this office.");
        }

        var used = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices_cash` WHERE `operation_code` = ? LIMIT 1"),
            cancellationToken,
            codeId);
        if (used > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This cash operation code is used by existing cash entries.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_offices_cash_codes` WHERE `id` = ?"),
            cancellationToken,
            codeId);
        return ErpSimpleWriteResult.Ok("Cash operation code deleted.", codeId);
    }

    /// <summary>PHP offices_cash comment: strip quotes/backslash/CR/tab, htmlentities, newline → br.</summary>
    internal static string SanitizeComment(string? raw)
    {
        var comment = (raw ?? string.Empty).Trim();
        comment = comment
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("\\", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        comment = WebUtility.HtmlEncode(comment);
        return comment.Replace("\n", "<br/>", StringComparison.Ordinal);
    }
}
