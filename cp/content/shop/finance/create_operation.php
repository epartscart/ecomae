<?php
/**
 * Страничный скрипт для создания финансовой операции
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( isset($_POST["action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	if( $_POST["action"] == 'create' )
	{
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			//Проверка наличия всех полей
			if( !isset($_POST["user_id"]) || !isset($_POST["income"]) || !isset($_POST["operation_code"]) || !isset($_POST["amount"]) )
			{
				throw new Exception(translate_str_by_id(3259));
			}
			
			//Данные для создаваемой операции
			$user_id = (int)$_POST["user_id"];
			$time = time();
			$income = $_POST["income"];
			$operation_code = $_POST["operation_code"];
			$amount = $_POST["amount"];
			$office_id = 0;
			
			if( isset( $DP_Config->wholesaler ) )
			{
				$office_id = $_POST["office_id"];
				//Проверяем, может ли этот менеджер добавлять операции для данного магазина
				$check_office_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ?;");
				$check_office_query->execute( array($office_id, '%"'.DP_User::getAdminId().'"%') );
				if( $check_office_query->fetchColumn() != 1 )
				{
					throw new Exception(translate_str_by_id(3260));
				}
			}
			
			
			//Проверки полей
			
			
			//Пользователь должен быть либо равен 0 либо равен ID одного из существующих пользователей
			if( $user_id != 0 )
			{
				$user_check_query = $db_link->prepare('SELECT COUNT(*) FROM `users` WHERE `user_id` = ?;');
				$user_check_query->execute( array($user_id) );
				if( $user_check_query->fetchColumn() == 0 )
				{
					throw new Exception(translate_str_by_id(3261));
				}
			}
			
			
			//income должен быть равен 1 или 0
			if( $income != 0 && $income != 1 )
			{
				throw new Exception(translate_str_by_id(3262));
			}
			
			
			//operation_code должен быть: существующим, доступным для ручного добавления, соответствующим income
			$operation_check_query = $db_link->prepare('SELECT * FROM `shop_accounting_codes` WHERE `id` = ?;');
			$operation_check_query->execute( array($operation_code) );
			$operation_check_record = $operation_check_query->fetch();
			if( $operation_check_record == false )
			{
				throw new Exception(translate_str_by_id(3263));
			}
			else
			{
				if( !$operation_check_record['manual_available'])
				{
					throw new Exception(translate_str_by_id(3264));
				}
				
				if( $operation_check_record['income'] != $income )
				{
					throw new Exception(translate_str_by_id(3265));
				}
			}
			
			
			//amount - число больше 0
			if( is_numeric($amount) )
			{
				if( $amount <= 0 )
				{
					throw new Exception(translate_str_by_id(3266));
				}
			}
			else
			{
				throw new Exception(translate_str_by_id(3266));
			}
			

			
			if(  ! $db_link->prepare("INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `office_id`) VALUES (?,?,?, CAST(? AS DECIMAL(8,2)) ,?,?,?);")->execute( array($user_id, $time, $income, $amount, $operation_code, 1, $office_id) ) )
			{
				throw new Exception(translate_str_by_id(3267));
			}
			
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/account_operations/create?error_message=<?php echo urlencode($e->getMessage()); ?>";
			</script>
			<?php
			exit;
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/account_operations?success_message=<?php echo urlencode(translate_str_by_id(3268)); ?>";
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
				<a class="panel_a" href="javascript:void(0);" onclick="create_operation();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
    
    
    
    
    
    
    
    <script>
    var income_operations = new Array();//Виды приходных операций
    var issue_operations = new Array();//Виды расходных операций
    //Получаем виды операций
    <?php
	$accounting_codes_query = $db_link->prepare("SELECT * FROM `shop_accounting_codes` WHERE `manual_available` = ?;");
	$accounting_codes_query->execute( array(1) );
    while($accounting_code = $accounting_codes_query->fetch() )
    {
		$accounting_code["name"] = translate_str_by_id($accounting_code["name"]);
		
        if($accounting_code["income"] == 1)
        {
            ?>
            income_operations[income_operations.length] = new Object;
            income_operations[income_operations.length-1].id = <?php echo $accounting_code["id"]; ?>;
            income_operations[income_operations.length-1].name = "<?php echo translate_str_by_id(2533)." ".$accounting_code["id"].", ".$accounting_code["name"]; ?>";
            <?php
        }
        else
        {
            ?>
            issue_operations[issue_operations.length] = new Object;
            issue_operations[issue_operations.length-1].id = <?php echo $accounting_code["id"]; ?>;
            issue_operations[issue_operations.length-1].name = "<?php echo translate_str_by_id(2533)." ".$accounting_code["id"].", ".$accounting_code["name"]; ?>";
            <?php
        }
    }
    ?>
    
    
    
    // -------------------------------------------------------------------------------------------------------
    //Обработка выбора направления оперций
    function handleDirectionChoise()
    {
        var operations_names_html = "";
        if(document.getElementById("income").value == 1)
        {
            operations_names_html = "<select name=\"operation_code\" class=\"form-control\">";
            for(var i=0; i < income_operations.length; i++)
            {
                operations_names_html += "<option value=\""+income_operations[i].id+"\">"+income_operations[i].name+"</option>";
            }
            operations_names_html += "</select>";
        }
        else
        {
            operations_names_html = "<select name=\"operation_code\" class=\"form-control\">";
            for(var i=0; i < issue_operations.length; i++)
            {
                operations_names_html += "<option value=\""+issue_operations[i].id+"\">"+issue_operations[i].name+"</option>";
            }
            operations_names_html += "</select>";
        }
        
        
        document.getElementById("operations_names").innerHTML = operations_names_html;
    }
    </script>
    
	
	
	<form method="POST" name="create_operation_form">
		<input type="hidden" name="action" value="create" />
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3269); ?>
				</div>
				<div class="panel-body">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3245); ?>
						</label>
						<div class="col-lg-6">							
							<input type="text" id="user_id_search" value="" class="form-control" placeholder="<?php echo translate_str_by_id(3246); ?>" />
							<input type="hidden" id="user_id" name="user_id" value="" />
							<div id="user_id_show"></div>
							
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
											console.log("<?php echo translate_str_by_id(3270); ?>");
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
									document.getElementById('user_id_show').innerHTML = '<?php echo translate_str_by_id(3271); ?>';
									
									document.getElementById("user_id_search").setAttribute('class', 'form-control');
								}
								else
								{
									document.getElementById('user_id_show').innerHTML = '<?php echo translate_str_by_id(3247); ?>: '+user_info+' <i class="far fa-window-close" style="color:#F00;cursor:pointer;" onclick="handle_user_selected(\'\', \'\');"></i>';
									
									document.getElementById("user_id_search").setAttribute('class', 'hidden');
								}
								
							}
							// ------------------------------------------------------------------------
							<?php
							$user_id = '';
							$user_id_show = '';
							if( isset( $_GET["user_id"] ) )
							{
								$user_id = $_GET["user_id"];
								
								//SQL-подзапрос компонует строку с данными пользователя
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
								$SQL_SELECT_CUSTOMER = " IF( `user_id` = 0, 'ID 0, ".translate_str_by_id(3233)."', CONCAT( 'ID ', `user_id`, ', E-mail: ', (SELECT IF(`email`!='', `email`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `users`.`user_id` LiMIT 1 ), ', ".translate_str_by_id(3112).": ', (SELECT IF(`phone`!='', `phone`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `users`.`user_id` LiMIT 1 ) ".$users_profile_SQL." ) )";
								$user_id_show_query = $db_link->prepare("SELECT *, $SQL_SELECT_CUSTOMER AS `customer` FROM `users` WHERE `user_id` = ?;");
								$user_id_show_query->execute( array($user_id) );
								$user_id_show_record = $user_id_show_query->fetch();
								if( $user_id == 0 )
								{
									$user_id_show = 'ID 0, '.translate_str_by_id(3233);
								}
								else if( $user_id_show_record == false )
								{
									$user_id_show = "";
									$user_id = "";
								}
								else
								{
									$user_id_show = $user_id_show_record['customer'];
								}
							}
							?>
							handle_user_selected('<?php echo $user_id; ?>', '<?php echo $user_id_show; ?>');
							</script>
							
							
							
							
							
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3239); ?>
						</label>
						<div class="col-lg-6">
							<select name="income" id="income" onchange="handleDirectionChoise();" class="form-control">
								<option value="1"><?php echo translate_str_by_id(3240); ?></option>
								<option value="0"><?php echo translate_str_by_id(3241); ?></option>
							</select>
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3252); ?>
						</label>
						<div class="col-lg-6" id="operations_names">
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3272); ?>
						</label>
						<div class="col-lg-6">
							<input type="number" name="amount" id="amount_input" class="form-control" />
						</div>
					</div>
					
					
					<?php
					if( isset($DP_Config->wholesaler) )
					{
						?>
						<div class="hr-line-dashed col-lg-12"></div>
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(3248); ?>
							</label>
							<div class="col-lg-6">
								<select name="office_id" class="form-control">
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
							</div>
						</div>
						
						<?php
					}
					?>
					
					
				</div>
			</div>
		</div>
		
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	

    <script>
    handleDirectionChoise();//Обработка выбора направления оперций
    </script>
    
    
	
	<script>
	//Обработка нажатия "Сохранить"
	function create_operation()
	{
		//Проверки
		
		//Должн быть выбран покупатель
		if( document.getElementById('user_id').value == '' )
		{
			alert('<?php echo translate_str_by_id(3273); ?>');
			return;
		}
		
		
		//Сумма операции
		var pay_value = document.getElementById('amount_input').value;
		pay_value = parseInt(pay_value*100)/100;
		//1. Должна быть указана сумма
		if( pay_value == '' )
		{
			alert('<?php echo translate_str_by_id(3274); ?>');
			return;
		}
		//2. Сумма не должна быть отрицательной, не должна быть равна 0
		if( pay_value <= 0 )
		{
			alert('<?php echo translate_str_by_id(3275); ?>');
			return;
		}

		
		document.forms['create_operation_form'].submit();
	}
	</script>
    
    
    
    <?php
}
?>