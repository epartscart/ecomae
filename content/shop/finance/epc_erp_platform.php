<?php
/**
 * Platform / cross-cutting services — enterprise batch jobs (batch job
 * definitions + run history + recurrence) and feature management (feature
 * flags toggled per company).
 *
 * Complements the existing workflow engine (epc_bos_workflow) and data-entity /
 * OData layer (epc_erp_integration). The recurrence + status helpers are pure
 * so scheduling math is deterministic & testable. Multi-company aware. Additive.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_plt_batch_statuses')) {
    /** @return array<int,string> */
    function epc_plt_batch_statuses(): array
    {
        return array('waiting', 'executing', 'ended', 'error', 'canceled');
    }
}

if (!function_exists('epc_plt_ensure_schema')) {
    function epc_plt_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_plt_batch_job` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(60) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `recurrence_min` int(11) NOT NULL DEFAULT 0,
            `status` varchar(12) NOT NULL DEFAULT 'waiting',
            `last_run` int(11) NOT NULL DEFAULT 0,
            `next_run` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Batch job definitions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_plt_batch_run` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `job_id` int(11) NOT NULL DEFAULT 0,
            `started` int(11) NOT NULL DEFAULT 0,
            `ended` int(11) NOT NULL DEFAULT 0,
            `status` varchar(12) NOT NULL DEFAULT 'ended',
            `message` varchar(255) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            KEY `x_job` (`job_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Batch job run history'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_plt_feature` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(60) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `enabled` tinyint(1) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Feature management flags'");
    }
}

/* ---------------- pure scheduling ---------------- */

if (!function_exists('epc_plt_next_run')) {
    /**
     * Pure: next run timestamp = from + recurrence (minutes). recurrence <= 0
     * means one-time (no next run -> 0).
     */
    function epc_plt_next_run(int $from, int $recurrenceMin): int
    {
        if ($recurrenceMin <= 0) {
            return 0;
        }
        return $from + ($recurrenceMin * 60);
    }
}

if (!function_exists('epc_plt_is_due')) {
    /** Pure: is the job due at $now? (active, has a next_run, next_run <= now) */
    function epc_plt_is_due(int $nextRun, int $now): bool
    {
        return $nextRun > 0 && $nextRun <= $now;
    }
}

/* ---------------- batch jobs ---------------- */

