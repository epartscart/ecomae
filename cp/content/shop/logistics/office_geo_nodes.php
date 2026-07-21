<?php
/**
 * Страница для настройки связи магазина с географическими узлами
*/
defined('_ASTEXE_') or die('No access');


require_once($_SERVER['DOCUMENT_ROOT'] . '/content/shop/geo/dp_geo_node_record.php');//Определение класса географического узла
?>


<?php
if(!empty($_POST["save_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
    $office_id = $_POST["office_id"];
    
    //1. Предварительно удаляем старые записи для этого магазина
    $db_link->prepare("DELETE FROM `shop_offices_geo_map` WHERE `office_id` = ?;")->execute( array($office_id) );
	
    //2. Создаем новые записи
    $geo_list = json_decode($_POST["geo_list"], true);
    
    $SQL_INSERT = "INSERT INTO `shop_offices_geo_map` (`office_id`, `geo_id`) VALUES ";
    $binding_values = array();
	for($i=0; $i < count($geo_list); $i++)
    {
        if($i > 0 )$SQL_INSERT .= ",";
        $SQL_INSERT .= "(?,?)";
		
		array_push($binding_values, $office_id);
		array_push($binding_values, $geo_list[$i]);
    }
	$db_link->prepare($SQL_INSERT)->execute($binding_values);
    
    $success_message = translate_str_by_id(2157);
    ?>
    <script>
        location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office/geo_nodes?office_id=<?php echo $office_id; ?>&success_message=<?php echo $success_message; ?>";
    </script>
    <?php
    exit;
    
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    if(empty($_GET["office_id"]))
    {
        exit;
    }
    $office_id = $_GET["office_id"];
    
    //Исходные данные:
    $page_title = translate_str_by_id(3382);
    require_once($_SERVER['DOCUMENT_ROOT'] . '/content/shop/geo/get_geo_tree.php');//Получение объекта иерархии существующих географических узлов для вывода в дерево-webix
    ?>
    <?php
        require_once($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/actions_alert.php');//Вывод сообщений о результатах действий
    ?>
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="checkAll()">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/checkbox.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2293); ?></div>
				</a>
				
				<a class="panel_a" href="javascript:void(0);" onclick="uncheckAll()">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/selection_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2294); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/offices/office?office_id=<?php echo $office_id; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/office.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3383); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/offices/office/storages_link?office_id=<?php echo $office_id; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/storages_link.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3384); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/offices">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/offices.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3369); ?></div>
				</a>


				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
    

    
    <form style="display:none" method="post" name="save_form">
        <input type="hidden" name="save_action" value="save_action" />
        <input type="hidden" name="office_id" value="<?php echo $office_id; ?>" />
        <input type="hidden" name="geo_list" id="geo_list" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    
    
    
    
    <script>
    //Сохранение привязки
    function save_action()
    {
        var checked_geo_nodes = tree.getChecked();
        
        document.getElementById("geo_list").value = JSON.stringify(checked_geo_nodes);
        
        document.forms["save_form"].submit();
    }
    </script>
    
    
    
    
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3385); ?>
			</div>
			<div class="panel-body">
				<div id="container_A" style="height:350px;"></div>
			</div>
		</div>
	</div>
	
    
    <div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2307); ?>
			</div>
			<div class="panel-body">
				<?php echo translate_str_by_id(3386); ?>
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
        editable:false,//редактируемое
        container:"container_A",//id блока div для дерева
        //Шаблон элемента дерева
    	template:function(obj, common)//Шаблон узла дерева
        	{
        	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
        	    var checkbox = common.checkbox(obj, common);//Чекбокс

  
                return common.icon(obj, common)+ checkbox + common.folder(obj, common) + value_text;
        	},//~template
        view:"tree",
    	select:true,//можно выделять элементы
    	drag:false,//можно переносить
    });
    /*~ДЕРЕВО*/
	webix.event(window, "resize", function(){ tree.adjust(); });
    //-----------------------------------------------------
	//Функция "Отметить все"
	function checkAll()
	{
		tree.checkAll();
	}
	//-----------------------------------------------------
	//Функция "Снять все"
	function uncheckAll()
	{
		tree.uncheckAll();
	}
	//-----------------------------------------------------
    //Инициализация редактора дерева материалов после загруки страницы
    function tree_start_init()
    {
    	var saved_tree = <?php
			$epcGeoDump = '[]';
			if (!empty($tree_dump_JSON)) {
				$epcGeoDump = (string) $tree_dump_JSON;
			} elseif (!empty($geo_tree_dump_JSON)) {
				$epcGeoDump = (string) $geo_tree_dump_JSON;
			}
			echo $epcGeoDump;
		?>;
	    tree.parse(saved_tree);
	    tree.openAll();
    }
    tree_start_init();
    //-----------------------------------------------------
    </script>
    
    
    <script>
    <?php
    //Отметим ранее привязанные узлы
	$checked_geo_nodes = $db_link->prepare("SELECT `geo_id` FROM `shop_offices_geo_map` WHERE `office_id` = ?;");
	$checked_geo_nodes->execute( array($office_id) );
    while($checked_geo_node = $checked_geo_nodes->fetch() )
    {
        ?>
        tree.checkItem(<?php echo $checked_geo_node["geo_id"]; ?>);
        <?php
    }
    ?>
    </script>
    
    <?php
}//else//Действий нет - выводим страницу
?>