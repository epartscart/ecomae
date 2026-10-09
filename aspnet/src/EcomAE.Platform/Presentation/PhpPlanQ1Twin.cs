using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-twin helpers. PHP identifiers kept for the inventory:
/// <c>EPC_IMPORT_ORCHESTRATOR_VERSION</c>, <c>epc_import_ensure_schema</c>,
/// <c>epc_import_entity_schemas</c>, <c>epc_import_create_job</c>,
/// <c>epc_import_validate_row</c>, <c>epc_import_process_chunk</c>,
/// <c>epc_import_job_status</c>, <c>epc_import_job_errors</c>,
/// <c>epc_import_list_jobs</c>, <c>epc_import_fleet_stats</c>,
/// <c>epc_import_dry_run</c>, <c>epc_import_validators</c>,
/// <c>epc_import_supported_formats</c>, <c>epc_import_supported_sources</c>,
/// <c>epc_import_cancel</c>, <c>epc_import_retry</c>,
/// <c>EPC_DOCUMENT_VAULT_VERSION</c>, <c>epc_vault_ensure_schema</c>,
/// <c>epc_vault_create_folder</c>, <c>epc_vault_list_folders</c>,
/// <c>epc_vault_upload</c>, <c>epc_vault_new_version</c>,
/// <c>epc_vault_list_documents</c>, <c>epc_vault_versions</c>,
/// <c>epc_vault_fleet_stats</c>, <c>epc_vault_gdpr_ensure_schema</c>,
/// <c>epc_vault_gdpr_request</c>, <c>epc_vault_gdpr_process</c>,
/// <c>epc_vault_gdpr_list</c>, <c>epc_vault_search</c>,
/// <c>epc_vault_delete</c>, <c>epc_vault_restore</c>,
/// <c>epc_onprem_license_ensure_schema</c>, <c>epc_onprem_license_generate</c>,
/// <c>epc_onprem_license_list</c>, <c>epc_onprem_license_revoke</c>,
/// <c>epc_onprem_license_fetch</c>, <c>epc_onprem_license_signing_key_path</c>,
/// <c>epc_onprem_license_sign</c>, <c>epc_onprem_core_bundle</c>,
/// <c>epc_onprem_license_activate</c>, <c>epc_onprem_health_log</c>,
/// <c>EPC_BI_METRICS_VERSION</c>, <c>epc_bi_ensure_schema</c>,
/// <c>epc_bi_builtin_metrics</c>, <c>epc_bi_record_snapshot</c>,
/// <c>epc_bi_metric_trend</c>, <c>epc_bi_latest_all</c>,
/// <c>epc_bi_compute_daily</c>, <c>epc_bi_dashboard</c>,
/// <c>epc_bi_fleet_overview</c>, <c>epc_bi_compare_tenants</c>,
/// <c>epc_bi_cleanup</c>,
/// <c>EPC_NOTIFICATIONS_VERSION</c>, <c>epc_notifications_ensure_schema</c>,
/// <c>epc_notification_categories</c>, <c>epc_notification_send</c>,
/// <c>epc_notification_broadcast</c>, <c>epc_notifications_list</c>,
/// <c>epc_notifications_unread_count</c>, <c>epc_notifications_mark_read</c>,
/// <c>epc_notifications_mark_all_read</c>, <c>epc_notifications_dismiss</c>,
/// <c>epc_notifications_summary</c>, <c>epc_notification_prefs_get</c>,
/// <c>epc_notification_prefs_save</c>, <c>epc_notifications_fleet_stats</c>,
/// <c>epc_notifications_cleanup</c>, <c>epc_notifications_pending_digest</c>.
/// </summary>
public static class PhpPlanQ1Twin
{
    public const string ImportOrchestratorPath = "content/general_pages/epc_import_orchestrator.php";
    public const string DocumentVaultPath = "content/general_pages/epc_document_vault.php";
    public const string OnpremLicensesPath = "content/general_pages/epc_onprem_licenses.php";
    public const string BiMetricsPath = "content/general_pages/epc_bi_metrics.php";
    public const string NotificationsPath = "content/general_pages/epc_notifications.php";
    public const string EpcImportOrchestratorVersion = "1.0.0";
    public const string EpcDocumentVaultVersion = "1.0.0";
    public const string EpcBiMetricsVersion = "1.0.0";
    public const string EpcNotificationsVersion = "1.0.0";

