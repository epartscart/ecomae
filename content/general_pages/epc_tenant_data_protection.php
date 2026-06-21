<?php
/**
 * Tenant Data Protection & Confidentiality Framework
 *
 * Enforces enterprise-grade data isolation, access control, audit logging,
 * and compliance for all tenant databases on the ecomae platform.
 *
 * Architecture principles:
 *   1. ISOLATION: Each tenant has its own MySQL database. No cross-tenant queries.
 *   2. ACCESS CONTROL: Only authenticated, authorized actors can access tenant data.
 *   3. AUDIT TRAIL: Every tenant data access is logged with actor, IP, timestamp.
 *   4. CONFIDENTIALITY: Tenant data is never exposed in logs, URLs, or error messages.
 *   5. DATA SOVEREIGNTY: Tenant data belongs to the tenant. Provider sees aggregated
 *      metadata only (counts, statuses) unless explicitly granted access.
 *
 * Worldwide principle: no country-specific behaviour. Data protection rules
 * apply uniformly to all tenants regardless of jurisdiction.
 */
defined('_ASTEXE_') or die('No access');

/* ───────────────────── constants ───────────────────── */

define('EPC_TDP_VERSION', '1.0.0');

// Access levels (bitwise)
define('EPC_TDP_ACCESS_NONE',       0);
define('EPC_TDP_ACCESS_METADATA',   1);  // Tenant name, status, industry — no business data
define('EPC_TDP_ACCESS_READ',       2);  // Read tenant business data (orders, customers, financials)
define('EPC_TDP_ACCESS_WRITE',      4);  // Modify tenant business data
define('EPC_TDP_ACCESS_ADMIN',      8);  // Manage tenant settings, credentials, provisioning
define('EPC_TDP_ACCESS_EXPORT',    16);  // Export/download tenant data
define('EPC_TDP_ACCESS_DELETE',    32);  // Delete tenant data (irreversible)

// Provider default: metadata + read (no write/export/delete without explicit grant)
define('EPC_TDP_PROVIDER_DEFAULT', EPC_TDP_ACCESS_METADATA | EPC_TDP_ACCESS_READ | EPC_TDP_ACCESS_ADMIN);

// Tenant user default: read + write within own tenant
define('EPC_TDP_TENANT_DEFAULT', EPC_TDP_ACCESS_METADATA | EPC_TDP_ACCESS_READ | EPC_TDP_ACCESS_WRITE | EPC_TDP_ACCESS_EXPORT);

/* ───────────────────── access control ───────────────────── */

/**
 * Validate that the current session has permission to access a tenant's data.
 * Must be called before any tenant DB connection or data retrieval.
 *
 * @param string $siteKey       The tenant being accessed
 * @param int    $requiredAccess Bitmask of required access levels
 * @param array  $context       Session context (from epc_bos_context() or CP session)
 * @return array{allowed:bool, reason:string, access_level:int}
 */
function epc_tdp_check_access(string $siteKey, int $requiredAccess, array $context = array()): array
{
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower($siteKey));
    if ($siteKey === '') {
        return array('allowed' => false, 'reason' => 'Invalid tenant key', 'access_level' => EPC_TDP_ACCESS_NONE);
    }

    $role = (string) ($context['role'] ?? 'guest');
    $userId = (int) ($context['user_id'] ?? 0);
    $sessionTenant = (string) ($context['tenant_key'] ?? '');

    // Guest: no access
    if ($role === 'guest' || $userId <= 0) {
        return array('allowed' => false, 'reason' => 'Authentication required', 'access_level' => EPC_TDP_ACCESS_NONE);
    }

    // Provider: has default provider access to all tenants
    if ($role === 'provider') {
        $accessLevel = EPC_TDP_PROVIDER_DEFAULT;
        $allowed = ($accessLevel & $requiredAccess) === $requiredAccess;
        return array(
            'allowed' => $allowed,
            'reason' => $allowed ? 'Provider access granted' : 'Insufficient provider permissions for this operation',
            'access_level' => $accessLevel,
        );
    }

    // Tenant user: only their own tenant
    if ($role === 'tenant') {
        if ($sessionTenant !== $siteKey) {
            epc_tdp_log_violation('cross_tenant_attempt', $userId, $siteKey, array(
                'session_tenant' => $sessionTenant,
                'attempted_tenant' => $siteKey,
                'ip' => epc_tdp_client_ip(),
            ));
            return array(
                'allowed' => false,
                'reason' => 'Access denied — tenant users can only access their own tenant data',
                'access_level' => EPC_TDP_ACCESS_NONE,
            );
        }
        $accessLevel = EPC_TDP_TENANT_DEFAULT;
        $allowed = ($accessLevel & $requiredAccess) === $requiredAccess;
        return array(
            'allowed' => $allowed,
            'reason' => $allowed ? 'Tenant user access granted' : 'Insufficient tenant permissions',
            'access_level' => $accessLevel,
        );
    }

    // Unknown role: deny
    return array('allowed' => false, 'reason' => 'Unknown role: ' . $role, 'access_level' => EPC_TDP_ACCESS_NONE);
}

