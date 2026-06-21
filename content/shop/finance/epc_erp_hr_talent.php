<?php
/**
 * Human resources depth — recruitment & performance management.
 *
 * Recruitment: job requisition -> applicants -> stage pipeline (applied ->
 *   screening -> interview -> offer -> hired/rejected). Hiring fills headcount
 *   and auto-closes the requisition when full.
 * Performance: review -> weighted goals -> finalize (weighted overall rating).
 *
 * Additive: new epc_hrt_* tables. Multi-company aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_hrt_ensure_schema')) {
    function epc_hrt_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hrt_job` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `title` varchar(160) NOT NULL DEFAULT '',
            `department` varchar(120) NOT NULL DEFAULT '',
            `headcount` int(11) NOT NULL DEFAULT 1,
            `hired` int(11) NOT NULL DEFAULT 0,
            `status` varchar(20) NOT NULL DEFAULT 'open',
            `hiring_manager` varchar(160) NOT NULL DEFAULT '',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Job requisitions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hrt_applicant` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `job_id` int(11) NOT NULL DEFAULT 0,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `email` varchar(160) NOT NULL DEFAULT '',
            `phone` varchar(60) NOT NULL DEFAULT '',
            `stage` varchar(20) NOT NULL DEFAULT 'applied',
            `rating` int(11) NOT NULL DEFAULT 0,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_job` (`job_id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Recruitment applicants'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hrt_review` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `employee_id` int(11) NOT NULL DEFAULT 0,
            `employee_name` varchar(160) NOT NULL DEFAULT '',
            `period` varchar(40) NOT NULL DEFAULT '',
            `status` varchar(20) NOT NULL DEFAULT 'draft',
            `reviewer` varchar(160) NOT NULL DEFAULT '',
            `overall_rating` decimal(5,2) NOT NULL DEFAULT 0.00,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Performance reviews'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_hrt_goal` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `review_id` int(11) NOT NULL DEFAULT 0,
            `title` varchar(200) NOT NULL DEFAULT '',
            `weight` decimal(8,2) NOT NULL DEFAULT 1.00,
            `target` varchar(200) NOT NULL DEFAULT '',
            `rating` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_review` (`review_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Performance review goals'");
    }
}

/* ---------------- recruitment ---------------- */

if (!function_exists('epc_hrt_applicant_stages')) {
    /** @return array<int,string> ordered pipeline (rejected is terminal, off-pipeline) */
    function epc_hrt_applicant_stages(): array
    {
        return array('applied', 'screening', 'interview', 'offer', 'hired');
    }
}

