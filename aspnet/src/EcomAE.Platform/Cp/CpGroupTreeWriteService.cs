using System.Globalization;
using System.Net;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>user_groups.php</c> save_tree twin: full upsert/delete of the <c>groups</c> hierarchy.</summary>
public interface ICpGroupTreeWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        IReadOnlyList<CpGroupDraft> rows,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default);

    Task<CpGroupTreeReadResult> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record CpGroupDraft(
    long Id,
    string Value,
    string ValueLangStrId,
    string Description,
    string DescriptionLangStrId,
    long Parent,
    int Unblocked,
    int ForGuests,
    int ForRegistrated,
    int ForBackend,
    int ForPercentage);

public sealed record CpGroupTreeRow(
    long Id,
    string Value,
    string ValueLangStrId,
    string Description,
    string DescriptionLangStrId,
    long Parent,
    int Level,
    int ChildCount,
    int SortOrder,
    int Unblocked,
    int ForGuests,
    int ForRegistrated,
    int ForBackend,
    int ForPercentage);

public sealed record CpGroupTreeReadResult(
    IReadOnlyList<CpGroupTreeRow> Groups,
    string Source,
    string Message);

public sealed class CpGroupTreeWriteService : ICpGroupTreeWriteService
{
    public const int MaxRows = 400;
    private const string Prefix = "g";
    private readonly IErpWriteConnectionFactory _connections;
    private readonly CpCustomTranslationWriter _translations = new("GROUPS EDITING");

