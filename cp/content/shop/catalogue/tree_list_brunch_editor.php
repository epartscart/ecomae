<?php
/**
Страница редактирования одного древовидного списка по ветвям
*/
defined('_ASTEXE_') or die('No access');
?>


<?php
if(!empty($_POST["save_action"]))//Создание или редактирование
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Генерируем линейный массив на основе полученого иерархического
    $linear_array = json_decode($_POST["tree_json"], true);;//Линейный массив материалов
	
	$tree_list_id = (int)$_POST["tree_list_id"];
	$parent_id = (int)$_POST["parent_id"];
	
	//Получаем уровень, на котором располагаются элементы ветви
	$level = 1;//Элементы первого уровня
	if($parent_id > 0)
	{
		$level_query = $db_link->prepare("SELECT `level` FROM `shop_tree_lists_items` WHERE `id` = ?;");
		$level_query->execute( array($parent_id) );
		$level_record = $level_query->fetch();
		$level = $level_record["level"] + 1;
	}
	
	
	
	//var_dump($linear_array);
	//exit;
	
    if($_POST["save_action"] == "create")
    {
		//Мультиязычность. Кастомный алгоритм
		$caption = save_custom_translation($_POST["caption_lang_str_id"], htmlentities($_POST["caption"]));
		
		
        if( $db_link->prepare("INSERT INTO `shop_tree_lists` (`caption`, `data_type`) VALUES (?,?);")->execute( array($caption, htmlentities($_POST["data_type"]) ) ) != true)
        {
            $error_message = translate_str_by_id(3065);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        
        //Получаем ID созданного списка
		$tree_list_id = $db_link->lastInsertId();
		
		//Добавляем элементы списка в таблицу элементов
		$binding_values = array();
		$SQL_items = "INSERT INTO `shop_tree_lists_items` (`id`, `tree_list_id`, `value`, `count`, `level`, `parent`, `order`, `open`, `alias`, `url`) VALUES ";
		for($i=0; $i < count($linear_array); $i++)
		{
			$order = $i+1;
			if($i > 0) $SQL_items .= ",";
			$SQL_items .= "(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
			
			//Мультиязычность кастомный алгоритм
			$linear_array[$i]["value"] = save_custom_translation($linear_array[$i]["value_lang_str_id"], htmlentities($linear_array[$i]["value"], ENT_QUOTES, "UTF-8", false) );
			
			
			array_push($binding_values, (int)$linear_array[$i]["id"]);
			array_push($binding_values, (int)$tree_list_id);
			array_push($binding_values, $linear_array[$i]["value"]);
			array_push($binding_values, 0);
			array_push($binding_values, 1);
			array_push($binding_values, 0);
			array_push($binding_values, (int)$order);
			array_push($binding_values, 1);
			
			if( $linear_array[$i]["alias"] == "" )
			{
				$linear_array[$i]["alias"] = null;
			}
			array_push($binding_values, htmlentities($linear_array[$i]["alias"], ENT_QUOTES, "UTF-8", false) );
			array_push($binding_values, htmlentities($linear_array[$i]["url"], ENT_QUOTES, "UTF-8", false) );
		}
		$SQL_items .= ";";
		$SQL_items_result = $db_link->prepare($SQL_items)->execute($binding_values);
		
		
		
		
		//СОХРАНЕНИЕ ИЗОБРАЖЕНИЙ
		$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/tree_lists_images/";
		$images_save_result = true;//Накопительный результат сохранения изображений
		for($i=0; $i < count($linear_array); $i++)
		{
			$FILE_POST = $_FILES["img_".$linear_array[$i]["id"]];
			
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
					exit;
				}
				
				
				//Имя файла будет вида <id элемента>.$file_extension
				$saved_file_name = $linear_array[$i]["id"].".".$file_extension;
				
				$uploadfile = $files_upload_dir.$saved_file_name;
				
				if (copy($FILE_POST['tmp_name'], $uploadfile))
				{
					if( $db_link->prepare("UPDATE `shop_tree_lists_items` SET `image` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $linear_array[$i]["id"]) ) != true)
					{
						$images_save_result = false;
					}
				} 
				else 
				{
					$images_save_result = false;
				}
			}
		}
		
		
		
		
		if($SQL_items_result == true)
		{
			$success_message = translate_str_by_id(3066);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $tree_list_id; ?>&parent_id=0&success_message=<?php echo $success_message; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$error_message = translate_str_by_id(3067);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/tree_list?tree_list_id=<?php echo $tree_list_id; ?>&parent_id=0&error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}
    }//~if($_POST["save_action"] == "create")
    else if($_POST["save_action"] == "edit")
    {
		//Мультиязычность. Кастомный алгоритм
		$caption = save_custom_translation($_POST["caption_lang_str_id"], htmlentities($_POST["caption"]));
		
		
        if( $db_link->prepare("UPDATE `shop_tree_lists` SET `caption` = ?, `data_type`=?  WHERE `id` = ?;")->execute( array($caption, htmlentities($_POST["data_type"]), (int)$tree_list_id) ) != true)
        {
            $error_message = translate_str_by_id(3077);
            ?>
            <script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $tree_list_id; ?>&parent_id=<?php echo $parent_id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
			$items_sql_no_error = true;//Флаг - нет ошибки при работе с элементами списка
			
			//Удаляем удаленные элементы
			// *********************************************************************************
			$items_to_del = "";//Строка с перечислением через запятую всех удаляемых элементов
			$items_query = $db_link->prepare("SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = ? AND `parent` = ?;");
			$items_query->execute( array($tree_list_id, $parent_id) );
			while( $item = $items_query->fetch() )
			{
				$deleted = true;//Флаг - элемент был удален при редактировании
				for($i=0; $i < count($linear_array); $i++)
				{
					//Если такой элемент есть в переданном массиве - не удаляем его из БД
					if($linear_array[$i]["id"] == $item["id"])
					{
						$deleted = false;
						break;//for
					}
				}
				
				//Если этот элемент удаляется
				if($deleted == true)
				{
					//Добавляем его в строку
					if($items_to_del != "")$items_to_del = $items_to_del.",";
					$items_to_del = $items_to_del.$item["id"];
					
					//Получаем все вложенные элементы
					$SQL_nested_items = "SELECT IFNULL(GROUP_CONCAT(Level SEPARATOR ','), '') AS `nested_ids` FROM (
						   SELECT @Ids := (
							   SELECT GROUP_CONCAT(`id` SEPARATOR ',')
							   FROM `shop_tree_lists_items`
							   WHERE FIND_IN_SET(`parent`, @Ids)
						   ) Level
						   FROM `shop_tree_lists_items`
						   JOIN (SELECT @Ids := ?) temp1
						   WHERE FIND_IN_SET(`parent`, @Ids)
						) temp2";
					
					$nested_items_query = $db_link->prepare($SQL_nested_items);
					$nested_items_query->execute( array($item["id"]) );
					$nested_items_record = $nested_items_query->fetch();
					if($nested_items_record["nested_ids"] != "")
					{
						$items_to_del = $items_to_del.",".$nested_items_record["nested_ids"];
					}
					
					//Уменьшаем количество элементов у родителя
					if( $parent_id != 0 )
					{
						if( ! $db_link->prepare("UPDATE `shop_tree_lists_items` SET `count` = `count`-1 WHERE `id` = ?;")->execute( array($parent_id) ) )
						{
							$items_sql_no_error = false;
						}
					}
				}
			}
			if($items_to_del != "")
			{
				if( ! $db_link->prepare("DELETE FROM `shop_tree_lists_items` WHERE `id` IN ($items_to_del);")->execute() )
				{
					$items_sql_no_error = false;
				}
			}
			// *********************************************************************************
			
			
			
			//Теперь обновляем элементы списка
			for($i=0; $i < count($linear_array); $i++)
			{
				$order = $i+1;
				
				if( $linear_array[$i]["alias"] == "" )
				{
					$linear_array[$i]["alias"] = null;
				}
				
				
				//Мультиязычность кастомный алгоритм
				$linear_array[$i]["value"] = save_custom_translation($linear_array[$i]["value_lang_str_id"], htmlentities($linear_array[$i]["value"], ENT_QUOTES, "UTF-8", false));
				
				
				//Новые элементы: добавляем
				if( $linear_array[$i]["is_new"] == 1 )
				{
					if( ! $db_link->prepare("INSERT INTO `shop_tree_lists_items` (`id`, `tree_list_id`, `value`, `count`, `level`, `parent`, `order`, `alias`, `url`) VALUES (?,?,?,?,?,?,?,?,?);")->execute( array( (int)$linear_array[$i]["id"], (int)$tree_list_id, $linear_array[$i]["value"], 0, (int)$level, (int)$parent_id, (int)$order, htmlentities($linear_array[$i]["alias"], ENT_QUOTES, "UTF-8", false), htmlentities($linear_array[$i]["url"], ENT_QUOTES, "UTF-8", false)) ) )
					{
						$items_sql_no_error = false;
					}
					else
					{
						//Прибавляем count родительского узла
						if( $parent_id != 0 )
						{
							if( ! $db_link->prepare("UPDATE `shop_tree_lists_items` SET `count` = `count`+1 WHERE `id` = ?;")->execute( array($parent_id) ) )
							{
								$items_sql_no_error = false;
							}
						}
					}
				}
				else//Старые элементы: обновляем
				{
					if( ! $db_link->prepare("UPDATE `shop_tree_lists_items` SET `value` = ?, `order` = ?, `alias` = ?, `url` = ? WHERE `id` = ?;")->execute( array( $linear_array[$i]["value"], (int)$order, htmlentities($linear_array[$i]["alias"], ENT_QUOTES, "UTF-8", false), htmlentities($linear_array[$i]["url"], ENT_QUOTES, "UTF-8", false), (int)$linear_array[$i]["id"]) ) )
					{
						$items_sql_no_error = false;
					}
				}
			}
			
			
			
			
			//СОХРАНЕНИЕ ИЗОБРАЖЕНИЙ
			$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/tree_lists_images/";
			$images_save_result = true;//Накопительный результат сохранения изображений
			for($i=0; $i < count($linear_array); $i++)
			{
				$FILE_POST = $_FILES["img_".$linear_array[$i]["id"]];
				
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
						exit;
					}
					
					
					//Имя файла будет вида <id элемента>.$file_extension
					$saved_file_name = $linear_array[$i]["id"].".".$file_extension;
					
					$uploadfile = $files_upload_dir.$saved_file_name;
					
					if (copy($FILE_POST['tmp_name'], $uploadfile))
					{
						if( $db_link->prepare("UPDATE `shop_tree_lists_items` SET `image` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $linear_array[$i]["id"]) ) != true)
						{
							$images_save_result = false;
						}
					} 
					else 
					{
						$images_save_result = false;
					}
				}
			}
			
			
			
			
			
			if($items_sql_no_error)
			{
				$success_message = translate_str_by_id(2877);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $tree_list_id; ?>&parent_id=<?php echo $parent_id; ?>&success_message=<?php echo $success_message; ?>";
				</script>
				<?php
				exit;
			}
			else
			{
				$error_message = translate_str_by_id(3070);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $tree_list_id; ?>&parent_id=<?php echo $parent_id; ?>&error_message=<?php echo $error_message; ?>";
				</script>
				<?php
				exit;
			}
        }
    }
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	/*
	//Получаем ID следующего добавляемого элемента
	$next_id_query = $db_link->prepare("SHOW TABLE STATUS LIKE 'shop_tree_lists_items'");
	$next_id_query->execute();
	$next_id_record = $next_id_query->fetch();
	if( $next_id_record == false )
	{
		exit("SQL error: next_id_query");
	}
    $next_id = $next_id_record["Auto_increment"];//ID следующего добавляемого элемента
	*/
	//Определяем следующий ID ($next_id)
	$table_name = "shop_tree_lists_items";
	$col_name = "id";//Имя колонки, в которой содержится id записей (обычно имя равно id)
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/lib/docpart/get_next_id.php");
	

    //Исходные переменные
    $page_title = translate_str_by_id(3078);
    $save_action_type = "create";//Тип действия при сохранении (создание/редактирование)
    $tree_list_id = 0;//ID списка
    $caption = "";
    $caption_lang_str_id = 0;
    $list_items = "[]";
    $data_type = "text";
    $parent_id = 0;
	$parent_url = "";
	$parent_parent = 0;
	
    //Передан аргумент - идет редактирование существующего списка
    if(!empty($_GET["tree_list_id"]))
    {
		$tree_list_id = (int)$_GET["tree_list_id"];//ID списка
		$page_title = translate_str_by_id(3072)." ";
        $save_action_type = "edit";//Тип действия при сохранении (создание/редактирование)
		$parent_id = (int)$_GET["parent_id"];
		
		//Получаем текущие данные списка
		$list_query = $db_link->prepare("SELECT * FROM `shop_tree_lists` WHERE `id` = ?;");
		$list_query->execute( array($tree_list_id) );
        $list_record = $list_query->fetch();
		$caption_lang_str_id = $list_record["caption"];
		$caption = translate_str_by_id($list_record["caption"]);
        $data_type = $list_record["data_type"];
		
		
		//Получаем данные родительского узла
		if($parent_id == 0)
		{
			$page_title .= "\"".$caption."\"";//Название списка без ссылки
			
			$page_title .= ", ".translate_str_by_id(3079);
			
			$parent_parent = 0;
		}
		else
		{
			//Название списка с ссылкой на переход к первому уровню
			$page_title .= "<a href=\"javascript:void(0);\" onclick=\"edit_breadcrumbs_brunch(0);\">"."\"".$caption."\""."</a>";
			
			
			//Получаем информацию по родительскому узлу
			$parent_info_query = $db_link->prepare("SELECT `level`, `value`, `parent`, `url` FROM `shop_tree_lists_items` WHERE `id` = ?;");
			$parent_info_query->execute( array($parent_id) );
			$parent_info = $parent_info_query->fetch();
			$parent_level = $parent_info["level"];
			$parent_value = translate_str_by_id($parent_info["value"]);
			$parent_parent = $parent_info["parent"];
			$parent_url = $parent_info["url"]."/";
			
			
			//Формируем хлебные крошки
			//$breadcrumbs = "<a href=\"javascript:void(0);\" onclick=\"edit_breadcrumbs_brunch(".$parent_id.");\">".$parent_value."</a>";
			$breadcrumbs = $parent_value;
			$upper_node_id = $parent_parent;
			for($i = $parent_level; $i > 0; $i--)
			{
				$breadcrumbs_query = $db_link->prepare("SELECT `value`, `parent` FROM `shop_tree_lists_items` WHERE `id` = ?;");
				$breadcrumbs_query->execute( array($upper_node_id) );
				$breadcrumbs_record = $breadcrumbs_query->fetch();
				
				$breadcrumbs = "<a href=\"javascript:void(0);\" onclick=\"edit_breadcrumbs_brunch(".$upper_node_id.");\">".translate_str_by_id($breadcrumbs_record["value"])."</a> > ".$breadcrumbs;

				
				
				$upper_node_id = $breadcrumbs_record["parent"];
			}
			
			$page_title = $page_title." ".$breadcrumbs;
		}
		
		
		//*********************************************************
		$list_items = array();
		//Получаем элементы ветви списка
		$items_query = $db_link->prepare("SELECT * FROM `shop_tree_lists_items` WHERE `parent` = ? AND `tree_list_id` = ? ORDER BY `order`;");
		$items_query->execute( array($parent_id,$tree_list_id) );
		$order = 1;//Порядок сдедования (нужно для определения несохраненных изменений)
		while( $item = $items_query->fetch() )
		{
			array_push($list_items, array("id"=>$item["id"], "value"=>translate_str_by_id($item["value"]), "value_lang_str_id"=>$item["value"], "alias"=>(string)$item["alias"], "value_start"=>translate_str_by_id($item["value"]), "i_count"=>$item["count"], "i_level"=>$item["level"], "i_parent"=>$item["parent"], "image"=>$item["image"], "order"=>$order) );
			$order++;
		}
		$list_items = json_encode($list_items);
		//*********************************************************
    }
    ?>
    
    
    <!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none" enctype="multipart/form-data">
        <input name="save_action" id="save_action" type="text" value="<?php echo $save_action_type; ?>" style="display:none"/>
        <input name="tree_json" id="tree_json" type="text" value="" style="display:none"/>
        <input name="tree_list_id" id="tree_list_id" type="text" value="<?php echo $tree_list_id; ?>" style="display:none"/>
        <input name="parent_id" id="parent_id" type="text" value="<?php echo $parent_id; ?>" style="display:none"/>
		<input name="caption" id="caption" type="text" value="" style="display:none"/>
		<input name="caption_lang_str_id" id="caption_lang_str_id" type="text" value="<?php echo $caption_lang_str_id; ?>" style="display:none"/>
        <input name="data_type" id="data_type" type="text" value="" style="display:none"/>
		
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
		
		<!-- Изображения загружаются с помощью input[type="file"], которые добавляются сюда при добавлении нового элемента -->
        <div id="img_box">
        </div>
    </form>
    <!--Форма для отправки-->
    
    
    
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" onClick="add_new_item();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<a class="panel_a" onClick="delete_selected_item();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				
				
				
				<?php
				if($parent_id != 0)
				{
					?>
					<a class="panel_a" onClick="edit_upper_brunch();" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/out.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(3080); ?></div>
					</a>
					<?php
				}
				?>

				
				<a class="panel_a" onClick="edit_brunch_of_item();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/in.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3081); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="unselect_tree();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/selection_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2268); ?></div>
				</a>
				
				
				
				<a class="panel_a" onClick="sort_items('asc');" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/sort_asc.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2881); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="sort_items('desc');" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/sort_desc.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2882); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="save_tree();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<?php
				if( $tree_list_id != 0 )
				{
					?>
					<a class="panel_a" onclick="show_tree_list();" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/monitor.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(3082); ?></div>
					</a>
					<?php
				}
				?>
				
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/tree_lists">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/tree_lists.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(783); ?></div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo $page_title; ?>
			</div>
			<div class="panel-body">
				<div id="container_A" style="height:350px;">
				</div>
			</div>
		</div>
	</div>
	
	
	<div class="col-lg-6" id="content_info_div_col">
		
		<div class="row">
		
			<div class="col-lg-12" id="content_info_div_col">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(2884); ?>
					</div>
					<div class="panel-body">
						
						
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2885); ?>
							</label>
							<div class="col-lg-6">
								<input type="text" id="caption_input" value="<?php echo $caption; ?>" class="form-control" />
							</div>
						</div>

						<div class="hr-line-dashed col-lg-12"></div>
						
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2899); ?>
							</label>
							<div class="col-lg-6">
								<select id="data_type_selector" class="form-control" >
									<option value="text"><?php echo translate_str_by_id(2901); ?></option>
									<option value="number"><?php echo translate_str_by_id(2902); ?></option>
								</select>
							</div>
						</div>
					</div>
				</div>
			</div>
			
			
			
			<div class="col-lg-12" id="selected_item_div">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(3073); ?>
					</div>
					<div class="panel-body" id="selected_item_div_options">
						
						<?php echo translate_str_by_id(3074); ?>
						
					</div>
				</div>
			</div>
		
		</div>
		
	</div>
	
	
	

    <script src="/lib/iso_9_js_master_translit/translit.js" type="text/javascript"></script>
    <script type="text/javascript" charset="utf-8">
	var next_id = <?php echo $next_id; ?>;//Следующий id
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
        	    var value_text = "<span>" + obj.value + " (<?php echo translate_str_by_id(2187); ?> " + obj.i_count + ")</span>";//Вывод текста
                
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
		//Если элементы не созданы
    	if(tree.count() == 0)
    	{
    	    document.getElementById("selected_item_div_options").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("selected_item_div").setAttribute("style", "display:none");
    	    return;
    	}
		
		
		//Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    document.getElementById("selected_item_div_options").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("selected_item_div").setAttribute("style", "display:none");
    	    return;
    	}
		
		
		//Показать контейнер для параметров
		document.getElementById("selected_item_div").setAttribute("style", "display:block");
		
		
		var node = "";//Ссылка на объект узла
    	//Выделенный узел
    	node = tree.getItem(node_id);
		
		
		var parameters_table_html = "";

		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+node.id+"</div></div>";
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+node.value+"</div></div>";
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2166); ?></label><div class=\"col-lg-6\"><input onkeyup=\"apply_options_for_item();\" type=\"text\" id=\"alias_input\" value=\""+node.alias+"\" class=\"form-control\" /></div></div>";
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		
		//Изображение
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2801); ?></label><div class=\"col-lg-6\"><button class=\"btn btn-success\" type=\"button\" onclick=\"document.getElementById('img_"+node.id+"').click();\"><i class=\"fa fa-file\"></i> <span class=\"bold\"><?php echo translate_str_by_id(2802); ?></span></button></div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"col-lg-12 text-center\" id=\"image_div\"></div>";
		
		document.getElementById("selected_item_div_options").innerHTML = parameters_table_html;
		
		//Выводим текущее изображение категории - для индикации
    	document.getElementById("image_div").innerHTML = "<img onerror = \"this.src = '<?php echo "/content/files/images/no_image.png"; ?>'\" src=\""+node.image_url+"\" style=\"max-width:96px; max-height:96px\" />";
		
    }//function onSelected()
	//-----------------------------------------------------
    //Обработка изменения файла для выбранного элемента
    var file_was_changed = false;//Флаг - было изменение файла (для определения несохраненных изменений)
	function onFileChanged()
    {
        //Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);

        var input_file = document.getElementById("img_"+node_id);//input для файла изображения
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
        
        
        //Создаем url файла для его отображения
        node.image_url = URL.createObjectURL(file);
    
        onSelected();
		
		file_was_changed = true;
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
        
		//Алиас автоматически заполняем транслитом ТОЛЬКО если его значение пустое
		if( node.alias == "" )
		{
			node.alias = iso_9_translit(node.value,  5);//5 - русский текст
			node.alias = node.alias.replace(/\s/g, '-');
			node.alias = node.alias.toLowerCase();
			node.alias = node.alias.replace(/[^\d\sA-Z\-_]/gi, '');//Убираем все символы кроме букв, цифр, тире и нинего подчеркивания
		}
		
		onSelected();
    });
    //-----------------------------------------------------
	//Обработчик После перетаскивания узлов дерева
	tree.attachEvent("onAfterDrop",function(){
	    onSelected();
	});
	//-----------------------------------------------------
	//Метод проверки существования повторяющихся значений атрибута alias на одном уровне
    //parent_id - родитель уровня; alias - проверяемое значение атрибута; except_node_id - узел, который не должен участвовать в проверке (например, когда сравнивается его собственное значение)
    function isAliasRepeated(parent, alias, except_node_id)
    {
        if(alias == "") return false;//Пустые значения вообще не проверяем
        
        if(parent == 0)//Работаем с узлами верхнего уровня
        {
            var first_id_same_level = tree.getFirstChildId(0);//Получаем Id самого первого узла дерева - он в любом случае на верхнем уровне
            var current_id = 0;//ID текущего проверяемого узла
            while(true)
            {
                //Сначала опрелеляем id текущего проверяемого узла
                if(current_id == 0)//Т.е. первая итерация цикла
                {
                    current_id = first_id_same_level;//Первый узел на уровне (в данном случае - первый узел дерева)
                }
                else
                {
                    current_id = tree.getNextSiblingId(current_id);//Получаем id следующего узла
                    if(current_id == null || current_id == false)//Больше узлов нет
                    {
                        break;
                    }
                }
                if(except_node_id == current_id)//Сам узел - пропускаем
                {
                    continue;
                }
                if(tree.getItem(current_id).$parent != 0)//Это может быть вложенный элемент (т.е. его вернул метод getNextSiblingId()). Он не должен проходить эту проверку, т.к. мы проверяем в данном случае только узлы верхнего уровня
                {
                    continue;
                }
                //Проверяемый узел подлежит проверке значения:
                var current_checked_node = tree.getItem(current_id);//Проверяемый узел
                if(current_checked_node.alias == alias)
                {
                    return true;//АТРИБУТ alias ПОВТОРЯЕТСЯ
                }
            }//~while(true)
        }//~if()
        else//Работаем с вложеженными узлами одного уровня одной ветви
        {
            var node_parent = tree.getItem(parent);//Родительский узел
            var first_id_same_level = tree.getFirstChildId(parent);//Получаем id первого узла на этом уровне в этой ветви
            var current_id = 0;//ID текущего проверяемого узла
            for(var i=0; i<node_parent.$count; i++)
            {
                //Сначала опрелеляем id текущего проверяемого узла
                if(i==0)
                {
                    current_id = first_id_same_level;//Первый узел на уровне
                }
                else
                {
                    current_id = tree.getNextSiblingId(current_id);//Получаем id следующего узла
                }
                if(except_node_id == current_id)//Проверяемый узел
                {
                    continue;
                }
                var current_checked_node = tree.getItem(current_id);//Проверяемый узел
                if(current_checked_node.alias == alias)
                {
                    return true;//АТРИБУТ alias ПОВТОРЯЕТСЯ
                }
            }//~for
        }
        
        return false;//Повторений атрибута не найдено
    }//~function isAliasRepeated(parent, alias, except_node_id = 0)
	//-----------------------------------------------------
	//Применить настройки для элемента
    function apply_options_for_item()
    {
        //1. Определяем выбранный элемент
        var node_id = tree.getSelectedId();//ID выделенного узла
		if(node_id == 0)
		{
			return;
		}

    	node = tree.getItem(node_id);//Выделенный узел

        //2. Сохраняем alias - это обязательное поле
		node.alias = document.getElementById("alias_input").value;
    }
    //-----------------------------------------------------
    //Добавить новый элемент в дерево
    function add_new_item()
    {
    	//Добавляем элемент в выделенный узел
    	//var parentId= tree.getSelectedId();//Выделеный узел
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(2908); ?>", value_lang_str_id:0, alias:"", url:"", value_start:"<?php echo translate_str_by_id(2908); ?>", id:next_id, is_new:true, image_url:"", i_count:0, order:0}, 0, 0);//Добавляем новый узел и запоминаем его ID
    	
		
		//Добавляем поле для изображения в форму:
    	var input_file = document.createElement("input");
        input_file.setAttribute("type","file");
        input_file.setAttribute("name","img_"+next_id);
        input_file.setAttribute("id","img_"+next_id);
        input_file.setAttribute("accept","image/jpeg,image/jpg,image/png,image/gif");
        input_file.setAttribute("onchange","onFileChanged();");
        document.getElementById('img_box').appendChild(input_file);
		
		
		
		onSelected();//Обработка текущего выделения
		next_id++;//Следующий ID материала
		//tree.open(parentId);//Раскрываем родительский узел
    }
    //-----------------------------------------------------
    //Удаление выделеного элемента
    var was_delete_action = false;//Флаг - было действие удаления элемента
	function delete_selected_item()
    {
    	var nodeId = tree.getSelectedId();
    	tree.remove(nodeId);
    	onSelected();
		
		was_delete_action = true;
    }
    //-----------------------------------------------------
    //Снятие выделения с дерева
    function unselect_tree()
    {
    	tree.unselect();
    	onSelected();
    }
    //-----------------------------------------------------
    //Упорядочить список
    function sort_items(dir)
    {
        var as = "string";//Сортировать, как [текст, число]
        var data_type = document.getElementById("data_type_selector").value;
        if(data_type == "number")
        {
            as = "int";
        }
        
        
        tree.sort('value', dir, as);
        onSelected();
    }
    //-----------------------------------------------------
    
    //Сохранение списка
    function save_tree()
    {
		//1. Проверить каждый узел дерева на предмет заполненности всех параметров
        //Если где-то не хватает данных - прервать метод и выдать сообщение об ошибке
        //В этом же методе одновременно проставляются полные url каждого элемента
        var tree_In_JSON = tree.serialize();//Получаем JSON-представление дерева
        if( ! checkEveryOneInArray(tree_In_JSON))//Передаем JSON представление дерева в рекурсивный метод проверки атрибутов
        {
            return false;
        }
		
		
    	//Задаем название списка:
    	var caption = document.getElementById("caption_input").value;
    	if(caption == "")
    	{
    	    alert("<?php echo translate_str_by_id(2909); ?>");
    	    return;
    	}
    	document.getElementById("caption").value = caption;
    	

    	//Задаем тип данных списка
    	document.getElementById("data_type").value = document.getElementById("data_type_selector").value;
    	
    	//Получаем строку JSON:
    	var tree_json_to_save = tree.serialize();
    	tree_dump = JSON.stringify(tree_json_to_save);
    	
    	//Задаем значение поля в форме:
    	var tree_json_input = document.getElementById("tree_json");
    	tree_json_input.value = tree_dump;
    	
    	document.forms["form_to_save"].submit();//Отправляем
    }
	//-----------------------------------------------------
	//Метод перебора всех элементов дерева - рекурсивный. Осуществляется проверка наличия значений атрибутов и простановка полных URL для каждого элемента
    function checkEveryOneInArray(level_array)
    {
        for(var i=0; i<level_array.length; i++)
        {
			//ПРОВЕРКА ЗАПОЛНЕНИЯ АЛИАС
			if(level_array[i]["alias"] == "")
			{
				webix.alert({
					title: "<?php echo translate_str_by_id(2122); ?>",
					text: "<div align='left'><b><?php echo translate_str_by_id(3075); ?> \""+level_array[i]["value"]+"\" (ID "+level_array[i]["id"]+") <?php echo translate_str_by_id(2289); ?></b>",
					type:"confirm-error"
				});
				return false;
			}
			
			//ПРОВЕРКА УНИКАЛЬНОСТИ АЛИАС
			var node_id_parent = level_array[i]["$parent"];
			if( isAliasRepeated(node_id_parent, level_array[i]["alias"], level_array[i]["id"] ) )
			{
				webix.alert({
					title: "<?php echo translate_str_by_id(2122); ?>",
					text: "<div align='left'><b><?php echo translate_str_by_id(3075); ?> \""+level_array[i]["value"]+"\" (ID "+level_array[i]["id"]+") <?php echo translate_str_by_id(3076); ?></b>",
					type:"confirm-error"
				});
				return false;
			}
			
            //Здесь можно поставить полный URL для данного материала (узла), т.к. он сам и элементы из его ветви всех уровней вышего него прошли проверку
            var node = tree.getItem(level_array[i]["id"]);//Получаем объект узла дерева
            node.url = "<?php echo $parent_url; ?>" + node.alias;
			/*if(node.$level == 1)//Для верхних элементов, их полные url равны их алиасам
            {
                node.url = node.alias;
            }
            else//Для вложенных элементов, их url равны <url родителя>+"/"+<свой алиас>
            {
                node.url = tree.getItem(node.$parent).url + "/" + node.alias;
            }*/
            
            /*
            //Рекурсивный вызов для вложенного уровня
            if(level_array[i]['$count'] > 0)
            {
                if(checkEveryOneInArray(level_array[i]["data"]) == false)
                {
                    return false;//Если метод вернул false - дальше проверять нет смысла - выходим
                }
            }*/
        }
        
        return true;
    }//~function checkEveryOneInArray(level_array)
    //-----------------------------------------------------
    //Рекурсивный метод инициализации полей image_url для каждого элемента после загрузки страницы
    function img_box_start_init(level_array)
    {
        for(var i=0; i<level_array.length; i++)
        {
            //Добавляем input - он пустой в любом случае, даже если изображение было добавлено при последнем редактировании
            var input_file = document.createElement("input");
            input_file.setAttribute("type","file");
            input_file.setAttribute("name","img_"+level_array[i]["id"]);
            input_file.setAttribute("id","img_"+level_array[i]["id"]);
            input_file.setAttribute("accept","image/jpeg,image/jpg,image/png,image/gif");
            input_file.setAttribute("onchange","onFileChanged();");
            document.getElementById('img_box').appendChild(input_file);
            
                      
            //Инициализируем image_url - будет использоваться скрипт для получения изображений каталога
            //level_array[i]["image_url"] = "<?php echo $DP_Config->domain_path; ?>content/shop/catalogue/get_category_image.php?id="+level_array[i]["id"];
            level_array[i]["image_url"] = "<?php echo $DP_Config->domain_path; ?>content/files/images/tree_lists_images/"+level_array[i]["image"];
            
            //Рекурсивный вызов для вложенного уровня
            /*
			if(level_array[i]['$count'] > 0)
            {
                img_box_start_init(level_array[i]["data"]);
            }
			*/
        }
    }//~function img_box_start_init(level_array)
    //-----------------------------------------------------
	//Функция перехода на редактирование ветви выделенного элемента
	function edit_brunch_of_item()
	{
		var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    alert("<?php echo translate_str_by_id(3083); ?>");
    	    return;
    	}
		
		var node = tree.getItem(node_id);
		if(node.is_new == true)
		{
			alert("<?php echo translate_str_by_id(3084); ?>");
    	    return;
		}
		
		
		//Проверка не сохраненных изменений
		if( is_not_saved() )
		{
			if( !confirm('<?php echo translate_str_by_id(3085); ?>') )
			{
				return;
			}
		}
		
		//alert("Переход на редактирование");
		
		location = "/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $tree_list_id; ?>&parent_id="+node_id;
	}
	//-----------------------------------------------------
	//Переход на редактирование на уровень выше
	function edit_upper_brunch()
	{
		if( <?php echo $parent_id; ?> == 0)
		{
			alert("<?php echo translate_str_by_id(3086); ?>");
			return;
		}
		
		
		if( is_not_saved() )
		{
			if( !confirm('<?php echo translate_str_by_id(3085); ?>') )
			{
				return;
			}
		}
		
		
		location = "/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $tree_list_id; ?>&parent_id=<?php echo $parent_parent; ?>";
	}
	//-----------------------------------------------------
	//Функция перехода на редактирование из хлебных крошек
	function edit_breadcrumbs_brunch(node_id)
	{
		if( is_not_saved() )
		{
			if( !confirm('<?php echo translate_str_by_id(3085); ?>') )
			{
				return;
			}
		}
		
		location = "/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $tree_list_id; ?>&parent_id="+node_id;
	}
	//-----------------------------------------------------
	//Переход на просмотр списка в асинхронном режиме
	function show_tree_list()
	{
		if(<?php echo (int)$tree_list_id; ?> == 0)
		{
			alert("<?php echo translate_str_by_id(3087); ?>");
			return;
		}
		
		
		if( is_not_saved() )
		{
			if( !confirm('<?php echo translate_str_by_id(3085); ?>') )
			{
				return;
			}
		}
		
		location = "/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/prosmotr-spiska?tree_list_id=<?php echo $tree_list_id; ?>";
	}
	//-----------------------------------------------------
	//Функция проверки наличия несохраненных изменений
	function is_not_saved()
	{
		//1. Проверяем название списка
		if( document.getElementById("caption_input").value != "<?php echo $caption; ?>" )
		{
			alert("<?php echo translate_str_by_id(3088); ?>");
			return true;
		}
		
		//2. Проверяем тип данных
		if( document.getElementById("data_type_selector").value != "<?php echo $data_type; ?>" )
		{
			alert("<?php echo translate_str_by_id(3089); ?>");
			return true;
		}
		
		
		//Перебираем все элементы
		var tree_data = tree.serialize();
		for(var i=0; i < tree_data.length; i++)
		{
			//3. Проверяем наличие новых элементов
			if(tree_data[i]["order"] == 0)//Это новый элемент (у новых всегда order = 0) - сразу понятно, что изменения не сохранены
			{
				alert("<?php echo translate_str_by_id(3090); ?>");
				return true;
			}
			
			//4. Проверяем порядок элементов
			if(tree_data[i]["order"] != (i+1) )
			{
				alert("<?php echo translate_str_by_id(3091); ?>");
				return true;
			}
			
			//4. Проверяем имена элементов
			if(tree_data[i]["value"] != tree_data[i]["value_start"] )
			{
				alert("<?php echo translate_str_by_id(3092); ?>");
				return true;
			}
		}
		
		
		
		
		//5. Проверяем наличие удаленных элементов
		if( was_delete_action )
		{
			alert("<?php echo translate_str_by_id(3093); ?>");
			return true;
		}
		
		
		//6. Проверяем изменение изображений
		if( file_was_changed  )
		{
			alert("<?php echo translate_str_by_id(3094); ?>");
			return true;
		}
		
		
		//Все проверки пройдены - несохраненных изменений нет
		return false;
	}
	//-----------------------------------------------------
    //Инициализация редактора дерева после загруки страницы
    function tree_start_init()
    {
    	var saved_list = <?php echo $list_items; ?>;
		img_box_start_init(saved_list);//Инициализируем изображения для элементов
	    tree.parse(saved_list);
	    //tree.openAll();
	    
	    //Выбираем текущий тип данных
	    document.getElementById("data_type_selector").value = '<?php echo $data_type; ?>';
    }
    tree_start_init();
    onSelected();//Обработка текущего выделения
    </script>
    
    
    <?php
}//~else//Действий нет - выводим страницу
?>