    private static readonly string[] LicenseTiers = { "standard", "professional", "enterprise" };
    private static readonly string[] NotifySeverities = { "info", "warning", "error", "success" };
    private static readonly Regex LicenseKeyRe = new(@"^LIC-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PhoneRe = new(@"^[\+\d\s\-\(\)]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed class ImportJob
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string SourceFormat { get; set; } = "csv";
        public string Filename { get; set; } = "";
        public int TotalRows { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessRows { get; set; }
        public int ErrorRows { get; set; }
        public int SkipRows { get; set; }
        public string FieldMappingJson { get; set; } = "[]";
        public string OptionsJson { get; set; } = "[]";
        public string Status { get; set; } = "pending";
        public int DryRun { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedAt { get; set; } = "";
        public string? CompletedAt { get; set; }
    }

    public sealed class ImportError
    {
        public long Id { get; set; }
        public int JobId { get; set; }
        public int RowNumber { get; set; }
        public string Field { get; set; } = "";
        public string Value { get; set; } = "";
        public string ErrorType { get; set; } = "unknown";
        public string Message { get; set; } = "";
    }

    public sealed class ImportStore
    {
        public bool SchemaReady { get; set; }
        public List<ImportJob> Jobs { get; } = new();
        public List<ImportError> Errors { get; } = new();
        public int NextJobId { get; set; } = 1;
        public long NextErrorId { get; set; } = 1;
        public Func<DateTime>? Clock { get; set; }
        public DateTime Now() => Clock?.Invoke() ?? DateTime.UtcNow;
    }

    public sealed class VaultFolder
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public int? ParentId { get; set; }
        public string Name { get; set; } = "";
        public string Path { get; set; } = "/";
        public int CreatedBy { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class VaultDocument
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public int? FolderId { get; set; }
        public string Filename { get; set; } = "";
        public string MimeType { get; set; } = "application/octet-stream";
        public long FileSize { get; set; }
        public int CurrentVersion { get; set; } = 1;
        public string TagsJson { get; set; } = "[]";
        public string AccessLevel { get; set; } = "tenant";
        public int RetentionDays { get; set; }
        public string Status { get; set; } = "active";
        public int UploadedBy { get; set; }
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
    }

    public sealed class VaultVersion
    {
        public long Id { get; set; }
        public int DocumentId { get; set; }
        public int VersionNumber { get; set; } = 1;
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public string Checksum { get; set; } = "";
        public string ChangeNote { get; set; } = "";
        public int UploadedBy { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class VaultGdprRequest
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string RequestType { get; set; } = "";
        public string SubjectEmail { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string Status { get; set; } = "pending";
        public int DocumentsFound { get; set; }
        public string? ResponseJson { get; set; }
        public int? ProcessedBy { get; set; }
        public string? CompletedAt { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class VaultStore
    {
        public bool SchemaReady { get; set; }
        public bool GdprSchemaReady { get; set; }
        public List<VaultFolder> Folders { get; } = new();
        public List<VaultDocument> Documents { get; } = new();
        public List<VaultVersion> Versions { get; } = new();
        public List<VaultGdprRequest> Gdpr { get; } = new();
        public int NextFolderId { get; set; } = 1;
        public int NextDocId { get; set; } = 1;
        public long NextVersionId { get; set; } = 1;
        public int NextGdprId { get; set; } = 1;
        public Func<DateTime>? Clock { get; set; }
        public DateTime Now() => Clock?.Invoke() ?? DateTime.UtcNow;
    }

    public sealed class OnpremLicense
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Tier { get; set; } = "standard";
        public string ModulesJson { get; set; } = "[\"all\"]";
        public int UsersMax { get; set; } = 25;
        public string Status { get; set; } = "issued";
        public string? Fingerprint { get; set; }
        public string Hostname { get; set; } = "";
        public string Ip { get; set; } = "";
        public long IssuedAt { get; set; }
        public long? ActivatedAt { get; set; }
        public long? LastSeenAt { get; set; }
        public long? ExpiresAt { get; set; }
        public string Notes { get; set; } = "";
    }

    public sealed class OnpremHealth
    {
        public long Id { get; set; }
        public string LicenseKey { get; set; } = "";
        public string Status { get; set; } = "";
        public string Uptime { get; set; } = "";
        public double DiskFreeGb { get; set; }
        public double MemoryUsageMb { get; set; }
        public string PhpVersion { get; set; } = "";
        public double DbSizeMb { get; set; }
        public string LastBackup { get; set; } = "";
        public long ReportedAt { get; set; }
    }

    public sealed class LicenseStore
    {
        public bool SchemaReady { get; set; }
        public List<OnpremLicense> Licenses { get; } = new();
        public List<OnpremHealth> Health { get; } = new();
        public int NextLicenseId { get; set; } = 1;
        public long NextHealthId { get; set; } = 1;
        public string? SigningKeyPath { get; set; }
        public string DocumentRoot { get; set; } = "";
        public Func<long>? Clock { get; set; }
        public long Now() => Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public sealed class BiSnapshot
    {
        public long Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string MetricKey { get; set; } = "";
        public string Period { get; set; } = "";
        public string PeriodStart { get; set; } = "";
        public double Value { get; set; }
        public double PreviousValue { get; set; }
        public double ChangePct { get; set; }
        public string ComputedAt { get; set; } = "";
    }

    public sealed class BiStore
    {
        public bool SchemaReady { get; set; }
        public List<BiSnapshot> Snapshots { get; } = new();
        public long NextId { get; set; } = 1;
        public Func<DateTime>? Clock { get; set; }
        public DateTime Now() => Clock?.Invoke() ?? DateTime.UtcNow;
    }

    public sealed class NotificationRow
    {
        public long Id { get; set; }
        public string TenantKey { get; set; } = "__platform__";
        public int UserId { get; set; }
        public string Channel { get; set; } = "in_app";
        public string Category { get; set; } = "system";
        public string Severity { get; set; } = "info";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string ActionUrl { get; set; } = "";
        public string ActionLabel { get; set; } = "";
        public int IsRead { get; set; }
        public string? ReadAt { get; set; }
        public int Dismissed { get; set; }
        public string? Metadata { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class NotificationPref
    {
        public int Id { get; set; }
        public string TenantKey { get; set; } = "__platform__";
        public int UserId { get; set; }
        public string Category { get; set; } = "*";
        public int ChannelInApp { get; set; } = 1;
        public int ChannelEmail { get; set; } = 1;
        public int ChannelWebhook { get; set; }
        public string EmailDigest { get; set; } = "daily";
    }

    public sealed class NotifyStore
    {
        public bool SchemaReady { get; set; }
        public List<NotificationRow> Rows { get; } = new();
        public List<NotificationPref> Prefs { get; } = new();
        public long NextId { get; set; } = 1;
        public int NextPrefId { get; set; } = 1;
        public Func<DateTime>? Clock { get; set; }
        public DateTime Now() => Clock?.Invoke() ?? DateTime.UtcNow;
    }

    public static void EpcImportEnsureSchema(ImportStore store) => store.SchemaReady = true;

    public static Dictionary<string, Dictionary<string, object?>> EpcImportEntitySchemas()
        => new(StringComparer.Ordinal)
        {
            ["products"] = Schema(new[] { "sku", "product_name" }, new[] { "price", "stock_qty", "category", "brand", "weight", "description", "image_url", "barcode" }, "sku"),
            ["customers"] = Schema(new[] { "email" }, new[] { "first_name", "last_name", "phone", "company", "address", "city", "country", "tax_id" }, "email"),
            ["orders"] = Schema(new[] { "order_ref", "customer_email", "total" }, new[] { "status", "currency", "shipping_address", "items_json" }, "order_ref"),
            ["inventory"] = Schema(new[] { "sku", "stock_qty" }, new[] { "warehouse", "location", "reorder_point", "cost_price" }, "sku"),
            ["gl_entries"] = Schema(new[] { "account_code", "debit", "credit" }, new[] { "description", "reference", "date", "currency" }, "")
        };

    public static List<Dictionary<string, string>> EpcImportSupportedFormats()
        => new()
        {
            Fmt("csv", "CSV (comma-separated)", "text/csv"),
            Fmt("tsv", "TSV (tab-separated)", "text/tab-separated-values"),
            Fmt("xlsx", "Excel (.xlsx)", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            Fmt("xml", "XML", "application/xml"),
            Fmt("json", "JSON", "application/json")
        };

    public static Dictionary<string, Dictionary<string, object?>> EpcImportSupportedSources()
        => new(StringComparer.Ordinal)
        {
            ["products"] = Src("Products / SKUs", "shop_products", "sku", "name"),
            ["customers"] = Src("Customers", "shop_customers", "name"),
            ["invoices"] = Src("Invoices", "epc_invoices", "invoice_no", "amount"),
            ["suppliers"] = Src("Suppliers", "epc_suppliers", "name"),
            ["gl_entries"] = Src("Journal Entries", "epc_gl_entries", "account_code", "amount"),
            ["employees"] = Src("Employees", "epc_employees", "name", "employee_id")
        };

    public static Dictionary<string, object?> EpcImportCreateJob(ImportStore store, string siteKey, Dictionary<string, object?> data)
    {
        EpcImportEnsureSchema(store);
        var entityType = Str(data, "entity_type", "products");
        var schemas = EpcImportEntitySchemas();
        if (!schemas.ContainsKey(entityType))
        {
            return Ok(false, "error", "Unknown entity type: " + entityType);
        }

        var mapping = data.TryGetValue("field_mapping", out var map) ? map : Array.Empty<object>();
        var options = data.TryGetValue("options", out var opt) ? opt : Array.Empty<object>();
        var job = new ImportJob
        {
            Id = store.NextJobId++,
            SiteKey = siteKey,
            EntityType = entityType,
            SourceFormat = Str(data, "source_format", "csv"),
            Filename = Str(data, "filename"),
            TotalRows = IntOf(data, "total_rows"),
            FieldMappingJson = JsonEncode(mapping),
            OptionsJson = JsonEncode(options),
            DryRun = IntOf(data, "dry_run"),
            CreatedBy = IntOf(data, "created_by"),
            CreatedAt = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        store.Jobs.Add(job);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["job_id"] = job.Id,
            ["schema"] = schemas[entityType]
        };
    }

    public static List<Dictionary<string, object?>> EpcImportValidateRow(Dictionary<string, object?> row, Dictionary<string, object?> schema, int rowNum)
    {
        var errors = new List<Dictionary<string, object?>>();
        var required = (string[])schema["required"]!;
        foreach (var field in required)
        {
            if (!row.TryGetValue(field, out var raw) || Trim(raw) == "")
            {
                errors.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["row_number"] = rowNum,
                    ["field"] = field,
                    ["error_type"] = "required",
                    ["message"] = "Required field missing: " + field
                });
            }
        }

        if (row.TryGetValue("email", out var email) && Str(email) != "" && !PhpEmailOk(Str(email)))
        {
            errors.Add(FormatErr(rowNum, "email", Str(email), "Invalid email format"));
        }

        if (row.TryGetValue("price", out var price) && Str(price) != "" && !PhpIsNumeric(price))
        {
            errors.Add(FormatErr(rowNum, "price", Str(price), "Price must be numeric"));
        }

        return errors;
    }

    public static Dictionary<string, object?> EpcImportProcessChunk(ImportStore store, int jobId, IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var job = store.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is null)
        {
            return Ok(false, "error", "Job not found");
        }

        var schemas = EpcImportEntitySchemas();
        var schema = schemas.TryGetValue(job.EntityType, out var sc)
            ? sc
            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["required"] = Array.Empty<string>(), ["optional"] = Array.Empty<string>() };
        var success = 0;
        var errors = 0;
        var skipped = 0;
        job.Status = "importing";
        for (var i = 0; i < rows.Count; i++)
        {
            var rowErrors = EpcImportValidateRow(rows[i], schema, i + 1);
            if (rowErrors.Count > 0)
            {
                foreach (var e in rowErrors)
                {
                    store.Errors.Add(new ImportError
                    {
                        Id = store.NextErrorId++,
                        JobId = jobId,
                        RowNumber = Convert.ToInt32(e["row_number"], CultureInfo.InvariantCulture),
                        Field = Str(e["field"]),
                        Value = e.TryGetValue("value", out var v) ? Str(v) : "",
                        ErrorType = Str(e["error_type"]),
                        Message = Str(e["message"])
                    });
                }

                errors++;
            }
            else
            {
                success++;
            }
        }

        job.ProcessedRows += rows.Count;
        job.SuccessRows += success;
        job.ErrorRows += errors;
        job.SkipRows += skipped;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["processed"] = rows.Count,
            ["success"] = success,
            ["errors"] = errors,
            ["skipped"] = skipped
        };
    }

    public static object EpcImportJobStatus(ImportStore store, int jobId)
    {
        var job = store.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is null)
        {
            return new List<object>();
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = job.Id,
            ["site_key"] = job.SiteKey,
            ["entity_type"] = job.EntityType,
            ["source_format"] = job.SourceFormat,
            ["filename"] = job.Filename,
            ["total_rows"] = job.TotalRows,
            ["processed_rows"] = job.ProcessedRows,
            ["success_rows"] = job.SuccessRows,
            ["error_rows"] = job.ErrorRows,
            ["skip_rows"] = job.SkipRows,
            ["field_mapping"] = JsonDecodeFlexible(job.FieldMappingJson),
            ["options"] = JsonDecodeFlexible(job.OptionsJson),
            ["status"] = job.Status,
            ["dry_run"] = job.DryRun,
            ["created_by"] = job.CreatedBy,
            ["created_at"] = job.CreatedAt,
            ["completed_at"] = job.CompletedAt
        };
    }

    public static List<ImportError> EpcImportJobErrors(ImportStore store, int jobId)
        => store.Errors.Where(e => e.JobId == jobId).OrderBy(e => e.RowNumber).ToList();

    public static List<ImportJob> EpcImportListJobs(ImportStore store, string siteKey)
    {
        EpcImportEnsureSchema(store);
        return store.Jobs.Where(j => j.SiteKey == siteKey).OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).ToList();
    }

    public static List<Dictionary<string, object?>> EpcImportFleetStats(ImportStore store)
    {
        EpcImportEnsureSchema(store);
        return store.Jobs
            .GroupBy(j => j.SiteKey)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = g.Key,
                ["jobs"] = g.Count(),
                ["imported"] = g.Sum(x => x.SuccessRows),
                ["errors"] = g.Sum(x => x.ErrorRows)
            })
            .ToList();
    }