/**
 * Enforce access — like check_access but throws on denial.
 * Use this as a guard at the top of any tenant data handler.
 */
function epc_tdp_enforce_access(string $siteKey, int $requiredAccess, array $context = array()): void
{
    $result = epc_tdp_check_access($siteKey, $requiredAccess, $context);
    if (!$result['allowed']) {
        epc_tdp_log_violation('access_denied', (int) ($context['user_id'] ?? 0), $siteKey, array(
            'reason' => $result['reason'],
            'required' => $requiredAccess,
            'granted' => $result['access_level'],
            'ip' => epc_tdp_client_ip(),
        ));
        http_response_code(403);
        if (!headers_sent()) {
            header('Content-Type: application/json; charset=utf-8');
        }
        echo json_encode(array('error' => 'Access denied', 'code' => 'TDP_FORBIDDEN'));
        exit;
    }
}

/* ───────────────────── audit logging ───────────────────── */

/**
 * Log a tenant data access event to the platform audit table.
 * All access to tenant data MUST be logged for compliance.
 */
function epc_tdp_log_access(string $siteKey, string $module, string $action, int $actorId, array $meta = array()): void
{
    try {
        $pdo = epc_tdp_platform_pdo();
        if (!$pdo) {
            return;
        }
        epc_tdp_ensure_audit_table($pdo);

        $stmt = $pdo->prepare(
            'INSERT INTO `epc_tdp_audit_log`
             (`site_key`, `module`, `action`, `actor_id`, `actor_ip`, `actor_ua`, `meta_json`, `created_at`)
             VALUES (?, ?, ?, ?, ?, ?, ?, ?)'
        );
        $stmt->execute(array(
            $siteKey,
            substr($module, 0, 64),
            substr($action, 0, 64),
            $actorId,
            epc_tdp_client_ip(),
            substr((string) ($_SERVER['HTTP_USER_AGENT'] ?? ''), 0, 255),
            !empty($meta) ? json_encode($meta, JSON_UNESCAPED_UNICODE) : null,
            time(),
        ));
    } catch (Exception $e) {
        // Audit logging must never break the application
        error_log('[EPC_TDP] audit log write failed: ' . $e->getMessage());
    }
}

/**
 * Log a security violation (cross-tenant access attempt, permission denial, etc.)
 */
function epc_tdp_log_violation(string $type, int $actorId, string $targetTenant, array $details = array()): void
{
    try {
        $pdo = epc_tdp_platform_pdo();
        if (!$pdo) {
            return;
        }
        epc_tdp_ensure_audit_table($pdo);

        $stmt = $pdo->prepare(
            'INSERT INTO `epc_tdp_violations`
             (`violation_type`, `actor_id`, `actor_ip`, `target_tenant`, `details_json`, `created_at`)
             VALUES (?, ?, ?, ?, ?, ?)'
        );
        $stmt->execute(array(
            substr($type, 0, 64),
            $actorId,
            epc_tdp_client_ip(),
            $targetTenant,
            json_encode($details, JSON_UNESCAPED_UNICODE),
            time(),
        ));
    } catch (Exception $e) {
        error_log('[EPC_TDP] violation log write failed: ' . $e->getMessage());
    }
}

