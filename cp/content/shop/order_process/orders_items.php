<?php
/**
 * Страница для вывода всех позиций всех заказов
*/
defined('_ASTEXE_') or die('No access');


function str_replace_once($search, $replace, $text) 
{ 
   $pos = strpos($text, $search); 
   return $pos!==false ? substr_replace($text, $replace, $pos, strlen($search)) : $text; 
}

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
    if ($first < 1) $first = 0;
		$last = $curr_link + $prev;
		
		$count_pages = (int)($all / $lim);//Количество страниц
		if($all%$lim)//Если остались еще элементы
		{
			$count_pages++;
		}

		$count_pages = $count_pages - 1;

    if ($last > $count_pages) $last = $count_pages;
 
    // начало вывода нумерации
    // выводим первую страницу
    $y = 0;
    if ($first > 0) $html .= "<li class='paginate_button'><a onclick='goToPage({$y})'>0</a></li>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<li class='paginate_button'><a onclick='goToPage({$y})'>...</a></li>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<li class='paginate_button'><a onclick='goToPage({$y})'>$i</a></li>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= "<li class='paginate_button ".$curr_css."'><a>". $i ."</a></li>";
        } else {
            $alink = "<li class='paginate_button'><a onclick='goToPage(";
            if($i != 0) $alink .= "{$i}";
            $alink .= ")'>$i</a></li>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < $count_pages && $count_pages - $last > 2) $html .=  "<li class='paginate_button'><a onclick='goToPage({$y})'>...</a></li>";
    // выводим последнюю страницу
    $e = $count_pages;
    if ($last < $count_pages) $html .=  "<li class='paginate_button'><a onclick='goToPage({$e})'>$e</a></li>";
	
	return $html;
}


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$manager_id = DP_User::getAdminId();//ID менежера, который отображает эту страницу



//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/order_process/orders_background.php")
?>

