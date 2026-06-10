<?php
defined('_ASTEXE_') or die('No access');
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();
$user_session = DP_User::getUserSession();
$is_admin_viewer = DP_User::isAdmin();
$admin_session = $is_admin_viewer ? DP_User::getAdminSession() : false;
$bulk_csrf_guard_key = ($user_id > 0 && is_array($user_session)) ? $user_session["csrf_guard_key"] : (is_array($admin_session) ? $admin_session["csrf_guard_key"] : '');
$price_profiles = array();
if($is_admin_viewer)
{
	$price_profiles_query = $db_link->prepare("SELECT `epc_price_profiles`.`group_id`, `epc_price_profiles`.`code`, `groups`.`value` FROM `epc_price_profiles` INNER JOIN `groups` ON `groups`.`id` = `epc_price_profiles`.`group_id` ORDER BY `epc_price_profiles`.`id` ASC;");
	$price_profiles_query->execute();
	while($price_profile = $price_profiles_query->fetch(PDO::FETCH_ASSOC))
	{
		$price_profile["caption"] = function_exists('translate_str_by_key') ? translate_str_by_key($price_profile["value"]) : translate_str_by_id($price_profile["value"]);
		$price_profiles[] = $price_profile;
	}
}
$lang_href = isset($multilang_params['lang_href']) ? $multilang_params['lang_href'] : '';
$bulk_history = array();
try
{
	$db_link->exec("CREATE TABLE IF NOT EXISTS `epc_bulk_upload_history` (
		`id` INT(11) NOT NULL AUTO_INCREMENT,
		`user_id` INT(11) NOT NULL DEFAULT 0,
		`created_by_admin` TINYINT(1) NOT NULL DEFAULT 0,
		`group_id` INT(11) NOT NULL DEFAULT 0,
		`file_name` VARCHAR(255) NOT NULL DEFAULT '',
		`priority` VARCHAR(20) NOT NULL DEFAULT 'price',
		`uploaded_count` INT(11) NOT NULL DEFAULT 0,
		`available_count` INT(11) NOT NULL DEFAULT 0,
		`cross_count` INT(11) NOT NULL DEFAULT 0,
		`short_count` INT(11) NOT NULL DEFAULT 0,
		`notfound_count` INT(11) NOT NULL DEFAULT 0,
		`result_json` LONGTEXT NULL,
		`csv_result` LONGTEXT NULL,
		`created_at` DATETIME NOT NULL,
		`updated_at` DATETIME NOT NULL,
		PRIMARY KEY (`id`),
		KEY `user_id` (`user_id`),
		KEY `created_at` (`created_at`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
	if($user_id > 0)
	{
		$history_query = $db_link->prepare("SELECT `id`, `file_name`, `priority`, `uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`, `created_at`, `updated_at` FROM `epc_bulk_upload_history` WHERE `user_id` = ? ORDER BY `id` DESC LIMIT 10;");
		$history_query->execute(array($user_id));
		$bulk_history = $history_query->fetchAll(PDO::FETCH_ASSOC);
	}
	else if($is_admin_viewer)
	{
		$history_query = $db_link->prepare("SELECT `id`, `user_id`, `file_name`, `priority`, `uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`, `created_at`, `updated_at` FROM `epc_bulk_upload_history` ORDER BY `id` DESC LIMIT 10;");
		$history_query->execute();
		$bulk_history = $history_query->fetchAll(PDO::FETCH_ASSOC);
	}
}
catch(Exception $e)
{
	$bulk_history = array();
}
?>

<style>
.epc-bulk-wrap{max-width:1320px;margin:0 auto 40px;}
.epc-bulk-hero{background:radial-gradient(circle at 10% 0%,rgba(96,165,250,.35),transparent 28%),linear-gradient(135deg,#0f172a,#1d4ed8);border-radius:18px;color:#fff;padding:28px;margin-bottom:22px;box-shadow:0 18px 40px rgba(15,23,42,.20);}
.epc-bulk-hero h1{color:#fff!important;margin:0 0 8px;font-size:30px;font-weight:900;letter-spacing:-.02em;}
.epc-bulk-hero p{color:rgba(255,255,255,.88)!important;margin:0;font-size:15px;}
.epc-bulk-hero-steps{display:flex;flex-wrap:wrap;gap:8px;margin-top:16px;}
.epc-bulk-hero-steps span{background:rgba(255,255,255,.14);border:1px solid rgba(255,255,255,.22);border-radius:999px;color:#fff;font-size:12px;font-weight:800;padding:7px 11px;}
.epc-bulk-panel{background:#fff;border:1px solid #e5eaf0;border-radius:16px;padding:22px;margin-bottom:18px;box-shadow:0 14px 34px rgba(15,23,42,.08);}
.epc-bulk-grid{display:grid;grid-template-columns:1fr 1fr;gap:18px;}
.epc-bulk-help{background:#fff8db;border:1px solid #f4df8a;border-radius:12px;padding:14px;color:#6b4f00;line-height:1.55;margin:14px 0;}
.epc-bulk-actions{align-items:center;background:#fff;border-top:1px solid #eef2f7;display:flex;gap:10px;flex-wrap:wrap;margin:18px -22px 0;padding:16px 22px 0;}
.epc-bulk-actions .btn{border-radius:10px;font-weight:800;padding:9px 14px;}
.epc-bulk-summary{display:grid;grid-template-columns:repeat(5,1fr);gap:10px;margin-bottom:16px;}
.epc-bulk-stat{background:#fff;border:1px solid #e2e8f0;border-radius:14px;box-shadow:0 8px 22px rgba(15,23,42,.05);padding:13px;}
.epc-bulk-stat span{color:#64748b;font-size:12px;font-weight:900;text-transform:uppercase;}
.epc-bulk-stat strong{display:block;font-size:24px;color:#0f172a;line-height:1.1;margin-top:3px;}
.epc-bulk-stat--available strong{color:#15803d;}
.epc-bulk-stat--cross strong{color:#1d4ed8;}
.epc-bulk-stat--short strong{color:#c2410c;}
.epc-bulk-stat--notfound strong{color:#be123c;}
.epc-bulk-table-wrap{background:#fff;border:1px solid #dbe4ee;border-radius:16px;box-shadow:0 12px 30px rgba(15,23,42,.06);overflow:auto;max-height:740px;}
.epc-bulk-table{border-collapse:separate;border-spacing:0;font-size:12px;margin:0;min-width:1280px;width:100%;}
.epc-bulk-table th{background:#f8fafc;border-bottom:1px solid #dbe4ee;color:#0f172a;font-size:11px;font-weight:900;letter-spacing:.02em;padding:9px 8px;position:sticky;text-transform:uppercase;top:0;z-index:2;}
.epc-bulk-table td{border-bottom:1px solid #edf2f7;color:#334155;padding:7px 8px;vertical-align:middle;}
.epc-bulk-table tr:hover td{background:#f8fafc;}
.epc-bulk-table .is-short td{background:#fff7ed;}
.epc-bulk-table .is-notfound td{background:#fff1f2;}
.epc-bulk-table .is-cross td{background:#eff6ff;}
.epc-bulk-table th:nth-child(1),.epc-bulk-table td:nth-child(1){position:sticky;left:0;z-index:1;}
.epc-bulk-table th:nth-child(1){z-index:3;}
.epc-bulk-table td:nth-child(1){background:inherit;}
.epc-bulk-part{font-weight:900;color:#0f172a;white-space:nowrap;}
.epc-bulk-name{max-width:260px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.epc-bulk-num{text-align:right;white-space:nowrap;}
.epc-bulk-actions-cell{white-space:nowrap;}
.epc-bulk-badge{display:inline-block;border-radius:999px;padding:3px 8px;font-size:11px;font-weight:700;}
.epc-bulk-badge--exact{background:#dcfce7;color:#166534;}
.epc-bulk-badge--cross{background:#dbeafe;color:#1d4ed8;}
.epc-bulk-badge--short{background:#fee2e2;color:#991b1b;}
.epc-bulk-muted{color:#64748b;}
.epc-bulk-empty{color:#94a3b8;font-style:italic;padding:10px 0;}
.epc-bulk-loading{display:none;margin-top:12px;color:#1f6feb;font-weight:700;}
.epc-bulk-progress{display:none;background:#f8fafc;border:1px solid #dbe4ee;border-radius:12px;margin-top:12px;padding:12px;}
.epc-bulk-progress-text{color:#0f172a;font-size:13px;font-weight:800;margin-bottom:8px;}
.epc-bulk-progress-track{background:#e2e8f0;border-radius:999px;height:10px;overflow:hidden;}
.epc-bulk-progress-bar{background:linear-gradient(90deg,#1f6feb,#16a34a);height:10px;width:0%;}
.epc-bulk-progress.is-waiting .epc-bulk-progress-bar{animation:epcBulkProgressPulse 1.2s ease-in-out infinite;}
.epc-bulk-run-note{color:#64748b;font-size:12px;font-weight:700;margin-top:7px;}
.epc-bulk-history{background:#fff;border:1px solid #e5eaf0;border-radius:16px;box-shadow:0 12px 28px rgba(15,23,42,.06);margin-bottom:18px;padding:18px;}
.epc-bulk-history h3{color:#0f172a;font-size:18px;font-weight:900;margin:0 0 12px;}
.epc-bulk-history-table{border-collapse:collapse;font-size:12px;margin:0;width:100%;}
.epc-bulk-history-table th{background:#f8fafc;color:#0f172a;font-size:11px;font-weight:900;padding:8px;text-transform:uppercase;}
.epc-bulk-history-table td{border-top:1px solid #edf2f7;color:#334155;padding:8px;}
.epc-bulk-history-file{color:#0f172a;font-weight:900;max-width:270px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.epc-bulk-history-empty{color:#64748b;margin:0;}
@keyframes epcBulkProgressPulse{0%{opacity:.55}50%{opacity:1}100%{opacity:.55}}
@media(max-width:900px){.epc-bulk-grid,.epc-bulk-summary{grid-template-columns:1fr}}
</style>

<div class="epc-bulk-wrap">
	<div class="epc-bulk-hero">
		<h1>Bulk Spare Parts Upload</h1>
		<p>Upload a customer list, compare by best price or fastest delivery, review exact and cross-reference matches, then add selected available parts to cart.</p>
		<div class="epc-bulk-hero-steps">
			<span>1. Exact stock first</span>
			<span>2. Cross-reference search</span>
			<span>3. Profile price applied</span>
			<span>4. One-click cart add</span>
		</div>
	</div>

	<?php if($user_id <= 0 && !$is_admin_viewer) { ?>
		<div class="alert alert-warning">Please log in to upload a bulk spare parts list and add results to cart.</div>
	<?php } else { ?>
		<div class="epc-bulk-panel">
			<form id="epc_bulk_form" enctype="multipart/form-data">
				<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($bulk_csrf_guard_key, ENT_QUOTES, 'UTF-8'); ?>">
				<div class="epc-bulk-grid">
					<div>
						<label>Excel / CSV file</label>
						<input class="form-control" type="file" name="bulk_file" accept=".xlsx,.xls,.csv,.txt" required>
						<div class="epc-bulk-help">
							<strong>File format:</strong> Brand, Part Number, Qty, Target Price, Required Delivery, Comment.<br>
							For the best result, Brand + Part Number are checked together as the exact match. Cross availability searches by part number only across all brands. Maximum 2,000 rows.
						</div>
					</div>
					<div>
						<?php if($is_admin_viewer) { ?>
						<label>Admin price profile</label>
						<select class="form-control" name="admin_group_id" id="epc_bulk_admin_group_id">
							<?php foreach($price_profiles as $price_profile) { ?>
							<option value="<?php echo (int)$price_profile["group_id"]; ?>"><?php echo htmlspecialchars($price_profile["caption"], ENT_QUOTES, 'UTF-8'); ?> (<?php echo htmlspecialchars($price_profile["code"], ENT_QUOTES, 'UTF-8'); ?>)</option>
							<?php } ?>
						</select>
						<p class="help-block">Admin only: choose which customer profile price to simulate. Customers always use their assigned login profile.</p>
						<?php } ?>
						<label>Customer choice priority</label>
						<select class="form-control" name="priority" id="epc_bulk_priority">
							<option value="price">Best price first</option>
							<option value="delivery">Fastest delivery first</option>
						</select>
						<br>
						<label>Result filter</label>
						<select class="form-control" id="epc_bulk_filter">
							<option value="all">All uploaded lines</option>
							<option value="available">Only available</option>
							<option value="short">Short quantity</option>
							<option value="cross">Cross / related found</option>
							<option value="notfound">Not found</option>
						</select>
					</div>
				</div>
				<div class="epc-bulk-actions">
					<button class="btn btn-primary" type="submit">Upload and check prices</button>
					<button class="btn btn-info" type="button" id="epc_bulk_fetch_all_cross" disabled>Fetch cross for all not found / short qty</button>
					<button class="btn btn-success" type="button" id="epc_bulk_add_selected" disabled>Add selected to cart</button>
					<button class="btn btn-default" type="button" id="epc_bulk_download" disabled>Download result CSV</button>
				</div>
				<div class="epc-bulk-loading" id="epc_bulk_loading">Processing file and checking availability...</div>
				<div class="epc-bulk-progress" id="epc_bulk_process_progress">
					<div class="epc-bulk-progress-text" id="epc_bulk_process_progress_text">Processing progress: 0%</div>
					<div class="epc-bulk-progress-track"><div class="epc-bulk-progress-bar" id="epc_bulk_process_progress_bar"></div></div>
				</div>
				<div class="epc-bulk-progress" id="epc_bulk_cross_progress">
					<div class="epc-bulk-progress-text" id="epc_bulk_cross_progress_text">Cross availability progress: 0%</div>
					<div class="epc-bulk-progress-track"><div class="epc-bulk-progress-bar" id="epc_bulk_cross_progress_bar"></div></div>
				</div>
			</form>
		</div>

		<div class="epc-bulk-history">
			<h3>Recent bulk upload history</h3>
			<?php if(empty($bulk_history)) { ?>
				<p class="epc-bulk-history-empty">No saved bulk uploads yet. Your next upload will be saved here for tracking.</p>
			<?php } else { ?>
				<div class="table-responsive">
					<table class="epc-bulk-history-table">
						<thead>
							<tr>
								<th>Date</th>
								<?php if($is_admin_viewer && $user_id <= 0) { ?><th>User ID</th><?php } ?>
								<th>File</th>
								<th>Priority</th>
								<th>Uploaded</th>
								<th>Available</th>
								<th>Cross</th>
								<th>Short</th>
								<th>Not found</th>
								<th>Updated</th>
							</tr>
						</thead>
						<tbody>
							<?php foreach($bulk_history as $history_row) { ?>
							<tr>
								<td><?php echo htmlspecialchars($history_row['created_at'], ENT_QUOTES, 'UTF-8'); ?></td>
								<?php if($is_admin_viewer && $user_id <= 0) { ?><td><?php echo (int)$history_row['user_id']; ?></td><?php } ?>
								<td class="epc-bulk-history-file" title="<?php echo htmlspecialchars($history_row['file_name'], ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($history_row['file_name'], ENT_QUOTES, 'UTF-8'); ?></td>
								<td><?php echo htmlspecialchars($history_row['priority'], ENT_QUOTES, 'UTF-8'); ?></td>
								<td><?php echo (int)$history_row['uploaded_count']; ?></td>
								<td><?php echo (int)$history_row['available_count']; ?></td>
								<td><?php echo (int)$history_row['cross_count']; ?></td>
								<td><?php echo (int)$history_row['short_count']; ?></td>
								<td><?php echo (int)$history_row['notfound_count']; ?></td>
								<td><?php echo htmlspecialchars($history_row['updated_at'], ENT_QUOTES, 'UTF-8'); ?></td>
							</tr>
							<?php } ?>
						</tbody>
					</table>
				</div>
			<?php } ?>
		</div>

		<div id="epc_bulk_results"></div>
	<?php } ?>
</div>

<script>
(function(){
	var bulkResults = [];
	var lastCsv = '';
	var currentUploadId = 0;
	var bulkCrossRunning = false;
	var langCartUrl = <?php echo json_encode($lang_href . '/shop/cart'); ?>;
	var csrfGuardKey = <?php echo json_encode($bulk_csrf_guard_key); ?>;

	function esc(v){ return String(v == null ? '' : v).replace(/[&<>"']/g, function(c){ return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]; }); }
	function money(v){ return (typeof epcFormatMoney === 'function') ? epcFormatMoney(v) : Number(v || 0).toFixed(2); }
	function applyFilter(){
		var filter = document.getElementById('epc_bulk_filter').value;
		document.querySelectorAll('.epc-bulk-result').forEach(function(el){
			var show = filter === 'all' || el.getAttribute('data-filter-' + filter) === '1';
			el.style.display = show ? '' : 'none';
		});
	}
	function selectedOption(row){
		if(row.cross && (!row.exact || Number(row.exact.exist || 0) < Number(row.input.qty || 1))){
			return row.cross;
		}
		return row.exact || row.cross || null;
	}
	function setCrossProgress(done, total, active){
		var percent = total > 0 ? Math.round((done / total) * 100) : 0;
		var pending = Math.max(0, total - done);
		var box = document.getElementById('epc_bulk_cross_progress');
		var text = document.getElementById('epc_bulk_cross_progress_text');
		var bar = document.getElementById('epc_bulk_cross_progress_bar');
		if(!box || !text || !bar){ return; }
		box.style.display = 'block';
		text.innerHTML = 'Cross availability progress: '+percent+'% complete | Completed '+done+' of '+total+' | Pending '+pending+(active ? ' | Checking '+active+' rows now' : '');
		bar.style.width = percent+'%';
	}
	function hideCrossProgress(){
		var box = document.getElementById('epc_bulk_cross_progress');
		var bar = document.getElementById('epc_bulk_cross_progress_bar');
		if(box){ box.style.display = 'none'; }
		if(bar){ bar.style.width = '0%'; }
	}
	function setProcessProgress(percent, message){
		var box = document.getElementById('epc_bulk_process_progress');
		var text = document.getElementById('epc_bulk_process_progress_text');
		var bar = document.getElementById('epc_bulk_process_progress_bar');
		percent = Math.max(0, Math.min(100, Math.round(percent)));
		if(!box || !text || !bar){ return; }
		box.className = box.className.replace(/\bis-waiting\b/g, '');
		box.style.display = 'block';
		text.innerHTML = (message || 'Processing file and checking availability') + ': ' + percent + '%';
		bar.style.width = percent + '%';
	}
	function setProcessWaiting(percent, elapsedSeconds){
		var box = document.getElementById('epc_bulk_process_progress');
		var text = document.getElementById('epc_bulk_process_progress_text');
		var bar = document.getElementById('epc_bulk_process_progress_bar');
		percent = Math.max(95, Math.min(99, Math.round(percent)));
		if(!box || !text || !bar){ return; }
		if((' '+box.className+' ').indexOf(' is-waiting ') === -1){ box.className += ' is-waiting'; }
		box.style.display = 'block';
		text.innerHTML = 'Checking availability on server: '+percent+'% | Finalizing result table, please wait | Elapsed '+elapsedSeconds+'s';
		bar.style.width = percent + '%';
	}
	function hideProcessProgress(){
		var box = document.getElementById('epc_bulk_process_progress');
		var bar = document.getElementById('epc_bulk_process_progress_bar');
		if(box){
			box.className = box.className.replace(/\bis-waiting\b/g, '');
			box.style.display = 'none';
		}
		if(bar){ bar.style.width = '0%'; }
	}
	function render(data){
		bulkResults = data.rows || [];
		lastCsv = data.csv || '';
		if(data.upload_id){ currentUploadId = Number(data.upload_id) || 0; }
		var summary = data.summary || {};
		var html = '<div class="epc-bulk-summary">' +
			'<div class="epc-bulk-stat"><span>Uploaded</span><strong>'+esc(summary.uploaded || 0)+'</strong></div>' +
			'<div class="epc-bulk-stat epc-bulk-stat--available"><span>Available</span><strong>'+esc(summary.available || 0)+'</strong></div>' +
			'<div class="epc-bulk-stat epc-bulk-stat--cross"><span>Cross found</span><strong>'+esc(summary.cross || 0)+'</strong></div>' +
			'<div class="epc-bulk-stat epc-bulk-stat--short"><span>Short qty</span><strong>'+esc(summary.short || 0)+'</strong></div>' +
			'<div class="epc-bulk-stat epc-bulk-stat--notfound"><span>Not found</span><strong>'+esc(summary.notfound || 0)+'</strong></div>' +
		'</div>';
		html += '<div class="epc-bulk-table-wrap"><table class="epc-bulk-table"><thead><tr><th>#</th><th>Add</th><th>Requested Brand</th><th>Requested Part</th><th>Need</th><th>Matched Brand</th><th>Matched Part</th><th>Name</th><th>Avail</th><th>Short</th><th>Price</th><th>Delivery</th><th>Match</th><th>Status / Cross</th></tr></thead><tbody>';
		bulkResults.forEach(function(row, idx){
			var opt = selectedOption(row);
			var side = (opt && row.cross && opt === row.cross) ? 'cross' : (opt && row.exact ? 'exact' : '');
			var trClass = row.short_qty ? 'is-short' : (!row.available ? 'is-notfound' : (row.cross ? 'is-cross' : ''));
			var canFetchCross = (!row.available || row.short_qty) && !row.cross_checked;
			var actionHtml = canFetchCross ? (bulkCrossRunning ? '<span class="epc-bulk-muted">Queued for cross check</span>' : '<button class="btn btn-xs btn-info epc-bulk-fetch-cross" type="button" data-row="'+idx+'">Fetch cross availability</button>') : esc(row.status_label);
			var checkbox = opt ? '<input type="checkbox" class="epc-bulk-select" data-row="'+idx+'" data-side="'+side+'" checked>' : '';
			html += '<tr class="epc-bulk-result '+trClass+'" data-filter-available="'+(row.available ? '1':'0')+'" data-filter-short="'+(row.short_qty ? '1':'0')+'" data-filter-cross="'+(row.cross_found ? '1':'0')+'" data-filter-notfound="'+(!row.available ? '1':'0')+'">';
			html += '<td class="epc-bulk-num">'+(idx+1)+'</td>';
			html += '<td>'+checkbox+'</td>';
			html += '<td>'+esc(row.input.brand || 'Any')+'</td>';
			html += '<td class="epc-bulk-part">'+esc(row.input.article)+'</td>';
			html += '<td class="epc-bulk-num">'+esc(row.input.qty)+'</td>';
			html += '<td>'+esc(opt ? opt.manufacturer : '-')+'</td>';
			html += '<td class="epc-bulk-part">'+esc(opt ? (opt.article_show || opt.article) : '-')+'</td>';
			html += '<td class="epc-bulk-name" title="'+esc(opt ? opt.name : '')+'">'+esc(opt ? opt.name : '-')+'</td>';
			html += '<td class="epc-bulk-num">'+esc(opt ? opt.exist : '-')+'</td>';
			html += '<td class="epc-bulk-num">'+(row.short_qty && opt ? esc(Math.max(0, Number(row.input.qty || 0) - Number(opt.exist || 0))) : '0')+'</td>';
			html += '<td class="epc-bulk-num">'+(opt ? money(opt.price) : '-')+'</td>';
			html += '<td class="epc-bulk-num">'+(opt ? esc(opt.time_to_exe)+' d' : '-')+'</td>';
			html += '<td>'+(opt ? '<span class="epc-bulk-badge '+(opt.match_type === 'exact' ? 'epc-bulk-badge--exact' : 'epc-bulk-badge--cross')+'">'+esc(opt.match_label)+'</span>' : '-')+'</td>';
			html += '<td class="epc-bulk-actions-cell">'+actionHtml+'</td>';
			html += '</tr>';
		});
		html += '</tbody></table></div>';
		document.getElementById('epc_bulk_results').innerHTML = html;
		document.getElementById('epc_bulk_add_selected').disabled = summary.available <= 0;
		document.getElementById('epc_bulk_fetch_all_cross').disabled = bulkCrossRunning || bulkResults.filter(function(row){ return (!row.available || row.short_qty) && !row.cross_checked; }).length === 0;
		document.getElementById('epc_bulk_download').disabled = !lastCsv;
		applyFilter();
	}
	function saveCurrentHistory(){
		if(!currentUploadId){ return Promise.resolve(false); }
		var body = new URLSearchParams({
			action: 'history_update',
			csrf_guard_key: csrfGuardKey,
			upload_id: currentUploadId,
			summary: JSON.stringify(recalcSummary()),
			rows: JSON.stringify(bulkResults),
			csv: lastCsv || ''
		});
		return fetch('/content/shop/bulk_upload/ajax_process.php', {
			method:'POST',
			headers:{'Content-Type':'application/x-www-form-urlencoded; charset=UTF-8'},
			body: body.toString(),
			credentials:'same-origin'
		}).then(function(r){ return r.json(); }).then(function(r){ return !!(r && r.status); }).catch(function(){ return false; });
	}
	document.getElementById('epc_bulk_form')?.addEventListener('submit', function(e){
		e.preventDefault();
		var form = e.target;
		var data = new FormData(form);
		hideCrossProgress();
		hideProcessProgress();
		document.getElementById('epc_bulk_loading').style.display = 'block';
		setProcessProgress(0, 'Starting upload');
		var progressPercent = 0;
		var elapsedSeconds = 0;
		var progressTimer = window.setInterval(function(){
			elapsedSeconds++;
			if(progressPercent < 95){
				progressPercent += progressPercent < 35 ? 3 : 1;
				setProcessProgress(progressPercent, progressPercent < 35 ? 'Uploading file' : 'Checking availability');
			} else {
				var waitingPercent = 95 + (elapsedSeconds % 5);
				setProcessWaiting(waitingPercent, elapsedSeconds);
			}
		}, 1000);
		var xhr = new XMLHttpRequest();
		xhr.open('POST', '/content/shop/bulk_upload/ajax_process.php', true);
		xhr.withCredentials = true;
		xhr.upload.onprogress = function(ev){
			if(ev.lengthComputable){
				progressPercent = Math.min(35, Math.round((ev.loaded / ev.total) * 35));
				setProcessProgress(progressPercent, 'Uploading file');
			}
		};
		xhr.onload = function(){
			window.clearInterval(progressTimer);
			try {
				var r = JSON.parse(xhr.responseText || '{}');
				document.getElementById('epc_bulk_loading').style.display = 'none';
				if(!r.status){
					hideProcessProgress();
					alert(r.message || 'Upload error');
					return;
				}
				setProcessProgress(100, 'Processing complete');
				render(r);
			} catch(err) {
				hideProcessProgress();
				document.getElementById('epc_bulk_loading').style.display = 'none';
				alert('Upload error');
			}
		};
		xhr.onerror = function(){
			window.clearInterval(progressTimer);
			hideProcessProgress();
			document.getElementById('epc_bulk_loading').style.display = 'none';
			alert('Upload error');
		};
		xhr.send(data);
	});
	document.getElementById('epc_bulk_filter')?.addEventListener('change', applyFilter);
	function fetchCrossForRow(idx){
		var row = bulkResults[idx];
		if(!row || row.cross_checked || (row.available && !row.short_qty)){
			return Promise.resolve(false);
		}
		var body = new URLSearchParams({
			action: 'cross',
			priority: document.getElementById('epc_bulk_priority').value,
			csrf_guard_key: csrfGuardKey,
			admin_group_id: document.getElementById('epc_bulk_admin_group_id') ? document.getElementById('epc_bulk_admin_group_id').value : '',
			brand: '',
			article: row.input.article || '',
			qty: row.input.qty || 1
		});
		return fetch('/content/shop/bulk_upload/ajax_process.php', {
			method:'POST',
			headers:{'Content-Type':'application/x-www-form-urlencoded; charset=UTF-8'},
			body: body.toString(),
			credentials:'same-origin'
		}).then(function(r){ return r.json(); }).then(function(r){
			if(!r.status){ return false; }
			var hadExactBefore = !!row.exact;
			var relatedByPartNumber = null;
			if(!hadExactBefore && r.exact){
				relatedByPartNumber = r.exact;
				relatedByPartNumber.match_type = 'cross';
				relatedByPartNumber.match_label = 'Related';
			}
			row.cross_checked = true;
			row.cross = r.cross || row.cross || relatedByPartNumber || null;
			if(hadExactBefore){
				row.exact = row.exact;
			} else if(!relatedByPartNumber) {
				row.exact = r.exact || null;
			}
			row.cross_found = !!row.cross;
			row.available = !!(row.exact || row.cross);
			var selected = selectedOption(row);
			row.short_qty = selected ? Number(selected.exist) < Number(row.input.qty || 1) : false;
			row.status_label = row.available ? (row.short_qty ? 'Available but short quantity' : 'Available') : 'No cross availability found';
			return true;
		}).catch(function(){ return false; });
	}
	document.getElementById('epc_bulk_results')?.addEventListener('click', function(e){
		var btn = e.target.closest('.epc-bulk-fetch-cross');
		if(!btn){ return; }
		var idx = Number(btn.getAttribute('data-row'));
		var row = bulkResults[idx];
		if(!row){ return; }
		btn.disabled = true;
		btn.innerHTML = 'Checking...';
		fetchCrossForRow(idx).then(function(ok){
			if(!ok){ alert('Cross check error or no cross result.'); }
			render({rows: bulkResults, summary: recalcSummary(), csv: lastCsv});
			saveCurrentHistory();
		}).finally(function(){
			btn.disabled = false;
			btn.innerHTML = 'Fetch cross availability';
		});
	});
	document.getElementById('epc_bulk_fetch_all_cross')?.addEventListener('click', function(){
		var btn = this;
		var indexes = [];
		bulkResults.forEach(function(row, idx){
			if((!row.available || row.short_qty) && !row.cross_checked){
				indexes.push(idx);
			}
		});
		if(indexes.length === 0){
			alert('No not-found or short-quantity rows need cross checking.');
			return;
		}
		btn.disabled = true;
		var originalText = btn.innerHTML;
		var done = 0;
		var active = 0;
		var cursor = 0;
		var concurrency = Math.min(4, indexes.length);
		var startedAt = Date.now();
		bulkCrossRunning = true;
		setCrossProgress(0, indexes.length, 0);
		render({rows: bulkResults, summary: recalcSummary(), csv: lastCsv});
		btn = document.getElementById('epc_bulk_fetch_all_cross') || btn;
		function finishIfDone(){
			if(done >= indexes.length && active === 0){
				bulkCrossRunning = false;
				btn = document.getElementById('epc_bulk_fetch_all_cross') || btn;
				if(btn){ btn.innerHTML = originalText; }
				setCrossProgress(indexes.length, indexes.length, 0);
				render({rows: bulkResults, summary: recalcSummary(), csv: lastCsv});
				saveCurrentHistory();
				return;
			}
			startWorkers();
		}
		function startWorkers(){
			while(active < concurrency && cursor < indexes.length){
				active++;
				var rowNumber = cursor + 1;
				var idx = indexes[cursor++];
				var percent = Math.round((done / indexes.length) * 100);
				var elapsed = Math.max(1, Math.round((Date.now() - startedAt) / 1000));
				btn = document.getElementById('epc_bulk_fetch_all_cross') || btn;
				if(btn){ btn.innerHTML = 'Checking cross '+rowNumber+' / '+indexes.length+' ('+percent+'%)...'; }
				setCrossProgress(done, indexes.length, active);
				fetchCrossForRow(idx).then(function(){
					done++;
					active--;
					setCrossProgress(done, indexes.length, active);
					if(done % 8 === 0 || done === indexes.length){
						render({rows: bulkResults, summary: recalcSummary(), csv: lastCsv});
					}
					var progressText = document.getElementById('epc_bulk_cross_progress_text');
					if(progressText && done < indexes.length){
						progressText.innerHTML += ' | Elapsed '+elapsed+'s';
					}
					finishIfDone();
				});
			}
		}
		startWorkers();
	});
	function recalcSummary(){
		var s = {uploaded: bulkResults.length, available:0, cross:0, short:0, notfound:0};
		bulkResults.forEach(function(row){
			if(row.available){ s.available++; } else { s.notfound++; }
			if(row.cross_found){ s.cross++; }
			if(row.short_qty){ s.short++; }
		});
		return s;
	}
	document.getElementById('epc_bulk_add_selected')?.addEventListener('click', function(){
		var selected = [];
		document.querySelectorAll('.epc-bulk-select:checked').forEach(function(chk){
			var row = bulkResults[Number(chk.getAttribute('data-row'))];
			var side = chk.getAttribute('data-side');
			if(row && row[side] && row[side].product_object){ selected.push(row[side].product_object); }
		});
		if(selected.length === 0){ alert('Select at least one available item.'); return; }
		fetch('/content/shop/order_process/ajax_add_to_basket.php', {
			method:'POST',
			headers:{'Content-Type':'application/x-www-form-urlencoded; charset=UTF-8'},
			body:'product_objects='+encodeURIComponent(JSON.stringify(selected))+'&csrf_guard_key='+encodeURIComponent(csrfGuardKey),
			credentials:'same-origin'
		}).then(function(r){ return r.json(); }).then(function(r){
			if(r.status){ if(confirm('Items added to cart. Open cart now?')){ window.location.href = langCartUrl; } }
			else { alert(r.message || 'Some items were not added. They may already be in cart.'); }
		}).catch(function(){ alert('Add to cart error'); });
	});
	document.getElementById('epc_bulk_download')?.addEventListener('click', function(){
		var blob = new Blob([lastCsv], {type:'text/csv;charset=utf-8;'});
		var url = URL.createObjectURL(blob);
		var a = document.createElement('a');
		a.href = url; a.download = 'bulk-upload-results.csv'; a.click();
		URL.revokeObjectURL(url);
	});
})();
</script>
