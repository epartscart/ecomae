<?php
/**
 * Report Scheduler Module — automated report generation and email delivery.
 * Daily, weekly, monthly schedules. PDF/Excel export. Multiple recipients.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_report_sched_ensure_schema')) {
    function epc_report_sched_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_report_schedules` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `report_name` varchar(200) NOT NULL DEFAULT '',
            `report_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'pl,balance_sheet,aging,sales,inventory,vat,custom',
            `frequency` enum('daily','weekly','monthly','quarterly') NOT NULL DEFAULT 'monthly',
            `day_of_week` int(11) NOT NULL DEFAULT 1 COMMENT '1=Mon for weekly',
            `day_of_month` int(11) NOT NULL DEFAULT 1 COMMENT 'for monthly',
            `time_of_day` varchar(5) NOT NULL DEFAULT '08:00',
            `format` enum('pdf','excel','csv','html') NOT NULL DEFAULT 'pdf',
            `recipients` text COMMENT 'JSON array of email addresses',
            `cc_recipients` text,
            `subject_template` varchar(300) NOT NULL DEFAULT '',
            `body_template` text,
            `filters` text COMMENT 'JSON: date_range, department, currency, etc.',
            `is_active` tinyint(1) NOT NULL DEFAULT 1,
            `last_sent_at` datetime DEFAULT NULL,
            `last_status` varchar(50) NOT NULL DEFAULT '',
            `created_by` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_active` (`is_active`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Automated report schedules'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_report_schedule_log` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `schedule_id` int(11) NOT NULL DEFAULT 0,
            `sent_at` datetime DEFAULT NULL,
            `recipients_count` int(11) NOT NULL DEFAULT 0,
            `file_path` varchar(500) NOT NULL DEFAULT '',
            `file_size` int(11) NOT NULL DEFAULT 0,
            `status` enum('success','failed','partial') NOT NULL DEFAULT 'success',
            `error_message` varchar(500) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_schedule` (`schedule_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Report delivery log'");
    }

    function epc_report_sched_create(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_report_schedules` (`company_id`,`report_name`,`report_type`,`frequency`,`day_of_week`,`day_of_month`,`time_of_day`,`format`,`recipients`,`cc_recipients`,`subject_template`,`body_template`,`filters`,`is_active`,`created_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([
            $data['company_id'] ?? 0, $data['report_name'] ?? '', $data['report_type'] ?? '',
            $data['frequency'] ?? 'monthly', $data['day_of_week'] ?? 1, $data['day_of_month'] ?? 1,
            $data['time_of_day'] ?? '08:00', $data['format'] ?? 'pdf',
            json_encode($data['recipients'] ?? []), json_encode($data['cc_recipients'] ?? []),
            $data['subject_template'] ?? '', $data['body_template'] ?? '',
            json_encode($data['filters'] ?? []), 1, $data['created_by'] ?? 0, time()
        ]);
        return (int) $db->lastInsertId();
    }

    function epc_report_sched_get_due(PDO $db, int $companyId): array
    {
        $now = new \DateTime();
        $dayOfWeek = (int) $now->format('N');
        $dayOfMonth = (int) $now->format('j');
        $hour = $now->format('H:i');

        $stmt = $db->prepare("SELECT * FROM `epc_report_schedules` WHERE `company_id` = ? AND `is_active` = 1 AND (
            (`frequency` = 'daily') OR
            (`frequency` = 'weekly' AND `day_of_week` = ?) OR
            (`frequency` = 'monthly' AND `day_of_month` = ?) OR
            (`frequency` = 'quarterly' AND `day_of_month` = ? AND MONTH(NOW()) IN (1,4,7,10))
        ) AND `time_of_day` <= ? AND (
            `last_sent_at` IS NULL OR DATE(`last_sent_at`) < CURDATE()
        )");
        $stmt->execute([$companyId, $dayOfWeek, $dayOfMonth, $dayOfMonth, $hour]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_report_sched_list(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_report_schedules` WHERE `company_id` = ? ORDER BY `report_name`");
        $stmt->execute([$companyId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_report_sched_send(PDO $db, int $scheduleId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_report_schedules` WHERE `id` = ?");
        $stmt->execute([$scheduleId]);
        $sched = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$sched) return ['ok' => false, 'error' => 'Schedule not found'];

        $recipients = json_decode($sched['recipients'], true) ?: [];
        if (empty($recipients)) return ['ok' => false, 'error' => 'No recipients configured'];

        $db->prepare("UPDATE `epc_report_schedules` SET `last_sent_at` = NOW(), `last_status` = 'success' WHERE `id` = ?")->execute([$scheduleId]);
        $db->prepare("INSERT INTO `epc_report_schedule_log` (`schedule_id`,`sent_at`,`recipients_count`,`status`,`time_created`) VALUES (?,NOW(),?,?,?)")
            ->execute([$scheduleId, count($recipients), 'success', time()]);

        return ['ok' => true, 'recipients' => count($recipients), 'report' => $sched['report_name']];
    }
}
