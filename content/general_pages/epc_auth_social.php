<?php
/**
 * CP social login — Google OAuth 2.0 + provider hooks.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_auth_common.php';

/**
 * Extensible social providers (Microsoft / Facebook / Apple later).
 *
 * @return array<string,array<string,mixed>>
 */
function epc_auth_social_providers(): array
{
	$oauth = epc_auth_oauth_config();
	$providers = array();
	$google = $oauth['google'] ?? array();
	if (trim((string) ($google['client_id'] ?? '')) !== '') {
		$providers['google'] = array(
			'id' => 'google',
			'label' => 'Continue with Google',
			'icon' => 'fa-google',
			'enabled' => trim((string) ($google['client_secret'] ?? '')) !== '',
			'start_url' => '/epc-auth-google-start.php',
		);
	} else {
		$providers['google'] = array(
			'id' => 'google',
			'label' => 'Continue with Google',
			'icon' => 'fa-google',
			'enabled' => false,
			'start_url' => '',
		);
	}
	return $providers;
}

function epc_auth_oauth_central_callback_url(): string
{
	$oauth = epc_auth_oauth_config();
	$uri = trim((string) (($oauth['google']['redirect_uri'] ?? '') ?: 'https://www.ecomae.com/epc-auth-google-callback.php'));
	return $uri;
}

function epc_auth_oauth_state_pack(array $context, string $nonce): string
{
	$payload = array(
		'n' => $nonce,
		'tk' => (string) ($context['tenant_key'] ?? ''),
		'k' => (string) ($context['kind'] ?? ''),
		'rh' => (string) ($context['return_host'] ?? ''),
		'rp' => (string) ($context['return_path'] ?? '/cp/'),
		'am' => epc_auth_normalize_mode((string) ($context['auth_mode'] ?? 'cp')),
		'lp' => (string) ($context['lang_prefix'] ?? ''),
		't' => time(),
	);
	$json = json_encode($payload);
	$p = rtrim(strtr(base64_encode($json), '+/', '-_'), '=');
	$sig = hash_hmac('sha256', $p, epc_auth_signing_secret());
	return $p . '.' . $sig;
}

function epc_auth_oauth_state_unpack(string $state): ?array
{
	$parts = explode('.', $state, 2);
	if (count($parts) !== 2) {
		return null;
	}
	$expected = hash_hmac('sha256', $parts[0], epc_auth_signing_secret());
	if (!hash_equals($expected, $parts[1])) {
		return null;
	}
	$json = base64_decode(strtr($parts[0], '-_', '+/') . str_repeat('=', (4 - strlen($parts[0]) % 4) % 4));
	$data = json_decode((string) $json, true);
	if (!is_array($data) || empty($data['n']) || empty($data['t'])) {
		return null;
	}
	if (time() - (int) $data['t'] > 900) {
		return null;
	}
	return $data;
}

function epc_auth_google_start_url(array $context): string
{
	$oauth = epc_auth_oauth_config();
	$google = $oauth['google'] ?? array();
	$clientId = trim((string) ($google['client_id'] ?? ''));
	if ($clientId === '') {
		return '';
	}
	$nonce = bin2hex(random_bytes(16));
	$state = epc_auth_oauth_state_pack($context, $nonce);
	$params = array(
		'client_id' => $clientId,
		'redirect_uri' => epc_auth_oauth_central_callback_url(),
		'response_type' => 'code',
		'scope' => 'openid email profile',
		'state' => $state,
		'nonce' => $nonce,
		'prompt' => 'select_account',
		'access_type' => 'online',
	);
	return 'https://accounts.google.com/o/oauth2/v2/auth?' . http_build_query($params);
}

