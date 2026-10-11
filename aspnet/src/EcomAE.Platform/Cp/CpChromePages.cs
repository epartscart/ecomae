namespace EcomAE.Platform.Cp;

/// <summary>
/// Remaining Control Panel chrome: content records, lang editor, requests,
/// file manager, left menu, logout, SSL check, and 2FA/auth templates.
/// </summary>
public static class CpChromePages
{
    public static readonly string[] Paths =
    [
        "cp/content/content/dp_content_record.php",
        "cp/content/content/get_content_records.php",
        "cp/content/content/structure_dumps/content_structure_dumps.php",
        "cp/content/filemanager/epc_filemanager_config.php",
        "cp/content/lang/page_lang_main.php",
        "cp/content/requests/request.php",
        "cp/epc_cp_auth_gate.php",
        "cp/epc_cp_bootstrap_light.php",
        "cp/epc_cp_fast_tenant.php",
        "cp/epc_portal_industry_switch.php",
        "cp/lib/docpart/get_next_id.php",
        "cp/modules/check_ssl/check_ssl.php",
        "cp/modules/left_cp_menu/catalogue_menu_helper.php",
        "cp/modules/logout/module.php",
        "cp/plugins/2fa/forms/template_attempts_expired.php",
        "cp/plugins/2fa/forms/template_form.php",
        "cp/plugins/authentication/login_form/template.php",
        "epc_cp_mainstream_menu.php",
        "epc_oms_menu_guide_lib.php",
        "epc_static_serve.php",
        "epc_storefront_stub_redirect.php",
        "epc_deploy_auth.php",
    ];
}
