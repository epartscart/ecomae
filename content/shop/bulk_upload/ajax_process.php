<?php
header('Content-Type: application/json;charset=utf-8;');
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
try {
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	$db_link->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$db_link->query("SET NAMES utf8;");
} catch (Exception $e) {
	exit(json_encode(array('status'=>false,'message'=>'No DB connect')));
}
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_pricing.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/bulk_upload/epc_bulk_helpers.php");

$user_id = DP_User::getUserId();
$is_admin_viewer = DP_User::isAdmin();
if($user_id <= 0 && !$is_admin_viewer) {
	epc_bulk_error('Please log in first.');
}

epc_bulk_check_csrf();
$group_id = 0;
if($is_admin_viewer && !empty($_POST['admin_group_id'])) {
	$profile_check = $db_link->prepare("SELECT `group_id` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1;");
	$profile_check->execute(array((int)$_POST['admin_group_id']));
	$group_id = (int)$profile_check->fetchColumn();
}
if($group_id <= 0 && $user_id > 0) {
	$userProfile = DP_User::getUserProfile();
	$group_id = (int)$userProfile["groups"][0];
}
if($group_id <= 0) {
	epc_bulk_error('Select customer price profile.');
}
$priority = isset($_POST['priority']) && $_POST['priority'] === 'delivery' ? 'delivery' : 'price';

if(isset($_POST['action']) && $_POST['action'] === 'history_update') {
	$upload_id = isset($_POST['upload_id']) ? (int)$_POST['upload_id'] : 0;
	$summary = isset($_POST['summary']) ? json_decode((string)$_POST['summary'], true) : null;
	$rows = isset($_POST['rows']) ? json_decode((string)$_POST['rows'], true) : null;
	$csv = isset($_POST['csv']) ? (string)$_POST['csv'] : '';
	if($upload_id <= 0 || !is_array($summary) || !is_array($rows)) {
		exit(json_encode(array('status'=>false,'message'=>'History update data is invalid.')));
	}
	$summary = array(
		'uploaded'=>isset($summary['uploaded']) ? (int)$summary['uploaded'] : count($rows),
		'available'=>isset($summary['available']) ? (int)$summary['available'] : 0,
		'cross'=>isset($summary['cross']) ? (int)$summary['cross'] : 0,
		'short'=>isset($summary['short']) ? (int)$summary['short'] : 0,
		'notfound'=>isset($summary['notfound']) ? (int)$summary['notfound'] : 0
	);
	$ok = epc_bulk_update_history($db_link, $user_id, $is_admin_viewer, $upload_id, $summary, $rows, $csv);
	exit(json_encode(array('status'=>$ok)));
}

$bunches = epc_bulk_customer_price_bunches($db_link);
if(empty($bunches)) {
	exit(json_encode(array('status'=>false,'message'=>'No price-list warehouses are available for your location.')));
}

if(isset($_POST['action']) && $_POST['action'] === 'cross') {
	$item = array(
		'brand'=>'',
		'article'=>isset($_POST['article']) ? $_POST['article'] : '',
		'qty'=>isset($_POST['qty']) ? (int)$_POST['qty'] : 1,
		'target_price'=>'',
		'delivery'=>'',
		'comment'=>''
	);
	if(trim($item['article']) === '') {
		exit(json_encode(array('status'=>false,'message'=>'Part number is required.')));
	}
	if($item['qty'] <= 0) { $item['qty'] = 1; }
	list($exact, $cross) = epc_bulk_best_options_for_item($db_link, $DP_Config, $group_id, $item, $bunches, $priority, true);
	exit(json_encode(array('status'=>true,'exact'=>$exact,'cross'=>$cross)));
}

if(empty($_FILES['bulk_file']) || $_FILES['bulk_file']['error'] !== UPLOAD_ERR_OK) {
	exit(json_encode(array('status'=>false,'message'=>'Upload file is required.')));
}

$items = epc_bulk_read_input_lines($_FILES['bulk_file']['tmp_name'], $_FILES['bulk_file']['name']);
if(empty($items)) {
	exit(json_encode(array('status'=>false,'message'=>'No valid rows found. Use Brand, Part Number, Qty columns.')));
}

try {
	$processed = epc_bulk_process_items($db_link, $DP_Config, $group_id, $items, $priority, false);
} catch (Throwable $e) {
	exit(json_encode(array('status'=>false,'message'=>$e->getMessage())));
}
$rows = $processed['rows'];
$summary = $processed['summary'];
$csv = $processed['csv'];

$upload_id = epc_bulk_save_history($db_link, $user_id, $is_admin_viewer, $group_id, $_FILES['bulk_file']['name'], $priority, $summary, $rows, $csv, 'storefront');
exit(json_encode(array('status'=>true,'rows'=>$rows,'summary'=>$summary,'csv'=>$csv,'upload_id'=>$upload_id)));
