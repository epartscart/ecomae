<?php
/**
 * Platform background job queue (1000+ tenant scale foundation).
 *
 * Heavy tenant work (price sync, catalog rebuild, fleet scans, onboarding
 * post-steps) must not run inline in web requests. This queue stores jobs in
 * the platform MySQL DB and is drained by epc-platform-jobs-cron.php.
 *
 * Pattern mirrors APAI background jobs but is platform-wide (not shop-scoped).
 */

function epc_platform_jobs_pdo(): ?PDO
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
        if (!function_exists('epc_portal_platform_pdo')) {
            $failed = true;
            return null;
        }
        $pdo = epc_portal_platform_pdo();
        return $pdo instanceof PDO ? $pdo : null;
    } catch (Throwable $e) {
        $failed = true;
        return null;
    }
}

function epc_platform_jobs_ensure_schema(?PDO $pdo = null): void
{
    $pdo = $pdo instanceof PDO ? $pdo : epc_platform_jobs_pdo();
    if (!$pdo instanceof PDO) {
        return;
    }
    static $ready = false;
    if ($ready) {
        return;
    }
    try {
        $pdo->exec(
            "CREATE TABLE IF NOT EXISTS epc_platform_jobs (
                id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                job_type VARCHAR(64) NOT NULL,
                tenant_key VARCHAR(64) NOT NULL DEFAULT '',
                payload_json LONGTEXT NULL,
                status VARCHAR(24) NOT NULL DEFAULT 'queued',
                priority INT NOT NULL DEFAULT 100,
                attempts INT NOT NULL DEFAULT 0,
                max_attempts INT NOT NULL DEFAULT 5,
                available_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                locked_at DATETIME NULL,
                locked_by VARCHAR(64) NULL,
                started_at DATETIME NULL,
                finished_at DATETIME NULL,
                last_error TEXT NULL,
                result_json LONGTEXT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (id),
                KEY idx_claim (status, available_at, priority, id),
                KEY idx_tenant_type (tenant_key, job_type, status),
                KEY idx_type_status (job_type, status, updated_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
        );
        $ready = true;
    } catch (Throwable $e) {
        // best-effort
    }
}

/**
 * Enqueue a platform job. Returns job id or 0 on failure.
 *
 * @param array<string,mixed> $payload
 */
function epc_platform_jobs_enqueue(string $jobType, string $tenantKey = '', array $payload = [], array $opts = []): int
{
    $pdo = epc_platform_jobs_pdo();
    if (!$pdo instanceof PDO) {
        return 0;
    }
    epc_platform_jobs_ensure_schema($pdo);

    $jobType = strtolower(trim($jobType));
    $tenantKey = strtolower(trim($tenantKey));
    if ($jobType === '') {
        return 0;
    }

    $priority = isset($opts['priority']) ? (int)$opts['priority'] : 100;
    $maxAttempts = isset($opts['max_attempts']) ? max(1, (int)$opts['max_attempts']) : 5;
    $delaySec = isset($opts['delay_sec']) ? max(0, (int)$opts['delay_sec']) : 0;
    $dedupe = !empty($opts['dedupe']);

    if ($dedupe) {
        try {
            $st = $pdo->prepare(
                "SELECT id FROM epc_platform_jobs
                 WHERE job_type = ? AND tenant_key = ? AND status IN ('queued','running')
                 ORDER BY id DESC LIMIT 1"
            );
            $st->execute([$jobType, $tenantKey]);
            $existing = (int)$st->fetchColumn();
            if ($existing > 0) {
                return $existing;
            }
        } catch (Throwable $e) {
            // continue to insert
        }
    }

    try {
        $st = $pdo->prepare(
            "INSERT INTO epc_platform_jobs
                (job_type, tenant_key, payload_json, status, priority, max_attempts, available_at)
             VALUES
                (?, ?, ?, 'queued', ?, ?, DATE_ADD(NOW(), INTERVAL ? SECOND))"
        );
        $st->execute([
            $jobType,
            $tenantKey,
            json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
            $priority,
            $maxAttempts,
            $delaySec,
        ]);
        return (int)$pdo->lastInsertId();
    } catch (Throwable $e) {
        return 0;
    }
}

/**
 * Claim next available jobs for a worker.
 *
 * @return list<array<string,mixed>>
 */
function epc_platform_jobs_claim(string $workerId, int $limit = 5): array
{
    $pdo = epc_platform_jobs_pdo();
    if (!$pdo instanceof PDO) {
        return [];
    }
    epc_platform_jobs_ensure_schema($pdo);

    $workerId = substr(preg_replace('/[^a-zA-Z0-9_\-\.]/', '', $workerId) ?: 'worker', 0, 64);
    $limit = max(1, min(50, $limit));
    $claimed = [];

    try {
        $pdo->beginTransaction();
        $st = $pdo->prepare(
            "SELECT id FROM epc_platform_jobs
             WHERE status = 'queued' AND available_at <= NOW()
             ORDER BY priority ASC, id ASC
             LIMIT {$limit}
             FOR UPDATE"
        );
        $st->execute();
        $ids = array_map('intval', $st->fetchAll(PDO::FETCH_COLUMN) ?: []);
        if (!$ids) {
            $pdo->commit();
            return [];
        }

        $upd = $pdo->prepare(
            "UPDATE epc_platform_jobs
             SET status = 'running',
                 attempts = attempts + 1,
                 locked_at = NOW(),
                 locked_by = ?,
                 started_at = NOW(),
                 updated_at = NOW()
             WHERE id = ? AND status = 'queued'"
        );
        foreach ($ids as $id) {
            $upd->execute([$workerId, $id]);
        }
        $pdo->commit();

        $in = implode(',', array_fill(0, count($ids), '?'));
        $fetch = $pdo->prepare(
            "SELECT * FROM epc_platform_jobs WHERE id IN ({$in}) AND status = 'running' AND locked_by = ?"
        );
        $params = $ids;
        $params[] = $workerId;
        $fetch->execute($params);
        $claimed = $fetch->fetchAll(PDO::FETCH_ASSOC) ?: [];
    } catch (Throwable $e) {
        if ($pdo->inTransaction()) {
            $pdo->rollBack();
        }
        return [];
    }

    return $claimed;
}

function epc_platform_jobs_complete(int $jobId, array $result = []): void
{
    $pdo = epc_platform_jobs_pdo();
    if (!$pdo instanceof PDO || $jobId <= 0) {
        return;
    }
    try {
        $st = $pdo->prepare(
            "UPDATE epc_platform_jobs
             SET status = 'done',
                 finished_at = NOW(),
                 result_json = ?,
                 last_error = NULL,
                 locked_at = NULL,
                 updated_at = NOW()
             WHERE id = ?"
        );
        $st->execute([
            json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
            $jobId,
        ]);
    } catch (Throwable $e) {
        // best-effort
    }
}

function epc_platform_jobs_fail(int $jobId, string $error, bool $retry = true): void
{
    $pdo = epc_platform_jobs_pdo();
    if (!$pdo instanceof PDO || $jobId <= 0) {
        return;
    }
    try {
        $st = $pdo->prepare('SELECT attempts, max_attempts FROM epc_platform_jobs WHERE id = ? LIMIT 1');
        $st->execute([$jobId]);
        $row = $st->fetch(PDO::FETCH_ASSOC) ?: [];
        $attempts = (int)($row['attempts'] ?? 0);
        $max = (int)($row['max_attempts'] ?? 5);
        $canRetry = $retry && $attempts < $max;
        if ($canRetry) {
            // Exponential backoff: 30s, 60s, 120s, ...
            $delay = (int)min(3600, 30 * (2 ** max(0, $attempts - 1)));
            $st2 = $pdo->prepare(
                "UPDATE epc_platform_jobs
                 SET status = 'queued',
                     available_at = DATE_ADD(NOW(), INTERVAL ? SECOND),
                     last_error = ?,
                     locked_at = NULL,
                     locked_by = NULL,
                     updated_at = NOW()
                 WHERE id = ?"
            );
            $st2->execute([$delay, mb_substr($error, 0, 2000), $jobId]);
        } else {
            $st2 = $pdo->prepare(
                "UPDATE epc_platform_jobs
                 SET status = 'failed',
                     finished_at = NOW(),
                     last_error = ?,
                     locked_at = NULL,
                     locked_by = NULL,
                     updated_at = NOW()
                 WHERE id = ?"
            );
            $st2->execute([mb_substr($error, 0, 2000), $jobId]);
        }
    } catch (Throwable $e) {
        // best-effort
    }
}

/**
 * Built-in handlers for common scale jobs.
 * Custom handlers can be registered via epc_platform_jobs_register_handler().
 *
 * @return array{ok:bool,result?:array,error?:string}
 */
function epc_platform_jobs_dispatch(array $job): array
{
    $type = strtolower(trim((string)($job['job_type'] ?? '')));
    $tenantKey = strtolower(trim((string)($job['tenant_key'] ?? '')));
    $payload = [];
    if (!empty($job['payload_json'])) {
        $decoded = json_decode((string)$job['payload_json'], true);
        if (is_array($decoded)) {
            $payload = $decoded;
        }
    }

    $handlers = $GLOBALS['EPC_PLATFORM_JOB_HANDLERS'] ?? [];
    if (isset($handlers[$type]) && is_callable($handlers[$type])) {
        try {
            $out = $handlers[$type]($tenantKey, $payload, $job);
            if (is_array($out) && array_key_exists('ok', $out)) {
                return $out;
            }
            return ['ok' => true, 'result' => is_array($out) ? $out : ['value' => $out]];
        } catch (Throwable $e) {
            return ['ok' => false, 'error' => $e->getMessage()];
        }
    }

    // Built-ins
    if ($type === 'tenant_health_ping') {
        return epc_platform_jobs_handle_tenant_health_ping($tenantKey, $payload);
    }
    if ($type === 'tenant_warmup_pdo') {
        return epc_platform_jobs_handle_tenant_warmup($tenantKey, $payload);
    }
    if ($type === 'blockchain_anchor_batch') {
        require_once __DIR__ . '/epc_blockchain_bos.php';
        return epc_bc_bos_job_anchor_batch($tenantKey, $payload, $job);
    }
    if ($type === 'noop') {
        return ['ok' => true, 'result' => ['echo' => $payload]];
    }

    return ['ok' => false, 'error' => 'Unknown job_type: ' . $type];
}

function epc_platform_jobs_register_handler(string $jobType, callable $fn): void
{
    $jobType = strtolower(trim($jobType));
    if ($jobType === '') {
        return;
    }
    if (!isset($GLOBALS['EPC_PLATFORM_JOB_HANDLERS']) || !is_array($GLOBALS['EPC_PLATFORM_JOB_HANDLERS'])) {
        $GLOBALS['EPC_PLATFORM_JOB_HANDLERS'] = [];
    }
    $GLOBALS['EPC_PLATFORM_JOB_HANDLERS'][$jobType] = $fn;
}

function epc_platform_jobs_handle_tenant_health_ping(string $tenantKey, array $payload): array
{
    if ($tenantKey === '') {
        return ['ok' => false, 'error' => 'tenant_key required'];
    }
    require_once __DIR__ . '/epc_portal_tenant_intro.php';
    $platform = epc_platform_jobs_pdo();
    if (!$platform instanceof PDO) {
        return ['ok' => false, 'error' => 'Platform DB unavailable'];
    }
    $row = epc_portal_tenant_get($platform, $tenantKey);
    if (!$row) {
        return ['ok' => false, 'error' => 'Tenant not found'];
    }
    require_once __DIR__ . '/epc_tenant_pdo.php';
    [$pdo, $err] = epc_tenant_pdo_from_row($row, ['timeout' => 5]);
    if (!$pdo instanceof PDO) {
        return ['ok' => false, 'error' => $err !== '' ? $err : 'DB connect failed'];
    }
    try {
        $pdo->query('SELECT 1');
        return [
            'ok' => true,
            'result' => [
                'tenant_key' => $tenantKey,
                'dedicated_db' => epc_tenant_row_uses_dedicated_db($row) ? 1 : 0,
                'db_name' => (string)($row['db_name'] ?? ''),
                'ping' => 'ok',
            ],
        ];
    } catch (Throwable $e) {
        return ['ok' => false, 'error' => $e->getMessage()];
    }
}

function epc_platform_jobs_handle_tenant_warmup(string $tenantKey, array $payload): array
{
    // Warm the process-local PDO pool + touch core tables so first user hit is faster.
    $ping = epc_platform_jobs_handle_tenant_health_ping($tenantKey, $payload);
    if (empty($ping['ok'])) {
        return $ping;
    }
    require_once __DIR__ . '/epc_portal_tenant_intro.php';
    $platform = epc_platform_jobs_pdo();
    $row = ($platform instanceof PDO) ? epc_portal_tenant_get($platform, $tenantKey) : null;
    require_once __DIR__ . '/epc_tenant_pdo.php';
    [$pdo] = epc_tenant_pdo_from_row($row ?: []);
    if ($pdo instanceof PDO) {
        try {
            // Soft touches — ignore missing tables.
            @$pdo->query('SELECT 1 FROM shop_stat LIMIT 1');
            @$pdo->query('SELECT 1 FROM `users` LIMIT 1');
        } catch (Throwable $e) {
            // ignore
        }
    }
    return ['ok' => true, 'result' => array_merge($ping['result'] ?? [], ['warmup' => true])];
}

/**
 * Drain up to $limit jobs. Returns summary for cron logs.
 */
function epc_platform_jobs_run_batch(int $limit = 10, string $workerId = ''): array
{
    if ($workerId === '') {
        $workerId = 'cron-' . substr(md5(gethostname() . '-' . getmypid()), 0, 10);
    }
    $jobs = epc_platform_jobs_claim($workerId, $limit);
    $done = 0;
    $failed = 0;
    $errors = [];
    foreach ($jobs as $job) {
        $id = (int)($job['id'] ?? 0);
        $out = epc_platform_jobs_dispatch($job);
        if (!empty($out['ok'])) {
            epc_platform_jobs_complete($id, $out['result'] ?? []);
            $done++;
        } else {
            $err = (string)($out['error'] ?? 'Job failed');
            epc_platform_jobs_fail($id, $err, true);
            $failed++;
            $errors[] = '#' . $id . ' ' . $err;
        }
    }
    return [
        'claimed' => count($jobs),
        'done' => $done,
        'failed' => $failed,
        'errors' => $errors,
        'worker' => $workerId,
    ];
}
