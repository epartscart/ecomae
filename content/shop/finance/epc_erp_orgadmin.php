<?php
/**
 * Organization administration / Enterprise — enterprise global address book
 * (parties, postal addresses, electronic contacts) and working calendars
 * (working-week + holidays with due-date arithmetic).
 *
 * Complements epc_erp_org.php (legal entities / operating units / number
 * sequences). The calendar arithmetic helpers (is-working-day, count working
 * days, add working days) are pure so date math is deterministic & testable.
 * Multi-company aware. Additive only.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_oa_ensure_schema')) {
    function epc_oa_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_oa_party` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `party_type` varchar(16) NOT NULL DEFAULT 'organization',
            `name` varchar(190) NOT NULL DEFAULT '',
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Global address book parties'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_oa_address` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `party_id` int(11) NOT NULL DEFAULT 0,
            `purpose` varchar(16) NOT NULL DEFAULT 'business',
            `line1` varchar(255) NOT NULL DEFAULT '',
            `city` varchar(120) NOT NULL DEFAULT '',
            `state` varchar(120) NOT NULL DEFAULT '',
            `postcode` varchar(40) NOT NULL DEFAULT '',
            `country` varchar(60) NOT NULL DEFAULT '',
            `is_primary` tinyint(1) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_party` (`party_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Party postal addresses'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_oa_contact` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `party_id` int(11) NOT NULL DEFAULT 0,
            `contact_type` varchar(12) NOT NULL DEFAULT 'email',
            `value` varchar(190) NOT NULL DEFAULT '',
            `is_primary` tinyint(1) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_party` (`party_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Party electronic contacts'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_oa_calendar` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `working_days` varchar(20) NOT NULL DEFAULT '1,2,3,4,5',
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_company_code` (`company_id`,`code`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Working calendars'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_oa_holiday` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `calendar_id` int(11) NOT NULL DEFAULT 0,
            `holiday_date` varchar(10) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_cal_date` (`calendar_id`,`holiday_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Calendar holidays'");
    }
}

/* ---------------- pure calendar arithmetic ---------------- */

if (!function_exists('epc_oa_working_set')) {
    /**
     * Pure: parse a working_days CSV ("1,2,3,4,5", ISO weekday 1=Mon..7=Sun)
     * into a lookup set.
     *
     * @return array<int,bool>
     */
    function epc_oa_working_set(string $workingDays): array
    {
        $set = array();
        foreach (explode(',', $workingDays) as $d) {
            $d = (int) trim($d);
            if ($d >= 1 && $d <= 7) {
                $set[$d] = true;
            }
        }
        return $set;
    }
}

if (!function_exists('epc_oa_is_working_day')) {
    /**
     * Pure: is the given timestamp a working day for this calendar?
     *
     * @param array<int,string> $holidays list of 'Y-m-d'
     */
    function epc_oa_is_working_day(string $workingDays, array $holidays, int $ts): bool
    {
        $set = epc_oa_working_set($workingDays);
        $iso = (int) date('N', $ts);
        if (empty($set[$iso])) {
            return false;
        }
        return !in_array(date('Y-m-d', $ts), $holidays, true);
    }
}

if (!function_exists('epc_oa_working_days_between')) {
    /**
     * Pure: count working days in [from, to] inclusive (excludes weekends per
     * the calendar + listed holidays).
     *
     * @param array<int,string> $holidays
     */
    function epc_oa_working_days_between(string $workingDays, array $holidays, int $from, int $to): int
    {
        if ($to < $from) {
            return 0;
        }
        $count = 0;
        $day = strtotime(date('Y-m-d', $from));
        $end = strtotime(date('Y-m-d', $to));
        while ($day <= $end) {
            if (epc_oa_is_working_day($workingDays, $holidays, $day)) {
                $count++;
            }
            $day = strtotime('+1 day', $day);
        }
        return $count;
    }
}

if (!function_exists('epc_oa_add_working_days')) {
    /**
     * Pure: add N working days to a start date, skipping non-working days +
     * holidays. N=0 returns the next working day on/after start. Returns 'Y-m-d'.
     *
     * @param array<int,string> $holidays
     */
    function epc_oa_add_working_days(string $workingDays, array $holidays, int $start, int $n): string
    {
        $day = strtotime(date('Y-m-d', $start));
        // advance to first working day on/after start
        while (!epc_oa_is_working_day($workingDays, $holidays, $day)) {
            $day = strtotime('+1 day', $day);
        }
        $added = 0;
        while ($added < $n) {
            $day = strtotime('+1 day', $day);
            if (epc_oa_is_working_day($workingDays, $holidays, $day)) {
                $added++;
            }
        }
        return date('Y-m-d', $day);
    }
}

