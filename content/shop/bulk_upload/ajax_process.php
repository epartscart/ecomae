<?php
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$db_link->query("SET NAMES utf8;");
} catch (Exception $e) {
	exit(json_encode(array('status'=>false,'message'=>'No DB connect')));
}
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_pricing.php");

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
		`uploaded_count` INT(11) NOT NULL DEFAULT 0,
		`available_count` INT(11) NOT NULL DEFAULT 0,
		`cross_count` INT(11) NOT NULL DEFAULT 0,
		`short_count` INT(11) NOT NULL DEFAULT 0,
		`notfound_count` INT(11) NOT NULL DEFAULT 0,
		`result_json` LONGTEXT NULL,
		`csv_result` LONGTEXT NULL,
		`created_at` DATETIME NOT NULL,
		`updated_at` DATETIME NOT NULL,
		PRIMARY KEY (`id`),
		KEY `user_id` (`user_id`),
		KEY `created_at` (`created_at`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
}

function epc_bulk_save_history($db_link, $user_id, $is_admin_viewer, $group_id, $file_name, $priority, $summary, $rows, $csv) {
	epc_bulk_ensure_history_schema($db_link);
	$stmt = $db_link->prepare("INSERT INTO `epc_bulk_upload_history` (`user_id`, `created_by_admin`, `group_id`, `file_name`, `priority`, `uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`, `result_json`, `csv_result`, `created_at`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, NOW(), NOW());");
	$stmt->execute(array(
		(int)$user_id,
		$is_admin_viewer ? 1 : 0,
		(int)$group_id,
		mb_substr((string)$file_name, 0, 255, 'UTF-8'),
		$priority,
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

epc_bulk_check_csrf();

$user_id = DP_User::getUserId();
$is_admin_viewer = DP_User::isAdmin();
if($user_id <= 0 && !$is_admin_viewer) {
	epc_bulk_error('Please log in first.');
}
$group_id = 0;
if($is_admin_viewer && !empty($_POST['admin_group_id'])) {
	$profile_check = $db_link->prepare("SELECT `group_id` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1;");
	$profile_check->execute(array((int)$_POST['admin_group_id']));
	$group_id = (int)$profile_check->fetchColumn();
}
if($group_id <= 0 && $user_id > 0) {
	$userProfile = DP_User::getUserProfile();
	$group_id = (int)$userProfile["groups"][0];
}
if($group_id <= 0) {
	epc_bulk_error('Select customer price profile.');
}
$priority = isset($_POST['priority']) && $_POST['priority'] === 'delivery' ? 'delivery' : 'price';

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

if(isset($_POST['action']) && $_POST['action'] === 'history_update') {
	$upload_id = isset($_POST['upload_id']) ? (int)$_POST['upload_id'] : 0;
	$summary = isset($_POST['summary']) ? json_decode((string)$_POST['summary'], true) : null;
	$rows = isset($_POST['rows']) ? json_decode((string)$_POST['rows'], true) : null;
	$csv = isset($_POST['csv']) ? (string)$_POST['csv'] : '';
	if($upload_id <= 0 || !is_array($summary) || !is_array($rows)) {
		exit(json_encode(array('status'=>false,'message'=>'History update data is invalid.')));
	}
	$summary = array(
		'uploaded'=>isset($summary['uploaded']) ? (int)$summary['uploaded'] : count($rows),
		'available'=>isset($summary['available']) ? (int)$summary['available'] : 0,
		'cross'=>isset($summary['cross']) ? (int)$summary['cross'] : 0,
		'short'=>isset($summary['short']) ? (int)$summary['short'] : 0,
		'notfound'=>isset($summary['notfound']) ? (int)$summary['notfound'] : 0
	);
	$ok = epc_bulk_update_history($db_link, $user_id, $is_admin_viewer, $upload_id, $summary, $rows, $csv);
	exit(json_encode(array('status'=>$ok)));
}

$bunches = epc_bulk_customer_price_bunches($db_link);
if(empty($bunches)) {
	exit(json_encode(array('status'=>false,'message'=>'No price-list warehouses are available for your location.')));
}

if(isset($_POST['action']) && $_POST['action'] === 'cross') {
	$item = array(
		'brand'=>'',
		'article'=>isset($_POST['article']) ? $_POST['article'] : '',
		'qty'=>isset($_POST['qty']) ? (int)$_POST['qty'] : 1,
		'target_price'=>'',
		'delivery'=>'',
		'comment'=>''
	);
	if(trim($item['article']) === '') {
		exit(json_encode(array('status'=>false,'message'=>'Part number is required.')));
	}
	if($item['qty'] <= 0) { $item['qty'] = 1; }
	list($exact, $cross) = epc_bulk_best_options_for_item($db_link, $DP_Config, $group_id, $item, $bunches, $priority, true);
	exit(json_encode(array('status'=>true,'exact'=>$exact,'cross'=>$cross)));
}

if(empty($_FILES['bulk_file']) || $_FILES['bulk_file']['error'] !== UPLOAD_ERR_OK) {
	exit(json_encode(array('status'=>false,'message'=>'Upload file is required.')));
}

$items = epc_bulk_read_input_lines($_FILES['bulk_file']['tmp_name'], $_FILES['bulk_file']['name']);
if(empty($items)) {
	exit(json_encode(array('status'=>false,'message'=>'No valid rows found. Use Brand, Part Number, Qty columns.')));
}

$rows = array();
$summary = array('uploaded'=>count($items),'available'=>0,'cross'=>0,'short'=>0,'notfound'=>0);
$csv = "Brand,Requested Article,Qty,Exact Brand,Exact Article,Exact Price,Exact Qty,Cross Brand,Cross Article,Cross Price,Cross Qty,Status\n";
foreach($items as $item) {
	list($exact, $cross) = epc_bulk_best_options_for_item($db_link, $DP_Config, $group_id, $item, $bunches, $priority, false);
	$available = ($exact !== null || $cross !== null);
	$short = false;
	if($exact !== null && $exact['selected'] && $exact['exist'] < $item['qty']) { $short = true; }
	if($exact === null && $cross !== null && $cross['exist'] < $item['qty']) { $short = true; }
	if($available) { $summary['available']++; } else { $summary['notfound']++; }
	if($cross !== null) { $summary['cross']++; }
	if($short) { $summary['short']++; }
	$status = $available ? ($short ? 'Available but short quantity' : 'Available') : 'Not found - click to check cross reference';
	$rows[] = array('input'=>$item,'exact'=>$exact,'cross'=>$cross,'available'=>$available,'cross_found'=>$cross!==null,'short_qty'=>$short,'status_label'=>$status,'cross_checked'=>false);
	$csv .= '"'.str_replace('"','""',$item['brand']).'","'.str_replace('"','""',$item['article']).'",'.(int)$item['qty'].',"'.
		str_replace('"','""', $exact ? $exact['manufacturer'] : '').'","'.str_replace('"','""', $exact ? $exact['article_show'] : '').'","'.($exact ? $exact['price'] : '').'","'.($exact ? $exact['exist'] : '').'","'.
		str_replace('"','""', $cross ? $cross['manufacturer'] : '').'","'.str_replace('"','""', $cross ? $cross['article_show'] : '').'","'.($cross ? $cross['price'] : '').'","'.($cross ? $cross['exist'] : '').'","'.$status."\"\n";
}

$upload_id = epc_bulk_save_history($db_link, $user_id, $is_admin_viewer, $group_id, $_FILES['bulk_file']['name'], $priority, $summary, $rows, $csv);
exit(json_encode(array('status'=>true,'rows'=>$rows,'summary'=>$summary,'csv'=>$csv,'upload_id'=>$upload_id)));
?>
