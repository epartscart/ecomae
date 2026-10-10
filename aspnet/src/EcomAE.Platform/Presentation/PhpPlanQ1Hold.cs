using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hold helpers. PHP identifiers kept for the inventory:
/// <c>epc_sso_ensure_schema</c>, <c>epc_sso_sp_metadata</c>,
/// <c>epc_sso_sp_metadata_xml</c>, <c>epc_sso_provider_create</c>,
/// <c>epc_sso_provider_get</c>, <c>epc_sso_provider_list</c>,
/// <c>epc_sso_provider_toggle</c>, <c>epc_sso_provider_delete</c>,
/// <c>epc_sso_authn_request</c>, <c>epc_sso_process_response</c>,
/// <c>epc_sso_active_sessions</c>, <c>epc_sso_logout</c>,
/// <c>epc_sso_expire_old</c>, <c>epc_sso_fleet_stats</c>,
/// <c>EPC_SSO_SAML_VERSION</c>,
/// <c>epc_bos_health_check_tenant</c>, <c>epc_bos_health_check_all</c>,
/// <c>epc_bos_health_summary</c>.
/// </summary>
public static class PhpPlanQ1Hold
{
    public const string SsoSamlPath = "content/general_pages/epc_sso_saml.php";
    public const string BosHealthPath = "content/general_pages/epc_bos_health_check.php";
    public const string EpcSsoSamlVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] CoreTables = ["users", "sessions", "pages", "modules", "control_groups", "control_items"];
    private static readonly string[] ErpTables = ["epc_erp_coa_accounts", "epc_erp_gl_journals", "epc_erp_cash_bank_accounts", "epc_erp_suppliers", "epc_erp_audit_log"];

    public static Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
    public static Func<string> RequestId { get; set; } = () => "_" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
    public static Dictionary<string, string> Server { get; } = new(StringComparer.OrdinalIgnoreCase);
    public static Func<string, (object? Pdo, string Error)> TenantConnect { get; set; } = _ => (null, "Connection failed");

    public static void Reset()
    {
        UtcNow = () => DateTime.UtcNow;
        RequestId = () => "_" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        Server.Clear();
        TenantConnect = _ => (null, "Connection failed");
    }

    public sealed class SsoStore
    {
        public int NextProviderId { get; set; } = 1;
        public int NextSessionId { get; set; } = 1;
        public List<ProviderRow> Providers { get; } = new();
        public List<SessionRow> Sessions { get; } = new();
    }

    public sealed class ProviderRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string ProviderName { get; set; } = "";
        public string ProviderType { get; set; } = "saml2";
        public string EntityId { get; set; } = "";
        public string SsoUrl { get; set; } = "";
        public string SloUrl { get; set; } = "";
        public string Certificate { get; set; } = "";
        public string MetadataXml { get; set; } = "";
        public string AttributeMapJson { get; set; } = "{}";
        public int JitProvision { get; set; } = 1;
        public string DefaultRole { get; set; } = "viewer";
        public int Active { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class SessionRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public int ProviderId { get; set; }
        public int UserId { get; set; }
        public string NameId { get; set; } = "";
        public string Email { get; set; } = "";
        public string AttributesJson { get; set; } = "{}";
        public string SessionIndex { get; set; } = "";
        public string Status { get; set; } = "active";
        public string IpAddress { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public DateTime LoginAt { get; set; }
        public DateTime? LogoutAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public sealed class HealthStore
    {
        public List<TenantRow> Tenants { get; } = new();
        public Dictionary<string, TenantDb> Dbs { get; } = new(StringComparer.Ordinal);
    }

    public sealed class TenantRow
    {
        public string SiteKey { get; set; } = "";
        public string TradeName { get; set; } = "";
        public string DbName { get; set; } = "";
        public string Hostname { get; set; } = "";
        public int IsActive { get; set; } = 1;
    }

    public sealed class TenantDb
    {
        public Dictionary<string, int> Tables { get; } = new(StringComparer.Ordinal);
        public int AdminUsers { get; set; }
        public int ActiveLanguages { get; set; }
        public string ConnectError { get; set; } = "";
        public bool Connected { get; set; }
    }

    public static void EpcSsoEnsureSchema(SsoStore db) => _ = db;

    public static Dictionary<string, object?> EpcSsoSpMetadata(string siteKey)
    {
        var baseUrl = "https://www.ecomae.com";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entity_id"] = baseUrl + "/saml/sp/" + siteKey,
            ["acs_url"] = baseUrl + "/saml/acs/" + siteKey,
            ["slo_url"] = baseUrl + "/saml/slo/" + siteKey,
            ["metadata_url"] = baseUrl + "/saml/metadata/" + siteKey,
            ["name_id_format"] = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };
    }

    public static string EpcSsoSpMetadataXml(string siteKey)
    {
        var sp = EpcSsoSpMetadata(siteKey);
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
               "<md:EntityDescriptor xmlns:md=\"urn:oasis:names:tc:SAML:2.0:metadata\"\n" +
               "    entityID=\"" + XmlH(Str(sp["entity_id"])) + "\">\n" +
               "  <md:SPSSODescriptor protocolSupportEnumeration=\"urn:oasis:names:tc:SAML:2.0:protocol\"\n" +
               "      AuthnRequestsSigned=\"true\" WantAssertionsSigned=\"true\">\n" +
               "    <md:NameIDFormat>" + Str(sp["name_id_format"]) + "</md:NameIDFormat>\n" +
               "    <md:AssertionConsumerService\n" +
               "        Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST\"\n" +
               "        Location=\"" + XmlH(Str(sp["acs_url"])) + "\"\n" +
               "        index=\"0\" isDefault=\"true\"/>\n" +
               "    <md:SingleLogoutService\n" +
               "        Binding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect\"\n" +
               "        Location=\"" + XmlH(Str(sp["slo_url"])) + "\"/>\n" +
               "  </md:SPSSODescriptor>\n" +
               "</md:EntityDescriptor>";
    }

    public static Dictionary<string, object?> EpcSsoProviderCreate(SsoStore db, string siteKey, IReadOnlyDictionary<string, object?> data)
    {
        EpcSsoEnsureSchema(db);
        var attrMap = data.TryGetValue("attribute_map", out var am) && am is IReadOnlyDictionary<string, object?> map
            ? map
            : DefaultAttrMap();
        var now = UtcNow();
        var row = new ProviderRow
        {
            Id = db.NextProviderId++,
            SiteKey = siteKey,
            ProviderName = Str(data, "provider_name", "SSO Provider"),
            ProviderType = Str(data, "provider_type", "saml2"),
            EntityId = Str(data, "entity_id"),
            SsoUrl = Str(data, "sso_url"),
            SloUrl = Str(data, "slo_url"),
            Certificate = Str(data, "certificate"),
            MetadataXml = Str(data, "metadata_xml"),
            AttributeMapJson = JsonSerializer.Serialize(attrMap, JsonOpts),
            JitProvision = ToInt(data, "jit_provision", 1),
            DefaultRole = Str(data, "default_role", "viewer"),
            Active = ToInt(data, "active"),
            CreatedBy = ToInt(data, "created_by"),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Providers.Add(row);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["provider_id"] = row.Id };
    }

    public static object EpcSsoProviderGet(SsoStore db, int providerId)
    {
        var row = db.Providers.FirstOrDefault(p => p.Id == providerId);
        if (row is null)
        {
            return new List<object>();
        }

        object? attr;
        try
        {
            attr = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrEmpty(row.AttributeMapJson) ? "{}" : row.AttributeMapJson);
        }
        catch (JsonException)
        {
            attr = null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["site_key"] = row.SiteKey,
            ["provider_name"] = row.ProviderName,
            ["provider_type"] = row.ProviderType,
            ["entity_id"] = row.EntityId,
            ["sso_url"] = row.SsoUrl,
            ["slo_url"] = row.SloUrl,
            ["certificate"] = row.Certificate,
            ["metadata_xml"] = row.MetadataXml,
            ["attribute_map"] = attr,
            ["jit_provision"] = row.JitProvision,
            ["default_role"] = row.DefaultRole,
            ["active"] = row.Active,
            ["created_by"] = row.CreatedBy,
            ["created_at"] = row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            ["updated_at"] = row.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
    }

    public static List<Dictionary<string, object?>> EpcSsoProviderList(SsoStore db, string siteKey)
    {
        EpcSsoEnsureSchema(db);
        return db.Providers.Where(p => p.SiteKey == siteKey)
            .OrderBy(p => p.ProviderName, StringComparer.Ordinal)
            .Select(p => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = p.Id,
                ["provider_name"] = p.ProviderName,
                ["provider_type"] = p.ProviderType,
                ["entity_id"] = p.EntityId,
                ["sso_url"] = p.SsoUrl,
                ["active"] = p.Active,
                ["jit_provision"] = p.JitProvision,
                ["default_role"] = p.DefaultRole,
                ["updated_at"] = p.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            }).ToList();
    }

    public static bool EpcSsoProviderToggle(SsoStore db, int providerId, bool active)
    {
        var row = db.Providers.FirstOrDefault(p => p.Id == providerId);
        if (row is not null)
        {
            row.Active = active ? 1 : 0;
            row.UpdatedAt = UtcNow();
        }

        return true;
    }

    public static bool EpcSsoProviderDelete(SsoStore db, int providerId)
    {
        db.Providers.RemoveAll(p => p.Id == providerId);
        return true;
    }

    public static Dictionary<string, object?> EpcSsoAuthnRequest(string siteKey, IReadOnlyDictionary<string, object?> provider)
    {
        var sp = EpcSsoSpMetadata(siteKey);
        var id = RequestId();
        var issueInstant = UtcNow().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var request = "<samlp:AuthnRequest xmlns:samlp=\"urn:oasis:names:tc:SAML:2.0:protocol\"\n" +
                      "        xmlns:saml=\"urn:oasis:names:tc:SAML:2.0:assertion\"\n" +
                      "        ID=\"" + id + "\"\n" +
                      "        Version=\"2.0\"\n" +
                      "        IssueInstant=\"" + issueInstant + "\"\n" +
                      "        Destination=\"" + XmlH(Str(provider, "sso_url")) + "\"\n" +
                      "        AssertionConsumerServiceURL=\"" + XmlH(Str(sp["acs_url"])) + "\"\n" +
                      "        ProtocolBinding=\"urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST\">\n" +
                      "    <saml:Issuer>" + XmlH(Str(sp["entity_id"])) + "</saml:Issuer>\n" +
                      "    <samlp:NameIDPolicy Format=\"" + Str(sp["name_id_format"]) + "\" AllowCreate=\"true\"/>\n" +
                      "</samlp:AuthnRequest>";
        var encoded = Convert.ToBase64String(Deflate(Encoding.UTF8.GetBytes(request)));
        var ssoUrl = Str(provider, "sso_url");
        var redirect = ssoUrl + "?" + "SAMLRequest=" + Uri.EscapeDataString(encoded).Replace("%20", "+");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["request_id"] = id,
            ["redirect_url"] = redirect,
            ["raw_request"] = request
        };
    }

    public static Dictionary<string, object?> EpcSsoProcessResponse(SsoStore db, string siteKey, string samlResponse, int providerId)
    {
        var providerObj = EpcSsoProviderGet(db, providerId);
        if (providerObj is not IReadOnlyDictionary<string, object?> provider || provider.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Provider not found" };
        }

        byte[] xmlBytes;
        try
        {
            xmlBytes = Convert.FromBase64String(samlResponse);
        }
        catch (FormatException)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid SAML response encoding" };
        }

        var xml = Encoding.UTF8.GetString(xmlBytes);
        var doc = new XmlDocument { XmlResolver = null };
        try
        {
            doc.LoadXml(xml);
        }
        catch (XmlException)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Invalid SAML response XML" };
        }

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
        ns.AddNamespace("samlp", "urn:oasis:names:tc:SAML:2.0:protocol");
        var status = doc.SelectSingleNode("//samlp:Status/samlp:StatusCode/@Value", ns);
        if (status is not null && (status.Value ?? "").IndexOf("Success", StringComparison.Ordinal) < 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "SAML authentication failed: " + status.Value
            };
        }

        var nameId = doc.SelectSingleNode("//saml:Assertion/saml:Subject/saml:NameID", ns)?.InnerText ?? "";
        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
        var attrs = doc.SelectNodes("//saml:Assertion/saml:AttributeStatement/saml:Attribute", ns);
        if (attrs is not null)
        {
            foreach (XmlNode attr in attrs)
            {
                var attrName = attr.Attributes?["Name"]?.Value ?? "";
                var valueNode = attr.SelectSingleNode("saml:AttributeValue", ns);
                attributes[attrName] = valueNode?.InnerText ?? "";
            }
        }

        var email = "";
        var firstName = "";
        var lastName = "";
        var role = Str(provider, "default_role", "viewer");
        var attrMap = provider.TryGetValue("attribute_map", out var am) && am is IReadOnlyDictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>();
        foreach (var kv in attrMap)
        {
            var claim = Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "";
            if (!attributes.TryGetValue(claim, out var val))
            {
                continue;
            }

            var text = Convert.ToString(val, CultureInfo.InvariantCulture) ?? "";
            switch (kv.Key)
            {
                case "email": email = text; break;
                case "first_name": firstName = text; break;
                case "last_name": lastName = text; break;
                case "role": role = text; break;
            }
        }

        if (email == "" && nameId != "" && IsEmail(nameId))
        {
            email = nameId;
        }

        var sessionIndex = doc.SelectSingleNode("//saml:Assertion/saml:AuthnStatement/@SessionIndex", ns)?.Value ?? "";
        var ua = Server.TryGetValue("HTTP_USER_AGENT", out var u) ? u ?? "" : "";
        var session = new SessionRow
        {
            Id = db.NextSessionId++,
            SiteKey = siteKey,
            ProviderId = providerId,
            NameId = nameId,
            Email = email,
            AttributesJson = JsonSerializer.Serialize(attributes, JsonOpts),
            SessionIndex = sessionIndex,
            Status = "active",
            IpAddress = Server.TryGetValue("REMOTE_ADDR", out var ip) ? ip ?? "" : "",
            UserAgent = ByteSubstr(ua, 255),
            LoginAt = UtcNow(),
            ExpiresAt = UtcNow().AddHours(8)
        };
        db.Sessions.Add(session);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["session_id"] = session.Id,
            ["email"] = email,
            ["first_name"] = firstName,
            ["last_name"] = lastName,
            ["role"] = role,
            ["name_id"] = nameId,
            ["jit_provision"] = !IsEmpty(provider.TryGetValue("jit_provision", out var jit) ? jit : 0)
        };
    }

    public static List<Dictionary<string, object?>> EpcSsoActiveSessions(SsoStore db, string siteKey)
    {
        EpcSsoEnsureSchema(db);
        var now = UtcNow();
        var rows = new List<Dictionary<string, object?>>();
        foreach (var s in db.Sessions.Where(x => x.SiteKey == siteKey && x.Status == "active" && x.ExpiresAt > now).OrderByDescending(x => x.LoginAt))
        {
            var p = db.Providers.FirstOrDefault(pr => pr.Id == s.ProviderId);
            if (p is null)
            {
                continue;
            }

            object? attr;
            try
            {
                attr = JsonSerializer.Deserialize<Dictionary<string, object?>>(string.IsNullOrEmpty(s.AttributesJson) ? "{}" : s.AttributesJson);
            }
            catch (JsonException)
            {
                attr = null;
            }

            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = s.Id,
                ["site_key"] = s.SiteKey,
                ["provider_id"] = s.ProviderId,
                ["user_id"] = s.UserId,
                ["name_id"] = s.NameId,
                ["email"] = s.Email,
                ["attributes"] = attr,
                ["session_index"] = s.SessionIndex,
                ["status"] = s.Status,
                ["ip_address"] = s.IpAddress,
                ["user_agent"] = s.UserAgent,
                ["provider_name"] = p.ProviderName
            });
        }

        return rows;
    }

    public static bool EpcSsoLogout(SsoStore db, int sessionId)
    {
        var row = db.Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (row is not null)
        {
            row.Status = "logged_out";
            row.LogoutAt = UtcNow();
        }

        return true;
    }

    public static int EpcSsoExpireOld(SsoStore db)
    {
        var now = UtcNow();
        var n = 0;
        foreach (var s in db.Sessions.Where(x => x.Status == "active" && x.ExpiresAt < now))
        {
            s.Status = "expired";
            n++;
        }

        return n;
    }

    public static List<Dictionary<string, object?>> EpcSsoFleetStats(SsoStore db)
    {
        EpcSsoEnsureSchema(db);
        return db.Providers
            .GroupBy(p => p.SiteKey, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = g.Key,
                ["providers"] = g.Select(p => p.Id).Distinct().Count(),
                ["active_providers"] = g.Count(p => p.Active == 1).ToString(CultureInfo.InvariantCulture),
                ["active_sessions"] = db.Sessions.Count(s => s.SiteKey == g.Key && s.Status == "active"),
                ["total_logins"] = db.Sessions.Count(s => s.SiteKey == g.Key)
            })
            .OrderByDescending(r => Convert.ToInt32(r["active_providers"], CultureInfo.InvariantCulture))
            .ToList();
    }

    public static Dictionary<string, object?> EpcBosHealthCheckTenant(HealthStore db, string siteKey)
    {
        var checks = new List<Dictionary<string, object?>>();
        var pass = 0;
        var total = 0;
        void Add(string name, bool ok, string detail)
        {
            checks.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["status"] = ok ? "pass" : "fail",
                ["detail"] = detail
            });
            total++;
            if (ok)
            {
                pass++;
            }
        }

        var tenant = db.Tenants.FirstOrDefault(t => t.SiteKey == siteKey);
        Add("Tenant record", tenant is not null, tenant is not null ? "Found" : "Not found in registry");
        if (tenant is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["tenant"] = siteKey, ["checks"] = checks, ["score"] = 0 };
        }

        var dbName = tenant.DbName;
        TenantDb? tenantDb = null;
        if (dbName != "")
        {
            try
            {
                var conn = TenantConnect(siteKey);
                tenantDb = conn.Pdo as TenantDb;
                if (tenantDb is { Connected: true })
                {
                    Add("DB connectivity", true, "Connected to " + dbName);
                }
                else
                {
                    Add("DB connectivity", false, "Connection failed");
                    tenantDb = null;
                }
            }
            catch (Exception ex)
            {
                Add("DB connectivity", false, ex.Message);
                tenantDb = null;
            }
        }
        else
        {
            Add("DB connectivity", false, "No db_name configured");
        }

        if (tenantDb is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tenant"] = siteKey,
                ["checks"] = checks,
                ["score"] = total > 0 ? (int)Math.Round(pass / (double)total * 100, MidpointRounding.AwayFromZero) : 0
            };
        }

        foreach (var table in CoreTables)
        {
            if (tenantDb.Tables.TryGetValue(table, out var count))
            {
                Add("Table: " + table, true, count + " rows");
            }
            else
            {
                Add("Table: " + table, false, "Missing or inaccessible");
            }
        }

        foreach (var table in ErpTables)
        {
            if (tenantDb.Tables.TryGetValue(table, out var count))
            {
                Add("ERP: " + table, true, count + " rows");
            }
            else
            {
                Add("ERP: " + table, false, "Not provisioned");
            }
        }

        if (tenantDb.Tables.ContainsKey("users"))
        {
            Add("Admin user", tenantDb.AdminUsers > 0, tenantDb.AdminUsers + " admin(s)");
        }
        else
        {
            Add("Admin user", false, "Cannot query users");
        }

        if (tenantDb.Tables.ContainsKey("lang_languages"))
        {
            Add("Languages", tenantDb.ActiveLanguages > 0, tenantDb.ActiveLanguages + " active language(s)");
        }
        else
        {
            Add("Languages", false, "Cannot query languages");
        }

        if (tenant.Hostname != "")
        {
            Add("Hostname configured", true, tenant.Hostname);
        }
        else
        {
            Add("Hostname configured", false, "No hostname set");
        }

        var score = total > 0 ? (int)Math.Round(pass / (double)total * 100, MidpointRounding.AwayFromZero) : 0;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["tenant"] = siteKey, ["checks"] = checks, ["score"] = score };
    }

    public static List<Dictionary<string, object?>> EpcBosHealthCheckAll(HealthStore db)
        => db.Tenants.Where(t => t.SiteKey != "" && t.IsActive == 1)
            .OrderBy(t => t.TradeName, StringComparer.Ordinal)
            .Select(t => EpcBosHealthCheckTenant(db, t.SiteKey))
            .ToList();

    public static Dictionary<string, object?> EpcBosHealthSummary(IEnumerable<Dictionary<string, object?>> results)
    {
        var list = results.ToList();
        var total = list.Count;
        var totalScore = 0;
        var critical = 0;
        var healthy = 0;
        foreach (var r in list)
        {
            var score = ToInt(r, "score");
            totalScore += score;
            if (score < 50)
            {
                critical++;
            }
            else if (score >= 80)
            {
                healthy++;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total_tenants"] = total,
            ["avg_score"] = total > 0 ? (int)Math.Round(totalScore / (double)total, MidpointRounding.AwayFromZero) : 0,
            ["critical"] = critical,
            ["healthy"] = healthy
        };
    }

    private static Dictionary<string, object?> DefaultAttrMap()
        => new(StringComparer.Ordinal)
        {
            ["email"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            ["first_name"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname",
            ["last_name"] = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname",
            ["role"] = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };

    private static byte[] Deflate(byte[] input)
    {
        using var output = new MemoryStream();
        using (var ds = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.SmallestSize, true))
        {
            ds.Write(input, 0, input.Length);
        }

        return output.ToArray();
    }

    private static string XmlH(string value)
        => (value ?? "")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string Str(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static string Str(IReadOnlyDictionary<string, object?> row, string key, string fallback = "")
        => row.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback
            : fallback;

    private static int ToInt(IReadOnlyDictionary<string, object?> row, string key, int fallback = 0)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static bool IsEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 or 0L or 0d => true,
            "" or "0" => true,
            _ => false
        };

    private static bool IsEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return value.Contains('@', StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string ByteSubstr(string value, int max)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");
        return bytes.Length <= max ? value ?? "" : Encoding.UTF8.GetString(bytes, 0, max);
    }
}
