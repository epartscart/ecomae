<?php
/**
 * Storefront registration — social sign-up, Retail / Wholesale tabs, country + TRN rules.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';

function epc_reg_h(string $value): string
{
	return htmlspecialchars((string)$value, ENT_QUOTES, 'UTF-8');
}

function epc_reg_auth_available(): bool
{
	return is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_common.php');
}

/** ISO 3166-1 alpha-2 country list (UAE + Gulf pinned, then alphabetical). */
function epc_reg_country_options(): array
{
	return epc_countries_registration_options();
}

function epc_reg_uae_emirates(): array
{
	return function_exists('epc_countries_uae_emirates') ? epc_countries_uae_emirates() : array('Dubai', 'Abu Dhabi', 'Sharjah', 'Ajman', 'Umm Al Quwain', 'Ras Al Khaimah', 'Fujairah');
}

function epc_reg_customer_type(array $post): string
{
	$type = strtolower(trim((string)($post['epc_customer_type'] ?? 'retail')));
	return in_array($type, array('retail', 'wholesale'), true) ? $type : 'retail';
}

function epc_reg_country_code(array $post): string
{
	$code = strtoupper(trim((string)($post['epc_reg_country'] ?? '')));
	return preg_match('/^[A-Z]{2}$/', $code) ? $code : '';
}

function epc_reg_trn_mode(array $post): string
{
	$mode = strtolower(trim((string)($post['epc_reg_trn_mode'] ?? '')));
	return in_array($mode, array('has_trn', 'not_available'), true) ? $mode : '';
}

