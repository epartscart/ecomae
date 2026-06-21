<?php
defined('_ASTEXE_') or die('No access');
/**
 * Subscription billing & revenue recognition (IFRS 15 / ASC 606 straight-line).
 *
 * - Recurring subscriptions with monthly/quarterly/annual cycles.
 * - Cycle invoices generated on demand; next-bill date auto-advances.
 * - Revenue recognised straight-line over the service term; the unrecognised
 *   portion of billed amounts is reported as deferred revenue (a liability).
 */

if (!function_exists('epc_sub_ensure_schema')) {
    function epc_sub_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_subscriptions` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `code` varchar(40) NOT NULL DEFAULT '',
            `customer` varchar(200) NOT NULL DEFAULT '',
            `plan_name` varchar(160) NOT NULL DEFAULT '',
            `amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `cycle` varchar(12) NOT NULL DEFAULT 'monthly',
            `term_months` int(11) NOT NULL DEFAULT 12,
            `start_date` int(11) NOT NULL DEFAULT 0,
            `next_bill_date` int(11) NOT NULL DEFAULT 0,
            `status` varchar(12) NOT NULL DEFAULT 'active',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_code` (`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Subscriptions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_sub_invoices` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `subscription_id` int(11) NOT NULL,
            `period_start` int(11) NOT NULL DEFAULT 0,
            `period_end` int(11) NOT NULL DEFAULT 0,
            `amount` decimal(16,2) NOT NULL DEFAULT 0.00,
            `status` varchar(12) NOT NULL DEFAULT 'issued',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_sub` (`subscription_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Subscription invoices'");
    }
}

if (!function_exists('epc_sub_cycle_months')) {
    function epc_sub_cycle_months(string $cycle): int
    {
        switch ($cycle) {
            case 'annual': return 12;
            case 'quarterly': return 3;
            default: return 1;
        }
    }
}

if (!function_exists('epc_sub_mrr')) {
    /** Normalise a subscription's cycle amount to monthly recurring revenue. */
    function epc_sub_mrr(float $amount, string $cycle): float
    {
        $m = epc_sub_cycle_months($cycle);
        return $m > 0 ? round($amount / $m, 2) : $amount;
    }
}

if (!function_exists('epc_sub_save')) {
    /**
     * @param array<string,mixed> $data
     */
    function epc_sub_save(PDO $db, array $data, int $id = 0): int
    {
        epc_sub_ensure_schema($db);
        $start = !empty($data['start_date']) ? (int) $data['start_date'] : time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_erp_subscriptions` SET `customer`=?, `plan_name`=?, `amount`=?, `currency`=?, `cycle`=?, `term_months`=?, `start_date`=? WHERE `id`=?")
               ->execute(array(
                   (string) ($data['customer'] ?? ''), (string) ($data['plan_name'] ?? ''),
                   (float) ($data['amount'] ?? 0), (string) ($data['currency'] ?? 'AED'),
                   (string) ($data['cycle'] ?? 'monthly'), (int) ($data['term_months'] ?? 12), $start, $id,
               ));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_erp_subscriptions` (`code`,`customer`,`plan_name`,`amount`,`currency`,`cycle`,`term_months`,`start_date`,`next_bill_date`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?, 'active', ?)")
           ->execute(array(
               (string) ($data['code'] ?? ''), (string) ($data['customer'] ?? ''), (string) ($data['plan_name'] ?? ''),
               (float) ($data['amount'] ?? 0), (string) ($data['currency'] ?? 'AED'),
               (string) ($data['cycle'] ?? 'monthly'), (int) ($data['term_months'] ?? 12), $start, $start, time(),
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_sub_set_status')) {
    function epc_sub_set_status(PDO $db, int $id, string $status): void
    {
        epc_sub_ensure_schema($db);
        $allowed = array('active', 'paused', 'cancelled');
        if (!in_array($status, $allowed, true)) {
            throw new Exception('Invalid subscription status');
        }
        $db->prepare("UPDATE `epc_erp_subscriptions` SET `status`=? WHERE `id`=?")->execute(array($status, $id));
    }
}

if (!function_exists('epc_sub_get')) {
    /** @return array<string,mixed>|null */
    function epc_sub_get(PDO $db, int $id): ?array
    {
        epc_sub_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_subscriptions` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_sub_generate_invoice')) {
    /**
     * Generate the next cycle invoice and advance next_bill_date by one cycle.
     *
     * @return array{id:int,amount:float,period_start:int,period_end:int}
     */
    function epc_sub_generate_invoice(PDO $db, int $subscriptionId): array
    {
        epc_sub_ensure_schema($db);
        $sub = epc_sub_get($db, $subscriptionId);
        if (!$sub) {
            throw new Exception('Subscription not found');
        }
        if ($sub['status'] !== 'active') {
            throw new Exception('Subscription is not active');
        }
        $months = epc_sub_cycle_months((string) $sub['cycle']);
        $periodStart = (int) $sub['next_bill_date'] > 0 ? (int) $sub['next_bill_date'] : (int) $sub['start_date'];
        $periodEnd = strtotime('+' . $months . ' months', $periodStart);
        $amount = (float) $sub['amount'];
        $db->prepare("INSERT INTO `epc_erp_sub_invoices` (`subscription_id`,`period_start`,`period_end`,`amount`,`status`,`time_created`) VALUES (?,?,?,?, 'issued', ?)")
           ->execute(array($subscriptionId, $periodStart, $periodEnd, $amount, time()));
        $invId = (int) $db->lastInsertId();
        $db->prepare("UPDATE `epc_erp_subscriptions` SET `next_bill_date`=? WHERE `id`=?")->execute(array($periodEnd, $subscriptionId));
        return array('id' => $invId, 'amount' => $amount, 'period_start' => $periodStart, 'period_end' => $periodEnd);
    }
}

if (!function_exists('epc_sub_invoice_set_status')) {
    function epc_sub_invoice_set_status(PDO $db, int $invoiceId, string $status): void
    {
        epc_sub_ensure_schema($db);
        $db->prepare("UPDATE `epc_erp_sub_invoices` SET `status`=? WHERE `id`=?")->execute(array($status, $invoiceId));
    }
}

if (!function_exists('epc_sub_invoices_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_sub_invoices_list(PDO $db, int $subscriptionId, int $limit = 100): array
    {
        epc_sub_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_sub_invoices` WHERE `subscription_id`=? ORDER BY `id` DESC LIMIT " . max(1, $limit));
        $st->execute(array($subscriptionId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_sub_billed_total')) {
    function epc_sub_billed_total(PDO $db, int $subscriptionId): float
    {
        epc_sub_ensure_schema($db);
        $st = $db->prepare("SELECT COALESCE(SUM(`amount`),0) FROM `epc_erp_sub_invoices` WHERE `subscription_id`=?");
        $st->execute(array($subscriptionId));
        return (float) $st->fetchColumn();
    }
}

if (!function_exists('epc_sub_revenue_recognized')) {
    /**
     * Straight-line revenue recognised to date over the contract term.
     * Monthly rate = MRR; recognised = min(term, elapsed months since start) × MRR.
     *
     * @return array{mrr:float,term_months:int,elapsed:int,recognized:float,contract_value:float,deferred:float}
     */
    function epc_sub_revenue_recognized(PDO $db, array $sub, ?int $asOf = null): array
    {
        $asOf = $asOf ?? time();
        $mrr = epc_sub_mrr((float) $sub['amount'], (string) $sub['cycle']);
        $term = max(1, (int) $sub['term_months']);
        $start = (int) $sub['start_date'] > 0 ? (int) $sub['start_date'] : (int) $sub['time_created'];
        $elapsed = 0;
        if ($asOf > $start) {
            // whole months elapsed since start
            $elapsed = (int) floor(($asOf - $start) / (30.4375 * 86400));
        }
        $elapsed = min($elapsed, $term);
        $contractValue = round($mrr * $term, 2);
        $recognized = round($mrr * $elapsed, 2);
        $billed = epc_sub_billed_total($db, (int) $sub['id']);
        $deferred = round(max(0.0, $billed - $recognized), 2);
        return array(
            'mrr' => $mrr,
            'term_months' => $term,
            'elapsed' => $elapsed,
            'recognized' => $recognized,
            'contract_value' => $contractValue,
            'deferred' => $deferred,
        );
    }
}

if (!function_exists('epc_sub_list')) {
    /** @return array<int,array<string,mixed>> */
    function epc_sub_list(PDO $db, int $limit = 200): array
    {
        epc_sub_ensure_schema($db);
        $rows = $db->query("SELECT * FROM `epc_erp_subscriptions` ORDER BY `id` DESC LIMIT " . max(1, $limit))->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['mrr'] = epc_sub_mrr((float) $r['amount'], (string) $r['cycle']);
            $r['rev'] = epc_sub_revenue_recognized($db, $r);
            $r['billed'] = epc_sub_billed_total($db, (int) $r['id']);
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_sub_summary')) {
    /**
     * @return array{active:int,mrr:float,arr:float,deferred:float,recognized:float}
     */
    function epc_sub_summary(PDO $db): array
    {
        $subs = epc_sub_list($db, 500);
        $mrr = 0.0;
        $deferred = 0.0;
        $recognized = 0.0;
        $active = 0;
        foreach ($subs as $s) {
            if ($s['status'] === 'active') {
                $active++;
                $mrr = round($mrr + (float) $s['mrr'], 2);
            }
            $deferred = round($deferred + (float) $s['rev']['deferred'], 2);
            $recognized = round($recognized + (float) $s['rev']['recognized'], 2);
        }
        return array(
            'active' => $active,
            'mrr' => $mrr,
            'arr' => round($mrr * 12, 2),
            'deferred' => $deferred,
            'recognized' => $recognized,
        );
    }
}
