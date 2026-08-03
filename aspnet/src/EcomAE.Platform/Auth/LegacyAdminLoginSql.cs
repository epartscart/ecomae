namespace EcomAE.Platform.Auth;

public static class LegacyAdminLoginSql
{
    public const string SelectUserByEmail = """
        SELECT `user_id`, `password`, IFNULL(`email`, '') AS email
        FROM `users`
        WHERE `email` = @contact AND `email_confirmed` = 1 AND `unlocked` = 1
        LIMIT 1
        """;

    public const string SelectUserByPhone = """
        SELECT `user_id`, `password`, IFNULL(`email`, '') AS email
        FROM `users`
        WHERE `phone` = @contact AND `phone_confirmed` = 1 AND `unlocked` = 1
        LIMIT 1
        """;

    /// <summary>Mirrors CP plugin INSERT (type=1 admin session).</summary>
    public const string InsertAdminSession = """
        INSERT INTO `sessions`
          (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`)
        VALUES
          (@session, @userId, @time, '', 1, @contactType, @csrf)
        """;

    /// <summary>
    /// Mirrors storefront customer INSERT with <c>last_activiti_time</c>
    /// (<c>epc_auth_create_storefront_session_record</c>); plugin login omits the column but
    /// the common helper sets it for session GC.
    /// </summary>
    public const string InsertCustomerSession = """
        INSERT INTO `sessions`
          (`session`, `user_id`, `time`, `data`, `last_activiti_time`, `csrf_guard_key`)
        VALUES
          (@session, @userId, @time, '', @time, @csrf)
        """;
}
