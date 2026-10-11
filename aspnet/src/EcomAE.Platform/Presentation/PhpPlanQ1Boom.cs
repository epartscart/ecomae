using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-boom blockchain BOS proofs. PHP identifiers kept for the inventory:
/// <c>epc_bc_bos_modes</c>, <c>epc_bc_bos_normalize_mode</c>,
/// <c>epc_bc_bos_anchor_network</c>, <c>epc_bc_bos_tenant_mode_cache_store</c>,
/// <c>epc_bc_bos_clear_tenant_mode_cache</c>, <c>epc_bc_bos_platform_pdo</c>,
/// <c>epc_bc_bos_ensure_schema</c>, <c>epc_bc_bos_canonical_json</c>,
/// <c>epc_bc_bos_is_assoc</c>, <c>epc_bc_bos_hash</c>, <c>epc_bc_bos_new_uid</c>,
/// <c>epc_bc_bos_record_proof</c>, <c>epc_bc_bos_merkle_root</c>,
/// <c>epc_bc_bos_merkle_proof_path</c>, <c>epc_bc_bos_verify_merkle_path</c>,
/// <c>epc_bc_bos_anchor_pending_batch</c>, <c>epc_bc_bos_verify</c>,
/// <c>epc_bc_bos_job_anchor_batch</c>, <c>epc_bc_bos_register_job_handlers</c>,
/// <c>epc_bc_bos_product_name</c>, <c>epc_bc_bos_product_tagline</c>,
/// <c>epc_bc_bos_resolve_site_key</c>, <c>epc_bc_bos_tenant_mode</c>,
/// <c>epc_bc_bos_maybe_record_document</c>, <c>epc_bc_bos_verify_url</c>,
/// <c>epc_bc_bos_public_base_url</c>, <c>epc_bc_bos_verify_url_absolute</c>,
/// <c>epc_bc_bos_lookup_proof</c>, <c>epc_bc_bos_list_proofs</c>,
/// <c>epc_bc_bos_list_proofs_fleet</c>, <c>epc_bc_bos_fleet_stats</c>,
/// <c>epc_bc_bos_proof_badge_html</c>, <c>epc_bc_bos_einvoice_record_keys</c>,
/// <c>epc_bc_bos_grn_record_id</c>, <c>epc_bc_bos_grn_badge_html</c>,
/// <c>epc_bc_bos_grn_flash_for_purchase</c>, <c>epc_bc_bos_tenant_proof_stats</c>,
/// <c>epc_bc_bos_document_badge_html</c>.
/// GET never mints a session cookie. Leftover ERP/shared/intro parents stay injected.
/// </summary>
public static class PhpPlanQ1Boom
{
    public const string BlockchainBosPath = "content/general_pages/epc_blockchain_bos.php";

    public sealed class ProofRow
    {
        public long Id { get; set; }
        public string ProofUid { get; set; } = "";
        public string TenantKey { get; set; } = "";
        public string RecordType { get; set; } = "";
        public string RecordId { get; set; } = "";
        public string PayloadHash { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public string Status { get; set; } = "pending";
        public long? BatchId { get; set; }
        public int? MerkleIndex { get; set; }
        public string? MerkleProofJson { get; set; }
        public string? AnchoredAt { get; set; }
        public string AnchorRef { get; set; } = "";
        public string CreatedAt { get; set; } = "NOW";
        public string UpdatedAt { get; set; } = "NOW";
    }

    public sealed class BatchRow
    {
        public long Id { get; set; }
        public string BatchUid { get; set; } = "";
        public string MerkleRoot { get; set; } = "";
        public int ProofCount { get; set; }
        public string Status { get; set; } = "anchored";
        public string AnchorNetwork { get; set; } = "local_merkle";
        public string AnchorRef { get; set; } = "";
        public string AnchoredAt { get; set; } = "NOW";
        public string? MetaJson { get; set; }
    }

    public static List<ProofRow> Proofs { get; } = [];
    public static List<BatchRow> Batches { get; } = [];
    public static List<Dictionary<string, object?>> Jobs { get; } = [];
    public static List<string> Handlers { get; } = [];
    public static Dictionary<string, string> ModeCache { get; } = new(StringComparer.Ordinal);
    public static Dictionary<string, Dictionary<string, object?>> TenantRows { get; } = new(StringComparer.Ordinal);
    public static bool HasPdo { get; set; } = true;
    public static bool SchemaReady { get; set; }
    public static bool RegisterDone { get; set; }
    public static string AnchorNetwork { get; set; } = "local_merkle";
    public static List<string> Rands { get; set; } = [];
    public static int RandI { get; set; }
    public static long Ms { get; set; } = 1_760_083_200_000;
    public static long Now { get; set; } = 1_760_083_200;
    public static string HttpHost { get; set; } = "";
    public static string Https { get; set; } = "";
    public static string ServerPort { get; set; } = "";
    public static string? DomainPath { get; set; }
    public static string? ConfigSiteKey { get; set; }
    public static string? ConfigSharedSiteKey { get; set; }
    public static Func<string>? ClientErpKey { get; set; }
    public static Func<Dictionary<string, object?>?>? SharedActive { get; set; }
    public static Func<string>? SharedCookie { get; set; }
    public static Func<string>? PortalHost { get; set; }
    public static Func<string, Dictionary<string, object?>?>? LoadTenantByHost { get; set; }
    public static Func<string>? PlatformBaseUrl { get; set; }
    public static Action<string, string, Dictionary<string, object?>, Dictionary<string, object?>>? Enqueue { get; set; }

