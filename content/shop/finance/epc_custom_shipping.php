<?php
/**
 * Custom & Shipping — customs declarations registry (Phase 1 from C&L Excel format).
 */
defined('_ASTEXE_') or die('No access');

function epc_cs_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_cs_categories_config()
{
	return array(
		'import' => array('label' => 'Import', 'icon' => 'fa-download', 'color' => '#2563eb'),
		'export' => array('label' => 'Export', 'icon' => 'fa-upload', 'color' => '#16a34a'),
		'transit' => array('label' => 'Transit', 'icon' => 'fa-exchange', 'color' => '#0891b2'),
		'temp_admission' => array('label' => 'Temporary Admission', 'icon' => 'fa-clock-o', 'color' => '#d97706'),
		'transfer' => array('label' => 'Transfer', 'icon' => 'fa-arrows-h', 'color' => '#7c3aed'),
		'lgp' => array('label' => 'LGP', 'icon' => 'fa-archive', 'color' => '#64748b'),
	);
}

/** Declaration types extracted from Excel "Declaration Type Data" sheet. */
function epc_cs_declaration_types_registry()
{
	return array(
		'import' => array(
			'Import to Local from ROW',
			'Import to local from FZ',
			'Import to Local from CW',
			'Import Statistical Declaration',
			'Import for Re Export to Local from ROW',
			'Import for Re Export to Local from FZ',
			'Import for Re Export to Local from CW',
			'Import for CW from ROW',
			'Import to CW from FZ',
			'Import to CW from Local (after temporary admission)',
			'Courier Import',
			'Import to Local After Temporary Admission',
		),
		'export' => array(
			'Export from Local to ROW',
			'Export from Local to FZ',
			'Export statisitical Declaration',
			'Temporary Export from local to ROW',
			'Temporay Export from local to FZ',
			'Export from CW to ROW',
			'Export from CW to FZ',
			'Re Export to ROW (after import for re export)',
			'Re Export to FZ (after import for Re Export)',
			'Return to FZ after temporary Admission',
			'Return to ROW after Temporary Admission',
			'Courier Export',
			'Goods Consumption within FZ',
		),
		'transit' => array(
			'Transit (ROW to ROW)',
			'FZ transit in',
			'FZ transit Out',
			'FZ transit in from GCC and other Emirates FZ and GCC local Market',
			'FZ Transit Between Dubai based FZ',
			'Courier Transit',
		),
		'temp_admission' => array(
			'Temporary Admission from ROW to Local',
			'Temporary Admission from FZ to Local',
			'Temporary Admission from CW to Local',
		),
		'transfer' => array(
			'Transfer of Cargo by Dubai Based CW',
			'Transfer within a FZ',
		),
	);
}

/** Phase 1 required fields (all non-LGP categories). */
function epc_cs_core_required_field_keys()
{
	return array('company', 'customs_emirate', 'declaration_type', 'entry_date', 'declaration_date');
}

/** Core ERP columns mirrored by declaration box fields — render once in PDF section only. */
function epc_cs_core_fields_mapped_to_boxes()
{
	return array(
		'declaration_number' => 'box_01',
		'declaration_date' => 'box_02',
		'bl_number' => 'box_17',
		'gross_weight' => 'box_10',
		'net_weight' => 'box_07',
		'package_type' => 'box_33',
		'package_detail' => 'box_16',
		'port_of_entry' => 'box_20',
		'port_of_exit' => 'box_46',
		'description_items' => 'box_23',
		'currency' => 'box_26',
		'invoice_amount_aed' => 'box_48',
	);
}

/** Box fields already shown as workflow/core inputs — hide from box grid to avoid duplicates. */
function epc_cs_form_skip_box_keys()
{
	return array('box_03', 'box_06');
}

/** Split form fields: manual (top), workflow (middle), box-mapped (PDF section only). */
function epc_cs_form_field_groups($category = '')
{
	$defs = epc_cs_field_definitions($category);
	$manualKeys = epc_cs_pdf_manual_only_fields();
	$boxMapped = array_keys(epc_cs_core_fields_mapped_to_boxes());
	$required = epc_cs_core_required_field_keys();
	$manual = array();
	$workflow = array();
	foreach ($defs as $key => $meta) {
		if (in_array($key, $boxMapped, true)) {
			continue;
		}
		if (in_array($key, $manualKeys, true)) {
			$manual[$key] = $meta;
		} else {
			$workflow[$key] = $meta;
		}
	}
	return array(
		'manual' => $manual,
		'workflow' => $workflow,
		'required' => $required,
	);
}

function epc_cs_sync_core_from_boxes(array &$data)
{
	if (!function_exists('epc_cs_merge_box_data_from_post')) {
		require_once __DIR__ . '/epc_custom_declaration_pdf_import.php';
	}
	$boxData = array();
	if (!empty($data['box_data']) && is_array($data['box_data'])) {
		$boxData = $data['box_data'];
	} elseif (function_exists('epc_cs_merge_box_data_from_post')) {
		$boxData = epc_cs_merge_box_data_from_post($data);
	}
	$boxes = is_array($boxData['boxes'] ?? null) ? $boxData['boxes'] : array();
	foreach (epc_cs_core_fields_mapped_to_boxes() as $coreKey => $boxKey) {
		if ((!isset($data[$coreKey]) || trim((string) $data[$coreKey]) === '') && !empty($boxes[$boxKey])) {
			$data[$coreKey] = $boxes[$boxKey];
		}
	}
	if ((!isset($data['declaration_type']) || trim((string) $data['declaration_type']) === '') && !empty($boxes['box_03'])) {
		$data['declaration_type'] = $boxes['box_03'];
	}
	if ((!isset($data['company']) || trim((string) $data['company']) === '') && !empty($boxes['box_06'])) {
		$data['company'] = $boxes['box_06'];
	}
	epc_cs_sync_company_box06($data);
}

function epc_cs_declaration_number_from_data(array $data)
{
	$num = trim((string) ($data['declaration_number'] ?? ''));
	if ($num !== '') {
		return $num;
	}
	if (!empty($data['boxes']['box_01'])) {
		return trim((string) $data['boxes']['box_01']);
	}
	if (!empty($data['box_data']['boxes']['box_01'])) {
		return trim((string) $data['box_data']['boxes']['box_01']);
	}
	return '';
}

function epc_cs_assert_unique_declaration_number(PDO $db, $declarationNumber, $excludeId = 0)
{
	$declarationNumber = trim((string) $declarationNumber);
	if ($declarationNumber === '') {
		return;
	}
	$existing = epc_cs_find_declaration_by_number($db, $declarationNumber);
	if ($existing && (int) ($existing['id'] ?? 0) !== (int) $excludeId) {
		if ((int) $excludeId <= 0) {
			throw new Exception('Declaration already saved — open from Reports to edit');
		}
		throw new Exception(
			'Declaration number ' . $declarationNumber . ' already exists (record #' . (int) $existing['id'] . '). Each customs declaration copy must be unique.'
		);
	}
}

function epc_cs_pdf_storage_root()
{
	$root = rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/\\') . '/content/files/epc_custom_shipping_pdfs';
	if (!is_dir($root)) {
		@mkdir($root, 0755, true);
	}
	$staging = $root . '/staging';
	if (!is_dir($staging)) {
		@mkdir($staging, 0755, true);
	}
	return $root;
}

function epc_cs_pdf_public_url($relativePath)
{
	$relativePath = trim(str_replace('\\', '/', (string) $relativePath), '/');
	if ($relativePath === '') {
		return '';
	}
	return '/content/files/epc_custom_shipping_pdfs/' . ltrim(str_replace('/content/files/epc_custom_shipping_pdfs/', '', $relativePath), '/');
}

function epc_cs_pdf_disk_path($relativePath)
{
	$relativePath = trim(str_replace('\\', '/', (string) $relativePath), '/');
	if ($relativePath === '') {
		return '';
	}
	if (strpos($relativePath, 'content/files/epc_custom_shipping_pdfs/') !== false) {
		return rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/\\') . '/' . ltrim($relativePath, '/');
	}
	return epc_cs_pdf_storage_root() . '/' . ltrim(str_replace('/content/files/epc_custom_shipping_pdfs/', '', $relativePath), '/');
}

