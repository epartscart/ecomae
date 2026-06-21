<?php
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_session = DP_User::getUserSession();


if(DP_User::getUserId() != 0)
{
    echo translate_str_by_id(4740);
}
else//Пользователь не авторизован - ВЫВОД СТРАНИЦЫ
{
    require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_storefront_auth_layout.php';
    epc_storefront_auth_layout_open('wide');
?>

    <?php
    //СОЗДАЕМ СПИСОК ВСЕХ ДОПОЛНИТЕЛЬНЫХ ПОЛЕЙ РЕГИСТРАЦИИ ДЛЯ JAVASCRIPT. ЭТОТ СПИСОК ИСПОЛЬЗУЕТСЯ: для буферизации введеных значений при переключении регистрационных вариантов. Основные поля (Телефон, E-mail и пароль сюда не входят)
    $all_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag` = ? ORDER BY `order` ASC;');
	$all_fields_query->execute( array(0) );
    ?>
    <script>
    var reg_fields = new Array();//Массив с объектами всех полей
    <?php
    while( $additional_field = $all_fields_query->fetch() )
    {
        ?>
        reg_fields[reg_fields.length] = new Object();//Создаем новый объект поля. И инициализируем его поля:
        reg_fields[reg_fields.length - 1].main_flag = <?php echo $additional_field["main_flag"]; ?>;
        reg_fields[reg_fields.length - 1].name = "<?php echo $additional_field["name"]; ?>";
        reg_fields[reg_fields.length - 1].caption = "<?php echo translate_str_by_id($additional_field["caption"]); ?>";
        reg_fields[reg_fields.length - 1].show_for = <?php echo $additional_field["show_for"]; ?>;
        reg_fields[reg_fields.length - 1].required_for = <?php echo $additional_field["required_for"]; ?>;
        reg_fields[reg_fields.length - 1].maxlen = <?php echo $additional_field["maxlen"]; ?>;
        reg_fields[reg_fields.length - 1].regexp = "<?php echo $additional_field["regexp"]; ?>";
		reg_fields[reg_fields.length - 1].widget_type = "<?php echo $additional_field["widget_type"]; ?>";
        reg_fields[reg_fields.length - 1].widget_options = <?php echo $additional_field["widget_options"]; ?>;
        reg_fields[reg_fields.length - 1].value_buffer = "";//Текущее значения - для сохранения при переключении регистрационных вариантов
		reg_fields[reg_fields.length - 1].example = "<?php echo translate_str_by_key($additional_field["example"]); ?>";
        <?php
    }
    ?>
    </script>
    

    <?php
    $epc_reg_enhanced = $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_registration_enhanced.php';
    $epc_reg_enhanced_ok = is_readable($epc_reg_enhanced);
    if ($epc_reg_enhanced_ok) {
        require_once $epc_reg_enhanced;
        epc_reg_render_social_block($multilang_params);
    }
    ?>

    <!-- Start ФОРМА РЕГИСТРАЦИИ -->
    <form action="<?php echo $multilang_params['lang_href']; ?>/users/register" id="regform" onsubmit="return onSubmitCheck();" method="post">
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
        <!--Блок для выбора Регистрационного Варианта-->
        <div id="RegVariantsSelector">
    		<?php
            //Выводим в JavaScript Регистрационные Варианты:
            $reg_variants_query = $db_link->prepare('SELECT COUNT(*) FROM `reg_variants` ORDER BY `order` ASC;');
			$reg_variants_query->execute();
            if( $reg_variants_query->fetchColumn() == 1)
            {
				$reg_variants_query = $db_link->prepare('SELECT * FROM `reg_variants` ORDER BY `order` ASC;');
				$reg_variants_query->execute();
                $reg_variant_record = $reg_variants_query->fetch();
                ?>
                <select id="reg_variant_selector" name="reg_variant" style="display:none" onchange="regenerateFields();">
                    <option value="<?php echo $reg_variant_record["id"]; ?>"><?php echo $reg_variant_record["caption"]; ?></option>
                </select>
                <?php
            }
            else
            {
				$reg_variants_query = $db_link->prepare('SELECT * FROM `reg_variants` ORDER BY `order` ASC;');
				$reg_variants_query->execute();
				
                ?>
				<div class="panel panel-primary">
                    <div class="panel-heading"><?php echo translate_str_by_id(4716); ?></div>
                    <div class="panel-body">
						  <div class="form-group">
							<select id="reg_variant_selector" name="reg_variant" onchange="regenerateFields();" class="form-control" />
							<?php
							while($reg_variant_record = $reg_variants_query->fetch())
							{
								$reg_variant_record["caption"] = translate_str_by_id($reg_variant_record["caption"]);
								
								?>
								<option value="<?php echo $reg_variant_record["id"]; ?>"><?php echo $reg_variant_record["caption"]; ?></option>
								<?php
							}
							?>
							</select>
						  </div>
                    </div>
                </div> <!-- panel panel-primary -->
                <?php
            }
            ?>
        </div>
        
        
        <?php
        //БЛОК ОСНОВНЫХ ПОЛЕЙ РЕГИСТРАЦИИ
		$main_fields_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `main_flag` = 1 ORDER BY `order`;');
		$main_fields_query->execute();
        ?>
		<div class="panel panel-primary">
			<div class="panel-heading"><?php echo translate_str_by_id(3925); ?></div>
			<div class="panel-body">
				
				<?php
				//Доступные способы связи
				$display_reg_contact_select = ' style="display:none;" ';//Для видимости селектора - по-умолчанию не видимый
				$reg_contact_select_options = '<option value="phone">Телефон</option> <option value="email">E-mail</option>';//Набор опций для способов регистрации
				$available_communications = DP_User::available_communications();//Получаем доступные способы связи
				if( $available_communications["all"] )
				{
					$display_reg_contact_select = "";//Селектор делаем видимым, чтобы клиент смог сам выбрать нужный вид контакта для регистрации
				}
				else if( $available_communications["sms"] )
				{
					$reg_contact_select_options = '<option value="phone">Телефон</option>';//Оставляем только телефон
				}
				else
				{
					$reg_contact_select_options = '<option value="email">E-mail</option>';//Оставляем только E-mail
				}
				?>
				
				<!-- Селектор контакта для регистрации -->
				<div class="form-group" <?php echo $display_reg_contact_select; ?>>
					<label for="reg_contact_select" class="col-sm-4 col-lg-3 control-label"><?php echo translate_str_by_id(4741); ?></label>
					<div class="col-sm-8 col-lg-9" style="padding:5px;">
						<select name="reg_contact_type" class="form-control" id="reg_contact_select" onchange="on_reg_contact_select_changed();">
							<?php echo $reg_contact_select_options; ?>
						</select>
					</div>
				</div>
				<div class="col-sm-12"></div>
				<!-- Поле для контакта -->
				<div class="form-group">
					<label for="reg_contact_input" class="col-sm-4 col-lg-3 control-label" id="reg_contact_label"></label>
					<div class="col-sm-8 col-lg-9" style="padding:5px;">
						<input type="text" name="reg_contact" class="form-control" id="reg_contact_input" placeholder="" />
					</div>
				</div>
				<script>
				//Обработка выбора контакта
				function on_reg_contact_select_changed()
				{
					if( document.getElementById("reg_contact_select").value == "email" )
					{
						document.getElementById("reg_contact_label").innerHTML = "E-mail*";
						document.getElementById("reg_contact_input").setAttribute("placeholder", "<?php echo translate_str_by_id(4742); ?>");
					}
					else
					{
						document.getElementById("reg_contact_label").innerHTML = "<?php echo translate_str_by_id(1312); ?>*";
						document.getElementById("reg_contact_input").setAttribute("placeholder", "<?php echo translate_str_by_id(2318); ?>, 9005556677");
					}
				}
				on_reg_contact_select_changed();
				</script>
				
				

				<div class="form-group">
					<label for="password" class="col-sm-4 col-lg-3 control-label"><?php echo translate_str_by_id(1311); ?>*</label>
					<div class="col-sm-8 col-lg-9" style="padding:5px;">
						<input type="password" name="password" class="form-control" id="password" placeholder="<?php echo translate_str_by_id(1311); ?>" />
					</div>
				</div>
				
				
				<div class="form-group">
					<label for="password_repeat" class="col-sm-4 col-lg-3 control-label"><?php echo translate_str_by_id(3927); ?>*</label>
					<div class="col-sm-8 col-lg-9" style="padding:5px;">
					  <input type="password" class="form-control" name="password_repeat" id="password_repeat" value="" placeholder="<?php echo translate_str_by_id(3927); ?>">
					</div>
				</div>
				

			</div>
		</div>
		
		
		<?php
		if (!empty($epc_reg_enhanced_ok)) {
			epc_reg_render_account_tabs();
		} else {
		?>
		<div class="panel panel-primary">
			<div class="panel-heading">Account type</div>
			<div class="panel-body">
				<div class="form-group">
					<label class="radio-inline" style="margin-right:18px;">
						<input type="radio" name="epc_customer_type" value="retail" checked="checked" /> Retail customer
					</label>
					<label class="radio-inline">
						<input type="radio" name="epc_customer_type" value="wholesale" /> Wholesale customer
					</label>
				</div>
			</div>
		</div>
		<?php
		}
		?>
		<input type="hidden" name="epc_reg_country" id="epc_reg_country_sync" value="" />
		<input type="hidden" name="epc_email_otp_verified" id="epc_reg_otp_verified_field" value="" />
		
		
        
        <!-- Блок для дополнительных полей -->
        <div id="additional_fields_div">
        </div>
        <script>
        //Перегенировать поля
        function regenerateFields()
        {
            if( reg_fields.length == 0 )
            {
                return;
            }
            var current_reg_variant = document.getElementById("reg_variant_selector").value;
            
            var additional_html = "";//HTML для дополнительных полей регистрации
            for(var i=0; i < reg_fields.length; i++)
            {
                //Обработка show_for:
                if(reg_fields[i].show_for.indexOf(parseInt(current_reg_variant)) < 0)
                {
                    continue;//Это поле не показываем
                }
                
                //Обработка required_for
                var required_for = "";//Для звездочки
                if(reg_fields[i].required_for.indexOf(parseInt(current_reg_variant)) >= 0)
                {
                    required_for = "*";//Это поле не показываем
                }
                
				var example = reg_fields[i].caption;//Пример для заполнения
				if(reg_fields[i].example != "")
				{
					example = reg_fields[i].example;
				}
				
				
				additional_html += "<div class=\"form-group\"><label for=\""+reg_fields[i].name+"\" class=\"col-sm-4 col-lg-3 control-label\">"+reg_fields[i].caption+required_for+"</label><div class=\"col-sm-8 col-lg-9\" style=\"padding:5px;\">";
				
				//Виджет:
                switch(reg_fields[i].widget_type)
                {
                    case "text":
                        additional_html += "<input onKeyUp=\"dynamicApplying('"+reg_fields[i].name+"');\" type=\"text\" name=\""+reg_fields[i].name+"\" id=\""+reg_fields[i].name+"\" value='"+reg_fields[i].value_buffer.replace('/(["\'\])/g', "\\$1")+"' class=\"form-control\" placeholder=\""+example+"\" />";
                        break;
                };
                
                
                additional_html += "</div></div><div class=\"row\"></div>";
            }
            
            
            additional_html = "<div class=\"panel panel-primary\"><div class=\"panel-heading\"><?php echo translate_str_by_id(3928); ?></div><div class=\"panel-body\">" + additional_html + "</div></div>";
            
            
            document.getElementById("additional_fields_div").innerHTML = additional_html;
        }//~function regenerateFields()
        
        
        
        // --------------------------------------------------------------------------
        //Функция динамическиго применния значений для текстовых строк
    	function dynamicApplying(attribute)
    	{
        	var str_value = document.getElementById(attribute).value;//Текущее значение
        	//Ищем поле
        	for(var i=0; i < reg_fields.length; i++)
        	{
        	    if(reg_fields[i].name == attribute)
        	    {
        	        reg_fields[i].value_buffer = str_value;
        	        //console.log(reg_fields[i].value_buffer);
        	        break;
        	    }
        	}
    	}
        
        
        regenerateFields();//Генерируем после загрузки страницы
        </script>
        
        <!--Captcha-->
        <div id="captcha">
        	<img src="/lib/captcha/captcha.php" id="capcha-image">
            <a href="javascript:void(0);" onclick="document.getElementById('capcha-image').src='/lib/captcha/captcha.php?rid=' + Math.random();"><img src="/lib/captcha/refresh.png" border="0"/></a><br>
            <?php echo translate_str_by_id(4067); ?>: <input type="text" name="capcha_input" id="capcha_input">
        </div>
        
		
		<?php
		//Подключаем общий модуль принятия пользовательского соглашения
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/users_agreement_module.php");
		?>
		
		
        <button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(4743); ?></button>
    </form>
    <!-- Start ФОРМА РЕГИСТРАЦИИ -->

<?php
    epc_storefront_auth_layout_close();
// ── Email OTP verification modal for registration ──
$_epcOtpModalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_otp_modal.php';
if (is_file($_epcOtpModalFile)) {
	require_once $_epcOtpModalFile;
	$_epcOtpSendUrl    = '/epc-auth-send-code.php';
	$_epcOtpVerifyUrl  = '/epc-auth-otp-verify-only.php';
	$_epcOtpTenantKey  = '';
	if (function_exists('epc_portal_site_profile')) {
		$_sp = epc_portal_site_profile();
		$_epcOtpTenantKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_sp['site_key'] ?? '')));
	}
	$_epcOtpLogoUrl = '';
	if (function_exists('epc_portal_site_profile')) {
		$_sp2 = epc_portal_site_profile();
		$_epcOtpLogoUrl = (string) ($_sp2['logo_url'] ?? '');
	}
	echo epc_otp_modal_render(array(
		'modal_id'    => 'epc_reg_otp',
		'context'     => 'storefront',
		'tenant_key'  => $_epcOtpTenantKey,
		'send_url'    => $_epcOtpSendUrl,
		'verify_url'  => $_epcOtpVerifyUrl,
		'logo_url'    => $_epcOtpLogoUrl,
		'label'       => 'epartscart',
		'verify_only' => true,
		'on_success'  => '
			window.epcRegOtpVerified = true;
			window.epcRegOtpEmail = data.verified_email || currentEmail;
			var hf = document.getElementById("epc_reg_otp_verified_field");
			if (hf) hf.value = "1";
			var rf = document.getElementById("regform");
			if (rf) rf.submit();
		',
	));
}
?>
    <script>
    // ------------------------------------------------------------------------------------
    //ПРОВЕРКА КОРРЕКСТНОСТИ ЗАПОЛНЕНИЯ ФОРМЫ:
    //Флаги для реализации синхронных проверочных запросов
    var reg_contact_check = false;//Флаг проверки контакта (уникальность и корректность)
    var captcha_correct = false;//Флаг корректности captcha
    function onSubmitCheck()
    {
		if( !check_user_agreement() )
		{
			return false;
		}
		
		
    	//1. ПРОВЕРКА КОРРЕКТНОСТИ ЗАПОЛНЕНИЯ
        //1.1 Текущий регистрационный вариант
        var currentRegVariant = document.getElementById("reg_variant_selector").value;
        
        //1.2 Проверка факта заполнения полей какими-либо значениями
    	for(var i=0; i<reg_fields.length; i++)
    	{
    		if(reg_fields[i].required_for.indexOf(parseInt(currentRegVariant)) != -1)//Заполнение требуется для данного Регистрационного Варианта
    		{
    			if(document.getElementById(reg_fields[i].name).value == "")//Но поле не заполнено
    			{
    				alert("Заполните поле "+reg_fields[i].caption);
    				return false;
    			}
    		}
    	}//for(i)
        
        
        //1.3 Обработка заполнения пароля:
    	if(document.getElementById("password").value != document.getElementById("password_repeat").value)//Пароли должны совпадать
    	{
    		alert("<?php echo translate_str_by_id(3933); ?>");
    		return false;
    	}
    	//Проверям минимально допустимую длину пароля
	    if(document.getElementById("password").value.length < <?php echo $DP_Config->min_password_len; ?>)
    	{
		    alert("<?php echo translate_str_by_id(3934); ?> <?php echo $DP_Config->min_password_len; ?> <?php echo translate_str_by_id(3935); ?>");
		    return false;
    	}
    	
    	
    	
    	
    	
    	//1.4 Проверка соответствия заполненных значений регулярным выражениям
    	//Если поле пустое - значит его можно было не заполнять (проверка на факт заполнения следует раньше). Но есть там есть значение, то оно обязательно должно соответствовать RegExp, даже если оно не обязательно к заполнению
    	for(var i=0; i<reg_fields.length; i++)
    	{
    		if(reg_fields[i].show_for.indexOf(parseInt(currentRegVariant)) == -1)//У этого поля не указан текущий Регистрационный Вариант - его нет в форме
    		{
    			continue;
    		}
			
			//Если регулярное выражение пустое - значит пропускаем, т.к. требований к содержимому нет
			if(reg_fields[i].regexp == "")
			{
				continue;
			}
    		
    		if(String(document.getElementById(reg_fields[i].name).value) != "")
    		{
    			var current_value = String(document.getElementById(reg_fields[i].name).value);//Заполненное значение
    			var regex = new RegExp(reg_fields[i].regexp);//Регулярное выражение для поля
    			//Далее ищем подстроку по регулярному выражению
    			var match = regex.exec(String(current_value));
    			if(match == null)
    			{
    				alert("<?php echo translate_str_by_id(3885); ?> "+reg_fields[i].caption+" <?php echo translate_str_by_id(3930); ?>");
    				return false;
    			}
    			else
    			{
    				var match_value = String(match[0]);//Подходящая подстрока
    				if(match_value != current_value)
    				{
    					alert("<?php echo translate_str_by_id(3893); ?> "+reg_fields[i].caption+" <?php echo translate_str_by_id(3931); ?>");
    					return false;
    				}
    			}
    			//Заполнено правильно, если: есть подстрока по регулярному выражению и она полностью равна самой строке
    		}
    	}
    	
    	
    	//1.5 Проверка уникальности и корректности reg_contact синхронным запросом
    	var reg_contact = document.getElementById("reg_contact_input").value;//Введеный reg_contact
		var reg_contact_type = document.getElementById("reg_contact_select").value;
		//Сама проверка
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "<?php echo $DP_Config->domain_path; ?>content/users/check_reg_contact.php",
			dataType: "text",//Тип возвращаемого значения
			data: "reg_contact="+reg_contact+"&reg_contact_type="+reg_contact_type+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				console.log(answer);
				
				var answer_ob = JSON.parse(answer);
				
				//Если некорректный парсинг ответа
				if( typeof answer_ob.status === "undefined" )
				{
					reg_contact_check = false;
					alert("<?php echo translate_str_by_id(4744); ?>");
				}
				else
				{
					//Корректный парсинг ответа
					if(answer_ob.status == true)
					{
						reg_contact_check = true;
					}
					else
					{
						reg_contact_check = false;
						alert(answer_ob.message);
					}
				}
			}
		});
    	if(reg_contact_check == false)
    	{
    		return false;
    	}
    	
		
		//alert("ok");
		//return false;
    	
    	//Проверка Captcha синхронным запросом
    	var capcha_input = document.getElementById("capcha_input").value;
    	jQuery.ajax({
    	   type: "POST",
    	   async: false, //Запрос синхронный
    	   url: "/lib/captcha/check_captcha.php",
    	   dataType: "json",//Тип возвращаемого значения
    	   data: "captcha_check="+capcha_input,
    	   success: function(is_captcha_correct){
    		   captcha_correct = is_captcha_correct;
    	   }
    	 });
    	if(captcha_correct == false)
    	{
    		alert("<?php echo translate_str_by_id(4070); ?>");
    		document.getElementById('capcha-image').src='/lib/captcha/captcha.php?rid=' + Math.random();
    		return false;
    	}
		<?php if (!empty($epc_reg_enhanced_ok)) { ?>
		if(typeof epcRegClientValidate === 'function' && !epcRegClientValidate()){
			return false;
		}
		<?php } else { ?>
		var tradeType = document.querySelector('input[name="epc_customer_type"]:checked');
		if(!tradeType || (tradeType.value !== 'retail' && tradeType.value !== 'wholesale')){
			alert('Please choose Retail customer or Wholesale customer.');
			return false;
		}
		<?php } ?>

		// ── Email OTP verification step ──
		var _regContactType = (document.getElementById('reg_contact_select')||{}).value||'email';
		if(_regContactType === 'email' && !window.epcRegOtpVerified){
			var _regEmail = (document.getElementById('reg_contact_input')||{}).value||'';
			_regEmail = _regEmail.trim();
			if(_regEmail && window.EpcOtpModal && window.EpcOtpModal['epc_reg_otp']){
				window.EpcOtpModal['epc_reg_otp'].open(_regEmail);
				return false; // Hold submit — modal on_success will re-submit
			}
		}

    	return true;
    }
    </script>

<?php
}//else//Пользователь не авторизован
?>