function epc_reg_render_social_block(array $multilang_params): void
{
	if (!epc_reg_auth_available()) {
		return;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_social.php';
	$ui = epc_auth_login_context_for_ui('storefront');
	$langHref = (string)($multilang_params['lang_href'] ?? '/en/');
	$returnUrl = rtrim($langHref, '/') . '/';
	$tenantKey = epc_reg_h((string)($ui['tenant_key'] ?? ''));

	$siteName = function_exists('epc_site_trade_name') ? trim((string) epc_site_trade_name()) : '';
	if ($siteName === '') {
		$siteName = (string)($ui['login_label'] ?? 'our store');
	}

	$social = '';
	$buttonsFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_oauth_buttons.php';
	if (is_file($buttonsFile)) {
		require_once $buttonsFile;
		$social = epc_oauth_buttons_render(array(
			'context'       => 'storefront',
			'return_url'    => $returnUrl,
			'require_terms' => true,
			'divider'       => false,
		));
	}

	$sendUrl = json_encode((string)($ui['send_code_url'] ?? '/epc-auth-send-code.php'));
	$verifyUrl = json_encode((string)($ui['verify_code_url'] ?? '/epc-auth-verify-code.php'));
	$uid = 'epc_reg_auth';
	?>
	<div class="panel panel-default epc-reg-social-panel" style="margin-bottom:14px">
		<div class="panel-body epc-cp-auth-modern">
			<p class="epc-reg-auth-title" style="font-size:18px;font-weight:700;color:#111;margin:0 0 4px;text-align:center">Register and join <?php echo epc_reg_h($siteName); ?></p>
			<p class="help-block" style="margin-top:0;text-align:center">Quick sign-in with email code or social — or complete the form below for retail (instant) or wholesale (manager approval).</p>
			<?php
			if ($social !== '') {
				echo $social;
				echo '<div class="epc-social-divider"><span>Or</span></div>';
			}
			?>
			<div class="epc-reg-email-otp" id="<?php echo $uid; ?>" data-tenant-key="<?php echo $tenantKey; ?>" style="margin-top:4px">
				<div class="form-group" style="margin-bottom:8px">
					<input type="email" class="form-control" id="<?php echo $uid; ?>_email" autocomplete="email" placeholder="you@example.com" />
				</div>
				<input type="button" class="btn btn-ar btn-block epc-continue-email" id="<?php echo $uid; ?>_send" value="Continue with Email" />
				<div class="form-group epc-cp-auth-code-wrap" id="<?php echo $uid; ?>_codewrap" style="display:none;margin-top:12px;margin-bottom:8px">
					<input type="text" class="form-control" id="<?php echo $uid; ?>_code" inputmode="numeric" maxlength="6" autocomplete="one-time-code" placeholder="6-digit code" />
				</div>
				<input type="button" class="btn btn-ar btn-success btn-block" id="<?php echo $uid; ?>_verify" style="display:none;margin-top:8px" value="Verify &amp; sign in" />
				<p class="epc-cp-auth-msg" id="<?php echo $uid; ?>_msg" aria-live="polite"></p>
			</div>
		</div>
	</div>
	<style>.epc-continue-email{background:#111827;border-color:#111827;color:#fff;font-weight:600}.epc-continue-email:hover,.epc-continue-email:focus{background:#000;border-color:#000;color:#fff}</style>
	<script>
	(function(){
		var root=document.getElementById(<?php echo json_encode($uid); ?>);
		if(!root)return;
		var tenantKey=root.getAttribute('data-tenant-key')||'';
		var sendUrl=<?php echo $sendUrl; ?>,verifyUrl=<?php echo $verifyUrl; ?>;
		var returnUrl=<?php echo json_encode(rtrim($langHref, '/') . '/'); ?>;
		var msg=document.getElementById(<?php echo json_encode($uid . '_msg'); ?>);
		var codeWrap=document.getElementById(<?php echo json_encode($uid . '_codewrap'); ?>);
		var verifyBtn=document.getElementById(<?php echo json_encode($uid . '_verify'); ?>);
		function showMsg(t,ok){if(msg){msg.textContent=t;msg.className='epc-cp-auth-msg'+(ok?' is-ok':' is-err');}}
		document.getElementById(<?php echo json_encode($uid . '_send'); ?>).addEventListener('click',function(){
			var em=(document.getElementById(<?php echo json_encode($uid . '_email'); ?>)||{}).value||'';
			showMsg('Sending…',true);
			fetch(sendUrl,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:em,tenant_key:tenantKey,context:'storefront',return_url:returnUrl})})
			.then(function(r){return r.json();}).then(function(d){showMsg(d.message||'',!!d.ok);if(d.ok){codeWrap.style.display='';verifyBtn.style.display='';}}).catch(function(){showMsg('Network error',false);});
		});
		verifyBtn.addEventListener('click',function(){
			var em=(document.getElementById(<?php echo json_encode($uid . '_email'); ?>)||{}).value||'';
			var code=(document.getElementById(<?php echo json_encode($uid . '_code'); ?>)||{}).value||'';
			showMsg('Verifying…',true);
			fetch(verifyUrl,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:em,code:code,tenant_key:tenantKey,context:'storefront',return_url:returnUrl})})
			.then(function(r){return r.json();}).then(function(d){if(d.ok&&d.redirect){location.href=d.redirect;return;}showMsg(d.message||'Error',!!d.ok);}).catch(function(){showMsg('Network error',false);});
		});
	})();
	</script>
	<?php
}

function epc_reg_render_country_select(string $id = 'epc_reg_country', string $selected = '', string $name = ''): void
{
	if ($name === '') {
		$name = $id;
	}
	?>
	<select name="<?php echo epc_reg_h($name); ?>" id="<?php echo epc_reg_h($id); ?>" class="form-control epc-reg-country">
		<option value="">— Select country —</option>
		<?php foreach (epc_reg_country_options() as $code => $label) { ?>
		<option value="<?php echo epc_reg_h($code); ?>"<?php echo ($selected === $code) ? ' selected="selected"' : ''; ?>><?php echo epc_reg_h($label); ?></option>
		<?php } ?>
	</select>
	<?php
}

