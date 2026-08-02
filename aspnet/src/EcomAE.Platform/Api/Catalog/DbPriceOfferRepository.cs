using System.Data.Common;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Read-only MySQL repository for <c>/api/v1/price/lookup</c>.
/// Executes <see cref="LegacyPriceLookupSql.LookupOffers"/> against
/// <c>shop_docpart_prices_data</c> with tenant database scoping.
/// Performs zero writes.
/// </summary>
public sealed class DbPriceOfferRepository : IPriceOfferRepository
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<PriceLookupOptions> _options;

    public DbPriceOfferRepository(
        ITenantDbConnectionFactory connections,
        IHttpContextAccessor httpContextAccessor,
        IOptions<PriceLookupOptions> options)
    {
        _connections = connections;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    public async Task<IReadOnlyCollection<PriceOfferRow>> FindOffersAsync(
        string normalizedBrand,
        string normalizedArticle,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured
            || string.IsNullOrWhiteSpace(normalizedBrand)
            || string.IsNullOrWhiteSpace(normalizedArticle))
        {
            return [];
        }

        var databaseName = ResolveDatabaseName();
        await using var connection = await _connections.OpenAsync(databaseName, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyPriceLookupSql.LookupOffers;

        AddParameter(command, "@brand", normalizedBrand);
        AddParameter(command, "@article", normalizedArticle);

        var rows = new List<PriceOfferRow>(LegacyPriceLookupSql.DefaultLimit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(PriceOfferRowMapper.FromLegacyPriceLookup(
                reader["manufacturer"] as string,
                reader["article"] as string,
                reader["name"] as string,
                reader["price"],
                reader["exist"],
                reader["storage"] as string,
                reader["time_to_exe"] as string));

            if (rows.Count >= LegacyPriceLookupSql.DefaultLimit)
            {
                break;
            }
        }

        return rows;
    }

    private string? ResolveDatabaseName()
    {
        if (_httpContextAccessor.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
            && !string.IsNullOrWhiteSpace(tenant.DatabaseName))
        {
            return tenant.DatabaseName;
        }

        return string.IsNullOrWhiteSpace(_options.Value.DatabaseName)
            ? null
            : _options.Value.DatabaseName.Trim();
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
