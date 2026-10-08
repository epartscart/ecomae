using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The storefront profile edit page (<c>/users/editform</c>, PHP <c>content/users/editform.php</c>), byte for byte:
/// the registration fields as a JavaScript list, the registration variant selector, the password block, the form
/// checks and the current values. <see cref="PostAsync"/> is the page's <c>edit_user</c> POST, which answers with the
/// PHP redirect script.
/// </summary>
public static partial class StorefrontEditForm
{
    public static bool IsEditPost(IReadOnlyDictionary<string, string> post) => post.ContainsKey("edit_user");

    /// <summary>The page for the visitor: translation 4709 for a guest, else the edit form.</summary>
    public static async Task<string> RenderAsync(DbConnection connection, StorefrontProfileForm.Request request, Func<string?, Task<string>> t, CancellationToken cancellationToken)
    {
        var userId = await StorefrontProfileForm.UserIdAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (userId == 0)
        {
            return await t("4709").ConfigureAwait(false);
        }

        var sb = new StringBuilder(Html[6]);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `main_flag`, `name`, `caption`, `show_for`, `required_for`, `maxlen`, `regexp`, `widget_type`, `widget_options` FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC";
            var fields = new List<string?[]>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var row = new string?[reader.FieldCount];
                    for (var i = 0; i < row.Length; i++)
                    {
                        row[i] = StorefrontProfileForm.PdoText(reader, i);
                    }

                    fields.Add(row);
                }
            }

            foreach (var field in fields)
            {
                sb.Append(Html[7]).Append(field[0]).Append(Html[8]).Append(field[1]).Append(Html[9]).Append(await t(field[2]).ConfigureAwait(false))
                    .Append(Html[10]).Append(field[3]).Append(Html[11]).Append(field[4]).Append(Html[12]).Append(field[5])
                    .Append(Html[13]).Append(field[6]).Append(Html[14]).Append(field[7]).Append(Html[15]).Append(field[8]).Append(Html[16]);
            }
        }

        var csrf = await StorefrontProfileForm.SessionCsrfAsync(connection, request, cancellationToken).ConfigureAwait(false);
        sb.Append(Html[17]).Append(Html[18]).Append(csrf).Append(Html[19]);

        var variants = new List<(string? Id, string? Caption)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`, `caption` FROM `reg_variants` ORDER BY `order` ASC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                variants.Add((StorefrontProfileForm.PdoText(reader, 0), StorefrontProfileForm.PdoText(reader, 1)));
            }
        }

        if (variants.Count == 1)
        {
            sb.Append(Html[20]).Append(variants[0].Id).Append(Html[21]).Append(await t(variants[0].Caption).ConfigureAwait(false)).Append(Html[22]);
        }
        else
        {
            sb.Append(Html[23]).Append(await t("4716").ConfigureAwait(false)).Append(Html[24]);
            foreach (var (id, caption) in variants)
            {
                sb.Append(Html[25]).Append(id).Append(Html[26]).Append(await t(caption).ConfigureAwait(false)).Append(Html[27]);
            }

            sb.Append(Html[28]);
        }

        sb.Append(Html[29]).Append(await t("4717").ConfigureAwait(false))
            .Append(Html[30]).Append(await t("4717").ConfigureAwait(false))
            .Append(Html[31]).Append(await t("3927").ConfigureAwait(false))
            .Append(Html[32]).Append(await t("3927").ConfigureAwait(false))
            .Append(Html[33]).Append(await t("4718").ConfigureAwait(false))
            .Append(Html[34]).Append(await t("3928").ConfigureAwait(false))
            .Append(Html[35]).Append(await t("2114").ConfigureAwait(false))
            .Append(Html[36]);

        var currentVariant = await UserRegVariantAsync(connection, null, userId, cancellationToken).ConfigureAwait(false);
        sb.Append(Html[37]);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?");
            ErpDb.AddParameters(command, userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                sb.Append(Html[38]).Append(StorefrontProfileForm.PdoText(reader, 0)).Append(Html[39]).Append(StorefrontProfileForm.PdoText(reader, 1)).Append(Html[40]);
            }
        }

        var minLength = request.Config.TryGetValue("min_password_len", out var configured) ? configured : string.Empty;
        sb.Append(Html[41]).Append(Html[42]).Append(currentVariant).Append(Html[43])
            .Append(Html[44]).Append(await t("3933").ConfigureAwait(false))
            .Append(Html[45]).Append(minLength)
            .Append(Html[46]).Append(await t("3934").ConfigureAwait(false))
            .Append(Html[47]).Append(minLength)
            .Append(Html[48]).Append(await t("3935").ConfigureAwait(false))
            .Append(Html[49]).Append(await t("3885").ConfigureAwait(false))
            .Append(Html[50]).Append(await t("3930").ConfigureAwait(false))
            .Append(Html[51]).Append(await t("3893").ConfigureAwait(false))
            .Append(Html[52]).Append(await t("3931").ConfigureAwait(false))
            .Append(Html[53]);
        return sb.ToString();
    }

    /// <summary>
    /// The <c>edit_user</c> POST of a signed-in customer, in one transaction: the CSRF key of the customer session, the
    /// new password when one is given, the registration fields of the posted variant (stored htmlentities-escaped), the
    /// removal of emptied fields, then the variant itself. Answers with the redirect script back to the edit page, the
    /// success text or the error of the failed step; null for a guest or a POST without the flag.
    /// </summary>
    public static async Task<string?> PostAsync(
        DbConnection connection,
        StorefrontProfileForm.Request request,
        IReadOnlyDictionary<string, string> post,
        Func<string?, Task<string>> t,
        CancellationToken cancellationToken)
    {
        var userId = await StorefrontProfileForm.UserIdAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (userId == 0 || !IsEditPost(post))
        {
            return null;
        }

        var csrf = await StorefrontProfileForm.SessionCsrfAsync(connection, request, cancellationToken).ConfigureAwait(false);
        DbTransaction transaction;
        try
        {
            transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return Redirect(request, "error_message", await t("2132").ConfigureAwait(false));
        }

        await using (transaction)
        {
            var failure = "4691";
            try
            {
                if (!post.TryGetValue("csrf_guard_key", out var posted) || csrf is null || !string.Equals(csrf, posted, StringComparison.Ordinal))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Redirect(request, "error_message", await t(failure).ConfigureAwait(false));
                }

                failure = "4710";
                if (post.TryGetValue("password", out var password) && !AuthEmailOtp.PhpEmpty(password))
                {
                    await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `users` SET `password` = ? WHERE `user_id` = ?"), cancellationToken, LegacyLoginSecurity.HashPassword(password), userId).ConfigureAwait(false);
                }

                var variant = post.GetValueOrDefault("reg_variant");
                var fields = await RegFieldsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                foreach (var (name, showFor) in fields)
                {
                    if (!ShownFor(showFor, variant))
                    {
                        continue;
                    }

                    var value = PhpHtmlEntities.Encode(post.GetValueOrDefault(name));
                    var count = await ErpDb.LongAsync(connection, transaction, ErpDb.Positional("SELECT COUNT(*) FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?"), cancellationToken, userId, name).ConfigureAwait(false);
                    if (count == 1)
                    {
                        failure = "4711";
                        await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `users_profiles` SET `data_value` = ? WHERE `data_key` = ? AND `user_id` = ?"), cancellationToken, value, name, userId).ConfigureAwait(false);
                    }
                    else
                    {
                        failure = "4712";
                        await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"), cancellationToken, userId, name, value).ConfigureAwait(false);
                    }
                }

                failure = "4713";
                var fieldNames = fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var key in await ProfileKeysAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false))
                {
                    if (fieldNames.Contains(key) && AuthEmailOtp.PhpEmpty(post.GetValueOrDefault(key)))
                    {
                        await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?"), cancellationToken, userId, key).ConfigureAwait(false);
                    }
                }

                failure = "4714";
                var current = await UserRegVariantAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
                if (!LooseEquals(current, variant))
                {
                    await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `users` SET `reg_variant` = ? WHERE `user_id` = ?"), cancellationToken, variant, userId).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Redirect(request, "error_message", await t(failure).ConfigureAwait(false));
            }
        }

        return Redirect(request, "success_message", await t("4715").ConfigureAwait(false));
    }

    private static string Redirect(StorefrontProfileForm.Request request, string parameter, string message)
        => parameter == "success_message"
            ? Html[3] + request.LangHref + Html[4] + message + Html[5]
            : Html[0] + request.LangHref + Html[1] + message + Html[2];

    private static async Task<string?> UserRegVariantAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `reg_variant` FROM `users` WHERE `user_id` = ?");
        ErpDb.AddParameters(command, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? StorefrontProfileForm.PdoText(reader, 0) : null;
    }

    private static async Task<List<(string Name, string? ShowFor)>> RegFieldsAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var fields = new List<(string Name, string? ShowFor)>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT `name`, `show_for` FROM `reg_fields` WHERE `main_flag` = 0";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            fields.Add((StorefrontProfileForm.PdoText(reader, 0) ?? string.Empty, StorefrontProfileForm.PdoText(reader, 1)));
        }

        return fields;
    }

    private static async Task<List<string>> ProfileKeysAsync(DbConnection connection, DbTransaction transaction, long userId, CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `data_key` FROM `users_profiles` WHERE `user_id` = ?");
        ErpDb.AddParameters(command, userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            keys.Add(StorefrontProfileForm.PdoText(reader, 0) ?? string.Empty);
        }

        return keys;
    }

    /// <summary><c>array_search($variant, json_decode($showFor, true)) !== false</c>; a list that does not decode to an array shows nothing.</summary>
    public static bool ShownFor(string? showFor, string? variant)
    {
        if (string.IsNullOrEmpty(showFor))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(showFor);
            var root = document.RootElement;
            var items = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray().ToList(),
                JsonValueKind.Object => root.EnumerateObject().Select(p => p.Value).ToList(),
                _ => [],
            };
            return items.Any(item => LooseEquals(variant, item));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>PHP 8 <c>$a == $b</c> for two strings (or null) from the database and the form.</summary>
    public static bool LooseEquals(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return (a ?? string.Empty) == (b ?? string.Empty);
        }

        return NumericString(a, out var x) && NumericString(b, out var y) ? x == y : string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>PHP 8 <c>$posted == $item</c> for a posted string (or null) and a decoded JSON value.</summary>
    public static bool LooseEquals(string? posted, JsonElement item)
    {
        switch (item.ValueKind)
        {
            case JsonValueKind.Null:
                return string.IsNullOrEmpty(posted);
            case JsonValueKind.True:
            case JsonValueKind.False:
                return (posted is not null && posted != "0" && posted.Length > 0) == (item.ValueKind == JsonValueKind.True);
            case JsonValueKind.String:
                return posted is null ? item.GetString()!.Length == 0 : LooseEquals(posted, item.GetString());
            case JsonValueKind.Number:
                var number = item.GetDouble();
                if (posted is null)
                {
                    return number == 0;
                }

                if (NumericString(posted, out var value))
                {
                    return value == number;
                }

                return string.Equals(posted, PhpNumberText(item), StringComparison.Ordinal);
            case JsonValueKind.Array:
                return posted is null && item.GetArrayLength() == 0;
            default:
                return false;
        }
    }

    private static string PhpNumberText(JsonElement item)
    {
        if (item.TryGetInt64(out var integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        var number = item.GetDouble();
        return number == Math.Floor(number) && Math.Abs(number) < 1e15
            ? number.ToString("0", CultureInfo.InvariantCulture)
            : number.ToString("G17", CultureInfo.InvariantCulture);
    }

    /// <summary>PHP 8 <c>is_numeric()</c> for a string: surrounding whitespace, a sign, digits with a dot and an exponent.</summary>
    private static bool NumericString(string value, out double number)
    {
        number = 0;
        var text = value.Trim(' ', '\t', '\n', '\r', '\v', '\f');
        if (text.Length == 0)
        {
            return false;
        }

        var i = 0;
        if (text[i] is '+' or '-')
        {
            i++;
        }

        var digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
            digits++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsAsciiDigit(text[i]))
            {
                i++;
                digits++;
            }
        }

        if (digits == 0)
        {
            return false;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var j = i + 1;
            if (j < text.Length && text[j] is '+' or '-')
            {
                j++;
            }

            var exponent = 0;
            while (j < text.Length && char.IsAsciiDigit(text[j]))
            {
                j++;
                exponent++;
            }

            if (exponent > 0)
            {
                i = j;
            }
        }

        return i == text.Length && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
