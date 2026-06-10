<?php
header('Content-Type: text/plain; charset=utf-8');
$checks = array(
	'epc_cp_translate.php' => __DIR__ . '/content/general_pages/epc_cp_translate.php',
	'desktop.php' => __DIR__ . '/cp/templates/bootstrap_admin/desktop.php',
	'erp_desktop.php' => __DIR__ . '/cp/templates/bootstrap_admin/erp_desktop.php',
	'industry_settings.php' => __DIR__ . '/cp/content/control/portal/industry_settings.php',
);
foreach ($checks as $label => $path) {
	$ok = is_file($path);
	$snippet = $ok ? substr(file_get_contents($path), 0, 50000) : '';
	echo $label . ': ' . ($ok ? 'OK' : 'MISSING');
	if ($label === 'epc_cp_translate.php') {
		echo ' fn=' . (strpos($snippet, 'function epc_cp_translate_render') !== false ? 'yes' : 'no');
	}
	if ($label === 'desktop.php') {
		echo ' render=' . (strpos($snippet, 'epc_cp_translate_render') !== false ? 'yes' : 'no');
	}
	if ($label === 'erp_desktop.php') {
		echo ' render=' . (strpos($snippet, "epc_cp_translate_render('erp')") !== false ? 'yes' : 'no');
	}
	if ($label === 'industry_settings.php') {
		echo ' dropdown=' . (strpos($snippet, 'cp_default_lang') !== false ? 'yes' : 'no');
	}
	echo "\n";
}
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
try {
	$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
	epc_portal_db_ensure($pdo);
	$st = $pdo->query("SHOW COLUMNS FROM `epc_portal_site_settings` LIKE 'cp_default_lang'");
	$row = $st ? $st->fetch(PDO::FETCH_ASSOC) : false;
	echo 'db_column_cp_default_lang: ' . ($row ? 'OK' : 'MISSING') . "\n";
} catch (Exception $e) {
	echo 'db_column_cp_default_lang: ERROR ' . $e->getMessage() . "\n";
}
echo 'host=' . ($_SERVER['HTTP_HOST'] ?? '') . "\n";
