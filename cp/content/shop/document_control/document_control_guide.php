<?php
/**
 * Document Control — operator guide (English).
 */
defined('_ASTEXE_') or die('No access');
?>

<div class="well">
	<h3><i class="fa fa-book"></i> Document Control System — Guide</h3>
	<p>Industrial-grade English document management for e-world Commerce System. Replaces the legacy Russian print module with FTA-ready templates and attachment storage.</p>
</div>

<h4>1. Company profile</h4>
<ol>
	<li>Open <strong>Company profile</strong> and enter legal name, full address, <strong>TRN</strong> (15-digit UAE VAT registration), phone, email, and bank IBAN.</li>
	<li>Upload your <strong>company logo</strong> (PNG/JPG). It appears on every printed document.</li>
	<li>Set the <strong>Legal footer</strong> — FTA retention notice, terms, and disclaimers shown on all documents.</li>
	<li>Optional: click <strong>Import from E-Invoicing</strong> to copy seller details already configured in ERP → E-Invoicing.</li>
</ol>

<h4>2. Document templates</h4>
<p>Four default templates are pre-installed:</p>
<ul>
	<li><strong>FTA Tax Invoice</strong> — mandatory fields for UAE VAT: supplier TRN, buyer TRN (if registered), invoice number &amp; date, line-level net/VAT/total, amount in words.</li>
	<li><strong>Packing Slip</strong> — warehouse picking list (no tax amounts).</li>
	<li><strong>Delivery Note</strong> — customer sign-off block for proof of delivery.</li>
	<li><strong>Payment Receipt</strong> — records amount received, method, and reference.</li>
</ul>
<p>Templates use HTML with placeholders such as <code>{{company_trn}}</code>, <code>{{lines_table}}</code>, <code>{{legal_footer}}</code>. Edit header, body, footer, and CSS directly — changes apply immediately to new prints.</p>

<h4>3. Printing documents</h4>
<ol>
	<li>Go to <strong>Print documents</strong>.</li>
	<li>Find the order and click the document type (opens in a new tab).</li>
	<li>Use the browser <strong>Print</strong> button or Ctrl+P. Save as PDF for email/archive.</li>
</ol>
<p>Invoice numbers prefer the e-invoice number from ERP if one exists; otherwise format <code>INV-000123</code>.</p>

<h4>4. Supplier &amp; other attachments</h4>
<ol>
	<li>Open <strong>Attachments</strong>.</li>
	<li>Enter order ID, choose category (e.g. <strong>Supplier purchase invoice</strong>), supplier name, reference, and upload PDF/image.</li>
	<li>Files are stored securely and linked to the order for audit trail (input VAT support).</li>
</ol>

<h4>5. FTA compliance checklist</h4>
<table class="table table-bordered">
	<thead><tr><th>Requirement</th><th>Where configured</th></tr></thead>
	<tbody>
		<tr><td>Supplier TRN on tax invoice</td><td>Company profile → TRN</td></tr>
		<tr><td>Buyer TRN (if VAT registered)</td><td>Customers → E-invoice buyer profile</td></tr>
		<tr><td>Unique invoice number &amp; date</td><td>Auto from order / e-invoice</td></tr>
		<tr><td>VAT rate &amp; amount</td><td>Finance → VAT settings (default 5%)</td></tr>
		<tr><td>Line item description &amp; value</td><td>Order lines</td></tr>
		<tr><td>5-year record retention</td><td>Legal footer + attachment storage</td></tr>
	</tbody>
</table>

<h4>6. Legacy module</h4>
<p>The old Russian module (<code>/cp/shop/modul-pechati-dokumentov</code>) redirects to this panel. Russian TORG-12 / UPD forms remain in the database for reference but are not recommended for UAE operations.</p>

<h4>7. Support &amp; deployment</h4>
<p>Setup script (server): <code>/epc-document-control-cp-setup.php?token=…</code></p>
<p>Designed by <strong>Electronic World Group</strong> for the <strong>e-world Commerce System</strong>.</p>
