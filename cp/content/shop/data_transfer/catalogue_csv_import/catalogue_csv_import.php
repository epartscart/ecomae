<?php
/*
Страничный скрипт для импорта товаров в каталог из CSV
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();

require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getAdminId();
?>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>/shop/perenos-dannyx">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/xml.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(3161); ?></div>
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
			<?php echo translate_str_by_id(3162); ?>
		</div>
		<div class="panel-body" style="max-height: 642px; overflow: hidden; overflow-y: auto;">
			<div id="container_A" style="height:600px;">
			</div>
		</div>
	</div>
</div>
<div class="col-lg-6" id="content_info_div_col">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2799); ?>
		</div>
		<div class="panel-body" style="max-height: 642px; overflow: hidden; overflow-y: auto;">
			<div id="content_info_div">
			</div>
		</div>
	</div>
</div>
<script>
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
			if(obj.published_flag == false)
			{
				icon_system += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
				value_text = "<span style=\"color:#AAA\">" + obj.value + "</span>";//Вывод текста
			}
			
			return common.icon(obj, common) + icon + folder + icon_system + value_text;
		},//~template
	
	
	editable:false,//редактируемое
	container:"container_A",//id блока div для дерева
	view:"edittree",
	select:true,//можно выделять элементы
	drag:false,//можно переносить
});
webix.event(window, "resize", function(){ tree.adjust(); })
/*~ДЕРЕВО*/
//-----------------------------------------------------
//Событие при выборе элемента дерева
tree.attachEvent("onAfterSelect", function(id)
{
	onSelected();
});
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
	
	
	var node = "";//Ссылка на объект узла
	//Выделенный узел
	node = tree.getItem(node_id);
	
	if(node['$count'] > 0)
	{
		document.getElementById("content_info_div").innerHTML = "";
		
		//Скрыть контейнер для параметров
		document.getElementById("content_info_div_col").setAttribute("style", "display:none");
		return;
	}
	
	//Показать контейнер для параметров
	document.getElementById("content_info_div_col").setAttribute("style", "display:block");
	
	
	
	
	var parameters_table_html = "";

	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+node.id+"</div></div>";
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+node.value+"</div></div>";
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	
	parameters_table_html += "<div class=\"col-lg-12 text-center\"> <h4> <?php echo translate_str_by_id(3163); ?>: </h4> </div>";
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-5 control-label\"><?php echo translate_str_by_id(3164); ?></label><div class=\"col-lg-1\"><label for=\"\" class=\"col-lg-6 control-label\">URL <button class=\"btn btn-xs btn-info btn-circle\" type=\"button\" onclick=\"show_hint('<?php echo translate_str_by_id(3165); ?>');\"><i class=\"fa fa-info\"></i></button></label></div><div class=\"col-lg-6\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3166); ?></label></div></div>";
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	
	//Пропустить
	var strings_to_left = parseInt(getCookie(node_id+"_strings_to_left"));
	if( isNaN(strings_to_left)) strings_to_left = 0;
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3167); ?></label><div class=\"col-lg-6\"><input type=\"text\" onkeyup=\"dynamic_apply_cookie('strings_to_left');\" class=\"form-control\" id=\"strings_to_left\" value=\""+strings_to_left+"\" /></div></div>";
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	
	//СТАНДАРТНЫЕ ПОЛЯ
	var col_name_val = parseInt(getCookie(node_id+"_col_name"));
	if( isNaN(col_name_val)) col_name_val = 0;
	var col_name_url_check = 'checked';
	if(getCookie(node_id+"_col_name_url_check") == 'false'){
		col_name_url_check = '';
	}
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-5 control-label\"><?php echo translate_str_by_id(3168); ?> \"<?php echo translate_str_by_id(2102); ?>\"</label><div class=\"col-lg-1\"><input style=\"margin: 0;\" type=\"checkbox\" onchange=\"dynamic_apply_cookie('col_name_url_check', 1);\" class=\"form-control\" id=\"col_name_url_check\" "+col_name_url_check+" /></div><div class=\"col-lg-6\"><input type=\"text\" onkeyup=\"dynamic_apply_cookie('col_name');\" class=\"form-control\" id=\"col_name\" value=\""+col_name_val+"\" /></div></div>";
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	var col_text_val = parseInt(getCookie(node_id+"_col_text"));
	if( isNaN(col_text_val)) col_text_val = 0;
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3168); ?> \"<?php echo translate_str_by_id(2971); ?>\"</label><div class=\"col-lg-6\"><input type=\"text\" onkeyup=\"dynamic_apply_cookie('col_text');\" class=\"form-control\" id=\"col_text\" value=\""+col_text_val+"\" /></div></div>";
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	var col_img_val = parseInt(getCookie(node_id+"_col_img"));
	if( isNaN(col_img_val)) col_img_val = 0;
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3168); ?> \"<?php echo translate_str_by_id(3169); ?>\" <button class=\"btn btn-xs btn-info btn-circle\" type=\"button\" onclick=\"show_hint('<?php echo translate_str_by_id(3170); ?>');\"><i class=\"fa fa-info\"></i></button></label><div class=\"col-lg-6\"><input type=\"text\" onkeyup=\"dynamic_apply_cookie('col_img');\" class=\"form-control\" id=\"col_img\" value=\""+col_img_val+"\" /></div></div>";
	
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	var col_price_val = parseInt(getCookie(node_id+"_col_price"));
	if( isNaN(col_price_val)) col_price_val = 0;
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3168); ?> \"<?php echo translate_str_by_id(2751); ?>\"</label><div class=\"col-lg-6\"><input type=\"text\" onkeyup=\"dynamic_apply_cookie('col_price');\" class=\"form-control\" id=\"col_price\" value=\""+col_price_val+"\" /></div></div>";
	
	
	
	parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	var col_exist_val = parseInt(getCookie(node_id+"_col_exist"));
	if( isNaN(col_exist_val)) col_exist_val = 0;
	parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(3168); ?> \"<?php echo translate_str_by_id(1717); ?>\"</label><div class=\"col-lg-6\"><input type=\"text\" onkeyup=\"dynamic_apply_cookie('col_exist');\" class=\"form-control\" id=\"col_exist\" value=\""+col_exist_val+"\" /></div></div>";
	
	
	for( var i = 0 ; i < node.properties.length ; i++ )
	{
		parameters_table_html += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
		
		var col_val = parseInt(getCookie(node_id+"_col_"+node.properties[i].id));
		if( isNaN(col_val)) col_val = 0;
		
		var text_info = '';
		if(node.properties[i].property_type_id == 6){
			text_info = " <button class=\"btn btn-xs btn-info btn-circle\" type=\"button\" onclick=\"show_hint('<?php echo translate_str_by_id(3171); ?> :');\"><i class=\"fa fa-info\"></i></button>";
		}
		
		var col_property_url_check = '';
		if(getCookie(node_id+"_col_"+node.properties[i].id+"_url_check") == 'true'){
			col_property_url_check = 'checked';
		}
		
		parameters_table_html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-5 control-label\"><?php echo translate_str_by_id(3168); ?> \""+node.properties[i].value+"\""+text_info+"</label><div class=\"col-lg-1\"><input style=\"margin: 0;\" type=\"checkbox\" onchange=\"dynamic_apply_cookie('col_"+node.properties[i].id+"_url_check', 1);\" class=\"form-control\" id=\"col_"+node.properties[i].id+"_url_check\" "+col_property_url_check+" /></div><div class=\"col-lg-6\"><input type=\"text\"  onkeyup=\"dynamic_apply_cookie('col_"+node.properties[i].id+"');\" class=\"form-control\" id=\"col_"+node.properties[i].id+"\" value=\""+col_val+"\" /></div></div>";
	}
	
	
	document.getElementById("content_info_div").innerHTML = parameters_table_html;
}//function onSelected()
//-----------------------------------------------------
/*
Правило именования колонок:
1. ID для input:
col_<property_id/Стандартное имя>
2. Куки:
<category_id>_col_<property_id/Стандартное имя>
*/
//Функция динамической записи в куки - чтобы при очередной загрузке - значения инициализировались
function dynamic_apply_cookie(col_id, checkbox)
{
	//Определяем выбранный материал
	var node_id = tree.getSelectedId();//ID выделенного узла
	node = tree.getItem(node_id);//Выделенный узел
	
	if(checkbox == 1){
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = node_id + "_" + col_id + "="+document.getElementById(col_id).checked+"; path=/; expires=" + date.toUTCString();
	}else{
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = node_id + "_" + col_id + "="+document.getElementById(col_id).value+"; path=/; expires=" + date.toUTCString();
	}
}
//-----------------------------------------------------
// возвращает cookie с именем name, если есть, если нет, то undefined
function getCookie(name) 
{
	var matches = document.cookie.match(new RegExp(
		"(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
	));
	return matches ? decodeURIComponent(matches[1]) : undefined;
}
//-----------------------------------------------------
//Инициализация редактора дерева материалов после загруки страницы
function catalogue_start_init()
{
	var saved_catalogue = <?php echo $catalogue_tree_dump_JSON; ?>;
	tree.parse(saved_catalogue);
	tree.openAll();
}
catalogue_start_init();
onSelected();
//-----------------------------------------------------
</script>




