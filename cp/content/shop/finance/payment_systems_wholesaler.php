<?php
/**
Серверный скрипт для подключения платежных систем
*/
defined('_ASTEXE_') or die('No access');

if( !empty($_POST["save_action"]) )//Действия
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	try
	{
		//Старт транзакции
		if( ! $db_link->beginTransaction()  )
		{
			throw new Exception(translate_str_by_id(2132));
		}
		
		
		//Проверка наличия необходимых параметров и технические проверки
		if( !isset( $_POST['office_id'] ) || !isset( $_POST['system_id'] ) || !isset( $_POST['parameters_values'] ) )
		{
			throw new Exception(translate_str_by_id(3288));
		}
		//У пользователя должны быть права на магазин
		$office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
		$office_query->execute( array($_POST['office_id']) );
		$office = $office_query->fetch();
		if( $office == false )
		{
			throw new Exception(translate_str_by_id(3323));
		}
		if( array_search( DP_User::getAdminId(), json_decode($office['users'], true) ) === false )
		{
			throw new Exception(translate_str_by_id(3324));
		}
		//system_id должен быть либо 0, либо одним из таблицы платежных систем
		if( $_POST['system_id'] != 0 )
		{
			$system_query = $db_link->prepare("SELECT * FROM `shop_payment_systems` WHERE `id` = ?;");
			$system_query->execute( array($_POST['system_id']) );
			if( ! $system_query->fetch() )
			{
				throw new Exception(translate_str_by_id(3325));
			}
		}
		
		
		if( ! $db_link->prepare("UPDATE `shop_offices` SET `pay_system_id`=?, `pay_system_parameters`=? WHERE `id`=?;")->execute( array($_POST['system_id'], $_POST['parameters_values'], $_POST['office_id']) ) )
		{
			throw new Exception(translate_str_by_id(3326));
		}
	}
	catch (Exception $e)
	{
		//Откатываем все изменения
		$db_link->rollBack();
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/platezhnye-sistemy?office_id=<?php echo $_POST['office_id']; ?>&error_message=<?php echo urlencode($e->getMessage()); ?>";
		</script>
		<?php
		exit;
	}

	//Дошли до сюда, значит выполнено ОК
	$db_link->commit();//Коммитим все изменения и закрываем транзакцию
	?>
	<script>
		location="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/platezhnye-sistemy?office_id=<?php echo $_POST['office_id']; ?>&success_message=<?php echo urlencode(translate_str_by_id(3327)); ?>";
	</script>
	<?php
	exit;
}
else
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	?>
	
	<?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
	
	<script>
	var payment_systems = new Array();
	<?php
	$payment_systems_query = $db_link->prepare("SELECT * FROM `shop_payment_systems` WHERE `anable` = 1;");
	$payment_systems_query->execute();
	while($payment_system = $payment_systems_query->fetch() )
	{
		if($payment_system["parameters"] == "" || $payment_system["parameters"] == NULL)
		{
			$payment_system["parameters"] = "[]";
		}
		if($payment_system["parameters_values"] == "" || $payment_system["parameters_values"] == NULL)
		{
			$payment_system["parameters_values"] = "[]";
		}
		?>
		payment_systems[payment_systems.length] = new Object;
		payment_systems[payment_systems.length-1].id = <?php echo $payment_system["id"]; ?>;
		payment_systems[payment_systems.length-1].name = '<?php echo $payment_system["name"]; ?>';
		payment_systems[payment_systems.length-1].parameters = JSON.parse('<?php echo $payment_system["parameters"]; ?>');
		payment_systems[payment_systems.length-1].description = '<?php echo $payment_system["description"]; ?>';
		payment_systems[payment_systems.length-1].active = 0;
		<?php
	}
	?>
	</script>
	
	
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
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	<?php
	//Получаем список магазинов, к которым имеет доступ данный пользователь
	$offices = array();
	$offices_query = $db_link->prepare('SELECT * FROM `shop_offices`');
	$offices_query->execute();
	while( $office = $offices_query->fetch() )
	{
		$managers = json_decode($office['users'], true);
		
		if( array_search( DP_User::getAdminId(), $managers ) !== false )
		{
			$offices['office_'.$office['id']] = $office;
		}
	}
	?>
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3328); ?>
			</div>
			<div class="panel-body">
				
				<select id="offices_select" name="offices_select" class="form-control" onchange="on_office_select();">
					<option value="0"><?php echo translate_str_by_id(2823); ?></option>
					<?php
					foreach( $offices AS $key => $office )
					{
						?>
						<option value="<?php echo $office['id']; ?>"><?php echo $office['caption'].", ".$office['city'].", ".$office['address']." (ID ".$office['id'].")"; ?></option>
						<?php
					}
					?>
				</select>
			
			</div>
		</div>
	</div>
	<script>
	var offices = <?php echo json_encode($offices); ?>;
	//Обработка выбора магазина
	function on_office_select()
	{
		//Если для текущего офиса были изменены настройки, предупреждаем, что они не сохранятся
		if( some_value_changed )
		{
			if( !confirm('<?php echo translate_str_by_id(3329); ?>') )
			{
				//Возвращаем обратно магазин. Настройки платежной системы еще не изменились
				document.getElementById('offices_select').value = document.getElementById('office_id').value;
				return;
			}
		}
		
		
		var selected_office = document.getElementById('offices_select').value;
		
		if( parseInt( selected_office ) == 0 )
		{
			//Скрыли все блоки настройки
			$('#current_active_ps_div').hide();
			$('#ps_select_div').hide();
			$('#selected_ps_options').hide();
		}
		else
		{
			//Показали все блоки настройки
			$('#current_active_ps_div').show();
			$('#ps_select_div').show();
			$('#selected_ps_options').show();
						
			
			//Показываем, какая платежная система для данного магазина была выбрана до открытия страницы
			if( parseInt( offices['office_'+selected_office]['pay_system_id'] ) == 0 )
			{
				document.getElementById('current_system_indicator').innerHTML = '<?php echo translate_str_by_id(3330); ?>';
				
				//Показываем текущую платежную систему для данного офиса (в селекте)
				document.getElementById('system_selector').value = 0;
				
				//Показываем виджеты для настройки данной платежной системы (справа)
				on_system_changed();
			}
			else
			{
				//Показываем текущую платежную систему для данного офиса (в селекте)
				document.getElementById('system_selector').value = parseInt( offices['office_'+selected_office]['pay_system_id'] );
			
				//Показываем виджеты для настройки данной платежной системы (справа)
				on_system_changed();
				
				//Показываем, через какую систему в данный момент принимаются платежи данным магазином
				for(var i=0; i < payment_systems.length; i++)
				{
					if( parseInt( payment_systems[i].id ) == parseInt( offices['office_'+selected_office]['pay_system_id'] ) )
					{
						document.getElementById('current_system_indicator').innerHTML = "<?php echo translate_str_by_id(3321); ?>: "+payment_systems[i].name;
						break;
					}
				}
				
				
				//Выставляем виджеты настройки платежной системы в текущие значения (которые были до открытия страницы)
				var office_pay_system_current_parameters = JSON.parse(offices['office_'+selected_office]['pay_system_parameters']);
				for(var i=0; i < payment_systems.length; i++)
				{	
					if(payment_systems[i].active == 1)
					{	
						for(var j=0; j < payment_systems[i].parameters.length; j++)
						{							
							if(payment_systems[i].parameters[j].type == "checkbox")//Инициализация значений для чекбокса
							{
								document.getElementById(payment_systems[i].parameters[j].name).checked = office_pay_system_current_parameters[payment_systems[i].parameters[j].name+''];
							}
							else//Инициализация значений для строковых типов (text, password) и для списков (select)
							{
								document.getElementById(payment_systems[i].parameters[j].name).value = office_pay_system_current_parameters[payment_systems[i].parameters[j].name+''];
							}
						}
						break;
					}
				}
			}
		}
		
		
		//Запись значений в форму
		set_form_values();
	}
	</script>
	
	

	
	<div class="col-lg-6" id="current_active_ps_div">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3331); ?>
			</div>
			<div class="panel-body" id="current_system_indicator">
			</div>
		</div>
	</div>
	
	
	<div class="col-lg-12">
	</div>
	
	
	<div class="col-lg-6" id="ps_select_div">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3318); ?>
			</div>
			<div class="panel-body">
				<select id="system_selector" name="system_selector" onchange="on_system_changed();set_form_values(1);" class="form-control">
				</select>
			</div>
		</div>
	</div>

	<script>
	//Обработка смены платежной системы
	function on_system_changed()
	{
		//Текущая выбранная система
		var current_system_selected = document.getElementById("system_selector").value;
		
		//Блок для виджетов настройки
		var mysql_options_div_fields = document.getElementById("mysql_options_div_fields");
		
		var html = "";
		
		
		//Снимаем текущую систему - прогоняем по всем
		for(var i=0; i < payment_systems.length; i++)//По системам
		{
			payment_systems[i].active = 0;
		}
		
		//Ищем выбранную систему в списка объектов описания
		for(var i=0; i < payment_systems.length; i++)//По системам
		{
			if(payment_systems[i].id != current_system_selected)
			{
				continue;
			}
			
			//Нужно, чтобы при сохранении получить настройки выбранной системы
			payment_systems[i].active = 1;
			
			for(var j=0; j < payment_systems[i].parameters.length; j++)//По параметрам выбранной системы
			{
				if( j > 0)
				{
					html += "<div class=\"hr-line-dashed col-lg-12\"></div>";
				}
				
				//В зависимости от типа свойства - выводим виджет для настроки
				html += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">"+payment_systems[i].parameters[j].caption+"</label><div class=\"col-lg-6\">";
				
				
				if(payment_systems[i].parameters[j].type == "text" || 
				payment_systems[i].parameters[j].type == "number" || 
				payment_systems[i].parameters[j].type == "color" || 
				payment_systems[i].parameters[j].type == "password" ||
				payment_systems[i].parameters[j].type == "checkbox")
				{					
					html += "<input type=\""+payment_systems[i].parameters[j].type+"\" id=\""+payment_systems[i].parameters[j].name+"\" name=\""+payment_systems[i].parameters[j].name+"\" value=\"\" class=\"form-control\" onkeyup=\"set_form_values(1);\" onchange=\"set_form_values(1);\" />";
				}
				else if(payment_systems[i].parameters[j].type == "select")
				{
					html += "<select name=\""+payment_systems[i].parameters[j].name+"\" id=\""+payment_systems[i].parameters[j].name+"\" class=\"form-control\" onchange=\"set_form_values(1);\">";
						for(var o=0; o < payment_systems[i].parameters[j].options.length; o++)
						{
							html += "<option value=\""+payment_systems[i].parameters[j].options[o].value+"\">"+payment_systems[i].parameters[j].options[o].caption+"</option>";
						}
					html += "</select>";
				}
				html += "</div></div>";
			}
			
			if(payment_systems[i].description != ''){
				html += "<div class=\"hr-line-dashed col-lg-12\"></div>";
				html += "<div class=\"col-lg-12\">";
				html += payment_systems[i].description;
				html += "</div>";
			}
			
			break;
		}
		
		if(html == "")
		{
			html = "<?php echo translate_str_by_id(2489); ?>";
		}
		
		mysql_options_div_fields.innerHTML = html;
		
	}
	</script>
	
	
	
	
	<div class="col-lg-6" id="selected_ps_options">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3320); ?>
			</div>
			<div class="panel-body" id="mysql_options_div_fields">
			</div>
		</div>
	</div>

	<form method="POST" name="form_to_save">
		<input type="hidden" name="save_action" value="ok" />
		<input type="hidden" name="system_id" id="system_id" value="" />
		<input type="hidden" name="parameters_values" id="parameters_values" value="" />
		<input type="hidden" name="office_id" id="office_id" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	// -------------------------------------------------------------------------------------------------
	//Функция сохранения
    function save_action()
    {
		//Выбранный офис
		var office_id = document.getElementById('offices_select').value;
		if( parseInt(office_id) == 0 )
		{
			alert('<?php echo translate_str_by_id(3332); ?>');
			return;
		}
		
		//Запись значений в форму
        set_form_values();
        
        document.forms["form_to_save"].submit();
    }//~function save_action()
	// -------------------------------------------------------------------------------------------------
	//Запись значений в форму
	var some_value_changed = 0;
	function set_form_values( value_changed = 0 )
	{
		some_value_changed = value_changed;
		
		
		//Выбранный магазин
		var office_id = document.getElementById('offices_select').value;
		document.getElementById('office_id').value = office_id;
		
		//1. Платежная система
        var system_id = document.getElementById("system_selector").value;
        document.getElementById("system_id").value = system_id;
        
		//2. Настройки
		var parameters_values = new Object;
		for(var i=0; i < payment_systems.length; i++)
		{
			if(payment_systems[i].active == 1)
			{
				for(var j=0; j < payment_systems[i].parameters.length; j++)
				{
					if(payment_systems[i].parameters[j].type == "checkbox")//Инициализация значений для чекбокса
					{
						if(document.getElementById(payment_systems[i].parameters[j].name).checked)
						{
							parameters_values[payment_systems[i].parameters[j].name] = 1;
						}
						else
						{
							parameters_values[payment_systems[i].parameters[j].name] = 0;
						}
					}
					else//Инициализация значений для строковых типов (text, password) и для списков (select)
					{
						parameters_values[payment_systems[i].parameters[j].name] = document.getElementById(payment_systems[i].parameters[j].name).value;
					}
				}
			}
		}
        document.getElementById("parameters_values").value = JSON.stringify(parameters_values);
	}
	// -------------------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<!-- Действия после загрузки страницы -->
	<script>
	//Заполняем селектор платежных систем
	var system_selector_html = "<option value=\"0\"><?php echo translate_str_by_id(2457); ?></option>";
	for(var i=0; i < payment_systems.length; i++)
	{
		system_selector_html += "<option value=\""+payment_systems[i].id+"\">"+payment_systems[i].name+"</option>";
	}
	document.getElementById("system_selector").innerHTML = system_selector_html;
	
	<?php
	if( isset( $_GET['office_id'] ) )
	{
		?>
		document.getElementById('offices_select').value = '<?php echo $_GET['office_id']; ?>';
		<?php
	}
	?>
	

	//Обработка выбора магазина
	on_office_select();
	</script>
	<?php
}
?>