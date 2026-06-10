<?php
/**
Страничный скрипт с менеджером специальных поисков
*/
defined('_ASTEXE_') or die('No access');
?>


<?php
if( !empty( $_POST["action"] ) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	if( $_POST["action"] == "delete_special_searches")//Удаление поисков
	{
		//Список ID поисков для удаления
		$searches_ids = $_POST["searches_ids"];
		//$searches_ids_str = str_replace( array("[", "]") , "", $searches_ids);//Строка с перечислением через запятую
		$searches_ids = json_decode($searches_ids, true);//PHP - массив
		
		$binding_values = array();
		$searches_ids_str = "";
		for( $i=0 ; $i < count($searches_ids) ; $i++ )
		{
			if( $i > 0 )
			{
				$searches_ids_str = $searches_ids_str.",";
			}
			$searches_ids_str = $searches_ids_str."?";
			
			array_push($binding_values, $searches_ids[$i]);
		}
		
		//Удаляем файл картинки
		$search_img_result = true;
		$img_query = $db_link->prepare("SELECT `img` FROM `shop_special_searches` WHERE `id` IN ($searches_ids_str) AND `img` != '';");
		$img_query->execute($binding_values);
		while( $img = $img_query->fetch() )
		{
			/*
			if(!unlink($_SERVER["DOCUMENT_ROOT"]."/content/files/images/catalogue_images/".$img["img"]))
			{
				$search_img_result = false;
			}
			*/
		}
		
		//Удаляем учетные записи поиков
		$search_record_result = $db_link->prepare("DELETE FROM `shop_special_searches` WHERE `id` IN ($searches_ids_str);")->execute($binding_values);
		
		
		//Удаляем шаги поисков
		$search_steps_result = $db_link->prepare("DELETE FROM `shop_special_searches_steps` WHERE `search_id` IN ($searches_ids_str);")->execute($binding_values);
		
		//Выводим результат
		if($search_img_result && $search_record_result && $search_steps_result)
		{
			//Полный успех
			$success_message = translate_str_by_id(3058);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/specialnye-poiski?success_message=<?php echo $success_message; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$warning_message = translate_str_by_id(2912);
			
			if(!$search_img_result)
			{
				$warning_message .= ". ".translate_str_by_id(3059);
			}
			if(!$search_record_result)
			{
				$warning_message .= ". ".translate_str_by_id(3060);
			}
			if(!$search_steps_result)
			{
				$warning_message .= ". ".translate_str_by_id(3061);
			}
			
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/specialnye-poiski?warning_message=<?php echo $warning_message; ?>";
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
				<a class="panel_a" href="<?php echo "/".$DP_Config->backend_dir; ?>/shop/catalogue/specialnye-poiski/specialnyj-poisk">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<a class="panel_a" onClick="deleteSpecialSearches();" href="javascript:void(0);">
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
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3062); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr>
								<th style="width:50px;">
									<input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/>
								</th>
								<th style="width:50px;">ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
							</tr>
						</thead>
						<tbody>
							<?php
							//Массивы для JS с id элементов и с чекбоксами элементов
							$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
							$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
							
							$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `shop_special_searches`;");
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
								
								//Ссылка для элемента списка
								$a_item = "<a href=\"/".$DP_Config->backend_dir."/shop/catalogue/specialnye-poiski/specialnyj-poisk?special_search_id=".$element_record["id"]."\">";
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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/shop/catalogue/specialnye-poiski?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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
	
	
	
	
	
	<form name="form_to_delete" method="post" style="display:none">
        <input type="text" name="action" value="delete_special_searches" />
		
		<input type="text" name="searches_ids" id="searches_ids" value="" />
		
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
	<script>
	//Функция удаления специальных поисков
	function deleteSpecialSearches()
	{
		var searches_ids = getCheckedElements();
		
		if(searches_ids.length == 0)
		{
			alert("<?php echo translate_str_by_id(3063); ?>");
			return;
		}
		
		
		if( !confirm("<?php echo translate_str_by_id(3064); ?>") )
		{
			return;
		}
		
		document.getElementById("searches_ids").value = JSON.stringify(searches_ids);
		
		document.forms["form_to_delete"].submit();
	}
	</script>
	
	
	
	

    
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
}
?>