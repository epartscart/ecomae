using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_pos.php</c> twins for <c>open_session</c>, <c>close_session</c>,
/// <c>save_settings</c>, the POS sale/line INSERT in <c>complete_sale</c>,
/// <c>epc_pos_ensure_walkin_user</c>, tax-toolkit cart totals, and the
/// <c>epc_erp_sales_order_save</c> / <c>epc_erp_so_convert_to_invoice</c> /
/// <c>epc_erp_receipt_voucher</c> postings after cart totals.
/// Tax-toolkit assign stays PHP. Inventory <c>sale_out</c>, product/customer
/// search, and <c>calc_cart</c> are ASP.NET-live. Printable receipt HTML is ASP.NET-live.
/// </summary>
public interface ICpPosWriteService
{
    Task<ErpSimpleWriteResult> OpenSessionAsync(
        decimal openingFloat,
        int adminUserId,
        string? registerName,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CloseSessionAsync(
        long sessionId,
        decimal closingCash,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveSettingsAsync(
        bool posEnabled,
        string? registerName,
        int defaultWarehouseId,
        int defaultCashAccountId,
        int defaultCardAccountId,
        string? receiptHeader,
        string? receiptFooter,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CompleteSaleAsync(
        CpPosCompleteSaleWriteRequest request,
        int adminUserId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> EnsureWalkinUserAsync(CancellationToken cancellationToken = default);

    Task<CpPosReceipt> LoadReceiptAsync(long saleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpPosProductHit>> SearchProductsAsync(string? query, int limit = 30, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpPosCustomerHit>> SearchCustomersAsync(string? query, int limit = 15, CancellationToken cancellationToken = default);

    Task<CpPosCartCalcResult> CalcCartAsync(
        IReadOnlyList<CpPosSaleLineInput>? lines,
        long customerUserId,
        long contactId,
        CancellationToken cancellationToken = default);
}

public sealed record CpPosSaleLineInput(
    string? Name,
    decimal Qty = 1,
    decimal UnitPriceEx = 0,
    decimal LineDiscountPct = 0,
    decimal LineDiscountAmt = 0,
    string? Sku = null,
    string? Barcode = null,
    string? Source = null,
    string? Ref = null,
    decimal Price = 0);

public sealed record CpPosCompleteSaleWriteRequest(
    long SessionId = 0,
    IReadOnlyList<CpPosSaleLineInput>? Lines = null,
    string? PaymentMethod = null,
    decimal CashAmount = 0,
    decimal CardAmount = 0,
    decimal TaxRate = 0,
    string? TaxKitCode = null,
    long CustomerUserId = 0,
    long ContactId = 0,
    string? CustomerLabel = null,
    string? SaleNotes = null,
    long WarehouseId = 0);

public sealed class CpPosWriteService : ICpPosWriteService
{
    public const string WalkinEmail = "pos.walkin@local";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IOptions<EcomAeOptions>? _options;
    private readonly IErpTaxAmountCalculator? _tax;
    private readonly IErpSalesOrderWriteService? _salesOrders;
    private readonly IErpSalesInvoiceWriteService? _invoices;
    private readonly IErpCashWriteService? _cash;

    public CpPosWriteService(
        IErpWriteConnectionFactory connections,
        IOptions<EcomAeOptions>? options = null,
        IErpTaxAmountCalculator? tax = null,
        IErpSalesOrderWriteService? salesOrders = null,
        IErpSalesInvoiceWriteService? invoices = null,
        IErpCashWriteService? cash = null)
    {
        _connections = connections;
        _options = options;
        _tax = tax;
        _salesOrders = salesOrders;
        _invoices = invoices;
        _cash = cash;
    }

    public async Task<ErpSimpleWriteResult> OpenSessionAsync(
        decimal openingFloat,
        int adminUserId,
        string? registerName,
        CancellationToken cancellationToken = default)
    {
        if (openingFloat < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "opening_float cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var enabled = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`pos_enabled`,0) FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        if (enabled != 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "POS is disabled for this tenant");
        }

        var openNo = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `session_no` FROM `epc_pos_sessions` WHERE `status`='open' ORDER BY `opened_at` DESC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(openNo))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A register session is already open (" + openNo + ")");
        }

        var reg = Clip(registerName, 64);
        if (reg.Length == 0)
        {
            reg = Clip(await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `register_name` FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
                cancellationToken), 64);
        }

        if (reg.Length == 0)
        {
            reg = "Register 1";
        }

        var sessionNo = "REG-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-"
                        + Random.Shared.Next(1, 10000).ToString("0000", CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_pos_sessions` (`session_no`,`register_name`,`opened_by`,`opened_at`,`opening_float`,`status`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            sessionNo, reg, adminUserId, now, Math.Round(openingFloat, 2, MidpointRounding.AwayFromZero), "open")
            .ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Session " + sessionNo + " opened.", id);
    }

    public async Task<ErpSimpleWriteResult> CloseSessionAsync(
        long sessionId,
        decimal closingCash,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (closingCash < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "closing_cash cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (sessionId <= 0)
        {
            sessionId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_pos_sessions` WHERE `status`='open' ORDER BY `opened_at` DESC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }

        var status = sessionId > 0
            ? await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `status` FROM `epc_pos_sessions` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                sessionId).ConfigureAwait(false)
            : null;
        if (!string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Open session not found");
        }

        var opening = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`opening_float`,0) FROM `epc_pos_sessions` WHERE `id`=?"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var cashTotal = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COALESCE(SUM(`cash_amount`),0) FROM `epc_pos_sales` WHERE `session_id`=? AND `status`='completed'"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var salesCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM `epc_pos_sales` WHERE `session_id`=? AND `status`='completed'"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var salesTotal = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COALESCE(SUM(`total_amount`),0) FROM `epc_pos_sales` WHERE `session_id`=? AND `status`='completed'"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var expected = Math.Round(opening + cashTotal, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pos_sessions` SET `closed_at`=?, `closing_cash`=?, `expected_cash`=?, `sales_count`=?, `sales_total`=?, `status`='closed', `notes`=? WHERE `id`=?"),
            cancellationToken,
            now,
            Math.Round(closingCash, 2, MidpointRounding.AwayFromZero),
            expected,
            salesCount,
            Math.Round(salesTotal, 2, MidpointRounding.AwayFromZero),
            Clip(notes, 512),
            sessionId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Session closed.", sessionId);
    }

    public async Task<ErpSimpleWriteResult> SaveSettingsAsync(
        bool posEnabled,
        string? registerName,
        int defaultWarehouseId,
        int defaultCashAccountId,
        int defaultCardAccountId,
        string? receiptHeader,
        string? receiptFooter,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "POS settings row is missing. Schema ensure stays PHP.");
        }

        var footer = Clip(receiptFooter, 512);
        if (footer.Length == 0)
        {
            footer = "Thank you for your purchase";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                UPDATE `epc_pos_settings` SET
                  `pos_enabled`=?, `register_name`=?, `default_warehouse_id`=?,
                  `default_cash_account_id`=?, `default_card_account_id`=?,
                  `receipt_header`=?, `receipt_footer`=?, `time_updated`=?
                WHERE `id`=?
                """),
            cancellationToken,
            posEnabled ? 1 : 0,
            Clip(registerName, 64).Length == 0 ? "Register 1" : Clip(registerName, 64),
            Math.Max(0, defaultWarehouseId),
            Math.Max(0, defaultCashAccountId),
            Math.Max(0, defaultCardAccountId),
            Clip(receiptHeader, 512),
            footer,
            now,
            id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("POS settings saved.", id);
    }

    public async Task<ErpSimpleWriteResult> CompleteSaleAsync(
        CpPosCompleteSaleWriteRequest request,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = ParseLines(request.Lines);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cart is empty");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sessionId = request.SessionId;
        if (sessionId > 0)
        {
            var st = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `status` FROM `epc_pos_sessions` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                sessionId).ConfigureAwait(false);
            if (!string.Equals(st, "open", StringComparison.OrdinalIgnoreCase))
            {
                sessionId = 0;
            }
        }

        if (sessionId <= 0)
        {
            sessionId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_pos_sessions` WHERE `status`='open' ORDER BY `opened_at` DESC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }

        if (sessionId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Open a register session before completing a sale");
        }

        var customerUserId = request.CustomerUserId;
        var contactId = request.ContactId;
        var label = Clip(request.CustomerLabel, 255);
        if (customerUserId <= 0)
        {
            customerUserId = await EnsureWalkinUserCoreAsync(connection, cancellationToken).ConfigureAwait(false);
            if (contactId <= 0)
            {
                if (label.Length == 0)
                {
                    label = "Walk-in guest";
                }
            }
            else if (label.Length == 0)
            {
                label = Clip(
                    await ErpDb.StringAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COALESCE(NULLIF(TRIM(`name`),''), NULLIF(TRIM(`company`),''), 'Customer') FROM `epc_erp_contacts` WHERE `id`=? LIMIT 1"),
                        cancellationToken,
                        contactId).ConfigureAwait(false),
                    255);
                if (label.Length == 0)
                {
                    label = "Customer";
                }
            }
        }
        else if (label.Length == 0)
        {
            label = Clip(
                await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id`=? LIMIT 1"),
                    cancellationToken,
                    customerUserId).ConfigureAwait(false),
                255);
            if (label.Length == 0)
            {
                label = "Customer";
            }
        }

        var cart = SumCart(lines);
        var taxUserId = customerUserId > int.MaxValue ? int.MaxValue : (int)customerUserId;
        var taxContactId = contactId > int.MaxValue ? int.MaxValue : (int)contactId;
        decimal taxRate;
        decimal vat;
        decimal total;
        var kitCode = Clip(request.TaxKitCode, 32);
        if (_tax is not null)
        {
            var header = await _tax.CalcAsync(
                connection,
                null,
                cart.AmountEx,
                taxUserId,
                taxContactId,
                false,
                cancellationToken).ConfigureAwait(false);
            taxRate = header.TaxRate;
            vat = header.VatAmount;
            total = header.TotalAmount;
            if (kitCode.Length == 0)
            {
                kitCode = Clip(header.KitCode, 32);
            }
        }
        else
        {
            taxRate = request.TaxRate < 0 ? 0 : request.TaxRate;
            vat = Math.Round(cart.AmountEx * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
            total = Math.Round(cart.AmountEx + vat, 2, MidpointRounding.AwayFromZero);
        }

        var method = (request.PaymentMethod ?? "cash").Trim().ToLowerInvariant();
        if (method is not ("cash" or "card" or "split"))
        {
            method = "cash";
        }

        decimal cash = Math.Round(request.CashAmount, 2, MidpointRounding.AwayFromZero);
        decimal card = Math.Round(request.CardAmount, 2, MidpointRounding.AwayFromZero);
        if (method == "cash")
        {
            cash = total;
            card = 0;
        }
        else if (method == "card")
        {
            card = total;
            cash = 0;
        }
        else
        {
            if (cash + card <= 0)
            {
                cash = total;
            }

            if (Math.Abs(cash + card - total) > 0.02m)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Split payment must equal total");
            }
        }

        var saleNo = await NextSaleNoAsync(connection, cancellationToken).ConfigureAwait(false);
        long salesOrderId = 0;
        long salesInvoiceId = 0;
        var receiptVoucherNo = "";
        if (_salesOrders is not null && _invoices is not null && _cash is not null)
        {
            try
            {
                (salesOrderId, salesInvoiceId, receiptVoucherNo) = await PostErpDocumentsAsync(
                    connection,
                    saleNo,
                    customerUserId,
                    contactId,
                    lines,
                    cash,
                    card,
                    request.SaleNotes,
                    adminUserId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ErpWriteException ex)
            {
                return ErpSimpleWriteResult.Fail("invalid", ex.Message);
            }
            catch (System.Data.Common.DbException ex)
            {
                return ErpSimpleWriteResult.Fail("invalid", ex.Message);
            }
        }

        await TryPostSaleOutAsync(
            connection,
            request.WarehouseId,
            saleNo,
            salesOrderId,
            lines,
            adminUserId,
            cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                """
                INSERT INTO `epc_pos_sales`
                (`session_id`,`sale_no`,`customer_user_id`,`contact_id`,`customer_label`,`sales_order_id`,`sales_invoice_id`,
                 `receipt_voucher_no`,`subtotal_ex`,`discount_total`,`vat_amount`,`total_amount`,`payment_method`,
                 `cash_amount`,`card_amount`,`tax_kit_code`,`tax_rate`,`admin_id`,`time_created`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            sessionId, saleNo, customerUserId, contactId, label, salesOrderId, salesInvoiceId, receiptVoucherNo,
            cart.SubtotalEx, cart.DiscountTotal, vat, total, method, cash, card,
            kitCode, taxRate, adminUserId, now).ConfigureAwait(false);
        var saleId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var lineNo = 1;
        foreach (var line in lines)
        {
            decimal lineVat;
            decimal lineTotal;
            var lineRate = taxRate;
            if (_tax is not null)
            {
                var lineTax = await _tax.CalcAsync(
                    connection,
                    tx,
                    line.LineExVat,
                    taxUserId,
                    taxContactId,
                    false,
                    cancellationToken).ConfigureAwait(false);
                lineRate = lineTax.TaxRate;
                lineVat = lineTax.VatAmount;
                lineTotal = lineTax.TotalAmount;
            }
            else
            {
                lineVat = Math.Round(line.LineExVat * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
                lineTotal = Math.Round(line.LineExVat + lineVat, 2, MidpointRounding.AwayFromZero);
            }
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_pos_sale_lines`
                    (`sale_id`,`line_no`,`product_source`,`product_ref`,`sku`,`barcode`,`name`,`qty`,`unit_price_ex`,
                     `line_discount_pct`,`line_discount_amt`,`line_ex_vat`,`tax_rate`,`vat_amount`,`line_total`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                saleId, lineNo, line.Source, line.Ref, line.Sku, line.Barcode, line.Name, line.Qty, line.UnitPriceEx,
                line.DiscountPct, line.DiscountAmt, line.LineExVat, lineRate, lineVat, lineTotal).ConfigureAwait(false);
            lineNo++;
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Sale " + saleNo + " completed.", saleId);
    }

    /// <summary>
    /// PHP <c>epc_pos_complete_sale</c> after cart totals: SO save → confirm → invoice → cash/card RV.
    /// Inventory <c>sale_out</c> is posted separately (best-effort).
    /// </summary>
    private async Task<(long SalesOrderId, long SalesInvoiceId, string ReceiptVoucherNo)> PostErpDocumentsAsync(
        System.Data.Common.DbConnection connection,
        string saleNo,
        long customerUserId,
        long contactId,
        IReadOnlyList<CpPosParsedLine> lines,
        decimal cash,
        decimal card,
        string? saleNotes,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        var soNotes = "POS sale " + saleNo;
        var extra = Clip(saleNotes, 240);
        if (extra.Length > 0)
        {
            soNotes += " — " + extra;
        }

        var saved = await _salesOrders!.SaveAsync(
            new ErpSalesOrderInput
            {
                CustomerUserId = ClampInt(customerUserId),
                ContactId = ClampInt(contactId),
                Title = "POS " + saleNo,
                Status = "confirmed",
                Notes = soNotes,
                LinesJson = BuildSoLinesJson(lines),
            },
            adminUserId,
            cancellationToken).ConfigureAwait(false);
        await _salesOrders.SetStatusAsync(saved.Id, "confirmed", adminUserId, cancellationToken).ConfigureAwait(false);
        var invoice = await _invoices!.ConvertSalesOrderAsync(saved.Id, adminUserId, cancellationToken).ConfigureAwait(false);

        var settingsCash = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`default_cash_account_id`,0) FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        var settingsCard = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`default_card_account_id`,0) FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        var accounts = await ListActiveCashAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
        var cashAccount = PickDefaultCashAccount(settingsCash, accounts);
        var cardAccount = PickDefaultCardAccount(settingsCard, accounts, cashAccount);

        var rvNo = "";
        if (cash > 0 && cashAccount > 0)
        {
            var rv = await _cash!.ReceiptVoucherAsync(
                new ErpReceiptVoucherInput
                {
                    UserId = ClampInt(customerUserId),
                    AccountId = cashAccount,
                    Amount = cash,
                    SalesOrderId = saved.Id,
                    SalesInvoiceId = invoice.SalesInvoiceId,
                    Reference = saleNo + "-CASH",
                    Note = "POS cash payment " + saleNo,
                    PostGl = true,
                },
                adminUserId,
                cancellationToken).ConfigureAwait(false);
            rvNo = rv.VoucherNo ?? "";
        }

        if (card > 0 && cardAccount > 0)
        {
            var rvCard = await _cash!.ReceiptVoucherAsync(
                new ErpReceiptVoucherInput
                {
                    UserId = ClampInt(customerUserId),
                    AccountId = cardAccount,
                    Amount = card,
                    SalesOrderId = saved.Id,
                    SalesInvoiceId = invoice.SalesInvoiceId,
                    Reference = saleNo + "-CARD",
                    Note = "POS card payment " + saleNo,
                    PostGl = true,
                },
                adminUserId,
                cancellationToken).ConfigureAwait(false);
            if (rvNo.Length == 0)
            {
                rvNo = rvCard.VoucherNo ?? "";
            }
        }

        return (saved.Id, invoice.SalesInvoiceId, rvNo);
    }

