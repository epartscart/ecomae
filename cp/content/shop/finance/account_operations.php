<?php
/**
 * Страничный скрипт для управления финансовыми операциями пользователей
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if(isset($_POST["action"]))
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
	$time_from = "";//1. Время с
	$time_to = "";//2. Время по
	$operation_code = -1;//3. Код операции
	$income = -1;//4. Напрвление операции
	$user_id = "";//5. Покупатель
	$user_id_show = "";//5. Покупатель для показа
	$order_id = "";//Привязка к заказу
	$office_id = -1;//Магазин
	
	
	//Получаем текущие значения фильтра:
	$account_operations_filter = NULL;
	if( isset($_COOKIE["account_operations_filter"]) )
	{
		$account_operations_filter = $_COOKIE["account_operations_filter"];
	}
	if($account_operations_filter != NULL)
	{
		$account_operations_filter = json_decode($account_operations_filter, true);
		$time_from = $account_operations_filter["time_from"];
		$time_to = $account_operations_filter["time_to"];
		$operation_code = $account_operations_filter["operation_code"];
		$income = $account_operations_filter["income"];
		$user_id = $account_operations_filter["user_id"];
		
		if( isset($account_operations_filter["order_id"]) )
		{
			$order_id = $account_operations_filter["order_id"];
		}
		else
		{
			$order_id = "";
		}
		
		
		if( isset($DP_Config->wholesaler) )
		{
			if( isset($account_operations_filter["office_id"]) )
			{
				$office_id = $account_operations_filter["office_id"];
			}
		}
		
		
		//Покупатель для показа
		if( $user_id != "" )
		{
			//SQL-подзапрос компонует строку с данными пользователя
			//Формируем часть SQL-запроса для покупателя (отдельно, чтобы можно было использовать и для WHERE)
			//Формируем подзапрос для значений профиля пользователя (только для тех полей, которые выводятся в таблицу пользователей в менеджер пользователей колонками)
			$users_profile_SQL = "";
			$users_profile_fields_query = $db_link->prepare("SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1;");
			$users_profile_fields_query->execute();
			while( $users_profile_field = $users_profile_fields_query->fetch() )
			{
				if( $users_profile_SQL != "" )
				{
					$users_profile_SQL = $users_profile_SQL.",";
				}
				
				//Допустимы только буквы и знаки нижнего подчеркивания
				$field_name = str_replace( array(' ', '#', '-', "'", '"'), '', $users_profile_field["name"] );
				
				$users_profile_SQL = $users_profile_SQL." IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `users`.`user_id`), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `users`.`user_id`)),'') ";
			}
			if( $users_profile_SQL != "" )
			{
				$users_profile_SQL = ",".$users_profile_SQL;
			}
			//SQL-подзапрос компонует строку с данными пользователя
			$SQL_SELECT_CUSTOMER = " IF( `user_id` = 0, 'ID 0, Незарегистрированный', CONCAT( 'ID ', `user_id`, ', E-mail: ', (IF(`email`!='', `email`, 'Не указан')), ', Телефон: ', (IF(`phone`!='', `phone`, 'Не указан')) ".$users_profile_SQL." ) )";
			$user_id_show_query = $db_link->prepare("SELECT *, $SQL_SELECT_CUSTOMER AS `customer` FROM `users` WHERE `user_id` = ?;");
			$user_id_show_query->execute( array($user_id) );
			$user_id_show_record = $user_id_show_query->fetch();
			if( $user_id == 0 )
			{
				$user_id_show = 'ID 0, '.translate_str_by_id(3233);
			}
			else if( $user_id_show_record == false )
			{
				$user_id_show = $user_id;
			}
			else
			{
				$user_id_show = $user_id_show_record['customer'];
			}
		}
		else
		{
			$user_id_show = '';
		}
	}
	?>

    <link rel="stylesheet" href="/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/assets/modal.css"><!--//Статистика-->
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
			

				<?php
				$add_operation_arg = "";
				if( $user_id > 0 )
				{
					$add_operation_arg = "?user_id=".$user_id;
				}
				
				//Добавить поступление/списание
				print_backend_button( array("background_color"=>"#63ce1c", "fontawesome_class"=>"fas fa-plus", "caption"=>translate_str_by_id(3234), "url"=>"/".$DP_Config->backend_dir."/shop/finance/account_operations/create".$add_operation_arg) );
				?>
				
				
				
				<?php
				//Редактор видов операций
				print_backend_button( array("background_color"=>"#3498db", "fontawesome_class"=>"fas fa-align-justify", "caption"=>translate_str_by_id(3235), "url"=>"/".$DP_Config->backend_dir."/shop/finance/operations_editor") );
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
				<?php echo translate_str_by_id(3236); ?>
			</div>
			<div class="panel-body filter_panel">			
				
				<div class="form-group col-lg-4">
					<label for="" class="col-lg-4 control-label">
						<?php echo translate_str_by_id(3237); ?>
					</label>
					<div class="col-lg-8">
						<div style="position:relative;height:34px;">
							<input style="position:absolute; z-index:2; opacity:0;" type="text"  id="time_from" value="<?php echo $time_from; ?>" class="form-control" />
							<input style="position:absolute; z-index:1; <?=(!empty($time_from))?'background:#b9fcab;':'';?>" type="text" id="time_from_show" class="form-control" />
							<script>
							//Инициализируем datetimepicker
							jQuery("#time_from").datetimepicker({
								lang:"<?php echo $multilang_params['lang']; ?>",
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
								if($time_from != "")
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
				
				
				
				<div class="form-group col-lg-4">
					<label for="" class="col-lg-4 control-label">
						<?php echo translate_str_by_id(3238); ?>
					</label>
					<div class="col-lg-8">
						<div style="position:relative;height:34px;">
							<input style="position:absolute; z-index:2; opacity:0;" type="text"  id="time_to" value="<?php echo $time_to; ?>" class="form-control" />
							<input style="position:absolute; z-index:1; <?=(!empty($time_to))?'background:#b9fcab;':'';?>" type="text" id="time_to_show" class="form-control" />
							<script>
							//Инициализируем datetimepicker
							jQuery("#time_to").datetimepicker({
								lang:"<?php echo $multilang_params['lang']; ?>",
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
								if($time_to != "")
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
				
				<div class="row"></div>
	
				<div class="form-group col-lg-4">
					<label for="" class="col-lg-4 control-label">
						<?php echo translate_str_by_id(3239); ?>
					</label>
					<div class="col-lg-8">
						<select <?=($income >= 0)?'style="background:#b9fcab;"':'';?> id="income" class="form-control">
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<option value="1"><?php echo translate_str_by_id(3240); ?></option>
							<option value="0"><?php echo translate_str_by_id(3241); ?></option>
						<select>
						<script>
							document.getElementById("income").value = <?php echo $income; ?>;
						</script>
					</div>
				</div>
				

				
				<div class="form-group col-lg-4">
					<label for="" class="col-lg-4 control-label">
						<?php echo translate_str_by_id(3242); ?>
					</label>
					<div class="col-lg-8">
						<select <?=($operation_code >= 0)?'style="background:#b9fcab;"':'';?> id="operation_code" class="form-control">
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<?php
							$accounting_codes_query = $db_link->prepare("SELECT * FROM `shop_accounting_codes` ORDER BY `id`;");
							$accounting_codes_query->execute();
							while($accounting_code = $accounting_codes_query->fetch() )
							{
								$accounting_code['name'] = translate_str_by_id($accounting_code['name']);
								
								$selected = "";
								if($operation_code == $accounting_code["id"])
								{
									$selected = "selected=\"selected\"";
								}
								
								$direction = translate_str_by_id(3240);
								if( $accounting_code['income']==0 )
								{
									$direction = translate_str_by_id(3241);
								}
								?>
								<option value="<?php echo $accounting_code["id"]; ?>" <?php echo $selected; ?>><?php echo translate_str_by_id(2533)." ".$accounting_code["id"]." ".$accounting_code["name"]." (".$direction.")"; ?></option>
								<?php
							}
							?>
						</select>
					</div>
				</div>
				
				
				
				<div class="form-group col-lg-4">
					<label for="" class="col-lg-4 control-label">
						<?php echo translate_str_by_id(3243); ?>
					</label>
					<div class="col-lg-8">
						<input <?=(!empty($order_id))?'style="background:#b9fcab;"':'';?> type="text" id="order_id" value="<?php echo $order_id; ?>" class="form-control" placeholder="<?php echo translate_str_by_id(3244); ?>" />
					</div>
				</div>

				<div class="row"></div>
				
				<div class="form-group col-lg-4">
					<label for="" class="col-lg-4 control-label">
						<?php echo translate_str_by_id(3245); ?>
					</label>
					<div class="col-lg-8">
						<input type="text" id="user_id_search" value="" class="form-control" placeholder="<?php echo translate_str_by_id(3246); ?>" />
						<input type="hidden" id="user_id" value="<?php echo $user_id; ?>" />
						<div style="" id="user_id_show" class=""></div>
					</div>
				</div>
				<script>
				//Выбор покупателя
				/*
				- пользователь начинает вводить данные покупателя (ФИО, ID, контакты и т.д.)
				- под полем ввода начинают предлагаться варианты
				- пользователь должен выбрать один из вариантов
				- после этого в поле отображаются данные покупатеоя, а в hidden-поле записывается его ID

				- при инициализации, id покупателя записывается в hidden-поле, а данные покупателя в видимое поле
				*/
				// ------------------------------------------------------------------------
				//Поле ввода города привязки - обработка заполнения
				jQuery("#user_id_search").autocomplete({
					source: function(request, response)
					{
						//Нужно ввести достаточное количество знаков для запуска autocomplete
						if( jQuery("#user_id_search").val().length < 2 )
						{
							//return;
						}
						
						jQuery.ajax({
							type: "POST",
							async: true, //Запрос асинхронный
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/users/ajax_get_users_autocomplete.php",
							dataType: "text",//Тип возвращаемого значения
							data: "input_str="+jQuery("#user_id_search").val()+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								//console.log(answer);
								
								answer_ob = JSON.parse(answer);

								if( answer_ob["status"] == undefined )
								{
									//console.log("Ошибка получения подходящих вариантов");
									//console.log(answer);
								}
								else
								{
									if( answer_ob["status"] == false )
									{
										//console.log( "Ошибка! " + answer_ob["message"] );
									}
									else//Возможные варианты успешно получены
									{
										if( answer_ob.vars.length == 0 )
										{
											//console.log("Нет подходящих вариантов");
											return;
										}
										
										response(jQuery.map( answer_ob.vars, function( item ) {
											return {
												label: item.user_info,
												object: item,
												value: item.user_info
											}
										}));
									}
								}
							},
							error: function(msg)
							{
								//console.log("Ошибка получения ответа от сервера");
							}
						});
					},
					//Обработка выбора пользователя:
					select: function (event, ui) 
					{
						var user_var = ui.item.object;
						
						handle_user_selected(user_var.user_id+'', user_var.user_info);
						
						return false;
					}
				});
				// ------------------------------------------------------------------------
				//Обработка текущего выбора пользователя
				function handle_user_selected(user_id, user_info)
				{
					//Поисковую строку очищаем
					jQuery("#user_id_search").val('');
					
					//Здесь указываем ID пользователя в hidden-поле
					jQuery("#user_id").val(user_id);
					
					//Здесь указываем индикацию текущего выбора
					if( user_id == '' )
					{
						document.getElementById('user_id_show').innerHTML = '';
						
						document.getElementById("user_id_search").setAttribute('class', 'form-control');
						document.getElementById("user_id_show").setAttribute('style', '');
						//$("#user_id_show").addClass('hidden');
					}
					else
					{
						document.getElementById('user_id_show').innerHTML = '<?php echo translate_str_by_id(3247); ?>: '+user_info+' <i class="far fa-window-close" style="color:#F00;cursor:pointer;" onclick="handle_user_selected(\'\', \'\');"></i>';
						
						document.getElementById("user_id_search").setAttribute('class', 'hidden');
						document.getElementById("user_id_show").setAttribute('style', 'background: #b9fcab; height: 34px; padding: 6px 12px; font-size: 14px; line-height: 1.42857143; border: 1px solid #e4e5e7; border-radius: 4px; white-space: nowrap; position: absolute;');
						//$("#user_id_show").removeClass('hidden');
					}
					
				}
				// ------------------------------------------------------------------------
				handle_user_selected('<?php echo $user_id; ?>', '<?php echo $user_id_show; ?>');
				</script>
				
				
				
				<?php
				if( isset( $DP_Config->wholesaler ) )
				{
					?>
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-2 control-label">
							<?php echo translate_str_by_id(3248); ?>
						</label>
						<div class="col-lg-10">
							<select id="office_id" class="form-control">
								<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
								<?php
								$offices_query = $db_link->prepare("SELECT * FROM `shop_offices` WHERE `users` LIKE ?;");
								$offices_query->execute( array('%"'.DP_User::getAdminId().'"%') );
								while( $office = $offices_query->fetch() )
								{
									?>
									<option value="<?php echo $office['id']; ?>"><?php echo $office['caption'].', '.$office['city'].', '.$office['address'].'. Тел. '.$office['phone']; ?></option>
									<?php
								}
								?>
							</select>
							<script>
							document.getElementById('office_id').value = '<?php echo $office_id; ?>';
							</script>
						</div>
					</div>
					<?php
				}
				?>
				

			</div>
			<script>
			$(".filter_panel .form-control").keyup(function(event){
				if(event.keyCode == 13){
					filterOperations();
				}
			});
			</script>
			<div class="panel-footer">
				<button class="btn btn-success" style="margin-top:3px;" type="button" onclick="filterOperations();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
				<button class="btn btn-primary" style="margin-top:3px;" type="button" onclick="unsetFilterOperations();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2233); ?></button>
			</div>
		</div>
	</div>


    <!--//Статистика-->
    <script src="/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/assets/main.js"></script>
    <script>
        function closeCustomerModalInfo(user_id) {
            if (window.event.srcElement.id == 'customer-modal-info-' + user_id || window.event.srcElement.id == 'close-customer-modal-info-' + user_id)
                document.querySelector('#customer-modal-info-' + user_id).classList.remove('customer-modal-info-wrapper-show');
        }

        function showCustomerModalInfo(customer_id)
        {
            jQuery.ajax({
                type: "POST",
                async: false, //Запрос синхронный
                url: "/<?php echo $DP_Config->backend_dir; ?>/content/users/statistics/frontAjax/ajax_loadUserModal.php",
                dataType: "json",//Тип возвращаемого значения
                data: "customer_id="+customer_id+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer){
                    if(answer.status == true)
                    {
                        document.querySelector('#customer-modal-info-' + customer_id).classList.add('customer-modal-info-wrapper-show');
                        document.querySelector('#customer-modal-info-' + customer_id).innerHTML = answer.modal;
                    }
                    else
                    {
                        alert("<?php echo translate_str_by_id(3541); ?>");
                    }
                }
            });
        }
    </script>
    <!--END //Статистика-->
    
    
    <script>

    // ------------------------------------------------------------------------------------------------
    //Устновка cookie в соответствии с фильтром
    function filterOperations()
    {
        var account_operations_filter = new Object;
        
        account_operations_filter.time_from = document.getElementById("time_from").value;
        account_operations_filter.time_to = document.getElementById("time_to").value;
        account_operations_filter.income = document.getElementById("income").value;
        account_operations_filter.operation_code = document.getElementById("operation_code").value;
        account_operations_filter.user_id = document.getElementById("user_id").value;
        account_operations_filter.order_id = document.getElementById("order_id").value;
		
		<?php
		if( isset( $DP_Config->wholesaler ) )
		{
			?>
			account_operations_filter.office_id = document.getElementById("office_id").value;
			<?php
		}
		?>
		
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "account_operations_filter="+JSON.stringify(account_operations_filter)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations';
    }
    // ------------------------------------------------------------------------------------------------
    //Снять все фильтры
    function unsetFilterOperations()
    {
        var account_operations_filter = new Object;

        account_operations_filter.time_from = "";
        account_operations_filter.time_to = "";
        account_operations_filter.income = -1;
        account_operations_filter.operation_code = -1;
        account_operations_filter.user_id = "";
        account_operations_filter.order_id = "";
        account_operations_filter.office_id = -1;
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() - 15552000 * 1000);
        document.cookie = "account_operations_filter="+JSON.stringify(account_operations_filter)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations';
    }
    // ------------------------------------------------------------------------------------------------
    </script>
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    <script>
    // ------------------------------------------------------------------------------------------------
    //Установка куки сортировки
    function sortOperationsItems(field)
    {
        var asc_desc = "asc";//Направление по умолчанию
        
        //Берем из куки текущий вариант сортировки
        var current_sort_cookie = getCookie("account_operations_sort");
        if(current_sort_cookie != undefined)
        {
            current_sort_cookie = JSON.parse(getCookie("account_operations_sort"));
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
        
        
        var account_operations_sort = new Object;
        account_operations_sort.field = field;//Поле, по которому сортировать
        account_operations_sort.asc_desc = asc_desc;//Направление сортировки
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "account_operations_sort="+JSON.stringify(account_operations_sort)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations';
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
    //Переход на другую страницу заказа
    function goToPage(need_page)
    {
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "account_operations_need_page="+need_page+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations';
    }
    // ------------------------------------------------------------------------------------------------
    </script>
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3249); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr>
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();" /></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('id');" id="id_sorter">ID</a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('amount');" id="amount_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('order_id');" id="order_id_sorter"><?php echo translate_str_by_id(3243); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('income');" id="income_sorter"><?php echo translate_str_by_id(3239); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('user_id');" id="user_id_sorter"><?php echo translate_str_by_id(3245); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('operation_code');" id="operation_code_sorter"><?php echo translate_str_by_id(3252); ?></a></th>
								
								<?php
								if( isset($DP_Config->wholesaler) )
								{
									?>
									<th><a href="javascript:void(0);" onclick="sortOperationsItems('office_caption');" id="office_caption_sorter"><?php echo translate_str_by_id(3248); ?></a></th>
									<?php
								}
								?>
								
							</tr>
						</thead>
						<tbody>
						<script>
							<?php
							//Определяем текущую сортировку и обозначаем ее:
							$account_operations_sort = NULL;
							if( isset($_COOKIE["account_operations_sort"]) )
							{
								$account_operations_sort = $_COOKIE["account_operations_sort"];
							}
							$sort_field = "id";
							$sort_asc_desc = "desc";
							if($account_operations_sort != NULL)
							{
								$account_operations_sort = json_decode($account_operations_sort, true);
								$sort_field = $account_operations_sort["field"];
								$sort_asc_desc = $account_operations_sort["asc_desc"];
							}
							
							if( strtolower($sort_asc_desc) == "asc" )
							{
								$sort_asc_desc = "asc";
							}
							else
							{
								$sort_asc_desc = "desc";
							}
							
							$sort_fields_exeptable = array('id', 'time', 'user_id', 'amount', 'operation_code', 'income', 'order_id');
							if( isset( $DP_Config->wholesaler ) )
							{
								$sort_fields_exeptable[] = 'office_caption';
							}
							
							if( array_search( $sort_field, $sort_fields_exeptable ) == false )
							{
								$sort_field = "id";
							}
							
							?>
							document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
						</script>
						
						
						<?php
						//Настройки пагинации
						$rows_per_page = $DP_Config->list_page_limit;//Количество строк на страницу (SQL-параметр)
						$row_from = 0;//С какой строки начать (SQL-параметр)
						$current_page = 0;
						if( isset($_GET["page"]) )
						{
							$current_page = (int)$_GET["page"];
						}
						$row_from = $current_page * $rows_per_page;
						
						
						
						
						//Формируем часть SQL-запроса для покупателя (отдельно, чтобы можно было использовать и для WHERE)
						//Формируем подзапрос для значений профиля пользователя (только для тех полей, которые выводятся в таблицу пользователей в менеджер пользователей колонками)
						$users_profile_SQL = "";
						$users_profile_fields_query = $db_link->prepare("SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1;");
						$users_profile_fields_query->execute();
						while( $users_profile_field = $users_profile_fields_query->fetch() )
						{
							if( $users_profile_SQL != "" )
							{
								$users_profile_SQL = $users_profile_SQL.",";
							}
							
							//Допустимы только буквы и знаки нижнего подчеркивания
							$field_name = str_replace( array(' ', '#', '-', "'", '"'), '', $users_profile_field["name"] );
							
							$users_profile_SQL = $users_profile_SQL." IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `shop_users_accounting`.`user_id`), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `shop_users_accounting`.`user_id`)),'') ";
						}
						if( $users_profile_SQL != "" )
						{
							$users_profile_SQL = ",".$users_profile_SQL;
						}
						//SQL-подзапрос компонует строку с данными пользователя
						$SQL_SELECT_CUSTOMER = " IF( `user_id` = 0, 'ID 0, ".translate_str_by_id(3233)."', CONCAT( 'ID ', `user_id`, ', E-mail: ', (SELECT IF(`email`!='', `email`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `shop_users_accounting`.`user_id` LiMIT 1 ), ', ".translate_str_by_id(1312).": ', (SELECT IF(`phone`!='', `phone`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `shop_users_accounting`.`user_id` LiMIT 1 ) ".$users_profile_SQL." ) )";
						
						
						
						$binding_values_conditions = array();
						$WHERE_CONDITIONS = " `active` = 1 ";
						$binding_values_conditions_balance = array();
						$WHERE_CONDITIONS_BALANCE = " `active` = 1 ";//Отдельная строка для условия - для подсчета баланса. Здесь нет условий по полю income
						//Ставим ПОЛЬЗОВАТЕЛЬСКИЕ фильтры
						$account_operations_filter = NULL;
						if( isset($_COOKIE["account_operations_filter"]) )
						{
							$account_operations_filter = $_COOKIE["account_operations_filter"];
						}
						if($account_operations_filter != NULL)
						{
							$account_operations_filter = json_decode($account_operations_filter, true);
							
							//1. Время с
							if($account_operations_filter["time_from"] != "")
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `time` > ?";
								array_push($binding_values_conditions, $account_operations_filter["time_from"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `time` > ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["time_from"]);
							}

							//2. Время по
							if($account_operations_filter["time_to"] != "")
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `time` < ?";
								array_push($binding_values_conditions, $account_operations_filter["time_to"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `time` < ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["time_to"]);
							}

							//3. income
							if($account_operations_filter["income"] != "" && $account_operations_filter["income"] != -1)
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `income` = ?";
								array_push($binding_values_conditions, $account_operations_filter["income"]);
							}
							
							//4. operation_code
							if($account_operations_filter["operation_code"] != 0 && $account_operations_filter["operation_code"] != -1)
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `operation_code` = ?";
								array_push($binding_values_conditions, $account_operations_filter["operation_code"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `operation_code` = ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["operation_code"]);
							}
							
							//5. user_id
							if($account_operations_filter["user_id"] != "" )
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `user_id` = ?";
								array_push($binding_values_conditions, $account_operations_filter["user_id"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `user_id` = ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["user_id"]);
							}
							
							
							//6 order_id
							if( isset($account_operations_filter["order_id"]) )
							{
								if($account_operations_filter["order_id"] != "" )
								{
									if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
									$WHERE_CONDITIONS .= " `order_id` = ?";
									array_push($binding_values_conditions, $account_operations_filter["order_id"]);
									
									if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
									$WHERE_CONDITIONS_BALANCE .= " `order_id` = ?";
									array_push($binding_values_conditions_balance, $account_operations_filter["order_id"]);
								}
							}
							
							
							
							//7 office_id
							if( isset( $DP_Config->wholesaler ) )
							{
								//Если менеджер указал конкретный магазин
								if($account_operations_filter["office_id"] != 0 && $account_operations_filter["office_id"] != -1)
								{
									if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
									$WHERE_CONDITIONS .= " `office_id` = ?";
									array_push($binding_values_conditions, $account_operations_filter["office_id"]);
									
									if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
									$WHERE_CONDITIONS_BALANCE .= " `office_id` = ?";
									array_push($binding_values_conditions_balance, $account_operations_filter["office_id"]);
								}
							}
							
						}
						if( isset( $DP_Config->wholesaler ) )
						{
							//Магазин в любом случае должен быть доступен данному менеджеру
							if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
							$WHERE_CONDITIONS .= " `office_id` IN (SELECT `id` FROM `shop_offices` WHERE `users` LIKE ?)";
							array_push($binding_values_conditions, '%"'.DP_User::getAdminId().'"%' );
							
							if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
							$WHERE_CONDITIONS_BALANCE .= " `office_id` IN (SELECT `id` FROM `shop_offices` WHERE `users` LIKE ?)";
							array_push($binding_values_conditions_balance, '%"'.DP_User::getAdminId().'"%');
						}
						if($WHERE_CONDITIONS != "") $WHERE_CONDITIONS = "WHERE ".$WHERE_CONDITIONS;
						if($WHERE_CONDITIONS_BALANCE != "")
						{
							$WHERE_CONDITIONS_BALANCE_INCOME = "WHERE ".$WHERE_CONDITIONS_BALANCE." AND `income` = 1";
							$WHERE_CONDITIONS_BALANCE_ISSUE = "WHERE ".$WHERE_CONDITIONS_BALANCE." AND `income` = 0";
						}
						else
						{
							$WHERE_CONDITIONS_BALANCE_INCOME = "WHERE `income` = 1";
							$WHERE_CONDITIONS_BALANCE_ISSUE = "WHERE `income` = 0";
						}
						
						
						//Формируем запрос
						$SQL_operation_name = "(SELECT `name` FROM `shop_accounting_codes` WHERE `id` = `shop_users_accounting`.`operation_code`)";
						
						
						//Подсчет сальдо
						$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` $WHERE_CONDITIONS_BALANCE_INCOME), 0)";
						$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` $WHERE_CONDITIONS_BALANCE_ISSUE),0)";
						
						
						$SQL_SELECT = "SELECT SQL_CALC_FOUND_ROWS *, $SQL_operation_name AS `name`, ($INCOME_SQL-$ISSUE_SQL) AS `balance`, ($SQL_SELECT_CUSTOMER) AS `customer`, CAST(`amount` AS DECIMAL(8,2) ) AS `amount`, IFNULL( (SELECT CONCAT(`caption`, ', ', `city`, ', ', `address`, ', ', `phone`) FROM `shop_offices` WHERE `id` = `shop_users_accounting`.`office_id`), 'Без привязки' ) AS `office_caption` FROM `shop_users_accounting` $WHERE_CONDITIONS ORDER BY `".$sort_field."` ".$sort_asc_desc." LIMIT ".$row_from.",".$rows_per_page;
						
						//var_dump($SQL_SELECT);
						
						$binding_values = array_merge($binding_values_conditions_balance, $binding_values_conditions_balance);
						$binding_values = array_merge($binding_values, $binding_values_conditions);
						
						$elements_query = $db_link->prepare($SQL_SELECT);
						$elements_query->execute($binding_values);
						
						$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
						$elements_count_rows_query->execute();
						$elements_count_rows = $elements_count_rows_query->fetchColumn();
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						
						$saldo = "no";
						while($element_record = $elements_query->fetch())
						{
							//Для Javascript
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
							
							
							if( $saldo == "no" )
							{
								$saldo = $element_record['balance'];
							}
							
							$amount = $element_record["amount"];
							$css_sub_color = "";
							if($element_record["income"] == 1)
							{
								$css_sub_color = "background-color:#d4ffd0;";
								$amount = "+".$amount;
							}
							else
							{
								$css_sub_color = "background-color:#ffecec;";
								$amount = "-".$amount;
							}
							
							
							$id = $element_record["id"];
							$time = $element_record["time"];
							$user_id = $element_record["user_id"];
							$name = translate_str_by_id($element_record["name"]);
							$customer_id = $element_record["user_id"];//Статистика
							?>
							
							
							
							<tr id="operation_record_<?php echo $id; ?>" style="<?php echo $css_sub_color; ?>">
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>" /></td>
								<td><?php echo $id; ?></td>
								<td><?php echo date("d.m.Y", $time)."<br>".date("G:i", $time); ?></td>
								<td><?php echo $amount; ?></td>
								<td>
								<?php
								if( $element_record["order_id"] > 0 )
								{
									?>
									<a style="text-decoration:underline;" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $element_record["order_id"]; ?>" target="_blank"><?php echo $element_record["order_id"]; ?></a>
									<?php
								}
								else
								{
									echo "-";
								}
								?>
								</td>
								<td>
								<?php
								if($element_record["income"] == 1)
								{
									echo translate_str_by_id(3240);
								}
								else
								{
									echo translate_str_by_id(3241);
								}
								?>
								</td>
								<td>
								<?php
								if( empty($element_record["customer"]) )
								{
									echo "ID ".$element_record["user_id"].", ".translate_str_by_id(3254);
								}
								else
								{
									if( $element_record["user_id"] > 0 )
									{
										?>
                                        <?php include $_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/users/statistics/modal.php';//Статистика?><a style="text-decoration:underline;" target="_blank" href="/<?php echo $DP_Config->backend_dir; ?>/users/usermanager/user?user_id=<?php echo $element_record["user_id"]; ?>"><?php echo $element_record["customer"]; ?></a>
										<?php
									}
									else
									{
										echo $element_record["customer"];
									}
								}
								?>
								</td>
								<td id="name_<?php echo $id; ?>"><?php echo translate_str_by_id(2533)." ".$element_record["operation_code"].", ".$name; ?></td>
								
								<?php
								if( isset( $DP_Config->wholesaler ) )
								{
									?>
									<td id="name_<?php echo $id; ?>"><?php echo $element_record["office_caption"]; ?></td>
									<?php
								}
								?>
								
							</tr>
							<?php
						}//while()
						?>
						</tbody>
						<tfoot>
							<tr>
								<?php
								$colspan="8";
								if( isset( $DP_Config->wholesaler ) )
								{
									$colspan="9";
								}
								?>
								<td colspan="<?php echo $colspan; ?>" style="text-align:center;">
									<div class="btn-group">
										<?php
										//КНОПКА "ВЛЕВО"
										$to_left_disabled = "";
										if( $current_page == 0 )
										{
											$to_left_disabled = "disabled";
										}
										?>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=0"><?php echo translate_str_by_id(4038); ?></a>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $current_page-1; ?>"><i class="fa fa-chevron-left"></i></a>
										
										
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
											<a class="btn btn-default <?php echo $active; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $i; ?>"><?php echo $i+1; ?></a>
											<?php
										}
										
										
										//КНОПКА "ВПРАВО"
										$to_right_disabled = "";
										if( ($current_page+1) == $pages_count )
										{
											$to_right_disabled = "disabled";
										}
										?>
										<a class="btn btn-default <?php echo $to_right_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $current_page+1; ?>"><i class="fa fa-chevron-right"></i></a>
										<a class="btn btn-default <?php echo $to_right_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $pages_count-1; ?>"><?php echo translate_str_by_id(2184); ?></a>
									</div>
									
									<br>
									<div style="text-align:left;">
									<?php echo translate_str_by_id(3255); ?>: <?php echo $elements_count_rows; ?>, <?php echo translate_str_by_id(3256); ?>: <?php echo $rows_per_page; ?>, <?php echo translate_str_by_id(3257); ?>: <?php echo $pages_count; ?>
									</div>
								</td>
							</tr>
						</tfoot>
					</table>
				</div>
			</div>
			<?php
			if($saldo === 'no'){
				$saldo = 0;
			}
			?>
			<div class="panel-footer">
                <?php echo translate_str_by_id(3258); ?>: <?php echo number_format($saldo, 2, '.', ''); ?>
            </div>
		</div>
	</div>
	
	
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
}//else//Действий нет - выводим страницу
?>