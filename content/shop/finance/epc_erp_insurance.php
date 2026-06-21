<?php
/**
 * Insurance Management — corporate insurance register built to international
 * (ISO 31000 / broker) best practice. Manages every class of company policy
 * (Marine, Property All-Risk, Business Interruption, Public Liability, Medical,
 * Fidelity Guarantee, Electronic Equipment, Warehouse, Assets All-Risk, Group
 * Personal Accident / GPA, Motor Fleet, Workmen's Compensation, Money, etc.),
 * with a document store, full claims advice & tracking, and per-policy
 * timeframe auto-email reminders.
 *
 * Renewals integrate with the central Document Expiry Tracker: each active
 * policy's expiry is fed into epc_erp_doc_expiry (source_module='insurance')
 * so a single reminder engine emails renewal alerts at the configured lead
 * times — no duplicate scheduling.
 *
 * Country-driven: the suggested statutory / compulsory cover for a tenant is
 * resolved from its REGISTRATION COUNTRY (platform hard rule); generic
 * fallback for unknown jurisdictions. Additive — no posting/GL impact.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_doc_expiry.php';

if (!function_exists('epc_ins_ensure_schema')) {
    function epc_ins_ensure_schema(PDO $db): void
    {
        epc_docx_ensure_schema($db);

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_ins_policies` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `policy_no` varchar(120) NOT NULL DEFAULT '',
            `class` varchar(40) NOT NULL DEFAULT 'other',
            `title` varchar(200) NOT NULL DEFAULT '',
            `insurer` varchar(200) NOT NULL DEFAULT '',
            `broker` varchar(200) NOT NULL DEFAULT '',
            `insured_name` varchar(200) NOT NULL DEFAULT '',
            `sum_insured` decimal(18,2) NOT NULL DEFAULT 0.00,
            `premium` decimal(18,2) NOT NULL DEFAULT 0.00,
            `deductible` decimal(18,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `start_date` int(11) NOT NULL DEFAULT 0,
            `expiry_date` int(11) NOT NULL DEFAULT 0,
            `reminder_days` varchar(120) NOT NULL DEFAULT '90,60,30,7',
            `contact_email` varchar(200) NOT NULL DEFAULT '',
            `status` varchar(16) NOT NULL DEFAULT 'active',
            `note` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_class` (`class`),
            KEY `x_expiry` (`expiry_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Insurance policy register'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_ins_documents` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `policy_id` int(11) NOT NULL,
            `doc_type` varchar(60) NOT NULL DEFAULT 'policy',
            `title` varchar(200) NOT NULL DEFAULT '',
            `file_path` varchar(255) NOT NULL DEFAULT '',
            `note` varchar(255) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_policy` (`policy_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Insurance document store'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_ins_claims` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `policy_id` int(11) NOT NULL,
            `claim_no` varchar(120) NOT NULL DEFAULT '',
            `loss_date` int(11) NOT NULL DEFAULT 0,
            `notified_date` int(11) NOT NULL DEFAULT 0,
            `description` text,
            `claim_amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `settled_amount` decimal(18,2) NOT NULL DEFAULT 0.00,
            `surveyor` varchar(200) NOT NULL DEFAULT '',
            `deadline_date` int(11) NOT NULL DEFAULT 0,
            `status` varchar(20) NOT NULL DEFAULT 'notified',
            `note` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_policy` (`policy_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Insurance claims tracking'");
    }
}

if (!function_exists('epc_ins_classes')) {
    /** @return array<string,string> class key => label (international cover lines) */
    function epc_ins_classes(): array
    {
        return array(
            'marine' => 'Marine Cargo / Hull',
            'property_air' => 'Property All-Risk',
            'business_interruption' => 'Business Interruption',
            'public_liability' => 'Public Liability',
            'product_liability' => 'Product Liability',
            'professional_indemnity' => 'Professional Indemnity',
            'medical' => 'Group Medical / Health',
            'gpa' => 'Group Personal Accident (GPA)',
            'workmen' => "Workmen's Compensation",
            'fidelity' => 'Fidelity Guarantee',
            'electronic_equipment' => 'Electronic Equipment',
            'machinery_breakdown' => 'Machinery Breakdown',
            'warehouse' => 'Warehouse / Stock',
            'assets' => 'Assets All-Risk',
            'motor' => 'Motor / Fleet',
            'money' => 'Money Insurance',
            'cyber' => 'Cyber Liability',
            'directors' => "Directors' & Officers' (D&O)",
            'travel' => 'Corporate Travel',
            'credit' => 'Trade Credit',
            'other' => 'Other',
        );
    }
}

