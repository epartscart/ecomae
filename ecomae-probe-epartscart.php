<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
echo "db={$DP_Config->db} user={$DP_Config->user} pass_len=" . strlen($DP_Config->password) . "\n";
echo "domain_path={$DP_Config->domain_path}\n";
$dh = parse_url($DP_Config->domain_path, PHP_URL_HOST);
echo "license_host_match=" . ($dh === 'www.epartscart.com' ? 'yes' : "no ({$dh})") . "\n";
$core = file_get_contents(__DIR__ . '/core/dp_core.php');
echo 'dp_core_alias_fix=' . (strpos($core, 'epcAliasHost') !== false ? 'yes' : 'no') . "\n";
try {
	$pdo = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	echo "pdo=ok tables=" . $pdo->query('SHOW TABLES')->rowCount() . "\n";
	$tpl = (int) $pdo->query('SELECT COUNT(*) FROM `templates` WHERE `current` = 1 AND `is_frontend` = 1')->fetchColumn();
	echo "frontend_templates={$tpl}\n";
	require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';
	$artExpr = docpart_sql_article_normalized_expr('`article`');
	$st = $pdo->prepare("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE {$artExpr} = ? AND UPPER(TRIM(`manufacturer`)) = 'TOYOTA'");
	$st->execute(array('1780131090'));
	echo "toyota_1780131090_rows=" . $st->fetchColumn() . "\n";
	$st2 = $pdo->prepare("SELECT DISTINCT UPPER(TRIM(`manufacturer`)) AS m, COUNT(*) c FROM `shop_docpart_prices_data` WHERE {$artExpr} = ? GROUP BY m ORDER BY c DESC LIMIT 10");
	$st2->execute(array('1780131090'));
	echo "manufacturers_for_article:\n";
	while ($r = $st2->fetch(PDO::FETCH_ASSOC)) {
		echo "  " . $r['m'] . " count=" . $r['c'] . "\n";
	}
	$st3 = $pdo->query("SELECT `manufacturer`, `article`, `exist`, `price`, `price_id` FROM `shop_docpart_prices_data` WHERE {$artExpr} = '1780131090' AND IFNULL(`exist`,0) > 0 AND IFNULL(`price`,0) > 0 LIMIT 5");
	echo "stock_rows_any_brand:\n";
	while ($r = $st3->fetch(PDO::FETCH_ASSOC)) {
		echo "  {$r['manufacturer']} {$r['article']} exist={$r['exist']} price={$r['price']} price_id={$r['price_id']}\n";
	}
	$total = (int) $pdo->query('SELECT COUNT(*) FROM `shop_docpart_prices_data`')->fetchColumn();
	$priced = (int) $pdo->query('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE IFNULL(`price`,0) > 0')->fetchColumn();
	echo "prices_data_total={$total} priced={$priced}\n";
	$like = $pdo->query("SELECT `manufacturer`, `article`, `exist`, `price` FROM `shop_docpart_prices_data` WHERE `article` LIKE '%1780131090%' OR `article` LIKE '%17801-31090%' LIMIT 10");
	echo "like_1780131090:\n";
	while ($r = $like->fetch(PDO::FETCH_ASSOC)) {
		echo "  {$r['manufacturer']} {$r['article']} exist={$r['exist']} price={$r['price']}\n";
	}
	$all = $pdo->query('SELECT `manufacturer`, `article`, `exist`, `price`, `price_id` FROM `shop_docpart_prices_data` ORDER BY `id` LIMIT 20');
	echo "all_rows_sample:\n";
	while ($r = $all->fetch(PDO::FETCH_ASSOC)) {
		echo "  {$r['manufacturer']} {$r['article']} exist={$r['exist']} price={$r['price']} price_id={$r['price_id']}\n";
	}
} catch (Exception $e) {
	echo 'pdo=fail ' . $e->getMessage() . "\n";
}

foreach (array('epartscart', 'docpart') as $dbName) {
	try {
		$legacy = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8;connect_timeout=3',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
		);
		$cnt = (int) $legacy->query('SELECT COUNT(*) FROM `shop_docpart_prices_data`')->fetchColumn();
		echo "db_{$dbName}_prices_data_rows={$cnt}\n";
		if ($cnt > 0 && isset($artExpr)) {
			$stL = $legacy->prepare("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE {$artExpr} = ?");
			$stL->execute(array('1780131090'));
			echo "db_{$dbName}_toyota_1780131090=" . $stL->fetchColumn() . "\n";
		}
	} catch (Exception $e) {
		echo "db_{$dbName}=unavailable " . $e->getMessage() . "\n";
	}
}
