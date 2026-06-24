<?php
/**
 * P2 #26 — WPS / Payroll Integration
 *
 * UAE Wage Protection System file generation, payroll processing,
 * employee salary management, and SIF file export.
 * Driven by tenant registration country via epc_country_profile().
 * Schema: epc_payroll_employees, epc_payroll_runs, epc_payroll_items
 */

if (!defined('EPC_WPS_PAYROLL_VERSION')) {
    define('EPC_WPS_PAYROLL_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_payroll_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_payroll_employees` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `employee_id`     VARCHAR(32)    NOT NULL,
            `full_name`       VARCHAR(128)   NOT NULL,
            `labour_card_no`  VARCHAR(32)    NOT NULL DEFAULT '',
            `mol_id`          VARCHAR(32)    NOT NULL DEFAULT '',
            `bank_code`       VARCHAR(16)    NOT NULL DEFAULT '',
            `iban`            VARCHAR(34)    NOT NULL DEFAULT '',
            `bank_name`       VARCHAR(64)    NOT NULL DEFAULT '',
            `basic_salary`    DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `housing`         DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `transport`       DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `other_allowance` DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `currency`        CHAR(3)        NOT NULL DEFAULT 'AED',
            `department`      VARCHAR(64)    NOT NULL DEFAULT '',
            `designation`     VARCHAR(64)    NOT NULL DEFAULT '',
            `join_date`       DATE           NULL,
            `status`          ENUM('active','on_leave','terminated','suspended') NOT NULL DEFAULT 'active',
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_emp` (`site_key`, `employee_id`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_payroll_runs` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `run_month`       CHAR(7)        NOT NULL,
            `status`          ENUM('draft','approved','processed','paid','cancelled') NOT NULL DEFAULT 'draft',
            `total_gross`     DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `total_deductions`DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `total_net`       DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
            `employee_count`  INT UNSIGNED   NOT NULL DEFAULT 0,
            `sif_generated`   TINYINT(1)     NOT NULL DEFAULT 0,
            `sif_filename`    VARCHAR(128)   NOT NULL DEFAULT '',
            `approved_by`     INT UNSIGNED   NOT NULL DEFAULT 0,
            `approved_at`     DATETIME       NULL,
            `created_by`      INT UNSIGNED   NOT NULL DEFAULT 0,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_run` (`site_key`, `run_month`),
            INDEX `idx_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_payroll_items` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `run_id`          INT UNSIGNED   NOT NULL,
            `employee_id`     INT UNSIGNED   NOT NULL,
            `basic_salary`    DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `housing`         DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `transport`       DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `other_allowance` DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `overtime`        DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `deductions`      DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `gross_salary`    DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `net_salary`      DECIMAL(12,2)  NOT NULL DEFAULT 0.00,
            `days_worked`     TINYINT UNSIGNED NOT NULL DEFAULT 30,
            `leave_days`      TINYINT UNSIGNED NOT NULL DEFAULT 0,
            INDEX `idx_run` (`run_id`),
            INDEX `idx_emp` (`employee_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── employee CRUD ─── */

function epc_payroll_employee_add(PDO $pdo, string $siteKey, array $data): array
{
    epc_payroll_ensure_schema($pdo);

    $st = $pdo->prepare("
        INSERT INTO `epc_payroll_employees`
            (`site_key`, `employee_id`, `full_name`, `labour_card_no`, `mol_id`,
             `bank_code`, `iban`, `bank_name`, `basic_salary`, `housing`, `transport`,
             `other_allowance`, `currency`, `department`, `designation`, `join_date`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (string) ($data['employee_id'] ?? ''),
        (string) ($data['full_name'] ?? ''),
        (string) ($data['labour_card_no'] ?? ''),
        (string) ($data['mol_id'] ?? ''),
        (string) ($data['bank_code'] ?? ''),
        (string) ($data['iban'] ?? ''),
        (string) ($data['bank_name'] ?? ''),
        (float) ($data['basic_salary'] ?? 0),
        (float) ($data['housing'] ?? 0),
        (float) ($data['transport'] ?? 0),
        (float) ($data['other_allowance'] ?? 0),
        (string) ($data['currency'] ?? 'AED'),
        (string) ($data['department'] ?? ''),
        (string) ($data['designation'] ?? ''),
        (string) ($data['join_date'] ?? date('Y-m-d')),
    ));

    return array('ok' => true, 'id' => (int) $pdo->lastInsertId());
}

function epc_payroll_employee_list(PDO $pdo, string $siteKey, string $status = ''): array
{
    epc_payroll_ensure_schema($pdo);

    $where = array('`site_key` = ?');
    $params = array($siteKey);
    if ($status !== '') {
        $where[] = '`status` = ?';
        $params[] = $status;
    }

    $st = $pdo->prepare("SELECT * FROM `epc_payroll_employees` WHERE " . implode(' AND ', $where) . " ORDER BY `full_name`");
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── payroll run ─── */

function epc_payroll_create_run(PDO $pdo, string $siteKey, string $month, int $userId = 0): array
{
    epc_payroll_ensure_schema($pdo);

    $employees = epc_payroll_employee_list($pdo, $siteKey, 'active');
    if (empty($employees)) {
        return array('ok' => false, 'error' => 'No active employees');
    }

    $st = $pdo->prepare("
        INSERT INTO `epc_payroll_runs` (`site_key`, `run_month`, `created_by`)
        VALUES (?, ?, ?)
        ON DUPLICATE KEY UPDATE `status` = 'draft'
    ");
    $st->execute(array($siteKey, $month, $userId));
    $runId = (int) $pdo->lastInsertId();

    if ($runId === 0) {
        $st = $pdo->prepare("SELECT `id` FROM `epc_payroll_runs` WHERE `site_key` = ? AND `run_month` = ?");
        $st->execute(array($siteKey, $month));
        $runId = (int) $st->fetchColumn();
    }

    $totalGross = 0;
    $totalDeductions = 0;
    $totalNet = 0;

    foreach ($employees as $emp) {
        $gross = (float) $emp['basic_salary'] + (float) $emp['housing'] + (float) $emp['transport'] + (float) $emp['other_allowance'];
        $deductions = 0;
        $net = $gross - $deductions;

        $pdo->prepare("
            INSERT INTO `epc_payroll_items`
                (`run_id`, `employee_id`, `basic_salary`, `housing`, `transport`, `other_allowance`, `gross_salary`, `deductions`, `net_salary`)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        ")->execute(array($runId, $emp['id'], $emp['basic_salary'], $emp['housing'], $emp['transport'], $emp['other_allowance'], $gross, $deductions, $net));

        $totalGross += $gross;
        $totalDeductions += $deductions;
        $totalNet += $net;
    }

    $pdo->prepare("
        UPDATE `epc_payroll_runs`
        SET `total_gross` = ?, `total_deductions` = ?, `total_net` = ?, `employee_count` = ?
        WHERE `id` = ?
    ")->execute(array($totalGross, $totalDeductions, $totalNet, count($employees), $runId));

    return array('ok' => true, 'run_id' => $runId, 'employees' => count($employees), 'total_net' => $totalNet);
}

function epc_payroll_approve_run(PDO $pdo, int $runId, int $approverId): array
{
    $st = $pdo->prepare("UPDATE `epc_payroll_runs` SET `status` = 'approved', `approved_by` = ?, `approved_at` = NOW() WHERE `id` = ? AND `status` = 'draft'");
    $st->execute(array($approverId, $runId));
    return array('ok' => $st->rowCount() > 0);
}

function epc_payroll_run_details(PDO $pdo, int $runId): array
{
    $st = $pdo->prepare("SELECT * FROM `epc_payroll_runs` WHERE `id` = ?");
    $st->execute(array($runId));
    $run = $st->fetch(PDO::FETCH_ASSOC);
    if (!$run) {
        return array();
    }

    $st = $pdo->prepare("
        SELECT i.*, e.`full_name`, e.`employee_id` AS `emp_code`, e.`iban`, e.`bank_code`, e.`labour_card_no`
        FROM `epc_payroll_items` i
        JOIN `epc_payroll_employees` e ON i.`employee_id` = e.`id`
        WHERE i.`run_id` = ?
        ORDER BY e.`full_name`
    ");
    $st->execute(array($runId));
    $run['items'] = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    return $run;
}

/* ─── WPS SIF file generation (UAE) ─── */

function epc_payroll_generate_sif(PDO $pdo, int $runId, string $employerMolId, string $employerBankCode): array
{
    $run = epc_payroll_run_details($pdo, $runId);
    if (empty($run)) {
        return array('ok' => false, 'error' => 'Payroll run not found');
    }

    $lines = array();

    $lines[] = implode(',', array(
        'EDR',
        $employerMolId,
        $employerBankCode,
        str_replace('-', '', $run['run_month']),
        count($run['items']),
        number_format((float) $run['total_net'], 2, '.', ''),
        'AED',
    ));

    foreach ($run['items'] as $item) {
        $lines[] = implode(',', array(
            'EDR',
            $item['labour_card_no'] ?? '',
            $item['bank_code'] ?? '',
            $item['iban'] ?? '',
            date('Ymd'),
            number_format((float) $item['basic_salary'], 2, '.', ''),
            number_format((float) $item['housing'], 2, '.', ''),
            number_format((float) $item['transport'], 2, '.', ''),
            number_format((float) $item['other_allowance'], 2, '.', ''),
            number_format((float) $item['net_salary'], 2, '.', ''),
            $item['leave_days'] ?? 0,
        ));
    }

    $sifContent = implode("\r\n", $lines);
    $filename = 'WPS_SIF_' . $run['run_month'] . '_' . date('Ymd_His') . '.csv';

    $pdo->prepare("UPDATE `epc_payroll_runs` SET `sif_generated` = 1, `sif_filename` = ? WHERE `id` = ?")->execute(array($filename, $runId));

    return array('ok' => true, 'filename' => $filename, 'content' => $sifContent, 'records' => count($run['items']));
}

/* ─── payroll history ─── */

function epc_payroll_run_list(PDO $pdo, string $siteKey): array
{
    epc_payroll_ensure_schema($pdo);

    $st = $pdo->prepare("SELECT * FROM `epc_payroll_runs` WHERE `site_key` = ? ORDER BY `run_month` DESC");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── fleet stats (BOS) ─── */

function epc_payroll_fleet_stats(PDO $pdo): array
{
    epc_payroll_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT e.`site_key`,
               COUNT(DISTINCT e.`id`) AS `employees`,
               SUM(CASE WHEN e.`status` = 'active' THEN 1 ELSE 0 END) AS `active`,
               (SELECT COUNT(*) FROM `epc_payroll_runs` r WHERE r.`site_key` = e.`site_key`) AS `runs`,
               (SELECT SUM(r2.`total_net`) FROM `epc_payroll_runs` r2 WHERE r2.`site_key` = e.`site_key` AND r2.`status` = 'paid') AS `total_paid`
        FROM `epc_payroll_employees` e
        GROUP BY e.`site_key`
        ORDER BY `employees` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
