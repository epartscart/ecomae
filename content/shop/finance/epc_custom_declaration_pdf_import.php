<?php
/**
 * UAE customs declaration PDF import — parse standard declaration copy and map boxes 1–59.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_custom_shipping.php';

if (!function_exists('epc_cs_parse_decimal')) {
	function epc_cs_parse_decimal($val, $default = 0.0)
	{
		if ($val === '' || $val === null) {
			return $default;
		}
		$s = trim((string) $val);
		$s = str_replace(array(',', ' '), '', $s);
		if ($s === '' || !is_numeric($s)) {
			return $default;
		}
		return (float) $s;
	}
}

if (!function_exists('epc_cs_decimal_string')) {
	function epc_cs_decimal_string($val, $default = '')
	{
		if ($val === '' || $val === null) {
			return $default;
		}
		$s = trim(str_replace(array(',', ' '), '', (string) $val));
		if ($s === '' || !is_numeric($s)) {
			return $default;
		}
		return $s;
	}
}

/** All 59 declaration box areas (+ sub-fields 12A, 37A, 37B, 48A–C). */
function epc_cs_declaration_box_definitions()
{
	static $defs = null;
	if ($defs !== null) {
		return $defs;
	}
	$defs = array(
		'box_01' => array('num' => '1', 'label' => 'DEC NO', 'group' => 'header', 'type' => 'text'),
		'box_02' => array('num' => '2', 'label' => 'DEC DATE', 'group' => 'header', 'type' => 'date'),
		'box_03' => array('num' => '3', 'label' => 'DEC TYPE', 'group' => 'header', 'type' => 'text'),
		'box_04' => array('num' => '4', 'label' => 'PORT TYPE', 'group' => 'header', 'type' => 'text'),
		'box_05' => array('num' => '5', 'label' => 'DELIVERY ORDER NO.', 'group' => 'header', 'type' => 'text'),
		'box_06' => array('num' => '6', 'label' => 'IMPORTER / EXPORTER (Company)', 'group' => 'header', 'type' => 'text'),
		'box_07' => array('num' => '7', 'label' => 'NET WEIGHT', 'group' => 'header', 'type' => 'text'),
		'box_08' => array('num' => '8', 'label' => "CARRIER'S / CAPTAIN / DRIVER", 'group' => 'header', 'type' => 'text'),
		'box_09' => array('num' => '9', 'label' => 'INTERCESSOR CO.', 'group' => 'header', 'type' => 'text'),
		'box_10' => array('num' => '10', 'label' => 'GROSS WEIGHT', 'group' => 'header', 'type' => 'text'),
		'box_11' => array('num' => '11', 'label' => "CARRIER'S NAME", 'group' => 'header', 'type' => 'text'),
		'box_12' => array('num' => '12', 'label' => 'COMMERCIAL REG. No.', 'group' => 'header', 'type' => 'text'),
		'box_12a' => array('num' => '12A', 'label' => 'TIN No.', 'group' => 'header', 'type' => 'text'),
		'box_13' => array('num' => '13', 'label' => 'MEASUREMENT', 'group' => 'header', 'type' => 'text'),
		'box_14' => array('num' => '14', 'label' => 'VOYAGE / FLIGHT No.', 'group' => 'header', 'type' => 'text'),
		'box_15' => array('num' => '15', 'label' => 'EXPORTED TO', 'group' => 'header', 'type' => 'text'),
		'box_16' => array('num' => '16', 'label' => 'NO. OF PACKAGES', 'group' => 'header', 'type' => 'text'),
		'box_17' => array('num' => '17', 'label' => 'B/L-AWB No. / MANIF.', 'group' => 'header', 'type' => 'text'),
		'box_18' => array('num' => '18', 'label' => 'PORT OF LOADING', 'group' => 'header', 'type' => 'text'),
		'box_19' => array('num' => '19', 'label' => 'MARKS & NUMBERS', 'group' => 'header', 'type' => 'text'),
		'box_20' => array('num' => '20', 'label' => 'PORT OF DISCHARGE', 'group' => 'header', 'type' => 'text'),
		'box_21' => array('num' => '21', 'label' => 'DESTINATION', 'group' => 'header', 'type' => 'text'),
		'box_22' => array('num' => '22', 'label' => 'H.S. CODE', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_23' => array('num' => '23', 'label' => 'GOODS DESCRIPTION', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_24' => array('num' => '24', 'label' => 'ORIGIN', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_25' => array('num' => '25', 'label' => 'FOREIGN VALUE', 'group' => 'line', 'type' => 'number', 'multi' => 'line'),
		'box_26' => array('num' => '26', 'label' => 'CURRENCY TYPE', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_27' => array('num' => '27', 'label' => 'CURRENCY VALUE', 'group' => 'line', 'type' => 'number', 'multi' => 'line'),
		'box_28' => array('num' => '28', 'label' => 'CIF LOCAL VALUE', 'group' => 'line', 'type' => 'number', 'multi' => 'line'),
		'box_29' => array('num' => '29', 'label' => 'D. RATE', 'group' => 'line', 'type' => 'number', 'multi' => 'line'),
		'box_30' => array('num' => '30', 'label' => 'INCOME TYPE', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_31' => array('num' => '31', 'label' => 'TOTAL DUTY AED', 'group' => 'line', 'type' => 'number', 'multi' => 'line'),
		'box_32' => array('num' => '32', 'label' => 'PACKAGES QTY', 'group' => 'line', 'type' => 'number', 'multi' => 'line'),
		'box_33' => array('num' => '33', 'label' => 'PACKAGES TYPE', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_34' => array('num' => '34', 'label' => 'ITEM QTY', 'group' => 'line', 'type' => 'number', 'multi' => 'line'),
		'box_35' => array('num' => '35', 'label' => 'ITEM UNIT', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_36' => array('num' => '36', 'label' => 'WEIGHT NET', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_37' => array('num' => '37', 'label' => 'WEIGHT GROSS', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_37a' => array('num' => '37A', 'label' => 'AIP NO.', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_37b' => array('num' => '37B', 'label' => 'AIP DUTY', 'group' => 'line', 'type' => 'text', 'multi' => 'line'),
		'box_38' => array('num' => '38', 'label' => 'CLEARING AGENT', 'group' => 'footer', 'type' => 'text'),
		'box_39' => array('num' => '39', 'label' => 'LICENCE No.', 'group' => 'footer', 'type' => 'text'),
		'box_40' => array('num' => '40', 'label' => 'CUSTOMS RESTRICTIONS AGENCY', 'group' => 'footer', 'type' => 'text'),
		'box_41' => array('num' => '41', 'label' => 'RELEASE REF.', 'group' => 'footer', 'type' => 'text'),
		'box_42' => array('num' => '42', 'label' => 'EXEMPTION OF DUTY CODE', 'group' => 'footer', 'type' => 'text'),
		'box_43' => array('num' => '43', 'label' => 'UNIFIED CUSTOMS CODE', 'group' => 'footer', 'type' => 'text'),
		'box_44' => array('num' => '44', 'label' => 'GCC AEO CODE', 'group' => 'footer', 'type' => 'text'),
		'box_45' => array('num' => '45', 'label' => 'OTHER REMARKS', 'group' => 'footer', 'type' => 'text', 'multi' => 'lines'),
		'box_46' => array('num' => '46', 'label' => 'EXIT PORT', 'group' => 'footer', 'type' => 'text'),
		'box_47' => array('num' => '47', 'label' => 'QR Code', 'group' => 'footer', 'type' => 'textarea'),
		'box_48' => array('num' => '48', 'label' => 'TOTAL DUTY', 'group' => 'footer', 'type' => 'number'),
		'box_48a' => array('num' => '48A', 'label' => 'VAT', 'group' => 'footer', 'type' => 'number'),
		'box_48b' => array('num' => '48B', 'label' => 'EXCISE TAX', 'group' => 'footer', 'type' => 'number'),
		'box_48c' => array('num' => '48C', 'label' => 'ANTI DUMPING', 'group' => 'footer', 'type' => 'number'),
		'box_49' => array('num' => '49', 'label' => 'HANDLING', 'group' => 'footer', 'type' => 'number'),
		'box_50' => array('num' => '50', 'label' => 'OTHER CHARGES', 'group' => 'footer', 'type' => 'number'),
		'box_51' => array('num' => '51', 'label' => 'DEFINITE', 'group' => 'footer', 'type' => 'number'),
		'box_52' => array('num' => '52', 'label' => 'TOTAL FEE / INSURED', 'group' => 'footer', 'type' => 'number'),
		'box_53' => array('num' => '53', 'label' => 'PAYMENT METHOD', 'group' => 'footer', 'type' => 'text'),
		'box_54' => array('num' => '54', 'label' => 'PAYMENT No.', 'group' => 'footer', 'type' => 'text', 'multi' => 'lines'),
		'box_55' => array('num' => '55', 'label' => 'PAYMENT DATE', 'group' => 'footer', 'type' => 'date'),
		'box_56' => array('num' => '56', 'label' => 'PAYMENT BANK', 'group' => 'footer', 'type' => 'text'),
		'box_57' => array('num' => '57', 'label' => 'RECEIPT NO.', 'group' => 'footer', 'type' => 'text'),
		'box_58' => array('num' => '58', 'label' => 'RECEIPT DATE', 'group' => 'footer', 'type' => 'date'),
		'box_59' => array('num' => '59', 'label' => 'RECEIPT BANK', 'group' => 'footer', 'type' => 'text'),
	);
	return $defs;
}

function epc_cs_declaration_box_count()
{
	return count(epc_cs_declaration_box_definitions());
}

/** ERP-only fields that PDF import must not overwrite. */
function epc_cs_pdf_manual_only_fields()
{
	return array(
		'supplier_detail', 'supplier_code_customs', 'ld_po_number', 'lc_dc_number',
		'srv_number', 'd365_po_reference', 'd365_so_reference', 'customer_ref',
		'customer_country', 'import_reexport_declaration_ref',
		'document_expiry_date', 'remarks',
	);
}

/** Box 45 structured sub-fields (parsed from OTHER REMARKS). */
function epc_cs_box45_field_definitions()
{
	return array(
		'invoice_term' => array('label' => 'Invoice term', 'type' => 'text'),
		'invoice_value' => array('label' => 'Invoice value', 'type' => 'number'),
		'customs_inspection_required' => array('label' => 'Customs inspection required', 'type' => 'select', 'options' => array('', 'YES', 'NO')),
	);
}

function epc_cs_pdf_normalize_yes_no($raw)
{
	$v = strtoupper(trim((string) $raw));
	if (in_array($v, array('Y', 'YES', '1', 'TRUE'), true)) {
		return 'YES';
	}
	if (in_array($v, array('N', 'NO', '0', 'FALSE'), true)) {
		return 'NO';
	}
	return '';
}

/** Parse invoice term, value, and inspection flag from Box 45 text and related lines. */
function epc_cs_pdf_parse_box45_fields($flat, array $lines, array $box45Lines)
{
	$result = array(
		'invoice_term' => '',
		'invoice_value' => '',
		'customs_inspection_required' => '',
	);
	$sources = array_merge($box45Lines, $lines);
	foreach ($sources as $line) {
		if ($result['invoice_term'] === '' && preg_match('/\[(FOB|CIF|CFR|EXW|DAP|DDP|FCA|CPT|CIP|FAS|DAT|DPU)\]/i', $line, $m)) {
			$result['invoice_term'] = strtoupper($m[1]);
		}
		if ($result['invoice_term'] === '' && preg_match('/\b(FOB|CIF|CFR|EXW|DAP|DDP|FCA|CPT|CIP|FAS|DAT|DPU)\b/i', $line, $m)
			&& !preg_match('/FRT|FREIGHT|INS\s*:/i', $line)) {
			$result['invoice_term'] = strtoupper($m[1]);
		}
		if ($result['invoice_value'] === '' && preg_match('/^(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:\s*([\d,\.]+)/i', $line, $m)) {
			$result['invoice_value'] = epc_cs_decimal_string(str_replace(',', '', $m[1]));
		}
		if ($result['customs_inspection_required'] === '' && preg_match('/(?:Custom\s*Inspection|Inspection\s+Required|Physical\s+Inspection|CUST\s*INSP)\s*:?\s*(YES|NO|Y|N)/i', $line, $m)) {
			$result['customs_inspection_required'] = epc_cs_pdf_normalize_yes_no($m[1]);
		}
	}
	if ($result['invoice_term'] === '' && preg_match('/\[(FOB|CIF|CFR|EXW|DAP|DDP|FCA|CPT|CIP|FAS|DAT|DPU)\]/i', $flat, $m)) {
		$result['invoice_term'] = strtoupper($m[1]);
	}
	if ($result['invoice_value'] === '' && preg_match('/(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:\s*([\d,\.]+)/i', $flat, $m)) {
		$result['invoice_value'] = epc_cs_decimal_string(str_replace(',', '', $m[1]));
	}
	if ($result['customs_inspection_required'] === '') {
		foreach (array(
			'/(?:Custom\s*Inspection|Inspection\s+Required|Physical\s+Inspection|CUST\s*INSP)\s*:?\s*(YES|NO|Y|N)/i',
			'/\bInspection\s+(YES|NO|Y|N)\b/i',
		) as $pat) {
			if (preg_match($pat, $flat, $m)) {
				$result['customs_inspection_required'] = epc_cs_pdf_normalize_yes_no($m[1]);
				break;
			}
		}
	}
	return $result;
}

function epc_cs_line_item_box_keys()
{
	return array(
		'box_22' => 'hs_code', 'box_23' => 'description', 'box_24' => 'country_of_origin',
		'box_25' => 'foreign_value', 'box_26' => 'currency', 'box_27' => 'currency_rate',
		'box_28' => 'cif_local_value', 'box_29' => 'duty_rate', 'box_30' => 'income_type',
		'box_31' => 'total_duty_aed', 'box_32' => 'packages_qty', 'box_33' => 'packages_type',
		'box_34' => 'quantity', 'box_35' => 'unit', 'box_36' => 'weight_net',
		'box_37' => 'weight_gross', 'box_37a' => 'aip_no', 'box_37b' => 'aip_duty',
	);
}

function epc_cs_ensure_box_schema(PDO $db)
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	$cols = array();
	try {
		$st = $db->query('SHOW COLUMNS FROM `epc_custom_shipping_declarations`');
		while ($c = $st->fetch(PDO::FETCH_ASSOC)) {
			$cols[$c['Field']] = true;
		}
	} catch (Exception $e) {
		return;
	}
	if (empty($cols['box_data'])) {
		$db->exec('ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `box_data` LONGTEXT NULL AFTER `field_data`');
	}
	if (empty($cols['pdf_autofill_keys'])) {
		$db->exec('ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `pdf_autofill_keys` TEXT NULL AFTER `box_data`');
	}
	if (empty($cols['pdf_file_path'])) {
		$db->exec('ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `pdf_file_path` VARCHAR(512) NULL AFTER `pdf_autofill_keys`');
	}
	if (empty($cols['pdf_file_name'])) {
		$db->exec('ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `pdf_file_name` VARCHAR(255) NULL AFTER `pdf_file_path`');
	}
	foreach (array(
		'box_45_invoice_term' => "VARCHAR(16) NULL DEFAULT NULL AFTER `pdf_file_name`",
		'box_45_invoice_value' => 'DECIMAL(18,2) NULL DEFAULT NULL AFTER `box_45_invoice_term`',
		'box_45_customs_inspection' => "VARCHAR(8) NULL DEFAULT NULL AFTER `box_45_invoice_value`",
	) as $col => $def) {
		if (empty($cols[$col])) {
			try {
				$db->exec('ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `' . $col . '` ' . $def);
			} catch (Exception $e) {
			}
		}
	}
	if (empty($cols['invoice_term'])) {
		try {
			$db->exec("ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `invoice_term` VARCHAR(16) NOT NULL DEFAULT '' AFTER `invoice_amount_aed`");
		} catch (Exception $e) {
		}
	}
	if (empty($cols['customs_inspection_required'])) {
		try {
			$db->exec("ALTER TABLE `epc_custom_shipping_declarations` ADD COLUMN `customs_inspection_required` VARCHAR(8) NOT NULL DEFAULT '' AFTER `invoice_term`");
		} catch (Exception $e) {
		}
	}
	try {
		$db->exec('ALTER TABLE `epc_custom_shipping_declarations` MODIFY `declaration_number` VARCHAR(64) NULL DEFAULT NULL');
		$db->exec("UPDATE `epc_custom_shipping_declarations` SET `declaration_number` = NULL WHERE TRIM(COALESCE(`declaration_number`, '')) = ''");
	} catch (Exception $e) {
	}
	try {
		$idx = $db->query("SHOW INDEX FROM `epc_custom_shipping_declarations` WHERE Key_name = 'uq_cs_declaration_number'");
		if (!$idx || !$idx->fetch(PDO::FETCH_ASSOC)) {
			$db->exec('ALTER TABLE `epc_custom_shipping_declarations` ADD UNIQUE KEY `uq_cs_declaration_number` (`declaration_number`)');
		}
	} catch (Exception $e) {
	}
	epc_cs_ensure_line_item_box_columns($db);
}

function epc_cs_ensure_line_item_box_columns(PDO $db)
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	$extra = array(
		'foreign_value' => 'DECIMAL(18,2) NOT NULL DEFAULT 0',
		'currency' => "VARCHAR(16) NOT NULL DEFAULT ''",
		'currency_rate' => 'DECIMAL(18,6) NOT NULL DEFAULT 0',
		'cif_local_value' => 'DECIMAL(18,2) NOT NULL DEFAULT 0',
		'duty_rate' => 'DECIMAL(8,2) NOT NULL DEFAULT 0',
		'income_type' => "VARCHAR(32) NOT NULL DEFAULT ''",
		'total_duty_aed' => 'DECIMAL(18,2) NOT NULL DEFAULT 0',
		'packages_qty' => 'DECIMAL(18,4) NOT NULL DEFAULT 0',
		'packages_type' => "VARCHAR(64) NOT NULL DEFAULT ''",
		'weight_net' => 'DECIMAL(18,4) NOT NULL DEFAULT 0',
		'weight_gross' => 'DECIMAL(18,4) NOT NULL DEFAULT 0',
		'aip_no' => "VARCHAR(64) NOT NULL DEFAULT ''",
		'aip_duty' => "VARCHAR(64) NOT NULL DEFAULT ''",
	);
	$cols = array();
	try {
		$st = $db->query('SHOW COLUMNS FROM `epc_custom_shipping_declaration_items`');
		while ($c = $st->fetch(PDO::FETCH_ASSOC)) {
			$cols[$c['Field']] = true;
		}
	} catch (Exception $e) {
		return;
	}
	foreach ($extra as $col => $def) {
		if (empty($cols[$col])) {
			$db->exec('ALTER TABLE `epc_custom_shipping_declaration_items` ADD COLUMN `' . $col . '` ' . $def);
		}
	}
}

function epc_cs_pdf_pdftotext_available()
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	foreach (array(
		'command -v pdftotext 2>/dev/null',
		'which pdftotext 2>/dev/null',
	) as $cmd) {
		$r = trim((string) @shell_exec($cmd));
		if ($r !== '' && stripos($r, 'not found') === false) {
			$cached = $r;
			return $cached;
		}
	}
	foreach (array('/usr/bin/pdftotext', '/usr/local/bin/pdftotext') as $p) {
		if (is_executable($p)) {
			$cached = $p;
			return $cached;
		}
	}
	$cached = '';
	return $cached;
}

function epc_cs_pdf_pdftotext_diagnostics()
{
	$path = epc_cs_pdf_pdftotext_available();
	return array(
		'available' => ($path !== ''),
		'path' => $path,
		'shell_exec' => function_exists('shell_exec'),
		'exec' => function_exists('exec'),
	);
}

function epc_cs_pdf_extract_text($binary)
{
	if ($binary === '' || strncmp($binary, '%PDF', 4) !== 0) {
		return '';
	}
	$tmpIn = tempnam(sys_get_temp_dir(), 'epc_cs_pdf_');
	if ($tmpIn) {
		$tmpOut = $tmpIn . '.txt';
		if (@file_put_contents($tmpIn, $binary) !== false) {
			$cmds = array(
				'pdftotext -layout -enc UTF-8 -q %s %s 2>/dev/null',
				'pdftotext -layout -q %s %s 2>/dev/null',
				'/usr/bin/pdftotext -layout -q %s %s 2>/dev/null',
				'pdftotext -layout -q %s %s 2>nul',
			);
			foreach ($cmds as $fmt) {
				@shell_exec(sprintf($fmt, escapeshellarg($tmpIn), escapeshellarg($tmpOut)));
				if (is_file($tmpOut)) {
					$viaTool = (string) @file_get_contents($tmpOut);
					@unlink($tmpOut);
					if (epc_cs_pdf_text_looks_valid($viaTool)) {
						@unlink($tmpIn);
						return epc_cs_pdf_normalize_text($viaTool);
					}
				}
			}
			@unlink($tmpIn);
		}
	}
	return epc_cs_pdf_extract_text_from_streams($binary);
}

function epc_cs_pdf_text_looks_valid($text)
{
	$text = (string) $text;
	if (trim($text) === '') {
		return false;
	}
	$len = strlen($text);
	if ($len < 20) {
		return false;
	}
	$good = preg_match_all('/[\x20-\x7E\n\r]/', $text);
	return ($good / max(1, $len)) >= 0.55;
}

/** Decode FlateDecode stream payload (handles zlib and raw deflate). */
function epc_cs_pdf_decode_flate_stream($stream)
{
	if (!is_string($stream) || $stream === '') {
		return '';
	}
	foreach (array($stream, substr($stream, 2)) as $candidate) {
		$decoded = @gzuncompress($candidate);
		if (is_string($decoded) && $decoded !== '') {
			return $decoded;
		}
		$decoded = @gzinflate($candidate);
		if (is_string($decoded) && $decoded !== '') {
			return $decoded;
		}
	}
	if (function_exists('gzdecode')) {
		$decoded = @gzdecode($stream);
		if (is_string($decoded) && $decoded !== '') {
			return $decoded;
		}
	}
	return '';
}

/** Extract literal/hex strings from PDF content operators. */
function epc_cs_pdf_collect_operator_strings($content, array &$lines)
{
	if (!function_exists('epc_uae_fta_pdf_unescape_string')) {
		$taxPath = dirname(__FILE__) . '/epc_uae_tax_compliance.php';
		if (is_file($taxPath)) {
			require_once $taxPath;
		}
	}
	if (!function_exists('epc_uae_fta_pdf_unescape_string')) {
		return;
	}
	if (preg_match_all('/\((?:[^\\\\()]|\\\\.)*\)\s*(?:Tj|\'|\')/s', $content, $tj)) {
		foreach ($tj[0] as $raw) {
			if (preg_match('/\((.*)\)\s*(?:Tj|\'|\')/s', $raw, $sm)) {
				$chunk = epc_uae_fta_pdf_unescape_string($sm[1]);
				if (trim($chunk) !== '') {
					$lines[] = $chunk;
				}
			}
		}
	}
	if (preg_match_all('/<([0-9A-Fa-f\s]+)>\s*(?:Tj|\'|\')/s', $content, $hex)) {
		foreach ($hex[1] as $h) {
			$h = preg_replace('/\s+/', '', $h);
			if ($h === '' || (strlen($h) % 2) !== 0) {
				continue;
			}
			$chunk = '';
			for ($i = 0; $i < strlen($h); $i += 2) {
				$chunk .= chr(hexdec(substr($h, $i, 2)));
			}
			if (trim($chunk) !== '') {
				$lines[] = $chunk;
			}
		}
	}
	if (preg_match_all('/\[(.*?)\]\s*TJ/s', $content, $arrays)) {
		foreach ($arrays[1] as $arr) {
			if (preg_match_all('/\((?:[^\\\\()]|\\\\.)*\)/s', $arr, $parts)) {
				$chunk = '';
				foreach ($parts[0] as $lit) {
					if (preg_match('/\((.*)\)/s', $lit, $sm)) {
						$chunk .= epc_uae_fta_pdf_unescape_string($sm[1]);
					}
				}
				if (trim($chunk) !== '') {
					$lines[] = $chunk;
				}
			}
		}
	}
}

/** Fallback stream parse — BT/ET blocks, flate streams, hex/TJ operators. */
function epc_cs_pdf_extract_text_from_streams($binary)
{
	if (!function_exists('epc_uae_fta_pdf_unescape_string')) {
		$taxPath = dirname(__FILE__) . '/epc_uae_tax_compliance.php';
		if (is_file($taxPath)) {
			require_once $taxPath;
		}
	}
	$lines = array();
	epc_cs_pdf_collect_operator_strings($binary, $lines);
	if (preg_match_all('/\bBT\b(.*?)ET/s', $binary, $blocks)) {
		foreach ($blocks[1] as $block) {
			epc_cs_pdf_collect_operator_strings($block, $lines);
		}
	}
	if (preg_match_all('/stream\r?\n(.*?)\r?\nendstream/s', $binary, $streams)) {
		foreach ($streams[1] as $stream) {
			$decoded = epc_cs_pdf_decode_flate_stream($stream);
			if ($decoded === '') {
				continue;
			}
			epc_cs_pdf_collect_operator_strings($decoded, $lines);
			if (preg_match_all('/\bBT\b(.*?)ET/s', $decoded, $innerBlocks)) {
				foreach ($innerBlocks[1] as $block) {
					epc_cs_pdf_collect_operator_strings($block, $lines);
				}
			}
		}
	}
	if (empty($lines) && function_exists('epc_uae_fta_extract_text_from_pdf_binary')) {
		$flat = epc_uae_fta_extract_text_from_pdf_binary($binary);
		if ($flat !== '') {
			$lines = preg_split('/(?<=[.!?])\s+|\s{2,}/', $flat) ?: array($flat);
		}
	}
	$text = epc_cs_pdf_normalize_text(implode("\n", $lines));
	if ($text !== '' && epc_cs_pdf_text_looks_valid($text)) {
		return $text;
	}
	$flat = epc_cs_pdf_normalize_text(implode(' ', $lines));
	if ($flat !== '' && strlen($flat) > strlen($text)) {
		return $flat;
	}
	return $text;
}

function epc_cs_pdf_normalize_text($text)
{
	$text = str_replace(array("\r\n", "\r"), "\n", (string) $text);
	$text = preg_replace('/[\x00-\x08\x0B\x0C\x0E-\x1F]/', '', $text);
	return trim($text);
}

function epc_cs_pdf_all_declaration_types_flat()
{
	$out = array();
	foreach (epc_cs_declaration_types_registry() as $types) {
		foreach ($types as $t) {
			$out[] = $t;
		}
	}
	usort($out, function ($a, $b) {
		return strlen($b) - strlen($a);
	});
	return $out;
}

function epc_cs_pdf_detect_declaration_type($text, $hint = '')
{
	$hint = trim((string) $hint);
	if ($hint !== '') {
		foreach (epc_cs_pdf_all_declaration_types_flat() as $t) {
			if (strcasecmp($t, $hint) === 0) {
				return $t;
			}
		}
	}
	foreach (epc_cs_pdf_all_declaration_types_flat() as $t) {
		if (stripos($text, $t) !== false) {
			return $t;
		}
	}
	if (preg_match('/\b(IMPORT|EXPORT|TRANSIT|COURIER)\b/i', $text, $m)) {
		return strtoupper($m[1]);
	}
	return '';
}

function epc_cs_pdf_parse_date($raw)
{
	$raw = trim((string) $raw);
	if (preg_match('/^(\d{2})\/(\d{2})\/(\d{4})$/', $raw, $m)) {
		return $m[3] . '-' . $m[2] . '-' . $m[1];
	}
	if (preg_match('/^(\d{4})-(\d{2})-(\d{2})$/', $raw)) {
		return $raw;
	}
	if (is_numeric($raw) && (float) $raw > 40000 && (float) $raw < 60000) {
		$ts = ((int) $raw - 25569) * 86400;
		return gmdate('Y-m-d', $ts);
	}
	return $raw;
}

function epc_cs_pdf_collect_lines($text)
{
	$lines = array();
	foreach (preg_split('/\n+/', $text) as $line) {
		$line = trim(preg_replace('/\s+/', ' ', $line));
		if ($line === '' || preg_match('/^Page \d+ of \d+$/i', $line)) {
			continue;
		}
		$lines[] = $line;
	}
	return $lines;
}

function epc_cs_pdf_is_country_code($s)
{
	static $skip = array('AE', 'IN', 'INS', 'FOB', 'FRT', 'LOC', 'KG', 'AED', 'USD', 'EUR', 'GBP');
	$s = strtoupper(trim((string) $s));
	if (strlen($s) !== 2 || !ctype_alpha($s)) {
		return false;
	}
	return !in_array($s, $skip, true);
}

function epc_cs_pdf_is_hs_code($s)
{
	return (bool) preg_match('/^\d{8}$/', trim((string) $s));
}

function epc_cs_pdf_is_payment_line($s)
{
	return (bool) preg_match('/^[A-Z]{2,5}\s+[\d.]+\s+\[\d+\]/', trim((string) $s));
}

/** Keep first occurrence of each exact string (UAE PDFs often repeat footer blocks). */
function epc_cs_pdf_dedupe_strings(array $lines)
{
	$seen = array();
	$out = array();
	foreach ($lines as $line) {
		$key = trim((string) $line);
		if ($key === '') {
			continue;
		}
		if (isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$out[] = $line;
	}
	return $out;
}

/** When PDF text repeats a full block (e.g. 4 lines then same 4 again), keep first half only. */
function epc_cs_pdf_dedupe_repeated_block(array $items, $equals = null)
{
	$n = count($items);
	if ($n < 2 || $n % 2 !== 0) {
		return $items;
	}
	$half = (int) ($n / 2);
	for ($i = 0; $i < $half; $i++) {
		$a = $items[$i];
		$b = $items[$i + $half];
		if ($equals !== null) {
			if (!$equals($a, $b)) {
				return $items;
			}
		} elseif ($a !== $b) {
			return $items;
		}
	}
	return array_slice($items, 0, $half);
}

function epc_cs_pdf_line_item_hash(array $item)
{
	$parts = array(
		trim((string) ($item['hs_code'] ?? '')),
		trim((string) ($item['country_of_origin'] ?? '')),
		trim((string) ($item['description'] ?? '')),
		(string) round((float) ($item['weight_gross'] ?? $item['weight'] ?? 0), 4),
		(string) round((float) ($item['foreign_value'] ?? 0), 4),
		(string) round((float) ($item['cif_local_value'] ?? $item['amount'] ?? 0), 4),
		(string) round((float) ($item['quantity'] ?? 0), 4),
	);
	return implode('|', $parts);
}

function epc_cs_pdf_line_items_equal(array $a, array $b)
{
	return epc_cs_pdf_line_item_hash($a) === epc_cs_pdf_line_item_hash($b);
}

/** Classify a PDF text line within the declaration line-item zone. */
function epc_cs_pdf_classify_line_item_line($line)
{
	$line = trim((string) $line);
	if ($line === '') {
		return 'empty';
	}
	if (epc_cs_pdf_is_hs_code($line)) {
		return 'hs_code';
	}
	if (epc_cs_pdf_is_country_code($line)) {
		return 'origin';
	}
	if (preg_match('/^[\d.]+\s*kg$/i', $line)) {
		return 'weight_gross';
	}
	if (preg_match('/^(kg|KG|pcs|PCS)$/i', $line)) {
		return 'unit';
	}
	if ($line === 'AED' || $line === 'USD' || $line === 'EUR' || $line === 'GBP') {
		return 'currency';
	}
	if (preg_match('/^1\.0+$/', $line)) {
		return 'currency_rate';
	}
	if (preg_match('/^(INS|DTY)$/i', $line)) {
		return 'income_type';
	}
	if (preg_match('/^\d+\.\d+$/', $line)) {
		$v = (float) $line;
		if ($v <= 100 && preg_match('/\.0+$/', $line)) {
			return 'duty_rate';
		}
		return 'number';
	}
	if (epc_cs_pdf_is_payment_line($line) || preg_match('/^\[/', $line)) {
		return 'stop';
	}
	if (preg_match('/^(Total Value|LOC:|Page \d+)/i', $line)) {
		return 'stop';
	}
	if (preg_match('/^[A-Z][A-Z0-9\s\-\/\.]{4,}$/', $line)
		&& !preg_match('/^(LAND|SEA|AIR|IMPORT|EXPORT|AED|INS)$/i', $line)
		&& stripos($line, 'Total Value') === false
		&& !preg_match('/^AE-\d+/i', $line)
		&& !preg_match('/^\d/', $line)) {
		return 'description';
	}
	if (preg_match('/^\d+(\.\d+)?$/', $line)) {
		return 'number';
	}
	return 'other';
}

/** Last contiguous run of lines matching a classifier type. */
function epc_cs_pdf_find_contiguous_run(array $lines, $type, $fromEnd = true)
{
	$runs = array();
	$current = array();
	$count = count($lines);
	for ($i = 0; $i < $count; $i++) {
		$idx = $fromEnd ? ($count - 1 - $i) : $i;
		$cls = epc_cs_pdf_classify_line_item_line($lines[$idx]);
		if ($cls === $type) {
			array_unshift($current, $lines[$idx]);
		} elseif (!empty($current)) {
			$runs[] = $current;
			$current = array();
		}
	}
	if (!empty($current)) {
		$runs[] = $current;
	}
	if (empty($runs)) {
		return array();
	}
	return $fromEnd ? $runs[0] : $runs[count($runs) - 1];
}

/** Find the last contiguous HS-code block in extracted PDF lines. */
function epc_cs_pdf_find_hs_code_block(array $lines)
{
	$count = count($lines);
	$bestStart = null;
	$bestEnd = null;
	$bestLen = 0;
	for ($i = 0; $i < $count; $i++) {
		if (epc_cs_pdf_classify_line_item_line($lines[$i]) !== 'hs_code') {
			continue;
		}
		$start = $i;
		while ($i < $count && epc_cs_pdf_classify_line_item_line($lines[$i]) === 'hs_code') {
			$i++;
		}
		$len = $i - $start;
		if ($len >= $bestLen) {
			$bestStart = $start;
			$bestEnd = $i - 1;
			$bestLen = $len;
		}
	}
	if ($bestStart === null) {
		return array('start' => null, 'end' => null, 'codes' => array());
	}
	$codes = array_slice($lines, $bestStart, $bestLen);
	$codes = epc_cs_pdf_dedupe_repeated_block($codes);
	$bestEnd = $bestStart + count($codes) - 1;
	return array('start' => $bestStart, 'end' => $bestEnd, 'codes' => array_values($codes));
}

/** Collect same-type runs walking backward from index (exclusive). */
function epc_cs_pdf_collect_runs_backward(array $lines, $startIdx, array $expectedTypes)
{
	$out = array();
	$numberRuns = array();
	$i = $startIdx - 1;
	foreach ($expectedTypes as $type) {
		$values = array();
		if ($i < 0) {
			if ($type === 'number') {
				$numberRuns[] = array();
			} else {
				$out[$type] = array();
			}
			continue;
		}
		while ($i >= 0) {
			$cls = epc_cs_pdf_classify_line_item_line($lines[$i]);
			if ($cls === 'stop' || $cls === 'other' || $cls === 'empty') {
				break;
			}
			if ($type === 'number') {
				if ($cls !== 'number') {
					break;
				}
			} elseif ($cls !== $type) {
				break;
			}
			$values[] = $lines[$i];
			$i--;
		}
		$values = array_reverse($values);
		if ($type === 'number') {
			$numberRuns[] = $values;
		} else {
			$out[$type] = $values;
		}
	}
	$out['_number_runs'] = $numberRuns;
	return $out;
}

/** Collect same-type runs walking forward from index (exclusive). */
function epc_cs_pdf_collect_runs_forward(array $lines, $startIdx, array $expectedTypes)
{
	$out = array();
	$i = $startIdx + 1;
	$count = count($lines);
	foreach ($expectedTypes as $type) {
		if ($i >= $count) {
			$out[$type] = array();
			continue;
		}
		$values = array();
		while ($i < $count) {
			$cls = epc_cs_pdf_classify_line_item_line($lines[$i]);
			if ($cls === 'stop' || $cls === 'other') {
				break;
			}
			if ($type === 'number') {
				if ($cls !== 'number') {
					break;
				}
			} elseif ($cls !== $type) {
				break;
			}
			$values[] = $lines[$i];
			$i++;
		}
		$out[$type] = $values;
	}
	return $out;
}

function epc_cs_pdf_normalize_weight_value($raw)
{
	return epc_cs_decimal_string(preg_replace('/\s*kg/i', '', trim((string) $raw)));
}

function epc_cs_pdf_take_n(array $values, $n, $fromEnd = false)
{
	$n = max(0, (int) $n);
	if ($n === 0 || empty($values)) {
		return array();
	}
	if (count($values) <= $n) {
		return array_values($values);
	}
	return $fromEnd ? array_values(array_slice($values, -$n)) : array_values(array_slice($values, 0, $n));
}

function epc_cs_pdf_pad_n(array $values, $n)
{
	$values = array_values($values);
	while (count($values) < $n) {
		$values[] = '';
	}
	return array_slice($values, 0, $n);
}

/** Boxes 32–37B: weights, units, then item quantities (row-aligned). */
function epc_cs_pdf_extract_lower_weight_table(array $lines, $hsEnd, $zoneEnd, $n)
{
	$slice = array_slice($lines, $hsEnd + 1, max(0, $zoneEnd - $hsEnd - 1));
	$weightsGross = array();
	$units = array();
	$quantities = array();
	$count = count($slice);
	$i = 0;
	while ($i < $count && count($weightsGross) < $n) {
		$line = $slice[$i];
		if (preg_match('/^([\d.]+)\s*kg$/i', $line, $m)) {
			$weightsGross[] = epc_cs_pdf_normalize_weight_value($m[1]);
			$i++;
			continue;
		}
		if (preg_match('/^([\d.]+)$/', $line, $m) && $i + 1 < $count && preg_match('/^kg$/i', $slice[$i + 1])) {
			$weightsGross[] = epc_cs_pdf_normalize_weight_value($m[1]);
			$i += 2;
			continue;
		}
		$i++;
	}
	while ($i < $count && count($units) < $n) {
		if (preg_match('/^(kg|pcs)$/i', $slice[$i])) {
			$units[] = strtoupper($slice[$i]);
		}
		$i++;
	}
	while ($i < $count && count($quantities) < $n) {
		$line = $slice[$i];
		if (preg_match('/^\d+(\.\d+)?$/', $line)) {
			$quantities[] = $line;
		} elseif (epc_cs_pdf_is_payment_line($line) || preg_match('/^\d{5,}$/', $line)) {
			break;
		}
		$i++;
	}
	return array($weightsGross, $units, $quantities);
}

/** Fallback scan for description/origin immediately above the HS block. */
function epc_cs_pdf_fill_upper_identity_fields(array $lines, $hsStart, $n, array $descriptions, array $origins)
{
	if (count($descriptions) >= $n && count($origins) >= $n) {
		return array(
			epc_cs_pdf_take_n($descriptions, $n),
			epc_cs_pdf_take_n($origins, $n),
		);
	}
	$desc = array();
	$orig = array();
	foreach (array_slice($lines, 0, $hsStart) as $line) {
		if (epc_cs_pdf_is_country_code($line)) {
			$orig[] = strtoupper($line);
		} elseif (epc_cs_pdf_is_description_line($line)) {
			$desc[] = $line;
		}
	}
	$desc = epc_cs_pdf_take_n(epc_cs_pdf_dedupe_repeated_block($desc), $n, true);
	$orig = epc_cs_pdf_take_n(epc_cs_pdf_dedupe_repeated_block($orig), $n, true);
	if (count($descriptions) < $n) {
		$descriptions = $desc;
	}
	if (count($origins) < $n) {
		$origins = $orig;
	}
	return array(
		epc_cs_pdf_take_n($descriptions, $n),
		epc_cs_pdf_take_n($origins, $n),
	);
}

function epc_cs_pdf_is_description_line($line)
{
	$line = trim((string) $line);
	return (bool) preg_match('/^[A-Z][A-Z0-9\s\-\/\.]{4,}$/', $line)
		&& !preg_match('/^(LAND|SEA|AIR|IMPORT|EXPORT|AED|INS)$/i', $line)
		&& !epc_cs_pdf_is_payment_line($line)
		&& stripos($line, 'Total Value') === false
		&& stripos($line, 'LOC:') === false
		&& !preg_match('/^AE-\d+/i', $line)
		&& !preg_match('/^\[/', $line)
		&& !preg_match('/^\d/', $line);
}

/** Index before footer (payment lines, Box 45, totals). */
function epc_cs_pdf_line_items_zone_end(array $lines)
{
	$n = count($lines);
	for ($i = 0; $i < $n; $i++) {
		$line = $lines[$i];
		if (epc_cs_pdf_is_payment_line($line)) {
			return $i;
		}
		if (preg_match('/^\[FOB\]/i', $line) || preg_match('/^(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:/i', $line)) {
			return $i;
		}
	}
	return $n;
}

/** Parse upper (boxes 22–31) and lower (32–37) blocks, merge by row index. */
function epc_cs_pdf_parse_line_item_blocks(array $lines, array $core = array())
{
	$zoneEnd = epc_cs_pdf_line_items_zone_end($lines);
	$lines = array_slice($lines, 0, $zoneEnd);

	$hsBlock = epc_cs_pdf_find_hs_code_block($lines);
	$hsCodes = $hsBlock['codes'] ?? array();
	if (empty($hsCodes)) {
		return array();
	}
	$n = count($hsCodes);
	$hsStart = $hsBlock['start'];
	$hsEnd = $hsBlock['end'];
	if ($hsStart === null) {
		return array();
	}

	$upperTypes = array('description', 'origin', 'number', 'currency', 'currency_rate', 'number', 'duty_rate', 'income_type', 'number');
	$upper = epc_cs_pdf_collect_runs_backward($lines, $hsStart, $upperTypes);

	$descriptions = epc_cs_pdf_dedupe_repeated_block(
		epc_cs_pdf_take_n($upper['description'] ?? array(), $n)
	);
	$origins = epc_cs_pdf_dedupe_repeated_block(
		epc_cs_pdf_take_n($upper['origin'] ?? array(), $n)
	);

	$numberRuns = $upper['_number_runs'] ?? array();
	$cifValues = array();
	$foreignValues = array();
	$totalDuties = array();
	if (count($numberRuns) >= 3) {
		$cifValues = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($numberRuns[0], $n));
		$foreignValues = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($numberRuns[1], $n));
		$totalDuties = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($numberRuns[2], $n));
	} elseif (count($numberRuns) === 2) {
		$foreignValues = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($numberRuns[0], $n));
		$cifValues = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($numberRuns[1], $n));
	} elseif (count($numberRuns) === 1) {
		$cifValues = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($numberRuns[0], $n));
	}

	$currencies = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($upper['currency'] ?? array(), $n));
	$currencyRates = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($upper['currency_rate'] ?? array(), $n));
	$incomeTypes = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($upper['income_type'] ?? array(), $n));
	$dutyRates = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($upper['duty_rate'] ?? array(), $n));
	$dutyRates = epc_cs_pdf_take_n($dutyRates, $n);

	list($weightsGross, $units, $quantities) = epc_cs_pdf_extract_lower_weight_table($lines, $hsEnd, $zoneEnd, $n);
	$weightsGross = epc_cs_pdf_dedupe_repeated_block($weightsGross);
	$weightsGross = epc_cs_pdf_take_n($weightsGross, $n);
	$units = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($units, $n));
	$quantities = epc_cs_pdf_dedupe_repeated_block(epc_cs_pdf_take_n($quantities, $n));

	list($descriptions, $origins) = epc_cs_pdf_fill_upper_identity_fields(
		$lines,
		$hsStart,
		$n,
		$descriptions,
		$origins
	);

	$pkgQtys = array();

	$upperRows = array();
	$lowerRows = array();
	for ($i = 0; $i < $n; $i++) {
		$upperRows[] = array(
			'hs_code' => $hsCodes[$i] ?? '',
			'description' => $descriptions[$i] ?? '',
			'country_of_origin' => isset($origins[$i]) ? strtoupper($origins[$i]) : '',
			'foreign_value' => isset($foreignValues[$i]) ? epc_cs_decimal_string($foreignValues[$i]) : '0',
			'currency' => $currencies[$i] ?? 'AED',
			'currency_rate' => isset($currencyRates[$i]) ? epc_cs_decimal_string($currencyRates[$i]) : '1',
			'cif_local_value' => isset($cifValues[$i]) ? epc_cs_decimal_string($cifValues[$i]) : '0',
			'duty_rate' => isset($dutyRates[$i]) ? epc_cs_decimal_string($dutyRates[$i]) : '0',
			'income_type' => $incomeTypes[$i] ?? '',
			'total_duty_aed' => isset($totalDuties[$i]) ? epc_cs_decimal_string($totalDuties[$i]) : '0',
		);
		$gross = $weightsGross[$i] ?? '0';
		$lowerRows[] = array(
			'packages_qty' => isset($pkgQtys[$i]) ? epc_cs_decimal_string($pkgQtys[$i]) : '0',
			'packages_type' => $core['package_type'] ?? '',
			'quantity' => isset($quantities[$i]) ? epc_cs_decimal_string($quantities[$i]) : '0',
			'unit' => isset($units[$i]) ? strtoupper($units[$i]) : 'KG',
			'weight_net' => $gross !== '0' ? $gross : '0',
			'weight_gross' => $gross !== '0' ? $gross : '0',
		);
	}

	$items = array();
	for ($i = 0; $i < $n; $i++) {
		$upperRow = $upperRows[$i] ?? array();
		$lowerRow = $lowerRows[$i] ?? array();
		if (trim((string) ($upperRow['hs_code'] ?? '')) === ''
			&& trim((string) ($upperRow['description'] ?? '')) === ''
			&& trim((string) ($upperRow['country_of_origin'] ?? '')) === '') {
			continue;
		}
		$item = array_merge(
			array(
				'line_number' => $i + 1,
				'weight' => $lowerRow['weight_gross'] ?? '0',
				'amount' => $upperRow['cif_local_value'] ?? '0',
				'volume' => 0,
				'volume_unit' => 'CBM',
				'aip_no' => '',
				'aip_duty' => '',
			),
			$upperRow,
			$lowerRow
		);
		if (epc_cs_parse_decimal($item['quantity']) <= 0 && epc_cs_parse_decimal($item['weight_gross']) > 0) {
			$item['quantity'] = $item['weight_gross'];
			$item['unit'] = 'KG';
		}
		if (epc_cs_parse_decimal($item['quantity']) <= 0) {
			$item['quantity'] = '1';
		}
		$item['weight'] = $item['weight_gross'];
		$item['amount'] = $item['cif_local_value'];
		$items[] = $item;
	}
	return $items;
}

