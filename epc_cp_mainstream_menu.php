<?php
/**
 * Top-level CP sidebar groups for Channels, ERP, and AI Agent (not under Shop).
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_mm_lang(PDO $pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_cp_mm_find_shop_group(PDO $pdo)
{
	$row = $pdo->query("SELECT `id`, `order` FROM `control_groups` WHERE `caption` = '744' LIMIT 1")->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		return array('id' => (int)$row['id'], 'order' => (int)$row['order']);
	}
	$row = $pdo->query(
		'SELECT g.`id`, g.`order`, COUNT(i.`id`) AS cnt
		 FROM `control_groups` g
		 INNER JOIN `control_items` i ON i.`items_group` = g.`id`
		 WHERE i.`url` LIKE \'%<backend>/shop/%\'
		 GROUP BY g.`id`, g.`order`
		 ORDER BY cnt DESC LIMIT 1'
	)->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		return array('id' => (int)$row['id'], 'order' => (int)$row['order']);
	}
	return array('id' => 6, 'order' => 5);
}

function epc_cp_mm_group_id(PDO $pdo, $captionKey)
{
	$st = $pdo->prepare('SELECT `id` FROM `control_groups` WHERE `caption` = ? LIMIT 1');
	$st->execute(array($captionKey));
	return (int)$st->fetchColumn();
}

function epc_cp_mm_ensure_group(PDO $pdo, $captionKey, $en, $ru, $order)
{
	epc_cp_mm_lang($pdo, $captionKey, $en, $ru);
	$id = epc_cp_mm_group_id($pdo, $captionKey);
	if ($id > 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = ? WHERE `id` = ?')->execute(array((int)$order, $id));
		return $id;
	}
	$pdo->prepare('INSERT INTO `control_groups` (`caption`, `order`) VALUES (?, ?)')->execute(array($captionKey, (int)$order));
	return (int)$pdo->lastInsertId();
}

function epc_cp_mm_item_id_for_url(PDO $pdo, $url)
{
	$st = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? LIMIT 1');
	$st->execute(array($url));
	$id = (int) $st->fetchColumn();
	if ($id > 0) {
		return $id;
	}
	$base = preg_replace('#\?.*$#', '', (string) $url);
	if ($base === '' || $base === $url) {
		return 0;
	}
	$st = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? OR `url` LIKE ? LIMIT 1');
	$st->execute(array($base, $base . '?%'));
	return (int) $st->fetchColumn();
}

function epc_cp_mm_ensure_item(PDO $pdo, $groupId, $captionKey, $url, $order, $color, $icon, $showAnyway = 0)
{
	$id = epc_cp_mm_item_id_for_url($pdo, $url);
	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `control_items` SET `items_group` = ?, `caption` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ?, `show_anyway` = ? WHERE `id` = ?'
		)->execute(array((int)$groupId, $captionKey, (int)$order, $color, $icon, (int)$showAnyway, $id));
		return $id;
	}
	$pdo->prepare(
		'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
		 VALUES (?, ?, ?, \'\', ?, ?, ?, \'\', ?)'
	)->execute(array((int)$groupId, $captionKey, $url, (int)$order, $color, $icon, (int)$showAnyway));
	return (int)$pdo->lastInsertId();
}

/**
 * Create/update CP menu groups: Channels, Logistics, ERP, AI Agent.
 *
 * @return array{channels_group:int,logistics_group:int,erp_group:int,ai_group:int,shop_group:int,items:array}
 */
