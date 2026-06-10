<?php
//Страничный скрипт настройки одного способа получения
defined('_ASTEXE_') or die('No access');
?>

<?php
if( !empty( $_POST["action"] ) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	var_dump($_POST);
	
	//Идет только обновление
	
	$mode_id = $_POST["id"];
	$caption = $_POST["caption"];
	$caption_lang_str_id = $_POST["caption_lang_str_id"];
	$order = $_POST["order"];
	$available = $_POST["available"];
	$parameters_values = $_POST["parameters_values"];
	
	//Обработка мультиязычности
	//Вызов функции сохранения строки в виде перевода на текущий язык панели управления (кастомный алгоритм). В ответ вернется ID этой строки, который и нужно будет сохранить
	$caption = save_custom_translation($caption_lang_str_id, $caption);
	
	if( ! $db_link->prepare("UPDATE `shop_obtaining_modes` SET `caption` = ?, `order` = ?, `available` = ?, `parameters_values` = ? WHERE `id` = ?;")->execute( array($caption, $order, $available, $parameters_values, $mode_id) ) )
	{
		//Переадресация с сообщением о результатах выполнения
		$error_message = translate_str_by_id(2276);
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/sposoby-polucheniya/sposob-polucheniya?obtaining_mode_id=<?php echo $mode_id; ?>&error_message=<?php echo $error_message; ?>";
		</script>
		<?php
	}
	else
	{
		//Переадресация с сообщением о результатах выполнения
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/sposoby-polucheniya/sposob-polucheniya?obtaining_mode_id=<?php echo $mode_id; ?>&success_message=<?php echo $success_message; ?>";
		</script>
		<?php
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
	
	<?php
	//Здесь получаем исходные данные
	$mode_id = 0;
	$caption = "";
	$caption_lang_str_id = 0;
	$order = 0;
	$available = 1;
	$parameters = "[]";
	$parameters_values = "[]";
	if( !empty($_GET["obtaining_mode_id"]) )
	{
		$mode_id = (int)$_GET["obtaining_mode_id"];
		
		$obtaining_mode_query = $db_link->prepare("SELECT * FROM `shop_obtaining_modes` WHERE `id` = ?;");
		$obtaining_mode_query->execute( array($mode_id) );
		$obtaining_mode_record = $obtaining_mode_query->fetch();
		
		$caption_lang_str_id = $obtaining_mode_record["caption"];
		$caption = translate_str_by_id($obtaining_mode_record["caption"]);
		
		$order = $obtaining_mode_record["order"];
		$available = $obtaining_mode_record["available"];
		$parameters_values = $obtaining_mode_record["parameters_values"];
		$parameters = $obtaining_mode_record["parameters"];
	}
	?>
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/sposoby-polucheniya" >
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/truck.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3358); ?></div>
				</a>
			
			
			
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	

	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3359); ?>
			</div>
			<div class="panel-body">
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						ID
					</label>
					<div class="col-lg-6 text-center">
						<?php echo $mode_id; ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3360); ?>
					</label>
					<div class="col-lg-6">
						<input type="text" name="caption_input" id="caption_input" value="<?php echo $caption; ?>" class="form-control" />
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3030); ?>
					</label>
					<div class="col-lg-6">
						<input type="number" name="order_input" id="order_input" value="<?php echo $order; ?>" class="form-control" />
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2626); ?>
					</label>
					<div class="col-lg-6">
						<?php
						if($available)
						{
							$checked = "checked=\"checked\"";
						}
						else
						{
							$checked = "";
						}
						?>
						<input type="checkbox" name="available_input" id="available_input"  class="form-control" <?php echo $checked; ?> />
					</div>
				</div>
				
				
			</div>
		</div>
	</div>
	
	
	
	<?php
	
	$paramaters_is_null = true;
	
	if( $parameters != "[]" )
	{
		
		$paramaters_is_null = false;
		
		$parameters = json_decode($parameters, true);
		$parameters_values = json_decode($parameters_values, true);
		?>
		<div class="col-lg-6">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3361); ?>
				</div>
				<div class="panel-body">
					<?php
					for( $i=0; $i < count($parameters); $i++ )
					{
						//Пока не учитываем типы полей, кроме text
						if( $i > 0 )
						{
							?>
							<div class="hr-line-dashed col-lg-12"></div>
							<?php
						}
						?>
						
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo $parameters[$i]["caption"]; ?>
							</label>
							<div class="col-lg-6">
								<input type="<?php echo $parameters[$i]["type"]; ?>" name="<?php echo $parameters[$i]["name"]; ?>" id="<?php echo $parameters[$i]["name"]; ?>" value="<?php echo $parameters_values[$parameters[$i]["name"]]; ?>" class="form-control" />
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
	
	
	<form method="POST" name="save_form" style="display:none;">
		<input type="text" name="action" id="action" value="save_action" />
		<input type="text" name="id" id="id" value="<?php echo $mode_id; ?>" />
		<input type="text" name="caption" id="caption" value="" />
		<input type="text" name="caption_lang_str_id" value="<?php echo $caption_lang_str_id; ?>" />
		<input type="text" name="order" id="order" value="" />
		<input type="text" name="available" id="available" value="" />
		<input type="text" name="parameters_values" id="parameters_values" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	//Функция сохранения
	function save_action()
	{
		//1. Сохраняем название
		document.getElementById("caption").value = document.getElementById("caption_input").value;
		
		//2. Сохраняем порядок отображения
		document.getElementById("order").value = document.getElementById("order_input").value;
		
		//3. Доступность способа
		if( document.getElementById("available_input").checked )
		{
			document.getElementById("available").value = 1;
		}
		else
		{
			document.getElementById("available").value = 0;
		}
		
		
		//4. Сохраняем специальные настройки
		var parameters_values = new Object;
		<?php
		
		

		if ( ! $paramaters_is_null) {
			
			for( $i=0; $i < count($parameters); $i++ ) {
				
				//Пока не учитываем типы полей, кроме text
				?>
				parameters_values["<?php echo $parameters[$i]["name"]; ?>"] = document.getElementById("<?php echo $parameters[$i]["name"]; ?>").value;
				<?php
			}
						
		}

		?>
		document.getElementById("parameters_values").value = JSON.stringify(parameters_values);
		
		
		//Отправка формы
		document.forms["save_form"].submit();
	}
	</script>
	<?php
	
}
?>