using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_workshop_endpoint.php</c> twins for assign / save_bay / save_tech / set_status /
/// create_job / add_line / create_appointment / convert_appointment.
/// Seed and schema-ensure stay PHP.
/// </summary>
public interface ICpWorkshopWriteService
{
    Task<ErpSimpleWriteResult> AssignAsync(long jobId, long bayId, long techId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveBayAsync(long id, string? code, string? name, int active, int sortOrder, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTechAsync(long id, string? name, string? phone, string? skill, int active, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetStatusAsync(long jobId, string? status, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateJobAsync(CpWorkshopCreateJobRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddLineAsync(CpWorkshopAddLineRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateAppointmentAsync(CpWorkshopCreateAppointmentRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ConvertAppointmentAsync(long appointmentId, CancellationToken cancellationToken = default);
}

public sealed record CpWorkshopCreateJobRequest(
    string? JobNo = null,
    string? Status = null,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? CustomerEmail = null,
    long CustomerId = 0,
    string? Plate = null,
    string? Vin = null,
    string? Make = null,
    string? Model = null,
    string? Year = null,
    int Odometer = 0,
    string? Complaint = null,
    long BayId = 0,
    long TechId = 0,
    bool EstimateApproved = false,
    bool UnderWarranty = false,
    string? Notes = null,
    long TimePromised = 0,
    string? LabourDesc = null,
    decimal LabourHours = 1,
    decimal LabourRate = 150,
    string? PartDesc = null,
    decimal PartQty = 1,
    decimal PartPrice = 0);

public sealed record CpWorkshopAddLineRequest(
    long JobId = 0,
    string? LineType = null,
    string? Description = null,
    long ItemId = 0,
    decimal Qty = 1,
    decimal UnitPrice = 0,
    decimal TaxPercent = 5,
    int? Chargeable = 1);

public sealed record CpWorkshopCreateAppointmentRequest(
    string? RefNo = null,
    string? Status = null,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? CustomerEmail = null,
    long CustomerId = 0,
    long GarageId = 0,
    string? Plate = null,
    string? Make = null,
    string? Model = null,
    string? Year = null,
    string? ServiceType = null,
    string? Notes = null,
    long TimeSlot = 0);

public readonly record struct CpWorkshopLineTotals(decimal PartsTotal, decimal LabourTotal, decimal TaxTotal, decimal GrandTotal);

public sealed class CpWorkshopWriteService : ICpWorkshopWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpWorkshopWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AssignAsync(
        long jobId,
        long bayId,
        long techId,
        CancellationToken cancellationToken = default)
    {
        if (jobId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A job id is required.");
        }

        if (bayId < 0 || techId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bay and technician ids cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_ws_jobs` SET `bay_id` = ?, `tech_id` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            bayId, techId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), jobId);
        return ErpSimpleWriteResult.Ok("Assignment saved.", jobId);
    }

    public async Task<ErpSimpleWriteResult> SaveBayAsync(
        long id,
        string? code,
        string? name,
        int active,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        var bayCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var bayName = (name ?? string.Empty).Trim();
        if (bayCode.Length == 0 || bayName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bay code and name required.");
        }

        if (bayCode.Length > 32)
        {
            bayCode = bayCode[..32];
        }

        if (bayName.Length > 190)
        {
            bayName = bayName[..190];
        }

        if (active is not (0 or 1) || sortOrder < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bay active must be 0 or 1.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_bays` SET `code` = ?, `name` = ?, `active` = ?, `sort_order` = ? WHERE `id` = ?"),
                cancellationToken,
                bayCode, bayName, active, sortOrder, id);
            return ErpSimpleWriteResult.Ok("Bay saved.", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_ws_bays` (`code`,`name`,`active`,`sort_order`) VALUES (?,?,?,?)"),
            cancellationToken,
            bayCode, bayName, 1, sortOrder);
        var newId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Bay saved.", newId);
    }

    public async Task<ErpSimpleWriteResult> SaveTechAsync(
        long id,
        string? name,
        string? phone,
        string? skill,
        int active,
        CancellationToken cancellationToken = default)
    {
        var techName = (name ?? string.Empty).Trim();
        if (techName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Technician name required.");
        }

        if (techName.Length > 190)
        {
            techName = techName[..190];
        }

        if (id > 0 && active is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Technician active must be 0 or 1.");
        }

        var phoneText = (phone ?? string.Empty).Trim();
        var skillText = (skill ?? string.Empty).Trim();
        if (phoneText.Length > 64)
        {
            phoneText = phoneText[..64];
        }

        if (skillText.Length > 190)
        {
            skillText = skillText[..190];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_technicians` SET `name` = ?, `phone` = ?, `skill` = ?, `active` = ? WHERE `id` = ?"),
                cancellationToken,
                techName, phoneText, skillText, active, id);
            return ErpSimpleWriteResult.Ok("Technician saved.", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_ws_technicians` (`name`,`phone`,`skill`,`active`) VALUES (?,?,?,1)"),
            cancellationToken,
            techName, phoneText, skillText);
        var newId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Technician saved.", newId);
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(long jobId, string? status, CancellationToken cancellationToken = default)
    {
        var key = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (jobId <= 0 || !Statuses.Contains(key))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A job id and a known workshop status are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var approve = key is "approved" or "in_progress" or "qc" or "ready" or "delivered";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (approve)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_jobs` SET `status` = ?, `estimate_approved` = 1, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                key, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), jobId);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_jobs` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                key, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), jobId);
        }

        return ErpSimpleWriteResult.Ok("Status updated.", jobId);
    }

    public async Task<ErpSimpleWriteResult> CreateJobAsync(
        CpWorkshopCreateJobRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var jobId = await InsertJobAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(request.LabourDesc))
        {
            await InsertLineAsync(
                connection,
                new CpWorkshopAddLineRequest(
                    jobId,
                    "labour",
                    request.LabourDesc,
                    0,
                    request.LabourHours <= 0 ? 1 : request.LabourHours,
                    request.LabourRate,
                    5,
                    1),
                cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(request.PartDesc))
        {
            await InsertLineAsync(
                connection,
                new CpWorkshopAddLineRequest(
                    jobId,
                    "part",
                    request.PartDesc,
                    0,
                    request.PartQty <= 0 ? 1 : request.PartQty,
                    request.PartPrice,
                    5,
                    1),
                cancellationToken).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Job created", jobId);
    }

    public async Task<ErpSimpleWriteResult> AddLineAsync(
        CpWorkshopAddLineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.JobId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid job");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_ws_jobs` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.JobId).ConfigureAwait(false);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid job");
        }

        var lineId = await InsertLineAsync(connection, request, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Line added", lineId);
    }

    public async Task<ErpSimpleWriteResult> CreateAppointmentAsync(
        CpWorkshopCreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var refNo = Clip(request.RefNo, 40);
        if (refNo.Length == 0)
        {
            refNo = await NextAppointmentRefAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var status = NormalizeAppointmentStatus(request.Status);
        var slot = request.TimeSlot > 0 ? request.TimeSlot : now + 86400;
        var service = Clip(request.ServiceType, 80);
        if (service.Length == 0)
        {
            service = "General service";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_ws_appointments`
                (`ref_no`,`status`,`customer_name`,`customer_phone`,`customer_email`,`customer_id`,`garage_id`,
                 `plate`,`make`,`model`,`year`,`service_type`,`notes`,`time_slot`,`job_id`,`time_created`,`time_updated`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?,?)
                """),
            cancellationToken,
            refNo,
            status,
            Clip(request.CustomerName, 160),
            Clip(request.CustomerPhone, 40),
            Clip(request.CustomerEmail, 160),
            Math.Max(0, request.CustomerId),
            Math.Max(0, request.GarageId),
            Clip(request.Plate, 40).ToUpperInvariant(),
            Clip(request.Make, 80),
            Clip(request.Model, 80),
            Clip(request.Year, 10),
            service,
            request.Notes ?? string.Empty,
            slot,
            now,
            now);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Appointment scheduled", id);
    }

    public async Task<ErpSimpleWriteResult> ConvertAppointmentAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        if (appointmentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Appointment not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var existingJob = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `job_id` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            appointmentId).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            appointmentId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Appointment not found");
        }

        if (existingJob > 0)
        {
            return ErpSimpleWriteResult.Ok("Checked in", existingJob);
        }

        var name = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `customer_name` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var phone = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `customer_phone` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var email = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `customer_email` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var customerId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `customer_id` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var plate = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `plate` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var make = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `make` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var model = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `model` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var year = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `year` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var service = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `service_type` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var notes = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `notes` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var garageId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `garage_id` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var refNo = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `ref_no` FROM `epc_ws_appointments` WHERE `id`=? LIMIT 1"), cancellationToken, appointmentId).ConfigureAwait(false);
        var complaint = ((service ?? string.Empty).Trim() + " — " + (notes ?? string.Empty)).Trim();
        var jobId = await InsertJobAsync(
            connection,
            new CpWorkshopCreateJobRequest(
                Status: "checkin",
                CustomerName: name,
                CustomerPhone: phone,
                CustomerEmail: email,
                CustomerId: customerId,
                Plate: plate,
                Make: make,
                Model: model,
                Year: year,
                Complaint: complaint,
                Notes: "From appointment " + (refNo ?? string.Empty).Trim()),
            cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_jobs` SET `garage_id`=?, `appointment_id`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                garageId, appointmentId, now, jobId);
        }
        catch (System.Data.Common.DbException)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_ws_jobs` SET `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                now, jobId);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_ws_appointments` SET `status`='converted', `job_id`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            jobId, now, appointmentId);
        return ErpSimpleWriteResult.Ok("Checked in", jobId);
    }

    public static string NormalizeJobStatus(string? status)
    {
        var key = (status ?? string.Empty).Trim().ToLowerInvariant();
        return Statuses.Contains(key) ? key : "checkin";
    }

    public static string NormalizeAppointmentStatus(string? status)
    {
        var key = (status ?? string.Empty).Trim().ToLowerInvariant();
        return AppointmentStatuses.Contains(key) ? key : "scheduled";
    }

    public static string NormalizeLineType(string? lineType)
        => string.Equals((lineType ?? string.Empty).Trim(), "labour", StringComparison.OrdinalIgnoreCase)
            ? "labour"
            : "part";

    /// <summary>PHP <c>sprintf('WS-%s-%03d', date('ymd'), $n)</c>.</summary>
    public static string FormatJobNo(string ymd, int sequence)
        => "WS-" + ymd + "-" + sequence.ToString("000", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>sprintf('AP-%s-%03d', date('ymd'), $n)</c>.</summary>
    public static string FormatAppointmentRef(string ymd, int sequence)
        => "AP-" + ymd + "-" + sequence.ToString("000", CultureInfo.InvariantCulture);

    public static CpWorkshopLineTotals RecalcTotals(IEnumerable<(string LineType, decimal Qty, decimal UnitPrice, decimal TaxPercent, int Chargeable)> lines)
    {
        decimal parts = 0;
        decimal labour = 0;
        decimal tax = 0;
        foreach (var ln in lines)
        {
            if (ln.Chargeable != 1)
            {
                continue;
            }

            var net = ln.Qty * ln.UnitPrice;
            tax += net * (ln.TaxPercent / 100m);
            if (NormalizeLineType(ln.LineType) == "labour")
            {
                labour += net;
            }
            else
            {
                parts += net;
            }
        }

        parts = Math.Round(parts, 2, MidpointRounding.AwayFromZero);
        labour = Math.Round(labour, 2, MidpointRounding.AwayFromZero);
        tax = Math.Round(tax, 2, MidpointRounding.AwayFromZero);
        return new(parts, labour, tax, Math.Round(parts + labour + tax, 2, MidpointRounding.AwayFromZero));
    }

    private async Task<long> InsertJobAsync(
        System.Data.Common.DbConnection connection,
        CpWorkshopCreateJobRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jobNo = Clip(request.JobNo, 40);
        if (jobNo.Length == 0)
        {
            jobNo = await NextJobNoAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var status = NormalizeJobStatus(request.Status);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_ws_jobs`
                (`job_no`,`status`,`customer_name`,`customer_phone`,`customer_email`,`customer_id`,
                 `plate`,`vin`,`make`,`model`,`year`,`odometer`,`complaint`,`bay_id`,`tech_id`,
                 `estimate_approved`,`under_warranty`,`notes`,`time_promised`,`time_created`,`time_updated`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            jobNo,
            status,
            Clip(request.CustomerName, 160),
            Clip(request.CustomerPhone, 40),
            Clip(request.CustomerEmail, 160),
            Math.Max(0, request.CustomerId),
            Clip(request.Plate, 40).ToUpperInvariant(),
            Clip(request.Vin, 40).ToUpperInvariant(),
            Clip(request.Make, 80),
            Clip(request.Model, 80),
            Clip(request.Year, 10),
            Math.Max(0, request.Odometer),
            request.Complaint ?? string.Empty,
            Math.Max(0, request.BayId),
            Math.Max(0, request.TechId),
            request.EstimateApproved ? 1 : 0,
            request.UnderWarranty ? 1 : 0,
            request.Notes ?? string.Empty,
            Math.Max(0, request.TimePromised),
            now,
            now);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> InsertLineAsync(
        System.Data.Common.DbConnection connection,
        CpWorkshopAddLineRequest request,
        CancellationToken cancellationToken)
    {
        var qty = request.Qty;
        var tax = request.TaxPercent;
        var chargeable = request.Chargeable ?? 1;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_ws_job_lines`
                (`job_id`,`line_type`,`description`,`item_id`,`qty`,`unit_price`,`tax_percent`,`chargeable`)
                VALUES (?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            request.JobId,
            NormalizeLineType(request.LineType),
            Clip(request.Description, 190),
            Math.Max(0, request.ItemId),
            qty,
            request.UnitPrice,
            tax,
            chargeable != 0 ? 1 : 0);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await RecalcJobAsync(connection, request.JobId, cancellationToken).ConfigureAwait(false);
        return id;
    }

    private static async Task RecalcJobAsync(
        System.Data.Common.DbConnection connection,
        long jobId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `line_type`,`qty`,`unit_price`,`tax_percent`,`chargeable` FROM `epc_ws_job_lines` WHERE `job_id`=?");
        ErpDb.AddParameters(command, jobId);
        var rows = new List<(string LineType, decimal Qty, decimal UnitPrice, decimal TaxPercent, int Chargeable)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    reader.IsDBNull(0) ? "part" : reader.GetString(0),
                    reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? 5 : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.IsDBNull(4) ? 1 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture)));
            }
        }

        var totals = RecalcTotals(rows);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_ws_jobs` SET `parts_total`=?, `labour_total`=?, `tax_total`=?, `grand_total`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            totals.PartsTotal, totals.LabourTotal, totals.TaxTotal, totals.GrandTotal, now, jobId);
    }

    private static async Task<string> NextJobNoAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var day = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `job_no` LIKE ?"),
            cancellationToken,
            "WS-" + day + "-%").ConfigureAwait(false);
        return FormatJobNo(day, (int)count + 1);
    }

    private static async Task<string> NextAppointmentRefAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var day = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_ws_appointments` WHERE `ref_no` LIKE ?"),
            cancellationToken,
            "AP-" + day + "-%").ConfigureAwait(false);
        return FormatAppointmentRef(day, (int)count + 1);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "checkin", "estimate", "approved", "in_progress", "qc", "ready", "delivered", "cancelled"
    };

    public static readonly HashSet<string> AppointmentStatuses = new(StringComparer.Ordinal)
    {
        "scheduled", "confirmed", "arrived", "converted", "no_show", "cancelled"
    };
}
