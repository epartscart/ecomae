<?php
/**
 * EPC Auto Price AI — product copy enrichment (rule-based MVP + optional OpenAI).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_auto_price_engine.php';
require_once __DIR__ . '/epc_industry_taxonomy.php';

function epc_apai_clean_title(string $raw): string
{
	$title = html_entity_decode(strip_tags($raw), ENT_QUOTES, 'UTF-8');
	$title = preg_replace('/\s*[\|\-–—]\s*(Sharaf DG|Noon|Amazon\.ae|Jumbo|UAE|Buy Online).*$/i', '', $title);
	$title = preg_replace('/\s+/', ' ', trim($title));
	if (strlen($title) > 120) {
		$title = substr($title, 0, 117) . '…';
	}
	return $title !== '' ? $title : 'Product';
}

function epc_apai_clean_description(string $raw, string $title = ''): string
{
	$desc = html_entity_decode(strip_tags($raw), ENT_QUOTES, 'UTF-8');
	$desc = preg_replace('/\s+/', ' ', trim($desc));
	if ($desc === '' && $title !== '') {
		$desc = 'Premium ' . $title . ' — available for fast delivery across the UAE.';
	}
	if (strlen($desc) > 500) {
		$desc = substr($desc, 0, 497) . '…';
	}
	return $desc;
}

function epc_apai_suggest_taxonomy(PDO $pdo, string $industryKey, string $title, string $description = ''): ?array
{
	$text = strtolower($title . ' ' . $description);
	$keywords = array(
		'auto_parts' => array(
			'filter' => 'auto-engine-filters-oil', 'brake' => 'auto-brakes-pads', 'pad' => 'auto-brakes-pads',
			'spark' => 'auto-engine-spark', 'belt' => 'auto-engine-belts', 'shock' => 'auto-brakes-shocks',
			'light' => 'auto-body-lights', 'mirror' => 'auto-body-mirrors', 'mat' => 'auto-interior-mats',
		),
		'electronics' => array(
			'iphone' => 'cell-phones-unlocked-iphone', 'samsung' => 'cell-phones-unlocked-android',
			'phone' => 'cell-phones', 'tablet' => 'computers-tablets', 'laptop' => 'computers-laptops',
			'headphone' => 'headphones', 'earbud' => 'headphones-earbuds', 'tv' => 'tv-video-televisions',
			'playstation' => 'gaming-consoles-playstation', 'xbox' => 'gaming-consoles-xbox',
		),
		'fashion' => array(
			'men' => 'fashion-men-shirts', 'shirt' => 'fashion-men-shirts', 'dress' => 'fashion-women-dresses',
			'abaya' => 'fashion-women-dresses', 'kid' => 'fashion-kids', 'bag' => 'fashion-accessories-bags',
			'sunglass' => 'fashion-accessories-sunglasses', 'shoe' => 'fashion-men-footwear',
		),
		'jewellery' => array(
			'ring' => 'jewellery-rings-gold', 'gold' => 'jewellery-rings-gold-22k', 'diamond' => 'jewellery-rings-diamond',
			'necklace' => 'jewellery-necklaces-pendant', 'watch' => 'jewellery-watches-luxury',
			'earring' => 'jewellery-earrings-studs', 'bracelet' => 'jewellery-bracelets',
		),
		'general_retail' => array(
			'coffee' => 'retail-home-appliances-coffee', 'blender' => 'retail-home-appliances-blenders',
			'office' => 'retail-office-supplies', 'vitamin' => 'retail-health-vitamins',
			'gift' => 'retail-gifts-hampers', 'decor' => 'retail-home-decor',
		),
	);
	$map = $keywords[$industryKey] ?? $keywords['general_retail'];
	foreach ($map as $kw => $slug) {
		if (strpos($text, $kw) !== false) {
			return epc_apai_tax_by_slug($pdo, $industryKey, $slug);
		}
	}
	$slugs = epc_apai_demo_slugs_for_industry($industryKey);
	return epc_apai_tax_by_slug($pdo, $industryKey, $slugs[0] ?? '');
}

function epc_apai_openai_enrich(string $title, string $description, array $config): ?array
{
	$key = trim((string) ($config['openai_key'] ?? ''));
	if ($key === '') {
		return null;
	}
	$prompt = "Clean this product for an e-commerce catalogue in UAE. Return JSON with keys: title (max 80 chars), description (SEO, 2 sentences), keywords (comma-separated).\n\nTitle: {$title}\nDescription: {$description}";
	$payload = json_encode(array(
		'model' => 'gpt-4o-mini',
		'messages' => array(array('role' => 'user', 'content' => $prompt)),
		'temperature' => 0.3,
		'max_tokens' => 300,
	));
	if (!function_exists('curl_init')) {
		return null;
	}
	$ch = curl_init('https://api.openai.com/v1/chat/completions');
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_POST => true,
		CURLOPT_HTTPHEADER => array('Content-Type: application/json', 'Authorization: Bearer ' . $key),
		CURLOPT_POSTFIELDS => $payload,
		CURLOPT_TIMEOUT => 30,
	));
	$body = (string) curl_exec($ch);
	curl_close($ch);
	$data = json_decode($body, true);
	$content = (string) ($data['choices'][0]['message']['content'] ?? '');
	$parsed = json_decode($content, true);
	return is_array($parsed) ? $parsed : null;
}

/**
 * @return array{title:string,description:string,taxonomy_node_id:int,sell_price:float,margin_pct:float,cost:float,source:string}
 */
