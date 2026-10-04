using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>A supplier file posted by the CP (PC upload / wizard) or the deploy API.</summary>
public sealed record CpPriceUpload(string FileName, Stream Content);

public sealed record CpPriceImportRequest(
    long PriceId,
    string Channel,
    long UploadedBy,
    CpPriceUpload? Upload = null,
    string? SourceRef = null,
    IReadOnlyDictionary<string, object?>? ExtraStats = null);

public sealed record CpPriceImportResult(
    bool Succeeded,
    string Code,
    string Message,
    long PriceId,
    string PriceName,
    string Channel,
    string Status,
    int RowsImported,
    int RowsSkipped,
    long RowsInDb,
    long BrandsCount,
    long HistoryId,
    long TaskId,
    string StoredRelpath,
    string IssuesRelpath,
    string Format,
    string Encoding,
    string Delimiter,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ValidationMessages,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyList<string> OtherMessages)
{
    public static CpPriceImportResult Fail(string code, string message, long priceId = 0, string channel = "")
        => new(false, code, message, priceId, string.Empty, channel, "failed", 0, 0, 0, 0, 0, 0, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, [], [], [message], []);

    public Dictionary<string, object?> ToPayload() => new(StringComparer.Ordinal)
    {
        ["status"] = Succeeded,
        ["ok"] = Succeeded,
        ["validation_code"] = Code,
        ["message"] = Message,
        ["price_id"] = PriceId,
        ["price_name"] = PriceName,
        ["channel"] = Channel,
        ["history_status"] = Status,
        ["records_handled"] = RowsImported,
        ["rows_skipped"] = RowsSkipped,
        ["records_in_db"] = RowsInDb,
        ["brands_count"] = BrandsCount,
        ["history_id"] = HistoryId,
        ["client_task_id"] = TaskId,
        ["stored_file"] = StoredRelpath,
        ["issues_file"] = IssuesRelpath,
        ["format"] = Format,
        ["encoding"] = Encoding,
        ["delimiter"] = Delimiter,
        ["files"] = Files,
        ["validation_messages"] = ValidationMessages,
        ["error_messages"] = ErrorMessages,
        ["other_messages"] = OtherMessages,
        ["writes"] = Succeeded ? RowsImported : 0,
    };
}

/// <summary>
/// Native twin of the PHP/pyprices supplier price-upload pipeline: <c>for_pyprices/upload_file.php</c> (PC),
/// <c>add_new_task.php</c> + <c>pyprices/api.py</c> (FTP / e-mail / URL "update now" and cron), the CP wizard
/// (<c>ajax_5_import_csv_to_db.php</c>) and the deploy API (<c>epc-upload-uae-prices.php</c>). Every channel writes
/// <c>shop_docpart_prices_data</c> (with <c>article_search</c>) in one transaction per list — the old rows are only
/// replaced when at least one new row imports — and records <c>epc_price_upload_history</c>, the archived source file
/// and the issues CSV the CP history panel downloads.
/// </summary>
public interface ICpPriceImportService
{
    /// <summary>Imports an uploaded file (<c>pc</c>, <c>wizard</c>, <c>api</c>, <c>api_reupload</c>).</summary>
    Task<CpPriceImportResult> ImportUploadAsync(CpPriceImportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fetches and imports lists from their own FTP / e-mail / URL source (PHP "update now" and cron).</summary>
    Task<IReadOnlyList<CpPriceImportResult>> ImportRemoteAsync(IReadOnlyList<long> priceIds, long uploadedBy, CancellationToken cancellationToken = default);
}

public sealed class CpPriceImportService : ICpPriceImportService
{
    public const int RowsPerQuery = 500;

    public static readonly IReadOnlyList<string> UploadChannels = ["pc", "wizard", "api", "api_reupload"];

    /// <summary>PHP <c>upload_file.php</c> <c>$file_type_allowed</c>.</summary>
    public static readonly IReadOnlyList<string> PcUploadExtensions = ["csv", "txt", "xls", "xlsx", "rar", "zip", "7z", "tar"];

    /// <summary>Deploy API / wizard accept plain price files only.</summary>
    public static readonly IReadOnlyList<string> PlainUploadExtensions = ["csv", "txt", "xls", "xlsx"];

    private const string EmptyTaskMessage = "After all processing, the list of files for this task is empty. We do not do anything";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpPriceRemoteSources _remote;
    private readonly Func<IReadOnlyDictionary<string, string>> _config;
    private readonly string _filesRoot;
    private readonly string _workRoot;

