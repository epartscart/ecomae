<?php
/**
 * One-off verify: Custom Shipping declaration PDF row (id=8) + live handler markers.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_custom_shipping.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_access.php';

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
$pdo = null;
$tenantLabel = 'platform';
if ($siteKey !== '') {
	require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';
	$platDb = '';
	$platUser = '';
	$platPass = '';
	$cfgFile = __DIR__ . '/config.local.php';
	if (!is_file($cfgFile)) {
		$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	}
	if (is_file($cfgFile)) {
		$epc_config_local = null;
		include $cfgFile;
		$platDb = (string) ($epc_config_local['db'] ?? '');
		$platUser = (string) ($epc_config_local['user'] ?? '');
		$platPass = (string) ($epc_config_local['password'] ?? '');
	}
	if ($platDb === '' || $platUser === '') {
		$platCfg = new DP_Config();
		$platDb = (string) $platCfg->db;
		$platUser = (string) $platCfg->user;
		$platPass = (string) $platCfg->password;
	}
	$platPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8',
		$platUser,
		$platPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$row = epc_portal_shared_erp_load_by_site_key($siteKey, $platPdo);
	if (!$row) {
		echo "tenant_not_found: {$siteKey}\n";
		exit(1);
	}
	$pdo = epc_portal_shared_erp_tenant_pdo($row);
	$tenantLabel = $siteKey . ' (' . ($row['db_name'] ?? '') . ')';
}
if (!$pdo instanceof PDO) {
	$cfg = new DP_Config();
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}
echo "tenant: {$tenantLabel}\n";
$_SERVER['HTTP_HOST'] = $_SERVER['HTTP_HOST'] ?? 'www.ecomae.com';
echo "ajax_endpoint: " . epc_erp_resolve_ajax_endpoint('/cp/') . "\n";
echo "form_js: " . (is_file(__DIR__ . '/content/shop/finance/epc_custom_shipping_form.js') ? 'yes' : 'no') . "\n";
echo "ajax_proxy: " . (is_file(__DIR__ . '/content/general_pages/ajax_epc_erp.php') ? 'yes' : 'no') . "\n";

epc_cs_ensure_schema($pdo);
epc_cs_ensure_box_schema($pdo);

$cnt = (int) $pdo->query('SELECT COUNT(*) FROM `epc_custom_shipping_declarations`')->fetchColumn();
$maxId = (int) $pdo->query('SELECT COALESCE(MAX(`id`), 0) FROM `epc_custom_shipping_declarations`')->fetchColumn();
echo "declarations_total: {$cnt}\n";
echo "declarations_max_id: {$maxId}\n";

$withPdf = (int) $pdo->query("SELECT COUNT(*) FROM `epc_custom_shipping_declarations` WHERE COALESCE(`pdf_file_path`, '') <> ''")->fetchColumn();
echo "declarations_with_pdf: {$withPdf}\n";

$recent = $pdo->query('SELECT `id`, `pdf_file_path`, `pdf_file_name` FROM `epc_custom_shipping_declarations` ORDER BY `id` DESC LIMIT 5')->fetchAll(PDO::FETCH_ASSOC);
echo "recent_declarations:\n";
foreach ($recent as $r) {
	echo '  #' . (int) $r['id'] . ' pdf=' . ($r['pdf_file_path'] ?: '(none)') . "\n";
}

$id = (int) ($_GET['id'] ?? 8);
$st = $pdo->prepare('SELECT `id`, `pdf_file_path`, `pdf_file_name`, `declaration_number`, `company` FROM `epc_custom_shipping_declarations` WHERE `id` = ? LIMIT 1');
$st->execute(array($id));
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
	echo "declaration id={$id}: NOT FOUND\n\n";
} else {

echo "declaration id={$id}\n";
echo "  number: " . ($row['declaration_number'] ?? '') . "\n";
echo "  company: " . ($row['company'] ?? '') . "\n";
echo "  pdf_file_path: " . ($row['pdf_file_path'] ?? '(null)') . "\n";
echo "  pdf_file_name: " . ($row['pdf_file_name'] ?? '(null)') . "\n";

$rel = trim((string) ($row['pdf_file_path'] ?? ''));
$public = $rel !== '' ? epc_cs_pdf_public_url($rel) : '';
echo "  public_url: {$public}\n";

if ($rel !== '') {
	$full = rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/\\') . '/' . ltrim(str_replace('\\', '/', $rel), '/');
	echo "  disk_path: {$full}\n";
	echo "  file_exists: " . (is_file($full) ? 'yes' : 'no') . "\n";
	if (is_file($full)) {
		echo "  file_size: " . filesize($full) . "\n";
	}
}

echo "\n";
}

$tabPaths = array(
	__DIR__ . '/cp/content/shop/finance/erp/erp_tabs_custom_shipping.php',
	__DIR__ . '/cp/platform-erp/content/shop/finance/erp/erp_tabs_custom_shipping.php',
);
foreach ($tabPaths as $tabFile) {
	if (!is_file($tabFile)) {
		echo "tab_file: {$tabFile} (missing)\n";
		continue;
	}
	$src = (string) file_get_contents($tabFile);
	echo "tab_file: {$tabFile}\n";
	echo "  has_epcCsBindPdfViewButtons: " . (strpos($src, 'epcCsBindPdfViewButtons') !== false ? 'yes' : 'no') . "\n";
	echo "  has_epcCsOpenPdfViewer: " . (strpos($src, 'epcCsOpenPdfViewer') !== false ? 'yes' : 'no') . "\n";
	echo "  has_document_pdf_delegation: " . (strpos($src, "closest('.epc-cs-pdf-view-btn')") !== false ? 'yes' : 'no') . "\n";
	echo "  has_modal_display_flex: " . (strpos($src, "modal.style.display = 'flex'") !== false ? 'yes' : 'no') . "\n";
	echo "  has_external_form_js: " . (strpos($src, 'epc_custom_shipping_form.js') !== false ? 'yes' : 'no') . "\n";
	echo "  has_form_boot_json: " . (strpos($src, 'epc_cs_form_boot') !== false ? 'yes' : 'no') . "\n";
	echo "  has_inline_pdf_fetch: " . (strpos($src, "fd.append('action', 'cs_import_declaration_pdf')") !== false ? 'yes' : 'no') . "\n";
}
if (!is_file($tabPaths[0]) && !is_file($tabPaths[1])) {
	echo "tab_file: NOT FOUND in expected paths\n";
}

$coreFile = __DIR__ . '/content/shop/finance/epc_custom_shipping.php';
if (is_file($coreFile)) {
	$core = (string) file_get_contents($coreFile);
	echo "core_file: {$coreFile}\n";
	echo "  has_epc_cs_declaration_row_actions_html: " . (strpos($core, 'epc_cs_declaration_row_actions_html') !== false ? 'yes' : 'no') . "\n";
	echo "  has_epc-cs-pdf-view-btn: " . (strpos($core, 'epc-cs-pdf-view-btn') !== false ? 'yes' : 'no') . "\n";
	echo "  has_epc_cs_pdf_file_exists: " . (strpos($core, 'epc_cs_pdf_file_exists') !== false ? 'yes' : 'no') . "\n";
}