function epc_cp_mainstream_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_channels', 'Channels', 'Каналы');
	epc_cp_mm_lang($pdo, 'epc_cp_group_logistics', 'Logistics', 'Логистика');
	epc_cp_mm_lang($pdo, 'epc_cp_group_erp', 'ERP Suite', 'ERP — бизнес');
	epc_cp_mm_lang($pdo, 'epc_erp_suite_cp', 'ERP &amp; Business', 'ERP и бизнес');
	epc_cp_mm_lang($pdo, 'epc_cp_group_ai', 'AI Agent', 'AI агент');
	epc_cp_mm_lang($pdo, 'epc_logistics_cp', 'Logistics hub', 'Логистика — обзор');
	epc_cp_mm_lang($pdo, 'epc_logistics_carriers_cp', 'Carriers & shipments', 'Перевозчики и отправки');
	epc_cp_mm_lang($pdo, 'epc_logistics_guide_cp', 'Logistics guide', 'Гид по логистике');
	epc_cp_mm_lang($pdo, 'epc_logistics_obtain_cp', 'Delivery methods', 'Способы доставки');
	epc_cp_mm_lang($pdo, 'epc_logistics_orders_cp', 'Customer orders', 'Заказы клиентов');
	epc_cp_mm_lang($pdo, 'epc_custom_shipping_cp', 'Custom & Shipping', 'Таможня и доставка');
	epc_cp_mm_lang($pdo, 'epc_custom_shipping_guide_cp', 'Custom & Shipping guide', 'Гид: таможня и доставка');

	$shop = epc_cp_mm_find_shop_group($pdo);
	$slot = $shop['order'] + 1;

	$keys = array('epc_cp_group_channels', 'epc_cp_group_logistics', 'epc_cp_group_erp', 'epc_cp_group_ai');
	$missing = 0;
	foreach ($keys as $key) {
		if (epc_cp_mm_group_id($pdo, $key) <= 0) {
			$missing++;
		}
	}
	if ($missing >= 3) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 4 WHERE `order` >= ? AND `id` != ?')
			->execute(array($slot, (int)$shop['id']));
	}

	$channelsGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_channels', 'Channels', 'Каналы', $slot);
	$logisticsGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_logistics', 'Logistics', 'Логистика', $slot + 1);
	$erpGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_erp', 'ERP Suite', 'ERP — бизнес', $slot + 2);
	$aiGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_ai', 'AI Agent', 'AI агент', $slot + 3);

	$items = array();

	$items['channels_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$channelsGroup,
		'epc_channels_cp',
		'/<backend>/shop/channels/channels',
		10,
		'#2563eb',
		'fas fa-plug',
		1
	);
	$items['channels_guide'] = epc_cp_mm_ensure_item(
		$pdo,
		$channelsGroup,
		'epc_channels_guide_cp',
		'/<backend>/shop/channels/guide',
		20,
		'#2563eb',
		'fas fa-book',
		0
	);
	$items['logistics_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$logisticsGroup,
		'epc_logistics_cp',
		'/<backend>/shop/logistics',
		10,
		'#0f766e',
		'fas fa-th-large',
		1
	);
	$items['logistics_carriers'] = epc_cp_mm_ensure_item(
		$pdo,
		$logisticsGroup,
		'epc_logistics_carriers_cp',
		'/<backend>/shop/logistics/carriers',
		20,
		'#0f766e',
		'fas fa-shipping-fast',
		0
	);
	$items['logistics_guide'] = epc_cp_mm_ensure_item(
		$pdo,
		$logisticsGroup,
		'epc_logistics_guide_cp',
		'/<backend>/shop/logistics/guide',
		30,
		'#0f766e',
		'fas fa-book',
		0
	);
	$items['logistics_obtain'] = epc_cp_mm_ensure_item(
		$pdo,
		$logisticsGroup,
		'epc_logistics_obtain_cp',
		'/<backend>/shop/logistics/sposoby-polucheniya',
		40,
		'#64748b',
		'fas fa-truck',
		0
	);
	$items['logistics_orders'] = epc_cp_mm_ensure_item(
		$pdo,
		$logisticsGroup,
		'epc_logistics_orders_cp',
		'/<backend>/shop/orders/orders',
		50,
		'#64748b',
		'fas fa-shopping-cart',
		0
	);

	// Remove obtaining modes from Channels group if it still points at logistics URL
	$st = $pdo->prepare('SELECT `id`, `items_group` FROM `control_items` WHERE `caption` = ? LIMIT 1');
	$st->execute(array('epc_cp_channels_obtain_modes'));
	$oldObtain = $st->fetch(PDO::FETCH_ASSOC);
	if ($oldObtain && (int)$oldObtain['items_group'] === (int)$channelsGroup) {
		$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array((int)$oldObtain['id']));
	}

	$items['erp_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$erpGroup,
		'epc_erp_suite_cp',
		'/<backend>/shop/finance/erp?epc_erp_shell=1',
		10,
		'#1e3a5f',
		'fas fa-briefcase',
		1
	);
	$items['erp_guide'] = epc_cp_mm_ensure_item(
		$pdo,
		$erpGroup,
		'epc_erp_guide_cp',
		'/<backend>/shop/finance/erp/guide?epc_erp_shell=1',
		20,
		'#27ae60',
		'fas fa-book',
		0
	);
	$items['custom_shipping'] = epc_cp_mm_ensure_item(
		$pdo,
		$erpGroup,
		'epc_custom_shipping_cp',
		'/<backend>/shop/finance/erp?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1',
		15,
		'#0f766e',
		'fas fa-ship',
		0
	);
	$items['custom_shipping_guide'] = epc_cp_mm_ensure_item(
		$pdo,
		$erpGroup,
		'epc_custom_shipping_guide_cp',
		'/<backend>/shop/finance/erp/custom-shipping-guide?epc_erp_shell=1',
		25,
		'#0f766e',
		'fas fa-book',
		0
	);
	$items['uae_tax_compliance'] = epc_cp_mm_ensure_item(
		$pdo,
		$erpGroup,
		'epc_uae_tax_compliance_cp',
		'/<backend>/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1',
		26,
		'#7c3aed',
		'fas fa-gavel',
		0
	);
	epc_cp_erp_menu_cleanup($pdo, $erpGroup, $items);

	$items['ai_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$aiGroup,
		'epc_parts_agent_chats_cp',
		'/<backend>/shop/parts_agent_chats',
		10,
		'#8e44ad',
		'fas fa-robot',
		1
	);

	$shopOrdersMenu = epc_cp_shop_orders_menu_apply($pdo);
	if (!empty($shopOrdersMenu['shop_orders_item'])) {
		$items['shop_orders'] = (int) $shopOrdersMenu['shop_orders_item'];
	}

	$shopCataloguePrices = epc_cp_shop_catalogue_prices_menu_apply($pdo);
	foreach ((array) ($shopCataloguePrices['items'] ?? array()) as $k => $itemId) {
		$items['shop_' . $k] = (int) $itemId;
	}

	return array(
		'shop_group' => (int)$shop['id'],
		'channels_group' => $channelsGroup,
		'logistics_group' => $logisticsGroup,
		'erp_group' => $erpGroup,
		'ai_group' => $aiGroup,
		'menu_orders' => array('channels' => $slot, 'logistics' => $slot + 1, 'erp' => $slot + 2, 'ai' => $slot + 3),
		'items' => $items,
	);
}

