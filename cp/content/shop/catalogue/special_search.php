<?php
/**
Страничный скрипт для управления одним специальным поиском (создание/редактирование)
*/
defined('_ASTEXE_') or die('No access');


//Мультиязычность. Массив с отдельными полями, которые содержат переводимые значения (здесь не все поля, кроме тех, что в древовидных списках). В массиве - постфиксы (без search_)
$translated_items = array('caption', 'title', 'description', 'keywords');
$translated_steps_levels_fields = array('value', 'h1', 'title', 'description', 'keywords');//Имена полей в массиве уровней вложенности шага
/*
Еще также переводимыми будут:
- шаги спецпоиска (только value)
- уровни каждого шага (value, h1, title, description, keywords)
*/
?>

<?php
if( isset( $_POST["action"] ) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	try
	{
		//Старт транзакции
		if( ! $db_link->beginTransaction()  )
		{
			throw new Exception(translate_str_by_id(2132));
		}
		
		//Получаем данные
		//$search_caption = htmlentities($_POST["search_caption"], ENT_QUOTES, "UTF-8", false);
		//$search_title = htmlentities($_POST["search_title"], ENT_QUOTES, "UTF-8", false);
		//$search_description = htmlentities($_POST["search_description"], ENT_QUOTES, "UTF-8", false);
		//$search_keywords = htmlentities($_POST["search_keywords"], ENT_QUOTES, "UTF-8", false);
		$search_robots = htmlentities($_POST["search_robots"], ENT_QUOTES, "UTF-8", false);
		$search_alias = htmlentities($_POST["search_alias"], ENT_QUOTES, "UTF-8", false);
		$search_order = (int)$_POST["search_order"];
		$steps = json_decode($_POST["tree_json"], true);
		$deleted_steps = json_decode($_POST["deleted_steps"], true);
		$search_active = (int)$_POST["search_active"];
		
		
		//Мультиязычность
		for( $i = 0 ; $i < count($translated_items) ; $i++ )
		{
			//Получаем аргументы по списку
			${"search_".$translated_items[$i]} = htmlentities($_POST["search_".$translated_items[$i]], ENT_QUOTES, "UTF-8", false);
			${"search_".$translated_items[$i]."_lang_str_id"} = $_POST["search_".$translated_items[$i]."_lang_str_id"];
			
			//Кастомный алгоритм
			${"search_".$translated_items[$i]} = save_custom_translation(${"search_".$translated_items[$i]."_lang_str_id"}, ${"search_".$translated_items[$i]});
			if( ${"search_".$translated_items[$i]} == 0 )
			{
				throw new Exception( 'Error executing multilang custom algorithm 1' );
			}
		}
		
		
		
		if( $_POST["search_id"] == 0 )//Создание
		{
			//Создаем учетную запись специального поиска
			if( ! $db_link->prepare("INSERT INTO `shop_special_searches` (`caption`, `alias`, `order`, `active`, `title`, `description`, `keywords`, `robots`) VALUES (?,?,?,?,?,?,?,?);")->execute( array($search_caption, $search_alias, $search_order, $search_active, $search_title, $search_description, $search_keywords, $search_robots) ) )
			{				
				throw new Exception(translate_str_by_id(3010));
			}
			
			$search_id = $db_link->lastInsertId();//ID добавленного поиска
				
			//СОХРАНЕНИЕ ИЗОБРАЖЕНИЯ
			$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/catalogue_images/";
			$FILE_POST = $_FILES["file_local"];
			if( $FILE_POST["size"] > 0 )
			{
				//Получаем файл:
				$fileName = $FILE_POST["name"];
				
				//Получаем расширение файла
				$file_extension = explode(".", $fileName);
				$file_extension = $file_extension[count($file_extension)-1];
				
				
				//Проверка расширения
				$file_extension = strtolower($file_extension);
				if( array_search( $file_extension, array('png', 'jpg', 'jpeg', 'gif') ) === false )
				{
					throw new Exception(translate_str_by_id(2771));
				}
				
				
				//Имя файла будет вида special_search_<id>.$file_extension
				$saved_file_name = "special_search_".$search_id.".".$file_extension;
				
				$uploadfile = $files_upload_dir.$saved_file_name;
				
				if (copy($FILE_POST['tmp_name'], $uploadfile))
				{
					if( $db_link->prepare("UPDATE `shop_special_searches` SET `img` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $search_id) ) != true)
					{
						throw new Exception(translate_str_by_id(3011));
					}
				} 
				else 
				{
					throw new Exception(translate_str_by_id(3012));
				}
			}
			
			
			
			//СОХРАНЯЕМ ШАГИ ПОИСКА
			for($i=0; $i < count($steps); $i++)
			{
				$order = $i+1;
				
				$caption = htmlentities($steps[$i]["value"], ENT_QUOTES, "UTF-8", false);
				$alias = htmlentities($steps[$i]["alias"], ENT_QUOTES, "UTF-8", false);
				
				//Мультиязычность.
				$caption = save_custom_translation($steps[$i]["value_lang_str_id"], $caption);
				if( $caption == 0 )
				{
					throw new Exception( 'Error executing custom algorithm 2.1.' );
				}
				
				
				$type = $steps[$i]["type"];
				if( $type != 1 && $type != 2 )
				{
					throw new Exception(translate_str_by_id(3013));
				}
				
				$objects = $steps[$i]["objects"];
				for( $o=0 ; $o < count($objects) ; $o++ )
				{
					$objects[$o] = (int)$objects[$o];
				}
				$objects = json_encode($objects);
				
				if( ! $db_link->prepare("INSERT INTO `shop_special_searches_steps` (`search_id`, `caption`, `alias`, `type`, `objects`, `order`) VALUES (?, ?, ?, ?, ?, ?);")->execute( array($search_id, $caption, $alias, $type, $objects, $order) ) )
				{
					throw new Exception(translate_str_by_id(3014));
				}
				
				$steps[$i]["id"] = $db_link->lastInsertId();
			}
			
			
			//Обработка метаданных для уровней вложенности шагов
			for($i=0; $i < count($steps); $i++)
			{
				//Уровни вложенности для данного шага
				$levels = $steps[$i]["levels"];
				
				//Добавляем настройки метаданных для каждого уровня вложенности данного шага
				for($lev = 0; $lev < count($levels); $lev++)
				{
					$levels[$lev]["value"] = htmlentities($levels[$lev]["value"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["h1"] = htmlentities($levels[$lev]["h1"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["title"] = htmlentities($levels[$lev]["title"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["description"] = htmlentities($levels[$lev]["description"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["keywords"] = htmlentities($levels[$lev]["keywords"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["robots"] = htmlentities($levels[$lev]["robots"], ENT_QUOTES, "UTF-8", false);
					
					
					
					//Мультиязычность. Кастомный алгоритм
					for( $t=0 ; $t < count($translated_steps_levels_fields) ; $t++ )
					{ 
						$levels[$lev][$translated_steps_levels_fields[$t]] = save_custom_translation($levels[$lev][$translated_steps_levels_fields[$t]."_lang_str_id"], $levels[$lev][$translated_steps_levels_fields[$t]]);
				
						if( $levels[$lev][$translated_steps_levels_fields[$t]] == 0 )
						{
							throw new Exception( 'Error executing custom algorithm 3.1.' );
						}
					}
					
					
					
					
					if( ! $db_link->prepare("INSERT INTO `shop_special_searches_metadata` (`value`, `search_id`, `step_id`, `step_level`, `h1`, `title`, `description`, `keywords`, `robots`) VALUES (?,?,?,?,?,?,?,?,?);")->execute( array($levels[$lev]["value"], $search_id, $steps[$i]["id"], $lev+1, $levels[$lev]["h1"], $levels[$lev]["title"], $levels[$lev]["description"], $levels[$lev]["keywords"], $levels[$lev]["robots"] ) ) )
					{
						throw new Exception(translate_str_by_id(3015));
					}
				}
			}
		}
		else//РЕДАКТИРОВАНИЕ
		{
			$search_id = (int)$_POST["search_id"];
			
			//УЧЕТНАЯ ЗАПИСЬ
			if( ! $db_link->prepare("UPDATE `shop_special_searches` SET `caption` = ?, `alias` = ?, `order`=?, `active` = ?, `title`=?, `description`=?, `keywords`=?, `robots`=? WHERE `id` = ?;")->execute( array($search_caption, $search_alias, $search_order, $search_active, $search_title, $search_description, $search_keywords, $search_robots, $search_id) ) )
			{
				throw new Exception(translate_str_by_id(3016));
			}
			

			//СОХРАНЕНИЕ ИЗОБРАЖЕНИЯ
			$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/catalogue_images/";
			$FILE_POST = $_FILES["file_local"];
			if( $FILE_POST["size"] > 0 )
			{
				//Получаем файл:
				$fileName = $FILE_POST["name"];
				
				//Получаем расширение файла
				$file_extension = explode(".", $fileName);
				$file_extension = $file_extension[count($file_extension)-1];
				
				
				//Проверка расширения
				$file_extension = strtolower($file_extension);
				if( array_search( $file_extension, array('png', 'jpg', 'jpeg', 'gif') ) === false )
				{
					throw new Exception(translate_str_by_id(2771));
				}
				
				
				//Имя файла будет вида special_search_<id>.$file_extension
				$saved_file_name = "special_search_".$search_id.".".$file_extension;
				
				$uploadfile = $files_upload_dir.$saved_file_name;
				
				if (copy($FILE_POST['tmp_name'], $uploadfile))
				{
					if( $db_link->prepare("UPDATE `shop_special_searches` SET `img` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $search_id) ) != true)
					{			
						throw new Exception(translate_str_by_id(3017));
					}
				} 
				else 
				{
					throw new Exception(translate_str_by_id(3018));
				}
			}
			
			
			//ШАГИ ПОИСКА
			//Добавляем новые шаги
			for($i=0; $i < count($steps); $i++)
			{
				$order = $i+1;
				
				
				$caption = htmlentities($steps[$i]["value"], ENT_QUOTES, "UTF-8", false);
				$alias = htmlentities($steps[$i]["alias"], ENT_QUOTES, "UTF-8", false);
				
				//Мультиязычность.
				$caption = save_custom_translation($steps[$i]["value_lang_str_id"], $caption);
				if( $caption == 0 )
				{
					throw new Exception( 'Error executing custom algorithm 2.2.1.' );
				}
				
				
				$type = $steps[$i]["type"];
				if( $type != 1 && $type != 2 )
				{
					throw new Exception(translate_str_by_id(3013));
				}
				
				$objects = $steps[$i]["objects"];
				for( $o=0 ; $o < count($objects) ; $o++ )
				{
					$objects[$o] = (int)$objects[$o];
				}
				$objects = json_encode($objects);
				
				if($steps[$i]["is_new"] == true)
				{
					if( ! $db_link->prepare("INSERT INTO `shop_special_searches_steps` (`search_id`, `caption`, `alias`, `type`, `objects`, `order`) VALUES (?, ?, ?, ?, ?, ?);")->execute( array($search_id, $caption, $alias, $type, $objects, $order) ) )
					{
						throw new Exception(translate_str_by_id(3019));
					}
					
					
					$steps[$i]["id"] = $db_link->lastInsertId();
				}
			}
			//Обновляем существующие шаги
			for($i=0; $i < count($steps); $i++)
			{
				$order = $i+1;
				$caption = htmlentities($steps[$i]["value"], ENT_QUOTES, "UTF-8", false);
				$alias = htmlentities($steps[$i]["alias"], ENT_QUOTES, "UTF-8", false);
				
				
				//Мультиязычность.
				$caption = save_custom_translation($steps[$i]["value_lang_str_id"], $caption);
				if( $caption == 0 )
				{
					throw new Exception( 'Error executing custom algorithm 2.2.2.' );
				}
				
				
				$type = $steps[$i]["type"];
				if( $type != 1 && $type != 2 )
				{
					throw new Exception(translate_str_by_id(3013));
				}
				
				$objects = $steps[$i]["objects"];
				for( $o=0 ; $o < count($objects) ; $o++ )
				{
					$objects[$o] = (int)$objects[$o];
				}
				$objects = json_encode($objects);
				
				
				if($steps[$i]["is_new"] == false)
				{
					if( ! $db_link->prepare("UPDATE `shop_special_searches_steps` SET `caption` = ?, `alias` = ?, `type`=?, `objects` = ?, `order` = ? WHERE `id` = ?;")->execute( array($caption, $alias, $type, $objects, $order, $steps[$i]["id"]) ) )
					{
						throw new Exception(translate_str_by_id(3020));
					}
				}
			}
			
			//Удаляем удаленные шаги
			if( count($deleted_steps) > 0 )
			{
				$binding_values = array();
				$STEPS_DELETE = "DELETE FROM `shop_special_searches_steps` WHERE `id` IN (";
				for($i=0; $i < count($deleted_steps); $i++)
				{
					if($i > 0)
					{
						$STEPS_DELETE = $STEPS_DELETE.",";
					}
					$STEPS_DELETE = $STEPS_DELETE."?";
					
					array_push($binding_values, $deleted_steps[$i]);
				}
				$STEPS_DELETE = $STEPS_DELETE. ");";
				
				if( ! $db_link->prepare($STEPS_DELETE)->execute($binding_values) )
				{
					throw new Exception(translate_str_by_id(3021));
				}
			}
			
			
			//Обработка метаданных для уровней вложенности шагов
			//Сначала удаляем старые записи для всего спецпоиска
			if( ! $db_link->prepare("DELETE FROM `shop_special_searches_metadata` WHERE `search_id` = ?;")->execute( array($search_id) ) )
			{
				throw new Exception(translate_str_by_id(3022));
			}
			//Теперь добавляем записи
			for($i=0; $i < count($steps); $i++)
			{
				//Уровни вложенности для данного шага
				$levels = $steps[$i]["levels"];
				
				//Добавляем настройки метаданных для каждого уровня вложенности данного шага
				for($lev = 0; $lev < count($levels); $lev++)
				{
					$levels[$lev]["value"] = htmlentities($levels[$lev]["value"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["h1"] = htmlentities($levels[$lev]["h1"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["title"] = htmlentities($levels[$lev]["title"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["description"] = htmlentities($levels[$lev]["description"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["keywords"] = htmlentities($levels[$lev]["keywords"], ENT_QUOTES, "UTF-8", false);
					$levels[$lev]["robots"] = htmlentities($levels[$lev]["robots"], ENT_QUOTES, "UTF-8", false);
					
					
					//Мультиязычность. Кастомный алгоритм
					for( $t=0 ; $t < count($translated_steps_levels_fields) ; $t++ )
					{ 
						$levels[$lev][$translated_steps_levels_fields[$t]] = save_custom_translation($levels[$lev][$translated_steps_levels_fields[$t]."_lang_str_id"], $levels[$lev][$translated_steps_levels_fields[$t]]);
				
						if( $levels[$lev][$translated_steps_levels_fields[$t]] == 0 )
						{
							throw new Exception( 'Error executing custom algorithm 3.2.' );
						}
					}
					
					
					
					if( ! $db_link->prepare("INSERT INTO `shop_special_searches_metadata` (`value`, `search_id`, `step_id`, `step_level`, `h1`, `title`, `description`, `keywords`, `robots`) VALUES (?,?,?,?,?,?,?,?,?);")->execute( array($levels[$lev]["value"], $search_id, $steps[$i]["id"], $lev+1, $levels[$lev]["h1"], $levels[$lev]["title"], $levels[$lev]["description"], $levels[$lev]["keywords"], $levels[$lev]["robots"] ) ) )
					{
						throw new Exception(translate_str_by_id(3023));
					}
				}
			}
		}
	}
	catch (Exception $e)
	{
		//Откатываем все изменения
		$db_link->rollBack();
		
		//Если был переход на создание, то при ошибке, special_search_id не будет в GET-параметрах
		$special_search_id_str = "";
		if( (int)$_POST["search_id"] > 0 )
		{
			//Был переход на редактирование, значит special_search_id должен быть в GET-параметрах
			$special_search_id_str = "&special_search_id=".(int)$_POST["search_id"];
		}
		
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/specialnye-poiski/specialnyj-poisk?error_message=<?php echo urlencode($e->getMessage()).$special_search_id_str; ?>";
		</script>
		<?php
		exit;
	}

	//Дошли до сюда, значит выполнено ОК
	$db_link->commit();//Коммитим все изменения и закрываем транзакцию
	?>
	<script>
		location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/specialnye-poiski/specialnyj-poisk?special_search_id=<?php echo $search_id; ?>&success_message=<?php echo urlencode(translate_str_by_id(3024)); ?>";
	</script>
	<?php
	exit;
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	//Получаем список древовидных списков (Сам список - линейный, т.е. просто перечисление древовидных списков)
	$tree_lists_array = array();
	$tree_lists_array_query = $db_link->prepare("SELECT * FROM `shop_tree_lists` ORDER BY `id`;");
	$tree_lists_array_query->execute();
	while($tree_lists_array_record = $tree_lists_array_query->fetch() )
	{
		array_push($tree_lists_array, array("id"=>$tree_lists_array_record["id"], "value"=>translate_str_by_id($tree_lists_array_record["caption"])) );
	}
	
	
	//Получаем дерево категорий товаров $catalogue_tree_dump_JSON
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
	
	
	//Исходные данные (по умолчанию - для создания нового поиска):
	$special_search_id = 0;
	$action = "create";
	$steps = "[]";
	//$search_caption = "";
	$search_alias = "";
	$search_order = "";
	$search_active = true;
	//$search_title = "";
	//$search_description = "";
	//$search_keywords = "";
	$search_robots = "";
	
	
	//Мультиязычность
	for( $i = 0 ; $i < count($translated_items) ; $i++ )
	{
		${"search_".$translated_items[$i]} = "";
		${"search_".$translated_items[$i]."_lang_str_id"} = 0;
	}
	
	
	
	//Идет редактирование
	if( isset($_GET["special_search_id"]) )
	{
		$special_search_id = $_GET["special_search_id"];
		$action = "edit";
		
		//Общие настройки поиска
		$search_query = $db_link->prepare("SELECT * FROM `shop_special_searches` WHERE `id` = ?;");
		$search_query->execute( array($special_search_id) );
		$search_record = $search_query->fetch();
		//$search_caption = $search_record["caption"];
		$search_alias = $search_record["alias"];
		$search_order = $search_record["order"];
		$search_img = $search_record["img"];
		$search_active = (bool)$search_record["active"];
		//$search_title = $search_record["title"];
		//$search_description = $search_record["description"];
		//$search_keywords = $search_record["keywords"];
		$search_robots = $search_record["robots"];
		
		
		//Мультиязычность
		for( $i = 0 ; $i < count($translated_items) ; $i++ )
		{
			${"search_".$translated_items[$i]."_lang_str_id"} = $search_record[$translated_items[$i]];
			${"search_".$translated_items[$i]} = translate_str_by_id($search_record[$translated_items[$i]]);
		}
		
		
		
		//Шаги
		$steps = array();
		$search_steps_query = $db_link->prepare("SELECT * FROM `shop_special_searches_steps` WHERE `search_id` = ? ORDER BY `order`;");
		$search_steps_query->execute( array($special_search_id) );
		while( $step = $search_steps_query->fetch() )
		{
			//Получаем шаблоны метаданных для уровней вложенности данного шага
			$levels = array();
			$levels_query = $db_link->prepare("SELECT * FROM `shop_special_searches_metadata` WHERE `step_id` = ?;");
			$levels_query->execute( array($step["id"]) );
			while( $level = $levels_query->fetch() )
			{
				$levels[] = array(
					"value_lang_str_id"=>$level["value"],
					"value"=>translate_str_by_id($level["value"]),
					
					"h1_lang_str_id"=>$level["h1"],
					"h1"=>translate_str_by_id($level["h1"]),
					
					"title_lang_str_id"=>$level["title"],
					"title"=>translate_str_by_id($level["title"]),
					
					"description_lang_str_id"=>$level["description"],
					"description"=>translate_str_by_id($level["description"]),
					
					"keywords_lang_str_id"=>$level["keywords"],
					"keywords"=>translate_str_by_id($level["keywords"]),
					
					"robots"=>$level["robots"]);
			}
			
			
			array_push($steps, array("id"=>$step["id"], "value"=>translate_str_by_id($step["caption"]), "value_lang_str_id"=>$step["caption"], "alias"=>$step["alias"], "type"=>$step["type"], "is_new"=>false,"objects"=>json_decode($step["objects"], true), "levels"=>$levels ) );
		}
		$steps = json_encode($steps);
		
		
		//Картинка
		if( $search_img != "" )
		{
			$search_img = "/content/files/images/catalogue_images/".$search_img;
		}
	}
	
	?>
	
	
	<?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
	
	
	
	
	
	<!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none" enctype="multipart/form-data">
        <input name="search_id" id="search_id" value="<?php echo $special_search_id; ?>" />
		
		<input name="action" id="action" type="text" value="ok" style="display:none"/>
        <input name="tree_json" id="tree_json" type="text" value="" style="display:none"/>
        
		<!-- <input name="search_caption" id="search_caption" /> -->
		<input name="search_alias" id="search_alias" />
		<input name="search_order" id="search_order" />
		
		<input name="search_active" id="search_active" />
		
		<input name="deleted_steps" id="deleted_steps" value="" />
		
		<input type="file" name="file_local" id="file_local" accept="image/jpeg,image/jpg,image/png,image/gif" onchange="onFileChanged();" />
		
		<!-- <input name="search_title" id="search_title" /> -->
		<!-- <input name="search_description" id="search_description" /> -->
		<!-- <input name="search_keywords" id="search_keywords" /> -->
		<input name="search_robots" id="search_robots" />
		
		<?php
		//Мультиязычность
		for( $i = 0 ; $i < count($translated_items) ; $i++ )
		{
			?>
			<input name="search_<?php echo $translated_items[$i]; ?>" id="search_<?php echo $translated_items[$i]; ?>" />
			<input name="search_<?php echo $translated_items[$i]; ?>_lang_str_id" value="<?php echo ${"search_".$translated_items[$i]."_lang_str_id"}; ?>" />
			<?php
		}
		?>
		
		
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!--Форма для отправки-->
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="add_new_item();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3025); ?></div>
				</a>
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="delete_selected_item();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3026); ?></div>
				</a>
				
				<a class="panel_a" href="javascript:void(0);" onclick="unselect_tree();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/selection_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3027); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/specialnye-poiski">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/special_search.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(785); ?></div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel collapsed">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(3028); ?>
			</div>
			<div class="panel-body">
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3029); ?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="search_caption_input" value="<?php echo $search_caption; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2166); ?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="search_alias_input" value="<?php echo $search_alias; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3030); ?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="search_order_input" value="<?php echo $search_order; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2801); ?>
						</label>
						<div class="col-lg-6 text-center">
							<button class="btn btn-success" type="button" onclick="document.getElementById('file_local').click();">
								<i class="fa fa-file"></i>
								<span class="bold"><?php echo translate_str_by_id(2802); ?></span>
							</button>
							<br><br>
							<img id="img_for_show" onerror = "this.src = '<?php echo "/content/files/images/no_image.png"; ?>'" src="<?php echo $search_img; ?>?chache=<?php echo time(); ?>" style="max-width:96px; max-height:96px" />

							<script>
							//Функция выбора файла
							function onFileChanged()
							{
								var input_file = document.getElementById("file_local");//input для файла изображения
								var file = input_file.files[0];//Получаем выбранный файл
								
								if(file == undefined)
								{
									return;
								}
								
								//Запрещаем загружать файлы больше 50 Кб
								if(file.size > 51200)
								{
									input_file.value = null;
									alert("<?php echo translate_str_by_id(3031); ?>");
									return;
								}
								
								//Проверяем тип файла
								if(file.type != "image/jpeg" && file.type != "image/jpg" && file.type != "image/png" && file.type != "image/gif")
								{
									input_file.value = null;
									alert("<?php echo translate_str_by_id(2444); ?>");
									return;
								}
								
								
								//Отображаем файл
								document.getElementById("img_for_show").setAttribute("src", URL.createObjectURL(file));
								
								
								//Сам файл для формы остается в инпуте
							}
							</script>
							
						</div>
					</div>
				</div>
				
				
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3032); ?>
						</label>
						<div class="col-lg-6">
							<?php
							$checked = "";
							if( $search_active )
							{
								$checked = " checked=\"checked\" ";
							}
							?>
						
							<input type="checkbox"  id="search_active_checkbox" value="search_active_checkbox" class="form-control" <?php echo $checked; ?> />
						</div>
					</div>
				</div>
				
				
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel collapsed">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
					<a class="showhide"><i class="fa fa-chevron-up"></i></a>
				</div>
				<?php echo translate_str_by_id(3033); ?>
			</div>
			<div class="panel-body">
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2167); ?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="search_title_input" value="<?php echo $search_title; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2237); ?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="search_description_input" value="<?php echo $search_description; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2281); ?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="search_keywords_input" value="<?php echo $search_keywords; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2282); ?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="search_robots_input" value="<?php echo $search_robots; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-12"></div>
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3034); ?>
			</div>
			<div class="panel-body">
				<div id="container_A" style="height:470px;">
				</div>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-6" id="step_info_div_col">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3035); ?>
			</div>
			<div class="panel-body">
				<div id="step_info_div">
				</div>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-12"></div>
	
	
	<div class="col-lg-6" id="step_levels_div">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3036); ?>
			</div>
			<div class="panel-body">
				<div id="container_B" style="height:470px;">
				</div>
			</div>
			<div class="panel-footer">
				
				
				
				<div class="row">
					<div class="col-md-12">
						
						<a class="btn btn-success" href="javascript:void(0);" style="border:0;" onclick="add_new_item_B();" title="<?php echo translate_str_by_id(3037); ?>"><i class="fas fa-plus"></i></a> 
						
						<a class="btn btn-danger" href="javascript:void(0);" style="border:0;" onclick="delete_selected_item_B();" title="<?php echo translate_str_by_id(3038); ?>"><i class="fas fa-minus"></i></a>
						
						<a class="btn btn-primary" href="javascript:void(0);" style="border:0;" onclick="unselect_tree_B();" title="<?php echo translate_str_by_id(2268); ?>"><i class="fas fa-square"></i></a>
						
					</div>
				</div>
				
				
			</div>
		</div>
	</div>
	
	<div class="col-lg-6" id="step_levels_metatemplates_div">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3039); ?>
			</div>
			<div class="panel-body">

				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3040); ?>
						</label>
						<div class="col-lg-6">
							<textarea id="level_h1" class="form-control" onkeyup="on_metadata_edit();"></textarea>
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2167); ?>
						</label>
						<div class="col-lg-6">
							<textarea id="level_title" class="form-control" onkeyup="on_metadata_edit();"></textarea>
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2280); ?>
						</label>
						<div class="col-lg-6">
							<textarea id="level_description" class="form-control" onkeyup="on_metadata_edit();"></textarea>
						</div>
					</div>
				</div>
				
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2281); ?>
						</label>
						<div class="col-lg-6">
							<textarea id="level_keywords" class="form-control" onkeyup="on_metadata_edit();"></textarea>
						</div>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="col-lg-12">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2282); ?>
						</label>
						<div class="col-lg-6">
							<textarea id="level_robots" class="form-control" onkeyup="on_metadata_edit();"></textarea>
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-12">
		
		<div class="hpanel collapsed">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(3041); ?>
			</div>
			<div class="panel-body">
				<?php echo translate_str_by_id(3042); ?>

				
			</div>
		</div>
	
	</div>
	
	
	
	<script type="text/javascript" charset="utf-8">
    /*ДЕРЕВО*/
    //Для редактируемости дерева
    webix.protoUI({
        name:"edittree"
    }, webix.EditAbility, webix.ui.tree);
    //Формирование дерева
    tree = new webix.ui({
		
		
		//Шаблон элемента дерева
		template:function(obj, common)//Шаблон узла дерева
			{
				var folder = common.folder(obj, common);
				var icon = "";
				
				//Указание типа списка
				var type_str = "<?php echo translate_str_by_id(2813); ?>";
				if(obj.type == 1)
				{
					type_str = "<?php echo translate_str_by_id(2990); ?>";
				}
				
				
				var value_text = "<span><b>" + obj.value + "</b>, тип \""+type_str+"\", объектов "+obj.objects.length+"</span>";//Вывод текста

				return common.icon(obj, common) + common.folder(obj, common)  + icon + value_text;
			},//~template
		
		
        editable:true,//редактируемое
        editValue:"value",
    	editaction:"dblclick",//редактирование по двойному нажатию
        container:"container_A",//id блока div для дерева
        view:"edittree",
    	select:true,//можно выделять элементы
    	drag:true,//можно переносить
    	editor:"text",//тип редактирование - текстовый
    });
    /*~ДЕРЕВО*/
	webix.event(window, "resize", function(){ tree.adjust(); });
    //-----------------------------------------------------
    webix.protoUI({
        name:"editlist" // or "edittree", "dataview-edit" in case you work with them
    }, webix.EditAbility, webix.ui.list);
    //-----------------------------------------------------
    //Событие при выборе элемента дерева
    tree.attachEvent("onAfterSelect", function(id)
    {
    	onSelected();
    });
    //-----------------------------------------------------
    //Обработка выбора элемента
    function onSelected()
    {
		//Если шаги не созданы
    	if(tree.count() == 0)
    	{
    	    document.getElementById("step_info_div").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("step_info_div_col").setAttribute("style", "display:none");
			
			
			//Блок настройки метаданных (скрываем)
			document.getElementById("step_levels_div").setAttribute("style", "display:none");
			document.getElementById("step_levels_metatemplates_div").setAttribute("style", "display:none");
    	    return;
    	}
    	
    	//Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    document.getElementById("step_info_div").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("step_info_div_col").setAttribute("style", "display:none");
			
			
			//Блок настройки метаданных (скрываем)
			document.getElementById("step_levels_div").setAttribute("style", "display:none");
			document.getElementById("step_levels_metatemplates_div").setAttribute("style", "display:none");
    	    return;
    	}
		
		//Показать контейнер для параметров
		document.getElementById("step_info_div_col").setAttribute("style", "display:block");
    	
		
		//Блок настройки метаданных (показываем)
		document.getElementById("step_levels_div").setAttribute("style", "display:block;");//Блок с уровнями вложенности
		show_step_levels_list();//Инициализация дерева для отображения уровней вложенности
		document.getElementById("step_levels_metatemplates_div").setAttribute("style", "display:none");//Блок с метаданными пока скрыт
		
		
    	var node = "";//Ссылка на объект узла
    	//Выделенный узел
    	node = tree.getItem(node_id);
    	
    	var parameters_table_html = "";
		
		var node_id = node.id;
		if(node.is_new)
		{
			node_id = 0;
		}
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+node_id+"</div></div>";
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+node.value+"</div></div>";
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----

		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2166); ?></label><div class=\"col-lg-6\"><input onkeyup=\"apply_options_for_content();\" type=\"text\" id=\"alias_input\" value=\""+node.alias+"\" class=\"form-control\" /></div></div>";
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		if(node.type == 1)
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3043); ?></label><div class=\"col-lg-6\"><select onchange=\"apply_options_for_step();onSelected();\" id=\"type_selector\" class=\"form-control\" ><option value=\"2\"><?php echo translate_str_by_id(2813); ?></option><option value=\"1\" selected=\"selected\"><?php echo translate_str_by_id(2990); ?></option></select></div></div>";
		}
		else
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3043); ?></label><div class=\"col-lg-6\"><select onchange=\"apply_options_for_step();onSelected();\" id=\"type_selector\" class=\"form-control\" ><option value=\"2\" selected=\"selected\"><?php echo translate_str_by_id(2813); ?></option><option value=\"1\"><?php echo translate_str_by_id(2990); ?></option></select></div></div>";
		}
		
		
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----

		parameters_table_html += "<div class=\"col-lg-12\"><label for=\"\" class=\"col-lg-12 control-label\"><?php echo translate_str_by_id(3044); ?></label></div>";
		
		parameters_table_html += "<div class=\"col-lg-12\"><div id=\"container_G\" style=\"height:150px;\"></div></div>";
		
		

    	document.getElementById("step_info_div").innerHTML = parameters_table_html;
    	
    	//Теперь инициализируем дерево объектов
    	objects_tree_init();
    	
		
    	//Отмечаем объекты:
		var objects_local = node.objects;
    	for(var i=0; i< objects_local.length; i++)
    	{
    	    objects_tree.checkItem(objects_local[i]);
    	}
		
		
		tree.refresh();
    }//function onSelected()
    //-----------------------------------------------------
	var objects_tree = "";//ПЕРЕМЕННАЯ ДЛЯ ДЕРЕВА ОБЪЕКТОВ ШАГА (Категории товаров или Древовидные списки)
        	    
    //Инициализация дерева групп после загруки страницы
    function objects_tree_init()
    {
        /*ДЕРЕВО*/
        //Формирование дерева
        objects_tree = new webix.ui({
        
            //Шаблон элемента дерева
        	template:function(obj, common)//Шаблон узла дерева
            	{
                    var folder = common.folder(obj, common);
            	    var icon = "";
                    
                    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
                    var checkbox = common.checkbox(obj, common);//Чекбокс

                    return common.icon(obj, common)+ checkbox + common.folder(obj, common)  + icon + value_text;
            	},//~template
        
            editable:false,//редактируемое
            container:"container_G",//id блока div для дерева
            view:"tree",
        	select:true,//можно выделять элементы
        	drag:false,//можно переносить
        });
        /*~ДЕРЕВО*/
		
		webix.event(window, "resize", function(){ objects_tree.adjust(); });
		

		//В зависимости от выбранного типа шага - показываем или список древовидных списков или дерево категорий товаров
		if( document.getElementById("type_selector").value == 1 )
		{
			//Выводим дерево категорий товаров
			var saved_objects = <?php echo $catalogue_tree_dump_JSON; ?>;
			objects_tree.parse(saved_objects);
			objects_tree.openAll();
		}
		else//Выводим перечень древовидных списков
		{
			var saved_objects = <?php echo json_encode($tree_lists_array); ?>;
			objects_tree.parse(saved_objects);
			objects_tree.openAll();
		}
		
		
		
		//Событие при выставлении/снятии чекбоксов групп - динамичнское применение настроек
		objects_tree.attachEvent("onItemCheck", function(id)
		{
			apply_options_for_step();
			tree.refresh();
		});
    }
	//-----------------------------------------------------
    //Событие при успешном редактировании элемента дерева
    tree.attachEvent("onValidationSuccess", function(){
        onSelected();
    });
    //-----------------------------------------------------
    tree.attachEvent("onAfterEditStop", function(state, editor, ignoreUpdate){
        //Задаем поле Alias - как транслитерация поля value;
        var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
        node.alias = iso_9_translit(node.value,  5);//5 - русский текст
        node.alias = node.alias.replace(/\s/g, '-');
        node.alias = node.alias.toLowerCase();
		node.alias = node.alias.replace(/[^\d\sA-Z\-_]/gi, '');//Убираем все символы кроме букв, цифр, тире и нинего подчеркивания
		
		onSelected();
    });
    //-----------------------------------------------------
	//Обработчик После перетаскивания узлов дерева
	tree.attachEvent("onAfterDrop",function(){
	    onSelected();
	});
    //-----------------------------------------------------
    //Добавить новый элемент в дерево
    function add_new_item()
    {
    	//Добавляем элемент в выделенный узел
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(3045); ?>", value_lang_str_id:0, is_new:true, alias:"", type:"1", objects:[], levels:[]}, tree.count(), 0);//Добавляем новый узел и запоминаем его ID
    	
    	onSelected();//Обработка текущего выделения
    }
    //-----------------------------------------------------
    //Удаление выделеного элемента
    var deleted_steps = new Array();//Массив с удаленными шагами
	function delete_selected_item()
    {
    	var nodeId = tree.getSelectedId();
		
		//Если этот шаг не новый (т.е. уже был ранее записан в БД), то вносим его в список на удаление
		node = tree.getItem(nodeId);//Выделенный узел
		if(node.is_new == false)
		{
			deleted_steps.push(node.id);
		}
		
    	tree.remove(nodeId);
    	onSelected();
    }
    //-----------------------------------------------------
    //Снятие выделения с дерева
    function unselect_tree()
    {
    	tree.unselect();
    	onSelected();
    }
	//-----------------------------------------------------
	//Применить настройки для материала
    function apply_options_for_step()
    {
        //1. Определяем выбранный материал
        var node_id = tree.getSelectedId();//ID выделенного узла
		if(node_id == 0)
		{
			return;
		}
    	node = tree.getItem(node_id);//Выделенный узел

        //2. Сохраняем alias - это обязательное поле
		node.alias = document.getElementById("alias_input").value;
        
		//3. Сохраняем тип
		node.type = document.getElementById("type_selector").value;
		
		
        //4. Сохраняем перечень объектов
        node.objects = new Array();//Массив с выбранными объектами
        node.objects = objects_tree.getChecked();
        
        //Сообщение о результате предварительного сохранения
        //webix.message("Настройки материала предварительно сохранены");
    }
    //-----------------------------------------------------
	//Функция валидации всего специального поиска
	function validate_special_search()
	{
		//1. Должно быть заполнено название поиска
		if( document.getElementById("search_caption_input").value == "" )
		{
			alert("<?php echo translate_str_by_id(3046); ?>");
			return false;
		}
		
		
		//2. Должен быть заполнен Алиас поиска
		if( document.getElementById("search_alias_input").value == "" )
		{
			alert("<?php echo translate_str_by_id(3047); ?>");
			return false;
		}
		
		
		//3. Должен быть по хотя бы один шаг
		if(tree.count() == 0)
		{
			alert("<?php echo translate_str_by_id(3048); ?>");
			return false;
		}
		
		//4. Должен присутствовать шаг с типом "Категории товаров"
		var type_1 = false;
		var tree_In_JSON = tree.serialize();//Получаем JSON-представление дерева
		for(var i=0; i < tree_In_JSON.length; i++)
		{
			if(tree_In_JSON[i]["type"] == 1)
			{
				type_1 = true;
				break;
			}
		}
		if(!type_1)
		{
			alert('<?php echo translate_str_by_id(3049); ?>');
			return false;
		}
		

		//5. Шаг с типом "Категории товаров" должен быть один
		var type_1_count = 0;
		for(var i=0; i < tree_In_JSON.length; i++)
		{
			if(tree_In_JSON[i]["type"] == 1)
			{
				type_1_count++;
			}
		}
		if(type_1_count > 1)
		{
			alert('<?php echo translate_str_by_id(3050); ?>');
			return false;
		}
		
		
		//6. После шага с типом "Категории товаров" не должно быть других шагов, т.е. он должен быть последним
		var meet_type_1 = false;
		for(var i=0; i < tree_In_JSON.length; i++)
		{
			if(meet_type_1)
			{
				alert('<?php echo translate_str_by_id(3051); ?> "'+tree_In_JSON[i-1]["value"]+'" <?php echo translate_str_by_id(3052); ?>');
				return false;
			}
			
			
			if(tree_In_JSON[i]["type"] == 1)
			{
				meet_type_1 = true;//Встретили шаг с типом "Категории товаров"
			}
		}
		
		//7. В каждом шаге с типом "Древовидный список" должен быть только один объект (т.е. не 0 и не более 1)
		for(var i=0; i < tree_In_JSON.length; i++)
		{
			if(tree_In_JSON[i]["type"] == 2)
			{
				if( parseInt(tree_In_JSON[i]["objects"].length) != 1)
				{
					alert('<?php echo translate_str_by_id(3053); ?> "'+tree_In_JSON[i]["value"]+'"');
					return false;
				}
			}
		}
		
		
		//8. В шаге с типом "Категории товаров" должен быть указан хотя бы один объект
		for(var i=0; i < tree_In_JSON.length; i++)
		{
			if(tree_In_JSON[i]["type"] == 1)
			{
				if( parseInt(tree_In_JSON[i]["objects"].length) == 0)
				{
					alert('<?php echo translate_str_by_id(3054); ?> "'+tree_In_JSON[i]["value"]+'"');
					return false;
				}
			}
		}
		
		
		//9. В каждом шаге должен быть заполнен Алиас
		for(var i=0; i < tree_In_JSON.length; i++)
		{
			if(tree_In_JSON[i]["alias"] == "")
			{
				alert('<?php echo translate_str_by_id(3055); ?> "'+tree_In_JSON[i]["value"]+'"');
				return false;
			}
		}
		
		
		//Все проверки пройдены:
		return true;
	}
	//-----------------------------------------------------
	//Инициализация редактора дерева после загруки страницы
    function tree_start_init()
    {
    	var steps = <?php echo $steps; ?>;
	    tree.parse(steps);
	    tree.openAll();
    }
    tree_start_init();
    onSelected();//Обработка текущего выделения
    // ----------------------------------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------------------------------
	//Дерево для списка уровненей вложенности шага (для ЧПУ)
	let tree_B = "";
	function show_step_levels_list()
	{
		document.getElementById("container_B").innerHTML = "";
		
		//Формирование дерева
		tree_B = new webix.ui({
			
			//Шаблон элемента дерева
			template:function(obj, common)//Шаблон узла дерева
				{
					let n = 0;
					//Шаг
					let node_id = tree.getSelectedId();//ID выделенного узла
					let node = tree.getItem(node_id);//Объект выделенного узла
					//Находим уровень вложенности
					for(let i=0; i < node.levels.length; i++)
					{
						n++;
						if( parseInt(node.levels[i].id) == parseInt(obj.id) )
						{
							break;
						}
					}
					
					
					
					
					
					var folder = common.folder(obj, common);
					var icon = "";
					return common.icon(obj, common) + common.folder(obj, common)  + icon + "<span>" + n + ". <b>" +obj.value+"</b></span>";
				},//~template
			
			
			editable:true,//редактируемое
			editValue:"value",
			editaction:"dblclick",//редактирование по двойному нажатию
			container:"container_B",//id блока div для дерева
			view:"edittree",
			select:true,//можно выделять элементы
			drag:true,//можно переносить
			editor:"text",//тип редактирование - текстовый
		});
		//Событие при выборе элемента дерева
		tree_B.attachEvent("onAfterSelect", function(id)
		{
			onSelected_B();
		});
		//Обработчик После перетаскивания узлов дерева (когда меняется порядок, нужно его зафиксировать в объекте)
		tree_B.attachEvent("onAfterDrop",function(){
			
			//Шаг
			let node_id = tree.getSelectedId();//ID выделенного узла
			let node = tree.getItem(node_id);//Объект выделенного узла
			
			let levels_ob = tree_B.serialize();
			console.log(levels_ob);
			
			node.levels = JSON.parse(JSON.stringify(levels_ob));
			
			tree_B.refresh();
			onSelected_B();
		});
		/*tree_B.attachEvent("onAfterEditStop",function(){
			//Шаг
			let node_id = tree.getSelectedId();//ID выделенного узла
			let node = tree.getItem(node_id);//Объект выделенного узла
			
			let levels_ob = tree_B.serialize();
			console.log(levels_ob);
			
			node.levels = JSON.parse(JSON.stringify(levels_ob));
			
			tree_B.refresh();
			onSelected_B();
		});*/

		tree_B.adjust();
		
		
		
		let node_id = tree.getSelectedId();//ID выделенного узла
    	let node = tree.getItem(node_id);//Объект выделенного узла
		tree_B.parse(node.levels);
	    tree_B.openAll();
		
		
		onSelected_B();
	}
	//-----------------------------------------------------
	//Обработка выбора одного из уровней вложенности
	function onSelected_B()
	{
		let step_level_id = tree_B.getSelectedId();
		if( step_level_id == 0 )
		{
			document.getElementById("step_levels_metatemplates_div").setAttribute("style", "display:none;");
			return;
		}
		
		
		document.getElementById("step_levels_metatemplates_div").setAttribute("style", "display:block;");
		
		
		//Шаг
		let node_id = tree.getSelectedId();//ID выделенного узла
    	let node = tree.getItem(node_id);//Объект выделенного узла
		
		//Находим уровень вложенности
		for(let i=0; i < node.levels.length; i++)
		{
			if( parseInt(node.levels[i].id) == parseInt(step_level_id) )
			{
				//Заполняем поля ввода метаданных текущими значениями
				
				document.getElementById("level_h1").value = node.levels[i].h1;
				document.getElementById("level_title").value = node.levels[i].title;
				document.getElementById("level_description").value = node.levels[i].description;
				document.getElementById("level_keywords").value = node.levels[i].keywords;
				document.getElementById("level_robots").value = node.levels[i].robots;
				
				
				break;
			}
		}
		
		
	}
	//-----------------------------------------------------
    //Добавить новый уровень вложенности для выделенного шага
    function add_new_item_B()
    {
		let node_id = tree.getSelectedId();//ID выделенного узла
    	let node = tree.getItem(node_id);//Объект выделенного узла
		
		//Добавляем объект уровня вложенности прямо в объет шага
		node.levels.push( {value:"<?php echo translate_str_by_id(3056); ?>", value_lang_str_id:0, is_new:true, h1:"", h1_lang_str_id:0, title:"", title_lang_str_id:0 , description:"", description_lang_str_id:0, keywords:"", keywords_lang_str_id:0, robots:""} );
		
		//Переотображаем дерево
		tree_B.clearAll();
		tree_B.parse(node.levels);
	    tree_B.openAll();
		
		onSelected_B();
    }
    //-----------------------------------------------------
	//Удалить уровень вложенности
	function delete_selected_item_B()
	{
		//Объект уровня вложенности
		let step_level_id = tree_B.getSelectedId();
		if( step_level_id == 0 )
		{
			alert("<?php echo translate_str_by_id(3057); ?>");
			return;
		}
		
		//Шаг
		let node_id = tree.getSelectedId();//ID выделенного узла
    	let node = tree.getItem(node_id);//Объект выделенного узла
		
		//Удаляем уровень вложенности
		for(let i=0; i < node.levels.length; i++)
		{
			console.log( node.levels[i].id + " - " + step_level_id );
			
			if( parseInt(node.levels[i].id) == parseInt(step_level_id) )
			{
				node.levels.splice(i,1);
				break;
			}
		}
		
		
		//Переотображаем дерево
		tree_B.clearAll();
		tree_B.parse(node.levels);
	    tree_B.openAll();
	}
	//-----------------------------------------------------
	//Снять выделение со списка уровненей вложенности
	function unselect_tree_B()
	{
		tree_B.unselect();
    	onSelected_B();
	}
	//-----------------------------------------------------
	//Функция отладки
	function debug()
	{
		var tree_json_to_save = tree.serialize();
    	tree_dump = JSON.stringify(tree_json_to_save);
		
		console.log(tree_dump);
	}
	//-----------------------------------------------------
	//Функция применения вводимых значений в поля метаданных
	function on_metadata_edit()
	{
		let step_level_id = tree_B.getSelectedId();

		//Шаг
		let node_id = tree.getSelectedId();//ID выделенного узла
    	let node = tree.getItem(node_id);//Объект выделенного узла
		
		//Находим уровень вложенности
		for(let i=0; i < node.levels.length; i++)
		{
			if( parseInt(node.levels[i].id) == parseInt(step_level_id) )
			{
				//Заполняем поля ввода метаданных текущими значениями

				node.levels[i].h1 = document.getElementById("level_h1").value;
				node.levels[i].title = document.getElementById("level_title").value;
				node.levels[i].description = document.getElementById("level_description").value;
				node.levels[i].keywords = document.getElementById("level_keywords").value;
				node.levels[i].robots = document.getElementById("level_robots").value;
				
				break;
			}
		}
	}
	//-----------------------------------------------------
	</script>

	
	
	
	
	
	
	
	
	
	
	
	
	<script>
	//Функция сохранения изменений
	function save_action()
	{
		//Проверка корректности данных
		if( ! validate_special_search() )
		{
			return;
		}
		
		
		//Заполняем форму
		//1. Дерево шагов
    	var tree_json_to_save = tree.serialize();
    	tree_dump = JSON.stringify(tree_json_to_save);
    	var tree_json_input = document.getElementById("tree_json");
    	tree_json_input.value = tree_dump;
		
		//2. Название поиска и метаданные
		document.getElementById("search_caption").value = document.getElementById("search_caption_input").value;
		document.getElementById("search_title").value = document.getElementById("search_title_input").value;
		document.getElementById("search_description").value = document.getElementById("search_description_input").value;
		document.getElementById("search_keywords").value = document.getElementById("search_keywords_input").value;
		document.getElementById("search_robots").value = document.getElementById("search_robots_input").value;
		
		//3. Алиас поиска
		document.getElementById("search_alias").value = document.getElementById("search_alias_input").value;
		
		//4. Порядок следования поиска
		document.getElementById("search_order").value = document.getElementById("search_order_input").value;
		
		//6. Список шагов на удаление
		document.getElementById("deleted_steps").value = JSON.stringify(deleted_steps);
		
		//7. Флаг активности
		var search_active = 1;
		if( document.getElementById("search_active_checkbox").checked == false )
		{
			search_active = 0;
		}
		document.getElementById("search_active").value = search_active;
		
		
		//Отправка формы
		document.forms["form_to_save"].submit();
	}
	</script>
	
	<?php
}
?>