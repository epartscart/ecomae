<?php
/**
 * Advanced ERP — Credit & Collections.
 *
 * Customer credit limits & terms, AR ageing (current / 30 / 60 / 90+ buckets),
 * credit-hold evaluation, dunning reminder levels and customer statements.
 *
 * Reads receivables from sales orders (invoiced/confirmed) net of customer
 * receipts in the cash/bank journal. Additive: one new table for credit
 * profiles; nothing existing is modified.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_credit_ensure_schema')) {
    function epc_credit_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_credit_profiles` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `customer_id` int(11) NOT NULL,
            `credit_limit` decimal(14,2) NOT NULL DEFAULT 0.00,
            `terms_days` int(11) NOT NULL DEFAULT 30,
            `on_hold` tinyint(1) NOT NULL DEFAULT 0,
            `risk_band` varchar(16) NOT NULL DEFAULT 'normal',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_customer` (`customer_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customer credit profiles'");

        // Customer master — extended fields (D365 depth: identity, dimensions, terms, tax, contact, address).
        $cols = array(
            'customer_account' => "varchar(32) DEFAULT NULL",
            'customer_name' => "varchar(255) DEFAULT NULL",
            'customer_group' => "varchar(64) DEFAULT NULL",
            'legal_entity_id' => "int(11) NOT NULL DEFAULT 0",
            'business_unit_id' => "int(11) NOT NULL DEFAULT 0",
            'currency_code' => "varchar(8) DEFAULT NULL",
            'payment_method' => "varchar(32) DEFAULT NULL",
            'delivery_terms' => "varchar(32) DEFAULT NULL",
            'delivery_mode' => "varchar(32) DEFAULT NULL",
            'trn' => "varchar(64) DEFAULT NULL",
            'tax_exempt' => "tinyint(1) NOT NULL DEFAULT 0",
            'sales_tax_group' => "varchar(64) DEFAULT NULL",
            'contact_person' => "varchar(255) DEFAULT NULL",
            'contact_email' => "varchar(128) DEFAULT NULL",
            'contact_phone' => "varchar(64) DEFAULT NULL",
            'website' => "varchar(255) DEFAULT NULL",
            'address' => "varchar(512) DEFAULT NULL",
            'city' => "varchar(128) DEFAULT NULL",
            'state_region' => "varchar(128) DEFAULT NULL",
            'postal_code' => "varchar(32) DEFAULT NULL",
            'country_code' => "varchar(8) DEFAULT NULL",
        );
        foreach ($cols as $col => $def) {
            try {
                $chk = $db->prepare("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_credit_profiles' AND COLUMN_NAME = ?");
                $chk->execute(array($col));
                if ((int) $chk->fetchColumn() === 0) {
                    $db->exec("ALTER TABLE `epc_credit_profiles` ADD COLUMN `$col` $def");
                }
            } catch (Exception $e) {
            }
        }
    }
}

if (!function_exists('epc_credit_set_master')) {
    /**
     * Persist the full D365-style customer master for a customer (user) id.
     * Preserves credit fields not supplied. Returns nothing.
     *
     * @param array<string,mixed> $data
     */
    function epc_credit_set_master(PDO $db, int $customerId, array $data): void
    {
        epc_credit_ensure_schema($db);
        $existing = epc_credit_get_profile($db, $customerId);
        $now = time();
        $get = function (string $k, $default = null) use ($data, $existing) {
            if (array_key_exists($k, $data) && $data[$k] !== '') {
                return $data[$k];
            }
            return $existing[$k] ?? $default;
        };
        $db->prepare(
            "INSERT INTO `epc_credit_profiles`
             (`customer_id`,`credit_limit`,`terms_days`,`on_hold`,`risk_band`,`notes`,
              `customer_account`,`customer_name`,`customer_group`,`legal_entity_id`,`business_unit_id`,`currency_code`,
              `payment_method`,`delivery_terms`,`delivery_mode`,`trn`,`tax_exempt`,`sales_tax_group`,
              `contact_person`,`contact_email`,`contact_phone`,`website`,`address`,`city`,`state_region`,`postal_code`,`country_code`,
              `time_created`,`time_updated`)
             VALUES (?,?,?,?,?,?, ?,?,?,?,?,?, ?,?,?,?,?,?, ?,?,?,?,?,?,?,?,?, ?,?)
             ON DUPLICATE KEY UPDATE
              `credit_limit`=VALUES(`credit_limit`), `terms_days`=VALUES(`terms_days`), `on_hold`=VALUES(`on_hold`),
              `risk_band`=VALUES(`risk_band`), `notes`=VALUES(`notes`),
              `customer_account`=VALUES(`customer_account`), `customer_name`=VALUES(`customer_name`), `customer_group`=VALUES(`customer_group`),
              `legal_entity_id`=VALUES(`legal_entity_id`), `business_unit_id`=VALUES(`business_unit_id`), `currency_code`=VALUES(`currency_code`),
              `payment_method`=VALUES(`payment_method`), `delivery_terms`=VALUES(`delivery_terms`), `delivery_mode`=VALUES(`delivery_mode`),
              `trn`=VALUES(`trn`), `tax_exempt`=VALUES(`tax_exempt`), `sales_tax_group`=VALUES(`sales_tax_group`),
              `contact_person`=VALUES(`contact_person`), `contact_email`=VALUES(`contact_email`), `contact_phone`=VALUES(`contact_phone`),
              `website`=VALUES(`website`), `address`=VALUES(`address`), `city`=VALUES(`city`), `state_region`=VALUES(`state_region`),
              `postal_code`=VALUES(`postal_code`), `country_code`=VALUES(`country_code`), `time_updated`=VALUES(`time_updated`)"
        )->execute(array(
            $customerId,
            round((float) $get('credit_limit', 0), 2),
            (int) $get('terms_days', 30),
            !empty($data['on_hold']) ? 1 : (int) ($existing['on_hold'] ?? 0),
            (string) $get('risk_band', 'normal'),
            (string) $get('notes', ''),
            ($get('customer_account') !== null) ? (string) $get('customer_account') : null,
            ($get('customer_name') !== null) ? (string) $get('customer_name') : null,
            ($get('customer_group') !== null) ? (string) $get('customer_group') : null,
            (int) $get('legal_entity_id', 0),
            (int) $get('business_unit_id', 0),
            ($get('currency_code') !== null) ? (string) $get('currency_code') : null,
            ($get('payment_method') !== null) ? (string) $get('payment_method') : null,
            ($get('delivery_terms') !== null) ? (string) $get('delivery_terms') : null,
            ($get('delivery_mode') !== null) ? (string) $get('delivery_mode') : null,
            ($get('trn') !== null) ? (string) $get('trn') : null,
            !empty($data['tax_exempt']) ? 1 : (int) ($existing['tax_exempt'] ?? 0),
            ($get('sales_tax_group') !== null) ? (string) $get('sales_tax_group') : null,
            ($get('contact_person') !== null) ? (string) $get('contact_person') : null,
            ($get('contact_email') !== null) ? (string) $get('contact_email') : null,
            ($get('contact_phone') !== null) ? (string) $get('contact_phone') : null,
            ($get('website') !== null) ? (string) $get('website') : null,
            ($get('address') !== null) ? (string) $get('address') : null,
            ($get('city') !== null) ? (string) $get('city') : null,
            ($get('state_region') !== null) ? (string) $get('state_region') : null,
            ($get('postal_code') !== null) ? (string) $get('postal_code') : null,
            ($get('country_code') !== null) ? (string) $get('country_code') : null,
            $now,
            $now,
        ));
    }
}

