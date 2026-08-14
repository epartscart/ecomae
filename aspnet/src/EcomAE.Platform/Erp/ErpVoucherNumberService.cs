using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Port of PHP <c>epc_erp_next_voucher_no</c>: atomic per type/year sequence rendered as
/// <c>PREFIX + YYYY + '-' + zero padded seq</c>, with tenant overrides from
/// <c>epc_erp_platform_settings</c> (<c>voucher_prefix_XX</c> / <c>voucher_pad_XX</c>).
/// </summary>
public interface IErpVoucherNumberService
{
    Task<string> NextAsync(DbConnection connection, DbTransaction? transaction, string voucherType, CancellationToken cancellationToken = default);
}

public sealed class ErpVoucherNumberService : IErpVoucherNumberService
{
    private static readonly Dictionary<string, string> PrefixMap = new(StringComparer.Ordinal)
    {
        ["PO"] = "PO-",
        ["SO"] = "SO-",
        ["PI"] = "PI-",
        ["SI"] = "SI-",
        ["RV"] = "RV-",
        ["PV"] = "PV-",
        ["GV"] = "GV-",
        ["TV"] = "TV-",
    };

    public async Task<string> NextAsync(DbConnection connection, DbTransaction? transaction, string voucherType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var type = NormalizeType(voucherType);
        if (!PrefixMap.TryGetValue(type, out var defaultPrefix))
        {
            throw new ErpWriteException("Unknown voucher type: " + type);
        }

        var prefix = await ResolvePrefixAsync(connection, transaction, type, defaultPrefix, cancellationToken).ConfigureAwait(false);
        var pad = await ResolvePadAsync(connection, transaction, type, cancellationToken).ConfigureAwait(false);
        var year = DateTimeOffset.Now.Year;
        var seq = await NextSequenceAsync(connection, transaction, type, year, cancellationToken).ConfigureAwait(false);

        return prefix
            + year.ToString(CultureInfo.InvariantCulture)
            + "-"
            + seq.ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0');
    }

    public static string NormalizeType(string voucherType)
    {
        var chars = (voucherType ?? string.Empty).ToUpperInvariant().Where(char.IsAsciiLetterUpper).ToArray();
        return new string(chars);
    }

    public static string Render(string prefix, int year, int seq, int pad)
        => prefix + year.ToString(CultureInfo.InvariantCulture) + "-" + seq.ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0');

    private static async Task<int> NextSequenceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string type,
        int year,
        CancellationToken cancellationToken)
    {
        var current = (int)await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = ? AND `year` = ? FOR UPDATE"),
            cancellationToken,
            type,
            year).ConfigureAwait(false);

        if (current <= 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_voucher_sequences` (`voucher_type`, `year`, `last_seq`) VALUES (?,?,1)"
                    + " ON DUPLICATE KEY UPDATE `last_seq` = `last_seq` + 1"),
                cancellationToken,
                type,
                year).ConfigureAwait(false);

            return (int)await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = ? AND `year` = ?"),
                cancellationToken,
                type,
                year).ConfigureAwait(false);
        }

        var next = current + 1;
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `epc_erp_voucher_sequences` SET `last_seq` = ? WHERE `voucher_type` = ? AND `year` = ?"),
            cancellationToken,
            next,
            type,
            year).ConfigureAwait(false);

        return next;
    }

    private static async Task<string> ResolvePrefixAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string type,
        string defaultPrefix,
        CancellationToken cancellationToken)
    {
        var setting = await ErpPlatformSettings.GetAsync(connection, transaction, "voucher_prefix_" + type, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(setting) ? defaultPrefix : setting.Trim();
    }

    private static async Task<int> ResolvePadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string type,
        CancellationToken cancellationToken)
    {
        var setting = await ErpPlatformSettings.GetAsync(connection, transaction, "voucher_pad_" + type, cancellationToken).ConfigureAwait(false);
        if (int.TryParse(setting, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pad) && pad >= 1 && pad <= 10)
        {
            return pad;
        }

        return 5;
    }
}

/// <summary>Port of PHP <c>epc_erp_platform_setting_get</c> (tenant key/value settings).</summary>
internal static class ErpPlatformSettings
{
    public static async Task<string?> GetAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `setting_value` FROM `epc_erp_platform_settings` WHERE `setting_key` = ? LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }
}