    public static Dictionary<string, object?> EpcImportDryRun(ImportStore store, string siteKey, string source, Dictionary<string, string> mapping, string format, IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var results = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = rows.Count,
            ["valid"] = 0,
            ["invalid"] = 0,
            ["errors"] = new List<Dictionary<string, object?>>()
        };
        var validators = EpcImportValidators(source);
        var errList = (List<Dictionary<string, object?>>)results["errors"]!;
        var valid = 0;
        var invalid = 0;
        for (var idx = 0; idx < rows.Count; idx++)
        {
            var row = rows[idx];
            var rowErrors = new List<Dictionary<string, object?>>();
            foreach (var kv in mapping)
            {
                var val = row.TryGetValue(kv.Value, out var raw) ? raw : "";
                if (validators.TryGetValue(kv.Key, out var check))
                {
                    var msg = check(Str(val));
                    if (msg is not true)
                    {
                        rowErrors.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["column"] = kv.Key,
                            ["value"] = val is null ? "" : val,
                            ["error"] = msg
                        });
                    }
                }
            }

            if (rowErrors.Count == 0)
            {
                valid++;
            }
            else
            {
                invalid++;
                if (errList.Count < 100)
                {
                    errList.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["row"] = idx + 1,
                        ["errors"] = rowErrors
                    });
                }
            }
        }

        results["valid"] = valid;
        results["invalid"] = invalid;
        return results;
    }

    public static Dictionary<string, Func<string, object>> EpcImportValidators(string source)
    {
        var common = new Dictionary<string, Func<string, object>>(StringComparer.Ordinal)
        {
            ["email"] = v => PhpEmailOk(v) || v == "" ? true : "Invalid email",
            ["phone"] = v => PhoneRe.IsMatch(v) ? true : "Invalid phone"
        };
        var extra = source switch
        {
            "products" => new Dictionary<string, Func<string, object>>(StringComparer.Ordinal)
            {
                ["sku"] = v => v != "" ? true : "SKU required",
                ["price"] = v => PhpIsNumeric(v) ? true : "Invalid price",
                ["stock_qty"] = v => PhpCtypeDigit(v) || v == "" ? true : "Invalid qty"
            },
            "customers" => new Dictionary<string, Func<string, object>>(StringComparer.Ordinal)
            {
                ["name"] = v => v != "" ? true : "Name required"
            },
            "invoices" => new Dictionary<string, Func<string, object>>(StringComparer.Ordinal)
            {
                ["amount"] = v => PhpIsNumeric(v) ? true : "Invalid amount",
                ["date"] = v => DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _) ? true : "Invalid date"
            },
            _ => new Dictionary<string, Func<string, object>>(StringComparer.Ordinal)
        };
        foreach (var kv in extra)
        {
            common[kv.Key] = kv.Value;
        }

        return common;
    }

    public static Dictionary<string, object?> EpcImportCancel(ImportStore store, int jobId)
    {
        var job = store.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is not null && (job.Status == "pending" || job.Status == "processing"))
        {
            job.Status = "cancelled";
        }

        return Ok(true);
    }

    public static Dictionary<string, object?> EpcImportRetry(ImportStore store, int jobId)
    {
        var job = store.Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job is not null && job.Status == "failed")
        {
            job.Status = "pending";
            job.ErrorRows = 0;
            job.SkipRows = 0;
        }

        return Ok(true);
    }

    public static void EpcVaultEnsureSchema(VaultStore store) => store.SchemaReady = true;
    public static void EpcVaultGdprEnsureSchema(VaultStore store) => store.GdprSchemaReady = true;

    public static Dictionary<string, object?> EpcVaultCreateFolder(VaultStore store, string siteKey, string name, int parentId = 0, int userId = 0)
    {
        EpcVaultEnsureSchema(store);
        var parentPath = "/";
        if (parentId > 0)
        {
            var p = store.Folders.FirstOrDefault(f => f.Id == parentId && f.SiteKey == siteKey);
            if (p is not null)
            {
                parentPath = p.Path.TrimEnd('/') + "/" + p.Name + "/";
            }
        }

        var folder = new VaultFolder
        {
            Id = store.NextFolderId++,
            SiteKey = siteKey,
            ParentId = parentId > 0 ? parentId : null,
            Name = name,
            Path = parentPath,
            CreatedBy = userId,
            CreatedAt = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        store.Folders.Add(folder);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["folder_id"] = folder.Id,
            ["path"] = parentPath + name + "/"
        };
    }

    public static List<VaultFolder> EpcVaultListFolders(VaultStore store, string siteKey, int parentId = 0)
    {
        EpcVaultEnsureSchema(store);
        IEnumerable<VaultFolder> q = store.Folders.Where(f => f.SiteKey == siteKey);
        q = parentId > 0 ? q.Where(f => f.ParentId == parentId) : q.Where(f => f.ParentId is null);
        return q.OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
    }

    public static Dictionary<string, object?> EpcVaultUpload(VaultStore store, string siteKey, Dictionary<string, object?> data)
    {
        EpcVaultEnsureSchema(store);
        var folderId = IntOf(data, "folder_id");
        var now = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var doc = new VaultDocument
        {
            Id = store.NextDocId++,
            SiteKey = siteKey,
            FolderId = folderId > 0 ? folderId : null,
            Filename = Str(data, "filename"),
            MimeType = Str(data, "mime_type", "application/octet-stream"),
            FileSize = IntOf(data, "file_size"),
            TagsJson = JsonEncode(data.TryGetValue("tags", out var tags) ? tags : Array.Empty<object>()),
            AccessLevel = Str(data, "access_level", "tenant"),
            RetentionDays = IntOf(data, "retention_days"),
            UploadedBy = IntOf(data, "uploaded_by"),
            CreatedAt = now,
            UpdatedAt = now
        };
        store.Documents.Add(doc);
        store.Versions.Add(new VaultVersion
        {
            Id = store.NextVersionId++,
            DocumentId = doc.Id,
            VersionNumber = 1,
            FilePath = Str(data, "file_path"),
            FileSize = doc.FileSize,
            Checksum = Str(data, "checksum"),
            ChangeNote = "Initial upload",
            UploadedBy = doc.UploadedBy,
            CreatedAt = now
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["document_id"] = doc.Id, ["version"] = 1 };
    }

    public static Dictionary<string, object?> EpcVaultNewVersion(VaultStore store, int docId, Dictionary<string, object?> data)
    {
        var doc = store.Documents.FirstOrDefault(d => d.Id == docId);
        var cv = doc?.CurrentVersion ?? 0;
        if (cv == 0)
        {
            return Ok(false, "error", "Document not found");
        }

        var newVer = cv + 1;
        var now = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        store.Versions.Add(new VaultVersion
        {
            Id = store.NextVersionId++,
            DocumentId = docId,
            VersionNumber = newVer,
            FilePath = Str(data, "file_path"),
            FileSize = IntOf(data, "file_size"),
            Checksum = Str(data, "checksum"),
            ChangeNote = Str(data, "change_note"),
            UploadedBy = IntOf(data, "uploaded_by"),
            CreatedAt = now
        });
        doc!.CurrentVersion = newVer;
        doc.FileSize = IntOf(data, "file_size");
        doc.UpdatedAt = now;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["version"] = newVer };
    }

    public static List<Dictionary<string, object?>> EpcVaultListDocuments(VaultStore store, string siteKey, int folderId = 0)
    {
        EpcVaultEnsureSchema(store);
        IEnumerable<VaultDocument> q = store.Documents.Where(d => d.SiteKey == siteKey && d.Status == "active");
        if (folderId > 0)
        {
            q = q.Where(d => d.FolderId == folderId);
        }

        return q.OrderBy(d => d.Filename, StringComparer.Ordinal).Select(d => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = d.Id,
            ["site_key"] = d.SiteKey,
            ["folder_id"] = d.FolderId,
            ["filename"] = d.Filename,
            ["mime_type"] = d.MimeType,
            ["file_size"] = d.FileSize,
            ["current_version"] = d.CurrentVersion,
            ["tags"] = JsonDecodeFlexible(d.TagsJson),
            ["access_level"] = d.AccessLevel,
            ["retention_days"] = d.RetentionDays,
            ["status"] = d.Status,
            ["uploaded_by"] = d.UploadedBy,
            ["created_at"] = d.CreatedAt,
            ["updated_at"] = d.UpdatedAt
        }).ToList();
    }

    public static List<VaultVersion> EpcVaultVersions(VaultStore store, int docId)
        => store.Versions.Where(v => v.DocumentId == docId).OrderByDescending(v => v.VersionNumber).ToList();

    public static List<Dictionary<string, object?>> EpcVaultFleetStats(VaultStore store)
    {
        EpcVaultEnsureSchema(store);
        return store.Documents.Where(d => d.Status == "active")
            .GroupBy(d => d.SiteKey)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = g.Key,
                ["documents"] = g.Count(),
                ["total_size"] = g.Sum(x => x.FileSize),
                ["max_versions"] = g.Max(x => x.CurrentVersion)
            })
            .ToList();
    }

    public static Dictionary<string, object?> EpcVaultGdprRequest(VaultStore store, string siteKey, string type, string email, string name = "")
    {
        EpcVaultGdprEnsureSchema(store);
        var row = new VaultGdprRequest
        {
            Id = store.NextGdprId++,
            SiteKey = siteKey,
            RequestType = type,
            SubjectEmail = email,
            SubjectName = name,
            CreatedAt = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        store.Gdpr.Add(row);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["request_id"] = row.Id,
            ["message"] = PhpUcfirst(type) + " request created"
        };
    }

    public static Dictionary<string, object?> EpcVaultGdprProcess(VaultStore store, int requestId, int userId)
    {
        EpcVaultGdprEnsureSchema(store);
        var req = store.Gdpr.FirstOrDefault(r => r.Id == requestId);
        if (req is null)
        {
            return Ok(false, "error", "Request not found");
        }

        EpcVaultEnsureSchema(store);
        var emailLike = req.SubjectEmail;
        var docs = store.Documents
            .Where(d => d.SiteKey == req.SiteKey && d.Status == "active" && (d.TagsJson.Contains(emailLike, StringComparison.Ordinal) || d.Filename.Contains(emailLike, StringComparison.Ordinal)))
            .Select(d => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = d.Id,
                ["filename"] = d.Filename,
                ["mime_type"] = d.MimeType,
                ["file_size"] = d.FileSize,
                ["tags"] = d.TagsJson
            })
            .ToList();
        var response = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["documents_found"] = docs.Count,
            ["documents"] = docs
        };
        if (req.RequestType == "erasure")
        {
            foreach (var doc in docs)
            {
                var id = Convert.ToInt32(doc["id"], CultureInfo.InvariantCulture);
                var row = store.Documents.First(d => d.Id == id);
                row.Status = "deleted";
            }

            response["erased"] = docs.Count;
        }

        req.Status = "completed";
        req.DocumentsFound = docs.Count;
        req.ResponseJson = JsonEncode(response);
        req.ProcessedBy = userId;
        req.CompletedAt = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["response"] = response };
    }

    public static List<VaultGdprRequest> EpcVaultGdprList(VaultStore store, string siteKey = "", string status = "")
    {
        EpcVaultGdprEnsureSchema(store);
        IEnumerable<VaultGdprRequest> q = store.Gdpr;
        if (siteKey != "")
        {
            q = q.Where(r => r.SiteKey == siteKey);
        }

        if (status != "")
        {
            q = q.Where(r => r.Status == status);
        }

        return q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(200).ToList();
    }

    public static List<VaultDocument> EpcVaultSearch(VaultStore store, string siteKey, string query)
    {
        EpcVaultEnsureSchema(store);
        return store.Documents
            .Where(d => d.SiteKey == siteKey && d.Status == "active" && (d.Filename.Contains(query, StringComparison.Ordinal) || d.TagsJson.Contains(query, StringComparison.Ordinal)))
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .Take(100)
            .ToList();
    }

    public static Dictionary<string, object?> EpcVaultDelete(VaultStore store, int docId)
    {
        var doc = store.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc is not null)
        {
            doc.Status = "deleted";
        }

        return Ok(true);
    }

    public static Dictionary<string, object?> EpcVaultRestore(VaultStore store, int docId)
    {
        var doc = store.Documents.FirstOrDefault(d => d.Id == docId);
        if (doc is not null)
        {
            doc.Status = "active";
        }

        return Ok(true);
    }

    public static void EpcOnpremLicenseEnsureSchema(LicenseStore store) => store.SchemaReady = true;

    public static Dictionary<string, object?> EpcOnpremLicenseGenerate(LicenseStore store, Dictionary<string, object?> opts)
    {
        EpcOnpremLicenseEnsureSchema(store);
        var year = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
        string key;
        do
        {
            var part1 = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
            var part2 = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
            key = "LIC-" + year + "-" + part1 + "-" + part2;
        }
        while (store.Licenses.Any(l => l.LicenseKey == key));

        var tier = LicenseTiers.Contains(Str(opts, "tier"), StringComparer.Ordinal) ? Str(opts, "tier") : "standard";
        var modules = opts.TryGetValue("modules", out var m) && m is IEnumerable<object> list && m is not string
            ? list.Cast<object?>().Select(x => Str(x)).ToList()
            : opts.TryGetValue("modules", out var m2) && m2 is IEnumerable<string> slist
                ? slist.ToList()
                : new List<string> { "all" };
        var usersMax = Math.Max(1, IntOf(opts, "users_max", 25));
        var expiresDays = opts.ContainsKey("expires_days") ? IntOf(opts, "expires_days") : 365;
        var now = store.Now();
        long? expiresAt = expiresDays > 0 ? now + (expiresDays * 86400L) : null;
        store.Licenses.Add(new OnpremLicense
        {
            Id = store.NextLicenseId++,
            LicenseKey = key,
            CustomerName = Str(opts, "customer_name"),
            Tier = tier,
            ModulesJson = JsonEncode(modules),
            UsersMax = usersMax,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            Notes = Str(opts, "notes")
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["license_key"] = key,
            ["tier"] = tier,
            ["modules"] = modules,
            ["users_max"] = usersMax,
            ["expires_at"] = expiresAt
        };
    }

    /// <summary>PHP binds LIMIT ? which fatals on this MariaDB; the twin takes an int limit.</summary>
    public static List<OnpremLicense> EpcOnpremLicenseList(LicenseStore store, int limit = 100)
    {
        EpcOnpremLicenseEnsureSchema(store);
        return store.Licenses.OrderByDescending(l => l.Id).Take(limit).ToList();
    }

    public static bool EpcOnpremLicenseRevoke(LicenseStore store, string key)
    {
        EpcOnpremLicenseEnsureSchema(store);
        var row = store.Licenses.FirstOrDefault(l => l.LicenseKey == key);
        if (row is null)
        {
            return false;
        }

        row.Status = "revoked";
        return true;
    }

    public static OnpremLicense? EpcOnpremLicenseFetch(LicenseStore store, string key)
        => store.Licenses.FirstOrDefault(l => l.LicenseKey == key);

    public static string EpcOnpremLicenseSigningKeyPath(LicenseStore? store = null)
    {
        if (store?.SigningKeyPath is { Length: > 0 } injected)
        {
            return injected;
        }

        var path = Environment.GetEnvironmentVariable("EPC_LICENSE_SIGNING_KEY_PATH");
        return string.IsNullOrEmpty(path) ? "/etc/ecomae/license_signing_key.pem" : path;
    }

    public static string? EpcOnpremLicenseSign(LicenseStore store, Dictionary<string, object?> certData)
    {
        var keyPath = EpcOnpremLicenseSigningKeyPath(store);
        if (!File.Exists(keyPath))
        {
            return null;
        }

        try
        {
            var pem = File.ReadAllText(keyPath);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var payload = JsonSerializer.Serialize(certData, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var sig = rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sig);
        }
        catch
        {
            return null;
        }
    }

    public static string? EpcOnpremCoreBundle(LicenseStore store)
    {
        var docRoot = (store.DocumentRoot ?? "").TrimEnd('/');
        if (docRoot == "")
        {
            return null;
        }

        var files = new[] { "core/dp_core.php", "core/dp_content.php", "core/dp_module.php", "core/dp_template.php" };
        if (!files.Any(f => File.Exists(docRoot + "/" + f)))
        {
            return null;
        }

        return null;
    }

    public static Dictionary<string, object?> EpcOnpremLicenseActivate(LicenseStore store, Dictionary<string, object?> input)
    {
        EpcOnpremLicenseEnsureSchema(store);
        var key = Str(input, "license_key").Trim();
        if (!LicenseKeyRe.IsMatch(key))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["success"] = false, ["error"] = "invalid_key_format", ["message"] = "License key format is invalid." };
        }

        var row = EpcOnpremLicenseFetch(store, key);
        if (row is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["success"] = false, ["error"] = "not_found", ["message"] = "License key not recognized." };
        }

        if (row.Status == "revoked")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["success"] = false, ["error"] = "revoked", ["message"] = "This license has been revoked." };
        }

        if (row.ExpiresAt is > 0 && row.ExpiresAt.Value < store.Now())
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["success"] = false, ["error"] = "expired", ["message"] = "This license has expired." };
        }

        var fingerprint = Str(input, "fingerprint");
        if (fingerprint == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["success"] = false, ["error"] = "missing_fingerprint", ["message"] = "Server fingerprint is required." };
        }

        if (!string.IsNullOrEmpty(row.Fingerprint) && row.Fingerprint != fingerprint)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["success"] = false,
                ["error"] = "already_activated",
                ["message"] = "This license is already activated on another server. Contact support to transfer it."
            };
        }

        var now = store.Now();
        row.Status = "active";
        row.Fingerprint = fingerprint;
        row.Hostname = PhpSubstr(Str(input, "hostname"), 190);
        row.Ip = PhpSubstr(Str(input, "ip"), 45);
        row.ActivatedAt ??= now;
        row.LastSeenAt = now;
        var modules = JsonSerializer.Deserialize<List<string>>(row.ModulesJson) ?? new List<string> { "all" };
        var certData = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["license_key"] = key,
            ["fingerprint"] = fingerprint,
            ["tier"] = row.Tier,
            ["modules"] = modules.Count == 0 ? new List<string> { "all" } : modules,
            ["users_max"] = row.UsersMax,
            ["issued_at"] = row.IssuedAt,
            ["expires_at"] = row.ExpiresAt is null ? null : DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt.Value).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)
        };
        var signature = EpcOnpremLicenseSign(store, certData);
        if (signature is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["success"] = false,
                ["error"] = "signing_unavailable",
                ["message"] = "License server signing key is not configured. Contact ecomae support."
            };
        }

        certData["signature"] = signature;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["success"] = true,
            ["activation_cert"] = certData,
            ["core_bundle"] = EpcOnpremCoreBundle(store)
        };
    }

    public static void EpcOnpremHealthLog(LicenseStore store, Dictionary<string, object?> input)
    {
        EpcOnpremLicenseEnsureSchema(store);
        var key = PhpSubstr(Str(input, "license_key"), 32);
        store.Health.Add(new OnpremHealth
        {
            Id = store.NextHealthId++,
            LicenseKey = key,
            Status = PhpSubstr(Str(input, "status"), 40),
            Uptime = PhpSubstr(Str(input, "uptime"), 40),
            DiskFreeGb = Dbl(input, "disk_free_gb"),
            MemoryUsageMb = Dbl(input, "memory_usage_mb"),
            PhpVersion = PhpSubstr(Str(input, "php_version"), 20),
            DbSizeMb = Dbl(input, "db_size_mb"),
            LastBackup = PhpSubstr(Str(input, "last_backup"), 40),
            ReportedAt = store.Now()
        });
        var lic = store.Licenses.FirstOrDefault(l => l.LicenseKey == key);
        if (lic is not null)
        {
            lic.LastSeenAt = store.Now();
        }
    }

    public static void EpcBiEnsureSchema(BiStore store) => store.SchemaReady = true;

    public static List<Dictionary<string, string>> EpcBiBuiltinMetrics()
        => new()
        {
            Met("revenue_daily", "Daily Revenue", "finance", "currency", "sum", "daily", "SELECT COALESCE(SUM(total), 0) FROM orders WHERE status != 'cancelled' AND DATE(created_at) = :date"),
            Met("orders_daily", "Daily Orders", "sales", "number", "count", "daily", "SELECT COUNT(*) FROM orders WHERE DATE(created_at) = :date"),
            Met("aov", "Average Order Value", "sales", "currency", "avg", "daily", "SELECT COALESCE(AVG(total), 0) FROM orders WHERE status != 'cancelled' AND DATE(created_at) = :date"),
            Met("new_customers", "New Customers", "growth", "number", "count", "daily", "SELECT COUNT(*) FROM customers WHERE DATE(created_at) = :date"),
            Met("active_skus", "Active SKUs", "inventory", "number", "latest", "daily", "SELECT COUNT(*) FROM products WHERE active = 1"),
            Met("low_stock_items", "Low Stock Items", "inventory", "number", "latest", "daily", "SELECT COUNT(*) FROM products WHERE stock_qty <= reorder_level AND stock_qty >= 0"),
            Met("outstanding_ar", "Outstanding AR", "finance", "currency", "latest", "daily", "SELECT COALESCE(SUM(balance_due), 0) FROM invoices WHERE status = 'outstanding'"),
            Met("gross_margin", "Gross Margin %", "finance", "percent", "avg", "weekly", "SELECT CASE WHEN SUM(total) > 0 THEN ((SUM(total) - SUM(COALESCE(cost,0))) / SUM(total)) * 100 ELSE 0 END FROM orders WHERE status != 'cancelled' AND created_at >= :week_start AND created_at < :week_end"),
            Met("return_rate", "Return Rate %", "operations", "percent", "avg", "weekly", "SELECT CASE WHEN COUNT(*) > 0 THEN (SUM(CASE WHEN status = 'returned' THEN 1 ELSE 0 END) / COUNT(*)) * 100 ELSE 0 END FROM orders WHERE created_at >= :week_start AND created_at < :week_end"),
            Met("conversion_rate", "Conversion Rate %", "sales", "percent", "avg", "daily", "SELECT 0")
        };

    public static Dictionary<string, object?> EpcBiRecordSnapshot(BiStore store, string siteKey, string metricKey, string period, string periodStart, double value, double previousValue = 0)
    {
        EpcBiEnsureSchema(store);
        var changePct = previousValue != 0
            ? Math.Round(((value - previousValue) / Math.Abs(previousValue)) * 100, 2, MidpointRounding.AwayFromZero)
            : 0;
        var now = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var existing = store.Snapshots.FirstOrDefault(s => s.SiteKey == siteKey && s.MetricKey == metricKey && s.Period == period && s.PeriodStart == periodStart);
        if (existing is null)
        {
            store.Snapshots.Add(new BiSnapshot
            {
                Id = store.NextId++,
                SiteKey = siteKey,
                MetricKey = metricKey,
                Period = period,
                PeriodStart = periodStart,
                Value = value,
                PreviousValue = previousValue,
                ChangePct = changePct,
                ComputedAt = now
            });
        }
        else
        {
            existing.Value = value;
            existing.PreviousValue = previousValue;
            existing.ChangePct = changePct;
            existing.ComputedAt = now;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["metric_key"] = metricKey,
            ["value"] = value,
            ["change_pct"] = changePct
        };
    }

    /// <summary>PHP binds LIMIT ? which fatals on this MariaDB; the twin takes an int limit.</summary>
    public static List<BiSnapshot> EpcBiMetricTrend(BiStore store, string siteKey, string metricKey, string period = "daily", int limit = 30)
    {
        EpcBiEnsureSchema(store);
        return store.Snapshots
            .Where(s => s.SiteKey == siteKey && s.MetricKey == metricKey && s.Period == period)
            .OrderByDescending(s => s.PeriodStart)
            .Take(limit)
            .Reverse()
            .ToList();
    }

    public static Dictionary<string, BiSnapshot> EpcBiLatestAll(BiStore store, string siteKey)
    {
        EpcBiEnsureSchema(store);
        return store.Snapshots.Where(s => s.SiteKey == siteKey)
            .GroupBy(s => s.MetricKey)
            .Select(g => g.OrderByDescending(x => x.PeriodStart).First())
            .ToDictionary(s => s.MetricKey, s => s, StringComparer.Ordinal);
    }

    public static Dictionary<string, object?> EpcBiComputeDaily(BiStore store, string siteKey, string date = "")
    {
        if (date == "")
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var results = new List<Dictionary<string, object?>>();
        foreach (var m in EpcBiBuiltinMetrics())
        {
            if (m["schedule"] != "daily")
            {
                continue;
            }

            var prev = EpcBiMetricTrend(store, siteKey, m["metric_key"], "daily", 1);
            var previousValue = prev.Count > 0 ? prev[0].Value : 0;
            results.Add(EpcBiRecordSnapshot(store, siteKey, m["metric_key"], "daily", date, 0, previousValue));
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["date"] = date,
            ["computed"] = results.Count,
            ["results"] = results
        };
    }

    public static Dictionary<string, List<Dictionary<string, object?>>> EpcBiDashboard(BiStore store, string siteKey)
    {
        var latest = EpcBiLatestAll(store, siteKey);
        var categories = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var m in EpcBiBuiltinMetrics())
        {
            var key = m["metric_key"];
            latest.TryGetValue(key, out var data);
            if (!categories.TryGetValue(m["category"], out var list))
            {
                list = new List<Dictionary<string, object?>>();
                categories[m["category"]] = list;
            }

            list.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["metric_key"] = key,
                ["label"] = m["label"],
                ["unit"] = m["unit"],
                ["value"] = data?.Value ?? 0d,
                ["change_pct"] = data?.ChangePct ?? 0d,
                ["computed_at"] = data?.ComputedAt
            });
        }

        return categories;
    }

    public static List<Dictionary<string, object?>> EpcBiFleetOverview(BiStore store)
    {
        EpcBiEnsureSchema(store);
        var latest = store.Snapshots
            .GroupBy(s => (s.SiteKey, s.MetricKey))
            .Select(g => g.OrderByDescending(x => x.PeriodStart).First())
            .ToList();
        return latest
            .GroupBy(s => s.SiteKey)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = g.Key,
                ["metrics_tracked"] = g.Select(x => x.MetricKey).Distinct(StringComparer.Ordinal).Count(),
                ["last_computed"] = g.Max(x => x.ComputedAt),
                ["latest_revenue"] = g.Where(x => x.MetricKey == "revenue_daily").Sum(x => x.Value),
                ["latest_orders"] = g.Where(x => x.MetricKey == "orders_daily").Sum(x => x.Value)
            })
            .OrderByDescending(x => Convert.ToDouble(x["latest_revenue"], CultureInfo.InvariantCulture))
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcBiCompareTenants(BiStore store, string metricKey)
    {
        EpcBiEnsureSchema(store);
        return store.Snapshots.Where(s => s.MetricKey == metricKey)
            .GroupBy(s => s.SiteKey)
            .Select(g => g.OrderByDescending(x => x.PeriodStart).First())
            .OrderByDescending(s => s.Value)
            .Select(s => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = s.SiteKey,
                ["value"] = s.Value,
                ["change_pct"] = s.ChangePct,
                ["period_start"] = s.PeriodStart
            })
            .ToList();
    }

    /// <summary>PHP binds INTERVAL ? DAY which is skipped in goldens; the twin takes an int keepDays.</summary>
    public static int EpcBiCleanup(BiStore store, int keepDays = 365)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-keepDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var n = store.Snapshots.RemoveAll(s => string.CompareOrdinal(s.PeriodStart, cutoff) < 0);
        return n;
    }

    public static void EpcNotificationsEnsureSchema(NotifyStore store) => store.SchemaReady = true;

    public static Dictionary<string, Dictionary<string, string>> EpcNotificationCategories()
        => new(StringComparer.Ordinal)
        {
            ["system"] = Cat("System Alerts", "fa-cog", "#6b7280"),
            ["security"] = Cat("Security", "fa-shield", "#ef4444"),
            ["order"] = Cat("Orders", "fa-shopping-cart", "#3b82f6"),
            ["invoice"] = Cat("Invoicing", "fa-file-text-o", "#10b981"),
            ["inventory"] = Cat("Inventory", "fa-cubes", "#f59e0b"),
            ["erp"] = Cat("ERP / Finance", "fa-calculator", "#8b5cf6"),
            ["compliance"] = Cat("Compliance", "fa-balance-scale", "#ec4899"),
            ["tenant"] = Cat("Tenant Updates", "fa-building", "#06b6d4"),
            ["webhook"] = Cat("Webhook Alerts", "fa-plug", "#84cc16"),
            ["deploy"] = Cat("Deploy / Maintenance", "fa-rocket", "#f97316")
        };

    public static int EpcNotificationSend(NotifyStore store, Dictionary<string, object?> data)
    {
        var title = Str(data, "title");
        if (title == "")
        {
            return 0;
        }

        var severity = Str(data, "severity", "info");
        if (!NotifySeverities.Contains(severity, StringComparer.Ordinal))
        {
            severity = "info";
        }

        EpcNotificationsEnsureSchema(store);
        var row = new NotificationRow
        {
            Id = store.NextId++,
            TenantKey = Str(data, "tenant_key", "__platform__"),
            UserId = IntOf(data, "user_id"),
            Category = Str(data, "category", "system"),
            Severity = severity,
            Title = title,
            Body = Str(data, "body"),
            ActionUrl = Str(data, "action_url"),
            ActionLabel = Str(data, "action_label"),
            Metadata = data.ContainsKey("metadata") ? JsonEncode(data["metadata"]) : null,
            CreatedAt = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        };
        store.Rows.Add(row);
        return (int)row.Id;
    }

    public static int EpcNotificationBroadcast(NotifyStore store, string tenantKey, Dictionary<string, object?> data)
    {
        var copy = new Dictionary<string, object?>(data, StringComparer.Ordinal)
        {
            ["tenant_key"] = tenantKey,
            ["user_id"] = 0
        };
        return EpcNotificationSend(store, copy);
    }

    public static List<NotificationRow> EpcNotificationsList(NotifyStore store, string tenantKey, int userId = 0, Dictionary<string, object?>? filters = null, int limit = 50, int offset = 0)
    {
        EpcNotificationsEnsureSchema(store);
        IEnumerable<NotificationRow> q = store.Rows.Where(n => n.TenantKey == tenantKey && (n.UserId == userId || n.UserId == 0));
        filters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        if (filters.TryGetValue("category", out var cat) && !IsPhpEmpty(cat))
        {
            q = q.Where(n => n.Category == Str(cat));
        }

        if (filters.ContainsKey("is_read"))
        {
            q = q.Where(n => n.IsRead == IntOf(filters, "is_read"));
        }

        if (filters.TryGetValue("severity", out var sev) && !IsPhpEmpty(sev))
        {
            q = q.Where(n => n.Severity == Str(sev));
        }

        if (filters.TryGetValue("dismissed", out var dis) && dis is false)
        {
            q = q.Where(n => n.Dismissed == 0);
        }

        return q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip(offset).Take(limit).ToList();
    }

    public static int EpcNotificationsUnreadCount(NotifyStore store, string tenantKey, int userId = 0)
    {
        EpcNotificationsEnsureSchema(store);
        return store.Rows.Count(n => n.TenantKey == tenantKey && (n.UserId == userId || n.UserId == 0) && n.IsRead == 0 && n.Dismissed == 0);
    }

    public static int EpcNotificationsMarkRead(NotifyStore store, IReadOnlyList<int> ids, string tenantKey)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var now = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var n = 0;
        foreach (var row in store.Rows.Where(r => ids.Contains((int)r.Id) && r.TenantKey == tenantKey))
        {
            if (row.IsRead == 0)
            {
                n++;
            }

            row.IsRead = 1;
            row.ReadAt = now;
        }

        return n;
    }

    public static int EpcNotificationsMarkAllRead(NotifyStore store, string tenantKey, int userId = 0)
    {
        var now = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var n = 0;
        foreach (var row in store.Rows.Where(r => r.TenantKey == tenantKey && (r.UserId == userId || r.UserId == 0) && r.IsRead == 0))
        {
            row.IsRead = 1;
            row.ReadAt = now;
            n++;
        }

        return n;
    }

    public static bool EpcNotificationsDismiss(NotifyStore store, int id, string tenantKey)
    {
        var row = store.Rows.FirstOrDefault(r => r.Id == id && r.TenantKey == tenantKey);
        if (row is null)
        {
            return false;
        }

        row.Dismissed = 1;
        return true;
    }

    public static List<Dictionary<string, object?>> EpcNotificationsSummary(NotifyStore store, string tenantKey, int userId = 0)
    {
        EpcNotificationsEnsureSchema(store);
        return store.Rows
            .Where(n => n.TenantKey == tenantKey && (n.UserId == userId || n.UserId == 0) && n.Dismissed == 0)
            .GroupBy(n => n.Category)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category"] = g.Key,
                ["total"] = g.Count(),
                ["unread"] = g.Count(x => x.IsRead == 0),
                ["latest"] = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => Convert.ToInt32(x["unread"], CultureInfo.InvariantCulture))
            .ThenByDescending(x => Str(x["latest"]))
            .ToList();
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcNotificationPrefsGet(NotifyStore store, string tenantKey, int userId)
    {
        EpcNotificationsEnsureSchema(store);
        var prefs = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var row in store.Prefs.Where(p => p.TenantKey == tenantKey && p.UserId == userId))
        {
            prefs[row.Category] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["in_app"] = row.ChannelInApp != 0,
                ["email"] = row.ChannelEmail != 0,
                ["webhook"] = row.ChannelWebhook != 0,
                ["email_digest"] = row.EmailDigest
            };
        }

        return prefs;
    }

    public static bool EpcNotificationPrefsSave(NotifyStore store, string tenantKey, int userId, string category, Dictionary<string, object?> channels)
    {
        EpcNotificationsEnsureSchema(store);
        var row = store.Prefs.FirstOrDefault(p => p.TenantKey == tenantKey && p.UserId == userId && p.Category == category);
        if (row is null)
        {
            row = new NotificationPref
            {
                Id = store.NextPrefId++,
                TenantKey = tenantKey,
                UserId = userId,
                Category = category
            };
            store.Prefs.Add(row);
        }

        row.ChannelInApp = IntOf(channels, "in_app", 1);
        row.ChannelEmail = IntOf(channels, "email", 1);
        row.ChannelWebhook = IntOf(channels, "webhook");
        row.EmailDigest = Str(channels, "email_digest", "daily");
        return true;
    }

    public static List<Dictionary<string, object?>> EpcNotificationsFleetStats(NotifyStore store)
    {
        EpcNotificationsEnsureSchema(store);
        return store.Rows.Where(n => n.Dismissed == 0)
            .GroupBy(n => n.TenantKey)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tenant_key"] = g.Key,
                ["total"] = g.Count(),
                ["unread"] = g.Count(x => x.IsRead == 0),
                ["errors"] = g.Count(x => x.Severity == "error" && x.IsRead == 0),
                ["latest"] = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => Convert.ToInt32(x["errors"], CultureInfo.InvariantCulture))
            .ThenByDescending(x => Convert.ToInt32(x["unread"], CultureInfo.InvariantCulture))
            .ToList();
    }

    public static int EpcNotificationsCleanup(NotifyStore store, int daysOld = 90)
    {
        var cutoff = store.Now().AddSeconds(-daysOld * 86400d).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return store.Rows.RemoveAll(n => string.CompareOrdinal(n.CreatedAt, cutoff) < 0 && n.IsRead == 1);
    }

    public static List<Dictionary<string, object?>> EpcNotificationsPendingDigest(NotifyStore store, string digestType = "daily")
    {
        EpcNotificationsEnsureSchema(store);
        var since = store.Now().AddSeconds(digestType == "hourly" ? -3600 : -86400).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var rows = store.Rows.Where(n => n.IsRead == 0 && string.CompareOrdinal(n.CreatedAt, since) >= 0)
            .Where(n => store.Prefs.Any(p =>
                p.TenantKey == n.TenantKey && p.UserId == n.UserId
                && (p.Category == n.Category || p.Category == "*")
                && p.ChannelEmail == 1 && p.EmailDigest == digestType))
            .OrderBy(n => n.TenantKey, StringComparer.Ordinal)
            .ThenBy(n => n.UserId)
            .ThenByDescending(n => n.CreatedAt)
            .ToList();
        var grouped = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = row.TenantKey + "::" + row.UserId;
            if (!grouped.TryGetValue(key, out var g))
            {
                g = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["tenant_key"] = row.TenantKey,
                    ["user_id"] = row.UserId,
                    ["items"] = new List<NotificationRow>()
                };
                grouped[key] = g;
            }

            ((List<NotificationRow>)g["items"]!).Add(row);
        }

        return grouped.Values.ToList();
    }

    private static Dictionary<string, object?> Schema(string[] required, string[] optional, string unique)
        => new(StringComparer.Ordinal) { ["required"] = required, ["optional"] = optional, ["unique_key"] = unique };

    private static Dictionary<string, string> Fmt(string format, string label, string mime)
        => new(StringComparer.Ordinal) { ["format"] = format, ["label"] = label, ["mime"] = mime };

    private static Dictionary<string, object?> Src(string label, string table, params string[] required)
        => new(StringComparer.Ordinal) { ["label"] = label, ["table"] = table, ["required_fields"] = required };

    private static Dictionary<string, string> Met(string key, string label, string category, string unit, string aggregation, string schedule, string sql)
        => new(StringComparer.Ordinal)
        {
            ["metric_key"] = key,
            ["label"] = label,
            ["category"] = category,
            ["unit"] = unit,
            ["aggregation"] = aggregation,
            ["schedule"] = schedule,
            ["sql_template"] = sql
        };

    private static Dictionary<string, string> Cat(string label, string icon, string color)
        => new(StringComparer.Ordinal) { ["label"] = label, ["icon"] = icon, ["color"] = color };

    private static Dictionary<string, object?> Ok(bool ok)
        => new(StringComparer.Ordinal) { ["ok"] = ok };

    private static Dictionary<string, object?> Ok(bool ok, string extraKey, object? extra)
        => new(StringComparer.Ordinal) { ["ok"] = ok, [extraKey] = extra };

    private static Dictionary<string, object?> FormatErr(int rowNum, string field, object? value, string message)
        => new(StringComparer.Ordinal)
        {
            ["row_number"] = rowNum,
            ["field"] = field,
            ["value"] = value,
            ["error_type"] = "format",
            ["message"] = message
        };

    private static string JsonEncode(object? value)
    {
        if (value is null)
        {
            return "[]";
        }

        if (value is System.Collections.IDictionary dict && dict.Count == 0)
        {
            return "[]";
        }

        if (value is string)
        {
            return JsonSerializer.Serialize(value);
        }

        if (value is System.Collections.IEnumerable seq)
        {
            var any = false;
            foreach (var _ in seq)
            {
                any = true;
                break;
            }

            if (!any && value is not System.Collections.IDictionary)
            {
                return "[]";
            }
        }

        return JsonSerializer.Serialize(value);
    }

    private static object JsonDecodeFlexible(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        using var doc = JsonDocument.Parse(json);
        return FromJson(doc.RootElement);
    }

    private static object FromJson(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => (object?)FromJson(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => el.EnumerateArray().Select(FromJson).ToList(),
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => ""
        };

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static string Str(Dictionary<string, object?> data, string key, string fallback = "")
        => data.TryGetValue(key, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback : fallback;

    private static int IntOf(Dictionary<string, object?> data, string key, int fallback = 0)
    {
        if (!data.TryGetValue(key, out var v) || v is null)
        {
            return fallback;
        }

        if (v is int i)
        {
            return i;
        }

        if (v is long l)
        {
            return (int)l;
        }

        var s = Convert.ToString(v, CultureInfo.InvariantCulture);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static double Dbl(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var v) || v is null)
        {
            return 0;
        }

        return Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    private static string Trim(object? value) => Str(value).Trim();

    private static bool PhpEmailOk(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        var domain = value[(at + 1)..];
        return domain.Contains('.', StringComparison.Ordinal) && !value.Contains(' ', StringComparison.Ordinal);
    }

    private static bool PhpIsNumeric(object? value)
    {
        var s = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
        return s.Length > 0 && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static bool PhpCtypeDigit(object? value)
    {
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return s.Length > 0 && s.All(char.IsDigit);
    }

    private static string PhpUcfirst(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string PhpSubstr(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static bool IsPhpEmpty(object? value)
        => value is null
            || value is false
            || value is int i && i == 0
            || value is string s && (s.Length == 0 || s == "0");
}