/**
 * Top-level Payment gateways CP group (alongside Channels, ERP, AI).
 */
function epc_cp_payments_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_payments', 'Payment gateways', 'Платёжные системы');

	$shop = epc_cp_mm_find_shop_group($pdo);
	$channelsOrder = epc_cp_mm_group_id($pdo, 'epc_cp_group_channels');
	$slot = $channelsOrder > 0 ? (int)$pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int)$channelsOrder . ' LIMIT 1')->fetchColumn() : $shop['order'] + 1;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_payments') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$paymentsGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_payments', 'Payment gateways', 'Платёжные системы', $slot);

	$items = array();
	$items['payments_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$paymentsGroup,
		'epc_payments_cp',
		'/<backend>/shop/payments/payments',
		10,
		'#7c3aed',
		'fas fa-credit-card',
		1
	);
	$items['payments_guide'] = epc_cp_mm_ensure_item(
		$pdo,
		$paymentsGroup,
		'epc_payments_guide_cp',
		'/<backend>/shop/payments/payments/guide',
		20,
		'#7c3aed',
		'fas fa-book',
		0
	);

	epc_cp_payments_menu_cleanup($pdo, $paymentsGroup, $items);

	return array(
		'payments_group' => $paymentsGroup,
		'items' => $items,
	);
}

/**
 * Remove duplicate ERP Suite sidebar entries; keep hub + guide only.
 */
function epc_cp_erp_menu_cleanup(PDO $pdo, $erpGroup, array $items)
{
	$keep = array();
	foreach ($items as $itemId) {
		$itemId = (int) $itemId;
		if ($itemId > 0) {
			$keep[$itemId] = true;
		}
	}
	if (empty($keep)) {
		return 0;
	}
	$removed = 0;
	$st = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `items_group` = ?');
	$st->execute(array((int) $erpGroup));
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$id = (int) $row['id'];
		if (!isset($keep[$id])) {
			$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array($id));
			$removed++;
		}
	}
	$erpBase = '/<backend>/shop/finance/erp';
	$guideBase = '/<backend>/shop/finance/erp/guide';
	$orphan = $pdo->query(
		"SELECT `id`, `url` FROM `control_items`
		 WHERE `url` LIKE '%/shop/finance/erp%'
		 ORDER BY `id` ASC"
	);
	while ($row = $orphan->fetch(PDO::FETCH_ASSOC)) {
		$id = (int) $row['id'];
		if (isset($keep[$id])) {
			continue;
		}
		$url = preg_replace('#\?.*$#', '', (string) $row['url']);
		if ($url === $erpBase || $url === $guideBase) {
			$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array($id));
			$removed++;
		}
	}
	return $removed;
}

/**
 * Remove duplicate Payment gateways sidebar entries; keep hub + guide only.
 */
function epc_cp_payments_menu_cleanup(PDO $pdo, $paymentsGroup, array $items)
{
	$keep = array();
	foreach ($items as $itemId) {
		$itemId = (int)$itemId;
		if ($itemId > 0) {
			$keep[$itemId] = true;
		}
	}
	if (empty($keep)) {
		return 0;
	}
	$st = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `items_group` = ?');
	$st->execute(array((int)$paymentsGroup));
	$removed = 0;
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$id = (int)$row['id'];
		if (!isset($keep[$id])) {
			$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array($id));
			$removed++;
		}
	}
	return $removed;
}

/**
 * Top-level Marketing CP group — growth hub with 10 strategies.
 */
function epc_cp_marketing_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_marketing', 'Marketing', 'Маркетинг');
	epc_cp_mm_lang($pdo, 'epc_marketing_cp', 'Marketing & growth', 'Маркетинг и рост');

	$aiOrder = epc_cp_mm_group_id($pdo, 'epc_cp_group_ai');
	$slot = $aiOrder > 0
		? (int)$pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int)$aiOrder . ' LIMIT 1')->fetchColumn() + 1
		: epc_cp_mm_find_shop_group($pdo)['order'] + 5;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_marketing') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$marketingGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_marketing', 'Marketing', 'Маркетинг', $slot);

	$items = array();
	$items['marketing_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$marketingGroup,
		'epc_marketing_cp',
		'/<backend>/shop/marketing/marketing',
		10,
		'#db2777',
		'fas fa-bullhorn',
		1
	);

	return array(
		'marketing_group' => $marketingGroup,
		'items' => $items,
	);
}

/**
 * Top-level Procurement CP group — suppliers, purchases, payments.
 */
