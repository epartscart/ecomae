using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>content/shop/finance/epc_erp_access.php</c>: who may open the ERP and its documents without (or with) a CP admin
/// session. The <c>DP_User</c> checks it relies on read the same <c>session</c> / <c>u_id</c> and <c>admin_session</c> /
/// <c>admin_u_id</c> cookies against <c>sessions</c>.
/// </summary>
public static class ErpUserAccess
{
    public const string TeamGroupValue = "EPC_ERP_TEAM";
    public const string CpErpContentUrl = "shop/finance/erp";

    public sealed record Cookies(string? Session, string? UserId, string? AdminSession, string? AdminUserId);

    /// <summary>
    /// The gate of PHP <c>content/shop/document_control/service/print.php</c>: <c>DP_User::isAdmin() || DP_User::isBackendGroup()</c>,
    /// else <c>epc_erp_user_can_access()</c>, where any database error denies.
    /// </summary>
    public static async Task<bool> CanPrintDocumentsAsync(DbConnection connection, Cookies cookies, CancellationToken cancellationToken)
    {
        if (await AdminIdAsync(connection, cookies, cancellationToken).ConfigureAwait(false) > 0
            || await IsBackendGroupAsync(connection, await UserIdAsync(connection, cookies, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        try
        {
            return await CanAccessAsync(connection, cookies, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return false;
        }
    }

    /// <summary>PHP <c>epc_erp_user_can_access()</c>.</summary>
    public static async Task<bool> CanAccessAsync(DbConnection connection, Cookies cookies, CancellationToken cancellationToken)
    {
        var adminId = await AdminIdAsync(connection, cookies, cancellationToken).ConfigureAwait(false);
        if (adminId > 0 && await HasContentAccessAsync(connection, CpErpContentUrl, false, adminId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        var userId = await UserIdAsync(connection, cookies, cancellationToken).ConfigureAwait(false);
        if (userId <= 0)
        {
            return false;
        }

        return await IsBackendGroupAsync(connection, userId, cancellationToken).ConfigureAwait(false)
            || await InBackendTreeAsync(connection, userId, cancellationToken).ConfigureAwait(false)
            || await InAdministratorGroupAsync(connection, userId, cancellationToken).ConfigureAwait(false)
            || await HasCpErpAccessAsync(connection, userId, cancellationToken).ConfigureAwait(false)
            || (await DepartmentCodesAsync(connection, userId, cancellationToken).ConfigureAwait(false)).Count > 0
            || await InTeamAsync(connection, userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>DP_User::getUserId()</c>: the <c>u_id</c> cookie when exactly one <c>sessions</c> row matches it and <c>session</c>.</summary>
    public static Task<int> UserIdAsync(DbConnection connection, Cookies cookies, CancellationToken cancellationToken)
        => SessionUserAsync(connection, cookies.Session, cookies.UserId, false, cancellationToken);

    /// <summary>PHP <c>DP_User::getAdminId()</c>: as <see cref="UserIdAsync"/> for the admin cookies and a <c>type = 1</c> row.</summary>
    public static Task<int> AdminIdAsync(DbConnection connection, Cookies cookies, CancellationToken cancellationToken)
        => SessionUserAsync(connection, cookies.AdminSession, cookies.AdminUserId, true, cancellationToken);

    /// <summary>PHP <c>DP_User::isBackendGroup()</c>: the first <c>for_backend</c> group or one of its direct children.</summary>
    public static async Task<bool> IsBackendGroupAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return false;
        }

        var root = await ErpDb.LongAsync(connection, null, "SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false);
        if (root <= 0)
        {
            return false;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = ? AND (`group_id` = ? OR `group_id` IN (SELECT `id` FROM `groups` WHERE `parent` = ?))"),
            cancellationToken,
            userId,
            root,
            root).ConfigureAwait(false) > 0;
    }

    /// <summary>PHP <c>epc_erp_backend_group_ids()</c>: the first <c>for_backend</c> group and its whole subtree, else groups 1 and 3.</summary>
    public static async Task<IReadOnlyList<long>> BackendGroupIdsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var root = await ErpDb.LongAsync(connection, null, "SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false);
        if (root <= 0)
        {
            return [1, 3];
        }

        var ids = new List<long>();
        await CollectSubtreeAsync(connection, root, ids, true, cancellationToken).ConfigureAwait(false);
        return ids;
    }

    /// <summary>PHP <c>epc_erp_user_in_backend_tree()</c>.</summary>
    public static async Task<bool> InBackendTreeAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return false;
        }

        var groups = await BackendGroupIdsAsync(connection, cancellationToken).ConfigureAwait(false);
        return groups.Count > 0 && await BoundToAnyAsync(connection, userId, groups, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_erp_user_in_administrator_group()</c>: a group whose value contains “Администратор” or “Administrator”.</summary>
    public static async Task<bool> InAdministratorGroupAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return false;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `users_groups_bind` ugb INNER JOIN `groups` g ON g.`id` = ugb.`group_id` WHERE ugb.`user_id` = ? AND (g.`value` LIKE ? OR g.`value` LIKE ?)"),
            cancellationToken,
            userId,
            "%Администратор%",
            "%Administrator%").ConfigureAwait(false) > 0;
    }

    /// <summary>PHP <c>epc_erp_user_has_cp_erp_access()</c>: no access list on the CP ERP page lets every signed-in user in.</summary>
    public static async Task<bool> HasCpErpAccessAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return false;
        }

        var allowed = await AllowedGroupsForContentAsync(connection, CpErpContentUrl, false, cancellationToken).ConfigureAwait(false);
        return allowed.Count == 0 || (await UserGroupsAsync(connection, userId, cancellationToken).ConfigureAwait(false)).Any(allowed.Contains);
    }

    /// <summary>
    /// PHP <c>epc_erp_staff_user_department_codes()</c>: the departments whose <c>EPC_ERP_DEPT_*</c> group the user is in, plus the
    /// active staff profile department. When no department group exists the profile is not read.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DepartmentCodesAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return [];
        }

        var groups = new Dictionary<long, string>();
        foreach (var department in ErpStaffDepartmentCatalog.All)
        {
            var id = await GroupIdByValueAsync(connection, "EPC_ERP_DEPT_" + department.Code.ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
            if (id > 0)
            {
                groups[id] = department.Code;
            }
        }

        if (groups.Count == 0)
        {
            return [];
        }

        var codes = new List<string>();
        foreach (var groupId in await UserGroupsAsync(connection, userId, cancellationToken).ConfigureAwait(false))
        {
            if (groups.TryGetValue(groupId, out var code))
            {
                codes.Add(code);
            }
        }

        var profile = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `department_code` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            userId).ConfigureAwait(false) ?? string.Empty;
        if (profile.Length > 0 && !codes.Contains(profile, StringComparer.Ordinal))
        {
            codes.Add(profile);
        }

        return codes;
    }

    /// <summary>PHP <c>epc_erp_user_in_team()</c>: bound to the <c>EPC_ERP_TEAM</c> group.</summary>
    public static async Task<bool> InTeamAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return false;
        }