if (!function_exists('epc_credit_set_profile')) {
    /**
     * @param array<string,mixed> $data credit_limit, terms_days, on_hold, risk_band, notes
     */
    function epc_credit_set_profile(PDO $db, int $customerId, array $data): void
    {
        epc_credit_ensure_schema($db);
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_credit_profiles` (`customer_id`,`credit_limit`,`terms_days`,`on_hold`,`risk_band`,`notes`,`time_created`,`time_updated`)
             VALUES (?,?,?,?,?,?,?,?)
             ON DUPLICATE KEY UPDATE `credit_limit`=VALUES(`credit_limit`), `terms_days`=VALUES(`terms_days`),
                `on_hold`=VALUES(`on_hold`), `risk_band`=VALUES(`risk_band`), `notes`=VALUES(`notes`), `time_updated`=VALUES(`time_updated`)"
        )->execute(array(
            $customerId,
            round((float) ($data['credit_limit'] ?? 0), 2),
            (int) ($data['terms_days'] ?? 30),
            !empty($data['on_hold']) ? 1 : 0,
            (string) ($data['risk_band'] ?? 'normal'),
            (string) ($data['notes'] ?? ''),
            $now,
            $now,
        ));
    }
}

if (!function_exists('epc_credit_get_profile')) {
    /**
     * @return array<string,mixed>
     */
    function epc_credit_get_profile(PDO $db, int $customerId): array
    {
        epc_credit_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_credit_profiles` WHERE `customer_id`=?");
        $st->execute(array($customerId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return array('customer_id' => $customerId, 'credit_limit' => 0.0, 'terms_days' => 30, 'on_hold' => 0, 'risk_band' => 'normal');
        }
        return $row;
    }
}

if (!function_exists('epc_credit_customer_receipts')) {
    /**
     * Total receipts (direction=1 customer entries) per customer order, keyed by
     * order_id; plus a per-customer total.
     *
     * @return array<string,mixed>
     */
    function epc_credit_customer_receipts(PDO $db, int $customerId): array
    {
        $byOrder = array();
        $total = 0.0;
        try {
            $st = $db->prepare(
                "SELECT `order_id`, COALESCE(SUM(`amount`),0) amt FROM `epc_erp_cash_bank_entries`
                 WHERE `active`=1 AND `direction`=1 AND `counterparty_type`='customer' AND `counterparty_id`=?
                 GROUP BY `order_id`"
            );
            $st->execute(array($customerId));
            foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
                $byOrder[(int) $r['order_id']] = round((float) $r['amt'], 2);
                $total += (float) $r['amt'];
            }
        } catch (Throwable $e) {
            // no cash table -> zero receipts
        }
        return array('by_order' => $byOrder, 'total' => round($total, 2));
    }
}

