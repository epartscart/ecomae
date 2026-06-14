<?php
/**
 * Collections & credit management — enterprise collections workspace on top
 * of the existing credit/ageing engine.
 *
 * Adds: collection cases (status + promise-to-pay), a collections activity log,
 * idempotent dunning runs, and a credit-hold action log that also flips the
 * customer credit profile's on_hold flag.
 *
 * Additive: new epc_coll_* tables; reuses epc_erp_credit.php for dunning levels
 * and credit profiles. Multi-company aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_credit.php';

if (!function_exists('epc_coll_ensure_schema')) {
    function epc_coll_ensure_schema(PDO $db): void
    {
        epc_credit_ensure_schema($db);

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_coll_cases` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `status` varchar(20) NOT NULL DEFAULT 'new',
            `balance` decimal(18,2) NOT NULL DEFAULT 0.00,
            `promise_amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `promise_date` int(11) NOT NULL DEFAULT 0,
            `assigned_to` varchar(120) NOT NULL DEFAULT '',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_customer` (`customer_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Collection cases'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_coll_activity` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `case_id` int(11) NOT NULL DEFAULT 0,
            `type` varchar(20) NOT NULL DEFAULT 'note',
            `outcome` varchar(200) NOT NULL DEFAULT '',
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `follow_up_date` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_case` (`case_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Collection activities'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_coll_dunning` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `run_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `level` int(11) NOT NULL DEFAULT 0,
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `message` varchar(255) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_run` (`run_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Dunning run log'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_coll_hold` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `action` varchar(10) NOT NULL DEFAULT 'place',
            `reason` varchar(255) NOT NULL DEFAULT '',
            `actor` varchar(120) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_customer` (`customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Credit hold action log'");
    }
}

/* ---------------- cases ---------------- */

if (!function_exists('epc_coll_case_statuses')) {
    /** @return array<int,string> */
    function epc_coll_case_statuses(): array
    {
        return array('new', 'in_progress', 'promise_to_pay', 'escalated', 'resolved');
    }
}

