<?php
/**
 * Advanced ERP — After-sales (returns/RMA, warranty, service & repair jobs).
 *
 * Critical for spare-parts/automotive/electronics tenants:
 *   - RMA: customer return authorisation with disposition (refund / replace /
 *     repair / reject) and restocking.
 *   - Warranty register: per-serial/per-invoice warranty with expiry check.
 *   - Service/repair jobs: job sheet with parts + labour lines and totals,
 *     warranty-covered vs chargeable.
 *
 * Additive: new epc_as_* tables. Polymorphic links to existing sales records
 * by (source_type, source_id); no existing tables changed.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_as_ensure_schema')) {
    function epc_as_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_as_rma` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `rma_no` varchar(40) NOT NULL DEFAULT '',
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `source_type` varchar(40) NOT NULL DEFAULT 'sales_order',
            `source_id` int(11) NOT NULL DEFAULT 0,
            `reason` varchar(190) DEFAULT NULL,
            `disposition` varchar(20) NOT NULL DEFAULT 'pending',
            `status` varchar(16) NOT NULL DEFAULT 'open',
            `refund_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `restock` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_customer` (`customer_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Return merchandise authorisations'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_as_rma_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `rma_id` int(11) NOT NULL,
            `item_id` int(11) NOT NULL,
            `qty` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `unit_price` decimal(14,2) NOT NULL DEFAULT 0.00,
            `condition_note` varchar(190) DEFAULT NULL,
            PRIMARY KEY (`id`),
            KEY `x_rma` (`rma_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='RMA return lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_as_warranty` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `serial_no` varchar(80) DEFAULT NULL,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `source_type` varchar(40) NOT NULL DEFAULT 'sales_order',
            `source_id` int(11) NOT NULL DEFAULT 0,
            `start_date` int(11) NOT NULL DEFAULT 0,
            `months` int(11) NOT NULL DEFAULT 0,
            `expires_at` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_serial` (`serial_no`),
            KEY `x_item` (`item_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Warranty register'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_as_jobs` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `job_no` varchar(40) NOT NULL DEFAULT '',
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `asset_ref` varchar(120) DEFAULT NULL,
            `complaint` text,
            `status` varchar(16) NOT NULL DEFAULT 'open',
            `under_warranty` tinyint(1) NOT NULL DEFAULT 0,
            `parts_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `labour_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `tax_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `grand_total` decimal(14,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Service / repair jobs'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_as_job_lines` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `job_id` int(11) NOT NULL,
            `line_type` varchar(10) NOT NULL DEFAULT 'part',
            `description` varchar(190) DEFAULT NULL,
            `item_id` int(11) NOT NULL DEFAULT 0,
            `qty` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `unit_price` decimal(14,2) NOT NULL DEFAULT 0.00,
            `tax_percent` decimal(7,3) NOT NULL DEFAULT 0.000,
            `chargeable` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            KEY `x_job` (`job_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Service job parts/labour lines'");
    }
}

/* ----------------------------- RMA ----------------------------- */

