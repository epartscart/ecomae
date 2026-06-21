<?php
/**
 * Auto Price AI — source prices storage + storefront market price block.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_auto_price_engine.php';

function epc_apai_source_prices_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_product_source_prices` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`product_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`discovery_queue_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`source_domain` VARCHAR(120) NOT NULL DEFAULT \'\',
			`source_url` VARCHAR(512) NOT NULL DEFAULT \'\',
			`price` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`specs_json` TEXT NULL,
			`fetched_at` INT NOT NULL DEFAULT 0,
			`is_primary` TINYINT(1) NOT NULL DEFAULT 0,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			KEY `site_product` (`site_key`, `product_id`),
			KEY `queue_id` (`discovery_queue_id`),
			KEY `source_domain` (`source_domain`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

/**
 * Storefront policy: never expose cross-market buy/sell source hints to customers (CP keeps APAI data).
 */
function epc_apai_storefront_hide_market_sourcing(): bool
{
	return true;
}

function epc_apai_show_market_prices_enabled(PDO $pdo, string $siteKey): bool
{
	if (function_exists('epc_apai_storefront_hide_market_sourcing') && epc_apai_storefront_hide_market_sourcing()) {
		return false;
	}
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	$config = (array) ($cfg['config'] ?? array());
	if (array_key_exists('show_market_prices_on_frontend', $config)) {
		return !empty($config['show_market_prices_on_frontend']);
	}
	return ((string) ($cfg['profile'] ?? '')) === 'marketplace_arbitrage';
}

function epc_apai_resolve_storefront_site_key(): string
{
	$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$map = array(
		'electronicae' => 'electronicae',
		'epartscart' => 'epartscart',
		'stylenlook' => 'stylenlook',
		'thejewellerytrend' => 'thejewellerytrend',
		'taxofinca' => 'taxofinca',
	);
	foreach ($map as $needle => $key) {
		if (strpos($host, $needle) !== false) {
			return $key;
		}
	}
	if (function_exists('epc_portal_site_key_from_hostname') && $host !== '') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
		return preg_replace('/[^a-z0-9_]/', '', strtolower(epc_portal_site_key_from_hostname($host)));
	}
	return '';
}

function epc_apai_source_price_save(PDO $pdo, string $siteKey, array $row): int
{
	epc_apai_source_prices_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$productId = max(0, (int) ($row['product_id'] ?? 0));
	$queueId = max(0, (int) ($row['discovery_queue_id'] ?? 0));
	$domain = strtolower(preg_replace('/^www\./', '', trim((string) ($row['source_domain'] ?? ''))));
	$url = trim((string) ($row['source_url'] ?? ''));
	$now = time();
	$specs = (array) ($row['specs'] ?? array());
	if (isset($row['specs_json']) && is_string($row['specs_json'])) {
		$decoded = json_decode($row['specs_json'], true);
		if (is_array($decoded)) {
			$specs = $decoded;
		}
	}

	$chk = $pdo->prepare(
		'SELECT `id` FROM `epc_product_source_prices`
		 WHERE `site_key` = ? AND `product_id` = ? AND `source_domain` = ? AND `source_url` = ? LIMIT 1'
	);
	$chk->execute(array($siteKey, $productId, $domain, $url));
	$id = (int) $chk->fetchColumn();

	$params = array(
		$siteKey, $productId, $queueId, $domain, $url,
		(float) ($row['price'] ?? 0),
		strtoupper(substr((string) ($row['currency'] ?? 'AED'), 0, 8)),
		json_encode($specs, JSON_UNESCAPED_UNICODE),
		(int) ($row['fetched_at'] ?? $now),
		!empty($row['is_primary']) ? 1 : 0,
		$now,
	);

	if ($id > 0) {
		$params[] = $id;
		$pdo->prepare(
			'UPDATE `epc_product_source_prices`
			 SET `discovery_queue_id`=?, `source_domain`=?, `source_url`=?, `price`=?, `currency`=?, `specs_json`=?, `fetched_at`=?, `is_primary`=?, `updated_at`=?
			 WHERE `id`=?'
		)->execute(array($queueId, $domain, $url, $params[5], $params[6], $params[7], $params[8], $params[9], $now, $id));
		return $id;
	}

	$params[] = $now;
	$pdo->prepare(
		'INSERT INTO `epc_product_source_prices`
		 (`site_key`, `product_id`, `discovery_queue_id`, `source_domain`, `source_url`, `price`, `currency`, `specs_json`, `fetched_at`, `is_primary`, `created_at`, `updated_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute($params);
	return (int) $pdo->lastInsertId();
}

function epc_apai_product_source_prices(PDO $pdo, string $siteKey, int $productId): array
{
	epc_apai_source_prices_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($productId <= 0) {
		return array();
	}
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_source_prices` WHERE `site_key` = ? AND `product_id` = ? AND `price` > 0 ORDER BY `price` ASC, `is_primary` DESC'
	);
	$stmt->execute(array($siteKey, $productId));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	foreach ($rows as &$row) {
		$row['specs'] = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($row['specs'])) {
			$row['specs'] = array();
		}
	}
	unset($row);
	return $rows;
}

