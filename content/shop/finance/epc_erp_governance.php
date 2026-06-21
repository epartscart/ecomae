<?php
/**
 * Advanced ERP — Governance: roles & permissions, notifications, questionnaire.
 *
 * - Roles with granular permissions (module.action), user-role assignment,
 *   permission checks honouring a superadmin wildcard.
 * - Notifications: per-user inbox, mark-read, unread count, broadcast.
 * - Questionnaire/surveys: definitions with questions, responses, scoring.
 *
 * Additive: new epc_gov_*, epc_ntf_*, epc_qn_* tables. Tenant-isolated.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_gov_ensure_schema')) {
    function epc_gov_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_gov_roles` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(120) NOT NULL DEFAULT '',
            `permissions` mediumtext,
            `is_system` tinyint(1) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Roles'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_gov_user_roles` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `user_id` int(11) NOT NULL,
            `role_id` int(11) NOT NULL,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_user_role` (`user_id`,`role_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User-role map'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_ntf_notifications` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `user_id` int(11) NOT NULL,
            `title` varchar(190) NOT NULL DEFAULT '',
            `body` text,
            `link` varchar(255) DEFAULT NULL,
            `severity` varchar(12) NOT NULL DEFAULT 'info',
            `is_read` tinyint(1) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_user` (`user_id`,`is_read`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Notifications'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qn_questionnaires` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `title` varchar(190) NOT NULL DEFAULT '',
            `questions` mediumtext,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Questionnaires'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_qn_responses` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `questionnaire_id` int(11) NOT NULL,
            `respondent` varchar(120) DEFAULT NULL,
            `answers` mediumtext,
            `score` decimal(10,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_qn` (`questionnaire_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Questionnaire responses'");
    }
}

/* --------------------------- Roles & access --------------------------- */

if (!function_exists('epc_gov_role_save')) {
    /**
     * @param array<int,string> $permissions e.g. ['sales.view','sales.create']; ['*'] = all
     */
    function epc_gov_role_save(PDO $db, string $code, string $name, array $permissions, bool $isSystem = false): int
    {
        epc_gov_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_gov_roles` (`code`,`name`,`permissions`,`is_system`) VALUES (?,?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `permissions`=VALUES(`permissions`)")
           ->execute(array($code, $name, json_encode(array_values($permissions)), $isSystem ? 1 : 0));
        $r = $db->prepare("SELECT `id` FROM `epc_gov_roles` WHERE `code`=?");
        $r->execute(array($code));
        return (int) $r->fetchColumn();
    }
}

if (!function_exists('epc_gov_assign_role')) {
    function epc_gov_assign_role(PDO $db, int $userId, int $roleId): void
    {
        epc_gov_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_gov_user_roles` (`user_id`,`role_id`) VALUES (?,?)
                      ON DUPLICATE KEY UPDATE `role_id`=VALUES(`role_id`)")
           ->execute(array($userId, $roleId));
    }
}

if (!function_exists('epc_gov_user_permissions')) {
    /**
     * Union of permissions across a user's roles.
     *
     * @return array<int,string>
     */
    function epc_gov_user_permissions(PDO $db, int $userId): array
    {
        epc_gov_ensure_schema($db);
        $st = $db->prepare("SELECT r.`permissions` FROM `epc_gov_user_roles` ur JOIN `epc_gov_roles` r ON r.`id`=ur.`role_id` WHERE ur.`user_id`=?");
        $st->execute(array($userId));
        $perms = array();
        foreach ($st->fetchAll(PDO::FETCH_COLUMN) as $json) {
            foreach ((array) json_decode((string) $json, true) as $p) {
                $perms[(string) $p] = true;
            }
        }
        return array_keys($perms);
    }
}

if (!function_exists('epc_gov_can')) {
    /**
     * Permission check. '*' grants everything; 'module.*' grants all actions in
     * a module; otherwise an exact 'module.action' match is required.
     */
    function epc_gov_can(PDO $db, int $userId, string $permission): bool
    {
        $perms = epc_gov_user_permissions($db, $userId);
        if (in_array('*', $perms, true)) {
            return true;
        }
        if (in_array($permission, $perms, true)) {
            return true;
        }
        $dot = strpos($permission, '.');
        if ($dot !== false) {
            $module = substr($permission, 0, $dot);
            if (in_array($module . '.*', $perms, true)) {
                return true;
            }
        }
        return false;
    }
}

/* ---------------------------- Notifications --------------------------- */

