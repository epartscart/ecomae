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

section('Fleet + print');
check('list_proofs_fleet exists', function_exists('epc_bc_bos_list_proofs_fleet'));
check('fleet_stats exists', function_exists('epc_bc_bos_fleet_stats'));
check('verify absolute uses https host fallback', strpos(epc_bc_bos_verify_url_absolute('prf_x'), 'epc-blockchain-verify.php?proof=prf_x') !== false);
$hub = (string)file_get_contents($root . '/cp/content/shop/tenant_hub/tenant_hub_main.php');
check('tenant hub has blockchain tab', strpos($hub, "tab=blockchain") !== false && strpos($hub, "tab === 'blockchain'") !== false);
$panel = $root . '/content/shop/tenant_hub/epc_tenant_blockchain_panel.php';
check('fleet panel file exists', is_file($panel));
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

echo "\n----------------------------\n";
echo "Passed: $pass_count  Failed: $fail_count\n";
exit($fail_count > 0 ? 1 : 0);