function epc_apai_product_has_discovery_import(PDO $pdo, string $siteKey, int $productId): bool
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($productId <= 0) {
		return false;
	}
	$stmt = $pdo->prepare(
		'SELECT `id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `product_id` = ? AND `status` = \'imported\' LIMIT 1'
	);
	$stmt->execute(array($siteKey, $productId));
	return (int) $stmt->fetchColumn() > 0;
}

/**
 * Seed alternate UAE source prices for demo/imported products.
 *
 * @param array<int,array<string,mixed>> $sources
 */
function epc_apai_seed_alternate_source_prices(PDO $pdo, string $siteKey, int $productId, int $queueId, float $basePrice, array $specs, array $sources, string $primaryDomain = ''): int
{
	$added = 0;
	require_once __DIR__ . '/epc_industry_taxonomy.php';
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$domains = epc_apai_ae_sources_for_industry($industryKey);
	$offsets = array(0, 0.03, -0.02, 0.05, -0.04, 0.01);
	$i = 0;
	foreach ($domains as $src) {
		$domain = (string) ($src['domain'] ?? '');
		if ($domain === '' || ($primaryDomain !== '' && $domain === $primaryDomain)) {
			continue;
		}
		$existing = null;
		foreach ($sources as $s) {
			if ((string) ($s['source_domain'] ?? '') === $domain) {
				$existing = $s;
				break;
			}
		}
		$price = $existing ? (float) ($existing['price'] ?? 0) : round($basePrice * (1 + ($offsets[$i % count($offsets)] ?? 0)), 2);
		if ($price <= 0) {
			continue;
		}
		epc_apai_source_price_save($pdo, $siteKey, array(
			'product_id' => $productId,
			'discovery_queue_id' => $queueId,
			'source_domain' => $domain,
			'source_url' => 'https://www.' . $domain . '/search?q=' . urlencode('product'),
			'price' => $price,
			'currency' => 'AED',
			'specs' => $specs,
			'fetched_at' => time(),
			'is_primary' => 0,
		));
		$added++;
		$i++;
		if ($i >= 4) {
			break;
		}
	}
	return $added;
}

