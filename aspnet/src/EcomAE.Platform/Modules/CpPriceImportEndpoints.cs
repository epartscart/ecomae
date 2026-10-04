using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Modules;

/// <summary>
/// Native supplier price-upload endpoints (PC / wizard upload, FTP / e-mail / URL "update now", schedules, cron tick,
/// history downloads, deploy API). CP writes follow the CP pattern: admin session with the <c>cp</c> capability,
/// <c>csrf_guard_key</c>, and <c>confirmWrites=1</c> (otherwise a dry-run envelope). Machine endpoints authenticate with
/// config.php <c>tech_key</c> like their PHP counterparts.
/// </summary>
internal static class CpPriceImportEndpoints
{
    public const long MaxUploadBytes = 512L * 1024 * 1024;
    private const string ReturnUrl = "/cp/prices-upload-app";

    public static void Map(IEndpointRouteBuilder endpoints, Func<LegacySessionContext, object> sessionPayload)
    {
        endpoints.MapPost(EcomAeRoutes.CpPricesUploadFile, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCsrfGuard csrf,
            ICpPriceImportService imports,
            IErpWriteConnectionFactory connections,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!IsCpAdmin(session))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=" + ReturnUrl, "Admin CP capability required for price upload.");
            }

            if (!context.Request.HasFormContentType)
            {
                return Answer(context, false, "invalid", "multipart/form-data with the price file is required.", sessionPayload(session));
            }

