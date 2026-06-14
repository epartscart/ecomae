<?php
/**
 * Document Expiry Tracker — one central register for every expiring document in
 * the organisation (legal, customer, insurance, banking + custom types), with
 * derived status (Valid / Expiring soon / Expired) and configurable auto-email
 * reminders at multiple lead times (e.g. 90 / 60 / 30 / 7 days before expiry).
 *
 * Design notes
 * ------------
 *  - Multi-company aware: every record is stamped with the active company id
 *    (enterprise legal entity) so each company keeps its own register.
 *  - Country-driven compliance (hard platform rule): the statutory document
 *    checklist + default reminder lead times are resolved from the tenant's
 *    REGISTRATION COUNTRY via epc_co_profile_get() — never hard-coded, with a
 *    safe generic fallback for unknown countries.
 *  - Insurance feed: the Insurance module registers each policy's expiry here
 *    through source_module='insurance' so all expiries live in one place; those
 *    rows are kept in sync by the insurance engine (read-only in this tab).
 *  - Reminder engine is pure + idempotent: each (document, threshold) fires at
 *    most once (logged in epc_erp_doc_expiry_reminders), so re-running the
 *    dispatcher never double-sends.
 *
 * Additive + backward compatible — no posting/GL impact.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_docx_ensure_schema')) {
    function epc_docx_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_doc_expiry` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `category` varchar(24) NOT NULL DEFAULT 'other',
            `doc_type` varchar(120) NOT NULL DEFAULT '',
            `title` varchar(200) NOT NULL DEFAULT '',
            `ref_no` varchar(120) NOT NULL DEFAULT '',
            `owner` varchar(200) NOT NULL DEFAULT '',
            `owner_email` varchar(200) NOT NULL DEFAULT '',
            `issuer` varchar(200) NOT NULL DEFAULT '',
            `issue_date` int(11) NOT NULL DEFAULT 0,
            `expiry_date` int(11) NOT NULL DEFAULT 0,
            `reminder_days` varchar(120) NOT NULL DEFAULT '90,60,30,7',
            `attachment_path` varchar(255) NOT NULL DEFAULT '',
            `note` text,
            `source_module` varchar(32) NOT NULL DEFAULT '',
            `source_ref_id` int(11) NOT NULL DEFAULT 0,
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_expiry` (`expiry_date`),
            KEY `x_source` (`source_module`,`source_ref_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Document expiry register'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_doc_expiry_reminders` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `doc_id` int(11) NOT NULL,
            `threshold_days` int(11) NOT NULL,
            `days_left` int(11) NOT NULL DEFAULT 0,
            `recipient` varchar(200) NOT NULL DEFAULT '',
            `channel` varchar(16) NOT NULL DEFAULT 'email',
            `sent_at` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            UNIQUE KEY `x_doc_threshold` (`doc_id`,`threshold_days`),
            KEY `x_doc` (`doc_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Document expiry reminder log'");
    }
}

if (!function_exists('epc_docx_categories')) {
    /** @return array<string,string> category key => label */
    function epc_docx_categories(): array
    {
        return array(
            'legal' => 'Legal',
            'customer' => 'Customer',
            'insurance' => 'Insurance',
            'banking' => 'Banking',
            'other' => 'Other',
        );
    }
}

if (!function_exists('epc_docx_resolve_country')) {
    /** Resolve the tenant's registration country (upper ISO-2), safe fallback. */
    function epc_docx_resolve_country(PDO $db): string
    {
        if (function_exists('epc_co_profile_get')) {
            $p = epc_co_profile_get($db);
            $c = strtoupper(substr(trim((string) ($p['country'] ?? '')), 0, 2));
            if ($c !== '') {
                return $c;
            }
        }
        return '';
    }
}

