using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_docx_save</c> / ajax <c>docx_save</c> twin.
/// UPDATE <c>epc_erp_doc_expiry</c> when <c>id</c> &gt; 0, else INSERT.
/// File bytes, reminder dispatch, delete, and schema ensure stay PHP.
/// Attachment is a path string only. Does not CREATE tables.
/// </summary>
public interface IErpDocxSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpDocxSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpDocxSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Category = null,
    string? DocType = null,
    string? Title = null,
    string? RefNo = null,
    string? Owner = null,
    string? OwnerEmail = null,
    string? Issuer = null,
    string? IssueDateStr = null,
    string? ExpiryDateStr = null,
    long IssueDate = 0,
    long ExpiryDate = 0,
    string? ReminderDays = null,
    string? AttachmentPath = null,
    string? Note = null,
    string? SourceModule = null,
    long SourceRefId = 0,
    int? Active = null);

public sealed class ErpDocxSaveWriteService : IErpDocxSaveWriteService
{
    public static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "legal", "customer", "insurance", "banking", "other",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpDocxSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpDocxSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var docType = (request.DocType ?? string.Empty).Trim();
        if (docType.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Document type is required");
        }

        if (IsPhpEmpty(request.ExpiryDateStr) && request.ExpiryDate <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Expiry date is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var category = request.Category ?? "other";
        if (!Categories.Contains(category))
        {
            category = "other";
        }

        var reminder = (request.ReminderDays ?? "90,60,30,7").Trim();
        reminder = FormatReminderDays(reminder);

        var issueDate = !IsPhpEmpty(request.IssueDateStr)
            ? ParsePhpDate(request.IssueDateStr)
            : request.IssueDate;
        var expiryDate = !IsPhpEmpty(request.ExpiryDateStr)
            ? ParsePhpDate(request.ExpiryDateStr)
            : request.ExpiryDate;

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var title = request.Title ?? string.Empty;
        var refNo = request.RefNo ?? string.Empty;
        var owner = request.Owner ?? string.Empty;
        var ownerEmail = request.OwnerEmail ?? string.Empty;
        var issuer = request.Issuer ?? string.Empty;
        var attachmentPath = request.AttachmentPath ?? string.Empty;
        var note = request.Note ?? string.Empty;
        var sourceModule = request.SourceModule ?? string.Empty;
        var sourceRefId = request.SourceRefId < 0 ? 0 : request.SourceRefId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_doc_expiry", "doc_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Document expiry table is not provisioned");
        }

        if (request.Id > 0)
        {
            var active = request.Active is null
                ? 1
                : (request.Active.Value != 0 ? 1 : 0);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_doc_expiry` SET `category`=?, `doc_type`=?, `title`=?, `ref_no`=?, `owner`=?, `owner_email`=?, `issuer`=?, `issue_date`=?, `expiry_date`=?, `reminder_days`=?, `attachment_path`=?, `note`=?, `active`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                category,
                docType,
                title,
                refNo,
                owner,
                ownerEmail,
                issuer,
                issueDate,
                expiryDate,
                reminder,
                attachmentPath,
                note,
                active,
                now,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Document saved to register", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_doc_expiry` (`company_id`,`category`,`doc_type`,`title`,`ref_no`,`owner`,`owner_email`,`issuer`,`issue_date`,`expiry_date`,`reminder_days`,`attachment_path`,`note`,`source_module`,`source_ref_id`,`active`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,1,?,?)"),
            cancellationToken,
            companyId,
            category,
            docType,
            title,
            refNo,
            owner,
            ownerEmail,
            issuer,
            issueDate,
            expiryDate,
            reminder,
            attachmentPath,
            note,
            sourceModule,
            sourceRefId,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Document saved to register", id);
    }

    public static bool IsPhpEmpty(string? value) =>
        string.IsNullOrEmpty(value) || value == "0";

    public static long ParsePhpDate(string? value)
    {
        if (IsPhpEmpty(value))
        {
            return 0;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            return dto.ToUnixTimeSeconds();
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeSeconds();
        }

        return 0;
    }

    public static string FormatReminderDays(string csv)
    {
        var seen = new HashSet<int>();
        var list = new List<int>();
        foreach (var part in csv.Split(','))
        {
            if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n <= 0)
            {
                continue;
            }

            if (seen.Add(n))
            {
                list.Add(n);
            }
        }

        list.Sort((a, b) => b.CompareTo(a));
        return list.Count > 0 ? string.Join(",", list) : "90,60,30,7";
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
