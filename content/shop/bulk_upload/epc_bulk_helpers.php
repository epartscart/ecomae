<?php
/**
 * Bulk spare-parts upload — shared helpers (storefront + CP).
 */
defined('_ASTEXE_') or die('No access');

function epc_bulk_error($message) {
	exit(json_encode(array('status'=>false,'message'=>$message)));
}

function epc_bulk_ensure_history_schema($db_link) {
	$db_link->exec("CREATE TABLE IF NOT EXISTS `epc_bulk_upload_history` (
		`id` INT(11) NOT NULL AUTO_INCREMENT,
		`user_id` INT(11) NOT NULL DEFAULT 0,
		`created_by_admin` TINYINT(1) NOT NULL DEFAULT 0,
		`group_id` INT(11) NOT NULL DEFAULT 0,
		`file_name` VARCHAR(255) NOT NULL DEFAULT '',
		`priority` VARCHAR(20) NOT NULL DEFAULT 'price',
		`source` VARCHAR(32) NOT NULL DEFAULT 'storefront',
		`uploaded_count` INT(11) NOT NULL DEFAULT 0,
		`available_count` INT(11) NOT NULL DEFAULT 0,
		`cross_count` INT(11) NOT NULL DEFAULT 0,
		`short_count` INT(11) NOT NULL DEFAULT 0,
		`notfound_count` INT(11) NOT NULL DEFAULT 0,
		`result_json` LONGTEXT NULL,
		`csv_result` LONGTEXT NULL,
		`cp_reviewed_at` DATETIME NULL,
		`cp_reviewed_by` INT(11) NOT NULL DEFAULT 0,
		`cp_notes` VARCHAR(512) NOT NULL DEFAULT '',
		`shop_quote_id` INT(11) NOT NULL DEFAULT 0,
		`crm_quote_id` INT(11) NOT NULL DEFAULT 0,
		`cart_added_count` INT(11) NOT NULL DEFAULT 0,
		`created_at` DATETIME NOT NULL,
		`updated_at` DATETIME NOT NULL,
		PRIMARY KEY (`id`),
		KEY `user_id` (`user_id`),
		KEY `created_at` (`created_at`),
		KEY `source` (`source`),
		KEY `cp_reviewed_at` (`cp_reviewed_at`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
	$alters = array(
		"ALTER TABLE `epc_bulk_upload_history` ADD COLUMN `source` VARCHAR(32) NOT NULL DEFAULT 'storefront'",
		"ALTER TABLE `epc_bulk_upload_history` ADD COLUMN `cp_reviewed_at` DATETIME NULL",
		"ALTER TABLE `epc_bulk_upload_history` ADD COLUMN `cp_reviewed_by` INT(11) NOT NULL DEFAULT 0",
		"ALTER TABLE `epc_bulk_upload_history` ADD COLUMN `cp_notes` VARCHAR(512) NOT NULL DEFAULT ''",
		"ALTER TABLE `epc_bulk_upload_history` ADD COLUMN `shop_quote_id` INT(11) NOT NULL DEFAULT 0",
		"ALTER TABLE `epc_bulk_upload_history` ADD COLUMN `crm_quote_id` INT(11) NOT NULL DEFAULT 0",
		"ALTER TABLE `epc_bulk_upload_history` ADD COLUMN `cart_added_count` INT(11) NOT NULL DEFAULT 0",
		"ALTER TABLE `epc_bulk_upload_history` ADD KEY `source` (`source`)",
	);
	foreach ($alters as $sql) {
		try { $db_link->exec($sql); } catch (Throwable $e) { /* already applied */ }
	}
}

function epc_bulk_save_history($db_link, $user_id, $is_admin_viewer, $group_id, $file_name, $priority, $summary, $rows, $csv, $source = 'storefront') {
	epc_bulk_ensure_history_schema($db_link);
	$source = preg_replace('/[^a-z0-9_]/', '', strtolower((string)$source));
	if ($source === '') { $source = 'storefront'; }
	$stmt = $db_link->prepare("INSERT INTO `epc_bulk_upload_history` (`user_id`, `created_by_admin`, `group_id`, `file_name`, `priority`, `source`, `uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`, `result_json`, `csv_result`, `created_at`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, NOW(), NOW());");
	$stmt->execute(array(
		(int)$user_id,
		$is_admin_viewer ? 1 : 0,
		(int)$group_id,
		mb_substr((string)$file_name, 0, 255, 'UTF-8'),
		$priority,
		$source,
		(int)$summary['uploaded'],
		(int)$summary['available'],
		(int)$summary['cross'],
		(int)$summary['short'],
		(int)$summary['notfound'],
		json_encode($rows, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$csv
	));
	return (int)$db_link->lastInsertId();
}

function epc_bulk_update_history($db_link, $user_id, $is_admin_viewer, $upload_id, $summary, $rows, $csv) {
	epc_bulk_ensure_history_schema($db_link);
	$where_user = $is_admin_viewer ? '' : ' AND `user_id` = ?';
	$stmt = $db_link->prepare("UPDATE `epc_bulk_upload_history` SET `uploaded_count` = ?, `available_count` = ?, `cross_count` = ?, `short_count` = ?, `notfound_count` = ?, `result_json` = ?, `csv_result` = ?, `updated_at` = NOW() WHERE `id` = ?".$where_user." LIMIT 1;");
	$args = array(
		(int)$summary['uploaded'],
		(int)$summary['available'],
		(int)$summary['cross'],
		(int)$summary['short'],
		(int)$summary['notfound'],
		json_encode($rows, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$csv,
		(int)$upload_id
	);
	if(!$is_admin_viewer) {
		$args[] = (int)$user_id;
	}
	$stmt->execute($args);
	return $stmt->rowCount() > 0;
}

function epc_bulk_check_csrf() {
	if(empty($_POST["csrf_guard_key"])) {
		epc_bulk_error('Error! CSRF 1');
	}
	$key = $_POST["csrf_guard_key"];
	$user_session = DP_User::getUserSession();
	if(is_array($user_session) && !empty($user_session["csrf_guard_key"]) && $user_session["csrf_guard_key"] === $key) {
		return true;
	}
	$admin_session = DP_User::getAdminSession();
	if(is_array($admin_session) && !empty($admin_session["csrf_guard_key"]) && $admin_session["csrf_guard_key"] === $key) {
		return true;
	}
	epc_bulk_error('Error! CSRF 4');
}

function epc_bulk_norm($value) {
	return mb_strtoupper(trim((string)$value), 'UTF-8');
}

function epc_bulk_parse_csv_text($text) {
	$rows = array();
	$lines = preg_split('/\r\n|\r|\n/', $text);
	foreach($lines as $line) {
		$line = trim($line);
		if($line === '') { continue; }
		$delimiter = (substr_count($line, ';') > substr_count($line, ',')) ? ';' : ',';
		if(strpos($line, "\t") !== false) { $delimiter = "\t"; }
		$rows[] = str_getcsv($line, $delimiter);
	}
	return $rows;
}

function epc_bulk_xlsx_cell_ref($cell) {
	return preg_replace('/[^A-Z]/', '', strtoupper($cell));
}

function epc_bulk_xlsx_col_index($letters) {
	$n = 0;
	for($i=0; $i<strlen($letters); $i++) {
		$n = $n * 26 + (ord($letters[$i]) - 64);
	}
	return $n - 1;
}

function epc_bulk_parse_xlsx($file) {
	if(!class_exists('ZipArchive')) { return array(); }
	$zip = new ZipArchive();
	if($zip->open($file) !== true) { return array(); }
	$shared = array();
	$sharedXml = $zip->getFromName('xl/sharedStrings.xml');
	if($sharedXml !== false) {
		$xml = @simplexml_load_string($sharedXml);
		if($xml) {
			foreach($xml->si as $si) {
				if(isset($si->t)) { $shared[] = (string)$si->t; }
				else {
					$txt = '';
					foreach($si->r as $r) { $txt .= (string)$r->t; }
					$shared[] = $txt;
				}
			}
		}
	}
	$sheetXml = $zip->getFromName('xl/worksheets/sheet1.xml');
	$zip->close();
	if($sheetXml === false) { return array(); }
	$xml = @simplexml_load_string($sheetXml);
	if(!$xml) { return array(); }
	$out = array();
	foreach($xml->sheetData->row as $row) {
		$line = array();
		foreach($row->c as $cell) {
			$ref = epc_bulk_xlsx_cell_ref((string)$cell['r']);
			$idx = epc_bulk_xlsx_col_index($ref);
			$type = (string)$cell['t'];
			$value = isset($cell->v) ? (string)$cell->v : '';
			if($type === 's') { $value = isset($shared[(int)$value]) ? $shared[(int)$value] : ''; }
			$line[$idx] = $value;
		}
		if(!empty($line)) {
			ksort($line);
			$out[] = $line;
		}
	}
	return $out;
}

function epc_bulk_rows_from_file($file, $name) {
	$ext = strtolower(pathinfo($name, PATHINFO_EXTENSION));
	if($ext === 'xlsx') { return epc_bulk_parse_xlsx($file); }
	$text = file_get_contents($file);
	if(substr($text, 0, 3) === "\xEF\xBB\xBF") { $text = substr($text, 3); }
	return epc_bulk_parse_csv_text($text);
}

function epc_bulk_read_input_lines($file, $name) {
	$raw = epc_bulk_rows_from_file($file, $name);
	$items = array();
	foreach($raw as $row) {
		$row = array_values($row);
		$brand = isset($row[0]) ? trim($row[0]) : '';
		$article = isset($row[1]) ? trim($row[1]) : '';
		$qty = isset($row[2]) ? (int)preg_replace('/[^0-9]/', '', (string)$row[2]) : 1;
		$target = isset($row[3]) ? trim($row[3]) : '';
		$delivery = isset($row[4]) ? trim($row[4]) : '';
		$comment = isset($row[5]) ? trim($row[5]) : '';
		if($article === '' || preg_match('/part|article|number|номер/i', $article)) { continue; }
		if($qty <= 0) { $qty = 1; }
		$items[] = array('brand'=>$brand,'article'=>$article,'qty'=>$qty,'target_price'=>$target,'delivery'=>$delivery,'comment'=>$comment);
		if(count($items) >= 2000) { break; }
	}
	return $items;
}

function epc_bulk_customer_price_bunches($db_link) {
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");
	$bunches = array();
	foreach($customer_offices as $office_id) {
		$stmt = $db_link->prepare("SELECT `shop_offices_storages_map`.`storage_id`, `shop_storages`.`connection_options`, `shop_storages`.`name`, `shop_storages`.`currency`, (SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = `shop_storages`.`currency`) AS `rate` FROM `shop_offices_storages_map` INNER JOIN `shop_storages` ON `shop_storages`.`id` = `shop_offices_storages_map`.`storage_id` WHERE `shop_offices_storages_map`.`office_id` = ? AND `shop_storages`.`hidden` = 0 AND (SELECT `handler_folder` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) = 'prices';");
		$stmt->execute(array((int)$office_id));
		while($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$options = json_decode($row['connection_options'], true);
			if(empty($options['price_id'])) { continue; }
			$bunches[] = array(
				'office_id'=>(int)$office_id,
				'storage_id'=>(int)$row['storage_id'],
				'price_id'=>(int)$options['price_id'],
				'storage'=>$row['name'],
				'rate'=>empty($row['rate']) ? 1 : (float)$row['rate'],
				'probability'=>isset($options['probability']) ? (int)$options['probability'] : 100
			);
		}
	}
	return $bunches;
}

function epc_bulk_marked_product($db_link, $DP_Config, $group_id, $product, $bunch, $qty, $match_type, $match_label) {
	$markup_stmt = $db_link->prepare("SELECT `markup`/100 AS `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= ? AND `max_point` > ? LIMIT 1;");
	$markup_stmt->execute(array($bunch['office_id'], $bunch['storage_id'], $group_id, $product['price'], $product['price']));
	$markup = (float)$markup_stmt->fetchColumn();
	$price_purchase = (float)$product['price'] * (float)$bunch['rate'];
	$price = ((float)$product['price'] + ((float)$product['price'] * $markup)) * (float)$bunch['rate'];
	$profile_rule = epc_pricing_apply_brand_rule($db_link, $group_id, $product['manufacturer'], $price, $markup);
	if(!$profile_rule['visible']) { return null; }
	$price = (float)$profile_rule['price'];
	$markup = (float)$profile_rule['markup_decimal'];
	$article = docpart_normalize_article_for_price($product['article']);
	$article_show = !empty($product['article_show']) ? $product['article_show'] : $product['article'];
	$json_params = '';
	$object = array(
		'manufacturer'=>epc_bulk_norm($product['manufacturer']),
		'article'=>$article,
		'article_show'=>$article_show,
		'name'=>$product['name'],
		'exist'=>(int)$product['exist'],
		'price'=>number_format($price, 2, '.', ''),
		'time_to_exe'=>(int)$product['time_to_exe'],
		'time_to_exe_guaranteed'=>(int)$product['time_to_exe'],
		'storage'=>$bunch['storage'],
		'min_order'=>max(1, (int)$product['min_order']),
		'probability'=>$bunch['probability'],
		'office_id'=>$bunch['office_id'],
		'storage_id'=>$bunch['storage_id'],
		'price_purchase'=>number_format($price_purchase, 2, '.', ''),
		'markup'=>(int)round($markup * 100),
		'json_params'=>$json_params,
		'product_type'=>2,
		'count_need'=>min((int)$qty, max(1, (int)$product['exist']))
	);
	$object['check_hash'] = md5($object['manufacturer'].$object['article'].$object['article_show'].$object['name'].$object['exist'].$object['price'].$object['time_to_exe'].$object['time_to_exe_guaranteed'].$object['storage'].$object['min_order'].$object['probability'].$object['office_id'].$object['storage_id'].$object['price_purchase'].$object['markup'].$json_params."2".$DP_Config->tech_key);
	return array(
		'manufacturer'=>$object['manufacturer'],
		'article'=>$object['article'],
		'article_show'=>$object['article_show'],
		'name'=>$object['name'],
		'exist'=>$object['exist'],
		'price'=>(float)$object['price'],
		'time_to_exe'=>$object['time_to_exe'],
		'match_type'=>$match_type,
		'match_label'=>$match_label,
		'product_object'=>$object
	);
}

function epc_bulk_fetch_url($url) {
	if(function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
		curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);
		curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, 5);
		curl_setopt($ch, CURLOPT_TIMEOUT, 10);
		curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
		curl_setopt($ch, CURLOPT_USERAGENT, 'ePartsCart bulk cross search');
		$html = curl_exec($ch);
		curl_close($ch);
		return is_string($html) ? $html : '';
	}
	$context = stream_context_create(array('http' => array('timeout' => 10, 'header' => "User-Agent: ePartsCart bulk cross search\r\n")));
	$html = @file_get_contents($url, false, $context);
	return is_string($html) ? $html : '';
}

function epc_bulk_add_cross_candidate(&$candidates, $article_norm, $label) {
	if($article_norm === '' || isset($candidates[$article_norm])) {
		return;
	}
	$candidates[$article_norm] = array('type'=>'cross','label'=>$label);
}

function epc_bulk_add_crossbase_candidates(&$candidates, $article_input) {
	$html = epc_bulk_fetch_url('https://crossbase.ru/cross/?q='.rawurlencode($article_input));
	if($html === '') {
		return;
	}
	$count = 0;
	if(preg_match_all('~<tr>\s*<td[^>]*>\s*[0-9]+\s*</td>\s*<td[^>]*>\s*<a[^>]*href=["\']/cross/\?q=([^"\']+)["\'][^>]*>~isu', $html, $row_matches, PREG_SET_ORDER)) {
		foreach($row_matches as $match) {
			$number_norm = docpart_normalize_article_for_price(urldecode($match[1]));
			epc_bulk_add_cross_candidate($candidates, $number_norm, 'Cross-reference');
			$count++;
			if($count >= 500) { break; }
		}
		return;
	}
	if(preg_match_all('~<a\s+[^>]*href=["\']/cross/\?q=([^"\']+)["\']~isu', $html, $matches, PREG_SET_ORDER)) {
		foreach($matches as $match) {
			$number_norm = docpart_normalize_article_for_price(urldecode($match[1]));
			epc_bulk_add_cross_candidate($candidates, $number_norm, 'Cross-reference');
			$count++;
			if($count >= 500) { break; }
		}
	}
}

function epc_bulk_find_options($db_link, $DP_Config, $group_id, $item, $bunches, $priority, $include_cross) {
	$article_norm = docpart_normalize_article_for_price($item['article']);
	$brand_norm = epc_bulk_norm($item['brand']);
	$candidates = array($article_norm=>array('type'=>'exact','label'=>'Exact'));
	if($include_cross) {
		$cross_stmt = $db_link->prepare("SELECT `article`, `analog` FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ? LIMIT 300;");
		$cross_stmt->execute(array($article_norm, $article_norm));
		while($r = $cross_stmt->fetch(PDO::FETCH_ASSOC)) {
			$a = docpart_normalize_article_for_price($r['article']);
			$b = docpart_normalize_article_for_price($r['analog']);
			$c = ($a === $article_norm) ? $b : $a;
			if($c !== '' && $c !== $article_norm) { $candidates[$c] = array('type'=>'cross','label'=>'Cross'); }
		}
		epc_bulk_add_crossbase_candidates($candidates, $item['article']);
	}
	$options = array();
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	foreach($bunches as $bunch) {
		foreach($candidates as $candidate => $meta) {
			$sql = "SELECT * FROM `shop_docpart_prices_data` WHERE $art_expr = ? AND `price_id` = ? AND `exist` > 0";
			$args = array($candidate, $bunch['price_id']);
			if($meta['type'] === 'exact' && $brand_norm !== '') {
				$sql .= " AND UPPER(`manufacturer`) = ?";
				$args[] = $brand_norm;
			}
			$sql .= " LIMIT 30";
			$stmt = $db_link->prepare($sql);
			$stmt->execute($args);
			while($product = $stmt->fetch(PDO::FETCH_ASSOC)) {
				$option = epc_bulk_marked_product($db_link, $DP_Config, $group_id, $product, $bunch, $item['qty'], $meta['type'], $meta['label']);
				if($option !== null) { $options[] = $option; }
			}
		}
	}
	usort($options, function($a, $b) use ($priority) {
		if($a['match_type'] !== $b['match_type']) { return $a['match_type'] === 'exact' ? -1 : 1; }
		if($priority === 'delivery' && $a['time_to_exe'] != $b['time_to_exe']) { return $a['time_to_exe'] - $b['time_to_exe']; }
		if($a['price'] == $b['price']) { return $a['time_to_exe'] - $b['time_to_exe']; }
		return ($a['price'] < $b['price']) ? -1 : 1;
	});
	return $options;
}

function epc_bulk_best_options_for_item($db_link, $DP_Config, $group_id, $item, $bunches, $priority, $include_cross) {
	$options = epc_bulk_find_options($db_link, $DP_Config, $group_id, $item, $bunches, $priority, $include_cross);
	$exact = null; $cross = null;
	foreach($options as $opt) {
		if($opt['match_type'] === 'exact' && $exact === null) { $exact = $opt; }
		if($opt['match_type'] === 'cross' && $cross === null) { $cross = $opt; }
	}
	if($exact !== null) { $exact['selected'] = true; }
	else if($cross !== null) { $cross['selected'] = true; }
	return array($exact, $cross);
}


function epc_bulk_h($v)
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_bulk_money($amount)
{
	return number_format((float)$amount, 2, '.', ',');
}

function epc_bulk_selected_option(array $row)
{
	if (!empty($row['exact']['selected']) && is_array($row['exact'])) {
		return $row['exact'];
	}
	if (!empty($row['cross']['selected']) && is_array($row['cross'])) {
		return $row['cross'];
	}
	if (!empty($row['exact']) && is_array($row['exact'])) {
		return $row['exact'];
	}
	if (!empty($row['cross']) && is_array($row['cross'])) {
		return $row['cross'];
	}
	return null;
}

function epc_bulk_process_items(PDO $db_link, $DP_Config, $group_id, array $items, $priority, $include_cross = false)
{
	$bunches = epc_bulk_customer_price_bunches($db_link);
	if (empty($bunches)) {
		throw new Exception('No price-list warehouses are available for this location.');
	}
	$rows = array();
	$summary = array('uploaded' => count($items), 'available' => 0, 'cross' => 0, 'short' => 0, 'notfound' => 0);
	$csv = "Brand,Requested Article,Qty,Exact Brand,Exact Article,Exact Price,Exact Qty,Cross Brand,Cross Article,Cross Price,Cross Qty,Status\n";
	foreach ($items as $item) {
		list($exact, $cross) = epc_bulk_best_options_for_item($db_link, $DP_Config, $group_id, $item, $bunches, $priority, $include_cross);
		$available = ($exact !== null || $cross !== null);
		$short = false;
		if ($exact !== null && !empty($exact['selected']) && (int)$exact['exist'] < (int)$item['qty']) {
			$short = true;
		}
		if ($exact === null && $cross !== null && (int)$cross['exist'] < (int)$item['qty']) {
			$short = true;
		}
		if ($available) {
			$summary['available']++;
		} else {
			$summary['notfound']++;
		}
		if ($cross !== null) {
			$summary['cross']++;
		}
		if ($short) {
			$summary['short']++;
		}
		$status = $available ? ($short ? 'Available but short quantity' : 'Available') : 'Not found - click to check cross reference';
		$rows[] = array(
			'input' => $item,
			'exact' => $exact,
			'cross' => $cross,
			'available' => $available,
			'cross_found' => $cross !== null,
			'short_qty' => $short,
			'status_label' => $status,
			'cross_checked' => (bool)$include_cross,
		);
		$csv .= '"' . str_replace('"', '""', $item['brand']) . '","' . str_replace('"', '""', $item['article']) . '",' . (int)$item['qty'] . ',"' .
			str_replace('"', '""', $exact ? $exact['manufacturer'] : '') . '","' . str_replace('"', '""', $exact ? $exact['article_show'] : '') . '","' . ($exact ? $exact['price'] : '') . '","' . ($exact ? $exact['exist'] : '') . '","' .
			str_replace('"', '""', $cross ? $cross['manufacturer'] : '') . '","' . str_replace('"', '""', $cross ? $cross['article_show'] : '') . '","' . ($cross ? $cross['price'] : '') . '","' . ($cross ? $cross['exist'] : '') . '","' . $status . "\"\n";
	}
	return array('rows' => $rows, 'summary' => $summary, 'csv' => $csv);
}

function epc_bulk_customer_label(PDO $db, $user_id)
{
	$user_id = (int)$user_id;
	if ($user_id <= 0) {
		return 'Unassigned / admin preview';
	}
	$st = $db->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$st->execute(array($user_id));
	$user = $st->fetch(PDO::FETCH_ASSOC);
	$email = $user ? (string)$user['email'] : '';
	$phone = $user ? (string)$user['phone'] : '';
	$name = '';
	$company = '';
	try {
		$p = $db->prepare(
			"SELECT `data_key`, `data_value` FROM `users_profiles`
			 WHERE `user_id` = ? AND `data_key` IN ('name','surname','patronymic','company','company_name')"
		);
		$p->execute(array($user_id));
		$parts = array();
		while ($row = $p->fetch(PDO::FETCH_ASSOC)) {
			$key = (string)$row['data_key'];
			$val = trim((string)$row['data_value']);
			if ($val === '') {
				continue;
			}
			if ($key === 'company' || $key === 'company_name') {
				$company = $val;
			} else {
				$parts[] = $val;
			}
		}
		$name = trim(implode(' ', $parts));
		if ($name === '' && $company !== '') {
			$name = $company;
		}
	} catch (Throwable $e) {
		$name = '';
	}
	$bits = array();
	if ($name !== '') {
		$bits[] = $name;
	}
	if ($email !== '') {
		$bits[] = $email;
	} elseif ($phone !== '') {
		$bits[] = $phone;
	}
	$bits[] = '#' . $user_id;
	return implode(' · ', $bits);
}

function epc_bulk_search_customers(PDO $db, $q, $limit = 20)
{
	$q = trim((string)$q);
	$limit = max(1, min(50, (int)$limit));
	if ($q === '') {
		$sql = 'SELECT u.`user_id`, u.`email` FROM `users` u WHERE u.`user_id` > 0 ORDER BY u.`user_id` DESC LIMIT ' . $limit;
		return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
	}
	if (ctype_digit($q)) {
		$st = $db->prepare('SELECT u.`user_id`, u.`email` FROM `users` u WHERE u.`user_id` = ? LIMIT 1');
		$st->execute(array((int)$q));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		return $row ? array($row) : array();
	}
	$like = '%' . $q . '%';
	$st = $db->prepare(
		'SELECT DISTINCT u.`user_id`, u.`email`
		 FROM `users` u
		 LEFT JOIN `users_profiles` p ON p.`user_id` = u.`user_id`
		 WHERE u.`user_id` > 0 AND (
			u.`email` LIKE ? OR u.`phone` LIKE ? OR p.`data_value` LIKE ?
		 )
		 ORDER BY u.`user_id` DESC
		 LIMIT ' . $limit
	);
	$st->execute(array($like, $like, $like));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_bulk_list_history(PDO $db, array $filters = array(), $limit = 40, $offset = 0)
{
	epc_bulk_ensure_history_schema($db);
	$where = array('1=1');
	$args = array();
	if (!empty($filters['user_id'])) {
		$where[] = '`user_id` = ?';
		$args[] = (int)$filters['user_id'];
	}
	if (!empty($filters['source'])) {
		$where[] = '`source` = ?';
		$args[] = (string)$filters['source'];
	}
	if (!empty($filters['unreviewed'])) {
		$where[] = '`cp_reviewed_at` IS NULL';
	}
	if (!empty($filters['q'])) {
		$where[] = '(`file_name` LIKE ? OR CAST(`user_id` AS CHAR) LIKE ?)';
		$like = '%' . $filters['q'] . '%';
		$args[] = $like;
		$args[] = $like;
	}
	$limit = max(1, min(100, (int)$limit));
	$offset = max(0, (int)$offset);
	$sql = 'SELECT `id`, `user_id`, `created_by_admin`, `group_id`, `file_name`, `priority`, `source`,
		`uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`,
		`cp_reviewed_at`, `cp_reviewed_by`, `cp_notes`, `shop_quote_id`, `crm_quote_id`, `cart_added_count`,
		`created_at`, `updated_at`
		FROM `epc_bulk_upload_history`
		WHERE ' . implode(' AND ', $where) . '
		ORDER BY `id` DESC
		LIMIT ' . $limit . ' OFFSET ' . $offset;
	$st = $db->prepare($sql);
	$st->execute($args);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$r) {
		$r['customer_label'] = epc_bulk_customer_label($db, (int)$r['user_id']);
	}
	unset($r);
	return $rows;
}

function epc_bulk_get_upload(PDO $db, $upload_id)
{
	epc_bulk_ensure_history_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_bulk_upload_history` WHERE `id` = ? LIMIT 1');
	$st->execute(array((int)$upload_id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	$row['customer_label'] = epc_bulk_customer_label($db, (int)$row['user_id']);
	$row['rows'] = json_decode((string)$row['result_json'], true);
	if (!is_array($row['rows'])) {
		$row['rows'] = array();
	}
	return $row;
}

function epc_bulk_dashboard(PDO $db)
{
	epc_bulk_ensure_history_schema($db);
	$total = (int)$db->query('SELECT COUNT(*) FROM `epc_bulk_upload_history`')->fetchColumn();
	$unreviewed = (int)$db->query('SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE `cp_reviewed_at` IS NULL')->fetchColumn();
	$today = (int)$db->query('SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE DATE(`created_at`) = CURDATE()')->fetchColumn();
	$storefront = (int)$db->query("SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE `source` = 'storefront'")->fetchColumn();
	$available = (int)$db->query('SELECT IFNULL(SUM(`available_count`),0) FROM `epc_bulk_upload_history` WHERE DATE(`created_at`) = CURDATE()')->fetchColumn();
	return array(
		'total' => $total,
		'unreviewed' => $unreviewed,
		'today' => $today,
		'storefront' => $storefront,
		'available_today' => $available,
	);
}

function epc_bulk_mark_reviewed(PDO $db, $upload_id, $admin_user_id, $notes = '')
{
	epc_bulk_ensure_history_schema($db);
	$st = $db->prepare('UPDATE `epc_bulk_upload_history` SET `cp_reviewed_at` = NOW(), `cp_reviewed_by` = ?, `cp_notes` = ?, `updated_at` = NOW() WHERE `id` = ? LIMIT 1');
	$st->execute(array((int)$admin_user_id, mb_substr(trim((string)$notes), 0, 512, 'UTF-8'), (int)$upload_id));
	return $st->rowCount() > 0;
}

function epc_bulk_add_to_customer_cart(PDO $db, $user_id, array $product_objects)
{
	$user_id = (int)$user_id;
	if ($user_id <= 0) {
		throw new Exception('Select a customer before adding to cart.');
	}
	$session_id = 0;
	$added = 0;
	$skipped = 0;
	foreach ($product_objects as $product_object) {
		if (!is_array($product_object)) {
			continue;
		}
		$price = (float)($product_object['price'] ?? 0);
		$count_need = max(1, (int)($product_object['count_need'] ?? 1));
		$t2_manufacturer = (string)($product_object['manufacturer'] ?? '');
		$t2_article = (string)($product_object['article'] ?? '');
		$t2_article_show = (string)($product_object['article_show'] ?? $t2_article);
		$t2_name = (string)($product_object['name'] ?? '');
		$t2_exist = (int)($product_object['exist'] ?? 0);
		$t2_time_to_exe = (int)($product_object['time_to_exe'] ?? 0);
		$t2_time_to_exe_guaranteed = (int)($product_object['time_to_exe_guaranteed'] ?? $t2_time_to_exe);
		$t2_storage = (string)($product_object['storage'] ?? '');
		$t2_min_order = max(1, (int)($product_object['min_order'] ?? 1));
		$t2_probability = (int)($product_object['probability'] ?? 100);
		$t2_markup = (int)($product_object['markup'] ?? 0);
		$t2_price_purchase = (float)($product_object['price_purchase'] ?? 0);
		$t2_office_id = (int)($product_object['office_id'] ?? 0);
		$t2_storage_id = (int)($product_object['storage_id'] ?? 0);
		$t2_json_params = (string)($product_object['json_params'] ?? '');
		if ($t2_article === '' || $price <= 0) {
			$skipped++;
			continue;
		}
		$chk = $db->prepare(
			'SELECT COUNT(*) FROM `shop_carts` WHERE `user_id` = ? AND `product_type` = 2
			 AND `t2_manufacturer` = ? AND `t2_article` = ? AND `t2_office_id` = ? AND `t2_storage_id` = ?
			 AND CAST(`price` AS DECIMAL(12,2)) = CAST(? AS DECIMAL(12,2))'
		);
		$chk->execute(array($user_id, $t2_manufacturer, $t2_article, $t2_office_id, $t2_storage_id, $price));
		if ((int)$chk->fetchColumn() > 0) {
			$skipped++;
			continue;
		}
		$sql = 'INSERT INTO `shop_carts` (
			`product_type`, `price`, `count_need`, `time`, `user_id`, `session_id`,
			`t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
			`t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
			`t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
			`t2_product_json`, `t2_json_params`
		) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)';
		$db->prepare($sql)->execute(array(
			2, $price, $count_need, time(), $user_id, $session_id,
			$t2_manufacturer, $t2_article, $t2_article_show, $t2_name, $t2_exist,
			$t2_time_to_exe, $t2_time_to_exe_guaranteed, $t2_storage, $t2_min_order,
			$t2_probability, $t2_markup, $t2_price_purchase, $t2_office_id, $t2_storage_id,
			json_encode($product_object, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
			$t2_json_params,
		));
		$added++;
	}
	return array('added' => $added, 'skipped' => $skipped);
}