/** Dedupe declaration line items — block-repeat trim then content-hash first-wins. */
function epc_cs_pdf_dedupe_line_items(array $items)
{
	if (empty($items)) {
		return $items;
	}
	$items = epc_cs_pdf_dedupe_repeated_block($items, 'epc_cs_pdf_line_items_equal');
	$seen = array();
	$out = array();
	foreach ($items as $item) {
		$h = epc_cs_pdf_line_item_hash($item);
		if ($h === '|||||0|0|0' || preg_match('/^\|+0(\|0)*$/', $h)) {
			continue;
		}
		if (isset($seen[$h])) {
			continue;
		}
		$seen[$h] = true;
		$out[] = $item;
	}
	foreach ($out as $i => &$item) {
		$item['line_number'] = $i + 1;
	}
	unset($item);
	return $out;
}

function epc_cs_pdf_parse_declaration_text($text, $typeHint = '', $opts = array())
{
	$text = epc_cs_pdf_normalize_text($text);
	$textValid = epc_cs_pdf_text_looks_valid($text);
	$allowPartial = !empty($opts['allow_partial']);
	$pdftotextMissing = empty($opts['pdftotext_available']);

	if ($text === '') {
		$msg = 'Could not extract any text from the PDF.';
		if ($pdftotextMissing) {
			$msg .= ' Server: pdftotext (poppler-utils) is not installed — ask your administrator to run the poppler install script, or fill the form manually.';
		} else {
			$msg .= ' Use a text-based declaration copy (not a scanned image), or fill the form manually.';
		}
		throw new Exception($msg);
	}

	$parseWarning = '';
	if (!$textValid) {
		$parseWarning = 'PDF text quality is low — some fields may be missing.';
		if ($pdftotextMissing) {
			$parseWarning .= ' Server: pdftotext missing (poppler-utils not installed).';
		}
		if (!$allowPartial) {
			$msg = 'Could not extract readable text from PDF. Use a text-based declaration copy (not a scan)';
			if ($pdftotextMissing) {
				$msg .= ', or ensure pdftotext is installed on the server (poppler-utils)';
			}
			$msg .= '. You can continue filling the form manually.';
			throw new Exception($msg);
		}
	}
	$lines = epc_cs_pdf_collect_lines($text);
	$flat = implode("\n", $lines);

	$boxes = array();
	$autofill = array();
	$core = array();
	$box45Lines = array();
	$box54Lines = array();
	$lineItems = array();

	$setBox = function ($key, $val) use (&$boxes, &$autofill) {
		$val = is_string($val) ? trim($val) : $val;
		if ($val === '' || $val === null) {
			return;
		}
		$boxes[$key] = $val;
		$autofill[$key] = true;
	};

	$declType = epc_cs_pdf_detect_declaration_type($flat, $typeHint);
	if ($declType !== '') {
		$setBox('box_03', $declType);
		$core['declaration_type'] = $declType;
		$core['category'] = epc_cs_category_for_type($declType);
	}

	if (preg_match('/\b(\d{3}-\d{8}-\d{2})\b/', $flat, $m)) {
		$setBox('box_01', $m[1]);
		$core['declaration_number'] = $m[1];
	}

	$dates = array();
	if (preg_match_all('/\b(\d{2}\/\d{2}\/\d{4})\b/', $flat, $dm)) {
		foreach ($dm[1] as $d) {
			$dates[] = epc_cs_pdf_parse_date($d);
		}
	}
	if (!empty($dates)) {
		$setBox('box_02', $dates[0]);
		$core['declaration_date'] = $dates[0];
		$core['entry_date'] = $dates[0];
		if (isset($dates[1])) {
			$setBox('box_55', $dates[1]);
		}
	}

	foreach ($lines as $line) {
		if (preg_match('/^(LAND|SEA|AIR)$/i', $line, $m)) {
			$setBox('box_04', strtoupper($m[1]));
			break;
		}
	}

	if (preg_match('/\b(AE-\d+\s*-\s*.+?)(?:\s*\(|$)/i', $flat, $m)) {
		$imp = trim($m[1]);
		$setBox('box_06', $imp);
		$core['company'] = $imp;
		if (preg_match('/AE-(\d+)/', $imp, $cm)) {
			$setBox('box_43', 'AE-' . $cm[1]);
		}
	}

	if (preg_match('/\b(\d+(?:\.\d+)?)\s*\(\s*kg\s*\)/i', $flat, $m)) {
		$setBox('box_10', $m[1] . ' kg');
		$core['gross_weight'] = epc_cs_parse_decimal($m[1]);
	}

	if (preg_match('/\b(\d+(?:\.\d+)?)\s+(CARTONS?|PALLETS?|PACKAGES?|BOXES?)\b/i', $flat, $m)) {
		$setBox('box_16', $m[1] . ' ' . strtoupper($m[2]));
		$core['package_detail'] = $m[1] . ' ' . strtoupper($m[2]);
		$core['package_type'] = strtoupper($m[2]);
	}

	if (preg_match('/\b(100\d{12})\b/', $flat, $m)) {
		$setBox('box_12a', $m[1]);
	}

	foreach ($lines as $line) {
		if (preg_match('/^\d{5,8}$/', $line) && empty($boxes['box_12'])) {
			$setBox('box_12', $line);
			break;
		}
	}

	if (preg_match('/\b(\d{10,15})\b/', $flat, $m)) {
		foreach ($lines as $line) {
			if ($line === $m[1] && strlen($line) >= 10 && !epc_cs_pdf_is_hs_code($line)) {
				$setBox('box_17', $line);
				$core['bl_number'] = $line;
				break;
			}
		}
	}
	if (empty($boxes['box_17']) && preg_match('/\b(\d{12})\b/', $flat, $m) && !epc_cs_pdf_is_hs_code($m[1])) {
		$setBox('box_17', $m[1]);
		$core['bl_number'] = $m[1];
	}

	foreach ($lines as $line) {
		if (preg_match('/^LOC:\s*(.+)$/i', $line, $m)) {
			$setBox('box_20', trim($m[1]));
			$core['port_of_exit'] = trim($m[1]);
		}
	}

	foreach ($lines as $line) {
		if (preg_match('/^\[FOB\]/i', $line) || preg_match('/\[(FOB|CIF|CFR|EXW|DAP|DDP|FCA)\]/i', $line)) {
			$box45Lines[] = $line;
		} elseif (preg_match('/^(?:Total\s+Value|Invoice\s+Value|Inv\.?\s*Value)\s*:/i', $line)) {
			$box45Lines[] = $line;
		} elseif (preg_match('/(?:Custom\s*Inspection|Inspection\s+Required|Physical\s+Inspection|CUST\s*INSP)\s*:?\s*(YES|NO|Y|N)/i', $line)) {
			$box45Lines[] = $line;
		} elseif (epc_cs_pdf_is_payment_line($line)) {
			$box54Lines[] = $line;
		}
	}
	$box45Lines = epc_cs_pdf_dedupe_strings($box45Lines);
	$box54Lines = epc_cs_pdf_dedupe_strings($box54Lines);
	if (!empty($box45Lines)) {
		$setBox('box_45', implode("\n", $box45Lines));
	}
	if (!empty($box54Lines)) {
		$setBox('box_54', implode(' | ', $box54Lines));
	}

	$box45Parsed = epc_cs_pdf_parse_box45_fields($flat, $lines, $box45Lines);
	if ($box45Parsed['invoice_term'] !== '') {
		$core['invoice_term'] = $box45Parsed['invoice_term'];
		$core['shipping_terms_inco'] = $box45Parsed['invoice_term'];
		$autofill['invoice_term'] = true;
	}
	if ($box45Parsed['invoice_value'] !== '') {
		$core['invoice_value'] = $box45Parsed['invoice_value'];
		$core['invoice_amount_aed'] = epc_cs_parse_decimal($box45Parsed['invoice_value']);
		$core['total_cost_aed'] = epc_cs_parse_decimal($box45Parsed['invoice_value']);
		$autofill['invoice_value'] = true;
	}
	if ($box45Parsed['customs_inspection_required'] !== '') {
		$core['customs_inspection_required'] = $box45Parsed['customs_inspection_required'];
		$core['custom_inspection'] = $box45Parsed['customs_inspection_required'];
		$autofill['customs_inspection_required'] = true;
		$autofill['custom_inspection'] = true;
	}

	foreach ($lines as $line) {
		if (preg_match('/^[a-z][a-z0-9._-]{4,}$/i', $line)
			&& !epc_cs_pdf_is_hs_code($line)
			&& !epc_cs_pdf_is_country_code($line)
			&& stripos($line, 'IMPORT') === false
			&& stripos($line, 'EXPORT') === false
			&& !preg_match('/^\d+(\.\d+)?$/', $line)) {
			$setBox('box_38', $line);
			break;
		}
	}

	$lineItems = epc_cs_pdf_dedupe_line_items(epc_cs_pdf_parse_line_item_blocks($lines, $core));

	if (!empty($lineItems)) {
		$netSum = 0;
		foreach ($lineItems as $li) {
			$netSum += (float) ($li['weight_net'] ?: $li['weight_gross']);
		}
		if ($netSum > 0) {
			$setBox('box_07', (string) $netSum);
			$core['net_weight'] = epc_cs_parse_decimal($netSum);
		}
	}

	$core['customs_emirate'] = 'DUBAI';
	$core['currency'] = 'AED';

	$result = array(
		'boxes' => $boxes,
		'box_45' => $box45Parsed,
		'box_45_lines' => $box45Lines,
		'box_54_lines' => $box54Lines,
		'line_items' => $lineItems,
		'core' => $core,
		'autofill_keys' => array_keys($autofill),
		'declaration_type' => $declType,
		'category' => epc_cs_category_for_type($declType),
		'text_preview' => mb_substr($flat, 0, 1200),
		'boxes_mapped' => count($boxes),
		'text_valid' => $textValid,
		'parse_warning' => $parseWarning,
		'partial' => (!$textValid && count($boxes) > 0),
	);

	$hasUsefulData = count($boxes) > 0
		|| !empty($core['declaration_number'])
		|| count($lineItems) > 0
		|| $declType !== '';

	if (!$textValid && !$hasUsefulData) {
		$msg = 'Could not map any declaration fields from this PDF.';
		if ($pdftotextMissing) {
			$msg .= ' Server: pdftotext missing — install poppler-utils on the server for best results.';
		}
		$msg .= ' Fill the form manually or try a text-based declaration copy.';
		throw new Exception($msg);
	}

	if (!$textValid && $hasUsefulData && $parseWarning === '') {
		$result['parse_warning'] = 'Partial import — verify all fields before saving.';
		$result['partial'] = true;
	}

	return $result;
}

