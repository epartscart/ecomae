<?php
//Страничный скрипт для настройки одного уведомления
defined('_ASTEXE_') or die('No access');


/*
Мультиязычность.
Для данной функции принцип редактирования проще, чем стандартный механизм с кастомными строками.
Здесь пользователь может редактировать исходные строки. При необходимости, он может откатывать их значения к default. При этом, id строк, записанные в таблицу не меняются никогда.

Это обусловлено тем, что уведомления являются предопределенными сразу в исходной версии и пользователь сам новые уведомления не может создавать. А доступность редактирования исходных строк вместо создания кастомных допустима, т.е. здесь есть функция отката к заводским настройкам.

Когда откатываем к заводским настройкам, то, из дефолтных строк берем значения и записываем их в поля на странице. В этот момент никакие действия со строками в БД не производятся. Затем, когда пользователь нажмет сохранить, то дефолтные значения запишутся в переводы строк, которые указаны в БД для соответствующего уведомления.
Т.е. в уведомлениях НИКОГДА не меняются id строк на кастомные или какие-другие. Переводы дефолтных строк никогда не меняются (такой функции нет). Переводы исходных строк могут редактироваться пользователем как угодно.
*/



//Если есть действия
if( isset($_POST['action']) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Делаем через транзакцию, т.е. могут потребоваться несколько запросов.
	try
	{
		//Старт транзакции
		if( ! $db_link->beginTransaction()  )
		{
			throw new Exception(translate_str_by_id(2132));
		}
		
		//Выполняем действия
		if( $_POST['action'] != 'save' )
		{
			throw new Exception("Incorrect parameter");
		}
		
		
		$notification_id = $_POST["notification_id"];
		
		
		//Получаем запись уведомления
		$notification_query = $db_link->prepare("SELECT * FROM `notifications_settings` WHERE `id` = ?;");
		$notification_query->execute( array($notification_id) );
		$notification = $notification_query->fetch();
		
		if( $notification == false)
		{
			throw new Exception(translate_str_by_id(2451));
		}
		
		
		/*
		Что может настраивать пользователь:
		- заголовок письма
		- текст письма
		- текст SMS

		- вкл/выкл E-mail
		- вкл/выкл SMS
		
		При этом данные настройки для E-mail и для Телефона можно делать только если у данного уведомления выставлен флаг foreseen
		*/
		
		
		//Для E-mail
		if( $notification['foreseen_email'] == 1 )
		{
			$email_on = (int)isset($_POST['email_on']);
			
			//Здесь нужно записать переводы в строки мультиязычности
			
			//Сохраняем заголовок письма
			if( $notification['email_subject'] != save_custom_translation($notification['email_subject'], $_POST['email_subject'], null, true) )
			{
				throw new Exception('Error saving Email subject');
			}
			//Сохраняем текст письма
			if( $notification['email_body'] != save_custom_translation($notification['email_body'], $_POST['email_body'], null, true) )
			{
				throw new Exception('Error saving Email body');
			}
		}
		else
		{
			$email_on = 0;
		}
		
		
		//Для телефона
		if( $notification['foreseen_sms'] == 1 )
		{
			$sms_on = (int)isset($_POST['sms_on']);
			
			//Здесь нужно записать переводы в строки мультиязычности
			
			//Сохраняем текст sms
			if( $notification['sms_body'] != save_custom_translation($notification['sms_body'], $_POST['sms_body'], null, true) )
			{
				throw new Exception('Error saving SMS body');
			}
		}
		else
		{
			$sms_on = 0;
		}
		
		
		//Здесь только остается записать настройки "Отправлять на Email" и "Отправлять по SMS"
		if( !$db_link->prepare("UPDATE `notifications_settings` SET `email_on` = ?, `sms_on` = ? WHERE `id` = ?;")->execute( array($email_on, $sms_on, $notification_id) ) )
		{
			throw new Exception(translate_str_by_id(2448));
		}
	}
	catch (Exception $e)
	{
		//Откатываем все изменения
		$db_link->rollBack();
		
		//Можно получить текст ошибки из throw: $e->getMessage()
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings/notification?notification_id=<?php echo $notification_id; ?>&error_message=<?php echo urlencode($e->getMessage()); ?>";
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
		location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings/notification?notification_id=<?php echo $notification_id; ?>&success_message=<?php echo urlencode($success_message); ?>";
	</script>
	<?php
	exit;
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

				<?php
				//Сохранить
				print_backend_button( array("background_color"=>"#63ce1c", "fontawesome_class"=>"fas fa-save", "caption"=>translate_str_by_id(2114), "onclick"=>"document.forms['save_notification_form'].submit();", "url"=>"javascript:void(0);") );
				?>
				
				
				<?php
				//Кнопка восстановления настроек по-умолчанию
				print_backend_button( array("background_color"=>"#8e44ad", "fontawesome_class"=>"fas fa-undo", "caption"=>translate_str_by_id(2449), "url"=>"javascript:void(0);", "onclick"=>"set_default();") );
				?>
				
				
				<?php
				//Обратно к уведомлениям
				print_backend_button( array("background_color"=>"#e74c3c", "fontawesome_class"=>"fas fa-envelope-open-text", "caption"=>translate_str_by_id(2450), "url"=>$DP_Config->domain_path.$DP_Config->backend_dir."/control/notifications_settings") );
				?>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
				
				
			</div>
		</div>
	</div>
	
	
	<?php
	$notification_query = $db_link->prepare('SELECT * FROM `notifications_settings` WHERE `id` = ?;');
	$notification_query->execute( array($_GET['notification_id']) );
	$notification = $notification_query->fetch();
	if( $notification == false )
	{
		//Переадресация с сообщением о результатах выполнения
		$warning_message = translate_str_by_id(2451);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/notifications_settings?warning_message=<?php echo urlencode($warning_message); ?>";
		</script>
		<?php
		exit;
	}
	
	//Переводим строки:
	$notification['caption'] = translate_str_by_id($notification['caption']);
	$notification['description'] = translate_str_by_id($notification['description']);
	$notification['event'] = translate_str_by_id($notification['event']);
	$notification['email_subject'] = translate_str_by_id($notification['email_subject']);
	$notification['email_body'] = translate_str_by_id($notification['email_body']);
	$notification['sms_body'] = translate_str_by_id($notification['sms_body']);
	?>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel collapsed">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(2452); ?> "<?php echo $notification["caption"]; ?>"
			</div>
			<div class="panel-body">
				
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						ID
					</label>
					<div class="col-lg-9">
						<?php echo $notification["id"]; ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2453); ?>
					</label>
					<div class="col-lg-9">
						<?php echo $notification["name"]; ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2277); ?>
					</label>
					<div class="col-lg-9">
						<?php echo $notification["caption"]; ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2073); ?>
					</label>
					<div class="col-lg-9">
						<?php echo $notification["description"]; ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2454); ?>
					</label>
					<div class="col-lg-9">
						<?php echo $notification["event"]; ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2455); ?>
					</label>
					<div class="col-lg-9">
						<?php
						if( $notification["send_for_not_confirmed"] == 1 )
						{
							echo translate_str_by_id(2456);
						}
						else
						{
							echo translate_str_by_id(2457);
						}
						?>
					</div>
				</div>
				
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2458); ?>
					</label>
					<div class="col-lg-9">
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead>
								<tr>
									<th><?php echo translate_str_by_id(2459); ?></th>
									<th><?php echo translate_str_by_id(2460); ?></th>
								</tr>
							</thead>
							<tbody>
								<?php
								$notification_vars = json_decode($notification["vars"], true);
								for( $i=0 ; $i < count($notification_vars) ; $i++ )
								{
									?>
									<tr>
										<td><?php echo translate_str_by_id($notification_vars[$i]['caption']); ?></td>
										<td>%<?php echo $notification_vars[$i]['name']; ?>%</td>
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
	</div>
	
	
	
	<form method="POST" name="save_notification_form">
	<input type="hidden" name="action" value="save" />
	<input type="hidden" name="notification_id" value="<?php echo $_GET['notification_id']; ?>" />
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2461); ?>
			</div>
			<div class="panel-body">
				<?php
				if( $notification['foreseen_email'] == 1 )
				{
					?>
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							<?php echo translate_str_by_id(2462); ?>
						</label>
						<div class="col-lg-9">
							<?php
							$checked = '';
							if( $notification['email_on'] == 1 )
							{
								$checked = ' checked="checked" ';
							}
							?>
							<input class="form-control" type="checkbox" name="email_on" id="email_on" <?php echo $checked; ?> />
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							<?php echo translate_str_by_id(2463); ?>
						</label>
						<div class="col-lg-9">
							<input class="form-control" type="text" name="email_subject" id="email_subject" value="<?php echo $notification['email_subject']; ?>" placeholder="<?php echo translate_str_by_id(2464); ?>" />
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<div class="col-lg-12">
							<label for="" class="control-label"><?php echo translate_str_by_id(2465); ?></label>
							<div id="email_body_div"></div>
							<script>
							// --------------------------------------------------------------------------------
							//Инициализация редактора
							function init_TinyMCE()
							{
								var email_body_div = document.getElementById("email_body_div");
								

								email_body_div.innerHTML = "<textarea style=\"min-height:400px\" class=\"tinymce_editor\" id=\"email_body\" name=\"email_body\"></textarea>";
								tinymce.init({
									selector: "textarea.tinymce_editor",
									toolbar: "bold italic | fontselect | fontsizeselect | styleselect | forecolor | backcolor",
									plugins: [
										"code fullscreen textcolor"
									],
								});
								
								
								<?php
								$email_body = addcslashes(str_replace(array("\n","\r"), '', $notification['email_body']), "'");
								$email_body = str_replace("/", "\/", $email_body);
								?>
								
								
								//Заполняем текущее содержимое:
								document.getElementById("email_body").value = '<?php echo $email_body; ?>';
							}//~function init_TinyMCE()
							// --------------------------------------------------------------------------------
							init_TinyMCE();
							</script>
							
						</div>
					</div>
					
					
					
					
					
					<?php
				}
				else
				{
					?>
					<?php echo translate_str_by_id(2466); ?>
					<?php
				}
				?>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2467); ?>
			</div>
			<div class="panel-body">
				<?php
				if( $notification['foreseen_sms'] == 1 )
				{
					?>
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							<?php echo translate_str_by_id(2468); ?>
						</label>
						<div class="col-lg-9">
							<?php
							$checked = '';
							if( $notification['sms_on'] == 1 )
							{
								$checked = ' checked="checked" ';
							}
							?>
							<input class="form-control" type="checkbox" name="sms_on" id="sms_on" <?php echo $checked; ?> />
						</div>
					</div>
					
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							<?php echo translate_str_by_id(2469); ?>
						</label>
						<div class="col-lg-9">
							
							<textarea class="form-control" name="sms_body" id="sms_body" placeholder="<?php echo translate_str_by_id(2470); ?>"><?php echo $notification["sms_body"]; ?></textarea>
							
						</div>
					</div>
					
					<?php
				}
				else
				{
					?>
					<?php echo translate_str_by_id(2471); ?>
					<?php
				}
				?>
			</div>
		</div>
	</div>
	
	
	</form>
	
	<script>
	// -------------------------------------------------------------------------------------------
	//Восстановление настроек по умолчанию
	function set_default()
	{
		<?php
		if( $notification['foreseen_email'] == 1 )
		{
			?>
			document.getElementById('email_on').checked = true;
			
			document.getElementById('email_subject').value = '<?php echo translate_str_by_id($notification['default_email_subject']); ?>';
			

			<?php
			$default_email_body = addcslashes(str_replace(array("\n","\r"), '', translate_str_by_id($notification['default_email_body'])), "'");
			$default_email_body = str_replace("/", "\/", $default_email_body);
			?>
			
			tinymce.get("email_body").setContent('<?php echo $default_email_body; ?>');
			<?php
		}
		
		if( $notification['foreseen_sms'] == 1 )
		{
			?>
			document.getElementById('sms_on').checked = true;
			
			document.getElementById('sms_body').value = '<?php echo translate_str_by_id($notification['default_sms_body']); ?>';
			<?php
		}
		?>
		
		alert('<?php echo translate_str_by_id(2472); ?>');
	}
	// -------------------------------------------------------------------------------------------
	</script>
	
	<?php
}
?>