<?php

/*
Страничный скрипт для редактирования полей регистрации
*/
/*
Мультиязычность.

Здесть идет редактирование мультиязычных строк:
- название поля
- пример для заполнения
- регулярное выражение
*/
$translated_items = array('value', 'example', 'regexp');

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
	if( $db_link->prepare("UPDATE `vin_fields` SET `order` = 0;")->execute() != true)
	{
		$error_message = translate_str_by_id(3868);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/requests/polya-vin-zaprosa?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}

	$vin_fields = json_decode($_POST["tree_json"], true);

	$vin_fields = tree_htmlentities($vin_fields);

	for($i=0; $i < count($vin_fields); $i++)
	{
        
		$order = $i+1;
		
		$show = $vin_fields[$i]["show"];
		$required = $vin_fields[$i]["required"];
		
		
		//Мультиязычность. Кастомный алгоритм
		for($c = 0; $c < count($translated_items); $c++)
		{
			$vin_fields[$i][$translated_items[$c]] = save_custom_translation($vin_fields[$i][$translated_items[$c]."_lang_str_id"], $vin_fields[$i][$translated_items[$c]] );
			
			if( $vin_fields[$i][$translated_items[$c]] == 0 )
			{
				exit('Error save_custom_translation('.$translated_items[$c].')');
			}
		}
		
		
		if($vin_fields[$i]["is_new"] == true)//Новые поля - добавляем
		{
			if( $db_link->prepare("INSERT INTO `vin_fields` (`main_flag`, `name`, `caption`, `show`, `required`, `maxlen`, `regexp`, `widget_type`, `widget_options`, `example`, `order`) VALUES (?,?,?,?,?,?,?,?,?,?,?);")->execute( array(0, $vin_fields[$i]["name"], $vin_fields[$i]["value"], $show, $required, $vin_fields[$i]["maxlen"], $vin_fields[$i]["regexp"], 'text', '[]', $vin_fields[$i]["example"], $order) ) != true)
			{
				$error_message = translate_str_by_id(3869);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/requests/polya-vin-zaprosa?error_message=<?php echo $error_message; ?>";
				</script>
				<?php
				exit;
			}
		}
		else
		{
			if( $db_link->prepare("UPDATE `vin_fields` SET `main_flag` = ?, `name` = ?, `caption` = ?, `show` = ?, `required` = ?, `maxlen` = ?, `regexp` = ?, `widget_type` = ?, `widget_options` = ?, `example` = ?, `order` = ? WHERE `record_id` = ?;")->execute( array(0, $vin_fields[$i]["name"], $vin_fields[$i]["value"], $show, $required, $vin_fields[$i]["maxlen"], $vin_fields[$i]["regexp"], 'text', '[]', $vin_fields[$i]["example"], $order, $vin_fields[$i]["id"]) ) != true)
			{
				$error_message = translate_str_by_id(3870);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/requests/polya-vin-zaprosa?error_message=<?php echo $error_message; ?>";
				</script>
				<?php
				exit;
			}
		}
	}

	//Теперь удаляем поля, которые были удалены при редактировании
	if( $db_link->prepare("DELETE FROM `vin_fields` WHERE `order` = 0;")->execute() != true)
	{
		$error_message = translate_str_by_id(3871);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/requests/polya-vin-zaprosa?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}

	$success_message = translate_str_by_id(2157);
	?>
	<script>
		location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/requests/polya-vin-zaprosa?success_message=<?php echo $success_message; ?>";
	</script>
	<?php
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	//Получаем текущие поля регистрации
	$vin_fields = array();
	$vin_fields_query = $db_link->prepare("SELECT * FROM `vin_fields` ORDER BY `order` ASC;");
	$vin_fields_query->execute();
	while( $vin_field = $vin_fields_query->fetch() )
	{
		array_push($vin_fields, array("id"=>$vin_field["record_id"], "is_new"=>0, "value"=>translate_str_by_id($vin_field["caption"]), "name"=>$vin_field["name"], "old_name"=>$vin_field["name"],"maxlen"=>$vin_field["maxlen"], "regexp"=>translate_str_by_id($vin_field["regexp"]), "example"=>translate_str_by_id($vin_field["example"]), "show"=>$vin_field["show"], "required"=>$vin_field["required"] ) );
		
		
		//Мультиязычность. Добавление hidden-полей для кастомного алгоритма
		for($c = 0 ; $c < count($translated_items); $c++)
		{
			//Поле value пишется в колонку caption...
			if( $translated_items[$c] == 'value' )
			{
				$vin_fields[count($vin_fields)-1][$translated_items[$c]."_lang_str_id"] = (int)$vin_field['caption'];
				continue;
			}
			
			
			$vin_fields[count($vin_fields)-1][$translated_items[$c]."_lang_str_id"] = (int)$vin_field[$translated_items[$c]];
		}
	}

  $vin_fields_noencode = $vin_fields;
	$vin_fields = json_encode($vin_fields);

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
				
				<?php
				print_backend_button(array('background_color'=>'#b9babb', 'fontawesome_class'=>'fas fa-chevron-left', 'caption'=>'Назад', 'url'=>$DP_Config->domain_path.$DP_Config->backend_dir.'/requests'));
				?>
				
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
				<?php echo translate_str_by_id(5142); ?>
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

    let show = "";
    if(node.show == '1') {
        show = "checked";
        
    }
    
    let required = "";
    if(node.required == '1') {
        required = "checked";
    }

		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+ID_for_show+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+node.value+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----

		if(node.name != "client_vin") {
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2460); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('name');\" id=\"name\" value=\""+node.name+"\" class=\"form-control\" /></div></div>";
			parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		}

		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3877); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('maxlen');\" id=\"maxlen\" value=\""+node.maxlen+"\" class=\"form-control\" /></div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3878); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('regexp');\" id=\"regexp\" value=\""+node.regexp+"\" class=\"form-control\" /></div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3879); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('example');\" id=\"example\" value=\""+node.example+"\" class=\"form-control\" /></div></div>";
		if(node.name != "client_vin") {
			parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3883); ?></label><div class=\"col-lg-6\"><input type=\"checkbox\" onChange=\"dynamicApplyingCheckboxes('required');\" id=\"required\"" + required + "  class=\"form-control\" /></div></div>";

			parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3882); ?></label><div class=\"col-lg-6\"><input type=\"checkbox\" onChange=\"dynamicApplyingCheckboxes('show');\" id=\"show\" class=\"form-control\" " + show + " /></div></div>";
		}
		
		parameters_table_html += "</tbody></table></div>";
		
    	document.getElementById("content_info_div").innerHTML = parameters_table_html;
  }
  // function onSelected()
	//-----------------------------------------------------
	//Массив с регистрационными вариантами
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
	//Функция динамического применения чебоксов
	function dynamicApplyingCheckboxes(type)
	{
		var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел

		if(type == "show")
		{
			if(document.getElementById("show").checked)
			{
				node.show = '1';
			}
			else
			{
				node.show = '0';
			}
		}
		else //required
		{
			if(document.getElementById("required").checked)
			{
				node.required = '1';
			}
			else
			{
				node.required = '0';
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
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(2908); ?>", is_new:true, name:"", maxlen:0, regexp:"", example:"", show:0, required:0, value_lang_str_id:0, regexp_lang_str_id:0, example_lang_str_id:0}, 0, 0);//Добавляем новый узел и запоминаем его ID

    	onSelected();//Обработка текущего выделения
    }
    //-----------------------------------------------------
    //Удаление выделеного элемента
    function delete_selected_item()
    {
    	let nodeId = tree.getSelectedId();
			let node = tree.getSelectedItem();
			if(node && node.name == 'client_vin') {
			alert('<?php echo translate_str_by_id(5225); ?>');
			return;
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
					alert("<?php echo translate_str_by_id(5226); ?>: "+tree_json_to_save[i].value+": <?php echo translate_str_by_id(3900); ?>");
					return;
				}
				else if( parseInt(tree_json_to_save[i].maxlen) != tree_json_to_save[i].maxlen )
				{
					alert("<?php echo translate_str_by_id(5226); ?>: "+tree_json_to_save[i].value+": <?php echo translate_str_by_id(3900); ?>");
					return;
				}
				else if(tree_json_to_save[i].maxlen < 0)
				{
					alert("<?php echo translate_str_by_id(5226); ?>: "+tree_json_to_save[i].value+": <?php echo translate_str_by_id(3900); ?>");
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
    	var saved_list = <?php echo $vin_fields; ?>;
	    tree.parse(saved_list);
	    tree.openAll();
    }
    tree_start_init();
    onSelected();//Обработка текущего выделения
    </script>
	
	
	<?php
}
?>