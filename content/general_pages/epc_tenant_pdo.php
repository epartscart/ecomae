<?php
/**
 * Tenant PDO connection manager (1000+ tenant scale foundation).
 *
 * Problem: fleet scripts and cron workers often open a fresh PDO per tenant.
 * At 1000+ tenants that exhausts MySQL max_connections and burns handshake cost.
 *
 * This helper keeps a process-local pool keyed by host|db|user and reuses
 * live connections. Dedicated-DB tenants and shared-docpart tenants both use it.
 *
 * Soft cap: EPC_TENANT_PDO_POOL_MAX (default 24). When full, the oldest idle
 * handle is dropped before opening a new one.
 */

if (!defined('EPC_TENANT_PDO_POOL_MAX')) {
    define('EPC_TENANT_PDO_POOL_MAX', 24);
}

/**
 * @return array{0:?PDO,1:string} [pdo, error]
 */
function epc_tenant_pdo(string $host, string $db, string $user, string $pass, array $opts = []): array
{
    $host = trim($host);
    $db = trim($db);
    $user = trim($user);
    if ($host === '' || $db === '' || $user === '') {
        return [null, 'Missing DB connection fields'];
    }

    static $pool = []; // key => ['pdo'=>PDO,'touched'=>float]
    $key = strtolower($host) . '|' . strtolower($db) . '|' . $user;

    if (isset($pool[$key]['pdo']) && $pool[$key]['pdo'] instanceof PDO) {
        try {
            $pool[$key]['pdo']->query('SELECT 1');
            $pool[$key]['touched'] = microtime(true);
            return [$pool[$key]['pdo'], ''];
        } catch (Throwable $e) {
            unset($pool[$key]);
        }
    }

    if (count($pool) >= (int)EPC_TENANT_PDO_POOL_MAX) {
        uasort($pool, static function ($a, $b) {
            return ($a['touched'] <=> $b['touched']);
        });
        $dropKey = array_key_first($pool);
        if (is_string($dropKey) && $dropKey !== '') {
            unset($pool[$dropKey]);
        }
    }

    $timeout = isset($opts['timeout']) ? max(1, (int)$opts['timeout']) : 8;
    try {
        $dsn = 'mysql:host=' . $host . ';dbname=' . $db . ';charset=utf8mb4';
        $pdo = new PDO($dsn, $user, (string)$pass, [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_TIMEOUT => $timeout,
        ]);
        $pool[$key] = ['pdo' => $pdo, 'touched' => microtime(true)];
        return [$pdo, ''];
    } catch (Throwable $e) {
        return [null, $e->getMessage()];
    }
}

/**
 * Resolve MySQL host for a tenant registry row (db_host optional).
 */
function epc_tenant_pdo_resolve_host(array $row): string
{
    $host = trim((string)($row['db_host'] ?? ''));
    if ($host !== '') {
        return $host;
    }
    try {
        if (!empty($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config']) && !empty($GLOBALS['DP_Config']->host)) {
            return (string)$GLOBALS['DP_Config']->host;
        }
        $docroot = (string)($_SERVER['DOCUMENT_ROOT'] ?? '');
        if ($docroot !== '' && is_file($docroot . '/config.php')) {
            require_once $docroot . '/config.php';
            if (class_exists('DP_Config', false)) {
                $cfg = new DP_Config();
                if (!empty($cfg->host)) {
                    return (string)$cfg->host;
                }
            }
        }
    } catch (Throwable $e) {
        // fall through
    }
    return '127.0.0.1';
}

/**
 * Open (or reuse) PDO from a tenant registry row.
 * Prefers dedicated DB credentials when dedicated_db=1 or db_name != docpart.
 *
 * @return array{0:?PDO,1:string}
 */
function epc_tenant_pdo_from_row(array $row, array $opts = []): array
{
    $host = epc_tenant_pdo_resolve_host($row);
    $db = trim((string)($row['db_name'] ?? ($row['db'] ?? '')));
    $user = trim((string)($row['db_user'] ?? ($row['user'] ?? '')));
    $pass = (string)($row['db_password'] ?? ($row['db_pass'] ?? ($row['password'] ?? '')));

    // Shared commerce fallback when registry has docpart but empty password.
    if ($db === 'docpart' && $pass === '' && function_exists('epc_portal_resolve_tenant_db_credentials')) {
        $creds = epc_portal_resolve_tenant_db_credentials();
        $user = (string)($creds['user'] ?? $user);
        $pass = (string)($creds['password'] ?? '');
        if ($db === '' || $db === 'docpart') {
            $db = (string)($creds['db'] ?? 'docpart');
        }
    }

    if ($host === '' || $db === '' || $user === '') {
        return [null, 'Tenant DB credentials incomplete'];
    }
    return epc_tenant_pdo($host, $db, $user, $pass, $opts);
}

function epc_tenant_row_uses_dedicated_db(array $row): bool
{
    if ((int)($row['dedicated_db'] ?? 0) === 1) {
        return true;
    }
    $policy = strtolower(trim((string)($row['scale_policy'] ?? '')));
    if ($policy === 'dedicated_mysql') {
        return true;
    }
    if (!empty($row['erp_only_shared'])) {
        return true;
    }
    $db = strtolower(trim((string)($row['db_name'] ?? ($row['db'] ?? ''))));
    if ($db !== '' && $db !== 'docpart') {
        return true;
    }
    return false;
}

function epc_tenant_pdo_pool_stats(): array
{
    return [
        'max' => (int)EPC_TENANT_PDO_POOL_MAX,
        'note' => 'Process-local pool; size is not globally exposed across PHP-FPM workers.',
    ];
}
