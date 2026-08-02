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
}
