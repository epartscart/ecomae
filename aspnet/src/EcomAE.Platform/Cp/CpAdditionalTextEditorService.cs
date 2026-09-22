using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpAdditionalTextRow(long Id, string Url);

public sealed record CpAdditionalTextList(IReadOnlyList<CpAdditionalTextRow> Rows, int Total, int Page, int PageSize, string SortField, bool SortDesc, string Search, bool ExactSearch)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / Math.Max(1, PageSize));
}

public sealed record CpAdditionalTextEditor(
    long Id,
    string Url,
    string Content,
    string ContentLangStrId,
    int BeforeMain,
    string TitleTag,
    string TitleLangStrId,
    string DescriptionTag,
    string DescriptionLangStrId,
    string KeywordsTag,
    string KeywordsLangStrId);

public interface ICpAdditionalTextEditorService
{
    Task<CpAdditionalTextList> ListAsync(int page, int pageSize, string? sortField, bool sortDesc, string? search, bool exact, CancellationToken cancellationToken = default);

    Task<CpAdditionalTextEditor?> OpenByUrlAsync(string url, CancellationToken cancellationToken = default);

    Task<CpAdditionalTextEditor?> OpenByIdAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>Read side of the CP additional-texts twin (<c>content/text_for_url/text_for_url_list.php</c> / <c>text_for_url.php</c>).</summary>
public sealed class CpAdditionalTextEditorService : ICpAdditionalTextEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpAdditionalTextEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpAdditionalTextList> ListAsync(int page, int pageSize, string? sortField, bool sortDesc, string? search, bool exact, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var sort = sortField == "url" ? "url" : "id";
        var term = (search ?? string.Empty).Trim();
        var rows = new List<CpAdditionalTextRow>();
        var total = 0;
        if (!_connections.IsConfigured)
        {
            return new(rows, total, page, pageSize, sort, sortDesc, term, exact);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var where = term.Length == 0 ? "" : exact ? " WHERE `url` = ?" : " WHERE `url` LIKE ?";
            var arg = term.Length == 0 ? null : exact ? term : "%" + term + "%";

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM `text_for_url`" + where;
                if (arg is not null) ErpDb.AddParameters(cmd, arg);
                total = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`url`,'') FROM `text_for_url`" + where
                    + " ORDER BY `" + sort + "` " + (sortDesc ? "DESC" : "ASC") + " LIMIT ? OFFSET ?";
                if (arg is not null) ErpDb.AddParameters(cmd, arg, pageSize, (page - 1) * pageSize);
                else ErpDb.AddParameters(cmd, pageSize, (page - 1) * pageSize);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAdditionalTextRow(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1)));
                }
            }
        }
        catch (DbException)
        {
        }

        return new(rows, total, page, pageSize, sort, sortDesc, term, exact);
    }

    public Task<CpAdditionalTextEditor?> OpenByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        url = (url ?? string.Empty).Trim();
        return url.Length == 0 ? Task.FromResult<CpAdditionalTextEditor?>(null) : OpenAsync("`url` = ?", url, cancellationToken);
    }

    public Task<CpAdditionalTextEditor?> OpenByIdAsync(long id, CancellationToken cancellationToken = default) =>
        id <= 0 ? Task.FromResult<CpAdditionalTextEditor?>(null) : OpenAsync("`id` = ?", id, cancellationToken);

    private async Task<CpAdditionalTextEditor?> OpenAsync(string where, object key, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            long id;
            string url, content, title, description, keywords;
            int beforeMain;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`url`,''), IFNULL(`content`,''), IFNULL(`before_main`,0), IFNULL(`title_tag`,''), IFNULL(`description_tag`,''), IFNULL(`keywords_tag`,'') FROM `text_for_url` WHERE " + where + " LIMIT 1";
                ErpDb.AddParameters(cmd, key);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                url = reader.GetString(1);
                content = reader.GetString(2);
                beforeMain = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture);
                title = reader.GetString(4);
                description = reader.GetString(5);
                keywords = reader.GetString(6);
            }

            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            return new CpAdditionalTextEditor(
                id,
                url,
                await translate(content).ConfigureAwait(false),
                content,
                beforeMain,
                await translate(title).ConfigureAwait(false),
                title,
                await translate(description).ConfigureAwait(false),
                description,
                await translate(keywords).ConfigureAwait(false),
                keywords);
        }
        catch (DbException)
        {
            return null;
        }
    }
}
