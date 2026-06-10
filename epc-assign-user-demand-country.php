<?php
/**
 * Assign demand country to a customer user (epc_user_demand_country + users_profiles).
 * Usage: ?user_id=123&country=SDN&token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/docpart/epc_demand_intelligence.php';

$token = isset($_REQUEST['token']) ? (string)$_REQUEST['token'] : '';
if ($token !== 'epartscart-deploy-2026') {
	echo "Forbidden\n";
	exit;
}

$user_id = isset($_REQUEST['user_id']) ? (int)$_REQUEST['user_id'] : 0;
$country = isset($_REQUEST['country']) ? trim((string)$_REQUEST['country']) : '';
$admin_all = isset($_REQUEST['admin_all']) && $_REQUEST['admin_all'] === '1';
if ($user_id <= 0) {
	echo "Usage: ?user_id=123&country=SDN&token=...\n";
	echo "   or: ?user_id=123&admin_all=1&token=... (all demand countries)\n";
	exit;
}

$cfg = new DP_Config();
$db = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

if ($admin_all) {
	$db->prepare('DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?')->execute(array($user_id, 'epc_demand_all_countries'));
	$db->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')->execute(array($user_id, 'epc_demand_all_countries', '1'));
	echo "User {$user_id} -> all demand countries (profile flag)\n";
	exit;
}

if ($country === '') {
	echo "Missing country= or use admin_all=1\n";
	exit;
}

$country = epc_demand_normalize_user_country_value($country);
if ($country === '') {
	echo "Invalid country code\n";
	exit;
}

epc_demand_ensure_schema($db);
$now = time();
$db->prepare(
	'INSERT INTO `epc_user_demand_country` (`user_id`, `country_code`, `updated_at`) VALUES (?, ?, ?)
	 ON DUPLICATE KEY UPDATE `country_code` = VALUES(`country_code`), `updated_at` = VALUES(`updated_at`)'
)->execute(array($user_id, $country, $now));

$db->prepare('DELETE FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?')->execute(array($user_id, 'epc_demand_country'));
$db->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')->execute(array($user_id, 'epc_demand_country', $country));

$registry = epc_demand_country_registry();
$name = isset($registry[$country]['name']) ? $registry[$country]['name'] : $country;
echo "User {$user_id} -> {$name} ({$country})\n";
