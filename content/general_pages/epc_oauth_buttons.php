<?php
/**
 * Reusable social sign-in buttons (Skywork.ai style).
 *
 * White rounded buttons, brand icon + "Continue with X". Only providers that
 * are actually configured (client id + secret) are rendered, so this gracefully
 * shows nothing when nothing is set up. The OTP modal / regform agent includes
 * this file and calls epc_oauth_buttons_render().
 *
 * Usage (PHP):
 *   require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_oauth_buttons.php';
 *   echo epc_oauth_buttons_render([
 *     'context'       => 'storefront',     // 'cp' | 'storefront'
 *     'tenant_key'    => '',
 *     'return_url'    => '/en/',
 *     'only'          => [],               // optional whitelist e.g. ['google','apple']
 *     'divider'       => true,             // show an "or" divider above the buttons
 *     'heading'       => '',               // optional small heading text
 *     'require_terms' => false,            // gate buttons behind a Terms checkbox
 *     'terms_url'     => '/en/terms',
 *     'privacy_url'   => '/en/privacy',
 *   ]);
 *
 * Returns '' (empty string) when no provider is configured — callers can echo
 * it unconditionally.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_oauth_providers.php';

function epc_oauth_buttons_styles_once(): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	echo '<style id="epc-oauth-buttons-styles">
.epc-social{margin:0;display:flex;flex-direction:column;gap:10px}
.epc-social-heading{font-size:13px;color:#6b7280;text-align:center;margin:0 0 2px}
.epc-social-divider{display:flex;align-items:center;text-align:center;color:#9ca3af;font-size:12px;margin:6px 0;gap:12px}
.epc-social-divider::before,.epc-social-divider::after{content:"";flex:1;height:1px;background:#e5e7eb}
.epc-social-btn{display:flex;align-items:center;justify-content:center;gap:10px;width:100%;
  padding:11px 14px;border:1px solid #dadce0;border-radius:10px;background:#fff;color:#3c4043;
  font-size:14px;font-weight:600;line-height:1;text-decoration:none;cursor:pointer;
  transition:background .15s,box-shadow .15s,border-color .15s;position:relative;box-sizing:border-box}
.epc-social-btn:hover{background:#f7f8fa;box-shadow:0 1px 4px rgba(0,0,0,.08);text-decoration:none;color:#3c4043}
.epc-social-btn:focus{outline:none;border-color:#2563eb;box-shadow:0 0 0 3px rgba(37,99,235,.15)}
.epc-social-btn[aria-disabled="true"]{opacity:.5;cursor:not-allowed;pointer-events:none}
.epc-social-btn svg{width:18px;height:18px;flex:0 0 18px;display:block}
.epc-social-btn .epc-social-label{flex:0 1 auto}
.epc-social-hint{font-size:11px;color:#6b7280;font-weight:400;margin-left:4px;white-space:nowrap;
  overflow:hidden;text-overflow:ellipsis;max-width:160px}
.epc-social-terms{display:flex;align-items:flex-start;gap:8px;font-size:12px;color:#6b7280;margin:2px 0 4px;line-height:1.45}
.epc-social-terms input{margin-top:2px}
.epc-social-terms a{color:#2563eb;text-decoration:underline}
@media(max-width:480px){.epc-social-btn{font-size:13px;padding:10px 12px}}
</style>';
}

/** Inline brand SVGs (no FontAwesome dependency). */
function epc_oauth_provider_icon_svg(string $provider): string
{
	switch ($provider) {
		case 'google':
			return '<svg viewBox="0 0 48 48" aria-hidden="true"><path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/><path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/><path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/><path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/></svg>';
		case 'microsoft':
			return '<svg viewBox="0 0 23 23" aria-hidden="true"><path fill="#f25022" d="M1 1h10v10H1z"/><path fill="#7fba00" d="M12 1h10v10H12z"/><path fill="#00a4ef" d="M1 12h10v10H1z"/><path fill="#ffb900" d="M12 12h10v10H12z"/></svg>';
		case 'facebook':
			return '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="#1877F2" d="M24 12.07C24 5.4 18.63 0 12 0S0 5.4 0 12.07c0 6.02 4.39 11.01 10.13 11.93v-8.44H7.08v-3.49h3.05V9.41c0-3.02 1.79-4.69 4.53-4.69 1.31 0 2.68.24 2.68.24v2.97h-1.51c-1.49 0-1.96.93-1.96 1.89v2.25h3.33l-.53 3.49h-2.8V24C19.61 23.08 24 18.09 24 12.07z"/></svg>';
		case 'github':
			return '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="#181717" d="M12 .5C5.37.5 0 5.87 0 12.5c0 5.3 3.44 9.8 8.21 11.39.6.11.82-.26.82-.58 0-.29-.01-1.04-.02-2.05-3.34.73-4.04-1.61-4.04-1.61-.55-1.39-1.34-1.76-1.34-1.76-1.09-.75.08-.73.08-.73 1.21.09 1.84 1.24 1.84 1.24 1.07 1.84 2.81 1.31 3.5 1 .11-.78.42-1.31.76-1.61-2.67-.3-5.47-1.33-5.47-5.93 0-1.31.47-2.38 1.24-3.22-.12-.31-.54-1.52.12-3.18 0 0 1.01-.32 3.3 1.23a11.5 11.5 0 0 1 6 0c2.29-1.55 3.3-1.23 3.3-1.23.66 1.66.24 2.87.12 3.18.77.84 1.23 1.91 1.23 3.22 0 4.61-2.81 5.62-5.49 5.92.43.37.81 1.1.81 2.22 0 1.61-.01 2.9-.01 3.29 0 .32.21.7.82.58A12 12 0 0 0 24 12.5C24 5.87 18.63.5 12 .5z"/></svg>';
		case 'apple':
			return '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="#000" d="M16.36 12.78c-.02-2.07 1.69-3.06 1.77-3.11-.96-1.41-2.46-1.6-3-1.62-1.27-.13-2.49.75-3.14.75-.65 0-1.65-.73-2.71-.71-1.39.02-2.68.81-3.4 2.06-1.45 2.51-.37 6.22 1.04 8.26.69 1 1.51 2.12 2.58 2.08 1.04-.04 1.43-.67 2.69-.67 1.25 0 1.61.67 2.71.65 1.12-.02 1.83-1.02 2.51-2.02.79-1.16 1.12-2.28 1.13-2.34-.02-.01-2.17-.83-2.19-3.31zM14.3 6.25c.58-.7.97-1.67.86-2.65-.83.03-1.84.55-2.44 1.25-.54.62-1.01 1.61-.88 2.56.92.07 1.87-.47 2.46-1.16z"/></svg>';
	}
	return '';
}

