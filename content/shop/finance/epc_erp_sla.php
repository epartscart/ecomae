<?php
/**
 * SLA Agreement Module — service level agreements with clients.
 * Tracks response/resolution times, uptime guarantees, penalty clauses.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_sla_ensure_schema')) {
    function epc_sla_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_sla_agreements` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `sla_code` varchar(32) NOT NULL DEFAULT '',
            `client_name` varchar(200) NOT NULL DEFAULT '',
            `client_id` int(11) NOT NULL DEFAULT 0,
            `service_type` varchar(100) NOT NULL DEFAULT '',
            `response_hours` decimal(6,2) NOT NULL DEFAULT 4.00,
            `resolution_hours` decimal(6,2) NOT NULL DEFAULT 24.00,
            `uptime_pct` decimal(5,2) NOT NULL DEFAULT 99.50,
            `penalty_type` varchar(50) NOT NULL DEFAULT 'credit_note',
            `penalty_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `start_date` date DEFAULT NULL,
            `end_date` date DEFAULT NULL,
            `status` enum('active','expiring','expired','suspended') NOT NULL DEFAULT 'active',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_client` (`client_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='SLA agreements'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_sla_breaches` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `sla_id` int(11) NOT NULL DEFAULT 0,
            `breach_type` varchar(50) NOT NULL DEFAULT 'response',
            `occurred_at` datetime DEFAULT NULL,
            `resolved_at` datetime DEFAULT NULL,
            `actual_hours` decimal(6,2) NOT NULL DEFAULT 0.00,
            `target_hours` decimal(6,2) NOT NULL DEFAULT 0.00,
            `penalty_applied` decimal(14,2) NOT NULL DEFAULT 0.00,
            `ticket_ref` varchar(64) NOT NULL DEFAULT '',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_sla` (`sla_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='SLA breach log'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_sla_templates` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(200) NOT NULL DEFAULT '',
            `response_hours` decimal(6,2) NOT NULL DEFAULT 4.00,
            `resolution_hours` decimal(6,2) NOT NULL DEFAULT 24.00,
            `uptime_pct` decimal(5,2) NOT NULL DEFAULT 99.50,
            `penalty_type` varchar(50) NOT NULL DEFAULT 'credit_note',
            `penalty_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='SLA templates'");
    }

    function epc_sla_list(PDO $db, int $companyId = 0): array
    {
        $sql = "SELECT * FROM `epc_sla_agreements` WHERE `company_id` = ? ORDER BY `start_date` DESC";
        $stmt = $db->prepare($sql);
        $stmt->execute([$companyId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_sla_create(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_sla_agreements`
            (`company_id`,`sla_code`,`client_name`,`client_id`,`service_type`,`response_hours`,`resolution_hours`,`uptime_pct`,`penalty_type`,`penalty_amount`,`start_date`,`end_date`,`status`,`notes`,`time_created`)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([
            $data['company_id'] ?? 0, $data['sla_code'] ?? '', $data['client_name'] ?? '',
            $data['client_id'] ?? 0, $data['service_type'] ?? '', $data['response_hours'] ?? 4,
            $data['resolution_hours'] ?? 24, $data['uptime_pct'] ?? 99.5, $data['penalty_type'] ?? 'credit_note',
            $data['penalty_amount'] ?? 0, $data['start_date'] ?? null, $data['end_date'] ?? null,
            $data['status'] ?? 'active', $data['notes'] ?? '', time()
        ]);
        return (int) $db->lastInsertId();
    }

    function epc_sla_check_compliance(PDO $db, int $slaId): array
    {
        $stmt = $db->prepare("SELECT COUNT(*) as total, SUM(CASE WHEN `penalty_applied` > 0 THEN 1 ELSE 0 END) as breached FROM `epc_sla_breaches` WHERE `sla_id` = ?");
        $stmt->execute([$slaId]);
        return $stmt->fetch(PDO::FETCH_ASSOC) ?: ['total' => 0, 'breached' => 0];
    }
}
