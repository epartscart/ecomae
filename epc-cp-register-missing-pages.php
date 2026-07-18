<?php
/**
 * Register missing Super CP CMS pages into the live site DB (docpart),
 * and report status. Platform DB registration alone is not enough — CP routes
 * resolve content from DP_Config->db.
 *
 * https://www.ecomae.com/epc-cp-register-missing-pages.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_integrations_helpers.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
echo 'site_db=' . $cfg->db . "\n";

$pages = array(
	array('epc_power_bi', 'epc_portal_power_bi', 'Power BI', 'Power BI', 'epc_power_bi.php', 10),
	array('epc_power_bi_guide', 'epc_portal_power_bi_guide', 'Power BI guide', 'Руководство Power BI', 'epc_power_bi_guide.php', 11),
	array('epc_industry_license_trends', 'epc_portal_industry_license_trends', 'Industry license trends', 'Тренды лицензий', 'epc_industry_license_trends.php', 12),
);

foreach ($pages as $p) {
	list($slug, $lang, $en, $ru, $file, $order) = $p;
	$abs = __DIR__ . '/cp/content/control/portal/' . $file;
	echo $slug . ' file=' . (is_file($abs) ? 'yes' : 'MISSING');
	$id = epc_integrations_register_cp_content($pdo, $slug, $lang, $en, $ru, $file, $order);
	$st = $pdo->prepare('SELECT `id`,`url`,`published_flag`,`content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array('control/portal/' . $slug));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	echo ' content_id=' . $id . ' row=' . json_encode($row, JSON_UNESCAPED_SLASHES) . "\n";
}

// Soft-check other broken modules
foreach (array(
	'cp/content/shop/returns/router.php',
	'content/shop/geo/dp_geo_node_record.php',
	'content/shop/catalogue/dp_category_record.php',
) as $rel) {
	echo 'dep ' . $rel . ' ' . (is_file(__DIR__ . '/' . $rel) ? 'yes' : 'MISSING') . "\n";
}
echo "Done.\n";
