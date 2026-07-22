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

	$sendUrl = json_encode((string)($ui['send_code_url'] ?? '/content/general_pages/epc_auth_api_send_code.php'));
	$verifyUrl = json_encode((string)($ui['verify_code_url'] ?? '/content/general_pages/epc_auth_api_verify_code.php'));
	$uid = 'epc_reg_auth';
	?>
	<div class="panel panel-default epc-reg-social-panel" style="margin-bottom:14px">
		<div class="panel-body epc-cp-auth-modern">
			<p class="epc-reg-auth-title" style="font-size:18px;font-weight:700;color:#111;margin:0 0 4px;text-align:center">Register and join <?php echo epc_reg_h($siteName); ?></p>
			<p class="help-block" style="margin-top:0;text-align:center">Quick sign-in with email code or social — or complete the form below for retail (instant) or wholesale (subject to approval only).</p>
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
		function parseJsonResponse(r){
			return r.text().then(function(t){
				var d=null;
				try{d=t?JSON.parse(t):null;}catch(e){d=null;}
				if(!d||typeof d!=='object'){
					var hint=(r.status===403)?'Sign-in service blocked — please contact support.':'Network error — please retry.';
					if(r.status&&r.status!==200&&r.status!==400){hint='Request failed ('+r.status+'). Please retry.';}
					return {ok:false,message:hint};
				}
				if(d.ok===undefined&&!r.ok){d.ok=false;if(!d.message)d.message='Request failed ('+r.status+'). Please retry.';}
				return d;
			});
		}
		document.getElementById(<?php echo json_encode($uid . '_send'); ?>).addEventListener('click',function(){
			var em=(document.getElementById(<?php echo json_encode($uid . '_email'); ?>)||{}).value||'';
			showMsg('Sending…',true);
			fetch(sendUrl,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:em,tenant_key:tenantKey,context:'storefront',return_url:returnUrl})})
			.then(parseJsonResponse).then(function(d){showMsg(d.message||'',!!d.ok);if(d.ok){codeWrap.style.display='';verifyBtn.style.display='';}}).catch(function(){showMsg('Network error — please retry.',false);});
		});
		verifyBtn.addEventListener('click',function(){
			var em=(document.getElementById(<?php echo json_encode($uid . '_email'); ?>)||{}).value||'';
			var code=(document.getElementById(<?php echo json_encode($uid . '_code'); ?>)||{}).value||'';
			showMsg('Verifying…',true);
			fetch(verifyUrl,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:em,code:code,tenant_key:tenantKey,context:'storefront',return_url:returnUrl})})
			.then(parseJsonResponse).then(function(d){if(d.ok&&d.redirect){location.href=d.redirect;return;}showMsg(d.message||'Error',!!d.ok);}).catch(function(){showMsg('Network error — please retry.',false);});
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
	$countries = epc_reg_country_options();
	$emirates = epc_reg_uae_emirates();
	?>
	<input type="hidden" name="epc_customer_type" id="epc_customer_type_field" value="retail" />
	<div class="panel panel-primary epc-reg-account-panel">
		<div class="panel-heading">Customer type</div>
		<div class="panel-body">
			<p class="help-block" style="margin-top:0;">Fields marked <strong>*</strong> are mandatory. <strong>Retail customer</strong> — short form (name, phone, address); approved immediately; no KYC documents. <strong>Wholesale customer</strong> — <em>subject to approval only</em>; company details plus <em>Additional information</em> (KYC / AML documents) required; pending until a manager approves trade pricing and currency in the Control Panel.</p>
			<ul class="nav nav-tabs epc-reg-type-tabs" role="tablist">
				<li role="presentation" class="active"><a href="#epc_reg_tab_retail" aria-controls="epc_reg_tab_retail" role="tab" data-toggle="tab" data-epc-type="retail">Retail customer</a></li>
				<li role="presentation"><a href="#epc_reg_tab_wholesale" aria-controls="epc_reg_tab_wholesale" role="tab" data-toggle="tab" data-epc-type="wholesale">Wholesale customer <span style="font-weight:600;opacity:.85">(subject to approval only)</span></a></li>
			</ul>
			<div class="tab-content epc-reg-type-panes" style="padding-top:14px;">
				<div role="tabpanel" class="tab-pane active" id="epc_reg_tab_retail">
					<div class="form-group">
						<label for="epc_retail_first_name" class="col-sm-4 col-lg-3 control-label">First name*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-retail-req" name="epc_retail_first_name" id="epc_retail_first_name" maxlength="80" placeholder="Given name" /></div>
					</div>
					<div class="form-group">
						<label for="epc_retail_last_name" class="col-sm-4 col-lg-3 control-label">Last name*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-retail-req" name="epc_retail_last_name" id="epc_retail_last_name" maxlength="80" placeholder="Family name" /></div>
					</div>
					<div class="form-group">
						<label for="epc_retail_mobile" class="col-sm-4 col-lg-3 control-label">Mobile phone*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="tel" class="form-control epc-reg-retail-req epc-reg-phone" name="epc_retail_mobile" id="epc_retail_mobile" maxlength="30" placeholder="e.g. +971501234567" data-epc-phone-for="epc_retail_country" /></div>
					</div>
					<div class="form-group">
						<label for="epc_retail_country" class="col-sm-4 col-lg-3 control-label">Country*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><?php epc_reg_render_country_select('epc_retail_country', '', 'epc_retail_country'); ?></div>
					</div>
					<div class="form-group">
						<label for="epc_retail_city" class="col-sm-4 col-lg-3 control-label">City*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-retail-req" name="epc_retail_city" id="epc_retail_city" maxlength="80" placeholder="City" /></div>
					</div>
					<div class="form-group">
						<label for="epc_retail_address" class="col-sm-4 col-lg-3 control-label">Delivery address*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-retail-req" name="epc_retail_address" id="epc_retail_address" maxlength="255" placeholder="Street, building, area" /></div>
					</div>
					<div class="form-group epc-reg-retail-emirate-row" id="epc_retail_emirate_row" style="display:none">
						<label for="epc_retail_emirate" class="col-sm-4 col-lg-3 control-label">Emirate</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;">
							<select name="epc_retail_emirate" id="epc_retail_emirate" class="form-control">
								<?php foreach ($emirates as $em) { ?>
								<option value="<?php echo epc_reg_h($em); ?>"><?php echo epc_reg_h($em); ?></option>
								<?php } ?>
							</select>
						</div>
					</div>
					<div class="form-group" id="epc_retail_state_row">
						<label for="epc_retail_state" class="col-sm-4 col-lg-3 control-label" id="epc_retail_state_label">State / region</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_retail_state" id="epc_retail_state" maxlength="80" placeholder="Emirate, province, or region" /></div>
					</div>
					<div class="form-group">
						<label for="epc_retail_postal" class="col-sm-4 col-lg-3 control-label">Postal / ZIP code</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_retail_postal" id="epc_retail_postal" maxlength="20" placeholder="e.g. 00000" /></div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 col-lg-3 control-label">Notifications</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;">
							<label class="checkbox-inline"><input type="checkbox" name="epc_retail_sms_notify" value="1" /> Receive order updates via SMS</label>
						</div>
					</div>
				</div>
				<div role="tabpanel" class="tab-pane" id="epc_reg_tab_wholesale">
					<div class="form-group">
						<label for="epc_wholesale_company" class="col-sm-4 col-lg-3 control-label">Company name*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_company" id="epc_wholesale_company" maxlength="200" placeholder="Registered business name" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_legal_name" class="col-sm-4 col-lg-3 control-label">Legal entity name*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_legal_name" id="epc_wholesale_legal_name" maxlength="200" placeholder="As on trade licence" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_first_name" class="col-sm-4 col-lg-3 control-label">Contact first name*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_first_name" id="epc_wholesale_first_name" maxlength="80" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_last_name" class="col-sm-4 col-lg-3 control-label">Contact last name*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_last_name" id="epc_wholesale_last_name" maxlength="80" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_job_title" class="col-sm-4 col-lg-3 control-label">Contact job title*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_job_title" id="epc_wholesale_job_title" maxlength="80" placeholder="e.g. Purchasing manager" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_mobile" class="col-sm-4 col-lg-3 control-label">Mobile phone*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="tel" class="form-control epc-reg-wholesale-req epc-reg-phone" name="epc_wholesale_mobile" id="epc_wholesale_mobile" maxlength="30" placeholder="e.g. +971501234567" data-epc-phone-for="epc_wholesale_country" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_country" class="col-sm-4 col-lg-3 control-label">Country*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><?php epc_reg_render_country_select('epc_wholesale_country', '', 'epc_wholesale_country'); ?></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_city" class="col-sm-4 col-lg-3 control-label">City*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_city" id="epc_wholesale_city" maxlength="80" value="Dubai" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_address" class="col-sm-4 col-lg-3 control-label">Business address*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_address" id="epc_wholesale_address" maxlength="255" placeholder="Street, building, PO box" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_postal" class="col-sm-4 col-lg-3 control-label">Postal / ZIP code</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_wholesale_postal" id="epc_wholesale_postal" maxlength="20" placeholder="e.g. 00000" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_business_type" class="col-sm-4 col-lg-3 control-label">Business type*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;">
							<select name="epc_wholesale_business_type" id="epc_wholesale_business_type" class="form-control epc-reg-wholesale-req">
								<option value="">— Select —</option>
								<option value="distributor">Distributor / wholesaler</option>
								<option value="retailer">Retailer / reseller</option>
								<option value="workshop">Workshop / garage</option>
								<option value="fleet">Fleet / transport</option>
								<option value="manufacturer">Manufacturer</option>
								<option value="other">Other</option>
							</select>
						</div>
					</div>
					<div class="form-group epc-reg-uae-emirate-row" id="epc_wholesale_emirate_row" style="display:none">
						<label for="epc_wholesale_emirate" class="col-sm-4 col-lg-3 control-label">Emirate*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;">
							<select name="epc_wholesale_emirate" id="epc_wholesale_emirate" class="form-control">
								<?php foreach ($emirates as $em) { ?>
								<option value="<?php echo epc_reg_h($em); ?>"><?php echo epc_reg_h($em); ?></option>
								<?php } ?>
							</select>
						</div>
					</div>
					<div class="form-group" id="epc_wholesale_trn_block">
						<label class="col-sm-4 col-lg-3 control-label">Tax registration (TRN)</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;">
							<p class="help-block" style="margin-top:0;" id="epc_trn_help">UAE companies must provide a 15-digit TRN. Other countries may enter TRN or mark as not available.</p>
							<div id="epc_trn_uae_only" style="display:none">
								<input type="text" class="form-control" name="epc_wholesale_trn" id="epc_wholesale_trn" maxlength="15" placeholder="e.g. 100123456700003" />
							</div>
							<div id="epc_trn_non_uae" style="display:none">
								<label class="radio-inline" style="margin-right:14px;"><input type="radio" name="epc_reg_trn_mode" value="has_trn" /> I have a TRN / VAT number</label>
								<label class="radio-inline"><input type="radio" name="epc_reg_trn_mode" value="not_available" /> Not available</label>
								<input type="text" class="form-control" name="epc_wholesale_trn_optional" id="epc_wholesale_trn_optional" maxlength="20" placeholder="TRN / VAT number" style="margin-top:8px;display:none" />
							</div>
						</div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_trade_licence" class="col-sm-4 col-lg-3 control-label">Trade licence no.*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control epc-reg-wholesale-req" name="epc_wholesale_trade_licence" id="epc_wholesale_trade_licence" maxlength="60" placeholder="Commercial / trade licence number" /></div>
					</div>
					<div class="form-group">
						<label for="epc_wholesale_website" class="col-sm-4 col-lg-3 control-label">Company website</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="url" class="form-control" name="epc_wholesale_website" id="epc_wholesale_website" maxlength="200" placeholder="https://example.com" /></div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 col-lg-3 control-label">Notifications</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;">
							<label class="checkbox-inline"><input type="checkbox" name="epc_wholesale_sms_notify" value="1" checked="checked" /> Receive order updates via SMS</label>
						</div>
					</div>

					<hr style="margin:18px 0 12px;border-top:1px solid #e5e7eb;" />
					<p class="help-block" style="margin:0 0 4px;font-size:15px;font-weight:700;color:#0f172a;">Additional information (wholesale only)</p>
					<p class="help-block" style="margin:0 0 12px;"><strong>KYC / AML &amp; e-invoice documents</strong> — required for wholesale approval under UAE compliance practice. Not shown for retail customers. PDF or image, max 8&nbsp;MB each.</p>
					<div class="form-group">
						<label for="epc_emirates_id_no" class="col-sm-4 col-lg-3 control-label">Emirates ID no.</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_emirates_id_no" id="epc_emirates_id_no" maxlength="20" placeholder="784-XXXX-XXXXXXX-X" /></div>
					</div>
					<div class="form-group">
						<label for="epc_authorized_signatory" class="col-sm-4 col-lg-3 control-label">Authorized signatory</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_authorized_signatory" id="epc_authorized_signatory" maxlength="120" placeholder="Full name as on Emirates ID / passport" /></div>
					</div>
					<div class="form-group">
						<label for="epc_authorized_signatory_id" class="col-sm-4 col-lg-3 control-label">Signatory ID no.</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_authorized_signatory_id" id="epc_authorized_signatory_id" maxlength="40" /></div>
					</div>
					<div class="form-group">
						<label for="epc_ubo_name" class="col-sm-4 col-lg-3 control-label">Ultimate beneficial owner</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_ubo_name" id="epc_ubo_name" maxlength="160" placeholder="UBO full name (25%+ ownership)" /></div>
					</div>
					<div class="form-group">
						<label for="epc_pep_declaration" class="col-sm-4 col-lg-3 control-label">PEP declaration*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;">
							<select name="epc_pep_declaration" id="epc_pep_declaration" class="form-control epc-reg-wholesale-req">
								<option value="">— Select —</option>
								<option value="No">No — not a politically exposed person</option>
								<option value="Yes">Yes — PEP / related to a PEP</option>
							</select>
						</div>
					</div>
					<div class="form-group">
						<label for="epc_source_of_funds" class="col-sm-4 col-lg-3 control-label">Source of funds</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="text" class="form-control" name="epc_source_of_funds" id="epc_source_of_funds" maxlength="255" placeholder="Business income, investment, etc." /></div>
					</div>
					<div class="form-group">
						<label for="epc_doc_trade_licence" class="col-sm-4 col-lg-3 control-label">Trade licence scan*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="file" class="form-control epc-reg-wholesale-file" name="epc_doc_trade_licence" id="epc_doc_trade_licence" accept=".pdf,.jpg,.jpeg,.png,.webp" /></div>
					</div>
					<div class="form-group">
						<label for="epc_doc_emirates_id" class="col-sm-4 col-lg-3 control-label">Emirates ID copy*</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="file" class="form-control epc-reg-wholesale-file" name="epc_doc_emirates_id" id="epc_doc_emirates_id" accept=".pdf,.jpg,.jpeg,.png,.webp" /></div>
					</div>
					<div class="form-group">
						<label for="epc_doc_vat_certificate" class="col-sm-4 col-lg-3 control-label">VAT / TRN certificate</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="file" class="form-control" name="epc_doc_vat_certificate" id="epc_doc_vat_certificate" accept=".pdf,.jpg,.jpeg,.png,.webp" /></div>
					</div>
					<div class="form-group">
						<label for="epc_ubo_id_document" class="col-sm-4 col-lg-3 control-label">UBO ID document</label>
						<div class="col-sm-8 col-lg-9" style="padding:5px;"><input type="file" class="form-control" name="epc_ubo_id_document" id="epc_ubo_id_document" accept=".pdf,.jpg,.jpeg,.png,.webp" /></div>
					</div>
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
	.epc-reg-type-tabs{margin-bottom:0}
	.epc-reg-type-tabs>li>a{font-weight:600}
	.epc-reg-account-panel .tab-pane .form-group:last-child{margin-bottom:0}
	</style>
	<script>
	function epcRegActiveType(){
		var hf=document.getElementById('epc_customer_type_field');
		return (hf&&hf.value)?hf.value:'retail';
	}
	function epcRegSyncCountryHidden(){
		var t=epcRegActiveType();
		var sel=document.getElementById(t==='wholesale'?'epc_wholesale_country':'epc_retail_country');
		var hid=document.getElementById('epc_reg_country_sync');
		if(sel&&hid){hid.value=sel.value||'';}
	}
	var epcRegDialMap=<?php echo json_encode(epc_countries_dial_codes()); ?>;
	var epcRegAddrMeta=<?php echo json_encode(array(
		'AE' => epc_countries_address_meta('AE'),
		'US' => epc_countries_address_meta('US'),
		'GB' => epc_countries_address_meta('GB'),
		'default' => epc_countries_address_meta(''),
	)); ?>;
	function epcRegDialPrefix(cc){return epcRegDialMap[cc]?'+'+epcRegDialMap[cc]:'';}
	function epcRegAddrFor(cc){return epcRegAddrMeta[cc]||epcRegAddrMeta.default;}
	function epcRegPhoneHint(countryId,phoneId){
		var cc=(document.getElementById(countryId)||{}).value||'';
		var ph=document.getElementById(phoneId);
		if(!ph)return;
		var p=epcRegDialPrefix(cc);
		ph.placeholder=p?('e.g. '+p+'501234567'):'Include country code e.g. +971501234567';
		if(p&&!(ph.value||'').trim()){ph.value=p+' ';}
	}
	function epcRegRetailCountryUi(){
		var cc=(document.getElementById('epc_retail_country')||{}).value||'';
		var meta=epcRegAddrFor(cc);
		var emRow=document.getElementById('epc_retail_emirate_row');
		var stRow=document.getElementById('epc_retail_state_row');
		var stLbl=document.getElementById('epc_retail_state_label');
		if(emRow)emRow.style.display=meta.use_emirate_select?'':'none';
		if(stRow)stRow.style.display=meta.use_emirate_select?'none':'';
		if(stLbl)stLbl.textContent=meta.state_label||'State / region';
		epcRegPhoneHint('epc_retail_country','epc_retail_mobile');
		epcRegSyncCountryHidden();
	}
	function epcRegWholesaleTrnUi(){
		var country=(document.getElementById('epc_wholesale_country')||{}).value||'';
		var uae=document.getElementById('epc_trn_uae_only');
		var non=document.getElementById('epc_trn_non_uae');
		var emRow=document.getElementById('epc_wholesale_emirate_row');
		var help=document.getElementById('epc_trn_help');
		if(country==='AE'){
			if(uae)uae.style.display='';
			if(non)non.style.display='none';
			if(emRow)emRow.style.display='';
			if(help)help.textContent='UAE B2B: 15-digit TRN is mandatory for e-invoicing.';
		}else{
			if(uae)uae.style.display='none';
			if(non)non.style.display='';
			if(emRow)emRow.style.display='none';
			if(help)help.textContent='Enter your TRN/VAT number if you have one, or select Not available.';
		}
		epcRegPhoneHint('epc_wholesale_country','epc_wholesale_mobile');
		epcRegSyncCountryHidden();
	}
	function epcRegTrnOptionalToggle(){
		var mode=document.querySelector('input[name="epc_reg_trn_mode"]:checked');
		var inp=document.getElementById('epc_wholesale_trn_optional');
		if(!inp)return;
		inp.style.display=(mode&&mode.value==='has_trn')?'':'none';
	}
	function epcRegSetPaneEnabled(pane, enabled){
		if(!pane)return;
		var nodes=pane.querySelectorAll('input, select, textarea, button');
		for(var i=0;i<nodes.length;i++){
			var el=nodes[i];
			if(enabled){
				if(el.getAttribute('data-epc-was-disabled')==='1'){
					el.removeAttribute('disabled');
					el.removeAttribute('data-epc-was-disabled');
				}
			}else{
				if(!el.disabled){
					el.setAttribute('data-epc-was-disabled','1');
					el.disabled=true;
				}
			}
		}
	}
	function epcRegSyncTabFields(){
		var t=epcRegActiveType();
		var retail=document.getElementById('epc_reg_tab_retail');
		var wholesale=document.getElementById('epc_reg_tab_wholesale');
		epcRegSetPaneEnabled(retail, t==='retail');
		epcRegSetPaneEnabled(wholesale, t==='wholesale');
		epcRegSyncCountryHidden();
	}
	(function(){
		var form=document.getElementById('regform');
		if(form){ form.setAttribute('novalidate','novalidate'); }
		var tabs=document.querySelectorAll('.epc-reg-type-tabs a[data-epc-type]');
		tabs.forEach(function(tab){
			tab.addEventListener('shown.bs.tab',function(){
				var hf=document.getElementById('epc_customer_type_field');
				if(hf)hf.value=tab.getAttribute('data-epc-type')||'retail';
				epcRegSyncTabFields();
			});
		});
		var wc=document.getElementById('epc_wholesale_country');
		if(wc){wc.addEventListener('change',epcRegWholesaleTrnUi);}
		document.querySelectorAll('input[name="epc_reg_trn_mode"]').forEach(function(r){
			r.addEventListener('change',epcRegTrnOptionalToggle);
		});
		var rc=document.getElementById('epc_retail_country');
		if(rc){rc.addEventListener('change',epcRegRetailCountryUi);}
		epcRegRetailCountryUi();
		epcRegWholesaleTrnUi();
		epcRegTrnOptionalToggle();
		epcRegSyncTabFields();
	})();
	function epcRegClientValidate(){
		epcRegSyncTabFields();
		var t=epcRegActiveType();
		var labels={epc_retail_first_name:'First name',epc_retail_last_name:'Last name',epc_retail_mobile:'Mobile phone',epc_retail_city:'City',epc_retail_address:'Delivery address',epc_wholesale_company:'Company name',epc_wholesale_legal_name:'Legal entity name',epc_wholesale_first_name:'Contact first name',epc_wholesale_last_name:'Contact last name',epc_wholesale_job_title:'Contact job title',epc_wholesale_mobile:'Mobile phone',epc_wholesale_city:'City',epc_wholesale_address:'Business address',epc_wholesale_business_type:'Business type',epc_wholesale_trade_licence:'Trade licence no.'};
		var ids=t==='wholesale'?['epc_wholesale_company','epc_wholesale_legal_name','epc_wholesale_first_name','epc_wholesale_last_name','epc_wholesale_job_title','epc_wholesale_mobile','epc_wholesale_city','epc_wholesale_address','epc_wholesale_business_type','epc_wholesale_trade_licence']:['epc_retail_first_name','epc_retail_last_name','epc_retail_mobile','epc_retail_city','epc_retail_address'];
		for(var i=0;i<ids.length;i++){
			var el=document.getElementById(ids[i]);
			if(!el||!String(el.value||'').trim()){alert('Please fill in: '+(labels[ids[i]]||ids[i]));if(el){try{el.focus();}catch(e){}}return false;}
		}
		var countryEl=document.getElementById(t==='wholesale'?'epc_wholesale_country':'epc_retail_country');
		if(!countryEl||!countryEl.value){alert('Please select your country.');if(countryEl){try{countryEl.focus();}catch(e){}}return false;}
		if(t==='wholesale'){
			if(countryEl.value==='AE'){
				var trn=(document.getElementById('epc_wholesale_trn')||{}).value||'';
				trn=trn.replace(/\D/g,'');
				if(trn.length!==15){alert('UAE TRN must be 15 digits.');return false;}
			}else{
				var mode=document.querySelector('#epc_reg_tab_wholesale input[name="epc_reg_trn_mode"]:checked');
				if(!mode){alert('Please choose TRN status: enter TRN or Not available.');return false;}
				if(mode.value==='has_trn'){
					var opt=(document.getElementById('epc_wholesale_trn_optional')||{}).value||'';
					if(!opt.trim()){alert('Please enter your TRN / VAT number.');return false;}
				}
			}
			var pep=document.getElementById('epc_pep_declaration');
			if(!pep||!String(pep.value||'').trim()){alert('Please complete the PEP declaration.');if(pep){try{pep.focus();}catch(e){}}return false;}
			var tlFile=document.getElementById('epc_doc_trade_licence');
			var eidFile=document.getElementById('epc_doc_emirates_id');
			if(!tlFile||!tlFile.files||!tlFile.files.length){alert('Please upload your trade licence scan.');if(tlFile){try{tlFile.focus();}catch(e){}}return false;}
			if(!eidFile||!eidFile.files||!eidFile.files.length){alert('Please upload your Emirates ID copy.');if(eidFile){try{eidFile.focus();}catch(e){}}return false;}
		}
		epcRegSyncCountryHidden();
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
	$prefix = $customer_type === 'wholesale' ? 'epc_wholesale_' : 'epc_retail_';
	$required = $customer_type === 'wholesale'
		? array('company', 'legal_name', 'first_name', 'last_name', 'job_title', 'mobile', 'city', 'address', 'business_type', 'trade_licence')
		: array('first_name', 'last_name', 'mobile', 'city', 'address');
	$labels = array(
		'first_name' => 'First name', 'last_name' => 'Last name', 'mobile' => 'Mobile phone',
		'city' => 'City', 'address' => 'Delivery address', 'company' => 'Company name',
		'legal_name' => 'Legal entity name', 'job_title' => 'Contact job title',
		'business_type' => 'Business type', 'trade_licence' => 'Trade licence no.',
	);
	foreach ($required as $key) {
		$field = $prefix . $key;
		if (trim((string)($post[$field] ?? '')) === '') {
			throw new Exception('Please fill in: ' . ($labels[$key] ?? $key));
		}
	}
	$countryKey = $prefix . 'country';
	$country = strtoupper(trim((string)($post[$countryKey] ?? $post['epc_reg_country'] ?? '')));
	if ($country === '' || !array_key_exists($country, epc_reg_country_options())) {
		throw new Exception('Please select your country.');
	}
	if ($customer_type === 'wholesale') {
		if ($country === 'AE') {
			$trn = preg_replace('/\D/', '', (string)($post['epc_wholesale_trn'] ?? ''));
			if (strlen($trn) !== 15) {
				throw new Exception('UAE TRN must be 15 digits.');
			}
		} else {
			$mode = epc_reg_trn_mode($post);
			if ($mode === '') {
				throw new Exception('Please choose TRN status: enter TRN or Not available.');
			}
			if ($mode === 'has_trn' && trim((string)($post['epc_wholesale_trn_optional'] ?? '')) === '') {
				throw new Exception('Please enter your TRN / VAT number.');
			}
		}
		if (trim((string) ($post['epc_pep_declaration'] ?? '')) === '') {
			throw new Exception('Please complete the PEP declaration.');
		}
		foreach (array('epc_doc_trade_licence' => 'trade licence scan', 'epc_doc_emirates_id' => 'Emirates ID copy') as $fileKey => $label) {
			if (empty($_FILES[$fileKey]['tmp_name']) || !is_uploaded_file((string) $_FILES[$fileKey]['tmp_name'])) {
				throw new Exception('Please upload your ' . $label . '.');
			}
		}
	}
}

