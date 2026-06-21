<?php
/**
 * Страничный скрипт для просмотра своих заказов
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

//Общая информация по заказам
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");


//Если пользователь авторизован, выводим его заказы
if($user_id > 0)
{
    //Способ оплаты - на странице созданного заказа
	$shop_orders_paid_type = array();
	$query = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;');
	$query->execute();
	while($rov = $query->fetch()){
		$shop_orders_paid_type[$rov['id']] = $rov['name'];
	}
	
	//Гараж
	$garage_list = array();
	$cars_query = $db_link->prepare('SELECT * FROM `shop_docpart_garage` WHERE `user_id` = :user_id ORDER BY `active` DESC, `caption` ASC;');
	$cars_query->bindValue(':user_id', $user_id);
	$cars_query->execute();
	while( $car = $cars_query->fetch() )
	{
		$garage_list[$car['id']] = str_replace(array('"',"'","\n","\r","\t"), '', $car['caption']);
	}
	if(isset($_GET['garage']) && isset($garage_list[$_GET['garage']]))
	{
		$my_orders_filter = json_decode($_COOKIE["my_orders_filter"], true);
		if( empty($my_orders_filter) )
		{
			$my_orders_filter = array();
		}
		
		if( isset($my_orders_filter) )
		{
			$my_orders_filter["time_from"]	= "";
			$my_orders_filter["time_to"] 	= "";
			$my_orders_filter["order_id"] 	= "";
			$my_orders_filter["status"] 	= 0;
			$my_orders_filter["paid"] 		= -1;
			$my_orders_filter["paid_type"] 	= -1;
			$my_orders_filter["garage"] 	= (int) $_GET['garage'];
			
			$_COOKIE["my_orders_filter"] = json_encode($my_orders_filter);
			?>
			<script>
			//Удалить параметр из адреса
			let url = new URL(document.location);
			let searchParams = url.searchParams;
			searchParams.delete("garage");
			window.history.pushState({}, '', url.toString());
			
			//Устанавливаем cookie (на полгода)
			var my_orders_filter = new Object;
				my_orders_filter.time_from = "";
				my_orders_filter.time_to = "";
				my_orders_filter.order_id = "";
				my_orders_filter.status = 0;
				my_orders_filter.paid = -1;
				my_orders_filter.paid_type = -1;
				my_orders_filter.garage = <?php echo (int) $_GET['garage']; ?>;
				
			var date = new Date(new Date().getTime() + 15552000 * 1000);
			document.cookie = "my_orders_filter="+JSON.stringify(my_orders_filter)+"; path=/; expires=" + date.toUTCString();
			</script>
			<?php
		}
	}
	
	$time_from 	= "";
    $time_to 	= "";
    $order_id 	= "";
    $status 	= 0;
    $paid 		= -1;
	$paid_type  = -1;
	$garage  	= 0;
	
    //Получаем текущие значения фильтра:
	$my_orders_filter = NULL;
    if( isset($_COOKIE["my_orders_filter"]) )
    {
        $my_orders_filter = json_decode($_COOKIE["my_orders_filter"], true);
		if( ! empty($my_orders_filter) ){
			$time_from 	= $my_orders_filter["time_from"];
			$time_to 	= $my_orders_filter["time_to"];
			$order_id 	= $my_orders_filter["order_id"];
			$status 	= (int) $my_orders_filter["status"];
			$paid 		= (int) $my_orders_filter["paid"];
			$paid_type  = (int) $my_orders_filter["paid_type"];
			$garage		= (int) $my_orders_filter["garage"];
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
                <input <?=($order_id !== '')?'style="background:#b9fcab;"':'';?> type="text"  id="order_id" value="<?php echo $order_id; ?>" class="form-control" />
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
		
		<div class="col-md-2 hidden">
            <div>
                <label style="margin-bottom: 0;" for="paid_type"><?php echo translate_str_by_id(4645); ?></label>
            </div>
            <div>
                <select <?=((int)$paid_type !== -1)?'style="background:#b9fcab;"':'';?> id="paid_type" class="form-control">
                    <option value="-1"><?php echo translate_str_by_id(2094); ?></option>
					<?php
					foreach($shop_orders_paid_type as $k => $v){
					?>
					<option value="<?=$k;?>"><?=$v;?></option>
					<?php
					}
					?>
                    <option value="0"><?php echo translate_str_by_id(3253); ?></option>
                </select>
                <script>
                    document.getElementById("paid_type").value = <?php echo $paid_type; ?>;
                </script>
            </div>
        </div>
		
		<div class="col-md-2">
            <div>
                <label style="margin-bottom: 0;" for="garage"><?php echo translate_str_by_id(627); ?></label>
            </div>
            <div>
                <select <?=((int)$garage !== 0)?'style="background:#b9fcab;"':'';?> id="garage" class="form-control">
                    <option value="0"><?php echo translate_str_by_id(2094); ?></option>
					<?php
					foreach($garage_list as $k => $v){
					?>
					<option value="<?=$k;?>"><?=$v;?></option>
					<?php
					}
					?>
                    <option value="-1"><?php echo translate_str_by_id(3253); ?></option>
                </select>
                <script>
                    document.getElementById("garage").value = <?php echo $garage; ?>;
                </script>
            </div>
        </div>
		
		<div class="col-md-2">
            <div>
                <label style="margin-bottom: 0;" for="status-select"><?php echo translate_str_by_id(2081); ?></label>
            </div>
            <div>
                <select <?=((int)$status !== 0)?'style="background:#b9fcab;"':'';?> id="status-select" class="form-control">
                <option value="0"><?php echo translate_str_by_id(2094); ?></option>
                <?php
                foreach($orders_statuses as $status_id=>$status_data)
                {
                    $selected = "";
                    if($status == $status_id)
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
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="filterOrders();"><?php echo translate_str_by_id(2232); ?></button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="unsetFilterOrders();"><?php echo translate_str_by_id(2555); ?></button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="location='<?php echo $multilang_params['lang_href']; ?>/shop/orders/items';"><?php echo translate_str_by_id(4556); ?></button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="hidden-xs hidden-sm btn btn-ar btn-primary" onclick="show_orders_items();"><i style="margin-right: 0px;" class="fa fa-level-down"></i></button>
    </div>
	
	<style>
	@media screen and (min-width: 768px) {
		.box_btn_filter .btn{
			display:inline-block;
		}
		.box_btn_filter .btn[onclick="location='<?php echo $multilang_params['lang_href']; ?>/shop/orders/items';"]{
			float:right;
		}
		.box_btn_filter .btn[onclick="show_orders_items();"]{
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
	function show_orders_items(){
		if($('.orders-items-tr').css('display') == 'table-row'){
			$('.orders-items-tr').css('display', 'none');
		}else{
			$('.orders-items-tr').css('display', 'table-row');
		}
	}
    // ------------------------------------------------------------------------------------------------
    //Устновка cookie в соответствии с фильтром
    function filterOrders()
    {
        var my_orders_filter = new Object;
        
        //1. Время с
        my_orders_filter.time_from = encodeURIComponent(document.getElementById("time_from").value);
        //2. Время по
        my_orders_filter.time_to = encodeURIComponent(document.getElementById("time_to").value);
        
        //3. Номер заказа
        my_orders_filter.order_id = encodeURIComponent(document.getElementById("order_id").value);
        
        //4. Статус заказа
        my_orders_filter.status = encodeURIComponent(document.getElementById("status-select").value);
        
        //5. Оплачен
        my_orders_filter.paid = encodeURIComponent(document.getElementById("paid").value);
		
		//6. Способ оплаты
        my_orders_filter.paid_type = encodeURIComponent(document.getElementById("paid_type").value);
		
		//7. Гараж
        my_orders_filter.garage = encodeURIComponent(document.getElementById("garage").value);
		
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "my_orders_filter="+JSON.stringify(my_orders_filter)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='<?php echo $multilang_params['lang_href']; ?>/shop/orders';
    }
    // ------------------------------------------------------------------------------------------------
    //Снять все фильтры
    function unsetFilterOrders()
    {
        var my_orders_filter = new Object;
        
        //1. Время с
        my_orders_filter.time_from = "";
        //2. Время по
        my_orders_filter.time_to = "";
        
        //3. Номер заказа
        my_orders_filter.order_id = "";
        
        //4. Статус заказа
        my_orders_filter.status = 0;
        
        //5. Оплачен
        my_orders_filter.paid = -1;
        
		//6. Способ оплаты
        my_orders_filter.paid_type = -1;
		
		//7. Гараж
        my_orders_filter.garage = 0;
		
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "my_orders_filter="+JSON.stringify(my_orders_filter)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='<?php echo $multilang_params['lang_href']; ?>/shop/orders';
    }
    // ------------------------------------------------------------------------------------------------
    </script>
    
    
    
    
    
    
    
    
    
    
    <script>
    // ------------------------------------------------------------------------------------------------
    //Установка куки сортировки заказов
    function sortOrders(field)
    {
        var asc_desc = "asc";//Направление по умолчанию
        
        //Берем из куки текущий вариант сортировки
        var current_sort_cookie = getCookie("my_orders_sort");
        if(current_sort_cookie != undefined)
        {
            current_sort_cookie = JSON.parse(getCookie("my_orders_sort"));
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
        
        
        var my_orders_sort = new Object;
        my_orders_sort.field = field;//Поле, по которому сортировать
        my_orders_sort.asc_desc = asc_desc;//Направление сортировки
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "my_orders_sort="+JSON.stringify(my_orders_sort)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='<?php echo $multilang_params['lang_href']; ?>/shop/orders';
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
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('id');" id="id_sorter">ID</a></th>
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('price_sum');" id="price_sum_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('paid');" id="paid_sorter"><?php echo translate_str_by_id(4557); ?></a></th>
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('paid_type');" id="paid_type_sorter"><?php echo translate_str_by_id(4645); ?></a></th>
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('status');" id="status_sorter"><?php echo translate_str_by_id(2081); ?></a></th>
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('obtain_caption');" id="obtain_caption_sorter"><?php echo translate_str_by_id(3507); ?></a></th>
			<th style="vertical-align: middle; white-space: nowrap;"><a href="javascript:void(0);" onclick="sortOrders('office_id');" id="office_id_sorter"><?php echo translate_str_by_id(3506); ?></a></th>
			<th style="vertical-align: middle; white-space: nowrap;"></th>
		</tr>
    
            <script>
                <?php
                //Определяем текущую сортировку и обозначаем ее:
                $my_orders_sort = NULL;
				if(isset($_COOKIE["my_orders_sort"])){
					$my_orders_sort = $_COOKIE["my_orders_sort"];
				}
                $sort_field = "id";
                $sort_asc_desc = "desc";
                if($my_orders_sort != NULL)
                {
                    $my_orders_sort = json_decode($my_orders_sort, true);
                    $sort_field = $my_orders_sort["field"];
                    $sort_asc_desc = $my_orders_sort["asc_desc"];
                }
				
				//Защита от SQL-инъекций
				if( $sort_asc_desc == "asc" )
				{
					$sort_asc_desc = "asc";
				}
				else
				{
					$sort_asc_desc = "desc";
				}
				
				if( array_search($sort_field, array('id','time','price_sum','paid','paid_type','status','obtain_caption','office_id','garage') ) === false )
				{
					$sort_field = "id";
				}
				
                ?>
                document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px; vertical-align: initial;\" />";
            </script>
 
        <?php
        
		$binding_values = array();
		
        //Подстрока с условиями фильтрования заказов
        $WHERE_CONDITIONS = ' WHERE `user_id` = ?';
        
        //Подстрока с условиями фильтрования статусов позиций, которые не участвуют в ценовых расчетах
		$WHERE_statuses_not_count = '';
		for($i=0; $i<count($orders_items_statuses_not_count); $i++)
		{
			$WHERE_statuses_not_count .= ' AND `status` != ?';
			array_push($binding_values, $orders_items_statuses_not_count[$i]);
		}
		
		array_push($binding_values, $user_id);//От WHERE_CONDITIONS
        
        //По куки фильтра:
		$my_orders_filter = NULL;
		if( isset($_COOKIE["my_orders_filter"]) )
		{
			$my_orders_filter = $_COOKIE["my_orders_filter"];
		}
        if($my_orders_filter != NULL)
        {
            $my_orders_filter = json_decode($my_orders_filter, true);
            
            //1. Время с
            if($my_orders_filter["time_from"] != "")
            {
                $WHERE_CONDITIONS .= ' AND `time` > ?';
				
				array_push($binding_values, (int) $my_orders_filter["time_from"]);
            }
            
            //2. Время по
            if($my_orders_filter["time_to"] != "")
            {
                $WHERE_CONDITIONS .= ' AND `time` < ?';
				
				array_push($binding_values, (int) $my_orders_filter["time_to"]);
            }
            
            //3. Номер заказа
            if($my_orders_filter["order_id"] != "")
            {
                $WHERE_CONDITIONS .= ' AND `id` = ?';
				
				array_push($binding_values, (int) $my_orders_filter["order_id"]);
            }
            
            //4. Номер заказа
            if($my_orders_filter["status"] != 0 )
            {
                $WHERE_CONDITIONS .= ' AND `status` = ?';
				
				array_push($binding_values, (int) $my_orders_filter["status"]);
            }
            
            //5. Оплата
            if($my_orders_filter["paid"] != -1 )
            {
                $WHERE_CONDITIONS .= ' AND `paid` = ?';
				
				array_push($binding_values, (int) $my_orders_filter["paid"]);
            }
			
			//6. Способ оплаты
            if($my_orders_filter["paid_type"] != -1 )
            {
                $WHERE_CONDITIONS .= ' AND `paid_type` = ?';
				
				array_push($binding_values, (int) $my_orders_filter["paid_type"]);
            }
			
			//7. Гараж
            if($my_orders_filter["garage"] != 0 )
            {
				if($my_orders_filter["garage"] > 0){
					$WHERE_CONDITIONS .= ' AND `id` IN(SELECT `order_id` FROM `shop_docpart_garage_orders` WHERE `garage_id` = ?)';
					array_push($binding_values, (int) $my_orders_filter["garage"]);
				}else{
					$WHERE_CONDITIONS .= ' AND `id` NOT IN(SELECT `order_id` FROM `shop_docpart_garage_orders` WHERE `garage_id` IN(SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ?))';
					array_push($binding_values, (int) $user_id);
				}
                
            }
			
        }
		
		//Индикатор непрочитанных сообщений в заказах
		if(isset($_GET["read"]) && (int)$_GET["read"] === 0)
		{
			$WHERE_CONDITIONS .= " AND `id` IN(SELECT DISTINCT `order_id` FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `order_id` > 0) ";
		}
		
		// Текущая страница
		$page = 1;
		if( isset( $_GET['page'] ) )
		{
			$page = (int) $_GET['page'];
		}
		if(empty($page))
		{
			$page = 1;
		}
		$lim_rows = $DP_Config->list_page_limit;// Количество строк на страницу
		$from_rows = ($page * $lim_rows) - $lim_rows;// С какой записи выводить
		
        
        $SQL_SELECT_ORDERS = 'SELECT SQL_CALC_FOUND_ROWS *, `shop_orders`.`id` AS `id`, ';
        $SQL_SELECT_ORDERS .= '`shop_orders`.`time` AS `time`, ';
		$SQL_SELECT_ORDERS .= '(SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get` ) AS `obtain_caption`, ';
        $SQL_SELECT_ORDERS .= '`shop_orders`.`paid` AS `paid`, ';
        $SQL_SELECT_ORDERS .= '`shop_orders`.`status` AS `status`, ';
		$SQL_SELECT_ORDERS .= " (SELECT COUNT(*) FROM `shop_orders_messages` WHERE `order_id` = `shop_orders`.`id` AND `read` = 0 AND `is_customer` = 0) AS `count_not_viewed_msg`, ";//Индикатор непрочитанных сообщений в заказах
		$SQL_SELECT_ORDERS .= " (SELECT GROUP_CONCAT(`garage_id`) FROM `shop_docpart_garage_orders` WHERE `order_id` = `shop_orders`.`id`) AS `garage`, ";//Гараж
        $SQL_SELECT_ORDERS .= ' CAST((SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` '.$WHERE_statuses_not_count.' ) AS DECIMAL(8,2)) AS `price_sum` ';//Сумма заказа
        $SQL_SELECT_ORDERS .= ' FROM `shop_orders` '.$WHERE_CONDITIONS.' ORDER BY `'.$sort_field.'` '.$sort_asc_desc.' LIMIT '.$from_rows.', '.$lim_rows;
        
		
		$elements_query = $db_link->prepare($SQL_SELECT_ORDERS);
		$elements_query->execute($binding_values);
		
		$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
		$elements_count_rows_query->execute();
		$all_rows = $elements_count_rows_query->fetchColumn();// Всего записей в базе данных
        
		
        //Массивы для JS с id элементов и с чекбоксами элементов
        $for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
        $for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
        
		$items_counter = 0;
		
		//Массив заказов
		$orders = array();
		$orders_id_list = array();
        while($record = $elements_query->fetch())
        {
			$orders[] = $record;
			$orders_id_list[] = $record["id"];
		}
		
		//Формируем список позиций заказов
		$orders_items = array();
		$orders_items_query = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `order_id` IN(".implode(',', $orders_id_list).");");
		$orders_items_query->execute();
		while($record = $orders_items_query->fetch())
        {
			$orders_items[$record['order_id']][] = $record;
		}
		
		//Отображаем заказы
		foreach($orders as $element_record)
        {
			$items_counter++;
			
    	    //Для Javascript
            $for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
            $for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
            
            $order_id = $element_record["id"];
            $time = $element_record["time"];
            $price_sum = number_format($element_record["price_sum"], 2, '.', ' ');
            $paid = $element_record["paid"];
			$paid_type = $element_record["paid_type"];
			$status = $element_record["status"];
            $obtain_caption = $element_record["obtain_caption"];
            $office_id = $element_record["office_id"];
            
			$order_garage = '';
			$order_garage_list = array();
			if(isset($element_record["garage"]) && !empty($element_record["garage"])){
				$order_garage_list = explode(',', $element_record["garage"]);
				if(!empty($order_garage_list)){
					foreach($order_garage_list as $item_garage_id){
						if(isset($garage_list[$item_garage_id])){
							if($order_garage != ''){
								$order_garage .= '; ';
							}
							$order_garage .= $garage_list[$item_garage_id];
						}
					}
				}
			}
			
			
            ?>
			<tr style="background-color:<?php echo $orders_statuses[$status]["color"]; ?>">
                <td class="hidden" style="line-height: 1em; vertical-align: middle;"><input style="margin-top: 0px;" type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"];?>');" id="checked_<?php echo $element_record["id"];?>" name="checked_<?php echo $element_record["id"];?>"/></td>
                <td style="line-height: 1em; white-space: nowrap; vertical-align: middle;">
				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/orders/order?order_id=<?php echo $order_id; ?>"><i class="fa fa-sign-in" aria-hidden="true"></i> <?php echo $order_id; ?> </a>
				<?php
				//Индикатор непрочитанных сообщений в заказах
				if($element_record["count_not_viewed_msg"] > 0){
				?>
				<small>
					<a style="white-space: nowrap;" class="dropdown-toggle label-menu-corner" href="<?php echo $multilang_params['lang_href']; ?>/shop/orders/order?order_id=<?=$order_id;?>">
						<i class="fa fa-envelope" aria-hidden="true"></i>
						<span style="font-size: 9px;"><?=$element_record["count_not_viewed_msg"];?></span>
					</a>
				</small>
				<?php
				}
				?>
				</td>
                <td style="line-height: 1em; vertical-align: middle;"><?php echo date("d.m.Y", $time)."<br><small>".date("G:i", $time)."</small>"; ?></td>
                <td style="line-height: 1em; white-space: nowrap; vertical-align: middle;"><?php echo $price_sum; ?></td>
                <td style="line-height: 1em; white-space: nowrap; vertical-align: middle;">
					<?php
					if($paid == 1)
					{
						echo translate_str_by_id(3514);
					}
					else if( $paid == 2 )
					{
						echo translate_str_by_id(3515);
					}
					else if( $paid == 0 )
					{
						echo translate_str_by_id(3513);
					}
					?>
				</td>
				<td style="line-height: 1em; white-space: nowrap; vertical-align: middle;"><?=(!empty($shop_orders_paid_type[$paid_type]))?translate_str_by_id($shop_orders_paid_type[$paid_type]):'';?></td>
                <td style="line-height: 1em; vertical-align: middle;"><?php echo translate_str_by_id($orders_statuses[$status]["name"]); ?></td>
                <td style="line-height: 1em; vertical-align: middle;"><?php echo translate_str_by_id($obtain_caption); ?></td>
                <td style="line-height: 1em; vertical-align: middle;"><?php echo translate_str_by_id($offices_list[$office_id]["caption"]); ?></td>
                <td style="line-height: 1em; vertical-align: middle;"><?php echo ($order_garage != '')?'<i style="margin-right: 0px; color: #999;" class="fa fa-car" title="'.$order_garage.'"></i>':''; ?></td>
            </tr>
			
			<?php
			//Отображаем позиции заказа
			if(!empty($orders_items[$element_record["id"]])){
				?>
				<tr class="orders-items-tr hidden-xs hidden-sm" style="display:none;">
					<td></td>
					<td></td>
					<td colspan="7" style="padding:0;">
						<table class="table" style="margin: 0; font-size: 12px;">
						<?php
						foreach($orders_items[$element_record["id"]] as $order_item){
						?>
							<tr style="background:<?php echo $orders_items_statuses[$order_item['status']]["color"]; ?>">
								<td style="padding: 5px; line-height: 1em; vertical-align: middle; width:20%;"><?php echo $order_item['t2_manufacturer']; ?></td>
								<td style="padding: 5px; line-height: 1em; vertical-align: middle; width:20%;"><?php echo $order_item['t2_article']; ?></td>
								<td style="padding: 5px; line-height: 1em; vertical-align: middle; width:40%;"><?php echo $order_item['t2_name']; ?></td>
								<td style="padding: 5px; line-height: 1em; vertical-align: middle;"><?php echo $orders_items_statuses[$order_item['status']]["name"]; ?></td>
							</tr>
						<?php
						}
						?>
						</table>
					</td>
				</tr>
				<?php
			}
			?>
			
        <?php
        }//while
		
		if($items_counter == 0){
			echo '<tr><td colspan="9">'.translate_str_by_id(4558).'</td></tr>';
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
}//if($user_id > 0)
else//Если покупатель не авторизован
{
    ?>
    <p><?php echo translate_str_by_id(4559); ?></p>
    
	
	<div class="panel panel-primary">
	<?php
	//Единый механизм формы авторизации
	$login_form_postfix = "my_orders";
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