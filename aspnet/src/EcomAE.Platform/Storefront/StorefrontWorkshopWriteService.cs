using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP <c>ajax_workshop_public.php</c> book + public track.
/// Job INSERT reuses <see cref="ICpWorkshopWriteService"/>. Schema-ensure stays Classic.
/// Does not invent a send.
/// </summary>
public interface IStorefrontWorkshopWriteService
{
    Task<StorefrontWorkshopWriteResult> BookAsync(
        StorefrontWorkshopBookRequest request,
        CancellationToken cancellationToken = default);

    Task<StorefrontWorkshopWriteResult> BookAppointmentAsync(
        StorefrontWorkshopBookRequest request,
        CancellationToken cancellationToken = default);

    Task<StorefrontWorkshopTrackResult> TrackAsync(
        string? reference,
        string? phone,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontWorkshopBookRequest(
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string? Plate,
    string? Vin,
    string? Make,
    string? Model,
    string? Year,
    string? Complaint,
    int Odometer = 0);

public sealed record StorefrontWorkshopWriteResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    long Id,
    string JobNo,
    int Writes)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        status = Ok,
        surface = "storefront",
        status_token = Status,
        writes = Writes,
        writesBlocked = false,
        cutoverAllowed = false,
        phpAuthoritative = false,
        validation_code = Code,
        job_id = Id,
        job_no = JobNo,
        message = Message,
        note = Message,
        session
    };
}

public sealed record StorefrontWorkshopTrackDigest(
    string JobNo,
    string Status,
    string Plate,
    string Make,
    string Model,
    string CustomerName,
    decimal GrandTotal);

public sealed record StorefrontWorkshopTrackResult(
    StorefrontWorkshopTrackDigest? Job,
    string Source,
    string Message);

