<?php
/**
 * ERP tab — UAE FTA legislation library (legislation.aspx only) + operational compliance.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_knowledge.php';

epc_uae_tax_compliance_ensure_schema($db_link);
epc_uae_tax_knowledge_seed_kb($db_link);

$ftaCache = epc_uae_fta_get_cached_legislation($db_link);
$legislation = $ftaCache['legislation'] ?? $ftaCache['items'] ?? array();
foreach ($legislation as $i => $legRow) {
	$legislation[$i] = epc_uae_tax_legislation_enrich_item($legRow, $db_link);
}
$newSince = $ftaCache['new_since_last'] ?? array();
$newCountTotal = (int)($ftaCache['new_count'] ?? count($newSince));
$changedCountTotal = (int)($ftaCache['changed_count'] ?? count($ftaCache['changed_since_last'] ?? array()));
$summariesNeedRegen = !empty($ftaCache['summaries_need_regen']);
if (!$summariesNeedRegen && !empty($legislation)) {
	$summariesNeedRegen = epc_uae_tax_legislation_summaries_need_regen($legislation);
}
$overallSummaries = $ftaCache['overall_summaries'] ?? epc_uae_tax_overall_summaries($db_link, $legislation);
if (!empty($overallSummaries)) {
	$firstOs = reset($overallSummaries);
	if (empty($firstOs['bullets'])) {
		$overallSummaries = epc_uae_tax_overall_summaries($db_link, $legislation);
	}
}
$chartStats = $ftaCache['chart_stats'] ?? epc_uae_tax_legislation_chart_stats($legislation);
$sections = epc_uae_tax_compliance_guide_sections();
$ct = epc_uae_corporate_tax_report($db_link, $date_from, $date_to);
$ctAdj = epc_uae_ct_get_adjustments($db_link, $date_from, $date_to);
$advancePeriod = epc_uae_vat_advance_period_summary($db_link, $date_from, $date_to);
$expInput = epc_uae_vat_input_expenses_report($db_link, $date_from, $date_to);
$invoiceChecklist = epc_uae_tax_invoice_format_checklist();
$legislationUrl = epc_uae_fta_legislation_url();
$ajaxUrl = isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . ($GLOBALS['DP_Config']->backend_dir ?? 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php');
$csrf = isset($csrf) ? $csrf : '';
$tcAssetVer = function_exists('epc_cp_page_asset_version') ? epc_cp_page_asset_version() : '20260720fta5';
$tcBackend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
if ($tcBackend === '') {
	$tcBackend = 'cp';
}
$tcJsSrc = '/' . $tcBackend . '/content/shop/finance/erp/epc_uae_tax_compliance.js?v=' . rawurlencode($tcAssetVer);
if (!isset($GLOBALS['epc_cp_page_assets']) || !is_array($GLOBALS['epc_cp_page_assets'])) {
	$GLOBALS['epc_cp_page_assets'] = array('css' => array(), 'js' => array());
}
$GLOBALS['epc_cp_page_assets']['js'][$tcJsSrc] = true;
$panel = isset($_GET['tax_panel']) ? (string)$_GET['tax_panel'] : 'legislation';
$taxFilter = isset($_GET['tax_type']) ? strtolower(trim((string)$_GET['tax_type'])) : '';
if ($taxFilter === '' && isset($_GET['leg_filter'])) {
	$taxFilter = strtolower(trim((string)$_GET['leg_filter']));
}
if ($taxFilter === 'ct') {
	$taxFilter = 'corporate_tax';
}
if ($taxFilter === 'e-invoice' || $taxFilter === 'einvoice') {
	$taxFilter = 'einvoicing';
}
$taxArea = function_exists('epc_erp_tab_to_area') ? epc_erp_tab_to_area('tax_compliance') : 'tax';
if ($taxArea === '' || $taxArea === 'overview') {
	$taxArea = 'tax';
}
$qaHistory = epc_uae_tax_legislation_qa_history_get($db_link);
?>

<div class="epc-erp-section epc-uae-tax-guide" id="epc_uae_tax_compliance_root" data-erp-ajax="<?php echo epc_erp_h($ajaxUrl); ?>" data-csrf="<?php echo epc_erp_h($csrf); ?>">
	<div class="alert alert-info">
		<strong>UAE FTA Tax compliance</strong> — legislation fetched only from
		<a href="<?php echo epc_erp_h($legislationUrl); ?>" target="_blank" rel="noopener">tax.gov.ae/en/legislation.aspx</a>.
		Returns filed on <a href="https://eservices.tax.gov.ae/" target="_blank" rel="noopener">EmaraTax</a>.
		ERP applies VAT/CT rules at voucher level (GL, purchases, e-invoices).
	</div>

	<ul class="nav nav-pills" style="margin-bottom:16px;">
		<li class="<?php echo $panel === 'legislation' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, $taxArea) . '&tax_panel=legislation'); ?>"><i class="fa fa-gavel"></i> Legislation library</a></li>
		<li class="<?php echo $panel === 'operations' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, $taxArea) . '&tax_panel=operations'); ?>"><i class="fa fa-calculator"></i> Operations</a></li>
		<li class="<?php echo $panel === 'knowledge' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, $taxArea) . '&tax_panel=knowledge'); ?>"><i class="fa fa-book"></i> Process guides</a></li>
	</ul>

<?php if ($panel === 'legislation'):

$legCatColors = array(
	'vat' => '#2563eb', 'corporate_tax' => '#7c3aed', 'excise' => '#ea580c',
	'procedures' => '#64748b', 'einvoicing' => '#059669', 'general' => '#94a3b8',
);
$legCatLabels = array(
	'vat' => 'VAT', 'corporate_tax' => 'CT', 'excise' => 'Excise',
	'procedures' => 'Procedures', 'einvoicing' => 'E-invoice', 'general' => 'General',
);
$moduleLabels = array(
	'einvoice' => 'E-Invoicing', 'vat_return' => 'VAT return', 'pl_ct' => 'P&L / CT',
	'purchases' => 'Purchases', 'gl_journals' => 'GL journals', 'inventory' => 'Inventory', 'customs' => 'Customs',
);
$moduleColors = array(
	'einvoice' => '#059669', 'vat_return' => '#2563eb', 'pl_ct' => '#7c3aed',
	'purchases' => '#0ea5e9', 'gl_journals' => '#64748b', 'inventory' => '#ea580c', 'customs' => '#ca8a04',
);
$filterCounts = array(
	'' => 0, 'vat' => 0, 'corporate_tax' => 0, 'excise' => 0,
	'procedures' => 0, 'einvoicing' => 0, 'general' => 0,
);
// Keep full list in the DOM so VAT/CT/… filters work even if the query string
// is dropped by the shell; JS shows/hides rows and updates the URL.
$allLegItems = array();
foreach ($legislation as $leg) {
	$leg = epc_uae_tax_legislation_enrich_item($leg, $db_link);
	$tt = strtolower(trim((string)($leg['tax_category'] ?? $leg['tax_type'] ?? 'general')));
	if ($tt === '' || !isset($filterCounts[$tt])) {
		$tt = 'general';
	}
	$leg['tax_type'] = $tt;
	$leg['tax_category'] = $tt;
	$filterCounts[$tt]++;
	$filterCounts['']++;
	$allLegItems[] = $leg;
}
$filteredLeg = array();
foreach ($allLegItems as $leg) {
	$tt = (string)($leg['tax_type'] ?? 'general');
	if ($taxFilter === '' || $tt === $taxFilter) {
		$filteredLeg[] = $leg;
	}
}
$legFilterBase = epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, $taxArea) . '&tax_panel=legislation';
$timelineYears = $chartStats['timeline_by_year'] ?? array();
$timelineMax = 1;
foreach ($timelineYears as $cnt) {
	$timelineMax = max($timelineMax, (int)$cnt);
}
?>

	<style>
	.epc-leg-dash { display:flex; flex-wrap:wrap; gap:12px; margin:0 0 18px; }
	.epc-leg-card { flex:1 1 180px; min-width:170px; background:#fff; border:1px solid #e2e8f0; border-radius:8px; padding:12px 14px; border-left:4px solid #2563eb; }
	.epc-leg-card .rate { font-size:22px; font-weight:700; line-height:1.1; }
	.epc-leg-card .cnt { font-size:11px; color:#64748b; margin-top:4px; }
	.epc-leg-charts { display:flex; flex-wrap:wrap; gap:16px; margin:0 0 18px; }
	.epc-leg-chart-box { flex:1 1 260px; background:#fff; border:1px solid #e2e8f0; border-radius:8px; padding:14px; }
	.epc-leg-chart-box h5 { margin:0 0 10px; font-size:13px; font-weight:600; }
	.epc-leg-donut-wrap { display:flex; align-items:center; gap:14px; }
	.epc-leg-legend { font-size:11px; line-height:1.6; }
	.epc-leg-legend span { display:inline-block; width:10px; height:10px; border-radius:2px; margin-right:4px; vertical-align:middle; }
	.epc-leg-timeline-row { display:flex; align-items:center; gap:8px; margin-bottom:6px; font-size:11px; }
	.epc-leg-timeline-row .yr { width:42px; text-align:right; color:#64748b; }
	.epc-leg-timeline-row .bar-wrap { flex:1; background:#f1f5f9; border-radius:4px; height:14px; overflow:hidden; }
	.epc-leg-timeline-row .bar { height:100%; background:#2563eb; border-radius:4px; min-width:2px; }
	.epc-leg-modules { display:flex; flex-wrap:wrap; gap:8px; }
	.epc-leg-mod-node { padding:6px 10px; border-radius:20px; font-size:11px; color:#fff; font-weight:600; }
	.epc-leg-item { border:1px solid #e2e8f0; border-radius:6px; margin-bottom:8px; background:#fff; }
	.epc-leg-item.is-new { border-color:#86efac; background:#f0fdf4; }
	.epc-leg-item-hd { padding:10px 12px; cursor:pointer; display:flex; flex-wrap:wrap; align-items:flex-start; gap:8px; }
	.epc-leg-item-hd:hover { background:#f8fafc; }
	.epc-leg-item-bd { padding:0 12px 12px; border-top:1px solid #f1f5f9; display:none; }
	.epc-leg-item.open .epc-leg-item-bd { display:block; }
	.epc-leg-item.open .epc-leg-chevron { transform:rotate(90deg); }
	.epc-leg-chevron { transition:transform .15s; color:#94a3b8; margin-top:2px; }
	.epc-leg-summary { font-size:12px; line-height:1.55; color:#334155; margin:10px 0; }
	.epc-leg-checklist { margin:0; padding-left:0; list-style:none; font-size:12px; }
	.epc-leg-checklist li { margin-bottom:6px; }
	.epc-leg-check-row.is-done span { text-decoration:line-through; color:#64748b; }
	.epc-leg-qa { background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; padding:14px 16px; margin:0 0 18px; }
	.epc-leg-qa h4 { margin:0 0 10px; font-size:15px; font-weight:600; }
	.epc-leg-qa-prompts { display:flex; flex-wrap:wrap; gap:6px; margin:0 0 10px; }
	.epc-leg-qa-prompts button { font-size:11px; padding:4px 10px; border-radius:16px; border:1px solid #cbd5e1; background:#fff; color:#334155; cursor:pointer; }
	.epc-leg-qa-prompts button:hover { background:#eff6ff; border-color:#93c5fd; }
	.epc-leg-qa-reply { display:none; margin-top:12px; padding-top:12px; border-top:1px solid #e2e8f0; }
	.epc-leg-qa-reply.visible { display:block; }
	.epc-leg-qa-answer { font-size:13px; line-height:1.55; color:#1e293b; }
	.epc-leg-qa-answer ul { margin:8px 0 0; padding-left:18px; }
	.epc-leg-qa-cite { margin-top:10px; font-size:12px; }
	.epc-leg-qa-cite li { margin-bottom:6px; }
	.epc-leg-qa-cite summary { cursor:pointer; color:#2563eb; }
	.epc-leg-qa-history { margin-top:14px; font-size:11px; color:#64748b; }
	.epc-leg-qa-history details { margin-bottom:6px; }
	</style>

	<p>
		<button type="button" class="btn btn-primary btn-sm" id="epc_fta_check_updates"><i class="fa fa-refresh"></i> Fetch legislation updates</button>
		<button type="button" class="btn btn-<?php echo $summariesNeedRegen ? 'warning' : 'default'; ?> btn-sm" id="epc_fta_regen_summaries"<?php if ($summariesNeedRegen): ?> title="Some items lack ERP summaries — click to rebuild"<?php endif; ?>><i class="fa fa-file-text-o"></i> Regenerate all summaries<?php if ($summariesNeedRegen): ?> <span class="label label-warning">Needed</span><?php endif; ?></button>
		<span id="epc_fta_status" class="text-muted" style="margin-left:10px;">
			<?php
			if (!empty($ftaCache['time_fetched_label'])) {
				echo 'Last sync: ' . epc_erp_h($ftaCache['time_fetched_label']);
				if (!empty($ftaCache['total_reported'])) {
					echo ' — FTA reports ' . (int)$ftaCache['total_reported'] . ' items, parsed ' . count($legislation);
				}
				if ($newCountTotal > 0) {
					echo ' — <span class="text-success">' . (int)$newCountTotal . ' new</span>';
				}
				if ($changedCountTotal > 0) {
					echo ' — <span class="text-warning">' . (int)$changedCountTotal . ' updated</span>';
				}
			} else {
				echo 'No cache — fetch from legislation.aspx';
			}
			?>
		</span>
	</p>

	<div class="epc-leg-qa" id="epc_leg_qa">
		<h4><i class="fa fa-comments-o"></i> Ask FTA legislation</h4>
		<p class="text-muted" style="font-size:12px;margin:0 0 10px;">Natural-language Q&amp;A from the <?php echo count($legislation); ?>-item legislation library (stored summaries — not AI hallucination).</p>
		<div class="epc-leg-qa-prompts">
			<button type="button" class="epc-leg-qa-example" data-q="What is VAT rate in UAE?">VAT rate?</button>
			<button type="button" class="epc-leg-qa-example" data-q="VAT on advance payment?">Advance payment VAT?</button>
			<button type="button" class="epc-leg-qa-example" data-q="Export zero rating conditions?">Export zero rating?</button>
			<button type="button" class="epc-leg-qa-example" data-q="Corporate tax threshold and 9% rate?">Corporate tax threshold?</button>
			<button type="button" class="epc-leg-qa-example" data-q="TRN format on tax invoice?">TRN format?</button>
			<button type="button" class="epc-leg-qa-example" data-q="Input VAT recovery and entertainment blocked?">Input VAT / entertainment?</button>
		</div>
		<div class="form-group" style="margin-bottom:8px;">
			<textarea class="form-control" id="epc_leg_qa_question" rows="2" placeholder="e.g. What is the standard VAT rate in UAE?"></textarea>
		</div>
		<button type="button" class="btn btn-primary btn-sm" id="epc_leg_qa_ask"><i class="fa fa-search"></i> Ask</button>
		<span id="epc_leg_qa_status" class="text-muted" style="margin-left:10px;font-size:12px;"></span>
		<div class="epc-leg-qa-reply" id="epc_leg_qa_reply">
			<div class="epc-leg-qa-answer" id="epc_leg_qa_answer"></div>
			<div class="epc-leg-qa-cite">
				<strong style="font-size:12px;"><i class="fa fa-gavel"></i> Cited legislation</strong>
				<ul id="epc_leg_qa_citations" style="margin:6px 0 0;padding-left:18px;"></ul>
			</div>
			<p class="text-muted" style="font-size:11px;margin:10px 0 0;" id="epc_leg_qa_meta"></p>
		</div>
		<?php if (!empty($qaHistory)): ?>
		<div class="epc-leg-qa-history">
			<strong>Recent questions</strong>
			<?php foreach (array_slice($qaHistory, 0, 5) as $hi => $hq): ?>
			<details<?php if ($hi === 0): ?> open<?php endif; ?>>
				<summary><?php echo epc_erp_h($hq['question'] ?? ''); ?></summary>
				<ul style="margin:4px 0 0;padding-left:16px;">
					<?php foreach ((array)($hq['answer'] ?? array()) as $hb): ?>
						<li><?php echo epc_erp_h($hb); ?></li>
					<?php endforeach; ?>
				</ul>
			</details>
			<?php endforeach; ?>
		</div>
		<?php endif; ?>
	</div>

	<?php if ($summariesNeedRegen && empty($newSince)): ?>
	<div class="alert alert-warning">
		<strong>Summaries incomplete</strong> — click <em>Regenerate all summaries</em> to rebuild per-item ERP text, family dashboard cards, and charts.
	</div>
	<?php endif; ?>

	<?php if (!empty($newSince)): ?>
	<div class="alert alert-success">
		<strong><?php echo count($newSince); ?> new</strong> since last fetch — summaries auto-generated:
		<ul style="margin:8px 0 0;">
			<?php foreach (array_slice($newSince, 0, 8) as $nw): ?>
				<li><?php echo epc_erp_h($nw['title'] ?? ''); ?> <small class="text-muted"><?php echo epc_erp_h($nw['issue_date'] ?? ''); ?></small></li>
			<?php endforeach; ?>
		</ul>
	</div>
	<?php endif; ?>

	<h4 style="margin:0 0 10px;"><i class="fa fa-dashboard"></i> Overall tax summary</h4>
	<div class="epc-leg-dash">
		<?php foreach ($overallSummaries as $os): ?>
		<div class="epc-leg-card" style="border-left-color:<?php echo epc_erp_h($os['color'] ?? '#2563eb'); ?>;">
			<div class="rate" style="color:<?php echo epc_erp_h($os['color'] ?? '#2563eb'); ?>;"><?php echo epc_erp_h($os['rate_label'] ?? ''); ?></div>
			<strong style="font-size:13px;"><?php echo epc_erp_h($os['title'] ?? ''); ?></strong>
			<div class="cnt"><?php echo (int)($os['item_count'] ?? 0); ?> instrument(s)<?php if (!empty($os['new_count'])): ?> · <span class="text-success"><?php echo (int)$os['new_count']; ?> new</span><?php endif; ?></div>
			<?php if (!empty($os['bullets']) && is_array($os['bullets'])): ?>
			<ul style="font-size:11px;color:#475569;margin:8px 0 0;padding-left:16px;line-height:1.45;">
				<?php foreach (array_slice($os['bullets'], 0, 5) as $ob): ?>
					<li><?php echo epc_erp_h($ob); ?></li>
				<?php endforeach; ?>
			</ul>
			<?php else: ?>
			<p style="font-size:11px;color:#475569;margin:8px 0 0;line-height:1.45;"><?php echo epc_erp_h($os['summary'] ?? ''); ?></p>
			<?php endif; ?>
			<?php if (!empty($os['recent_changes'])): ?>
			<ul style="font-size:10px;margin:6px 0 0;padding-left:14px;color:#64748b;">
				<?php foreach (array_slice($os['recent_changes'], 0, 3) as $rc): ?>
					<li><?php if (!empty($rc['is_new'])): ?><span class="label label-success" style="font-size:9px;">New</span> <?php endif; ?><?php echo epc_erp_h($rc['title'] ?? ''); ?></li>
				<?php endforeach; ?>
			</ul>
			<?php endif; ?>
		</div>
		<?php endforeach; ?>
	</div>

	<?php if (!empty($legislation)): ?>
	<div class="epc-leg-charts">
		<div class="epc-leg-chart-box">
			<h5><i class="fa fa-pie-chart"></i> Legislation by category</h5>
			<div class="epc-leg-donut-wrap">
				<?php
				$catCounts = $chartStats['category_counts'] ?? array();
				$totalCat = max(1, (int)($chartStats['total'] ?? 1));
				$cx = 50; $cy = 50; $r = 38; $ri = 24;
				$angle = -90;
				$paths = array();
				foreach ($catCounts as $ck => $cv) {
					if ((int)$cv <= 0) {
						continue;
					}
					$frac = (int)$cv / $totalCat;
					$sweep = 360 * $frac;
					if ($sweep >= 359.99) {
						$paths[] = '<circle cx="' . $cx . '" cy="' . $cy . '" r="' . $r . '" fill="' . ($legCatColors[$ck] ?? '#94a3b8') . '" />';
						$paths[] = '<circle cx="' . $cx . '" cy="' . $cy . '" r="' . $ri . '" fill="#fff" />';
						break;
					}
					$a1 = deg2rad($angle);
					$a2 = deg2rad($angle + $sweep);
					$x1 = $cx + $r * cos($a1); $y1 = $cy + $r * sin($a1);
					$x2 = $cx + $r * cos($a2); $y2 = $cy + $r * sin($a2);
					$xi1 = $cx + $ri * cos($a2); $yi1 = $cy + $ri * sin($a2);
					$xi2 = $cx + $ri * cos($a1); $yi2 = $cy + $ri * sin($a1);
					$large = $sweep > 180 ? 1 : 0;
					$col = $legCatColors[$ck] ?? '#94a3b8';
					$paths[] = '<path d="M' . round($x1,2) . ',' . round($y1,2)
						. ' A' . $r . ',' . $r . ' 0 ' . $large . ',1 ' . round($x2,2) . ',' . round($y2,2)
						. ' L' . round($xi1,2) . ',' . round($yi1,2)
						. ' A' . $ri . ',' . $ri . ' 0 ' . $large . ',0 ' . round($xi2,2) . ',' . round($yi2,2) . ' Z" fill="' . $col . '" />';
					$angle += $sweep;
				}
				?>
				<svg width="100" height="100" viewBox="0 0 100 100" aria-hidden="true"><?php echo implode('', $paths); ?></svg>
				<div class="epc-leg-legend">
					<?php foreach ($catCounts as $ck => $cv):
						if ((int)$cv <= 0) continue;
						$pct = round(100 * (int)$cv / $totalCat, 1);
					?>
					<div><span style="background:<?php echo epc_erp_h($legCatColors[$ck] ?? '#94a3b8'); ?>;"></span>
						<?php echo epc_erp_h($legCatLabels[$ck] ?? $ck); ?>: <?php echo (int)$cv; ?> (<?php echo epc_erp_h($pct); ?>%)</div>
					<?php endforeach; ?>
				</div>
			</div>
		</div>
		<div class="epc-leg-chart-box">
			<h5><i class="fa fa-calendar"></i> Timeline by issue year</h5>
			<?php $yrShown = 0; foreach ($timelineYears as $yr => $cnt):
				if ($yrShown >= 10) break;
				$yrShown++;
				$pctW = round(100 * (int)$cnt / $timelineMax, 1);
			?>
			<div class="epc-leg-timeline-row">
				<span class="yr"><?php echo epc_erp_h($yr); ?></span>
				<div class="bar-wrap"><div class="bar" style="width:<?php echo epc_erp_h($pctW); ?>%;"></div></div>
				<span><?php echo (int)$cnt; ?></span>
			</div>
			<?php endforeach; ?>
		</div>
		<div class="epc-leg-chart-box">
			<h5><i class="fa fa-sitemap"></i> ERP modules affected</h5>
			<div class="epc-leg-modules">
				<?php foreach (($chartStats['erp_module_hits'] ?? array()) as $mk => $mv):
					if ((int)$mv <= 0) continue;
				?>
				<span class="epc-leg-mod-node" style="background:<?php echo epc_erp_h($moduleColors[$mk] ?? '#64748b'); ?>;">
					<?php echo epc_erp_h($moduleLabels[$mk] ?? $mk); ?> · <?php echo (int)$mv; ?>
				</span>
				<?php endforeach; ?>
			</div>
			<p class="text-muted" style="font-size:10px;margin:10px 0 0;">Node count = legislation items referencing each ERP area.</p>
		</div>
	</div>
	<?php endif; ?>

	<div class="btn-group btn-group-sm epc-leg-filter-bar" style="margin-bottom:12px;" id="epc_leg_filter_bar" data-filter="<?php echo epc_erp_h($taxFilter); ?>">
		<a class="btn btn-default epc-leg-filter-btn<?php echo $taxFilter === '' ? ' active' : ''; ?>" href="<?php echo epc_erp_h($legFilterBase); ?>" data-filter="" data-count="<?php echo (int)$filterCounts['']; ?>">All <small>(<?php echo (int)$filterCounts['']; ?>)</small></a>
		<?php foreach (array('vat' => 'VAT', 'corporate_tax' => 'CT', 'excise' => 'Excise', 'procedures' => 'Procedures', 'einvoicing' => 'E-invoice', 'general' => 'General') as $tk => $tl): ?>
			<a class="btn btn-default epc-leg-filter-btn<?php echo $taxFilter === $tk ? ' active' : ''; ?>" href="<?php echo epc_erp_h($legFilterBase . '&tax_type=' . rawurlencode($tk)); ?>" data-filter="<?php echo epc_erp_h($tk); ?>" data-count="<?php echo (int)($filterCounts[$tk] ?? 0); ?>"><?php echo epc_erp_h($tl); ?> <small>(<?php echo (int)($filterCounts[$tk] ?? 0); ?>)</small></a>
		<?php endforeach; ?>
	</div>

	<?php if (empty($legislation)): ?>
		<p class="text-muted">No legislation in cache. Click <strong>Fetch legislation updates</strong> (source: legislation.aspx only).</p>
	<?php else: ?>
		<p class="text-muted" style="margin-bottom:10px;" id="epc_leg_filter_count"><small><span id="epc_leg_filter_shown"><?php echo count($filteredLeg); ?></span> of <?php echo count($allLegItems); ?> items — click a row to expand summary &amp; compliance checklist.</small></p>
		<p class="text-muted" id="epc_leg_filter_empty" style="<?php echo empty($filteredLeg) ? '' : 'display:none;'; ?>">No items match this filter. <a href="<?php echo epc_erp_h($legFilterBase); ?>" class="epc-leg-filter-btn" data-filter="">Show all</a></p>
		<div id="epc_leg_list">
		<?php foreach ($allLegItems as $idx => $leg):
			$tt = strtolower(trim((string)($leg['tax_type'] ?? $leg['tax_category'] ?? 'general')));
			if ($tt === '') {
				$tt = 'general';
			}
			$hiddenByFilter = ($taxFilter !== '' && $tt !== $taxFilter);
			$summary = trim((string)($leg['erp_summary'] ?? $leg['summary'] ?? ''));
			if ($summary === '') {
				$built = epc_uae_tax_legislation_build_summary($leg);
				$summary = (string)($built['erp_summary'] ?? 'UAE FTA legislation — review ERP tax settings with your advisor.');
			}
			$checkItems = $leg['checklist_items'] ?? array();
			if (!is_array($checkItems) || empty($checkItems)) {
				$actions = $leg['compliance_actions'] ?? epc_uae_tax_legislation_build_compliance_actions($leg);
				$chk = epc_uae_tax_legislation_checklist_with_status($db_link, (string)($leg['item_key'] ?? ''), (array)$actions);
				$checkItems = $chk['items'];
				$leg['impl_status'] = $chk['impl_status'];
				$leg['impl_pending'] = $chk['pending'];
				$leg['impl_done'] = $chk['done'];
			}
			$implStatus = (string)($leg['impl_status'] ?? 'pending');
			$implLabel = $implStatus === 'implemented' ? 'Implemented' : ($implStatus === 'in_progress' ? 'In progress' : 'Implementation pending');
			$implColor = $implStatus === 'implemented' ? '#1a7f37' : ($implStatus === 'in_progress' ? '#b8860b' : '#c0392b');
			$itemClass = !empty($leg['is_new']) ? 'is-new' : '';
			$itemKey = (string)($leg['item_key'] ?? '');
		?>
		<div class="epc-leg-item <?php echo epc_erp_h($itemClass); ?>" data-leg-idx="<?php echo (int)$idx; ?>" data-item-key="<?php echo epc_erp_h($itemKey); ?>" data-tax-type="<?php echo epc_erp_h($tt); ?>"<?php echo $hiddenByFilter ? ' style="display:none;"' : ''; ?>>
			<div class="epc-leg-item-hd" onclick="this.parentElement.classList.toggle('open');">
				<i class="fa fa-chevron-right epc-leg-chevron"></i>
				<div style="flex:1;min-width:200px;">
					<?php if (!empty($leg['is_new'])): ?><span class="label label-success">New</span> <?php endif; ?>
					<?php if (!empty($leg['is_updated']) || !empty($leg['is_changed'])): ?><span class="label label-warning">Updated</span> <?php endif; ?>
					<span class="label" style="background:<?php echo epc_erp_h($implColor); ?>;"><?php echo epc_erp_h($implLabel); ?></span>
					<strong><?php echo epc_erp_h($leg['title'] ?? ''); ?></strong>
					<br><small class="text-muted"><?php echo epc_erp_h($leg['category'] ?? ''); ?></small>
				</div>
				<div style="font-size:11px;color:#64748b;min-width:90px;"><?php echo epc_erp_h($leg['issue_date'] ?? ''); ?></div>
				<div style="min-width:70px;"><span class="label label-default" style="background:<?php echo epc_erp_h($legCatColors[$tt] ?? '#94a3b8'); ?>;"><?php echo epc_erp_h($legCatLabels[$tt] ?? ucfirst(str_replace('_', ' ', $tt))); ?></span></div>
				<div style="min-width:50px;"><?php if (!empty($leg['pdf_url'])): ?><a href="<?php echo epc_erp_h($leg['pdf_url']); ?>" target="_blank" rel="noopener" onclick="event.stopPropagation();"><i class="fa fa-file-pdf-o"></i> PDF</a><?php endif; ?></div>
			</div>
			<div class="epc-leg-item-bd">
				<p class="epc-leg-summary"><strong>Summary</strong> — <?php echo epc_erp_h($summary); ?></p>
				<strong style="font-size:12px;"><i class="fa fa-check-square-o"></i> ERP compliance checklist</strong>
				<span class="text-muted" style="font-size:11px;margin-left:6px;"><?php echo (int)($leg['impl_done'] ?? 0); ?>/<?php echo count($checkItems); ?> done</span>
				<ul class="epc-leg-checklist">
					<?php foreach ($checkItems as $ci):
						$done = (($ci['status'] ?? '') === 'done');
						?>
						<li class="epc-leg-check-row<?php echo $done ? ' is-done' : ''; ?>">
							<label style="display:flex;gap:8px;align-items:flex-start;font-weight:400;cursor:pointer;margin:0;">
								<input type="checkbox" class="epc-leg-check" data-item-key="<?php echo epc_erp_h($itemKey); ?>" data-action-key="<?php echo epc_erp_h((string)($ci['key'] ?? '')); ?>" data-action-text="<?php echo epc_erp_h((string)($ci['text'] ?? '')); ?>" <?php echo $done ? 'checked' : ''; ?> onclick="event.stopPropagation();">
								<span><?php echo epc_erp_h((string)($ci['text'] ?? '')); ?></span>
							</label>
						</li>
					<?php endforeach; ?>
				</ul>
				<?php if (!empty($leg['erp_modules'])): ?>
				<div style="margin-top:8px;">
					<?php foreach ($leg['erp_modules'] as $mod): ?>
						<span class="label" style="background:<?php echo epc_erp_h($moduleColors[$mod] ?? '#64748b'); ?>;margin-right:4px;"><?php echo epc_erp_h($moduleLabels[$mod] ?? $mod); ?></span>
					<?php endforeach; ?>
				</div>
				<?php endif; ?>
			</div>
		</div>
		<?php endforeach; ?>
		</div>
	<?php endif; ?>

<?php elseif ($panel === 'operations'): ?>

	<div class="epc-erp-kpi" style="margin:0 0 16px;">
		<div class="kpi"><div class="lbl">Advance VAT</div><div class="val"><?php echo epc_erp_money($advancePeriod['output_vat_on_advances']); ?></div></div>
		<div class="kpi"><div class="lbl">Credited on invoices</div><div class="val green"><?php echo epc_erp_money($advancePeriod['advance_vat_credited_on_invoices']); ?></div></div>
		<div class="kpi"><div class="lbl">Recoverable input VAT</div><div class="val green"><?php echo epc_erp_money($expInput['totals']['recoverable_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">Blocked input VAT</div><div class="val"><?php echo epc_erp_money($expInput['totals']['blocked_vat']); ?></div></div>
		<div class="kpi"><div class="lbl">CT provision</div><div class="val red"><?php echo epc_erp_money($ct['corporate_tax_provision']); ?></div></div>
	</div>

	<?php foreach ($sections as $sec): ?>
		<h4><i class="fa fa-check-square-o"></i> <?php echo epc_erp_h($sec['title']); ?></h4>
		<ol><?php foreach ($sec['points'] as $pt): ?><li><?php echo epc_erp_h($pt); ?></li><?php endforeach; ?></ol>
	<?php endforeach; ?>

	<h4><i class="fa fa-file-text-o"></i> Tax invoice mandatory fields (PINT-AE / FTA)</h4>
	<?php foreach ($invoiceChecklist as $grp): ?>
		<p><strong><?php echo epc_erp_h($grp['group']); ?></strong></p>
		<ul class="list-unstyled" style="columns:2;margin-bottom:12px;">
			<?php foreach ($grp['fields'] as $f): ?>
				<li style="margin-bottom:4px;"><code><?php echo epc_erp_h($f['key']); ?></code> — <?php echo epc_erp_h($f['label']); ?></li>
			<?php endforeach; ?>
		</ul>
	<?php endforeach; ?>

	<h4><i class="fa fa-credit-card"></i> Input VAT by expense type (period)</h4>
	<table class="table table-bordered table-condensed">
		<thead><tr><th>Expense type</th><th>Recovery</th><th class="text-right">VAT</th><th class="text-right">Recoverable</th><th class="text-right">Blocked</th><th class="text-right">Count</th></tr></thead>
		<tbody>
		<?php foreach ($expInput['lines'] as $ln): ?>
			<tr>
				<td><?php echo epc_erp_h($ln['label']); ?></td>
				<td><?php echo epc_erp_h(number_format($ln['recoverable_percent'], 0)); ?>%</td>
				<td class="text-right"><?php echo epc_erp_money($ln['vat_amount']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($ln['recoverable_vat']); ?></td>
				<td class="text-right"><?php echo epc_erp_money($ln['blocked_vat']); ?></td>
				<td class="text-right"><?php echo (int)$ln['transaction_count']; ?></td>
			</tr>
		<?php endforeach; ?>
			<tr class="active">
				<td colspan="2"><strong>Total</strong></td>
				<td class="text-right"><strong><?php echo epc_erp_money($expInput['totals']['vat_amount']); ?></strong></td>
				<td class="text-right"><strong><?php echo epc_erp_money($expInput['totals']['recoverable_vat']); ?></strong></td>
				<td class="text-right"><strong><?php echo epc_erp_money($expInput['totals']['blocked_vat']); ?></strong></td>
				<td class="text-right"><?php echo (int)$expInput['totals']['transaction_count']; ?></td>
			</tr>
		</tbody>
	</table>

	<h4><i class="fa fa-sliders"></i> Corporate Tax adjustments (P&amp;L period)</h4>
	<form id="epc_ct_adjustments_form" class="form-horizontal">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
		<input type="hidden" name="date_from" value="<?php echo epc_erp_h($date_from_str); ?>">
		<input type="hidden" name="date_to" value="<?php echo epc_erp_h($date_to_str); ?>">
		<table class="table table-bordered table-condensed">
			<thead><tr><th>Adjustment</th><th>Direction</th><th class="text-right">Amount (AED)</th></tr></thead>
			<tbody>
			<?php foreach ($ctAdj['fields'] as $key => $field): ?>
				<tr>
					<td><?php echo epc_erp_h($field['label']); ?><br><small class="text-muted"><?php echo epc_erp_h($field['hint'] ?? ''); ?></small></td>
					<td><?php echo $field['direction'] === 'add' ? 'Add-back' : 'Deduct'; ?></td>
					<td class="text-right"><input type="number" step="0.01" min="0" class="form-control input-sm text-right" name="ct_<?php echo epc_erp_h($key); ?>" value="<?php echo epc_erp_h(number_format($field['amount'], 2, '.', '')); ?>" style="max-width:120px;margin-left:auto;"></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save CT adjustments</button>
	</form>
	<table class="table table-condensed" style="margin-top:12px;max-width:520px;">
		<tr><td>Accounting profit (GL)</td><td class="text-right"><?php echo epc_erp_money($ct['accounting_profit']); ?></td></tr>
		<tr><td>Add-backs</td><td class="text-right">+ <?php echo epc_erp_money($ct['ct_add_backs_total']); ?></td></tr>
		<tr><td>Deductions</td><td class="text-right">− <?php echo epc_erp_money($ct['ct_deductions_total']); ?></td></tr>
		<tr><td><strong>Adjusted taxable profit</strong></td><td class="text-right"><strong><?php echo epc_erp_money($ct['adjusted_taxable_profit']); ?></strong></td></tr>
		<tr><td>CT <?php echo epc_erp_h(number_format($ct['rate_percent'], 2)); ?>% provision</td><td class="text-right" style="color:#b91c1c;"><?php echo epc_erp_money($ct['corporate_tax_provision']); ?></td></tr>
	</table>

	<h4><i class="fa fa-link"></i> ERP shortcuts</h4>
	<ul>
		<li><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'vat_return', $date_from_str, $date_to_str, 'finance')); ?>">UAE VAT return</a></li>
		<li><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'pl', $date_from_str, $date_to_str, 'insights')); ?>">P&amp;L with Corporate Tax</a></li>
		<li><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str, 'finance')); ?>">E-Invoicing</a></li>
	</ul>

<?php else: /* knowledge / process guides */ ?>

	<?php
	$kbTypes = epc_uae_tax_knowledge_type_labels();
	foreach ($kbTypes as $typeKey => $typeLabel):
		$articles = epc_uae_tax_knowledge_by_type($typeKey);
		if (empty($articles)) {
			continue;
		}
	?>
	<h4><i class="fa fa-folder-open"></i> <?php echo epc_erp_h($typeLabel); ?></h4>
	<div class="panel-group">
		<?php foreach ($articles as $key => $art): ?>
		<div class="panel panel-default">
			<div class="panel-heading"><h4 class="panel-title"><a data-toggle="collapse" href="#epc_kb_<?php echo epc_erp_h($typeKey . '_' . $key); ?>"><?php echo epc_erp_h($art['title']); ?></a></h4></div>
			<div id="epc_kb_<?php echo epc_erp_h($typeKey . '_' . $key); ?>" class="panel-collapse collapse">
				<div class="panel-body"><?php echo epc_uae_tax_knowledge_render_article($art); ?></div>
			</div>
		</div>
		<?php endforeach; ?>
	</div>
	<?php endforeach; ?>
	<p class="text-muted"><small>Synced to <a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'knowledge_base', $date_from_str, $date_to_str, 'insights') . '&kb_cat=uae_tax'); ?>">Knowledge base → UAE tax</a> including legislation articles after fetch.</small></p>

<?php endif; ?>

	<p class="text-muted"><small><?php echo epc_erp_h($ct['note']); ?></small></p>
</div>
<?php
// Fallback when the host shell does not flush epc_cp_page_assets (standalone /erp).
// Guarded by __epcUaeTaxComplianceBound inside the script so CP double-load is safe.
if (!empty($tcJsSrc)):
?>
<script src="<?php echo epc_erp_h($tcJsSrc); ?>"></script>
<?php endif; ?>
