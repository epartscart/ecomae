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

// 4b. Module entitlements + per-module schema (only enabled modules built).
//
//   &bundle=einvoice_only|payroll_only|customs_logistics|pos_retail|
//           finance_suite|supply_chain|manufacturing_pack|full
//   &exclusive=1   disable every module not in the bundle (clean per-client run)
//
// If no entitlement rows exist yet and no bundle is given, default policy is
// everything-on (so an existing full tenant keeps working). A bundle makes the
// tenant pick-and-choose (e.g. an e-invoice-only client).
require_once __DIR__ . '/content/shop/finance/epc_erp_modules.php';
try {
    epc_mod_ensure_schema($pdo);
    $bundle = (string) ($_GET['bundle'] ?? $_POST['bundle'] ?? '');
    if ($bundle !== '') {
        $exclusive = (string) ($_GET['exclusive'] ?? $_POST['exclusive'] ?? '') === '1';
        $enabled = epc_mod_apply_bundle($pdo, $bundle, $exclusive);
        $steps['entitlements'] = 'ok (bundle ' . $bundle . ($exclusive ? ', exclusive' : '') . ': ' . count($enabled) . ' modules)';
    } else {
        $steps['entitlements'] = 'ok (no bundle; default policy applies)';
    }
} catch (Exception $e) {
    $steps['entitlements'] = 'error: ' . $e->getMessage();
}

// Map: module code => [relative file, schema function]. Only the schema of an
// ENABLED module is created, so a payroll-only tenant never gets SCM tables.
$moduleSchema = array(
    'currency' => array('content/shop/finance/epc_erp_currency.php', 'epc_ccy_ensure_schema'),
    'credit' => array('content/shop/finance/epc_erp_credit.php', 'epc_credit_ensure_schema'),
    'procurement' => array('content/shop/finance/epc_erp_scm.php', 'epc_scm_ensure_schema'),
    'customs' => array('content/shop/finance/epc_erp_customs.php', 'epc_cust_ensure_schema'),
    'manufacturing' => array('content/shop/finance/epc_erp_manufacturing.php', 'epc_mfg_ensure_schema'),
    'aftersales' => array('content/shop/finance/epc_erp_aftersales.php', 'epc_as_ensure_schema'),
    'asset_maint' => array('content/shop/finance/epc_erp_costing.php', 'epc_eam_ensure_schema'),
    'cost_accounting' => array('content/shop/finance/epc_erp_costing.php', 'epc_cc_ensure_schema'),
    'rebate' => array('content/shop/finance/epc_erp_costing.php', 'epc_rbt_ensure_schema'),
    'org' => array('content/shop/finance/epc_erp_org.php', 'epc_org_ensure_schema'),
    'vouchers' => array('content/shop/finance/epc_erp_vouchers.php', 'epc_erp_vouchers_ensure_schema'),
    'closing' => array('content/shop/finance/epc_erp_closing.php', 'epc_fy_ensure_schema'),
    'integration' => array('content/shop/finance/epc_erp_integration.php', 'epc_int_ensure_schema'),
    'process_flow' => array('content/shop/finance/epc_erp_process_flows.php', 'epc_flow_ensure_schema'),
    'hr' => array('content/shop/finance/epc_erp_hr.php', 'epc_hr_ensure_schema'),
    'projects' => array('content/shop/finance/epc_erp_projects.php', 'epc_prj_ensure_schema'),
    'pricing' => array('content/shop/finance/epc_erp_pricing.php', 'epc_price_ensure_schema'),
    'migration' => array('content/shop/finance/epc_erp_migration.php', 'epc_mig_ensure_schema'),
    'roles' => array('content/shop/finance/epc_erp_governance.php', 'epc_gov_ensure_schema'),
    'compliance' => array('content/shop/finance/epc_erp_compliance.php', 'epc_cmp_ensure_schema'),
    'workflow' => array('content/shop/finance/epc_erp_collab.php', 'epc_collab_ensure_schema'),
    'ecommerce' => array('content/shop/finance/epc_erp_ecommerce.php', 'epc_ec_ensure_schema'),
);
$built = array();
$skipped = array();
foreach ($moduleSchema as $code => $spec) {
    list($relFile, $fn) = $spec;
    try {
        if (!epc_mod_enabled($pdo, $code, true)) {
            $skipped[] = $code;
            continue;
        }
        $abs = __DIR__ . '/' . $relFile;
        if (is_file($abs)) {
            require_once $abs;
        }
        if (function_exists($fn)) {
            $fn($pdo);
            $built[] = $code;
        }
    } catch (Exception $e) {
        $steps['schema_' . $code] = 'error: ' . $e->getMessage();
    }
}
$steps['module_schema'] = 'built: ' . implode(',', $built) . ($skipped ? ' | skipped(disabled): ' . implode(',', $skipped) : '');

// 4c. Seed the UAE (FTA) compliance baseline when compliance is enabled.
try {
    if (epc_mod_enabled($pdo, 'compliance', true) && function_exists('epc_cmp_seed_uae')) {
        epc_cmp_seed_uae($pdo);
        $steps['compliance_seed'] = 'ok (UAE FTA baseline)';
    }
} catch (Exception $e) {
    $steps['compliance_seed'] = 'error: ' . $e->getMessage();
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
