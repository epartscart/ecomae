using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public static partial class ErpDocumentControlRender
{
    /// <summary>PHP <c>epc_einvoice_default_settings()</c> for the seller keys the e-invoice context reads.</summary>
    private static readonly (string Key, string Default)[] SellerSettings =
    [
        ("seller_name", "ePartsCart LLC"),
        ("seller_trn", string.Empty),
        ("seller_address_line1", string.Empty),
        ("seller_city", "Dubai"),
        ("seller_phone", string.Empty),
        ("seller_email", string.Empty),
    ];

    /// <summary>
    /// PHP <c>epc_dc_einvoice_context()</c> placeholders: an active <c>epc_einvoice_documents</c> row and its lines, the company
    /// row falling back to the e-invoice seller settings. Throws <see cref="ErpWriteException"/> “Invoice not found”.
    /// </summary>
    private static async Task<List<(string Key, string Value)>> InvoicePlaceholdersAsync(DbConnection connection, long invoiceId, CancellationToken cancellationToken)
    {
        var doc = (await NullableRowsAsync(
            connection,
            ErpDb.Positional("SELECT * FROM `epc_einvoice_documents` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            invoiceId).ConfigureAwait(false)).FirstOrDefault()
            ?? throw new ErpWriteException("Invoice not found");

        List<Dictionary<string, string?>> lines;
        try
        {
            lines = await NullableRowsAsync(
                connection,
                ErpDb.Positional("SELECT * FROM `epc_einvoice_lines` WHERE `document_id` = ? ORDER BY `line_no`, `id`"),
                cancellationToken,
                invoiceId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            lines = [];
        }

        var company = (await NullableRowsAsync(connection, "SELECT * FROM `epc_document_company` WHERE `id` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false)).FirstOrDefault()
            ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        var seller = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, fallback) in SellerSettings)
        {
            seller[key] = await EinvoiceSettingAsync(connection, key, fallback, cancellationToken).ConfigureAwait(false);
        }

        var userId = (int)PhpInt(Coalesce(doc, "user_id"));
        var buyer = userId > 0
            ? await InvoiceBuyerAsync(connection, userId, cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string?>(StringComparer.Ordinal);

        var legal = (Coalesce(company, "legal_name") ?? string.Empty).Trim();
        if (legal.Length == 0)
        {
            legal = (Coalesce(seller, "seller_name") ?? Coalesce(company, "trade_name") ?? "Company").Trim();
        }

        var companyAddress = JoinFilled(
            ", ",
            Coalesce(company, "address_line1") ?? Coalesce(seller, "seller_address_line1") ?? string.Empty,
            Coalesce(company, "address_line2") ?? string.Empty,
            Coalesce(company, "city") ?? Coalesce(seller, "seller_city") ?? string.Empty,
            Coalesce(company, "country") ?? string.Empty).Trim();
        var trn = (Coalesce(company, "trn") ?? string.Empty).Trim();
        if (trn.Length == 0)
        {
            trn = (Coalesce(seller, "seller_trn") ?? string.Empty).Trim();
        }

        var logo = (Coalesce(company, "logo_path") ?? string.Empty).Trim();
        if (logo.Length == 0)
        {
            logo = "/content/files/epc_doc/logo.png";
        }

        var buyerName = (Coalesce(buyer, "buyer_name") ?? Coalesce(doc, "buyer_name") ?? "Customer").Trim();
        var buyerAddress = JoinFilled(
            ", ",
            Coalesce(buyer, "address_line1") ?? Coalesce(doc, "buyer_address_line1") ?? string.Empty,
            Coalesce(buyer, "city") ?? Coalesce(doc, "buyer_city") ?? string.Empty,
            Coalesce(buyer, "emirate") ?? Coalesce(doc, "buyer_emirate") ?? string.Empty,
            Coalesce(buyer, "country_code") ?? Coalesce(doc, "buyer_country_code") ?? "AE").Trim();
        var buyerTrn = (Coalesce(buyer, "trn") ?? Coalesce(doc, "buyer_trn") ?? string.Empty).Trim();

        var rows = new List<DcLine>();
        var rate = 5m;
        foreach (var line in lines)
        {
            var qty = PhpDecimal(Coalesce(line, "quantity") ?? "1");
            var net = Round2(PhpDecimal(Coalesce(line, "line_net")));
            var vat = Round2(PhpDecimal(Coalesce(line, "vat_line_aed") ?? Coalesce(line, "vat_amount")));
            var gross = Coalesce(line, "gross_amount") is { } g ? Round2(PhpDecimal(g)) : Round2(net + vat);
            var lineRate = PhpDecimal(Coalesce(line, "tax_rate") ?? "5");
            if (lineRate > 0m)
            {
                rate = lineRate;
            }

            rows.Add(new DcLine(
                Coalesce(line, "item_code") ?? string.Empty,
                string.Empty,
                Coalesce(line, "item_name") ?? Coalesce(line, "description") ?? "Item",
                qty,
                PhpDecimal(Coalesce(line, "unit_price")),
                net,
                vat,
                gross,
                lineRate));
        }

        var subtotal = Round2(PhpDecimal(Coalesce(doc, "subtotal_ex_vat")));
        var vatTotal = Round2(PhpDecimal(Coalesce(doc, "total_vat")));
        var totalIncl = Coalesce(doc, "total_incl_vat") is { } t ? Round2(PhpDecimal(t)) : Round2(subtotal + vatTotal);
        var paid = Round2(PhpDecimal(Coalesce(doc, "paid_amount")));
        var invoiceNumber = (Coalesce(doc, "invoice_number") ?? "INV-" + invoiceId.ToString(CultureInfo.InvariantCulture)).Trim();
        var issue = Coalesce(doc, "issue_date") is { } issueRaw ? PhpInt(issueRaw) : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var orderRef = PhpInt(Coalesce(doc, "order_id"));
        if (orderRef <= 0)
        {
            orderRef = PhpInt(Coalesce(doc, "sales_order_id"));
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return
        [
            ("company_logo", logo),
            ("company_legal_name", legal),
            ("company_trade_name", Coalesce(company, "trade_name") ?? string.Empty),
            ("company_address", companyAddress),
            ("company_trn", trn),
            ("company_phone", Coalesce(company, "phone") ?? Coalesce(seller, "seller_phone") ?? string.Empty),
            ("company_email", Coalesce(company, "email") ?? Coalesce(seller, "seller_email") ?? string.Empty),
            ("company_website", Coalesce(company, "website") ?? string.Empty),
            ("document_number", invoiceNumber),
            ("document_date", PhpDate("dd MMM yyyy", issue)),
            ("order_id", (orderRef > 0 ? orderRef : invoiceId).ToString(CultureInfo.InvariantCulture)),
            ("supply_date", PhpDate("dd MMM yyyy", Coalesce(doc, "vat_point_date") is { } point ? PhpInt(point) : issue)),
            ("buyer_name", buyerName),
            ("buyer_address", buyerAddress),
            ("buyer_trn", buyerTrn),
            ("ship_to_name", buyerName),
            ("ship_to_address", buyerAddress),
            ("ship_to_phone", Coalesce(buyer, "phone") ?? string.Empty),
            ("subtotal_excl_vat", Money(subtotal)),
            ("vat_amount", Money(vatTotal)),
            ("total_incl_vat", Money(totalIncl)),
            ("vat_rate", PhpNumber(rate)),
            ("amount_words", AmountWordsEn(totalIncl)),
            ("payment_terms", Coalesce(doc, "payment_terms") ?? "Due on receipt unless agreed otherwise"),
            ("bank_name", Coalesce(company, "bank_name") ?? string.Empty),
            ("bank_iban", Coalesce(company, "bank_iban") ?? string.Empty),
            ("legal_footer", Coalesce(company, "legal_footer") ?? string.Empty),
            ("carrier", "—"),
            ("tracking_no", "—"),
            ("package_count", "1"),
            ("total_weight", "—"),
            ("prepared_by", "Accounts"),
            ("driver_info", "—"),
            ("delivery_notes", string.Empty),
            ("amount_received", Money(paid > 0m ? paid : totalIncl)),
            ("payment_method", paid >= totalIncl && totalIncl > 0m ? "Paid in full" : "Pending"),
            ("payment_reference", invoiceNumber),
            ("payment_date", PhpDate("dd MMM yyyy", now)),
            ("lines_table", LinesTableSales(rows, rate)),
            ("lines_table_packing", LinesTablePacking(rows)),
            ("lines_table_delivery", LinesTablePacking(rows)),
        ];
    }

    /// <summary>PHP <c>epc_einvoice_buyer_profile()</c> keeping the stored row's NULLs, which the context coalesces past.</summary>
    private static async Task<Dictionary<string, string?>> InvoiceBuyerAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        try
        {
            var stored = (await NullableRowsAsync(
                connection,
                ErpDb.Positional("SELECT * FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false)).FirstOrDefault();
            if (stored is not null)
            {
                return stored;
            }
        }
        catch (DbException)
        {
        }

        return (await RawBuyerProfileAsync(connection, userId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal);
    }

    /// <summary>PHP <c>epc_einvoice_get_setting()</c>: the stored value (NULL reads as empty), else the default when the key has no row.</summary>
    private static async Task<string> EinvoiceSettingAsync(DbConnection connection, string key, string fallback, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await NullableRowsAsync(
                connection,
                ErpDb.Positional("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
            return rows.Count == 0 ? fallback : rows[0]["setting_value"] ?? string.Empty;
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    /// <summary>PHP <c>$row[$key] ?? …</c>: null for a missing key or a NULL column.</summary>
    private static string? Coalesce(IReadOnlyDictionary<string, string?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static async Task<List<Dictionary<string, string?>>> NullableRowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        var rows = new List<Dictionary<string, string?>>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : PdoString(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }
}
