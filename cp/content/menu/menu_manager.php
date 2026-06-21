<?php
/**
 * Страница менеджера меню
*/
defined('_ASTEXE_') or die('No access');


//Режим редактирования Фронтэнд/Бэкэнд
$edit_mode = null;
if(isset($_COOKIE["edit_mode"]))
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
if(!empty($_POST["menu_action"]))
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
	
	
    //Какие-то действия
    if($_POST["menu_action"] == "delete")
    {
        $SQL_DELETE_MENUS = "DELETE FROM `menu` WHERE ";
        $binding_values = array();
        $menu_list = json_decode($_POST["menu_list"], true);
        for($i=0; $i<count($menu_list); $i++)
        {
            if($i > 0)
            {
                $SQL_DELETE_MENUS .= " OR ";
            }
            $SQL_DELETE_MENUS .= "`id`=?";
			
			array_push($binding_values, $menu_list[$i]);
        }
        

        if( $db_link->prepare($SQL_DELETE_MENUS)->execute( $binding_values ) != true)
        {
            $error_message = translate_str_by_id(2610);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/menu/menu_manager?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
            $success_message = translate_str_by_id(2351);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/menu/menu_manager?success_message=<?php echo $success_message; ?>";
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
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/menu/menu_edit">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<a class="panel_a" href="javascript:void(0);" onclick="delete_menus();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				

				<script>
				//Функция установки режима редактирования меню Фронтэнд/Бэкэнд
				function set_edit_mode(mode)
				{
					$.getJSON("<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/control/set_edit_mode_cookie.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&edit_mode="+encodeURI(mode)+"&callback=?", function(data){
							location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/menu/menu_manager";
						});
				}
				</script>
				
				<?php
				if($is_frontend)
				{
				?>
					<a class="panel_a" onClick="set_edit_mode('backend');" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/backend_edit.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2221); ?></div>
					</a>
				<?php
				}
				else
				{
				?>
					<a class="panel_a" onClick="set_edit_mode('frontend');" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/frontend_edit.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2222); ?></div>
					</a>

				<?php
				}
				?>
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2611); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
							</tr>
						</thead>
						<tbody>
						<?php
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						

						$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `menu` WHERE `is_frontend` = ?;");
						$elements_query->execute( array($is_frontend) );
						
						
						$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
						$elements_count_rows_query->execute();
						$elements_count_rows = $elements_count_rows_query->fetchColumn();
						
						//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
						//---------------------------------------------------------------------------------------------->
						//Определяем количество страниц для вывода:
						$p = $DP_Config->list_page_limit;//Штук на страницу
						$count_pages = (int)( $elements_count_rows / $p);//Количество страниц
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
							
							
							//Ссылка для элемента списка
							$a_item = "<a href=\"/".$DP_Config->backend_dir."/menu/menu_edit?menu_id=".$element_record["id"]."\">";
						?>
							<tr>
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"];?>');" id="checked_<?php echo $element_record["id"];?>" name="checked_<?php echo $element_record["id"];?>"/></td>
								<td>
									<?php echo $a_item.$element_record["id"];?></a>
								</td>
								<td>
									<?php echo $a_item.translate_str_by_id($element_record["caption"]);?></a>
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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/menu/menu_manager?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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
	
	


    
    <form name="delete_menu_form" method="POST" style="display:none">
        <input type="hidden" name="menu_action" value="delete" />
        <input type="hidden" name="menu_list" id="menu_list" value="" />
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none"/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    
    
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
    </script>
    
    
    <script>
    //Функция удаления выбранных меню
    function delete_menus()
    {
        var menus_to_delete = Array();
        
        for(var i=0; i<elements_array.length;i++)
        {
            if(document.getElementById(elements_array[i]).checked == true)
            {
                menus_to_delete.push(elements_id_array[i]);
            }
        }
        
        if(menus_to_delete.length == 0)
        {
            alert("<?php echo translate_str_by_id(2612); ?>");
            return;
        }
        
        if(!confirm("<?php echo translate_str_by_id(2613); ?>")) 
        {
            return;
        }
        
        //Задаем список меню в форме удаления
        document.getElementById("menu_list").value = JSON.stringify(menus_to_delete);
        
        //Отправляем форму
    	document.forms["delete_menu_form"].submit();//Отправляем
    }
    </script>
    
    <?php
}
?>