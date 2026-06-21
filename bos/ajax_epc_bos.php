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