    public CpPriceImportService(
        IErpWriteConnectionFactory connections,
        ICpPriceRemoteSources remote,
        IOptions<PhpReferenceOptions> reference,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        : this(
            connections,
            remote,
            () => CpPhpConfig.Read(reference.Value),
            Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(env), "content", "files"),
            Path.Combine(Path.GetTempPath(), "ecomae-price-import"))
    {
    }

    public CpPriceImportService(
        IErpWriteConnectionFactory connections,
        ICpPriceRemoteSources remote,
        Func<IReadOnlyDictionary<string, string>> config,
        string filesRoot,
        string workRoot)
    {
        _connections = connections;
        _remote = remote;
        _config = config;
        _filesRoot = filesRoot;
        _workRoot = workRoot;
    }

    public async Task<CpPriceImportResult> ImportUploadAsync(CpPriceImportRequest request, CancellationToken cancellationToken = default)
    {
        var channel = (request.Channel ?? string.Empty).Trim().ToLowerInvariant();
        if (!UploadChannels.Contains(channel))
        {
            return CpPriceImportResult.Fail("invalid", "Unknown upload channel " + channel, request.PriceId, channel);
        }

        if (request.PriceId <= 0)
        {
            return CpPriceImportResult.Fail("invalid", "A price list id is required.", request.PriceId, channel);
        }

        if (request.Upload is null)
        {
            return CpPriceImportResult.Fail("invalid", "price_file upload required", request.PriceId, channel);
        }

        var originalName = Path.GetFileName((request.Upload.FileName ?? string.Empty).Replace('\\', '/'));
        var extension = PriceFileReader.ExtensionOf(originalName);
        if (extension == "noext")
        {
            return CpPriceImportResult.Fail("invalid", "File has no extension", request.PriceId, channel);
        }

        var allowed = channel == "pc" ? PcUploadExtensions : PlainUploadExtensions;
        if (!allowed.Contains(extension))
        {
            return CpPriceImportResult.Fail("invalid", "File has incompatible type", request.PriceId, channel);
        }

        if (!_connections.IsConfigured)
        {
            return CpPriceImportResult.Fail("db", "TenantRegistry DB is not configured.", request.PriceId, channel);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var list = await CpPriceListConfig.LoadAsync(connection, request.PriceId, cancellationToken).ConfigureAwait(false);
        if (list is null)
        {
            return CpPriceImportResult.Fail("not_found", "No such price", request.PriceId, channel);
        }

        await CpPriceUploadHistory.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var task = new ImportTask(list, channel, NewWorkDirectory());
        if (!await TryLockAsync(connection, list.Id, cancellationToken).ConfigureAwait(false))
        {
            return CpPriceImportResult.Fail("busy", "Price list ID " + list.Id.ToString(CultureInfo.InvariantCulture) + " is already being updated. Wait until the running update finishes.", list.Id, channel);
        }

        try
        {
            var uploadPath = Path.Combine(task.UploadDirectory, originalName);
            Directory.CreateDirectory(task.UploadDirectory);
            await using (var target = File.Create(uploadPath))
            {
                await request.Upload.Content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            var storedRelpath = CpPriceUploadHistory.ArchiveFile(_filesRoot, uploadPath, list.Id, originalName);
            task.StoredRelpath = storedRelpath;
            task.ArchivedOriginal = storedRelpath.Length > 0;
            task.HistoryId = await CpPriceUploadHistory.SaveAsync(
                connection,
                new CpPriceHistoryRow(
                    list.Id,
                    list.Name,
                    CpPriceUploadHistory.UploadSourceForChannel(channel),
                    request.SourceRef ?? DefaultSourceRef(channel, list.Id),
                    originalName,
                    storedRelpath,
                    new FileInfo(uploadPath).Length,
                    "pending",
                    request.UploadedBy,
                    ErrorText: storedRelpath.Length == 0 ? "Warning: source file archive failed at upload time; download may use DB export after import." : ""),
                cancellationToken).ConfigureAwait(false);

            task.Validation.AddRange(list.Validate(channel));
            if (task.Validation.Count == 0)
            {
                AcceptLocalFile(task, uploadPath, originalName, applyNameFilter: channel == "pc");
                await ImportFilesAsync(connection, task, cancellationToken).ConfigureAwait(false);
            }

            return await FinishAsync(connection, task, request.ExtraStats, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await ReleaseLockAsync(connection, list.Id, CancellationToken.None).ConfigureAwait(false);
            DeleteDirectory(task.Root);
        }
    }

    public async Task<IReadOnlyList<CpPriceImportResult>> ImportRemoteAsync(IReadOnlyList<long> priceIds, long uploadedBy, CancellationToken cancellationToken = default)
    {
        var ids = priceIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [CpPriceImportResult.Fail("invalid", "A price list id is required.")];
        }

        if (!_connections.IsConfigured)
        {
            return [CpPriceImportResult.Fail("db", "TenantRegistry DB is not configured.")];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpPriceUploadHistory.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var config = _config();
        var mail = CpPriceMailSettings.FromConfig(config);
        var results = new List<CpPriceImportResult>();
        var tasks = new List<ImportTask>();
        try
        {
            foreach (var id in ids)
            {
                var list = await CpPriceListConfig.LoadAsync(connection, id, cancellationToken).ConfigureAwait(false);
                if (list is null)
                {
                    results.Add(CpPriceImportResult.Fail("not_found", "Прайс-лист с ID " + id.ToString(CultureInfo.InvariantCulture) + " не найден", id));
                    continue;
                }

                if (list.LoadMode == CpPriceListConfig.LoadModePc)
                {
                    results.Add(CpPriceImportResult.Fail("invalid", "Price list \"" + list.Name + "\" is updated by PC upload; it has no FTP / e-mail / URL source.", id, "pc"));
                    continue;
                }

                if (!await TryLockAsync(connection, list.Id, cancellationToken).ConfigureAwait(false))
                {
                    results.Add(CpPriceImportResult.Fail("busy", "Price list ID " + id.ToString(CultureInfo.InvariantCulture) + " is already being updated. Wait until the running update finishes.", id, list.RemoteChannel));
                    continue;
                }

                var task = new ImportTask(list, list.RemoteChannel, NewWorkDirectory()) { Locked = true };
                tasks.Add(task);
                task.TaskId = await BeginRemoteTaskAsync(connection, task, uploadedBy, cancellationToken).ConfigureAwait(false);
                task.Validation.AddRange(list.Validate(task.Channel));
                if (task.Channel == "email" && !mail.IsConfigured)
                {
                    task.Validation.Add("The object cannot be processed due to lack of connection to the IMAP mail server");
                }
            }

            foreach (var task in tasks.Where(t => t.Validation.Count == 0 && t.Channel is "ftp" or "url"))
            {
                try
                {
                    if (task.Channel == "url")
                    {
                        await FetchUrlAsync(task, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await FetchFtpAsync(task, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    task.Errors.Add((task.Channel == "url" ? "An error occurred while trying to get a file from a URL: " : string.Empty) + ex.Message);
                }
            }

            foreach (var group in tasks.Where(t => t.Validation.Count == 0 && t.Channel == "email")
                         .GroupBy(t => t.List.SenderEmail.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    await FetchEmailGroupAsync(mail, group.Key, group.ToList(), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    foreach (var task in group)
                    {
                        task.Errors.Add("When receiving files from E-mail from " + group.Key + " an error has occurred: " + ex.Message);
                    }
                }
            }

            foreach (var task in tasks)
            {
                if (task.Validation.Count == 0)
                {
                    await ImportFilesAsync(connection, task, cancellationToken).ConfigureAwait(false);
                }

                results.Add(await FinishAsync(connection, task, null, cancellationToken).ConfigureAwait(false));
            }
        }
        finally
        {
            foreach (var task in tasks)
            {
                if (task.Locked)
                {
                    await ReleaseLockAsync(connection, task.List.Id, CancellationToken.None).ConfigureAwait(false);
                }

                DeleteDirectory(task.Root);
            }
        }

        return results;
    }

    private async Task<long> BeginRemoteTaskAsync(DbConnection connection, ImportTask task, long uploadedBy, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_docpart_pyprices_tasks` (`time_created`, `price_id`) VALUES (?, ?)"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            task.List.Id).ConfigureAwait(false);
        var taskId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var label = CpPriceUploadHistory.ChannelLabel(task.Channel);
        task.HistoryId = await CpPriceUploadHistory.SaveAsync(
            connection,
            new CpPriceHistoryRow(
                task.List.Id,
                task.List.Name,
                CpPriceUploadHistory.UploadSourceForChannel(task.Channel),
                "task_" + taskId.ToString(CultureInfo.InvariantCulture),
                label + " — " + (task.List.Name.Length > 0 ? task.List.Name : "price #" + task.List.Id.ToString(CultureInfo.InvariantCulture)),
                string.Empty,
                0,
                "pending",
                uploadedBy),
            cancellationToken).ConfigureAwait(false);
        return taskId;
    }

    /// <summary>pyprices <c>file_receiver_local_path.get_file</c>.</summary>
    private static void AcceptLocalFile(ImportTask task, string path, string fileName, bool applyNameFilter)
    {
        Directory.CreateDirectory(task.FilesDirectory);
        if (PriceFileReader.IsArchive(fileName))
        {
            if (applyNameFilter && !PriceFileNameFilter.Matches(fileName, task.List.FileNameSubstringArch))
            {
                task.Errors.Add("The file name did not match");
                return;
            }

            ExtractInto(task, path, applyNameFilter);
            return;
        }

        if (applyNameFilter && !PriceFileNameFilter.Matches(fileName, task.List.FileNameSubstring))
        {
            task.Errors.Add("The file name did not match");
            return;
        }

        File.Copy(path, Path.Combine(task.FilesDirectory, fileName), overwrite: true);
    }

    private static void ExtractInto(ImportTask task, string archivePath, bool applyNameFilter)
    {
        try
        {
            PriceArchiveExtractor.Extract(archivePath, task.FilesDirectory, applyNameFilter ? task.List.FileNameSubstring : null, task.Other);
        }
        catch (PriceFileFormatException ex)
        {
            task.Errors.Add(ex.Message);
        }
        finally
        {
            if (archivePath.StartsWith(task.FilesDirectory, StringComparison.Ordinal))
            {
                File.Delete(archivePath);
            }
        }
    }

    /// <summary>pyprices <c>file_receiver_url.get_file</c>.</summary>
    private async Task FetchUrlAsync(ImportTask task, CancellationToken cancellationToken)
    {
        var url = new Uri(task.List.Link.Trim(), UriKind.Absolute);
        var fileName = Path.GetFileName(Uri.UnescapeDataString(url.AbsolutePath));
        var extension = PriceFileReader.ExtensionOf(fileName);
        if (!PriceFileReader.IsSuitable(fileName) && !PriceFileReader.IsArchive(fileName))
        {
            throw new InvalidOperationException("The file specified in the URL has incorrect format: " + extension);
        }

        var archive = PriceFileReader.IsArchive(fileName);
        if (!PriceFileNameFilter.Matches(fileName, archive ? task.List.FileNameSubstringArch : task.List.FileNameSubstring))
        {
            throw new InvalidOperationException("The file name did not match: " + fileName);
        }

        Directory.CreateDirectory(task.FilesDirectory);
        var destination = Path.Combine(task.FilesDirectory, fileName);
        await _remote.DownloadUrlAsync(url, destination, task.Other, cancellationToken).ConfigureAwait(false);
        if (!File.Exists(destination))
        {
            throw new InvalidOperationException("Failed to download file");
        }

        if (archive)
        {
            ExtractInto(task, destination, applyNameFilter: true);
        }
    }

    /// <summary>pyprices <c>file_receiver_ftp.get_file</c>.</summary>
    private async Task FetchFtpAsync(ImportTask task, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(task.FilesDirectory);
        var downloadDirectory = Path.Combine(task.Root, "ftp");
        Directory.CreateDirectory(downloadDirectory);
        var files = await _remote.FetchFtpAsync(
            task.List,
            name =>
            {
                if (!PriceFileReader.IsSuitable(name) && !PriceFileReader.IsArchive(name))
                {
                    task.Other.Add("File on FTP with file name [" + name + "] has incorrect type");
                    return false;
                }

                var substring = PriceFileReader.IsArchive(name) ? task.List.FileNameSubstringArch : task.List.FileNameSubstring;
                if (!PriceFileNameFilter.Matches(name, substring))
                {
                    task.Other.Add("File on FTP with file name [" + name + "] does not match by name");
                    return false;
                }

                return true;
            },
            downloadDirectory,
            task.Other,
            cancellationToken).ConfigureAwait(false);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (PriceFileReader.IsArchive(name))
            {
                ExtractInto(task, file, applyNameFilter: true);
            }
            else
            {
                File.Move(file, UniquePath(task.FilesDirectory, name));
            }
        }
    }

    /// <summary>pyprices <c>file_receiver_email.get_files_for_tasks</c>: one mailbox read per sender, shared by its lists.</summary>
    private async Task FetchEmailGroupAsync(CpPriceMailSettings mail, string sender, IReadOnlyList<ImportTask> group, CancellationToken cancellationToken)
    {
        void ToAll(string message)
        {
            foreach (var task in group)
            {
                task.Other.Add(message);
            }
        }

        foreach (var task in group)
        {
            Directory.CreateDirectory(task.FilesDirectory);
        }

        var messages = await _remote.FetchMailAsync(mail, sender, !group[0].List.NotMarkSeenEmailMessages, cancellationToken).ConfigureAwait(false);
        if (messages.Count == 0)
        {
            ToAll("No new messages from " + sender);
            return;
        }

        foreach (var message in messages)
        {
            ToAll("Processing a letter from " + message.From + " received " + message.Date.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture));
            if (message.Attachments.Count == 0)
            {
                ToAll("The letter without attachments");
                continue;
            }

            foreach (var attachment in message.Attachments)
            {
                var fileName = Path.GetFileName(attachment.FileName.Replace('\\', '/'));
                ToAll("A file was found in the letter: " + fileName);
                if (!PriceFileReader.IsSuitable(fileName) && !PriceFileReader.IsArchive(fileName))
                {
                    ToAll("This file has incorrect format " + PriceFileReader.ExtensionOf(fileName));
                    continue;
                }

                foreach (var task in group)
                {
                    var subjectFilter = task.List.MessageHeaderSubstring;
                    if (subjectFilter.Length > 0 && !message.Subject.Contains(subjectFilter, StringComparison.Ordinal))
                    {
                        task.Other.Add("The file does not match the subject of the letter. Letter subject: [" + message.Subject + "], filter in price list settings: [" + subjectFilter + "]");
                        continue;
                    }

                    var archive = PriceFileReader.IsArchive(fileName);
                    if (!PriceFileNameFilter.Matches(fileName, archive ? task.List.FileNameSubstringArch : task.List.FileNameSubstring))
                    {
                        task.Other.Add((archive ? "File (archive) named [" : "File (not archive) named [") + fileName + "] does not match the name");
                        continue;
                    }

                    var destination = UniquePath(task.FilesDirectory, fileName);
                    await File.WriteAllBytesAsync(destination, attachment.Content, cancellationToken).ConfigureAwait(false);
                    task.Other.Add("File [" + fileName + "] sucessfully downloaded from the message");
                    if (archive)
                    {
                        ExtractInto(task, destination, applyNameFilter: true);
                    }
                }
            }
        }
    }

    /// <summary>pyprices <c>api.py</c> import loop + <c>PriceFileHandler</c>, in one transaction per list.</summary>
    private async Task ImportFilesAsync(DbConnection connection, ImportTask task, CancellationToken cancellationToken)
    {
        var files = Directory.Exists(task.FilesDirectory)
            ? Directory.GetFiles(task.FilesDirectory).OrderBy(f => f, StringComparer.Ordinal).ToList()
            : [];
        if (files.Count == 0)
        {
            task.Errors.Add(EmptyTaskMessage);
            return;
        }

        if (!task.ArchivedOriginal)
        {
            task.StoredRelpath = CpPriceUploadHistory.ArchiveFile(_filesRoot, files[0], task.List.Id, Path.GetFileName(files[0]));
            task.StoredFileSize = new FileInfo(files[0]).Length;
            task.StoredFileName = Path.GetFileName(files[0]);
            if (files.Count > 1)
            {
                task.Other.Add("History keeps a copy of " + task.StoredFileName + "; " + (files.Count - 1).ToString(CultureInfo.InvariantCulture) + " more file(s) were imported in the same run.");
            }
        }

        var hasArticleSearch = await HasArticleSearchColumnAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (task.List.CleanBefore)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = ?"),
                cancellationToken,
                task.List.Id).ConfigureAwait(false);
        }

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            task.Files.Add(fileName);
            var log = new PriceFileReadLog();
            var fileIssues = new List<PriceImportIssue>();
            var batch = new List<(long Line, PriceImportRow Row)>(RowsPerQuery);
            var fileImported = 0;
            try
            {
                foreach (var source in PriceFileReader.ReadRows(file, fileName, task.List.Encoding, task.List.StringsToLeft, log))
                {
                    if (source.SkipReason is not null)
                    {
                        task.RowsSkipped++;
                        fileIssues.Add(new PriceImportIssue(source.LineNo, "skipped", source.SkipReason, Prefix(files.Count, fileName, source.SkipDetails), source.Cells));
                        continue;
                    }

                    var outcome = PriceRecordNormalizer.Normalize(source.Cells, task.List.Columns);
                    if (outcome.Row is null)
                    {
                        task.RowsSkipped++;
                        fileIssues.Add(new PriceImportIssue(source.LineNo, "skipped", outcome.SkipReason ?? "error", Prefix(files.Count, fileName, outcome.Details), source.Cells, outcome.Parsed));
                        continue;
                    }

                    batch.Add((source.LineNo, outcome.Row));
                    if (batch.Count >= RowsPerQuery)
                    {
                        fileImported += await InsertBatchAsync(connection, transaction, task, batch, hasArticleSearch, fileName, fileIssues, cancellationToken).ConfigureAwait(false);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    fileImported += await InsertBatchAsync(connection, transaction, task, batch, hasArticleSearch, fileName, fileIssues, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is PriceFileFormatException or IOException or InvalidDataException or DecoderFallbackException
                                           or ExcelDataReader.Exceptions.ExcelReaderException)
            {
                task.Other.Add(fileName + ": " + ex.Message);
            }

            task.RowsImported += fileImported;
            task.Other.AddRange(log.Messages);
            task.Other.Add("File [" + fileName + "] handled: " + fileImported.ToString(CultureInfo.InvariantCulture) + " rows imported"
                           + (log.Format.Length > 0 ? ", format " + log.Format : string.Empty)
                           + (log.Encoding.Length > 0 ? ", encoding " + log.Encoding : string.Empty)
                           + (log.Delimiter.Length > 0 ? ", delimiter " + DelimiterLabel(log.Delimiter) : string.Empty));
            task.Format = task.Format.Length > 0 ? task.Format : log.Format;
            task.Encoding = task.Encoding.Length > 0 ? task.Encoding : log.Encoding;
            task.Delimiter = task.Delimiter.Length > 0 ? task.Delimiter : log.Delimiter;
            var labels = PriceImportIssues.ColumnLabels(log.HeaderRow, task.List.Columns);
            foreach (var issue in fileIssues)
            {
                if (task.IssueFields.Count >= CpPriceUploadHistory.MaxIssueRows)
                {
                    task.IssuesTruncated = true;
                    break;
                }

                task.IssueFields.Add(PriceImportIssues.ToFields(issue, labels));
            }
        }

        if (task.RowsImported > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                task.List.Id).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.TryExecuteAsync(
                connection,
                "UPDATE `shop_docpart_prices` SET `records_count` = (SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = "
                + task.List.Id.ToString(CultureInfo.InvariantCulture) + ") WHERE `id` = " + task.List.Id.ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<int> InsertBatchAsync(
        DbConnection connection,
        DbTransaction transaction,
        ImportTask task,
        List<(long Line, PriceImportRow Row)> batch,
        bool hasArticleSearch,
        string fileName,
        List<PriceImportIssue> fileIssues,
        CancellationToken cancellationToken)
    {
        var columns = hasArticleSearch
            ? "`price_id`,`manufacturer`,`article`,`article_search`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`"
            : "`price_id`,`manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`";
        var tuple = hasArticleSearch ? "(?,?,?,?,?,?,?,?,?,?,?)" : "(?,?,?,?,?,?,?,?,?,?)";
        var sql = new StringBuilder("INSERT INTO `shop_docpart_prices_data` (").Append(columns).Append(") VALUES ");
        var values = new List<object?>(batch.Count * 11);
        for (var i = 0; i < batch.Count; i++)
        {
            if (i > 0)
            {
                sql.Append(',');
            }

            sql.Append(tuple);
            var row = batch[i].Row;
            values.Add(task.List.Id);
            values.Add(row.Manufacturer);
            values.Add(row.Article);
            if (hasArticleSearch)
            {
                values.Add(row.ArticleSearch);
            }

            values.Add(row.ArticleShow);
            values.Add(row.Name);
            values.Add(row.Exist);
            values.Add(row.Price);
            values.Add(row.TimeToExe);
            values.Add(row.Storage);
            values.Add(row.MinOrder);
        }

        try
        {
            return await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional(sql.ToString()), cancellationToken, values.ToArray()).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            var first = batch[0].Line.ToString(CultureInfo.InvariantCulture);
            var last = batch[^1].Line.ToString(CultureInfo.InvariantCulture);
            task.Errors.Add("Rows " + first + "–" + last + " of " + fileName + " were not written: " + ex.Message);
            task.RowsSkipped += batch.Count;
            foreach (var (line, _) in batch)
            {
                fileIssues.Add(new PriceImportIssue(line, "error", "import_error", ex.Message));
            }

            return 0;
        }
    }

    private async Task<CpPriceImportResult> FinishAsync(DbConnection connection, ImportTask task, IReadOnlyDictionary<string, object?>? extraStats, CancellationToken cancellationToken)
    {
        var errors = task.Validation.Concat(task.Errors).ToList();
        if (task.Validation.Count == 0 && task.RowsImported == 0 && task.Errors.Count == 0)
        {
            task.Errors.Add("No rows were imported: every row was skipped or the file(s) could not be read.");
            errors.Add(task.Errors[^1]);
        }

        var status = task.RowsImported > 0
            ? errors.Count == 0 && task.RowsSkipped == 0 ? "ok" : "partial"
            : "failed";
        var errorText = string.Join("\n", errors.Take(20));
        var stats = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["validation_messages"] = task.Validation,
            ["error_messages"] = task.Errors,
            ["other_messages"] = task.Other,
            ["channel"] = task.Channel,
            ["files"] = task.Files,
            ["format"] = task.Format,
            ["encoding"] = task.Encoding,
            ["delimiter"] = task.Delimiter,
            ["rows_skipped"] = task.RowsSkipped,
            ["engine"] = "aspnet",
        };
        if (task.TaskId > 0)
        {
            stats["client_task_id"] = task.TaskId;
        }

        if (extraStats is not null)
        {
            foreach (var (key, value) in extraStats)
            {
                stats[key] = value;
            }
        }

        var statsJson = JsonSerializer.Serialize(stats, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var issueFields = new List<Dictionary<string, string>>(task.IssueFields);
        var moduleLine = 0;
        if (errorText.Length > 0)
        {
            issueFields.Insert(0, PriceImportIssues.ToFields(new PriceImportIssue(0, "error", "import_error", errorText), []));
        }

        foreach (var (messages, reason) in new[] { (task.Validation, "validation"), (task.Errors, "error"), (task.Other, "info") })
        {
            foreach (var text in messages)
            {
                moduleLine++;
                issueFields.Add(PriceImportIssues.ToFields(new PriceImportIssue(moduleLine, "error", reason, text), []));
            }
        }

        if (task.IssuesTruncated)
        {
            issueFields.Add(PriceImportIssues.ToFields(new PriceImportIssue(0, "error", "info", "Only the first " + CpPriceUploadHistory.MaxIssueRows.ToString(CultureInfo.InvariantCulture) + " skipped rows are listed."), []));
        }

        var issuesRelpath = CpPriceUploadHistory.WriteIssuesFile(_filesRoot, task.List.Id, task.HistoryId, issueFields);
        var rowsInDb = await CpPriceUploadHistory.CountItemsAsync(connection, task.List.Id, cancellationToken).ConfigureAwait(false);
        var brands = await CpPriceUploadHistory.CountBrandsAsync(connection, task.List.Id, cancellationToken).ConfigureAwait(false);
        await CpPriceUploadHistory.CompleteAsync(
            connection,
            task.HistoryId,
            task.List.Id,
            new CpPriceHistoryOutcome(
                task.RowsImported,
                task.RowsSkipped,
                rowsInDb,
                brands,
                status,
                errorText,
                statsJson,
                task.ArchivedOriginal ? null : task.StoredRelpath,
                task.StoredFileSize,
                task.StoredFileName,
                issuesRelpath),
            cancellationToken).ConfigureAwait(false);

        var count = task.RowsImported.ToString(CultureInfo.InvariantCulture);
        var message = status switch
        {
            "ok" => "Imported " + count + " rows into \"" + task.List.Name + "\".",
            "partial" => "Imported " + count + " rows into \"" + task.List.Name + "\"; " + task.RowsSkipped.ToString(CultureInfo.InvariantCulture)
                         + " rows skipped" + (errors.Count > 0 ? ", " + errors[0] : string.Empty) + ". Download the issues file for details.",
            _ => "Price list \"" + task.List.Name + "\" was not updated: " + (errors.FirstOrDefault() ?? "no rows imported") + (task.List.CleanBefore ? " Existing rows were kept." : string.Empty),
        };
        return new CpPriceImportResult(
            task.RowsImported > 0,
            task.RowsImported > 0 ? "ok" : task.Validation.Count > 0 ? "invalid" : "failed",
            message,
            task.List.Id,
            task.List.Name,
            task.Channel,
            status,
            task.RowsImported,
            task.RowsSkipped,
            rowsInDb,
            brands,
            task.HistoryId,
            task.TaskId,
            task.StoredRelpath,
            issuesRelpath,
            task.Format,
            task.Encoding,
            task.Delimiter,
            task.Files,
            task.Validation,
            task.Errors,
            task.Other);
    }

    private static async Task<bool> HasArticleSearchColumnAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ScalarAsync(connection, null, "SELECT `article_search` FROM `shop_docpart_prices_data` LIMIT 1", cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    /// <summary>One running update per list across both runtimes' native workers (MySQL named lock).</summary>
    private static async Task<bool> TryLockAsync(DbConnection connection, long priceId, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT GET_LOCK(?, 0)"), cancellationToken, LockName(priceId)).ConfigureAwait(false) == 1;
        }
        catch (DbException)
        {
            return true;
        }
    }

    private static async Task ReleaseLockAsync(DbConnection connection, long priceId, CancellationToken cancellationToken)
        => await ErpDb.TryExecuteAsync(connection, "SELECT RELEASE_LOCK('" + LockName(priceId) + "')", cancellationToken).ConfigureAwait(false);

    public static string LockName(long priceId) => "epc_price_import_" + priceId.ToString(CultureInfo.InvariantCulture);

    /// <summary>PHP <c>upload_file.php</c> <c>price_manual_upload_{id}_{time}_{rand}</c> (and per-channel equivalents).</summary>
    public static string DefaultSourceRef(string channel, long priceId)
    {
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var rand = RandomNumberGenerator.GetInt32(1, 5001).ToString(CultureInfo.InvariantCulture);
        var id = priceId.ToString(CultureInfo.InvariantCulture);
        return channel switch
        {
            "pc" => "price_manual_upload_" + id + "_" + stamp + "_" + rand,
            "wizard" => "cp_wizard_" + id + "_" + stamp + "_" + rand,
            _ => string.Empty,
        };
    }

    private static string Prefix(int fileCount, string fileName, string details)
        => fileCount > 1 ? "[" + fileName + "] " + details : details;

    public static string DelimiterLabel(string delimiter) => delimiter switch
    {
        "\t" => "TAB",
        ";" => ";",
        "," => ",",
        _ => delimiter,
    };

    private string NewWorkDirectory()
        => Path.Combine(_workRoot, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N"));

    private static string UniquePath(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        var n = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, n.ToString(CultureInfo.InvariantCulture) + "_" + name);
            n++;
        }

        return candidate;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class ImportTask(CpPriceListConfig list, string channel, string root)
    {
        public CpPriceListConfig List { get; } = list;
        public string Channel { get; } = channel;
        public string Root { get; } = root;
        public string UploadDirectory => Path.Combine(Root, "upload");
        public string FilesDirectory => Path.Combine(Root, "files");
        public bool Locked { get; set; }
        public long TaskId { get; set; }
        public long HistoryId { get; set; }
        public bool ArchivedOriginal { get; set; }
        public string StoredRelpath { get; set; } = string.Empty;
        public long StoredFileSize { get; set; }
        public string? StoredFileName { get; set; }
        public List<string> Validation { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Other { get; } = [];
        public List<string> Files { get; } = [];
        public List<Dictionary<string, string>> IssueFields { get; } = [];
        public bool IssuesTruncated { get; set; }
        public int RowsImported { get; set; }
        public int RowsSkipped { get; set; }
        public string Format { get; set; } = string.Empty;
        public string Encoding { get; set; } = string.Empty;
        public string Delimiter { get; set; } = string.Empty;
    }
}

/// <summary>Reads PHP <c>config.php</c> from <see cref="PhpReferenceOptions.PhpDocRoot"/> (or <c>ECOMAE_PHP_DOCROOT</c>).</summary>
public static class CpPhpConfig
{
    public static IReadOnlyDictionary<string, string> Read(PhpReferenceOptions reference)
    {
        var root = (reference.PhpDocRoot ?? string.Empty).Trim();
        if (root.Length == 0)
        {
            root = (Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
        }

        var path = root.Length == 0 ? string.Empty : Path.Combine(root, "config.php");
        if (path.Length == 0 || !File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return PhpConfigFile.Values(PhpConfigFile.Parse(File.ReadAllText(path)));
        }
        catch (IOException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (UnauthorizedAccessException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>Constant-time comparison against config.php <c>tech_key</c>; an unset key never matches.</summary>
    public static bool TechKeyMatches(IReadOnlyDictionary<string, string> config, string? supplied)
    {
        if (!config.TryGetValue("tech_key", out var expected) || expected.Length == 0 || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
}
