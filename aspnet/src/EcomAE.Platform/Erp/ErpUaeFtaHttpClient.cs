using System.Net.Http.Headers;

namespace EcomAE.Platform.Erp;

/// <summary>HTTP twin of PHP <c>epc_uae_fta_http_get</c> / <c>epc_uae_fta_http_post</c>.</summary>
public interface IErpUaeFtaHttpClient
{
    Task<string> GetAsync(string url, CancellationToken cancellationToken = default);

    Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken = default);
}

public sealed class ErpUaeFtaHttpClient : IErpUaeFtaHttpClient
{
    private readonly HttpClient _http;

    public ErpUaeFtaHttpClient(HttpClient http)
    {
        _http = http;
        if (_http.Timeout == Timeout.InfiniteTimeSpan || _http.Timeout > TimeSpan.FromSeconds(45))
        {
            _http.Timeout = TimeSpan.FromSeconds(45);
        }

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(ErpUaeTaxFtaLegislation.UserAgent);
        }
    }

    public async Task<string> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return "";
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return "";
        }
    }

    public async Task<string> PostFormAsync(string url, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new FormUrlEncodedContent(fields.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value ?? "")));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            using var response = await _http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return "";
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return "";
        }
    }
}
