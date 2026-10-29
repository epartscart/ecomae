using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Stock ledger row: weight, rate and value for one metal stock code (opening derived from closing − in + out).</summary>
public sealed record ErpJwStockLedgerRow(
    string StockCode,
    string Description,
    string Karat,
    decimal Purity,
    decimal OpenPcs,
    decimal OpenGross,
    decimal OpenPure,
    decimal InPcs,
    decimal InGross,
    decimal InPure,
    decimal InValue,
    decimal OutPcs,
    decimal OutGross,
    decimal OutPure,
    decimal OutValue,
    decimal ClosePcs,
    decimal CloseGross,
    decimal ClosePure,
    decimal Rate,
    decimal CloseValue);

/// <summary>Metal trial balance row: pure-weight Dr/Cr with rate and value (stock at cost, parties at today's 24K rate).</summary>
public sealed record ErpJwMetalTbRow(
    string Group,
    string Code,
    string Name,
    string Karat,
    decimal GrossWeight,
    decimal PureDebit,
    decimal PureCredit,
    decimal Rate,
    decimal ValueDebit,
    decimal ValueCredit);

public sealed record ErpJwMetalReportsResult<T>(IReadOnlyList<T> Rows, decimal Rate24K, string Source, string Message);

public interface IErpJwMetalReportsReadService
{
    Task<ErpJwMetalReportsResult<ErpJwStockLedgerRow>> StockLedgerAsync(int companyId, CancellationToken cancellationToken = default);

    Task<ErpJwMetalReportsResult<ErpJwMetalTbRow>> MetalTrialBalanceAsync(int companyId, CancellationToken cancellationToken = default);
}