function epc_apai_imported_compare_matrix(PDO $pdo, string $siteKey): array
{
	epc_apai_source_prices_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$rules = epc_ape_rules_get($pdo, $siteKey);
	$minMargin = (float) ($rules['min_margin_percent'] ?? 15);

	$stmt = $pdo->prepare(
		'SELECT sp.*, scp.`caption` AS `product_title`, scp.`id` AS `catalogue_id`
		 FROM `epc_product_source_prices` sp
		 LEFT JOIN `shop_catalogue_products` scp ON scp.`id` = sp.`product_id`
		 WHERE sp.`site_key` = ? AND sp.`product_id` > 0
		 ORDER BY sp.`product_id`, sp.`price` ASC'
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();

	$byProduct = array();
	foreach ($rows as $row) {
		$pid = (int) ($row['product_id'] ?? 0);
		if (!isset($byProduct[$pid])) {
			$byProduct[$pid] = array(
				'product_id' => $pid,
				'title' => (string) ($row['product_title'] ?? ''),
				'brand' => '',
				'article_number' => '',
				'brand_article_key' => '',
				'sources' => array(),
				'spec_keys' => array(),
				'spec_conflicts' => array(),
				'lowest_price' => null,
				'lowest_source' => '',
				'highest_price' => null,
				'highest_source' => '',
				'sell_price' => 0.0,
			);
		}
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
		if (function_exists('epc_apai_specs_enrich_brand_article')) {
			$specs = epc_apai_specs_enrich_brand_article($specs);
		}
		if (!empty($specs['brand']) && $byProduct[$pid]['brand'] === '') {
			$byProduct[$pid]['brand'] = (string) $specs['brand'];
		}
		if (!empty($specs['article_number']) && $byProduct[$pid]['article_number'] === '') {
			$byProduct[$pid]['article_number'] = (string) $specs['article_number'];
		}
		if (!empty($specs['brand_article_key']) && $byProduct[$pid]['brand_article_key'] === '') {
			$byProduct[$pid]['brand_article_key'] = (string) $specs['brand_article_key'];
		}
		foreach (array_keys($specs) as $k) {
			$byProduct[$pid]['spec_keys'][$k] = true;
		}
		$price = (float) ($row['price'] ?? 0);
		$byProduct[$pid]['sources'][] = array(
			'source_domain' => (string) ($row['source_domain'] ?? ''),
			'source_url' => (string) ($row['source_url'] ?? ''),
			'price' => $price,
			'currency' => (string) ($row['currency'] ?? 'AED'),
			'specs' => $specs,
			'fetched_at' => (int) ($row['fetched_at'] ?? 0),
			'is_primary' => !empty($row['is_primary']),
		);
		if ($price > 0 && ($byProduct[$pid]['lowest_price'] === null || $price < $byProduct[$pid]['lowest_price'])) {
			$byProduct[$pid]['lowest_price'] = $price;
			$byProduct[$pid]['lowest_source'] = (string) ($row['source_domain'] ?? '');
		}
		if ($price > 0 && ($byProduct[$pid]['highest_price'] === null || $price > $byProduct[$pid]['highest_price'])) {
			$byProduct[$pid]['highest_price'] = $price;
			$byProduct[$pid]['highest_source'] = (string) ($row['source_domain'] ?? '');
		}
	}

	foreach ($byProduct as &$p) {
		$p['spec_keys'] = array_keys($p['spec_keys']);
		sort($p['spec_keys']);
		$p['spec_conflicts'] = epc_apai_detect_spec_conflicts($p['sources'], $p['spec_keys']);
		if ($p['product_id'] > 0) {
			try {
				$pr = $pdo->prepare('SELECT MIN(`price`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0');
				$pr->execute(array($p['product_id']));
				$p['sell_price'] = (float) $pr->fetchColumn();
			} catch (Throwable $e) {
			}
		}
		$low = $p['lowest_price'] !== null ? (float) $p['lowest_price'] : 0.0;
		$high = $p['highest_price'] !== null ? (float) $p['highest_price'] : $low;
		$p['min_price'] = $low;
		$p['max_price'] = $high;
		$p['margin_abs'] = ($low > 0 && $high > 0) ? round($high - $low, 2) : 0.0;
		$p['margin_pct'] = ($low > 0 && $p['margin_abs'] > 0) ? round(($p['margin_abs'] / $low) * 100, 1) : 0.0;
		if ($p['sell_price'] > 0 && $low > 0) {
			$p['margin_vs_market'] = round((($p['sell_price'] - $low) / $low) * 100, 2);
			$p['meets_margin'] = $p['margin_vs_market'] >= $minMargin;
		} else {
			$p['margin_vs_market'] = null;
			$p['meets_margin'] = false;
		}
	}
	unset($p);

	$rows = array_values($byProduct);
	usort($rows, function ($a, $b) {
		$ma = (float) ($a['margin_pct'] ?? 0);
		$mb = (float) ($b['margin_pct'] ?? 0);
		if ($ma !== $mb) {
			return $mb <=> $ma;
		}
		if (function_exists('epc_apai_resolve_industry')) {
			$ka = (string) ($a['brand_article_key'] ?? '');
			$kb = (string) ($b['brand_article_key'] ?? '');
			if ($ka !== $kb) {
				return strcmp($ka, $kb);
			}
		}
		return strcmp((string) ($a['title'] ?? ''), (string) ($b['title'] ?? ''));
	});
	return $rows;
}

function epc_apai_detect_spec_conflicts(array $sources, array $specKeys): array
{
	$conflicts = array();
	$watch = array('Model', 'RAM', 'Storage', 'Color', 'Size', 'Capacity', 'Weight');
	foreach ($watch as $key) {
		if (!in_array($key, $specKeys, true)) {
			continue;
		}
		$vals = array();
		foreach ($sources as $src) {
			$specs = (array) ($src['specs'] ?? array());
			if (!empty($specs[$key])) {
				$vals[] = (string) $specs[$key];
			}
		}
		$uniq = array_unique($vals);
		if (count($uniq) > 1) {
			$conflicts[] = array('key' => $key, 'values' => array_values($uniq));
		}
	}
	return $conflicts;
}

function epc_apai_render_market_prices_block(PDO $pdo, string $siteKey, int $productId, float $ourPrice = 0): string
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($productId <= 0 || !epc_apai_show_market_prices_enabled($pdo, $siteKey)) {
		return '';
	}

	$prices = epc_apai_product_source_prices($pdo, $siteKey, $productId);
	if (count($prices) < 1) {
		return '';
	}
	if (!epc_apai_product_has_discovery_import($pdo, $siteKey, $productId)) {
		return '';
	}

	$lowest = (float) ($prices[0]['price'] ?? 0);
	$highest = $lowest;
	foreach ($prices as $pr) {
		$p = (float) ($pr['price'] ?? 0);
		if ($p > $highest) {
			$highest = $p;
		}
	}
	$rules = epc_ape_rules_get($pdo, $siteKey);
	$marginPct = (float) ($rules['min_margin_percent'] ?? 15);
	$marginOk = ($ourPrice > 0 && $lowest > 0 && (($ourPrice - $lowest) / $lowest * 100) >= $marginPct);

	ob_start();
	?>
	<div class="epc-apai-market-prices">
		<h4 class="epc-apai-market-prices__title"><i class="fa fa-line-chart" aria-hidden="true"></i> Market prices (UAE)</h4>
		<?php if ($lowest > 0 && $highest > $lowest) { ?>
		<p class="epc-apai-market-prices__range">Market range: from <strong><?php echo number_format($lowest, 2); ?></strong> to <strong><?php echo number_format($highest, 2); ?> AED</strong></p>
		<?php } ?>
		<p class="epc-apai-market-prices__hint">Prices fetched from UAE retailers when this product was imported via Auto Price AI.</p>
		<table class="table table-condensed epc-apai-market-prices__table">
			<thead>
				<tr>
					<th>Source</th>
					<th>Price</th>
					<th>Key specs</th>
				</tr>
			</thead>
			<tbody>
			<?php foreach ($prices as $row) {
				$specs = (array) ($row['specs'] ?? array());
				$specChips = array();
				foreach (array('Model', 'RAM', 'Storage', 'Color', 'Size', 'Capacity') as $sk) {
					if (!empty($specs[$sk])) {
						$specChips[] = $sk . ': ' . $specs[$sk];
					}
				}
				if (!$specChips && $specs) {
					foreach (array_slice($specs, 0, 4, true) as $k => $v) {
						$specChips[] = $k . ': ' . $v;
					}
				}
				$isLow = ((float) ($row['price'] ?? 0) === $lowest && $lowest > 0);
				$isHigh = ((float) ($row['price'] ?? 0) === $highest && $highest > $lowest);
				$rowCls = $isLow ? 'epc-apai-market-prices__lowest' : ($isHigh ? 'epc-apai-market-prices__highest' : '');
				?>
				<tr class="<?php echo $rowCls; ?>">
					<td>
						<?php if (!empty($row['source_url'])) { ?>
						<a href="<?php echo htmlspecialchars((string) $row['source_url'], ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener"><?php echo htmlspecialchars((string) $row['source_domain'], ENT_QUOTES, 'UTF-8'); ?></a>
						<?php } else { ?>
						<?php echo htmlspecialchars((string) $row['source_domain'], ENT_QUOTES, 'UTF-8'); ?>
						<?php } ?>
					</td>
					<td><strong><?php echo number_format((float) $row['price'], 2); ?> <?php echo htmlspecialchars((string) ($row['currency'] ?? 'AED'), ENT_QUOTES, 'UTF-8'); ?></strong></td>
					<td><?php echo $specChips ? htmlspecialchars(implode(' · ', $specChips), ENT_QUOTES, 'UTF-8') : '—'; ?></td>
				</tr>
			<?php } ?>
			</tbody>
		</table>
		<?php if ($ourPrice > 0 && $lowest > 0) { ?>
		<div class="epc-apai-market-prices__summary">
			<span>Our price: <strong><?php echo number_format($ourPrice, 2); ?> AED</strong></span>
			<span>Market lowest: <strong><?php echo number_format($lowest, 2); ?> AED</strong></span>
			<span class="epc-apai-market-prices__badge <?php echo $marginOk ? 'epc-apai-market-prices__badge--ok' : 'epc-apai-market-prices__badge--warn'; ?>">
				<?php echo $marginOk ? 'Margin OK' : 'Below target margin'; ?> (<?php echo number_format($marginPct, 1); ?>% target)
			</span>
		</div>
		<?php } ?>
	</div>
	<?php
	return (string) ob_get_clean();
}

