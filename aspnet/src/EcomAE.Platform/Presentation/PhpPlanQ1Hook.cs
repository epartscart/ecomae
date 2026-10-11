using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hook helpers. PHP identifiers kept for the inventory:
/// <c>epc_webhooks_ensure_schema</c>, <c>epc_webhooks_register</c>,
/// <c>epc_webhooks_update</c>, <c>epc_webhooks_delete</c>, <c>epc_webhooks_list</c>,
/// <c>epc_webhooks_dispatch</c>, <c>epc_webhooks_deliver</c>,
/// <c>epc_webhooks_process_retries</c>, <c>epc_webhooks_move_to_dlq</c>,
/// <c>epc_webhooks_dlq_list</c>, <c>epc_webhooks_dlq_retry</c>,
/// <c>epc_webhooks_dlq_resolve</c>, <c>epc_webhooks_delivery_stats</c>,
/// <c>epc_webhooks_encryption_key</c>, <c>epc_webhooks_encrypt_secret</c>,
/// <c>epc_webhooks_decrypt_secret</c>, <c>epc_webhooks_verify_signature</c>,
/// <c>epc_events_ensure_schema</c>, <c>epc_event_emit</c>,
/// <c>epc_events_list</c>, <c>epc_events_count</c>, <c>epc_events_type_summary</c>,
/// <c>epc_event_emit_invoice_posted</c>, <c>epc_event_emit_invoice_paid</c>,
/// <c>epc_event_emit_credit_note</c>, <c>epc_event_emit_order_placed</c>,
/// <c>epc_event_emit_order_shipped</c>, <c>epc_event_emit_stock_below</c>,
/// <c>epc_event_emit_stock_adjusted</c>, <c>epc_event_emit_payment_received</c>,
/// <c>epc_event_emit_voucher_posted</c>, <c>epc_event_emit_period_closed</c>,
/// <c>epc_event_emit_tenant</c>.
/// </summary>
public static class PhpPlanQ1Hook
{
    public const string WebhooksPath = "content/general_pages/epc_webhooks.php";
    public const string EventsPath = "content/general_pages/epc_events.php";

    public sealed class HookRow
    {
        public int Id { get; set; }
        public string TenantKey { get; set; } = "__platform__";
        public string Url { get; set; } = "";
        public string SecretHash { get; set; } = "";
        public string SecretEncrypted { get; set; } = "";
        public string EventsJson { get; set; } = "[]";
        public int Active { get; set; } = 1;
        public string Description { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string? UpdatedAt { get; set; }
    }

