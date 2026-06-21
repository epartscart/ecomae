<?php
/**
 * Procurement & sourcing depth — purchase requisitions, procurement categories
 * and procurement policies.
 *
 * Adds the front of the D365 procure-to-pay flow that was missing:
 *   requisition (draft) -> submit -> policy-driven approval -> convert to PO.
 * Plus a procurement category tree and per-category spending policies that
 * decide when a requisition needs approval and which vendor is preferred.
 *
 * Additive: new epc_proc_* tables. Multi-company aware (company_id scoping).
 * Company-configurable (policies are per company, no hard-coded thresholds).
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_proc_ensure_schema')) {
    function epc_proc_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_proc_category` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `code` varchar(40) NOT NULL DEFAULT '',
            `name` varchar(160) NOT NULL DEFAULT '',
            `parent_id` int(11) NOT NULL DEFAULT 0,
            `default_account` varchar(40) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_parent` (`parent_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Procurement categories'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_proc_policy` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `category_id` int(11) NOT NULL DEFAULT 0,
            `approval_threshold` decimal(18,2) NOT NULL DEFAULT 0.00,
            `preferred_vendor` varchar(160) NOT NULL DEFAULT '',
            `active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_category` (`category_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Procurement policies'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_proc_req` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `req_number` varchar(40) NOT NULL DEFAULT '',
            `requester` varchar(160) NOT NULL DEFAULT '',
            `business_unit_id` int(11) NOT NULL DEFAULT 0,
            `status` varchar(20) NOT NULL DEFAULT 'draft',
            `justification` text,
            `total` decimal(18,2) NOT NULL DEFAULT 0.00,
            `requires_approval` tinyint(1) NOT NULL DEFAULT 0,
            `decided_by` varchar(160) NOT NULL DEFAULT '',
            `decision_note` varchar(255) NOT NULL DEFAULT '',
            `po_ref` varchar(40) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Purchase requisitions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_proc_req_line` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `req_id` int(11) NOT NULL DEFAULT 0,
            `category_id` int(11) NOT NULL DEFAULT 0,
            `item_code` varchar(80) NOT NULL DEFAULT '',
            `description` varchar(255) NOT NULL DEFAULT '',
            `qty` decimal(18,4) NOT NULL DEFAULT 0.0000,
            `unit_price` decimal(18,2) NOT NULL DEFAULT 0.00,
            `line_total` decimal(18,2) NOT NULL DEFAULT 0.00,
            `preferred_vendor` varchar(160) NOT NULL DEFAULT '',
            PRIMARY KEY (`id`),
            KEY `x_req` (`req_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Purchase requisition lines'");
    }
}

/* ---------------- categories ---------------- */

