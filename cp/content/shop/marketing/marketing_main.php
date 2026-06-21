<?php
/**
 * CP — Marketing & growth hub (10 strategies: guide, follow, review, results).
 * URL: /cp/shop/marketing/marketing
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/marketing/epc_marketing_helpers.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	require __DIR__ . '/ajax_marketing.php';
	exit;
}

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed.</div>';
		return;
	}
}

epc_marketing_ensure_schema($db_link);

$strategies = epc_marketing_strategies();
$progress = epc_marketing_load_progress($db_link);
$completion = epc_marketing_completion_stats($strategies, $progress);
$live = epc_marketing_live_snapshot($db_link);
$live['domain'] = rtrim((string)$DP_Config->domain_path, '/');
$live['sitemap_url'] = $live['domain'] . '/sitemap-products.php';
$latestKpis = epc_marketing_latest_kpis($db_link, $strategies);
$reviews = epc_marketing_recent_reviews($db_link, 15);

$backend = (string)$DP_Config->backend_dir;
$marketingUrl = '/' . $backend . '/shop/marketing/marketing';
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$activeStrategy = isset($_GET['s']) ? (string)$_GET['s'] : 'overview';
if ($activeStrategy !== 'overview' && !isset($strategies[$activeStrategy])) {
	$activeStrategy = 'overview';
}
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">
<style>
.epc-mkt-hero { background: linear-gradient(135deg, #1e3a8a 0%, #7c3aed 50%, #db2777 100%); color: #fff; border-radius: 10px; padding: 22px 24px; margin-bottom: 18px; }
.epc-mkt-hero h3 { margin: 0 0 8px; color: #fff; font-weight: 700; }
.epc-mkt-kpi { display: flex; flex-wrap: wrap; gap: 12px; margin: 0 0 18px; }
.epc-mkt-kpi .kpi { flex: 1 1 130px; min-width: 110px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px; }
.epc-mkt-kpi .lbl { font-size: 10px; color: #64748b; text-transform: uppercase; letter-spacing: .04em; }
.epc-mkt-kpi .val { font-size: 20px; font-weight: 700; color: #1e40af; }
.epc-mkt-tabs > li > a { font-size: 12px; padding: 8px 10px; }
.epc-mkt-strategy-head { border-left: 5px solid #2563eb; padding: 12px 16px; margin: 0 0 16px; background: #f8fafc; border-radius: 0 8px 8px 0; }
.epc-mkt-subnav { margin-bottom: 16px; }
.epc-mkt-subnav .btn { margin-right: 6px; margin-bottom: 6px; }
.epc-mkt-panel { display: none; }
.epc-mkt-panel.active { display: block; }
.epc-mkt-task.done label { text-decoration: line-through; color: #64748b; }
.epc-mkt-progress-bar { height: 8px; background: #e2e8f0; border-radius: 4px; overflow: hidden; margin-top: 4px; }
.epc-mkt-progress-bar > span { display: block; height: 100%; background: #2563eb; }
.epc-mkt-review-item { padding: 8px 0; border-bottom: 1px solid #f1f5f9; }
.epc-mkt-live-tag { font-size: 10px; background: #dcfce7; color: #166534; padding: 2px 6px; border-radius: 4px; margin-left: 6px; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<i class="fa fa-globe"></i> Marketing &amp; growth
			<span class="pull-right">
				<a class="btn btn-default btn-xs" href="<?php echo epc_marketing_h($live['domain']); ?>" target="_blank"><i class="fa fa-external-link"></i> Storefront</a>
				<a class="btn btn-default btn-xs" href="/epc-marketing-demo.php?token=epartscart-deploy-2026" target="_blank"><i class="fa fa-code"></i> JSON</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-mkt-hero">
				<h3><i class="fa fa-rocket"></i> Promote epartscart.com worldwide</h3>
				<p style="margin:0;opacity:.95;">
					10 strategies with <strong>guidelines</strong>, <strong>follow</strong> checklists,
					<strong>review</strong> questions, and <strong>results</strong> KPIs.
					Overall progress: <strong id="epc_mkt_overall_pct"><?php echo (int)$completion['pct']; ?>%</strong>
					(<?php echo (int)$completion['done']; ?> / <?php echo (int)$completion['total']; ?> tasks).
					Update weekly — snapshot <?php echo epc_marketing_h($live['generated_at']); ?>.
				</p>
			</div>

			<div class="epc-mkt-kpi">
				<div class="kpi"><div class="lbl">Orders (30d)</div><div class="val"><?php echo (int)$live['orders_30d']; ?></div></div>
				<div class="kpi"><div class="lbl">Orders (7d)</div><div class="val"><?php echo (int)$live['orders_7d']; ?></div></div>
				<div class="kpi"><div class="lbl">Total orders</div><div class="val"><?php echo (int)$live['orders_total']; ?></div></div>
				<div class="kpi"><div class="lbl">Users</div><div class="val"><?php echo (int)$live['users_total']; ?></div></div>
				<div class="kpi"><div class="lbl">Catalog rows</div><div class="val"><?php echo number_format((int)$live['price_rows']); ?></div></div>
				<div class="kpi"><div class="lbl">Brands</div><div class="val"><?php echo (int)$live['brands_count']; ?></div></div>
				<div class="kpi"><div class="lbl">Marketplace</div><div class="val"><?php echo (int)$live['marketplace_orders']; ?></div></div>
				<div class="kpi"><div class="lbl">WA API sent</div><div class="val"><?php echo (int)$live['whatsapp_api_sent']; ?></div></div>
			</div>

			<ul class="nav nav-tabs epc-mkt-tabs" role="tablist">
				<li class="<?php echo $activeStrategy === 'overview' ? 'active' : ''; ?>">
					<a href="<?php echo epc_marketing_h($marketingUrl); ?>"><i class="fa fa-dashboard"></i> Overview</a>
				</li>
				<?php foreach ($strategies as $key => $str): ?>
				<li class="<?php echo $activeStrategy === $key ? 'active' : ''; ?>">
					<a href="<?php echo epc_marketing_h($marketingUrl . '?s=' . rawurlencode($key)); ?>">
						<i class="fa <?php echo epc_marketing_h($str['icon']); ?>"></i>
						<?php echo epc_marketing_h(preg_replace('/^\d+\.\s/', '', $str['title'])); ?>
						<span class="badge"><?php echo (int)$completion['by_strategy'][$key]['pct']; ?>%</span>
					</a>
				</li>
				<?php endforeach; ?>
			</ul>

			<div style="margin-top:16px;">

			<?php if ($activeStrategy === 'overview'): ?>
				<h4><i class="fa fa-calendar-check-o"></i> 90-day roadmap</h4>
				<table class="table table-bordered table-condensed">
					<thead><tr><th>Phase</th><th>Focus</th><th>Strategies</th></tr></thead>
					<tbody>
						<tr><td>Days 0–30</td><td>Measure, ads, WhatsApp, trust, quick wins</td><td>1, 3, 5, 6, 10</td></tr>
						<tr><td>Days 1–90</td><td>SEO content, marketplaces, email</td><td>2, 4, 8</td></tr>
						<tr><td>Days 30–180</td><td>GCC expansion, B2B partners</td><td>7, 9</td></tr>
					</tbody>
				</table>

				<h4>Strategy progress</h4>
				<table class="table table-striped table-condensed">
					<thead><tr><th>Strategy</th><th>Timeline</th><th>Tasks</th><th>Progress</th></tr></thead>
					<tbody>
					<?php foreach ($strategies as $key => $str):
						$bs = $completion['by_strategy'][$key];
					?>
						<tr>
							<td><a href="<?php echo epc_marketing_h($marketingUrl . '?s=' . rawurlencode($key)); ?>"><?php echo epc_marketing_h($str['title']); ?></a></td>
							<td><?php echo epc_marketing_h($str['timeline']); ?></td>
							<td><?php echo (int)$bs['done']; ?> / <?php echo (int)$bs['total']; ?></td>
							<td style="min-width:140px;">
								<div class="epc-mkt-progress-bar"><span style="width:<?php echo (int)$bs['pct']; ?>%"></span></div>
								<?php echo (int)$bs['pct']; ?>%
							</td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>

				<h4>External tools (monitor daily/weekly)</h4>
				<p>
					<a class="btn btn-default btn-sm" href="https://analytics.google.com/" target="_blank" rel="noopener">Google Analytics (<?php echo epc_marketing_h($live['ga_property']); ?>)</a>
					<a class="btn btn-default btn-sm" href="https://search.google.com/search-console" target="_blank" rel="noopener">Search Console</a>
					<a class="btn btn-default btn-sm" href="<?php echo epc_marketing_h($live['sitemap_url']); ?>" target="_blank" rel="noopener">Product sitemap</a>
					<a class="btn btn-default btn-sm" href="https://ads.google.com/" target="_blank" rel="noopener">Google Ads</a>
					<a class="btn btn-default btn-sm" href="https://business.facebook.com/" target="_blank" rel="noopener">Meta Business</a>
				</p>

				<h4>Recent reviews (all strategies)</h4>
				<?php if (empty($reviews)): ?>
					<p class="text-muted">No reviews yet — open each strategy tab and submit a weekly review.</p>
				<?php else: ?>
					<?php foreach ($reviews as $rev):
						$sk = (string)$rev['strategy_key'];
						$title = isset($strategies[$sk]) ? $strategies[$sk]['title'] : $sk;
					?>
					<div class="epc-mkt-review-item">
						<strong><?php echo epc_marketing_h($title); ?></strong>
						<span class="text-muted"><?php echo epc_marketing_h(date('Y-m-d H:i', (int)$rev['created_at'])); ?></span>
						— Score <?php echo (int)$rev['score']; ?>/5 (<?php echo epc_marketing_h($rev['review_type']); ?>)
						<?php if ($rev['notes'] !== ''): ?><br><small><?php echo epc_marketing_h($rev['notes']); ?></small><?php endif; ?>
					</div>
					<?php endforeach; ?>
				<?php endif; ?>

			<?php else:
				$str = $strategies[$activeStrategy];
				$bs = $completion['by_strategy'][$activeStrategy];
			?>
				<div class="epc-mkt-strategy-head" style="border-left-color:<?php echo epc_marketing_h($str['color']); ?>">
					<h4 style="margin:0 0 6px;"><i class="fa <?php echo epc_marketing_h($str['icon']); ?>"></i> <?php echo epc_marketing_h($str['title']); ?></h4>
					<p style="margin:0;"><?php echo epc_marketing_h($str['summary']); ?> · Timeline: <strong><?php echo epc_marketing_h($str['timeline']); ?></strong>
						· Tasks <?php echo (int)$bs['done']; ?>/<?php echo (int)$bs['total']; ?> (<?php echo (int)$bs['pct']; ?>%)</p>
				</div>

				<div class="epc-mkt-subnav">
					<button type="button" class="btn btn-primary btn-sm epc-mkt-sub" data-panel="guide">Guide</button>
					<button type="button" class="btn btn-default btn-sm epc-mkt-sub" data-panel="follow">Follow</button>
					<button type="button" class="btn btn-default btn-sm epc-mkt-sub" data-panel="review">Review</button>
					<button type="button" class="btn btn-default btn-sm epc-mkt-sub" data-panel="results">Results</button>
				</div>

				<div class="epc-mkt-panel active" id="epc_mkt_panel_guide">
					<h4><i class="fa fa-book"></i> Guidelines</h4>
					<?php foreach ($str['guidelines'] as $section): ?>
						<div class="well well-sm">
							<strong><?php echo epc_marketing_h($section['title']); ?></strong>
							<div><?php echo $section['body']; ?></div>
						</div>
					<?php endforeach; ?>
					<?php if (!empty($str['links'])): ?>
					<h5>Quick links</h5>
					<p>
						<?php foreach ($str['links'] as $lnk):
							$href = epc_marketing_resolve_link($lnk['url'], $backend, $live['domain']);
						?>
							<a class="btn btn-default btn-xs" href="<?php echo epc_marketing_h($href); ?>" <?php echo !empty($lnk['external']) ? 'target="_blank" rel="noopener"' : ''; ?>>
								<?php echo epc_marketing_h($lnk['label']); ?>
							</a>
						<?php endforeach; ?>
					</p>
					<?php endif; ?>
				</div>

				<div class="epc-mkt-panel" id="epc_mkt_panel_follow">
					<h4><i class="fa fa-check-square-o"></i> Follow — action checklist</h4>
					<p class="text-muted">Check off tasks as you complete them. Progress saves automatically.</p>
					<ul class="list-unstyled" id="epc_mkt_tasks">
					<?php foreach ($str['follow_tasks'] as $taskKey => $label):
						$done = !empty($progress[$activeStrategy][$taskKey]['done']);
					?>
						<li class="epc-mkt-task <?php echo $done ? 'done' : ''; ?>" style="margin:8px 0;">
							<label style="font-weight:normal;cursor:pointer;">
								<input type="checkbox" class="epc-mkt-task-cb" data-strategy="<?php echo epc_marketing_h($activeStrategy); ?>" data-task="<?php echo epc_marketing_h($taskKey); ?>" <?php echo $done ? 'checked' : ''; ?>>
								<?php echo epc_marketing_h($label); ?>
							</label>
							<?php if ($done && !empty($progress[$activeStrategy][$taskKey]['done_at'])): ?>
								<small class="text-muted"> — <?php echo epc_marketing_h(date('Y-m-d', (int)$progress[$activeStrategy][$taskKey]['done_at'])); ?></small>
							<?php endif; ?>
						</li>
					<?php endforeach; ?>
					</ul>
				</div>

				<div class="epc-mkt-panel" id="epc_mkt_panel_review">
					<h4><i class="fa fa-search"></i> Review — weekly / monthly questions</h4>
					<ul>
						<?php foreach ($str['review_checklist'] as $q): ?>
							<li><?php echo epc_marketing_h($q); ?></li>
						<?php endforeach; ?>
					</ul>
					<hr>
					<h5>Submit review score</h5>
					<form id="epc_mkt_review_form" class="form-inline">
						<input type="hidden" name="strategy_key" value="<?php echo epc_marketing_h($activeStrategy); ?>">
						<select name="review_type" class="form-control input-sm">
							<option value="weekly">Weekly</option>
							<option value="monthly">Monthly</option>
						</select>
						<select name="score" class="form-control input-sm">
							<?php for ($i = 1; $i <= 5; $i++): ?>
							<option value="<?php echo $i; ?>"><?php echo $i; ?> — <?php echo array('', 'Poor', 'Fair', 'Good', 'Very good', 'Excellent')[$i]; ?></option>
							<?php endfor; ?>
						</select>
						<input type="text" name="notes" class="form-control input-sm" placeholder="Notes (optional)" style="min-width:220px;">
						<button type="submit" class="btn btn-success btn-sm">Save review</button>
					</form>
					<div id="epc_mkt_review_msg" class="alert" style="display:none;margin-top:10px;"></div>
				</div>

				<div class="epc-mkt-panel" id="epc_mkt_panel_results">
					<h4><i class="fa fa-bar-chart"></i> Results — KPIs to monitor</h4>
					<table class="table table-bordered table-condensed">
						<thead><tr><th>KPI</th><th>Target</th><th>Source</th><th>Latest logged</th><th>Log value</th></tr></thead>
						<tbody>
						<?php foreach ($str['kpis'] as $kpiKey => $meta):
							$row = isset($latestKpis[$kpiKey]) ? $latestKpis[$kpiKey] : null;
							$liveVal = '';
							if ($activeStrategy === 'quick_wins') {
								if ($kpiKey === 'site_orders_30d') {
									$liveVal = (string)(int)$live['orders_30d'];
								} elseif ($kpiKey === 'registered_users') {
									$liveVal = (string)(int)$live['users_total'];
								} elseif ($kpiKey === 'price_rows') {
									$liveVal = number_format((int)$live['price_rows']);
								} elseif ($kpiKey === 'strategy_completion') {
									$liveVal = (string)(int)$completion['pct'] . '%';
								}
							}
							if ($kpiKey === 'marketplace_orders' && (int)$live['marketplace_orders'] > 0) {
								$liveVal = (string)(int)$live['marketplace_orders'];
							}
							if ($kpiKey === 'wa_api_sent' && (int)$live['whatsapp_api_sent'] > 0) {
								$liveVal = (string)(int)$live['whatsapp_api_sent'];
							}
						?>
							<tr>
								<td><?php echo epc_marketing_h($meta['label']); ?></td>
								<td><?php echo epc_marketing_h($meta['target']); ?></td>
								<td><?php echo epc_marketing_h($meta['source']); ?>
									<?php if ($liveVal !== ''): ?><span class="epc-mkt-live-tag">live: <?php echo epc_marketing_h($liveVal); ?></span><?php endif; ?>
								</td>
								<td><?php echo $row ? epc_marketing_h(date('Y-m-d', (int)$row['recorded_at'])) : '—'; ?></td>
								<td><?php echo $row ? epc_marketing_h($row['value_text']) : '—'; ?></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>

					<h5>Record KPI (manual — from GA, Ads, etc.)</h5>
					<form id="epc_mkt_kpi_form" class="form-inline">
						<input type="hidden" name="strategy_key" value="<?php echo epc_marketing_h($activeStrategy); ?>">
						<select name="kpi_key" class="form-control input-sm">
							<?php foreach ($str['kpis'] as $kpiKey => $meta): ?>
							<option value="<?php echo epc_marketing_h($kpiKey); ?>"><?php echo epc_marketing_h($meta['label']); ?></option>
							<?php endforeach; ?>
						</select>
						<input type="text" name="value" class="form-control input-sm" placeholder="Value" required>
						<input type="text" name="note" class="form-control input-sm" placeholder="Note (week of…)" style="min-width:160px;">
						<button type="submit" class="btn btn-primary btn-sm">Save KPI</button>
					</form>
					<div id="epc_mkt_kpi_msg" class="alert" style="display:none;margin-top:10px;"></div>

					<?php
					$firstKpi = array_key_first($str['kpis']);
					if ($firstKpi):
						$hist = epc_marketing_kpi_history($db_link, $firstKpi, 8);
					?>
					<h5 style="margin-top:18px;">History sample (<?php echo epc_marketing_h($str['kpis'][$firstKpi]['label']); ?>)</h5>
					<ul class="list-unstyled">
						<?php foreach ($hist as $h): ?>
						<li><small><?php echo epc_marketing_h(date('Y-m-d', (int)$h['recorded_at'])); ?> — <?php echo epc_marketing_h($h['value_text']); ?>
							<?php if ($h['note'] !== ''): ?> (<?php echo epc_marketing_h($h['note']); ?>)<?php endif; ?></small></li>
						<?php endforeach; ?>
						<?php if (empty($hist)): ?><li class="text-muted">No entries yet</li><?php endif; ?>
					</ul>
					<?php endif; ?>
				</div>

			<?php endif; ?>

			</div>
			<div id="epc_mkt_global_msg" class="alert" style="display:none;margin-top:12px;"></div>
		</div>
	</div>
</div>

<script>
(function(){
	var url = <?php echo json_encode($marketingUrl); ?>;
	var csrf = <?php echo json_encode($csrf); ?>;

	function post(action, data, cb) {
		var body = new FormData();
		body.append('action', action);
		if (csrf) body.append('csrf_guard_key', csrf);
		for (var k in data) { if (data.hasOwnProperty(k)) body.append(k, data[k]); }
		fetch(url, { method: 'POST', body: body, credentials: 'same-origin' })
			.then(function(r) { return r.json(); })
			.then(cb)
			.catch(function(e) { cb({ status: false, message: e.message || 'Request failed' }); });
	}

	function flash(el, ok, text) {
		if (!el) return;
		el.className = 'alert alert-' + (ok ? 'success' : 'danger');
		el.textContent = text;
		el.style.display = 'block';
		setTimeout(function() { el.style.display = 'none'; }, 4000);
	}

	document.querySelectorAll('.epc-mkt-sub').forEach(function(btn) {
		btn.addEventListener('click', function() {
			var panel = btn.getAttribute('data-panel');
			document.querySelectorAll('.epc-mkt-sub').forEach(function(b) {
				b.className = 'btn btn-default btn-sm epc-mkt-sub';
			});
			btn.className = 'btn btn-primary btn-sm epc-mkt-sub';
			document.querySelectorAll('.epc-mkt-panel').forEach(function(p) {
				p.classList.remove('active');
			});
			var target = document.getElementById('epc_mkt_panel_' + panel);
			if (target) target.classList.add('active');
		});
	});

	document.querySelectorAll('.epc-mkt-task-cb').forEach(function(cb) {
		cb.addEventListener('change', function() {
			var li = cb.closest('.epc-mkt-task');
			post('toggle_task', {
				strategy_key: cb.getAttribute('data-strategy'),
				task_key: cb.getAttribute('data-task'),
				is_done: cb.checked ? '1' : '0'
			}, function(res) {
				flash(document.getElementById('epc_mkt_global_msg'), res.status, res.message || (res.status ? 'Saved' : 'Error'));
				if (res.status && li) {
					if (cb.checked) li.classList.add('done'); else li.classList.remove('done');
					if (res.completion) {
						var el = document.getElementById('epc_mkt_overall_pct');
						if (el) el.textContent = res.completion.pct + '%';
					}
				} else if (!res.status) {
					cb.checked = !cb.checked;
				}
			});
		});
	});

	var kpiForm = document.getElementById('epc_mkt_kpi_form');
	if (kpiForm) {
		kpiForm.addEventListener('submit', function(ev) {
			ev.preventDefault();
			var fd = new FormData(kpiForm);
			post('save_kpi', {
				strategy_key: fd.get('strategy_key'),
				kpi_key: fd.get('kpi_key'),
				value: fd.get('value'),
				note: fd.get('note') || ''
			}, function(res) {
				flash(document.getElementById('epc_mkt_kpi_msg'), res.status, res.message || '');
				if (res.status) setTimeout(function() { location.reload(); }, 800);
			});
		});
	}

	var revForm = document.getElementById('epc_mkt_review_form');
	if (revForm) {
		revForm.addEventListener('submit', function(ev) {
			ev.preventDefault();
			var fd = new FormData(revForm);
			post('save_review', {
				strategy_key: fd.get('strategy_key'),
				review_type: fd.get('review_type'),
				score: fd.get('score'),
				notes: fd.get('notes') || ''
			}, function(res) {
				flash(document.getElementById('epc_mkt_review_msg'), res.status, res.message || '');
				if (res.status) revForm.querySelector('[name=notes]').value = '';
			});
		});
	}
})();
</script>
