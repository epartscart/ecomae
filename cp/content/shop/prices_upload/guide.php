<?php
/**
 * Wrapper for prices_upload_guide (used by ?view=guide on prices_manager).
 */
defined('_ASTEXE_') or die('No access');

if (!isset($user_session) || !is_array($user_session)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$user_session = DP_User::getAdminSession();
}

try {
	require __DIR__ . '/prices_upload_guide.php';
} catch (Exception $e) {
	echo '<div class="alert alert-danger"><strong>Guide could not load:</strong> '
		. htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
} catch (Throwable $e) {
	echo '<div class="alert alert-danger"><strong>Guide could not load:</strong> '
		. htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
}
