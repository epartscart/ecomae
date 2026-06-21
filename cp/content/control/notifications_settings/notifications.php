<?php
//Страничный скрипт отображения таблицы notifications_settings (менеджер уведомлений)
defined('_ASTEXE_') or die('No access');


//Если есть действия
if( isset($_POST['action']) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	//Действие - восстановление настроек по умолчанию
	/*
	Мультиязычность:
	- текст письма, заголовок письма, текст sms: переводы на все языки восстанавливаем из значений по умолчанию
	- оставшиеся настройки делаем в таблице notifications_settings (email_on, sms_on)
	*/
	if( $_POST['action'] == 'set_default' )
	{
		//Массив с ID уведомлений
		$notifications_ids = json_decode($_POST['notifications_ids'], true);
		
		//Делаем через транзакцию
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception( translate_str_by_id(2132) );
			}
			
			//Выполняем действия
			if( !is_array($notifications_ids) )
			{
				throw new Exception( 'Incorrect parameter' );
			}
			
			
			//Получаем список языков сайта
			$langs_query = $db_link->prepare("SELECT * FROM `lang_languages`");
			$langs_query->execute();
			$langs = $langs_query->fetchAll();
			
			
			//По каждому уведомлению
			//Готовим запрос - общий для всех
			$restore_default_str_query = $db_link->prepare("UPDATE `lang_text_strings_translation` SET `value` = (SELECT `value` FROM (SELECT * FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_id` = ?) AS `sub_table` ) WHERE `lang_code` = ? AND `str_id` = ?;");
			for( $i = 0 ; $i < count($notifications_ids) ; $i++ )
			{
				//Получаем объект уведомления
				$notification_query = $db_link->prepare("SELECT * FROM `notifications_settings` WHERE `id` = ?;");
				$notification_query->execute( array($notifications_ids[$i]) );
				$notification = $notification_query->fetch();
				
				if( !$notification )
				{
					throw new Exception( 'Notification not found' );
				}
				
				
				//Для тех уведомлений, которые еще не перенесены в мультиязычность - ничего не делаем
				if( 
				!is_numeric($notification['default_email_subject']) || 
				!is_numeric($notification['default_email_body']) ||
				!is_numeric($notification['default_sms_body']) ||
				!is_numeric($notification['email_subject']) || 
				!is_numeric($notification['email_body']) ||
				!is_numeric($notification['sms_body'])
					)
				{
					continue;
				}
				
				//Здесь знаем id всех строк данного уведомления
				
				
				//Для каждого языка
				for( $lg=0 ; $lg < count($langs) ; $lg++ )
				{
					//Если предусмотрено письмо
					if( $notification['foreseen_email'] == 1 )
					{
						//Откатываем заголовок письма
						if( ! $restore_default_str_query->execute( array( $langs[$lg]['lang_code'], $notification['default_email_subject'], $langs[$lg]['lang_code'], $notification['email_subject'] ) ) )
						{
							throw new Exception( 'Error restoring email_subject' );
						}
						
						//Откатываем текст письма
						if( ! $restore_default_str_query->execute( array( $langs[$lg]['lang_code'], $notification['default_email_body'], $langs[$lg]['lang_code'], $notification['email_body'] ) ) )
						{
							throw new Exception( 'Error restoring email_body' );
						}
					}
					
					//Если предусмотрено sms
					if( $notification['foreseen_sms'] == 1 )
					{
						//Откатываем текст sms
						if( ! $restore_default_str_query->execute( array( $langs[$lg]['lang_code'], $notification['default_sms_body'], $langs[$lg]['lang_code'], $notification['sms_body'] ) ) )
						{
							throw new Exception( 'Error restoring sms_body' );
						}
					}
				}//~for по каждому языку
				
				
				
				//Остается только восстановить настройки email_on, sms_on
				if( ! $db_link->prepare( "UPDATE `notifications_settings` SET `email_on` = `foreseen_email`, `sms_on` = `foreseen_sms` WHERE `id` = ?;" )->execute( array($notifications_ids[$i]) ) )
				{
					throw new Exception( 'Error restoring email_on and sms_on' );
				}
			}//~for по каждому уведомлению
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			
			
			//Можно получить текст ошибки из throw: $e->getMessage()
			
			//Переадресация с сообщением о результатах выполнения
			$error_message = $e->getMessage();
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		
		
		//Переадресация с сообщением о результатах выполнения
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?success_message=<?php echo urlencode($success_message); ?>";
		</script>
		<?php
		exit;
	}
	//Отправлять на email и Отправлять на Телефон
	else if( $_POST['action'] == 'set_send' )
	{
		$type = $_POST['type'];//E-mail или Телефон
		$notification_id = $_POST['notification_id'];
		$set_send = $_POST['set_send'];//Вкл или выкл
		
		
		//$type используется в SQL-запросах. Проверяем значение
		if( $type != 'email' && $type != 'sms' )
		{
			exit;
		}
		
		
		
		//Отключать отправку можно в любом случае.
		//Включать можно только, если предусмотрен соответствующий способ отправки для данного уведомления
		if( $set_send == 1 )
		{
			//Проверяем, предусмотрен ли данный способ отправки по этому уведомлению
			$foreseen_query = $db_link->prepare("SELECT * FROM `notifications_settings` WHERE `id` = ?;");
			$foreseen_query->execute( array($notification_id) );
			$foreseen_record = $foreseen_query->fetch();
			
			if( $foreseen_record['foreseen_'.$type] == 0 )
			{
				//Переадресация с сообщением о результатах выполнения
				$warning_message = translate_str_by_id(2474);
				?>
				<script>
					location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?warning_message=<?php echo urlencode($warning_message); ?>";
				</script>
				<?php
				exit;
			}
		}
		
		
		
		//Включаем/отключаем
		if( !$db_link->prepare("UPDATE `notifications_settings` SET `".$type."_on` = ? WHERE `id` = ?;")->execute( array($set_send, $notification_id) ) )
		{
			//Переадресация с сообщением о результатах выполнения
			$error_message = translate_str_by_id(2473);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			//Переадресация с сообщением о результатах выполнения
			$success_message = translate_str_by_id(2157);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?success_message=<?php echo urlencode($success_message); ?>";
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
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
				
				<?php
				//Кнопка восстановления настроек по-умолчанию
				print_backend_button( array("background_color"=>"#8e44ad", "fontawesome_class"=>"fas fa-undo", "caption"=>translate_str_by_id(2449), "url"=>"javascript:void(0);", "onclick"=>"set_default_checked();") );
				?>
				<form name="set_default_form" method="POST">
					<input type="hidden" name="action" value="set_default" />
					<input type="hidden" name="notifications_ids" id="notifications_ids" value="" />
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
				</form>
				<script>
				// ----------------------------------------------------------------------------------
				//Откат к настройкам по-умолчанию для отмеченных уведомлений
				function set_default_checked()
				{
					var notifications_ids = getCheckedElements();
					
					if( notifications_ids.length == 0 )
					{
						alert("<?php echo translate_str_by_id(2475); ?>");
						return;
					}
					
					if( !confirm("<?php echo translate_str_by_id(2476); ?>") )
					{
						return;
					}
					
					document.getElementById('notifications_ids').value = JSON.stringify(notifications_ids);
					
					document.forms['set_default_form'].submit();
				}
				// ----------------------------------------------------------------------------------
				//Откат к настройкам по-умолчанию для определенного уведомления
				function set_default_one(notification_id)
				{
					if( !confirm("<?php echo translate_str_by_id(2477); ?>") )
					{
						return;
					}
					
					document.getElementById('notifications_ids').value = '['+notification_id+']';
					
					document.forms['set_default_form'].submit();
				}
				// ----------------------------------------------------------------------------------
				</script>
				
				
			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2478); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="footable table table-hover toggle-arrow " data-sort="false">
						<thead> 
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
								<th><?php echo translate_str_by_id(2453); ?></th>
								<th><?php echo translate_str_by_id(2479); ?></th>
								<th><?php echo translate_str_by_id(2073); ?></th>
								<th class="text-center"><?php echo translate_str_by_id(2480); ?></th>
								<th class="text-center"><?php echo translate_str_by_id(2481); ?></th>
								<th class="text-center"><?php echo translate_str_by_id(2113); ?></th>
							</tr>
						</thead>
						<tbody>
							<?php
							//Массивы для JS с id элементов и с чекбоксами элементов
							$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
							$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
							
							
							$elements_query = $db_link->prepare("SELECT * FROM `notifications_settings` ORDER BY `id` ASC;");
							$elements_query->execute();
							while( $element_record = $elements_query->fetch() )
							{
								//Для Javascript
								$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
								$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
								
								
								$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/control/notifications_settings/notification?notification_id=".$element_record["id"]."\">";
								?>
								<tr>
									<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
									<td><?php echo $a_item.$element_record["id"]; ?></a></td>
									<td><?php echo $a_item.translate_str_by_id($element_record["caption"]); ?></a></td>
									<td><?php echo $a_item.$element_record["name"]; ?></a></td>
									<td><?php echo $a_item.translate_str_by_id($element_record["event"]); ?></a></td>
									<td><?php echo $a_item.translate_str_by_id($element_record["description"]); ?></a></td>
									<td class="text-center">
										<form method="POST" name="set_send_email_<?php echo $element_record["id"]; ?>">
											<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
											<input type="hidden" name="action" value="set_send" />
											<input type="hidden" name="type" value="email" />
											<input type="hidden" name="notification_id" value="<?php echo $element_record["id"]; ?>" />
											<?php
											if( $element_record["email_on"] == 1 )
											{
												?>
												<input type="hidden" name="set_send" value="0" />
												<i class="fas fa-check-circle" style="color:#0C0;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2482); ?>" onclick="forms['set_send_email_<?php echo $element_record["id"]; ?>'].submit();"></i>
												<?php
											}
											else
											{
												?>
												<input type="hidden" name="set_send" value="1" />
												<i class="fas fa-minus-circle" style="color:#C33;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2483); ?>" onclick="forms['set_send_email_<?php echo $element_record["id"]; ?>'].submit();"></i>
												<?php
											}
											?>
										</form>
									</td>
									<td class="text-center">
										<form method="POST" name="set_send_sms_<?php echo $element_record["id"]; ?>">
											<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
											<input type="hidden" name="action" value="set_send" />
											<input type="hidden" name="type" value="sms" />
											<input type="hidden" name="notification_id" value="<?php echo $element_record["id"]; ?>" />
											<?php
											if( $element_record["sms_on"] == 1 )
											{
												?>
												<input type="hidden" name="set_send" value="0" />
												<i class="fas fa-check-circle" style="color:#0C0;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2484); ?>" onclick="forms['set_send_sms_<?php echo $element_record["id"]; ?>'].submit();"></i>
												<?php
											}
											else
											{
												?>
												<input type="hidden" name="set_send" value="1" />
												<i class="fas fa-minus-circle" style="color:#C33;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2485); ?>" onclick="forms['set_send_sms_<?php echo $element_record["id"]; ?>'].submit();"></i>
												<?php
											}
											?>
										</form>
									</td>
									<td class="text-center">
										<i class="fas fa-undo" style="color:#8e44ad;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2449); ?>" onclick="set_default_one(<?php echo $element_record["id"]; ?>);"></i>
										
										
										<?php echo $a_item; ?><i class="fas fa-edit" style="color:#3498db;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2486); ?>"></i></a>
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
    </script>
	
	
	<?php
}
?>