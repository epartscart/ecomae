<?php
/**
 * Shortcut Icon Builder — personalized quick-access shortcuts per user.
 * Drag-drop reorder, icon selection, link to any ERP/CP page.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_shortcuts_ensure_schema')) {
    function epc_shortcuts_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_user_shortcuts` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `user_id` int(11) NOT NULL DEFAULT 0,
            `label` varchar(100) NOT NULL DEFAULT '',
            `icon_class` varchar(100) NOT NULL DEFAULT 'fa fa-star',
            `icon_color` varchar(20) NOT NULL DEFAULT '#3498db',
            `target_url` varchar(500) NOT NULL DEFAULT '',
            `target_tab` varchar(50) NOT NULL DEFAULT '' COMMENT 'ERP tab name if applicable',
            `sort_order` int(11) NOT NULL DEFAULT 0,
            `is_pinned` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_user` (`user_id`),
            KEY `x_company` (`company_id`),
            KEY `x_sort` (`sort_order`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User shortcut icons'");
    }

    function epc_shortcuts_list(PDO $db, int $userId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_user_shortcuts` WHERE `user_id` = ? ORDER BY `sort_order` ASC, `time_created` ASC");
        $stmt->execute([$userId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }

    function epc_shortcuts_add(PDO $db, array $data): int
    {
        $maxSort = 0;
        $stmt = $db->prepare("SELECT MAX(`sort_order`) FROM `epc_user_shortcuts` WHERE `user_id` = ?");
        $stmt->execute([$data['user_id'] ?? 0]);
        $maxSort = (int) $stmt->fetchColumn() + 1;

        $stmt = $db->prepare("INSERT INTO `epc_user_shortcuts` (`company_id`,`user_id`,`label`,`icon_class`,`icon_color`,`target_url`,`target_tab`,`sort_order`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['user_id'] ?? 0, $data['label'] ?? '', $data['icon_class'] ?? 'fa fa-star', $data['icon_color'] ?? '#3498db', $data['target_url'] ?? '', $data['target_tab'] ?? '', $maxSort, time()]);
        return (int) $db->lastInsertId();
    }

    function epc_shortcuts_reorder(PDO $db, int $userId, array $ids): bool
    {
        foreach ($ids as $order => $id) {
            $db->prepare("UPDATE `epc_user_shortcuts` SET `sort_order` = ? WHERE `id` = ? AND `user_id` = ?")->execute([$order, $id, $userId]);
        }
        return true;
    }

    function epc_shortcuts_delete(PDO $db, int $id, int $userId): bool
    {
        $db->prepare("DELETE FROM `epc_user_shortcuts` WHERE `id` = ? AND `user_id` = ?")->execute([$id, $userId]);
        return true;
    }
}
