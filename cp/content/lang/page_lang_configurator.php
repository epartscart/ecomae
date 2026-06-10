<?php
//Страничный скрипт - Конфигуратор языков
defined('_ASTEXE_') or die('No access');


//Редактор конфига
require_once( $_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/control/dp_configeditor.php');



if( isset( $_POST["action"] ) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Сохраняем через транзакцию
	try
	{
		
		//Старт транзакции
		if( ! $db_link->beginTransaction()  )
		{
			throw new Exception(translate_str_by_id(2132));
		}
		
		//Должны быть все аргументы
		if( !isset( $_POST['multilang_on'] ) || !isset( $_POST['langs_active'] ) || !isset( $_POST['lang_default'] ) )
		{
			throw new Exception("Too few arguments");
		}
		
		//Проверяем значение multilang_on
		if( $_POST['multilang_on'] != 1 && $_POST['multilang_on'] != 0 )
		{
			throw new Exception("Incorrect value 1");
		}
		
		
		//Проверяем язык "по умолчанию". Он должен быть из списка
		$check_lang_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?;');
		$check_lang_query->execute( array($_POST['lang_default']) );
		if( $check_lang_query->fetchColumn() != 1 )
		{
			throw new Exception("Incorrect value 2");
		}
		
		
		
		if( $_POST['multilang_on'] == 1 )
		{
			//Мультиязычность включена
			
			//Проверяем еще langs_active. Это должен быть не пустой массив. В массиве должен присутствовать язык "по умолчанию". Все языки должны быть из списка
			$langs_active = json_decode( $_POST['langs_active'], true );
			if( !is_array( $langs_active ) || count($langs_active) == 0 || array_search($_POST['lang_default'], $langs_active ) ===false )
			{
				throw new Exception("Incorrect value 3.1");
			}
			//Все в массиве должны быть из списка языков
			for( $i = 0 ; $i < count($langs_active) ; $i++ )
			{
				$check_lang_query->execute( array( $langs_active[$i] ) );
				if( $check_lang_query->fetchColumn() != 1 )
				{
					throw new Exception("Incorrect value 3.1");
				}
			}
			
			
			//Все проверки пройдены, пишем.
			
			//Активные языки
			//Сначала все снимаем
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `active` = ?;')->execute( array(0) ) )
			{
				throw new Exception("UPDATE 1");
			}
			//Устанавливаем активные
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `active` = ? WHERE `lang_code` IN ( ? '.str_repeat( ', ?' , count($langs_active)-1 ).' );')->execute( array_merge( array(1) , $langs_active ) ) )
			{
				throw new Exception("UPDATE 2");
			}
			
			//Язык "по умолчанию"
			//Сначала все снимаем
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `is_default` = ?;')->execute( array(0) ) )
			{
				throw new Exception("UPDATE 3");
			}
			//Устанавливаем
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `is_default` = ? WHERE `lang_code` = ?;')->execute( array(1, $_POST['lang_default']) ) )
			{
				throw new Exception("UPDATE 4");
			}
			
			
			//Режим мультиязычности редактируем в config.php
			DP_ConfigEditor::setParameter( 'multilang' , filter_var(true, FILTER_VALIDATE_BOOLEAN));
			sleep(5);//Для тех конфигураций сервера, где настроен кеш
			//Перепроверка, сохранилось ли значение
			//Аргументы запроса
			$postdata_array = array("key"=>$DP_Config->tech_key );
			$postdata = http_build_query($postdata_array);
			$curl = curl_init();
			curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path.$DP_Config->backend_dir.'/content/lang/ajax_get_multilang_value.php');
			curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
			curl_setopt($curl, CURLOPT_POST, true);
			curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
			curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
			curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
			curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
			$curl_result = curl_exec($curl);
			curl_close($curl);
			//Если не равно только что выставленному значению, значит в файл config.php настройка не записалась. Тогда все изменения БД тоже откатываем
			if( $curl_result != 'ON' )
			{
				throw new Exception("Config write 1. ".$curl_result);
			}
		}
		else
		{
			//Мультиязычность выключена
			
			//Массив langs_active проверять не надо. Остальные проверки пройдены. Пишем.
			
			
			//Активный язык
			//Сначала все снимаем
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `active` = ?;')->execute( array(0) ) )
			{
				throw new Exception("UPDATE 1");
			}
			//Устанавливаем активный язык равный "по умолчанию"
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `active` = ? WHERE `lang_code` = ?;')->execute( array(1, $_POST['lang_default'] ) ) )
			{
				throw new Exception("UPDATE 2");
			}
			
			
			//Язык "по умолчанию"
			//Сначала все снимаем
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `is_default` = ?;')->execute( array(0) ) )
			{
				throw new Exception("UPDATE 3");
			}
			//Устанавливаем
			if( ! $db_link->prepare('UPDATE `lang_languages` SET `is_default` = ? WHERE `lang_code` = ?;')->execute( array(1, $_POST['lang_default']) ) )
			{
				throw new Exception("UPDATE 4");
			}
			
			
			//Режим мультиязычности редактируем в config.php
			DP_ConfigEditor::setParameter( 'multilang' , filter_var(false, FILTER_VALIDATE_BOOLEAN));
			sleep(5);//Для тех конфигураций сервера, где настроен кеш
			//Перепроверка, сохранилось ли значение
			//Аргументы запроса
			$postdata_array = array("key"=>$DP_Config->tech_key );
			$postdata = http_build_query($postdata_array);
			$curl = curl_init();
			curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path.$DP_Config->backend_dir.'/content/lang/ajax_get_multilang_value.php');
			curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
			curl_setopt($curl, CURLOPT_POST, true);
			curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
			curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
			curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
			curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
			$curl_result = curl_exec($curl);
			curl_close($curl);
			//Если не равно только что выставленному значению, значит в файл config.php настройка не записалась. Тогда все изменения БД тоже откатываем
			if( $curl_result != 'OFF' )
			{
				throw new Exception("Config write 0. ".$curl_result);
			}
		}
	}
	catch (Exception $e)
	{
		//Откатываем все изменения
		$db_link->rollBack();
		?>
		<script>
		location = "/<?php echo $DP_Config->backend_dir; ?>/lang/configurator?error_message=<?php echo urlencode(translate_str_by_id(2122).'. '.$e->getMessage().'. '.translate_str_by_id(2526).'.'); ?>";
		</script>
		<?php
		exit();
	}

	//Дошли до сюда, значит выполнено ОК
	$db_link->commit();//Коммитим все изменения и закрываем транзакцию
	?>
	<script>
	location = "/<?php echo $DP_Config->backend_dir; ?>/lang/configurator?success_message=<?php echo urlencode(translate_str_by_id(2527)); ?>";
	</script>
	<?php
	exit();
}
else//Действий нет - выводим страницу
{
	?>
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				
				
				<?php
				//Сохранить конфигурацию мультиязычности
				print_backend_button( array( 'onclick'=>'save_lang_configuration();' , 'background_color'=>'#62cb31', 'url'=>'javascript:void(0);', 'fontawesome_class'=>'fa fa-save', 'caption'=>translate_str_by_id(2114) ) );
				?>
				
				
				
				<?php
				//Редактор переводов строк
				print_backend_button( array("background_color"=>"#00b05a", "fontawesome_class"=>"fas fa-pencil-alt", "caption"=>translate_str_by_id(2528), "url"=>"/".$DP_Config->backend_dir."/lang/editor") );
				?>
				
				
				<?php
				//Корневой раздел настройки языков
				print_backend_button( array("background_color"=>"#00b05a", "fontawesome_class"=>"fas fa-language", "caption"=>translate_str_by_id(2529), "url"=>"/".$DP_Config->backend_dir."/lang") );
				?>
				
				
				
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
				<?php echo translate_str_by_id(2530); ?>
			</div>
			<div class="panel-body">
				
				<div class="row">
					<div class="form-group col-lg-4">
						<label for="multilang_on" class="col-lg-4 control-label" style="text-align:left;">
							<?php echo translate_str_by_id(2531); ?>
						</label>
						<div class="col-lg-2" style="text-align:left;">
							<?php
							$checked = '';
							if( $DP_Config->multilang )
							{
								$checked = ' checked="checked" ';
							}
							?>
							<input onchange="on_multilang_on_checked();" type="checkbox" name="multilang_on" id="multilang_on" class="form-control" <?php echo $checked; ?> />
						</div>
					</div>
				</div>
				
				
				
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr>
								<th style="width:50px;line-height:30px;">ID</th>
								<th style="width:200px;line-height:30px;"><?php echo translate_str_by_id(2532); ?></th>
								<th style="width:100px;line-height:30px;"><?php echo translate_str_by_id(2533); ?></th>
								<th style="text-align:left;line-height:30px;" id="active_col_caption"><input type="checkbox" id="check_uncheck_all" onchange="check_uncheck_all();" /> <?php echo translate_str_by_id(2534); ?></th>
								<th style="text-align:left;line-height:30px;" id="is_default_col_caption"><?php echo translate_str_by_id(2535); ?></th>
							</tr>
						</thead>
						<tbody>
							
							<?php
							$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
							$languages_query->execute();
							while( $language = $languages_query->fetch() )
							{
								?>
								
								<tr>
									<td><?php echo $language['id']; ?></td>
									<td><?php echo translate_str_by_id($language['caption_str_key']); ?></td>
									<td><?php echo $language['lang_code']; ?></td>
									<td style="text-align:left;" class="active_col_td">
										<?php
										$checked = '';
										if( $language['active'] )
										{
											$checked = ' checked="checked" ';
										}
										?>
										<input class="active_langs_checkbox" type="checkbox" name="active_<?php echo $language['lang_code']; ?>" id="active_<?php echo $language['lang_code']; ?>" <?php echo $checked; ?> />
									</td>
									<td style="text-align:left;">
										<?php
										$checked = '';
										if( $language['is_default'] )
										{
											$checked = ' checked="checked" ';
										}
										?>
										<input type="radio" name="is_default" value="<?php echo $language['lang_code']; ?>" <?php echo $checked; ?> />
									</td>
								</tr>
								
								<?php
							}
							?>

						</tbody>
					</table>
				</div>
				
				
			</div>
		</div>
	</div>
	
	
	
	
	
	<script>
	// -----------------------------------------------------------------------------------------------
	//Обработка чекбокса вкл/выкл мультиязычности. Для удобства пользователя, обрабатываем виджеты
	function on_multilang_on_checked()
	{
		//Включен ли режим мультиязычности
		var multilang_on = document.getElementById('multilang_on').checked;
		
		//Если мультиязычность ON
		if( multilang_on )
		{
			//Колонку "По умолчанию" переименовываем обратно в "По умолчанию"
			document.getElementById('is_default_col_caption').innerHTML = '<?php echo translate_str_by_id(2535); ?>';
			
			//Колонку "Активный" показываем
			$('#active_col_caption').show();
			$('.active_col_td').show();
		}
		else
		{
			//Мультиязычность OFF
			
			//Колонку "По умолчанию" переименовываем в "Активный"
			document.getElementById('is_default_col_caption').innerHTML = '<?php echo translate_str_by_id(2534); ?>';
			
			
			//Настоящую колонку "Активный" скрываем
			$('#active_col_caption').hide();
			$('.active_col_td').hide();
		}
	}
	// -----------------------------------------------------------------------------------------------
	//Снять/установить все галки "Активен"
	function check_uncheck_all()
	{
		if( document.getElementById('check_uncheck_all').checked )
		{
			$('.active_langs_checkbox').prop('checked', true);
		}
		else
		{
			$('.active_langs_checkbox').prop('checked', false);
		}
	}
	// -----------------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	<form method="POST" name="lang_configuration_form" style="display:none;">
		
		<input type="hidden" name="action" value="save" />
		
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
		
		<!-- Мультиязычность включена -->
		<input type="hidden" name="multilang_on" id="multilang_on_form" value="" />
		
		<!-- Список активных языков -->
		<input type="hidden" name="langs_active" id="langs_active_form" value="" />
		
		<!-- Язык по умолчанию -->
		<input type="hidden" name="lang_default" id="lang_default_form" value="" />
		
	</form>
	<script>
	//Массив с кодами языков
	var langs = new Array();
	<?php
	$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
	$languages_query->execute();
	while( $language = $languages_query->fetch() )
	{
		?>
		langs.push( '<?php echo $language['lang_code']; ?>' );
		<?php
	}
	?>
	
	
	//Отправка формы
	function save_lang_configuration()
	{		
		//Включен ли режим мультиязычности
		var multilang_on = document.getElementById('multilang_on').checked;
		
		//Если мультиязычность включена
		if( multilang_on )
		{
			//Мультиязычность включена. Языков может быть включено от 1 до всех. При этом язык "По умолчанию" должен быть включен
			
			//Получаем включенные языки
			var langs_active = new Array();
			for( var i = 0 ; i < langs.length ; i++)
			{
				if( document.getElementById( 'active_' + langs[i] ).checked )
				{
					langs_active.push(langs[i]);
				}
			}
			if( langs_active.length == 0 )
			{
				alert('<?php echo translate_str_by_id(2536); ?>');
				return;
			}
			
			//Есть активные языки. Проверяем, чтобы язык "по умолчанию" был среди них
			var default_lang = $('input[name="is_default"]:checked').val();//Язык по умолчанию
			if( ! document.getElementById( 'active_' + default_lang ).checked )
			{
				alert('<?php echo translate_str_by_id(2537); ?>');
				return;
			}
			
			
			//Для формы
			document.getElementById('multilang_on_form').value = 1;//Мультиязычность включена
			document.getElementById('langs_active_form').value = JSON.stringify(langs_active);//Список включенных языков
			document.getElementById('lang_default_form').value = default_lang;//Язык по умолчанию
		}
		else
		{
			//Мультиязычность вЫключена. На активные языки не обращаем внимание. Берем только язык по умолчанию. Он и будет единственным включен на уровне сервера.
			//Обработки никакие не делаем, т.к. пользователю доступна только радио-кнопка
			
			var default_lang = $('input[name="is_default"]:checked').val();//Язык по умолчанию
			
			
			//Для формы
			document.getElementById('multilang_on_form').value = 0;//Мультиязычность вЫключена
			document.getElementById('langs_active_form').value = '[]';//Список включенных языков
			document.getElementById('lang_default_form').value = default_lang;//Язык по умолчанию
		}
		
		
		//Теперь записываем значения в поля формы и отправляем
		document.forms['lang_configuration_form'].submit();
	}
	</script>
	
	
	
	
	<script>
	//Обработка чекбока вкл/выкл мультиязычность
	on_multilang_on_checked();
	</script>
	
	<?php
}
?>