<?php
/**
 * BOS AJAX handler — login, tenant switch, module load.
 */
defined('_ASTEXE_') or define('_ASTEXE_', 1);
defined('EPC_BOS_ENTRY') or define('EPC_BOS_ENTRY', true);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bos_unified.php';

header('Content-Type: application/json; charset=utf-8');

$action = isset($_POST['bos_action']) ? trim($_POST['bos_action']) : (isset($_GET['bos_action']) ? trim($_GET['bos_action']) : '');

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['email']) && isset($_POST['password']) && $action === '') {
    $action = 'login';
}

$response = array('ok' => false, 'error' => 'Unknown action');

switch ($action) {
    case 'login':
        $response = epc_bos_ajax_login();
        break;

    case 'switch_tenant':
        $response = epc_bos_ajax_switch_tenant();
        break;

    case 'tenant_list':
        $response = epc_bos_ajax_tenant_list();
        break;

    case 'tenant_info':
        $response = epc_bos_ajax_tenant_info();
        break;

    case 'fleet_health':
        $response = epc_bos_ajax_fleet_health();
        break;

    case 'tenant_compliance':
        $response = epc_bos_ajax_tenant_compliance();
        break;

    case 'system_health':
        $response = epc_bos_ajax_system_health();
        break;

    case 'isolation_audit':
        $response = epc_bos_ajax_isolation_audit();
        break;

    case 'mfa_policy':
        $response = epc_bos_ajax_mfa_policy();
        break;

    case 'mfa_stats':
        $response = epc_bos_ajax_mfa_stats();
        break;

    case 'webhooks':
        $response = epc_bos_ajax_webhooks();
        break;

    case 'events':
        $response = epc_bos_ajax_events();
        break;

    case 'design_tokens':
        $response = epc_bos_ajax_design_tokens();
        break;

    case 'readiness_score':
        $response = epc_bos_ajax_readiness_score();
        break;

    case 'notifications':
        $response = epc_bos_ajax_notifications();
        break;

    case 'db_migrations':
        $response = epc_bos_ajax_db_migrations();
        break;

    case 'cp_role_home':
        $response = epc_bos_ajax_cp_role_home();
        break;

    case 'credit_limit':
        $response = epc_bos_ajax_credit_limit();
        break;

    case 'order_erp_pipeline':
        $response = epc_bos_ajax_order_erp_pipeline();
        break;

    case 'po_approval':
        $response = epc_bos_ajax_po_approval();
        break;

    case 'rest_api_v2':
        $response = epc_bos_ajax_rest_api_v2();
        break;

    case 'fulfillment_queue':
        $response = epc_bos_ajax_fulfillment_queue();
        break;

    case 'bi_metrics':
        $response = epc_bos_ajax_bi_metrics();
        break;

    case 'ai_classification':
        $response = epc_bos_ajax_ai_classification();
        break;

    case 'tenant_config':
        $response = epc_bos_ajax_tenant_config();
        break;

    case 'workflow_builder':
        $response = epc_bos_ajax_workflow_builder();
        break;

    case 'inventory_forecast':
        $response = epc_bos_ajax_inventory_forecast();
        break;

    case 'multi_currency_gl':
        $response = epc_bos_ajax_multi_currency_gl();
        break;

    case 'sso_saml':
        $response = epc_bos_ajax_sso_saml();
        break;

    case 'wps_payroll':
        $response = epc_bos_ajax_wps_payroll();
        break;

    default:
        $response = array('ok' => false, 'error' => 'Invalid action');
}

echo json_encode($response, JSON_UNESCAPED_UNICODE);
exit;

/* ───────────────────── login ───────────────────── */

function epc_bos_ajax_login(): array
{
    $email = trim((string) ($_POST['email'] ?? ''));
    $password = (string) ($_POST['password'] ?? '');

    if ($email === '' || $password === '') {
        return array('ok' => false, 'error' => 'Email and password required');
    }

    $secretSuccession = '';
    $mainPdo = null;
    try {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
        $cfg = new \DP_Config();
        $secretSuccession = (string) ($cfg->secret_succession ?? '');
        $mainPdo = new PDO(
            'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
            $cfg->user,
            $cfg->password,
            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
        );
    } catch (Exception $e) {
        $mainPdo = null;
    }

    $platformPdo = null;
    try {
        $platformPdo = epc_portal_platform_operator_pdo();
    } catch (Exception $e) {
        $platformPdo = null;
    }

    if (!$mainPdo && !$platformPdo) {
        return array('ok' => false, 'error' => 'Platform database unavailable');
    }

    $userRow = null;
    $authPdo = null;
    $tables = array('users', 'admin', 'epc_cp_users');
    $pdoSources = array_filter(array($mainPdo, $platformPdo));

    foreach ($pdoSources as $tryPdo) {
        foreach ($tables as $table) {
            try {
                $st = $tryPdo->prepare("SELECT * FROM `{$table}` WHERE `email` = ? LIMIT 1");
                $st->execute(array($email));
                $row = $st->fetch(PDO::FETCH_ASSOC);
                if ($row) {
                    $userRow = $row;
                    $userRow['_table'] = $table;
                    $authPdo = $tryPdo;
                    break 2;
                }
            } catch (Exception $e) {
                continue;
            }
        }
    }

    if (!$userRow) {
        return array('ok' => false, 'error' => 'Invalid credentials');
    }

    $storedPass = (string) ($userRow['password'] ?? $userRow['pass'] ?? '');
    $passOk = false;

    if ($storedPass !== '') {
        if (password_verify($password, $storedPass)) {
            $passOk = true;
        } elseif ($secretSuccession !== '' && md5($password . $secretSuccession) === $storedPass) {
            $passOk = true;
        } elseif (md5($password) === $storedPass) {
            $passOk = true;
        }
    }

    if (!$passOk) {
        return array('ok' => false, 'error' => 'Invalid credentials');
    }

    // Transparent password upgrade: MD5 → bcrypt on successful login
    $upgradeFile = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_password_upgrade.php';
    if (is_file($upgradeFile)) {
        require_once $upgradeFile;
        $userId_ = (int) ($userRow['id'] ?? $userRow['ID'] ?? $userRow['user_id'] ?? 0);
        if ($userId_ > 0 && $authPdo && epc_password_is_legacy_md5($storedPass)) {
            epc_password_upgrade_if_needed($authPdo, $userId_, $password, $storedPass);
        }
    }

    $userId = (int) ($userRow['id'] ?? $userRow['ID'] ?? $userRow['user_id'] ?? 0);
    $role = 'provider';

    $tenantSiteKey = '';
    if (isset($userRow['site_key']) && $userRow['site_key'] !== '') {
        $role = 'tenant';
        $tenantSiteKey = $userRow['site_key'];
    }

    session_regenerate_id(true);

    epc_bos_set_context(array(
        'role'       => $role,
        'user_id'    => $userId,
        'email'      => $email,
        'tenant_key' => $tenantSiteKey,
    ));

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
    if (function_exists('epc_boc_audit_log')) {
        try {
            $auditPdo = $platformPdo ?: $mainPdo;
            if ($auditPdo) {
                epc_boc_audit_log($auditPdo, $userId, 'bos', 'login', '', array('role' => $role), $email);
            }
        } catch (Exception $e) {
            // audit log failure should not block login
        }
    }

    $redirect = '/bos/';
    if ($tenantSiteKey !== '') {
        $redirect = '/bos/?t=' . urlencode($tenantSiteKey);
    }

    return array('ok' => true, 'redirect' => $redirect, 'role' => $role);
}

/* ───────────────────── switch tenant ───────────────────── */

