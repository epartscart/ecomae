using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Read-only brand parts listing from <c>shop_docpart_prices_data</c>.
/// Mirrors PHP <c>epc_brand_parts_payload</c>. Performs zero writes.
/// </summary>
public sealed class DbCatalogBrandPartsRepository : ICatalogBrandPartsRepository
{
    private readonly ITenantDbConnectionFactory _connections;

    public DbCatalogBrandPartsRepository(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<(int TotalRows, IReadOnlyList<CatalogBrandPartRow> Page)> FindByBrandAsync(
        string brandUpper,
        string brandCompact,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || string.IsNullOrWhiteSpace(brandUpper))
        {
            return (0, []);
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = LegacyCatalogBrandPartsSql.CountDistinctArticles;
        AddParameter(countCommand, "@brand", brandUpper);
        AddParameter(countCommand, "@brandCompact", brandCompact);
        var totalObj = await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var total = Convert.ToInt32(totalObj ?? 0, CultureInfo.InvariantCulture);

        await using var pageCommand = connection.CreateCommand();
        pageCommand.CommandText = LegacyCatalogBrandPartsSql.SelectPage;
        AddParameter(pageCommand, "@brand", brandUpper);
        AddParameter(pageCommand, "@brandCompact", brandCompact);
        AddParameter(pageCommand, "@limit", limit);
        AddParameter(pageCommand, "@offset", offset);

        var rows = new List<CatalogBrandPartRow>();
        await using var reader = await pageCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CatalogBrandPartRow(
                Convert.ToString(reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["article_show"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["article"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["name"], CultureInfo.InvariantCulture),
                Convert.ToDecimal(reader["exist"], CultureInfo.InvariantCulture),
                Convert.ToDecimal(reader["price"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["time_to_exe"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["storage"], CultureInfo.InvariantCulture)));
        }

        return (total, rows);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
