<?php
/**
 * AJAX: temporary storefront ON/OFF for warehouse / price list.
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_prices_ajax_init.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php';

$pages_to_check = array();
$pages_to_check[] = array('url' => 'shop/prices', 'is_frontend' => 0);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/check_user_access.php';

$action = (string) ($_POST['action'] ?? '');
if ($action !== 'toggle') {
	exit(json_encode(array('ok' => false, 'message' => 'Unknown action')));
}

$entityType = (string) ($_POST['entity_type'] ?? 'storage');
$entityId = (int) ($_POST['entity_id'] ?? 0);
$storefrontEnabled = !empty($_POST['storefront_enabled']) && (string) $_POST['storefront_enabled'] !== '0';
$disabled = $storefrontEnabled ? 0 : 1;

$userId = (int) DP_User::getUserId();
$userLabel = 'admin';
try {
	$profile = DP_User::getUserProfile();
	if (!empty($profile['email'])) {
		$userLabel = (string) $profile['email'];
	} elseif (!empty($profile['name'])) {
		$userLabel = (string) $profile['name'];
	} elseif ($userId > 0) {
		$userLabel = 'user#' . $userId;
	}
} catch (Throwable $e) {
	// keep default
}

$result = epc_ssf_set_toggle($db_link, $entityType, $entityId, $disabled, $userId, $userLabel);
exit(json_encode($result, JSON_UNESCAPED_UNICODE));
