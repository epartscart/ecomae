<?php
/**
 * Blockchain BOS Enterprise — proof / integrity layer.
 *
 * Operational truth stays in tenant MySQL (orders, stock, GL).
 * This module makes selected business facts cryptographically verifiable:
 *   1) Canonical JSON → SHA-256 proof hash
 *   2) Queue into epc_bc_proofs
 *   3) Batch-anchor via Merkle root (platform job)
 *   4) Public verify endpoint
 *
 * Modes (tenant / platform):
 *   off          — disabled
 *   anchor       — hash + Merkle batch anchor (default for Blockchain BOS)
 *   network      — reserved for permissioned network participation
 */

function epc_bc_bos_modes(): array
{
    return [
        'off' => 'Off',
        'anchor' => 'Blockchain anchor (recommended)',
        'network' => 'Network participant (roadmap)',
    ];
}

function epc_bc_bos_normalize_mode(string $mode): string
{
    $mode = strtolower(trim($mode));
    return isset(epc_bc_bos_modes()[$mode]) ? $mode : 'off';
}

function epc_bc_bos_platform_pdo(): ?PDO
{
    static $pdo = null;
    static $failed = false;
    if ($failed) {
        return null;
    }
    if ($pdo instanceof PDO) {
        return $pdo;
    }
    try {
        require_once __DIR__ . '/epc_portal_tenant.php';
        $pdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
        if (!$pdo instanceof PDO) {
            $failed = true;
            return null;
        }
        return $pdo;
    } catch (Throwable $e) {
        $failed = true;
        return null;
    }
}