    public static void Reset()
    {
        Proofs.Clear();
        Batches.Clear();
        Jobs.Clear();
        Handlers.Clear();
        ModeCache.Clear();
        TenantRows.Clear();
        HasPdo = true;
        SchemaReady = false;
        RegisterDone = false;
        AnchorNetwork = "local_merkle";
        Rands =
        [
            "aaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbb", "cccccccccccccccc", "dddddddddddddddd",
            "eeeeeeeeeeeeeeee", "ffffffffffffffff", "1111111111111111", "2222222222222222",
            "3333333333333333", "4444444444444444", "5555555555555555", "6666666666666666"
        ];
        RandI = 0;
        Ms = 1_760_083_200_000;
        Now = 1_760_083_200;
        HttpHost = "";
        Https = "";
        ServerPort = "";
        DomainPath = null;
        ConfigSiteKey = null;
        ConfigSharedSiteKey = null;
        ClientErpKey = null;
        SharedActive = null;
        SharedCookie = null;
        PortalHost = null;
        LoadTenantByHost = null;
        PlatformBaseUrl = null;
        Enqueue = (type, tenant, payload, opts) =>
        {
            Jobs.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = type,
                ["tenant"] = tenant,
                ["payload"] = payload,
                ["opts"] = opts
            });
        };
    }

    public static Dictionary<string, string> EpcBcBosModes()
        => new(StringComparer.Ordinal)
        {
            ["off"] = "Off",
            ["anchor"] = "Blockchain anchor (recommended)",
            ["network"] = "Network participant (roadmap)"
        };

    public static string EpcBcBosNormalizeMode(string mode)
    {
        mode = mode.ToLowerInvariant().Trim();
        return EpcBcBosModes().ContainsKey(mode) ? mode : "off";
    }

    public static string EpcBcBosAnchorNetwork()
    {
        var n = (AnchorNetwork ?? "").Trim();
        return n != "" ? n : "local_merkle";
    }

    public static Dictionary<string, string> EpcBcBosTenantModeCacheStore()
        => ModeCache;

    public static void EpcBcBosClearTenantModeCache(string? siteKey = null)
    {
        if (string.IsNullOrEmpty(siteKey))
        {
            ModeCache.Clear();
            return;
        }

        ModeCache.Remove(SanitizeTenant(siteKey));
    }

    public static bool EpcBcBosPlatformPdo()
        => HasPdo;

    public static void EpcBcBosEnsureSchema()
    {
        if (!HasPdo || SchemaReady)
        {
            return;
        }

        SchemaReady = true;
    }

    public static bool EpcBcBosIsAssoc(object? arr)
    {
        if (arr is Dictionary<string, object?> map)
        {
            if (map.Count == 0)
            {
                return false;
            }

            var keys = map.Keys.ToList();
            return !IsListKeys(keys);
        }

        if (arr is System.Collections.IList && arr is not string)
        {
            return false;
        }

        return false;
    }

    public static string EpcBcBosCanonicalJson(object? data)
    {
        data = CanonNode(data);
        return PhpJson(data);
    }

    public static string EpcBcBosHash(object? payload)
    {
        var canonical = payload is string s ? s : EpcBcBosCanonicalJson(payload);
        return Sha256(canonical);
    }

    public static string EpcBcBosNewUid(string prefix = "prf")
    {
        var rand = RandI < Rands.Count ? Rands[RandI] : new string('a', 16);
        RandI++;
        var ms = Ms;
        Ms++;
        return prefix + "_" + rand + "_" + ms.ToString("x", CultureInfo.InvariantCulture);
    }

    public static Dictionary<string, object?> EpcBcBosRecordProof(
        string tenantKey, string recordType, string recordId, Dictionary<string, object?> payload, Dictionary<string, object?>? opts = null)
    {
        if (!HasPdo)
        {
            return Err("Platform DB unavailable");
        }

        EpcBcBosEnsureSchema();
        tenantKey = SanitizeTenant(tenantKey);
        recordType = SanitizeType(recordType);
        recordId = ClipId(recordId);
        if (recordType == "")
        {
            return Err("record_type required");
        }

        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var envelope = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_key"] = tenantKey,
            ["record_type"] = recordType,
            ["record_id"] = recordId,
            ["payload"] = payload,
            ["ts"] = opts.TryGetValue("ts", out var ts) ? PhpString(ts) : GmDate()
        };
        var canonical = EpcBcBosCanonicalJson(envelope);
        var hash = EpcBcBosHash(canonical);
        var uid = EpcBcBosNewUid("prf");
        var existing = Proofs
            .Where(p => p.TenantKey == tenantKey && p.RecordType == recordType && p.RecordId == recordId && p.PayloadHash == hash)
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();
        if (existing != null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["proof_uid"] = existing.ProofUid,
                ["payload_hash"] = existing.PayloadHash,
                ["status"] = existing.Status,
                ["deduped"] = true
            };
        }

        Proofs.Add(new ProofRow
        {
            Id = Proofs.Count + 1,
            ProofUid = uid,
            TenantKey = tenantKey,
            RecordType = recordType,
            RecordId = recordId,
            PayloadHash = hash,
            PayloadJson = canonical,
            Status = "pending"
        });
        if (!PhpEmpty(opts.TryGetValue("enqueue_anchor", out var enq) ? enq : null))
        {
            try
            {
                Enqueue?.Invoke(
                    "blockchain_anchor_batch",
                    tenantKey,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["reason"] = "proof_recorded",
                        ["proof_uid"] = uid
                    },
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["priority"] = 80,
                        ["dedupe"] = true,
                        ["delay_sec"] = 2
                    });
            }
            catch
            {
                // non-fatal
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["proof_uid"] = uid,
            ["payload_hash"] = hash,
            ["status"] = "pending"
        };
    }

    public static string EpcBcBosMerkleRoot(IEnumerable<object?> leaves)
    {
        var level = new List<string>();
        foreach (var h in leaves)
        {
            var v = PhpString(h).Trim().ToLowerInvariant();
            if (v == "")
            {
                continue;
            }

            level.Add(v);
        }

        if (level.Count == 0)
        {
            return Sha256("");
        }

        while (level.Count > 1)
        {
            var next = new List<string>();
            for (var i = 0; i < level.Count; i += 2)
            {
                var left = level[i];
                var right = i + 1 < level.Count ? level[i + 1] : left;
                next.Add(Sha256(left + right));
            }

            level = next;
        }

        return level[0];
    }

    public static List<Dictionary<string, object?>> EpcBcBosMerkleProofPath(IEnumerable<object?> leaves, int index)
    {
        var level = leaves.Select(PhpString).ToList();
        var path = new List<Dictionary<string, object?>>();
        var idx = index;
        while (level.Count > 1)
        {
            var count = level.Count;
            var isRight = idx % 2 == 1;
            var pair = isRight ? idx - 1 : idx + 1;
            if (pair >= count)
            {
                pair = idx;
            }

            path.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sibling"] = level[pair],
                ["side"] = isRight ? "left" : "right"
            });
            var next = new List<string>();
            for (var i = 0; i < count; i += 2)
            {
                var left = level[i];
                var right = i + 1 < count ? level[i + 1] : left;
                next.Add(Sha256(left + right));
            }

            level = next;
            idx = (int)Math.Floor(idx / 2.0);
        }

        return path;
    }

    public static bool EpcBcBosVerifyMerklePath(string leafHash, IEnumerable<Dictionary<string, object?>> path, string expectedRoot)
    {
        var hash = leafHash.Trim().ToLowerInvariant();
        foreach (var step in path)
        {
            var sib = PhpString(step.TryGetValue("sibling", out var s) ? s : "").Trim().ToLowerInvariant();
            var side = PhpString(step.TryGetValue("side", out var sd) ? sd : "right");
            hash = side == "left" ? Sha256(sib + hash) : Sha256(hash + sib);
        }

        return string.Equals(expectedRoot.Trim().ToLowerInvariant(), hash, StringComparison.Ordinal);
    }

    public static Dictionary<string, object?> EpcBcBosAnchorPendingBatch(int limit = 100)
    {
        if (!HasPdo)
        {
            return Err("Platform DB unavailable");
        }

        EpcBcBosEnsureSchema();
        limit = Math.Max(1, Math.Min(500, limit));
        var rows = Proofs.Where(p => p.Status == "pending").OrderBy(p => p.Id).Take(limit).ToList();
        if (rows.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["proof_count"] = 0,
                ["message"] = "nothing_pending"
            };
        }

        var leaves = rows.Select(r => r.PayloadHash).Cast<object?>().ToList();
        var root = EpcBcBosMerkleRoot(leaves);
        var batchUid = EpcBcBosNewUid("bat");
        var network = EpcBcBosAnchorNetwork();
        var anchorRef = network + ":" + root;
        var batch = new BatchRow
        {
            Id = Batches.Count + 1,
            BatchUid = batchUid,
            MerkleRoot = root,
            ProofCount = rows.Count,
            Status = "anchored",
            AnchorNetwork = network,
            AnchorRef = anchorRef,
            MetaJson = "{\"engine\":\"epc_blockchain_bos\",\"v\":1}"
        };
        Batches.Add(batch);
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Status != "pending")
            {
                continue;
            }

            var path = EpcBcBosMerkleProofPath(leaves, i);
            r.Status = "anchored";
            r.BatchId = batch.Id;
            r.MerkleIndex = i;
            r.MerkleProofJson = PhpJson(path);
            r.AnchoredAt = "NOW";
            r.AnchorRef = anchorRef;
            r.UpdatedAt = "NOW";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["batch_uid"] = batchUid,
            ["merkle_root"] = root,
            ["proof_count"] = rows.Count,
            ["anchor_ref"] = anchorRef,
            ["anchor_network"] = network
        };
    }

    public static Dictionary<string, object?> EpcBcBosVerify(string uidOrHash)
    {
        if (!HasPdo)
        {
            return Err("Platform DB unavailable");
        }

        EpcBcBosEnsureSchema();
        var key = uidOrHash.Trim();
        if (key == "")
        {
            return Err("proof_uid or hash required");
        }

        var proof = Proofs
            .Where(p => p.ProofUid == key || p.PayloadHash == key.ToLowerInvariant())
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();
        if (proof == null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["valid"] = false,
                ["error"] = "Proof not found"
            };
        }

        var rehash = proof.PayloadJson != "" ? EpcBcBosHash(proof.PayloadJson) : "";
        var hashOk = rehash != "" && string.Equals(proof.PayloadHash, rehash, StringComparison.Ordinal);
        Dictionary<string, object?>? batchOut = null;
        var merkleOk = false;
        if (!PhpEmpty(proof.BatchId))
        {
            var batch = Batches.FirstOrDefault(b => b.Id == proof.BatchId);
            if (batch != null)
            {
                var path = DecodePath(proof.MerkleProofJson);
                merkleOk = EpcBcBosVerifyMerklePath(proof.PayloadHash, path, batch.MerkleRoot);
                batchOut = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["batch_uid"] = batch.BatchUid,
                    ["merkle_root"] = batch.MerkleRoot,
                    ["proof_count"] = batch.ProofCount,
                    ["anchor_network"] = batch.AnchorNetwork,
                    ["anchor_ref"] = batch.AnchorRef,
                    ["anchored_at"] = batch.AnchoredAt
                };
            }
        }

        var valid = hashOk && (proof.Status == "pending" || (proof.Status == "anchored" && merkleOk));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["valid"] = valid,
            ["hash_ok"] = hashOk,
            ["merkle_ok"] = merkleOk,
            ["proof"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["proof_uid"] = proof.ProofUid,
                ["tenant_key"] = proof.TenantKey,
                ["record_type"] = proof.RecordType,
                ["record_id"] = proof.RecordId,
                ["payload_hash"] = proof.PayloadHash,
                ["status"] = proof.Status,
                ["anchor_ref"] = proof.AnchorRef,
                ["anchored_at"] = string.IsNullOrEmpty(proof.AnchoredAt) ? "" : proof.AnchoredAt,
                ["created_at"] = proof.CreatedAt
            },
            ["batch"] = batchOut,
            ["product"] = "ECOM AE Blockchain BOS Enterprise"
        };
    }

    public static Dictionary<string, object?> EpcBcBosJobAnchorBatch(string tenantKey, Dictionary<string, object?> payload, Dictionary<string, object?>? job = null)
    {
        var limit = payload.TryGetValue("limit", out var raw) ? PhpInt(raw) : 100;
        var result = EpcBcBosAnchorPendingBatch(limit);
        if (!PhpEmpty(result.TryGetValue("ok", out var ok) ? ok : null))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["result"] = result };
        }

        return Err(PhpString(result.TryGetValue("error", out var err) ? err : "anchor failed"));
    }

    public static void EpcBcBosRegisterJobHandlers()
    {
        if (RegisterDone)
        {
            return;
        }

        Handlers.Add("blockchain_anchor_batch");
        RegisterDone = true;
    }

    public static string EpcBcBosProductName(bool shortName = false)
        => shortName ? "Blockchain BOS" : "Blockchain BOS Enterprise System";

    public static string EpcBcBosProductTagline()
        => "One unified Blockchain Business Operating System — ERP, commerce, compliance, workflows, intelligence and cryptographic proof on one cloud.";

    public static string EpcBcBosResolveSiteKey(Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!PhpEmpty(opts.TryGetValue("tenant_key", out var tk) ? tk : null))
        {
            return SanitizeTenant(PhpString(tk));
        }

        if (ClientErpKey != null)
        {
            var k = ClientErpKey();
            if (k != "")
            {
                return k.ToLowerInvariant();
            }
        }

        if (!PhpEmpty(ConfigSharedSiteKey))
        {
            return SanitizeTenant(ConfigSharedSiteKey!);
        }

        if (!PhpEmpty(ConfigSiteKey))
        {
            return SanitizeTenant(ConfigSiteKey!);
        }

        try
        {
            var row = SharedActive?.Invoke();
            if (row != null && !PhpEmpty(row.TryGetValue("site_key", out var sk) ? sk : null))
            {
                return PhpString(sk).ToLowerInvariant();
            }

            var cookie = SharedCookie?.Invoke() ?? "";
            if (cookie != "")
            {
                return cookie.ToLowerInvariant();
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            var host = PortalHost?.Invoke() ?? "";
            if (host != "")
            {
                var profile = LoadTenantByHost?.Invoke(host);
                if (profile != null && !PhpEmpty(profile.TryGetValue("site_key", out var sk) ? sk : null))
                {
                    return PhpString(sk).ToLowerInvariant();
                }
            }
        }
        catch
        {
            // ignore
        }

        return "";
    }

    public static string EpcBcBosTenantMode(string siteKey)
    {
        siteKey = SanitizeTenant(siteKey);
        if (siteKey == "")
        {
            return "off";
        }

        if (ModeCache.TryGetValue(siteKey, out var cached))
        {
            return cached;
        }

        var mode = "anchor";
        try
        {
            if (HasPdo)
            {
                if (TenantRows.TryGetValue(siteKey, out var row))
                {
                    mode = EpcBcBosNormalizeMode(PhpString(row.TryGetValue("blockchain_mode", out var m) ? m : "anchor"));
                }
            }
        }
        catch
        {
            mode = "anchor";
        }

        ModeCache[siteKey] = mode;
        return mode;
    }

    public static Dictionary<string, object?> EpcBcBosMaybeRecordDocument(
        string recordType, string recordId, Dictionary<string, object?> payload, Dictionary<string, object?>? opts = null)
    {
        try
        {
            opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            var siteKey = EpcBcBosResolveSiteKey(opts);
            if (siteKey == "")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = false,
                    ["skipped"] = true,
                    ["reason"] = "no_tenant"
                };
            }

            if (EpcBcBosTenantMode(siteKey) == "off")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = true,
                    ["skipped"] = true,
                    ["reason"] = "mode_off"
                };
            }

            recordType = SanitizeType(recordType);
            recordId = ClipId(recordId);
            if (recordType == "" || recordId == "")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = false,
                    ["skipped"] = true,
                    ["reason"] = "missing_ids"
                };
            }

            var enqueue = opts.ContainsKey("enqueue_anchor") ? !PhpEmpty(opts["enqueue_anchor"]) : true;
            var result = EpcBcBosRecordProof(siteKey, recordType, recordId, payload, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["enqueue_anchor"] = enqueue,
                ["ts"] = opts.TryGetValue("ts", out var ts) ? PhpString(ts) : GmDate()
            });
            if (!PhpEmpty(result.TryGetValue("ok", out var ok) ? ok : null))
            {
                return result;
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["skipped"] = false,
                ["error"] = PhpString(result.TryGetValue("error", out var err) ? err : "record_failed")
            };
        }
        catch (Exception ex)
        {
            return Err(ex.Message);
        }
    }

    public static string EpcBcBosVerifyUrl(string proofUidOrHash)
        => "/epc-blockchain-verify.php?proof=" + Uri.EscapeDataString(proofUidOrHash.Trim());

    public static string EpcBcBosPublicBaseUrl()
    {
        try
        {
            var domain = (DomainPath ?? "").Trim();
            if (domain != "")
            {
                return domain.TrimEnd('/');
            }

            if (PlatformBaseUrl != null)
            {
                var baseUrl = PlatformBaseUrl().Trim();
                if (baseUrl != "")
                {
                    return baseUrl.TrimEnd('/');
                }
            }
        }
        catch
        {
            // fall through
        }

        var host = HttpHost.Trim().ToLowerInvariant();
        if (host != "")
        {
            var https = (!PhpEmpty(Https) && Https != "off") || ServerPort == "443";
            return (https ? "https://" : "http://") + host;
        }

        return "https://www.ecomae.com";
    }

    public static string EpcBcBosVerifyUrlAbsolute(string proofUidOrHash)
        => EpcBcBosPublicBaseUrl().TrimEnd('/') + EpcBcBosVerifyUrl(proofUidOrHash);

    public static Dictionary<string, object?>? EpcBcBosLookupProof(string tenantKey, string recordType, string recordId)
    {
        tenantKey = SanitizeTenant(tenantKey);
        recordType = SanitizeType(recordType);
        recordId = ClipId(recordId);
        if (tenantKey == "" || recordType == "" || recordId == "")
        {
            return null;
        }

        if (!HasPdo)
        {
            return null;
        }

        EpcBcBosEnsureSchema();
        var row = Proofs
            .Where(p => p.TenantKey == tenantKey && p.RecordType == recordType && p.RecordId == recordId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();
        return row == null ? null : ProofPub(row);
    }

    public static List<Dictionary<string, object?>> EpcBcBosListProofs(string tenantKey, Dictionary<string, object?>? filters = null)
    {
        tenantKey = SanitizeTenant(tenantKey);
        if (tenantKey == "" || !HasPdo)
        {
            return [];
        }

        EpcBcBosEnsureSchema();
        filters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var limit = filters.TryGetValue("limit", out var lim) ? Math.Max(1, Math.Min(200, PhpInt(lim))) : 100;
        IEnumerable<ProofRow> q = Proofs.Where(p => p.TenantKey == tenantKey);
        if (!PhpEmpty(filters.TryGetValue("record_type", out var rt) ? rt : null))
        {
            var type = SanitizeType(PhpString(rt));
            q = q.Where(p => p.RecordType == type);
        }

        if (!PhpEmpty(filters.TryGetValue("status", out var st) ? st : null))
        {
            var status = SanitizeTenant(PhpString(st));
            q = q.Where(p => p.Status == status);
        }

        return q.OrderByDescending(p => p.Id).Take(limit).Select(ProofPub).ToList();
    }

    public static List<Dictionary<string, object?>> EpcBcBosListProofsFleet(Dictionary<string, object?>? filters = null)
    {
        if (!HasPdo)
        {
            return [];
        }

        EpcBcBosEnsureSchema();
        filters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var limit = filters.TryGetValue("limit", out var lim) ? Math.Max(1, Math.Min(500, PhpInt(lim))) : 150;
        IEnumerable<ProofRow> q = Proofs;
        var tenantKey = SanitizeTenant(PhpString(
            filters.TryGetValue("tenant_key", out var tk) ? tk :
            filters.TryGetValue("site_key", out var sk) ? sk : ""));
        if (tenantKey != "")
        {
            q = q.Where(p => p.TenantKey == tenantKey);
        }

        if (!PhpEmpty(filters.TryGetValue("record_type", out var rt) ? rt : null))
        {
            var type = SanitizeType(PhpString(rt));
            q = q.Where(p => p.RecordType == type);
        }

        if (!PhpEmpty(filters.TryGetValue("status", out var st) ? st : null))
        {
            var status = SanitizeTenant(PhpString(st));
            q = q.Where(p => p.Status == status);
        }

        return q.OrderByDescending(p => p.Id).Take(limit).Select(ProofPub).ToList();
    }

    public static Dictionary<string, object?> EpcBcBosFleetStats()
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = 0,
            ["anchored"] = 0,
            ["pending"] = 0,
            ["tenants"] = 0
        };
        if (!HasPdo)
        {
            return result;
        }

        EpcBcBosEnsureSchema();
        result["total"] = Proofs.Count;
        result["anchored"] = Proofs.Count(p => p.Status == "anchored");
        result["pending"] = Proofs.Count(p => p.Status == "pending");
        result["tenants"] = Proofs.Select(p => p.TenantKey).Distinct(StringComparer.Ordinal).Count();
        return result;
    }

    public static string EpcBcBosProofBadgeHtml(Dictionary<string, object?>? proof, Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        if (proof == null)
        {
            return !PhpEmpty(opts.TryGetValue("show_missing", out var sm) ? sm : null)
                ? "<span class=\"label label-default\">No blockchain proof</span>"
                : "";
        }

        var status = PhpString(proof.TryGetValue("status", out var st) ? st : "pending").ToLowerInvariant();
        var tone = status == "anchored" ? "success" : status == "pending" ? "warning" : "default";
        var uid = PhpString(proof.TryGetValue("proof_uid", out var u) ? u : "");
        var label = status == "anchored" ? "Blockchain anchored" : "Blockchain proof pending";
        var html = "<span class=\"label label-" + H(tone) + "\">" + H(label) + "</span>";
        if (uid != "")
        {
            html += " <a href=\"" + H(EpcBcBosVerifyUrl(uid))
                + "\" target=\"_blank\" rel=\"noopener\" class=\"btn btn-default btn-xs\" style=\"margin-left:6px\">"
                + "<i class=\"fa fa-external-link\"></i> Verify</a>";
            if (!PhpEmpty(opts.TryGetValue("show_uid", out var su) ? su : null))
            {
                html += " <small class=\"text-muted\"><code>" + H(uid) + "</code></small>";
            }
        }

        return html;
    }

    public static object?[] EpcBcBosEinvoiceRecordKeys(Dictionary<string, object?> doc)
    {
        var cat = PhpString(doc.TryGetValue("doc_category", out var c) ? c : "tax_invoice");
        var typeCode = PhpString(doc.TryGetValue("invoice_type_code", out var t) ? t : "380");
        var recordType = cat == "tax_credit_note" || typeCode == "381" ? "credit_note" : "invoice";
        var invNo = PhpString(doc.TryGetValue("invoice_number", out var n) ? n : "").Trim();
        var recordId = invNo != "" ? invNo : PhpInt(doc.TryGetValue("id", out var id) ? id : 0).ToString(CultureInfo.InvariantCulture);
        return [recordType, recordId];
    }

    public static string EpcBcBosGrnRecordId(Dictionary<string, object?> purchase)
    {
        var purchaseId = PhpInt(purchase.TryGetValue("id", out var id) ? id : 0);
        var invNo = PhpString(purchase.TryGetValue("invoice_number", out var n) ? n : "").Trim();
        if (invNo != "")
        {
            return "PINV-" + invNo;
        }

        return purchaseId > 0 ? "PINV-" + purchaseId.ToString(CultureInfo.InvariantCulture) : "";
    }

    public static string EpcBcBosGrnBadgeHtml(Dictionary<string, object?> purchase, Dictionary<string, object?>? opts = null)
    {
        if (PhpEmpty(purchase.TryGetValue("inv_receipt_posted", out var a) ? a : null)
            && PhpEmpty(purchase.TryGetValue("inventory_receipt_posted", out var b) ? b : null))
        {
            return "";
        }

        var rid = EpcBcBosGrnRecordId(purchase);
        return rid == "" ? "" : EpcBcBosDocumentBadgeHtml("grn", rid, opts);
    }

    public static Dictionary<string, object?> EpcBcBosGrnFlashForPurchase(Dictionary<string, object?> purchase)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = "",
            ["extra"] = new List<object?>()
        };
        try
        {
            if (PhpEmpty(purchase.TryGetValue("inv_receipt_posted", out var a) ? a : null)
                && PhpEmpty(purchase.TryGetValue("inventory_receipt_posted", out var b) ? b : null))
            {
                return result;
            }

            var grnRef = EpcBcBosGrnRecordId(purchase);
            var siteKey = EpcBcBosResolveSiteKey();
            if (siteKey == "" || grnRef == "")
            {
                return result;
            }

            var proof = EpcBcBosLookupProof(siteKey, "grn", grnRef);
            if (proof == null || PhpEmpty(proof.TryGetValue("proof_uid", out var uid) ? uid : null))
            {
                return result;
            }

            var extra = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["blockchain_proof_uid"] = PhpString(uid),
                ["blockchain_verify_url"] = EpcBcBosVerifyUrl(PhpString(uid)),
                ["grn_record_id"] = grnRef
            };
            result["extra"] = extra;
            result["message"] = " · Blockchain GRN proof " + PhpString(proof.TryGetValue("status", out var st) ? st : "pending")
                + " — verify " + extra["blockchain_verify_url"];
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["message"] = "",
                ["extra"] = new List<object?>()
            };
        }

        return result;
    }

    public static Dictionary<string, object?> EpcBcBosTenantProofStats(string? siteKey = null)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = 0,
            ["anchored"] = 0,
            ["pending"] = 0,
            ["mode"] = "off",
            ["site_key"] = ""
        };
        try
        {
            siteKey = !string.IsNullOrEmpty(siteKey) ? siteKey : EpcBcBosResolveSiteKey();
            siteKey = SanitizeTenant(siteKey ?? "");
            if (siteKey == "")
            {
                return result;
            }

            result["site_key"] = siteKey;
            result["mode"] = EpcBcBosTenantMode(siteKey);
            if ((string)result["mode"]! == "off")
            {
                return result;
            }

            var rows = EpcBcBosListProofs(siteKey, new Dictionary<string, object?>(StringComparer.Ordinal) { ["limit"] = 500 });
            result["total"] = rows.Count;
            var anchored = 0;
            var pending = 0;
            foreach (var r in rows)
            {
                if (PhpString(r.TryGetValue("status", out var st) ? st : "") == "anchored")
                {
                    anchored++;
                }
                else
                {
                    pending++;
                }
            }

            result["anchored"] = anchored;
            result["pending"] = pending;
        }
        catch
        {
            // keep zeros
        }

        return result;
    }

    public static string EpcBcBosDocumentBadgeHtml(string recordType, string recordId, Dictionary<string, object?>? opts = null)
    {
        try
        {
            opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            var siteKey = EpcBcBosResolveSiteKey(opts);
            if (siteKey == "")
            {
                return "";
            }

            if (EpcBcBosTenantMode(siteKey) == "off")
            {
                return "";
            }

            return EpcBcBosProofBadgeHtml(EpcBcBosLookupProof(siteKey, recordType, recordId), opts);
        }
        catch
        {
            return "";
        }
    }

    public static Dictionary<string, object?> ProofPub(ProofRow row)
    {
        BatchRow? batch = null;
        if (row.BatchId is > 0)
        {
            batch = Batches.FirstOrDefault(b => b.Id == row.BatchId);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["proof_uid"] = row.ProofUid,
            ["tenant_key"] = row.TenantKey,
            ["record_type"] = row.RecordType,
            ["record_id"] = row.RecordId,
            ["payload_hash"] = row.PayloadHash,
            ["status"] = row.Status,
            ["batch_uid"] = batch?.BatchUid ?? "",
            ["merkle_root"] = batch?.MerkleRoot ?? "",
            ["merkle_index"] = row.MerkleIndex,
            ["anchor_ref"] = row.AnchorRef,
            ["anchored_at"] = string.IsNullOrEmpty(row.AnchoredAt) ? "" : "NOW"
        };
    }

    private static object? CanonNode(object? data)
    {
        if (AsMap(data, out var map) && EpcBcBosIsAssoc(map))
        {
            var ordered = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in map.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                ordered[kv.Key] = IsArrayish(kv.Value) ? JsonDecode(EpcBcBosCanonicalJson(kv.Value)) : kv.Value;
            }

            return ordered;
        }

        if (AsList(data, out var list))
        {
            return list.Select(v => IsArrayish(v) ? JsonDecode(EpcBcBosCanonicalJson(v)) : v).ToList();
        }

        return data;
    }

    private static bool AsMap(object? data, out Dictionary<string, object?> map)
    {
        if (data is Dictionary<string, object?> typed)
        {
            map = typed;
            return true;
        }

        map = new Dictionary<string, object?>(StringComparer.Ordinal);
        return false;
    }

    private static bool AsList(object? data, out List<object?> list)
    {
        if (data is List<object?> typed)
        {
            list = typed;
            return true;
        }

        if (data is System.Collections.IList raw && data is not string)
        {
            list = raw.Cast<object?>().ToList();
            return true;
        }

        list = [];
        return false;
    }

    private static bool IsArrayish(object? value)
        => value is Dictionary<string, object?> || (value is System.Collections.IList && value is not string);

    private static bool IsListKeys(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < keys.Count; i++)
        {
            if (keys[i] != i.ToString(CultureInfo.InvariantCulture))
            {
                return false;
            }
        }

        return true;
    }

    private static string PhpJson(object? data)
    {
        if (data == null)
        {
            return "null";
        }

        switch (data)
        {
            case bool b:
                return b ? "true" : "false";
            case string s:
                return "\"" + EscapeJson(s) + "\"";
            case int or long or short or byte or sbyte or uint or ushort or ulong:
                return Convert.ToString(data, CultureInfo.InvariantCulture) ?? "0";
            case double or float or decimal:
                var num = Convert.ToDouble(data, CultureInfo.InvariantCulture);
                return Math.Abs(num - Math.Truncate(num)) < double.Epsilon
                    ? ((long)num).ToString(CultureInfo.InvariantCulture)
                    : num.ToString("0.################", CultureInfo.InvariantCulture);
            case Dictionary<string, object?> map:
                return "{" + string.Join(",", map.Select(kv => PhpJson(kv.Key) + ":" + PhpJson(kv.Value))) + "}";
            case System.Collections.IList list when data is not string:
                return "[" + string.Join(",", list.Cast<object?>().Select(PhpJson)) + "]";
            default:
                return PhpJson(PhpString(data));
        }
    }

    private static string EscapeJson(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (ch < ' ')
                    {
                        sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    private static object? JsonDecode(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return FromJson(doc.RootElement);
    }

    private static object? FromJson(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => el.EnumerateArray().Select(FromJson).Cast<object?>().ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number when el.TryGetInt64(out var n) && n is >= int.MinValue and <= int.MaxValue => (int)n,
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };

    private static List<Dictionary<string, object?>> DecodePath(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            var decoded = JsonDecode(json);
            if (decoded is List<object?> list)
            {
                return list.OfType<Dictionary<string, object?>>().ToList();
            }
        }
        catch (JsonException)
        {
            // empty
        }

        return [];
    }

    private static Dictionary<string, object?> Err(string error)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["error"] = error };

    private static string SanitizeTenant(string value)
        => Regex.Replace(value ?? "", "[^a-z0-9_]", "").ToLowerInvariant();

    private static string SanitizeType(string value)
        => Regex.Replace(value ?? "", "[^a-z0-9_\\-]", "").ToLowerInvariant();

    private static string ClipId(string value)
    {
        value = (value ?? "").Trim();
        return value.Length <= 128 ? value : value[..128];
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string GmDate()
        => DateTimeOffset.FromUnixTimeSeconds(Now).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string H(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static int PhpInt(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return (int)l;
        }

        var text = PhpString(value);
        if (text.Length == 0)
        {
            return 0;
        }

        var n2 = 0;
        var sign = 1;
        var p = 0;
        while (p < text.Length && char.IsWhiteSpace(text[p]))
        {
            p++;
        }

        if (p < text.Length && (text[p] == '+' || text[p] == '-'))
        {
            sign = text[p] == '-' ? -1 : 1;
            p++;
        }

        var any = false;
        while (p < text.Length && char.IsAsciiDigit(text[p]))
        {
            any = true;
            n2 = (n2 * 10) + (text[p] - '0');
            p++;
        }

        return any ? sign * n2 : 0;
    }
}
