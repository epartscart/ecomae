<?php
/**
 * Страница управления модулями в бэкэнде
*/
defined('_ASTEXE_') or die('No access');

//Режим редактирования Фронтэнд/Бэкэнд
$edit_mode = null;
if( isset($_COOKIE["edit_mode"]) )
{
	$edit_mode = $_COOKIE["edit_mode"];
}
switch($edit_mode)
{
    case "frontend":
        $is_frontend = 1;
        break;
    case "backend":
        $is_frontend = 0;
        break;
    default:
        $is_frontend = 1;
        break;
}
?>

<?php
//Если есть действия
if(!empty($_POST["modules_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Если админ в другой вкладке браузера переключил режим редактирования (фронтенд/бэкенд), то, запрещаем сохранять изменения.
	if( $is_frontend != $_POST["is_frontend"] )
	{
		$warning_message = urlencode(translate_str_by_id(2204));
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/?warning_message=<?php echo $warning_message; ?>";
		</script>
		<?php
		exit;
	}
	
	
	
    $modules_list = json_decode($_POST["modules_list"], true);//Список модулей
    
    if($_POST["modules_action_type"] == "delete")
    {
        //Удаление указанных модулей
        $SQL_DELETE = "DELETE FROM `modules` WHERE ";
        $SQL_SUB_IDS = "";
        $binding_values = array();
		for($i=0; $i < count($modules_list); $i++)
        {
            if($SQL_SUB_IDS != "") $SQL_SUB_IDS .= " OR";
            $SQL_SUB_IDS .= " `id`=?";
			
			array_push($binding_values, (int)$modules_list[$i]);
        }//for($i) - по всем удаляемым модулям
        
		
        if( $db_link->prepare($SQL_DELETE.$SQL_SUB_IDS)->execute($binding_values) != true)
        {
            //Возникла ошибка
            $error_message = translate_str_by_id(2639);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/modules_manager?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit();
        }
        else//Модули удалены успешно
        {
            //Теперь нужно отвязать удаленные модули от страниц
            $content_untie_no_error = true;//Флаг - указыват, что отвязка модулей от материалов прошла успешно
            //Цикл по списку удаленных модулей
            for($i=0; $i < count($modules_list); $i++)
            {
                //Получаем список материалов
				$content_query = $db_link->prepare("SELECT * FROM `content` WHERE `is_frontend` = ?;");
				$content_query->execute( array($is_frontend) );
                //Цикл по списку материалов
                while( $content_record = $content_query->fetch() )
                {
                    $modules_array = json_decode($content_record["modules_array"], true);//Список модулей, подключнных к данному материалу
                    //Если данный модуль был привязан к странице - убираем его из списка модулей страницы:
                    if(array_search($modules_list[$i], $modules_array) !== false)
                    {
                        $modules_array_new = array();//Новый массив с модулями страницы в который не попадет данный модуль
                        //По старому списку модулей страницы
                        for($j=0; $j<count($modules_array); $j++)
                        {
                            if($modules_array[$j] != $modules_list[$i])//Если этот элемент списка модулей страницы не равен удаляемому модулю - оставляем его в списке модулей страницы
                            {
                                array_push($modules_array_new, (integer)$modules_array[$j]);
                            }
                        }
                        //Обновляем список модулей в странице
                        if( $db_link->prepare("UPDATE `content` SET `modules_array` = ? WHERE `id` = ?;")->execute( array(json_encode($modules_array_new), $content_record["id"]) ) != true)
                        {
                            $content_untie_no_error = false;
                        }
                    }//if - если этот модуль был в списке страницы
                }//for($c) - по списку страниц
            }//for($i) - по всем удаляемым модулям
            
            
            
            $warning_message = "";
            if(!$content_untie_no_error)
            {
                $warning_message = "&warning_message=".translate_str_by_id(2640);
            }
            
            //Выполнено без ошибок
            $success_message = translate_str_by_id(2641);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/modules_manager?success_message=<?php echo $success_message.$warning_message; ?>";
            </script>
            <?php
            exit();
        }
    }//if - удаление модулей
    else if($_POST["modules_action_type"] == "activated")
    {
        //Включение / Отключение указанных модулей
        $SQL_UPDATE_ACTIVATE = "UPDATE `modules` SET `activated` = ".$_POST["flag_value"]." WHERE ";
        $SQL_SUB_IDS = "";
        $binding_values = array();
		for($i=0; $i < count($modules_list); $i++)
        {
            if($SQL_SUB_IDS != "") $SQL_SUB_IDS .= " OR";
            $SQL_SUB_IDS .= " `id`=?";
			
			array_push($binding_values, (int)$modules_list[$i]);
        }
		

        if( $db_link->prepare($SQL_UPDATE_ACTIVATE.$SQL_SUB_IDS)->execute($binding_values) != true)
        {
            //Возникла ошибка
            $error_message = translate_str_by_id(2642);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/modules_manager?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit();
        }
        else
        {
            //Выполнено без ошибок
            $success_message = translate_str_by_id(2643);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/modules_manager?success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit();
        }
    }//~else if($_POST["modules_action_type"] == "activated")
}//if - есть действие
else//Действий нет - выводим страницу менеджера
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
				<a class="panel_a" onClick="openPrototypeWindow();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2292); ?></div>
				</a>
				
				<a class="panel_a" onClick="deleteModules(0);" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="activateModules(0, true);" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/on.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2644); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="activateModules(0, false);" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2645); ?></div>
				</a>
				
				
				<script>
				//Функция установки режима редактирования Фронтэнд/Бэкэнд
				function set_edit_mode(mode)
				{
					$.getJSON("<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/content/control/set_edit_mode_cookie.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&edit_mode="+encodeURI(mode)+"&callback=?", function(data){
							location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/modules_manager";
						});
				}
				</script>
				
				<?php
				if($is_frontend)
				{
				?>
					<a class="panel_a" onClick="set_edit_mode('backend');" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/backend_edit.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2221); ?></div>
					</a>
				<?php
				}
				else
				{
				?>
					<a class="panel_a" onClick="set_edit_mode('frontend');" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/frontend_edit.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2222); ?></div>
					</a>
				<?php
				}
				?>
			
			 
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
    

	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2646); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead> 
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
								<th><?php echo translate_str_by_id(2630); ?></th>
								<th><?php echo translate_str_by_id(2118); ?></th>
								<th><?php echo translate_str_by_id(2627); ?></th>
								<th class="text-center"><?php echo translate_str_by_id(2647); ?></th>
							</tr>
						</thead>
						<tbody>
						<?php
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						

						
						$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `modules` WHERE `is_frontend` = ? AND `is_prototype` = 0 AND `control_available` = ?;");
						$elements_query->execute( array($is_frontend, 1) );
						
						$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
						$elements_count_rows_query->execute();
						$elements_count_rows = $elements_count_rows_query->fetchColumn();
						
						//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
						//---------------------------------------------------------------------------------------------->
						//Определяем количество страниц для вывода:
						$p = $DP_Config->list_page_limit;//Штук на страницу
						$count_pages = (int)($elements_count_rows / $p);//Количество страниц
						if($elements_count_rows % $p)//Если остались еще элементы
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
							
							$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/modules/module?module_id=".$element_record["id"]." \">";
						?>
							<tr>
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
								<td><?php echo $a_item.$element_record["id"]; ?></a></td>
								<td><?php echo $a_item.translate_str_by_id($element_record["caption"]); ?></a></td>
								<td><?php echo $a_item.translate_str_by_id($element_record["prototype_name"]); ?></a></td>
								<td><?php echo $a_item.$element_record["content_type"]; ?></a></td>
								<td><?php echo $a_item.$element_record["position"]; ?></a></td>
								<td class="text-center">
								<?php
								if($element_record["activated"] == 1)
								{
									?>
									<a href="javascript:void(0);" onclick="activateModules(<?php echo $element_record["id"]; ?>, false);"><img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/on.png" /></a>
									<?php
								}
								else
								{
									?>
									<a href="javascript:void(0);" onclick="activateModules(<?php echo $element_record["id"]; ?>, true);"><img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/off.png" /></a>
									<?php
								}
								?>
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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/modules/modules_manager?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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

	
	
	

    
    
    <!-- START Общая форма для действий менеджера: активация/деактивация/удаление -->
    <form name="modules_manager_action_form" method="POST" style="display:none">
        <input type="hidden" name="modules_action" id="modules_action" value="modules_action" /><!-- Говорит, что есть действие -->
        <input type="hidden" name="modules_action_type" id="modules_action_type" value="" /><!-- Тип действия (Удаление или Активация) -->
        <input type="hidden" name="flag_value" id="flag_value" value="" /><!-- Значение действия -->
        <input type="hidden" name="modules_list" id="modules_list" value="" />
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none"/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!-- END Общая форма для действий менеджера: активация/деактивация/удаление -->
    
    
    
    <!-- START БЛОК АКТИВАЦИИ / ДЕАКТИВАЦИИ МОДУЛЕЙ -->
    <script>
    //Функция активации модулей
    function activateModules(module, activate_value)
    {
        if(module == 0)//Если равно 0 - активируем отмеченные галочками модули
        {
            document.getElementById("modules_list").value = JSON.stringify(getCheckedElements());
            if(document.getElementById("modules_list").value == "[]")
            {
                alert("<?php echo translate_str_by_id(2648); ?>");
                return;
            }
        }
        else//Активируем только один указанный модуль
        {
            document.getElementById("modules_list").value = "["+module+"]";
        }
        
        document.getElementById("modules_action_type").value = "activated";//Тип действия - активация / деактивация
        document.getElementById("flag_value").value = activate_value;
        
        //Отправляем форму
    	document.forms["modules_manager_action_form"].submit();//Отправляем
    }
    </script>
    <!-- END БЛОК АКТИВАЦИИ / ДЕАКТИВАЦИИ МОДУЛЕЙ -->
    
    
    
    
    <!-- START БЛОК УДАЛЕНИЯ МОДУЛЕЙ -->
    <script>
    //Функция удаления модулей
    function deleteModules(module)
    {
        if(module == 0)//Если равно 0 - удаляем отмеченные галочками модули
        {
            document.getElementById("modules_list").value = JSON.stringify(getCheckedElements());
            if(document.getElementById("modules_list").value == "[]")
            {
                alert("<?php echo translate_str_by_id(2648); ?>");
                return;
            }
        }
        else//Удаляем только один указанный модуль
        {
            document.getElementById("modules_list").value = "["+module+"]";
        }
        
        if(!confirm("<?php echo translate_str_by_id(2649); ?>"))
        {
            return;
        }
        
        document.getElementById("modules_action_type").value = "delete";//Тип действия - удаление
        
        //Отправляем форму
    	document.forms["modules_manager_action_form"].submit();//Отправляем
    }
    </script>
    <!-- END БЛОК УДАЛЕНИЯ МОДУЛЕЙ -->
    
    
    
    
    
    
    <!--Start Модальное окно: Выбор прототипа создаваемого модуля -->
    <div class="text-center m-b-md">
		<div class="modal fade" id="modalWindow_1" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(2650); ?></h4>
					</div>
					<div class="modal-body">
						<div class="row">
						<?php
						//Выводим список прототипов
						$prototypes_query = $db_link->prepare("SELECT * FROM `modules` WHERE `is_prototype` = 1 AND `is_frontend` = ? AND `control_available` = ?;");
						$prototypes_query->execute( array($is_frontend, 1) );
						
						//Таблицу выводим в две колонки
						while( $prototype_record = $prototypes_query->fetch() )
						{
							?>
							<div class="col-lg-4">
								<a href="/<?php echo $DP_Config->backend_dir; ?>/modules/module?prototype_id=<?php echo $prototype_record["id"]; ?>"><?php echo translate_str_by_id($prototype_record["prototype_name"]); ?></a>
							</div>
							<?php
						}
						?>
						</div>
					</div>
					<div class="modal-footer">
						<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	


    <script>
        // ----------------------------------------------------------------
        function openPrototypeWindow()
        {
            jQuery('#modalWindow_1').modal();//Открыть окно
        }
        // ----------------------------------------------------------------
        //document.body.innerHTML += "Ok";
    </script>
    <!--Start Модальное окно: Выбор прототипа создаваемого модуля -->
    
    
    
    
    
    
    
    
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