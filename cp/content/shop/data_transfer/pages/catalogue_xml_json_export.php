<?php
/**
Страничный скрипт для страницы экспорта каталога в xml
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
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/perenos-dannyx">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/xml.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(3161); ?></div>
			</a>
		
		
		
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
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
			<div class="col-lg-12 text-center"><h3><?php echo translate_str_by_id(3196); ?></h3></div>
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3197); ?>
				</label>
				<div class="col-lg-6">
					<input type="checkbox" id="output_products_text" checked="checked" />
				</div>
			</div>
			<div class="hr-line-dashed col-lg-12"></div>
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3198); ?>
				</label>
				<div class="col-lg-6">
					<input type="checkbox" id="output_products_images" checked="checked" />
				</div>
			</div>
			<div class="hr-line-dashed col-lg-12"></div>
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3199); ?>
				</label>
				<div class="col-lg-6">
					<input type="checkbox" id="output_products_suggestions" checked="checked" />
				</div>
			</div>
			<div class="hr-line-dashed col-lg-12"></div>
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(762); ?>
				</label>
				<div class="col-lg-6">
					<select multiple="multiple" id="offices">
						<?php
						$offices_query = $db_link->prepare("SELECT * FROM `shop_offices`");
						$offices_query->execute();
						while( $office = $offices_query->fetch() )
						{
							?>
							<option value="<?php echo $office["id"]; ?>"><?php echo translate_str_by_id($office["caption"])." (ID ".$office["id"].")"; ?></option>
							<?php
						}
						?>
					</select>
					<script>
						//Делаем из селектора виджет с чекбоками
						$('#offices').multipleSelect({placeholder: "<?php echo translate_str_by_id(3200); ?>...", width:"100%"});
						
						$("#offices").multipleSelect('checkAll');
					</script>
				</div>
			</div>
			
			<div class="hr-line-dashed col-lg-12"></div>
			
			
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3201); ?> (<?php echo translate_str_by_id(3202); ?>)
				</label>
				<div class="col-lg-6">
					<select id="groups" class="form-control">
						<?php
						$groups_query = $db_link->prepare("SELECT * FROM `groups`");
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
			
			
			
			
			<div class="col-lg-12 text-center"><h3><?php echo translate_str_by_id(3203); ?></h3></div>
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3204); ?>
				</label>
				<div class="col-lg-6">
					<select id="output_format" class="form-control">
						<option value="xml">XML</option>
						<option value="json">JSON</option>
					</select>
				</div>
			</div>
			<div class="hr-line-dashed col-lg-12"></div>
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3205); ?>
				</label>
				<div class="col-lg-6">
					<select id="data_output_mode" class="form-control">
						<option value="create_file"><?php echo translate_str_by_id(3206); ?></option>
						<option value="download_file"><?php echo translate_str_by_id(3207); ?></option>
						<option value="open_file_browser"><?php echo translate_str_by_id(3208); ?></option>
					</select>
				</div>
			</div>
		</div>
		<div class="panel-footer">
			<div class="row">
				<div class="col-lg-12">
					<button onclick="exec_export();" class="btn btn-success " type="button"><i class="fa fa-download"></i> <span class="bold"><?php echo translate_str_by_id(3209); ?></span></button>
				</div>
			</div>
		</div>
	</div>
</div>









<a href="" id="a_download" target="_blank" download></a>
<a href="" id="a_open_tab" target="_blank"></a>

<script>
//Функция запроса на экспорт
function exec_export()
{
	var request = new Object;
	request.output_format = document.getElementById("output_format").value;
	request.output_products_text = document.getElementById("output_products_text").checked;
	request.output_products_images = document.getElementById("output_products_images").checked;
	request.output_products_suggestions = document.getElementById("output_products_suggestions").checked;
	request.data_output_mode = document.getElementById("data_output_mode").value;
    request.offices = [].concat( $("#offices").multipleSelect('getSelects') );
	request.group_id = document.getElementById("groups").value;

	jQuery.ajax({
		type: "GET",
		async: true, //Запрос синхронный
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/data_transfer/ajax/ajax_catalogue_to_xml.php",
		dataType: "json",//Тип возвращаемого значения
		data: "export_options="+encodeURI(JSON.stringify(request))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			console.log(answer);
			if(answer.status == true)
			{
				if(document.getElementById("data_output_mode").value == "create_file")
				{
					alert("<?php echo translate_str_by_id(3210); ?>");
				}
				else if(document.getElementById("data_output_mode").value == "download_file")
				{
					document.getElementById("a_download").setAttribute("href", '/<?php echo $DP_Config->backend_dir; ?>/tmp/'+answer.filename);
					document.getElementById("a_download").click();
				}
				else if(document.getElementById("data_output_mode").value == "open_file_browser")
				{
					document.getElementById("a_open_tab").setAttribute("href", '/<?php echo $DP_Config->backend_dir; ?>/tmp/'+answer.filename);
					document.getElementById("a_open_tab").click();
				}
			}
			else
			{
				alert("<?php echo translate_str_by_id(2122); ?>");
			}
		}
	});
}
</script>
