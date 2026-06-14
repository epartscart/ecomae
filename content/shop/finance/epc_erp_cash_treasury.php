<?php
/**
 * Cash & treasury depth — cash flow forecast + bank instrument lifecycle.
 *
 * Cash flow forecast: forecast header (opening balance) -> dated in/out lines
 *   -> running-balance projection (closing + minimum balance).
 * Bank instruments: letters of credit / bank guarantees / SBLC with a status
 *   lifecycle (draft -> issued -> amended/utilized -> expired/closed) and an
 *   event log; outstanding exposure = open instruments.
 *
 * Additive: new epc_cft_* tables. Multi-company aware.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_cft_ensure_schema')) {
    function epc_cft_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cft_forecast` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `opening_balance` decimal(18,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(8) NOT NULL DEFAULT '',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cash flow forecasts'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cft_line` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `forecast_id` int(11) NOT NULL DEFAULT 0,
            `due_date` varchar(16) NOT NULL DEFAULT '',
            `direction` varchar(4) NOT NULL DEFAULT 'in',
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `category` varchar(80) NOT NULL DEFAULT '',
            `source` varchar(120) NOT NULL DEFAULT '',
            `notes` varchar(255) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            KEY `x_forecast` (`forecast_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Cash flow forecast lines'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cft_instrument` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `ref` varchar(60) NOT NULL DEFAULT '',
            `type` varchar(12) NOT NULL DEFAULT 'lc',
            `beneficiary` varchar(180) NOT NULL DEFAULT '',
            `applicant` varchar(180) NOT NULL DEFAULT '',
            `bank` varchar(180) NOT NULL DEFAULT '',
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(8) NOT NULL DEFAULT '',
            `issue_date` varchar(16) NOT NULL DEFAULT '',
            `expiry_date` varchar(16) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'draft',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bank instruments (LC/BG/SBLC)'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_cft_instr_event` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `instrument_id` int(11) NOT NULL DEFAULT 0,
            `event_type` varchar(24) NOT NULL DEFAULT '',
            `detail` varchar(255) NOT NULL DEFAULT '',
            `amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_instrument` (`instrument_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bank instrument events'");
    }
}

/* ---------------- cash flow forecast ---------------- */

