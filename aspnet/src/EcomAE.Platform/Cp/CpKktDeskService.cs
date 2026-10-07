using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>Row of a PHP fiscal reference table (<c>shop_kkt_ref_tag_*</c>, <c>shop_kkt_ref_payment_types_tags</c>).</summary>
public sealed record CpKktRefValue(long Value, string ForPrint);

/// <summary>Cash register from <c>shop_kkt_devices</c> with the description of its wired interface.</summary>
public sealed record CpKktDevice(long Id, string Name, string Handler, string InterfaceDescription);

/// <summary>One <c>shop_kkt_default_setting</c> row: type 2 = manual check, type 1 = automatic check after online payment.</summary>
public sealed record CpKktDefaults(
    int Type,
    long TaxationSystem,
    long DeviceId,
    long ProductTax,
    long PaymentMethodType,
    long PaymentSubjectType,
    long PaymentType,
    bool Print)
{
    public static CpKktDefaults Empty(int type) => new(type, 0, 0, 0, 0, 0, 0, false);
}

public sealed record CpKktCheckProduct(
    long Id,
    string Text,
    decimal Price,
    decimal Count,
    decimal Sum,
    string PaymentMethodType,
    string PaymentSubjectType,
    string Tax,
    long OrderId,
    long OrderItemId);

public sealed record CpKktCheckPayment(long Id, decimal Amount, string TypeForPrint);

/// <summary>Correction-check block of <c>shop_kkt_checks</c> (tags 1173/1174/1178/1179 and the six tax sums).</summary>
public sealed record CpKktCorrection(
    long CorrectionType,
    string Description,
    long CauseDocumentDate,
    string CauseDocumentNumber,
    decimal TotalSum,
    decimal CashSum,
    decimal ECashSum,
    decimal PrepaymentSum,
    decimal PostpaymentSum,
    decimal OtherPaymentTypeSum,
    decimal Tax1Sum,
    decimal Tax2Sum,
    decimal Tax3Sum,
    decimal Tax4Sum,
    decimal Tax5Sum,
    decimal Tax6Sum);

public sealed record CpKktCheck(
    long Id,
    long DeviceId,
    string DeviceName,
    string TypeForPrint,
    string TaxationSystemForPrint,
    string CustomerContact,
    long TimeCreated,
    bool SentToDevice,
    bool DeviceApproved,
    string DeviceAnswer,
    bool IsCorrection,
    decimal Sum,
    CpKktCorrection Correction,
    IReadOnlyList<CpKktCheckProduct> Products,
    IReadOnlyList<CpKktCheckPayment> Payments);

/// <summary>PHP <c>checks.php</c> filter fields — <c>-1</c> and empty text are the "all" defaults.</summary>
public sealed record CpKktCheckFilter(
    long CheckId = 0,
    long DeviceId = -1,
    long Type = -1,
    string CustomerContact = "",
    long TaxationSystem = -1,
    long TimeFrom = 0,
    long TimeTo = 0,
    long SentFlag = -1,
    long ApprovedFlag = -1,
    long CorrectionFlag = -1,
    string ProductText = "",
    long ProductTax = -1,
    long ProductPaymentMethodType = -1,
    long ProductPaymentSubjectType = -1,
    long PaymentType = -1,
    long OrderId = 0,
    long OrderItemId = 0)
{
    public static CpKktCheckFilter Default { get; } = new();
}

public sealed record CpKktChecksPage(
    IReadOnlyList<CpKktCheck> Checks,
    int Page,
    int PageSize,
    int TotalRows,
    string SortField,
    bool SortAscending)
{
    public int PageCount => PageSize <= 0 ? 0 : (TotalRows + PageSize - 1) / PageSize;
}

public sealed record CpKktDesk(
    bool Available,
    string Message,
    IReadOnlyList<CpKktDevice> Devices,
    IReadOnlyList<CpKktRefValue> TaxationSystems,
    IReadOnlyList<CpKktRefValue> CheckTypes,
    IReadOnlyList<CpKktRefValue> ProductTaxes,
    IReadOnlyList<CpKktRefValue> PaymentMethodTypes,
    IReadOnlyList<CpKktRefValue> PaymentSubjectTypes,
    IReadOnlyList<CpKktRefValue> PaymentTypes,
    CpKktDefaults ManualDefaults,
    CpKktDefaults OnlineDefaults)
{
    public static CpKktDesk Unavailable(string message)
        => new(false, message, [], [], [], [], [], [], [], CpKktDefaults.Empty(2), CpKktDefaults.Empty(1));
}

