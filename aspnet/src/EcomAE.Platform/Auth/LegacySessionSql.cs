namespace EcomAE.Platform.Auth;

public static class LegacySessionSql
{
    public const string SourceTable = "sessions";

    /// <summary>Mirrors PHP CP gate: sessions with type=1 for admin cookies.</summary>
    public const string CountAdminSession = """
        SELECT COUNT(*) FROM `sessions`
        WHERE `session` = @session AND `type` = 1 AND `user_id` = @userId
        """;
}