<?php
if(!empty($_POST["action"]))
{
    
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    ?>
	
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
		<div class="col-lg-12 epc-scp-panel epc-oi-page epc-orders-page">
		<div class="epc-orders-page__hero" style="margin-bottom:12px;">
			<div>
				<h2 style="margin:0 0 6px;font-size:22px;font-weight:800;"><i class="fa fa-list-alt"></i> Orders items</h2>
				<p style="margin:0;color:#64748b;font-size:13px;">Filter line items across orders. Open an order in the <strong>one-page OMS</strong> to manage it.</p>
			</div>
			<div class="epc-orders-page__hero-actions">
				<a class="btn btn-primary btn-sm" href="/<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/orders/orders"><i class="fa fa-columns"></i> One-page OMS</a>
				<button type="button" class="btn btn-info btn-sm" onclick="itemsInProcess();"><i class="fa fa-bolt"></i> <?php echo translate_str_by_id(5314); ?></button>
			</div>
		</div>
	<div class="epc-scp-orders-filter">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
					<a class="showhide"><i class="fa fa-chevron-up"></i></a>
				</div>
				<?php echo translate_str_by_id(3601); ?>
			</div>
			<div class="panel-body filter_panel">
				<?php
				$time_from = "";
				$time_to = "";
				$order_id = "";
				$order_status = "0";
				$paid = -1;
				$customer = "";
				$customer_id = "";
				$order_item_status = "0";
				$office_id = "0";
				$product_name = "";
				$article = "";
				$manufacturer = "";
				$viewed = -1;
				$storage_id = -1;
				$phone = "";

				$orders_items_filter = NULL;
				if (isset($_COOKIE["orders_items_filter"])) {
					$orders_items_filter = $_COOKIE["orders_items_filter"];
				}
				if ($orders_items_filter != NULL) {
					$orders_items_filter = json_decode($orders_items_filter, true);
					if (is_array($orders_items_filter)) {
						$time_from = isset($orders_items_filter["time_from"]) ? $orders_items_filter["time_from"] : "";
						$time_to = isset($orders_items_filter["time_to"]) ? $orders_items_filter["time_to"] : "";
						$order_id = isset($orders_items_filter["order_id"]) ? $orders_items_filter["order_id"] : "";
						$order_status = isset($orders_items_filter["order_status"]) ? $orders_items_filter["order_status"] : "0";
						$paid = isset($orders_items_filter["paid"]) ? $orders_items_filter["paid"] : -1;
						$customer = isset($orders_items_filter["customer"]) ? $orders_items_filter["customer"] : "";
						$customer_id = isset($orders_items_filter["customer_id"]) ? $orders_items_filter["customer_id"] : "";
						$order_item_status = isset($orders_items_filter["order_item_status"]) ? $orders_items_filter["order_item_status"] : "0";
						$office_id = isset($orders_items_filter["office_id"]) ? $orders_items_filter["office_id"] : "0";
						$product_name = isset($orders_items_filter["product_name"]) ? $orders_items_filter["product_name"] : "";
						$article = isset($orders_items_filter["article"]) ? $orders_items_filter["article"] : "";
						$manufacturer = isset($orders_items_filter["manufacturer"]) ? $orders_items_filter["manufacturer"] : "";
						$viewed = isset($orders_items_filter["viewed"]) ? $orders_items_filter["viewed"] : -1;
						$storage_id = isset($orders_items_filter["storage_id"]) ? $orders_items_filter["storage_id"] : -1;
						$phone = isset($orders_items_filter["phone"]) ? $orders_items_filter["phone"] : "";
					}
				}

				$epc_oi_date_show = static function ($unix) {
					$unix = (int) $unix;
					return $unix > 0 ? date('d.m.Y H:i', $unix) : '';
				};
				$time_from_show = $epc_oi_date_show($time_from);
				$time_to_show = $epc_oi_date_show($time_to);

				$fields_for_customer_search = "ID, E-mail, ".translate_str_by_id(1312);
				$users_profile_fields_query = $db_link->prepare("SELECT `caption` FROM `reg_fields` WHERE `to_users_table` = 1;");
				$users_profile_fields_query->execute();
				while ($users_profile_field = $users_profile_fields_query->fetch()) {
					$fields_for_customer_search .= ", ".$users_profile_field["caption"];
				}
				$phone_show = str_replace(array("+7","+375","+380"), "", (string) $phone);
				if ($phone_show !== '' && function_exists('urldecode')) {
					$phone_show = rawurldecode($phone_show);
				}
				$product_name_show = $product_name;
				$article_show = $article;
				$manufacturer_show = $manufacturer;
				if (function_exists('urldecode')) {
					if ($product_name_show !== '') $product_name_show = rawurldecode((string)$product_name_show);
					if ($article_show !== '') $article_show = rawurldecode((string)$article_show);
					if ($manufacturer_show !== '') $manufacturer_show = rawurldecode((string)$manufacturer_show);
				}
				?>
				<div class="epc-orders-filter-grid">
					<div class="epc-orders-filter-field<?php echo $time_from_show !== '' ? ' is-active' : ''; ?>">
						<label for="time_from_show"><?php echo translate_str_by_id(3237); ?></label>
						<input type="hidden" id="time_from" value="<?php echo htmlspecialchars((string)$time_from, ENT_QUOTES, 'UTF-8'); ?>" />
						<input type="text" id="time_from_show" class="form-control" value="<?php echo htmlspecialchars($time_from_show, ENT_QUOTES, 'UTF-8'); ?>" placeholder="dd.mm.yyyy hh:mm" autocomplete="off" />
					</div>
					<div class="epc-orders-filter-field<?php echo $time_to_show !== '' ? ' is-active' : ''; ?>">
						<label for="time_to_show"><?php echo translate_str_by_id(3238); ?></label>
						<input type="hidden" id="time_to" value="<?php echo htmlspecialchars((string)$time_to, ENT_QUOTES, 'UTF-8'); ?>" />
						<input type="text" id="time_to_show" class="form-control" value="<?php echo htmlspecialchars($time_to_show, ENT_QUOTES, 'UTF-8'); ?>" placeholder="dd.mm.yyyy hh:mm" autocomplete="off" />
					</div>
					<div class="epc-orders-filter-field<?php echo $order_id !== '' ? ' is-active' : ''; ?>">
						<label for="order_id"><?php echo translate_str_by_id(1082); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3202); ?>');"><i class="fa fa-info"></i></button></label>
						<input type="text" id="order_id" value="<?php echo htmlspecialchars((string)$order_id, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field" id="paid_div">
						<label for="paid"><?php echo translate_str_by_id(3584); ?></label>
						<select multiple="multiple" id="paid">
							<option value="1"><?php echo translate_str_by_id(3514); ?></option>
							<option value="2"><?php echo translate_str_by_id(3515); ?></option>
							<option value="0"><?php echo translate_str_by_id(3513); ?></option>
						</select>
					</div>
					<div class="epc-orders-filter-field" id="order_status_div">
						<label for="order_status"><?php echo translate_str_by_id(3603); ?></label>
						<select multiple="multiple" id="order_status">
							<?php foreach ($orders_statuses as $status_id => $status_data) { ?>
							<option value="<?php echo (int)$status_id; ?>"><?php echo translate_str_by_id($status_data["name"]); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="epc-orders-filter-field" id="order_item_status_div">
						<label for="order_item_status"><?php echo translate_str_by_id(3604); ?></label>
						<select multiple="multiple" id="order_item_status">
							<?php foreach ($orders_items_statuses as $status_id => $status_data) { ?>
							<option value="<?php echo (int)$status_id; ?>"><?php echo translate_str_by_id($status_data["name"]); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="epc-orders-filter-field<?php echo $customer_id !== '' ? ' is-active' : ''; ?>">
						<label for="customer_id"><?php echo translate_str_by_id(3818); ?></label>
						<input type="text" id="customer_id" value="<?php echo htmlspecialchars((string)$customer_id, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field<?php echo $customer !== '' ? ' is-active' : ''; ?>">
						<label for="customer"><?php echo translate_str_by_id(3245); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3579); ?>: <?php echo htmlspecialchars($fields_for_customer_search, ENT_QUOTES, 'UTF-8'); ?>. <?php echo translate_str_by_id(3580); ?>');"><i class="fa fa-info"></i></button></label>
						<input type="text" id="customer" value="<?php echo htmlspecialchars((string)$customer, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field" id="office_id_div">
						<label for="office_id"><?php echo translate_str_by_id(3506); ?></label>
						<select multiple="multiple" id="office_id">
							<?php foreach ($offices_list as $office_id_key => $office_name) { ?>
							<option value="<?php echo (int)$office_id_key; ?>"><?php echo translate_str_by_id($office_name); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="epc-orders-filter-field<?php echo $manufacturer_show !== '' ? ' is-active' : ''; ?>">
						<label for="manufacturer"><?php echo translate_str_by_id(2070); ?></label>
						<input type="text" id="manufacturer" value="<?php echo htmlspecialchars((string)$manufacturer_show, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field<?php echo $article_show !== '' ? ' is-active' : ''; ?>">
						<label for="article"><?php echo translate_str_by_id(2071); ?></label>
						<input type="text" id="article" value="<?php echo htmlspecialchars((string)$article_show, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field<?php echo $product_name_show !== '' ? ' is-active' : ''; ?>">
						<label for="product_name"><?php echo translate_str_by_id(2102); ?></label>
						<input type="text" id="product_name" value="<?php echo htmlspecialchars((string)$product_name_show, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field" id="viewed_div">
						<label for="viewed"><?php echo translate_str_by_id(3605); ?></label>
						<select multiple="multiple" id="viewed">
							<option value="1"><?php echo translate_str_by_id(3581); ?></option>
							<option value="0"><?php echo translate_str_by_id(3582); ?></option>
						</select>
					</div>
					<div class="epc-orders-filter-field" id="storage_id_div">
						<label for="storage_id"><?php echo translate_str_by_id(3606); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3607); ?> &#34;<?php echo translate_str_by_id(2094); ?>&#34;');"><i class="fa fa-info"></i></button></label>
						<select multiple="multiple" id="storage_id">
							<?php
							$storages_query = $db_link->prepare("SELECT * FROM `shop_storages` ORDER BY `name`;");
							$storages_query->execute();
							while ($storage = $storages_query->fetch()) {
								$label = $storage["name"] . ' - id ' . $storage["id"];
								if (!empty($storage["short_name"])) {
									$label .= ' - ' . $storage["short_name"];
								}
								?>
							<option value="<?php echo (int)$storage["id"]; ?>"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="epc-orders-filter-field<?php echo $phone_show !== '' ? ' is-active' : ''; ?>">
						<label for="phone"><?php echo translate_str_by_id(1312); ?></label>
						<input type="text" id="phone" value="<?php echo htmlspecialchars((string)$phone_show, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
				</div>
			</div>
			<div class="panel-footer epc-scp-filter-bar">
				<button class="btn btn-success" type="button" onclick="filterOrdersItems();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
				<button class="btn btn-primary" type="button" onclick="unsetFilterOrdersItems();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2555); ?></button>
				<button class="btn btn-info" type="button" onclick="itemsInProcess();"><i class="fa fa-toolbox"></i> <?php echo translate_str_by_id(5314); ?></button>
			</div>
		</div>
	</div>

<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3498); ?> <span class="text-muted" style="font-weight:500;font-size:12px;">· click Order # to open OMS</span>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<?php
					// ---------- Start SAO ----------
					//Предварительно получаем список возможных SAO-действий:
					$sao_actions = array();
					
					$sao_actions_query = $db_link->prepare("SELECT * FROM `shop_sao_actions`");
					$sao_actions_query->execute();
					while( $sao_action = $sao_actions_query->fetch() )
					{
						$sao_actions[$sao_action["id"]] = array();
						$sao_actions[$sao_action["id"]]["name"] = $sao_action["name"];
						$sao_actions[$sao_action["id"]]["script"] = $sao_action["script"];
						$sao_actions[$sao_action["id"]]["fontawesome"] = $sao_action["fontawesome"];
						$sao_actions[$sao_action["id"]]["btn_class"] = $sao_action["btn_class"];
					}
					
					//Подключаем протокол выполнения действий
					$sao_propocol_mode = 2;//Режим работы протокола - страница "Позиции заказов"
					require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/sao/actions_exec_propocol.php");
					// ---------- End SAO ----------
					
					
					
					//Определяем текущую сортировку и обозначаем ее:
					$orders_items_sort = NULL;
					if( isset($_COOKIE["orders_items_sort"]) )
					{
						$orders_items_sort = $_COOKIE["orders_items_sort"];
					}
					$sort_field = "id";
					$sort_asc_desc = "desc";
					if($orders_items_sort != NULL)
					{
						$orders_items_sort = json_decode($orders_items_sort, true);
						$sort_field = $orders_items_sort["field"];
						$sort_asc_desc = $orders_items_sort["asc_desc"];
					}
					
					if( strtolower($sort_asc_desc) == "asc" )
					{
						$sort_asc_desc = "asc";
					}
					else
					{
						$sort_asc_desc = "desc";
					}
					
					
					if( array_search($sort_field, array('id', 'product_name', 'article', 'manufacturer', 'price', 'count_need', 'price_sum', 'price_purchase_sum', 'profit', 'status', 'time', 'order_id', 'office_id', 't2_time_to_exe', 'customer', 'customer_id', 'checks_count') ) === false )
					{
						$sort_field = "id";
					}
					
					
					//Формируем сложный SQL-запрос для получения всей информации по каждой позиции
					$binding_values = array();
					
					//Запрос времени оформления заказа
					$SELECT_time = "(SELECT `time` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
					
					//Запрос суммы позиции
					$SELECT_price_sum = "CAST(`price`*`count_need` AS DECIMAL(20,2))";
					
					//Запрос офисов обслуживания
					$SELECT_offices = "(SELECT `office_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
					
					
					//Запрос клиента
					//Формируем подзапрос для значений профиля пользователя (только для тех полей, которые выводятся в таблицу пользователей в менеджер пользователей колонками)
					$users_profile_SQL = "";
					$users_profile_fields_query = $db_link->prepare("SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1;");
					$users_profile_fields_query->execute();
					while( $users_profile_field = $users_profile_fields_query->fetch() )
					{
						if( $users_profile_SQL != "" )
						{
							$users_profile_SQL = $users_profile_SQL.",";
						}
						
						//Допустимы только буквы и знаки нижнего подчеркивания
						$field_name = str_replace( array(' ', '#', '-', "'", '"'), '', $users_profile_field["name"] );
						
						$users_profile_SQL = $users_profile_SQL." IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = (SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id` LIMIT 1 )), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = (SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id` LIMIT 1 ))),'') ";
					}
					if( $users_profile_SQL != "" )
					{
						$users_profile_SQL = ",".$users_profile_SQL;
					}
					//SQL-подзапрос компонует строку с данными пользователя
					$SELECT_clients = " IF( (SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id` ) = 0, CONCAT('".translate_str_by_id(3233)." (ID 0)', IF( (SELECT `phone_not_auth` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)='', '', CONCAT(', ".translate_str_by_id(1312).": ', (SELECT `phone_not_auth` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) )), IF( (SELECT `email_not_auth` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)='', '', CONCAT(', E-mail: ', (SELECT `email_not_auth` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) ))), CONCAT( 'ID ', (SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id` ), ', E-mail: ', (SELECT IF(`email`!='', `email`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = (SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id` LIMIT 1 ) LiMIT 1 ), ', ".translate_str_by_id(1312).": ', (SELECT IF(`phone`!='', `phone`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = (SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id` LIMIT 1 ) LiMIT 1 ) ".$users_profile_SQL." ) )";
					
					
					
					//Запрос закупа
					$SELECT_price_purchase_sum = "IFNULL((SELECT SUM(`price_purchase`*(`count_reserved`+`count_issued`+`count_canceled`)) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`), CAST(`t2_price_purchase`*`count_need` AS DECIMAL(20,2)))";
					//Запрос маржы
					$SELECT_profit = "CAST(($SELECT_price_sum - $SELECT_price_purchase_sum) AS DECIMAL(20,2))";
					//Запрос статуса заказа
					$SELECT_order_status = "(SELECT `status` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
					//Запрос флаг "Заказ оплачен"
					$SELECT_paid = "(SELECT `paid` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
					
					//Запрос флага "Заказ просмотрен" viewed_flag
					$SELECT_viewed = " IFNULL( (SELECT `viewed_flag` FROM `shop_orders_viewed` WHERE `order_id` = `shop_orders_items`.`order_id` AND `user_id` = ? LIMIT 1), 1 ) ";
					
					array_push($binding_values, $manager_id);
					
					
					
					//SAO
					$SELECT_item_sao_state = "IFNULL( (SELECT `name` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state` ), '')";
					$SELECT_item_sao_color_background = "IFNULL( (SELECT `color_background` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state` ), '')";
					$SELECT_item_sao_color_text = "IFNULL( (SELECT `color_text` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state` ), '')";
					//Получаем через запятую возможные дейстия для SAO для данного состояния и данного поставщика
					$SELECT_item_sao_actions = " IFNULL(( SELECT GROUP_CONCAT(`id` SEPARATOR ',') FROM `shop_sao_actions` WHERE id IN (SELECT `action_id` FROM `shop_sao_states_types_actions_link` WHERE `state_type_id` = (SELECT `id` FROM `shop_sao_states_types_link` WHERE `state_id` = `shop_orders_items`.`sao_state` AND `interface_type_id` =  (SELECT `interface_type` FROM `shop_storages` WHERE `id` = `shop_orders_items`.`t2_storage_id` ) )) ), '')";
					
					
					
					//Данные о способе получения:
					$SELECT_how_get_caption = "(SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = (SELECT `how_get` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)  )";
					$SELECT_how_get_handler = "(SELECT `handler` FROM `shop_obtaining_modes` WHERE `id` = (SELECT `how_get` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)  )";
					$SELECT_how_get_json = "(SELECT `how_get_json` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
					
					
					
					
					//Фильтры
					$WHERE_CONDITIONS = " WHERE ";

					//По офисам обслуживания - только те, с которыми работает данный менеджер
					$sub_WHERE_offices = "";
					foreach($offices_list as $office_id => $office_caption)
					{
						if(isset($orders_items_filter["office_id"]) && $orders_items_filter["office_id"] != 0 && !in_array($office_id, $orders_items_filter["office_id"]))
						{
							continue;//Если выбран в фильтре офис
						}
						
						if($sub_WHERE_offices != "")$sub_WHERE_offices .= ",";
						$sub_WHERE_offices .= "?";
						
						array_push($binding_values, $office_id);
					}
					if($sub_WHERE_offices == "")
					{
						$WHERE_CONDITIONS .= "0=1";
					}
					else
					{
						$WHERE_CONDITIONS .= "$SELECT_offices IN ($sub_WHERE_offices)";
					}

					//Ставим ПОЛЬЗОВАТЕЛЬСКИЕ фильтры
					$orders_items_filter = NULL;
					if( isset($_COOKIE["orders_items_filter"]) )
					{
						$orders_items_filter = $_COOKIE["orders_items_filter"];
					}
					if($orders_items_filter != NULL)
					{
						$orders_items_filter = json_decode($orders_items_filter, true);

						//1. Время с
						if($orders_items_filter["time_from"] != "")
						{
							$WHERE_CONDITIONS .= " AND $SELECT_time > ?";
							
							array_push($binding_values, $orders_items_filter["time_from"]);
						}

						//2. Время по
						if($orders_items_filter["time_to"] != "")
						{
							$WHERE_CONDITIONS .= " AND $SELECT_time < ?";
							
							array_push($binding_values, $orders_items_filter["time_to"]);
						}

						//3. Номер заказа
						if($orders_items_filter["order_id"] != "")
						{
							$WHERE_CONDITIONS .= " AND `order_id` = ?";
							
							array_push($binding_values, $orders_items_filter["order_id"]);
						}
						
						//4. Статус заказа
						if($orders_items_filter["order_status"] != 0)
						{
							$WHERE_CONDITIONS .= " AND $SELECT_order_status IN (". str_repeat("?,", count($orders_items_filter["order_status"]) - 1) ."?)";

							$binding_values = array_merge($binding_values, $orders_items_filter["order_status"]);
						}
						
						//5. Оплата
						if($orders_items_filter["paid"] != -1)
						{
							$WHERE_CONDITIONS .= " AND $SELECT_paid IN (". str_repeat("?,", count($orders_items_filter["paid"]) - 1) ."?)";

							$binding_values = array_merge($binding_values, $orders_items_filter["paid"]);
						}
						
						//6. Покупатель
						if($orders_items_filter["customer"] != "" )
						{
							$WHERE_CONDITIONS .= " AND $SELECT_clients  LIKE ?";
							
							array_push($binding_values, "%".htmlentities($orders_items_filter["customer"])."%");
						}
						
						if($orders_items_filter["customer_id"] != "" )
						{
							$WHERE_CONDITIONS .= " AND (SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) = ?";
							
							array_push($binding_values, $orders_items_filter["customer_id"]);
						}
						
						//7. Статус позиции
						if($orders_items_filter["order_item_status"] != 0)
						{
							$WHERE_CONDITIONS .= " AND `status` IN (". str_repeat("?,", count($orders_items_filter["order_item_status"]) - 1) ."?)";

							$binding_values = array_merge($binding_values, $orders_items_filter["order_item_status"]);
						}

						//8. Офис обслуживания
						if($orders_items_filter["office_id"] != 0)
						{
							$WHERE_CONDITIONS .= " AND $SELECT_offices IN (". str_repeat("?,", count($orders_items_filter["office_id"]) - 1) ."?)";

							$binding_values = array_merge($binding_values, $orders_items_filter["office_id"]);
						}

						//9. Наименование
						if($orders_items_filter["product_name"] != "" )
						{
							$WHERE_CONDITIONS .= " AND `t2_name` LIKE ?";
							
							array_push($binding_values, '%'.$orders_items_filter["product_name"].'%');
						}
						
						if($orders_items_filter["article"] != "" )
						{
							$WHERE_CONDITIONS .= " AND `t2_article` LIKE ?";
							
							array_push($binding_values, '%'.$orders_items_filter["article"].'%');
						}
						
						if($orders_items_filter["manufacturer"] != "" )
						{
							$WHERE_CONDITIONS .= " AND `t2_manufacturer` LIKE ?";
							
							array_push($binding_values, '%'.$orders_items_filter["manufacturer"].'%');
						}
						
						//10. Заказ просмотрен
						if($orders_items_filter["viewed"] != -1 && count($orders_items_filter["viewed"]) == 1)
						{
							$orders_items_filter["viewed"] = $orders_items_filter["viewed"][0];
							$WHERE_CONDITIONS .= " AND IFNULL( (SELECT `viewed_flag` FROM `shop_orders_viewed` WHERE `order_id` = `shop_orders_items`.`order_id` AND `user_id` = ? LIMIT 1), 1 ) = ?";
							
							array_push($binding_values, $manager_id);
							array_push($binding_values, $orders_items_filter["viewed"]);
						}
						
						//11. ID Склада
						if($orders_items_filter["storage_id"] != -1)
						{
							$WHERE_CONDITIONS .= " AND IF( `t2_storage_id` = 0, IFNULL((SELECT `storage_id` FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id` AND `storage_id` = ? LIMIT 1 ), 0) , `t2_storage_id` ) IN (". str_repeat("?,", count($orders_items_filter["storage_id"]) - 1) ."?)";

							array_push($binding_values, $orders_items_filter["storage_id"]);
							$binding_values = array_merge($binding_values, $orders_items_filter["storage_id"]);
						}
						
						//12. Телефон клиента
						if($orders_items_filter["phone"] != "" )
						{
							$WHERE_CONDITIONS .= " AND $SELECT_clients  LIKE ?";
							
							array_push($binding_values, "%".htmlentities($orders_items_filter["phone"])."%");
						}
					}
			
					//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
					//---------------------------------------------------------------------------------------------->

					//Определяем, с какой страницы начать вывод:
					$s_page = 0;
					if( isset($_COOKIE['orders_items_need_page']) )
					{
						$s_page = (int) $_COOKIE['orders_items_need_page'];
					}


					//Определяем сколько пропустить записей для выборки
					$p = $DP_Config->list_page_limit;//Штук на страницу
					$start_elements_of_page = abs($s_page * $p);

					// print_r($SQL_SELECT_ITEMS_LIMIT);

					$elements_counter = 0;
					//----------------------------------------------------------------------------------------------|

					//ЗАПРОС 
					$SQL_SELECT_ITEMS = "SELECT SQL_CALC_FOUND_ROWS *, 
						`t2_name` AS `product_name`, 
						`t2_article` AS `article`, 
						`t2_manufacturer` AS `manufacturer`, 
						$SELECT_price_sum AS `price_sum`, 
						$SELECT_offices AS `office_id`, 
						$SELECT_clients AS `customer`, 
						(SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) AS `customer_id`, 
						$SELECT_price_purchase_sum AS `price_purchase_sum`,
						$SELECT_profit AS `profit`,
						$SELECT_time AS `time`,
						$SELECT_order_status AS `order_status`,
						$SELECT_paid AS `paid`,
						$SELECT_viewed AS `viewed_flag`,
						$SELECT_item_sao_state AS `sao_state_name`,
						$SELECT_item_sao_color_background AS `sao_state_color_background`, 
						$SELECT_item_sao_color_text AS `sao_state_color_text`, 
						$SELECT_item_sao_actions AS `sao_actions`,
						$SELECT_how_get_caption AS `how_get_caption`,
						(SELECT COUNT(`id`) FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `order_item_id` = `shop_orders_items`.`id`) AS `checks_count`
						FROM `shop_orders_items` $WHERE_CONDITIONS ORDER BY `$sort_field` $sort_asc_desc LIMIT $start_elements_of_page,$p";
					

					$elements_query = $db_link->prepare($SQL_SELECT_ITEMS);
					$elements_query->execute($binding_values);
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					//Определяем количество страниц для вывода:
					$count_pages = (int)($elements_count_rows / $p);//Количество страниц
					if($elements_count_rows%$p)//Если остались еще элементы
					{
						$count_pages++;
					}
					
					// -------------------------------------------------------------------
					//Получаем суммарные показатели: Сумма всех заказов (price_sum_total), Количество заказов (orders_count), Количество позиций (positions_count), Сумма маржи (profit_sum_total), Сумма закупа (price_purchase_sum_total)
					$SQL_SELECT_TOTAL_INDICATORS = "SELECT 
						SUM($SELECT_price_sum) AS `price_sum_total`,
						COUNT(*) AS `positions_count`,
						COUNT( DISTINCT(`order_id`) ) AS `orders_count`,
						SUM($SELECT_profit) AS `profit_sum_total`,
						SUM($SELECT_price_purchase_sum) AS `price_purchase_sum_total`
						FROM `shop_orders_items` $WHERE_CONDITIONS ";
					
					//Удаляем первый элемент массива связанных значений (это был manager_id для флага "Просмотрен")
					array_shift($binding_values);
					
					$total_indicators_query = $db_link->prepare($SQL_SELECT_TOTAL_INDICATORS);
					$total_indicators_query->execute($binding_values);
					$total_indicators = $total_indicators_query->fetch();
					// -------------------------------------------------------------------
					
					$oi_boot = array(
						'elements_array' => array(),
						'elements_id_array' => array(),
						'orders_items_to_orders_map' => array(),
						'orders_items_ids_to_orders_items_objects' => array(),
					);
					
					?>
					<table id="orders_items_table" class="footable table table-hover toggle-arrow " data-sort="false" data-page-size="<?php echo $elements_count_rows; ?>">
						<thead>
							<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();" /></th>
							<th data-toggle="true"></th>
							<th><a href="javascript:void(0);" onclick="sortOrdersItems('id');" id="id_sorter">ID</a></th>
							<th><a href="javascript:void(0);" onclick="sortOrdersItems('manufacturer');" id="manufacturer_sorter"><?php echo translate_str_by_id(2070); ?></a></th>
							<th><a href="javascript:void(0);" onclick="sortOrdersItems('article');" id="article_sorter"><?php echo translate_str_by_id(2071); ?></a></th>
							<th><a href="javascript:void(0);" onclick="sortOrdersItems('product_name');" id="product_name_sorter"><?php echo translate_str_by_id(2102); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('checks_count');" id="checks_count_sorter"><?php echo translate_str_by_id(3551); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('price');" id="price_sorter"><?php echo translate_str_by_id(2751); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('count_need');" id="count_need_sorter"><?php echo translate_str_by_id(4526); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('price_sum');" id="price_sum_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
							<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('price_purchase_sum');" id="price_purchase_sum_sorter"><?php echo translate_str_by_id(5306); ?></a></th>
							<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('profit');" id="profit_sorter"><?php echo translate_str_by_id(3499); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('status');" id="status_sorter"><?php echo translate_str_by_id(2081); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('customer');" id="customer_sorter"><?php echo translate_str_by_id(3245); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('order_id');" id="order_id_sorter"><?php echo translate_str_by_id(3243); ?></a></th>
							<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('office_id');" id="office_id_sorter"><?php echo translate_str_by_id(3506); ?></a></th>
							<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('t2_time_to_exe');" id="t2_time_to_exe_sorter"><?php echo translate_str_by_id(3550); ?></a></th>
							
							<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('how_get_caption');" id="how_get_caption_sorter"><?php echo translate_str_by_id(608); ?></a></th>
							
							<th data-hide="phone,tablet,default"><a href="javascript:void(0);"><?php echo translate_str_by_id(3608); ?></a></th>
							
							<th data-hide="phone,tablet,default"><?php echo translate_str_by_id(3500); ?></th>
						</thead>
						<tbody>
						<?php
						$items_counter = 0;

						while( $item = $elements_query->fetch() )
						{
							if($elements_counter >= $s_page*$p+$p)
							{
								break;
							}
							$elements_counter++;
							$oi_boot['elements_array'][] = 'checked_' . $item['id'];
							$oi_boot['elements_id_array'][] = (int) $item['id'];
							$oi_boot['orders_items_to_orders_map'][(string) $item['id']] = (int) $item['order_id'];
							$oi_boot['orders_items_ids_to_orders_items_objects'][(string) $item['id']] = array(
								'product_name' => $item['manufacturer'] . ' ' . $item['article'] . ' ' . $item['product_name'],
								'name' => $item['product_name'],
								'article' => $item['article'],
								'manufacturer' => $item['manufacturer'],
								'price' => (float) $item['price'],
								'count_need' => (int) $item['count_need'],
							);
							
							
							$item_id = $item["id"];
							$item_product_type = $item["product_type"];
							$item_status = $item["status"];
							$item_order_id = $item["order_id"];
							$item_product_name = $item["product_name"];
							$item_article = $item["article"];
							$item_manufacturer = $item["manufacturer"];
							$item_price = $item["price"];
							$item_count_need = $item["count_need"];
							$item_price_sum = $item["price_sum"];
							$item_office_id = $item["office_id"];
							$item_customer = $item["customer"];
							
							$customer_id =  $item['customer_id'];//Статистика
							
							$item_how_get_caption = $item["how_get_caption"];
							
							$item_price_purchase_sum = $item["price_purchase_sum"];
							$item_profit = $item["profit"];
							$item_time = $item["time"];
							$item_t2_time_to_exe = $item["t2_time_to_exe"];
							$item_t2_time_to_exe_guaranteed = $item["t2_time_to_exe_guaranteed"];
							
							//Срок доставки для продуктов типа 2
							if($item_t2_time_to_exe < $item_t2_time_to_exe_guaranteed)
							{
								$item_t2_time_to_exe = $item_t2_time_to_exe." - ".$item_t2_time_to_exe_guaranteed;
							}
							$item_t2_time_to_exe = $item_t2_time_to_exe." ".translate_str_by_id(5315).".";
							
							//Флаг "Заказ просмотрен"
							$viewed_class = "";
							$viewed_flag = $item["viewed_flag"];
							if( $viewed_flag == 0)
							{
								$viewed_class = " not_viewed";
							}
							
							
							

							
							//SAO
							$item_sao_state_name = $item["sao_state_name"];
							$item_sao_state = $item["sao_state"];
							$item_sao_state_color_background = $item["sao_state_color_background"];
							$item_sao_state_color_text = $item["sao_state_color_text"];
							$item_sao_actions = $item["sao_actions"];
							$item_sao_message = $item["sao_message"];
							
							
							//Чеки
							$item_checks_count = $item["checks_count"];
							if( $item_checks_count == 0 )
							{
								$item_checks_count = translate_str_by_id(36);
							}
							else
							{
								$item_checks_count = "<span onclick=\"show_order_item_checks(".$item_id.");\">".$item_checks_count." <i class=\"fas fa-search\"></i></span>";
							}
							?>
							

							<tr class="<?php echo $viewed_class; ?>" id="order_item_record_<?php echo $item_id; ?>" style="background-color:<?php echo $orders_items_statuses[$item_status]["color"]; ?>">
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $item_id; ?>');" id="checked_<?php echo $item_id; ?>" name="checked_<?php echo $item_id; ?>" /></td>
								<td></td>
								<td><?php echo $item_id; ?></td>
								<td><?php echo $item_manufacturer; ?></td>
								<td><?php echo $item_article; ?></td>
								<td><?php echo $item_product_name; ?></td>
								<td><?php echo $item_checks_count; ?></td>
								<td><?php echo number_format($item_price, 2, '.', ''); ?></td>
								<td><?php echo $item_count_need; ?></td>
								<td><?php echo number_format($item_price_sum, 2, '.', ''); ?></td>
								<td><?php echo number_format($item_price_purchase_sum, 2, '.', ''); ?></td>
								<td><?php echo number_format($item_profit, 2, '.', ''); ?></td>
								<td id="order_item_status_name_td_<?php echo $item_id; ?>"><?php echo translate_str_by_id($orders_items_statuses[$item_status]["name"]); ?></td>
								<td><?php echo date("d.m.Y", $item_time)." ".date("G:i", $item_time); ?></td>
								<td><?php include $_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/users/statistics/modal.php';//Статистика?><?php echo $item_customer; ?></td>
								<td>
									<a href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/orders?order_id=<?php echo $item_order_id; ?>" title="Open in one-page OMS">
										<?php echo translate_str_by_id(1082); ?> <?php echo $item_order_id; ?><br>
										<font style="font-size:0.8em;">
										<?php
										if( $item["paid"] == 0 )
										{
											echo translate_str_by_id(3513);
										}
										else if( $item["paid"] == 1 )
										{
											echo translate_str_by_id(3514);
										}
										else
										{
											echo translate_str_by_id(3515);
										}
										?>
										</font>
									</a>
								</td>
								<td><?php echo translate_str_by_id($offices_list[$item_office_id]); ?></td>
								<td><?php echo $item_t2_time_to_exe; ?></td>
								<td>
									<?php echo translate_str_by_id($item_how_get_caption); ?>
								</td>
								<td>
									<a class="btn btn-success " href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/orders?order_id=<?php echo $item_order_id; ?>"><i class="fa fa-search"></i> <span class="bold"><?php echo translate_str_by_id(3609); ?></span></a>
								</td>
								<td>
									<div class="row">
										<div class="col-lg-12">
										
											
											
											<?php
											if($item["paid"] == 0)
											{
											?>
												<div style="position:relative; left:-70px; top:60px;">
													<a href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/items/edit?id=<?=$item_id;?>" title="<?php echo translate_str_by_id(3552); ?>"> <i style="font-size: 4em;" class="far fa-edit"></i></a>
												</div>
											<?php
											}
											?>
											
										
										
										
											<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped table-bordered">
												<thead>
													<tr>
														<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(233); ?></th>
														<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(3501); ?></th>
														<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(3425); ?></th>
														<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(2752); ?></th>
														<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(3502); ?></th>
														<th colspan="3" style="text-align:center;">
															SAO
														</th>
													</tr>
													<tr>
														<th style="text-align:center;"><?php echo translate_str_by_id(3512); ?></th>
														<th style="text-align:center;"><?php echo translate_str_by_id(4325); ?></th>
														<th style="text-align:center;"><?php echo translate_str_by_id(2113); ?></th>
													</tr>
												</thead>
												<tbody>
												<?php
												//Выводим данные по поставкам. Логика зависит от типа продукта
												if($item_product_type == 1)
												{
													$details_query = $db_link->prepare("SELECT *, (`count_reserved`+`count_issued`+`count_canceled`)*`price_purchase` AS `price_purchase_sum`, `count_reserved`+`count_issued`+`count_canceled` AS `count_reserved_issued` FROM `shop_orders_items_details` WHERE `order_item_id` = ?;");
													$details_query->execute( array($item_id) );
													while( $detail = $details_query->fetch() )
													{
														?>
														<tr>
															<td><?php echo $storages_list[$detail["storage_id"]]; ?> (ID <?php echo $detail["storage_id"]; ?>)</td>
															<td><?php echo $detail["storage_record_id"]; ?></td>
															<td><?php echo number_format($detail["price_purchase"], 2, '.', ''); ?></td>
															<td><?php echo $detail["count_reserved_issued"]; ?></td>
															<td><?php echo number_format($detail["price_purchase_sum"], 2, '.', ''); ?></td>
															<td colspan="3"><?php echo translate_str_by_id(3610); ?></td>
														</tr>
														<?php
													}
												}
												else if($item_product_type == 2)
												{
													?>
													<tr>
														<td><?php echo $storages_list[$item["t2_storage_id"]]; ?> (ID <?php echo $item["t2_storage_id"]; ?>)</td>
														<td><?php echo "-"; ?></td>
														<td><?php echo number_format($item["t2_price_purchase"], 2, '.', ''); ?></td>
														<td><?php echo $item["count_need"]; ?></td>
														<td><?php echo number_format($item["t2_price_purchase"]*$item["count_need"], 2, '.', ''); ?></td>
														<?php
														if( $item_sao_state > 0 )
														{
															?>
															<td id="order_item_sao_state_td_<?php echo $item_id; ?>" style="background-color:<?php echo $item_sao_state_color_background; ?>; color:<?php echo $item_sao_state_color_text; ?>;vertical-align:middle;">
																<?php echo $item_sao_state_name; ?>
															</td>
															<td id="order_item_sao_info_td_<?php echo $item_id; ?>">
																<?php
																if($item_sao_message != "")
																{
																	echo $item_sao_message;
																}
																else
																{
																	echo "-";
																}
																?>
															</td>
															<td id="order_item_sao_actions_td_<?php echo $item_id; ?>">
																<?php
																if($item_sao_actions != "")
																{
																	$item_sao_actions = explode(",", $item_sao_actions);
																	for($ac=0; $ac < count($item_sao_actions); $ac++)
																	{
																		?>
																		<button onclick="exec_action(<?php echo $item_id; ?>, <?php echo $item_sao_actions[$ac]; ?>);" class="btn <?php echo $sao_actions[$item_sao_actions[$ac]]["btn_class"]; ?> " type="button"><i class="fa <?php echo $sao_actions[$item_sao_actions[$ac]]["fontawesome"]; ?>"></i> <span class="bold"><?php echo $sao_actions[$item_sao_actions[$ac]]["name"]; ?></span></button>
																		<?php
																	}
																}
																else
																{
																	?>
																	<?php echo translate_str_by_id(3554); ?>
																	<?php
																}
																?>
															</td>
															<?php
														}
														else
														{
															?>
															<td colspan="3"><?php echo translate_str_by_id(3555); ?></td>
															<?php
														}
														?>
													</tr>
													<?php
												}
												?>
													<tr>
														<td colspan="2"></td>
														<td><strong><?php echo translate_str_by_id(3503); ?></strong></td>
														<td><strong><?php echo $item_count_need; ?></strong></td>
														<td><strong><?php echo number_format($item_price_purchase_sum, 2, '.', ''); ?></strong></td>
														<td colspan="3"></td>
													</tr>
												</tbody>
											</table>
										</div>
									</div>
								</td>
							</tr>
							<?php
							$items_counter++;
						}//while() - по позициям
						?>
						</tbody>
						<tfoot style="display:none;"><tr><td><ul class="pagination"></ul></td></tr></tfoot>
					</table>
					<div id="epc-oi-boot" style="display:none"><?php echo htmlspecialchars(json_encode($oi_boot, JSON_UNESCAPED_UNICODE), ENT_QUOTES, 'UTF-8'); ?></div>
				</div>
				
				
				<?php
				//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				if( $count_pages > 1 )
				{
					// формируем пагинацию
					$pagination = pagination($elements_count_rows, $p, 3, $s_page, 'paginate_button active', '');
					if($pagination != '<a class="paginate_button active">1</a>'){
						$pagination = '<div class="pagination">'.$pagination.'</div>';
					}else{
						$pagination = '';
					}
					?>
					<div class="row">
						<div class="col-lg-12 text-center">
							<div class="dataTables_paginate paging_simple_numbers">
								<?php echo $pagination; ?>	
							
							</div>
						</div>
					</div>
				<?php
				}
				//END ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				?>
					
				
			</div>
			<div class="panel-footer">
				<div class="row">
					<!--<div class="col-lg-2">
						<button class="btn btn-info " type="button" onclick="create_check_for_orders_items();"><i class="fas fa-receipt"></i> <?php echo translate_str_by_id(3556); ?></button>
					</div>-->
				
					<div class="col-lg-6">	
						<div class="input-group">
							<select id="setOrderItemsStatusSelect" class="form-control">
								<?php
								foreach($orders_items_statuses as $status_id=>$status_data)
								{
									?>
									<option value="<?php echo $status_id; ?>"><?php echo translate_str_by_id($status_data["name"]); ?></option>
									<?php
								}
								?>
							</select>
							<span class="input-group-btn">
								<button onclick="setOrderItemsStatus();" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(3558); ?></span></button>
							</span>
						</div>
					</div>
					
					
					
					<div class="col-lg-6">	
						<div class="input-group">
							<select id="setOrderViewed" class="form-control">
								<option value="1"><?php echo translate_str_by_id(3611); ?></option>
								<option value="0"><?php echo translate_str_by_id(3612); ?></option>
							</select>
							<span class="input-group-btn">
								<button onclick="setOrderViewed();" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold">Ok</span></button>
							</span>
						</div>
					</div>
					
					
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3613); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3614); ?>');"><i class="fa fa-info"></i></button>
			</div>
			<div class="panel-body">
				<div class="row">
					<div class="col-lg-12 text-center">
						<div class="table-responsive">
							<table cellpadding="1" cellspacing="1" class="table">
								<thead>
									<tr>
										<th style="text-align:center;"><?php echo translate_str_by_id(3615); ?></th>
										<th style="text-align:center;"><?php echo translate_str_by_id(3593); ?></th>
										<th style="text-align:center;"><?php echo translate_str_by_id(3616); ?></th>
										<th style="text-align:center;"><?php echo translate_str_by_id(3617); ?></th>
										<th style="text-align:center;"><?php echo translate_str_by_id(3818); ?></th>
									</tr>
								</thead>
								<tbody>
									<tr>
										<td style="text-align:center;"><?php echo $total_indicators["positions_count"]; ?></td>
										<td style="text-align:center;"><?php echo $total_indicators["orders_count"]; ?></td>
										<td style="text-align:center;"><?php echo $total_indicators["price_sum_total"]; ?></td>
										<td style="text-align:center;"><?php echo $total_indicators["profit_sum_total"]; ?></td>
										<td style="text-align:center;"><?php echo $total_indicators["price_purchase_sum_total"]; ?></td>
									</tr>
								</tbody>
							</table>
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	

	
	
    <?php
}
?>