function epc_bos_ajax_switch_tenant(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] === 'guest') {
        return array('ok' => false, 'error' => 'Not authenticated');
    }

    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? ''))));
    if ($siteKey === '') {
        return array('ok' => false, 'error' => 'site_key required');
    }

    if ($ctx['role'] === 'tenant' && $ctx['tenant_key'] !== $siteKey) {
        return array('ok' => false, 'error' => 'Access denied — tenant users can only access their own tenant');
    }

    try {
        $platformPdo = epc_portal_platform_operator_pdo();
        if (!$platformPdo) {
            return array('ok' => false, 'error' => 'Platform database unavailable');
        }
    } catch (Exception $e) {
        return array('ok' => false, 'error' => 'Platform database unavailable');
    }

    $conn = epc_bos_tenant_connect($platformPdo, $siteKey);
    if ($conn['pdo'] === null) {
        return array('ok' => false, 'error' => $conn['error'] ?? 'Cannot connect to tenant');
    }

    epc_bos_set_context(array_merge($ctx, array('tenant_key' => $siteKey)));

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
    if (function_exists('epc_boc_audit_log')) {
        try {
            epc_boc_audit_log($platformPdo, $ctx['user_id'], 'bos', 'switch_tenant', $siteKey, array(), $ctx['email']);
        } catch (Exception $e) {
            // non-critical
        }
    }

    return array('ok' => true, 'tenant' => $siteKey, 'redirect' => '/bos/?t=' . urlencode($siteKey));
}

/* ───────────────────── tenant list ───────────────────── */

function epc_bos_ajax_tenant_list(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }
    return array('ok' => true, 'tenants' => epc_bos_tenant_list($platformPdo));
}

/* ───────────────────── tenant info ───────────────────── */

function epc_bos_ajax_tenant_info(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] === 'guest') {
        return array('ok' => false, 'error' => 'Not authenticated');
    }
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $_GET['site_key'] ?? ''))));
    if ($siteKey === '') {
        return array('ok' => false, 'error' => 'site_key required');
    }
    if ($ctx['role'] === 'tenant' && $ctx['tenant_key'] !== $siteKey) {
        return array('ok' => false, 'error' => 'Access denied');
    }
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $settings = epc_bos_load_tenant_settings($platformPdo, $siteKey);
    if (!$settings) {
        return array('ok' => false, 'error' => 'Tenant not found');
    }

    $sections = epc_bos_sidebar_sections($platformPdo, $siteKey);
    if ($ctx['role'] !== 'provider') {
        $sections = array_filter($sections, function($s) { return empty($s['provider_only']); });
    }

    $country = null;
    $conn = epc_bos_tenant_connect($platformPdo, $siteKey);
    if ($conn['pdo']) {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';
        try {
            $coSt = $conn['pdo']->query("SELECT `value` FROM `epc_co_profile` WHERE `field` = 'country' LIMIT 1");
            $coRow = $coSt ? $coSt->fetch(PDO::FETCH_ASSOC) : null;
            $c = $coRow ? (string) $coRow['value'] : '';
            if ($c !== '') {
                $country = epc_country_profile($c);
            }
        } catch (Exception $e) {
            // non-critical
        }
    }

    return array(
        'ok' => true,
        'tenant' => $siteKey,
        'settings' => $settings,
        'sections' => array_values($sections),
        'country_profile' => $country,
    );
}

/* ───────────────────── fleet health ───────────────────── */

function epc_bos_ajax_fleet_health(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $tenants = epc_bos_tenant_list($platformPdo);
    $fleet = array();

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';

    foreach ($tenants as $t) {
        $siteKey = $t['site_key'] ?? ($t['key'] ?? '');
        if ($siteKey === '') {
            continue;
        }

        $health = array(
            'site_key' => $siteKey,
            'trade_name' => $t['trade_name'] ?? $siteKey,
            'hostname' => $t['hostname'] ?? '',
            'status' => 'unknown',
            'country' => '',
            'currency' => '',
            'users' => 0,
            'products' => 0,
            'orders' => 0,
            'erp_tables' => 0,
        );

        $conn = epc_bos_tenant_connect($platformPdo, $siteKey);
        if ($conn['pdo'] === null) {
            $health['status'] = 'offline';
            $fleet[] = $health;
            continue;
        }

        $health['status'] = 'online';
        $tPdo = $conn['pdo'];

        try {
            $coSt = $tPdo->query("SELECT `value` FROM `epc_co_profile` WHERE `field` = 'country' LIMIT 1");
            $coRow = $coSt ? $coSt->fetch(PDO::FETCH_ASSOC) : null;
            $c = $coRow ? (string) $coRow['value'] : '';
            if ($c !== '') {
                $prof = epc_country_profile($c);
                $health['country'] = $prof['name'];
                $health['currency'] = $prof['currency'];
            }
        } catch (Exception $e) {}

        try { $health['users'] = (int) $tPdo->query("SELECT COUNT(*) FROM `users`")->fetchColumn(); } catch (Exception $e) {}
        try { $health['products'] = (int) $tPdo->query("SELECT COUNT(*) FROM `shop_products`")->fetchColumn(); } catch (Exception $e) {}
        try { $health['orders'] = (int) $tPdo->query("SELECT COUNT(*) FROM `shop_orders`")->fetchColumn(); } catch (Exception $e) {}

        try {
            $erpSt = $tPdo->query("SHOW TABLES LIKE 'epc_erp_%'");
            $health['erp_tables'] = $erpSt ? $erpSt->rowCount() : 0;
        } catch (Exception $e) {}

        $fleet[] = $health;
    }

    $online = count(array_filter($fleet, function($f) { return $f['status'] === 'online'; }));
    $offline = count(array_filter($fleet, function($f) { return $f['status'] === 'offline'; }));

    return array(
        'ok' => true,
        'fleet' => $fleet,
        'summary' => array(
            'total' => count($fleet),
            'online' => $online,
            'offline' => $offline,
            'total_users' => array_sum(array_column($fleet, 'users')),
            'total_products' => array_sum(array_column($fleet, 'products')),
            'total_orders' => array_sum(array_column($fleet, 'orders')),
        ),
    );
}

/* ───────────────────── tenant compliance ───────────────────── */

