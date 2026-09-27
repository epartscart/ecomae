using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Binds the PHP payment-hub forms: the configure tab posts one field per gateway parameter
/// (PHP builds the same object in JS before <c>save_config</c>), the accounts tab posts the
/// <c>epc_pay_accounts_save</c> field set with the owner id split across office / vendor selects.
/// </summary>
public static class CpPaymentsFormBinder
{
    /// <summary>Prefix of the dynamic credential widgets rendered from <c>shop_payment_systems.parameters</c>.</summary>
    public const string ParameterPrefix = "param_";

    /// <summary>Collects <c>param_*</c> fields into the JSON object PHP stores in <c>parameters_values</c>.</summary>
    public static string ParametersValues(IFormCollection form)
    {
        var raw = form["parameters_values"].ToString();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in form)
        {
            if (!field.Key.StartsWith(ParameterPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var name = field.Key[ParameterPrefix.Length..];
            if (name.Length == 0)
            {
                continue;
            }

            values[name] = field.Value.ToString();
        }

        return JsonSerializer.Serialize(values);
    }

    public static CpPaymentAccountInput Account(IFormCollection form)
    {
        var ownerType = CpPaymentsWriteService.NormaliseOwnerType(form["owner_type"].ToString());
        var ownerId = ownerType switch
        {
            "office" => Long(form, "office_id"),
            "vendor" => Long(form, "vendor_id"),
            _ => 0L
        };
        if (ownerId == 0)
        {
            ownerId = Long(form, "owner_id");
        }

        return new CpPaymentAccountInput(
            Long(form, "id"),
            ownerType,
            ownerId,
            form["title"].ToString(),
            form["handler"].ToString(),
            form["mode"].ToString(),
            string.IsNullOrWhiteSpace(form["credentials_json"].ToString()) ? "{}" : form["credentials_json"].ToString(),
            form["connected_account_id"].ToString(),
            form["payout_iban"].ToString(),
            form["payout_bank"].ToString(),
            form["payout_name"].ToString(),
            Dec(form, "platform_fee_pct"),
            form["status"].ToString(),
            Flag(form, "demo_mode"),
            Flag(form, "is_default"));
    }

    private static long Long(IFormCollection form, string name)
        => long.TryParse(form[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0L;

    private static decimal Dec(IFormCollection form, string name)
        => decimal.TryParse(form[name].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;

    private static bool Flag(IFormCollection form, string name)
        => form[name].ToString().Trim() is "1" or "true" or "True" or "on" or "yes";
}
