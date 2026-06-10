<?php
/**
 * Скрипт для управления синонимами производителей
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>
	
<style>
	.my_table{
		width:100%;
		margin-bottom:20px;
	}
	.my_table td:nth-child(2){
		width: 120px;
		text-align: right;
	}
	.my_table_2{
		width:100%;
	}
	.my_table_2 td:nth-child(2){
		width: 195px;
		text-align: right;
	}
	.manufacturer, .synonym{
		border:1px solid #fff;
		position: relative;
	}
	.manufacturer_name, .synonym_name{
		padding:10px;
		cursor:pointer;
	}
	.manufacturer:hover, .manufacturer_active, .synonym:hover, .synonym_active{
		background:#eee;
		border:1px solid #ddd;
	}
	.manufacturer_edit, .synonym_edit{
		display:none;
	}
	.btn_block{
		position: absolute;
		display:inline-block;
		right: 10px;
		top: 4px;
	}
</style>
	
<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">

			<?php echo translate_str_by_id(3476); ?>
			<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(5291); ?>');"><i class="fa fa-info"></i></button>

		</div>
		<div class="panel-body">
			
			<div>
				<table class="my_table">
				<tr>
					<td>
						<input class="form-control" type="text" id="new_manufacturer" />
					</td>
					<td>
						<a onclick="manufacturer_add();" class="btn btn-ar btn-primary"><i class="fa fa-plus"></i> <?php echo translate_str_by_id(2267); ?></a>
					</td>
				</tr>
				</table>
			</div>
			
			<div id="manufacturers_div" style="max-height: 600px; overflow-y: auto; border-top: 1px solid #34495e; padding-top: 10px;"></div>
			
		</div>
	</div>
</div>

<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3477); ?>
		</div>
		<div class="panel-body" id="synonyms_div">
			<p><?php echo translate_str_by_id(5292); ?></p>
		</div>
	</div>
</div>
	
	
	
	
<script>
	
	
	
	// Функция отображает форму редактирования производителя
	function synonym_edit(id){
        document.getElementById('synonym_'+id).style.display = 'none';
		document.getElementById('synonym_edit_block_'+id).style.display = 'block';
	}
	
	
	
	// Функция отменяет редактирование производителя
	function synonym_edit_otmena(id){
        document.getElementById('synonym_edit_'+id).value = document.getElementById('synonym_name_'+id).innerHTML.trim();
        document.getElementById('synonym_edit_block_'+id).style.display = 'none';
        document.getElementById('synonym_'+id).style.display = 'block';
	}
	
	
	
	// Функция сохранения редактируемого производителя
	function synonym_edit_save(id){
		var name = document.getElementById('synonym_edit_'+id).value;
		
		if(name === ''){
			alert("<?php echo translate_str_by_id(3478); ?>");
			return;
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'save_synonym';
		request_object.id = id;
		request_object.name = encodeURIComponent(name);
    
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                //console.log(answer);
                if(answer.status == true)
                {
                    document.getElementById('synonym_name_'+id).innerHTML = name;
                    document.getElementById('synonym_edit_block_'+id).style.display = 'none';
                    document.getElementById('synonym_'+id).style.display = 'block';
                }
                else
                {
                    document.getElementById('synonym_edit_'+id).value = document.getElementById('synonym_name_'+id).innerHTML.trim();
                    document.getElementById('synonym_edit_block_'+id).style.display = 'none';
                    document.getElementById('synonym_'+id).style.display = 'block';
					alert("<?php echo translate_str_by_id(2576); ?>");
                }
            }
        });
	}
	
	
	
	
	// Функция добавления нового синонима
	function synonym_add(id)
	{
		var name = document.getElementById('new_synonym').value;
		
		if(name === ''){
			alert("<?php echo translate_str_by_id(3478); ?>");
			return;
		}
		
		//Объект для запроса
        var request_object = new Object;
			request_object.action = 'add_synonym';
			request_object.id = id;
			request_object.name = encodeURIComponent(name);
    
		var request_objec_convert = encodeURIComponent( JSON.stringify( request_object ) )
	

	
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+request_objec_convert+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                // console.log(answer);
                if(answer.status == true)
                {
                    show_synonyms(id);
                }
                else
                {
					alert("<?php echo translate_str_by_id(3356); ?>");
                }
            }
        });
	}
	
	
	
	// Функция удаления синонима
	function synonym_del(id, manufacturer){
        if(confirm('<?php echo translate_str_by_id(3479); ?>')){
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'del_synonym';
			request_object.id = id;
		
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//console.log(answer);
					if(answer.status == true)
					{
						show_synonyms(manufacturer);
					}
					else
					{
						alert("<?php echo translate_str_by_id(3356); ?>");
					}
				}
			});
		}
	}
	
	
// **************************************************************************************
	
	
	// Функция добавления нового производителя
	function manufacturer_add(){
		var name = document.getElementById('new_manufacturer').value;
		
		if(name === ''){
			alert("<?php echo translate_str_by_id(3478); ?>");
			return;
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'add_manufacturer';
		request_object.name = encodeURIComponent(name);
    
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                //console.log(answer);
                if(answer.status == true)
                {
                    document.getElementById('new_manufacturer').value = '';
					show_manufacturers();
                }
                else
                {
					alert("<?php echo translate_str_by_id(3356); ?>");
                }
            }
        });
	}
	
	
	
	// Функция удаления производителя
	function manufacturer_del(id){
        if(confirm('<?php echo translate_str_by_id(3480); ?>')){
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'del_manufacturer';
			request_object.id = id;
		
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURIComponent(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//console.log(answer);
					if(answer.status == true)
					{
						document.getElementById('synonyms_div').innerHTML = '';
						show_manufacturers();
					}
					else
					{
						alert("<?php echo translate_str_by_id(2610); ?>");
					}
				}
			});
		}
	}
	
	
	
	// Функция отображает форму редактирования производителя
	function manufacturer_edit(id){
        document.getElementById('manufacturer_'+id).style.display = 'none';
		document.getElementById('manufacturer_edit_block_'+id).style.display = 'block';
	}
	
	
	
	// Функция отменяет редактирование производителя
	function manufacturer_edit_otmena(id){
        document.getElementById('manufacturer_edit_'+id).value = document.getElementById('manufacturer_name_'+id).innerHTML.trim();
        document.getElementById('manufacturer_edit_block_'+id).style.display = 'none';
        document.getElementById('manufacturer_'+id).style.display = 'block';
	}
	
	
	
	// Функция сохранения редактируемого производителя
	function manufacturer_edit_save(id)
	{
		var name = document.getElementById('manufacturer_edit_'+id).value;
		
		if(name === ''){
			alert("<?php echo translate_str_by_id(3478); ?>");
			return;
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'save_manufacturer';
		request_object.id = id;
		request_object.name = encodeURIComponent(name);
    
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                //console.log(answer);
                if(answer.status == true)
                {
                    document.getElementById('manufacturer_name_'+id).innerHTML = name;
                    document.getElementById('manufacturer_edit_block_'+id).style.display = 'none';
                    document.getElementById('manufacturer_'+id).style.display = 'block';
                }
                else
                {
                    document.getElementById('manufacturer_edit_'+id).value = document.getElementById('manufacturer_name_'+id).innerHTML.trim();
                    document.getElementById('manufacturer_edit_block_'+id).style.display = 'none';
                    document.getElementById('manufacturer_'+id).style.display = 'block';
					alert("<?php echo translate_str_by_id(2576); ?>");
                }
            }
        });
	}
	
	
	
	// Отображаем производителей
	function show_manufacturers()
	{
		
		var html = '';
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'get_manufacturers';
		
		jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
				var manufacturers = answer['manufacturers'];
				for(var i=0; i < manufacturers.length; i++){
				
				html += '<div class="manufacturer" id="manufacturer_line_'+ manufacturers[i]['id'] +'">';
				html += '	<div id="manufacturer_'+ manufacturers[i]['id'] +'">';
				html += '		<div id="manufacturer_name_'+ manufacturers[i]['id'] +'" onclick="show_synonyms('+ manufacturers[i]['id'] +');" class="manufacturer_name">';
				html += '			'+ manufacturers[i]['name'] +'';
				html += '		</div>';
				html += '		<div class="btn_block">';
				html += '			<a onclick="manufacturer_edit('+ manufacturers[i]['id'] +');" class="btn btn-sm btn-primary" title="<?php echo translate_str_by_id(2270); ?>"><i class="fas fa-pencil-alt"></i></a>';
				html += '			<a onclick="manufacturer_del('+ manufacturers[i]['id'] +');" class="btn btn-sm btn-primary" title="<?php echo translate_str_by_id(2224); ?>"><i class="fa fa-times"></i></a>';
				html += '		</div>';
				html += '	</div>';
				html += '	<div class="manufacturer_edit" id="manufacturer_edit_block_'+ manufacturers[i]['id'] +'">';
				html += '		<table class="my_table_2">';
				html += '			<tr>';
				html += '				<td>';
				html += '					<input class="form-control" type="text" id="manufacturer_edit_'+ manufacturers[i]['id'] +'" value="'+ manufacturers[i]['name'] +'"/>';
				html += '				</td>';
				html += '				<td>';
				html += '					<a onclick="manufacturer_edit_save('+ manufacturers[i]['id'] +');" class="btn btn-sm btn-primary"><i class="far fa-save"></i> <?php echo translate_str_by_id(2114); ?></a>';
				html += '					<a onclick="manufacturer_edit_otmena('+ manufacturers[i]['id'] +');" class="btn btn-sm btn-primary"><i class="fa fa-chevron-left"></i> <?php echo translate_str_by_id(2190); ?></a>';
				html += '				</td>';
				html += '			</tr>';
				html += '		</table>';
				html += '	</div>';
				html += '</div>';
				
				}
		    }
        });
		
		document.getElementById('manufacturers_div').innerHTML = html;
	}
	
	
	
	// Запрос синонимов выбранного производителя
	function show_synonyms(id)
	{
		// Задаем фон выбранному производителю
		var manufacturer_line = document.getElementById('manufacturer_line_'+id);
		$(".manufacturer_active").removeClass("manufacturer_active");
		if(manufacturer_line.classList.contains("manufacturer_active") === false){
			manufacturer_line.classList.add("manufacturer_active");
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'get_synonyms';
		request_object.id = id;
		
		jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/manufacturers_synonyms/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                var synonyms = answer['synonyms'];
				//console.log(synonyms);
                var html = '';
				html += '<div>';
				html += '	<table class="my_table">';
				html += '	<tr>';
				html += '		<td>';
				html += '			<input class="form-control" type="text" id="new_synonym" />';
				html += '		</td>';
				html += '		<td>';
				html += '			<a onclick="synonym_add('+ id +');" class="btn btn-ar btn-primary"><i class="fa fa-plus" aria-hidden="true"></i> <?php echo translate_str_by_id(2267); ?></a>';
				html += '		</td>';
				html += '	</tr>';
				html += '	</table>';
				html += '</div>';
				
				html += '<div style="max-height: 600px; overflow-y: auto; border-top: 1px solid #34495e; padding-top: 10px;">';
				
					for(var i = 0; i < synonyms.length; i++){
						html += '<div class="synonym">';
						html += '	<div id="synonym_'+ synonyms[i]['id'] +'">';
						html += '		<div id="synonym_name_'+ synonyms[i]['id'] +'" class="synonym_name">';
						html += '			'+ synonyms[i]['synonym'] +'';
						html += '		</div>';
						html += '		<div class="btn_block">';
						html += '			<a onclick="synonym_edit('+ synonyms[i]['id'] +');" class="btn btn-sm btn-primary" title="<?php echo translate_str_by_id(2270); ?>"><i class="fas fa-pencil-alt"></i></a>';
						html += '			<a onclick="synonym_del('+ synonyms[i]['id'] +', '+ id +');" class="btn btn-sm btn-primary" title="<?php echo translate_str_by_id(2224); ?>"><i class="fa fa-times"></i></a>';
						html += '		</div>';
						html += '	</div>';
						html += '	<div class="synonym_edit" id="synonym_edit_block_'+ synonyms[i]['id'] +'">';
						html += '		<table class="my_table_2">';
						html += '			<tr>';
						html += '				<td>';
						html += '					<input class="form-control" type="text" id="synonym_edit_'+ synonyms[i]['id'] +'" value="'+ synonyms[i]['synonym'] +'"/>';
						html += '				</td>';
						html += '				<td>';
						html += '					<a onclick="synonym_edit_save('+ synonyms[i]['id'] +');" class="btn btn-sm btn-primary"><i class="far fa-save"></i> <?php echo translate_str_by_id(2114); ?></a>';
						html += '					<a onclick="synonym_edit_otmena('+ synonyms[i]['id'] +');" class="btn btn-sm btn-primary"><i class="fa fa-chevron-left"></i> <?php echo translate_str_by_id(2190); ?></a>';
						html += '				</td>';
						html += '			</tr>';
						html += '		</table>';
						html += '	</div>';
						html += '	</div>';
					}
				
				html += '</div>';
				
				document.getElementById('synonyms_div').innerHTML = html;
            }
        });
	}
	
	
	// После открытия страницы отображаем список производителей
	show_manufacturers();	
</script>