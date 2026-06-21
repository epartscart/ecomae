<?php
/**
 * Super CP platform modules — customer board, price configs, info blocks, communication.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_portal.php';
require_once __DIR__ . '/epc_portal_tenant_control.php';

function epc_scp_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_scp_backend(): string
{
	global $DP_Config;
	return trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
}

function epc_scp_platform_ensure_schema(PDO $pdo): void
{
	epc_portal_db_ensure($pdo);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_platform_price_configs` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`name` VARCHAR(120) NOT NULL,
			`scope` VARCHAR(16) NOT NULL DEFAULT \'platform\',
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`client_type` VARCHAR(32) NOT NULL DEFAULT \'all\',
			`client_ref` VARCHAR(120) NOT NULL DEFAULT \'\',
			`markup_percent` DECIMAL(8,2) NOT NULL DEFAULT 0,
			`markup_fixed` DECIMAL(12,4) NOT NULL DEFAULT 0,
			`currency` VARCHAR(8) NOT NULL DEFAULT \'AED\',
			`priority` INT NOT NULL DEFAULT 100,
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`notes` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			KEY `scope_site` (`scope`, `site_key`),
			KEY `active_priority` (`active`, `priority`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_platform_info_blocks` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`block_key` VARCHAR(64) NOT NULL,
			`title` VARCHAR(200) NOT NULL,
			`scope` VARCHAR(16) NOT NULL DEFAULT \'platform\',
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`placement` VARCHAR(64) NOT NULL DEFAULT \'homepage\',
			`content_html` MEDIUMTEXT NULL,
			`locale` VARCHAR(8) NOT NULL DEFAULT \'en\',
			`active` TINYINT(1) NOT NULL DEFAULT 1,
			`sort_order` INT NOT NULL DEFAULT 0,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `block_unique` (`block_key`, `scope`, `site_key`, `locale`),
			KEY `placement_active` (`placement`, `active`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_platform_comm_settings` (
			`setting_key` VARCHAR(64) NOT NULL PRIMARY KEY,
			`setting_value` TEXT NULL,
			`updated_at` INT NOT NULL DEFAULT 0
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_platform_internal_tasks` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`title` VARCHAR(200) NOT NULL,
			`description` TEXT NULL,
			`assigned_to` INT NOT NULL DEFAULT 0,
			`assigned_email` VARCHAR(120) NOT NULL DEFAULT \'\',
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`category` VARCHAR(32) NOT NULL DEFAULT \'support\',
			`status` VARCHAR(24) NOT NULL DEFAULT \'open\',
			`priority` VARCHAR(16) NOT NULL DEFAULT \'normal\',
			`due_at` INT NOT NULL DEFAULT 0,
			`created_by` INT NOT NULL DEFAULT 0,
			`created_at` INT NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			KEY `status_priority` (`status`, `priority`),
			KEY `assigned_email` (`assigned_email`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

function epc_scp_price_client_types(): array
{
	return array(
		'all' => 'All clients',
		'catalog' => 'Built-in catalogue',
		'api' => 'API / integration',
		'channel' => 'Sales channel',
		'price_list' => 'Price list',
	);
}

function epc_scp_info_placements(): array
{
	return array(
		'homepage' => 'Storefront homepage',
		'footer' => 'Storefront footer',
		'checkout' => 'Checkout sidebar',
		'cp_notice' => 'CP dashboard notice',
		'product_list' => 'Product listing banner',
		'login' => 'Login / register page',
	);
}

function epc_scp_task_categories(): array
{
	return array(
		'onboarding' => 'Onboarding',
		'support' => 'Support',
		'billing' => 'Billing',
		'pricing' => 'Pricing',
		'content' => 'Content',
		'other' => 'Other',
	);
}

function epc_scp_task_statuses(): array
{
	return array(
		'open' => 'Open',
		'in_progress' => 'In progress',
		'done' => 'Done',
		'cancelled' => 'Cancelled',
	);
}

function epc_scp_task_priorities(): array
{
	return array(
		'low' => 'Low',
		'normal' => 'Normal',
		'high' => 'High',
		'urgent' => 'Urgent',
	);
}

function epc_scp_default_comm_settings(): array
{
	return array(
		'notify_from_name' => 'ECOM AE Platform',
		'notify_from_email' => 'noreply@ecomae.com',
		'notify_reply_to' => 'support@ecomae.com',
		'notify_tenant_onboard' => '1',
		'notify_tenant_dns_live' => '1',
		'notify_demo_expiry' => '1',
		'notify_task_assigned' => '1',
		'notify_daily_digest' => '0',
		'digest_hour_utc' => '6',
	);
}

function epc_scp_comm_settings_get(PDO $pdo): array
{
	epc_scp_platform_ensure_schema($pdo);
	$defaults = epc_scp_default_comm_settings();
	$st = $pdo->query('SELECT `setting_key`, `setting_value` FROM `epc_platform_comm_settings`');
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$defaults[(string) $row['setting_key']] = (string) $row['setting_value'];
	}
	return $defaults;
}

function epc_scp_comm_settings_save(PDO $pdo, array $data): void
{
	epc_scp_platform_ensure_schema($pdo);
	$allowed = array_keys(epc_scp_default_comm_settings());
	$now = time();
	$ins = $pdo->prepare(
		'INSERT INTO `epc_platform_comm_settings` (`setting_key`, `setting_value`, `updated_at`)
		 VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `updated_at` = VALUES(`updated_at`)'
	);
	foreach ($allowed as $key) {
		if (!array_key_exists($key, $data)) {
			continue;
		}
		$val = (string) $data[$key];
		if (strpos($key, 'notify_') === 0) {
			$val = !empty($data[$key]) ? '1' : '0';
		}
		$ins->execute(array($key, $val, $now));
	}
}

function epc_scp_price_configs_list(PDO $pdo): array
{
	epc_scp_platform_ensure_schema($pdo);
	return $pdo->query(
		'SELECT * FROM `epc_platform_price_configs` ORDER BY `active` DESC, `priority` ASC, `name` ASC'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_scp_price_config_save(PDO $pdo, array $data, int $id = 0): array
{
	epc_scp_platform_ensure_schema($pdo);
	$name = trim((string) ($data['name'] ?? ''));
	if ($name === '') {
		return array('ok' => false, 'message' => 'Name is required');
	}
	$scope = in_array((string) ($data['scope'] ?? ''), array('platform', 'tenant'), true) ? (string) $data['scope'] : 'platform';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['site_key'] ?? '')));
	$clientType = (string) ($data['client_type'] ?? 'all');
	if (!isset(epc_scp_price_client_types()[$clientType])) {
		$clientType = 'all';
	}
	$now = time();
	$fields = array(
		$name,
		$scope,
		$siteKey,
		$clientType,
		trim((string) ($data['client_ref'] ?? '')),
		(float) ($data['markup_percent'] ?? 0),
		(float) ($data['markup_fixed'] ?? 0),
		strtoupper(substr(trim((string) ($data['currency'] ?? 'AED')), 0, 8)),
		max(1, (int) ($data['priority'] ?? 100)),
		!empty($data['active']) ? 1 : 0,
		trim((string) ($data['notes'] ?? '')),
		$now,
	);
	if ($id > 0) {
		$fields[] = $id;
		$pdo->prepare(
			'UPDATE `epc_platform_price_configs` SET
			 `name` = ?, `scope` = ?, `site_key` = ?, `client_type` = ?, `client_ref` = ?,
			 `markup_percent` = ?, `markup_fixed` = ?, `currency` = ?, `priority` = ?,
			 `active` = ?, `notes` = ?, `updated_at` = ? WHERE `id` = ?'
		)->execute($fields);
		return array('ok' => true, 'id' => $id);
	}
	$fields[] = $now;
	$pdo->prepare(
		'INSERT INTO `epc_platform_price_configs`
		 (`name`, `scope`, `site_key`, `client_type`, `client_ref`, `markup_percent`, `markup_fixed`,
		  `currency`, `priority`, `active`, `notes`, `updated_at`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute($fields);
	return array('ok' => true, 'id' => (int) $pdo->lastInsertId());
}

function epc_scp_price_config_delete(PDO $pdo, int $id): bool
{
	epc_scp_platform_ensure_schema($pdo);
	return $pdo->prepare('DELETE FROM `epc_platform_price_configs` WHERE `id` = ?')->execute(array($id));
}

function epc_scp_info_blocks_list(PDO $pdo, string $placement = ''): array
{
	epc_scp_platform_ensure_schema($pdo);
	if ($placement !== '') {
		$st = $pdo->prepare(
			'SELECT * FROM `epc_platform_info_blocks` WHERE `placement` = ? ORDER BY `sort_order` ASC, `title` ASC'
		);
		$st->execute(array($placement));
		return $st->fetchAll(PDO::FETCH_ASSOC);
	}
	return $pdo->query(
		'SELECT * FROM `epc_platform_info_blocks` ORDER BY `placement` ASC, `sort_order` ASC, `title` ASC'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_scp_info_block_save(PDO $pdo, array $data, int $id = 0): array
{
	epc_scp_platform_ensure_schema($pdo);
	$key = preg_replace('/[^a-z0-9_-]/', '', strtolower(trim((string) ($data['block_key'] ?? ''))));
	$title = trim((string) ($data['title'] ?? ''));
	if ($key === '' || $title === '') {
		return array('ok' => false, 'message' => 'Block key and title are required');
	}
	$scope = in_array((string) ($data['scope'] ?? ''), array('platform', 'tenant'), true) ? (string) $data['scope'] : 'platform';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['site_key'] ?? '')));
	$placement = (string) ($data['placement'] ?? 'homepage');
	if (!isset(epc_scp_info_placements()[$placement])) {
		$placement = 'homepage';
	}
	$locale = substr(trim((string) ($data['locale'] ?? 'en')), 0, 8);
	$now = time();
	$row = array(
		$key,
		$title,
		$scope,
		$siteKey,
		$placement,
		(string) ($data['content_html'] ?? ''),
		$locale !== '' ? $locale : 'en',
		!empty($data['active']) ? 1 : 0,
		(int) ($data['sort_order'] ?? 0),
		$now,
	);
	if ($id > 0) {
		$row[] = $id;
		$pdo->prepare(
			'UPDATE `epc_platform_info_blocks` SET
			 `block_key` = ?, `title` = ?, `scope` = ?, `site_key` = ?, `placement` = ?,
			 `content_html` = ?, `locale` = ?, `active` = ?, `sort_order` = ?, `updated_at` = ? WHERE `id` = ?'
		)->execute($row);
		return array('ok' => true, 'id' => $id);
	}
	$row[] = $now;
	try {
		$pdo->prepare(
			'INSERT INTO `epc_platform_info_blocks`
			 (`block_key`, `title`, `scope`, `site_key`, `placement`, `content_html`, `locale`, `active`, `sort_order`, `updated_at`, `created_at`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
		)->execute($row);
	} catch (Exception $e) {
		return array('ok' => false, 'message' => 'Duplicate block key for this scope/locale');
	}
	return array('ok' => true, 'id' => (int) $pdo->lastInsertId());
}

function epc_scp_info_block_delete(PDO $pdo, int $id): bool
{
	epc_scp_platform_ensure_schema($pdo);
	return $pdo->prepare('DELETE FROM `epc_platform_info_blocks` WHERE `id` = ?')->execute(array($id));
}

function epc_scp_tasks_list(PDO $pdo, string $statusFilter = ''): array
{
	epc_scp_platform_ensure_schema($pdo);
	if ($statusFilter !== '' && isset(epc_scp_task_statuses()[$statusFilter])) {
		$st = $pdo->prepare(
			'SELECT * FROM `epc_platform_internal_tasks` WHERE `status` = ? ORDER BY FIELD(`priority`, \'urgent\', \'high\', \'normal\', \'low\'), `due_at` ASC, `id` DESC'
		);
		$st->execute(array($statusFilter));
		return $st->fetchAll(PDO::FETCH_ASSOC);
	}
	return $pdo->query(
		'SELECT * FROM `epc_platform_internal_tasks` ORDER BY FIELD(`status`, \'open\', \'in_progress\', \'done\', \'cancelled\'), FIELD(`priority`, \'urgent\', \'high\', \'normal\', \'low\'), `due_at` ASC, `id` DESC LIMIT 200'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_scp_task_save(PDO $pdo, array $data, int $id = 0, int $createdBy = 0): array
{
	epc_scp_platform_ensure_schema($pdo);
	$title = trim((string) ($data['title'] ?? ''));
	if ($title === '') {
		return array('ok' => false, 'message' => 'Title is required');
	}
	$category = (string) ($data['category'] ?? 'support');
	if (!isset(epc_scp_task_categories()[$category])) {
		$category = 'support';
	}
	$status = (string) ($data['status'] ?? 'open');
	if (!isset(epc_scp_task_statuses()[$status])) {
		$status = 'open';
	}
	$priority = (string) ($data['priority'] ?? 'normal');
	if (!isset(epc_scp_task_priorities()[$priority])) {
		$priority = 'normal';
	}
	$now = time();
	$row = array(
		$title,
		trim((string) ($data['description'] ?? '')),
		max(0, (int) ($data['assigned_to'] ?? 0)),
		strtolower(trim((string) ($data['assigned_email'] ?? ''))),
		preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['site_key'] ?? ''))),
		$category,
		$status,
		$priority,
		max(0, (int) ($data['due_at'] ?? 0)),
		$now,
	);
	if ($id > 0) {
		$row[] = $id;
		$pdo->prepare(
			'UPDATE `epc_platform_internal_tasks` SET
			 `title` = ?, `description` = ?, `assigned_to` = ?, `assigned_email` = ?, `site_key` = ?,
			 `category` = ?, `status` = ?, `priority` = ?, `due_at` = ?, `updated_at` = ? WHERE `id` = ?'
		)->execute($row);
		return array('ok' => true, 'id' => $id);
	}
	$row[] = max(0, $createdBy);
	$row[] = $now;
	$pdo->prepare(
		'INSERT INTO `epc_platform_internal_tasks`
		 (`title`, `description`, `assigned_to`, `assigned_email`, `site_key`, `category`, `status`, `priority`, `due_at`, `updated_at`, `created_by`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute($row);
	return array('ok' => true, 'id' => (int) $pdo->lastInsertId());
}

function epc_scp_task_delete(PDO $pdo, int $id): bool
{
	epc_scp_platform_ensure_schema($pdo);
	return $pdo->prepare('DELETE FROM `epc_platform_internal_tasks` WHERE `id` = ?')->execute(array($id));
}

function epc_scp_platform_users(PDO $pdo): array
{
	try {
		return $pdo->query(
			'SELECT u.`user_id`, u.`email`, MAX(CASE WHEN up.`data_key` = \'name\' THEN up.`data_value` END) AS fname
			 FROM `users` u
			 LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`
			 WHERE u.`user_id` > 0 GROUP BY u.`user_id`, u.`email` ORDER BY u.`email` ASC LIMIT 100'
		)->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return array();
	}
}

function epc_scp_tenant_options(PDO $pdo): array
{
	$tenants = epc_portal_tenant_control_list_all($pdo);
	$out = array();
	foreach ($tenants as $t) {
		if (empty($t['in_registry'])) {
			continue;
		}
		$key = (string) ($t['site_key'] ?? '');
		if ($key === '') {
			continue;
		}
		$out[] = array(
			'site_key' => $key,
			'label' => trim((string) ($t['trade_name'] ?? $key)) . ' (' . $key . ')',
			'hostname' => (string) ($t['hostname'] ?? ''),
			'urls' => is_array($t['urls'] ?? null) ? $t['urls'] : epc_portal_tenant_control_urls($t),
		);
	}
	return $out;
}

function epc_scp_customer_name_from_row(array $row): string
{
	$parts = array_filter(array(
		trim((string) ($row['fname'] ?? '')),
		trim((string) ($row['sname'] ?? '')),
	));
	if (count($parts) > 0) {
		return implode(' ', $parts);
	}
	$company = trim((string) ($row['company'] ?? ''));
	if ($company !== '') {
		return $company;
	}
	return trim((string) ($row['email'] ?? ''));
}

function epc_scp_customers_from_pdo(PDO $tenantPdo, array $tenantMeta, string $search, int $limit): array
{
	$limit = max(1, min(50, $limit));
	$sql = "SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_reg`,
		MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname,
		MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname,
		MAX(CASE WHEN up.`data_key` = 'company' THEN up.`data_value` END) AS company
		FROM `users` u
		LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`
		WHERE u.`user_id` > 0";
	$params = array();
	if ($search !== '') {
		$sql .= ' AND (u.`email` LIKE ? OR u.`phone` LIKE ? OR up.`data_value` LIKE ?)';
		$q = '%' . $search . '%';
		$params = array($q, $q, $q);
	}
	$sql .= ' GROUP BY u.`user_id`, u.`email`, u.`phone`, u.`time_reg` ORDER BY u.`user_id` DESC LIMIT ' . (int) $limit;
	try {
		$st = $tenantPdo->prepare($sql);
		$st->execute($params);
		$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return array();
	}
	$out = array();
	foreach ($rows as $r) {
		$urls = $tenantMeta['urls'];
		$cpBase = rtrim((string) ($urls['cp'] ?? ''), '/');
		$erpUrl = rtrim((string) ($urls['client_erp'] ?? $cpBase), '/');
		$out[] = array(
			'source' => (string) $tenantMeta['site_key'],
			'source_label' => (string) $tenantMeta['label'],
			'hostname' => (string) $tenantMeta['hostname'],
			'user_id' => (int) $r['user_id'],
			'name' => epc_scp_customer_name_from_row($r),
			'email' => (string) ($r['email'] ?? ''),
			'phone' => (string) ($r['phone'] ?? ''),
			'time_reg' => (int) ($r['time_reg'] ?? 0),
			'links' => array(
				'crm' => $cpBase !== '' ? $cpBase . '/shop/customer_mgmt/customer_mgmt?tab=customers&user_id=' . (int) $r['user_id'] : '',
				'erp' => $erpUrl !== '' ? $erpUrl . '/shop/finance/erp?epc_erp_shell=1&area=sales' : '',
				'cp' => $cpBase,
			),
		);
	}
	return $out;
}

function epc_scp_customer_board_search(PDO $platformPdo, string $search = '', string $tenantFilter = '', int $page = 1, int $perPage = 50): array
{
	$search = trim($search);
	$tenantFilter = preg_replace('/[^a-z0-9_]/', '', strtolower($tenantFilter));
	$page = max(1, $page);
	$perPage = max(10, min(100, $perPage));
	$results = array();
	$stats = array('platform' => 0, 'tenants_scanned' => 0, 'tenants_with_hits' => 0);

	$platformSql = "SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_reg`,
		MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname,
		MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname,
		MAX(CASE WHEN up.`data_key` = 'company' THEN up.`data_value` END) AS company
		FROM `users` u
		LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`
		WHERE u.`user_id` > 0";
	$params = array();
	if ($search !== '') {
		$platformSql .= ' AND (u.`email` LIKE ? OR u.`phone` LIKE ? OR up.`data_value` LIKE ?)';
		$q = '%' . $search . '%';
		$params = array($q, $q, $q);
	}
	$platformSql .= ' GROUP BY u.`user_id` ORDER BY u.`user_id` DESC LIMIT 80';
	try {
		$st = $platformPdo->prepare($platformSql);
		$st->execute($params);
		$backend = epc_scp_backend();
		foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
			$results[] = array(
				'source' => 'platform',
				'source_label' => 'Platform (ecomae)',
				'hostname' => 'www.ecomae.com',
				'user_id' => (int) $r['user_id'],
				'name' => epc_scp_customer_name_from_row($r),
				'email' => (string) ($r['email'] ?? ''),
				'phone' => (string) ($r['phone'] ?? ''),
				'time_reg' => (int) ($r['time_reg'] ?? 0),
				'links' => array(
					'crm' => '/' . $backend . '/shop/customer_mgmt/customer_mgmt?tab=customers&user_id=' . (int) $r['user_id'],
					'erp' => '/' . $backend . '/shop/finance/erp?epc_erp_shell=1&area=sales',
					'cp' => '/' . $backend . '/',
				),
			);
		}
		$stats['platform'] = count($results);
	} catch (Exception $e) {
	}

	if ($tenantFilter === '' || $tenantFilter === 'platform') {
		// platform-only filter handled above
	}

	$tenants = epc_portal_tenant_control_list_all($platformPdo);
	foreach ($tenants as $t) {
		if (empty($t['in_registry']) || !empty($t['access_blocked'])) {
			continue;
		}
		$key = (string) ($t['site_key'] ?? '');
		if ($tenantFilter !== '' && $tenantFilter !== 'platform' && $tenantFilter !== $key) {
			continue;
		}
		if ($tenantFilter === 'platform') {
			continue;
		}
		$tenantPdo = epc_portal_tenant_control_tenant_pdo($t);
		if (!$tenantPdo instanceof PDO) {
			continue;
		}
		$stats['tenants_scanned']++;
		$meta = array(
			'site_key' => $key,
			'label' => trim((string) ($t['trade_name'] ?? $key)),
			'hostname' => (string) ($t['hostname'] ?? ''),
			'urls' => is_array($t['urls'] ?? null) ? $t['urls'] : epc_portal_tenant_control_urls($t),
		);
		$chunk = epc_scp_customers_from_pdo($tenantPdo, $meta, $search, 25);
		if (count($chunk) > 0) {
			$stats['tenants_with_hits']++;
		}
		$results = array_merge($results, $chunk);
		if (count($results) >= 200) {
			break;
		}
	}

	usort($results, function ($a, $b) {
		return strcmp((string) ($b['email'] ?? ''), (string) ($a['email'] ?? ''));
	});
	$total = count($results);
	$offset = ($page - 1) * $perPage;
	$pageRows = array_slice($results, $offset, $perPage);

	return array(
		'rows' => $pageRows,
		'total' => $total,
		'page' => $page,
		'per_page' => $perPage,
		'stats' => $stats,
	);
}

function epc_scp_render_hero(string $badge, string $title, string $sub, array $actions = array()): void
{
	?>
<div class="epc-scp-panel__hero">
	<div>
		<span class="epc-scp-dashboard__badge"><?php echo epc_scp_h($badge); ?></span>
		<h2 class="epc-scp-dashboard__title"><?php echo epc_scp_h($title); ?></h2>
		<p class="epc-scp-dashboard__sub"><?php echo epc_scp_h($sub); ?></p>
	</div>
	<?php if (count($actions) > 0) { ?>
	<div class="epc-scp-dashboard__hero-actions">
		<?php foreach ($actions as $act) { ?>
		<a class="btn btn-sm <?php echo !empty($act['primary']) ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_scp_h($act['url']); ?>">
			<?php if (!empty($act['icon'])) { ?><i class="fa <?php echo epc_scp_h($act['icon']); ?>"></i> <?php } ?>
			<?php echo epc_scp_h($act['label']); ?>
		</a>
		<?php } ?>
	</div>
	<?php } ?>
</div>
	<?php
}

function epc_scp_operator_guide_url(): string
{
	return '/' . epc_scp_backend() . '/control/portal/epc_super_cp_operator_guide';
}

function epc_scp_render_workspace_intro(string $module): void
{
	$guide = epc_scp_operator_guide_url();
	$intros = array(
		'customer_board' => array(
			'title' => 'Operator workspace — Customer board',
			'body' => 'Search customers across the platform registry and every live tenant database. Use CRM and ERP links for support without logging into each client CP separately.',
		),
		'info_blocks' => array(
			'title' => 'Operator workspace — Info blocks',
			'body' => 'Publish HTML banners and notices on platform marketing pages, tenant storefronts, checkout, and CP dashboard slots. Scope blocks platform-wide or per tenant.',
		),
		'price_configs' => array(
			'title' => 'Operator workspace — Price configs',
			'body' => 'Stack markup rules for catalogue, price lists, and API clients. Platform defaults apply everywhere unless a tenant override wins on priority.',
		),
		'communication' => array(
			'title' => 'Operator workspace — Communication',
			'body' => 'Set which platform events send email, review SMTP diagnostics, and track internal tasks assigned to ECOM AE operators.',
		),
	);
	if (!isset($intros[$module])) {
		return;
	}
	$row = $intros[$module];
	?>
<div class="epc-scp-intro-panel">
	<div class="epc-scp-intro-panel__body">
		<strong><?php echo epc_scp_h($row['title']); ?></strong>
		<p><?php echo epc_scp_h($row['body']); ?></p>
	</div>
	<a class="btn btn-sm btn-default" href="<?php echo epc_scp_h($guide); ?>"><i class="fa fa-book"></i> Operator guide</a>
</div>
	<?php
}

function epc_scp_render_empty_state(string $title, string $body, array $actions = array()): void
{
	?>
<div class="epc-scp-empty-state">
	<div class="epc-scp-empty-state__icon"><i class="fa fa-inbox"></i></div>
	<h4><?php echo epc_scp_h($title); ?></h4>
	<p><?php echo epc_scp_h($body); ?></p>
	<?php if (count($actions) > 0) { ?>
	<div class="epc-scp-empty-state__actions">
		<?php foreach ($actions as $act) { ?>
		<a class="btn btn-sm <?php echo !empty($act['primary']) ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_scp_h($act['url']); ?>">
			<?php if (!empty($act['icon'])) { ?><i class="fa <?php echo epc_scp_h($act['icon']); ?>"></i> <?php } ?>
			<?php echo epc_scp_h($act['label']); ?>
		</a>
		<?php } ?>
	</div>
	<?php } ?>
</div>
	<?php
}

function epc_scp_guard_super_admin(): bool
{
	if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
		echo '<div class="alert alert-warning">This module is available on ECOM AE Super CP only.</div>';
		return false;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (!DP_User::isAdmin()) {
		global $DP_Config;
		echo '<div class="alert alert-warning">Please <a href="/' . epc_scp_h((string) ($DP_Config->backend_dir ?? 'cp')) . '/">log in to Super CP</a>.</div>';
		return false;
	}
	return true;
}
