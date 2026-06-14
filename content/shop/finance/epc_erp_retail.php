<?php
/**
 * Retail / Commerce — D365 F&O Commerce-style channels, assortments, retail
 * pricing & periodic discounts, POS transactions and end-of-day statements.
 *
 * - Channels: retail stores / online channels (currency, type).
 * - Assortments: which items are sellable per channel.
 * - Periodic discounts: percent or amount, date-effective, per channel or all
 *   channels; pure best-discount selection.
 * - POS transactions: receipt with lines; gross/discount/net/tax/total + tender.
 * - Statement (Z-report): aggregate sales for a channel over a period.
 *
 * Pure pricing/aggregation helpers are separated from persistence for tests.
 * Tax rate is passed in by the caller (resolved from the tenant's registration
 * country per the worldwide-compliance rule — never hard-coded here).
 * Multi-company aware. Additive only.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_rtl_ensure_schema')) {
    function epc_rtl_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rtl_channel` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `channel_type` varchar(12) NOT NULL DEFAULT 'store',
            `currency` varchar(8) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Retail channels'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rtl_assortment` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `channel_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_channel_item` (`channel_id`,`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Channel assortments'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rtl_discount` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `channel_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `disc_type` varchar(8) NOT NULL DEFAULT 'percent',
            `value` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `starts` int(11) NOT NULL DEFAULT 0,
            `ends` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_channel` (`channel_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Retail periodic discounts'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rtl_txn` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `channel_id` int(11) NOT NULL DEFAULT 0,
            `receipt_no` varchar(40) NOT NULL DEFAULT '',
            `gross` decimal(18,2) NOT NULL DEFAULT 0.00,
            `discount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `net` decimal(18,2) NOT NULL DEFAULT 0.00,
            `tax` decimal(18,2) NOT NULL DEFAULT 0.00,
            `total` decimal(18,2) NOT NULL DEFAULT 0.00,
            `tender_type` varchar(12) NOT NULL DEFAULT 'cash',
            `txn_time` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_channel` (`channel_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS transactions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_rtl_txn_line` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `txn_id` int(11) NOT NULL DEFAULT 0,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `unit_price` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `line_discount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `line_net` decimal(18,2) NOT NULL DEFAULT 0.00,
            PRIMARY KEY (`id`),
            KEY `x_txn` (`txn_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS transaction lines'");
    }
}

/* ---------------- pure pricing ---------------- */

if (!function_exists('epc_rtl_best_discount')) {
    /**
     * Pure: from active discounts, choose the one giving the lowest unit price.
     *
     * @param float $unitPrice base unit price
     * @param array<int,array{disc_type:string,value:float}> $discounts
     * @return array{net_unit:float,discount_unit:float,applied:array<string,mixed>|null}
     */
    function epc_rtl_best_discount(float $unitPrice, array $discounts): array
    {
        $bestNet = $unitPrice;
        $applied = null;
        foreach ($discounts as $d) {
            $type = (string) ($d['disc_type'] ?? 'percent');
            $val = (float) ($d['value'] ?? 0);
            if ($type === 'percent') {
                $net = $unitPrice * (1 - max(0.0, min(100.0, $val)) / 100.0);
            } else {
                $net = $unitPrice - $val;
            }
            if ($net < 0) {
                $net = 0.0;
            }
            if ($net < $bestNet - 1e-9) {
                $bestNet = $net;
                $applied = $d;
            }
        }
        return array(
            'net_unit' => round($bestNet, 4),
            'discount_unit' => round($unitPrice - $bestNet, 4),
            'applied' => $applied,
        );
    }
}

