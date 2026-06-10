<?php
/**
 * Скрипт для обработки различных операций над таблицей
*/



// формируем пагинацию
// $all 		= количество постов в категории (определяем количество постов в базе данных)
// $lim 		= количество постов, размещаемых на одной странице
// $prev 		= количество отображаемых ссылок до и после номера текущей страницы
// $curr_link 	= номер текущей страницы (получаем из URL)
// $curr_css 	= css-стиль для ссылки на "текущую (активную)" страницу
// $link 		= часть адреса, используемый для формирования линков на другие страницы
function pagination($all, $lim, $prev, $curr_link, $curr_css, $link)
{
    $html = '';
	// осуществляем проверку, чтобы выводимые первая и последняя страницы
    // не вышли за границы нумерации
    $first = $curr_link - $prev;
    if ($first < 1) $first = 1;
    $last = $curr_link + $prev;
    if ($last > ceil($all/$lim)) $last = ceil($all/$lim);
 
    // начало вывода нумерации
    // выводим первую страницу
    $y = 1;
    if ($first > 1) $html .= "<a onclick='go_to_page({$y})'>1</a>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<a onclick='go_to_page({$y})'>...</a>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<a onclick='go_to_page({$y})'>$i</a>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= '<a class="'.$curr_css.'">'. $i .'</a>';
        } else {
            $alink = "<a onclick='go_to_page(";
            if($i != 1) $alink .= "{$i}";
            $alink .= ")'>$i</a>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < ceil($all / $lim) && ceil($all / $lim) - $last > 2) $html .=  "<a onclick='go_to_page({$y})'>...</a>";
    // выводим последнюю страницу
    $e = ceil($all / $lim);
    if ($last < ceil($all / $lim)) $html .=  "<a onclick='go_to_page({$e})'>$e</a>";
	
	return $html;
}

// Функция формирования WHERE строки для запроса. Используется при отображении таблицы и удалении позиций с учетом фильтра
function get_where($where_object)
{
	global $binding_values;
	
	$where = '';
	
	if(!empty($where_object))
	{
		$search_text = trim(urldecode($where_object['search_text']));
		$price_id = (int) $where_object['price_id'];
		$article = trim($where_object['article']);
		$manufacturer = trim($where_object['manufacturer']);
		$no_article = (int) $where_object['no_article'];
		$no_manufacturer = (int) $where_object['no_manufacturer'];
		
		$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article), "UTF-8");
		$manufacturer = htmlentities(mb_strtoupper(trim($manufacturer), "UTF-8"), ENT_QUOTES, "UTF-8");
		
		if(!empty($price_id))
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "(`price_id` IN(?))";
			array_push($binding_values, $price_id);
		}
		
		if($article !== '')
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "(`article` LIKE ?)";
			array_push($binding_values, $article);
		}
		
		if($manufacturer !== '')
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "(`manufacturer` LIKE ?)";
			array_push($binding_values, $manufacturer);
		}
		
		if(!empty($no_article))
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "(`article` LIKE ?)";
			array_push($binding_values, '');
		}
		
		if(!empty($no_manufacturer))
		{
			if($where != ''){$where .= ' AND ';}
			$where .= "(`manufacturer` LIKE ?)";
			array_push($binding_values, '');
		}
		
		////////////////////////////////////////////////////////////////////////////////////
		
		if(!empty($search_text))
		{
			$search_text_arr = explode(' ', $search_text);
			$where_tmp = '';
			
			
			$where_tmp_2 = '';
			if($where_tmp != ''){$where_tmp .= ' OR ';}
			foreach($search_text_arr as $search_text_item){
				if(mb_strlen($search_text_item, 'UTF-8') >= 3){
					if($where_tmp_2 != ''){$where_tmp_2 .= ' AND ';}
					$where_tmp_2 .= "(`article` LIKE ?)";
					array_push($binding_values, '%'.$search_text_item.'%');
				}
			}
			$where_tmp .= $where_tmp_2;
			if($where_tmp != ''){$where_tmp = '('. $where_tmp .')';}
			
			
			$where_tmp_2 = '';
			if($where_tmp != ''){$where_tmp .= ' OR ';}
			foreach($search_text_arr as $search_text_item){
				if(mb_strlen($search_text_item, 'UTF-8') >= 3){
					if($where_tmp_2 != ''){$where_tmp_2 .= ' AND ';}
					$where_tmp_2 .= "(`manufacturer` LIKE ?)";
					array_push($binding_values, '%'.$search_text_item.'%');
				}
			}
			$where_tmp .= $where_tmp_2;
			if($where_tmp != ''){$where_tmp = '('. $where_tmp .')';}
			
			
			$where_tmp_2 = '';
			if($where_tmp != ''){$where_tmp .= ' OR ';}
			foreach($search_text_arr as $search_text_item){
				if(mb_strlen($search_text_item, 'UTF-8') >= 3){
					if($where_tmp_2 != ''){$where_tmp_2 .= ' AND ';}
					$where_tmp_2 .= "(`name` LIKE ?)";
					array_push($binding_values, '%'.$search_text_item.'%');
				}
			}
			$where_tmp .= $where_tmp_2;
			if($where_tmp != ''){$where_tmp = '('. $where_tmp .')';}
			
			
			if($where_tmp != ''){
				if($where != ''){$where .= ' AND ';}
				$where .= '('. $where_tmp .')';
			}
		}
		
		////////////////////////////////////////////////////////////////////////////////////
		
		if(!empty($where)){
			$where = 'WHERE ' . $where;
		}
	}
	
	return $where;
}




