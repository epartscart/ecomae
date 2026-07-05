<?php
/**
 * AML Compliance Module — Anti-Money Laundering checks, KYC, suspicious transaction reporting.
 * Compliant with FATF recommendations, UAE CBUAE guidelines, EU AMLD.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_aml_ensure_schema')) {
    function epc_aml_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_kyc` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `customer_name` varchar(200) NOT NULL DEFAULT '',
            `id_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'passport,emirates_id,national_id,driving_license,trade_license',
            `id_number` varchar(100) NOT NULL DEFAULT '',
            `id_expiry` date DEFAULT NULL,
            `id_document_path` varchar(500) NOT NULL DEFAULT '',
            `nationality` varchar(50) NOT NULL DEFAULT '',
            `dob` date DEFAULT NULL,
            `risk_level` enum('low','medium','high','very_high') NOT NULL DEFAULT 'low',
            `pep_status` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'Politically Exposed Person',
            `sanctions_checked` tinyint(1) NOT NULL DEFAULT 0,
            `sanctions_match` tinyint(1) NOT NULL DEFAULT 0,
            `verification_status` enum('pending','verified','rejected','expired') NOT NULL DEFAULT 'pending',
            `verified_by` int(11) NOT NULL DEFAULT 0,
            `verified_at` datetime DEFAULT NULL,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_customer` (`customer_id`),
            KEY `x_risk` (`risk_level`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='KYC records'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_transactions` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `transaction_type` varchar(30) NOT NULL DEFAULT '' COMMENT 'cash_sale,wire_transfer,card_payment,crypto',
            `amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `currency` varchar(3) NOT NULL DEFAULT 'AED',
            `reference` varchar(100) NOT NULL DEFAULT '',
            `risk_score` int(11) NOT NULL DEFAULT 0 COMMENT '0-100',
            `flagged` tinyint(1) NOT NULL DEFAULT 0,
            `flag_reason` varchar(300) NOT NULL DEFAULT '',
            `sar_filed` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'Suspicious Activity Report filed',
            `sar_reference` varchar(64) NOT NULL DEFAULT '',
            `reviewed_by` int(11) NOT NULL DEFAULT 0,
            `reviewed_at` datetime DEFAULT NULL,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_customer` (`customer_id`),
            KEY `x_flagged` (`flagged`),
            KEY `x_amount` (`amount`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AML transaction monitoring'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_rules` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `rule_name` varchar(200) NOT NULL DEFAULT '',
            `rule_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'threshold,pattern,frequency,country,pep',
            `threshold_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
            `threshold_currency` varchar(3) NOT NULL DEFAULT 'AED',
            `frequency_count` int(11) NOT NULL DEFAULT 0,
            `frequency_period_days` int(11) NOT NULL DEFAULT 1,
            `countries_list` text COMMENT 'JSON array of high-risk countries',
            `action` varchar(50) NOT NULL DEFAULT 'flag' COMMENT 'flag,block,alert,report',
            `is_active` tinyint(1) NOT NULL DEFAULT 1,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AML detection rules'");

        $db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_reports` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `report_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'sar,ctr,goaml,compliance_summary',
            `period_from` date DEFAULT NULL,
            `period_to` date DEFAULT NULL,
            `total_transactions` int(11) NOT NULL DEFAULT 0,
            `flagged_transactions` int(11) NOT NULL DEFAULT 0,
            `sar_count` int(11) NOT NULL DEFAULT 0,
            `filed_to` varchar(200) NOT NULL DEFAULT '' COMMENT 'FIU, CBUAE, etc.',
            `file_reference` varchar(100) NOT NULL DEFAULT '',
            `generated_by` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AML compliance reports'");
    }

    function epc_aml_check_transaction(PDO $db, int $companyId, int $customerId, float $amount, string $currency = 'AED'): array
    {
        $flags = [];
        $riskScore = 0;

        $stmt = $db->prepare("SELECT * FROM `epc_aml_rules` WHERE `company_id` = ? AND `is_active` = 1");
        $stmt->execute([$companyId]);
        $rules = $stmt->fetchAll(PDO::FETCH_ASSOC);

        foreach ($rules as $rule) {
            if ($rule['rule_type'] === 'threshold' && $amount >= (float) $rule['threshold_amount'] && $currency === $rule['threshold_currency']) {
                $flags[] = 'Exceeds threshold: ' . $rule['rule_name'];
                $riskScore += 30;
            }
            if ($rule['rule_type'] === 'frequency') {
                $since = time() - ($rule['frequency_period_days'] * 86400);
                $countStmt = $db->prepare("SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `customer_id` = ? AND `time_created` > ?");
                $countStmt->execute([$companyId, $customerId, $since]);
                if ((int) $countStmt->fetchColumn() >= $rule['frequency_count']) {
                    $flags[] = 'Frequency exceeded: ' . $rule['rule_name'];
                    $riskScore += 25;
                }
            }
        }

        $kyc = $db->prepare("SELECT `risk_level`, `pep_status` FROM `epc_aml_kyc` WHERE `company_id` = ? AND `customer_id` = ? ORDER BY `time_created` DESC LIMIT 1");
        $kyc->execute([$companyId, $customerId]);
        $kycRow = $kyc->fetch(PDO::FETCH_ASSOC);
        if ($kycRow) {
            if ($kycRow['risk_level'] === 'high') $riskScore += 20;
            if ($kycRow['risk_level'] === 'very_high') $riskScore += 40;
            if ($kycRow['pep_status']) $riskScore += 15;
        }

        $flagged = $riskScore >= 50 || !empty($flags);
        return ['flagged' => $flagged, 'risk_score' => min(100, $riskScore), 'flags' => $flags];
    }

    function epc_aml_default_rules(int $companyId): array
    {
        return [
            ['rule_name' => 'Cash transaction > 55,000 AED', 'rule_type' => 'threshold', 'threshold_amount' => 55000, 'threshold_currency' => 'AED', 'action' => 'flag'],
            ['rule_name' => 'Wire transfer > 100,000 AED', 'rule_type' => 'threshold', 'threshold_amount' => 100000, 'threshold_currency' => 'AED', 'action' => 'report'],
            ['rule_name' => 'More than 5 cash transactions in 7 days', 'rule_type' => 'frequency', 'frequency_count' => 5, 'frequency_period_days' => 7, 'action' => 'flag'],
            ['rule_name' => 'High-risk country origin', 'rule_type' => 'country', 'action' => 'flag'],
        ];
    }
}
