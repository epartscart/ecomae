<?php
/**
 * CP page: Commerce data upload → warehouse price lists (*-S / *.P / *-L).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');
?>
<div class="row">
	<div class="col-lg-12">
		<h2 style="margin-top:0;">Commerce data → warehouse price lists</h2>
		<p class="text-muted">
			Upload real sales, purchase, or inventory Excel/CSV files. They become Docpart warehouse price lists
			shown in storefront search. Recurring updates: re-upload the same file type, or set a file URL to refresh later.
		</p>

		<div class="row" style="margin-bottom:18px;">
			<div class="col-md-4">
				<div class="hpanel">
					<div class="panel-body">
						<h4><span class="label label-success">*-S</span> Sales</h4>
						<p>Highest <strong>sales price</strong> per brand+article becomes our shelf price. Quantities are summed.</p>
						<p class="text-muted"><small>List name: <code>BASE-S</code> (e.g. <code>MAIN-S</code>)</small></p>
					</div>
				</div>
			</div>
			<div class="col-md-4">
				<div class="hpanel">
					<div class="panel-body">
						<h4><span class="label label-warning">*.P</span> Purchase</h4>
						<p>One warehouse list per supplier. Shelf price = cost × (1 + margin%). Lowest cost wins on duplicates.</p>
						<p class="text-muted"><small>List name: <code>SUPPLIER.P</code></small></p>
					</div>
				</div>
			</div>
			<div class="col-md-4">
				<div class="hpanel">
					<div class="panel-body">
						<h4><span class="label label-info">*-L</span> Inventory / local</h4>
						<p>Stock quantities + cost/list price with margin. Creates/updates <code>BASE-L</code> warehouse list.</p>
						<p class="text-muted"><small>List name: <code>BASE-L</code></small></p>
					</div>
				</div>
			</div>
		</div>

		<div class="hpanel">
			<div class="panel-heading"><h4 class="panel-title" style="margin:0;">Upload file</h4></div>
			<div class="panel-body">
				<form id="epcCommerceIngestForm" enctype="multipart/form-data" onsubmit="return false;">
					<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
					<div class="row">
						<div class="col-md-3">
							<label>Data type</label>
							<select class="form-control" name="role" id="epcCommerceRole">
								<option value="sales">Sales → *-S</option>
								<option value="purchase">Purchase → *.P</option>
								<option value="inventory">Inventory → *-L</option>
							</select>
						</div>
						<div class="col-md-3">
							<label>Base name</label>
							<input class="form-control" type="text" name="base_name" id="epcCommerceBase" value="EPC" placeholder="MAIN or EPC" />
							<small class="text-muted">Used for <code>BASE-S</code> / <code>BASE-L</code>. Purchase uses Supplier column when present.</small>
						</div>
						<div class="col-md-2">
							<label>Margin %</label>
							<input class="form-control" type="number" step="0.01" min="0" name="margin_percent" id="epcCommerceMargin" value="0" />
							<small class="text-muted">Purchase &amp; inventory only</small>
						</div>
						<div class="col-md-4">
							<label>Excel / CSV file</label>
							<input class="form-control" type="file" name="price_file" id="epcCommerceFile" accept=".csv,.txt,.xls,.xlsx" />
						</div>
					</div>
					<div class="row" style="margin-top:12px;">
						<div class="col-md-8">
							<label>Optional recurring file URL</label>
							<input class="form-control" type="url" name="source_url" id="epcCommerceUrl" placeholder="https://…/sales.csv" />
							<small class="text-muted">Stored on the price list (<code>load_mode=URL</code>). Refresh via API <code>action=refresh_url</code> or Cron later.</small>
						</div>
						<div class="col-md-4" style="padding-top:22px;">
							<button type="button" class="btn btn-primary btn-block" id="epcCommerceSubmit">
								<i class="fa fa-upload"></i> Import to warehouse price lists
							</button>
						</div>
					</div>
				</form>
				<div id="epcCommerceResult" style="margin-top:16px;"></div>
			</div>
		</div>

		<div class="hpanel">
			<div class="panel-heading"><h4 class="panel-title" style="margin:0;">Expected columns (header row)</h4></div>
			<div class="panel-body">
				<p>Headers are matched flexibly (Brand/Manufacturer, Article/SKU/Number, Name/Description, Qty/Stock, Price/Sales price, Cost/Purchase, Supplier/Vendor).</p>
				<pre style="background:#f8fafc;border:1px solid #e2e8f0;padding:12px;border-radius:6px;">Brand,Article,Name,Qty,Price
Brand,Article,Name,Qty,Cost,Supplier
Brand,Article,Name,Stock,Cost</pre>
				<p>
					<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/prices">Back to price lists</a>
					<a class="btn btn-default" href="/<?php echo $backend; ?>/shop/prices/guide">Upload guide</a>
				</p>
			</div>
		</div>
	</div>
</div>
<script>
(function(){
	var btn = document.getElementById('epcCommerceSubmit');
	var out = document.getElementById('epcCommerceResult');
	if (!btn) return;
	btn.addEventListener('click', function(){
		var fileInput = document.getElementById('epcCommerceFile');
		if (!fileInput || !fileInput.files || !fileInput.files.length) {
			out.innerHTML = '<div class="alert alert-warning">Choose a file first.</div>';
			return;
		}
		var fd = new FormData();
		fd.append('csrf_guard_key', '<?php echo $csrf; ?>');
		fd.append('role', document.getElementById('epcCommerceRole').value);
		fd.append('base_name', document.getElementById('epcCommerceBase').value || 'EPC');
		fd.append('margin_percent', document.getElementById('epcCommerceMargin').value || '0');
		fd.append('source_url', document.getElementById('epcCommerceUrl').value || '');
		fd.append('price_file', fileInput.files[0]);
		btn.disabled = true;
		out.innerHTML = '<div class="alert alert-info"><i class="fas fa-spinner fa-pulse"></i> Importing…</div>';
		fetch('/<?php echo $backend; ?>/content/shop/prices_upload/ajax_epc_commerce_ingest.php', {
			method: 'POST',
			body: fd,
			credentials: 'same-origin'
		}).then(function(r){ return r.json(); }).then(function(j){
			btn.disabled = false;
			if (!j || !j.status) {
				out.innerHTML = '<div class="alert alert-danger">' + (j && j.message ? j.message : 'Import failed') + '</div>';
				return;
			}
			var html = '<div class="alert alert-success">' + (j.message || 'OK') +
				' — source rows: ' + (j.source_rows || 0) + '</div><ul>';
			(j.lists || []).forEach(function(item){
				html += '<li><strong>' + (item.price_name || '') + '</strong> — ' +
					(item.status ? 'OK' : 'FAIL') +
					', imported ' + (item.records_handled || 0) +
					', in DB ' + (item.records_in_db || 0) +
					(item.price_id ? ' (price_id=' + item.price_id + ')' : '') +
					'</li>';
			});
			html += '</ul><p><a href="/<?php echo $backend; ?>/shop/prices">Open price lists</a></p>';
			out.innerHTML = html;
		}).catch(function(err){
			btn.disabled = false;
			out.innerHTML = '<div class="alert alert-danger">Network error</div>';
		});
	});
})();
</script>
