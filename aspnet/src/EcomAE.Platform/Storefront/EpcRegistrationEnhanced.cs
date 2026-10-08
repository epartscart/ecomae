using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>A refusal of the registration form, with PHP's message.</summary>
public sealed class EpcRegistrationException(string message) : Exception(message);

/// <summary>An uploaded registration document: the client file name and its bytes.</summary>
public sealed record EpcRegistrationUpload(string FileName, ReadOnlyMemory<byte> Content);

/// <summary>
/// The validation and save half of the PHP enhanced registration form: the retail and wholesale form
/// fields, the country and TRN rules, the KYC documents, the profile keys, the VAT type and the e-invoice buyer profile.
/// The form is the posted fields; a missing key is a field that was not posted.
/// </summary>
public static class EpcRegistrationEnhanced
{
    public const long MaxKycBytes = 8 * 1024 * 1024;

    private static readonly string[] KycTextKeys =
    [
        "epc_emirates_id_no", "epc_authorized_signatory", "epc_authorized_signatory_id", "epc_ubo_name", "epc_pep_declaration",
        "epc_source_of_funds", "epc_passport_no", "epc_nationality", "epc_sanctions_declaration",
    ];

    private static readonly string[] KycDocumentKeys =
    [
        "epc_doc_trade_licence", "epc_doc_emirates_id", "epc_doc_vat_certificate", "epc_ubo_id_document", "epc_doc_passport",
        "epc_doc_power_of_attorney", "epc_doc_moa",
    ];