    public sealed class DeliveryRow
    {
        public long Id { get; set; }
        public int WebhookId { get; set; }
        public long EventId { get; set; }
        public string EventType { get; set; } = "";
        public string Status { get; set; } = "pending";
        public int HttpStatus { get; set; }
        public string? ResponseBody { get; set; }
        public string ErrorMessage { get; set; } = "";
        public int Attempt { get; set; }
        public int MaxAttempts { get; set; } = 5;
        public string? NextRetryAt { get; set; }
        public string? DeliveredAt { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class DlqRow
    {
        public long Id { get; set; }
        public long DeliveryId { get; set; }
        public int WebhookId { get; set; }
        public long EventId { get; set; }
        public string EventType { get; set; } = "";
        public string PayloadJson { get; set; } = "{}";
        public string LastError { get; set; } = "";
        public int LastHttpStatus { get; set; }
        public int Attempts { get; set; }
        public string CreatedAt { get; set; } = "";
        public string? ResolvedAt { get; set; }
        public int Resolved { get; set; }
    }

    public sealed class EventRow
    {
        public long Id { get; set; }
        public string EventType { get; set; } = "";
        public string TenantKey { get; set; } = "__platform__";
        public string PayloadJson { get; set; } = "{}";
        public int ActorId { get; set; }
        public string ActorType { get; set; } = "system";
        public string? IdempotencyKey { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class HookStore
    {
        public bool HookSchema { get; set; }
        public bool EventSchema { get; set; }
        public List<HookRow> Hooks { get; } = new();
        public List<DeliveryRow> Deliveries { get; } = new();
        public List<DlqRow> Dlq { get; } = new();
        public List<EventRow> Events { get; } = new();
        public int NextHookId { get; set; } = 1;
        public long NextDeliveryId { get; set; } = 1;
        public long NextDlqId { get; set; } = 1;
        public long NextEventId { get; set; } = 1;
        public Func<DateTimeOffset>? Clock { get; set; }
        public Func<string, string, string, long, Dictionary<string, object?>, Dictionary<string, object?>>? Deliver { get; set; }
        public DateTimeOffset Now() => Clock?.Invoke() ?? DateTimeOffset.UtcNow;
        public string NowSql() => Now().UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        public string NowIso() => Now().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture);
    }

    public static void EpcWebhooksEnsureSchema(HookStore store) => store.HookSchema = true;
    public static void EpcEventsEnsureSchema(HookStore store) => store.EventSchema = true;

    public static Dictionary<string, object?> EpcWebhooksRegister(HookStore store, Dictionary<string, object?> data)
    {
        EpcWebhooksEnsureSchema(store);
        var url = (Convert.ToString(data.TryGetValue("url", out var u) ? u : "", CultureInfo.InvariantCulture) ?? "").Trim();
        var secret = Convert.ToString(data.TryGetValue("secret", out var s) ? s : "", CultureInfo.InvariantCulture) ?? "";
        var events = data.TryGetValue("events", out var ev) ? ev : new[] { "*" };
        var tenantKey = Convert.ToString(data.TryGetValue("tenant_key", out var t) ? t : "__platform__", CultureInfo.InvariantCulture) ?? "__platform__";
        var description = Convert.ToString(data.TryGetValue("description", out var d) ? d : "", CultureInfo.InvariantCulture) ?? "";
        if (url == "" || !FilterValidateUrl(url))
        {
            return Fail("Invalid webhook URL");
        }

        if (!url.StartsWith("https://", StringComparison.Ordinal))
        {
            return Fail("Webhook URL must use HTTPS");
        }

        var row = new HookRow
        {
            Id = store.NextHookId++,
            TenantKey = tenantKey,
            Url = url,
            SecretHash = secret != "" ? Sha256Hex(secret) : "",
            SecretEncrypted = secret != "" ? EpcWebhooksEncryptSecret(secret) : "",
            EventsJson = JsonSerializer.Serialize(events is IEnumerable<object> or JsonElement ? events : new[] { events }),
            Description = description,
            CreatedAt = store.NowSql()
        };
        if (events is IEnumerable<string> strs)
        {
            row.EventsJson = JsonSerializer.Serialize(strs.ToArray());
        }
        else if (events is IEnumerable<object> objs)
        {
            row.EventsJson = JsonSerializer.Serialize(objs.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)).ToArray());
        }
        else if (events is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            row.EventsJson = el.GetRawText();
        }
        else
        {
            row.EventsJson = JsonSerializer.Serialize(new[] { Convert.ToString(events, CultureInfo.InvariantCulture) });
        }

        store.Hooks.Add(row);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["webhook_id"] = row.Id };
    }

