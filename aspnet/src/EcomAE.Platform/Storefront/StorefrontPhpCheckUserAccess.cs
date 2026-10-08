using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/users/check_user_access.php</c>: the group must reach every page in <c>$pages_to_check</c>, through
/// <c>content_access</c> plus the groups nested under those (only below a group whose <c>count</c> is not 0). A frontend page without
/// rules is open, a backend page without rules is closed. Backend pages use the admin profile, frontend pages the
/// customer profile (<c>DP_User::getUserProfile()</c> groups).
/// </summary>
public static partial class StorefrontPhpAjax
{
    public sealed record PhpPageToCheck(string Url, int IsFrontend);

    /// <summary>The inputs of PHP <c>multilang_init()</c> for a Control Panel ajax request.</summary>
    public sealed record CpLangRequest(string? LangCpCookie, string? BackendUiLang, bool Multilang)
    {
        public static CpLangRequest Default { get; } = new(null, null, false);

        public static CpLangRequest FromConfig(IReadOnlyDictionary<string, string> config, string? langCpCookie)
            => new(
                langCpCookie,
                config.TryGetValue("backend_ui_lang", out var forced) ? forced : null,
                config.TryGetValue("multilang", out var multilang) && ShopPayForOrderService.PhpTruthy(multilang));
    }

    /// <summary>
    /// PHP <c>multilang_init()</c> in backend mode: the active <c>backend_ui_lang</c>, else the active <c>lang_cp</c>
    /// cookie, else the active default language. With multilang off, only the active default language.
    /// </summary>
    public static async Task<string?> PhpBackendLangAsync(DbConnection connection, CpLangRequest request, CancellationToken cancellationToken)
    {
        async Task<bool> ActiveAsync(string? code)
            => code is not null
               && await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ? AND `lang_code` = ?"), cancellationToken, 1, code)
                   .ConfigureAwait(false) != 0;

        Task<string?> DefaultAsync()
            => ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `lang_code` FROM `lang_languages` WHERE `active` = ? AND `is_default` = ?"), cancellationToken, 1, 1);

        if (!request.Multilang)
        {
            return await DefaultAsync().ConfigureAwait(false);
        }

        var forced = (request.BackendUiLang ?? string.Empty).Trim();
        if (forced.Length > 0 && await ActiveAsync(forced).ConfigureAwait(false))
        {
            return forced;
        }

        return await ActiveAsync(request.LangCpCookie).ConfigureAwait(false)
            ? request.LangCpCookie
            : await DefaultAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Null when access is allowed, otherwise PHP's <c>{"status":false,"error":…,"message":…}</c> with string 2387 (no
    /// pages) or 2388 (denied). <paramref name="adminId"/> is <c>DP_User::getAdminId()</c> (0 when not an admin) and
    /// <paramref name="userId"/> is <c>DP_User::getUserId()</c> (0 for a guest).
    /// </summary>
    public static async Task<LangAccessBody?> CheckUserAccessAsync(
        DbConnection connection,
        IReadOnlyList<PhpPageToCheck> pages,
        long adminId,
        long userId,
        Func<Task<string?>> lang,
        CancellationToken cancellationToken)
    {
        async Task<LangAccessBody> RefuseAsync(int id)
        {
            var text = await new StorefrontPhpTranslator(connection, await lang().ConfigureAwait(false) ?? string.Empty).RawAsync(id.ToString(CultureInfo.InvariantCulture), cancellationToken)
                .ConfigureAwait(false);
            return new LangAccessBody(false, text, text);
        }

        if (pages.Count == 0)
        {
            return await RefuseAsync(2387).ConfigureAwait(false);
        }

        List<long>? adminGroups = adminId == 0 ? null : await GroupIdsAsync(connection, "SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?", cancellationToken, adminId).ConfigureAwait(false);
        List<long>? userGroups = null;

        foreach (var page in pages)
        {
            var allowed = await GroupIdsAsync(
                connection,
                "SELECT `group_id` FROM `content_access` WHERE `content_id` = (SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = ?)",
                cancellationToken,
                page.Url,
                page.IsFrontend).ConfigureAwait(false);
            var inserted = new List<long>();
            foreach (var group in allowed.ToList())
            {
                await InsertedGroupsAsync(connection, group, allowed, inserted, cancellationToken).ConfigureAwait(false);
            }

            allowed.AddRange(inserted);

            var accessAllowed = allowed.Count == 0 && page.IsFrontend == 1;
            List<long>? profile;
            if (page.IsFrontend == 0)
            {
                profile = adminGroups;
            }
            else
            {
                userGroups ??= await UserProfileGroupsAsync(connection, userId, cancellationToken).ConfigureAwait(false);
                profile = userGroups;
            }

            if (!accessAllowed && profile is not null)
            {
                accessAllowed = profile.Any(allowed.Contains);
            }

            if (!accessAllowed)
            {
                return await RefuseAsync(2388).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static async Task InsertedGroupsAsync(DbConnection connection, long group, List<long> allowed, List<long> inserted, CancellationToken cancellationToken)
    {
        var count = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `count` FROM `groups` WHERE `id` = ?"), cancellationToken, group).ConfigureAwait(false);
        if (count is null || (double.TryParse(count, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == 0))
        {
            return;
        }

        foreach (var child in await GroupIdsAsync(connection, "SELECT `id` FROM `groups` WHERE `parent` = ?", cancellationToken, group).ConfigureAwait(false))
        {
            if (inserted.Contains(child) || allowed.Contains(child))
            {
                continue;
            }

            inserted.Add(child);
            await InsertedGroupsAsync(connection, child, allowed, inserted, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The <c>groups</c> of PHP <c>DP_User::getUserProfile()</c>: the guest group, the bound groups, or else the first registered group.</summary>
    internal static async Task<List<long>> UserProfileGroupsAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        if (userId == 0)
        {
            var guest = await GroupIdsAsync(connection, "SELECT `id` FROM `groups` WHERE `for_guests` = ?", cancellationToken, 1).ConfigureAwait(false);
            return [guest.Count > 0 ? guest[0] : 0];
        }

        var groups = await GroupIdsAsync(connection, "SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?", cancellationToken, userId).ConfigureAwait(false);
        if (groups.Count == 0)
        {
            var registered = await GroupIdsAsync(connection, "SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
            groups.AddRange(registered);
        }

        return groups;
    }

    private static async Task<List<long>> GroupIdsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        var ids = new List<long>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.IsDBNull(0) ? 0 : (long)PhpFloatCast(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)));
        }

        return ids;
    }
}
