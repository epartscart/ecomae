using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Accounts receivable inquiries from the customer process: balance list, account statement,
/// open transactions, and the amounts a receipt settles. The same rows feed the receivables page
/// and the receivables workspace.
/// </summary>
public static class ErpCustomerAr
{
    public const string BalanceTab = "ar_customer_balance";
    public const string StatementTab = "ar_customer_statement";
    public const string TransactionsTab = "ar_customer_transactions";
    public const string OpenTab = "ar_open_invoices";
    public const string PaymentTab = "ar_payment_journal";
    public const string SettledTab = "ar_settled_invoices";
    public const string ReconTab = "ar_customer_ledger_recon";

    public const string ViewBalance = "balance";
    public const string ViewStatement = "statement";
    public const string ViewTransactions = "transactions";
    public const string ViewOpen = "open";
    public const string ViewSettled = "settled";
    public const string ViewRecon = "recon";

    public sealed record Invoice(
        long Id,
        long CustomerId,
        string Number,
        string Date,
        long When,
        decimal Total,
        decimal Paid,
        decimal Outstanding,
        bool CreditNote);

    public sealed record Receipt(
        long Id,
        long CustomerId,
        string Number,
        string Date,
        long When,
        decimal Amount,
        decimal Applied,
        decimal Unapplied);

    public sealed record Line(
        string Date,
        long CustomerId,
        string Customer,
        string Kind,
        string Number,
        decimal Debit,
        decimal Credit,
        decimal Balance);

    public sealed record BalanceRow(
        long CustomerId,
        string Customer,
        int OpenCount,
        decimal Open,
        decimal Unapplied,
        decimal Net);

    public sealed record Report(
        decimal Open,
        decimal Unapplied,
        decimal Net,
        decimal StatementBalance,
        IReadOnlyList<BalanceRow> Balances,
        IReadOnlyList<Line> Statement,
        IReadOnlyList<Invoice> OpenInvoices,
        IReadOnlyList<Invoice> SettledInvoices,
        string Note);

    public static bool IsInquiryTab(string? tab)
        => ViewFor(tab) is not null;

    public static string? ViewFor(string? tab)
        => (tab ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            BalanceTab or ViewBalance => ViewBalance,
            StatementTab or ViewStatement => ViewStatement,
            TransactionsTab or ViewTransactions => ViewTransactions,
            OpenTab or PaymentTab or ViewOpen => ViewOpen,
            SettledTab or ViewSettled => ViewSettled,
            ReconTab or ViewRecon => ViewRecon,
            _ => null,
        };

    public static decimal OutstandingOf(decimal total, decimal paid)
        => Round2(Math.Max(0m, total) - Math.Max(0m, paid));

    public static Invoice MakeInvoice(long id, long customerId, string number, long when, decimal total, decimal paid, bool creditNote)
    {
        var totalRounded = Round2(Math.Abs(total));
        var paidRounded = Round2(Math.Min(Math.Abs(paid), totalRounded));
        return new(
            id,
            customerId,
            number.Trim(),
            Day(when),
            when,
            totalRounded,
            paidRounded,
            OutstandingOf(totalRounded, paidRounded),
            creditNote);
    }

    public static Receipt MakeReceipt(long id, long customerId, string number, long when, decimal amount, decimal applied)
    {
        var amountRounded = Round2(Math.Max(0m, amount));
        var appliedRounded = Round2(Math.Min(Math.Max(0m, applied), amountRounded));
        return new(
            id,
            customerId,
            number.Trim(),
            Day(when),
            when,
            amountRounded,
            appliedRounded,
            Round2(amountRounded - appliedRounded));
    }

