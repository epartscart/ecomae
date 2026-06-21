<?php
//Страничный скрипт для страницы настройки одного таба поиска
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/get_widget.php");
?>

<?php
if( !empty($_POST["action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	$options_dump = json_encode($_POST);
	//$options_dump = str_replace("\\","\\\\",$options_dump);
	
	
	
	//Мультиязычность
	//Обработка мультиязычности
	//Вызов функции сохранения строки в виде перевода на текущий язык панели управления (кастомный алгоритм). В ответ вернется ID этой строки, который и нужно будет сохранить
	$tab_caption = htmlentities($_POST["tab_caption"]);//Само название на текущем языке ПУ
	$tab_caption_lang_str_id = $_POST["tab_caption_lang_str_id"];//ID строки из мультиязычности
	$tab_caption = save_custom_translation($tab_caption_lang_str_id, $tab_caption);
	

	
	if( $db_link->prepare("UPDATE `shop_docpart_search_tabs` SET `caption` = ?, `order` = ?, `enabled` = ?, `parameters_values` = ? WHERE `id` = ?;")->execute( array( $tab_caption, (int)$_POST["tab_order"], ((bool)$_POST["tab_enabled"]), $options_dump, (int)$_POST["tab_id"]) ) )
	{
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/shop/taby-poiska/tab-poiska?tab_id=<?php echo $_POST["tab_id"]; ?>&success_message=<?php echo $success_message; ?>";
		</script>
		<?php
	}
	else
	{
		$error_message = "SQL-".translate_str_by_id(2095);
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/shop/taby-poiska/tab-poiska?tab_id=<?php echo $_POST["tab_id"]; ?>&error_message=<?php echo $error_message; ?>";
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
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/taby-poiska">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/tabs.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3829); ?></div>
				</a>
			
			
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	<?php
	//Получаем текущие настройки таба
	$tab_id = (int)$_GET["tab_id"];
	
	$tab_query = $db_link->prepare("SELECT * FROM `shop_docpart_search_tabs` WHERE `id` = ?;");
	$tab_query->execute( array($tab_id) );
	$tab_record = $tab_query->fetch();
	
	
	$tab_caption = $tab_record["caption"];
	$tab_parameters = json_decode($tab_record["parameters"], true);
	$tab_parameters_values = json_decode($tab_record["parameters_values"], true);
	$tab_order = $tab_record["order"];
	$tab_enabled = (boolean)$tab_record["enabled"];
	?>
	<form method="POST" name="save_form">
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	<input type="hidden" name="action" value="save" />
	<input type="hidden" name="tab_id" value="<?php echo $tab_id; ?>" />
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3830); ?>
			</div>
			<div class="panel-body form-horizontal">
			<?php
			//Здесь выводим общие настройки таба (название, порядок отображения, чекбокс включен/выключен)
			?>
				<div class="form-group">
					<label class="col-sm-4 control-label">
						<?php echo translate_str_by_id(3831); ?>
					</label>
					<div class="col-sm-8">
						<input type="text" class="form-control" name="tab_caption" value="<?php echo translate_str_by_id($tab_caption); ?>" />
						<input type="hidden" name="tab_caption_lang_str_id" value="<?php echo $tab_caption; ?>" />
					</div>
				</div>
				
				<div class="hr-line-dashed"></div>
				
				<div class="form-group">
					<label class="col-sm-4 control-label">
						<?php echo translate_str_by_id(3030); ?>
					</label>
					<div class="col-sm-8">
						<input type="number" class="form-control" name="tab_order" value="<?php echo $tab_order; ?>" />
					</div>
				</div>
				
				<div class="hr-line-dashed"></div>
				
				<div class="form-group">
					<label class="col-sm-4 control-label">
						<?php echo translate_str_by_id(3832); ?>
					</label>
					<div class="col-sm-8">
						<input type="checkbox"  class="form-control" name="tab_enabled" value="tab_enabled" <?php if($tab_enabled){echo "checked=\"checked\"";} ?> />
					</div>
				</div>
			
			</div>
		</div>
	</div>
	<?php
	
	
	
	//Выводим специальные настройки таба
	if( count($tab_parameters) > 0 )
	{
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
					<a class="showhide"><i class="fa fa-chevron-up"></i></a>
				</div>
				<?php echo translate_str_by_id(3833); ?>
			</div>
			<div class="panel-body form-horizontal" style="backgroud-color:#f5f5f5!important;">
			<?php
			for($i=0; $i < count($tab_parameters); $i++)
			{
				if( $tab_parameters[$i]["type"] == "groupbox" )
				{
				?>
				<div class="hpanel collapsed">
					<div class="panel-heading hbuilt">
						<div class="panel-tools">
							<a class="showhide"><i class="fa fa-chevron-up"></i></a>
						</div>
						<?php echo $tab_parameters[$i]["caption"]; ?>
					</div>
					<div class="panel-body">
					<?php
					//Получаем элементы группы
					$elements = $tab_parameters[$i]["elements"];
					for($e=0; $e < count($elements); $e++)
					{
						if($e > 0)
						{
							?>
							<div class="hr-line-dashed"></div>
							<?php
						}
						?>
						<div class="form-group"><label class="col-sm-4 control-label"><?php echo $elements[$e]["caption"]; ?></label><div class="col-sm-8">
						<?php
						//Для стандартных элеметов выводим через общий генератор виджетов
						if( $elements[$e]["type"] == "text" || $elements[$e]["type"] == "number" || $elements[$e]["type"] == "checkbox" )
						{
							if( !isset($tab_parameters_values[$elements[$e]["name"]]) )
							{
								$tab_parameters_values[$elements[$e]["name"]] = null;
							}
							
							echo get_widget($elements[$e]["type"], $elements[$e]["name"], $tab_parameters_values[$elements[$e]["name"]], NULL);
						}
						else//Вывод нестандартных элементов
						{
							if( $elements[$e]["type"] == "multiselect" )
							{
							?>
							<select multiple="multiple" name="<?php echo $elements[$e]["name"]; ?>[]" id="<?php echo $elements[$e]["name"]; ?>">
							<?php
							if($elements[$e]["source"] == "sql")
							{
								$options_query = $db_link->prepare($elements[$e]["sql"]);
								$options_query->execute();
								while( $option = $options_query->fetch() )
								{
								?>
								<option value="<?php echo $option["value"]; ?>"><?php echo strtoupper($option["caption"]); ?></option>
								<?php
								}
							}
							?>
							</select>
							<script>
								//Делаем из селектора виджет с чекбоками
								$('#<?php echo $elements[$e]["name"]; ?>').multipleSelect({placeholder: "<?php echo translate_str_by_id(3200); ?>...", width:"100%"});
								
								<?php
								if( !isset($tab_parameters_values[$elements[$e]["name"]]) )
								{
									$tab_parameters_values[$elements[$e]["name"]] = null;
								}
								?>
								
								//Устанавливаем текущие значения
								$('#<?php echo $elements[$e]["name"]; ?>').multipleSelect('setSelects', <?php echo json_encode($tab_parameters_values[$elements[$e]["name"]]); ?>);
							</script>
							<?php
							}
							if( $elements[$e]["type"] == "completed_html" )
							{
								if($elements[$e]["source"] == "sql")
								{
									
									$sql_set =  explode(";", $elements[$e]["sql"]);
									
									//echo "<pre>";
									//var_dump($sql_set);
									
									$last_query = (count($sql_set) - 1);
									if( empty( $sql_set[count($sql_set) - 1] ) )
									{
										$last_query = (count($sql_set) - 2);
									}
									
									for( $s=0 ; $s < count($sql_set) ; $s++ )
									{
										$multi_query = $db_link->prepare($sql_set[$s]);
										$multi_query->execute();
										
										//var_dump($multi_query->errorInfo());
										
										//SELECT по архитектуре должен быть последним
										if( $s == $last_query )
										{
											$html = $multi_query->fetch();
											echo $html["html"];
											
											//ЗДЕСЬ ВЫВОДИМ JavaScript для инициализации значений
											//Перечень всех имен полей получаем также через SQL
											
											$fields_names_query = $db_link->prepare($elements[$e]["fields_names_sql"]);
											$fields_names_query->execute();
											?>
											<script>
											<?php
											while($field_name = $fields_names_query->fetch() )
											{
												?>
												document.getElementById("<?php echo $field_name["field_name"]; ?>").value = '<?php echo $tab_parameters_values[$field_name["field_name"]]; ?>';
												<?php
											}
											?>
											</script>
											<?php
										}
									}
									

									
									
								}
							}
						}
						?>
						</div></div>
						<?php
					}
					?>
					</div>
				</div>
				<?php
				}
			}
			?>	
			</div>
		</div>
	</div>
	
	
	<?php
	}
	?>
	
	
	</form>
	
	<script>
	//Отправка формы сохранения табов
	function save_action()
	{
		document.forms["save_form"].submit();
	}
	</script>
	<?php
	
	
}
?>