    public static Dictionary<string, object?> EpcWebhooksUpdate(HookStore store, int webhookId, Dictionary<string, object?> data)
    {
        EpcWebhooksEnsureSchema(store);
        var row = store.Hooks.FirstOrDefault(h => h.Id == webhookId);
        var sets = 0;
        if (data.ContainsKey("url"))
        {
            var url = (Convert.ToString(data["url"], CultureInfo.InvariantCulture) ?? "").Trim();
            if (!FilterValidateUrl(url) || !url.StartsWith("https://", StringComparison.Ordinal))
            {
                return Fail("Invalid webhook URL");
            }

            if (row is not null)
            {
                row.Url = url;
            }

            sets++;
        }

        if (data.ContainsKey("events"))
        {
            if (row is not null)
            {
                row.EventsJson = JsonSerializer.Serialize(data["events"]);
            }

            sets++;
        }

        if (data.ContainsKey("active"))
        {
            if (row is not null)
            {
                row.Active = ToInt(data["active"]);
            }

            sets++;
        }

        if (data.ContainsKey("description"))
        {
            if (row is not null)
            {
                row.Description = Convert.ToString(data["description"], CultureInfo.InvariantCulture) ?? "";
            }

            sets++;
        }

        if (data.ContainsKey("secret"))
        {
            var secret = Convert.ToString(data["secret"], CultureInfo.InvariantCulture) ?? "";
            if (row is not null)
            {
                row.SecretHash = secret != "" ? Sha256Hex(secret) : "";
                row.SecretEncrypted = secret != "" ? EpcWebhooksEncryptSecret(secret) : "";
            }

            sets++;
        }

        if (sets == 0)
        {
            return Fail("No fields to update");
        }

        if (row is not null)
        {
            row.UpdatedAt = store.NowSql();
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    public static Dictionary<string, object?> EpcWebhooksDelete(HookStore store, int webhookId)
    {
        EpcWebhooksEnsureSchema(store);
        var row = store.Hooks.FirstOrDefault(h => h.Id == webhookId);
        if (row is not null)
        {
            row.Active = 0;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    public static List<Dictionary<string, object?>> EpcWebhooksList(HookStore store, string tenantKey = "")
    {
        EpcWebhooksEnsureSchema(store);
        var rows = store.Hooks.Where(h => h.Active == 1 && (tenantKey == "" || h.TenantKey == tenantKey)).OrderBy(h => h.Id).ToList();
        return rows.Select(h =>
        {
            object events;
            try
            {
                events = JsonSerializer.Deserialize<List<string>>(h.EventsJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                events = new List<string>();
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = h.Id,
                ["tenant_key"] = h.TenantKey,
                ["url"] = h.Url,
                ["events"] = events,
                ["active"] = h.Active,
                ["description"] = h.Description,
                ["created_at"] = h.CreatedAt,
                ["updated_at"] = h.UpdatedAt
            };
        }).ToList();
    }

    public static int EpcWebhooksDispatch(HookStore store, int eventId, string eventType, Dictionary<string, object?> payload, string tenantKey = "__platform__")
    {
        EpcWebhooksEnsureSchema(store);
        var hooks = store.Hooks.Where(h => h.Active == 1 && (h.TenantKey == tenantKey || h.TenantKey == "__platform__")).ToList();
        var dispatched = 0;
        foreach (var hook in hooks)
        {
            var events = DecodeEvents(hook.EventsJson);
            if (!events.Contains("*", StringComparer.Ordinal) && !events.Contains(eventType, StringComparer.Ordinal))
            {
                continue;
            }

            var delivery = new DeliveryRow
            {
                Id = store.NextDeliveryId++,
                WebhookId = hook.Id,
                EventId = eventId,
                EventType = eventType,
                Status = "pending",
                NextRetryAt = store.NowSql(),
                CreatedAt = store.NowSql()
            };
            store.Deliveries.Add(delivery);
            var secret = hook.SecretEncrypted != "" ? EpcWebhooksDecryptSecret(hook.SecretEncrypted) : "";
            var result = EpcWebhooksDeliver(hook.Url, secret, eventType, delivery.Id, payload, store);
            if (result["ok"] is true)
            {
                delivery.Status = "delivered";
                delivery.HttpStatus = ToInt(result["http_status"]);
                delivery.ResponseBody = Left(Convert.ToString(result.TryGetValue("response", out var r) ? r : "", CultureInfo.InvariantCulture), 2000);
                delivery.Attempt = 1;
                delivery.DeliveredAt = store.NowSql();
            }
            else
            {
                delivery.Status = "failed";
                delivery.HttpStatus = ToInt(result.TryGetValue("http_status", out var hs) ? hs : 0);
                delivery.ErrorMessage = Left(Convert.ToString(result.TryGetValue("error", out var err) ? err : "", CultureInfo.InvariantCulture), 512);
                delivery.Attempt = 1;
                delivery.NextRetryAt = store.Now().AddSeconds(60).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            dispatched++;
        }

        return dispatched;
    }

    public static Dictionary<string, object?> EpcWebhooksDeliver(
        string url,
        string secret,
        string eventType,
        long deliveryId,
        Dictionary<string, object?> payload,
        HookStore? store = null)
    {
        if (store?.Deliver is not null)
        {
            return store.Deliver(url, secret, eventType, deliveryId, payload);
        }

        var body = JsonSerializer.Serialize(payload);
        var timestamp = (store?.Now() ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        if (secret != "")
        {
            _ = EpcWebhooksVerifySignature(body, secret, "sha256=" + HmacSha256Hex(timestamp + "." + body, secret), timestamp.ToString(CultureInfo.InvariantCulture));
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["error"] = "skipped",
            ["http_status"] = 0
        };
    }

    public static Dictionary<string, int> EpcWebhooksProcessRetries(HookStore store, int batchSize = 50)
    {
        EpcWebhooksEnsureSchema(store);
        var now = store.NowSql();
        var deliveries = store.Deliveries
            .Where(d => d.Status == "failed" && d.Attempt < d.MaxAttempts && string.CompareOrdinal(d.NextRetryAt ?? "", now) <= 0)
            .OrderBy(d => d.NextRetryAt, StringComparer.Ordinal)
            .Take(batchSize)
            .ToList();
        var stats = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["processed"] = 0,
            ["delivered"] = 0,
            ["failed"] = 0,
            ["dlq"] = 0
        };
        foreach (var del in deliveries)
        {
            stats["processed"]++;
            var hook = store.Hooks.FirstOrDefault(h => h.Id == del.WebhookId);
            if (hook is null || hook.Active == 0)
            {
                EpcWebhooksMoveToDlq(store, DeliveryToDict(del, null));
                stats["dlq"]++;
                continue;
            }

            var ev = store.Events.FirstOrDefault(e => e.Id == del.EventId);
            var payloadJson = ev?.PayloadJson ?? "{}";
            Dictionary<string, object?> payload;
            try
            {
                payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(payloadJson) ?? new();
            }
            catch (JsonException)
            {
                payload = new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            var secret = hook.SecretEncrypted != "" ? EpcWebhooksDecryptSecret(hook.SecretEncrypted) : "";
            var attempt = del.Attempt + 1;
            var result = EpcWebhooksDeliver(hook.Url, secret, del.EventType, del.Id, payload, store);
            if (result["ok"] is true)
            {
                del.Status = "delivered";
                del.HttpStatus = ToInt(result["http_status"]);
                del.ResponseBody = Left(Convert.ToString(result.TryGetValue("response", out var r) ? r : "", CultureInfo.InvariantCulture), 2000);
                del.Attempt = attempt;
                del.DeliveredAt = store.NowSql();
                stats["delivered"]++;
            }
            else if (attempt >= del.MaxAttempts)
            {
                var bag = DeliveryToDict(del, payloadJson);
                bag["last_error"] = result.TryGetValue("error", out var err) ? err : "";
                bag["last_http_status"] = result.TryGetValue("http_status", out var hs) ? hs : 0;
                bag["attempts"] = attempt;
                EpcWebhooksMoveToDlq(store, bag);
                del.Status = "dlq";
                del.Attempt = attempt;
                stats["dlq"]++;
            }
            else
            {
                var delays = new[] { 60, 300, 1800, 7200, 43200 };
                var delay = delays[Math.Min(attempt - 1, delays.Length - 1)];
                del.HttpStatus = ToInt(result.TryGetValue("http_status", out var hs) ? hs : 0);
                del.ErrorMessage = Left(Convert.ToString(result.TryGetValue("error", out var err) ? err : "", CultureInfo.InvariantCulture), 512);
                del.Attempt = attempt;
                del.NextRetryAt = store.Now().AddSeconds(delay).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                stats["failed"]++;
            }
        }

        return stats;
    }

    public static void EpcWebhooksMoveToDlq(HookStore store, Dictionary<string, object?> delivery)
    {
        store.Dlq.Add(new DlqRow
        {
            Id = store.NextDlqId++,
            DeliveryId = ToLong(delivery.TryGetValue("id", out var id) ? id : 0),
            WebhookId = ToInt(delivery.TryGetValue("webhook_id", out var wh) ? wh : 0),
            EventId = ToLong(delivery.TryGetValue("event_id", out var ev) ? ev : 0),
            EventType = Convert.ToString(delivery.TryGetValue("event_type", out var et) ? et : "", CultureInfo.InvariantCulture) ?? "",
            PayloadJson = Convert.ToString(delivery.TryGetValue("payload_json", out var pj) ? pj : "{}", CultureInfo.InvariantCulture) ?? "{}",
            LastError = Convert.ToString(
                delivery.TryGetValue("last_error", out var le) ? le : delivery.TryGetValue("error_message", out var em) ? em : "",
                CultureInfo.InvariantCulture) ?? "",
            LastHttpStatus = ToInt(delivery.TryGetValue("last_http_status", out var lhs) ? lhs : delivery.TryGetValue("http_status", out var hs) ? hs : 0),
            Attempts = ToInt(delivery.TryGetValue("attempts", out var at) ? at : delivery.TryGetValue("attempt", out var att) ? att : 0),
            CreatedAt = store.NowSql()
        });
    }

    public static List<Dictionary<string, object?>> EpcWebhooksDlqList(HookStore store, bool unresolvedOnly = true, int limit = 50)
    {
        EpcWebhooksEnsureSchema(store);
        return store.Dlq
            .Where(x => !unresolvedOnly || x.Resolved == 0)
            .OrderByDescending(x => x.CreatedAt, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = x.Id,
                ["delivery_id"] = x.DeliveryId,
                ["webhook_id"] = x.WebhookId,
                ["event_id"] = x.EventId,
                ["event_type"] = x.EventType,
                ["payload_json"] = x.PayloadJson,
                ["last_error"] = x.LastError,
                ["last_http_status"] = x.LastHttpStatus,
                ["attempts"] = x.Attempts,
                ["created_at"] = x.CreatedAt,
                ["resolved_at"] = x.ResolvedAt,
                ["resolved"] = x.Resolved
            })
            .ToList();
    }

    public static Dictionary<string, object?> EpcWebhooksDlqRetry(HookStore store, int dlqId)
    {
        EpcWebhooksEnsureSchema(store);
        var item = store.Dlq.FirstOrDefault(x => x.Id == dlqId && x.Resolved == 0);
        var hook = item is null ? null : store.Hooks.FirstOrDefault(h => h.Id == item.WebhookId);
        if (item is null || hook is null)
        {
            return Fail("DLQ item not found or already resolved");
        }

        Dictionary<string, object?> payload;
        try
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.PayloadJson) ?? new();
        }
        catch (JsonException)
        {
            payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var secret = hook.SecretEncrypted != "" ? EpcWebhooksDecryptSecret(hook.SecretEncrypted) : "";
        var result = EpcWebhooksDeliver(hook.Url, secret, item.EventType, item.DeliveryId, payload, store);
        if (result["ok"] is true)
        {
            item.Resolved = 1;
            item.ResolvedAt = store.NowSql();
            var del = store.Deliveries.FirstOrDefault(d => d.Id == item.DeliveryId);
            if (del is not null)
            {
                del.Status = "delivered";
                del.DeliveredAt = store.NowSql();
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "DLQ item delivered successfully" };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["error"] = result.TryGetValue("error", out var err) ? err : "Delivery failed",
            ["http_status"] = result.TryGetValue("http_status", out var hs) ? hs : 0
        };
    }

    public static Dictionary<string, object?> EpcWebhooksDlqResolve(HookStore store, int dlqId)
    {
        EpcWebhooksEnsureSchema(store);
        var item = store.Dlq.FirstOrDefault(x => x.Id == dlqId);
        if (item is not null)
        {
            item.Resolved = 1;
            item.ResolvedAt = store.NowSql();
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    public static Dictionary<string, object?> EpcWebhooksDeliveryStats(HookStore store, int hours = 24)
    {
        EpcWebhooksEnsureSchema(store);
        var since = store.Now().AddHours(-hours).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var rows = store.Deliveries.Where(d => string.CompareOrdinal(d.CreatedAt, since) >= 0).ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["pending"] = rows.Count(d => d.Status == "pending"),
            ["delivered"] = rows.Count(d => d.Status == "delivered"),
            ["failed"] = rows.Count(d => d.Status == "failed"),
            ["dlq"] = store.Dlq.Count(x => x.Resolved == 0),
            ["period_hours"] = hours
        };
    }

    public static byte[] EpcWebhooksEncryptionKey()
        => SHA256.HashData(Encoding.UTF8.GetBytes("epc-webhooks-secret-key:epartscart-deploy-2026"));

    public static string EpcWebhooksEncryptSecret(string plaintext)
    {
        var key = EpcWebhooksEncryptionKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(plaintext), 0, Encoding.UTF8.GetByteCount(plaintext));
        var packed = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, packed, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, packed, aes.IV.Length, cipher.Length);
        return Convert.ToBase64String(packed);
    }

    public static string EpcWebhooksDecryptSecret(string ciphertext)
    {
        if (ciphertext == "")
        {
            return "";
        }

        byte[] data;
        try
        {
            data = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException)
        {
            return "";
        }

        if (data.Length < 17)
        {
            return "";
        }

        var key = EpcWebhooksEncryptionKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = data[..16];
        try
        {
            using var dec = aes.CreateDecryptor();
            var plain = dec.TransformFinalBlock(data, 16, data.Length - 16);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            return "";
        }
    }

    public static bool EpcWebhooksVerifySignature(string rawBody, string secret, string signatureHeader, string timestampHeader = "")
    {
        if (secret == "" || signatureHeader == "")
        {
            return false;
        }

        var timestamp = timestampHeader != "" ? timestampHeader : DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var expected = "sha256=" + HmacSha256Hex(timestamp + "." + rawBody, secret);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
    }

    public static long EpcEventEmit(
        HookStore store,
        string eventType,
        Dictionary<string, object?>? payload = null,
        string tenantKey = "__platform__",
        int actorId = 0,
        string actorType = "system",
        string idempotencyKey = "")
    {
        EpcEventsEnsureSchema(store);
        payload ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        payload["_event_type"] = eventType;
        payload["_tenant_key"] = tenantKey;
        payload["_timestamp"] = store.NowIso();
        var idemKey = idempotencyKey != "" ? idempotencyKey : null;
        if (idemKey is not null && store.Events.Any(e => e.IdempotencyKey == idemKey))
        {
            return 0;
        }

        var row = new EventRow
        {
            Id = store.NextEventId++,
            EventType = eventType,
            TenantKey = tenantKey,
            PayloadJson = JsonSerializer.Serialize(payload),
            ActorId = actorId,
            ActorType = actorType,
            IdempotencyKey = idemKey,
            CreatedAt = store.NowSql()
        };
        store.Events.Add(row);
        EpcWebhooksDispatch(store, (int)row.Id, eventType, payload, tenantKey);
        return row.Id;
    }

    public static List<Dictionary<string, object?>> EpcEventsList(HookStore store, Dictionary<string, object?>? filters = null, int limit = 50, int offset = 0)
    {
        EpcEventsEnsureSchema(store);
        return FilterEvents(store, filters)
            .OrderByDescending(e => e.Id)
            .Skip(offset)
            .Take(limit)
            .Select(e => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = e.Id,
                ["event_type"] = e.EventType,
                ["tenant_key"] = e.TenantKey,
                ["payload_json"] = e.PayloadJson,
                ["actor_id"] = e.ActorId,
                ["actor_type"] = e.ActorType,
                ["idempotency_key"] = e.IdempotencyKey,
                ["created_at"] = e.CreatedAt
            })
            .ToList();
    }

    public static int EpcEventsCount(HookStore store, Dictionary<string, object?>? filters = null)
    {
        EpcEventsEnsureSchema(store);
        return FilterEvents(store, filters, tenantAndTypeOnly: true).Count();
    }

    public static List<Dictionary<string, object?>> EpcEventsTypeSummary(HookStore store, string since = "")
    {
        EpcEventsEnsureSchema(store);
        if (since == "")
        {
            since = store.Now().AddDays(-7).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return store.Events
            .Where(e => string.CompareOrdinal(e.CreatedAt, since) >= 0)
            .GroupBy(e => e.EventType, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["event_type"] = g.Key,
                ["count"] = g.Count(),
                ["last_at"] = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => Convert.ToInt32(x["count"], CultureInfo.InvariantCulture))
            .ToList();
    }

    public static long EpcEventEmitInvoicePosted(HookStore store, int invoiceId, double total, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "invoice.posted", new Dictionary<string, object?> { ["invoice_id"] = invoiceId, ["total"] = total, ["currency"] = "AED" }, tenantKey, userId, "user", "inv-post-" + invoiceId);

    public static long EpcEventEmitInvoicePaid(HookStore store, int invoiceId, double amount, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "invoice.paid", new Dictionary<string, object?> { ["invoice_id"] = invoiceId, ["amount"] = amount }, tenantKey, userId, "user", "inv-paid-" + invoiceId + "-" + store.Now().ToUnixTimeSeconds());

    public static long EpcEventEmitCreditNote(HookStore store, int invoiceId, double amount, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "invoice.credit_note", new Dictionary<string, object?> { ["invoice_id"] = invoiceId, ["amount"] = amount }, tenantKey, userId, "user", "inv-cn-" + invoiceId);

    public static long EpcEventEmitOrderPlaced(HookStore store, int orderId, double total, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "order.placed", new Dictionary<string, object?> { ["order_id"] = orderId, ["total"] = total }, tenantKey, userId, userId > 0 ? "user" : "system", "order-placed-" + orderId);

    public static long EpcEventEmitOrderShipped(HookStore store, int orderId, string trackingNo, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "order.shipped", new Dictionary<string, object?> { ["order_id"] = orderId, ["tracking"] = trackingNo }, tenantKey, userId, "system", "order-shipped-" + orderId);

    public static long EpcEventEmitStockBelow(HookStore store, string sku, int qty, int reorderLevel, string tenantKey)
        => EpcEventEmit(store, "stock.below", new Dictionary<string, object?> { ["sku"] = sku, ["qty"] = qty, ["reorder_level"] = reorderLevel }, tenantKey, 0, "system", "stock-below-" + sku + "-" + store.Now().ToString("yyyyMMdd", CultureInfo.InvariantCulture));

    public static long EpcEventEmitStockAdjusted(HookStore store, string sku, int oldQty, int newQty, string reason, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "stock.adjusted", new Dictionary<string, object?> { ["sku"] = sku, ["old_qty"] = oldQty, ["new_qty"] = newQty, ["reason"] = reason }, tenantKey, userId, "user");

    public static long EpcEventEmitPaymentReceived(HookStore store, int paymentId, double amount, string method, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "payment.received", new Dictionary<string, object?> { ["payment_id"] = paymentId, ["amount"] = amount, ["method"] = method }, tenantKey, userId, "system", "pmt-" + paymentId);

    public static long EpcEventEmitVoucherPosted(HookStore store, int voucherId, string type, double amount, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "erp.voucher_posted", new Dictionary<string, object?> { ["voucher_id"] = voucherId, ["type"] = type, ["amount"] = amount }, tenantKey, userId, "user", "voucher-" + voucherId);

    public static long EpcEventEmitPeriodClosed(HookStore store, string yearMonth, string tenantKey, int userId = 0)
        => EpcEventEmit(store, "erp.period_closed", new Dictionary<string, object?> { ["year_month"] = yearMonth }, tenantKey, userId, "user", "period-close-" + yearMonth);

    public static long EpcEventEmitTenant(HookStore store, string action, string tenantKey, Dictionary<string, object?>? details = null)
        => EpcEventEmit(store, "tenant." + action, details ?? new Dictionary<string, object?>(StringComparer.Ordinal), tenantKey, 0, "system");

    private static IEnumerable<EventRow> FilterEvents(HookStore store, Dictionary<string, object?>? filters, bool tenantAndTypeOnly = false)
    {
        IEnumerable<EventRow> rows = store.Events;
        if (filters is null)
        {
            return rows;
        }

        if (!IsPhpEmpty(filters.TryGetValue("event_type", out var et) ? et : null))
        {
            rows = rows.Where(e => e.EventType == Convert.ToString(et, CultureInfo.InvariantCulture));
        }

        if (!IsPhpEmpty(filters.TryGetValue("tenant_key", out var tk) ? tk : null))
        {
            rows = rows.Where(e => e.TenantKey == Convert.ToString(tk, CultureInfo.InvariantCulture));
        }

        if (!tenantAndTypeOnly && !IsPhpEmpty(filters.TryGetValue("since", out var since) ? since : null))
        {
            var s = Convert.ToString(since, CultureInfo.InvariantCulture) ?? "";
            rows = rows.Where(e => string.CompareOrdinal(e.CreatedAt, s) >= 0);
        }

        if (!tenantAndTypeOnly && !IsPhpEmpty(filters.TryGetValue("until", out var until) ? until : null))
        {
            var u = Convert.ToString(until, CultureInfo.InvariantCulture) ?? "";
            rows = rows.Where(e => string.CompareOrdinal(e.CreatedAt, u) <= 0);
        }

        return rows;
    }

    private static List<string> DecodeEvents(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static Dictionary<string, object?> DeliveryToDict(DeliveryRow del, string? payloadJson)
        => new(StringComparer.Ordinal)
        {
            ["id"] = del.Id,
            ["webhook_id"] = del.WebhookId,
            ["event_id"] = del.EventId,
            ["event_type"] = del.EventType,
            ["payload_json"] = payloadJson ?? "{}",
            ["error_message"] = del.ErrorMessage,
            ["http_status"] = del.HttpStatus,
            ["attempt"] = del.Attempt
        };

    private static bool FilterValidateUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);

    private static Dictionary<string, object?> Fail(string error)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = error };

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string HmacSha256Hex(string value, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string Left(string? value, int n)
    {
        value ??= "";
        return value.Length <= n ? value : value[..n];
    }

    private static int ToInt(object? value)
        => value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    private static long ToLong(object? value)
        => value is null ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);

    private static bool IsPhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s.Length == 0 || s == "0"));
}