/* ---------------- address book ---------------- */

if (!function_exists('epc_oa_party_save')) {
    /** @param array<string,mixed> $data */
    function epc_oa_party_save(PDO $db, int $companyId, array $data, int $id = 0): int
    {
        epc_oa_ensure_schema($db);
        $type = (string) ($data['party_type'] ?? 'organization');
        if (!in_array($type, array('organization', 'person'), true)) {
            throw new Exception('Invalid party type');
        }
        if (trim((string) ($data['name'] ?? '')) === '') {
            throw new Exception('Party name is required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_oa_party` SET `party_type`=?, `name`=?, `time_updated`=? WHERE id=? AND company_id=?")
               ->execute(array($type, (string) $data['name'], time(), $id, $companyId));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_oa_party` (`company_id`,`party_type`,`name`,`time_updated`) VALUES (?,?,?,?)")
           ->execute(array($companyId, $type, (string) $data['name'], time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_oa_parties')) {
    /** @return array<int,array<string,mixed>> */
    function epc_oa_parties(PDO $db, int $companyId = 0): array
    {
        epc_oa_ensure_schema($db);
        $sql = "SELECT * FROM `epc_oa_party` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND company_id=?";
            $args[] = $companyId;
        }
        $sql .= " ORDER BY name";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['address_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_oa_address` WHERE party_id=" . (int) $r['id'])->fetchColumn();
            $r['contact_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_oa_contact` WHERE party_id=" . (int) $r['id'])->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_oa_address_save')) {
    /**
     * Save an address. When is_primary, clears primary on the party's other
     * addresses of the same purpose.
     *
     * @param array<string,mixed> $data
     */
    function epc_oa_address_save(PDO $db, int $partyId, array $data): int
    {
        epc_oa_ensure_schema($db);
        $purpose = (string) ($data['purpose'] ?? 'business');
        if (!in_array($purpose, array('business', 'invoice', 'delivery', 'home', 'other'), true)) {
            throw new Exception('Invalid address purpose');
        }
        $primary = !empty($data['is_primary']) ? 1 : 0;
        if ($primary) {
            $db->prepare("UPDATE `epc_oa_address` SET `is_primary`=0 WHERE party_id=? AND purpose=?")->execute(array($partyId, $purpose));
        }
        $db->prepare("INSERT INTO `epc_oa_address` (`party_id`,`purpose`,`line1`,`city`,`state`,`postcode`,`country`,`is_primary`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array($partyId, $purpose, (string) ($data['line1'] ?? ''), (string) ($data['city'] ?? ''), (string) ($data['state'] ?? ''), (string) ($data['postcode'] ?? ''), (string) ($data['country'] ?? ''), $primary));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_oa_addresses')) {
    /** @return array<int,array<string,mixed>> */
    function epc_oa_addresses(PDO $db, int $partyId): array
    {
        epc_oa_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_oa_address` WHERE party_id=? ORDER BY is_primary DESC, id");
        $st->execute(array($partyId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_oa_contact_save')) {
    /** @param array<string,mixed> $data */
    function epc_oa_contact_save(PDO $db, int $partyId, array $data): int
    {
        epc_oa_ensure_schema($db);
        $type = (string) ($data['contact_type'] ?? 'email');
        if (!in_array($type, array('email', 'phone', 'mobile', 'fax', 'url'), true)) {
            throw new Exception('Invalid contact type');
        }
        $primary = !empty($data['is_primary']) ? 1 : 0;
        if ($primary) {
            $db->prepare("UPDATE `epc_oa_contact` SET `is_primary`=0 WHERE party_id=? AND contact_type=?")->execute(array($partyId, $type));
        }
        $db->prepare("INSERT INTO `epc_oa_contact` (`party_id`,`contact_type`,`value`,`is_primary`) VALUES (?,?,?,?)")
           ->execute(array($partyId, $type, (string) ($data['value'] ?? ''), $primary));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_oa_contacts')) {
    /** @return array<int,array<string,mixed>> */
    function epc_oa_contacts(PDO $db, int $partyId): array
    {
        epc_oa_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_oa_contact` WHERE party_id=? ORDER BY is_primary DESC, id");
        $st->execute(array($partyId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

/* ---------------- calendars ---------------- */

if (!function_exists('epc_oa_calendar_save')) {
    /** @param array<string,mixed> $data */
    function epc_oa_calendar_save(PDO $db, int $companyId, array $data): int
    {
        epc_oa_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        if ($code === '') {
            throw new Exception('Calendar code is required');
        }
        $wd = trim((string) ($data['working_days'] ?? '1,2,3,4,5'));
        if (empty(epc_oa_working_set($wd))) {
            throw new Exception('At least one working day is required');
        }
        $db->prepare("INSERT INTO `epc_oa_calendar` (`company_id`,`code`,`name`,`working_days`,`time_updated`) VALUES (?,?,?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `working_days`=VALUES(`working_days`), `time_updated`=VALUES(`time_updated`)")
           ->execute(array($companyId, $code, (string) ($data['name'] ?? ''), $wd, time()));
        $st = $db->prepare("SELECT id FROM `epc_oa_calendar` WHERE company_id=? AND code=?");
        $st->execute(array($companyId, $code));
        return (int) $st->fetchColumn();
    }
}

if (!function_exists('epc_oa_calendars')) {
    /** @return array<int,array<string,mixed>> */
    function epc_oa_calendars(PDO $db, int $companyId = 0): array
    {
        epc_oa_ensure_schema($db);
        $sql = "SELECT * FROM `epc_oa_calendar` WHERE 1=1";
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
            $r['holiday_count'] = (int) $db->query("SELECT COUNT(*) FROM `epc_oa_holiday` WHERE calendar_id=" . (int) $r['id'])->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_oa_holiday_add')) {
    function epc_oa_holiday_add(PDO $db, int $calendarId, string $date, string $name = ''): void
    {
        epc_oa_ensure_schema($db);
        if (!preg_match('/^\d{4}-\d{2}-\d{2}$/', $date)) {
            throw new Exception('Holiday date must be Y-m-d');
        }
        $db->prepare("INSERT INTO `epc_oa_holiday` (`calendar_id`,`holiday_date`,`name`) VALUES (?,?,?)
                      ON DUPLICATE KEY UPDATE `name`=VALUES(`name`)")->execute(array($calendarId, $date, $name));
    }
}

if (!function_exists('epc_oa_holidays')) {
    /** @return array<int,array<string,mixed>> */
    function epc_oa_holidays(PDO $db, int $calendarId): array
    {
        epc_oa_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_oa_holiday` WHERE calendar_id=? ORDER BY holiday_date");
        $st->execute(array($calendarId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_oa_holiday_dates')) {
    /** @return array<int,string> */
    function epc_oa_holiday_dates(PDO $db, int $calendarId): array
    {
        epc_oa_ensure_schema($db);
        $st = $db->prepare("SELECT holiday_date FROM `epc_oa_holiday` WHERE calendar_id=?");
        $st->execute(array($calendarId));
        return $st->fetchAll(PDO::FETCH_COLUMN);
    }
}

/* ---------------- summary ---------------- */

if (!function_exists('epc_oa_summary')) {
    /** @return array{parties:int,addresses:int,calendars:int,holidays:int} */
    function epc_oa_summary(PDO $db, int $companyId = 0): array
    {
        epc_oa_ensure_schema($db);
        $wa = $companyId > 0 ? " AND company_id=" . $companyId : "";
        $parties = (int) $db->query("SELECT COUNT(*) FROM `epc_oa_party` WHERE 1=1" . $wa)->fetchColumn();
        $cals = (int) $db->query("SELECT COUNT(*) FROM `epc_oa_calendar` WHERE 1=1" . $wa)->fetchColumn();
        $addr = (int) $db->query("SELECT COUNT(*) FROM `epc_oa_address` a JOIN `epc_oa_party` p ON p.id=a.party_id WHERE 1=1" . ($companyId > 0 ? " AND p.company_id=" . $companyId : ""))->fetchColumn();
        $hol = (int) $db->query("SELECT COUNT(*) FROM `epc_oa_holiday` h JOIN `epc_oa_calendar` c ON c.id=h.calendar_id WHERE 1=1" . ($companyId > 0 ? " AND c.company_id=" . $companyId : ""))->fetchColumn();
        return array('parties' => $parties, 'addresses' => $addr, 'calendars' => $cals, 'holidays' => $hol);
    }
}
