using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-kelp client onboard intro. PHP identifiers kept for the inventory:
/// <c>epc_portal_intro_field_defs</c>, <c>epc_portal_intro_defaults</c>,
/// <c>epc_portal_intro_from_post</c>, <c>epc_portal_intro_merge</c>,
/// <c>epc_portal_intro_decode</c>, <c>epc_portal_intro_validate</c>,
/// <c>epc_portal_site_key_from_hostname</c>, <c>epc_portal_tenant_get</c>,
/// <c>epc_portal_apply_intro_to_site_settings</c>, <c>epc_portal_apply_industry_theme_to_tenant</c>,
/// <c>epc_portal_onboard_client</c>, <c>epc_portal_tenant_launch_checklist</c>,
/// <c>epc_portal_erp_only_onboard_steps</c>, <c>epc_portal_onboard_guide_steps</c>.
/// Path: <c>content/general_pages/epc_portal_tenant_intro.php</c>.
/// GET never mints a session cookie. Leftover country-profile / hub-helpers stay injected.
/// Do not write those leftover unique basenames.
/// </summary>
public static class PhpPlanQ1Kelp
{
    public const string PortalTenantIntroPath = "content/general_pages/epc_portal_tenant_intro.php";

    public static Func<string, string>? NormalizeCountryCode { get; set; }
    public static Func<string, Dictionary<string, object?>>? DefaultSiteSettings { get; set; }
    public static Func<Dictionary<string, object?>, Dictionary<string, object?>>? DefaultContact { get; set; }
    public static Func<Dictionary<string, object?>, Dictionary<string, object?>, string, Dictionary<string, object?>, Dictionary<string, object?>>? ApplyIndustryTheme { get; set; }
    public static Action<MySqlConnection, Dictionary<string, object?>>? SaveSiteSettings { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>>? LoadSiteSettingsForHost { get; set; }
    public static Action<MySqlConnection>? DbEnsure { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>, Dictionary<string, object?>>? SaveTenant { get; set; }
    public static Func<string>? PlatformIp { get; set; }
    public static Func<Dictionary<string, object?>, bool>? RowIsSharedErp { get; set; }
    public static Func<Dictionary<string, object?>, string>? ResolveAccessMode { get; set; }
    public static Func<string, string, int>? EnqueueWarmup { get; set; }
    public static Func<string, string, Dictionary<string, object?>, Dictionary<string, object?>>? ApplyCountryProfile { get; set; }
    public static Func<Dictionary<string, object?>, Dictionary<string, string>>? TenantActionUrls { get; set; }
    public static Func<long>? Clock { get; set; }
    public static List<Dictionary<string, object?>> SavedSettings { get; } = [];

    public static void Reset()
    {
        NormalizeCountryCode = null;
        DefaultSiteSettings = null;
        DefaultContact = null;
        ApplyIndustryTheme = null;
        SaveSiteSettings = null;
        LoadSiteSettingsForHost = null;
        DbEnsure = null;
        SaveTenant = null;
        PlatformIp = null;
        RowIsSharedErp = null;
        ResolveAccessMode = null;
        EnqueueWarmup = null;
        ApplyCountryProfile = null;
        TenantActionUrls = null;
        Clock = null;
        SavedSettings.Clear();
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPortalIntroFieldDefs()
        => new(StringComparer.Ordinal)
        {
            ["contact_person"] = Field("Contact person", true),
            ["contact_email"] = Field("Contact email", true),
            ["contact_phone"] = Field("Phone / WhatsApp", false),
            ["legal_name"] = Field("Legal company name", false),
            ["trn"] = Field("TRN / VAT number", false),
            ["city"] = Field("City", false),
            ["country"] = Field("Country", true),
            ["country_code"] = Field("Country code", false),
            ["head_office_address"] = Field("Head office address", false),
            ["admin_email"] = Field("Admin CP email", true),
            ["tagline"] = Field("Tagline", false),
            ["domain_registrar"] = Field("Domain registrar", false),
            ["launch_notes"] = Field("Launch notes", false)
        };

    public static Dictionary<string, object?> EpcPortalIntroDefaults()
        => new(StringComparer.Ordinal)
        {
            ["contact_person"] = "",
            ["contact_email"] = "",
            ["contact_phone"] = "",
            ["legal_name"] = "",
            ["trn"] = "",
            ["city"] = "",
            ["country"] = "United Arab Emirates",
            ["country_code"] = "AE",
            ["head_office_address"] = "",
            ["admin_email"] = "",
            ["tagline"] = "Designed by Electronic World Group",
            ["domain_registrar"] = "GoDaddy",
            ["launch_notes"] = "",
            ["submitted_at"] = 0,
            ["submitted_by"] = ""
        };

    public static Dictionary<string, object?> EpcPortalIntroFromPost(Dictionary<string, object?> post)
    {
        var intro = EpcPortalIntroDefaults();
        foreach (var (key, _) in EpcPortalIntroFieldDefs())
        {
            if (post.ContainsKey(key))
            {
                intro[key] = Str(post[key]).Trim();
            }
        }

        intro["erp_only"] = !PhpEmpty(post.GetValueOrDefault("erp_only"))
            || (!PhpEmpty(post.GetValueOrDefault("tenant_mode")) && Str(post.GetValueOrDefault("tenant_mode")) == "erp_only");
        intro["access_mode"] = post.ContainsKey("access_mode") ? Str(post["access_mode"]) : "";
        if (Str(intro["access_mode"]) == "" && PhpTruthy(intro["erp_only"]))
        {
            intro["access_mode"] = "erp_only";
        }

        if (Str(intro["access_mode"]) == "full_commerce")
        {
            intro["access_mode"] = "full";
        }

        var mods = PhpPlanQ1Brine.EpcPortalErpModulesFromPost(post);
        intro["erp_modules"] = mods.Count > 0 ? mods : new List<string>();
        intro["erp_modules_preset"] = post.ContainsKey("erp_modules_preset") ? Str(post["erp_modules_preset"]) : "";
        intro["erp_only_shared"] = !PhpEmpty(post.GetValueOrDefault("erp_only_shared")) || !PhpEmpty(post.GetValueOrDefault("hosted_on_platform"));
        if (PhpTruthy(intro["erp_only_shared"]))
        {
            intro["hosted_on"] = "platform";
        }

        if (post.ContainsKey("scale_policy"))
        {
            intro["scale_policy"] = Str(post["scale_policy"]).Trim().ToLowerInvariant();
        }

        if (post.ContainsKey("dedicated_db"))
        {
            intro["dedicated_db"] = PhpEmpty(post["dedicated_db"]) ? 0 : 1;
        }
        else if (!PhpEmpty(intro.GetValueOrDefault("erp_only_shared")))
        {
            intro["dedicated_db"] = 1;
            intro["scale_policy"] = "dedicated_mysql";
        }
        else if (!intro.ContainsKey("dedicated_db"))
        {
            intro["dedicated_db"] = 1;
            intro["scale_policy"] = "dedicated_mysql";
        }

        if (!PhpEmpty(post.GetValueOrDefault("country_code")))
        {
            intro["country_code"] = Regex.Replace(Str(post["country_code"]), "[^A-Za-z]", "").ToUpperInvariant();
            if (Str(intro["country_code"]).Length > 2)
            {
                intro["country_code"] = Str(intro["country_code"])[..2];
            }
        }
        else if (!PhpEmpty(intro.GetValueOrDefault("country")))
        {
            var cc = Country(Str(intro["country"]));
            if (cc != "")
            {
                intro["country_code"] = cc;
            }
        }

        if (post.ContainsKey("theme_template"))
        {
            intro["theme_template"] = Regex.Replace(Str(post["theme_template"]).ToLowerInvariant(), "[^a-z0-9_]", "");
        }

        if (post.ContainsKey("storefront_package"))
        {
            intro["storefront_package"] = Regex.Replace(Str(post["storefront_package"]).ToLowerInvariant(), "[^a-z0-9_]", "");
        }

        return intro;
    }

    public static Dictionary<string, object?> EpcPortalIntroMerge(Dictionary<string, object?> stored, Dictionary<string, object?> incoming)
    {
        var baseIntro = EpcPortalIntroDefaults();
        foreach (var key in baseIntro.Keys.ToList())
        {
            if (incoming.TryGetValue(key, out var inc) && Str(inc) != "")
            {
                baseIntro[key] = inc is int or long or double ? inc : Str(inc);
            }
            else if (stored.TryGetValue(key, out var st) && Str(st) != "")
            {
                baseIntro[key] = st is int or long or double ? st : Str(st);
            }
        }

        if (incoming.TryGetValue("submitted_at", out var inAt) && !PhpEmpty(inAt))
        {
            baseIntro["submitted_at"] = ToInt(inAt);
        }
        else if (stored.TryGetValue("submitted_at", out var stAt) && !PhpEmpty(stAt))
        {
            baseIntro["submitted_at"] = ToInt(stAt);
        }

        if (incoming.TryGetValue("submitted_by", out var inBy) && !PhpEmpty(inBy))
        {
            baseIntro["submitted_by"] = Str(inBy);
        }
        else if (stored.TryGetValue("submitted_by", out var stBy) && !PhpEmpty(stBy))
        {
            baseIntro["submitted_by"] = Str(stBy);
        }

        return baseIntro;
    }

    public static Dictionary<string, object?> EpcPortalIntroDecode(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return EpcPortalIntroDefaults();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return EpcPortalIntroDefaults();
            }

            var data = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                data[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.Number => p.Value.TryGetInt64(out var n) ? n : p.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => p.Value.GetString(),
                    _ => p.Value.GetRawText()
                };
            }

            return EpcPortalIntroMerge([], data);
        }
        catch (JsonException)
        {
            return EpcPortalIntroDefaults();
        }
    }

