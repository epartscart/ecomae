<?php
/**
 * Страница для вывода всех позиций всех КОРЗИН
*/
defined('_ASTEXE_') or die('No access');

//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/order_process/orders_background.php")
?>

<?php
if(isset($_POST["action"]))
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

    <link rel="stylesheet" href="/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/assets/modal.css"><!--//Статистика-->
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" onClick="deleteCartItems();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>

	

	<script>
	//Удаление отмеченных записей корзин
	function deleteCartItems()
	{
		var records_to_del = getCheckedElements();
		
		if(records_to_del.length == 0)
		{
			alert("<?php echo translate_str_by_id(3495); ?>");
			return;
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.records_to_del = records_to_del;
    
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/content/shop/order_process/ajax_delete_cart_record.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                console.log(answer);
                if(answer.status == true)
                {
                    //Обновляем страницу
					location = "/<?php echo $DP_Config->backend_dir; ?>/shop/orders/carts";
                }
                else
                {
                    alert("<?php echo translate_str_by_id(3496); ?>");
                }
            }
        });
		
		
		
		console.log(getCheckedElements());
	}
	</script>
	
	
    
    
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3497); ?>
			</div>
			<div class="panel-body filter_panel">
				<?php
				$time_from = "";//1. Время с
				$time_to = "";//2. Время по
				$customer = "";//3. Покупатель
				$storage_id = "0";//4. Склад, с которого зарезервирован товар
				
				//Получаем текущие значения фильтра:
				$carts_items_filter = NULL;
				if( isset($_COOKIE["carts_items_filter"]) )
				{
					$carts_items_filter = $_COOKIE["carts_items_filter"];
				}
				if($carts_items_filter != NULL)
				{
					$carts_items_filter = json_decode($carts_items_filter, true);
					$time_from = $carts_items_filter["time_from"];
					$time_to = $carts_items_filter["time_to"];
					$customer = $carts_items_filter["customer"];
					$storage_id = $carts_items_filter["storage_id"];
				}
				?>
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3237); ?>
						</label>
						<div class="col-lg-6">
							<div style="position:relative;height:34px;">
								<input style="position:absolute; z-index:2; opacity:0;" type="text"  id="time_from" value="<?php echo $time_from; ?>" class="form-control" />
								<input style="position:absolute; z-index:1; <?=(!empty($time_from))?'background:#b9fcab;':'';?>" type="text" id="time_from_show" class="form-control" />
								<script>
								//Инициализируем datetimepicker
								jQuery("#time_from").datetimepicker({
									lang:"<?php echo $multilang_params['lang']; ?>",
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
					</div>
				</div>
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3238); ?>
						</label>
						<div class="col-lg-6">
							<div style="position:relative;height:34px;">
								<input style="position:absolute; z-index:2; opacity:0;" type="text"  id="time_to" value="<?php echo $time_to; ?>" class="form-control" />
								<input style="position:absolute; z-index:1; <?=(!empty($time_to))?'background:#b9fcab;':'';?>" type="text" id="time_to_show" class="form-control" />
								<script>
								//Инициализируем datetimepicker
								jQuery("#time_to").datetimepicker({
									lang:"<?php echo $multilang_params['lang']; ?>",
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
					</div>
				</div>
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3245); ?>
						</label>
						<div class="col-lg-6">
							<input <?=(!empty($customer))?'style="background:#b9fcab;"':'';?> type="text" id="customer" value="<?php echo $customer; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2750); ?>
						</label>
						<div class="col-lg-6">
							<select <?=($storage_id > 0)?'style="background:#b9fcab;"':'';?> id="storage_id" class="form-control">
								 <option value="0"><?php echo translate_str_by_id(2094); ?></option>
								 <?php
								 foreach($available_storages_list as $storage_id_key => $storage_name)
								 {
									$selected = "";
									if($storage_id == $storage_id_key)
									{
										$selected = "selected=\"selected\"";
									}
									?>
									 <option value="<?php echo $storage_id_key; ?>" <?php echo $selected; ?>><?php echo $storage_name; ?></option>
									 <?php
								 }
								 ?>
							</select>
						</div>
					</div>
				</div>
			</div>
			<script>
			$(".filter_panel .form-control").keyup(function(event){
				if(event.keyCode == 13){
					filterCartsItems();
				}
			});
			</script>
			<div class="panel-footer">
				<div class="row">
					<div class="col-lg-12 float-e-margins">
						<button class="btn btn-success" type="button" onclick="filterCartsItems();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
						<button class="btn btn-primary" type="button" onclick="unsetFilterCartsItems();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2555); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>



    <!--//Статистика-->
    <script src="/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/assets/main.js"></script>
    <script>
        function closeCustomerModalInfo(user_id) {
            if (window.event.srcElement.id == 'customer-modal-info-' + user_id || window.event.srcElement.id == 'close-customer-modal-info-' + user_id)
                document.querySelector('#customer-modal-info-' + user_id).classList.remove('customer-modal-info-wrapper-show');
        }

        function showCustomerModalInfo(customer_id)
        {
            jQuery.ajax({
                type: "POST",
                async: false, //Запрос синхронный
                url: "/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/frontAjax/ajax_loadUserModal.php",
                dataType: "json",//Тип возвращаемого значения
                data: "customer_id="+customer_id+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer){
                    if(answer.status == true)
                    {
                        document.querySelector('#customer-modal-info-' + customer_id).classList.add('customer-modal-info-wrapper-show');
                        document.querySelector('#customer-modal-info-' + customer_id).innerHTML = answer.modal;
                    }
                    else
                    {
                        alert("<?php echo translate_str_by_id(3541); ?>");
                    }
                }
            });
        }
    </script>
    <!--END //Статистика-->

    <script>
    // ------------------------------------------------------------------------------------------------
    //Устновка cookie в соответствии с фильтром
    function filterCartsItems()
    {
        var carts_items_filter = new Object;
        
        //1. Время с
        carts_items_filter.time_from = document.getElementById("time_from").value;
        
        //2. Время по
        carts_items_filter.time_to = document.getElementById("time_to").value;
        
        //3. Покупатель
        carts_items_filter.customer = document.getElementById("customer").value;
     
        //4. Склад
        carts_items_filter.storage_id = document.getElementById("storage_id").value;
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "carts_items_filter="+JSON.stringify(carts_items_filter)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/carts';
    }
    // ------------------------------------------------------------------------------------------------
    //Снять все фильтры
    function unsetFilterCartsItems()
    {
        var carts_items_filter = new Object;
        
        //1. Время с
        carts_items_filter.time_from = "";
        
        //2. Время по
        carts_items_filter.time_to = "";
        
        //3. Покупатель
        carts_items_filter.customer = "";

        //4. Склад
        carts_items_filter.storage_id = 0;
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "carts_items_filter="+JSON.stringify(carts_items_filter)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/carts';
    }
    // ------------------------------------------------------------------------------------------------
    </script>
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    <script>
    // ------------------------------------------------------------------------------------------------
    //Установка куки сортировки позиций корзины
    function sortCartsItems(field)
    {
        var asc_desc = "asc";//Направление по умолчанию
        
        //Берем из куки текущий вариант сортировки
        var current_sort_cookie = getCookie("carts_items_sort");
        if(current_sort_cookie != undefined)
        {
            current_sort_cookie = JSON.parse(getCookie("carts_items_sort"));
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
        
        
        var carts_items_sort = new Object;
        carts_items_sort.field = field;//Поле, по которому сортировать
        carts_items_sort.asc_desc = asc_desc;//Направление сортировки
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "carts_items_sort="+JSON.stringify(carts_items_sort)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/carts';
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
    //Переход на другую страницу заказа
    function goToPage(need_page)
    {
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "carts_items_need_page="+need_page+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/carts';
    }
    // ------------------------------------------------------------------------------------------------
    </script>
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3498); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<?php
					//Определяем текущую сортировку и обозначаем ее:
					$carts_items_sort = NULL;
					if( isset($_COOKIE["carts_items_sort"]) )
					{
						$carts_items_sort = $_COOKIE["carts_items_sort"];
					}
					$sort_field = "id";
					$sort_asc_desc = "desc";
					if($carts_items_sort != NULL)
					{
						$carts_items_sort = json_decode($carts_items_sort, true);
						$sort_field = $carts_items_sort["field"];
						$sort_asc_desc = $carts_items_sort["asc_desc"];
					}
					
					if( strtolower($sort_asc_desc) == "asc" )
					{
						$sort_asc_desc = "asc";
					}
					else
					{
						$sort_asc_desc = "desc";
					}
					
					if( array_search($sort_field, array('id', 'product_name', 'article', 'manufacturer', 'price', 'count_need', 'price_sum', 'price_purchase_sum', 'profit', 'time', 'user_id') ) === false )
					{
						$sort_field = "id";
					}
					
					
					//Формируем сложный SQL-запрос для получения всей информации по каждой позиции
					
					
					//Запрос суммы позиции
					$SELECT_price_sum = "`price`*`count_need`";
					//Запрос закупа
					$SELECT_price_purchase_sum = "IFNULL((SELECT SUM(`price_purchase`*`count_reserved`) FROM `shop_carts_details` WHERE `cart_record_id` = `shop_carts`.`id`), CAST(`t2_price_purchase`*`count_need` AS DECIMAL(8,2)))";
					//Запрос маржы
					$SELECT_profit = "($SELECT_price_sum - $SELECT_price_purchase_sum)";

					//Фильтры
					$WHERE_CONDITIONS = "";
					
					$binding_values = array();
					
					//Ставим ПОЛЬЗОВАТЕЛЬСКИЕ фильтры
					$need_storage_id = 0;//По умолчанию, не отсеиваем записи по складу
					$carts_items_filter = NULL;
					if( isset($_COOKIE["carts_items_filter"]) )
					{
						$carts_items_filter = $_COOKIE["carts_items_filter"];
					}
					if($carts_items_filter != NULL)
					{
						$carts_items_filter = json_decode($carts_items_filter, true);

						//1. Время с
						if($carts_items_filter["time_from"] != "")
						{
							$WHERE_CONDITIONS .= " `time` > ?";
							
							array_push($binding_values, $carts_items_filter["time_from"]);
						}

						//2. Время по
						if($carts_items_filter["time_to"] != "")
						{
							if($WHERE_CONDITIONS != "") $WHERE_CONDITIONS .= " AND ";
							$WHERE_CONDITIONS .= " `time` < ?";
							
							array_push($binding_values, $carts_items_filter["time_to"]);
						}
						
						//3. Покупатель
						if($carts_items_filter["customer"] != "" )
						{
							if($WHERE_CONDITIONS != "") $WHERE_CONDITIONS .= " AND ";
							$WHERE_CONDITIONS .= " `user_id` = ?";
							
							array_push($binding_values, $carts_items_filter["customer"]);
						}

						//4. Склад, который присутствует в позиции
						if($carts_items_filter["storage_id"] != 0 )
						{
							$need_storage_id = $carts_items_filter["storage_id"];
						}
					}
					if($WHERE_CONDITIONS != "")
					{
						$WHERE_CONDITIONS = " WHERE ".$WHERE_CONDITIONS;
					}

					//ЗАПРОС 
					$SQL_SELECT_ITEMS = "SELECT SQL_CALC_FOUND_ROWS *,
						CAST($SELECT_price_sum AS DECIMAL(8,2)) AS `price_sum`,
						$SELECT_price_purchase_sum AS `price_purchase_sum`,
						CAST($SELECT_profit AS DECIMAL(8,2)) AS `profit`,
						`t2_name` AS `product_name`, 
						`t2_article` AS `article`, 
						`t2_manufacturer` AS `manufacturer` 
					FROM `shop_carts` $WHERE_CONDITIONS ORDER BY `$sort_field` $sort_asc_desc;";
					
					

					
					$elements_query = $db_link->prepare($SQL_SELECT_ITEMS);
					$elements_query->execute($binding_values);
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					
					//Массивы для JS с id элементов и с чекбоксами элементов
					$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
					$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
					
					//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
					//---------------------------------------------------------------------------------------------->
					//Определяем количество страниц для вывода:
					$p = $DP_Config->list_page_limit;//Штук на страницу
					$count_pages = (int)($elements_count_rows / $p);//Количество страниц
					if($elements_count_rows%$p)//Если остались еще элементы
					{
						$count_pages++;
					}
					//Определяем, с какой страницы начать вывод:
					$s_page = 0;
					if( isset($_COOKIE['carts_items_need_page']) )
					{
						$s_page = $_COOKIE['carts_items_need_page'];
						if($s_page >= $count_pages)
						{
							$s_page = $count_pages-1;//Чтобы не выходить за пределы
						}
					}
					$elements_counter = 0;
					//----------------------------------------------------------------------------------------------|
					?>
					<table id="carts_items_table" class="footable table table-hover toggle-arrow " data-sort="false" data-page-size="<?php echo $elements_count_rows; ?>">
						<thead>
							<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
							<th data-toggle="true"></th>
							<th><a href="javascript:void(0);" onclick="sortCartsItems('id');" id="id_sorter">ID</a></th>
							<th><a href="javascript:void(0);" onclick="sortCartsItems('manufacturer');" id="manufacturer_sorter"><?php echo translate_str_by_id(2070); ?></a></th>
							<th><a href="javascript:void(0);" onclick="sortCartsItems('article');" id="article_sorter"><?php echo translate_str_by_id(2071); ?></a></th>
							<th><a href="javascript:void(0);" onclick="sortCartsItems('product_name');" id="product_name_sorter"><?php echo translate_str_by_id(2102); ?></a></th>
							<th data-hide="phone"><a href="javascript:void(0);" onclick="sortCartsItems('price');" id="price_sorter"><?php echo translate_str_by_id(2751); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortCartsItems('count_need');" id="count_need_sorter"><?php echo translate_str_by_id(2752); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortCartsItems('price_sum');" id="price_sum_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortCartsItems('price_purchase_sum');" id="price_purchase_sum_sorter"><?php echo translate_str_by_id(3425); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortCartsItems('profit');" id="profit_sorter"><?php echo translate_str_by_id(3499); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortCartsItems('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
							<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortCartsItems('user_id');" id="user_id_sorter"><?php echo translate_str_by_id(3245); ?></a></th>
							<th data-hide="phone,tablet,default"><?php echo translate_str_by_id(3500); ?></th>
						</thead>
						<tbody>
							<?php
							$items_counter = 0;
							while( $item = $elements_query->fetch() )
							{
								$item_id = $item["id"];

								//Отсеим те позиции корзины, среди которых нет товара, зарезервированного на указанном складе
								if($need_storage_id != 0)
								{
									if($item["product_type"] == 1)//Для типа продукта = 1
									{
										$item_storages = array();
										$item_storages_query = $db_link->prepare("SELECT DISTINCT(`storage_id`) FROM `shop_carts_details` WHERE `cart_record_id` = ?;");
										$item_storages_query->execute( array($item_id) );
										while($item_storage = $item_storages_query->fetch() )
										{
											array_push($item_storages, $item_storage["storage_id"]);
										}
										if( array_search($need_storage_id, $item_storages) === false)
										{   
											continue;
										}
									}
									else if($item["product_type"] == 2)//Для типа продукта = 2
									{
										if($need_storage_id != $item["t2_storage_id"])
										{
											continue;
										}
									}
								}

								//Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
								if($elements_counter < $s_page*$p)
								{
									$elements_counter++;
									continue;
								}
								if($elements_counter >= $s_page*$p+$p)
								{
									break;
								}
								$elements_counter++;
								//Для Javascript
								$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$item["id"]."\";\n";//Добавляем элемент для JS
								$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$item["id"].";\n";//Добавляем элемент для JS

								$item_product_name = $item["product_name"];
								$item_article = $item["article"];
								$item_manufacturer = $item["manufacturer"];
								$item_product_type = $item["product_type"];
								$item_price = $item["price"];
								$item_count_need = $item["count_need"];
								$item_price_sum = $item["price_sum"];
								$item_customer_id = $item["user_id"];
								$item_price_purchase_sum = $item["price_purchase_sum"];
								$item_profit = $item["profit"];
								$item_time = $item["time"];
                                $customer_id = $item_customer_id;//Статистика
								?>
								<tr id="order_item_record_<?php echo $item_id; ?>">
									<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $item_id; ?>');" id="checked_<?php echo $item_id; ?>" name="checked_<?php echo $item_id; ?>"/></td>
									<td></td>
									<td><?php echo $item_id; ?></td>
									<td><?php echo $item_manufacturer; ?></td>
									<td><?php echo $item_article; ?></td>
									<td><?php echo $item_product_name; ?></td>
									<td><?php echo $item_price; ?></td>
									<td><?php echo $item_count_need; ?></td>
									<td><?php echo $item_price_sum; ?></td>
									<td><?php echo $item_price_purchase_sum; ?></td>
									<td><?php echo $item_profit; ?></td>
									<td><?php echo date("d.m.Y", $item_time)." ".date("G:i", $item_time); ?></td>
									<td><?php include $_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/users/statistics/modal.php';//Статистика?><?php echo $item_customer_id; ?></td>
									<td>
										<div class="row">
											<div class="col-lg-12">
												<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped table-bordered">
													<thead>
														<th><?php echo translate_str_by_id(2750); ?></th>
														<th><?php echo translate_str_by_id(3501); ?></th>
														<th><?php echo translate_str_by_id(3425); ?></th>
														<th><?php echo translate_str_by_id(2752); ?></th>
														<th><?php echo translate_str_by_id(3502); ?></th>
													</thead>
													<tbody>
													<?php
													//ДАЛЕЕ ВЫВОДИТСЯ СКЛАДСКАЯ ИНФОРМАЦИЯ
													if($item_product_type == 1)
													{
														//Выводим данные по поставкам
														$details_query = $db_link->prepare("SELECT *, `count_reserved`*`price_purchase` AS `price_purchase_sum` FROM `shop_carts_details` WHERE `cart_record_id` = ?;");
														$details_query->execute( array($item_id) );
														while( $detail = $details_query->fetch() )
														{
															?>
															<tr>
																<td><?php echo $storages_list[$detail["storage_id"]]; ?></td>
																<td><?php echo $detail["storage_record_id"]; ?></td>
																<td><?php echo $detail["price_purchase"]; ?></td>
																<td><?php echo $detail["count_reserved"]; ?></td>
																<td><?php echo $detail["price_purchase_sum"]; ?></td>
															</tr>
															<?php
														}
													}
													else if($item_product_type == 2)//Для запчастей Docpart
													{
														?>
														<tr>
															<td><?php echo $storages_list[$item["t2_storage_id"]]; ?></td>
															<td><?php echo "-"; ?></td>
															<td><?php echo $item["t2_price_purchase"]; ?></td>
															<td><?php echo $item["count_need"]; ?></td>
															<td><?php echo $item["t2_price_purchase"]*$item["count_need"]; ?></td>
														</tr>
														<?php
													}
													?>
														<tr>
															<td colspan="2"></td>
															<td><strong><?php echo translate_str_by_id(3503); ?></strong></td>
															<td><strong><?php echo $item_count_need; ?></strong></td>
															<td><strong><?php echo $item_price_purchase_sum; ?></strong></td>
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
				</div>
				
				
				
				
				<?php
				//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				if( $count_pages > 1 )
				{
					?>
					<div class="row">
						<div class="col-lg-12 text-center">
							<div class="dataTables_paginate paging_simple_numbers">
								<ul class="pagination">
								<?php
								for($i=0; $i < $count_pages; $i++)
								{
									//Класс первой страницы
									$previous = "";
									if($i == 0) $previous = "previous";
									
									//Класс последней страницы
									$next = "";
									if($i == $count_pages-1) $next = "next";
									
									if($i == $s_page)//Текущая страница
									{
										?>
										<li class="paginate_button active <?php echo $previous; ?> <?php echo $next; ?>"><a href="javascript:void(0);"><?php echo $i; ?></a></li>
										<?php
									}
									else
									{
										?>
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="javascript:void(0);" onclick="goToPage(<?php echo $i; ?>);"><?php echo $i; ?></a></li>
										<?php
									}
								}
								?>
								</ul>
							</div>
						</div>
					</div>
				<?php
				}
				//END ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				?>
			</div>
		</div>
	</div>
	
	
	
	
	<script>
		jQuery( window ).load(function() {
			$('#carts_items_table').footable();
			
			document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
		});
	</script>
	
	
	
	

    
    
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
?>