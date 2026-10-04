using System.Globalization;
using System.Net;
using System.Net.Security;
using FluentFTP;
using FluentFTP.Exceptions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>config.php <c>prices_email_*</c> (CP "Mail for price-list upload").</summary>
public sealed record CpPriceMailSettings(string Server, string Encryption, int Port, string Username, string Password)
{
    public bool IsConfigured => Server.Trim().Length > 0 && Username.Trim().Length > 0;

    public static CpPriceMailSettings FromConfig(IReadOnlyDictionary<string, string> config)
    {
        string Value(string key) => config.TryGetValue(key, out var v) ? v : string.Empty;
        var port = int.TryParse(Value("prices_email_port").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : 993;
        return new CpPriceMailSettings(
            Value("prices_email_server").Trim(),
            Value("prices_email_encryption").Trim(),
            port,
            Value("prices_email_username").Trim(),
            Value("prices_email_password"));
    }
}

public sealed record CpPriceMailAttachment(string FileName, byte[] Content);

public sealed record CpPriceMailMessage(string Uid, DateTimeOffset Date, string From, string Subject, IReadOnlyList<CpPriceMailAttachment> Attachments);

/// <summary>
/// pyprices <c>file_receiver_url</c> / <c>file_receiver_ftp</c> / <c>docpart_imap</c> transports. The import service
/// owns every file-type / file-name decision; implementations only move bytes.
/// </summary>
public interface ICpPriceRemoteSources
{
    /// <summary>Downloads <paramref name="url"/> into <paramref name="destinationPath"/>.</summary>
    Task DownloadUrlAsync(Uri url, string destinationPath, ICollection<string> messages, CancellationToken cancellationToken);

    /// <summary>Lists the FTP folder and downloads each name <paramref name="wanted"/> accepts into <paramref name="targetDirectory"/>.</summary>
    Task<IReadOnlyList<string>> FetchFtpAsync(
        CpPriceListConfig list,
        Func<string, bool> wanted,
        string targetDirectory,
        ICollection<string> messages,
        CancellationToken cancellationToken);

    /// <summary>Unseen INBOX messages from <paramref name="sender"/>, optionally marked seen.</summary>
    Task<IReadOnlyList<CpPriceMailMessage>> FetchMailAsync(CpPriceMailSettings settings, string sender, bool markSeen, CancellationToken cancellationToken);
}

public sealed class CpPriceRemoteSources : ICpPriceRemoteSources
{
    /// <summary>pyprices sends a desktop browser UA because some supplier hosts refuse robots.</summary>
    public const string BrowserUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_9_3) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.1916.47 Safari/537.36";

    public const string HttpClientName = "cp-price-url";
    public const string InsecureHttpClientName = "cp-price-url-insecure";

    /// <summary>Largest supplier file accepted from any remote source.</summary>
    public const long MaxDownloadBytes = 512L * 1024 * 1024;

    private readonly IHttpClientFactory _httpClients;

    public CpPriceRemoteSources(IHttpClientFactory httpClients)
    {
        _httpClients = httpClients;
    }