    public static List<string> EpcPortalIntroValidate(Dictionary<string, object?> intro, Dictionary<string, object?> tenantData)
    {
        var errors = new List<string>();
        foreach (var (key, def) in EpcPortalIntroFieldDefs())
        {
            if (PhpTruthy(def.GetValueOrDefault("required")) && Str(intro.GetValueOrDefault(key)).Trim() == "")
            {
                errors.Add(Str(def["label"]) + " is required");
            }
        }

        var email = Str(intro.GetValueOrDefault("contact_email")).Trim();
        if (email != "" && !IsEmail(email))
        {
            errors.Add("Contact email is invalid");
        }

        var admin = Str(intro.GetValueOrDefault("admin_email")).Trim();
        if (admin != "" && !IsEmail(admin))
        {
            errors.Add("Admin CP email is invalid");
        }

        var hostname = Str(tenantData.GetValueOrDefault("hostname")).Trim().ToLowerInvariant();
        var shared = !PhpEmpty(intro.GetValueOrDefault("erp_only_shared"))
            || !PhpEmpty(tenantData.GetValueOrDefault("erp_only_shared"))
            || Str(tenantData.GetValueOrDefault("hosted_on")) == "platform";
        if (!shared && (hostname == "" || !hostname.Contains('.', StringComparison.Ordinal)))
        {
            errors.Add("Primary domain (www.client.com) is required — or enable shared ERP on ecomae.com");
        }

        if (Str(tenantData.GetValueOrDefault("trade_name")).Trim() == "")
        {
            errors.Add("Trade / brand name is required");
        }

        var cc = Str(intro.GetValueOrDefault("country_code")).ToUpperInvariant();
        if (cc.Length > 2)
        {
            cc = cc[..2];
        }

        if (cc == "" && !PhpEmpty(intro.GetValueOrDefault("country")))
        {
            cc = Country(Str(intro["country"]));
        }

        if (cc == "")
        {
            errors.Add("Country is required");
        }

        return errors;
    }

