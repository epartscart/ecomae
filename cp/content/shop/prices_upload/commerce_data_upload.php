<?php
/**
 * CP page: Commerce data upload → warehouse price lists (*-S / *.P / *-L).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_commerce_price_ingest.php';

$user_session = DP_User::getAdminSession();
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');

$sources = array();
try {
	$sources = epc_commerce_list_sources($db_link, false);
} catch (Throwable $e) {
	$sources = array();
}
?>
<div class="row">
	<div class="col-lg-12">
		<h2 style="margin-top:0;">Commerce data → warehouse price lists</h2>
		<p class="text-muted">
			Upload real sales, purchase, or inventory Excel/CSV files. They become Docpart warehouse price lists
			shown in storefront search. Recurring updates: re-upload the same file type, or set a file URL and refresh later (cron / button).
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
			<div class="panel-heading"><h4 class="panel-title" style="margin:0;">Upload file or connect URL</h4></div>
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
							<small class="text-muted">Purchase &amp; inventory only (saved for URL refresh)</small>
						</div>
						<div class="col-md-4">
							<label>Excel / CSV file <span class="text-muted">(optional if URL set)</span></label>
							<input class="form-control" type="file" name="price_file" id="epcCommerceFile" accept=".csv,.txt,.xls,.xlsx" />
						</div>
					</div>
					<div class="row" style="margin-top:12px;">
						<div class="col-md-8">
							<label>Recurring file URL (Google Drive / Dropbox / HTTPS link to Excel or CSV)</label>
							<input class="form-control" type="url" name="source_url" id="epcCommerceUrl" placeholder="https://…/sales.xlsx" />
							<small class="text-muted">Stored on the price list. Re-import without uploading: Refresh below, or cron <code>action=refresh_all</code>.</small>
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
			<div class="panel-heading" style="display:flex;align-items:center;justify-content:space-between;">
				<h4 class="panel-title" style="margin:0;">Linked commerce lists (recurring)</h4>
				<span>
					<button type="button" class="btn btn-xs btn-default" id="epcCommerceReloadSources"><i class="fa fa-refresh"></i> Reload</button>
					<button type="button" class="btn btn-xs btn-warning" id="epcCommerceRefreshAll"><i class="fa fa-cloud-download"></i> Refresh all URLs</button>
				</span>
			</div>
			<div class="panel-body">
				<p class="text-muted">Lists created by this module. If a URL is stored, Refresh re-downloads Excel/CSV and rebuilds the warehouse price list (margin remembered).</p>
				<div class="table-responsive">
					<table class="table table-striped table-condensed" id="epcCommerceSourcesTable">
						<thead>
							<tr>
								<th>List</th>
								<th>Role</th>
								<th>Margin</th>
								<th>URL</th>
								<th>Updated</th>
								<th></th>
							</tr>
						</thead>
						<tbody>
						<?php if (count($sources) === 0): ?>
							<tr><td colspan="6" class="text-muted">No commerce lists yet — import a sales / purchase / inventory file above.</td></tr>
						<?php else: ?>
							<?php foreach ($sources as $src): ?>
								<tr data-price-id="<?php echo (int) $src['price_id']; ?>">
									<td>
										<strong><?php echo htmlspecialchars((string) $src['price_name'], ENT_QUOTES, 'UTF-8'); ?></strong>
										<br><small class="text-muted">#<?php echo (int) $src['price_id']; ?></small>
									</td>
									<td><?php echo htmlspecialchars((string) $src['role'], ENT_QUOTES, 'UTF-8'); ?></td>
									<td><?php echo htmlspecialchars((string) $src['margin_percent'], ENT_QUOTES, 'UTF-8'); ?>%</td>
									<td style="max-width:280px;word-break:break-all;">
										<?php if (!empty($src['has_url'])): ?>
											<small><a href="<?php echo htmlspecialchars((string) $src['link'], ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener"><?php echo htmlspecialchars((string) $src['link'], ENT_QUOTES, 'UTF-8'); ?></a></small>
										<?php else: ?>
											<span class="text-muted">— re-upload file —</span>
										<?php endif; ?>
									</td>
									<td>
										<?php
										$lu = (int) ($src['last_updated'] ?? 0);
										echo $lu > 0 ? htmlspecialchars(date('Y-m-d H:i', $lu), ENT_QUOTES, 'UTF-8') : '—';
										?>
									</td>
									<td class="text-right">
										<?php if (!empty($src['has_url'])): ?>
											<button type="button" class="btn btn-xs btn-primary epc-commerce-refresh-one" data-price-id="<?php echo (int) $src['price_id']; ?>">Refresh</button>
										<?php endif; ?>
										<a class="btn btn-xs btn-default" href="/<?php echo $backend; ?>/shop/prices/price?price_id=<?php echo (int) $src['price_id']; ?>">Open</a>
									</td>
								</tr>
							<?php endforeach; ?>
						<?php endif; ?>
						</tbody>
					</table>
				</div>
				<div id="epcCommerceRefreshResult" style="margin-top:10px;"></div>
			</div>
		</div>

		<div class="hpanel">
			<div class="panel-heading"><h4 class="panel-title" style="margin:0;">Expected columns (header row)</h4></div>
			<div class="panel-body">
				<p>Headers are matched flexibly (Brand/Manufacturer, Article/SKU/Number, Name/Description, Qty/Stock, Price/Sales price, Cost/Purchase, Supplier/Vendor).</p>
				<pre style="background:#f8fafc;border:1px solid #e2e8f0;padding:12px;border-radius:6px;">Brand,Article,Name,Qty,Price
Brand,Article,Name,Qty,Cost,Supplier
Brand,Article,Name,Stock,Cost</pre>
				<p><strong>Cron example</strong> (after deploy): refresh every linked Excel URL nightly:</p>
				<pre style="background:#f8fafc;border:1px solid #e2e8f0;padding:12px;border-radius:6px;">wget -q -O /dev/null 'https://www.epartscart.com/epc-upload-commerce-prices.php?token=…&amp;key=TECH_KEY&amp;action=refresh_all'</pre>
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
	var ajaxUrl = '/<?php echo $backend; ?>/content/shop/prices_upload/ajax_epc_commerce_ingest.php';
	var csrf = '<?php echo $csrf; ?>';
	var btn = document.getElementById('epcCommerceSubmit');
	var out = document.getElementById('epcCommerceResult');
	var refreshOut = document.getElementById('epcCommerceRefreshResult');

	function renderLists(lists) {
		var html = '<ul>';
		(lists || []).forEach(function(item){
			html += '<li><strong>' + (item.price_name || '') + '</strong> — ' +
				(item.status ? 'OK' : 'FAIL') +
				', imported ' + (item.records_handled || 0) +
				', in DB ' + (item.records_in_db || 0) +
				(item.price_id ? ' (price_id=' + item.price_id + ')' : '') +
				'</li>';
		});
		html += '</ul><p><a href="/<?php echo $backend; ?>/shop/prices">Open price lists</a></p>';
		return html;
	}

	if (btn) {
		btn.addEventListener('click', function(){
			var fileInput = document.getElementById('epcCommerceFile');
			var url = (document.getElementById('epcCommerceUrl').value || '').trim();
			var hasFile = fileInput && fileInput.files && fileInput.files.length;
			if (!hasFile && !url) {
				out.innerHTML = '<div class="alert alert-warning">Choose a file or paste a recurring file URL.</div>';
				return;
			}
			var fd = new FormData();
			fd.append('csrf_guard_key', csrf);
			fd.append('action', 'upload');
			fd.append('role', document.getElementById('epcCommerceRole').value);
			fd.append('base_name', document.getElementById('epcCommerceBase').value || 'EPC');
			fd.append('margin_percent', document.getElementById('epcCommerceMargin').value || '0');
			fd.append('source_url', url);
			if (hasFile) {
				fd.append('price_file', fileInput.files[0]);
			}
			btn.disabled = true;
			out.innerHTML = '<div class="alert alert-info"><i class="fas fa-spinner fa-pulse"></i> Importing…</div>';
			fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){
					btn.disabled = false;
					if (!j || !j.status) {
						out.innerHTML = '<div class="alert alert-danger">' + (j && j.message ? j.message : 'Import failed') + '</div>';
						return;
					}
					out.innerHTML = '<div class="alert alert-success">' + (j.message || 'OK') +
						' — source rows: ' + (j.source_rows || 0) +
						(j.ingest_mode ? ' (' + j.ingest_mode + ')' : '') +
						'</div>' + renderLists(j.lists);
					setTimeout(function(){ location.reload(); }, 1200);
				}).catch(function(){
					btn.disabled = false;
					out.innerHTML = '<div class="alert alert-danger">Network error</div>';
				});
		});
	}

	function postAction(fields, targetEl) {
		var fd = new FormData();
		fd.append('csrf_guard_key', csrf);
		Object.keys(fields).forEach(function(k){ fd.append(k, fields[k]); });
		targetEl.innerHTML = '<div class="alert alert-info"><i class="fas fa-spinner fa-pulse"></i> Working…</div>';
		return fetch(ajaxUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function(r){ return r.json(); });
	}

	document.querySelectorAll('.epc-commerce-refresh-one').forEach(function(el){
		el.addEventListener('click', function(){
			var id = el.getAttribute('data-price-id');
			el.disabled = true;
			postAction({ action: 'refresh_url', price_id: id }, refreshOut).then(function(j){
				el.disabled = false;
				if (!j || !j.status) {
					refreshOut.innerHTML = '<div class="alert alert-danger">' + (j && j.message ? j.message : 'Refresh failed') + '</div>';
					return;
				}
				refreshOut.innerHTML = '<div class="alert alert-success">' + (j.message || 'Refreshed') +
					' — rows ' + (j.source_rows || 0) + '</div>' + renderLists(j.lists || []);
				setTimeout(function(){ location.reload(); }, 1000);
			}).catch(function(){
				el.disabled = false;
				refreshOut.innerHTML = '<div class="alert alert-danger">Network error</div>';
			});
		});
	});

	var refreshAll = document.getElementById('epcCommerceRefreshAll');
	if (refreshAll) {
		refreshAll.addEventListener('click', function(){
			refreshAll.disabled = true;
			postAction({ action: 'refresh_all' }, refreshOut).then(function(j){
				refreshAll.disabled = false;
				if (!j) {
					refreshOut.innerHTML = '<div class="alert alert-danger">Refresh failed</div>';
					return;
				}
				var cls = j.status ? 'success' : 'warning';
				refreshOut.innerHTML = '<div class="alert alert-' + cls + '">' + (j.message || '') + '</div>';
				if (j.status) {
					setTimeout(function(){ location.reload(); }, 1000);
				}
			}).catch(function(){
				refreshAll.disabled = false;
				refreshOut.innerHTML = '<div class="alert alert-danger">Network error</div>';
			});
		});
	}

	var reloadBtn = document.getElementById('epcCommerceReloadSources');
	if (reloadBtn) {
		reloadBtn.addEventListener('click', function(){ location.reload(); });
	}
})();
</script>
