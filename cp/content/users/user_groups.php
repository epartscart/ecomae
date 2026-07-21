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

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	if ($backend === '') {
		$backend = 'cp';
	}
	$backendH = htmlspecialchars($backend, ENT_QUOTES, 'UTF-8');

	function epc_ug_t($id, $fallback = '')
	{
		$t = translate_str_by_id($id);
		if ($t === null || $t === false) {
			$t = '';
		}
		$t = trim((string) $t);
		if ($t === '' || strcasecmp($t, 'null') === 0) {
			return $fallback;
		}
		return $t;
	}

	if (function_exists('epc_cp_page_frame_open')) {
		epc_cp_page_frame_open(array(
			'class' => 'epc-users-cp-frame',
			'hero' => array(
				'badge' => 'Users',
				'title' => epc_ug_t(3956, 'User groups'),
				'sub' => 'Organize access roles — guest default, registration default, control-panel access, and pricing tiers.',
				'actions' => array(
					array(
						'url' => '/' . $backend . '/users/usermanager',
						'label' => 'Users list',
						'icon' => 'fa-users',
						'primary' => true,
					),
					array(
						'url' => '/' . $backend . '/users/usermanager/user',
						'label' => 'New user',
						'icon' => 'fa-user-plus',
					),
				),
			),
		));
	}