if (!function_exists('epc_docx_country_profile')) {
    /**
     * Country-driven compliance profile: statutory document checklist + the
     * default reminder lead times for that jurisdiction. Driven by the tenant's
     * registration country with a generic fallback (never hard-coded).
     *
     * @return array{country:string,default_reminder_days:string,documents:array<int,array{type:string,category:string,authority:string}>}
     */
    function epc_docx_country_profile(string $country): array
    {
        $c = strtoupper(trim($country));
        $packs = array(
            'AE' => array(
                'reminder' => '90,60,30,7',
                'docs' => array(
                    array('Trade Licence', 'legal', 'Department of Economy / DED'),
                    array('Establishment Card', 'legal', 'ICP / MOHRE'),
                    array('Chamber of Commerce Membership', 'legal', 'Chamber of Commerce'),
                    array('VAT Registration (TRN)', 'legal', 'Federal Tax Authority'),
                    array('Tenancy Contract (Ejari)', 'legal', 'Municipality / Ejari'),
                    array('Import-Export Code', 'legal', 'Customs'),
                    array('Trademark Registration', 'legal', 'Ministry of Economy'),
                ),
            ),
            'SA' => array(
                'reminder' => '90,60,30,7',
                'docs' => array(
                    array('Commercial Registration (CR)', 'legal', 'Ministry of Commerce'),
                    array('VAT Registration', 'legal', 'ZATCA'),
                    array('Saudization (Nitaqat) Certificate', 'legal', 'HRSD'),
                    array('Chamber of Commerce Membership', 'legal', 'Chamber of Commerce'),
                    array('Municipal Licence (Baladiya)', 'legal', 'Municipality'),
                ),
            ),
            'GB' => array(
                'reminder' => '90,60,30,14',
                'docs' => array(
                    array('Companies House Confirmation Statement', 'legal', 'Companies House'),
                    array('VAT Registration', 'legal', 'HMRC'),
                    array('Employer Liability Insurance', 'insurance', 'Insurer'),
                    array('Data Protection (ICO) Registration', 'legal', 'ICO'),
                ),
            ),
            'IN' => array(
                'reminder' => '90,60,30,7',
                'docs' => array(
                    array('GST Registration', 'legal', 'GSTN'),
                    array('Shops & Establishment Licence', 'legal', 'State Labour Dept'),
                    array('Import Export Code (IEC)', 'legal', 'DGFT'),
                    array('Professional Tax Registration', 'legal', 'State'),
                ),
            ),
            'PK' => array(
                'reminder' => '90,60,30,7',
                'docs' => array(
                    array('NTN / Sales Tax Registration', 'legal', 'FBR'),
                    array('Chamber of Commerce Membership', 'legal', 'Chamber of Commerce'),
                    array('Import Export Registration', 'legal', 'WeBOC'),
                ),
            ),
        );

        if (isset($packs[$c])) {
            $docs = array();
            foreach ($packs[$c]['docs'] as $d) {
                $docs[] = array('type' => $d[0], 'category' => $d[1], 'authority' => $d[2]);
            }
            return array(
                'country' => $c,
                'default_reminder_days' => $packs[$c]['reminder'],
                'documents' => $docs,
            );
        }

        // Generic fallback — universal corporate documents that exist everywhere.
        return array(
            'country' => $c !== '' ? $c : 'XX',
            'default_reminder_days' => '90,60,30,7',
            'documents' => array(
                array('type' => 'Business Registration', 'category' => 'legal', 'authority' => 'Companies Registry'),
                array('type' => 'Tax Registration', 'category' => 'legal', 'authority' => 'Tax Authority'),
                array('type' => 'Bank Guarantee', 'category' => 'banking', 'authority' => 'Bank'),
            ),
        );
    }
}

if (!function_exists('epc_docx_parse_reminder_days')) {
    /**
     * Parse a "90,60,30,7" CSV into a clean, de-duplicated, descending int list.
     *
     * @return array<int,int>
     */
    function epc_docx_parse_reminder_days(string $csv): array
    {
        $out = array();
        foreach (explode(',', $csv) as $part) {
            $n = (int) trim($part);
            if ($n > 0) {
                $out[$n] = $n;
            }
        }
        $out = array_values($out);
        rsort($out);
        return $out;
    }
}

