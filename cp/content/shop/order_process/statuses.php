<?php
/**
 * Страничный скрипт для управления статусами заказов
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if(isset($_POST["save_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    $no_error = true;//Флаг ошибки
    
    //1. Сохраняем статусы заказов
    $orders_statuses_list = json_decode($_POST["orders_statuses"], true);
	$orders_statuses_ids_exist_str = "";//Подстрока для SQL (те статусы, которые не удалять)
    for($s=0; $s < count($orders_statuses_list); $s++)
    {
        $id = (int)$orders_statuses_list[$s]["id"];
        $name = htmlentities($orders_statuses_list[$s]["value"]);
		$value_lang_str_id = $orders_statuses_list[$s]["value_lang_str_id"];//Мультиязычность
        $color = htmlentities($orders_statuses_list[$s]["color"]);
        $for_created = (int)$orders_statuses_list[$s]["for_created"];
        $for_paid = (int)$orders_statuses_list[$s]["for_paid"];
        $for_finish = (int)$orders_statuses_list[$s]["for_finish"];
        $for_inverse = (int)$orders_statuses_list[$s]["for_inverse"];
		$to_manager_email = (int)$orders_statuses_list[$s]["to_manager_email"];
		$to_manager_sms = (int)$orders_statuses_list[$s]["to_manager_sms"];
		$to_customer_email = (int)$orders_statuses_list[$s]["to_customer_email"];
		$to_customer_sms = (int)$orders_statuses_list[$s]["to_customer_sms"];
		
		
		//Обработка мультиязычности
		//Вызов функции сохранения строки в виде перевода на текущий язык панели управления (кастомный алгоритм). В ответ вернется ID этой строки, который и нужно будет сохранить
		$name = save_custom_translation($value_lang_str_id, $name);
		
        
		if($orders_statuses_ids_exist_str != "") $orders_statuses_ids_exist_str .= ",";
		$orders_statuses_ids_exist_str .= (int)$id;
		
        //Статус был создан ранее - редактируем запись
        if($orders_statuses_list[$s]["created_earlier"] == 1)
        {
            if( $db_link->prepare("UPDATE `shop_orders_statuses_ref` SET `name`=?, `color`=?, `for_created`=?, `for_paid`=?, `for_finish`=?, `for_inverse`=?,`order`=?, `to_manager_email` = ?, `to_manager_sms` = ?, `to_customer_email` = ?, `to_customer_sms` = ? WHERE `id` = ?")->execute( array($name, $color, $for_created, $for_paid, $for_finish, $for_inverse, $s, $to_manager_email, $to_manager_sms, $to_customer_email, $to_customer_sms, $id) ) != true)
            {
                $no_error = false;
            }
        }
        else//Статус - новый - создаем запись
        {
            if( $db_link->prepare("INSERT INTO `shop_orders_statuses_ref` (`name`,`color`,`for_created`, `for_paid`, `for_finish`, `for_inverse`, `order`, `to_manager_email`, `to_manager_sms`, `to_customer_email`, `to_customer_sms`) VALUES (?,?,?,?,?,?,?,?,?,?,?);")->execute( array($name, $color, $for_created, $for_paid, $for_finish, $for_inverse, $s, $to_manager_email, $to_manager_sms, $to_customer_email, $to_customer_sms) ) != true)
            {
                $no_error = false;
            }
			else
			{
				//Добаляем в список неудаляемых
				if($orders_statuses_ids_exist_str != "") $orders_statuses_ids_exist_str .= ",";
				$orders_statuses_ids_exist_str .= $db_link->lastInsertId();
			}
        }
    }
	//Теперь удаляем статусы, которые были удалены при редактировании
	$orders_statuses_ids_exist_str = "(".$orders_statuses_ids_exist_str.")";
	if(! $db_link->prepare("DELETE FROM `shop_orders_statuses_ref` WHERE `id` NOT IN $orders_statuses_ids_exist_str;")->execute())
	{
		$no_error = false;
	}
    
    
    //2. Сохраняем статусы позиций заказов
    $orders_items_statuses_list = json_decode($_POST["orders_items_statuses"], true);
	$orders_items_statuses_ids_exist_str = "";//Подстрока для SQL (те статусы, которые не удалять)
    for($s=0; $s < count($orders_items_statuses_list); $s++)
    {
        $id = (int)$orders_items_statuses_list[$s]["id"];
        $name = htmlentities($orders_items_statuses_list[$s]["value"]);
		$value_lang_str_id = $orders_items_statuses_list[$s]["value_lang_str_id"];//Мультиязычность
        $color = htmlentities($orders_items_statuses_list[$s]["color"]);
        $for_created = (int)$orders_items_statuses_list[$s]["for_created"];
        $for_finish = (int)$orders_items_statuses_list[$s]["for_finish"];
        $count_flag = (int)$orders_items_statuses_list[$s]["count_flag"];
        $issue_flag = (int)$orders_items_statuses_list[$s]["issue_flag"];
		$to_manager_email = (int)$orders_items_statuses_list[$s]["to_manager_email"];
		$to_manager_sms = (int)$orders_items_statuses_list[$s]["to_manager_sms"];
		$to_customer_email = (int)$orders_items_statuses_list[$s]["to_customer_email"];
		$to_customer_sms = (int)$orders_items_statuses_list[$s]["to_customer_sms"];
		$for_return = (int)$orders_items_statuses_list[$s]["for_return"];
		$check_for_return = (int)$orders_items_statuses_list[$s]["check_for_return"];
		$complete_return = (int)$orders_items_statuses_list[$s]["complete_return"];
		$reject_return = (int)$orders_items_statuses_list[$s]["reject_return"];
		
		//Обработка мультиязычности
		//Вызов функции сохранения строки в виде перевода на текущий язык панели управления (кастомный алгоритм). В ответ вернется ID этой строки, который и нужно будет сохранить
		$name = save_custom_translation($value_lang_str_id, $name);
		
		if($orders_items_statuses_ids_exist_str != "") $orders_items_statuses_ids_exist_str .= ",";
		$orders_items_statuses_ids_exist_str .= (int)$id;
		
	    //Статус был создан ранее - редактируем запись
			if($orders_items_statuses_list[$s]["created_earlier"] == 1)
			{
					if( $db_link->prepare("UPDATE `shop_orders_items_statuses_ref` SET `name`=?, `color`=?, `for_created`=?, `for_finish`=?, `count_flag`=?,`issue_flag`=?,`order`=?, `to_manager_email` = ?, `to_manager_sms` = ?, `to_customer_email` = ?, `to_customer_sms` = ?, `for_return` = ?, `check_for_return` = ?, `complete_return` = ?, `reject_return` = ? WHERE `id` = ?;")->execute( array($name, $color, $for_created, $for_finish, $count_flag, $issue_flag, $s, $to_manager_email, $to_manager_sms, $to_customer_email, $to_customer_sms,$for_return, $check_for_return, $complete_return, $reject_return, $id) ) != true)
					{
							$no_error = false;
					}
			}
			else//Статус - новый - создаем запись
			{
					if( $db_link->prepare("INSERT INTO `shop_orders_items_statuses_ref` (`name`, `color`, `for_created`, `for_finish`, `count_flag`, `issue_flag`, `order`, `to_manager_email`, `to_manager_sms`, `to_customer_email`, `to_customer_sms`,`for_return`,`check_for_return`,`complete_return`, `reject_return`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);")->execute( array($name, $color, $for_created, $for_finish, $count_flag, $issue_flag, $s, $to_manager_email, $to_manager_sms, $to_customer_email, $to_customer_sms,$for_return, $check_for_return, $complete_return, $reject_return ) ) != true)
					{
							$no_error = false;
					}
					else
					{
						//Добаляем в список неудаляемых
						if($orders_items_statuses_ids_exist_str != "") $orders_items_statuses_ids_exist_str .= ",";
						$orders_items_statuses_ids_exist_str .= $db_link->lastInsertId();
					}
			}
    }
    //Теперь удаляем статусы, которые были удалены при редактировании
	$orders_items_statuses_ids_exist_str = "(".$orders_items_statuses_ids_exist_str.")";
	if(! $db_link->prepare("DELETE FROM `shop_orders_items_statuses_ref` WHERE `id` NOT IN $orders_items_statuses_ids_exist_str;")->execute())
	{
		$no_error = false;
	}
	
	
    
    
    //3. Обрабатываем результат
    if($no_error == true)
    {
        $success_message = translate_str_by_id(3641);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/orders/statuses?success_message=<?php echo $success_message; ?>";
        </script>
        <?php
        exit;
    }
    else
    {
        $error_message = translate_str_by_id(3642);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/orders/statuses?error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit;
    }
}//~if($_POST["save_action"])
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	
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
				<a class="panel_a" onClick="save_action();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
    
    
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(765); ?>
			</div>
			<div class="panel-body">
				<div class="row">
					<div class="col-lg-12" style="margin-bottom: 2px;">
						<a href="javascript:void(0);" onclick="a_add_item();" title="<?php echo translate_str_by_id(2267); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/add.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="a_delete_item();" title="<?php echo translate_str_by_id(2224); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="a_set_for_created();" title="<?php echo translate_str_by_id(3643); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/file.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="a_set_for_paid();" title="<?php echo translate_str_by_id(5329); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/currencies.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="a_set_for_finish();" title="<?php echo translate_str_by_id(5330); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="a_set_for_inverse();" title="<?php echo translate_str_by_id(5331); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png" class="col_img_popup">
						</a>
					</div>
					<div class="col-lg-6">
						<div id="container_A" style="height:300px;">
                        </div>
					</div>
					<div class="col-lg-6">
						<div id="order_statuses_info_div">
                        </div>
					</div>
				</div>
			</div>
		</div>
		
		<div class="hpanel">
			<div class="panel-body">
				<div class="row">
					<div class="col-lg-12" style="margin-bottom: 2px;">
						<table class="table">
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/add.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5332); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5333); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/file.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5334); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/currencies.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5335); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5336); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5337); ?></td>
							</tr>
						</table>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3645); ?>
			</div>
			<div class="panel-body">
				<div class="row">
					<div class="col-lg-12" style="margin-bottom: 2px;">
						<a href="javascript:void(0);" onclick="b_add_item();" title="<?php echo translate_str_by_id(2267); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/add.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_delete_item();" title="<?php echo translate_str_by_id(2224); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_set_for_created();" title="<?php echo translate_str_by_id(5338); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/file.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_set_issue_flag_inverse();" title="<?php echo translate_str_by_id(3647); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/cargo.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_set_for_finish();" title="<?php echo translate_str_by_id(5339); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_count_flag_inverse();" title="<?php echo translate_str_by_id(5340); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/not_count.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_check_for_return();" title="<?php echo translate_str_by_id(5670); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/update.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_set_for_return();" title="<?php echo translate_str_by_id(5671); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/out.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_set_complete_return();" title="<?php echo translate_str_by_id(5672); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/in.png" class="col_img_popup">
						</a>
						<a href="javascript:void(0);" onclick="b_set_reject_return();" title="<?php echo translate_str_by_id(5673); ?>">
							<img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png" class="col_img_popup">
						</a>
					</div>
					<div class="col-lg-6">
						<div id="container_B" style="height:300px;">
                        </div>
					</div>
					<div class="col-lg-6">
						<div id="order_items_statuses_info_div">
                        </div>
					</div>
				</div>
			</div>
		</div>
		
		<div class="hpanel">
			<div class="panel-body">
				<div class="row">
					<div class="col-lg-12" style="margin-bottom: 2px;">
						<table class="table">
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/add.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5332); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5333); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/file.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5341); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/cargo.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5342); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5343); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/not_count.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5344); ?></td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/update.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5674); ?>.</td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/out.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5675); ?>.</td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/in.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5676); ?>.</td>
							</tr>
							<tr>
								<td><img src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png" class="col_img_popup"></td>
								<td><?php echo translate_str_by_id(5677); ?>.</td>
							</tr>
						</table>
					</div>
				</div>
			</div>
		</div>
	</div>
	
    

    <!-- Start Общая часть для двух деревьев -->
    <script>
     //--------------------------------------------------------------------------------------
    //Для редактируемости дерева
    webix.protoUI({
        name:"edittree"
    }, webix.EditAbility, webix.ui.tree);
    //--------------------------------------------------------------------------------------
    webix.protoUI({
        name:"editlist" // or "edittree", "dataview-edit" in case you work with them
    }, webix.EditAbility, webix.ui.list);
    //--------------------------------------------------------------------------------------
    var color_popup = undefined;//Переменная для окна выбора цвета
    var current_edited_type = undefined;//Текущий редактируемый тип статуса (заказ / позиция)
    var current_edited_node_id = undefined;//Текущий редактируемый элемент дерева
    //Открыть панель цвета
    function openColorboard(node_id, type)
    {
        current_edited_node_id = node_id;
        current_edited_type = type;
        
    	//Палитра - опционально
    	var colors_palette = [
    					["#fee984", "#fdf045", "#fff343", "#fbdb44", "#f7d145", "#ecb144"],
    					["#e39544", "#e18c44", "#da704a", "#ecb69b", "#dd7f60", "#d55851"],
    					["#3fb0ec", "#41b4e6", "#42b4e1", "#90cbe1", "#4bb2b9", "#50b1a2"],
    					["#6ab8a2", "#65b487", "#52af88", "#408b74", "#6cb681", "#508d56"],
    					["#4eb1aa", "#89be65", "#54793e", "#92934a", "#80ab59", "#81bc72"],
    					["#a1c977", "#abcc60", "#abcb56", "#cfcf4e", "#eae749", "#f0bb44"],
    					["#aca17f", "#97a196", "#ccc0ba", "#cbc7c5", "#d8d8d8", "#e2e1e1"],
    				];
    
    	color_popup = webix.ui({
    		view:"popup",
    		body:{
    				view:"colorboard",
    				id:"color_board",
    				width	:100,
    				height	:100,
    				left:100, 
    				top:100,
    				cols:30,//Опционально
            		rows:30,//Опционально
    				on:{
    					onSelect:function(val)
    					    {
    					        if(current_edited_type == "a")
    					        {
    					            var item = a_tree.getItem(current_edited_node_id);
    					            item.color = val;
    					            a_onSelected()
    					        }
    					        else
    					        {
    					            var item = b_tree.getItem(current_edited_node_id);
    					            item.color = val;
    					            b_onSelected()
    					        }
    							color_popup.close();
    						}
    				}//~on
    		}//~body
    	});
    	
    	if(current_edited_type == "a")
    	{
    	    color_popup.show(document.getElementById("color_indicator_a_"+current_edited_node_id));
    	}
    	else
    	{
    	    color_popup.show(document.getElementById("color_indicator_b_"+current_edited_node_id));
    	}
    	
    }//~function openColorboard()
    //--------------------------------------------------------------------------------------
    //Функция сохранения
    function save_action()
    {
        //1. Статусы заказов
        //1.1 Дерево A
        var a_tree_json = a_tree.serialize();
    	a_tree_dump = JSON.stringify(a_tree_json);
    	document.getElementById("orders_statuses").value = a_tree_dump;
        
        
    	//2. Статусы позиций заказа
    	//2.1. Дерево B
        var b_tree_json = b_tree.serialize();
    	b_tree_dump = JSON.stringify(b_tree_json);
    	document.getElementById("orders_items_statuses").value = b_tree_dump;
    	
		//2.2. Обязательно проверяем наличие выставленных флагов
		let count_flag = 0;
		b_tree.data.each(function(obj){
			if(obj.count_flag === 0){
				count_flag++;
			}
		});
		if(count_flag <= 0){
			alert("<?php echo translate_str_by_id(3648); ?>");
			return;
		}
		
    	//3.Отправляем форму
    	document.forms["save_form"].submit();
    }
    //--------------------------------------------------------------------------------------
    //--------------------------------------------------------------------------------------
    </script>
    <form name="save_form" method="POST">
        <input type="hidden" name="save_action" value="save_action" />
        
        <input type="hidden" name="orders_statuses" id="orders_statuses" value="" /><!-- Список статусов заказов -->
        
        <input type="hidden" name="orders_items_statuses" id="orders_items_statuses" value="" /><!-- Список статусов позиций заказов -->
		
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!-- End Общая часть для двух деревьев -->
    
    
    
    
    
    
    
    <script type="text/javascript" charset="utf-8">
        <?php
		//Функция записи настроек уведомлений
		if( isset( $DP_Config->orders_statuses_notifications_settings ) )
		{
			?>
			function on_change_notifications_settings(status_type)
			{
				var node = '';
				
				if( status_type == 'order' )
				{
					var nodeId = a_tree.getSelectedId();
					node = a_tree.getItem(nodeId);
				}
				else
				{
					var nodeId = b_tree.getSelectedId();
					node = b_tree.getItem(nodeId);
				}
				
				if( document.getElementById(status_type + '_status_to_manager_email').checked )
				{
					node.to_manager_email = 1;
				}
				else
				{
					node.to_manager_email = 0;
				}
				
				
				if( document.getElementById(status_type + '_status_to_manager_sms').checked )
				{
					node.to_manager_sms = 1;
				}
				else
				{
					node.to_manager_sms = 0;
				}
				
				
				if( document.getElementById(status_type + '_status_to_customer_email').checked )
				{
					node.to_customer_email = 1;
				}
				else
				{
					node.to_customer_email = 0;
				}
				
				
				if( document.getElementById(status_type + '_status_to_customer_sms').checked )
				{
					node.to_customer_sms = 1;
				}
				else
				{
					node.to_customer_sms = 0;
				}
				
			}
			<?php
		}
		?>
		//--------------------------------------------------------------------------------------
        /*ДЕРЕВО СТАТУСОВ ЗАКАЗОВ (А)*/
        //Формирование дерева
        a_tree = new webix.ui({
            editable:true,//редактируемое
            editValue:"value",
        	editaction:"dblclick",//редактирование по двойному нажатию
            container:"container_A",//id блока div для дерева
            view:"edittree",
        	select:true,//можно выделять элементы
        	drag:true,//можно переносить
        	editor:"text",//тип редактирование - текстовый
        	//Шаблон элемента дерева
        	template:function(obj, common)//Шаблон узла дерева
            	{
                    var folder = common.folder(obj, common);
            	    var icon = "";
            	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
                    var icon_right = "";
                    
            	    //Индикация статуса для создаваемого
            	    if(obj.for_created == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/file.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
                    
                    //Индикация статуса для оплаченного
            	    if(obj.for_paid == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/currencies.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
            	    
                    //Индикация статуса
            	    if(obj.for_finish == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
            	    
                    //Индикация статуса
            	    if(obj.for_inverse == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
            	    
                    return common.icon(obj, common) + icon + folder + icon_right + value_text;
            	},//~template
        
        });
        /*~ДЕРЕВО*/
		webix.event(window, "resize", function(){ a_tree.adjust(); });
        //--------------------------------------------------------------------------------------
        //Событие при выборе элемента дерева
        a_tree.attachEvent("onAfterSelect", function(id)
        {
        	a_onSelected();
        });
        //--------------------------------------------------------------------------------------
        //Обработка выбора элемента
        function a_onSelected()
        {
            var nodeId = a_tree.getSelectedId();
            if(nodeId == 0)
            {
                document.getElementById("order_statuses_info_div").innerHTML = "";
                return;
            }
            var node = a_tree.getItem(nodeId);
            
            //Строка для вывода ID
            var id_str = node.id;
            if(node.created_earlier == undefined)
            {
                id_str = "<?php echo translate_str_by_id(3649); ?>";
            }
            
            var node_html = "<table class=\"table\">";
            node_html += "<tr> <td>ID</td> <td>"+id_str+"</td> </tr>";
            node_html += "<tr> <td><?php echo translate_str_by_id(3650); ?></td> <td><div id=\"color_indicator_a_"+node.id+"\" onclick=\"openColorboard("+node.id+", 'a');\" style=\"width:20px; height:20px; border-radius:7px; background-color:"+node.color+"; border:#099 solid 1px;\"></div> </td> </tr>";
            
			<?php
			if( isset( $DP_Config->orders_statuses_notifications_settings ) )
			{
				?>
				node_html += "<tr> <td colspan='2' style='text-align:center;font-weight:bold;'> <?php echo translate_str_by_id(3651); ?> </td>  </tr>";
				
				var input_checked = '';
				if( node.to_manager_email == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3652); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order\");' id='order_status_to_manager_email' "+input_checked+" /> </td> </tr>";
				
				input_checked = '';
				if( node.to_manager_sms == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3653); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order\");' id='order_status_to_manager_sms' "+input_checked+" /> </td> </tr>";
				
				input_checked = '';
				if( node.to_customer_email == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3654); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order\");' id='order_status_to_customer_email' "+input_checked+" /> </td> </tr>";
				
				input_checked = '';
				if( node.to_customer_sms == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3655); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order\");' id='order_status_to_customer_sms' "+input_checked+" /> </td> </tr>";
				
				<?php
			}
			?>
			
			
			node_html += "</table>";
            
            
            
            document.getElementById("order_statuses_info_div").innerHTML = node_html;
        }//function a_onSelected()
        //--------------------------------------------------------------------------------------
        //Событие при успешном редактировании элемента дерева
        a_tree.attachEvent("onValidationSuccess", function(){
            a_onSelected();
        });
        //--------------------------------------------------------------------------------------
        a_tree.attachEvent("onAfterEditStop", function(state, editor, ignoreUpdate){
            a_onSelected();
        });
        //-----------------------------------------------------
    	//Обработчик После перетаскивания узлов дерева
    	a_tree.attachEvent("onAfterDrop",function(){
    	    a_onSelected();
    	});
        //--------------------------------------------------------------------------------------
        //Добавить новый элемент в дерево
        function a_add_item()
        {
        	var newItemId = a_tree.add( {value:"<?php echo translate_str_by_id(3656); ?>", value_lang_str_id:"0", for_created:0, color:"#FFF", for_paid:0, for_finish:0, for_inverse:0, to_manager_email:1, to_manager_sms:1, to_customer_email:1, to_customer_sms:1}, a_tree.count(), 0);
        	
        	a_onSelected();//Обработка текущего выделения
        }
        //--------------------------------------------------------------------------------------
        //Удаление выделеного элемента
        function a_delete_item()
        {
            var nodeId = a_tree.getSelectedId();
            var node = a_tree.getItem(nodeId);
            
            if(a_tree.count() == 1)
            {
                alert("<?php echo translate_str_by_id(3657); ?>");
                return;
            }
            if(node.for_created == 1)
            {
                alert("<?php echo translate_str_by_id(3658); ?>");
                return;
            }
            if(node.for_finish == 1)
            {
                alert("<?php echo translate_str_by_id(5345); ?>");
                return;
            }
            if(node.for_inverse == 1)
            {
                alert("<?php echo translate_str_by_id(5346); ?>");
                return;
            }
        	
        	if(node.for_paid == 1)
        	{
        	    a_current_for_paid = undefined;
        	}
        	
        	a_tree.remove(nodeId);
        	a_onSelected();
        }
        //--------------------------------------------------------------------------------------
        //Назначить для создаваемого заказа
        function a_set_for_created()
        {
            var nodeId = a_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
        
            //Снимаем текущий
            var old_item = a_tree.getItem(a_current_for_created);
            old_item.for_created = 0;
            
            //Ставим новый
            var new_item = a_tree.getItem(nodeId);
            new_item.for_created = 1;
            a_current_for_created = nodeId;
            
            a_tree.refresh();
        }
        //--------------------------------------------------------------------------------------
        //Назначить для оплаченного заказа
        function a_set_for_paid()
        {
            var nodeId = a_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            
            //Снимаем текущий
            if(a_current_for_paid != undefined)
            {
                var old_item = a_tree.getItem(a_current_for_paid);
                old_item.for_paid = 0;
            }
            
            //Ставим новый
			if(nodeId != a_current_for_paid){
				var new_item = a_tree.getItem(nodeId);
				new_item.for_paid = 1;
				a_current_for_paid = nodeId;
			}
            
            a_tree.refresh();
        }
		//--------------------------------------------------------------------------------------
        //Назначить для завершенного заказа
        function a_set_for_finish()
        {
            var nodeId = a_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            
			var item = a_tree.getItem(nodeId);
			
			if(item.for_finish == 1)
            {
                item.for_finish = 0;
            }
            else
            {
                item.for_finish = 1;
            }
            
            a_tree.refresh();
        }
		//--------------------------------------------------------------------------------------
        //Назначить для отмененного заказа
        function a_set_for_inverse()
        {
            var nodeId = a_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            
			var item = a_tree.getItem(nodeId);
			
			if(item.for_inverse == 1)
            {
                item.for_inverse = 0;
            }
            else
            {
                item.for_inverse = 1;
            }
            
            a_tree.refresh();
        }
        //--------------------------------------------------------------------------------------
        //Снятие выделения с дерева
        function a_unselect_tree()
        {
        	a_tree.unselect();
        	a_onSelected();
        }
        //--------------------------------------------------------------------------------------
        var a_current_for_paid = undefined;
		<?php
		//Инициализация дерева при загрузке страницы
		$orders_statuses_query = $db_link->prepare("SELECT * FROM `shop_orders_statuses_ref` ORDER BY `order` ASC;");
        $orders_statuses_query->execute();
        while( $record = $orders_statuses_query->fetch() )
        {
            ?>
            a_tree.add( {value:"<?php echo translate_str_by_id($record["name"]); ?>", value_lang_str_id:"<?php echo $record["name"]; ?>", id:<?php echo $record["id"]; ?>, for_created:<?php echo $record["for_created"]; ?>, for_finish:<?php echo $record["for_finish"]; ?>, for_inverse:<?php echo $record["for_inverse"]; ?>, color:"<?php echo $record["color"]; ?>", created_earlier:1, for_paid:<?php echo $record["for_paid"]; ?>, to_manager_email:<?php echo $record["to_manager_email"]; ?>, to_manager_sms:<?php echo $record["to_manager_sms"]; ?>, to_customer_email:<?php echo $record["to_customer_email"]; ?>, to_customer_sms:<?php echo $record["to_customer_sms"]; ?>}, a_tree.count(), 0);
            <?php
            if($record["for_created"] == 1)
            {
                ?>
                var a_current_for_created = <?php echo $record["id"]; ?>;
                <?php
            }
            ?>
            
            <?php
            if($record["for_paid"] == 1)
            {
                ?>
                a_current_for_paid = <?php echo $record["id"]; ?>;
                <?php
            }
        }
        ?>
    </script>
    
    
    
    
    
    
    
    
    <script type="text/javascript" charset="utf-8">
        //--------------------------------------------------------------------------------------
        /*ДЕРЕВО СТАТУСОВ ПОЗИЦИЙ ЗАКАЗОВ (B)*/
        //Формирование дерева
        b_tree = new webix.ui({
            editable:true,//редактируемое
            editValue:"value",
        	editaction:"dblclick",//редактирование по двойному нажатию
            container:"container_B",//id блока div для дерева
            view:"edittree",
        	select:true,//можно выделять элементы
        	drag:true,//можно переносить
        	editor:"text",//тип редактирование - текстовый
        	//Шаблон элемента дерева
        	template:function(obj, common)//Шаблон узла дерева
            	{
                    var folder = common.folder(obj, common);
            	    var icon = "";
            	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
                    var icon_right = "";
                    
            	    //Индикация статуса для создаваемого
            	    if(obj.for_created == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/file.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
                    
                    //Индикация флага "Учитывать при расчетах"
            	    if(obj.count_flag == 0)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/not_count.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
					
					//Списывать товар со склада при выставлении позиции в данный статус
					if(obj.issue_flag == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/cargo.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
					
					//Позиция выдана
					if(obj.for_finish == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }

                    //Индикация статуса
										if(obj.for_return == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/out.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }

                    //Индикация статуса
                    if(obj.check_for_return == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/update.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }

                    //Индикация статуса
                    if(obj.complete_return == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/in.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }

                    //Индикация статуса
                    if(obj.reject_return == 1)
                    {
                        icon_right += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png' class='col_img' style='float:right; margin:2px 4px 2px 4px;'>";
                    }
            	    
                    return common.icon(obj, common) + icon + folder + icon_right + value_text;
            	},//~template
        
        });
        /*~ДЕРЕВО*/
		webix.event(window, "resize", function(){ b_tree.adjust(); });
        //--------------------------------------------------------------------------------------
        //Событие при выборе элемента дерева
        b_tree.attachEvent("onAfterSelect", function(id)
        {
        	b_onSelected();
        });
        //--------------------------------------------------------------------------------------
        //Обработка выбора элемента
        function b_onSelected()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                document.getElementById("order_items_statuses_info_div").innerHTML = "";
                return;
            }
            var node = b_tree.getItem(nodeId);
            
            //Строка для вывода ID
            var id_str = node.id;
            if(node.created_earlier == undefined)
            {
                id_str = "<?php echo translate_str_by_id(3649); ?>";
            }
            
            var node_html = "<table class=\"table\">";
            node_html += "<tr> <td>ID</td> <td>"+id_str+"</td> </tr>";
            node_html += "<tr> <td><?php echo translate_str_by_id(3650); ?></td> <td><div id=\"color_indicator_b_"+node.id+"\" onclick=\"openColorboard("+node.id+", 'b');\" style=\"width:20px; height:20px; border-radius:7px; background-color:"+node.color+"; border:#099 solid 1px;\"></div> </td> </tr>";
            
			<?php
			if( isset( $DP_Config->orders_statuses_notifications_settings ) )
			{
				?>
				node_html += "<tr> <td colspan='2' style='text-align:center;font-weight:bold;'> <?php echo translate_str_by_id(3651); ?> </td>  </tr>";
				
				var input_checked = '';
				if( node.to_manager_email == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3652); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order_item\");' id='order_item_status_to_manager_email' "+input_checked+" /> </td> </tr>";
				
				input_checked = '';
				if( node.to_manager_sms == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3653); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order_item\");' id='order_item_status_to_manager_sms' "+input_checked+" /> </td> </tr>";
				
				input_checked = '';
				if( node.to_customer_email == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3654); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order_item\");' id='order_item_status_to_customer_email' "+input_checked+" /> </td> </tr>";
				
				input_checked = '';
				if( node.to_customer_sms == 1 )
				{
					input_checked = ' checked=\"checked\" ';
				}
				node_html += "<tr> <td><?php echo translate_str_by_id(3655); ?></td> <td> <input type='checkbox' onchange='on_change_notifications_settings(\"order_item\");' id='order_item_status_to_customer_sms' "+input_checked+" /> </td> </tr>";
				
				<?php
			}
			?>
			
			
			
			
			node_html += "</table>";
            
            
            
            document.getElementById("order_items_statuses_info_div").innerHTML = node_html;
        }//function a_onSelected()
        //--------------------------------------------------------------------------------------
        //Событие при успешном редактировании элемента дерева
        b_tree.attachEvent("onValidationSuccess", function(){
            b_onSelected();
        });
        //--------------------------------------------------------------------------------------
        b_tree.attachEvent("onAfterEditStop", function(state, editor, ignoreUpdate){
            b_onSelected();
        });
        //-----------------------------------------------------
    	//Обработчик После перетаскивания узлов дерева
    	b_tree.attachEvent("onAfterDrop",function(){
    	    b_onSelected();
    	});
        //--------------------------------------------------------------------------------------
        //Добавить новый элемент в дерево
        function b_add_item()
        {
        	var newItemId = b_tree.add( {value:"<?php echo translate_str_by_id(3656); ?>", value_lang_str_id:"0", for_created:0, for_finish:0, color:"#FFF", count_flag:1, to_manager_email:1, to_manager_sms:1, to_customer_email:1, to_customer_sms:1, for_return:0, complete_return:0, check_for_return:0, reject_return: 0}, b_tree.count(), 0);
        	
        	b_onSelected();//Обработка текущего выделения
        }
        //--------------------------------------------------------------------------------------
        //Удаление выделеного элемента
        function b_delete_item()
        {
            var nodeId = b_tree.getSelectedId();
            var node = b_tree.getItem(nodeId);
            
            if(b_tree.count() == 1)
            {
                alert("<?php echo translate_str_by_id(3657); ?>");
                return;
            }
            if(node.for_created == 1)
            {
                alert("<?php echo translate_str_by_id(3658); ?>");
                return;
            }
        	
        	b_tree.remove(nodeId);
        	b_onSelected();
        }
        //--------------------------------------------------------------------------------------
        //Назначить для создаваемой позиции
        function b_set_for_created()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
        
            //Снимаем текущий
            var old_item = b_tree.getItem(b_current_for_created);
            old_item.for_created = 0;
            
            //Ставим новый
            var new_item = b_tree.getItem(nodeId);
            new_item.for_created = 1;
            b_current_for_created = nodeId;
            
            b_tree.refresh();
        }
		//--------------------------------------------------------------------------------------
        //Назначить для выданой позиции
        function b_set_for_finish()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            
			var item = b_tree.getItem(nodeId);
			
			if(item.for_finish == 1)
            {
                item.for_finish = 0;
            }
            else
            {
                item.for_finish = 1;
            }
            
            b_tree.refresh();
        }


				
        //Оформление заявки на возврат
        function b_set_for_return()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            if (typeof b_current_for_return != 'undefined')
            {
                var current_node =  b_tree.getItem(b_current_for_return);
                current_node.for_return = 0;
            }

            if (typeof b_current_for_return != 'undefined' && b_current_for_return == nodeId)
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.for_return = 0;
                b_current_for_return = undefined;
            }
            else
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.for_return = 1;
                b_current_for_return = nodeId;
            }

            b_tree.refresh();
        }

        //Доступен для оформления
        function b_check_for_return()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }

            if (typeof b_current_check_for_return != 'undefined' && b_current_check_for_return == nodeId)
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.check_for_return = 0;
                b_current_check_for_return = undefined;
            }
            else
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.check_for_return = 1;
                b_current_check_for_return = nodeId;
            }

            b_tree.refresh();
        }

        //Одобрение возврата
        function b_set_complete_return()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            if (typeof b_current_for_complete_return != 'undefined')
            {
                var current_node =  b_tree.getItem(b_current_for_complete_return);
                current_node.complete_return = 0;
            }

            if (typeof b_current_for_complete_return != 'undefined' && b_current_for_complete_return == nodeId)
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.complete_return = 0;
                b_current_for_complete_return = undefined;
            }
            else
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.complete_return = 1;
                b_current_for_complete_return = nodeId;
            }

            b_tree.refresh();
        }

        //Отклонение возврата
        function b_set_reject_return()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }

            if (typeof b_current_for_reject_return != 'undefined')
            {
                var current_node =  b_tree.getItem(b_current_for_reject_return);
                current_node.reject_return = 0;
            }

            if (typeof b_current_for_reject_return != 'undefined' && b_current_for_reject_return == nodeId)
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.reject_return = 0;
                b_current_for_reject_return = undefined;
            }
            else
            {
                var new_item = b_tree.getItem(nodeId);
                new_item.reject_return = 1;
                b_current_for_reject_return = nodeId;
            }

            b_tree.refresh();
        }


        //--------------------------------------------------------------------------------------
        //Снятие выделения с дерева
        function b_unselect_tree()
        {
        	b_tree.unselect();
        	b_onSelected();
        }
        //--------------------------------------------------------------------------------------
        //Обратить значение флага "Учитывать при расчетах"
        function b_count_flag_inverse()
        {
            var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            
            var item = b_tree.getItem(nodeId);
            
            if(item.count_flag == 1)
            {
                item.count_flag = 0;
				item.issue_flag = 0;//Обязательно запретить списание товара
            }
            else
            {
                item.count_flag = 1;
            }
            
            b_tree.refresh();
        }
		//--------------------------------------------------------------------------------------
		//При выставлении позиции в данный статус - идет списание товара со склада
		function b_set_issue_flag_inverse()
		{
			var nodeId = b_tree.getSelectedId();
            if(nodeId == 0)
            {
                alert("<?php echo translate_str_by_id(3659); ?>");
                return;
            }
            
            var item = b_tree.getItem(nodeId);
            
            if(item.issue_flag == 1)
            {
                item.issue_flag = 0;
            }
            else
            {
                item.issue_flag = 1;
				item.count_flag = 1;//Обязательно учесть при подсчете суммы
            }
            
            b_tree.refresh();
		}
        //--------------------------------------------------------------------------------------
        <?php
        //Инициализация дерева при загрузке страницы
		$orders_items_statuses_query = $db_link->prepare("SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC;");
        $orders_items_statuses_query->execute();
        while( $record = $orders_items_statuses_query->fetch() )
        {
            ?>
            b_tree.add( {
                value:"<?php echo translate_str_by_id($record["name"]); ?>",
				value_lang_str_id:"<?php echo $record["name"]; ?>",
                id:<?php echo $record["id"]; ?>,
                for_created:<?php echo $record["for_created"]; ?>,
                for_return:<?php echo $record["for_return"]; ?>,
                check_for_return:<?php echo $record["check_for_return"]; ?>,
                complete_return:<?php echo $record["complete_return"]; ?>,
                reject_return:<?php echo $record["reject_return"]; ?>,
                for_finish:<?php echo $record["for_finish"]; ?>,
                color:"<?php echo $record["color"]; ?>",
                count_flag:<?php echo $record["count_flag"]; ?>,
                issue_flag:<?php echo $record["issue_flag"]; ?>,
                created_earlier:1,
                to_manager_email:<?php echo $record["to_manager_email"]; ?>,
                to_manager_sms:<?php echo $record["to_manager_sms"]; ?>,
                to_customer_email:<?php echo $record["to_customer_email"]; ?>,
                to_customer_sms:<?php echo $record["to_customer_sms"]; ?>},
                b_tree.count(), 0
            );
			<?php
            //Указываем текущий для создаваемой позиции
            if($record["for_created"] == 1)
            {
                ?>
                var b_current_for_created = <?php echo $record["id"]; ?>;
                <?php
            }
						
            if($record["for_return"] == 1)
            {
                ?>
                    var b_current_for_return = <?php echo $record["id"]; ?>;
                <?php
            }

            if($record["complete_return"] == 1)
            {
                ?>
                    var b_current_for_complete_return= <?php echo $record["id"]; ?>;
                <?php
            }

            if ($record["check_for_return"])
            {
                ?>
                    var b_current_check_for_return = <?php echo $record["id"]; ?>;
                <?php
            }

            if ($record["reject_return"])
            {
                ?>
                    var b_current_for_reject_return = <?php echo $record["id"]; ?>;
                <?php
            }

        }
        ?>
    </script>
    
    
    <style>
    .webix_tree_leaves > div {
		border-bottom: 1px dotted #eee;
	}
	.webix_tree_leaves .webix_tree_none {
		display: none;
	}
	.webix_tree .webix_dt_editor {
		left: 33px !important;
		right: 36px !important;
		width: auto !important;
	}
	</style>
    
    <?php
}
?>