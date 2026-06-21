<?php
//Скрипт для страницы "Настройка способов связи"
defined('_ASTEXE_') or die('No access');



if( isset($_POST["action"]) )
{
	
}
else//Действий нет, выводим страницу
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
				//Настройка SMS
				print_backend_button( array("background_color"=>"#e74c3c", "fontawesome_class"=>"fas fa-mobile-alt", "caption"=>translate_str_by_id(2393), "url"=>"/".$DP_Config->backend_dir."/control/sms-operatory", ) );
				?>
				
				
				<?php
				//Настройка E-mail
				print_backend_button( array("background_color"=>"#33cc33", "fontawesome_class"=>"far fa-envelope", "caption"=>translate_str_by_id(2394), "url"=>"/".$DP_Config->backend_dir."/control/config?need_config_group=3") );
				?>
				
				
				<?php
				//Настройка Уведомлений
				print_backend_button( array("background_color"=>"#e74c3c", "fontawesome_class"=>"fas fa-envelope-open-text", "caption"=>translate_str_by_id(2395), "url"=>"/".$DP_Config->backend_dir."/control/notifications_settings") );
				?>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			
			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(2396); ?>
			</div>
			<div class="panel-body">
				
				<p style="font-weight:bold;font-size:1.5em;"><?php echo translate_str_by_id(2397); ?></p>
				
				<p><?php echo translate_str_by_id(2398); ?></p>
				
				<p><?php echo translate_str_by_id(2399); ?></p>
				
				<ul>
					<li><?php echo translate_str_by_id(2400); ?> <a href="https://www.ecomae.com/platform/contact" style="text-decoration:underline;"><?php echo translate_str_by_id(2401); ?></a></li>
					<li><?php echo translate_str_by_id(4596); ?> (01:35:47) <a href="https://www.ecomae.com/platform/contact" style="text-decoration:underline;">Видео-урок "Первоначальная настройка интернет-магазина"</a></li>
					<li><?php echo translate_str_by_id(2402); ?> <a href="https://www.ecomae.com/platform/contact" style="text-decoration:underline;"><?php echo translate_str_by_id(2401); ?></a></li>
				</ul>
				
				<p><?php echo translate_str_by_id(2403); ?></p>

				<p style="font-weight:bold;font-size:1.2em;"><?php echo translate_str_by_id(2404); ?></p>
				<p><?php echo translate_str_by_id(2405); ?></p>

				<p style="font-weight:bold;font-size:1.2em;"><?php echo translate_str_by_id(2406); ?></p>
				<p><?php echo translate_str_by_id(2407); ?></p>

				<p style="font-weight:bold;font-size:1.2em;"><?php echo translate_str_by_id(2408); ?></p>
				<p><?php echo translate_str_by_id(2409); ?></p>
			
			
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2410); ?>
			</div>
			<div class="panel-body">
				
				
				<div class="row">
					<div class="col-md-4">
						<div class="form-group">
							<label class="col-sm-4 control-label"><?php echo translate_str_by_id(2411); ?></label>
							<div class="col-sm-8">
								
								<?php
								$email_settings_pointed = false;//Флаг - настройки E-mail заданы
								
								
								if( !empty($DP_Config->from_name) && !empty($DP_Config->from_email) && !empty($DP_Config->smtp_mode) && !empty($DP_Config->smtp_encryption) && !empty($DP_Config->smtp_host) && !empty($DP_Config->smtp_port) && !empty($DP_Config->smtp_username) && !empty($DP_Config->smtp_password) )
								{
									$email_settings_pointed = true;
									?>
									<i class="fas fa-check-circle" style="color:#0C0;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2412); ?>"></i>
									<?php
								}
								else
								{
									$email_settings_pointed = false;
									?>
									<i class="fas fa-exclamation-triangle" style="color:#FF0000;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2413); ?>"></i>
									<?php
								}
								?>
								
								
							</div>
						</div>
					</div>
					
					<div class="col-md-4">
						<div class="form-group">
							<label class="col-sm-4 control-label"><?php echo translate_str_by_id(2414); ?></label>
							<div class="col-sm-8">
								<?php
								//Результат проверки настроек выводим только если они заданы
								if( $email_settings_pointed )
								{
									$email_debug_query = $db_link->prepare("SELECT * FROM `debug_results` WHERE `name` = ?;");
									$email_debug_query->execute( array('email') );
									$email_debug = $email_debug_query->fetch();
									
									if( $email_debug == false )
									{
										?>
										<i class="far fa-circle" style="color:#AAA;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2415); ?>"></i>
										<?php
									}
									else
									{
										//Коррекно
										if( $email_debug['status'] == 1 )
										{
											//Выводим время, когда была последняя проверка. Считаем, что проверка за последние сутки - новая, от суток до недели - средняя, более недели - старая
											
											if( time() - $email_debug['time'] < 86400 )
											{
												//Новая
												$title="";
												$style="";
											}
											else if( time() - $email_debug['time'] >= 86400 && time() - $email_debug['time'] < 604800 )
											{
												//Средняя
												$title=translate_str_by_id(2416);
												$style="background-color:#f5de1c;color:#000;cursor:pointer;";
											}
											else
											{
												//Старая
												$title=translate_str_by_id(2417);
												$style="background-color:#ff0000;color:#FFF;cursor:pointer;";
											}
											
											?>
											<i class="fas fa-check-circle" style="color:#0C0;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2418); ?>"></i> <?php echo translate_str_by_id(2419); ?> <span title="<?php echo $title; ?>" style="<?php echo $style; ?>"><?php echo date("d.m.Y в H:i:s", $email_debug['time']); ?></span>
											<?php
										}
										else//Не корректно
										{
											?>
											<i class="fas fa-exclamation-triangle" style="color:#C33;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2420); ?>"></i> <?php echo translate_str_by_id(2433); ?> <?php echo date("d.m.Y в H:i:s", $email_debug['time']); ?>
											<br><?php echo translate_str_by_id(2421); ?>: <span style="background-color:#EFEFEF;"><?php echo $email_debug['debug_result']; ?></span>
											<?php
										}
									}
								}
								else
								{
									?>
									<?php echo translate_str_by_id(2422); ?>
									<?php
								}
								?>
							</div>
						</div>
					</div>
					
					
					<div class="col-md-4 text-right">
						<a class="btn w-xs btn-success" href="<?php echo "/".$DP_Config->backend_dir."/control/config?need_config_group=3"; ?>"><i class="far fa-envelope"></i> <?php echo translate_str_by_id(2423); ?></a>

					</div>
					
					
				</div>
				
				
				
				<?php
				//Форму тестирования E-mail выводим только если заданы настройки
				if( $email_settings_pointed )
				{
					?>
					<div class="hr-line-dashed"></div>
				
					<div class="row">
						<div class="col-md-12">
							
							<h5><?php echo translate_str_by_id(2424); ?></h5>
							<p><?php echo translate_str_by_id(2425); ?> "<i class="far fa-envelope"></i> <?php echo translate_str_by_id(2426); ?>"</p>
							
							
							<?php
							//Для автозаполнения адреса получателя тестового письма
							$email_for_test_letter = "";
							$my_admin_profile = DP_User::getAdminProfile();
							if( !empty($my_admin_profile["email"]) )
							{
								$email_for_test_letter = $my_admin_profile["email"];
							}
							else if( !empty($DP_Config->from_email) )
							{
								$email_for_test_letter = $DP_Config->from_email;
							}
							?>
							
							
							<div class="input-group">
								<input type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2427); ?>" value="<?php echo $email_for_test_letter; ?>" id="email_for_test" />
								<span class="input-group-btn">
									<button class="btn btn-primary" onclick="test_email();"><i class="far fa-envelope"></i> <?php echo translate_str_by_id(2426); ?></button> 
								</span>
							</div>
							<script>
							function test_email()
							{
								var email_for_test = document.getElementById('email_for_test').value;
								
								
								if( email_for_test == '' )
								{
									alert('<?php echo translate_str_by_id(2428); ?>');
									return;
								}
								
								//Отправка тестового письма
								jQuery.ajax({
									type: "POST",
									async: false, //Запрос синхронный
									url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/control/communications/ajax_test_notification.php",
									dataType: "text",//Тип возвращаемого значения
									data: "contact="+email_for_test+"&type=email&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
									success: function(answer)
									{
										console.log(answer);
										
										var answer_ob = JSON.parse(answer);
										
										//Если некорректный парсинг ответа
										if( typeof answer_ob.status === "undefined" )
										{
											alert("<?php echo translate_str_by_id(2429); ?>");
										}
										else
										{
											//Корректный парсинг ответа
											if(answer_ob.status == true)
											{
												alert('<?php echo translate_str_by_id(2430); ?>');
											}
											else
											{
												alert(answer_ob.message);
											}
										}
										
										
										location = "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/communications";
									}
								});
								
								
							}
							</script>
						</div>
					</div>
					<?php
				}
				?>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2431); ?>
			</div>
			<div class="panel-body">
				
				
				<div class="row">
					<div class="col-md-4">
						<div class="form-group">
							<label class="col-sm-4 control-label"><?php echo translate_str_by_id(2432); ?></label>
							<div class="col-sm-8">
								
								<?php
								$sms_settings_pointed = false;//Флаг - настройки SMS-оператора заданы
								
								
								
								$check_sms_query = $db_link->prepare("SELECT COUNT(*) FROM `sms_api` WHERE `active` = ?;");
								$check_sms_query->execute( array(1) );
								if( $check_sms_query->fetchColumn() == 1 )
								{
									$sms_settings_pointed = true;
									?>
									<i class="fas fa-check-circle" style="color:#0C0;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2412); ?>"></i>
									<?php
								}
								else
								{
									$sms_settings_pointed = false;
									?>
									<i class="fas fa-exclamation-triangle" style="color:#FF0000;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2413); ?>"></i>
									<?php
								}
								?>
								
								
							</div>
						</div>
					</div>
					
					<div class="col-md-4">
						<div class="form-group">
							<label class="col-sm-4 control-label"><?php echo translate_str_by_id(2414); ?></label>
							<div class="col-sm-8">
								<?php
								//Результат проверки настроек выводим только если они заданы
								if( $sms_settings_pointed )
								{
									$sms_debug_query = $db_link->prepare("SELECT * FROM `debug_results` WHERE `name` = ?;");
									$sms_debug_query->execute( array('sms') );
									$sms_debug = $sms_debug_query->fetch();
									
									if( $sms_debug == false )
									{
										?>
										<i class="far fa-circle" style="color:#AAA;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2415); ?>"></i>
										<?php
									}
									else
									{
										//Коррекно
										if( $sms_debug['status'] == 1 )
										{
											//Выводим время, когда была последняя проверка. Считаем, что проверка за последние сутки - новая, от суток до недели - средняя, более недели - старая
											
											if( time() - $sms_debug['time'] < 86400 )
											{
												//Новая
												$title="";
												$style="";
											}
											else if( time() - $sms_debug['time'] >= 86400 && time() - $sms_debug['time'] < 604800 )
											{
												//Средняя
												$title=translate_str_by_id(2416);
												$style="background-color:#f5de1c;color:#000;cursor:pointer;";
											}
											else
											{
												//Старая
												$title=translate_str_by_id(2417);
												$style="background-color:#ff0000;color:#FFF;cursor:pointer;";
											}
											
											?>
											<i class="fas fa-check-circle" style="color:#0C0;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2418); ?>"></i> <?php echo translate_str_by_id(2419); ?> <span title="<?php echo $title; ?>" style="<?php echo $style; ?>"><?php echo date("d.m.Y в H:i:s", $sms_debug['time']); ?></span>
											<?php
										}
										else//Не корректно
										{
											?>
											<i class="fas fa-exclamation-triangle" style="color:#C33;cursor:pointer;font-size:1.5em;" title="<?php echo translate_str_by_id(2420); ?>"></i> <?php echo translate_str_by_id(2433); ?> <?php echo date("d.m.Y в H:i:s", $sms_debug['time']); ?>
											<br><?php echo translate_str_by_id(2421); ?>: <span style="background-color:#EFEFEF;"><?php echo $sms_debug['debug_result']; ?></span>
											<?php
										}
									}
								}
								else
								{
									?>
									<?php echo translate_str_by_id(2434); ?>
									<?php
								}
								?>
							</div>
						</div>
					</div>
					
					
					<div class="col-md-4 text-right">
						<a class="btn w-xs btn-success" href="/<?php echo $DP_Config->backend_dir; ?>/control/sms-operatory"><i class="fas fa-mobile-alt"></i> <?php echo translate_str_by_id(2435); ?></a>
					</div>
					
					
				</div>
				
				
				
				<?php
				//Форму тестирования SMS выводим только если заданы настройки
				if( $sms_settings_pointed )
				{
					?>
					<div class="hr-line-dashed"></div>
				
					<div class="row">
						<div class="col-md-12">
							
							<h5><?php echo translate_str_by_id(2436); ?></h5>
							<p><?php echo translate_str_by_id(2437); ?> "<i class="fas fa-mobile-alt"></i> <?php echo translate_str_by_id(2426); ?>"</p>
							
							
							<?php
							//Для автозаполнения номера телефона получателя тестового SMS
							$phone_for_test_sms = "";
							$my_admin_profile = DP_User::getAdminProfile();
							if( !empty($my_admin_profile["phone"]) )
							{
								$phone_for_test_sms = $my_admin_profile["phone"];
							}
							?>
							
							
							<div class="input-group">
								<input type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2438); ?>" value="<?php echo $phone_for_test_sms; ?>" id="phone_for_test" />
								<span class="input-group-btn">
									<button class="btn btn-primary" onclick="test_sms();"><i class="fas fa-mobile-alt"></i> <?php echo translate_str_by_id(2426); ?></button> 
								</span>
							</div>
							<script>
							function test_sms()
							{
								var phone_for_test = document.getElementById('phone_for_test').value;
								
								
								if( phone_for_test == '' )
								{
									alert('<?php echo translate_str_by_id(2439); ?>');
									return;
								}
								
								//Отправка тестового SMS
								jQuery.ajax({
									type: "POST",
									async: false, //Запрос синхронный
									url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/control/communications/ajax_test_notification.php",
									dataType: "text",//Тип возвращаемого значения
									data: "contact="+phone_for_test+"&type=phone&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
									success: function(answer)
									{
										console.log(answer);
										
										var answer_ob = JSON.parse(answer);
										
										//Если некорректный парсинг ответа
										if( typeof answer_ob.status === "undefined" )
										{
											alert("<?php echo translate_str_by_id(2429); ?>");
										}
										else
										{
											//Корректный парсинг ответа
											if(answer_ob.status == true)
											{
												alert('<?php echo translate_str_by_id(2440); ?>');
											}
											else
											{
												alert(answer_ob.message);
											}
										}
										
										
										location = "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/communications";
									}
								});
								
								
							}
							</script>
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