<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_social.php';

$ui = epc_auth_login_context_for_ui('storefront');
$providers = epc_auth_social_providers();
$google = $providers['google'] ?? array();
$googleEnabled = !empty($google['enabled']);
$returnUrl = rawurlencode((string) ($multilang_params['lang_href'] ?? '/en/') . '/');
$googleUrl = $googleEnabled
	? htmlspecialchars(
		(string) ($ui['google_start_url'] ?? '/epc-auth-google-start.php')
			. '?context=storefront&tenant_key=' . rawurlencode((string) ($ui['tenant_key'] ?? ''))
			. '&return_url=' . $returnUrl,
		ENT_QUOTES,
		'UTF-8'
	)
	: '';
?>
<div class="epc-cp-auth-modern epc-storefront-auth">
	<?php if ($googleEnabled && $googleUrl !== '') { ?>
	<a class="btn btn-ar btn-block epc-cp-auth-google" href="<?php echo $googleUrl; ?>"><i class="fa fa-google"></i> Continue with Google</a>
	<?php } else { ?>
	<button type="button" class="btn btn-ar btn-block epc-cp-auth-google is-disabled" disabled><i class="fa fa-google"></i> Continue with Google</button>
	<?php } ?>
	<button type="button" class="btn btn-ar btn-block epc-cp-auth-google is-disabled" disabled title="Coming soon"><i class="fa fa-windows"></i> Microsoft (soon)</button>
	<button type="button" class="btn btn-ar btn-block epc-cp-auth-google is-disabled" disabled title="Coming soon"><i class="fa fa-facebook"></i> Facebook (soon)</button>
	<p class="epc-cp-auth-hint">First sign-in creates your customer account automatically.</p>
</div>
