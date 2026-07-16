<?php
/**
 * Smoke tests for Blockchain BOS Enterprise proof layer (no live MySQL required
 * for pure crypto/Merkle paths).
 *
 *   php tests/erp_advanced/run_blockchain_bos_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
    http_response_code(403);
    exit('CLI only');
}

define('_ASTEXE_', 1);
$root = dirname(__DIR__, 2);
require_once $root . '/content/general_pages/epc_blockchain_bos.php';

$pass_count = 0;
$fail_count = 0;

function check(string $label, bool $cond): void
{
    global $pass_count, $fail_count;
    if ($cond) {
        $pass_count++;
        echo "  PASS  $label\n";
    } else {
        $fail_count++;
        echo "  FAIL  $label\n";
    }
}

function section(string $t): void
{
    echo "\n== $t ==\n";
}

section('Product branding helpers');
check('product name long', epc_bc_bos_product_name() === 'Blockchain BOS Enterprise System');
check('product name short', epc_bc_bos_product_name(true) === 'Blockchain BOS');
check('tagline mentions unified', strpos(epc_bc_bos_product_tagline(), 'unified') !== false);

section('Modes');
check('normalize anchor', epc_bc_bos_normalize_mode('anchor') === 'anchor');
check('normalize junk -> off', epc_bc_bos_normalize_mode('xyz') === 'off');
check('modes include network', isset(epc_bc_bos_modes()['network']));

section('Canonical hash stability');
$a = ['b' => 2, 'a' => 1, 'nested' => ['z' => 9, 'y' => 8]];
$b = ['a' => 1, 'nested' => ['y' => 8, 'z' => 9], 'b' => 2];
$h1 = epc_bc_bos_hash($a);
$h2 = epc_bc_bos_hash($b);
check('sorted keys same hash', $h1 === $h2 && strlen($h1) === 64);
check('different payload different hash', epc_bc_bos_hash(['x' => 1]) !== epc_bc_bos_hash(['x' => 2]));

section('Merkle root + path');
$leaves = [
    hash('sha256', 'leaf0'),
    hash('sha256', 'leaf1'),
    hash('sha256', 'leaf2'),
];
$merkleRoot = epc_bc_bos_merkle_root($leaves);
check('root is 64 hex', (bool)preg_match('/^[a-f0-9]{64}$/', $merkleRoot));
$path0 = epc_bc_bos_merkle_proof_path($leaves, 0);
check('path verifies for leaf0', epc_bc_bos_verify_merkle_path($leaves[0], $path0, $merkleRoot));
$path1 = epc_bc_bos_merkle_proof_path($leaves, 1);
check('path verifies for leaf1', epc_bc_bos_verify_merkle_path($leaves[1], $path1, $merkleRoot));
check('tampered leaf fails', !epc_bc_bos_verify_merkle_path(hash('sha256', 'nope'), $path0, $merkleRoot));

section('Job wiring');
require_once $root . '/content/general_pages/epc_platform_jobs.php';
$jobsSrc = (string)file_get_contents($root . '/content/general_pages/epc_platform_jobs.php');
check('jobs dispatch knows blockchain_anchor_batch', strpos($jobsSrc, 'blockchain_anchor_batch') !== false);

section('Public verify entrypoint');
$verify = $root . '/epc-blockchain-verify.php';
check('verify endpoint exists', is_file($verify));
$vsrc = (string)file_get_contents($verify);
check('verify uses epc_blockchain_bos', strpos($vsrc, 'epc_blockchain_bos.php') !== false);

section('Schema + marketing wiring');
$dbSrc = (string)file_get_contents($root . '/content/general_pages/epc_portal_db.php');
check('tenant column blockchain_mode', strpos($dbSrc, 'blockchain_mode') !== false);

$hero = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_platform_layout.php');
check('hero says Blockchain BOS Enterprise', strpos($hero, 'Blockchain BOS Enterprise System') !== false);
check('hero not ONE BOS legacy', strpos($hero, 'ONE BOS</span>') === false);

$home = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_home_sections.php');
check('home has Blockchain Proof Layer', strpos($home, 'Blockchain Proof Layer') !== false);
check('home unified enterprise messaging', strpos($home, 'One Blockchain BOS Enterprise System') !== false);
check('home post-hero blockchain section', strpos($home, 'id="blockchain-proof"') !== false && strpos($home, 'ehm-bc-graphic') !== false);
check('home blockchain graphic has Merkle + flow', strpos($home, 'ehm-bc-merkle') !== false && strpos($home, 'ehm-bc-flow') !== false);
$homeCss = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_home_sections.css');
check('home css styles blockchain graphic', strpos($homeCss, '.ehm-bc-graphic') !== false && strpos($homeCss, '@keyframes ehm-bc-scan') !== false);

$mkt = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_marketing_content.php');
check('docs overview rebranded', strpos($mkt, 'Blockchain BOS Enterprise System') !== false);

$docs = $root . '/docs/BLOCKCHAIN_BOS_ENTERPRISE.md';
check('docs exist', is_file($docs));

section('Document auto-hooks');
check('maybe_record helper exists', function_exists('epc_bc_bos_maybe_record_document'));
check('resolve_site_key exists', function_exists('epc_bc_bos_resolve_site_key'));
$skip = epc_bc_bos_maybe_record_document('invoice', 'INV-TEST', ['x' => 1]);
check('maybe_record skips without tenant', !empty($skip['skipped']) || empty($skip['ok']));

$invSrc = (string)file_get_contents($root . '/content/shop/finance/epc_einvoice.php');
check('invoice save hooks maybe_record', strpos($invSrc, 'epc_bc_bos_maybe_record_document') !== false);
check('credit note hooks maybe_record', substr_count($invSrc, 'epc_bc_bos_maybe_record_document') >= 2);

$grnSrc = (string)file_get_contents($root . '/content/shop/finance/epc_erp_inventory.php');
check('GRN receive hooks maybe_record', strpos($grnSrc, "epc_bc_bos_maybe_record_document") !== false && strpos($grnSrc, "'grn'") !== false);

$asSrc = (string)file_get_contents($root . '/content/shop/finance/epc_erp_aftersales.php');
check('aftersales RMA hooks maybe_record', strpos($asSrc, 'epc_bc_bos_maybe_record_document') !== false);

$wSrc = (string)file_get_contents($root . '/content/shop/finance/epc_warranty_rma.php');
check('warranty RMA hooks maybe_record', strpos($wSrc, 'epc_bc_bos_maybe_record_document') !== false);

$vsrc = (string)file_get_contents($root . '/epc-blockchain-verify.php');
check('verify UI has HTML form', strpos($vsrc, '<form') !== false && strpos($vsrc, 'Verify a business proof') !== false);

section('Operator UI helpers');
check('lookup_proof exists', function_exists('epc_bc_bos_lookup_proof'));
check('list_proofs exists', function_exists('epc_bc_bos_list_proofs'));
check('badge html empty without proof', epc_bc_bos_proof_badge_html(null) === '');
$badge = epc_bc_bos_proof_badge_html([
    'status' => 'anchored',
    'proof_uid' => 'prf_test_ui_1',
]);
check('badge shows anchored + verify', strpos($badge, 'Blockchain anchored') !== false && strpos($badge, 'epc-blockchain-verify.php') !== false);
list($t, $id) = epc_bc_bos_einvoice_record_keys([
    'doc_category' => 'tax_invoice',
    'invoice_type_code' => '380',
    'invoice_number' => 'INV-9',
    'id' => 9,
]);
check('einvoice keys invoice', $t === 'invoice' && $id === 'INV-9');
list($t2, $id2) = epc_bc_bos_einvoice_record_keys([
    'doc_category' => 'tax_credit_note',
    'invoice_type_code' => '381',
    'invoice_number' => 'CN-1',
]);
check('einvoice keys credit_note', $t2 === 'credit_note' && $id2 === 'CN-1');

$tabFile = $root . '/cp/content/shop/finance/erp/erp_tabs_blockchain_proofs.php';
check('ERP proofs tab exists', is_file($tabFile));
$nav = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_nav_areas.php');
check('nav includes blockchain_proofs', strpos($nav, "'blockchain_proofs'") !== false);
$main = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_main.php');
check('main maps blockchain_proofs tab', strpos($main, "'blockchain_proofs' => 'erp_tabs_blockchain_proofs.php'") !== false);
$einvUi = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_tabs_einvoice.php');
check('einvoice view shows proof badge', strpos($einvUi, 'epc_bc_bos_document_badge_html') !== false);
$invUi = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_tabs_invoices.php');
check('invoices view shows proof badge', strpos($invUi, 'epc_bc_bos_document_badge_html') !== false);

section('GRN / RMA document UX');
check('grn_record_id helper', function_exists('epc_bc_bos_grn_record_id'));
check('grn_record_id from invoice no', epc_bc_bos_grn_record_id(['id' => 9, 'invoice_number' => 'SUP-1']) === 'PINV-SUP-1');
check('grn_record_id fallback id', epc_bc_bos_grn_record_id(['id' => 9, 'invoice_number' => '']) === 'PINV-9');
check('grn_badge_html helper', function_exists('epc_bc_bos_grn_badge_html'));
check('grn badge empty without receipt', epc_bc_bos_grn_badge_html(['id' => 1, 'invoice_number' => 'X']) === '');
$purchUi = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_main.php');
check('purchases tab has GRN blockchain column', strpos($purchUi, 'epc_bc_bos_grn_badge_html') !== false && strpos($purchUi, 'Blockchain BOS GRN proof') !== false);
$asTab = $root . '/cp/content/shop/finance/erp/erp_tabs_aftersales.php';
check('aftersales RMA tab exists', is_file($asTab));
$asTabSrc = (string)file_get_contents($asTab);
check('aftersales tab shows RMA badge', strpos($asTabSrc, "epc_bc_bos_document_badge_html('rma'") !== false);
check('main maps aftersales tab', strpos($purchUi, "'aftersales' => 'erp_tabs_aftersales.php'") !== false);
$nav = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_nav_areas.php');
check('nav includes aftersales', strpos($nav, "'aftersales'") !== false);
$ajax = (string)file_get_contents($root . '/cp/content/shop/finance/erp/ajax_erp.php');
check('ajax as_rma_create action', strpos($ajax, "case 'as_rma_create'") !== false);
check('ajax RMA verify flash', strpos($ajax, 'Blockchain RMA proof') !== false);
check('as_rma_list helper', function_exists('epc_as_rma_list') || strpos((string)file_get_contents($root . '/content/shop/finance/epc_erp_aftersales.php'), 'function epc_as_rma_list') !== false);
check('as_rma_get helper', strpos((string)file_get_contents($root . '/content/shop/finance/epc_erp_aftersales.php'), 'function epc_as_rma_get') !== false);
$asSrc2 = (string)file_get_contents($root . '/content/shop/finance/epc_erp_aftersales.php');
check('RMA create uses stable rma_no for proof', strpos($asSrc2, "Prefer stable RMA-{id}") !== false || strpos($asSrc2, "RMA-' . \$rmaId") !== false);

section('Fleet + print');
check('list_proofs_fleet exists', function_exists('epc_bc_bos_list_proofs_fleet'));
check('fleet_stats exists', function_exists('epc_bc_bos_fleet_stats'));
check('verify absolute uses https host fallback', strpos(epc_bc_bos_verify_url_absolute('prf_x'), 'epc-blockchain-verify.php?proof=prf_x') !== false);
$hub = (string)file_get_contents($root . '/cp/content/shop/tenant_hub/tenant_hub_main.php');
check('tenant hub has blockchain tab', strpos($hub, "tab=blockchain") !== false && strpos($hub, "tab === 'blockchain'") !== false);
check('tenant hub posts bc mode', strpos($hub, 'epc_th_bc_mode') !== false);
check('tenant hub posts anchor now', strpos($hub, 'epc_th_bc_anchor_now') !== false);
$panel = $root . '/content/shop/tenant_hub/epc_tenant_blockchain_panel.php';
check('fleet panel file exists', is_file($panel));
$panelSrc = (string)file_get_contents($panel);
check('fleet panel has mode controls', strpos($panelSrc, 'epc_th_bc_mode') !== false && strpos($panelSrc, 'Tenant blockchain modes') !== false);
check('fleet panel has anchor now', strpos($panelSrc, 'epc_th_bc_anchor_now') !== false && strpos($panelSrc, 'Anchor pending now') !== false);
check('fleet panel shows anchor network', strpos($panelSrc, 'epc_bc_bos_anchor_network') !== false);
$helpers = (string)file_get_contents($root . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php');
check('helper update blockchain mode', strpos($helpers, 'function epc_th_update_tenant_blockchain_mode') !== false);
check('helper anchor pending now', strpos($helpers, 'function epc_th_anchor_blockchain_pending_now') !== false);
check('anchor_network helper', function_exists('epc_bc_bos_anchor_network') && epc_bc_bos_anchor_network() !== '');
check('clear mode cache exists', function_exists('epc_bc_bos_clear_tenant_mode_cache'));
$printSrc = (string)file_get_contents($root . '/content/shop/finance/epc_erp_invoices.php');
check('print html includes blockchain proof block', strpos($printSrc, 'bc-proof') !== false && strpos($printSrc, 'epc_bc_bos_verify_url_absolute') !== false);

section('Marketing /blockchain page');
$routerSrc = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_platform_router.php');
check('router matches /blockchain', strpos($routerSrc, "\$path === '/blockchain'") !== false);
check('sitemap includes /blockchain', strpos($routerSrc, "array('/blockchain'") !== false);
$pageFile = $root . '/content/general_pages/epc_ecomae_blockchain_page.php';
check('blockchain page file exists', is_file($pageFile));
$cssFile = $root . '/content/general_pages/epc_ecomae_blockchain_3d.css';
$jsFile = $root . '/content/general_pages/epc_ecomae_blockchain_3d.js';
check('blockchain 3d css exists', is_file($cssFile));
check('blockchain 3d js exists', is_file($jsFile));
$pageSrc = (string)file_get_contents($pageFile);
check('page has structure + process sections', strpos($pageSrc, 'id="structure"') !== false && strpos($pageSrc, 'id="process"') !== false);
check('page mentions Merkle and verify', strpos($pageSrc, 'Merkle') !== false && strpos($pageSrc, 'epc-blockchain-verify.php') !== false);
check('3d js targets ebc-page', strpos((string)file_get_contents($jsFile), '#ebc-page.ebc-page--3d') !== false);
$pagesSrc = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_platform_pages.php');
check('platform pages require blockchain page', strpos($pagesSrc, 'epc_ecomae_blockchain_page.php') !== false);
$navSrc = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_platform_data.php');
check('nav links to /blockchain', strpos($navSrc, "'blockchain'") !== false && strpos($navSrc, 'blockchain') !== false);
$footerSrc = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_platform_layout.php');
check('footer links to blockchain', strpos($footerSrc, 'blockchain">Blockchain BOS') !== false);
$homeSrc = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_home_sections.php');
check('home links How it works to /blockchain', strpos($homeSrc, 'href="/blockchain"') !== false);

section('Completeness — staff allowlist + GRN flash + dashboard');
$staffSrc = (string)file_get_contents($root . '/content/shop/finance/epc_erp_staff.php');
check('staff all_tabs includes aftersales', strpos($staffSrc, "'aftersales'") !== false);
check('staff all_tabs includes blockchain_proofs', strpos($staffSrc, "'blockchain_proofs'") !== false);
check('finance dept has blockchain_proofs', (bool)preg_match("/'finance'\\s*=>\\s*array\\([\\s\\S]*?'blockchain_proofs'/", $staffSrc));
check('sales dept has aftersales', (bool)preg_match("/'sales'\\s*=>\\s*array\\([\\s\\S]*?'aftersales'/", $staffSrc));
$uiCfg = (string)file_get_contents($root . '/content/shop/finance/epc_erp_ui.php');
check('ui config has aftersales tab', strpos($uiCfg, "'aftersales'") !== false);

check('grn_flash_for_purchase helper', function_exists('epc_bc_bos_grn_flash_for_purchase'));
$emptyFlash = epc_bc_bos_grn_flash_for_purchase(['id' => 1, 'invoice_number' => 'X']);
check('grn flash empty without receipt', ($emptyFlash['message'] ?? 'x') === '' && empty($emptyFlash['extra']));
check('tenant_proof_stats helper', function_exists('epc_bc_bos_tenant_proof_stats'));
$stats = epc_bc_bos_tenant_proof_stats('');
check('tenant_proof_stats shape', isset($stats['total'], $stats['anchored'], $stats['pending'], $stats['mode']));

$ajaxErp = (string)file_get_contents($root . '/cp/content/shop/finance/erp/ajax_erp.php');
check('erp create_purchase uses grn flash helper', strpos($ajaxErp, 'epc_bc_bos_grn_flash_for_purchase') !== false);
check('erp purchase_from_order GRN flash', strpos($ajaxErp, "case 'purchase_from_order'") !== false
	&& strpos($ajaxErp, 'epc_bc_bos_grn_flash_for_purchase') !== false
	&& strpos($ajaxErp, 'inventory_receipt_posted') !== false);
$ajaxProc = (string)file_get_contents($root . '/cp/content/shop/procurement/ajax_procurement.php');
check('procurement create_purchase GRN flash', strpos($ajaxProc, 'epc_bc_bos_grn_flash_for_purchase') !== false);
check('procurement purchase_from_order GRN flash', strpos($ajaxProc, "case 'purchase_from_order'") !== false
	&& substr_count($ajaxProc, 'epc_bc_bos_grn_flash_for_purchase') >= 2);

$asTabSrc2 = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_tabs_aftersales.php');
check('aftersales warranty RMA section', strpos($asTabSrc2, 'Warranty RMAs') !== false && strpos($asTabSrc2, 'epc_rma_list') !== false);
$dashSrc = (string)file_get_contents($root . '/cp/content/shop/finance/erp/erp_dashboard.php');
check('dashboard proof KPI strip', strpos($dashSrc, 'epc_bc_bos_tenant_proof_stats') !== false && strpos($dashSrc, 'Blockchain proofs') !== false);
$vsrc2 = (string)file_get_contents($root . '/epc-blockchain-verify.php');
check('verify links to /blockchain', strpos($vsrc2, '/blockchain') !== false);
$pageSrc2 = (string)file_get_contents($root . '/content/general_pages/epc_ecomae_blockchain_page.php');
check('marketing page mentions After-sales + dashboard', strpos($pageSrc2, 'After-sales') !== false && strpos($pageSrc2, 'dashboard') !== false);
$docsSrc = (string)file_get_contents($root . '/docs/BLOCKCHAIN_BOS_ENTERPRISE.md');
check('docs phase complete anchor mode', strpos($docsSrc, 'Phase complete (anchor mode)') !== false);
check('docs mention purchase_from_order', strpos($docsSrc, 'purchase_from_order') !== false);
check('grn badge accepts inventory_receipt_posted', strpos((string)file_get_contents($root . '/content/general_pages/epc_blockchain_bos.php'), 'inventory_receipt_posted') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
