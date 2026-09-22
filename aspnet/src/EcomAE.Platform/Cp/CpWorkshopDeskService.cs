using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Read side of the PHP garage desk (<c>epc_workshop_helpers.php</c>: statuses, dashboard, list_jobs, job_get,
/// list_bays, list_techs, list_appointments, seed_demo) that <c>workshop_main_page.php</c> and
/// <c>ajax_workshop_endpoint.php</c> use.
/// </summary>
public interface ICpWorkshopDeskService
{
    Task<CpWorkshopDesk> LoadAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpWorkshopJobRow>> ListJobsAsync(string? status, int limit = 200, CancellationToken cancellationToken = default);

    Task<CpWorkshopDashboard> DashboardAsync(CancellationToken cancellationToken = default);

    Task<CpWorkshopJobCard?> GetJobAsync(long jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpWorkshopBay>> ListBaysAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpWorkshopTech>> ListTechsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpWorkshopAppointmentRow>> ListAppointmentsAsync(int limit = 60, CancellationToken cancellationToken = default);

    Task<CpWorkshopSeedResult> SeedDemoAsync(CancellationToken cancellationToken = default);
}

public sealed record CpWorkshopDashboard(int Open, int InProgress, int Ready, int DeliveredToday, decimal RevenueOpen);

public sealed record CpWorkshopBay(long Id, string Code, string Name, bool Active, int SortOrder);

public sealed record CpWorkshopTech(long Id, string Name, string Phone, string Skill, bool Active);

public sealed record CpWorkshopJobRow(
    long Id,
    string JobNo,
    string Status,
    string CustomerName,
    string CustomerPhone,
    string Plate,
    string Make,
    string Model,
    string Year,
    long BayId,
    long TechId,
    string BayName,
    string TechName,
    decimal GrandTotal);

public sealed record CpWorkshopJobLine(long Id, string LineType, string Description, decimal Qty, decimal UnitPrice, decimal TaxPercent, decimal LineTotal);

/// <summary>PHP <c>epc_ws_job_get</c> shape: <c>{header, lines}</c>.</summary>
public sealed record CpWorkshopJobCard(IReadOnlyDictionary<string, object?> Header, IReadOnlyList<IReadOnlyDictionary<string, object?>> Lines)
{
    public string JobNo => Header.TryGetValue("job_no", out var v) ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";
}

public sealed record CpWorkshopAppointmentRow(
    long Id,
    string RefNo,
    long TimeSlot,
    string CustomerName,
    string CustomerPhone,
    string Plate,
    string Make,
    string Model,
    string ServiceType,
    string Status,
    long JobId);

public sealed record CpWorkshopSeedResult(int Bays, int Techs, int Jobs);

public sealed record CpWorkshopDesk(
    bool DbAvailable,
    string LoadError,
    CpWorkshopDashboard Dashboard,
    IReadOnlyList<CpWorkshopJobRow> Jobs,
    IReadOnlyList<CpWorkshopBay> Bays,
    IReadOnlyList<CpWorkshopTech> Techs,
    IReadOnlyList<CpWorkshopAppointmentRow> Appointments)
{
    public static CpWorkshopDesk Unavailable(string error) =>
        new(false, error, new CpWorkshopDashboard(0, 0, 0, 0, 0m), [], [], [], []);
}

public sealed class CpWorkshopDeskService : ICpWorkshopDeskService
{
    /// <summary>PHP <c>epc_ws_statuses()</c>, in order.</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Statuses =
    [
        new("checkin", "Check-in"),
        new("estimate", "Estimate"),
        new("approved", "Approved"),
        new("in_progress", "In progress"),
        new("qc", "QC / test"),
        new("ready", "Ready"),
        new("delivered", "Delivered"),
        new("cancelled", "Cancelled"),
    ];

    public static readonly IReadOnlyList<string> BoardColumns = ["checkin", "estimate", "approved", "in_progress", "qc", "ready"];

    public static string StatusLabel(string status)
    {
        foreach (var kv in Statuses)
        {
            if (kv.Key == status)
            {
                return kv.Value;
            }
        }

        return status;
    }

    public static bool IsStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return false;
        }

        foreach (var kv in Statuses)
        {
            if (kv.Key == status)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>PHP jobs-tab pill class.</summary>
    public static string PillClass(string status) => status switch
    {
        "ready" => "epc-ws-pill is-ready",
        "in_progress" or "qc" => "epc-ws-pill is-progress",
        "delivered" or "cancelled" => "epc-ws-pill is-done",
        _ => "epc-ws-pill",
    };

    /// <summary>PHP <c>epc_ws_dashboard</c> aggregation over per-status (count, total) rows.</summary>
    public static CpWorkshopDashboard Aggregate(IEnumerable<(string Status, int Count, decimal Total)> rows, int deliveredToday)
    {
        var open = 0;
        var progress = 0;
        var ready = 0;
        var revenue = 0m;
        foreach (var (status, count, total) in rows)
        {
            if (status is not ("delivered" or "cancelled"))
            {
                open += count;
                revenue += total;
            }

            if (status is "in_progress" or "qc")
            {
                progress += count;
            }

            if (status == "ready")
            {
                ready += count;
            }
        }

        return new CpWorkshopDashboard(open, progress, ready, deliveredToday, Math.Round(revenue, 2, MidpointRounding.AwayFromZero));
    }

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpWorkshopWriteService _writes;

    public CpWorkshopDeskService(IErpWriteConnectionFactory connections, ICpWorkshopWriteService writes)
    {
        _connections = connections;
        _writes = writes;
    }

    public async Task<CpWorkshopDesk> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpWorkshopDesk.Unavailable("No database");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var count = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_ws_jobs`", cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                await _writes.SeedDemoAsync(cancellationToken).ConfigureAwait(false);
            }

            return new CpWorkshopDesk(
                true,
                "",
                await DashboardCoreAsync(connection, cancellationToken).ConfigureAwait(false),
                await ListJobsCoreAsync(connection, null, 200, cancellationToken).ConfigureAwait(false),
                await ListBaysCoreAsync(connection, cancellationToken).ConfigureAwait(false),
                await ListTechsCoreAsync(connection, cancellationToken).ConfigureAwait(false),
                await ListAppointmentsCoreAsync(connection, 60, cancellationToken).ConfigureAwait(false));
        }
        catch (DbException ex)
        {
            return CpWorkshopDesk.Unavailable(ex.Message);
        }
    }

    public async Task<IReadOnlyList<CpWorkshopJobRow>> ListJobsAsync(string? status, int limit = 200, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ListJobsCoreAsync(connection, status, limit, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CpWorkshopDashboard> DashboardAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await DashboardCoreAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CpWorkshopJobCard?> GetJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        if (jobId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, object?>? header = null;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional(
                "SELECT j.*, b.name AS bay_name, b.code AS bay_code, t.name AS tech_name FROM `epc_ws_jobs` j LEFT JOIN `epc_ws_bays` b ON b.id = j.bay_id LEFT JOIN `epc_ws_technicians` t ON t.id = j.tech_id WHERE j.id = ? LIMIT 1");
            ErpDb.AddParameters(c, jobId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                header = RowToMap(r);
            }
        }

        if (header is null)
        {
            return null;
        }

        var lines = new List<IReadOnlyDictionary<string, object?>>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT * FROM `epc_ws_job_lines` WHERE `job_id` = ? ORDER BY `id` ASC");
            ErpDb.AddParameters(c, jobId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(RowToMap(r));
            }
        }

        return new CpWorkshopJobCard(header, lines);
    }

    public async Task<IReadOnlyList<CpWorkshopBay>> ListBaysAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ListBaysCoreAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CpWorkshopTech>> ListTechsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ListTechsCoreAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CpWorkshopAppointmentRow>> ListAppointmentsAsync(int limit = 60, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ListAppointmentsCoreAsync(connection, limit, cancellationToken).ConfigureAwait(false);
    }

    public Task<CpWorkshopSeedResult> SeedDemoAsync(CancellationToken cancellationToken = default) => _writes.SeedDemoAsync(cancellationToken);

    private static async Task<CpWorkshopDashboard> DashboardCoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<(string, int, decimal)>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `status`, COUNT(*) c, IFNULL(SUM(`grand_total`),0) t FROM `epc_ws_jobs` GROUP BY `status`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    r.IsDBNull(0) ? "" : r.GetString(0),
                    Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(r.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }

        var dayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();
        var delivered = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `status`='delivered' AND `time_updated` >= ?"),
            cancellationToken,
            dayStart).ConfigureAwait(false);
        return Aggregate(rows, (int)delivered);
    }

    private static async Task<IReadOnlyList<CpWorkshopJobRow>> ListJobsCoreAsync(DbConnection connection, string? status, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        var list = new List<CpWorkshopJobRow>();
        await using var c = connection.CreateCommand();
        var sql = "SELECT j.id, IFNULL(j.job_no,''), IFNULL(j.status,''), IFNULL(j.customer_name,''), IFNULL(j.customer_phone,''), IFNULL(j.plate,''), IFNULL(j.make,''), IFNULL(j.model,''), IFNULL(j.year,''), IFNULL(j.bay_id,0), IFNULL(j.tech_id,0), IFNULL(b.name,''), IFNULL(t.name,''), IFNULL(j.grand_total,0) FROM `epc_ws_jobs` j LEFT JOIN `epc_ws_bays` b ON b.id = j.bay_id LEFT JOIN `epc_ws_technicians` t ON t.id = j.tech_id";
        if (IsStatus(status))
        {
            sql += " WHERE j.status = ?";
            ErpDb.AddParameters(c, status);
        }

        sql += " ORDER BY FIELD(j.status,'checkin','estimate','approved','in_progress','qc','ready','delivered','cancelled'), j.id DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        c.CommandText = ErpDb.Positional(sql);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpWorkshopJobRow(
                Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8),
                Convert.ToInt64(r.GetValue(9), CultureInfo.InvariantCulture),
                Convert.ToInt64(r.GetValue(10), CultureInfo.InvariantCulture),
                r.GetString(11), r.GetString(12),
                Convert.ToDecimal(r.GetValue(13), CultureInfo.InvariantCulture)));
        }

        return list;
    }

    private static async Task<IReadOnlyList<CpWorkshopBay>> ListBaysCoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<CpWorkshopBay>();
        await using var c = connection.CreateCommand();
        c.CommandText = "SELECT `id`, IFNULL(`code`,''), IFNULL(`name`,''), IFNULL(`active`,1), IFNULL(`sort_order`,0) FROM `epc_ws_bays` ORDER BY `sort_order` ASC, `id` ASC";
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpWorkshopBay(
                Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                r.GetString(1), r.GetString(2),
                Convert.ToInt32(r.GetValue(3), CultureInfo.InvariantCulture) != 0,
                Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture)));
        }

        return list;
    }

    private static async Task<IReadOnlyList<CpWorkshopTech>> ListTechsCoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<CpWorkshopTech>();
        await using var c = connection.CreateCommand();
        c.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`phone`,''), IFNULL(`skill`,''), IFNULL(`active`,1) FROM `epc_ws_technicians` ORDER BY `name` ASC";
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new CpWorkshopTech(
                Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                r.GetString(1), r.GetString(2), r.GetString(3),
                Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture) != 0));
        }

        return list;
    }

    private static async Task<IReadOnlyList<CpWorkshopAppointmentRow>> ListAppointmentsCoreAsync(DbConnection connection, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 300);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var list = new List<CpWorkshopAppointmentRow>();
        try
        {
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`ref_no`,''), IFNULL(`time_slot`,0), IFNULL(`customer_name`,''), IFNULL(`customer_phone`,''), IFNULL(`plate`,''), IFNULL(`make`,''), IFNULL(`model`,''), IFNULL(`service_type`,''), IFNULL(`status`,''), IFNULL(`job_id`,0) FROM `epc_ws_appointments` WHERE `time_slot` >= ? AND `time_slot` <= ? ORDER BY `time_slot` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(c, now - 86400, now + 21 * 86400);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new CpWorkshopAppointmentRow(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.GetString(1),
                    Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture),
                    r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetString(9),
                    Convert.ToInt64(r.GetValue(10), CultureInfo.InvariantCulture)));
            }
        }
        catch (DbException)
        {
            list.Clear();
        }

        return list;
    }

    private static Dictionary<string, object?> RowToMap(DbDataReader r)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < r.FieldCount; i++)
        {
            var v = r.IsDBNull(i) ? null : r.GetValue(i);
            map[r.GetName(i)] = v switch
            {
                byte[] bytes => Convert.ToBase64String(bytes),
                decimal d => d.ToString(CultureInfo.InvariantCulture),
                _ => v,
            };
        }

        return map;
    }
}