    public async Task DownloadUrlAsync(Uri url, string destinationPath, ICollection<string> messages, CancellationToken cancellationToken)
    {
        try
        {
            await DownloadAsync(_httpClients.CreateClient(HttpClientName), url, destinationPath, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            // pyprices always disables certificate checks; retrying only after a TLS failure keeps that reach while
            // recording that the supplier link is not verified.
            messages.Add("TLS certificate of " + url.Host + " could not be verified; downloaded without certificate validation (pyprices behaviour).");
            await DownloadAsync(_httpClients.CreateClient(InsecureHttpClientName), url, destinationPath, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task DownloadAsync(HttpClient client, Uri url, string destinationPath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("HTTP Error " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + ": " + response.ReasonPhrase);
        }

        if (response.Content.Headers.ContentLength is > MaxDownloadBytes)
        {
            throw new HttpRequestException("The file is larger than the " + (MaxDownloadBytes / 1024 / 1024).ToString(CultureInfo.InvariantCulture) + " MB limit");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(destinationPath);
        await CopyLimitedAsync(source, target, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> FetchFtpAsync(
        CpPriceListConfig list,
        Func<string, bool> wanted,
        string targetDirectory,
        ICollection<string> messages,
        CancellationToken cancellationToken)
    {
        var host = list.FtpHost.Trim();
        var port = 21;
        var colon = host.LastIndexOf(':');
        if (colon > 0 && int.TryParse(host[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
        {
            port = parsedPort;
            host = host[..colon];
        }

        var downloaded = new List<string>();
        await using var client = new AsyncFtpClient(host, list.FtpUser, list.FtpPassword, port);
        client.Config.EncryptionMode = FtpEncryptionMode.None;
        client.Encoding = System.Text.Encoding.UTF8;
        await client.Connect(cancellationToken).ConfigureAwait(false);
        if (list.FtpFolder.Trim().Length > 0)
        {
            await client.SetWorkingDirectory(list.FtpFolder.Trim(), cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in await client.GetNameListing(cancellationToken).ConfigureAwait(false))
        {
            var name = Path.GetFileName(entry.Replace('\\', '/').TrimEnd('/'));
            if (name.Length == 0 || !wanted(name))
            {
                continue;
            }

            var local = Path.Combine(targetDirectory, name);
            try
            {
                var status = await client.DownloadFile(local, name, FtpLocalExists.Overwrite, FtpVerify.None, null, cancellationToken).ConfigureAwait(false);
                if (status == FtpStatus.Failed || !File.Exists(local))
                {
                    messages.Add("Failed to download file from FTP [" + name + "]");
                    continue;
                }

                if (new FileInfo(local).Length > MaxDownloadBytes)
                {
                    File.Delete(local);
                    messages.Add("File on FTP [" + name + "] is larger than the download limit");
                    continue;
                }

                messages.Add((PriceFileReader.IsArchive(name) ? "DOWNLOADED file (archive) from FTP with the name [" : "File downloaded from FTP and has file name [") + name + "]");
                downloaded.Add(local);
            }
            catch (FtpException)
            {
                messages.Add("Failed to download file from FTP [" + name + "]");
            }
        }

        await client.Disconnect(cancellationToken).ConfigureAwait(false);
        return downloaded;
    }

    public async Task<IReadOnlyList<CpPriceMailMessage>> FetchMailAsync(CpPriceMailSettings settings, string sender, bool markSeen, CancellationToken cancellationToken)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(settings.Server, settings.Port, SocketOptions(settings), cancellationToken).ConfigureAwait(false);
        await client.AuthenticateAsync(new NetworkCredential(settings.Username, settings.Password), cancellationToken).ConfigureAwait(false);
        var inbox = client.Inbox;
        await inbox.OpenAsync(markSeen ? FolderAccess.ReadWrite : FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
        var uids = await inbox.SearchAsync(SearchQuery.FromContains(sender).And(SearchQuery.NotSeen), cancellationToken).ConfigureAwait(false);
        var messages = new List<CpPriceMailMessage>(uids.Count);
        foreach (var uid in uids)
        {
            var message = await inbox.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            var attachments = new List<CpPriceMailAttachment>();
            foreach (var part in message.Attachments.OfType<MimePart>())
            {
                var name = part.FileName;
                if (string.IsNullOrWhiteSpace(name) || part.Content is null)
                {
                    continue;
                }

                using var buffer = new MemoryStream();
                await part.Content.DecodeToAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (buffer.Length <= MaxDownloadBytes)
                {
                    attachments.Add(new CpPriceMailAttachment(name, buffer.ToArray()));
                }
            }

            if (markSeen)
            {
                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken).ConfigureAwait(false);
            }

            messages.Add(new CpPriceMailMessage(
                uid.Id.ToString(CultureInfo.InvariantCulture),
                message.Date,
                message.From.ToString(),
                message.Subject ?? string.Empty,
                attachments));
        }

        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        return messages;
    }

    /// <summary>pyprices <c>MailBox</c> is always implicit SSL; <c>tls</c>/<c>starttls</c> and <c>none</c> are honoured too.</summary>
    public static SecureSocketOptions SocketOptions(CpPriceMailSettings settings) => settings.Encryption.Trim().ToLowerInvariant() switch
    {
        "tls" or "starttls" => SecureSocketOptions.StartTls,
        "none" or "no" or "0" => SecureSocketOptions.None,
        _ => SecureSocketOptions.SslOnConnect,
    };

    private static async Task CopyLimitedAsync(Stream source, Stream target, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaxDownloadBytes)
            {
                throw new HttpRequestException("The file is larger than the " + (MaxDownloadBytes / 1024 / 1024).ToString(CultureInfo.InvariantCulture) + " MB limit");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Certificate-ignoring handler for the pyprices-compatible retry (named client <see cref="InsecureHttpClientName"/>).</summary>
    public static HttpMessageHandler CreateInsecureHandler() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true },
    };
}
