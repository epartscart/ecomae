<?php
/**
 * CP page stubs — never use `return` in content eval'd inside the template (it cuts off footer/scripts).
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_page_login_url(): string
{
	$backend = isset($GLOBALS['DP_Config']->backend_dir)
		? (string) $GLOBALS['DP_Config']->backend_dir
		: 'cp';
	return '/' . htmlspecialchars(trim($backend, '/'), ENT_QUOTES, 'UTF-8') . '/';
}

function epc_cp_page_require_admin(string $featureLabel): bool
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (DP_User::isAdmin()) {
		return true;
	}
	$login = epc_cp_page_login_url();
	echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to open '
		. htmlspecialchars($featureLabel, ENT_QUOTES, 'UTF-8') . '.</div>';
	return false;
}

function epc_cp_page_include(string $relativeMainPhp, string $missingMessage): void
{
	$backend = isset($GLOBALS['DP_Config']->backend_dir)
		? trim((string) $GLOBALS['DP_Config']->backend_dir, '/')
		: 'cp';
	$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/' . ltrim($relativeMainPhp, '/');
	if (!is_file($include)) {
		echo '<div class="alert alert-danger">' . htmlspecialchars($missingMessage, ENT_QUOTES, 'UTF-8') . '</div>';
		return;
	}
	include $include;
}