function epc_cp_procurement_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_procurement', 'Procurement', 'Закупки');
	epc_cp_mm_lang($pdo, 'epc_procurement_cp', 'Procurement & suppliers', 'Закупки и поставщики');

	$erpOrder = epc_cp_mm_group_id($pdo, 'epc_cp_group_erp');
	$slot = $erpOrder > 0
		? (int)$pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int)$erpOrder . ' LIMIT 1')->fetchColumn() + 1
		: epc_cp_mm_find_shop_group($pdo)['order'] + 3;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_procurement') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$procGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_procurement', 'Procurement', 'Закупки', $slot);

	$items = array();
	$items['procurement_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$procGroup,
		'epc_procurement_cp',
		'/<backend>/shop/procurement/procurement',
		10,
		'#1e4d3a',
		'fas fa-truck-loading',
		1
	);

	return array(
		'procurement_group' => $procGroup,
		'items' => $items,
	);
}

/**
 * POS Terminal — under ERP Suite group (retail counter).
 */
function epc_cp_pos_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_pos_terminal_cp', 'POS Terminal', 'Касса POS');

	$erpGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_erp');
	if ($erpGroup <= 0) {
		$shop = epc_cp_mm_find_shop_group($pdo);
		$erpGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_erp', 'ERP Suite', 'ERP — бизнес', $shop['order'] + 2);
	}

	$itemId = epc_cp_mm_ensure_item(
		$pdo,
		$erpGroup,
		'epc_pos_terminal_cp',
		'/<backend>/shop/pos/terminal',
		12,
		'#2563eb',
		'fa-cash-register',
		1
	);

	return array(
		'erp_group' => $erpGroup,
		'items' => array('pos_terminal' => $itemId),
	);
}

/**
 * Top-level Customers CP group — customer management hub.
 */
function epc_cp_customer_mgmt_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_customers', 'Customers', 'Клиенты');
	epc_cp_mm_lang($pdo, 'epc_customer_mgmt_cp', 'Customer management', 'Управление клиентами');

	$oldUrls = array('/<backend>/users/customer_mgmt', '/<backend>/shop/customer_mgmt/customer_mgmt');
	foreach ($oldUrls as $oldUrl) {
		$st = $pdo->prepare('SELECT `id`, `items_group` FROM `control_items` WHERE `url` = ? LIMIT 1');
		$st->execute(array($oldUrl));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row && (int)$row['items_group'] !== 0) {
			$grp = $pdo->prepare('SELECT `caption` FROM `control_groups` WHERE `id` = ? LIMIT 1');
			$grp->execute(array((int)$row['items_group']));
			$cap = (string)$grp->fetchColumn();
			if ($cap !== 'epc_cp_group_customers') {
				$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array((int)$row['id']));
			}
		}
	}

	$procGroupId = epc_cp_mm_group_id($pdo, 'epc_cp_group_procurement');
	$slot = $procGroupId > 0
		? (int)$pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int)$procGroupId . ' LIMIT 1')->fetchColumn() + 1
		: epc_cp_mm_find_shop_group($pdo)['order'] + 2;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_customers') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$customersGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_customers', 'Customers', 'Клиенты', $slot);

	$itemId = epc_cp_mm_ensure_item(
		$pdo,
		$customersGroup,
		'epc_customer_mgmt_cp',
		'/<backend>/shop/customer_mgmt/customer_mgmt',
		10,
		'#2563eb',
		'fas fa-address-book',
		1
	);

	return array(
		'customers_group' => $customersGroup,
		'customer_mgmt_item' => $itemId,
	);
}

function epc_cp_document_control_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_documents', 'Documents', 'Документы');
	epc_cp_mm_lang($pdo, 'epc_document_control_cp', 'Document Control', 'Управление документами');

	$oldUrls = array('/<backend>/shop/modul-pechati-dokumentov', '/<backend>/shop/document_control/document_control');
	foreach ($oldUrls as $oldUrl) {
		$st = $pdo->prepare('SELECT `id`, `items_group` FROM `control_items` WHERE `url` = ? LIMIT 1');
		$st->execute(array($oldUrl));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row && (int)$row['items_group'] !== 0) {
			$grp = $pdo->prepare('SELECT `caption` FROM `control_groups` WHERE `id` = ? LIMIT 1');
			$grp->execute(array((int)$row['items_group']));
			$cap = (string)$grp->fetchColumn();
			if ($cap !== 'epc_cp_group_documents') {
				$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array((int)$row['id']));
			}
		}
	}

	$customersGroupId = epc_cp_mm_group_id($pdo, 'epc_cp_group_customers');
	$slot = $customersGroupId > 0
		? (int)$pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int)$customersGroupId . ' LIMIT 1')->fetchColumn() + 1
		: epc_cp_mm_find_shop_group($pdo)['order'] + 3;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_documents') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$documentsGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_documents', 'Documents', 'Документы', $slot);

	$itemId = epc_cp_mm_ensure_item(
		$pdo,
		$documentsGroup,
		'epc_document_control_cp',
		'/<backend>/shop/document_control/document_control',
		10,
		'#0f766e',
		'fas fa-file-invoice',
		1
	);

	return array(
		'documents_group' => $documentsGroup,
		'document_control_item' => $itemId,
	);
}

