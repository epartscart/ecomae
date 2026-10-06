using System.Data.Common;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string IntegrationsAdminRequired = "Admin login required";
    public const string IntegrationsSuperOnly = "Super CP only";

    public static async Task<object> IntegrationsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        string? requestHost,
        ICpMobileAppsWriteService mobile,
        ICpTenantFeaturesWriteService features,
        ICpTenantEmailWriteService email,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(
            connection,
            adminSession,
            adminUser,
            new CodedJson(403, new FlagBody(false, IntegrationsAdminRequired)),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action == "save_mobile")
        {
            var written = await mobile.SaveMobileAsync(
                new CpMobileAppsSaveRequest(
                    IntegrationsFilled(fields, "enabled"),
                    OmsField(fields, "app_name"),
                    OmsField(fields, "bundle_id"),
                    OmsField(fields, "deep_link_scheme"),
                    OmsField(fields, "deep_link_domain"),
                    OmsField(fields, "api_base_url"),
                    OmsField(fields, "play_store_url"),
                    OmsField(fields, "app_store_url"),
                    IntegrationsFilled(fields, "pwa_enabled"),
                    OmsField(fields, "firebase_project_id"),
                    IntegrationsFilled(fields, "push_enabled")),
                requestHost,
                cancellationToken).ConfigureAwait(false);
            return new FlagBody(written.Succeeded, written.Message);
        }

        if (action == "save_feature_flags")
        {
            if (!PlatformHostPolicy.IsSuperCpHost(requestHost))
            {
                return new FlagBody(false, IntegrationsSuperOnly);
            }

            var written = await features.SaveFlagsAsync(
                new CpTenantFeaturesSaveRequest(OmsField(fields, "site_key"), IntegrationsPostedFeatures(fields)),
                cancellationToken).ConfigureAwait(false);
            return new FlagBody(written.Succeeded, written.Message);
        }

        if (action == "save_tenant_smtp")
        {
            var written = await email.SaveAsync(
                new CpTenantEmailSaveRequest(
                    IntegrationsFilled(fields, "use_tenant_smtp"),
                    OmsField(fields, "smtp_host"),
                    OmsField(fields, "smtp_port"),
                    OmsField(fields, "smtp_encryption"),
                    OmsField(fields, "smtp_username"),
                    OmsField(fields, "smtp_password"),
                    OmsField(fields, "from_name"),
                    OmsField(fields, "from_email")),
                cancellationToken).ConfigureAwait(false);
            return new FlagBody(written.Succeeded, written.Message);
        }

        if (action == "test_tenant_smtp")
        {
            var to = OmsField(fields, "test_to").Trim();
            if (to.Length == 0 || !CpTenantEmailWriteService.IsEmail(to))
            {
                return new FlagBody(false, "Valid test email required");
            }

            var written = await email.SendTestAsync(to, cancellationToken).ConfigureAwait(false);
            return new FlagBody(written.Succeeded, written.Message);
        }

        return new FlagBody(false, "Unknown action");
    }

    private static bool IntegrationsFilled(IReadOnlyDictionary<string, string> fields, string name)
    {
        var value = OmsField(fields, name);
        return value.Length > 0 && value != "0";
    }

    private static Dictionary<string, bool> IntegrationsPostedFeatures(IReadOnlyDictionary<string, string> fields)
    {
        var posted = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var pair in fields)
        {
            const string prefix = "features[";
            if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal) || pair.Key.Length < prefix.Length + 2 || pair.Key[^1] != ']')
            {
                continue;
            }

            var key = pair.Key[prefix.Length..^1];
            posted[key] = pair.Value.Length > 0 && pair.Value != "0";
        }

        return posted;
    }
}
