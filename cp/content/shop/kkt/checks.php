<?php
/*
Страничный скрипт для страницы просмотра чеков
*/
defined('_ASTEXE_') or die('No access');


if( isset( $_POST["action"] ) )
{
	
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	//Подключение окна для создания чеков коррекции (их можно создавать только находясь на странице Чеки)
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/kkt/check_correction_create_modal_window.php");
	
	
	$item_name = 'checks';//Для возможности использования скрипта для других похожих страниц.
	?>
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Действия
			</div>
			<div class="panel-body">
				
				<?php
				//Кнопка создания чека
				print_backend_button( array("url"=>"javascript:void(0);", "background_color"=>"#63ce1c", "fontawesome_class"=>"fas fa-plus", "caption"=>"Создать обычный чек", "onclick"=>"init_modal_kkt_create_new_check();") );
				?>
				
				
				<?php
				//Кнопка создания чека КОРРЕКЦИИ
				print_backend_button( array("url"=>"javascript:void(0);", "background_color"=>"#ffae00", "fontawesome_class"=>"fas fa-plus", "caption"=>"Создать чек коррекции", "onclick"=>"correction_init_modal_kkt_create_new_check();") );
				?>
				
				
				<?php
				//Ссылка на страницу "Кассовые аппараты"
				print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/onlajn-kassy/devices", "background_color"=>"#8e44ad", "fontawesome_class"=>"fas fa-server", "caption"=>"Устройства") );
				
				//Корневой раздел по онлайн-кассам
				print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/onlajn-kassy", "background_color"=>"#3498db", "fontawesome_class"=>"fas fa-receipt", "caption"=>"Корневой раздел по чекам") );
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
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				Фильтр чеков
			</div>
			<div class="panel-body" style="padding-top:0;">
				<?php				
				$filter_fields = array();//Массив имен полей фильтра (так проще писать код)
				
				$filter_fields["check_id"] = array("current_value"=>"", "default"=>"", "operator"=>"=", "sql_field_name"=>"check_id");//ID чека
				
				$filter_fields["kkt_device_id"] = array("current_value"=>"", "default"=>"-1", "operator"=>"=", "sql_field_name"=>"kkt_device_id");//ID ККТ
				
				$filter_fields["type"] = array("current_value"=>"", "default"=>"-1", "operator"=>"=", "sql_field_name"=>"type");//Тип (Приход/Возврат прихода/Расход/Возврат расхода)
				
				$filter_fields["customerContact"] = array("current_value"=>"", "default"=>"", "operator" => "LIKE", "sql_field_name"=>"customerContact");//Контакт клиента
				
				
				$filter_fields["taxationSystem"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"taxationSystem");//СНО
				
				
				$filter_fields["time_from"] = array("current_value"=>"", "default"=>"", "operator" => ">", "sql_field_name"=>"time_created");//Время оформления чека, с
				
				$filter_fields["time_to"] = array("current_value"=>"", "default"=>"", "operator" => "<", "sql_field_name"=>"time_created");//Время оформления чека, по
				
				
				
				$filter_fields["sent_to_real_device_flag"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"sent_to_real_device_flag");//Флаг - отправлен на реальное устройство
				
				$filter_fields["real_device_approved_flag"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"real_device_approved_flag");//Флаг - От реальной ККТ получено подтверждение о приеме
				
				
				$filter_fields["is_correction_flag"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"is_correction_flag");//Флаг - Является чеком коррекции
				
				
				
				
				//По товарам и платежам:
				$filter_fields["check_product_text"] = array("current_value"=>"", "default"=>"", "operator" => "LIKE", "sql_field_name"=>"check_product_text");//Наименование товара в одной из позиций чека
				
				$filter_fields["check_product_tax"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"check_product_tax");//Ставка НДС в одной из позиций чека
				
				$filter_fields["check_product_paymentMethodType"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"check_product_paymentMethodType");//Признак способа расчета в одной из позиций чека
				
				$filter_fields["check_product_paymentSubjectType"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"check_product_paymentSubjectType");//Признак предмета расчета в одной из позиций чека
				
				
				$filter_fields["order_item_id"] = array("current_value"=>"", "default"=>"", "operator" => "=", "sql_field_name"=>"order_item_id", "one_of_child_items"=>true);//ID позиции заказа, к которому привяны чеки
				
				$filter_fields["order_id"] = array("current_value"=>"", "default"=>"", "operator" => "=", "sql_field_name"=>"order_id", "one_of_child_items"=>true);//ID заказа, к позициям которого привяны чеки
				
				
				
				$filter_fields["check_payment_type"] = array("current_value"=>"", "default"=>"-1", "operator" => "=", "sql_field_name"=>"check_payment_type");//Способ любого из платежей по чеку
				

				//Получаем текущие значения фильтра:
				if( isset($_COOKIE[$item_name."_filter"]) )
				{
					$items_filter = json_decode($_COOKIE[$item_name."_filter"], true);
					
					
					//Цикл по безопасному массиву
					foreach( $filter_fields AS $name=>$field_params )
					{
						if( isset($items_filter[$name]) )
						{
							$filter_fields[$name]["current_value"] = $items_filter[$name];
						}
						else
						{
							$filter_fields[$name]["current_value"] = $filter_fields[$name]["default"];
						}
					}
				}
				else//Фильтр не выставлялся
				{
					//Цикл по безопасному массиву
					foreach( $filter_fields AS $name=>$field_params )
					{
						$filter_fields[$name]["current_value"] = $filter_fields[$name]["default"];
					}
				}
				?>
				
				<div class="row">
					<div class="col-lg-12">
						<h3>Общие поля чеков:</h3>
					</div>
				</div>
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Дата с
						</label>
						<div class="col-lg-6">
							<div style="position:relative;height:34px;">
								<input style="position:absolute; z-index:2; opacity:0" type="text"  id="time_from" value="<?php echo $filter_fields["time_from"]["current_value"]; ?>" class="form-control" />
								<input style="position:absolute; z-index:1;" type="text" id="time_from_show" class="form-control" />
								<script>
								//Инициализируем datetimepicker
								jQuery("#time_from").datetimepicker({
									lang:"ru",
									closeOnDateSelect:true,
									closeOnTimeSelect:false,
									dayOfWeekStart:1,
									format:'unixtime',
									onClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
									{
										var time_string = "";
										var date_ob = new Date(current_time);
										time_string += date_ob.getDate()+".";
										time_string += (date_ob.getMonth() + 1)+".";
										time_string += date_ob.getFullYear()+" ";
										time_string += date_ob.getHours()+":"+date_ob.getMinutes();
										document.getElementById("time_from_show").value = time_string;//Показываем время в понятном виде
									}
									<?php
									if($filter_fields["time_from"]["current_value"] != "")
									{
										?>
										,
										onGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
										{
											var time_string = "";
											var date_ob = new Date(current_time);
											time_string += date_ob.getDate()+".";
											time_string += (date_ob.getMonth() + 1)+".";
											time_string += date_ob.getFullYear()+" ";
											time_string += date_ob.getHours()+":"+date_ob.getMinutes();
											document.getElementById("time_from_show").value = time_string;//Показываем время в понятном виде
										}
										<?php
									}
									?>
								});
								</script>
							</div>
						</div>
					</div>
				</div>
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Дата по
						</label>
						<div class="col-lg-6">
							<div style="position:relative;height:34px;">
								<input style="position:absolute; z-index:2; opacity:0" type="text"  id="time_to" value="<?php echo $filter_fields["time_to"]["current_value"]; ?>" class="form-control" />
								<input style="position:absolute; z-index:1;" type="text" id="time_to_show" class="form-control" />
								<script>
								//Инициализируем datetimepicker
								jQuery("#time_to").datetimepicker({
									lang:"ru",
									closeOnDateSelect:true,
									closeOnTimeSelect:false,
									dayOfWeekStart:1,
									format:'unixtime',
									onClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
									{
										var time_string = "";
										var date_ob = new Date(current_time);
										time_string += date_ob.getDate()+".";
										time_string += (date_ob.getMonth() + 1)+".";
										time_string += date_ob.getFullYear()+" ";
										time_string += date_ob.getHours()+":"+date_ob.getMinutes();
										document.getElementById("time_to_show").value = time_string;//Показываем время в понятном виде
									}
									<?php
									if($filter_fields["time_to"]["current_value"] != "")
									{
										?>
										,
										onGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации
										{
											var time_string = "";
											var date_ob = new Date(current_time);
											time_string += date_ob.getDate()+".";
											time_string += (date_ob.getMonth() + 1)+".";
											time_string += date_ob.getFullYear()+" ";
											time_string += date_ob.getHours()+":"+date_ob.getMinutes();
											document.getElementById("time_to_show").value = time_string;//Показываем время в понятном виде
										}
										<?php
									}
									?>
								});
								</script>
							</div>
						</div>
					</div>
				</div>
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Внутренний ID чека
						</label>
						<div class="col-lg-6">
							<input type="text"  id="check_id" value="<?php echo $filter_fields["check_id"]["current_value"]; ?>" class="form-control" />
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
								<option value="-1">Все</option>
								<?php
								$kkt_device_query = $db_link->prepare("SELECT * FROM `shop_kkt_devices` ORDER BY `name`;");
								$kkt_device_query->execute();
								while( $kkt_device = $kkt_device_query->fetch() )
								{
									$selected = "";
									if( $filter_fields["kkt_device_id"]["current_value"] == $kkt_device["id"] )
									{
										$selected = " selected=\"selected\" ";
									}
									
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
							Операция
						</label>
						<div class="col-lg-6">
							<select id="type" class="form-control">
								<option value="-1">Все</option>
								<?php
								$type_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1054` ORDER BY `value`;");
								$type_query->execute();
								while( $type = $type_query->fetch() )
								{
									$selected = "";
									if( $filter_fields["type"]["current_value"] == $type["value"] )
									{
										$selected = " selected=\"selected\" ";
									}
									
									?>
									<option value="<?php echo $type["value"]; ?>" <?php echo $selected; ?>><?php echo $type["for_print"]; ?></option>
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
							Контакт покупателя
						</label>
						<div class="col-lg-6">
							<input type="text"  id="customerContact" value="<?php echo $filter_fields["customerContact"]["current_value"]; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							СНО
						</label>
						<div class="col-lg-6">
							<select id="taxationSystem" class="form-control">
								<option value="-1">Все</option>
								<?php
								$taxationSystem_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1055` ORDER BY `value`;");
								$taxationSystem_query->execute();
								while( $taxationSystem = $taxationSystem_query->fetch() )
								{
									$selected = "";
									if( $filter_fields["taxationSystem"]["current_value"] == $taxationSystem["value"] )
									{
										$selected = " selected=\"selected\" ";
									}
									
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
							Отправлен на устройство
						</label>
						<div class="col-lg-6">
							<select id="sent_to_real_device_flag" class="form-control">
								<option value="-1">Все</option>
								<option value="1">Отправлен</option>
								<option value="0">Не отправлен</option>
							</select>
							<script>
								<?php
								if( $filter_fields["sent_to_real_device_flag"]["current_value"] != null )
								{
									?>
									document.getElementById("sent_to_real_device_flag").value = <?php echo $filter_fields["sent_to_real_device_flag"]["current_value"]; ?>;
									<?php
								}
								?>
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Есть подтверждение от устройства
						</label>
						<div class="col-lg-6">
							<select id="real_device_approved_flag" class="form-control">
								<option value="-1">Все</option>
								<option value="1">Есть</option>
								<option value="0">Нет</option>
							</select>
							<script>
								<?php
								if( $filter_fields["real_device_approved_flag"]["current_value"] != null )
								{
									?>
									document.getElementById("real_device_approved_flag").value = <?php echo $filter_fields["real_device_approved_flag"]["current_value"]; ?>;
									<?php
								}
								?>
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Тип чека
						</label>
						<div class="col-lg-6">
							<select id="is_correction_flag" class="form-control">
								<option value="-1">Все</option>
								<option value="0">Обычные чеки</option>
								<option value="1">Чеки коррекции</option>
							</select>
							<script>
								<?php
								if( $filter_fields["is_correction_flag"]["current_value"] != null )
								{
									?>
									document.getElementById("is_correction_flag").value = <?php echo $filter_fields["is_correction_flag"]["current_value"]; ?>;
									<?php
								}
								?>
							</script>
						</div>
					</div>
				</div>
				
				
				
				<div class="row">
					<div class="col-lg-12">
						<h3>Фильтры по товарным позициям: <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('В одном чеке может быть несколько товарных позиций. По данным фильтрам будут показаны чеки, в позициях которых найдено хотя бы одно совпадение.');"><i class="fa fa-info"></i></button></h3>
					</div>
				</div>
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Наименование позиции
						</label>
						<div class="col-lg-6">
							<input type="text"  id="check_product_text" value="<?php echo $filter_fields["check_product_text"]["current_value"]; ?>" class="form-control" />
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
								<option value="-1">Все</option>
								<?php
								$check_product_tax_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1199` ORDER BY `value`;");
								$check_product_tax_query->execute();
								while( $check_product_tax = $check_product_tax_query->fetch() )
								{
									$selected = "";
									if( $filter_fields["check_product_tax"]["current_value"] == $check_product_tax["value"] )
									{
										$selected = " selected=\"selected\" ";
									}
									
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
							Признак способа расчета
						</label>
						<div class="col-lg-6">
							<select id="check_product_paymentMethodType" class="form-control">
								<option value="-1">Все</option>
								<?php
								$check_product_paymentMethodType_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1214` ORDER BY `value`;");
								$check_product_paymentMethodType_query->execute();
								while( $check_product_paymentMethodType = $check_product_paymentMethodType_query->fetch() )
								{
									$selected = "";
									if( $filter_fields["check_product_paymentMethodType"]["current_value"] == $check_product_paymentMethodType["value"] )
									{
										$selected = " selected=\"selected\" ";
									}
									
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
							Признак предмета расчета
						</label>
						<div class="col-lg-6">
							<select id="check_product_paymentSubjectType" class="form-control">
								<option value="-1">Все</option>
								<?php
								$check_product_paymentSubjectType_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_tag_1212` ORDER BY `value`;");
								$check_product_paymentSubjectType_query->execute();
								while( $check_product_paymentSubjectType = $check_product_paymentSubjectType_query->fetch() )
								{
									$selected = "";
									if( $filter_fields["check_product_paymentSubjectType"]["current_value"] == $check_product_paymentSubjectType["value"] )
									{
										$selected = " selected=\"selected\" ";
									}
									
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
							ID позиции заказа, к которой привязаны чеки
						</label>
						<div class="col-lg-6">
							<input type="text"  id="order_item_id" value="<?php echo $filter_fields["order_item_id"]["current_value"]; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							ID заказа, к позициям которого привязаны чеки
						</label>
						<div class="col-lg-6">
							<input type="text"  id="order_id" value="<?php echo $filter_fields["order_id"]["current_value"]; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				
				
				<div class="row">
					<div class="col-lg-12">
						<h3>Фильтры по платежам по чеку: <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('В одном чеке может быть несколько платежей. По данным фильтрам будут показаны чеки, в платежах которых найдено хотя бы одно совпадение.');"><i class="fa fa-info"></i></button></h3>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Способ платежа
						</label>
						<div class="col-lg-6">
							<select id="check_payment_type" class="form-control">
								<option value="-1">Все</option>
								<?php
								$check_payment_type_query = $db_link->prepare("SELECT * FROM `shop_kkt_ref_payment_types_tags` ORDER BY `value`;");
								$check_payment_type_query->execute();
								while( $check_payment_type = $check_payment_type_query->fetch() )
								{
									$selected = "";
									if( $filter_fields["check_payment_type"]["current_value"] == $check_payment_type["value"] )
									{
										$selected = " selected=\"selected\" ";
									}
									
									?>
									<option value="<?php echo $check_payment_type["value"]; ?>" <?php echo $selected; ?>><?php echo $check_payment_type["for_print"]; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
				</div>
				
				
				
				
				
				
			</div>
			<div class="panel-footer">
				<div class="row">
					<div class="col-lg-12 float-e-margins">
						<button class="btn btn-success" type="button" onclick="filterItems();"><i class="fa fa-filter"></i> Отфильтровать</button>
						<button class="btn btn-primary" type="button" onclick="unsetFilterItems();"><i class="fa fa-square"></i> Снять фильры</button>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	<script>
    // ------------------------------------------------------------------------------------------------
    //Устновка cookie в соответствии с фильтром
    function filterItems()
    {
        var filter = new Object;
        
		
		//...Установка фильтров
        <?php
		foreach($filter_fields AS $key => $field)
		{
			?>
			filter.<?php echo $key; ?> = document.getElementById("<?php echo $key; ?>").value;
			<?php
		}
		?>
		
		
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "<?php echo $item_name; ?>_filter="+JSON.stringify(filter)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks';
    }
    // ------------------------------------------------------------------------------------------------
    //Снять все фильтры
    function unsetFilterItems()
    {
        var date = new Date(new Date().getTime() - 15552000 * 1000);//Время в прошлом - куки удалится
        document.cookie = "<?php echo $item_name; ?>_filter=1; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks';
    }
    // ------------------------------------------------------------------------------------------------
    </script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<script>
    // ------------------------------------------------------------------------------------------------
    //Установка куки сортировки
    function sortItems(field)
    {
        var asc_desc = "asc";//Направление по умолчанию
        
        //Берем из куки текущий вариант сортировки
        var current_sort_cookie = getCookie("<?php echo $item_name; ?>_sort");
        if(current_sort_cookie != undefined)
        {
            current_sort_cookie = JSON.parse(getCookie("<?php echo $item_name; ?>_sort"));
            //Если поле это же - обращаем направление
            if(current_sort_cookie.field == field)
            {
                if(current_sort_cookie.asc_desc == "asc")
                {
                    asc_desc = "desc";
                }
                else
                {
                    asc_desc = "asc";
                }
            }
        }
        
        
        var <?php echo $item_name; ?>_sort = new Object;
        <?php echo $item_name; ?>_sort.field = field;//Поле, по которому сортировать
        <?php echo $item_name; ?>_sort.asc_desc = asc_desc;//Направление сортировки
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "<?php echo $item_name; ?>_sort="+JSON.stringify(<?php echo $item_name; ?>_sort)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks';
    }
    // ------------------------------------------------------------------------------------------------
    // возвращает cookie с именем name, если есть, если нет, то undefined
    function getCookie(name) 
    {
        var matches = document.cookie.match(new RegExp(
            "(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
        ));
        return matches ? decodeURIComponent(matches[1]) : undefined;
    }
    // ------------------------------------------------------------------------------------------------
    </script>
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Чеки
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<?php
					//Определяем текущую сортировку и обозначаем ее:
					$items_sort = NULL;
					$sort_field = "id";
					$sort_asc_desc = "desc";
					if( isset($_COOKIE[$item_name."_sort"]) )
					{
						$items_sort = $_COOKIE[$item_name."_sort"];
					}
					if($items_sort != NULL)
					{
						$items_sort = json_decode($items_sort, true);
						$sort_field = $items_sort["field"];
						$sort_asc_desc = $items_sort["asc_desc"];
					}
					
					if( strtolower($sort_asc_desc) == "asc" )
					{
						$sort_asc_desc = "asc";
					}
					else
					{
						$sort_asc_desc = "desc";
					}
					
					//Имя колонки указывается в куки. Исключаем инъекцию: ищем в списке допустимых
					if( ! is_array($filter_fields[$sort_field]) && array_search($sort_field, array("check_id","kkt_device_id","type","taxationSystem","sent_to_real_device_flag","real_device_approved_flag","is_correction_flag","check_sum","time_created")) === false )
					{
						$sort_field = "check_id";
					}
					
					
					
					
					
					//Учет фильтра
					$WHERE = "";
					$binding_values = array();
					$binding_values_before = array();
					/*
					Цикл по массиву $filter_fields (это массив с описанием полей фильтра). 
					$field - безопасно, т.к. задается выше в скрипте.
					$params["operator"] - безопасно, т.к. задается выше в скрипте
					
					$params["current_value"] - НЕбезопасно, т.к. содержит данные от пользователя (из куки)
					
					
					т.е. в $params - все поля кроме $params["current_value"], безопасны
					*/
					
					foreach( $filter_fields AS $field=>$params )
					{
						
						//Если это поле фильтра, для которого требуется хотя бы одно совпадение среди вложенных узлов (например, когда требуется выбрать чек, в позицях которого есть совпадение по фильтру). Т.е. количество вложенных элементов, удовлетворяющих фильтру больше 0 (к имени колонки в SQL подстваляется count_). Особенность этого типа фильтра такая, что в данном цикле foreach подготовка запроса идет в двух частях. В первой части - добавляется значение в binding_values_before, т.к. его нужно добавить в SQL-запрос в любом случае, даже если фильтр не выставлен. Во второй части - если выставлен фильтр, то добавляется условие в WHERE
						if( isset($params["one_of_child_items"]) )
						{
							$sql_field_name = "count_".$field;//Имя поля в фильтре (в большинстве случаев равно имени колонки в выборке)
							if( isset($params["sql_field_name"]) )
							{
								//Для некоторых полей фильтра, название колонки в SQL-выборке может отличаться, например time_to и time_from - это это поля фильтра, но в SQL-запросе - это одно и то же поле time_created
								$sql_field_name = "count_".$params["sql_field_name"];
							}
							
							//Биндим значение - в начало массива!!!
							$binding_values_before[] = $params["current_value"];
						}
						
						
						//Данное поле не учитываем
						if( $params["current_value"] == $params["default"] )
						{
							continue;
						}
						
						if( $WHERE != "" )
						{
							$WHERE = $WHERE." AND ";
						}
						
						
						//Если в поле фильтра указан параметр several_columns - это значит, что значение этого фильтра нужно искать по нескольким колонкам, указанным в several_columns через оператор OR (это массив). Пример: значение поля фильтра "Адрес" ищем по колонкам SQL-выборки "Город", "Улица", "Дом"
						if( isset($params["several_columns"]) )
						{
							for($i=0 ; $i < count($params["several_columns"]) ; $i++)
							{
								if($i==0)
								{
									$WHERE = $WHERE." (";//Первая колонка
								}
								
								if( $i > 0 )
								{
									$WHERE = $WHERE." OR ";
								}
								
								$WHERE = $WHERE." `".$params["several_columns"][$i]."` ".$params["operator"]." ? ";
								
								
								if($i==count($params["several_columns"])-1)
								{
									$WHERE = $WHERE." )";//Последняя колонка
								}
								
								
								//Биндим значение
								if($params["operator"] == "LIKE")
								{
									$binding_values[] = "%".$params["current_value"]."%";
								}
								else
								{
									$binding_values[] = $params["current_value"];
								}
							}
						}
						else if( isset($params["one_of_child_items"]) )
						{
							$WHERE = $WHERE." `".$sql_field_name."` > 0 ";
						}
						else//Обычный способ (когда одно поле фильтра работает с одной колонкой в SQL-выборке)
						{
							$sql_field_name = $field;//Имя поля в фильтре (в большинстве случаев равно имени колонки в выборке)
							if( isset($params["sql_field_name"]) )
							{
								//Для некоторых полей фильтра, название колонки в SQL-выборке может отличаться, например time_to и time_from - это это поля фильтра, но в SQL-запросе - это одно и то же поле time_created
								$sql_field_name = $params["sql_field_name"];
							}
							
							
							$WHERE = $WHERE." `".$sql_field_name."` ".$params["operator"]." ? ";
							
							//Биндим значение
							if($params["operator"] == "LIKE")
							{
								$binding_values[] = "%".$params["current_value"]."%";
							}
							else
							{
								$binding_values[] = $params["current_value"];
							}
						}
						
					}
					$binding_values = array_merge($binding_values_before, $binding_values);
					if($WHERE != "")
					{
						$WHERE = " WHERE ".$WHERE;
					}
					

					
					//Настройки пагинации
					$rows_per_page = $DP_Config->list_page_limit;//Количество строк на страницу (SQL-параметр)
					$row_from = 0;//С какой строки начать (SQL-параметр)
					$current_page = 0;
					if( isset($_GET["page"]) )
					{
						$current_page = (int)$_GET["page"];
					}
					$row_from = $current_page * $rows_per_page;
					
					
					
					
					
					//ЗАПРОС
					/*
					Запрос будет делать так:
					- сначала запрос самих чеков.
					- потом, на этапе отображения каждого чека будет делать запрос по его платежам и товарам. Учитывая, что отображать чеки будем постранично - большой нагрузки не вызовет.
					*/
					
					
					$SQL = "SELECT SQL_CALC_FOUND_ROWS * FROM
					(SELECT 
					shop_kkt_checks.id AS `id`,
					shop_kkt_checks.id AS `check_id`,
					shop_kkt_checks.kkt_device_id,
					shop_kkt_checks.type,
					shop_kkt_checks.customerContact,
					shop_kkt_checks.taxationSystem,
					shop_kkt_checks.time_created,
					shop_kkt_checks.sent_to_real_device_flag,
					shop_kkt_checks.real_device_approved_flag,
					shop_kkt_checks.real_device_text_answer,
					shop_kkt_checks.is_correction_flag,
					shop_kkt_checks.correction_type,
					shop_kkt_checks.correction_description,
					shop_kkt_checks.correction_causeDocumentDate,
					shop_kkt_checks.correction_causeDocumentNumber,
					shop_kkt_checks.correction_totalSum,
					shop_kkt_checks.correction_cashSum,
					shop_kkt_checks.correction_eCashSum,
					shop_kkt_checks.correction_prepaymentSum,
					shop_kkt_checks.correction_postpaymentSum,
					shop_kkt_checks.correction_otherPaymentTypeSum,
					shop_kkt_checks.correction_tax1Sum,
					shop_kkt_checks.correction_tax2Sum,
					shop_kkt_checks.correction_tax3Sum,
					shop_kkt_checks.correction_tax4Sum,
					shop_kkt_checks.correction_tax5Sum,
					shop_kkt_checks.correction_tax6Sum,
					(SELECT `name` FROM `shop_kkt_devices` WHERE `id` = `shop_kkt_checks`.`kkt_device_id`) AS `kkt_device_name`,
					
					(SELECT `for_print` FROM `shop_kkt_ref_tag_1055` WHERE `value` = `shop_kkt_checks`.`taxationSystem`) AS `taxationSystem_for_print`,
					
					(SELECT `for_print` FROM `shop_kkt_ref_tag_1054` WHERE `value` = `shop_kkt_checks`.`type`) AS `type_for_print`,
					
					
					IF(`is_correction_flag` = 0, (SELECT SUM(`amount`) FROM `shop_kkt_checks_payments` WHERE `check_id` = `shop_kkt_checks`.`id` ), correction_totalSum ) AS `check_sum`,
					
					(SELECT `text` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1 ) AS `check_product_text`,
					
					(SELECT `tax` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1 ) AS `check_product_tax`,
					
					(SELECT `paymentMethodType` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1 ) AS `check_product_paymentMethodType`,
					
					(SELECT `paymentSubjectType` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1 ) AS `check_product_paymentSubjectType`,
					
					(SELECT `type` FROM `shop_kkt_checks_payments` WHERE `check_id` = `shop_kkt_checks`.`id` LIMIT 1 ) AS `check_payment_type`,
					
					
					
					
					(SELECT COUNT(`id`) FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` IN (SELECT `id` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` ) AND `order_item_id` = ? ) AS `count_order_item_id`,
					
					
					(SELECT COUNT(`order_id`) FROM `shop_orders_items` WHERE `order_id` = ? AND `id` IN (SELECT `order_item_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` IN (SELECT `id` FROM `shop_kkt_checks_products` WHERE `check_id` = `shop_kkt_checks`.`id` ) ) ) AS `count_order_id`
					
					
					FROM
					`shop_kkt_checks` ) t1
					 ".$WHERE." ORDER BY `".$sort_field."` ".$sort_asc_desc." LIMIT ".$row_from.",".$rows_per_page;
					
					
					
					
					$elements_query = $db_link->prepare($SQL);
					if($elements_query->execute($binding_values) != true)
					{
						echo "<br><font style=\"background-color:#C33;color:#FFF;\">Есть ошибка запроса: ".print_r($elements_query->errorInfo(), true)."</font><br>";
						
						echo $SQL."<br>".print_r($binding_values, true);
					}
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					//Массивы для JS с id элементов и с чекбоксами элементов
					$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
					$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов

					
					?>
					<table id="<?php echo $item_name; ?>_table" class="footable table table-hover toggle-arrow " data-sort="false" data-page-size="<?php echo $rows_per_page; ?>">
						<thead>
							<th style="width:30px;"><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();" /></th>
							<th data-toggle="true" style="width:50px;"></th>
							<th style="text-align:center; width:50px;"><a href="javascript:void(0);" onclick="sortItems('check_id');" id="check_id_sorter">ID</a></th>
							
							<th><a href="javascript:void(0);" onclick="sortItems('kkt_device_id');" id="kkt_device_id_sorter">Касса</a></th>
							
							<th><a href="javascript:void(0);" onclick="sortItems('type');" id="type_sorter">Операция</a></th>
							
							<th><a href="javascript:void(0);" onclick="sortItems('taxationSystem');" id="taxationSystem_sorter">СНО</a></th>
							
							
							<th style="text-align:center;"><a href="javascript:void(0);" onclick="sortItems('sent_to_real_device_flag');" id="sent_to_real_device_flag_sorter">Отправлено<br>на устройство</a></th>
							
							<th style="text-align:center;"><a href="javascript:void(0);" onclick="sortItems('real_device_approved_flag');" id="real_device_approved_flag_sorter">Получено<br>подтверждение<br>от устройства</a></th>
							
							
							
							<th style="text-align:center;"><a href="javascript:void(0);" onclick="sortItems('is_correction_flag');" id="is_correction_flag_sorter">Тип чека</a></th>
							
							
							<th><a href="javascript:void(0);" onclick="sortItems('check_sum');" id="check_sum_sorter">Сумма по чеку</a></th>
							
							
							
							<th style="text-align:center; width:100px;"><a href="javascript:void(0);" onclick="sortItems('time_created');" id="time_created_sorter">Время</a></th>

							<th data-hide="phone,tablet,default"><a href="javascript:void(0);" onclick="" id=""></a></th>
						</thead>
						<tbody>
						<?php
						while( $item = $elements_query->fetch() )
						{
							//Для Javascript
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$item["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$item["id"].";\n";//Добавляем элемент для JS
							?>
							

							<tr class="" id="<?php echo $item_name; ?>_record_<?php echo $item["id"]; ?>">
								<td style="text-align:center;"><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $item["id"]; ?>');" id="checked_<?php echo $item["id"]; ?>" name="checked_<?php echo $item["id"]; ?>" /></td>
								<td style="text-align:center;"></td>
								<td style="text-align:center;"><?php echo $item["id"]; ?></td>
								<td><?php echo translate_str_by_id($item["kkt_device_name"]); ?></td>
								<td><?php echo $item["type_for_print"]; ?></td>
								<td><?php echo $item["taxationSystem_for_print"]; ?></td>
								
								
								<td style="text-align:center;">
									<?php
									if( $item["sent_to_real_device_flag"] == 1 )
									{
										?>
										<i class="fas fa-check-circle" style="font-size:20px;color:#3C3;"></i>
										<?php
									}
									else
									{
										?>
										<i class="fas fa-exclamation-triangle" style="font-size:20px;color:#C33;"></i>
										<?php
									}
									?>
								</td>
								
								<td style="text-align:center;">
									<?php
									if( $item["real_device_approved_flag"] == 1)
									{
										?>
										<i class="fas fa-check-circle" style="font-size:20px;color:#3C3;"></i>
										<?php
									}
									else
									{
										?>
										<i class="fas fa-exclamation-triangle" style="font-size:20px;color:#C33;"></i>
										<?php
									}
									?>
								</td>
								
								
								<td style="text-align:center;">
									<?php
									if( $item["is_correction_flag"] == 0 )
									{
										?>
										Обычный
										<?php
									}
									else
									{
										?>
										<span style="background-color:#ffae00;color:#000;">Чек коррекции</span>
										<?php
									}
									?>
								</td>
								
								
								<td>
									<?php echo number_format($item["check_sum"],2,'.',''); ?>
								</td>
								
								
								
								<td style="text-align:center;"><?php echo date("Y.m.d", $item["time_created"])."<br>".date("H:i:s", $item["time_created"]); ?></td>
								
								
								
								<td>
									<?php
									//Для обычного чека
									if( ! $item["is_correction_flag"] )
									{
										?>
										<b>Контакт покупателя:</b> <?php echo $item["customerContact"]; ?><br>
										<b>Товарные позиции чека:</b>
										<table class="table">
											<tr> <th>№</th> <th>Наименование</th> <th>Цена</th> <th>Количество</th> <th>Сумма</th> <th>Признак способа расчета</th> <th>Признак предмета расчета</th> <th>НДС</th> <th>Привязка к заказу</th>  </tr>
											<?php
											//Получаем перечен товарных позиций
											$check_products_query = $db_link->prepare("SELECT *, `price`*`count` AS `sum`, (SELECT `for_print` FROM `shop_kkt_ref_tag_1214` WHERE `value` = `shop_kkt_checks_products`.`paymentMethodType`) AS `paymentMethodType_for_print`, (SELECT `for_print` FROM `shop_kkt_ref_tag_1212` WHERE `value` = `shop_kkt_checks_products`.`paymentSubjectType`) AS `paymentSubjectType_for_print`, (SELECT `for_print` FROM `shop_kkt_ref_tag_1199` WHERE `value` = `shop_kkt_checks_products`.`tax`) AS `tax_for_print`, (SELECT `order_id` FROM `shop_orders_items` WHERE `id` IN (SELECT `order_item_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` = `shop_kkt_checks_products`.`id`)) AS `order_id`, (SELECT `order_item_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `check_product_id` = `shop_kkt_checks_products`.`id`) AS `order_item_id` FROM `shop_kkt_checks_products` WHERE `check_id` = ?");
											$check_products_query->execute( array($item["id"]) );
											$product_n = 1;
											$products_sum = 0;
											while( $check_product = $check_products_query->fetch() )
											{
												?>
												<tr> <td><?php echo $product_n; ?></td> <td><?php echo $check_product["text"]; ?></td> <td><?php echo number_format($check_product["price"],2,'.',''); ?></td> <td><?php echo $check_product["count"]; ?></td> <td><?php echo number_format($check_product["sum"],2,'.',''); ?></td> <td><?php echo $check_product["paymentMethodType_for_print"]; ?></td> <td><?php echo $check_product["paymentSubjectType_for_print"]; ?></td> <td><?php echo $check_product["tax_for_print"]; ?></td>
												
												<td>
												<?php
												if( (int)$check_product["order_id"] == 0 )
												{
													?>
													-
													<?php
												}
												else
												{
													?>
													Заказ <?php echo $check_product["order_id"]; ?> (поз. <?php echo $check_product["order_item_id"]; ?>)
													<?php
												}
												?>
												</td> </tr>
												<?php
												$products_sum = $products_sum + $check_product["sum"];
												$product_n++;
											}
											?>
											<tr> <td colspan="4" style="text-align:right;font-weight:bold;">ИТОГО</td> <td style="font-weight:bold;"><?php echo number_format($products_sum,2,'.',''); ?></td> <td></td> <td></td> <td></td> <td></td> </tr>
										</table>
										
										<b>Платежи по чеку:</b>
										<table class="table">
											<tr> <th>№</th> <th>Сумма</th> <th>Способ платежа</th>  </tr>
											<?php
											//Получаем перечен товарных позиций
											$check_payments_query = $db_link->prepare("SELECT *, (SELECT `for_print` FROM `shop_kkt_ref_payment_types_tags` WHERE `value` = `shop_kkt_checks_payments`.`type`) AS `type_for_print` FROM `shop_kkt_checks_payments` WHERE `check_id` = ?");
											$check_payments_query->execute( array($item["id"]) );
											$payment_n = 1;
											$payments_sum = 0;
											while( $check_payment = $check_payments_query->fetch() )
											{
												?>
												<tr> <td><?php echo $payment_n; ?></td> <td><?php echo number_format($check_payment["amount"],2,'.',''); ?></td> <td><?php echo $check_payment["type_for_print"]; ?></td> </tr>
												<?php
												$payment_n++;
												$payments_sum = $payments_sum + $check_payment["amount"];
											}
											?>
											<tr> <td style="text-align:right;font-weight:bold;">ИТОГО</td> <td style="font-weight:bold;"><?php echo number_format($payments_sum,2,'.',''); ?></td> <td></td> </tr>
										</table>
										<?php
									}
									else//Для чека коррекции
									{
										?>
										<b>Тип коррекции:</b> 
										<?php 
										if($item["correction_type"] == 0)
										{
											?>
											Самостоятельно
											<?php
										}
										else
										{
											?>
											По предписанию
											<?php
										}
										?>
										<br>
										<b>Описание коррекции:</b> <?php echo $item["correction_description"]; ?><br>
										
										<?php
										if($item["correction_type"] == 1)
										{
											?>
											<b>Дата документа основания:</b> <?php echo date("m.d.Y", $item["correction_causeDocumentDate"]); ?><br>
											<b>Номер документа основания:</b> <?php echo $item["correction_causeDocumentNumber"]; ?><br>
											<?php
										}
										?>
										
										
										
										
										
										<div class="hr-line-dashed"></div>
										
										<b>Сумма по чеку:</b> <?php echo $item["correction_totalSum"]; ?><br>
										<b>Сумма по чеку наличными:</b> <?php echo $item["correction_cashSum"]; ?><br>
										<b>Сумма по чеку безналичными:</b> <?php echo $item["correction_eCashSum"]; ?><br>
										<b>Сумма по чеку предоплатой:</b> <?php echo $item["correction_prepaymentSum"]; ?><br>
										<b>Сумма по чеку постоплатой:</b> <?php echo $item["correction_postpaymentSum"]; ?><br>
										<b>Сумма по чеку встречным предоставлением:</b> <?php echo $item["correction_otherPaymentTypeSum"]; ?><br>
										
										<div class="hr-line-dashed"></div>
										
										<b>Сумма НДС в чеке по ставке 20%:</b> <?php echo $item["correction_tax1Sum"]; ?><br>
										<b>Сумма НДС в чеке по ставке 10%:</b> <?php echo $item["correction_tax2Sum"]; ?><br>
										<b>Сумма НДС в чеке по ставке 0%:</b> <?php echo $item["correction_tax3Sum"]; ?><br>
										<b>Сумма по чеку без НДС:</b> <?php echo $item["correction_tax4Sum"]; ?><br>
										<b>Сумма НДС в чеке по ставке 20/120:</b> <?php echo $item["correction_tax5Sum"]; ?><br>
										<b>Сумма НДС в чеке по ставке 10/110:</b> <?php echo $item["correction_tax6Sum"]; ?><br>
										<?php
									}
									?>
								</td>
							</tr>
							<?php
						}//while() - по заказам
						?>
						</tbody>
						<tfoot>
							<tr>
								<td colspan="20" style="text-align:center;">
									<div class="btn-group">
										<?php
										//КНОПКА "ВЛЕВО"
										$to_left_disabled = "";
										if( $current_page == 0 )
										{
											$to_left_disabled = "disabled";
										}
										?>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks?page=0">Первая</a>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks?page=<?php echo $current_page-1; ?>"><i class="fa fa-chevron-left"></i></a>
										
										
										<?php
										//Определяем количество страниц
										$pages_count = (int)($elements_count_rows/$rows_per_page);
										if( ($elements_count_rows%$rows_per_page) > 0 )
										{
											$pages_count++;
										}
										
										
										//Выводим кнопки для конкретных страниц (с номерами)
										for($i=0; $i < $pages_count; $i++)
										{
											//Две кнопки до текущей - показываем
											if( ($current_page - $i) > 2  )
											{
												continue;
											}
											
											
											//Две кнопки после текущей - показываем
											if( ($i - $current_page) > 2  )
											{
												break;
											}
											
											
											
											$active = "";
											if($i == $current_page)
											{
												$active = "active";
											}
											?>
											<a class="btn btn-default <?php echo $active; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks?page=<?php echo $i; ?>"><?php echo $i+1; ?></a>
											<?php
										}
										
										
										//КНОПКА "ВПРАВО"
										$to_right_disabled = "";
										if( ($current_page+1) == $pages_count )
										{
											$to_right_disabled = "disabled";
										}
										?>
										<a class="btn btn-default <?php echo $to_right_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks?page=<?php echo $current_page+1; ?>"><i class="fa fa-chevron-right"></i></a>
										<a class="btn btn-default <?php echo $to_right_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/onlajn-kassy/checks?page=<?php echo $pages_count-1; ?>">Последняя</a>
									</div>
									
									<br>
									<div style="text-align:left;">
									Всего элементов по фильтру: <?php echo $elements_count_rows; ?>, элементов на одной странице: <?php echo $rows_per_page; ?>, страниц всего по фильтру: <?php echo $pages_count; ?>
									</div>
								</td>
							</tr>
						</tfoot>
					</table>
				</div>
				
				
				
				
				
			</div>
			<!--
			<div class="panel-footer">
				<div class="row">
					<div class="col-lg-6">
					</div>
				</div>
			</div>
			-->
		</div>
	</div>
	
	
	
	
	
	
	<script>
		jQuery( window ).load(function() {
			$('#<?php echo $item_name; ?>_table').footable();
			
			document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
		});
	</script>
	
	<script>
    // ----------------------------------------------------------------------------------------
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
    // ----------------------------------------------------------------------------------------
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
    // ----------------------------------------------------------------------------------------
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
    // ----------------------------------------------------------------------------------------
    </script>
	
	<?php
}
?>