if (!function_exists('epc_coll_case_save')) {
    /** @param array<string,mixed> $data customer_id, status, balance, promise_amount, promise_date, assigned_to, notes */
    function epc_coll_case_save(PDO $db, array $data, int $id = 0): int
    {
        epc_coll_ensure_schema($db);
        $status = (string) ($data['status'] ?? 'new');
        if (!in_array($status, epc_coll_case_statuses(), true)) {
            $status = 'new';
        }
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_coll_cases` SET `customer_id`=?, `status`=?, `balance`=?, `promise_amount`=?, `promise_date`=?, `assigned_to`=?, `notes`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array((int) ($data['customer_id'] ?? 0), $status, (float) ($data['balance'] ?? 0), (float) ($data['promise_amount'] ?? 0), (int) ($data['promise_date'] ?? 0), (string) ($data['assigned_to'] ?? ''), (string) ($data['notes'] ?? ''), $now, $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_coll_cases` (`company_id`,`customer_id`,`status`,`balance`,`promise_amount`,`promise_date`,`assigned_to`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['customer_id'] ?? 0), $status, (float) ($data['balance'] ?? 0), (float) ($data['promise_amount'] ?? 0), (int) ($data['promise_date'] ?? 0), (string) ($data['assigned_to'] ?? ''), (string) ($data['notes'] ?? ''), $now, $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_coll_case_set_status')) {
    function epc_coll_case_set_status(PDO $db, int $id, string $status): void
    {
        epc_coll_ensure_schema($db);
        if (!in_array($status, epc_coll_case_statuses(), true)) {
            throw new Exception('Invalid case status');
        }
        $db->prepare("UPDATE `epc_coll_cases` SET `status`=?, `time_updated`=? WHERE `id`=?")->execute(array($status, time(), $id));
    }
}

if (!function_exists('epc_coll_case_promise')) {
    /** Record a promise-to-pay: sets amount/date and moves the case to promise_to_pay. */
    function epc_coll_case_promise(PDO $db, int $id, float $amount, int $promiseDate): void
    {
        epc_coll_ensure_schema($db);
        $db->prepare("UPDATE `epc_coll_cases` SET `promise_amount`=?, `promise_date`=?, `status`='promise_to_pay', `time_updated`=? WHERE `id`=?")
           ->execute(array($amount, $promiseDate, time(), $id));
        epc_coll_activity_log($db, $id, 'promise', 'Promise to pay ' . number_format($amount, 2), $amount, $promiseDate);
    }
}

if (!function_exists('epc_coll_case_get')) {
    /** @return array<string,mixed>|null */
    function epc_coll_case_get(PDO $db, int $id): ?array
    {
        epc_coll_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_coll_cases` WHERE id=?");
        $st->execute(array($id));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ?: null;
    }
}

if (!function_exists('epc_coll_cases')) {
    /** @return array<int,array<string,mixed>> */
    function epc_coll_cases(PDO $db, int $companyId = 0, string $status = ''): array
    {
        epc_coll_ensure_schema($db);
        $sql = "SELECT * FROM `epc_coll_cases` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        if ($status !== '') {
            $sql .= " AND status=?";
            $args[] = $status;
        }
        $sql .= " ORDER BY balance DESC, id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- activities ---------------- */

if (!function_exists('epc_coll_activity_log')) {
    function epc_coll_activity_log(PDO $db, int $caseId, string $type, string $outcome, float $amount = 0.0, int $followUp = 0): int
    {
        epc_coll_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_coll_activity` (`case_id`,`type`,`outcome`,`amount`,`follow_up_date`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array($caseId, $type, $outcome, $amount, $followUp, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_coll_activities')) {
    /** @return array<int,array<string,mixed>> */
    function epc_coll_activities(PDO $db, int $caseId): array
    {
        epc_coll_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_coll_activity` WHERE case_id=? ORDER BY id DESC");
        $st->execute(array($caseId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- dunning runs ---------------- */

if (!function_exists('epc_coll_dunning_plan')) {
    /**
     * Pure: from a list of customers with ageing buckets, produce dunning
     * entries for those at level >= 1 (skips current accounts). Reuses
     * epc_credit_dunning_level for the level/message mapping.
     *
     * @param array<int,array{customer_id:int,buckets:array<string,float>}> $customers
     * @return array<int,array{customer_id:int,level:int,amount:float,message:string}>
     */
    function epc_coll_dunning_plan(array $customers): array
    {
        $out = array();
        foreach ($customers as $c) {
            $dl = epc_credit_dunning_level(array('buckets' => $c['buckets']));
            if ((int) $dl['level'] < 1) {
                continue;
            }
            $out[] = array(
                'customer_id' => (int) $c['customer_id'],
                'level' => (int) $dl['level'],
                'amount' => (float) $dl['overdue_total'],
                'message' => (string) $dl['message'],
            );
        }
        return $out;
    }
}

if (!function_exists('epc_coll_dunning_run')) {
    /**
     * Execute a dunning run: plan from customer ageings then persist one log row
     * per dunned customer under a new run id. Returns the run id + entries.
     *
     * @param array<int,array{customer_id:int,buckets:array<string,float>}> $customers
     * @return array{run_id:int,entries:array<int,array<string,mixed>>}
     */
    function epc_coll_dunning_run(PDO $db, int $companyId, array $customers): array
    {
        epc_coll_ensure_schema($db);
        $plan = epc_coll_dunning_plan($customers);
        $runId = (int) $db->query("SELECT COALESCE(MAX(run_id),0)+1 FROM `epc_coll_dunning`" . ($companyId > 0 ? " WHERE company_id=" . $companyId : ""))->fetchColumn();
        $ins = $db->prepare("INSERT INTO `epc_coll_dunning` (`company_id`,`run_id`,`customer_id`,`level`,`amount`,`message`,`time_created`) VALUES (?,?,?,?,?,?,?)");
        $now = time();
        foreach ($plan as &$p) {
            $ins->execute(array($companyId, $runId, $p['customer_id'], $p['level'], $p['amount'], $p['message'], $now));
            $p['id'] = (int) $db->lastInsertId();
            $p['run_id'] = $runId;
        }
        return array('run_id' => $runId, 'entries' => $plan);
    }
}

if (!function_exists('epc_coll_dunning_log')) {
    /** @return array<int,array<string,mixed>> */
    function epc_coll_dunning_log(PDO $db, int $companyId = 0): array
    {
        epc_coll_ensure_schema($db);
        $sql = "SELECT * FROM `epc_coll_dunning` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY run_id DESC, level DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- credit holds ---------------- */

if (!function_exists('epc_coll_hold_set')) {
    /**
     * Place or release a credit hold: logs the action AND flips the customer
     * credit profile on_hold flag (reuses epc_credit_set_profile).
     */
    function epc_coll_hold_set(PDO $db, int $companyId, int $customerId, bool $place, string $reason = '', string $actor = ''): int
    {
        epc_coll_ensure_schema($db);
        $profile = epc_credit_get_profile($db, $customerId);
        epc_credit_set_profile($db, $customerId, array(
            'credit_limit' => (float) ($profile['credit_limit'] ?? 0),
            'terms_days' => (int) ($profile['terms_days'] ?? 30),
            'on_hold' => $place ? 1 : 0,
            'risk_band' => (string) ($profile['risk_band'] ?? 'normal'),
            'notes' => (string) ($profile['notes'] ?? ''),
        ));
        $db->prepare("INSERT INTO `epc_coll_hold` (`company_id`,`customer_id`,`action`,`reason`,`actor`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array($companyId, $customerId, $place ? 'place' : 'release', $reason, $actor, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_coll_holds')) {
    /** @return array<int,array<string,mixed>> */
    function epc_coll_holds(PDO $db, int $companyId = 0): array
    {
        epc_coll_ensure_schema($db);
        $sql = "SELECT * FROM `epc_coll_hold` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY id DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- workspace summary ---------------- */

if (!function_exists('epc_coll_summary')) {
    /** @return array{open_cases:int,promise_cases:int,escalated_cases:int,total_balance:float,on_hold:int,dunning_runs:int} */
    function epc_coll_summary(PDO $db, int $companyId = 0): array
    {
        epc_coll_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        return array(
            'open_cases' => (int) $db->query("SELECT COUNT(*) FROM `epc_coll_cases` WHERE status NOT IN ('resolved')" . $wa)->fetchColumn(),
            'promise_cases' => (int) $db->query("SELECT COUNT(*) FROM `epc_coll_cases` WHERE status='promise_to_pay'" . $wa)->fetchColumn(),
            'escalated_cases' => (int) $db->query("SELECT COUNT(*) FROM `epc_coll_cases` WHERE status='escalated'" . $wa)->fetchColumn(),
            'total_balance' => (float) $db->query("SELECT COALESCE(SUM(balance),0) FROM `epc_coll_cases` WHERE status NOT IN ('resolved')" . $wa)->fetchColumn(),
            'on_hold' => (int) $db->query("SELECT COUNT(*) FROM `epc_credit_profiles` WHERE on_hold=1")->fetchColumn(),
            'dunning_runs' => (int) $db->query("SELECT COUNT(DISTINCT run_id) FROM `epc_coll_dunning` WHERE 1=1" . $wa)->fetchColumn(),
        );
    }
}
