<?php
/**
 * Fix Shop management (pickup points): content paths + verify CP PHP files on ecomae.
 * https://www.ecomae.com/epc-ecomae-shop-offices-fix.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$routes = array(
	'shop/logistics/offices' => '/<backend_dir>/content/shop/logistics/offices.php',
	'shop/logistics/offices/office' => '/<backend_dir>/content/shop/logistics/office.php',
	'shop/logistics/offices/office/storages_link' => '/<backend_dir>/content/shop/logistics/office_storages_link.php',
	'shop/logistics/offices/office/geo_nodes' => '/<backend_dir>/content/shop/logistics/office_geo_nodes.php',
);

$upd = $pdo->prepare(
	'UPDATE `content` SET `content_type` = \'php\', `content` = ?, `published_flag` = 1, `is_frontend` = 0 WHERE `url` = ? AND `is_frontend` = 0'
);

echo "database: {$cfg->db}\n";
echo "host: " . ($_SERVER['HTTP_HOST'] ?? '') . "\n\n";

foreach ($routes as $url => $phpPath) {
	$upd->execute(array($phpPath, $url));
	$n = $upd->rowCount();
	$st = $pdo->prepare('SELECT `id`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$disk = '';
	if ($row) {
		$disk = str_replace('<backend_dir>', $cfg->backend_dir, $_SERVER['DOCUMENT_ROOT'] . (string) $row['content']);
	}
	echo "{$url}: rows_updated={$n} id=" . (int) ($row['id'] ?? 0) . " file=" . (is_file($disk) ? 'yes' : 'MISSING') . "\n";
	if ($disk !== '' && !is_file($disk)) {
		echo "  expected: {$disk}\n";
	}
}

$backend = $cfg->backend_dir;
$testUrl = 'https://www.ecomae.com/' . $backend . '/shop/logistics/offices';
echo "\nTest URL: {$testUrl}\n";
echo "Done.\n";
