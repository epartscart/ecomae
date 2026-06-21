<?php
/**
 * Default English document templates (FTA-aligned tax invoice + logistics docs).
 */
defined('_ASTEXE_') or die('No access');

function epc_doc_control_default_templates(): array
{
	$sharedCss = '
.epc-doc { font-family: Arial, Helvetica, sans-serif; font-size: 12px; color: #111; max-width: 900px; margin: 0 auto; }
.epc-doc table { width: 100%; border-collapse: collapse; }
.epc-doc th, .epc-doc td { border: 1px solid #ccc; padding: 6px 8px; vertical-align: top; }
.epc-doc th { background: #f4f4f4; text-align: left; }
.epc-doc .no-border td, .epc-doc .no-border th { border: none; }
.epc-doc .right { text-align: right; }
.epc-doc .title { font-size: 20px; font-weight: bold; margin: 0 0 8px; }
.epc-doc .muted { color: #555; font-size: 11px; }
.epc-doc .legal { font-size: 10px; color: #444; border-top: 1px solid #999; margin-top: 16px; padding-top: 8px; }
';

	$header = '<div class="epc-doc">
<table class="no-border"><tr>
<td width="55%"><img src="{{company_logo}}" alt="Logo" style="max-height:70px;max-width:220px;" onerror="this.style.display=\'none\'" />
<div class="title">{{company_legal_name}}</div>
<div>{{company_address}}</div>
<div>TRN: {{company_trn}} | Tel: {{company_phone}} | {{company_email}}</div>
</td>
<td width="45%" class="right">
<div class="title">{{document_title}}</div>
<div><strong>No:</strong> {{document_number}}</div>
<div><strong>Date:</strong> {{document_date}}</div>
<div><strong>Order ref:</strong> {{order_id}}</div>
</td></tr></table>';

	$footer = '<div class="legal">{{legal_footer}}</div></div>';

	$ftaInvoiceBody = '
<table class="no-border" style="margin:12px 0"><tr>
<td width="50%"><strong>Bill To</strong><br/>{{buyer_name}}<br/>{{buyer_address}}<br/>TRN: {{buyer_trn}}</td>
<td width="50%"><strong>Ship To</strong><br/>{{ship_to_name}}<br/>{{ship_to_address}}</td>
</tr></table>
<p class="muted">Tax Invoice — UAE Federal Tax Authority (FTA) compliant format. Supply date: {{supply_date}}</p>
{{lines_table}}
<table style="width:320px;margin-left:auto;margin-top:12px">
<tr><td>Subtotal (excl. VAT)</td><td class="right">{{subtotal_excl_vat}}</td></tr>
<tr><td>VAT ({{vat_rate}}%)</td><td class="right">{{vat_amount}}</td></tr>
<tr><th>Total (incl. VAT)</th><th class="right">{{total_incl_vat}}</th></tr>
</table>
<p><strong>Amount in words:</strong> {{amount_words}}</p>
<p><strong>Payment terms:</strong> {{payment_terms}}</p>
<p><strong>Bank:</strong> {{bank_name}} — IBAN: {{bank_iban}}</p>';

	$packingBody = '
<table class="no-border" style="margin:12px 0"><tr>
<td><strong>Customer</strong><br/>{{buyer_name}}<br/>{{buyer_address}}</td>
<td><strong>Delivery</strong><br/>{{ship_to_name}}<br/>{{ship_to_address}}<br/>Carrier: {{carrier}}<br/>Tracking: {{tracking_no}}</td>
</tr></table>
<p class="muted">Packing list — items included in this shipment (no tax values).</p>
{{lines_table_packing}}
<p><strong>Packages:</strong> {{package_count}} &nbsp; <strong>Total weight:</strong> {{total_weight}}</p>
<p><strong>Prepared by:</strong> {{prepared_by}} &nbsp; <strong>Checked by:</strong> _______________</p>';

	$deliveryBody = '
<table class="no-border" style="margin:12px 0"><tr>
<td><strong>Deliver To</strong><br/>{{ship_to_name}}<br/>{{ship_to_address}}<br/>Phone: {{ship_to_phone}}</td>
<td><strong>From</strong><br/>{{company_legal_name}}<br/>{{company_address}}</td>
</tr></table>
<p class="muted">Delivery note — proof of delivery. Not a tax invoice.</p>
{{lines_table_delivery}}
<p><strong>Vehicle / driver:</strong> {{driver_info}}</p>
<p><strong>Received by (name & signature):</strong> _________________________ &nbsp; Date: __________</p>
<p><strong>Condition notes:</strong> {{delivery_notes}}</p>';

	$receiptBody = '
<table class="no-border" style="margin:12px 0"><tr>
<td><strong>Received from</strong><br/>{{buyer_name}}<br/>{{buyer_address}}</td>
<td><strong>Payment for</strong><br/>Invoice / Order: {{document_number}}<br/>Order ID: {{order_id}}</td>
</tr></table>
<p class="muted">Official payment receipt — not a tax invoice unless marked as Tax Invoice.</p>
<table style="width:360px;margin:12px 0">
<tr><td>Amount received</td><td class="right"><strong>{{amount_received}}</strong></td></tr>
<tr><td>Method</td><td class="right">{{payment_method}}</td></tr>
<tr><td>Reference</td><td class="right">{{payment_reference}}</td></tr>
<tr><td>Date received</td><td class="right">{{payment_date}}</td></tr>
</table>
<p><strong>Amount in words:</strong> {{amount_words}}</p>
<p>Authorized signature: _________________________</p>';

	return array(
		array(
			'code' => 'fta_tax_invoice',
			'title' => 'FTA Tax Invoice',
			'description' => 'UAE FTA-compliant tax invoice with TRN, VAT breakdown, and line items.',
			'category' => 'sales',
			'sort_order' => 10,
			'header_html' => str_replace('{{document_title}}', 'TAX INVOICE', $header),
			'body_html' => $ftaInvoiceBody,
			'footer_html' => $footer,
			'css_extra' => $sharedCss,
		),
		array(
			'code' => 'packing_slip',
			'title' => 'Packing Slip',
			'description' => 'Shipment packing list with quantities and SKU references.',
			'category' => 'logistics',
			'sort_order' => 20,
			'header_html' => str_replace('{{document_title}}', 'PACKING SLIP', $header),
			'body_html' => $packingBody,
			'footer_html' => $footer,
			'css_extra' => $sharedCss,
		),
		array(
			'code' => 'delivery_note',
			'title' => 'Delivery Note',
			'description' => 'Proof-of-delivery document for warehouse and customer sign-off.',
			'category' => 'logistics',
			'sort_order' => 30,
			'header_html' => str_replace('{{document_title}}', 'DELIVERY NOTE', $header),
			'body_html' => $deliveryBody,
			'footer_html' => $footer,
			'css_extra' => $sharedCss,
		),
		array(
			'code' => 'payment_receipt',
			'title' => 'Payment Receipt',
			'description' => 'Customer payment acknowledgment with method and reference.',
			'category' => 'finance',
			'sort_order' => 40,
			'header_html' => str_replace('{{document_title}}', 'PAYMENT RECEIPT', $header),
			'body_html' => $receiptBody,
			'footer_html' => $footer,
			'css_extra' => $sharedCss,
		),
	);
}