if (!function_exists('epc_hrt_job_save')) {
    /** @param array<string,mixed> $data company_id, title, department, headcount, hiring_manager, notes */
    function epc_hrt_job_save(PDO $db, array $data, int $id = 0): int
    {
        epc_hrt_ensure_schema($db);
        $title = trim((string) ($data['title'] ?? ''));
        if ($title === '') {
            throw new Exception('Job title is required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_hrt_job` SET `title`=?, `department`=?, `headcount`=?, `hiring_manager`=?, `notes`=? WHERE `id`=?")
               ->execute(array($title, (string) ($data['department'] ?? ''), max(1, (int) ($data['headcount'] ?? 1)), (string) ($data['hiring_manager'] ?? ''), (string) ($data['notes'] ?? ''), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_hrt_job` (`company_id`,`title`,`department`,`headcount`,`hired`,`status`,`hiring_manager`,`notes`,`time_created`) VALUES (?,?,?,?,0,'open',?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $title, (string) ($data['department'] ?? ''), max(1, (int) ($data['headcount'] ?? 1)), (string) ($data['hiring_manager'] ?? ''), (string) ($data['notes'] ?? ''), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_hrt_job_get')) {
    /** @return array<string,mixed>|null */
    function epc_hrt_job_get(PDO $db, int $id): ?array
    {
        epc_hrt_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_hrt_job` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_hrt_jobs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_hrt_jobs(PDO $db, int $companyId, string $status = ''): array
    {
        epc_hrt_ensure_schema($db);
        $sql = "SELECT * FROM `epc_hrt_job` WHERE `company_id`=?";
        $args = array($companyId);
        if ($status !== '') {
            $sql .= " AND `status`=?";
            $args[] = $status;
        }
        $sql .= " ORDER BY `id` DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_hrt_job_set_status')) {
    function epc_hrt_job_set_status(PDO $db, int $id, string $status): void
    {
        epc_hrt_ensure_schema($db);
        if (!in_array($status, array('open', 'on_hold', 'filled', 'closed'), true)) {
            throw new Exception('Invalid job status');
        }
        $db->prepare("UPDATE `epc_hrt_job` SET `status`=? WHERE `id`=?")->execute(array($status, $id));
    }
}

if (!function_exists('epc_hrt_applicant_add')) {
    /** @param array<string,mixed> $data name, email, phone, rating, notes */
    function epc_hrt_applicant_add(PDO $db, int $jobId, array $data): int
    {
        epc_hrt_ensure_schema($db);
        $job = epc_hrt_job_get($db, $jobId);
        if (!$job) {
            throw new Exception('Job requisition not found');
        }
        $name = trim((string) ($data['name'] ?? ''));
        if ($name === '') {
            throw new Exception('Applicant name is required');
        }
        $now = time();
        $db->prepare("INSERT INTO `epc_hrt_applicant` (`job_id`,`company_id`,`name`,`email`,`phone`,`stage`,`rating`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,'applied',?,?,?,?)")
           ->execute(array($jobId, (int) $job['company_id'], $name, (string) ($data['email'] ?? ''), (string) ($data['phone'] ?? ''), (int) ($data['rating'] ?? 0), (string) ($data['notes'] ?? ''), $now, $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_hrt_applicants')) {
    /** @return array<int,array<string,mixed>> */
    function epc_hrt_applicants(PDO $db, int $jobId): array
    {
        epc_hrt_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_hrt_applicant` WHERE `job_id`=? ORDER BY `id` ASC");
        $st->execute(array($jobId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_hrt_applicant_get')) {
    /** @return array<string,mixed>|null */
    function epc_hrt_applicant_get(PDO $db, int $id): ?array
    {
        epc_hrt_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_hrt_applicant` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_hrt_applicant_set_stage')) {
    /** Move an applicant to a stage; hiring fills headcount and may close the job. */
    function epc_hrt_applicant_set_stage(PDO $db, int $id, string $stage): void
    {
        epc_hrt_ensure_schema($db);
        $valid = array_merge(epc_hrt_applicant_stages(), array('rejected'));
        if (!in_array($stage, $valid, true)) {
            throw new Exception('Invalid applicant stage');
        }
        $app = epc_hrt_applicant_get($db, $id);
        if (!$app) {
            throw new Exception('Applicant not found');
        }
        $wasHired = ($app['stage'] === 'hired');
        $db->prepare("UPDATE `epc_hrt_applicant` SET `stage`=?, `time_updated`=? WHERE `id`=?")->execute(array($stage, time(), $id));
        if ($stage === 'hired' && !$wasHired) {
            $job = epc_hrt_job_get($db, (int) $app['job_id']);
            if ($job) {
                $hired = (int) $job['hired'] + 1;
                $status = $hired >= (int) $job['headcount'] ? 'filled' : $job['status'];
                $db->prepare("UPDATE `epc_hrt_job` SET `hired`=?, `status`=? WHERE `id`=?")->execute(array($hired, $status, (int) $job['id']));
            }
        }
    }
}

/* ---------------- performance ---------------- */

if (!function_exists('epc_hrt_review_save')) {
    /** @param array<string,mixed> $data company_id, employee_id, employee_name, period, reviewer, notes */
    function epc_hrt_review_save(PDO $db, array $data, int $id = 0): int
    {
        epc_hrt_ensure_schema($db);
        $name = trim((string) ($data['employee_name'] ?? ''));
        if ($name === '' && (int) ($data['employee_id'] ?? 0) <= 0) {
            throw new Exception('Employee name or id is required');
        }
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_hrt_review` SET `employee_id`=?, `employee_name`=?, `period`=?, `reviewer`=?, `notes`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array((int) ($data['employee_id'] ?? 0), $name, (string) ($data['period'] ?? ''), (string) ($data['reviewer'] ?? ''), (string) ($data['notes'] ?? ''), $now, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_hrt_review` (`company_id`,`employee_id`,`employee_name`,`period`,`status`,`reviewer`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,'draft',?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['employee_id'] ?? 0), $name, (string) ($data['period'] ?? ''), (string) ($data['reviewer'] ?? ''), (string) ($data['notes'] ?? ''), $now, $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_hrt_review_get')) {
    /** @return array<string,mixed>|null */
    function epc_hrt_review_get(PDO $db, int $id): ?array
    {
        epc_hrt_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_hrt_review` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_hrt_reviews')) {
    /** @return array<int,array<string,mixed>> */
    function epc_hrt_reviews(PDO $db, int $companyId, string $status = ''): array
    {
        epc_hrt_ensure_schema($db);
        $sql = "SELECT * FROM `epc_hrt_review` WHERE `company_id`=?";
        $args = array($companyId);
        if ($status !== '') {
            $sql .= " AND `status`=?";
            $args[] = $status;
        }
        $sql .= " ORDER BY `id` DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_hrt_goal_add')) {
    /** @param array<string,mixed> $data title, weight, target, rating */
    function epc_hrt_goal_add(PDO $db, int $reviewId, array $data): int
    {
        epc_hrt_ensure_schema($db);
        $review = epc_hrt_review_get($db, $reviewId);
        if (!$review) {
            throw new Exception('Review not found');
        }
        if ($review['status'] === 'completed') {
            throw new Exception('Cannot add goals to a completed review');
        }
        $title = trim((string) ($data['title'] ?? ''));
        if ($title === '') {
            throw new Exception('Goal title is required');
        }
        $rating = (int) ($data['rating'] ?? 0);
        if ($rating < 0 || $rating > 5) {
            throw new Exception('Rating must be 0-5');
        }
        $db->prepare("INSERT INTO `epc_hrt_goal` (`review_id`,`title`,`weight`,`target`,`rating`) VALUES (?,?,?,?,?)")
           ->execute(array($reviewId, $title, max(0.0, (float) ($data['weight'] ?? 1)), (string) ($data['target'] ?? ''), $rating));
        $gid = (int) $db->lastInsertId();
        if ($review['status'] === 'draft') {
            $db->prepare("UPDATE `epc_hrt_review` SET `status`='in_progress', `time_updated`=? WHERE `id`=?")->execute(array(time(), $reviewId));
        }
        return $gid;
    }
}

if (!function_exists('epc_hrt_goals')) {
    /** @return array<int,array<string,mixed>> */
    function epc_hrt_goals(PDO $db, int $reviewId): array
    {
        epc_hrt_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_hrt_goal` WHERE `review_id`=? ORDER BY `id` ASC");
        $st->execute(array($reviewId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_hrt_review_weighted_rating')) {
    /** Weighted overall rating = sum(weight*rating)/sum(weight). */
    function epc_hrt_review_weighted_rating(PDO $db, int $reviewId): float
    {
        $goals = epc_hrt_goals($db, $reviewId);
        $wsum = 0.0;
        $rsum = 0.0;
        foreach ($goals as $g) {
            $w = (float) $g['weight'];
            $wsum += $w;
            $rsum += $w * (float) $g['rating'];
        }
        if ($wsum <= 0) {
            return 0.0;
        }
        return round($rsum / $wsum, 2);
    }
}

if (!function_exists('epc_hrt_review_finalize')) {
    /** Finalize a review: compute weighted overall rating and mark completed. */
    function epc_hrt_review_finalize(PDO $db, int $reviewId): float
    {
        epc_hrt_ensure_schema($db);
        $review = epc_hrt_review_get($db, $reviewId);
        if (!$review) {
            throw new Exception('Review not found');
        }
        if ($review['status'] === 'completed') {
            throw new Exception('Review is already completed');
        }
        if (count(epc_hrt_goals($db, $reviewId)) === 0) {
            throw new Exception('Add at least one goal before finalizing');
        }
        $overall = epc_hrt_review_weighted_rating($db, $reviewId);
        $db->prepare("UPDATE `epc_hrt_review` SET `status`='completed', `overall_rating`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($overall, time(), $reviewId));
        return $overall;
    }
}

if (!function_exists('epc_hrt_summary')) {
    /** @return array<string,mixed> */
    function epc_hrt_summary(PDO $db, int $companyId): array
    {
        epc_hrt_ensure_schema($db);
        $out = array('open_jobs' => 0, 'filled_jobs' => 0, 'applicants' => 0, 'hired' => 0, 'reviews_open' => 0, 'reviews_done' => 0);
        $st = $db->prepare("SELECT `status`, COUNT(*) c FROM `epc_hrt_job` WHERE `company_id`=? GROUP BY `status`");
        $st->execute(array($companyId));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            if ($r['status'] === 'open') {
                $out['open_jobs'] = (int) $r['c'];
            } elseif ($r['status'] === 'filled') {
                $out['filled_jobs'] = (int) $r['c'];
            }
        }
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_hrt_applicant` WHERE `company_id`=?");
        $st->execute(array($companyId));
        $out['applicants'] = (int) $st->fetchColumn();
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_hrt_applicant` WHERE `company_id`=? AND `stage`='hired'");
        $st->execute(array($companyId));
        $out['hired'] = (int) $st->fetchColumn();
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_hrt_review` WHERE `company_id`=? AND `status`<>'completed'");
        $st->execute(array($companyId));
        $out['reviews_open'] = (int) $st->fetchColumn();
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_hrt_review` WHERE `company_id`=? AND `status`='completed'");
        $st->execute(array($companyId));
        $out['reviews_done'] = (int) $st->fetchColumn();
        return $out;
    }
}
