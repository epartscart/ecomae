<?php
/**
 * Страница управления точками выдачи (магазинами)
 * 
 * Данная страница предназначена для создания/редактирования/удаления точек выдачи
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( ! empty($_POST["action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
    if($_POST["action"] == "delete_offices")
    {
        $offices = json_decode($_POST["offices"], true);
        
        //Делаем строку с перечислением через запятую магазинов
        $offices_str = "";
        $binding_values = array();
		for($i=0; $i < count($offices); $i++)
        {
            if($i > 0) $offices_str .= ",";
            $offices_str .= "?";
			
			array_push($binding_values, $offices[$i]);
        }
        
        
        //1. Удаляем учетные записи магазинов (shop_offices)
        $delete_record_result = true;//Результат
		if( $db_link->prepare("DELETE FROM `shop_offices` WHERE `id` IN ($offices_str);")->execute($binding_values) != true)
        {
            $delete_record_result = false;//Результат
        }
        
        
        //2. Удаляем связь магазинов и складов (shop_offices_storages_map)
        $delete_offices_storages_map_result = true;//Результат
		if( $db_link->prepare("DELETE FROM `shop_offices_storages_map` WHERE `office_id` IN ($offices_str);")->execute( $binding_values ) != true)
        {
            $delete_offices_storages_map_result = false;//Результат
        }
        

        //3. Удаляем географические привязки (shop_offices_geo_map)
        $delete_offices_geo_result = true;
		if( $db_link->prepare("DELETE FROM `shop_offices_geo_map` WHERE `office_id` IN ($offices_str);")->execute($binding_values) != true)
        {
            $delete_offices_geo_result = false;//Результат
        }
        
        
        
        
        
        
        
        if($delete_record_result && $delete_offices_storages_map_result && $delete_offices_geo_result)
        {
            $success_message = translate_str_by_id(3407);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices?success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
            $error_message = translate_str_by_id(2912).": <br>";
            if(!$delete_record_result)
            {
                $error_message .= translate_str_by_id(3408)."<br>";
            }
            if(!$delete_offices_storages_map_result)
            {
                $error_message .= translate_str_by_id(3409)."<br>";
            }
            if(!$delete_offices_geo_result)
            {
                $error_message .= translate_str_by_id(3410)."<br>";
            }
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
    }
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
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/offices/office">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<a class="panel_a" onClick="deleteOffices();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>


				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
    
    
    
    
    
    <!-- Блок удаления магазинов -->
    <form name="delete_offices_form" method="POST">
        <input type="hidden" name="action" value="delete_offices" />
        <input type="hidden" name="offices" id="offices_to_delete" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <script>
        //Удаление магазинов
        function deleteOffices()
        {
            var offices = getCheckedElements();
            
            if(offices.length == 0)
            {
                alert("<?php echo translate_str_by_id(3411); ?>");
                return;
            }
            if(!confirm("<?php echo translate_str_by_id(3412); ?>"))
            {
                return;
            }
            
            
            
            
            document.getElementById("offices_to_delete").value = JSON.stringify(offices);
            document.forms["delete_offices_form"].submit();
        }
    </script>
    
    
    
    
    
    
    
    <div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3413); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead> 
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
								<th><?php echo translate_str_by_id(3376); ?></th>
								<th><?php echo translate_str_by_id(1312); ?></th>
							</tr>
						</thead>
						<tbody>
						<?php
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						

						$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `shop_offices`;");
						$elements_query->execute();
						
						
						$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
						$elements_count_rows_query->execute();
						$elements_count_rows = $elements_count_rows_query->fetchColumn();
						
						
						//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
						//---------------------------------------------------------------------------------------------->
						//Определяем количество страниц для вывода:
						$p = $DP_Config->list_page_limit;//Штук на страницу
						$count_pages = (int)( $elements_count_rows / $p);//Количество страниц
						if($elements_count_rows%$p)//Если остались еще элементы
						{
							$count_pages++;
						}
						//Определяем, с какой страницы начать вывод:
						$s_page = 0;
						if(!empty($_GET['s_page']))
						{
							$s_page = $_GET['s_page'];
						}
						//----------------------------------------------------------------------------------------------|
						
						
						for($i=0, $d=0; $i<$elements_count_rows && $d<$p; $i++, $d++)
						{
							$element_record = $elements_query->fetch();
							
							//Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
							if($i < $s_page*$p)
							{
								$d--;
								continue;
							}
							
							//Для Javascript
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
							
							
							$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/shop/logistics/offices/office?office_id=".$element_record["id"]." \">";
						?>
							<tr>
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
								<td><?php echo $a_item.$element_record["id"]; ?></a></td>
								<td><?php echo $a_item.translate_str_by_id($element_record["caption"]); ?></a></td>
								<td><?php echo $a_item.translate_str_by_id($element_record["city"]).", ".translate_str_by_id($element_record["address"]); ?></a></td>
								<td><?php echo $a_item.$element_record["phone"]; ?></a></td>
							</tr>
						<?php
						}//for
						?>
						</tbody>
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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/shop/logistics/offices?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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
    </script>
    
    <?php
}//~else - вывод страницы
?>