/**
 * ePartsCart warehouse_supplier + auto_parts storefront (not electronicae / other tenants).
 */
function epc_apai_is_warehouse_auto_parts_storefront(PDO $pdo): bool
{
	$siteKey = epc_apai_resolve_storefront_site_key();
	if ($siteKey !== 'epartscart') {
		return false;
	}
	if (!function_exists('epc_apai_resolve_industry')) {
		require_once __DIR__ . '/epc_industry_taxonomy.php';
	}
	if (epc_apai_resolve_industry($pdo, $siteKey) !== 'auto_parts') {
		return false;
	}
	$cfg = epc_ape_tenant_config_get($pdo, $siteKey);
	return ((string) ($cfg['profile'] ?? '')) === 'warehouse_supplier';
}

/**
 * Resolve brand + article for a catalogue product (alias, properties, APAI meta, title).
 *
 * @return array{brand:string,article:string,caption:string,brand_display:string,article_display:string}
 */
function epc_apai_product_part_identity(PDO $pdo, int $productId): array
{
	$out = array(
		'brand' => '',
		'article' => '',
		'caption' => '',
		'brand_display' => '',
		'article_display' => '',
	);
	if ($productId <= 0) {
		return $out;
	}

	$stmt = $pdo->prepare(
		'SELECT `caption`, `alias`, `category_id` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1'
	);
	$stmt->execute(array($productId));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return $out;
	}
	$out['caption'] = trim((string) translate_str_by_id((int) ($row['caption'] ?? 0)));
	$alias = trim((string) ($row['alias'] ?? ''));

	if (is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		$parsed = epc_apai_parse_product_chpu($alias);
		if (!empty($parsed['brand'])) {
			$out['brand'] = (string) $parsed['brand'];
		}
		if (!empty($parsed['article'])) {
			$out['article'] = (string) $parsed['article'];
		}
	}

	if ($out['brand'] === '' || $out['article'] === '') {
		$catId = (int) ($row['category_id'] ?? 0);
		if ($catId > 0) {
			try {
				$propStmt = $pdo->prepare(
					'SELECT
						(SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (
							SELECT `value` FROM `shop_properties_values_list`
							 WHERE `product_id` = ? AND `property_id` = (
								SELECT `id` FROM `shop_categories_properties_map`
								 WHERE `category_id` = ? AND `property_type_id` = 5
								   AND `value` IN (SELECT `id` FROM `lang_text_strings` WHERE `id` IN (
									SELECT `str_key` FROM `lang_text_strings_translation`
									 WHERE `value` IN (\'Manufacturer\', \'Производитель\') AND `lang_code` = \'ru\'
								   ))
								 LIMIT 1
							 )
							 LIMIT 1
						)) AS `manufacturer`,
						(SELECT `value` FROM `shop_properties_values_text`
						 WHERE `product_id` = ? AND `property_id` = (
							SELECT `id` FROM `shop_categories_properties_map`
							 WHERE `category_id` = ? AND `property_type_id` = 3
							   AND `value` IN (SELECT `id` FROM `lang_text_strings` WHERE `id` IN (
								SELECT `str_key` FROM `lang_text_strings_translation`
								 WHERE `value` IN (\'Article\', \'Артикул\') AND `lang_code` = \'ru\'
							   ))
							 LIMIT 1
						 )
						 LIMIT 1
						) AS `article_raw`'
				);
				$propStmt->execute(array($productId, $catId, $productId, $catId));
				$propRow = $propStmt->fetch(PDO::FETCH_ASSOC);
				if ($propRow) {
					if ($out['brand'] === '' && !empty($propRow['manufacturer'])) {
						$out['brand'] = epc_apai_normalize_brand((string) translate_str_by_id((int) $propRow['manufacturer']));
					}
					if ($out['article'] === '' && !empty($propRow['article_raw'])) {
						$out['article'] = epc_apai_normalize_article((string) translate_str_by_id((int) $propRow['article_raw']));
					}
				}
			} catch (Throwable $e) {
			}
		}
	}

	if (($out['brand'] === '' || $out['article'] === '') && is_file(__DIR__ . '/epc_auto_price_categories.php')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		$fromTitle = epc_apai_extract_brand_article_from_title($out['caption']);
		if ($out['brand'] === '' && $fromTitle['brand'] !== '') {
			$out['brand'] = epc_apai_normalize_brand((string) $fromTitle['brand']);
		}
		if ($out['article'] === '' && $fromTitle['article'] !== '') {
			$out['article'] = epc_apai_normalize_article((string) $fromTitle['article']);
		}
	}

	if ($out['brand'] !== '') {
		$out['brand_display'] = strtoupper(trim($out['brand']));
	}
	if ($out['article'] !== '') {
		$out['article_display'] = epc_apai_normalize_article($out['article']);
	}

	return $out;
}

/** CHPU href for warehouse price search: /{lang}/parts/{brand}/{article}. */
function epc_apai_warehouse_parts_search_href($DP_Config, string $brand, string $article, string $langHref = ''): string
{
	$brand = trim($brand);
	$article = trim($article);
	if ($brand === '' || $article === '') {
		return '';
	}
	if ($langHref === '' && function_exists('epc_apai_storefront_lang_prefix')) {
		require_once __DIR__ . '/epc_auto_price_categories.php';
		$langHref = epc_apai_storefront_lang_prefix();
	}
	$langHref = rtrim($langHref !== '' ? $langHref : '/en', '/');

	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php';
		$path = epc_demand_chpu_part_url($DP_Config, $brand, $article);
		if ($langHref !== '/en') {
			$path = preg_replace('#^/en(?=/|$)#', $langHref, $path);
		}
		return $path;
	}

	$partsSeg = 'parts';
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config['level_1']['url'])) {
		$partsSeg = (string) $DP_Config->chpu_search_config['level_1']['url'];
	}
	$articleNorm = function_exists('docpart_normalize_article_for_price')
		? docpart_normalize_article_for_price($article)
		: epc_apai_normalize_article($article);
	$slash = '---';
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config['slash_code'])) {
		$slash = (string) $DP_Config->chpu_search_config['slash_code'];
	}
	$brandAlias = str_replace('/', $slash, $brand);

	return $langHref . '/' . $partsSeg . '/' . rawurlencode($brandAlias) . '/' . rawurlencode($articleNorm);
}