if (!function_exists('epc_ins_class_label')) {
    function epc_ins_class_label(string $class): string
    {
        $c = epc_ins_classes();
        return $c[$class] ?? ucfirst(str_replace('_', ' ', $class));
    }
}

if (!function_exists('epc_ins_claim_statuses')) {
    /** Claim lifecycle: notify → survey → docs → assess → settle/reject/closed. */
    function epc_ins_claim_statuses(): array
    {
        return array(
            'notified' => 'Notified',
            'survey' => 'Survey / inspection',
            'docs' => 'Documents submitted',
            'assessed' => 'Assessed',
            'settled' => 'Settled',
            'rejected' => 'Rejected',
            'closed' => 'Closed',
        );
    }
}

if (!function_exists('epc_ins_country_recommended')) {
    /**
     * Country-driven recommended / compulsory cover for the tenant's
     * jurisdiction. Resolved from registration country with a generic fallback.
     *
     * @return array<int,array{class:string,basis:string}>
     */
    function epc_ins_country_recommended(string $country): array
    {
        $c = strtoupper(trim($country));
        $packs = array(
            'AE' => array(
                array('medical', 'Compulsory employee health cover (DHA/DOH/HAAD)'),
                array('workmen', 'Compulsory workmen compensation'),
                array('motor', 'Compulsory third-party motor'),
                array('property_air', 'Lender / landlord requirement'),
                array('public_liability', 'Best practice'),
            ),
            'SA' => array(
                array('medical', 'Compulsory cooperative health insurance (CCHI)'),
                array('motor', 'Compulsory third-party motor'),
                array('workmen', 'GOSI occupational hazards'),
                array('property_air', 'Best practice'),
            ),
            'GB' => array(
                array('workmen', "Compulsory Employers' Liability"),
                array('motor', 'Compulsory third-party motor'),
                array('public_liability', 'Best practice'),
                array('professional_indemnity', 'Best practice / regulated sectors'),
            ),
            'IN' => array(
                array('workmen', "Employees' Compensation / ESIC"),
                array('motor', 'Compulsory third-party motor'),
                array('medical', 'Best practice (group mediclaim)'),
                array('marine', 'Trade requirement'),
            ),
        );
        if (isset($packs[$c])) {
            $out = array();
            foreach ($packs[$c] as $r) {
                $out[] = array('class' => $r[0], 'basis' => $r[1]);
            }
            return $out;
        }
        return array(
            array('class' => 'public_liability', 'basis' => 'Best practice'),
            array('class' => 'property_air', 'basis' => 'Asset protection'),
            array('class' => 'workmen', 'basis' => 'Employee protection'),
        );
    }
}

if (!function_exists('epc_ins_policy_status')) {
    /** Derive a display status combining stored status + expiry. */
    function epc_ins_policy_status(array $policy, int $now = 0): string
    {
        $stored = (string) ($policy['status'] ?? 'active');
        if ($stored === 'cancelled' || $stored === 'lapsed') {
            return $stored;
        }
        return epc_docx_status((int) ($policy['expiry_date'] ?? 0), $now);
    }
}

