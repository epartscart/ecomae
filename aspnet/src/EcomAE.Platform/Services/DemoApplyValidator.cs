using System.Net.Mail;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Services;

/// <summary>
/// Field validation for the public Layla demo wizard. Messages match
/// <c>epc_portal_demo_provision()</c>. This validator never provisions a tenant —
/// live apply stays on PHP <c>epc-demo-provision-public.php</c> (CloudPanel + platform DB).
/// </summary>
public static class DemoApplyValidator
{
    public const string UnavailableMessage = "Platform database unavailable";
    public const string PostRequiredMessage = "POST required";

    public static readonly string[] IndustryPresets = ["auto_parts", "fashion", "erp_only"];

    public static DemoApplyRequest FromForm(IFormCollection form)
    {
        var termsRaw = First(form, "terms", "accept_terms");
        var terms = !string.IsNullOrWhiteSpace(termsRaw)
            && !termsRaw.Equals("0", StringComparison.Ordinal)
            && !termsRaw.Equals("false", StringComparison.OrdinalIgnoreCase);

        return new DemoApplyRequest(
            ContactName: First(form, "contact_name", "name"),
            ContactEmail: First(form, "contact_email", "email"),
            ContactPhone: First(form, "contact_phone", "phone"),
            Company: First(form, "company"),
            CountryCode: First(form, "country_code", "country"),
            IndustryCode: First(form, "industry_code", "industry"),
            Terms: terms);
    }

    /// <summary>PHP-identical field checks. <see cref="DemoApplyResult.Ok"/> is true only when fields pass.</summary>
    public static DemoApplyResult ValidateFields(DemoApplyRequest request)
    {
        var name = (request.ContactName ?? "").Trim();
        var email = (request.ContactEmail ?? "").Trim().ToLowerInvariant();
        var phone = request.ContactPhone ?? "";
        var country = NormalizeCountry(request.CountryCode);
        var industry = NormalizeIndustry(request.IndustryCode);

        if (name.Length == 0)
        {
            return Fail("Name is required");
        }

        if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
        {
            return Fail("Valid email is required");
        }

        if (!PhoneValid(phone))
        {
            return Fail("Valid phone number is required (7–15 digits)");
        }

        if (country.Length != 2)
        {
            return Fail("Please select your country");
        }

        if (!request.Terms)
        {
            return Fail("You must accept the demo terms");
        }

        if (industry.Length == 0)
        {
            return Fail("Select an industry (auto_parts, fashion, or erp_only)");
        }

        if (industry is not ("auto_parts" or "fashion" or "erp_only"))
        {
            return Fail("Industry not available — choose auto parts, fashion, or ERP only");
        }

        return new DemoApplyResult(true, "ok", 200);
    }

    public static DemoApplyResult Unavailable()
        => new(false, UnavailableMessage, StatusCodes.Status503ServiceUnavailable);

    public static DemoApplyResult MethodNotAllowed()
        => new(false, PostRequiredMessage, StatusCodes.Status405MethodNotAllowed);

    public static string NormalizeCountry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var letters = Regex.Replace(raw, "[^A-Za-z]", "");
        return letters.Length == 0 ? "" : letters[..Math.Min(2, letters.Length)].ToUpperInvariant();
    }

    public static string NormalizeIndustry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var cleaned = Regex.Replace(raw.Trim().ToLowerInvariant(), "[^a-z0-9_]", "");
        return cleaned == "erp_standalone" ? "erp_only" : cleaned;
    }

    public static bool PhoneValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        var digits = Regex.Replace(phone, @"\D+", "");
        return digits.Length is >= 7 and <= 15;
    }

    private static DemoApplyResult Fail(string message)
        => new(false, message, StatusCodes.Status400BadRequest);

    private static string First(IFormCollection form, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (form.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.ToString();
            }
        }

        return "";
    }
}

public sealed record DemoApplyRequest(
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    string Company,
    string CountryCode,
    string IndustryCode,
    bool Terms);

public sealed record DemoApplyResult(bool Ok, string Message, int StatusCode);