function epc_cs_pdf_import_from_upload(array $file, $typeHint = '')
{
	if (empty($file['tmp_name']) || !is_uploaded_file($file['tmp_name'])) {
		throw new Exception('No PDF uploaded');
	}
	$name = (string) ($file['name'] ?? '');
	if (!preg_match('/\.pdf$/i', $name)) {
		throw new Exception('Upload must be a PDF file');
	}
	if ((int) ($file['size'] ?? 0) > 15 * 1024 * 1024) {
		throw new Exception('PDF exceeds 15 MB limit');
	}
	$binary = (string) file_get_contents($file['tmp_name']);
	if ($binary === '') {
		throw new Exception('Empty PDF file');
	}
	if (!function_exists('epc_uae_fta_extract_text_from_pdf_binary')) {
		$taxPath = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
		if (is_file($taxPath)) {
			require_once $taxPath;
		}
	}
	$text = epc_cs_pdf_extract_text($binary);
	$diag = epc_cs_pdf_pdftotext_diagnostics();
	$parsed = epc_cs_pdf_parse_declaration_text($text, $typeHint, array(
		'allow_partial' => true,
		'pdftotext_available' => !empty($diag['available']),
	));
	$parsed['pdftotext'] = $diag;
	return $parsed;
}

function epc_cs_merge_box_data_from_post(array $post)
{
	$defs = epc_cs_declaration_box_definitions();
	$boxes = array();
	foreach (array_keys($defs) as $key) {
		if (isset($post['boxes'][$key])) {
			$raw = trim((string) $post['boxes'][$key]);
			if ($raw !== '' && ($defs[$key]['type'] ?? '') === 'number') {
				$boxes[$key] = (string) epc_cs_parse_decimal($raw);
			} else {
				$boxes[$key] = $raw;
			}
		}
	}
	$box45 = array();
	if (!empty($post['box_45_lines']) && is_array($post['box_45_lines'])) {
		foreach ($post['box_45_lines'] as $ln) {
			$ln = trim((string) $ln);
			if ($ln !== '') {
				$box45[] = $ln;
			}
		}
	}
	$box54 = array();
	if (!empty($post['box_54_lines']) && is_array($post['box_54_lines'])) {
		foreach ($post['box_54_lines'] as $ln) {
			$ln = trim((string) $ln);
			if ($ln !== '') {
				$box54[] = $ln;
			}
		}
	}
	if (!empty($post['company'])) {
		$boxes['box_06'] = trim((string) $post['company']);
	}
	$box45Fields = array();
	foreach (array_keys(epc_cs_box45_field_definitions()) as $fk) {
		if (isset($post[$fk])) {
			$raw = trim((string) $post[$fk]);
			if ($raw !== '') {
				if ($fk === 'invoice_value') {
					$box45Fields[$fk] = epc_cs_decimal_string($raw);
				} else {
					$box45Fields[$fk] = $raw;
				}
			}
		}
	}
	return array(
		'boxes' => $boxes,
		'box_45_lines' => $box45,
		'box_54_lines' => $box54,
		'box_45_fields' => $box45Fields,
	);
}

