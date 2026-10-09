using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-done helpers. PHP identifiers kept for the inventory:
/// <c>epc_php_serving_is_temporarily_deactivated</c>, <c>epc_php_serving_deactivated_maybe_exit</c>,
/// <c>epc_cp_trace_enabled</c>, <c>epc_cp_trace_logfile</c>, <c>epc_cp_trace</c>,
/// <c>epc_cpi_nav_links</c>, <c>epc_cpi_header_contact</c>, <c>epc_cpi_hero_slides</c>,
/// <c>epc_cpi_icon_boxes</c>, <c>epc_cpi_credentials</c>, <c>epc_cpi_about_image_url</c>,
/// <c>epc_cpi_services</c>, <c>epc_cpi_stats</c>, <c>epc_cpi_team</c>,
/// <c>epc_cpi_testimonials</c>, <c>epc_cpi_partners</c>, <c>epc_cpi_process_steps</c>,
/// <c>epc_cpi_footer_columns</c>, <c>epc_cpi_theme_palette</c>,
/// <c>epc_cpi_pro_hero_eyebrow</c>, <c>epc_cpi_pro_hero_title</c>, <c>epc_cpi_pro_hero_copy</c>,
/// <c>epc_cpi_pro_hero_actions</c>, <c>epc_cpi_pro_hero_stats</c>,
/// <c>epc_cpi_stamp_skus</c>, <c>epc_cpi_format_aed</c>, <c>epc_cpi_service_packages</c>,
/// <c>epc_ecomae_docs_catalog</c>, <c>epc_ecomae_compare_catalog</c>,
/// <c>epc_ecomae_bos_articles_catalog</c>, <c>epc_ecomae_solutions_catalog</c>,
/// <c>EPC_SOC2_VERSION</c>, <c>epc_soc2_ensure_schema</c>, <c>epc_soc2_builtin_controls</c>,
/// <c>epc_soc2_seed_controls</c>, <c>epc_soc2_list_controls</c>, <c>epc_soc2_update_control</c>,
/// <c>epc_soc2_add_evidence</c>, <c>epc_soc2_control_evidence</c>, <c>epc_soc2_gap_analysis</c>,
/// <c>epc_soc2_create_policy</c>, <c>epc_soc2_list_policies</c>, <c>epc_soc2_fleet_stats</c>,
/// <c>epc_soc2_remediation_plan</c>, <c>epc_soc2_evidence_summary</c>,
/// <c>epc_soc2_update_policy</c>, <c>epc_soc2_compliance_report</c>.
/// </summary>
public static class PhpPlanQ1Done
{
    public const string PhpServingDeactivatePath = "content/general_pages/epc_php_serving_deactivate.php";
    public const string CpTracePath = "content/general_pages/epc_cp_trace.php";
    public const string ConsultingDataPath = "content/general_pages/epc_consulting_primeinvest_data.php";
    public const string MarketingContentPath = "content/general_pages/epc_ecomae_marketing_content.php";
    public const string Soc2CompliancePath = "content/general_pages/epc_soc2_compliance.php";
    public const string EpcSoc2Version = "1.0.0";

