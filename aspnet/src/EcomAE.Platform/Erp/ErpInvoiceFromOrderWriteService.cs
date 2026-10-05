using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_invoice_from_order</c>: builds a PINT-AE tax invoice from a
/// legacy <c>shop_orders</c> row (only <c>successfully_created = 1</c>), persists it with lines,
/// creation event, advance-VAT adjustment, customer settlement, GL posting (Dr 1100 / Cr 4000 /
/// Cr 2100) and the linked ERP sales-order marker inside a single transaction. Retrying an already
/// converted order returns the existing invoice instead of failing, and the EINV number is allocated
/// inside the transaction so a rollback consumes nothing.
/// </summary>
public interface IErpInvoiceFromOrderWriteService
{
    Task<ErpInvoiceFromOrderResult> ConvertAsync(long orderId, int adminId, CancellationToken cancellationToken = default);
    Task<ErpEinvoiceCreateResult> CreateDocumentAsync(long orderId, IReadOnlyDictionary<string, bool>? transactionFlags, int adminId, CancellationToken cancellationToken = default);
}

public sealed record ErpEinvoiceCreateResult(
    long OrderId,
    long DocumentId,
    string InvoiceNumber,
    bool ValidationOk,
    decimal AdvanceVatCredit);

public sealed record ErpInvoiceFromOrderResult(
    long OrderId,
    long SalesInvoiceId,
    string InvoiceNumber,
    decimal SubtotalExVat,
    decimal TotalVat,
    decimal TotalInclVat,
    decimal PaidAmount,
    decimal AmountDue,
    long LedgerId,
    long GlJournalId);

public sealed record ErpInvoiceFromOrderLine(
    int LineNo,
    string ItemName,
    string ItemDescription,
    string ItemType,
    decimal Quantity,
    string UomCode,
    decimal UnitNet,
    decimal UnitPrice,
    decimal LineNet,
    string TaxCategory,
    decimal TaxRate,
    decimal TaxAmount,
    decimal GrossAmount);

public sealed class ErpInvoiceFromOrderWriteService : IErpInvoiceFromOrderWriteService
{
    private const string ElectronicScheme = "0235";
    private const string EndpointNotOnboarded = "0235:9900000098";
    private const string EndpointDeemedSupply = "0235:9900000097";
    private const string EndpointExports = "0235:9900000099";
    private const string SpecificationId = "urn:peppol:pint:billing-1@ae-1";
    private const string BusinessProcess = "urn:peppol:bis:billing";

    private static readonly string[] FlagOrder =
        ["free_zone", "deemed_supply", "margin_scheme", "summary_invoice", "continuous_supply", "agent_billing", "ecommerce", "exports"];

