<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$GLOBALS['DP_Config'] = $cfg;

$pdo = epc_portal_platform_pdo();
echo "=== POS route check ===\n";
$st = $pdo->prepare('SELECT id, url, content, published_flag FROM content WHERE url = ? LIMIT 1');
$st->execute(array('shop/pos/terminal'));
$row = $st->fetch(PDO::FETCH_ASSOC);
print_r($row);

$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db, $cfg->user, $cfg->password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$GLOBALS['db_link'] = $db;

require_once __DIR__ . '/content/users/dp_user.php';
$GLOBALS['user_session'] = array('user_id' => 1, 'csrf_guard_key' => 'probe');
echo "user_session=mock\n";

echo "\n--- direct include terminal ---\n";
ob_start();
try {
	include __DIR__ . '/cp/content/shop/pos/epc_pos_terminal.php';
	$html = ob_get_clean();
	echo 'bytes=' . strlen($html) . ' pos_wrap=' . (stripos($html, 'epc-pos-wrap') !== false ? 'yes' : 'no') . "\n";
	if (stripos($html, 'Fatal') !== false) {
		echo substr($html, 0, 1500) . "\n";
	}
} catch (Throwable $e) {
	ob_end_clean();
	echo 'FAIL: ' . $e->getMessage() . "\n";
}
