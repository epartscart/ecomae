using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public sealed record ErpSalesInvoiceLine(
    int LineNo,
    string ItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineNet,
    string TaxCategory,
    decimal TaxRate,
    decimal TaxAmount,
    decimal GrossAmount);

public sealed record ErpSoToInvoiceResult(
    long SalesOrderId,
    long SalesInvoiceId,
    string InvoiceNumber,
    decimal SubtotalExVat,
    decimal TotalVat,
    decimal TotalInclVat,
    long LedgerId);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_so_convert_to_invoice</c> → <c>epc_erp_invoice_save</c> →
/// <c>epc_einvoice_save_document</c> (<c>content/shop/finance/epc_erp_vouchers.php</c>,
/// <c>epc_erp_invoices.php</c>, <c>epc_einvoice.php</c>): same guards, line copy, per-line tenant tax,
/// SI numbering, document/line/event persistence, AR settlement and status transition.
/// The PINT XML payload is left empty; the PHP export handler rebuilds and caches it on first download.
/// </summary>
public interface IErpSalesInvoiceWriteService
{
    Task<ErpSoToInvoiceResult> ConvertSalesOrderAsync(long salesOrderId, int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpSalesInvoiceWriteService : IErpSalesInvoiceWriteService
{
    private const string BusinessProcess = "urn:peppol:bis:billing";
    private const string SpecificationId = "urn:peppol:pint:billing-1@ae-1";
    private const string ElectronicScheme = "0235";
    private const string EndpointNotOnboarded = "0235:9900000098";
    private const string InvoiceTypeTax = "380";

    private static readonly string[] ConvertibleStatuses = ["draft", "confirmed"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly IErpCashWriteService _cash;
    private readonly IErpAuditLogWriter _audit;

    public ErpSalesInvoiceWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers,
        IErpTaxAmountCalculator tax,
        IErpCashWriteService cash,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _vouchers = vouchers;
        _tax = tax;
        _cash = cash;
        _audit = audit;
    }

    public async Task<ErpSoToInvoiceResult> ConvertSalesOrderAsync(long salesOrderId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }

        if (salesOrderId <= 0)
        {
            throw new ErpWriteException("Sales order not found");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var order = await LoadSalesOrderAsync(connection, salesOrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Sales order not found");
        if (order.SalesInvoiceId > 0)
        {
            throw new ErpWriteException("Sales order already invoiced");
        }

        if (!ConvertibleStatuses.Contains(order.Status, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Only draft or confirmed sales orders can be invoiced");
        }

        var lines = await BuildLinesAsync(connection, order, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            throw new ErpWriteException("Add at least one line item");
        }

        var seller = await LoadSellerProfileAsync(connection, cancellationToken).ConfigureAwait(false);
        var buyer = await LoadBuyerProfileAsync(connection, order.CustomerUserId, cancellationToken).ConfigureAwait(false);

        var subtotal = ErpTaxAmountCalculator.Round2(lines.Sum(line => line.LineNet));
        var totalVat = ErpTaxAmountCalculator.Round2(lines.Sum(line => line.TaxAmount));
        var totalIncl = ErpTaxAmountCalculator.Round2(subtotal + totalVat);

        var invoiceNumber = await _vouchers.NextAsync(connection, null, "SI", cancellationToken).ConfigureAwait(false);
        var issueDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dueDate = issueDate + (30 * 86400);

        var errors = ValidateTaxInvoice(invoiceNumber, seller, buyer, lines, totalVat);
        if (errors.Count > 0)
        {
            throw new ErpWriteException("Tax invoice validation failed: " + string.Join("; ", errors));
        }

        var paymentTerms = await SettingAsync(connection, "payment_terms", "Within 7 days", cancellationToken).ConfigureAwait(false);
        var paymentMeans = await SettingAsync(connection, "payment_means_code", "30", cancellationToken).ConfigureAwait(false);
        var bankAccount = await SettingAsync(connection, "seller_bank_account", string.Empty, cancellationToken).ConfigureAwait(false);
        var taxBreakdown = BuildTaxBreakdown(lines, subtotal, totalVat);

        long invoiceId;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_documents` (`uuid`, `invoice_number`, `order_id`, `user_id`, `sales_order_id`,"
                    + " `doc_category`, `invoice_type_code`, `issue_date`, `payment_due_date`, `vat_point_date`, `currency_code`,"
                    + " `vat_currency_code`, `transaction_type_code`, `payment_means_code`, `payment_terms`, `bank_account`,"
                    + " `seller_json`, `buyer_json`, `subtotal_ex_vat`, `total_vat`, `total_incl_vat`, `paid_amount`,"
                    + " `rounding_amount`, `amount_due`, `tax_breakdown_json`, `status`, `validation_ok`, `validation_errors_json`,"
                    + " `xml_content`, `time_created`, `time_updated`, `admin_id`)"
                    + " VALUES (?,?,?,?,?,'tax_invoice',?,?,?,?,'AED','AED','00000000',?,?,?,?,?,?,?,?,0,0,?,?,'validated',1,'[]','',?,?,?)"),
                cancellationToken,
                Guid.NewGuid().ToString("D"),
                invoiceNumber,
                order.ShopOrderId,
                order.CustomerUserId,
                salesOrderId,
                InvoiceTypeTax,
                issueDate,
                dueDate,
                issueDate,
                paymentMeans,
                paymentTerms,
                bankAccount,
                JsonSerializer.Serialize(seller),
                JsonSerializer.Serialize(buyer),
                subtotal,
                totalVat,
                totalIncl,
                totalIncl,
                taxBreakdown,
                issueDate,
                issueDate,
                adminId).ConfigureAwait(false);
            invoiceId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            foreach (var line in lines)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_einvoice_lines` (`document_id`, `line_no`, `item_name`, `item_description`, `item_type`,"
                        + " `quantity`, `uom_code`, `unit_price`, `line_net`, `tax_category`, `tax_rate`, `tax_amount`,"
                        + " `gross_amount`, `vat_line_aed`, `line_amount_aed`) VALUES (?,?,?,'','G',?,'C62',?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    invoiceId,
                    line.LineNo,
                    line.ItemName,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineNet,
                    line.TaxCategory,
                    line.TaxRate,
                    line.TaxAmount,
                    line.GrossAmount,
                    line.TaxAmount,
                    line.GrossAmount).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_events` (`document_id`, `event_type`, `status`, `message`, `payload_json`,"
                    + " `time_created`) VALUES (?, 'created', 'validated', ?, ?, ?)"),
                cancellationToken,
                invoiceId,
                "Document validated against mandatory fields",
                JsonSerializer.Serialize(new Dictionary<string, string> { ["sales_order"] = order.SoNo }),
                issueDate).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_erp_sales_orders` SET `status` = 'invoiced', `sales_invoice_id` = ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                invoiceId,
                issueDate,
                salesOrderId).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var ledgerId = await _cash.CustomerSettlementAsync(
            connection,
            new ErpCustomerSettlementInput
            {
                UserId = order.CustomerUserId,
                Amount = ErpTaxAmountCalculator.Round2(order.TotalAmount > 0m ? order.TotalAmount : totalIncl),
                Income = true,
                EntryKind = "adjustment",
                Reference = invoiceNumber,
                Note = "Sales invoice from SO " + order.SoNo,
                Time = issueDate,
                PostGl = true,
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "so_to_invoice",
            "sales_order",
            salesOrderId,
            "Converted to sales invoice",
            new Dictionary<string, string?>
            {
                ["invoice_id"] = invoiceId.ToString(CultureInfo.InvariantCulture),
                ["si_no"] = invoiceNumber,
            },
            cancellationToken).ConfigureAwait(false);

        return new ErpSoToInvoiceResult(salesOrderId, invoiceId, invoiceNumber, subtotal, totalVat, totalIncl, ledgerId);
    }

    /// <summary>
    /// Port of PHP <c>epc_einvoice_validate_document</c> (tax-invoice mode) plus the seller-side
    /// checks of <c>epc_uae_vat_apply_to_voucher('einvoice')</c>. Registration-number rules follow the
    /// tenant's own registered country, so non-AE tenants are not held to the FTA 15-digit TRN format.
    /// </summary>
    public static List<string> ValidateTaxInvoice(
        string invoiceNumber,
        IReadOnlyDictionary<string, string> seller,
        IReadOnlyDictionary<string, string> buyer,
        IReadOnlyList<ErpSalesInvoiceLine> lines,
        decimal totalVat)
    {
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentNullException.ThrowIfNull(buyer);
        ArgumentNullException.ThrowIfNull(lines);

        var errors = new List<string>();
        if (invoiceNumber.Trim().Length == 0)
        {
            errors.Add("Invoice number is required");
        }

        foreach (var (key, label) in new[]
        {
            ("seller_name", "Seller name"),
            ("seller_legal_reg_no", "Seller legal registration identifier"),
            ("seller_legal_reg_type", "Seller legal registration identifier type"),
            ("seller_address_line1", "Seller address line 1"),
            ("seller_city", "Seller city"),
            ("seller_emirate", "Seller country subdivision"),
            ("seller_country_code", "Seller country code"),
            ("seller_peppol_endpoint", "Seller electronic address (Peppol)"),
        })
        {
            if (Value(seller, key).Length == 0)
            {
                errors.Add(label + " is required");
            }
        }

        foreach (var (key, label) in new[]
        {
            ("buyer_name", "Buyer name"),
            ("buyer_address_line1", "Buyer address line 1"),
            ("buyer_city", "Buyer city"),
            ("buyer_emirate", "Buyer country subdivision"),
            ("buyer_country_code", "Buyer country code"),
            ("buyer_peppol_endpoint", "Buyer electronic address"),
        })
        {
            if (Value(buyer, key).Length == 0)
            {
                errors.Add(label + " is required");
            }
        }

        var sellerCountry = Value(seller, "seller_country_code").ToUpperInvariant();
        var sellerTrn = Digits(Value(seller, "seller_trn"));
        if (sellerTrn.Length == 0)
        {
            errors.Add("Seller tax identifier (TRN) is required");
        }
        else if (!TaxRegistrationValid(sellerCountry, sellerTrn))
        {
            errors.Add("Seller TRN must be exactly 15 digits (UAE FTA)");
        }

        var buyerCountry = Value(buyer, "buyer_country_code").ToUpperInvariant();
        var buyerTrn = Digits(Value(buyer, "buyer_trn"));
        if (buyerTrn.Length > 0 && !TaxRegistrationValid(buyerCountry, buyerTrn))
        {
            errors.Add("Buyer TRN must be exactly 15 digits when provided (UAE FTA)");
        }

        if (lines.Count < 1)
        {
            errors.Add("At least one invoice line is required");
        }

        foreach (var line in lines)
        {
            if (line.ItemName.Trim().Length == 0)
            {
                errors.Add("Line " + line.LineNo.ToString(CultureInfo.InvariantCulture) + ": item_name is required");
            }

            if (line.Quantity <= 0m)
            {
                errors.Add("Line " + line.LineNo.ToString(CultureInfo.InvariantCulture) + ": quantity is required");
            }

            if (line.TaxCategory.Trim().Length == 0)
            {
                errors.Add("Line " + line.LineNo.ToString(CultureInfo.InvariantCulture) + ": tax_category is required");
            }
        }

        if (totalVat < 0m)
        {
            errors.Add("Invoice total tax amount is required");
        }

        return errors;
    }

    /// <summary>AE tenants follow the FTA 15-digit TRN rule; other jurisdictions only need a registration number.</summary>
    private static bool TaxRegistrationValid(string countryCode, string digits)
        => !string.Equals(countryCode, "AE", StringComparison.Ordinal) || digits.Length == 15;

    private async Task<List<ErpSalesInvoiceLine>> BuildLinesAsync(
        DbConnection connection,
        ErpSalesOrderRow order,
        CancellationToken cancellationToken)
    {
        var lines = new List<ErpSalesInvoiceLine>();
        var raw = new List<(string Description, decimal Qty, decimal Unit, decimal Net)>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `description`, `qty`, `unit_price_ex_vat`, `line_ex_vat` FROM `epc_erp_sales_order_lines`"
                + " WHERE `sales_order_id` = ? ORDER BY `line_no`");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = order.Id;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                    reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                    reader.IsDBNull(3) ? 0m : reader.GetDecimal(3)));
            }
        }

