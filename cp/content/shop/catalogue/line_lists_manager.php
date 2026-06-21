<?php
/**
 * Страница менеджера справочников свойств
*/
defined('_ASTEXE_') or die('No access');

$types_list = array(1 => translate_str_by_id(3096), 2 => translate_str_by_id(2898));
?>


<?php
if(!empty($_POST["action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    //Удаление линейных списков
    if($_POST["action"] == "delete_line_lists")
    {
        $line_lists = json_decode($_POST["line_lists"], true);
        
        //Подстрока для перечисления id удаляемых списков через запятую        
        $line_lists_ids_str = "";
        $binding_values = array();
		for($i=0; $i < count($line_lists); $i++)
        {
			//Защита от удаления линейного списка производителей
			if( (int)$line_lists[$i] == 10 )
			{
				$warning_message = translate_str_by_id(2910);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists?warning_message=<?php echo $warning_message; ?>";
				</script>
				<?php
				exit;
			}
			
			
			
            if($i > 0) $line_lists_ids_str .= ",";
            $line_lists_ids_str .= "?";
			
			array_push($binding_values, $line_lists[$i]);
        }
        
        //Список свойств, которые относятся к удаляемым спискам (property_id из таблицы shop_categories_properties_map по которому нужно удалять записи значений свойств для товаров из "5 таблиц")
        $properties_ids_str = "";
		
		$properties_id_query = $db_link->prepare("SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `list_id` IN ($line_lists_ids_str);");
		$properties_id_query->execute($binding_values);
        while($property_record = $properties_id_query->fetch())
        {
            if($properties_ids_str != "") $properties_ids_str .= ",";
            $properties_ids_str .= $property_record["id"];
        }
        
        
        //1. Удаляем учетную запись линейного списка (shop_line_lists)
        $line_list_record_delete_result = true;//Результат
        if( $db_link->prepare("DELETE FROM `shop_line_lists` WHERE `id` IN ($line_lists_ids_str);")->execute($binding_values) != true)
        {
            $line_list_record_delete_result = false;
        }
        
        //2. Удаляем связи этого линейного списка с категориями товаров (shop_categories_properties_map)
        $categories_link_delete_result = true;//Результат
		if( $db_link->prepare("DELETE FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `list_id` IN ($line_lists_ids_str);")->execute($binding_values) != true)
        {
            $categories_link_delete_result = false;//Результат
        }
        
        //3. Удаляем записи значений списка для товаров (shop_properties_values_list)
        $values_delete_result = true;//Результат
		if( $db_link->prepare("DELETE FROM `shop_properties_values_list` WHERE `property_id` IN ($properties_ids_str);")->execute($binding_values) != true)
        {
            $values_delete_result = false;//Результат
        }
        
        
        if($line_list_record_delete_result && $categories_link_delete_result && $values_delete_result)
        {
            $success_message = translate_str_by_id(2911);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists?success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
            $error_message = translate_str_by_id(2912).": <br>";
            if(!$line_list_record_delete_result)
            {
                $error_message .= translate_str_by_id(2913)."<br>";
            }
            if(!$categories_link_delete_result)
            {
                $error_message .= translate_str_by_id(2914)."<br>";
            }
            if(!$values_delete_result)
            {
                $error_message .= translate_str_by_id(2915)."<br>";
            }
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists?error_message=<?php echo $error_message; ?>";
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
				<a class="panel_a" href="<?php echo "/".$DP_Config->backend_dir; ?>/shop/catalogue/line_lists/line_list">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<a class="panel_a" onClick="deleteLineLists();" href="javascript:void(0);">
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
	
	

    
    
    
    
    <!-- Блок удаления линейных списков -->
    <form name="delete_line_lists_form" method="POST">
        <input type="hidden" name="action" value="delete_line_lists" />
        <input type="hidden" name="line_lists" id="line_lists_to_delete" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <script>
        //Удаление линейных списков
        function deleteLineLists()
        {
            var line_lists = getCheckedElements();
            
            if(line_lists.length == 0)
            {
                alert("<?php echo translate_str_by_id(2916); ?>");
                return;
            }
            if(!confirm("<?php echo translate_str_by_id(2917); ?>"))
            {
                return;
            }
            
            
            
            
            document.getElementById("line_lists_to_delete").value = JSON.stringify(line_lists);
            document.forms["delete_line_lists_form"].submit();
        }
    </script>
    
    
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2918); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr>
								<th>
									<input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/>
								</th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
								<th><?php echo translate_str_by_id(2238); ?></th>
							</tr>
						</thead>
						<tbody>
							<?php
							//Массивы для JS с id элементов и с чекбоксами элементов
							$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
							$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
							
							$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `shop_line_lists`;");
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
								$element_record =$elements_query->fetch();
								
								//Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
								if($i < $s_page*$p)
								{
									$d--;
									continue;
								}
								
								//Для Javascript
								$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
								$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
								
								//Ссылка для элемента списка
								$a_item = "<a href=\"/".$DP_Config->backend_dir."/shop/catalogue/line_lists/line_list?id=".$element_record["id"]."\">";
							?>
								<tr>
									<td>
										<input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/>
									</td>
									<td>
										<?php echo $a_item.$element_record["id"]; ?></a>
									</td>
									<td>
										<?php echo $a_item.translate_str_by_id($element_record["caption"]); ?></a>
									</td>
									<td>
										<?php echo $a_item.$types_list[$element_record["type"]]; ?></a>
									</td>
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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/shop/catalogue/line_lists?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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
}//~else//Действий нет - выводим страницу
?>