<?php
//Страничный скрипт для простановки цен
defined('_ASTEXE_') or die('No access');



if( isset( $_GET['action'] ) )
{
	
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
	
	
	
	
	<?php
	$price_id = $_GET["price_id"];
	$price_id_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_docpart_prices` WHERE `id` = ?;");
	$price_id_query->execute( array($price_id) );
	if( $price_id_query->fetchColumn() == 0 )
	{
		exit;
	}
	?>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/excel.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(771); ?></div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>

			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel collapsed">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(2396); ?>
			</div>
			<div class="panel-body">

<?php echo translate_str_by_id(3733); ?>

			</div>
		</div>
	</div>
	
	
	
	
	
	<div class="col-lg-12" id="options_div">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3734); ?>
			</div>
			<div class="panel-body form-horizontal">
				
				
				
				<div class="form-group">
					<label class="col-sm-2 control-label"><?php echo translate_str_by_id(3735); ?></label>

                    <div class="col-sm-10">
						<select class="form-control m-b" id="base_mark">
							<option value="min"><?php echo translate_str_by_id(3736); ?></option>
							<option value="middle"><?php echo translate_str_by_id(3737); ?></option>
							<option value="max"><?php echo translate_str_by_id(3738); ?></option>
						</select>
                    </div>
                </div>
				
				
				<div class="hr-line-dashed"></div>
				
				
				<div class="form-group">
					<label class="col-sm-2 control-label"><?php echo translate_str_by_id(2755); ?></label>

                    <div class="col-sm-10">
						<select class="form-control m-b" id="plus_minus">
							<option value="plus"><?php echo translate_str_by_id(3739); ?></option>
							<option value="minus"><?php echo translate_str_by_id(3740); ?></option>
						</select>
                    </div>
                </div>
				
				
				<div class="hr-line-dashed"></div>
				
				
				<div class="form-group">
					<label class="col-sm-2 control-label"><?php echo translate_str_by_id(3741); ?></label>

                    <div class="col-sm-10">
						<input class="form-control" placeholder="<?php echo translate_str_by_id(3742); ?>" id="percent" type="number" />
                    </div>
                </div>
				
				
				<div class="hr-line-dashed"></div>
				
				
				
				<div class="form-group">
					<label class="col-sm-2 control-label"><?php echo translate_str_by_id(3743); ?></label>

                    <div class="col-sm-10">
						
						<select multiple="multiple" id="prices">
							
							<?php
							$prices_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `id` != ?;");
							$prices_query->execute( array( $price_id ) );
							while( $price = $prices_query->fetch() )
							{
								?>
								<option value="<?php echo $price['id']; ?>"><?php echo $price['name']." (ID ".$price['id'].")"; ?></option>
								<?php
							}
							?>
						
						</select>
						<script>
							//Делаем из селектора виджет с чекбоками
							$('#prices').multipleSelect({placeholder: "<?php echo translate_str_by_id(3200); ?>...", width:"100%"});
						</script>
						
                    </div>
                </div>
				
				
				<div class="hr-line-dashed"></div>
				
				<div id="buttons_div">
					<button type="button" class="btn w-xs btn-primary2" onclick="review_price();"><i class="fas fa-sync"></i> <?php echo translate_str_by_id(3744); ?></button>
				</div>
		
			</div>
		</div>
	</div>
		
		
		
	
	<div class="col-lg-12" id="progress_bar_div" style="display:none;">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3745); ?>
			</div>
			<div class="panel-body form-horizontal">
		
				<div class="m-t-xl" style="margin-top:0!important;">
					<h3 class="m-b-xs">Простановка цен</h3>
					<span class="font-bold no-margins" id="progress_text">
						<?php echo translate_str_by_id(2722); ?> 76.43%
					</span>
					<div class="progress m-t-xs full progress-small">
						<div style="width: 55%" aria-valuemax="100" aria-valuemin="0" aria-valuenow="55" role="progressbar" class=" progress-bar progress-bar-success" id="progress_bar"></div>
					</div>
					
					<div class="row" id="cancel_button_div">
						<div class="col-md-12">
							<button type="button" class="btn w-xs btn-danger" onclick="cancel_process();"><i class="fas fa-stop"></i> <?php echo translate_str_by_id(2190); ?></button>
							<script>
							function cancel_process()
							{
								can_go_on = 0;
								
								if( confirm('<?php echo translate_str_by_id(3746); ?>') )
								{
									document.getElementById('options_div').setAttribute('style', 'display:block;');
									document.getElementById('progress_bar_div').setAttribute('style', 'display:none;');
									return;
								}
								else
								{
									//Продолжаем
									can_go_on = 1;
									send_request_for_one_part();
								}
							}
							</script>
						</div>
					</div>
					
					<div class="row" id="work_result">
						
						<div class="col-md-12">
							<h3><?php echo translate_str_by_id(3747); ?></h3>
						</div>
						
						
						<div class="col-md-4">
							<?php echo translate_str_by_id(3748); ?>: <span id="items_count">10000</span><br>
							<button type="button" class="btn w-xs btn-primary" onclick="download_price(1);" id="download_button_1"><i class="fas fa-download"></i> Скачать</button> <img src="/content/files/images/ajax-loader-transparent.gif" id="download_img_1" style="display:none;" />
						</div>
						
						<div class="col-md-4">
							<?php echo translate_str_by_id(3749); ?>: <span id="reviewed_yes">5000</span><br>
							<button type="button" class="btn w-xs btn-success" onclick="download_price(2);" id="download_button_2"><i class="fas fa-download"></i> Скачать</button> <img src="/content/files/images/ajax-loader-transparent.gif" id="download_img_2" style="display:none;" />
						</div>
						
						<div class="col-md-4">
							<?php echo translate_str_by_id(3750); ?> <span id="reviewed_no">5000</span><br>
							<button type="button" class="btn w-xs btn-danger" onclick="download_price(3);" id="download_button_3"><i class="fas fa-download"></i> Скачать</button> <img src="/content/files/images/ajax-loader-transparent.gif" id="download_img_3" style="display:none;" />
						</div>
						
						<script>
						// -----------------------------------------------------------------------------------------
						function download_price(type)
						{
							//Индикация рядом
							document.getElementById('download_img_'+type).setAttribute('style', 'display:block;');
							
							//Все кнопки неактивны
							document.getElementById('download_button_1').disabled = true;
							document.getElementById('download_button_2').disabled = true;
							document.getElementById('download_button_3').disabled = true;
							
							
							jQuery.ajax({
								type: "POST",
								async: true,
								url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/price_review/ajax_create_csv.php",
								dataType: "text",//Тип возвращаемого значения
								data: "price_id=<?php echo $price_id; ?>&type="+type+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
								success: function(answer)
								{	
									//Все кнопки активны
									document.getElementById('download_button_1').disabled = false;
									document.getElementById('download_button_2').disabled = false;
									document.getElementById('download_button_3').disabled = false;
									
									//Убрать индикацию
									document.getElementById('download_img_1').setAttribute('style', 'display:none;');
									document.getElementById('download_img_2').setAttribute('style', 'display:none;');
									document.getElementById('download_img_3').setAttribute('style', 'display:none;');
									
									
									var answer_ob = JSON.parse(answer);
									
									//Если некорректный парсинг ответа
									if( typeof answer_ob.status === "undefined" )
									{
										alert("<?php echo translate_str_by_id(3751); ?>");
									}
									else
									{
										//Корректный парсинг ответа
										if(answer_ob.status == true)
										{
											//Здесь скачиваем файл
											var a = document.createElement("a");
											a.href = answer_ob.csv_path_rel;
											a.download = answer_ob.csv_name;
											a.click();
										}
										else
										{
											alert(answer_ob.message);
										}
									}							
								}
							});
						}
						// -----------------------------------------------------------------------------------------
						</script>
						
					</div>
				</div>
				
	
			</div>
		</div>
	</div>
	
	
	
	<script>
	<?php
	//Для управления процессом, нужно знать количество позиций в прайс-листе
	$items_count_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?;");
	$items_count_query->execute( array( $price_id ) );
	$items_count = $items_count_query->fetchColumn();
	?>
	var items_count = parseInt(<?php echo (int)$items_count; ?>);
	var process_complete_parts = '';//Для массива, в котором будет храниться учет обработанных частей
	var items_per_time = 100;//Сколько строк обрабатывать за один запрос
	var can_go_on = 1;//Флаг - можно продолжать
	var was_complete = 0;//Флаг - процесс был завершен
	// -----------------------------------------------------------------------------------
	//Нажатие кнопки "Проставить цены"
	function review_price()
	{
		if( was_complete )
		{
			if( !confirm('<?php echo translate_str_by_id(3752); ?>') )
			{
				return;
			}
		}
		
		
		//Проверка настроек
		//Процент
		var percent = document.getElementById('percent').value;
		percent = parseInt(percent*100)/100;
		//Значение не должно быть отрицательным
		if( percent < 0 )
		{
			alert('<?php echo translate_str_by_id(3753); ?>');
			return;
		}
		//Перечень прайс-листов, откуда брать цены
		var prices = [].concat( $("#prices").multipleSelect('getSelects') );
		if( prices.length == 0 )
		{
			alert('<?php echo translate_str_by_id(3754); ?>');
			return;
		}
		
		//Обнуляем переменные для управления процессом
		process_complete_parts = new Array();
		var process_parts_count = items_count/items_per_time;
		if( items_count%items_per_time > 0 )
		{
			process_parts_count++;
		}
		for(var i=0; i < process_parts_count; i++)
		{
			process_complete_parts.push(0);
		}
		can_go_on = 1;//Флаг - можно продолжать
		was_complete = 0;
		
		
		//Индикация процесса (СТАРТ)
		document.getElementById('progress_bar').setAttribute('aria-valuenow', '0');
		document.getElementById('progress_bar').setAttribute('style', 'width: 0%');
		document.getElementById('progress_text').innerHTML = '<?php echo translate_str_by_id(3755); ?> 0%';
		document.getElementById('work_result').setAttribute('style', 'display:none;');
		document.getElementById('options_div').setAttribute('style', 'display:none;');
		document.getElementById('progress_bar_div').setAttribute('style', 'display:block;');
		document.getElementById('cancel_button_div').setAttribute('style', 'display:block;');
		
		
		
		//Отправляем первый запрос
		send_request_for_one_part();
	}
	// -----------------------------------------------------------------------------------
	//Отправка запроса для одной части
	function send_request_for_one_part()
	{
		//Определяем, на каком шаге сейчас
		var is_start = 0;
		var is_end = 0;
		var start_from = 0;
		if( process_complete_parts[0] == 0 )
		{
			is_start = 1;
		}
		if( parseInt(process_complete_parts[ process_complete_parts.length-1 ]) == 0 && parseInt(process_complete_parts[ process_complete_parts.length-2 ]) == 1 )
		{
			is_end = 1;
		}
		for( var i=0; i < process_complete_parts.length; i++)
		{
			if( process_complete_parts[i] == 0 )
			{
				process_complete_parts[i] = 1;
				start_from = items_per_time*i;
				break;
			}
		}
		
		
		//Здесь уже можно не проверять
		var percent = document.getElementById('percent').value;
		var prices = [].concat( $("#prices").multipleSelect('getSelects') );
		
		jQuery.ajax({
			type: "POST",
			async: true,
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/price_review/ajax_price_review.php",
			dataType: "text",//Тип возвращаемого значения
			data: "start="+is_start+"&end="+is_end+"&price_id=<?php echo $price_id; ?>&base_mark="+document.getElementById('base_mark').value+"&plus_minus="+document.getElementById('plus_minus').value+"&percent="+percent+"&prices="+encodeURIComponent(JSON.stringify(prices))+"&from="+start_from+"&items_per_time="+items_per_time+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{				
				var answer_ob = JSON.parse(answer);
				
				//Если некорректный парсинг ответа
				if( typeof answer_ob.status === "undefined" )
				{
					alert("<?php echo translate_str_by_id(3756); ?>");
				}
				else
				{
					//Корректный парсинг ответа
					if(answer_ob.status == true)
					{
						//Индикация процесса (СЕРЕДИНА)
						var complete_value = 0;
						for( var i=0; i < process_complete_parts.length; i++)
						{
							if( process_complete_parts[i] == 0 )
							{
								complete_value = (i*100)/process_complete_parts.length;
								break;
							}
						}
						document.getElementById('progress_bar').setAttribute('aria-valuenow', complete_value);
						document.getElementById('progress_bar').setAttribute('style', 'width: '+complete_value+'%');
						document.getElementById('progress_text').innerHTML = '<?php echo translate_str_by_id(3755); ?> '+parseInt(complete_value)+'%';
						
						
						if( parseInt(process_complete_parts[ process_complete_parts.length-1 ]) == 1 )
						{
							//Индикация процесса (ЗАВЕРШЕНО)
							document.getElementById('progress_bar').setAttribute('aria-valuenow', '100');
							document.getElementById('progress_bar').setAttribute('style', 'width: 100%');
							document.getElementById('progress_text').innerHTML = '<?php echo translate_str_by_id(3755); ?> 100%';
							//Индикация процесса (РЕЗУЛЬТАТ)
							document.getElementById('items_count').innerHTML = answer_ob.result.items_count;
							document.getElementById('reviewed_yes').innerHTML = answer_ob.result.reviewed_yes;
							document.getElementById('reviewed_no').innerHTML = answer_ob.result.reviewed_no;
							document.getElementById('work_result').setAttribute('style', 'display:block;');
							document.getElementById('options_div').setAttribute('style', 'display:block;');
							document.getElementById('cancel_button_div').setAttribute('style', 'display:none;');
							
							was_complete = 1;
							return;
						}
						
						
						if( can_go_on == 1 )
						{
							send_request_for_one_part();
						}
					}
					else
					{
						alert(answer_ob.message + '. <?php echo translate_str_by_id(2515); ?>');
					}
				}							
			}
		});
	}
	// -----------------------------------------------------------------------------------
	</script>
	<?php
}
?>