if (!function_exists('epc_ins_sync_expiry')) {
    /** Feed (or remove) a policy's renewal expiry into the central tracker. */
    function epc_ins_sync_expiry(PDO $db, int $policyId): void
    {
        $p = epc_ins_get($db, $policyId);
        if (!$p) {
            epc_docx_delete_source($db, 'insurance', $policyId);
            return;
        }
        $active = in_array((string) $p['status'], array('active'), true) && (int) $p['expiry_date'] > 0;
        if (!$active) {
            epc_docx_delete_source($db, 'insurance', $policyId);
            return;
        }
        epc_docx_upsert_source($db, 'insurance', $policyId, array(
            'company_id' => (int) $p['company_id'],
            'category' => 'insurance',
            'doc_type' => epc_ins_class_label((string) $p['class']) . ' policy',
            'title' => (string) ($p['title'] !== '' ? $p['title'] : (epc_ins_class_label((string) $p['class']) . ' — ' . $p['policy_no'])),
            'ref_no' => (string) $p['policy_no'],
            'owner' => (string) $p['insured_name'],
            'owner_email' => (string) $p['contact_email'],
            'issuer' => (string) $p['insurer'],
            'issue_date' => (int) $p['start_date'],
            'expiry_date' => (int) $p['expiry_date'],
            'reminder_days' => (string) $p['reminder_days'],
            'note' => 'Auto-fed from Insurance Management',
            'active' => 1,
        ));
    }
}