function epc_cs_pdf_file_exists($relativePath)
{
	$full = epc_cs_pdf_disk_path($relativePath);
	return $full !== '' && is_file($full);
}

function epc_cs_stage_pdf_binary($binary, $originalName = '')
{
	if ($binary === '' || strncmp($binary, '%PDF', 4) !== 0) {
		throw new Exception('Invalid PDF file');
	}
	$token = function_exists('random_bytes') ? bin2hex(random_bytes(16)) : bin2hex(openssl_random_pseudo_bytes(16));
	$dir = epc_cs_pdf_storage_root() . '/staging';
	$file = $dir . '/' . $token . '.pdf';
	if (@file_put_contents($file, $binary) === false) {
		throw new Exception('Could not store PDF on server');
	}
	return array(
		'token' => $token,
		'preview_url' => '/content/files/epc_custom_shipping_pdfs/staging/' . $token . '.pdf',
		'file_name' => trim((string) $originalName) !== '' ? basename((string) $originalName) : ('declaration_' . $token . '.pdf'),
	);
}

function epc_cs_stage_pdf_upload(array $file)
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
	return epc_cs_stage_pdf_binary($binary, $name);
}

function epc_cs_commit_staged_pdf(PDO $db, $declarationId, $token, $originalName = '')
{
	$token = preg_replace('/[^a-f0-9]/', '', strtolower((string) $token));
	if ($token === '') {
		return '';
	}
	$staging = epc_cs_pdf_storage_root() . '/staging/' . $token . '.pdf';
	if (!is_file($staging)) {
		return '';
	}
	$declarationId = (int) $declarationId;
	$safeName = preg_replace('/[^a-zA-Z0-9._-]+/', '_', (string) $originalName);
	if ($safeName === '' || !preg_match('/\.pdf$/i', $safeName)) {
		$safeName = 'declaration_' . $declarationId . '.pdf';
	}
	$destName = 'decl_' . $declarationId . '_' . $safeName;
	if (!preg_match('/\.pdf$/i', $destName)) {
		$destName .= '.pdf';
	}
	$dest = epc_cs_pdf_storage_root() . '/' . $destName;
	if (!@rename($staging, $dest)) {
		if (!@copy($staging, $dest)) {
			throw new Exception('Could not attach PDF to declaration');
		}
		@unlink($staging);
	}
	$rel = '/content/files/epc_custom_shipping_pdfs/' . $destName;
	$db->prepare('UPDATE `epc_custom_shipping_declarations` SET `pdf_file_path` = ?, `pdf_file_name` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($rel, basename($destName), time(), $declarationId));
	return $rel;
}

/** Phase 1 form fields mapped from Excel column A labels. */
function epc_cs_field_definitions($category = '')
{
	$common = array(
		'company' => array('label' => 'Company', 'type' => 'text', 'required' => true),
		'customs_emirate' => array('label' => 'Customs (Emirates)', 'type' => 'select', 'required' => true, 'options' => array('DUBAI', 'SHARJAH', 'ABU DHABI', 'AJMAN', 'RAK', 'UAQ', 'FUJairah')),
		'declaration_type' => array('label' => 'Declaration type', 'type' => 'select', 'required' => true),
		'entry_date' => array('label' => 'Date', 'type' => 'date', 'required' => true),
		'declaration_date' => array('label' => 'Declaration date', 'type' => 'date', 'required' => true),
		'declaration_number' => array('label' => 'Declaration #', 'type' => 'text'),
		'bl_number' => array('label' => 'B/L #', 'type' => 'text'),
		'bl_date' => array('label' => 'B/L date', 'type' => 'date'),
		'srv_number' => array('label' => 'SRV #', 'type' => 'text'),
		'lc_dc_number' => array('label' => 'LC / DC number', 'type' => 'text'),
		'ld_po_number' => array('label' => 'LD / PO number', 'type' => 'text'),
		'supplier_detail' => array('label' => 'Supplier detail', 'type' => 'text'),
		'currency' => array('label' => 'Currency', 'type' => 'text'),
		'invoice_amount_aed' => array('label' => 'Invoice amount (AED)', 'type' => 'number'),
		'package_type' => array('label' => 'Package type', 'type' => 'text'),
		'package_detail' => array('label' => 'Package detail', 'type' => 'text'),
		'gross_weight' => array('label' => 'Gross weight shipment', 'type' => 'number'),
		'net_weight' => array('label' => 'Net weight shipment', 'type' => 'number'),
		'description_items' => array('label' => 'Description items', 'type' => 'textarea'),
		'port_of_entry' => array('label' => 'Port of entry', 'type' => 'text'),
		'port_of_exit' => array('label' => 'Port of exit', 'type' => 'text'),
		'remarks' => array('label' => 'Remarks', 'type' => 'textarea'),
		'shipping_terms_inco' => array('label' => 'Shipping terms (INCO)', 'type' => 'text'),
	);

	if ($category === 'lgp') {
		return array(
			'customer_ref_no' => array('label' => 'Customer ref no', 'type' => 'text', 'required' => true),
			'cargo_source' => array('label' => 'Cargo source', 'type' => 'text', 'required' => true),
			'goods_coming_from' => array('label' => 'Goods coming from', 'type' => 'text', 'required' => true),
			'warehouse_name' => array('label' => 'Warehouse name', 'type' => 'text', 'required' => true),
			'local_company' => array('label' => 'Local company', 'type' => 'text', 'required' => true),
			'purpose_of_entry' => array('label' => 'Purpose of entry', 'type' => 'text', 'required' => true),
			'warehouse_number' => array('label' => 'Warehouse number', 'type' => 'text', 'required' => true),
			'documents_ref_no' => array('label' => 'Documents ref no', 'type' => 'text'),
			'packing_list' => array('label' => 'Packing list', 'type' => 'text', 'required' => true),
			'commercial_invoice' => array('label' => 'Commercial invoice', 'type' => 'text', 'required' => true),
			'hs_code' => array('label' => 'HS code', 'type' => 'text'),
			'goods_description' => array('label' => 'Goods description', 'type' => 'textarea'),
			'package_type' => array('label' => 'Package type', 'type' => 'text'),
			'quantity' => array('label' => 'Quantity', 'type' => 'number'),
			'weight_kgs' => array('label' => 'Weight (kgs)', 'type' => 'number'),
			'volume_cbm' => array('label' => 'Volume (CBM)', 'type' => 'number'),
			'value_aed' => array('label' => 'Value (AED)', 'type' => 'number'),
			'remarks' => array('label' => 'Remarks', 'type' => 'textarea'),
		);
	}

	$extra = array();
	if ($category === 'import') {
		$extra = array(
			'supplier_code_customs' => array('label' => 'Supplier code (customs)', 'type' => 'text'),
			'custom_inspection' => array('label' => 'Custom inspection', 'type' => 'select', 'options' => array('', 'YES', 'NO')),
			'd365_po_reference' => array('label' => 'ERP PO reference', 'type' => 'text'),
		);
	} elseif ($category === 'export' || $category === 'transit' || $category === 'temp_admission' || $category === 'transfer') {
		$extra = array(
			'import_reexport_declaration_ref' => array('label' => 'Import (re-export) declaration ref #', 'type' => 'text'),
			'document_expiry_date' => array('label' => 'Document expiry date', 'type' => 'date'),
			'd365_so_reference' => array('label' => 'ERP SO reference', 'type' => 'text'),
			'customer_ref' => array('label' => 'Customer ref', 'type' => 'text'),
			'customer_country' => array('label' => 'Customer country', 'type' => 'text'),
		);
	}

	return array_merge($common, $extra);
}

function epc_cs_category_for_type($declarationType)
{
	$declarationType = trim((string) $declarationType);
	foreach (epc_cs_declaration_types_registry() as $cat => $types) {
		if (in_array($declarationType, $types, true)) {
			return $cat;
		}
	}
	return 'import';
}

function epc_cs_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_custom_shipping_declarations` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
		`category` VARCHAR(32) NOT NULL DEFAULT 'import',
		`declaration_type` VARCHAR(191) NOT NULL DEFAULT '',
		`status` VARCHAR(32) NOT NULL DEFAULT 'draft',
		`company` VARCHAR(255) NOT NULL DEFAULT '',
		`customs_emirate` VARCHAR(64) NOT NULL DEFAULT '',
		`entry_date` DATE NULL,
		`declaration_date` DATE NULL,
		`declaration_number` VARCHAR(64) NOT NULL DEFAULT '',
		`bl_number` VARCHAR(64) NOT NULL DEFAULT '',
		`bl_date` DATE NULL,
		`srv_number` VARCHAR(64) NOT NULL DEFAULT '',
		`lc_dc_number` VARCHAR(128) NOT NULL DEFAULT '',
		`ld_po_number` VARCHAR(128) NOT NULL DEFAULT '',
		`supplier_detail` VARCHAR(255) NOT NULL DEFAULT '',
		`currency` VARCHAR(16) NOT NULL DEFAULT 'AED',
		`invoice_amount_aed` DECIMAL(18,2) NOT NULL DEFAULT 0,
		`total_cost_aed` DECIMAL(18,2) NOT NULL DEFAULT 0,
		`remarks` TEXT,
		`field_data` LONGTEXT,
		`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
		`created_by` INT UNSIGNED NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `idx_cs_category` (`category`),
		KEY `idx_cs_status` (`status`),
		KEY `idx_cs_declaration_date` (`declaration_date`),
		KEY `idx_cs_declaration_type` (`declaration_type`(64))
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	epc_cs_ensure_line_items_schema($db);
	if (is_file(__DIR__ . '/epc_custom_declaration_pdf_import.php')) {
		require_once __DIR__ . '/epc_custom_declaration_pdf_import.php';
		epc_cs_ensure_box_schema($db);
	}
}

