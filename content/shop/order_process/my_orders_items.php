<?php
/**
 * Страница для вывода всех позиций всех заказов для покупателя
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();


//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");


if($user_id > 0)
{
	$time_from 			= "";//1. Время с
	$time_to 			= "";//2. Время по
	$order_id 			= "";//3. ID заказа
	$order_status 		= 0;//4. Статус заказа
	$paid 				= -1;//5. Флаг - Заказ оплачен
	$order_item_status 	= 0;//6. Статус позиции
	
	//Получаем текущие значения фильтра:
	$my_orders_items_filter = NULL;
	if(isset($_COOKIE["my_orders_items_filter"]))
	{
		$my_orders_items_filter = $_COOKIE["my_orders_items_filter"];
	}
	if($my_orders_items_filter != NULL)
	{
		$my_orders_items_filter = json_decode($my_orders_items_filter, true);
		if( ! empty($my_orders_items_filter) ){
			$time_from 			= $my_orders_items_filter["time_from"];
			$time_to 			= $my_orders_items_filter["time_to"];
			$order_id 			= $my_orders_items_filter["order_id"];
			$order_status 		= (int) $my_orders_items_filter["order_status"];
			$paid 				= (int) $my_orders_items_filter["paid"];
			$order_item_status 	= (int) $my_orders_items_filter["order_item_status"];
		}
	}
	?>

	<div class="row">
		<div class="col-md-2">
			<div>
                <label style="margin-bottom: 0;" for="time_from_show"><?php echo translate_str_by_id(3237); ?></label>
            </div>
			<div style="position: relative; height: 36px;">
				<input style="position:absolute; z-index:2; opacity:0;width:100%;" type="text"  id="time_from" value="<?php echo $time_from; ?>" />
				<input style=" <?=($time_from !== '')?'background:#b9fcab;':'';?> position:absolute; z-index:1;width:100%;" type="text" id="time_from_show" class="form-control" />
				<script>
				//Инициализируем datetimepicker
				jQuery("#time_from").datetimepicker({
					lang:"ru",
					closeOnDateSelect:true,
					closeOnTimeSelect:false,
					dayOfWeekStart:1,
					format:'unixtime',
					onClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
					{
						var time_string = "";
						var date_ob = new Date(current_time);
						time_string += date_ob.getDate()+".";
						time_string += (date_ob.getMonth() + 1)+".";
						time_string += date_ob.getFullYear()+" ";
						time_string += date_ob.getHours()+":"+date_ob.getMinutes();
						document.getElementById("time_from_show").value = time_string;//Показываем время в понятном виде
					}
					<?php
					if($time_from != "")
					{
						?>
						,
						onGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
						{
							var time_string = "";
							var date_ob = new Date(current_time);
							time_string += date_ob.getDate()+".";
							time_string += (date_ob.getMonth() + 1)+".";
							time_string += date_ob.getFullYear()+" ";
							time_string += date_ob.getHours()+":"+date_ob.getMinutes();
							document.getElementById("time_from_show").value = time_string;//Показываем время в понятном виде
						}
						<?php
					}
					?>
				});
				</script>
			</div>
		</div>
		
		
		
		<div class="col-md-2">
			<div>
                <label style="margin-bottom: 0;" for="time_to_show"><?php echo translate_str_by_id(3238); ?></label>
            </div>
			<div style="position: relative; height: 36px;">
				<input style="position:absolute; z-index:2; opacity:0;width:100%;" type="text"  id="time_to" value="<?php echo $time_to; ?>" />
				<input style=" <?=($time_to !== '')?'background:#b9fcab;':'';?> position:absolute; z-index:1;width:100%;" type="text" id="time_to_show" class="form-control" />
				<script>
				//Инициализируем datetimepicker
				jQuery("#time_to").datetimepicker({
					lang:"ru",
					closeOnDateSelect:true,
					closeOnTimeSelect:false,
					dayOfWeekStart:1,
					format:'unixtime',
					onClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
					{
						var time_string = "";
						var date_ob = new Date(current_time);
						time_string += date_ob.getDate()+".";
						time_string += (date_ob.getMonth() + 1)+".";
						time_string += date_ob.getFullYear()+" ";
						time_string += date_ob.getHours()+":"+date_ob.getMinutes();
						document.getElementById("time_to_show").value = time_string;//Показываем время в понятном виде
					}
					<?php
					if($time_to != "")
					{
						?>
						,
						onGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
						{
							var time_string = "";
							var date_ob = new Date(current_time);
							time_string += date_ob.getDate()+".";
							time_string += (date_ob.getMonth() + 1)+".";
							time_string += date_ob.getFullYear()+" ";
							time_string += date_ob.getHours()+":"+date_ob.getMinutes();
							document.getElementById("time_to_show").value = time_string;//Показываем время в понятном виде
						}
						<?php
					}
					?>
				});
				</script>
			</div>
		</div>
		
		
		
		
		<div class="col-md-2">
			<div>
                <label style="margin-bottom: 0;" for="order_id"><?php echo translate_str_by_id(3244); ?></label>
            </div>
			<div>
				<input <?=($order_id !== '')?'style="background:#b9fcab;"':'';?> type="text" id="order_id" value="<?php echo $order_id; ?>" class="form-control" />
			</div>
		</div>
		
		
		
		
		<div class="col-md-2">
			<div>
                <label style="margin-bottom: 0;" for="paid"><?php echo translate_str_by_id(4350); ?></label>
            </div>
			<div>
				<select <?=((int)$paid !== -1)?'style="background:#b9fcab;"':'';?> id="paid" class="form-control">
					<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
					<option value="1"><?php echo translate_str_by_id(3514); ?></option>
					<option value="0"><?php echo translate_str_by_id(3513); ?></option>
					<option value="2"><?php echo translate_str_by_id(3515); ?></option>
				</select>
				<script>
					document.getElementById("paid").value = <?php echo $paid; ?>;
				</script>
			</div>
		</div>
		
		
		
		
		
		<div class="col-md-2">
			<div>
                <label style="margin-bottom: 0;" for="order_status"><?php echo translate_str_by_id(3603); ?></label>
            </div>
			<div>
				<select <?=((int)$order_status !== 0)?'style="background:#b9fcab;"':'';?> id="order_status" class="form-control">
				<option value="0"><?php echo translate_str_by_id(2094); ?></option>
				<?php
				foreach($orders_statuses as $status_id=>$status_data)
				{
					$selected = "";
					if($order_status == $status_id)
					{
						$selected = "selected=\"selected\"";
					}
					?>
					<option value="<?php echo $status_id; ?>" <?php echo $selected; ?>><?php echo translate_str_by_id($status_data["name"]); ?></option>
					<?php
				}
				?>
				</select>
			</div>
		</div>
		
		<div class="col-md-2">
			<div>
                <label style="margin-bottom: 0;" for="order_item_status"><?php echo translate_str_by_id(3604); ?></label>
            </div>
			<div>
				<select <?=((int)$order_item_status !== 0)?'style="background:#b9fcab;"':'';?> id="order_item_status" class="form-control">
				<option value="0"><?php echo translate_str_by_id(2094); ?></option>
				<?php
				foreach($orders_items_statuses as $status_id=>$status_data)
				{
					$selected = "";
					if($order_item_status == $status_id)
					{
						$selected = "selected=\"selected\"";
					}
					?>
					<option value="<?php echo $status_id; ?>" <?php echo $selected; ?>><?php echo translate_str_by_id($status_data["name"]); ?></option>
					<?php
				}
				?>
				</select>
			</div>
		</div>
	</div>
	
	<div class="box_btn_filter" style="margin:20px 0px 15px;">
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="filterOrdersItems();"><?php echo translate_str_by_id(2232); ?></button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="unsetFilterOrdersItems();"><?php echo translate_str_by_id(2555); ?></button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="location='<?php echo $multilang_params['lang_href']; ?>/shop/orders';"><?php echo translate_str_by_id(4562); ?></button>
	</div>
	
	<style>
	@media screen and (min-width: 768px) {
		.box_btn_filter .btn{
			display:inline-block;
		}
		.box_btn_filter .btn[onclick="location='<?php echo $multilang_params['lang_href']; ?>/shop/orders';"]{
			float:right;
		}
	}
	@media screen and (max-width: 767px) {
		.box_btn_filter .btn{
			display:block;
			float:none;
			width: 100%;
		}
	}
	</style>
	
	<script>
	// ------------------------------------------------------------------------------------------------
	//Устновка cookie в соответствии с фильтром
	function filterOrdersItems()
	{
		var my_orders_items_filter = new Object;
		
		//1. Время с
		my_orders_items_filter.time_from = encodeURIComponent(document.getElementById("time_from").value);
		
		//2. Время по
		my_orders_items_filter.time_to = encodeURIComponent(document.getElementById("time_to").value);
		
		//3. Номер заказа
		my_orders_items_filter.order_id = encodeURIComponent(document.getElementById("order_id").value);
		
		//4. Статус заказа
		my_orders_items_filter.order_status = encodeURIComponent(document.getElementById("order_status").value);
		
		//5. Оплачен
		my_orders_items_filter.paid = encodeURIComponent(document.getElementById("paid").value);
		
		//6. Статус позиции
		my_orders_items_filter.order_item_status = encodeURIComponent(document.getElementById("order_item_status").value);
		
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_orders_items_filter="+JSON.stringify(my_orders_items_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/shop/orders/items';
	}
	// ------------------------------------------------------------------------------------------------
	//Снять все фильтры
	function unsetFilterOrdersItems()
	{
		var my_orders_items_filter = new Object;
		
		//1. Время с
		my_orders_items_filter.time_from = "";
		
		//2. Время по
		my_orders_items_filter.time_to = "";
		
		//3. Номер заказа
		my_orders_items_filter.order_id = "";
		
		//4. Статус заказа
		my_orders_items_filter.order_status = 0;
		
		//5. Товар
		my_orders_items_filter.paid = -1;
		
		//6. Статус позиции
		my_orders_items_filter.order_item_status = 0;
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_orders_items_filter="+JSON.stringify(my_orders_items_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/shop/orders/items';
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<script>
	// ------------------------------------------------------------------------------------------------
	//Установка куки сортировки позиций заказов
	function sortOrdersItems(field)
	{
		var asc_desc = "asc";//Направление по умолчанию
		
		//Берем из куки текущий вариант сортировки
		var current_sort_cookie = getCookie("my_orders_items_sort");
		if(current_sort_cookie != undefined)
		{
			current_sort_cookie = JSON.parse(getCookie("my_orders_items_sort"));
			//Если поле это же - обращаем направление
			if(current_sort_cookie.field == field)
			{
				if(current_sort_cookie.asc_desc == "asc")
				{
					asc_desc = "desc";
				}
				else
				{
					asc_desc = "asc";
				}
			}
		}
		
		
		var my_orders_items_sort = new Object;
		my_orders_items_sort.field = field;//Поле, по которому сортировать
		my_orders_items_sort.asc_desc = asc_desc;//Направление сортировки
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_orders_items_sort="+JSON.stringify(my_orders_items_sort)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='<?php echo $multilang_params['lang_href']; ?>/shop/orders/items';
	}
	// ------------------------------------------------------------------------------------------------
	// возвращает cookie с именем name, если есть, если нет, то undefined
	function getCookie(name) 
	{
		var matches = document.cookie.match(new RegExp(
			"(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
		));
		return matches ? decodeURIComponent(matches[1]) : undefined;
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	
	<div style="overflow: hidden; overflow-x: auto;">
	<table class="table">
	<tr>
		<th class="hidden" style="vertical-align: middle; white-space: nowrap;"><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('id');" id="id_sorter">ID</a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('manufacturer');" id="manufacturer_sorter"><?php echo translate_str_by_id(2070); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('article');" id="article_sorter"><?php echo translate_str_by_id(2071); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap; min-width:200px;"><a href="javascript:void(0);" onclick="sortOrdersItems('product_name');" id="product_name_sorter"><?php echo translate_str_by_id(2102); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('price');" id="price_sorter"><?php echo translate_str_by_id(2751); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap; text-align:center;"><a href="javascript:void(0);" onclick="sortOrdersItems('count_need');" id="count_need_sorter"><?php echo translate_str_by_id(4526); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('price_sum');" id="price_sum_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('status');" id="status_sorter"><?php echo translate_str_by_id(2081); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('t2_time_to_exe');" id="t2_time_to_exe_sorter"><?php echo translate_str_by_id(3550); ?></a></th>
		
		
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
		<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('order_id');" id="order_id_sorter"><?php echo translate_str_by_id(3243); ?></a></th>
		<th class="hidden" style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrdersItems('office_id');" id="office_id_sorter"><?php echo translate_str_by_id(3506); ?></a></th>
	</tr>
	
	<script>
		<?php
		//Определяем текущую сортировку и обозначаем ее:
		$my_orders_items_sort = $_COOKIE["my_orders_items_sort"];
		$sort_field = "id";
		$sort_asc_desc = "desc";
		if($my_orders_items_sort != NULL)
		{
			$my_orders_items_sort = json_decode($my_orders_items_sort, true);
			$sort_field = $my_orders_items_sort["field"];
			$sort_asc_desc = $my_orders_items_sort["asc_desc"];
		}
		
		//Защита от SQL-инъекций
		if( $sort_asc_desc == 'asc' )
		{
			$sort_asc_desc = 'asc';
		}
		else
		{
			$sort_asc_desc = 'desc';
		}
		
		if( array_search($sort_field, array('id', 'product_name', 'price', 'count_need', 'price_sum', 'status', 't2_time_to_exe', 'time', 'order_id', 'office_id', 'article', 'manufacturer') ) === false )
		{
			$sort_field = "id";
		}
		
		?>
		document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px; vertical-align: initial;\" />";
	</script>
	
	
	
	<?php
	//Формируем сложный SQL-запрос для получения всей информации по каждой позиции
	
	//Запрос времени оформления заказа
	$SELECT_time = '(SELECT `time` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)';
	//Запрос суммы позиции
	$SELECT_price_sum = '`price`*`count_need`';
	//Запрос офисов обслуживания
	$SELECT_offices = '(SELECT `office_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)';
	//Запрос статуса заказа
	$SELECT_order_status = '(SELECT `status` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)';
	//Запрос флаг "Заказ оплачен"
	$SELECT_paid = '(SELECT `paid` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`)';
	
	//Фильтры
	//Для отсева по пользователю
	$WHERE_CONDITIONS = ' WHERE `order_id` IN(SELECT `id` FROM `shop_orders` WHERE `shop_orders`.`user_id` = ?)';
	
	$binding_values = array();
	array_push($binding_values, $user_id);
	
	//Ставим ПОЛЬЗОВАТЕЛЬСКИЕ фильтры
	$my_orders_items_filter = NULL;
	if(isset($_COOKIE["my_orders_items_filter"]))
	{
		$my_orders_items_filter = $_COOKIE["my_orders_items_filter"];
	}
	if($my_orders_items_filter != NULL)
	{
		$my_orders_items_filter = json_decode($my_orders_items_filter, true);
		
		//1. Время с
		if($my_orders_items_filter["time_from"] != '')
		{
			$WHERE_CONDITIONS .= ' AND '.$SELECT_time.' > ?';
			
			array_push($binding_values, (int) $my_orders_items_filter["time_from"]);
		}

		//2. Время по
		if($my_orders_items_filter["time_to"] != '')
		{
			$WHERE_CONDITIONS .= ' AND '.$SELECT_time.' < ?';
			
			array_push($binding_values, (int) $my_orders_items_filter["time_to"]);
		}

		//3. Номер заказа
		if($my_orders_items_filter["order_id"] != '')
		{
			$WHERE_CONDITIONS .= ' AND `order_id` = ?';
			
			array_push($binding_values, (int) $my_orders_items_filter["order_id"]);
		}
		
		//4. Статус заказа
		if($my_orders_items_filter["order_status"] != 0 )
		{
			$WHERE_CONDITIONS .= ' AND '.$SELECT_order_status.' = ?';
			
			array_push($binding_values, (int) $my_orders_items_filter["order_status"]);
		}
		
		//5. Оплата
		if($my_orders_items_filter["paid"] != -1 )
		{
			$WHERE_CONDITIONS .= ' AND '.$SELECT_paid.' = ?';
			
			array_push($binding_values, (int) $my_orders_items_filter["paid"]);
		}
		
		//6. Статус позиции
		if($my_orders_items_filter["order_item_status"] != 0 )
		{
			$WHERE_CONDITIONS .= ' AND `status` = ?';
			
			array_push($binding_values, (int) $my_orders_items_filter["order_item_status"]);
		}
	}
	
	
	// Текущая страница
	$page = 1;
	if( isset($_GET['page']) )
	{
		$page = (int) $_GET['page'];
	}
	if(empty($page))
	{
		$page = 1;
	}
	$lim_rows = $DP_Config->list_page_limit;// Количество строк на страницу
	$from_rows = ($page * $lim_rows) - $lim_rows;// С какой записи выводить
	
	
	//ЗАПРОС 
	$SQL_SELECT_ITEMS = 'SELECT SQL_CALC_FOUND_ROWS *, 
		'.$SELECT_price_sum.' AS `price_sum`, 
		'.$SELECT_offices.' AS `office_id`, 
		'.$SELECT_time.' AS `time`,
		'.$SELECT_order_status.' AS `order_status`,
		'.$SELECT_paid.' AS `paid`, 
		`t2_name` AS `product_name`, 
		`t2_article` AS `article`, 
		`t2_manufacturer` AS `manufacturer` 
		FROM `shop_orders_items` '.$WHERE_CONDITIONS.' ORDER BY `'.$sort_field.'` '.$sort_asc_desc.' LIMIT '.$from_rows.', '.$lim_rows;
	
	
	$elements_query = $db_link->prepare($SQL_SELECT_ITEMS);
	$elements_query->execute($binding_values);
	
	$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
	$elements_count_rows_query->execute();
	$all_rows = $elements_count_rows_query->fetchColumn();
	
	
	//Массивы для JS с id элементов и с чекбоксами элементов
	$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
	$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
	
	$items_counter = 0;
	
	while( $item = $elements_query->fetch() )
	{
		$items_counter++;
		
		//Для Javascript
		$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$item["id"]."\";\n";//Добавляем элемент для JS
		$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$item["id"].";\n";//Добавляем элемент для JS
		
		$item_id = $item["id"];
		$item_product_type = $item["product_type"];
		$item_status = $item["status"];
		$item_order_id = $item["order_id"];
		$item_product_name = $item["product_name"];
		$item_price = number_format($item["price"], 2, '.', ' ');
		$item_count_need = $item["count_need"];
		$item_price_sum = number_format($item["price_sum"], 2, '.', ' ');
		$item_office_id = $item["office_id"];
		$item_time = $item["time"];
		$item_t2_time_to_exe = $item["t2_time_to_exe"];
		$item_t2_time_to_exe_guaranteed = $item["t2_time_to_exe_guaranteed"];
		$article = $item["article"];
		$manufacturer = $item["manufacturer"];
		$paid = $item["paid"];
		
		
		//Срок доставки для продуктов типа 2
		if($item_t2_time_to_exe < $item_t2_time_to_exe_guaranteed)
		{
			$item_t2_time_to_exe = $item_t2_time_to_exe." - ".$item_t2_time_to_exe_guaranteed;
		}
		$item_t2_time_to_exe = $item_t2_time_to_exe." дн.";
		if( !isset($item_product_type) )
		{
			$item_product_type = null;
		}
		if($item_product_type == 1)
		{
			$item_t2_time_to_exe = "";
		}
		?>
		<tr style="background:<?php echo $orders_items_statuses[$item_status]["color"]; ?>">
			<td class="hidden" style="line-height: 1em; vertical-align: middle;">
				<input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $item_id; ?>');" id="checked_<?php echo $item_id; ?>" name="checked_<?php echo $item_id; ?>"/>
			</td>
			<td style="line-height: 1em; vertical-align: middle; white-space: nowrap;"><?php echo $item_id; ?></td>
			<td style="line-height: 1em; vertical-align: middle;"><?php echo $manufacturer; ?></td>
			<td style="line-height: 1em; vertical-align: middle;"><?php echo $article; ?></td>
			<td style="line-height: 1em; vertical-align: middle;"><?php echo $item_product_name; ?></td>
			<td style="line-height: 1em; vertical-align: middle; white-space: nowrap;"><?php echo $item_price; ?></td>
			<td style="line-height: 1em; vertical-align: middle; white-space: nowrap; text-align:center;"><?php echo $item_count_need; ?></td>
			<td style="line-height: 1em; vertical-align: middle; white-space: nowrap;"><?php echo $item_price_sum; ?></td>
			<td style="line-height: 1em; vertical-align: middle;"><?php echo translate_str_by_id($orders_items_statuses[$item_status]["name"]); ?></td>
			<td style="line-height: 1em; vertical-align: middle; white-space: nowrap;"><?php echo $item_t2_time_to_exe; ?></td>
			<td style="line-height: 1em; vertical-align: middle;"><?php echo date("d.m.Y", $item_time)."<br><small>".date("G:i", $item_time).'</small>'; ?></td>
			<td style="line-height: 1em; vertical-align: middle; white-space: nowrap;">
				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/orders/order?order_id=<?php echo $item_order_id; ?>">
					<i class="fa fa-sign-in" aria-hidden="true"></i> <?php echo translate_str_by_id(3243); ?> <?php echo $item_order_id; ?>
					<br>
					<?php
					$paid_caption = "";
					if( $paid == 0 )
					{
						$paid_caption = translate_str_by_id(3513);
					}
					else if( $paid == 1 )
					{
						$paid_caption = translate_str_by_id(3514);
					}
					else if( $paid == 2 )
					{
						$paid_caption = translate_str_by_id(3515);
					}
					?>
					<span style="font-weight:bold;font-size:0.8em;"><?php echo $paid_caption; ?></span>
				</a>
			</td>
			<td class="hidden" style="line-height: 1em; vertical-align: middle;"><?php echo translate_str_by_id(3506); ?>: <?php echo $offices_list[$item_office_id]["caption"]; ?></td>
			
		</tr>
	<?php
	}//while() - по позициям
	
	if($items_counter == 0){
		echo '<tr><td colspan="12">'.translate_str_by_id(4563).'</td></tr>';
	}
	?>
	</table>
	</div>
	
	<?php
	if(ceil($all_rows / $lim_rows) > 1){
	?>
	<div class="text-center">
		<ul class="pagination">
		<?php
		echo pagination($all_rows, $lim_rows, 2, $page, 'active');
		?>
		</ul>
	</div>
	<?php
	}
	?>
	
	<script>
	// ----------------------------------------------------------------------------------------
	<?php
	echo $for_js;//Выводим массив с чекбоксами для элементов
	?>
	//Обработка переключения Выделить все/Снять все
	function on_check_uncheck_all()
	{
		var state = document.getElementById("check_uncheck_all").checked;
		
		for(var i=0; i<elements_array.length;i++)
		{
			document.getElementById(elements_array[i]).checked = state;
		}
	}//~function on_check_uncheck_all()
	// ----------------------------------------------------------------------------------------
	//Обработка переключения одного чекбокса
	function on_one_check_changed(id)
	{
		//Если хотя бы один чекбокс снят - снимаем общий чекбокс
		for(var i=0; i<elements_array.length;i++)
		{
			if(document.getElementById(elements_array[i]).checked == false)
			{
				document.getElementById("check_uncheck_all").checked = false;
				break;
			}
		}
	}//~function on_one_check_changed(id)
	// ----------------------------------------------------------------------------------------
	//Получение массива id отмеченых элементов
	function getCheckedElements()
	{
		var checked_ids = new Array();
		//По массиву чекбоксов
		for(var i=0; i<elements_array.length;i++)
		{
			if(document.getElementById(elements_array[i]).checked == true)
			{
				checked_ids.push(elements_id_array[i]);
			}
		}
		
		return checked_ids;
	}
	// ----------------------------------------------------------------------------------------
	</script>
<?php
}
else//Пользователь не авторизован
{
?>
	<p><?php echo translate_str_by_id(4564); ?></p>
	<div class="panel panel-primary">
	<?php
	//Единый механизм формы авторизации
	$login_form_postfix = "my_orders_items";
	require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
	?>
	</div>
<?php
}
?>




<div id="users_agreement_div" style="padding: 0px 15px; border: 1px solid #ddd; background: #f7f7f7; margin:20px 0px;">
	<table>
		<tr>
			<td><i class="fa fa-info-circle" aria-hidden="true"></i></td>
			<td style="line-height: 1.2em; padding: 15px 5px;"><?php echo translate_str_by_id(4560); ?> <a class="text_a" href="<?php echo $multilang_params['lang_href']; ?>/shop/orders/zakaz-bez-registracii"><?php echo translate_str_by_id(4561); ?></a></td>
		</tr>
	</table>
</div>




<?php
// формируем пагинацию
// $all 		= количество постов в категории (определяем количество постов в базе данных)
// $lim 		= количество постов, размещаемых на одной странице
// $prev 		= количество отображаемых ссылок до и после номера текущей страницы
// $curr_link 	= номер текущей страницы (получаем из URL)
// $curr_css 	= css-стиль для ссылки на "текущую (активную)" страницу
// $link 		= часть адреса, используемый для формирования линков на другие страницы
function pagination($all, $lim, $prev, $curr_link, $curr_css, $link='')
{
    global $DP_Content;
	
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
    if ($first > 1) $html .= "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}'>1</a></li>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$y}' >...</a></li>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$y}' >$i</a></li>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= '<li class="'.$curr_css.'"><a>'. $i .'</a></li>';
        } else {
            $alink = "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}";
            if($i != 1) $alink .= "?page={$i}";
            $alink .= "'>$i</a></li>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < ceil($all / $lim) && ceil($all / $lim) - $last > 2) $html .=  "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$y}' >...</a></li>";
    // выводим последнюю страницу
    $e = ceil($all / $lim);
    if ($last < ceil($all / $lim)) $html .=  "<li><a href='".$multilang_params['lang_href']."/{$DP_Content->url}?page={$e}' >$e</a></li>";
	
	return $html;
}
?>