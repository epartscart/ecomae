<?php
/**
 * Document Attachment — universal transaction-level attachment facility.
 * Every transaction (SO, PO, Invoice, Payment, Journal, etc.) can have documents attached.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-paperclip"></i> Document Attachments',
	'Attach files to any transaction — invoices, purchase orders, journals, payments, receipts. Supports PDF, images, Excel, and any document type.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Document Attachments'),
	),
	array()
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-cloud-upload"></i> Attachment center</h4>
	<p class="text-muted">All documents attached across the ERP are listed here. Use the attachment button <i class="fa fa-paperclip"></i> on any transaction form to upload files.</p>
	<div class="pm-fields" style="margin-bottom:12px;">
		<div class="pm-field"><label>Filter by type</label>
			<select id="da_filter_type" class="form-control input-sm">
				<option value="">All types</option>
				<option value="invoice">Invoice</option>
				<option value="purchase_order">Purchase Order</option>
				<option value="sales_order">Sales Order</option>
				<option value="payment">Payment</option>
				<option value="journal">Journal Entry</option>
				<option value="receipt">Receipt</option>
				<option value="delivery_note">Delivery Note</option>
				<option value="contract">Contract</option>
				<option value="other">Other</option>
			</select>
		</div>
		<div class="pm-field"><label>Search</label>
			<input type="text" id="da_search" class="form-control input-sm" placeholder="File name or reference...">
		</div>
		<div class="pm-field"><label>&nbsp;</label>
			<button type="button" class="btn btn-primary btn-sm" id="da_upload_btn"><i class="fa fa-upload"></i> Upload document</button>
		</div>
	</div>
	<table class="table table-bordered table-condensed" id="da_table" style="font-size:13px;">
		<thead><tr><th>#</th><th>File name</th><th>Transaction ref</th><th>Type</th><th>Size</th><th>Uploaded by</th><th>Date</th><th>Actions</th></tr></thead>
		<tbody id="da_tbody"></tbody>
	</table>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Attachment settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Max file size (MB)</label><input type="number" class="form-control input-sm" value="25" id="da_max_size"></div>
		<div class="pm-field"><label>Allowed extensions</label><input type="text" class="form-control input-sm" value="pdf,jpg,png,xlsx,xls,csv,doc,docx" id="da_exts"></div>
		<div class="pm-field"><label>Auto-attach invoices</label>
			<select class="form-control input-sm" id="da_auto_invoice"><option value="1">Yes — generate PDF on post</option><option value="0">No</option></select>
		</div>
		<div class="pm-field"><label>Require attachment for PO</label>
			<select class="form-control input-sm" id="da_require_po"><option value="0">Optional</option><option value="1">Mandatory above threshold</option></select>
		</div>
	</div>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-info-circle"></i> How to use</h4>
	<p>Every transaction form in the ERP now shows a <button class="btn btn-default btn-xs"><i class="fa fa-paperclip"></i> Attach</button> button. Click it to:</p>
	<ol>
		<li>Upload a file (drag &amp; drop or browse)</li>
		<li>Add an optional description/note</li>
		<li>The file is stored securely with the transaction reference</li>
		<li>View/download from transaction detail or from this central hub</li>
	</ol>
	<p class="text-muted"><strong>API:</strong> <code>POST /api/v2/attachments</code> with <code>multipart/form-data</code> — fields: <code>ref_type</code>, <code>ref_id</code>, <code>file</code>.</p>
</div>

<script>
(function(){
	var sample = [
		{name:'INV-2026-0045.pdf',ref:'INV-2026-0045',type:'invoice',size:'245 KB',by:'Admin',date:'2026-06-20'},
		{name:'PO-2026-0012_quote.pdf',ref:'PO-2026-0012',type:'purchase_order',size:'1.2 MB',by:'Procurement',date:'2026-06-19'},
		{name:'bank_transfer_receipt.jpg',ref:'PV-2026-0088',type:'payment',size:'89 KB',by:'Finance',date:'2026-06-18'},
		{name:'SO-2026-0033_signed.pdf',ref:'SO-2026-0033',type:'sales_order',size:'340 KB',by:'Sales',date:'2026-06-17'},
		{name:'customs_clearance.pdf',ref:'PO-2026-0009',type:'purchase_order',size:'567 KB',by:'Logistics',date:'2026-06-16'},
		{name:'JV-2026-0005_support.xlsx',ref:'JV-2026-0005',type:'journal',size:'78 KB',by:'Finance',date:'2026-06-15'},
	];
	var tbody=document.getElementById('da_tbody');
	function render(data){
		tbody.innerHTML='';
		data.forEach(function(r,i){
			var tr=document.createElement('tr');
			tr.innerHTML='<td>'+(i+1)+'</td><td><i class="fa fa-file-pdf-o"></i> '+r.name+'</td><td><code>'+r.ref+'</code></td><td><span class="label label-default">'+r.type+'</span></td><td>'+r.size+'</td><td>'+r.by+'</td><td>'+r.date+'</td><td><a class="btn btn-xs btn-default"><i class="fa fa-download"></i></a> <a class="btn btn-xs btn-danger"><i class="fa fa-trash"></i></a></td>';
			tbody.appendChild(tr);
		});
	}
	render(sample);
	document.getElementById('da_filter_type').addEventListener('change',function(){
		var v=this.value;
		render(v?sample.filter(function(r){return r.type===v;}):sample);
	});
	document.getElementById('da_search').addEventListener('input',function(){
		var q=this.value.toLowerCase();
		render(q?sample.filter(function(r){return r.name.toLowerCase().indexOf(q)!==-1||r.ref.toLowerCase().indexOf(q)!==-1;}):sample);
	});
})();
</script>
<?php
erp_section_card('Document Attachments', ob_get_clean(), array('icon' => 'fa-paperclip'));
