<?php
/**
 * Advanced ERP — Collaboration & Workflow.
 *
 * Two additive capabilities used across every module:
 *   1. Inter-department messaging: threads attached to any record (or
 *      standalone), with messages and per-department routing.
 *   2. Workflow / approval engine: generic multi-step approval requests
 *      (e.g. PO approval, leave request, credit override) with sequential
 *      steps, decisions and an audit-friendly history.
 *
 * Polymorphic via (entity_type, entity_id) so it works for POs, invoices,
 * shipments, leave requests, etc. Nothing existing is modified.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_collab_ensure_schema')) {
    function epc_collab_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_collab_threads` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `entity_type` varchar(40) NOT NULL DEFAULT 'general',
            `entity_id` int(11) NOT NULL DEFAULT 0,
            `subject` varchar(200) NOT NULL DEFAULT '',
            `department` varchar(60) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'open',
            `created_by` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_entity` (`entity_type`,`entity_id`),
            KEY `x_dept` (`department`,`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Inter-department message threads'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_collab_messages` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `thread_id` int(11) NOT NULL,
            `from_admin_id` int(11) NOT NULL DEFAULT 0,
            `to_department` varchar(60) NOT NULL DEFAULT '',
            `body` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_thread` (`thread_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Thread messages'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_wf_requests` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `entity_type` varchar(40) NOT NULL,
            `entity_id` int(11) NOT NULL DEFAULT 0,
            `title` varchar(200) NOT NULL DEFAULT '',
            `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `status` varchar(16) NOT NULL DEFAULT 'pending',
            `current_step` int(11) NOT NULL DEFAULT 1,
            `requested_by` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_entity` (`entity_type`,`entity_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Workflow approval requests'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_wf_steps` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `request_id` int(11) NOT NULL,
            `step_no` int(11) NOT NULL,
            `approver_role` varchar(60) NOT NULL DEFAULT '',
            `approver_admin_id` int(11) NOT NULL DEFAULT 0,
            `decision` varchar(16) NOT NULL DEFAULT 'pending',
            `comment` text,
            `decided_by` int(11) NOT NULL DEFAULT 0,
            `decided_at` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_req_step` (`request_id`,`step_no`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Workflow approval steps'");
    }
}

/* ----------------------------------------------------------- messaging --- */

if (!function_exists('epc_collab_thread_open')) {
    /**
     * @param array<string,mixed> $data entity_type, entity_id, subject, department, created_by
     */
    function epc_collab_thread_open(PDO $db, array $data): int
    {
        epc_collab_ensure_schema($db);
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_collab_threads` (`entity_type`,`entity_id`,`subject`,`department`,`status`,`created_by`,`time_created`,`time_updated`)
             VALUES (?,?,?,?, 'open', ?,?,?)"
        )->execute(array(
            (string) ($data['entity_type'] ?? 'general'),
            (int) ($data['entity_id'] ?? 0),
            (string) ($data['subject'] ?? ''),
            (string) ($data['department'] ?? ''),
            (int) ($data['created_by'] ?? 0),
            $now,
            $now,
        ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_collab_message_post')) {
    /**
     * @param array<string,mixed> $data from_admin_id, to_department, body
     */
    function epc_collab_message_post(PDO $db, int $threadId, array $data): int
    {
        epc_collab_ensure_schema($db);
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_collab_messages` (`thread_id`,`from_admin_id`,`to_department`,`body`,`time_created`) VALUES (?,?,?,?,?)"
        )->execute(array(
            $threadId,
            (int) ($data['from_admin_id'] ?? 0),
            (string) ($data['to_department'] ?? ''),
            (string) ($data['body'] ?? ''),
            $now,
        ));
        $db->prepare("UPDATE `epc_collab_threads` SET `time_updated`=? WHERE `id`=?")->execute(array($now, $threadId));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_collab_thread_messages')) {
    /**
     * @return array<int,array<string,mixed>>
     */
    function epc_collab_thread_messages(PDO $db, int $threadId): array
    {
        epc_collab_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_collab_messages` WHERE `thread_id`=? ORDER BY `id`");
        $st->execute(array($threadId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_collab_thread_close')) {
    function epc_collab_thread_close(PDO $db, int $threadId): void
    {
        epc_collab_ensure_schema($db);
        $db->prepare("UPDATE `epc_collab_threads` SET `status`='closed', `time_updated`=? WHERE `id`=?")
           ->execute(array(time(), $threadId));
    }
}

/* ------------------------------------------------- workflow / approvals --- */

if (!function_exists('epc_wf_request_create')) {
    /**
     * Create an approval request with sequential steps.
     *
     * @param array<string,mixed> $data entity_type, entity_id, title, amount, requested_by
     * @param array<int,array<string,mixed>> $steps each: approver_role, approver_admin_id
     */
    function epc_wf_request_create(PDO $db, array $data, array $steps): int
    {
        epc_collab_ensure_schema($db);
        if (!$steps) {
            throw new Exception('At least one approval step is required');
        }
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_wf_requests` (`entity_type`,`entity_id`,`title`,`amount`,`status`,`current_step`,`requested_by`,`time_created`,`time_updated`)
             VALUES (?,?,?,?, 'pending', 1, ?,?,?)"
        )->execute(array(
            (string) ($data['entity_type'] ?? 'general'),
            (int) ($data['entity_id'] ?? 0),
            (string) ($data['title'] ?? ''),
            round((float) ($data['amount'] ?? 0), 2),
            (int) ($data['requested_by'] ?? 0),
            $now,
            $now,
        ));
        $reqId = (int) $db->lastInsertId();
        $n = 0;
        foreach ($steps as $s) {
            $n++;
            $db->prepare(
                "INSERT INTO `epc_wf_steps` (`request_id`,`step_no`,`approver_role`,`approver_admin_id`,`decision`) VALUES (?,?,?,?, 'pending')"
            )->execute(array(
                $reqId,
                $n,
                (string) ($s['approver_role'] ?? ''),
                (int) ($s['approver_admin_id'] ?? 0),
            ));
        }
        return $reqId;
    }
}

