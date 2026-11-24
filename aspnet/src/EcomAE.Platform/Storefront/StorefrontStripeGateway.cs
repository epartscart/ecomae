using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live Stripe Checkout for the <c>stripe</c> payment gateway: create a hosted Checkout Session when
/// <c>go_to_pay.php</c> fires, and retrieve it on the success URL to confirm the payment before the
/// order/balance write runs. Keys come from the tenant's <c>shop_payment_systems.parameters_values</c>
/// (CP → Payment gateways), so each storefront uses its own Stripe account.
/// </summary>
public static class StorefrontStripeGateway
{
    /// <summary>Currencies Stripe treats as zero-decimal (no ×100 minor-unit conversion).</summary>
    private static readonly HashSet<string> ZeroDecimal = new(StringComparer.OrdinalIgnoreCase)
    {
        "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
    };

    public static long MinorUnits(decimal amount, string currency)
        => ZeroDecimal.Contains(currency) ? (long)Math.Round(amount) : (long)Math.Round(amount * 100m);

    public static decimal MajorUnits(long minor, string currency)
        => ZeroDecimal.Contains(currency) ? minor : minor / 100m;

    /// <summary>Creates a Stripe Checkout Session; returns the hosted page URL to redirect the shopper to.</summary>
    public static async Task<string?> CreateCheckoutSessionAsync(
        HttpClient client,
        string secretKey,
        string currency,
        decimal amount,
        string description,
        string successUrl,
        string cancelUrl,
        string operationId,
        CancellationToken cancellationToken)
    {
        // Idempotent per operation + amount: reopening a pending pay link replays the same request and
        // Stripe returns the original session; a changed amount mints a new one instead of erroring.
        var idempotencyKey = "epc-checkout-" + operationId + "-" + MinorUnits(amount, currency).ToString(CultureInfo.InvariantCulture) + "-" + currency.ToLowerInvariant();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = "payment",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["client_reference_id"] = operationId,
            ["payment_intent_data[metadata][operation_id]"] = operationId,
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = currency.ToLowerInvariant(),
            ["line_items[0][price_data][unit_amount]"] = MinorUnits(amount, currency).ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = description
        };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions")
            {
                Content = new FormUrlEncodedContent(fields)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("url", out var url) ? url.GetString() : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public sealed record StripeSession(string PaymentStatus, long AmountTotal, string Currency, string ClientReferenceId);

    /// <summary>Verifies a Stripe-Signature header (t=…,v1=…) over the raw webhook body, ±5 min tolerance.</summary>
    public static bool VerifyWebhookSignature(string rawBody, string? signatureHeader, string webhookSecret)
    {
        if (webhookSecret.Length == 0 || string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            if (kv[0] == "t")
            {
                timestamp = kv[1];
            }
            else if (kv[0] == "v1")
            {
                signatures.Add(kv[1]);
            }
        }

        if (timestamp is null || signatures.Count == 0
            || !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ts)
            || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300)
        {
            return false;
        }

        var expected = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), Encoding.UTF8.GetBytes(timestamp + "." + rawBody)));
        return signatures.Any(sig => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sig)));
    }

    /// <summary>Retrieves a Checkout Session so the success URL can be trusted (server-side, not shopper-supplied).</summary>
    public static async Task<StripeSession?> RetrieveSessionAsync(
        HttpClient client,
        string secretKey,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.stripe.com/v1/checkout/sessions/" + Uri.EscapeDataString(sessionId));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new StripeSession(
                root.TryGetProperty("payment_status", out var ps) && ps.ValueKind == JsonValueKind.String ? ps.GetString() ?? string.Empty : string.Empty,
                root.TryGetProperty("amount_total", out var at) && at.ValueKind == JsonValueKind.Number ? at.GetInt64() : 0,
                root.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String ? cur.GetString() ?? string.Empty : string.Empty,
                root.TryGetProperty("client_reference_id", out var cr) && cr.ValueKind == JsonValueKind.String ? cr.GetString() ?? string.Empty : string.Empty);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