function epc_cp_super_platform_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_tenant_hub', 'Platform', 'Платформа');
	epc_cp_mm_lang($pdo, 'epc_tenant_hub_cp', 'Tenant hub', 'Центр клиентов');

	$portalGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_portal');
	$slot = $portalGroup > 0
		? (int) $pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int) $portalGroup . ' LIMIT 1')->fetchColumn()
		: 1;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_tenant_hub') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$hubGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_tenant_hub', 'Platform', 'Платформа', $slot);

	$itemId = epc_cp_mm_ensure_item(
		$pdo,
		$hubGroup,
		'epc_tenant_hub_cp',
		'/<backend>/shop/tenant_hub/tenant_hub',
		1,
		'#0369a1',
		'fas fa-cloud',
		1
	);

	return array(
		'tenant_hub_group' => $hubGroup,
		'tenant_hub_item' => $itemId,
	);
}

/**
 * Super CP operator tools — customer board, pricing, content, communication.
 *
 * @return array<string,int>
 */
function epc_cp_super_cp_operator_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_operator', 'Operator', 'Оператор');
	epc_cp_mm_lang($pdo, 'epc_cp_group_operator_desc', 'Cross-tenant platform tools', 'Инструменты платформы');
	epc_cp_mm_lang($pdo, 'epc_super_cp_operator_guide', 'Operator guide', 'Гид оператора');
	epc_cp_mm_lang($pdo, 'epc_super_cp_customer_board', 'Customer board', 'Клиенты платформы');
	epc_cp_mm_lang($pdo, 'epc_super_cp_price_configs', 'Price configs', 'Генерация цен');
	epc_cp_mm_lang($pdo, 'epc_super_cp_info_blocks', 'Info blocks', 'Инфо-блоки');
	epc_cp_mm_lang($pdo, 'epc_visual_page_editor', 'Visual page editor', 'Визуальный редактор');
	epc_cp_mm_lang($pdo, 'epc_super_cp_communication', 'Communication', 'Коммуникации');

	$hubGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_tenant_hub');
	$slot = $hubGroup > 0
		? (int) $pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int) $hubGroup . ' LIMIT 1')->fetchColumn() + 1
		: 2;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_operator') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$operatorGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_operator', 'Operator', 'Оператор', $slot);

	$items = array();
	$items['operator_guide'] = epc_cp_mm_ensure_item(
		$pdo,
		$operatorGroup,
		'epc_super_cp_operator_guide',
		'/<backend>/control/portal/epc_super_cp_operator_guide',
		1,
		'#2563eb',
		'fas fa-book',
		1
	);
	$items['customer_board'] = epc_cp_mm_ensure_item(
		$pdo,
		$operatorGroup,
		'epc_super_cp_customer_board',
		'/<backend>/control/portal/epc_super_cp_customer_board',
		2,
		'#2563eb',
		'fas fa-users',
		1
	);
	$items['price_configs'] = epc_cp_mm_ensure_item(
		$pdo,
		$operatorGroup,
		'epc_super_cp_price_configs',
		'/<backend>/control/portal/epc_super_cp_price_configs',
		3,
		'#1d4ed8',
		'fas fa-tags',
		1
	);
	$items['info_blocks'] = epc_cp_mm_ensure_item(
		$pdo,
		$operatorGroup,
		'epc_super_cp_info_blocks',
		'/<backend>/control/portal/epc_super_cp_info_blocks',
		4,
		'#0ea5e9',
		'fas fa-th-large',
		1
	);
	$items['visual_editor'] = epc_cp_mm_ensure_item(
		$pdo,
		$operatorGroup,
		'epc_visual_page_editor',
		'/<backend>/control/portal/epc_visual_page_editor',
		5,
		'#7c3aed',
		'fas fa-magic',
		1
	);
	$items['communication'] = epc_cp_mm_ensure_item(
		$pdo,
		$operatorGroup,
		'epc_super_cp_communication',
		'/<backend>/control/portal/epc_super_cp_communication',
		6,
		'#0369a1',
		'fas fa-envelope',
		1
	);

	return array_merge(array('operator_group' => $operatorGroup), $items);
}

/**
 * Integrations hub — Super CP + tenant CP sidebar group.
 *
 * @return array<string, mixed>
 */
function epc_cp_integrations_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_cp_group_integrations', 'Integrations', 'Интеграции');
	epc_cp_mm_lang($pdo, 'epc_cp_group_integrations_desc', 'Email, mobile, payments & more', 'Почта, мобильные, платежи');
	epc_cp_mm_lang($pdo, 'epc_integrations_hub_cp', 'Integrations hub', 'Хаб интеграций');
	epc_cp_mm_lang($pdo, 'epc_mobile_apps_cp', 'Mobile apps', 'Мобильные приложения');
	epc_cp_mm_lang($pdo, 'epc_tenant_features_cp', 'Tenant features', 'Функции клиентов');
	epc_cp_mm_lang($pdo, 'epc_tenant_email_cp', 'Email / SMTP', 'Email / SMTP');

	$portalGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_portal');
	$slot = $portalGroup > 0
		? (int) $pdo->query('SELECT `order` FROM `control_groups` WHERE `id` = ' . (int) $portalGroup . ' LIMIT 1')->fetchColumn() + 1
		: 3;

	if (epc_cp_mm_group_id($pdo, 'epc_cp_group_integrations') <= 0) {
		$pdo->prepare('UPDATE `control_groups` SET `order` = `order` + 1 WHERE `order` >= ?')
			->execute(array($slot));
	}

	$intGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_integrations', 'Integrations', 'Интеграции', $slot);

	$items = array();
	$items['integrations_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$intGroup,
		'epc_integrations_hub_cp',
		'/<backend>/control/portal/epc_integrations_hub',
		1,
		'#059669',
		'fas fa-plug',
		1
	);
	$items['mobile_apps'] = epc_cp_mm_ensure_item(
		$pdo,
		$intGroup,
		'epc_mobile_apps_cp',
		'/<backend>/control/portal/epc_mobile_apps',
		2,
		'#dc2626',
		'fas fa-mobile-alt',
		1
	);
	$items['tenant_email'] = epc_cp_mm_ensure_item(
		$pdo,
		$intGroup,
		'epc_tenant_email_cp',
		'/<backend>/control/portal/epc_tenant_email_settings',
		3,
		'#33cc33',
		'far fa-envelope',
		0
	);
	$items['tenant_features'] = epc_cp_mm_ensure_item(
		$pdo,
		$intGroup,
		'epc_tenant_features_cp',
		'/<backend>/control/portal/epc_tenant_features',
		4,
		'#2563eb',
		'fas fa-sliders',
		1
	);

	return array_merge(array('integrations_group' => $intGroup), $items);
}