function epc_bc_bos_ensure_schema(?PDO $pdo = null): void
{
    $pdo = $pdo instanceof PDO ? $pdo : epc_bc_bos_platform_pdo();
    if (!$pdo instanceof PDO) {
        return;
    }
    static $ready = false;
    if ($ready) {
        return;
    }
    try {
        $pdo->exec(
            "CREATE TABLE IF NOT EXISTS `epc_bc_proofs` (
                `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                `proof_uid` VARCHAR(64) NOT NULL,
                `tenant_key` VARCHAR(64) NOT NULL DEFAULT '',
                `record_type` VARCHAR(64) NOT NULL,
                `record_id` VARCHAR(128) NOT NULL DEFAULT '',
                `payload_hash` CHAR(64) NOT NULL,
                `payload_json` LONGTEXT NULL,
                `status` VARCHAR(24) NOT NULL DEFAULT 'pending',
                `batch_id` BIGINT UNSIGNED NULL,
                `merkle_index` INT NULL,
                `merkle_proof_json` LONGTEXT NULL,
                `anchored_at` DATETIME NULL,
                `anchor_ref` VARCHAR(255) NOT NULL DEFAULT '',
                `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_proof_uid` (`proof_uid`),
                KEY `idx_hash` (`payload_hash`),
                KEY `idx_tenant_type` (`tenant_key`, `record_type`, `status`),
                KEY `idx_batch` (`batch_id`, `status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
        );
        $pdo->exec(
            "CREATE TABLE IF NOT EXISTS `epc_bc_anchor_batches` (
                `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                `batch_uid` VARCHAR(64) NOT NULL,
                `merkle_root` CHAR(64) NOT NULL,
                `proof_count` INT NOT NULL DEFAULT 0,
                `status` VARCHAR(24) NOT NULL DEFAULT 'anchored',
                `anchor_network` VARCHAR(64) NOT NULL DEFAULT 'local_merkle',
                `anchor_ref` VARCHAR(255) NOT NULL DEFAULT '',
                `anchored_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `meta_json` LONGTEXT NULL,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_batch_uid` (`batch_uid`),
                KEY `idx_root` (`merkle_root`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
        );
        $ready = true;
    } catch (Throwable $e) {
        // best-effort
    }
}

/**
 * Stable canonical JSON for hashing (sorted keys, unescaped unicode).
 *
 * @param mixed $data
 */
function epc_bc_bos_canonical_json($data): string
{
    if (is_array($data)) {
        if (epc_bc_bos_is_assoc($data)) {
            ksort($data);
            $out = [];
            foreach ($data as $k => $v) {
                $out[(string)$k] = $v;
            }
            $data = $out;
        } else {
            $data = array_values($data);
        }
        foreach ($data as $k => $v) {
            if (is_array($v)) {
                $data[$k] = json_decode(epc_bc_bos_canonical_json($v), true);
            }
        }
    }
    $json = json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    return is_string($json) ? $json : 'null';
}

function epc_bc_bos_is_assoc(array $arr): bool
{
    if ($arr === []) {
        return false;
    }
    return array_keys($arr) !== range(0, count($arr) - 1);
}

/**
 * @param array<string,mixed>|string $payload
 */
function epc_bc_bos_hash($payload): string
{
    if (is_string($payload)) {
        $canonical = $payload;
    } else {
        $canonical = epc_bc_bos_canonical_json($payload);
    }
    return hash('sha256', $canonical);
}

function epc_bc_bos_new_uid(string $prefix = 'prf'): string
{
    try {
        $rand = bin2hex(random_bytes(8));
    } catch (Throwable $e) {
        $rand = substr(md5(uniqid((string)mt_rand(), true)), 0, 16);
    }
    return $prefix . '_' . $rand . '_' . dechex((int)(microtime(true) * 1000));
}

/**
 * Record a business-fact proof. Returns proof row summary or error.
 *
 * @param array<string,mixed> $payload
 * @return array{ok:bool,proof_uid?:string,payload_hash?:string,status?:string,error?:string}
 */
function epc_bc_bos_record_proof(string $tenantKey, string $recordType, string $recordId, array $payload, array $opts = []): array
{
    $pdo = epc_bc_bos_platform_pdo();
    if (!$pdo instanceof PDO) {
        return ['ok' => false, 'error' => 'Platform DB unavailable'];
    }
    epc_bc_bos_ensure_schema($pdo);

    $tenantKey = strtolower(preg_replace('/[^a-z0-9_]/', '', $tenantKey) ?: '');
    $recordType = strtolower(preg_replace('/[^a-z0-9_\-]/', '', $recordType) ?: '');
    $recordId = substr(trim($recordId), 0, 128);
    if ($recordType === '') {
        return ['ok' => false, 'error' => 'record_type required'];
    }

    $envelope = [
        'tenant_key' => $tenantKey,
        'record_type' => $recordType,
        'record_id' => $recordId,
        'payload' => $payload,
        'ts' => (string)($opts['ts'] ?? gmdate('c')),
    ];
    $canonical = epc_bc_bos_canonical_json($envelope);
    $hash = epc_bc_bos_hash($canonical);
    $uid = epc_bc_bos_new_uid('prf');

    try {
        // Dedupe identical pending/anchored hash for same tenant+type+record
        $st = $pdo->prepare(
            "SELECT proof_uid, payload_hash, status FROM epc_bc_proofs
             WHERE tenant_key = ? AND record_type = ? AND record_id = ? AND payload_hash = ?
             ORDER BY id DESC LIMIT 1"
        );
        $st->execute([$tenantKey, $recordType, $recordId, $hash]);
        $existing = $st->fetch(PDO::FETCH_ASSOC);
        if ($existing) {
            return [
                'ok' => true,
                'proof_uid' => (string)$existing['proof_uid'],
                'payload_hash' => (string)$existing['payload_hash'],
                'status' => (string)$existing['status'],
                'deduped' => true,
            ];
        }

        $ins = $pdo->prepare(
            "INSERT INTO epc_bc_proofs
                (proof_uid, tenant_key, record_type, record_id, payload_hash, payload_json, status)
             VALUES (?, ?, ?, ?, ?, ?, 'pending')"
        );
        $ins->execute([$uid, $tenantKey, $recordType, $recordId, $hash, $canonical]);

        if (!empty($opts['enqueue_anchor'])) {
            try {
                require_once __DIR__ . '/epc_platform_jobs.php';
                epc_platform_jobs_enqueue(
                    'blockchain_anchor_batch',
                    $tenantKey,
                    ['reason' => 'proof_recorded', 'proof_uid' => $uid],
                    ['priority' => 80, 'dedupe' => true, 'delay_sec' => 2]
                );
            } catch (Throwable $e) {
                // non-fatal
            }
        }

        return [
            'ok' => true,
            'proof_uid' => $uid,
            'payload_hash' => $hash,
            'status' => 'pending',
        ];
    } catch (Throwable $e) {
        return ['ok' => false, 'error' => $e->getMessage()];
    }
}

/**
 * Build a simple binary Merkle root over a list of hex hashes.
 *
 * @param list<string> $leaves
 */
function epc_bc_bos_merkle_root(array $leaves): string
{
    $level = [];
    foreach ($leaves as $h) {
        $h = strtolower(trim((string)$h));
        if ($h === '') {
            continue;
        }
        $level[] = $h;
    }
    if ($level === []) {
        return hash('sha256', '');
    }
    while (count($level) > 1) {
        $next = [];
        $count = count($level);
        for ($i = 0; $i < $count; $i += 2) {
            $left = $level[$i];
            $right = $level[$i + 1] ?? $left;
            $next[] = hash('sha256', $left . $right);
        }
        $level = $next;
    }
    return $level[0];
}

/**
 * @param list<string> $leaves ordered hashes
 * @return list<array{sibling:string,side:string}>
 */
function epc_bc_bos_merkle_proof_path(array $leaves, int $index): array
{
    $level = array_values($leaves);
    $path = [];
    $idx = $index;
    while (count($level) > 1) {
        $count = count($level);
        $isRight = ($idx % 2) === 1;
        $pair = $isRight ? $idx - 1 : $idx + 1;
        if ($pair >= $count) {
            $pair = $idx; // duplicated last leaf
        }
        $path[] = [
            'sibling' => $level[$pair],
            'side' => $isRight ? 'left' : 'right',
        ];
        $next = [];
        for ($i = 0; $i < $count; $i += 2) {
            $left = $level[$i];
            $right = $level[$i + 1] ?? $left;
            $next[] = hash('sha256', $left . $right);
        }
        $level = $next;
        $idx = (int)floor($idx / 2);
    }
    return $path;
}

function epc_bc_bos_verify_merkle_path(string $leafHash, array $path, string $expectedRoot): bool
{
    $hash = strtolower(trim($leafHash));
    foreach ($path as $step) {
        $sib = strtolower(trim((string)($step['sibling'] ?? '')));
        $side = (string)($step['side'] ?? 'right');
        if ($side === 'left') {
            $hash = hash('sha256', $sib . $hash);
        } else {
            $hash = hash('sha256', $hash . $sib);
        }
    }
    return hash_equals(strtolower(trim($expectedRoot)), $hash);
}

/**
 * Anchor up to $limit pending proofs into one Merkle batch.
 *
 * @return array{ok:bool,batch_uid?:string,merkle_root?:string,proof_count?:int,error?:string}
 */
function epc_bc_bos_anchor_pending_batch(int $limit = 100): array
{
    $pdo = epc_bc_bos_platform_pdo();
    if (!$pdo instanceof PDO) {
        return ['ok' => false, 'error' => 'Platform DB unavailable'];
    }
    epc_bc_bos_ensure_schema($pdo);
    $limit = max(1, min(500, $limit));

    try {
        $st = $pdo->prepare(
            "SELECT id, proof_uid, payload_hash FROM epc_bc_proofs
             WHERE status = 'pending'
             ORDER BY id ASC
             LIMIT {$limit}"
        );
        $st->execute();
        $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
        if (!$rows) {
            return ['ok' => true, 'proof_count' => 0, 'message' => 'nothing_pending'];
        }

        $leaves = [];
        foreach ($rows as $r) {
            $leaves[] = (string)$r['payload_hash'];
        }
        $root = epc_bc_bos_merkle_root($leaves);
        $batchUid = epc_bc_bos_new_uid('bat');
        $network = (string)(getenv('EPC_BC_ANCHOR_NETWORK') ?: 'local_merkle');
        $anchorRef = $network . ':' . $root;

        $pdo->beginTransaction();
        $ins = $pdo->prepare(
            "INSERT INTO epc_bc_anchor_batches
                (batch_uid, merkle_root, proof_count, status, anchor_network, anchor_ref, meta_json)
             VALUES (?, ?, ?, 'anchored', ?, ?, ?)"
        );
        $ins->execute([
            $batchUid,
            $root,
            count($rows),
            $network,
            $anchorRef,
            json_encode(['engine' => 'epc_blockchain_bos', 'v' => 1], JSON_UNESCAPED_SLASHES),
        ]);
        $batchId = (int)$pdo->lastInsertId();

        $upd = $pdo->prepare(
            "UPDATE epc_bc_proofs
             SET status = 'anchored',
                 batch_id = ?,
                 merkle_index = ?,
                 merkle_proof_json = ?,
                 anchored_at = NOW(),
                 anchor_ref = ?,
                 updated_at = NOW()
             WHERE id = ? AND status = 'pending'"
        );
        foreach ($rows as $i => $r) {
            $path = epc_bc_bos_merkle_proof_path($leaves, (int)$i);
            $upd->execute([
                $batchId,
                (int)$i,
                json_encode($path, JSON_UNESCAPED_SLASHES),
                $anchorRef,
                (int)$r['id'],
            ]);
        }
        $pdo->commit();

        return [
            'ok' => true,
            'batch_uid' => $batchUid,
            'merkle_root' => $root,
            'proof_count' => count($rows),
            'anchor_ref' => $anchorRef,
            'anchor_network' => $network,
        ];
    } catch (Throwable $e) {
        if ($pdo->inTransaction()) {
            $pdo->rollBack();
        }
        return ['ok' => false, 'error' => $e->getMessage()];
    }
}

/**
 * Verify a proof by uid or payload hash.
 *
 * @return array{ok:bool,valid?:bool,proof?:array,batch?:array,error?:string}
 */
function epc_bc_bos_verify(string $uidOrHash): array
{
    $pdo = epc_bc_bos_platform_pdo();
    if (!$pdo instanceof PDO) {
        return ['ok' => false, 'error' => 'Platform DB unavailable'];
    }
    epc_bc_bos_ensure_schema($pdo);

    $key = trim($uidOrHash);
    if ($key === '') {
        return ['ok' => false, 'error' => 'proof_uid or hash required'];
    }

    try {
        $st = $pdo->prepare(
            "SELECT * FROM epc_bc_proofs WHERE proof_uid = ? OR payload_hash = ? ORDER BY id DESC LIMIT 1"
        );
        $st->execute([$key, strtolower($key)]);
        $proof = $st->fetch(PDO::FETCH_ASSOC);
        if (!$proof) {
            return ['ok' => true, 'valid' => false, 'error' => 'Proof not found'];
        }

        $payloadJson = (string)($proof['payload_json'] ?? '');
        $rehash = $payloadJson !== '' ? epc_bc_bos_hash($payloadJson) : '';
        $hashOk = $rehash !== '' && hash_equals((string)$proof['payload_hash'], $rehash);

        $batch = null;
        $merkleOk = false;
        if (!empty($proof['batch_id'])) {
            $bst = $pdo->prepare('SELECT * FROM epc_bc_anchor_batches WHERE id = ? LIMIT 1');
            $bst->execute([(int)$proof['batch_id']]);
            $batch = $bst->fetch(PDO::FETCH_ASSOC) ?: null;
            if ($batch) {
                $path = json_decode((string)($proof['merkle_proof_json'] ?? '[]'), true);
                if (!is_array($path)) {
                    $path = [];
                }
                $merkleOk = epc_bc_bos_verify_merkle_path(
                    (string)$proof['payload_hash'],
                    $path,
                    (string)$batch['merkle_root']
                );
            }
        }

        $valid = $hashOk && (
            (string)$proof['status'] === 'pending'
            || ((string)$proof['status'] === 'anchored' && $merkleOk)
        );

        return [
            'ok' => true,
            'valid' => $valid,
            'hash_ok' => $hashOk,
            'merkle_ok' => $merkleOk,
            'proof' => [
                'proof_uid' => (string)$proof['proof_uid'],
                'tenant_key' => (string)$proof['tenant_key'],
                'record_type' => (string)$proof['record_type'],
                'record_id' => (string)$proof['record_id'],
                'payload_hash' => (string)$proof['payload_hash'],
                'status' => (string)$proof['status'],
                'anchor_ref' => (string)$proof['anchor_ref'],
                'anchored_at' => (string)($proof['anchored_at'] ?? ''),
                'created_at' => (string)($proof['created_at'] ?? ''),
            ],
            'batch' => $batch ? [
                'batch_uid' => (string)$batch['batch_uid'],
                'merkle_root' => (string)$batch['merkle_root'],
                'proof_count' => (int)$batch['proof_count'],
                'anchor_network' => (string)$batch['anchor_network'],
                'anchor_ref' => (string)$batch['anchor_ref'],
                'anchored_at' => (string)$batch['anchored_at'],
            ] : null,
            'product' => 'ECOM AE Blockchain BOS Enterprise',
        ];
    } catch (Throwable $e) {
        return ['ok' => false, 'error' => $e->getMessage()];
    }
}

function epc_bc_bos_job_anchor_batch(string $tenantKey, array $payload, array $job = []): array
{
    $limit = isset($payload['limit']) ? (int)$payload['limit'] : 100;
    $out = epc_bc_bos_anchor_pending_batch($limit);
    if (!empty($out['ok'])) {
        return ['ok' => true, 'result' => $out];
    }
    return ['ok' => false, 'error' => (string)($out['error'] ?? 'anchor failed')];
}

/**
 * Register blockchain job handlers with the platform queue (idempotent).
 */
function epc_bc_bos_register_job_handlers(): void
{
    static $done = false;
    if ($done) {
        return;
    }
    require_once __DIR__ . '/epc_platform_jobs.php';
    epc_platform_jobs_register_handler('blockchain_anchor_batch', 'epc_bc_bos_job_anchor_batch');
    $done = true;
}

/**
 * Branding helper for marketing / UI.
 */
function epc_bc_bos_product_name(bool $short = false): string
{
    return $short ? 'Blockchain BOS' : 'Blockchain BOS Enterprise System';
}

function epc_bc_bos_product_tagline(): string
{
    return 'One unified Blockchain Business Operating System — ERP, commerce, compliance, workflows, intelligence and cryptographic proof on one cloud.';
}

/**
 * Resolve current tenant site_key from runtime context.
 */
function epc_bc_bos_resolve_site_key(array $opts = []): string
{
    if (!empty($opts['tenant_key'])) {
        return strtolower(preg_replace('/[^a-z0-9_]/', '', (string)$opts['tenant_key']) ?: '');
    }

    if (function_exists('epc_client_erp_site_key')) {
        $k = epc_client_erp_site_key();
        if ($k !== '') {
            return strtolower($k);
        }
    } elseif (is_file(__DIR__ . '/epc_client_erp_router.php')) {
        require_once __DIR__ . '/epc_client_erp_router.php';
        if (function_exists('epc_client_erp_site_key')) {
            $k = epc_client_erp_site_key();
            if ($k !== '') {
                return strtolower($k);
            }
        }
    }

    if (!empty($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config'])) {
        if (!empty($GLOBALS['DP_Config']->epc_shared_erp_site_key)) {
            return strtolower(preg_replace('/[^a-z0-9_]/', '', (string)$GLOBALS['DP_Config']->epc_shared_erp_site_key) ?: '');
        }
        if (!empty($GLOBALS['DP_Config']->site_key)) {
            return strtolower(preg_replace('/[^a-z0-9_]/', '', (string)$GLOBALS['DP_Config']->site_key) ?: '');
        }
    }

    try {
        if (is_file(__DIR__ . '/epc_portal_shared_erp.php')) {
            require_once __DIR__ . '/epc_portal_shared_erp.php';
            if (function_exists('epc_portal_shared_erp_active_tenant')) {
                $row = epc_portal_shared_erp_active_tenant();
                if (is_array($row) && !empty($row['site_key'])) {
                    return strtolower((string)$row['site_key']);
                }
            }
            if (function_exists('epc_portal_shared_erp_cookie_site_key')) {
                $k = epc_portal_shared_erp_cookie_site_key();
                if ($k !== '') {
                    return strtolower($k);
                }
            }
        }
    } catch (Throwable $e) {
        // ignore
    }

    try {
        if (is_file(__DIR__ . '/epc_portal_tenant.php')) {
            require_once __DIR__ . '/epc_portal_tenant.php';
            if (function_exists('epc_portal_host') && function_exists('epc_portal_load_tenant_by_host')) {
                $host = epc_portal_host();
                if ($host !== '') {
                    $profile = epc_portal_load_tenant_by_host($host);
                    if (is_array($profile) && !empty($profile['site_key'])) {
                        return strtolower((string)$profile['site_key']);
                    }
                }
            }
        }
    } catch (Throwable $e) {
        // ignore
    }

    return '';
}

/**
 * Load blockchain_mode for a tenant (request-cached).
 */
function epc_bc_bos_tenant_mode(string $siteKey): string
{
    static $cache = [];
    $siteKey = strtolower(preg_replace('/[^a-z0-9_]/', '', $siteKey) ?: '');
    if ($siteKey === '') {
        return 'off';
    }
    if (isset($cache[$siteKey])) {
        return $cache[$siteKey];
    }

    $mode = 'anchor'; // product default when registry unavailable
    try {
        $pdo = epc_bc_bos_platform_pdo();
        if ($pdo instanceof PDO) {
            require_once __DIR__ . '/epc_portal_tenant_intro.php';
            $row = epc_portal_tenant_get($pdo, $siteKey);
            if (is_array($row)) {
                $mode = epc_bc_bos_normalize_mode((string)($row['blockchain_mode'] ?? 'anchor'));
            }
        }
    } catch (Throwable $e) {
        $mode = 'anchor';
    }
    $cache[$siteKey] = $mode;
    return $mode;
}

/**
 * Best-effort document proof. Never throws; never blocks business commits.
 *
 * @param array<string,mixed> $payload
 * @param array<string,mixed> $opts tenant_key?, enqueue_anchor?, skip_if_draft?
 * @return array{ok:bool,skipped?:bool,reason?:string,proof_uid?:string,payload_hash?:string,status?:string,error?:string}
 */
function epc_bc_bos_maybe_record_document(string $recordType, string $recordId, array $payload, array $opts = []): array
{
    try {
        $siteKey = epc_bc_bos_resolve_site_key($opts);
        if ($siteKey === '') {
            return ['ok' => false, 'skipped' => true, 'reason' => 'no_tenant'];
        }
        $mode = epc_bc_bos_tenant_mode($siteKey);
        if ($mode === 'off') {
            return ['ok' => true, 'skipped' => true, 'reason' => 'mode_off'];
        }

        $recordType = strtolower(preg_replace('/[^a-z0-9_\-]/', '', $recordType) ?: '');
        $recordId = substr(trim((string)$recordId), 0, 128);
        if ($recordType === '' || $recordId === '') {
            return ['ok' => false, 'skipped' => true, 'reason' => 'missing_ids'];
        }

        $enqueue = array_key_exists('enqueue_anchor', $opts)
            ? !empty($opts['enqueue_anchor'])
            : true;

        $out = epc_bc_bos_record_proof($siteKey, $recordType, $recordId, $payload, [
            'enqueue_anchor' => $enqueue,
            'ts' => (string)($opts['ts'] ?? gmdate('c')),
        ]);
        if (!empty($out['ok'])) {
            return $out;
        }
        return [
            'ok' => false,
            'skipped' => false,
            'error' => (string)($out['error'] ?? 'record_failed'),
        ];
    } catch (Throwable $e) {
        return ['ok' => false, 'error' => $e->getMessage()];
    }
}

/**
 * Public verify URL helper.
 */
function epc_bc_bos_verify_url(string $proofUidOrHash): string
{
    $key = rawurlencode(trim($proofUidOrHash));
    return '/epc-blockchain-verify.php?proof=' . $key;
}

/**
 * Lookup latest proof for a tenant document.
 *
 * @return array<string,mixed>|null
 */
function epc_bc_bos_lookup_proof(string $tenantKey, string $recordType, string $recordId): ?array
{
    $tenantKey = strtolower(preg_replace('/[^a-z0-9_]/', '', $tenantKey) ?: '');
    $recordType = strtolower(preg_replace('/[^a-z0-9_\-]/', '', $recordType) ?: '');
    $recordId = substr(trim($recordId), 0, 128);
    if ($tenantKey === '' || $recordType === '' || $recordId === '') {
        return null;
    }
    $pdo = epc_bc_bos_platform_pdo();
    if (!$pdo instanceof PDO) {
        return null;
    }
    epc_bc_bos_ensure_schema($pdo);
    try {
        $st = $pdo->prepare(
            "SELECT p.*, b.batch_uid, b.merkle_root, b.anchor_network AS batch_network
             FROM epc_bc_proofs p
             LEFT JOIN epc_bc_anchor_batches b ON b.id = p.batch_id
             WHERE p.tenant_key = ? AND p.record_type = ? AND p.record_id = ?
             ORDER BY p.id DESC
             LIMIT 1"
        );
        $st->execute([$tenantKey, $recordType, $recordId]);
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    } catch (Throwable $e) {
        return null;
    }
}

/**
 * List recent proofs for a tenant (operator UI).
 *
 * @return list<array<string,mixed>>
 */
function epc_bc_bos_list_proofs(string $tenantKey, array $filters = []): array
{
    $tenantKey = strtolower(preg_replace('/[^a-z0-9_]/', '', $tenantKey) ?: '');
    if ($tenantKey === '') {
        return [];
    }
    $pdo = epc_bc_bos_platform_pdo();
    if (!$pdo instanceof PDO) {
        return [];
    }
    epc_bc_bos_ensure_schema($pdo);

    $limit = isset($filters['limit']) ? max(1, min(200, (int)$filters['limit'])) : 100;
    $where = ['p.tenant_key = ?'];
    $params = [$tenantKey];
    if (!empty($filters['record_type'])) {
        $where[] = 'p.record_type = ?';
        $params[] = strtolower(preg_replace('/[^a-z0-9_\-]/', '', (string)$filters['record_type']) ?: '');
    }
    if (!empty($filters['status'])) {
        $where[] = 'p.status = ?';
        $params[] = strtolower(preg_replace('/[^a-z0-9_]/', '', (string)$filters['status']) ?: '');
    }

    try {
        $sql = 'SELECT p.*, b.batch_uid, b.merkle_root
                FROM epc_bc_proofs p
                LEFT JOIN epc_bc_anchor_batches b ON b.id = p.batch_id
                WHERE ' . implode(' AND ', $where) . '
                ORDER BY p.id DESC
                LIMIT ' . $limit;
        $st = $pdo->prepare($sql);
        $st->execute($params);
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: [];
    } catch (Throwable $e) {
        return [];
    }
}

/**
 * Bootstrap label + verify link for document detail pages.
 */
function epc_bc_bos_proof_badge_html(?array $proof, array $opts = []): string
{
    if (!$proof) {
        if (!empty($opts['show_missing'])) {
            return '<span class="label label-default">No blockchain proof</span>';
        }
        return '';
    }
    $status = strtolower((string)($proof['status'] ?? 'pending'));
    $tone = $status === 'anchored' ? 'success' : ($status === 'pending' ? 'warning' : 'default');
    $uid = (string)($proof['proof_uid'] ?? '');
    $label = $status === 'anchored' ? 'Blockchain anchored' : 'Blockchain proof pending';
    $html = '<span class="label label-' . htmlspecialchars($tone, ENT_QUOTES, 'UTF-8') . '">'
        . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</span>';
    if ($uid !== '') {
        $url = epc_bc_bos_verify_url($uid);
        $html .= ' <a href="' . htmlspecialchars($url, ENT_QUOTES, 'UTF-8')
            . '" target="_blank" rel="noopener" class="btn btn-default btn-xs" style="margin-left:6px">'
            . '<i class="fa fa-external-link"></i> Verify</a>';
        if (!empty($opts['show_uid'])) {
            $html .= ' <small class="text-muted"><code>'
                . htmlspecialchars($uid, ENT_QUOTES, 'UTF-8') . '</code></small>';
        }
    }
    return $html;
}

/**
 * Resolve record type + id from an e-invoice document row.
 *
 * @return array{0:string,1:string}
 */
function epc_bc_bos_einvoice_record_keys(array $doc): array
{
    $cat = (string)($doc['doc_category'] ?? 'tax_invoice');
    $typeCode = (string)($doc['invoice_type_code'] ?? '380');
    $recordType = ($cat === 'tax_credit_note' || $typeCode === '381') ? 'credit_note' : 'invoice';
    $invNo = trim((string)($doc['invoice_number'] ?? ''));
    $recordId = $invNo !== '' ? $invNo : (string)(int)($doc['id'] ?? 0);
    return [$recordType, $recordId];
}

/**
 * Convenience: lookup + badge HTML for current tenant + document.
 */
function epc_bc_bos_document_badge_html(string $recordType, string $recordId, array $opts = []): string
{
    try {
        $siteKey = epc_bc_bos_resolve_site_key($opts);
        if ($siteKey === '') {
            return '';
        }
        if (epc_bc_bos_tenant_mode($siteKey) === 'off') {
            return '';
        }
        $proof = epc_bc_bos_lookup_proof($siteKey, $recordType, $recordId);
        return epc_bc_bos_proof_badge_html($proof, $opts);
    } catch (Throwable $e) {
        return '';
    }
}
