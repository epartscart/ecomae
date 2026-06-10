<?php
/**
 * Verify warehouse stock for a brand/article on epartscart tenant DB.
 * https://www.epartscart.com/epc-epartscart-stock-probe.php?token=...&brand=toyota&article=1780131090
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = $_SERVER['HTTP_HOST'] ?? 'www.epartscart.com';

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

$brand = trim((string) ($_GET['brand'] ?? 'toyota'));
$article = trim((string) ($_GET['article'] ?? '1780131090'));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

echo "=== epartscart stock probe ===\n";
echo "host=" . epc_portal_host() . " db={$cfg->db} user={$cfg->user}\n";
echo "brand={$brand} article={$article}\n\n";

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=5',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 5)
	);
} catch (Exception $e) {
	exit('connect=fail ' . $e->getMessage() . "\n");
}

$artNorm = docpart_normalize_article_for_price($article);
$artExpr = docpart_sql_article_normalized_expr('`article`');
echo "article_norm={$artNorm}\n\n";

$st = $pdo->prepare(
	"SELECT `manufacturer`, `article`, `article_show`, `exist`, `price`, `price_id`
	 FROM `shop_docpart_prices_data`
	 WHERE {$artExpr} = ? AND UPPER(TRIM(`manufacturer`)) = UPPER(?)
	 ORDER BY `exist` DESC, `price` ASC"
);
$st->execute(array($article, $brand));
$rows = $st->fetchAll(PDO::FETCH_ASSOC);
echo 'brand_match_rows=' . count($rows) . "\n";
foreach (array_slice($rows, 0, 10) as $r) {
	echo sprintf(
		"  %s %s exist=%s price=%s price_id=%s\n",
		$r['manufacturer'],
		$r['article_show'] ?: $r['article'],
		$r['exist'],
		$r['price'],
		$r['price_id']
	);
}

$st2 = $pdo->prepare(
	"SELECT `manufacturer`, `article`, `exist`, `price`, `price_id`
	 FROM `shop_docpart_prices_data`
	 WHERE {$artExpr} = ?
	 ORDER BY `exist` DESC, `price` ASC LIMIT 10"
);
$st2->execute(array($article));
$any = $st2->fetchAll(PDO::FETCH_ASSOC);
echo "\nany_brand_rows=" . count($any) . "\n";
foreach ($any as $r) {
	echo sprintf(
		"  %s %s exist=%s price=%s price_id=%s\n",
		$r['manufacturer'],
		$r['article'],
		$r['exist'],
		$r['price'],
		$r['price_id']
	);
}

$tpl = (int) $pdo->query('SELECT COUNT(*) FROM `templates` WHERE `current` = 1 AND `is_frontend` = 1')->fetchColumn();
echo "\nfrontend_templates={$tpl}\n";