if (!function_exists('epc_docx_days_left')) {
    /** Whole days from now until expiry (negative = already expired). */
    function epc_docx_days_left(int $expiry, int $now = 0): int
    {
        if ($expiry <= 0) {
            return 0;
        }
        if ($now <= 0) {
            $now = time();
        }
        return (int) floor(($expiry - $now) / 86400);
    }
}

if (!function_exists('epc_docx_status')) {
    /**
     * Derive a status from the expiry date.
     *   none     — no expiry set
     *   expired  — past
     *   expiring — within the "soon" window (default 30 days)
     *   valid    — beyond the window
     */
    function epc_docx_status(int $expiry, int $now = 0, int $soonDays = 30): string
    {
        if ($expiry <= 0) {
            return 'none';
        }
        $left = epc_docx_days_left($expiry, $now);
        if ($left < 0) {
            return 'expired';
        }
        if ($left <= $soonDays) {
            return 'expiring';
        }
        return 'valid';
    }
}

if (!function_exists('epc_docx_due_thresholds')) {
    /**
     * Pure reminder selector: given an expiry, the configured lead days, the
     * current time and the thresholds already sent, return the unsent
     * thresholds that are now due (ascending — index 0 is the most urgent).
     *
     * A threshold D is due when days_left <= D. When a document is added late,
     * several larger thresholds can be due at once — the dispatcher sends one
     * email and marks them all as sent so reminders never spam.
     *
     * @param array<int,int> $reminderDays
     * @param array<int,int> $sent
     * @return array<int,int>
     */
    function epc_docx_due_thresholds(int $expiry, array $reminderDays, int $now = 0, array $sent = array()): array
    {
        if ($expiry <= 0) {
            return array();
        }
        if ($now <= 0) {
            $now = time();
        }
        $left = epc_docx_days_left($expiry, $now);
        $sentMap = array();
        foreach ($sent as $s) {
            $sentMap[(int) $s] = true;
        }
        $due = array();
        foreach ($reminderDays as $d) {
            $d = (int) $d;
            if ($d <= 0) {
                continue;
            }
            if ($left <= $d && empty($sentMap[$d])) {
                $due[$d] = $d;
            }
        }
        $due = array_values($due);
        sort($due);
        return $due;
    }
}

