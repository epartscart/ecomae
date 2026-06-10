<?php
/**
 * Demo hub tab verification — source markers for password + CP deep link (no CP login required).
 * GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$manage = __DIR__ . '/cp/content/control/portal/epc_demo_tenants_manage.php';
$hubCandidates = array(
	__DIR__ . '/cp/content/shop/tenant_hub/tenant_hub_main.php',
	__DIR__ . '/cp/content/shop/tenant_hub/tenant_hub_hub_page.php',
);
$hub = '';
$hubSrc = '';
foreach ($hubCandidates as $candidate) {
	if (is_file($candidate)) {
		$src = (string) file_get_contents($candidate);
		if (stripos($src, 'tab=demos') !== false || stripos($src, 'epc_demo_tenants_manage.php') !== false) {
			$hub = $candidate;
			$hubSrc = $src;
			break;
		}
		if ($hubSrc === '' || strlen($src) > strlen($hubSrc)) {
			$hub = $candidate;
			$hubSrc = $src;
		}
	}
}
$manageSrc = is_file($manage) ? (string) file_get_contents($manage) : '';

$checks = array(
	'manage_file_bytes' => strlen($manageSrc),
	'hub_file_bytes' => strlen($hubSrc),
	'hub_file' => $hub !== '' ? basename($hub) : null,
	'hub_has_demos_tab' => stripos($hubSrc, 'tab=demos') !== false,
	'hub_includes_manage' => stripos($hubSrc, 'epc_demo_tenants_manage.php') !== false,
	'manage_has_password_column' => stripos($manageSrc, 'Pass:</strong>') !== false || stripos($manageSrc, 'epc-demo-pwd-plain') !== false,
	'manage_has_username_column' => stripos($manageSrc, 'User:</strong>') !== false || stripos($manageSrc, 'admin_email') !== false,
	'manage_has_cp_login_url' => stripos($manageSrc, 'CP login') !== false || stripos($manageSrc, 'cpScopedUrl') !== false,
	'manage_has_stored_password' => stripos($manageSrc, 'stored_password') !== false,
	'manage_has_cp_autologin' => stripos($manageSrc, 'cp_autologin') !== false || stripos($manageSrc, 'cp_login') !== false,
);

$ok = $checks['hub_has_demos_tab']
	&& $checks['hub_includes_manage']
	&& $checks['manage_has_password_column']
	&& $checks['manage_has_cp_login_url']
	&& $checks['manage_has_stored_password'];

echo json_encode(array(
	'ok' => $ok,
	'checks' => $checks,
	'urls' => array(
		'hub' => 'https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=demos',
		'manage' => 'https://www.ecomae.com/cp/control/portal/epc_demo_tenants_manage',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
