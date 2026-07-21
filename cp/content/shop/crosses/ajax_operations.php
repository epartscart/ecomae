<?php
/**
 * CP AJAX — shop crosses (shop_docpart_articles_analogs_list).
 */
ini_set('display_errors', '0');
ini_set('max_execution_time', '120');
header('Content-Type: application/json; charset=utf-8');
if (ob_get_level()) {
	@ob_end_clean();
}

function prepareString($string)
{
	$sweep = array('#', '`', "\r\n", "\r", "\n", "\t", "'", '"');
	return trim(str_replace($sweep, '', (string) $string));
}

function pagination($all, $lim, $prev, $curr_link, $curr_css, $link)
{
	$html = '';
	$all = (int) $all;
	$lim = max(1, (int) $lim);
	$prev = max(1, (int) $prev);
	$curr_link = max(1, (int) $curr_link);
	$pages = (int) ceil($all / $lim);
	if ($pages < 1) {
		$pages = 1;
	}
	$first = max(1, $curr_link - $prev);
	$last = min($pages, $curr_link + $prev);
	if ($first > 1) {
		$html .= "<a onclick='go_to_page(1)'>1</a>";
	}
	$y = $first - 1;
	if ($first > $prev) {
		$html .= "<a onclick='go_to_page({$y})'>...</a>";
	} else {
		for ($i = 2; $i < $first; $i++) {
			$html .= "<a onclick='go_to_page({$y})'>$i</a>";
		}
	}
	for ($i = $first; $i <= $last; $i++) {
		if ($i == $curr_link) {
			$html .= '<a class="' . $curr_css . '">' . $i . '</a>';
		} else {
			$html .= "<a onclick='go_to_page(" . ($i != 1 ? $i : '') . ")'>$i</a>";
		}
	}
	$y = $last + 1;
	if ($last < $pages && $pages - $last > 2) {
		$html .= "<a onclick='go_to_page({$y})'>...</a>";
	}
	if ($last < $pages) {
		$html .= "<a onclick='go_to_page({$pages})'>$pages</a>";
	}
	return $html;
}

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	$dbHost = trim((string) $DP_Config->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
	$db_link->query('SET NAMES utf8mb4');
} catch (Throwable $e) {
	http_response_code(503);
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'forbidden')));
}

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
	if (function_exists('multilang_init')) {
		multilang_init();
	}
} catch (Throwable $e) {
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

$answer = array('status' => false);
$raw = (string) ($_POST['request_object'] ?? '');
$request_object = json_decode($raw, true);
if (!is_array($request_object)) {
	$request_object = json_decode(urldecode($raw), true);
}
if (!is_array($request_object)) {
	exit(json_encode(array('status' => false, 'message' => 'bad_request')));
}

$sweep = array(' ', '-', '_', '`', '/', "'", '"', '\\', '.', ',', '#', "\r\n", "\r", "\n", "\t");
$action = (string) ($request_object['action'] ?? '');

try {
switch ($action) {
	case 'get_table_crosses':
		$page = (int) ($request_object['page'] ?? 1);
		if ($page < 1) { $page = 1; }
		$kol = (int) ($DP_Config->list_page_limit ?? 30);
		if ($kol < 1) { $kol = 30; }
		$art = ($page * $kol) - $kol;
		$article = strip_tags(mb_strtoupper(trim(urldecode((string) ($request_object['article'] ?? ''))), 'UTF-8'));
		$article = (string) str_replace($sweep, '', $article);
		$manufacturer = strip_tags(mb_strtoupper(trim(urldecode((string) ($request_object['manufacturer'] ?? ''))), 'UTF-8'));
		$where = '';
		$binding_values = array();
		if ($article !== '') {
			if ($manufacturer !== '') {
				$where = ' (`article` = ? AND `manufacturer_article` = ?) OR (`analog` = ? AND `manufacturer_analog` = ?) ';
				$binding_values = array($article, $manufacturer, $article, $manufacturer);
			} else {
				$where = ' (`article` = ?) OR (`analog` = ?) ';
				$binding_values = array($article, $article);
			}
		}
		$nullFlag = (int) ($request_object['null'] ?? 0);
		if ($nullFlag === 1) {
			$where = ($where !== '')
				? (' (' . $where . ') AND (`article` = "" OR `manufacturer_article` = "" OR `analog` = "" OR `manufacturer_analog` = "") ')
				: ' (`article` = "" OR `manufacturer_article` = "" OR `analog` = "" OR `manufacturer_analog` = "") ';
		}
		$idFrom = $request_object['id_from'] ?? 'null';
		$idBefore = $request_object['id_before'] ?? 'null';
		if ($idFrom != 'null' && (int) $idFrom > 0) {
			$where = ($where !== '') ? (' (' . $where . ') AND (`id` >= ?) ') : ' (`id` >= ?) ';
			$binding_values[] = (int) $idFrom;
		}
		if ($idBefore != 'null' && (int) $idBefore > 0) {
			$where = ($where !== '') ? (' (' . $where . ') AND (`id` <= ?) ') : ' (`id` <= ?) ';
			$binding_values[] = (int) $idBefore;
		}
		$filtered = ($where !== '');
		if ($where !== '') {
			$where = ' WHERE ' . $where;
		}
		$total = 0;
		$countApprox = false;
		if ($filtered) {
			$res = $db_link->prepare('SELECT COUNT(*) AS `count` FROM `shop_docpart_articles_analogs_list`' . $where);
			$res->execute($binding_values);
			$total = (int) ($res->fetchColumn());
		} else {
			try {
				$st = $db_link->query("SELECT TABLE_ROWS FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_docpart_articles_analogs_list' LIMIT 1");
				$total = (int) $st->fetchColumn();
				$countApprox = true;
			} catch (Throwable $e) {
				$total = $kol * 50;
				$countApprox = true;
			}
			if ($total < 1) { $total = $kol; }
		}
		$sort_field = 'id';
		$sort_asc_desc = 'desc';
		if (!empty($_COOKIE['crosses_sort'])) {
			$cs = json_decode((string) $_COOKIE['crosses_sort'], true);
			if (is_array($cs)) {
				$sort_field = (string) ($cs['field'] ?? 'id');
				$sort_asc_desc = (string) ($cs['asc_desc'] ?? 'desc');
			}
		}
		$sort_asc_desc = (strtolower($sort_asc_desc) === 'asc') ? 'asc' : 'desc';
		if (!in_array($sort_field, array('id', 'article', 'manufacturer_article', 'analog', 'manufacturer_analog'), true)) {
			$sort_field = 'id';
		}
		if (!$filtered) {
			$sort_field = 'id';
			$sort_asc_desc = 'desc';
		}
		$sql = "SELECT * FROM `shop_docpart_articles_analogs_list`$where ORDER BY `$sort_field` $sort_asc_desc LIMIT $art, $kol";
		$query = $db_link->prepare($sql);
		$query->execute($binding_values);
		$t = function ($id, $fb) {
			return function_exists('translate_str_by_id') ? translate_str_by_id($id) : $fb;
		};
		$html = '';
		while ($rov = $query->fetch(PDO::FETCH_ASSOC)) {
			$id = (int) $rov['id'];
			$a = htmlspecialchars((string) $rov['article'], ENT_QUOTES, 'UTF-8');
			$ma = htmlspecialchars((string) $rov['manufacturer_article'], ENT_QUOTES, 'UTF-8');
			$an = htmlspecialchars((string) $rov['analog'], ENT_QUOTES, 'UTF-8');
			$man = htmlspecialchars((string) $rov['manufacturer_analog'], ENT_QUOTES, 'UTF-8');
			$html .= '<tr id="show_line_' . $id . '"><td>' . $a . '</td><td>' . $ma . '</td><td>' . $an . '</td><td>' . $man . '</td><td>' . $id . '</td><td>'
				. '<a onclick="crosses_edit(' . $id . ');" class="btn btn-sm btn-primary" title="' . htmlspecialchars($t(2270, 'Edit'), ENT_QUOTES, 'UTF-8') . '"><i class="fas fa-pencil-alt"></i></a> '
				. '<a onclick="crosses_del(' . $id . ');" class="btn btn-sm btn-danger" title="' . htmlspecialchars($t(2224, 'Delete'), ENT_QUOTES, 'UTF-8') . '"><i class="fa fa-times"></i></a></td></tr>'
				. '<tr class="hidden" id="edit_line_' . $id . '"><td><input class="form-control" type="text" id="article_edit_' . $id . '" value="' . $a . '"/></td>'
				. '<td><input class="form-control" type="text" id="manufacturer_article_edit_' . $id . '" value="' . $ma . '"/></td>'
				. '<td><input class="form-control" type="text" id="analog_edit_' . $id . '" value="' . $an . '"/></td>'
				. '<td><input class="form-control" type="text" id="manufacturer_analog_edit_' . $id . '" value="' . $man . '"/></td>'
				. '<td colspan="2"><a onclick="crosses_edit_save(' . $id . ');" class="btn btn-sm btn-success"><i class="fa fa-floppy-o"></i> ' . htmlspecialchars($t(2114, 'Save'), ENT_QUOTES, 'UTF-8') . '</a> '
				. '<a onclick="crosses_edit_otmena(' . $id . ');" class="btn btn-sm btn-default"><i class="fa fa-chevron-left"></i> ' . htmlspecialchars($t(2190, 'Cancel'), ENT_QUOTES, 'UTF-8') . '</a></td></tr>';
		}
		if (!$filtered) {
			$banner = '<div class="epc-cross-banner">Browsing latest links' . ($countApprox ? (' (~' . number_format($total) . ' in catalog)') : '') . '. Search by part number for exact matches.</div>';
		} else {
			$banner = '<div class="epc-cross-banner epc-cross-banner-ok">Filtered: <strong>' . number_format($total) . '</strong> row(s).</div>';
		}
		if ($html !== '') {
			$table = '<table class="table table-striped table_crosses"><thead><tr>'
				. '<th><a href="javascript:void(0);" onclick="sortCrosses(\'article\');" id="article_sorter">' . htmlspecialchars($t(2071, 'Article'), ENT_QUOTES, 'UTF-8') . '</a></th>'
				. '<th><a href="javascript:void(0);" onclick="sortCrosses(\'manufacturer_article\');" id="manufacturer_article_sorter">' . htmlspecialchars($t(2070, 'Manufacturer'), ENT_QUOTES, 'UTF-8') . '</a></th>'
				. '<th><a href="javascript:void(0);" onclick="sortCrosses(\'analog\');" id="analog_sorter">' . htmlspecialchars($t(3113, 'Analog'), ENT_QUOTES, 'UTF-8') . '</a></th>'
				. '<th><a href="javascript:void(0);" onclick="sortCrosses(\'manufacturer_analog\');" id="manufacturer_analog_sorter">' . htmlspecialchars($t(3114, 'Analog manufacturer'), ENT_QUOTES, 'UTF-8') . '</a></th>'
				. '<th><a href="javascript:void(0);" onclick="sortCrosses(\'id\');" id="id_sorter">ID</a></th>'
				. '<th>' . htmlspecialchars($t(2755, 'Actions'), ENT_QUOTES, 'UTF-8') . '</th></tr></thead><tbody>' . $html . '</tbody></table>';
			$pagination = pagination($total, $kol, 3, $page, 'pagination_active', '');
			$pagination = ($pagination !== '<a class="pagination_active">1</a>')
				? '<div class="panel-footer"><div class="pagination_box">' . $pagination . '</div></div>'
				: '';
			$html = '<div class="panel-body">' . $banner . $table . '</div>' . $pagination;
		} else {
			$html = '<div class="panel-body">' . $banner . '<p class="text-muted">' . htmlspecialchars($t(2756, 'No records'), ENT_QUOTES, 'UTF-8') . '</p></div>';
		}
		header('Content-Type: text/html; charset=utf-8');
		exit($html);

	case 'add_crosses':
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_cross_interchange.php");
		$article 				= strip_tags(mb_strtoupper(trim(urldecode($request_object['article'])), 'UTF-8'));
		$manufacturer_article 	= strip_tags(mb_strtoupper(trim(urldecode($request_object['manufacturer_article'])), 'UTF-8'));
		$analog 				= strip_tags(mb_strtoupper(trim(urldecode($request_object['analog'])), 'UTF-8'));
		$manufacturer_analog 	= strip_tags(mb_strtoupper(trim(urldecode($request_object['manufacturer_analog'])), 'UTF-8'));
		
		$article 				= str_replace($sweep, "", $article);
		$analog 				= str_replace($sweep, "", $analog);
		$manufacturer_article 	= docpart_cross_prepare_brand_name(prepareString($manufacturer_article));
		$manufacturer_analog 	= docpart_cross_prepare_brand_name(prepareString($manufacturer_analog));
		if($manufacturer_article === '')
		{
			$manufacturer_article = docpart_cross_resolve_brand_for_article($db_link, $article, array('partner_brand' => $manufacturer_analog));
		}
		if($manufacturer_analog === '')
		{
			$manufacturer_analog = docpart_cross_resolve_brand_for_article($db_link, $analog, array('partner_brand' => $manufacturer_article, 'fallback_brand' => $manufacturer_article));
		}
		
		if(!empty($article) && !empty($manufacturer_article) && !empty($analog) && !empty($manufacturer_analog))
		{
			if(docpart_cross_persist_interchange_pair_bidirectional($db_link, $article, $manufacturer_article, $analog, $manufacturer_analog) > 0)
			{
				$answer['status'] = true;
			}
		}
		break;
	case 'save_crosses':
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_cross_interchange.php");
		$id = (int)$request_object['id'];
		
		$article 				= strip_tags(mb_strtoupper(trim(urldecode($request_object['article'])), 'UTF-8'));
		$manufacturer_article 	= strip_tags(mb_strtoupper(trim(urldecode($request_object['manufacturer_article'])), 'UTF-8'));
		$analog 				= strip_tags(mb_strtoupper(trim(urldecode($request_object['analog'])), 'UTF-8'));
		$manufacturer_analog 	= strip_tags(mb_strtoupper(trim(urldecode($request_object['manufacturer_analog'])), 'UTF-8'));
		
		$article 				= str_replace($sweep, "", $article);
		$analog 				= str_replace($sweep, "", $analog);
		$manufacturer_article 	= docpart_cross_prepare_brand_name(prepareString($manufacturer_article));
		$manufacturer_analog 	= docpart_cross_prepare_brand_name(prepareString($manufacturer_analog));
		if($manufacturer_article === '')
		{
			$manufacturer_article = docpart_cross_resolve_brand_for_article($db_link, $article, array('partner_brand' => $manufacturer_analog));
		}
		if($manufacturer_analog === '')
		{
			$manufacturer_analog = docpart_cross_resolve_brand_for_article($db_link, $analog, array('partner_brand' => $manufacturer_article, 'fallback_brand' => $manufacturer_article));
		}

		if(!empty($article) && !empty($manufacturer_article) && !empty($analog) && !empty($manufacturer_analog))
		{
			$sql = "UPDATE `shop_docpart_articles_analogs_list` SET `article` = ?, `manufacturer_article` = ?, `analog` = ?, `manufacturer_analog` = ? WHERE `id` = ?;";

			if($db_link->prepare($sql)->execute( array($article, $manufacturer_article, $analog, $manufacturer_analog, $id) ))
			{
				$answer['status'] = true;
			}
		}
		break;
	case 'del_crosses':
		$id = (int)$request_object['id'];
		$sql = "DELETE FROM `shop_docpart_articles_analogs_list` WHERE `id` = ?;";
		if($db_link->prepare($sql)->execute( array($id) ))
		{
			$answer['status'] = true;
		}
		break;
	case 'del_search_crosses':
		$article = strip_tags(mb_strtoupper(trim(urldecode($request_object['article'])), 'UTF-8'));
		$article = (string) str_replace($sweep, "", $article);
		$manufacturer = strip_tags(mb_strtoupper(trim(urldecode($request_object['manufacturer'])), 'UTF-8'));
		
		$where = '';
		$binding_values = array();
		if(!empty($article))
		{
			if(!empty($manufacturer))
			{
				$where = " (`article` = ? AND `manufacturer_article` = ?) OR (`analog` = ? AND `manufacturer_analog` = ?) ";
				
				array_push($binding_values, $article);
				array_push($binding_values, $manufacturer);
				array_push($binding_values, $article);
				array_push($binding_values, $manufacturer);
			}
			else
			{
				$where = " (`article` = ?) OR (`analog` = ?) ";
				
				array_push($binding_values, $article);
				array_push($binding_values, $article);
			}
		}
		
		if($request_object['null'] == 1){
			if($where != ''){
				$where = ' ('. $where .') AND (`article` = "" OR `manufacturer_article` = "" OR `analog` = "" OR `manufacturer_analog` = "") ';
			}else{
				$where = ' (`article` = "" OR `manufacturer_article` = "" OR `analog` = "" OR `manufacturer_analog` = "") ';
			}
		}
		
		if($request_object['id_from'] != 'null' && ((int)$request_object['id_from'] > 0)){
			if($where != ''){
				$where = ' ('. $where .') AND (`id` >= ?) ';
				array_push($binding_values, (int) $request_object['id_from']);
			}else{
				$where = ' (`id` >= ?) ';
				array_push($binding_values, (int) $request_object['id_from']);
			}
		}
		if($request_object['id_before'] != 'null' && ((int)$request_object['id_before'] > 0)){
			if($where != ''){
				$where = ' ('. $where .') AND (`id` <= ?) ';
				array_push($binding_values, (int) $request_object['id_before']);
			}else{
				$where = ' (`id` <= ?) ';
				array_push($binding_values, (int) $request_object['id_before']);
			}
		}
		
		if($where != ''){
			$where = ' WHERE ' . $where;
		}
		
		$sql = "DELETE FROM `shop_docpart_articles_analogs_list` $where LIMIT 50000;";
		do{
			$query = $db_link->prepare($sql);
			$query->execute($binding_values);
		}while($query->rowCount() > 0);
		
		$answer['status'] = true;
		
		break;
	case 'get_search_manufacturer':
		$article = strip_tags(mb_strtoupper(trim(urldecode($request_object['article'])), 'UTF-8'));
		$article = str_replace($sweep, "", $article);
		
		$list_manufacturer = array();
		
		$sql = "SELECT `manufacturer_article` FROM `shop_docpart_articles_analogs_list` WHERE `article` = ?;";
		$query = $db_link->prepare($sql);
		$query->execute( array($article) );
		while($rov = $query->fetch() ){
			if(array_search($rov['manufacturer_article'], $list_manufacturer) === false){
				$list_manufacturer[] = $rov['manufacturer_article'];
			}
		}
		
		$sql = "SELECT `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE `analog` = ?;";
		$query = $db_link->prepare($sql);
		$query->execute( array($article) );
		while($rov = $query->fetch() ){
			if(array_search($rov['manufacturer_analog'], $list_manufacturer) === false){
				$list_manufacturer[] = $rov['manufacturer_analog'];
			}
		}
		
		sort($list_manufacturer);
		
		$answer['status'] = true;
		$answer['list_manufacturer'] = json_encode($list_manufacturer);
		
		break;
}
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('status' => false, 'message' => 'query_failed', 'error' => $e->getMessage())));
}
exit(json_encode($answer));
