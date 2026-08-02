namespace EcomAE.Platform.Auth;

public static class LegacySessionSql
{
    public const string SourceTable = "sessions";

    /// <summary>Mirrors PHP CP gate: sessions with type=1 for admin cookies.</summary>
    public const string CountAdminSession = """
        SELECT COUNT(*) FROM `sessions`
        WHERE `session` = @session AND `type` = 1 AND `user_id` = @userId
        """;

    /// <summary>Mirrors PHP DP_User::getUserId storefront check (no type filter).</summary>
    public const string CountCustomerSession = """
        SELECT COUNT(*) FROM `sessions`
        WHERE `session` = @session AND `user_id` = @userId
        """;

    public const string SelectUserEmail = """
        SELECT `email` FROM `users`
        WHERE `user_id` = @userId
        LIMIT 1
        """;

    public const string SelectUserGroupIds = """
        SELECT `group_id` FROM `users_groups_bind`
        WHERE `user_id` = @userId
        ORDER BY `group_id` ASC
        """;

    public const string SelectBackendGroupIds = """
        SELECT `id` FROM `groups`
        WHERE `for_backend` = 1
        ORDER BY `id` ASC
        """;

    /// <summary>
    /// Modules with no modules_access rows are open to all (PHP access_control rule).
    /// </summary>
    public const string SelectOpenModules = """
        SELECT m.`id`, IFNULL(m.`caption`, '') AS caption
        FROM `modules` m
        WHERE m.`activated` = 1 AND m.`is_prototype` = 0
          AND NOT EXISTS (SELECT 1 FROM `modules_access` ma WHERE ma.`module_id` = m.`id`)
        ORDER BY m.`id` ASC
        LIMIT 500
        """;

    /// <summary>
    /// Explicit modules_access grants for one group. Nested group inheritance is pending.
    /// </summary>
    public const string SelectModuleAccessForGroup = """
        SELECT DISTINCT ma.`module_id`, IFNULL(m.`caption`, '') AS caption
        FROM `modules_access` ma
        LEFT JOIN `modules` m ON m.`id` = ma.`module_id`
        WHERE ma.`group_id` = @groupId
        ORDER BY ma.`module_id` ASC
        """;
}
