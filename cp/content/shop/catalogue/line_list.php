<?php
/**
 * Страница управления одним справочником
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
	
	
	//Элементы списка
	$list_items = json_decode($_POST["tree_json"], true);
	
    if($_POST["save_action"] == "create")
    {
		//Мультиязычность. Кастомный алгоритм
		$caption = save_custom_translation(0, htmlentities($_POST["caption"]));
		
		
        if( $db_link->prepare("INSERT INTO `shop_line_lists` (`caption`, `type`, `data_type`, `auto_sort`) VALUES (?, ?, ?, ?);")->execute( array( $caption, (int)$_POST["type"], htmlentities($_POST["data_type"]), htmlentities($_POST["auto_sort"])) ) != true)
        {
            $error_message = translate_str_by_id(2873);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists/line_list?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        
        //Получаем ID созданного списка
		$id = $db_link->lastInsertId();
		
		//Добавляем элементы списка в таблицу элементов
		$SQL_items = "INSERT INTO `shop_line_lists_items` (`line_list_id`, `value`, `order`) VALUES ";
		$binding_values = array();
		for($i=0; $i < count($list_items); $i++)
		{
			$order = $i+1;
			if($i > 0) $SQL_items .= ",";
			$SQL_items .= "(?, ?, ?)";
			
			
			//Мультиязычность. Кастомный алгоритм
			$value = save_custom_translation(0, htmlentities($list_items[$i]["value"]) );
			
			
			array_push($binding_values, (int)$id);
			array_push($binding_values, $value);
			array_push($binding_values, $order);
		}
		$SQL_items .= ";";
		

		if( $db_link->prepare($SQL_items)->execute($binding_values) == true)
		{
			$success_message = translate_str_by_id(2874);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists/line_list?id=<?php echo $id; ?>&success_message=<?php echo $success_message; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$error_message = translate_str_by_id(2875);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists/line_list?id=<?php echo $id; ?>&error_message=<?php echo $error_message; ?>";
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
		
		
		if( $db_link->prepare("UPDATE `shop_line_lists` SET `caption` = ?, `type` = ?, `data_type`=?, `auto_sort`=?  WHERE `id` = ?;")->execute( array( $caption, (int)$_POST["type"], htmlentities($_POST["data_type"]), htmlentities($_POST["auto_sort"]), (int)$id) ) != true)
        {
            $error_message = translate_str_by_id(2876);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists/line_list?id=<?php echo $id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
			$items_sql_no_error = true;//Флаг - нет ошибки при работе с элементами списка
			
			//Удаляем удаленные элементы
			$items_query = $db_link->prepare("SELECT * FROM `shop_line_lists_items` WHERE `line_list_id` = ?;");
			$items_query->execute( array($id) );
			while( $item = $items_query->fetch() )
			{
				$deleted = true;//Флаг - элемент был удален при редактировании
				for($i=0; $i < count($list_items); $i++)
				{
					//Если такой элемент есть в переданном массиве - не удаляем его из БД
					if($list_items[$i]["id"] == $item["id"])
					{
						$deleted = false;
						break;//for
					}
				}
				
				if($deleted)
				{
					if( ! $db_link->prepare("DELETE FROM `shop_line_lists_items` WHERE `id` = ?;")->execute( array( (int)$item["id"] ) ) )
					{
						$items_sql_no_error = false;
					}
				}
			}
			
			
			
			//Теперь обновляем элементы списка
			for($i=0; $i < count($list_items); $i++)
			{
				$order = $i+1;
				
				//Мультиязычность. Кастомный алгоритм
				$list_items[$i]["value"] = save_custom_translation($list_items[$i]["value_lang_str_id"], htmlentities($list_items[$i]["value"]) );
				
				
				//Новые элементы: добавляем
				if( !empty($list_items[$i]["is_new"]) )
				{
					if( ! $db_link->prepare("INSERT INTO `shop_line_lists_items` (`line_list_id`, `value`, `order`) VALUES (?, ?, ?);")->execute( array( (int)$id, $list_items[$i]["value"], $order) ) )
					{
						$items_sql_no_error = false;
					}
				}
				else//Старые элементы: обновляем value и order
				{
					if( ! $db_link->prepare("UPDATE `shop_line_lists_items` SET `value` = ?, `order` = ? WHERE `id` = ?;")->execute( array( $list_items[$i]["value"], $order, (int)$list_items[$i]["id"]) ) )
					{
						$items_sql_no_error = false;
					}
				}
			}
			
			
			if($items_sql_no_error)
			{
				$success_message = translate_str_by_id(2877);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists/line_list?id=<?php echo $id; ?>&success_message=<?php echo $success_message; ?>";
				</script>
				<?php
				exit;
			}
			else
			{
				$error_message = translate_str_by_id(2878);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/line_lists/line_list?id=<?php echo $id; ?>&error_message=<?php echo $error_message; ?>";
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
	
    //Исходные переменные
    $page_title = translate_str_by_id(2879);
    $save_action_type = "create";//Тип действия при сохранении (создание/редактирование)
    $list_id = 0;//ID списка
    $caption = "";
    $caption_lang_str_id = 0;
    $type = 1;
    $list_items = "[]";
    $data_type = "text";
    $auto_sort = "no";
    
    //Передан аргумент - идет редактирование существующего справочника
    if(!empty($_GET["id"]))
    {
		$list_id = $_GET["id"];//ID списка
		$page_title = translate_str_by_id(2880);
        $save_action_type = "edit";//Тип действия при сохранении (создание/редактирование)
		
        //Получаем текущие данные списка
        $list_query = $db_link->prepare("SELECT * FROM `shop_line_lists` WHERE `id` = ?;");
		$list_query->execute( array($list_id) );
        $list_record = $list_query->fetch();
		$caption_lang_str_id = $list_record["caption"];
		$caption = translate_str_by_id($list_record["caption"]);
        $type = $list_record["type"];
        $data_type = $list_record["data_type"];
        $auto_sort = $list_record["auto_sort"];
		
		//Получаем элементы линейного списка:
		$list_items = array();
		
		switch($auto_sort){
			case 'asc' :
			$list_items_query = $db_link->prepare('SELECT * FROM `shop_line_lists_items` WHERE `line_list_id` = ? ORDER BY `value` ASC;');
			break;
			case 'desc' :
			$list_items_query = $db_link->prepare('SELECT * FROM `shop_line_lists_items` WHERE `line_list_id` = ? ORDER BY `value` DESC;');
			break;
			default :
			$list_items_query = $db_link->prepare('SELECT * FROM `shop_line_lists_items` WHERE `line_list_id` = ? ORDER BY `order`;');
			break;
		}
		
		$list_items_query->execute( array($list_id) );
		while( $list_item = $list_items_query->fetch() )
		{
			array_push( $list_items, array("id"=>$list_item["id"], "value"=>translate_str_by_id($list_item["value"]), "value_lang_str_id"=>$list_item["value"]) );
		}
		$list_items = json_encode($list_items);
    }
    ?>
    
    
    <!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none">
        <input name="save_action" id="save_action" type="text" value="<?php echo $save_action_type; ?>" style="display:none"/>
        <input name="tree_json" id="tree_json" type="text" value="" style="display:none"/>
        <input name="id" id="id" type="text" value="<?php echo $list_id; ?>" style="display:none"/>
        <input name="caption" id="caption" type="text" value="" style="display:none"/>
        <input name="caption_lang_str_id" id="caption_lang_str_id" type="text" value="<?php echo $caption_lang_str_id; ?>" style="display:none"/>
        <input name="type" id="type" type="text" value="" style="display:none"/>
        <input name="data_type" id="data_type" type="text" value="" style="display:none"/>
        <input name="auto_sort" id="auto_sort" type="text" value="" style="display:none"/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
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
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/line_lists">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/list.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(759); ?></div>
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
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2884); ?>
			</div>
			<div class="panel-body">
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2885); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(2886); ?> <b>&quot;<?php echo translate_str_by_id(2887); ?>&quot;</b> <?php echo translate_str_by_id(2888); ?> <b>&quot;<?php echo translate_str_by_id(2889); ?>&quot;</b>');"><i class="fa fa-info"></i></button>
					</label>
					<div class="col-lg-6">
						<input type="text" id="caption_input" value="<?php echo $caption; ?>" class="form-control" />
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2890); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(2891); ?><br><br><?php echo translate_str_by_id(2892); ?> <b>&quot;<?php echo translate_str_by_id(2893); ?>&quot;</b> <?php echo translate_str_by_id(2894); ?><br><br><?php echo translate_str_by_id(2892); ?> <b>&quot;<?php echo translate_str_by_id(2895); ?>&quot;</b> <?php echo translate_str_by_id(2896); ?>');"><i class="fa fa-info"></i></button>
					</label>
					<div class="col-lg-6">
						<select id="type_selector" class="form-control" >
							<option value="1"><?php echo translate_str_by_id(2897); ?></option>
							<option value="2"><?php echo translate_str_by_id(2898); ?></option>
						</select>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2899); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(2900); ?>');"><i class="fa fa-info"></i></button>
					</label>
					<div class="col-lg-6">
						<select id="data_type_selector" class="form-control" >
							<option value="text"><?php echo translate_str_by_id(2901); ?></option>
							<option value="number"><?php echo translate_str_by_id(2902); ?></option>
						</select>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2903); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(2904); ?>');"><i class="fa fa-info"></i></button>
					</label>
					<div class="col-lg-6">
						<select id="auto_sort_selector" class="form-control" >
							<option value="no"><?php echo translate_str_by_id(2905); ?></option>
							<option value="asc"><?php echo translate_str_by_id(2906); ?></option>
							<option value="desc"><?php echo translate_str_by_id(2907); ?></option>
						</select>
					</div>
				</div>

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
    }//function onSelected()
    //-----------------------------------------------------
    //Событие при успешном редактировании элемента дерева
    tree.attachEvent("onValidationSuccess", function(){
        onSelected();
    });
    //-----------------------------------------------------
    tree.attachEvent("onAfterEditStop", function(state, editor, ignoreUpdate){
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
    	var parentId= tree.getSelectedId();//Выделеный узел
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(2908); ?>", value_lang_str_id:0, is_new:true}, 0, 0);//Добавляем новый узел и запоминаем его ID
    	
    	onSelected();//Обработка текущего выделения
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
    	//Задаем название списка:
    	var caption = document.getElementById("caption_input").value;
    	if(caption == "")
    	{
    	    alert("<?php echo translate_str_by_id(2909); ?>");
    	    return;
    	}
    	document.getElementById("caption").value = caption;
    	
    	
    	//Задаем тип списка
    	document.getElementById("type").value = document.getElementById("type_selector").value;
    	
    	//Задаем тип данных списка
    	document.getElementById("data_type").value = document.getElementById("data_type_selector").value;
    	
    	//Задаем "Автосортировка при редактировании товара"
    	document.getElementById("auto_sort").value = document.getElementById("auto_sort_selector").value;
    	
    	//Получаем строку JSON:
    	var tree_json_to_save = tree.serialize();
    	tree_dump = JSON.stringify(tree_json_to_save);
    	
    	//Задаем значение поля в форме:
    	var tree_json_input = document.getElementById("tree_json");
    	tree_json_input.value = tree_dump;
    	
    	document.forms["form_to_save"].submit();//Отправляем
    }
    //-----------------------------------------------------
    
    //Инициализация редактора дерева после загруки страницы
    function tree_start_init()
    {
    	var saved_list = <?php echo $list_items; ?>;
	    tree.parse(saved_list);
	    tree.openAll();
	    
	    //Выбираем текущую специфику
	    document.getElementById("type_selector").value = '<?php echo $type; ?>';
	    
	    //Выбираем текущий тип данных
	    document.getElementById("data_type_selector").value = '<?php echo $data_type; ?>';
	    
	    //Выбираем настройку "Автосортировка при редактировании товара"
	    document.getElementById("auto_sort_selector").value = '<?php echo $auto_sort; ?>';
    }
    tree_start_init();
    onSelected();//Обработка текущего выделения
    </script>
    
    
    <?php
}//~else//Действий нет - выводим страницу
?>