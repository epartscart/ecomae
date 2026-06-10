<?php

defined('_ASTEXE_') or die('No access');



//Скрипт конкретного типа авторизации

//Подключается внутри скрипта \modules\login\login_form_general.php

//Внутри данного скрипта доступна переменная $auth_type - с массивом свойств данного типа авторизации





//Доступные методы авторизации

$arr_auth_method = array();

$available_communications = DP_User::available_communications();//Получаем доступные способы связи

if( $available_communications["sms"] )

{

	$arr_auth_method[] = array('key'=>'phone', 'type'=>'phone', 'caption'=>translate_str_by_id(1312), 'icon_class'=>'fa fa-phone', 'placeholder'=>translate_str_by_id(4018));

}

$arr_auth_method[] = array('key'=>'email', 'type'=>'email', 'caption'=>'E-mail', 'icon_class'=>'fa fa-envelope', 'placeholder'=>translate_str_by_id(4017));



$pass_single_email = (count($arr_auth_method) === 1 && $arr_auth_method[0]['key'] === 'email');

$pass_form_id = 'auth_form_' . $auth_type["key"] . '_' . $login_form_postfix;

$pass_js_suffix = $auth_type["key"] . '_' . $login_form_postfix;

?>





<div class="panel-body no-auth epc-pass-login">

	<form method="POST" name="<?php echo $pass_form_id; ?>" id="<?php echo $pass_form_id; ?>">

		

		<input type="hidden" name="form_name" value="auth_form<?php echo $login_form_postfix; ?>" />

		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />

		<input type="hidden" name="authentication" value="true"/>

		<input type="hidden" name="auth_contact" value="" id="auth_contact_<?php echo $pass_js_suffix; ?>"/>

		<input type="hidden" name="auth_contact_type" value="" id="auth_contact_type_<?php echo $pass_js_suffix; ?>"/>

		<?php

		if( ! isset($login_form_target) )

		{

			$login_form_target = "";

		}

		?>

		<input type="hidden" name="target" value="<?php echo $login_form_target; ?>"/>

		

		<div class="form-group">

			

			<?php if (count($arr_auth_method) > 1) { ?>

			<!-- Селектор контакта для аутентификации -->

			<div class="epc-pass-auth-method-select">

				<div class="input-group login-input">

					<span style="padding-left: 3px; padding-right: 2px;" class="input-group-addon"><small><?php echo translate_str_by_id(5644); ?></small></span>

					<select name="auth_contact_type" class="form-control" id="auth_contact_select_<?php echo $pass_js_suffix; ?>" onchange="onChangeAuthMethod_<?php echo $pass_js_suffix; ?>();" style="height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555; padding-left: 8px;">

						<?php

						foreach($arr_auth_method as $auth_method)

						{

						?>

						<option value="<?php echo $auth_method['key']; ?>"><?php echo $auth_method['caption']; ?></option>

						<?php

						}

						?>

					</select>

				</div>

				<br/>

			</div>

			<?php } else { ?>

			<input type="hidden" value="email" id="auth_contact_select_<?php echo $pass_js_suffix; ?>" data-epc-pass-method="email" />

			<?php } ?>

			

			<?php

			foreach($arr_auth_method as $idx => $auth_method)

			{

				$is_visible = $pass_single_email || $idx === 0;

				$wrapper_class = 'input-group login-input epc-pass-contact-wrap';

				if (!$is_visible) {

					$wrapper_class .= ' hidden';

				}

				$input_type = ($auth_method['type'] === 'email') ? 'email' : 'text';

				$input_attrs = ($auth_method['type'] === 'email') ? ' required autocomplete="email"' : ' autocomplete="tel"';

			?>

			<div id="wrapper_<?php echo $auth_type["key"]; ?>_<?php echo $auth_method['key']; ?>_<?php echo $login_form_postfix; ?>" class="<?php echo $wrapper_class; ?>">

				<span class="input-group-addon"><i class="<?php echo $auth_method['icon_class']; ?>"></i></span>

				<input style="height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555;" type="<?php echo $input_type; ?>" class="form-control" placeholder="<?php echo $auth_method['placeholder']; ?>" name="<?php echo $auth_method['type']; ?>" id="auth_contact_input_<?php echo $auth_type["key"]; ?>_<?php echo $auth_method['key']; ?>_<?php echo $login_form_postfix; ?>"<?php echo $input_attrs; ?> />

			</div>

			<?php

			}

			?>

			

			<div class="input-group login-input epc-pass-password-wrap">

				<span class="input-group-addon"><i style="padding: 0px 2px 0px 3px;" class="fa fa-lock"></i></span>

				<input style="height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555;" type="password" class="form-control" placeholder="<?php echo translate_str_by_id(1311); ?>" name="password" autocomplete="current-password" required />

			</div>

			

			<div class="checkbox">

				<input type="checkbox" id="checkbox_remember_<?php echo $pass_js_suffix; ?>" name="rememberme" />

				<label for="checkbox_remember_<?php echo $pass_js_suffix; ?>"><?php echo translate_str_by_id(4666); ?></label>

			</div>

			

			<a href="javascript:void(0);" onclick="onAuthFormSubmit_<?php echo $pass_js_suffix; ?>();" class="btn btn-ar btn-primary btn_auth" style="color:#FFF;"><?php echo translate_str_by_id(4008); ?></a>

			<?php if (!empty($login_form_postfix) && strpos((string) $login_form_postfix, 'login_page') === 0) { ?>

			<a href="<?php echo $multilang_params['lang_href']; ?>/users/registration" class="btn btn-ar btn-success btn_reg" style="color:#FFF;"><?php echo translate_str_by_id(3987); ?></a>

			<?php } ?>

			

			<hr class="dotted margin-10">

			

			<a href="<?php echo $multilang_params['lang_href']; ?>/users/forgot_password" class="btn btn-ar btn-warning btn_forget" style="color:#FFF;"><?php echo translate_str_by_id(4667); ?></a>

			

			<div class="clearfix"></div>

			

		</div>

		

	</form>