/// <summary>
/// Read twin of the PHP online-cashier pages <c>cp/content/shop/kkt/kkt_root_page.php</c>,
/// <c>devices.php</c> and <c>checks.php</c>: fiscal reference tables, registered devices,
/// the two default-setting rows and the filtered/paged fiscal check register.
/// </summary>
public interface ICpKktDeskService
{
    Task<CpKktDesk> LoadDeskAsync(CancellationToken cancellationToken = default);

    Task<CpKktChecksPage> LoadChecksAsync(
        CpKktCheckFilter filter,
        int page,
        string sortField,
        bool sortAscending,
        CancellationToken cancellationToken = default);
}

public sealed class CpKktDeskService : ICpKktDeskService
{
    /// <summary>PHP <c>DP_Config->list_page_limit</c> default.</summary>
    public const int PageLimit = 20;

    /// <summary>PHP guards the cookie-supplied sort column against injection with this whitelist.</summary>
    public static readonly IReadOnlyList<string> SortableFields =
    [
        "check_id",
        "kkt_device_id",
        "type",
        "taxationSystem",
        "sent_to_real_device_flag",
        "real_device_approved_flag",
        "is_correction_flag",
        "check_sum",
        "time_created"
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public CpKktDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>Unknown columns fall back to <c>check_id</c>, exactly as PHP does with the sort cookie.</summary>
    public static string NormaliseSortField(string? field)
        => SortableFields.Contains(field ?? "", StringComparer.Ordinal) ? field! : "check_id";

    /// <summary>Builds the PHP <c>WHERE</c> clause of <c>checks.php</c> over the derived check projection.</summary>
    public static (string Where, IReadOnlyList<object?> Values) BuildFilter(CpKktCheckFilter filter)
    {
        var clauses = new List<string>();
        var values = new List<object?>();

        void Equal(string column, long value, long ignore)
        {
            if (value == ignore)
            {
                return;
            }

            clauses.Add("`" + column + "` = ?");
            values.Add(value);
        }

        void Like(string column, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            clauses.Add("`" + column + "` LIKE ?");
            values.Add("%" + value + "%");
        }

        Equal("check_id", filter.CheckId, 0);
        Equal("kkt_device_id", filter.DeviceId, -1);
        Equal("type", filter.Type, -1);
        Like("customerContact", filter.CustomerContact);
        Equal("taxationSystem", filter.TaxationSystem, -1);

        if (filter.TimeFrom > 0)
        {
            clauses.Add("`time_created` > ?");
            values.Add(filter.TimeFrom);
        }

        if (filter.TimeTo > 0)
        {
            clauses.Add("`time_created` < ?");
            values.Add(filter.TimeTo);
        }

        Equal("sent_to_real_device_flag", filter.SentFlag, -1);
        Equal("real_device_approved_flag", filter.ApprovedFlag, -1);
        Equal("is_correction_flag", filter.CorrectionFlag, -1);
        Like("check_product_text", filter.ProductText);
        Equal("check_product_tax", filter.ProductTax, -1);
        Equal("check_product_paymentMethodType", filter.ProductPaymentMethodType, -1);
        Equal("check_product_paymentSubjectType", filter.ProductPaymentSubjectType, -1);
        Equal("check_payment_type", filter.PaymentType, -1);

        // PHP counts matching child rows in the projection and filters on "at least one match".
        if (filter.OrderItemId > 0)
        {
            clauses.Add("`count_order_item_id` > 0");
        }

        if (filter.OrderId > 0)
        {
            clauses.Add("`count_order_id` > 0");
        }

        return (clauses.Count == 0 ? "" : " WHERE " + string.Join(" AND ", clauses), values);
    }

    public async Task<CpKktDesk> LoadDeskAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpKktDesk.Unavailable("No database configured — online cashiers are unavailable.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var devices = new List<CpKktDevice>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`handler`,''), "
                                  + "IFNULL((SELECT `description` FROM `shop_kkt_interfaces_types` "
                                  + "WHERE `handler` = `shop_kkt_devices`.`handler` LIMIT 1),'') "
                                  + "FROM `shop_kkt_devices` ORDER BY `name`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    devices.Add(new CpKktDevice(
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        r.GetString(2),
                        r.GetString(3)));
                }
            }

            var taxationSystems = await LoadReferenceAsync(connection, "shop_kkt_ref_tag_1055", cancellationToken).ConfigureAwait(false);
            var checkTypes = await LoadReferenceAsync(connection, "shop_kkt_ref_tag_1054", cancellationToken).ConfigureAwait(false);
            var productTaxes = await LoadReferenceAsync(connection, "shop_kkt_ref_tag_1199", cancellationToken).ConfigureAwait(false);
            var methodTypes = await LoadReferenceAsync(connection, "shop_kkt_ref_tag_1214", cancellationToken).ConfigureAwait(false);
            var subjectTypes = await LoadReferenceAsync(connection, "shop_kkt_ref_tag_1212", cancellationToken).ConfigureAwait(false);
            var paymentTypes = await LoadReferenceAsync(connection, "shop_kkt_ref_payment_types_tags", cancellationToken).ConfigureAwait(false);

            var manual = await LoadDefaultsAsync(connection, 2, cancellationToken).ConfigureAwait(false);
            var online = await LoadDefaultsAsync(connection, 1, cancellationToken).ConfigureAwait(false);

            return new CpKktDesk(
                true,
                "",
                devices,
                taxationSystems,
                checkTypes,
                productTaxes,
                methodTypes,
                subjectTypes,
                paymentTypes,
                manual,
                online);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new CpKktDesk(true, string.Empty, [], [], [], [], [], [], [], CpKktDefaults.Empty(2), CpKktDefaults.Empty(1));
        }
        catch (DbException ex)
        {
            return CpKktDesk.Unavailable("Online cashier tables are unavailable: " + ex.Message);
        }
    }

    public async Task<CpKktChecksPage> LoadChecksAsync(
        CpKktCheckFilter filter,
        int page,
        string sortField,
        bool sortAscending,
        CancellationToken cancellationToken = default)
    {
        var sort = NormaliseSortField(sortField);
        if (page < 0)
        {
            page = 0;
        }

        if (!_connections.IsConfigured)
        {
            return new CpKktChecksPage([], page, PageLimit, 0, sort, sortAscending);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var (where, filterValues) = BuildFilter(filter);
            // PHP binds the order-item / order id of the child-count columns first, then the WHERE values.
            var values = new List<object?> { filter.OrderItemId, filter.OrderId };
            values.AddRange(filterValues);

            var projection = Projection();
            var checks = new List<CpKktCheck>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT * FROM (" + projection + ") t1" + where
                    + " ORDER BY `" + sort + "` " + (sortAscending ? "ASC" : "DESC")
                    + " LIMIT " + (page * PageLimit).ToString(CultureInfo.InvariantCulture)
                    + "," + PageLimit.ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(cmd, values.ToArray());
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    checks.Add(ReadCheck(r));
                }
            }

            var total = 0;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT COUNT(*) FROM (" + projection + ") t1" + where);
                ErpDb.AddParameters(cmd, values.ToArray());
                var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                total = scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
            }

            var hydrated = new List<CpKktCheck>(checks.Count);
            foreach (var check in checks)
            {
                var products = check.IsCorrection
                    ? []
                    : await LoadProductsAsync(connection, check.Id, cancellationToken).ConfigureAwait(false);
                var payments = check.IsCorrection
                    ? []
                    : await LoadPaymentsAsync(connection, check.Id, cancellationToken).ConfigureAwait(false);
                hydrated.Add(check with { Products = products, Payments = payments });
            }

            return new CpKktChecksPage(hydrated, page, PageLimit, total, sort, sortAscending);
        }
        catch (DbException)
        {
            return new CpKktChecksPage([], page, PageLimit, 0, sort, sortAscending);
        }
    }

    private static string Projection()
        => """
           SELECT
           `shop_kkt_checks`.`id` AS `id`,
           `shop_kkt_checks`.`id` AS `check_id`,
           `shop_kkt_checks`.`kkt_device_id`,
           `shop_kkt_checks`.`type`,
           IFNULL(`shop_kkt_checks`.`customerContact`,'') AS `customerContact`,
           `shop_kkt_checks`.`taxationSystem`,
           `shop_kkt_checks`.`time_created`,
           `shop_kkt_checks`.`sent_to_real_device_flag`,
           `shop_kkt_checks`.`real_device_approved_flag`,
           IFNULL(`shop_kkt_checks`.`real_device_text_answer`,'') AS `real_device_text_answer`,
           `shop_kkt_checks`.`is_correction_flag`,
           IFNULL(`shop_kkt_checks`.`correction_type`,0) AS `correction_type`,
           IFNULL(`shop_kkt_checks`.`correction_description`,'') AS `correction_description`,
           IFNULL(`shop_kkt_checks`.`correction_causeDocumentDate`,0) AS `correction_causeDocumentDate`,
           IFNULL(`shop_kkt_checks`.`correction_causeDocumentNumber`,'') AS `correction_causeDocumentNumber`,
           IFNULL(`shop_kkt_checks`.`correction_totalSum`,0) AS `correction_totalSum`,
           IFNULL(`shop_kkt_checks`.`correction_cashSum`,0) AS `correction_cashSum`,
           IFNULL(`shop_kkt_checks`.`correction_eCashSum`,0) AS `correction_eCashSum`,
           IFNULL(`shop_kkt_checks`.`correction_prepaymentSum`,0) AS `correction_prepaymentSum`,
           IFNULL(`shop_kkt_checks`.`correction_postpaymentSum`,0) AS `correction_postpaymentSum`,
           IFNULL(`shop_kkt_checks`.`correction_otherPaymentTypeSum`,0) AS `correction_otherPaymentTypeSum`,
           IFNULL(`shop_kkt_checks`.`correction_tax1Sum`,0) AS `correction_tax1Sum`,
           IFNULL(`shop_kkt_checks`.`correction_tax2Sum`,0) AS `correction_tax2Sum`,
           IFNULL(`shop_kkt_checks`.`correction_tax3Sum`,0) AS `correction_tax3Sum`,
           IFNULL(`shop_kkt_checks`.`correction_tax4Sum`,0) AS `correction_tax4Sum`,
           IFNULL(`shop_kkt_checks`.`correction_tax5Sum`,0) AS `correction_tax5Sum`,
           IFNULL(`shop_kkt_checks`.`correction_tax6Sum`,0) AS `correction_tax6Sum`,
           IFNULL((SELECT `name` FROM `shop_kkt_devices` WHERE `id` = `shop_kkt_checks`.`kkt_device_id`),'') AS `kkt_device_name`,
           IFNULL((SELECT `for_print` FROM `shop_kkt_ref_tag_1055` WHERE `value` = `shop_kkt_checks`.`taxationSystem`),'') AS `taxationSystem_for_print`,
           IFNULL((SELECT `for_print` FROM `shop_kkt_ref_tag_1054` WHERE `value` = `shop_kkt_checks`.`type`),'') AS `type_for_print`,
           IFNULL(IF(`is_correction_flag` = 0, (SELECT SUM(`amount`) FROM `shop_kkt_checks_payments` WHERE `check_id` = `shop_kkt_checks`.`id`), `correction_totalSum`),0) AS `check_sum`,
           IFNULL((SELECT `text` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1),'') AS `check_product_text`,
           IFNULL((SELECT `tax` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1),-1) AS `check_product_tax`,
           IFNULL((SELECT `paymentMethodType` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1),-1) AS `check_product_paymentMethodType`,
           IFNULL((SELECT `paymentSubjectType` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1),-1) AS `check_product_paymentSubjectType`,
           IFNULL((SELECT `type` FROM `shop_kkt_checks_payments` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1),-1) AS `check_payment_type`,
           (SELECT COUNT(`id`) FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` IN (SELECT `id` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id`) AND `order_item_id` = ?) AS `count_order_item_id`,
           (SELECT COUNT(`order_id`) FROM `shop_orders_items` WHERE `order_id` = ? AND `id` IN (SELECT `order_item_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` IN (SELECT `id` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id`))) AS `count_order_id`
           FROM `shop_kkt_checks`
           """;

    private static CpKktCheck ReadCheck(DbDataReader r)
    {
        long Int(string column) => Convert.ToInt64(r.GetValue(r.GetOrdinal(column)), CultureInfo.InvariantCulture);
        decimal Dec(string column) => Convert.ToDecimal(r.GetValue(r.GetOrdinal(column)), CultureInfo.InvariantCulture);
        string Str(string column) => r.GetValue(r.GetOrdinal(column)).ToString() ?? "";

        var correction = new CpKktCorrection(
            Int("correction_type"),
            Str("correction_description"),
            Int("correction_causeDocumentDate"),
            Str("correction_causeDocumentNumber"),
            Dec("correction_totalSum"),
            Dec("correction_cashSum"),
            Dec("correction_eCashSum"),
            Dec("correction_prepaymentSum"),
            Dec("correction_postpaymentSum"),
            Dec("correction_otherPaymentTypeSum"),
            Dec("correction_tax1Sum"),
            Dec("correction_tax2Sum"),
            Dec("correction_tax3Sum"),
            Dec("correction_tax4Sum"),
            Dec("correction_tax5Sum"),
            Dec("correction_tax6Sum"));

        return new CpKktCheck(
            Int("id"),
            Int("kkt_device_id"),
            Str("kkt_device_name"),
            Str("type_for_print"),
            Str("taxationSystem_for_print"),
            Str("customerContact"),
            Int("time_created"),
            Int("sent_to_real_device_flag") == 1,
            Int("real_device_approved_flag") == 1,
            Str("real_device_text_answer"),
            Int("is_correction_flag") != 0,
            Dec("check_sum"),
            correction,
            [],
            []);
    }

    private static async Task<IReadOnlyList<CpKktRefValue>> LoadReferenceAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpKktRefValue>();
        await using var cmd = connection.CreateCommand();
        // Table names come from this file's own constants, never from a request.
        cmd.CommandText = "SELECT `value`, IFNULL(`for_print`,'') FROM `" + table + "` ORDER BY `value`";
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpKktRefValue(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1)));
        }

        return rows;
    }

    private static async Task<CpKktDefaults> LoadDefaultsAsync(
        DbConnection connection,
        int type,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`taxationSystem`,0), IFNULL(`kkt_device_id`,0), IFNULL(`check_product_tax`,0), "
            + "IFNULL(`check_product_paymentMethodType`,0), IFNULL(`check_product_paymentSubjectType`,0), "
            + "IFNULL(`check_payment_type`,0), IFNULL(`print`,0) "
            + "FROM `shop_kkt_default_setting` WHERE `type` = ? LIMIT 1");
        ErpDb.AddParameters(cmd, type);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CpKktDefaults.Empty(type);
        }

        return new CpKktDefaults(
            type,
            Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
            Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
            Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToInt64(r.GetValue(3), CultureInfo.InvariantCulture),
            Convert.ToInt64(r.GetValue(4), CultureInfo.InvariantCulture),
            Convert.ToInt64(r.GetValue(5), CultureInfo.InvariantCulture),
            Convert.ToInt64(r.GetValue(6), CultureInfo.InvariantCulture) != 0);
    }

    private static async Task<IReadOnlyList<CpKktCheckProduct>> LoadProductsAsync(
        DbConnection connection,
        long checkId,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpKktCheckProduct>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`, IFNULL(`text`,''), IFNULL(`price`,0), IFNULL(`count`,0), IFNULL(`price`,0)*IFNULL(`count`,0), "
            + "IFNULL((SELECT `for_print` FROM `shop_kkt_ref_tag_1214` WHERE `value` = `shop_kkt_checks_products`.`paymentMethodType`),''), "
            + "IFNULL((SELECT `for_print` FROM `shop_kkt_ref_tag_1212` WHERE `value` = `shop_kkt_checks_products`.`paymentSubjectType`),''), "
            + "IFNULL((SELECT `for_print` FROM `shop_kkt_ref_tag_1199` WHERE `value` = `shop_kkt_checks_products`.`tax`),''), "
            + "IFNULL((SELECT `order_id` FROM `shop_orders_items` WHERE `id` IN (SELECT `order_item_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` = `shop_kkt_checks_products`.`id`)),0), "
            + "IFNULL((SELECT `order_item_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` = `shop_kkt_checks_products`.`id`),0) "
            + "FROM `shop_kkt_checks_products` WHERE `check_id` = ? ORDER BY `id`");
        ErpDb.AddParameters(cmd, checkId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpKktCheckProduct(
                Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                r.GetString(1),
                Convert.ToDecimal(r.GetValue(2), CultureInfo.InvariantCulture),
                Convert.ToDecimal(r.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToDecimal(r.GetValue(4), CultureInfo.InvariantCulture),
                r.GetString(5),
                r.GetString(6),
                r.GetString(7),
                Convert.ToInt64(r.GetValue(8), CultureInfo.InvariantCulture),
                Convert.ToInt64(r.GetValue(9), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpKktCheckPayment>> LoadPaymentsAsync(
        DbConnection connection,
        long checkId,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpKktCheckPayment>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`, IFNULL(`amount`,0), "
            + "IFNULL((SELECT `for_print` FROM `shop_kkt_ref_payment_types_tags` WHERE `value` = `shop_kkt_checks_payments`.`type`),'') "
            + "FROM `shop_kkt_checks_payments` WHERE `check_id` = ? ORDER BY `id`");
        ErpDb.AddParameters(cmd, checkId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpKktCheckPayment(
                Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture),
                r.GetString(2)));
        }

        return rows;
    }
}
