<?php
/**
 * ABCP-inspired orders workspace helpers (badges, KPI, status pills).
 */
defined('_ASTEXE_') or die('No access');

function epc_orders_ws_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_orders_ws_badge_class(int $statusId, PDO $db): string
{
	static $cache = array();
	if (!isset($cache[$statusId])) {
		$q = $db->prepare('SELECT `for_finish`, `for_inverse`, `for_created` FROM `shop_orders_statuses_ref` WHERE `id` = ? LIMIT 1');
		$q->execute(array($statusId));
		$row = $q->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			$cache[$statusId] = 'epc-scp-badge--normal';
		} elseif ((int) ($row['for_inverse'] ?? 0) === 1) {
			$cache[$statusId] = 'epc-scp-badge--urgent';
		} elseif ((int) ($row['for_finish'] ?? 0) === 1) {
			$cache[$statusId] = 'epc-scp-badge--tenant';
		} elseif ((int) ($row['for_created'] ?? 0) === 1) {
			$cache[$statusId] = 'epc-scp-badge--high';
		} else {
			$cache[$statusId] = 'epc-scp-badge--normal';
		}
	}
	return $cache[$statusId];
}

function epc_orders_ws_status_badge(int $statusId, array $orders_statuses, PDO $db): string
{
	$label = translate_str_by_id($orders_statuses[$statusId]['name'] ?? '');
	$cls = epc_orders_ws_badge_class($statusId, $db);
	return '<span class="epc-scp-badge ' . epc_orders_ws_h($cls) . '">' . epc_orders_ws_h($label) . '</span>';
}

function epc_orders_ws_paid_badge(int $paid): string
{
	if ($paid === 1) {
		return '<span class="epc-scp-badge epc-scp-badge--tenant">' . epc_orders_ws_h(translate_str_by_id(3514)) . '</span>';
	}
	if ($paid === 2) {
		return '<span class="epc-scp-badge epc-scp-badge--high">' . epc_orders_ws_h(translate_str_by_id(3515)) . '</span>';
	}
	return '<span class="epc-scp-badge epc-scp-badge--urgent">' . epc_orders_ws_h(translate_str_by_id(3513)) . '</span>';
}

function epc_orders_ws_kpi(PDO $db, array $offices_list, int $manager_id): array
{
	$officeIds = array_keys($offices_list);
	if (count($officeIds) === 0) {
		return array('open' => 0, 'today' => 0, 'pending_ship' => 0);
	}
	$ph = implode(',', array_fill(0, count($officeIds), '?'));
	$todayStart = strtotime('today midnight');

	$openStatuses = array();
	$q = $db->query("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1");
	while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
		$openStatuses[] = (int) $r['id'];
	}

	$open = 0;
	if (count($openStatuses) > 0) {
		$sp = implode(',', array_fill(0, count($openStatuses), '?'));
		$args = array_merge($officeIds, $openStatuses);
		$st = $db->prepare("SELECT COUNT(*) FROM `shop_orders` WHERE `office_id` IN ($ph) AND `status` IN ($sp)");
		$st->execute($args);
		$open = (int) $st->fetchColumn();
	}

	$argsToday = $officeIds;
	$argsToday[] = $todayStart;
	$st = $db->prepare("SELECT COUNT(*) FROM `shop_orders` WHERE `office_id` IN ($ph) AND `time` >= ?");
	$st->execute($argsToday);
	$today = (int) $st->fetchColumn();

	$shipStatuses = array();
	$q = $db->query("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` != 1 AND `for_inverse` != 1");
	while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
		$shipStatuses[] = (int) $r['id'];
	}
	$pendingShip = 0;
	if (count($shipStatuses) > 0) {
		$sp = implode(',', array_fill(0, count($shipStatuses), '?'));
		$args = array_merge($officeIds, $shipStatuses);
		$st = $db->prepare("SELECT COUNT(*) FROM `shop_orders` WHERE `office_id` IN ($ph) AND `status` IN ($sp) AND `paid` IN (1,2)");
		$st->execute($args);
		$pendingShip = (int) $st->fetchColumn();
	}

	return array('open' => $open, 'today' => $today, 'pending_ship' => $pendingShip);
}

function epc_orders_ws_in_process_status_ids(PDO $db): array
{
	$ids = array();
	$q = $db->query("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1");
	while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
		$ids[] = (int) $r['id'];
	}
	return $ids;
}
