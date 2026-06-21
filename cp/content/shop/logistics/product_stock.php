<?php
/**
 * Скрипт для управления наличием продукта по складам
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( ! empty($_POST["save_action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
    $product_id = $_POST["product_id"];
    $category_id = $_POST["category_id"];
    $storages = json_decode($_POST["storages"], true);//Ключ - id скалада в системе
    
    $error_array = array();//Список ошибок
    $no_error = true;//Флаг - выполнено без ошибок
    
    foreach($storages as $storage_id => $data_object)
    {
        if($storage_id == NULL) continue;
        
        //Для обработок возможных ошибок
        $error_array[$storage_id] = array("errors" => array());
        
		
        //1. Создание/Обновление записей
		$records = $data_object["records"];//Записи товаров
		for($i=0; $i < count($records); $i++)
		{
			$record = $records[$i];
			
			$record_id = $record["id"];
			$price = $record["price"];
			$price_purchase = $record["price_purchase"];
			$price_crossed_out = $record["price_crossed_out"];
			$exist = $record["exist"];
			$reserved = $record["reserved"];
			$issued = $record["issued"];
			$arrival_time = $record["arrival_time"];
			$time_to_exe = $record["time_to_exe"];
			

			if($record_id != 0)//Запись существующая - обновляем
			{
				$SQL_SAVE = "UPDATE `shop_storages_data` SET `price` = ?, `price_purchase` = ?, `price_crossed_out` = ?, `exist` = ?, `reserved` = ?, `issued` = ?, `arrival_time` = ?, `time_to_exe` = ? WHERE `id` = ?;";
				
				$binding_values = array($price, $price_purchase, $price_crossed_out, $exist, $reserved, $issued, $arrival_time, $time_to_exe, $record_id);
			}
			else//Запись новая - создаем
			{
				$SQL_SAVE = "INSERT INTO `shop_storages_data` (`storage_id`, `product_id`, `category_id`, `price`, `price_purchase`, `price_crossed_out`, `exist`, `reserved`, `issued`, `arrival_time`, `time_to_exe`) VALUES (?,?,?,?,?,?,?,?,?,?,?);";
				
				$binding_values = array($storage_id, $product_id, $category_id, $price, $price_purchase, $price_crossed_out, $exist, $reserved, $issued, $arrival_time, $time_to_exe);
			}
			

			//Запрос в БД UPDATE/INSERT
			if( $db_link->prepare($SQL_SAVE)->execute($binding_values) != true)
			{
				//Обработать ошибку
				array_push($error_array[$storage_id]["errors"], translate_str_by_id(3414));
				$no_error = false;
			}
		}//for($i)
		
		
		//2. Удаляем записи, которые были удалены на стороне клиента:
		$records_deleted = $data_object["records_deleted"];//ID удаленных клиентом записей
		if( count($records_deleted) > 0)
		{
			$binding_values = array();
			$SQL_DELETE = "DELETE FROM `shop_storages_data` WHERE";
			for($i=0; $i < count($records_deleted); $i++)
			{
				if($i>0) $SQL_DELETE .= " OR";
				$SQL_DELETE .= " `id` = ?";
				
				array_push($binding_values, $records_deleted[$i]);
			}
			$SQL_DELETE .=";";
			//Запрос на удаление
			if( $db_link->prepare($SQL_DELETE)->execute( $binding_values ) != true)
			{
				//Обработать ошибку
				array_push($error_array[$storage_id]["errors"], translate_str_by_id(3415));
				$no_error = false;
			}
		}
    }
    
    //ОБРАБОТКА РЕЗУЛЬТАТА:
    if($no_error)
    {
        $success_message = translate_str_by_id(2157);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/stock/product?product_id=<?php echo $product_id; ?>&success_message=<?php echo $success_message; ?>";
        </script>
        <?php
        exit;
    }
    else
    {
        $error_message = translate_str_by_id(2912).": <br>";
        foreach($error_array as $storage_id => $errors)
        {
            for($t=0; $t < count($errors); $t++)
            {
                $error_message .= translate_str_by_id(3398)." ID $storage_id: ".$errors[$t]."<br>";
            }
        }
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/stock/product?product_id=<?php echo $product_id; ?>&success_message=<?php echo $success_message; ?>";
        </script>
        <?php
        exit;
    }
    
}//if( ! empty($_POST["save_action"]) )
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    $product_id = $_GET["product_id"];
    
	$category_id_query = $db_link->prepare("SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?;");
	$category_id_query->execute( array($product_id) );
    $category_id_record = $category_id_query->fetch();
    $category_id = $category_id_record["category_id"];
    ?>
    
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/products/product?category_id=<?php echo $category_id; ?>&product_id=<?php echo $product_id; ?>" target="_blank">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/cargo.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3416); ?></div>
				</a>
				
				


				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
    

    
    
    
    
    
    
    <script>
    // --------------------------------------------------------------------------------------
    var storages = new Array();//Массив с объектами наличия по складам
    var storages_ids = new Array();//Массив с id складов (вспомогательный, т.к. ключи массива storages являются id складов и могут быть не по порядку)
    // --------------------------------------------------------------------------------------
    //Добавить запись товара в склад
    function addRecord(storage_id)
    {
        var new_record = new Object;
        new_record.id=0;
        new_record.price=0;
        new_record.price_crossed_out=0;
        new_record.price_purchase=0;
        new_record.arrival_time= Date.now()/1000;//Время ставим текущее (формат UNIX)
        new_record.time_to_exe=0;
        new_record.exist=0;
        new_record.reserved=0;
        new_record.issued=0;
        storages[storage_id].records.push(new_record);
        
        refreshStorageTable(storage_id);//Перерисовываем таблицу записей склада
    }
    // --------------------------------------------------------------------------------------
    //Удаление отмеченных записей одного склада
    function deleteCheckedRecords(storage_id)
    {
        var checked_indexes = getCheckedElements(storage_id);
        
        if(checked_indexes.length == 0)
        {
            alert("<?php echo translate_str_by_id(3417); ?>");
        }
        
        
        //Удаляем записи из объекта описания (удаляем с конца массива, чтобы не сбивались индексы)
        for(var i = checked_indexes.length-1; i >=0 ; i--)
        {
            //Вносим ID записи в список удаленных, чтобы затем удалить ее на сервере
            if(storages[storage_id].records[i].id > 0)
            {
                storages[storage_id].records_deleted.push(storages[storage_id].records[checked_indexes[i]].id);
                console.log("<?php echo translate_str_by_id(3418); ?>: "+storages[storage_id].records[checked_indexes[i]].id);
            }
        
            storages[storage_id].records.splice(checked_indexes[i], 1);
        }
        
        refreshStorageTable(storage_id);//Перерисовываем таблицу
    }
    // --------------------------------------------------------------------------------------
    //Удаление одной записи
    function deleteRecord(storage_id, i)
    {
        //Вносим ID записи в список удаленных, чтобы затем удалить ее на сервере
        if(storages[storage_id].records[i].id > 0)
        {
            storages[storage_id].records_deleted.push(storages[storage_id].records[i].id);
            console.log("<?php echo translate_str_by_id(3418); ?>: "+storages[storage_id].records[i].id);
        }
    
        storages[storage_id].records.splice(i, 1);
        
        refreshStorageTable(storage_id);//Перерисовываем таблицу
    }
    // --------------------------------------------------------------------------------------
    //Перерисовка таблицы записей склада
    function refreshStorageTable(storage_id)
    {
        var tbody = document.getElementById("tbody_"+storage_id);
        var tbody_innerHTML = "";
        
        for(var i=0; i < storages[storage_id].records.length; i++)
        {
            var record_id = storages[storage_id].records[i].id;
            if(record_id == 0)record_id = "<?php echo translate_str_by_id(3419); ?>";

            tbody_innerHTML += "<tr>";
                tbody_innerHTML += "<td><input type=\"checkbox\" id=\"record_"+storage_id+"_"+i+"\" onchange=\"on_one_check_changed("+storage_id+");\"/></td>";
                tbody_innerHTML += "<td>"+record_id+"</td>";
	            tbody_innerHTML += "<td><input class=\"form-control\" type=\"text\" id=\"price_"+storage_id+"_"+i+"\" onKeyUp=\"dynamicApplying("+storage_id+","+i+", 'price');\" value=\""+storages[storage_id].records[i].price+"\" /></td>";
	            tbody_innerHTML += "<td><input class=\"form-control\" type=\"text\" id=\"price_crossed_out_"+storage_id+"_"+i+"\" onKeyUp=\"dynamicApplying("+storage_id+","+i+", 'price_crossed_out');\" value=\""+storages[storage_id].records[i].price_crossed_out+"\" /></td>";
	            tbody_innerHTML += "<td><input class=\"form-control\" type=\"text\" id=\"price_purchase_"+storage_id+"_"+i+"\" onKeyUp=\"dynamicApplying("+storage_id+","+i+", 'price_purchase');\" value=\""+storages[storage_id].records[i].price_purchase+"\" /></td>";
	            tbody_innerHTML += "<td><input class=\"form-control\" type=\"text\" id=\"exist_"+storage_id+"_"+i+"\" onKeyUp=\"dynamicApplying("+storage_id+","+i+", 'exist');\" value=\""+storages[storage_id].records[i].exist+"\" /></td>";
	            tbody_innerHTML += "<td><input class=\"form-control\" type=\"text\" id=\"reserved_"+storage_id+"_"+i+"\" onKeyUp=\"dynamicApplying("+storage_id+","+i+", 'reserved');\" value=\""+storages[storage_id].records[i].reserved+"\" /></td>";
	            tbody_innerHTML += "<td><input class=\"form-control\" type=\"text\" id=\"issued_"+storage_id+"_"+i+"\" onKeyUp=\"dynamicApplying("+storage_id+","+i+", 'issued');\" value=\""+storages[storage_id].records[i].issued+"\" /></td>";
	            tbody_innerHTML += "<td><div style=\"position:relative;width:160px;\">";
	                //Для хранения метки времени
	                tbody_innerHTML += "<input class=\"form-control\" style=\"position:absolute; z-index:2; opacity:0\" type=\"text\" i=\""+i+"\" storage_id=\""+storage_id+"\" id=\"arrival_time_"+storage_id+"_"+i+"\" value=\""+storages[storage_id].records[i].arrival_time+"\" />";
	                //Для отображения даты поставки
	                tbody_innerHTML += "<input class=\"form-control\" style=\"position:absolute; z-index:1;\" type=\"text\" id=\"arrival_time_show_"+storage_id+"_"+i+"\" />";
                tbody_innerHTML += "</div></td>";
				tbody_innerHTML += "<td><input class=\"form-control\" type=\"text\" id=\"time_to_exe_"+storage_id+"_"+i+"\" onKeyUp=\"dynamicApplying("+storage_id+","+i+", 'time_to_exe');\" value=\""+storages[storage_id].records[i].time_to_exe+"\" /></td>";
                
                //Колонка "Удалить"
                tbody_innerHTML += "<td><a href=\"javascript:void(0);\" onclick=\"deleteRecord("+storage_id+", "+i+");\">";
                    tbody_innerHTML += "<img src=\"/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png\" class=\"col_img_popup\">";
                tbody_innerHTML += "</a></td>";
            tbody_innerHTML += "</tr>";
        }
        
        tbody.innerHTML = tbody_innerHTML;
        
        datetimePickersInit(storage_id);//Теперь инициализируем датапикеры
    }
    // --------------------------------------------------------------------------------------
    //Инициализация datetimePickers для записей одного склада
    function datetimePickersInit(storage_id)
    {
        for(var i=0; i < storages[storage_id].records.length; i++)
        {
            //Инициализируем datetimepicker
            jQuery("#arrival_time_"+storage_id+"_"+i).datetimepicker({
                lang:"<?php echo $multilang_params['lang']; ?>",
                closeOnDateSelect:true,
                closeOnTimeSelect:false,
                dayOfWeekStart:1,
                format:'unixtime',
                onClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
                {
                    showDatetime(current_time, input);
                },
                onGenerate:function(current_time, input)//При создании datetimepicker - отображаем в поле индикации
                {
                    showDatetime(current_time, input);
                },
            });
        }
    }
    // --------------------------------------------------------------------------------------
    //Метод отображения времени в понятном виде - в специальном поле (storage_id - склад, i - индекс записи в объекте описания)
    function showDatetime(current_time, input)
    {
        var input_id = input[0].id;//Получаем id того input, к которому привязан этот датапикер
        var input = document.getElementById(input_id);//DOM инпута, из которого теперь можно получить storage_id и индекс записи продукта
        
        var storage_id = input.getAttribute("storage_id");
        var i = input.getAttribute("i");
        
        var timestamp = parseInt(document.getElementById("arrival_time_"+storage_id+"_"+i).value)*1000;
        
        var time_string = "";
        
        var date_ob = new Date(timestamp);
        
        time_string += date_ob.getDate()+".";
        time_string += (date_ob.getMonth() + 1)+".";
        time_string += date_ob.getFullYear()+" ";
        
        time_string += date_ob.getHours()+":"+date_ob.getMinutes();
        
        document.getElementById("arrival_time_show_"+storage_id+"_"+i).value = time_string;//Показываем время в понятном виде
        storages[storage_id].records[i]["arrival_time"] = timestamp/1000;//Указываем метку UNIX в объект описания
    }
    // --------------------------------------------------------------------------------------
	//Функция динамическиго применния значений
	function dynamicApplying(storage_id, i, parameter)
	{
	    var value = document.getElementById(parameter+"_"+storage_id+"_"+i).value;
	
	    storages[storage_id].records[i][parameter] = value;
	} 
    // --------------------------------------------------------------------------------------
    </script>
    <?php
    //Получаем склады, с которыми может работать этот пользователь
    $user_id = DP_User::getAdminId();
    $storages_list = array();//Список с id складов (В этом списке ключ - id склада, а значение массив с id записей)
    
    //Получаем список складов с типом продукта 1:
    $SQL_SELECT_STORAGES = "SELECT
        `shop_storages`.`id` AS `id`,
        `shop_storages`.`name` AS `name`,
        `shop_storages`.`connection_options` AS `connection_options`,
        `shop_storages`.`interface_type` AS `interface_type`
        FROM
        `shop_storages`
        INNER JOIN `shop_storages_interfaces_types` ON `shop_storages`.`interface_type` = `shop_storages_interfaces_types`.`id`
        WHERE
        `shop_storages_interfaces_types`.`product_type` = 1 AND
        `shop_storages`.`users` LIKE ?;";
    
	$storages_query = $db_link->prepare($SQL_SELECT_STORAGES);
	$storages_query->execute( array('%'.$user_id.'%') );
    //По каждому складу
    while($storage = $storages_query->fetch() )
    {
        $storage_id = $storage["id"];
        $storage_name = $storage["name"];
        $interface_type = $storage["interface_type"];
        $connection_options = json_decode($storage["connection_options"], true);
        ?>
        <script>
            //Заносим склад в список javascript
            storages[<?php echo $storage_id; ?>] = new Object;
            storages[<?php echo $storage_id; ?>].records = new Array();//Массив для записей продукта с данного склада
            storages[<?php echo $storage_id; ?>].records_deleted = new Array();//Список записей, которые в процесс работы кладовщика будут удалены. (Чтобы сервер знал, что удалять)
            
            //Заносим id склада во вспомогательный массив
            storages_ids.push(<?php echo $storage_id; ?>);
        </script>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php
					if( ! isset($connection_status) )
					{
						$connection_status = null;
					}
					?>
					<?php echo translate_str_by_id(3420); ?> <strong><?php echo $storage_name." (".$storage_id.")".$connection_status; ?></strong>
				</div>
				<div class="panel-body">
					<div class="table-responsive">
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead> 
								<tr>
									<th><input type="checkbox" id="check_uncheck_all_<?php echo $storage_id; ?>" onchange="on_check_uncheck_all(<?php echo $storage_id; ?>);"/></th>
									<th>ID</th>
									<th><?php echo translate_str_by_id(3421); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3422); ?>');"><i class="fa fa-info"></i></button></th>
									<th><?php echo translate_str_by_id(3423); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3424); ?>');"><i class="fa fa-info"></i></button></th>
									<th><?php echo translate_str_by_id(3425); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3426); ?>');"><i class="fa fa-info"></i></button></th>
									<th><?php echo translate_str_by_id(1717); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3427); ?>');"><i class="fa fa-info"></i></button></th>
									<th><?php echo translate_str_by_id(2753); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3428); ?>');"><i class="fa fa-info"></i></button></th>
									<th><?php echo translate_str_by_id(3429); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3430); ?>');"><i class="fa fa-info"></i></button></th>
									<th><?php echo translate_str_by_id(3431); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3432); ?>');"><i class="fa fa-info"></i></button></th>
									<th><?php echo translate_str_by_id(3433); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3434); ?>');"><i class="fa fa-info"></i></button></th>
									<th><!--Удаление--></th>
								</tr>
							</thead>
							<tbody id="tbody_<?php echo $storage_id; ?>">
							</tbody>
						</table>
					</div>
				</div>
				<div class="panel-footer">
					
					<button onclick="addRecord(<?php echo $storage_id; ?>);" class="btn btn-success " type="button"><i class="fa fa-plus"></i> <span class="bold"><?php echo translate_str_by_id(3435); ?></span></button>
					
					<button onclick="deleteCheckedRecords(<?php echo $storage_id; ?>);" class="btn btn-danger " type="button"><i class="fa fa-trash-o"></i> <span class="bold"><?php echo translate_str_by_id(3436); ?></span></button>
					
				</div>
			</div>
		</div>
        <?php
        //Запрашиваем текущие данные со склада
        ?>
		<script>
			storages[<?php echo $storage_id; ?>].connection_status = true;//ПОДКЛЮЧЕНИЕ УСТАНОВЛЕНО
		</script>
		<?php
		$storage_data_query = $db_link->prepare("SELECT * FROM `shop_storages_data` WHERE `product_id` = ? AND storage_id = ?;");
		$storage_data_query->execute( array($product_id, $storage_id) );
		while( $storage_product_record = $storage_data_query->fetch() )
		{
			$record_id = $storage_product_record["id"];
			$price = $storage_product_record["price"];
			$price_crossed_out = $storage_product_record["price_crossed_out"];
			$price_purchase = $storage_product_record["price_purchase"];
			$arrival_time = $storage_product_record["arrival_time"];
			$time_to_exe = $storage_product_record["time_to_exe"];
			$exist = $storage_product_record["exist"];
			$reserved = $storage_product_record["reserved"];
			$issued = $storage_product_record["issued"];
			
			?>
			<script>
				//Добавляем объект записи в наш список
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length] = new Object;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].id = <?php echo $record_id; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].price = <?php echo $price; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].price_crossed_out = <?php echo $price_crossed_out; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].price_purchase = <?php echo $price_purchase; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].arrival_time = <?php echo $arrival_time; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].time_to_exe = <?php echo $time_to_exe; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].exist = <?php echo $exist; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].reserved = <?php echo $reserved; ?>;
				storages[<?php echo $storage_id; ?>].records[storages[<?php echo $storage_id; ?>].records.length-1].issued = <?php echo $issued; ?>;
			</script>
			<?php
		}
    }//~while($storage) - по кадому складу
    ?>
    
    
    
    
    <script>
    //ОБРАБОТКА ЧЕКБОКСОВ ДЛЯ ЗАПИСЕЙ
    // ----------------------------------------------------------------------
    //Обработка переключения Выделить все/Снять все
    function on_check_uncheck_all(storage_id)
    {
        var state = document.getElementById("check_uncheck_all_"+storage_id).checked;
        
        for(var i=0; i < storages[storage_id].records.length; i++)
        {
            document.getElementById("record_"+storage_id+"_"+i).checked = state;
        }
    }//~function on_check_uncheck_all()
    // ----------------------------------------------------------------------
    //Обработка переключения одного чекбокса
    function on_one_check_changed(storage_id)
    {
        //Если хотя бы один чекбокс снят - снимаем общий чекбокс
        for(var i=0; i < storages[storage_id].records.length; i++)
        {
            if(document.getElementById("record_"+storage_id+"_"+i).checked == false)
            {
                document.getElementById("check_uncheck_all_"+storage_id).checked = false;
                break;
            }
        }
    }//~function on_one_check_changed(id)
    // ----------------------------------------------------------------------
    //Получение массива индексов отмеченых записей одного склада
    function getCheckedElements(storage_id)
    {
        var checked_ids = new Array();
        //По массиву чекбоксов
        for(var i=0; i < storages[storage_id].records.length; i++)
        {
            if(document.getElementById("record_"+storage_id+"_"+i).checked == true)
            {
                checked_ids.push(i);
            }
        }
        
        return checked_ids;
    }
    // ----------------------------------------------------------------------
    </script>
    
    
    
    
    
    <!-- Start Форма сохранения -->
    <form method="POST" name="save_form">
        <input type="hidden" name="save_action" value="save_action" />
        <input type="hidden" name="storages" id="storages" value="" />
        <input type="hidden" name="product_id" value="<?php echo $product_id; ?>" />
        <input type="hidden" name="category_id" value="<?php echo $category_id; ?>" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <script>
    //Сохранение изменений
    function save_action()
    {
        document.getElementById("storages").value = JSON.stringify(storages);
        
        document.forms["save_form"].submit();
    }
    </script>
    <!-- End Форма сохранения -->
    
    
    
    
    
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2069); ?>
			</div>
			<div class="panel-body">
				<?php
				//Выводим страницу товара:
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printProduct_Info.php");
				?>
			</div>
		</div>
	</div>
    

    
    
    
    <script>
    //После загрузки страницы - инициализируем таблицы наличия для всех складов
	for(var i=0; i < storages_ids.length; i++)
    {
        refreshStorageTable(storages_ids[i])
    }
    </script>
    <?php
}
?>