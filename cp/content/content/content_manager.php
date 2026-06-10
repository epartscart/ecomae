<?php
//Страничный скрипт вывода всех материалов в виде таблицы
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
// ----------------------------------------------------------------------------------------
//Получение отформатированной строки со временем и датой
function get_date_time_good_string($time)
{
	if( $time == 0)
	{
		return translate_str_by_id(2203);
	}
	
	return date("d.m.Y H:i:s", $time); 
}
// ----------------------------------------------------------------------------------------
//Рекурсивная функция получения списка всех вложенных материалов
function get_child_nodes($content_id, $list)
{
	global $db_link;
	global $is_frontend;
	
	//Получаем количество вложенных элементов данного узла
	$content_data_query = $db_link->prepare("SELECT `count` FROM `content` WHERE `id` = ? AND `is_frontend` = ?;");
	if( ! $content_data_query->execute( array($content_id, $is_frontend) ) )
	{
		return false;
	}
	$content_data_record = $content_data_query->fetch();
	if( $content_data_record == false )
	{
		return false;
	}
	$count = $content_data_record["count"];
	//Если вложенных нет
	if( $count == 0 )
	{
		return $list;
	}
	
	
	//Получаем список вложенных узлов
	$child_nodes_query = $db_link->prepare("SELECT `id`,`count` FROM `content` WHERE `parent` = ? AND `is_frontend` = ?;");
	if( ! $child_nodes_query->execute( array($content_id, $is_frontend) ) )
	{
		return false;
	}
	while( $child_node = $child_nodes_query->fetch() )
	{
		$child_id = $child_node["id"];
		$child_count = $child_node["count"];
		
		//Добавляем узел в список
		array_push($list, (int)$child_id);
		
		
		//Рекурсивный вызов для вложенных узлов
		if( $child_count > 0 )
		{
			//Получаем список вложенных узлов
			$list = get_child_nodes($child_id, $list);
			
			//Если была ошибка хотя бы на одном узле - возращаем false
			if( ! $list )
			{
				return false;
			}
		}
	}
	
	return $list;
}
// ----------------------------------------------------------------------------------------
// ДЕЙСТВИЯ
if( !empty( $_POST["action"] ) )
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
	
	
	//Действие - установить материал главным
	if( $_POST["action"] == "set_main_flag" )
	{
		try
		{
			//Меняем статус autocommit на FALSE. Т.е. старт транзакции
			if( ! $db_link->beginTransaction() )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			$content_id = $_POST["content_id"];
			
			//Проверяем уровень материала. Он не должен быть вложенным и не должен быть снятым с публикации.
			$content_query = $db_link->prepare('SELECT `level`, `main_flag`, `published_flag` FROM `content` WHERE `id` = ? AND `is_frontend` = ?;');
			if( $content_query->execute( array($content_id, $is_frontend) ) == false )
			{
				throw new Exception(translate_str_by_id(2144));
			}
			$content_record = $content_query->fetch();
			
			//Проверка существования материала
			if( $content_record == false )
			{
				throw new Exception(translate_str_by_id(2205));
			}
			
			$content_level = (int)$content_record["level"];
			$content_main_flag = $content_record["main_flag"];
			$content_published_flag = $content_record["published_flag"];
			
			if( $content_level > 1 )
			{
				throw new Exception(translate_str_by_id(2206));
			}
			
			
			if( $content_published_flag == 0 )
			{
				throw new Exception(translate_str_by_id(2207));
			}
			
			
			
			if( $content_main_flag == true )
			{
				$db_link->rollBack();
				//Инфо
				$info_message = urlencode(translate_str_by_id(2208));
				?>
				<script>
					location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?info_message=<?php echo $info_message; ?>";
				</script>
				<?php
				exit;
			}
			
			//Ставим этот материал главным
			if( ! $db_link->prepare('UPDATE `content` SET `main_flag` = 1 WHERE `id` = ?;')->execute( array($content_id) ) )
			{
				throw new Exception(translate_str_by_id(2209));
			}
			
			//Снимаем предыдущий главный материал
			if( ! $db_link->prepare('UPDATE `content` SET `main_flag` = 0 WHERE `id` != ? AND `is_frontend` = ? AND `main_flag` = 1;')->execute( array($content_id, $is_frontend) ) )
			{
				throw new Exception(translate_str_by_id(2210));
			}
		}
		catch (Exception $e)
		{
			$db_link->rollBack();
			//Ошибка
			$error_message = urlencode($e->getMessage().". ".translate_str_by_id(2142));
			?>
			<script>
				location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}


		//Дошли сюда - значит все запросы выполнены без ошибок
		$db_link->commit();
		
		
		//Выполнено успешно
		$success_message = urlencode(translate_str_by_id(2157));
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}
	//Действие - публикация или снятие с публикации
	else if( $_POST["action"] == "set_published_flag" )
	{
		try
		{
			//Меняем статус autocommit на FALSE. Т.е. старт транзакции
			if( ! $db_link->beginTransaction() )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			
			$published_flag = (int)$_POST["published_flag"];
			$content_array = $_POST["content_array"];//JSON - строка
			$content_array = json_decode($content_array, true);
			$bindingValues = array();
			$content_array_str = "";
			for( $i=0; $i < count($content_array) ; $i++ )
			{
				if( $i > 0 )
				{
					$content_array_str = $content_array_str.",";
				}
				$content_array_str = $content_array_str."?";
				
				array_push($bindingValues, $content_array[$i]);
			}
			
			if( $published_flag == 0 )
			{
				//Проверяем, нет ли среди материалов главного - его нельзя снять с публикации
				$check_content_query = $db_link->prepare('SELECT COUNT(*) FROM `content` WHERE `main_flag` = 1 AND `id` IN ('.$content_array_str.');');
				if( ! $check_content_query->execute($bindingValues) )
				{
					throw new Exception(translate_str_by_id(2211));
				}
				if( $check_content_query->fetchColumn() > 0 )
				{
					throw new Exception(translate_str_by_id(2212));
				}
			}
			
			
			//Защита от изменения и удаления системных материалов
			if( $DP_Config->allow_edit_system_content != true )
			{
				$check_content_query = $db_link->prepare('SELECT COUNT(*) FROM `content` WHERE `system_flag` = 1 AND `id` IN ('.$content_array_str.');');
				if( ! $check_content_query->execute($bindingValues) )
				{
					throw new Exception(translate_str_by_id(2213));
				}
				if( $check_content_query->fetchColumn() > 0 )
				{
					throw new Exception(translate_str_by_id(2214));
				}
			}
			
			
			//Устанавливаем флаг
			array_unshift($bindingValues, $published_flag);
			if( ! $db_link->prepare('UPDATE `content` SET `published_flag` = ? WHERE `id` IN ('.$content_array_str.');')->execute( $bindingValues ) )
			{
				throw new Exception(translate_str_by_id(2215));
			}
			
		}
		catch (Exception $e)
		{
			$db_link->rollBack();
			//Ошибка
			$error_message = urlencode($e->getMessage().". ".translate_str_by_id(2142));
			?>
			<script>
				location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}


		//Дошли сюда - значит все запросы выполнены без ошибок
		$db_link->commit();
		//Выполнено успешно
		$success_message = urlencode(translate_str_by_id(2157));
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}
	//Действие - удаление материалов
	else if( $_POST["action"] == "delete_content" )
	{
		try
		{
			//Меняем статус autocommit на FALSE. Т.е. старт транзакции
			if( ! $db_link->beginTransaction() )
			{
				throw new Exception(translate_str_by_id(2132));
			}

			//Список материалов на удаление
			$list = json_decode($_POST["content_array"], true);//JSON - строка
			
			//Приведем все значения к int
			for($i = 0 ; $i < count($list) ; $i++)
			{
				$list[$i] = (int)$list[$i];
			}
			
			
			//Теперь заполняем список вложенными узлами
			$start_list_count = count($list);
			for($i = 0 ; $i < $start_list_count ; $i++)
			{
				$list = get_child_nodes($list[$i], $list);
				if( $list == false )
				{
					throw new Exception(translate_str_by_id(2216));
				}
			}
			
			//Фильтруем повторяющиеся значения
			$list_unique = array();
			for($i = 0 ; $i < count($list) ; $i++)
			{
				if( array_search($list[$i], $list_unique) === false )
				{
					array_push($list_unique, $list[$i]);
				}
			}
			$list = $list_unique;
			
			
			//Далее выполнить действия
			
			//Провеям наличие главного материала в списке
			/*$list = json_encode($list);
			$list = str_replace("[","(",$list);
			$list = str_replace("]",")",$list);*/
			
			
			$bindingValues = array();
			$list_str = "";
			for( $i=0; $i < count($list) ; $i++ )
			{
				if( $i > 0 )
				{
					$list_str = $list_str.",";
				}
				$list_str = $list_str."?";
				
				array_push($bindingValues, $list[$i]);
			}
			
			
			$check_list_query = $db_link->prepare('SELECT COUNT(*) FROM `content` WHERE `main_flag` = 1 AND `id` IN ('.$list_str.');');
			if( ! $check_list_query->execute($bindingValues) )
			{
				throw new Exception(translate_str_by_id(2211));
			}
			if( $check_list_query->fetchColumn() > 0 )
			{
				throw new Exception(translate_str_by_id(2217));
			}
			
			
			
			//Защита от изменения и удаления системных материалов
			if( $DP_Config->allow_edit_system_content != true )
			{
				$check_content_query = $db_link->prepare('SELECT COUNT(*) FROM `content` WHERE `system_flag` = 1 AND `id` IN ('.$list_str.');');
				if( ! $check_content_query->execute($bindingValues) )
				{
					throw new Exception(translate_str_by_id(2213));
				}
				if( $check_content_query->fetchColumn() > 0 )
				{
					throw new Exception(translate_str_by_id(2214));
				}
			}
			
			
			
			
			//Далее можем удалять
			if( ! $db_link->prepare('DELETE FROM `content` WHERE `id` IN ('.$list_str.');')->execute( $bindingValues ) )
			{
				throw new Exception(translate_str_by_id(2218));
			}
			
			//Удаляем права доступа
			if( ! $db_link->prepare('DELETE FROM `content_access` WHERE `content_id` IN ('.$list_str.');')->execute( $bindingValues ) )
			{
				throw new Exception(translate_str_by_id(2219));
			}
			
		}
		catch (Exception $e)
		{
			$db_link->rollBack();
			//Ошибка
			$error_message = urlencode($e->getMessage().". ".translate_str_by_id(2142));
			?>
			<script>
				location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}


		//Дошли сюда - значит все запросы выполнены без ошибок
		$db_link->commit();

		//Выполнено успешно
		$success_message = urlencode(translate_str_by_id(2157));
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
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

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>/content/content_manager/content">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2220); ?></div>
				</a>
				
				
				<a class="panel_a" href="javascript:void(0);" onClick="set_main_flag();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/star.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2177); ?></div>
				</a>
				
				<script>
				//Функция установки режима редактирования материалов Фронтэнд/Бэкэнд
				function set_edit_mode(mode)
				{
					//При смене режима редактирования - сбрасываем все куки менеджера материалов:
					cookie_to_default();//Сброс всех куки менеджера материалов
					
					$.getJSON("<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/control/set_edit_mode_cookie.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&edit_mode="+encodeURI(mode)+"&callback=?", function(data){
							location = location;
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
				
				
				
				<a class="panel_a" href="javascript:void(0);" onClick="set_published_flag_action(1);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/public.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2178); ?></div>
				</a>
				
				
				<a class="panel_a" href="javascript:void(0);" onClick="set_published_flag_action(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/public_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2223); ?></div>
				</a>
				
				
				
				
				<a class="panel_a" href="javascript:void(0);" onClick="delete_action();">
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
	//Сброс всех куки менеджера материалов
	function cookie_to_default()
	{
		var date = new Date(new Date().getTime() - 15552000 * 1000);//Прошедшее время (для удаления)
		document.cookie = "content_manager_mode=0; path=/; expires=" + date.toUTCString();
		document.cookie = "content_filter=0; path=/; expires=" + date.toUTCString();
		document.cookie = "content_manager_page=0; path=/; expires=" + date.toUTCString();
		document.cookie = "content_sort=0; path=/; expires=" + date.toUTCString();
	}
	</script>
	
	
	
	
	
	<?php
	//Выводим фильтры
	$content_manager_mode = "hierarchy";
	$content_manager_mode_cookie = null;
	if( isset($_COOKIE["content_manager_mode"]) )
	{
		$content_manager_mode_cookie = $_COOKIE["content_manager_mode"];
	}
	switch($content_manager_mode_cookie)
	{
		case "hierarchy":
			$content_manager_mode = "hierarchy";//Иерархическое отображение материалов
			break;
		case "direct_list":
			$content_manager_mode = "direct_list";//Примой табличный список с сортировкой и поиском
			break;
		default:
			$content_manager_mode = "hierarchy";
			break;
	}
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2117); ?>
			</div>
			<div class="panel-body">
				<?php
				if( $content_manager_mode == "hierarchy" )
				{
					?>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2225); ?>
						</label>
						<div class="col-lg-6">
							<button type="button" class="btn w-xs btn-success" onClick="change_content_manager_mode('direct_list');"><?php echo translate_str_by_id(2226); ?></button>
						</div>
					</div>
					<?php
				}
				else//Прямая таблица с сортировкой и поиском
				{
					$id_filter = "";
					$content_content_filter = "";
					$meta_data_filter = "";
					//Получаем текущие значения фильтра:
					$content_filter = NULL;
					if( isset($_COOKIE["content_filter"]) )
					{
						$content_filter = $_COOKIE["content_filter"];
					}
					if($content_filter != NULL)
					{
						$content_filter = json_decode($content_filter, true);
						$id_filter = $content_filter["id"];
						$content_content_filter = $content_filter["content_content"];
						$meta_data_filter = $content_filter["meta_data"];
					}
					
					?>
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2227); ?>
						</label>
						<div class="col-lg-6">
							<button type="button" class="btn w-xs btn-success" onClick="change_content_manager_mode('hierarchy');"><?php echo translate_str_by_id(2228); ?></button>
						</div>
					</div>
					
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2229); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" id="id_filter_input" value="<?php echo $id_filter; ?>" class="form-control" />
						</div>
					</div>
					
					
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2230); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" id="content_content_filter_input" value="<?php echo $content_content_filter; ?>" class="form-control" />
						</div>
					</div>
					
					
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2231); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" id="meta_data_filter_input" value="<?php echo $meta_data_filter; ?>" class="form-control" />
						</div>
					</div>
					<?php
				}
				?>
			</div>
			
			<?php
			//Для линейного отображения - выводим кнопки для фильров
			if( $content_manager_mode == "direct_list" )
			{
				?>
				<div class="panel-footer">
					<button class="btn btn-success" type="button" onclick="filterContent();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
					<button class="btn btn-primary" type="button" onclick="unsetFilterContent();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2233); ?></button>
				</div>
				<script>
				// ------------------------------------------------------------------------------------------------
				//Установка cookie в соответствии с фильтром
				function filterContent()
				{
					var content_filter = new Object;
					//Устанавливаем значения объекта фильтра
					content_filter.id = document.getElementById("id_filter_input").value;
					content_filter.content_content = document.getElementById("content_content_filter_input").value;
					content_filter.meta_data = document.getElementById("meta_data_filter_input").value;
					
					//Устанавливаем cookie
					document.cookie = "content_filter="+JSON.stringify(content_filter)+"; path=/;";
					
					//При изменении настроек - номер страницы ставим 0
					document.cookie = "content_manager_page="+JSON.stringify(0)+"; path=/;";
					
					//Обновляем страницу
					location=location;
				}
				// ------------------------------------------------------------------------------------------------
				//Снять все фильтры
				function unsetFilterContent()
				{					
					//Время в прошлом (для удаления куки)
					var date = new Date(new Date().getTime() - 15552000 * 1000);
					document.cookie = "content_filter=ok; path=/; expires=" + date.toUTCString();
					
					//При изменении настроек - номер страницы ставим 0
					document.cookie = "content_manager_page="+JSON.stringify(0)+"; path=/;";
					
					//Обновляем страницу
					location=location;
				}
				// ------------------------------------------------------------------------------------------------
				</script>
				<?php
			}
			?>
		</div>
	</div>
	<script>
	function change_content_manager_mode(mode)
	{
		//Перед сменой режима отображения, сбрасывам все куки менеджера материалов
		cookie_to_default();
		
        document.cookie = "content_manager_mode="+mode+"; path=/;";
		
		location=location;
	}
	</script>
	
	
	
	
	
	
	
	
	<?php
	//Javascript для режима "Линейный"
	if( $content_manager_mode == "direct_list" )
	{
		?>
		<script>
		// ------------------------------------------------------------------------------------------------
		//Установка куки сортировки
		function sortContent(field)
		{
			var asc_desc = "asc";//Направление по умолчанию
			
			//Берем из куки текущий вариант сортировки
			var current_sort_cookie = getCookie("content_sort");
			if(current_sort_cookie != undefined)
			{
				current_sort_cookie = JSON.parse(getCookie("content_sort"));
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
			
			
			var content_sort = new Object;
			content_sort.field = field;//Поле, по которому сортировать
			content_sort.asc_desc = asc_desc;//Направление сортировки
			
			//Устанавливаем cookie
			document.cookie = "content_sort="+JSON.stringify(content_sort)+"; path=/;";
			
			//При смене сортировки, можно открыть с первой страницы (хотя, кому, как, больше нравится...)
			//document.cookie = "content_manager_page=0; path=/;";
			
			//Обновляем страницу
			location=location;
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
		<?php
	}
	?>
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2234); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table id="your_table_id" cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<?php
							if( $content_manager_mode == "hierarchy" )
							{
								//Без сортировки
								?>
								<tr>
									<th><?php echo translate_str_by_id(2235); ?></th>
									<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
									<th>ID</th>
									<th><?php echo translate_str_by_id(2236); ?></th>
									<th><?php echo translate_str_by_id(2167); ?></th>
									<th><?php echo translate_str_by_id(2237); ?></th>
									<th><?php echo translate_str_by_id(2238); ?></th>
									<th><?php echo translate_str_by_id(2239); ?></th>
									<th><?php echo translate_str_by_id(2240); ?></th>
									<th class="text-center"><?php echo translate_str_by_id(2241); ?></th>
									<th><?php echo translate_str_by_id(2242); ?></th>
									<th><?php echo translate_str_by_id(2243); ?></th>
								</tr>
								<?php
							}
							else
							{
								//С сортировкой
								?>
								<tr>
									<th><?php echo translate_str_by_id(2235); ?></th>
									<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
									<th><a href="javascript:void(0);" onclick="sortContent('id');" id="id_sorter">ID</a></th>
									<th><a href="javascript:void(0);" onclick="sortContent('value');" id="value_sorter"><?php echo translate_str_by_id(2236); ?></a></th>
									<th><a href="javascript:void(0);" onclick="sortContent('title_tag');" id="title_tag_sorter"><?php echo translate_str_by_id(2167); ?></a></th>
									<th><a href="javascript:void(0);" onclick="sortContent('description_tag');" id="description_tag_sorter"><?php echo translate_str_by_id(2237); ?></a></th>
									<th><a href="javascript:void(0);" onclick="sortContent('content_type');" id="content_type_sorter"><?php echo translate_str_by_id(2238); ?></a></th>
									<th><a href="javascript:void(0);" onclick="sortContent('parent');" id="parent_sorter"><?php echo translate_str_by_id(2239); ?></a></th>
									<th><a href="javascript:void(0);" onclick="sortContent('level');" id="level_sorter"><?php echo translate_str_by_id(2240); ?></a></th>
									<th class="text-center"><?php echo translate_str_by_id(2241); ?></th>
									<th><a href="javascript:void(0);" onclick="sortContent('time_created');" id="time_created_sorter"><?php echo translate_str_by_id(2242); ?></a></th>
									<th><a href="javascript:void(0);" onclick="sortContent('time_edited');" id="time_edited_sorter"><?php echo translate_str_by_id(2243); ?></a></th>
								</tr>
								<?php
							}
							?>	
						</thead>
						<tbody>
						<?php
						
						//Получаем номер страницы
						if( ! isset($_COOKIE["content_manager_page"]) )
						{
							$s_page = 0;
						}
						else
						{
							$s_page = $_COOKIE["content_manager_page"];
						}
						
						//Количество элементов списка:
						$list_page_limit = (int)$DP_Config->list_page_limit;
						
						//Начать с - для LIMIT в SQL-запров
						$from = (int)$s_page*$list_page_limit;
						
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						
						//Далее логика вывода зависит от режима отображения
						if( $content_manager_mode == "hierarchy" )
						{
							//ИЕРАРИХИЧЕСКИЙ РЕЖИМ
							$db_link->prepare('SET SQL_BIG_SELECTS=1;')->execute();
							
							//Получаем максимальный уровень вложенности материалов
							$max_level_query = $db_link->prepare('SELECT MAX(`level`) AS `max_level` FROM `content` WHERE `is_frontend`=?;');
							$max_level_query->execute( array($is_frontend) );
							$max_level = $max_level_query->fetchColumn();
							
							//Формируем SQL-запрос для получения записей в виде древовидной структуры
							$SQL = "SELECT ";
							$SQL_fields = "";
							$SQL_joins = "";
							for($l=1; $l <= $max_level; $l++)
							{
								if( $l > 1 )
								{
									$SQL_fields = $SQL_fields.",";
									
									$l_last = $l -1;
									
									$SQL_joins = $SQL_joins." LEFT JOIN `content` AS `t$l` ON `t$l`.`parent` = `t$l_last`.`id` ";
								}
								
								
								$SQL_fields = $SQL_fields."
								`t$l`.`id` AS `l".$l."_id`,
								`t$l`.`value` AS `l".$l."_value`,
								`t$l`.`description_tag` AS `l".$l."_description_tag`,
								`t$l`.`level` AS `l".$l."_level`,
								`t$l`.`title_tag` AS `l".$l."_title_tag`,
								`t$l`.`content_type` AS `l".$l."_content_type`,
								`t$l`.`main_flag` AS `l".$l."_main_flag`,
								`t$l`.`published_flag` AS `l".$l."_published_flag`,
								`t$l`.`parent` AS `l".$l."_parent`,
								`t$l`.`system_flag` AS `l".$l."_system_flag`,
								`t$l`.`time_created` AS `l".$l."_time_created`,
								`t$l`.`time_edited` AS `l".$l."_time_edited`";
							}
							//Собираем строку запроса
							$SQL = $SQL.$SQL_fields." FROM `content` AS `t1` ".$SQL_joins." WHERE `t1`.`parent` =0 AND `t1`.`is_frontend`=? LIMIT $from, $list_page_limit";
							
							
							
							//Общее количество получаем точно также, как при линейном способе отображения, только без дополнительных условий
							$SQL_count_total = "SELECT COUNT(`id`) AS `count_total` FROM `content` WHERE `is_frontend`=?;";
							$count_total_query = $db_link->prepare($SQL_count_total);
							$count_total_query->execute( array($is_frontend) );
							$count_total_record = $count_total_query->fetch();
							$count_total = $count_total_record["count_total"];
							
							
							//Еще нужно получить общее количество - для вывода переключателей страниц (для иерархического режима есть особенность - $count_total и $count_total_for_pagination НЕ РАВНЫ)
							$SQL_count_total_for_pagination = "SELECT COUNT(`t1`.`id`) AS `count_total` FROM `content` AS `t1` ".$SQL_joins." WHERE `t1`.`parent` =0 AND `t1`.`is_frontend`=?;";
							$count_total_for_pagination_query = $db_link->prepare($SQL_count_total_for_pagination);
							$count_total_for_pagination_query->execute( array($is_frontend) );
							$count_total_for_pagination_record = $count_total_for_pagination_query->fetch();
							$count_total_for_pagination = $count_total_for_pagination_record["count_total"];
							
							
							
							
							

							//echo $SQL;
							//Выполняем запрос
							$elements_query = $db_link->prepare($SQL);
							$elements_query->execute( array($is_frontend) );
							$already_shown = array();//Фильтр - для уже показанных материалов
							$pp = 0;//Порядковый номер на странице
							while( $element_record = $elements_query->fetch() )
							{
								for($l=1; $l <= $max_level; $l++)
								{
									if($element_record["l".$l."_id"] == NULL)
									{
										break;//К следующей ветке
									}
									
									//Такой узел уже был показан выше
									if( array_search((int)$element_record["l".$l."_id"], $already_shown) === false )
									{
										array_push($already_shown, (int)$element_record["l".$l."_id"]);
									}
									else
									{
										continue;
									}
									
									//Для Javascript
									$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["l".$l."_id"]."\";\n";//Добавляем элемент для JS
									$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["l".$l."_id"].";\n";//Добавляем элемент для JS

									//Добавляем обозначение вложенности
									for($lev=1; $lev < $element_record["l".$l."_level"]; $lev++)
									{
										$element_record["l".$l."_value"] = "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;".$element_record["l".$l."_value"];
									}
									
									
									$a_item = "<a href=\"/".$DP_Config->backend_dir."/content/content_manager/content?content_id=".$element_record["l".$l."_id"]."\">";
									
									//Обозначение главного материала
									$font="";
									$font_="";
									$tr_style = "";
									if( $element_record["l".$l."_main_flag"] == true )
									{
										$font="<font style=\"font-weight:bold;color:#000;\" title=\"".translate_str_by_id(2244)."\" >";
										$font_="</font>";
										
										$tr_style = " background-color:#62cb31!important; ";
									}
									if( $element_record["l".$l."_published_flag"] == false )
									{
										$font="<font style=\"font-weight:bold;color:#CCC;\" title=\"".translate_str_by_id(2245)."\" >";
										$font_="</font>";
									}
									
									$pp++;
									
									//Здесь value содержит пробелы для обозначения иерархии, поэтому, для мультиязычности обрабатываем дополнительно
									$l_i_value = str_replace('&nbsp;', '', $element_record["l".$l."_value"]);//Остается str_key
									$lang_str_key = $l_i_value;//Запомнили str_key
									$l_i_value = translate_str_by_id($lang_str_key);//Перевод на текущий язык
									$element_record["l".$l."_value"] = str_replace($lang_str_key, $l_i_value, $element_record["l".$l."_value"]);
									?>
									<tr style="<?php echo $tr_style; ?>">
										<td><?php echo $pp; ?></td>
										<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["l".$l."_id"]; ?>');" id="checked_<?php echo $element_record["l".$l."_id"]; ?>" name="checked_<?php echo $element_record["l".$l."_id"]; ?>"/></td>
										<td><?php echo $a_item.$font.$element_record["l".$l."_id"].$font_; ?></a></td>
										
									
										<td class="value_td"><?php echo $a_item.$font.$element_record["l".$l."_value"].$font_; ?></a></td>
										<td><?php echo $a_item.$font.translate_str_by_id($element_record["l".$l."_title_tag"]).$font_; ?></a></td>
										<td><?php echo $a_item.$font.translate_str_by_id($element_record["l".$l."_description_tag"]).$font_; ?></a></td>
										
										
										<td><?php echo $a_item.$font.strtoupper($element_record["l".$l."_content_type"]).$font_; ?></a></td>
										
										<td><?php echo $a_item.$font.$element_record["l".$l."_parent"].$font_; ?></a></td>
										<td><?php echo $a_item.$font.$element_record["l".$l."_level"].$font_; ?></a></td>
										
										<td class="text-center">
											<?php
											if( $element_record["l".$l."_system_flag"] == 1 )
											{
												?>
												<i class="fas fa-cog"></i>
												<?php
											}
											?>
										</td>
										
										<td><?php echo $a_item.$font.get_date_time_good_string($element_record["l".$l."_time_created"]).$font_; ?></a></td>
										<td><?php echo $a_item.$font.get_date_time_good_string($element_record["l".$l."_time_edited"]).$font_; ?></a></td>
										
									</tr>
								<?php
								}
							}//for
						}
						else
						{
							//ЛИНЕЙНЫЙ РЕЖИМ
							
							
							//Определяем текущую сортировку и обозначаем ее:
							$content_sort = null;
							if(isset($_COOKIE["content_sort"]))
							{
								$content_sort = $_COOKIE["content_sort"];
							}
							$sort_field = "id";
							$sort_asc_desc = "asc";
							if($content_sort != NULL)
							{
								$content_sort = json_decode($content_sort, true);
								$sort_field = $content_sort["field"];
								$sort_asc_desc = $content_sort["asc_desc"];
							}
							
							//Защита от SQL-инъекций
							if( array_search( $sort_field, array('id','value','title_tag','description_tag','content_type','parent','level','time_created','time_edited') ) === false )
							{
								$sort_field = 'id';
							}
							if( strtolower($sort_asc_desc) == "asc" )
							{
								$sort_asc_desc = "asc";
							}
							else
							{
								$sort_asc_desc = "desc";
							}
							
							
							//Для сортировки с учетом мультиязычности. Поля: value, title_tag, description_tag
							if( array_search($sort_field, array('value', 'title_tag', 'description_tag') ) !== false )
							{
								$sort_field = $sort_field."_lang";
							}
							
							
							
							//По куки фильтра:
							$bindingValues = array();
							array_push($bindingValues, $is_frontend);
							$WHERE_CONDITIONS = "";
							$content_filter = NULL;
							if( isset($_COOKIE["content_filter"]) )
							{
								$content_filter = $_COOKIE["content_filter"];
							}
							if($content_filter != NULL)
							{
								$content_filter = json_decode($content_filter, true);
								

								if($content_filter["id"] != "")
								{
									$WHERE_CONDITIONS .= " AND `id` = ? ";
									
									array_push($bindingValues, $content_filter["id"]);
								}
								
								if($content_filter["content_content"] != "")
								{
									$WHERE_CONDITIONS .= " AND `content` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?) ";
									
									array_push($bindingValues, '%'.$content_filter["content_content"].'%');
								}
								
								if($content_filter["meta_data"] != "")
								{
									$WHERE_CONDITIONS .= " AND ( `title_tag` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?) OR `description_tag` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?) OR `keywords_tag` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?) OR `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?) ) ";
									
									array_push($bindingValues, '%'.$content_filter["meta_data"].'%');
									array_push($bindingValues, '%'.$content_filter["meta_data"].'%');
									array_push($bindingValues, '%'.$content_filter["meta_data"].'%');
									array_push($bindingValues, '%'.$content_filter["meta_data"].'%');
								}
							}
							
							
							//Формируем SQL-запрос для получения записей в виде древовидной структуры
							$SQL = "SELECT *,
							(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = `content`.`value` AND `lang_code` = '".$multilang_params['lang']."' ) AS `value_lang`,
							(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = `content`.`title_tag` AND `lang_code` = '".$multilang_params['lang']."') AS `title_tag_lang`,
							(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = `content`.`description_tag` AND `lang_code` = '".$multilang_params['lang']."') AS `description_tag_lang`
							FROM `content` WHERE `is_frontend`=? ".$WHERE_CONDITIONS." ORDER BY `".$sort_field."` ".$sort_asc_desc." LIMIT ".(int)$from.", ".(int)$list_page_limit;
							
							
							//Еще нужно получить общее количество - для вывода переключателей страниц
							$SQL_count_total = "SELECT COUNT(`id`) AS `count_total` FROM `content` WHERE `is_frontend`=? ".$WHERE_CONDITIONS;
							
							$count_total_query = $db_link->prepare($SQL_count_total);
							$count_total_query->execute($bindingValues);
							$count_total_record = $count_total_query->fetch();
							$count_total = $count_total_record["count_total"];
							
							
							//Для линейного режима они равны
							$count_total_for_pagination = $count_total;
							

							//echo $SQL;
							//Выполняем запрос
							$elements_query = $db_link->prepare($SQL);
							$elements_query->execute($bindingValues);
							$pp = 0;//Порядковый номер на странице
							while( $element_record = $elements_query->fetch() )
							{
								//Для Javascript
								$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
								$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS

								
								$a_item = "<a href=\"/".$DP_Config->backend_dir."/content/content_manager/content?content_id=".$element_record["id"]."\">";
								
								
								//Обозначение главного материала
								$font="";
								$font_="";
								$tr_style = "";
								if( $element_record["main_flag"] == true )
								{
									$font="<font style=\"font-weight:bold;color:#000;\" title=\"".translate_str_by_id(2244)."\" >";
									$font_="</font>";
									
									$tr_style = " background-color:#62cb31!important; ";
								}
								if( $element_record["published_flag"] == false )
								{
									$font="<font style=\"font-weight:bold;color:#CCC;\" title=\"".translate_str_by_id(2245)."\" >";
									$font_="</font>";
								}
								
								$pp++;
								?>
								<tr style="<?php echo $tr_style; ?>">
									<td><?php echo $pp; ?></td>
									<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
									<td><?php echo $a_item.$font.$element_record["id"].$font_; ?></a></td>
									
								
									<td class="value_td"><?php echo $a_item.$font.translate_str_by_id($element_record["value"]).$font_; ?></a></td>
									<td><?php echo $a_item.$font.translate_str_by_id($element_record["title_tag"]).$font_; ?></a></td>
									<td><?php echo $a_item.$font.translate_str_by_id($element_record["description_tag"]).$font_; ?></a></td>
									
									
									<td><?php echo $a_item.$font.strtoupper($element_record["content_type"]).$font_; ?></a></td>
									
									<td><?php echo $a_item.$font.$element_record["parent"].$font_; ?></a></td>
									<td><?php echo $a_item.$font.$element_record["level"].$font_; ?></a></td>
									
									<td class="text-center">
										<?php
										if( $element_record["system_flag"] == 1 )
										{
											?>
											<i class="fas fa-cog"></i>
											<?php
										}
										?>
									</td>
									
									
									<td><?php echo $a_item.$font.get_date_time_good_string($element_record["time_created"]).$font_; ?></a></td>
									<td><?php echo $a_item.$font.get_date_time_good_string($element_record["time_edited"]).$font_; ?></a></td>
									
								</tr>
								<?php
							}//for
						}
						?>
						</tbody>
						
						
						<tfoot>
							<tr>
								<td colspan="12" style="text-align:center;">
									<div class="btn-group">
										<?php
										/*
										Исходные данные для переключателя страниц:
										$current_page (номер текущей страницы, от 0)
										$elements_count_rows (всего количество элементов)
										$rows_per_page (максимальное количество элементов на страницу)
										*/
										$current_page = $s_page;
										$rows_per_page = $list_page_limit;
										$elements_count_rows = $count_total_for_pagination;
										
										
										
										//КНОПКА "ВЛЕВО"
										$to_left_disabled = "";
										if( $current_page == 0 )
										{
											$to_left_disabled = "disabled";
										}
										?>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" onclick="go_to_page(0);" href="javascript:void(0);"><?php echo translate_str_by_id(4038); ?></a>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" onclick="go_to_page(<?php echo $current_page-1; ?>);" href="javascript:void(0);"><i class="fa fa-chevron-left"></i></a>
										
										
										<?php
										//Определяем количество страниц
										$pages_count = (int)($elements_count_rows/$rows_per_page);
										if( ($elements_count_rows%$rows_per_page) > 0 )
										{
											$pages_count++;
										}
										
										
										//Выводим кнопки для конкретных страниц (с номерами)
										/*
										Количество страниц, теоретически, может быть очень большим. Чтобы не гонять цикл, пропускаем страницы до тех, которые нужно выводить
										*/
										$i_start = $current_page - 2;
										if($i_start < 0)
										{
											$i_start = 0;
										}
										for($i=$i_start; $i < $pages_count; $i++)
										{
											//Две кнопки до текущей - показываем
											if( ($current_page - $i) > 2  )
											{
												continue;
											}
											
											
											//Две кнопки после текущей - показываем
											if( ($i - $current_page) > 2  )
											{
												break;
											}
											
											
											
											$active = "";
											if($i == $current_page)
											{
												$active = "active";
											}
											?>
											<a href="javascript:void(0);" class="btn btn-default <?php echo $active; ?>" onclick="go_to_page(<?php echo $i; ?>);"><?php echo $i+1; ?></a>
											<?php
										}
										
										
										//КНОПКА "ВПРАВО"
										$to_right_disabled = "";
										if( ($current_page+1) == $pages_count )
										{
											$to_right_disabled = "disabled";
										}
										?>
										<a href="javascript:void(0);" class="btn btn-default <?php echo $to_right_disabled; ?>" onclick="go_to_page(<?php echo $current_page+1; ?>);"><i class="fa fa-chevron-right"></i></a>
										<a href="javascript:void(0);" class="btn btn-default <?php echo $to_right_disabled; ?>" onclick="go_to_page(<?php echo $pages_count-1; ?>);"><?php echo translate_str_by_id(2184); ?></a>
									</div>
									
									<br>
									
									<?php
									if( $content_manager_mode == "hierarchy" )
									{
										?>
										<div style="text-align:left;color:#000;">
										<?php echo translate_str_by_id(2246); ?>: <b><?php echo translate_str_by_id(2247); ?></b> (<?php echo translate_str_by_id(2248); ?> - <?php echo $rows_per_page; ?>. <?php echo translate_str_by_id(2249); ?>)<br>
										<?php echo translate_str_by_id(2187); ?>: <b><?php echo $count_total; ?></b><br>
										<?php echo translate_str_by_id(2188); ?>: <b><?php echo $pages_count; ?></b>
										</div>
										<?php
									}
									else//Линейный
									{
										?>
										<div style="text-align:left;color:#000;">
										<?php echo translate_str_by_id(2246); ?>: <b><?php echo translate_str_by_id(2250); ?></b><br>
										<?php echo translate_str_by_id(2251); ?>: <b><?php echo $count_total; ?></b><br>
										<?php echo translate_str_by_id(2252); ?>: <b><?php echo $rows_per_page; ?></b><br>
										<?php echo translate_str_by_id(2253); ?>: <b><?php echo $pages_count; ?></b>
										</div>
										<?php
									}
									?>
								</td>
							</tr>
						</tfoot>
						<script>
						function go_to_page(need_page)
						{
							//Устанавливаем cookie
							document.cookie = "content_manager_page="+JSON.stringify(need_page)+"; path=/;";
							
							location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager";
						}
						</script>
						
					</table>
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	<!-- БЛОК УСТАНОВКИ ФЛАГА ГЛАВНЫЙ -->
	<form method="POST" name="set_main_flag_form" style="display:none;">
		<input type="hidden" name="action" value="set_main_flag" />
		<input type="hidden" name="content_id" id="content_id_set_main_flag" value="" />
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	function set_main_flag()
	{
		var content_array = getCheckedElements();
		
		if( content_array.length == 0 )
		{
			alert("<?php echo translate_str_by_id(2254); ?>");
			return;
		}
		
		if( content_array.length > 1 )
		{
			alert("<?php echo translate_str_by_id(2255); ?>");
			return;
		}
		
		document.getElementById("content_id_set_main_flag").value = content_array[0];
		document.forms["set_main_flag_form"].submit();
	}
	</script>
	
	
	
	
	
	
	
	<!-- БЛОК ПУБЛИКАЦИИ/СНЯТИЯ С ПУБЛИКАЦИИ МАТЕРИАЛОВ -->
	<form method="POST" name="set_published_flag_form" style="display:none;">
		<input type="hidden" name="action" value="set_published_flag" />
		<input type="hidden" name="content_array" id="content_array_published_flag_action" value="" />
		<input type="hidden" name="published_flag" id="published_flag_input" value="" />
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	function set_published_flag_action(mode)
	{
		var content_array = getCheckedElements();
		
		if( content_array.length == 0 )
		{
			alert("<?php echo translate_str_by_id(2256); ?>");
			return;
		}
		
		document.getElementById("published_flag_input").value = mode;
		document.getElementById("content_array_published_flag_action").value = JSON.stringify(content_array);
		document.forms["set_published_flag_form"].submit();
	}
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<!-- БЛОК УДАЛЕНИЯ МАТЕРИАЛОВ -->
	<form method="POST" name="delete_form" style="display:none;">
		<input type="hidden" name="action" value="delete_content" />
		<input type="hidden" name="content_array" id="content_array_delete_action" value="" />
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	//Функция удаления материалов
	function delete_action()
	{
		var content_array = getCheckedElements();
		
		if( content_array.length == 0 )
		{
			alert("<?php echo translate_str_by_id(2257); ?>");
			return;
		}
		
		if( !confirm("<?php echo translate_str_by_id(2258); ?>") )
		{
			return;
		}
		
		
		document.getElementById("content_array_delete_action").value = JSON.stringify(content_array);
		document.forms["delete_form"].submit();
	}
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	<?php
	//Для мультиязычных полей возвращаем обратно:
	$sort_field = str_replace("_lang", "", $sort_field);
	
	//Выставляем индикатор сортировки для режима "Линейный"
	if( $content_manager_mode == "direct_list" )
	{
		?>
		<script>
		jQuery( window ).load(function() 
		{
			document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
		});
		// ------------------------------------------------------------------------------------------------
		</script>
		<?php
	}
	?>
	
	
	
	

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
	/*
	Если было выставлено куки cm_last_edit_mode, то это куки должно совпадать с $is_frontend. Если не совпадает, то это куки нужно перезаписать (указать равным $is_frontend) и перезагрузить страницу с предварительным сбросом всех куки менеджера материалов. Таким образом, будут исключены колизии, вызванные тем, что пользователь ЗДЕСЬ ранее мог работать в одном edit_mode, затем, находясь в ДРУГОМ разделе ПУ поменял edit_mode и потом снова вернулся СЮДА.
	К колизиям, к примеру, относится выход за пределы количества страниц, если во фронтенде их больше, чем в бэкенде (или наоборот) и пользователь ранее находился на странице в одном режиме, номер которой больше, чем количество страниц в другом режиме.
	
	
	
	Кроме этого, если в куки был записан номер страницы ($s_page), превышающий их текущее количество ($pages_count), также делаем сброс всех куки менеджера материалов. Это могло произойти, если к примеру пользователь находился в менеджере материалов на странице с большим номером, а потом, к примеру через страницу "Дерево материалов" удалил часть страниц сайта и затем снова открыл менеджер материалов - тогда количество страниц может оказаться меньше номера той страницы, на которой пользователь был до удаления материалов. Тоже самое может быть при удалении материалов здесь же - на последней странице
	*/
	//cm_last_edit_mode - последний режим редактирования, использовавшийся в менеджере материалов
	if( isset( $_COOKIE["cm_last_edit_mode"] ) || $s_page >= $pages_count )
	{
		//Если не равен текущему режиму редактирования. Перезаписываем куки cm_last_edit_mode, сбрасываем все куки менеджера материалов и перезагружаем страницу
		if( $_COOKIE["cm_last_edit_mode"] != $is_frontend || $s_page >= $pages_count )
		{
			?>
			<script>
			cookie_to_default();//Сброс куки менеджера материалов
			
			document.cookie = "cm_last_edit_mode=<?php echo $is_frontend; ?>; path=/;";
			
			location=location;
			</script>
			<?php
			exit();
		}
	}
	else//Куки не был выставлен ранее, значит, просто выставляем текущий режим
	{
		?>
		
		<script>
		document.cookie = "cm_last_edit_mode=<?php echo $is_frontend; ?>; path=/;";
		</script>
		
		<?php
	}
	?>
	
	
	
	
	<?php
}
?>