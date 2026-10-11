namespace EcomAE.Platform.Storefront;

/// <summary>
/// Customer account includes from <c>content/users</c>. Login, OTP and register
/// writes stay on their existing endpoints. <c>DP_User</c> session reads stay on
/// <c>ILegacySessionValidator</c>.
/// </summary>
public static class StorefrontUserPages
{
    public const string DpUserPath = "content/users/dp_user.php";
    public const string RegistrationEnhancedPath = "content/users/epc_registration_enhanced.php";
    public const string SessionSecurityPath = "content/users/epc_session_security.php";
    public const string UsersFunctionsPath = "content/users/users_functions.php";
    public const string ProfileFormPath = "content/users/profileform.php";
    public const string LoginFormPath = "content/users/loginform.php";

    public const string epc_reg_auth_available = nameof(epc_reg_auth_available);
    public const string epc_reg_country_options = nameof(epc_reg_country_options);
    public const string epc_reg_h = nameof(epc_reg_h);
    public const string epc_reg_profile_set = nameof(epc_reg_profile_set);
    public const string epc_reg_uae_emirates = nameof(epc_reg_uae_emirates);
    public const string epc_reg_validate_trade_uae = nameof(epc_reg_validate_trade_uae);
    public const string epc_session_destroy_safe = nameof(epc_session_destroy_safe);
    public const string epc_session_harden_ini = nameof(epc_session_harden_ini);
    public const string epc_session_metadata = nameof(epc_session_metadata);
    public const string epc_session_regenerate = nameof(epc_session_regenerate);
    public const string epc_session_validate = nameof(epc_session_validate);
}