if (!function_exists('epc_credit_ageing')) {
    /**
     * AR ageing for a customer: open invoices bucketed by age.
     * Buckets: current (<=terms), b30 (terms+1..30 past due), b60, b90, b90plus.
     *
     * @return array<string,mixed>
     */
    function epc_credit_ageing(PDO $db, int $customerId, int $asOf = 0): array
    {
        $asOf = $asOf > 0 ? $asOf : time();
        $profile = epc_credit_get_profile($db, $customerId);
        $terms = (int) $profile['terms_days'];
        $receipts = epc_credit_customer_receipts($db, $customerId);

        $buckets = array('current' => 0.0, 'd1_30' => 0.0, 'd31_60' => 0.0, 'd61_90' => 0.0, 'd90_plus' => 0.0);
        $invoices = array();
        $totalOutstanding = 0.0;

        try {
            $st = $db->prepare(
                "SELECT `id`,`total_amount`,`time_created` FROM `epc_erp_sales_orders`
                 WHERE `customer_user_id`=? AND `status` IN ('confirmed','invoiced')"
            );
            $st->execute(array($customerId));
            $rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
        } catch (Throwable $e) {
            $rows = array();
        }

        foreach ($rows as $r) {
            $oid = (int) $r['id'];
            $paid = $receipts['by_order'][$oid] ?? 0.0;
            $outstanding = round((float) $r['total_amount'] - $paid, 2);
            if ($outstanding <= 0.005) {
                continue;
            }
            $invoiceDate = (int) $r['time_created'];
            $dueDate = $invoiceDate + $terms * 86400;
            $daysPastDue = (int) floor(($asOf - $dueDate) / 86400);
            if ($daysPastDue <= 0) {
                $bucket = 'current';
            } elseif ($daysPastDue <= 30) {
                $bucket = 'd1_30';
            } elseif ($daysPastDue <= 60) {
                $bucket = 'd31_60';
            } elseif ($daysPastDue <= 90) {
                $bucket = 'd61_90';
            } else {
                $bucket = 'd90_plus';
            }
            $buckets[$bucket] += $outstanding;
            $totalOutstanding += $outstanding;
            $invoices[] = array(
                'order_id' => $oid,
                'invoice_total' => round((float) $r['total_amount'], 2),
                'paid' => $paid,
                'outstanding' => $outstanding,
                'days_past_due' => max(0, $daysPastDue),
                'bucket' => $bucket,
            );
        }
        foreach ($buckets as $k => $v) {
            $buckets[$k] = round($v, 2);
        }

        return array(
            'customer_id' => $customerId,
            'as_of' => $asOf,
            'terms_days' => $terms,
            'buckets' => $buckets,
            'total_outstanding' => round($totalOutstanding, 2),
            'invoices' => $invoices,
        );
    }
}

