<?php
/**
 * Native CRM module — database schema (ERP-embedded).
 */
defined('_ASTEXE_') or die('No access');

function epc_crm_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_leads` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`company` varchar(255) NOT NULL DEFAULT '',
		`contact_name` varchar(255) NOT NULL DEFAULT '',
		`email` varchar(255) DEFAULT NULL,
		`phone` varchar(64) DEFAULT NULL,
		`source` varchar(64) NOT NULL DEFAULT 'web',
		`status` enum('new','contacted','qualified','unqualified','converted') NOT NULL DEFAULT 'new',
		`owner_user_id` int(11) NOT NULL DEFAULT 0,
		`expected_value` decimal(14,2) NOT NULL DEFAULT 0.00,
		`notes` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_status` (`status`,`active`),
		KEY `x_owner` (`owner_user_id`),
		KEY `x_created` (`time_created`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM leads';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_opportunities` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`lead_id` int(11) NOT NULL DEFAULT 0,
		`title` varchar(255) NOT NULL,
		`stage` enum('prospect','qualified','proposal','negotiation','won','lost') NOT NULL DEFAULT 'prospect',
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`probability` tinyint(3) NOT NULL DEFAULT 10,
		`close_date` int(11) NOT NULL DEFAULT 0,
		`owner_user_id` int(11) NOT NULL DEFAULT 0,
		`linked_user_id` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_stage` (`stage`,`active`),
		KEY `x_lead` (`lead_id`),
		KEY `x_owner` (`owner_user_id`),
		KEY `x_close` (`close_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM opportunities';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_activities` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`activity_type` enum('call','email','meeting','note','task') NOT NULL DEFAULT 'task',
		`related_type` enum('lead','opportunity','user','ticket','project') NOT NULL DEFAULT 'lead',
		`related_id` int(11) NOT NULL DEFAULT 0,
		`due_date` int(11) NOT NULL DEFAULT 0,
		`done` tinyint(1) NOT NULL DEFAULT 0,
		`owner_user_id` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_related` (`related_type`,`related_id`,`active`),
		KEY `x_due` (`done`,`due_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM activities';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_quotes` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`opportunity_id` int(11) NOT NULL DEFAULT 0,
		`lead_id` int(11) NOT NULL DEFAULT 0,
		`customer_user_id` int(11) NOT NULL DEFAULT 0,
		`quote_number` varchar(32) NOT NULL DEFAULT '',
		`status` enum('draft','sent','accepted','rejected') NOT NULL DEFAULT 'draft',
		`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`subtotal` decimal(14,2) NOT NULL DEFAULT 0.00,
		`shop_order_id` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_opp` (`opportunity_id`),
		KEY `x_status` (`status`,`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM quotes';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_quote_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`quote_id` int(11) NOT NULL,
		`description` varchar(512) NOT NULL DEFAULT '',
		`qty` decimal(12,3) NOT NULL DEFAULT 1.000,
		`unit_price` decimal(14,2) NOT NULL DEFAULT 0.00,
		`sort_order` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_quote` (`quote_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM quote lines';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_tickets` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`customer_user_id` int(11) NOT NULL DEFAULT 0,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`subject` varchar(255) NOT NULL,
		`status` enum('open','pending','resolved','closed') NOT NULL DEFAULT 'open',
		`priority` enum('low','normal','high','urgent') NOT NULL DEFAULT 'normal',
		`assigned_user_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_status` (`status`,`active`),
		KEY `x_customer` (`customer_user_id`),
		KEY `x_order` (`order_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM support tickets';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_ticket_messages` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`ticket_id` int(11) NOT NULL,
		`author_user_id` int(11) NOT NULL DEFAULT 0,
		`is_staff` tinyint(1) NOT NULL DEFAULT 1,
		`body` text NOT NULL,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_ticket` (`ticket_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM ticket messages';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_projects` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`name` varchar(255) NOT NULL,
		`opportunity_id` int(11) NOT NULL DEFAULT 0,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`status` enum('planned','active','on_hold','done','cancelled') NOT NULL DEFAULT 'planned',
		`progress_pct` tinyint(3) NOT NULL DEFAULT 0,
		`start_date` int(11) NOT NULL DEFAULT 0,
		`end_date` int(11) NOT NULL DEFAULT 0,
		`owner_user_id` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_status` (`status`,`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM projects';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_project_tasks` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`project_id` int(11) NOT NULL,
		`title` varchar(255) NOT NULL,
		`status` enum('todo','doing','done') NOT NULL DEFAULT 'todo',
		`progress_pct` tinyint(3) NOT NULL DEFAULT 0,
		`hours_est` decimal(8,2) NOT NULL DEFAULT 0.00,
		`hours_logged` decimal(8,2) NOT NULL DEFAULT 0.00,
		`due_date` int(11) NOT NULL DEFAULT 0,
		`sort_order` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_project` (`project_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM project tasks';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_contracts` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`customer_user_id` int(11) NOT NULL DEFAULT 0,
		`title` varchar(255) NOT NULL DEFAULT '',
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`billing_interval` enum('monthly','quarterly','yearly','once') NOT NULL DEFAULT 'monthly',
		`next_billing_date` int(11) NOT NULL DEFAULT 0,
		`status` enum('draft','active','paused','ended') NOT NULL DEFAULT 'draft',
		`notes` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_next_bill` (`next_billing_date`,`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM recurring contracts';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_crm_expenses` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`employee_user_id` int(11) NOT NULL DEFAULT 0,
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`category` varchar(64) NOT NULL DEFAULT 'travel',
		`status` enum('draft','submitted','approved','rejected','paid') NOT NULL DEFAULT 'draft',
		`receipt_note` varchar(512) NOT NULL DEFAULT '',
		`cash_entry_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		KEY `x_status` (`status`,`active`),
		KEY `x_employee` (`employee_user_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='CRM expense reports';");

	epc_crm_schema_migrate_columns($db);
	epc_crm_seed_sample_if_empty($db);
}

function epc_crm_schema_migrate_columns(PDO $db)
{
	$add = function ($table, $column, $def) use ($db) {
		try {
			$t = str_replace('`', '', $table);
			$c = str_replace('`', '', $column);
			$st = $db->prepare('SHOW COLUMNS FROM `' . $t . '` LIKE ?');
			$st->execute(array($c));
			if (!$st->fetch()) {
				$db->exec('ALTER TABLE `' . $t . '` ADD `' . $c . '` ' . $def);
			}
		} catch (Exception $e) {
		}
	};
	$add('epc_crm_leads', 'time_updated', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_leads', 'active', 'tinyint(1) NOT NULL DEFAULT 1');
	$add('epc_crm_opportunities', 'time_updated', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_opportunities', 'active', 'tinyint(1) NOT NULL DEFAULT 1');
	$add('epc_crm_activities', 'time_updated', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_activities', 'owner_user_id', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_activities', 'active', 'tinyint(1) NOT NULL DEFAULT 1');
	$add('epc_crm_quotes', 'lead_id', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_quotes', 'customer_user_id', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_quotes', 'shop_order_id', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_quotes', 'time_updated', 'int(11) NOT NULL DEFAULT 0');
	$add('epc_crm_quotes', 'active', 'tinyint(1) NOT NULL DEFAULT 1');
	try {
		$db->exec("ALTER TABLE `epc_crm_activities` MODIFY `related_type` enum('lead','opportunity','user','ticket','project') NOT NULL DEFAULT 'lead'");
	} catch (Exception $e) {
	}
}

function epc_crm_seed_sample_if_empty(PDO $db)
{
	$n = (int)$db->query('SELECT COUNT(*) FROM `epc_crm_leads`')->fetchColumn();
	if ($n > 0) {
		return;
	}
	$now = time();
	$leads = array(
		array('Gulf Auto Trading LLC', 'Ahmed Al Rashid', 'ahmed@gulfauto.ae', '+971501234567', 'referral', 'qualified', 45000),
		array('Desert Fleet Services', 'Sara Khan', 'sara@desertfleet.com', '+971509876543', 'web', 'contacted', 28000),
		array('Emirates Parts Co', 'Omar Hassan', 'omar@eparts.demo', '+971551112233', 'campaign', 'new', 12000),
	);
	$ins = $db->prepare(
		'INSERT INTO `epc_crm_leads`
		(`company`, `contact_name`, `email`, `phone`, `source`, `status`, `owner_user_id`, `expected_value`, `notes`, `time_created`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?, 0, ?, ?, ?, ?)'
	);
	$leadIds = array();
	foreach ($leads as $i => $L) {
		$ts = $now - ($i + 1) * 86400 * 3;
		$ins->execute(array($L[0], $L[1], $L[2], $L[3], $L[4], $L[5], $L[6], 'Sample lead for CRM pipeline demo.', $ts, $ts));
		$leadIds[] = (int)$db->lastInsertId();
	}
	$oppIns = $db->prepare(
		'INSERT INTO `epc_crm_opportunities`
		(`lead_id`, `title`, `stage`, `amount`, `probability`, `close_date`, `owner_user_id`, `linked_user_id`, `notes`, `time_created`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, ?, 0, 0, ?, ?, ?)'
	);
	$oppIns->execute(array($leadIds[0], 'Annual parts supply — Gulf Auto', 'negotiation', 45000, 70, $now + 86400 * 14, 'Converted from qualified lead.', $now, $now));
	$oppIns->execute(array($leadIds[1], 'Fleet maintenance package', 'proposal', 28000, 40, $now + 86400 * 30, '', $now - 86400, $now));
	$oppIns->execute(array(0, 'Walk-in counter sale', 'prospect', 3500, 20, $now + 86400 * 7, 'No lead linked.', $now - 3600, $now));
	$actIns = $db->prepare(
		'INSERT INTO `epc_crm_activities`
		(`activity_type`, `related_type`, `related_id`, `due_date`, `done`, `owner_user_id`, `notes`, `time_created`, `time_updated`)
		VALUES (?, ?, ?, ?, ?, 0, ?, ?, ?)'
	);
	$actIns->execute(array('call', 'lead', $leadIds[0], $now + 86400, 0, 'Follow up on negotiation terms.', $now, $now));
	$actIns->execute(array('meeting', 'opportunity', 1, $now + 86400 * 2, 0, 'Proposal review meeting.', $now, $now));
	$actIns->execute(array('task', 'lead', $leadIds[2], $now + 3600, 0, 'Send introductory catalogue.', $now, $now));

	$db->prepare(
		'INSERT INTO `epc_crm_quotes` (`opportunity_id`, `lead_id`, `quote_number`, `status`, `subtotal`, `notes`, `time_created`, `time_updated`)
		 VALUES (1, ?, ?, \'draft\', 42000, \'Sample quote\', ?, ?)'
	)->execute(array($leadIds[0], 'Q-' . date('Ym') . '-001', $now, $now));
	$qid = (int)$db->lastInsertId();
	$db->prepare(
		'INSERT INTO `epc_crm_quote_lines` (`quote_id`, `description`, `qty`, `unit_price`, `sort_order`) VALUES (?, ?, ?, ?, 0)'
	)->execute(array($qid, 'Annual parts supply package', 1, 42000));

	$db->prepare(
		'INSERT INTO `epc_crm_tickets` (`customer_user_id`, `subject`, `status`, `priority`, `time_created`, `time_updated`)
		 VALUES (0, ?, \'open\', \'normal\', ?, ?)'
	)->execute(array('Sample: delivery delay enquiry', $now, $now));
	$tid = (int)$db->lastInsertId();
	$db->prepare(
		'INSERT INTO `epc_crm_ticket_messages` (`ticket_id`, `is_staff`, `body`, `time_created`) VALUES (?, 1, ?, ?)'
	)->execute(array($tid, 'Customer reported late delivery — please check order status.', $now));

	$db->prepare(
		'INSERT INTO `epc_crm_projects` (`name`, `opportunity_id`, `status`, `progress_pct`, `start_date`, `end_date`, `time_created`, `time_updated`)
		 VALUES (?, 1, \'active\', 35, ?, ?, ?, ?)'
	)->execute(array('Gulf Auto rollout', $now - 86400 * 7, $now + 86400 * 60, $now, $now));
	$pid = (int)$db->lastInsertId();
	$db->prepare(
		'INSERT INTO `epc_crm_project_tasks` (`project_id`, `title`, `status`, `progress_pct`, `due_date`, `sort_order`, `time_updated`)
		 VALUES (?, ?, \'doing\', 50, ?, 0, ?), (?, ?, \'todo\', 0, ?, 1, ?)'
	)->execute(array($pid, 'Site survey', $now + 86400 * 5, $now, $pid, 'Stock allocation', $now + 86400 * 20, $now));

	$db->prepare(
		'INSERT INTO `epc_crm_contracts` (`customer_user_id`, `title`, `amount`, `billing_interval`, `next_billing_date`, `status`, `time_created`, `time_updated`)
		 VALUES (0, ?, 2500, \'monthly\', ?, \'active\', ?, ?)'
	)->execute(array('Maintenance retainer — sample', $now + 86400 * 14, $now, $now));

	$db->prepare(
		'INSERT INTO `epc_crm_expenses` (`employee_user_id`, `amount`, `category`, `status`, `receipt_note`, `time_created`, `time_updated`)
		 VALUES (0, 185.50, \'travel\', \'submitted\', \'Taxi — client visit\', ?, ?)'
	)->execute(array($now, $now));
}
