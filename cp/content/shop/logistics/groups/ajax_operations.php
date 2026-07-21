<?php
/**
 * AJAX for warehouse groups (async polling groups).
 * Actions: get_table (HTML), get_storages (JSON), add_group (JSON), del (JSON).
 */
header('Content-Type: text/html; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('status' => false, 'message' => 'DB connect error')));
}
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
$multilang_params = multilang_init();

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

function epc_sg_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

/** @return array<int, int> storage IDs already assigned to any group */
function epc_sg_assigned_storage_ids(PDO $db): array
{
	$ids = array();
	$stmt = $db->query('SELECT `storages` FROM `shop_storages_groups`');
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$parts = preg_split('/\s*,\s*/', trim((string) ($row['storages'] ?? '')), -1, PREG_SPLIT_NO_EMPTY);
		foreach ($parts as $part) {
			$id = (int) $part;
			if ($id > 0) {
				$ids[$id] = $id;
			}
		}
	}
	return $ids;
}

/** Storages eligible for custom async groups (not Treelax DB / Docpart Price / Treelax catalogue). */
function epc_sg_available_storages(PDO $db): array
{
	$assigned = epc_sg_assigned_storage_ids($db);
	$out = array();
	$stmt = $db->query('SELECT `id`, `name`, `interface_type` FROM `shop_storages` WHERE `interface_type` NOT IN (1, 2, 6) ORDER BY `name`');
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$id = (int) $row['id'];
		if (isset($assigned[$id])) {
			continue;
		}
		$out[] = array(
			'id' => $id,
			'checked' => false,
			'name' => (string) $row['name'],
			'selected' => false,
			'interface_type' => (int) $row['interface_type'],
		);
	}
	return $out;
}

function epc_sg_storage_name_map(PDO $db): array
{
	$map = array();
	$stmt = $db->query('SELECT `id`, `name` FROM `shop_storages`');
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$map[(int) $row['id']] = (string) $row['name'];
	}
	return $map;
}

function epc_sg_render_table(PDO $db): string
{
	$names = epc_sg_storage_name_map($db);
	$rows = $db->query('SELECT * FROM `shop_storages_groups` ORDER BY `order`, `id`')->fetchAll(PDO::FETCH_ASSOC);
	$html = '<div class="col-lg-12"><div class="hpanel epc-sg-groups-panel">';
	$html .= '<div class="panel-heading hbuilt"><i class="fa fa-object-group"></i> Warehouse groups</div>';
	$html .= '<div class="panel-body">';
	if (!$rows) {
		$html .= '<div class="alert alert-info" style="margin:0">';
		$html .= '<strong>No warehouse groups yet.</strong> ';
		$html .= 'Create a group on the right to batch async API warehouses during price search. ';
		$html .= 'Docpart price lists and own-warehouse (types 1 / 2 / 6) are handled automatically and do not need a group.';
		$html .= '</div>';
	} else {
		$html .= '<div class="table-responsive"><table class="table table-striped" style="margin:0">';
		$html .= '<thead><tr><th style="width:70px">ID</th><th>Name</th><th>Warehouses</th><th style="width:90px"></th></tr></thead><tbody>';
		foreach ($rows as $row) {
			$id = (int) $row['id'];
			$parts = preg_split('/\s*,\s*/', trim((string) ($row['storages'] ?? '')), -1, PREG_SPLIT_NO_EMPTY);
			$labels = array();
			foreach ($parts as $part) {
				$sid = (int) $part;
				if ($sid <= 0) {
					continue;
				}
				$label = isset($names[$sid]) ? $names[$sid] : ('#' . $sid);
				$labels[] = epc_sg_h($label) . ' <span class="text-muted">(' . $sid . ')</span>';
			}
			$html .= '<tr>';
			$html .= '<td>' . $id . '</td>';
			$html .= '<td><strong>' . epc_sg_h($row['name'] ?? '') . '</strong></td>';
			$html .= '<td>' . ($labels ? implode(', ', $labels) : '<span class="text-muted">—</span>') . '</td>';
			$html .= '<td class="text-right"><a href="javascript:void(0);" class="btn btn-xs btn-danger" onclick="del(' . $id . ');"><i class="fa fa-trash"></i> Delete</a></td>';
			$html .= '</tr>';
		}
		$html .= '</tbody></table></div>';
	}
	$html .= '</div></div></div>';
	return $html;
}

$raw = (string) ($_POST['request_object'] ?? '');
$request = json_decode($raw, true);
if (!is_array($request)) {
	// Some clients send URL-encoded JSON
	$request = json_decode(urldecode($raw), true);
}
if (!is_array($request)) {
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(array('status' => false, 'message' => 'Bad request')));
}

$action = (string) ($request['action'] ?? '');

if ($action === 'get_table') {
	header('Content-Type: text/html; charset=utf-8');
	echo epc_sg_render_table($db_link);
	exit;
}

header('Content-Type: application/json; charset=utf-8');

if ($action === 'get_storages') {
	exit(json_encode(array(
		'status' => true,
		'storages_list' => epc_sg_available_storages($db_link),
	)));
}

if ($action === 'add_group') {
	$name = trim(urldecode((string) ($request['name'] ?? '')));
	$storages = isset($request['storages']) && is_array($request['storages']) ? $request['storages'] : array();
	$ids = array();
	foreach ($storages as $sid) {
		$id = (int) $sid;
		if ($id > 0) {
			$ids[$id] = $id;
		}
	}
	$ids = array_values($ids);
	if ($name === '' || !$ids) {
		exit(json_encode(array('status' => false, 'message' => 'Name and warehouses required')));
	}
	$order = (int) $db_link->query('SELECT COALESCE(MAX(`order`), 0) FROM `shop_storages_groups`')->fetchColumn() + 1;
	$ok = $db_link->prepare('INSERT INTO `shop_storages_groups` (`name`, `storages`, `order`) VALUES (?, ?, ?)')
		->execute(array($name, implode(',', $ids), $order));
	exit(json_encode(array('status' => (bool) $ok)));
}

if ($action === 'del') {
	$id = (int) ($request['id'] ?? 0);
	if ($id <= 0) {
		exit(json_encode(array('status' => false)));
	}
	$ok = $db_link->prepare('DELETE FROM `shop_storages_groups` WHERE `id` = ?')->execute(array($id));
	exit(json_encode(array('status' => (bool) $ok)));
}

exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