if (!function_exists('epc_as_rma_create')) {
    /**
     * @param array<string,mixed> $data customer_id, source_type, source_id, reason, rma_no, restock
     * @param array<int,array<string,mixed>> $lines item_id, qty, unit_price, condition_note
     */
    function epc_as_rma_create(PDO $db, array $data, array $lines): int
    {
        epc_as_ensure_schema($db);
        $now = time();
        $rmaNo = trim((string) ($data['rma_no'] ?? ''));
        if ($rmaNo === '') {
            $rmaNo = 'RMA-' . $now;
        }
        $db->prepare(
            "INSERT INTO `epc_as_rma` (`rma_no`,`customer_id`,`source_type`,`source_id`,`reason`,`disposition`,`status`,`restock`,`time_created`,`time_updated`)
             VALUES (?,?,?,?,?, 'pending','open', ?, ?,?)"
        )->execute(array(
            $rmaNo,
            (int) ($data['customer_id'] ?? 0),
            (string) ($data['source_type'] ?? 'sales_order'),
            (int) ($data['source_id'] ?? 0),
            (string) ($data['reason'] ?? ''),
            isset($data['restock']) ? (int) (bool) $data['restock'] : 1,
            $now,
            $now,
        ));
        $rmaId = (int) $db->lastInsertId();
        // Prefer stable RMA-{id} when caller did not supply a number.
        if (trim((string) ($data['rma_no'] ?? '')) === '' && $rmaId > 0) {
            $rmaNo = 'RMA-' . $rmaId;
            $db->prepare('UPDATE `epc_as_rma` SET `rma_no` = ? WHERE `id` = ?')->execute(array($rmaNo, $rmaId));
        }
        $ins = $db->prepare("INSERT INTO `epc_as_rma_lines` (`rma_id`,`item_id`,`qty`,`unit_price`,`condition_note`) VALUES (?,?,?,?,?)");
        foreach ($lines as $ln) {
            $ins->execute(array($rmaId, (int) $ln['item_id'], (float) ($ln['qty'] ?? 0), (float) ($ln['unit_price'] ?? 0), (string) ($ln['condition_note'] ?? '')));
        }
        // Blockchain BOS: anchor aftersales RMA create (best-effort) — same record_id as stored rma_no.
        try {
            $bcFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
            if (is_file($bcFile)) {
                require_once $bcFile;
                epc_bc_bos_maybe_record_document(
                    'rma',
                    $rmaNo,
                    array(
                        'rma_id' => $rmaId,
                        'rma_no' => $rmaNo,
                        'customer_id' => (int) ($data['customer_id'] ?? 0),
                        'source_type' => (string) ($data['source_type'] ?? 'sales_order'),
                        'source_id' => (int) ($data['source_id'] ?? 0),
                        'reason' => (string) ($data['reason'] ?? ''),
                        'line_count' => count($lines),
                        'channel' => 'aftersales',
                    )
                );
            }
        } catch (Throwable $e) {
            // best-effort
        }
        return $rmaId;
    }
}

if (!function_exists('epc_as_rma_list')) {
    /**
     * @return list<array<string,mixed>>
     */
    function epc_as_rma_list(PDO $db, int $limit = 100): array
    {
        epc_as_ensure_schema($db);
        $limit = max(1, min(500, $limit));
        $st = $db->query(
            "SELECT r.*,
                    (SELECT COUNT(*) FROM `epc_as_rma_lines` l WHERE l.rma_id = r.id) AS line_count
             FROM `epc_as_rma` r
             ORDER BY r.id DESC
             LIMIT {$limit}"
        );
        return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
    }
}

if (!function_exists('epc_as_rma_get')) {
    /**
     * @return array{header:array<string,mixed>,lines:list<array<string,mixed>>}|null
     */
    function epc_as_rma_get(PDO $db, int $rmaId): ?array
    {
        epc_as_ensure_schema($db);
        if ($rmaId <= 0) {
            return null;
        }
        $hdr = $db->prepare('SELECT * FROM `epc_as_rma` WHERE `id` = ? LIMIT 1');
        $hdr->execute(array($rmaId));
        $row = $hdr->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return null;
        }
        $lst = $db->prepare('SELECT * FROM `epc_as_rma_lines` WHERE `rma_id` = ? ORDER BY `id` ASC');
        $lst->execute(array($rmaId));
        return array(
            'header' => $row,
            'lines' => $lst->fetchAll(PDO::FETCH_ASSOC) ?: array(),
        );
    }
}

