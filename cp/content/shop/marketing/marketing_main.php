<?php
/**
 * CP — Marketing & growth hub (10 strategies: guide, follow, review, results).
 * URL: /cp/shop/marketing/marketing
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/marketing/epc_marketing_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';

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

$ver = epc_cp_page_asset_version();
epc_cp_register_page_assets(
	array(
		'/content/shop/finance/epc_erp_ui.css?v=' . rawurlencode($ver),
		'/content/general_pages/epc_marketing_hub_css.php?v=' . rawurlencode($ver . 'mktHub2'),
	),
	array(
		'/content/general_pages/epc_marketing_hub_js.php?v=' . rawurlencode($ver . 'mktHub2'),
	)
);

epc_marketing_ensure_schema($db_link);

$strategies = epc_marketing_strategies();
$progress = epc_marketing_load_progress($db_link);
$completion = epc_marketing_completion_stats($strategies, $progress);
$live = epc_marketing_live_snapshot($db_link);
$live['domain'] = rtrim((string)$DP_Config->domain_path, '/');
$live['sitemap_url'] = $live['domain'] . '/sitemap-products.php';
$latestKpis = epc_marketing_latest_kpis($db_link, $strategies);
$reviews = epc_marketing_recent_reviews($db_link, 15);

$backend = trim((string)$DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}
$marketingUrl = '/' . $backend . '/shop/marketing/marketing';
$ajaxUrl = '/' . $backend . '/content/shop/marketing/ajax_marketing_endpoint.php';
$broadcastUrl = '/' . $backend . '/control/portal/epc_marketing_broadcast';
$socialUrl = '/' . $backend . '/control/portal/epc_social_media_hub';
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$activeStrategy = isset($_GET['s']) ? (string)$_GET['s'] : 'overview';
if ($activeStrategy !== 'overview' && !isset($strategies[$activeStrategy])) {
	$activeStrategy = 'overview';
}

$mbStats = array(
	'campaigns' => 0,
	'emails_sent' => 0,
	'whatsapp_sent' => 0,
	'email_recipients' => 0,
	'whatsapp_recipients' => 0,
);
$mbCampaigns = array();
try {
	$mbHelpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/marketing/epc_marketing_broadcast_helpers.php';
	if (is_file($mbHelpers)) {
		require_once $mbHelpers;
		if (function_exists('epc_mb_dashboard_stats')) {
			$mbStats = array_merge($mbStats, epc_mb_dashboard_stats($db_link));
		}
		if (function_exists('epc_mb_list_campaigns')) {
			$mbCampaigns = epc_mb_list_campaigns($db_link, 6);
		}
	}
} catch (Throwable $e) {
	// Broadcast module optional for hub overview
}

epc_cp_page_frame_open(array(
	'class' => 'epc-erp-shell',
	'hero' => array(
		'badge' => 'Marketing ops',
		'title' => 'Growth hub',
		'sub' => 'Plan, execute, and review channel work across SEO, ads, WhatsApp, email, and marketplaces.',
		'actions' => array(
			array(
				'label' => 'Broadcast',
				'url' => $broadcastUrl,
				'icon' => 'fa-bullhorn',
				'primary' => true,
			),
			array(
				'label' => 'Storefront',
				'url' => $live['domain'],
				'icon' => 'fa-external-link',
			),
		),
	),
));
?>

<div
	id="epcMktHub"
	class="epc-mkt-hub"
	data-post-url="<?php echo epc_marketing_h($ajaxUrl); ?>"
	data-csrf="<?php echo epc_marketing_h($csrf); ?>"
	data-initial="<?php echo epc_marketing_h($activeStrategy); ?>"
>
	<div class="epc-mkt-hub__toolbar">
		<a class="epc-m-btn epc-m-btn--primary" href="<?php echo epc_marketing_h($broadcastUrl); ?>">
			<i class="fa fa-paper-plane"></i> Send campaign
		</a>
		<a class="epc-m-btn" href="<?php echo epc_marketing_h($socialUrl); ?>">
			<i class="fa fa-share-alt"></i> Social hub
		</a>
		<a class="epc-m-btn epc-m-btn--muted" href="<?php echo epc_marketing_h($live['sitemap_url']); ?>" target="_blank" rel="noopener">
			<i class="fa fa-sitemap"></i> Sitemap
		</a>
		<button type="button" class="epc-m-btn epc-m-btn--muted" id="epcMktSnapshot">
			<i class="fa fa-refresh"></i> Snapshot
		</button>
		<span class="epc-mkt-hub__spacer"></span>
		<span class="epc-m-btn epc-m-btn--muted" style="cursor:default;">
			Progress
			<strong id="epcMktOverallPct"><?php echo (int)$completion['pct']; ?>%</strong>
			(<span id="epcMktOverallDone"><?php echo (int)$completion['done']; ?></span>/<span id="epcMktOverallTotal"><?php echo (int)$completion['total']; ?></span>)
		</span>
	</div>

	<p class="epc-mkt-hub__hint">
		Operate like a growth team: pick a channel, follow the checklist, log KPIs weekly, and review what moved orders.
		Live snapshot <?php echo epc_marketing_h($live['generated_at']); ?>.
	</p>

	<div class="epc-mkt-kpi">
		<div class="kpi"><div class="lbl">Orders (30d)</div><div class="val"><?php echo (int)$live['orders_30d']; ?></div></div>
		<div class="kpi"><div class="lbl">Orders (7d)</div><div class="val"><?php echo (int)$live['orders_7d']; ?></div></div>
		<div class="kpi"><div class="lbl">Broadcast campaigns</div><div class="val"><?php echo (int)$mbStats['campaigns']; ?></div></div>
		<div class="kpi"><div class="lbl">Email / WA sent</div><div class="val"><?php echo (int)$mbStats['emails_sent']; ?> / <?php echo (int)$mbStats['whatsapp_sent']; ?></div></div>
	</div>

	<div class="epc-mkt-channels">
		<a class="epc-mkt-channel" href="<?php echo epc_marketing_h($broadcastUrl); ?>">
			<i class="fa fa-envelope"></i>
			<strong>Email &amp; WhatsApp</strong>
			<span><?php echo (int)$mbStats['email_recipients']; ?> email · <?php echo (int)$mbStats['whatsapp_recipients']; ?> WhatsApp recipients</span>
		</a>
		<a class="epc-mkt-channel" href="<?php echo epc_marketing_h($socialUrl); ?>">
			<i class="fa fa-instagram"></i>
			<strong>Social media</strong>
			<span>Content packs, publishing checklist, and account setup</span>
		</a>
		<a class="epc-mkt-channel" href="https://analytics.google.com/" target="_blank" rel="noopener">
			<i class="fa fa-line-chart"></i>
			<strong>Analytics</strong>
			<span>GA <?php echo epc_marketing_h($live['ga_property']); ?> · Search Console</span>
		</a>
		<a class="epc-mkt-channel" href="#seo" data-strategy-goto="seo">
			<i class="fa fa-search"></i>
			<strong>SEO &amp; content</strong>
			<span>Open the SEO strategy workbench</span>
		</a>
	</div>

	<div class="epc-mkt-layout">
		<aside class="epc-mkt-nav">
			<div class="epc-mkt-nav__h">
				<h3>Playbook</h3>
				<span>10 strategies · side nav</span>
			</div>
			<ul class="epc-mkt-nav__list">
				<li>
					<a href="#overview" data-strategy="overview" data-title="Growth overview" data-meta="Channel health, cadence, and strategy progress" class="<?php echo $activeStrategy === 'overview' ? 'is-active' : ''; ?>">
						<i class="fa fa-dashboard"></i>
						Overview
						<span class="epc-mkt-nav__meta" data-nav-pct="overview"><?php echo (int)$completion['pct']; ?>%</span>
					</a>
				</li>
				<?php foreach ($strategies as $key => $str):
					$bs = $completion['by_strategy'][$key];
					$short = preg_replace('/^\d+\.\s/', '', (string)$str['title']);
				?>
				<li>
					<a
						href="#<?php echo epc_marketing_h($key); ?>"
						data-strategy="<?php echo epc_marketing_h($key); ?>"
						data-title="<?php echo epc_marketing_h($str['title']); ?>"
						data-meta="<?php echo epc_marketing_h($str['timeline'] . ' · ' . $bs['done'] . '/' . $bs['total'] . ' tasks'); ?>"
						class="<?php echo $activeStrategy === $key ? 'is-active' : ''; ?>"
					>
						<i class="fa <?php echo epc_marketing_h($str['icon']); ?>"></i>
						<?php echo epc_marketing_h($short); ?>
						<span class="epc-mkt-nav__meta" data-nav-pct="<?php echo epc_marketing_h($key); ?>"><?php echo (int)$bs['pct']; ?>%</span>
					</a>
				</li>
				<?php endforeach; ?>
			</ul>
		</aside>

		<section class="epc-mkt-main">
			<div class="epc-mkt-main__h">
				<h3 id="epcMktActiveTitle">Growth overview</h3>
				<span id="epcMktActiveMeta">Channel health, cadence, and strategy progress</span>
			</div>
			<div class="epc-mkt-main__body">

				<div id="epcMktOverview" <?php echo $activeStrategy === 'overview' ? '' : 'hidden'; ?>>
					<div class="epc-mkt-section-title">90-day roadmap</div>
					<div class="epc-mkt-table-wrap">
						<table class="table table-condensed">
							<thead><tr><th>Phase</th><th>Focus</th><th>Strategies</th></tr></thead>
							<tbody>
								<tr><td>Days 0–30</td><td>Measure, ads, WhatsApp, trust, quick wins</td><td>1, 3, 5, 6, 10</td></tr>
								<tr><td>Days 1–90</td><td>SEO content, marketplaces, email</td><td>2, 4, 8</td></tr>
								<tr><td>Days 30–180</td><td>GCC expansion, B2B partners</td><td>7, 9</td></tr>
							</tbody>
						</table>
					</div>

					<div class="epc-mkt-section-title">Strategy progress</div>
					<div class="epc-mkt-table-wrap">
						<table class="table table-condensed">
							<thead><tr><th>Strategy</th><th>Timeline</th><th>Tasks</th><th>Progress</th></tr></thead>
							<tbody>
							<?php foreach ($strategies as $key => $str):
								$bs = $completion['by_strategy'][$key];
							?>
								<tr>
									<td><a href="#<?php echo epc_marketing_h($key); ?>" data-strategy-goto="<?php echo epc_marketing_h($key); ?>"><?php echo epc_marketing_h($str['title']); ?></a></td>
									<td><?php echo epc_marketing_h($str['timeline']); ?></td>
									<td data-progress-counts="<?php echo epc_marketing_h($key); ?>"><?php echo (int)$bs['done']; ?> / <?php echo (int)$bs['total']; ?></td>
									<td style="min-width:140px;">
										<div class="epc-mkt-progress-bar"><span data-progress-bar="<?php echo epc_marketing_h($key); ?>" style="width:<?php echo (int)$bs['pct']; ?>%"></span></div>
										<span data-progress-label="<?php echo epc_marketing_h($key); ?>"><?php echo (int)$bs['pct']; ?>%</span>
									</td>
								</tr>
							<?php endforeach; ?>
							</tbody>
						</table>
					</div>

					<?php if (!empty($mbCampaigns)): ?>
					<div class="epc-mkt-section-title">Recent broadcasts</div>
					<div class="epc-mkt-campaigns">
						<ul>
						<?php foreach ($mbCampaigns as $c):
							$name = (string)($c['name'] ?? $c['subject'] ?? ('Campaign #' . (int)($c['id'] ?? 0)));
							$channel = (string)($c['channel'] ?? '');
							$sent = (int)($c['sent_ok'] ?? 0);
							$created = !empty($c['created_at']) ? (int)$c['created_at'] : 0;
						?>
							<li>
								<strong><?php echo epc_marketing_h($name); ?></strong>
								<span class="text-muted"> · <?php echo epc_marketing_h($channel); ?> · <?php echo $sent; ?> sent
								<?php if ($created > 0): ?> · <?php echo epc_marketing_h(date('Y-m-d', $created)); ?><?php endif; ?></span>
							</li>
						<?php endforeach; ?>
						</ul>
						<p style="margin:10px 0 0;"><a class="epc-m-btn" href="<?php echo epc_marketing_h($broadcastUrl); ?>">Open broadcast hub</a></p>
					</div>
					<?php endif; ?>

					<div class="epc-mkt-section-title">External monitors</div>
					<p style="margin:0 0 16px;">
						<a class="epc-m-btn" href="https://analytics.google.com/" target="_blank" rel="noopener">Google Analytics</a>
						<a class="epc-m-btn" href="https://search.google.com/search-console" target="_blank" rel="noopener">Search Console</a>
						<a class="epc-m-btn" href="https://ads.google.com/" target="_blank" rel="noopener">Google Ads</a>
						<a class="epc-m-btn" href="https://business.facebook.com/" target="_blank" rel="noopener">Meta Business</a>
					</p>

					<div class="epc-mkt-section-title">Recent reviews</div>
					<?php if (empty($reviews)): ?>
						<p class="text-muted" style="margin:0;">No reviews yet — open a strategy and submit a weekly score.</p>
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
				</div>

				<div id="epcMktWorkbench" <?php echo $activeStrategy === 'overview' ? 'hidden' : ''; ?>>
				<?php foreach ($strategies as $key => $str):
					$bs = $completion['by_strategy'][$key];
					$isActive = ($activeStrategy === $key);
				?>
					<div class="epc-mkt-strategy" data-strategy="<?php echo epc_marketing_h($key); ?>" <?php echo $isActive ? '' : 'hidden'; ?>>
						<div class="epc-mkt-strategy-head" style="border-left-color:<?php echo epc_marketing_h($str['color']); ?>">
							<h4><i class="fa <?php echo epc_marketing_h($str['icon']); ?>"></i> <?php echo epc_marketing_h($str['title']); ?></h4>
							<p>
								<?php echo epc_marketing_h($str['summary']); ?>
								· Timeline: <strong><?php echo epc_marketing_h($str['timeline']); ?></strong>
								· Tasks <span data-progress-counts="<?php echo epc_marketing_h($key); ?>"><?php echo (int)$bs['done']; ?>/<?php echo (int)$bs['total']; ?></span>
								(<span data-progress-label="<?php echo epc_marketing_h($key); ?>"><?php echo (int)$bs['pct']; ?>%</span>)
							</p>
							<div class="epc-mkt-progress-bar" style="margin-top:10px;">
								<span data-progress-bar="<?php echo epc_marketing_h($key); ?>" style="width:<?php echo (int)$bs['pct']; ?>%"></span>
							</div>
						</div>

						<div class="epc-mkt-subnav">
							<button type="button" class="epc-mkt-sub is-active btn-primary" data-panel="guide">Guide</button>
							<button type="button" class="epc-mkt-sub" data-panel="follow">Follow</button>
							<button type="button" class="epc-mkt-sub" data-panel="review">Review</button>
							<button type="button" class="epc-mkt-sub" data-panel="results">Results</button>
						</div>

						<div class="epc-mkt-panel active" data-panel="guide">
							<div class="epc-mkt-section-title">Guidelines</div>
							<?php foreach ($str['guidelines'] as $section): ?>
								<div class="epc-mkt-guide-card">
									<strong><?php echo epc_marketing_h($section['title']); ?></strong>
									<div><?php echo $section['body']; ?></div>
								</div>
							<?php endforeach; ?>
							<?php if (!empty($str['links'])): ?>
							<div class="epc-mkt-section-title" style="margin-top:14px;">Quick links</div>
							<p style="margin:0;">
								<?php foreach ($str['links'] as $lnk):
									$href = epc_marketing_resolve_link($lnk['url'], $backend, $live['domain']);
								?>
									<a class="epc-m-btn" href="<?php echo epc_marketing_h($href); ?>" <?php echo !empty($lnk['external']) ? 'target="_blank" rel="noopener"' : ''; ?>>
										<?php echo epc_marketing_h($lnk['label']); ?>
									</a>
								<?php endforeach; ?>
							</p>
							<?php endif; ?>
						</div>

						<div class="epc-mkt-panel" data-panel="follow">
							<div class="epc-mkt-section-title">Follow — action checklist</div>
							<p class="text-muted" style="margin:0 0 12px;">Check off tasks as you complete them. Progress saves automatically.</p>
							<?php foreach ($str['follow_tasks'] as $taskKey => $label):
								$done = !empty($progress[$key][$taskKey]['done']);
							?>
								<div class="epc-mkt-task <?php echo $done ? 'done' : ''; ?>">
									<label>
										<input type="checkbox" class="epc-mkt-task-cb" data-strategy="<?php echo epc_marketing_h($key); ?>" data-task="<?php echo epc_marketing_h($taskKey); ?>" <?php echo $done ? 'checked' : ''; ?>>
										<span>
											<?php echo epc_marketing_h($label); ?>
											<?php if ($done && !empty($progress[$key][$taskKey]['done_at'])): ?>
												<small class="text-muted"> — <?php echo epc_marketing_h(date('Y-m-d', (int)$progress[$key][$taskKey]['done_at'])); ?></small>
											<?php endif; ?>
										</span>
									</label>
								</div>
							<?php endforeach; ?>
						</div>

						<div class="epc-mkt-panel" data-panel="review">
							<div class="epc-mkt-section-title">Review questions</div>
							<ul>
								<?php foreach ($str['review_checklist'] as $q): ?>
									<li><?php echo epc_marketing_h($q); ?></li>
								<?php endforeach; ?>
							</ul>
							<div class="epc-mkt-section-title" style="margin-top:16px;">Submit review score</div>
							<form class="epc-mkt-review-form form-inline">
								<input type="hidden" name="strategy_key" value="<?php echo epc_marketing_h($key); ?>">
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
								<button type="submit" class="epc-m-btn epc-m-btn--primary">Save review</button>
							</form>
						</div>

						<div class="epc-mkt-panel" data-panel="results">
							<div class="epc-mkt-section-title">Results — KPIs</div>
							<div class="epc-mkt-table-wrap">
								<table class="table table-condensed">
									<thead><tr><th>KPI</th><th>Target</th><th>Source</th><th>Latest</th><th>Value</th></tr></thead>
									<tbody>
									<?php foreach ($str['kpis'] as $kpiKey => $meta):
										$row = isset($latestKpis[$kpiKey]) ? $latestKpis[$kpiKey] : null;
										$liveVal = '';
										if ($key === 'quick_wins') {
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
							</div>

							<div class="epc-mkt-section-title">Record KPI</div>
							<form class="epc-mkt-kpi-form form-inline">
								<input type="hidden" name="strategy_key" value="<?php echo epc_marketing_h($key); ?>">
								<select name="kpi_key" class="form-control input-sm">
									<?php foreach ($str['kpis'] as $kpiKey => $meta): ?>
									<option value="<?php echo epc_marketing_h($kpiKey); ?>"><?php echo epc_marketing_h($meta['label']); ?></option>
									<?php endforeach; ?>
								</select>
								<input type="text" name="value" class="form-control input-sm" placeholder="Value" required>
								<input type="text" name="note" class="form-control input-sm" placeholder="Note (week of…)" style="min-width:160px;">
								<button type="submit" class="epc-m-btn epc-m-btn--primary">Save KPI</button>
								<div class="epc-mkt-form-msg" hidden></div>
							</form>

							<?php
							$firstKpi = array_key_first($str['kpis']);
							if ($firstKpi):
								$hist = epc_marketing_kpi_history($db_link, $firstKpi, 8);
							?>
							<div class="epc-mkt-section-title" style="margin-top:18px;">History · <?php echo epc_marketing_h($str['kpis'][$firstKpi]['label']); ?></div>
							<ul class="list-unstyled" style="margin:0;">
								<?php foreach ($hist as $h): ?>
								<li><small><?php echo epc_marketing_h(date('Y-m-d', (int)$h['recorded_at'])); ?> — <?php echo epc_marketing_h($h['value_text']); ?>
									<?php if ($h['note'] !== ''): ?> (<?php echo epc_marketing_h($h['note']); ?>)<?php endif; ?></small></li>
								<?php endforeach; ?>
								<?php if (empty($hist)): ?><li class="text-muted">No entries yet</li><?php endif; ?>
							</ul>
							<?php endif; ?>
						</div>
					</div>
				<?php endforeach; ?>
				</div>

			</div>
		</section>
	</div>

	<div id="epcMktToast" class="epc-mkt-toast" hidden></div>
</div>

<?php
epc_cp_page_frame_close();
