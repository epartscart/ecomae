<?php
//Страничный скрипт для создания прайс-листов для скачивания
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(747); ?>
		</div>
		<div class="panel-body">
			<select class="form-control" id="group_select">
			<?php
			$groups_query = $db_link->prepare("SELECT * FROM `groups` ORDER BY `id`;");
			$groups_query->execute();
			while( $group = $groups_query->fetch() )
			{
				?>
				<option value="<?php echo $group["id"]; ?>"><?php echo translate_str_by_id($group["value"])." (ID ".$group["id"].")"; ?></option>
				<?php
			}
			?>
			</select>
		</div>
	</div>
</div>





<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3248); ?>
		</div>
		<div class="panel-body">
			<select id="office_select" name="office_select" class="form-control">
			<?php
			$query = $db_link->prepare("SELECT * FROM `shop_offices`");
			$query->execute();
			while( $array = $query->fetch() )
			{
				?>
				<option value="<?=$array["id"];?>"><?=translate_str_by_id($array["caption"])." (ID ".$array["id"].")";?></option>
				<?php
			}//for($i)
			?>
			</select>
		</div>
	</div>
</div>



<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(763); ?>
		</div>
		<div class="panel-body" style="height: 436px; overflow-y: auto;">
			<div class="table-responsive">
				<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
					<thead> 
						<tr> 
							<th><input checked type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
							<th>ID</th>
							<th><?php echo translate_str_by_id(2277); ?></th>
							<th><?php echo translate_str_by_id(3474); ?></th>
						</tr>
					</thead>
					<tbody>
					<?php
					
					//Массивы для JS с id элементов и с чекбоксами элементов
					$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
					$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
					

					$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS *, (SELECT `name` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) AS `interface_type_name` FROM `shop_storages` WHERE `interface_type` = 1 OR `interface_type` = 2;");
					$elements_query->execute();
					
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					
					for($i=0; $i<$elements_count_rows; $i++)
					{
						$element_record = $elements_query->fetch();
						
						//Для Javascript
						$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
						$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
						?>
					
					
						<tr>
							<td><input checked type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"];?>');" id="checked_<?php echo $element_record["id"];?>" name="checked_<?php echo $element_record["id"];?>"/></td>
							<td><?php echo $element_record["id"]; ?></td>
							<td><?php echo $element_record["name"]; ?></td>
							<td><?php echo $element_record["interface_type_name"]; ?></td>
						</tr>
					<?php
					}//for
					?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
</div>








<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
?>




<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3726); ?>
		</div>
		<div class="panel-body">
			<div>
				<div style="padding:0 0 10px 0;">
				<button onclick="catalogue_tree.checkAll();" class="btn w-xs btn-success"><?php echo translate_str_by_id(2293); ?></button>
				<button onclick="catalogue_tree.uncheckAll();" class="btn w-xs btn-primary2"><?php echo translate_str_by_id(2294); ?></button>
				</div>
			</div>
			<div id="container_A" style="height:350px;"></div>
			
			<div style="padding:15px 0px;" class="hidden">
				<label for="">Склад: </label>
				<select id="storages" name="storages" class="form-control" style="display:inline-block; width: auto;">
				<?php
					$storages_query = $db_link->prepare("SELECT * FROM `shop_storages`");
					$storages_query->execute();
					while( $storages = $storages_query->fetch() )
					{
						if((int)$storages['interface_type'] === 1){
							$arr_users = json_decode($storages['users']);
							foreach($arr_users as $id_user){
								if((int)$id_user === (int)$admin_id){
									?>
									<option value="<?=$storages["id"];?>"><?=$storages["name"]." (ID ".$storages["id"].")";?></option>
									<?php
								}
							}
						}
					}
				?>
				</select> 
			</div>
		</div>
	</div>
</div>





<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			<div>
				<div style="padding:0 0 10px 0;">
					<button onclick="create_prices();" class="btn w-xs btn-success"><?php echo translate_str_by_id(3673); ?></button>
				</div>
				<div id="create_prices_status"></div>
			</div>
		</div>
	</div>
