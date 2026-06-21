<?php
/**
 * Скрипт страницы управления одним плагином
*/
defined('_ASTEXE_') or die('No access');


//Режим редактирования Фронтэнд/Бэкэнд
$edit_mode = null;
if(isset($_COOKIE["edit_mode"]))
{
	$edit_mode = $_COOKIE["edit_mode"];
}
switch($edit_mode)
{
    case "frontend":
        $is_frontend = 1;
        break;
    case "backend":
        $is_frontend = 0;
        break;
    default:
        $is_frontend = 1;
        break;
}




//Обработка древовидной структуры
function tree_htmlentities($data, $data_value_lang)
{
	global $data_structure;
	
	foreach( $data AS $key => $item )
	{
		//Проверяем ключ
		if( htmlentities($key) != $key )
		{
			unset($data[$key]);
			continue;
		}
		
		
		if( is_array($item) )
		{
			$item = tree_htmlentities($item, $data_value_lang);
		}
		else
		{
			//На числовые поля никак не влияет
			$item = htmlentities($item, ENT_QUOTES, "UTF-8", false);
			
			//Какстомный алгоритм
			if( array_search($key, $data_value_lang) !== false )
			{
				$item = save_custom_translation( $data[$key."_lang_str_id"], $item);
			}
		}
		
		$data[$key] = $item;
	}
	
	return $data;
}
?>