    public CpGroupTreeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>Reads the editable grid posted by <c>/cp/groups-app</c> (fields <c>g_idx</c>, <c>g_{i}_*</c>).</summary>
    public static (IReadOnlyList<CpGroupDraft> Rows, string? Error) ParseForm(IFormCollection form)
    {
        if (!form.TryGetValue(Prefix + "_idx", out var idx))
        {
            return ([], "No group rows were posted.");
        }

        var guests = form[Prefix + "_for_guests"].ToString();
        var registrated = form[Prefix + "_for_registrated"].ToString();
        var backend = form[Prefix + "_for_backend"].ToString();
        var rows = new List<CpGroupDraft>();
        foreach (var raw in idx)
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) || i < 0)
            {
                continue;
            }

            var key = Prefix + "_" + i.ToString(CultureInfo.InvariantCulture) + "_";
            var value = Normalize(form[key + "value"].ToString(), 255);
            if (value.Length == 0)
            {
                continue;
            }

            long.TryParse(form[key + "id"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id);
            long.TryParse(form[key + "parent"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parent);
            var marker = i.ToString(CultureInfo.InvariantCulture);
            rows.Add(new CpGroupDraft(
                id,
                value,
                form[key + "value_lang_str_id"].ToString().Trim(),
                Normalize(form[key + "description"].ToString(), 2000),
                form[key + "description_lang_str_id"].ToString().Trim(),
                parent < 0 ? 0 : parent,
                Checked(form, key + "unblocked"),
                guests == marker ? 1 : 0,
                registrated == marker ? 1 : 0,
                backend == marker ? 1 : 0,
                Checked(form, key + "for_percentage")));
        }

        return rows.Count == 0 ? ([], "At least one group is required.") : (rows, null);
    }

    /// <summary>PHP commonCheck() twin: exactly one guests/registered/backend group, all unblocked, no backend ancestry for public roles, ordering resolved parent-first.</summary>
    public static (IReadOnlyList<CpGroupDraft> Ordered, IReadOnlyDictionary<long, int> Levels, string? Error) Validate(IReadOnlyList<CpGroupDraft> rows)
    {
        if (rows.Count > MaxRows)
        {
            return ([], new Dictionary<long, int>(), "Too many groups.");
        }

        if (rows.Count(r => r.ForGuests == 1) != 1)
        {
            return ([], new Dictionary<long, int>(), "Exactly one group must be marked for guests.");
        }

        if (rows.Count(r => r.ForRegistrated == 1) != 1)
        {
            return ([], new Dictionary<long, int>(), "Exactly one group must be marked for registration.");
        }

        if (rows.Count(r => r.ForBackend == 1) != 1)
        {
            return ([], new Dictionary<long, int>(), "Exactly one group must be marked for backend administrators.");
        }

        var backendRow = rows.First(r => r.ForBackend == 1);
        if (backendRow.ForGuests == 1 || backendRow.ForRegistrated == 1)
        {
            return ([], new Dictionary<long, int>(), "The backend group cannot also be the guests or registration group.");
        }

        var byId = new Dictionary<long, CpGroupDraft>();
        foreach (var row in rows)
        {
            if (row.Id > 0 && !byId.TryAdd(row.Id, row))
            {
                return ([], new Dictionary<long, int>(), "Group id " + row.Id.ToString(CultureInfo.InvariantCulture) + " is duplicated.");
            }
        }

        foreach (var row in rows)
        {
            if (row.Parent != 0 && (!byId.ContainsKey(row.Parent) || row.Parent == row.Id))
            {
                return ([], new Dictionary<long, int>(), "Group '" + WebUtility.HtmlDecode(row.Value) + "' points to a missing parent.");
            }
        }

        var levels = new Dictionary<long, int>();
        foreach (var row in rows)
        {
            var depth = 1;
            var cursor = row.Parent;
            var guard = 0;
            while (cursor != 0)
            {
                depth++;
                if (++guard > rows.Count)
                {
                    return ([], new Dictionary<long, int>(), "Group hierarchy contains a cycle.");
                }

                cursor = byId[cursor].Parent;
            }

            if (row.Id > 0)
            {
                levels[row.Id] = depth;
            }
        }

        foreach (var role in rows.Where(r => r.ForGuests == 1 || r.ForRegistrated == 1 || r.ForBackend == 1))
        {
            if (role.Unblocked != 1)
            {
                return ([], new Dictionary<long, int>(), "Guests, registration and backend groups must be unblocked.");
            }

            var cursor = role.Parent;
            while (cursor != 0)
            {
                var ancestor = byId[cursor];
                if (ancestor.Unblocked != 1)
                {
                    return ([], new Dictionary<long, int>(), "A parent of a guests/registration/backend group is blocked.");
                }

                if (role.ForBackend != 1 && ancestor.ForBackend == 1)
                {
                    return ([], new Dictionary<long, int>(), "Guests and registration groups cannot sit under the backend group.");
                }

                cursor = ancestor.Parent;
            }
        }

        var ordered = new List<CpGroupDraft>(rows.Count);
        var emitted = new HashSet<long>();
        void Emit(long parent)
        {
            foreach (var row in rows.Where(r => r.Parent == parent))
            {
                if (row.Id > 0 && !emitted.Add(row.Id))
                {
                    continue;
                }

                ordered.Add(row);
                if (row.Id > 0)
                {
                    Emit(row.Id);
                }
            }
        }

        Emit(0);
        foreach (var row in rows.Where(r => r.Id <= 0 && !ordered.Contains(r)))
        {
            ordered.Add(row);
        }

        return (ordered, levels, null);
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        IReadOnlyList<CpGroupDraft> rows,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default)
    {
        var (ordered, levels, error) = Validate(rows);
        if (error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lang = CpCustomTranslationWriter.NormalizeLang(langCode);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var nextId = await ErpDb.LongAsync(
                connection, transaction, ErpDb.Positional("SELECT IFNULL(MAX(`id`),0) + 1 FROM `groups`"), cancellationToken).ConfigureAwait(false);
            var assigned = new Dictionary<CpGroupDraft, long>(ReferenceEqualityComparer.Instance);
            foreach (var row in ordered)
            {
                assigned[row] = row.Id > 0 ? row.Id : nextId++;
            }

            var keep = new List<long>(ordered.Count);
            for (var i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                var id = assigned[row];
                var level = row.Parent == 0 ? 1 : levels.TryGetValue(row.Parent, out var parentLevel) ? parentLevel + 1 : 1;
                var childCount = ordered.Count(r => r.Parent == id);
                var valueKey = await _translations.SaveAsync(
                    connection, transaction, row.ValueLangStrId, row.Value, lang, domainPath, cancellationToken).ConfigureAwait(false);
                var descriptionKey = await _translations.SaveAsync(
                    connection, transaction, row.DescriptionLangStrId, row.Description, lang, domainPath, cancellationToken).ConfigureAwait(false);

                var exists = row.Id > 0 && await ErpDb.LongAsync(
                    connection, transaction, ErpDb.Positional("SELECT COUNT(*) FROM `groups` WHERE `id` = ?"), cancellationToken, row.Id).ConfigureAwait(false) == 1;
                if (exists)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            UPDATE `groups`
                            SET `value`=?, `count`=?, `level`=?, `parent`=?, `unblocked`=?, `for_guests`=?, `for_registrated`=?,
                                `for_backend`=?, `for_percentage`=?, `description`=?, `order`=?
                            WHERE `id`=?
                            """),
                        cancellationToken,
                        valueKey, childCount, level, row.Parent, row.Unblocked, row.ForGuests, row.ForRegistrated,
                        row.ForBackend, row.ForPercentage, descriptionKey, i + 1, id).ConfigureAwait(false);
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            INSERT INTO `groups`
                            (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`)
                            VALUES (?,?,?,?,?,?,?,?,?,?,?,?)
                            """),
                        cancellationToken,
                        id, valueKey, childCount, level, row.Parent, row.Unblocked, row.ForGuests, row.ForRegistrated,
                        row.ForBackend, row.ForPercentage, descriptionKey, i + 1).ConfigureAwait(false);
                }

                keep.Add(id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `groups` WHERE `id` NOT IN (" + string.Join(",", keep.Select(_ => "?")) + ")"),
                cancellationToken,
                keep.Cast<object?>().ToArray()).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Groups saved.", keep[0], keep.Count);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save groups.");
        }
    }

    public async Task<CpGroupTreeReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpGroupTreeRow>();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT g.`id`, IFNULL(tv.`value`, IFNULL(g.`value`,'')) AS caption, IFNULL(g.`value`,'') AS value_key,
                       IFNULL(td.`value`, '') AS description, IFNULL(g.`description`,'') AS description_key,
                       IFNULL(g.`parent`,0) AS parent, IFNULL(g.`level`,1) AS level, IFNULL(g.`count`,0) AS child_count,
                       IFNULL(g.`order`,0) AS sort_order, IFNULL(g.`unblocked`,0) AS unblocked, IFNULL(g.`for_guests`,0) AS for_guests,
                       IFNULL(g.`for_registrated`,0) AS for_registrated, IFNULL(g.`for_backend`,0) AS for_backend,
                       IFNULL(g.`for_percentage`,0) AS for_percentage
                FROM `groups` g
                LEFT JOIN `lang_text_strings_translation` tv ON tv.`str_key` = g.`value` AND tv.`lang_code` = 'en'
                LEFT JOIN `lang_text_strings_translation` td ON td.`str_key` = g.`description` AND td.`lang_code` = 'en'
                ORDER BY g.`order` ASC, g.`level` ASC, g.`id` ASC
                LIMIT 400
                """;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpGroupTreeRow(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["value_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["description"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["description_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["parent"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["level"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["child_count"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["sort_order"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["unblocked"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["for_guests"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["for_registrated"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["for_backend"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["for_percentage"], CultureInfo.InvariantCulture)));
            }

            return new(rows, "database", string.Empty);
        }
        catch (System.Data.Common.DbException ex)
        {
            return new([], "database-error", ex.Message);
        }
    }

    private static string Normalize(string? raw, int max)
    {
        var text = WebUtility.HtmlEncode((raw ?? string.Empty).Trim());
        return text.Length <= max ? text : text[..max];
    }

    private static int Checked(IFormCollection form, string key)
    {
        var v = form[key].ToString();
        return v is "1" or "on" or "true" ? 1 : 0;
    }
}