function epc_bos_ajax_tenant_compliance(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] === 'guest') {
        return array('ok' => false, 'error' => 'Not authenticated');
    }

    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $_GET['site_key'] ?? ''))));
    if ($siteKey === '') {
        return array('ok' => false, 'error' => 'site_key required');
    }

    if ($ctx['role'] === 'tenant' && $ctx['tenant_key'] !== $siteKey) {
        return array('ok' => false, 'error' => 'Access denied');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_localization.php';

    $conn = epc_bos_tenant_connect($platformPdo, $siteKey);
    if ($conn['pdo'] === null) {
        return array('ok' => false, 'error' => 'Cannot connect to tenant');
    }

    $tPdo = $conn['pdo'];
    $checks = array();

    // 1. Company profile completeness
    $profileFields = array('company_name', 'country', 'currency', 'address', 'phone', 'tax_number');
    $profileSet = 0;
    foreach ($profileFields as $f) {
        try {
            $st = $tPdo->prepare("SELECT `value` FROM `epc_co_profile` WHERE `field` = ? LIMIT 1");
            $st->execute(array($f));
            $v = $st->fetchColumn();
            if ($v !== false && trim((string) $v) !== '') {
                $profileSet++;
            }
        } catch (Exception $e) {}
    }
    $checks[] = array(
        'category' => 'Company profile',
        'check' => 'Profile completeness',
        'status' => $profileSet >= 5 ? 'pass' : ($profileSet >= 3 ? 'warn' : 'fail'),
        'detail' => $profileSet . '/' . count($profileFields) . ' fields set',
    );

    // 2. Country configuration
    $country = '';
    try {
        $st = $tPdo->prepare("SELECT `value` FROM `epc_co_profile` WHERE `field` = 'country' LIMIT 1");
        $st->execute();
        $country = (string) $st->fetchColumn();
    } catch (Exception $e) {}
    $checks[] = array(
        'category' => 'Localization',
        'check' => 'Registration country',
        'status' => $country !== '' ? 'pass' : 'fail',
        'detail' => $country !== '' ? epc_country_profile($country)['name'] : 'Not set — all tax/currency/compliance defaults to generic',
    );

    // 3. Chart of Accounts
    $coaCount = 0;
    try { $coaCount = (int) $tPdo->query("SELECT COUNT(*) FROM `epc_erp_coa`")->fetchColumn(); } catch (Exception $e) {}
    $checks[] = array(
        'category' => 'Accounting',
        'check' => 'Chart of Accounts',
        'status' => $coaCount > 10 ? 'pass' : ($coaCount > 0 ? 'warn' : 'fail'),
        'detail' => $coaCount . ' accounts',
    );

    // 4. ERP schema tables
    $erpTables = 0;
    try {
        $erpSt = $tPdo->query("SHOW TABLES LIKE 'epc_erp_%'");
        $erpTables = $erpSt ? $erpSt->rowCount() : 0;
    } catch (Exception $e) {}
    $checks[] = array(
        'category' => 'ERP',
        'check' => 'ERP schema deployed',
        'status' => $erpTables > 20 ? 'pass' : ($erpTables > 0 ? 'warn' : 'fail'),
        'detail' => $erpTables . ' ERP tables',
    );

    // 5. Admin users
    $adminCount = 0;
    try { $adminCount = (int) $tPdo->query("SELECT COUNT(*) FROM `users` WHERE `unlocked` = 1")->fetchColumn(); } catch (Exception $e) {}
    $checks[] = array(
        'category' => 'Security',
        'check' => 'Active users',
        'status' => $adminCount > 0 ? 'pass' : 'fail',
        'detail' => $adminCount . ' active users',
    );

    // 6. Password hash quality
    $md5Count = 0;
    $bcryptCount = 0;
    try {
        $md5Count = (int) $tPdo->query("SELECT COUNT(*) FROM `users` WHERE LENGTH(`password`) = 32")->fetchColumn();
        $bcryptCount = (int) $tPdo->query("SELECT COUNT(*) FROM `users` WHERE `password` LIKE '\$2y\$%'")->fetchColumn();
    } catch (Exception $e) {}
    $checks[] = array(
        'category' => 'Security',
        'check' => 'Password hashing',
        'status' => $md5Count === 0 ? 'pass' : ($bcryptCount > 0 ? 'warn' : 'fail'),
        'detail' => $bcryptCount . ' bcrypt, ' . $md5Count . ' legacy MD5',
    );

    $pass = count(array_filter($checks, function($c) { return $c['status'] === 'pass'; }));
    $warn = count(array_filter($checks, function($c) { return $c['status'] === 'warn'; }));
    $fail = count(array_filter($checks, function($c) { return $c['status'] === 'fail'; }));

    return array(
        'ok' => true,
        'tenant' => $siteKey,
        'checks' => $checks,
        'score' => array('pass' => $pass, 'warn' => $warn, 'fail' => $fail, 'total' => count($checks)),
    );
}

/* ───────────────────── system health ───────────────────── */

function epc_bos_ajax_system_health(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bos_health_check.php';

    $siteKey = isset($_POST['site_key']) ? preg_replace('/[^a-z0-9_]/', '', strtolower(trim($_POST['site_key']))) : '';

    if ($siteKey !== '') {
        $result = epc_bos_health_check_tenant($platformPdo, $siteKey);
        return array('ok' => true, 'results' => array($result), 'summary' => epc_bos_health_summary(array($result)));
    }

    $results = epc_bos_health_check_all($platformPdo);
    return array('ok' => true, 'results' => $results, 'summary' => epc_bos_health_summary($results));
}

/* ───────────────────── isolation audit ───────────────────── */