        var team = await GroupIdByValueAsync(connection, TeamGroupValue, cancellationToken).ConfigureAwait(false);
        return team > 0 && await BoundToAnyAsync(connection, userId, [team], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_erp_has_content_access()</c> for the admin profile (<paramref name="frontend"/> false) or the user profile.</summary>
    public static async Task<bool> HasContentAccessAsync(DbConnection connection, string contentUrl, bool frontend, int profileUserId, CancellationToken cancellationToken)
    {
        var allowed = await AllowedGroupsForContentAsync(connection, contentUrl, frontend, cancellationToken).ConfigureAwait(false);
        if (allowed.Count == 0)
        {
            return true;
        }

        var groups = profileUserId > 0
            ? await UserGroupsAsync(connection, profileUserId, cancellationToken).ConfigureAwait(false)
            : frontend ? await GuestGroupsAsync(connection, cancellationToken).ConfigureAwait(false) : [];
        return groups.Any(allowed.Contains);
    }

    /// <summary>PHP <c>epc_erp_allowed_groups_for_content()</c>: the <c>content_access</c> groups of the page and every group below them.</summary>
    public static async Task<IReadOnlyList<long>> AllowedGroupsForContentAsync(DbConnection connection, string contentUrl, bool frontend, CancellationToken cancellationToken)
    {
        var allowed = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `group_id` FROM `content_access` WHERE `content_id` = (SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = ? LIMIT 1)");
            ErpDb.AddParameters(command, [contentUrl, frontend ? 1 : 0]);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                allowed.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        if (allowed.Count == 0)
        {
            return allowed;
        }

        var result = new List<long>(allowed);
        foreach (var groupId in allowed)
        {
            await CollectSubtreeAsync(connection, groupId, result, false, cancellationToken).ConfigureAwait(false);
        }

        return result.Distinct().ToList();
    }

    private static async Task CollectSubtreeAsync(DbConnection connection, long parentId, List<long> ids, bool includeParent, CancellationToken cancellationToken)
    {
        if (includeParent && !ids.Contains(parentId))
        {
            ids.Add(parentId);
        }

        var children = new List<(long Id, long Count)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `id`, `count` FROM `groups` WHERE `parent` = ?");
            ErpDb.AddParameters(command, [parentId]);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                children.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture)));
            }
        }

        foreach (var (id, count) in children)
        {
            if (!ids.Contains(id))
            {
                ids.Add(id);
            }

            if (count > 0)
            {
                await CollectSubtreeAsync(connection, id, ids, false, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<int> SessionUserAsync(DbConnection connection, string? session, string? userCookie, bool admin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(session) || !int.TryParse(userCookie, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            return 0;
        }

        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ?" + (admin ? " AND `type` = 1" : string.Empty) + " AND `user_id` = ?"),
            cancellationToken,
            session,
            userId).ConfigureAwait(false);
        return count == 1 ? userId : 0;
    }

    private static async Task<long> GroupIdByValueAsync(DbConnection connection, string value, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1"), cancellationToken, value).ConfigureAwait(false);

    private static async Task<List<long>> UserGroupsAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        var groups = new List<long>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?");
        ErpDb.AddParameters(command, [userId]);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            groups.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return groups;
    }

    private static async Task<List<long>> GuestGroupsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var guest = await ErpDb.LongAsync(connection, null, "SELECT `id` FROM `groups` WHERE `for_guests` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false);
        return guest > 0 ? [guest] : [];
    }

    private static async Task<bool> BoundToAnyAsync(DbConnection connection, int userId, IReadOnlyList<long> groups, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            "SELECT COUNT(*) FROM `users_groups_bind` WHERE `user_id` = " + userId.ToString(CultureInfo.InvariantCulture)
                + " AND `group_id` IN (" + string.Join(",", groups.Select(g => g.ToString(CultureInfo.InvariantCulture))) + ")",
            cancellationToken).ConfigureAwait(false) > 0;
}
