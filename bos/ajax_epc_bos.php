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
