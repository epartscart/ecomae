using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    private const int VendorMaxBytes = 12 * 1024 * 1024;

    private static readonly string[] WorkshopSchema =
    [
        """
        CREATE TABLE IF NOT EXISTS `epc_ws_bays` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(20) NOT NULL DEFAULT '',
            `name` varchar(80) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `sort_order` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `u_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_ws_technicians` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `name` varchar(120) NOT NULL DEFAULT '',
            `phone` varchar(40) NOT NULL DEFAULT '',
            `skill` varchar(80) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_ws_jobs` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `job_no` varchar(40) NOT NULL DEFAULT '',
            `status` varchar(20) NOT NULL DEFAULT 'checkin',
            `customer_name` varchar(160) NOT NULL DEFAULT '',
            `customer_phone` varchar(40) NOT NULL DEFAULT '',
            `customer_email` varchar(160) NOT NULL DEFAULT '',
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `plate` varchar(40) NOT NULL DEFAULT '',
            `vin` varchar(40) NOT NULL DEFAULT '',
            `make` varchar(80) NOT NULL DEFAULT '',
            `model` varchar(80) NOT NULL DEFAULT '',
            `year` varchar(10) NOT NULL DEFAULT '',
            `odometer` int(11) NOT NULL DEFAULT 0,
            `complaint` text,
            `bay_id` int(11) NOT NULL DEFAULT 0,
            `tech_id` int(11) NOT NULL DEFAULT 0,
            `estimate_approved` tinyint(1) NOT NULL DEFAULT 0,
            `under_warranty` tinyint(1) NOT NULL DEFAULT 0,
            `parts_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `labour_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `tax_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `grand_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `notes` text,
            `time_promised` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `u_job_no` (`job_no`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_ws_job_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `job_id` int(11) NOT NULL,
            `line_type` varchar(10) NOT NULL DEFAULT 'part',
            `description` varchar(190) NOT NULL DEFAULT '',
            `item_id` int(11) NOT NULL DEFAULT 0,
            `qty` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `unit_price` decimal(14,2) NOT NULL DEFAULT 0.00,
            `tax_percent` decimal(7,3) NOT NULL DEFAULT 5.000,
            `chargeable` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_ws_appointments` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `ref_no` varchar(40) NOT NULL DEFAULT '',
            `status` varchar(20) NOT NULL DEFAULT 'scheduled',
            `customer_name` varchar(160) NOT NULL DEFAULT '',
            `customer_phone` varchar(40) NOT NULL DEFAULT '',
            `customer_email` varchar(160) NOT NULL DEFAULT '',
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `garage_id` int(11) NOT NULL DEFAULT 0,
            `plate` varchar(40) NOT NULL DEFAULT '',
            `make` varchar(80) NOT NULL DEFAULT '',
            `model` varchar(80) NOT NULL DEFAULT '',
            `year` varchar(10) NOT NULL DEFAULT '',
            `service_type` varchar(80) NOT NULL DEFAULT 'General service',
            `notes` text,
            `time_slot` int(11) NOT NULL DEFAULT 0,
            `job_id` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `u_ref` (`ref_no`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """,
        """
        CREATE TABLE IF NOT EXISTS `epc_ws_labour_ops` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `hours` decimal(8,2) NOT NULL DEFAULT 1.00,
            `rate` decimal(14,2) NOT NULL DEFAULT 150.00,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            UNIQUE KEY `u_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """
    ];

    private static readonly Dictionary<string, string> JobStatuses = new(StringComparer.Ordinal)
    {
        ["checkin"] = "Check-in",
        ["estimate"] = "Estimate",
        ["approved"] = "Approved",
        ["in_progress"] = "In progress",
        ["qc"] = "QC / test",
        ["ready"] = "Ready",
        ["delivered"] = "Delivered",
        ["cancelled"] = "Cancelled"
    };

    public static Task<object> LoadReturnsAsync(
        DbConnection connection,
        string expectedTechKey,
        string postedTechKey,
        IReadOnlyList<ReturnLine> items,
        string userId,
        string totalSum,
        CancellationToken cancellationToken)
        => LoadReturnsFullAsync(connection, expectedTechKey, postedTechKey, items, userId, totalSum, string.Empty, [], string.Empty, null, cancellationToken);

    public static async Task<object> WorkshopPublicAsync(
        DbConnection connection,
        string action,
        string name,
        string phone,
        string plate,
        string complaint,
        string email,
        string vin,
        string make,
        string model,
        string year,
        string odometer,
        string reference,
        CancellationToken cancellationToken)
    {
        await EnsureWorkshopAsync(connection, cancellationToken).ConfigureAwait(false);
        if (string.Equals(action, "track", StringComparison.Ordinal))
        {
            return await TrackWorkshopAsync(connection, reference, phone, cancellationToken).ConfigureAwait(false);
        }

        if (!string.Equals(action, "book", StringComparison.Ordinal))
        {
            return new FlagBody(false, WorkshopUnknown);
        }

        if (name.Trim().Length == 0 || phone.Trim().Length == 0 || plate.Trim().Length == 0 || complaint.Trim().Length == 0)
        {
            return new FlagBody(false, WorkshopRequired);
        }

        var created = await CreateJobAsync(
            connection,
            "checkin",
            name,
            phone,
            email,
            plate,
            vin,
            make,
            model,
            year,
            odometer,
            complaint,
            "Booked from storefront /auto-workshop",
            cancellationToken).ConfigureAwait(false);
        return new WorkshopBookBody(true, WorkshopBooked, created.JobNo, created.Id);
    }

    public static async Task<object> GarageManagerAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? userSession,
        string? userIdCookie,
        string postedCsrf,
        string action,
        string name,
        string phone,
        string plate,
        string complaint,
        string email,
        string vin,
        string make,
        string model,
        string year,
        string odometer,
        string jobId,
        string status,
        CancellationToken cancellationToken)
    {
        bool staff;
        try
        {
            staff = await IsAdminSessionAsync(connection, adminSession, adminUser, cancellationToken).ConfigureAwait(false)
                || await IsBackendGroupAsync(connection, userSession, userIdCookie, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return SessionsMissing;
        }

        if (!staff)
        {
            return new FlagBody(false, GarageDenied);
        }

        var stored = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? AND `type` = 1 LIMIT 1"),
            cancellationToken,
            adminSession ?? string.Empty,
            ParseId(adminUser)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(stored) || !string.Equals(stored, postedCsrf, StringComparison.Ordinal))
        {
            return new FlagBody(false, GarageCsrf);
        }

        await EnsureWorkshopAsync(connection, cancellationToken).ConfigureAwait(false);
        if (string.Equals(action, "create_job", StringComparison.Ordinal))
        {
            var created = await CreateJobAsync(
                connection,
                status,
                name,
                phone,
                email,
                plate,
                vin,
                make,
                model,
                year,
                odometer,
                complaint,
                string.Empty,
                cancellationToken).ConfigureAwait(false);
            return new GarageJobBody(true, GarageCreated, new GarageJobCard(new GarageJobHeader(created.JobNo, created.Status, created.Plate, created.Name)));
        }

        if (string.Equals(action, "set_status", StringComparison.Ordinal))
        {
            if (!JobStatuses.ContainsKey(status) || ParseId(jobId) <= 0)
            {
                return new FlagBody(false, GarageInvalid);
            }

            var now = UnixNow();
            if (status is "approved" or "in_progress" or "qc" or "ready" or "delivered")
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `epc_ws_jobs` SET `status` = ?, `estimate_approved` = 1, `time_updated` = ? WHERE `id` = ?"),
                    cancellationToken,
                    status,
                    now,
                    ParseId(jobId)).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `epc_ws_jobs` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
                    cancellationToken,
                    status,
                    now,
                    ParseId(jobId)).ConfigureAwait(false);
            }

            return new FlagBody(true, "Status → " + status);
        }

        return new FlagBody(false, WorkshopUnknown);
    }

    public static async Task<object> ContactsAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? type,
        string? action,
        string? csrf,
        string? contact,
        bool hasType,
        bool hasAction,
        bool hasCsrf,
        CancellationToken cancellationToken)
    {
        int userId;
        try
        {
            userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, SessionsMissing);
        }

        if (userId == 0)
        {
            return new FlagBody(false, ContactsNotLoggedIn);
        }

        if (!hasType || !hasAction || !hasCsrf)
        {
            return new FlagBody(false, ContactsBadInput);
        }

        string? stored;
        try
        {
            stored = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                session ?? string.Empty,
                userId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, SessionsMissing);
        }

        if (!string.Equals(stored ?? string.Empty, csrf ?? string.Empty, StringComparison.Ordinal))
        {
            return new FlagBody(false, ContactsCsrf);
        }

        if (type is not ("email" or "phone") || action is not ("set" or "change" or "confirm"))
        {
            return new FlagBody(false, ContactsBadInput);
        }

        string current;
        string confirmed;
        long lockUntil;
        try
        {
            current = await ErpDb.StringAsync(
                connection,
                null,
                "SELECT `" + type + "` FROM `users` WHERE `user_id` = ?",
                cancellationToken,
                userId).ConfigureAwait(false) ?? string.Empty;
            confirmed = await ErpDb.StringAsync(
                connection,
                null,
                "SELECT `" + type + "_confirmed` FROM `users` WHERE `user_id` = ?",
                cancellationToken,
                userId).ConfigureAwait(false) ?? "0";
            lockUntil = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `" + type + "_code_send_lock_expired` FROM `users` WHERE `user_id` = ?",
                cancellationToken,
                userId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, UserAccountsMissing);
        }

        if (action is "set" or "change")
        {
            if (contact is null)
            {
                return new FlagBody(false, ContactsBadInput);
            }

            string? pattern;
            try
            {
                pattern = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `regexp` FROM `reg_fields` WHERE `name` = ?"),
                    cancellationToken,
                    type).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new FlagBody(false, RegistrationFieldsMissing);
            }

            if (!string.IsNullOrEmpty(pattern) && !Regex.IsMatch(contact, pattern))
            {
                return new FlagBody(false, ContactsRegexp);
            }

            try
            {
                var taken = await ErpDb.LongAsync(
                    connection,
                    null,
                    "SELECT COUNT(*) FROM `users` WHERE (`" + type + "` = ? OR `" + type + "_new` = ?) AND `user_id` != ?",
                    cancellationToken,
                    contact,
                    contact,
                    userId).ConfigureAwait(false);
                if (taken > 0)
                {
                    return new FlagBody(false, ContactsDuplicate);
                }
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new FlagBody(false, UserAccountsMissing);
            }
        }

        if (action == "set" && current.Length > 0)
        {
            return new FlagBody(false, ContactsBadInput);
        }

        if (action == "change" && current.Length == 0)
        {
            return new FlagBody(false, ContactsEmpty);
        }

        if (action == "confirm" && current.Length == 0)
        {
            return new FlagBody(false, ContactsMissingContact);
        }

        if (action == "confirm" && confirmed != "0")
        {
            return new FlagBody(false, ContactsEmpty);
        }

        if (action is "change" or "confirm" && lockUntil > UnixNow())
        {
            return new FlagBody(false, ContactsLock);
        }

        return new FlagBody(false, ContactsNotifyFailed);
    }

    public static async Task<object> SendLoginCodeAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? postedCsrf,
        string? method,
        string? contact,
        CancellationToken cancellationToken)
    {
        var csrf = await SessionCsrfAsync(connection, session, userCookie, postedCsrf, cancellationToken).ConfigureAwait(false);
        if (csrf.Failure is not null)
        {
            return csrf.Failure;
        }

        var data = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `data` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            session ?? string.Empty,
            csrf.UserId).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(data))
        {
            try
            {
                using var document = JsonDocument.Parse(data);
                if (document.RootElement.TryGetProperty("timeSendFaCode", out var sent) && sent.TryGetInt64(out var unix))
                {
                    var elapsed = UnixNow() - unix;
                    if (elapsed < 30)
                    {
                        var wait = 30 - elapsed;
                        return new LoginCodeBody(501, "5656 " + wait.ToString(CultureInfo.InvariantCulture) + " 5647");
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        if (method is not ("sms" or "smtp"))
        {
            return new LoginCodeBody(403, LoginUnknownMethod);
        }

        var field = method == "sms" ? "phone" : "email";
        string? pattern;
        try
        {
            pattern = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `regexp` FROM `reg_fields` WHERE `name` = ?"),
                cancellationToken,
                field).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, RegistrationFieldsMissing);
        }

        if (!string.IsNullOrEmpty(pattern) && !Regex.IsMatch(contact ?? string.Empty, pattern))
        {
            return new LoginCodeBody(501, LoginBadContact);
        }

        return new LoginCodeBody(501, LoginNotifyFailed);
    }

    public static async Task<object> CheckLoginCodeAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? postedCsrf,
        string? code,
        CancellationToken cancellationToken)
    {
        var csrf = await SessionCsrfAsync(connection, session, userCookie, postedCsrf, cancellationToken).ConfigureAwait(false);
        if (csrf.Failure is not null)
        {
            return csrf.Failure;
        }

        var stored = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `2fa_code` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            session ?? string.Empty,
            csrf.UserId).ConfigureAwait(false) ?? string.Empty;
        var attempts = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `2fa_attempts` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            session ?? string.Empty,
            csrf.UserId).ConfigureAwait(false);
        if (attempts < 1)
        {
            return new LoginCodeBody(501, LoginNoAttempts);
        }

        if (string.Equals(stored, code ?? string.Empty, StringComparison.Ordinal))
        {
            var data = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `data` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                session ?? string.Empty,
                csrf.UserId).ConfigureAwait(false);
            long expire = 0;
            if (!string.IsNullOrWhiteSpace(data))
            {
                try
                {
                    using var document = JsonDocument.Parse(data);
                    if (document.RootElement.TryGetProperty("expireFaCode", out var node) && node.TryGetInt64(out var unix))
                    {
                        expire = unix;
                    }
                }
                catch (JsonException)
                {
                }
            }

            if (expire < UnixNow())
            {
                return new LoginCodeBody(501, LoginExpired);
            }

            return new LoginCodeBody(200, null);
        }

        var left = attempts - 1;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `sessions` SET `2fa_attempts` = ? WHERE `session` = ? AND `user_id` = ?"),
            cancellationToken,
            left,
            session ?? string.Empty,
            csrf.UserId).ConfigureAwait(false);
        return new LoginCodeBody(501, LoginMismatch + ": " + left.ToString(CultureInfo.InvariantCulture) + ".");
    }

    public static async Task<object> BulkUploadAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        string? action,
        string? uploadId,
        string? summary,
        string? rows,
        string? article,
        bool hasFile,
        CancellationToken cancellationToken)
    {
        int userId;
        bool admin;
        try
        {
            userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
            admin = await IsAdminSessionAsync(connection, adminSession, adminUser, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, SessionsMissing);
        }

        if (userId <= 0 && !admin)
        {
            return new FlagBody(false, BulkLogin);
        }

        if (string.IsNullOrEmpty(postedCsrf))
        {
            return new FlagBody(false, "Error! CSRF 1");
        }

        if (!await BulkCsrfMatchesAsync(connection, session, userCookie, adminSession, adminUser, postedCsrf, cancellationToken).ConfigureAwait(false))
        {
            return new FlagBody(false, "Error! CSRF 4");
        }

        if (userId > 0)
        {
            var group = await CustomerGroupAsync(connection, userId, cancellationToken).ConfigureAwait(false);
            if (group.Missing is not null)
            {
                return new FlagBody(false, group.Missing);
            }

            if (group.Id <= 0)
            {
                return new FlagBody(false, BulkProfile);
            }
        }
        else if (!admin)
        {
            return new FlagBody(false, BulkProfile);
        }

        if (string.Equals(action, "history_update", StringComparison.Ordinal))
        {
            if (ParseId(uploadId) <= 0 || !IsJsonObject(summary) || !IsJsonArray(rows))
            {
                return new FlagBody(false, BulkHistory);
            }

            return new FlagBody(false, "Bulk upload history is not in this database.");
        }

        List<int> offices;
        try
        {
            offices = await CustomerOfficesAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, BulkWarehousesMissing);
        }

        var bunches = 0;
        try
        {
            foreach (var office in offices)
            {
                bunches += (int)await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "SELECT COUNT(*) FROM `shop_offices_storages_map` INNER JOIN `shop_storages` ON `shop_storages`.`id` = `shop_offices_storages_map`.`storage_id` WHERE `shop_offices_storages_map`.`office_id` = ? AND `shop_storages`.`hidden` = 0 AND (SELECT `handler_folder` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) = 'prices' AND `shop_storages`.`connection_options` LIKE '%price_id%'"),
                    cancellationToken,
                    office).ConfigureAwait(false);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, BulkWarehousesMissing);
        }

        if (bunches == 0)
        {
            return new FlagBody(false, BulkNoWarehouses);
        }

        if (string.Equals(action, "cross", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(article))
            {
                return new FlagBody(false, BulkPartRequired);
            }

            return new FlagBody(false, PriceListsMissing);
        }

        if (!hasFile)
        {
            return new FlagBody(false, BulkFileRequired);
        }

        return new FlagBody(false, PriceListsMissing);
    }

    public static async Task<object> VendorIngestAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? postedCsrf,
        bool hasFile,
        string? fileName,
        long fileSize,
        CancellationToken cancellationToken)
    {
        int userId;
        try
        {
            userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, SessionsMissing);
        }

        if (userId <= 0)
        {
            return new FlagBody(false, VendorSignIn);
        }

        string? status;
        try
        {
            status = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `status` FROM `epc_vendor_accounts` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, VendorAccountsMissing);
        }

        if (status is not ("approved" or "active"))
        {
            return new FlagBody(false, VendorNotApproved);
        }

        var stored = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            session ?? string.Empty,
            userId).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(stored) && !string.IsNullOrEmpty(postedCsrf) && !string.Equals(stored, postedCsrf, StringComparison.Ordinal))
        {
            return new FlagBody(false, VendorToken);
        }

        if (!hasFile)
        {
            return new FlagBody(false, VendorChooseFile);
        }

        if (fileSize > VendorMaxBytes)
        {
            return new FlagBody(false, VendorTooLarge);
        }

        var ext = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
        if (ext is not ("csv" or "xls" or "xlsx" or "txt"))
        {
            return new FlagBody(false, VendorBadType);
        }

        return new FlagBody(false, VendorImportFailed);
    }

    public static async Task<object> UCatalogApiAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? requestObject,
        CancellationToken cancellationToken)
    {
        var answer = new UCatalogAnswer(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, null);
        if (string.IsNullOrWhiteSpace(requestObject))
        {
            answer = answer with { Request = new UCatalogRequest("null") };
            return answer;
        }

        JsonElement request;
        try
        {
            using var document = JsonDocument.Parse(requestObject);
            request = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            answer = answer with { Request = new UCatalogRequest("null") };
            return answer;
        }

        var action = FieldText(request, "action");
        if (string.Equals(action, "get_garage", StringComparison.Ordinal))
        {
            try
            {
                var json = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `UCatalog_json` FROM `shop_docpart_garage` WHERE `id` = ?"),
                    cancellationToken,
                    ParseId(FieldText(request, "id"))).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var stored = JsonDocument.Parse(json);
                    request = stored.RootElement.Clone();
                    action = FieldText(request, "action");
                }
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return answer with { Message = GarageMissing, Request = new UCatalogRequest(action) };
            }
            catch (JsonException)
            {
            }
        }

        if (string.Equals(action, "add_garage", StringComparison.Ordinal))
        {
            return await AddUCatalogGarageAsync(connection, session, userCookie, request, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(action, "get_notepad", StringComparison.Ordinal))
        {
            return await UCatalogNotepadAsync(connection, session, userCookie, request, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(action, "add_notepad", StringComparison.Ordinal))
        {
            return await AddUCatalogNotepadAsync(connection, session, userCookie, request, cancellationToken).ConfigureAwait(false);
        }

        if (action is "get_marks" or "get_models" or "get_modifications" or "get_types" or "get_tree" or "get_parts")
        {
            var shown = action == "get_marks" ? "get_types" : action;
            return answer with { Message = UCatalogListFailure, Request = new UCatalogRequest(shown) };
        }

        if (string.Equals(action, "get_info", StringComparison.Ordinal))
        {
            return answer with { Key = FieldText(request, "key"), Request = new UCatalogRequest(action) };
        }

        if (string.Equals(action, "get_info_html", StringComparison.Ordinal))
        {
            return new UCatalogAnswer(true, string.Empty, string.Empty, string.Empty, FieldText(request, "key"), string.Empty, null, new UCatalogRequest(action));
        }

        return answer with { Request = new UCatalogRequest(string.IsNullOrEmpty(action) ? "null" : action) };
    }

    private static async Task<UCatalogAnswer> AddUCatalogGarageAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        JsonElement request,
        CancellationToken cancellationToken)
    {
        var userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        var nested = request.TryGetProperty("request_object", out var inner) ? inner : request;
        var caption = FieldText(nested, "caption");
        try
        {
            var columns = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_docpart_garage' AND COLUMN_NAME = 'UCatalog_json'"),
                cancellationToken).ConfigureAwait(false);
            if (columns == 0)
            {
                var table = await ErpDb.LongAsync(
                    connection,
                    null,
                    "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_docpart_garage'",
                    cancellationToken).ConfigureAwait(false);
                if (table == 0)
                {
                    return new UCatalogAnswer(false, GarageMissing, string.Empty, string.Empty, string.Empty, string.Empty, null, new UCatalogRequest("add_garage"));
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    "ALTER TABLE `shop_docpart_garage` ADD `UCatalog_json` TEXT NOT NULL",
                    cancellationToken).ConfigureAwait(false);
            }

            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `UCatalog_json` != '' AND `caption` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                caption,
                userId).ConfigureAwait(false);
            if (existing == 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `shop_docpart_garage` (`caption`, `UCatalog_json`, `user_id`) VALUES (?, ?, ?)"),
                    cancellationToken,
                    caption,
                    nested.GetRawText(),
                    userId).ConfigureAwait(false);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new UCatalogAnswer(false, GarageMissing, string.Empty, string.Empty, string.Empty, string.Empty, null, new UCatalogRequest("add_garage"));
        }

        return new UCatalogAnswer(
            true,
            string.Empty,
            "<span class=\"btn_primary\"><i class=\"fa fa-check\" aria-hidden=\"true\"></i> 2062</span>",
            "UCatalog_breadcrumbs_right_btn",
            string.Empty,
            string.Empty,
            null,
            new UCatalogRequest("add_garage"));
    }

    private static async Task<UCatalogAnswer> UCatalogNotepadAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        JsonElement request,
        CancellationToken cancellationToken)
    {
        var userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        try
        {
            var options = new StringBuilder();
            options.Append("<option value=\"0\">2100</option>");
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT g.`id`, g.`caption` FROM `shop_docpart_garage` g LEFT JOIN `shop_docpart_cars` c ON c.`id` = g.`mark_id` WHERE g.`user_id` = ?");
            ErpDb.AddParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.IsDBNull(0) ? "0" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture);
                var caption = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                options.Append("<option value=\"").Append(id).Append("\">").Append(caption).Append("</option>");
            }

            var html = "<span>2099</span><select id=\"UCatalog_garage_auto\" class=\"form-control\">" + options + "</select><span class=\"btn_primary\">2101</span>";
            return new UCatalogAnswer(true, string.Empty, html, "UCatalog_modal_garage_body", string.Empty, string.Empty, null, new UCatalogRequest("get_notepad"));
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new UCatalogAnswer(false, GarageMissing, string.Empty, string.Empty, string.Empty, string.Empty, null, new UCatalogRequest("get_notepad"));
        }
    }

    private static async Task<UCatalogAnswer> AddUCatalogNotepadAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        JsonElement request,
        CancellationToken cancellationToken)
    {
        var userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        if (userId == 0)
        {
            return new UCatalogAnswer(true, string.Empty, "<span><i class=\"fa fa-times\"></i> 2063</span>", "UCatalog_add_bloknot_msg", string.Empty, string.Empty, null, new UCatalogRequest("add_notepad"));
        }

        var garageId = ParseId(FieldText(request, "id_notepad"));
        if (garageId > 0)
        {
            try
            {
                var owned = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND `id` = ?"),
                    cancellationToken,
                    userId,
                    garageId).ConfigureAwait(false);
                if (owned <= 0)
                {
                    return new UCatalogAnswer(true, string.Empty, "<span><i class=\"fa fa-times\"></i> 2064</span>", "UCatalog_add_bloknot_msg", string.Empty, string.Empty, null, new UCatalogRequest("add_notepad"));
                }
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return new UCatalogAnswer(false, GarageMissing, string.Empty, string.Empty, string.Empty, string.Empty, null, new UCatalogRequest("add_notepad"));
            }
        }

        var article = HtmlCompat(StripTags(FieldText(request, "article")).Trim());
        if (article.Length == 0)
        {
            return new UCatalogAnswer(true, string.Empty, "<span><i class=\"fa fa-times\"></i> 2068</span>", "UCatalog_add_bloknot_msg", string.Empty, string.Empty, null, new UCatalogRequest("add_notepad"));
        }

        var brand = HtmlCompat(StripTags(FieldText(request, "manufacturer")).Trim());
        var name = HtmlCompat(StripTags(FieldText(request, "name")).Trim());
        var comment = "2065 " + DateTime.Now.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_docpart_garage_notepad` (`user_id`, `garage_id`, `brend`, `article`, `name`, `exist`, `price`, `comment`) VALUES (?, ?, ?, ?, ?, 0, 0, ?)"),
                cancellationToken,
                userId,
                garageId,
                brand,
                article,
                name,
                comment).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new UCatalogAnswer(false, NotepadMissing, string.Empty, string.Empty, string.Empty, string.Empty, null, new UCatalogRequest("add_notepad"));
        }

        return new UCatalogAnswer(true, string.Empty, "<span><i class=\"fa fa-check\"></i> 2066</span>", "UCatalog_add_bloknot_msg", string.Empty, string.Empty, null, new UCatalogRequest("add_notepad"));
    }

    private static async Task EnsureWorkshopAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in WorkshopSchema)
        {
            await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }

        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_ws_jobs` ADD COLUMN `garage_id` int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_ws_jobs` ADD COLUMN `appointment_id` int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_ws_jobs` ADD COLUMN `invoice_ref` varchar(64) NOT NULL DEFAULT ''", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CreatedJob> CreateJobAsync(
        DbConnection connection,
        string status,
        string name,
        string phone,
        string email,
        string plate,
        string vin,
        string make,
        string model,
        string year,
        string odometer,
        string complaint,
        string notes,
        CancellationToken cancellationToken)
    {
        if (!JobStatuses.ContainsKey(status))
        {
            status = "checkin";
        }

        var day = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `job_no` LIKE ?"),
            cancellationToken,
            "WS-" + day + "-%").ConfigureAwait(false);
        var jobNo = "WS-" + day + "-" + (count + 1).ToString("000", CultureInfo.InvariantCulture);
        var now = UnixNow();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_ws_jobs` (`job_no`,`status`,`customer_name`,`customer_phone`,`customer_email`,`customer_id`,`plate`,`vin`,`make`,`model`,`year`,`odometer`,`complaint`,`bay_id`,`tech_id`,`estimate_approved`,`under_warranty`,`notes`,`time_promised`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,0,?,?,?,?,?,?,?,0,0,0,0,?,0,?,?)"),
            cancellationToken,
            jobNo,
            status,
            name.Trim(),
            phone.Trim(),
            email.Trim(),
            plate.Trim().ToUpperInvariant(),
            vin.Trim().ToUpperInvariant(),
            make.Trim(),
            model.Trim(),
            year.Trim(),
            ParseId(odometer),
            complaint.Trim(),
            notes.Trim(),
            now,
            now).ConfigureAwait(false);
        var id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new CreatedJob(id, jobNo, status, plate.Trim().ToUpperInvariant(), name.Trim());
    }

    private static async Task<object> TrackWorkshopAsync(
        DbConnection connection,
        string reference,
        string phone,
        CancellationToken cancellationToken)
    {
        var jobNo = reference.Trim();
        if (jobNo.Length == 0)
        {
            return new FlagBody(false, WorkshopNoJob);
        }

        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT id FROM `epc_ws_jobs` WHERE `job_no` = ? OR `plate` = ? ORDER BY id DESC LIMIT 1"),
            cancellationToken,
            jobNo,
            jobNo.ToUpperInvariant()).ConfigureAwait(false);
        if (id <= 0)
        {
            return new FlagBody(false, WorkshopNoJob);
        }

        var storedPhone = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `customer_phone` FROM `epc_ws_jobs` WHERE `id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false) ?? string.Empty;
        if (phone.Length > 0 && !PhoneTail(storedPhone, phone))
        {
            return new FlagBody(false, WorkshopNoJob);
        }

        var rowJob = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `job_no` FROM `epc_ws_jobs` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false) ?? string.Empty;
        var rowStatus = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `status` FROM `epc_ws_jobs` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false) ?? string.Empty;
        var rowPlate = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `plate` FROM `epc_ws_jobs` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false) ?? string.Empty;
        var rowName = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `customer_name` FROM `epc_ws_jobs` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false) ?? string.Empty;
        var label = JobStatuses.TryGetValue(rowStatus, out var text) ? text : rowStatus;
        return new WorkshopTrackBody(true, new WorkshopTrackJob(rowJob, rowStatus, label, rowPlate, rowName));
    }

    private static async Task<int> ReturnStatusIdAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (var caption in new[] { "3806", "3796", "epc_ret_st_under_consideration", "epc_ret_st_created" })
        {
            var id = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = ? LIMIT 1"),
                cancellationToken,
                caption).ConfigureAwait(false);
            if (id > 0)
            {
                return (int)id;
            }
        }

        return (int)await ErpDb.LongAsync(
            connection,
            transaction,
            "SELECT `id` FROM `shop_orders_returns_statuses` ORDER BY `id` ASC LIMIT 1",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> OrderCountNeedAsync(DbConnection connection, DbTransaction transaction, int itemId, CancellationToken cancellationToken)
    {
        try
        {
            return (int)await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `count_need` FROM `shop_orders_items` WHERE `id` = ?"),
                cancellationToken,
                itemId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 0;
        }
    }

    private static async Task<int> CookieUserIdAsync(DbConnection connection, string? session, string? userCookie, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(session) || !int.TryParse(userCookie, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            return 0;
        }

        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `user_id` = ?"),
            cancellationToken,
            session,
            userId).ConfigureAwait(false);
        return count == 1 ? userId : 0;
    }

    private static async Task<bool> IsAdminSessionAsync(DbConnection connection, string? session, string? userCookie, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(session) || !int.TryParse(userCookie, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            return false;
        }

        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
            cancellationToken,
            session,
            userId).ConfigureAwait(false);
        return count == 1;
    }

    private static async Task<bool> IsBackendGroupAsync(DbConnection connection, string? session, string? userCookie, CancellationToken cancellationToken)
    {
        var userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        if (userId == 0)
        {
            return false;
        }

        try
        {
            var root = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (root <= 0)
            {
                return false;
            }

            var bound = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = ? AND (`group_id` = ? OR `group_id` IN (SELECT `id` FROM `groups` WHERE `parent` = ?))"),
                cancellationToken,
                userId,
                root,
                root).ConfigureAwait(false);
            return bound > 0;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return false;
        }
    }

    private static async Task<(int UserId, object? Failure)> SessionCsrfAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? postedCsrf,
        CancellationToken cancellationToken)
    {
        if (postedCsrf is null)
        {
            return (0, CsrfFailure("Error! CSRF 1"));
        }

        if (postedCsrf.Length == 0)
        {
            return (0, CsrfFailure("Error! CSRF 3"));
        }

        int userId;
        try
        {
            userId = await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return (0, CsrfFailure(SessionsMissing));
        }

        if (userId == 0)
        {
            return (0, CsrfFailure("Error! CSRF 3.1"));
        }

        var stored = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            session ?? string.Empty,
            userId).ConfigureAwait(false);
        if (!string.Equals(stored ?? string.Empty, postedCsrf, StringComparison.Ordinal))
        {
            return (0, CsrfFailure("Error! CSRF 4"));
        }

        return (userId, null);
    }

    private static async Task<bool> BulkCsrfMatchesAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        string? adminSession,
        string? adminUser,
        string posted,
        CancellationToken cancellationToken)
    {
        if (int.TryParse(userCookie, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            var userKey = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                session ?? string.Empty,
                userId).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(userKey) && string.Equals(userKey, posted, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (int.TryParse(adminUser, NumberStyles.Integer, CultureInfo.InvariantCulture, out var adminId))
        {
            var adminKey = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `csrf_guard_key` FROM `sessions` WHERE `session` = ? AND `user_id` = ? AND `type` = 1 LIMIT 1"),
                cancellationToken,
                adminSession ?? string.Empty,
                adminId).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(adminKey) && string.Equals(adminKey, posted, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<(int Id, string? Missing)> CustomerGroupAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `user_id` = ?"), cancellationToken, userId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return (0, UserAccountsMissing);
        }

        try
        {
            await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `users_profiles` WHERE `user_id` = ?"), cancellationToken, userId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return (0, UserProfilesMissing);
        }

        long bound;
        try
        {
            bound = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return (0, CustomerGroupsMissing);
        }

        if (bound > 0)
        {
            return ((int)bound, null);
        }

        try
        {
            var registered = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            return ((int)registered, null);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return (0, CustomerGroupsMissing);
        }
    }

    private static async Task<List<int>> CustomerOfficesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var geo = await ErpDb.LongAsync(connection, null, "SELECT MIN(`id`) FROM `shop_geo`", cancellationToken).ConfigureAwait(false);
        var offices = new List<int>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `office_id` FROM `shop_offices_geo_map` WHERE `geo_id` = ?");
            ErpDb.AddParameters(command, geo);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                offices.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        if (offices.Count == 0)
        {
            var first = await ErpDb.LongAsync(connection, null, "SELECT `id` FROM `shop_offices` ORDER BY `id` LIMIT 1", cancellationToken).ConfigureAwait(false);
            if (first > 0)
            {
                offices.Add((int)first);
            }
        }

        return offices;
    }

    private static bool PhoneTail(string stored, string posted)
    {
        var left = Digits(stored);
        var right = Digits(posted);
        if (left.Length == 0 || right.Length == 0)
        {
            return true;
        }

        var leftTail = left.Length <= 7 ? left : left[^7..];
        var rightTail = right.Length <= 7 ? right : right[^7..];
        return string.Equals(leftTail, rightTail, StringComparison.Ordinal);
    }

    private static string Digits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static bool IsJsonObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string FieldText(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;

    private static string StripTags(string value)
        => Regex.Replace(value ?? string.Empty, "<[^>]*>", string.Empty);

    private static string HtmlCompat(string? value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static int ItemCount(string raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static int ParseId(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static long UnixNow()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static ReturnLoadBody ReturnError(string message)
        => new(false, null, message);

    public readonly record struct ReturnLine(int ItemId, int ReasonId, string Comment, string Count);

    private readonly record struct CreatedJob(int Id, string JobNo, string Status, string Plate, string Name);

    public sealed record FlagBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message);

    public sealed record ReturnLoadBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("data")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Data,
        [property: JsonPropertyName("error_message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? ErrorMessage);

    public sealed record WorkshopBookBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("job_no")] string JobNo,
        [property: JsonPropertyName("job_id")] int JobId);

    public sealed record WorkshopTrackBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("job")] WorkshopTrackJob Job);

    public sealed record WorkshopTrackJob(
        [property: JsonPropertyName("job_no")] string JobNo,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("status_label")] string StatusLabel,
        [property: JsonPropertyName("plate")] string Plate,
        [property: JsonPropertyName("customer_name")] string CustomerName);

    public sealed record GarageJobBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("job")] GarageJobCard Job);

    public sealed record GarageJobCard(
        [property: JsonPropertyName("header")] GarageJobHeader Header);

    public sealed record GarageJobHeader(
        [property: JsonPropertyName("job_no")] string JobNo,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("plate")] string Plate,
        [property: JsonPropertyName("customer_name")] string CustomerName);

    public sealed record LoginCodeBody(
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message);

    public sealed record UCatalogAnswer(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("json")] string Json,
        [property: JsonPropertyName("breadcrumbs")] string? Breadcrumbs,
        [property: JsonPropertyName("request_object")] UCatalogRequest? Request);

    public sealed record UCatalogRequest(
        [property: JsonPropertyName("action")] string Action);
}
