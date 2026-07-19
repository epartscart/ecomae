<?php
/**
 * In-CP full Control Panel brochure (print from /cp).
 * Content URL: control/cp_brochure
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_full_brochure.php';

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$scope = preg_replace('/[^a-z]/', '', strtolower((string) ($_GET['scope'] ?? ($isSuper ? 'all' : 'client'))));
$print = isset($_GET['print']) && (string) $_GET['print'] === '1';
$brand = $isSuper ? 'ecomae' : 'epartscart';

// Render full document (replaces CP chrome) so Print/PDF is clean.
epc_cp_full_brochure_render_and_exit(array(
	'brand' => $brand,
	'scope' => $scope !== '' ? $scope : ($isSuper ? 'all' : 'client'),
	'print' => $print,
	'base_path' => '/' . trim((string) ($DP_Config->backend_dir ?? 'cp'), '/') . '/control/cp_brochure',
));
