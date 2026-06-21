<?php
/**
 * Shared bootstrap for prices-upload AJAX endpoints on multi-tenant hosts.
 * Ensures tenant DB (e.g. docpart for epartscart) and admin CSRF context.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$db_link->query('SET NAMES utf8');
} catch (PDOException $e) {
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('status' => false, 'message' => 'No DB Connect')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();

$csrf_check_admin = true;
