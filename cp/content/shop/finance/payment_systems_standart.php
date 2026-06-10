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
	
	
	$result = true;//Накопительный результат
	
	
	//Получаем id платежной системы
	$system_id = $_POST["system_id"];
	if($system_id > 0)
	{
		//Предварительно отключаем все системы
		if( $db_link->prepare("UPDATE `shop_payment_systems` SET `active`=0;")->execute() != true)
		{
			$result = false;
		}
		
		
		//Ставим новые настройки:
		if( $db_link->prepare("UPDATE `shop_payment_systems` SET `active`=1, `parameters_values` = ? WHERE `id` = ?;")->execute( array($_POST["parameters_values"], $system_id) ) != true)
		{
			$result = false;
		}
	}
	else//Отключаем все системы
	{
		if( $db_link->prepare("UPDATE `shop_payment_systems` SET `active`=0;")->execute() != true)
		{
			$result = false;
		}
	}
	
	if($result)
	{
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/shop/payments/payments?tab=configure&success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}
	else
	{
		$error_message = translate_str_by_id(2122);
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/shop/payments/payments?tab=configure&error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}
	
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
		payment_systems[payment_systems.length-1].name = '<?php echo translate_str_by_id($payment_system["name"]); ?>';
		payment_systems[payment_systems.length-1].parameters = JSON.parse('<?php echo $payment_system["parameters"]; ?>');
		payment_systems[payment_systems.length-1].parameters_values = JSON.parse('<?php echo $payment_system["parameters_values"]; ?>');
		payment_systems[payment_systems.length-1].description = '<?php echo translate_str_by_id($payment_system["description"]); ?>';
		payment_systems[payment_systems.length-1].active = <?php echo $payment_system["active"]; ?>;
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
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3317); ?>
			</div>
			<div class="panel-body" id="current_system_indicator">
			</div>
		</div>
	</div>
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3318); ?>
			</div>
			<div class="panel-body">
				<select id="system_selector" name="system_selector" onchange="on_system_changed();" class="form-control">
				</select>
			</div>
		</div>
	</div>

	<script>
	//Обработка смены типа интерфейса
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
					html += "<input type=\""+payment_systems[i].parameters[j].type+"\" id=\""+payment_systems[i].parameters[j].name+"\" name=\""+payment_systems[i].parameters[j].name+"\" value=\"\" class=\"form-control\" />";
				}
				else if(payment_systems[i].parameters[j].type == "select")
				{
					html += "<select name=\""+payment_systems[i].parameters[j].name+"\" id=\""+payment_systems[i].parameters[j].name+"\" class=\"form-control\">";
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
			html = "<?php echo translate_str_by_id(3319); ?>";
		}
		
		mysql_options_div_fields.innerHTML = html;
		
	}
	</script>
	
	
	
	
	<div class="col-lg-6">
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
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	//Функция сохранения
    function save_action()
    {
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
        
        console.log(document.getElementById("parameters_values").value);
        
        //alert("Ok");
        //return;
        
        document.forms["form_to_save"].submit();
    }//~function save_action()
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<!-- Действия после загрузки страницы -->
	<script>
	//Заполняем селектор платежных систем
	var system_selector_html = "<option value=\"0\"><?php echo translate_str_by_id(2457); ?></option>";
	var current_selected_id = 0;
	for(var i=0; i < payment_systems.length; i++)
	{
		system_selector_html += "<option value=\""+payment_systems[i].id+"\">"+payment_systems[i].name+"</option>";
		
		//Отмечаем текущую активную
		if(payment_systems[i].active == 1)
		{
			current_selected_id = payment_systems[i].id;
			document.getElementById("current_system_indicator").innerHTML = "<?php echo translate_str_by_id(3321); ?>: "+payment_systems[i].name;
		}
	}
	document.getElementById("system_selector").innerHTML = system_selector_html;
	
	//Указываем текущий выбранный элемент
	document.getElementById("system_selector").value = current_selected_id;
	
	//Показываем менеджеру текущую платежную систему
	if(current_selected_id == 0)
	{
		document.getElementById("current_system_indicator").innerHTML = "<?php echo translate_str_by_id(3322); ?>";
	}
	
	//Обработка текущего выбора платежной системы
	on_system_changed();
	
	
	for(var i=0; i < payment_systems.length; i++)
	{
		if(payment_systems[i].active == 1)
		{
			for(var j=0; j < payment_systems[i].parameters.length; j++)
			{
				if(payment_systems[i].parameters[j].type == "checkbox")//Инициализация значений для чекбокса
				{
					document.getElementById(payment_systems[i].parameters[j].name).checked = payment_systems[i].parameters_values[payment_systems[i].parameters[j].name];
				}
				else//Инициализация значений для строковых типов (text, password) и для списков (select)
				{
					document.getElementById(payment_systems[i].parameters[j].name).value = payment_systems[i].parameters_values[payment_systems[i].parameters[j].name];
				}
			}
		}
	}
	</script>
	<?php
}
?>