function epc_auth_google_exchange_code(string $code): array
{
	$oauth = epc_auth_oauth_config();
	$google = $oauth['google'] ?? array();
	$clientId = trim((string) ($google['client_id'] ?? ''));
	$clientSecret = trim((string) ($google['client_secret'] ?? ''));
	if ($clientId === '' || $clientSecret === '' || $code === '') {
		return array('ok' => false, 'message' => 'Google OAuth not configured');
	}
	$body = http_build_query(array(
		'code' => $code,
		'client_id' => $clientId,
		'client_secret' => $clientSecret,
		'redirect_uri' => epc_auth_oauth_central_callback_url(),
		'grant_type' => 'authorization_code',
	));
	$ch = curl_init('https://oauth2.googleapis.com/token');
	curl_setopt_array($ch, array(
		CURLOPT_POST => true,
		CURLOPT_POSTFIELDS => $body,
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_TIMEOUT => 30,
		CURLOPT_HTTPHEADER => array('Content-Type: application/x-www-form-urlencoded'),
	));
	$raw = curl_exec($ch);
	$err = curl_error($ch);
	curl_close($ch);
	if ($raw === false) {
		return array('ok' => false, 'message' => 'Token exchange failed: ' . $err);
	}
	$data = json_decode((string) $raw, true);
	if (!is_array($data) || empty($data['id_token'])) {
		return array('ok' => false, 'message' => 'Invalid token response from Google');
	}
	$profile = epc_auth_google_verify_id_token((string) $data['id_token'], $clientId);
	if (empty($profile['ok'])) {
		return $profile;
	}
	return array('ok' => true, 'profile' => $profile);
}

function epc_auth_google_verify_id_token(string $idToken, string $clientId): array
{
	$parts = explode('.', $idToken);
	if (count($parts) < 2) {
		return array('ok' => false, 'message' => 'Malformed id_token');
	}
	$payloadJson = base64_decode(strtr($parts[1], '-_', '+/') . str_repeat('=', (4 - strlen($parts[1]) % 4) % 4));
	$payload = json_decode((string) $payloadJson, true);
	if (!is_array($payload)) {
		return array('ok' => false, 'message' => 'Invalid id_token payload');
	}
	$aud = (string) ($payload['aud'] ?? '');
	$email = strtolower(trim((string) ($payload['email'] ?? '')));
	$verified = !empty($payload['email_verified']);
	$iss = (string) ($payload['iss'] ?? '');
	if ($aud !== $clientId) {
		return array('ok' => false, 'message' => 'id_token audience mismatch');
	}
	if ($email === '' || !$verified) {
		return array('ok' => false, 'message' => 'Google account email not verified');
	}
	if ($iss !== 'https://accounts.google.com' && $iss !== 'accounts.google.com') {
		return array('ok' => false, 'message' => 'id_token issuer invalid');
	}
	if (!empty($payload['exp']) && (int) $payload['exp'] < time()) {
		return array('ok' => false, 'message' => 'id_token expired');
	}
	return array(
		'ok' => true,
		'email' => $email,
		'name' => trim((string) ($payload['name'] ?? '')),
		'sub' => (string) ($payload['sub'] ?? ''),
	);
}

function epc_auth_google_complete_login(array $stateData, array $profile): array
{
	$authMode = epc_auth_normalize_mode((string) ($stateData['am'] ?? 'cp'));
	$hints = array('tenant_key' => (string) ($stateData['tk'] ?? ''));
	$ctx = epc_auth_resolve_for_mode($authMode, $hints);
	if (empty($ctx['ok'])) {
		if ($authMode === 'cp' && (string) ($stateData['tk'] ?? '') !== '') {
			$ctx = epc_auth_context_from_registry_key((string) $stateData['tk'], (string) ($stateData['k'] ?? ''));
		}
		if (empty($ctx['ok']) && (string) ($stateData['tk'] ?? '') !== '') {
			$ctx = epc_auth_storefront_from_registry_key((string) $stateData['tk'], (string) ($stateData['k'] ?? ''));
		}
	}
	if (empty($ctx['ok'])) {
		return array('ok' => false, 'message' => (string) ($ctx['message'] ?? 'Tenant context lost'));
	}
	$ctx['auth_mode'] = $authMode;
	if (!empty($stateData['rh'])) {
		$ctx['return_host'] = (string) $stateData['rh'];
	}
	if (!empty($stateData['rp'])) {
		$ctx['return_path'] = (string) $stateData['rp'];
	}
	if (!empty($stateData['lp'])) {
		$ctx['lang_prefix'] = (string) $stateData['lp'];
	}

	$email = (string) ($profile['email'] ?? '');
	$name = (string) ($profile['name'] ?? '');
	if ($authMode === 'storefront') {
		$userId = epc_auth_find_or_provision_storefront_customer($ctx, $email, $name);
		if ($userId <= 0) {
			return array('ok' => false, 'message' => 'Could not sign in with this Google account');
		}
	} else {
		$userId = epc_auth_find_or_provision_cp_user($ctx, $email, $name);
		if ($userId <= 0) {
			return array('ok' => false, 'message' => 'No CP access for this Google account on this workspace');
		}
	}
	$finish = epc_auth_finish_login($ctx, $userId, 'email');
	if (empty($finish['ok'])) {
		return array('ok' => false, 'message' => (string) ($finish['message'] ?? 'Could not create session'));
	}
	return array('ok' => true, 'redirect' => (string) ($finish['redirect'] ?? epc_auth_post_login_redirect($ctx)));
}

