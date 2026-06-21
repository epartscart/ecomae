<?php
defined('_ASTEXE_') or die('No access');
//Страничный скрипт модуля печати документов. Настройка для одного документа



if( isset( $_POST["action"] ) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	/*
	Перед ПЕРЕсохранением настроек - нужно получить из таблицы текущие значения (они могут потребоваться при формировании JSON новых настроек)
	
	
	Когда получаем данные по свойству типа "image_file":
	
	- есть данные в поле name (т.е. пользователь явно указал файл, значит сам файл закачиваем в /content/files/images, в настройку в JSON записываем путь к нему). Далее смотрим, был ранее указан другой файл (в предыдущих настройках) - если был - удаляем сам файл
	
	- если в поле name нет данных и при этом есть поле name_delete = true, значит пользователь удалил текущий файл, а новый не стал выбирать. В этом случае: удаляем сам файл, в настройки JSON указываем пустую строку
	
	- если в поле name нет данных и нет name_delete = true, т.е. пользователь не выбирал файл и не удалял файл, т.е. вообще не изменял значение данной настройки. В этом случае в JSON записываем предыдущее значение, а, с файлами ничего не делаем
	
	
	*/
	
	
	
	/*if( true )
	{
		var_dump($_FILES);
		var_dump($_POST);
	}
	else */
	if( $_POST["action"] == "save" )
	{
		$files_prefix = time();//Для добавления уникально префикса в имена файлов
		
		
		//Получаем текущие значения параметров и само описание параметров в JSON-виде
		$current_parameters_values_query = $db_link->prepare("SELECT * FROM `shop_print_docs` WHERE `id` = ?;");
		$current_parameters_values_query->execute( array($_POST["print_doc_id"]) );
		$current_parameters_values_record = $current_parameters_values_query->fetch();
		$current_parameters_values = json_decode($current_parameters_values_record["parameters_values"], true);
		$parameters_description = json_decode($current_parameters_values_record["parameters_description"], true);
		
		
		$offic_id_get_arg = '';
		$parameters_record_id = 0;
		if( isset( $DP_Config->wholesaler ) )
		{
			//У пользователя должны быть права на магазин
			$office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
			$office_query->execute( array($_POST['office_id']) );
			$office = $office_query->fetch();
			if( $office == false )
			{
				exit;
			}
			if( array_search( DP_User::getAdminId(), json_decode($office['users'], true) ) === false )
			{
				exit;
			}

			
			$current_parameters_values_query = $db_link->prepare("SELECT * FROM `shop_print_docs_wholesaler` WHERE `doc_name` = ? AND `office_id` = ?;");
			$current_parameters_values_query->execute( array( $current_parameters_values_record['name'], $_POST['office_id'] ) );
			$current_parameters_values_record = $current_parameters_values_query->fetch();
			$current_parameters_values = json_decode($current_parameters_values_record["parameters_values"], true);
			
			
			$parameters_record_id = $current_parameters_values_record['id'];
			
			$offic_id_get_arg = '&office_id='.$_POST['office_id'];
		}
		
		
		
		//Формирование массива новых значений параметров
		$new_parameters_values = array();
		for( $i=0 ; $i < count($parameters_description) ; $i++ )
		{			
			//Текстовый input или textarea
			if( $parameters_description[$i]["type"] == "text" || $parameters_description[$i]["type"] == "textarea" )
			{
				$new_parameters_values[$parameters_description[$i]["name"]] = htmlentities($_POST[$parameters_description[$i]["name"]], ENT_QUOTES, "UTF-8");
			}
			//Чекбокс
			else if( $parameters_description[$i]["type"] == "checkbox" )
			{
				if( isset($_POST[$parameters_description[$i]["name"]]) )
				{
					$new_parameters_values[$parameters_description[$i]["name"]] = 1;
				}
				else
				{
					$new_parameters_values[$parameters_description[$i]["name"]] = 0;
				}
			}
			//Поля профиля пользователя
			else if( $parameters_description[$i]["type"] == "user_profile_json_builder" )
			{
				$value = array();
				//Получаем типы учетных записей
				$reg_variants_query = $db_link->prepare("SELECT * FROM `reg_variants` ORDER BY `order`, `id`;");
				$reg_variants_query->execute();
				while( $reg_variant = $reg_variants_query->fetch() )
				{
					$value["reg_variant_".$reg_variant["id"]] = json_decode($_POST[$parameters_description[$i]["name"]."_user_profile_".$reg_variant["id"]]);
				}
				
				$new_parameters_values[$parameters_description[$i]["name"]] = $value;
			}
			//Файл изображения
			else if( $parameters_description[$i]["type"] == "image_file" )
			{
				//Если в поле name нет данных и нет name_delete = true, т.е. пользователь не выбирал файл и не удалял файл, т.е. вообще не изменял значение данной настройки. В этом случае в JSON записываем предыдущее значение, а, с файлами ничего не делаем
				if( $_FILES[$parameters_description[$i]["name"]]["size"] == 0 && empty($_POST["image_file_deleted_".$parameters_description[$i]["name"]]) )
				{
					$new_parameters_values[$parameters_description[$i]["name"]] = $current_parameters_values[$parameters_description[$i]["name"]];
				}
				//Если в поле name нет данных и при этом есть поле name_delete = true, значит пользователь удалил текущий файл, а новый не стал выбирать. В этом случае: удаляем сам файл, в настройки JSON указываем пустую строку
				else if( $_FILES[$parameters_description[$i]["name"]]["size"] == 0 && $_POST["image_file_deleted_".$parameters_description[$i]["name"]] == "deleted")
				{
					$new_parameters_values[$parameters_description[$i]["name"]] = "";
					
					unlink($_SERVER["DOCUMENT_ROOT"]."/content/files/images/".$current_parameters_values[$parameters_description[$i]["name"]]);
				}
				//Есть данные в поле name (т.е. пользователь явно указал файл, значит сам файл закачиваем в /content/files/images, в настройку в JSON записываем путь к нему). Далее смотрим, был ранее указан другой файл (в предыдущих настройках) - если был - удаляем сам файл
				else if( $_FILES[$parameters_description[$i]["name"]]["size"] > 0 )
				{
					//Проверяем тип файла
					$fileName = $_FILES[$parameters_description[$i]["name"]]['name'];
					//Получаем расширение файла
					$file_extension = explode(".", $fileName);
					$file_extension = $file_extension[count($file_extension)-1];
					//Проверка расширения
					$file_extension = strtolower($file_extension);
					if( array_search( $file_extension, array('png', 'jpg', 'jpeg', 'gif') ) === false )
					{
						exit;
					}
					
					
					//Сначала проверяем, был ли загружен ранее другой файл
					if( $current_parameters_values[$parameters_description[$i]["name"]] != "" )
					{
						unlink($_SERVER["DOCUMENT_ROOT"]."/content/files/images/".$current_parameters_values[$parameters_description[$i]["name"]]);
					}
					
					//Теперь загружаем новый файл
					$files_prefix++;
					if (! move_uploaded_file($_FILES[$parameters_description[$i]["name"]]['tmp_name'], $_SERVER["DOCUMENT_ROOT"]."/content/files/images/".$files_prefix."_".basename($_FILES[$parameters_description[$i]["name"]]['name']))) 
					{
						$error_message = translate_str_by_id(3787)." ".basename($_FILES[$parameters_description[$i]["name"]]['name']).". ".translate_str_by_id(3788);
						?>
						<script>
							location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/modul-pechati-dokumentov/nastrojka-pechati-dokumenta?print_doc_id=<?php echo $_POST["print_doc_id"]; ?>&error_message=<?php echo urlencode($error_message).$offic_id_get_arg; ?>";
						</script>
						<?php
						exit;
					}
					
					$new_parameters_values[$parameters_description[$i]["name"]] = $files_prefix."_".basename($_FILES[$parameters_description[$i]["name"]]['name']);
				}
				else
				{
					$error_message = translate_str_by_id(3789);
					?>
					<script>
						location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/modul-pechati-dokumentov/nastrojka-pechati-dokumenta?print_doc_id=<?php echo $_POST["print_doc_id"]; ?>&error_message=<?php echo urlencode($error_message).$offic_id_get_arg; ?>";
					</script>
					<?php
					exit;
				}
			}//~ тип файл
		}//~for
		
		
		
		
		$SQL_update = "UPDATE `shop_print_docs` SET `parameters_values` = ? WHERE `id` = ?;";
		$binding_values = array( json_encode($new_parameters_values), $_POST["print_doc_id"] );
		if( isset( $DP_Config->wholesaler ) )
		{
			$SQL_update = "UPDATE `shop_print_docs_wholesaler` SET `parameters_values` = ? WHERE `id` = ?;";
			$binding_values = array( json_encode($new_parameters_values), $parameters_record_id );
		}
		
		
		//Записываем новые значения в БД
		if( $db_link->prepare( $SQL_update )->execute( $binding_values ) )
		{
			$success_message = translate_str_by_id(3790);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/modul-pechati-dokumentov/nastrojka-pechati-dokumenta?print_doc_id=<?php echo $_POST["print_doc_id"]; ?>&success_message=<?php echo urlencode($success_message).$offic_id_get_arg; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$error_message = translate_str_by_id(3326);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/modul-pechati-dokumentov/nastrojka-pechati-dokumenta?print_doc_id=<?php echo $_POST["print_doc_id"]; ?>&error_message=<?php echo urlencode($error_message).$offic_id_get_arg; ?>";
			</script>
			<?php
			exit;
		}
	}
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/get_widget.php");//Скрипт для получения html-кода виджетов различных типов
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
				
				<?php
				//Кнопка сохранить
				print_backend_button( array("caption"=>translate_str_by_id(2114), "url"=>"javascript:void(0);" ,"onclick"=>"document.forms['save_form'].submit();", "background_color"=>"#3C3", "fontawesome_class"=>"fas fa-save") );
				?>
				
				
				
				<?php
				//Кнопка для перехода на страницу со списком всех документов
				$print_doc_button_query = $db_link->prepare("SELECT * FROM `control_items` WHERE `caption` = ?;");
				$print_doc_button_query->execute( array(797) );
				$print_doc_button = $print_doc_button_query->fetch();
				$print_doc_button["url"] = str_replace( array("<backend>"), $DP_Config->backend_dir, $print_doc_button["url"]);
				$print_doc_button["caption"] = translate_str_by_id($print_doc_button["caption"]);
				print_backend_button( $print_doc_button );
				?>
			
			
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	<?php
	//Получаем настройки из учетной записи документа
	$doc_query = $db_link->prepare("SELECT * FROM `shop_print_docs` WHERE `id` = ?;");
	$doc_query->execute( array($_GET["print_doc_id"]) );
	$doc_record = $doc_query->fetch();
	if( $doc_record == false )
	{
		exit;
	}
	
	$parameters_description = json_decode($doc_record["parameters_description"], true);
	$parameters_values = json_decode($doc_record["parameters_values"], true);
	?>
	
	
	<form method="POST" enctype="multipart/form-data" name="save_form">
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	<input type="hidden" name="action" value="save" />
	<input type="hidden" name="print_doc_id" value="<?php echo $_GET["print_doc_id"]; ?>" />
	
	
	
	<script>
	var some_value_changed = false;
	function something_changed()
	{
		some_value_changed = true;
	}
	</script>
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3791); ?> "<?php echo translate_str_by_id($doc_record["caption"]); ?>"
			</div>
			<div class="panel-body">
				
				
				
				<?php
				if( isset( $DP_Config->wholesaler ) )
				{
					//Перезаполняем parameters_values пустыми значениями
					$parameters_values_empty = array();
					for($i=0; $i<count($parameters_description); $i++)
					{
						$parameters_values_empty[$parameters_description[$i]["name"]] = '';
						
						//Если это особый тип - user_profile
						if( $parameters_description[$i]["type"] == 'user_profile_json_builder' )
						{
							$value = array();
							$reg_variants_query = $db_link->prepare("SELECT * FROM `reg_variants`;");
							$reg_variants_query->execute();
							while( $reg_variant = $reg_variants_query->fetch() )
							{
								$value['reg_variant_'.$reg_variant['id']] = array();
							}
							$parameters_values_empty[$parameters_description[$i]["name"]] = $value;
						}
						
					}
					$get_from_first = false;
					
					
					//Получаем список магазинов, к которым имеет доступ данный пользователь
					$offices = array();
					$offices_query = $db_link->prepare('SELECT *, (SELECT `parameters_values` FROM `shop_print_docs_wholesaler` WHERE `office_id` = `shop_offices`.`id` AND `doc_name` = (SELECT `name` FROM `shop_print_docs` WHERE `id` = ?) ) AS `parameters_values` FROM `shop_offices`');
					$offices_query->execute( array($_GET["print_doc_id"]) );
					while( $office = $offices_query->fetch() )
					{
						$managers = json_decode($office['users'], true);
						
						
						//Если для данного магазина еще не было записи для данного документа, ее необходимо создать с пустыми значениями
						if( $office["parameters_values"] == null )
						{
							$db_link->prepare("INSERT INTO `shop_print_docs_wholesaler` (`doc_name`, `office_id`, `parameters_values`) VALUES (?,?,?);")->execute( array($doc_record['name'], $office['id'], json_encode($parameters_values_empty) ) );
						}
						
						
						//Если данный пользователь имеет доступ к этому магазину
						if( array_search( DP_User::getAdminId(), $managers ) !== false )
						{
							//Массив для селекта
							$offices['office_'.$office['id']] = $office;
							
							//Для первого магазина берем настройки
							if( ! $get_from_first )
							{
								//Текущие настройки
								$parameters_values = json_decode($office["parameters_values"], true);
								$get_from_first = true;
							}
							
							//Если был задан аргумент office_id, т.е. пользователь настраивает документ для конкретного магазина, то, берем его текущие настройки
							if( isset( $_GET['office_id'] ) )
							{
								if( $_GET['office_id'] == $office['id'] )
								{
									$parameters_values = json_decode($office["parameters_values"], true);
								}
							}
						}
					}
					if( !$parameters_values )
					{
						$parameters_values = $parameters_values_empty;
					}
					?>
					
					<div class="form-group">
						<label for="office_id" class="col-lg-6 control-label"><?php echo translate_str_by_id(3792); ?></label>
						<div class="col-lg-6">
							<select id="office_id" name="office_id" class="form-control" onchange="select_office();">
								<?php
								foreach( $offices AS $key => $office )
								{
									?>
									<option value="<?php echo $office['id']; ?>"><?php echo $office['caption'].", ".$office['city'].", ".$office['address']." (ID ".$office['id'].")"; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<script>
					// ---------------------------------------------------------------------------------------------
					function select_office()
					{
						if( some_value_changed )
						{
							if( !confirm("<?php echo translate_str_by_id(3793); ?>") )
							{
								document.getElementById('office_id').value = office_id_current;
								return;
							}
						}
						
						
						var office_id = document.getElementById('office_id').value;
						
						location = '/<?php echo $DP_Config->backend_dir; ?>/shop/modul-pechati-dokumentov/nastrojka-pechati-dokumenta?print_doc_id=<?php echo $_GET["print_doc_id"]; ?>&office_id='+office_id;
					}
					// ---------------------------------------------------------------------------------------------
					</script>
					<?php
					if( isset($_GET['office_id']) )
					{
						?>
						<script>
						document.getElementById('office_id').value = '<?php echo $_GET['office_id']; ?>';
						</script>
						<?php
					}
				}
				
				
				
				
				for($i=0; $i<count($parameters_description); $i++)
				{
					$value = $parameters_values[$parameters_description[$i]["name"]];
					//Обработка значения для типа "Файл изображения"
					if( $parameters_description[$i]["type"] == "image_file" )
					{
						if($value != "")
						{
							$value = "/content/files/images/".$value;
						}
					}
					//Обработка значения для типа "Выбор полей профиля пользователя"
					if( $parameters_description[$i]["type"] == "user_profile_json_builder" )
					{
						//$value = array( "reg_variant_1" => array( "name", "surname"), "reg_variant_2" => array( "name", "surname", "company_name"), "reg_variant_3" => array( "name", "company_name") );
					}
					
					
					$widget = get_widget($parameters_description[$i]["type"], $parameters_description[$i]["name"], $value, '');
					
					if($i > 0)
					{
						?>
						<div class="hr-line-dashed col-lg-12"></div>
						<?php
					}
					?>
					<div class="form-group">
						<label for="<?php echo $parameters_description[$i]["name"]; ?>" class="col-lg-6 control-label"><?php echo $parameters_description[$i]["caption"]; ?> 
						<?php
						if( isset($parameters_description[$i]["hint"]) )
						{
							if( $parameters_description[$i]["hint"] != "" )
							{
								?>
								<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo htmlentities($parameters_description[$i]["hint"], ENT_QUOTES, "UTF-8"); ?>');"><i class="fa fa-info"></i></button>
								<?php
							}
						}
						?>
						</label>
						<div class="col-lg-6">
							<?php echo str_replace('id=', ' onkeyup="something_changed();" onchange="something_changed();" id=', $widget); ?>
						</div>
					</div>
					<?php
				}//for()
				?>
				
			</div>
		</div>
	</div>
	</form>
	
	
	<!-- Действия после загрузки страницы -->
	<script>
	some_value_changed = false;
	var office_id_current = document.getElementById('office_id').value;
	</script>
	<?php
}
?>