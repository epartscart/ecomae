using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpPosTerminalWarehouse(long Id, string Label);

public sealed record CpPosOpenSessionInfo(long Id, string SessionNo, decimal OpeningFloat);

public sealed record CpPosTerminalContext(
    bool DbAvailable,
    bool PosEnabled,
    string RegisterName,
    CpPosOpenSessionInfo? OpenSession,
    int TodaySales,
    decimal TodayTotal,
    int WeekSales,
    decimal WeekTotal,
    decimal TaxRate,
    string TaxLabel,
    string Currency,
    string CountryCode,
    IReadOnlyList<CpPosTerminalWarehouse> Warehouses,
    long WarehouseId,
    string WarehouseName)
{
    public static CpPosTerminalContext Unavailable { get; } = new(
        false, true, "Register 1", null, 0, 0m, 0, 0m, 0m, "VAT", "AED", "AE", [], 0, "Default warehouse");
}

public sealed record CpPosSessionStatus(CpPosOpenSessionInfo? OpenSession, int TodaySales, decimal TodayTotal, int WeekSales, decimal WeekTotal);

/// <summary>
/// Read side of PHP <c>epc_pos_terminal.php</c>: settings, open shift, shift KPIs
/// (<c>epc_pos_dashboard_stats</c>), warehouses (<c>epc_erp_inventory_list_warehouses</c>),
/// walk-in tax context (<c>epc_tax_toolkit_resolve</c>) and the world currency for the country.
/// </summary>
public interface ICpPosTerminalService
{
    Task<CpPosTerminalContext> LoadAsync(CancellationToken cancellationToken = default);

    Task<CpPosSessionStatus> SessionStatusAsync(CancellationToken cancellationToken = default);
}