    public static string EpcPortalSiteKeyFromHostname(string hostname)
    {
        var host = hostname.Trim().ToLowerInvariant();
        host = Regex.Replace(host, @"^www\.", "");
        host = Regex.Replace(host, @"\.[a-z0-9.-]+$", "");
        return Regex.Replace(host.Replace('-', '_'), "[^a-z0-9_]", "");
    }

    public static Dictionary<string, object?>? EpcPortalTenantGet(MySqlConnection pdo, string siteKey)
    {
        var key = Regex.Replace(siteKey.ToLowerInvariant(), "[^a-z0-9_]", "");
        if (key == "")
        {
            return null;
        }

        DbEnsure?.Invoke(pdo);
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = "SELECT * FROM `epc_portal_tenants` WHERE `site_key` = @k LIMIT 1";
        cmd.Parameters.AddWithValue("@k", key);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    public static void EpcPortalApplyIntroToSiteSettings(MySqlConnection pdo, string hostname, Dictionary<string, object?> tenantRow, Dictionary<string, object?> intro)
    {
        var settings = DefaultSettings(hostname);
        settings["host"] = hostname;
        settings["industry_code"] = Str(tenantRow.GetValueOrDefault("industry_code"), "auto_parts");
        settings["hub_name"] = Str(tenantRow.GetValueOrDefault("trade_name"), Str(settings.GetValueOrDefault("hub_name")));
        if (!PhpEmpty(intro.GetValueOrDefault("tagline")))
        {
            settings["tagline"] = Str(intro["tagline"]);
        }

        settings["domain_path"] = "https://" + hostname + "/";
        var countryCode = Str(intro.GetValueOrDefault("country_code")).ToUpperInvariant();
        if (countryCode.Length > 2)
        {
            countryCode = countryCode[..2];
        }

        if (countryCode == "" && !PhpEmpty(intro.GetValueOrDefault("country")))
        {
            countryCode = Country(Str(intro["country"]));
        }

        if (countryCode == "")
        {
            countryCode = "AE";
        }

        var contact = Contact(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["trade_name"] = Str(tenantRow.GetValueOrDefault("trade_name")),
            ["hub_name"] = Str(tenantRow.GetValueOrDefault("hub_name")),
            ["from_email"] = Str(tenantRow.GetValueOrDefault("from_email")),
            ["admin_email"] = Str(intro.GetValueOrDefault("admin_email")),
            ["contact_phone"] = Str(intro.GetValueOrDefault("contact_phone")),
            ["head_office_address"] = Str(intro.GetValueOrDefault("head_office_address")),
            ["head_office_email"] = Str(intro.GetValueOrDefault("contact_email")),
            ["city"] = Str(intro.GetValueOrDefault("city")),
            ["country"] = Str(intro.GetValueOrDefault("country")),
            ["country_code"] = countryCode
        });
        contact["trade_name"] = Str(tenantRow.GetValueOrDefault("trade_name"), Str(contact.GetValueOrDefault("trade_name")));
        if (!PhpEmpty(intro.GetValueOrDefault("contact_person")))
        {
            contact["from_name"] = Str(intro["contact_person"]);
        }

        if (!PhpEmpty(tenantRow.GetValueOrDefault("from_email")))
        {
            contact["from_email"] = Str(tenantRow["from_email"]);
        }
        else if (!PhpEmpty(intro.GetValueOrDefault("contact_email")))
        {
            contact["from_email"] = Str(intro["contact_email"]);
        }

        var industryCode = Regex.Replace(Str(tenantRow.GetValueOrDefault("industry_code")), "[^a-z0-9_]", "");
        var erpOnly = !PhpEmpty(intro.GetValueOrDefault("erp_only")) || industryCode == "erp_standalone";
        var accessFromIntro = intro.ContainsKey("access_mode") ? Str(intro["access_mode"]) : "";
        if (erpOnly && accessFromIntro == "")
        {
            accessFromIntro = "erp_only";
        }

        if (erpOnly || accessFromIntro == "erp_only")
        {
            settings["industry_code"] = "erp_standalone";
            settings["access_mode"] = "erp_only";
            settings["enabled_packs"] = new List<string> { "erp" };
            settings["system_name"] = Str(tenantRow.GetValueOrDefault("trade_name"), "ERP Suite") + " ERP";
            var themeOpts = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_only"] = true,
                ["skip_package"] = true,
                ["theme_template"] = !PhpEmpty(intro.GetValueOrDefault("theme_template")) ? Str(intro["theme_template"]) : "classic"
            };
            ApplyTheme(settings, contact, "erp_standalone", themeOpts);
        }
        else
        {
            if (accessFromIntro == "mixed")
            {
                settings["access_mode"] = "mixed";
                var packs = settings.TryGetValue("enabled_packs", out var p) && p is List<string> list ? list : ["core"];
                if (!packs.Contains("erp", StringComparer.Ordinal))
                {
                    settings["enabled_packs"] = packs.Concat(new[] { "erp", "professional" }).ToList();
                }
            }
            else if (accessFromIntro != "" && accessFromIntro is "full" or "consultancy")
            {
                settings["access_mode"] = accessFromIntro;
            }

            var themeOpts = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_only"] = false,
                ["keep_packs"] = accessFromIntro == "mixed",
                ["keep_access_mode"] = accessFromIntro != ""
            };
            if (!PhpEmpty(intro.GetValueOrDefault("theme_template")))
            {
                themeOpts["theme_template"] = Str(intro["theme_template"]);
            }