    private static readonly (string Key, string Label)[] RequiredDocuments = [("epc_doc_trade_licence", "trade licence scan"), ("epc_doc_emirates_id", "Emirates ID copy")];

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["first_name"] = "First name", ["last_name"] = "Last name", ["mobile"] = "Mobile phone",
        ["city"] = "City", ["address"] = "Delivery address", ["company"] = "Company name",
        ["legal_name"] = "Legal entity name", ["job_title"] = "Contact job title",
        ["business_type"] = "Business type", ["trade_licence"] = "Trade licence no.",
    };

    private static string Trim(string? value) => EpcEinvoiceBuyer.PhpTrim(value);

    private static string Upper(string value) => EpcEinvoiceBuyer.AsciiUpper(value);

    private static string Lower(string value) => string.Create(value.Length, value, (span, source) =>
    {
        for (var i = 0; i < source.Length; i++)
        {
            span[i] = source[i] is >= 'A' and <= 'Z' ? (char)(source[i] + 32) : source[i];
        }
    });

    private static string Digits(string? value) => Regex.Replace(value ?? string.Empty, "\\D", string.Empty);

    private static string? Get(IReadOnlyDictionary<string, string> post, string key) => post.TryGetValue(key, out var value) ? value : null;

    private static bool NotEmpty(IReadOnlyDictionary<string, string> post, string key) => !EpcEinvoiceBuyer.PhpEmpty(Get(post, key));

    /// <summary>PHP <c>epc_reg_customer_type()</c>: wholesale, else retail.</summary>
    public static string CustomerType(IReadOnlyDictionary<string, string> post)
    {
        var type = Lower(Trim(Get(post, "epc_customer_type") ?? "retail"));
        return type is "retail" or "wholesale" ? type : "retail";
    }

    /// <summary>PHP <c>epc_reg_country_code()</c>: two upper-case letters, else empty.</summary>
    public static string CountryCode(IReadOnlyDictionary<string, string> post)
    {
        var code = Upper(Trim(Get(post, "epc_reg_country")));
        return Regex.IsMatch(code, "^[A-Z]{2}$") ? code : string.Empty;
    }

    /// <summary>PHP <c>epc_reg_trn_mode()</c>: has_trn, not_available, or empty.</summary>
    public static string TrnMode(IReadOnlyDictionary<string, string> post)
    {
        var mode = Lower(Trim(Get(post, "epc_reg_trn_mode")));
        return mode is "has_trn" or "not_available" ? mode : string.Empty;
    }

    /// <summary>PHP <c>epc_reg_uae_requested()</c>.</summary>
    public static bool UaeRequested(IReadOnlyDictionary<string, string> post)
        => CountryCode(post) == "AE" ? ExtractTrn(post).Length > 0 : NotEmpty(post, "epc_uae_company");

    /// <summary>PHP <c>epc_reg_extract_trn()</c>: the UAE TRN digits, or the optional TRN text abroad.</summary>
    public static string ExtractTrn(IReadOnlyDictionary<string, string> post)
    {
        if (CountryCode(post) == "AE")
        {
            return Digits(Get(post, "epc_wholesale_trn") ?? Get(post, "epc_uae_trn") ?? string.Empty);
        }

        return TrnMode(post) == "has_trn" ? Trim(Get(post, "epc_wholesale_trn_optional")) : string.Empty;
    }

    /// <summary>
    /// PHP <c>epc_reg_validate_enhanced_fields()</c>: the required fields of the chosen tab, the country, the TRN rule, the
    /// PEP declaration and the two required wholesale documents. Throws <see cref="EpcRegistrationException"/> with PHP's message.
    /// </summary>
    public static void ValidateEnhancedFields(IReadOnlyDictionary<string, string> post, string customerType, IReadOnlyDictionary<string, EpcRegistrationUpload> files)
    {
        var merged = new Dictionary<string, string>(post, StringComparer.Ordinal) { ["epc_customer_type"] = customerType };
        customerType = CustomerType(merged);
        var prefix = customerType == "wholesale" ? "epc_wholesale_" : "epc_retail_";
        string[] required = customerType == "wholesale"
            ? ["company", "legal_name", "first_name", "last_name", "job_title", "mobile", "city", "address", "business_type", "trade_licence"]
            : ["first_name", "last_name", "mobile", "city", "address"];
        foreach (var key in required)
        {
            if (Trim(Get(post, prefix + key)).Length == 0)
            {
                throw new EpcRegistrationException("Please fill in: " + Labels.GetValueOrDefault(key, key));
            }
        }

        var country = Upper(Trim(Get(post, prefix + "country") ?? Get(post, "epc_reg_country") ?? string.Empty));
        if (country.Length == 0 || !EpcCountries.RegistrationOptions.Any(c => c.Key == country))
        {
            throw new EpcRegistrationException("Please select your country.");
        }

        if (customerType != "wholesale")
        {
            return;
        }

        if (country == "AE")
        {
            if (Digits(Get(post, "epc_wholesale_trn")).Length != 15)
            {
                throw new EpcRegistrationException("UAE TRN must be 15 digits.");
            }
        }
        else
        {
            var mode = TrnMode(post);
            if (mode.Length == 0)
            {
                throw new EpcRegistrationException("Please choose TRN status: enter TRN or Not available.");
            }

            if (mode == "has_trn" && Trim(Get(post, "epc_wholesale_trn_optional")).Length == 0)
            {
                throw new EpcRegistrationException("Please enter your TRN / VAT number.");
            }
        }

        if (Trim(Get(post, "epc_pep_declaration")).Length == 0)
        {
            throw new EpcRegistrationException("Please complete the PEP declaration.");
        }

        foreach (var (fileKey, label) in RequiredDocuments)
        {
            if (!files.ContainsKey(fileKey))
            {
                throw new EpcRegistrationException("Please upload your " + label + ".");
            }
        }
    }

    /// <summary>PHP <c>epc_reg_validate_uae_fields()</c>: the UAE company fields of a wholesale customer in the UAE.</summary>
    public static void ValidateUaeFields(IReadOnlyDictionary<string, string> post)
    {
        if (CountryCode(post) != "AE" || CustomerType(post) != "wholesale")
        {
            return;
        }

        var buyerName = Trim(Get(post, "epc_wholesale_legal_name") ?? Get(post, "epc_uae_buyer_name") ?? string.Empty);
        var trn = Digits(Get(post, "epc_wholesale_trn") ?? Get(post, "epc_uae_trn") ?? string.Empty);
        var address = Trim(Get(post, "epc_wholesale_address") ?? Get(post, "epc_uae_address_line1") ?? string.Empty);
        var city = Trim(Get(post, "epc_wholesale_city") ?? Get(post, "epc_uae_city") ?? string.Empty);
        if (buyerName.Length == 0 || trn.Length == 0 || address.Length == 0 || city.Length == 0)
        {
            throw new EpcRegistrationException("Complete UAE company fields (legal name, TRN, address, city).");
        }

        if (trn.Length != 15)
        {
            throw new EpcRegistrationException("UAE TRN must be 15 digits.");
        }
    }

    /// <summary>
    /// PHP <c>epc_reg_store_kyc_upload()</c>: the document is written to <c>content/files/kyc/{user}/</c> under the web root
    /// and its public path returned (8 MB at most; PDF, JPG, JPEG, PNG or WEBP).
    /// </summary>
    public static async Task<string> StoreKycUploadAsync(string webRoot, long userId, string fieldKey, EpcRegistrationUpload? file, CancellationToken cancellationToken)
    {
        if (userId <= 0 || file is null)
        {
            return string.Empty;
        }

        if (file.Content.Length > MaxKycBytes)
        {
            throw new EpcRegistrationException("Document too large (max 8 MB): " + fieldKey);
        }

        var baseName = file.FileName[(file.FileName.LastIndexOf('/') + 1)..];
        var dot = baseName.LastIndexOf('.');
        var ext = Lower(dot >= 0 ? baseName[(dot + 1)..] : string.Empty);
        if (ext is not ("pdf" or "jpg" or "jpeg" or "png" or "webp"))
        {
            throw new EpcRegistrationException("Allowed document formats: PDF, JPG, PNG, WEBP.");
        }

        var user = userId.ToString(CultureInfo.InvariantCulture);
        var dir = Path.Combine(webRoot, "content", "files", "kyc", user);
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (IOException)
        {
            throw new EpcRegistrationException("Could not create KYC upload folder.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new EpcRegistrationException("Could not create KYC upload folder.");
        }

        var safe = Regex.Replace(Lower(fieldKey), "[^a-z0-9_]", string.Empty) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "." + ext;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(dir, safe), file.Content.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new EpcRegistrationException("Document upload failed: " + fieldKey);
        }

        return "/content/files/kyc/" + user + "/" + safe;
    }

    /// <summary>
    /// PHP <c>epc_reg_save_enhanced_profile()</c>: the contact, address and trade fields go to <c>users_profiles</c>, wholesale
    /// KYC text and documents are stored, then the VAT type is synced and the e-invoice buyer profile saved.
    /// </summary>
    public static async Task SaveEnhancedProfileAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string webRoot,
        long userId,
        IReadOnlyDictionary<string, string> post,
        IReadOnlyDictionary<string, EpcRegistrationUpload> files,
        string regContact,
        string regContactType,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return;
        }

        Task Set(string key, string value) => EpcCustomerTrade.ProfileSetAsync(connection, transaction, userId, key, value, cancellationToken);

        var type = CustomerType(post);
        var prefix = type == "wholesale" ? "epc_wholesale_" : "epc_retail_";
        var country = Upper(Trim(Get(post, prefix + "country") ?? Get(post, "epc_reg_country") ?? string.Empty));
        var first = Trim(Get(post, prefix + "first_name"));
        var last = Trim(Get(post, prefix + "last_name"));
        var mobile = Trim(Get(post, prefix + "mobile"));
        var city = Trim(Get(post, prefix + "city"));
        var address = Trim(Get(post, prefix + "address"));
        var smsKey = type == "wholesale" ? "epc_wholesale_sms_notify" : "epc_retail_sms_notify";

        await Set("epc_reg_country", country).ConfigureAwait(false);
        await Set("name", first).ConfigureAwait(false);
        await Set("surname", last).ConfigureAwait(false);
        if (mobile.Length > 0)
        {
            await Set("phone", mobile).ConfigureAwait(false);
        }

        await Set("epc_reg_city", city).ConfigureAwait(false);
        await Set("epc_reg_address", address).ConfigureAwait(false);
        await Set("epc_reg_sms_notify", NotEmpty(post, smsKey) ? "1" : "0").ConfigureAwait(false);
        var postal = Trim(Get(post, prefix + "postal"));
        if (postal.Length > 0)
        {
            await Set("epc_reg_postal", postal).ConfigureAwait(false);
        }

        if (country == "AE" && type == "retail")
        {
            var emirate = Trim(Get(post, "epc_retail_emirate"));
            if (emirate.Length > 0)
            {
                await Set("epc_reg_emirate", emirate).ConfigureAwait(false);
                await Set("epc_reg_state", emirate).ConfigureAwait(false);
            }
        }
        else
        {
            var state = Trim(Get(post, "epc_retail_state"));
            if (state.Length > 0)
            {
                await Set("epc_reg_state", state).ConfigureAwait(false);
            }
        }

        if (type == "wholesale")
        {
            await Set("company_name", Trim(Get(post, "epc_wholesale_company"))).ConfigureAwait(false);
            await Set("epc_reg_legal_name", Trim(Get(post, "epc_wholesale_legal_name"))).ConfigureAwait(false);
            await Set("epc_reg_job_title", Trim(Get(post, "epc_wholesale_job_title"))).ConfigureAwait(false);
            await Set("epc_reg_business_type", Trim(Get(post, "epc_wholesale_business_type"))).ConfigureAwait(false);
            await Set("epc_reg_trade_licence", Trim(Get(post, "epc_wholesale_trade_licence"))).ConfigureAwait(false);
            var website = Trim(Get(post, "epc_wholesale_website"));
            if (website.Length > 0)
            {
                await Set("epc_reg_website", website).ConfigureAwait(false);
            }

            await Set("epc_reg_trn_mode", country == "AE" ? "has_trn" : TrnMode(post)).ConfigureAwait(false);
            var trn = ExtractTrn(post);
            if (trn.Length > 0)
            {
                await Set("epc_reg_trn", trn).ConfigureAwait(false);
            }

            if (country == "AE")
            {
                await Set("epc_reg_emirate", Trim(Get(post, "epc_wholesale_emirate") ?? "Dubai")).ConfigureAwait(false);
            }

            await Set("epc_legal_reg_type", "TL").ConfigureAwait(false);

            foreach (var key in KycTextKeys)
            {
                var value = Trim(Get(post, key));
                if (value.Length > 0)
                {
                    await Set(key, value).ConfigureAwait(false);
                }
            }

            foreach (var key in KycDocumentKeys)
            {
                if (!files.TryGetValue(key, out var file))
                {
                    continue;
                }

                var path = await StoreKycUploadAsync(webRoot, userId, key, file, cancellationToken).ConfigureAwait(false);
                if (path.Length > 0)
                {
                    await Set(key, path).ConfigureAwait(false);
                    await Set(key + "_status", "pending_review").ConfigureAwait(false);
                }
            }
        }

        foreach (var (legacyKey, legacyValue) in new[] { ("name", first), ("surname", last), ("company_name", Trim(Get(post, "epc_wholesale_company"))) })
        {
            if (legacyValue.Length > 0 && Get(post, legacyKey) is not null)
            {
                await Set(legacyKey, legacyValue).ConfigureAwait(false);
            }
        }

        await EpcUaeCustomerVat.SyncAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);

        if (country.Length == 0)
        {
            return;
        }

        var buyerName = Trim(first + " " + last);
        if (type == "wholesale")
        {
            buyerName = Trim(Get(post, "epc_wholesale_legal_name") ?? Get(post, "epc_wholesale_company") ?? buyerName);
        }

        var buyerTrn = ExtractTrn(post);
        await EpcEinvoiceBuyer.SaveBuyerProfileAsync(connection, transaction, new EpcEinvoiceBuyerInput
        {
            UserId = userId,
            BuyerName = buyerName.Length > 0 ? buyerName : "Customer #" + userId.ToString(CultureInfo.InvariantCulture),
            Trn = buyerTrn,
            LegalRegNo = type == "wholesale" ? Trim(Get(post, "epc_wholesale_trade_licence") ?? Get(post, "epc_reg_trade_licence") ?? string.Empty) : string.Empty,
            LegalRegType = type == "wholesale" ? "TL" : "EID",
            AddressLine1 = address,
            City = city,
            Emirate = Trim(Get(post, prefix + "emirate") ?? Get(post, "epc_retail_emirate") ?? "Dubai"),
            CountryCode = country,
            Email = regContactType == "email" ? Trim(regContact) : string.Empty,
            Phone = mobile,
            BuyerOnboarded = type == "wholesale" && country == "AE" && Digits(buyerTrn).Length == 15,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PHP <c>epc_reg_save_uae_buyer_profile()</c>: the enhanced profile, then for a UAE wholesale customer with a 15-digit
    /// TRN an onboarded buyer profile and the <c>epc_uae_company</c> flag.
    /// </summary>
    public static async Task SaveUaeBuyerProfileAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string webRoot,
        long userId,
        IReadOnlyDictionary<string, string> post,
        IReadOnlyDictionary<string, EpcRegistrationUpload> files,
        string regContact,
        string regContactType,
        CancellationToken cancellationToken)
    {
        await SaveEnhancedProfileAsync(connection, transaction, webRoot, userId, post, files, regContact, regContactType, cancellationToken).ConfigureAwait(false);
        if (CustomerType(post) != "wholesale" || CountryCode(post) != "AE")
        {
            return;
        }

        var trn = Digits(Get(post, "epc_wholesale_trn"));
        if (trn.Length != 15)
        {
            return;
        }

        var emirate = Trim(Get(post, "epc_wholesale_emirate") ?? "Dubai");
        var phone = Trim(Get(post, "epc_wholesale_mobile"));
        if (phone.Length == 0 && regContactType == "phone")
        {
            phone = Trim(regContact);
        }

        await EpcEinvoiceBuyer.SaveBuyerProfileAsync(connection, transaction, new EpcEinvoiceBuyerInput
        {
            UserId = userId,
            BuyerName = Trim(Get(post, "epc_wholesale_legal_name")),
            Trn = trn,
            LegalRegNo = Trim(Get(post, "epc_wholesale_trade_licence")),
            LegalRegType = "TL",
            AddressLine1 = Trim(Get(post, "epc_wholesale_address")),
            City = Trim(Get(post, "epc_wholesale_city")),
            Emirate = emirate.Length > 0 ? emirate : "Dubai",
            CountryCode = "AE",
            Email = regContactType == "email" ? Trim(regContact) : string.Empty,
            Phone = phone,
            BuyerOnboarded = true,
        }, cancellationToken).ConfigureAwait(false);
        await EpcCustomerTrade.ProfileSetAsync(connection, transaction, userId, "epc_uae_company", "1", cancellationToken).ConfigureAwait(false);
    }
}
