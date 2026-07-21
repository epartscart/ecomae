<?php
/**
 * Dedicated storefront login page (/users/login).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_auth_links.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_storefront_auth_layout.php';

$user_session = DP_User::getUserSession();
$langHref = (string) ($multilang_params['lang_href'] ?? '/en/');

if ((int) DP_User::getUserId() > 0) {
	echo '<div class="alert alert-info">You are already signed in. <a href="' . htmlspecialchars(rtrim($langHref, '/') . '/users/profile', ENT_QUOTES, 'UTF-8') . '">Go to profile</a></div>';
	return;
}

$signupUrl = epc_storefront_auth_signup_url($multilang_params);
epc_storefront_auth_layout_open();
?>
<div class="panel panel-primary epc-login-page">
	<div class="panel-heading"><?php echo htmlspecialchars(translate_str_by_id(4008) ?: 'Login', ENT_QUOTES, 'UTF-8'); ?></div>
	<div class="panel-body">
		<p class="help-block">Sign in to browse, track orders, and checkout. New customer? <a href="<?php echo htmlspecialchars($signupUrl, ENT_QUOTES, 'UTF-8'); ?>"><strong>Sign up</strong></a> — retail accounts are approved instantly and can see prices; wholesale requires manager approval before prices and stock details unlock.</p>
		<?php
		$login_form_postfix = 'login_page';
		$login_form_target = rtrim($langHref, '/') . '/';
		require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
		?>
	</div>
</div>
<?php
epc_storefront_auth_layout_close();
echo epc_storefront_auth_links_styles();
?>
