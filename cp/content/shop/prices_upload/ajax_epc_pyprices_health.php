<?php
/**
 * Non-blocking pyprices DB health check for prices manager modal.
 */
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_prices_ajax_init.php';
require_once __DIR__ . '/epc_prices_manager_perf.php';

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$pages_to_check = array();
$pages_to_check[] = array('url' => 'shop/prices', 'is_frontend' => 0);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/check_user_access.php';

$check = epc_pyprices_health_check($DP_Config, 5);

$backend = $DP_Config->backend_dir;
$cronUrl = $DP_Config->domain_path . $backend
	. '/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key=' . urlencode($DP_Config->tech_key);

if ($check['ok']) {
	$html = '<p style="color:#000;padding:5px;"><i class="fas fa-check-circle" style="color:#74d348"></i> '
		. htmlspecialchars(translate_str_by_id(5412), ENT_QUOTES, 'UTF-8') . '</p>'
		. '<p style="color:#000;padding:5px;">'
		. '<a class="btn btn-info btn-sm" href="/' . htmlspecialchars($backend, ENT_QUOTES, 'UTF-8')
		. '/shop/prices/guide"><i class="fa fa-book"></i> Full upload guide &amp; channel status</a></p>'
		. '<p style="color:#000;padding:5px;"><i class="fas fa-info-circle" style="color:#3498db"></i> '
		. htmlspecialchars(translate_str_by_id(5413), ENT_QUOTES, 'UTF-8') . '<br>'
		. htmlspecialchars(translate_str_by_id(5414), ENT_QUOTES, 'UTF-8') . '<br>'
		. htmlspecialchars(translate_str_by_id(5415), ENT_QUOTES, 'UTF-8') . '<br>'
		. htmlspecialchars(translate_str_by_id(5416), ENT_QUOTES, 'UTF-8')
		. '<ul><li style="color:#000;">' . htmlspecialchars(translate_str_by_id(5417), ENT_QUOTES, 'UTF-8') . '</li>'
		. '<li style="color:#000;">' . htmlspecialchars(translate_str_by_id(5418), ENT_QUOTES, 'UTF-8') . '</li>'
		. '<li style="color:#000;">' . htmlspecialchars(translate_str_by_id(5419), ENT_QUOTES, 'UTF-8')
		. '<br><strong>wget -O /dev/null -q \'' . htmlspecialchars($cronUrl, ENT_QUOTES, 'UTF-8') . '\'</strong></li></ul></p>';
	exit(json_encode(array(
		'status' => true,
		'critical' => false,
		'html' => $html,
	), JSON_UNESCAPED_UNICODE));
}

$html = '<p style="background-color:#e74c3c;color:#FFF;padding:5px;"><i class="fas fa-exclamation-triangle"></i> <strong>'
	. htmlspecialchars(translate_str_by_id(2853), ENT_QUOTES, 'UTF-8') . '</strong> '
	. htmlspecialchars(translate_str_by_id(5497), ENT_QUOTES, 'UTF-8') . '</p>'
	. '<p style="padding:5px;">' . htmlspecialchars(translate_str_by_id(5498), ENT_QUOTES, 'UTF-8') . '</p>'
	. '<button class="btn btn-success" type="button" onclick="pyprices_deploy();"><i class="fa fa-wrench"></i> <span class="bold">'
	. htmlspecialchars(translate_str_by_id(5499), ENT_QUOTES, 'UTF-8') . '</span></button>';

exit(json_encode(array(
	'status' => false,
	'critical' => true,
	'message' => $check['message'] ?? 'pyprices unavailable',
	'html' => $html,
), JSON_UNESCAPED_UNICODE));
