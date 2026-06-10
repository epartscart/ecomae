<?php
/**
 * Helpers for CP price list row editor: warehouses, profile margins, site preview links.
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_prices_edit_load_price_names')) {
	function epc_prices_edit_load_price_names(PDO $db)
	{
		$out = array();
		$q = $db->query('SELECT `id`, `name` FROM `shop_docpart_prices` ORDER BY `name`');
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$out[(int)$row['id']] = (string)$row['name'];
		}
		return $out;
	}
}

if (!function_exists('epc_prices_edit_load_warehouse_map')) {
	function epc_prices_edit_load_warehouse_map(PDO $db)
	{
		$map = array();
		$q = $db->query("SELECT `id`, `name`, `connection_options` FROM `shop_storages` WHERE `interface_type` = 2");
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$opts = json_decode((string)$row['connection_options'], true);
			if (!is_array($opts) || empty($opts['price_id'])) {
				continue;
			}
			$pid = (int)$opts['price_id'];
			if (!isset($map[$pid])) {
				$map[$pid] = array('names' => array(), 'office_id' => 0, 'storage_id' => (int)$row['id']);
			}
			$map[$pid]['names'][] = (string)$row['name'];
			if ($map[$pid]['office_id'] <= 0) {
				$off = $db->prepare(
					'SELECT `office_id` FROM `shop_offices_storages_map` WHERE `storage_id` = ? ORDER BY `id` ASC LIMIT 1'
				);
				$off->execute(array((int)$row['id']));
				$map[$pid]['office_id'] = (int)$off->fetchColumn();
			}
		}
		foreach ($map as $pid => $info) {
			$map[$pid]['label'] = implode(', ', $info['names']);
		}
		return $map;
	}
}

if (!function_exists('epc_prices_edit_load_profiles')) {
	function epc_prices_edit_load_profiles(PDO $db)
	{
		$profiles = array();
		try {
			$q = $db->query(
				"SELECT `groups`.`id`, `groups`.`value`, `epc_price_profiles`.`code`, `epc_price_profiles`.`vat_percent`
				 FROM `epc_price_profiles`
				 INNER JOIN `groups` ON `groups`.`id` = `epc_price_profiles`.`group_id`
				 ORDER BY `epc_price_profiles`.`id` ASC"
			);
			while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
				$profiles[] = $row;
			}
		} catch (Exception $e) {
			$profiles = array();
		}
		return $profiles;
	}
}

if (!function_exists('epc_prices_edit_round_price')) {
	function epc_prices_edit_round_price($work_price, $DP_Config)
	{
		$work_price = number_format((float)$work_price, 2, '.', '');
		$mode = isset($DP_Config->price_rounding) ? (string)$DP_Config->price_rounding : '';
		if ($mode === '1') {
			if ($work_price > (int)$work_price) {
				return (int)$work_price + 1;
			}
			return (int)$work_price;
		}
		if ($mode === '2' || $mode === '3') {
			$work_price = (int)$work_price;
			$last = (int)substr((string)$work_price, -1);
			if ($mode === '2') {
				if ($last > 0 && $last < 5) {
					$work_price += (5 - $last);
				} elseif ($last > 5 && $last <= 9) {
					$work_price += (10 - $last);
				}
			} elseif ($last !== 0) {
				$work_price += (10 - $last);
			}
		}
		return (float)$work_price;
	}
}

if (!function_exists('epc_prices_edit_calc_site_price')) {
	function epc_prices_edit_calc_site_price(PDO $db, $DP_Config, $base_price, $manufacturer, $group_id, $office_id, $storage_id)
	{
		$base_price = (float)$base_price;
		$group_id = (int)$group_id;
		if ($base_price <= 0 || $group_id <= 0) {
			return array('visible' => true, 'site_price' => null, 'margin_pct' => null, 'markup_pct' => null);
		}

		$work_price = $base_price;
		$markup_decimal = 0.0;
		$office_id = (int)$office_id;
		$storage_id = (int)$storage_id;

		if ($office_id > 0 && $storage_id > 0) {
			$mq = $db->prepare(
				'SELECT `min_point`, `max_point`, `markup` / 100 AS `markup`
				 FROM `shop_offices_storages_map`
				 WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ?
				 ORDER BY `min_point`'
			);
			$mq->execute(array($office_id, $storage_id, $group_id));
			while ($mr = $mq->fetch(PDO::FETCH_ASSOC)) {
				if ($work_price >= (float)$mr['min_point'] && $work_price <= (float)$mr['max_point']) {
					$markup_decimal = (float)$mr['markup'];
					$work_price = $work_price + ($work_price * $markup_decimal);
					break;
				}
			}
		}

		if (!function_exists('epc_pricing_apply_brand_rule')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
		}

		$visible = true;
		$brand_margin = 0.0;
		if (function_exists('epc_pricing_apply_brand_rule')) {
			$rule = epc_pricing_apply_brand_rule($db, $group_id, $manufacturer, $work_price, $markup_decimal, $article);
			$visible = !empty($rule['visible']);
			$work_price = (float)$rule['price'];
			$markup_decimal = (float)$rule['markup_decimal'];
			$brand_margin = isset($rule['brand_margin_percent']) ? (float)$rule['brand_margin_percent'] : 0.0;
		}

		if (!$visible) {
			return array('visible' => false, 'site_price' => null, 'margin_pct' => null, 'markup_pct' => null);
		}

		$work_price = epc_prices_edit_round_price($work_price, $DP_Config);
		$margin_pct = $base_price > 0 ? round((($work_price - $base_price) / $base_price) * 100, 1) : 0;
		$markup_pct = round($markup_decimal * 100, 1);

		return array(
			'visible' => true,
			'site_price' => $work_price,
			'margin_pct' => $margin_pct,
			'markup_pct' => $markup_pct,
			'brand_margin' => $brand_margin,
		);
	}
}

if (!function_exists('epc_prices_edit_site_url')) {
	function epc_prices_edit_site_url($DP_Config, $article, $manufacturer = '')
	{
		$base = rtrim((string)$DP_Config->domain_path, '/');
		$url = $base . '/shop/part_search?article=' . rawurlencode((string)$article);
		if (trim((string)$manufacturer) !== '') {
			$url .= '&brend=' . rawurlencode((string)$manufacturer);
		}
		return $url;
	}
}

if (!function_exists('epc_prices_edit_h')) {
	function epc_prices_edit_h($s)
	{
		return htmlspecialchars((string)$s, ENT_QUOTES, 'UTF-8');
	}
}