</div>




<script>
//Обработка нажатия кнопки "Сформировать прайс лист"
function create_prices()
{
	//Пользуемся тем же скриптом для формирования прайс-листов, что и для рассылки
	
	var request_object = new Object;
	
	
	//Получаем группу пользователей
	request_object.group_id_my_list_emails = document.getElementById("group_select").value;
	
	
	//Получаем выбранный магазин
	request_object.offices = document.getElementById("office_select").value;
	
	
	//Получаем выбранные склады
	request_object.arr_storages = getCheckedElements();
	if(request_object.arr_storages.length == 0)
	{
		alert("<?php echo translate_str_by_id(3727); ?>");
		return;
	}

	
	//Получаем выбранные категории товаров
	request_object.arr_category = catalogue_tree.getChecked();
	
	request_object.action = "create_prices";
	
	console.log(request_object);
	
	
	
	
	// -----------------------------------------------------------
	// Отправляем запрос на формирование прайсов. 
	// Отображаем индикатор загрузки
	document.getElementById('create_prices_status').innerHTML = '<div class="panel-body text-left"><img src="/content/files/images/ajax-loader-transparent.gif"/></div>';
	
	// Отправляем запрос
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",//Тип возвращаемого значения
		data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			console.log(answer);
			
			if(answer.status == true)
			{
			   document.getElementById('create_prices_status').innerHTML = '<?php echo translate_str_by_id(4649); ?>.<br><?php echo translate_str_by_id(4650); ?> <br><b><?php echo $DP_Config->domain_path; ?>content/files/Documents/prices_tmp/prices_'+request_object.group_id_my_list_emails+'.csv</b>';
			}
			else
			{
				alert("<?php echo translate_str_by_id(3682); ?>");
				document.getElementById('create_prices_status').innerHTML = '';
			}
		}
	});
}
</script>


















<script>
<?php
echo $for_js;//Выводим массив с чекбоксами для элементов
?>
//Обработка переключения Выделить все/Снять все
function on_check_uncheck_all()
{
	var state = document.getElementById("check_uncheck_all").checked;
	
	for(var i=0; i<elements_array.length;i++)
	{
		document.getElementById(elements_array[i]).checked = state;
	}
}//~function on_check_uncheck_all()



//Обработка переключения одного чекбокса
function on_one_check_changed(id)
{
	//Если хотя бы один чекбокс снят - снимаем общий чекбокс
	for(var i=0; i<elements_array.length;i++)
	{
		if(document.getElementById(elements_array[i]).checked == false)
		{
			document.getElementById("check_uncheck_all").checked = false;
			break;
		}
	}
}//~function on_one_check_changed(id)



//Получение массива id отмеченых элементов
function getCheckedElements()
{
	var checked_ids = new Array();
	//По массиву чекбоксов
	for(var i=0; i<elements_array.length;i++)
	{
		if(document.getElementById(elements_array[i]).checked == true)
		{
			checked_ids.push(elements_id_array[i]);
		}
	}
	
	return checked_ids;
}





/*ДЕРЕВО КАТАЛОГА ТОВАРОВ*/
//Для редактируемости дерева
webix.protoUI({
    name:"edittree"
}, webix.EditAbility, webix.ui.tree);
//Формирование дерева
catalogue_tree = new webix.ui({
    editable:false,//не редактируемое
    container:"container_A",//id блока div для дерева
    view:"tree",
	select:false,//можно выделять элементы
	drag:false,//можно переносить
	//Шаблон элемента дерева
	template:function(obj, common)//Шаблон узла дерева
    	{
            var folder = common.folder(obj, common);
    	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
    	    var checkbox = common.checkbox(obj, common);
            return common.icon(obj, common) + checkbox + folder + value_text;
    	},//~template
});
webix.event(window, "resize", function(){ catalogue_tree.adjust(); });

var saved_catalogue = <?php echo $catalogue_tree_dump_JSON;?>;
catalogue_tree.parse(saved_catalogue);
catalogue_tree.openAll();
/*~ДЕРЕВО*/
</script>