/**
 * @param  array<string,mixed> $cfg
 * @return string HTML (empty string when no provider configured)
 */
function epc_oauth_buttons_render(array $cfg = array()): string
{
	$enabled = epc_oauth_enabled_providers();

	$only = isset($cfg['only']) && is_array($cfg['only']) ? array_map('strval', $cfg['only']) : array();
	if ($only !== array()) {
		$enabled = array_values(array_intersect($enabled, $only));
	}
	if ($enabled === array()) {
		return '';
	}

	$context    = epc_auth_normalize_mode((string) ($cfg['context'] ?? 'storefront'));
	$tenantKey  = (string) ($cfg['tenant_key'] ?? '');
	$returnUrl  = (string) ($cfg['return_url'] ?? '');
	$divider    = !array_key_exists('divider', $cfg) ? true : !empty($cfg['divider']);
	$heading    = (string) ($cfg['heading'] ?? '');
	$reqTerms   = !empty($cfg['require_terms']);
	$termsUrl   = (string) ($cfg['terms_url'] ?? '/en/terms');
	$privacyUrl = (string) ($cfg['privacy_url'] ?? '');
	$uid        = 'epc_social_' . substr(md5($context . '|' . $tenantKey . '|' . microtime()), 0, 8);

	$lastGoogle = '';
	if (in_array('google', $enabled, true) && !empty($_COOKIE['epc_oauth_last_google_email'])) {
		$cookieEmail = strtolower(trim((string) $_COOKIE['epc_oauth_last_google_email']));
		if (filter_var($cookieEmail, FILTER_VALIDATE_EMAIL)) {
			$lastGoogle = $cookieEmail;
		}
	}

	$defs = epc_oauth_provider_defs();
	$h = function ($v) {
		return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
	};

	ob_start();
	epc_oauth_buttons_styles_once();
	?>
<div class="epc-social" id="<?php echo $h($uid); ?>">
<?php if ($divider): ?>
	<div class="epc-social-divider"><span>or continue with</span></div>
<?php endif; ?>
<?php if ($heading !== ''): ?>
	<p class="epc-social-heading"><?php echo $h($heading); ?></p>
<?php endif; ?>
<?php if ($reqTerms): ?>
	<label class="epc-social-terms">
		<input type="checkbox" class="epc-social-terms-cb" data-social-root="<?php echo $h($uid); ?>">
		<span>I agree to the <a href="<?php echo $h($termsUrl); ?>" target="_blank" rel="noopener">Terms</a><?php if ($privacyUrl !== ''): ?> and <a href="<?php echo $h($privacyUrl); ?>" target="_blank" rel="noopener">Privacy Policy</a><?php endif; ?>.</span>
	</label>
<?php endif; ?>
<?php
	foreach ($enabled as $pid) {
		$def = $defs[$pid] ?? array();
		$label = (string) ($def['label'] ?? ucfirst($pid));
		$icon = epc_oauth_provider_icon_svg($pid);
		$startParams = array(
			'provider' => $pid,
			'context' => $context,
			'tenant_key' => $tenantKey,
		);
		if ($returnUrl !== '') {
			$startParams['return_url'] = $returnUrl;
		}
		$href = '/api/epc_oauth_start.php?' . http_build_query($startParams);
		$hint = ($pid === 'google' && $lastGoogle !== '')
			? '<span class="epc-social-hint">' . $h($lastGoogle) . '</span>'
			: '';
		?>
	<a class="epc-social-btn epc-social-btn--<?php echo $h($pid); ?>" href="<?php echo $h($href); ?>"
	   data-provider="<?php echo $h($pid); ?>"<?php echo $reqTerms ? ' aria-disabled="true" data-href="' . $h($href) . '"' : ''; ?>>
		<?php echo $icon; ?>
		<span class="epc-social-label">Continue with <?php echo $h($label); ?></span>
		<?php echo $hint; ?>
	</a>
<?php } ?>
</div>
<?php if ($reqTerms): ?>
<script>
(function(){
	var root=document.getElementById(<?php echo json_encode($uid); ?>);
	if(!root)return;
	var cb=root.querySelector('.epc-social-terms-cb');
	var btns=root.querySelectorAll('.epc-social-btn');
	function sync(){
		var ok=cb&&cb.checked;
		btns.forEach(function(b){
			if(ok){b.removeAttribute('aria-disabled');b.setAttribute('href',b.getAttribute('data-href')||b.getAttribute('href'));}
			else{b.setAttribute('aria-disabled','true');}
		});
	}
	if(cb){cb.addEventListener('change',sync);sync();}
	btns.forEach(function(b){
		b.addEventListener('click',function(e){
			if(b.getAttribute('aria-disabled')==='true'){e.preventDefault();if(cb){cb.focus();}}
		});
	});
})();
</script>
<?php endif; ?>
<?php
	return (string) ob_get_clean();
}
