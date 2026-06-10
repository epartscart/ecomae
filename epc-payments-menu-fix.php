<?php
/**
 * Fix Payment gateways CP menu: remove duplicate items, restore guide link.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/payments/epc_payment_helpers.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_payment_lang_seed($pdo);

$before = $pdo->query(
	"SELECT `id`, `caption`, `url`, `items_group`, `order` FROM `control_items`
	 WHERE `url` LIKE '%/shop/payments/%' OR `url` LIKE '%platezhnye-sistemy%'
	 ORDER BY `items_group`, `order`, `id`"
)->fetchAll(PDO::FETCH_ASSOC);

$payMenu = epc_cp_payments_menu_apply($pdo);
$removed = epc_cp_payments_menu_cleanup($pdo, $payMenu['payments_group'], $payMenu['items']);

$shopGroup = (int)$pdo->query("SELECT `id` FROM `control_groups` WHERE `caption` = '744' LIMIT 1")->fetchColumn();
if ($shopGroup <= 0) {
	$shop = epc_cp_mm_find_shop_group($pdo);
	$shopGroup = (int)$shop['id'];
}
if ($shopGroup > 0) {
	$pdo->prepare(
		"UPDATE `control_items` SET `items_group` = ?, `caption` = '3320', `url` = '/<backend>/shop/finance/platezhnye-sistemy', `show_anyway` = 0
		 WHERE `url` LIKE '%platezhnye-sistemy%'"
	)->execute(array($shopGroup));
}

$pdo->exec("UPDATE `control_items` SET `show_anyway` = 1 WHERE `id` IN (" . (int)$payMenu['items']['payments_hub'] . ',' . (int)$payMenu['items']['payments_guide'] . ")");

$after = $pdo->prepare('SELECT `id`, `caption`, `url`, `items_group`, `order` FROM `control_items` WHERE `items_group` = ? ORDER BY `order`, `id`');
$after->execute(array((int)$payMenu['payments_group']));
$menuItems = $after->fetchAll(PDO::FETCH_ASSOC);

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'message' => 'Payment gateways menu fixed — hub + guide only',
	'duplicates_removed' => $removed,
	'payments_group' => (int)$payMenu['payments_group'],
	'menu_items' => $menuItems,
	'before' => $before,
	'urls' => array(
		'hub' => $base . '/' . $cfg->backend_dir . '/shop/payments/payments',
		'guide' => $base . '/' . $cfg->backend_dir . '/shop/payments/payments/guide',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
