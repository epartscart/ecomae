<?php
/**
 * Менеджер шаблонов
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
//Метод удаления не пустого каталога
function removeNotEmptyDir($dir)
{
    if(is_file($dir)) return unlink($dir);
    
    $dh=opendir($dir);
    while(false!==($file=readdir($dh)))
    {
            if($file=='.'||$file=='..') continue;
            removeNotEmptyDir($dir."/".$file);
    }
    closedir($dh);
    
    return rmdir($dir);
}
?>



<?php
if(!empty($_POST["templates_action"]))
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
	
	
	
    $templates_list = json_decode($_POST["templates_list"], true);
    
    if($_POST["templates_action_type"] == "set_current")//Установка текущего шаблона
    {
        //1. Выставляем для всех шаблонов "Не текущий"
        $current_off_all_result = $db_link->prepare("UPDATE `templates` SET `current` = 0 WHERE `is_frontend` = ?;")->execute( array($is_frontend) );
		

        //2. Выставляем текущий
		$current_on_result = $db_link->prepare("UPDATE `templates` SET `current` = 1 WHERE `id` = ?;")->execute( array($templates_list[0]) );
		
        if($current_off_all_result == true && $current_on_result == true)
        {
            //Выполнено без ошибок
            $success_message = translate_str_by_id(2722);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/templates_manager?success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit();
        }
        else
        {
            //Возникли ошибки
            $error_message = translate_str_by_id(2912).":";
            if($current_off_all_result != true)
            {
                $error_message .= "<br> ".translate_str_by_id(3847);
            }
            if($current_on_result != true)
            {
                $error_message .= "<br> ".translate_str_by_id(3848);
            }
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/templates_manager?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit();
        }
    }//if($_POST["templates_action_type"] == "set_current")
    else if($_POST["templates_action_type"] == "delete")//Действие - Удаление шаблонов
    {
        //Сначала проверяем наличие в списке текущего шаблона
        for($i=0; $i<count($templates_list); $i++)
        {
			$check_template_query = $db_link->prepare("SELECT * FROM `templates` WHERE `id` = ?;");
			$check_template_query->execute( array($templates_list[$i]) );
            $check_template_record = $check_template_query->fetch();
            if($check_template_record["current"] == true)
            {
                //Проверка не пройдена
                $error_message = translate_str_by_id(3857);
                ?>
                <script>
                    location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/templates_manager?error_message=<?php echo $error_message; ?>";
                </script>
                <?php
                exit();
            }
        }//for($i) - проверка на наличие текущего шаблона
        
        //Проверка пройдена - удаляем
        
        
        //Сначала удаляем каталоги указанных шаблонов:
        for($i=0; $i < count($templates_list); $i++)
        {
			$current_template_query = $db_link->prepare("SELECT * FROM `templates` WHERE `id` = ?;");
			$current_template_query->execute( array($templates_list[$i]) );
            $current_template_record = $current_template_query->fetch();
            
			if( !$current_template_record )
			{
				exit;
			}
			
            $backend_dir = "";//Если работаем с шаблоном бэкэнда
            if(!$is_frontend)
            {
                $backend_dir = $DP_Config->backend_dir."/";//Если работаем с шаблоном бэкэнда
            }
            
            $path = $_SERVER['DOCUMENT_ROOT']."/".$backend_dir."templates/".$current_template_record["name"];
            
            if(file_exists($path))
            {
                removeNotEmptyDir($path);//Рекурсивно удаляем каталог шаблона
            }
        }//for($i) - по всем удаляемым плагинам
        
        
        //Удаление указанных шаблонов из таблицы
        $SQL_DELETE = "DELETE FROM `templates` WHERE ";
        $SQL_SUB_IDS = "";
		$binding_values = array();
        for($i=0; $i < count($templates_list); $i++)
        {
            if($SQL_SUB_IDS != "") $SQL_SUB_IDS .= " OR";
            $SQL_SUB_IDS .= " `id`=?";
			
			array_push($binding_values, $templates_list[$i]);
        }//for($i) - по всем удаляемым шаблонам

        if( $db_link->prepare($SQL_DELETE.$SQL_SUB_IDS)->execute($binding_values) != true)
        {
            //Возникла ошибка
            $error_message = translate_str_by_id(3858);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/templates_manager?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit();
        }
        else//Шаблоны удалены успешно
        {
            //Выполнено без ошибок
            $success_message = translate_str_by_id(3859);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/templates_manager?success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit();
        }
    }//~else if($_POST["templates_action_type"] == "delete")//Действие - Удаление шаблонов
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
				<a class="panel_a" onClick="setCurrent(0);" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/star.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3854); ?></div>
				</a>
				

				<a class="panel_a" onClick="deleteTemplates(0);" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				
				
				<script>
				//Функция установки режима редактирования материалов Фронтэнд/Бэкэнд
				function set_edit_mode(mode)
				{
					$.getJSON("<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/control/set_edit_mode_cookie.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&edit_mode="+encodeURI(mode)+"&callback=?", function(data){
							location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/templates_manager";
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
	
	
	
	
	
	
	<?php
    //Выводим таблицу
    $current_template = 0;//Текущий шаблон
    ?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3860); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead> 
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
								<th><?php echo translate_str_by_id(3861); ?></th>
								<th><?php echo translate_str_by_id(3862); ?></th>
								<th class="text-center"><?php echo translate_str_by_id(3863); ?></th>
							</tr>
						</thead>
						<tbody>
						<?php
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						
						
						$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `templates` WHERE `is_frontend` = ?;");
						$elements_query->execute( array($is_frontend) );
						
						
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
							
							$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/templates/template?template_id=".$element_record["id"]." \">";
						?>
							<tr>
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
								
								<td><?php echo $a_item.$element_record["id"]; ?></a></td>
								<td><?php echo $a_item.$element_record["caption"]; ?></a></td>
								
								<td>
								<?php
								if($element_record["phone_support"] == 1)
								{
									?>
									<img style="max-width:20px" src="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/control/images/check_32.png" />
									<?php
								}
								?>
								</td>
								
								
								
								<td>
								<?php
								if($element_record["tablet_support"] == 1)
								{
									?>
									<img style="max-width:20px" src="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/control/images/check_32.png" />
									<?php
								}
								?>
								</td>
								
								
								
								
								<td class="text-center">
								<?php
								if($element_record["current"] == 1)
								{
									$current_template = $element_record["id"];//Указываем текущий шаблон
									?>
									<img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/star.png" />
									<?php
								}
								else
								{
									?>
									<a href="javascript:void(0);" onclick="setCurrent(<?php echo $element_record["id"]; ?>);"><img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/star_grey.png" /></a>
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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/templates/templates_manager?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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
	
	
	


    
    <!-- START Общая форма для действий менеджера: установка текущего/удаление -->
    <form name="templates_manager_action_form" method="POST" style="display:none">
        <input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
		<input type="hidden" name="templates_action" id="templates_action" value="templates_action" /><!-- Говорит, что есть действие -->
        <input type="hidden" name="templates_action_type" id="templates_action_type" value="" /><!-- Тип действия (Удаление или Установка текущего шаблона) -->
        <input type="hidden" name="templates_list" id="templates_list" value="" />
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none"/>
    </form>
    <!-- END Общая форма для действий менеджера: установка текущего/удаление -->
    
    
    
    
    <!-- START БЛОК УСТАНОВКИ ТЕКУЩЕГО ШАБЛОНА -->
    <script>
    //Функция установки текущего шаблона
    function setCurrent(template)
    {
        if(template == 0)//Если равно 0 - текущим устанавливаем тот шаблон, который отмечен галочкой
        {
            document.getElementById("templates_list").value = JSON.stringify(getCheckedElements());
            if(document.getElementById("templates_list").value == "[]")
            {
                alert("<?php echo translate_str_by_id(3864); ?>");
                return;
            }
            else if(getCheckedElements().length > 1)
            {
                alert("<?php echo translate_str_by_id(3865); ?>");
                return;
            }
        }
        else//Устанавливаем текущим указанный шаблон
        {
            document.getElementById("templates_list").value = "["+template+"]";
        }
        
        document.getElementById("templates_action_type").value = "set_current";//Тип действия - установка текущего шаблона
        
        //Отправляем форму
    	document.forms["templates_manager_action_form"].submit();//Отправляем
    }
    </script>
    <!-- END БЛОК УСТАНОВКИ ТЕКУЩЕГО ШАБЛОНА -->
    
    
    
    
    
    
    
    <!-- START БЛОК УДАЛЕНИЯ ШАБЛОНОВ -->
    <script>
    var current_template = <?php echo $current_template?>;//Текущий шаблон
    //Функция удаления плагинов
    function deleteTemplates(template)
    {
        if(template == 0)//Если равно 0 - удаляем отмеченные галочками шаблоны
        {
            document.getElementById("templates_list").value = JSON.stringify(getCheckedElements());
            if(document.getElementById("templates_list").value == "[]")
            {
                alert("<?php echo translate_str_by_id(3864); ?>");
                return;
            }
        }
        else//Удаляем только один указанный шаблон
        {
            document.getElementById("templates_list").value = "["+template+"]";
        }
        
        //Запрещаем удалять текущий шаблон
        var templates_list = JSON.parse(document.getElementById("templates_list").value);
        for(var i=0; i < templates_list.length; i++)
        {
            if(templates_list[i] == current_template)
            {
                alert("<?php echo translate_str_by_id(3866); ?>");
                return;
            }
        }
        
        
        if(!confirm("<?php echo translate_str_by_id(3867); ?>"))
        {
            return;
        }
        
        document.getElementById("templates_action_type").value = "delete";//Тип действия - удаление
        
        //Отправляем форму
    	document.forms["templates_manager_action_form"].submit();//Отправляем
    }
    </script>
    <!-- END БЛОК УДАЛЕНИЯ ШАБЛОНОВ -->
    
    
    
    
    
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