if (!function_exists('epc_cft_forecast_save')) {
    /** @param array<string,mixed> $data company_id, name, opening_balance, currency, notes */
    function epc_cft_forecast_save(PDO $db, array $data, int $id = 0): int
    {
        epc_cft_ensure_schema($db);
        $name = trim((string) ($data['name'] ?? ''));
        if ($name === '') {
            throw new Exception('Forecast name is required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_cft_forecast` SET `name`=?, `opening_balance`=?, `currency`=?, `notes`=? WHERE `id`=?")
               ->execute(array($name, (float) ($data['opening_balance'] ?? 0), (string) ($data['currency'] ?? ''), (string) ($data['notes'] ?? ''), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_cft_forecast` (`company_id`,`name`,`opening_balance`,`currency`,`notes`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $name, (float) ($data['opening_balance'] ?? 0), (string) ($data['currency'] ?? ''), (string) ($data['notes'] ?? ''), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_cft_forecast_get')) {
    /** @return array<string,mixed>|null */
    function epc_cft_forecast_get(PDO $db, int $id): ?array
    {
        epc_cft_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cft_forecast` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_cft_forecasts')) {
    /** @return array<int,array<string,mixed>> */
    function epc_cft_forecasts(PDO $db, int $companyId): array
    {
        epc_cft_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cft_forecast` WHERE `company_id`=? ORDER BY `id` DESC");
        $st->execute(array($companyId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_cft_line_add')) {
    /** @param array<string,mixed> $data due_date, direction(in|out), amount, category, source, notes */
    function epc_cft_line_add(PDO $db, int $forecastId, array $data): int
    {
        epc_cft_ensure_schema($db);
        if (!epc_cft_forecast_get($db, $forecastId)) {
            throw new Exception('Forecast not found');
        }
        $dir = (string) ($data['direction'] ?? 'in');
        if (!in_array($dir, array('in', 'out'), true)) {
            throw new Exception('Direction must be in or out');
        }
        $db->prepare("INSERT INTO `epc_cft_line` (`forecast_id`,`due_date`,`direction`,`amount`,`category`,`source`,`notes`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array($forecastId, (string) ($data['due_date'] ?? ''), $dir, abs((float) ($data['amount'] ?? 0)), (string) ($data['category'] ?? ''), (string) ($data['source'] ?? ''), (string) ($data['notes'] ?? '')));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_cft_lines')) {
    /** @return array<int,array<string,mixed>> ordered by due date then id */
    function epc_cft_lines(PDO $db, int $forecastId): array
    {
        epc_cft_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cft_line` WHERE `forecast_id`=? ORDER BY `due_date` ASC, `id` ASC");
        $st->execute(array($forecastId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_cft_projection')) {
    /**
     * Running-balance projection from opening balance.
     * @return array{opening:float,total_in:float,total_out:float,closing:float,min_balance:float,rows:array<int,array<string,mixed>>}
     */
    function epc_cft_projection(PDO $db, int $forecastId): array
    {
        $fc = epc_cft_forecast_get($db, $forecastId);
        $opening = $fc ? (float) $fc['opening_balance'] : 0.0;
        $lines = epc_cft_lines($db, $forecastId);
        $bal = $opening;
        $min = $opening;
        $tin = 0.0;
        $tout = 0.0;
        $rows = array();
        foreach ($lines as $l) {
            $amt = (float) $l['amount'];
            if ($l['direction'] === 'in') {
                $bal += $amt;
                $tin += $amt;
            } else {
                $bal -= $amt;
                $tout += $amt;
            }
            if ($bal < $min) {
                $min = $bal;
            }
            $l['running_balance'] = $bal;
            $rows[] = $l;
        }
        return array(
            'opening' => $opening,
            'total_in' => $tin,
            'total_out' => $tout,
            'closing' => $bal,
            'min_balance' => $min,
            'rows' => $rows,
        );
    }
}

/* ---------------- bank instruments (LC / BG / SBLC) ---------------- */

if (!function_exists('epc_cft_instr_transitions')) {
    /** @return array<string,array<int,string>> allowed status transitions */
    function epc_cft_instr_transitions(): array
    {
        return array(
            'draft' => array('issued', 'cancelled'),
            'issued' => array('amended', 'utilized', 'expired', 'closed'),
            'amended' => array('utilized', 'expired', 'closed'),
            'utilized' => array('closed'),
            'expired' => array('closed'),
            'closed' => array(),
            'cancelled' => array(),
        );
    }
}

if (!function_exists('epc_cft_instrument_save')) {
    /** @param array<string,mixed> $data company_id, ref, type, beneficiary, applicant, bank, amount, currency, issue_date, expiry_date, notes */
    function epc_cft_instrument_save(PDO $db, array $data, int $id = 0): int
    {
        epc_cft_ensure_schema($db);
        $type = (string) ($data['type'] ?? 'lc');
        if (!in_array($type, array('lc', 'bg', 'sblc'), true)) {
            throw new Exception('Type must be lc, bg or sblc');
        }
        $ref = trim((string) ($data['ref'] ?? ''));
        if ($id > 0) {
            $db->prepare("UPDATE `epc_cft_instrument` SET `ref`=?, `type`=?, `beneficiary`=?, `applicant`=?, `bank`=?, `amount`=?, `currency`=?, `issue_date`=?, `expiry_date`=?, `notes`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array($ref, $type, (string) ($data['beneficiary'] ?? ''), (string) ($data['applicant'] ?? ''), (string) ($data['bank'] ?? ''), (float) ($data['amount'] ?? 0), (string) ($data['currency'] ?? ''), (string) ($data['issue_date'] ?? ''), (string) ($data['expiry_date'] ?? ''), (string) ($data['notes'] ?? ''), time(), $id));
            return $id;
        }
        $now = time();
        if ($ref === '') {
            $ref = strtoupper($type) . '-' . date('Ymd', $now) . '-' . substr((string) $now, -4);
        }
        $db->prepare("INSERT INTO `epc_cft_instrument` (`company_id`,`ref`,`type`,`beneficiary`,`applicant`,`bank`,`amount`,`currency`,`issue_date`,`expiry_date`,`status`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,'draft',?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $ref, $type, (string) ($data['beneficiary'] ?? ''), (string) ($data['applicant'] ?? ''), (string) ($data['bank'] ?? ''), (float) ($data['amount'] ?? 0), (string) ($data['currency'] ?? ''), (string) ($data['issue_date'] ?? ''), (string) ($data['expiry_date'] ?? ''), (string) ($data['notes'] ?? ''), $now, $now));
        $iid = (int) $db->lastInsertId();
        epc_cft_instr_log($db, $iid, 'created', 'Instrument drafted', (float) ($data['amount'] ?? 0));
        return $iid;
    }
}

if (!function_exists('epc_cft_instr_log')) {
    function epc_cft_instr_log(PDO $db, int $instrumentId, string $type, string $detail, float $amount = 0.0): void
    {
        epc_cft_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_cft_instr_event` (`instrument_id`,`event_type`,`detail`,`amount`,`time_created`) VALUES (?,?,?,?,?)")
           ->execute(array($instrumentId, $type, $detail, $amount, time()));
    }
}

if (!function_exists('epc_cft_instrument_get')) {
    /** @return array<string,mixed>|null */
    function epc_cft_instrument_get(PDO $db, int $id): ?array
    {
        epc_cft_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cft_instrument` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_cft_instruments')) {
    /** @return array<int,array<string,mixed>> */
    function epc_cft_instruments(PDO $db, int $companyId, string $status = ''): array
    {
        epc_cft_ensure_schema($db);
        $sql = "SELECT * FROM `epc_cft_instrument` WHERE `company_id`=?";
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

if (!function_exists('epc_cft_instrument_set_status')) {
    /** Transition an instrument's status (validated) and log the event. */
    function epc_cft_instrument_set_status(PDO $db, int $id, string $status, string $detail = '', float $amount = 0.0): void
    {
        epc_cft_ensure_schema($db);
        $inst = epc_cft_instrument_get($db, $id);
        if (!$inst) {
            throw new Exception('Instrument not found');
        }
        $trans = epc_cft_instr_transitions();
        $allowed = $trans[$inst['status']] ?? array();
        if (!in_array($status, $allowed, true)) {
            throw new Exception('Cannot move instrument from ' . $inst['status'] . ' to ' . $status);
        }
        $db->prepare("UPDATE `epc_cft_instrument` SET `status`=?, `time_updated`=? WHERE `id`=?")->execute(array($status, time(), $id));
        epc_cft_instr_log($db, $id, $status, $detail !== '' ? $detail : ('Status -> ' . $status), $amount);
    }
}

if (!function_exists('epc_cft_instr_events')) {
    /** @return array<int,array<string,mixed>> */
    function epc_cft_instr_events(PDO $db, int $instrumentId): array
    {
        epc_cft_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_cft_instr_event` WHERE `instrument_id`=? ORDER BY `id` ASC");
        $st->execute(array($instrumentId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_cft_instrument_summary')) {
    /** @return array<string,mixed> outstanding exposure = open instruments */
    function epc_cft_instrument_summary(PDO $db, int $companyId): array
    {
        epc_cft_ensure_schema($db);
        $out = array('draft' => 0, 'issued' => 0, 'amended' => 0, 'utilized' => 0, 'expired' => 0, 'closed' => 0, 'cancelled' => 0, 'count' => 0, 'exposure' => 0.0);
        $st = $db->prepare("SELECT `status`, COUNT(*) c, COALESCE(SUM(`amount`),0) v FROM `epc_cft_instrument` WHERE `company_id`=? GROUP BY `status`");
        $st->execute(array($companyId));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $s = (string) $r['status'];
            if (isset($out[$s])) {
                $out[$s] = (int) $r['c'];
            }
            $out['count'] += (int) $r['c'];
            if (in_array($s, array('issued', 'amended', 'utilized'), true)) {
                $out['exposure'] += (float) $r['v'];
            }
        }
        return $out;
    }
}