function epc_reg_render_account_tabs(): void
{
	?>
	<input type="hidden" name="epc_customer_type" id="epc_customer_type_field" value="retail" />
	<div class="panel panel-primary epc-reg-account-panel">
		<div class="panel-heading">Create your account</div>
		<div class="panel-body">
			<p class="help-block" style="margin-top:0;">Quick signup — same simple form for every customer. Documents and trade paperwork are collected later in the Control Panel after you are onboarded.</p>
			<div class="form-group">
				<label class="col-sm-4 col-lg-3 control-label">Account type*</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;">
					<label class="radio-inline" style="margin-right:18px;">
						<input type="radio" name="epc_customer_type_radio" value="retail" checked="checked" data-epc-type="retail" /> Retail
					</label>
					<label class="radio-inline">
						<input type="radio" name="epc_customer_type_radio" value="wholesale" data-epc-type="wholesale" /> Wholesale
					</label>
					<p class="help-block" style="margin:8px 0 0;">Retail is approved immediately. Wholesale pricing needs manager approval in CP.</p>
				</div>
			</div>
			<div class="form-group">
				<label for="epc_first_name" class="col-sm-4 col-lg-3 control-label">First name*</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_first_name" id="epc_first_name" maxlength="80" placeholder="Given name" autocomplete="given-name" /></div>
			</div>
			<div class="form-group">
				<label for="epc_last_name" class="col-sm-4 col-lg-3 control-label">Last name*</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_last_name" id="epc_last_name" maxlength="80" placeholder="Family name" autocomplete="family-name" /></div>
			</div>
			<div class="form-group">
				<label for="epc_mobile" class="col-sm-4 col-lg-3 control-label">Mobile phone*</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="tel" class="form-control epc-reg-phone" name="epc_mobile" id="epc_mobile" maxlength="30" placeholder="e.g. +971501234567" autocomplete="tel" /></div>
			</div>
			<div class="form-group">
				<label for="epc_country" class="col-sm-4 col-lg-3 control-label">Country*</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;"><?php epc_reg_render_country_select('epc_country', '', 'epc_country'); ?></div>
			</div>
			<div class="form-group">
				<label for="epc_city" class="col-sm-4 col-lg-3 control-label">City*</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_city" id="epc_city" maxlength="80" placeholder="City" autocomplete="address-level2" /></div>
			</div>
			<div class="form-group">
				<label for="epc_address" class="col-sm-4 col-lg-3 control-label">Address*</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_address" id="epc_address" maxlength="255" placeholder="Street, building, area" autocomplete="street-address" /></div>
			</div>
			<div class="form-group" id="epc_company_row" style="display:none;">
				<label for="epc_company_name" class="col-sm-4 col-lg-3 control-label">Company name</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_company_name" id="epc_company_name" maxlength="200" placeholder="Optional — can be completed later in CP" autocomplete="organization" /></div>
			</div>
			<div class="form-group">
				<label class="col-sm-4 col-lg-3 control-label">Notifications</label>
				<div class="col-sm-8 col-lg-9" style="padding:5px;">
					<label class="checkbox-inline"><input type="checkbox" name="epc_sms_notify" id="epc_sms_notify" value="1" /> Receive order updates via SMS</label>
				</div>
			</div>
		</div>
	</div>
	<?php epc_reg_render_tab_scripts(); ?>
	<?php
}

