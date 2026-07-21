<?php
/**
 * Dashboard shortcuts — per-user pins for CP + ERP homes.
 * Users add/remove from a catalogue (or custom URL); persisted in epc_user_shortcuts.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_shortcuts_ensure_schema')) {
	function epc_shortcuts_ensure_schema(PDO $db): void
	{
		static $done = false;
		if ($done) {
			return;
		}
		$db->exec("CREATE TABLE IF NOT EXISTS `epc_user_shortcuts` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`company_id` int(11) NOT NULL DEFAULT 0,
			`user_id` int(11) NOT NULL DEFAULT 0,
			`surface` varchar(16) NOT NULL DEFAULT 'both',
			`shortcut_key` varchar(64) NOT NULL DEFAULT '',
			`label` varchar(100) NOT NULL DEFAULT '',
			`icon_class` varchar(100) NOT NULL DEFAULT 'fa fa-star',
			`icon_color` varchar(20) NOT NULL DEFAULT '#3498db',
			`target_url` varchar(500) NOT NULL DEFAULT '',
			`target_tab` varchar(50) NOT NULL DEFAULT '',
			`sort_order` int(11) NOT NULL DEFAULT 0,
			`is_pinned` tinyint(1) NOT NULL DEFAULT 1,
			`time_created` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `x_user` (`user_id`),
			KEY `x_user_surface` (`user_id`, `surface`),
			KEY `x_company` (`company_id`),
			KEY `x_sort` (`sort_order`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User dashboard shortcuts'");

		// Upgrade older installs that lack surface / shortcut_key.
		try {
			$cols = array();
			$q = $db->query('SHOW COLUMNS FROM `epc_user_shortcuts`');
			while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
				$cols[strtolower((string) ($r['Field'] ?? ''))] = true;
			}
			if (empty($cols['surface'])) {
				$db->exec("ALTER TABLE `epc_user_shortcuts` ADD COLUMN `surface` varchar(16) NOT NULL DEFAULT 'both' AFTER `user_id`");
			}
			if (empty($cols['shortcut_key'])) {
				$db->exec("ALTER TABLE `epc_user_shortcuts` ADD COLUMN `shortcut_key` varchar(64) NOT NULL DEFAULT '' AFTER `surface`");
			}
		} catch (Throwable $e) {
		}
		$done = true;
	}

	function epc_shortcuts_user_id(): int
	{
		if (function_exists('epc_erp_admin_id')) {
			$uid = (int) epc_erp_admin_id();
			if ($uid > 0) {
				return $uid;
			}
		}
		if (isset($_SESSION['user_id'])) {
			return (int) $_SESSION['user_id'];
		}
		if (isset($_SESSION['admin_id'])) {
			return (int) $_SESSION['admin_id'];
		}
		return 0;
	}

	function epc_shortcuts_list(PDO $db, int $userId): array
	{
		epc_shortcuts_ensure_schema($db);
		$stmt = $db->prepare('SELECT * FROM `epc_user_shortcuts` WHERE `user_id` = ? ORDER BY `sort_order` ASC, `time_created` ASC');
		$stmt->execute(array($userId));
		return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}

	/**
	 * @param string $surface cp|erp|both
	 */
	function epc_shortcuts_list_for_surface(PDO $db, int $userId, string $surface): array
	{
		epc_shortcuts_ensure_schema($db);
		$surface = in_array($surface, array('cp', 'erp', 'both'), true) ? $surface : 'both';
		$stmt = $db->prepare(
			"SELECT * FROM `epc_user_shortcuts`
			 WHERE `user_id` = ? AND (`surface` = ? OR `surface` = 'both')
			 ORDER BY `sort_order` ASC, `time_created` ASC"
		);
		$stmt->execute(array($userId, $surface));
		return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}

	function epc_shortcuts_add(PDO $db, array $data): int
	{
		epc_shortcuts_ensure_schema($db);
		$userId = (int) ($data['user_id'] ?? 0);
		if ($userId <= 0) {
			return 0;
		}
		$key = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($data['shortcut_key'] ?? '')));
		$surface = (string) ($data['surface'] ?? 'both');
		if (!in_array($surface, array('cp', 'erp', 'both'), true)) {
			$surface = 'both';
		}
		// Prevent duplicate catalogue keys on the same surface for one user.
		if ($key !== '') {
			$chk = $db->prepare(
				"SELECT `id` FROM `epc_user_shortcuts`
				 WHERE `user_id` = ? AND `shortcut_key` = ? AND (`surface` = ? OR `surface` = 'both') LIMIT 1"
			);
			$chk->execute(array($userId, $key, $surface));
			$existing = (int) $chk->fetchColumn();
			if ($existing > 0) {
				return $existing;
			}
		}

		$stmt = $db->prepare('SELECT MAX(`sort_order`) FROM `epc_user_shortcuts` WHERE `user_id` = ?');
		$stmt->execute(array($userId));
		$maxSort = (int) $stmt->fetchColumn() + 1;

		$icon = trim((string) ($data['icon_class'] ?? 'fa fa-star'));
		if ($icon !== '' && strpos($icon, 'fa ') !== 0 && strpos($icon, 'fa-') === 0) {
			$icon = 'fa ' . $icon;
		}
		$color = trim((string) ($data['icon_color'] ?? '#3498db'));
		if ($color === '' || $color[0] !== '#') {
			$color = '#3498db';
		}

		$stmt = $db->prepare(
			'INSERT INTO `epc_user_shortcuts`
			 (`company_id`,`user_id`,`surface`,`shortcut_key`,`label`,`icon_class`,`icon_color`,`target_url`,`target_tab`,`sort_order`,`time_created`)
			 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
		);
		$stmt->execute(array(
			(int) ($data['company_id'] ?? 0),
			$userId,
			$surface,
			$key,
			mb_substr(trim((string) ($data['label'] ?? 'Shortcut')), 0, 100, 'UTF-8'),
			mb_substr($icon, 0, 100, 'UTF-8'),
			mb_substr($color, 0, 20, 'UTF-8'),
			mb_substr(trim((string) ($data['target_url'] ?? '')), 0, 500, 'UTF-8'),
			mb_substr(trim((string) ($data['target_tab'] ?? '')), 0, 50, 'UTF-8'),
			$maxSort,
			time(),
		));
		return (int) $db->lastInsertId();
	}

	function epc_shortcuts_reorder(PDO $db, int $userId, array $ids): bool
	{
		epc_shortcuts_ensure_schema($db);
		foreach ($ids as $order => $id) {
			$db->prepare('UPDATE `epc_user_shortcuts` SET `sort_order` = ? WHERE `id` = ? AND `user_id` = ?')
				->execute(array((int) $order, (int) $id, $userId));
		}
		return true;
	}

	function epc_shortcuts_delete(PDO $db, int $id, int $userId): bool
	{
		epc_shortcuts_ensure_schema($db);
		$db->prepare('DELETE FROM `epc_user_shortcuts` WHERE `id` = ? AND `user_id` = ?')->execute(array($id, $userId));
		return true;
	}

	function epc_shortcuts_delete_by_key(PDO $db, int $userId, string $key, string $surface = ''): bool
	{
		epc_shortcuts_ensure_schema($db);
		$key = preg_replace('/[^a-z0-9_\-]/', '', strtolower($key));
		if ($key === '' || $userId <= 0) {
			return false;
		}
		if ($surface !== '' && in_array($surface, array('cp', 'erp'), true)) {
			$db->prepare(
				"DELETE FROM `epc_user_shortcuts`
				 WHERE `user_id` = ? AND `shortcut_key` = ? AND (`surface` = ? OR `surface` = 'both')"
			)->execute(array($userId, $key, $surface));
		} else {
			$db->prepare('DELETE FROM `epc_user_shortcuts` WHERE `user_id` = ? AND `shortcut_key` = ?')
				->execute(array($userId, $key));
		}
		return true;
	}

	function epc_shortcuts_reset(PDO $db, int $userId, string $surface = ''): bool
	{
		epc_shortcuts_ensure_schema($db);
		if ($userId <= 0) {
			return false;
		}
		if ($surface !== '' && in_array($surface, array('cp', 'erp'), true)) {
			$db->prepare("DELETE FROM `epc_user_shortcuts` WHERE `user_id` = ? AND (`surface` = ? OR `surface` = 'both')")
				->execute(array($userId, $surface));
		} else {
			$db->prepare('DELETE FROM `epc_user_shortcuts` WHERE `user_id` = ?')->execute(array($userId));
		}
		return true;
	}

	/**
	 * Catalogue of pin-able shortcuts for CP control dashboard.
	 *
	 * @return array<string, array{key:string,label:string,icon:string,color:string,url:string,tone?:string}>
	 */
	function epc_shortcuts_catalog_cp(string $base): array
	{
		$base = rtrim($base, '/');
		$items = array(
			'orders' => array('label' => 'Orders (OMS)', 'icon' => 'fa-shopping-cart', 'color' => '#dc2626', 'url' => $base . '/shop/orders/orders', 'tone' => 'red'),
			'catalogue' => array('label' => 'Catalogue', 'icon' => 'fa-th-large', 'color' => '#2563eb', 'url' => $base . '/shop/catalogue/products', 'tone' => 'blue'),
			'sku_media' => array('label' => 'SKU photos & specs', 'icon' => 'fa-picture-o', 'color' => '#0f766e', 'url' => $base . '/shop/catalogue/sku_media', 'tone' => 'teal'),
			'prices' => array('label' => 'Prices', 'icon' => 'fa-tags', 'color' => '#d97706', 'url' => $base . '/shop/prices', 'tone' => 'amber'),
			'clients' => array('label' => 'Customers', 'icon' => 'fa-address-book', 'color' => '#0d9488', 'url' => $base . '/shop/customer_mgmt/customer_mgmt', 'tone' => 'teal'),
			'warehouses' => array('label' => 'Warehouses', 'icon' => 'fa-building', 'color' => '#7c3aed', 'url' => $base . '/shop/logistics/storages', 'tone' => 'violet'),
			'stock' => array('label' => 'Stock', 'icon' => 'fa-cubes', 'color' => '#059669', 'url' => $base . '/shop/logistics/stock', 'tone' => 'emerald'),
			'procurement' => array('label' => 'Procurement', 'icon' => 'fa-truck', 'color' => '#4f46e5', 'url' => $base . '/shop/procurement/procurement', 'tone' => 'indigo'),
			'erp' => array('label' => 'ERP finance', 'icon' => 'fa-university', 'color' => '#e11d48', 'url' => $base . '/shop/finance/erp?epc_erp_shell=1', 'tone' => 'rose'),
			'insights' => array('label' => 'Insights', 'icon' => 'fa-lightbulb-o', 'color' => '#0f766e', 'url' => $base . '/shop/finance/erp?epc_erp_shell=1&area=overview&tab=dashboard#epc-insights', 'tone' => 'teal'),
			'documents' => array('label' => 'Documents', 'icon' => 'fa-file-text-o', 'color' => '#64748b', 'url' => $base . '/shop/document_control/document_control', 'tone' => 'slate'),
			'pos' => array('label' => 'POS terminal', 'icon' => 'fa-credit-card', 'color' => '#db2777', 'url' => $base . '/shop/pos/terminal', 'tone' => 'rose'),
			'multivendor' => array('label' => 'Multivendor', 'icon' => 'fa-handshake-o', 'color' => '#0891b2', 'url' => $base . '/shop/prices/multivendor', 'tone' => 'teal'),
			'crosses' => array('label' => 'Crosses', 'icon' => 'fa-exchange', 'color' => '#2563eb', 'url' => $base . '/shop/crosses', 'tone' => 'blue'),
			'ai_chats' => array('label' => 'AI chats', 'icon' => 'fa-comments', 'color' => '#7c3aed', 'url' => $base . '/shop/parts_agent_chats', 'tone' => 'violet'),
			'settings' => array('label' => 'Settings', 'icon' => 'fa-cog', 'color' => '#475569', 'url' => $base . '/control/portal/industry_settings', 'tone' => 'slate'),
			'brochure' => array('label' => 'CP brochure', 'icon' => 'fa-book', 'color' => '#4f46e5', 'url' => $base . '/control/cp_brochure', 'tone' => 'indigo'),
			'accessories' => array('label' => 'Accessories', 'icon' => 'fa-puzzle-piece', 'color' => '#7c3aed', 'url' => $base . '/shop/accessories', 'tone' => 'violet'),
		);
		$out = array();
		foreach ($items as $key => $row) {
			$row['key'] = $key;
			$row['icon'] = (strpos($row['icon'], 'fa ') === 0) ? $row['icon'] : ('fa ' . $row['icon']);
			$out[$key] = $row;
		}
		return $out;
	}

	/**
	 * Catalogue for Super CP / BOC Operations Command Center (ecomae.com/cp).
	 *
	 * @return array<string, array{key:string,label:string,icon:string,color:string,url:string,tone?:string}>
	 */
	function epc_shortcuts_catalog_boc(string $base): array
	{
		$base = rtrim($base, '/');
		$items = array(
			'tenant_hub' => array('label' => 'Tenant hub', 'icon' => 'fa-sitemap', 'color' => '#dc2626', 'url' => $base . '/shop/tenant_hub/tenant_hub', 'tone' => 'red'),
			'onboard' => array('label' => 'Onboard client', 'icon' => 'fa-rocket', 'color' => '#0a0a0a', 'url' => $base . '/shop/tenant_hub/tenant_hub?tab=onboard', 'tone' => 'black'),
			'health' => array('label' => 'Health checkup', 'icon' => 'fa-stethoscope', 'color' => '#059669', 'url' => $base . '/control/portal/epc_platform_health_checkup', 'tone' => 'emerald'),
			'channels' => array('label' => 'Channels & OMS', 'icon' => 'fa-exchange', 'color' => '#2563eb', 'url' => $base . '/control/portal/epc_boc_channel_control', 'tone' => 'blue'),
			'governance' => array('label' => 'Governance', 'icon' => 'fa-gavel', 'color' => '#7c3aed', 'url' => $base . '/control/portal/epc_platform_governance', 'tone' => 'violet'),
			'audit' => array('label' => 'Audit log', 'icon' => 'fa-history', 'color' => '#475569', 'url' => $base . '/control/portal/epc_boc_audit_log', 'tone' => 'slate'),
			'industry' => array('label' => 'Industry packs', 'icon' => 'fa-industry', 'color' => '#d97706', 'url' => $base . '/control/portal/industry_settings', 'tone' => 'amber'),
			'brochure' => array('label' => 'CP brochure', 'icon' => 'fa-book', 'color' => '#4f46e5', 'url' => $base . '/control/cp_brochure', 'tone' => 'indigo'),
			'platform_erp' => array('label' => 'Platform ERP', 'icon' => 'fa-university', 'color' => '#e11d48', 'url' => $base . '/platform-erp/', 'tone' => 'rose'),
			'deploy' => array('label' => 'Deploy & DNS', 'icon' => 'fa-cloud-upload', 'color' => '#0891b2', 'url' => $base . '/shop/tenant_hub/tenant_hub?tab=deploy', 'tone' => 'teal'),
			'demos' => array('label' => 'Demo tenants', 'icon' => 'fa-flask', 'color' => '#db2777', 'url' => $base . '/shop/tenant_hub/tenant_hub?tab=demos', 'tone' => 'pink'),
			'features' => array('label' => 'Feature flags', 'icon' => 'fa-toggle-on', 'color' => '#0d9488', 'url' => $base . '/shop/tenant_hub/tenant_hub?tab=features', 'tone' => 'teal'),
		);
		$out = array();
		foreach ($items as $key => $row) {
			$row['key'] = $key;
			$row['icon'] = (strpos($row['icon'], 'fa ') === 0) ? $row['icon'] : ('fa ' . $row['icon']);
			$out[$key] = $row;
		}
		return $out;
	}

	/**
	 * Catalogue for ERP dashboard (keys aligned with NetSuite quick catalog).
	 *
	 * @param callable $urlFn function(string $tab, string $area): string
	 * @return array<string, array{key:string,label:string,icon:string,color:string,url:string,tone?:string}>
	 */
	function epc_shortcuts_catalog_erp(callable $urlFn): array
	{
		$items = array(
			'sales_orders' => array('label' => 'Sales orders', 'icon' => 'fa-shopping-cart', 'color' => '#2563eb', 'url' => $urlFn('sales_orders', 'sales'), 'tone' => 'qa-blue'),
			'purchase_orders' => array('label' => 'Purchase orders', 'icon' => 'fa-clipboard', 'color' => '#4f46e5', 'url' => $urlFn('purchase_orders', 'purchasing'), 'tone' => 'qa-indigo'),
			'inventory' => array('label' => 'Inventory', 'icon' => 'fa-cubes', 'color' => '#d97706', 'url' => $urlFn('inventory', 'operations'), 'tone' => 'qa-amber'),
			'customers' => array('label' => 'Customers', 'icon' => 'fa-user-plus', 'color' => '#db2777', 'url' => $urlFn('receivables', 'sales'), 'tone' => 'qa-pink'),
			'vendors' => array('label' => 'Vendors', 'icon' => 'fa-truck', 'color' => '#0d9488', 'url' => $urlFn('payables', 'purchasing'), 'tone' => 'qa-teal'),
			'cash_bank' => array('label' => 'Cash & bank', 'icon' => 'fa-money', 'color' => '#059669', 'url' => $urlFn('cash_bank', 'finance'), 'tone' => 'qa-green'),
			'gl' => array('label' => 'General ledger', 'icon' => 'fa-book', 'color' => '#475569', 'url' => $urlFn('gl', 'finance'), 'tone' => 'qa-slate'),
			'vat_return' => array('label' => 'VAT return', 'icon' => 'fa-percent', 'color' => '#b45309', 'url' => $urlFn('vat_return', 'finance'), 'tone' => 'qa-rust'),
			'pl' => array('label' => 'Profit & loss', 'icon' => 'fa-line-chart', 'color' => '#4f46e5', 'url' => $urlFn('pl', 'insights'), 'tone' => 'qa-indigo'),
			'insights' => array('label' => 'Insights hub', 'icon' => 'fa-lightbulb-o', 'color' => '#0f766e', 'url' => $urlFn('dashboard', 'overview') . '#epc-insights', 'tone' => 'qa-teal'),
			'receivables' => array('label' => 'Receivables', 'icon' => 'fa-users', 'color' => '#2563eb', 'url' => $urlFn('receivables', 'sales'), 'tone' => 'qa-blue'),
			'payables' => array('label' => 'Payables', 'icon' => 'fa-truck', 'color' => '#0d9488', 'url' => $urlFn('payables', 'purchasing'), 'tone' => 'qa-teal'),
			'crm' => array('label' => 'CRM pipeline', 'icon' => 'fa-handshake-o', 'color' => '#db2777', 'url' => $urlFn('crm', 'sales'), 'tone' => 'qa-pink'),
			'coa' => array('label' => 'Chart of accounts', 'icon' => 'fa-list', 'color' => '#64748b', 'url' => $urlFn('coa', 'finance'), 'tone' => 'qa-slate'),
			'balance_sheet' => array('label' => 'Balance sheet', 'icon' => 'fa-balance-scale', 'color' => '#d97706', 'url' => $urlFn('balance_sheet', 'insights'), 'tone' => 'qa-amber'),
			'hr' => array('label' => 'HR', 'icon' => 'fa-users', 'color' => '#2563eb', 'url' => $urlFn('hr', 'people'), 'tone' => 'qa-blue'),
			'payroll' => array('label' => 'Payroll', 'icon' => 'fa-credit-card', 'color' => '#059669', 'url' => $urlFn('payroll', 'people'), 'tone' => 'qa-green'),
			'staff' => array('label' => 'Staff', 'icon' => 'fa-id-badge', 'color' => '#475569', 'url' => $urlFn('staff', 'people'), 'tone' => 'qa-slate'),
			'fulfilment' => array('label' => 'Fulfilment', 'icon' => 'fa-truck', 'color' => '#d97706', 'url' => $urlFn('fulfilment', 'sales'), 'tone' => 'qa-amber'),
			'bank_recon' => array('label' => 'Bank reconcile', 'icon' => 'fa-university', 'color' => '#b45309', 'url' => $urlFn('bank_recon', 'finance'), 'tone' => 'qa-rust'),
		);
		$out = array();
		foreach ($items as $key => $row) {
			$row['key'] = $key;
			$row['icon'] = (strpos($row['icon'], 'fa ') === 0) ? $row['icon'] : ('fa ' . $row['icon']);
			$out[$key] = $row;
		}
		return $out;
	}

	/**
	 * Seed defaults once when the user has no shortcuts for this surface.
	 *
	 * @param array<int, string> $defaultKeys
	 * @param array<string, array> $catalog
	 */
	function epc_shortcuts_seed_defaults(PDO $db, int $userId, string $surface, array $defaultKeys, array $catalog): void
	{
		if ($userId <= 0) {
			return;
		}
		$existing = epc_shortcuts_list_for_surface($db, $userId, $surface);
		if (!empty($existing)) {
			return;
		}
		foreach ($defaultKeys as $key) {
			if (!isset($catalog[$key])) {
				continue;
			}
			$item = $catalog[$key];
			epc_shortcuts_add($db, array(
				'user_id' => $userId,
				'surface' => $surface,
				'shortcut_key' => $key,
				'label' => $item['label'],
				'icon_class' => $item['icon'],
				'icon_color' => $item['color'],
				'target_url' => $item['url'],
			));
		}
	}

	/**
	 * Normalize saved rows into dashboard tile shape.
	 *
	 * @return array<int, array{id:int,key:string,label:string,icon:string,color:string,url:string,tone:string}>
	 */
	function epc_shortcuts_as_tiles(array $rows): array
	{
		$tones = array('red', 'black', 'crimson', 'stone', 'blue', 'teal', 'amber', 'violet', 'indigo', 'emerald', 'rose', 'slate');
		$out = array();
		$i = 0;
		foreach ($rows as $row) {
			$icon = trim((string) ($row['icon_class'] ?? 'fa fa-star'));
			if (strpos($icon, 'fa ') !== 0 && strpos($icon, 'fa-') === 0) {
				$icon = 'fa ' . $icon;
			}
			$iconBare = preg_replace('/^fa\s+/', '', $icon);
			$key = (string) ($row['shortcut_key'] ?? '');
			$out[] = array(
				'id' => (int) ($row['id'] ?? 0),
				'key' => $key,
				'label' => (string) ($row['label'] ?? 'Shortcut'),
				'icon' => $iconBare,
				'icon_class' => $icon,
				'color' => (string) ($row['icon_color'] ?? '#3498db'),
				'url' => (string) ($row['target_url'] ?? '#'),
				'tone' => $tones[$i % count($tones)],
			);
			$i++;
		}
		return $out;
	}
}
