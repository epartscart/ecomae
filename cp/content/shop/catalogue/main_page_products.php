<?php
defined('_ASTEXE_') or die('No access');

/**
Страница для управления выводом товаров на главной
*/
?>

<?php
if( !empty($_POST["save_action"]) )//Переход с сохранением структуры
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	$tree_dump = json_decode($_POST["tree_json"], true);
	
	//Флаги ошибок
	$no_error_preclean = true;
	$no_error_groups = true;
	$no_error_products = true;
	
	
	//Предварительно очищаем таблицы товаров на главной
	if( ! $db_link->prepare("DELETE FROM `shop_main_page_groups`")->execute() )
	{
		$no_error_preclean = false;
	}
	if( ! $db_link->prepare("DELETE FROM `shop_main_page_products`")->execute() )
	{
		$no_error_preclean = false;
	}
	
	
	//Теперь сохраняем структуру
	if($no_error_preclean)
	{
		for($g=0; $g < count($tree_dump); $g++)//Группы
		{
			if(!$no_error_products)
			{
				break;
			}
			
			
			$caption = htmlentities($tree_dump[$g]["value"], ENT_QUOTES, "UTF-8", false);
			
			//Мультиязычность. Кастомный алгоритм
			$caption = save_custom_translation( $tree_dump[$g]["value_lang_str_id"], $caption );
			
			
			$show_caption = (int)$tree_dump[$g]["show_caption"];
			$active = (int)$tree_dump[$g]["active"];
			$order = $g + 1;

			if( ! $db_link->prepare("INSERT INTO `shop_main_page_groups` (`caption`, `order`, `show_caption`, `active`) VALUES (?,?,?,?);")->execute( array($caption, $order, $show_caption, $active) ) )
			{
				$no_error_groups = false;
				break;
			}
			
			$group_id = $db_link->lastInsertId();
			
			//Товары группы
			$products = $tree_dump[$g]["data"];
			for( $p = 0; $p < count($products); $p++ )
			{
				$product_id = $products[$p]["product_id"];
				$order = $p + 1;
				
				if( ! $db_link->prepare("INSERT INTO `shop_main_page_products` (`product_id`, `order`, `group_id`) VALUES (?,?,?);")->execute( array($product_id, $order, $group_id) ) )
				{
					$no_error_products = false;
					break;
				}
			}
		}
	}
	
	
	
	
	//Выводим результат работы
    //Выполнено без ошибок
    if($no_error_preclean && $no_error_groups && $no_error_products)
    {
        $success_message = translate_str_by_id(2376);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tovary-na-glavnoj?success_message=<?php echo $success_message; ?>";
        </script>
        <?php
        exit;
    }
    else
    {
        $error_message = translate_str_by_id(2912).": <br>";
        if(!$no_error_preclean)
        {
            $error_message .= translate_str_by_id(2919)."<br>";
        }
        if(!$no_error_groups)
        {
            $error_message .= translate_str_by_id(2920)."<br>";
        }
		if(!$no_error_products)
        {
            $error_message .= translate_str_by_id(2921)."<br>";
        }
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/tovary-na-glavnoj?error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit;
    }
}
else//Действий нет - выводит страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
	
	
	//Исходные данные:
	$main_page_products_tree_dump_JSON = array();
	
	$groups_query = $db_link->prepare("SELECT * FROM `shop_main_page_groups` ORDER BY `order`;");
	$groups_query->execute();
	while( $group_record = $groups_query->fetch() )
	{
		$group = array("is_product"=>false, "product_id"=>0, '$level'=>1, '$parent'=>0, 'value'=>translate_str_by_id($group_record["caption"]), 'value_lang_str_id'=>$group_record["caption"], "show_caption"=>$group_record["show_caption"], "active"=>$group_record["active"], "data"=>array() );
		
		//Запрос товаров
		$products_query = $db_link->prepare("SELECT *, (SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = `shop_main_page_products`.`product_id`) AS `caption` FROM `shop_main_page_products` WHERE `group_id` = ? ORDER BY `order`;");
		$products_query->execute( array($group_record["id"]) );
		while( $product_record = $products_query->fetch() )
		{
			array_push($group["data"], array("is_product"=>true, "product_id"=>$product_record['product_id'], '$level'=>2, '$parent'=>$group_record["id"], 'value'=>translate_str_by_id($product_record['caption'])) );
		}
		
		array_push($main_page_products_tree_dump_JSON, $group);
	}
	
	$main_page_products_tree_dump_JSON = json_encode($main_page_products_tree_dump_JSON);
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
					<div class="panel_a_caption"><?php echo translate_str_by_id(2922); ?></div>
				</a>
				
				<a class="panel_a" onClick="delete_selected_item();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2923); ?></div>
				</a>
				
				<a class="panel_a" onClick="unselect_tree();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/selection_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2268); ?></div>
				</a>
				
				<a class="panel_a" onClick="save_tree();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	<!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none">
        <input name="save_action" id="save_action" type="text" value="save_action" style="display:none"/>
        <input name="tree_json" id="tree_json" type="text" value="" style="display:none"/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!--Форма для отправки-->
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2924); ?>
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
				<?php echo translate_str_by_id(2925); ?>
			</div>
			<div class="panel-body">
				<div id="content_info_div">
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<script src="/lib/iso_9_js_master_translit/translit.js" type="text/javascript"></script>
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
			var value_text = "<span>" + obj.value + "</span>";//Вывод текста
			
			//Индикация материала, снятого с публикации
			var icon_system = "";
			if(obj.active == false)
			{
				icon_system += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
				value_text = "<span style=\"color:#AAA\">" + obj.value + "</span>";//Вывод текста
			}
			
			return common.icon(obj, common) + icon + folder + icon_system + value_text;
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
	webix.event(window, "resize", function(){ tree.adjust(); })
    /*~ДЕРЕВО*/
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
	//После завершения редактирования
	tree.attachEvent("onAfterEditStop", function(id)
    {
    	onSelected();
    });
	//-----------------------------------------------------
	//Перед началом редактирования
	tree.attachEvent("onBeforeEditStart", function(id){
		node = tree.getItem(id);
		
		if(node['$level'] > 1)
		{
			return false;
		}
		
		return true;
	});
	//-----------------------------------------------------
	//Обработчик До перетаскивания узлов дерева
	tree.attachEvent("onBeforeDrop",function(context)
	{
	    var node_id = context.source;//ID переносимого элемента
	    var node_parent_id = tree.getItem(node_id).$parent;//ID исходного родителя элемента
	    var node_parent = tree.getItem(node_parent_id);
		
		
	    var target_id = context.target;//ID того элемента, на место которого переносим
	    var target_parent_id = tree.getItem(target_id).$parent;//ID целевого родителя элемента
		var target_parent = tree.getItem(target_parent_id);
		
		//Перенос допустим. 
		if(node_parent_id == target_parent_id)
		{
			return true;
		}
		
		//Перенос не допустим
		if( node_parent_id == 0 || target_parent_id == 0) //(т.е. один из них не равен 0)
		{
			alert("<?php echo translate_str_by_id(2926); ?>");
			return false;
		}
		
		
		//Перенос товара. Проверяем дублирование товара
		var product_id = tree.getItem(node_id).product_id;//ID товара
		var first_product_node_id_target = tree.getFirstChildId(target_parent_id);//Первый элемент в уровне назначения
		if(first_product_node_id_target != null)
		{
			while(true)
			{
				if(tree.getItem(first_product_node_id_target).product_id == product_id)
				{
					alert("<?php echo translate_str_by_id(2927); ?>");
					return false;
				}
				
				first_product_node_id_target = tree.getNextSiblingId(first_product_node_id_target);//Следующий товар группы
				if(first_product_node_id_target == null)
				{
					break;
				}
			}
		}
		
		
		
		//Во всех остальных случая перенос будет допустим, т.к. уровень переносимого элемента не изменится
		
		return true;
	});
	//-----------------------------------------------------
    //Обработка выбора элемента
    function onSelected()
    {
        //Если категории не созданы
    	if(tree.count() == 0)
    	{
    	    document.getElementById("content_info_div").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("content_info_div_col").setAttribute("style", "display:none");
    	    return;
    	}
    	
    	//Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	if(node_id == 0)
    	{
    	    document.getElementById("content_info_div").innerHTML = "";
			
			//Скрыть контейнер для параметров
			document.getElementById("content_info_div_col").setAttribute("style", "display:none");
    	    return;
    	}
    	
		//Показать контейнер для параметров
		document.getElementById("content_info_div_col").setAttribute("style", "display:block");
		
		
    	var node = "";//Ссылка на объект узла
    	//Выделенный узел
    	node = tree.getItem(node_id);
    	
		
		//Далее в зависимости от типа элемента (группа/товар)
		var parameters_table_html = "";
		if( parseInt(node['$level']) == 1)//Группа
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2928); ?></label><div class=\"col-lg-6\"> "+node.value+" </div></div>";
			
			parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			
			var checked = "";
			if(node.show_caption == true)
			{
				checked = " checked=\"checked\" ";
			}
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2929); ?></label><div class=\"col-lg-6\"><input onchange=\"dynamicApplyingCheck('show_caption');\" type=\"checkbox\" id=\"show_caption\" "+checked+" class=\"form-control\"/></div></div>";
			
			parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			
			checked = "";
			if(node.active == true)
			{
				checked = " checked=\"checked\" ";
			}
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2930); ?></label><div class=\"col-lg-6\"><input onchange=\"dynamicApplyingCheck('active');\" type=\"checkbox\" id=\"active\" "+checked+" class=\"form-control\"/></div></div>";
			
			
			parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			
			
			parameters_table_html += "<div class=\"col-lg-12\"> <button onclick=\"editGroupProducts();\" class=\"btn btn-info \" type=\"button\"><i class=\"fa fa-pencil\"></i> <?php echo translate_str_by_id(2931); ?></button> </div>";
		}
		else if( parseInt(node['$level']) == 2)
		{
			parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2932); ?>: </label><div class=\"col-lg-6\"> "+node.value+" </div></div>";
			
			parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			
			parameters_table_html += "<div class=\"col-lg-12\"> <button onclick=\"delete_selected_item();\" class=\"btn btn-danger \" type=\"button\"><i class=\"fa fa-trash-o\"></i> <?php echo translate_str_by_id(2933); ?></button> </div>";
		}
		else
		{
			alert("<?php echo translate_str_by_id(2934); ?>");
		}
		
		
		document.getElementById("content_info_div").innerHTML = parameters_table_html;
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
	//Функция динамического применения значений чекбоксов
	function dynamicApplyingCheck(attribute)
	{
		var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
		
		if(document.getElementById(attribute).checked == true)
		{
			node[attribute] = 1;
		}
		else
		{
			node[attribute] = 0;
		}
		
		tree.refresh();
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
    	var newItemId = tree.add( {value:"<?php echo translate_str_by_id(2935); ?>", value_lang_str_id:0, show_caption:false, active:true}, tree.count(), 0);
    	
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
    //Сохранение перечня категорий
    function save_tree()
    {
    	//Получаем строку JSON:
    	var tree_json_to_save = tree.serialize();
    	tree_dump = JSON.stringify(tree_json_to_save);
    	
    	//Задаем значение поля в форме:
    	var tree_json_input = document.getElementById("tree_json");
    	tree_json_input.value = tree_dump;
    	
    	document.forms["form_to_save"].submit();//Отправляем
    }
    //-----------------------------------------------------
    //Инициализация редактора дерева материалов после загруки страницы
    function catalogue_start_init()
    {
    	var saved_catalogue = <?php echo $main_page_products_tree_dump_JSON; ?>;
	    tree.parse(saved_catalogue);
	    tree.openAll();
    }
    catalogue_start_init();
    onSelected();//Обработка текущего выделения
    </script>
	
	
	
	
	
	<style>
	#products_area .main_action_div,
	#products_area .product_div_marks
	{
		display:none;
	}
	#products_area .product_checkbox
	{
		cursor: pointer;
		width: 20px;
		height: 20px;
		margin: 0;
	}
	#products_area .product_div_tile {
		height: 310px;
		overflow: hidden;
	}
	#products_area .showAnother_tile {
		height: 310px;
	}
	#products_area .product_div_list_photo > .product_div_name, 
	#products_area .product_div_list > .product_div_name
	{
		width: auto;
		right: 0;
	}
	#products_area .product_div_tile > .product_div_name {
		white-space: normal;
	}
	#products_area .product_div_image_wrap a,
	#products_area .product_div_name a
	{
	  pointer-events: none;
	  cursor: default;
	}
	</style>
	
	
	<!-- Модальное окно "Редактирование товаров группы" -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalWindow_productsEdit" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(2936); ?></h4>
					</div>
					<div class="modal-body">
						<div class="row">
							<div id="container_B" style="height:350px;">
							</div>
							
							<button onclick="catalogue_tree.openAll();" class="btn btn-primary2 " type="button"><i class="fa fa-folder-open"></i> <span class="bold"><?php echo translate_str_by_id(2937); ?></span></button>
						
							<button onclick="catalogue_tree.closeAll();" class="btn btn-primary " type="button"><i class="fa fa-folder"></i> <span class="bold"><?php echo translate_str_by_id(2938); ?></span></button>
							
							<button onclick="unselect_tree_categories();" class="btn btn-primary " type="button"><span class="bold"><?php echo translate_str_by_id(2268); ?></span></button>
						</div>
						<br/>
						<div class="row">
							<div style="height:650px;">
								<div class="row">
								
								 <!-- БЛОК ДЛЯ РАБОТЫ С ТОВАРАМИ (ВЫДЕЛЕНИЕ, СНЯТИЕ И Т.Д.) -->
								<script>
									// -----------------------------------------------------------------------------------------------------------
									//Получение отмеченных продуктов (список ID)
									function getCheckedProducts()
									{
										var products_checkboxes = document.getElementsByClassName("product_checkbox");
										
										var products_checked = new Array();
										
										for(var i=0; i < products_checkboxes.length; i++)
										{
											if(products_checkboxes[i].checked == true)
											{
												let product = new Object;
												product.product_id = products_checkboxes[i].getAttribute("product_id");
												product.value = products_checkboxes[i].getAttribute("product_caption");
												products_checked.push(product);
											}
										}
										
										return products_checked;
									}
									// -----------------------------------------------------------------------------------------------------------
									//Отметить все (true) / Снять все (false)
									function checkAll(check)
									{
										var products_checkboxes = document.getElementsByClassName("product_checkbox");
										
										for(var i=0; i < products_checkboxes.length; i++)
										{
											products_checkboxes[i].checked = check;
										}
									}
									// -----------------------------------------------------------------------------------------------------------
								</script>
								
								<?php
								$is_products_mode = true;//Флаг - страница работает в режиме отображения товаров
								$category_block_type = 2;//Тип блоков категорий - для редактирования справочников товаров (используется в /content/shop/catalogue/printCategories.php)
								$product_block_type = 2;//Параметр для скрипта /content/shop/catalogue/printProducts.php - знать, как выводить товары
    
								//ID категории для отображения
								if(!empty($_GET["category_id"]))
								{
									$category_id = $_GET["category_id"];
								}
								else
								{
									$category_id = 0;
								}
    
								//Общий скрипт вывода товаров в основную область страницы
								require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printProducts.php");
								?>
								
								</div>
								
								<div class="hidden" id="side_properties_widgets_div"></div>
							
							</div>
						</div>
					</div>
					<div class="modal-footer">
						<button onclick="checkAll(true);" class="btn btn-primary2 " type="button"><i class="fa fa-check-square"></i> <span class="bold"><?php echo translate_str_by_id(2293); ?></span></button>
						<button onclick="checkAll(false);" class="btn btn-primary " type="button"><i class="fa fa-square-o"></i> <span class="bold"><?php echo translate_str_by_id(2294); ?></span></button>
						<button onclick="applyProductsChecks();" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(2189); ?></span></button>
						<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2190); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<script>
	//-----------------------------------------------------
	//Кнопка "Список товаров группы"
	var catalogue_tree = "";
	function editGroupProducts()
	{
		//Сбрасываем старое дерево
		catalogue_tree = "";
		document.getElementById("container_B").innerHTML = "";
		
		//Формирование дерева каталога
		catalogue_tree = new webix.ui({
			
			//Шаблон элемента дерева
			template:function(obj, common)//Шаблон узла дерева
        	{
                var folder = common.folder(obj, common);
        	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
				var checkbox = "";
				
        	    //Чекбоксы только для товаров
				if(obj.is_product == true)
                {
                    checkbox = common.checkbox(obj, common);
                }
				
                return common.icon(obj, common) + checkbox + folder + value_text;
        	},//~template
			
			
			editable:false,//редактируемое
			container:"container_B",//id блока div для дерева
			view:"tree",
			select:true,//можно выделять элементы
			drag:false,//можно переносить
		});
		
		webix.event(window, "resize", function(){ catalogue_tree.adjust(); });
		
		var catalogue = <?php echo $catalogue_tree_dump_JSON; ?>;
		catalogue_tree.parse(catalogue);
		
		//Событие перед выбором элемента дерева
		catalogue_tree.attachEvent("onBeforeSelect", function(category_id)
		{
			ref_getProductsHTML(category_id);
		});
		
		//После отображения окна - подгоняем дерево под размер
		$('#modalWindow_productsEdit').on('shown.bs.modal',function(){
			catalogue_tree.adjust();
		});
		
		checkAll(false);// Снять ранее выделенные элементы
		
		$('#modalWindow_productsEdit').modal();//Открыть окно
	}
	//Снятие выделения с дерева
    function unselect_tree_categories()
    {
    	catalogue_tree.unselect();
    	ref_getProductsHTML(0);
    }
	function ref_getProductsHTML(category_id){
		document.getElementById("products_area").innerHTML = "<div class=\"text-center\" id=\"start_loading_div\"><p><?php echo translate_str_by_id(2939); ?></p><img src=\"/content/files/images/ajax-loader-transparent.gif\" class=\"loading_img\" /></div>";
		
		propucts_request.category_id = category_id;
		propucts_request.needPagesCount = 1;//Нужна одна страница
        propucts_request.startFrom = 0;
        propucts_request.innerHTML_mode = "refresh";//Способ работы с innerHTML блока товаров (add/refresh)
		
		productsCountRequest();
		getProductsHTML();
		
	}
	//-----------------------------------------------------
	//Функция выставления галочки для товаров, которые уже находятся в группе
	function checkProductNodeByProductId(product_id, catalogue_tree_JSON)
	{
		for(var i=0; i < catalogue_tree_JSON.length; i++)
        {
			if(catalogue_tree_JSON[i].product_id == product_id)
			{
				catalogue_tree.checkItem(catalogue_tree_JSON[i].id);
				return;
			}
			
			if(catalogue_tree_JSON[i]["data"] != null)
			{
				checkProductNodeByProductId(product_id, catalogue_tree_JSON[i]["data"]);
			}
		}
	}
	//-----------------------------------------------------
	//Кнопка "Применить" в окне выбора товара
	function applyProductsChecks()
	{
		//Выделенный узел
    	var group_node_id = tree.getSelectedId();//ID выделенного узла

		//Теперь по циклу добавляем узлы товаров в дерево
		var checked_products = getCheckedProducts();
		for(var i=0; i < checked_products.length; i++)
		{
			let flag = true;
			let all = tree.serialize();
			
			for(var j=0; j < all.length; j++)
			{
				if(all[j].id == group_node_id){
					if(all[j].data){
						for(var h=0; h < all[j].data.length; h++)
						{
							if(all[j].data[h].product_id === checked_products[i].product_id){
								flag = false;
							}
						}
					}
				}
			}
			
			if(flag === true){
				var newItemId = tree.add( {value:checked_products[i].value, product_id:checked_products[i].product_id}, tree.count(), group_node_id);
			}
		}
		
		tree.openAll();
		
		//Скрыть окно выбора товаров
		$('#modalWindow_productsEdit').modal('hide');
	}
	//-----------------------------------------------------
	</script>
	<?php
}
?>