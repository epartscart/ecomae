<?php
/**
Страница управления одним древовидным списком (Создание/управление)
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/tree_lists/dp_tree_list_item.php");//Определение класса элемента древовидного списка
?>


<?php
// --------------------------------- Start PHP - метод ---------------------------------
//Рекурсивная функция для перевода иерархического массива (JSON перечня категорий) в линейный массив (просто набор объектов)
function getLinearListOfItems($hierarchy_array)
{
    $linear_array = array();//Линейный массив
    
    for($i=0; $i<count($hierarchy_array); $i++)
    {
        //Генерируем объект записи материала и заносим его в линейный массив
        $current_item = new DP_TreeListItem;
        $current_item->id = (int)$hierarchy_array[$i]["id"];
        $current_item->count = (int)$hierarchy_array[$i]['$count'];
        $current_item->level = (int)$hierarchy_array[$i]['$level'];
        $current_item->alias = htmlentities($hierarchy_array[$i]["alias"], ENT_QUOTES, "UTF-8", false);
		if( $current_item->alias == "" )
		{
			$current_item->alias = null;
		}
		$current_item->url = htmlentities($hierarchy_array[$i]["url"], ENT_QUOTES, "UTF-8", false);
        $current_item->parent = (int)$hierarchy_array[$i]['$parent'];
		$current_item->is_new = (int)$hierarchy_array[$i]['is_new'];
		if((bool)$hierarchy_array[$i]["open"] == true)
        {
            $current_item->open = 1;
        }
        else
        {
            $current_item->open = 0;
        }
		
		
		//Обработка мультиязычности
		$current_item->value = htmlentities($hierarchy_array[$i]["value"], ENT_QUOTES, "UTF-8", false);
        $current_item->value_lang_str_id = $hierarchy_array[$i]["value_lang_str_id"];
		$current_item->value = save_custom_translation($current_item->value_lang_str_id, $current_item->value);
		
		
        array_push($linear_array, $current_item);
        
        //Рекурсивный вызов для вложенного уровня
        if($hierarchy_array[$i]['$count'] > 0)
        {
            $data_linear_array = getLinearListOfItems($hierarchy_array[$i]["data"]);
            //Добавляем массив вложенного уровня к текущему
            for($j=0; $j<count($data_linear_array); $j++)
            {
                array_push($linear_array, $data_linear_array[$j]);
            }//for(j)
        }
    }//for(i)
    
    return $linear_array;
}//~function getLinearListOfItems($hierarchy_array)
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~






if(!empty($_POST["save_action"]))//Создание или редактирование
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Генерируем линейный массив на основе полученого иерархического
    $php_dump = json_decode($_POST["tree_json"], true);
    $linear_array = array();//Линейный массив материалов
    $linear_array = getLinearListOfItems($php_dump);//Генерируем линейный массив
	
    if($_POST["save_action"] == "create")
    {
		//Мультиязычность. Кастомный алгоритм
		$caption = save_custom_translation($_POST["caption_lang_str_id"], htmlentities($_POST["caption"]));
		
		
        if( $db_link->prepare("INSERT INTO `shop_tree_lists` (`caption`, `data_type`) VALUES (?, ?);")->execute( array( $caption, htmlentities($_POST["data_type"]) ) ) != true)
        {
            $error_message = translate_str_by_id(3065);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/tree_list?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        
        //Получаем ID созданного списка
		$id = $db_link->lastInsertId();
		
		//Добавляем элементы списка в таблицу элементов
		$SQL_items = "INSERT INTO `shop_tree_lists_items` (`id`, `tree_list_id`, `value`, `count`, `level`, `parent`, `order`, `open`, `alias`, `url`) VALUES ";
		$binding_values = array();
		for($i=0; $i < count($linear_array); $i++)
		{
			$order = $i+1;
			if($i > 0) $SQL_items .= ",";
			$SQL_items .= "(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
			
			array_push($binding_values, $linear_array[$i]->id);
			array_push($binding_values, $id);
			array_push($binding_values, $linear_array[$i]->value);
			array_push($binding_values, $linear_array[$i]->count);
			array_push($binding_values, $linear_array[$i]->level);
			array_push($binding_values, $linear_array[$i]->parent);
			array_push($binding_values, $order);
			array_push($binding_values, $linear_array[$i]->open);
			array_push($binding_values, $linear_array[$i]->alias);
			array_push($binding_values, $linear_array[$i]->url);
		}
		$SQL_items .= ";";
		$SQL_items_result = $db_link->prepare($SQL_items)->execute($binding_values);
		
		
		
		
		//СОХРАНЕНИЕ ИЗОБРАЖЕНИЙ
		$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/tree_lists_images/";
		$images_save_result = true;//Накопительный результат сохранения изображений
		for($i=0; $i < count($linear_array); $i++)
		{
			$FILE_POST = $_FILES["img_".$linear_array[$i]->id];
			
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
				$saved_file_name = $linear_array[$i]->id.".".$file_extension;
				
				$uploadfile = $files_upload_dir.$saved_file_name;
				
				if (copy($FILE_POST['tmp_name'], $uploadfile))
				{
					if( $db_link->prepare("UPDATE `shop_tree_lists_items` SET `image` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $linear_array[$i]->id) ) != true)
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
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/tree_list?id=<?php echo $id; ?>&success_message=<?php echo $success_message; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$error_message = translate_str_by_id(3067);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/tree_list?id=<?php echo $id; ?>&error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}
    }//~if($_POST["save_action"] == "create")
    else if($_POST["save_action"] == "edit")
    {
        $id = (int)$_POST["id"];
        
		
		//Мультиязычность. Кастомный алгоритм
		$caption = save_custom_translation($_POST["caption_lang_str_id"], htmlentities($_POST["caption"]));
		
		
        if( $db_link->prepare("UPDATE `shop_tree_lists` SET `caption` = ?, `data_type`=?  WHERE `id` = ?;")->execute( array($caption, htmlentities($_POST["data_type"]), $id) ) != true)
        {
            $error_message = translate_str_by_id(3068);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/tree_list?id=<?php echo $id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
			$items_sql_no_error = true;//Флаг - нет ошибки при работе с элементами списка
			
			//Удаляем удаленные элементы
			$items_query = $db_link->prepare("SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = ?;");
			$items_query->execute( array($id) );
			while( $item = $items_query->fetch() )
			{
				$deleted = true;//Флаг - элемент был удален при редактировании
				for($i=0; $i < count($linear_array); $i++)
				{
					//Если такой элемент есть в переданном массиве - не удаляем его из БД
					if($linear_array[$i]->id == $item["id"])
					{
						$deleted = false;
						break;//for
					}
				}
				
				if($deleted)
				{
					if( ! $db_link->prepare("DELETE FROM `shop_tree_lists_items` WHERE `id` = ?;")->execute( array($item["id"]) ) )
					{
						$items_sql_no_error = false;
					}
				}
			}
			
			
			
			//Теперь обновляем элементы списка
			for($i=0; $i < count($linear_array); $i++)
			{
				$order = $i+1;
				
				//Новые элементы: добавляем
				if( $linear_array[$i]->is_new == 1 )
				{
					if( ! $db_link->prepare("INSERT INTO `shop_tree_lists_items` (`id`, `tree_list_id`, `value`, `count`, `level`, `parent`, `order`, `open`, `alias`, `url`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);")->execute( array($linear_array[$i]->id, $id, $linear_array[$i]->value, $linear_array[$i]->count, $linear_array[$i]->level, $linear_array[$i]->parent, $order, $linear_array[$i]->open, $linear_array[$i]->alias, $linear_array[$i]->url) ) )
					{
						$items_sql_no_error = false;
					}
				}
				else//Старые элементы: обновляем
				{
					if( ! $db_link->prepare("UPDATE `shop_tree_lists_items` SET `value` = ?, `count` = ?, `level` = ?, `parent` = ?, `order` = ?, `open` = ?, `alias` = ?, `url` = ? WHERE `id` = ?;")->execute( array($linear_array[$i]->value, $linear_array[$i]->count, $linear_array[$i]->level, $linear_array[$i]->parent, $order, $linear_array[$i]->open, $linear_array[$i]->alias, $linear_array[$i]->url, $linear_array[$i]->id) ) )
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
				$FILE_POST = $_FILES["img_".$linear_array[$i]->id];
				
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
					$saved_file_name = $linear_array[$i]->id.".".$file_extension;
					
					$uploadfile = $files_upload_dir.$saved_file_name;
					
					if (copy($FILE_POST['tmp_name'], $uploadfile))
					{
						if( $db_link->prepare("UPDATE `shop_tree_lists_items` SET `image` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $linear_array[$i]->id) ) != true)
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
				$success_message = translate_str_by_id(3069);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/tree_list?id=<?php echo $id; ?>&success_message=<?php echo $success_message; ?>";
				</script>
				<?php
				exit;
			}
			else
			{
				$error_message = translate_str_by_id(3070);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/tree_list?id=<?php echo $id; ?>&error_message=<?php echo $error_message; ?>";
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
    $page_title = translate_str_by_id(3071);
    $save_action_type = "create";//Тип действия при сохранении (создание/редактирование)
    $list_id = 0;//ID списка
    $caption = "";
    $caption_lang_str_id = 0;
    $list_items = "[]";
    $data_type = "text";
    
	
    //Передан аргумент - идет редактирование существующего списка
    if(!empty($_GET["id"]))
    {
		$list_id = $_GET["id"];//ID списка
		$page_title = translate_str_by_id(3072);
        $save_action_type = "edit";//Тип действия при сохранении (создание/редактирование)
		
		
		//Получаем текущие данные списка
		$list_query = $db_link->prepare("SELECT * FROM `shop_tree_lists` WHERE `id` = ?;");
		$list_query->execute( array($list_id) );
        $list_record = $list_query->fetch();
		$caption_lang_str_id = $list_record["caption"];
		$caption = translate_str_by_id($list_record["caption"]);
        $data_type = $list_record["data_type"];
		
		
		//*********************************************************
		$needed_tree_list_id = $list_id;//Указываем ID древовидного списка, который требуется получить
        require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/tree_lists/get_tree_list_items.php");//Получение объекта иерархии указанного древовидного списка
		$list_items = $tree_list_dump_JSON;
		//*********************************************************
    }
    ?>
    
    
    <!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none" enctype="multipart/form-data">
        <input name="save_action" id="save_action" type="text" value="<?php echo $save_action_type; ?>" style="display:none"/>
        <input name="tree_json" id="tree_json" type="text" value="" style="display:none"/>
        <input name="id" id="id" type="text" value="<?php echo $list_id; ?>" style="display:none"/>
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
				<?php echo translate_str_by_id(2883); ?>
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
		parameters_table_html += "<div class=\"col-lg-12 text-center\" id=\"image_div\"></div>";
		
		document.getElementById("selected_item_div_options").innerHTML = parameters_table_html;
		
		//Выводим текущее изображение категории - для индикации
    	document.getElementById("image_div").innerHTML = "<img onerror = \"this.src = '<?php echo "/content/files/images/no_image.png"; ?>'\" src=\""+node.image_url+"\" style=\"max-width:96px; max-height:96px\" />";
		
    }//function onSelected()
	//-----------------------------------------------------
    //Обработка изменения файла для выбранного элемента
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
    //Добавить новый элемент в дерево
    function add_new_item()
    {
    	//Добавляем элемент в выделенный узел
    	var parentId= tree.getSelectedId();//Выделеный узел
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(2908); ?>", value_lang_str_id:0, alias:"", id:next_id, is_new:true, image_url:""}, 0, parentId);//Добавляем новый узел и запоминаем его ID
    	
		
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
		tree.open(parentId);//Раскрываем родительский узел
    }
    //-----------------------------------------------------
    //Удаление выделеного элемента
    function delete_selected_item()
    {
    	var nodeId = tree.getSelectedId();
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
		//console.log(tree_In_JSON);
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
    	
		//console.log(tree_dump);
		//return;
		
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
			if(level_array[i]["alias"] == "")
			{
				var node = tree.getItem(level_array[i]["id"]);//Получаем объект узла дерева
				
				node.alias = iso_9_translit(node.value,  5);//5 - русский текст
				node.alias = node.alias.replace(/\s/g, '-');
				node.alias = node.alias.toLowerCase();
				node.alias = node.alias.replace(/[^\d\sA-Z\-_]/gi, '');
				
				level_array[i]["alias"] = node.alias;
				
				var node_id_parent = level_array[i]["$parent"];
				if( isAliasRepeated(node_id_parent, level_array[i]["alias"], level_array[i]["id"] ) )
				{
					node.alias = node.alias + '-cargo';
					level_array[i]["alias"] = node.alias;
				}
			}
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
            if(node.$level == 1)//Для верхних элементов, их полные url равны их алиасам
            {
                node.url = node.alias;
            }
            else//Для вложенных элементов, их url равны <url родителя>+"/"+<свой алиас>
            {
                node.url = tree.getItem(node.$parent).url + "/" + node.alias;
            }
            
            
            //Рекурсивный вызов для вложенного уровня
            if(level_array[i]['$count'] > 0)
            {
                if(checkEveryOneInArray(level_array[i]["data"]) == false)
                {
                    return false;//Если метод вернул false - дальше проверять нет смысла - выходим
                }
            }
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
            if(level_array[i]['$count'] > 0)
            {
                img_box_start_init(level_array[i]["data"]);
            }
        }
    }//~function img_box_start_init(level_array)
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