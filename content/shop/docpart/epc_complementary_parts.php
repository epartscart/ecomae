<?php
/**
 * Rule-based complementary / related parts suggestions (cross-ref + name cluster).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
$cross_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_cross_interchange.php';
if (is_file($cross_path)) {
	require_once $cross_path;
}

function epc_complementary_normalize_key(string $brand, string $article): string
{
	return strtoupper(trim($brand) . '|' . docpart_normalize_article_for_price($article));
}

function epc_complementary_suggest_for_article(PDO $db, string $brand, string $article, int $limit = 6): array
{
	$limit = max(1, min(12, $limit));
	$article_norm = docpart_normalize_article_for_price($article);
	if ($article_norm === '') {
		return array();
	}
	$exclude = array(epc_complementary_normalize_key($brand, $article) => true);
	$out = array();
	if (function_exists('docpart_load_interchange_partners')) {
		$partners = docpart_load_interchange_partners($db, $article_norm, 2, 40);
		foreach ($partners as $p) {
			$key = epc_complementary_normalize_key((string)($p['brand'] ?? ''), (string)($p['article'] ?? ''));
			if ($key === '|' || isset($exclude[$key])) {
				continue;
			}
			$exclude[$key] = true;
			$out[] = array(
				'brand' => trim((string)($p['brand'] ?? '')),
				'article' => trim((string)($p['article'] ?? '')),
				'label' => trim((string)($p['brand'] ?? '') . ' ' . (string)($p['article'] ?? '')),
				'reason' => 'Cross reference',
			);
			if (count($out) >= $limit) {
				return $out;
			}
		}
	}
	if (count($out) < $limit && function_exists('docpart_cross_refs_from_price_name_cluster')) {
		$cluster = docpart_cross_refs_from_price_name_cluster($db, $article_norm, $limit * 2);
		foreach ($cluster as $row) {
			$key = epc_complementary_normalize_key((string)($row['brand'] ?? ''), (string)($row['article'] ?? ''));
			if ($key === '|' || isset($exclude[$key])) {
				continue;
			}
			$exclude[$key] = true;
			$out[] = array(
				'brand' => trim((string)($row['brand'] ?? '')),
				'article' => trim((string)($row['article'] ?? '')),
				'label' => trim((string)($row['brand'] ?? '') . ' ' . (string)($row['article'] ?? '')),
				'reason' => 'Often ordered together',
			);
			if (count($out) >= $limit) {
				break;
			}
		}
	}
	return $out;
}

function epc_complementary_suggest_for_cart(PDO $db, int $user_id, int $session_id, int $limit = 8): array
{
	$limit = max(1, min(16, $limit));
	$st = $db->prepare('SELECT `t2_manufacturer`, `t2_article` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ? AND `checked_for_order` = 1 ORDER BY `id` DESC LIMIT 6;');
	$st->execute(array($user_id, $session_id));
	$cart_keys = array();
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$cart_keys[epc_complementary_normalize_key((string)$row['t2_manufacturer'], (string)$row['t2_article'])] = true;
	}
	$out = array();
	$seen = array();
	$stAll = $db->prepare('SELECT `t2_manufacturer`, `t2_article` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ? AND `checked_for_order` = 1 ORDER BY `id` ASC LIMIT 6;');
	$stAll->execute(array($user_id, $session_id));
	while ($row = $stAll->fetch(PDO::FETCH_ASSOC)) {
		$suggestions = epc_complementary_suggest_for_article($db, (string)$row['t2_manufacturer'], (string)$row['t2_article'], 4);
		foreach ($suggestions as $s) {
			$key = epc_complementary_normalize_key($s['brand'], $s['article']);
			if ($key === '|' || isset($cart_keys[$key]) || isset($seen[$key])) {
				continue;
			}
			$seen[$key] = true;
			$out[] = $s;
			if (count($out) >= $limit) {
				return $out;
			}
		}
	}
	return $out;
}

function epc_complementary_search_url(string $brand, string $article): string
{
	global $multilang_params;
	$lang = is_array($multilang_params ?? null) ? (string)($multilang_params['lang_href'] ?? '') : '';
	$url = $lang . '/shop/part_search?article=' . rawurlencode($article);
	if ($brand !== '') {
		$url .= '&brend=' . rawurlencode($brand);
	}
	return $url;
}

function epc_complementary_render_html(array $suggestions, string $heading = 'Related parts you may also need'): string
{
	if (empty($suggestions)) {
		return '';
	}
	$html = '<div class="epc-complementary-panel alert alert-info" style="margin:16px 0;">';
	$html .= '<h4 style="margin:0 0 10px;font-size:16px;"><i class="fa fa-puzzle-piece"></i> ' . htmlspecialchars($heading, ENT_QUOTES, 'UTF-8') . '</h4>';
	$html .= '<p class="text-muted" style="margin:0 0 10px;font-size:13px;">Based on cross references and catalogue clusters — verify fitment before ordering.</p>';
	$html .= '<div class="epc-complementary-list" style="display:flex;flex-wrap:wrap;gap:8px;">';
	foreach ($suggestions as $s) {
		$href = epc_complementary_search_url((string)$s['brand'], (string)$s['article']);
		$label = trim((string)($s['label'] ?? ''));
		if ($label === '') {
			continue;
		}
		$html .= '<a class="btn btn-sm btn-default" href="' . htmlspecialchars($href, ENT_QUOTES, 'UTF-8') . '" style="margin:0;">'
			. htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</a>';
	}
	$html .= '</div></div>';
	return $html;
}