function epc_cs_sync_box45_fields(array &$data)
{
	$fields = array();
	if (!empty($data['box_data']['box_45_fields']) && is_array($data['box_data']['box_45_fields'])) {
		$fields = $data['box_data']['box_45_fields'];
	}
	foreach (array_keys(epc_cs_box45_field_definitions()) as $fk) {
		if (isset($data[$fk]) && trim((string) $data[$fk]) !== '') {
			$raw = trim((string) $data[$fk]);
			$fields[$fk] = ($fk === 'invoice_value') ? epc_cs_decimal_string($raw) : $raw;
		}
	}
	if (!empty($fields['invoice_term'])) {
		$data['shipping_terms_inco'] = $fields['invoice_term'];
		$data['box_45_invoice_term'] = $fields['invoice_term'];
	}
	if (!empty($fields['invoice_value'])) {
		$data['invoice_amount_aed'] = epc_cs_parse_decimal($fields['invoice_value']);
		$data['total_cost_aed'] = epc_cs_parse_decimal($fields['invoice_value']);
		$data['box_45_invoice_value'] = epc_cs_parse_decimal($fields['invoice_value']);
	}
	if (!empty($fields['customs_inspection_required'])) {
		$data['custom_inspection'] = $fields['customs_inspection_required'];
		$data['box_45_customs_inspection'] = $fields['customs_inspection_required'];
	}
	if (!isset($data['box_data']) || !is_array($data['box_data'])) {
		$data['box_data'] = array();
	}
	$data['box_data']['box_45_fields'] = $fields;
}