    /// <summary>
    /// PHP <c>epc_pos_complete_sale</c> inventory loop: warehouse pick, SKU resolve/create,
    /// <c>sale_out</c> movement. Exceptions are swallowed so a short-stock line does not abort the sale.
    /// Schema-ensure stays PHP.
    /// </summary>
    private async Task TryPostSaleOutAsync(
        System.Data.Common.DbConnection connection,
        long requestedWarehouseId,
        string saleNo,
        long salesOrderId,
        IReadOnlyList<CpPosParsedLine> lines,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        int warehouseId;
        try
        {
            warehouseId = await ResolveWarehouseIdAsync(connection, requestedWarehouseId, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            return;
        }

        if (warehouseId <= 0)
        {
            return;
        }

        foreach (var line in lines)
        {
            if (!HasSaleOutSku(line.Sku))
            {
                continue;
            }

            try
            {
                var itemId = await ResolveOrCreateItemAsync(connection, line.Sku, line.Name, cancellationToken).ConfigureAwait(false);
                if (itemId <= 0)
                {
                    continue;
                }

                await RecordSaleOutAsync(
                    connection,
                    warehouseId,
                    itemId,
                    line.Qty,
                    saleNo,
                    salesOrderId,
                    adminUserId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ErpWriteException)
            {
            }
            catch (System.Data.Common.DbException)
            {
            }
        }
    }

    /// <summary>PHP warehouse pick: payload → settings → first active warehouse.</summary>
    public static int PickWarehouseId(long requestedWarehouseId, long settingsWarehouseId, long firstActiveWarehouseId)
    {
        if (requestedWarehouseId > 0)
        {
            return ClampInt(requestedWarehouseId);
        }

        if (settingsWarehouseId > 0)
        {
            return ClampInt(settingsWarehouseId);
        }

        return firstActiveWarehouseId > 0 ? ClampInt(firstActiveWarehouseId) : 0;
    }

    /// <summary>PHP skips inventory when the cart line has no SKU.</summary>
    public static bool HasSaleOutSku(string? sku)
        => !string.IsNullOrWhiteSpace(sku);

    private async Task<int> ResolveWarehouseIdAsync(
        System.Data.Common.DbConnection connection,
        long requestedWarehouseId,
        CancellationToken cancellationToken)
    {
        var settingsWarehouse = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`default_warehouse_id`,0) FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        long firstActive = 0;
        try
        {
            firstActive = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `active` = 1 ORDER BY `id` ASC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            firstActive = 0;
        }

        return PickWarehouseId(requestedWarehouseId, settingsWarehouse, firstActive);
    }

    private static async Task<long> ResolveOrCreateItemAsync(
        System.Data.Common.DbConnection connection,
        string sku,
        string name,
        CancellationToken cancellationToken)
    {
        var trimmed = Clip(sku, 64);
        if (trimmed.Length == 0)
        {
            return 0;
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_items` WHERE `sku` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            trimmed).ConfigureAwait(false);
        if (existing > 0)
        {
            return existing;
        }

        var itemName = Clip(name, 255);
        if (itemName.Length == 0)
        {
            itemName = trimmed;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_inv_items` (`sku`, `name`, `product_id`, `item_type`, `track_expiry`, `unit`, `time_created`)"
                + " VALUES (?,?,0,'standard',0,'pcs',?)"),
            cancellationToken,
            trimmed,
            itemName,
            now).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RecordSaleOutAsync(
        System.Data.Common.DbConnection connection,
        int warehouseId,
        long itemId,
        decimal qty,
        string saleNo,
        long salesOrderId,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        var qtyAbs = Math.Abs(qty);
        if (warehouseId <= 0 || itemId <= 0 || qtyAbs == 0m)
        {
            throw new ErpWriteException("Warehouse, item and quantity required");
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var stockCmd = connection.CreateCommand();
            stockCmd.Transaction = tx;
            stockCmd.CommandText = ErpDb.Positional(
                "SELECT `id`, `qty_on_hand`, `avg_unit_cost` FROM `epc_erp_inv_stock`"
                + " WHERE `warehouse_id` = ? AND `item_id` = ?"
                + " AND IFNULL(`batch_no`,'') = '' AND IFNULL(`variant_label`,'') = ''"
                + " LIMIT 1 FOR UPDATE");
            ErpDb.AddParameters(stockCmd, warehouseId, itemId);
            long stockId = 0;
            decimal onHand = 0;
            decimal avgCost = 0;
            await using (var reader = await stockCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new ErpWriteException("Insufficient quantity on hand");
                }

                stockId = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                onHand = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                avgCost = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
            }

            if (stockId <= 0 || onHand < qtyAbs)
            {
                throw new ErpWriteException("Insufficient quantity on hand");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var newQty = onHand - qtyAbs;
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "UPDATE `epc_erp_inv_stock` SET `qty_on_hand` = ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                newQty,
                now,
                stockId).ConfigureAwait(false);

            var totalCost = Math.Round(qtyAbs * avgCost, 2, MidpointRounding.AwayFromZero);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_inv_movements`"
                    + " (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`transfer_warehouse_id`,"
                    + " `purchase_id`,`order_id`,`batch_no`,`expiry_date`,`reference`,`note`,`movement_date`,`admin_id`,`opening_batch_id`)"
                    + " VALUES ('sale_out',?,?,?,?,?,0,0,?,NULL,NULL,?,?,?,?,0)"),
                cancellationToken,
                warehouseId,
                itemId,
                qtyAbs,
                avgCost,
                totalCost,
                salesOrderId,
                saleNo,
                "POS sale",
                now,
                adminUserId).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>PHP <c>lines_json</c> for POS → <c>epc_erp_sales_order_save</c>.</summary>
    public static string BuildSoLinesJson(IReadOnlyList<CpPosParsedLine> lines)
    {
        var payload = new List<Dictionary<string, object?>>();
        foreach (var line in lines)
        {
            payload.Add(new Dictionary<string, object?>
            {
                ["description"] = line.Name,
                ["qty"] = line.Qty,
                ["unit_price_ex_vat"] = line.UnitPriceEx,
                ["line_ex_vat"] = line.LineExVat,
            });
        }

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>PHP <c>epc_pos_default_cash_account</c> picker after settings / list.</summary>
    public static int PickDefaultCashAccount(int settingsAccountId, IReadOnlyList<CpPosCashAccountHint> accounts)
    {
        if (settingsAccountId > 0)
        {
            return settingsAccountId;
        }

        foreach (var account in accounts)
        {
            if (account.Name.Contains("cash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(account.AccountType, "cash", StringComparison.Ordinal))
            {
                return account.Id;
            }
        }

        return accounts.Count > 0 ? accounts[0].Id : 0;
    }

    /// <summary>PHP <c>epc_pos_default_card_account</c> picker after settings / list.</summary>
    public static int PickDefaultCardAccount(
        int settingsAccountId,
        IReadOnlyList<CpPosCashAccountHint> accounts,
        int cashFallback)
    {
        if (settingsAccountId > 0)
        {
            return settingsAccountId;
        }

        foreach (var account in accounts)
        {
            if (account.Name.Contains("card", StringComparison.OrdinalIgnoreCase)
                || account.Name.Contains("bank", StringComparison.OrdinalIgnoreCase))
            {
                return account.Id;
            }
        }

        return cashFallback;
    }

    private static async Task<List<CpPosCashAccountHint>> ListActiveCashAccountsAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var accounts = new List<CpPosCashAccountHint>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, `name`, `account_type` FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1 ORDER BY `account_type`, `name`");
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                accounts.Add(new CpPosCashAccountHint(
                    reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                    reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? ""));
            }
        }
        catch (System.Data.Common.DbException)
        {
            return accounts;
        }

        return accounts;
    }

    private static int ClampInt(long value)
        => value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;

    /// <summary>PHP <c>epc_pos_calc_cart_totals</c> header: gross qty×unit, then discount, then amount ex.</summary>
    public static CpPosCartTotals SumCart(IReadOnlyList<CpPosParsedLine> lines)
    {
        decimal subtotal = 0;
        decimal discountTotal = 0;
        foreach (var line in lines)
        {
            subtotal += Math.Round(line.Qty * line.UnitPriceEx, 2, MidpointRounding.AwayFromZero);
            discountTotal += line.DiscountAmt;
        }

        subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        discountTotal = Math.Round(discountTotal, 2, MidpointRounding.AwayFromZero);
        return new(subtotal, discountTotal, Math.Round(subtotal - discountTotal, 2, MidpointRounding.AwayFromZero));
    }

    public static IReadOnlyList<CpPosParsedLine> ParseLines(IReadOnlyList<CpPosSaleLineInput>? raw)
    {
        var parsed = new List<CpPosParsedLine>();
        if (raw is null)
        {
            return parsed;
        }

        foreach (var ln in raw)
        {
            var name = Clip(ln.Name, 255);
            if (name.Length == 0)
            {
                continue;
            }

            var qty = ln.Qty < 0.001m ? 0.001m : ln.Qty;
            var unitRaw = ln.UnitPriceEx > 0 ? ln.UnitPriceEx : ln.Price;
            var unit = Math.Round(Math.Max(0, unitRaw), 4, MidpointRounding.AwayFromZero);
            var discPct = Math.Clamp(ln.LineDiscountPct, 0, 100);
            var discAmt = Math.Round(Math.Max(0, ln.LineDiscountAmt), 2, MidpointRounding.AwayFromZero);
            var gross = Math.Round(qty * unit, 2, MidpointRounding.AwayFromZero);
            if (discPct > 0)
            {
                discAmt = Math.Round(gross * discPct / 100m, 2, MidpointRounding.AwayFromZero);
            }

            var net = Math.Round(Math.Max(0, gross - discAmt), 2, MidpointRounding.AwayFromZero);
            parsed.Add(new CpPosParsedLine(
                Clip(ln.Source, 16).Length == 0 ? "manual" : Clip(ln.Source, 16),
                Clip(ln.Ref, 64),
                Clip(ln.Sku, 64),
                Clip(ln.Barcode, 64).Length == 0 ? Clip(ln.Sku, 64) : Clip(ln.Barcode, 64),
                name,
                qty,
                unit,
                discPct,
                discAmt,
                net));
        }

        return parsed;
    }

    public static IReadOnlyList<CpPosSaleLineInput> ParseLinesJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var raw = new List<CpPosSaleLineInput>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                raw.Add(new CpPosSaleLineInput(
                    ReadString(el, "name"),
                    ReadDecimal(el, 1, "qty"),
                    ReadDecimal(el, 0, "unit_price_ex", "unitPriceEx", "UnitPriceEx"),
                    ReadDecimal(el, 0, "line_discount_pct", "lineDiscountPct", "LineDiscountPct"),
                    ReadDecimal(el, 0, "line_discount_amt", "lineDiscountAmt", "LineDiscountAmt"),
                    ReadString(el, "sku"),
                    ReadString(el, "barcode"),
                    ReadString(el, "source"),
                    ReadString(el, "ref"),
                    ReadDecimal(el, 0, "price")));
            }

            return raw;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static decimal ReadDecimal(JsonElement el, decimal fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var value))
            {
                return value;
            }

            if (prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static async Task<string> NextSaleNoAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var seq = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `last_seq` FROM `epc_pos_sequences` WHERE `year`=? LIMIT 1"),
                cancellationToken,
                year).ConfigureAwait(false);
            if (seq <= 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional(
                        "INSERT INTO `epc_pos_sequences` (`year`,`last_seq`) VALUES (?,1) ON DUPLICATE KEY UPDATE `last_seq`=`last_seq`+1"),
                    cancellationToken,
                    year).ConfigureAwait(false);
                seq = 1;
            }
            else
            {
                seq++;
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `epc_pos_sequences` SET `last_seq`=? WHERE `year`=?"),
                    cancellationToken,
                    seq, year).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return "POS-" + year.ToString(CultureInfo.InvariantCulture) + "-"
                   + seq.ToString("00000", CultureInfo.InvariantCulture);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ErpSimpleWriteResult> EnsureWalkinUserAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var uid = await EnsureWalkinUserCoreAsync(connection, cancellationToken).ConfigureAwait(false);
        return uid > 0
            ? ErpSimpleWriteResult.Ok("Walk-in user ready.", uid)
            : ErpSimpleWriteResult.Fail("invalid", "Walk-in user could not be created.");
    }

    /// <summary>PHP <c>md5(bin2hex(random_bytes(8)) . $secret_succession)</c>.</summary>
    public static string HashWalkinPassword(string randomHex, string? secretSuccession)
        => LegacyPasswordVerifier.Md5Hex((randomHex ?? string.Empty) + (secretSuccession ?? string.Empty));

    private async Task<long> EnsureWalkinUserCoreAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var settingsId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        var storedWalkin = settingsId > 0
            ? await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `walkin_user_id` FROM `epc_pos_settings` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                settingsId).ConfigureAwait(false)
            : 0L;
        if (storedWalkin > 0)
        {
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `user_id`=? LIMIT 1"),
                cancellationToken,
                storedWalkin).ConfigureAwait(false);
            if (exists > 0)
            {
                return storedWalkin;
            }
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `email`=? LIMIT 1"),
            cancellationToken,
            WalkinEmail).ConfigureAwait(false);
        if (existing > 0)
        {
            await RememberWalkinUserAsync(connection, settingsId, existing, cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var hash = HashWalkinPassword(randomHex, _options?.Value.SecretSuccession);
        long uid;
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`)
                    VALUES (?, 1, ?, 1, 1, ?, 1)
                    """),
                cancellationToken,
                WalkinEmail, hash, now).ConfigureAwait(false);
            uid = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            uid = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `email`=? LIMIT 1"),
                cancellationToken,
                WalkinEmail).ConfigureAwait(false);
        }

        if (uid > 0)
        {
            await RememberWalkinUserAsync(connection, settingsId, uid, cancellationToken).ConfigureAwait(false);
        }

        return uid;
    }

    private static async Task RememberWalkinUserAsync(
        System.Data.Common.DbConnection connection,
        long settingsId,
        long userId,
        CancellationToken cancellationToken)
    {
        if (settingsId <= 0 || userId <= 0)
        {
            return;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_pos_settings` SET `walkin_user_id`=? WHERE `id`=?"),
            cancellationToken,
            userId, settingsId).ConfigureAwait(false);
    }

    public async Task<CpPosReceipt> LoadReceiptAsync(long saleId, CancellationToken cancellationToken = default)
    {
        if (saleId <= 0)
        {
            return CpPosReceipt.NotFound;
        }

        if (!_connections.IsConfigured)
        {
            return CpPosReceipt.NotFound;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var saleCmd = connection.CreateCommand();
        saleCmd.CommandText = ErpDb.Positional(
            """
            SELECT `id`, `sale_no`, `customer_label`, `subtotal_ex`, `discount_total`, `vat_amount`,
                   `total_amount`, `payment_method`, `tax_rate`, `sales_invoice_id`, `time_created`
            FROM `epc_pos_sales` WHERE `id` = ? LIMIT 1
            """);
        ErpDb.AddParameters(saleCmd, saleId);
        long invoiceId = 0;
        string saleNo = "";
        string customerLabel = "";
        decimal subtotalEx = 0;
        decimal discountTotal = 0;
        decimal vatAmount = 0;
        decimal totalAmount = 0;
        string paymentMethod = "";
        decimal taxRate = 0;
        long timeCreated = 0;
        await using (var reader = await saleCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return CpPosReceipt.NotFound;
            }

            saleNo = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
            customerLabel = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
            subtotalEx = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
            discountTotal = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
            vatAmount = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture);
            totalAmount = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture);
            paymentMethod = reader.IsDBNull(7) ? "" : reader.GetValue(7)?.ToString() ?? "";
            taxRate = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader.GetValue(8), CultureInfo.InvariantCulture);
            invoiceId = reader.IsDBNull(9) ? 0 : Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture);
            timeCreated = reader.IsDBNull(10) ? 0 : Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture);
        }

        var lines = new List<CpPosReceiptLine>();
        await using var lineCmd = connection.CreateCommand();
        lineCmd.CommandText = ErpDb.Positional(
            "SELECT `name`, `qty`, `line_discount_amt`, `line_total` FROM `epc_pos_sale_lines` WHERE `sale_id` = ? ORDER BY `line_no`");
        ErpDb.AddParameters(lineCmd, saleId);
        await using (var lineReader = await lineCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await lineReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = lineReader.IsDBNull(0) ? "" : lineReader.GetValue(0)?.ToString() ?? "";
                var qty = lineReader.IsDBNull(1) ? 0 : Convert.ToDecimal(lineReader.GetValue(1), CultureInfo.InvariantCulture);
                var disc = lineReader.IsDBNull(2) ? 0 : Convert.ToDecimal(lineReader.GetValue(2), CultureInfo.InvariantCulture);
                var lineTotal = lineReader.IsDBNull(3) ? 0 : Convert.ToDecimal(lineReader.GetValue(3), CultureInfo.InvariantCulture);
                lines.Add(new CpPosReceiptLine(FormatQty(qty), name, disc, lineTotal));
            }
        }

        var header = "";
        var footer = "Thank you for your purchase";
        try
        {
            header = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`receipt_header`,'') FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
                cancellationToken).ConfigureAwait(false) ?? "";
            var storedFooter = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`receipt_footer`,'') FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(storedFooter))
            {
                footer = storedFooter.Trim();
            }
        }
        catch (System.Data.Common.DbException)
        {
            // Settings stay optional; PHP defaults apply.
        }

        var invoiceNumber = "";
        if (invoiceId > 0)
        {
            try
            {
                invoiceNumber = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `invoice_number` FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    invoiceId).ConfigureAwait(false) ?? "";
            }
            catch (System.Data.Common.DbException)
            {
                invoiceNumber = "";
            }
        }

        var soldAt = timeCreated > 0
            ? DateTimeOffset.FromUnixTimeSeconds(timeCreated).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "";
        return new CpPosReceipt(
            true,
            "",
            saleNo,
            soldAt,
            customerLabel,
            header.Trim(),
            footer,
            "VAT",
            discountTotal,
            subtotalEx,
            taxRate,
            vatAmount,
            totalAmount,
            TitleCase(paymentMethod),
            invoiceNumber.Trim(),
            lines);
    }

    public async Task<IReadOnlyList<CpPosProductHit>> SearchProductsAsync(
        string? query,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var q = NormalizeSearchQuery(query);
        if (q.Length == 0 || !_connections.IsConfigured)
        {
            return [];
        }

        limit = ClampProductLimit(limit);
        var like = "%" + q + "%";
        var exact = NormalizeArticleExact(q);
        var hits = new List<CpPosProductHit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `id`, `manufacturer`, COALESCE(NULLIF(`article_show`, ''), `article`) AS `article`,"
                + " `name`, `price`, `exist`, `storage` FROM `shop_docpart_prices_data`"
                + " WHERE (`name` LIKE ? OR `article` LIKE ? OR `article_show` LIKE ? OR `manufacturer` LIKE ?"
                + " OR UPPER(REPLACE(`article`, ' ', '')) = ?) AND IFNULL(`price`, 0) > 0"
                + " ORDER BY `name` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(cmd, like, like, like, like, exact);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.IsDBNull(0) ? "0" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "0";
                var key = "pd_" + id;
                if (!seen.Add(key))
                {
                    continue;
                }

                var sku = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString()?.Trim() ?? "";
                hits.Add(new CpPosProductHit(
                    "price_data",
                    id,
                    sku,
                    sku,
                    reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString() ?? "",
                    reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                    reader.IsDBNull(4) ? 0 : Math.Round(Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture), 4, MidpointRounding.AwayFromZero),
                    reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    reader.IsDBNull(6) ? "" : reader.GetValue(6)?.ToString() ?? ""));
            }
        }
        catch (System.Data.Common.DbException)
        {
        }

        if (hits.Count < limit)
        {
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, `caption`, `alias`, `price` FROM `shop_catalogue_products`"
                    + " WHERE (`caption` LIKE ? OR `alias` LIKE ?) AND `published_flag` = 1"
                    + " ORDER BY `caption` ASC LIMIT " + (limit - hits.Count).ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(cmd, like, like);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = reader.IsDBNull(0) ? "0" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "0";
                    var key = "cat_" + id;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var sku = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
                    hits.Add(new CpPosProductHit(
                        "catalog",
                        id,
                        sku,
                        sku,
                        reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                        "",
                        reader.IsDBNull(3) ? 0 : Math.Round(Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture), 4, MidpointRounding.AwayFromZero),
                        null,
                        ""));
                }
            }
            catch (System.Data.Common.DbException)
            {
            }
        }

        if (hits.Count < limit)
        {
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ErpDb.Positional(
                    "SELECT i.`id`, i.`sku`, i.`name`, s.`qty_on_hand` FROM `epc_erp_inv_items` i"
                    + " LEFT JOIN `epc_erp_inv_stock` s ON s.`item_id` = i.`id`"
                    + " WHERE i.`active` = 1 AND (i.`sku` LIKE ? OR i.`name` LIKE ? OR i.`sku` = ?)"
                    + " GROUP BY i.`id` ORDER BY i.`name` ASC LIMIT "
                    + (limit - hits.Count).ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(cmd, like, like, q);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = reader.IsDBNull(0) ? "0" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "0";
                    var key = "inv_" + id;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var sku = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                    hits.Add(new CpPosProductHit(
                        "inventory",
                        id,
                        sku,
                        sku,
                        reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "",
                        "",
                        0,
                        reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                        ""));
                }
            }
            catch (System.Data.Common.DbException)
            {
            }
        }

        return hits;
    }

    public async Task<IReadOnlyList<CpPosCustomerHit>> SearchCustomersAsync(
        string? query,
        int limit = 15,
        CancellationToken cancellationToken = default)
    {
        var q = NormalizeSearchQuery(query);
        if (q.Length == 0 || !_connections.IsConfigured)
        {
            return [];
        }

        var userLimit = Math.Max(1, Math.Min(30, limit));
        var contactLimit = Math.Max(1, Math.Min(20, limit));
        var like = "%" + q + "%";
        var hits = new List<CpPosCustomerHit>();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT u.`user_id`, u.`email`, p.`surname`, p.`name`, p.`phone`"
                + " FROM `users` u LEFT JOIN `users_profile` p ON p.`user_id` = u.`user_id`"
                + " WHERE u.`email` LIKE ? OR p.`phone` LIKE ? OR p.`name` LIKE ? OR p.`surname` LIKE ?"
                + " ORDER BY u.`user_id` DESC LIMIT " + userLimit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(cmd, like, like, like, like);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var email = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                var name = reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString() ?? "";
                var surname = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
                var label = (name + " " + surname).Trim();
                if (label.Length == 0)
                {
                    label = email;
                }

                hits.Add(new CpPosCustomerHit(
                    "user",
                    reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    0,
                    label,
                    email,
                    reader.IsDBNull(4) ? "" : reader.GetValue(4)?.ToString() ?? ""));
            }
        }
        catch (System.Data.Common.DbException)
        {
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `id`, `name`, `company`, `email`, `phone` FROM `epc_erp_contacts`"
                + " WHERE `party_type` IN ('customer','both') AND (`name` LIKE ? OR `company` LIKE ? OR `email` LIKE ? OR `phone` LIKE ?)"
                + " ORDER BY `name` ASC LIMIT " + contactLimit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(cmd, like, like, like, like);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                var company = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
                var label = name.Trim().Length > 0 ? name.Trim() : company.Trim();
                hits.Add(new CpPosCustomerHit(
                    "contact",
                    0,
                    reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    label,
                    reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString() ?? "",
                    reader.IsDBNull(4) ? "" : reader.GetValue(4)?.ToString() ?? ""));
            }
        }
        catch (System.Data.Common.DbException)
        {
        }

        return hits;
    }

    public async Task<CpPosCartCalcResult> CalcCartAsync(
        IReadOnlyList<CpPosSaleLineInput>? lines,
        long customerUserId,
        long contactId,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseLines(lines);
        if (!_connections.IsConfigured)
        {
            return CpPosCartCalcResult.Fail("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (customerUserId <= 0 && contactId <= 0)
        {
            customerUserId = await EnsureWalkinUserCoreAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var cart = SumCart(parsed);
        var taxUserId = ClampInt(customerUserId);
        var taxContactId = ClampInt(contactId);
        decimal taxRate = 0;
        decimal vat = 0;
        decimal total = cart.AmountEx;
        var kitCode = "";
        var taxLabel = "VAT";
        var detail = new List<CpPosCartCalcLine>();
        foreach (var line in parsed)
        {
            decimal lineVat;
            decimal lineTotal;
            var lineRate = taxRate;
            if (_tax is not null)
            {
                var lineTax = await _tax.CalcAsync(
                    connection,
                    null,
                    line.LineExVat,
                    taxUserId,
                    taxContactId,
                    false,
                    cancellationToken).ConfigureAwait(false);
                lineRate = lineTax.TaxRate;
                lineVat = lineTax.VatAmount;
                lineTotal = lineTax.TotalAmount;
                if (kitCode.Length == 0)
                {
                    kitCode = Clip(lineTax.KitCode, 32);
                    taxLabel = string.IsNullOrWhiteSpace(lineTax.TaxLabel) ? "VAT" : lineTax.TaxLabel;
                    taxRate = lineTax.TaxRate;
                }
            }
            else
            {
                lineVat = 0;
                lineTotal = line.LineExVat;
            }

            detail.Add(new CpPosCartCalcLine(
                line.Name, line.Qty, line.UnitPriceEx, line.DiscountAmt, line.LineExVat, lineRate, lineVat, lineTotal));
        }

        if (_tax is not null)
        {
            var header = await _tax.CalcAsync(
                connection,
                null,
                cart.AmountEx,
                taxUserId,
                taxContactId,
                false,
                cancellationToken).ConfigureAwait(false);
            taxRate = header.TaxRate;
            vat = header.VatAmount;
            total = header.TotalAmount;
            kitCode = Clip(header.KitCode, 32);
            taxLabel = string.IsNullOrWhiteSpace(header.TaxLabel) ? "VAT" : header.TaxLabel;
        }

        return new CpPosCartCalcResult(
            true,
            "",
            detail,
            cart.SubtotalEx,
            cart.DiscountTotal,
            cart.AmountEx,
            vat,
            total,
            taxRate,
            taxLabel,
            kitCode);
    }

    /// <summary>PHP <c>trim($q)</c> for POS search.</summary>
    public static string NormalizeSearchQuery(string? query)
        => (query ?? string.Empty).Trim();

    /// <summary>PHP <c>preg_replace('/\\s+/', '', strtoupper($q))</c>.</summary>
    public static string NormalizeArticleExact(string query)
    {
        var chars = new char[query.Length];
        var n = 0;
        foreach (var ch in query)
        {
            if (!char.IsWhiteSpace(ch))
            {
                chars[n++] = char.ToUpperInvariant(ch);
            }
        }

        return new string(chars, 0, n);
    }

    /// <summary>PHP product search clamp 1..50.</summary>
    public static int ClampProductLimit(int limit)
        => Math.Max(1, Math.Min(50, limit));

    /// <summary>PHP <c>rtrim(rtrim(number_format($qty, 3, '.', ''), '0'), '.')</c>.</summary>
    public static string FormatQty(decimal qty)
    {
        var text = qty.ToString("0.000", CultureInfo.InvariantCulture);
        text = text.TrimEnd('0').TrimEnd('.');
        return text.Length == 0 ? "0" : text;
    }

    private static string TitleCase(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "";
        }

        return char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed record CpPosCashAccountHint(int Id, string Name, string AccountType);

public sealed record CpPosCartTotals(decimal SubtotalEx, decimal DiscountTotal, decimal AmountEx);

public sealed record CpPosParsedLine(
    string Source,
    string Ref,
    string Sku,
    string Barcode,
    string Name,
    decimal Qty,
    decimal UnitPriceEx,
    decimal DiscountPct,
    decimal DiscountAmt,
    decimal LineExVat);

public sealed record CpPosReceipt(
    bool Ok,
    string Error,
    string SaleNo,
    string SoldAt,
    string CustomerLabel,
    string Header,
    string Footer,
    string TaxLabel,
    decimal DiscountTotal,
    decimal SubtotalEx,
    decimal TaxRate,
    decimal VatAmount,
    decimal TotalAmount,
    string PaymentMethod,
    string InvoiceNumber,
    IReadOnlyList<CpPosReceiptLine> Lines)
{
    public static CpPosReceipt NotFound { get; } = new(
        false,
        "Sale not found",
        "",
        "",
        "",
        "",
        "Thank you for your purchase",
        "VAT",
        0,
        0,
        0,
        0,
        0,
        "",
        "",
        []);

    public decimal SubtotalAfterDiscount => SubtotalEx - DiscountTotal;
}

public sealed record CpPosReceiptLine(
    string QtyText,
    string Name,
    decimal LineDiscountAmt,
    decimal LineTotal);

public sealed record CpPosProductHit(
    string Source,
    string Ref,
    string Sku,
    string Barcode,
    string Name,
    string Brand,
    decimal Price,
    decimal? Stock,
    string Storage);

public sealed record CpPosCustomerHit(
    string Type,
    long UserId,
    long ContactId,
    string Label,
    string Email,
    string Phone);

public sealed record CpPosCartCalcLine(
    string Name,
    decimal Qty,
    decimal UnitPriceEx,
    decimal LineDiscountAmt,
    decimal LineExVat,
    decimal TaxRate,
    decimal VatAmount,
    decimal LineTotal);

public sealed record CpPosCartCalcResult(
    bool Ok,
    string Message,
    IReadOnlyList<CpPosCartCalcLine> Lines,
    decimal SubtotalEx,
    decimal DiscountTotal,
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount,
    decimal TaxRate,
    string TaxLabel,
    string KitCode)
{
    public static CpPosCartCalcResult Fail(string message) => new(
        false, message, [], 0, 0, 0, 0, 0, 0, "VAT", "");
}
