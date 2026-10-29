using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpJwLeaseRow(int Month, decimal Opening, decimal Interest, decimal Payment, decimal Principal, decimal Closing, decimal Depreciation);

/// <summary>Pure IFRS period-end adjustment recipes (IFRS 9, IAS 2, IAS 37, IFRS 16). Covered by unit tests.</summary>
public static class ErpJwIfrsAdjustments
{
    public const string LossAllowanceAccount = "1190";
    public const string RouAssetAccount = "1700";
    public const string RouDepreciationAccount = "1710";
    public const string ProvisionAccount = "2400";
    public const string LeaseLiabilityAccount = "2600";
    public const string WriteDownAccount = "5050";
    public const string TrueUpAccount = "5900";
    public const string EclExpenseAccount = "6950";
    public const string ProvisionExpenseAccount = "6960";
    public const string RouDepreciationExpenseAccount = "6970";
    public const string LeaseInterestAccount = "6980";

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>IFRS 9.5.5.17: book the movement from the current allowance (credit balance) to the target.</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> Ecl(decimal targetAllowance, decimal currentAllowance)
    {
        var delta = R(targetAllowance - currentAllowance);
        if (delta == 0) return [];
        return delta > 0
            ? [new(EclExpenseAccount, delta, 0m, "Increase in expected credit loss allowance"), new(LossAllowanceAccount, 0m, delta, "Loss allowance — trade receivables")]
            : [new(LossAllowanceAccount, -delta, 0m, "Release of expected credit loss allowance"), new(EclExpenseAccount, 0m, -delta, "Reversal of impairment loss")];
    }

    /// <summary>IAS 2.34: write inventory down to NRV.</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> NrvWriteDown(decimal amount)
        => R(amount) <= 0 ? [] : [new(WriteDownAccount, R(amount), 0m, "Inventory written down to net realisable value"), new(ErpAutoPartsIfrs.InventoryAccount, 0m, R(amount), "Inventory — NRV write-down")];

    /// <summary>Approved true-up of GL inventory to the stock sub-ledger (difference = sub-ledger − GL).</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> InventoryTrueUp(decimal difference)
    {
        var d = R(difference);
        if (d == 0) return [];
        return d > 0
            ? [new(ErpAutoPartsIfrs.InventoryAccount, d, 0m, "Inventory true-up to stock sub-ledger"), new(TrueUpAccount, 0m, d, "Inventory reconciliation difference")]
            : [new(TrueUpAccount, -d, 0m, "Inventory reconciliation difference"), new(ErpAutoPartsIfrs.InventoryAccount, 0m, -d, "Inventory true-up to stock sub-ledger")];
    }

    /// <summary>IAS 37.14: recognise (positive) or release (negative) a provision.</summary>
    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> Provision(decimal amount, string description)
    {
        var a = R(amount);
        if (a == 0) return [];
        return a > 0
            ? [new(ProvisionExpenseAccount, a, 0m, description), new(ProvisionAccount, 0m, a, description)]
            : [new(ProvisionAccount, -a, 0m, description), new(ProvisionExpenseAccount, 0m, -a, description)];
    }

    /// <summary>IFRS 16.26: present value of equal monthly payments in arrears at the incremental borrowing rate.</summary>
    public static decimal LeasePresentValue(decimal monthlyPayment, int months, decimal annualRatePct)
    {
        if (monthlyPayment <= 0 || months <= 0) return 0;
        var i = (double)annualRatePct / 100d / 12d;
        if (i <= 0) return R(monthlyPayment * months);
        var pv = (double)monthlyPayment * (1 - Math.Pow(1 + i, -months)) / i;
        return R((decimal)pv);
    }

