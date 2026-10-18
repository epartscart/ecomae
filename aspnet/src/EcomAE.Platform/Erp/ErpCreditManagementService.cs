using System.Data.Common;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;

namespace EcomAE.Platform.Erp;

public interface IErpCreditManagementService
{
    Task<ErpCreditSnapshot> LoadAsync(long companyId, long customerId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveProfileAsync(ErpCreditProfileWrite request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveRulesAsync(long companyId, ErpCreditBlock.Rules rules, CancellationToken cancellationToken = default);

    Task<ErpCreditBlock.Decision> AssessAsync(
        long customerId,
        decimal invoiceAmount,
        int invoiceTermsDays,
        long excludeInvoiceId = 0,
        CancellationToken cancellationToken = default);

    Task EnsureAllowsInvoiceAsync(
        DbConnection connection,
        long customerId,
        decimal invoiceAmount,
        int invoiceTermsDays,
        long excludeInvoiceId = 0,
        CancellationToken cancellationToken = default);

    Task<ErpCreditApplicationSnapshot> LoadApplicationAsync(long customerId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveApplicationAsync(ErpCreditApplicationWrite request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SavePartnerAsync(long customerId, string? name, long retirePartnerId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveDocumentAsync(ErpCreditDocumentWrite request, Stream content, string? fileName, CancellationToken cancellationToken = default);

    Task<ErpCreditDocumentFile?> OpenDocumentAsync(long documentId, CancellationToken cancellationToken = default);
}

public sealed record ErpCreditProfileRow(
    long CustomerId,
    string Name,
    bool OnHold,
    decimal PermanentLimit,
    int PermanentTermsDays,
    decimal TemporaryLimit,
    int TemporaryTermsDays,
    bool Mandatory,
    long ReviewUntil,
    long TemporaryFrom,
    long TemporaryUntil);

public sealed record ErpCreditSnapshot(
    ErpCreditBlock.Rules Rules,
    ErpCreditBlock.Position? Position,
    ErpCreditBlock.Decision? Decision,
    IReadOnlyList<ErpCreditProfileRow> Customers);

public sealed record ErpCreditApplicationSnapshot(
    bool Filed,
    string Party,
    long ValidUntil,
    decimal RequestedLimit,
    int RequestedTerms,
    IReadOnlyList<ErpCreditPartnerRow> Partners,
    IReadOnlyList<ErpCreditDocumentRow> Documents,
    IReadOnlyList<string> Gaps);

public sealed record ErpCreditPartnerRow(long Id, string Name);

public sealed record ErpCreditDocumentRow(
    long Id,
    long PartnerId,
    string Kind,
    string Number,
    long ExpiryUnix,
    string FileName,
    bool Attached);

public sealed record ErpCreditApplicationWrite(
    long CustomerId,
    string? Party,
    string? ValidUntil,
    decimal RequestedLimit,
    int RequestedTerms);

public sealed record ErpCreditDocumentWrite(
    long CustomerId,
    long PartnerId,
    string? Kind,
    string? Number,
    string? Expiry);

public sealed record ErpCreditDocumentFile(Stream Content, string ContentType, string DownloadName) : IDisposable
{
    public void Dispose() => Content.Dispose();
}

public sealed record ErpCreditProfileWrite(
    long CustomerId,
    long CompanyId,
    string? Name,
    bool OnHold,
    bool Mandatory,
    decimal PermanentLimit,
    int PermanentTermsDays,
    string? ReviewDate,
    decimal TemporaryLimit,
    int TemporaryTermsDays,
    string? TemporaryFrom,
    string? TemporaryUntil);

public sealed class ErpCreditManagementService : IErpCreditManagementService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IWebHostEnvironment _env;

    public ErpCreditManagementService(IErpWriteConnectionFactory connections, IWebHostEnvironment env)
    {
        _connections = connections;
        _env = env;
    }

    public async Task<ErpCreditSnapshot> LoadAsync(long companyId, long customerId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpCreditSnapshot(ErpCreditBlock.Rules.Defaults, null, null, []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var rules = await RulesAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        var customers = await CustomersAsync(connection, cancellationToken).ConfigureAwait(false);
        if (customerId <= 0)
        {
            return new ErpCreditSnapshot(rules, null, null, customers);
        }

        var decision = await AssessAsync(connection, customerId, 0m, 0, 0, cancellationToken).ConfigureAwait(false);
        return new ErpCreditSnapshot(rules, null, decision, customers);
    }

    public async Task<ErpSimpleWriteResult> SaveProfileAsync(ErpCreditProfileWrite request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CustomerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A customer id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (request.PermanentLimit < 0m || request.TemporaryLimit < 0m || request.PermanentTermsDays < 0 || request.TemporaryTermsDays < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Credit limits and terms cannot be negative.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var limit = ErpCreditBlock.Round(request.PermanentLimit);
        var temporary = ErpCreditBlock.Round(request.TemporaryLimit);
        var name = Clip(request.Name, 255);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_credit_profiles` (`customer_id`,`customer_name`,`legal_entity_id`,`credit_limit`,`terms_days`,`on_hold`,`permanent_limit`,`permanent_terms_days`,`limit_review_date`,`temporary_limit`,`temporary_terms_days`,`temporary_from`,`temporary_until`,`mandatory_limit`,`time_created`,`time_updated`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"
                + " ON DUPLICATE KEY UPDATE `customer_name`=VALUES(`customer_name`), `legal_entity_id`=VALUES(`legal_entity_id`), `credit_limit`=VALUES(`credit_limit`), `terms_days`=VALUES(`terms_days`), `on_hold`=VALUES(`on_hold`), `permanent_limit`=VALUES(`permanent_limit`), `permanent_terms_days`=VALUES(`permanent_terms_days`), `limit_review_date`=VALUES(`limit_review_date`), `temporary_limit`=VALUES(`temporary_limit`), `temporary_terms_days`=VALUES(`temporary_terms_days`), `temporary_from`=VALUES(`temporary_from`), `temporary_until`=VALUES(`temporary_until`), `mandatory_limit`=VALUES(`mandatory_limit`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            request.CustomerId,
            name,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            limit,
            request.PermanentTermsDays,
            request.OnHold ? 1 : 0,
            limit,
            request.PermanentTermsDays,
            ErpCreditBlock.EndOfUtcDay(request.ReviewDate),
            temporary,
            request.TemporaryTermsDays,
            ErpCreditBlock.StartOfUtcDay(request.TemporaryFrom),
            ErpCreditBlock.EndOfUtcDay(request.TemporaryUntil),
            request.Mandatory ? 1 : 0,
            now,
            now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Credit limit saved for customer " + request.CustomerId.ToString(CultureInfo.InvariantCulture) + ".", request.CustomerId);
    }

    public async Task<ErpSimpleWriteResult> SaveRulesAsync(long companyId, ErpCreditBlock.Rules rules, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var company = companyId < 0 ? 0 : companyId;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_credit_block_rules` (`company_id`,`block_account_status`,`block_credit_limit`,`limit_percent`,`limit_tolerance`,`block_limit_expired`,`block_days_overdue`,`days_threshold`,`grace_days`,`block_overdue_amount`,`overdue_threshold`,`block_terms`,`block_documents`,`time_updated`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)"
                + " ON DUPLICATE KEY UPDATE `block_account_status`=VALUES(`block_account_status`), `block_credit_limit`=VALUES(`block_credit_limit`), `limit_percent`=VALUES(`limit_percent`), `limit_tolerance`=VALUES(`limit_tolerance`), `block_limit_expired`=VALUES(`block_limit_expired`), `block_days_overdue`=VALUES(`block_days_overdue`), `days_threshold`=VALUES(`days_threshold`), `grace_days`=VALUES(`grace_days`), `block_overdue_amount`=VALUES(`block_overdue_amount`), `overdue_threshold`=VALUES(`overdue_threshold`), `block_terms`=VALUES(`block_terms`), `block_documents`=VALUES(`block_documents`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            company,
            rules.AccountStatus ? 1 : 0,
            rules.CreditLimit ? 1 : 0,
            ErpCreditBlock.Round(rules.LimitPercent <= 0m ? 100m : rules.LimitPercent),
            ErpCreditBlock.Round(rules.Tolerance),
            rules.LimitExpired ? 1 : 0,
            rules.DaysOverdue ? 1 : 0,
            rules.DaysThreshold < 0 ? 0 : rules.DaysThreshold,
            rules.GraceDays < 0 ? 0 : rules.GraceDays,
            rules.OverdueAmount ? 1 : 0,
            ErpCreditBlock.Round(rules.OverdueThreshold),
            rules.TermsOfPayment ? 1 : 0,
            rules.Documents ? 1 : 0,
            now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Blocking rules saved.", company);
    }

    public async Task<ErpCreditBlock.Decision> AssessAsync(
        long customerId,
        decimal invoiceAmount,
        int invoiceTermsDays,
        long excludeInvoiceId = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || customerId <= 0)
        {
            return new ErpCreditBlock.Decision(true, false, 0m, 0, invoiceAmount, []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        return await AssessAsync(connection, customerId, invoiceAmount, invoiceTermsDays, excludeInvoiceId, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureAllowsInvoiceAsync(
        DbConnection connection,
        long customerId,
        decimal invoiceAmount,
        int invoiceTermsDays,
        long excludeInvoiceId = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (customerId <= 0 || invoiceAmount <= 0m)
        {
            return;
        }

        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var decision = await AssessAsync(connection, customerId, invoiceAmount, invoiceTermsDays, excludeInvoiceId, cancellationToken).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            throw new ErpWriteException(decision.Message);
        }
    }

    private async Task<ErpCreditBlock.Decision> AssessAsync(
        DbConnection connection,
        long customerId,
        decimal invoiceAmount,
        int invoiceTermsDays,
        long excludeInvoiceId,
        CancellationToken cancellationToken)
    {
        var profile = await ProfileAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return new ErpCreditBlock.Decision(true, false, 0m, invoiceTermsDays, invoiceAmount, []);
        }

        var rules = await RulesAsync(connection, profile.CompanyId, cancellationToken).ConfigureAwait(false);
        var asOf = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var probe = new ErpCreditBlock.Position(
            profile.OnHold,
            profile.Mandatory,
            profile.PermanentLimit,
            profile.TermsDays,
            profile.ReviewUntil,
            profile.TemporaryLimit,
            profile.TemporaryTermsDays,
            profile.TemporaryFrom,
            profile.TemporaryUntil,
            0m,
            0m,
            0,
            0m,
            0);
        var termsForDue = ErpCreditBlock.TemporaryActive(probe, asOf) && profile.TemporaryTermsDays > 0
            ? profile.TemporaryTermsDays
            : profile.TermsDays;
        var open = await OpenInvoicesAsync(connection, customerId, excludeInvoiceId, termsForDue, cancellationToken).ConfigureAwait(false);
        var overdueAmount = 0m;
        var oldest = 0;
        foreach (var invoice in open)
        {
            var days = ErpCreditBlock.OverdueDays(invoice.Due, asOf, profile.TermsDays);
            if (days <= 0)
            {
                continue;
            }

            overdueAmount += invoice.Outstanding;
            if (days > oldest)
            {
                oldest = days;
            }
        }

        var position = new ErpCreditBlock.Position(
            profile.OnHold,
            profile.Mandatory,
            profile.PermanentLimit,
            profile.TermsDays,
            profile.ReviewUntil,
            profile.TemporaryLimit,
            profile.TemporaryTermsDays,
            profile.TemporaryFrom,
            profile.TemporaryUntil,
            ErpCreditBlock.Round(open.Sum(invoice => invoice.Outstanding)),
            ErpCreditBlock.Round(overdueAmount),
            oldest,
            ErpCreditBlock.Round(invoiceAmount),
            invoiceTermsDays);
        var credit = ErpCreditBlock.Evaluate(position, rules, asOf);
        var application = await ApplicationFileAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        return ErpCreditApplication.Apply(credit, rules.Documents, application, asOf);
    }

    public async Task<ErpCreditApplicationSnapshot> LoadApplicationAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var empty = new ErpCreditApplicationSnapshot(false, ErpCreditApplication.Company, 0, 0m, 0, [], [], ErpCreditApplication.Gaps(null, 0));
        if (!_connections.IsConfigured || customerId <= 0)
        {
            return empty;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var file = await ApplicationFileAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        return file is null
            ? empty with { Gaps = ErpCreditApplication.Gaps(null, DateTimeOffset.UtcNow.ToUnixTimeSeconds()) }
            : await ReadHeaderAsync(connection, customerId, file, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SaveApplicationAsync(ErpCreditApplicationWrite request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CustomerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A customer id is required.");
        }

        if (request.RequestedLimit < 0m || request.RequestedTerms < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "The requested limit and terms cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_credit_applications` (`customer_id`,`party_kind`,`valid_until`,`requested_limit`,`requested_terms_days`,`time_updated`)"
                + " VALUES (?,?,?,?,?,?)"
                + " ON DUPLICATE KEY UPDATE `party_kind`=VALUES(`party_kind`), `valid_until`=VALUES(`valid_until`), `requested_limit`=VALUES(`requested_limit`), `requested_terms_days`=VALUES(`requested_terms_days`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            request.CustomerId,
            ErpCreditApplication.NormalizeParty(request.Party),
            ErpCreditBlock.EndOfUtcDay(request.ValidUntil),
            ErpCreditBlock.Round(request.RequestedLimit),
            request.RequestedTerms,
            now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Credit application saved for customer " + request.CustomerId.ToString(CultureInfo.InvariantCulture) + ".", request.CustomerId);
    }

    public async Task<ErpSimpleWriteResult> SavePartnerAsync(long customerId, string? name, long retirePartnerId, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A customer id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureApplicationRowAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (retirePartnerId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_credit_partners` SET `active`=0, `time_updated`=? WHERE `id`=? AND `customer_id`=?"),
                cancellationToken,
                now,
                retirePartnerId,
                customerId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_credit_documents` SET `active`=0, `time_updated`=? WHERE `customer_id`=? AND `partner_id`=?"),
                cancellationToken,
                now,
                customerId,
                retirePartnerId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Partner removed from the credit application.", retirePartnerId);
        }

        var partnerName = Clip(name, 200);
        if (partnerName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A partner name is required.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_credit_partners` (`customer_id`,`full_name`,`active`,`time_updated`) VALUES (?,?,1,?)"),
            cancellationToken,
            customerId,
            partnerName,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Partner " + partnerName + " added. Attach passport, visa, and Emirates ID for this partner.", id);
    }

    public async Task<ErpSimpleWriteResult> SaveDocumentAsync(ErpCreditDocumentWrite request, Stream content, string? fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        if (request.CustomerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A customer id is required.");
        }

        if (!ErpCreditApplication.KnownKind(request.Kind))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose a trade license, passport, visa, or Emirates ID.");
        }

        var number = Clip(request.Number, 80);
        if (number.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A document number is required.");
        }

        var expiry = ErpCreditBlock.EndOfUtcDay(request.Expiry);
        if (expiry <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An expiry date is required.");
        }

        var ext = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
        if (ext is not ("pdf" or "jpg" or "jpeg" or "png" or "webp"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Attach a PDF or a photo (JPG, PNG, or WebP).");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var party = await PartyKindAsync(connection, request.CustomerId, cancellationToken).ConfigureAwait(false);
        var kind = request.Kind!.Trim().ToLowerInvariant();
        var partnerId = request.PartnerId < 0 ? 0 : request.PartnerId;
        if (ErpCreditApplication.IsIndividual(party))
        {
            if (kind.Equals(ErpCreditApplication.TradeLicense, StringComparison.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("invalid", "An individual credit application does not use a trade license.");
            }

            partnerId = 0;
        }
        else if (kind.Equals(ErpCreditApplication.TradeLicense, StringComparison.Ordinal))
        {
            partnerId = 0;
        }
        else if (!await PartnerBelongsAsync(connection, request.CustomerId, partnerId, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the partner this passport, visa, or Emirates ID belongs to.");
        }

        var stored = "credit_" + request.CustomerId.ToString(CultureInfo.InvariantCulture)
            + "_" + kind + "_" + Guid.NewGuid().ToString("N") + "." + (ext == "jpeg" ? "jpg" : ext);
        var dir = DocumentDirectory();
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, stored);
        long size;
        await using (var dest = new FileStream(full, FileMode.CreateNew, FileAccess.Write))
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            await content.CopyToAsync(dest, cancellationToken).ConfigureAwait(false);
            size = dest.Length;
        }

        if (size <= 0 || size > 15L * 1024 * 1024)
        {
            TryDelete(full);
            return ErpSimpleWriteResult.Fail("invalid", size <= 0 ? "The file is empty." : "File exceeds 15 MB.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_credit_documents` SET `active`=0, `time_updated`=? WHERE `customer_id`=? AND `partner_id`=? AND `doc_kind`=? AND `active`=1"),
                cancellationToken,
                now,
                request.CustomerId,
                partnerId,
                kind).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_credit_documents` (`customer_id`,`partner_id`,`doc_kind`,`doc_number`,`expiry_unix`,`file_name`,`file_path`,`file_size`,`active`,`time_updated`)"
                    + " VALUES (?,?,?,?,?,?,?,?,1,?)"),
                cancellationToken,
                request.CustomerId,
                partnerId,
                kind,
                number,
                expiry,
                Clip(Path.GetFileName(fileName), 255),
                stored,
                size,
                now).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            TryDelete(full);
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(ErpCreditApplication.Label(kind) + " attached.", id);
    }

    public async Task<ErpCreditDocumentFile?> OpenDocumentAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || documentId <= 0)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        string stored;
        string download;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`file_path`,''), IFNULL(`file_name`,''), IFNULL(`doc_kind`,'') FROM `epc_credit_documents` WHERE `id`=? AND `active`=1 LIMIT 1");
            ErpDb.AddParameters(command, documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            stored = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            download = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        }
        catch (DbException)
        {
            return null;
        }

        var full = ResolveStoredFile(stored);
        if (full is null)
        {
            return null;
        }

        var ext = Path.GetExtension(full).TrimStart('.').ToLowerInvariant();
        var type = ext switch
        {
            "pdf" => "application/pdf",
            "png" => "image/png",
            "webp" => "image/webp",
            _ => "image/jpeg",
        };
        var name = string.IsNullOrWhiteSpace(download) ? Path.GetFileName(full) : Path.GetFileName(download);
        return new ErpCreditDocumentFile(new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read), type, name);
    }

    private static async Task<Profile?> ProfileAsync(DbConnection connection, long customerId, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `on_hold`,`credit_limit`,`terms_days`,IFNULL(`permanent_limit`,0),IFNULL(`permanent_terms_days`,0),IFNULL(`limit_review_date`,0),IFNULL(`temporary_limit`,0),IFNULL(`temporary_terms_days`,0),IFNULL(`temporary_from`,0),IFNULL(`temporary_until`,0),IFNULL(`mandatory_limit`,0),IFNULL(`legal_entity_id`,0)"
                + " FROM `epc_credit_profiles` WHERE `customer_id`=? LIMIT 1");
            ErpDb.AddParameters(command, customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var creditLimit = Dec(reader, 1);
            var terms = Int(reader, 2);
            var permanent = Dec(reader, 3);
            var permanentTerms = Int(reader, 4);
            return new Profile(
                Int(reader, 0) == 1,
                Int(reader, 10) == 1,
                permanent > 0m ? permanent : creditLimit,
                permanentTerms > 0 ? permanentTerms : terms,
                Long(reader, 5),
                Dec(reader, 6),
                Int(reader, 7),
                Long(reader, 8),
                Long(reader, 9),
                Long(reader, 11));
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<ErpCreditBlock.Rules> RulesAsync(DbConnection connection, long companyId, CancellationToken cancellationToken)
    {
        var specific = await ReadRulesAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        if (specific is not null)
        {
            return specific;
        }

        if (companyId != 0)
        {
            var shared = await ReadRulesAsync(connection, 0, cancellationToken).ConfigureAwait(false);
            if (shared is not null)
            {
                return shared;
            }
        }

        return ErpCreditBlock.Rules.Defaults;
    }

    private static async Task<ErpCreditBlock.Rules?> ReadRulesAsync(DbConnection connection, long companyId, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `block_account_status`,`block_credit_limit`,`limit_percent`,`limit_tolerance`,`block_limit_expired`,`block_days_overdue`,`days_threshold`,`grace_days`,`block_overdue_amount`,`overdue_threshold`,`block_terms`,IFNULL(`block_documents`,1)"
                + " FROM `epc_credit_block_rules` WHERE `company_id`=? LIMIT 1");
            ErpDb.AddParameters(command, companyId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new ErpCreditBlock.Rules(
                Int(reader, 0) == 1,
                Int(reader, 1) == 1,
                Dec(reader, 2),
                Dec(reader, 3),
                Int(reader, 4) == 1,
                Int(reader, 5) == 1,
                Int(reader, 6),
                Int(reader, 7),
                Int(reader, 8) == 1,
                Dec(reader, 9),
                Int(reader, 10) == 1,
                Int(reader, 11) == 1);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<OpenInvoice>> OpenInvoicesAsync(
        DbConnection connection,
        long customerId,
        long excludeInvoiceId,
        int termsDays,
        CancellationToken cancellationToken)
    {
        var rows = new List<OpenInvoice>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`issue_date`,`payment_due_date`, ROUND(`total_incl_vat` - `paid_amount`, 2)"
                + " FROM `epc_einvoice_documents`"
                + " WHERE `active`=1 AND `user_id`=? AND `status` NOT IN ('cancelled','draft')"
                + " AND `doc_category` IN ('tax_invoice','commercial_invoice')"
                + " AND ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005");
            ErpDb.AddParameters(command, customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Long(reader, 0);
                if (id == excludeInvoiceId)
                {
                    continue;
                }

                var issued = Long(reader, 1);
                var due = Long(reader, 2);
                if (due <= 0 && issued > 0)
                {
                    due = issued + (Math.Max(termsDays, 0) * 86400L);
                }

                rows.Add(new OpenInvoice(due, Dec(reader, 3)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpCreditProfileRow>> CustomersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ErpCreditProfileRow>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `customer_id`, IFNULL(`customer_name`,''), `on_hold`, `credit_limit`, `terms_days`, IFNULL(`temporary_limit`,0), IFNULL(`temporary_terms_days`,0), IFNULL(`mandatory_limit`,0), IFNULL(`limit_review_date`,0), IFNULL(`temporary_from`,0), IFNULL(`temporary_until`,0)"
                + " FROM `epc_credit_profiles` ORDER BY `customer_id` DESC LIMIT 80";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpCreditProfileRow(
                    Long(reader, 0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Int(reader, 2) == 1,
                    Dec(reader, 3),
                    Int(reader, 4),
                    Dec(reader, 5),
                    Int(reader, 6),
                    Int(reader, 7) == 1,
                    Long(reader, 8),
                    Long(reader, 9),
                    Long(reader, 10)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpCreditSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var (column, definition) in new[]
        {
            ("permanent_limit", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("permanent_terms_days", "int(11) NOT NULL DEFAULT 0"),
            ("limit_review_date", "int(11) NOT NULL DEFAULT 0"),
            ("temporary_limit", "decimal(14,2) NOT NULL DEFAULT 0.00"),
            ("temporary_terms_days", "int(11) NOT NULL DEFAULT 0"),
            ("temporary_from", "int(11) NOT NULL DEFAULT 0"),
            ("temporary_until", "int(11) NOT NULL DEFAULT 0"),
            ("mandatory_limit", "tinyint(1) NOT NULL DEFAULT 0"),
        })
        {
            await ErpDb.TryExecuteAsync(
                connection,
                "ALTER TABLE `epc_credit_profiles` ADD COLUMN `" + column + "` " + definition,
                cancellationToken).ConfigureAwait(false);
        }

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_credit_block_rules` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`company_id` int(11) NOT NULL DEFAULT 0,"
            + "`block_account_status` tinyint(1) NOT NULL DEFAULT 1,"
            + "`block_credit_limit` tinyint(1) NOT NULL DEFAULT 1,"
            + "`limit_percent` decimal(8,2) NOT NULL DEFAULT 100.00,"
            + "`limit_tolerance` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`block_limit_expired` tinyint(1) NOT NULL DEFAULT 1,"
            + "`block_days_overdue` tinyint(1) NOT NULL DEFAULT 1,"
            + "`days_threshold` int(11) NOT NULL DEFAULT 1,"
            + "`grace_days` int(11) NOT NULL DEFAULT 0,"
            + "`block_overdue_amount` tinyint(1) NOT NULL DEFAULT 1,"
            + "`overdue_threshold` decimal(14,2) NOT NULL DEFAULT 0.01,"
            + "`block_terms` tinyint(1) NOT NULL DEFAULT 1,"
            + "`block_documents` tinyint(1) NOT NULL DEFAULT 1,"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), UNIQUE KEY `x_company` (`company_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Invoice blocking rules'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_credit_block_rules` ADD COLUMN `block_documents` tinyint(1) NOT NULL DEFAULT 1",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_credit_applications` ("
            + "`customer_id` int(11) NOT NULL,"
            + "`party_kind` varchar(20) NOT NULL DEFAULT 'company',"
            + "`valid_until` int(11) NOT NULL DEFAULT 0,"
            + "`requested_limit` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`requested_terms_days` int(11) NOT NULL DEFAULT 0,"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`customer_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Credit application'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_credit_partners` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`customer_id` int(11) NOT NULL DEFAULT 0,"
            + "`full_name` varchar(200) NOT NULL DEFAULT '',"
            + "`active` tinyint(1) NOT NULL DEFAULT 1,"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), KEY `x_customer` (`customer_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Credit application partners'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_credit_documents` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`customer_id` int(11) NOT NULL DEFAULT 0,"
            + "`partner_id` int(11) NOT NULL DEFAULT 0,"
            + "`doc_kind` varchar(32) NOT NULL DEFAULT '',"
            + "`doc_number` varchar(80) NOT NULL DEFAULT '',"
            + "`expiry_unix` int(11) NOT NULL DEFAULT 0,"
            + "`file_name` varchar(255) NOT NULL DEFAULT '',"
            + "`file_path` varchar(255) NOT NULL DEFAULT '',"
            + "`file_size` int(11) NOT NULL DEFAULT 0,"
            + "`active` tinyint(1) NOT NULL DEFAULT 1,"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), KEY `x_customer_kind` (`customer_id`,`partner_id`,`doc_kind`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Credit application documents'",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ErpCreditApplication.File?> ApplicationFileAsync(DbConnection connection, long customerId, CancellationToken cancellationToken)
    {
        try
        {
            string party;
            long validUntil;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`party_kind`,''), IFNULL(`valid_until`,0) FROM `epc_credit_applications` WHERE `customer_id`=? LIMIT 1");
                ErpDb.AddParameters(command, customerId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                party = reader.IsDBNull(0) ? ErpCreditApplication.Company : reader.GetString(0);
                validUntil = Long(reader, 1);
            }

            var partners = new List<ErpCreditApplication.Partner>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`full_name`,'') FROM `epc_credit_partners` WHERE `customer_id`=? AND `active`=1 ORDER BY `id`");
                ErpDb.AddParameters(command, customerId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    partners.Add(new ErpCreditApplication.Partner(Long(reader, 0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
                }
            }

            var papers = new List<ErpCreditApplication.Paper>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `partner_id`, IFNULL(`doc_kind`,''), IFNULL(`doc_number`,''), IFNULL(`expiry_unix`,0), IFNULL(`file_path`,'')"
                    + " FROM `epc_credit_documents` WHERE `customer_id`=? AND `active`=1 ORDER BY `id`");
                ErpDb.AddParameters(command, customerId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var path = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                    papers.Add(new ErpCreditApplication.Paper(
                        Long(reader, 0),
                        reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Long(reader, 3),
                        path.Length > 0));
                }
            }

            return new ErpCreditApplication.File(true, ErpCreditApplication.NormalizeParty(party), validUntil, partners, papers);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<ErpCreditDocumentRow>> DocumentRowsAsync(DbConnection connection, long customerId, CancellationToken cancellationToken)
    {
        var rows = new List<ErpCreditDocumentRow>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`partner_id`,IFNULL(`doc_kind`,''),IFNULL(`doc_number`,''),IFNULL(`expiry_unix`,0),IFNULL(`file_name`,''),IFNULL(`file_path`,'')"
                + " FROM `epc_credit_documents` WHERE `customer_id`=? AND `active`=1 ORDER BY `partner_id`,`doc_kind`,`id`");
            ErpDb.AddParameters(command, customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var path = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                rows.Add(new ErpCreditDocumentRow(
                    Long(reader, 0),
                    Long(reader, 1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Long(reader, 4),
                    reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    path.Length > 0));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private async Task<ErpCreditApplicationSnapshot> ReadHeaderAsync(DbConnection connection, long customerId, ErpCreditApplication.File? file, CancellationToken cancellationToken)
    {
        var limit = 0m;
        var terms = 0;
        if (file is not null)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`requested_limit`,0), IFNULL(`requested_terms_days`,0) FROM `epc_credit_applications` WHERE `customer_id`=? LIMIT 1");
                ErpDb.AddParameters(command, customerId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    limit = Dec(reader, 0);
                    terms = Int(reader, 1);
                }
            }
            catch (DbException)
            {
                limit = 0m;
            }
        }

        var stored = file is null ? [] : await DocumentRowsAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        var partners = file?.Partners.Select(partner => new ErpCreditPartnerRow(partner.Id, partner.Name)).ToArray() ?? [];
        return new ErpCreditApplicationSnapshot(
            file is not null,
            file?.Party ?? ErpCreditApplication.Company,
            file?.ValidUntilUnix ?? 0,
            limit,
            terms,
            partners,
            stored,
            ErpCreditApplication.Gaps(file, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    private static async Task EnsureApplicationRowAsync(DbConnection connection, long customerId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_credit_applications` (`customer_id`,`party_kind`,`valid_until`,`requested_limit`,`requested_terms_days`,`time_updated`)"
                + " VALUES (?,'company',0,0,0,?)"
                + " ON DUPLICATE KEY UPDATE `customer_id`=`customer_id`"),
            cancellationToken,
            customerId,
            now).ConfigureAwait(false);
    }

    private static async Task<string> PartyKindAsync(DbConnection connection, long customerId, CancellationToken cancellationToken)
    {
        await EnsureApplicationRowAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT IFNULL(`party_kind`,'company') FROM `epc_credit_applications` WHERE `customer_id`=? LIMIT 1");
            ErpDb.AddParameters(command, customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && !reader.IsDBNull(0))
            {
                return ErpCreditApplication.NormalizeParty(reader.GetString(0));
            }
        }
        catch (DbException)
        {
            return ErpCreditApplication.Company;
        }

        return ErpCreditApplication.Company;
    }

    private static async Task<bool> PartnerBelongsAsync(DbConnection connection, long customerId, long partnerId, CancellationToken cancellationToken)
    {
        if (partnerId <= 0)
        {
            return false;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `id` FROM `epc_credit_partners` WHERE `id`=? AND `customer_id`=? AND `active`=1 LIMIT 1");
            ErpDb.AddParameters(command, partnerId, customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return false;
        }
    }

    private string DocumentDirectory()
        => Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(_env), "content", "files", "epc_credit_documents");

    private string? ResolveStoredFile(string? stored)
    {
        var name = Path.GetFileName((stored ?? string.Empty).Trim());
        if (name.Length == 0 || name != stored?.Trim())
        {
            if (name.Length == 0)
            {
                return null;
            }
        }

        var root = Path.GetFullPath(DocumentDirectory());
        var full = Path.GetFullPath(Path.Combine(root, name));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(full) ? full : null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static decimal Dec(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static int Int(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static long Long(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private sealed record Profile(
        bool OnHold,
        bool Mandatory,
        decimal PermanentLimit,
        int TermsDays,
        long ReviewUntil,
        decimal TemporaryLimit,
        int TemporaryTermsDays,
        long TemporaryFrom,
        long TemporaryUntil,
        long CompanyId);

    private sealed record OpenInvoice(long Due, decimal Outstanding);
}
