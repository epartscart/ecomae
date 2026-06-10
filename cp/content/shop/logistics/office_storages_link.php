<?php
/**
 * Скрипт страницы настройки связи магазина и складов. Управление наценками
*/
defined('_ASTEXE_') or die('No access');
?>


<?php
require_once("content/users/dp_group_record.php");//Определение класса записи группы пользователей
?>


<?php
if(!empty($_POST["save_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    //0. Данные
    $office_id = $_POST["office_id"];
    
    
    //1 УДАЛЯЕМ СТАРЫЕ записи
	if( $db_link->prepare("DELETE FROM `shop_offices_storages_map` WHERE `office_id` = ?;")->execute( array($office_id) ) != true)
    {
        $error_message = translate_str_by_id(3387);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office/storages_link?office_id=<?php echo $office_id; ?>&error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit;
    }
    
    
    //2. СОЗДАЕМ НОВЫЕ ЗАПИСИ:
    $storages_list = json_decode($_POST["storages_list"], true);
    $SQL_INSERT = "INSERT INTO `shop_offices_storages_map` (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`) VALUES ";
    $first_record = true;//Первая запись (для простановки запитых в SQL запросе)
    $binding_values = array();
	for($i=0; $i < count($storages_list); $i++)
    {
        if($storages_list[$i]["checked"] == false)
        {
            continue;//Склад не подключен к магазину
        }
        
        $storage_id = $storages_list[$i]["id"];
        $additional_time = $storages_list[$i]["time_to_shop"];
        

        $groups = $storages_list[$i]["groups"];//Объекты групп
        for($g=0; $g < count($groups); $g++)
        {
            $group_id = $groups[$g]["id"];
            $prices_ranges = $groups[$g]["prices_ranges"];//Объекты диапазонов учетных цен
            $min_point = 0;//Минимальная цена для первого диапазона
            for($r=0; $r < count($prices_ranges); $r++)
            {
                $range = $prices_ranges[$r];//Объект диапазона цены
                $max_point = $range["max_point"];
                if($max_point == -1) $max_point = 999999999999;//Для бесконечности
                $markup = $range["markup"];
                
                if(!$first_record)$SQL_INSERT .= ", ";
                $SQL_INSERT .= "(?, ?, ?, ?, ?, ?, ?)";
				array_push($binding_values, $office_id);
				array_push($binding_values, $storage_id);
				array_push($binding_values, $group_id);
				array_push($binding_values, $min_point);
				array_push($binding_values, $max_point);
				array_push($binding_values, $markup);
				array_push($binding_values, $additional_time);
				
                if($first_record)$first_record = false;
                
                $min_point = $max_point;//Минимальная цена для следующего диапазона
            }//for($r)
        }//for($g)
    }
    $SQL_INSERT .= ";";
    //Если $first_record == false, значит есть по крайней мере одна запись - делаем запрос
    $result_insert = true;//Результат запроса на внесение наценок
    if(!$first_record)
    {
        if( $db_link->prepare($SQL_INSERT)->execute($binding_values) != true)
        {
            $result_insert = false;
        }
    }
    
    
    //ОБРАБОТКА РЕЗУЛЬТАТА СОХРАНЕНИЯ
    if($result_insert)
    {
        $success_message = translate_str_by_id(2157);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office/storages_link?office_id=<?php echo $office_id; ?>&success_message=<?php echo $success_message; ?>";
        </script>
        <?php
        exit;
    }
    else
    {
        $error_message = translate_str_by_id(2912).": <br>";
        if(!$result_insert)
        {
            $error_message .= translate_str_by_id(3388)."<br>";
        }
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office/storages_link?office_id=<?php echo $office_id; ?>&error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit;
    }
    
}//~if(!empty($_POST["save_action"]))
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	
    //Исходные данные:
    $page_title = translate_str_by_id(3389);
    if(empty($_GET["office_id"]))
    {
        exit;
    }
    $id = $_GET["office_id"];

    ?>
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
	

    <form name="save_form" method="POST" style="display:none">
        <input type="hidden" name="save_action" value="save_action" />
        <input type="hidden" name="office_id" value="<?php echo $id ; ?>" />
        <input type="hidden" name="storages_list" id="storages_list" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    
    
	
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
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/offices/office?office_id=<?php echo $id; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/office.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3248); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/offices/office/geo_nodes?office_id=<?php echo $id; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/geo_link.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3390); ?></div>
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
	
	
	

    


    <script>
    //Скрипты для работы с настройкой складов
    var storages_list = new Array();//Массив с объектами складов (Объект склада - это объект описания подключения данного склада к магазину)
    var storages_id_to_index_map = new Array();//Массив для связи id склада с его индексом в var storages_list
    /*Каждый объект содержит:
    - id склада
    - флаг - "Подключен"
    - срок доставки до магазина
    - массив объектов групп пользователей (объект группы пользователей - это объект описания диапазонов цен для данной группы)
    */
    <?php
    //Заполняем список складов
    $SQL_SELECT_STORAGES = "SELECT 
		`shop_storages`.`id` AS `id`, 
		`shop_storages`.`name` AS `name`, 
		`shop_storages`.`interface_type` AS `interface_type`, 
		`shop_storages`.`users` AS `users`, 
		`shop_storages`.`connection_options` AS `connection_options`, 
		`shop_storages_interfaces_types`.`product_type` AS `product_type` FROM `shop_storages` INNER JOIN `shop_storages_interfaces_types` ON `shop_storages`.`interface_type` = `shop_storages_interfaces_types`.`id` GROUP BY `shop_storages`.`id`";
	$storages_query = $db_link->prepare($SQL_SELECT_STORAGES);
	$storages_query->execute();
	while( $storage = $storages_query->fetch() )
    {
        ?>
        storages_list[storages_list.length] = new Object;
        storages_list[storages_list.length-1].id = <?php echo $storage['id']; ?>;//ID склада
        storages_list[storages_list.length-1].checked = false;//По умолчанию - отключен
        storages_list[storages_list.length-1].name = "<?php echo $storage["name"]; ?>";//Название склада
        storages_list[storages_list.length-1].selected = false;//Флаг - не выделен в данный момент в дереве (используется для отображения в браузере)
        storages_list[storages_list.length-1].product_type = <?php echo $storage["product_type"]; ?>;
        storages_list[storages_list.length-1].time_to_shop = 0;//Время доставки до склада
        storages_list[storages_list.length-1].groups = new Array();
        
        //Связываем индекс в списке складов с его id (id склада)
        storages_id_to_index_map[<?php echo $storage["id"]; ?>] = storages_list.length-1;
        <?php
    }
    
    //Получаем список групп
    ?>
    var user_groups_list = new Array();//Список групп пользователей (линейный)
    var user_groups_id_to_index_map = new Array();//Массив для связи id группы с его индексом в var user_groups_list
    <?php
	$groups_query = $db_link->prepare("SELECT `id` FROM `groups`");
	$groups_query->execute();
    while( $group = $groups_query->fetch() )
    {
        ?>
        user_groups_list[user_groups_list.length] = new Object;
        user_groups_list[user_groups_list.length - 1].id = <?php echo $group["id"]; ?>;
        user_groups_list[user_groups_list.length - 1].selected = false;//Флаг - не выделена в данный момент в дереве (используется для отображения в браузере)
        
        //Связываем индекс в списке групп с ее id (id группы)
        user_groups_id_to_index_map[<?php echo $group["id"]; ?>] = user_groups_list.length - 1;
        <?php
    }
    ?>
    //Копируем массив с группами пользователей в каждый объект списка складов
    for(var i=0; i < storages_list.length; i++)
    {
        for(var g=0; g < user_groups_list.length; g++)
        {
            var group = JSON.stringify(user_groups_list[g]);
            storages_list[i].groups.push(JSON.parse(group));
            storages_list[i].groups[storages_list[i].groups.length-1].prices_ranges = new Array();//Массив с диапазонами цен
            
            
            var price_range = new Object;
            price_range.max_point = -1;//Начальная точка диапазона. -1 = Бесконечность
            price_range.markup = 0;//Наценка
            
            storages_list[i].groups[storages_list[i].groups.length-1].prices_ranges.push(price_range);//Добавляем объект наценки для данной группы для данного склада
        }
    }
    
    // ------------------------------------------------------------------------------------
    //Обработка выделения склада
    function onStorageSelected()
    {
        //Получаем текущий выделенный склад
        var selected_storage_id = storages_tree.getSelectedId();
        if(selected_storage_id == 0)
        {
            //Скрываем все виджеты для склада:
            document.getElementById("general_options_div").setAttribute("style", "display:none");
            document.getElementById("groups_div").setAttribute("style", "display:none");
            document.getElementById("groups_prices_div").setAttribute("style", "display:none");
            return;
        }
        
        
        //Инициализируем поле "Время доставки до магазина"
        document.getElementById("time_to_shop_input").value = storages_list[storages_id_to_index_map[selected_storage_id]].time_to_shop;
        document.getElementById("time_to_shop_input").setAttribute("style", "");
        
        //Выделяем группу, которая для этого склада была выделена последней
        for(var i=0; i < user_groups_list.length; i++)//Цикл работает по общему списку групп
        {
            if(storages_list[storages_id_to_index_map[selected_storage_id]].groups[i].selected == true)
            {
                users_group_tree.select(storages_list[storages_id_to_index_map[selected_storage_id]].groups[i].id);
                break;
            }
            users_group_tree.unselectAll();
        }
        
        
        //После инициализации всех виджетов - показываем их
        document.getElementById("general_options_div").setAttribute("style", "display:block;");
        document.getElementById("groups_div").setAttribute("style", "display:block;");
        
        onGroupSelected();//Обработка выделения группы
    }
    // ------------------------------------------------------------------------------------
    //Обработка выделения группы (склад выделен обязательно уже)
    function onGroupSelected()
    {
        var selected_storage_id = storages_tree.getSelectedId();//ID текущего склада
        
        //Работа с текущей выделенной группой
        var selected_group_id = users_group_tree.getSelectedId();//Id выделенной группы
        if(selected_group_id == 0)
        {
            document.getElementById("groups_prices_div").setAttribute("style", "display:none");
            return;
        }
        
        
        //Снимаем предыдущую выделенную группу
        for(var i=0; i < user_groups_list.length; i++)//Цикл работает по общему списку групп, но внутри цикла работаем с копией этого списка в объекте склада
        {
            if(storages_list[storages_id_to_index_map[selected_storage_id]].groups[i].selected == true)
            {
                storages_list[storages_id_to_index_map[selected_storage_id]].groups[i].selected = false;
                break;
            }
        }
        storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].selected = true;
        
        
        document.getElementById("groups_prices_div").setAttribute("style", "display:block;");
        
        
        //Далее идет инициализация блока настройки наценки для группы
        prices_ranges_tree.clearAll();//Очищаем все дерево наценок
        //Получаем массив с объектами дипазонов для данной группы
        var prices_ranges = storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges;
        for(var i=0; i < prices_ranges.length; i++)
        {
            prices_ranges_tree.add({value:prices_ranges[i].max_point+""}, prices_ranges_tree.count(), 0);
        }
        
        
        //Обработка выделения диапазона цен
        prices_ranges_tree_selected();
    }
    // ------------------------------------------------------------------------------------
    </script>
	
	<div class="row">
		<div class="col-lg-12">
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(3391); ?>
					</div>
					<div class="panel-body">
						<div id="container_A_storages" style="height:200px;">
						</div>
						<script type="text/javascript" charset="utf-8">
						/*ДЕРЕВО СКЛАДОВ*/
						//Формирование дерева
						storages_tree = new webix.ui({
							//Шаблон элемента дерева
							template:function(obj, common)//Шаблон узла дерева
								{
									var value_text = "<span>" + obj.value + "</span>";//Вывод текста
									var checkbox = common.checkbox(obj, common);//Чекбокс
					
					  
									return common.icon(obj, common)+ checkbox + common.folder(obj, common) + value_text;
								},//~template
						
						
						
							editable:false,//редактируемое
							container:"container_A_storages",//id блока div для дерева
							view:"tree",
							select:true,//можно выделять элементы
							drag:false,//можно переносить
						});
						webix.event(window, "resize", function(){ storages_tree.adjust(); });
						//Событие при выборе элемента дерева
						storages_tree.attachEvent("onAfterSelect", function(id)
						{
							onStorageSelected();
						});
						/*~ДЕРЕВО*/
						//-----------------------------------------------------
						//Инициализация редактора дерева
						function storages_tree_start_init()
						{
							for(var i=0; i < storages_list.length; i++)
							{
								storages_tree.add({id:storages_list[i].id, value:storages_list[i].name}, storages_tree.count(), 0);
							}
						}
						storages_tree_start_init();
						//-----------------------------------------------------
						</script>
					</div>
				</div>
			</div>
			
			

			
			<div class="col-lg-6" id="general_options_div">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(3392); ?>
					</div>
					<div class="panel-body">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(3393); ?>
							</label>
							<div class="col-lg-6">
								<input class="form-control" type="text" id="time_to_shop_input" onKeyUp="dynamicApplying('time_to_shop_input');" />
							</div>
						</div>
					</div>
				</div>
			</div>
	
		</div>
	</div>
	
	
	<div class="row">
		<div class="col-lg-12">
			<div class="col-lg-6" id="groups_div">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(747); ?>
					</div>
					<div class="panel-body">
						<?php
						require_once("content/users/get_group_records.php");//Получение объекта иерархии существующих групп для вывода в дерево-webix
						?>
						<div id="container_A_users_groups" style="height:150px;">
						</div>
						<script type="text/javascript" charset="utf-8">
						/*ДЕРЕВО ГРУПП*/
						//Формирование дерева
						users_group_tree = new webix.ui({
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
									
									
									return common.icon(obj, common) + common.folder(obj, common)  + icon + value_text;
								},//~template
						
						
						
							editable:false,//редактируемое
							container:"container_A_users_groups",//id блока div для дерева
							view:"tree",
							select:true,//можно выделять элементы
							drag:false,//можно переносить
						});
						webix.event(window, "resize", function(){ users_group_tree.adjust(); });
						//Событие при выборе элемента дерева групп
						users_group_tree.attachEvent("onAfterSelect", function(id)
						{
							onGroupSelected();
						});
						/*~ДЕРЕВО*/
						//-----------------------------------------------------
						//Инициализация редактора дерева материалов после загруки страницы
						function users_group_tree_start_init()
						{
							var saved_tree = <?php echo $group_tree_dump_JSON; ?>;
							users_group_tree.parse(saved_tree);
							users_group_tree.openAll();
						}
						users_group_tree_start_init();
						//-----------------------------------------------------
						</script>
					</div>
				</div>
			</div>
			
			
			<div class="col-lg-6" id="groups_prices_div">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(3394); ?>
					</div>
					<div class="panel-body">
					
						<div class="col-lg-6">
							<input placeholder="<?php echo translate_str_by_id(3395); ?>" type="text" id="new_point_input" class="form-control" />
										

							<div style="padding:2px;">
							<button onclick="add_new_price_point();" class="btn btn-success " type="button"><i class="fa fa-plus"></i> <span class="bold"><?php echo translate_str_by_id(2267); ?></span></button>
							
							<button onclick="delete_price_point();" class="btn btn-danger " type="button"><i class="fa fa-trash-o"></i> <span class="bold"><?php echo translate_str_by_id(2224); ?></span></button>
							</div>
							
							
					

							
							<!-- Start Список диапазонов цен -->
							<div id="container_A_prices" style="height:150px;">
							</div>
							<!-- End Список диапазонов цен -->
							<script>
							/*ДЕРЕВО СКЛАДОВ*/
							//Формирование дерева
							prices_ranges_tree = new webix.ui({
								//Шаблон элемента дерева
								template:function(obj, common)//Шаблон узла дерева
									{
										var value_text = "<span>" + obj.value + "</span>";//Вывод текста
										if(obj.value == "-1")
										{
											value_text = "<span><font style=\"font-weight:bold;font-size:16px\">∞</font></span>";//Вывод текста
										}
						  
										return common.icon(obj, common) + common.folder(obj, common) + value_text;
									},//~template
								editable:false,//редактируемое
								container:"container_A_prices",//id блока div для дерева
								view:"tree",
								select:true,//можно выделять элементы
								drag:false,//можно переносить
							});
							webix.event(window, "resize", function(){ prices_ranges_tree.adjust(); });
							//Событие при выборе элемента дерева
							prices_ranges_tree.attachEvent("onAfterSelect", function(id)
							{
								//Инициализируем настройку наценки для выбранного диапазона
								prices_ranges_tree_selected();
							});
							/*~ДЕРЕВО*/
							// -------------------------------------------------------------------------------------
							//Добавить новую точку цены
							function add_new_price_point()
							{
								//Проверка корректности введенного значения
								var entered_value = parseInt(document.getElementById("new_point_input").value);
								if(isNaN(entered_value) || entered_value <= 0)
								{
									alert("<?php echo translate_str_by_id(3396); ?>");
									document.getElementById("new_point_input").value = "";
									return;
								}
								
							
								//1. Получить текущий склад и группу
								var selected_storage_id = storages_tree.getSelectedId();//ID текущего склада
								var selected_group_id = users_group_tree.getSelectedId();//Id выделенной группы
								
								//1.1. Проверить существование такой же точки
								var prices_ranges = storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges;
								for(var i=0; i < prices_ranges.length; i++)
								{
									if(parseInt(prices_ranges[i].max_point) == parseInt(document.getElementById("new_point_input").value))
									{
										alert("<?php echo translate_str_by_id(3397); ?>");
										document.getElementById("new_point_input").value = "";
										return;
									}
								}
							
								
								
								//1.2 Создать новый объект точки диапазона
								var new_point = new Object;
								new_point.max_point = document.getElementById("new_point_input").value;
								new_point.min_point = 0;//Это нижний предел диапазона - он будет инициализирован при нажатии кнопки сохранить
								new_point.markup = 0;//Наценка для диапазона
								
								//2. В его выделенную группу добавить новый объект наценки
								storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges.push(new_point);//Добавляем объект наценки для данной группы для данного склада
								
								//3. Упорядочить их
								/**Сортировка объектов по точкам диапазона*/
								storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges.sort
								(
									function (x, y)
									{
										//-1 = бесконечность, обрабатываем этот момент, т.е. в любом случаем (-1) - больше всех
										if(parseInt (y ["max_point"]) == -1)
										{
											return -1;
										}
										if(parseInt (x ["max_point"]) == -1)
										{
											return 1;
										}
										
										return (parseInt (x ["max_point"]) - parseInt (y ["max_point"]))
									}
								);
								/**~Сортировка объектов по точкам диапазона*/
								
								
								//4. Обработать выделение группы
								onGroupSelected();
								
								//5. Очищаем поле ввода
								document.getElementById("new_point_input").value = "";
								
								//6. Обрабаотываем выделение
								prices_ranges_tree_selected();
							}
							// -------------------------------------------------------------------------------------
							//Обработка выделения элемента в списке диапазонов
							function prices_ranges_tree_selected()
							{
								var selected_storage_id = storages_tree.getSelectedId();//ID текущего склада
								var selected_storage_item = storages_tree.getItem(selected_storage_id);
								
								var selected_group_id = users_group_tree.getSelectedId();//Id выделенной группы
								var selected_group_item = users_group_tree.getItem(selected_group_id);
								
								var selected_price_range_id = prices_ranges_tree.getSelectedId();//Выделенный диапазон
								var selected_price_range_item = prices_ranges_tree.getItem(selected_price_range_id);
								if(selected_price_range_id == 0)
								{
									document.getElementById("prices_range_turn").innerHTML = "";
									return;
								}
								
								//Крайние точки диапазона
								var max_point = selected_price_range_item.value;
								if(max_point == -1)
								{
									max_point = "∞";
								}
								var min_point_id = prices_ranges_tree.getPrevId(selected_price_range_id);
								console.log(min_point_id);
								var min_point = 0;
								if(min_point_id != undefined)
								{
									min_point_item = prices_ranges_tree.getItem(min_point_id);
									min_point = min_point_item.value;
								}

								
								var prices_range_turn_text = "";
								
								prices_range_turn_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3398); ?></label><div class=\"col-lg-6\">"+selected_storage_item.value+"</div></div>";
								prices_range_turn_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
								prices_range_turn_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3399); ?></label><div class=\"col-lg-6\">"+selected_group_item.value+"</div></div>";
								prices_range_turn_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
								prices_range_turn_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3400); ?></label><div class=\"col-lg-6\">"+min_point+" - "+max_point+" (<?php echo translate_str_by_id(3402); ?>)</div></div>";
								prices_range_turn_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
								prices_range_turn_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3401); ?>, %</label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('markup_input');\" id=\"markup_input\" class=\"form-control\" /></div></div>";
								

								
								
								document.getElementById("prices_range_turn").innerHTML = prices_range_turn_text;
								
								//Наценка для диапазона:
								var markup = 0;
								var prices_ranges = storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges;
								for(var i=0; i < prices_ranges.length; i++)
								{
									if(parseInt(selected_price_range_item.value) == parseInt(prices_ranges[i].max_point))
									{
										markup = storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges[i].markup;
										document.getElementById("markup_input").value = markup;
										break;
									}
								}
							}
							// -------------------------------------------------------------------------------------
							//Динамическое присвоение значений
							function dynamicApplying(attribute)
							{
								if(attribute == "markup_input")
								{
									var markup = document.getElementById("markup_input").value;
									if(markup == "") markup = 0;
									var markup = parseInt(markup);
									if(isNaN(markup) || markup < -99)
									{
										webix.message({type:"error", text:"Введите значение от -99 до ∞"});
										document.getElementById("markup_input").setAttribute("style", "outline-style:none; border: 2px solid #C00;");
										return;
									}
									
									
									//Присваиваем значение
									var selected_storage_id = storages_tree.getSelectedId();//ID текущего склада
									
									var selected_group_id = users_group_tree.getSelectedId();//Id выделенной группы
									
									var selected_price_range_id = prices_ranges_tree.getSelectedId();//Выделенный диапазон
									var selected_price_range_item = prices_ranges_tree.getItem(selected_price_range_id);//Узел webix точки цены
									
									var prices_ranges = storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges;
									for(var i=0; i < prices_ranges.length; i++)
									{
										if(parseInt(selected_price_range_item.value) == parseInt(prices_ranges[i].max_point))
										{
											storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges[i].markup = markup;
											document.getElementById("markup_input").setAttribute("style", "outline-style:none; border: 2px solid #0A0;");
											break;
										}
									}
								}//~if(attribute == "markup_input")
								else if(attribute == "time_to_shop_input")//Присвоение срока доставки
								{
									var time_to_shop = document.getElementById("time_to_shop_input").value;
									if(time_to_shop == "") time_to_shop = 0;
									var time_to_shop = parseInt(time_to_shop);
									if(isNaN(time_to_shop) || time_to_shop < 0)
									{
										webix.message({type:"error", text:"<?php echo translate_str_by_id(3403); ?>"});
										document.getElementById("time_to_shop_input").setAttribute("style", "outline-style:none; border: 2px solid #C00;");
										return;
									}
									
									//Присваиваем значение
									var selected_storage_id = storages_tree.getSelectedId();//ID текущего склада
									storages_list[storages_id_to_index_map[selected_storage_id]].time_to_shop = time_to_shop;
									document.getElementById("time_to_shop_input").setAttribute("style", "outline-style:none; border: 2px solid #0A0;");
								}
							}//~function dynamicApplying(attribute)
							// -------------------------------------------------------------------------------------
							//Удаление выделенного диапазона
							function delete_price_point()
							{
								var selected_price_range_id = prices_ranges_tree.getSelectedId();//Выделенный диапазон
								if(selected_price_range_id == 0)
								{
									alert("<?php echo translate_str_by_id(3404); ?>");
									return;
								}
								var selected_price_range_item = prices_ranges_tree.getItem(selected_price_range_id);
								if(selected_price_range_item.value == -1)
								{
									alert("<?php echo translate_str_by_id(3405); ?>");
									return;
								}
								
								var selected_storage_id = storages_tree.getSelectedId();//ID текущего склада
								var selected_storage_item = storages_tree.getItem(selected_storage_id);
								
								var selected_group_id = users_group_tree.getSelectedId();//Id выделенной группы
								var selected_group_item = users_group_tree.getItem(selected_group_id);
								
								//Определяем индекс данного диапазона в ОБЪЕКТЕ ОПИСАНИЯ
								var prices_ranges = storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges;
								for(var i=0; i < prices_ranges.length; i++)
								{
									if(prices_ranges[i].max_point == selected_price_range_item.value)
									{
										storages_list[storages_id_to_index_map[selected_storage_id]].groups[user_groups_id_to_index_map[selected_group_id]].prices_ranges.splice(i,1);
									}
								}
								
								onGroupSelected();//Обработаем выдение группы
							}
							// -------------------------------------------------------------------------------------
							</script>
							
							
						</div>
						<div class="col-lg-6" id="prices_range_turn">
						</div>
					

					</div>
				</div>
			</div>
	
		</div>
	</div>

    <script>
    //Функция сохранения
    function save_action()
    {
        //1.1 Получить отмеченные склады (т.е. те склады, которые подключены к данному магазину)
        var ckecked_storages = storages_tree.getChecked();
        
        if(ckecked_storages.length == 0)
        {
            if(!confirm("<?php echo translate_str_by_id(3406); ?>"))
            {
                return;
            }
        }
        
        //1.1.1 Сбросить предварительно установку флагов checked в списке складов
        for(var i=0; i < storages_list.length; i++)
        {
            storages_list[i].checked = false;
        }
        
        
        //1.1.2 Инициализировать в списке объектов складов отмеченные
        for(var i=0; i < ckecked_storages.length; i++)
        {
            storages_list[storages_id_to_index_map[ckecked_storages[i]]].checked = true;
        }
        
        
        //2. Инициализируем форму сохранение
        var storages_list_json = JSON.stringify(storages_list);
        document.getElementById("storages_list").value = storages_list_json;
        
        
        //ОТПРАВЛЯЕМ ФОРМУ
        document.forms["save_form"].submit();
    }
    </script>
    
    
    
    
    
    <script>
    <?php
    //ИНИЦИАЛИЗАЦИЯ ПРИ ОТКРЫТИИ
    //Наценки:
	$markups_query = $db_link->prepare("SELECT * FROM `shop_offices_storages_map` WHERE `office_id` = ?;");
	$markups_query->execute( array($id) );
    while($markup_record = $markups_query->fetch() )
    {
        ?>
        //Если есть такой склад
        if(storages_id_to_index_map[<?php echo $markup_record["storage_id"]; ?>] != undefined)
        {
            //Если есть такая группа
            if(storages_list[storages_id_to_index_map[<?php echo $markup_record["storage_id"]; ?>]].groups[user_groups_id_to_index_map[<?php echo $markup_record["group_id"]; ?>]] != undefined)
            {
                //Узел "До бесконечности". Он уже однозначно есть. Он последний. Указываем для него наценку
                if(<?php echo $markup_record["max_point"]; ?> >= 2147483647)
                {
                    //Длина массива объектов диапазона для данной группы
                    var prices_ranges_length = storages_list[storages_id_to_index_map[<?php echo $markup_record["storage_id"]; ?>]].groups[user_groups_id_to_index_map[<?php echo $markup_record["group_id"]; ?>]].prices_ranges.length;
                    storages_list[storages_id_to_index_map[<?php echo $markup_record["storage_id"]; ?>]].groups[user_groups_id_to_index_map[<?php echo $markup_record["group_id"]; ?>]].prices_ranges[prices_ranges_length-1].markup = <?php echo $markup_record["markup"]; ?>;
                }
                else//Создаем объект диапазона, добавлям его в массив и упорядочиваем
                {
                    var new_point = new Object;
                    new_point.max_point = <?php echo $markup_record["max_point"]; ?>;
                    new_point.min_point = 0;//Это нижний предел диапазона - он будет инициализирован при нажатии кнопки сохранить
                    new_point.markup = <?php echo $markup_record["markup"]; ?>;//Наценка для диапазона
                    //Добавляем объект
                    storages_list[storages_id_to_index_map[<?php echo $markup_record["storage_id"]; ?>]].groups[user_groups_id_to_index_map[<?php echo $markup_record["group_id"]; ?>]].prices_ranges.push(new_point);
                    
                    /**Сортировка объектов по точкам диапазона*/
                    storages_list[storages_id_to_index_map[<?php echo $markup_record["storage_id"]; ?>]].groups[user_groups_id_to_index_map[<?php echo $markup_record["group_id"]; ?>]].prices_ranges.sort
                    (
                        function (x, y)
                        {
                            //-1 = бесконечность, обрабатываем этот момент, т.е. в любом случаем (-1) - больше всех
                            if(parseInt (y ["max_point"]) == -1)
                            {
                                return -1;
                            }
                            if(parseInt (x ["max_point"]) == -1)
                            {
                                return 1;
                            }
                            
                            return (parseInt (x ["max_point"]) - parseInt (y ["max_point"]))
                        }
                    );
                    /**~Сортировка объектов по точкам диапазона*/
                }
            }
            storages_tree.checkItem(<?php echo $markup_record["storage_id"]; ?>);//Отмечаем в дереве
        }
        
        
        
        <?php
    }//~while
    //Сроки доставки:
	$times_to_shop_query = $db_link->prepare("SELECT * FROM `shop_offices_storages_map` WHERE `office_id` = ?;");
	$times_to_shop_query->execute( array($id) );
    while($time_to_shop_record = $times_to_shop_query->fetch() )
    {
        ?>
        //Если есть такой склад
        if(storages_id_to_index_map[<?php echo $time_to_shop_record["storage_id"]; ?>] != undefined)
        {
            storages_list[storages_id_to_index_map[<?php echo $time_to_shop_record["storage_id"]; ?>]].time_to_shop = <?php echo $time_to_shop_record["additional_time"]; ?>;
        }
        <?php
    }
    ?>
    
    
    onStorageSelected();//Обработка выделения склада
    </script>
    
    
    <?php
}//else//Действий нет - выводим страницу
?>