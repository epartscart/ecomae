using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP <c>content/shop/vendor/vendor_register.php</c> account INSERT.
/// Auto-approve reuses <see cref="ICpVendorApprovalWriteService"/> (group bind + warehouse).
/// Activation e-mail / SMS stay uninvented. Schema-ensure for missing tables stays Classic.
/// </summary>
public interface IStorefrontVendorRegisterWriteService
{
    Task<StorefrontVendorRegisterWriteResult> RegisterAsync(
        StorefrontVendorRegisterWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontVendorRegisterWriteRequest(
    string? Email,
    string? Password,
    string? Password2,
    string? ContactName,
    string? ContactJobTitle,
    string? Phone,
    string? BillingEmail,
    string? VendorFull,
    string? VendorShort,
    string? LegalName,
    bool VatRegistered,
    string? Trn,
    string? LegalRegNo,
    string? LegalRegType,
    string? AuthorityName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Emirate,
    string? PostalCode,
    string? CountryCode);

public sealed record StorefrontVendorRegisterWriteResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    long UserId,
    long AccountId,
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
        would_write = Ok && Writes > 0,
        user_id = UserId,
        account_id = AccountId,
        message = Message,
        note = Message,
        session
    };
}

public sealed class StorefrontVendorRegisterWriteService : IStorefrontVendorRegisterWriteService
{
    private static readonly HashSet<string> LegalRegTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TL", "EID", "PAS", "CD",
    };

    private static readonly HashSet<string> Emirates = new(StringComparer.Ordinal)
    {
        "Dubai", "Abu Dhabi", "Sharjah", "Ajman", "Umm Al Quwain", "Ras Al Khaimah", "Fujairah",
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpVendorApprovalWriteService _approvals;
    private readonly EcomAeOptions _options;

    public StorefrontVendorRegisterWriteService(
        IErpWriteConnectionFactory connections,
        ICpVendorApprovalWriteService approvals,
        IOptions<EcomAeOptions> options)
    {
        _connections = connections;
        _approvals = approvals;
        _options = options.Value;
    }

    public static string NormalizeEmail(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizeText(string? value)
        => (value ?? string.Empty).Trim();

    public static string DigitsOnly(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool LooksLikeEmail(string contact)
        => contact.Contains('@', StringComparison.Ordinal)
           && contact.IndexOf('@') > 0
           && contact.IndexOf('@') < contact.Length - 1;

    public static bool LooksLikeTrn(string digits)
        => digits.Length == 15;

    public static string TinFromTrn(string digits)
        => digits.Length >= 10 ? digits[..10] : digits;

    public static string PeppolFromTin(string tin)
        => tin.Length == 0 ? string.Empty : "0235:" + tin;

    public static string NormalizeLegalRegType(string? raw)
    {
        var code = NormalizeText(raw).ToUpperInvariant();
        return LegalRegTypes.Contains(code) ? code : "TL";
    }

    public async Task<StorefrontVendorRegisterWriteResult> RegisterAsync(
        StorefrontVendorRegisterWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = NormalizeEmail(request.Email);
        var password = request.Password ?? string.Empty;
        var password2 = request.Password2 ?? string.Empty;
        var contact = NormalizeText(request.ContactName);
        var jobTitle = Clip(NormalizeText(request.ContactJobTitle), 120);
        var phone = Clip(NormalizeText(request.Phone), 64);
        var billingEmail = NormalizeEmail(request.BillingEmail);
        var vendorFull = CpVendorApprovalWriteService.SanitizeFull(request.VendorFull);
        var vendorShort = CpVendorApprovalWriteService.SanitizeShort(request.VendorShort);
        var legalName = Clip(NormalizeText(request.LegalName), 255);
        var vatRegistered = request.VatRegistered;
        var trn = DigitsOnly(request.Trn);
        var legalRegNo = Clip(NormalizeText(request.LegalRegNo), 64);
        var legalRegType = NormalizeLegalRegType(request.LegalRegType);
        var authority = Clip(NormalizeText(request.AuthorityName), 255);
        var address1 = Clip(NormalizeText(request.AddressLine1), 255);
        var address2 = Clip(NormalizeText(request.AddressLine2), 255);
        var city = Clip(NormalizeText(request.City), 120);
        var emirate = NormalizeText(request.Emirate);
        var postal = Clip(NormalizeText(request.PostalCode), 32);
        var country = Clip(NormalizeText(request.CountryCode).ToUpperInvariant(), 8);
        if (country.Length == 0)
        {
            country = "AE";
        }

        if (authority.Length == 0 && emirate.Length > 0)
        {
            authority = Clip(PhpVendorPortal.AuthorityFor(emirate), 255);
        }

        if (email.Length == 0 || !LooksLikeEmail(email))
        {
            return Fail("invalid", "Enter a valid login email address.");
        }

        if (password.Length < 6)
        {
            return Fail("password", "Password must be at least 6 characters.");
        }

        if (!string.Equals(password, password2, StringComparison.Ordinal))
        {
            return Fail("password", "Passwords do not match.");
        }

        if (contact.Length == 0)
        {
            return Fail("invalid", "Contact person name is required.");
        }

        if (phone.Length == 0)
        {
            return Fail("invalid", "Phone / mobile is required.");
        }

        if (billingEmail.Length > 0 && !LooksLikeEmail(billingEmail))
        {
            return Fail("invalid", "Billing email is invalid.");
        }

        if (vendorFull.Length == 0)
        {
            return Fail("invalid", "Trade / storefront name is required.");
        }

        if (vendorShort.Length == 0)
        {
            return Fail("invalid", "Vendor short code is invalid.");
        }

        if (legalName.Length == 0)
        {
            return Fail("invalid", "Legal entity name (as on trade licence) is required for UAE e-invoicing.");
        }

        if (legalRegNo.Length == 0)
        {
            return Fail("invalid", "Trade licence / legal registration number is required.");
        }

        if (address1.Length == 0)
        {
            return Fail("invalid", "Business address line 1 is required.");
        }

        if (city.Length == 0)
        {
            return Fail("invalid", "City is required.");
        }

        if (country == "AE")
        {
            if (emirate.Length == 0 || !Emirates.Contains(emirate))
            {
                return Fail("invalid", "Select a UAE emirate (country subdivision) for e-invoicing.");
            }

            if (authority.Length == 0)
            {
                return Fail("invalid", "Licensing authority name is required.");
            }

            if (vatRegistered && !LooksLikeTrn(trn))
            {
                return Fail("trn", "UAE TRN must be exactly 15 digits (FTA tax registration number).");
            }

            if (!vatRegistered && trn.Length > 0 && !LooksLikeTrn(trn))
            {
                return Fail("trn", "If provided, TRN must be exactly 15 digits.");
            }
        }
        else if (trn.Length > 0 && !LooksLikeTrn(trn))
        {
            return Fail("trn", "If provided, TRN must be exactly 15 digits.");
        }

        if (string.IsNullOrWhiteSpace(_options.SecretSuccession))
        {
            return Fail("config", "Registration is not configured.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Account database is not configured.");
        }

        var tin = TinFromTrn(trn);
        var peppol = PeppolFromTin(tin);
        var hash = LegacyPasswordVerifier.Md5Hex(password + _options.SecretSuccession);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false)
                || !await TableExistsAsync(connection, "users_groups_bind", cancellationToken).ConfigureAwait(false)
                || !await TableExistsAsync(connection, "groups", cancellationToken).ConfigureAwait(false)
                || !await TableExistsAsync(connection, "epc_vendor_accounts", cancellationToken).ConfigureAwait(false))
            {
                return Fail("schema", "Vendor tables are not provisioned. Schema ensure stays on the Classic twin.");
            }

            var emailTaken = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `email` = ?"),
                cancellationToken,
                email).ConfigureAwait(false);
            if (emailTaken > 0)
            {
                return Fail("exists", "This email is already registered. Sign in instead.");
            }

            var shortTaken = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_vendor_accounts` WHERE UPPER(`vendor_short`) = UPPER(?)"),
                cancellationToken,
                vendorShort).ConfigureAwait(false);
            if (shortTaken > 0)
            {
                return Fail("exists", "That vendor short code is already taken. Choose another.");
            }

            if (trn.Length > 0)
            {
                var trnTaken = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_vendor_accounts` WHERE `trn` = ? AND `trn` <> ''"),
                    cancellationToken,
                    trn).ConfigureAwait(false);
                if (trnTaken > 0)
                {
                    return Fail("exists", "This TRN is already registered to another vendor.");
                }
            }

            var variant = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `reg_variants` ORDER BY `order` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (variant <= 0)
            {
                variant = 1;
            }

            var retailGroup = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);

            var vendorGroup = await EnsureVendorGroupAsync(connection, cancellationToken).ConfigureAwait(false);
            if (vendorGroup <= 0)
            {
                return Fail("group", "Vendor group EPC_VENDOR is missing.");
            }

            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            long userId;
            long accountId;
            var writes = 0;
            try
            {
                writes += await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional(
                        "INSERT INTO `users` (`email`, `reg_variant`, `password`, `email_confirmed`, `time_registered`, `unlocked`) VALUES (?, ?, ?, 1, ?, 1)"),
                    cancellationToken,
                    email,
                    variant,
                    hash,
                    now).ConfigureAwait(false);
                userId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                if (writes <= 0 || userId <= 0)
                {
                    throw new ErpWriteException("Could not create account. Try again.");
                }

                if (retailGroup > 0)
                {
                    writes += await ErpDb.ExecuteAsync(
                        connection,
                        tx,
                        ErpDb.Positional("INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"),
                        cancellationToken,
                        userId,
                        retailGroup).ConfigureAwait(false);
                }

                writes += await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"),
                    cancellationToken,
                    userId,
                    vendorGroup).ConfigureAwait(false);

                writes += await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_vendor_accounts`
                        (`user_id`, `storage_id`, `vendor_full`, `vendor_short`,
                         `legal_name`, `trn`, `vat_registered`, `tin`, `peppol_endpoint`,
                         `legal_reg_no`, `legal_reg_type`, `authority_name`,
                         `address_line1`, `address_line2`, `city`, `emirate`, `postal_code`, `country_code`,
                         `contact_name`, `contact_job_title`, `phone`, `billing_email`,
                         `status`, `created_at`, `updated_at`)
                        VALUES (?, 0, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'pending', ?, ?)
                        """),
                    cancellationToken,
                    userId,
                    vendorFull,
                    vendorShort,
                    legalName,
                    trn,
                    vatRegistered ? 1 : 0,
                    tin,
                    peppol,
                    legalRegNo,
                    legalRegType,
                    authority,
                    address1,
                    address2,
                    city,
                    emirate,
                    postal,
                    country,
                    contact,
                    jobTitle,
                    phone,
                    billingEmail,
                    now,
                    now).ConfigureAwait(false);
                accountId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                if (accountId <= 0)
                {
                    throw new ErpWriteException("Could not create vendor account. Try again.");
                }

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }

            try
            {
                var approved = await _approvals.SetStatusAsync(accountId, "approve", 0, cancellationToken).ConfigureAwait(false);
                if (approved.Succeeded)
                {
                    writes += approved.Writes;
                }
            }
            catch
            {
                // PHP still returns success when warehouse provision fails.
            }

            return new StorefrontVendorRegisterWriteResult(
                true,
                "ok",
                "ok",
                "Vendor account created for " + vendorShort + ". Sign in to upload your price list. This page does not invent a send.",
                userId,
                accountId,
                writes);
        }
        catch (Exception ex)
        {
            return Fail("db", ex.Message);
        }
    }

    private static async Task<long> EnsureVendorGroupAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1"),
            cancellationToken,
            CpVendorApprovalWriteService.VendorGroupKey).ConfigureAwait(false);
        if (existing > 0)
        {
            return existing;
        }

        var maxId = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT IFNULL(MAX(`id`), 0) FROM `groups`",
            cancellationToken).ConfigureAwait(false);
        var id = maxId + 1;
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `groups`
                    (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`)
                    VALUES (?, ?, 0, 2, 1, 1, 0, 0, 0, 0, ?, 96)
                    """),
                cancellationToken,
                id,
                CpVendorApprovalWriteService.VendorGroupKey,
                CpVendorApprovalWriteService.VendorGroupKey).ConfigureAwait(false);
            return id;
        }
        catch
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1"),
                cancellationToken,
                CpVendorApprovalWriteService.VendorGroupKey).ConfigureAwait(false);
        }
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static StorefrontVendorRegisterWriteResult Fail(string code, string message)
        => new(false, "error", code, message, 0, 0, 0);
}
