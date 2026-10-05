using System.Data.Common;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string BuyerProfilesMissing = "Buyer profiles are not in this database.";
    public const string CustomerProfilesMissing = "Customer profiles are not in this database.";
    public const string EinvoiceNotPosted = "E-invoice was not posted";

    public static Task<object> CrmEndpointGateAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? method,
        CancellationToken cancellationToken)
        => DeskGateAsync(connection, adminSession, adminUser, null, method, string.Empty, false, (_, _) => Task.FromResult<object>(new FlagBody(false, "No action")), cancellationToken);

    public static Task<object> CustomerMgmtAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? method,
        string? action,
        int userId,
        string? buyerName,
        string? company,
        string? address,
        string? city,
        string? phone,
        string? email,
        string? trn,
        string? countryCode,
        string? amountText,
        IErpEinvoiceProfileWriteService buyers,
        IErpCashWriteService cash,
        CancellationToken cancellationToken)
        => DeskGateAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            method,
            action,
            true,
            (adminId, token) => CustomerActionAsync(
                connection, action, userId, buyerName, company, address, city, phone, email, trn, countryCode, amountText, adminId, buyers, cash, token),
            cancellationToken);

    public static Task<object> DocumentControlAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? method,
        string? action,
        int expectedVersion,
        string? legalName,
        string? templateCode,
        string? templateTitle,
        IReadOnlySet<string> posted,
        ICpDocumentControlWriteService documents,
        CancellationToken cancellationToken)
        => DeskGateAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            method,
            action,
            true,
            (_, token) => DocumentActionAsync(action, expectedVersion, legalName, templateCode, templateTitle, posted, documents, token),
            cancellationToken);

    private static async Task<object> DeskGateAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? method,
        string? action,
        bool requireCsrf,
        Func<int, CancellationToken, Task<object>> body,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        if (!HttpMethods.IsPost(method ?? string.Empty) || string.IsNullOrWhiteSpace(action))
        {
            return new FlagBody(false, "No action");
        }

        if (!requireCsrf)
        {
            return await body(ParseId(adminUser), cancellationToken).ConfigureAwait(false);
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Access denied"),
            (adminId, token) => body(adminId, token),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> CustomerActionAsync(
        DbConnection connection,
        string? action,
        int userId,
        string? buyerName,
        string? company,
        string? address,
        string? city,
        string? phone,
        string? email,
        string? trn,
        string? countryCode,
        string? amountText,
        int adminId,
        IErpEinvoiceProfileWriteService buyers,
        IErpCashWriteService cash,
        CancellationToken cancellationToken)
    {
        switch ((action ?? string.Empty).Trim())
        {
            case "save_customer":
                return await SaveCustomerAsync(connection, userId, buyerName, company, address, city, phone, email, trn, countryCode, buyers, cancellationToken).ConfigureAwait(false);
            case "customer_advance":
                return await CustomerAdvanceAsync(userId, amountText, adminId, cash, cancellationToken).ConfigureAwait(false);
            case "einvoice_create":
                return new FlagBody(false, EinvoiceNotPosted);
            default:
                return new FlagBody(false, "Unknown action");
        }
    }

    private static async Task<object> SaveCustomerAsync(
        DbConnection connection,
        int userId,
        string? buyerName,
        string? company,
        string? address,
        string? city,
        string? phone,
        string? email,
        string? trn,
        string? countryCode,
        IErpEinvoiceProfileWriteService buyers,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return new FlagBody(false, "Invalid customer");
        }

        ErpSimpleWriteResult buyer;
        try
        {
            buyer = await buyers.SaveBuyerAsync(
                new ErpEinvoiceBuyerWriteRequest(userId, buyerName, trn, null, null, null, null, address, city, null, countryCode, phone, email),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, BuyerProfilesMissing);
        }

        if (!buyer.Succeeded)
        {
            return new FlagBody(false, buyer.Message);
        }

        try
        {
            await UpsertProfileAsync(connection, userId, "company", (company ?? string.Empty).Trim(), cancellationToken).ConfigureAwait(false);
            await UpsertProfileAsync(connection, userId, "address", (address ?? string.Empty).Trim(), cancellationToken).ConfigureAwait(false);
            await UpsertProfileAsync(connection, userId, "city", (city ?? string.Empty).Trim(), cancellationToken).ConfigureAwait(false);
            await UpsertProfileAsync(connection, userId, "phone", (phone ?? string.Empty).Trim(), cancellationToken).ConfigureAwait(false);
            if (countryCode is not null)
            {
                await UpsertProfileAsync(connection, userId, "epc_reg_country", countryCode.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
            }

            if (trn is not null)
            {
                await UpsertProfileAsync(connection, userId, "epc_reg_trn", Regex.Replace(trn, "\\D", string.Empty), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, CustomerProfilesMissing);
        }

        return new FlagBody(true, "Customer profile saved");
    }

    private static async Task UpsertProfileAsync(DbConnection connection, int userId, string key, string value, CancellationToken cancellationToken)
    {
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1"),
            cancellationToken,
            userId,
            key).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `users_profiles` SET `data_value` = ? WHERE `id` = ?"),
                cancellationToken,
                value,
                id).ConfigureAwait(false);
            return;
        }

        if (value.Length == 0)
        {
            return;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"),
            cancellationToken,
            userId,
            key,
            value).ConfigureAwait(false);
    }

    private static async Task<object> CustomerAdvanceAsync(
        int userId,
        string? amountText,
        int adminId,
        IErpCashWriteService cash,
        CancellationToken cancellationToken)
    {
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            amount = 0m;
        }

        try
        {
            var ledgerId = await cash.CustomerSettlementAsync(
                new ErpCustomerSettlementInput
                {
                    UserId = userId,
                    Amount = amount,
                    Income = true,
                    EntryKind = "advance",
                    PostGl = false,
                },
                adminId,
                cancellationToken).ConfigureAwait(false);
            return new JsonObject
            {
                ["status"] = true,
                ["message"] = "Customer advance recorded",
                ["ledger_id"] = ledgerId,
            };
        }
        catch (ErpWriteException ex)
        {
            return new FlagBody(false, ex.Message);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, CustomerAccountingMissing);
        }
    }

    private static async Task<object> DocumentActionAsync(
        string? action,
        int expectedVersion,
        string? legalName,
        string? templateCode,
        string? templateTitle,
        IReadOnlySet<string> posted,
        ICpDocumentControlWriteService documents,
        CancellationToken cancellationToken)
    {
        switch ((action ?? string.Empty).Trim())
        {
            case "save_company":
            {
                var written = await documents.SaveCompanyAsync(
                    new CpDocumentCompanySaveRequest(
                        expectedVersion,
                        legalName,
                        null, null, null, null, null, null, null, null, null, null, null, null, null,
                        posted),
                    cancellationToken).ConfigureAwait(false);
                return new FlagBody(written.Succeeded, written.Succeeded ? "Company profile saved" : written.Message);
            }

            case "save_template":
            {
                var written = await documents.SaveTemplateAsync(
                    new CpDocumentTemplateSaveRequest(templateCode, templateTitle, null, null, null, null, null, true, posted),
                    cancellationToken).ConfigureAwait(false);
                return new FlagBody(written.Succeeded, written.Succeeded ? "Template saved" : written.Message);
            }

            case "upload_logo":
                return new FlagBody(false, "No logo file");
            case "upload_attachment":
            case "delete_attachment":
                return new FlagBody(false, "Document attachments stay on the classic folder.");
            case "sync_einvoice_seller":
                return new FlagBody(false, "Seller import stays on the e-invoice profile.");
            default:
                return new FlagBody(false, "Unknown action");
        }
    }
}