?>


    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>

	<div class="epc-users-cp">
		<div class="epc-users-cp__toolbar" role="toolbar" aria-label="Group actions">
			<button type="button" class="epc-u-btn" onclick="add_new_item();"><i class="fa fa-plus"></i> <?php echo htmlspecialchars(epc_ug_t(2267, 'Add'), ENT_QUOTES, 'UTF-8'); ?></button>
			<button type="button" class="epc-u-btn epc-u-btn--danger" onclick="delete_selected_item();"><i class="fa fa-trash"></i> <?php echo htmlspecialchars(epc_ug_t(2224, 'Delete'), ENT_QUOTES, 'UTF-8'); ?></button>
			<button type="button" class="epc-u-btn" onclick="unselect_tree();"><i class="fa fa-times"></i> <?php echo htmlspecialchars(epc_ug_t(2268, 'Clear selection'), ENT_QUOTES, 'UTF-8'); ?></button>
			<button type="button" class="epc-u-btn epc-u-btn--primary" onclick="save_tree();"><i class="fa fa-save"></i> <?php echo htmlspecialchars(epc_ug_t(2114, 'Save'), ENT_QUOTES, 'UTF-8'); ?></button>
			<span class="epc-users-cp__toolbar-spacer"></span>
			<div class="epc-users-cp__toolbar-group" aria-label="Role flags">
				<span>Roles</span>
				<button type="button" class="epc-u-btn" onclick="setForGuests();" title="Default group for guests"><i class="fa fa-user"></i> Guest</button>
				<button type="button" class="epc-u-btn" onclick="setForRegistrated();" title="Default group after registration"><i class="fa fa-check"></i> Registration</button>
				<button type="button" class="epc-u-btn" onclick="setForBackend();" title="Control panel access group"><i class="fa fa-shield"></i> Control panel</button>
				<button type="button" class="epc-u-btn" onclick="lock_unlock();" title="Block or unblock this group"><i class="fa fa-lock"></i> Block</button>
				<button type="button" class="epc-u-btn" onclick="setForPercentage();" title="Pricing / percentage group"><i class="fa fa-percent"></i> Pricing</button>
			</div>
			<a class="epc-u-btn epc-u-btn--muted" href="/<?php echo $backendH; ?>"><i class="fa fa-home"></i> <?php echo htmlspecialchars(epc_ug_t(2116, 'Control panel'), ENT_QUOTES, 'UTF-8'); ?></a>
		</div>

		<p class="epc-users-cp__hint">
			Select a group, set its role flags, then <strong>Save</strong>. Double-click a name to rename. Drag to reorder or nest groups.
		</p>

		<div class="epc-users-cp__legend">
			<span><i class="fa fa-user"></i> <strong>Guest</strong> — storefront visitors</span>
			<span><i class="fa fa-check"></i> <strong>Registration</strong> — new accounts</span>
			<span><i class="fa fa-shield"></i> <strong>Control panel</strong> — admin access</span>
			<span><i class="fa fa-lock"></i> <strong>Blocked</strong> — cannot sign in</span>
			<span><i class="fa fa-percent"></i> <strong>Pricing</strong> — price-list tier</span>
		</div>

		<div class="epc-users-cp__workspace">
			<div class="epc-users-cp__pane">
				<div class="epc-users-cp__pane-h">
					<h3><?php echo htmlspecialchars(epc_ug_t(3956, 'Groups tree'), ENT_QUOTES, 'UTF-8'); ?></h3>
					<span>Drag to nest · double-click to rename</span>
				</div>
				<div class="epc-users-cp__search">
					<i class="fa fa-search" aria-hidden="true"></i>
					<input type="search" id="epc_ug_tree_filter" placeholder="Filter groups…" autocomplete="off" />
				</div>
				<div class="epc-users-cp__tree-wrap">
					<div id="container_A"></div>
				</div>
			</div>

			<div class="epc-users-cp__pane" id="group_info_div_col">
				<div class="epc-users-cp__pane-h">
					<h3><?php echo htmlspecialchars(epc_ug_t(3957, 'Group details'), ENT_QUOTES, 'UTF-8'); ?></h3>
					<span>Select a group to edit description and review flags</span>
				</div>
				<div class="epc-users-cp__detail-body" id="group_info_div">
					<div class="epc-u-empty">Select a group in the tree to view and edit its details.</div>
				</div>
			</div>
		</div>
	</div>

    
    <!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none" aria-hidden="true">
        <input name="save_tree" id="save_tree" type="hidden" value="ok"/>
        <input name="tree_json" id="tree_json" type="hidden" value=""/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string) $user_session["csrf_guard_key"], ENT_QUOTES, 'UTF-8'); ?>" />
    </form>
    <!--Форма для отправки-->
    
    
    
    <script type="text/javascript" charset="utf-8">
    var next_id = <?php echo (int) $next_id; ?>;//Следующий id
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
        	    var label = (obj.value == null || obj.value === 'null') ? ('Group #' + obj.id) : String(obj.value);
        	    var value_text = "<span>" + label.replace(/</g,'&lt;') + "</span>";
                
                
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
    	filterMode:{ showSubItems:true }
    });
    /*~ДЕРЕВО*/
	webix.event(window, "resize", function(){ tree.adjust(); });
	if (typeof window.epcUsersBindTreeFilter === 'function') {
		window.epcUsersBindTreeFilter('epc_ug_tree_filter', function () { return tree; });
	}
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
		var detailCol = document.getElementById("group_info_div_col");
		var detailBox = document.getElementById("group_info_div");
		if (detailCol) { detailCol.style.display = ""; }

        //Если материалы не созданы
    	if(tree.count() == 0)
    	{
    	    detailBox.innerHTML = "<div class=\"epc-u-empty\">No groups yet. Click <b>Add</b> to create the first group.</div>";
    	    return;
    	}
    	
    	//Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	if(!node_id)
    	{
    	    detailBox.innerHTML = "<div class=\"epc-u-empty\">Select a group in the tree to view and edit its details.</div>";
    	    return;
    	}
    	
    	var node = tree.getItem(node_id);
    	var name = (node.value == null || node.value === 'null') ? ('Group #' + node.id) : String(node.value);
    	var desc = (node.description == null || node.description === 'null') ? '' : String(node.description);
    	var esc = window.epcUsersEsc || function (v) { return String(v == null ? '' : v).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); };
    	var badges = "";
    	if (node.for_guests == 1) { badges += "<span class=\"epc-users-cp__badge epc-users-cp__badge--guest\"><i class=\"fa fa-user\"></i> Guest</span>"; }
    	if (node.for_registrated == 1) { badges += "<span class=\"epc-users-cp__badge epc-users-cp__badge--reg\"><i class=\"fa fa-check\"></i> Registration</span>"; }
    	if (node.for_backend == 1) { badges += "<span class=\"epc-users-cp__badge epc-users-cp__badge--cp\"><i class=\"fa fa-shield\"></i> Control panel</span>"; }
    	if (node.unblocked == 0) { badges += "<span class=\"epc-users-cp__badge epc-users-cp__badge--lock\"><i class=\"fa fa-lock\"></i> Blocked</span>"; }
    	if (node.for_percentage == 1) { badges += "<span class=\"epc-users-cp__badge epc-users-cp__badge--pct\"><i class=\"fa fa-percent\"></i> Pricing</span>"; }
    	if (!badges) { badges = "<span class=\"epc-users-cp__badge\">No special roles</span>"; }

    	var parameters_table_html = "";
		parameters_table_html += "<div class=\"epc-u-section\"><div class=\"epc-u-section-title\">General</div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+esc(node.id)+"</div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_ug_t(2277, 'Name'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><strong>"+esc(name)+"</strong> <span class=\"text-muted\">(double-click in tree)</span></div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\">Roles</label><div class=\"col-lg-6\"><div class=\"epc-users-cp__badges\">"+badges+"</div></div></div>";
		parameters_table_html += "</div>";
		parameters_table_html += "<div class=\"epc-u-section\"><div class=\"epc-u-section-title\">Structure</div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_ug_t(2278, 'Level'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\">"+esc(node.$level)+"</div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_ug_t(2279, 'Parent ID'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\">"+esc(node.$parent)+"</div></div>";
		parameters_table_html += "</div>";
		parameters_table_html += "<div class=\"epc-u-section\"><div class=\"epc-u-section-title\">Notes</div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_ug_t(2175, 'Description'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><textarea class=\"form-control\" id=\"description\" onKeyUp=\"dynamicApplying('description');\">"+esc(desc)+"</textarea></div></div>";
		parameters_table_html += "</div>";

    	detailBox.innerHTML = parameters_table_html;
    	
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
	if (function_exists('epc_cp_page_frame_close')) {
		epc_cp_page_frame_close();
	}
}//else - выводим страницу
?>