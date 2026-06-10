<?php
/**
 * Advanced ERP — one-shot setup / migration (token-gated, additive).
 *
 * Ensures every advanced-ERP capability is present and wired for the current
 * tenant, then registers the in-app guide. Everything is additive
 * (CREATE TABLE IF NOT EXISTS / INSERT ... ON DUPLICATE KEY UPDATE) so it is
 * safe to run on a live tenant such as epartscart.com.
 *
 * Run (browser or curl):
 *   https://www.<tenant>.com/epc-erp-advanced-setup.php?token=epartscart-deploy-2026
 *
 * Optional parameters:
 *   &industry=auto_parts   Apply an industry blueprint (seeds inventory fields)
 *   &tax=defaults|all      Install worldwide tax kits (default: defaults)
 *
 * What it does:
 *   1. Ensures core ERP + inventory schema.
 *   2. Ensures CRM schema (incl. the fixed sample-seed).
 *   3. Ensures the worldwide tax toolkit schema and installs tax kits.
 *   4. Ensures the industry foundation schema (+ optionally applies one).
 *   5. Registers the Advanced ERP guide CP page.
 */

error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_inventory.php';
require_once __DIR__ . '/content/shop/finance/epc_crm_schema.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_advanced.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_industry.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_crm_advanced.php';

$cfg = isset($GLOBALS['DP_Config']) ? $GLOBALS['DP_Config'] : new DP_Config();

try {
    $pdo = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
        $cfg->user,
        $cfg->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
} catch (PDOException $e) {
    http_response_code(500);
    exit(json_encode(array('status' => false, 'message' => 'DB connect failed')));
}

$steps = array();

// 0. Shared key/value settings store (some ERP schema reads depend on it).
try {
    epc_erp_adv_settings_ensure($pdo);
    $steps['settings'] = 'ok';
} catch (Exception $e) {
    $steps['settings'] = 'error: ' . $e->getMessage();
}

// 1. Core ERP + inventory.
try {
    if (function_exists('epc_erp_full_ensure_schema')) {
        epc_erp_full_ensure_schema($pdo);
    }
    epc_erp_inventory_ensure_schema($pdo);
    $steps['erp_inventory'] = 'ok';
} catch (Exception $e) {
    $steps['erp_inventory'] = 'error: ' . $e->getMessage();
}

// 2. CRM.
try {
    epc_crm_ensure_schema($pdo);
    $steps['crm'] = 'ok';
} catch (Exception $e) {
    $steps['crm'] = 'error: ' . $e->getMessage();
}

// 3. Worldwide tax toolkit.
$taxMode = (string) ($_GET['tax'] ?? $_POST['tax'] ?? 'defaults');
$taxToolkit = __DIR__ . '/content/shop/finance/epc_tax_toolkit.php';
try {
    if (is_file($taxToolkit)) {
        require_once $taxToolkit;
        if (function_exists('epc_tax_toolkit_ensure_schema')) {
            epc_tax_toolkit_ensure_schema($pdo);
        }
        if ($taxMode === 'all' && function_exists('epc_tax_toolkit_install_all_kits')) {
            $n = epc_tax_toolkit_install_all_kits($pdo, 0);
            $steps['tax'] = 'ok (installed all kits: ' . (int) $n . ')';
        } elseif (function_exists('epc_tax_toolkit_install_defaults')) {
            epc_tax_toolkit_install_defaults($pdo, 0);
            $steps['tax'] = 'ok (default kits installed)';
        } elseif (function_exists('epc_tax_toolkit_seed_kits')) {
            $n = epc_tax_toolkit_seed_kits($pdo);
            $steps['tax'] = 'ok (seeded ' . (int) $n . ' kits)';
        } else {
            $steps['tax'] = 'schema-only (no installer found)';
        }
    } else {
        $steps['tax'] = 'toolkit not present';
    }
} catch (Exception $e) {
    $steps['tax'] = 'error: ' . $e->getMessage();
}

// 4. Industry foundation.
try {
    epc_erp_industry_ensure_schema($pdo);
    $steps['industry_schema'] = 'ok';
    $industry = (string) ($_GET['industry'] ?? $_POST['industry'] ?? '');
    if ($industry !== '') {
        $applied = epc_erp_industry_apply($pdo, $industry, 0);
        $steps['industry_applied'] = $applied;
    }
} catch (Exception $e) {
    $steps['industry_schema'] = 'error: ' . $e->getMessage();
}

// 5. Register the advanced guide CP page.
try {
    $guide = epc_erp_adv_register_guides($pdo, (string) $cfg->backend_dir);
    $steps['guide'] = $guide;
} catch (Exception $e) {
    $steps['guide'] = 'error: ' . $e->getMessage();
}

$backend = '/' . trim((string) $cfg->backend_dir, '/');

echo json_encode(array(
    'status' => true,
    'message' => 'Advanced ERP setup complete',
    'steps' => $steps,
    'industries_available' => epc_erp_industry_keys(),
    'links' => array(
        'erp' => $backend . '/shop/finance/erp',
        'inventory' => $backend . '/shop/finance/erp?tab=inventory',
        'crm' => $backend . '/shop/finance/crm',
        'tax_toolkit' => $backend . '/shop/finance/tax-toolkit',
        'advanced_guide' => $backend . '/shop/finance/erp/advanced-guide',
    ),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
