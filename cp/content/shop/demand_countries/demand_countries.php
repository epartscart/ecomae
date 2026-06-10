<?php
/**
 * CP: upload demand-by-country CSV (brand + article + ISO3 countries).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$backend = isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp';
$uploadUrl = '/' . $backend . '/content/shop/demand_countries/ajax_epc_demand_upload_tmp.php';
$importUrl = '/' . $backend . '/content/shop/demand_countries/ajax_epc_demand_csv.php';
$sampleUrl = '/' . $backend . '/content/shop/demand_countries/epc_demand_countries_sample.csv';
?>
<style>
.epc-dc-panel { max-width: 960px; }
.epc-dc-panel pre { background: #f7f9fa; border: 1px solid #e4e5e7; padding: 12px; font-size: 12px; }
.epc-dc-stats { margin-top: 12px; }
.epc-dc-preview table { font-size: 13px; }
.epc-dc-preview .text-danger { color: #c0392b; }
</style>
<div class="hpanel epc-dc-panel">
	<div class="panel-heading hbuilt">Demand countries CSV</div>
	<div class="panel-body">
		<p>Upload planning demand tags by <strong>brand + article</strong>. Stock is still read from the UAE pool; these ISO&nbsp;3166-1 <strong>alpha-3</strong> codes drive Country Intelligence results.</p>
		<h4>CSV format</h4>
		<ul>
			<li>Required columns: <code>brand</code> (or <code>manufacturer</code>), <code>article</code></li>
			<li>Countries either in one column <code>countries</code> as <code>SDN,DZA,KEN</code> <em>or</em> separate columns <code>A</code>, <code>B</code>, <code>C</code>, … with one code each</li>
			<li>Use 3-letter codes only: SDN, DZA, KEN, EGY, NGA, SAU (not SD, DZ). ARE is UAE stock — do not use as demand.</li>
		</ul>
		<pre>brand,article,A,B,C
TOYOTA,1310154101,SDN,DZA,KEN
BOSCH,0986424590,SDN,,

brand,article,countries
MANN,W712/75,"SDN,DZA"</pre>
		<p><a class="btn btn-default btn-sm" href="<?php echo htmlspecialchars($sampleUrl, ENT_QUOTES, 'UTF-8'); ?>" download>Download sample CSV</a></p>
		<hr>
		<div class="form-group">
			<label>CSV file</label>
			<input type="file" id="epc-dc-file" accept=".csv,text/csv" class="form-control">
		</div>
		<div class="form-group">
			<label>Import mode</label>
			<select id="epc-dc-mode" class="form-control" style="max-width:320px;">
				<option value="merge">Merge — add/update tags, keep other countries on the part</option>
				<option value="replace">Replace — for each row, set only the countries listed (removes other tags for that brand+article)</option>
			</select>
		</div>
		<button type="button" class="btn btn-primary" id="epc-dc-preview-btn">Preview</button>
		<button type="button" class="btn btn-success" id="epc-dc-import-btn" disabled>Import</button>
		<div id="epc-dc-msg" class="alert" style="display:none;margin-top:12px;"></div>
		<div id="epc-dc-preview" class="epc-dc-preview" style="display:none;margin-top:16px;"></div>
		<div id="epc-dc-stats" class="epc-dc-stats"></div>
	</div>
</div>
<script>
(function () {
	var fileInput = document.getElementById('epc-dc-file');
	var modeSel = document.getElementById('epc-dc-mode');
	var previewBtn = document.getElementById('epc-dc-preview-btn');
	var importBtn = document.getElementById('epc-dc-import-btn');
	var msgBox = document.getElementById('epc-dc-msg');
	var previewBox = document.getElementById('epc-dc-preview');
	var statsBox = document.getElementById('epc-dc-stats');
	var uploadUrl = <?php echo json_encode($uploadUrl); ?>;
	var importUrl = <?php echo json_encode($importUrl); ?>;
	var uploadedPath = '';

	function showMsg(text, ok) {
		msgBox.style.display = 'block';
		msgBox.className = 'alert alert-' + (ok ? 'success' : 'danger');
		msgBox.textContent = text;
	}

	function uploadFile(cb) {
		if (!fileInput.files || !fileInput.files[0]) {
			showMsg('Choose a CSV file first.', false);
			return;
		}
		var fd = new FormData();
		fd.append('csv_file', fileInput.files[0]);
		var xhr = new XMLHttpRequest();
		xhr.open('POST', uploadUrl, true);
		xhr.onload = function () {
			var data;
			try { data = JSON.parse(xhr.responseText); } catch (e) { showMsg('Upload failed.', false); return; }
			if (!data || !data.status) {
				showMsg((data && data.message) ? data.message : 'Upload failed.', false);
				return;
			}
			uploadedPath = data.file_full_path;
			cb();
		};
		xhr.send(fd);
	}

	function renderPreview(rows) {
		if (!rows || !rows.length) {
			previewBox.innerHTML = '<p>No data rows found.</p>';
			previewBox.style.display = 'block';
			return;
		}
		var html = '<table class="table table-bordered table-striped"><thead><tr><th>Line</th><th>Brand</th><th>Article</th><th>Countries</th><th>Check</th></tr></thead><tbody>';
		rows.forEach(function (r) {
			var err = (r.errors && r.errors.length) ? r.errors.join('; ') : 'OK';
			var cls = (r.errors && r.errors.length) ? 'text-danger' : '';
			html += '<tr><td>' + r.line + '</td><td>' + escapeHtml(r.brand) + '</td><td>' + escapeHtml(r.article) + '</td><td>' + escapeHtml(r.countries) + '</td><td class="' + cls + '">' + escapeHtml(err) + '</td></tr>';
		});
		html += '</tbody></table>';
		previewBox.innerHTML = html;
		previewBox.style.display = 'block';
	}

	function escapeHtml(s) {
		return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
	}

	function callImport(action) {
		uploadFile(function () {
			var xhr = new XMLHttpRequest();
			xhr.open('POST', importUrl, true);
			xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
			xhr.onload = function () {
				var data;
				try { data = JSON.parse(xhr.responseText); } catch (e) { showMsg('Request failed.', false); return; }
				if (!data || !data.status) {
					showMsg((data && data.message) ? data.message : 'Failed.', false);
					return;
				}
				if (action === 'preview') {
					renderPreview(data.rows || []);
					importBtn.disabled = false;
					showMsg('Preview ready. Check rows, then Import.', true);
				} else {
					var s = data.stats || {};
					statsBox.innerHTML = '<p><strong>Rows read:</strong> ' + (s.rows_read || 0)
						+ ' · <strong>OK:</strong> ' + (s.rows_ok || 0)
						+ ' · <strong>Skipped:</strong> ' + (s.rows_skipped || 0)
						+ ' · <strong>Tags written:</strong> ' + (s.tags_inserted || 0) + '</p>';
					showMsg(data.message || 'Import complete.', true);
				}
			};
			xhr.send('action=' + encodeURIComponent(action)
				+ '&file_full_path=' + encodeURIComponent(uploadedPath)
				+ '&mode=' + encodeURIComponent(modeSel.value));
		});
	}

	previewBtn.addEventListener('click', function () { callImport('preview'); });
	importBtn.addEventListener('click', function () {
		if (!confirm('Import demand tags from this CSV?')) { return; }
		callImport('import');
	});
})();
</script>
