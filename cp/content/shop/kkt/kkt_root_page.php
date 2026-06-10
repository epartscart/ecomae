<?php
/*
Описание скрипта
*/
defined('_ASTEXE_') or die('No access');

if( isset( $_POST["action"] ) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	$type 								= (int) $_POST["type"];
	$taxationSystem 					= (int) $_POST["taxationSystem"];
	$kkt_device_id 						= (int) $_POST["kkt_device_id"];
	$check_product_tax 					= (int) $_POST["check_product_tax"];
	$check_product_paymentMethodType 	= (int) $_POST["check_product_paymentMethodType"];
	$check_product_paymentSubjectType 	= (int) $_POST["check_product_paymentSubjectType"];
	$check_payment_type 				= (int) $_POST["check_payment_type"];
	$print 								= (int) $_POST["print"];
	
	$SQL = "UPDATE `shop_kkt_default_setting` SET `taxationSystem`=?,`kkt_device_id`=?,`check_product_tax`=?,`check_product_paymentMethodType`=?,`check_product_paymentSubjectType`=?,`check_payment_type`=?,`print`=? WHERE `type`=?";
	
	if( $db_link->prepare($SQL)->execute( array($taxationSystem, $kkt_device_id, $check_product_tax, $check_product_paymentMethodType, $check_product_paymentSubjectType, $check_payment_type, $print, $type) ) != true)
	{
		$error_message = "Не удалось сохранить настройки";
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/onlajn-kassy?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}else{
		$success_message = "Настройки сохранены";
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/onlajn-kassy?success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Действия
			</div>
			<div class="panel-body">
				
				<?php
				//Ссылка на страницу "Кассовые чеки"
				print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/onlajn-kassy/checks", "background_color"=>"#3C3", "fontawesome_class"=>"fas fa-receipt", "caption"=>"Чеки") );
				
				
				//Ссылка на страницу "Кассовые аппараты"
				print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/onlajn-kassy/devices", "background_color"=>"#8e44ad", "fontawesome_class"=>"fas fa-server", "caption"=>"Устройства") );
				?>
				
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption">Выход</div>
				</a>
			</div>
		</div>
	</div>
	
	

	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Настройки по умолчанию для ручного формирования чека <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('Здесь можно указать параметры чеков по умолчанию. Эти параметры будут выставляться автоматически в окне создания чека при его открытии. Рекомендуется выставить здесь наиболее часто используемые параметры. При необходимости, можно изменять эти параметры в процессе редактирования нового чека.');"><i class="fa fa-info"></i></button>
			</div>
			<div class="panel-body">
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							СНО по чеку
						</label>
						<div class="col-lg-6">
							<select id="taxationSystem" class="form-control">
								<?php
								$taxationSystem_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1055` ORDER BY `value`;");
								$taxationSystem_query->execute();
								while( $taxationSystem = $taxationSystem_query->fetch() )
								{
									?>
									<option value="<?php echo $taxationSystem["value"]; ?>" <?php echo $selected; ?>><?php echo $taxationSystem["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Касса
						</label>
						<div class="col-lg-6">
							<select id="kkt_device_id" class="form-control">
								<?php
								$kkt_device_query = $db_link->prepare("SELECT * FROM `shop_kkt_devices` ORDER BY `name`;");
								$kkt_device_query->execute();
								while( $kkt_device = $kkt_device_query->fetch() )
								{
									?>
									<option value="<?php echo $kkt_device["id"]; ?>" <?php echo $selected; ?>><?php echo translate_str_by_id($kkt_device["name"]); ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Ставка НДС позиции
						</label>
						<div class="col-lg-6">
							<select id="check_product_tax" class="form-control">
								<?php
								$check_product_tax_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1199` ORDER BY `value`;");
								$check_product_tax_query->execute();
								while( $check_product_tax = $check_product_tax_query->fetch() )
								{
									?>
									<option value="<?php echo $check_product_tax["value"]; ?>" <?php echo $selected; ?>><?php echo $check_product_tax["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Признак способа расчета по позиции
						</label>
						<div class="col-lg-6">
							<select id="check_product_paymentMethodType" class="form-control">
								<?php
								$check_product_paymentMethodType_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1214` ORDER BY `value`;");
								$check_product_paymentMethodType_query->execute();
								while( $check_product_paymentMethodType = $check_product_paymentMethodType_query->fetch() )
								{
									?>
									<option value="<?php echo $check_product_paymentMethodType["value"]; ?>" <?php echo $selected; ?>><?php echo $check_product_paymentMethodType["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Признак предмета расчета по позиции
						</label>
						<div class="col-lg-6">
							<select id="check_product_paymentSubjectType" class="form-control">
								<?php
								$check_product_paymentSubjectType_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1212` ORDER BY `value`;");
								$check_product_paymentSubjectType_query->execute();
								while( $check_product_paymentSubjectType = $check_product_paymentSubjectType_query->fetch() )
								{
									?>
									<option value="<?php echo $check_product_paymentSubjectType["value"]; ?>" <?php echo $selected; ?>><?php echo $check_product_paymentSubjectType["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Способ платежа по чеку
						</label>
						<div class="col-lg-6">
							<select id="check_payment_type" class="form-control">
								<?php
								$check_payment_type_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_payment_types_tags` ORDER BY `value`;");
								$check_payment_type_query->execute();
								while( $check_payment_type = $check_payment_type_query->fetch() )
								{
									?>
									<option value="<?php echo $check_payment_type["value"]; ?>" <?php echo $selected; ?>><?php echo $check_payment_type["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Печатать бумажный чек
						</label>
						<div class="col-lg-6">
							<input type="checkbox" id="print" class="form-control"/>
						</div>
					</div>
				</div>
				
			
			</div>
			<div class="panel-footer">
                <button class="btn btn-primary " type="button" onclick="apply_check_default_options();"><i class="fa fa-check"></i> Применить</button>
            </div>
		</div>
	</div>
	
	<form id="form_type_2" method="POST" style="display:none;">
		<input type="hidden" name="action" value="save_setting" />
		<input type="hidden" name="type" value="2" />
		<input type="hidden" name="taxationSystem" id="taxationSystem__form_type_2" value="" />
		<input type="hidden" name="kkt_device_id" id="kkt_device_id__form_type_2" value="" />
		<input type="hidden" name="check_product_tax" id="check_product_tax__form_type_2" value="" />
		<input type="hidden" name="check_product_paymentMethodType" id="check_product_paymentMethodType__form_type_2" value="" />
		<input type="hidden" name="check_product_paymentSubjectType" id="check_product_paymentSubjectType__form_type_2" value="" />
		<input type="hidden" name="check_payment_type" id="check_payment_type__form_type_2" value="" />
		<input type="hidden" name="print" id="print__form_type_2" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	
	<script>
	//Применение настроек чека по умолчанию
	function apply_check_default_options()
	{
		// Записываем настройки в базу
		document.getElementById("taxationSystem__form_type_2").value = document.getElementById("taxationSystem").value;
		document.getElementById("kkt_device_id__form_type_2").value = document.getElementById("kkt_device_id").value;
		document.getElementById("check_product_tax__form_type_2").value = document.getElementById("check_product_tax").value;
		document.getElementById("check_product_paymentMethodType__form_type_2").value = document.getElementById("check_product_paymentMethodType").value;
		document.getElementById("check_product_paymentSubjectType__form_type_2").value = document.getElementById("check_product_paymentSubjectType").value;
		document.getElementById("check_payment_type__form_type_2").value = document.getElementById("check_payment_type").value;
		if(document.getElementById("print").checked){
			document.getElementById("print__form_type_2").value = 1;
		}else{
			document.getElementById("print__form_type_2").value = 0;
		}
		
		document.getElementById("form_type_2").submit();
	}
	</script>
	
	
	
	<?php
	// Выставляем сохраненные настройки по умолчанию
	$shop_kkt_default_setting_query = $db_link->prepare("SELECT * FROM `shop_kkt_default_setting` WHERE `type` = 2 LIMIT 1;");
	$shop_kkt_default_setting_query->execute();
	$default_setting_type_2 = $shop_kkt_default_setting_query->fetch();
	?>
	<script>
		jQuery( window ).load(function() {
			// Выставляем значения в форме
			document.getElementById("taxationSystem").value = '<?=$default_setting_type_2['taxationSystem'];?>';
			document.getElementById("kkt_device_id").value = '<?=$default_setting_type_2['kkt_device_id'];?>';
			document.getElementById("check_product_tax").value = '<?=$default_setting_type_2['check_product_tax'];?>';
			document.getElementById("check_product_paymentMethodType").value = '<?=$default_setting_type_2['check_product_paymentMethodType'];?>';
			document.getElementById("check_product_paymentSubjectType").value = '<?=$default_setting_type_2['check_product_paymentSubjectType'];?>';
			document.getElementById("check_payment_type").value = '<?=$default_setting_type_2['check_payment_type'];?>';
			document.getElementById("print").checked = <?=(int)$default_setting_type_2['print'];?>;
			
			
			// Записываем в cookie - для совместимости функционала со старыми версиями
			var check_default_options = new Object;
			
			
			check_default_options.taxationSystem = document.getElementById("taxationSystem").value;
			check_default_options.kkt_device_id = document.getElementById("kkt_device_id").value;
			check_default_options.check_product_tax = document.getElementById("check_product_tax").value;
			check_default_options.check_product_paymentMethodType = document.getElementById("check_product_paymentMethodType").value;
			check_default_options.check_product_paymentSubjectType = document.getElementById("check_product_paymentSubjectType").value;
			check_default_options.check_payment_type = document.getElementById("check_payment_type").value;
			if(document.getElementById("print").checked){
				check_default_options.print = 1;
			}else{
				check_default_options.print = 0;
			}
			
			
			//Устанавливаем cookie (на полгода)
			var date = new Date(new Date().getTime() + 15552000 * 1000);
			document.cookie = "check_default_options="+JSON.stringify(check_default_options)+"; path=/; expires=" + date.toUTCString();
		});
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Настройки по умолчанию для автоматических чеков после онлайн оплаты <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('Здесь нужно указать параметры чеков онлайн оплаты по умолчанию. Эти параметры будут выставляться автоматически в чеке после онлайн оплаты заказа или поплнения баланса.');"><i class="fa fa-info"></i></button>
			</div>
			<div class="panel-body">
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							СНО по чеку
						</label>
						<div class="col-lg-6">
							<select id="taxationSystem__type_1" class="form-control">
								<?php
								$taxationSystem_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1055` ORDER BY `value`;");
								$taxationSystem_query->execute();
								while( $taxationSystem = $taxationSystem_query->fetch() )
								{
									?>
									<option value="<?php echo $taxationSystem["value"]; ?>" <?php echo $selected; ?>><?php echo $taxationSystem["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Касса
						</label>
						<div class="col-lg-6">
							<select id="kkt_device_id__type_1" class="form-control">
								<?php
								$kkt_device_query = $db_link->prepare("SELECT * FROM `shop_kkt_devices` ORDER BY `name`;");
								$kkt_device_query->execute();
								while( $kkt_device = $kkt_device_query->fetch() )
								{
									?>
									<option value="<?php echo $kkt_device["id"]; ?>" <?php echo $selected; ?>><?php echo translate_str_by_id($kkt_device["name"]); ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Ставка НДС позиции
						</label>
						<div class="col-lg-6">
							<select id="check_product_tax__type_1" class="form-control">
								<?php
								$check_product_tax_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1199` ORDER BY `value`;");
								$check_product_tax_query->execute();
								while( $check_product_tax = $check_product_tax_query->fetch() )
								{
									?>
									<option value="<?php echo $check_product_tax["value"]; ?>" <?php echo $selected; ?>><?php echo $check_product_tax["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Признак способа расчета по позиции
						</label>
						<div class="col-lg-6">
							<select id="check_product_paymentMethodType__type_1" class="form-control">
								<?php
								$check_product_paymentMethodType_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1214` ORDER BY `value`;");
								$check_product_paymentMethodType_query->execute();
								while( $check_product_paymentMethodType = $check_product_paymentMethodType_query->fetch() )
								{
									?>
									<option value="<?php echo $check_product_paymentMethodType["value"]; ?>" <?php echo $selected; ?>><?php echo $check_product_paymentMethodType["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Признак предмета расчета по позиции
						</label>
						<div class="col-lg-6">
							<select id="check_product_paymentSubjectType__type_1" class="form-control">
								<?php
								$check_product_paymentSubjectType_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1212` ORDER BY `value`;");
								$check_product_paymentSubjectType_query->execute();
								while( $check_product_paymentSubjectType = $check_product_paymentSubjectType_query->fetch() )
								{
									?>
									<option value="<?php echo $check_product_paymentSubjectType["value"]; ?>" <?php echo $selected; ?>><?php echo $check_product_paymentSubjectType["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Способ платежа по чеку
						</label>
						<div class="col-lg-6">
							<select id="check_payment_type__type_1" class="form-control">
								<?php
								$check_payment_type_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_payment_types_tags` ORDER BY `value`;");
								$check_payment_type_query->execute();
								while( $check_payment_type = $check_payment_type_query->fetch() )
								{
									?>
									<option value="<?php echo $check_payment_type["value"]; ?>" <?php echo $selected; ?>><?php echo $check_payment_type["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Печатать бумажный чек
						</label>
						<div class="col-lg-6">
							<input type="checkbox" id="print__type_1" class="form-control"/>
						</div>
					</div>
				</div>
				
			
			</div>
			<div class="panel-footer">
                <button class="btn btn-primary " type="button" onclick="apply_check_default_options__type_1();"><i class="fa fa-check"></i> Применить</button>
            </div>
		</div>
	</div>
	
	<form id="form_type_1" method="POST" style="display:none;">
		<input type="hidden" name="action" value="save_setting" />
		<input type="hidden" name="type" value="1" />
		<input type="hidden" name="taxationSystem" id="taxationSystem__form_type_1" value="" />
		<input type="hidden" name="kkt_device_id" id="kkt_device_id__form_type_1" value="" />
		<input type="hidden" name="check_product_tax" id="check_product_tax__form_type_1" value="" />
		<input type="hidden" name="check_product_paymentMethodType" id="check_product_paymentMethodType__form_type_1" value="" />
		<input type="hidden" name="check_product_paymentSubjectType" id="check_product_paymentSubjectType__form_type_1" value="" />
		<input type="hidden" name="check_payment_type" id="check_payment_type__form_type_1" value="" />
		<input type="hidden" name="print" id="print__form_type_1" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	
	<script>
	//Применение настроек чека по умолчанию
	function apply_check_default_options__type_1()
	{
		document.getElementById("taxationSystem__form_type_1").value = document.getElementById("taxationSystem__type_1").value;
		document.getElementById("kkt_device_id__form_type_1").value = document.getElementById("kkt_device_id__type_1").value;
		document.getElementById("check_product_tax__form_type_1").value = document.getElementById("check_product_tax__type_1").value;
		document.getElementById("check_product_paymentMethodType__form_type_1").value = document.getElementById("check_product_paymentMethodType__type_1").value;
		document.getElementById("check_product_paymentSubjectType__form_type_1").value = document.getElementById("check_product_paymentSubjectType__type_1").value;
		document.getElementById("check_payment_type__form_type_1").value = document.getElementById("check_payment_type__type_1").value;
		if(document.getElementById("print__type_1").checked){
			document.getElementById("print__form_type_1").value = 1;
		}else{
			document.getElementById("print__form_type_1").value = 0;
		}
		
		document.getElementById("form_type_1").submit();
	}
	</script>
	
	
	
	<?php
	// Выставляем сохраненные настройки по умолчанию
	$shop_kkt_default_setting_query = $db_link->prepare("SELECT * FROM `shop_kkt_default_setting` WHERE `type` = 1 LIMIT 1;");
	$shop_kkt_default_setting_query->execute();
	$default_setting_type_1 = $shop_kkt_default_setting_query->fetch();
	?>
	<script>
		jQuery( window ).load(function() {
			document.getElementById("taxationSystem__type_1").value = '<?=$default_setting_type_1['taxationSystem'];?>';
			document.getElementById("kkt_device_id__type_1").value = '<?=$default_setting_type_1['kkt_device_id'];?>';
			document.getElementById("check_product_tax__type_1").value = '<?=$default_setting_type_1['check_product_tax'];?>';
			document.getElementById("check_product_paymentMethodType__type_1").value = '<?=$default_setting_type_1['check_product_paymentMethodType'];?>';
			document.getElementById("check_product_paymentSubjectType__type_1").value = '<?=$default_setting_type_1['check_product_paymentSubjectType'];?>';
			document.getElementById("check_payment_type__type_1").value = '<?=$default_setting_type_1['check_payment_type'];?>';
			document.getElementById("print__type_1").checked = <?=(int)$default_setting_type_1['print'];?>;
		});
	</script>
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Информация
			</div>
			<div class="panel-body">
				<p>
					После подключения кассы к интернет-магазину необходимо настроить данные для печати, которые выставляются по умолчанию (СНО по чеку и Ставка НДС позиции, остальные можно оставить без изменений).
				</p>
				<p>
					Обратите внимание на "Признак способа расчета по позиции", изначально он выставлен как "Полный расчет".
					<br/>
					Если вы изменяете, этот параметр на "Предоплата", то необходимо будет не забывать формировать вручную второй чек при выдаче товара, но уже с признаком "Полный расчет".
				</p>
			</div>
		</div>
	</div>
	
	<?php
}
?>