<div class="col-lg-12">
</div>


<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3172); ?>
		</div>
		<div class="panel-body">
			<select id="storages" class="form-control">
				<option value="0">Не выбран</option>
				<?php
					$storages_query = $db_link->prepare("SELECT * FROM `shop_storages`");
					$storages_query->execute();
					while( $storages = $storages_query->fetch() )
					{
						if((int)$storages['interface_type'] === 1){
							$is_klad = false;
							$id_users = json_decode($storages['users']);
							foreach($id_users as $id_user){
								if((int)$id_user === (int)$user_id){
									$is_klad = true;
									break;
								}
							}
							if($is_klad){
								?>
								<option value="<?php echo $storages["id"]; ?>"><?php echo $storages["name"]." (ID ".$storages["id"].")"; ?></option>
								<?php
							}
						}
					}
				
				?>
			</select>
			<div class="hr-line-dashed col-lg-12"></div>
			
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3173); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3174); ?>');"><i class="fa fa-info"></i></button>
				</label>
				<div class="col-lg-6">
					<input type="checkbox" id="delete_storage_data" class="form-control" />
				</div>
			</div>
			
			<div class="hr-line-dashed col-lg-12"></div>
			
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3175); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3176); ?>');"><i class="fa fa-info"></i></button>
				</label>
				<div class="col-lg-6">
					<input type="checkbox" id="delete_products_data" class="form-control" />
				</div>
			</div>
			
		</div>
	</div>