    /// <summary>Effective-interest schedule; the last month absorbs rounding so the liability closes at zero.</summary>
    public static IReadOnlyList<ErpJwLeaseRow> LeaseSchedule(decimal monthlyPayment, int months, decimal annualRatePct)
    {
        var rows = new List<ErpJwLeaseRow>();
        var pv = LeasePresentValue(monthlyPayment, months, annualRatePct);
        var rate = annualRatePct / 100m / 12m;
        var dep = months > 0 ? R(pv / months) : 0;
        var opening = pv;
        var depLeft = pv;
        for (var m = 1; m <= months; m++)
        {
            var interest = R(opening * rate);
            var principal = R(monthlyPayment - interest);
            var d = dep;
            if (m == months)
            {
                principal = opening;
                interest = R(monthlyPayment - principal);
                d = depLeft;
            }

            var closing = R(opening - principal);
            rows.Add(new(m, opening, interest, R(monthlyPayment), principal, closing, d));
            opening = closing;
            depLeft = R(depLeft - d);
        }

        return rows;
    }

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> LeaseInitial(decimal pv)
        => R(pv) <= 0 ? [] : [new(RouAssetAccount, R(pv), 0m, "Right-of-use asset recognised"), new(LeaseLiabilityAccount, 0m, R(pv), "Lease liability recognised")];

    public static IReadOnlyList<ErpAutoPartsIfrs.JournalLine> LeaseMonth(ErpJwLeaseRow row, string cashAccount)
    {
        var lines = new List<ErpAutoPartsIfrs.JournalLine>();
        if (row.Interest > 0) lines.Add(new(LeaseInterestAccount, row.Interest, 0m, "Lease interest month " + row.Month.ToString(CultureInfo.InvariantCulture)));
        if (row.Principal > 0) lines.Add(new(LeaseLiabilityAccount, row.Principal, 0m, "Lease liability repaid"));
        lines.Add(new(cashAccount, 0m, row.Payment, "Lease payment"));
        if (row.Depreciation > 0)
        {
            lines.Add(new(RouDepreciationExpenseAccount, row.Depreciation, 0m, "Right-of-use depreciation"));
            lines.Add(new(RouDepreciationAccount, 0m, row.Depreciation, "Accumulated depreciation — right-of-use"));
        }

        return lines;
    }
}

public sealed record ErpJwIfrsAdjustRequest(string Kind, IReadOnlyDictionary<string, string> Values, bool Confirm, int ActorId);

public interface IErpJwIfrsAdjustmentWriteService
{
    Task<ErpSimpleWriteResult> PostAsync(ErpJwIfrsAdjustRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Controlled, auditable IFRS month-end adjustments. Each posts one new journal with a unique reference; nothing historical is edited.</summary>
public sealed class ErpJwIfrsAdjustmentWriteService : IErpJwIfrsAdjustmentWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;
    private readonly IErpJwIfrsReconciliationReadService _recon;

    public ErpJwIfrsAdjustmentWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl, IErpJwIfrsReconciliationReadService recon)
    {
        _connections = connections;
        _gl = gl;
        _recon = recon;
    }