function epc_cs_line_item_unit_options()
{
	return array('PCS', 'KG', 'SET', 'PAIR', 'M', 'L', 'BOX', 'CTN');
}

function epc_cs_line_item_volume_unit_options()
{
	return array('CBM', 'CFT', 'L');
}

function epc_cs_ensure_line_items_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_custom_shipping_declaration_items` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
		`declaration_id` INT UNSIGNED NOT NULL,
		`line_number` INT UNSIGNED NOT NULL DEFAULT 1,
		`hs_code` VARCHAR(32) NOT NULL DEFAULT '',
		`country_of_origin` VARCHAR(64) NOT NULL DEFAULT '',
		`description` VARCHAR(512) NOT NULL DEFAULT '',
		`quantity` DECIMAL(18,4) NOT NULL DEFAULT 0,
		`unit` VARCHAR(16) NOT NULL DEFAULT 'PCS',
		`volume` DECIMAL(18,4) NOT NULL DEFAULT 0,
		`volume_unit` VARCHAR(16) NOT NULL DEFAULT 'CBM',
		`amount` DECIMAL(18,2) NOT NULL DEFAULT 0,
		`weight` DECIMAL(18,4) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `idx_cs_item_declaration` (`declaration_id`),
		KEY `idx_cs_item_line` (`declaration_id`, `line_number`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");
}

function epc_cs_normalize_line_item(array $row, $lineNumber = 0)
{
	$lineNumber = (int) ($row['line_number'] ?? $row['line_no'] ?? $lineNumber);
	if ($lineNumber < 1) {
		$lineNumber = 1;
	}
	$unit = strtoupper(trim((string) ($row['unit'] ?? 'PCS')));
	$units = epc_cs_line_item_unit_options();
	if (!in_array($unit, $units, true)) {
		$unit = 'PCS';
	}
	$volUnit = strtoupper(trim((string) ($row['volume_unit'] ?? 'CBM')));
	$volUnits = epc_cs_line_item_volume_unit_options();
	if (!in_array($volUnit, $volUnits, true)) {
		$volUnit = 'CBM';
	}
	return array(
		'line_number' => $lineNumber,
		'hs_code' => trim((string) ($row['hs_code'] ?? '')),
		'country_of_origin' => trim((string) ($row['country_of_origin'] ?? '')),
		'description' => trim((string) ($row['description'] ?? '')),
		'quantity' => (float) ($row['quantity'] ?? 0),
		'unit' => $unit,
		'volume' => (float) ($row['volume'] ?? 0),
		'volume_unit' => $volUnit,
		'amount' => (float) ($row['amount'] ?? 0),
		'weight' => (float) ($row['weight'] ?? 0),
		'foreign_value' => (float) ($row['foreign_value'] ?? 0),
		'currency' => trim((string) ($row['currency'] ?? 'AED')),
		'currency_rate' => (float) ($row['currency_rate'] ?? 0),
		'cif_local_value' => (float) ($row['cif_local_value'] ?? 0),
		'duty_rate' => (float) ($row['duty_rate'] ?? 0),
		'income_type' => trim((string) ($row['income_type'] ?? '')),
		'total_duty_aed' => (float) ($row['total_duty_aed'] ?? 0),
		'packages_qty' => (float) ($row['packages_qty'] ?? 0),
		'packages_type' => trim((string) ($row['packages_type'] ?? '')),
		'weight_net' => (float) ($row['weight_net'] ?? 0),
		'weight_gross' => (float) ($row['weight_gross'] ?? 0),
		'aip_no' => trim((string) ($row['aip_no'] ?? '')),
		'aip_duty' => trim((string) ($row['aip_duty'] ?? '')),
	);
}

function epc_cs_parse_line_items_input(array $data)
{
	$raw = array();
	if (!empty($data['line_items_json'])) {
		$decoded = json_decode((string) $data['line_items_json'], true);
		if (is_array($decoded)) {
			$raw = $decoded;
		}
	} elseif (!empty($data['line_items']) && is_array($data['line_items'])) {
		$raw = $data['line_items'];
	}
	$items = array();
	$lineNo = 0;
	foreach ($raw as $row) {
		if (!is_array($row)) {
			continue;
		}
		$lineNo++;
		$norm = epc_cs_normalize_line_item($row, $lineNo);
		if ($norm['hs_code'] === '' && $norm['country_of_origin'] === '' && $norm['quantity'] <= 0
			&& $norm['volume'] <= 0 && $norm['amount'] <= 0 && $norm['description'] === '') {
			continue;
		}
		$norm['line_number'] = $lineNo;
		$items[] = $norm;
	}
	return $items;
}

function epc_cs_validate_line_items(array $items)
{
	if (empty($items)) {
		throw new Exception('Add at least one declaration line item (HS code, country of origin, quantity).');
	}
	$errors = array();
	foreach ($items as $item) {
		$n = (int) $item['line_number'];
		if ($item['hs_code'] === '') {
			$errors[] = 'Line ' . $n . ': HS code is required';
		}
		if ($item['country_of_origin'] === '') {
			$errors[] = 'Line ' . $n . ': country of origin is required';
		}
		if ($item['quantity'] <= 0) {
			$errors[] = 'Line ' . $n . ': quantity must be greater than zero';
		}
	}
	if (!empty($errors)) {
		throw new Exception(implode('; ', $errors));
	}
}

function epc_cs_get_declaration_items(PDO $db, $declarationId)
{
	epc_cs_ensure_line_items_schema($db);
	$st = $db->prepare(
		'SELECT `id`, `declaration_id`, `line_number`, `hs_code`, `country_of_origin`, `description`,
		 `quantity`, `unit`, `volume`, `volume_unit`, `amount`, `weight`,
		 `foreign_value`, `currency`, `currency_rate`, `cif_local_value`, `duty_rate`, `income_type`,
		 `total_duty_aed`, `packages_qty`, `packages_type`, `weight_net`, `weight_gross`, `aip_no`, `aip_duty`
		 FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` = ? ORDER BY `line_number` ASC, `id` ASC'
	);
	$st->execute(array((int) $declarationId));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	$out = array();
	foreach ($rows as $row) {
		$out[] = epc_cs_normalize_line_item($row, (int) $row['line_number']);
	}
	return $out;
}