if (!function_exists('epc_ins_save')) {
    /**
     * @param array<string,mixed> $data
     */
    function epc_ins_save(PDO $db, array $data, int $id = 0): int
    {
        epc_ins_ensure_schema($db);
        $classes = epc_ins_classes();
        $class = (string) ($data['class'] ?? 'other');
        if (!isset($classes[$class])) {
            $class = 'other';
        }
        $status = (string) ($data['status'] ?? 'active');
        $allowedStatus = array('active', 'cancelled', 'lapsed', 'expired');
        if (!in_array($status, $allowedStatus, true)) {
            $status = 'active';
        }
        $reminder = trim((string) ($data['reminder_days'] ?? '90,60,30,7'));
        $parsed = epc_docx_parse_reminder_days($reminder);
        $reminder = $parsed ? implode(',', $parsed) : '90,60,30,7';
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_erp_ins_policies` SET `policy_no`=?, `class`=?, `title`=?, `insurer`=?, `broker`=?, `insured_name`=?, `sum_insured`=?, `premium`=?, `deductible`=?, `currency`=?, `start_date`=?, `expiry_date`=?, `reminder_days`=?, `contact_email`=?, `status`=?, `note`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array(
                   (string) ($data['policy_no'] ?? ''), $class, (string) ($data['title'] ?? ''),
                   (string) ($data['insurer'] ?? ''), (string) ($data['broker'] ?? ''), (string) ($data['insured_name'] ?? ''),
                   (float) ($data['sum_insured'] ?? 0), (float) ($data['premium'] ?? 0), (float) ($data['deductible'] ?? 0),
                   strtoupper(substr((string) ($data['currency'] ?? 'AED'), 0, 3)), (int) ($data['start_date'] ?? 0),
                   (int) ($data['expiry_date'] ?? 0), $reminder, (string) ($data['contact_email'] ?? ''),
                   $status, (string) ($data['note'] ?? ''), $now, $id,
               ));
            epc_ins_sync_expiry($db, $id);
            return $id;
        }
        $db->prepare("INSERT INTO `epc_erp_ins_policies` (`company_id`,`policy_no`,`class`,`title`,`insurer`,`broker`,`insured_name`,`sum_insured`,`premium`,`deductible`,`currency`,`start_date`,`expiry_date`,`reminder_days`,`contact_email`,`status`,`note`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)")
           ->execute(array(
               (int) ($data['company_id'] ?? 0), (string) ($data['policy_no'] ?? ''), $class, (string) ($data['title'] ?? ''),
               (string) ($data['insurer'] ?? ''), (string) ($data['broker'] ?? ''), (string) ($data['insured_name'] ?? ''),
               (float) ($data['sum_insured'] ?? 0), (float) ($data['premium'] ?? 0), (float) ($data['deductible'] ?? 0),
               strtoupper(substr((string) ($data['currency'] ?? 'AED'), 0, 3)), (int) ($data['start_date'] ?? 0),
               (int) ($data['expiry_date'] ?? 0), $reminder, (string) ($data['contact_email'] ?? ''),
               $status, (string) ($data['note'] ?? ''), $now, $now,
           ));
        $id = (int) $db->lastInsertId();
        epc_ins_sync_expiry($db, $id);
        return $id;
    }
}

if (!function_exists('epc_ins_get')) {
    /** @return array<string,mixed>|null */
    function epc_ins_get(PDO $db, int $id): ?array
    {
        epc_ins_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_ins_policies` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_ins_delete')) {
    function epc_ins_delete(PDO $db, int $id): void
    {
        epc_ins_ensure_schema($db);
        epc_docx_delete_source($db, 'insurance', $id);
        $db->prepare("DELETE FROM `epc_erp_ins_documents` WHERE `policy_id`=?")->execute(array($id));
        $db->prepare("DELETE FROM `epc_erp_ins_claims` WHERE `policy_id`=?")->execute(array($id));
        $db->prepare("DELETE FROM `epc_erp_ins_policies` WHERE `id`=?")->execute(array($id));
    }
}

if (!function_exists('epc_ins_list')) {
    /**
     * @return array<int,array<string,mixed>>
     */
    function epc_ins_list(PDO $db, int $companyId = 0, string $class = '', int $now = 0): array
    {
        epc_ins_ensure_schema($db);
        if ($now <= 0) {
            $now = time();
        }
        $sql = "SELECT * FROM `epc_erp_ins_policies` WHERE 1=1";
        $args = array();
        if ($companyId > 0) {
            $sql .= " AND `company_id`=?";
            $args[] = $companyId;
        }
        if ($class !== '') {
            $sql .= " AND `class`=?";
            $args[] = $class;
        }
        $sql .= " ORDER BY (`expiry_date`=0), `expiry_date` ASC, `id` DESC";
        $st = $db->prepare($sql);
        $st->execute($args);
        $rows = $st->fetchAll(PDO::FETCH_ASSOC);
        foreach ($rows as &$r) {
            $r['days_left'] = epc_docx_days_left((int) $r['expiry_date'], $now);
            $r['eff_status'] = epc_ins_policy_status($r, $now);
            $r['class_label'] = epc_ins_class_label((string) $r['class']);
            $r['open_claims'] = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_ins_claims` WHERE `policy_id`=" . (int) $r['id'] . " AND `status` NOT IN ('settled','rejected','closed')")->fetchColumn();
        }
        unset($r);
        return $rows;
    }
}

if (!function_exists('epc_ins_summary')) {
    /**
     * @return array{policies:int,active:int,expiring:int,expired:int,sum_insured:float,annual_premium:float,open_claims:int}
     */
    function epc_ins_summary(PDO $db, int $companyId = 0, int $now = 0): array
    {
        $rows = epc_ins_list($db, $companyId, '', $now);
        $out = array('policies' => 0, 'active' => 0, 'expiring' => 0, 'expired' => 0, 'sum_insured' => 0.0, 'annual_premium' => 0.0, 'open_claims' => 0);
        foreach ($rows as $r) {
            $out['policies']++;
            $s = (string) $r['eff_status'];
            if ($s === 'valid' || $s === 'expiring') {
                $out['active']++;
            }
            if ($s === 'expiring') {
                $out['expiring']++;
            }
            if ($s === 'expired') {
                $out['expired']++;
            }
            if ((string) $r['status'] === 'active') {
                $out['sum_insured'] += (float) $r['sum_insured'];
                $out['annual_premium'] += (float) $r['premium'];
            }
            $out['open_claims'] += (int) $r['open_claims'];
        }
        $out['sum_insured'] = round($out['sum_insured'], 2);
        $out['annual_premium'] = round($out['annual_premium'], 2);
        return $out;
    }
}

/* ---------- documents ---------- */

if (!function_exists('epc_ins_doc_add')) {
    /** @param array<string,mixed> $data */
    function epc_ins_doc_add(PDO $db, int $policyId, array $data): int
    {
        epc_ins_ensure_schema($db);
        $db->prepare("INSERT INTO `epc_erp_ins_documents` (`policy_id`,`doc_type`,`title`,`file_path`,`note`,`time_created`) VALUES (?,?,?,?,?,?)")
           ->execute(array(
               $policyId, (string) ($data['doc_type'] ?? 'policy'), (string) ($data['title'] ?? ''),
               (string) ($data['file_path'] ?? ''), (string) ($data['note'] ?? ''), time(),
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_ins_docs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_ins_docs(PDO $db, int $policyId): array
    {
        epc_ins_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_erp_ins_documents` WHERE `policy_id`=? ORDER BY `id` DESC");
        $st->execute(array($policyId));
        return $st->fetchAll(PDO::FETCH_ASSOC);
    }
}

if (!function_exists('epc_ins_doc_delete')) {
    function epc_ins_doc_delete(PDO $db, int $id): void
    {
        epc_ins_ensure_schema($db);
        $db->prepare("DELETE FROM `epc_erp_ins_documents` WHERE `id`=?")->execute(array($id));
    }
}

/* ---------- claims ---------- */

if (!function_exists('epc_ins_claim_save')) {
    /** @param array<string,mixed> $data */
    function epc_ins_claim_save(PDO $db, array $data, int $id = 0): int
    {
        epc_ins_ensure_schema($db);
        $status = (string) ($data['status'] ?? 'notified');
        if (!isset(epc_ins_claim_statuses()[$status])) {
            $status = 'notified';
        }
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_erp_ins_claims` SET `claim_no`=?, `loss_date`=?, `notified_date`=?, `description`=?, `claim_amount`=?, `settled_amount`=?, `surveyor`=?, `deadline_date`=?, `status`=?, `note`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array(
                   (string) ($data['claim_no'] ?? ''), (int) ($data['loss_date'] ?? 0), (int) ($data['notified_date'] ?? 0),
                   (string) ($data['description'] ?? ''), (float) ($data['claim_amount'] ?? 0), (float) ($data['settled_amount'] ?? 0),
                   (string) ($data['surveyor'] ?? ''), (int) ($data['deadline_date'] ?? 0), $status, (string) ($data['note'] ?? ''), $now, $id,
               ));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_erp_ins_claims` (`policy_id`,`claim_no`,`loss_date`,`notified_date`,`description`,`claim_amount`,`settled_amount`,`surveyor`,`deadline_date`,`status`,`note`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)")
           ->execute(array(
               (int) ($data['policy_id'] ?? 0), (string) ($data['claim_no'] ?? ''), (int) ($data['loss_date'] ?? 0),
               (int) ($data['notified_date'] ?? 0), (string) ($data['description'] ?? ''), (float) ($data['claim_amount'] ?? 0),
               (float) ($data['settled_amount'] ?? 0), (string) ($data['surveyor'] ?? ''), (int) ($data['deadline_date'] ?? 0),
               $status, (string) ($data['note'] ?? ''), $now, $now,
           ));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_ins_claim_set_status')) {
    function epc_ins_claim_set_status(PDO $db, int $id, string $status): void
    {
        epc_ins_ensure_schema($db);
        if (!isset(epc_ins_claim_statuses()[$status])) {
            throw new Exception('Invalid claim status');
        }
        $db->prepare("UPDATE `epc_erp_ins_claims` SET `status`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($status, time(), $id));
    }
}

if (!function_exists('epc_ins_claims')) {
    /** @return array<int,array<string,mixed>> */
    function epc_ins_claims(PDO $db, int $policyId = 0): array
    {
        epc_ins_ensure_schema($db);
        if ($policyId > 0) {
            $st = $db->prepare("SELECT * FROM `epc_erp_ins_claims` WHERE `policy_id`=? ORDER BY `id` DESC");
            $st->execute(array($policyId));
            return $st->fetchAll(PDO::FETCH_ASSOC);
        }
        return $db->query("SELECT * FROM `epc_erp_ins_claims` ORDER BY `id` DESC LIMIT 500")->fetchAll(PDO::FETCH_ASSOC);
    }
}
