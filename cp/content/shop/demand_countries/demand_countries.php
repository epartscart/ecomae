<?php
/**
 * CP: upload demand-by-country CSV (brand + article + ISO3 countries).
 * Feeds frontend Vehicle Parts / AI Parts Country Intelligence.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
$backend = isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp';
$uploadUrl = '/' . $backend . '/content/shop/demand_countries/ajax_epc_demand_upload_tmp.php';
$importUrl = '/' . $backend . '/content/shop/demand_countries/ajax_epc_demand_csv.php';
$sampleUrl = '/' . $backend . '/content/shop/demand_countries/epc_demand_countries_sample.csv';
$csrf = isset($user_session['csrf_guard_key']) ? (string) $user_session['csrf_guard_key'] : '';
?>
<style>
.epc-dc-page {
	--dc-ink: #0f172a;
	--dc-muted: #64748b;
	--dc-line: #e2e8f0;
	--dc-accent: #0369a1;
	--dc-ok: #047857;
	margin: 0 -5px 20px;
}
.epc-dc-page .epc-dc-hero {
	background: linear-gradient(135deg, #0c4a6e 0%, #0369a1 55%, #0e7490 100%);
	color: #f8fafc;
	border-radius: 12px;
	padding: 18px 20px;
	margin: 0 5px 14px;
	display: flex;
	flex-wrap: wrap;
	gap: 12px 20px;
	align-items: center;
	justify-content: space-between;
}
.epc-dc-page .epc-dc-hero h2 {
	margin: 0 0 4px;
	font-size: 22px;
	font-weight: 700;
	letter-spacing: -0.02em;
}
.epc-dc-page .epc-dc-hero p {
	margin: 0;
	opacity: 0.88;
	font-size: 13px;
	max-width: 640px;
}
.epc-dc-page .epc-dc-stat {
	background: rgba(255,255,255,0.12);
	border: 1px solid rgba(255,255,255,0.18);
	border-radius: 10px;
	padding: 10px 14px;
	min-width: 110px;
	text-align: center;
}
.epc-dc-page .epc-dc-stat b { display: block; font-size: 18px; line-height: 1.2; }
.epc-dc-page .epc-dc-stat span { font-size: 11px; opacity: 0.8; text-transform: uppercase; letter-spacing: 0.04em; }
.epc-dc-page .hpanel {
	border-radius: 10px;
	overflow: hidden;
	border: 1px solid var(--dc-line);
	box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
}
.epc-dc-page .hpanel .panel-heading.hbuilt {
	background: linear-gradient(180deg, #fff 0%, #f8fafc 100%);
	border-bottom: 1px solid var(--dc-line);
	font-weight: 600;
	color: var(--dc-ink);
}
.epc-dc-page pre {
	background: #f8fafc;
	border: 1px solid var(--dc-line);
	border-radius: 8px;
	padding: 12px;
	font-size: 12px;
}
.epc-dc-page .epc-dc-preview table { font-size: 13px; margin-bottom: 0; }
.epc-dc-page .epc-dc-preview .text-danger { color: #b91c1c; }
.epc-dc-page .epc-dc-markets { font-size: 12px; color: var(--dc-muted); margin-top: 8px; }
.epc-dc-page .epc-dc-markets code { margin-right: 4px; }
.epc-dc-page .btn-primary { background: var(--dc-accent); border-color: var(--dc-accent); }
.epc-dc-page .btn-success { background: var(--dc-ok); border-color: var(--dc-ok); }
.epc-dc-page #epc-dc-country-table td { vertical-align: middle !important; }
.epc-dc-page #epc-dc-country-table tbody tr {
	cursor: pointer;
	transition: background 0.12s ease;
}
.epc-dc-page #epc-dc-country-table tbody tr:hover { background: #e0f2fe; }
.epc-dc-page #epc-dc-country-table tbody tr.is-active {
	background: #bae6fd;
	outline: 2px solid #0284c7;
	outline-offset: -2px;
}
.epc-dc-page .epc-dc-parts-panel {
	margin-top: 14px;
	border: 1px solid var(--dc-line);
	border-radius: 10px;
	background: #fff;
	overflow: hidden;
}
.epc-dc-page .epc-dc-parts-head {
	display: flex;
	flex-wrap: wrap;
	gap: 8px 12px;
	align-items: center;
	justify-content: space-between;
	padding: 12px 14px;
	background: linear-gradient(180deg, #fff 0%, #f8fafc 100%);
	border-bottom: 1px solid var(--dc-line);
}
.epc-dc-page .epc-dc-parts-head h4 {
	margin: 0;
	font-size: 15px;
	font-weight: 700;
	color: var(--dc-ink);
}
.epc-dc-page .epc-dc-parts-head h4 code {
	font-size: 12px;
	background: #e0f2fe;
	color: #075985;
	border-radius: 4px;
	padding: 1px 6px;
	margin-left: 4px;
}
.epc-dc-page .epc-dc-parts-tools {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	align-items: center;
}
.epc-dc-page .epc-dc-parts-tools .form-control {
	height: 32px;
	min-width: 180px;
	border-radius: 6px;
}
.epc-dc-page .epc-dc-parts-body { padding: 0; max-height: 420px; overflow: auto; }
.epc-dc-page .epc-dc-parts-body table { margin: 0; font-size: 13px; }
.epc-dc-page .epc-dc-parts-body td { vertical-align: middle !important; }
.epc-dc-page .epc-dc-parts-foot {
	padding: 10px 14px;
	border-top: 1px solid var(--dc-line);
	background: #f8fafc;
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	align-items: center;
	justify-content: space-between;
	font-size: 12px;
	color: var(--dc-muted);
}
.epc-dc-page .epc-dc-parts-empty {
	padding: 20px 14px;
	text-align: center;
	color: var(--dc-muted);
	font-size: 13px;
}
.epc-dc-page .epc-dc-chip {
	display: inline-block;
	font-size: 11px;
	background: #eef2f7;
	color: #475569;
	border-radius: 999px;
	padding: 1px 7px;
	margin: 0 2px 2px 0;
}
</style>
<div class="epc-dc-page">
	<div class="epc-dc-hero">
		<div>
			<h2>Demand countries</h2>
			<p>Tag brand + article with ISO&nbsp;3166-1 alpha-3 markets. The storefront AI Parts / Country Intelligence module uses these tags (UAE remains the stock pool — do not tag ARE as demand).</p>
		</div>
		<div class="epc-dc-stat"><b id="epc-dc-stat-parts">—</b><span>Parts tagged</span></div>
		<div class="epc-dc-stat"><b id="epc-dc-stat-tags">—</b><span>Demand tags</span></div>
	</div>

	<div class="row" style="margin:0;">
		<div class="col-lg-7">
			<div class="hpanel">
				<div class="panel-heading hbuilt">Upload CSV for AI Parts demand tags</div>
				<div class="panel-body">
					<ul>
						<li>Required columns: <code>brand</code> (or <code>manufacturer</code>), <code>article</code></li>
						<li>Countries in <code>countries</code> as <code>SDN,DZA,KEN</code> <em>or</em> columns <code>A</code>, <code>B</code>, <code>C</code>, …</li>
						<li>Use 3-letter codes only (SDN, DZA, KEN, EGY, NGA, SAU). ARE = UAE stock — not demand.</li>
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
						<select id="epc-dc-mode" class="form-control" style="max-width:360px;">
							<option value="merge">Merge — add/update tags, keep other countries on the part</option>
							<option value="replace">Replace — set only listed countries for each brand+article</option>
						</select>
					</div>
					<button type="button" class="btn btn-primary" id="epc-dc-preview-btn"><i class="fa fa-eye"></i> Preview</button>
					<button type="button" class="btn btn-success" id="epc-dc-import-btn" disabled><i class="fa fa-upload"></i> Import</button>
					<div id="epc-dc-msg" class="alert" style="display:none;margin-top:12px;"></div>
					<div id="epc-dc-preview" class="epc-dc-preview" style="display:none;margin-top:16px;"></div>
					<div id="epc-dc-stats" class="epc-dc-stats" style="margin-top:12px;"></div>
				</div>
			</div>
		</div>
		<div class="col-lg-5">
			<div class="hpanel">
				<div class="panel-heading hbuilt">Live demand tags (feeds AI Parts)</div>
				<div class="panel-body">
					<p class="text-muted" style="margin-top:0;font-size:13px;">Counts from <code>epc_article_demand</code>. <strong>Click a country</strong> to see which brand + part numbers are tagged for that market.</p>
					<div id="epc-dc-by-country"><div class="text-muted">Loading overview…</div></div>
					<div class="epc-dc-markets" id="epc-dc-markets"></div>
					<p style="margin-top:12px;margin-bottom:0;">
						<button type="button" class="btn btn-default btn-sm" id="epc-dc-refresh-stats"><i class="fa fa-refresh"></i> Refresh</button>
					</p>
				</div>
			</div>
		</div>
	</div>

	<div class="row" style="margin:14px 0 0;">
		<div class="col-lg-12">
			<div class="hpanel" id="epc-dc-parts-card" style="display:none;">
				<div class="panel-heading hbuilt">Part numbers for selected country</div>
				<div class="panel-body" style="padding:0;">
					<div class="epc-dc-parts-panel" style="margin:0;border:none;border-radius:0;">
						<div class="epc-dc-parts-head">
							<h4 id="epc-dc-parts-title">Select a country</h4>
							<div class="epc-dc-parts-tools">
								<input type="search" class="form-control" id="epc-dc-parts-q" placeholder="Filter brand or article…" autocomplete="off"/>
								<button type="button" class="btn btn-primary btn-sm" id="epc-dc-parts-search"><i class="fa fa-search"></i> Search</button>
								<button type="button" class="btn btn-default btn-sm" id="epc-dc-parts-close"><i class="fa fa-times"></i> Close</button>
							</div>
						</div>
						<div class="epc-dc-parts-body" id="epc-dc-parts-body">
							<div class="epc-dc-parts-empty">Click a market in the overview to load its part numbers.</div>
						</div>
						<div class="epc-dc-parts-foot">
							<span id="epc-dc-parts-meta"></span>
							<span id="epc-dc-parts-pager"></span>
						</div>
					</div>
				</div>
			</div>
		</div>
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
	var csrf = <?php echo json_encode($csrf); ?>;
	var uploadedPath = '';

	function showMsg(text, ok) {
		msgBox.style.display = 'block';
		msgBox.className = 'alert alert-' + (ok ? 'success' : 'danger');
		msgBox.textContent = text;
	}

	function escapeHtml(s) {
		return String(s || '')
			.replace(/&/g, '&amp;')
			.replace(new RegExp('<', 'g'), '&lt;')
			.replace(new RegExp('>', 'g'), '&gt;');
	}

	function uploadFile(cb) {
		if (!fileInput.files || !fileInput.files[0]) {
			showMsg('Choose a CSV file first.', false);
			return;
		}
		var fd = new FormData();
		fd.append('csv_file', fileInput.files[0]);
		fd.append('csrf_guard_key', csrf);
		var xhr = new XMLHttpRequest();
		xhr.open('POST', uploadUrl, true);
		xhr.onload = function () {
			var data;
			try { data = JSON.parse(xhr.responseText); } catch (e) {
				showMsg('Upload failed (HTTP ' + xhr.status + ').', false);
				return;
			}
			if (!data || !data.status) {
				showMsg((data && data.message) ? data.message : 'Upload failed.', false);
				return;
			}
			uploadedPath = data.file_full_path;
			cb();
		};
		xhr.onerror = function () { showMsg('Upload network error.', false); };
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

	function callImport(action) {
		uploadFile(function () {
			var xhr = new XMLHttpRequest();
			xhr.open('POST', importUrl, true);
			xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
			xhr.onload = function () {
				var data;
				try { data = JSON.parse(xhr.responseText); } catch (e) {
					showMsg('Request failed (HTTP ' + xhr.status + ').', false);
					return;
				}
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
					loadOverview();
				}
			};
			xhr.onerror = function () { showMsg('Import network error.', false); };
			xhr.send('action=' + encodeURIComponent(action)
				+ '&file_full_path=' + encodeURIComponent(uploadedPath)
				+ '&mode=' + encodeURIComponent(modeSel.value)
				+ '&csrf_guard_key=' + encodeURIComponent(csrf));
		});
	}

	var selectedCountry = '';
	var partsPage = 1;
	var partsQ = '';

	function setActiveCountryRow(code) {
		var table = document.getElementById('epc-dc-country-table');
		if (!table) { return; }
		Array.prototype.forEach.call(table.querySelectorAll('tbody tr'), function (tr) {
			if (tr.getAttribute('data-code') === code) {
				tr.classList.add('is-active');
			} else {
				tr.classList.remove('is-active');
			}
		});
	}

	function loadCountryParts(code, page, q) {
		if (!code) { return; }
		selectedCountry = code;
		partsPage = page || 1;
		partsQ = (typeof q === 'string') ? q : partsQ;
		var card = document.getElementById('epc-dc-parts-card');
		var body = document.getElementById('epc-dc-parts-body');
		var title = document.getElementById('epc-dc-parts-title');
		var meta = document.getElementById('epc-dc-parts-meta');
		var pager = document.getElementById('epc-dc-parts-pager');
		card.style.display = 'block';
		body.innerHTML = '<div class="epc-dc-parts-empty"><i class="fa fa-spinner fa-spin"></i> Loading part numbers…</div>';
		meta.textContent = '';
		pager.innerHTML = '';
		setActiveCountryRow(code);

		var xhr = new XMLHttpRequest();
		xhr.open('POST', importUrl, true);
		xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
		xhr.onload = function () {
			var data;
			try { data = JSON.parse(xhr.responseText); } catch (e) {
				body.innerHTML = '<div class="epc-dc-parts-empty text-danger">Could not load parts.</div>';
				return;
			}
			if (!data || !data.status) {
				body.innerHTML = '<div class="epc-dc-parts-empty text-danger">' + escapeHtml((data && data.message) || 'Failed') + '</div>';
				return;
			}
			var cName = (data.country && data.country.name) ? data.country.name : code;
			var cCode = (data.country && data.country.code) ? data.country.code : code;
			title.innerHTML = escapeHtml(cName) + ' <code>' + escapeHtml(cCode) + '</code>';
			var parts = data.parts || [];
			if (!parts.length) {
				body.innerHTML = '<div class="epc-dc-parts-empty">No part numbers tagged for this country'
					+ (partsQ ? ' matching “' + escapeHtml(partsQ) + '”' : '') + '.</div>';
			} else {
				var html = '<table class="table table-striped table-condensed"><thead><tr>'
					+ '<th>#</th><th>Brand</th><th>Part number</th><th>Also tagged</th><th>Source</th><th></th>'
					+ '</tr></thead><tbody>';
				var baseIdx = ((data.page || 1) - 1) * (data.per_page || 50);
				parts.forEach(function (p, i) {
					var others = (p.other_countries || []).map(function (oc) {
						return '<span class="epc-dc-chip">' + escapeHtml(oc) + '</span>';
					}).join(' ');
					html += '<tr>'
						+ '<td>' + (baseIdx + i + 1) + '</td>'
						+ '<td><strong>' + escapeHtml(p.brand) + '</strong></td>'
						+ '<td><code>' + escapeHtml(p.article) + '</code></td>'
						+ '<td>' + (others || '<span class="text-muted">—</span>') + '</td>'
						+ '<td class="text-muted">' + escapeHtml(p.source || '') + '</td>'
						+ '<td class="text-right"><a class="btn btn-xs btn-primary" target="_blank" rel="noopener" href="'
						+ escapeHtml(p.search_url || '#') + '">Open search</a></td>'
						+ '</tr>';
				});
				html += '</tbody></table>';
				body.innerHTML = html;
			}
			meta.textContent = 'Showing ' + parts.length + ' of ' + (data.total || 0)
				+ ' part number(s) for ' + cName + ' (' + cCode + ')';
			var pages = data.pages || 1;
			var cur = data.page || 1;
			if (pages > 1) {
				var pHtml = '';
				if (cur > 1) {
					pHtml += '<a href="javascript:void(0)" class="btn btn-xs btn-default" data-page="' + (cur - 1) + '">&laquo; Prev</a> ';
				}
				pHtml += ' Page ' + cur + ' / ' + pages + ' ';
				if (cur < pages) {
					pHtml += '<a href="javascript:void(0)" class="btn btn-xs btn-default" data-page="' + (cur + 1) + '">Next &raquo;</a>';
				}
				pager.innerHTML = pHtml;
				Array.prototype.forEach.call(pager.querySelectorAll('[data-page]'), function (a) {
					a.addEventListener('click', function () {
						loadCountryParts(selectedCountry, parseInt(a.getAttribute('data-page'), 10) || 1, partsQ);
					});
				});
			}
			try { card.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); } catch (e) {}
		};
		xhr.onerror = function () {
			body.innerHTML = '<div class="epc-dc-parts-empty text-danger">Network error.</div>';
		};
		xhr.send('action=country_parts'
			+ '&country=' + encodeURIComponent(code)
			+ '&page=' + encodeURIComponent(String(partsPage))
			+ '&q=' + encodeURIComponent(partsQ)
			+ '&per_page=50'
			+ '&csrf_guard_key=' + encodeURIComponent(csrf));
	}

	function loadOverview() {
		var box = document.getElementById('epc-dc-by-country');
		var xhr = new XMLHttpRequest();
		xhr.open('POST', importUrl, true);
		xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
		xhr.onload = function () {
			var data;
			try { data = JSON.parse(xhr.responseText); } catch (e) {
				box.innerHTML = '<div class="text-danger">Could not load overview.</div>';
				return;
			}
			if (!data || !data.status || !data.stats) {
				box.innerHTML = '<div class="text-danger">' + escapeHtml((data && data.message) || 'Overview failed') + '</div>';
				return;
			}
			var s = data.stats;
			document.getElementById('epc-dc-stat-parts').textContent = String(s.total_parts || 0);
			document.getElementById('epc-dc-stat-tags').textContent = String(s.total_tags || 0);
			var rows = s.by_country || [];
			if (!rows.length) {
				box.innerHTML = '<p class="text-muted" style="margin:0;">No demand tags yet. Preview and import a CSV to feed AI Parts Country Intelligence.</p>';
			} else {
				var html = '<table class="table table-striped table-condensed" id="epc-dc-country-table"><thead><tr><th>Market</th><th>Code</th><th>Parts</th><th>Tags</th><th></th></tr></thead><tbody>';
				rows.forEach(function (r) {
					html += '<tr data-code="' + escapeHtml(r.code) + '" title="Click to see part numbers">'
						+ '<td><strong>' + escapeHtml(r.name) + '</strong></td>'
						+ '<td><code>' + escapeHtml(r.code) + '</code></td>'
						+ '<td>' + (r.parts || 0) + '</td>'
						+ '<td>' + (r.tags || 0) + '</td>'
						+ '<td class="text-right"><span class="btn btn-xs btn-info"><i class="fa fa-list"></i> Parts</span></td>'
						+ '</tr>';
				});
				html += '</tbody></table>';
				box.innerHTML = html;
				Array.prototype.forEach.call(box.querySelectorAll('tbody tr[data-code]'), function (tr) {
					tr.addEventListener('click', function () {
						var code = tr.getAttribute('data-code') || '';
						document.getElementById('epc-dc-parts-q').value = '';
						partsQ = '';
						loadCountryParts(code, 1, '');
					});
				});
				if (selectedCountry) {
					setActiveCountryRow(selectedCountry);
				}
			}
			var markets = s.markets || [];
			var mHtml = 'Selectable demand markets: ';
			markets.forEach(function (m) {
				mHtml += '<code>' + escapeHtml(m.code) + '</code> ' + escapeHtml(m.name) + ' · ';
			});
			document.getElementById('epc-dc-markets').innerHTML = mHtml.replace(/\s·\s$/, '');
		};
		xhr.send('action=stats&csrf_guard_key=' + encodeURIComponent(csrf));
	}

	previewBtn.addEventListener('click', function () { callImport('preview'); });
	importBtn.addEventListener('click', function () {
		if (!confirm('Import demand tags from this CSV into epc_article_demand for AI Parts?')) { return; }
		callImport('import');
	});
	document.getElementById('epc-dc-refresh-stats').addEventListener('click', loadOverview);
	document.getElementById('epc-dc-parts-search').addEventListener('click', function () {
		if (!selectedCountry) { return; }
		partsQ = document.getElementById('epc-dc-parts-q').value.trim();
		loadCountryParts(selectedCountry, 1, partsQ);
	});
	document.getElementById('epc-dc-parts-q').addEventListener('keydown', function (e) {
		if (e.key === 'Enter') {
			e.preventDefault();
			document.getElementById('epc-dc-parts-search').click();
		}
	});
	document.getElementById('epc-dc-parts-close').addEventListener('click', function () {
		selectedCountry = '';
		document.getElementById('epc-dc-parts-card').style.display = 'none';
		setActiveCountryRow('');
	});
	loadOverview();
})();
</script>
