<?php
/**
 * P1 #23 — Multi-Currency GL
 *
 * FX rate management, multi-currency journal entries, unrealized
 * gain/loss calculation, and currency revaluation.
 * Schema: epc_fx_rates, epc_gl_currency_entries
 */

if (!defined('EPC_MULTI_CURRENCY_GL_VERSION')) {
    define('EPC_MULTI_CURRENCY_GL_VERSION', '1.0.0');
}

/* ─── schema ─── */

function epc_mcgl_ensure_schema(PDO $pdo): void
{
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_fx_rates` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `base_currency`   CHAR(3)        NOT NULL DEFAULT 'AED',
            `target_currency` CHAR(3)        NOT NULL,
            `rate`            DECIMAL(16,8)  NOT NULL,
            `inverse_rate`    DECIMAL(16,8)  NOT NULL,
            `source`          VARCHAR(64)    NOT NULL DEFAULT 'manual',
            `effective_date`  DATE           NOT NULL,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_rate` (`base_currency`, `target_currency`, `effective_date`),
            INDEX `idx_date` (`effective_date`),
            INDEX `idx_target` (`target_currency`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");

    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_gl_currency_entries` (
            `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64)    NOT NULL,
            `journal_ref`     VARCHAR(64)    NOT NULL DEFAULT '',
            `account_code`    VARCHAR(16)    NOT NULL,
            `account_name`    VARCHAR(128)   NOT NULL DEFAULT '',
            `entry_date`      DATE           NOT NULL,
            `txn_currency`    CHAR(3)        NOT NULL,
            `txn_amount`      DECIMAL(16,2)  NOT NULL DEFAULT 0.00,
            `fx_rate`         DECIMAL(16,8)  NOT NULL DEFAULT 1.00000000,
            `base_currency`   CHAR(3)        NOT NULL DEFAULT 'AED',
            `base_amount`     DECIMAL(16,2)  NOT NULL DEFAULT 0.00,
            `entry_type`      ENUM('debit','credit') NOT NULL,
            `description`     VARCHAR(255)   NOT NULL DEFAULT '',
            `revalued`        TINYINT(1)     NOT NULL DEFAULT 0,
            `reval_gain_loss` DECIMAL(16,2)  NOT NULL DEFAULT 0.00,
            `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX `idx_site_date` (`site_key`, `entry_date`),
            INDEX `idx_account` (`account_code`),
            INDEX `idx_currency` (`txn_currency`),
            INDEX `idx_journal` (`journal_ref`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
}

/* ─── supported currencies ─── */

function epc_mcgl_currencies(): array
{
    return array(
        'AED' => array('name' => 'UAE Dirham',         'symbol' => 'AED', 'decimals' => 2),
        'USD' => array('name' => 'US Dollar',          'symbol' => '$',   'decimals' => 2),
        'EUR' => array('name' => 'Euro',               'symbol' => "\xe2\x82\xac",   'decimals' => 2),
        'GBP' => array('name' => 'British Pound',      'symbol' => "\xc2\xa3",   'decimals' => 2),
        'SAR' => array('name' => 'Saudi Riyal',        'symbol' => 'SAR', 'decimals' => 2),
        'INR' => array('name' => 'Indian Rupee',       'symbol' => "\xe2\x82\xb9",   'decimals' => 2),
        'CNY' => array('name' => 'Chinese Yuan',       'symbol' => "\xc2\xa5",   'decimals' => 2),
        'JPY' => array('name' => 'Japanese Yen',       'symbol' => "\xc2\xa5",   'decimals' => 0),
        'KWD' => array('name' => 'Kuwaiti Dinar',      'symbol' => 'KWD', 'decimals' => 3),
        'BHD' => array('name' => 'Bahraini Dinar',     'symbol' => 'BHD', 'decimals' => 3),
        'OMR' => array('name' => 'Omani Rial',         'symbol' => 'OMR', 'decimals' => 3),
        'QAR' => array('name' => 'Qatari Riyal',       'symbol' => 'QAR', 'decimals' => 2),
        'EGP' => array('name' => 'Egyptian Pound',     'symbol' => 'EGP', 'decimals' => 2),
        'TRY' => array('name' => 'Turkish Lira',       'symbol' => 'TRY', 'decimals' => 2),
        'PKR' => array('name' => 'Pakistani Rupee',    'symbol' => 'PKR', 'decimals' => 2),
    );
}

/* ─── FX rate management ─── */

function epc_mcgl_set_rate(PDO $pdo, string $base, string $target, float $rate, string $date = '', string $source = 'manual'): array
{
    epc_mcgl_ensure_schema($pdo);

    if ($date === '') {
        $date = date('Y-m-d');
    }
    $inverse = $rate > 0 ? round(1 / $rate, 8) : 0;

    $st = $pdo->prepare("
        INSERT INTO `epc_fx_rates` (`base_currency`, `target_currency`, `rate`, `inverse_rate`, `source`, `effective_date`)
        VALUES (?, ?, ?, ?, ?, ?)
        ON DUPLICATE KEY UPDATE `rate` = VALUES(`rate`), `inverse_rate` = VALUES(`inverse_rate`), `source` = VALUES(`source`)
    ");
    $st->execute(array(strtoupper($base), strtoupper($target), $rate, $inverse, $source, $date));

    return array('ok' => true, 'base' => $base, 'target' => $target, 'rate' => $rate, 'inverse' => $inverse, 'date' => $date);
}

function epc_mcgl_get_rate(PDO $pdo, string $base, string $target, string $date = ''): float
{
    epc_mcgl_ensure_schema($pdo);

    if (strtoupper($base) === strtoupper($target)) {
        return 1.0;
    }
    if ($date === '') {
        $date = date('Y-m-d');
    }

    $st = $pdo->prepare("
        SELECT `rate` FROM `epc_fx_rates`
        WHERE `base_currency` = ? AND `target_currency` = ? AND `effective_date` <= ?
        ORDER BY `effective_date` DESC LIMIT 1
    ");
    $st->execute(array(strtoupper($base), strtoupper($target), $date));
    $rate = $st->fetchColumn();

    if ($rate !== false) {
        return (float) $rate;
    }

    $st = $pdo->prepare("
        SELECT `inverse_rate` FROM `epc_fx_rates`
        WHERE `base_currency` = ? AND `target_currency` = ? AND `effective_date` <= ?
        ORDER BY `effective_date` DESC LIMIT 1
    ");
    $st->execute(array(strtoupper($target), strtoupper($base), $date));
    $inv = $st->fetchColumn();

    return $inv !== false ? (float) $inv : 0;
}

function epc_mcgl_rate_history(PDO $pdo, string $base, string $target, int $days = 90): array
{
    $st = $pdo->prepare("
        SELECT `effective_date`, `rate`, `source`
        FROM `epc_fx_rates`
        WHERE `base_currency` = ? AND `target_currency` = ? AND `effective_date` >= DATE_SUB(CURDATE(), INTERVAL ? DAY)
        ORDER BY `effective_date` ASC
    ");
    $st->execute(array(strtoupper($base), strtoupper($target), $days));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── convert amount ─── */

function epc_mcgl_convert(PDO $pdo, float $amount, string $from, string $to, string $date = ''): array
{
    $rate = epc_mcgl_get_rate($pdo, $from, $to, $date);
    if ($rate <= 0) {
        return array('ok' => false, 'error' => 'No FX rate found for ' . $from . '/' . $to);
    }
    return array('ok' => true, 'from' => $from, 'to' => $to, 'rate' => $rate, 'original' => $amount, 'converted' => round($amount * $rate, 2));
}

/* ─── multi-currency journal entry ─── */

function epc_mcgl_journal_entry(PDO $pdo, string $siteKey, array $entry): array
{
    epc_mcgl_ensure_schema($pdo);

    $txnCurrency = strtoupper((string) ($entry['txn_currency'] ?? 'AED'));
    $baseCurrency = strtoupper((string) ($entry['base_currency'] ?? 'AED'));
    $txnAmount = (float) ($entry['txn_amount'] ?? 0);
    $entryDate = (string) ($entry['entry_date'] ?? date('Y-m-d'));

    $rate = 1.0;
    if ($txnCurrency !== $baseCurrency) {
        $rate = epc_mcgl_get_rate($pdo, $txnCurrency, $baseCurrency, $entryDate);
        if ($rate <= 0) {
            return array('ok' => false, 'error' => 'No FX rate for ' . $txnCurrency . '/' . $baseCurrency);
        }
    }
    $baseAmount = round($txnAmount * $rate, 2);

    $st = $pdo->prepare("
        INSERT INTO `epc_gl_currency_entries`
            (`site_key`, `journal_ref`, `account_code`, `account_name`, `entry_date`,
             `txn_currency`, `txn_amount`, `fx_rate`, `base_currency`, `base_amount`,
             `entry_type`, `description`)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ");
    $st->execute(array(
        $siteKey,
        (string) ($entry['journal_ref'] ?? ''),
        (string) ($entry['account_code'] ?? ''),
        (string) ($entry['account_name'] ?? ''),
        $entryDate,
        $txnCurrency,
        $txnAmount,
        $rate,
        $baseCurrency,
        $baseAmount,
        (string) ($entry['entry_type'] ?? 'debit'),
        (string) ($entry['description'] ?? ''),
    ));

    return array('ok' => true, 'entry_id' => (int) $pdo->lastInsertId(), 'fx_rate' => $rate, 'base_amount' => $baseAmount);
}

/* ─── unrealized gain/loss revaluation ─── */

function epc_mcgl_revalue(PDO $pdo, string $siteKey, string $baseCurrency = 'AED'): array
{
    epc_mcgl_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT * FROM `epc_gl_currency_entries`
        WHERE `site_key` = ? AND `txn_currency` != ? AND `revalued` = 0
    ");
    $st->execute(array($siteKey, $baseCurrency));
    $entries = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();

    $totalGainLoss = 0;
    $revalued = 0;

    foreach ($entries as $entry) {
        $currentRate = epc_mcgl_get_rate($pdo, $entry['txn_currency'], $baseCurrency);
        if ($currentRate <= 0) {
            continue;
        }

        $newBaseAmount = round((float) $entry['txn_amount'] * $currentRate, 2);
        $originalBase = (float) $entry['base_amount'];
        $gainLoss = $newBaseAmount - $originalBase;

        if ($entry['entry_type'] === 'credit') {
            $gainLoss = -$gainLoss;
        }

        $pdo->prepare("
            UPDATE `epc_gl_currency_entries`
            SET `revalued` = 1, `reval_gain_loss` = ?
            WHERE `id` = ?
        ")->execute(array($gainLoss, $entry['id']));

        $totalGainLoss += $gainLoss;
        $revalued++;
    }

    return array(
        'ok'              => true,
        'entries_revalued' => $revalued,
        'total_gain_loss' => round($totalGainLoss, 2),
        'base_currency'   => $baseCurrency,
    );
}

/* ─── currency exposure report ─── */

function epc_mcgl_exposure(PDO $pdo, string $siteKey): array
{
    epc_mcgl_ensure_schema($pdo);

    $st = $pdo->prepare("
        SELECT `txn_currency`,
               SUM(CASE WHEN `entry_type` = 'debit' THEN `txn_amount` ELSE 0 END) AS `total_debit`,
               SUM(CASE WHEN `entry_type` = 'credit' THEN `txn_amount` ELSE 0 END) AS `total_credit`,
               SUM(CASE WHEN `entry_type` = 'debit' THEN `base_amount` ELSE -`base_amount` END) AS `net_base`,
               SUM(`reval_gain_loss`) AS `unrealized_gl`,
               COUNT(*) AS `entry_count`
        FROM `epc_gl_currency_entries`
        WHERE `site_key` = ?
        GROUP BY `txn_currency`
        ORDER BY ABS(SUM(CASE WHEN `entry_type` = 'debit' THEN `base_amount` ELSE -`base_amount` END)) DESC
    ");
    $st->execute(array($siteKey));
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── trial balance in base currency ─── */

function epc_mcgl_trial_balance(PDO $pdo, string $siteKey, string $fromDate = '', string $toDate = ''): array
{
    epc_mcgl_ensure_schema($pdo);

    $where = array('`site_key` = ?');
    $params = array($siteKey);
    if ($fromDate !== '') {
        $where[] = '`entry_date` >= ?';
        $params[] = $fromDate;
    }
    if ($toDate !== '') {
        $where[] = '`entry_date` <= ?';
        $params[] = $toDate;
    }

    $sql = "
        SELECT `account_code`, `account_name`,
               SUM(CASE WHEN `entry_type` = 'debit' THEN `base_amount` ELSE 0 END) AS `debit_base`,
               SUM(CASE WHEN `entry_type` = 'credit' THEN `base_amount` ELSE 0 END) AS `credit_base`,
               SUM(`reval_gain_loss`) AS `unrealized_gl`
        FROM `epc_gl_currency_entries`
        WHERE " . implode(' AND ', $where) . "
        GROUP BY `account_code`, `account_name`
        ORDER BY `account_code`
    ";
    $st = $pdo->prepare($sql);
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── fleet stats (BOS) ─── */

function epc_mcgl_fleet_stats(PDO $pdo): array
{
    epc_mcgl_ensure_schema($pdo);

    $st = $pdo->query("
        SELECT `site_key`,
               COUNT(DISTINCT `txn_currency`) AS `currencies_used`,
               COUNT(*) AS `total_entries`,
               SUM(`reval_gain_loss`) AS `total_unrealized_gl`,
               MAX(`created_at`) AS `last_entry`
        FROM `epc_gl_currency_entries`
        GROUP BY `site_key`
        ORDER BY `currencies_used` DESC
    ");
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── seed default FX rates ─── */

function epc_mcgl_seed_rates(PDO $pdo): int
{
    $rates = array(
        array('AED', 'USD', 0.27229),
        array('AED', 'EUR', 0.25100),
        array('AED', 'GBP', 0.21500),
        array('AED', 'SAR', 1.02041),
        array('AED', 'INR', 22.7300),
        array('AED', 'CNY', 1.97000),
        array('AED', 'JPY', 41.8000),
        array('AED', 'KWD', 0.08370),
        array('AED', 'BHD', 0.10261),
        array('AED', 'OMR', 0.10477),
        array('AED', 'QAR', 0.99110),
        array('AED', 'EGP', 13.3700),
    );

    $count = 0;
    $date = date('Y-m-d');
    foreach ($rates as $r) {
        $result = epc_mcgl_set_rate($pdo, $r[0], $r[1], $r[2], $date, 'seed');
        if (!empty($result['ok'])) {
            $count++;
        }
    }
    return $count;
}
