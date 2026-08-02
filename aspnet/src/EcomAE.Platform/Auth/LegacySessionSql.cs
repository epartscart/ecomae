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
}