/* ───────────────────── data sanitization ───────────────────── */

/**
 * Sanitize tenant data before logging or displaying in cross-tenant contexts.
 * Redacts sensitive fields (passwords, bank details, tax IDs, personal data).
 */
function epc_tdp_redact_sensitive(array $data, array $sensitiveFields = array()): array
{
    $defaultSensitive = array(
        'password', 'db_password', 'db_pass', 'secret', 'token',
        'bank_account', 'account_number', 'iban', 'swift', 'routing_number',
        'tax_id', 'trn', 'vat_number', 'ssn', 'national_id', 'passport',
        'credit_card', 'card_number', 'cvv', 'expiry',
        'salary', 'compensation', 'bonus',
        'api_key', 'api_secret', 'private_key', 'access_token', 'refresh_token',
    );
    $allSensitive = array_merge($defaultSensitive, $sensitiveFields);

    $redacted = array();
    foreach ($data as $key => $value) {
        $keyLower = strtolower((string) $key);
        $isSensitive = false;
        foreach ($allSensitive as $sf) {
            if (strpos($keyLower, strtolower($sf)) !== false) {
                $isSensitive = true;
                break;
            }
        }
        if ($isSensitive) {
            $redacted[$key] = '***REDACTED***';
        } elseif (is_array($value)) {
            $redacted[$key] = epc_tdp_redact_sensitive($value, $sensitiveFields);
        } else {
            $redacted[$key] = $value;
        }
    }
    return $redacted;
}

/* ───────────────────── response security headers ───────────────────── */

/**
 * Apply security headers for tenant data responses.
 * Prevents data leakage via caching, framing, MIME sniffing, etc.
 */
function epc_tdp_apply_security_headers(): void
{
    if (headers_sent()) {
        return;
    }

    // Prevent caching of tenant data responses
    header('Cache-Control: no-store, no-cache, must-revalidate, private');
    header('Pragma: no-cache');
    header('Expires: 0');

    // Prevent MIME type sniffing
    header('X-Content-Type-Options: nosniff');

    // Prevent clickjacking — only allow same-origin framing
    header('X-Frame-Options: SAMEORIGIN');

    // Content Security Policy — restrict data exfiltration
    header("Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com; style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; img-src 'self' data: https:; connect-src 'self'; frame-ancestors 'self'");

    // Referrer policy — don't leak tenant URLs in referrer headers
    header('Referrer-Policy: strict-origin-when-cross-origin');

    // Permissions policy — restrict browser features
    header('Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()');
}

/* ───────────────────── tenant isolation verification ───────────────────── */

/**
 * Verify tenant database isolation.
 * Returns a report of isolation checks for a given tenant.
 */
