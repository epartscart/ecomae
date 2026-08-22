using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>VAT-inclusive advance split (PHP <c>epc_uae_vat_split_inclusive</c>).</summary>
public sealed record ErpAdvanceVatSplit(decimal AmountExVat, decimal VatAmount, decimal VatRate);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_advances.php</c>: the VAT-on-advance registers
/// (<c>epc_uae_vat_advance</c> / <c>epc_uae_vat_supplier_advance</c>) plus the advance GL
/// journals (Dr cash / Cr 2050 + 2100 on receipt, Dr 2060 + 1150 / Cr cash on payment).
/// The whole family is gated on the tenant's registered country and VAT registration
/// exactly as PHP gates it — a non-UAE tenant records no advance VAT at all.
/// </summary>
public interface IErpAdvanceVatService
{
    Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_uae_vat_split_inclusive</c>: zero VAT unless tenant output VAT is live.</summary>
    Task<ErpAdvanceVatSplit> SplitInclusiveAsync(
        DbConnection connection,
        decimal amountInclusive,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_uae_vat_record_advance_on_receipt</c>. Returns 0 when not applicable.</summary>
    Task<long> RecordCustomerAdvanceAsync(
        DbConnection connection,
        long cashEntryId,
        int userId,
        decimal paymentAmount,
        long salesOrderId,
        long paymentTime,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_uae_vat_record_supplier_advance_on_payment</c>. Returns 0 when not applicable.</summary>
    Task<long> RecordSupplierAdvanceAsync(
        DbConnection connection,
        long cashEntryId,
        int supplierId,
        decimal paymentAmount,
        long purchaseOrderId,
        long paymentTime,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_erp_gl_post_advance_receipt</c>.</summary>
    Task<long> PostAdvanceReceiptGlAsync(
        DbConnection connection,
        long cashEntryId,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_erp_gl_post_advance_payment</c>.</summary>
    Task<long> PostAdvancePaymentGlAsync(
        DbConnection connection,
        long cashEntryId,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpAdvanceVatService : IErpAdvanceVatService
{
    private readonly IErpGlPostingService _gl;

    public ErpAdvanceVatService(IErpGlPostingService gl) => _gl = gl;

    public async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `is_advance` tinyint(1) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `purchase_order_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_uae_vat_advance` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `user_id` int(11) NOT NULL DEFAULT 0,"
            + " `ledger_id` int(11) NOT NULL DEFAULT 0,"
            + " `cash_entry_id` int(11) NOT NULL DEFAULT 0,"
            + " `sales_order_id` int(11) NOT NULL DEFAULT 0,"
            + " `source_type` varchar(16) NOT NULL DEFAULT 'order',"
            + " `payment_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_rate` decimal(5,2) NOT NULL DEFAULT 5.00,"
            + " `payment_time` int(11) NOT NULL DEFAULT 0,"
            + " `adjusted` tinyint(1) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_user` (`user_id`,`adjusted`),"
            + " KEY `x_cash_entry` (`cash_entry_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE VAT on customer advances'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_uae_vat_advance` ADD `cash_entry_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_uae_vat_advance` ADD `sales_order_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_uae_vat_advance` ADD `source_type` varchar(16) NOT NULL DEFAULT 'order'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_uae_vat_supplier_advance` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `supplier_id` int(11) NOT NULL DEFAULT 0,"
            + " `cash_entry_id` int(11) NOT NULL DEFAULT 0,"
            + " `purchase_order_id` int(11) NOT NULL DEFAULT 0,"
            + " `payment_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_rate` decimal(5,2) NOT NULL DEFAULT 5.00,"
            + " `payment_time` int(11) NOT NULL DEFAULT 0,"
            + " `purchase_id` int(11) NOT NULL DEFAULT 0,"
            + " `adjusted` tinyint(1) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_cash` (`cash_entry_id`),"
            + " KEY `x_supplier` (`supplier_id`,`adjusted`),"
            + " KEY `x_time` (`payment_time`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE VAT on supplier advance payments'",
            cancellationToken).ConfigureAwait(false);

        await SeedCoaAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpAdvanceVatSplit> SplitInclusiveAsync(
        DbConnection connection,
        decimal amountInclusive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var amount = ErpTaxAmountCalculator.Round2(decimal.Max(0m, amountInclusive));
        if (amount <= 0m || !await SalesVatEnabledAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return new ErpAdvanceVatSplit(amount, 0m, 0m);
        }

        var rate = await VatRatePercentAsync(connection, cancellationToken).ConfigureAwait(false);
        var ex = ErpTaxAmountCalculator.Round2(amount / (1m + (rate / 100m)));
        return new ErpAdvanceVatSplit(ex, ErpTaxAmountCalculator.Round2(amount - ex), rate);
    }

    public async Task<long> RecordCustomerAdvanceAsync(
        DbConnection connection,
        long cashEntryId,
        int userId,
        decimal paymentAmount,
        long salesOrderId,
        long paymentTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var amount = ErpTaxAmountCalculator.Round2(paymentAmount);
        if (cashEntryId <= 0 || userId <= 0 || amount <= 0m)
        {
            return 0L;
        }

        if (!await AdvanceVatEnabledAsync(connection, cancellationToken).ConfigureAwait(false)
            || !await SalesVatEnabledAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return 0L;
        }

        var duplicate = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_uae_vat_advance` WHERE `cash_entry_id` = ? LIMIT 1"),
            cancellationToken,
            cashEntryId).ConfigureAwait(false);
        if (duplicate > 0)
        {
            return 0L;
        }

        var split = await SplitInclusiveAsync(connection, amount, cancellationToken).ConfigureAwait(false);
        if (split.VatAmount <= 0m)
        {
            return 0L;
        }

        var now = paymentTime > 0 ? paymentTime : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var orderId = salesOrderId > 0
            ? await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `shop_order_id` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                salesOrderId).ConfigureAwait(false)
            : 0L;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_uae_vat_advance` (`order_id`, `user_id`, `ledger_id`, `cash_entry_id`, `sales_order_id`,"
                + " `source_type`, `payment_amount`, `amount_ex_vat`, `vat_amount`, `vat_rate`, `payment_time`, `time_created`)"
                + " VALUES (?,?,0,?,?,'receipt',?,?,?,?,?,?)"),
            cancellationToken,
            orderId,
            userId,
            cashEntryId,
            salesOrderId,
            amount,
            split.AmountExVat,
            split.VatAmount,
            split.VatRate,
            now,
            now).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> RecordSupplierAdvanceAsync(
        DbConnection connection,
        long cashEntryId,
        int supplierId,
        decimal paymentAmount,
        long purchaseOrderId,
        long paymentTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var amount = ErpTaxAmountCalculator.Round2(paymentAmount);
        if (cashEntryId <= 0 || supplierId <= 0 || amount <= 0m)
        {
            return 0L;
        }

        if (!await SupplierAdvanceApplicableAsync(connection, supplierId, cancellationToken).ConfigureAwait(false))
        {
            return 0L;
        }

        var duplicate = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_uae_vat_supplier_advance` WHERE `cash_entry_id` = ? LIMIT 1"),
            cancellationToken,
            cashEntryId).ConfigureAwait(false);
        if (duplicate > 0)
        {
            return 0L;
        }

        var split = await SplitInclusiveAsync(connection, amount, cancellationToken).ConfigureAwait(false);
        if (split.VatAmount <= 0m)
        {
            return 0L;
        }

        var now = paymentTime > 0 ? paymentTime : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_uae_vat_supplier_advance` (`supplier_id`, `cash_entry_id`, `purchase_order_id`,"
                + " `payment_amount`, `amount_ex_vat`, `vat_amount`, `vat_rate`, `payment_time`, `time_created`)"
                + " VALUES (?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            supplierId,
            cashEntryId,
            purchaseOrderId,
            amount,
            split.AmountExVat,
            split.VatAmount,
            split.VatRate,
            now,
            now).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> PostAdvanceReceiptGlAsync(
        DbConnection connection,
        long cashEntryId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var entry = await LoadAdvanceEntryAsync(connection, cashEntryId, "receipt", cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Advance receipt entry not found");
        if (entry.GlJournalId > 0)
        {
            return entry.GlJournalId;
        }

        var cashCoaId = entry.CashCoaId > 0
            ? entry.CashCoaId
            : await CoaIdAsync(connection, entry.AccountType == "bank" ? "1010" : "1000", cancellationToken).ConfigureAwait(false);
        var advanceCoaId = await CoaIdAsync(connection, "2050", cancellationToken).ConfigureAwait(false);
        if (cashCoaId <= 0 || advanceCoaId <= 0)
        {
            throw new ErpWriteException("COA cash/2050 missing");
        }

        var split = await SplitInclusiveAsync(connection, entry.Amount, cancellationToken).ConfigureAwait(false);
        var vat = split.VatAmount;
        var ex = vat > 0m ? split.AmountExVat : entry.Amount;
        var outputVatCoaId = vat > 0m
            ? await CoaIdAsync(connection, "2100", cancellationToken).ConfigureAwait(false)
            : 0L;
        if (vat > 0m && outputVatCoaId <= 0)
        {
            // PHP folds the VAT back into the advance liability when 2100 is missing.
            ex = entry.Amount;
            vat = 0m;
        }

        var lines = new List<ErpGlLine>
        {
            new(cashCoaId, entry.Amount, 0m, "Customer advance receipt"),
            new(advanceCoaId, 0m, ex, "Customer advance liability"),
        };
        if (vat > 0m)
        {
            lines.Add(new ErpGlLine(outputVatCoaId, 0m, vat, "UAE VAT output on advance"));
        }

        return await PostAndLinkAsync(
            connection,
            cashEntryId,
            entry,
            lines,
            "Customer advance receipt #",
            adminId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> PostAdvancePaymentGlAsync(
        DbConnection connection,
        long cashEntryId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var entry = await LoadAdvanceEntryAsync(connection, cashEntryId, "payment", cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Advance payment entry not found");
        if (entry.GlJournalId > 0)
        {
            return entry.GlJournalId;
        }

        var cashCoaId = entry.CashCoaId > 0
            ? entry.CashCoaId
            : await CoaIdAsync(connection, entry.AccountType == "bank" ? "1010" : "1000", cancellationToken).ConfigureAwait(false);
        var prepaymentCoaId = await CoaIdAsync(connection, "2060", cancellationToken).ConfigureAwait(false);
        if (cashCoaId <= 0 || prepaymentCoaId <= 0)
        {
            throw new ErpWriteException("COA cash/2060 missing");
        }

        // PHP prefers the amounts already registered for this entry, falling back to a fresh split.
        var registered = await LoadSupplierAdvanceSplitAsync(connection, cashEntryId, cancellationToken).ConfigureAwait(false);
        var split = registered ?? await SplitInclusiveAsync(connection, entry.Amount, cancellationToken).ConfigureAwait(false);
        var vat = split.VatAmount;
        var ex = vat > 0m ? split.AmountExVat : entry.Amount;
        var inputVatCoaId = vat > 0m
            ? await CoaIdAsync(connection, "1150", cancellationToken).ConfigureAwait(false)
            : 0L;
        if (vat > 0m && inputVatCoaId <= 0)
        {
            ex = entry.Amount;
            vat = 0m;
        }

        var lines = new List<ErpGlLine> { new(prepaymentCoaId, ex, 0m, "Supplier advance prepayment") };
        if (vat > 0m)
        {
            lines.Add(new ErpGlLine(inputVatCoaId, vat, 0m, "UAE VAT input on advance"));
        }

        lines.Add(new ErpGlLine(cashCoaId, 0m, entry.Amount, "Supplier advance payment"));
        return await PostAndLinkAsync(
            connection,
            cashEntryId,
            entry,
            lines,
            "Supplier advance payment #",
            adminId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> PostAndLinkAsync(
        DbConnection connection,
        long cashEntryId,
        AdvanceCashEntry entry,
        IReadOnlyList<ErpGlLine> lines,
        string descriptionPrefix,
        int adminId,
        CancellationToken cancellationToken)
    {
        var journalId = await _gl.PostJournalAsync(
            connection,
            new ErpGlJournalHeader
            {
                JournalDate = entry.Time,
                Reference = entry.Reference,
                Description = descriptionPrefix + cashEntryId.ToString(CultureInfo.InvariantCulture),
                SourceType = "cash",
                SourceId = cashEntryId,
                LegislationRef = "vat-decree-8-2017-art27",
            },
            lines,
            adminId,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_cash_bank_entries` SET `gl_journal_id` = ? WHERE `id` = ?"),
            cancellationToken,
            journalId,
            cashEntryId).ConfigureAwait(false);
        return journalId;
    }

    private sealed record AdvanceCashEntry(
        long Time,
        decimal Amount,
        string Reference,
        long GlJournalId,
        long CashCoaId,
        string AccountType);

    private static async Task<AdvanceCashEntry?> LoadAdvanceEntryAsync(
        DbConnection connection,
        long cashEntryId,
        string entryType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT e.`time`, e.`amount`, e.`voucher_no`, e.`reference`, e.`gl_journal_id`, a.`coa_id`, a.`account_type`"
            + " FROM `epc_erp_cash_bank_entries` e"
            + " INNER JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = e.`account_id`"
            + " WHERE e.`id` = ? AND e.`active` = 1 AND e.`is_advance` = 1 AND e.`entry_type` = ? LIMIT 1");
        ErpDb.AddParameters(command, cashEntryId, entryType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var voucherNo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        var reference = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
        return new AdvanceCashEntry(
            reader.GetInt64(0),
            ErpTaxAmountCalculator.Round2(reader.GetDecimal(1)),
            voucherNo.Length > 0 ? voucherNo : reference,
            reader.IsDBNull(4) ? 0L : reader.GetInt64(4),
            reader.IsDBNull(5) ? 0L : reader.GetInt64(5),
            reader.IsDBNull(6) ? "cash" : reader.GetString(6));
    }

    private static async Task<ErpAdvanceVatSplit?> LoadSupplierAdvanceSplitAsync(
        DbConnection connection,
        long cashEntryId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `amount_ex_vat`, `vat_amount`, `vat_rate` FROM `epc_uae_vat_supplier_advance`"
                + " WHERE `cash_entry_id` = ? LIMIT 1");
            ErpDb.AddParameters(command, cashEntryId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new ErpAdvanceVatSplit(
                ErpTaxAmountCalculator.Round2(reader.GetDecimal(0)),
                ErpTaxAmountCalculator.Round2(reader.GetDecimal(1)),
                reader.IsDBNull(2) ? 0m : reader.GetDecimal(2));
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>PHP <c>epc_uae_vat_supplier_input_applicable</c> on an active supplier row.</summary>
    private static async Task<bool> SupplierAdvanceApplicableAsync(
        DbConnection connection,
        int supplierId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `vat_registered`, `country_code` FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1");
            ErpDb.AddParameters(command, supplierId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (!reader.IsDBNull(0) && reader.GetInt32(0) != 1)
            {
                return false;
            }

            var country = (reader.IsDBNull(1) ? "AE" : reader.GetString(1)).Trim().ToUpperInvariant();
            return country is "" or "AE" or "ARE" or "UAE";
        }
        catch (DbException)
        {
            return false;
        }
    }

    /// <summary>PHP <c>epc_uae_vat_on_advance_enabled</c>.</summary>
    private static async Task<bool> AdvanceVatEnabledAsync(DbConnection connection, CancellationToken cancellationToken)
        => Truthy(await SettingAsync(connection, "vat_on_advance_enabled", "1", cancellationToken).ConfigureAwait(false));

    /// <summary>PHP <c>epc_uae_vat_sales_enabled</c>: tenant registered in the UAE and VAT-registered.</summary>
    private static async Task<bool> SalesVatEnabledAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!Truthy(await SettingAsync(connection, "vat_uae_sales_only", "1", cancellationToken).ConfigureAwait(false)))
        {
            return false;
        }

        var country = (await SettingAsync(connection, "company_country_code", "AE", cancellationToken).ConfigureAwait(false))
            .Trim()
            .ToUpperInvariant();
        var vatRegistered = Truthy(await SettingAsync(connection, "company_vat_registered", "1", cancellationToken).ConfigureAwait(false));
        return vatRegistered && country is "AE" or "ARE" or "UAE";
    }

    private static async Task<decimal> VatRatePercentAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var raw = await SettingAsync(connection, "vat_percent", "5.00", cancellationToken).ConfigureAwait(false);
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? decimal.Round(decimal.Clamp(parsed, 0m, 100m), 2, MidpointRounding.AwayFromZero)
            : 5m;
    }

    /// <summary>PHP treats <c>'1'</c>, <c>'true'</c> and an empty stored value as enabled.</summary>
    private static bool Truthy(string value)
    {
        var trimmed = value.Trim();
        return trimmed is "" or "1" or "true";
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

    private static Task<long> CoaIdAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            code);

    /// <summary>PHP <c>epc_erp_advances_seed_coa</c>.</summary>
    private static async Task SeedCoaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        (string Code, string Name, string Type, string Side, string Description)[] rows =
        [
            ("2050", "Customer advances received", "liability", "credit", "UAE customer prepayments until invoiced"),
            ("2060", "Supplier advance payments", "asset", "debit", "UAE supplier prepayments until purchase invoice"),
        ];

        foreach (var row in rows)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `description`,"
                        + " `system_flag`, `time_created`) SELECT ?, ?, ?, ?, ?, 1, ? FROM DUAL"
                        + " WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1)"),
                    cancellationToken,
                    row.Code,
                    row.Name,
                    row.Type,
                    row.Side,
                    row.Description,
                    now,
                    row.Code).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // Chart of accounts not installed yet — PHP's seed is equally best-effort.
            }
        }
    }
}
