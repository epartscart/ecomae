<?php
//Скрипт страницы создания/редактирования финансовых операций
defined('_ASTEXE_') or die('No access');



if( isset($_POST['action']) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	if( $_POST['action'] == 'save_operation' )
	{
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			//Проверки входных пераметров
			if( !isset($_POST['operation_id']) || !isset($_POST['income']) || !isset($_POST['name']) || !isset($_POST['name_lang_str_id']) )
			{
				throw new Exception(translate_str_by_id(3288));
			}
			if( ( $_POST['income']!=0 && $_POST['income']!=1 ) || empty($_POST['name']) || !is_numeric($_POST['name_lang_str_id']) )
			{
				throw new Exception(translate_str_by_id(3289));
			}
			
			$operation_id = $_POST['operation_id'];
			$income = $_POST['income'];
			$name = htmlentities($_POST['name']);
			
			//Мультиязычность
			$name_lang_str_id = $_POST['name_lang_str_id'];
			//Обработка мультиязычности
			//Вызов функции сохранения строки в виде перевода на текущий язык панели управления (кастомный алгоритм). В ответ вернется ID этой строки, который и нужно будет сохранить
			$name = save_custom_translation($name_lang_str_id, $name);
			if( $name == 0 )
			{
				throw new Exception("Error executing custom strings function");
			}
			
			
			
			if( $operation_id == 0 )
			{
				//Создаем новый вид операции
				$success_message = translate_str_by_id(3290);
				
				if( !$db_link->prepare( 'INSERT INTO `shop_accounting_codes` (`income`, `name`, `manual_available`) VALUES (?,?,?);' )->execute( array($income, $name, 1) ) )
				{
					throw new Exception(translate_str_by_id(3291));
				}
				
				$operation_id = $db_link->lastInsertId();
				
				if( !$operation_id )
				{
					throw new Exception(translate_str_by_id(3292));
				}
			}
			else
			{
				//Редактируем существующую операцию
				$success_message = translate_str_by_id(3293);
				
				
				//Перед редактированием проверяем - что вид операции не является системный или что операции данного вида еще не совершались
				$operation_query = $db_link->prepare('SELECT *, (SELECT COUNT(*) FROM `shop_users_accounting` WHERE `operation_code` = `shop_accounting_codes`.`id` ) AS `used` FROM `shop_accounting_codes` WHERE `id` = ?;');
				$operation_query->execute( array($operation_id) );
				$operation = $operation_query->fetch();
				
				if( $operation == false )
				{
					throw new Exception(translate_str_by_id(3263));
				}
				if( $operation['system'] )
				{
					throw new Exception(translate_str_by_id(3294));
				}
				if( $operation['used'] > 0 )
				{
					throw new Exception(translate_str_by_id(3295));
				}
				
				if( !$db_link->prepare('UPDATE `shop_accounting_codes` SET `income` = ?, `name` = ? WHERE `id` = ?;')->execute( array($income, $name, $operation_id) ) )
				{
					throw new Exception(translate_str_by_id(3296));
				}
			}
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/operations_editor/operation?operation_id=<?php echo $operation_id; ?>&error_message=<?php echo urlencode($e->getMessage()); ?>";
			</script>
			<?php
			exit;
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/operations_editor/operation?operation_id=<?php echo $operation_id; ?>&success_message=<?php echo urlencode($success_message); ?>";
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
				
				
				<?php
				//Сохранить
				print_backend_button( array("background_color"=>"#63ce1c", "fontawesome_class"=>"fas fa-save", "caption"=>translate_str_by_id(2114), "url"=>"javascript:void(0);", "onclick"=>"save_form_submit();") );
				?>
				
				
				
				<?php
				//Редактор видов операций
				print_backend_button( array("background_color"=>"#3498db", "fontawesome_class"=>"fas fa-align-justify", "caption"=>translate_str_by_id(3297), "url"=>"/".$DP_Config->backend_dir."/shop/finance/operations_editor") );
				?>

				
				<?php
				//Вернуться обратно в "Счета покупателей"
				print_backend_button( array("background_color"=>"#27ae60", "fontawesome_class"=>"fas fa-money-check-alt", "caption"=>translate_str_by_id(3298), "url"=>"/".$DP_Config->backend_dir."/shop/finance/account_operations") );
				?>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
				
				
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<?php
	$operation_id = 0;
	$income = 1;
	$name = '';
	$name_lang_str_id = 0;//Мультиязычность
	$system = 0;
	$used = 0;
	if( isset($_GET['operation_id']) )
	{
		$operation_query = $db_link->prepare('SELECT *, (SELECT COUNT(*) FROM `shop_users_accounting` WHERE `operation_code` = `shop_accounting_codes`.`id` ) AS `used` FROM `shop_accounting_codes` WHERE `id` = ?;');
		$operation_query->execute( array($_GET['operation_id']) );
		$operation = $operation_query->fetch();
		
		if( $operation != false )
		{
			$operation_id = $operation['id'];
			$income = $operation['income'];
			
			$name_lang_str_id = $operation['name'];//Мультиязычность
			$name = translate_str_by_id($operation['name']);
			
			$system = $operation['system'];
			$used = $operation['used'];
		}
	}
	?>
	
	
	<form name="operation_save_form" method="POST">
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	<input type="hidden" name="action" value="save_operation" />
	<input type="hidden" name="operation_id" value="<?php echo $operation_id; ?>" />
	
	<input type="hidden" name="name_lang_str_id" value="<?php echo $name_lang_str_id; ?>" />
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3299); ?>
			</div>
			<div class="panel-body">
				
				<?php
				if( $operation_id > 0 )
				{
					?>
					<div class="form-group">
						<label class="col-sm-2 control-label">ID (<?php echo translate_str_by_id(3300); ?>)</label>
						<div class="col-sm-10">
							<?php echo $operation_id; ?>
						</div>
					</div>
					<div class="hr-line-dashed"></div>
					<?php
				}
				if( $system )
				{
					?>
					
					<div style="color:#FFF;background-color:#e74c3c;border-radius:3px;padding:6px 12px;font-weight:normal;"><?php echo translate_str_by_id(3301); ?></div>
					
					<div class="hr-line-dashed"></div>
					<?php
				}
				if( $used > 0 )
				{
					?>
					
					<div style="color:#FFF;background-color:#e74c3c;border-radius:3px;padding:6px 12px;font-weight:normal;"><?php echo translate_str_by_id(3302); ?></div>
					
					<div class="hr-line-dashed"></div>
					<?php
				}
				?>
			
				
				<div class="form-group">
					<label class="col-sm-2 control-label"><?php echo translate_str_by_id(3239); ?></label>
                    <div class="col-sm-10">
						<select class="form-control m-b" name="income" id="income">
							<option value="1"><?php echo translate_str_by_id(3240); ?></option>
							<option value="0"><?php echo translate_str_by_id(3241); ?></option>
						</select>
						<script>
						document.getElementById('income').value = '<?php echo $income; ?>';
						</script>
                    </div>
                </div>
			
				<div class="hr-line-dashed"></div>
				
				<div class="form-group">
					<label class="col-sm-2 control-label"><?php echo translate_str_by_id(2102); ?></label>

                    <div class="col-sm-10">
						<input type="text" placeholder="<?php echo translate_str_by_id(3303); ?>" name="name" id="name" value="<?php echo $name; ?>" class="form-control" />
					</div>
                </div>
				
			
			</div>
		</div>
	</div>
	
	
	
	</form>
	
	<script>
	function save_form_submit()
	{
		<?php
		if($system)
		{
			?>
			alert('<?php echo translate_str_by_id(3304); ?>');
			return;
			<?php
		}
		?>
		
		
		<?php
		if($used > 0)
		{
			?>
			alert('<?php echo translate_str_by_id(3305); ?>');
			return;
			<?php
		}
		?>
		
		if( document.getElementById('name').value == '' )
		{
			alert('<?php echo translate_str_by_id(3306); ?>');
			return;
		}
		
		
		document.forms['operation_save_form'].submit();
	}
	</script>
	
	
	<?php
}



?>