</div>







<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2446); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3177); ?>');"><i class="fa fa-info"></i></button>
		</div>
		<div class="panel-body" style="min-height: 232px;">
			<input type="file" name="csv_file" id="csv_file" />
			<br>
			<label for="" class="control-label"><?php echo translate_str_by_id(3178); ?>:</label>
			<select id="encoding" name="encoding" class="form-control">
				<option value="windows-1251">Windows-1251 (ANSI)</option>
				<option value="UTF-8">UTF-8</option>
			</select>
			<br>
			<?php echo translate_str_by_id(3179); ?>
		</div>
	</div>
</div>






<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3180); ?>
		</div>
		<div class="panel-body">
			<div class="row">
				<div class="col-lg-12">
					<button id="start_button" onclick="start_import();" class="btn btn-success " type="button"><i class="fa fa-upload"></i> <span class="bold"><?php echo translate_str_by_id(3181); ?></span></button>
					<div id="process_indicator" style="display:inline-block;" class="hidden">
						<img src="/content/files/images/ajax-loader-transparent.gif" /> <?php echo translate_str_by_id(2939); ?> 
					</div>
				</div>
				
				<div id="process_result" class="col-lg-12"></div>
			</div>
		</div>
	</div>
</div>
<script>
// ----------------------------------------------------------------------------------------
//Показ индикатора загрузки
function processIndicator(start)
{
	if(start)//Запуск процесса
	{
		document.getElementById("process_indicator").setAttribute("class", "");//Индикатор процесса показать
		document.getElementById("start_button").disabled = true;//Кнопку загрузки заблокировать
		document.getElementById("process_result").innerHTML = "";//Сообщение с результатами загрузки - убрать
	}
	else//Стоп процесса
	{
		document.getElementById("process_indicator").setAttribute("class", "hidden");
		document.getElementById("start_button").disabled = false;
		document.getElementById("csv_file").value = "";//Сбросить выбор файла
	}
}
// ----------------------------------------------------------------------------------------
// Функция старта процесса
function start_import()
{
	//Выделенный узел
	var node_id = tree.getSelectedId();//ID выделенного узла
	if(node_id == 0)
	{
		alert("<?php echo translate_str_by_id(3182); ?>");
		return;
	}
	var node = tree.getItem(node_id);
	
	//Проверка, является ли категория конечной
	if(node['$count'] != 0)
	{
		alert("<?php echo translate_str_by_id(3183); ?>");
		return;
	}
	
	//Проверяем корректность заполнения настроек файла
	var name = document.getElementById("col_name").value;
	if( name == "" || name <= 0 || isNaN(name) )
	{
		alert('<?php echo translate_str_by_id(3184); ?>');
		return;
	}
	var text = document.getElementById("col_text").value;
	if( text == "" || text < 0 || isNaN(text) )
	{
		alert('<?php echo translate_str_by_id(3185); ?>');
		return;
	}
	var img = document.getElementById("col_img").value;
	if( img == "" || img < 0 || isNaN(img) )
	{
		alert('<?php echo translate_str_by_id(3186); ?>');
		return;
	}
	var price = document.getElementById("col_price").value;
	if( price == "" || price <= 0 || isNaN(price) )
	{
		alert('<?php echo translate_str_by_id(3187); ?>');
		return;
	}
	var exist = document.getElementById("col_exist").value;
	if( exist == "" || exist <= 0 || isNaN(exist) )
	{
		alert('<?php echo translate_str_by_id(3188); ?>');
		return;
	}
	
	//Проверка корректности заполнения номеров колонок со свойствами
	for( var i = 0 ; i < node.properties.length ; i++ )
	{
		var property_value = document.getElementById("col_"+node.properties[i].id).value;
		
		if( property_value == "" || property_value < 0 || isNaN(property_value) )
		{
			alert('<?php echo translate_str_by_id(3189); ?> "'+node.properties[i].value+'" <?php echo translate_str_by_id(3190); ?>');
			return;
		}	
	}
	
	
	
	//СКЛАДЫ
	var storage_id = document.getElementById("storages").value;
	if(storage_id == 0)
	{
		alert("<?php echo translate_str_by_id(3191); ?>");
		return;
	}
	
	
	//Проверка наличия файла
	var csv_file = document.getElementById("csv_file").value;
	if( csv_file == "" )
	{
		alert("<?php echo translate_str_by_id(3192); ?>");
		return;
	}
	
	//ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ
	processIndicator(true);
	
	//ОТПРАВЛЯЕМ ФАЙЛ НА СЕРВЕР
	var csv_file = $("#csv_file");//Input с файлом
    var formData = new FormData;//Объект данных формы
    formData.append('csv_file', csv_file.prop('files')[0]);//Добавляем в объект формы - файл
    formData.append('csrf_guard_key', '<?php echo $user_session["csrf_guard_key"]; ?>');
    $.ajax({
        url: '/<?php echo $DP_Config->backend_dir; ?>/content/shop/data_transfer/catalogue_csv_import/ajax_upload_file_to_tmp.php',
        data: formData,
        processData: false,
        contentType: false,
        type: 'POST',
		dataType: "json",//Тип возвращаемого значения
        success: function (result) 
		{
			console.log(result);
            if( result.status != true )
			{
				processIndicator(false);
				alert("<?php echo translate_str_by_id(3126); ?>");
			}
			else//Файл загружен, запускаем импорт
			{
				var node_id = tree.getSelectedId();
				var node = tree.getItem(node_id);
				
				//Создаем объект с параметрами импорта
				var import_options = new Object;
				
				import_options.file_full_path = result.file_full_path;//Полный путь к файлу
				
				import_options.category_id = node_id;//ID категории товаров
				import_options.storage_id = document.getElementById("storages").value;//ID склада
				
				//Удалять ли складску информацию
				if(document.getElementById("delete_storage_data").checked == true)
				{
					import_options.delete_storage_data = 1;
				}
				else
				{
					import_options.delete_storage_data = 0;
				}
				
				//Удалять ли товары категории
				if(document.getElementById("delete_products_data").checked == true)
				{
					import_options.delete_products_data = 1;
				}
				else
				{
					import_options.delete_products_data = 0;
				}
				
				
				//Обязательные колонки
				import_options["strings_to_left"] = document.getElementById("strings_to_left").value;
				import_options["col_name"] = document.getElementById("col_name").value;
				import_options["col_name_url_check"] = document.getElementById("col_name_url_check").checked;
				import_options["col_text"] = document.getElementById("col_text").value;
				import_options["col_img"] = document.getElementById("col_img").value;
				import_options["col_price"] = document.getElementById("col_price").value;
				import_options["col_exist"] = document.getElementById("col_exist").value;
				
				//Свойства
				for( var i = 0 ; i < node.properties.length ; i++ )
				{
					import_options["col_"+node.properties[i].id] = document.getElementById("col_"+node.properties[i].id).value;
					import_options["col_"+node.properties[i].id+"_url_check"] = document.getElementById("col_"+node.properties[i].id+"_url_check").checked;
				}
				
				//Кодировка
				import_options.encoding = document.getElementById("encoding").value;//ID encoding
				
				console.log(import_options);
				
				//ПЕРЕДАЕМ ЗАПРОС НА СЕРВЕР ДЛЯ ЗАПУСКА ПРОЦЕССА
				jQuery.ajax({
					type: "POST",
					async: true,
					url: '/<?php echo $DP_Config->backend_dir; ?>/content/shop/data_transfer/catalogue_csv_import/ajax_handle_file.php',
					dataType: "json",//Тип возвращаемого значения
					data: "import_options="+encodeURI( JSON.stringify(import_options) )+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
					success: function(result)
					{
						console.log(result);
						
						if(result.status != true)
						{
							processIndicator(false);
							alert("<?php echo translate_str_by_id(2122); ?>. "+result.message);
						}
						else
						{
							processIndicator(false);
							var resule_html = "<p><?php echo translate_str_by_id(3193); ?></p>"
							
							if( result.warnings.length > 0 )
							{
								resule_html = resule_html + "<h4><?php echo translate_str_by_id(3194); ?>:</h4>";
								
								for(var i=0 ; i < result.warnings.length ; i++)
								{
									resule_html = resule_html + "<p>"+result.warnings[i]+"</p>";
								}
							}
							document.getElementById("process_result").innerHTML = resule_html;
						}
					},
					error: function (e, ajaxOptions, thrownError){
						processIndicator(false);
						var resule_html = "<p><?php echo translate_str_by_id(3195); ?></p>"
						document.getElementById("process_result").innerHTML = resule_html;
					}
				});
			}
        }
    });
}
// ----------------------------------------------------------------------------------------
</script>