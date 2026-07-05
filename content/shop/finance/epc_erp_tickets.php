<?php
/**
 * Client Ticket System — support ticket management for tenants.
 * Priority levels, assignment, SLA tracking, escalation rules.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_tickets_ensure_schema')) {
    function epc_tickets_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_tickets` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `ticket_no` varchar(32) NOT NULL DEFAULT '',
            `subject` varchar(300) NOT NULL DEFAULT '',
            `description` text,
            `category` varchar(100) NOT NULL DEFAULT 'general',
            `priority` enum('low','medium','high','critical') NOT NULL DEFAULT 'medium',
            `status` enum('open','in_progress','waiting','resolved','closed') NOT NULL DEFAULT 'open',
            `client_id` int(11) NOT NULL DEFAULT 0,
            `client_name` varchar(200) NOT NULL DEFAULT '',
            `assigned_to` int(11) NOT NULL DEFAULT 0,
            `assigned_name` varchar(120) NOT NULL DEFAULT '',
            `sla_id` int(11) NOT NULL DEFAULT 0,
            `response_deadline` datetime DEFAULT NULL,
            `resolution_deadline` datetime DEFAULT NULL,
            `first_response_at` datetime DEFAULT NULL,
            `resolved_at` datetime DEFAULT NULL,
            `escalation_level` int(11) NOT NULL DEFAULT 0,
            `tags` varchar(500) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_ticket_no` (`ticket_no`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`),
            KEY `x_priority` (`priority`),
            KEY `x_assigned` (`assigned_to`),
            KEY `x_client` (`client_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Support tickets'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ticket_replies` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `ticket_id` int(11) NOT NULL DEFAULT 0,
            `author_id` int(11) NOT NULL DEFAULT 0,
            `author_name` varchar(120) NOT NULL DEFAULT '',
            `author_type` enum('staff','client') NOT NULL DEFAULT 'staff',
            `message` text,
            `is_internal` tinyint(1) NOT NULL DEFAULT 0,
            `attachments` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_ticket` (`ticket_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Ticket replies'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ticket_escalations` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(200) NOT NULL DEFAULT '',
            `trigger_hours` int(11) NOT NULL DEFAULT 4,
            `priority_filter` varchar(50) NOT NULL DEFAULT 'all',
            `notify_email` varchar(300) NOT NULL DEFAULT '',
            `auto_reassign_to` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Escalation rules'");
    }

    function epc_tickets_generate_no(PDO $db, int $companyId): string
    {
        $stmt = $db->prepare("SELECT COUNT(*) FROM `epc_tickets` WHERE `company_id` = ?");
        $stmt->execute([$companyId]);
        $count = (int) $stmt->fetchColumn() + 1;
        return 'TKT-' . date('Y') . '-' . str_pad((string) $count, 4, '0', STR_PAD_LEFT);
    }

    function epc_tickets_create(PDO $db, array $data): int
    {
        $ticketNo = epc_tickets_generate_no($db, (int) ($data['company_id'] ?? 0));
        $stmt = $db->prepare("INSERT INTO `epc_tickets`
            (`company_id`,`ticket_no`,`subject`,`description`,`category`,`priority`,`status`,`client_id`,`client_name`,`assigned_to`,`assigned_name`,`sla_id`,`response_deadline`,`resolution_deadline`,`time_created`)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([
            $data['company_id'] ?? 0, $ticketNo, $data['subject'] ?? '', $data['description'] ?? '',
            $data['category'] ?? 'general', $data['priority'] ?? 'medium', 'open',
            $data['client_id'] ?? 0, $data['client_name'] ?? '', $data['assigned_to'] ?? 0,
            $data['assigned_name'] ?? '', $data['sla_id'] ?? 0,
            $data['response_deadline'] ?? null, $data['resolution_deadline'] ?? null, time()
        ]);
        return (int) $db->lastInsertId();
    }

    function epc_tickets_list(PDO $db, int $companyId, string $status = ''): array
    {
        $sql = "SELECT * FROM `epc_tickets` WHERE `company_id` = ?";
        $params = [$companyId];
        if ($status !== '') {
            $sql .= " AND `status` = ?";
            $params[] = $status;
        }
        $sql .= " ORDER BY FIELD(`priority`,'critical','high','medium','low'), `time_created` DESC";
        $stmt = $db->prepare($sql);
        $stmt->execute($params);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_tickets_add_reply(PDO $db, int $ticketId, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_ticket_replies` (`ticket_id`,`author_id`,`author_name`,`author_type`,`message`,`is_internal`,`time_created`) VALUES (?,?,?,?,?,?,?)");
        $stmt->execute([$ticketId, $data['author_id'] ?? 0, $data['author_name'] ?? '', $data['author_type'] ?? 'staff', $data['message'] ?? '', $data['is_internal'] ?? 0, time()]);
        $db->prepare("UPDATE `epc_tickets` SET `time_updated` = ? WHERE `id` = ?")->execute([time(), $ticketId]);
        return (int) $db->lastInsertId();
    }
}