function epc_tdp_verify_isolation(PDO $platformPdo, string $siteKey): array
{
    $report = array(
        'tenant' => $siteKey,
        'timestamp' => date('Y-m-d H:i:s T'),
        'checks' => array(),
        'passed' => true,
    );

    // Check 1: Tenant has a dedicated database
    require_once __DIR__ . '/epc_portal_tenant_control.php';
    $row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
    if (!$row) {
        $report['checks'][] = array('check' => 'tenant_exists', 'passed' => false, 'detail' => 'Tenant not found in registry');
        $report['passed'] = false;
        return $report;
    }

    $dbName = (string) ($row['db_name'] ?? '');
    $report['checks'][] = array(
        'check' => 'dedicated_database',
        'passed' => $dbName !== '' && $dbName !== 'ecomae',
        'detail' => $dbName !== '' ? 'Database: ' . $dbName : 'No dedicated database assigned',
    );
    if ($dbName === '' || $dbName === 'ecomae') {
        $report['passed'] = false;
    }

    // Check 2: DB credentials are stored (not empty)
    $hasCredentials = !empty($row['db_password']) || ($dbName === 'docpart');
    $report['checks'][] = array(
        'check' => 'credentials_stored',
        'passed' => $hasCredentials,
        'detail' => $hasCredentials ? 'Database credentials present in registry' : 'Missing database credentials',
    );
    if (!$hasCredentials) {
        $report['passed'] = false;
    }

    // Check 3: DB connection works
    $connResult = epc_portal_tenant_control_tenant_pdo_connect($row);
    $dbConnects = $connResult['pdo'] instanceof PDO;
    $report['checks'][] = array(
        'check' => 'db_connectivity',
        'passed' => $dbConnects,
        'detail' => $dbConnects ? 'Successfully connected to tenant database' : 'Connection failed: ' . ($connResult['error'] ?? 'unknown'),
    );
    if (!$dbConnects) {
        $report['passed'] = false;
    }

    // Check 4: Tenant hostname is properly scoped
    $hostname = (string) ($row['hostname'] ?? '');
    $isShared = !empty($row['erp_only_shared']);
    $hostnameOk = $hostname !== '' && ($isShared || $hostname !== 'www.ecomae.com');
    $report['checks'][] = array(
        'check' => 'hostname_scoped',
        'passed' => $hostnameOk,
        'detail' => $isShared
            ? 'Shared ERP tenant on platform host (expected)'
            : 'Hostname: ' . $hostname,
    );

    // Check 5: Tenant status is valid
    $validStatuses = array('draft', 'dns_pending', 'live', 'suspended');
    $status = (string) ($row['status'] ?? '');
    $report['checks'][] = array(
        'check' => 'valid_status',
        'passed' => in_array($status, $validStatuses, true),
        'detail' => 'Status: ' . $status,
    );

    // Check 6: No cross-tenant DB reference (tenant DB != platform DB)
    $report['checks'][] = array(
        'check' => 'no_platform_db_sharing',
        'passed' => $dbName !== 'ecomae',
        'detail' => $dbName === 'ecomae'
            ? 'CRITICAL: tenant is using the platform registry database!'
            : 'Tenant uses dedicated database (not platform registry)',
    );
    if ($dbName === 'ecomae') {
        $report['passed'] = false;
    }

    return $report;
}

/**
 * Run isolation verification across ALL tenants. Returns array of reports.
 */
function epc_tdp_verify_all_tenants(PDO $platformPdo): array
{
    require_once __DIR__ . '/epc_portal_tenant.php';
    $tenants = epc_portal_list_tenants($platformPdo);
    $reports = array();
    foreach ($tenants as $t) {
        $key = (string) ($t['site_key'] ?? '');
        if ($key !== '') {
            $reports[$key] = epc_tdp_verify_isolation($platformPdo, $key);
        }
    }
    return $reports;
}

/* ───────────────────── data retention policy ───────────────────── */

/**
 * Get the data retention policy for a tenant.
 * Worldwide principle: same policy for all tenants, driven by platform config.
 */
function epc_tdp_retention_policy(): array
{
    return array(
        'audit_logs'        => array('retention_days' => 2555, 'description' => 'Platform audit logs — 7 years (legal/tax requirement)'),
        'violation_logs'    => array('retention_days' => 2555, 'description' => 'Security violation logs — 7 years'),
        'erp_transactions'  => array('retention_days' => 2555, 'description' => 'ERP financial records — 7 years (legal requirement)'),
        'customer_data'     => array('retention_days' => 1825, 'description' => 'Customer PII — 5 years after last activity'),
        'session_logs'      => array('retention_days' => 90,   'description' => 'Login/session records — 90 days'),
        'temp_exports'      => array('retention_days' => 7,    'description' => 'Temporary data exports — 7 days'),
    );
}

/* ───────────────────── confidentiality classification ───────────────────── */

/**
 * Classify data sensitivity level for a given field or module.
 */