public sealed class StorefrontWorkshopWriteService : IStorefrontWorkshopWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpWorkshopWriteService _workshop;

    public StorefrontWorkshopWriteService(
        IErpWriteConnectionFactory connections,
        ICpWorkshopWriteService workshop)
    {
        _connections = connections;
        _workshop = workshop;
    }

    public static string Normalize(string? value)
        => (value ?? string.Empty).Trim();

    public static string DigitsOnly(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public async Task<StorefrontWorkshopWriteResult> BookAsync(
        StorefrontWorkshopBookRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = Normalize(request.CustomerName);
        var phone = Normalize(request.CustomerPhone);
        var plate = Normalize(request.Plate);
        var complaint = Normalize(request.Complaint);
        if (name.Length == 0 || phone.Length == 0 || plate.Length == 0 || complaint.Length == 0)
        {
            return Fail("invalid", "Name, phone, plate, and complaint are required.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Workshop database is not configured.");
        }

        try
        {
            var created = await _workshop.CreateJobAsync(
                new CpWorkshopCreateJobRequest(
                    Status: "checkin",
                    CustomerName: name,
                    CustomerPhone: phone,
                    CustomerEmail: Normalize(request.CustomerEmail),
                    Plate: plate,
                    Vin: Normalize(request.Vin),
                    Make: Normalize(request.Make),
                    Model: Normalize(request.Model),
                    Year: Normalize(request.Year),
                    Odometer: request.Odometer < 0 ? 0 : request.Odometer,
                    Complaint: complaint,
                    Notes: "Booked from storefront /auto-workshop"),
                cancellationToken).ConfigureAwait(false);
            if (!created.Succeeded || created.Id <= 0)
            {
                return Fail(created.Code, string.IsNullOrWhiteSpace(created.Message)
                    ? "Workshop tables are not provisioned. Schema ensure stays on the Classic twin."
                    : created.Message);
            }

            var jobNo = await ReadJobNoAsync(created.Id, cancellationToken).ConfigureAwait(false);
            var label = jobNo.Length > 0 ? jobNo : created.Id.ToString(CultureInfo.InvariantCulture);
            return new StorefrontWorkshopWriteResult(
                true,
                "ok",
                "ok",
                "Service request received. Keep job number " + label + " for tracking. This page does not invent a send.",
                created.Id,
                jobNo,
                created.Writes);
        }
        catch (Exception ex)
        {
            return Fail("db", ex.Message);
        }
    }

    public async Task<StorefrontWorkshopWriteResult> BookAppointmentAsync(
        StorefrontWorkshopBookRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = Normalize(request.CustomerName);
        var plate = Normalize(request.Plate);
        if (name.Length == 0 || plate.Length == 0)
        {
            return Fail("invalid", "Customer name and plate are required.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Workshop database is not configured.");
        }

        try
        {
            var created = await _workshop.CreateAppointmentAsync(
                new CpWorkshopCreateAppointmentRequest(
                    Status: "scheduled",
                    CustomerName: name,
                    CustomerPhone: Normalize(request.CustomerPhone),
                    CustomerEmail: Normalize(request.CustomerEmail),
                    Plate: plate,
                    Make: Normalize(request.Make),
                    Model: Normalize(request.Model),
                    Year: Normalize(request.Year),
                    ServiceType: Clip(Normalize(request.Complaint), 80),
                    Notes: Normalize(request.Complaint),
                    TimeSlot: DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400),
                cancellationToken).ConfigureAwait(false);
            if (!created.Succeeded || created.Id <= 0)
            {
                return Fail(created.Code, string.IsNullOrWhiteSpace(created.Message)
                    ? "Workshop tables are not provisioned. Schema ensure stays on the Classic twin."
                    : created.Message);
            }

            var refNo = await ReadAppointmentRefAsync(created.Id, cancellationToken).ConfigureAwait(false);
            var label = refNo.Length > 0 ? refNo : created.Id.ToString(CultureInfo.InvariantCulture);
            return new StorefrontWorkshopWriteResult(
                true,
                "ok",
                "ok",
                "Appointment booked. Keep reference " + label + " for tracking. This page does not invent a send.",
                created.Id,
                refNo,
                created.Writes);
        }
        catch (Exception ex)
        {
            return Fail("db", ex.Message);
        }
    }

    public async Task<StorefrontWorkshopTrackResult> TrackAsync(
        string? reference,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        var refNo = Normalize(reference);
        if (refNo.Length == 0)
        {
            return new(null, "rejected", "Enter a job number or plate.");
        }

        if (!_connections.IsConfigured)
        {
            return new(null, "migration", "Workshop database is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `job_no`, `status`, `plate`, `make`, `model`, `customer_name`, `grand_total`, `customer_phone`
                FROM `epc_ws_jobs`
                WHERE `job_no` = ? OR `plate` = ?
                ORDER BY `id` DESC LIMIT 1
                """);
            ErpDb.AddParameters(command, refNo, refNo.ToUpperInvariant());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var storedPhone = reader.IsDBNull(7) ? "" : reader.GetValue(7)?.ToString() ?? "";
                if (!PhoneTailMatches(storedPhone, phone))
                {
                    return new(null, "empty", "No job found for that reference.");
                }

                return new(
                    new StorefrontWorkshopTrackDigest(
                        ReadString(reader, 0),
                        ReadString(reader, 1),
                        ReadString(reader, 2),
                        ReadString(reader, 3),
                        ReadString(reader, 4),
                        ReadString(reader, 5),
                        reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture)),
                    "database",
                    string.Empty);
            }
        }
        catch (Exception ex)
        {
            return new(null, "database-error", ex.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `ref_no`, `status`, `plate`, `make`, `model`, `customer_name`, `customer_phone`
                FROM `epc_ws_appointments`
                WHERE `ref_no` = ? OR `plate` = ?
                ORDER BY `id` DESC LIMIT 1
                """);
            ErpDb.AddParameters(command, refNo, refNo.ToUpperInvariant());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var storedPhone = reader.IsDBNull(6) ? "" : reader.GetValue(6)?.ToString() ?? "";
                if (!PhoneTailMatches(storedPhone, phone))
                {
                    return new(null, "empty", "No job found for that reference.");
                }

                return new(
                    new StorefrontWorkshopTrackDigest(
                        ReadString(reader, 0),
                        "Appointment: " + ReadString(reader, 1),
                        ReadString(reader, 2),
                        ReadString(reader, 3),
                        ReadString(reader, 4),
                        ReadString(reader, 5),
                        0),
                    "database",
                    string.Empty);
            }
        }
        catch
        {
            // Appointment table is optional when schema-ensure stays Classic.
        }

        return new(null, "empty", "No job found for that reference.");
    }

    private async Task<string> ReadJobNoAsync(long jobId, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `job_no` FROM `epc_ws_jobs` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            jobId).ConfigureAwait(false) ?? string.Empty;
    }

    private async Task<string> ReadAppointmentRefAsync(long appointmentId, CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `ref_no` FROM `epc_ws_appointments` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            appointmentId).ConfigureAwait(false) ?? string.Empty;
    }

    public static bool PhoneTailMatches(string? stored, string? given)
    {
        var want = DigitsOnly(given);
        if (want.Length == 0)
        {
            return true;
        }

        var have = DigitsOnly(stored);
        if (have.Length == 0)
        {
            return true;
        }

        var a = have.Length <= 7 ? have : have[^7..];
        var b = want.Length <= 7 ? want : want[^7..];
        return string.Equals(a, b, StringComparison.Ordinal);
    }

    private static string ReadString(System.Data.Common.DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static StorefrontWorkshopWriteResult Fail(string code, string message)
        => new(false, "error", code, message, 0, string.Empty, 0);
}
