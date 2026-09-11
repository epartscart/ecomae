using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>subscription_billing</c> <c>cancel</c> / <c>epc_billing_cancel</c>,
/// <c>pay</c> / <c>epc_billing_record_payment</c>, and <c>create_plan</c> / <c>epc_billing_create_plan</c>.
/// Subscribe and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. Create-plan always returns ok — this write does not invent plan-code checks.
/// </summary>
public interface IBosBillingWriteService
{
    Task<ErpSimpleWriteResult> CancelAsync(
        long subscriptionId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PayAsync(
        long invoiceId,
        string? method,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreatePlanAsync(
        string? planData,
        CancellationToken cancellationToken = default);
}

public sealed class BosBillingWriteService : IBosBillingWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosBillingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CancelAsync(
        long subscriptionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var cancelledReason = reason ?? string.Empty;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_subscriptions` SET `status`='cancelled', `cancel_at`=CURDATE(), `cancelled_reason`=?
                    WHERE `id`=?
                    """),
                cancellationToken, cancelledReason, subscriptionId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Subscription cancelled", subscriptionId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Subscriptions table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> PayAsync(
        long invoiceId,
        string? method,
        CancellationToken cancellationToken = default)
    {
        var paymentMethod = method ?? "card";
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_billing_invoices` SET `status`='paid', `paid_at`=NOW(), `payment_method`=?
                    WHERE `id`=? AND `status` IN ('sent','overdue')
                    """),
                cancellationToken, paymentMethod, invoiceId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Invoice payment recorded", invoiceId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Billing invoices table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP <c>(int)</c> / <c>intval</c> on a token (leading optional sign + digits).</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>PHP <c>(float)</c> on a token (leading numeric / optional exponent; trailing junk ignored).</summary>
    public static decimal PhpFloat(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var text = raw.TrimStart();
        if (text.Length == 0)
        {
            return 0;
        }

        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        var sawDigit = false;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            sawDigit = true;
            i++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                sawDigit = true;
                i++;
            }
        }

        if (!sawDigit)
        {
            return 0;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var exp = i + 1;
            if (exp < text.Length && text[exp] is '+' or '-')
            {
                exp++;
            }

            var expDigits = exp;
            while (expDigits < text.Length && char.IsDigit(text[expDigits]))
            {
                expDigits++;
            }

            if (expDigits > exp)
            {
                i = expDigits;
            }
        }

        return decimal.TryParse(text[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// PHP <c>json_decode((string)($_POST['plan_data'] ?? '{}'), true) ?: array()</c>
    /// then <c>plan_code</c> via <c>strtoupper</c>, <c>billing_cycle ?? monthly</c>,
    /// <c>base_price ?? 0</c>, <c>currency ?? AED</c>, <c>trial_days ?? 0</c>, <c>setup_fee ?? 0</c>,
    /// and <c>json_encode</c> of <c>features</c> / <c>usage_limits</c> (default empty array).
    /// </summary>
    public static (
        string PlanCode,
        string Name,
        string Description,
        string BillingCycle,
        decimal BasePrice,
        string Currency,
        long TrialDays,
        decimal SetupFee,
        string FeaturesJson,
        string UsageLimitsJson) ParsePlanData(string? raw)
    {
        var planCode = "";
        var name = "";
        var description = "";
        var billingCycle = "monthly";
        var basePrice = 0m;
        var currency = "AED";
        var trialDays = 0L;
        var setupFee = 0m;
        var featuresJson = "[]";
        var usageLimitsJson = "[]";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (planCode, name, description, billingCycle, basePrice, currency, trialDays, setupFee, featuresJson, usageLimitsJson);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (planCode, name, description, billingCycle, basePrice, currency, trialDays, setupFee, featuresJson, usageLimitsJson);
            }

            if (TryGetPresent(doc.RootElement, "plan_code", out var codeEl))
            {
                planCode = JsonString(codeEl).ToUpperInvariant();
            }

            if (TryGetPresent(doc.RootElement, "name", out var nameEl))
            {
                name = JsonString(nameEl);
            }

            if (TryGetPresent(doc.RootElement, "description", out var descEl))
            {
                description = JsonString(descEl);
            }

            if (TryGetPresent(doc.RootElement, "billing_cycle", out var cycleEl))
            {
                billingCycle = JsonString(cycleEl);
            }

            if (TryGetPresent(doc.RootElement, "base_price", out var priceEl))
            {
                basePrice = PhpFloat(priceEl);
            }

            if (TryGetPresent(doc.RootElement, "currency", out var currencyEl))
            {
                currency = JsonString(currencyEl);
            }

            if (TryGetPresent(doc.RootElement, "trial_days", out var trialEl))
            {
                trialDays = PhpIntval(trialEl);
            }

            if (TryGetPresent(doc.RootElement, "setup_fee", out var feeEl))
            {
                setupFee = PhpFloat(feeEl);
            }

            featuresJson = EncodeJsonField(doc.RootElement, "features");
            usageLimitsJson = EncodeJsonField(doc.RootElement, "usage_limits");
            return (planCode, name, description, billingCycle, basePrice, currency, trialDays, setupFee, featuresJson, usageLimitsJson);
        }
        catch (JsonException)
        {
            return ("", "", "", "monthly", 0m, "AED", 0L, 0m, "[]", "[]");
        }
    }

    private static bool TryGetPresent(JsonElement root, string name, out JsonElement element)
    {
        if (root.TryGetProperty(name, out element)
            && element.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return true;
        }

        element = default;
        return false;
    }

    private static long PhpIntval(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var n) => n,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => (long)d,
            JsonValueKind.String => PhpIntval(element.GetString()),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0
        };

    private static decimal PhpFloat(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.Number when element.TryGetDouble(out var n) => (decimal)n,
            JsonValueKind.String => PhpFloat(element.GetString()),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0
        };

    private static string JsonString(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "",
            _ => ""
        };

    private static string EncodeJsonField(JsonElement root, string name)
    {
        if (!TryGetPresent(root, name, out var el))
        {
            return "[]";
        }

        if (el.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            return JsonSerializer.Serialize(el);
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            var text = el.GetString() ?? "";
            if (text.Length == 0)
            {
                return JsonSerializer.Serialize(text);
            }

            try
            {
                using var nested = JsonDocument.Parse(text);
                if (nested.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    return JsonSerializer.Serialize(nested.RootElement);
                }
            }
            catch (JsonException)
            {
            }

            return JsonSerializer.Serialize(text);
        }

        return JsonSerializer.Serialize(el);
    }

    public async Task<ErpSimpleWriteResult> CreatePlanAsync(
        string? planData,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var parsed = ParsePlanData(planData);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_billing_plans` (`plan_code`,`name`,`description`,`billing_cycle`,`base_price`,`currency`,`trial_days`,`setup_fee`,`features`,`usage_limits`) VALUES (?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                parsed.PlanCode,
                parsed.Name,
                parsed.Description,
                parsed.BillingCycle,
                parsed.BasePrice,
                parsed.Currency,
                parsed.TrialDays,
                parsed.SetupFee,
                parsed.FeaturesJson,
                parsed.UsageLimitsJson).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Billing plan created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Billing plans table is missing — schema-ensure stays Classic.");
        }
    }
}
