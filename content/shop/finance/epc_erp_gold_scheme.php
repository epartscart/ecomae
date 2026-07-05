<?php
/**
 * Gold Scheme Module — customer saving schemes for jewellery businesses.
 * Supports value-based and gram-based schemes, maturity periods (6-12 months),
 * benefits like free month or free making charges. Worldwide practice.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_gold_scheme_ensure_schema')) {
    function epc_gold_scheme_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_gold_schemes` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `scheme_code` varchar(32) NOT NULL DEFAULT '',
            `scheme_name` varchar(200) NOT NULL DEFAULT '',
            `scheme_type` enum('value','gram') NOT NULL DEFAULT 'value' COMMENT 'value=fixed amount monthly, gram=fixed grams monthly',
            `maturity_months` int(11) NOT NULL DEFAULT 11 COMMENT '6-12 months typical',
            `bonus_type` enum('free_month','free_making','discount_pct','bonus_gram','none') NOT NULL DEFAULT 'free_month',
            `bonus_value` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'amount or grams or percentage depending on bonus_type',
            `min_installment` decimal(14,2) NOT NULL DEFAULT 500.00,
            `max_installment` decimal(14,2) NOT NULL DEFAULT 50000.00,
            `is_active` tinyint(1) NOT NULL DEFAULT 1,
            `terms_text` text COMMENT 'T&C shown to customer',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_active` (`is_active`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Gold saving scheme definitions'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_gold_scheme_enrollments` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `scheme_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `customer_name` varchar(200) NOT NULL DEFAULT '',
            `enrollment_no` varchar(32) NOT NULL DEFAULT '',
            `installment_amount` decimal(14,4) NOT NULL DEFAULT 0.0000 COMMENT 'per month value or grams',
            `start_date` date DEFAULT NULL,
            `maturity_date` date DEFAULT NULL,
            `total_paid` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `installments_paid` int(11) NOT NULL DEFAULT 0,
            `status` enum('active','matured','redeemed','cancelled','defaulted') NOT NULL DEFAULT 'active',
            `bonus_earned` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `redemption_invoice_id` int(11) NOT NULL DEFAULT 0,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_scheme` (`scheme_id`),
            KEY `x_customer` (`customer_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customer enrollments in gold schemes'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_gold_scheme_payments` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `enrollment_id` int(11) NOT NULL DEFAULT 0,
            `installment_no` int(11) NOT NULL DEFAULT 0,
            `amount` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `payment_mode` varchar(50) NOT NULL DEFAULT 'cash',
            `gold_rate_at_payment` decimal(14,4) NOT NULL DEFAULT 0.0000,
            `grams_equivalent` decimal(14,6) NOT NULL DEFAULT 0.000000,
            `receipt_no` varchar(64) NOT NULL DEFAULT '',
            `payment_date` date DEFAULT NULL,
            `notes` varchar(300) NOT NULL DEFAULT '',
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_enrollment` (`enrollment_id`),
            KEY `x_date` (`payment_date`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Scheme installment payments'");
    }

    function epc_gold_scheme_create(PDO $db, array $data): int
    {
        $stmt = $db->prepare("INSERT INTO `epc_gold_schemes` (`company_id`,`scheme_code`,`scheme_name`,`scheme_type`,`maturity_months`,`bonus_type`,`bonus_value`,`min_installment`,`max_installment`,`terms_text`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['scheme_code'] ?? '', $data['scheme_name'] ?? '', $data['scheme_type'] ?? 'value', $data['maturity_months'] ?? 11, $data['bonus_type'] ?? 'free_month', $data['bonus_value'] ?? 0, $data['min_installment'] ?? 500, $data['max_installment'] ?? 50000, $data['terms_text'] ?? '', time()]);
        return (int) $db->lastInsertId();
    }

    function epc_gold_scheme_enroll(PDO $db, array $data): int
    {
        $scheme = $db->prepare("SELECT * FROM `epc_gold_schemes` WHERE `id` = ?");
        $scheme->execute([$data['scheme_id'] ?? 0]);
        $s = $scheme->fetch(PDO::FETCH_ASSOC);
        if (!$s) return 0;

        $startDate = $data['start_date'] ?? date('Y-m-d');
        $maturityDate = date('Y-m-d', strtotime($startDate . ' + ' . $s['maturity_months'] . ' months'));
        $enrollNo = 'GS-' . date('Ym') . '-' . str_pad((string) (rand(1, 9999)), 4, '0', STR_PAD_LEFT);

        $stmt = $db->prepare("INSERT INTO `epc_gold_scheme_enrollments` (`company_id`,`scheme_id`,`customer_id`,`customer_name`,`enrollment_no`,`installment_amount`,`start_date`,`maturity_date`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$s['company_id'], $s['id'], $data['customer_id'] ?? 0, $data['customer_name'] ?? '', $enrollNo, $data['installment_amount'] ?? $s['min_installment'], $startDate, $maturityDate, 'active', time()]);
        return (int) $db->lastInsertId();
    }

    function epc_gold_scheme_pay_installment(PDO $db, int $enrollmentId, array $data): int
    {
        $stmt = $db->prepare("SELECT * FROM `epc_gold_scheme_enrollments` WHERE `id` = ?");
        $stmt->execute([$enrollmentId]);
        $e = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$e || $e['status'] !== 'active') return 0;

        $goldRate = (float) ($data['gold_rate'] ?? 0);
        $amount = (float) ($data['amount'] ?? $e['installment_amount']);
        $gramsEquiv = $goldRate > 0 ? $amount / $goldRate : 0;
        $installmentNo = $e['installments_paid'] + 1;

        $stmt = $db->prepare("INSERT INTO `epc_gold_scheme_payments` (`enrollment_id`,`installment_no`,`amount`,`payment_mode`,`gold_rate_at_payment`,`grams_equivalent`,`receipt_no`,`payment_date`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$enrollmentId, $installmentNo, $amount, $data['payment_mode'] ?? 'cash', $goldRate, $gramsEquiv, $data['receipt_no'] ?? '', $data['payment_date'] ?? date('Y-m-d'), time()]);

        $db->prepare("UPDATE `epc_gold_scheme_enrollments` SET `installments_paid` = `installments_paid` + 1, `total_paid` = `total_paid` + ?, `time_updated` = ? WHERE `id` = ?")->execute([$amount, time(), $enrollmentId]);

        return (int) $db->lastInsertId();
    }

    function epc_gold_scheme_check_maturity(PDO $db, int $companyId): array
    {
        $stmt = $db->prepare("SELECT e.*, s.`scheme_name`, s.`bonus_type`, s.`bonus_value`, s.`maturity_months` FROM `epc_gold_scheme_enrollments` e JOIN `epc_gold_schemes` s ON s.`id` = e.`scheme_id` WHERE e.`company_id` = ? AND e.`status` = 'active' AND e.`maturity_date` <= CURDATE()");
        $stmt->execute([$companyId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