function epc_apai_enrich_product(PDO $pdo, string $siteKey, array $input): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$rawTitle = trim((string) ($input['title'] ?? ''));
	$rawDesc = trim((string) ($input['description'] ?? ''));
	$url = trim((string) ($input['source_url'] ?? ''));
	$cost = (float) ($input['cost_estimate'] ?? 0);
	$price = (float) ($input['price'] ?? $input['suggested_price'] ?? 0);

	if ($url !== '' && $rawTitle === '') {
		$fetched = epc_disc_fetch_url($url);
		if (!empty($fetched['ok'])) {
			$rawTitle = (string) ($fetched['title'] ?? '');
			$rawDesc = (string) ($fetched['description'] ?? '');
			if ($price <= 0) {
				$price = (float) ($fetched['price'] ?? 0);
			}
		}
	}

	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	$source = 'rule_based';

	$ai = epc_apai_openai_enrich($rawTitle, $rawDesc, $config);
	if ($ai) {
		$title = epc_apai_clean_title((string) ($ai['title'] ?? $rawTitle));
		$description = epc_apai_clean_description((string) ($ai['description'] ?? $rawDesc), $title);
		$source = 'openai';
	} else {
		$title = epc_apai_clean_title($rawTitle);
		$description = epc_apai_clean_description($rawDesc, $title);
	}

	$taxNode = epc_apai_suggest_taxonomy($pdo, $industryKey, $title, $description);
	$taxId = $taxNode ? (int) $taxNode['id'] : 0;

	if ($cost <= 0 && $price > 0) {
		$cost = round($price * 0.82, 2);
	}
	$margin = epc_auto_price_apply_margin($pdo, $cost, $siteKey);
	$sellPrice = $price > 0 ? $price : (float) $margin['sell_price'];
	$marginPct = $cost > 0 ? round((($sellPrice - $cost) / $cost) * 100, 2) : (float) $margin['margin_pct'];

	return array(
		'title' => $title,
		'description' => $description,
		'taxonomy_node_id' => $taxId,
		'sell_price' => $sellPrice,
		'margin_pct' => $marginPct,
		'cost' => $cost,
		'suggested_price' => $price > 0 ? $price : $sellPrice,
		'source' => $source,
		'industry_key' => $industryKey,
	);
}