if (!function_exists('epc_proc_category_save')) {
    /** @param array<string,mixed> $data company_id, code, name, parent_id, default_account, active */
    function epc_proc_category_save(PDO $db, array $data, int $id = 0): int
    {
        epc_proc_ensure_schema($db);
        $code = trim((string) ($data['code'] ?? ''));
        $name = trim((string) ($data['name'] ?? ''));
        if ($code === '' || $name === '') {
            throw new Exception('Category code and name are required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_proc_category` SET `code`=?, `name`=?, `parent_id`=?, `default_account`=?, `active`=? WHERE `id`=?")
               ->execute(array($code, $name, (int) ($data['parent_id'] ?? 0), (string) ($data['default_account'] ?? ''), (int) (!empty($data['active']) ? 1 : 0), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_proc_category` (`company_id`,`code`,`name`,`parent_id`,`default_account`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $code, $name, (int) ($data['parent_id'] ?? 0), (string) ($data['default_account'] ?? ''), (int) (isset($data['active']) ? (!empty($data['active']) ? 1 : 0) : 1), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_proc_categories')) {
    /** @return array<int,array<string,mixed>> */
    function epc_proc_categories(PDO $db, int $companyId, bool $activeOnly = false): array
    {
        epc_proc_ensure_schema($db);
        $sql = "SELECT * FROM `epc_proc_category` WHERE `company_id`=?";
        if ($activeOnly) {
            $sql .= " AND `active`=1";
        }
        $sql .= " ORDER BY `code` ASC";
        $st = $db->prepare($sql);
        $st->execute(array($companyId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_proc_category_get')) {
    /** @return array<string,mixed>|null */
    function epc_proc_category_get(PDO $db, int $id): ?array
    {
        epc_proc_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_proc_category` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

/* ---------------- policies ---------------- */

if (!function_exists('epc_proc_policy_save')) {
    /** @param array<string,mixed> $data company_id, name, category_id, approval_threshold, preferred_vendor, active */
    function epc_proc_policy_save(PDO $db, array $data, int $id = 0): int
    {
        epc_proc_ensure_schema($db);
        $name = trim((string) ($data['name'] ?? ''));
        if ($name === '') {
            throw new Exception('Policy name is required');
        }
        if ($id > 0) {
            $db->prepare("UPDATE `epc_proc_policy` SET `name`=?, `category_id`=?, `approval_threshold`=?, `preferred_vendor`=?, `active`=? WHERE `id`=?")
               ->execute(array($name, (int) ($data['category_id'] ?? 0), (float) ($data['approval_threshold'] ?? 0), (string) ($data['preferred_vendor'] ?? ''), (int) (!empty($data['active']) ? 1 : 0), $id));
            return $id;
        }
        $db->prepare("INSERT INTO `epc_proc_policy` (`company_id`,`name`,`category_id`,`approval_threshold`,`preferred_vendor`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)")
           ->execute(array((int) ($data['company_id'] ?? 0), $name, (int) ($data['category_id'] ?? 0), (float) ($data['approval_threshold'] ?? 0), (string) ($data['preferred_vendor'] ?? ''), (int) (isset($data['active']) ? (!empty($data['active']) ? 1 : 0) : 1), time()));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_proc_policies')) {
    /** @return array<int,array<string,mixed>> */
    function epc_proc_policies(PDO $db, int $companyId, bool $activeOnly = false): array
    {
        epc_proc_ensure_schema($db);
        $sql = "SELECT * FROM `epc_proc_policy` WHERE `company_id`=?";
        if ($activeOnly) {
            $sql .= " AND `active`=1";
        }
        $sql .= " ORDER BY `name` ASC";
        $st = $db->prepare($sql);
        $st->execute(array($companyId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_proc_policy_for_category')) {
    /** Resolve the active policy that applies to a category (exact match, else company default with category 0). */
    function epc_proc_policy_for_category(PDO $db, int $companyId, int $categoryId): ?array
    {
        epc_proc_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_proc_policy` WHERE `company_id`=? AND `active`=1 AND `category_id`=? ORDER BY `id` DESC LIMIT 1");
        $st->execute(array($companyId, $categoryId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        if ($row) {
            return $row;
        }
        $st = $db->prepare("SELECT * FROM `epc_proc_policy` WHERE `company_id`=? AND `active`=1 AND `category_id`=0 ORDER BY `id` DESC LIMIT 1");
        $st->execute(array($companyId));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_proc_requires_approval')) {
    /** Decide if a requisition amount on a category needs approval given company policy. */
    function epc_proc_requires_approval(PDO $db, int $companyId, int $categoryId, float $amount): bool
    {
        $policy = epc_proc_policy_for_category($db, $companyId, $categoryId);
        if (!$policy) {
            return $amount > 0; // no policy configured -> be safe, require approval for any spend
        }
        $threshold = (float) $policy['approval_threshold'];
        if ($threshold <= 0) {
            return false; // threshold 0 means auto-approve under this policy
        }
        return $amount > $threshold;
    }
}

/* ---------------- requisitions ---------------- */

if (!function_exists('epc_proc_req_statuses')) {
    /** @return array<int,string> */
    function epc_proc_req_statuses(): array
    {
        return array('draft', 'submitted', 'approved', 'rejected', 'converted');
    }
}

if (!function_exists('epc_proc_req_next_number')) {
    function epc_proc_req_next_number(PDO $db, int $companyId): string
    {
        epc_proc_ensure_schema($db);
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_proc_req` WHERE `company_id`=?");
        $st->execute(array($companyId));
        $n = (int) $st->fetchColumn() + 1;
        return 'PR-' . str_pad((string) $n, 5, '0', STR_PAD_LEFT);
    }
}

if (!function_exists('epc_proc_req_save')) {
    /** Create/update a requisition header (status stays draft on create). */
    function epc_proc_req_save(PDO $db, array $data, int $id = 0): int
    {
        epc_proc_ensure_schema($db);
        $requester = trim((string) ($data['requester'] ?? ''));
        if ($requester === '') {
            throw new Exception('Requester is required');
        }
        $now = time();
        if ($id > 0) {
            $db->prepare("UPDATE `epc_proc_req` SET `requester`=?, `business_unit_id`=?, `justification`=?, `time_updated`=? WHERE `id`=?")
               ->execute(array($requester, (int) ($data['business_unit_id'] ?? 0), (string) ($data['justification'] ?? ''), $now, $id));
            return $id;
        }
        $companyId = (int) ($data['company_id'] ?? 0);
        $num = trim((string) ($data['req_number'] ?? ''));
        if ($num === '') {
            $num = epc_proc_req_next_number($db, $companyId);
        }
        $db->prepare("INSERT INTO `epc_proc_req` (`company_id`,`req_number`,`requester`,`business_unit_id`,`status`,`justification`,`total`,`time_created`,`time_updated`) VALUES (?,?,?,?,'draft',?,0,?,?)")
           ->execute(array($companyId, $num, $requester, (int) ($data['business_unit_id'] ?? 0), (string) ($data['justification'] ?? ''), $now, $now));
        return (int) $db->lastInsertId();
    }
}

if (!function_exists('epc_proc_req_get')) {
    /** @return array<string,mixed>|null */
    function epc_proc_req_get(PDO $db, int $id): ?array
    {
        epc_proc_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_proc_req` WHERE `id`=?");
        $st->execute(array($id));
        $row = $st->fetch(PDO::FETCH_ASSOC);
        return $row ?: null;
    }
}

if (!function_exists('epc_proc_reqs')) {
    /** @return array<int,array<string,mixed>> */
    function epc_proc_reqs(PDO $db, int $companyId, string $status = ''): array
    {
        epc_proc_ensure_schema($db);
        $sql = "SELECT * FROM `epc_proc_req` WHERE `company_id`=?";
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

if (!function_exists('epc_proc_req_add_line')) {
    /** @param array<string,mixed> $data category_id, item_code, description, qty, unit_price, preferred_vendor */
    function epc_proc_req_add_line(PDO $db, int $reqId, array $data): int
    {
        epc_proc_ensure_schema($db);
        $req = epc_proc_req_get($db, $reqId);
        if (!$req) {
            throw new Exception('Requisition not found');
        }
        if ($req['status'] !== 'draft') {
            throw new Exception('Lines can only be added while the requisition is a draft');
        }
        $qty = (float) ($data['qty'] ?? 0);
        $price = (float) ($data['unit_price'] ?? 0);
        $lineTotal = round($qty * $price, 2);
        $pref = trim((string) ($data['preferred_vendor'] ?? ''));
        if ($pref === '') {
            $policy = epc_proc_policy_for_category($db, (int) $req['company_id'], (int) ($data['category_id'] ?? 0));
            if ($policy) {
                $pref = (string) $policy['preferred_vendor'];
            }
        }
        $db->prepare("INSERT INTO `epc_proc_req_line` (`req_id`,`category_id`,`item_code`,`description`,`qty`,`unit_price`,`line_total`,`preferred_vendor`) VALUES (?,?,?,?,?,?,?,?)")
           ->execute(array($reqId, (int) ($data['category_id'] ?? 0), (string) ($data['item_code'] ?? ''), (string) ($data['description'] ?? ''), $qty, $price, $lineTotal, $pref));
        $lineId = (int) $db->lastInsertId();
        epc_proc_req_recalc($db, $reqId);
        return $lineId;
    }
}

if (!function_exists('epc_proc_req_lines')) {
    /** @return array<int,array<string,mixed>> */
    function epc_proc_req_lines(PDO $db, int $reqId): array
    {
        epc_proc_ensure_schema($db);
        $st = $db->prepare("SELECT * FROM `epc_proc_req_line` WHERE `req_id`=? ORDER BY `id` ASC");
        $st->execute(array($reqId));
        return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    }
}

if (!function_exists('epc_proc_req_recalc')) {
    /** Recompute the requisition total and whether it needs approval per policy. */
    function epc_proc_req_recalc(PDO $db, int $reqId): float
    {
        epc_proc_ensure_schema($db);
        $st = $db->prepare("SELECT COALESCE(SUM(`line_total`),0) FROM `epc_proc_req_line` WHERE `req_id`=?");
        $st->execute(array($reqId));
        $total = (float) $st->fetchColumn();
        $req = epc_proc_req_get($db, $reqId);
        $needs = 0;
        if ($req) {
            // Use the dominant category (first line's category) for policy resolution.
            $lines = epc_proc_req_lines($db, $reqId);
            $catId = isset($lines[0]['category_id']) ? (int) $lines[0]['category_id'] : 0;
            $needs = epc_proc_requires_approval($db, (int) $req['company_id'], $catId, $total) ? 1 : 0;
        }
        $db->prepare("UPDATE `epc_proc_req` SET `total`=?, `requires_approval`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($total, $needs, time(), $reqId));
        return $total;
    }
}

if (!function_exists('epc_proc_req_submit')) {
    /** Move a draft requisition to submitted (or straight to approved if no approval needed). */
    function epc_proc_req_submit(PDO $db, int $reqId): string
    {
        epc_proc_ensure_schema($db);
        $req = epc_proc_req_get($db, $reqId);
        if (!$req) {
            throw new Exception('Requisition not found');
        }
        if ($req['status'] !== 'draft') {
            throw new Exception('Only a draft requisition can be submitted');
        }
        if (count(epc_proc_req_lines($db, $reqId)) === 0) {
            throw new Exception('Add at least one line before submitting');
        }
        epc_proc_req_recalc($db, $reqId);
        $req = epc_proc_req_get($db, $reqId);
        $newStatus = ((int) $req['requires_approval'] === 1) ? 'submitted' : 'approved';
        $db->prepare("UPDATE `epc_proc_req` SET `status`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($newStatus, time(), $reqId));
        return $newStatus;
    }
}

if (!function_exists('epc_proc_req_decision')) {
    /** Approve or reject a submitted requisition. */
    function epc_proc_req_decision(PDO $db, int $reqId, bool $approve, string $by = '', string $note = ''): void
    {
        epc_proc_ensure_schema($db);
        $req = epc_proc_req_get($db, $reqId);
        if (!$req) {
            throw new Exception('Requisition not found');
        }
        if ($req['status'] !== 'submitted') {
            throw new Exception('Only a submitted requisition can be approved or rejected');
        }
        $status = $approve ? 'approved' : 'rejected';
        $db->prepare("UPDATE `epc_proc_req` SET `status`=?, `decided_by`=?, `decision_note`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($status, $by, $note, time(), $reqId));
    }
}

if (!function_exists('epc_proc_req_convert')) {
    /** Convert an approved requisition to a purchase order (records a PO ref + status converted). */
    function epc_proc_req_convert(PDO $db, int $reqId): string
    {
        epc_proc_ensure_schema($db);
        $req = epc_proc_req_get($db, $reqId);
        if (!$req) {
            throw new Exception('Requisition not found');
        }
        if ($req['status'] !== 'approved') {
            throw new Exception('Only an approved requisition can be converted to a PO');
        }
        $poRef = 'PO-' . $req['req_number'];
        $db->prepare("UPDATE `epc_proc_req` SET `status`='converted', `po_ref`=?, `time_updated`=? WHERE `id`=?")
           ->execute(array($poRef, time(), $reqId));
        return $poRef;
    }
}

if (!function_exists('epc_proc_summary')) {
    /** @return array<string,mixed> */
    function epc_proc_summary(PDO $db, int $companyId): array
    {
        epc_proc_ensure_schema($db);
        $out = array('draft' => 0, 'submitted' => 0, 'approved' => 0, 'rejected' => 0, 'converted' => 0, 'categories' => 0, 'policies' => 0, 'open_value' => 0.0);
        $st = $db->prepare("SELECT `status`, COUNT(*) c, COALESCE(SUM(`total`),0) v FROM `epc_proc_req` WHERE `company_id`=? GROUP BY `status`");
        $st->execute(array($companyId));
        foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
            $s = (string) $r['status'];
            if (isset($out[$s])) {
                $out[$s] = (int) $r['c'];
            }
            if (in_array($s, array('submitted', 'approved'), true)) {
                $out['open_value'] += (float) $r['v'];
            }
        }
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_proc_category` WHERE `company_id`=?");
        $st->execute(array($companyId));
        $out['categories'] = (int) $st->fetchColumn();
        $st = $db->prepare("SELECT COUNT(*) FROM `epc_proc_policy` WHERE `company_id`=?");
        $st->execute(array($companyId));
        $out['policies'] = (int) $st->fetchColumn();
        return $out;
    }
}