if (!function_exists('epc_as_rma_resolve')) {
    /**
     * Set disposition (refund/replace/repair/reject) and close. For 'refund'
     * the refund amount defaults to the sum of return lines. For dispositions
     * that restock, posts positive inventory movements when available.
     *
     * @return array<string,mixed>
     */
    function epc_as_rma_resolve(PDO $db, int $rmaId, string $disposition, float $refundAmount = -1, int $warehouseId = 0): array
    {
        epc_as_ensure_schema($db);
        $valid = array('refund', 'replace', 'repair', 'reject');
        if (!in_array($disposition, $valid, true)) {
            throw new Exception('Invalid disposition');
        }
        $hdr = $db->prepare("SELECT * FROM `epc_as_rma` WHERE `id`=?");
        $hdr->execute(array($rmaId));
        $rma = $hdr->fetch(PDO::FETCH_ASSOC);
        if (!$rma) {
            throw new Exception('RMA not found');
        }
        $lst = $db->prepare("SELECT * FROM `epc_as_rma_lines` WHERE `rma_id`=?");
        $lst->execute(array($rmaId));
        $lines = $lst->fetchAll(PDO::FETCH_ASSOC);

        $linesTotal = 0.0;
        foreach ($lines as $ln) {
            $linesTotal += (float) $ln['qty'] * (float) $ln['unit_price'];
        }
        $linesTotal = round($linesTotal, 2);
        if ($disposition === 'refund') {
            $refundAmount = $refundAmount < 0 ? $linesTotal : round($refundAmount, 2);
        } else {
            $refundAmount = $refundAmount < 0 ? 0.0 : round($refundAmount, 2);
        }

        $doRestock = ((int) $rma['restock'] === 1) && in_array($disposition, array('refund', 'replace'), true);
        if ($doRestock && function_exists('epc_erp_inventory_record_movement')) {
            foreach ($lines as $ln) {
                try {
                    epc_erp_inventory_record_movement($db, array(
                        'item_id' => (int) $ln['item_id'],
                        'warehouse_id' => $warehouseId,
                        'movement_type' => 'rma_restock',
                        'qty' => (float) $ln['qty'],
                        'unit_cost' => (float) $ln['unit_price'],
                        'reference' => $rma['rma_no'],
                        'movement_date' => date('Y-m-d'),
                    ));
                } catch (Throwable $e) {
                    // tracking-only fallback
                }
            }
        }

        $db->prepare("UPDATE `epc_as_rma` SET `disposition`=?, `status`='closed', `refund_amount`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($disposition, $refundAmount, time(), $rmaId));

        return array(
            'rma_id' => $rmaId,
            'disposition' => $disposition,
            'lines_total' => $linesTotal,
            'refund_amount' => $refundAmount,
            'restocked' => $doRestock,
        );
    }
}

/* --------------------------- Warranty -------------------------- */

if (!function_exists('epc_as_warranty_register')) {
    /**
     * @param array<string,mixed> $data item_id, serial_no, customer_id, source_type, source_id, start_date, months
     */
    function epc_as_warranty_register(PDO $db, array $data): int
    {
        epc_as_ensure_schema($db);
        $start = (int) ($data['start_date'] ?? time());
        $months = (int) ($data['months'] ?? 0);
        $expires = $months > 0 ? strtotime('+' . $months . ' months', $start) : 0;
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_as_warranty` (`item_id`,`serial_no`,`customer_id`,`source_type`,`source_id`,`start_date`,`months`,`expires_at`,`time_created`)
             VALUES (?,?,?,?,?,?,?,?,?)"
        )->execute(array(
            (int) ($data['item_id'] ?? 0),
            (string) ($data['serial_no'] ?? ''),
            (int) ($data['customer_id'] ?? 0),
            (string) ($data['source_type'] ?? 'sales_order'),
            (int) ($data['source_id'] ?? 0),
            $start,
            $months,
            (int) $expires,
            $now,
        ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_as_warranty_check')) {
    /**
     * Is an item/serial under warranty as-of a date?
     *
     * @return array<string,mixed> covered(bool), expires_at, days_left, record
     */
    function epc_as_warranty_check(PDO $db, int $itemId, string $serialNo = '', int $asOf = 0): array
    {
        epc_as_ensure_schema($db);
        $asOf = $asOf > 0 ? $asOf : time();
        if ($serialNo !== '') {
            $st = $db->prepare("SELECT * FROM `epc_as_warranty` WHERE `serial_no`=? ORDER BY `expires_at` DESC LIMIT 1");
            $st->execute(array($serialNo));
        } else {
            $st = $db->prepare("SELECT * FROM `epc_as_warranty` WHERE `item_id`=? ORDER BY `expires_at` DESC LIMIT 1");
            $st->execute(array($itemId));
        }
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if (!$row) {
            return array('covered' => false, 'expires_at' => 0, 'days_left' => 0, 'record' => null);
        }
        $exp = (int) $row['expires_at'];
        $covered = $exp > 0 && $exp >= $asOf;
        return array(
            'covered' => $covered,
            'expires_at' => $exp,
            'days_left' => $covered ? (int) floor(($exp - $asOf) / 86400) : 0,
            'record' => $row,
        );
    }
}

/* ------------------------ Service jobs ------------------------- */

if (!function_exists('epc_as_job_create')) {
    /**
     * @param array<string,mixed> $data customer_id, asset_ref, complaint, under_warranty, job_no
     */
    function epc_as_job_create(PDO $db, array $data): int
    {
        epc_as_ensure_schema($db);
        $now = time();
        $db->prepare(
            "INSERT INTO `epc_as_jobs` (`job_no`,`customer_id`,`asset_ref`,`complaint`,`status`,`under_warranty`,`time_created`,`time_updated`)
             VALUES (?,?,?,?, 'open', ?, ?,?)"
        )->execute(array(
            (string) ($data['job_no'] ?? ('JOB-' . $now)),
            (int) ($data['customer_id'] ?? 0),
            (string) ($data['asset_ref'] ?? ''),
            (string) ($data['complaint'] ?? ''),
            isset($data['under_warranty']) ? (int) (bool) $data['under_warranty'] : 0,
            $now,
            $now,
        ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_as_job_add_line')) {
    /**
     * @param array<string,mixed> $line line_type(part|labour), description, item_id, qty, unit_price, tax_percent, chargeable
     */
    function epc_as_job_add_line(PDO $db, int $jobId, array $line): int
    {
        epc_as_ensure_schema($db);
        $db->prepare(
            "INSERT INTO `epc_as_job_lines` (`job_id`,`line_type`,`description`,`item_id`,`qty`,`unit_price`,`tax_percent`,`chargeable`)
             VALUES (?,?,?,?,?,?,?,?)"
        )->execute(array(
            $jobId,
            (string) ($line['line_type'] ?? 'part'),
            (string) ($line['description'] ?? ''),
            (int) ($line['item_id'] ?? 0),
            (float) ($line['qty'] ?? 0),
            (float) ($line['unit_price'] ?? 0),
            (float) ($line['tax_percent'] ?? 0),
            isset($line['chargeable']) ? (int) (bool) $line['chargeable'] : 1,
        ));
        $lineId = (int) $db->lastInsertId();
        epc_as_job_recalc($db, $jobId);
        return $lineId;
    }
}

if (!function_exists('epc_as_job_recalc')) {
    /**
     * Recompute job totals. Non-chargeable lines (warranty-covered) are
     * excluded from billable totals but kept on the job sheet.
     *
     * @return array<string,float>
     */
    function epc_as_job_recalc(PDO $db, int $jobId): array
    {
        epc_as_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_as_job_lines` WHERE `job_id`=?");
        $st->execute(array($jobId));
        $parts = 0.0;
        $labour = 0.0;
        $tax = 0.0;
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $ln) {
            if ((int) $ln['chargeable'] !== 1) {
                continue;
            }
            $lineNet = (float) $ln['qty'] * (float) $ln['unit_price'];
            $lineTax = $lineNet * (float) $ln['tax_percent'] / 100;
            $tax += $lineTax;
            if ($ln['line_type'] === 'labour') {
                $labour += $lineNet;
            } else {
                $parts += $lineNet;
            }
        }
        $parts = round($parts, 2);
        $labour = round($labour, 2);
        $tax = round($tax, 2);
        $grand = round($parts + $labour + $tax, 2);
        $db->prepare("UPDATE `epc_as_jobs` SET `parts_total`=?, `labour_total`=?, `tax_total`=?, `grand_total`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($parts, $labour, $tax, $grand, time(), $jobId));
        return array('parts_total' => $parts, 'labour_total' => $labour, 'tax_total' => $tax, 'grand_total' => $grand);
    }
}

if (!function_exists('epc_as_job_close')) {
    function epc_as_job_close(PDO $db, int $jobId): void
    {
        epc_as_ensure_schema($db);
        epc_as_job_recalc($db, $jobId);
        $db->prepare("UPDATE `epc_as_jobs` SET `status`='closed', `time_updated`=? WHERE `id`=?")->execute(array(time(), $jobId));
    }
}