        foreach (var (description, qty, unit, net) in raw)
        {
            var lineNet = ErpTaxAmountCalculator.Round2(net);
            var tax = await _tax.CalcAsync(
                connection,
                null,
                lineNet,
                order.CustomerUserId,
                order.ContactId,
                export: false,
                cancellationToken).ConfigureAwait(false);
            lines.Add(new ErpSalesInvoiceLine(
                lines.Count + 1,
                description.Trim().Length > 0 ? description.Trim() : "Line",
                decimal.Max(0.0001m, qty),
                decimal.Round(unit, 4, MidpointRounding.AwayFromZero),
                lineNet,
                tax.TaxCategory,
                tax.TaxRate,
                tax.VatAmount,
                tax.TotalAmount));
        }

        return lines;
    }

    private static string BuildTaxBreakdown(IReadOnlyList<ErpSalesInvoiceLine> lines, decimal subtotal, decimal totalVat)
    {
        var first = lines.Count > 0 ? lines[0] : null;
        var breakdown = new[]
        {
            new Dictionary<string, string>
            {
                ["tax_category"] = first?.TaxCategory ?? "S",
                ["taxable_amount"] = subtotal.ToString(CultureInfo.InvariantCulture),
                ["tax_rate"] = (first?.TaxRate ?? 0m).ToString(CultureInfo.InvariantCulture),
                ["tax_amount"] = totalVat.ToString(CultureInfo.InvariantCulture),
            },
        };
        return JsonSerializer.Serialize(breakdown);
    }

    private sealed record ErpSalesOrderRow(
        long Id,
        string SoNo,
        int CustomerUserId,
        int ContactId,
        string Status,
        long SalesInvoiceId,
        long ShopOrderId,
        decimal TotalAmount);

    private static async Task<ErpSalesOrderRow?> LoadSalesOrderAsync(
        DbConnection connection,
        long salesOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `so_no`, `customer_user_id`, `contact_id`, `status`, `sales_invoice_id`, `total_amount`"
            + " FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = salesOrderId;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ErpSalesOrderRow(
            salesOrderId,
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            reader.IsDBNull(3) ? "draft" : reader.GetString(3),
            reader.IsDBNull(4) ? 0L : reader.GetInt64(4),
            0L,
            reader.IsDBNull(5) ? 0m : reader.GetDecimal(5));
    }

    /// <summary>PHP <c>epc_einvoice_seller_profile</c> over <c>epc_einvoice_settings</c>.</summary>
    private async Task<Dictionary<string, string>> LoadSellerProfileAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var profile = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, fallback) in new[]
        {
            ("seller_name", ""),
            ("seller_trn", ""),
            ("seller_tin", ""),
            ("seller_legal_reg_no", ""),
            ("seller_legal_reg_type", "TL"),
            ("seller_authority_name", ""),
            ("seller_address_line1", ""),
            ("seller_city", ""),
            ("seller_emirate", ""),
            ("seller_country_code", "AE"),
            ("seller_phone", ""),
            ("seller_email", ""),
            ("seller_bank_account", ""),
        })
        {
            profile[key] = await SettingAsync(connection, key, fallback, cancellationToken).ConfigureAwait(false);
        }

        var tin = profile["seller_tin"];
        if (tin.Length == 0)
        {
            tin = TinFromTrn(profile["seller_trn"]);
        }

        profile["seller_tin"] = tin;
        profile["seller_peppol_endpoint"] = PeppolEndpoint(tin, ElectronicScheme);
        profile["seller_electronic_id"] = ElectronicScheme;
        profile["business_process"] = BusinessProcess;
        profile["specification_id"] = SpecificationId;
        return profile;
    }

    /// <summary>PHP <c>epc_einvoice_buyer_profile</c> with the <c>users_profiles</c> fallback.</summary>
    private static async Task<Dictionary<string, string>> LoadBuyerProfileAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        var buyer = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buyer_name"] = string.Empty,
            ["buyer_trn"] = string.Empty,
            ["buyer_legal_reg_no"] = string.Empty,
            ["buyer_legal_reg_type"] = "TL",
            ["buyer_address_line1"] = string.Empty,
            ["buyer_city"] = string.Empty,
            ["buyer_emirate"] = string.Empty,
            ["buyer_country_code"] = "AE",
            ["buyer_email"] = string.Empty,
            ["buyer_peppol_endpoint"] = string.Empty,
        };

        var stored = await LoadBuyerProfileRowAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            foreach (var pair in stored)
            {
                if (pair.Value.Length > 0)
                {
                    buyer[pair.Key] = pair.Value;
                }
            }
        }
        else
        {
            foreach (var pair in await LoadBuyerFromUserAsync(connection, userId, cancellationToken).ConfigureAwait(false))
            {
                if (pair.Value.Length > 0)
                {
                    buyer[pair.Key] = pair.Value;
                }
            }
        }

        if (buyer["buyer_name"].Length == 0)
        {
            buyer["buyer_name"] = "Customer #" + userId.ToString(CultureInfo.InvariantCulture);
        }

        var country = buyer["buyer_country_code"].ToUpperInvariant();
        buyer["buyer_country_code"] = country.Length == 0 ? "AE" : country;
        if (buyer["buyer_address_line1"].Length == 0)
        {
            buyer["buyer_address_line1"] = "Not provided";
        }

        if (buyer["buyer_city"].Length == 0)
        {
            buyer["buyer_city"] = string.Equals(buyer["buyer_country_code"], "AE", StringComparison.Ordinal) ? "Dubai" : "—";
        }

        if (buyer["buyer_emirate"].Length == 0)
        {
            buyer["buyer_emirate"] = buyer["buyer_city"];
        }

        if (buyer["buyer_peppol_endpoint"].Length == 0)
        {
            buyer["buyer_peppol_endpoint"] = PeppolEndpoint(buyer["buyer_trn"], ElectronicScheme);
        }

        if (buyer["buyer_peppol_endpoint"].Length == 0)
        {
            buyer["buyer_peppol_endpoint"] = EndpointNotOnboarded;
        }

        return buyer;
    }

    private static async Task<Dictionary<string, string>?> LoadBuyerProfileRowAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `buyer_name`, `trn`, `legal_reg_no`, `legal_reg_type`, `address_line1`, `city`, `emirate`,"
                + " `country_code`, `email`, `peppol_endpoint` FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = userId;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["buyer_name"] = Text(reader, 0),
                ["buyer_trn"] = Text(reader, 1),
                ["buyer_legal_reg_no"] = Text(reader, 2),
                ["buyer_legal_reg_type"] = Text(reader, 3),
                ["buyer_address_line1"] = Text(reader, 4),
                ["buyer_city"] = Text(reader, 5),
                ["buyer_emirate"] = Text(reader, 6),
                ["buyer_country_code"] = Text(reader, 7),
                ["buyer_email"] = Text(reader, 8),
                ["buyer_peppol_endpoint"] = Text(reader, 9),
            };
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<Dictionary<string, string>> LoadBuyerFromUserAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        var profile = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = userId;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                fields[Text(reader, 0)] = Text(reader, 1);
            }
        }
        catch (DbException)
        {
            return profile;
        }

        var company = fields.GetValueOrDefault("company", string.Empty);
        var name = company.Length > 0
            ? company
            : (fields.GetValueOrDefault("name", string.Empty) + " " + fields.GetValueOrDefault("surname", string.Empty)).Trim();

        profile["buyer_name"] = name;
        profile["buyer_address_line1"] = fields.GetValueOrDefault("address", string.Empty);
        profile["buyer_city"] = fields.GetValueOrDefault("city", string.Empty);
        profile["buyer_trn"] = Digits(fields.GetValueOrDefault("epc_reg_trn", string.Empty));

        var country = fields.GetValueOrDefault("epc_reg_country", string.Empty).ToUpperInvariant();
        if (country.Length == 2)
        {
            profile["buyer_country_code"] = country;
        }

        var email = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            userId).ConfigureAwait(false);
        profile["buyer_email"] = email ?? string.Empty;
        return profile;
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

    private static string Value(IReadOnlyDictionary<string, string> source, string key)
        => source.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : (reader.GetValue(ordinal) is string text ? text.Trim() : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty);

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    /// <summary>PHP <c>epc_einvoice_tin_from_trn</c>.</summary>
    private static string TinFromTrn(string trn)
    {
        var digits = Digits(trn);
        return digits.Length >= 10 ? digits[..10] : digits;
    }

    /// <summary>PHP <c>epc_einvoice_peppol_endpoint</c>.</summary>
    private static string PeppolEndpoint(string trnOrTin, string scheme)
    {
        var tin = TinFromTrn(trnOrTin);
        return tin.Length == 0 ? string.Empty : scheme + ":" + tin;
    }

    /// <summary>Subset of PHP <c>epc_einvoice_ensure_schema</c> / <c>epc_erp_invoices_ensure_schema</c>.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_einvoice_documents` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `uuid` char(36) NOT NULL,"
            + " `invoice_number` varchar(64) NOT NULL,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `user_id` int(11) NOT NULL DEFAULT 0,"
            + " `doc_category` enum('tax_invoice','tax_credit_note','commercial_invoice','credit_note') NOT NULL DEFAULT 'tax_invoice',"
            + " `invoice_type_code` varchar(8) NOT NULL DEFAULT '380',"
            + " `issue_date` int(11) NOT NULL DEFAULT 0,"
            + " `payment_due_date` int(11) NOT NULL DEFAULT 0,"
            + " `vat_point_date` int(11) NOT NULL DEFAULT 0,"
            + " `currency_code` varchar(8) NOT NULL DEFAULT 'AED',"
            + " `vat_currency_code` varchar(8) NOT NULL DEFAULT 'AED',"
            + " `transaction_type_code` char(8) NOT NULL DEFAULT '00000000',"
            + " `payment_means_code` varchar(8) NOT NULL DEFAULT '30',"
            + " `payment_terms` varchar(255) DEFAULT NULL,"
            + " `bank_account` varchar(64) DEFAULT NULL,"
            + " `seller_json` mediumtext,"
            + " `buyer_json` mediumtext,"
            + " `subtotal_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `total_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `total_incl_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `paid_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `rounding_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `amount_due` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `tax_breakdown_json` text,"
            + " `status` enum('draft','validated','queued','submitted','accepted','rejected','cancelled') NOT NULL DEFAULT 'draft',"
            + " `validation_ok` tinyint(1) NOT NULL DEFAULT 0,"
            + " `validation_errors_json` text,"
            + " `xml_content` mediumtext,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " `time_submitted` int(11) NOT NULL DEFAULT 0,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_uuid` (`uuid`),"
            + " UNIQUE KEY `x_invoice_no` (`invoice_number`),"
            + " KEY `x_order` (`order_id`),"
            + " KEY `x_user` (`user_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE electronic invoice documents'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_einvoice_lines` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `document_id` int(11) NOT NULL,"
            + " `line_no` int(11) NOT NULL DEFAULT 1,"
            + " `item_name` varchar(255) NOT NULL,"
            + " `item_description` varchar(512) DEFAULT NULL,"
            + " `item_type` enum('G','S','B') NOT NULL DEFAULT 'G',"
            + " `quantity` decimal(14,4) NOT NULL DEFAULT 0.0000,"
            + " `uom_code` varchar(16) NOT NULL DEFAULT 'C62',"
            + " `unit_price` decimal(14,4) NOT NULL DEFAULT 0.0000,"
            + " `line_net` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `tax_category` varchar(8) NOT NULL DEFAULT 'S',"
            + " `tax_rate` decimal(5,2) NOT NULL DEFAULT 5.00,"
            + " `tax_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `gross_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_line_aed` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `line_amount_aed` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_doc` (`document_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE e-invoice line items'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_einvoice_events` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `document_id` int(11) NOT NULL,"
            + " `event_type` varchar(32) NOT NULL,"
            + " `status` varchar(32) NOT NULL DEFAULT 'info',"
            + " `message` text,"
            + " `payload_json` mediumtext,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_doc` (`document_id`,`time_created`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='E-invoice transmission & FTA event log'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_einvoice_settings` ("
            + " `setting_key` varchar(64) NOT NULL,"
            + " `setting_value` text,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`setting_key`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE e-invoice seller & ASP settings'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_einvoice_documents` ADD `sales_order_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
    }
}