if (!function_exists('epc_wf_decide')) {
    /**
     * Record a decision on the current step. 'approve' advances to the next
     * step (or marks the request approved if it was the last). 'reject' marks
     * the whole request rejected.
     *
     * @return array<string,mixed> updated request state
     */
    function epc_wf_decide(PDO $db, int $requestId, int $stepNo, string $decision, int $decidedBy = 0, string $comment = ''): array
    {
        epc_collab_ensure_schema($db);
        $decision = in_array($decision, array('approve', 'reject'), true) ? $decision : 'approve';

        $req = $db->prepare("SELECT * FROM `epc_wf_requests` WHERE `id`=?");
        $req->execute(array($requestId));
        $request = $req->fetch(PDO::FETCH_ASSOC);
        if (!$request) {
            throw new Exception('Workflow request not found');
        }
        if ($request['status'] !== 'pending') {
            return $request;
        }
        if ((int) $request['current_step'] !== $stepNo) {
            throw new Exception('Not the current approval step');
        }

        $now = time();
        $db->prepare(
            "UPDATE `epc_wf_steps` SET `decision`=?, `comment`=?, `decided_by`=?, `decided_at`=? WHERE `request_id`=? AND `step_no`=?"
        )->execute(array($decision === 'approve' ? 'approved' : 'rejected', $comment, $decidedBy, $now, $requestId, $stepNo));

        if ($decision === 'reject') {
            $db->prepare("UPDATE `epc_wf_requests` SET `status`='rejected', `time_updated`=? WHERE `id`=?")->execute(array($now, $requestId));
        } else {
            $totalSteps = (int) $db->query("SELECT COUNT(*) FROM `epc_wf_steps` WHERE `request_id`=" . (int) $requestId)->fetchColumn();
            if ($stepNo >= $totalSteps) {
                $db->prepare("UPDATE `epc_wf_requests` SET `status`='approved', `time_updated`=? WHERE `id`=?")->execute(array($now, $requestId));
            } else {
                $db->prepare("UPDATE `epc_wf_requests` SET `current_step`=?, `time_updated`=? WHERE `id`=?")->execute(array($stepNo + 1, $now, $requestId));
            }
        }

        $req->execute(array($requestId));
        return $req->fetch(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_wf_request_state')) {
    /**
     * @return array<string,mixed> request header + steps
     */
    function epc_wf_request_state(PDO $db, int $requestId): array
    {
        epc_collab_ensure_schema($db);
        $req = $db->prepare("SELECT * FROM `epc_wf_requests` WHERE `id`=?");
        $req->execute(array($requestId));
        $request = $req->fetch(PDO::FETCH_ASSOC) ?: array();
        $st = $db->prepare("SELECT * FROM `epc_wf_steps` WHERE `request_id`=? ORDER BY `step_no`");
        $st->execute(array($requestId));
        $request['steps'] = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
        return $request;
    }
}

if (!function_exists('epc_wf_pending_for_role')) {
    /**
     * Pending requests whose current step is assigned to a given role.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_wf_pending_for_role(PDO $db, string $role): array
    {
        epc_collab_ensure_schema($db);
        $st = $db->prepare(
            "SELECT r.* FROM `epc_wf_requests` r
             INNER JOIN `epc_wf_steps` s ON s.`request_id`=r.`id` AND s.`step_no`=r.`current_step`
             WHERE r.`status`='pending' AND s.`approver_role`=? ORDER BY r.`id`"
        );
        $st->execute(array($role));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}