</div>







<script>

(function(){

	var formId = <?php echo json_encode($pass_form_id); ?>;

	var jsSuffix = <?php echo json_encode($pass_js_suffix); ?>;

	var singleEmail = <?php echo $pass_single_email ? 'true' : 'false'; ?>;

	var methods = <?php echo json_encode(array_column($arr_auth_method, 'key')); ?>;

	var methodTypes = <?php echo json_encode(array_column($arr_auth_method, 'type', 'key')); ?>;



	function wrapperEl(key) {

		return document.getElementById('wrapper_pass_' + key + '_' + jsSuffix.replace(/^pass_/, ''));

	}



	window['onChangeAuthMethod_' + jsSuffix] = function()

	{

		if (singleEmail) {

			return;

		}

		var select = document.getElementById('auth_contact_select_' + jsSuffix);

		if (!select) {

			return;

		}

		var method = select.value || 'email';

		methods.forEach(function(key) {

			var wrap = wrapperEl(key);

			if (!wrap) {

				return;

			}

			if (method === key) {

				wrap.classList.remove('hidden');

			} else {

				wrap.classList.add('hidden');

			}

		});

	};



	window['onAuthFormSubmit_' + jsSuffix] = function()

	{

		var form = document.forms[formId] || document.getElementById(formId);

		if (!form) {

			return;

		}

		var activeKey = singleEmail ? 'email' : (document.getElementById('auth_contact_select_' + jsSuffix).value || 'email');

		var wrap = wrapperEl(activeKey);

		var input = wrap ? wrap.querySelector('input') : null;

		var contactHidden = document.getElementById('auth_contact_' + jsSuffix);

		var typeHidden = document.getElementById('auth_contact_type_' + jsSuffix);

		if (input && contactHidden) {

			contactHidden.value = input.value;

		}

		if (typeHidden && methodTypes[activeKey]) {

			typeHidden.value = methodTypes[activeKey];

		}

		if (typeof form.reportValidity === 'function' && !form.reportValidity()) {

			return;

		}

		form.submit();

	};



	function initPassLogin() {

		if (!singleEmail) {

			window['onChangeAuthMethod_' + jsSuffix]();

		}

	}

	if (document.readyState === 'loading') {

		document.addEventListener('DOMContentLoaded', initPassLogin);

	} else {

		initPassLogin();

	}

})();

</script>



