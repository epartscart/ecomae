<?php
/**
 * Страничный скрипт списка дополнительных текстов для URL
*/
defined('_ASTEXE_') or die('No access');
?>





<?php
if(!empty($_POST["action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	if( $_POST["action"] == "delete" )
	{
		$urls_to_del = $_POST["urls_to_del"];
		$urls_to_del_php = json_decode($urls_to_del, true);
		$binding_values = array();
		$urls_to_del = "";
		for($i=0; $i<count($urls_to_del_php);$i++)
		{
			if($i > 0)
			{
				$urls_to_del = $urls_to_del.",";
			}
			$urls_to_del = $urls_to_del."?";
			
			array_push($binding_values, $urls_to_del_php[$i]);
		}

		
		if( $db_link->prepare("DELETE FROM `text_for_url` WHERE `id` IN ($urls_to_del);")->execute( $binding_values ) != true )
		{
			$error_message = translate_str_by_id(2375);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/content/dopolnitelnye-teksty?error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit();
		}
		else
		{
			$success_message = translate_str_by_id(2376);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/content/dopolnitelnye-teksty?success_message=<?php echo urlencode($success_message); ?>";
			</script>
			<?php
			exit();
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
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/content/dopolnitelnye-teksty/dopolnitelnyj-tekst">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				

				<a class="panel_a" onClick="deleteURLS();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	
	
	<script>
    // ------------------------------------------------------------------------------------------------
    //Установка куки сортировки URL
    function sortURLS(field)
    {
        var asc_desc = "asc";//Направление по умолчанию
        
        //Берем из куки текущий вариант сортировки
        var current_sort_cookie = getCookie("url_sort");
        if(current_sort_cookie != undefined)
        {
            current_sort_cookie = JSON.parse(getCookie("url_sort"));
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
        
        
        var url_sort = new Object;
        url_sort.field = field;//Поле, по которому сортировать
        url_sort.asc_desc = asc_desc;//Направление сортировки
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "url_sort="+JSON.stringify(url_sort)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/content/dopolnitelnye-teksty';
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
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2377); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead> 
							<tr> 
								<th style="width:50px;"><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th style="width:50px;"><a href="javascript:void(0);" onclick="sortURLS('id');" id="id_sorter">ID</a></th>
								<th><a href="javascript:void(0);" onclick="sortURLS('url');" id="url_sorter">URL</a></th>
							</tr>
							<script>
								<?php
								//Определяем текущую сортировку и обозначаем ее:
								$url_sort = $_COOKIE["url_sort"];
								$sort_field = "id";
								$sort_asc_desc = "desc";
								if($url_sort != NULL)
								{
									$url_sort = json_decode($url_sort, true);
									$sort_field = $url_sort["field"];
									$sort_asc_desc = $url_sort["asc_desc"];
								}
								
								//Защита от SQL-инъекций
								if( strtolower($sort_asc_desc) === "asc" )
								{
									$sort_asc_desc = "asc";
								}
								else
								{
									$sort_asc_desc = "desc";
								}
								
								if( array_search( $sort_field, array('id', 'url') ) === false )
								{
									$sort_field = "id";
								}
								
								?>
								document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
							</script>
						</thead>
						<tbody>
						<?php
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						
						$SQL_condition = "";
						$binding_values = array();
						if( !empty($_COOKIE["url_search"]) )
						{
							$url_search = json_decode($_COOKIE["url_search"], true);
							$url = $url_search["url"];
							$exact_search = $url_search["exact_search"];
							if($exact_search)
							{
								$SQL_condition = " WHERE `url` = ? ";
								
								array_push($binding_values, $url);
							}
							else
							{
								$SQL_condition = " WHERE `url` LIKE ? ";
								
								array_push($binding_values, '%'.$url.'%');
							}
						}
						
						
						$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `text_for_url` $SQL_condition ORDER BY `$sort_field` $sort_asc_desc;");
						$elements_query->execute($binding_values);
						
						$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
						$elements_count_rows_query->execute();
						$elements_count_rows = $elements_count_rows_query->fetchColumn();
						
						//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
						//---------------------------------------------------------------------------------------------->
						//Определяем количество страниц для вывода:
						$p = $DP_Config->list_page_limit;//Штук на страницу
						$count_pages = (int)( $elements_count_rows / $p );//Количество страниц
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
							
							$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/content/dopolnitelnye-teksty/dopolnitelnyj-tekst?url=".urlencode($element_record["url"])." \">";
						?>
							<tr>
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"];?>');" id="checked_<?php echo $element_record["id"];?>" name="checked_<?php echo $element_record["id"];?>"/></td>
								
								<td><?php echo $a_item.$element_record["id"];?></a></td>
								<td><?php echo $a_item.$element_record["url"];?></a></td>

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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/content/dopolnitelnye-teksty?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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
			<div class="panel-footer">
				
				<div class="row">
					<?php
					//Инициализируем поля
					$url = "";
					$exact_search = 0;
					if( !empty($_COOKIE["url_search"]) )
					{
						$url_search = json_decode($_COOKIE["url_search"], true);
						$url = $url_search["url"];
						$exact_search = $url_search["exact_search"];
					}
					//Отмечаем чекбокс точного поиска
					$exact_search_checked = "";
					if( $exact_search == 1 )
					{
						$exact_search_checked = " checked='checked' ";
					}
					?>
				
					<div class="col-lg-6">
						<input placeholder="Поиск страницы по URL..." type="text" class="form-control" name="url_search" id="url_search" value="<?php echo $url; ?>" />
					</div>
					
					<div class="col-lg-2">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label text-right">
								<?php echo translate_str_by_id(2378); ?>
							</label>
							<div class="col-lg-6">
								<input type="checkbox" name="exact_search" id="exact_search" value="exact_search" <?php echo $exact_search_checked; ?> />
							</div>
						</div>
					</div>
					
					
					<div class="col-lg-2">
						<button onclick="searchURL();" class="btn btn-success " type="button"><i class="fa fa-search"></i> <span class="bold"><?php echo translate_str_by_id(2379); ?></span></button>
					</div>
					<div class="col-lg-2">
						<button onclick="searchURL_cancel();" class="btn btn-danger " type="button"><i class="fa fa-square-o"></i> <span class="bold"><?php echo translate_str_by_id(2380); ?></span></button>
					</div>
					
					<script>
						// ---------------------------------------------------------------
						function searchURL()
						{
							var url_search = new Object;
							url_search.url = document.getElementById("url_search").value;
							if(document.getElementById("exact_search").checked == true)
							{
								url_search.exact_search = 1;
							}
							else
							{
								url_search.exact_search = 0;
							}
							
							
							
							//Устанавливаем cookie (на полгода)
							var date = new Date(new Date().getTime() + 15552000 * 1000);
							document.cookie = "url_search="+JSON.stringify(url_search)+"; path=/; expires=" + date.toUTCString();
							

							//Обновляем страницу
							location='/<?php echo $DP_Config->backend_dir; ?>/content/dopolnitelnye-teksty';
						}
						// ---------------------------------------------------------------
						function searchURL_cancel()
						{
							//Устанавливаем cookie в прошлом
							var date = new Date(new Date().getTime() - 10000);
							document.cookie = "url_search="+JSON.stringify(url_search)+"; path=/; expires=" + date.toUTCString();
							
							//Обновляем страницу
							location='/<?php echo $DP_Config->backend_dir; ?>/content/dopolnitelnye-teksty';
						}
						// ---------------------------------------------------------------
					</script>
				</div>

			</div>
		</div>
	</div>
	
	
	
	
	<!-- Start Удаление текстов -->
	<form method="POST" name="urls_to_del_form">
		<input type="hidden" name="action" value="delete" />
		<input type="hidden" name="urls_to_del" id="urls_to_del" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	function deleteURLS()
	{
		var urls_to_del = getCheckedElements();
		
		if( urls_to_del.length == 0 )
		{
			alert("<?php echo translate_str_by_id(2381); ?>");
			return;
		}
		
		if( ! confirm("<?php echo translate_str_by_id(2382); ?>") )
		{
			return;
		}
		
		document.getElementById("urls_to_del").value = JSON.stringify(urls_to_del);
		
		document.forms["urls_to_del_form"].submit();
	}
	</script>
	<!-- End Удаление текстов -->
	
	
	

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