function epc_bos_ajax_isolation_audit(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_commerce_isolation.php';

    $subAction = (string) ($_POST['sub_action'] ?? 'run_audit');

    switch ($subAction) {
        case 'run_audit':
            $commercePdo = $platformPdo;
            try {
                $cfgFile = $_SERVER['DOCUMENT_ROOT'] . '/config.local.php';
                if (is_file($cfgFile)) {
                    $epc_config_local = null;
                    include $cfgFile;
                    $cDb = (string) ($epc_config_local['commerce_db'] ?? 'docpart');
                    $cUser = (string) ($epc_config_local['user'] ?? '');
                    $cPass = (string) ($epc_config_local['password'] ?? '');
                    if ($cUser !== '') {
                        $commercePdo = new PDO(
                            'mysql:host=127.0.0.1;dbname=' . $cDb . ';charset=utf8mb4',
                            $cUser, $cPass,
                            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
                        );
                    }
                }
            } catch (Exception $e) {
                // Fall back to platform PDO
            }
            $results = epc_ci_run_full_audit($platformPdo, $commercePdo);
            return array('ok' => true, 'audit' => $results);

        case 'recent_violations':
            $violations = epc_ci_recent_violations($platformPdo, 50);
            return array('ok' => true, 'violations' => $violations);

        case 'latest_run':
            $run = epc_ci_latest_audit_run($platformPdo);
            return array('ok' => true, 'latest_run' => $run);

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ─────────────────── MFA Policy Management ─────────────────── */

function epc_bos_ajax_mfa_policy(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_mfa.php';

    $subAction = (string) ($_POST['sub_action'] ?? 'get');

    switch ($subAction) {
        case 'get':
            $tenantKey = (string) ($_POST['tenant_key'] ?? '__platform__');
            $policy = epc_mfa_get_policy($platformPdo, $tenantKey);
            return array('ok' => true, 'policy' => $policy);

        case 'save':
            $tenantKey = (string) ($_POST['tenant_key'] ?? '__platform__');
            $roles = json_decode((string) ($_POST['roles'] ?? '[]'), true);
            $paths = json_decode((string) ($_POST['paths'] ?? '[]'), true);
            $grace = (int) ($_POST['grace_period_hours'] ?? 72);
            $policy = array(
                'require_mfa_for_roles' => is_array($roles) ? $roles : array(),
                'require_mfa_for_paths' => is_array($paths) ? $paths : array(),
                'grace_period_hours'    => $grace,
            );
            $saved = epc_mfa_save_policy($platformPdo, $policy, $tenantKey);
            return array('ok' => $saved, 'policy' => $policy);

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

function epc_bos_ajax_mfa_stats(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_mfa.php';
    epc_mfa_ensure_schema($platformPdo);

    $stats = array('total_enrolled' => 0, 'total_users' => 0, 'recent_verifications' => 0, 'recent_failures' => 0);

    try {
        $st = $platformPdo->query('SELECT COUNT(DISTINCT `user_id`) FROM `epc_mfa_secrets` WHERE `confirmed` = 1');
        $stats['total_enrolled'] = (int) $st->fetchColumn();
    } catch (Exception $e) {}

    try {
        $st = $platformPdo->query('SELECT COUNT(*) FROM `users` WHERE `type` = 1');
        $stats['total_users'] = (int) $st->fetchColumn();
    } catch (Exception $e) {}

    try {
        $since = date('Y-m-d H:i:s', time() - 86400);
        $st = $platformPdo->prepare('SELECT COUNT(*) FROM `epc_mfa_audit_log` WHERE `action` = \'verify_ok\' AND `created_at` > ?');
        $st->execute(array($since));
        $stats['recent_verifications'] = (int) $st->fetchColumn();
    } catch (Exception $e) {}

    try {
        $since = date('Y-m-d H:i:s', time() - 86400);
        $st = $platformPdo->prepare('SELECT COUNT(*) FROM `epc_mfa_audit_log` WHERE `success` = 0 AND `created_at` > ?');
        $st->execute(array($since));
        $stats['recent_failures'] = (int) $st->fetchColumn();
    } catch (Exception $e) {}

    return array('ok' => true, 'stats' => $stats);
}

/* ─────────────────── Webhooks Management ─────────────────── */

function epc_bos_ajax_webhooks(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_webhooks.php';

    $subAction = (string) ($_POST['sub_action'] ?? 'list');

    switch ($subAction) {
        case 'list':
            $tenantKey = (string) ($_POST['tenant_key'] ?? '');
            $hooks = epc_webhooks_list($platformPdo, $tenantKey);
            return array('ok' => true, 'webhooks' => $hooks);

        case 'register':
            return epc_webhooks_register($platformPdo, array(
                'url'         => (string) ($_POST['url'] ?? ''),
                'secret'      => (string) ($_POST['secret'] ?? ''),
                'events'      => json_decode((string) ($_POST['events'] ?? '["*"]'), true) ?: array('*'),
                'tenant_key'  => (string) ($_POST['tenant_key'] ?? '__platform__'),
                'description' => (string) ($_POST['description'] ?? ''),
            ));

        case 'update':
            $webhookId = (int) ($_POST['webhook_id'] ?? 0);
            $data = array();
            if (isset($_POST['url'])) { $data['url'] = (string) $_POST['url']; }
            if (isset($_POST['events'])) { $data['events'] = json_decode((string) $_POST['events'], true); }
            if (isset($_POST['active'])) { $data['active'] = (int) $_POST['active']; }
            if (isset($_POST['description'])) { $data['description'] = (string) $_POST['description']; }
            return epc_webhooks_update($platformPdo, $webhookId, $data);

        case 'delete':
            return epc_webhooks_delete($platformPdo, (int) ($_POST['webhook_id'] ?? 0));

        case 'stats':
            return array('ok' => true, 'stats' => epc_webhooks_delivery_stats($platformPdo));

        case 'dlq':
            return array('ok' => true, 'items' => epc_webhooks_dlq_list($platformPdo));

        case 'dlq_retry':
            return epc_webhooks_dlq_retry($platformPdo, (int) ($_POST['dlq_id'] ?? 0));

        case 'dlq_resolve':
            return epc_webhooks_dlq_resolve($platformPdo, (int) ($_POST['dlq_id'] ?? 0));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ─────────────────── Events Browser ─────────────────── */

function epc_bos_ajax_events(): array
{
    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_events.php';

    $subAction = (string) ($_POST['sub_action'] ?? 'list');

    switch ($subAction) {
        case 'list':
            $filters = array();
            if (!empty($_POST['event_type'])) { $filters['event_type'] = (string) $_POST['event_type']; }
            if (!empty($_POST['tenant_key'])) { $filters['tenant_key'] = (string) $_POST['tenant_key']; }
            if (!empty($_POST['since'])) { $filters['since'] = (string) $_POST['since']; }
            $limit = min(100, max(1, (int) ($_POST['limit'] ?? 50)));
            $offset = max(0, (int) ($_POST['offset'] ?? 0));
            $events = epc_events_list($platformPdo, $filters, $limit, $offset);
            $count = epc_events_count($platformPdo, $filters);
            return array('ok' => true, 'events' => $events, 'total' => $count);

        case 'summary':
            $since = (string) ($_POST['since'] ?? '');
            return array('ok' => true, 'summary' => epc_events_type_summary($platformPdo, $since));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── design tokens ───────────────────── */

function epc_bos_ajax_design_tokens(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_design_tokens.php';

    $subAction = (string) ($_POST['sub_action'] ?? 'list_tenants');

    switch ($subAction) {
        case 'list_tenants':
            $catalog = epc_design_tokens_tenant_catalog();
            $result = array();
            foreach ($catalog as $key => $entry) {
                $meta = epc_design_tokens_tenant_meta($key);
                $tokens = epc_design_tokens_resolve($key, $meta['industry']);
                $result[] = array(
                    'site_key'   => $key,
                    'trade_name' => $meta['trade_name'],
                    'tagline'    => $meta['tagline'],
                    'industry'   => $meta['industry'],
                    'primary'    => $tokens['--epc-brand-primary'] ?? '',
                    'accent'     => $tokens['--epc-brand-accent'] ?? '',
                    'logo_url'   => $tokens['--epc-brand-logo-url'] ?? '',
                );
            }
            return array('ok' => true, 'tenants' => $result);

        case 'get_tokens':
            $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            $meta = epc_design_tokens_tenant_meta($siteKey);
            $tokens = epc_design_tokens_resolve($siteKey, $meta['industry']);
            $css = epc_design_tokens_css($siteKey, $meta['industry']);
            return array('ok' => true, 'site_key' => $siteKey, 'meta' => $meta, 'tokens' => $tokens, 'css' => $css);

        case 'save_token':
            $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
            $settingKey = preg_replace('/[^a-z0-9_]/', '', (string) ($_POST['setting_key'] ?? ''));
            $value = (string) ($_POST['value'] ?? '');
            if ($siteKey === '' || $settingKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key or setting_key');
            }
            try {
                require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
                $pdo = epc_portal_platform_pdo();
                if (!$pdo) {
                    return array('ok' => false, 'error' => 'Platform DB not available');
                }
                $ok = epc_design_tokens_save($pdo, $siteKey, $settingKey, $value);
                return array('ok' => $ok, 'saved' => array('site_key' => $siteKey, 'key' => $settingKey, 'value' => $value));
            } catch (\Exception $e) {
                return array('ok' => false, 'error' => $e->getMessage());
            }

        case 'preview_css':
            $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
            $industry = preg_replace('/[^a-z0-9_]/', '', (string) ($_POST['industry'] ?? ''));
            $css = epc_design_tokens_css($siteKey, $industry);
            return array('ok' => true, 'css' => $css);

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── readiness score ───────────────────── */

function epc_bos_ajax_readiness_score(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_readiness_score.php';

    $subAction = (string) ($_POST['sub_action'] ?? 'tenant');

    switch ($subAction) {
        case 'tenant':
            $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            try {
                require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
                $pdo = epc_portal_platform_pdo();
                if (!$pdo) {
                    return array('ok' => false, 'error' => 'Platform DB not available');
                }
                return array('ok' => true, 'readiness' => epc_readiness_score($pdo, $siteKey));
            } catch (\Exception $e) {
                return array('ok' => false, 'error' => $e->getMessage());
            }

        case 'fleet':
            try {
                require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
                $pdo = epc_portal_platform_pdo();
                if (!$pdo) {
                    return array('ok' => false, 'error' => 'Platform DB not available');
                }
                return array('ok' => true, 'fleet' => epc_readiness_fleet_summary($pdo));
            } catch (\Exception $e) {
                return array('ok' => false, 'error' => $e->getMessage());
            }

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── notifications ───────────────────── */

function epc_bos_ajax_notifications(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_notifications.php';

    $ctx = epc_bos_context();
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'list');
    $tenantKey = (string) ($_POST['tenant_key'] ?? '__platform__');

    switch ($subAction) {
        case 'list':
            $filters = array();
            if (isset($_POST['category']) && $_POST['category'] !== '') {
                $filters['category'] = (string) $_POST['category'];
            }
            if (isset($_POST['is_read'])) {
                $filters['is_read'] = (int) $_POST['is_read'];
            }
            if (isset($_POST['severity']) && $_POST['severity'] !== '') {
                $filters['severity'] = (string) $_POST['severity'];
            }
            $filters['dismissed'] = false;
            $limit = min(100, max(1, (int) ($_POST['limit'] ?? 50)));
            $offset = max(0, (int) ($_POST['offset'] ?? 0));
            $userId = (int) ($_POST['user_id'] ?? 0);
            $items = epc_notifications_list($platformPdo, $tenantKey, $userId, $filters, $limit, $offset);
            $unread = epc_notifications_unread_count($platformPdo, $tenantKey, $userId);
            return array('ok' => true, 'notifications' => $items, 'unread' => $unread);

        case 'unread_count':
            $userId = (int) ($_POST['user_id'] ?? 0);
            return array('ok' => true, 'unread' => epc_notifications_unread_count($platformPdo, $tenantKey, $userId));

        case 'summary':
            $userId = (int) ($_POST['user_id'] ?? 0);
            return array('ok' => true, 'summary' => epc_notifications_summary($platformPdo, $tenantKey, $userId));

        case 'mark_read':
            $ids = json_decode((string) ($_POST['ids'] ?? '[]'), true);
            if (!is_array($ids) || empty($ids)) {
                return array('ok' => false, 'error' => 'Missing ids');
            }
            $count = epc_notifications_mark_read($platformPdo, array_map('intval', $ids), $tenantKey);
            return array('ok' => true, 'marked' => $count);

        case 'mark_all_read':
            $userId = (int) ($_POST['user_id'] ?? 0);
            $count = epc_notifications_mark_all_read($platformPdo, $tenantKey, $userId);
            return array('ok' => true, 'marked' => $count);

        case 'dismiss':
            $id = (int) ($_POST['notification_id'] ?? 0);
            return array('ok' => epc_notifications_dismiss($platformPdo, $id, $tenantKey));

        case 'send':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $notifId = epc_notification_send($platformPdo, array(
                'tenant_key'   => $tenantKey,
                'user_id'      => (int) ($_POST['user_id'] ?? 0),
                'category'     => (string) ($_POST['category'] ?? 'system'),
                'severity'     => (string) ($_POST['severity'] ?? 'info'),
                'title'        => (string) ($_POST['title'] ?? ''),
                'body'         => (string) ($_POST['body'] ?? ''),
                'action_url'   => (string) ($_POST['action_url'] ?? ''),
                'action_label' => (string) ($_POST['action_label'] ?? ''),
            ));
            return array('ok' => $notifId > 0, 'notification_id' => $notifId);

        case 'broadcast':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $notifId = epc_notification_broadcast($platformPdo, $tenantKey, array(
                'category' => (string) ($_POST['category'] ?? 'system'),
                'severity' => (string) ($_POST['severity'] ?? 'info'),
                'title'    => (string) ($_POST['title'] ?? ''),
                'body'     => (string) ($_POST['body'] ?? ''),
            ));
            return array('ok' => $notifId > 0, 'notification_id' => $notifId);

        case 'fleet_stats':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            return array('ok' => true, 'stats' => epc_notifications_fleet_stats($platformPdo));

        case 'categories':
            return array('ok' => true, 'categories' => epc_notification_categories());

        case 'prefs_get':
            $userId = (int) ($_POST['user_id'] ?? 0);
            return array('ok' => true, 'prefs' => epc_notification_prefs_get($platformPdo, $tenantKey, $userId));

        case 'prefs_save':
            $userId = (int) ($_POST['user_id'] ?? 0);
            $category = (string) ($_POST['category'] ?? '*');
            $channels = array(
                'in_app'       => (int) ($_POST['channel_in_app'] ?? 1),
                'email'        => (int) ($_POST['channel_email'] ?? 1),
                'webhook'      => (int) ($_POST['channel_webhook'] ?? 0),
                'email_digest' => (string) ($_POST['email_digest'] ?? 'daily'),
            );
            $ok = epc_notification_prefs_save($platformPdo, $tenantKey, $userId, $category, $channels);
            return array('ok' => $ok);

        case 'cleanup':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $days = max(7, (int) ($_POST['days'] ?? 90));
            $deleted = epc_notifications_cleanup($platformPdo, $days);
            return array('ok' => true, 'deleted' => $deleted);

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── db migrations ───────────────────── */

function epc_bos_ajax_db_migrations(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_db_migrations.php';

    $ctx = epc_bos_context();
    if ($ctx['role'] !== 'provider') {
        return array('ok' => false, 'error' => 'Provider access required');
    }

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'status');

    switch ($subAction) {
        case 'status':
            return array('ok' => true, 'migrations' => epc_migrations_status($platformPdo));

        case 'pending':
            return array('ok' => true, 'pending' => epc_migrations_pending($platformPdo));

        case 'run_all':
            return array('ok' => true, 'result' => epc_migrations_run_all($platformPdo));

        case 'apply':
            $version = preg_replace('/[^0-9]/', '', (string) ($_POST['version'] ?? ''));
            if ($version === '') {
                return array('ok' => false, 'error' => 'Missing version');
            }
            $registry = epc_migrations_registry();
            foreach ($registry as $m) {
                if ($m['version'] === $version) {
                    return epc_migration_apply($platformPdo, $m);
                }
            }
            return array('ok' => false, 'error' => 'Migration not found');

        case 'rollback':
            $version = preg_replace('/[^0-9]/', '', (string) ($_POST['version'] ?? ''));
            if ($version === '') {
                return array('ok' => false, 'error' => 'Missing version');
            }
            return epc_migration_rollback($platformPdo, $version);

        case 'verify':
            return array('ok' => true, 'integrity' => epc_migrations_verify($platformPdo));

        case 'dry_run':
            return array('ok' => true, 'dry_run' => epc_migrations_dry_run($platformPdo));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── role management ───────────────────── */

function epc_bos_ajax_role_management(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_role_home.php';

    $subAction = (string) ($_POST['sub_action'] ?? 'list_roles');

    switch ($subAction) {
        case 'list_roles':
            return array('ok' => true, 'roles' => epc_cp_roles());

        case 'role_tiles':
            $role = preg_replace('/[^a-z_]/', '', (string) ($_POST['role'] ?? 'admin'));
            return array('ok' => true, 'tiles' => epc_cp_role_dashboard_tiles($role));

        case 'role_actions':
            $role = preg_replace('/[^a-z_]/', '', (string) ($_POST['role'] ?? 'admin'));
            return array('ok' => true, 'actions' => epc_cp_role_quick_actions($role));

        case 'role_modules':
            $role = preg_replace('/[^a-z_]/', '', (string) ($_POST['role'] ?? 'admin'));
            return array('ok' => true, 'modules' => epc_cp_role_modules($role));

        case 'check_permission':
            $role = preg_replace('/[^a-z_]/', '', (string) ($_POST['role'] ?? 'viewer'));
            $permission = preg_replace('/[^a-z0-9_.*]/', '', (string) ($_POST['permission'] ?? ''));
            return array('ok' => true, 'allowed' => epc_cp_role_can($role, $permission));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── credit limits ───────────────────── */

function epc_bos_ajax_credit_limits(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_credit_limit.php';

    $ctx = epc_bos_context();
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_summary');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_summary':
            return array('ok' => true, 'summary' => epc_credit_fleet_summary($platformPdo));

        case 'get':
            $customerId = (int) ($_POST['customer_id'] ?? 0);
            if ($siteKey === '' || $customerId <= 0) {
                return array('ok' => false, 'error' => 'Missing site_key or customer_id');
            }
            return array('ok' => true, 'credit' => epc_credit_get($platformPdo, $siteKey, $customerId));

        case 'set_limit':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $customerId = (int) ($_POST['customer_id'] ?? 0);
            $limit = (float) ($_POST['credit_limit'] ?? 0);
            if ($siteKey === '' || $customerId <= 0) {
                return array('ok' => false, 'error' => 'Missing site_key or customer_id');
            }
            $result = epc_credit_set_limit($platformPdo, $siteKey, $customerId, $limit, array(
                'currency'      => (string) ($_POST['currency'] ?? 'AED'),
                'payment_terms' => (string) ($_POST['payment_terms'] ?? 'net30'),
                'notes'         => (string) ($_POST['notes'] ?? ''),
            ));
            return array('ok' => true, 'credit' => $result);

        case 'check_order':
            $customerId = (int) ($_POST['customer_id'] ?? 0);
            $orderTotal = (float) ($_POST['order_total'] ?? 0);
            if ($siteKey === '' || $customerId <= 0) {
                return array('ok' => false, 'error' => 'Missing site_key or customer_id');
            }
            return array('ok' => true, 'check' => epc_credit_check_order($platformPdo, $siteKey, $customerId, $orderTotal));

        case 'hold':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $customerId = (int) ($_POST['customer_id'] ?? 0);
            $reason = (string) ($_POST['reason'] ?? '');
            epc_credit_hold($platformPdo, $siteKey, $customerId, $reason);
            return array('ok' => true);

        case 'release':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $customerId = (int) ($_POST['customer_id'] ?? 0);
            epc_credit_release($platformPdo, $siteKey, $customerId);
            return array('ok' => true);

        case 'history':
            $customerId = (int) ($_POST['customer_id'] ?? 0);
            if ($siteKey === '' || $customerId <= 0) {
                return array('ok' => false, 'error' => 'Missing site_key or customer_id');
            }
            return array('ok' => true, 'history' => epc_credit_history($platformPdo, $siteKey, $customerId));

        case 'due_for_review':
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            return array('ok' => true, 'reviews' => epc_credit_due_for_review($platformPdo, $siteKey));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── order erp pipeline ───────────────────── */

function epc_bos_ajax_order_erp_pipeline(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_order_erp_pipeline.php';

    $ctx = epc_bos_context();
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_order_erp_fleet_stats($platformPdo));

        case 'order_status':
            $orderId = (int) ($_POST['order_id'] ?? 0);
            if ($siteKey === '' || $orderId <= 0) {
                return array('ok' => false, 'error' => 'Missing site_key or order_id');
            }
            return array('ok' => true, 'pipeline' => epc_order_erp_status($platformPdo, $siteKey, $orderId));

        case 'run':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $order = json_decode((string) ($_POST['order_data'] ?? '{}'), true);
            if (empty($order) || $siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key or order_data');
            }
            return epc_order_erp_run($platformPdo, $siteKey, $order);

        case 'steps':
            return array('ok' => true, 'steps' => epc_order_erp_pipeline_steps());

        case 'retry':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $orderId = (int) ($_POST['order_id'] ?? 0);
            $order = json_decode((string) ($_POST['order_data'] ?? '{}'), true);
            if ($siteKey === '' || $orderId <= 0) {
                return array('ok' => false, 'error' => 'Missing site_key or order_id');
            }
            return epc_order_erp_retry_failed($platformPdo, $siteKey, $orderId, $order ?: array());

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── po approval ───────────────────── */

function epc_bos_ajax_po_approval(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_po_approval.php';

    $ctx = epc_bos_context();
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_po_fleet_stats($platformPdo));

        case 'list':
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            $filters = array();
            if (!empty($_POST['status'])) $filters['status'] = (string) $_POST['status'];
            return array('ok' => true, 'pos' => epc_po_list($platformPdo, $siteKey, $filters));

        case 'get':
            $poId = (int) ($_POST['po_id'] ?? 0);
            if ($poId <= 0) {
                return array('ok' => false, 'error' => 'Missing po_id');
            }
            return array('ok' => true, 'po' => epc_po_get($platformPdo, $poId));

        case 'create':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            $data = json_decode((string) ($_POST['po_data'] ?? '{}'), true);
            if (empty($data)) {
                return array('ok' => false, 'error' => 'Missing po_data');
            }
            return epc_po_create($platformPdo, $siteKey, $data);

        case 'approve':
            $poId = (int) ($_POST['po_id'] ?? 0);
            $tier = (int) ($_POST['tier'] ?? 0);
            $approverId = (int) ($_POST['approver_id'] ?? 0);
            $comment = (string) ($_POST['comment'] ?? '');
            return epc_po_approve($platformPdo, $poId, $tier, $approverId, $comment);

        case 'reject':
            $poId = (int) ($_POST['po_id'] ?? 0);
            $tier = (int) ($_POST['tier'] ?? 0);
            $approverId = (int) ($_POST['approver_id'] ?? 0);
            $reason = (string) ($_POST['reason'] ?? '');
            return epc_po_reject($platformPdo, $poId, $tier, $approverId, $reason);

        case 'cancel':
            $poId = (int) ($_POST['po_id'] ?? 0);
            $userId = (int) ($_POST['user_id'] ?? 0);
            return epc_po_cancel($platformPdo, $poId, $userId);

        case 'tiers':
            return array('ok' => true, 'tiers' => epc_po_approval_tiers());

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── rest api v2 ───────────────────── */

function epc_bos_ajax_rest_api(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_rest_api_v2.php';

    $ctx = epc_bos_context();
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_api_fleet_stats($platformPdo));

        case 'endpoints':
            return array('ok' => true, 'endpoints' => epc_api_v2_endpoints());

        case 'openapi':
            return array('ok' => true, 'spec' => epc_api_v2_openapi_spec());

        case 'keys_list':
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            return array('ok' => true, 'keys' => epc_api_keys_list($platformPdo, $siteKey));

        case 'key_generate':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            $scopes = json_decode((string) ($_POST['scopes'] ?? '["read"]'), true);
            return epc_api_key_generate($platformPdo, $siteKey, array(
                'scopes'     => $scopes,
                'label'      => (string) ($_POST['label'] ?? 'API Key'),
                'rate_limit' => (int) ($_POST['rate_limit'] ?? 1000),
            ));

        case 'key_revoke':
            if ($ctx['role'] !== 'provider') {
                return array('ok' => false, 'error' => 'Provider access required');
            }
            $keyId = (int) ($_POST['key_id'] ?? 0);
            epc_api_key_revoke($platformPdo, $keyId);
            return array('ok' => true);

        case 'usage':
            if ($siteKey === '') {
                return array('ok' => false, 'error' => 'Missing site_key');
            }
            $hours = (int) ($_POST['hours'] ?? 24);
            return array('ok' => true, 'usage' => epc_api_usage_by_endpoint($platformPdo, $siteKey, $hours));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── fulfillment queue ───────────────────── */

function epc_bos_ajax_fulfillment(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_fulfillment_queue.php';

    $ctx = epc_bos_context();
    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_fulfillment_fleet_stats($platformPdo));

        case 'list':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $filters = array();
            if (!empty($_POST['status'])) $filters['status'] = (string) $_POST['status'];
            if (!empty($_POST['warehouse'])) $filters['warehouse'] = (string) $_POST['warehouse'];
            return array('ok' => true, 'queue' => epc_fulfillment_list($platformPdo, $siteKey, $filters));

        case 'get':
            $fId = (int) ($_POST['fulfillment_id'] ?? 0);
            return array('ok' => true, 'fulfillment' => epc_fulfillment_get($platformPdo, $fId));

        case 'queue':
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $order = json_decode((string) ($_POST['order_data'] ?? '{}'), true);
            return epc_fulfillment_queue($platformPdo, $siteKey, $order ?: array());

        case 'transition':
            $fId = (int) ($_POST['fulfillment_id'] ?? 0);
            $newStatus = (string) ($_POST['new_status'] ?? '');
            $opts = json_decode((string) ($_POST['opts'] ?? '{}'), true);
            return epc_fulfillment_transition($platformPdo, $fId, $newStatus, $opts ?: array());

        case 'pick_item':
            $itemId = (int) ($_POST['item_id'] ?? 0);
            $qtyPicked = (int) ($_POST['qty_picked'] ?? 0);
            return epc_fulfillment_pick_item($platformPdo, $itemId, $qtyPicked);

        case 'packing_slip':
            $fId = (int) ($_POST['fulfillment_id'] ?? 0);
            return epc_fulfillment_packing_slip($platformPdo, $fId);

        case 'create_wave':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $ids = json_decode((string) ($_POST['fulfillment_ids'] ?? '[]'), true);
            return epc_fulfillment_create_wave($platformPdo, $siteKey, $ids ?: array());

        case 'sla_breaches':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $maxHours = (int) ($_POST['max_hours'] ?? 48);
            return array('ok' => true, 'breaches' => epc_fulfillment_sla_breaches($platformPdo, $siteKey, $maxHours));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── bi metrics ───────────────────── */

function epc_bos_ajax_bi_metrics(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bi_metrics.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_overview');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_overview':
            return array('ok' => true, 'overview' => epc_bi_fleet_overview($platformPdo));

        case 'dashboard':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'dashboard' => epc_bi_dashboard($platformPdo, $siteKey));

        case 'trend':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $metricKey = preg_replace('/[^a-z0-9_]/', '', (string) ($_POST['metric_key'] ?? ''));
            $limit = (int) ($_POST['limit'] ?? 30);
            return array('ok' => true, 'trend' => epc_bi_metric_trend($platformPdo, $siteKey, $metricKey, 'daily', $limit));

        case 'compute':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $date = (string) ($_POST['date'] ?? date('Y-m-d'));
            return epc_bi_compute_daily($platformPdo, $siteKey, $date);

        case 'compare':
            $metricKey = preg_replace('/[^a-z0-9_]/', '', (string) ($_POST['metric_key'] ?? ''));
            return array('ok' => true, 'comparison' => epc_bi_compare_tenants($platformPdo, $metricKey));

        case 'definitions':
            return array('ok' => true, 'metrics' => epc_bi_builtin_metrics());

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── ai classification ───────────────────── */

function epc_bos_ajax_ai_classification(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ai_classification.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_ai_class_fleet_stats($platformPdo));

        case 'classify':
            $text = (string) ($_POST['text'] ?? '');
            return array('ok' => true, 'result' => epc_ai_classify($text));

        case 'classify_product':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $product = json_decode((string) ($_POST['product'] ?? '{}'), true);
            return epc_ai_classify_and_store($platformPdo, $siteKey, $product ?: array());

        case 'batch':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $products = json_decode((string) ($_POST['products'] ?? '[]'), true);
            return epc_ai_classify_batch($platformPdo, $siteKey, $products ?: array());

        case 'stats':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'stats' => epc_ai_class_stats($platformPdo, $siteKey));

        case 'hs_lookup':
            $query = (string) ($_POST['query'] ?? '');
            return array('ok' => true, 'results' => epc_ai_hs_lookup($platformPdo, $query));

        case 'seed_hs':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            return array('ok' => true, 'inserted' => epc_ai_seed_hs_codes($platformPdo));

        case 'rules':
            return array('ok' => true, 'rules' => epc_ai_category_rules());

        case 'review':
            $classId = (int) ($_POST['classification_id'] ?? 0);
            $category = (string) ($_POST['category'] ?? '');
            $subcategory = (string) ($_POST['subcategory'] ?? '');
            $hsCode = (string) ($_POST['hs_code'] ?? '');
            $reviewerId = (int) ($_POST['reviewer_id'] ?? 0);
            epc_ai_review($platformPdo, $classId, $category, $subcategory, $hsCode, $reviewerId);
            return array('ok' => true);

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── tenant config ───────────────────── */

function epc_bos_ajax_tenant_config(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_tenant_config.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet':
            return array('ok' => true, 'fleet' => epc_tenant_config_fleet($platformPdo));

        case 'groups':
            return array('ok' => true, 'groups' => epc_tenant_config_groups());

        case 'all':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'config' => epc_tenant_config_all($platformPdo, $siteKey));

        case 'group':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $group = preg_replace('/[^a-z_]/', '', (string) ($_POST['group'] ?? ''));
            return array('ok' => true, 'fields' => epc_tenant_config_group($platformPdo, $siteKey, $group));

        case 'set':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $group = preg_replace('/[^a-z_]/', '', (string) ($_POST['group'] ?? ''));
            $key = preg_replace('/[^a-z0-9_]/', '', (string) ($_POST['key'] ?? ''));
            $value = (string) ($_POST['value'] ?? '');
            return epc_tenant_config_set($platformPdo, $siteKey, $group, $key, $value);

        case 'bulk_set':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $group = preg_replace('/[^a-z_]/', '', (string) ($_POST['group'] ?? ''));
            $values = json_decode((string) ($_POST['values'] ?? '{}'), true);
            return epc_tenant_config_bulk_set($platformPdo, $siteKey, $group, $values ?: array());

        case 'history':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'history' => epc_tenant_config_history($platformPdo, $siteKey));

        case 'export':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'config' => epc_tenant_config_export($platformPdo, $siteKey));

        case 'import':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $configs = json_decode((string) ($_POST['configs'] ?? '[]'), true);
            return epc_tenant_config_import($platformPdo, $siteKey, $configs ?: array());

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── workflow builder ───────────────────── */

function epc_bos_ajax_workflow_builder(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_workflow_builder.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_workflow_fleet_stats($platformPdo));

        case 'list':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $filters = array();
            if (isset($_POST['active'])) $filters['active'] = (int) $_POST['active'];
            return array('ok' => true, 'workflows' => epc_workflow_list($platformPdo, $siteKey, $filters));

        case 'get':
            $wfId = (int) ($_POST['workflow_id'] ?? 0);
            return array('ok' => true, 'workflow' => epc_workflow_get($platformPdo, $wfId));

        case 'create':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $data = json_decode((string) ($_POST['workflow_data'] ?? '{}'), true);
            return epc_workflow_create($platformPdo, $siteKey, $data ?: array());

        case 'toggle':
            $wfId = (int) ($_POST['workflow_id'] ?? 0);
            $active = (bool) ($_POST['active'] ?? false);
            epc_workflow_toggle($platformPdo, $wfId, $active);
            return array('ok' => true);

        case 'delete':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            $wfId = (int) ($_POST['workflow_id'] ?? 0);
            epc_workflow_delete($platformPdo, $wfId);
            return array('ok' => true);

        case 'execute':
            $wfId = (int) ($_POST['workflow_id'] ?? 0);
            $triggerData = json_decode((string) ($_POST['trigger_data'] ?? '{}'), true);
            return epc_workflow_execute($platformPdo, $wfId, $triggerData ?: array());

        case 'history':
            $wfId = (int) ($_POST['workflow_id'] ?? 0);
            return array('ok' => true, 'runs' => epc_workflow_run_history($platformPdo, $wfId));

        case 'triggers':
            return array('ok' => true, 'triggers' => epc_workflow_trigger_types());

        case 'actions':
            return array('ok' => true, 'actions' => epc_workflow_action_types());

        case 'templates':
            return array('ok' => true, 'templates' => epc_workflow_templates());

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── inventory forecast ───────────────────── */

function epc_bos_ajax_inventory_forecast(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_inventory_forecast.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_forecast_fleet_stats($platformPdo));

        case 'compute':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $sku = (string) ($_POST['sku'] ?? '');
            $stock = (int) ($_POST['current_stock'] ?? 0);
            $name = (string) ($_POST['product_name'] ?? '');
            $lead = (int) ($_POST['lead_time_days'] ?? 7);
            return epc_forecast_compute($platformPdo, $siteKey, $sku, $stock, $name, $lead);

        case 'suggestions':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'suggestions' => epc_forecast_purchase_suggestions($platformPdo, $siteKey));

        case 'trend':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $sku = (string) ($_POST['sku'] ?? '');
            return array('ok' => true, 'trend' => epc_forecast_demand_trend($platformPdo, $siteKey, $sku));

        case 'stockouts':
            $days = (int) ($_POST['within_days'] ?? 14);
            return array('ok' => true, 'stockouts' => epc_forecast_upcoming_stockouts($platformPdo, $days));

        case 'abc':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'abc' => epc_forecast_abc_analysis($platformPdo, $siteKey));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── multi currency gl ───────────────────── */

function epc_bos_ajax_multi_currency(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_multi_currency_gl.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_mcgl_fleet_stats($platformPdo));

        case 'currencies':
            return array('ok' => true, 'currencies' => epc_mcgl_currencies());

        case 'set_rate':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            $base = strtoupper((string) ($_POST['base'] ?? 'AED'));
            $target = strtoupper((string) ($_POST['target'] ?? ''));
            $rate = (float) ($_POST['rate'] ?? 0);
            $date = (string) ($_POST['date'] ?? date('Y-m-d'));
            return epc_mcgl_set_rate($platformPdo, $base, $target, $rate, $date);

        case 'get_rate':
            $base = strtoupper((string) ($_POST['base'] ?? 'AED'));
            $target = strtoupper((string) ($_POST['target'] ?? ''));
            $rate = epc_mcgl_get_rate($platformPdo, $base, $target);
            return array('ok' => true, 'base' => $base, 'target' => $target, 'rate' => $rate);

        case 'rate_history':
            $base = strtoupper((string) ($_POST['base'] ?? 'AED'));
            $target = strtoupper((string) ($_POST['target'] ?? ''));
            return array('ok' => true, 'history' => epc_mcgl_rate_history($platformPdo, $base, $target));

        case 'convert':
            $amount = (float) ($_POST['amount'] ?? 0);
            $from = strtoupper((string) ($_POST['from'] ?? ''));
            $to = strtoupper((string) ($_POST['to'] ?? ''));
            return epc_mcgl_convert($platformPdo, $amount, $from, $to);

        case 'journal_entry':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $entry = json_decode((string) ($_POST['entry'] ?? '{}'), true);
            return epc_mcgl_journal_entry($platformPdo, $siteKey, $entry ?: array());

        case 'revalue':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return epc_mcgl_revalue($platformPdo, $siteKey);

        case 'exposure':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'exposure' => epc_mcgl_exposure($platformPdo, $siteKey));

        case 'trial_balance':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $from = (string) ($_POST['from_date'] ?? '');
            $to = (string) ($_POST['to_date'] ?? '');
            return array('ok' => true, 'trial_balance' => epc_mcgl_trial_balance($platformPdo, $siteKey, $from, $to));

        case 'seed_rates':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            return array('ok' => true, 'seeded' => epc_mcgl_seed_rates($platformPdo));

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── sso saml ───────────────────── */

function epc_bos_ajax_sso_saml(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_sso_saml.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_sso_fleet_stats($platformPdo));

        case 'sp_metadata':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'metadata' => epc_sso_sp_metadata($siteKey));

        case 'sp_metadata_xml':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'xml' => epc_sso_sp_metadata_xml($siteKey));

        case 'providers':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'providers' => epc_sso_provider_list($platformPdo, $siteKey));

        case 'provider_get':
            $pid = (int) ($_POST['provider_id'] ?? 0);
            return array('ok' => true, 'provider' => epc_sso_provider_get($platformPdo, $pid));

        case 'provider_create':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $data = json_decode((string) ($_POST['provider_data'] ?? '{}'), true);
            return epc_sso_provider_create($platformPdo, $siteKey, $data ?: array());

        case 'provider_toggle':
            $pid = (int) ($_POST['provider_id'] ?? 0);
            $active = (bool) ($_POST['active'] ?? false);
            epc_sso_provider_toggle($platformPdo, $pid, $active);
            return array('ok' => true);

        case 'provider_delete':
            $ctx = epc_bos_context();
            if ($ctx['role'] !== 'provider') return array('ok' => false, 'error' => 'Provider access required');
            $pid = (int) ($_POST['provider_id'] ?? 0);
            epc_sso_provider_delete($platformPdo, $pid);
            return array('ok' => true);

        case 'initiate':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $pid = (int) ($_POST['provider_id'] ?? 0);
            $provider = epc_sso_provider_get($platformPdo, $pid);
            if (empty($provider)) return array('ok' => false, 'error' => 'Provider not found');
            return epc_sso_authn_request($siteKey, $provider);

        case 'sessions':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'sessions' => epc_sso_active_sessions($platformPdo, $siteKey));

        case 'logout':
            $sessionId = (int) ($_POST['session_id'] ?? 0);
            epc_sso_logout($platformPdo, $sessionId);
            return array('ok' => true);

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}

/* ───────────────────── wps payroll ───────────────────── */

function epc_bos_ajax_wps_payroll(): array
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_wps_payroll.php';

    $platformPdo = epc_portal_platform_operator_pdo();
    if (!$platformPdo) {
        return array('ok' => false, 'error' => 'Database unavailable');
    }

    $subAction = (string) ($_POST['sub_action'] ?? 'fleet_stats');
    $siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));

    switch ($subAction) {
        case 'fleet_stats':
            return array('ok' => true, 'stats' => epc_payroll_fleet_stats($platformPdo));

        case 'employees':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $status = (string) ($_POST['status'] ?? '');
            return array('ok' => true, 'employees' => epc_payroll_employee_list($platformPdo, $siteKey, $status));

        case 'employee_add':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $data = json_decode((string) ($_POST['employee_data'] ?? '{}'), true);
            return epc_payroll_employee_add($platformPdo, $siteKey, $data ?: array());

        case 'create_run':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            $month = (string) ($_POST['month'] ?? date('Y-m'));
            return epc_payroll_create_run($platformPdo, $siteKey, $month);

        case 'approve_run':
            $runId = (int) ($_POST['run_id'] ?? 0);
            $approverId = (int) ($_POST['approver_id'] ?? 0);
            return epc_payroll_approve_run($platformPdo, $runId, $approverId);

        case 'run_details':
            $runId = (int) ($_POST['run_id'] ?? 0);
            return array('ok' => true, 'run' => epc_payroll_run_details($platformPdo, $runId));

        case 'runs':
            if ($siteKey === '') return array('ok' => false, 'error' => 'Missing site_key');
            return array('ok' => true, 'runs' => epc_payroll_run_list($platformPdo, $siteKey));

        case 'generate_sif':
            $runId = (int) ($_POST['run_id'] ?? 0);
            $molId = (string) ($_POST['employer_mol_id'] ?? '');
            $bankCode = (string) ($_POST['employer_bank_code'] ?? '');
            return epc_payroll_generate_sif($platformPdo, $runId, $molId, $bankCode);

        default:
            return array('ok' => false, 'error' => 'Unknown sub_action');
    }
}