    public async Task<ErpSimpleWriteResult> PostAsync(ErpJwIfrsAdjustRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured) return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        string V(string k) => request.Values.TryGetValue(k, out var v) ? (v ?? "").Trim() : "";
        decimal N(string k) => decimal.TryParse(V(k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
        var asOf = DateOnly.TryParseExact(V("as_of"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) ? a : DateOnly.FromDateTime(DateTime.UtcNow);
        var stamp = asOf.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var date = new DateTimeOffset(asOf.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero).ToUnixTimeSeconds();

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(c, cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsGl.EnsureCoaAsync(c, cancellationToken).ConfigureAwait(false);
        await ErpJewelleryGl.EnsureCoaAsync(c, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(c, 0, cancellationToken).ConfigureAwait(false);

        string reference;
        string description;
        IReadOnlyList<ErpAutoPartsIfrs.JournalLine> recipe;
        Func<DbTransaction, Task>? after = null;
        switch (request.Kind)
        {
            case "ecl":
            {
                var report = await _recon.ReadAsync(null, asOf, 0.02m, cancellationToken).ConfigureAwait(false);
                var target = V("amount").Length > 0 ? N("amount") : report.Ecl.Sum(b => b.Allowance);
                var current = -report.Accounts.Where(x => x.Code == ErpJwIfrsAdjustments.LossAllowanceAccount).Sum(x => x.Closing);
                recipe = ErpJwIfrsAdjustments.Ecl(target, current);
                reference = "JW-IFRS9-ECL-" + stamp;
                description = "IFRS 9 expected credit loss — allowance to " + target.ToString("N2", CultureInfo.InvariantCulture);
                break;
            }

            case "nrv":
            {
                var report = await _recon.ReadAsync(null, asOf, N("cost_to_sell_pct") > 0 ? N("cost_to_sell_pct") / 100m : 0.02m, cancellationToken).ConfigureAwait(false);
                var lines = report.Nrv.Where(n => n.Shortfall > 0).ToList();
                recipe = ErpJwIfrsAdjustments.NrvWriteDown(lines.Sum(n => n.Shortfall));
                reference = "JW-IAS2-NRV-" + stamp;
                description = "IAS 2 write-down to NRV: " + string.Join(", ", lines.Select(n => n.Code + " " + n.Shortfall.ToString("N2", CultureInfo.InvariantCulture)));
                after = async tx =>
                {
                    foreach (var g in lines.GroupBy(n => n.Code, StringComparer.OrdinalIgnoreCase))
                    {
                        var stockId = await ErpDb.LongAsync(c, tx, ErpDb.Positional(
                            "SELECT `id` FROM `epc_jewel_metal_stock` WHERE `company_id` IN (0,?) AND `item_code`=? ORDER BY `company_id` DESC LIMIT 1"),
                            cancellationToken, companyId, g.Key).ConfigureAwait(false);
                        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                            "UPDATE `epc_jewel_metal_stock` SET `stock_value`=GREATEST(0,`stock_value`-?),`pur_cost_gms`=IF(`stock_gms`>0,ROUND(GREATEST(0,`stock_value`-?)/`stock_gms`,4),`pur_cost_gms`) WHERE `id`=?"),
                            cancellationToken, g.Sum(n => n.Shortfall), g.Sum(n => n.Shortfall), stockId).ConfigureAwait(false);
                    }
                };
                break;
            }

            case "true_up":
            {
                var report = await _recon.ReadAsync(null, asOf, 0.02m, cancellationToken).ConfigureAwait(false);
                var inv = report.Checks.FirstOrDefault(x => x.Area == "Inventory");
                if (inv is null) return ErpSimpleWriteResult.Fail("invalid", "The inventory check is only available for a current as-of date.");
                recipe = ErpJwIfrsAdjustments.InventoryTrueUp(inv.Expected - inv.Actual);
                reference = "JW-INV-TRUEUP-" + stamp;
                description = "Approved inventory true-up to stock sub-ledger (" + V("approved_by") + ")";
                if (V("approved_by").Length == 0) return ErpSimpleWriteResult.Fail("invalid", "Enter who approved the true-up.");
                break;
            }

            case "provision":
            {
                var key = Slug(V("ref"));
                if (key.Length == 0) return ErpSimpleWriteResult.Fail("invalid", "Enter a provision reference.");
                description = "IAS 37 provision " + key + (V("description").Length > 0 ? " — " + V("description") : "");
                recipe = ErpJwIfrsAdjustments.Provision(N("amount"), description.Length > 250 ? description[..250] : description);
                reference = "JW-IAS37-" + key + "-" + stamp;
                break;
            }

            case "lease":
            {
                var key = Slug(V("ref"));
                var months = (int)N("months");
                var payment = N("payment");
                var rate = N("rate");
                if (key.Length == 0 || months <= 0 || payment <= 0) return ErpSimpleWriteResult.Fail("invalid", "Enter lease reference, monthly payment and term in months.");
                if (months > 600) return ErpSimpleWriteResult.Fail("invalid", "Lease term is limited to 600 months.");
                var terms = months.ToString(CultureInfo.InvariantCulture) + " × " + payment.ToString("N2", CultureInfo.InvariantCulture) + " @ " + rate.ToString("0.##", CultureInfo.InvariantCulture) + "%";
                var month = (int)N("month");
                var pv = ErpJwIfrsAdjustments.LeasePresentValue(payment, months, rate);
                if (month <= 0)
                {
                    recipe = ErpJwIfrsAdjustments.LeaseInitial(pv);
                    reference = "JW-IFRS16-" + key + "-INIT";
                    description = "IFRS 16 lease " + key + ": " + terms + " → PV " + pv.ToString("N2", CultureInfo.InvariantCulture);
                }
                else
                {
                    if (month > months) return ErpSimpleWriteResult.Fail("invalid", "Month is after the lease term.");
                    var initId = await ErpAutoPartsGl.PostedJournalIdAsync(c, "JW-IFRS16-" + key + "-INIT", cancellationToken).ConfigureAwait(false);
                    if (initId <= 0) return ErpSimpleWriteResult.Fail("invalid", "Recognise the lease (month 0) first.");
                    var initDescription = await ErpDb.StringAsync(c, null, ErpDb.Positional("SELECT IFNULL(`description`,'') FROM `epc_erp_gl_journals` WHERE `id`=?"), cancellationToken, initId).ConfigureAwait(false) ?? "";
                    if (!initDescription.Contains(": " + terms + " →", StringComparison.Ordinal))
                        return ErpSimpleWriteResult.Fail("invalid", "Lease terms differ from the recognised lease (" + initDescription + "). Use the original terms or recognise a new lease reference.");
                    var row = ErpJwIfrsAdjustments.LeaseSchedule(payment, months, rate)[month - 1];
                    recipe = ErpJwIfrsAdjustments.LeaseMonth(row, ErpAutoPartsIfrs.BankAccount);
                    reference = "JW-IFRS16-" + key + "-M" + month.ToString("000", CultureInfo.InvariantCulture);
                    description = "IFRS 16 lease " + key + " month " + month.ToString(CultureInfo.InvariantCulture) + ": interest " + row.Interest.ToString("N2", CultureInfo.InvariantCulture) + ", principal " + row.Principal.ToString("N2", CultureInfo.InvariantCulture);
                }

                break;
            }

            default:
                return ErpSimpleWriteResult.Fail("invalid", "Unknown adjustment.");
        }

        if (recipe.Count == 0) return ErpSimpleWriteResult.Ok("Nothing to post — " + description, 0);
        if (recipe.Sum(l => l.Debit) != recipe.Sum(l => l.Credit)) return ErpSimpleWriteResult.Fail("invalid", "Adjustment does not balance.");
        if (await ErpAutoPartsGl.PostedJournalIdAsync(c, reference, cancellationToken).ConfigureAwait(false) > 0)
            return ErpSimpleWriteResult.Fail("duplicate", reference + " is already posted.");
        var preview = reference + ": " + string.Join("; ", recipe.Select(l => (l.Debit > 0 ? "Dr " : "Cr ") + l.Code + " " + (l.Debit > 0 ? l.Debit : l.Credit).ToString("N2", CultureInfo.InvariantCulture)));
        if (!request.Confirm) return ErpSimpleWriteResult.Ok("Dry run — would post " + preview, 0);

        await using var tx = await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        long id;
        try
        {
            if (after is not null) await after(tx).ConfigureAwait(false);
            id = await ErpAutoPartsGl.PostAsync(_gl, c, tx, request.ActorId, companyId, 0, reference, description, recipe, date, cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Posting failed — check the period is open.");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return ErpSimpleWriteResult.Ok("Posted " + preview, id);
    }

    private static string Slug(string raw)
        => new string(raw.ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '-').Take(24).ToArray());
}
