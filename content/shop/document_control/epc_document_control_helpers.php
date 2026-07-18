<?php
/**
 * Document Control System — company profile, templates, order render, attachments.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_document_control_schema.php';
require_once __DIR__ . '/epc_document_control_cp_install.php';
require_once __DIR__ . '/../finance/epc_uae_vat.php';

function epc_dc_h($v)
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_dc_money($n)
{
	return number_format((float)$n, 2, '.', ',') . ' AED';
}

function epc_dc_tab_url($base, $tab, $extra = array())
{
	$q = array_merge(array('tab' => $tab), $extra);
	return $base . '?' . http_build_query($q);
}

function epc_dc_ensure(PDO $db): void
{
	epc_doc_control_ensure_schema($db);
}

function epc_dc_attachments_dir(): string
{
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_doc_attachments';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	return $dir;
}

function epc_dc_logo_dir(): string
{
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_doc';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	return $dir;
}

function epc_dc_get_company(PDO $db): array
{
	epc_dc_ensure($db);
	$row = $db->query('SELECT * FROM `epc_document_company` WHERE `id` = 1 LIMIT 1')->fetch(PDO::FETCH_ASSOC);
	return $row ?: array();
}

function epc_dc_save_company(PDO $db, array $data): void
{
	epc_dc_ensure($db);
	$fields = array(
		'legal_name', 'trade_name', 'address_line1', 'address_line2', 'city', 'country',
		'trn', 'phone', 'email', 'website', 'logo_path', 'bank_name', 'bank_iban', 'legal_footer',
	);
	$sets = array();
	$params = array();
	foreach ($fields as $f) {
		if (array_key_exists($f, $data)) {
			$sets[] = '`' . $f . '` = ?';
			$params[] = trim((string)$data[$f]);
		}
	}
	if (!$sets) {
		return;
	}
	$sets[] = '`updated_at` = ?';
	$params[] = time();
	$params[] = 1;
	$db->prepare('UPDATE `epc_document_company` SET ' . implode(', ', $sets) . ' WHERE `id` = ?')->execute($params);
}

function epc_dc_list_templates(PDO $db): array
{
	epc_dc_ensure($db);
	$st = $db->query('SELECT * FROM `epc_document_templates` ORDER BY `sort_order`, `code`');
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_dc_get_template(PDO $db, string $code): ?array
{
	epc_dc_ensure($db);
	$st = $db->prepare('SELECT * FROM `epc_document_templates` WHERE `code` = ? LIMIT 1');
	$st->execute(array($code));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_dc_save_template(PDO $db, string $code, array $data): void
{
	epc_dc_ensure($db);
	$db->prepare(
		'UPDATE `epc_document_templates` SET
		`title` = ?, `description` = ?, `header_html` = ?, `body_html` = ?, `footer_html` = ?, `css_extra` = ?, `active` = ?, `updated_at` = ?
		WHERE `code` = ?'
	)->execute(array(
		trim((string)($data['title'] ?? '')),
		trim((string)($data['description'] ?? '')),
		(string)($data['header_html'] ?? ''),
		(string)($data['body_html'] ?? ''),
		(string)($data['footer_html'] ?? ''),
		(string)($data['css_extra'] ?? ''),
		!empty($data['active']) ? 1 : 0,
		time(),
		$code,
	));
}

function epc_dc_dashboard(PDO $db): array
{
	epc_dc_ensure($db);
	$templates = (int)$db->query('SELECT COUNT(*) FROM `epc_document_templates` WHERE `active` = 1')->fetchColumn();
	$attachments = (int)$db->query('SELECT COUNT(*) FROM `epc_document_attachments`')->fetchColumn();
	$supplier = (int)$db->query('SELECT COUNT(*) FROM `epc_document_attachments` WHERE `doc_category` = \'supplier_invoice\'')->fetchColumn();
	$company = epc_dc_get_company($db);
	$trnOk = trim((string)($company['trn'] ?? '')) !== '';
	return array(
		'active_templates' => $templates,
		'attachments_total' => $attachments,
		'supplier_invoices' => $supplier,
		'company_configured' => $trnOk && trim((string)($company['legal_name'] ?? '')) !== '',
	);
}

function epc_dc_recent_orders(PDO $db, int $limit = 40): array
{
	$st = $db->query(
		'SELECT o.`id`, o.`user_id`, o.`time`, o.`paid`, u.`email`,
		(SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = o.`id`) AS sale_ex
		FROM `shop_orders` o
		LEFT JOIN `users` u ON u.`user_id` = o.`user_id`
		WHERE o.`successfully_created` = 1
		ORDER BY o.`id` DESC LIMIT ' . (int)$limit
	);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_dc_list_attachments(PDO $db, string $entity_type = '', int $entity_id = 0, string $category = ''): array
{
	epc_dc_ensure($db);
	$sql = 'SELECT * FROM `epc_document_attachments` WHERE 1=1';
	$params = array();
	if ($entity_type !== '') {
		$sql .= ' AND `entity_type` = ?';
		$params[] = $entity_type;
	}
	if ($entity_id > 0) {
		$sql .= ' AND `entity_id` = ?';
		$params[] = $entity_id;
	}
	if ($category !== '') {
		$sql .= ' AND `doc_category` = ?';
		$params[] = $category;
	}
	$sql .= ' ORDER BY `uploaded_at` DESC LIMIT 200';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_dc_save_attachment(PDO $db, array $meta, array $file): void
{
	epc_dc_ensure($db);
	if (empty($file['tmp_name']) || !is_uploaded_file($file['tmp_name'])) {
		throw new Exception('No file uploaded');
	}
	$max = 15 * 1024 * 1024;
	if ((int)$file['size'] > $max) {
		throw new Exception('File exceeds 15 MB limit');
	}
	$orig = basename((string)$file['name']);
	$ext = strtolower(pathinfo($orig, PATHINFO_EXTENSION));
	$allowed = array('pdf', 'jpg', 'jpeg', 'png', 'webp', 'doc', 'docx', 'xls', 'xlsx');
	if (!in_array($ext, $allowed, true)) {
		throw new Exception('File type not allowed');
	}
	$dir = epc_dc_attachments_dir();
	$stored = 'att_' . time() . '_' . bin2hex(random_bytes(4)) . '.' . $ext;
	$path = $dir . '/' . $stored;
	if (!move_uploaded_file($file['tmp_name'], $path)) {
		throw new Exception('Upload failed');
	}
	$rel = '/content/files/epc_doc_attachments/' . $stored;
	$adminId = class_exists('DP_User') ? (int)DP_User::getAdminId() : 0;
	$db->prepare(
		'INSERT INTO `epc_document_attachments`
		(`entity_type`, `entity_id`, `doc_category`, `supplier_name`, `reference_no`, `file_name`, `file_path`, `mime_type`, `file_size`, `notes`, `uploaded_by`, `uploaded_at`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	)->execute(array(
		(string)($meta['entity_type'] ?? 'order'),
		(int)($meta['entity_id'] ?? 0),
		(string)($meta['doc_category'] ?? 'supplier_invoice'),
		trim((string)($meta['supplier_name'] ?? '')),
		trim((string)($meta['reference_no'] ?? '')),
		$orig,
		$rel,
		(string)($file['type'] ?? ''),
		(int)$file['size'],
		trim((string)($meta['notes'] ?? '')),
		$adminId,
		time(),
	));
}

function epc_dc_delete_attachment(PDO $db, int $id): void
{
	$st = $db->prepare('SELECT * FROM `epc_document_attachments` WHERE `id` = ? LIMIT 1');
	$st->execute(array($id));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Attachment not found');
	}
	$full = $_SERVER['DOCUMENT_ROOT'] . $row['file_path'];
	if (is_file($full)) {
		@unlink($full);
	}
	$db->prepare('DELETE FROM `epc_document_attachments` WHERE `id` = ?')->execute(array($id));
}

function epc_dc_amount_words_en($amount): string
{
	$amount = round((float)$amount, 2);
	$whole = (int)floor($amount);
	$fils = (int)round(($amount - $whole) * 100);
	$words = epc_dc_number_words($whole);
	$out = ucfirst($words) . ' UAE Dirhams';
	if ($fils > 0) {
		$out .= ' and ' . epc_dc_number_words($fils) . ' Fils';
	}
	$out .= ' only';
	return $out;
}

function epc_dc_number_words($n): string
{
	$n = (int)$n;
	if ($n === 0) {
		return 'zero';
	}
	$ones = array('', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten',
		'eleven', 'twelve', 'thirteen', 'fourteen', 'fifteen', 'sixteen', 'seventeen', 'eighteen', 'nineteen');
	$tens = array('', '', 'twenty', 'thirty', 'forty', 'fifty', 'sixty', 'seventy', 'eighty', 'ninety');
	$scales = array('', ' thousand', ' million', ' billion');
	$parts = array();
	$scale = 0;
	while ($n > 0) {
		$chunk = $n % 1000;
		if ($chunk) {
			$w = '';
			$h = (int)floor($chunk / 100);
			$r = $chunk % 100;
			if ($h) {
				$w .= $ones[$h] . ' hundred';
				if ($r) {
					$w .= ' and ';
				}
			}
			if ($r < 20) {
				$w .= $ones[$r];
			} else {
				$w .= $tens[(int)floor($r / 10)];
				if ($r % 10) {
					$w .= '-' . $ones[$r % 10];
				}
			}
			$parts[] = trim($w) . $scales[$scale];
		}
		$n = (int)floor($n / 1000);
		$scale++;
	}
	return implode(' ', array_reverse($parts));
}

function epc_dc_order_context(PDO $db, int $order_id): array
{
	require_once __DIR__ . '/../finance/epc_einvoice.php';
	require_once __DIR__ . '/../finance/epc_erp_helpers.php';

	$ost = $db->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1');
	$ost->execute(array($order_id));
	$order = $ost->fetch(PDO::FETCH_ASSOC);
	if (!$order) {
		throw new Exception('Order not found');
	}

	$item_where = epc_erp_item_status_exclusion($db)['where_and'];
	$items = $db->prepare(
		'SELECT *, (`price`*`count_need`) AS `line_net`, `count_need` AS `qty` FROM `shop_orders_items`
		WHERE `order_id` = ?' . $item_where
	);
	$items->execute(array($order_id));
	$rows = $items->fetchAll(PDO::FETCH_ASSOC);

	$user_id = (int)$order['user_id'];
	$buyer = epc_einvoice_buyer_profile($db, $user_id);
	$company = epc_dc_get_company($db);

	require_once __DIR__ . '/../finance/epc_uae_customer_vat.php';

	$rate = epc_uae_vat_rate_percent($db);
	$subtotal = 0.0;
	$vatTotal = 0.0;
	$lineRows = array();
	foreach ($rows as $r) {
		$qty = (float)($r['qty'] ?? $r['count_need'] ?? 0);
		$lineCalc = epc_uae_customer_vat_order_line($db, $user_id, (float)$r['price'], $qty, array());
		$net = round((float)$lineCalc['line_net'], 2);
		$vat = round((float)$lineCalc['vat_amount'], 2);
		$gross = round((float)$lineCalc['gross'], 2);
		$subtotal += $net;
		$vatTotal += $vat;
		if ($rate <= 0 && (float)$lineCalc['tax_rate'] > 0) {
			$rate = (float)$lineCalc['tax_rate'];
		}
		$r['unit_net'] = round((float)$lineCalc['unit_net'], 2);
		$r['line_net'] = $net;
		$r['vat_amount'] = $vat;
		$r['gross'] = $gross;
		$r['tax_rate'] = (float)$lineCalc['tax_rate'];
		$r['qty'] = $qty;
		$lineRows[] = $r;
	}
	$rows = $lineRows;
	$subtotal = round($subtotal, 2);
	$vatTotal = round($vatTotal, 2);
	$totalIncl = round($subtotal + $vatTotal, 2);

	$pq = $db->prepare('SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=0 AND `order_id`=?');
	$pq->execute(array($order_id));
	$paid = round((float)$pq->fetchColumn(), 2);

	$invNo = 'INV-' . str_pad((string)$order_id, 6, '0', STR_PAD_LEFT);
	$einv = $db->prepare('SELECT `invoice_number` FROM `epc_einvoice_documents` WHERE `order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1');
	$einv->execute(array($order_id));
	$einvNo = (string)$einv->fetchColumn();
	if ($einvNo !== '') {
		$invNo = $einvNo;
	}

	$buyerName = trim((string)($buyer['buyer_name'] ?? ''));
	if ($buyerName === '') {
		$buyerName = trim((string)($buyer['company'] ?? 'Customer'));
	}
	$buyerEmail = trim((string)($buyer['email'] ?? ''));
	$buyerPhone = trim((string)($buyer['phone'] ?? ''));
	if ($buyerEmail === '' || $buyerPhone === '') {
		try {
			$uq = $db->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
			$uq->execute(array($user_id));
			$u = $uq->fetch(PDO::FETCH_ASSOC) ?: array();
			if ($buyerEmail === '') {
				$buyerEmail = trim((string)($u['email'] ?? ''));
			}
			if ($buyerPhone === '') {
				$buyerPhone = trim((string)($u['phone'] ?? ''));
			}
		} catch (Throwable $e) {
			// ignore
		}
	}
	$buyerAddr = trim(implode(', ', array_filter(array(
		$buyer['address_line1'] ?? '',
		$buyer['city'] ?? '',
		$buyer['emirate'] ?? '',
		$buyer['country_code'] ?? 'AE',
	))));
	if ($buyerAddr === '' || $buyerAddr === 'AE') {
		$buyerAddr = trim(implode(', ', array_filter(array(
			$buyerEmail,
			$buyerPhone,
			$buyer['city'] ?? 'Dubai',
			'United Arab Emirates',
		))));
	}

	$legal = trim((string)($company['legal_name'] ?? ''));
	if ($legal === '') {
		$legal = trim((string)($company['trade_name'] ?? 'Company'));
	}
	$coAddr = trim(implode(', ', array_filter(array(
		$company['address_line1'] ?? '',
		$company['address_line2'] ?? '',
		$company['city'] ?? '',
		$company['country'] ?? '',
	))));

	$logo = trim((string)($company['logo_path'] ?? ''));
	if ($logo === '') {
		$logo = '/content/files/epc_doc/logo.png';
	}

	return array(
		'order' => $order,
		'items' => $rows,
		'company' => $company,
		'buyer' => $buyer,
		'invoice_number' => $invNo,
		'subtotal_excl_vat' => $subtotal,
		'vat_amount' => $vatTotal,
		'total_incl_vat' => $totalIncl,
		'vat_rate' => $rate,
		'amount_paid' => $paid,
		'amount_due' => max(0, round($totalIncl - $paid, 2)),
		'placeholders' => array(
			'company_logo' => $logo,
			'company_legal_name' => $legal,
			'company_trade_name' => (string)($company['trade_name'] ?? ''),
			'company_address' => $coAddr,
			'company_trn' => (string)($company['trn'] ?? ''),
			'company_phone' => (string)($company['phone'] ?? ''),
			'company_email' => (string)($company['email'] ?? ''),
			'company_website' => (string)($company['website'] ?? ''),
			'document_number' => $invNo,
			'document_date' => date('d M Y', (int)$order['time']),
			'order_id' => (string)$order_id,
			'supply_date' => date('d M Y', (int)$order['time']),
			'buyer_name' => $buyerName,
			'buyer_address' => $buyerAddr,
			'buyer_trn' => (string)($buyer['trn'] ?? ''),
			'ship_to_name' => $buyerName,
			'ship_to_address' => $buyerAddr,
			'ship_to_phone' => (string)($buyer['phone'] ?? ''),
			'subtotal_excl_vat' => epc_dc_money($subtotal),
			'vat_amount' => epc_dc_money($vatTotal),
			'total_incl_vat' => epc_dc_money($totalIncl),
			'vat_rate' => (string)$rate,
			'amount_words' => epc_dc_amount_words_en($totalIncl),
			'payment_terms' => 'Due on receipt unless agreed otherwise',
			'bank_name' => (string)($company['bank_name'] ?? ''),
			'bank_iban' => (string)($company['bank_iban'] ?? ''),
			'legal_footer' => (string)($company['legal_footer'] ?? ''),
			'carrier' => '—',
			'tracking_no' => '—',
			'package_count' => '1',
			'total_weight' => '—',
			'prepared_by' => 'Warehouse',
			'driver_info' => '—',
			'delivery_notes' => '',
			'amount_received' => epc_dc_money($paid > 0 ? $paid : $totalIncl),
			'payment_method' => !empty($order['paid']) ? 'Paid in full' : 'Pending',
			'payment_reference' => 'ORD-' . $order_id,
			'payment_date' => date('d M Y'),
			'lines_table' => epc_dc_lines_table_sales($rows, $rate),
			'lines_table_packing' => epc_dc_lines_table_packing($rows),
			'lines_table_delivery' => epc_dc_lines_table_delivery($rows),
		),
	);
}

function epc_dc_lines_table_sales(array $rows, float $vatRate): string
{
	$html = '<table><thead><tr>
<th>#</th><th>Description</th><th>SKU / Part</th><th class="right">Qty</th><th class="right">Unit (excl.)</th><th class="right">Net</th><th class="right">VAT ' . epc_dc_h((string)$vatRate) . '%</th><th class="right">Total</th>
</tr></thead><tbody>';
	$i = 0;
	foreach ($rows as $r) {
		$i++;
		$net = round((float)($r['line_net'] ?? 0), 2);
		if (isset($r['vat_amount'])) {
			$vat = round((float)$r['vat_amount'], 2);
			$tot = isset($r['gross']) ? round((float)$r['gross'], 2) : round($net + $vat, 2);
			$unit = isset($r['unit_net']) ? (float)$r['unit_net'] : (float)($r['price'] ?? 0);
		} else {
			// Fallback only when precomputed VAT is unavailable.
			$vat = round($net * ($vatRate / 100), 2);
			$tot = round($net + $vat, 2);
			$unit = (float)($r['price'] ?? 0);
		}
		$lineRate = isset($r['tax_rate']) ? (float)$r['tax_rate'] : $vatRate;
		$sku = trim(($r['t2_manufacturer'] ?? '') . ' ' . ($r['t2_article'] ?? ''));
		$name = trim((string)($r['t2_name'] ?? 'Item'));
		$html .= '<tr><td>' . $i . '</td><td>' . epc_dc_h($name) . '</td><td>' . epc_dc_h($sku) . '</td>';
		$html .= '<td class="right">' . epc_dc_h((string)$r['qty']) . '</td>';
		$html .= '<td class="right">' . epc_dc_money($unit) . '</td>';
		$html .= '<td class="right">' . epc_dc_money($net) . '</td>';
		$html .= '<td class="right">' . epc_dc_money($vat) . ' <span class="muted">(' . epc_dc_h((string)$lineRate) . '%)</span></td>';
		$html .= '<td class="right">' . epc_dc_money($tot) . '</td></tr>';
	}
	$html .= '</tbody></table>';
	return $html;
}

function epc_dc_lines_table_packing(array $rows): string
{
	$html = '<table><thead><tr><th>#</th><th>Description</th><th>SKU</th><th class="right">Qty</th><th>Bin / Notes</th></tr></thead><tbody>';
	$i = 0;
	foreach ($rows as $r) {
		$i++;
		$sku = trim(($r['t2_manufacturer'] ?? '') . ' ' . ($r['t2_article'] ?? ''));
		$name = trim((string)($r['t2_name'] ?? 'Item'));
		$html .= '<tr><td>' . $i . '</td><td>' . epc_dc_h($name) . '</td><td>' . epc_dc_h($sku) . '</td>';
		$html .= '<td class="right">' . epc_dc_h((string)$r['qty']) . '</td><td></td></tr>';
	}
	$html .= '</tbody></table>';
	return $html;
}

function epc_dc_lines_table_delivery(array $rows): string
{
	return epc_dc_lines_table_packing($rows);
}

function epc_dc_render_template(PDO $db, string $code, int $order_id = 0, array $extra = array()): string
{
	$tpl = epc_dc_get_template($db, $code);
	if (!$tpl || !(int)$tpl['active']) {
		throw new Exception('Template not found or inactive');
	}
	$ph = array();
	if ($order_id > 0) {
		$ctx = epc_dc_order_context($db, $order_id);
		$ph = $ctx['placeholders'];
	} else {
		$company = epc_dc_get_company($db);
		$ph = array(
			'company_logo' => (string)($company['logo_path'] ?? '/content/files/epc_doc/logo.png'),
			'company_legal_name' => (string)($company['legal_name'] ?? 'Preview Company'),
			'company_address' => trim((string)($company['address_line1'] ?? '')),
			'company_trn' => (string)($company['trn'] ?? ''),
			'company_phone' => (string)($company['phone'] ?? ''),
			'company_email' => (string)($company['email'] ?? ''),
			'document_number' => 'PREVIEW-001',
			'document_date' => date('d M Y'),
			'order_id' => '0',
			'legal_footer' => (string)($company['legal_footer'] ?? ''),
			'lines_table' => '<p><em>Preview — select an order to populate line items.</em></p>',
			'lines_table_packing' => '<p><em>Preview packing lines.</em></p>',
			'lines_table_delivery' => '<p><em>Preview delivery lines.</em></p>',
		);
	}
	$ph = array_merge($ph, $extra);
	$html = (string)$tpl['header_html'] . (string)$tpl['body_html'] . (string)$tpl['footer_html'];
	foreach ($ph as $k => $v) {
		$html = str_replace('{{' . $k . '}}', (string)$v, $html);
	}
	$css = (string)($tpl['css_extra'] ?? '');
	return '<!DOCTYPE html><html><head><meta charset="utf-8"><title>' . epc_dc_h($tpl['title']) . '</title>'
		. '<style>' . $css . '@media print { body { margin: 0; } .no-print { display:none; } }</style></head><body>'
		. '<div class="no-print" style="padding:10px;background:#eef2ff;margin-bottom:12px;">'
		. '<button onclick="window.print()">Print</button> <button onclick="window.close()">Close</button></div>'
		. $html . '</body></html>';
}

function epc_dc_sync_seller_from_einvoice(PDO $db, bool $force = false): void
{
	if (!function_exists('epc_einvoice_seller_profile')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
	}
	$co = epc_dc_get_company($db);
	$s = epc_einvoice_seller_profile($db);
	$fill = static function ($current, $incoming) use ($force) {
		$current = trim((string) $current);
		$incoming = trim((string) $incoming);
		if ($force && $incoming !== '') {
			return $incoming;
		}
		return $current !== '' ? $current : $incoming;
	};

	$legalFooter = trim((string) ($co['legal_footer'] ?? ''));
	if ($legalFooter === '' || $force) {
		$legalFooter = 'This document is a Tax Invoice issued in accordance with UAE Federal Tax Authority (FTA) '
			. 'requirements and UAE e-invoicing (PINT-AE). Seller VAT Registration Number (TRN) must appear on all tax invoices. '
			. 'Retain records for a minimum of 5 years.';
	}

	epc_dc_save_company($db, array(
		'legal_name' => $fill($co['legal_name'] ?? '', $s['seller_name'] ?? ''),
		'trade_name' => $fill($co['trade_name'] ?? '', $s['seller_name'] ?? ''),
		'address_line1' => $fill($co['address_line1'] ?? '', $s['seller_address_line1'] ?? ''),
		'city' => $fill($co['city'] ?? '', $s['seller_city'] ?? 'Dubai'),
		'country' => $fill($co['country'] ?? '', 'United Arab Emirates'),
		'trn' => $fill($co['trn'] ?? '', $s['seller_trn'] ?? ''),
		'phone' => $fill($co['phone'] ?? '', $s['seller_phone'] ?? ''),
		'email' => $fill($co['email'] ?? '', $s['seller_email'] ?? ''),
		'website' => $fill($co['website'] ?? '', 'https://www.epartscart.com'),
		'bank_name' => $fill($co['bank_name'] ?? '', 'Bank account'),
		'bank_iban' => $fill($co['bank_iban'] ?? '', $s['seller_bank_account'] ?? ''),
		'legal_footer' => $legalFooter,
	));
}
