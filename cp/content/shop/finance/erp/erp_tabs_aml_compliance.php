<?php
/**
 * AML Compliance — graphical command centre (KYC, monitoring, legislation, guide, reports).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_aml_compliance.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';

epc_aml_ensure_schema($db_link);
epc_aml_seed_rules($db_link);

$amlSection = isset($_GET['aml_section']) ? preg_replace('/[^a-z_]/', '', (string) $_GET['aml_section']) : 'dashboard';
$allowedSections = array('dashboard', 'monitoring', 'kyc', 'reports', 'legislation', 'guide', 'settings');
if (!in_array($amlSection, $allowedSections, true)) {
	$amlSection = 'dashboard';
}
$viewReportId = isset($_GET['aml_report']) ? (int) $_GET['aml_report'] : 0;

$amlCompanyId = function_exists('epc_erp_active_company_id') ? (int) epc_erp_active_company_id($db_link) : 0;
$amlDash = epc_aml_dashboard($db_link, $amlCompanyId, (int) $date_from, (int) $date_to);
$amlReady = epc_aml_module_completeness($db_link);
$amlJourney = epc_aml_journey_steps($db_link);
$amlLaws = epc_aml_legislation_items($db_link);
$amlDnfbp = epc_aml_dnfbp_context($db_link);
$amlAlerts = epc_aml_list_alerts($db_link, $amlCompanyId, 40);
$amlKyc = epc_aml_list_kyc($db_link, $amlCompanyId, 80);
$amlReports = epc_aml_list_reports($db_link, $amlCompanyId, 25);
$viewReport = $viewReportId > 0 ? epc_aml_get_report($db_link, $viewReportId) : null;

$ftaCache = array();
$ftaFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_compliance.php';
if (is_file($ftaFile)) {
	require_once $ftaFile;
}
if (function_exists('epc_uae_fta_get_cached_legislation')) {
	$ftaCache = epc_uae_fta_get_cached_legislation($db_link);
	if (!is_array($ftaCache)) {
		$ftaCache = array();
	}
}

$amlBase = epc_erp_tab_url($erpUrl, 'aml_compliance', $date_from_str, $date_to_str, $erpArea ?? 'tax');
$extAmlUrl = epc_erp_tab_url($erpUrl, 'ext_reports', $date_from_str, $date_to_str, 'regrep') . '&cat=aml';
$extSarUrl = $extAmlUrl . '&rep=aml__suspicious_activity_report_sar';
$extStrUrl = $extAmlUrl . '&rep=aml__suspicious_transaction_report_str';
$goamlUrl = 'https://services.uaefiu.gov.ae';
$fiuUrl = 'https://www.uaefiu.gov.ae';

function epc_aml_url($base, $section, $extra = '')
{
	$u = $base . '&aml_section=' . rawurlencode($section);
	return $extra !== '' ? ($u . '&' . $extra) : $u;
}

$activeStepKey = 'learn';
foreach ($amlJourney as $js) {
	if ($amlSection === $js['section']) {
		$activeStepKey = $js['key'];
		break;
	}
}
if ($amlSection === 'dashboard') {
	foreach ($amlJourney as $js) {
		if (empty($js['done'])) {
			$activeStepKey = $js['key'];
			break;
		}
		$activeStepKey = $js['key'];
	}
}

$csrfLocal = isset($csrf) ? $csrf : '';
$amlAjax = isset($erpAjaxUrl) ? (string) $erpAjaxUrl : (isset($erpAjaxEndpoint) ? (string) $erpAjaxEndpoint : '/erp/ajax');
?>

<style>
.epc-aml-hero{position:relative;overflow:hidden;border-radius:14px;padding:22px 24px 18px;margin:0 0 18px;background:linear-gradient(125deg,#0c1222 0%,#1e293b 45%,#0f766e 100%);color:#fff;box-shadow:0 10px 28px rgba(0,0,0,.14);}
.epc-aml-hero::after{content:"";position:absolute;right:-36px;top:-48px;width:210px;height:210px;border-radius:50%;background:radial-gradient(circle,rgba(45,212,191,.32),transparent 68%);pointer-events:none;}
.epc-aml-hero>*{position:relative;z-index:1;}
.epc-aml-hero h2{margin:0 0 6px;font-size:22px;font-weight:800;color:#fff!important;letter-spacing:.01em;}
.epc-aml-hero p{margin:0;max-width:760px;font-size:13px;line-height:1.5;color:rgba(255,255,255,.9);}
.epc-aml-chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:14px;}
.epc-aml-chip{display:inline-flex;align-items:center;gap:6px;padding:5px 10px;border-radius:999px;background:rgba(255,255,255,.1);border:1px solid rgba(255,255,255,.18);font-size:11px;font-weight:700;letter-spacing:.03em;text-transform:uppercase;}
.epc-aml-actions{display:flex;flex-wrap:wrap;align-items:center;gap:10px;margin:0 0 18px;}
.epc-aml-actions .btn-fetch{background:#0f766e;border-color:#0f766e;color:#fff;font-weight:700;}
.epc-aml-actions .btn-fetch:hover{background:#115e59;border-color:#115e59;color:#fff;}
.epc-aml-actions .btn-fetch.is-busy{opacity:.7;pointer-events:none;}
.epc-aml-steps{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:10px;margin:0 0 20px;}
@media(max-width:1100px){.epc-aml-steps{grid-template-columns:repeat(3,minmax(0,1fr));}}
@media(max-width:640px){.epc-aml-steps{grid-template-columns:1fr 1fr;}}
.epc-aml-step{display:flex;flex-direction:column;gap:8px;padding:14px 12px 12px;border-radius:12px;background:#fff;border:1px solid #e5e5e5;text-decoration:none!important;color:#0a0a0a!important;min-height:128px;box-shadow:0 4px 14px rgba(0,0,0,.04);transition:transform .14s ease,box-shadow .14s ease,border-color .14s ease;position:relative;}
.epc-aml-step:hover{transform:translateY(-2px);box-shadow:0 10px 22px rgba(0,0,0,.08);border-color:#5eead4;color:#0a0a0a!important;}
.epc-aml-step.is-done{border-color:#86efac;background:linear-gradient(180deg,#f0fdf4,#fff);}
.epc-aml-step.is-current{border-color:#0f766e;box-shadow:0 0 0 2px rgba(15,118,110,.18),0 10px 22px rgba(0,0,0,.08);}
.epc-aml-step__n{width:28px;height:28px;border-radius:8px;display:inline-flex;align-items:center;justify-content:center;font-size:12px;font-weight:800;background:#0f172a;color:#fff;}
.epc-aml-step.is-done .epc-aml-step__n{background:#16a34a;}
.epc-aml-step.is-current .epc-aml-step__n{background:#0f766e;}
.epc-aml-step__ico{position:absolute;top:12px;right:12px;font-size:18px;color:#a3a3a3;}
.epc-aml-step.is-done .epc-aml-step__ico,.epc-aml-step.is-current .epc-aml-step__ico{color:#0f766e;}
.epc-aml-step__t{font-size:13px;font-weight:800;line-height:1.25;margin-top:2px;}
.epc-aml-step__b{font-size:11.5px;color:#737373;line-height:1.35;flex:1;}
.epc-aml-step__cta{font-size:11px;font-weight:700;color:#0f766e;}
.epc-aml-flow{display:flex;flex-wrap:wrap;align-items:center;justify-content:center;gap:6px 4px;margin:0 0 20px;padding:16px;border-radius:12px;background:#f8fafc;border:1px solid #e2e8f0;}
.epc-aml-flow__node{display:inline-flex;align-items:center;gap:7px;padding:8px 12px;border-radius:999px;background:#fff;border:1px solid #e2e8f0;font-size:12px;font-weight:700;color:#0f172a;}
.epc-aml-flow__node .fa{color:#0f766e;}
.epc-aml-flow__arrow{color:#94a3b8;font-size:12px;padding:0 2px;}
.epc-aml-kpi-strip{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px;margin:0 0 18px;}
.epc-aml-kpi{padding:14px;border-radius:12px;background:#fff;border:1px solid #e5e5e5;}
.epc-aml-kpi .lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#737373;}
.epc-aml-kpi .val{font-size:22px;font-weight:800;color:#0a0a0a;margin-top:4px;line-height:1.1;}
.epc-aml-kpi .val.ok{color:#16a34a;}
.epc-aml-kpi .val.warn{color:#d97706;}
.epc-aml-kpi .val.bad{color:#dc2626;}
.epc-aml-panel{background:#fff;border:1px solid #e5e5e5;border-radius:12px;padding:16px 18px;margin:0 0 16px;box-shadow:0 4px 14px rgba(0,0,0,.03);}
.epc-aml-panel h4{margin:0 0 10px;font-size:15px;font-weight:800;}
.epc-aml-nav{display:flex;flex-wrap:wrap;gap:6px;margin:0 0 18px;padding:0;list-style:none;}
.epc-aml-nav>li>a{display:inline-flex;align-items:center;gap:6px;padding:7px 12px;border-radius:8px;border:1px solid #e5e5e5;background:#fff;color:#404040!important;font-size:12px;font-weight:700;text-decoration:none!important;}
.epc-aml-nav>li.active>a,.epc-aml-nav>li>a:hover{background:#0f172a;border-color:#0f172a;color:#fff!important;}
.epc-aml-leg__list{list-style:none;margin:12px 0 0;padding:0;display:grid;gap:8px;}
.epc-aml-leg__list li{display:flex;gap:10px;align-items:flex-start;padding:10px 12px;border-radius:10px;background:#f8fafc;border:1px solid #f1f5f9;}
.epc-aml-badge{display:inline-block;font-size:10px;font-weight:800;padding:2px 7px;border-radius:999px;text-transform:uppercase;color:#fff;background:#64748b;}
.epc-aml-badge.new,.epc-aml-badge.core{background:#0f766e;}
.epc-aml-badge.upd,.epc-aml-badge.update{background:#b45309;}
.epc-aml-badge.portal{background:#1d4ed8;}
.epc-aml-badge.intl{background:#7c3aed;}
.epc-aml-checkgrid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:8px;margin:0;}
.epc-aml-checkgrid li{list-style:none;padding:10px 12px;border-radius:10px;border:1px solid #e5e5e5;background:#fafafa;font-size:12.5px;}
.epc-aml-checkgrid li.ok{border-color:#86efac;background:#f0fdf4;}
.epc-aml-obl{display:grid;gap:8px;}
.epc-aml-obl article{padding:12px 14px;border-radius:10px;border:1px solid #e2e8f0;background:linear-gradient(180deg,#fff,#f8fafc);}
.epc-aml-obl article h5{margin:0 0 4px;font-size:13px;font-weight:800;}
.epc-aml-obl article p{margin:0;font-size:12px;color:#64748b;}
.epc-aml-fields{display:flex;flex-wrap:wrap;gap:12px;align-items:flex-end;}
.epc-aml-fields .f{min-width:140px;flex:1;}
.epc-aml-fields label{display:block;font-size:11px;font-weight:700;color:#64748b;margin-bottom:4px;}
</style>

<div class="epc-erp-section epc-aml-panel-root">
	<div class="epc-aml-hero">
		<h2><i class="fa fa-shield"></i> AML / CFT Compliance</h2>
		<p>Know-your-customer, transaction monitoring, and goAML reporting for DNFBPs — aligned with UAE Federal Decree-Law 20/2018 and FATF standards. One programme for the whole company team.</p>
		<div class="epc-aml-chips">
			<span class="epc-aml-chip"><i class="fa fa-balance-scale"></i> Decree-Law 20/2018</span>
			<span class="epc-aml-chip"><i class="fa fa-university"></i> UAE FIU · goAML</span>
			<span class="epc-aml-chip"><i class="fa fa-diamond"></i> <?php echo !empty($amlDnfbp['is_precious_metals']) ? 'DPMS / jewellery' : (!empty($amlDnfbp['is_dnfbp']) ? 'DNFBP' : 'Enterprise AML'); ?></span>
			<span class="epc-aml-chip"><i class="fa fa-money"></i> Cash ≥ <?php echo epc_erp_h(number_format((float) $amlDash['cash_threshold'], 0)); ?> AED</span>
			<span class="epc-aml-chip"><i class="fa fa-check-circle"></i> Readiness <?php echo (int) $amlReady['percent']; ?>%</span>
		</div>
	</div>

	<div class="epc-aml-actions">
		<button type="button" class="btn btn-sm btn-fetch" id="epc_aml_fetch_legislation" title="Pull latest legislation and refresh AML-related items">
			<i class="fa fa-refresh"></i> Fetch new AML / tax legislation
		</button>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($fiuUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> UAE FIU</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($goamlUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-cloud-upload"></i> goAML portal</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($extAmlUrl); ?>"><i class="fa fa-file-text-o"></i> External AML reports</a>
		<span id="epc_aml_leg_status" class="text-muted" style="font-size:12px;">
			<?php
			if (!empty($ftaCache['time_fetched_label'])) {
				echo 'Last legislation sync: ' . epc_erp_h((string) $ftaCache['time_fetched_label']);
				echo ' · ' . count($amlLaws) . ' AML-related item(s)';
			} else {
				echo 'Curated AML laws loaded — click Fetch to sync FTA cache for related updates';
			}
			?>
		</span>
	</div>

	<div class="epc-aml-flow" aria-label="AML control flow">
		<span class="epc-aml-flow__node"><i class="fa fa-user"></i> Customer / KYC</span>
		<span class="epc-aml-flow__arrow"><i class="fa fa-long-arrow-right"></i></span>
		<span class="epc-aml-flow__node"><i class="fa fa-search"></i> Monitor</span>
		<span class="epc-aml-flow__arrow"><i class="fa fa-long-arrow-right"></i></span>
		<span class="epc-aml-flow__node"><i class="fa fa-exclamation-triangle"></i> Alert / MLRO</span>
		<span class="epc-aml-flow__arrow"><i class="fa fa-long-arrow-right"></i></span>
		<span class="epc-aml-flow__node"><i class="fa fa-file-text"></i> STR / SAR</span>
		<span class="epc-aml-flow__arrow">→</span>
		<span class="epc-aml-flow__node"><i class="fa fa-university"></i> goAML FIU</span>
	</div>

	<div class="epc-aml-steps" role="navigation" aria-label="AML programme steps">
		<?php foreach ($amlJourney as $step):
			$isCurrent = ($activeStepKey === $step['key']) || ($amlSection === $step['section'] && $amlSection !== 'dashboard');
			$cls = 'epc-aml-step';
			if (!empty($step['done'])) {
				$cls .= ' is-done';
			}
			if ($isCurrent) {
				$cls .= ' is-current';
			}
			?>
		<a class="<?php echo epc_erp_h($cls); ?>" href="<?php echo epc_erp_h(epc_aml_url($amlBase, $step['section'])); ?>">
			<span class="epc-aml-step__n"><?php echo !empty($step['done']) ? '<i class="fa fa-check"></i>' : (int) $step['n']; ?></span>
			<span class="epc-aml-step__ico"><i class="fa <?php echo epc_erp_h($step['icon']); ?>"></i></span>
			<span class="epc-aml-step__t">Step <?php echo (int) $step['n']; ?> · <?php echo epc_erp_h($step['title']); ?></span>
			<span class="epc-aml-step__b"><?php echo epc_erp_h($step['blurb']); ?></span>
			<span class="epc-aml-step__cta"><?php echo epc_erp_h($step['cta']); ?> <i class="fa fa-angle-right"></i></span>
		</a>
		<?php endforeach; ?>
	</div>

	<ul class="epc-aml-nav">
		<li class="<?php echo $amlSection === 'dashboard' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'dashboard')); ?>"><i class="fa fa-th-large"></i> Overview</a></li>
		<li class="<?php echo $amlSection === 'monitoring' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'monitoring')); ?>"><i class="fa fa-eye"></i> Monitoring</a></li>
		<li class="<?php echo $amlSection === 'kyc' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'kyc')); ?>"><i class="fa fa-user"></i> KYC</a></li>
		<li class="<?php echo $amlSection === 'reports' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'reports')); ?>"><i class="fa fa-file-text-o"></i> Reports</a></li>
		<li class="<?php echo $amlSection === 'legislation' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'legislation')); ?>"><i class="fa fa-gavel"></i> Legislation</a></li>
		<li class="<?php echo $amlSection === 'guide' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'guide')); ?>"><i class="fa fa-book"></i> Guide</a></li>
		<li class="<?php echo $amlSection === 'settings' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'settings')); ?>"><i class="fa fa-cog"></i> Settings</a></li>
	</ul>

	<?php if ($amlSection === 'dashboard'): ?>
		<div class="epc-aml-kpi-strip">
			<div class="epc-aml-kpi"><div class="lbl">Programme readiness</div><div class="val"><?php echo (int) $amlReady['percent']; ?>%</div></div>
			<div class="epc-aml-kpi"><div class="lbl">KYC verified</div><div class="val <?php echo (int) $amlDash['kyc_pct'] >= 80 ? 'ok' : 'warn'; ?>"><?php echo (int) $amlDash['kyc_pct']; ?>%</div></div>
			<div class="epc-aml-kpi"><div class="lbl">High-risk customers</div><div class="val <?php echo (int) $amlDash['high_risk'] > 0 ? 'bad' : 'ok'; ?>"><?php echo (int) $amlDash['high_risk']; ?></div></div>
			<div class="epc-aml-kpi"><div class="lbl">Open alerts</div><div class="val <?php echo (int) $amlDash['open_alerts'] > 0 ? 'warn' : 'ok'; ?>"><?php echo (int) $amlDash['open_alerts']; ?></div></div>
			<div class="epc-aml-kpi"><div class="lbl">Checks (period)</div><div class="val"><?php echo (int) $amlDash['tx_total']; ?></div></div>
			<div class="epc-aml-kpi"><div class="lbl">STR filed (period)</div><div class="val"><?php echo (int) $amlDash['sar_filed']; ?></div></div>
			<div class="epc-aml-kpi"><div class="lbl">PEP flagged</div><div class="val"><?php echo (int) $amlDash['pep_count']; ?></div></div>
			<div class="epc-aml-kpi"><div class="lbl">Active rules</div><div class="val ok"><?php echo (int) $amlDash['rules_active']; ?></div></div>
		</div>

		<div class="row">
			<div class="col-md-6">
				<div class="epc-aml-panel">
					<h4><i class="fa fa-check-square-o"></i> Completeness</h4>
					<ul class="epc-aml-checkgrid" style="padding:0;margin:0;">
						<?php foreach ($amlReady['items'] as $it): ?>
						<li class="<?php echo !empty($it['done']) ? 'ok' : ''; ?>">
							<i class="fa <?php echo !empty($it['done']) ? 'fa-check-circle text-success' : 'fa-circle-o text-muted'; ?>"></i>
							<?php echo epc_erp_h($it['label']); ?>
						</li>
						<?php endforeach; ?>
					</ul>
				</div>
			</div>
			<div class="col-md-6">
				<div class="epc-aml-panel">
					<h4><i class="fa fa-list-alt"></i> DNFBP obligations</h4>
					<?php if (empty($amlDnfbp['obligations'])): ?>
						<p class="text-muted">Generic AML controls apply. Set industry profile to jewellery / precious metals to unlock DPMS cash reporting.</p>
					<?php else: ?>
						<div class="epc-aml-obl">
							<?php foreach (array_slice($amlDnfbp['obligations'], 0, 4) as $ob): ?>
							<article>
								<h5><?php echo epc_erp_h((string) $ob['title']); ?></h5>
								<p><?php echo epc_erp_h((string) ($ob['authority'] ?? '')); ?> · <?php echo epc_erp_h((string) ($ob['frequency'] ?? '')); ?></p>
							</article>
							<?php endforeach; ?>
						</div>
					<?php endif; ?>
				</div>
			</div>
		</div>

		<div class="epc-aml-panel">
			<h4><i class="fa fa-gavel"></i> AML legislation snapshot</h4>
			<ul class="epc-aml-leg__list">
				<?php foreach (array_slice($amlLaws, 0, 5) as $law): ?>
				<li>
					<span class="epc-aml-badge <?php echo epc_erp_h((string) ($law['badge'] ?? 'core')); ?>"><?php echo epc_erp_h((string) ($law['badge'] ?? 'law')); ?></span>
					<div>
						<?php if (!empty($law['href'])): ?>
							<a href="<?php echo epc_erp_h((string) $law['href']); ?>" target="_blank" rel="noopener"><?php echo epc_erp_h((string) $law['title']); ?></a>
						<?php else: ?>
							<strong><?php echo epc_erp_h((string) $law['title']); ?></strong>
						<?php endif; ?>
						<div class="text-muted" style="font-size:12px;margin-top:2px;"><?php echo epc_erp_h((string) ($law['summary'] ?? '')); ?></div>
					</div>
				</li>
				<?php endforeach; ?>
			</ul>
			<p style="margin:12px 0 0;"><a href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'legislation')); ?>">View full legislation library →</a></p>
		</div>

	<?php elseif ($amlSection === 'monitoring'): ?>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-search"></i> Real-time transaction check</h4>
			<p class="text-muted">Runs against active rules, persists to the monitoring log, and opens an alert when flagged.</p>
			<form id="aml_check_form" class="epc-aml-fields">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="action" value="aml_check">
				<div class="f"><label>Customer ID</label><input type="number" name="customer_id" class="form-control input-sm" placeholder="0"></div>
				<div class="f" style="flex:1.4;"><label>Customer name</label><input type="text" name="customer_name" class="form-control input-sm" placeholder="Optional"></div>
				<div class="f"><label>Amount</label><input type="number" step="any" name="amount" class="form-control input-sm" required placeholder="0.00"></div>
				<div class="f"><label>Currency</label>
					<select name="currency" class="form-control input-sm"><option>AED</option><option>USD</option><option>EUR</option><option>GBP</option></select>
				</div>
				<div class="f"><label>Type</label>
					<select name="transaction_type" class="form-control input-sm">
						<option value="cash_sale">Cash sale</option>
						<option value="wire_transfer">Wire</option>
						<option value="card_payment">Card</option>
						<option value="crypto">Crypto</option>
					</select>
				</div>
				<div class="f"><label>&nbsp;</label><button type="submit" class="btn btn-warning btn-sm"><i class="fa fa-search"></i> Run check</button></div>
			</form>
			<div id="aml_check_result" style="display:none;margin-top:12px;"></div>
		</div>

		<div class="epc-aml-panel">
			<h4><i class="fa fa-exclamation-triangle"></i> Alerts &amp; monitoring log</h4>
			<?php if (!$amlAlerts): ?>
				<p class="text-muted">No flagged transactions yet. Run a check above or wait for live sales to hit a rule.</p>
			<?php else: ?>
			<table class="table table-bordered table-condensed" style="font-size:13px;">
				<thead><tr><th>When</th><th>Customer</th><th>Amount</th><th>Score</th><th>Flags</th><th>Status</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($amlAlerts as $a): ?>
					<tr>
						<td><?php echo epc_erp_h(date('Y-m-d H:i', (int) $a['time_created'])); ?></td>
						<td><?php echo epc_erp_h((string) ($a['customer_name'] !== '' ? $a['customer_name'] : ('#' . $a['customer_id']))); ?></td>
						<td><?php echo epc_erp_h(number_format((float) $a['amount'], 2) . ' ' . $a['currency']); ?></td>
						<td><span class="label label-<?php echo (int) $a['risk_score'] >= 70 ? 'danger' : 'warning'; ?>"><?php echo (int) $a['risk_score']; ?></span></td>
						<td><small><?php echo epc_erp_h((string) $a['flag_reason']); ?></small></td>
						<td><?php echo epc_erp_h((string) ($a['review_status'] ?? 'open')); ?></td>
						<td style="white-space:nowrap;">
							<button type="button" class="btn btn-xs btn-default aml-alert-btn" data-id="<?php echo (int) $a['id']; ?>" data-status="reviewed">Review</button>
							<button type="button" class="btn btn-xs btn-danger aml-alert-btn" data-id="<?php echo (int) $a['id']; ?>" data-status="escalated" data-sar="1">Escalate / STR</button>
						</td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php endif; ?>
		</div>

	<?php elseif ($amlSection === 'kyc'): ?>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-plus-circle"></i> Add / update KYC</h4>
			<form id="aml_kyc_form" class="epc-aml-fields">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="action" value="aml_kyc_save">
				<div class="f" style="flex:1.5;"><label>Customer name *</label><input type="text" name="customer_name" class="form-control input-sm" required></div>
				<div class="f"><label>Customer ID</label><input type="number" name="customer_id" class="form-control input-sm" value="0"></div>
				<div class="f"><label>ID type</label>
					<select name="id_type" class="form-control input-sm">
						<option value="emirates_id">Emirates ID</option>
						<option value="passport">Passport</option>
						<option value="trade_license">Trade licence</option>
						<option value="national_id">National ID</option>
					</select>
				</div>
				<div class="f"><label>ID number</label><input type="text" name="id_number" class="form-control input-sm"></div>
				<div class="f"><label>Risk</label>
					<select name="risk_level" class="form-control input-sm">
						<option value="low">Low</option>
						<option value="medium">Medium</option>
						<option value="high">High</option>
						<option value="very_high">Very high</option>
					</select>
				</div>
				<div class="f"><label>Status</label>
					<select name="verification_status" class="form-control input-sm">
						<option value="pending">Pending</option>
						<option value="verified">Verified</option>
						<option value="rejected">Rejected</option>
						<option value="expired">Expired</option>
					</select>
				</div>
				<div class="f"><label>Next review</label><input type="date" name="next_review" class="form-control input-sm"></div>
				<div class="f"><label><input type="checkbox" name="pep_status" value="1"> PEP</label>
					<label style="margin-top:6px;"><input type="checkbox" name="sanctions_checked" value="1" checked> Sanctions checked</label>
				</div>
				<div class="f"><label>&nbsp;</label><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save KYC</button></div>
			</form>
			<div id="aml_kyc_result" style="display:none;margin-top:10px;"></div>
		</div>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-users"></i> KYC register</h4>
			<?php if (!$amlKyc): ?>
				<p class="text-muted">No KYC records yet — add the first customer above.</p>
			<?php else: ?>
			<table class="table table-bordered table-condensed" style="font-size:13px;">
				<thead><tr><th>Customer</th><th>ID type</th><th>Status</th><th>Risk</th><th>PEP</th><th>Next review</th></tr></thead>
				<tbody>
				<?php foreach ($amlKyc as $k): ?>
					<tr>
						<td><?php echo epc_erp_h((string) $k['customer_name']); ?><?php if ((int) $k['customer_id'] > 0): ?> <small class="text-muted">#<?php echo (int) $k['customer_id']; ?></small><?php endif; ?></td>
						<td><?php echo epc_erp_h((string) $k['id_type']); ?></td>
						<td><?php echo epc_erp_h((string) $k['verification_status']); ?></td>
						<td><span class="label label-<?php echo in_array($k['risk_level'], array('high', 'very_high'), true) ? 'danger' : ($k['risk_level'] === 'medium' ? 'warning' : 'success'); ?>"><?php echo epc_erp_h((string) $k['risk_level']); ?></span></td>
						<td><?php echo !empty($k['pep_status']) ? '<span class="label label-danger">PEP</span>' : '—'; ?></td>
						<td><?php echo epc_erp_h((string) ($k['next_review'] ?? '—')); ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php endif; ?>
		</div>

	<?php elseif ($amlSection === 'reports'): ?>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-file-text-o"></i> AML report module</h4>
			<p class="text-muted">Generate period packs inside ERP, or open the official goAML SAR/STR builders under External reporting.</p>
			<div class="epc-aml-actions" style="margin-bottom:14px;">
				<a class="btn btn-danger btn-sm" href="<?php echo epc_erp_h($extSarUrl); ?>"><i class="fa fa-exclamation-triangle"></i> Build SAR (goAML)</a>
				<a class="btn btn-warning btn-sm" href="<?php echo epc_erp_h($extStrUrl); ?>"><i class="fa fa-exchange"></i> Build STR (goAML)</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($extAmlUrl); ?>"><i class="fa fa-folder-open"></i> All AML external reports</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($goamlUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> File on goAML</a>
			</div>
			<form id="aml_report_form" class="epc-aml-fields">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="action" value="aml_report_generate">
				<div class="f"><label>Report type</label>
					<select name="report_type" class="form-control input-sm">
						<option value="compliance_summary">Compliance summary</option>
						<option value="ctr">Cash / CTR pack (≥ threshold)</option>
						<option value="monitoring">Monitoring log</option>
						<option value="kyc_register">KYC register extract</option>
					</select>
				</div>
				<div class="f"><label>From</label><input type="date" name="period_from" class="form-control input-sm" value="<?php echo epc_erp_h($date_from_str); ?>"></div>
				<div class="f"><label>To</label><input type="date" name="period_to" class="form-control input-sm" value="<?php echo epc_erp_h($date_to_str); ?>"></div>
				<div class="f"><label>&nbsp;</label><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-magic"></i> Generate report</button></div>
			</form>
			<div id="aml_report_result" style="display:none;margin-top:10px;"></div>
		</div>

		<?php if ($viewReport): ?>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-eye"></i> <?php echo epc_erp_h((string) $viewReport['title']); ?></h4>
			<p class="text-muted small">Ref <?php echo epc_erp_h((string) $viewReport['file_reference']); ?> · <?php echo epc_erp_h((string) $viewReport['filed_to']); ?></p>
			<?php echo $viewReport['body_html']; // trusted internal HTML ?>
		</div>
		<?php endif; ?>

		<div class="epc-aml-panel">
			<h4><i class="fa fa-history"></i> Generated reports</h4>
			<?php if (!$amlReports): ?>
				<p class="text-muted">No reports yet — generate a compliance summary for this period.</p>
			<?php else: ?>
			<table class="table table-bordered table-condensed" style="font-size:13px;">
				<thead><tr><th>When</th><th>Type</th><th>Title</th><th>Flagged</th><th>Ref</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($amlReports as $r): ?>
					<tr>
						<td><?php echo epc_erp_h(date('Y-m-d H:i', (int) $r['time_created'])); ?></td>
						<td><?php echo epc_erp_h((string) $r['report_type']); ?></td>
						<td><?php echo epc_erp_h((string) $r['title']); ?></td>
						<td><?php echo (int) $r['flagged_transactions']; ?></td>
						<td><code><?php echo epc_erp_h((string) $r['file_reference']); ?></code></td>
						<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_aml_url($amlBase, 'reports', 'aml_report=' . (int) $r['id'])); ?>">Open</a></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
			<?php endif; ?>
		</div>

	<?php elseif ($amlSection === 'legislation'): ?>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-gavel"></i> AML legislation library</h4>
			<p class="text-muted">Curated UAE FIU / MoET instruments plus FTA-cached items that match AML / goAML / sanctions keywords. Use <strong>Fetch new AML / tax legislation</strong> above to refresh.</p>
			<ul class="epc-aml-leg__list">
				<?php foreach ($amlLaws as $law): ?>
				<li>
					<span class="epc-aml-badge <?php echo epc_erp_h((string) ($law['badge'] ?? 'core')); ?>">
						<?php
						if (!empty($law['_is_new'])) {
							echo 'new';
						} elseif (!empty($law['_is_changed'])) {
							echo 'upd';
						} else {
							echo epc_erp_h((string) ($law['badge'] ?? 'law'));
						}
						?>
					</span>
					<div style="flex:1;">
						<?php if (!empty($law['href'])): ?>
							<a href="<?php echo epc_erp_h((string) $law['href']); ?>" target="_blank" rel="noopener"><strong><?php echo epc_erp_h((string) $law['title']); ?></strong></a>
						<?php else: ?>
							<strong><?php echo epc_erp_h((string) $law['title']); ?></strong>
						<?php endif; ?>
						<div class="text-muted" style="font-size:11px;margin-top:2px;"><?php echo epc_erp_h((string) ($law['source'] ?? '')); ?></div>
						<div style="font-size:12.5px;margin-top:4px;"><?php echo epc_erp_h((string) ($law['summary'] ?? '')); ?></div>
					</div>
				</li>
				<?php endforeach; ?>
			</ul>
		</div>

	<?php elseif ($amlSection === 'guide'): ?>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-book"></i> AML operator guide</h4>
			<p class="text-muted">Practical programme for jewellery / DNFBP tenants and any company running AML controls in this ERP.</p>
			<?php foreach (epc_aml_guide_sections() as $sec): ?>
				<div style="margin:0 0 18px;">
					<h4 style="font-size:14px;font-weight:800;margin:0 0 8px;"><?php echo epc_erp_h($sec['title']); ?></h4>
					<ol style="margin:0;padding-left:18px;">
						<?php foreach ($sec['points'] as $pt): ?>
						<li style="margin-bottom:6px;font-size:13px;line-height:1.45;"><?php echo epc_erp_h($pt); ?></li>
						<?php endforeach; ?>
					</ol>
				</div>
			<?php endforeach; ?>
		</div>

	<?php elseif ($amlSection === 'settings'): ?>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-cog"></i> AML configuration</h4>
			<form id="aml_settings_form">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<input type="hidden" name="action" value="aml_settings_save">
				<div class="row">
					<div class="col-md-4">
						<div class="form-group">
							<label>Cash reporting threshold (AED)</label>
							<input type="number" name="cash_threshold" class="form-control input-sm" value="<?php echo epc_erp_h((string) (int) $amlDash['cash_threshold']); ?>">
						</div>
					</div>
					<div class="col-md-4">
						<div class="form-group">
							<label>Filing authority</label>
							<select name="authority" class="form-control input-sm">
								<?php
								$auth = (string) $amlDash['authority'];
								foreach (array('UAE FIU (goAML)', 'UK NCA (SAR Online)', 'US FinCEN', 'Custom authority') as $opt) {
									$sel = ($auth === $opt) ? ' selected' : '';
									echo '<option value="' . epc_erp_h($opt) . '"' . $sel . '>' . epc_erp_h($opt) . '</option>';
								}
								?>
							</select>
						</div>
					</div>
					<div class="col-md-4">
						<div class="form-group">
							<label>MLRO name</label>
							<input type="text" name="mlro_name" class="form-control input-sm" value="<?php echo epc_erp_h(epc_aml_setting_get($db_link, 'mlro_name', '')); ?>" placeholder="Compliance officer">
						</div>
					</div>
					<div class="col-md-4">
						<div class="form-group">
							<label>goAML registration no.</label>
							<input type="text" name="goaml_reg" class="form-control input-sm" value="<?php echo epc_erp_h(epc_aml_setting_get($db_link, 'goaml_reg', '')); ?>" placeholder="GOAML-AE-…">
						</div>
					</div>
					<div class="col-md-4">
						<div class="form-group" style="padding-top:24px;">
							<label><input type="checkbox" name="structuring_enabled" value="1" <?php echo epc_aml_setting_get($db_link, 'structuring_enabled', '1') === '1' ? 'checked' : ''; ?>> Structuring detection</label><br>
							<label><input type="checkbox" name="pep_screening" value="1" <?php echo epc_aml_setting_get($db_link, 'pep_screening', '1') === '1' ? 'checked' : ''; ?>> PEP / sanctions screening flag</label>
						</div>
					</div>
				</div>
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save settings</button>
				<button type="button" class="btn btn-default btn-sm" id="aml_seed_rules_btn"><i class="fa fa-magic"></i> Seed default rules</button>
				<span id="aml_settings_status" class="text-muted" style="margin-left:8px;"></span>
			</form>
		</div>
		<div class="epc-aml-panel">
			<h4><i class="fa fa-sliders"></i> Active rules</h4>
			<?php
			$rulesSt = $db_link->prepare('SELECT * FROM `epc_aml_rules` WHERE `company_id` = ? ORDER BY `id`');
			$rulesSt->execute(array($amlCompanyId));
			$rules = $rulesSt->fetchAll(PDO::FETCH_ASSOC) ?: array();
			?>
			<table class="table table-bordered table-condensed" style="font-size:13px;">
				<thead><tr><th>Rule</th><th>Type</th><th>Threshold / freq</th><th>Action</th><th>Active</th></tr></thead>
				<tbody>
				<?php if (!$rules): ?>
					<tr><td colspan="5">No rules — click Seed default rules.</td></tr>
				<?php else: foreach ($rules as $rule): ?>
					<tr>
						<td><?php echo epc_erp_h((string) $rule['rule_name']); ?></td>
						<td><?php echo epc_erp_h((string) $rule['rule_type']); ?></td>
						<td><?php
							if ($rule['rule_type'] === 'threshold') {
								echo epc_erp_h(number_format((float) $rule['threshold_amount'], 0) . ' ' . $rule['threshold_currency']);
							} elseif ($rule['rule_type'] === 'frequency') {
								echo (int) $rule['frequency_count'] . ' / ' . (int) $rule['frequency_period_days'] . 'd';
							} else {
								echo '—';
							}
						?></td>
						<td><?php echo epc_erp_h((string) $rule['action']); ?></td>
						<td><?php echo !empty($rule['is_active']) ? '<span class="label label-success">On</span>' : '<span class="label label-default">Off</span>'; ?></td>
					</tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
	<?php endif; ?>
</div>

<script>
(function(){
	var endpoint = <?php echo json_encode($amlAjax); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var reportsUrl = <?php echo json_encode(epc_aml_url($amlBase, 'reports')); ?>;

	function postAction(data, cb) {
		var fd = new FormData();
		Object.keys(data).forEach(function(k){ if (data[k] !== undefined && data[k] !== null) fd.append(k, data[k]); });
		if (!fd.has('csrf_guard_key')) fd.append('csrf_guard_key', csrf);
		fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function(r){ return r.json(); })
			.then(cb)
			.catch(function(){ cb({ status: false, message: 'Network error' }); });
	}

	var checkForm = document.getElementById('aml_check_form');
	var resultBox = document.getElementById('aml_check_result');
	if (checkForm) {
		checkForm.addEventListener('submit', function(e){
			e.preventDefault();
			var fd = new FormData(checkForm);
			resultBox.style.display = 'none';
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(res){
					var data = res.data || res || {};
					var flagged = !!(data.flagged || res.flagged);
					var score = data.risk_score != null ? data.risk_score : (res.risk_score != null ? res.risk_score : '—');
					var flags = (data.flags || res.flags || []);
					if (!Array.isArray(flags)) flags = [];
					var cls = flagged ? 'danger' : 'success';
					var label = flagged ? 'FLAGGED' : 'CLEAR';
					resultBox.innerHTML = '<div class="alert alert-' + cls + '"><strong>' + label + '</strong> — Risk score: ' + score + '/100. Flags: '
						+ (flags.length ? flags.join('; ') : 'None')
						+ (data.transaction_id ? (' · Log #' + data.transaction_id) : '')
						+ '</div>';
					resultBox.style.display = 'block';
					if (flagged) setTimeout(function(){ location.reload(); }, 900);
				})
				.catch(function(){
					resultBox.innerHTML = '<div class="alert alert-danger">Error running AML check.</div>';
					resultBox.style.display = 'block';
				});
		});
	}

	document.querySelectorAll('.aml-alert-btn').forEach(function(btn){
		btn.addEventListener('click', function(){
			postAction({
				action: 'aml_alert_status',
				transaction_id: btn.getAttribute('data-id'),
				status: btn.getAttribute('data-status'),
				file_sar: btn.getAttribute('data-sar') || '0',
				sar_reference: btn.getAttribute('data-sar') === '1' ? ('STR-' + Date.now()) : ''
			}, function(res){
				alert(res.message || (res.status ? 'Updated' : 'Failed'));
				if (res.status) location.reload();
			});
		});
	});

	var kycForm = document.getElementById('aml_kyc_form');
	if (kycForm) {
		kycForm.addEventListener('submit', function(e){
			e.preventDefault();
			var fd = new FormData(kycForm);
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(res){
					var box = document.getElementById('aml_kyc_result');
					box.style.display = 'block';
					box.innerHTML = '<div class="alert alert-' + (res.status ? 'success' : 'danger') + '">' + (res.message || '') + '</div>';
					if (res.status) setTimeout(function(){ location.reload(); }, 600);
				});
		});
	}

	var reportForm = document.getElementById('aml_report_form');
	if (reportForm) {
		reportForm.addEventListener('submit', function(e){
			e.preventDefault();
			var fd = new FormData(reportForm);
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(res){
					var box = document.getElementById('aml_report_result');
					box.style.display = 'block';
					box.innerHTML = '<div class="alert alert-' + (res.status ? 'success' : 'danger') + '">' + (res.message || '') + '</div>';
					if (res.status && res.id) {
						location.href = reportsUrl + '&aml_report=' + res.id;
					}
				});
		});
	}

	var settingsForm = document.getElementById('aml_settings_form');
	if (settingsForm) {
		settingsForm.addEventListener('submit', function(e){
			e.preventDefault();
			var fd = new FormData(settingsForm);
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(res){
					var st = document.getElementById('aml_settings_status');
					if (st) st.textContent = res.message || '';
					if (res.status) setTimeout(function(){ location.reload(); }, 500);
				});
		});
	}
	var seedBtn = document.getElementById('aml_seed_rules_btn');
	if (seedBtn) {
		seedBtn.addEventListener('click', function(){
			postAction({ action: 'aml_seed_rules' }, function(res){
				alert(res.message || '');
				if (res.status) location.reload();
			});
		});
	}

	var fetchBtn = document.getElementById('epc_aml_fetch_legislation');
	var fetchStatus = document.getElementById('epc_aml_leg_status');
	if (fetchBtn) {
		fetchBtn.addEventListener('click', function(){
			fetchBtn.classList.add('is-busy');
			fetchBtn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Fetching legislation…';
			if (fetchStatus) fetchStatus.textContent = 'Syncing tax.gov.ae legislation cache — AML keywords will refresh…';
			var fd = new FormData();
			fd.append('action', 'uae_tax_fta_fetch');
			fd.append('force', '1');
			fd.append('csrf_guard_key', csrf);
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(res){
					fetchBtn.classList.remove('is-busy');
					fetchBtn.innerHTML = '<i class="fa fa-refresh"></i> Fetch new AML / tax legislation';
					var msg = res.message || (res.status ? 'Legislation updated' : 'Fetch failed');
					if (fetchStatus) fetchStatus.textContent = msg;
					if (res.status || res.ok) setTimeout(function(){ location.reload(); }, 700);
					else alert(msg);
				})
				.catch(function(){
					fetchBtn.classList.remove('is-busy');
					fetchBtn.innerHTML = '<i class="fa fa-refresh"></i> Fetch new AML / tax legislation';
					alert('Fetch failed — try again');
				});
		});
	}
})();
</script>