function epc_cs_legacy_line_items_from_row(array $row)
{
	$fd = is_array($row['field_data'] ?? null) ? $row['field_data'] : array();
	$hs = trim((string) ($fd['hs_code'] ?? ''));
	$origin = trim((string) ($fd['country_of_origin'] ?? ''));
	$qty = (float) ($fd['quantity'] ?? 0);
	$vol = (float) ($fd['volume_cbm'] ?? 0);
	$amt = (float) ($fd['value_aed'] ?? 0);
	$weight = (float) ($fd['weight_kgs'] ?? 0);
	$desc = trim((string) ($fd['goods_description'] ?? ($fd['description_items'] ?? '')));
	if ($hs === '' && $origin === '' && $qty <= 0 && $vol <= 0 && $amt <= 0) {
		return array();
	}
	return array(epc_cs_normalize_line_item(array(
		'line_number' => 1,
		'hs_code' => $hs,
		'country_of_origin' => $origin,
		'description' => $desc,
		'quantity' => $qty > 0 ? $qty : 1,
		'unit' => 'PCS',
		'volume' => $vol,
		'volume_unit' => 'CBM',
		'amount' => $amt,
		'weight' => $weight,
	), 1));
}

function epc_cs_save_declaration_items(PDO $db, $declarationId, array $items)
{
	epc_cs_ensure_line_items_schema($db);
	$declarationId = (int) $declarationId;
	$db->prepare('DELETE FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` = ?')->execute(array($declarationId));
	$ins = $db->prepare(
		'INSERT INTO `epc_custom_shipping_declaration_items`
		(`declaration_id`, `line_number`, `hs_code`, `country_of_origin`, `description`, `quantity`, `unit`, `volume`, `volume_unit`, `amount`, `weight`,
		 `foreign_value`, `currency`, `currency_rate`, `cif_local_value`, `duty_rate`, `income_type`, `total_duty_aed`,
		 `packages_qty`, `packages_type`, `weight_net`, `weight_gross`, `aip_no`, `aip_duty`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	);
	$lineNo = 0;
	foreach ($items as $item) {
		$lineNo++;
		$item = epc_cs_normalize_line_item($item, $lineNo);
		$ins->execute(array(
			$declarationId,
			$lineNo,
			$item['hs_code'],
			$item['country_of_origin'],
			$item['description'],
			$item['quantity'],
			$item['unit'],
			$item['volume'],
			$item['volume_unit'],
			$item['amount'],
			$item['weight'],
			$item['foreign_value'],
			$item['currency'],
			$item['currency_rate'],
			$item['cif_local_value'],
			$item['duty_rate'],
			$item['income_type'],
			$item['total_duty_aed'],
			$item['packages_qty'],
			$item['packages_type'],
			$item['weight_net'],
			$item['weight_gross'],
			$item['aip_no'],
			$item['aip_duty'],
		));
	}
}

function epc_cs_attach_item_counts(PDO $db, array &$rows)
{
	if (empty($rows)) {
		return;
	}
	epc_cs_ensure_line_items_schema($db);
	$ids = array();
	foreach ($rows as $r) {
		$ids[] = (int) ($r['id'] ?? 0);
	}
	$ids = array_values(array_filter(array_unique($ids)));
	if (empty($ids)) {
		return;
	}
	$placeholders = implode(',', array_fill(0, count($ids), '?'));
	$st = $db->prepare(
		'SELECT `declaration_id`, COUNT(*) AS cnt FROM `epc_custom_shipping_declaration_items`
		 WHERE `declaration_id` IN (' . $placeholders . ') GROUP BY `declaration_id`'
	);
	$st->execute($ids);
	$counts = array();
	while ($c = $st->fetch(PDO::FETCH_ASSOC)) {
		$counts[(int) $c['declaration_id']] = (int) $c['cnt'];
	}
	foreach ($rows as &$row) {
		$row['item_count'] = (int) ($counts[(int) ($row['id'] ?? 0)] ?? 0);
	}
	unset($row);
}

function epc_cs_dashboard_counts(PDO $db, $dateFrom = 0, $dateTo = 0)
{
	epc_cs_ensure_schema($db);
	$cats = epc_cs_categories_config();
	$out = array('total' => 0, 'draft' => 0, 'submitted' => 0, 'cleared' => 0, 'by_category' => array());
	foreach (array_keys($cats) as $cat) {
		$out['by_category'][$cat] = 0;
	}
	$sql = 'SELECT `category`, `status`, COUNT(*) AS cnt FROM `epc_custom_shipping_declarations` WHERE 1=1';
	$params = array();
	if ($dateFrom > 0) {
		$sql .= ' AND (`entry_date` IS NULL OR `entry_date` >= ?)';
		$params[] = date('Y-m-d', $dateFrom);
	}
	if ($dateTo > 0) {
		$sql .= ' AND (`entry_date` IS NULL OR `entry_date` <= ?)';
		$params[] = date('Y-m-d', $dateTo);
	}
	$sql .= ' GROUP BY `category`, `status`';
	$st = $db->prepare($sql);
	$st->execute($params);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$cat = (string) ($row['category'] ?? '');
		$status = (string) ($row['status'] ?? 'draft');
		$cnt = (int) ($row['cnt'] ?? 0);
		$out['total'] += $cnt;
		if (isset($out['by_category'][$cat])) {
			$out['by_category'][$cat] += $cnt;
		}
		if (isset($out[$status])) {
			$out[$status] += $cnt;
		}
	}
	return $out;
}