function epc_bulk_create_shop_quote(PDO $db, $user_id, array $product_objects, $admin_note = '', $status = 'quoted')
{
	$user_id = (int)$user_id;
	if ($user_id <= 0) {
		throw new Exception('Select a customer before creating a quote.');
	}
	if (empty($product_objects)) {
		throw new Exception('No products selected for quote.');
	}
	$now = time();
	$status = in_array($status, array('draft', 'submitted', 'quoted'), true) ? $status : 'quoted';
	$db->prepare(
		'INSERT INTO `shop_quote_requests` (`user_id`, `session_id`, `status`, `time_created`, `time_updated`, `time_submitted`, `admin_note`)
		 VALUES (?, 0, ?, ?, ?, ?, ?)'
	)->execute(array(
		$user_id,
		$status,
		$now,
		$now,
		$status === 'draft' ? null : $now,
		mb_substr(trim((string)$admin_note), 0, 2000, 'UTF-8'),
	));
	$quote_id = (int)$db->lastInsertId();
	$ins = $db->prepare(
		'INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`, `quoted_price`, `quoted_time_to_exe`, `line_admin_note`)
		 VALUES (?, 2, ?, ?, ?, ?, ?)'
	);
	$lines = 0;
	foreach ($product_objects as $po) {
		if (!is_array($po)) {
			continue;
		}
		$count = max(1, (int)($po['count_need'] ?? 1));
		$price = (float)($po['price'] ?? 0);
		$tte = (int)($po['time_to_exe'] ?? 0);
		$note = trim((string)(($po['manufacturer'] ?? '') . ' ' . ($po['article_show'] ?? $po['article'] ?? '')));
		$ins->execute(array(
			$quote_id,
			json_encode($po, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
			$count,
			$price,
			$tte,
			mb_substr($note, 0, 512, 'UTF-8'),
		));
		$lines++;
	}
	if ($lines < 1) {
		$db->prepare('DELETE FROM `shop_quote_requests` WHERE `id` = ?')->execute(array($quote_id));
		throw new Exception('No valid quote lines.');
	}
	return array('quote_id' => $quote_id, 'lines' => $lines, 'status' => $status);
}

function epc_bulk_create_crm_quote(PDO $db, $user_id, array $product_objects, $notes = '')
{
	$user_id = (int)$user_id;
	if ($user_id <= 0) {
		throw new Exception('Select a customer before creating an ERP quote.');
	}
	$crmFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_modules.php';
	if (!is_file($crmFile)) {
		throw new Exception('ERP CRM module not available.');
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_schema.php';
	require_once $crmFile;
	if (!function_exists('epc_crm_save_quote')) {
		throw new Exception('ERP CRM quote API missing.');
	}
	$quoteId = epc_crm_save_quote($db, array(
		'customer_user_id' => $user_id,
		'status' => 'draft',
		'notes' => $notes !== '' ? $notes : 'Created from CP bulk upload',
	), 0);
	$sort = 0;
	$ins = $db->prepare(
		'INSERT INTO `epc_crm_quote_lines` (`quote_id`, `description`, `qty`, `unit_price`, `sort_order`) VALUES (?, ?, ?, ?, ?)'
	);
	$lines = 0;
	foreach ($product_objects as $po) {
		if (!is_array($po)) {
			continue;
		}
		$desc = trim((string)(($po['manufacturer'] ?? '') . ' ' . ($po['article_show'] ?? $po['article'] ?? '') . ' — ' . ($po['name'] ?? '')));
		if ($desc === '') {
			$desc = 'Spare part';
		}
		$ins->execute(array(
			$quoteId,
			mb_substr($desc, 0, 512, 'UTF-8'),
			max(0.001, (float)($po['count_need'] ?? 1)),
			max(0, (float)($po['price'] ?? 0)),
			$sort++,
		));
		$lines++;
	}
	if ($lines < 1) {
		throw new Exception('No valid ERP quote lines.');
	}
	if (function_exists('epc_crm_recalc_quote_subtotal')) {
		epc_crm_recalc_quote_subtotal($db, $quoteId);
	}
	return array('crm_quote_id' => $quoteId, 'lines' => $lines);
}

function epc_bulk_collect_product_objects(array $rows, array $indexes = null)
{
	$out = array();
	foreach ($rows as $i => $row) {
		if ($indexes !== null && !in_array((int)$i, $indexes, true) && !in_array((string)$i, array_map('strval', $indexes), true)) {
			continue;
		}
		$opt = epc_bulk_selected_option(is_array($row) ? $row : array());
		if ($opt && !empty($opt['product_object']) && is_array($opt['product_object'])) {
			$out[] = $opt['product_object'];
		}
	}
	return $out;
}

function epc_bulk_customer_group_id(PDO $db, $user_id)
{
	$user_id = (int)$user_id;
	if ($user_id <= 0) {
		return 0;
	}
	if (function_exists('epc_pricing_resolve_customer_group_id')) {
		$gid = (int) epc_pricing_resolve_customer_group_id($db, $user_id, 0);
		if ($gid > 0) {
			return $gid;
		}
	}
	try {
		$st = $db->prepare('SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? ORDER BY `record_id` DESC LIMIT 1');
		$st->execute(array($user_id));
		$gid = (int)$st->fetchColumn();
		if ($gid > 0) {
			return $gid;
		}
	} catch (Throwable $e) {
		// fall through
	}
	$profile = DP_User::getUserProfileById($user_id);
	if (is_array($profile) && !empty($profile['groups'][0])) {
		return (int)$profile['groups'][0];
	}
	return 0;
}
