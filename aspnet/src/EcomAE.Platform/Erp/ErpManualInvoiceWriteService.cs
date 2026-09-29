using System.Data.Common;
using System.Text.Json;
using System.Xml.Linq;

namespace EcomAE.Platform.Erp;

public sealed record ErpManualInvoiceLineInput(
    string? ItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate = 5m,
    string? ItemDescription = null);

public sealed record ErpManualInvoiceWriteRequest(
    long Id,
    long UserId,
    string? InvoiceNumber,
    string? CurrencyCode,
    string? SellerJson,
    string? BuyerJson,
    string? LinesJson,
    string? PaymentTerms,
    string? DueDate,
    string? IssueDate,
    long OrderId,
    decimal PaidAmount,
    string? TransactionTypeCode,
    string? PaymentMeansCode,
    string? BankAccount,
    int AdminId);

public sealed record ErpManualInvoiceWriteResult(
    long InvoiceId,
    string InvoiceNumber,
    decimal SubtotalExVat,
    decimal TotalVat,
    decimal TotalInclVat);

public interface IErpManualInvoiceWriteService
{
    Task<ErpManualInvoiceWriteResult> SaveAsync(
        ErpManualInvoiceWriteRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Live guarded implementation of the PHP manual invoice save contract.</summary>
public sealed class ErpManualInvoiceWriteService : IErpManualInvoiceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpManualInvoiceWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _vouchers = vouchers;
    }

    public async Task<ErpManualInvoiceWriteResult> SaveAsync(
        ErpManualInvoiceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }

        var lines = ParseLines(request.LinesJson);
        if (lines.Count == 0)
        {
            throw new ErpWriteException("Add at least one line item");
        }
        ValidateLines(lines);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var sellerJson = await ResolveSellerJsonAsync(connection, request.SellerJson, cancellationToken).ConfigureAwait(false);
        var buyerJson = await ResolveBuyerJsonAsync(connection, request.UserId, request.BuyerJson, cancellationToken).ConfigureAwait(false);
        if (request.Id > 0)
        {
            return await UpdateAsync(connection, request, lines, sellerJson, buyerJson, cancellationToken).ConfigureAwait(false);
        }

