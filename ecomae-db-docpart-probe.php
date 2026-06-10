<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/content/general_pages/epc_portal.php';
$doc = epc_portal_docpart_config();
echo "docpart db={$doc->db} user={$doc->user} pass_len=" . strlen($doc->password) . "\n";
try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=' . $doc->db, $doc->user, $doc->password);
	echo 'docpart connect=ok tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo 'docpart connect=fail ' . $e->getMessage() . "\n";
}

$epc_config_local = null;
require __DIR__ . '/config.local.php';
$pass = (string) ($epc_config_local['password'] ?? '');
try {
	$pdo2 = new PDO('mysql:host=127.0.0.1;dbname=ecomae', 'ecomae', $pass);
	echo 'ecomae connect=ok tables=' . $pdo2->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo 'ecomae connect=fail ' . $e->getMessage() . "\n";
}

define('_ASTEXE_', 1);
$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
require __DIR__ . '/config.php';
$cfg = new DP_Config();
epc_portal_apply_config($cfg);
echo "epartscart applied db={$cfg->db} user={$cfg->user} pass_len=" . strlen($cfg->password) . "\n";
$cleaner = __DIR__ . '/cp/content/shop/prices_upload/for_pyprices/pyprices_tables_cleaner.php';
echo 'pyprices_tables_cleaner=' . (is_file($cleaner) ? 'yes' : 'MISSING') . "\n";
try {
	$pdo3 = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db, $cfg->user, $cfg->password);
	echo 'epartscart applied connect=ok tables=' . $pdo3->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo 'epartscart applied connect=fail ' . $e->getMessage() . "\n";
}