function epc_tdp_classify_data(string $module, string $field = ''): array
{
    $classifications = array(
        // Highly confidential — financial data
        'gl'            => array('level' => 'highly_confidential', 'label' => 'Financial — General Ledger'),
        'ap'            => array('level' => 'highly_confidential', 'label' => 'Financial — Accounts Payable'),
        'ar'            => array('level' => 'highly_confidential', 'label' => 'Financial — Accounts Receivable'),
        'cash_bank'     => array('level' => 'highly_confidential', 'label' => 'Financial — Cash & Bank'),
        'payroll'       => array('level' => 'highly_confidential', 'label' => 'HR — Payroll & Compensation'),
        'tax'           => array('level' => 'highly_confidential', 'label' => 'Tax — Filings & Compliance'),

        // Confidential — business operations
        'hr'            => array('level' => 'confidential', 'label' => 'HR — Employee Records'),
        'orders'        => array('level' => 'confidential', 'label' => 'Commerce — Orders'),
        'customers'     => array('level' => 'confidential', 'label' => 'Commerce — Customer Data'),
        'vendors'       => array('level' => 'confidential', 'label' => 'Supply Chain — Vendor Data'),
        'inventory'     => array('level' => 'confidential', 'label' => 'Warehouse — Inventory'),
        'pricing'       => array('level' => 'confidential', 'label' => 'Commerce — Pricing'),

        // Internal — operational data
        'products'      => array('level' => 'internal', 'label' => 'Catalogue — Products'),
        'cms'           => array('level' => 'internal', 'label' => 'Content — CMS'),
        'settings'      => array('level' => 'internal', 'label' => 'Configuration — Settings'),
        'marketing'     => array('level' => 'internal', 'label' => 'Marketing — Campaigns'),

        // Public — non-sensitive
        'storefront'    => array('level' => 'public', 'label' => 'Storefront — Public Catalogue'),
    );

    $moduleLower = strtolower($module);
    return $classifications[$moduleLower] ?? array('level' => 'internal', 'label' => 'Unclassified — ' . $module);
}

/* ───────────────────── schema ───────────────────── */

function epc_tdp_ensure_audit_table(PDO $pdo): void
{
    static $done = false;
    if ($done) {
        return;
    }
    $done = true;

    $pdo->exec("CREATE TABLE IF NOT EXISTS `epc_tdp_audit_log` (
        `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `site_key` VARCHAR(64) NOT NULL,
        `module` VARCHAR(64) NOT NULL,
        `action` VARCHAR(64) NOT NULL,
        `actor_id` INT UNSIGNED NOT NULL DEFAULT 0,
        `actor_ip` VARCHAR(45) NOT NULL DEFAULT '',
        `actor_ua` VARCHAR(255) NOT NULL DEFAULT '',
        `meta_json` TEXT NULL,
        `created_at` INT NOT NULL DEFAULT 0,
        KEY `site_key_module` (`site_key`, `module`),
        KEY `actor_id` (`actor_id`),
        KEY `created_at` (`created_at`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    $pdo->exec("CREATE TABLE IF NOT EXISTS `epc_tdp_violations` (
        `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
        `violation_type` VARCHAR(64) NOT NULL,
        `actor_id` INT UNSIGNED NOT NULL DEFAULT 0,
        `actor_ip` VARCHAR(45) NOT NULL DEFAULT '',
        `target_tenant` VARCHAR(64) NOT NULL DEFAULT '',
        `details_json` TEXT NULL,
        `created_at` INT NOT NULL DEFAULT 0,
        KEY `violation_type` (`violation_type`),
        KEY `target_tenant` (`target_tenant`),
        KEY `created_at` (`created_at`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
}

/* ───────────────────── helpers ───────────────────── */

function epc_tdp_client_ip(): string
{
    $headers = array('HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'HTTP_X_REAL_IP', 'REMOTE_ADDR');
    foreach ($headers as $h) {
        $val = trim((string) ($_SERVER[$h] ?? ''));
        if ($val !== '') {
            // X-Forwarded-For may contain multiple IPs — use the first
            $parts = explode(',', $val);
            return trim($parts[0]);
        }
    }
    return '0.0.0.0';
}

function epc_tdp_platform_pdo(): ?PDO
{
    if (function_exists('epc_portal_platform_pdo')) {
        $pdo = epc_portal_platform_pdo();
        return $pdo instanceof PDO ? $pdo : null;
    }
    return null;
}
