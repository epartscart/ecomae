<?php
/**
 * ERP Print Designer — tenant-configurable document print templates.
 *
 * Allows tenants to customise the print layout for vouchers, invoices, POs,
 * SOs, delivery notes, receipt vouchers, payment vouchers, and reports.
 * Each template stores:
 *   - Company logo position & size
 *   - Header/footer HTML (with merge fields)
 *   - Column selection & order
 *   - Font, colours, margins
 *   - Page size (A4/Letter/A5)
 *   - Industry-specific sections (e.g., jewellery weight columns)
 *
 * Templates are stored per-company in epc_erp_print_templates.
 * The renderer (epc_erp_print_render) reads the active template + document
 * data and produces an HTML/CSS page ready for window.print().
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';

/* ─── Schema ─── */

function epc_erp_print_designer_ensure_schema(PDO $db): void
{
	static $done = false;
	if ($done) return;
	$done = true;

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_print_templates` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`doc_type` varchar(40) NOT NULL COMMENT 'invoice,sales_order,purchase_order,payment_voucher,receipt_voucher,journal_voucher,delivery_note,quotation,report',
		`name` varchar(120) NOT NULL DEFAULT 'Default',
		`is_default` tinyint(1) NOT NULL DEFAULT 0,
		`page_size` varchar(10) NOT NULL DEFAULT 'A4',
		`orientation` enum('portrait','landscape') NOT NULL DEFAULT 'portrait',
		`margin_top` int(11) NOT NULL DEFAULT 15 COMMENT 'mm',
		`margin_bottom` int(11) NOT NULL DEFAULT 15,
		`margin_left` int(11) NOT NULL DEFAULT 10,
		`margin_right` int(11) NOT NULL DEFAULT 10,
		`font_family` varchar(60) NOT NULL DEFAULT 'Arial, sans-serif',
		`font_size` int(11) NOT NULL DEFAULT 11 COMMENT 'pt',
		`primary_color` varchar(10) NOT NULL DEFAULT '#1565c0',
		`secondary_color` varchar(10) NOT NULL DEFAULT '#4a6a7a',
		`logo_position` enum('left','center','right','none') NOT NULL DEFAULT 'left',
		`logo_max_height` int(11) NOT NULL DEFAULT 60 COMMENT 'px',
		`header_html` text COMMENT 'HTML with merge fields like {{company_name}}, {{trn}}, {{address}}',
		`footer_html` text COMMENT 'HTML with merge fields like {{page}}, {{total_pages}}, {{printed_by}}',
		`body_columns` text COMMENT 'JSON array of column configs: [{key,label,width,align}]',
		`show_terms` tinyint(1) NOT NULL DEFAULT 1,
		`terms_html` text,
		`show_bank_details` tinyint(1) NOT NULL DEFAULT 1,
		`bank_details_html` text,
		`show_signature_line` tinyint(1) NOT NULL DEFAULT 1,
		`signature_labels` varchar(255) NOT NULL DEFAULT 'Prepared by,Approved by,Received by',
		`show_qr_code` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'QR code with doc reference',
		`show_barcode` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'Barcode with doc number',
		`custom_css` text,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_doctype` (`doc_type`,`is_default`),
		KEY `x_active` (`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tenant-configurable document print templates'");
}

/* ─── Seed default templates ─── */

function epc_erp_print_designer_seed_defaults(PDO $db): void
{
	epc_erp_print_designer_ensure_schema($db);
	$check = $db->query("SELECT COUNT(*) FROM epc_erp_print_templates")->fetchColumn();
	if ((int)$check > 0) return;

	$docTypes = array(
		'invoice' => array(
			'name' => 'Tax Invoice — Standard',
			'header_html' => '<div style="text-align:center;font-size:18px;font-weight:bold;">TAX INVOICE</div><div style="text-align:center;font-size:12px;color:#666;">{{company_name}} — TRN: {{trn}}</div>',
			'footer_html' => '<div style="border-top:1px solid #ccc;padding-top:6px;font-size:9px;text-align:center;">{{company_name}} | {{address}} | Tel: {{phone}} | Email: {{email}}</div>',
			'body_columns' => json_encode(array(
				array('key' => 'line_no', 'label' => '#', 'width' => '5%', 'align' => 'center'),
				array('key' => 'description', 'label' => 'Description', 'width' => '35%', 'align' => 'left'),
				array('key' => 'qty', 'label' => 'Qty', 'width' => '8%', 'align' => 'center'),
				array('key' => 'unit_price', 'label' => 'Unit Price', 'width' => '12%', 'align' => 'right'),
				array('key' => 'discount', 'label' => 'Discount', 'width' => '10%', 'align' => 'right'),
				array('key' => 'vat_rate', 'label' => 'VAT %', 'width' => '8%', 'align' => 'center'),
				array('key' => 'vat_amount', 'label' => 'VAT', 'width' => '10%', 'align' => 'right'),
				array('key' => 'line_total', 'label' => 'Total', 'width' => '12%', 'align' => 'right'),
			)),
		),
		'sales_order' => array(
			'name' => 'Sales Order — Standard',
			'header_html' => '<div style="text-align:center;font-size:18px;font-weight:bold;">SALES ORDER</div><div style="text-align:center;font-size:12px;color:#666;">{{company_name}}</div>',
			'footer_html' => '<div style="font-size:9px;text-align:center;">{{company_name}} | {{address}}</div>',
			'body_columns' => json_encode(array(
				array('key' => 'line_no', 'label' => '#', 'width' => '5%', 'align' => 'center'),
				array('key' => 'description', 'label' => 'Item / Description', 'width' => '40%', 'align' => 'left'),
				array('key' => 'qty', 'label' => 'Qty', 'width' => '10%', 'align' => 'center'),
				array('key' => 'unit_price', 'label' => 'Rate', 'width' => '15%', 'align' => 'right'),
				array('key' => 'line_total', 'label' => 'Amount', 'width' => '15%', 'align' => 'right'),
			)),
		),
		'purchase_order' => array(
			'name' => 'Purchase Order — Standard',
			'header_html' => '<div style="text-align:center;font-size:18px;font-weight:bold;">PURCHASE ORDER</div><div style="text-align:center;font-size:12px;color:#666;">{{company_name}}</div>',
			'footer_html' => '<div style="font-size:9px;text-align:center;">{{company_name}} | {{address}}</div>',
			'body_columns' => json_encode(array(
				array('key' => 'line_no', 'label' => '#', 'width' => '5%', 'align' => 'center'),
				array('key' => 'description', 'label' => 'Item / Description', 'width' => '40%', 'align' => 'left'),
				array('key' => 'qty', 'label' => 'Qty', 'width' => '10%', 'align' => 'center'),
				array('key' => 'unit_price', 'label' => 'Rate', 'width' => '15%', 'align' => 'right'),
				array('key' => 'line_total', 'label' => 'Amount', 'width' => '15%', 'align' => 'right'),
			)),
		),
		'payment_voucher' => array(
			'name' => 'Payment Voucher — Standard',
			'header_html' => '<div style="text-align:center;font-size:18px;font-weight:bold;">PAYMENT VOUCHER</div>',
			'footer_html' => '',
			'body_columns' => json_encode(array(
				array('key' => 'account', 'label' => 'Account', 'width' => '25%', 'align' => 'left'),
				array('key' => 'description', 'label' => 'Description', 'width' => '35%', 'align' => 'left'),
				array('key' => 'debit', 'label' => 'Debit', 'width' => '20%', 'align' => 'right'),
				array('key' => 'credit', 'label' => 'Credit', 'width' => '20%', 'align' => 'right'),
			)),
		),
		'receipt_voucher' => array(
			'name' => 'Receipt Voucher — Standard',
			'header_html' => '<div style="text-align:center;font-size:18px;font-weight:bold;">RECEIPT VOUCHER</div>',
			'footer_html' => '',
			'body_columns' => json_encode(array(
				array('key' => 'account', 'label' => 'Account', 'width' => '25%', 'align' => 'left'),
				array('key' => 'description', 'label' => 'Description', 'width' => '35%', 'align' => 'left'),
				array('key' => 'debit', 'label' => 'Debit', 'width' => '20%', 'align' => 'right'),
				array('key' => 'credit', 'label' => 'Credit', 'width' => '20%', 'align' => 'right'),
			)),
		),
		'delivery_note' => array(
			'name' => 'Delivery Note — Standard',
			'header_html' => '<div style="text-align:center;font-size:18px;font-weight:bold;">DELIVERY NOTE</div>',
			'footer_html' => '<div style="font-size:9px;text-align:center;">Goods received in good condition</div>',
			'body_columns' => json_encode(array(
				array('key' => 'line_no', 'label' => '#', 'width' => '5%', 'align' => 'center'),
				array('key' => 'description', 'label' => 'Item', 'width' => '50%', 'align' => 'left'),
				array('key' => 'qty', 'label' => 'Qty', 'width' => '15%', 'align' => 'center'),
				array('key' => 'uom', 'label' => 'UOM', 'width' => '15%', 'align' => 'center'),
			)),
		),
		'quotation' => array(
			'name' => 'Quotation — Standard',
			'header_html' => '<div style="text-align:center;font-size:18px;font-weight:bold;">QUOTATION</div><div style="text-align:center;font-size:12px;color:#666;">{{company_name}}</div>',
			'footer_html' => '<div style="font-size:10px;">This quotation is valid for 30 days from the date of issue.</div>',
			'body_columns' => json_encode(array(
				array('key' => 'line_no', 'label' => '#', 'width' => '5%', 'align' => 'center'),
				array('key' => 'description', 'label' => 'Description', 'width' => '40%', 'align' => 'left'),
				array('key' => 'qty', 'label' => 'Qty', 'width' => '10%', 'align' => 'center'),
				array('key' => 'unit_price', 'label' => 'Rate', 'width' => '15%', 'align' => 'right'),
				array('key' => 'line_total', 'label' => 'Amount', 'width' => '15%', 'align' => 'right'),
			)),
		),
	);

	$now = time();
	$st = $db->prepare(
		'INSERT INTO epc_erp_print_templates
		 (doc_type, name, is_default, header_html, footer_html, body_columns, active, time_created, time_updated)
		 VALUES (?,?,1,?,?,?,1,?,?)'
	);
	foreach ($docTypes as $type => $tpl) {
		$st->execute(array(
			$type, $tpl['name'], $tpl['header_html'], $tpl['footer_html'],
			$tpl['body_columns'], $now, $now,
		));
	}
}

/* ─── CRUD ─── */

/** @return array<int,array<string,mixed>> */
function epc_erp_print_templates_list(PDO $db, string $docType = ''): array
{
	epc_erp_print_designer_ensure_schema($db);
	$sql = 'SELECT * FROM epc_erp_print_templates WHERE active = 1';
	$params = array();
	if ($docType !== '') {
		$sql .= ' AND doc_type = ?';
		$params[] = $docType;
	}
	$sql .= ' ORDER BY doc_type, is_default DESC, name';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/** @return array<string,mixed>|null */
function epc_erp_print_template_get(PDO $db, int $id): ?array
{
	$st = $db->prepare('SELECT * FROM epc_erp_print_templates WHERE id = ?');
	$st->execute(array($id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_erp_print_template_get_default(PDO $db, string $docType): ?array
{
	epc_erp_print_designer_ensure_schema($db);
	epc_erp_print_designer_seed_defaults($db);
	$st = $db->prepare('SELECT * FROM epc_erp_print_templates WHERE doc_type = ? AND is_default = 1 AND active = 1 LIMIT 1');
	$st->execute(array($docType));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		$st2 = $db->prepare('SELECT * FROM epc_erp_print_templates WHERE doc_type = ? AND active = 1 ORDER BY id LIMIT 1');
		$st2->execute(array($docType));
		$row = $st2->fetch(PDO::FETCH_ASSOC);
	}
	return $row ?: null;
}

function epc_erp_print_template_save(PDO $db, array $data): int
{
	epc_erp_print_designer_ensure_schema($db);
	$now = time();
	$id = (int)($data['id'] ?? 0);

	$fields = array(
		'doc_type', 'name', 'page_size', 'orientation',
		'margin_top', 'margin_bottom', 'margin_left', 'margin_right',
		'font_family', 'font_size', 'primary_color', 'secondary_color',
		'logo_position', 'logo_max_height',
		'header_html', 'footer_html', 'body_columns',
		'show_terms', 'terms_html', 'show_bank_details', 'bank_details_html',
		'show_signature_line', 'signature_labels',
		'show_qr_code', 'show_barcode', 'custom_css',
	);

	if ($id > 0) {
		$sets = array();
		$vals = array();
		foreach ($fields as $f) {
			if (array_key_exists($f, $data)) {
				$sets[] = '`' . $f . '` = ?';
				$vals[] = $data[$f];
			}
		}
		$sets[] = '`time_updated` = ?';
		$vals[] = $now;
		$vals[] = $id;
		$db->prepare('UPDATE epc_erp_print_templates SET ' . implode(', ', $sets) . ' WHERE id = ?')->execute($vals);
		if (!empty($data['is_default'])) {
			$db->prepare('UPDATE epc_erp_print_templates SET is_default = 0 WHERE doc_type = ? AND id != ?')
				->execute(array($data['doc_type'], $id));
			$db->prepare('UPDATE epc_erp_print_templates SET is_default = 1 WHERE id = ?')->execute(array($id));
		}
		return $id;
	}

	$cols = array();
	$placeholders = array();
	$vals = array();
	foreach ($fields as $f) {
		if (array_key_exists($f, $data)) {
			$cols[] = '`' . $f . '`';
			$placeholders[] = '?';
			$vals[] = $data[$f];
		}
	}
	$cols[] = '`time_created`';
	$placeholders[] = '?';
	$vals[] = $now;
	$cols[] = '`time_updated`';
	$placeholders[] = '?';
	$vals[] = $now;

	$db->prepare(
		'INSERT INTO epc_erp_print_templates (' . implode(',', $cols) . ') VALUES (' . implode(',', $placeholders) . ')'
	)->execute($vals);
	$newId = (int)$db->lastInsertId();
	if (!empty($data['is_default'])) {
		$db->prepare('UPDATE epc_erp_print_templates SET is_default = 0 WHERE doc_type = ? AND id != ?')
			->execute(array($data['doc_type'], $newId));
		$db->prepare('UPDATE epc_erp_print_templates SET is_default = 1 WHERE id = ?')->execute(array($newId));
	}
	return $newId;
}

/* ─── Render engine ─── */

/**
 * Render a document using the tenant's configured print template.
 *
 * @param array $template  Row from epc_erp_print_templates
 * @param array $doc       Document data with merge fields
 * @param array $lines     Line items array
 * @return string           Full HTML page for printing
 */
function epc_erp_print_render(array $template, array $doc, array $lines): string
{
	$h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
	$mergeFields = array(
		'{{company_name}}' => $doc['company_name'] ?? '',
		'{{trn}}' => $doc['trn'] ?? '',
		'{{address}}' => $doc['address'] ?? '',
		'{{phone}}' => $doc['phone'] ?? '',
		'{{email}}' => $doc['email'] ?? '',
		'{{doc_no}}' => $doc['doc_no'] ?? '',
		'{{doc_date}}' => $doc['doc_date'] ?? '',
		'{{due_date}}' => $doc['due_date'] ?? '',
		'{{customer_name}}' => $doc['customer_name'] ?? '',
		'{{supplier_name}}' => $doc['supplier_name'] ?? '',
		'{{currency}}' => $doc['currency'] ?? 'AED',
		'{{total}}' => $doc['total'] ?? '0.00',
		'{{page}}' => '1',
		'{{total_pages}}' => '1',
		'{{printed_by}}' => $doc['printed_by'] ?? '',
		'{{printed_date}}' => date('d M Y H:i'),
	);

	$pageSize = $template['page_size'] ?? 'A4';
	$orient = $template['orientation'] ?? 'portrait';
	$margins = array(
		'top' => (int)($template['margin_top'] ?? 15),
		'bottom' => (int)($template['margin_bottom'] ?? 15),
		'left' => (int)($template['margin_left'] ?? 10),
		'right' => (int)($template['margin_right'] ?? 10),
	);
	$font = $h($template['font_family'] ?? 'Arial, sans-serif');
	$fontSize = (int)($template['font_size'] ?? 11);
	$primaryColor = $h($template['primary_color'] ?? '#1565c0');
	$secondaryColor = $h($template['secondary_color'] ?? '#4a6a7a');

	$headerHtml = str_replace(array_keys($mergeFields), array_map($h, array_values($mergeFields)), $template['header_html'] ?? '');
	$footerHtml = str_replace(array_keys($mergeFields), array_map($h, array_values($mergeFields)), $template['footer_html'] ?? '');
	$columns = json_decode($template['body_columns'] ?? '[]', true) ?: array();

	$html = '<!DOCTYPE html><html><head><meta charset="UTF-8"><title>' . $h($doc['doc_no'] ?? 'Document') . '</title>';
	$html .= '<style>';
	$html .= '@page { size: ' . $pageSize . ' ' . $orient . '; margin: ' . $margins['top'] . 'mm ' . $margins['right'] . 'mm ' . $margins['bottom'] . 'mm ' . $margins['left'] . 'mm; }';
	$html .= 'body { font-family: ' . $font . '; font-size: ' . $fontSize . 'pt; color: #333; line-height: 1.4; margin: 0; padding: 20px; }';
	$html .= '.print-header { margin-bottom: 20px; }';
	$html .= '.print-meta { display: flex; justify-content: space-between; margin: 16px 0; font-size: 10pt; }';
	$html .= '.print-meta-col { flex: 1; }';
	$html .= '.print-table { width: 100%; border-collapse: collapse; margin: 16px 0; }';
	$html .= '.print-table th { background: ' . $primaryColor . '; color: #fff; padding: 6px 8px; font-size: 10pt; text-align: left; }';
	$html .= '.print-table td { padding: 5px 8px; border-bottom: 1px solid #e0e0e0; font-size: 10pt; }';
	$html .= '.print-table tr:nth-child(even) { background: #f8f9fa; }';
	$html .= '.print-totals { text-align: right; margin: 10px 0; font-size: 11pt; }';
	$html .= '.print-totals strong { color: ' . $primaryColor . '; }';
	$html .= '.print-signatures { display: flex; justify-content: space-between; margin-top: 40px; padding-top: 10px; }';
	$html .= '.print-sig-box { text-align: center; min-width: 120px; }';
	$html .= '.print-sig-line { border-top: 1px solid #999; margin-top: 40px; padding-top: 4px; font-size: 9pt; color: ' . $secondaryColor . '; }';
	$html .= '.print-footer { margin-top: 20px; border-top: 1px solid #e0e0e0; padding-top: 8px; font-size: 9pt; color: #888; }';
	$html .= '.print-terms { margin: 12px 0; padding: 8px 12px; background: #f8f9fa; border-left: 3px solid ' . $primaryColor . '; font-size: 9pt; }';
	$html .= '.print-bank { font-size: 9pt; margin: 8px 0; }';
	$html .= '@media print { body { padding: 0; } .no-print { display: none; } }';
	if (!empty($template['custom_css'])) {
		$html .= $template['custom_css'];
	}
	$html .= '</style></head><body>';

	// Print button (hidden in print)
	$html .= '<div class="no-print" style="text-align:right;margin-bottom:10px;"><button onclick="window.print()" style="padding:8px 20px;background:' . $primaryColor . ';color:#fff;border:none;border-radius:4px;cursor:pointer;font-size:12pt;">Print</button></div>';

	// Header
	$html .= '<div class="print-header">' . $headerHtml . '</div>';

	// Document meta
	$html .= '<div class="print-meta">';
	$html .= '<div class="print-meta-col"><strong>Document #:</strong> ' . $h($doc['doc_no'] ?? '') . '<br><strong>Date:</strong> ' . $h($doc['doc_date'] ?? '') . '</div>';
	if (!empty($doc['customer_name'])) {
		$html .= '<div class="print-meta-col"><strong>Bill To:</strong><br>' . $h($doc['customer_name']) . '</div>';
	}
	if (!empty($doc['supplier_name'])) {
		$html .= '<div class="print-meta-col"><strong>Supplier:</strong><br>' . $h($doc['supplier_name']) . '</div>';
	}
	$html .= '</div>';

	// Lines table
	if (!empty($columns) && !empty($lines)) {
		$html .= '<table class="print-table"><thead><tr>';
		foreach ($columns as $col) {
			$html .= '<th style="width:' . $h($col['width'] ?? 'auto') . ';text-align:' . $h($col['align'] ?? 'left') . ';">' . $h($col['label'] ?? '') . '</th>';
		}
		$html .= '</tr></thead><tbody>';
		foreach ($lines as $li => $line) {
			$html .= '<tr>';
			foreach ($columns as $col) {
				$key = $col['key'] ?? '';
				$val = $line[$key] ?? '';
				if ($key === 'line_no' && $val === '') $val = (string)($li + 1);
				$html .= '<td style="text-align:' . $h($col['align'] ?? 'left') . ';">' . $h($val) . '</td>';
			}
			$html .= '</tr>';
		}
		$html .= '</tbody></table>';
	}

	// Totals
	if (isset($doc['subtotal']) || isset($doc['total'])) {
		$html .= '<div class="print-totals">';
		if (isset($doc['subtotal'])) $html .= 'Subtotal: <strong>' . $h($doc['subtotal']) . ' ' . $h($doc['currency'] ?? 'AED') . '</strong><br>';
		if (isset($doc['vat_amount'])) $html .= 'VAT: <strong>' . $h($doc['vat_amount']) . '</strong><br>';
		if (isset($doc['discount'])) $html .= 'Discount: <strong>(' . $h($doc['discount']) . ')</strong><br>';
		$html .= '<span style="font-size:13pt;">Total: <strong>' . $h($doc['total'] ?? '0.00') . ' ' . $h($doc['currency'] ?? 'AED') . '</strong></span>';
		$html .= '</div>';
	}

	// Terms
	if (!empty($template['show_terms']) && !empty($template['terms_html'])) {
		$html .= '<div class="print-terms"><strong>Terms & Conditions:</strong><br>' . $template['terms_html'] . '</div>';
	}

	// Bank details
	if (!empty($template['show_bank_details']) && !empty($template['bank_details_html'])) {
		$html .= '<div class="print-bank"><strong>Bank Details:</strong><br>' . $template['bank_details_html'] . '</div>';
	}

	// Signature lines
	if (!empty($template['show_signature_line'])) {
		$labels = explode(',', $template['signature_labels'] ?? 'Prepared by,Approved by,Received by');
		$html .= '<div class="print-signatures">';
		foreach ($labels as $lbl) {
			$html .= '<div class="print-sig-box"><div class="print-sig-line">' . $h(trim($lbl)) . '</div></div>';
		}
		$html .= '</div>';
	}

	// Footer
	if ($footerHtml !== '') {
		$html .= '<div class="print-footer">' . $footerHtml . '</div>';
	}

	$html .= '</body></html>';
	return $html;
}

/** Available merge fields for template editor */
function epc_erp_print_merge_fields(): array
{
	return array(
		'{{company_name}}' => 'Company name',
		'{{trn}}' => 'Tax Registration Number',
		'{{address}}' => 'Company address',
		'{{phone}}' => 'Company phone',
		'{{email}}' => 'Company email',
		'{{doc_no}}' => 'Document number',
		'{{doc_date}}' => 'Document date',
		'{{due_date}}' => 'Due date',
		'{{customer_name}}' => 'Customer name',
		'{{supplier_name}}' => 'Supplier name',
		'{{currency}}' => 'Currency code',
		'{{total}}' => 'Document total',
		'{{page}}' => 'Current page',
		'{{total_pages}}' => 'Total pages',
		'{{printed_by}}' => 'Printed by (user)',
		'{{printed_date}}' => 'Print date/time',
	);
}

/** Document types available for print templates */
function epc_erp_print_doc_types(): array
{
	return array(
		'invoice' => 'Tax Invoice',
		'sales_order' => 'Sales Order',
		'purchase_order' => 'Purchase Order',
		'payment_voucher' => 'Payment Voucher',
		'receipt_voucher' => 'Receipt Voucher',
		'journal_voucher' => 'Journal Voucher',
		'delivery_note' => 'Delivery Note',
		'quotation' => 'Quotation',
		'report' => 'Report',
	);
}