/**
 * Store a KYC/AML registration document into users_profiles as a public path.
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
	if (epc_reg_country_code($post) !== 'AE') {
		return;
	}
	$customer_type = epc_reg_customer_type($post);
	if ($customer_type !== 'wholesale') {
		return;
	}
	$buyerName = trim((string)($post['epc_wholesale_legal_name'] ?? $post['epc_uae_buyer_name'] ?? ''));
	$trn = preg_replace('/\D/', '', (string)($post['epc_wholesale_trn'] ?? $post['epc_uae_trn'] ?? ''));
	$address = trim((string)($post['epc_wholesale_address'] ?? $post['epc_uae_address_line1'] ?? ''));
	$city = trim((string)($post['epc_wholesale_city'] ?? $post['epc_uae_city'] ?? ''));
	if ($buyerName === '' || $trn === '' || $address === '' || $city === '') {
		throw new Exception('Complete UAE company fields (legal name, TRN, address, city).');
	}
	if (strlen($trn) !== 15) {
		throw new Exception('UAE TRN must be 15 digits.');
	}
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
	$country = strtoupper(trim((string)($post[$prefix . 'country'] ?? $post['epc_reg_country'] ?? '')));
	$first = trim((string)($post[$prefix . 'first_name'] ?? ''));
	$last = trim((string)($post[$prefix . 'last_name'] ?? ''));
	$mobile = trim((string)($post[$prefix . 'mobile'] ?? ''));
	$city = trim((string)($post[$prefix . 'city'] ?? ''));
	$address = trim((string)($post[$prefix . 'address'] ?? ''));
	$smsKey = $type === 'wholesale' ? 'epc_wholesale_sms_notify' : 'epc_retail_sms_notify';

	epc_reg_profile_set($db_link, $user_id, 'epc_reg_country', $country);
	epc_reg_profile_set($db_link, $user_id, 'name', $first);
	epc_reg_profile_set($db_link, $user_id, 'surname', $last);
	if ($mobile !== '') {
		epc_reg_profile_set($db_link, $user_id, 'phone', $mobile);
	}
	epc_reg_profile_set($db_link, $user_id, 'epc_reg_city', $city);
	epc_reg_profile_set($db_link, $user_id, 'epc_reg_address', $address);
	epc_reg_profile_set($db_link, $user_id, 'epc_reg_sms_notify', !empty($post[$smsKey]) ? '1' : '0');
	$postal = trim((string)($post[$prefix . 'postal'] ?? ''));
	if ($postal !== '') {
		epc_reg_profile_set($db_link, $user_id, 'epc_reg_postal', $postal);
	}
	if ($country === 'AE' && $type === 'retail') {
		$em = trim((string)($post['epc_retail_emirate'] ?? ''));
		if ($em !== '') {
			epc_reg_profile_set($db_link, $user_id, 'epc_reg_emirate', $em);
			epc_reg_profile_set($db_link, $user_id, 'epc_reg_state', $em);
		}
	} else {
		$state = trim((string)($post['epc_retail_state'] ?? ''));
		if ($state !== '') {
			epc_reg_profile_set($db_link, $user_id, 'epc_reg_state', $state);
		}
	}

	if ($type === 'wholesale') {
		epc_reg_profile_set($db_link, $user_id, 'company_name', trim((string)($post['epc_wholesale_company'] ?? '')));
		epc_reg_profile_set($db_link, $user_id, 'epc_reg_legal_name', trim((string)($post['epc_wholesale_legal_name'] ?? '')));
		epc_reg_profile_set($db_link, $user_id, 'epc_reg_job_title', trim((string)($post['epc_wholesale_job_title'] ?? '')));
		epc_reg_profile_set($db_link, $user_id, 'epc_reg_business_type', trim((string)($post['epc_wholesale_business_type'] ?? '')));
		epc_reg_profile_set($db_link, $user_id, 'epc_reg_trade_licence', trim((string)($post['epc_wholesale_trade_licence'] ?? '')));
		$website = trim((string)($post['epc_wholesale_website'] ?? ''));
		if ($website !== '') {
			epc_reg_profile_set($db_link, $user_id, 'epc_reg_website', $website);
		}
		$trnMode = epc_reg_trn_mode($post);
		epc_reg_profile_set($db_link, $user_id, 'epc_reg_trn_mode', $country === 'AE' ? 'has_trn' : $trnMode);
		$trn = epc_reg_extract_trn($post);
		if ($trn !== '') {
			epc_reg_profile_set($db_link, $user_id, 'epc_reg_trn', $trn);
		}
		if ($country === 'AE') {
			epc_reg_profile_set($db_link, $user_id, 'epc_reg_emirate', trim((string)($post['epc_wholesale_emirate'] ?? 'Dubai')));
		}
		epc_reg_profile_set($db_link, $user_id, 'epc_legal_reg_type', 'TL');

		// KYC / AML text fields (also mirrored as reg_fields for approval checklist)
		foreach (array(
			'epc_emirates_id_no',
			'epc_authorized_signatory',
			'epc_authorized_signatory_id',
			'epc_ubo_name',
			'epc_pep_declaration',
			'epc_source_of_funds',
			'epc_passport_no',
			'epc_nationality',
			'epc_sanctions_declaration',
		) as $kycKey) {
			$val = trim((string) ($post[$kycKey] ?? ''));
			if ($val !== '') {
				epc_reg_profile_set($db_link, $user_id, $kycKey, $val);
			}
		}

		foreach (array(
			'epc_doc_trade_licence',
			'epc_doc_emirates_id',
			'epc_doc_vat_certificate',
			'epc_ubo_id_document',
			'epc_doc_passport',
			'epc_doc_power_of_attorney',
			'epc_doc_moa',
		) as $docKey) {
			if (empty($_FILES[$docKey]) || !is_array($_FILES[$docKey])) {
				continue;
			}
			$path = epc_reg_store_kyc_upload($user_id, $docKey, $_FILES[$docKey]);
			if ($path !== '') {
				epc_reg_profile_set($db_link, $user_id, $docKey, $path);
				epc_reg_profile_set($db_link, $user_id, $docKey . '_status', 'pending_review');
			}
		}
	}

	// Sync legacy reg_fields where present in POST
	foreach (array('name' => $first, 'surname' => $last, 'company_name' => trim((string)($post['epc_wholesale_company'] ?? ''))) as $legacyKey => $legacyVal) {
		if ($legacyVal !== '' && isset($post[$legacyKey])) {
			epc_reg_profile_set($db_link, $user_id, $legacyKey, $legacyVal);
		}
	}

	$taxFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit.php';
	if (is_readable($taxFile) && $db_link instanceof PDO) {
		require_once $taxFile;
		// Tenant kit applies globally — customer country at registration does not override tax jurisdiction.
	}

	$vatFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
	if (is_readable($vatFile) && $db_link instanceof PDO) {
		require_once $vatFile;
		epc_uae_customer_vat_sync($db_link, $user_id);
	}

	$einvoiceFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
	if (is_readable($einvoiceFile) && $db_link instanceof PDO && $country !== '') {
		require_once $einvoiceFile;
		if (function_exists('epc_einvoice_save_buyer_profile')) {
			$buyerName = trim($first . ' ' . $last);
			if ($type === 'wholesale') {
				$buyerName = trim((string)($post['epc_wholesale_legal_name'] ?? $post['epc_wholesale_company'] ?? $buyerName));
			}
			$trn = epc_reg_extract_trn($post);
			$legalRegNo = $type === 'wholesale'
				? trim((string)($post['epc_wholesale_trade_licence'] ?? $post['epc_reg_trade_licence'] ?? ''))
				: '';
			epc_einvoice_save_buyer_profile($db_link, array(
				'user_id' => $user_id,
				'buyer_name' => $buyerName !== '' ? $buyerName : ('Customer #' . $user_id),
				'trn' => $trn,
				'legal_reg_no' => $legalRegNo,
				'legal_reg_type' => $type === 'wholesale' ? 'TL' : 'EID',
				'address_line1' => $address,
				'city' => $city,
				'emirate' => trim((string)($post[$prefix . 'emirate'] ?? $post['epc_retail_emirate'] ?? 'Dubai')),
				'country_code' => $country,
				'email' => $reg_contact_type === 'email' ? trim($reg_contact) : '',
				'phone' => $mobile,
				'buyer_onboarded' => ($type === 'wholesale' && $country === 'AE' && strlen(preg_replace('/\D/', '', $trn)) === 15) ? 1 : 0,
			));
		}
	}
}

function epc_reg_save_uae_buyer_profile($db_link, int $user_id, array $post, string $reg_contact, string $reg_contact_type): void
{
	epc_reg_save_enhanced_profile($db_link, $user_id, $post, $reg_contact, $reg_contact_type);

	$customer_type = epc_reg_customer_type($post);
	if ($customer_type !== 'wholesale' || epc_reg_country_code($post) !== 'AE') {
		return;
	}
	$trn = preg_replace('/\D/', '', (string)($post['epc_wholesale_trn'] ?? ''));
	if (strlen($trn) !== 15) {
		return;
	}
	$buyerName = trim((string)($post['epc_wholesale_legal_name'] ?? ''));
	$address = trim((string)($post['epc_wholesale_address'] ?? ''));
	$city = trim((string)($post['epc_wholesale_city'] ?? ''));
	$emirate = trim((string)($post['epc_wholesale_emirate'] ?? 'Dubai'));
	$email = $reg_contact_type === 'email' ? trim($reg_contact) : '';
	$phone = trim((string)($post['epc_wholesale_mobile'] ?? ''));
	if ($phone === '' && $reg_contact_type === 'phone') {
		$phone = trim($reg_contact);
	}
	$einvoiceFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
	if (!is_readable($einvoiceFile)) {
		return;
	}
	require_once $einvoiceFile;
	if (!function_exists('epc_einvoice_save_buyer_profile')) {
		return;
	}
	$pdo = $db_link instanceof PDO ? $db_link : null;
	if (!$pdo instanceof PDO) {
		return;
	}
	epc_einvoice_save_buyer_profile($pdo, array(
		'user_id' => $user_id,
		'buyer_name' => $buyerName,
		'trn' => $trn,
		'legal_reg_no' => trim((string)($post['epc_wholesale_trade_licence'] ?? '')),
		'legal_reg_type' => 'TL',
		'address_line1' => $address,
		'city' => $city,
		'emirate' => $emirate !== '' ? $emirate : 'Dubai',
		'country_code' => 'AE',
		'email' => $email,
		'phone' => $phone,
		'buyer_onboarded' => 1,
	));
	$tradeFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
	if (is_readable($tradeFile)) {
		require_once $tradeFile;
		if (function_exists('epc_trade_profile_set')) {
			epc_trade_profile_set($db_link, $user_id, 'epc_uae_company', '1');
		}
	}
}