    public static Report Build(
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Receipt> receipts,
        IReadOnlyDictionary<long, string> names,
        string? customer,
        DateOnly? from,
        DateOnly? to)
    {
        var invoiceRows = invoices.Where(row => Match(row.CustomerId, row.Number, names, customer) && InRange(row.When, from, to)).ToList();
        var receiptRows = receipts.Where(row => Match(row.CustomerId, row.Number, names, customer) && InRange(row.When, from, to)).ToList();
        var ids = invoiceRows.Select(row => row.CustomerId).Concat(receiptRows.Select(row => row.CustomerId)).Distinct().ToList();
        var balances = ids
            .Select(id =>
            {
                var mine = invoiceRows.Where(row => row.CustomerId == id).ToList();
                var open = Round2(mine.Where(row => !row.CreditNote).Sum(row => row.Outstanding) - mine.Where(row => row.CreditNote).Sum(row => row.Outstanding));
                var unapplied = Round2(receiptRows.Where(row => row.CustomerId == id).Sum(row => row.Unapplied));
                return new BalanceRow(
                    id,
                    Name(id, names),
                    mine.Count(row => !row.CreditNote && row.Outstanding > 0m),
                    open,
                    unapplied,
                    Round2(open - unapplied));
            })
            .OrderByDescending(row => row.Net)
            .ThenBy(row => row.Customer, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statement = new List<Line>();
        foreach (var id in ids.OrderBy(id => Name(id, names), StringComparer.OrdinalIgnoreCase))
        {
            var events = new List<(long When, string Kind, string Number, decimal Debit, decimal Credit)>();
            foreach (var invoice in invoiceRows.Where(row => row.CustomerId == id))
            {
                if (invoice.CreditNote)
                {
                    events.Add((invoice.When, "Credit", invoice.Number, 0m, invoice.Total));
                }
                else
                {
                    events.Add((invoice.When, "Invoice", invoice.Number, invoice.Total, 0m));
                }
            }

            foreach (var receipt in receiptRows.Where(row => row.CustomerId == id))
            {
                events.Add((receipt.When, "Payment", receipt.Number, 0m, receipt.Amount));
            }

            var running = 0m;
            foreach (var item in events.OrderBy(item => item.When).ThenBy(item => item.Kind, StringComparer.OrdinalIgnoreCase))
            {
                running = Round2(running + item.Debit - item.Credit);
                statement.Add(new Line(Day(item.When), id, Name(id, names), item.Kind, item.Number, item.Debit, item.Credit, running));
            }
        }

        var openTotal = Round2(balances.Sum(row => row.Open));
        var unappliedTotal = Round2(balances.Sum(row => row.Unapplied));
        var net = Round2(openTotal - unappliedTotal);
        var statementBalance = statement.Count == 0 ? 0m : Round2(statement.Sum(line => line.Debit - line.Credit));
        return new(
            openTotal,
            unappliedTotal,
            net,
            statementBalance,
            balances,
            statement,
            invoiceRows.Where(row => !row.CreditNote && row.Outstanding > 0m).OrderBy(row => row.When).ThenBy(row => row.Number, StringComparer.OrdinalIgnoreCase).ToList(),
            invoiceRows.Where(row => !row.CreditNote && row.Outstanding <= 0m && row.Paid > 0m).OrderByDescending(row => row.When).ToList(),
            "Open balance is unsettled invoice amounts minus remaining credits and receipts not yet applied to an invoice. Mark open invoices on the payment journal to settle them.");
    }

    public static async Task<Report> LoadAsync(
        DbConnection connection,
        string? customer,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var invoices = await InvoicesAsync(connection, cancellationToken).ConfigureAwait(false);
        var receipts = await ReceiptsAsync(connection, cancellationToken).ConfigureAwait(false);
        var ids = invoices.Select(row => row.CustomerId).Concat(receipts.Select(row => row.CustomerId)).Where(id => id > 0).Distinct().ToList();
        var names = await NamesAsync(connection, ids, cancellationToken).ConfigureAwait(false);
        return Build(invoices, receipts, names, customer, from, to);
    }

    private static async Task<List<Invoice>> InvoicesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<Invoice>();
        if (!await TableExistsAsync(connection, "epc_einvoice_documents", cancellationToken).ConfigureAwait(false))
        {
            return rows;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `id`, `user_id`, `invoice_number`, `issue_date`, `total_incl_vat`, `paid_amount`, `doc_category`
            FROM `epc_einvoice_documents`
            WHERE `active` = 1 AND `status` <> 'cancelled'
              AND `doc_category` IN ('tax_invoice','commercial_invoice','tax_credit_note','credit_note')
            ORDER BY `issue_date` DESC, `id` DESC
            LIMIT 5000
            """;
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var category = reader.IsDBNull(6) ? "" : reader.GetString(6);
                var credit = category.Contains("credit", StringComparison.OrdinalIgnoreCase);
                rows.Add(MakeInvoice(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? 0L : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture),
                    reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                    credit));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<List<Receipt>> ReceiptsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var applied = await AppliedAsync(connection, cancellationToken).ConfigureAwait(false);
        var rows = new List<Receipt>();
        if (!await TableExistsAsync(connection, "epc_erp_cash_bank_entries", cancellationToken).ConfigureAwait(false))
        {
            return rows;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `id`, `counterparty_id`, `time`, `amount`, `voucher_no`, `reference`
            FROM `epc_erp_cash_bank_entries`
            WHERE `counterparty_type` = 'customer' AND `entry_type` = 'receipt' AND `direction` = 1
            ORDER BY `time` DESC, `id` DESC
            LIMIT 5000
            """;
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var voucher = reader.IsDBNull(4) ? "" : reader.GetString(4);
                var reference = reader.IsDBNull(5) ? "" : reader.GetString(5);
                applied.TryGetValue(id, out var used);
                rows.Add(MakeReceipt(
                    id,
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    voucher.Length > 0 ? voucher : reference,
                    reader.IsDBNull(2) ? 0L : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    used));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<Dictionary<long, decimal>> AppliedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, decimal>();
        if (!await TableExistsAsync(connection, "epc_erp_settlement_allocations", cancellationToken).ConfigureAwait(false))
        {
            return map;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `cash_entry_id`, COALESCE(SUM(`amount`), 0)
            FROM `epc_erp_settlement_allocations`
            WHERE `doc_type` = 'ar' AND `active` = 1
            GROUP BY `cash_entry_id`
            """;
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] = Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
        }
        catch (DbException)
        {
            return [];
        }

        return map;
    }

    private static async Task<Dictionary<long, string>> NamesAsync(DbConnection connection, IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, string>();
        if (ids.Count == 0 || !await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false))
        {
            return map;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `user_id`, `email` FROM `users` WHERE `user_id` IN (" + string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")";
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
            }
        }
        catch (DbException)
        {
            return [];
        }

        return map;
    }

    private static bool Match(long customerId, string number, IReadOnlyDictionary<long, string> names, string? customer)
    {
        var text = (customer ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return true;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return id == customerId || string.Equals(number, text, StringComparison.OrdinalIgnoreCase);
        }

        names.TryGetValue(customerId, out var name);
        return (name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)
            || number.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InRange(long when, DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null)
        {
            return true;
        }

        var day = when > 0
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(when).UtcDateTime)
            : DateOnly.MinValue;
        return (from is null || day >= from) && (to is null || day <= to);
    }

    private static string Name(long customerId, IReadOnlyDictionary<long, string> names)
    {
        names.TryGetValue(customerId, out var name);
        return string.IsNullOrWhiteSpace(name) ? "Customer " + customerId.ToString(CultureInfo.InvariantCulture) : name.Trim();
    }

    private static string Day(long when)
        => when > 0
            ? DateTimeOffset.FromUnixTimeSeconds(when).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "";

    private static Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
        => ErpDb.TableKnownAsync(connection, table, cancellationToken);

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
