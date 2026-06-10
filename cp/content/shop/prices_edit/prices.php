<?php
/**
 * Скрипт редактирования позиций прайс листов
 */
defined('_ASTEXE_') or die('No access');

$prices_list = array();
$price_profiles = array();
$default_profile_id = 0;
$user_session = array();
$epc_prices_edit_error = '';

	try {
	if (!isset($db_link) || !($db_link instanceof PDO)) {
		throw new Exception('Database connection is not available.');
	}
	$epc_prices_edit_helpers = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir
		. '/content/shop/prices_edit/epc_prices_edit_helpers.php';
	if (!is_file($epc_prices_edit_helpers)) {
		throw new Exception('Helper file not found: ' . $epc_prices_edit_helpers);
	}
	require_once $epc_prices_edit_helpers;
	$prices_list = epc_prices_edit_load_price_names($db_link);
	$price_profiles = epc_prices_edit_load_profiles($db_link);
	if (!empty($price_profiles)) {
		$default_profile_id = (int)$price_profiles[0]['id'];
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
	if (empty($user_session) || !is_array($user_session)) {
		$epc_prices_edit_error = 'Please log in to the control panel to use the price list editor.';
	}
} catch (Exception $e) {
	$epc_prices_edit_error = $e->getMessage();
} catch (Throwable $e) {
	$epc_prices_edit_error = $e->getMessage();
}

if ($epc_prices_edit_error !== '') {
	echo '<div class="alert alert-danger"><strong>Price list editor could not load:</strong> '
		. htmlspecialchars($epc_prices_edit_error, ENT_QUOTES, 'UTF-8') . '</div>';
	return;
}

$epc_prices_edit_csrf = '';
if (!empty($user_session['csrf_guard_key'])) {
	$epc_prices_edit_csrf = (string)$user_session['csrf_guard_key'];
}
?>





<div class="row row-add" style="margin: 0;">
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Edit price list records
				<span class="pull-right">
					<a class="btn btn-default btn-xs" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices"><i class="fa fa-arrow-left"></i> Back to price lists</a>
				</span>
			</div>
			<div class="panel-body">
				<p class="text-muted" style="margin:0;">Search and edit rows in <code>shop_docpart_prices_data</code>. Choose a profile below to preview site price and margin.</p>
			</div>
		</div>
	</div>
</div>

<div class="row row-add" style="margin: 0;">
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Fuzzy search
			</div>
			<div class="panel-body">
				<div class="col-lg-6">
					<label>Search:</label>
					<input class="form-control" type="text" id="search_text"/>
				</div>
			</div>
			<div class="panel-heading hbuilt" style="border-top: 0;">
				Exact match filter
			</div>
			<div class="panel-body">
				<div class="col-lg-3">
					<label>Price list:</label>
					<select id="search_price_id" class="form-control">
					<option value="0">All</option>
					<?php
					foreach($prices_list as $price_id => $price_name){
						?>
						<option value="<?php echo (int)$price_id; ?>"><?php echo 'id ' . (int)$price_id . ' - ' . htmlspecialchars($price_name, ENT_QUOTES, 'UTF-8'); ?></option>
						<?php
					}
					?>
					</select>
				</div>
				<div class="col-lg-3">
					<label>Article:</label>
					<div style="position:relative;">
						<input class="form-control" type="text" id="search_article"/>
						<input title="Find rows with empty article" class="form-control" type="checkbox" id="search_no_article" style="position: absolute; bottom: 0; right: 3px; width: 26px; cursor:pointer;"/>
					</div>
				</div>
				<div class="col-lg-3">
					<label>Manufacturer:</label>
					<div style="position:relative;">
						<input class="form-control" type="text" id="search_manufacturer"/>
						<input title="Find rows with empty manufacturer" class="form-control" type="checkbox" id="search_no_manufacturer" style="position: absolute; bottom: 0; right: 3px; width: 26px; cursor:pointer;"/>
					</div>
				</div>
			</div>
			<div class="panel-heading hbuilt" style="border-top: 0;">
				Site preview (profile margin)
			</div>
			<div class="panel-body">
				<div class="col-lg-4">
					<label>Customer profile (as on storefront):</label>
					<select id="preview_group_id" class="form-control">
					<option value="0" selected>— Base list price only —</option>
					<?php
					foreach ($price_profiles as $prof) {
						$gid = (int)$prof['id'];
						$label = trim($prof['value'] . ' — ' . $prof['code']);
						?>
						<option value="<?php echo $gid; ?>"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></option>
						<?php
					}
					?>
					</select>
				</div>
				<div class="col-lg-2">
					<label>Min. margin %:</label>
					<input class="form-control" type="number" id="min_margin" min="0" step="0.1" placeholder="Any"/>
				</div>
				<div class="col-lg-3" style="padding-top: 24px;">
					<label class="checkbox-inline" style="font-weight: normal;">
						<input type="checkbox" id="hide_hidden"/> Hide brands hidden for this profile
					</label>
				</div>
				<div class="col-lg-3" style="padding-top: 24px;">
					<small class="text-muted">Site price uses warehouse markup + brand rules (same as shop search).</small>
				</div>
			</div>
			<div class="panel-footer text-right">
				<a onclick="clear_search();" class="btn btn-ar btn-default"><i class="fa fa-eraser"></i> Reset</a>
				<a onclick="search_items();" class="btn btn-ar btn-primary"><i class="fa fa-search"></i> Search</a>
			</div>
		</div>
	</div>
</div>





<div class="row row-add" style="margin: 0;">
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				Add row manually
			</div>
			<div class="panel-body">
				<div class="col-lg-3">
					<label>Price list:</label>
					<select id="new_price_id" class="form-control">
					<?php
					foreach($prices_list as $price_id => $price_name){
						?>
						<option value="<?php echo (int)$price_id; ?>"><?php echo 'id ' . (int)$price_id . ' - ' . htmlspecialchars($price_name, ENT_QUOTES, 'UTF-8'); ?></option>
						<?php
					}
					?>
					</select>
				</div>
				<div class="col-lg-9"><label>Name:</label><input class="form-control" type="text" id="new_name"/></div>
				
				<div class="col-lg-3"><label>Article:</label><input class="form-control" type="text" id="new_article"/></div>
				<div class="col-lg-3"><label>Manufacturer:</label><input class="form-control" type="text" id="new_manufacturer"/></div>
				<div class="col-lg-3 hidden"><label>Article (raw):</label><input class="form-control" type="text" id="new_article_show"/></div>
				
				<div class="col-lg-3"><label>Qty:</label><input class="form-control" type="number" id="new_exist" value="1"/></div>
				<div class="col-lg-3"><label>Price:</label><input class="form-control" type="number" id="new_price"/></div>
				<div class="col-lg-3"><label>Lead time:</label><input class="form-control" type="number" id="new_time_to_exe" value="0"/></div>
				<div class="col-lg-3 hidden"><label>Internal storage:</label><input class="form-control" type="text" id="new_storage"/></div>
				<div class="col-lg-3"><label>Min. order:</label><input class="form-control" type="number" id="new_min_order" value="1"/></div>
			</div>
			<div class="panel-footer text-right">
				<img id="img_add" style="height: 31px; margin-right: 5px;" class="hidden" src="/content/files/images/ajax-loader-transparent.gif"/><a id="btn_add" onclick="add();" class="btn btn-ar btn-primary"><i class="fa fa-plus"></i> Add</a>
			</div>
		</div>
	</div>
</div>





<div class="row" style="margin: 0;">
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt" style="position:relative;">
				Items
			</div>
			<div id="div_table"></div>
		</div>
	</div>
</div>





<div class="row row-add" style="margin: 0;">
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				Delete rows matching current filter and search
			</div>
			<div class="panel-body">
				<div class="col-lg-12">
					<a onclick="del_search();" class="btn btn-ar btn-danger">Delete matching rows</a>
				</div>
			</div>
		</div>
	</div>
</div>





<script>
	var page = 1;// Текущая страница таблицы
	var where_object = new Object;// Объект фильтра
	
	
	
	// Функция перехода по страницам таблицы
	function go_to_page(p){
		page = p;
		show_table();
	}
	
	
	
	// Функция приминения фильтров
	function search_items(){
		where_object = new Object;
		page = 1;
		
		where_object.search_text = encodeURIComponent(document.getElementById("search_text").value);
		
		var n = document.getElementById("search_price_id").options.selectedIndex;
		where_object.price_id = encodeURIComponent(document.getElementById("search_price_id").options[n].value);
		
		where_object.article = encodeURIComponent(document.getElementById("search_article").value);
		where_object.manufacturer = encodeURIComponent(document.getElementById("search_manufacturer").value);
		
		if(document.getElementById("search_no_article").checked){where_object.no_article = 1;}
		if(document.getElementById("search_no_manufacturer").checked){where_object.no_manufacturer = 1;}

		var pg = document.getElementById("preview_group_id");
		if(pg){where_object.preview_group_id = encodeURIComponent(pg.options[pg.selectedIndex].value);}
		var mm = document.getElementById("min_margin");
		if(mm && mm.value !== ''){where_object.min_margin = encodeURIComponent(mm.value);}
		if(document.getElementById("hide_hidden") && document.getElementById("hide_hidden").checked){where_object.hide_hidden = 1;}
		
		show_table();
	}
	
	
	
	// Функция отображает таблицу с условиями фильтрации
	function show_table(){
		document.getElementById('div_table').innerHTML = '';
		
		setTimeout(function(){ 
			if(document.getElementById('div_table').innerHTML == ''){
				// Отображаем индикатор загрузки
				document.getElementById('div_table').innerHTML = '<div class="panel-body text-center"><img src="/content/files/images/ajax-loader-transparent.gif"/></div>';
			}
		}, 500)

		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'get_table';
		request_object.page = page;
		request_object.where_object = where_object;

		// Отправляем запрос
		jQuery.ajax({
            type: "POST",
            async: true,
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_edit/ajax_operations.php",
            dataType: "text",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo htmlspecialchars($epc_prices_edit_csrf, ENT_QUOTES, 'UTF-8'); ?>",
            success: function(answer)
            {
				// Вставляем сформированный html на страницу
				document.getElementById('div_table').innerHTML = answer;
		    }
        });
	}
	
	
	
	// Функция ручного добавления
	function add(){
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'add';
		var n = document.getElementById("new_price_id").options.selectedIndex;
		request_object.price_id = encodeURIComponent(document.getElementById("new_price_id").options[n].value);
		request_object.name = encodeURIComponent(document.getElementById("new_name").value);
		request_object.article = encodeURIComponent(document.getElementById("new_article").value);
		request_object.manufacturer = encodeURIComponent(document.getElementById("new_manufacturer").value);
		request_object.article_show = encodeURIComponent(document.getElementById("new_article_show").value);
		request_object.exist = encodeURIComponent(document.getElementById("new_exist").value);
		request_object.price = encodeURIComponent(document.getElementById("new_price").value);
		request_object.time_to_exe = encodeURIComponent(document.getElementById("new_time_to_exe").value);
		request_object.storage = encodeURIComponent(document.getElementById("new_storage").value);
		request_object.min_order = encodeURIComponent(document.getElementById("new_min_order").value);
		
		if(request_object.article === '' || request_object.manufacturer === '' || (request_object.exist*1) <= 0  || (request_object.price*1) <= 0){
			alert("Fill in all required fields");
			return;
		}
		
		// Очищаем форму
		document.getElementById("new_price_id").options[0].selected = true;
		document.getElementById("new_name").value = '';
		document.getElementById("new_article").value = '';
		document.getElementById("new_manufacturer").value = '';
		document.getElementById("new_article_show").value = '';
		document.getElementById("new_exist").value = '1';
		document.getElementById("new_price").value = '';
		document.getElementById("new_time_to_exe").value = '0';
		document.getElementById("new_storage").value = '';
		document.getElementById("new_min_order").value = '1';
		
		$('#btn_add').addClass('disabled');// Блокируем кнопку
		$('#img_add').removeClass('hidden');// Отображаем индикатор загрузки
		
        jQuery.ajax({
            type: "POST",
            async: true, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_edit/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo htmlspecialchars($epc_prices_edit_csrf, ENT_QUOTES, 'UTF-8'); ?>",
            success: function(answer)
            {
                $('#btn_add').removeClass('disabled');// Разблокируем кнопку
				$('#img_add').addClass('hidden');// Убираем индикатор загрузки
				
				//console.log(answer);
                if(answer.status == true)
                {
                   show_table();
                }
                else
                {
					alert("Could not add row.");
                }
            }
        });
	}
	
	
	
	// Функция отображает форму редактирования
	function edit(id){
        $('#show_line_'+id).addClass('hidden');
        $('#edit_line_'+id).removeClass('hidden');
	}

	
	
	// Функция отменяет редактирование
	function edit_otmena(id){
        $('#edit_line_'+id).addClass('hidden');
		$('#show_line_'+id).removeClass('hidden');
	}

	
	
	// Функция сохранения редактируемой позиции
	function edit_save(id){
		var price_id = document.getElementById('price_id_edit_'+id).value;
		var article = document.getElementById('article_edit_'+id).value;
		var manufacturer = document.getElementById('manufacturer_edit_'+id).value;
		var article_show = document.getElementById('article_show_edit_'+id).value;
		var name = document.getElementById('name_edit_'+id).value;
		var exist = document.getElementById('exist_edit_'+id).value;
		var price = document.getElementById('price_edit_'+id).value;
		var time_to_exe = document.getElementById('time_to_exe_edit_'+id).value;
		var storage = document.getElementById('storage_edit_'+id).value;
		var min_order = document.getElementById('min_order_edit_'+id).value;
		
		if(price_id === '' || article === '' || manufacturer === '' || exist === '' || price === ''){
			alert("Fill in all required fields");
			return;
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'save';
		request_object.id = id;
		request_object.price_id = encodeURIComponent(price_id);
		request_object.article = encodeURIComponent(article);
		request_object.manufacturer = encodeURIComponent(manufacturer);
		request_object.article_show = encodeURIComponent(article_show);
		request_object.name = encodeURIComponent(name);
		request_object.exist = encodeURIComponent(exist);
		request_object.price = encodeURIComponent(price);
		request_object.time_to_exe = encodeURIComponent(time_to_exe);
		request_object.storage = encodeURIComponent(storage);
		request_object.min_order = encodeURIComponent(min_order);
    
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_edit/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo htmlspecialchars($epc_prices_edit_csrf, ENT_QUOTES, 'UTF-8'); ?>",
            success: function(answer)
            {
                if(answer.status == true)
                {
                    show_table();
                }
                else
                {
					alert("Could not save.");
                }
            }
        });
	}
	
	
	
	// Функция удаления
	function del(id){
        if(confirm('Delete this row?')){
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'del';
			request_object.id = id;
		
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_edit/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo htmlspecialchars($epc_prices_edit_csrf, ENT_QUOTES, 'UTF-8'); ?>",
				success: function(answer)
				{
					if(answer.status == true)
					{
						show_table();
					}
					else
					{
						alert("Could not delete");
					}
				}
			});
		}
	}
	
	
	
	// Функция удаления с учетом поиска
	function del_search(){
        if(confirm('Delete all rows matching the current filter?')){
			
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'del_search';
			request_object.page = page;
			request_object.where_object = where_object;
			
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_edit/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo htmlspecialchars($epc_prices_edit_csrf, ENT_QUOTES, 'UTF-8'); ?>",
				success: function(answer)
				{
					if(answer.status == true)
					{
						show_table();
					}
					else
					{
						alert("Could not delete");
					}
				}
			});
		}
	}
	
	
	
	// Функция сбрасывает фильтры поиска
	function clear_search(){
		where_object = new Object;
		page = 1;
		document.getElementById('search_text').value = '';
		document.getElementById("search_price_id").options[0].selected = true;
		document.getElementById('search_article').value = '';
		document.getElementById('search_manufacturer').value = '';
		document.getElementById('search_no_article').checked = false;
		document.getElementById('search_no_manufacturer').checked = false;
		var pg = document.getElementById("preview_group_id");
		if(pg && pg.options.length){pg.options[0].selected = true;}
		var mm = document.getElementById("min_margin");
		if(mm){mm.value = '';}
		var hh = document.getElementById("hide_hidden");
		if(hh){hh.checked = false;}
		show_table();
	}

	
	
	// First load after CP shell is ready (homer.js hides "Loading framework" splash)
	function epcPricesEditInitTable() {
		where_object = new Object();
		where_object.preview_group_id = encodeURIComponent('0');
		show_table();
	}
	if (typeof jQuery !== 'undefined') {
		jQuery(epcPricesEditInitTable);
	} else {
		document.addEventListener('DOMContentLoaded', epcPricesEditInitTable);
	}
</script>