function epc_cp_login_modern_auth_html(array $ui): string
{
	// Deduplicate: if already rendered on this request, skip (template.php calls this twice)
	static $callCount = 0;
	$callCount++;
	if ($callCount > 1) {
		return '';
	}

	$tenantKey  = (string) ($ui['tenant_key'] ?? '');
	$sendUrl    = (string) ($ui['send_code_url'] ?? '/epc-auth-send-code.php');
	$verifyUrl  = (string) ($ui['verify_code_url'] ?? '/epc-auth-verify-code.php');
	$providers  = epc_auth_social_providers();
	$google     = $providers['google'] ?? array();
	$googleConfigured = !empty($google['enabled']);
	$policy     = epc_cp_modern_auth_policy();
	$passwordOn = !empty($policy['password']);
	$emailOtpOn = !empty($policy['email_otp']);
	$googleOn   = !empty($policy['google_oauth']) && $googleConfigured;
	$authContext = epc_auth_normalize_mode((string) ($ui['context'] ?? 'cp'));
	$label      = (string) ($ui['login_label'] ?? 'Control Panel');
	$googleUrl  = $googleOn
		? htmlspecialchars(
			'/epc-auth-google-start.php?context=' . rawurlencode($authContext)
				. '&tenant_key=' . rawurlencode($tenantKey),
			ENT_QUOTES, 'UTF-8'
		)
		: '';

	// Ensure modal CSS + component is available
	$modalPhp = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_otp_modal.php';
	if (is_file($modalPhp)) {
		require_once $modalPhp;
	}

	// Skywork-style multi-provider social buttons (renders configured providers
	// only; empty string when none, so the "Or" divider below stays hidden too).
	$socialButtons = '';
	$buttonsPhp = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_oauth_buttons.php';
	if (is_file($buttonsPhp)) {
		require_once $buttonsPhp;
		$socialButtons = epc_oauth_buttons_render(array(
			'context'    => $authContext,
			'tenant_key' => $tenantKey,
			'return_url' => '/cp/',
			'divider'    => false,
		));
	}

	ob_start();

	$tkEsc  = htmlspecialchars($tenantKey, ENT_QUOTES, 'UTF-8');
	$ctxEsc = htmlspecialchars($authContext, ENT_QUOTES, 'UTF-8');
	$defaultTab = $passwordOn ? 'password' : ($emailOtpOn ? 'email_code' : 'password');
	?>
<div class="epc-cp-auth-modern" id="epc_cp_auth_modern" data-tenant-key="<?php echo $tkEsc; ?>" data-auth-context="<?php echo $ctxEsc; ?>">
<p class="epc-cp-auth-title">Sign in to <?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></p>
<?php if ($socialButtons !== ''): ?>
<?php echo $socialButtons; ?>
<div class="epc-social-divider"><span>Or</span></div>
<?php endif; ?>
<?php if ($passwordOn || $emailOtpOn): ?>
<div class="epc-cp-auth-tabs" role="tablist">
<?php if ($passwordOn): ?>
  <button type="button" class="epc-cp-auth-tab is-active" data-tab="password" role="tab">Password</button>
<?php endif; ?>
<?php if ($emailOtpOn): ?>
  <button type="button" class="epc-cp-auth-tab<?php echo $passwordOn ? '' : ' is-active'; ?>" data-tab="email_code" role="tab">Email code</button>
<?php endif; ?>
</div>
<?php endif; ?>

<?php if ($passwordOn): ?>
<div class="epc-cp-auth-pane is-active" data-pane="password">
  <p class="epc-cp-auth-hint">Sign in with your CP password below.</p>
</div>
<?php endif; ?>

<?php if ($emailOtpOn): ?>
<div class="epc-cp-auth-pane<?php echo $passwordOn ? '' : ' is-active'; ?>" data-pane="email_code">
  <div class="form-group">
    <label class="control-label" for="epc_auth_email">Email</label>
    <input type="email" class="form-control" id="epc_auth_email" autocomplete="email" placeholder="you@company.com" />
  </div>
  <button type="button" class="btn btn-block epc-cp-auth-continue-email" id="epc_auth_send_code">Continue with Email</button>
  <p class="epc-cp-auth-msg" id="epc_auth_otp_msg" aria-live="polite"></p>
</div>
<?php endif; ?>
</div>

<?php
	// Render the Skywork-style 6-box OTP modal (only if email OTP is enabled)
	if ($emailOtpOn && function_exists('epc_otp_modal_render')) {
		echo epc_otp_modal_render(array(
			'modal_id'   => 'epc_cp_otp_modal',
			'context'    => $authContext,
			'tenant_key' => $tenantKey,
			'send_url'   => $sendUrl,
			'verify_url' => $verifyUrl,
			'label'      => $label,
			'on_success' => 'if(data.redirect){location.href=data.redirect;}',
		));
	}
?>
<script>
(function(){
var root=document.getElementById('epc_cp_auth_modern');
if(!root)return;
var authContext=root.getAttribute('data-auth-context')||'cp';
var defaultTab=<?php echo json_encode($defaultTab); ?>;

// Tab switching
root.querySelectorAll('.epc-cp-auth-tab').forEach(function(btn){
  btn.addEventListener('click',function(){
    var t=btn.getAttribute('data-tab');
    root.querySelectorAll('.epc-cp-auth-tab').forEach(function(b){b.classList.toggle('is-active',b===btn);});
    root.querySelectorAll('.epc-cp-auth-pane').forEach(function(p){p.classList.toggle('is-active',p.getAttribute('data-pane')===t);});
    var lf=document.getElementById('login_form');
    if(lf)lf.style.display=(t==='password')?'':'none';
  });
});
var lf=document.getElementById('login_form');
if(lf&&defaultTab!=='password')lf.style.display='none';

<?php if ($emailOtpOn): ?>
// Send code → open modal
var sendBtn=document.getElementById('epc_auth_send_code');
var emailIn=document.getElementById('epc_auth_email');
var msgEl=document.getElementById('epc_auth_otp_msg');
function showOtpMsg(t,ok){if(msgEl){msgEl.textContent=t;msgEl.className='epc-cp-auth-msg'+(ok?' is-ok':' is-err');}}
if(sendBtn){
  sendBtn.addEventListener('click',function(){
    var em=(emailIn||{}).value||'';
    em=em.trim();
    if(!em||!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em)){
      showOtpMsg('Please enter a valid email address.',false);return;
    }
    showOtpMsg('',true);
    if(window.EpcOtpModal&&window.EpcOtpModal['epc_cp_otp_modal']){
      window.EpcOtpModal['epc_cp_otp_modal'].open(em);
    }
  });
}
if(emailIn){
  emailIn.addEventListener('keydown',function(e){if(e.key==='Enter'){e.preventDefault();if(sendBtn)sendBtn.click();}});
}
<?php endif; ?>
})();
</script>
<?php
	return (string) ob_get_clean();
}