if (!function_exists('epc_rtl_price_line')) {
    /**
     * Pure: price a POS line. tax applied on net (caller supplies country rate).
     *
     * @param array<int,array{disc_type:string,value:float}> $discounts
     * @return array{gross:float,discount:float,net:float,tax:float,total:float,net_unit:float}
     */
    function epc_rtl_price_line(float $unitPrice, float $qty, array $discounts, float $taxRate = 0.0): array
    {
        $best = epc_rtl_best_discount($unitPrice, $discounts);
        $gross = round($unitPrice * $qty, 2);
        $net = round($best['net_unit'] * $qty, 2);
        $discount = round($gross - $net, 2);
        $tax = round($net * ($taxRate / 100.0), 2);
        return array(
            'gross' => $gross,
            'discount' => $discount,
            'net' => $net,
            'tax' => $tax,
            'total' => round($net + $tax, 2),
            'net_unit' => $best['net_unit'],
        );
    }
}

if (!function_exists('epc_rtl_statement_totals')) {
    /**
     * Pure: aggregate POS transactions into a statement (Z-report).
     *
     * @param array<int,array<string,mixed>> $txns
     * @return array{count:int,gross:float,discount:float,net:float,tax:float,total:float,by_tender:array<string,float>}
     */
    function epc_rtl_statement_totals(array $txns): array
    {
        $sum = array('count' => 0, 'gross' => 0.0, 'discount' => 0.0, 'net' => 0.0, 'tax' => 0.0, 'total' => 0.0, 'by_tender' => array());
        foreach ($txns as $t) {
            $sum['count']++;
            $sum['gross'] += (float) ($t['gross'] ?? 0);
            $sum['discount'] += (float) ($t['discount'] ?? 0);
            $sum['net'] += (float) ($t['net'] ?? 0);
            $sum['tax'] += (float) ($t['tax'] ?? 0);
            $sum['total'] += (float) ($t['total'] ?? 0);
            $tender = (string) ($t['tender_type'] ?? 'cash');
            $sum['by_tender'][$tender] = round(($sum['by_tender'][$tender] ?? 0) + (float) ($t['total'] ?? 0), 2);
        }
        foreach (array('gross', 'discount', 'net', 'tax', 'total') as $k) {
            $sum[$k] = round($sum[$k], 2);
        }
        return $sum;
    }
}

/* ---------------- channels ---------------- */