function epc_cs_apply_parsed_to_form_data(array $parsed)
{
	$manual = epc_cs_pdf_manual_only_fields();
	$data = $parsed['core'] ?? array();
	foreach ($manual as $k) {
		unset($data[$k]);
	}
	$data['box_data'] = array(
		'boxes' => $parsed['boxes'] ?? array(),
		'box_45_lines' => $parsed['box_45_lines'] ?? array(),
		'box_54_lines' => $parsed['box_54_lines'] ?? array(),
		'box_45_fields' => $parsed['box_45'] ?? array(),
	);
	foreach ($parsed['box_45'] ?? array() as $fk => $fv) {
		if ($fv === '' || $fv === null) {
			continue;
		}
		$data[$fk] = $fv;
	}
	if (!empty($data['invoice_term'])) {
		$data['shipping_terms_inco'] = $data['invoice_term'];
	}
	if (!empty($data['invoice_value'])) {
		$data['invoice_amount_aed'] = epc_cs_parse_decimal($data['invoice_value']);
	}
	if (!empty($data['customs_inspection_required'])) {
		$data['custom_inspection'] = $data['customs_inspection_required'];
	}
	$data['pdf_autofill_keys'] = $parsed['autofill_keys'] ?? array();
	if (in_array('invoice_term', $data['pdf_autofill_keys'], true) && !in_array('shipping_terms_inco', $data['pdf_autofill_keys'], true)) {
		$data['pdf_autofill_keys'][] = 'shipping_terms_inco';
	}
	if (in_array('invoice_value', $data['pdf_autofill_keys'], true) && !in_array('invoice_amount_aed', $data['pdf_autofill_keys'], true)) {
		$data['pdf_autofill_keys'][] = 'invoice_amount_aed';
	}
	if (in_array('box_06', $data['pdf_autofill_keys'], true) && !in_array('company', $data['pdf_autofill_keys'], true)) {
		$data['pdf_autofill_keys'][] = 'company';
	}
	$data['line_items'] = $parsed['line_items'] ?? array();
	return $data;
}