function epc_cs_list_declarations(PDO $db, array $filters = array(), $limit = 100)
{
	epc_cs_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_custom_shipping_declarations` WHERE 1=1';
	$params = array();
	if (!empty($filters['category'])) {
		$sql .= ' AND `category` = ?';
		$params[] = (string) $filters['category'];
	}
	if (!empty($filters['status'])) {
		$sql .= ' AND `status` = ?';
		$params[] = (string) $filters['status'];
	}
	if (!empty($filters['declaration_type'])) {
		$sql .= ' AND `declaration_type` = ?';
		$params[] = (string) $filters['declaration_type'];
	}
	if (!empty($filters['from'])) {
		$sql .= ' AND (`entry_date` IS NULL OR `entry_date` >= ?)';
		$params[] = (string) $filters['from'];
	}
	if (!empty($filters['to'])) {
		$sql .= ' AND (`entry_date` IS NULL OR `entry_date` <= ?)';
		$params[] = (string) $filters['to'];
	}
	if (!empty($filters['company'])) {
		$sql .= ' AND `company` LIKE ?';
		$params[] = '%' . (string) $filters['company'] . '%';
	}
	if (!empty($filters['customs_emirate'])) {
		$sql .= ' AND `customs_emirate` = ?';
		$params[] = (string) $filters['customs_emirate'];
	}
	if (!empty($filters['q'])) {
		$q = '%' . (string) $filters['q'] . '%';
		$sql .= ' AND (`declaration_number` LIKE ? OR `company` LIKE ? OR `supplier_detail` LIKE ? OR `srv_number` LIKE ? OR `bl_number` LIKE ? OR `ld_po_number` LIKE ?)';
		$params[] = $q;
		$params[] = $q;
		$params[] = $q;
		$params[] = $q;
		$params[] = $q;
		$params[] = $q;
	}
	$sql .= ' ORDER BY `id` DESC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$row) {
		$row['field_data'] = json_decode((string) ($row['field_data'] ?? ''), true) ?: array();
		$row['box_data'] = json_decode((string) ($row['box_data'] ?? ''), true) ?: array();
		$row['pdf_autofill_keys'] = json_decode((string) ($row['pdf_autofill_keys'] ?? ''), true) ?: array();
	}
	unset($row);
	epc_cs_attach_item_counts($db, $rows);
	return $rows;
}

function epc_cs_get_declaration(PDO $db, $id)
{
	epc_cs_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_custom_shipping_declarations` WHERE `id` = ? LIMIT 1');
	$st->execute(array((int) $id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	$row['field_data'] = json_decode((string) ($row['field_data'] ?? ''), true) ?: array();
	$row['box_data'] = json_decode((string) ($row['box_data'] ?? ''), true) ?: array();
	$row['pdf_autofill_keys'] = json_decode((string) ($row['pdf_autofill_keys'] ?? ''), true) ?: array();
	$row['line_items'] = epc_cs_get_declaration_items($db, (int) $row['id']);
	if (empty($row['line_items'])) {
		$row['line_items'] = epc_cs_legacy_line_items_from_row($row);
	}
	$row['item_count'] = count($row['line_items']);
	return $row;
}

function epc_cs_validate_declaration(array $data)
{
	$category = (string) ($data['category'] ?? 'import');
	if ($category === 'lgp') {
		$required = array('customer_ref_no', 'cargo_source', 'goods_coming_from', 'warehouse_name', 'local_company', 'purpose_of_entry', 'warehouse_number', 'packing_list', 'commercial_invoice');
	} else {
		$required = epc_cs_core_required_field_keys();
	}
	$missing = array();
	foreach ($required as $key) {
		if (!isset($data[$key]) || trim((string) $data[$key]) === '') {
			$missing[] = $key;
		}
	}
	if (!empty($missing)) {
		throw new Exception('Required fields missing: ' . implode(', ', $missing));
	}
	$types = epc_cs_declaration_types_registry();
	if ($category !== 'lgp') {
		$declType = trim((string) ($data['declaration_type'] ?? ''));
		if ($declType === '' || empty($types[$category]) || !in_array($declType, $types[$category], true)) {
			throw new Exception('Invalid declaration type for category');
		}
	}
}

function epc_cs_save_declaration(PDO $db, array $data, $userId = 0)
{
	if (!function_exists('epc_cs_merge_box_data_from_post')) {
		require_once __DIR__ . '/epc_custom_declaration_pdf_import.php';
	}
	epc_cs_sync_core_from_boxes($data);
	$declNo = epc_cs_declaration_number_from_data($data);
	if ($declNo !== '') {
		$data['declaration_number'] = $declNo;
	}
	epc_cs_assert_unique_declaration_number($db, $declNo, (int) ($data['id'] ?? 0));
	epc_cs_validate_declaration($data);
	$lineItems = epc_cs_parse_line_items_input($data);
	epc_cs_validate_line_items($lineItems);
	$category = (string) ($data['category'] ?? 'import');
	$fieldDefs = epc_cs_field_definitions($category);
	$coreCols = array(
		'company', 'customs_emirate', 'declaration_type', 'entry_date', 'declaration_date',
		'declaration_number', 'bl_number', 'bl_date', 'srv_number', 'lc_dc_number', 'ld_po_number',
		'supplier_detail', 'currency', 'invoice_amount_aed', 'total_cost_aed', 'remarks',
	);
	$extra = array();
	foreach (array_keys($fieldDefs) as $key) {
		if (in_array($key, $coreCols, true)) {
			continue;
		}
		if (array_key_exists($key, $data)) {
			$extra[$key] = $data[$key];
		}
	}
	$boxData = array();
	$pdfAutofill = array();
	if (!empty($data['box_data']) && is_array($data['box_data'])) {
		$boxData = $data['box_data'];
	} elseif (function_exists('epc_cs_merge_box_data_from_post')) {
		$boxData = epc_cs_merge_box_data_from_post($data);
	}
	if (!empty($data['pdf_autofill_keys']) && is_array($data['pdf_autofill_keys'])) {
		$pdfAutofill = $data['pdf_autofill_keys'];
	}
	$now = time();
	$id = (int) ($data['id'] ?? 0);
	$status = (string) ($data['status'] ?? 'draft');
	if (!in_array($status, array('draft', 'submitted', 'cleared'), true)) {
		$status = 'draft';
	}
	$params = array(
		$category,
		trim((string) ($data['declaration_type'] ?? ($category === 'lgp' ? 'LGP entry' : ''))),
		$status,
		trim((string) ($data['company'] ?? ($data['local_company'] ?? ''))),
		trim((string) ($data['customs_emirate'] ?? 'DUBAI')),
		!empty($data['entry_date']) ? (string) $data['entry_date'] : null,
		!empty($data['declaration_date']) ? (string) $data['declaration_date'] : null,
		($declNo !== '' ? $declNo : null),
		trim((string) ($data['bl_number'] ?? '')),
		!empty($data['bl_date']) ? (string) $data['bl_date'] : null,
		trim((string) ($data['srv_number'] ?? '')),
		trim((string) ($data['lc_dc_number'] ?? '')),
		trim((string) ($data['ld_po_number'] ?? '')),
		trim((string) ($data['supplier_detail'] ?? '')),
		trim((string) ($data['currency'] ?? 'AED')),
		(float) ($data['invoice_amount_aed'] ?? ($data['value_aed'] ?? 0)),
		(float) ($data['total_cost_aed'] ?? ($data['value_aed'] ?? 0)),
		trim((string) ($data['remarks'] ?? '')),
		json_encode($extra, JSON_UNESCAPED_UNICODE),
		json_encode($boxData, JSON_UNESCAPED_UNICODE),
		json_encode($pdfAutofill, JSON_UNESCAPED_UNICODE),
	);
	if ($id > 0) {
		$params[] = $now;
		$params[] = $id;
		$db->prepare(
			'UPDATE `epc_custom_shipping_declarations` SET
			 `category`=?, `declaration_type`=?, `status`=?, `company`=?, `customs_emirate`=?, `entry_date`=?, `declaration_date`=?,
			 `declaration_number`=?, `bl_number`=?, `bl_date`=?, `srv_number`=?, `lc_dc_number`=?, `ld_po_number`=?,
			 `supplier_detail`=?, `currency`=?, `invoice_amount_aed`=?, `total_cost_aed`=?, `remarks`=?, `field_data`=?, `box_data`=?, `pdf_autofill_keys`=?, `updated_at`=?
			 WHERE `id`=?'
		)->execute($params);
		epc_cs_save_declaration_items($db, $id, $lineItems);
		epc_cs_attach_pdf_to_declaration($db, $id, $data);
		return $id;
	}
	$params[] = $now;
	$params[] = $now;
	$params[] = (int) $userId;
	$db->prepare(
		'INSERT INTO `epc_custom_shipping_declarations`
		(`category`, `declaration_type`, `status`, `company`, `customs_emirate`, `entry_date`, `declaration_date`,
		 `declaration_number`, `bl_number`, `bl_date`, `srv_number`, `lc_dc_number`, `ld_po_number`,
		 `supplier_detail`, `currency`, `invoice_amount_aed`, `total_cost_aed`, `remarks`, `field_data`, `box_data`, `pdf_autofill_keys`,
		 `created_at`, `updated_at`, `created_by`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute($params);
	$newId = (int) $db->lastInsertId();
	epc_cs_save_declaration_items($db, $newId, $lineItems);
	epc_cs_attach_pdf_to_declaration($db, $newId, $data);
	return $newId;
}

function epc_cs_attach_pdf_to_declaration(PDO $db, $declarationId, array $data)
{
	$declarationId = (int) $declarationId;
	if ($declarationId <= 0) {
		return '';
	}
	if (!empty($data['pdf_token'])) {
		return epc_cs_commit_staged_pdf($db, $declarationId, (string) $data['pdf_token'], (string) ($data['pdf_file_name'] ?? ''));
	}
	return '';
}

function epc_cs_submit_declaration(PDO $db, $id)
{
	$row = epc_cs_get_declaration($db, $id);
	if (!$row) {
		throw new Exception('Declaration not found');
	}
	$db->prepare('UPDATE `epc_custom_shipping_declarations` SET `status` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array('submitted', time(), (int) $id));
	return true;
}

function epc_cs_resolve_erp_url()
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	if (!function_exists('epc_erp_configure_portal_urls')) {
		require_once __DIR__ . '/epc_erp_access.php';
	}
	$urls = epc_erp_configure_portal_urls('cp');
	$cached = (string) ($urls['erpUrl'] ?? ('/' . ($GLOBALS['DP_Config']->backend_dir ?? 'cp') . '/shop/finance/erp'));
	return $cached;
}

function epc_cs_tab_url($erpUrl, $from, $to, array $extra = array())
{
	$q = array(
		'area' => 'custom_shipping',
		'tab' => 'custom_shipping',
		'from' => $from,
		'to' => $to,
	);
	foreach ($extra as $k => $v) {
		if ($v !== '' && $v !== null) {
			$q[$k] = $v;
		}
	}
	if (!function_exists('epc_erp_shell_url_query')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	}
	$shellQ = epc_erp_shell_url_query();
	$url = $erpUrl . '?' . http_build_query($q);
	if ($shellQ !== '') {
		$url .= '&' . $shellQ;
	}
	return $url;
}

function epc_cs_reports_management_url($from = '', $to = '')
{
	if ($from === '') {
		$from = date('Y-m-01');
	}
	if ($to === '') {
		$to = date('Y-m-d');
	}
	return epc_cs_tab_url(epc_cs_resolve_erp_url(), $from, $to, array(
		'cs_view' => 'reports',
		'cs_report' => 'search_results',
	));
}

function epc_cs_redirect_after_save($id, $from = '', $to = '')
{
	return epc_cs_reports_management_url($from, $to);
}

function epc_cs_status_badge_html($status)
{
	$status = (string) $status;
	$class = 'epc-scp-badge epc-scp-badge--normal';
	if ($status === 'cleared') {
		$class = 'epc-scp-badge';
	} elseif ($status === 'submitted') {
		$class = 'epc-scp-badge epc-scp-badge--tenant';
	} elseif ($status === 'draft') {
		$class = 'epc-scp-badge epc-scp-badge--normal';
	}
	return '<span class="' . epc_cs_h($class) . '">' . epc_cs_h(ucfirst($status ?: 'draft')) . '</span>';
}

function epc_cs_declaration_row_actions_html($erpUrl, $from, $to, array $row)
{
	$id = (int) ($row['id'] ?? 0);
	if ($id <= 0) {
		return '';
	}
	$editUrl = epc_cs_tab_url($erpUrl, $from, $to, array('cs_view' => 'form', 'cs_id' => $id));
	$html = '<div class="epc-scp-actions-cell epc-cs-row-actions" role="group">';
	$html .= '<a class="btn btn-xs btn-primary" href="' . epc_cs_h($editUrl) . '" title="Edit declaration"><i class="fa fa-pencil"></i> Edit</a>';
	$pdfPath = trim((string) ($row['pdf_file_path'] ?? ''));
	$pdfUrl = ($pdfPath !== '') ? epc_cs_pdf_public_url($pdfPath) : '';
	$pdfName = trim((string) ($row['pdf_file_name'] ?? ('declaration_' . $id . '.pdf')));
	$hasPdf = ($pdfPath !== '' && $pdfUrl !== '' && epc_cs_pdf_file_exists($pdfPath));
	if ($pdfPath !== '' && !$hasPdf) {
		$pdfBtnTitle = 'PDF record exists but file is missing on server';
	} elseif ($hasPdf) {
		$pdfBtnTitle = 'View PDF copy';
	} else {
		$pdfBtnTitle = 'No PDF attached';
	}
	$pdfBtnClass = $hasPdf ? 'btn-info' : 'btn-default';
	$html .= '<button type="button" class="btn btn-xs ' . $pdfBtnClass . ' epc-cs-pdf-view-btn" data-has-pdf="' . ($hasPdf ? '1' : '0') . '" data-pdf-url="' . epc_cs_h($pdfUrl) . '" data-pdf-name="' . epc_cs_h($pdfName) . '" title="' . epc_cs_h($pdfBtnTitle) . '"><i class="fa fa-file-pdf-o"></i> PDF</button>';
	$html .= '<button type="button" class="btn btn-xs btn-danger epc-cs-delete-btn" data-id="' . $id . '" data-category="' . epc_cs_h((string) ($row['category'] ?? '')) . '" title="Delete declaration"><i class="fa fa-trash"></i></button>';
	$html .= '</div>';
	return $html;
}

function epc_cs_delete_declaration(PDO $db, $id)
{
	$id = (int) $id;
	if ($id <= 0) {
		throw new Exception('Invalid declaration id');
	}
	$row = epc_cs_get_declaration($db, $id);
	if (!$row) {
		throw new Exception('Declaration not found');
	}
	epc_cs_ensure_line_items_schema($db);
	$db->prepare('DELETE FROM `epc_custom_shipping_declaration_items` WHERE `declaration_id` = ?')->execute(array($id));
	if (!empty($row['pdf_file_path'])) {
		$rel = ltrim(str_replace('\\', '/', (string) $row['pdf_file_path']), '/');
		if (strpos($rel, 'content/files/epc_custom_shipping_pdfs/') !== false) {
			$full = rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/\\') . '/' . ltrim($rel, '/');
			if (is_file($full)) {
				@unlink($full);
			}
		}
	}
	$db->prepare('DELETE FROM `epc_custom_shipping_declarations` WHERE `id` = ?')->execute(array($id));
	return true;
}

function epc_cs_configure_urls()
{
	$erpBase = epc_cs_resolve_erp_url();
	return array(
		'erpUrl' => $erpBase,
		'csTabUrl' => $erpBase . '?area=custom_shipping&tab=custom_shipping',
		'csGuideUrl' => $erpBase . '/custom-shipping-guide',
	);
}

/** Re-export declaration types (Excel Export + Import-for-re-export sheets). */
function epc_cs_reexport_import_types()
{
	return array(
		'Import for Re Export to Local from ROW',
		'Import for Re Export to Local from FZ',
		'Import for Re Export to Local from CW',
	);
}

function epc_cs_reexport_export_types()
{
	return array(
		'Re Export to ROW (after import for re export)',
		'Re Export to FZ (after import for Re Export)',
	);
}

function epc_cs_report_definitions()
{
	return array(
		'search_results' => array(
			'label' => 'Declaration search',
			'icon' => 'fa-search',
			'desc' => 'Filter declarations by date, type, category, company, emirate, and status — Excel Search results sheet.',
			'status' => 'live',
		),
		'cost_summary' => array(
			'label' => 'Cost summary',
			'icon' => 'fa-calculator',
			'desc' => 'Aggregate invoice and line-item amounts by company and category — partial Excel Cost Summary.',
			'status' => 'partial',
		),
		'duty_report' => array(
			'label' => 'Duty report',
			'icon' => 'fa-percent',
			'desc' => 'Declaration totals with HS codes and origins — duty paid/payable columns stubbed until Phase 3.',
			'status' => 'partial',
		),
		'reexport_tracking' => array(
			'label' => 'Re-export tracking',
			'icon' => 'fa-refresh',
			'desc' => 'Import-for-re-export and re-export declarations with link to original import ref #.',
			'status' => 'live',
		),
		'document_expiry' => array(
			'label' => 'Document expiry',
			'icon' => 'fa-calendar-times-o',
			'desc' => 'Declarations with document expiry date — overdue and upcoming within 30 days.',
			'status' => 'live',
		),
	);
}

function epc_cs_report_stubs()
{
	return epc_cs_report_definitions();
}

function epc_cs_report_filters_from_request(array $get = array())
{
	return array(
		'category' => trim((string) ($get['cs_category'] ?? $get['category'] ?? '')),
		'status' => trim((string) ($get['cs_status'] ?? $get['status'] ?? '')),
		'declaration_type' => trim((string) ($get['cs_type'] ?? $get['declaration_type'] ?? '')),
		'company' => trim((string) ($get['cs_company'] ?? $get['company'] ?? '')),
		'customs_emirate' => trim((string) ($get['cs_emirate'] ?? $get['customs_emirate'] ?? '')),
		'from' => trim((string) ($get['from'] ?? '')),
		'to' => trim((string) ($get['to'] ?? '')),
		'q' => trim((string) ($get['cs_q'] ?? $get['q'] ?? '')),
		'expiry_days' => max(0, (int) ($get['cs_expiry_days'] ?? 30)),
	);
}

function epc_cs_field_data_val(array $row, $key, $default = '')
{
	if (isset($row[$key]) && $row[$key] !== '' && $row[$key] !== null) {
		return $row[$key];
	}
	$fd = is_array($row['field_data'] ?? null) ? $row['field_data'] : array();
	return isset($fd[$key]) && $fd[$key] !== '' ? $fd[$key] : $default;
}

function epc_cs_find_declaration_by_number(PDO $db, $declarationNumber)
{
	$declarationNumber = trim((string) $declarationNumber);
	if ($declarationNumber === '') {
		return null;
	}
	epc_cs_ensure_schema($db);
	$st = $db->prepare('SELECT `id`, `declaration_number`, `declaration_type`, `entry_date` FROM `epc_custom_shipping_declarations` WHERE `declaration_number` = ? ORDER BY `id` DESC LIMIT 1');
	$st->execute(array($declarationNumber));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_cs_report_search_results(PDO $db, array $filters = array(), $limit = 500)
{
	return epc_cs_list_declarations($db, $filters, $limit);
}

function epc_cs_report_cost_summary(PDO $db, array $filters = array())
{
	epc_cs_ensure_schema($db);
	epc_cs_ensure_line_items_schema($db);
	$sql = 'SELECT d.`category`, d.`company`, d.`customs_emirate`,
		COUNT(DISTINCT d.`id`) AS decl_count,
		COALESCE(SUM(d.`invoice_amount_aed`), 0) AS sum_invoice_aed,
		COALESCE(SUM(d.`total_cost_aed`), 0) AS sum_total_cost_aed,
		COALESCE(SUM(i.`amount`), 0) AS sum_line_amount_aed,
		COALESCE(SUM(i.`quantity`), 0) AS sum_line_qty
		FROM `epc_custom_shipping_declarations` d
		LEFT JOIN `epc_custom_shipping_declaration_items` i ON i.`declaration_id` = d.`id`
		WHERE 1=1';
	$params = array();
	if (!empty($filters['category'])) {
		$sql .= ' AND d.`category` = ?';
		$params[] = (string) $filters['category'];
	}
	if (!empty($filters['status'])) {
		$sql .= ' AND d.`status` = ?';
		$params[] = (string) $filters['status'];
	}
	if (!empty($filters['from'])) {
		$sql .= ' AND (d.`entry_date` IS NULL OR d.`entry_date` >= ?)';
		$params[] = (string) $filters['from'];
	}
	if (!empty($filters['to'])) {
		$sql .= ' AND (d.`entry_date` IS NULL OR d.`entry_date` <= ?)';
		$params[] = (string) $filters['to'];
	}
	if (!empty($filters['company'])) {
		$sql .= ' AND d.`company` LIKE ?';
		$params[] = '%' . (string) $filters['company'] . '%';
	}
	if (!empty($filters['customs_emirate'])) {
		$sql .= ' AND d.`customs_emirate` = ?';
		$params[] = (string) $filters['customs_emirate'];
	}
	$sql .= ' GROUP BY d.`category`, d.`company`, d.`customs_emirate` ORDER BY sum_invoice_aed DESC, d.`company` ASC';
	$st = $db->prepare($sql);
	$st->execute($params);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	$totals = array('decl_count' => 0, 'sum_invoice_aed' => 0, 'sum_total_cost_aed' => 0, 'sum_line_amount_aed' => 0);
	foreach ($rows as $r) {
		$totals['decl_count'] += (int) $r['decl_count'];
		$totals['sum_invoice_aed'] += (float) $r['sum_invoice_aed'];
		$totals['sum_total_cost_aed'] += (float) $r['sum_total_cost_aed'];
		$totals['sum_line_amount_aed'] += (float) $r['sum_line_amount_aed'];
	}
	return array('rows' => $rows, 'totals' => $totals);
}

function epc_cs_report_duty_lines(PDO $db, array $filters = array(), $limit = 500)
{
	epc_cs_ensure_schema($db);
	epc_cs_ensure_line_items_schema($db);
	$sql = 'SELECT d.*, i.`line_number`, i.`hs_code`, i.`country_of_origin`, i.`description` AS item_description,
		i.`quantity`, i.`amount` AS line_amount
		FROM `epc_custom_shipping_declarations` d
		INNER JOIN `epc_custom_shipping_declaration_items` i ON i.`declaration_id` = d.`id`
		WHERE 1=1';
	$params = array();
	if (!empty($filters['category'])) {
		$sql .= ' AND d.`category` = ?';
		$params[] = (string) $filters['category'];
	}
	if (!empty($filters['status'])) {
		$sql .= ' AND d.`status` = ?';
		$params[] = (string) $filters['status'];
	}
	if (!empty($filters['from'])) {
		$sql .= ' AND (d.`entry_date` IS NULL OR d.`entry_date` >= ?)';
		$params[] = (string) $filters['from'];
	}
	if (!empty($filters['to'])) {
		$sql .= ' AND (d.`entry_date` IS NULL OR d.`entry_date` <= ?)';
		$params[] = (string) $filters['to'];
	}
	if (!empty($filters['company'])) {
		$sql .= ' AND d.`company` LIKE ?';
		$params[] = '%' . (string) $filters['company'] . '%';
	}
	$sql .= ' ORDER BY d.`entry_date` DESC, d.`id` DESC, i.`line_number` ASC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$row) {
		$row['field_data'] = json_decode((string) ($row['field_data'] ?? ''), true) ?: array();
	}
	unset($row);
	return $rows;
}

function epc_cs_report_reexport_tracking(PDO $db, array $filters = array(), $limit = 300)
{
	$importTypes = epc_cs_reexport_import_types();
	$exportTypes = epc_cs_reexport_export_types();
	$allTypes = array_merge($importTypes, $exportTypes);
	$placeholders = implode(',', array_fill(0, count($allTypes), '?'));
	epc_cs_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_custom_shipping_declarations` WHERE `declaration_type` IN (' . $placeholders . ')';
	$params = $allTypes;
	if (!empty($filters['from'])) {
		$sql .= ' AND (`entry_date` IS NULL OR `entry_date` >= ?)';
		$params[] = (string) $filters['from'];
	}
	if (!empty($filters['to'])) {
		$sql .= ' AND (`entry_date` IS NULL OR `entry_date` <= ?)';
		$params[] = (string) $filters['to'];
	}
	if (!empty($filters['company'])) {
		$sql .= ' AND `company` LIKE ?';
		$params[] = '%' . (string) $filters['company'] . '%';
	}
	if (!empty($filters['status'])) {
		$sql .= ' AND `status` = ?';
		$params[] = (string) $filters['status'];
	}
	$sql .= ' ORDER BY `entry_date` DESC, `id` DESC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	$out = array();
	foreach ($rows as $row) {
		$row['field_data'] = json_decode((string) ($row['field_data'] ?? ''), true) ?: array();
		$importRef = trim((string) epc_cs_field_data_val($row, 'import_reexport_declaration_ref'));
		$row['import_ref'] = $importRef;
		$row['import_link'] = null;
		if ($importRef !== '') {
			$row['import_link'] = epc_cs_find_declaration_by_number($db, $importRef);
		}
		$row['is_reexport'] = in_array($row['declaration_type'], $exportTypes, true);
		$row['is_import_for_reexport'] = in_array($row['declaration_type'], $importTypes, true);
		$row['document_expiry_date'] = epc_cs_field_data_val($row, 'document_expiry_date');
		$out[] = $row;
	}
	return $out;
}

function epc_cs_report_document_expiry(PDO $db, array $filters = array(), $limit = 300)
{
	$rows = epc_cs_list_declarations($db, array_merge($filters, array()), $limit * 2);
	$days = max(1, (int) ($filters['expiry_days'] ?? 30));
	$today = date('Y-m-d');
	$horizon = date('Y-m-d', strtotime('+' . $days . ' days'));
	$out = array();
	foreach ($rows as $row) {
		$exp = trim((string) epc_cs_field_data_val($row, 'document_expiry_date'));
		if ($exp === '') {
			continue;
		}
		$row['document_expiry_date'] = $exp;
		$row['days_until_expiry'] = (int) floor((strtotime($exp . ' 00:00:00') - strtotime($today . ' 00:00:00')) / 86400);
		if ($row['days_until_expiry'] < 0) {
			$row['expiry_status'] = 'overdue';
		} elseif ($row['days_until_expiry'] <= $days) {
			$row['expiry_status'] = 'upcoming';
		} else {
			continue;
		}
		$out[] = $row;
		if (count($out) >= $limit) {
			break;
		}
	}
	usort($out, function ($a, $b) {
		return strcmp((string) $a['document_expiry_date'], (string) $b['document_expiry_date']);
	});
	return $out;
}

function epc_cs_report_csv_rows($reportKey, array $data, PDO $db = null)
{
	$categories = epc_cs_categories_config();
	switch ($reportKey) {
		case 'search_results':
			$headers = array('ID', 'Category', 'Type', 'Company', 'Emirate', 'Entry date', 'Decl #', 'SRV #', 'Supplier', 'Status', 'Invoice AED', 'Total cost AED', 'Items');
			$rows = array();
			foreach ($data as $r) {
				$cat = $categories[$r['category'] ?? '']['label'] ?? ($r['category'] ?? '');
				$rows[] = array(
					(int) ($r['id'] ?? 0),
					$cat,
					$r['declaration_type'] ?? '',
					$r['company'] ?? '',
					$r['customs_emirate'] ?? '',
					$r['entry_date'] ?? '',
					$r['declaration_number'] ?? '',
					$r['srv_number'] ?? '',
					$r['supplier_detail'] ?? '',
					$r['status'] ?? '',
					number_format((float) ($r['invoice_amount_aed'] ?? 0), 2, '.', ''),
					number_format((float) ($r['total_cost_aed'] ?? 0), 2, '.', ''),
					(int) ($r['item_count'] ?? 0),
				);
			}
			return array($headers, $rows);
		case 'cost_summary':
			$headers = array('Category', 'Company', 'Emirate', 'Declarations', 'Invoice AED', 'Total cost AED', 'Line items AED', 'Line qty');
			$rows = array();
			foreach (($data['rows'] ?? array()) as $r) {
				$cat = $categories[$r['category'] ?? '']['label'] ?? ($r['category'] ?? '');
				$rows[] = array(
					$cat,
					$r['company'] ?? '',
					$r['customs_emirate'] ?? '',
					(int) ($r['decl_count'] ?? 0),
					number_format((float) ($r['sum_invoice_aed'] ?? 0), 2, '.', ''),
					number_format((float) ($r['sum_total_cost_aed'] ?? 0), 2, '.', ''),
					number_format((float) ($r['sum_line_amount_aed'] ?? 0), 2, '.', ''),
					$r['sum_line_qty'] ?? 0,
				);
			}
			return array($headers, $rows);
		case 'duty_report':
			$headers = array('Decl ID', 'Decl #', 'Company', 'Type', 'Entry date', 'Line', 'HS code', 'Origin', 'Qty', 'Line AED', 'Invoice AED', 'Total cost AED', 'Duty paid', 'Duty payable', 'Duty payable date');
			$rows = array();
			foreach ($data as $r) {
				$rows[] = array(
					(int) ($r['id'] ?? 0),
					$r['declaration_number'] ?? '',
					$r['company'] ?? '',
					$r['declaration_type'] ?? '',
					$r['entry_date'] ?? '',
					(int) ($r['line_number'] ?? 0),
					$r['hs_code'] ?? '',
					$r['country_of_origin'] ?? '',
					$r['quantity'] ?? 0,
					number_format((float) ($r['line_amount'] ?? 0), 2, '.', ''),
					number_format((float) ($r['invoice_amount_aed'] ?? 0), 2, '.', ''),
					number_format((float) ($r['total_cost_aed'] ?? 0), 2, '.', ''),
					epc_cs_field_data_val($r, 'custom_duty_paid', ''),
					epc_cs_field_data_val($r, 'custom_duty_payable', ''),
					epc_cs_field_data_val($r, 'duty_payable_date', ''),
				);
			}
			return array($headers, $rows);
		case 'reexport_tracking':
			$headers = array('ID', 'Flow', 'Type', 'Company', 'Entry date', 'Decl #', 'Import ref #', 'Linked import ID', 'Expiry date', 'Status', 'Invoice AED');
			$rows = array();
			foreach ($data as $r) {
				$flow = !empty($r['is_reexport']) ? 'Re-export' : 'Import for re-export';
				$rows[] = array(
					(int) ($r['id'] ?? 0),
					$flow,
					$r['declaration_type'] ?? '',
					$r['company'] ?? '',
					$r['entry_date'] ?? '',
					$r['declaration_number'] ?? '',
					$r['import_ref'] ?? '',
					!empty($r['import_link']['id']) ? (int) $r['import_link']['id'] : '',
					$r['document_expiry_date'] ?? '',
					$r['status'] ?? '',
					number_format((float) ($r['invoice_amount_aed'] ?? 0), 2, '.', ''),
				);
			}
			return array($headers, $rows);
		case 'document_expiry':
			$headers = array('ID', 'Category', 'Company', 'Type', 'Decl #', 'Expiry date', 'Days', 'Status', 'Emirate');
			$rows = array();
			foreach ($data as $r) {
				$cat = $categories[$r['category'] ?? '']['label'] ?? ($r['category'] ?? '');
				$rows[] = array(
					(int) ($r['id'] ?? 0),
					$cat,
					$r['company'] ?? '',
					$r['declaration_type'] ?? '',
					$r['declaration_number'] ?? '',
					$r['document_expiry_date'] ?? '',
					(int) ($r['days_until_expiry'] ?? 0),
					$r['expiry_status'] ?? '',
					$r['customs_emirate'] ?? '',
				);
			}
			return array($headers, $rows);
		default:
			return array(array('Error'), array(array('Unknown report')));
	}
}

function epc_cs_report_run(PDO $db, $reportKey, array $filters = array())
{
	switch ($reportKey) {
		case 'search_results':
			return epc_cs_report_search_results($db, $filters);
		case 'cost_summary':
			return epc_cs_report_cost_summary($db, $filters);
		case 'duty_report':
			return epc_cs_report_duty_lines($db, $filters);
		case 'reexport_tracking':
			return epc_cs_report_reexport_tracking($db, $filters);
		case 'document_expiry':
			return epc_cs_report_document_expiry($db, $filters);
		default:
			return array();
	}
}

function epc_cs_csv_output($filename, array $headers, array $rows)
{
	header('Content-Type: text/csv; charset=utf-8');
	header('Content-Disposition: attachment; filename="' . preg_replace('/[^a-zA-Z0-9._-]/', '_', $filename) . '"');
	$out = fopen('php://output', 'w');
	if ($out) {
		fprintf($out, chr(0xEF) . chr(0xBB) . chr(0xBF));
		fputcsv($out, $headers);
		foreach ($rows as $row) {
			fputcsv($out, $row);
		}
		fclose($out);
	}
	exit;
}

function epc_cs_handle_report_csv_export(PDO $db, $reportKey, array $filters = array())
{
	$defs = epc_cs_report_definitions();
	if (!isset($defs[$reportKey])) {
		http_response_code(404);
		exit('Unknown report');
	}
	$data = epc_cs_report_run($db, $reportKey, $filters);
	list($headers, $rows) = epc_cs_report_csv_rows($reportKey, $data, $db);
	$fname = 'custom-shipping-' . $reportKey . '-' . date('Y-m-d') . '.csv';
	epc_cs_csv_output($fname, $headers, $rows);
}
