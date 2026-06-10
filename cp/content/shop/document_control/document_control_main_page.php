<?php
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

if (empty($user_session) || !is_array($user_session)) {
	$login = '/' . htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';
	echo '<div class="alert alert-warning">Please <a href="' . $login . '">log in to the control panel</a> to open Document Control.</div>';
	return;
}

$backend = trim((string) $GLOBALS['DP_Config']->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}
$candidates = array(
	$_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/document_control/document_control_main.php',
	$_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/document_control/document_control_main.php',
);
$include = '';
foreach ($candidates as $path) {
	if (is_file($path)) {
		$include = $path;
		break;
	}
}

if ($include === '') {
	echo '<div class="alert alert-danger"><strong>Document Control module not found.</strong> Deploy CP files and run <code>epc-document-control-cp-setup.php?token=…</code> or <code>epc-document-control-cp-setup-all.php?apply=1</code>.</div>';
	return;
}

include $include;