        var invoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber)
            ? await _vouchers.NextAsync(connection, null, "SI", cancellationToken).ConfigureAwait(false)
            : request.InvoiceNumber.Trim();
        var currency = Clip(request.CurrencyCode, 8);
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var subtotal = Round(lines.Sum(line => line.Quantity * line.UnitPrice));
        var totalVat = Round(lines.Sum(line => line.Quantity * line.UnitPrice * line.TaxRate / 100m));
        var totalIncl = Round(subtotal + totalVat);
        var paidAmount = Round(Math.Max(0, request.PaidAmount));
        var amountDue = Round(Math.Max(0, totalIncl - paidAmount));
        var issueDate = ParseDate(request.IssueDate, now);
        var dueDate = ParseDate(request.DueDate, issueDate + 30 * 86400L);
        var validationErrors = BuildValidationErrors(
            invoiceNumber,
            issueDate,
            dueDate,
            request.TransactionTypeCode,
            request.PaymentMeansCode,
            sellerJson,
            buyerJson,
            lines);
        var validationOk = validationErrors.Count == 0;
        var documentStatus = validationOk ? "validated" : "draft";
        var validationErrorsJson = JsonSerializer.Serialize(validationErrors);
        var xmlContent = BuildInvoiceXml(
            invoiceNumber,
            issueDate,
            dueDate,
            currency,
            request.PaymentMeansCode,
            request.BankAccount,
            sellerJson,
            buyerJson,
            lines,
            subtotal,
            totalVat,
            totalIncl);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_documents` (`uuid`,`invoice_number`,`order_id`,`user_id`,`doc_category`,`invoice_type_code`,`issue_date`,`payment_due_date`,`vat_point_date`,`currency_code`,`vat_currency_code`,`transaction_type_code`,`payment_means_code`,`payment_terms`,`bank_account`,`business_process`,`specification_id`,`seller_json`,`buyer_json`,`subtotal_ex_vat`,`total_vat`,`total_incl_vat`,`paid_amount`,`rounding_amount`,`amount_due`,`tax_breakdown_json`,`status`,`validation_ok`,`validation_errors_json`,`xml_content`,`time_created`,`time_updated`,`admin_id`,`active`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,1)"),
                cancellationToken,
                Guid.NewGuid().ToString("D"),
                invoiceNumber,
                request.OrderId,
                request.UserId > 0 ? request.UserId : 0,
                "tax_invoice",
                "380",
                issueDate,
                dueDate,
                issueDate,
                currency,
                currency,
                Clip(request.TransactionTypeCode, 32) is { Length: > 0 } transactionType ? transactionType : "00000000",
                Clip(request.PaymentMeansCode, 16) is { Length: > 0 } paymentMeans ? paymentMeans : "30",
                Clip(request.PaymentTerms, 255),
                Clip(request.BankAccount, 255),
                "urn:peppol:bis:billing",
                "urn:peppol:pint:billing-1@ae-1",
                sellerJson,
                buyerJson,
                subtotal,
                totalVat,
                totalIncl,
                paidAmount,
                0m,
                amountDue,
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        tax_category = lines[0].TaxRate > 0 ? "S" : "Z",
                        taxable_amount = subtotal,
                        tax_rate = lines[0].TaxRate,
                        tax_amount = totalVat,
                    },
                }),
                documentStatus,
                validationOk ? 1 : 0,
                validationErrorsJson,
                xmlContent,
                now,
                now,
                request.AdminId).ConfigureAwait(false);
            var invoiceId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            var lineNo = 1;
            foreach (var line in lines)
            {
                var net = Round(line.Quantity * line.UnitPrice);
                var vat = Round(net * line.TaxRate / 100m);
                var gross = Round(net + vat);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_einvoice_lines` (`document_id`,`line_no`,`item_name`,`item_description`,`item_type`,`quantity`,`uom_code`,`unit_price`,`line_net`,`tax_category`,`tax_rate`,`tax_amount`,`gross_amount`,`vat_line_aed`,`line_amount_aed`) VALUES (?,?,?,?,'G',?,'C62',?,?, 'S',?,?,?,?,?)"),
                    cancellationToken,
                    invoiceId,
                    lineNo++,
                    Clip(line.ItemName, 255),
                    Clip(line.ItemDescription, 1000),
                    line.Quantity,
                    line.UnitPrice,
                    net,
                    line.TaxRate,
                    vat,
                    gross,
                    vat,
                    gross).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_events` (`document_id`,`event_type`,`status`,`message`,`payload_json`,`time_created`) VALUES (?,'created','draft',?,?,?)"),
                cancellationToken,
                invoiceId,
                "Manual invoice saved as draft",
                request.LinesJson ?? "[]",
                now).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(invoiceId, invoiceNumber, subtotal, totalVat, totalIncl);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<ErpManualInvoiceWriteResult> UpdateAsync(
        DbConnection connection,
        ErpManualInvoiceWriteRequest request,
        IReadOnlyList<ErpManualInvoiceLineInput> lines,
        string sellerJson,
        string buyerJson,
        CancellationToken cancellationToken)
    {
        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_einvoice_documents` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ErpWriteException("Invoice not found");
        }

        if (status is "submitted" or "accepted" or "queued")
        {
            throw new ErpWriteException("Submitted invoices cannot be edited — issue a credit note instead");
        }

        var invoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber)
            ? await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `invoice_number` FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                request.Id).ConfigureAwait(false) ?? ("SI-" + request.Id)
            : request.InvoiceNumber.Trim();
        var currency = Clip(request.CurrencyCode, 8);
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var subtotal = Round(lines.Sum(line => line.Quantity * line.UnitPrice));
        var totalVat = Round(lines.Sum(line => line.Quantity * line.UnitPrice * line.TaxRate / 100m));
        var totalIncl = Round(subtotal + totalVat);
        var paidAmount = Round(Math.Max(0, request.PaidAmount));
        var amountDue = Round(Math.Max(0, totalIncl - paidAmount));
        var issueDate = ParseDate(request.IssueDate, now);
        var dueDate = ParseDate(request.DueDate, issueDate + 30 * 86400L);
        var validationErrors = BuildValidationErrors(
            invoiceNumber,
            issueDate,
            dueDate,
            request.TransactionTypeCode,
            request.PaymentMeansCode,
            sellerJson,
            buyerJson,
            lines);
        var validationOk = validationErrors.Count == 0;
        var documentStatus = validationOk ? "validated" : "draft";
        var validationErrorsJson = JsonSerializer.Serialize(validationErrors);
        var xmlContent = BuildInvoiceXml(
            invoiceNumber,
            issueDate,
            dueDate,
            currency,
            request.PaymentMeansCode,
            request.BankAccount,
            sellerJson,
            buyerJson,
            lines,
            subtotal,
            totalVat,
            totalIncl);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var changed = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_einvoice_documents` SET `order_id`=?,`invoice_number`=?,`user_id`=?,`issue_date`=?,`vat_point_date`=?,`payment_due_date`=?,`currency_code`=?,`vat_currency_code`=?,`transaction_type_code`=?,`payment_means_code`=?,`payment_terms`=?,`bank_account`=?,`business_process`=?,`specification_id`=?,`seller_json`=?,`buyer_json`=?,`subtotal_ex_vat`=?,`total_vat`=?,`total_incl_vat`=?,`paid_amount`=?,`rounding_amount`=?,`amount_due`=?,`tax_breakdown_json`=?,`status`=?,`validation_ok`=?,`validation_errors_json`=?,`xml_content`=?,`time_updated`=? WHERE `id`=? AND `active`=1 AND `status` NOT IN ('submitted','accepted','queued')"),
                cancellationToken,
                request.OrderId,
                invoiceNumber,
                request.UserId > 0 ? request.UserId : 0,
                issueDate,
                issueDate,
                dueDate,
                currency,
                currency,
                Clip(request.TransactionTypeCode, 32) is { Length: > 0 } transactionType ? transactionType : "00000000",
                Clip(request.PaymentMeansCode, 16) is { Length: > 0 } paymentMeans ? paymentMeans : "30",
                Clip(request.PaymentTerms, 255),
                Clip(request.BankAccount, 255),
                "urn:peppol:bis:billing",
                "urn:peppol:pint:billing-1@ae-1",
                sellerJson,
                buyerJson,
                subtotal,
                totalVat,
                totalIncl,
                paidAmount,
                0m,
                amountDue,
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        tax_category = lines[0].TaxRate > 0 ? "S" : "Z",
                        taxable_amount = subtotal,
                        tax_rate = lines[0].TaxRate,
                        tax_amount = totalVat,
                    },
                }),
                documentStatus,
                validationOk ? 1 : 0,
                validationErrorsJson,
                xmlContent,
                now,
                request.Id).ConfigureAwait(false);
            if (changed == 0)
            {
                throw new ErpWriteException("Invoice could not be updated — another user may have submitted it.");
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_einvoice_lines` WHERE `document_id` = ?"),
                cancellationToken,
                request.Id).ConfigureAwait(false);

            var lineNo = 1;
            foreach (var line in lines)
            {
                var net = Round(line.Quantity * line.UnitPrice);
                var vat = Round(net * line.TaxRate / 100m);
                var gross = Round(net + vat);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_einvoice_lines` (`document_id`,`line_no`,`item_name`,`item_description`,`item_type`,`quantity`,`uom_code`,`unit_price`,`line_net`,`tax_category`,`tax_rate`,`tax_amount`,`gross_amount`,`vat_line_aed`,`line_amount_aed`) VALUES (?,?,?,?,'G',?,'C62',?,?, 'S',?,?,?,?,?)"),
                    cancellationToken,
                    request.Id,
                    lineNo++,
                    Clip(line.ItemName, 255),
                    Clip(line.ItemDescription, 1000),
                    line.Quantity,
                    line.UnitPrice,
                    net,
                    line.TaxRate,
                    vat,
                    gross,
                    vat,
                    gross).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_events` (`document_id`,`event_type`,`status`,`message`,`payload_json`,`time_created`) VALUES (?,'updated','draft',?,?,?)"),
                cancellationToken,
                request.Id,
                "Manual invoice updated as draft",
                request.LinesJson ?? "[]",
                now).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(request.Id, invoiceNumber, subtotal, totalVat, totalIncl);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<string> ResolveSellerJsonAsync(
        DbConnection connection,
        string? supplied,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return supplied;
        }

        var profile = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, fallback) in new[]
        {
            ("seller_name", "ePartsCart LLC"),
            ("seller_trn", ""),
            ("seller_tin", ""),
            ("seller_legal_reg_no", ""),
            ("seller_legal_reg_type", "TL"),
            ("seller_authority_name", "Dubai Economy and Tourism"),
            ("seller_address_line1", ""),
            ("seller_city", "Dubai"),
            ("seller_emirate", "Dubai"),
            ("seller_country_code", "AE"),
            ("seller_phone", ""),
            ("seller_email", ""),
            ("seller_bank_account", ""),
        })
        {
            profile[key] = await SettingAsync(connection, key, fallback, cancellationToken).ConfigureAwait(false);
        }
        profile["seller_peppol_endpoint"] = PeppolEndpoint(profile["seller_trn"]);

        return JsonSerializer.Serialize(profile);
    }

    private static async Task<string> ResolveBuyerJsonAsync(
        DbConnection connection,
        long userId,
        string? supplied,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return supplied;
        }

        var buyer = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buyer_name"] = userId > 0 ? "Customer #" + userId : "Customer #0",
            ["buyer_trn"] = "",
            ["buyer_legal_reg_no"] = "",
            ["buyer_legal_reg_type"] = "TL",
            ["buyer_address_line1"] = "United Arab Emirates",
            ["buyer_city"] = "Dubai",
            ["buyer_emirate"] = "Dubai",
            ["buyer_country_code"] = "AE",
            ["buyer_email"] = userId > 0 ? "customer" + userId + "@epartscart.local" : "customer@epartscart.local",
            ["buyer_peppol_endpoint"] = "0235:9900000098",
        };

        if (userId > 0 && userId <= int.MaxValue)
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(
                    "SELECT `buyer_name`, `trn`, `legal_reg_no`, `legal_reg_type`, `address_line1`, `city`, `emirate`, `country_code`, `email`, `peppol_endpoint`"
                    + " FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1");
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@p0";
                parameter.Value = (int)userId;
                command.Parameters.Add(parameter);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    buyer["buyer_name"] = Text(reader, 0) is { Length: > 0 } name ? name : buyer["buyer_name"];
                    buyer["buyer_trn"] = Text(reader, 1);
                    buyer["buyer_legal_reg_no"] = Text(reader, 2);
                    buyer["buyer_legal_reg_type"] = Text(reader, 3) is { Length: > 0 } type ? type : "TL";
                    buyer["buyer_address_line1"] = Text(reader, 4) is { Length: > 0 } address ? address : buyer["buyer_address_line1"];
                    buyer["buyer_city"] = Text(reader, 5) is { Length: > 0 } city ? city : buyer["buyer_city"];
                    buyer["buyer_emirate"] = Text(reader, 6) is { Length: > 0 } emirate ? emirate : buyer["buyer_emirate"];
                    buyer["buyer_country_code"] = Text(reader, 7) is { Length: > 0 } country ? country.ToUpperInvariant() : "AE";
                    buyer["buyer_email"] = Text(reader, 8);
                    buyer["buyer_peppol_endpoint"] = Text(reader, 9) is { Length: > 0 } endpoint ? endpoint : buyer["buyer_peppol_endpoint"];
                }
            }
            catch (DbException)
            {
            }
        }

        return JsonSerializer.Serialize(buyer);
    }

    private static async Task<string> SettingAsync(
        DbConnection connection,
        string key,
        string fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static string PeppolEndpoint(string trn)
    {
        var digits = new string(trn.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 10 ? "0235:" + digits[..10] : "0235:9900000098";
    }

    private static List<ErpManualInvoiceLineInput> ParseLines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ErpManualInvoiceLineInput>>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new ErpWriteException("Invalid invoice lines");
        }
    }

    private static void ValidateLines(IReadOnlyList<ErpManualInvoiceLineInput> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line.ItemName))
            {
                throw new ErpWriteException($"Line {index + 1}: item name is required");
            }

            if (line.Quantity <= 0)
            {
                throw new ErpWriteException($"Line {index + 1}: quantity must be positive");
            }

            if (line.UnitPrice < 0)
            {
                throw new ErpWriteException($"Line {index + 1}: unit price cannot be negative");
            }

            if (line.TaxRate < 0)
            {
                throw new ErpWriteException($"Line {index + 1}: tax rate cannot be negative");
            }
        }
    }

    private static List<string> BuildValidationErrors(
        string invoiceNumber,
        long issueDate,
        long dueDate,
        string? transactionTypeCode,
        string? paymentMeansCode,
        string sellerJson,
        string buyerJson,
        IReadOnlyList<ErpManualInvoiceLineInput> lines)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(invoiceNumber)) errors.Add("Invoice number is required");
        if (issueDate <= 0) errors.Add("Issue date is required");
        if (dueDate <= 0) errors.Add("Payment due date is required");
        if (string.IsNullOrWhiteSpace(transactionTypeCode)) errors.Add("Transaction type code is required");
        if (string.IsNullOrWhiteSpace(paymentMeansCode)) errors.Add("Payment means type code is required");

        CheckJsonField(errors, sellerJson, "seller_name", "Seller name");
        CheckJsonField(errors, sellerJson, "seller_legal_reg_no", "Seller legal registration identifier");
        CheckJsonField(errors, sellerJson, "seller_legal_reg_type", "Seller legal registration identifier type");
        CheckJsonField(errors, sellerJson, "seller_trn", "Seller tax identifier (TRN)");
        CheckJsonField(errors, sellerJson, "seller_address_line1", "Seller address line 1");
        CheckJsonField(errors, sellerJson, "seller_city", "Seller city");
        CheckJsonField(errors, sellerJson, "seller_emirate", "Seller country subdivision");
        CheckJsonField(errors, sellerJson, "seller_country_code", "Seller country code");
        CheckJsonField(errors, sellerJson, "seller_peppol_endpoint", "Seller electronic address (Peppol)");
        CheckJsonField(errors, buyerJson, "buyer_name", "Buyer name");
        CheckJsonField(errors, buyerJson, "buyer_address_line1", "Buyer address line 1");
        CheckJsonField(errors, buyerJson, "buyer_city", "Buyer city");
        CheckJsonField(errors, buyerJson, "buyer_emirate", "Buyer country subdivision");
        CheckJsonField(errors, buyerJson, "buyer_country_code", "Buyer country code");
        CheckJsonField(errors, buyerJson, "buyer_peppol_endpoint", "Buyer electronic address");
        for (var index = 0; index < lines.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index].ItemName))
            {
                errors.Add($"Line {index + 1}: item_name is required");
            }
        }

        return errors;
    }

    private static void CheckJsonField(List<string> errors, string json, string key, string label)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(key, out var value) ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                errors.Add(label + " is required");
            }
        }
        catch (JsonException)
        {
            errors.Add(label + " is required");
        }
    }

    private static string BuildInvoiceXml(
        string invoiceNumber,
        long issueDate,
        long dueDate,
        string currency,
        string? paymentMeansCode,
        string? bankAccount,
        string sellerJson,
        string buyerJson,
        IReadOnlyList<ErpManualInvoiceLineInput> lines,
        decimal subtotal,
        decimal totalVat,
        decimal totalIncl)
    {
        var cbc = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        var cac = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        var invoice = new XElement(
            XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2") + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cbc", cbc),
            new XAttribute(XNamespace.Xmlns + "cac", cac),
            new XElement(cbc + "CustomizationID", "urn:peppol:pint:billing-1@ae-1"),
            new XElement(cbc + "ProfileID", "urn:peppol:bis:billing"),
            new XElement(cbc + "ID", invoiceNumber),
            new XElement(cbc + "IssueDate", IsoDate(issueDate)),
            new XElement(cbc + "DueDate", IsoDate(dueDate)),
            new XElement(cbc + "InvoiceTypeCode", "380"),
            new XElement(cbc + "DocumentCurrencyCode", currency),
            new XElement(cbc + "TaxCurrencyCode", "AED"),
            BuildParty(cac, cbc, "AccountingSupplierParty", sellerJson, "seller"),
            BuildParty(cac, cbc, "AccountingCustomerParty", buyerJson, "buyer"),
            new XElement(
                cac + "PaymentMeans",
                new XElement(cbc + "PaymentMeansCode", paymentMeansCode ?? "30"),
                string.IsNullOrWhiteSpace(bankAccount)
                    ? null
                    : new XElement(
                        cac + "PayeeFinancialAccount",
                        new XElement(cbc + "ID", bankAccount))),
            new XElement(
                cac + "TaxTotal",
                new XElement(cbc + "TaxAmount", Amount(totalVat), new XAttribute("currencyID", currency))),
            new XElement(
                cac + "LegalMonetaryTotal",
                new XElement(cbc + "LineExtensionAmount", Amount(subtotal), new XAttribute("currencyID", currency)),
                new XElement(cbc + "TaxExclusiveAmount", Amount(subtotal), new XAttribute("currencyID", currency)),
                new XElement(cbc + "TaxInclusiveAmount", Amount(totalIncl), new XAttribute("currencyID", currency)),
                new XElement(cbc + "PayableAmount", Amount(totalIncl), new XAttribute("currencyID", currency))),
            lines.Select((line, index) =>
                new XElement(
                    cac + "InvoiceLine",
                    new XElement(cbc + "ID", index + 1),
                    new XElement(cbc + "InvoicedQuantity", line.Quantity, new XAttribute("unitCode", "C62")),
                    new XElement(cbc + "LineExtensionAmount", Amount(Round(line.Quantity * line.UnitPrice)), new XAttribute("currencyID", currency)),
                    new XElement(
                        cac + "Item",
                        new XElement(cbc + "Name", line.ItemName),
                        string.IsNullOrWhiteSpace(line.ItemDescription)
                            ? null
                            : new XElement(cbc + "Description", line.ItemDescription),
                        new XElement(
                            cac + "ClassifiedTaxCategory",
                            new XElement(cbc + "ID", line.TaxRate > 0 ? "S" : "Z"),
                            new XElement(cbc + "Percent", line.TaxRate))),
                    new XElement(
                        cac + "Price",
                        new XElement(cbc + "PriceAmount", Amount(line.UnitPrice), new XAttribute("currencyID", currency)))))
                .ToArray());

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), invoice).ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildParty(
        XNamespace cac,
        XNamespace cbc,
        string partyName,
        string json,
        string prefix)
    {
        return new XElement(
            cac + partyName,
            new XElement(
                cac + "Party",
                new XElement(cbc + "EndpointID", JsonText(json, prefix + "_peppol_endpoint"), new XAttribute("schemeID", "0235")),
                new XElement(
                    cac + "PartyName",
                    new XElement(cbc + "Name", JsonText(json, prefix + "_name"))),
                new XElement(
                    cac + "PostalAddress",
                    new XElement(cbc + "StreetName", JsonText(json, prefix + "_address_line1")),
                    new XElement(cbc + "CityName", JsonText(json, prefix + "_city")),
                    new XElement(cbc + "CountrySubentity", JsonText(json, prefix + "_emirate")),
                    new XElement(
                        cac + "Country",
                        new XElement(cbc + "IdentificationCode", JsonText(json, prefix + "_country_code"))))));
    }

    private static string JsonText(string json, string key)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(key, out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string IsoDate(long timestamp)
        => DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string Amount(decimal value)
        => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS `epc_einvoice_documents` (`id` int NOT NULL AUTO_INCREMENT, `uuid` char(36) NOT NULL, `invoice_number` varchar(64) NOT NULL, `order_id` int NOT NULL DEFAULT 0, `user_id` int NOT NULL DEFAULT 0, `doc_category` varchar(32) NOT NULL DEFAULT 'tax_invoice', `invoice_type_code` varchar(8) NOT NULL DEFAULT '380', `issue_date` int NOT NULL DEFAULT 0, `payment_due_date` int NOT NULL DEFAULT 0, `vat_point_date` int NOT NULL DEFAULT 0, `currency_code` varchar(8) NOT NULL DEFAULT 'AED', `vat_currency_code` varchar(8) NOT NULL DEFAULT 'AED', `transaction_type_code` varchar(32) NOT NULL DEFAULT '00000000', `payment_means_code` varchar(16) NOT NULL DEFAULT '30', `payment_terms` varchar(255) DEFAULT NULL, `bank_account` varchar(255) DEFAULT NULL, `business_process` varchar(255) NOT NULL DEFAULT 'urn:peppol:bis:billing', `specification_id` varchar(255) NOT NULL DEFAULT 'urn:peppol:pint:billing-1@ae-1', `seller_json` mediumtext, `buyer_json` mediumtext, `subtotal_ex_vat` decimal(14,2) NOT NULL DEFAULT 0, `total_vat` decimal(14,2) NOT NULL DEFAULT 0, `total_incl_vat` decimal(14,2) NOT NULL DEFAULT 0, `paid_amount` decimal(14,2) NOT NULL DEFAULT 0, `rounding_amount` decimal(14,2) NOT NULL DEFAULT 0, `amount_due` decimal(14,2) NOT NULL DEFAULT 0, `tax_breakdown_json` text, `status` varchar(32) NOT NULL DEFAULT 'draft', `validation_ok` tinyint NOT NULL DEFAULT 0, `validation_errors_json` text, `xml_content` mediumtext, `time_created` int NOT NULL DEFAULT 0, `time_updated` int NOT NULL DEFAULT 0, `admin_id` int NOT NULL DEFAULT 0, `active` tinyint NOT NULL DEFAULT 1, PRIMARY KEY (`id`), UNIQUE KEY `x_uuid` (`uuid`), UNIQUE KEY `x_invoice_no` (`invoice_number`))", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `transaction_type_code` varchar(32) NOT NULL DEFAULT '00000000'", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `payment_means_code` varchar(16) NOT NULL DEFAULT '30'", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `bank_account` varchar(255) DEFAULT NULL", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `business_process` varchar(255) NOT NULL DEFAULT 'urn:peppol:bis:billing'", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `specification_id` varchar(255) NOT NULL DEFAULT 'urn:peppol:pint:billing-1@ae-1'", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `rounding_amount` decimal(14,2) NOT NULL DEFAULT 0", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_documents` ADD COLUMN `tax_breakdown_json` text", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS `epc_einvoice_lines` (`id` int NOT NULL AUTO_INCREMENT, `document_id` int NOT NULL, `line_no` int NOT NULL DEFAULT 1, `item_name` varchar(255) NOT NULL, `item_description` text, `item_type` varchar(4) NOT NULL DEFAULT 'G', `quantity` decimal(14,4) NOT NULL DEFAULT 0, `uom_code` varchar(16) NOT NULL DEFAULT 'C62', `unit_price` decimal(14,4) NOT NULL DEFAULT 0, `line_net` decimal(14,2) NOT NULL DEFAULT 0, `tax_category` varchar(8) NOT NULL DEFAULT 'S', `tax_rate` decimal(5,2) NOT NULL DEFAULT 5, `tax_amount` decimal(14,2) NOT NULL DEFAULT 0, `gross_amount` decimal(14,2) NOT NULL DEFAULT 0, `vat_line_aed` decimal(14,2) NOT NULL DEFAULT 0, `line_amount_aed` decimal(14,2) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_doc` (`document_id`))", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_einvoice_lines` ADD COLUMN `item_description` text", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS `epc_einvoice_events` (`id` int NOT NULL AUTO_INCREMENT, `document_id` int NOT NULL, `event_type` varchar(32) NOT NULL, `status` varchar(32) NOT NULL DEFAULT 'info', `message` text, `payload_json` mediumtext, `time_created` int NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_doc` (`document_id`,`time_created`))", cancellationToken);
    }

    private static long ParseDate(string? value, long fallback)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed.ToUnixTimeSeconds() : fallback;

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