    private static readonly Dictionary<string, string> TaxCategoryLabels = new(StringComparer.Ordinal)
    {
        ["S"] = "Standard Rate",
        ["Z"] = "Zero rated",
        ["E"] = "Exempt from VAT",
        ["O"] = "Outside scope of VAT",
        ["AE"] = "Reverse Charge",
        ["M"] = "Margin scheme",
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpCashWriteService _cash;
    private readonly IErpGlPostingService _gl;
    private readonly IErpAdvanceVatService _advanceVat;
    private readonly IErpAuditLogWriter _audit;

    public ErpInvoiceFromOrderWriteService(
        IErpWriteConnectionFactory connections,
        IErpCashWriteService cash,
        IErpGlPostingService gl,
        IErpAdvanceVatService advanceVat,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _cash = cash;
        _gl = gl;
        _advanceVat = advanceVat;
        _audit = audit;
    }

    private sealed record LegacyOrderRow(long Id, int UserId, long Time, string HowGetJson);

    public async Task<ErpInvoiceFromOrderResult> ConvertAsync(long orderId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }

        if (orderId <= 0)
        {
            throw new ErpWriteException("Order not found");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        // DDL implicitly commits on MySQL — every schema ensure runs before the conversion transaction opens.
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _advanceVat.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var order = await LoadOrderAsync(connection, null, orderId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Order not found");

        var completed = await CompletedConversionAsync(connection, null, orderId, cancellationToken).ConfigureAwait(false);
        if (completed is not null)
        {
            return completed;
        }

        var exclusions = await ExcludedItemStatusesAsync(connection, cancellationToken).ConfigureAwait(false);
        var items = await LoadItemsAsync(connection, orderId, exclusions, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            throw new ErpWriteException("Order has no billable lines");
        }

        var tax = await ErpDashboardReadService.LoadTenantVatAsync(connection, cancellationToken).ConfigureAwait(false);
        var customer = await ErpDashboardReadService.CustomerContextAsync(connection, order.UserId, cancellationToken).ConfigureAwait(false);
        var seller = await LoadSellerProfileAsync(connection, cancellationToken).ConfigureAwait(false);

        var destination = ErpDashboardReadService.DestinationCountry(order.HowGetJson, customer.Country);
        if (destination.Length == 0)
        {
            destination = "AE";
        }

        var exports = destination != "AE";
        var transactionTypeCode = BuildTransactionTypeCode(exports);
        var (supplyCategory, supplyRate) = SupplyCategory(destination, exports, tax.RatePercent);
        var inclusive = string.Equals(
            ErpDashboardReadService.DisplayMode(customer.VatType),
            "inclusive",
            StringComparison.Ordinal)
            && supplyRate > 0m;

        var buyer = await LoadBuyerAsync(connection, order, customer.Country, destination, exports, cancellationToken).ConfigureAwait(false);

        var lines = new List<ErpInvoiceFromOrderLine>();
        var lineNo = 0;
        var subtotal = 0m;
        var totalVat = 0m;
        foreach (var item in items)
        {
            lineNo++;
            var line = ComputeOrderLine(
                item.UnitPrice,
                item.Quantity,
                supplyCategory,
                supplyRate,
                inclusive,
                tax.SalesEnabled,
                lineNo,
                item.Name,
                item.Description,
                "G");
            lines.Add(line);
            subtotal += line.LineNet;
            totalVat += line.TaxAmount;
        }

        var courier = CourierLine(order, lineNo + 1, supplyCategory, supplyRate, tax.SalesEnabled);
        if (courier is not null)
        {
            lines.Add(courier);
            subtotal += courier.LineNet;
            totalVat += courier.TaxAmount;
        }

        subtotal = Round2(subtotal);
        totalVat = Round2(totalVat);
        var totalIncl = Round2(subtotal + totalVat);

        var paid = Round2(await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?"),
            cancellationToken,
            orderId).ConfigureAwait(false));
        var amountDue = Math.Max(0m, Round2(totalIncl - paid));

        var issueDate = order.Time;
        var dueDays = ParseInt(await EinvoicingSettingAsync(connection, "default_payment_due_days", "7", cancellationToken).ConfigureAwait(false), 7);
        var dueDate = issueDate + ((long)dueDays * 86400);
        var paymentMeans = await EinvoicingSettingAsync(connection, "payment_means_code", "30", cancellationToken).ConfigureAwait(false);
        var paymentTerms = await EinvoicingSettingAsync(connection, "payment_terms", "Within 7 days", cancellationToken).ConfigureAwait(false);
        var bankAccount = await EinvoicingSettingAsync(connection, "seller_bank_account", string.Empty, cancellationToken).ConfigureAwait(false);

        var errors = ValidateTaxInvoice(seller, buyer, lines, subtotal, totalVat, tax.VatRegistered);
        if (errors.Count > 0)
        {
            throw new ErpWriteException("Tax invoice validation failed: " + string.Join("; ", errors));
        }

        var taxBreakdownJson = SerializeTaxBreakdown(supplyCategory, subtotal, supplyRate, totalVat);
        var uuid = Guid.NewGuid().ToString("D");
        var xml = BuildInvoiceXml(
            uuid,
            "PLACEHOLDER",
            issueDate,
            dueDate,
            paymentMeans,
            bankAccount,
            seller,
            buyer,
            lines,
            subtotal,
            totalVat,
            totalIncl,
            paid,
            amountDue,
            supplyCategory,
            supplyRate);

        long invoiceId;
        string invoiceNumber;
        long ledgerId;
        long glJournalId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var locked = await LoadOrderAsync(connection, transaction, orderId, cancellationToken).ConfigureAwait(false)
                ?? throw new ErpWriteException("Order not found");
            var alreadyDone = await CompletedConversionAsync(connection, transaction, orderId, cancellationToken).ConfigureAwait(false);
            if (alreadyDone is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return alreadyDone;
            }

            invoiceNumber = await NextInvoiceNumberAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            xml = xml.Replace("<cbc:ID>PLACEHOLDER</cbc:ID>", "<cbc:ID>" + invoiceNumber + "</cbc:ID>", StringComparison.Ordinal);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_documents` (`uuid`, `invoice_number`, `order_id`, `user_id`, `doc_category`,"
                    + " `invoice_type_code`, `issue_date`, `payment_due_date`, `vat_point_date`, `currency_code`,"
                    + " `vat_currency_code`, `transaction_type_code`, `payment_means_code`, `payment_terms`, `bank_account`,"
                    + " `seller_json`, `buyer_json`, `subtotal_ex_vat`, `total_vat`, `total_incl_vat`, `paid_amount`,"
                    + " `rounding_amount`, `amount_due`, `tax_breakdown_json`, `status`, `validation_ok`,"
                    + " `validation_errors_json`, `xml_content`, `time_created`, `time_updated`, `admin_id`)"
                    + " VALUES (?,?,?,?,'tax_invoice','380',?,?,?,'AED','AED',?,?,?,?,?,?,?,?,?,?,0,?,?,'validated',1,'[]',?,?,?,?)"),
                cancellationToken,
                uuid,
                invoiceNumber,
                orderId,
                locked.UserId,
                issueDate,
                dueDate,
                issueDate,
                transactionTypeCode,
                paymentMeans,
                paymentTerms,
                bankAccount,
                JsonSerializer.Serialize(seller),
                JsonSerializer.Serialize(buyer),
                subtotal,
                totalVat,
                totalIncl,
                paid,
                amountDue,
                taxBreakdownJson,
                xml,
                now,
                now,
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
                        + " `gross_amount`, `vat_line_aed`, `line_amount_aed`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    invoiceId,
                    line.LineNo,
                    line.ItemName,
                    line.ItemDescription,
                    line.ItemType,
                    line.Quantity,
                    line.UomCode,
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
                    + " `time_created`) VALUES (?, 'created', 'validated', 'Document validated against mandatory fields', ?, ?)"),
                cancellationToken,
                invoiceId,
                JsonSerializer.Serialize(new { errors = Array.Empty<string>(), order_id = orderId }),
                now).ConfigureAwait(false);

            await ApplyAdvanceVatAdjustmentAsync(
                connection,
                transaction,
                invoiceId,
                orderId,
                locked.UserId,
                totalVat,
                issueDate,
                tax,
                cancellationToken).ConfigureAwait(false);

            ledgerId = await _cash.CustomerSettlementAsync(
                connection,
                transaction,
                new ErpCustomerSettlementInput
                {
                    UserId = locked.UserId,
                    Amount = totalIncl,
                    Income = true,
                    EntryKind = "adjustment",
                    OrderId = orderId,
                    Reference = invoiceNumber,
                    Note = "Tax invoice " + invoiceNumber + " from order #" + orderId.ToString(CultureInfo.InvariantCulture),
                    Time = issueDate,
                    PostGl = false,
                },
                adminId,
                cancellationToken).ConfigureAwait(false);

            glJournalId = await _gl.PostSalesInvoiceAsync(
                connection,
                transaction,
                new ErpGlSalesInvoicePosting(
                    invoiceId,
                    invoiceNumber,
                    subtotal,
                    totalVat,
                    totalIncl,
                    issueDate,
                    ErpSalesInvoiceWriteService.SalesLegislationRef(seller)),
                adminId,
                cancellationToken).ConfigureAwait(false);

            // PHP epc_erp_order_fulfillment_post_sales_invoice marks the linked ERP sales order.
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_sales_orders` SET `status` = 'invoiced', `sales_invoice_id` = ?,"
                        + " `fulfillment_status` = 'invoiced', `time_updated` = ? WHERE `shop_order_id` = ?"),
                    cancellationToken,
                    invoiceId,
                    now,
                    orderId).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // epc_erp_sales_orders is optional linkage on legacy tenants; missing table is not fatal.
            }

            await _audit.LogAsync(
                connection,
                transaction,
                adminId,
                "invoice_from_order",
                "shop_order",
                orderId,
                "Converted to tax invoice",
                new Dictionary<string, string?>
                {
                    ["invoice_id"] = invoiceId.ToString(CultureInfo.InvariantCulture),
                    ["invoice_number"] = invoiceNumber,
                },
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ErpWriteException("Order conversion failed and was rolled back: " + ex.Message);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new ErpInvoiceFromOrderResult(
            orderId,
            invoiceId,
            invoiceNumber,
            subtotal,
            totalVat,
            totalIncl,
            paid,
            amountDue,
            ledgerId,
            glJournalId);
    }

    /// <summary>
    /// PHP <c>einvoice_create</c> ajax case: <c>epc_einvoice_build_from_order</c> +
    /// <c>epc_einvoice_save_document</c>. Writes the e-invoice document, lines, and 'created'
    /// event plus the advance-VAT adjustment inside one transaction — no settlement, GL,
    /// sales-order marker, or audit entry (those belong to the so_to_invoice conversion path).
    /// Operator-supplied transaction flags merge into the computed flag set exactly like the
    /// PHP POST `transaction_flags`/`flag_*` inputs feeding <c>epc_order_vat_transaction_flags</c>.
    /// </summary>
    public async Task<ErpEinvoiceCreateResult> CreateDocumentAsync(long orderId, IReadOnlyDictionary<string, bool>? transactionFlags, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
        if (orderId <= 0)
        {
            throw new ErpWriteException("Order not found");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _advanceVat.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var order = await LoadOrderAsync(connection, null, orderId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Order not found");

        var exclusions = await ExcludedItemStatusesAsync(connection, cancellationToken).ConfigureAwait(false);
        var items = await LoadItemsAsync(connection, orderId, exclusions, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0)
        {
            throw new ErpWriteException("Order has no billable lines");
        }

        var tax = await ErpDashboardReadService.LoadTenantVatAsync(connection, cancellationToken).ConfigureAwait(false);
        var customer = await ErpDashboardReadService.CustomerContextAsync(connection, order.UserId, cancellationToken).ConfigureAwait(false);
        var seller = await LoadSellerProfileAsync(connection, cancellationToken).ConfigureAwait(false);

        var destination = ErpDashboardReadService.DestinationCountry(order.HowGetJson, customer.Country);
        if (destination.Length == 0)
        {
            destination = "AE";
        }

        var flags = transactionFlags is null
            ? new Dictionary<string, bool>(StringComparer.Ordinal)
            : new Dictionary<string, bool>(transactionFlags, StringComparer.Ordinal);
        var exports = destination != "AE";
        if (exports)
        {
            flags["exports"] = true;
        }
        var deemedSupply = flags.TryGetValue("deemed_supply", out var ds) && ds;
        var transactionTypeCode = BuildTransactionTypeCode(flags);
        var (supplyCategory, supplyRate) = SupplyCategoryWithFlags(destination, flags, tax.RatePercent);
        var inclusive = string.Equals(
            ErpDashboardReadService.DisplayMode(customer.VatType),
            "inclusive",
            StringComparison.Ordinal)
            && supplyRate > 0m;

        var buyer = await LoadBuyerAsync(connection, order, customer.Country, destination, exports, cancellationToken, deemedSupply).ConfigureAwait(false);

        var lines = new List<ErpInvoiceFromOrderLine>();
        var lineNo = 0;
        var subtotal = 0m;
        var totalVat = 0m;
        foreach (var item in items)
        {
            lineNo++;
            var line = ComputeOrderLine(
                item.UnitPrice,
                item.Quantity,
                supplyCategory,
                supplyRate,
                inclusive,
                tax.SalesEnabled,
                lineNo,
                item.Name,
                item.Description,
                "G");
            lines.Add(line);
            subtotal += line.LineNet;
            totalVat += line.TaxAmount;
        }

        var courier = CourierLine(order, lineNo + 1, supplyCategory, supplyRate, tax.SalesEnabled);
        if (courier is not null)
        {
            lines.Add(courier);
            subtotal += courier.LineNet;
            totalVat += courier.TaxAmount;
        }

        subtotal = Round2(subtotal);
        totalVat = Round2(totalVat);
        var totalIncl = Round2(subtotal + totalVat);

        var paid = Round2(await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?"),
            cancellationToken,
            orderId).ConfigureAwait(false));
        var amountDue = Math.Max(0m, Round2(totalIncl - paid));

        var issueDate = order.Time;
        var dueDays = ParseInt(await EinvoicingSettingAsync(connection, "default_payment_due_days", "7", cancellationToken).ConfigureAwait(false), 7);
        var dueDate = issueDate + ((long)dueDays * 86400);
        var paymentMeans = await EinvoicingSettingAsync(connection, "payment_means_code", "30", cancellationToken).ConfigureAwait(false);
        var paymentTerms = await EinvoicingSettingAsync(connection, "payment_terms", "Within 7 days", cancellationToken).ConfigureAwait(false);
        var bankAccount = await EinvoicingSettingAsync(connection, "seller_bank_account", string.Empty, cancellationToken).ConfigureAwait(false);

        var errors = ValidateTaxInvoice(seller, buyer, lines, subtotal, totalVat, tax.VatRegistered);
        if (errors.Count > 0)
        {
            throw new ErpWriteException("Tax invoice validation failed: " + string.Join("; ", errors));
        }

        var taxBreakdownJson = SerializeTaxBreakdown(supplyCategory, subtotal, supplyRate, totalVat);
        var uuid = Guid.NewGuid().ToString("D");

        // PHP allocates the invoice number before building the XML (no PLACEHOLDER swap needed).
        var invoiceNumber = await NextEinvoiceNumberAsync(connection, cancellationToken).ConfigureAwait(false);
        var xml = BuildInvoiceXml(
            uuid,
            invoiceNumber,
            issueDate,
            dueDate,
            paymentMeans,
            bankAccount,
            seller,
            buyer,
            lines,
            subtotal,
            totalVat,
            totalIncl,
            paid,
            amountDue,
            supplyCategory,
            supplyRate);

        long documentId;
        decimal advanceVatCredit;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_documents` (`uuid`, `invoice_number`, `order_id`, `user_id`, `doc_category`,"
                    + " `invoice_type_code`, `issue_date`, `payment_due_date`, `vat_point_date`, `currency_code`,"
                    + " `vat_currency_code`, `transaction_type_code`, `payment_means_code`, `payment_terms`, `bank_account`,"
                    + " `seller_json`, `buyer_json`, `subtotal_ex_vat`, `total_vat`, `total_incl_vat`, `paid_amount`,"
                    + " `rounding_amount`, `amount_due`, `tax_breakdown_json`, `status`, `validation_ok`,"
                    + " `validation_errors_json`, `xml_content`, `time_created`, `time_updated`, `admin_id`)"
                    + " VALUES (?,?,?,?,'tax_invoice','380',?,?,?,'AED','AED',?,?,?,?,?,?,?,?,?,?,0,?,?,'validated',1,'[]',?,?,?,?)"),
                cancellationToken,
                uuid,
                invoiceNumber,
                orderId,
                order.UserId,
                issueDate,
                dueDate,
                issueDate,
                transactionTypeCode,
                paymentMeans,
                paymentTerms,
                bankAccount,
                JsonSerializer.Serialize(seller),
                JsonSerializer.Serialize(buyer),
                subtotal,
                totalVat,
                totalIncl,
                paid,
                amountDue,
                taxBreakdownJson,
                xml,
                now,
                now,
                adminId).ConfigureAwait(false);
            documentId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            foreach (var line in lines)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_einvoice_lines` (`document_id`, `line_no`, `item_name`, `item_description`, `item_type`,"
                        + " `quantity`, `uom_code`, `unit_price`, `line_net`, `tax_category`, `tax_rate`, `tax_amount`,"
                        + " `gross_amount`, `vat_line_aed`, `line_amount_aed`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    documentId,
                    line.LineNo,
                    line.ItemName,
                    line.ItemDescription,
                    line.ItemType,
                    line.Quantity,
                    line.UomCode,
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
                    + " `time_created`) VALUES (?, 'created', 'validated', 'Document validated against mandatory fields', ?, ?)"),
                cancellationToken,
                documentId,
                JsonSerializer.Serialize(new { errors = Array.Empty<string>() }),
                now).ConfigureAwait(false);

            advanceVatCredit = await ApplyAdvanceVatAdjustmentAsync(
                connection,
                transaction,
                documentId,
                orderId,
                order.UserId,
                totalVat,
                issueDate,
                tax,
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new ErpWriteException("E-invoice creation failed and was rolled back: " + ex.Message);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new ErpEinvoiceCreateResult(orderId, documentId, invoiceNumber, true, advanceVatCredit);
    }

    private static async Task<LegacyOrderRow?> LoadOrderAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long orderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT `id`, `user_id`, `time`, `how_get_json` FROM `shop_orders`"
            + " WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1"
            + (transaction is null ? string.Empty : " FOR UPDATE"));
        ErpDb.AddParameters(command, orderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new LegacyOrderRow(
            reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
            reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
            reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty);
    }

    /// <summary>PHP <c>epc_erp_item_status_exclusion</c>: statuses with <c>count_flag = 0</c> are never billed.</summary>
    private static async Task<List<long>> ExcludedItemStatusesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }
        }
        catch (DbException)
        {
            // PHP swallows a missing status-reference table and bills every line.
        }

        return ids;
    }

    private static async Task<List<LegacyItem>> LoadItemsAsync(
        DbConnection connection,
        long orderId,
        IReadOnlyList<long> exclusions,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT `t2_manufacturer`, `t2_article`, `t2_name`, `price`, `count_need`"
            + " FROM `shop_orders_items` WHERE `order_id` = ?";
        if (exclusions.Count > 0)
        {
            sql += " AND `status` NOT IN (" + string.Join(",", exclusions.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")";
        }

        sql += " ORDER BY `id`";

        var items = new List<LegacyItem>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = ((Text(reader, 0) + " " + Text(reader, 1)).Trim());
            items.Add(new LegacyItem(
                name,
                Text(reader, 2),
                DecimalValue(reader, 3),
                DecimalValue(reader, 4)));
        }

        return items;
    }

    private sealed record LegacyItem(string Name, string Description, decimal UnitPrice, decimal Quantity);

    /// <summary>PHP <c>epc_uae_customer_vat_order_line</c> line math.</summary>
    public static ErpInvoiceFromOrderLine ComputeOrderLine(
        decimal unitPrice,
        decimal quantity,
        string taxCategory,
        decimal taxRate,
        bool inclusive,
        bool salesEnabled,
        int lineNo,
        string name,
        string description,
        string itemType)
    {
        var qty = Math.Max(0m, quantity);
        var unit = Round2(Math.Max(0m, unitPrice));
        var grossStored = Round2(unit * qty);

        decimal unitNet;
        decimal lineNet;
        decimal vat;
        decimal gross;
        decimal rate;
        if (taxRate <= 0m || !salesEnabled)
        {
            unitNet = unit;
            lineNet = grossStored;
            vat = 0m;
            gross = grossStored;
            rate = 0m;
        }
        else if (inclusive)
        {
            lineNet = Round2(grossStored / (1m + taxRate / 100m));
            vat = Round2(grossStored - lineNet);
            gross = grossStored;
            unitNet = qty > 0m ? decimal.Round(lineNet / qty, 4, MidpointRounding.AwayFromZero) : 0m;
            rate = taxRate;
        }
        else
        {
            unitNet = unit;
            lineNet = grossStored;
            vat = Round2(lineNet * taxRate / 100m);
            gross = Round2(lineNet + vat);
            rate = taxRate;
        }

        return new ErpInvoiceFromOrderLine(
            lineNo,
            name.Trim().Length > 0 ? name.Trim() : "Part line " + lineNo.ToString(CultureInfo.InvariantCulture),
            (description ?? string.Empty).Trim(),
            itemType,
            qty,
            "C62",
            unitNet,
            Round2(unitNet),
            lineNet,
            taxCategory,
            rate,
            vat,
            gross);
    }

    /// <summary>PHP <c>epc_order_courier_invoice_line</c>: customer-paid delivery as a service line.</summary>
    private static ErpInvoiceFromOrderLine? CourierLine(
        LegacyOrderRow order,
        int lineNo,
        string taxCategory,
        decimal taxRate,
        bool salesEnabled)
    {
        var amount = Round2(Math.Max(0m, ErpDashboardReadService.CourierAmount(order.HowGetJson)));
        if (amount <= 0m)
        {
            return null;
        }

        var vat = 0m;
        var gross = amount;
        if (taxRate > 0m && salesEnabled)
        {
            vat = Round2(amount * taxRate / 100m);
            gross = Round2(amount + vat);
        }

        var how = ParseHowGet(order.HowGetJson);
        var parts = new List<string>();
        var carrier = HowText(how, "carrier").ToUpperInvariant();
        var service = HowText(how, "service");
        var country = HowText(how, "country").ToUpperInvariant();
        if (carrier.Length > 0)
        {
            parts.Add(carrier);
        }

        if (service.Length > 0)
        {
            parts.Add(service);
        }

        if (country.Length > 0)
        {
            parts.Add("to " + country);
        }

        return new ErpInvoiceFromOrderLine(
            lineNo,
            "Courier / delivery (customer paid)",
            parts.Count > 0 ? string.Join(" · ", parts) : "Shipping charge billed to customer",
            "S",
            1m,
            "C62",
            amount,
            amount,
            amount,
            taxCategory,
            amount > 0m ? taxRate : 0m,
            vat,
            gross);
    }

    /// <summary>PHP <c>epc_einvoice_build_transaction_code</c>.</summary>
    public static string BuildTransactionTypeCode(bool exports)
        => BuildTransactionTypeCode(new Dictionary<string, bool>(StringComparer.Ordinal) { ["exports"] = exports });

    /// <summary>PHP <c>epc_einvoice_build_transaction_code</c>: 8-bit flag string in FlagOrder.</summary>
    public static string BuildTransactionTypeCode(IReadOnlyDictionary<string, bool> flags)
    {
        var code = new char[8];
        for (var i = 0; i < FlagOrder.Length; i++)
        {
            code[i] = flags.TryGetValue(FlagOrder[i], out var on) && on ? '1' : '0';
        }

        return new string(code);
    }

    /// <summary>PHP <c>epc_uae_vat_supply_tax_category</c> for the flags this route can produce.</summary>
    public static (string Category, decimal Rate) SupplyCategory(string buyerCountry, bool exports, decimal tenantRate)
        => exports || ErpDashboardReadService.NormalizeCountry(buyerCountry) != "AE"
            ? ("Z", 0m)
            : ("S", tenantRate);

    /// <summary>PHP <c>epc_uae_vat_supply_tax_category</c>: exports/non-AE → Z, margin_scheme → M, else S at tenant rate.</summary>
    public static (string Category, decimal Rate) SupplyCategoryWithFlags(string buyerCountry, IReadOnlyDictionary<string, bool> flags, decimal tenantRate)
    {
        if (flags.TryGetValue("exports", out var e) && e)
        {
            return ("Z", 0m);
        }
        if (ErpDashboardReadService.NormalizeCountry(buyerCountry) != "AE")
        {
            return ("Z", 0m);
        }
        if (flags.TryGetValue("margin_scheme", out var m) && m)
        {
            return ("M", 0m);
        }
        return ("S", tenantRate);
    }

    /// <summary>
    /// PHP <c>epc_einvoice_validate_document</c> (tax-invoice mode) plus the einvoice checks of
    /// <c>epc_uae_vat_apply_to_voucher</c>.
    /// </summary>
    public static List<string> ValidateTaxInvoice(
        IReadOnlyDictionary<string, string> seller,
        IReadOnlyDictionary<string, string> buyer,
        IReadOnlyList<ErpInvoiceFromOrderLine> lines,
        decimal subtotal,
        decimal totalVat,
        bool tenantVatRegistered)
    {
        var errors = ErpSalesInvoiceWriteService.ValidateTaxInvoiceContent(
            seller,
            buyer,
            lines.Select(l => new ErpSalesInvoiceLine(l.LineNo, l.ItemName, l.Quantity, l.UnitPrice, l.LineNet, l.TaxCategory, l.TaxRate, l.TaxAmount, l.GrossAmount)).ToList(),
            totalVat);

        if (!string.Equals(Value(seller, "seller_country_code"), "AE", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Seller country must be AE for UAE FTA tax invoices");
        }

        if (!tenantVatRegistered)
        {
            errors.Add("Tenant must be VAT-registered in UAE to issue FTA tax invoices");
        }

        var sellerTrn = Digits(Value(seller, "seller_trn"));
        if (sellerTrn.Length != 15)
        {
            errors.Add("Seller TRN must be exactly 15 digits (FTA)");
        }

        if (string.Equals(Value(buyer, "buyer_country_code"), "AE", StringComparison.OrdinalIgnoreCase))
        {
            var buyerTrn = Digits(Value(buyer, "buyer_trn"));
            if (buyerTrn.Length > 0 && buyerTrn.Length != 15)
            {
                errors.Add("UAE buyer TRN must be exactly 15 digits when provided");
            }
        }

        if (subtotal < 0m || totalVat < 0m)
        {
            errors.Add("Invoice amounts must be nonnegative");
        }

        return errors;
    }

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
            profile[key] = await EinvoicingSettingAsync(connection, key, fallback, cancellationToken).ConfigureAwait(false);
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

    /// <summary>PHP <c>epc_einvoice_buyer_profile</c> + the <c>how_get_json</c> shipment overrides.</summary>
    private static async Task<Dictionary<string, string>> LoadBuyerAsync(
        DbConnection connection,
        LegacyOrderRow order,
        string customerCountry,
        string destination,
        bool exports,
        CancellationToken cancellationToken,
        bool deemedSupply = false)
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buyer_name"] = string.Empty,
            ["trn"] = string.Empty,
            ["tin"] = string.Empty,
            ["legal_reg_no"] = string.Empty,
            ["legal_reg_type"] = "TL",
            ["authority_name"] = string.Empty,
            ["address_line1"] = string.Empty,
            ["city"] = string.Empty,
            ["emirate"] = string.Empty,
            ["country_code"] = string.Empty,
            ["phone"] = string.Empty,
            ["email"] = string.Empty,
            ["peppol_endpoint"] = string.Empty,
            ["buyer_onboarded"] = "0",
        };

        var hasStoredProfile = false;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `buyer_name`, `trn`, `tin`, `legal_reg_no`, `legal_reg_type`, `authority_name`, `address_line1`,"
                + " `city`, `emirate`, `country_code`, `phone`, `email`, `peppol_endpoint`, `buyer_onboarded`"
                + " FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1");
            ErpDb.AddParameters(command, order.UserId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                hasStoredProfile = true;
                var keys = raw.Keys.ToArray();
                for (var i = 0; i < keys.Length; i++)
                {
                    var value = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
                    if (value.Length > 0)
                    {
                        raw[keys[i]] = value;
                    }
                }
            }
        }
        catch (DbException)
        {
            // Optional on legacy tenants: fall through to the users_profiles derived buyer.
        }

        if (!hasStoredProfile)
        {
            foreach (var pair in await BuyerFromUserAsync(connection, order.UserId, cancellationToken).ConfigureAwait(false))
            {
                if (pair.Value.Length > 0 && raw.ContainsKey(pair.Key))
                {
                    raw[pair.Key] = pair.Value;
                }
            }
        }

        var tin = raw["tin"].Length > 0 ? raw["tin"] : TinFromTrn(raw["trn"]);
        var storedEndpoint = raw["peppol_endpoint"].Length > 0 ? raw["peppol_endpoint"] : PeppolEndpoint(tin, ElectronicScheme);
        var endpoint = deemedSupply
            ? EndpointDeemedSupply
            : exports
                ? EndpointExports
                : raw["buyer_onboarded"] == "1" && storedEndpoint.Length > 0
                    ? storedEndpoint
                    : EndpointNotOnboarded;

        var how = ParseHowGet(order.HowGetJson);
        var shipAddress = HowText(how, "address");
        var shipCity = HowText(how, "city");
        var shipPhone = HowText(how, "phone");

        var country = destination.Length > 0 ? destination : ErpDashboardReadService.NormalizeCountry(customerCountry);
        var name = raw["buyer_name"];
        if (name.Length == 0)
        {
            name = "Customer #" + order.UserId.ToString(CultureInfo.InvariantCulture);
        }

        var city = shipCity.Length > 0 ? shipCity : raw["city"];
        if (city.Length == 0)
        {
            city = country == "AE" ? "Dubai" : "—";
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buyer_name"] = name,
            ["buyer_trn"] = raw["trn"],
            ["buyer_tin"] = tin,
            ["buyer_legal_reg_no"] = raw["legal_reg_no"],
            ["buyer_legal_reg_type"] = raw["legal_reg_type"],
            ["buyer_authority_name"] = raw["authority_name"],
            ["buyer_address_line1"] = shipAddress.Length > 0 ? shipAddress : raw["address_line1"],
            ["buyer_city"] = city,
            ["buyer_emirate"] = country == "AE" ? (raw["emirate"].Length > 0 ? raw["emirate"] : "Dubai") : string.Empty,
            ["buyer_country_code"] = country,
            ["buyer_phone"] = shipPhone.Length > 0 ? shipPhone : raw["phone"],
            ["buyer_email"] = raw["email"],
            ["buyer_electronic_id"] = ElectronicScheme,
            ["buyer_peppol_endpoint"] = endpoint,
        };
    }

    /// <summary>PHP <c>epc_einvoice_buyer_profile_from_user</c>.</summary>
    private static async Task<Dictionary<string, string>> BuyerFromUserAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                fields[Text(reader, 0)] = Text(reader, 1);
            }
        }
        catch (DbException)
        {
            return fields;
        }

        var profile = new Dictionary<string, string>(StringComparer.Ordinal);
        var company = fields.GetValueOrDefault("company", string.Empty);
        profile["buyer_name"] = company.Length > 0
            ? company
            : (fields.GetValueOrDefault("name", string.Empty) + " " + fields.GetValueOrDefault("surname", string.Empty)).Trim();
        profile["address_line1"] = fields.GetValueOrDefault("address", string.Empty);
        profile["city"] = fields.GetValueOrDefault("city", string.Empty);
        profile["trn"] = Digits(fields.GetValueOrDefault("epc_reg_trn", string.Empty));
        var country = fields.GetValueOrDefault("epc_reg_country", string.Empty).ToUpperInvariant();
        profile["country_code"] = country.Length == 2 ? country : string.Empty;
        profile["phone"] = fields.GetValueOrDefault("phone", string.Empty);

        try
        {
            var email = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false);
            profile["email"] = email ?? string.Empty;
            var phone = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `phone` FROM `users` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false);
            if (profile["phone"].Length == 0)
            {
                profile["phone"] = phone ?? string.Empty;
            }
        }
        catch (DbException)
        {
        }

        return profile;
    }

    /// <summary>
    /// PHP <c>epc_uae_vat_apply_invoice_adjustment</c>: sync unrecorded order payments into
    /// <c>epc_uae_vat_advance</c>, then credit up to the invoice VAT against unadjusted advances —
    /// all inside the conversion transaction.
    /// </summary>
    private async Task<decimal> ApplyAdvanceVatAdjustmentAsync(
        DbConnection connection,
        DbTransaction transaction,
        long documentId,
        long orderId,
        int userId,
        decimal invoiceVat,
        long paymentTimeFallback,
        ErpDashboardReadService.TenantVat tax,
        CancellationToken cancellationToken)
    {
        var advanceEnabled = Truthy(await PricingSettingAsync(connection, transaction, "vat_on_advance_enabled", "1", cancellationToken).ConfigureAwait(false));
        if (advanceEnabled && tax.SalesEnabled && userId > 0)
        {
            var payments = new List<(long LedgerId, decimal Amount, long Time)>();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = ErpDb.Positional(
                    "SELECT `id`, `amount`, `time` FROM `shop_users_accounting`"
                    + " WHERE `active` = 1 AND `income` = 0 AND `order_id` = ? AND `amount` > 0");
                ErpDb.AddParameters(command, orderId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    payments.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture),
                        reader.IsDBNull(2) ? 0L : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture)));
                }
            }

            foreach (var (ledgerRowId, amount, paidAt) in payments)
            {
                var duplicate = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `id` FROM `epc_uae_vat_advance` WHERE `ledger_id` = ? LIMIT 1"),
                    cancellationToken,
                    ledgerRowId).ConfigureAwait(false);
                if (duplicate > 0)
                {
                    continue;
                }

                var rounded = Round2(amount);
                var ex = Round2(rounded / (1m + tax.RatePercent / 100m));
                var vat = Round2(rounded - ex);
                if (vat <= 0m)
                {
                    continue;
                }

                var now = paidAt > 0 ? paidAt : paymentTimeFallback;
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_uae_vat_advance` (`order_id`, `user_id`, `ledger_id`, `cash_entry_id`, `sales_order_id`,"
                        + " `source_type`, `payment_amount`, `amount_ex_vat`, `vat_amount`, `vat_rate`, `payment_time`, `einvoice_document_id`, `adjusted`, `time_created`)"
                        + " VALUES (?,?,?,0,0,'order',?,?,?,?,?,0,0,?)"),
                    cancellationToken,
                    orderId,
                    userId,
                    ledgerRowId,
                    rounded,
                    ex,
                    vat,
                    tax.RatePercent,
                    now,
                    now).ConfigureAwait(false);
            }
        }

        var advanceVat = Round2(await ErpDb.DecimalAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT IFNULL(SUM(`vat_amount`),0) FROM `epc_uae_vat_advance` WHERE `order_id` = ? AND `adjusted` = 0"),
            cancellationToken,
            orderId).ConfigureAwait(false));
        var credit = Round2(Math.Min(invoiceVat, advanceVat));
        var netVat = Round2(Math.Max(0m, invoiceVat - credit));

        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "UPDATE `epc_einvoice_documents` SET `advance_vat_credit` = ?, `vat_net_after_advance` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            credit,
            netVat,
            paymentTimeFallback,
            documentId).ConfigureAwait(false);

        if (credit > 0m)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_uae_vat_advance` SET `adjusted` = 1, `einvoice_document_id` = ? WHERE `order_id` = ? AND `adjusted` = 0"),
                cancellationToken,
                documentId,
                orderId).ConfigureAwait(false);
        }

        return credit;
    }

    /// <summary>
    /// Rollback-safe <c>EINV-YYYY-NNNNN</c>: the sequence row lives inside the conversion
    /// transaction so a validation or posting failure consumes no number.
    /// </summary>
    private static async Task<string> NextInvoiceNumberAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var year = DateTimeOffset.UtcNow.Year;
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_voucher_sequences` (`voucher_type`, `year`, `last_seq`) VALUES ('EINV', ?, 1)"
                + " ON DUPLICATE KEY UPDATE `last_seq` = `last_seq` + 1"),
            cancellationToken,
            year).ConfigureAwait(false);
        var seq = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `last_seq` FROM `epc_erp_voucher_sequences` WHERE `voucher_type` = 'EINV' AND `year` = ?"),
            cancellationToken,
            year).ConfigureAwait(false);
        return "EINV-" + year.ToString(CultureInfo.InvariantCulture) + "-" + seq.ToString(CultureInfo.InvariantCulture).PadLeft(5, '0');
    }

    /// <summary>PHP <c>epc_einvoice_next_number</c>: max numeric suffix over existing EINV-YYYY-* documents + 1.</summary>
    private static async Task<string> NextEinvoiceNumberAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var prefix = "EINV-" + year.ToString(CultureInfo.InvariantCulture) + "-";
        var last = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `invoice_number` FROM `epc_einvoice_documents` WHERE `invoice_number` LIKE ? ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            prefix + "%").ConfigureAwait(false);
        var n = 1;
        if (last is not null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(last, "-(\\d+)$");
            if (match.Success)
            {
                n = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) + 1;
            }
        }
        return prefix + n.ToString(CultureInfo.InvariantCulture).PadLeft(5, '0');
    }

    /// <summary>Prior committed conversion by order linkage or by the linked ERP sales order.</summary>
    private static async Task<ErpInvoiceFromOrderResult?> CompletedConversionAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long orderId,
        CancellationToken cancellationToken)
    {
        var invoiceId = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_einvoice_documents` WHERE `order_id` = ? AND `doc_category` = 'tax_invoice'"
                + " AND `status` <> 'cancelled' AND `active` = 1 ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);

        if (invoiceId <= 0)
        {
            try
            {
                var soInvoiceId = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `sales_invoice_id` FROM `epc_erp_sales_orders` WHERE `shop_order_id` = ? ORDER BY `id` DESC LIMIT 1"),
                    cancellationToken,
                    orderId).ConfigureAwait(false);
                if (soInvoiceId > 0)
                {
                    invoiceId = soInvoiceId;
                }
            }
            catch (DbException)
            {
                // epc_erp_sales_orders is optional on legacy tenants.
            }
        }

        if (invoiceId <= 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT d.`invoice_number`, d.`subtotal_ex_vat`, d.`total_vat`, d.`total_incl_vat`, d.`paid_amount`, d.`amount_due`,"
            + " (SELECT MAX(j.`id`) FROM `epc_erp_gl_journals` j WHERE j.`source_type` = 'sales_invoice' AND j.`source_id` = d.`id` AND j.`active` = 1)"
            + " FROM `epc_einvoice_documents` d WHERE d.`id` = ? LIMIT 1");
        ErpDb.AddParameters(command, invoiceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ErpInvoiceFromOrderResult(
            orderId,
            invoiceId,
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
            reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
            reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
            reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
            reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
            0L,
            reader.IsDBNull(6) ? 0L : reader.GetInt64(6));
    }

    /// <summary>PHP <c>epc_einvoice_build_xml</c>.</summary>
    public static string BuildInvoiceXml(
        string uuid,
        string invoiceNumber,
        long issueDate,
        long dueDate,
        string? paymentMeansCode,
        string? bankAccount,
        IReadOnlyDictionary<string, string> seller,
        IReadOnlyDictionary<string, string> buyer,
        IReadOnlyList<ErpInvoiceFromOrderLine> lines,
        decimal subtotal,
        decimal totalVat,
        decimal totalIncl,
        decimal paidAmount,
        decimal amountDue,
        string taxCategory,
        decimal taxRate)
    {
        var cbc = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
        var cac = XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
        var buyerEndpoint = Value(buyer, "buyer_peppol_endpoint");
        var buyerTin = buyerEndpoint.Contains(':', StringComparison.Ordinal)
            ? buyerEndpoint[(buyerEndpoint.LastIndexOf(':') + 1)..]
            : string.Empty;

        var invoice = new XElement(
            XNamespace.Get("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2") + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", cac),
            new XAttribute(XNamespace.Xmlns + "cbc", cbc),
            new XElement(cbc + "CustomizationID", SpecificationId),
            new XElement(cbc + "ProfileID", BusinessProcess),
            new XElement(cbc + "ID", invoiceNumber),
            new XElement(cbc + "UUID", uuid),
            new XElement(cbc + "IssueDate", IsoDate(issueDate)),
            new XElement(cbc + "DueDate", IsoDate(dueDate)),
            new XElement(cbc + "InvoiceTypeCode", "380"),
            new XElement(cbc + "DocumentCurrencyCode", "AED"),
            new XElement(cbc + "TaxCurrencyCode", "AED"),
            new XElement(
                cac + "AccountingSupplierParty",
                new XElement(
                    cac + "Party",
                    new XElement(cbc + "EndpointID", TinFromTrn(Value(seller, "seller_trn")), new XAttribute("schemeID", ElectronicScheme)),
                    new XElement(cac + "PartyName", new XElement(cbc + "Name", Value(seller, "seller_name"))),
                    new XElement(
                        cac + "PostalAddress",
                        new XElement(cbc + "StreetName", Value(seller, "seller_address_line1")),
                        new XElement(cbc + "CityName", Value(seller, "seller_city")),
                        new XElement(cbc + "CountrySubentity", Value(seller, "seller_emirate")),
                        new XElement(cac + "Country", new XElement(cbc + "IdentificationCode", Value(seller, "seller_country_code").Length > 0 ? Value(seller, "seller_country_code") : "AE"))),
                    new XElement(
                        cac + "PartyTaxScheme",
                        new XElement(cbc + "CompanyID", Value(seller, "seller_trn")),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))),
                    new XElement(
                        cac + "PartyLegalEntity",
                        new XElement(cbc + "RegistrationName", Value(seller, "seller_name")),
                        new XElement(
                            cbc + "CompanyID",
                            Value(seller, "seller_legal_reg_no"),
                            new XAttribute("schemeID", Value(seller, "seller_legal_reg_type").Length > 0 ? Value(seller, "seller_legal_reg_type") : "TL"))))),
            new XElement(
                cac + "AccountingCustomerParty",
                new XElement(
                    cac + "Party",
                    new XElement(cbc + "EndpointID", buyerTin, new XAttribute("schemeID", ElectronicScheme)),
                    new XElement(cac + "PartyName", new XElement(cbc + "Name", Value(buyer, "buyer_name"))),
                    new XElement(
                        cac + "PostalAddress",
                        new XElement(cbc + "StreetName", Value(buyer, "buyer_address_line1")),
                        new XElement(cbc + "CityName", Value(buyer, "buyer_city")),
                        new XElement(cbc + "CountrySubentity", Value(buyer, "buyer_emirate")),
                        new XElement(cac + "Country", new XElement(cbc + "IdentificationCode", Value(buyer, "buyer_country_code").Length > 0 ? Value(buyer, "buyer_country_code") : "AE"))),
                    new XElement(
                        cac + "PartyTaxScheme",
                        new XElement(cbc + "CompanyID", Value(buyer, "buyer_trn")),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))))),
            new XElement(
                cac + "PaymentMeans",
                new XElement(cbc + "PaymentMeansCode", string.IsNullOrWhiteSpace(paymentMeansCode) ? "30" : paymentMeansCode),
                string.IsNullOrWhiteSpace(bankAccount)
                    ? null
                    : new XElement(cac + "PayeeFinancialAccount", new XElement(cbc + "ID", bankAccount))),
            new XElement(
                cac + "TaxTotal",
                new XElement(cbc + "TaxAmount", Amount(totalVat), new XAttribute("currencyID", "AED")),
                new XElement(
                    cac + "TaxSubtotal",
                    new XElement(cbc + "TaxableAmount", Amount(subtotal), new XAttribute("currencyID", "AED")),
                    new XElement(cbc + "TaxAmount", Amount(totalVat), new XAttribute("currencyID", "AED")),
                    new XElement(
                        cac + "TaxCategory",
                        new XElement(cbc + "ID", taxCategory),
                        new XElement(cbc + "Percent", Amount(taxRate)),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))))),
            new XElement(
                cac + "LegalMonetaryTotal",
                new XElement(cbc + "LineExtensionAmount", Amount(subtotal), new XAttribute("currencyID", "AED")),
                new XElement(cbc + "TaxExclusiveAmount", Amount(subtotal), new XAttribute("currencyID", "AED")),
                new XElement(cbc + "TaxInclusiveAmount", Amount(totalIncl), new XAttribute("currencyID", "AED")),
                new XElement(cbc + "PrepaidAmount", Amount(paidAmount), new XAttribute("currencyID", "AED")),
                new XElement(cbc + "PayableAmount", Amount(amountDue), new XAttribute("currencyID", "AED"))),
            lines.Select(line =>
                new XElement(
                    cac + "InvoiceLine",
                    new XElement(cbc + "ID", line.LineNo),
                    new XElement(cbc + "InvoicedQuantity", Qty(line.Quantity), new XAttribute("unitCode", line.UomCode)),
                    new XElement(cbc + "LineExtensionAmount", Amount(line.LineNet), new XAttribute("currencyID", "AED")),
                    new XElement(
                        cac + "Item",
                        new XElement(cbc + "Name", line.ItemName),
                        line.ItemDescription.Length > 0 ? new XElement(cbc + "Description", line.ItemDescription) : null,
                        new XElement(
                            cac + "ClassifiedTaxCategory",
                            new XElement(cbc + "ID", line.TaxCategory),
                            new XElement(cbc + "Percent", Amount(line.TaxRate)),
                            new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT")))),
                    new XElement(
                        cac + "Price",
                        new XElement(cbc + "PriceAmount", Qty(line.UnitPrice), new XAttribute("currencyID", "AED")))))
                .ToArray());

        return new XDocument(new XDeclaration("1.0", "utf-8", null), invoice).ToString(SaveOptions.DisableFormatting);
    }

    private static string SerializeTaxBreakdown(string taxCategory, decimal subtotal, decimal taxRate, decimal totalVat)
        => JsonSerializer.Serialize(new[]
        {
            new
            {
                tax_category = taxCategory,
                taxable_amount = subtotal,
                tax_rate = taxRate,
                tax_amount = totalVat,
                label = TaxCategoryLabels.TryGetValue(taxCategory, out var label) ? label : "Standard Rate",
            },
        });

    private static JsonElement? ParseHowGet(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string HowText(JsonElement? how, string key)
    {
        if (how is null || !how.Value.TryGetProperty(key, out var v))
        {
            return string.Empty;
        }

        return v.ValueKind == JsonValueKind.String ? (v.GetString() ?? string.Empty).Trim() : v.ToString().Trim();
    }

    private static async Task<string> EinvoicingSettingAsync(
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

    private static async Task<string> PricingSettingAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string key,
        string fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
            return value ?? fallback;
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_voucher_sequences` ("
            + " `voucher_type` varchar(8) NOT NULL,"
            + " `year` int(11) NOT NULL,"
            + " `last_seq` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`voucher_type`,`year`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP voucher sequences'",
            cancellationToken).ConfigureAwait(false);
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
            + " `advance_vat_credit` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_net_after_advance` decimal(14,2) NOT NULL DEFAULT 0.00,"
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
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_einvoice_documents` ADD `advance_vat_credit` decimal(14,2) NOT NULL DEFAULT 0.00",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_einvoice_documents` ADD `vat_net_after_advance` decimal(14,2) NOT NULL DEFAULT 0.00",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_uae_vat_advance` ADD `einvoice_document_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
    }

    private static string Value(IReadOnlyDictionary<string, string> source, string key)
        => source.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : (Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty);

    private static decimal DecimalValue(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private static string TinFromTrn(string trn)
    {
        var digits = Digits(trn);
        return digits.Length >= 10 ? digits[..10] : digits;
    }

    private static string PeppolEndpoint(string trnOrTin, string scheme)
    {
        var tin = TinFromTrn(trnOrTin);
        return tin.Length == 0 ? string.Empty : scheme + ":" + tin;
    }

    private static bool Truthy(string value) => value.Trim() is "" or "1" or "true";

    private static int ParseInt(string value, int fallback)
        => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string IsoDate(long timestamp)
        => DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Qty(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
