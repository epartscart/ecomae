<?php
/**
Страничный скрипт для страницы импорта каталога из xml
*/
defined('_ASTEXE_') or die('No access');
require_once($_SERVER["DOCUMENT_ROOT"]."/content/general/actions_alert.php");//Вывод сообщений о результатах выполнения действий

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getAdminId();
$user_session = DP_User::getAdminSession();
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




<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2117); ?>
		</div>
		<div class="panel-body">
			
			<div id="info_box" class="col-lg-12">
				<div id="load_info">
					<form action="/<?=$DP_Config->backend_dir; ?>/content/shop/data_transfer/ajax/handler_files.php" method="post" id="file_form" enctype="multipart/form-data">
						<input type="file" onchange="add_files_request()" id="upload_input" name="myfile" style="cursor:pointer;" class="form-control"/>
						<input style="display:none;" type="submit" id="submit" value="<?php echo translate_str_by_id(3211); ?>">
					</form>
				</div>
				
				<div style="display:none;" id="load_info">
					<div style="float:left; text-align:center;">
						<img src="/content/files/images/ajax-loader.gif">
						<br/><span><?php echo translate_str_by_id(3212); ?></span>
					</div>
				</div>
			</div>
			
			
			
			<div class="hr-line-dashed col-lg-12"></div>
			
			
			
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(2750); ?>
				</label>
				<div class="col-lg-6">
					<select id="storages" class="form-control">
						<option value="0"><?php echo translate_str_by_id(2823); ?></option>
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
				</div>
			</div>
			
			<div class="hr-line-dashed col-lg-12"></div>
			
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3213); ?>
				</label>
				<div class="col-lg-6">
					<input id="clear_table" type="checkbox" />
				</div>
			</div>
			
			<div class="hr-line-dashed col-lg-12"></div>
			
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3214); ?>
				</label>
				<div class="col-lg-6">
					<input id="clear_table_storages" type="checkbox" />
				</div>
			</div>
		</div>
		<div class="panel-footer">
			<div class="row">
				<div class="col-lg-12">
					<button onclick="exec_import();" class="btn btn-success " type="button"><i class="fa fa-upload"></i> <span class="bold"><?php echo translate_str_by_id(3181); ?></span></button>
				</div>
			</div>
		</div>
	</div>
</div>







<script>
//Функция запроса на импорт
function exec_import()
{
	var sel = document.getElementById("storages");
	var val = sel.options[sel.selectedIndex].value;
	
	var clear_table = 0;
	if(document.getElementById('clear_table_storages').checked){
		clear_table = 1;
	}
	if(document.getElementById('clear_table').checked){
		clear_table = 2;
	}
	
	if( clear_table == 2 ){
		if( !confirm("<?php echo translate_str_by_id(3215); ?>") ){
			return false;
		}
	}
	
	if(val <= 0){
		alert('<?php echo translate_str_by_id(3216); ?>.');
		return false;
	}
	
	if(!document.getElementById('upload_input').value){
		alert('<?php echo translate_str_by_id(2727); ?>.');
		return false;
	}
	
	var info_box = document.getElementById('info_box');
	info_box.innerHTML = '<div style="overflow:hidden;"><div style="float:left; text-align:center;"><img src="/content/files/images/ajax-loader.gif"><br/><span><?php echo translate_str_by_id(3217); ?></span></div></div>';
	
	jQuery.ajax({
		type: "GET",
		async: true, //Запрос синхронный
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/data_transfer/ajax/ajax_xml_reader.php",
		dataType: "json",//Тип возвращаемого значения
		data: "storage="+val+"&clear="+clear_table+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			//console.log(answer);
			if(answer.error_message != ''){
				info_box.innerHTML = '<div style="overflow:hidden;">'+answer.error_message+'</div>';
			}else{
				location="/<?php echo $DP_Config->backend_dir; ?>/shop/perenos-dannyx/import-kataloga-tovarov-iz-xml-i-json?success_message=<?php echo translate_str_by_id(2300); ?>.";
			}
		}
	});
}

// Жмем на кнопку загрузить файл
function add_files_request(){
	if(document.getElementById('upload_input').value != ''){
		document.getElementById('submit').click();
		var file_form = document.getElementById('file_form');
			file_form.style.display = 'none';
		var load_info = document.getElementById('load_info');
			load_info.style.display = 'block';
	}
}
// Загрузка файла на сервер после выбора файла пользователем.
$(function(){
	$('#file_form').on('submit', function(e){
		// Загружаем файл на сервер
		e.preventDefault();
		var $that = $(this),
				formData = new FormData($that.get(0));// создаем новый экземпляр объекта и передаем ему нашу форму
				
				formData.append('csrf_guard_key', '<?php echo $user_session["csrf_guard_key"]; ?>');
		$.ajax({
			url: $that.attr('action'),
			type: $that.attr('method'),
			contentType: false, // важно - убираем форматирование данных по умолчанию
			processData: false, // важно - убираем преобразование строк по умолчанию
			data: formData,
			dataType: 'json',
			success: function(json){
				if(json){
					var load_info = document.getElementById('load_info');
						load_info.style.display = 'none';
					var file_form = document.getElementById('file_form');
						file_form.style.display = 'block';
					// Если есть сообщение об ошибке
					if(json.msg){
						alert(json.msg);
					}
				}
			}
		});
	});
});
</script>