public sealed class CpPosTerminalService : ICpPosTerminalService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpTaxAmountCalculator? _tax;
    private readonly ICpPosWriteService _pos;

    public CpPosTerminalService(IErpWriteConnectionFactory connections, ICpPosWriteService pos, IErpTaxAmountCalculator? tax = null)
    {
        _connections = connections;
        _pos = pos;
        _tax = tax;
    }

    private static readonly IReadOnlyDictionary<string, string> WorldCurrency = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AE"] = "AED", ["SA"] = "SAR", ["OM"] = "OMR", ["BH"] = "BHD", ["KW"] = "KWD", ["QA"] = "QAR", ["IN"] = "INR", ["PK"] = "PKR",
        ["GB"] = "GBP", ["UK"] = "GBP", ["US"] = "USD", ["CA"] = "CAD", ["AU"] = "AUD", ["NZ"] = "NZD", ["SG"] = "SGD", ["MY"] = "MYR",
        ["ID"] = "IDR", ["PH"] = "PHP", ["TH"] = "THB", ["VN"] = "VND", ["CN"] = "CNY", ["JP"] = "JPY", ["KR"] = "KRW", ["HK"] = "HKD",
        ["CH"] = "CHF", ["NO"] = "NOK", ["IS"] = "ISK", ["TR"] = "TRY", ["RU"] = "RUB", ["UA"] = "UAH", ["ZA"] = "ZAR", ["NG"] = "NGN",
        ["KE"] = "KES", ["EG"] = "EGP", ["MA"] = "MAD", ["BR"] = "BRL", ["MX"] = "MXN", ["AR"] = "ARS", ["CL"] = "CLP", ["CO"] = "COP",
        ["DE"] = "EUR", ["FR"] = "EUR", ["IT"] = "EUR", ["ES"] = "EUR", ["NL"] = "EUR", ["BE"] = "EUR", ["AT"] = "EUR", ["IE"] = "EUR",
        ["PL"] = "PLN", ["SE"] = "SEK", ["DK"] = "DKK", ["FI"] = "EUR", ["PT"] = "EUR", ["GR"] = "EUR", ["CZ"] = "CZK", ["RO"] = "RON",
        ["HU"] = "HUF", ["BD"] = "BDT", ["LK"] = "LKR", ["NP"] = "NPR",
    };

    public static string CurrencyFor(string? countryCode)
        => !string.IsNullOrWhiteSpace(countryCode) && WorldCurrency.TryGetValue(countryCode.Trim(), out var cur) ? cur : "AED";

    public static string WarehouseLabel(string? name, string? code)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length > 0)
        {
            return n;
        }

        var c = (code ?? string.Empty).Trim();
        return c.Length > 0 ? c : "Warehouse";
    }

    public static long PickWarehouse(long settingsWarehouseId, IReadOnlyList<CpPosTerminalWarehouse> warehouses)
        => settingsWarehouseId > 0 ? settingsWarehouseId : warehouses.Count > 0 ? warehouses[0].Id : 0;

    public async Task<CpPosTerminalContext> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPosTerminalContext.Unavailable;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var posEnabled = true;
            var registerName = "Register 1";
            long settingsWarehouse = 0;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`pos_enabled`,1), IFNULL(`register_name`,'Register 1'), IFNULL(`default_warehouse_id`,0) FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1");
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    posEnabled = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture) != 0;
                    registerName = r.GetString(1);
                    settingsWarehouse = Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture);
                }
            }

            var status = await SessionStatusCoreAsync(connection, cancellationToken).ConfigureAwait(false);

            var warehouses = new List<CpPosTerminalWarehouse>();
            try
            {
                await using var c = connection.CreateCommand();
                c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`name`,''), IFNULL(`code`,'') FROM `epc_erp_inv_warehouses` WHERE `active` = 1 ORDER BY `name`");
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    warehouses.Add(new CpPosTerminalWarehouse(
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        WarehouseLabel(r.GetString(1), r.GetString(2))));
                }
            }
            catch (DbException)
            {
                warehouses.Clear();
            }

            var warehouseId = PickWarehouse(settingsWarehouse, warehouses);
            var warehouseName = warehouses.FirstOrDefault(w => w.Id == warehouseId)?.Label ?? "Default warehouse";

            decimal taxRate = 0m;
            var taxLabel = "VAT";
            var country = "AE";
            if (_tax is not null)
            {
                try
                {
                    var walkin = await _pos.EnsureWalkinUserAsync(cancellationToken).ConfigureAwait(false);
                    var tax = await _tax.CalcAsync(connection, null, 100m, (int)Math.Clamp(walkin.Id, 0, int.MaxValue), 0, false, cancellationToken).ConfigureAwait(false);
                    taxRate = tax.TaxRate;
                    if (!string.IsNullOrWhiteSpace(tax.TaxLabel))
                    {
                        taxLabel = tax.TaxLabel;
                    }

                    if (!string.IsNullOrWhiteSpace(tax.CountryCode))
                    {
                        country = tax.CountryCode.Trim().ToUpperInvariant();
                    }
                }
                catch (DbException)
                {
                }
            }

            return new CpPosTerminalContext(
                true,
                posEnabled,
                registerName,
                status.OpenSession,
                status.TodaySales,
                status.TodayTotal,
                status.WeekSales,
                status.WeekTotal,
                taxRate,
                taxLabel,
                CurrencyFor(country),
                country,
                warehouses,
                warehouseId,
                warehouseName);
        }
        catch (DbException)
        {
            return CpPosTerminalContext.Unavailable;
        }
    }

    public async Task<CpPosSessionStatus> SessionStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new CpPosSessionStatus(null, 0, 0m, 0, 0m);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await SessionStatusCoreAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return new CpPosSessionStatus(null, 0, 0m, 0, 0m);
        }
    }

    private static async Task<CpPosSessionStatus> SessionStatusCoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        CpPosOpenSessionInfo? open = null;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`session_no`,''), IFNULL(`opening_float`,0) FROM `epc_pos_sessions` WHERE `status` = 'open' ORDER BY `opened_at` DESC LIMIT 1");
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                open = new CpPosOpenSessionInfo(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.GetString(1),
                    Convert.ToDecimal(r.GetValue(2), CultureInfo.InvariantCulture));
            }
        }

        var now = DateTimeOffset.UtcNow;
        var today = new DateTimeOffset(now.Date, TimeSpan.Zero).ToUnixTimeSeconds();
        var week = now.AddDays(-7).ToUnixTimeSeconds();
        var (todayCount, todayTotal) = await CountSinceAsync(connection, today, cancellationToken).ConfigureAwait(false);
        var (weekCount, weekTotal) = await CountSinceAsync(connection, week, cancellationToken).ConfigureAwait(false);
        return new CpPosSessionStatus(open, todayCount, todayTotal, weekCount, weekTotal);
    }

    private static async Task<(int Count, decimal Total)> CountSinceAsync(DbConnection connection, long since, CancellationToken cancellationToken)
    {
        try
        {
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT COUNT(*), COALESCE(SUM(`total_amount`),0) FROM `epc_pos_sales` WHERE `time_created` >= ? AND `status` = 'completed'");
            ErpDb.AddParameters(c, since);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (
                    Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture),
                    Math.Round(Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture), 2));
            }
        }
        catch (DbException)
        {
        }

        return (0, 0m);
    }
}