/**
 * Portal CP group — Visual page editor / social / broadcast (tenant + Super CP sidebar).
 * POS Terminal stays under ERP Suite only (see epc_cp_pos_menu_apply).
 *
 * @return array{portal_group:int,items:array<string,int>}
 */
function epc_cp_portal_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_visual_page_editor', 'Visual page editor', 'Визуальный редактор страниц');
	epc_cp_mm_lang($pdo, 'epc_social_media_hub_cp', 'Social media hub', 'Соцсети — хаб');
	epc_cp_mm_lang($pdo, 'epc_marketing_broadcast_cp', 'Marketing broadcast', 'Рассылка — маркетинг');

	$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);

	$items = array();
	// POS is ERP-only. Re-home any historical Portal POS seed under ERP.
	if (function_exists('epc_cp_pos_menu_apply')) {
		epc_cp_pos_menu_apply($pdo);
	}
	$items['visual_editor'] = epc_cp_mm_ensure_item(
		$pdo,
		$portalGroup,
		'epc_visual_page_editor',
		'/<backend>/control/portal/epc_visual_page_editor',
		16,
		'#7c3aed',
		'fas fa-magic',
		1
	);
	$items['social_media_hub'] = epc_cp_mm_ensure_item(
		$pdo,
		$portalGroup,
		'epc_social_media_hub_cp',
		'/<backend>/control/portal/epc_social_media_hub',
		17,
		'#e1306c',
		'fas fa-share-alt',
		1
	);
	$items['marketing_broadcast'] = epc_cp_mm_ensure_item(
		$pdo,
		$portalGroup,
		'epc_marketing_broadcast_cp',
		'/<backend>/control/portal/epc_marketing_broadcast',
		18,
		'#db2777',
		'fas fa-bullhorn',
		1
	);

	return array(
		'portal_group' => $portalGroup,
		'items' => $items,
	);
}

/**
 * Shop top-menu rows for catalogue SKU media + price upload submodules.
 * Keeps new modules discoverable under Commerce (group 744) next to Price lists.
 *
 * @return array{shop_group:int,items:array<string,int>}
 */
function epc_cp_shop_catalogue_prices_menu_apply(PDO $pdo)
{
	epc_cp_mm_lang($pdo, 'epc_sku_media_manager', 'SKU photos & specs', 'Фото и характеристики SKU');
	epc_cp_mm_lang($pdo, 'epc_prices_multivendor_cp', 'Multi-vendor upload', 'Мульти-вендор загрузка');
	epc_cp_mm_lang($pdo, 'epc_prices_guide_cp', 'Price upload guide', 'Гид по загрузке цен');

	$shop = epc_cp_mm_find_shop_group($pdo);
	$shopGroupId = (int) ($shop['id'] ?? 0);
	$out = array('shop_group' => $shopGroupId, 'items' => array(), 'removed_commerce' => 0);
	if ($shopGroupId <= 0) {
		return $out;
	}

	// Retired: Commerce S/P/L upload — Multivendor covers multi-supplier price files.
	$rm = $pdo->prepare(
		"DELETE FROM `control_items`
		 WHERE `url` LIKE '%/shop/prices/commerce%'
		    OR `caption` = 'epc_prices_commerce_cp'"
	);
	$rm->execute();
	$out['removed_commerce'] = (int) $rm->rowCount();

	// Prefer slots near Price lists (caption 771) when present.
	$orderBase = 14;
	$priceOrderSt = $pdo->prepare(
		'SELECT `order` FROM `control_items` WHERE `items_group` = ? AND (`url` LIKE ? OR `caption` = ?) ORDER BY `order` ASC LIMIT 1'
	);
	$priceOrderSt->execute(array($shopGroupId, '%/shop/prices', '771'));
	$priceOrder = (int) $priceOrderSt->fetchColumn();
	if ($priceOrder > 0) {
		$orderBase = $priceOrder + 1;
	}

	$defs = array(
		'sku_media' => array(
			'caption' => 'epc_sku_media_manager',
			'url' => '/<backend>/shop/catalogue/sku_media',
			'order' => $orderBase,
			'color' => '#0f766e',
			'icon' => 'fa-picture-o',
		),
		'multivendor' => array(
			'caption' => 'epc_prices_multivendor_cp',
			'url' => '/<backend>/shop/prices/multivendor',
			'order' => $orderBase + 1,
			'color' => '#0891b2',
			'icon' => 'fa-handshake-o',
		),
		'prices_guide' => array(
			'caption' => 'epc_prices_guide_cp',
			'url' => '/<backend>/shop/prices/guide',
			'order' => $orderBase + 2,
			'color' => '#26ad5f',
			'icon' => 'fas fa-book',
		),
	);

	foreach ($defs as $key => $def) {
		$out['items'][$key] = (int) epc_cp_mm_ensure_item(
			$pdo,
			$shopGroupId,
			$def['caption'],
			$def['url'],
			(int) $def['order'],
			$def['color'],
			$def['icon'],
			1
		);
	}

	// Drop orphan duplicate under the unused epc_cp_group_commerce group if present.
	$orphanGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_commerce');
	if ($orphanGroup > 0) {
		$keepIds = array_values(array_filter(array_map('intval', $out['items'])));
		$st = $pdo->prepare('SELECT `id`, `url` FROM `control_items` WHERE `items_group` = ?');
		$st->execute(array($orphanGroup));
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$id = (int) $row['id'];
			if (in_array($id, $keepIds, true)) {
				continue;
			}
			$url = (string) ($row['url'] ?? '');
			if (strpos($url, 'sku_media') !== false
				|| strpos($url, 'prices/multivendor') !== false
				|| strpos($url, 'prices/commerce') !== false
				|| strpos($url, 'prices/guide') !== false) {
				$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array($id));
			}
		}
		$left = (int) $pdo->query('SELECT COUNT(*) FROM `control_items` WHERE `items_group` = ' . (int) $orphanGroup)->fetchColumn();
		if ($left === 0) {
			$pdo->prepare('DELETE FROM `control_groups` WHERE `id` = ?')->execute(array($orphanGroup));
		}
	}

	return $out;
}

