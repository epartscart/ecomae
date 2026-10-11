namespace EcomAE.Platform;

/// <summary>
/// PHP on-prem license helpers. Live activation stays on
/// <c>/erp/on-premises/license-activate-dry-run</c> and
/// <c>/erp/on-premises/licenses</c>.
/// </summary>
public static class OnPremisesLicensePages
{
    public const string LicenseManagerPath = "deploy/on-premises/epc_license_manager.php";

    public const string epc_license_valid = nameof(epc_license_valid);
    public const string epc_license_module_allowed = nameof(epc_license_module_allowed);
    public const string epc_license_info = nameof(epc_license_info);
}
