<?php
/**
 * ID Card Reader Module — scan/photograph official ID cards during invoicing.
 * Supports Emirates ID, passport, national ID for AML/KYC requirements.
 * OCR extraction from card photos, MRZ parsing for passports.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_card_reader_ensure_schema')) {
    function epc_card_reader_ensure_schema(PDO $db): void
    {
        $db->exec("CREATE TABLE IF NOT EXISTS `epc_card_scans` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `customer_id` int(11) NOT NULL DEFAULT 0,
            `invoice_id` int(11) NOT NULL DEFAULT 0,
            `card_type` varchar(30) NOT NULL DEFAULT '' COMMENT 'emirates_id,passport,national_id,driving_license,trade_license',
            `card_number` varchar(100) NOT NULL DEFAULT '',
            `holder_name` varchar(200) NOT NULL DEFAULT '',
            `nationality` varchar(50) NOT NULL DEFAULT '',
            `dob` date DEFAULT NULL,
            `expiry_date` date DEFAULT NULL,
            `gender` varchar(10) NOT NULL DEFAULT '',
            `issuing_country` varchar(50) NOT NULL DEFAULT '',
            `front_image_path` varchar(500) NOT NULL DEFAULT '',
            `back_image_path` varchar(500) NOT NULL DEFAULT '',
            `ocr_raw` text COMMENT 'Raw OCR extracted text',
            `ocr_confidence` decimal(5,2) NOT NULL DEFAULT 0.00,
            `mrz_data` text COMMENT 'Machine Readable Zone data (passports)',
            `verified` tinyint(1) NOT NULL DEFAULT 0,
            `scanned_by` int(11) NOT NULL DEFAULT 0,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_customer` (`customer_id`),
            KEY `x_invoice` (`invoice_id`),
            KEY `x_card_no` (`card_number`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ID card scans for AML'");
    }

    function epc_card_reader_save_scan(PDO $db, array $data, array $files): array
    {
        $uploadDir = $_SERVER['DOCUMENT_ROOT'] . '/uploads/card_scans/' . ($data['company_id'] ?? 0) . '/';
        if (!is_dir($uploadDir)) @mkdir($uploadDir, 0755, true);

        $frontPath = '';
        if (!empty($files['front_image']['tmp_name']) && is_uploaded_file($files['front_image']['tmp_name'])) {
            $frontName = time() . '_front_' . preg_replace('/[^a-zA-Z0-9._-]/', '_', basename($files['front_image']['name']));
            $frontPath = $uploadDir . $frontName;
            move_uploaded_file($files['front_image']['tmp_name'], $frontPath);
        }

        $backPath = '';
        if (!empty($files['back_image']['tmp_name']) && is_uploaded_file($files['back_image']['tmp_name'])) {
            $backName = time() . '_back_' . preg_replace('/[^a-zA-Z0-9._-]/', '_', basename($files['back_image']['name']));
            $backPath = $uploadDir . $backName;
            move_uploaded_file($files['back_image']['tmp_name'], $backPath);
        }

        $stmt = $db->prepare("INSERT INTO `epc_card_scans` (`company_id`,`customer_id`,`invoice_id`,`card_type`,`card_number`,`holder_name`,`nationality`,`dob`,`expiry_date`,`gender`,`issuing_country`,`front_image_path`,`back_image_path`,`scanned_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)");
        $stmt->execute([$data['company_id'] ?? 0, $data['customer_id'] ?? 0, $data['invoice_id'] ?? 0, $data['card_type'] ?? '', $data['card_number'] ?? '', $data['holder_name'] ?? '', $data['nationality'] ?? '', $data['dob'] ?? null, $data['expiry_date'] ?? null, $data['gender'] ?? '', $data['issuing_country'] ?? '', $frontPath, $backPath, $data['scanned_by'] ?? 0, time()]);

        return ['ok' => true, 'scan_id' => (int) $db->lastInsertId()];
    }

    function epc_card_reader_parse_emirates_id(string $rawText): array
    {
        $result = ['card_number' => '', 'holder_name' => '', 'nationality' => '', 'dob' => '', 'expiry_date' => '', 'gender' => ''];
        if (preg_match('/784-\d{4}-\d{7}-\d/', $rawText, $m)) $result['card_number'] = $m[0];
        if (preg_match('/Name:\s*(.+)/i', $rawText, $m)) $result['holder_name'] = trim($m[1]);
        if (preg_match('/Nationality:\s*(.+)/i', $rawText, $m)) $result['nationality'] = trim($m[1]);
        if (preg_match('/DOB:\s*(\d{2}\/\d{2}\/\d{4})/i', $rawText, $m)) $result['dob'] = $m[1];
        if (preg_match('/Expiry:\s*(\d{2}\/\d{2}\/\d{4})/i', $rawText, $m)) $result['expiry_date'] = $m[1];
        if (preg_match('/Sex:\s*(M|F)/i', $rawText, $m)) $result['gender'] = strtoupper($m[1]);
        return $result;
    }

    function epc_card_reader_parse_mrz(string $mrzLine1, string $mrzLine2): array
    {
        $result = ['card_number' => '', 'holder_name' => '', 'nationality' => '', 'dob' => '', 'expiry_date' => '', 'gender' => ''];
        if (strlen($mrzLine2) >= 28) {
            $result['card_number'] = trim(substr($mrzLine2, 0, 9), '<');
            $result['nationality'] = substr($mrzLine2, 10, 3);
            $dobRaw = substr($mrzLine2, 13, 6);
            $result['dob'] = '20' . substr($dobRaw, 0, 2) . '-' . substr($dobRaw, 2, 2) . '-' . substr($dobRaw, 4, 2);
            $result['gender'] = substr($mrzLine2, 20, 1);
            $expRaw = substr($mrzLine2, 21, 6);
            $result['expiry_date'] = '20' . substr($expRaw, 0, 2) . '-' . substr($expRaw, 2, 2) . '-' . substr($expRaw, 4, 2);
        }
        if (strlen($mrzLine1) >= 5) {
            $namePart = substr($mrzLine1, 5);
            $parts = explode('<<', $namePart, 2);
            $result['holder_name'] = str_replace('<', ' ', trim(($parts[1] ?? '') . ' ' . ($parts[0] ?? '')));
        }
        return $result;
    }

    function epc_card_reader_get_customer_scans(PDO $db, int $customerId): array
    {
        $stmt = $db->prepare("SELECT * FROM `epc_card_scans` WHERE `customer_id` = ? ORDER BY `time_created` DESC");
        $stmt->execute([$customerId]);
        return $stmt->fetchAll(PDO::FETCH_ASSOC);
    }
}
