<?php
/**
 * Public full CP brochure for epartscart.com (and other tenants).
 * URL: /brochure-cp  or  /brochure/cp
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_full_brochure.php';

$scope = preg_replace('/[^a-z]/', '', strtolower((string) ($_GET['scope'] ?? 'client')));
$print = isset($_GET['print']) && (string) $_GET['print'] === '1';
epc_cp_full_brochure_render_and_exit(array(
	'brand' => 'epartscart',
	'scope' => $scope !== '' ? $scope : 'client',
	'print' => $print,
	'base_path' => '/brochure-cp',
));
