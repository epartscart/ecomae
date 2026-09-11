using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>subscription_billing</c> <c>cancel</c> / <c>epc_billing_cancel</c>,
/// <c>pay</c> / <c>epc_billing_record_payment</c>, <c>create_plan</c> / <c>epc_billing_create_plan</c>,
/// and <c>subscribe</c> / <c>epc_billing_subscribe</c>.
/// Schema-ensure stays Classic. This service does not invent a send.
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

    Task<ErpSimpleWriteResult> SubscribeAsync(
        string? siteKey,
        long planId,
        CancellationToken cancellationToken = default);
}

public sealed class BosBillingWriteService : IBosBillingWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

    public async Task<ErpSimpleWriteResult> SubscribeAsync(
        string? siteKey,
        long planId,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `name`,`billing_cycle`,`base_price`,`setup_fee`,`trial_days` FROM `epc_billing_plans` WHERE `id`=? AND `active`=1");
            ErpDb.AddParameters(command, planId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Plan not found");
            }

            var planName = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var cycle = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var basePrice = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
            var setupFee = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
            var trialDays = reader.IsDBNull(4) ? 0L : Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture);
            await reader.DisposeAsync().ConfigureAwait(false);

            var start = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var days = CycleDays(cycle);
            var end = DateTime.Now.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            object? trialEnd = trialDays > 0
                ? DateTime.Now.AddDays(trialDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;
            var status = trialDays > 0 ? "trial" : "active";
            var mrr = MonthlyRecurring(basePrice, cycle);

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_subscriptions` (`site_key`,`plan_id`,`status`,`current_period_start`,`current_period_end`,`trial_end`,`mrr`) VALUES (?,?,?,?,?,?,?)"),
                cancellationToken, key, planId, status, start, end, trialEnd, mrr).ConfigureAwait(false);
            var subId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            var invNum = InvoiceNumber(key, subId, DateTime.Now);
            var tax = Math.Round(basePrice * 0.05m, 2, MidpointRounding.AwayFromZero);
            var subtotal = basePrice + setupFee;
            var total = subtotal + tax;
            var due = DateTime.Now.AddDays(15).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var lines = JsonSerializer.Serialize(new object[]
            {
                new Dictionary<string, object?> { ["description"] = planName + " subscription", ["amount"] = basePrice },
                new Dictionary<string, object?> { ["description"] = "Setup fee", ["amount"] = setupFee },
            });
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_billing_invoices`
                        (`subscription_id`,`site_key`,`invoice_number`,`period_start`,`period_end`,`subtotal`,`tax`,`total`,`status`,`due_date`,`line_items`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken, subId, key, invNum, start, end, subtotal, tax, total, "sent", due, lines).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Subscription created", subId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Billing tables are missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>$cycleDays[$plan['billing_cycle']] ?? 30</c>.</summary>
    public static int CycleDays(string? cycle)
        => cycle switch
        {
            "monthly" => 30,
            "quarterly" => 90,
            "semi_annual" => 180,
            "annual" => 365,
            _ => 30,
        };

    /// <summary>PHP MRR from <c>base_price</c> and <c>billing_cycle</c>.</summary>
    public static decimal MonthlyRecurring(decimal basePrice, string? cycle)
        => cycle switch
        {
            "quarterly" => Math.Round(basePrice / 3m, 2, MidpointRounding.AwayFromZero),
            "semi_annual" => Math.Round(basePrice / 6m, 2, MidpointRounding.AwayFromZero),
            "annual" => Math.Round(basePrice / 12m, 2, MidpointRounding.AwayFromZero),
            _ => basePrice,
        };

    /// <summary>PHP <c>INV-</c> + upper site key + <c>Ymd</c> + padded subscription id.</summary>
    public static string InvoiceNumber(string siteKey, long subscriptionId, DateTime now)
        => "INV-" + siteKey.ToUpperInvariant() + "-" + now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-"
           + subscriptionId.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
}