<?php
if(!empty($_POST["save_plugin_action"]))//Есть действия
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	//Если админ в другой вкладке браузера переключил режим редактирования (фронтенд/бэкенд), то, запрещаем сохранять изменения.
	if( $is_frontend != $_POST["is_frontend"] )
	{
		$warning_message = urlencode(translate_str_by_id(2204));
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/?warning_message=<?php echo $warning_message; ?>";
		</script>
		<?php
		exit;
	}
	
	
	
    //Сначала проверяем, доступно ли управление для этого плагина
    $check_access_query = $db_link->prepare( "SELECT * FROM `plugins` WHERE `id` = ?;" );
	$check_access_query->execute( array($_POST["id"]) );
    $check_access_record = $check_access_query->fetch();
    if($check_access_record["control_lock"] == true)
    {
       $warning_message = translate_str_by_id(2728);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/plugins/plugin?plugin_id=<?php echo $_POST["id"]; ?>&warning_message=<?php echo $warning_message; ?>";
        </script>
        <?php
        exit();
    }
	
	
	//Если включается плагин "Двухфакторная аутентификация бэкенда" нужно проверить доступность уведомлений
	if($_POST["id"] == 10 && $_POST["activated"] == 1){
		
		$flag_2fa_plugin = false;
		
		//Проверяем настройку почты
		if( !empty($DP_Config->from_name) && !empty($DP_Config->from_email) && !empty($DP_Config->smtp_mode) && !empty($DP_Config->smtp_encryption) && !empty($DP_Config->smtp_host) && !empty($DP_Config->smtp_port) && !empty($DP_Config->smtp_username) && !empty($DP_Config->smtp_password) )
		{
			$email_debug_query = $db_link->prepare("SELECT * FROM `debug_results` WHERE `name` = ?;");
			$email_debug_query->execute( array('email') );
			$email_debug = $email_debug_query->fetch();
			if( $email_debug['status'] == 1 )
			{
				$email_on_query = $db_link->prepare("SELECT `email_on` FROM `notifications_settings` WHERE `name` = 'backend_2fa_email';");
				$email_on_query->execute();
				$email_on_record = $email_on_query->fetch();
				if( $email_on_record['email_on'] == 1 )
				{
					$flag_2fa_plugin = true;
				}
			}
		}
		
		if($flag_2fa_plugin == false)
		{
			//Проверяем настройку телефона
			$check_sms_query = $db_link->prepare("SELECT COUNT(*) FROM `sms_api` WHERE `active` = ?;");
			$check_sms_query->execute( array(1) );
			if( $check_sms_query->fetchColumn() == 1 )
			{
				$sms_debug_query = $db_link->prepare("SELECT * FROM `debug_results` WHERE `name` = ?;");
				$sms_debug_query->execute( array('sms') );
				$sms_debug = $sms_debug_query->fetch();
				if( $sms_debug['status'] == 1 )
				{
					$sms_on_query = $db_link->prepare("SELECT `sms_on` FROM `notifications_settings` WHERE `name` = 'backend_2fa_phone';");
					$sms_on_query->execute();
					$sms_on_record = $sms_on_query->fetch();
					if( $sms_on_record['sms_on'] == 1 )
					{
						$flag_2fa_plugin = true;
					}
				}
			}
		}
		
		if($flag_2fa_plugin == false)
		{
			$warning_message = "Управление этим плагином не доступно. Для активации плагина необходимо настроить смс или email-уведомления - изменения не сохранены";
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/plugins/plugin?plugin_id=<?php echo $_POST["id"]; ?>&warning_message=<?php echo $warning_message; ?>";
			</script>
			<?php
			exit();
		}
	}
	
	
    //Управление доступно - сохраняем
    $SQL_UPDATE = "UPDATE `plugins` SET `caption` = ?, `description` = ?, `activated` = ?, `order` = ?, `data_value` = ? WHERE `id` = ?;";
	
	
	
	$_POST["caption"] = htmlentities($_POST["caption"], ENT_QUOTES, "UTF-8", false);
	$_POST["description"] = htmlentities($_POST["description"], ENT_QUOTES, "UTF-8", false);
	
	
	//Мультиязычность. Кастомный алгоритм
	$_POST["caption"] = save_custom_translation($_POST["caption_lang_str_id"], $_POST["caption"]);
	$_POST["description"] = save_custom_translation($_POST["description_lang_str_id"], $_POST["description"]);
	
	
	
	$_POST["data_value"] = json_decode($_POST["data_value"], true);
	$_POST["data_value"] = tree_htmlentities($_POST["data_value"], json_decode($_POST['data_value_lang'], true));
	$_POST["data_value"] = json_encode($_POST["data_value"]);
	
    if( $db_link->prepare($SQL_UPDATE)->execute( array($_POST["caption"], $_POST["description"], (int)$_POST["activated"], (int)$_POST["order"], $_POST["data_value"], (int)$_POST["id"]) ) != true)
    {
        $error_message = translate_str_by_id(2729);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/plugins/plugin?plugin_id=<?php echo $_POST["id"]; ?>&error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit();
    }
    else
    {
        $success_message = translate_str_by_id(2730);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/plugins/plugin?plugin_id=<?php echo $_POST["id"]; ?>&success_message=<?php echo $success_message ;?>";
        </script>
        <?php
        exit();
    }
}//~if(!empty($_POST["save_plugin_action"]))//Есть действия
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    ?>
    
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
    
    
    <?php
    //Получаем текущие данные по плагину
	$plugin_query = $db_link->prepare("SELECT * FROM `plugins` WHERE `id` = ?;");
	$plugin_query->execute( array($_GET["plugin_id"]) );
    $plugin_record = $plugin_query->fetch();
    
    //Исходные данные:
    $id = $plugin_record["id"];//ID плагина
    $activated = $plugin_record["activated"];//Активирован
    $control_lock = $plugin_record["control_lock"];//Управление заблокировано
    
	$caption_lang_str_id = $plugin_record["caption"];//Заголовок
	$caption = translate_str_by_id($plugin_record["caption"]);//Заголовок
	
    $description_lang_str_id = $plugin_record["description"];//Описание
    $description = translate_str_by_id($plugin_record["description"]);//Описание
    
	$order = $plugin_record["order"];//Порядок запуска
    $data_structure = $plugin_record["data_structure"];//Структура спецнастроек
    $data_value = $plugin_record["data_value"];//Значения спецнастроек
    
    
    //Обрабатываем некоторые параметры
    //Чекбокс "Активирован"
    $activated_ckecked = "";
    if($activated == true)
    {
        $activated_ckecked = " checked";
    }
    //Управление доступно
    $control_state = "<font style=\"font-weight:bold; color:#00A100\"> ".translate_str_by_id(2731)."</font>";
    if($control_lock == true)
    {
        $control_state = "<font style=\"font-weight:bold; color:#C10000\"> ".translate_str_by_id(2732)."</font>";
    }
    //Структура спецнастроек
    if($data_structure == "") $data_structure = "[]";
    //Значения спецнастроек
    if($data_value == "") $data_value = "[]";
    ?>
    
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" onClick="save_plugin();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/plugins/plugins_manager">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/puzzle.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2733); ?></div>
				</a>
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
    
    
    
    
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2734); ?>
			</div>
			<div class="panel-body">
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2735); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $plugin_record["id"]; ?>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2277); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" name="caption_input" id="caption_input" value="<?php echo $caption; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2073); ?>
					</label>
					<div class="col-lg-6">
						<textarea class="form-control" name="description_input" id="description_input"><?php echo $description; ?></textarea>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2644); ?>
					</label>
					<div class="col-lg-6">
						<input type="checkbox" name="activated_checkbox" id="activated_checkbox" <?php echo $activated_ckecked; ?>/>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2736); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" name="order_input" id="order_input" value="<?php echo $order; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2737); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $control_state; ?>
					</div>
				</div>
				
			</div>
		</div>
	</div>
	
	
	
	
	<?php
	//!!! Мультиязычность. Данная версия скрипта актуальная для двух плагинов с id 11 и 12 (403 и 404 страницы для фронтенда и бэкенда). Пока только у этих плагинов есть настраиваемые параметры. Если появятся еще плагины с настраиваемыми параметрами, то, смотреть уже специфику - возможно нужно будет доработать здесь обработку мультиязычности
	if($data_structure != "[]")
	{
		?>
		<div class="col-lg-6">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2738); ?>
				</div>
				<div class="panel-body">
					<?php
					require_once("content/control/get_widget.php");//Скрипт для получения html-кода виджетов различных типов
    	        
					//Текущие значения спецнастроек плагина
					$data_value = json_decode($data_value, true);
					
					$data_structure = json_decode($plugin_record["data_structure"], true);//Структура спецнастроек в форма JSON
					
					
					for($i=0; $i<count($data_structure); $i++)
					{
						
						if($i > 0)
						{
							?>
							<div class="hr-line-dashed col-lg-12"></div>
							<?php
						}
						
						$options = array();//Переменная для списка возможных опций
						//Проверяем существование поля "Способ получения возможных значений"
						if(!empty($data_structure[$i]["options_way"]))
						{
							//Получаем список опций указанным способом
							switch($data_structure[$i]["options_way"])
							{
								case "direct":
									$options = json_decode($data_structure[$i]["options"], true);
									break;
								case "sql":
									$SQL_SELECT_OPTIONS = str_replace(array("<is_frontend>"), $is_frontend, $data_structure[$i]["options"]);//Подставляем режим работы
									
									$options_query = $db_link->prepare($SQL_SELECT_OPTIONS);
									$options_query->execute();
									while( $options_record = $options_query->fetch() )
									{
										array_push($options, array("caption"=>$options_record["caption"], "value"=>$options_record["value"]));
									}
									break;
							};
						}//if() - если предполагается получение возможных опций настройки
						
						$value = $data_value[$data_structure[$i]["name"]];//Текущее значение спецнастройки
						
						//Для текстовых полей - мультиязычность
						$widget_hidden = "";
						if( $data_structure[$i]["type"] == 'text' || $data_structure[$i]["type"] == 'textarea')
						{
							$do_multilang = true;
							
							//Особая обработка для 403_content и 404_content (там есть особенность). Мультиязычность только если тип text
							if( $data_structure[$i]["name"] == '403_content' )
							{
								if( $data_value['403_content_type'] == 'php' )
								{
									$do_multilang = false;
								}
							}
							if( $data_structure[$i]["name"] == '404_content' )
							{
								if( $data_value['404_content_type'] == 'php' )
								{
									$do_multilang = false;
								}
							}
							
							if( $do_multilang )
							{
								//hidden-инпут, содержащий ID строки из мультиязычности
								$widget_hidden = get_widget('hidden', $data_structure[$i]["name"]."_lang_str_id", (int)$value, null);
								
								//Перевод строки на текущий язык ПУ
								$value = translate_str_by_id($value);
							}
							else
							{
								//hidden-инпут, содержащий ID строки из мультиязычности
								$widget_hidden = get_widget('hidden', $data_structure[$i]["name"]."_lang_str_id", '0', null);
							}
						}
						
						
						$widget = get_widget($data_structure[$i]["type"], $data_structure[$i]["name"], $value, $options);	
						?>
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id($data_structure[$i]["caption"]); ?>
							</label>
							<div class="col-lg-6">
								<?php echo $widget;?>
								<?php echo $widget_hidden;?>
							</div>
						</div>
						<?php
					}
					?>
				</div>
			</div>
		</div>
		<?php
	}
	?>
	
	
	


    

    
    
    

    
    
    
    
    
    
    
    
    <?php
	//Блок формы сохранения выводим только, если управление доступно
	if($control_lock == false)
	{
	?>
    
        <!-- ********************************************************************** -->
        <!-- START - БЛОК ФОРМЫ СОХРАНЕНИЯ -->
        <script>
        <?php
        if($data_structure == "[]")
        {
            ?>
            var data_structure = "";
            <?php
        }
        else
        {
            ?>
            var data_structure = <?php echo json_encode($data_structure);?>;//Структура спецнастроек
            <?php
        }
        ?>
        // ----------------------------------------------------------------
        //Сохранение плагина
        function save_plugin()
        {
            //1. Заголовок
            if(document.getElementById("caption_input").value == "")
            {
                webix.message({type:"error", text:"<?php echo translate_str_by_id(2638); ?>"});
                return;
            }
            document.getElementById("caption").value = document.getElementById("caption_input").value;
            
            
            //2. Включен
			if( document.getElementById("activated_checkbox").checked == true )
			{
				document.getElementById("activated").value = '1';
			}
			else
			{
				document.getElementById("activated").value = '0';
			}
            
            
            
            //3. Порядок запуска
            document.getElementById("order").value = document.getElementById("order_input").value;
            
            
            //4. Описание
            document.getElementById("description").value = document.getElementById("description_input").value;
            
          
            
            //5. Значения специальных настроек
            var data_value = new Object;
			var data_value_lang = new Array();
            if(data_structure.length > 0)
            {
                //5.1 По списку специальных настроек
                for(var i=0; i < data_structure.length; i++)
                {
                    //5.2 Для кажой настройки получить значение из виджета
                    //5.3 Записать это значение в общий объект спецнастроек
                    data_value[data_structure[i]["name"]] = document.getElementById(data_structure[i]["name"]).value;
					
					//Мультиязычность
					if( data_structure[i]["type"] == 'text' || data_structure[i]["type"] == 'textarea' )
					{
						//Если это 403_content или 404_content, на мультиязычность они обрабатываются только, если тип text, а не php
						if( data_structure[i]["name"] == '403_content' )
						{
							if( document.getElementById('403_content_type').value == 'php' )
							{
								continue;
							}
						}
						if( data_structure[i]["name"] == '404_content' )
						{
							if( document.getElementById('404_content_type').value == 'php' )
							{
								continue;
							}
						}
						
						
						data_value[data_structure[i]["name"]+'_lang_str_id'] = document.getElementById(data_structure[i]["name"]+'_lang_str_id').value;
						
						//Добавляем имя данного поля в список на обработку мультиязычности
						data_value_lang.push(data_structure[i]["name"]);
					}
                }
            }
			document.getElementById('data_value_lang').value = JSON.stringify(data_value_lang);
            
            //5.4 Перевести объект в JSON-формат и записать его в поле формы
            document.getElementById("data_value").value = JSON.stringify(data_value);
            
			//console.log(data_value_lang);
			//return;
            
            //6. Отправляем форму
        	document.forms["save_plugin_form"].submit();//Отправляем
        }
        // ----------------------------------------------------------------
        </script>
        <form name="save_plugin_form" style="display:none" method="POST">
            <input type="hidden" name="save_plugin_action" id="save_plugin_action" value="save_plugin_action" /> <!-- Говорит скрипту, что идет сохранение плагина -->
            <input type="hidden" name="id" id="id" value="<?php echo $id;?>" /> <!-- ID плагина -->
            <!-- Ok --><input type="hidden" name="caption" id="caption" value="" /> <!-- Заголовок -->
            <!-- Ok --><input type="hidden" name="caption_lang_str_id" id="caption_lang_str_id" value="<?php echo $caption_lang_str_id; ?>" /> <!-- Заголовок -->
            <!-- Ok --><input type="hidden" name="activated" id="activated" value="" /> <!-- Включен (либо " checked") -->
            <!-- Ok --><input type="hidden" name="order" id="order" value="" /> <!-- Порядок запуска -->
            <!-- Ok --><input type="hidden" name="data_value" id="data_value" value="" /> <!-- Значения специальных настроек -->
            <!-- Ok --><input type="hidden" name="description" id="description" value="" /> <!-- Описание -->
            <!-- Ok --><input type="hidden" name="description_lang_str_id" id="description_lang_str_id" value="<?php echo $description_lang_str_id; ?>" /> <!-- Описание -->
            
			<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none"/>
			
			<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
			
			<!-- Массив имен дополнительных параметров, которые при сохранении нужно обработать через кастомный алгоритм -->
			<input type="hidden" name="data_value_lang" id="data_value_lang" value='' />
        </form>
        <!-- END - БЛОК ФОРМЫ СОХРАНЕНИЯ -->
        <!-- ********************************************************************** -->
    
    <?php
	}//if - //Блок формы сохранения выводим только, если управление доступно
    ?>
    
    
    
    <?php
}//~else//Действий нет - выводим страницу
?>