if (!function_exists('epc_ntf_push')) {
    /** @param array<string,mixed> $data title, body, link, severity */
    function epc_ntf_push(PDO $db, int $userId, array $data): int
    {
        epc_gov_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_ntf_notifications` (`user_id`,`title`,`body`,`link`,`severity`,`is_read`,`time_created`) VALUES (?,?,?,?,?,0,?)")
           ->execute(array($userId, (string) ($data['title'] ?? ''), (string) ($data['body'] ?? ''), (string) ($data['link'] ?? ''), (string) ($data['severity'] ?? 'info'), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_ntf_broadcast')) {
    /**
     * Push the same notification to many users.
     *
     * @param array<int,int> $userIds
     * @param array<string,mixed> $data
     * @return int count pushed
     */
    function epc_ntf_broadcast(PDO $db, array $userIds, array $data): int
    {
        $n = 0;
        foreach ($userIds as $uid) {
            epc_ntf_push($db, (int) $uid, $data);
            $n++;
        }
        return $n;
    }
}

if (!function_exists('epc_ntf_unread_count')) {
    function epc_ntf_unread_count(PDO $db, int $userId): int
    {
        epc_gov_ensure_schema($db);
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_ntf_notifications` WHERE `user_id`=? AND `is_read`=0");
        $st->execute(array($userId));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_ntf_mark_read')) {
    function epc_ntf_mark_read(PDO $db, int $notificationId): void
    {
        epc_gov_ensure_schema($db);
        $db->prepare("UPDATE `epc_ntf_notifications` SET `is_read`=1 WHERE `id`=?")->execute(array($notificationId));
    }
}

if (!function_exists('epc_ntf_mark_all_read')) {
    function epc_ntf_mark_all_read(PDO $db, int $userId): int
    {
        epc_gov_ensure_schema($db);
        $st = $db->prepare("UPDATE `epc_ntf_notifications` SET `is_read`=1 WHERE `user_id`=? AND `is_read`=0");
        $st->execute(array($userId));
        return $st->rowCount();
    }
}

/* ---------------------------- Questionnaire --------------------------- */

if (!function_exists('epc_qn_save')) {
    /**
     * Save a questionnaire. Each question: {key, text, type, weight?, options?}.
     *
     * @param array<int,array<string,mixed>> $questions
     */
    function epc_qn_save(PDO $db, string $code, string $title, array $questions): int
    {
        epc_gov_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_qn_questionnaires` (`code`,`title`,`questions`,`active`) VALUES (?,?,?,1)
                      ON DUPLICATE KEY UPDATE `title`=VALUES(`title`), `questions`=VALUES(`questions`)")
           ->execute(array($code, $title, json_encode($questions)));
        $r = $db->prepare("SELECT `id` FROM `epc_qn_questionnaires` WHERE `code`=?");
        $r->execute(array($code));
        return (int) $r->fetchColumn();
    }
}

if (!function_exists('epc_qn_submit')) {
    /**
     * Submit a response. Score = sum over numeric answers of (answer * weight).
     *
     * @param array<string,mixed> $answers key => value
     * @return array<string,mixed> {response_id, score}
     */
    function epc_qn_submit(PDO $db, int $questionnaireId, string $respondent, array $answers): array
    {
        epc_gov_ensure_schema($db);
        $q = $db->prepare("SELECT `questions` FROM `epc_qn_questionnaires` WHERE `id`=?");
        $q->execute(array($questionnaireId));
        $questions = (array) json_decode((string) $q->fetchColumn(), true);
        $weights = array();
        foreach ($questions as $qq) {
            $weights[(string) ($qq['key'] ?? '')] = (float) ($qq['weight'] ?? 0);
        }
        $score = 0.0;
        foreach ($answers as $k => $v) {
            if (is_numeric($v) && isset($weights[$k])) {
                $score = round($score + (float) $v * $weights[$k], 2);
            }
        }
        $db->prepare("INSERT INTO `epc_qn_responses` (`questionnaire_id`,`respondent`,`answers`,`score`,`time_created`) VALUES (?,?,?,?,?)")
           ->execute(array($questionnaireId, $respondent, json_encode($answers), $score, time()));
        return array('response_id' => (int) $db->lastInsertId(), 'score' => $score);
    }
}

if (!function_exists('epc_qn_summary')) {
    /**
     * Response count + average score for a questionnaire.
     *
     * @return array<string,mixed>
     */
    function epc_qn_summary(PDO $db, int $questionnaireId): array
    {
        epc_gov_ensure_schema($db);
        $st = $db->prepare("SELECT COUNT(*) c, COALESCE(AVG(`score`),0) a FROM `epc_qn_responses` WHERE `questionnaire_id`=?");
        $st->execute(array($questionnaireId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return array('responses' => (int) $row['c'], 'avg_score' => round((float) $row['a'], 2));
    }
}