if (!function_exists('epc_docx_save')) {
    /**
     * Insert or update a register row. Returns the row id.
     *
     * @param array<string,mixed> $data
     */
    function epc_docx_save(PDO $db, array $data, int $id = 0): int
    {
        epc_docx_ensure_schema($db);
        $cats = epc_docx_categories();
        $category = (string) ($data['category'] ?? 'other');
        if (!isset($cats[$category])) {
            $category = 'other';
        }
        $companyId = (int) ($data['company_id'] ?? 0);
        $reminder = trim((string) ($data['reminder_days'] ?? '90,60,30,7'));
        $parsed = epc_docx_parse_reminder_days($reminder);
        $reminder = $parsed ? implode(',', $parsed) : '90,60,30,7';
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_erp_doc_expiry` SET `category`=?, `doc_type`=?, `title`=?, `ref_no`=?, `owner`=?, `owner_email`=?, `issuer`=?, `issue_date`=?, `expiry_date`=?, `reminder_days`=?, `attachment_path`=?, `note`=?, `active`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array(
                   $category, (string) ($data['doc_type'] ?? ''), (string) ($data['title'] ?? ''),
                   (string) ($data['ref_no'] ?? ''), (string) ($data['owner'] ?? ''), (string) ($data['owner_email'] ?? ''),
                   (string) ($data['issuer'] ?? ''), (int) ($data['issue_date'] ?? 0), (int) ($data['expiry_date'] ?? 0),
                   $reminder, (string) ($data['attachment_path'] ?? ''), (string) ($data['note'] ?? ''),
                   (int) (!empty($data['active']) ? 1 : (isset($data['active']) ? 0 : 1)), $now, $id,
               ));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_erp_doc_expiry` (`company_id`,`category`,`doc_type`,`title`,`ref_no`,`owner`,`owner_email`,`issuer`,`issue_date`,`expiry_date`,`reminder_days`,`attachment_path`,`note`,`source_module`,`source_ref_id`,`active`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,1,?,?)")
           ->execute(array(
               $companyId, $category, (string) ($data['doc_type'] ?? ''), (string) ($data['title'] ?? ''),
               (string) ($data['ref_no'] ?? ''), (string) ($data['owner'] ?? ''), (string) ($data['owner_email'] ?? ''),
               (string) ($data['issuer'] ?? ''), (int) ($data['issue_date'] ?? 0), (int) ($data['expiry_date'] ?? 0),
               $reminder, (string) ($data['attachment_path'] ?? ''), (string) ($data['note'] ?? ''),
               (string) ($data['source_module'] ?? ''), (int) ($data['source_ref_id'] ?? 0), $now, $now,
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_docx_upsert_source')) {
    /**
     * Upsert a register row keyed by (source_module, source_ref_id) — used by
     * the Insurance module to feed policy expiries in without creating
     * duplicates. Returns the row id.
     *
     * @param array<string,mixed> $data
     */
    function epc_docx_upsert_source(PDO $db, string $module, int $refId, array $data): int
    {
        epc_docx_ensure_schema($db);
        $st = $db->prepare("SELECT `id` FROM `epc_erp_doc_expiry` WHERE `source_module`=? AND `source_ref_id`=? LIMIT 1");
        $st->execute(array($module, $refId));
        $existing = (int) $st->fetchColumn();
        if ($existing > 0) {
            // keep the existing reminder log valid by only touching content fields
            $reminder = trim((string) ($data['reminder_days'] ?? ''));
            $parsed = epc_docx_parse_reminder_days($reminder);
            $reminder = $parsed ? implode(',', $parsed) : '90,60,30,7';
            $db->prepare("UPDATE `epc_erp_doc_expiry` SET `category`=?, `doc_type`=?, `title`=?, `ref_no`=?, `owner`=?, `owner_email`=?, `issuer`=?, `issue_date`=?, `expiry_date`=?, `reminder_days`=?, `note`=?, `active`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array(
                   (string) ($data['category'] ?? 'insurance'), (string) ($data['doc_type'] ?? 'Insurance Policy'),
                   (string) ($data['title'] ?? ''), (string) ($data['ref_no'] ?? ''), (string) ($data['owner'] ?? ''),
                   (string) ($data['owner_email'] ?? ''), (string) ($data['issuer'] ?? ''),
                   (int) ($data['issue_date'] ?? 0), (int) ($data['expiry_date'] ?? 0), $reminder,
                   (string) ($data['note'] ?? ''), (int) (!empty($data['active']) ? 1 : 0), time(), $existing,
               ));
            return $existing;
        }
        $data['source_module'] = $module;
        $data['source_ref_id'] = $refId;
        if (!isset($data['category'])) {
            $data['category'] = 'insurance';
        }
        return epc_docx_save($db, $data, 0);
    }
}

if (!function_exists('epc_docx_delete_source')) {
    /** Remove the register row fed by a source module record. */
    function epc_docx_delete_source(PDO $db, string $module, int $refId): void
    {
        epc_docx_ensure_schema($db);
        $st = $db->prepare("SELECT `id` FROM `epc_erp_doc_expiry` WHERE `source_module`=? AND `source_ref_id`=? LIMIT 1");
        $st->execute(array($module, $refId));
        $id = (int) $st->fetchColumn();
        if ($id > 0) {
            $db->prepare("DELETE FROM `epc_erp_doc_expiry_reminders` WHERE `doc_id`=?")->execute(array($id));
            $db->prepare("DELETE FROM `epc_erp_doc_expiry` WHERE `id`=?")->execute(array($id));
        }
    }
}

if (!function_exists('epc_docx_delete')) {
    function epc_docx_delete(PDO $db, int $id): void
    {
        epc_docx_ensure_schema($db);
        $db->prepare("DELETE FROM `epc_erp_doc_expiry_reminders` WHERE `doc_id`=?")->execute(array($id));
        $db->prepare("DELETE FROM `epc_erp_doc_expiry` WHERE `id`=?")->execute(array($id));
    }
}

if (!function_exists('epc_docx_get')) {
    /** @return array<string,mixed>|null */
    function epc_docx_get(PDO $db, int $id): ?array
    {
        epc_docx_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_doc_expiry` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_docx_list')) {
    /**
     * List register rows for a company (0 = all), newest expiry first, each
     * decorated with derived status + days_left.
     *
     * @return array<int,array<string,mixed>>
     */
    function epc_docx_list(PDO $db, int $companyId = 0, string $category = '', int $now = 0): array
    {
        epc_docx_ensure_schema($db);
        if ($now <= 0) {
            $now = time();
        }
        $sql = "SELECT * FROM `epc_erp_doc_expiry` WHERE `active`=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND `company_id`=?";
            $args[] = $companyId;
        }
        if ($category !== '') {
            $sql .= " AND `category`=?";
            $args[] = $category;
        }
        $sql .= " ORDER BY (`expiry_date`=0), `expiry_date` ASC, `id` DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['days_left'] = epc_docx_days_left((int) $r['expiry_date'], $now);
            $r['status'] = epc_docx_status((int) $r['expiry_date'], $now);
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_docx_summary')) {
    /**
     * @return array{total:int,valid:int,expiring:int,expired:int}
     */
    function epc_docx_summary(PDO $db, int $companyId = 0, int $now = 0): array
    {
        $rows = epc_docx_list($db, $companyId, '', $now);
        $out = array('total' => 0, 'valid' => 0, 'expiring' => 0, 'expired' => 0);
        foreach ($rows as $r) {
            $out['total']++;
            if ($r['status'] === 'valid') {
                $out['valid']++;
            } elseif ($r['status'] === 'expiring') {
                $out['expiring']++;
            } elseif ($r['status'] === 'expired') {
                $out['expired']++;
            }
        }
        return $out;
    }
}

if (!function_exists('epc_docx_reminders_sent')) {
    /** @return array<int,int> thresholds already sent for a document */
    function epc_docx_reminders_sent(PDO $db, int $docId): array
    {
        epc_docx_ensure_schema($db);
        $st = $db->prepare("SELECT `threshold_days` FROM `epc_erp_doc_expiry_reminders` WHERE `doc_id`=?");
        $st->execute(array($docId));
        return array_map('intval', $st->fetchAll(PDO::FETCH_COLUMN));
    }
}

if (!function_exists('epc_docx_reminder_recipient')) {
    /** Resolve the reminder recipient — document owner email, else admin inbox. */
    function epc_docx_reminder_recipient(array $row): string
    {
        $email = trim((string) ($row['owner_email'] ?? ''));
        if ($email !== '' && filter_var($email, FILTER_VALIDATE_EMAIL)) {
            return $email;
        }
        if (function_exists('epc_admin_notify_email')) {
            return (string) epc_admin_notify_email();
        }
        return '';
    }
}

if (!function_exists('epc_docx_send_email')) {
    /**
     * Send one reminder email. Returns true on hand-off to the MTA. Isolated so
     * tests can stub it; uses the platform mail() like other ERP notifiers.
     */
    function epc_docx_send_email(string $to, string $subject, string $body): bool
    {
        if ($to === '') {
            return false;
        }
        if (defined('EPC_DOCX_SUPPRESS_MAIL') && EPC_DOCX_SUPPRESS_MAIL) {
            return true;
        }
        $headers = 'MIME-Version: 1.0' . "\r\n"
            . 'Content-Type: text/plain; charset=UTF-8' . "\r\n"
            . 'X-Mailer: ECOM-AE-ERP';
        return @mail($to, $subject, $body, $headers);
    }
}

if (!function_exists('epc_docx_run_reminders')) {
    /**
     * Dispatch all due reminders across the register (or a single document when
     * $onlyDocId > 0). Idempotent: each (document, threshold) is logged so a
     * second run on the same day sends nothing new. Suitable for a daily cron.
     *
     * @return array{checked:int,sent:int,skipped:int,details:array<int,array<string,mixed>>}
     */
    function epc_docx_run_reminders(PDO $db, int $companyId = 0, int $now = 0, int $onlyDocId = 0): array
    {
        epc_docx_ensure_schema($db);
        if ($now <= 0) {
            $now = time();
        }
        $rows = epc_docx_list($db, $companyId, '', $now);
        $checked = 0;
        $sent = 0;
        $skipped = 0;
        $details = array();
        foreach ($rows as $row) {
            $docId = (int) $row['id'];
            if ($onlyDocId > 0 && $docId !== $onlyDocId) {
                continue;
            }
            $checked++;
            $expiry = (int) $row['expiry_date'];
            if ($expiry <= 0) {
                $skipped++;
                continue;
            }
            $reminderDays = epc_docx_parse_reminder_days((string) $row['reminder_days']);
            $already = epc_docx_reminders_sent($db, $docId);
            $due = epc_docx_due_thresholds($expiry, $reminderDays, $now, $already);
            if (!$due) {
                $skipped++;
                continue;
            }
            $recipient = epc_docx_reminder_recipient($row);
            $daysLeft = epc_docx_days_left($expiry, $now);
            $urgent = $due[0];
            $when = $daysLeft < 0
                ? ('EXPIRED ' . abs($daysLeft) . ' day(s) ago')
                : ('expires in ' . $daysLeft . ' day(s)');
            $subject = '[Document expiry] ' . (string) ($row['title'] !== '' ? $row['title'] : $row['doc_type']) . ' — ' . $when;
            $body = "Document expiry reminder\n\n"
                . 'Document : ' . (string) $row['title'] . "\n"
                . 'Type     : ' . (string) $row['doc_type'] . "\n"
                . 'Category : ' . (string) $row['category'] . "\n"
                . 'Ref no   : ' . (string) $row['ref_no'] . "\n"
                . 'Owner    : ' . (string) $row['owner'] . "\n"
                . 'Issuer   : ' . (string) $row['issuer'] . "\n"
                . 'Expiry   : ' . ($expiry > 0 ? date('Y-m-d', $expiry) : '—') . "\n"
                . 'Status   : ' . strtoupper($when) . "\n\n"
                . "Please action the renewal before the expiry date.\n";
            $ok = epc_docx_send_email($recipient, $subject, $body);
            if ($ok) {
                $ins = $db->prepare("INSERT IGNORE INTO `epc_erp_doc_expiry_reminders` (`doc_id`,`threshold_days`,`days_left`,`recipient`,`channel`,`sent_at`) VALUES (?,?,?,?, 'email', ?)");
                foreach ($due as $d) {
                    $ins->execute(array($docId, $d, $daysLeft, $recipient, $now));
                }
                $sent++;
                $details[] = array('doc_id' => $docId, 'recipient' => $recipient, 'threshold' => $urgent, 'days_left' => $daysLeft, 'covered' => $due);
            } else {
                $skipped++;
            }
        }
        return array('checked' => $checked, 'sent' => $sent, 'skipped' => $skipped, 'details' => $details);
    }
}
