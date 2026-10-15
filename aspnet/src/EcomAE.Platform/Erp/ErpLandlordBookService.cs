using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

public interface IErpLandlordBook
{
    Task EnsureOpeningAsync(string? siteKey, CancellationToken cancellationToken = default);

    Task<ErpLandlordBoard> LoadAsync(int year, long propertyId, long unitId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveLineAsync(ErpLandlordEntry entry, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DistributeAsync(long lineId, long unitId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> VoidAsync(long lineId, CancellationToken cancellationToken = default);

    Task<string> CsvAsync(int year, long propertyId, long unitId, CancellationToken cancellationToken = default);
}

public sealed record ErpLandlordEntry(
    long PropertyId,
    long UnitId,
    int Year,
    string Kind,
    string Category,
    decimal Amount,
    string Spread,
    string Note,
    string? MovementDate,
    string? Code);

public sealed record ErpLandlordBoard(
    int Year,
    long PropertyId,
    long UnitId,
    ErpLandlordTotals Totals,
    ErpLandlordTotals Life,
    IReadOnlyList<int> Years,
    IReadOnlyList<ErpLandlordProperty> Properties,
    IReadOnlyList<ErpLandlordUnit> Units,
    IReadOnlyList<ErpLandlordLine> Lines,
    IReadOnlyList<ErpLandlordRollup> Rollup,
    string Message);

public sealed class ErpLandlordBookService : IErpLandlordBook
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpLandlordBookService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task EnsureOpeningAsync(string? siteKey, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || !string.Equals(siteKey, "real", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var connection = await OpenReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpLandlordSql.ApplyOpeningAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpLandlordBoard> LoadAsync(int year, long propertyId, long unitId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Empty(year, propertyId, unitId, "The company database is not configured.");
        }

        await using var connection = await OpenReadyAsync(cancellationToken).ConfigureAwait(false);
        var properties = await ErpLandlordSql.PropertiesAsync(connection, cancellationToken).ConfigureAwait(false);
        var units = await ErpLandlordSql.UnitsAsync(connection, propertyId, cancellationToken).ConfigureAwait(false);
        var lines = await ErpLandlordSql.LinesAsync(connection, cancellationToken).ConfigureAwait(false);
        var years = lines.Select(line => line.Year).Where(value => value > 0).Distinct().Order().ToArray();
        if (year > 0 && !years.Contains(year))
        {
            years = years.Append(year).Order().ToArray();
        }

        if (!years.Contains(ErpLandlordOpening.SheetYear))
        {
            years = years.Append(ErpLandlordOpening.SheetYear).Order().ToArray();
        }

        return new ErpLandlordBoard(
            year,
            propertyId,
            unitId,
            ErpLandlordCalc.Summarize(lines, year, propertyId, unitId),
            ErpLandlordCalc.Summarize(lines, 0, propertyId, unitId),
            years,
            properties,
            units,
            lines.Where(line => ErpLandlordCalc.InYear(line, year)
                && (propertyId <= 0 || line.PropertyId == propertyId)
                && (unitId <= 0
                    ? line.ParentId == 0
                    : ErpLandlordCalc.CountsInView(line, unitId)
                        || (line.ParentId == 0 && line.UnitId == 0 && string.Equals(line.Kind, "expense", StringComparison.OrdinalIgnoreCase)))).ToArray(),
            ErpLandlordCalc.ByProperty(lines, properties, year),
            "");
    }

    public async Task<ErpSimpleWriteResult> SaveLineAsync(ErpLandlordEntry entry, CancellationToken cancellationToken = default)
        => await WriteAsync((connection, tx, ct) => ErpLandlordSql.SaveAsync(connection, tx, entry, 0, ct), cancellationToken).ConfigureAwait(false);

    public async Task<ErpSimpleWriteResult> DistributeAsync(long lineId, long unitId, CancellationToken cancellationToken = default)
        => await WriteAsync((connection, tx, ct) => ErpLandlordSql.DistributeAsync(connection, tx, lineId, unitId, ct), cancellationToken).ConfigureAwait(false);

    public async Task<ErpSimpleWriteResult> VoidAsync(long lineId, CancellationToken cancellationToken = default)
        => await WriteAsync((connection, tx, ct) => ErpLandlordSql.VoidAsync(connection, tx, lineId, ct), cancellationToken).ConfigureAwait(false);

    public async Task<string> CsvAsync(int year, long propertyId, long unitId, CancellationToken cancellationToken = default)
    {
        var board = await LoadAsync(year, propertyId, unitId, cancellationToken).ConfigureAwait(false);
        var csv = new StringBuilder();
        csv.AppendLine("Year,Property,Holder,Flat,Kind,Category,Amount,Spread,Date,Note");
        foreach (var line in board.Lines.Where(line => ErpLandlordCalc.CountsInView(line, unitId)))
        {
            csv.Append(line.Year.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(line.PropertyName)).Append(',')
                .Append(Csv(board.Properties.FirstOrDefault(p => p.Id == line.PropertyId)?.Holder ?? "")).Append(',')
                .Append(Csv(line.UnitCode)).Append(',')
                .Append(Csv(line.Kind)).Append(',')
                .Append(Csv(line.Category)).Append(',')
                .Append(line.Amount.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(line.Spread)).Append(',')
                .Append(Csv(line.MovementDate)).Append(',')
                .Append(Csv(line.Note)).AppendLine();
        }

        return csv.ToString();
    }

    private async Task<ErpSimpleWriteResult> WriteAsync(
        Func<DbConnection, DbTransaction, CancellationToken, Task<ErpSimpleWriteResult>> work,
        CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "The company database is not configured.");
        }

        await using var connection = await OpenReadyAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var result = await work(connection, tx, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<DbConnection> OpenReadyAsync(CancellationToken cancellationToken)
    {
        var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpRealEstateWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpLandlordSql.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static ErpLandlordBoard Empty(int year, long propertyId, long unitId, string message)
        => new(year, propertyId, unitId, new ErpLandlordTotals(0, 0, 0, 0, 0, 0, false), new ErpLandlordTotals(0, 0, 0, 0, 0, 0, false), [ErpLandlordOpening.SheetYear], [], [], [], [], message);

    private static string Csv(string? value)
    {
        var text = value ?? "";
        return text.Contains('"') || text.Contains(',') || text.Contains('\n')
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }
}
