<?php
/**
 * In-CP full Control Panel brochure (print from /cp).
 * Content URL: control/cp_brochure
 *
 * Prefer early include from cp/index.php (after auth). Also safe if CMS-eval'd:
 * clear buffers and exit with a full document.
 */
defined('_ASTEXE_') or die('No access');

@set_time_limit(120);
@ini_set('memory_limit', '512M');
while (ob_get_level() > 0) {
	@ob_end_clean();
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_full_brochure.php';

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$scope = preg_replace('/[^a-z]/', '', strtolower((string) ($_GET['scope'] ?? ($isSuper ? 'all' : 'client'))));
$print = isset($_GET['print']) && (string) $_GET['print'] === '1';
$brand = $isSuper ? 'ecomae' : 'epartscart';
$backend = 'cp';
if (isset($DP_Config) && is_object($DP_Config) && !empty($DP_Config->backend_dir)) {
	$backend = trim((string) $DP_Config->backend_dir, '/');
}
if ($backend === '') {
	$backend = 'cp';
}

// Render full document (replaces CP chrome) so Print/PDF is clean.
epc_cp_full_brochure_render_and_exit(array(
	'brand' => $brand,
	'scope' => $scope !== '' ? $scope : ($isSuper ? 'all' : 'client'),
	'print' => $print,
	'base_path' => '/' . $backend . '/control/cp_brochure',
));
