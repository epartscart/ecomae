using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live twin of the <c>save_setting</c> action of PHP <c>kkt/kkt_root_page.php</c>.</summary>
public interface ICpKktWriteService
{
    Task<ErpSimpleWriteResult> SaveDefaultsAsync(
        CpKktDefaults defaults,
        CancellationToken cancellationToken = default);
}

public sealed class CpKktWriteService : ICpKktWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpKktWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP posts every default as an integer field alongside <c>type</c> (1 = online payment, 2 = manual).</summary>
    public static CpKktDefaults ParseDefaults(IReadOnlyDictionary<string, string> form)
    {
        long Number(string name)
            => form.TryGetValue(name, out var raw)
               && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;

        var type = (int)Number("type");
        return new CpKktDefaults(
            type,
            Number("taxationSystem"),
            Number("kkt_device_id"),
            Number("check_product_tax"),
            Number("check_product_paymentMethodType"),
            Number("check_product_paymentSubjectType"),
            Number("check_payment_type"),
            Number("print") != 0);
    }

    public async Task<ErpSimpleWriteResult> SaveDefaultsAsync(
        CpKktDefaults defaults,
        CancellationToken cancellationToken = default)
    {
        if (defaults.Type is not (1 or 2))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Check defaults exist for type 1 (online payment) and type 2 (manual) only.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (defaults.DeviceId > 0)
        {
            await using var check = connection.CreateCommand();
            check.CommandText = ErpDb.Positional("SELECT COUNT(*) FROM `shop_kkt_devices` WHERE `id` = ?");
            ErpDb.AddParameters(check, defaults.DeviceId);
            var scalar = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is null or DBNull || Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 0)
            {
                return ErpSimpleWriteResult.Fail("missing", "The selected cash register does not exist.");
            }
        }

        var print = defaults.Print ? 1 : 0;
        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `shop_kkt_default_setting` SET `taxationSystem` = ?, `kkt_device_id` = ?, `check_product_tax` = ?, "
                + "`check_product_paymentMethodType` = ?, `check_product_paymentSubjectType` = ?, `check_payment_type` = ?, "
                + "`print` = ? WHERE `type` = ?"),
            cancellationToken,
            defaults.TaxationSystem,
            defaults.DeviceId,
            defaults.ProductTax,
            defaults.PaymentMethodType,
            defaults.PaymentSubjectType,
            defaults.PaymentType,
            print,
            defaults.Type).ConfigureAwait(false);

        if (updated == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_kkt_default_setting` (`type`, `taxationSystem`, `kkt_device_id`, `check_product_tax`, "
                    + "`check_product_paymentMethodType`, `check_product_paymentSubjectType`, `check_payment_type`, `print`) "
                    + "VALUES (?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                defaults.Type,
                defaults.TaxationSystem,
                defaults.DeviceId,
                defaults.ProductTax,
                defaults.PaymentMethodType,
                defaults.PaymentSubjectType,
                defaults.PaymentType,
                print).ConfigureAwait(false);
        }

        return new ErpSimpleWriteResult(
            true,
            "ok",
            defaults.Type == 1
                ? "Default settings for automatic checks after online payment saved."
                : "Default settings for manually created checks saved.",
            0,
            1);
    }
}
