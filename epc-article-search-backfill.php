<?php
/**
 * Backfill shop_docpart_prices_data.article_search (offline; not on click path).
 *
 *   /epc-article-search-backfill.php?token=epartscart-deploy-2026&apply=1
 *   optional: &price_id=3 &chunk=5000 &max_chunks=20
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
@set_time_limit(300);
@ini_set('memory_limit', '512M');

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

$DP_Config = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$pdo->query('SET NAMES utf8;');

$apply = isset($_GET['apply']) && (string) $_GET['apply'] === '1';
$priceId = isset($_GET['price_id']) ? (int) $_GET['price_id'] : 0;
$chunk = isset($_GET['chunk']) ? max(100, min(10000, (int) $_GET['chunk'])) : 5000;
$maxChunks = isset($_GET['max_chunks']) ? max(1, min(100, (int) $_GET['max_chunks'])) : 20;

$report = array(
	'ok' => true,
	'apply' => $apply,
	'price_id' => $priceId,
	'chunk' => $chunk,
	'backfilled' => 0,
	'empty_before' => 0,
	'empty_after' => null,
	'chunks' => 0,
);

$countSql = "SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE (`article_search` = '' OR `article_search` IS NULL)"
	. ($priceId > 0 ? ' AND `price_id` = ' . $priceId : '');
$report['empty_before'] = (int) $pdo->query($countSql)->fetchColumn();

if ($apply) {
	$expr = docpart_sql_article_normalized_expr('`article`');
	$total = 0;
	for ($i = 0; $i < $maxChunks; $i++) {
		$idSql = 'SELECT `id` FROM `shop_docpart_prices_data`
			WHERE (`article_search` = \'\' OR `article_search` IS NULL)'
			. ($priceId > 0 ? ' AND `price_id` = ' . $priceId : '')
			. ' ORDER BY `id` ASC LIMIT ' . (int) $chunk;
		$ids = $pdo->query($idSql)->fetchAll(PDO::FETCH_COLUMN);
		if (!$ids) {
			break;
		}
		$ph = implode(',', array_fill(0, count($ids), '?'));
		$st = $pdo->prepare(
			'UPDATE `shop_docpart_prices_data` SET `article_search` = ' . $expr . ' WHERE `id` IN (' . $ph . ')'
		);
		$st->execute(array_map('intval', $ids));
		$total += count($ids);
		$report['chunks']++;
	}
	$report['backfilled'] = $total;
	$report['empty_after'] = (int) $pdo->query($countSql)->fetchColumn();
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
exit;