/**
 * Shop sidebar — single "OMS · Orders" entry (/cp/shop/orders/orders).
 * Removes separate Orders items / Orders statuses menu rows (content routes remain).
 *
 * @return array{shop_group:int,shop_orders_item:int,order:int,removed:int}
 */
function epc_cp_shop_orders_menu_apply(PDO $pdo)
{
	// One OMS entry in Shop — not separate Orders / Items / Statuses sidebar rows.
	epc_cp_mm_lang($pdo, '282', 'OMS · Orders', 'OMS · Заказы');
	epc_cp_mm_lang($pdo, '284', 'OMS · Orders', 'OMS · Заказы');
	epc_cp_mm_lang($pdo, 'epc_shop_orders_cp', 'OMS · Orders', 'OMS · Заказы');
	epc_cp_mm_lang($pdo, 'epc_oms_orders_cp', 'OMS · Orders', 'OMS · Заказы');
	epc_cp_mm_lang($pdo, 'epc_oms_guide_cp', 'OMS daily guide', 'OMS — ежедневный гид');
	epc_cp_mm_lang($pdo, 'epc_logistics_orders_cp', 'OMS · Orders', 'OMS · Заказы');

	$shop = epc_cp_mm_find_shop_group($pdo);
	$shopGroupId = (int) ($shop['id'] ?? 0);
	if ($shopGroupId <= 0) {
		return array('shop_group' => 0, 'shop_orders_item' => 0, 'order' => 0, 'removed' => 0);
	}

	$ordersUrl = '/<backend>/shop/orders/orders';

	$st = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `items_group` = ? AND `url` = ? LIMIT 1');
	$st->execute(array($shopGroupId, $ordersUrl));
	$itemId = (int) $st->fetchColumn();

	$orderSlot = 25;
	$statusSt = $pdo->prepare(
		'SELECT `order` FROM `control_items` WHERE `items_group` = ? AND (`url` LIKE ? OR `caption` IN (?, ?)) ORDER BY `order` ASC LIMIT 1'
	);
	$statusSt->execute(array($shopGroupId, '%/shop/orders/statuses%', '279', '281'));
	$statusOrder = (int) $statusSt->fetchColumn();
	if ($statusOrder > 0) {
		$orderSlot = max(1, $statusOrder - 1);
	} else {
		$whSt = $pdo->prepare(
			'SELECT `order` FROM `control_items` WHERE `items_group` = ? AND `url` LIKE ? ORDER BY `order` DESC LIMIT 1'
		);
		$whSt->execute(array($shopGroupId, '%/storages%'));
		$whOrder = (int) $whSt->fetchColumn();
		if ($whOrder > 0) {
			$orderSlot = $whOrder + 1;
		}
	}

	if ($itemId > 0) {
		$pdo->prepare(
			'UPDATE `control_items` SET `caption` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ?, `show_anyway` = 0 WHERE `id` = ?'
		)->execute(array('epc_oms_orders_cp', $orderSlot, '#0f766e', 'fas fa-columns', $itemId));
	} else {
		$pdo->prepare(
			'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
			 VALUES (?, ?, ?, \'\', ?, ?, ?, \'\', 0)'
		)->execute(array($shopGroupId, 'epc_oms_orders_cp', $ordersUrl, $orderSlot, '#0f766e', 'fas fa-columns'));
		$itemId = (int) $pdo->lastInsertId();
	}

	$removed = epc_cp_oms_menu_cleanup($pdo, $shopGroupId, $itemId);

	return array(
		'shop_group' => $shopGroupId,
		'shop_orders_item' => $itemId,
		'order' => $orderSlot,
		'removed' => $removed,
	);
}

/**
 * Remove separate Shop sidebar rows for order items / statuses (routes stay published).
 * Keeps the single OMS · Orders item (+ carts / quotes / SAO if present).
 */
function epc_cp_oms_menu_cleanup(PDO $pdo, $shopGroupId, $keepOrdersItemId)
{
	$removed = 0;
	$keepOrdersItemId = (int) $keepOrdersItemId;
	$shopGroupId = (int) $shopGroupId;

	// Remove sidebar rows for statuses / items list pages (keep deep routes published).
	$st = $pdo->query(
		"SELECT `id`, `url` FROM `control_items`
		 WHERE `url` LIKE '%/shop/orders/statuses%'
		    OR (`url` LIKE '%/shop/orders/items%' AND `url` NOT LIKE '%/shop/orders/items/%')"
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$id = (int) $row['id'];
		$url = preg_replace('#\?.*$#', '', (string) $row['url']);
		if ($id === $keepOrdersItemId) {
			continue;
		}
		if (
			preg_match('#/shop/orders/statuses/?$#', $url)
			|| preg_match('#/shop/orders/items/?$#', $url)
		) {
			$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array($id));
			$removed++;
		}
	}

	// Align Logistics "Customer orders" label with OMS
	$pdo->prepare(
		"UPDATE `control_items` SET `caption` = 'epc_oms_orders_cp', `fontawesome_class` = 'fas fa-columns', `background_color` = '#0f766e'
		 WHERE `url` = '/<backend>/shop/orders/orders' AND `id` != ?"
	)->execute(array($keepOrdersItemId));

	return $removed;
}