            AllowLargeBody(context);
            var form = await context.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = MaxUploadBytes }, cancellationToken);
            var input = CpCrmActionInput.FromForm(form);
            var verdict = await csrf.VerifyAsync(context, session, input.TextOrNull(CpCsrfGuard.FieldName), cancellationToken);
            if (!verdict.Ok)
            {
                return Answer(context, false, verdict.Code, verdict.Message, sessionPayload(session));
            }

            var priceId = input.Long("price_id", "priceId");
            var channel = input.Text("channel").ToLowerInvariant() == "wizard" ? "wizard" : "pc";
            var file = form.Files.GetFile("file_" + priceId.ToString(CultureInfo.InvariantCulture))
                       ?? form.Files.GetFile("price_file")
                       ?? form.Files.GetFile("file")
                       ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return Answer(context, false, "invalid", "Choose a price file to upload.", sessionPayload(session));
            }

            if (!input.Flag("confirmWrites", "confirm_writes"))
            {
                return Results.Json(DryRun("Set confirmWrites=1 to import " + file.FileName + " into price list " + priceId.ToString(CultureInfo.InvariantCulture) + ".", sessionPayload(session)));
            }

            if (channel == "wizard" && connections.IsConfigured)
            {
                var layout = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var key in new[]
                         {
                             "strings_to_left", "manufacturer_col", "article_col", "name_col", "exist_col", "price_col",
                             "time_to_exe_col", "storage_col", "min_order_col", "clean_before", "encoding", "separator", "file_name_substring",
                         })
                {
                    if (form.ContainsKey(key))
                    {
                        layout[key] = form[key].ToString();
                    }
                }

                if (layout.Count > 0)
                {
                    await using var connection = await connections.OpenAsync(cancellationToken);
                    await CpPriceListConfig.SaveLayoutAsync(connection, priceId, layout, cancellationToken);
                }
            }

            await using var content = file.OpenReadStream();
            var result = await imports.ImportUploadAsync(new CpPriceImportRequest(priceId, channel, session.UserId, new CpPriceUpload(file.FileName, content)), cancellationToken);
            return Answer(context, result, sessionPayload(session));
        }).DisableAntiforgery();

        endpoints.MapPost(EcomAeRoutes.CpPricesUpdateNow, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCsrfGuard csrf,
            ICpPriceImportService imports,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!IsCpAdmin(session))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=" + ReturnUrl, "Admin CP capability required for price update.");
            }

            var input = await ReadInputAsync(context, cancellationToken);
            var verdict = await csrf.VerifyAsync(context, session, input.TextOrNull(CpCsrfGuard.FieldName), cancellationToken);
            if (!verdict.Ok)
            {
                return Answer(context, false, verdict.Code, verdict.Message, sessionPayload(session));
            }

            var ids = Ids(input.Text("price_ids", "price_id", "priceId"));
            if (ids.Count == 0)
            {
                return Answer(context, false, "invalid", "A price list id is required.", sessionPayload(session));
            }

            if (!input.Flag("confirmWrites", "confirm_writes"))
            {
                return Results.Json(DryRun("Set confirmWrites=1 to fetch and import price list(s) " + string.Join(", ", ids) + " from their FTP / e-mail / URL source.", sessionPayload(session)));
            }

            var results = await imports.ImportRemoteAsync(ids, session.UserId, cancellationToken);
            if (results.Count == 1)
            {
                return Answer(context, results[0], sessionPayload(session));
            }

            var ok = results.Any(r => r.Succeeded);
            var message = string.Join(" ", results.Select(r => r.Message));
            return LiveWriteFormBinder.Complete(context, ReturnUrl, ok, message, new Dictionary<string, object?>
            {
                ["status"] = ok,
                ["message"] = message,
                ["list_to_handle"] = results.Select(r => r.ToPayload()).ToList(),
                ["phpAuthoritative"] = false,
                ["session"] = sessionPayload(session),
            });
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.CpPricesUploadHistory, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCsrfGuard csrf,
            IErpWriteConnectionFactory connections,
            IWebHostEnvironment env,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!IsCpAdmin(session))
            {
                return Results.Json(new { status = false, message = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);
            }

            var query = context.Request.Query;
            var action = ((string?)query["action"] ?? "list").Trim();
            var priceId = Long(query["price_id"]);
            var historyId = Long(query["history_id"]);
            if (action != "list")
            {
                var verdict = await csrf.VerifyAsync(context, session, query[CpCsrfGuard.FieldName], cancellationToken);
                if (!verdict.Ok)
                {
                    return Results.Json(new { status = false, message = verdict.Message, validation_code = verdict.Code }, statusCode: StatusCodes.Status400BadRequest);
                }
            }

            if (!connections.IsConfigured)
            {
                return Results.Json(new { status = false, message = "TenantRegistry DB is not configured." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var filesRoot = FilesRoot(env);
            await using var connection = await connections.OpenAsync(cancellationToken);
            await CpPriceUploadHistory.EnsureSchemaAsync(connection, cancellationToken);
            CpPriceDownload? download = action switch
            {
                "download" => await CpPriceHistoryDownloads.SourceFileAsync(connection, filesRoot, historyId, cancellationToken),
                "download_latest" => await CpPriceHistoryDownloads.LatestAsync(connection, filesRoot, priceId, cancellationToken),
                "download_issues" => await CpPriceHistoryDownloads.IssuesAsync(connection, filesRoot, historyId, "all", cancellationToken),
                "download_skipped" => await CpPriceHistoryDownloads.IssuesAsync(connection, filesRoot, historyId, "skipped", cancellationToken),
                "download_errors" => await CpPriceHistoryDownloads.IssuesAsync(connection, filesRoot, historyId, "error", cancellationToken),
                "export_db" => await CpPriceHistoryDownloads.ExportDbAsync(connection, priceId, cancellationToken),
                _ => null,
            };
            if (action == "list")
            {
                var rows = await CpPriceUploadHistory.ListAsync(connection, priceId, (int)Math.Clamp(Long(query["limit"]) is var l && l > 0 ? l : 100, 1, 500), cancellationToken);
                return Results.Json(new { status = true, history = rows });
            }

            if (download is null)
            {
                return Results.Text(
                    action is "download" or "download_latest"
                        ? "Upload file not available: the archived source file for this upload is missing on disk. Use action=export_db to download the current prices."
                        : "Not found",
                    "text/plain; charset=utf-8",
                    statusCode: StatusCodes.Status404NotFound);
            }

            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(download.Content, download.ContentType, download.FileName);
        });

        endpoints.MapPost(EcomAeRoutes.CpPricesCronTasks, async (
            HttpContext context,
            ILegacySessionValidator validator,
            ICpCsrfGuard csrf,
            ICpPriceCronService cron,
            CancellationToken cancellationToken) =>
        {
            var session = await validator.ValidateAsync(context, cancellationToken);
            if (!IsCpAdmin(session))
            {
                return LiveWriteFormBinder.LoginRedirect(context, "/cp/login?returnUrl=" + ReturnUrl, "Admin CP capability required for scheduled updates.");
            }

            var input = await ReadInputAsync(context, cancellationToken);
            var verdict = await csrf.VerifyAsync(context, session, input.TextOrNull(CpCsrfGuard.FieldName), cancellationToken);
            if (!verdict.Ok)
            {
                return Answer(context, false, verdict.Code, verdict.Message, sessionPayload(session));
            }

            var action = input.Text("action");
            if (action is "list" or "get_cron_tasks" or "")
            {
                var schedules = await cron.ListAsync(input.Long("price_id"), cancellationToken);
                return Results.Json(new { status = true, cron_tasks = schedules.Select(s => new { id = s.Id, active = s.Active ? 1 : 0, days = s.Days, time = s.Time, prices = s.PriceIds }) });
            }

            if (action is not ("save" or "create_edit" or "delete" or "delete_cron_task"))
            {
                return Answer(context, false, "unknown_action", "Unknown action", sessionPayload(session));
            }

            if (!input.Flag("confirmWrites", "confirm_writes"))
            {
                return Results.Json(DryRun("Set confirmWrites=1 to " + action + " the scheduled update.", sessionPayload(session)));
            }

            var written = action is "delete" or "delete_cron_task"
                ? await cron.DeleteAsync(input.Long("cron_task_id", "id"), cancellationToken)
                : await cron.SaveAsync(
                    input.Long("cron_task_id", "id") is var id && id > 0 ? id : null,
                    Ids(input.Text("prices", "price_ids")),
                    input.TextOrNull("active"),
                    input.Text("days").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    input.TextOrNull("time"),
                    cancellationToken);
            return LiveWriteFormBinder.Complete(context, ReturnUrl, written.Succeeded, written.Message, new
            {
                status = written.Succeeded,
                ok = written.Succeeded,
                cron_task_id = written.Id,
                writes = written.Writes,
                validation_code = written.Code,
                message = written.Message,
                phpAuthoritative = false,
                session = sessionPayload(session),
            });
        }).DisableAntiforgery();

        endpoints.MapGet(EcomAeRoutes.CpPricesCronTick, async (
            HttpContext context,
            IOptions<PhpReferenceOptions> reference,
            ICpPriceCronService cron,
            IServiceScopeFactory scopes,
            IHostApplicationLifetime lifetime,
            CancellationToken cancellationToken) =>
        {
            if (!CpPhpConfig.TechKeyMatches(CpPhpConfig.Read(reference.Value), context.Request.Query["key"]))
            {
                return Results.Text("No access", statusCode: StatusCodes.Status403Forbidden);
            }

            var launches = await cron.StartDueAsync(DateTimeOffset.Now, cancellationToken);
            var wait = string.Equals(context.Request.Query["wait"], "1", StringComparison.Ordinal);
            if (wait)
            {
                var finished = new List<object>();
                foreach (var launch in launches)
                {
                    finished.Add(new { launch_id = launch.LaunchId, crontab_task_id = launch.CrontabTaskId, list_to_handle = (await cron.ExecuteAsync(launch, cancellationToken)).Select(r => r.ToPayload()) });
                }

                return Results.Json(new { status = true, launches = finished });
            }

            foreach (var launch in launches)
            {
                // cron_crutch.php spawns cron_task_executor.php with "&" so the wget tick returns at once.
                _ = Task.Run(async () =>
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var executor = scope.ServiceProvider.GetRequiredService<ICpPriceCronService>();
                    await executor.ExecuteAsync(launch, lifetime.ApplicationStopping);
                }, CancellationToken.None);
            }

            return Results.Json(new { status = true, launches = launches.Select(l => new { launch_id = l.LaunchId, crontab_task_id = l.CrontabTaskId }) });
        });

        endpoints.Map(EcomAeRoutes.CpPricesDeployApi, async (
            HttpContext context,
            IOptions<PhpReferenceOptions> reference,
            ICpPriceDeployApiService api,
            IErpWriteConnectionFactory connections,
            CancellationToken cancellationToken) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
            {
                return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
            }

            IFormCollection? form = null;
            if (context.Request.HasFormContentType)
            {
                AllowLargeBody(context);
                form = await context.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = MaxUploadBytes }, cancellationToken);
            }

            string Field(string name) => ((string?)form?[name] ?? (string?)context.Request.Query[name] ?? string.Empty).Trim();

            if (!CpPhpConfig.TechKeyMatches(CpPhpConfig.Read(reference.Value), Field("key")))
            {
                return Results.Json(new { status = false, message = "Invalid tech_key" }, statusCode: StatusCodes.Status403Forbidden);
            }

            if (!connections.IsConfigured)
            {
                return Results.Json(new { status = false, message = "DB connect failed" });
            }

            var action = Field("action");
            var priceId = Long(Field("price_id"));
            switch (action)
            {
                case "list_prices":
                    return Results.Json(new { status = true, prices = await api.ListPricesAsync(cancellationToken) });
                case "list_latest_uploads":
                    return Results.Json(new { status = true, uploads = await api.ListLatestUploadsAsync(cancellationToken) });
                case "reupload_latest":
                    return Results.Json(await api.ReuploadLatestAsync(priceId, Field("price_name"), cancellationToken));
                case "" or "upload":
                    var file = form?.Files.GetFile("price_file");
                    if (file is null || file.Length == 0)
                    {
                        return Results.Json(new { status = false, message = "price_file upload required" });
                    }

                    await using (var content = file.OpenReadStream())
                    {
                        return Results.Json(await api.UploadAsync(priceId, Field("price_name"), new CpPriceUpload(file.FileName, content), cancellationToken));
                    }

                default:
                    return Results.Json(new { status = false, message = "Unknown action" });
            }
        }).DisableAntiforgery();
    }

    private static bool IsCpAdmin(LegacySessionContext session)
        => session.Kind == LegacySessionKind.Admin && session.Capabilities.Contains("cp");

    private static async Task<CpCrmActionInput> ReadInputAsync(HttpContext context, CancellationToken cancellationToken)
        => context.Request.HasFormContentType
            ? CpCrmActionInput.FromForm(await context.Request.ReadFormAsync(cancellationToken))
            : await CpCrmActionInput.FromJsonAsync(context, cancellationToken);

    private static void AllowLargeBody(HttpContext context)
    {
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = MaxUploadBytes;
        }
    }

    public static string FilesRoot(IWebHostEnvironment env)
        => Path.Combine(PhpLegacyAssetBridge.FindRepoRoot(env), "content", "files");

    private static Dictionary<string, object?> DryRun(string message, object session) => new(StringComparer.Ordinal)
    {
        ["status"] = "dry-run",
        ["writes"] = 0,
        ["writesBlocked"] = true,
        ["phpAuthoritative"] = true,
        ["validation_code"] = "dry_run",
        ["message"] = message,
        ["session"] = session,
    };

    /// <summary>PHP ajax handlers return JSON. The operator page posts <c>returnUrl</c> and follows the flash redirect.</summary>
    private static bool PreferJson(HttpContext context)
        => !context.Request.HasFormContentType || string.IsNullOrWhiteSpace(context.Request.Form["returnUrl"]);

    private static IResult Answer(HttpContext context, bool ok, string code, string message, object session)
    {
        var payload = new Dictionary<string, object?>
        {
            ["status"] = ok,
            ["ok"] = ok,
            ["validation_code"] = code,
            ["message"] = message,
            ["writes"] = 0,
            ["phpAuthoritative"] = false,
            ["session"] = session,
        };
        if (PreferJson(context))
        {
            return Results.Json(payload);
        }

        return LiveWriteFormBinder.Complete(context, ReturnUrl, ok, message, payload);
    }

    private static IResult Answer(HttpContext context, CpPriceImportResult result, object session)
    {
        var payload = result.ToPayload();
        payload["phpAuthoritative"] = false;
        payload["session"] = session;
        if (PreferJson(context))
        {
            return Results.Json(payload);
        }

        return LiveWriteFormBinder.Complete(context, ReturnUrl, result.Succeeded, result.Message, payload, StatusCodes.Status200OK);
    }

    private static List<long> Ids(string raw)
        => raw.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => long.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();

    private static long Long(string? raw)
        => long.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