//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");





//Проверяем право менеджера
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
if( ! DP_User::isAdmin())
{
	$answer = array('status'=>false);
	exit(json_encode($answer));
}

$answer = array('status'=>false);
$request_object = json_decode($_POST['request_object'], true);

$sweep = array(" ", "-", "_", "`", "/", "'", '"', "\\", ".", ",", "#", "\r\n", "\r", "\n", "\t");

// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------





switch($request_object['action'])
{
	case 'get_table':
		$epc_prices_edit_helpers = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir
			. '/content/shop/prices_edit/epc_prices_edit_helpers.php';
		if (!is_file($epc_prices_edit_helpers)) {
			$epc_prices_edit_helpers = __DIR__ . '/epc_prices_edit_helpers.php';
		}
		if (!is_file($epc_prices_edit_helpers)) {
			exit('<div class="panel-body"><div class="alert alert-danger">Helper file missing on server. Re-deploy <code>prices_edit/epc_prices_edit_helpers.php</code>.</div></div>');
		}
		require_once $epc_prices_edit_helpers;

		$page = (int)$request_object['page'];
		if (empty($page)) {
			$page = 1;
		}

		$kol = 20;
		$art = ($page * $kol) - $kol;

		$where_object = isset($request_object['where_object']) ? $request_object['where_object'] : array();
		$preview_group_id = isset($where_object['preview_group_id']) ? (int)$where_object['preview_group_id'] : 0;
		$min_margin = isset($where_object['min_margin']) ? (float)$where_object['min_margin'] : 0;
		$hide_hidden = !empty($where_object['hide_hidden']);

		$binding_values = array();
		$where = get_where($where_object);

		$res = $db_link->prepare("SELECT COUNT(*) AS `count` FROM `shop_docpart_prices_data` $where");
		$res->execute($binding_values);
		$row = $res->fetch();
		$total = (int)$row['count'];

		$sql = "SELECT * FROM `shop_docpart_prices_data` $where ORDER BY `article` LIMIT $art, $kol;";
		$query = $db_link->prepare($sql);
		$query->execute($binding_values);

		$price_names = epc_prices_edit_load_price_names($db_link);
		$warehouse_map = epc_prices_edit_load_warehouse_map($db_link);
		$profiles = epc_prices_edit_load_profiles($db_link);
		$profile_title = 'Site price';
		foreach ($profiles as $prof) {
			if ((int)$prof['id'] === $preview_group_id) {
				$profile_title = 'Site (' . $prof['value'] . ')';
				break;
			}
		}

		$rows_html = '';
		$shown = 0;
		while ($rov = $query->fetch()) {
			$pid = (int)$rov['price_id'];
			$mfr_raw = html_entity_decode((string)$rov['manufacturer'], ENT_QUOTES, 'UTF-8');
			$article_link = trim((string)$rov['article_show']) !== '' ? (string)$rov['article_show'] : (string)$rov['article'];
			$site_url = epc_prices_edit_site_url($DP_Config, $article_link, $mfr_raw);

			$warehouse_label = '—';
			$office_id = 0;
			$storage_id = 0;
			if (isset($warehouse_map[$pid])) {
				$warehouse_label = $warehouse_map[$pid]['label'];
				$office_id = (int)$warehouse_map[$pid]['office_id'];
				$storage_id = (int)$warehouse_map[$pid]['storage_id'];
			}
			$row_storage = html_entity_decode(trim((string)$rov['storage']), ENT_QUOTES, 'UTF-8');
			if ($row_storage !== '') {
				$warehouse_label .= ($warehouse_label === '—' ? '' : '; ') . 'row: ' . $row_storage;
			}

			$price_list_name = isset($price_names[$pid]) ? $price_names[$pid] : ('#' . $pid);
			$base_price = (float)$rov['price'];

			$site_cell = '<span class="text-muted">—</span>';
			$margin_cell = '<span class="text-muted">—</span>';
			if ($preview_group_id > 0) {
				$calc = epc_prices_edit_calc_site_price(
					$db_link,
					$DP_Config,
					$base_price,
					$mfr_raw,
					$preview_group_id,
					$office_id,
					$storage_id
				);
				if (!$calc['visible']) {
					if ($hide_hidden) {
						continue;
					}
					$site_cell = '<span class="text-danger" title="Hidden for this profile">hidden</span>';
					$margin_cell = '—';
				} else {
					if ($min_margin > 0 && ($calc['margin_pct'] === null || $calc['margin_pct'] < $min_margin)) {
						continue;
					}
					$site_cell = '<strong>' . epc_prices_edit_h(number_format((float)$calc['site_price'], 2, '.', '')) . '</strong>';
					$margin_cell = epc_prices_edit_h($calc['margin_pct']) . '%';
				}
			} elseif ($min_margin > 0 || $hide_hidden) {
				continue;
			}

			$id = (int)$rov['id'];
			$shown++;
			$rows_html .= '
			<tr id="show_line_' . $id . '">
				<td class="col-site"><a href="' . epc_prices_edit_h($site_url) . '" target="_blank" rel="noopener" class="btn btn-xs btn-default" title="Open on site"><i class="fa fa-external-link"></i></a></td>
				<td title="' . epc_prices_edit_h('id ' . $pid) . '">' . epc_prices_edit_h($price_list_name) . '</td>
				<td class="col-warehouse">' . epc_prices_edit_h($warehouse_label) . '</td>
				<td class="bgtd">' . epc_prices_edit_h($rov['article']) . '</td>
				<td>' . epc_prices_edit_h($rov['manufacturer']) . '</td>
				<td>' . epc_prices_edit_h($rov['name']) . '</td>
				<td class="center">' . (int)$rov['exist'] . '</td>
				<td class="bgtd right">' . epc_prices_edit_h($rov['price']) . '</td>
				<td class="right">' . $site_cell . '</td>
				<td class="center">' . $margin_cell . '</td>
				<td class="center">' . (int)$rov['time_to_exe'] . '</td>
				<td class="center">' . (int)$rov['min_order'] . '</td>
				<td style="white-space: nowrap;">
					<a onclick="edit(' . $id . ');" class="btn btn-sm btn-primary" title="Edit"><i class="fa fas fa-pencil-alt"></i></a>
					<a onclick="del(' . $id . ');" class="btn btn-sm btn-primary" title="Delete"><i class="fa fa-times"></i></a>
				</td>
			</tr>
			<tr class="hidden epc-edit-row" id="edit_line_' . $id . '">
				<td></td>
				<td><input class="form-control" type="text" id="price_id_edit_' . $id . '" value="' . $pid . '"/></td>
				<td></td>
				<td><input class="form-control" type="text" id="article_edit_' . $id . '" value="' . epc_prices_edit_h($rov['article']) . '"/></td>
				<td><input class="form-control" type="text" id="manufacturer_edit_' . $id . '" value="' . epc_prices_edit_h($rov['manufacturer']) . '"/></td>
				<td><input type="hidden" id="article_show_edit_' . $id . '" value="' . epc_prices_edit_h($rov['article_show']) . '"/>
					<input class="form-control" type="text" id="name_edit_' . $id . '" value="' . epc_prices_edit_h($rov['name']) . '"/></td>
				<td><input class="form-control" type="number" id="exist_edit_' . $id . '" value="' . (int)$rov['exist'] . '"/></td>
				<td><input class="form-control" type="number" id="price_edit_' . $id . '" value="' . epc_prices_edit_h($rov['price']) . '"/></td>
				<td colspan="2"></td>
				<td><input class="form-control" type="number" id="time_to_exe_edit_' . $id . '" value="' . (int)$rov['time_to_exe'] . '"/>
					<input type="hidden" id="storage_edit_' . $id . '" value="' . epc_prices_edit_h($rov['storage']) . '"/></td>
				<td><input class="form-control" type="number" id="min_order_edit_' . $id . '" value="' . (int)$rov['min_order'] . '"/></td>
				<td style="white-space: nowrap;">
					<a onclick="edit_save(' . $id . ');" class="btn btn-sm btn-primary"><i class="fa fa-floppy-o"></i> Save</a>
					<a onclick="edit_otmena(' . $id . ');" class="btn btn-sm btn-primary"><i class="fa fa-chevron-left"></i> Cancel</a>
				</td>
			</tr>';
		}

		if ($rows_html !== '') {
			$filter_note = '';
			if ($min_margin > 0 || $hide_hidden) {
				$filter_note = '<p class="text-muted" style="margin:8px 0 0;"><small>Profile filters apply to rows on this page (' . $shown . ' shown).</small></p>';
			}
			$html = '<table class="table table epc-prices-table"><thead><tr>
						<th class="col-site">Site</th>
						<th>Price list</th>
						<th>Warehouse</th>
						<th>Article</th>
						<th>Manufacturer</th>
						<th>Name</th>
						<th class="center">Qty</th>
						<th class="right">Base price</th>
						<th class="right">' . epc_prices_edit_h($profile_title) . '</th>
						<th class="center">Margin %</th>
						<th class="center">Lead</th>
						<th class="center">Min.</th>
						<th></th>
					</tr></thead><tbody>' . $rows_html . '</tbody></table>' . $filter_note;

			$pagination = pagination($total, $kol, 3, $page, 'pagination_active', '');
			if ($pagination != '<a class="pagination_active">1</a>') {
				$pagination = '<div class="panel-footer"><div class="pagination_box">' . $pagination . '</div></div>';
			} else {
				$pagination = '';
			}

			$html = '<div class="panel-body">' . $html . '</div>' . $pagination;
		} else {
			$html = '<div class="panel-body">Nothing found</div>';
		}

		exit($html);
		break;
	case 'add':
		$price_id 				= (int) $request_object['price_id'];
		$article 				= strip_tags(mb_strtoupper(trim(urldecode($request_object['article'])), 'UTF-8'));
		$manufacturer 			= strip_tags(mb_strtoupper(trim(urldecode($request_object['manufacturer'])), 'UTF-8'));
		$article_show 			= strip_tags(trim(urldecode($request_object['article_show'])));
		$name				 	= strip_tags(trim(urldecode($request_object['name'])));
		$exist				 	= (int) preg_replace("/[^0-9]+/", "", $request_object['exist']);
		$price				 	= number_format( (float) trim($request_object['price']), 2, '.', '');
		$time_to_exe		 	= (int) trim($request_object['time_to_exe']);
		$storage			 	= htmlentities(strip_tags(trim(urldecode($request_object['storage']))), ENT_QUOTES, "UTF-8");
		$min_order			 	= (int) trim($request_object['min_order']);
		
		$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article), "UTF-8");
		$manufacturer = htmlentities(mb_strtoupper(trim($manufacturer), "UTF-8"), ENT_QUOTES, "UTF-8");
		$article_show = htmlentities($article, ENT_QUOTES, "UTF-8");
		$name = htmlentities(str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), "", $name), ENT_QUOTES, "UTF-8");
		
		if(empty($article_show)){$article_show = $article;}
		
		if(($article !== '') && ($manufacturer !== '') && !empty($price))
		{
			$sql = "INSERT INTO `shop_docpart_prices_data`(`price_id`, `manufacturer`, `article`, `article_show`, `name`, `exist`, `price`, `time_to_exe`, `storage`, `min_order`) VALUES (?,?,?,?,?,?,?,?,?,?)";
			if($db_link->prepare($sql)->execute( array($price_id, $manufacturer, $article, $article_show, $name, $exist, $price, $time_to_exe, $storage, $min_order) ))
			{
				$answer['status'] = true;
			}
		}
		break;
	case 'save':
		$id 					= (int) $request_object['id'];
		$price_id 				= (int) $request_object['price_id'];
		$article 				= strip_tags(mb_strtoupper(trim(urldecode($request_object['article'])), 'UTF-8'));
		$manufacturer 			= strip_tags(mb_strtoupper(trim(urldecode($request_object['manufacturer'])), 'UTF-8'));
		$article_show 			= strip_tags(trim(urldecode($request_object['article_show'])));
		$name				 	= strip_tags(trim(urldecode($request_object['name'])));
		$exist				 	= (int) preg_replace("/[^0-9]+/", "", $request_object['exist']);
		$price				 	= number_format( (float) trim($request_object['price']), 2, '.', '');
		$time_to_exe		 	= (int) trim($request_object['time_to_exe']);
		$storage			 	= htmlentities(strip_tags(trim(urldecode($request_object['storage']))), ENT_QUOTES, "UTF-8");
		$min_order			 	= (int) trim($request_object['min_order']);
		
		$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article), "UTF-8");
		$manufacturer = htmlentities(mb_strtoupper(trim($manufacturer), "UTF-8"), ENT_QUOTES, "UTF-8");
		$article_show = htmlentities($article, ENT_QUOTES, "UTF-8");
		$name = htmlentities(str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), "", $name), ENT_QUOTES, "UTF-8");
		
		if(empty($article_show)){$article_show = $article;}
		
		if(($article !== '') && ($manufacturer !== '') && !empty($price_id))
		{
			$sql = "UPDATE `shop_docpart_prices_data` SET `price_id`=?,`manufacturer`=?,`article`=?,`article_show`=?,`name`=?,`exist`=?,`price`=?,`time_to_exe`=?,`storage`=?,`min_order`=? WHERE `id` = ?;";

			if($db_link->prepare($sql)->execute( array($price_id, $manufacturer, $article, $article_show, $name, $exist, $price, $time_to_exe, $storage, $min_order, $id) ))
			{
				$answer['status'] = true;
			}
		}
		break;
	case 'del':
		$id = (int) $request_object['id'];
		$sql = "DELETE FROM `shop_docpart_prices_data` WHERE `id` = ? LIMIT 1;";
		if($db_link->prepare($sql)->execute( array($id) ))
		{
			$answer['status'] = true;
		}
		break;
	case 'del_search':
		
		$binding_values = array();
		$where = get_where($request_object['where_object']);
		
		$sql = "DELETE FROM `shop_docpart_prices_data` $where LIMIT 10000;";

		do{
			$query = $db_link->prepare($sql);
			$query->execute($binding_values);
		}while($query->fetchColumn() > 0);
		
		$answer['status'] = true;
		
		break;
}
exit(json_encode($answer));
?>