function epc_reg_render_tab_scripts(): void
{
	?>
	<style>
	.epc-reg-account-panel .form-group:last-child{margin-bottom:0}
	</style>
	<script>
	function epcRegActiveType(){
		var hf=document.getElementById('epc_customer_type_field');
		return (hf&&hf.value)?hf.value:'retail';
	}
	function epcRegSyncCountryHidden(){
		var sel=document.getElementById('epc_country');
		var hid=document.getElementById('epc_reg_country_sync');
		if(sel&&hid){hid.value=sel.value||'';}
	}
	function epcRegSyncTypeUi(){
		var t=epcRegActiveType();
		var row=document.getElementById('epc_company_row');
		if(row){row.style.display=(t==='wholesale')?'':'none';}
		epcRegSyncCountryHidden();
	}
	function epcRegMirrorTypedFields(){
		var t=epcRegActiveType();
		var map={
			first_name:(document.getElementById('epc_first_name')||{}).value||'',
			last_name:(document.getElementById('epc_last_name')||{}).value||'',
			mobile:(document.getElementById('epc_mobile')||{}).value||'',
			country:(document.getElementById('epc_country')||{}).value||'',
			city:(document.getElementById('epc_city')||{}).value||'',
			address:(document.getElementById('epc_address')||{}).value||'',
			company:(document.getElementById('epc_company_name')||{}).value||''
		};
		var prefix=t==='wholesale'?'epc_wholesale_':'epc_retail_';
		function ensureHidden(name,val){
			var el=document.getElementById(name);
			if(!el){
				el=document.createElement('input');
				el.type='hidden';
				el.name=name;
				el.id=name;
				var form=document.getElementById('regform');
				if(form)form.appendChild(el);
			}
			el.value=val;
		}
		ensureHidden(prefix+'first_name', map.first_name);
		ensureHidden(prefix+'last_name', map.last_name);
		ensureHidden(prefix+'mobile', map.mobile);
		ensureHidden(prefix+'country', map.country);
		ensureHidden(prefix+'city', map.city);
		ensureHidden(prefix+'address', map.address);
		if(t==='wholesale'){
			ensureHidden('epc_wholesale_company', map.company);
			ensureHidden('epc_wholesale_legal_name', map.company);
			ensureHidden('epc_wholesale_sms_notify', document.getElementById('epc_sms_notify')&&document.getElementById('epc_sms_notify').checked?'1':'');
		}else{
			ensureHidden('epc_retail_sms_notify', document.getElementById('epc_sms_notify')&&document.getElementById('epc_sms_notify').checked?'1':'');
		}
		epcRegSyncCountryHidden();
	}
	var epcRegDialMap=<?php echo json_encode(epc_countries_dial_codes()); ?>;
	function epcRegDialPrefix(cc){return epcRegDialMap[cc]?'+'+epcRegDialMap[cc]:'';}
	function epcRegPhoneHint(){
		var cc=(document.getElementById('epc_country')||{}).value||'';
		var ph=document.getElementById('epc_mobile');
		if(!ph)return;
		var p=epcRegDialPrefix(cc);
		ph.placeholder=p?('e.g. '+p+'501234567'):'Include country code e.g. +971501234567';
		if(p&&!(ph.value||'').trim()){ph.value=p+' ';}
	}
	(function(){
		var form=document.getElementById('regform');
		if(form){ form.setAttribute('novalidate','novalidate'); }
		document.querySelectorAll('input[name="epc_customer_type_radio"]').forEach(function(r){
			r.addEventListener('change',function(){
				var hf=document.getElementById('epc_customer_type_field');
				if(hf)hf.value=r.value||'retail';
				epcRegSyncTypeUi();
			});
		});
		var cc=document.getElementById('epc_country');
		if(cc){cc.addEventListener('change',function(){epcRegPhoneHint();epcRegSyncCountryHidden();});}
		epcRegSyncTypeUi();
		epcRegPhoneHint();
	})();
	function epcRegClientValidate(){
		var ids=['epc_first_name','epc_last_name','epc_mobile','epc_city','epc_address'];
		var labels={epc_first_name:'First name',epc_last_name:'Last name',epc_mobile:'Mobile phone',epc_city:'City',epc_address:'Address'};
		for(var i=0;i<ids.length;i++){
			var el=document.getElementById(ids[i]);
			if(!el||!String(el.value||'').trim()){alert('Please fill in: '+(labels[ids[i]]||ids[i]));if(el){try{el.focus();}catch(e){}}return false;}
		}
		var countryEl=document.getElementById('epc_country');
		if(!countryEl||!countryEl.value){alert('Please select your country.');if(countryEl){try{countryEl.focus();}catch(e){}}return false;}
		epcRegMirrorTypedFields();
		return true;
	}
	</script>
	<?php
}

/** @deprecated use epc_reg_render_account_tabs */
function epc_reg_render_uae_panel(): void
{
	// Legacy no-op — UAE fields are inside Wholesale tab.
}

function epc_reg_uae_requested(array $post): bool
{
	if (epc_reg_country_code($post) === 'AE') {
		$trn = epc_reg_extract_trn($post);
		return $trn !== '';
	}
	return !empty($post['epc_uae_company']) && (string)$post['epc_uae_company'] !== '0';
}

function epc_reg_extract_trn(array $post): string
{
	$country = epc_reg_country_code($post);
	if ($country === 'AE') {
		return preg_replace('/\D/', '', (string)($post['epc_wholesale_trn'] ?? $post['epc_uae_trn'] ?? ''));
	}
	if (epc_reg_trn_mode($post) === 'has_trn') {
		return trim((string)($post['epc_wholesale_trn_optional'] ?? ''));
	}
	return '';
}