    private static readonly Lazy<JsonElement> Cpi = new(() => JsonDocument.Parse(PhpPlanQ1DoneData.ConsultingJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Docs = new(() => JsonDocument.Parse(PhpPlanQ1DoneData.DocsJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Compare = new(() => JsonDocument.Parse(PhpPlanQ1DoneData.CompareJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Bos = new(() => JsonDocument.Parse(PhpPlanQ1DoneData.BosJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Solutions = new(() => JsonDocument.Parse(PhpPlanQ1DoneData.SolutionsJson).RootElement.Clone());

    private static readonly string[] UpdateControlFields = { "status", "implementation", "owner", "frequency", "last_tested", "next_review" };
    private static readonly string[] UpdatePolicyFields = { "title", "content", "owner", "status" };
    private static readonly Dictionary<string, int> RemediationPriority = new(StringComparer.Ordinal)
    {
        ["critical"] = 1,
        ["high"] = 2,
        ["medium"] = 3,
        ["low"] = 4
    };

    public sealed class DeactivateCtx
    {
        public string DocumentRoot { get; set; } = "";
        public bool EtcFlag { get; set; }
        public string Sapi { get; set; } = "cli";
        public bool HeadersSent { get; set; }
    }

    public sealed class TraceCtx
    {
        public Dictionary<string, string> Get { get; } = new(StringComparer.Ordinal);
        public string TempDir { get; set; } = Path.GetTempPath();
        public string RequestUri { get; set; } = "";
        public double RequestTimeFloat { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        public int Pid { get; set; } = Environment.ProcessId;
        public Func<DateTime>? Clock { get; set; }
    }

    public sealed class Soc2Control
    {
        public int Id { get; set; }
        public string ControlId { get; set; } = "";
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Implementation { get; set; } = "";
        public string Status { get; set; } = "not_started";
        public string Owner { get; set; } = "";
        public string Frequency { get; set; } = "annual";
        public string? LastTested { get; set; }
        public string? NextReview { get; set; }
        public string RiskLevel { get; set; } = "medium";
    }

    public sealed class Soc2Evidence
    {
        public int Id { get; set; }
        public string ControlId { get; set; } = "";
        public string EvidenceType { get; set; } = "document";
        public string Title { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string CollectedAt { get; set; } = "";
        public string CollectedBy { get; set; } = "";
        public string? ValidFrom { get; set; }
        public string? ValidTo { get; set; }
        public string Notes { get; set; } = "";
    }

    public sealed class Soc2Policy
    {
        public int Id { get; set; }
        public string PolicyCode { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Version { get; set; } = "1.0";
        public string Status { get; set; } = "draft";
        public string Owner { get; set; } = "";
        public object? RelatedControls { get; set; }
    }

    public sealed class Soc2Store
    {
        public bool SchemaReady { get; set; }
        public List<Soc2Control> Controls { get; } = new();
        public List<Soc2Evidence> Evidence { get; } = new();
        public List<Soc2Policy> Policies { get; } = new();
        public int NextControlId { get; set; } = 1;
        public int NextEvidenceId { get; set; } = 1;
        public int NextPolicyId { get; set; } = 1;
    }

    public static bool EpcPhpServingIsTemporarilyDeactivated(DeactivateCtx ctx)
    {
        var root = (ctx.DocumentRoot ?? "").TrimEnd('/');
        if (root.Length > 0 && File.Exists(root + "/.epc_php_serving_deactivated"))
        {
            return true;
        }

        return ctx.EtcFlag;
    }

    public static bool EpcPhpServingDeactivatedMaybeExit(DeactivateCtx ctx, string reason = "php-http-serving")
    {
        if (!EpcPhpServingIsTemporarilyDeactivated(ctx))
        {
            return false;
        }

        if (ctx.Sapi == "cli" || ctx.HeadersSent)
        {
            return false;
        }

        return true;
    }

    public static bool EpcCpTraceEnabled(TraceCtx ctx)
        => ctx.Get.ContainsKey("epc_trace") || File.Exists(TraceFlag(ctx));

    public static string EpcCpTraceLogfile(TraceCtx ctx)
        => ctx.TempDir.TrimEnd('/') + "/epc_cp_trace.log";

    public static void EpcCpTrace(TraceCtx ctx, string label)
    {
        if (!EpcCpTraceEnabled(ctx))
        {
            return;
        }

        var now = ctx.Clock?.Invoke() ?? DateTime.UtcNow;
        var elapsed = (int)Math.Round((now.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds * 1000
            - ctx.RequestTimeFloat * 1000);
        var line = string.Format(
            CultureInfo.InvariantCulture,
            "{0} pid={1} +{2}ms {3} {4}\n",
            now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            ctx.Pid,
            Math.Max(0, elapsed),
            label,
            ctx.RequestUri ?? "");
        File.AppendAllText(EpcCpTraceLogfile(ctx), line);
    }

    public static JsonElement EpcCpiNavLinks() => Cpi.Value.GetProperty("nav").Clone();
    public static JsonElement EpcCpiHeaderContact() => Cpi.Value.GetProperty("contact").Clone();
    public static JsonElement EpcCpiHeroSlides() => Cpi.Value.GetProperty("slides").Clone();
    public static JsonElement EpcCpiIconBoxes() => Cpi.Value.GetProperty("icons").Clone();
    public static JsonElement EpcCpiCredentials() => Cpi.Value.GetProperty("credentials").Clone();
    public static string EpcCpiAboutImageUrl() => Cpi.Value.GetProperty("about_image").GetString() ?? "";
    public static JsonElement EpcCpiServices() => Cpi.Value.GetProperty("services").Clone();
    public static JsonElement EpcCpiStats() => Cpi.Value.GetProperty("stats").Clone();
    public static JsonElement EpcCpiTeam() => Cpi.Value.GetProperty("team").Clone();
    public static JsonElement EpcCpiTestimonials() => Cpi.Value.GetProperty("testimonials").Clone();
    public static JsonElement EpcCpiPartners() => Cpi.Value.GetProperty("partners").Clone();
    public static JsonElement EpcCpiProcessSteps() => Cpi.Value.GetProperty("steps").Clone();
    public static JsonElement EpcCpiFooterColumns() => Cpi.Value.GetProperty("footer").Clone();
    public static JsonElement EpcCpiThemePalette() => Cpi.Value.GetProperty("palette").Clone();
    public static string EpcCpiProHeroEyebrow() => Cpi.Value.GetProperty("eyebrow").GetString() ?? "";
    public static string EpcCpiProHeroTitle() => Cpi.Value.GetProperty("title").GetString() ?? "";
    public static string EpcCpiProHeroCopy() => Cpi.Value.GetProperty("copy").GetString() ?? "";
    public static JsonElement EpcCpiProHeroActions(string lang) => Cpi.Value.GetProperty("actions").Clone();
    public static JsonElement EpcCpiProHeroStats() => Cpi.Value.GetProperty("hero_stats").Clone();
    public static JsonElement EpcCpiServicePackages() => Cpi.Value.GetProperty("packages").Clone();

    public static List<Dictionary<string, object?>> EpcCpiStampSkus(IEnumerable<Dictionary<string, object?>> items, string prefix = "TF")
    {
        var n = 1001;
        var outItems = new List<Dictionary<string, object?>>();
        foreach (var item in items)
        {
            var copy = new Dictionary<string, object?>(item, StringComparer.Ordinal);
            if (IsPhpEmpty(copy.TryGetValue("sku", out var sku) ? sku : null))
            {
                copy["sku"] = prefix + "-" + n++;
            }

            outItems.Add(copy);
        }

        return outItems;
    }

    public static string EpcCpiFormatAed(object? amount)
    {
        var value = amount is null ? 0d : Convert.ToDouble(amount, CultureInfo.InvariantCulture);
        if (value <= 0)
        {
            return "";
        }

        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return "AED " + rounded.ToString("N0", CultureInfo.GetCultureInfo("en-US"));
    }

    public static JsonElement EpcEcomaeDocsCatalog() => Docs.Value.Clone();
    public static JsonElement EpcEcomaeCompareCatalog() => Compare.Value.Clone();
    public static JsonElement EpcEcomaeBosArticlesCatalog() => Bos.Value.Clone();
    public static JsonElement EpcEcomaeSolutionsCatalog() => Solutions.Value.Clone();

    public static void EpcSoc2EnsureSchema(Soc2Store store) => store.SchemaReady = true;

    public static List<Dictionary<string, string>> EpcSoc2BuiltinControls()
        => new()
        {
            C("CC1.1", "security", "Security governance & oversight", "high"),
            C("CC1.2", "security", "Board and management accountability", "high"),
            C("CC2.1", "security", "Information and communication policies", "medium"),
            C("CC3.1", "security", "Risk assessment process", "high"),
            C("CC4.1", "security", "Monitoring of controls", "high"),
            C("CC5.1", "security", "Logical access controls", "critical"),
            C("CC5.2", "security", "Authentication mechanisms (MFA)", "critical"),
            C("CC5.3", "security", "Access provisioning and deprovisioning", "high"),
            C("CC6.1", "security", "Encryption at rest and in transit", "critical"),
            C("CC6.2", "security", "Network security and firewall", "critical"),
            C("CC7.1", "security", "Incident detection and response", "high"),
            C("CC7.2", "security", "Incident communication", "medium"),
            C("CC8.1", "security", "Change management process", "high"),
            C("CC9.1", "security", "Vendor and subservice management", "medium"),
            C("A1.1", "availability", "System availability monitoring", "high"),
            C("A1.2", "availability", "Disaster recovery and BCP", "critical"),
            C("PI1.1", "processing_integrity", "Data validation and completeness", "high"),
            C("C1.1", "confidentiality", "Data classification and handling", "high"),
            C("C1.2", "confidentiality", "Tenant data isolation", "critical"),
            C("P1.1", "privacy", "Privacy notice and consent", "medium"),
            C("P1.2", "privacy", "Data retention and deletion", "medium")
        };

    public static int EpcSoc2SeedControls(Soc2Store store)
    {
        EpcSoc2EnsureSchema(store);
        var inserted = 0;
        foreach (var c in EpcSoc2BuiltinControls())
        {
            if (store.Controls.Any(x => x.ControlId == c["control_id"]))
            {
                continue;
            }

            store.Controls.Add(new Soc2Control
            {
                Id = store.NextControlId++,
                ControlId = c["control_id"],
                Category = c["category"],
                Title = c["title"],
                RiskLevel = c["risk_level"]
            });
            inserted++;
        }

        return inserted;
    }

    public static List<Soc2Control> EpcSoc2ListControls(Soc2Store store, string category = "")
    {
        EpcSoc2EnsureSchema(store);
        IEnumerable<Soc2Control> q = store.Controls;
        if (category != "")
        {
            q = q.Where(c => c.Category == category);
        }

        return q.OrderBy(c => c.ControlId, StringComparer.Ordinal).ToList();
    }

    public static Dictionary<string, object?> EpcSoc2UpdateControl(Soc2Store store, string controlId, Dictionary<string, object?> data)
    {
        var fields = UpdateControlFields.Where(data.ContainsKey).ToList();
        if (fields.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "No fields to update" };
        }

        var row = store.Controls.FirstOrDefault(c => c.ControlId == controlId);
        if (row is not null)
        {
            foreach (var f in fields)
            {
                var value = Convert.ToString(data[f], CultureInfo.InvariantCulture) ?? "";
                switch (f)
                {
                    case "status": row.Status = value; break;
                    case "implementation": row.Implementation = value; break;
                    case "owner": row.Owner = value; break;
                    case "frequency": row.Frequency = value; break;
                    case "last_tested": row.LastTested = value; break;
                    case "next_review": row.NextReview = value; break;
                }
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    public static Dictionary<string, object?> EpcSoc2AddEvidence(Soc2Store store, string controlId, Dictionary<string, object?> data)
    {
        var id = store.NextEvidenceId++;
        store.Evidence.Add(new Soc2Evidence
        {
            Id = id,
            ControlId = controlId,
            EvidenceType = Convert.ToString(data.TryGetValue("evidence_type", out var t) ? t : "document", CultureInfo.InvariantCulture) ?? "document",
            Title = Convert.ToString(data.TryGetValue("title", out var title) ? title : "", CultureInfo.InvariantCulture) ?? "",
            FilePath = Convert.ToString(data.TryGetValue("file_path", out var path) ? path : "", CultureInfo.InvariantCulture) ?? "",
            CollectedBy = Convert.ToString(data.TryGetValue("collected_by", out var by) ? by : "", CultureInfo.InvariantCulture) ?? "",
            ValidFrom = data.TryGetValue("valid_from", out var vf) ? Convert.ToString(vf, CultureInfo.InvariantCulture) : null,
            ValidTo = data.TryGetValue("valid_to", out var vt) ? Convert.ToString(vt, CultureInfo.InvariantCulture) : null,
            Notes = Convert.ToString(data.TryGetValue("notes", out var notes) ? notes : "", CultureInfo.InvariantCulture) ?? "",
            CollectedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["evidence_id"] = id };
    }

    public static List<Soc2Evidence> EpcSoc2ControlEvidence(Soc2Store store, string controlId)
        => store.Evidence.Where(e => e.ControlId == controlId).OrderByDescending(e => e.CollectedAt).ToList();

    public static Dictionary<string, object?> EpcSoc2GapAnalysis(Soc2Store store)
    {
        EpcSoc2EnsureSchema(store);
        var controls = EpcSoc2ListControls(store);
        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["not_started"] = 0,
            ["in_progress"] = 0,
            ["implemented"] = 0,
            ["tested"] = 0,
            ["effective"] = 0
        };
        var gaps = new List<Dictionary<string, object?>>();
        foreach (var c in controls)
        {
            byStatus[c.Status] = (byStatus.TryGetValue(c.Status, out var n) ? n : 0) + 1;
            if (c.Status is "not_started" or "in_progress")
            {
                gaps.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["control_id"] = c.ControlId,
                    ["title"] = c.Title,
                    ["risk_level"] = c.RiskLevel,
                    ["status"] = c.Status
                });
            }
        }

        var readiness = controls.Count > 0
            ? Math.Round((byStatus["effective"] + byStatus["tested"]) / (double)controls.Count * 100, 1, MidpointRounding.AwayFromZero)
            : 0d;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total_controls"] = controls.Count,
            ["by_status"] = byStatus,
            ["gaps"] = gaps,
            ["readiness_pct"] = readiness
        };
    }

    public static Dictionary<string, object?> EpcSoc2CreatePolicy(Soc2Store store, Dictionary<string, object?> data)
    {
        EpcSoc2EnsureSchema(store);
        var id = store.NextPolicyId++;
        store.Policies.Add(new Soc2Policy
        {
            Id = id,
            PolicyCode = (Convert.ToString(data.TryGetValue("policy_code", out var code) ? code : "", CultureInfo.InvariantCulture) ?? "").ToUpperInvariant(),
            Title = Convert.ToString(data.TryGetValue("title", out var title) ? title : "", CultureInfo.InvariantCulture) ?? "",
            Content = Convert.ToString(data.TryGetValue("content", out var content) ? content : "", CultureInfo.InvariantCulture) ?? "",
            Owner = Convert.ToString(data.TryGetValue("owner", out var owner) ? owner : "", CultureInfo.InvariantCulture) ?? "",
            RelatedControls = data.TryGetValue("related_controls", out var rel) ? rel : Array.Empty<object>()
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["policy_id"] = id };
    }

    public static List<Soc2Policy> EpcSoc2ListPolicies(Soc2Store store)
    {
        EpcSoc2EnsureSchema(store);
        return store.Policies.OrderBy(p => p.PolicyCode, StringComparer.Ordinal).ToList();
    }

    public static Dictionary<string, object?> EpcSoc2FleetStats(Soc2Store store)
    {
        var gap = EpcSoc2GapAnalysis(store);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["controls"] = gap["total_controls"],
            ["readiness_pct"] = gap["readiness_pct"],
            ["gaps"] = ((List<Dictionary<string, object?>>)gap["gaps"]!).Count,
            ["policies"] = store.Policies.Count,
            ["evidence_items"] = store.Evidence.Count
        };
    }

    public static List<Dictionary<string, object?>> EpcSoc2RemediationPlan(Soc2Store store)
    {
        var gap = EpcSoc2GapAnalysis(store);
        var plan = new List<Dictionary<string, object?>>();
        foreach (var g in (List<Dictionary<string, object?>>)gap["gaps"]!)
        {
            var risk = Convert.ToString(g["risk_level"], CultureInfo.InvariantCulture) ?? "";
            var status = Convert.ToString(g["status"], CultureInfo.InvariantCulture) ?? "";
            plan.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["control_id"] = g["control_id"],
                ["title"] = g["title"],
                ["risk_level"] = g["risk_level"],
                ["priority"] = RemediationPriority.TryGetValue(risk, out var p) ? p : 5,
                ["status"] = status,
                ["action"] = status == "not_started" ? "Implement control" : "Complete implementation"
            });
        }

        return plan.OrderBy(x => Convert.ToInt32(x["priority"], CultureInfo.InvariantCulture)).ToList();
    }

    public static List<Dictionary<string, object?>> EpcSoc2EvidenceSummary(Soc2Store store)
        => store.Evidence
            .GroupBy(e => e.ControlId)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["control_id"] = g.Key,
                ["evidence_count"] = g.Count(),
                ["last_collected"] = g.Max(x => x.CollectedAt)
            })
            .ToList();

    public static Dictionary<string, object?> EpcSoc2UpdatePolicy(Soc2Store store, int policyId, Dictionary<string, object?> data)
    {
        var fields = UpdatePolicyFields.Where(data.ContainsKey).ToList();
        if (fields.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false };
        }

        var row = store.Policies.FirstOrDefault(p => p.Id == policyId);
        if (row is not null)
        {
            foreach (var f in fields)
            {
                var value = Convert.ToString(data[f], CultureInfo.InvariantCulture) ?? "";
                switch (f)
                {
                    case "title": row.Title = value; break;
                    case "content": row.Content = value; break;
                    case "owner": row.Owner = value; break;
                    case "status": row.Status = value; break;
                }
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    public static Dictionary<string, object?> EpcSoc2ComplianceReport(Soc2Store store)
    {
        var gap = EpcSoc2GapAnalysis(store);
        var evidence = EpcSoc2EvidenceSummary(store);
        var policies = EpcSoc2ListPolicies(store);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["readiness"] = gap["readiness_pct"],
            ["controls_total"] = gap["total_controls"],
            ["controls_by_status"] = gap["by_status"],
            ["outstanding_gaps"] = ((List<Dictionary<string, object?>>)gap["gaps"]!).Count,
            ["evidence_items"] = evidence.Count,
            ["policies"] = policies.Count,
            ["report_type"] = "SOC 2 Type II Readiness Assessment"
        };
    }

    private static string TraceFlag(TraceCtx ctx) => ctx.TempDir.TrimEnd('/') + "/epc_cp_trace.on";

    private static Dictionary<string, string> C(string id, string category, string title, string risk)
        => new(StringComparer.Ordinal)
        {
            ["control_id"] = id,
            ["category"] = category,
            ["title"] = title,
            ["risk_level"] = risk
        };

    private static bool IsPhpEmpty(object? value)
        => value is null
            || (value is string s && (s.Length == 0 || s == "0"))
            || value is int i && i == 0
            || value is false;
}