if (!function_exists('epc_credit_evaluate')) {
    /**
     * Credit-hold evaluation: combine limit, current exposure and overdue.
     *
     * @return array<string,mixed>
     */
    function epc_credit_evaluate(PDO $db, int $customerId, float $newOrderAmount = 0.0, int $asOf = 0): array
    {
        $profile = epc_credit_get_profile($db, $customerId);
        $ageing = epc_credit_ageing($db, $customerId, $asOf);
        $limit = (float) $profile['credit_limit'];
        $exposure = $ageing['total_outstanding'] + $newOrderAmount;
        $overdue = $ageing['buckets']['d1_30'] + $ageing['buckets']['d31_60'] + $ageing['buckets']['d61_90'] + $ageing['buckets']['d90_plus'];

        $reasons = array();
        if (!empty($profile['on_hold'])) {
            $reasons[] = 'Account is manually on hold';
        }
        if ($limit > 0 && $exposure > $limit) {
            $reasons[] = 'Exposure ' . number_format($exposure, 2) . ' exceeds limit ' . number_format($limit, 2);
        }
        if ($ageing['buckets']['d90_plus'] > 0) {
            $reasons[] = 'Invoices more than 90 days overdue';
        }

        return array(
            'customer_id' => $customerId,
            'credit_limit' => $limit,
            'current_exposure' => round($ageing['total_outstanding'], 2),
            'projected_exposure' => round($exposure, 2),
            'overdue_total' => round($overdue, 2),
            'available_credit' => $limit > 0 ? round($limit - $ageing['total_outstanding'], 2) : null,
            'approved' => count($reasons) === 0,
            'reasons' => $reasons,
        );
    }
}

if (!function_exists('epc_credit_dunning_level')) {
    /**
     * Map the worst overdue age to a dunning level (0..3) + suggested message.
     *
     * @param array<string,mixed> $ageing result of epc_credit_ageing()
     * @return array<string,mixed>
     */
    function epc_credit_dunning_level(array $ageing): array
    {
        $b = $ageing['buckets'];
        if ($b['d90_plus'] > 0) {
            $level = 3;
            $tone = 'Final notice — account may be referred to collections.';
        } elseif ($b['d61_90'] > 0) {
            $level = 2;
            $tone = 'Second reminder — please settle the overdue balance now.';
        } elseif ($b['d1_30'] > 0 || $b['d31_60'] > 0) {
            $level = 1;
            $tone = 'Friendly reminder — your invoice is past due.';
        } else {
            $level = 0;
            $tone = 'Account current — no reminder needed.';
        }
        return array('level' => $level, 'message' => $tone, 'overdue_total' => round(
            $b['d1_30'] + $b['d31_60'] + $b['d61_90'] + $b['d90_plus'],
            2
        ));
    }
}

if (!function_exists('epc_credit_statement')) {
    /**
     * Customer statement: profile + ageing + dunning, ready to render/send.
     *
     * @return array<string,mixed>
     */
    function epc_credit_statement(PDO $db, int $customerId, int $asOf = 0): array
    {
        $ageing = epc_credit_ageing($db, $customerId, $asOf);
        return array(
            'customer_id' => $customerId,
            'profile' => epc_credit_get_profile($db, $customerId),
            'ageing' => $ageing,
            'dunning' => epc_credit_dunning_level($ageing),
            'evaluation' => epc_credit_evaluate($db, $customerId, 0.0, $asOf),
        );
    }
}