            if (intro.ContainsKey("storefront_package") && Str(intro["storefront_package"]) != "")
            {
                themeOpts["storefront_package"] = Str(intro["storefront_package"]);
            }

            ApplyTheme(settings, contact, industryCode != "" ? industryCode : "auto_parts", themeOpts);
        }

        settings["erp_modules"] = PhpPlanQ1Brine.EpcPortalErpModulesResolveForOnboard(
            intro,
            Str(settings.GetValueOrDefault("industry_code"), industryCode),
            Str(settings.GetValueOrDefault("access_mode"), "full"));
        settings["contact"] = contact;
        SaveSettings(pdo, settings);
    }

    public static Dictionary<string, object?> EpcPortalApplyIndustryThemeToTenant(MySqlConnection pdo, string siteKey, Dictionary<string, object?>? opts = null)
    {
        opts ??= [];
        DbEnsure?.Invoke(pdo);
        var key = Regex.Replace(siteKey.ToLowerInvariant(), "[^a-z0-9_]", "");
        if (key == "")
        {
            return Fail("Invalid tenant");
        }

        var row = EpcPortalTenantGet(pdo, key);
        if (row is null)
        {
            return Fail("Tenant not found");
        }

        var hostname = Str(row.GetValueOrDefault("hostname"));
        if (hostname == "")
        {
            return Fail("Tenant has no hostname");
        }

        var industryCode = Regex.Replace(Str(opts.GetValueOrDefault("industry_code"), Str(row.GetValueOrDefault("industry_code"), "auto_parts")), "[^a-z0-9_]", "");
        if (industryCode == "")
        {
            industryCode = "auto_parts";
        }

        if (!PhpEmpty(opts.GetValueOrDefault("industry_code")) && industryCode != Str(row.GetValueOrDefault("industry_code")))
        {
            using var upd = pdo.CreateCommand();
            upd.CommandText = "UPDATE `epc_portal_tenants` SET `industry_code` = @i, `updated_at` = @t WHERE `site_key` = @k";
            upd.Parameters.AddWithValue("@i", industryCode);
            upd.Parameters.AddWithValue("@t", Now());
            upd.Parameters.AddWithValue("@k", key);
            upd.ExecuteNonQuery();
            row["industry_code"] = industryCode;
        }

        var settings = LoadSettings(pdo, hostname);
        settings["host"] = hostname;
        var contact = settings.TryGetValue("contact", out var c) && c is Dictionary<string, object?> map
            ? map
            : [];
        var erpOnly = industryCode == "erp_standalone" || Str(settings.GetValueOrDefault("access_mode")) == "erp_only";
        var themeOpts = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_only"] = erpOnly,
            ["skip_package"] = erpOnly,
            ["force_package_tagline"] = !PhpEmpty(opts.GetValueOrDefault("force_package_tagline"))
        };
        if (!PhpEmpty(opts.GetValueOrDefault("theme_template")))
        {
            themeOpts["theme_template"] = Str(opts["theme_template"]);
        }

        if (opts.ContainsKey("storefront_package") && Str(opts["storefront_package"]) != "")
        {
            themeOpts["storefront_package"] = Str(opts["storefront_package"]);
        }

        var applied = ApplyTheme(settings, contact, industryCode, themeOpts);
        settings["contact"] = contact;
        SaveSettings(pdo, settings);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = Str(applied.GetValueOrDefault("message")),
            ["theme_template"] = Str(applied.GetValueOrDefault("theme_template")),
            ["storefront_package"] = Str(applied.GetValueOrDefault("storefront_package")),
            ["client_sync"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "skipped" },
            ["site_key"] = key
        };
    }

    public static Dictionary<string, object?> EpcPortalOnboardClient(MySqlConnection pdo, Dictionary<string, object?> post, string submittedBy = "")
    {
        DbEnsure?.Invoke(pdo);
        var hostname = Str(post.GetValueOrDefault("hostname")).Trim().ToLowerInvariant();
        var siteKey = Regex.Replace(Str(post.GetValueOrDefault("site_key")).ToLowerInvariant(), "[^a-z0-9_]", "");
        var intro = EpcPortalIntroFromPost(post);
        var erpOnlyShared = !PhpEmpty(post.GetValueOrDefault("erp_only_shared"))
            || !PhpEmpty(post.GetValueOrDefault("hosted_on_platform"))
            || !PhpEmpty(intro.GetValueOrDefault("erp_only_shared"));
        if (erpOnlyShared)
        {
            hostname = "www.ecomae.com";
        }

        if (siteKey == "" && hostname != "" && !erpOnlyShared)
        {
            siteKey = EpcPortalSiteKeyFromHostname(hostname);
        }

        var existing = siteKey != "" ? EpcPortalTenantGet(pdo, siteKey) : null;
        if (existing is not null)
        {
            intro = EpcPortalIntroMerge(EpcPortalIntroDecode(Str(existing.GetValueOrDefault("intro_json"))), intro);
        }

        bool dedicatedDb;
        if (post.ContainsKey("scale_policy"))
        {
            dedicatedDb = Str(post["scale_policy"]).Trim().ToLowerInvariant() == "dedicated_mysql";
        }
        else if (post.ContainsKey("dedicated_db"))
        {
            dedicatedDb = !PhpEmpty(post["dedicated_db"]);
        }
        else
        {
            dedicatedDb = !intro.ContainsKey("dedicated_db") || !PhpEmpty(intro["dedicated_db"]);
        }

        if (erpOnlyShared)
        {
            dedicatedDb = true;
        }

        var scalePolicy = dedicatedDb ? "dedicated_mysql" : "shared_docpart";
        var tenantData = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = siteKey,
            ["hostname"] = hostname,
            ["industry_code"] = !PhpEmpty(intro.GetValueOrDefault("erp_only")) ? "erp_standalone" : Str(post.GetValueOrDefault("industry_code"), "auto_parts"),
            ["status"] = Str(post.GetValueOrDefault("status"), "dns_pending"),
            ["trade_name"] = Str(post.GetValueOrDefault("trade_name")),
            ["hub_name"] = Str(post.GetValueOrDefault("hub_name"), "Electronic World Group"),
            ["from_email"] = Str(post.GetValueOrDefault("from_email"), Str(intro.GetValueOrDefault("contact_email"))),
            ["db_name"] = Str(post.GetValueOrDefault("db_name"), dedicatedDb ? siteKey : "docpart"),
            ["db_user"] = Str(post.GetValueOrDefault("db_user"), dedicatedDb ? siteKey : "docpart"),
            ["db_password"] = Str(post.GetValueOrDefault("db_password")),
            ["notes"] = Str(post.GetValueOrDefault("notes")),
            ["hosted_on"] = erpOnlyShared ? "platform" : "client",
            ["erp_only_shared"] = erpOnlyShared ? 1 : 0,
            ["dedicated_db"] = dedicatedDb ? 1 : 0,
            ["scale_policy"] = scalePolicy,
            ["blockchain_mode"] = post.ContainsKey("blockchain_mode") ? Str(post["blockchain_mode"]) : Str(intro.GetValueOrDefault("blockchain_mode"), "anchor")
        };
        var errors = EpcPortalIntroValidate(intro, tenantData);
        if (errors.Count > 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = string.Join(". ", errors),
                ["errors"] = errors
            };
        }

        intro["submitted_at"] = Now();
        if (submittedBy != "")
        {
            intro["submitted_by"] = submittedBy;
        }

        var result = PersistTenant(pdo, tenantData);
        if (PhpEmpty(result.GetValueOrDefault("ok")) && result.GetValueOrDefault("ok") is not true)
        {
            return result;
        }

        using (var upd = pdo.CreateCommand())
        {
            upd.CommandText = "UPDATE `epc_portal_tenants` SET `intro_json` = @j WHERE `site_key` = @k";
            upd.Parameters.AddWithValue("@j", JsonSerializer.Serialize(intro));
            upd.Parameters.AddWithValue("@k", siteKey);
            upd.ExecuteNonQuery();
        }

        var row = EpcPortalTenantGet(pdo, siteKey);
        if (row is not null)
        {
            EpcPortalApplyIntroToSiteSettings(pdo, hostname, row, intro);
        }

        var countryProfile = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "skipped" };
        var cc = Str(intro.GetValueOrDefault("country_code")).ToUpperInvariant();
        if (cc.Length > 2)
        {
            cc = cc[..2];
        }

        if (cc == "" && !PhpEmpty(intro.GetValueOrDefault("country")))
        {
            cc = Country(Str(intro["country"]));
        }

        if (cc != "" && siteKey != "" && ApplyCountryProfile is not null)
        {
            countryProfile = ApplyCountryProfile(siteKey, cc, intro);
        }

        var warmup = 0;
        if (dedicatedDb && siteKey != "")
        {
            try
            {
                warmup = EnqueueWarmup?.Invoke("tenant_warmup_pdo", siteKey) ?? 0;
            }
            catch
            {
                warmup = 0;
            }
        }

        var checklist = EpcPortalTenantLaunchChecklist(pdo, siteKey);
        var msg = "Client onboarded — tenant registered and portal settings seeded. Complete DNS + set Live when ready.";
        if (erpOnlyShared)
        {
            msg = "Shared ERP company registered on www.ecomae.com — CP users and MySQL database provisioned when CloudPanel is configured.";
        }
        else if (dedicatedDb)
        {
            msg = "Client onboarded with dedicated MySQL (scale-ready). Complete DNS + set Live when ready.";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = msg,
            ["site_key"] = siteKey,
            ["hostname"] = hostname,
            ["country_code"] = cc != "" ? cc : "AE",
            ["country_profile"] = countryProfile,
            ["dedicated_db"] = dedicatedDb ? 1 : 0,
            ["scale_policy"] = scalePolicy,
            ["warmup_job_id"] = warmup,
            ["cp_url"] = "",
            ["erp_url"] = "",
            ["checklist"] = checklist
        };
    }

    public static Dictionary<string, object?> EpcPortalTenantLaunchChecklist(MySqlConnection pdo, string siteKey)
    {
        DbEnsure?.Invoke(pdo);
        var row = EpcPortalTenantGet(pdo, siteKey);
        if (row is null)
        {
            return [];
        }

        var intro = EpcPortalIntroDecode(Str(row.GetValueOrDefault("intro_json")));
        var hostname = Str(row.GetValueOrDefault("hostname"));
        var status = Str(row.GetValueOrDefault("status"));
        var sharedErp = RowIsSharedErp?.Invoke(row) == true
            || !PhpEmpty(row.GetValueOrDefault("erp_only_shared"))
            || Str(row.GetValueOrDefault("hosted_on")) == "platform";
        var loginHost = sharedErp ? "www.ecomae.com" : hostname;
        var submitted = !PhpEmpty(intro.GetValueOrDefault("submitted_at"));
        var items = new List<Dictionary<string, object?>>
        {
            Item("intro", "Client intro form submitted", submitted, submitted ? FormatTs(ToInt(intro["submitted_at"])) : "Fill the onboard form"),
            Item("tenant", "Tenant registered in platform DB", true, hostname),
            Item("db", "Tenant MySQL database configured", Str(row.GetValueOrDefault("db_name")).Trim() != "", Str(row.GetValueOrDefault("db_name")).Trim() != "" ? Str(row["db_name"]) : "Create DB + enter credentials"),
            Item("settings", "Site settings & contact seeded", submitted, "Branding, from-email, admin email"),
            Item("dns", "GoDaddy DNS → platform IP", status is "dns_pending" or "live", "A record @ and www → " + Ip()),
            Item("alias", "CloudPanel domain alias on www.ecomae.com", false, "Add " + hostname + " as alias (manual)"),
            Item("ssl", "SSL certificate issued", false, "Let's Encrypt for alias"),
            Item("live", "Tenant status = Live", status == "live", status == "live" ? "Live" : "Set status to Live in Tenants tab"),
            Item("cp", "Client CP accessible", status == "live", "https://" + hostname + "/cp/")
        };
        if (sharedErp)
        {
            foreach (var item in items)
            {
                var id = Str(item["id"]);
                if (id is "dns" or "alias" or "ssl")
                {
                    item["label"] = Str(item["label"]).Replace("GoDaddy DNS", "Client DNS", StringComparison.Ordinal);
                    item["done"] = true;
                    item["hint"] = "Not required — shared ERP on www.ecomae.com (no client domain)";
                }
            }
        }

        var done = items.Count(i => PhpTruthy(i.GetValueOrDefault("done")));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["items"] = items,
            ["done"] = done,
            ["total"] = items.Count,
            ["ready"] = status == "live",
            ["hostname"] = hostname,
            ["cp_url"] = "https://" + loginHost + "/cp/",
            ["erp_url"] = "https://" + loginHost + "/cp/shop/finance/erp?epc_erp_shell=1",
            ["erp_only_shared"] = sharedErp
        };
    }

    public static List<Dictionary<string, object?>> EpcPortalErpOnlyOnboardSteps()
        =>
        [
            Step("Choose shared ERP on ecomae.com", "On <strong>Onboard client</strong>, tick <strong>ERP only</strong> and <strong>Hosted on ecomae.com (shared)</strong>. Enter company display name and site key (e.g. <code>asap</code>) — <strong>no client domain</strong>. Hostname is always <code>www.ecomae.com</code>."),
            Step("Choose access mode & ERP modules", "Set <code>access_mode=erp_only</code> and tick the ERP module grid — presets include Full ERP, Custom &amp; Shipping only, People only, Finance + e-invoice. Settings sync to the company MySQL DB on Live."),
            Step("Create tenant database & ERP users", "Provision one MySQL database per company on the platform VPS (<code>asapc</code>, <code>company2</code>, …). Use Tenant hub onboard with <strong>Hosted on ecomae.com (shared)</strong> — the provision flow creates DB credentials automatically when CloudPanel is configured."),
            Step("Add company 2, 3 on same host", "Repeat onboard with a new site key and separate MySQL DB. Each company gets its own login at <code>/cp/client-erp/{site_key}/</code>. If the same email exists in two companies, the login form shows a company picker."),
            Step("Hand off login URL", "Share <code>https://www.ecomae.com/cp/client-erp/{site_key}/</code> (e.g. <code>…/asapcustom/</code>). After login, users land in the ERP shell for their company only — <strong>no Super CP</strong>, no storefront, no client DNS.")
        ];

    public static List<Dictionary<string, object?>> EpcPortalOnboardGuideSteps()
    {
        var ip = Ip();
        return
        [
            Step("Collect client intro", "Use the <strong>Onboard client</strong> tab — one form captures brand, domain, contacts, admin email, and DB credentials. Submitting registers the tenant and seeds portal settings immediately, including <code>contact.use_animated_hub_logo</code> so the client storefront header shows the ECOM AE animated hub beside their trade name."),
            Step("Create tenant database", "In CloudPanel / MySQL, create one database per client on the same VPS. Enter db name, user, and password in the intro form (or Tenants tab). Import industry seed if needed (e.g. docpart clone for auto parts)."),
            Step("GoDaddy DNS", "Client keeps the domain at GoDaddy. Add A records <code>@</code> and <code>www</code> → <code>" + ip + "</code>. Remove old A records. Wait 5–60 minutes."),
            Step("CloudPanel alias (no extra site)", "Add <code>www.client.com</code> as a <strong>domain alias</strong> on the existing <code>www.ecomae.com</code> site — same docroot, zero extra disk. Do not create a separate CloudPanel site per client."),
            Step("SSL + go Live", "Issue Let's Encrypt for the alias. In Tenant hub → Tenants, set status to <strong>Live</strong>. The app routes by hostname to the tenant DB automatically."),
            Step("Hand off client CP", "Client control panel: <code>https://www.client.com/cp/</code>. Platform operator hub stays at <code>https://cp.ecomae.com/cp/</code>. Share admin credentials separately.")
        ];
    }

    private static Dictionary<string, object?> Field(string label, bool required)
        => new(StringComparer.Ordinal) { ["label"] = label, ["required"] = required };

    private static Dictionary<string, object?> Step(string title, string body)
        => new(StringComparer.Ordinal) { ["title"] = title, ["body"] = body };

    private static Dictionary<string, object?> Item(string id, string label, bool done, string hint)
        => new(StringComparer.Ordinal) { ["id"] = id, ["label"] = label, ["done"] = done, ["hint"] = hint };

    private static Dictionary<string, object?> Fail(string message)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["message"] = message };

    private static Dictionary<string, object?> DefaultSettings(string hostname)
        => DefaultSiteSettings?.Invoke(hostname)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["host"] = hostname,
                ["hub_name"] = "Hub",
                ["enabled_packs"] = new List<string> { "core" },
                ["access_mode"] = "full"
            };

    private static Dictionary<string, object?> Contact(Dictionary<string, object?> row)
        => DefaultContact?.Invoke(row)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["trade_name"] = Str(row.GetValueOrDefault("trade_name")),
                ["from_email"] = Str(row.GetValueOrDefault("from_email")),
                ["country"] = Str(row.GetValueOrDefault("country")),
                ["country_code"] = Str(row.GetValueOrDefault("country_code"))
            };

    private static Dictionary<string, object?> ApplyTheme(Dictionary<string, object?> settings, Dictionary<string, object?> contact, string industry, Dictionary<string, object?> opts)
    {
        if (ApplyIndustryTheme is not null)
        {
            return ApplyIndustryTheme(settings, contact, industry, opts);
        }

        settings["theme_industry"] = industry;
        settings["theme_erp_only"] = PhpTruthy(opts.GetValueOrDefault("erp_only")) ? 1 : 0;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = "theme:" + industry,
            ["theme_template"] = Str(opts.GetValueOrDefault("theme_template"), "classic"),
            ["storefront_package"] = Str(opts.GetValueOrDefault("storefront_package"))
        };
    }

    private static void SaveSettings(MySqlConnection pdo, Dictionary<string, object?> settings)
    {
        SavedSettings.Add(settings);
        SaveSiteSettings?.Invoke(pdo, settings);
    }

    private static Dictionary<string, object?> LoadSettings(MySqlConnection pdo, string host)
        => LoadSiteSettingsForHost?.Invoke(pdo, host)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["host"] = host, ["access_mode"] = "full" };

    private static Dictionary<string, object?> PersistTenant(MySqlConnection pdo, Dictionary<string, object?> data)
    {
        if (SaveTenant is not null)
        {
            return SaveTenant(pdo, data);
        }

        using var cmd = pdo.CreateCommand();
        cmd.CommandText = "INSERT INTO `epc_portal_tenants` (`site_key`,`hostname`,`industry_code`,`status`,`trade_name`,`hub_name`,`from_email`,`db_name`,`db_user`,`db_password`,`notes`,`intro_json`,`hosted_on`,`erp_only_shared`,`dedicated_db`,`scale_policy`,`blockchain_mode`,`created_at`,`updated_at`) VALUES (@sk,@h,@i,@s,@t,@hub,@fe,@db,@du,@dp,@n,'',@ho,@eo,@dd,@sp,@bc,@c,@u)";
        cmd.Parameters.AddWithValue("@sk", Str(data["site_key"]));
        cmd.Parameters.AddWithValue("@h", Str(data["hostname"]));
        cmd.Parameters.AddWithValue("@i", Str(data["industry_code"]));
        cmd.Parameters.AddWithValue("@s", Str(data["status"]));
        cmd.Parameters.AddWithValue("@t", Str(data["trade_name"]));
        cmd.Parameters.AddWithValue("@hub", Str(data["hub_name"]));
        cmd.Parameters.AddWithValue("@fe", Str(data["from_email"]));
        cmd.Parameters.AddWithValue("@db", Str(data["db_name"]));
        cmd.Parameters.AddWithValue("@du", Str(data["db_user"]));
        cmd.Parameters.AddWithValue("@dp", Str(data["db_password"]));
        cmd.Parameters.AddWithValue("@n", Str(data["notes"]));
        cmd.Parameters.AddWithValue("@ho", Str(data["hosted_on"]));
        cmd.Parameters.AddWithValue("@eo", ToInt(data.GetValueOrDefault("erp_only_shared")));
        cmd.Parameters.AddWithValue("@dd", ToInt(data.GetValueOrDefault("dedicated_db")));
        cmd.Parameters.AddWithValue("@sp", Str(data["scale_policy"]));
        cmd.Parameters.AddWithValue("@bc", Str(data["blockchain_mode"]));
        cmd.Parameters.AddWithValue("@c", Now());
        cmd.Parameters.AddWithValue("@u", Now());
        cmd.ExecuteNonQuery();
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["site_key"] = data["site_key"] };
    }

    private static string Country(string value)
    {
        if (NormalizeCountryCode is not null)
        {
            return NormalizeCountryCode(value);
        }

        var v = value.Trim().ToUpperInvariant();
        if (v is "UNITED ARAB EMIRATES" or "UAE" or "AE")
        {
            return "AE";
        }

        return v is "PAKISTAN" or "PK" ? "PK" : Regex.IsMatch(v, "^[A-Z]{2}$") ? v : "";
    }

    private static string Ip() => PlatformIp?.Invoke() ?? "203.0.113.10";

    private static long Now() => Clock?.Invoke() ?? 1700000000;

    private static string FormatTs(long ts)
        => DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static bool IsEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return email.Contains('@', StringComparison.Ordinal) && email.Contains('.', StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string Str(object? value, string fallback = "")
    {
        if (value is null)
        {
            return fallback;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
    }

    private static int ToInt(object? value)
        => Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);

    private static bool PhpTruthy(object? value)
        => value is true or 1 or 1L or 1.0 or "1" || (value is string s && s != "" && s != "0");

    private static bool PhpEmpty(object? value)
        => value is null or false or 0 or 0L or 0d || value is string s && (s == "" || s == "0");
}
