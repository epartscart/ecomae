<?php
/**
 * Customer Group / Type Module — classify customers for reporting and pricing.
 * Enables group-based reports, tiered pricing, credit policies.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_cust_groups_ensure_schema')) {
    function epc_cust_groups_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_customer_groups` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `group_code` varchar(32) NOT NULL DEFAULT '',
            `group_name` varchar(200) NOT NULL DEFAULT '',
            `group_type` varchar(50) NOT NULL DEFAULT 'general' COMMENT 'general,vip,wholesale,retail,corporate,government',
            `discount_pct` decimal(5,2) NOT NULL DEFAULT 0.00,
            `credit_limit` decimal(14,2) NOT NULL DEFAULT 0.00,
            `payment_terms_days` int(11) NOT NULL DEFAULT 30,
            `price_list_id` int(11) NOT NULL DEFAULT 0,
            `description` text,
            `is_active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_type` (`group_type`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customer groups/types'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_customer_group_members` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `group_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `time_added` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `ux_group_cust` (`group_id`, `customer_id`),
            KEY `x_customer` (`customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customer group membership'");
    }

    function epc_cust_groups_list(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT g.*, (SELECT COUNT(*) FROM `epc_customer_group_members` m WHERE m.`group_id` = g.`id`) AS member_count FROM `epc_customer_groups` g WHERE g.`company_id` = ? ORDER BY g.`group_name`");
        $stmt->execute([$companyId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_cust_groups_create(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_customer_groups` (`company_id`,`group_code`,`group_name`,`group_type`,`discount_pct`,`credit_limit`,`payment_terms_days`,`price_list_id`,`description`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['group_code'] ?? '', $data['group_name'] ?? '', $data['group_type'] ?? 'general', $data['discount_pct'] ?? 0, $data['credit_limit'] ?? 0, $data['payment_terms_days'] ?? 30, $data['price_list_id'] ?? 0, $data['description'] ?? '', time()]);
        return (int) $db->lastInsertId();
    }

    function epc_cust_groups_assign(PDO $db, int $groupId, int $customerId): bool
    {
        $stmt = $db->prepare("INSERT IGNORE INTO `epc_customer_group_members` (`group_id`,`customer_id`,`time_added`) VALUES (?,?,?)");
        return $stmt->execute([$groupId, $customerId, time()]);
    }

    function epc_cust_groups_get_customer_group(PDO $db, int $customerId): ?array
    {
        $stmt = $db->prepare("SELECT g.* FROM `epc_customer_groups` g JOIN `epc_customer_group_members` m ON m.`group_id` = g.`id` WHERE m.`customer_id` = ? LIMIT 1");
        $stmt->execute([$customerId]);
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }

    function epc_cust_groups_delete(PDO $db, int $groupId, int $companyId): bool
    {
        $stmt = $db->prepare("DELETE FROM `epc_customer_groups` WHERE `id` = ? AND `company_id` = ?");
        $ok = $stmt->execute([$groupId, $companyId]);
        $db->prepare("DELETE FROM `epc_customer_group_members` WHERE `group_id` = ?")->execute([$groupId]);
        return $ok;
    }
}
