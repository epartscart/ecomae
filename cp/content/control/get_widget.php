<?php
/**
 * Библиотечный скрипт для получения html-кода виджета
 * Используется для страницы настройки конфигурации config.php и для страницы управления модулем и т.д.
*/
defined('_ASTEXE_') or die('No access');

function get_widget($type, $name, $value, $options)
{
	global $db_link;
	
    $widget = "";
    switch($type)
    {
        case 'text':
            $widget = "<input class=\"form-control\" type=\"text\" name=\"".$name."\" id=\"".$name."\" value=\"".$value."\" />";
            break;
        case 'password':
            $widget = "<input class=\"form-control\" type=\"password\" name=\"".$name."\" id=\"".$name."\" value=\"\" placeholder=\"".translate_str_by_id(2443)."\" />";
            break;
        case 'checkbox':
            $checked = "";
            if(filter_var($value, FILTER_VALIDATE_BOOLEAN))
            {
                $checked = "checked";
            }
            $widget = "<input class=\"form-control\" type=\"checkbox\" name=\"".$name."\" id=\"".$name."\" $checked/>";
            break;
        case 'radio':
            break;
        case 'select':
            $widget = "<select class=\"form-control\" name=\"".$name."\" id=\"".$name."\">";
            for($j=0; $j<count($options); $j++)
            {
                $selected = "";
                if($value == $options[$j]["value"]) $selected = " selected";
                $widget .= "<option value=\"".$options[$j]["value"]."\" $selected>".translate_str_by_id($options[$j]["caption"])."</option>";
            }
            $widget .= "</select>";
            break;
        case 'image':
            $widget = "<button onclick=\"point_file('".$name."');\" class=\"btn btn-success\" type=\"button\"><i class=\"fa fa-file\"></i> <span class=\"bold\">Выбрать файл</span></button><input type=\"hidden\" value=\"".$value."\" id=\"".$name."\" name=\"".$name."\" /> <div style=\"display:inline-block\" id=\"".$name."_indicator\">".$value."</div>";
            break;
        case 'textarea':
            $widget = "<textarea class=\"form-control\" name=\"".$name."\" id=\"".$name."\">".$value."</textarea>";
            break;
        case 'number':
            $widget = "<input class=\"form-control\" type=\"number\" name=\"".$name."\" id=\"".$name."\" value=\"".$value."\" />";
            break;
        case 'color':
            $widget = "<input class=\"form-control\" type=\"color\" name=\"".$name."\" id=\"".$name."\" value=\"".$value."\" />";
            break;
		case 'hidden':
			$widget = "<input class=\"hidden form-control\" type=\"hidden\" name=\"".$name."\" id=\"".$name."\" value=\"".$value."\" />";
			break;
		case 'image_file':
			$img_preview_src = $value;
			$delete_a_style = "";
			if( empty($img_preview_src) )
			{
				$img_preview_src = "/content/files/images/no_image.png";
				
				$delete_a_style = " style=\"display:none;\"";
			}
			$img_preview = "<img src=\"".$img_preview_src."\" style=\"max-width:90px;max-height:90px;\">";
		
            $widget = "<input style=\"display:none;\" type=\"file\" value=\"".$value."\" id=\"".$name."\" name=\"".$name."\" onchange=\"onFileChanged_".$name."();\" accept=\"image/jpeg,image/jpg,image/png,image/gif\" /> <button class=\"btn btn-success\" type=\"button\" onclick=\"document.getElementById('".$name."').click();\"><i class=\"fa fa-file\"></i> <span class=\"bold\">Выбрать файл</span></button> <br><br> <div id=\"delete_a_div_".$name."\" align=\"left\" ".$delete_a_style."><a class=\"delete_a\" href=\"javascript:void(0);\" onclick=\"deleteFile_".$name."();\"><span>x</span></a></div> <div id=\"image_file_preview_".$name."\">".$img_preview."</div>
			<input type=\"hidden\" name=\"image_file_deleted_".$name."\" id=\"image_file_deleted_".$name."\" />
			<script>
			function onFileChanged_".$name."()
			{
				var input_file = document.getElementById(\"".$name."\");//input для файла изображения
				var file = input_file.files[0];//Получаем выбранный файл
				
				if(file == undefined)
				{
					return;
				}

				//Проверяем тип файла
				if(file.type != \"image/jpeg\" && file.type != \"image/jpg\" && file.type != \"image/png\" && file.type != \"image/gif\")
				{
					input_file.value = null;
					alert(\"".translate_str_by_id(2444)."\");
					return;
				}
				
				//Предпросмотр файла - Показываем
				document.getElementById(\"image_file_preview_".$name."\").innerHTML = \"<img src='\"+URL.createObjectURL(file)+\"' style='max-width:90px;max-height:90px;' />\";
				
				//Кнопка удалить - Показать
				document.getElementById(\"delete_a_div_".$name."\").setAttribute(\"style\",\"display:block\");
				
				//Ставим флаг Удален - Нет
				document.getElementById(\"image_file_deleted_".$name."\").value = 'deleted';
			}
			function deleteFile_".$name."()
			{
				//Очищаем input с типом Файл
				document.getElementById(\"".$name."\").value = \"\";
				
				//Предпросмотр файла - Нет изображения
				document.getElementById(\"image_file_preview_".$name."\").innerHTML = \"<img src='/content/files/images/no_image.png' style='max-width:90px;max-height:90px;' />\";
				
				//Кнопка удалить - Скрыть
				document.getElementById(\"delete_a_div_".$name."\").setAttribute(\"style\",\"display:none\");
				
				//Ставим флаг Удален - Да
				document.getElementById(\"image_file_deleted_".$name."\").value = 'deleted';
			}
			</script>
			";
            break;
		//Далее специальный виджет для выбора полей профиля пользователя. Используется для настройки печати документов
		case 'user_profile_json_builder':
			$widget = "";
			//Получаем поля регистрации
			$reg_fields = array();
			$reg_fields_query = $db_link->prepare("SELECT * FROM `reg_fields` WHERE `main_flag` = ? ORDER BY `order`, `record_id`;");
			$reg_fields_query->execute( array(0) );
			while( $reg_field = $reg_fields_query->fetch() )
			{
				$reg_field["show_for"] = json_decode($reg_field["show_for"], true);
				
				$reg_field['caption'] = translate_str_by_id($reg_field['caption']);
				
				array_push($reg_fields, $reg_field);
			}
			
			
			//Получаем регистрационные варианты:
			$reg_variants_query = $db_link->prepare("SELECT * FROM `reg_variants` ORDER BY `order`, `id`;");
			$reg_variants_query->execute();
			while( $reg_variant = $reg_variants_query->fetch() )
			{
				$widget .= "<div class=\"col-lg-6 text-right\">".translate_str_by_id(2445)." \"".translate_str_by_id($reg_variant["caption"])."\"</div><div class=\"col-lg-6\"><select multiple=\"multiple\" id=\"".$name."_user_profile_".$reg_variant["id"]."_select\">";
				
				for( $i=0 ; $i < count($reg_fields) ; $i++ )
				{
					if( array_search($reg_variant["id"], $reg_fields[$i]["show_for"]) !== false )
					{
						$widget .= "<option value=\"".$reg_fields[$i]["name"]."\">".$reg_fields[$i]["caption"]."</option>";
					}
				}
				
				
				
				
				$widget .= "</select>
				<!-- Инпут для хранения значений и передачи на сервер -->
				<input type=\"hidden\" name=\"".$name."_user_profile_".$reg_variant["id"]."\" id=\"".$name."_user_profile_".$reg_variant["id"]."\" value='".json_encode($value["reg_variant_".$reg_variant["id"]])."' />
				</div><br><br>";
				
				$widget .= "<script>
					function set_input_value_on_".$name."_user_profile_".$reg_variant["id"]."()
					{
						document.getElementById(\"".$name."_user_profile_".$reg_variant["id"]."\").value = JSON.stringify([].concat( $('#".$name."_user_profile_".$reg_variant["id"]."_select').multipleSelect('getSelects') ));
					}
					
				
					//Делаем из селектора виджет с чекбоками
					$('#".$name."_user_profile_".$reg_variant["id"]."_select').multipleSelect(
						{
							placeholder: \"".translate_str_by_id(2445).": &quot;".translate_str_by_id($reg_variant["caption"])."&quot;\", width:\"100%\",
							
							onClose: function () {
								set_input_value_on_".$name."_user_profile_".$reg_variant["id"]."();
							}
						}
					);

					
					
					//Инициализируем выбранные значения
					$('#".$name."_user_profile_".$reg_variant["id"]."_select').multipleSelect('setSelects', ".json_encode($value["reg_variant_".$reg_variant["id"]]).");
				</script>";
				
			}
		
			
			break;
    };
    
    return $widget;
}//~ function get_widget($type, $name, $value, $options)
?>


<!-- START БЛОК ДЛЯ ВЫБОРА ФАЙЛА С СЕРВЕРА (папка /content/files/ через elfinder) -->
<!--Start Модальное окно: Модальное окно: Указание файла-->
<div class="text-center m-b-md">
	<div class="modal fade" id="modalWindow_fileWindow" tabindex="-1" role="dialog"  aria-hidden="true">
		<div class="modal-dialog modal-lg">
			<div class="modal-content">
				<div class="color-line"></div>
				<div class="modal-header">
					<h4 class="modal-title"><?php echo translate_str_by_id(2446); ?></h4>
				</div>
				<div class="modal-body">
					<div class="row">
						<div class="col-lg-12">
							<div id="elfinder">
							</div>
						</div>
					</div>
					<input type="hidden" id="current_field_edited" value="" />
				</div>
				<div class="modal-footer">
					
					<div class="col-lg-6">
						<input id="selected_file_path" value="" class="form-control" />
					</div>
					

					
					<div class="col-lg-6">
						<button onclick="apply_button_click();" class="btn btn-success" type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(2189); ?></span></button>
					
						<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
</div>

<?php
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>
<!--End Модальное окно: Модальное окно: Указание файла-->
<script>
var elf = "";//Переменная для файлового менеджера
function point_file(name)
{
	//Текущее редактируемое поле
	document.getElementById("current_field_edited").value = name;
	
	//Задаем значение в поле окна
	document.getElementById("selected_file_path").value = document.getElementById(name+"_indicator").innerHTML;
	
	$('#modalWindow_fileWindow').modal();//Открыть окно
	
	// elFinder initialization (REQUIRED)
	$().ready(function() {
		elf = $('#elfinder').elfinder({
			url : '/<?php echo $DP_Config->backend_dir?>/lib/elfinder/php/connector.php',  // connector URL (REQUIRED)
			lang: 'ru',             // language (OPTIONAL)
			onlyMimes: ["image"], // display all images
			commands : ['home','back', 'forward', 'search','sort'],
			getFileCallback: function(url) {
					var re = /\/\.\./g;
					var result = url.replace(re, "");
			
					
					
					$('#selected_file_path').val(result);
				},
			uiOptions : {
				// toolbar configuration
				toolbar : [
					['back', 'forward'],
					['info'],
					['search'],
					['view'],
				]
			},
			customData: { 
				csrf_guard_key: '<?php echo $user_session["csrf_guard_key"]; ?>'
			},
			allowShortcuts:false,
			
		}).elfinder('instance');
	});
}
//Нажатие применить для указания файла изображения
function apply_button_click()
{
	document.getElementById(document.getElementById("current_field_edited").value).value = document.getElementById("selected_file_path").value;
	document.getElementById(document.getElementById("current_field_edited").value+"_indicator").innerHTML = document.getElementById("selected_file_path").value;
	$('#modalWindow_fileWindow').modal('hide');//Скрыть окно
}
</script>
<!-- ~ END БЛОК ДЛЯ ВЫБОРА ФАЙЛА С СЕРВЕРА (папка /content/files/ через elfinder) -->