if (!function_exists('epc_plt_batch_job_save')) {
    /** @param array<string,mixed> $data */
    function epc_plt_batch_job_save(PDO $db, int $companyId, array $data): int
    {
        epc_plt_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Job code is required');
        }
        $rec = max(0, (int) ($data['recurrence_min'] ?? 0));
        $active = !empty($data['active']) ? 1 : 0;
        $next = $active ? epc_plt_next_run(time(), $rec) : 0;
        $db->prepare("INSERT INTO `epc_plt_batch_job` (`company_id`,`code`,`name`,`recurrence_min`,`status`,`next_run`,`active`,`time_updated`)
                      VALUES (?,?,?,?, 'waiting', ?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `recurrence_min`=VALUES(`recurrence_min`), `active`=VALUES(`active`), `next_run`=VALUES(`next_run`), `time_updated`=VALUES(`time_updated`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? ''), $rec, $next, $active, time()));
        $st = $db->prepare("SELECT id FROM `epc_plt_batch_job` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_plt_batch_jobs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_plt_batch_jobs(PDO $db, int $companyId = 0): array
    {
        epc_plt_ensure_schema($db);
        $sql = "SELECT * FROM `epc_plt_batch_job` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY code";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['run_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_plt_batch_run` WHERE job_id=" . (int) $r['id'])->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_plt_batch_run')) {
    /**
     * Record a job execution: logs a run row and rolls the job's last_run /
     * next_run (per recurrence) and status.
     *
     * @return array{run_id:int,status:string,next_run:int}
     */
    function epc_plt_batch_run(PDO $db, int $jobId, string $status = 'ended', string $message = ''): array
    {
        epc_plt_ensure_schema($db);
        if (!in_array($status, epc_plt_batch_statuses(), true)) {
            throw new Exception('Invalid batch status');
        }
        $st = $db->prepare("SELECT * FROM `epc_plt_batch_job` WHERE id=?");
        $st->execute(array($jobId));
        $job = $st->fetch(PDO::FETCH_ASSOC);
        if (!$job) {
            throw new Exception('Job not found');
        }
        $now = time();
        $db->prepare("INSERT INTO `epc_plt_batch_run` (`job_id`,`started`,`ended`,`status`,`message`) VALUES (?,?,?,?,?)")
           ->execute(array($jobId, $now, $now, $status, $message));
        $runId = (int) $db->lastInsertId();
        $next = $status === 'ended' ? epc_plt_next_run($now, (int) $job['recurrence_min']) : (int) $job['next_run'];
        $db->prepare("UPDATE `epc_plt_batch_job` SET `last_run`=?, `next_run`=?, `status`=? WHERE id=?")
           ->execute(array($now, $next, $status, $jobId));
        return array('run_id' => $runId, 'status' => $status, 'next_run' => $next);
    }
}

if (!function_exists('epc_plt_batch_runs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_plt_batch_runs(PDO $db, int $jobId, int $limit = 20): array
    {
        epc_plt_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_plt_batch_run` WHERE job_id=? ORDER BY id DESC LIMIT " . max(1, (int) $limit));
        $st->execute(array($jobId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- feature management ---------------- */

if (!function_exists('epc_plt_feature_save')) {
    /** @param array<string,mixed> $data */
    function epc_plt_feature_save(PDO $db, int $companyId, array $data): int
    {
        epc_plt_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Feature code is required');
        }
        $enabled = !empty($data['enabled']) ? 1 : 0;
        $db->prepare("INSERT INTO `epc_plt_feature` (`company_id`,`code`,`name`,`enabled`,`time_updated`) VALUES (?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `enabled`=VALUES(`enabled`), `time_updated`=VALUES(`time_updated`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? ''), $enabled, time()));
        $st = $db->prepare("SELECT id FROM `epc_plt_feature` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_plt_feature_toggle')) {
    function epc_plt_feature_toggle(PDO $db, int $companyId, string $code, bool $enabled): void
    {
        epc_plt_ensure_schema($db);
        $db->prepare("UPDATE `epc_plt_feature` SET `enabled`=?, `time_updated`=? WHERE company_id=? AND code=?")
           ->execute(array($enabled ? 1 : 0, time(), $companyId, $code));
    }
}

if (!function_exists('epc_plt_feature_enabled')) {
    function epc_plt_feature_enabled(PDO $db, int $companyId, string $code): bool
    {
        epc_plt_ensure_schema($db);
        $st = $db->prepare("SELECT enabled FROM `epc_plt_feature` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        $v = $st->fetchColumn();
        return $v !== false && (int) $v === 1;
    }
}

if (!function_exists('epc_plt_features')) {
    /** @return array<int,array<string,mixed>> */
    function epc_plt_features(PDO $db, int $companyId = 0): array
    {
        epc_plt_ensure_schema($db);
        $sql = "SELECT * FROM `epc_plt_feature` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY code";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_plt_summary')) {
    /** @return array{jobs:int,active_jobs:int,runs:int,features:int,features_on:int} */
    function epc_plt_summary(PDO $db, int $companyId = 0): array
    {
        epc_plt_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        $jobs = (int) $db->query("SELECT COUNT(*) FROM `epc_plt_batch_job` WHERE 1=1" . $wa)->fetchColumn();
        $active = (int) $db->query("SELECT COUNT(*) FROM `epc_plt_batch_job` WHERE active=1" . $wa)->fetchColumn();
        $feat = (int) $db->query("SELECT COUNT(*) FROM `epc_plt_feature` WHERE 1=1" . $wa)->fetchColumn();
        $featOn = (int) $db->query("SELECT COUNT(*) FROM `epc_plt_feature` WHERE enabled=1" . $wa)->fetchColumn();
        $runs = (int) $db->query("SELECT COUNT(*) FROM `epc_plt_batch_run` r JOIN `epc_plt_batch_job` j ON j.id=r.job_id WHERE 1=1" . ($companyId > 0 ? " AND j.company_id=" . $companyId : ""))->fetchColumn();
        return array('jobs' => $jobs, 'active_jobs' => $active, 'runs' => $runs, 'features' => $feat, 'features_on' => $featOn);
    }
}
