<?php
defined('_ASTEXE_') or die('No access');

// Password login type — included from modules/login/login_form_general.php.
// $auth_type is available in scope.

$arr_auth_method = array();
$available_communications = DP_User::available_communications();
if (!empty($available_communications['sms'])) {
	$arr_auth_method[] = array(
		'key' => 'phone',
		'type' => 'phone',
		'caption' => translate_str_by_id(1312),
		'icon_class' => 'fa fa-phone',
		'placeholder' => translate_str_by_id(4018),
	);
}
$arr_auth_method[] = array(
	'key' => 'email',
	'type' => 'email',
	'caption' => 'E-mail',
	'icon_class' => 'fa fa-envelope',
	'placeholder' => translate_str_by_id(4017),
);

$pass_single_email = (count($arr_auth_method) === 1 && $arr_auth_method[0]['key'] === 'email');
$pass_form_id = 'auth_form_' . $auth_type['key'] . '_' . $login_form_postfix;
$pass_js_suffix = $auth_type['key'] . '_' . $login_form_postfix;
?>

<div class="panel-body no-auth epc-pass-login">
	<form method="POST" name="<?php echo htmlspecialchars($pass_form_id, ENT_QUOTES, 'UTF-8'); ?>" id="<?php echo htmlspecialchars($pass_form_id, ENT_QUOTES, 'UTF-8'); ?>" autocomplete="off" data-epc-no-autofill="1">
		<input type="hidden" name="form_name" value="auth_form<?php echo htmlspecialchars((string) $login_form_postfix, ENT_QUOTES, 'UTF-8'); ?>" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
		<input type="hidden" name="authentication" value="true"/>
		<input type="hidden" name="auth_contact" value="" id="auth_contact_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>"/>
		<input type="hidden" name="auth_contact_type" value="" id="auth_contact_type_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>"/>
		<?php
		if (!isset($login_form_target)) {
			$login_form_target = '';
		}
		?>
		<input type="hidden" name="target" value="<?php echo htmlspecialchars((string) $login_form_target, ENT_QUOTES, 'UTF-8'); ?>"/>

		<!-- Dummy fields: browsers often target the first email/password pair for autofill. -->
		<input type="text" name="epc_autofill_trap_user" value="" tabindex="-1" aria-hidden="true" autocomplete="username" style="position:absolute;left:-9999px;width:1px;height:1px;opacity:0;" />
		<input type="password" name="epc_autofill_trap_pass" value="" tabindex="-1" aria-hidden="true" autocomplete="current-password" style="position:absolute;left:-9999px;width:1px;height:1px;opacity:0;" />

		<div class="form-group">
			<?php if (count($arr_auth_method) > 1) { ?>
			<div class="epc-pass-auth-method-select">
				<div class="input-group login-input">
					<span style="padding-left: 3px; padding-right: 2px;" class="input-group-addon"><small><?php echo translate_str_by_id(5644); ?></small></span>
					<select name="auth_contact_type" class="form-control" id="auth_contact_select_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>" onchange="onChangeAuthMethod_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>();" style="height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555; padding-left: 8px;" autocomplete="off">
						<?php foreach ($arr_auth_method as $auth_method) { ?>
						<option value="<?php echo htmlspecialchars($auth_method['key'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($auth_method['caption'], ENT_QUOTES, 'UTF-8'); ?></option>
						<?php } ?>
					</select>
				</div>
				<br/>
			</div>
			<?php } else { ?>
			<input type="hidden" value="email" id="auth_contact_select_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>" data-epc-pass-method="email" />
			<?php } ?>

			<?php
			foreach ($arr_auth_method as $idx => $auth_method) {
				$is_visible = $pass_single_email || $idx === 0;
				$wrapper_class = 'input-group login-input epc-pass-contact-wrap';
				if (!$is_visible) {
					$wrapper_class .= ' hidden';
				}
				$input_type = ($auth_method['type'] === 'email') ? 'email' : 'text';
				// Do not invite password-manager autofill of prior admin/tenant accounts.
				$input_attrs = ' required autocomplete="off" autocapitalize="off" autocorrect="off" spellcheck="false" data-epc-secure-field="1" readonly';
			?>
			<div id="wrapper_<?php echo htmlspecialchars($auth_type['key'] . '_' . $auth_method['key'] . '_' . $login_form_postfix, ENT_QUOTES, 'UTF-8'); ?>" class="<?php echo htmlspecialchars($wrapper_class, ENT_QUOTES, 'UTF-8'); ?>">
				<span class="input-group-addon"><i class="<?php echo htmlspecialchars($auth_method['icon_class'], ENT_QUOTES, 'UTF-8'); ?>"></i></span>
				<input style="height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555;" type="<?php echo htmlspecialchars($input_type, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" placeholder="<?php echo htmlspecialchars($auth_method['placeholder'], ENT_QUOTES, 'UTF-8'); ?>" name="<?php echo htmlspecialchars($auth_method['type'], ENT_QUOTES, 'UTF-8'); ?>" id="auth_contact_input_<?php echo htmlspecialchars($auth_type['key'] . '_' . $auth_method['key'] . '_' . $login_form_postfix, ENT_QUOTES, 'UTF-8'); ?>" value=""<?php echo $input_attrs; ?> />
			</div>
			<?php } ?>

			<div class="input-group login-input epc-pass-password-wrap">
				<span class="input-group-addon"><i style="padding: 0px 2px 0px 3px;" class="fa fa-lock"></i></span>
				<input style="height: 40px; background-color:#FFF; border: 1px solid #ccc; color: #555;" type="password" class="form-control" placeholder="<?php echo translate_str_by_id(1311); ?>" name="password" id="auth_password_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>" value="" autocomplete="new-password" required data-epc-secure-field="1" readonly />
			</div>

			<div class="checkbox">
				<input type="checkbox" id="checkbox_remember_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>" name="rememberme" />
				<label for="checkbox_remember_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>"><?php echo translate_str_by_id(4666); ?></label>
			</div>

			<a href="javascript:void(0);" onclick="onAuthFormSubmit_<?php echo htmlspecialchars($pass_js_suffix, ENT_QUOTES, 'UTF-8'); ?>();" class="btn btn-ar btn-primary btn_auth" style="color:#FFF;"><?php echo translate_str_by_id(4008); ?></a>
			<?php if (!empty($login_form_postfix) && strpos((string) $login_form_postfix, 'login_page') === 0) { ?>
			<a href="<?php echo htmlspecialchars((string) $multilang_params['lang_href'], ENT_QUOTES, 'UTF-8'); ?>/users/registration" class="btn btn-ar btn-success btn_reg" style="color:#FFF;"><?php echo translate_str_by_id(3987); ?></a>
			<?php } ?>

			<hr class="dotted margin-10">

			<a href="<?php echo htmlspecialchars((string) $multilang_params['lang_href'], ENT_QUOTES, 'UTF-8'); ?>/users/forgot_password" class="btn btn-ar btn-warning btn_forget" style="color:#FFF;"><?php echo translate_str_by_id(4667); ?></a>

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

	function unlockSecureField(el) {
		if (!el) return;
		el.removeAttribute('readonly');
	}

	function clearAutofill(form) {
		if (!form) return;
		var fields = form.querySelectorAll('[data-epc-secure-field="1"]');
		for (var i = 0; i < fields.length; i++) {
			var el = fields[i];
			if (el && el.value) {
				el.value = '';
			}
			if (el && el.hasAttribute('readonly')) {
				// keep readonly until user focuses
			}
		}
		var traps = form.querySelectorAll('input[name^="epc_autofill_trap_"]');
		for (var t = 0; t < traps.length; t++) {
			traps[t].value = '';
		}
	}

	window['onChangeAuthMethod_' + jsSuffix] = function() {
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

	window['onAuthFormSubmit_' + jsSuffix] = function() {
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
		var form = document.getElementById(formId);
		if (form) {
			clearAutofill(form);
			// Browsers often inject saved credentials after first paint.
			setTimeout(function(){ clearAutofill(form); }, 50);
			setTimeout(function(){ clearAutofill(form); }, 300);
			setTimeout(function(){ clearAutofill(form); }, 1000);

			var secure = form.querySelectorAll('[data-epc-secure-field="1"]');
			for (var i = 0; i < secure.length; i++) {
				(function(el){
					el.addEventListener('focus', function(){ unlockSecureField(el); }, true);
					el.addEventListener('mousedown', function(){ unlockSecureField(el); }, true);
					el.addEventListener('touchstart', function(){ unlockSecureField(el); }, true);
				})(secure[i]);
			}
		}
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
