<?php
/**
 * ERP — departments, staff profiles, workflows, marketing & HR extensions.
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_staff_all_tabs()
{
	return array(
		'dashboard', 'workflow', 'crm', 'proposals', 'sales_orders', 'delivery_notes', 'invoices',
		'fulfilment', 'revenue', 'receivables',
		'purchases', 'payables', 'rfq', 'purchase_orders', 'three_way_match',
		'cash_bank', 'payment_batches', 'petty_cash',
		'coa', 'gl', 'aging', 'pl', 'balance_sheet', 'vat_return', 'tax_compliance', 'einvoice', 'opening_balances', 'document_control',
		'inventory', 'fixed_assets', 'manufacturing', 'custom_shipping',
		'staff', 'hr', 'payroll', 'expense_reports',
		'marketing', 'reports', 'knowledge_base', 'multi_entity', 'audit',
		'agenda', 'projects', 'contacts', 'documents',
		'erp_setup', 'data_import',
		// D365 F&O-style modules
		'business_units', 'listing', 'budgeting',
		'product_info', 'inv_groups', 'master_planning', 'retail_barcode',
		'ap_setup', 'ar_setup', 'bank_setup',
		'consolidation_bu', 'enterprise_reports', 'landed_cost', 'doc_formats',
		// BOS pillars (Phase 2)
		'compliance', 'approvals', 'industry_intel', 'ai_advisor', 'vat_refund',
	);
}

function epc_erp_departments_config()
{
	return array(
		'admin' => array(
			'name' => 'Administration',
			'group' => 'EPC_ERP_DEPT_ADMIN',
			'icon' => 'fa-shield',
			'color' => '#334155',
			'tabs' => array('*'),
			'workflows' => array(
				'Month-end ERP checklist',
				'User access review',
				'Cross-department escalation',
				'Policy & configuration sign-off',
			),
		),
		'sales' => array(
			'name' => 'Sales',
			'group' => 'EPC_ERP_DEPT_SALES',
			'icon' => 'fa-line-chart',
			'color' => '#2563eb',
			'tabs' => array('dashboard', 'crm', 'proposals', 'sales_orders', 'delivery_notes', 'invoices', 'fulfilment', 'revenue', 'receivables', 'documents', 'workflow', 'staff'),
			'workflows' => array(
				'Qualify customer / credit check',
				'Confirm order & payment terms',
				'Track open receivable',
				'Customer settlement / write-off request',
			),
		),
		'logistics' => array(
			'name' => 'Logistics',
			'group' => 'EPC_ERP_DEPT_LOGISTICS',
			'icon' => 'fa-truck',
			'color' => '#0d9488',
			'tabs' => array('dashboard', 'fulfilment', 'custom_shipping', 'inventory', 'workflow', 'staff'),
			'workflows' => array(
				'Reserve stock for order',
				'Supplier delivery to warehouse',
				'Pick, pack & carrier label',
				'Confirm delivery to customer',
				'Process customer return',
			),
		),
		'marketing' => array(
			'name' => 'Marketing',
			'group' => 'EPC_ERP_DEPT_MARKETING',
			'icon' => 'fa-bullhorn',
			'color' => '#db2777',
			'tabs' => array('dashboard', 'marketing', 'workflow', 'staff'),
			'workflows' => array(
				'Plan campaign & budget',
				'Launch ads / marketplace promo',
				'Track leads & conversion',
				'Report ROI to finance',
			),
		),
		'finance' => array(
			'name' => 'Finance',
			'group' => 'EPC_ERP_DEPT_FINANCE',
			'icon' => 'fa-money',
			'color' => '#16a34a',
			'tabs' => array('dashboard', 'receivables', 'payables', 'purchases', 'cash_bank', 'inventory', 'fixed_assets', 'opening_balances', 'vat_return', 'tax_compliance', 'einvoice', 'invoices', 'documents', 'coa', 'gl', 'pl', 'balance_sheet', 'payroll', 'workflow', 'staff'),
			'workflows' => array(
				'Approve & pay payroll run',
				'Customer receipt / refund',
				'Supplier payment run',
				'Cash & bank reconciliation',
				'VAT review',
			),
		),
		'hr' => array(
			'name' => 'Human Resources',
			'group' => 'EPC_ERP_DEPT_HR',
			'icon' => 'fa-users',
			'color' => '#9333ea',
			'tabs' => array('dashboard', 'hr', 'payroll', 'staff', 'workflow'),
			'workflows' => array(
				'Prepare monthly payroll',
				'Onboard new staff ERP access',
				'Leave & attendance record',
				'Performance review cycle',
				'Offboard & revoke access',
			),
		),
		'it' => array(
			'name' => 'Information Technology',
			'group' => 'EPC_ERP_DEPT_IT',
			'icon' => 'fa-laptop',
			'color' => '#0891b2',
			'tabs' => array('dashboard', 'workflow', 'staff'),
			'workflows' => array(
				'ERP & CP user access',
				'System backup & security review',
				'Marketplace / carrier API integration',
				'Support staff ERP login issues',
			),
		),
		'purchase' => array(
			'name' => 'Purchase',
			'group' => 'EPC_ERP_DEPT_PURCHASE',
			'icon' => 'fa-shopping-basket',
			'color' => '#ea580c',
			'tabs' => array('dashboard', 'purchases', 'payables', 'inventory', 'fulfilment', 'custom_shipping', 'workflow', 'staff'),
			'workflows' => array(
				'Raise PO / supplier order',
				'Match supplier invoice to order',
				'Record purchase in ERP',
				'Resolve price variance',
			),
		),
		'accounts' => array(
			'name' => 'Accounts',
			'group' => 'EPC_ERP_DEPT_ACCOUNTS',
			'icon' => 'fa-book',
			'color' => '#1d4ed8',
			'tabs' => array('dashboard', 'revenue', 'purchases', 'cash_bank', 'vat_return', 'tax_compliance', 'einvoice', 'invoices', 'documents', 'coa', 'gl', 'pl', 'balance_sheet', 'payroll', 'workflow', 'staff'),
			'workflows' => array(
				'Post sales orders to GL',
				'Sync purchases & cash to GL',
				'Prepare P&L & balance sheet',
				'Trial balance & period close',
			),
		),
	);
}

function epc_erp_staff_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_departments` (
		`code` varchar(32) NOT NULL,
		`name` varchar(128) NOT NULL,
		`group_value` varchar(64) NOT NULL,
		`tabs_json` text,
		`workflows_json` text,
		`sort_order` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`code`),
		UNIQUE KEY `group_value` (`group_value`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_staff_profiles` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`user_id` int(11) NOT NULL,
		`department_code` varchar(32) NOT NULL,
		`display_name` varchar(128) NOT NULL,
		`job_title` varchar(128) DEFAULT NULL,
		`email` varchar(128) DEFAULT NULL,
		`phone` varchar(64) DEFAULT NULL,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `user_id` (`user_id`),
		KEY `department_code` (`department_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_workflow_tasks` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`department_code` varchar(32) NOT NULL,
		`workflow_step` varchar(64) DEFAULT NULL,
		`title` varchar(255) NOT NULL,
		`description` text,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`status` enum('pending','in_progress','done','cancelled') NOT NULL DEFAULT 'pending',
		`priority` enum('low','normal','high') NOT NULL DEFAULT 'normal',
		`assigned_user_id` int(11) NOT NULL DEFAULT 0,
		`created_by` int(11) NOT NULL DEFAULT 0,
		`due_at` int(11) NOT NULL DEFAULT 0,
		`completed_at` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `department_code` (`department_code`,`status`),
		KEY `assigned_user_id` (`assigned_user_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_marketing_campaigns` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`name` varchar(255) NOT NULL,
		`channel` varchar(64) DEFAULT NULL,
		`budget` decimal(14,2) NOT NULL DEFAULT 0.00,
		`spent` decimal(14,2) NOT NULL DEFAULT 0.00,
		`leads` int(11) NOT NULL DEFAULT 0,
		`status` enum('draft','active','paused','completed') NOT NULL DEFAULT 'draft',
		`time_start` int(11) NOT NULL DEFAULT 0,
		`time_end` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_hr_records` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`staff_profile_id` int(11) NOT NULL,
		`hire_date` int(11) NOT NULL DEFAULT 0,
		`leave_balance_days` decimal(5,1) NOT NULL DEFAULT 0.0,
		`manager_user_id` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `staff_profile_id` (`staff_profile_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	epc_erp_staff_seed_departments($db);
}

function epc_erp_staff_seed_departments(PDO $db)
{
	$cfg = epc_erp_departments_config();
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_departments` (`code`, `name`, `group_value`, `tabs_json`, `workflows_json`, `sort_order`, `active`)
		 VALUES (?, ?, ?, ?, ?, ?, 1)
		 ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `tabs_json` = VALUES(`tabs_json`), `workflows_json` = VALUES(`workflows_json`), `sort_order` = VALUES(`sort_order`)'
	);
	$order = 0;
	foreach ($cfg as $code => $row) {
		$order += 10;
		$ins->execute(array(
			$code,
			$row['name'],
			$row['group'],
			json_encode(isset($row['tabs']) ? $row['tabs'] : array()),
			json_encode(isset($row['workflows']) ? $row['workflows'] : array()),
			$order,
		));
	}
}

function epc_erp_staff_group_id(PDO $db, $groupValue)
{
	$st = $db->prepare('SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1');
	$st->execute(array($groupValue));
	return (int)$st->fetchColumn();
}

function epc_erp_staff_user_department_codes(PDO $db, $userId = 0)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if ($userId <= 0) {
		$userId = (int)DP_User::getUserId();
	}
	if ($userId <= 0) {
		return array();
	}
	$cfg = epc_erp_departments_config();
	$groups = array();
	foreach ($cfg as $code => $row) {
		$gid = epc_erp_staff_group_id($db, $row['group']);
		if ($gid > 0) {
			$groups[$gid] = $code;
		}
	}
	if (empty($groups)) {
		return array();
	}
	$in = implode(',', array_map('intval', array_keys($groups)));
	$st = $db->prepare('SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN (' . $in . ')');
	$st->execute(array($userId));
	$codes = array();
	while ($gid = $st->fetchColumn()) {
		$codes[] = $groups[(int)$gid];
	}
	$prof = $db->prepare('SELECT `department_code` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? AND `active` = 1 LIMIT 1');
	$prof->execute(array($userId));
	$pc = (string)$prof->fetchColumn();
	if ($pc !== '' && !in_array($pc, $codes, true)) {
		$codes[] = $pc;
	}
	return $codes;
}

function epc_erp_staff_user_is_full_admin(PDO $db, $userId = 0)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	if (DP_User::isAdmin() || epc_erp_user_in_administrator_group($db, $userId)) {
		return true;
	}
	if (epc_erp_user_in_backend_tree($db, $userId)) {
		return true;
	}
	$codes = epc_erp_staff_user_department_codes($db, $userId);
	return in_array('admin', $codes, true);
}

function epc_erp_staff_allowed_tabs(PDO $db, $userId = 0)
{
	$all = epc_erp_staff_all_tabs();
	if (function_exists('epc_platform_erp_is_request') && epc_platform_erp_is_request()
		&& function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()) {
		return $all;
	}
	if (epc_erp_staff_user_is_full_admin($db, $userId)) {
		$userTabs = $all;
	} else {
		$codes = epc_erp_staff_user_department_codes($db, $userId);
		if (empty($codes)) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
			if (epc_erp_user_in_team($db, $userId)) {
				$userTabs = array('dashboard', 'revenue', 'receivables', 'payables', 'purchases', 'cash_bank', 'workflow', 'staff');
			} else {
				$userTabs = array('dashboard', 'workflow');
			}
		} else {
			$cfg = epc_erp_departments_config();
			$tabs = array();
			$userTabs = null;
			foreach ($codes as $code) {
				if (!isset($cfg[$code])) {
					continue;
				}
				if (in_array('*', $cfg[$code]['tabs'], true)) {
					$userTabs = $all;
					break;
				}
				foreach ($cfg[$code]['tabs'] as $t) {
					$tabs[$t] = true;
				}
			}
			if ($userTabs === null) {
				$userTabs = array_values(array_intersect($all, array_keys($tabs)));
			}
		}
	}
	$modFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
	if (is_file($modFile)) {
		require_once $modFile;
		if (function_exists('epc_erp_filter_tabs_by_tenant_modules')) {
			return epc_erp_filter_tabs_by_tenant_modules($userTabs);
		}
	}
	return $userTabs;
}

function epc_erp_staff_primary_department(PDO $db, $userId = 0)
{
	$st = $db->prepare('SELECT `department_code` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? AND `active` = 1 LIMIT 1');
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if ($userId <= 0) {
		$userId = (int)DP_User::getUserId();
	}
	$st->execute(array($userId));
	$code = (string)$st->fetchColumn();
	if ($code !== '') {
		return $code;
	}
	$codes = epc_erp_staff_user_department_codes($db, $userId);
	return !empty($codes) ? $codes[0] : '';
}

function epc_erp_staff_department_name($code)
{
	$cfg = epc_erp_departments_config();
	return isset($cfg[$code]['name']) ? $cfg[$code]['name'] : ucfirst($code);
}

function epc_erp_staff_list(PDO $db, $departmentCode = '')
{
	epc_erp_staff_ensure_schema($db);
	$sql = 'SELECT p.*, u.`email` AS user_email
		FROM `epc_erp_staff_profiles` p
		LEFT JOIN `users` u ON u.`user_id` = p.`user_id`
		WHERE p.`active` = 1';
	$params = array();
	if ($departmentCode !== '') {
		$sql .= ' AND p.`department_code` = ?';
		$params[] = $departmentCode;
	}
	$sql .= ' ORDER BY p.`department_code`, p.`display_name`';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_workflow_list(PDO $db, $departmentCode = '', $status = '', $limit = 100)
{
	epc_erp_staff_ensure_schema($db);
	$sql = 'SELECT t.*, p.`display_name` AS assignee_name
		FROM `epc_erp_workflow_tasks` t
		LEFT JOIN `epc_erp_staff_profiles` p ON p.`user_id` = t.`assigned_user_id`
		WHERE 1=1';
	$params = array();
	if ($departmentCode !== '') {
		$sql .= ' AND t.`department_code` = ?';
		$params[] = $departmentCode;
	}
	if ($status !== '') {
		$sql .= ' AND t.`status` = ?';
		$params[] = $status;
	}
	$sql .= ' ORDER BY FIELD(t.`status`, \'in_progress\', \'pending\', \'done\', \'cancelled\'), t.`priority` DESC, t.`due_at` ASC, t.`id` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_workflow_update_status(PDO $db, $taskId, $status, $userId = 0)
{
	$allowed = array('pending', 'in_progress', 'done', 'cancelled');
	if (!in_array($status, $allowed, true)) {
		throw new Exception('Invalid status');
	}
	$now = time();
	$completed = ($status === 'done') ? $now : 0;
	$db->prepare('UPDATE `epc_erp_workflow_tasks` SET `status` = ?, `completed_at` = ? WHERE `id` = ?')
		->execute(array($status, $completed, (int)$taskId));
	return (int)$taskId;
}

function epc_erp_workflow_create(PDO $db, array $data, $createdBy = 0)
{
	epc_erp_staff_ensure_schema($db);
	$db->prepare(
		'INSERT INTO `epc_erp_workflow_tasks`
		(`department_code`, `workflow_step`, `title`, `description`, `order_id`, `status`, `priority`, `assigned_user_id`, `created_by`, `due_at`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		(string)($data['department_code'] ?? 'admin'),
		(string)($data['workflow_step'] ?? ''),
		mb_substr((string)($data['title'] ?? 'Task'), 0, 255),
		(string)($data['description'] ?? ''),
		(int)($data['order_id'] ?? 0),
		in_array($data['status'] ?? '', array('pending', 'in_progress', 'done', 'cancelled'), true) ? $data['status'] : 'pending',
		in_array($data['priority'] ?? '', array('low', 'normal', 'high'), true) ? $data['priority'] : 'normal',
		(int)($data['assigned_user_id'] ?? 0),
		(int)$createdBy,
		!empty($data['due_at']) ? (int)$data['due_at'] : (time() + 86400 * 3),
		time(),
	));
	return (int)$db->lastInsertId();
}

function epc_erp_marketing_list(PDO $db)
{
	epc_erp_staff_ensure_schema($db);
	return $db->query('SELECT * FROM `epc_erp_marketing_campaigns` ORDER BY `id` DESC LIMIT 50')->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_marketing_create(PDO $db, array $data)
{
	epc_erp_staff_ensure_schema($db);
	$now = time();
	$name = mb_substr(trim((string)($data['name'] ?? 'Campaign')), 0, 255);
	$channel = mb_substr(trim((string)($data['channel'] ?? 'digital')), 0, 64);
	$budget = max(0, (float)($data['budget'] ?? 0));
	$status = in_array($data['status'] ?? '', array('draft', 'active', 'paused', 'completed'), true) ? $data['status'] : 'active';
	$start = !empty($data['time_start']) ? strtotime((string)$data['time_start'] . ' 00:00:00') : $now;
	$end = !empty($data['time_end']) ? strtotime((string)$data['time_end'] . ' 23:59:59') : ($now + 86400 * 30);
	if ($start === false) {
		$start = $now;
	}
	if ($end === false) {
		$end = $now + 86400 * 30;
	}
	$db->prepare(
		'INSERT INTO `epc_erp_marketing_campaigns`
		(`name`, `channel`, `budget`, `spent`, `leads`, `status`, `time_start`, `time_end`, `notes`, `time_created`)
		VALUES (?, ?, ?, 0, 0, ?, ?, ?, ?, ?)'
	)->execute(array(
		$name,
		$channel,
		$budget,
		$status,
		$start,
		$end,
		trim((string)($data['notes'] ?? '')),
		$now,
	));
	return (int)$db->lastInsertId();
}

function epc_erp_hr_list(PDO $db)
{
	epc_erp_staff_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_payroll.php';
	epc_erp_payroll_ensure_schema($db);
	return $db->query(
		'SELECT h.*, p.`display_name`, p.`department_code`, p.`job_title`, p.`email`, p.`user_id`
		 FROM `epc_erp_hr_records` h
		 INNER JOIN `epc_erp_staff_profiles` p ON p.`id` = h.`staff_profile_id`
		 ORDER BY p.`department_code`, p.`display_name`'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_staff_dashboard(PDO $db)
{
	epc_erp_staff_ensure_schema($db);
	$depts = $db->query('SELECT * FROM `epc_erp_departments` WHERE `active` = 1 ORDER BY `sort_order`')->fetchAll(PDO::FETCH_ASSOC);
	$staffCount = (int)$db->query('SELECT COUNT(*) FROM `epc_erp_staff_profiles` WHERE `active` = 1')->fetchColumn();
	$tasksOpen = (int)$db->query("SELECT COUNT(*) FROM `epc_erp_workflow_tasks` WHERE `status` IN ('pending','in_progress')")->fetchColumn();
	$campaigns = (int)$db->query("SELECT COUNT(*) FROM `epc_erp_marketing_campaigns` WHERE `status` = 'active'")->fetchColumn();
	$byDept = array();
	$st = $db->query(
		"SELECT `department_code`, COUNT(*) AS cnt FROM `epc_erp_workflow_tasks`
		 WHERE `status` IN ('pending','in_progress') GROUP BY `department_code`"
	);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$byDept[$r['department_code']] = (int)$r['cnt'];
	}
	return array(
		'departments' => $depts,
		'staff_count' => $staffCount,
		'tasks_open' => $tasksOpen,
		'tasks_by_department' => $byDept,
		'active_campaigns' => $campaigns,
	);
}

function epc_erp_staff_demo_report(PDO $db)
{
	require_once __DIR__ . '/epc_erp_payroll.php';
	epc_erp_payroll_ensure_schema($db);
	return array(
		'dashboard' => epc_erp_staff_dashboard($db),
		'staff' => epc_erp_staff_list($db),
		'workflow' => epc_erp_workflow_list($db, '', '', 50),
		'marketing' => epc_erp_marketing_list($db),
		'hr' => epc_erp_hr_list($db),
		'payroll' => epc_erp_payroll_demo_report($db),
		'departments' => epc_erp_departments_config(),
	);
}

function epc_erp_staff_lang(PDO $db, $key, $en, $ru)
{
	$db->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$db->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$db->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_erp_staff_ensure_group(PDO $db, $groupValue, $description = '')
{
	$gid = epc_erp_staff_group_id($db, $groupValue);
	if ($gid > 0) {
		return $gid;
	}
	$maxId = (int)$db->query('SELECT IFNULL(MAX(`id`), 0) FROM `groups`')->fetchColumn();
	$gid = $maxId + 1;
	$db->prepare(
		'INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`)
		 VALUES (?, ?, 0, 2, 1, 1, 0, 0, 0, 0, ?, 90)'
	)->execute(array($gid, $groupValue, $description !== '' ? $description : $groupValue));
	try {
		$db->exec('UPDATE `groups` SET `count` = (SELECT COUNT(*) FROM (SELECT `id` FROM `groups` WHERE `parent` = 1) x) WHERE `id` = 1');
	} catch (Exception $e) {
	}
	return $gid;
}

function epc_erp_staff_create_user(PDO $db, $cfg, array $spec)
{
	$email = strtolower(trim((string)$spec['email']));
	$password = (string)$spec['password'];
	$existing = $db->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
	$existing->execute(array($email));
	$userId = (int)$existing->fetchColumn();
	if ($userId <= 0) {
		$db->prepare(
			'INSERT INTO `users` (`reg_variant`, `email`, `email_confirmed`, `phone`, `phone_confirmed`, `password`, `unlocked`, `time_registered`, `admin_created`)
			 VALUES (1, ?, 1, NULL, 0, ?, 1, ?, 1)'
		)->execute(array($email, md5($password . $cfg->secret_succession), time()));
		$userId = (int)$db->lastInsertId();
	}
	$groupIds = isset($spec['group_ids']) ? $spec['group_ids'] : array();
	$ins = $db->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
	foreach ($groupIds as $gid) {
		$ins->execute(array($userId, (int)$gid));
	}
	epc_erp_staff_ensure_schema($db);
	$prof = $db->prepare('SELECT `id` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1');
	$prof->execute(array($userId));
	$profileId = (int)$prof->fetchColumn();
	if ($profileId <= 0) {
		$db->prepare(
			'INSERT INTO `epc_erp_staff_profiles` (`user_id`, `department_code`, `display_name`, `job_title`, `email`, `phone`, `active`, `time_created`)
			 VALUES (?, ?, ?, ?, ?, ?, 1, ?)'
		)->execute(array(
			$userId,
			(string)$spec['department_code'],
			(string)$spec['display_name'],
			(string)$spec['job_title'],
			$email,
			(string)($spec['phone'] ?? ''),
			time(),
		));
		$profileId = (int)$db->lastInsertId();
	} else {
		$db->prepare(
			'UPDATE `epc_erp_staff_profiles` SET `department_code` = ?, `display_name` = ?, `job_title` = ?, `email` = ?, `active` = 1 WHERE `id` = ?'
		)->execute(array(
			(string)$spec['department_code'],
			(string)$spec['display_name'],
			(string)$spec['job_title'],
			$email,
			$profileId,
		));
	}
	if (!empty($spec['hr'])) {
		require_once __DIR__ . '/epc_erp_payroll.php';
		epc_erp_payroll_ensure_schema($db);
		$hrData = $spec['hr'];
		$hr = $db->prepare('SELECT `id` FROM `epc_erp_hr_records` WHERE `staff_profile_id` = ? LIMIT 1');
		$hr->execute(array($profileId));
		$hrId = (int)$hr->fetchColumn();
		$basic = round((float)($hrData['basic_salary'] ?? 0), 2);
		$allow = round((float)($hrData['allowances'] ?? 0), 2);
		$bankAcc = (string)($hrData['bank_account'] ?? '');
		$bankName = (string)($hrData['bank_name'] ?? 'Emirates NBD');
		$daysWorked = (float)($hrData['days_worked'] ?? 30);
		if ($daysWorked <= 0) {
			$daysWorked = 30;
		}
		if ($hrId <= 0) {
			$db->prepare(
				'INSERT INTO `epc_erp_hr_records` (`staff_profile_id`, `hire_date`, `leave_balance_days`, `manager_user_id`, `notes`, `basic_salary`, `allowances`, `bank_account`, `bank_name`, `days_worked`, `time_updated`)
				 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
			)->execute(array(
				$profileId,
				(int)($hrData['hire_date'] ?? time()),
				(float)($hrData['leave_balance'] ?? 22),
				(int)($hrData['manager_user_id'] ?? 0),
				(string)($hrData['notes'] ?? ''),
				$basic,
				$allow,
				$bankAcc,
				$bankName,
				$daysWorked,
				time(),
			));
		} else {
			$db->prepare(
				'UPDATE `epc_erp_hr_records` SET `hire_date` = ?, `leave_balance_days` = ?, `basic_salary` = ?, `allowances` = ?, `bank_account` = ?, `bank_name` = ?, `days_worked` = ?, `time_updated` = ? WHERE `id` = ?'
			)->execute(array(
				(int)($hrData['hire_date'] ?? time()),
				(float)($hrData['leave_balance'] ?? 22),
				$basic,
				$allow,
				$bankAcc,
				$bankName,
				$daysWorked,
				time(),
				$hrId,
			));
		}
	}
	return array('user_id' => $userId, 'profile_id' => $profileId, 'email' => $email);
}

function epc_erp_staff_seed_demo(PDO $db, $cfg)
{
	epc_erp_staff_ensure_schema($db);
	epc_erp_staff_lang($db, 'EPC_ERP_TEAM', 'ERP Finance team', 'ERP — команда финансов');

	$teamGid = epc_erp_staff_ensure_group($db, 'EPC_ERP_TEAM', 'ERP Finance team (all staff)');
	$deptGroups = array();
	foreach (epc_erp_departments_config() as $code => $row) {
		epc_erp_staff_lang($db, $row['group'], $row['name'] . ' (ERP)', $row['name']);
		$deptGroups[$code] = epc_erp_staff_ensure_group($db, $row['group'], $row['name']);
	}

	$defaultPass = 'EpcStaff2026!';
	$salaryMap = array(
		'admin' => array(25000, 2000),
		'sales' => array(12000, 1500),
		'logistics' => array(10000, 800),
		'marketing' => array(11000, 1000),
		'finance' => array(15000, 1200),
		'hr' => array(14000, 1000),
		'purchase' => array(11000, 900),
		'accounts' => array(13000, 1100),
		'it' => array(18000, 1500),
	);
	$staffSpecs = array(
		array('code' => 'admin', 'email' => 'erp.admin@epartscart.local', 'name' => 'Dummy Admin User', 'title' => 'ERP Administrator', 'days' => 30),
		array('code' => 'sales', 'email' => 'erp.sales@epartscart.local', 'name' => 'Dummy Sales User', 'title' => 'Sales Executive', 'days' => 28),
		array('code' => 'logistics', 'email' => 'erp.logistics@epartscart.local', 'name' => 'Dummy Logistics User', 'title' => 'Logistics Coordinator', 'days' => 31),
		array('code' => 'marketing', 'email' => 'erp.marketing@epartscart.local', 'name' => 'Dummy Marketing User', 'title' => 'Marketing Specialist', 'days' => 30),
		array('code' => 'finance', 'email' => 'erp.finance@epartscart.local', 'name' => 'Dummy Finance User', 'title' => 'Finance Officer', 'days' => 30),
		array('code' => 'hr', 'email' => 'erp.hr@epartscart.local', 'name' => 'Dummy HR User', 'title' => 'HR Manager', 'days' => 29),
		array('code' => 'purchase', 'email' => 'erp.purchase@epartscart.local', 'name' => 'Dummy Purchase User', 'title' => 'Purchase Officer', 'days' => 27),
		array('code' => 'accounts', 'email' => 'erp.accounts@epartscart.local', 'name' => 'Dummy Accounts User', 'title' => 'Accountant', 'days' => 30),
		array('code' => 'it', 'email' => 'erp.it@epartscart.local', 'name' => 'Dummy IT Manager', 'title' => 'IT Manager', 'days' => 32),
	);

	$users = array();
	$adminUserId = 0;
	foreach ($staffSpecs as $spec) {
		$code = $spec['code'];
		$gids = array($teamGid, $deptGroups[$code]);
		$sal = isset($salaryMap[$code]) ? $salaryMap[$code] : array(10000, 0);
		$row = epc_erp_staff_create_user($db, $cfg, array(
			'email' => $spec['email'],
			'password' => $defaultPass,
			'group_ids' => $gids,
			'department_code' => $code,
			'display_name' => $spec['name'],
			'job_title' => $spec['title'],
			'hr' => array(
				'hire_date' => strtotime('-' . (30 + count($users) * 15) . ' days'),
				'leave_balance' => 22 - count($users) * 0.5,
				'basic_salary' => $sal[0],
				'allowances' => $sal[1],
				'days_worked' => isset($spec['days']) ? (float)$spec['days'] : 30,
				'bank_account' => 'AE' . str_pad((string)(470000000000 + count($users)), 12, '0', STR_PAD_LEFT),
				'bank_name' => 'Emirates NBD',
			),
		));
		if ($code === 'admin') {
			$adminUserId = (int)$row['user_id'];
		}
		$users[] = array_merge($row, array('department' => $code, 'password' => $defaultPass));
	}

	if ((int)$db->query('SELECT COUNT(*) FROM `epc_erp_marketing_campaigns`')->fetchColumn() === 0) {
		$now = time();
		$camps = array(
			array('Google Ads — Auto Parts UAE', 'google', 5000, 3200, 48, 'active', $now - 86400 * 20, $now + 86400 * 10),
			array('Amazon Sponsored Products', 'amazon', 3000, 1850, 32, 'active', $now - 86400 * 15, $now + 86400 * 15),
			array('Instagram Reels — Bosch filters', 'social', 1500, 890, 19, 'completed', $now - 86400 * 45, $now - 86400 * 5),
		);
		$ins = $db->prepare(
			'INSERT INTO `epc_erp_marketing_campaigns` (`name`, `channel`, `budget`, `spent`, `leads`, `status`, `time_start`, `time_end`, `notes`, `time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
		);
		foreach ($camps as $c) {
			$ins->execute(array($c[0], $c[1], $c[2], $c[3], $c[4], $c[5], $c[6], $c[7], 'Demo campaign — rename staff & budgets later', $now));
		}
	}

	if ((int)$db->query('SELECT COUNT(*) FROM `epc_erp_workflow_tasks`')->fetchColumn() === 0) {
		$assignMap = array();
		foreach ($users as $u) {
			$assignMap[$u['department']] = (int)$u['user_id'];
		}
		$sampleOrder = (int)$db->query('SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 ORDER BY `id` DESC LIMIT 1')->fetchColumn();
		if ($sampleOrder <= 0) {
			$sampleOrder = 18;
		}
		$tasks = array(
			array('sales', 'Confirm payment terms', 'Follow up customer payment for order #' . $sampleOrder, $sampleOrder, 'in_progress', 'high'),
			array('logistics', 'Pick & pack', 'Prepare shipment for order #' . $sampleOrder, $sampleOrder, 'pending', 'high'),
			array('purchase', 'Supplier invoice', 'Record supplier invoice linked to order #' . $sampleOrder, $sampleOrder, 'pending', 'normal'),
			array('finance', 'Customer receipt', 'Post bank receipt when order #' . $sampleOrder . ' is paid', $sampleOrder, 'pending', 'normal'),
			array('accounts', 'GL posting', 'Post completed sales to GL for current month', 0, 'pending', 'normal'),
			array('marketing', 'Campaign ROI', 'Review May campaign spend vs leads', 0, 'in_progress', 'low'),
			array('hr', 'Payroll — May 2026', 'Generate and approve May payroll run', 0, 'pending', 'high'),
			array('it', 'ERP access audit', 'Review dummy staff logins and department tab access', 0, 'pending', 'normal'),
			array('admin', 'Month-end', 'Run month-end ERP checklist — all departments', 0, 'pending', 'high'),
		);
		foreach ($tasks as $t) {
			epc_erp_workflow_create($db, array(
				'department_code' => $t[0],
				'workflow_step' => $t[1],
				'title' => $t[1],
				'description' => $t[2],
				'order_id' => (int)$t[3],
				'status' => $t[4],
				'priority' => $t[5],
				'assigned_user_id' => isset($assignMap[$t[0]]) ? $assignMap[$t[0]] : 0,
				'due_at' => time() + 86400 * 2,
			), $adminUserId);
		}
	}

	require_once __DIR__ . '/epc_erp_payroll.php';
	$payrollSeed = epc_erp_payroll_seed_demo($db);

	return array('users' => $users, 'default_password' => $defaultPass, 'team_group_id' => $teamGid, 'department_groups' => $deptGroups, 'payroll' => $payrollSeed);
}