function epc_cp_system_menu_hidden_url_patterns()
{
	return array(
		'/control/o-programme',
		'/control/obnovleniya',
		'/control/izmeneniya',
		'/content/usefull/changes_fc',
		'changes_fc.php',
		'/version_control/about_program',
		'/version_control/updates',
	);
}

/**
 * English/Russian sidebar labels for legacy SYSTEM items to remove.
 *
 * @return array<int, string>
 */
function epc_cp_system_menu_hidden_labels()
{
	return array(
		'about program',
		'docpart changes',
		'updates',
		'история изменений',
		'изменения docpart',
		'изменения',
		'о программе',
		'обновления',
	);
}

function epc_cp_system_menu_item_hidden($url, $caption = '')
{
	$url = strtolower(preg_replace('#\?.*$#', '', (string) $url));
	foreach (epc_cp_system_menu_hidden_url_patterns() as $pat) {
		if ($pat !== '' && strpos($url, strtolower($pat)) !== false) {
			return true;
		}
	}
	$label = strtolower(trim((string) $caption));
	if ($label !== '') {
		foreach (epc_cp_system_menu_hidden_labels() as $needle) {
			if ($needle !== '' && $label === $needle) {
				return true;
			}
		}
	}
	return false;
}

/**
 * Resolve translated sidebar label for a control_items.caption key/id.
 */
function epc_cp_system_menu_item_label(PDO $pdo, $caption)
{
	$caption = trim((string) $caption);
	if ($caption === '') {
		return '';
	}
	if (preg_match('/^[A-Za-z0-9_]+$/', $caption)) {
		$st = $pdo->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1');
		$st->execute(array($caption, 'en'));
		$val = trim((string) $st->fetchColumn());
		if ($val !== '') {
			return $val;
		}
	}
	if (ctype_digit($caption)) {
		$st = $pdo->prepare(
			'SELECT t.`value` FROM `lang_text_strings_translation` t
			 INNER JOIN `lang_text_strings` s ON s.`str_key` = t.`str_key`
			 WHERE s.`id` = ? AND t.`lang_code` = ? LIMIT 1'
		);
		$st->execute(array((int) $caption, 'en'));
		$val = trim((string) $st->fetchColumn());
		if ($val !== '') {
			return $val;
		}
	}
	return $caption;
}

/**
 * Delete legacy SYSTEM sidebar items (About program, Docpart changes, Updates).
 *
 * @return array{removed:int,items:array<int,array{id:int,url:string,caption:string,label:string}>}
 */
function epc_cp_system_menu_cleanup(PDO $pdo)
{
	$removed = array();
	$st = $pdo->query('SELECT `id`, `url`, `caption` FROM `control_items` ORDER BY `id` ASC');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$id = (int) $row['id'];
		$url = (string) ($row['url'] ?? '');
		$caption = (string) ($row['caption'] ?? '');
		$label = epc_cp_system_menu_item_label($pdo, $caption);
		if (!epc_cp_system_menu_item_hidden($url, $label)) {
			continue;
		}
		$pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?')->execute(array($id));
		$removed[] = array(
			'id' => $id,
			'url' => $url,
			'caption' => $caption,
			'label' => $label,
		);
	}
	return array(
		'removed' => count($removed),
		'items' => $removed,
	);
}
