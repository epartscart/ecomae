using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

public interface IErpOperatorIssueScan
{
    Task<string> DescribeAsync(CancellationToken cancellationToken);
}

/// <summary>Read-only book check. It never posts a journal or applies a foreign key.</summary>
public sealed class ErpOperatorIssueScan : IErpOperatorIssueScan
{
    private readonly IErpIntegrityService _integrity;

    public ErpOperatorIssueScan(IErpIntegrityService integrity)
    {
        _integrity = integrity;
    }

    public async Task<string> DescribeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ErpIntegrityAccountingRow> rows;
        try
        {
            rows = await _integrity.ScanAccountingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            rows = [];
        }

        return Describe(rows);
    }

    public static string Describe(IReadOnlyList<ErpIntegrityAccountingRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return "The company books are not available from here, so no issue list was built. Open the command centre and run the book check there.";
        }

        var dirty = rows.Where(row => string.Equals(row.Status, "dirty", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (dirty.Length == 0)
        {
            if (rows.All(row => string.Equals(row.Status, "missing", StringComparison.OrdinalIgnoreCase)))
            {
                return "The company books are not available from here, so no issue list was built. Open the command centre and run the book check there.";
            }

            return "Book check is clear. Journals balance, and the customer, supplier, and cash totals match their control accounts.";
        }

        var parts = dirty.Select(row =>
        {
            var line = row.Label;
            if (row.Issues > 1)
            {
                line += " (" + row.Issues.ToString(CultureInfo.InvariantCulture) + ")";
            }

            if (row.Variance != 0m)
            {
                line += ", difference " + row.Variance.ToString("0.00", CultureInfo.InvariantCulture);
            }

            return line;
        });
        var count = dirty.Length.ToString(CultureInfo.InvariantCulture);
        return "Book check found " + count + (dirty.Length == 1 ? " issue. " : " issues. ") + string.Join(". ", parts) + ".";
    }
}
