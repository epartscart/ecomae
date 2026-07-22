<?php
/**
 * Storefront email OTP login widget — renders an email input + "Send code" button.
 * Clicking "Send code" opens the Skywork-style 6-box OTP modal overlay.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_social.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_otp_modal.php';

$ui        = epc_auth_login_context_for_ui('storefront');
$uid       = 'epc_sf_' . preg_replace('/[^a-z0-9_]/', '_', (string) $login_form_postfix);
$modalId   = $uid . '_otpm';
$tenantKey = (string) ($ui['tenant_key'] ?? '');
$sendUrl   = (string) ($ui['send_code_url'] ?? '/content/general_pages/epc_auth_api_send_code.php');
$verifyUrl = (string) ($ui['verify_code_url'] ?? '/content/general_pages/epc_auth_api_verify_code.php');
$returnUrl = rtrim((string) ($multilang_params['lang_href'] ?? '/en'), '/') . '/';

// Detect logo for branding in modal
$logoUrl = '';
if (function_exists('epc_portal_site_profile')) {
	$profile = epc_portal_site_profile();
	$logoUrl = (string) ($profile['logo_url'] ?? '');
}
$label = htmlspecialchars((string) ($ui['login_label'] ?? 'Shop'), ENT_QUOTES, 'UTF-8');
?>
<div class="epc-cp-auth-modern epc-storefront-auth" id="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>">
	<p class="epc-cp-auth-hint">Enter your email — we&rsquo;ll send a 6-digit code. New customers are registered automatically.</p>
	<div class="form-group">
		<input type="email" class="form-control" id="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>_email"
			autocomplete="email" placeholder="your@email.com" />
	</div>
	<button type="button" class="btn btn-ar btn-block epc-continue-email" id="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>_send">
		Continue with Email
	</button>
	<style>.epc-continue-email{background:#111827;border-color:#111827;color:#fff;font-weight:600}.epc-continue-email:hover,.epc-continue-email:focus{background:#000;border-color:#000;color:#fff}</style>
	<p class="epc-cp-auth-msg" id="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>_msg" aria-live="polite"></p>
</div>

<?php
// Render the Skywork-style 6-box modal
echo epc_otp_modal_render(array(
	'modal_id'   => $modalId,
	'context'    => 'storefront',
	'tenant_key' => $tenantKey,
	'send_url'   => $sendUrl,
	'verify_url' => $verifyUrl,
	'return_url' => $returnUrl,
	'logo_url'   => $logoUrl,
	'label'      => $label,
	'on_success' => 'if(data.redirect){location.href=data.redirect;}',
));
?>
<script>
(function(){
var uid=<?php echo json_encode($uid); ?>;
var modalId=<?php echo json_encode($modalId); ?>;
var sendBtn=document.getElementById(uid+'_send');
var emailIn=document.getElementById(uid+'_email');
var msgEl=document.getElementById(uid+'_msg');

function showMsg(t,ok){
	if(msgEl){msgEl.textContent=t;msgEl.className='epc-cp-auth-msg'+(ok?' is-ok':' is-err');}
}

if(sendBtn){
	sendBtn.addEventListener('click',function(){
		var email=(emailIn||{}).value||'';
		email=email.trim();
		if(!email||!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)){
			showMsg('Please enter a valid email address.',false);
			return;
		}
		showMsg('',true);
		if(window.EpcOtpModal&&window.EpcOtpModal[modalId]){
			window.EpcOtpModal[modalId].open(email);
		}
	});
}
if(emailIn){
	emailIn.addEventListener('keydown',function(e){
		if(e.key==='Enter'){e.preventDefault();if(sendBtn)sendBtn.click();}
	});
}
})();
</script>
