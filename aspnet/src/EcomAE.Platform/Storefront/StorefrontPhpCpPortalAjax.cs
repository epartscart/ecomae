using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string PortalDeployOnlyOnPlatform = "Deploy is only available on the ecomae platform control panel.";
    public const string PortalDeployStaysClassic = "Site deploy stays Classic.";
    public const string PortalSeedStaysClassic = "Storefront seed stays on the classic helper.";
    public const string PortalMenuStaysClassic = "Menu item list stays on the classic helper.";
    public const string PortalPasswordStaysClassic = "Password reset stays Classic.";
    public const string PortalRevealStaysClassic = "Password reveal stays Classic.";
    public const string PortalDemoStaysClassic = "Demo access stays Classic.";
    public const string PortalPushStaysClassic = "Client host push stays Classic.";

    public sealed record PortalActiveBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("is_active")] int IsActive);

    public static async Task<object> PortalAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        string? requestHost,
        ICpIndustrySettingsWriteService settings,
        ICpTenantsWriteService tenants,
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
        var super = PlatformHostPolicy.IsSuperCpHost(requestHost);
        if (action == "save_settings")
        {
            var written = await settings.SaveAsync(PortalSettingsRequest(fields), requestHost, cancellationToken).ConfigureAwait(false);
            var message = written.Message;
            if (written.Succeeded && super && OmsField(fields, "target_host").Trim().Length > 0)
            {
                message = message + " " + PortalPushStaysClassic;
            }

            return new FlagBody(written.Succeeded, message);
        }

        if (action == "tenant_set_active")
        {
            if (!super)
            {
                return new CodedJson(403, new FlagBody(false, IntegrationsSuperOnly));
            }

            var active = PortalFilled(fields, "active");
            var written = await tenants.SetActiveAsync(
                new CpTenantsSetActiveRequest(OmsField(fields, "site_key"), active),
                cancellationToken).ConfigureAwait(false);
            return new PortalActiveBody(written.Succeeded, written.Message, written.Succeeded && active ? 1 : 0);
        }

        if (action is "tenant_reset_password" or "tenant_reveal_password" or "tenant_demo_access_load" or "tenant_demo_access_save")
        {
            if (!super)
            {
                return new CodedJson(403, new FlagBody(false, IntegrationsSuperOnly));
            }

            var message = action switch
            {
                "tenant_reveal_password" => PortalRevealStaysClassic,
                "tenant_demo_access_load" or "tenant_demo_access_save" => PortalDemoStaysClassic,
                _ => PortalPasswordStaysClassic,
            };
            return new FlagBody(false, message);
        }

        if (action == "deploy_site")
        {
            return new FlagBody(false, super ? PortalDeployStaysClassic : PortalDeployOnlyOnPlatform);
        }

        if (action == "seed_storefront_data")
        {
            return new FlagBody(false, PortalSeedStaysClassic);
        }

        if (action == "menu_items")
        {
            return new FlagBody(false, PortalMenuStaysClassic);
        }

        return new FlagBody(false, "Unknown action");
    }

    private static CpIndustrySettingsSaveRequest PortalSettingsRequest(IReadOnlyDictionary<string, string> fields)
        => new(
            OmsField(fields, "industry_code"),
            OmsField(fields, "theme_template"),
            OmsField(fields, "storefront_layout"),
            OmsField(fields, "access_mode"),
            OmsField(fields, "cp_default_lang"),
            OmsField(fields, "country_code"),
            OmsField(fields, "system_name"),
            OmsField(fields, "hub_name"),
            OmsField(fields, "tagline"),
            OmsField(fields, "domain_path"),
            OmsField(fields, "contact_trade_name"),
            OmsField(fields, "contact_from_email"),
            OmsField(fields, "contact_admin_email"),
            OmsField(fields, "contact_phone"),
            OmsField(fields, "contact_head_office_address"),
            OmsField(fields, "contact_city"),
            OmsField(fields, "contact_country"),
            PortalPostedValues(fields, "enabled_packs"),
            PortalPostedValues(fields, "erp_modules"),
            PortalPostedIds(fields, "hidden_groups"),
            PortalPostedIds(fields, "hidden_items"));

    private static bool PortalFilled(IReadOnlyDictionary<string, string> fields, string name)
    {
        var value = OmsField(fields, name);
        return value.Length > 0 && value != "0";
    }

    private static List<string> PortalPostedValues(IReadOnlyDictionary<string, string> fields, string name)
    {
        var values = new List<string>();
        if (fields.TryGetValue(name, out var single) && single.Length > 0)
        {
            values.Add(single);
        }

        var prefix = name + "[";
        foreach (var pair in fields)
        {
            if (pair.Key.StartsWith(prefix, StringComparison.Ordinal) && pair.Key.EndsWith(']') && pair.Value.Length > 0)
            {
                values.Add(pair.Value);
            }
        }

        return values;
    }

    private static List<int> PortalPostedIds(IReadOnlyDictionary<string, string> fields, string name)
    {
        var ids = new List<int>();
        foreach (var value in PortalPostedValues(fields, name))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