public sealed class ErpJwMetalReportsReadService : IErpJwMetalReportsReadService
{
    private static readonly HashSet<string> InTypes = new(
        ErpJwVocTypes.MetalPurchaseAliases.Concat(ErpJwVocTypes.DiamondPurchaseAliases).Concat(ErpJwVocTypes.SalesReturnAliases),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> OutTypes = new(
        ErpJwVocTypes.RetailAliases.Concat(ErpJwVocTypes.MetalSaleAliases).Concat(ErpJwVocTypes.SalesAnalysisAliases),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> PurchaseTypes = new(
        ErpJwVocTypes.MetalPurchaseAliases.Concat(ErpJwVocTypes.DiamondPurchaseAliases), StringComparer.OrdinalIgnoreCase);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwMetalReportsReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    private sealed record StockMaster(string Code, string Description, string Karat, decimal Purity, decimal Pcs, decimal Gross, decimal Value, decimal CostGms);

    private sealed record Movement(string VocType, bool Fixed, string Party, string PartyName, string Stock, decimal Pcs, decimal Gross, decimal Pure, decimal Value);

    public async Task<ErpJwMetalReportsResult<ErpJwStockLedgerRow>> StockLedgerAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            var stock = await StockAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
            var moves = await MovementsAsync(connection, companyId, stock, cancellationToken).ConfigureAwait(false);
            var rows = stock.Values
                .OrderBy(s => s.Karat, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
                .Select(s => BuildLedgerRow(s, moves.Where(m => string.Equals(m.Stock, s.Code, StringComparison.OrdinalIgnoreCase)).ToList()))
                .Where(r => r.ClosePcs != 0 || r.CloseGross != 0 || r.InGross != 0 || r.OutGross != 0 || r.CloseValue != 0)
                .ToList();
            return new(rows, 0, "database", string.Empty);
        }
        catch (DbException ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpJwMetalReportsResult<ErpJwMetalTbRow>> MetalTrialBalanceAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            var rate = R(await ErpGoldRateBoard.ResolveSellAsync(
                connection, companyId, "24K", "AED", DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false), 5);
            var stock = await StockAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
            var moves = await MovementsAsync(connection, companyId, stock, cancellationToken).ConfigureAwait(false);
            var fixings = await FixingsAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
            return new(BuildTrialBalance(stock.Values, moves, fixings, rate), rate, "database", string.Empty);
        }
        catch (DbException ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    private static ErpJwStockLedgerRow BuildLedgerRow(StockMaster s, IReadOnlyList<Movement> moves)
    {
        var inMoves = moves.Where(m => InTypes.Contains(m.VocType)).ToList();
        var outMoves = moves.Where(m => OutTypes.Contains(m.VocType) && !InTypes.Contains(m.VocType)).ToList();
        var closePure = R(s.Gross * s.Purity, 4);
        var inPcs = inMoves.Sum(m => m.Pcs);
        var inGross = R(inMoves.Sum(m => m.Gross), 4);
        var inPure = R(inMoves.Sum(m => m.Pure), 4);
        var outPcs = outMoves.Sum(m => m.Pcs);
        var outGross = R(outMoves.Sum(m => m.Gross), 4);
        var outPure = R(outMoves.Sum(m => m.Pure), 4);
        var rate = s.Gross > 0 ? R(s.Value / s.Gross, 5) : R(s.CostGms, 5);
        return new(
            s.Code, s.Description, s.Karat, s.Purity,
            s.Pcs - inPcs + outPcs, R(s.Gross - inGross + outGross, 4), R(closePure - inPure + outPure, 4),
            inPcs, inGross, inPure, R(inMoves.Sum(m => m.Value), 2),
            outPcs, outGross, outPure, R(outMoves.Sum(m => m.Value), 2),
            s.Pcs, s.Gross, closePure, rate, R(s.Value, 2));
    }

    /// <summary>
    /// Stock is a pure-weight debit per karat. Unfixed purchases credit the supplier's metal account,
    /// unfixed sales debit the customer's; purchase fixing (PF) debits the supplier weight it settles,
    /// sales fixing (SF) credits the customer weight.
    /// </summary>
    internal static IReadOnlyList<ErpJwMetalTbRow> BuildTrialBalance(
        IEnumerable<ErpJwMetalReportsReadService.StockMasterInput> stockInput,
        IEnumerable<ErpJwMetalReportsReadService.MovementInput> movesInput,
        IEnumerable<(string Type, string Party, string PartyName, decimal Qty)> fixings,
        decimal rate)
        => BuildTrialBalance(
            stockInput.Select(s => new StockMaster(s.Code, s.Description, s.Karat, s.Purity, s.Pcs, s.Gross, s.Value, 0)),
            movesInput.Select(m => new Movement(m.VocType, m.Fixed, m.Party, m.PartyName, "", 0, 0, m.Pure, 0)).ToList(),
            fixings.ToList(),
            rate);

    public sealed record StockMasterInput(string Code, string Description, string Karat, decimal Purity, decimal Pcs, decimal Gross, decimal Value);

    public sealed record MovementInput(string VocType, bool Fixed, string Party, string PartyName, decimal Pure);

    private static IReadOnlyList<ErpJwMetalTbRow> BuildTrialBalance(
        IEnumerable<StockMaster> stock,
        IReadOnlyList<Movement> moves,
        IReadOnlyList<(string Type, string Party, string PartyName, decimal Qty)> fixings,
        decimal rate)
    {
        var rows = new List<ErpJwMetalTbRow>();
        foreach (var g in stock.Where(s => s.Gross != 0 || s.Value != 0).GroupBy(s => s.Karat).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var gross = R(g.Sum(s => s.Gross), 4);
            var pure = R(g.Sum(s => s.Gross * s.Purity), 4);
            var value = R(g.Sum(s => s.Value), 2);
            rows.Add(new("Stock", "STK-" + g.Key, "Metal stock " + g.Key + "K", g.Key, gross,
                pure > 0 ? pure : 0, pure < 0 ? -pure : 0, pure != 0 ? R(value / pure, 5) : 0,
                value > 0 ? value : 0, value < 0 ? -value : 0));
        }

        var party = new Dictionary<string, (string Name, decimal Net)>(StringComparer.OrdinalIgnoreCase);
        void Add(string code, string name, decimal net)
        {
            if (string.IsNullOrWhiteSpace(code) || net == 0) return;
            party[code] = party.TryGetValue(code, out var cur)
                ? (string.IsNullOrWhiteSpace(cur.Name) ? name : cur.Name, cur.Net + net)
                : (name, net);
        }

        foreach (var m in moves.Where(m => !m.Fixed))
        {
            if (PurchaseTypes.Contains(m.VocType)) Add(m.Party, m.PartyName, -m.Pure);
            else if (OutTypes.Contains(m.VocType)) Add(m.Party, m.PartyName, m.Pure);
        }

        foreach (var f in fixings)
        {
            if (string.Equals(f.Type, "PF", StringComparison.OrdinalIgnoreCase)) Add(f.Party, f.PartyName, f.Qty);
            else if (string.Equals(f.Type, "SF", StringComparison.OrdinalIgnoreCase)) Add(f.Party, f.PartyName, -f.Qty);
        }

        foreach (var (code, (name, net)) in party.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var pure = R(net, 4);
            if (pure == 0) continue;
            var value = R(Math.Abs(pure) * rate, 2);
            rows.Add(new("Party metal", code, name, "24", 0,
                pure > 0 ? pure : 0, pure < 0 ? -pure : 0, rate,
                pure > 0 ? value : 0, pure < 0 ? value : 0));
        }

        return rows;
    }

    private static async Task<Dictionary<string, StockMaster>> StockAsync(DbConnection connection, int companyId, CancellationToken ct)
    {
        var map = new Dictionary<string, StockMaster>(StringComparer.OrdinalIgnoreCase);
        if (!await ErpDb.TableKnownAsync(connection, "epc_jewel_metal_stock", ct).ConfigureAwait(false)) return map;
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `item_code`,IFNULL(`description`,''),IFNULL(`karat`,''),IFNULL(`purity`,0),IFNULL(`stock_pcs`,0),IFNULL(`stock_gms`,0),IFNULL(`stock_value`,0),IFNULL(`pur_cost_gms`,0)"
            + " FROM `epc_jewel_metal_stock`" + (companyId > 0 ? " WHERE `company_id` IN (0,?)" : ""));
        if (companyId > 0) ErpDb.AddParameters(command, companyId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var code = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
            if (code.Length == 0) continue;
            map[code] = new(code, reader.GetString(1), reader.GetString(2), D(reader, 3), D(reader, 4), D(reader, 5), D(reader, 6), D(reader, 7));
        }

        return map;
    }

    private static async Task<List<Movement>> MovementsAsync(DbConnection connection, int companyId, IReadOnlyDictionary<string, StockMaster> stock, CancellationToken ct)
    {
        var list = new List<Movement>();
        if (!await ErpDb.TableKnownAsync(connection, "epc_jewel_voucher_lines", ct).ConfigureAwait(false)) return list;
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT v.`voc_type`,IFNULL(v.`is_fixed`,0),IFNULL(v.`party_code`,''),IFNULL(v.`party_name`,''),IFNULL(l.`stock_code`,''),IFNULL(l.`pcs`,0),IFNULL(l.`gr_wt`,0),IFNULL(l.`pure_wt`,0),IFNULL(l.`purity`,0),IFNULL(l.`total_amount`,0)"
            + " FROM `epc_jewel_voucher_lines` l INNER JOIN `epc_jewel_voucher` v ON v.`id`=l.`voucher_id`"
            + " WHERE v.`status` NOT IN ('draft','cancelled')" + (companyId > 0 ? " AND v.`company_id` IN (0,?)" : ""));
        if (companyId > 0) ErpDb.AddParameters(command, companyId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var code = reader.GetString(4).Trim();
            var gross = D(reader, 6);
            var pure = D(reader, 7);
            if (pure == 0)
            {
                var purity = D(reader, 8);
                if (purity == 0 && stock.TryGetValue(code, out var master)) purity = master.Purity;
                pure = R(gross * purity, 4);
            }

            list.Add(new(reader.GetString(0).Trim(), Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0,
                reader.GetString(2).Trim(), reader.GetString(3).Trim(), code, D(reader, 5), gross, pure, D(reader, 9)));
        }

        return list;
    }

    private static async Task<List<(string, string, string, decimal)>> FixingsAsync(DbConnection connection, int companyId, CancellationToken ct)
    {
        var list = new List<(string, string, string, decimal)>();
        if (!await ErpDb.TableKnownAsync(connection, "epc_jewel_fixing", ct).ConfigureAwait(false)) return list;
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`fix_type`,''),IFNULL(`party_code`,''),IFNULL(`party_name`,''),IFNULL(`fix_qty_gms`,0) FROM `epc_jewel_fixing`"
            + " WHERE IFNULL(`status`,'') NOT IN ('cancelled','void')" + (companyId > 0 ? " AND `company_id` IN (0,?)" : ""));
        if (companyId > 0) ErpDb.AddParameters(command, companyId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add((reader.GetString(0).Trim(), reader.GetString(1).Trim(), reader.GetString(2).Trim(), D(reader, 3)));
        }

        return list;
    }

    private static decimal D(DbDataReader r, int i)
        => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);

    private static decimal R(decimal v, int dp) => Math.Round(v, dp, MidpointRounding.AwayFromZero);
}
