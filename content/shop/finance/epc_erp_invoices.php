<?php
/**
 * ERP customer tax invoices (e-invoice format) — list, manual create/edit, print & JSON export.
 * Persists to epc_einvoice_documents / epc_einvoice_lines (UAE PINT-AE ready).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_einvoice.php';

/** Table aliases used by this module (see epc_einvoice_schema.php). */
function epc_erp_invoices_table_names(): array
{
	return array(
		'header' => 'epc_einvoice_documents',
		'lines' => 'epc_einvoice_lines',
		'events' => 'epc_einvoice_events',
	);
}

function epc_erp_invoices_ensure_schema(PDO $db): void
{
	require_once __DIR__ . '/epc_erp_schema.php';
	epc_einvoice_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_phase8.php';
	epc_erp_phase8_ensure_schema($db);
	epc_erp_schema_add_column_if_missing($db, 'epc_einvoice_documents', 'crm_quote_id', 'int(11) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_einvoice_documents', 'sales_order_id', 'int(11) NOT NULL DEFAULT 0');
}

function epc_erp_invoice_list(PDO $db, int $date_from, int $date_to, array $filters = array(), int $limit = 150): array
{
	epc_erp_invoices_ensure_schema($db);
	$sql = 'SELECT d.*, u.`email` AS customer_email
		FROM `epc_einvoice_documents` d
		LEFT JOIN `users` u ON u.`user_id` = d.`user_id`
		WHERE d.`active` = 1 AND d.`issue_date` >= ? AND d.`issue_date` <= ?';
	$params = array($date_from, $date_to);
	if (!empty($filters['status'])) {
		$sql .= ' AND d.`status` = ?';
		$params[] = $filters['status'];
	}
	if (!empty($filters['q'])) {
		$sql .= ' AND (d.`invoice_number` LIKE ? OR d.`order_id` = ?)';
		$q = trim((string)$filters['q']);
		$params[] = '%' . $q . '%';
		$params[] = ctype_digit($q) ? (int)$q : 0;
	}
	$sql .= ' ORDER BY d.`issue_date` DESC, d.`id` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_invoice_kpis(PDO $db, int $date_from, int $date_to): array
{
	epc_erp_invoices_ensure_schema($db);
	$st = $db->prepare(
		'SELECT COUNT(*) AS total,
			SUM(`validation_ok` = 1) AS validated,
			SUM(`status` IN ("submitted","accepted","queued")) AS submitted,
			IFNULL(SUM(`total_incl_vat`),0) AS amount_incl_vat
		FROM `epc_einvoice_documents`
		WHERE `active` = 1 AND `issue_date` >= ? AND `issue_date` <= ?'
	);
	$st->execute(array($date_from, $date_to));
	$row = $st->fetch(PDO::FETCH_ASSOC) ?: array();
	return array(
		'total' => (int)($row['total'] ?? 0),
		'validated' => (int)($row['validated'] ?? 0),
		'submitted' => (int)($row['submitted'] ?? 0),
		'amount_incl_vat' => (float)($row['amount_incl_vat'] ?? 0),
	);
}

function epc_erp_invoice_parse_lines_from_post(PDO $db, array $post): array
{
	$lines = array();
	if (!empty($post['lines_json'])) {
		$decoded = json_decode((string)$post['lines_json'], true);
		if (is_array($decoded)) {
			$lines = $decoded;
		}
	}
	if (empty($lines) && !empty($post['line_desc']) && is_array($post['line_desc'])) {
		$n = count($post['line_desc']);
		for ($i = 0; $i < $n; $i++) {
			$desc = trim((string)($post['line_desc'][$i] ?? ''));
			if ($desc === '') {
				continue;
			}
			$qty = max(0.0001, (float)($post['line_qty'][$i] ?? 1));
			$unit = round((float)($post['line_unit'][$i] ?? 0), 4);
			$defaultRate = 5.0;
			if (!empty($post['user_id']) || !empty($post['customer_user_id'])) {
				require_once __DIR__ . '/epc_tax_toolkit.php';
				$ctx = epc_tax_toolkit_resolve($db, (int)($post['user_id'] ?? $post['customer_user_id'] ?? 0));
				$defaultRate = (float)$ctx['tax_rate'];
			}
			$rate = round((float)($post['line_vat_rate'][$i] ?? $defaultRate), 2);
			$net = round($qty * $unit, 2);
			$vat = round($net * $rate / 100, 2);
			$lines[] = array(
				'line_no' => count($lines) + 1,
				'item_name' => $desc,
				'item_description' => trim((string)($post['line_detail'][$i] ?? '')),
				'item_type' => 'G',
				'quantity' => $qty,
				'uom_code' => 'C62',
				'unit_price' => $unit,
				'line_net' => $net,
				'tax_category' => $rate > 0 ? 'S' : 'Z',
				'tax_rate' => $rate,
				'tax_amount' => $vat,
				'gross_amount' => $net + $vat,
				'vat_line_aed' => $vat,
				'line_amount_aed' => $net + $vat,
			);
		}
	}
	return $lines;
}

function epc_erp_invoice_build_manual(PDO $db, array $data, array $lines): array
{
	$const = epc_einvoice_constants();
	$seller = epc_einvoice_seller_profile($db);
	$issueTs = !empty($data['issue_date']) ? strtotime($data['issue_date'] . ' 12:00:00') : time();
	$dueTs = !empty($data['due_date']) ? strtotime($data['due_date'] . ' 23:59:59') : ($issueTs + 7 * 86400);
	$userId = (int)($data['user_id'] ?? 0);
	$orderId = (int)($data['order_id'] ?? $data['sales_order_id'] ?? 0);
	$buyer = array(
		'buyer_name' => trim((string)($data['buyer_name'] ?? '')),
		'buyer_trn' => trim((string)($data['buyer_trn'] ?? '')),
		'buyer_legal_reg_no' => trim((string)($data['buyer_legal_reg_no'] ?? '')),
		'buyer_legal_reg_type' => trim((string)($data['buyer_legal_reg_type'] ?? 'TL')),
		'buyer_address_line1' => trim((string)($data['buyer_address_line1'] ?? '')),
		'buyer_city' => trim((string)($data['buyer_city'] ?? 'Dubai')),
		'buyer_emirate' => trim((string)($data['buyer_emirate'] ?? 'Dubai')),
		'buyer_country_code' => strtoupper(substr(trim((string)($data['buyer_country_code'] ?? 'AE')), 0, 8)),
		'buyer_peppol_endpoint' => trim((string)($data['buyer_peppol_endpoint'] ?? '')),
	);
	if ($buyer['buyer_peppol_endpoint'] === '' && $buyer['buyer_trn'] !== '') {
		$buyer['buyer_peppol_endpoint'] = epc_einvoice_peppol_endpoint($buyer['buyer_trn']);
	}
	if ($buyer['buyer_peppol_endpoint'] === '') {
		$buyer['buyer_peppol_endpoint'] = $const['endpoint_not_onboarded'];
	}
	if ($userId > 0) {
		$bp = epc_einvoice_buyer_profile($db, $userId);
		if ($bp) {
			$buyer = array_merge($buyer, array(
				'buyer_name' => trim((string)($bp['buyer_name'] ?? '')) ?: $buyer['buyer_name'],
				'buyer_trn' => trim((string)($bp['trn'] ?? '')) ?: $buyer['buyer_trn'],
				'buyer_address_line1' => trim((string)($bp['address_line1'] ?? '')) ?: $buyer['buyer_address_line1'],
				'buyer_city' => trim((string)($bp['city'] ?? '')) ?: $buyer['buyer_city'],
				'buyer_emirate' => trim((string)($bp['emirate'] ?? '')) ?: $buyer['buyer_emirate'],
				'buyer_country_code' => trim((string)($bp['country_code'] ?? '')) ?: $buyer['buyer_country_code'],
				'buyer_email' => trim((string)($bp['email'] ?? '')) ?: ($buyer['buyer_email'] ?? ''),
				'buyer_peppol_endpoint' => trim((string)($bp['peppol_endpoint'] ?? '')) ?: $buyer['buyer_peppol_endpoint'],
			));
		}
	}
	if ($buyer['buyer_name'] === '') {
		$buyer['buyer_name'] = 'Customer #' . $userId;
	}
	if ($buyer['buyer_address_line1'] === '') {
		$buyer['buyer_address_line1'] = 'United Arab Emirates';
	}
	if (empty($buyer['buyer_email'])) {
		$buyer['buyer_email'] = $userId > 0 ? ('customer' . $userId . '@epartscart.local') : 'customer@epartscart.local';
	}
	if ($buyer['buyer_peppol_endpoint'] === '') {
		$buyer['buyer_peppol_endpoint'] = $const['endpoint_not_onboarded'];
	}

	$subtotal = 0;
	$totalVat = 0;
	foreach ($lines as $ln) {
		$subtotal += (float)$ln['line_net'];
		$totalVat += (float)($ln['vat_line_aed'] ?? $ln['tax_amount'] ?? 0);
	}
	$totalIncl = round($subtotal + $totalVat, 2);
	$paid = round((float)($data['paid_amount'] ?? 0), 2);
	$amountDue = round(max(0, $totalIncl - $paid), 2);
	$taxCat = 'S';
	$taxRate = 5.0;
	if (!empty($lines[0]['tax_rate'])) {
		$taxRate = (float)$lines[0]['tax_rate'];
		$taxCat = $lines[0]['tax_category'] ?? ($taxRate > 0 ? 'S' : 'Z');
	}

	return array(
		'uuid' => !empty($data['uuid']) ? $data['uuid'] : epc_einvoice_generate_uuid(),
		'invoice_number' => trim((string)($data['invoice_number'] ?? '')) !== ''
			? trim((string)$data['invoice_number'])
			: epc_einvoice_next_number($db),
		'order_id' => $orderId,
		'user_id' => $userId,
		'doc_category' => 'tax_invoice',
		'invoice_type_code' => $const['invoice_type_tax'],
		'issue_date' => $issueTs,
		'payment_due_date' => $dueTs,
		'vat_point_date' => $issueTs,
		'currency_code' => strtoupper(substr(trim((string)($data['currency_code'] ?? 'AED')), 0, 8)),
		'vat_currency_code' => 'AED',
		'transaction_type_code' => trim((string)($data['transaction_type_code'] ?? '00000000')),
		'payment_means_code' => epc_einvoice_get_setting($db, 'payment_means_code', '30'),
		'payment_terms' => trim((string)($data['payment_terms'] ?? epc_einvoice_get_setting($db, 'payment_terms', 'Within 7 days'))),
		'bank_account' => epc_einvoice_get_setting($db, 'seller_bank_account', ''),
		'business_process' => $const['business_process'],
		'specification_id' => $const['specification_id'],
		'seller' => $seller,
		'buyer' => $buyer,
		'subtotal_ex_vat' => $subtotal,
		'total_vat' => $totalVat,
		'total_incl_vat' => $totalIncl,
		'paid_amount' => $paid,
		'rounding_amount' => 0,
		'amount_due' => $amountDue,
		'tax_breakdown' => array(array(
			'tax_category' => $taxCat,
			'taxable_amount' => $subtotal,
			'tax_rate' => $taxRate,
			'tax_amount' => $totalVat,
			'label' => epc_einvoice_tax_categories()[$taxCat]['label'] ?? 'Standard Rate',
		)),
		'lines' => $lines,
	);
}

function epc_erp_invoice_update(PDO $db, int $id, array $doc, int $admin_id = 0): int
{
	epc_erp_invoices_ensure_schema($db);
	$existing = epc_einvoice_get_document($db, $id);
	if (!$existing) {
		throw new Exception('Invoice not found');
	}
	if (in_array($existing['status'], array('submitted', 'accepted', 'queued'), true)) {
		throw new Exception('Submitted invoices cannot be edited — issue a credit note instead');
	}
	$validation = epc_einvoice_validate_document($doc, $doc['lines'] ?? array(), true);
	$xml = epc_einvoice_build_xml($doc);
	$now = time();
	$status = $validation['ok'] ? 'validated' : 'draft';

	$db->beginTransaction();
	try {
		$db->prepare(
			'UPDATE `epc_einvoice_documents` SET
			 `invoice_number`=?, `order_id`=?, `user_id`=?, `issue_date`=?, `payment_due_date`=?, `vat_point_date`=?,
			 `currency_code`=?, `transaction_type_code`=?, `payment_terms`=?, `bank_account`=?,
			 `seller_json`=?, `buyer_json`=?, `subtotal_ex_vat`=?, `total_vat`=?, `total_incl_vat`=?,
			 `paid_amount`=?, `amount_due`=?, `tax_breakdown_json`=?, `status`=?, `validation_ok`=?,
			 `validation_errors_json`=?, `xml_content`=?, `time_updated`=?
			 WHERE `id`=? AND `active`=1'
		)->execute(array(
			$doc['invoice_number'],
			(int)$doc['order_id'],
			(int)$doc['user_id'],
			(int)$doc['issue_date'],
			(int)$doc['payment_due_date'],
			(int)($doc['vat_point_date'] ?? $doc['issue_date']),
			$doc['currency_code'] ?? 'AED',
			$doc['transaction_type_code'] ?? '00000000',
			$doc['payment_terms'] ?? '',
			$doc['bank_account'] ?? '',
			json_encode($doc['seller'], JSON_UNESCAPED_UNICODE),
			json_encode($doc['buyer'], JSON_UNESCAPED_UNICODE),
			$doc['subtotal_ex_vat'],
			$doc['total_vat'],
			$doc['total_incl_vat'],
			$doc['paid_amount'] ?? 0,
			$doc['amount_due'],
			json_encode($doc['tax_breakdown'] ?? array(), JSON_UNESCAPED_UNICODE),
			$status,
			$validation['ok'] ? 1 : 0,
			json_encode($validation['errors'], JSON_UNESCAPED_UNICODE),
			$xml,
			$now,
			$id,
		));
		$db->prepare('DELETE FROM `epc_einvoice_lines` WHERE `document_id` = ?')->execute(array($id));
		$lins = $db->prepare(
			'INSERT INTO `epc_einvoice_lines`
			(`document_id`, `line_no`, `item_name`, `item_description`, `item_type`, `quantity`, `uom_code`,
			 `unit_price`, `line_net`, `tax_category`, `tax_rate`, `tax_amount`, `gross_amount`, `vat_line_aed`, `line_amount_aed`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		);
		foreach ($doc['lines'] as $ln) {
			$lins->execute(array(
				$id,
				(int)$ln['line_no'],
				$ln['item_name'],
				$ln['item_description'] ?? '',
				$ln['item_type'] ?? 'G',
				$ln['quantity'],
				$ln['uom_code'] ?? 'C62',
				$ln['unit_price'],
				$ln['line_net'],
				$ln['tax_category'],
				$ln['tax_rate'],
				$ln['tax_amount'],
				$ln['gross_amount'],
				$ln['vat_line_aed'],
				$ln['line_amount_aed'],
			));
		}
		epc_einvoice_log_event($db, $id, 'updated', $status, 'Invoice updated in ERP', array('admin_id' => $admin_id));
		$db->commit();
		return $id;
	} catch (Exception $e) {
		$db->rollBack();
		throw $e;
	}
}

function epc_erp_invoice_save(PDO $db, array $data, int $admin_id = 0): int
{
	epc_erp_invoices_ensure_schema($db);
	$lines = epc_erp_invoice_parse_lines_from_post($db, $data);
	if (empty($lines)) {
		throw new Exception('Add at least one line item');
	}
	$id = (int)($data['id'] ?? $data['invoice_id'] ?? 0);
	$doc = epc_erp_invoice_build_manual($db, $data, $lines);
	if ($id > 0) {
		$existing = epc_einvoice_get_document($db, $id);
		if ($existing) {
			$doc['uuid'] = $existing['uuid'];
			if (trim((string)($data['invoice_number'] ?? '')) === '') {
				$doc['invoice_number'] = $existing['invoice_number'];
			}
		}
		return epc_erp_invoice_update($db, $id, $doc, $admin_id);
	}
	return epc_einvoice_save_document($db, $doc, $admin_id);
}

function epc_erp_invoice_from_order(PDO $db, int $orderId, array $opts = array(), int $admin_id = 0): int
{
	$built = epc_einvoice_build_from_order($db, $orderId, $opts);
	return epc_einvoice_save_document($db, $built, $admin_id);
}

function epc_erp_invoice_peppol_json(array $doc): string
{
	$payload = array(
		'ubl_version' => '2.1',
		'profile' => $doc['business_process'] ?? 'urn:peppol:bis:billing',
		'specification' => $doc['specification_id'] ?? 'urn:peppol:pint:billing-1@ae-1',
		'invoice' => array(
			'uuid' => $doc['uuid'],
			'number' => $doc['invoice_number'],
			'type_code' => $doc['invoice_type_code'],
			'issue_date' => date('Y-m-d', (int)$doc['issue_date']),
			'due_date' => date('Y-m-d', (int)$doc['payment_due_date']),
			'currency' => $doc['currency_code'],
			'transaction_type' => $doc['transaction_type_code'],
			'payment_terms' => $doc['payment_terms'],
			'seller' => $doc['seller'],
			'buyer' => $doc['buyer'],
			'lines' => $doc['lines'],
			'totals' => array(
				'subtotal_ex_vat' => (float)$doc['subtotal_ex_vat'],
				'vat' => (float)$doc['total_vat'],
				'total_incl_vat' => (float)$doc['total_incl_vat'],
				'amount_due' => (float)$doc['amount_due'],
			),
			'tax_breakdown' => $doc['tax_breakdown'],
		),
		'status' => $doc['status'],
		'validation_ok' => !empty($doc['validation_ok']),
	);
	return json_encode($payload, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
}

function epc_erp_invoice_print_html(array $doc): string
{
	$seller = $doc['seller'];
	$buyer = $doc['buyer'];
	$company = htmlspecialchars((string)($seller['seller_name'] ?? 'Company'), ENT_QUOTES, 'UTF-8');
	$html = '<!DOCTYPE html><html><head><meta charset="utf-8"><title>Tax Invoice ' . htmlspecialchars($doc['invoice_number'], ENT_QUOTES, 'UTF-8') . '</title>
	<style>
	body{font-family:"Segoe UI",Arial,sans-serif;margin:32px;color:#0f172a;font-size:13px;}
	.hdr{border-bottom:3px solid #1d4ed8;padding-bottom:16px;margin-bottom:24px;}
	.hdr h1{margin:0;font-size:22px;color:#1d4ed8;}
	.grid{display:flex;gap:24px;margin-bottom:20px;}
	.box{flex:1;}
	.box h3{margin:0 0 8px;font-size:11px;text-transform:uppercase;color:#64748b;}
	table{width:100%;border-collapse:collapse;margin:16px 0;}
	th,td{border:1px solid #e2e8f0;padding:8px;text-align:left;}
	th{background:#f8fafc;font-size:11px;text-transform:uppercase;}
	.totals{max-width:320px;margin-left:auto;}
	.totals td{border:none;padding:4px 8px;}
	.totals tr.total td{font-weight:bold;font-size:15px;border-top:2px solid #1d4ed8;}
	.bc-proof{margin-top:24px;padding:12px 14px;border:1px solid #bfdbfe;background:#eff6ff;border-radius:6px;font-size:12px;}
	.bc-proof strong{color:#1d4ed8;}
	.bc-proof a{color:#1d4ed8;word-break:break-all;}
	.foot{margin-top:40px;font-size:11px;color:#64748b;}
	@media print{.no-print{display:none;}}
	</style></head><body>';
	$html .= '<div class="no-print" style="margin-bottom:16px;"><button onclick="window.print()">Print</button></div>';
	$html .= '<div class="hdr"><h1>TAX INVOICE</h1><p style="margin:4px 0 0;color:#64748b;">UAE Federal Tax Authority · e-Invoice (PINT-AE) · '
		. htmlspecialchars($doc['invoice_number'], ENT_QUOTES, 'UTF-8') . '</p></div>';
	$sellerTrn = htmlspecialchars($seller['seller_trn'] ?? '—', ENT_QUOTES, 'UTF-8');
	$sellerAddr = htmlspecialchars(trim(($seller['seller_address_line1'] ?? '') . ', ' . ($seller['seller_city'] ?? '') . ', ' . ($seller['seller_emirate'] ?? '') . ', ' . ($seller['seller_country_code'] ?? 'AE'), ' ,'), ENT_QUOTES, 'UTF-8');
	$sellerReg = trim((string)($seller['seller_legal_reg_type'] ?? '') . ' ' . (string)($seller['seller_legal_reg_no'] ?? ''));
	$html .= '<div class="grid"><div class="box"><h3>Seller</h3><strong>' . $company . '</strong><br>TRN: ' . $sellerTrn
		. ($sellerReg !== '' ? '<br>Legal reg: ' . htmlspecialchars($sellerReg, ENT_QUOTES, 'UTF-8') : '')
		. '<br>' . $sellerAddr
		. (!empty($seller['seller_phone']) ? '<br>Tel: ' . htmlspecialchars((string)$seller['seller_phone'], ENT_QUOTES, 'UTF-8') : '')
		. (!empty($seller['seller_email']) ? '<br>' . htmlspecialchars((string)$seller['seller_email'], ENT_QUOTES, 'UTF-8') : '')
		. '</div>';
	$buyerTrn = trim((string)($buyer['buyer_trn'] ?? ''));
	$buyerAddr = htmlspecialchars(trim(($buyer['buyer_address_line1'] ?? '') . ', ' . ($buyer['buyer_city'] ?? '') . ', ' . ($buyer['buyer_emirate'] ?? '') . ', ' . ($buyer['buyer_country_code'] ?? 'AE'), ' ,'), ENT_QUOTES, 'UTF-8');
	$html .= '<div class="box"><h3>Buyer</h3><strong>' . htmlspecialchars($buyer['buyer_name'] ?? '—', ENT_QUOTES, 'UTF-8') . '</strong><br>TRN: '
		. htmlspecialchars($buyerTrn !== '' ? $buyerTrn : 'Not registered / B2C', ENT_QUOTES, 'UTF-8') . '<br>' . $buyerAddr
		. (!empty($buyer['buyer_phone']) ? '<br>Tel: ' . htmlspecialchars((string)$buyer['buyer_phone'], ENT_QUOTES, 'UTF-8') : '')
		. (!empty($buyer['buyer_email']) ? '<br>' . htmlspecialchars((string)$buyer['buyer_email'], ENT_QUOTES, 'UTF-8') : '')
		. '</div>';
	$html .= '<div class="box"><h3>Invoice</h3>Issue date: ' . date('Y-m-d', (int)$doc['issue_date'])
		. '<br>Supply / VAT point: ' . date('Y-m-d', (int)($doc['vat_point_date'] ?? $doc['issue_date']))
		. '<br>Due: ' . date('Y-m-d', (int)$doc['payment_due_date'])
		. '<br>Currency: ' . htmlspecialchars($doc['currency_code'], ENT_QUOTES, 'UTF-8')
		. '<br>Type code: ' . htmlspecialchars((string)($doc['invoice_type_code'] ?? '380'), ENT_QUOTES, 'UTF-8') . ' (Tax Invoice)';
	if ((int)$doc['order_id'] > 0) {
		$html .= '<br>Order #' . (int)$doc['order_id'];
	}
	$html .= '</div></div>';
	$html .= '<p style="font-size:12px;color:#475569;margin:0 0 12px;">This is a Tax Invoice for UAE VAT purposes. Seller TRN must appear on all tax invoices. Retain for minimum 5 years.</p>';
	$html .= '<table><thead><tr><th>#</th><th>Description</th><th>Qty</th><th>Unit</th><th>Net</th><th>VAT %</th><th>VAT</th><th>Gross</th></tr></thead><tbody>';
	foreach ($doc['lines'] as $ln) {
		$html .= '<tr><td>' . (int)$ln['line_no'] . '</td><td>' . htmlspecialchars($ln['item_name'], ENT_QUOTES, 'UTF-8') . '</td>';
		$html .= '<td>' . htmlspecialchars(number_format((float)$ln['quantity'], 2), ENT_QUOTES, 'UTF-8') . '</td>';
		$html .= '<td>' . number_format((float)$ln['unit_price'], 2) . '</td><td>' . number_format((float)$ln['line_net'], 2) . '</td>';
		$html .= '<td>' . number_format((float)$ln['tax_rate'], 2) . '</td><td>' . number_format((float)$ln['vat_line_aed'], 2) . '</td>';
		$html .= '<td>' . number_format((float)$ln['gross_amount'], 2) . '</td></tr>';
	}
	$html .= '</tbody></table>';
	$html .= '<table class="totals"><tr><td>Subtotal ex VAT</td><td style="text-align:right;">' . number_format((float)$doc['subtotal_ex_vat'], 2) . ' ' . htmlspecialchars($doc['currency_code'], ENT_QUOTES, 'UTF-8') . '</td></tr>';
	$html .= '<tr><td>VAT</td><td style="text-align:right;">' . number_format((float)$doc['total_vat'], 2) . '</td></tr>';
	$html .= '<tr class="total"><td>Total incl. VAT</td><td style="text-align:right;">' . number_format((float)$doc['total_incl_vat'], 2) . '</td></tr>';
	$html .= '<tr><td>Amount due</td><td style="text-align:right;"><strong>' . number_format((float)$doc['amount_due'], 2) . '</strong></td></tr></table>';
	if (!empty($doc['payment_terms'])) {
		$html .= '<p><strong>Payment terms:</strong> ' . htmlspecialchars($doc['payment_terms'], ENT_QUOTES, 'UTF-8') . '</p>';
	}

	// Blockchain BOS verify block (best-effort; silent if no proof).
	try {
		$bcFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
		if (is_file($bcFile)) {
			require_once $bcFile;
			$siteKey = epc_bc_bos_resolve_site_key();
			if ($siteKey !== '' && epc_bc_bos_tenant_mode($siteKey) !== 'off') {
				list($bcType, $bcId) = epc_bc_bos_einvoice_record_keys($doc);
				$proof = epc_bc_bos_lookup_proof($siteKey, $bcType, $bcId);
				if ($proof && !empty($proof['proof_uid'])) {
					$verifyAbs = epc_bc_bos_verify_url_absolute((string)$proof['proof_uid']);
					$status = htmlspecialchars((string)($proof['status'] ?? 'pending'), ENT_QUOTES, 'UTF-8');
					$html .= '<div class="bc-proof"><strong>Blockchain BOS proof</strong> · status: '
						. $status . '<br>Proof ID: <code>'
						. htmlspecialchars((string)$proof['proof_uid'], ENT_QUOTES, 'UTF-8')
						. '</code><br>Verify authenticity: <a href="'
						. htmlspecialchars($verifyAbs, ENT_QUOTES, 'UTF-8') . '">'
						. htmlspecialchars($verifyAbs, ENT_QUOTES, 'UTF-8') . '</a></div>';
				}
			}
		}
	} catch (Throwable $e) {
		// never break print
	}

	$html .= '<div class="foot">UAE FTA Tax Invoice / e-Invoice (PINT-AE) · Specification '
		. htmlspecialchars((string)($doc['specification_id'] ?? 'urn:peppol:pint:billing-1@ae-1'), ENT_QUOTES, 'UTF-8')
		. '<br>UUID ' . htmlspecialchars((string)($doc['uuid'] ?? ''), ENT_QUOTES, 'UTF-8')
		. ' · Retain records for minimum 5 years as required by UAE law.</div></body></html>';
	return $html;
}