function epc_reg_validate_enhanced_fields(array $post, string $customer_type): void
{
	$customer_type = epc_reg_customer_type(array_merge($post, array('epc_customer_type' => $customer_type)));

	// Prefer shared simple signup fields; fall back to typed prefixes.
	$first = trim((string)($post['epc_first_name'] ?? $post['epc_' . ($customer_type === 'wholesale' ? 'wholesale' : 'retail') . '_first_name'] ?? ''));
	$last = trim((string)($post['epc_last_name'] ?? $post['epc_' . ($customer_type === 'wholesale' ? 'wholesale' : 'retail') . '_last_name'] ?? ''));
	$mobile = trim((string)($post['epc_mobile'] ?? $post['epc_' . ($customer_type === 'wholesale' ? 'wholesale' : 'retail') . '_mobile'] ?? ''));
	$city = trim((string)($post['epc_city'] ?? $post['epc_' . ($customer_type === 'wholesale' ? 'wholesale' : 'retail') . '_city'] ?? ''));
	$address = trim((string)($post['epc_address'] ?? $post['epc_' . ($customer_type === 'wholesale' ? 'wholesale' : 'retail') . '_address'] ?? ''));
	$country = strtoupper(trim((string)($post['epc_country'] ?? $post['epc_reg_country'] ?? $post['epc_' . ($customer_type === 'wholesale' ? 'wholesale' : 'retail') . '_country'] ?? '')));

	$required = array(
		'First name' => $first,
		'Last name' => $last,
		'Mobile phone' => $mobile,
		'City' => $city,
		'Address' => $address,
	);
	foreach ($required as $label => $val) {
		if ($val === '') {
			throw new Exception('Please fill in: ' . $label);
		}
	}
	if ($country === '' || !array_key_exists($country, epc_reg_country_options())) {
		throw new Exception('Please select your country.');
	}
	// KYC / trade documents are collected later in CP after onboarding — not at signup.
}

/**
 * Store a KYC/AML registration document into users_profiles as a public path.
 * Used by Control Panel / post-onboarding flows (not required at initial signup).
 */
function epc_reg_store_kyc_upload(int $user_id, string $fieldKey, array $file): string
{
	if ($user_id <= 0 || empty($file['tmp_name']) || !is_uploaded_file((string) $file['tmp_name'])) {
		return '';
	}
	$max = 8 * 1024 * 1024;
	if ((int) ($file['size'] ?? 0) > $max) {
		throw new Exception('Document too large (max 8 MB): ' . $fieldKey);
	}
	$ext = strtolower(pathinfo((string) ($file['name'] ?? ''), PATHINFO_EXTENSION));
	$allowed = array('pdf', 'jpg', 'jpeg', 'png', 'webp');
	if (!in_array($ext, $allowed, true)) {
		throw new Exception('Allowed document formats: PDF, JPG, PNG, WEBP.');
	}
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/kyc/' . $user_id;
	if (!is_dir($dir) && !mkdir($dir, 0755, true) && !is_dir($dir)) {
		throw new Exception('Could not create KYC upload folder.');
	}
	$safe = preg_replace('/[^a-z0-9_]/', '', strtolower($fieldKey)) . '_' . date('Ymd_His') . '.' . $ext;
	$dest = $dir . '/' . $safe;
	if (!move_uploaded_file((string) $file['tmp_name'], $dest)) {
		throw new Exception('Document upload failed: ' . $fieldKey);
	}
	return '/content/files/kyc/' . $user_id . '/' . $safe;
}

function epc_reg_validate_trade_uae(array $post, string $customer_type): void
{
	epc_reg_validate_enhanced_fields($post, $customer_type);
}

function epc_reg_validate_uae_fields(array $post): void
{
	// Initial signup is simple — UAE TRN / e-invoice buyer details are completed later in CP.
	return;
}

function epc_reg_profile_set($db_link, int $user_id, string $key, string $value): void
{
	if ($user_id <= 0 || $key === '' || !isset($db_link) || !$db_link) {
		return;
	}
	try {
		$db_link->prepare('DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?;')->execute(array($user_id, $key));
		$db_link->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?);')->execute(array($user_id, $key, $value));
	} catch (Throwable $e) {
	}
}

