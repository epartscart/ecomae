<?php
/*
Страничный скрипт для редактирования полей регистрации
*/
defined('_ASTEXE_') or die('No access');



//Обработка древовидной структуры
function tree_htmlentities($data)
{
	foreach( $data AS $key => $item )
	{
		//Проверяем ключ
		if( htmlentities($key) != $key )
		{
			unset($data[$key]);
			continue;
		}
		
		
		if( is_array($item) )
		{
			$item = tree_htmlentities($item);
		}
		else
		{
			if( $item === (int)$item )
			{
				$item = (int)$item;
			}
			else
			{
				$item = htmlentities($item, ENT_QUOTES, "UTF-8", false);
			}
		}
		
		$data[$key] = $item;
	}
	
	return $data;
}
?>

<?php
if( !empty($_POST["save_action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Предварительно ставим поле order = 0, чтобы затем понять, какие поля были удалены
	if( $db_link->prepare("UPDATE `reg_fields` SET `order` = 0 WHERE `main_flag` != 1;")->execute() != true)
	{
		$error_message = translate_str_by_id(3868);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}
	
	
	
	$reg_fields = json_decode($_POST["tree_json"], true);
	
	
	$reg_fields = tree_htmlentities($reg_fields);
	
	
	for($i=0; $i < count($reg_fields); $i++)
	{
		$order = $i+1;
		
		$show_for = json_encode($reg_fields[$i]["show_for"]);
		$required_for = json_encode($reg_fields[$i]["required_for"]);
		
		
		//Мультиязычность. Кастомный алгоритм
		$reg_fields[$i]["value"] = save_custom_translation($reg_fields[$i]["value_lang_str_id"], $reg_fields[$i]["value"]);
		$reg_fields[$i]["example"] = save_custom_translation($reg_fields[$i]["example_lang_str_id"], $reg_fields[$i]["example"]);
		
		
		
		if($reg_fields[$i]["is_new"] == true)//Новые поля - добавляем
		{
			if( $db_link->prepare("INSERT INTO `reg_fields` (`main_flag`, `name`, `caption`, `show_for`, `required_for`, `maxlen`, `regexp`, `widget_type`, `widget_options`, `example`, `order`, `to_filter`, `to_users_table`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?);")->execute( array(0, $reg_fields[$i]["name"], $reg_fields[$i]["value"], $show_for, $required_for, $reg_fields[$i]["maxlen"], $reg_fields[$i]["regexp"], 'text', '[]', $reg_fields[$i]["example"], $order, $reg_fields[$i]["to_filter"], $reg_fields[$i]["to_users_table"]) ) != true)
			{
				$error_message = translate_str_by_id(3869);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo $error_message; ?>";
				</script>
				<?php
				exit;
			}
		}
		else
		{
			if( $db_link->prepare("UPDATE `reg_fields` SET `main_flag` = ?, `name` = ?, `caption` = ?, `show_for` = ?, `required_for` = ?, `maxlen` = ?, `regexp` = ?, `widget_type` = ?, `widget_options` = ?, `example` = ?, `order` = ?, `to_filter` = ?, `to_users_table` = ? WHERE `record_id` = ?;")->execute( array(0, $reg_fields[$i]["name"], $reg_fields[$i]["value"], $show_for, $required_for, $reg_fields[$i]["maxlen"], $reg_fields[$i]["regexp"], 'text', '[]', $reg_fields[$i]["example"], $order, $reg_fields[$i]["to_filter"], $reg_fields[$i]["to_users_table"], $reg_fields[$i]["id"]) ) != true)
			{
				$error_message = translate_str_by_id(3870);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo $error_message; ?>";
				</script>
				<?php
				exit;
			}
		}
	}
	
	
	//Теперь удаляем поля, которые были удалены при редактировании
	if( $db_link->prepare("DELETE FROM `reg_fields` WHERE `order` = 0 AND `main_flag` = 0;")->execute() != true)
	{
		$error_message = translate_str_by_id(3871);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/users/polya-registracii?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}
	
	
	$success_message = translate_str_by_id(2157);
	?>
	<script>
		location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/users/polya-registracii?success_message=<?php echo $success_message; ?>";
	</script>
	<?php
	exit;
	
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	//Получаем текущие поля регистрации
	$reg_fields = array();
	$reg_fields_query = $db_link->prepare("SELECT * FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC;");
	$reg_fields_query->execute();
	while( $reg_field = $reg_fields_query->fetch() )
	{
		$reg_field["caption_lang_str_id"] = $reg_field["caption"];
		$reg_field["caption"] = translate_str_by_id($reg_field["caption"]);
		
		$reg_field["example_lang_str_id"] = $reg_field["example"];
		$reg_field["example"] = translate_str_by_id($reg_field["example"]);
		
		array_push($reg_fields, array("id"=>$reg_field["record_id"], "to_filter"=>$reg_field["to_filter"], "to_users_table"=>$reg_field["to_users_table"],"is_new"=>0, "value"=>$reg_field["caption"], "value_lang_str_id"=>$reg_field["caption_lang_str_id"], "name"=>$reg_field["name"], "old_name"=>$reg_field["name"],"maxlen"=>$reg_field["maxlen"], "regexp"=>$reg_field["regexp"], "example"=>$reg_field["example"], "example_lang_str_id"=>$reg_field["example_lang_str_id"], "show_for"=>json_decode($reg_field["show_for"], true), "required_for"=>json_decode($reg_field["required_for"], true) ) );
	}
	$reg_fields = json_encode($reg_fields);
	
	
	//Получаем список регистрационных вариантов
	$reg_variants = array();
	$reg_variants_query = $db_link->prepare("SELECT * FROM `reg_variants` ORDER BY `order`;");
	$reg_variants_query->execute();
	while( $reg_variant = $reg_variants_query->fetch() )
	{
		$reg_variant["caption"] = translate_str_by_id($reg_variant["caption"]);
		
		array_push( $reg_variants, array("id"=>$reg_variant["id"], "value"=>$reg_variant["caption"], "is_new"=>0) );
	}
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
				

				<a class="panel_a" href="javascript:void(0);" onclick="save_tree();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3872); ?>
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
				<?php echo translate_str_by_id(3873); ?>
			</div>
			<div class="panel-body">
				<div id="content_info_div">
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	<!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none">
        <input name="save_action" id="save_action" type="text" value="ok" style="display:none"/>
        <input name="tree_json" id="tree_json" type="text" value="" style="display:none"/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!--Форма для отправки-->
	
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
	//Обработка выбора элемента
    function onSelected()
    {
        //Если элементы не созданы
    	if(tree.count() == 0)
    	{
    	    document.getElementById("content_info_div").innerHTML = "";
    	    document.getElementById("content_info_div_col").setAttribute("style", "display:none;");
    	    return;
    	}
    	
    	//Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    document.getElementById("content_info_div").innerHTML = "";
			document.getElementById("content_info_div_col").setAttribute("style", "display:none;");
    	    return;
    	}
    	
		document.getElementById("content_info_div_col").setAttribute("style", "display:block;");
		
    	var node = "";//Ссылка на объект узла
    	//Выделенный узел
    	node = tree.getItem(node_id);
    	
    	var parameters_table_html = "";
		var ID_for_show = node.id;
		if(node.is_new == true)
		{
			ID_for_show = "<?php echo translate_str_by_id(3874); ?>";
		}
		
		
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+ID_for_show+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		if( parseInt(node.to_filter) == 1 )
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3875); ?></label><div class=\"col-lg-6\"><input type=\"checkbox\" onChange=\"to_filter_handler();\" id=\"to_filter\" class=\"form-control\" checked=\"checked\" /></div></div>";
		}
		else
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3875); ?></label><div class=\"col-lg-6\"><input type=\"checkbox\" onChange=\"to_filter_handler();\" id=\"to_filter\" class=\"form-control\" /></div></div>";
		}
		
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		if( parseInt(node.to_users_table) == 1 )
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3876); ?></label><div class=\"col-lg-6\"><input type=\"checkbox\" onChange=\"to_users_table_handler();\" id=\"to_users_table\" class=\"form-control\" checked=\"checked\" /></div></div>";
		}
		else
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3876); ?></label><div class=\"col-lg-6\"><input type=\"checkbox\" onChange=\"to_users_table_handler();\" id=\"to_users_table\" class=\"form-control\" /></div></div>";
		}
		
		
		
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+node.value+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2460); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('name');\" id=\"name\" value=\""+node.name+"\" class=\"form-control\" /></div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3877); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('maxlen');\" id=\"maxlen\" value=\""+node.maxlen+"\" class=\"form-control\" /></div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3878); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('regexp');\" id=\"regexp\" value=\""+node.regexp+"\" class=\"form-control\" /></div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3879); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('example');\" id=\"example\" value=\""+node.example+"\" class=\"form-control\" /></div></div>";
		
		
		//Выводим таблицу привязки к регистрационным вариантам
		parameters_table_html += "<div class=\"col-lg-12 text-center\"><h3><?php echo translate_str_by_id(3880); ?></h3></div>";
		
		
		
		

		parameters_table_html += "<div class=\"table-responsive col-lg-12\"><table cellpadding=\"1\" cellspacing=\"1\" class=\"table table-condensed table-striped\">";
		parameters_table_html += "<thead><tr> <th><?php echo translate_str_by_id(3881); ?></th> <th><?php echo translate_str_by_id(3882); ?></th> <th><?php echo translate_str_by_id(3883); ?></th> </tr></thead><tbody>";
		<?php
		for($i=0; $i < count($reg_variants); $i++)
		{	
			?>
			var show_for = "";
			if(node.show_for.indexOf(<?php echo $reg_variants[$i]["id"]; ?>) >= 0)
			{
				show_for = "checked";
			}
			
			var required_for = "";
			if(node.required_for.indexOf(<?php echo $reg_variants[$i]["id"]; ?>) >= 0)
			{
				required_for = "checked";
			}
			
			parameters_table_html += "<tr> <td><?php echo $reg_variants[$i]["value"]; ?></td>";
			
			parameters_table_html += "<td class=\"text-center\"><input onchange=\"dynamicApplyingCheckboxes('show_for', <?php echo $reg_variants[$i]["id"]; ?>);\" type=\"checkbox\" id=\"show_for_input_<?php echo $reg_variants[$i]["id"]; ?>\" "+show_for+"/></td>";
			
			parameters_table_html += "<td class=\"text-center\"><input onchange=\"dynamicApplyingCheckboxes('required_for', <?php echo $reg_variants[$i]["id"]; ?>);\" type=\"checkbox\" id=\"required_for_input_<?php echo $reg_variants[$i]["id"]; ?>\" "+required_for+"/></td>";
			
			parameters_table_html += "</tr>";
			<?php
		}
		?>
		
		parameters_table_html += "</tbody></table></div>";
		
		
		console.log(parameters_table_html);
		
    	document.getElementById("content_info_div").innerHTML = parameters_table_html;
    }//function onSelected()
	//-----------------------------------------------------
	//Массив с регистрационными вариантами
	var reg_variants = new Object();
	<?php
	for($i=0; $i < count($reg_variants); $i++)
	{
		?>
		reg_variants["<?php echo $reg_variants[$i]["id"]; ?>"] = "<?php echo $reg_variants[$i]["value"]; ?>";
		<?php
	}
	?>
	//-----------------------------------------------------
	//Функция динамическиго применния значений
	function dynamicApplying(attribute)
	{
	    var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
    	
    	var str_value = document.getElementById(attribute).value;
    	
    	var str_handled = str_value.replace(/"/g, "&quot;");
    	
    	node[attribute] = str_handled;
	}
    //-----------------------------------------------------
	//Обработка выставления чекбокса "Вывести колонку в менеджер пользователей"
	function to_users_table_handler()
	{
		var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
		
		
		if(document.getElementById("to_users_table").checked)
		{
			node.to_users_table = 1;
		}
		else
		{
			node.to_users_table = 0;
		}
	}
	//-----------------------------------------------------
	//Обработка выставления чекбокса "Для фильтра"
	function to_filter_handler()
	{
		var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
		
		
		if(document.getElementById("to_filter").checked)
		{
			node.to_filter = 1;
		}
		else
		{
			node.to_filter = 0;
		}
	}
	//-----------------------------------------------------
	//Функция динамического применения чебоксов
	function dynamicApplyingCheckboxes(type, reg_variant_id)
	{
		var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
		

		if(type == "show_for")
		{
			if(document.getElementById("show_for_input_"+reg_variant_id).checked)
			{
				node.show_for.push(reg_variant_id);
			}
			else
			{
				node.show_for.splice(node.show_for.indexOf(reg_variant_id), 1);
			}
		}
		else //required_for
		{
			if(document.getElementById("required_for_input_"+reg_variant_id).checked)
			{
				node.required_for.push(reg_variant_id);
			}
			else
			{
				node.required_for.splice(node.required_for.indexOf(reg_variant_id), 1);
			}
		}
	}
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
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(2908); ?>", value_lang_str_id:0, is_new:true, name:"", maxlen:0, regexp:"", example:"", example_lang_str_id:0, show_for:[], required_for:[], to_filter:0, to_users_table:0}, 0, 0);//Добавляем новый узел и запоминаем его ID
    	
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
    //Сохранение списка
    function save_tree()
    {
    	//Получаем строку JSON:
    	var tree_json_to_save = tree.serialize();
		
		//Сначала проверяем на корректность значений
		var unique_keys = new Array();
		for(var i=0; i < tree_json_to_save.length; i++)
		{
			//1. Заполнение ключей
			if(tree_json_to_save[i].name == "")
			{
				alert("<?php echo translate_str_by_id(3884); ?>: "+tree_json_to_save[i].value+"");
				return;
			}
			
			//2. Соответствие ключей регулярному выражению:
			var current_value = tree_json_to_save[i].name;//Заполненное значение
			var regex = new RegExp("[a-z_]{1,}");//Регулярное выражение для поля
			//Далее ищем подстроку по регулярному выражению
			var match = regex.exec(String(current_value));
			if(match == null)
			{
				alert("<?php echo translate_str_by_id(3885); ?> "+tree_json_to_save[i].value+" <?php echo translate_str_by_id(3886); ?>");
				return false;
			}
			else
			{
				var match_value = String(match[0]);//Подходящая подстрока
				if(match_value != current_value)
				{
					alert("<?php echo translate_str_by_id(3887); ?> "+tree_json_to_save[i].value+" <?php echo translate_str_by_id(3888); ?>");
					return false;
				}
			}
			
			//2.1. Ключ не должен быть равен одному из следующих значений
			<?php
			//Нельзя использовать имена колонок из таблицы users
			$users_table_columns_query = $db_link->prepare("SELECT `COLUMN_NAME` FROM `INFORMATION_SCHEMA`.`COLUMNS` WHERE TABLE_NAME = 'users' AND `TABLE_SCHEMA` = '".$DP_Config->db."';");
			$users_table_columns_query->execute();
			while( $col_record =  $users_table_columns_query->fetch() )
			{
				?>
				if( String(tree_json_to_save[i].name) == "<?php echo $col_record['COLUMN_NAME']; ?>")
				{
					alert("<?php echo translate_str_by_id(3889); ?> "+tree_json_to_save[i].value+" <?php echo translate_str_by_id(3890); ?> (<?php echo $col_record['COLUMN_NAME']; ?>). <?php echo translate_str_by_id(3891); ?>.");
					return false;
				}
				<?php
				
			}
			?>
			
			//2.3. Название должно быть заполнено
			if( tree_json_to_save[i].value == "" )
			{
				alert("<?php echo translate_str_by_id(3892); ?>");
				return false;
			}
			
			//2.4. Частая логическая ошибка пользователей - поставить галку "Обязательно", и при этом не поставить "Показывать". Обрабатываем...
			for(var r=0; r < tree_json_to_save[i].required_for.length; r++)
			{
				if( tree_json_to_save[i].show_for.indexOf(tree_json_to_save[i].required_for[r]) < 0 )
				{
					alert("<?php echo translate_str_by_id(3893); ?> \""+tree_json_to_save[i].value+"\" <?php echo translate_str_by_id(3894); ?> \"<?php echo translate_str_by_id(3883); ?>\" <?php echo translate_str_by_id(3895); ?> \""+reg_variants[tree_json_to_save[i].required_for[r]]+"\", <?php echo translate_str_by_id(3896); ?> \"<?php echo translate_str_by_id(3882); ?>\" <?php echo translate_str_by_id(3897); ?>.");
					return false;
				}
			}
			
			
			//3. Уникальность ключей
			if( unique_keys.indexOf(tree_json_to_save[i].name) < 0 )
			{
				unique_keys.push(tree_json_to_save[i].name);
			}
			else
			{
				alert("<?php echo translate_str_by_id(3898); ?>: "+tree_json_to_save[i].name+". <?php echo translate_str_by_id(3899); ?>");
				return;
			}
			
			//2. Максимальная длина - целое число 
			if( !( !isNaN(parseFloat(tree_json_to_save[i].maxlen)) && isFinite(tree_json_to_save[i].maxlen) ) )
			{
				alert("<?php echo translate_str_by_id(2122); ?>! <?php echo translate_str_by_id(3893); ?>: "+tree_json_to_save[i].value+": <?php echo translate_str_by_id(3900); ?>");
				return;
			}
			else if( parseInt(tree_json_to_save[i].maxlen) != tree_json_to_save[i].maxlen )
			{
				alert("<?php echo translate_str_by_id(2122); ?>! <?php echo translate_str_by_id(3893); ?>: "+tree_json_to_save[i].value+": <?php echo translate_str_by_id(3900); ?>");
				return;
			}
			else if(tree_json_to_save[i].maxlen < 0)
			{
				alert("<?php echo translate_str_by_id(2122); ?>! <?php echo translate_str_by_id(3893); ?>: "+tree_json_to_save[i].value+": <?php echo translate_str_by_id(3900); ?>");
				return;
			}
			
			
			//3. Проверка на изменение ключа - выдать предупреждение
			if(tree_json_to_save[i].is_new == false)
			{
				if(tree_json_to_save[i].name != tree_json_to_save[i].old_name)
				{
					if( !confirm("<?php echo translate_str_by_id(3901); ?> "+tree_json_to_save[i].value+" <?php echo translate_str_by_id(3902); ?>: "+tree_json_to_save[i].old_name+"). <?php echo translate_str_by_id(3903); ?>") )
					{
						return;
					}
				}
			}
		}
		
    	tree_dump = JSON.stringify(tree_json_to_save);//Упаковываем в строку
    	
    	//Задаем значение поля в форме:
    	var tree_json_input = document.getElementById("tree_json");
    	tree_json_input.value = tree_dump;
    	
    	document.forms["form_to_save"].submit();//Отправляем
    }
    //-----------------------------------------------------
    
    //Инициализация редактора дерева после загруки страницы
    function tree_start_init()
    {
    	var saved_list = <?php echo $reg_fields; ?>;
	    tree.parse(saved_list);
	    tree.openAll();
    }
    tree_start_init();
    onSelected();//Обработка текущего выделения
    </script>
	
	
	<?php
}
?>