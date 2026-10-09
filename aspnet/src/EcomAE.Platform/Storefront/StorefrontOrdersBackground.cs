using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Read-only port of the storefront order-background include. It loads the shared order-status, line-status and
/// office reference data used by the storefront order pages and protocol handlers.
/// </summary>
public static class StorefrontOrdersBackground
{
    public sealed record ReferenceRow(long Id, IReadOnlyDictionary<string, string?> Fields)
    {
        public string? this[string field] => Fields.GetValueOrDefault(field);
    }

    public sealed record Data(
        IReadOnlyDictionary<long, ReferenceRow> OrderStatuses,
        IReadOnlyDictionary<long, ReferenceRow> ItemStatuses,
        IReadOnlyList<long> ItemStatusesNotCount,
        IReadOnlyDictionary<long, ReferenceRow> Offices);

    public static async Task<Data> LoadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var orderStatuses = await LoadRowsAsync(
            connection,
            "SELECT * FROM `shop_orders_statuses_ref` ORDER BY `order` ASC;",
            cancellationToken).ConfigureAwait(false);
        var itemStatuses = await LoadRowsAsync(
            connection,
            "SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC;",
            cancellationToken).ConfigureAwait(false);
        var offices = await LoadRowsAsync(
            connection,
            "SELECT `id`,`caption` FROM `shop_offices`;",
            cancellationToken).ConfigureAwait(false);

        var notCount = itemStatuses.Values
            .Where(row => IsPhpNumericZero(row["count_flag"]))
            .Select(row => row.Id)
            .ToArray();
        return new(orderStatuses, itemStatuses, notCount, offices);
    }

    private static async Task<Dictionary<long, ReferenceRow>> LoadRowsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var rows = new Dictionary<long, ReferenceRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var fields = new Dictionary<string, string?>(reader.FieldCount, StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                fields[reader.GetName(i)] = reader.IsDBNull(i)
                    ? null
                    : DatabaseString(reader.GetValue(i));
            }

            if (!long.TryParse(fields.GetValueOrDefault("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                id = 0;
            }

            rows[id] = new(id, fields);
        }

        return rows;
    }

    private static bool IsPhpNumericZero(string? value)
        => value is null
           || (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
               && number == 0);

    private static string? DatabaseString(object value)
        => value is bool flag
            ? flag ? "1" : "0"
            : Convert.ToString(value, CultureInfo.InvariantCulture);
}