if (!function_exists('epc_rtl_channel_save')) {
    /** @param array<string,mixed> $data */
    function epc_rtl_channel_save(PDO $db, int $companyId, array $data, int $id = 0): int
    {
        epc_rtl_ensure_schema($db);
        $type = (string) ($data['channel_type'] ?? 'store');
        if (!in_array($type, array('store', 'online', 'callcenter'), true)) {
            throw new Exception('Invalid channel type');
        }
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Channel code is required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_rtl_channel` SET `name`=?, `channel_type`=?, `currency`=?, `active`=?, `time_updated`=? WHERE id=? AND company_id=?")
               ->execute(array((string) ($data['name'] ?? ''), $type, (string) ($data['currency'] ?? ''), !empty($data['active']) ? 1 : 0, time(), $id, $companyId));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_rtl_channel` (`company_id`,`code`,`name`,`channel_type`,`currency`,`active`,`time_updated`) VALUES (?,?,?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `channel_type`=VALUES(`channel_type`), `currency`=VALUES(`currency`), `active`=VALUES(`active`), `time_updated`=VALUES(`time_updated`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? ''), $type, (string) ($data['currency'] ?? ''), !empty($data['active']) ? 1 : 0, time()));
        $st = $db->prepare("SELECT id FROM `epc_rtl_channel` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_rtl_channels')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rtl_channels(PDO $db, int $companyId = 0): array
    {
        epc_rtl_ensure_schema($db);
        $sql = "SELECT * FROM `epc_rtl_channel` WHERE 1=1";
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
            $r['assort_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_rtl_assortment` WHERE channel_id=" . (int) $r['id'] . " AND active=1")->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

/* ---------------- assortments ---------------- */

if (!function_exists('epc_rtl_assortment_set')) {
    function epc_rtl_assortment_set(PDO $db, int $companyId, int $channelId, int $itemId, bool $active): void
    {
        epc_rtl_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_rtl_assortment` (`company_id`,`channel_id`,`item_id`,`active`) VALUES (?,?,?,?)
                      ON DUPLICATE KEY UPDATE `active`=VALUES(`active`)")
           ->execute(array($companyId, $channelId, $itemId, $active ? 1 : 0));
    }
}

if (!function_exists('epc_rtl_assortments')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rtl_assortments(PDO $db, int $channelId): array
    {
        epc_rtl_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_rtl_assortment` WHERE channel_id=? ORDER BY item_id");
        $st->execute(array($channelId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_rtl_in_assortment')) {
    function epc_rtl_in_assortment(PDO $db, int $channelId, int $itemId): bool
    {
        epc_rtl_ensure_schema($db);
        $st = $db->prepare("SELECT active FROM `epc_rtl_assortment` WHERE channel_id=? AND item_id=?");
        $st->execute(array($channelId, $itemId));
        $r = $st->fetch(PDO::FETCH_ASSOC);
        return $r ? (int) $r['active'] === 1 : false;
    }
}

/* ---------------- discounts ---------------- */

if (!function_exists('epc_rtl_discount_save')) {
    /** @param array<string,mixed> $data */
    function epc_rtl_discount_save(PDO $db, int $companyId, array $data): int
    {
        epc_rtl_ensure_schema($db);
        $type = (string) ($data['disc_type'] ?? 'percent');
        if (!in_array($type, array('percent', 'amount'), true)) {
            throw new Exception('Invalid discount type');
        }
        $db->prepare("INSERT INTO `epc_rtl_discount` (`company_id`,`channel_id`,`code`,`name`,`disc_type`,`value`,`starts`,`ends`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)")
           ->execute(array($companyId, (int) ($data['channel_id'] ?? 0), (string) ($data['code'] ?? ''), (string) ($data['name'] ?? ''), $type, (float) ($data['value'] ?? 0), (int) ($data['starts'] ?? 0), (int) ($data['ends'] ?? 0), !empty($data['active']) ? 1 : 0, time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_rtl_active_discounts')) {
    /**
     * Active discounts for a channel at a point in time (channel-specific +
     * all-channel where channel_id=0). starts/ends of 0 mean unbounded.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_rtl_active_discounts(PDO $db, int $companyId, int $channelId, int $atTime = 0): array
    {
        epc_rtl_ensure_schema($db);
        $atTime = $atTime > 0 ? $atTime : time();
        $st = $db->prepare("SELECT * FROM `epc_rtl_discount` WHERE company_id=? AND active=1 AND (channel_id=? OR channel_id=0)
                            AND (starts=0 OR starts<=?) AND (ends=0 OR ends>=?) ORDER BY id");
        $st->execute(array($companyId, $channelId, $atTime, $atTime));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_rtl_discounts')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rtl_discounts(PDO $db, int $companyId = 0): array
    {
        epc_rtl_ensure_schema($db);
        $sql = "SELECT * FROM `epc_rtl_discount` WHERE 1=1";
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

/* ---------------- POS transactions ---------------- */

if (!function_exists('epc_rtl_pos_sale')) {
    /**
     * Record a POS sale. Each line is priced with active channel discounts +
     * supplied tax rate. Returns the persisted transaction totals.
     *
     * @param array<int,array{item_id:int,qty:float,unit_price:float}> $lines
     * @return array{id:int,gross:float,discount:float,net:float,tax:float,total:float}
     */
    function epc_rtl_pos_sale(PDO $db, int $companyId, int $channelId, array $lines, string $tender = 'cash', float $taxRate = 0.0, string $receiptNo = '', int $atTime = 0): array
    {
        epc_rtl_ensure_schema($db);
        if (!in_array($tender, array('cash', 'card', 'online', 'voucher'), true)) {
            throw new Exception('Invalid tender type');
        }
        $atTime = $atTime > 0 ? $atTime : time();
        $discounts = epc_rtl_active_discounts($db, $companyId, $channelId, $atTime);
        $gross = $disc = $net = $tax = $total = 0.0;
        $priced = array();
        foreach ($lines as $ln) {
            $p = epc_rtl_price_line((float) ($ln['unit_price'] ?? 0), (float) ($ln['qty'] ?? 0), $discounts, $taxRate);
            $gross += $p['gross'];
            $disc += $p['discount'];
            $net += $p['net'];
            $tax += $p['tax'];
            $total += $p['total'];
            $priced[] = array('item_id' => (int) ($ln['item_id'] ?? 0), 'qty' => (float) ($ln['qty'] ?? 0), 'unit_price' => (float) ($ln['unit_price'] ?? 0), 'line_discount' => $p['discount'], 'line_net' => $p['net']);
        }
        $gross = round($gross, 2);
        $disc = round($disc, 2);
        $net = round($net, 2);
        $tax = round($tax, 2);
        $total = round($total, 2);
        $db->prepare("INSERT INTO `epc_rtl_txn` (`company_id`,`channel_id`,`receipt_no`,`gross`,`discount`,`net`,`tax`,`total`,`tender_type`,`txn_time`) VALUES (?,?,?,?,?,?,?,?,?,?)")
           ->execute(array($companyId, $channelId, $receiptNo, $gross, $disc, $net, $tax, $total, $tender, $atTime));
        $txnId = (int) $db->lastInsertId();
        $insL = $db->prepare("INSERT INTO `epc_rtl_txn_line` (`txn_id`,`item_id`,`qty`,`unit_price`,`line_discount`,`line_net`) VALUES (?,?,?,?,?,?)");
        foreach ($priced as $pl) {
            $insL->execute(array($txnId, $pl['item_id'], $pl['qty'], $pl['unit_price'], $pl['line_discount'], $pl['line_net']));
        }
        return array('id' => $txnId, 'gross' => $gross, 'discount' => $disc, 'net' => $net, 'tax' => $tax, 'total' => $total);
    }
}

if (!function_exists('epc_rtl_transactions')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rtl_transactions(PDO $db, int $companyId, int $channelId = 0, int $from = 0, int $to = 0, int $limit = 200): array
    {
        epc_rtl_ensure_schema($db);
        $sql = "SELECT * FROM `epc_rtl_txn` WHERE company_id=?";
        $args = array($companyId);
        if ($channelId > 0) {
            $sql .= " AND channel_id=?";
            $args[] = $channelId;
        }
        if ($from > 0) {
            $sql .= " AND txn_time>=?";
            $args[] = $from;
        }
        if ($to > 0) {
            $sql .= " AND txn_time<=?";
            $args[] = $to;
        }
        $sql .= " ORDER BY id DESC LIMIT " . max(1, $limit);
        $st = $db->prepare($sql);
        $st->execute($args);
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_rtl_statement')) {
    /**
     * End-of-day statement (Z-report) for a channel over a period.
     *
     * @return array<string,mixed>
     */
    function epc_rtl_statement(PDO $db, int $companyId, int $channelId, int $from = 0, int $to = 0): array
    {
        $txns = epc_rtl_transactions($db, $companyId, $channelId, $from, $to, 100000);
        return epc_rtl_statement_totals($txns);
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_rtl_summary')) {
    /** @return array{channels:int,discounts:int,transactions:int,sales_total:float} */
    function epc_rtl_summary(PDO $db, int $companyId = 0): array
    {
        epc_rtl_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        return array(
            'channels' => (int) $db->query("SELECT COUNT(*) FROM `epc_rtl_channel` WHERE 1=1" . $wa)->fetchColumn(),
            'discounts' => (int) $db->query("SELECT COUNT(*) FROM `epc_rtl_discount` WHERE active=1" . $wa)->fetchColumn(),
            'transactions' => (int) $db->query("SELECT COUNT(*) FROM `epc_rtl_txn` WHERE 1=1" . $wa)->fetchColumn(),
            'sales_total' => round((float) $db->query("SELECT COALESCE(SUM(total),0) FROM `epc_rtl_txn` WHERE 1=1" . $wa)->fetchColumn(), 2),
        );
    }
}