function epc_reg_save_enhanced_profile($db_link, int $user_id, array $post, string $reg_contact, string $reg_contact_type): void
{
	if ($user_id <= 0) {
		return;
	}
	$type = epc_reg_customer_type($post);
	$prefix = $type === 'wholesale' ? 'epc_wholesale_' : 'epc_retail_';

	$first = trim((string)($post['epc_first_name'] ?? $post[$prefix . 'first_name'] ?? ''));
	$last = trim((string)($post['epc_last_name'] ?? $post[$prefix . 'last_name'] ?? ''));
	$mobile = trim((string)($post['epc_mobile'] ?? $post[$prefix . 'mobile'] ?? ''));
	$city = trim((string)($post['epc_city'] ?? $post[$prefix . 'city'] ?? ''));
	$address = trim((string)($post['epc_address'] ?? $post[$prefix . 'address'] ?? ''));
	$country = strtoupper(trim((string)($post['epc_country'] ?? $post['epc_reg_country'] ?? $post[$prefix . 'country'] ?? '')));
	$company = trim((string)($post['epc_company_name'] ?? $post['epc_wholesale_company'] ?? ''));
	$smsOn = !empty($post['epc_sms_notify']) || !empty($post['epc_retail_sms_notify']) || !empty($post['epc_wholesale_sms_notify']);

	epc_reg_profile_set($db_link, $user_id, 'epc_reg_country', $country);
	epc_reg_profile_set($db_link, $user_id, 'name', $first);
	epc_reg_profile_set($db_link, $user_id, 'surname', $last);
	if ($mobile !== '') {
		epc_reg_profile_set($db_link, $user_id, 'phone', $mobile);
	}
	epc_reg_profile_set($db_link, $user_id, 'epc_reg_city', $city);
	epc_reg_profile_set($db_link, $user_id, 'epc_reg_address', $address);
	epc_reg_profile_set($db_link, $user_id, 'epc_reg_sms_notify', $smsOn ? '1' : '0');

	if ($type === 'wholesale' && $company !== '') {
		epc_reg_profile_set($db_link, $user_id, 'company_name', $company);
		epc_reg_profile_set($db_link, $user_id, 'epc_reg_legal_name', $company);
	}

	// Sync legacy reg_fields where present in POST
	foreach (array('name' => $first, 'surname' => $last, 'company_name' => $company) as $legacyKey => $legacyVal) {
		if ($legacyVal !== '' && isset($post[$legacyKey])) {
			epc_reg_profile_set($db_link, $user_id, $legacyKey, $legacyVal);
		}
	}

	$taxFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit.php';
	if (is_readable($taxFile) && $db_link instanceof PDO) {
		require_once $taxFile;
	}

	$vatFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
	if (is_readable($vatFile) && $db_link instanceof PDO) {
		require_once $vatFile;
		epc_uae_customer_vat_sync($db_link, $user_id);
	}

	// Light buyer stub only — full TRN / KYC / documents are completed later in CP.
	$einvoiceFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
	if (is_readable($einvoiceFile) && $db_link instanceof PDO && $country !== '') {
		require_once $einvoiceFile;
		if (function_exists('epc_einvoice_save_buyer_profile')) {
			$buyerName = trim($first . ' ' . $last);
			if ($type === 'wholesale' && $company !== '') {
				$buyerName = $company;
			}
			epc_einvoice_save_buyer_profile($db_link, array(
				'user_id' => $user_id,
				'buyer_name' => $buyerName !== '' ? $buyerName : ('Customer #' . $user_id),
				'trn' => '',
				'legal_reg_no' => '',
				'legal_reg_type' => $type === 'wholesale' ? 'TL' : 'EID',
				'address_line1' => $address,
				'city' => $city,
				'emirate' => 'Dubai',
				'country_code' => $country,
				'email' => $reg_contact_type === 'email' ? trim($reg_contact) : '',
				'phone' => $mobile,
				'buyer_onboarded' => 0,
			));
		}
	}
}

function epc_reg_save_uae_buyer_profile($db_link, int $user_id, array $post, string $reg_contact, string $reg_contact_type): void
{
	epc_reg_save_enhanced_profile($db_link, $user_id, $post, $reg_contact, $reg_contact_type);
}
