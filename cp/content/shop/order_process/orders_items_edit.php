<?php
/**
 * Страница для редактирования позиции заказа
*/
defined('_ASTEXE_') or die('No access');
ini_set("display_errors",0);

//Получаем список статусов позиций с флагом "for_inverse" - заказ отменен
$orders_items_statuses_for_inverse = array();
$query = $db_link->prepare("SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0 ORDER BY `order` ASC;");
$query->execute();
while($status_row = $query->fetch() )
{
	$orders_items_statuses_for_inverse[] = $status_row["id"];
}

//Получаем список статусов позиций с флагом "for_finish" - позиция выдана
$orders_items_statuses_for_finish = array();
$query = $db_link->prepare("SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC;");
$query->execute();
while($status_row = $query->fetch() )
{
	$orders_items_statuses_for_finish[] = $status_row["id"];
}

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/order_process/orders_background.php");

$AdminId = (int) DP_User::getAdminId();

if(!empty($_POST["save_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	if($_POST["save_action"] != 'update')
	{
		$error_message = translate_str_by_id(2304);
	}
	else
	{
		$item_id = (int)$_POST['item_id'];
		$order_id = (int)$_POST['order_id'];
		$item_product_type = (int)$_POST['item_product_type'];
		$article = htmlentities($_POST['art']);
		$manufacturer = htmlentities($_POST['man']);
		$name = htmlentities($_POST['name']);
		
		$manufacturer = mb_strtoupper(trim($manufacturer), "UTF-8");
		$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article), "UTF-8");
        $name = str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), "", $name);
		
		//$t2_markup = (int)$_POST['t2_markup'];
		//$_POST['markup'] = $t2_markup;
		$storage_id = (int)$_POST['storage_id'];
		$user_id = (int)$_POST['user_id'];
		//$t2_price_purchase = (float)$_POST['t2_price_purchase'];
		$count_need = (int)$_POST['count_need'];
		$price = (float)$_POST['price'];//$t2_price_purchase + (($t2_price_purchase / 100) * ((float)$_POST['markup'])); 
		$price_zakup = (float)$_POST['price_zakup'];
		
		$time_to_exe = (int)trim($_POST['t2_time_to_exe']);
		$time_to_exe_guaranteed = (int)trim($_POST['t2_time_to_exe_guaranteed']);
		if($time_to_exe > $time_to_exe_guaranteed){
			$time_to_exe_guaranteed = $time_to_exe;
		}
		
		$error_message = "";
		$success_message = "";
		
		//Позиция не должна быть в отмененном или выданном статусе
		$check_query = $db_link->prepare('SELECT `status` FROM `shop_orders_items` WHERE `id` = ?;');
		$check_query->execute( array($item_id) );
		$check = $check_query->fetch();
		if( !$check || in_array((int)$check['status'], $orders_items_statuses_for_inverse, true) || in_array((int)$check['status'], $orders_items_statuses_for_finish, true) )
		{
			$location_url = '/'.$DP_Config->backend_dir.'/shop/orders/items/edit?id='.$item_id;
			?>
			<script>
				location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5316)); ?>";
			</script>
			<?php
			exit;
		}
		
		//Первым делом проверяем состояние оплаты. Нельзя редактировать позиции заказов, которые Оплачены или частично оплачены.
		$check_paid_query = $db_link->prepare('SELECT `paid` FROM `shop_orders` WHERE `id` = (SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ?);');
		$check_paid_query->execute( array($item_id) );
		$check_paid = $check_paid_query->fetch();
		if( !$check_paid || (int)$check_paid['paid'] !== 0 )
		{
			$location_url = '/'.$DP_Config->backend_dir.'/shop/orders/items/edit?id='.$item_id;
			?>
			<script>
				location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5317)); ?>";
			</script>
			<?php
			exit;
		}
		
		
		
		if( empty($item_id) || empty($count_need) || empty($price) )
		{
			$error_message = translate_str_by_id(3628);
		}
		else
		{
			if($item_product_type === 2)
			{
				// Обновляем данные
				if( $db_link->prepare("UPDATE `shop_orders_items` SET `price` = ?, `t2_price_purchase` = ?, `t2_name` = ?, `t2_time_to_exe` = ?, `t2_time_to_exe_guaranteed` = ?, `count_need` = ?, `t2_storage_id` = ?, `t2_article` = ?, `t2_manufacturer` = ? WHERE `id` = ?;")->execute( array($price, $price_zakup, $name, $time_to_exe, $time_to_exe_guaranteed, $count_need, $storage_id, $article, $manufacturer, $item_id) ) != true)
				{
					$error_message = translate_str_by_id(3629);
				}
				else
				{
					$success_message = translate_str_by_id(3630);
				}
			}
			else
			{
				//Сначала проверяем был ли изменен склад и если склад изменен, то проверяем статус позиции
				$shop_orders_items_status_query = $db_link->prepare("SELECT `status` FROM `shop_orders_items` WHERE `id` = ?");
				$shop_orders_items_status_query->execute(array($item_id));
				$shop_orders_items_status = $shop_orders_items_status_query->fetchColumn();

				//Если статус выдана
				if($shop_orders_items_status == 5) {

					$error_message = translate_str_by_id(3631);

				} else {

					//Смотрим старую складскую запись и какой был склад
					$storage_data_id_query = $db_link->prepare("SELECT `storage_id` AS `prev_storage_id`, `product_id` FROM `shop_storages_data` WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `order_item_id` = ?)");
					$storage_data_id_query->execute(array($item_id));
					$storage_data_id = $storage_data_id_query->fetch();
			
					//Делаем перемещение товаров от одного склада к другому, если был выбран другой склад
					if(1) {
						
						//Сначала нужно проверить, есть ли этот товар на выбранном складе
						$storage_data_product_query = $db_link->prepare("SELECT COUNT(*) AS `storage_data_product_count`, `id`, `exist` FROM `shop_storages_data` WHERE `product_id` = ? AND `storage_id` = ?");
						$storage_data_product_query->execute(array($storage_data_id['product_id'], $storage_id));
						$storage_data_product = $storage_data_product_query->fetch();

						//Получаем количество товара который будем перемещать из детальных записей
						$storage_detail_items_reserved_query = $db_link->prepare("SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `order_item_id`=?");
						$storage_detail_items_reserved_query->execute(array($item_id));
						$storage_detail_items_reserved = $storage_detail_items_reserved_query->fetchColumn();

						//Переменная нужна для проверки - выполнен ли трансфер товара из одного склада на другой
						//От этого зависит нужно ли обновлять в таблице shop_orders_items_details id складской записи или нет
						$check_order_item_transfer = false;
						$storage_data_product_record_id = $storage_data_product['id'];

						if($storage_data_product['storage_data_product_count'] > 0 && $storage_data_product['exist'] >= $count_need) {

							//Такой товар есть на складе, начинаем его перемещать
							//Сначала восстанавливаем количество на изначальном складе
							$storage_data_update_query_1 = $db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist` + (SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `order_item_id`=?), `reserved` = `reserved` - (SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `order_item_id`=?) WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `order_item_id`=?)');
							if($storage_data_update_query_1->execute( array($item_id, $item_id, $item_id)) != true) {

								$error_message .= translate_str_by_id(3632).". ";

							} else {

								//Теперь создаем резерв и списываем количество на выбранном складе
								$storage_data_update_query_2 = $db_link->prepare('UPDATE `shop_storages_data` SET `exist` = `exist` - ?, `reserved` = `reserved` + ? WHERE `id` = ?');
								if($storage_data_update_query_2->execute( array($count_need, $count_need, $storage_data_product_record_id)) != true) {

									$error_message .= translate_str_by_id(3633).". <br/> ".translate_str_by_id(3634).".  <br/> ".translate_str_by_id(3635).". ";

								} else {

									$check_order_item_transfer = true;
									$success_message .= translate_str_by_id(5318);

								}
							}

						}
						else
						{
							$error_message .= translate_str_by_id(3637).". ";
						}



						// Обновляем данные 1 тип продукта, после перемещения товара на другой склад
						if($check_order_item_transfer) {

							$SQL_order_items_detail = "UPDATE `shop_orders_items_details` SET `price_purchase` = ?, `count_reserved` = ?, `count_issued` = 0, `count_canceled` = 0, `storage_id` = $storage_id, `storage_record_id` = $storage_data_product_record_id WHERE `order_item_id` = ?;";
							
							if( $db_link->prepare("UPDATE `shop_orders_items` SET `price` = ?, `count_need` = ?, `t2_name` = ?, `t2_article` = ?, `t2_manufacturer` = ?, `t2_time_to_exe` = ?, `t2_time_to_exe_guaranteed` = ? WHERE `id` = ?;")->execute( array($price, $count_need, $name, $article, $manufacturer, $time_to_exe, $time_to_exe_guaranteed, $item_id) ) != true)
							{
								$error_message .= translate_str_by_id(3629);
							}
							else
							{
								if( $db_link->prepare($SQL_order_items_detail)->execute( array($price_zakup, $count_need, $item_id) ) != true)
								{
									$error_message .= translate_str_by_id(3629);
								}
								else
								{  
									$success_message .= translate_str_by_id(3639);
								}
							}
						} 
						
						
						

					}

				}

			}
			
		}
	}
	
	//Пишем лог заказа
	$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order_id, time(), $AdminId, 1, translate_str_by_id(5319).' <b>id '.$item_id.'</b>', 0) );
	
	$location_url = '/'.$DP_Config->backend_dir.'/shop/orders/items/edit?id='.$item_id;
	?>
	<script>
		location="<?=$location_url?>&error_message=<?=$error_message;?>&success_message=<?=$success_message;?>";
	</script>
	<?php
	exit;
}
else
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	require_once($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/order_process/orders_background.php');
	
	$item_id = (int)$_GET['id'];
	
	//Первым делом проверяем состояние оплаты. Нельзя редактировать позиции заказов, которые Оплачены или частично оплачены.
	$check_paid_query = $db_link->prepare('SELECT `paid` FROM `shop_orders` WHERE `id` = (SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ?);');
	$check_paid_query->execute( array($item_id) );
	$check_paid = $check_paid_query->fetch();
	if( !$check_paid || (int)$check_paid['paid'] !== 0 )
	{
		$_GET["warning_message"] = translate_str_by_id(3640);
	}
	
	$available_storages_list = array();
	$query = $db_link->prepare('SELECT * FROM `shop_storages`;');
	$query->execute();
	while($row = $query->fetch()){
		$available_storages_list[$row['id']] = $row['name'].' ('.$row['id'].')';
	}
	
	$old_price = '';
	$old_count_need = '';
	$style = '';
	
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/actions_alert.php");//Вывод сообщений о результатах действий
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/items">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/orders_items.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3622); ?></div>
				</a>
				<a id="order_id_a" class="panel_a" href="">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/store.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3623); ?></div>
				</a>
			</div>
		</div>
	</div>
	<div class="col-lg-12">
	<div class="table-responsive">
	<div class="hpanel">
	<div class="panel-heading hbuilt">
		<?php echo translate_str_by_id(5320); ?>
	</div>
	<div class="panel-body">
	<?php
	//Формируем сложный SQL-запрос для получения всей информации по позиции
	//Запрос суммы позиции
	$SELECT_price_sum = "CAST(`price`*`count_need` AS DECIMAL(8,2))";
	//Запрос офисов обслуживания
	$SELECT_offices = "(SELECT `office_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
	//Запрос клиента
	$SELECT_clients = "(SELECT `user_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
	//Запрос закупа
	$SELECT_price_purchase_sum = "IFNULL((SELECT SUM(`price_purchase`*(`count_reserved`+`count_issued`+`count_canceled`)) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`), CAST(`t2_price_purchase`*`count_need` AS DECIMAL(8,2)))";
	$SELECT_price_purchase = "IFNULL((SELECT `price_purchase` FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`), CAST(`t2_price_purchase` AS DECIMAL(8,2)))";
	//Запрос маржы
	$SELECT_profit = "CAST(($SELECT_price_sum - $SELECT_price_purchase_sum) AS DECIMAL(8,2))";
	//Запрос статуса заказа
	$SELECT_order_status = "(SELECT `status` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
	//Запрос флаг "Заказ оплачен"
	$SELECT_paid = "(SELECT `paid` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";
	//Запрос складов
	$SELECT_storages = "IFNULL((SELECT `storage_id` FROM `shop_orders_items_details` WHERE `order_id` = `shop_orders_items`.`order_id` AND `order_item_id` = `shop_orders_items`.`id`), `t2_storage_id`)";
	//Запрос времени создания заказа
	$SELECT_time = "(SELECT `time` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)";



	//ЗАПРОС
	$SQL_SELECT_ITEMS = "SELECT SQL_CALC_FOUND_ROWS *, 
		$SELECT_price_sum AS `price_sum`, 
		$SELECT_offices AS `office_id`, 
		$SELECT_clients AS `customer_id`, 
		$SELECT_price_purchase_sum AS `price_purchase_sum`,
		$SELECT_price_purchase AS `price_purchase`,
		$SELECT_profit AS `profit`,
		$SELECT_order_status AS `order_status`,
		$SELECT_paid AS `paid`,
		$SELECT_storages AS `storages`,
		$SELECT_time AS `time` 
		FROM `shop_orders_items` WHERE `id` = ?;";


	$elements_query = $db_link->prepare($SQL_SELECT_ITEMS);
	$elements_query->execute( array($item_id) );
	//var_dump($elements_query->fetch());
	//var_dump($SQL_SELECT_ITEMS);
	//var_dump($SQL_SELECT_ITEMS);
	
	$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
	$elements_count_rows_query->execute();
	$elements_count_rows = (int)$elements_count_rows_query->fetchColumn();
	
	if($elements_count_rows <= 0)
	{
		?>
		<div class="alert alert-warning"><?php echo translate_str_by_id(3628); ?> (ID <?php echo (int)$item_id; ?>)</div>
		<?php
	}
	else
	{
	
	?>
	<table style="font-size:11px;" id="orders_items_table" class="footable table table-hover toggle-arrow " data-sort="false" data-page-size="<?php echo $elements_count_rows; ?>">
		<thead>
			<th><a href="javascript:void(0);" onclick="sortOrdersItems('id');" id="id_sorter">ID</a></th>
			<th><a href="javascript:void(0);" onclick="sortOrdersItems('t2_manufacturer');" id="t2_manufacturer_sorter"><?php echo translate_str_by_id(2070); ?></a></th>
			<th><a href="javascript:void(0);" onclick="sortOrdersItems('t2_article');" id="t2_article_sorter"><?php echo translate_str_by_id(2071); ?></a></th>
			<th><a href="javascript:void(0);" onclick="sortOrdersItems('t2_name');" id="t2_name_sorter"><?php echo translate_str_by_id(2102); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('price');" id="price_sorter"><?php echo translate_str_by_id(2751); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('count_need');" id="count_need_sorter"><?php echo translate_str_by_id(4526); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('profit');" id="profit_sorter"><?php echo translate_str_by_id(3499); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('price_sum');" id="price_sum_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
			<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('price_purchase_sum');" id="price_purchase_sum_sorter"><?php echo translate_str_by_id(5306); ?></a></th>
			<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('profit2');" id="profit_sorter2"><?php echo translate_str_by_id(3499); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('status');" id="status_sorter"><?php echo translate_str_by_id(2081); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('order_id');" id="order_id_sorter"><?php echo translate_str_by_id(3243); ?></a></th>
			<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrdersItems('storages');" id="storages_sorter"><?php echo translate_str_by_id(233); ?></a></th>
			<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('office_id');" id="office_id_sorter"><?php echo translate_str_by_id(3506); ?></a></th>
			<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="sortOrdersItems('t2_time_to_exe');" id="t2_time_to_exe_sorter"><?php echo translate_str_by_id(3550); ?></a></th>
			<th><a href="javascript:void(0);" onclick="sortOrdersItems('customer_id');" id="customer_id_sorter"><?php echo translate_str_by_id(3245); ?></a></th>
								
		</thead>
		<tbody>
		<?php
		$item_count_sum = 0;// количество
		$item_profit_sum = 0;// Маржа сумма
		$item_sum = 0;// сумма
		while( $item = $elements_query->fetch() )
		{
			//var_dump($item);
			
			$elements_counter++;
			
			$item_id = $item["id"];
			$order_id = $item["order_id"];
			$item_product_type = (int)$item["product_type"];
			$item_status = $item["status"];
			$item_order_id = $item["order_id"];
			$item_product_name = $item["t2_name"];
			$item_article = $item["t2_article"];
			$item_manufacturer = $item["t2_manufacturer"];
			$item_price = $item["price"];
			$item_count_need = $item["count_need"];
			$item_price_sum = $item["price_sum"];
			$item_office_id = $item["office_id"];
			$item_customer_id = $item["customer_id"];
			$item_price_purchase_sum = $item["price_purchase_sum"];
			$item_profit = $item["profit"];
			$item_time = $item["time"];
			$item_storages = $item["storages"];
			
			$item_t2_time_to_exe = $item["t2_time_to_exe"];
			$item_t2_time_to_exe_guaranteed = $item["t2_time_to_exe_guaranteed"];
			$item_t2_name = $item["t2_name"];
			$item_t2_article = $item["t2_article"];
			$item_t2_manufacturer = $item["t2_manufacturer"];
			$item_t2_markup = $item["t2_markup"];
			
			// Переменные итого:
			$item_count_sum += $item_count_need;// количество
			$item_profit_sum += $item_profit;// Маржа
			$item_sum += $item_price_sum;// сумма
			
			//Теперь получаем ФИО
			$profile = DP_User::getUserProfileById($item_customer_id);
			$customer_name = $profile["surname"]." ".$profile["name"]."(".$item_customer_id.")";
			
			//Срок доставки для продуктов типа 2
			if($item_t2_time_to_exe < $item_t2_time_to_exe_guaranteed)
			{
				$item_t2_time_to_exe = $item_t2_time_to_exe." - ".$item_t2_time_to_exe_guaranteed;
			}
			$item_t2_time_to_exe = $item_t2_time_to_exe." ".translate_str_by_id(5315);
			
			
			/// Получаем комментарии к позиции заказа
			$item_comment = '';
			try {
				$SELECT_POS_COMMENT = "SELECT `comment` FROM `shop_orders_pos_comment` WHERE `pos_id` = ?;";
				$item_query = $db_link->prepare($SELECT_POS_COMMENT);
				$item_query->execute( array($item_id) );
				$item_row = $item_query->fetch();
				$item_comment = ($item_row && isset($item_row['comment'])) ? $item_row['comment'] : '';
			} catch (Throwable $e) {
				$item_comment = '';
			}
			

			?>
			<tr id="order_item_record_<?php echo $item_id; ?>" style="background-color:<?php echo $orders_items_statuses[$item_status]["color"]; ?>">


				<td><?php echo $item_id; ?></td>
				<td><?php echo $item_t2_manufacturer; ?></td>
				<td><?php echo $item_t2_article; ?></td>
				<td><?php echo $item_t2_name; ?></td>
				<td><?php echo number_format($item_price, 2, '.', ''). $old_price; ?></td>
				<td><?php echo $item_count_need . $old_count_need; ?></td>
				<td><?php echo $item_profit; ?></td>
				<td><?php echo number_format($item_price_sum, 2, '.', ''); ?></td>
				<td><?php echo number_format($item_price_purchase_sum, 2, '.', ''); ?></td>
				<td><?php echo number_format($item_profit, 2, '.', ''); ?><input type="hidden" id="inp_markup" value="<?=$item_profit;?>"/></td>
				<td><?php echo $orders_items_statuses[$item_status]["name"]; ?></td>
				<td><?php echo date("d.m.Y", $item_time)." ".date("G:i", $item_time); ?></td>
				<td><a href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $item_order_id; ?>"><?php echo translate_str_by_id(3243); ?> <?php echo $item_order_id; ?></a><input type="hidden" id="inp_order_id" value="<?=$item_order_id;?>"/></td>
				
				<?php
				// Склад
				if($item_product_type == 1){
				?>
					<td><?php echo $available_storages_list[$item["storages"]]; ?></td>
				<?php
				}else{
				?>
					<td><?php echo $available_storages_list[$item["t2_storage_id"]]; ?></td>
				<?php
				}
				?>
				
				<td><?php echo $offices_list[$item_office_id]; ?></td>
				<td><?php echo $item_t2_time_to_exe; ?></td>
				<td><?php echo $customer_name; ?></td>
				
			</tr>
			

		</tbody>
		<tfoot style="display:none;"><tr><td><ul class="pagination"></ul></td></tr></tfoot>
	</table>


	<?php
	// Склад
	if($item_product_type == 1){
		//$style = " display:none;";
	}
	?>
	<div style="padding:20px 0px;<?=$style;?>">
		<label><?php echo translate_str_by_id(2071); ?>:</label><br/>
		<input class="form-control" style="width:500px;" id="art_item_inp" type="text" value="<?=$item_t2_article;?>" />
		<br/><label><?php echo translate_str_by_id(2070); ?>:</label><br/>
		<input class="form-control" style="width:500px;" id="man_item_inp" type="text" value="<?=$item_t2_manufacturer;?>"/><br/><label><?php echo translate_str_by_id(2102); ?>:</label><br/>
		<input class="form-control" style="width:500px;" id="name_item_inp" type="text" value="<?=$item_t2_name;?>"/>
		<br/><label><?php echo translate_str_by_id(4317); ?>:</label><br/>
		<input class="form-control" style="width:200px;" id="t2_time_to_exe_item_inp" type="text" value="<?=$item["t2_time_to_exe"];?>"/>
		<br/><label><?php echo translate_str_by_id(4318); ?>:</label><br/>
		<input class="form-control" style="width:200px;" id="t2_time_to_exe_guaranteed_item_inp" type="text" value="<?=$item["t2_time_to_exe_guaranteed"];?>"/>
	</div>




	<table>
		<tr>
			<td>
			<div class="row">
				<div class="col-lg-12">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped table-bordered">
						<thead>
							<th><?php echo translate_str_by_id(2750); ?></th>
							<th><?php echo translate_str_by_id(2751); ?></th>
							<th><?php echo translate_str_by_id(3624); ?></th>
							<th><?php echo translate_str_by_id(2752); ?></th>
							<th><?php echo translate_str_by_id(3251); ?></th>
							<th><?php echo translate_str_by_id(3502); ?></th>
						</thead>
						<tbody>
						<?php
						//Выводим данные по поставкам. Логика зависит от типа продукта
						if($item_product_type == 1)
						{
							$details_query = $db_link->prepare("SELECT *, (`count_reserved`+`count_issued`+`count_canceled`)*`price_purchase` AS `price_purchase_sum` FROM `shop_orders_items_details` WHERE `order_item_id` = ?;");
							$details_query->execute( array($item_id) );

							
							
							while( $detail = $details_query->fetch() )
							{
								?>
								<tr>
									<td>
										<select class="form-control" id="inp_storage_id">
											
									<?php 
										if($item_product_type !== 0)
										{
											$query = $db_link->prepare("SELECT * FROM `shop_storages` WHERE `interface_type` = 1 AND `users` LIKE ?;");
											$query->execute( array('%"'.$AdminId.'"%') );
											while( $row = $query->fetch() )
											{
												$k = $row['id'];
												$v = $row['name'];
												if($detail["storage_id"] == $k){
													echo '<option selected value="'.$k.'">'.$v.' ('.$k.')</option>';
												}else{
													echo '<option value="'.$k.'">'.$v.' ('.$k.')</option>';
												}
												 
											}
										}
									?>
										</select>
									</td>
									<td><input class="form-control" type="text" id="inp_price" value="<?php echo number_format($item["price"], 2, '.', ''); ?>" /> </td>
									<td><input class="form-control" type="text" id="inp_price_zakup" value="<?php echo number_format($detail["price_purchase"], 2, '.', ''); ?>" /> </td>
									<td><input class="form-control" type="text" id="inp_count_need" value="<?php echo $detail["count_reserved"]+$detail["count_issued"]+$detail["count_canceled"]; ?>" /><?=$old_count_need;?></td>
									<td><?php echo number_format($item["price"]*$item["count_need"], 2, '.', ' '); ?></td>
									<td><?php echo number_format($detail["price_purchase_sum"], 2, '.', ''); ?></td>
								</tr>
								<?php
							}
						}
						else if($item_product_type == 2)
						{
							?>
							<tr>
								<td>
									<select id="inp_storage_id" class="form-control">
								<?php 
									foreach($storages_list as $k => $v){
										if($item["t2_storage_id"] == $k){
											echo '<option selected value="'.$k.'">'.$v.' ('.$k.')</option>';
										}else{
											echo '<option value="'.$k.'">'.$v.' ('.$k.')</option>';
										}
										 
									}
								?>
									</select>
								<td><input class="form-control" type="text" id="inp_price" value="<?php echo number_format($item["price"], 2, '.', ''); ?>" /> </td>
								<td><input class="form-control" type="text" id="inp_price_zakup" value="<?php echo number_format($item["price_purchase"], 2, '.', ''); ?>" /> </td>
								<td><input class="form-control" type="text" id="inp_count_need" value="<?php echo $item["count_need"]; ?>" /><?=$old_count_need;?></td>
								<td><?php echo number_format($item["price"]*$item["count_need"], 2, '.', ' '); ?></td>
								<td><?php echo number_format($item["price_purchase"]*$item["count_need"], 2, '.', ' '); ?></td>
							</tr>
							<?php
						}
						?>
							<tr>
								<td colspan="2"></td>
								<td><strong><?php echo translate_str_by_id(3503); ?></strong></td>
								<td><strong><?php echo $item_count_need; ?></strong></td>
								<td><strong><?php echo number_format($item_price_sum, 2, '.', ' '); ?></strong></td>
								<td><strong><?php echo number_format($item_price_purchase_sum, 2, '.', ' '); ?></strong></td>
							</tr>
						</tbody>
					</table>
				</div>
			</div>
			</td>
		</tr>
	</table>
						
	<?php
		$items_counter++;
	}//while() - по позициям
	?>
	<form id="save_form" name="save_form" method="POST" style="display:none;">
		<input type="hidden" name="save_action" id="save_action" value="update" />
		<input type="hidden" name="item_id" value="<?=$item_id;?>" />
		<input type="hidden" id="price" name="price" value="" />
		<input type="hidden" id="price_zakup" name="price_zakup" value="" />
		<input type="hidden" id="count_need" name="count_need" value="" />
		<input type="hidden" id="order_id" name="order_id" value="" />
		<input type="hidden" id="user_id" name="user_id" value="<?=$item_customer_id?>" />
		<input type="hidden" id="storage_id" name="storage_id" value="" />
		<input type="hidden" id="art" name="art" value="" />
		<input type="hidden" id="man" name="man" value="" />
		<input type="hidden" id="name" name="name" value="" />
		<input type="hidden" id="t2_time_to_exe" name="t2_time_to_exe" value="" />
		<input type="hidden" id="t2_time_to_exe_guaranteed" name="t2_time_to_exe_guaranteed" value="" />
		<input type="hidden" id="item_product_type" name="item_product_type" value="<?=$item_product_type;?>" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	</div>
	</div>
	</div>
	</div>
<?php
	}// item found
}//else GET
?>