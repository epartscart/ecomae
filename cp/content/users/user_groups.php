<?php
/**
 * Скрипт для работы со деревом групп
 * 
 * Сделать группы для регистрации и для гостей
*/
defined('_ASTEXE_') or die('No access');

require_once("content/users/dp_group_record.php");//Определение класса записи группы пользователей
?>


<?php
// --------------------------------- Start PHP - метод ---------------------------------
//Рекурсивная функция для перевода иерархического массива (JSON перечня групп) в линейный массив (просто набор объектов групп)
function getLinearListOfGroups($hierarchy_array)
{
    $linear_array = array();//Линейный массив
    
    for($i=0; $i<count($hierarchy_array); $i++)
    {	
        //Генерируем объект записи группы и заносим его в линейный массив
        $current_group = new DP_GroupRecord;
        $current_group->id = (int)$hierarchy_array[$i]["id"];
        $current_group->value = htmlentities($hierarchy_array[$i]["value"], ENT_QUOTES, "UTF-8", false);
        $current_group->count = (int)$hierarchy_array[$i]['$count'];
        $current_group->level = (int)$hierarchy_array[$i]['$level'];
        $current_group->parent = (int)$hierarchy_array[$i]['$parent'];
        $current_group->unblocked = (int)$hierarchy_array[$i]["unblocked"];
        $current_group->for_guests = (int)$hierarchy_array[$i]["for_guests"];
        $current_group->for_registrated = (int)$hierarchy_array[$i]["for_registrated"];
        $current_group->for_backend = (int)$hierarchy_array[$i]["for_backend"];
		$current_group->for_percentage = (int)$hierarchy_array[$i]["for_percentage"];
        $current_group->description = htmlentities($hierarchy_array[$i]["description"], ENT_QUOTES, "UTF-8", false);
        $current_group->value_lang_str_id = $hierarchy_array[$i]["value_lang_str_id"];
        $current_group->description_lang_str_id = $hierarchy_array[$i]["description_lang_str_id"];
        array_push($linear_array, $current_group);
        
        //Рекурсивный вызов для вложенного уровня
        if($hierarchy_array[$i]['$count'] > 0)
        {
            $data_linear_array = getLinearListOfGroups($hierarchy_array[$i]["data"]);
            //Добавляем массив вложенного уровня к текущему
            for($j=0; $j<count($data_linear_array); $j++)
            {
                array_push($linear_array, $data_linear_array[$j]);
            }//for(j)
        }
    }//for(i)
    
    return $linear_array;
}//~function getLinearListOfGroups($hierarchy_array)
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//Сохранение групп
if(!empty($_POST["save_tree"]))//Для действий
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    //Генерируем линейный массив на основе полученого иерархического
    $php_dump = json_decode($_POST["tree_json"], true);
    $linear_array = array();//Линейный массив групп
    $linear_array = getLinearListOfGroups($php_dump);//Генерируем линейный массив групп
    
    
    
    $no_update_error = true;//Накопительный результат обновления групп
    $no_insert_error = true;//Накопительный результат создания групп
    
    //По всем элементам линейного массива: Созданние и Обновление
    for($i=0; $i<count($linear_array); $i++)
    {
		$order = $i + 1;
		
		
		
		//Мультиязычность. Кастомный алгоритм
		$linear_array[$i]->value = save_custom_translation($linear_array[$i]->value_lang_str_id, $linear_array[$i]->value);
		$linear_array[$i]->description = save_custom_translation($linear_array[$i]->description_lang_str_id, $linear_array[$i]->description);
		
		
		
        //Проверяем существование записи группы:
		$check_group_exist_query = $db_link->prepare("SELECT COUNT(*) FROM `groups` WHERE `id`=?;");
		$check_group_exist_query->execute( array($linear_array[$i]->id) );
        if($check_group_exist_query->fetchColumn() == 1)
        {
            //Запись существует - ее нужно обновить
			if( $db_link->prepare("UPDATE `groups` SET `value`=?, `count`=?, `level`=?,`parent`=?, `unblocked`=?, `for_guests`=?, `for_registrated`=?, `for_backend`=?, `for_percentage` = ?, `description`=?, `order` = ? WHERE `id`=?;")->execute( array($linear_array[$i]->value, $linear_array[$i]->count, $linear_array[$i]->level, $linear_array[$i]->parent, $linear_array[$i]->unblocked, $linear_array[$i]->for_guests, $linear_array[$i]->for_registrated, $linear_array[$i]->for_backend, $linear_array[$i]->for_percentage, $linear_array[$i]->description, $order, $linear_array[$i]->id) ) != true )
            {
                $no_update_error = false;
            }
        }
        else
        {
            //Запись не существует - ее нужно создать
            if( $db_link->prepare("INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")->execute( array($linear_array[$i]->id, $linear_array[$i]->value, $linear_array[$i]->count, $linear_array[$i]->level, $linear_array[$i]->parent, $linear_array[$i]->unblocked, $linear_array[$i]->for_guests, $linear_array[$i]->for_registrated, $linear_array[$i]->for_backend, $linear_array[$i]->for_percentage, $linear_array[$i]->description, $order) ) != true )
            {
                $no_insert_error = false;
            }
        }
    }//for($i) По всем элементам линейного массива:
    
    
    
    //По всем записям базы данных для удаления записей, которые были удалены при редактировании
    $no_delete_error = true;//Накопительный результат удаления групп
    
	$all_groups_record_query = $db_link->prepare("SELECT * FROM `groups`");
	$all_groups_record_query->execute();
    while( $group_record = $all_groups_record_query->fetch() )
    {
        $such_group_record_exist = false;
        for($j=0; $j < count($linear_array); $j++)
        {
            if($group_record["id"] == $linear_array[$j]->id)
            {
                $such_group_record_exist = true;
                break;
            }
        }
        
        //Если такой группы нет в сохраняемом перечне, значит при редактировании она была удалена - удаляем ее из БД
        if(!$such_group_record_exist)
        {
            if( $db_link->prepare("DELETE FROM `groups` WHERE `id` = ?;")->execute( array($group_record["id"]) ) != true)
            {
                $no_delete_error = false;
            }
        }
    }
    

 
    //Выполнено без ошибок
    if($no_update_error && $no_insert_error && $no_delete_error)
    {
        $success_message = translate_str_by_id(3948);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/users/usergroups?success_message=<?php echo $success_message; ?>";
        </script>
        <?php
        exit;
    }
    else
    {
        $error_message = translate_str_by_id(2912).": <br>";
        if(!$no_update_error)
        {
            $error_message .= translate_str_by_id(3949)."<br>";
        }
        if(!$no_insert_error)
        {
            $error_message .= translate_str_by_id(3950)."<br>";
        }
        if(!$no_delete_error)
        {
            $error_message .= translate_str_by_id(3951)."<br>";
        }
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/users/usergroups?error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit;
    }
}//Сохранение материалов
else//если действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	
    require_once("content/users/get_group_records.php");//Получение объекта иерархии существующих групп для вывода в дерево-webix
    
	/*
    //Получить следующий Auto increment
	$next_id_query = $db_link->prepare("SHOW TABLE STATUS LIKE 'groups'");
	$next_id_query->execute();
	$next_id_record = $next_id_query->fetch();
	if( $next_id_record == false )
	{
		exit("SQL error: next_id_query");
	}
    $next_id = $next_id_record["Auto_increment"];//ID следующей создаваемой группы
	*/
	//Определяем следующий ID ($next_id)
	$table_name = "groups";
	$col_name = "id";//Имя колонки, в которой содержится id записей (обычно имя равно id)
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/lib/docpart/get_next_id.php");
	
	
    
    //Получить текущую группу для гостей
	$get_for_guests_query = $db_link->prepare("SELECT * FROM `groups` WHERE `for_guests`=1");
	$get_for_guests_query->execute();
	$get_for_guests_record = $get_for_guests_query->fetch();
    if( $get_for_guests_record != false )
    {
        $for_guests_current = $get_for_guests_record["id"];
    }
    else
    {
        $for_guests_current = 0;
    }
    
    //Получить текущую группу для регистрации
	$get_for_registrated_query = $db_link->prepare("SELECT * FROM `groups` WHERE `for_registrated`=1");
	$get_for_registrated_query->execute();
	$get_for_registrated_record = $get_for_registrated_query->fetch();
    if( $get_for_registrated_record != false )
    {
        $for_registrated_current = $get_for_registrated_record["id"];
    }
    else
    {
        $for_registrated_current = 0;
    }
    
    //Получить текущую группу для бэкэнда
	$get_for_backend_query = $db_link->prepare("SELECT * FROM `groups` WHERE `for_backend`=1");
	$get_for_backend_query->execute();
	$get_for_backend_record = $get_for_backend_query->fetch();
    if( $get_for_backend_record != false )
    {
        $for_backend_current = $get_for_backend_record["id"];
    }
    else
    {
        $for_backend_current = 0;
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
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<a class="panel_a" onClick="delete_selected_item();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				
				<a class="panel_a" onClick="unselect_tree();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/selection_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2268); ?></div>
				</a>
				

				<a class="panel_a" onClick="save_tree();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="setForGuests();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/guest.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3952); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="setForRegistrated();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3953); ?></div>
				</a>
				
				<a class="panel_a" onClick="setForBackend();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/shield.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3954); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="lock_unlock();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3955); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="setForPercentage();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/star.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(4654); ?></div>
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
				<?php echo translate_str_by_id(3956); ?>
			</div>
			<div class="panel-body">
				<div id="container_A" style="height:350px;">
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-6" id="group_info_div_col">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3957); ?>
			</div>
			<div class="panel-body">
				<div id="group_info_div">
				</div>
			</div>
		</div>
	</div>
	

    
    <!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none">
        <input name="save_tree" id="save_tree" type="text" value="ok" style="display:none"/>
        <input name="tree_json" id="tree_json" type="text" value="" style="display:none"/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!--Форма для отправки-->
    
    
    
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
        	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
                
                
                if(obj.for_registrated == true)
                {
                    icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                }
                if(obj.for_guests == true)
                {
                    icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/guest.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                }
                if(obj.for_backend == true)
                {
                    icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/shield.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                }
                if(obj.unblocked == 0)
                {
                    icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                }
				if(obj.for_percentage == 1)
                {
                    icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/star.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                }
                
				
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
    
    //Назначить группу для гостей
    var current_for_guests_id = <?php echo $for_guests_current; ?>;//Текущая для гостей
    function setForGuests()
    {
        var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    alert("<?php echo translate_str_by_id(3958); ?>");
    	    return;
    	}
    	
    	//Выделенный узел
    	node = tree.getItem(node_id);
    	if(node.for_guests == 1)//Уже назначена
    	{
    	    return;
    	}
    	
    	node.for_guests = 1;
    	
    	//Снимаем старый
    	if(current_for_guests_id != 0)
    	{
    	    last_for_guests_node = tree.getItem(current_for_guests_id);
    	    if(last_for_guests_node != undefined)
    	    {
    	        last_for_guests_node.for_guests = 0;
    	    }
    	}
    	
    	//Запоминаем текущий
    	current_for_guests_id = node_id;
    	
    	tree.refresh();
    }
    
    //-----------------------------------------------------
    
    //Установка группы для регистрации
    var current_for_registrated_id = <?php echo $for_registrated_current; ?>;//Текущая для регистрации
    function setForRegistrated()
    {
        var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    alert("<?php echo translate_str_by_id(3958); ?>");
    	    return;
    	}
    	
    	//Выделенный узел
    	node = tree.getItem(node_id);
    	if(node.for_registrated == 1)//Уже назначена
    	{
    	    return;
    	}
    	node.for_registrated = 1;
    	
    	//Снимаем старый
    	if(current_for_registrated_id != 0)
    	{
    	    last_for_registrated_node = tree.getItem(current_for_registrated_id);
    	    if(last_for_registrated_node != undefined)
    	    {
    	        last_for_registrated_node.for_registrated = 0;
    	    }
    	}
    	
    	//Запоминаем текущий
    	current_for_registrated_id = node_id;
    	
    	tree.refresh();
    }
    
    //-----------------------------------------------------
    //Установка группы для бэкэнда
    var current_for_backend_id = <?php echo $for_backend_current; ?>;//Текущая для бэкэнда
    function setForBackend()
    {
        var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    alert("<?php echo translate_str_by_id(3958); ?>");
    	    return;
    	}
    	
    	//Выделенный узел
    	node = tree.getItem(node_id);
    	if(node.for_backend == 1)//Уже назначена
    	{
    	    return;
    	}
    	node.for_backend = 1;
    	
    	//Снимаем старый
    	if(current_for_backend_id != 0)
    	{
    	    last_for_backend_node = tree.getItem(current_for_backend_id);
    	    if(last_for_backend_node != undefined)
    	    {
    	        last_for_backend_node.for_backend = 0;
    	    }
    	}
    	
    	//Запоминаем текущий
    	current_for_backend_id = node_id;
    	
    	tree.refresh();
    }
    
	//-----------------------------------------------------
    
    //Тех. информация в проценке
    function setForPercentage()
    {
        var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    alert("Выберите группу");
    	    return;
    	}
    	
    	//Выделенный узел
    	node = tree.getItem(node_id);

    	node.for_percentage = !node.for_percentage;
    	tree.refresh();
    }//~function setForPercentage()

    //-----------------------------------------------------
    
    //Блокировка / разблокировка выделенной группы
    function lock_unlock()
    {
        var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    alert("<?php echo translate_str_by_id(3958); ?>");
    	    return;
    	}
    	
    	//Выделенный узел
    	node = tree.getItem(node_id);

    	node.unblocked = !node.unblocked;
    	tree.refresh();
    }//~function lock_unlock()

    //-----------------------------------------------------
    
    //Событие при выборе элемента дерева
    tree.attachEvent("onAfterSelect", function(id)
    {
    	onSelected();
    });
    //Обработка выбора элемента
    function onSelected()
    {
        //Если материалы не созданы
    	if(tree.count() == 0)
    	{
    	    document.getElementById("group_info_div").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("group_info_div_col").setAttribute("style", "display:none");
    	    return;
    	}
    	
    	//Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    document.getElementById("group_info_div").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("group_info_div_col").setAttribute("style", "display:none");
    	    return;
    	}
		
		//Скрыть контейнер для параметров
		document.getElementById("group_info_div_col").setAttribute("style", "display:block;");
    	
    	
    	
    	var node = "";//Ссылка на объект узла
    	//Выделенный узел
    	node = tree.getItem(node_id);
    	
    	var parameters_table_html = "";
		
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+node.id+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+node.value+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2278); ?></label><div class=\"col-lg-6\">"+node.$level+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2279); ?></label><div class=\"col-lg-6\">"+node.$parent+"</div></div>";
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2175); ?></label><div class=\"col-lg-6\"><textarea class=\"form-control\" id=\"description\" onKeyUp=\"dynamicApplying('description');\">"+node.description+"</textarea></div></div>";

    	document.getElementById("group_info_div").innerHTML = parameters_table_html;
    	
    }//function onSelected()
    
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
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(2935); ?>", value_lang_str_id:0, id:next_id, description:"", description_lang_str_id:0, for_registrated:0, for_guests:0, for_backend:0, for_percentage:0, unblocked:1}, 0, parentId);//Добавляем новый узел и запоминаем его ID
    	onSelected();//Обработка текущего выделения
    	next_id++;//Следующий ID группы
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
    //Отладочный метод
    function debug_func()
    {
    	
    }
    //-----------------------------------------------------
    //Снятие выделения с дерева
    function unselect_tree()
    {
    	tree.unselect();
    	onSelected();
    }
    //-----------------------------------------------------
    //Сохранение перечня материалов
    function save_tree()
    {   
        //ЕДИНАЯ ПРОВЕРКА КОРРЕКТНОСТИ СОХРАНЯЕМОЙ КОНФИГУРАЦИИ ГРУПП
        if(!commonCheck())
        {
            return;
        }
        
        //webix.message("ОТЛАДКА: все правильно");
        //return;
        
        //Получаем строку JSON:
    	var tree_json_to_save = tree.serialize();//Дамп дерева в JavaScript
    	tree_dump = JSON.stringify(tree_json_to_save);//Дамп дерева в JSON
        
    	//Задаем значение поля в форме:
    	var tree_json_input = document.getElementById("tree_json");
    	tree_json_input.value = tree_dump;
    	
    	document.forms["form_to_save"].submit();//Отправляем
    }//~function save_tree() - сохранение групп
    
    //-----------------------------------------------------
    
    //ЕДИНАЯ ПРОВЕРКА КОРРЕКТНОСТИ СОХРАНЯЕМОЙ КОНФИГУРАЦИИ ГРУПП
    function commonCheck()
    {
        var tree_JavaScript = tree.serialize();//Дамп дерева в JavaScript
    
        //1. ПРОВЕРКА ОБЯЗАТЕЛЬНОГО НАЛИЧИЯ ГРУПП: ДЛЯ РЕГИСТРАЦИИ, БЭКЭНДА И ГОСТЕЙ
        if(!checkGroupsFlags(tree_JavaScript, "for_guests"))
        {
            alert("<?php echo translate_str_by_id(3959); ?>");
            return false;
        }
        if(!checkGroupsFlags(tree_JavaScript, "for_registrated"))
        {
            alert("<?php echo translate_str_by_id(3960); ?>");
            return false;
        }
        if(!checkGroupsFlags(tree_JavaScript, "for_backend"))
        {
            alert("<?php echo translate_str_by_id(3961); ?>");
            return false;
        }
        
        //2. ПРОВЕРКА НА ОСТУТСТВИЕ СОВПАДЕНИЙ ГРУППЫ БЭКЭНДА С ГРУППОЙ ДЛЯ ГОСТЕЙ ИЛИ ЗАРЕГИСТРИРОВАННЫХ
        /*
        current_for_guests_id
        current_for_registrated_id
        current_for_backend_id
        */
        if(current_for_backend_id == current_for_guests_id || current_for_backend_id == current_for_registrated_id)
        {
            alert("<?php echo translate_str_by_id(3962); ?>");
            return false;
        }
        
        
        //3. ПРОВЕРКА ОТСУТСТВИЯ ВЛОЖЕННОСТИ ГРУППЫ ДЛЯ ГОСТЕЙ И ГРУППЫ ДЛЯ ЗАРЕГИСТРИРОВАННЫХ В ГРУППУ ДЛЯ БЭКЭНДА
        //3.1 ГОСТИ
        var guest_node = tree.getItem(current_for_guests_id);//Узел для гостей
        var guest_parent_id = guest_node.$parent;//ID узла родителя группы для гостей
        while(guest_parent_id)
        {
            var guest_parent_node = tree.getItem(guest_parent_id);//Узел родителя группы для гостей
            if(guest_parent_node.for_backend == true)
            {
                alert("<?php echo translate_str_by_id(3963); ?>");
                return false;
            }
            else
            {
                guest_parent_id = guest_parent_node.$parent;////ID узла родителя более высокого уровня
            }
        }
        //3.2 ЗАРЕГИСТРИРОВАННЫЕ
        var registrated_node = tree.getItem(current_for_registrated_id);//Узел для зарегистрированных
        var registrated_parent_id = registrated_node.$parent;//ID узла родителя группы для зарегистрированных
        while(registrated_parent_id)
        {
            var registrated_parent_node = tree.getItem(registrated_parent_id);//Узел родителя группы для зарегистрированных
            if(registrated_parent_node.for_backend == true)
            {
                alert("<?php echo translate_str_by_id(3964); ?>");
                return false;
            }
            else
            {
                registrated_parent_id = registrated_parent_node.$parent;////ID узла родителя более высокого уровня
            }
        }
        
        
        //4. ПРОВЕРКА КОРРЕКТНОСТИ БЛОКИРОВКИ
        //4.1. ПРОВЕРКА ОТСУТСТВИЯ БЛОКИРОВКИ ГРУПП ДЛЯ БЭКЭНДА, ГОСТЕЙ И РЕГИСТРАЦИИ
        var backend_node = tree.getItem(current_for_backend_id);
        if(backend_node.unblocked == false)
        {
            alert("<?php echo translate_str_by_id(3965); ?>");
            return false;
        }
        if(guest_node.unblocked == false)
        {
            alert("<?php echo translate_str_by_id(3966); ?>");
            return false;
        }
        if(registrated_node.unblocked == false)
        {
            alert("<?php echo translate_str_by_id(3967); ?>");
            return false;
        }
        //4.2 ПРОВЕРКА ОСТУТСТВИЯ БЛОКИРОВКИ РОДИТЕЛЬСКИХ ГРУПП ДЛЯ БЭКЭНДА, ГОСТЕЙ И РЕГИСТРАЦИИ
        //4.2.1 БЭКЭНД
        var backend_parent_id = backend_node.$parent;//ID узла родителя группы для бэкэнда
        while(backend_parent_id)
        {
            var backend_parent_node = tree.getItem(backend_parent_id);//Узел родителя группы для бэкэнда
            if(backend_parent_node.unblocked == false)
            {
                alert("<?php echo translate_str_by_id(3968); ?>");
                return false;
            }
            else
            {
                backend_parent_id = backend_parent_node.$parent;//ID узла родителя более высокого уровня
            }
        }
        //4.2.2 ГОСТИ
        var guest_parent_id = guest_node.$parent;//ID узла родителя группы для гостей
        while(guest_parent_id)
        {
            var guest_parent_node = tree.getItem(guest_parent_id);//Узел родителя группы для гостей
            if(guest_parent_node.unblocked == false)
            {
                alert("<?php echo translate_str_by_id(3969); ?>");
                return false;
            }
            else
            {
                guest_parent_id = guest_parent_node.$parent;//ID узла родителя более высокого уровня
            }
        }
        //4.2.3 ЗАРЕГИСТРИРОВАННЫЕ
        var registrated_parent_id = registrated_node.$parent;//ID узла родителя группы для зарегистрированных
        while(registrated_parent_id)
        {
            var registrated_parent_node = tree.getItem(registrated_parent_id);//Узел родителя группы для зарегистрированных
            if(registrated_parent_node.unblocked == false)
            {
                alert("<?php echo translate_str_by_id(3970); ?>");
                return false;
            }
            else
            {
                registrated_parent_id = registrated_parent_node.$parent;//ID узла родителя более высокого уровня
            }
        }
        
        
        
        return true;
    }//~function commonCheck()//ЕДИНАЯ ПРОВЕРКА КОРРЕКТНОСТИ СОХРАНЯЕМОЙ КОНФИГУРАЦИИ ГРУПП
    
    //-----------------------------------------------------
    
    
    //Рекурсивный метод проверки наличия групп: Для регистрации, Для бэкэнда, Для гостей
    function checkGroupsFlags(data, flag_name)
    {
        for(var i=0; i < data.length; i++)
        {
            if(data[i][flag_name] == 1)//Если найден элемент с установленным флагом - проверка пройдена
            {
                return true;
            }
            
            if(data[i].$count > 0)
            {
                var data_result = checkGroupsFlags(data[i].data, flag_name);
                if(data_result == true)//Если среди вложенных элементов найдена группа с флагом - проверка пройдена. Если нет - продолжаем искать
                {
                    return true;
                }
            }
        }
        
        return false;
    }//~function checkGroupsFlags()
    
    //-----------------------------------------------------
    //Инициализация редактора дерева материалов после загруки страницы
    function group_start_init()
    {
    	var saved_groups = <?php echo $group_tree_dump_JSON; ?>;
	    tree.parse(saved_groups);
	    tree.openAll();
    }
    group_start_init();
    onSelected();//Обработка текущего выделения
    </script>

<?php
}//else - выводим страницу
?>