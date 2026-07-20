<?php
/**
 * ERP tab — UAE Electronic Invoicing (PINT-AE / MoF Feb 2026).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_concurrency.php';

$einvSection = isset($_GET['einv_section']) ? (string)$_GET['einv_section'] : 'dashboard';
$viewDocId = isset($_GET['einv_doc']) ? (int)$_GET['einv_doc'] : 0;
$viewOrderId = isset($_GET['einv_order']) ? (int)$_GET['einv_order'] : 0;
$viewUserId = isset($_GET['einv_user']) ? (int)$_GET['einv_user'] : 0;

$einvDash = epc_einvoice_dashboard($db_link, $date_from, $date_to);
$readiness = epc_einvoice_readiness_checklist($db_link);
$seller = epc_einvoice_seller_profile($db_link);
$const = epc_einvoice_constants();
$flags = epc_einvoice_transaction_flags();
$taxCats = epc_einvoice_tax_categories();
$journey = epc_einvoice_journey_steps($db_link);
$einvLegislation = epc_einvoice_legislation_items($db_link);
$ftaCache = function_exists('epc_uae_fta_get_cached_legislation')
	? epc_uae_fta_get_cached_legislation($db_link)
	: array();
$legislationUrl = function_exists('epc_uae_fta_legislation_url')
	? epc_uae_fta_legislation_url()
	: 'https://tax.gov.ae/en/legislation.aspx';

$einvBase = epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str, $erpArea ?? 'tax');
if (!isset($DP_Config) && isset($GLOBALS['DP_Config'])) {
	$DP_Config = $GLOBALS['DP_Config'];
}
$einvAjaxUrl = (isset($erpAjaxEndpoint) && $erpAjaxEndpoint !== '')
	? (string) $erpAjaxEndpoint
	: ('/' . (isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp')
		. '/content/shop/finance/erp/ajax_erp_endpoint.php');
$einvModuleMap = epc_einvoice_module_completeness($db_link);
$einvReadyCount = 0;
foreach ($einvModuleMap as $mm) {
	if (($mm['status'] ?? '') === 'ready') {
		$einvReadyCount++;
	}
}
function epc_einv_url($base, $section, $extra = '')
{
	$u = $base . '&einv_section=' . rawurlencode($section);
	return $extra !== '' ? ($u . '&' . $extra) : $u;
}
$activeStepKey = 'learn';
foreach ($journey as $js) {
	if ($einvSection === $js['section'] || ($einvSection === 'view' && $js['section'] === 'invoices') || ($einvSection === 'dashboard' && $js['key'] === 'learn')) {
		if ($einvSection === 'dashboard') {
			break;
		}
		$activeStepKey = $js['key'];
		break;
	}
}
if ($einvSection === 'dashboard') {
	foreach ($journey as $js) {
		if (empty($js['done'])) {
			$activeStepKey = $js['key'];
			break;
		}
		$activeStepKey = $js['key'];
	}
}
?>

<style>
.epc-einv-hero{position:relative;overflow:hidden;border-radius:14px;padding:22px 24px 18px;margin:0 0 18px;background:linear-gradient(125deg,#0a0a0a 0%,#1c1917 42%,#7f1d1d 100%);color:#fff;box-shadow:0 10px 28px rgba(0,0,0,.14);}
.epc-einv-hero::after{content:"";position:absolute;right:-40px;top:-50px;width:200px;height:200px;border-radius:50%;background:radial-gradient(circle,rgba(248,113,113,.35),transparent 68%);pointer-events:none;}
.epc-einv-hero>*{position:relative;z-index:1;}
.epc-einv-hero h2{margin:0 0 6px;font-size:22px;font-weight:800;color:#fff!important;letter-spacing:.01em;}
.epc-einv-hero p{margin:0;max-width:720px;font-size:13px;line-height:1.5;color:rgba(255,255,255,.88);}
.epc-einv-chips{display:flex;flex-wrap:wrap;gap:8px;margin-top:14px;}
.epc-einv-chip{display:inline-flex;align-items:center;gap:6px;padding:5px 10px;border-radius:999px;background:rgba(255,255,255,.1);border:1px solid rgba(255,255,255,.18);font-size:11px;font-weight:700;letter-spacing:.03em;text-transform:uppercase;}
.epc-einv-actions{display:flex;flex-wrap:wrap;align-items:center;gap:10px;margin:0 0 18px;}
.epc-einv-actions .btn-fetch{background:#0a0a0a;border-color:#0a0a0a;color:#fff;font-weight:700;}
.epc-einv-actions .btn-fetch:hover{background:#dc2626;border-color:#b91c1c;color:#fff;}
.epc-einv-actions .btn-fetch.is-busy{opacity:.7;pointer-events:none;}
.epc-einv-steps{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:10px;margin:0 0 20px;}
@media(max-width:1100px){.epc-einv-steps{grid-template-columns:repeat(3,minmax(0,1fr));}}
@media(max-width:640px){.epc-einv-steps{grid-template-columns:1fr 1fr;}}
.epc-einv-step{display:flex;flex-direction:column;gap:8px;padding:14px 12px 12px;border-radius:12px;background:#fff;border:1px solid #e5e5e5;text-decoration:none!important;color:#0a0a0a!important;min-height:128px;box-shadow:0 4px 14px rgba(0,0,0,.04);transition:transform .14s ease,box-shadow .14s ease,border-color .14s ease;position:relative;}
.epc-einv-step:hover{transform:translateY(-2px);box-shadow:0 10px 22px rgba(0,0,0,.08);border-color:#fca5a5;color:#0a0a0a!important;}
.epc-einv-step.is-done{border-color:#86efac;background:linear-gradient(180deg,#f0fdf4,#fff);}
.epc-einv-step.is-current{border-color:#dc2626;box-shadow:0 0 0 2px rgba(220,38,38,.18),0 10px 22px rgba(0,0,0,.08);}
.epc-einv-step__n{width:28px;height:28px;border-radius:8px;display:inline-flex;align-items:center;justify-content:center;font-size:12px;font-weight:800;background:#0a0a0a;color:#fff;}
.epc-einv-step.is-done .epc-einv-step__n{background:#16a34a;}
.epc-einv-step.is-current .epc-einv-step__n{background:#dc2626;}
.epc-einv-step__ico{position:absolute;top:12px;right:12px;font-size:18px;color:#a3a3a3;}
.epc-einv-step.is-done .epc-einv-step__ico{color:#16a34a;}
.epc-einv-step.is-current .epc-einv-step__ico{color:#dc2626;}
.epc-einv-step__t{font-size:13px;font-weight:800;line-height:1.25;margin-top:2px;}
.epc-einv-step__b{font-size:11.5px;color:#737373;line-height:1.35;flex:1;}
.epc-einv-step__cta{font-size:11px;font-weight:700;color:#dc2626;}
.epc-einv-flow{display:flex;flex-wrap:wrap;align-items:center;justify-content:center;gap:6px 4px;margin:0 0 20px;padding:16px;border-radius:12px;background:#fafafa;border:1px solid #e5e5e5;}
.epc-einv-flow__node{display:inline-flex;align-items:center;gap:7px;padding:8px 12px;border-radius:999px;background:#fff;border:1px solid #e5e5e5;font-size:12px;font-weight:700;color:#0a0a0a;}
.epc-einv-flow__node .fa{color:#dc2626;}
.epc-einv-flow__arrow{color:#a3a3a3;font-size:12px;padding:0 2px;}
.epc-einv-leg{margin:0 0 20px;padding:16px 18px;border-radius:12px;background:#fff;border:1px solid #e5e5e5;box-shadow:0 4px 14px rgba(0,0,0,.04);}
.epc-einv-leg h4{margin:0 0 6px;font-size:15px;font-weight:800;}
.epc-einv-leg__list{list-style:none;margin:12px 0 0;padding:0;display:grid;gap:8px;}
.epc-einv-leg__list li{display:flex;gap:10px;align-items:flex-start;padding:10px 12px;border-radius:10px;background:#fafafa;border:1px solid #f0f0f0;}
.epc-einv-leg__list li .badge-new{background:#dc2626;color:#fff;font-size:10px;font-weight:800;padding:2px 7px;border-radius:999px;text-transform:uppercase;}
.epc-einv-leg__list li .badge-upd{background:#b45309;color:#fff;font-size:10px;font-weight:800;padding:2px 7px;border-radius:999px;text-transform:uppercase;}
.epc-einv-leg__list a{font-weight:700;color:#1d4ed8;}
.epc-einv-kpi-strip{display:grid;grid-template-columns:repeat(auto-fit,minmax(140px,1fr));gap:10px;margin:0 0 18px;}
.epc-einv-kpi{padding:14px;border-radius:12px;background:#fff;border:1px solid #e5e5e5;}
.epc-einv-kpi .lbl{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.05em;color:#737373;}
.epc-einv-kpi .val{font-size:22px;font-weight:800;color:#0a0a0a;margin-top:4px;line-height:1.1;}
.epc-einv-kpi .val.ok{color:#16a34a;}
.epc-einv-kpi .val.bad{color:#dc2626;}
.epc-einv-panel-card{background:#fff;border:1px solid #e5e5e5;border-radius:12px;padding:16px 18px;margin:0 0 16px;box-shadow:0 4px 14px rgba(0,0,0,.03);}
.epc-einv-panel-card h4{margin-top:0;}
.epc-einvoice-nav{display:flex;flex-wrap:wrap;gap:6px;margin:0 0 18px;padding:0;list-style:none;}
.epc-einvoice-nav>li>a{display:inline-flex;align-items:center;gap:6px;padding:7px 12px;border-radius:8px;border:1px solid #e5e5e5;background:#fff;color:#404040!important;font-size:12px;font-weight:700;text-decoration:none!important;}
.epc-einvoice-nav>li.active>a,.epc-einvoice-nav>li>a:hover{background:#0a0a0a;border-color:#0a0a0a;color:#fff!important;}
</style>

<div class="epc-erp-section epc-einvoice-panel">
	<div class="epc-einv-hero">
		<h2><i class="fa fa-file-code-o"></i> UAE Electronic Invoicing</h2>
		<p>5-corner Peppol model — Supplier → ASP → Buyer ASP → Buyer, with parallel FTA Tax Data reporting. Follow the steps below to go live with PINT-AE.</p>
		<div class="epc-einv-chips">
			<span class="epc-einv-chip"><i class="fa fa-book"></i> Guidelines V1.0 · 23 Feb 2026</span>
			<span class="epc-einv-chip"><i class="fa fa-calendar"></i> Voluntary 1 Jul 2026</span>
			<span class="epc-einv-chip"><i class="fa fa-flag"></i> Mandatory ≥ AED 50M · 1 Jan 2027</span>
			<span class="epc-einv-chip"><i class="fa fa-code"></i> PINT-AE XML</span>
		</div>
	</div>

	<div class="epc-einv-actions">
		<button type="button" class="btn btn-sm btn-fetch" id="epc_einv_fetch_legislation" title="Pull latest FTA legislation for e-invoicing">
			<i class="fa fa-refresh"></i> Fetch new legislation for e-invoice
		</button>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h($legislationUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> FTA legislation.aspx</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, $erpArea ?? 'tax') . '&tax_panel=legislation'); ?>"><i class="fa fa-gavel"></i> Full tax library</a>
		<span id="epc_einv_leg_status" class="text-muted" style="font-size:12px;">
			<?php
			if (!empty($ftaCache['time_fetched_label'])) {
				echo 'Last FTA sync: ' . epc_erp_h((string) $ftaCache['time_fetched_label']);
				echo ' · ' . count($einvLegislation) . ' e-invoice related item(s)';
			} else {
				echo 'No legislation cache yet — click Fetch to pull from tax.gov.ae';
			}
			?>
		</span>
	</div>

	<div class="epc-einv-flow" aria-label="5-corner Peppol model">
		<span class="epc-einv-flow__node"><i class="fa fa-building"></i> You (Supplier)</span>
		<span class="epc-einv-flow__arrow"><i class="fa fa-long-arrow-right"></i></span>
		<span class="epc-einv-flow__node"><i class="fa fa-cloud"></i> Your ASP</span>
		<span class="epc-einv-flow__arrow"><i class="fa fa-long-arrow-right"></i></span>
		<span class="epc-einv-flow__node"><i class="fa fa-exchange"></i> Buyer ASP</span>
		<span class="epc-einv-flow__arrow"><i class="fa fa-long-arrow-right"></i></span>
		<span class="epc-einv-flow__node"><i class="fa fa-user"></i> Buyer</span>
		<span class="epc-einv-flow__arrow">+</span>
		<span class="epc-einv-flow__node"><i class="fa fa-university"></i> FTA report</span>
	</div>

	<div class="epc-einv-steps" role="navigation" aria-label="E-invoice go-live steps">
		<?php foreach ($journey as $step):
			$isCurrent = ($activeStepKey === $step['key']) || ($einvSection === $step['section']);
			$cls = 'epc-einv-step';
			if (!empty($step['done'])) {
				$cls .= ' is-done';
			}
			if ($isCurrent && $einvSection !== 'dashboard') {
				$cls .= ' is-current';
			} elseif ($einvSection === 'dashboard' && $activeStepKey === $step['key'] && empty($step['done'])) {
				$cls .= ' is-current';
			}
			?>
		<a class="<?php echo epc_erp_h($cls); ?>" href="<?php echo epc_erp_h(epc_einv_url($einvBase, $step['section'])); ?>">
			<span class="epc-einv-step__n"><?php echo !empty($step['done']) ? '<i class="fa fa-check"></i>' : (int) $step['n']; ?></span>
			<span class="epc-einv-step__ico"><i class="fa <?php echo epc_erp_h($step['icon']); ?>"></i></span>
			<span class="epc-einv-step__t">Step <?php echo (int) $step['n']; ?> · <?php echo epc_erp_h($step['title']); ?></span>
			<span class="epc-einv-step__b"><?php echo epc_erp_h($step['blurb']); ?></span>
			<span class="epc-einv-step__cta"><?php echo epc_erp_h($step['cta']); ?> <i class="fa fa-angle-right"></i></span>
		</a>
		<?php endforeach; ?>
	</div>

	<ul class="epc-einvoice-nav">
		<li class="<?php echo $einvSection === 'dashboard' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'dashboard')); ?>"><i class="fa fa-th-large"></i> Overview</a></li>
		<li class="<?php echo in_array($einvSection, array('invoices', 'view'), true) ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'invoices')); ?>"><i class="fa fa-list"></i> Invoices</a></li>
		<li class="<?php echo $einvSection === 'create' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'create')); ?>"><i class="fa fa-plus-circle"></i> Generate</a></li>
		<li class="<?php echo $einvSection === 'seller' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'seller')); ?>"><i class="fa fa-building"></i> Seller</a></li>
		<li class="<?php echo $einvSection === 'buyers' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'buyers')); ?>"><i class="fa fa-users"></i> Buyers</a></li>
		<li class="<?php echo $einvSection === 'asp' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'asp')); ?>"><i class="fa fa-cloud-upload"></i> ASP</a></li>
		<li class="<?php echo $einvSection === 'guide' ? 'active' : ''; ?>"><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'guide')); ?>"><i class="fa fa-book"></i> Guide</a></li>
	</ul>

	<?php if ($einvSection === 'dashboard'): ?>
		<div class="epc-einv-kpi-strip">
			<div class="epc-einv-kpi"><div class="lbl">Readiness</div><div class="val"><?php echo (int) $readiness['percent']; ?>%</div></div>
			<div class="epc-einv-kpi"><div class="lbl">E-invoices</div><div class="val"><?php echo (int) $einvDash['total']; ?></div></div>
			<div class="epc-einv-kpi"><div class="lbl">Validated</div><div class="val ok"><?php echo (int) $einvDash['validated']; ?></div></div>
			<div class="epc-einv-kpi"><div class="lbl">Submitted</div><div class="val"><?php echo (int) $einvDash['submitted']; ?></div></div>
			<div class="epc-einv-kpi"><div class="lbl">Accepted</div><div class="val ok"><?php echo (int) $einvDash['accepted']; ?></div></div>
			<div class="epc-einv-kpi"><div class="lbl">Rejected</div><div class="val bad"><?php echo (int) $einvDash['rejected']; ?></div></div>
			<div class="epc-einv-kpi"><div class="lbl">Incl. VAT</div><div class="val" style="font-size:16px;"><?php echo epc_erp_money($einvDash['amount_incl_vat']); ?></div></div>
		</div>

		<div class="epc-einv-leg" id="epc_einv_legislation_box">
			<h4><i class="fa fa-gavel"></i> E-invoice legislation</h4>
			<p class="text-muted" style="margin:0;font-size:12.5px;">Filtered from FTA <a href="<?php echo epc_erp_h($legislationUrl); ?>" target="_blank" rel="noopener">legislation.aspx</a> — Peppol / PINT-AE / e-invoicing decisions that affect this module.</p>
			<?php if (empty($einvLegislation)): ?>
				<p class="text-muted" style="margin:12px 0 0;">No e-invoice legislation in cache yet. Click <strong>Fetch new legislation for e-invoice</strong> to pull the latest from FTA.</p>
			<?php else: ?>
				<ul class="epc-einv-leg__list">
					<?php foreach (array_slice($einvLegislation, 0, 12) as $leg): ?>
					<li>
						<span><i class="fa fa-file-text-o" style="color:#dc2626;"></i></span>
						<span style="flex:1;min-width:0;">
							<?php if (!empty($leg['_is_new'])): ?><span class="badge-new">New</span> <?php endif; ?>
							<?php if (!empty($leg['_is_changed'])): ?><span class="badge-upd">Updated</span> <?php endif; ?>
							<?php
							$legTitle = (string) ($leg['title'] ?? 'Legislation item');
							$legHref = trim((string) ($leg['url'] ?? $leg['detail_url'] ?? ''));
							if ($legHref !== ''):
							?>
								<a href="<?php echo epc_erp_h($legHref); ?>" target="_blank" rel="noopener"><?php echo epc_erp_h($legTitle); ?></a>
							<?php else: ?>
								<strong><?php echo epc_erp_h($legTitle); ?></strong>
							<?php endif; ?>
							<?php if (!empty($leg['issue_date'])): ?>
								<span class="text-muted" style="font-size:11px;"> · <?php echo epc_erp_h((string) $leg['issue_date']); ?></span>
							<?php endif; ?>
							<?php if (!empty($leg['erp_summary']) || !empty($leg['summary'])): ?>
								<div style="font-size:12px;color:#525252;margin-top:3px;line-height:1.4;"><?php echo epc_erp_h((string) ($leg['erp_summary'] ?? $leg['summary'])); ?></div>
							<?php endif; ?>
							<?php if (!empty($leg['erp_apply'])): ?>
								<div style="font-size:11.5px;color:#737373;margin-top:2px;"><i class="fa fa-wrench"></i> <?php echo epc_erp_h((string) $leg['erp_apply']); ?></div>
							<?php endif; ?>
						</span>
					</li>
					<?php endforeach; ?>
				</ul>
			<?php endif; ?>
		</div>

		<div class="row">
			<div class="col-md-6">
				<div class="epc-einv-panel-card">
					<h4><i class="fa fa-check-square-o"></i> Readiness checklist</h4>
					<div class="progress" style="height:22px;margin-bottom:12px;">
						<div class="progress-bar progress-bar-success" style="width:<?php echo (int)$readiness['percent']; ?>%;line-height:22px;"><?php echo (int)$readiness['percent']; ?>%</div>
					</div>
					<ul class="list-group" style="margin-bottom:0;">
						<?php foreach ($readiness['items'] as $it): ?>
							<li class="list-group-item">
								<i class="fa fa-<?php echo $it['done'] ? 'check-circle text-success' : 'circle-o text-muted'; ?>"></i>
								<?php echo epc_erp_h($it['label']); ?>
							</li>
						<?php endforeach; ?>
					</ul>
				</div>
			</div>
			<div class="col-md-6">
				<div class="epc-einv-panel-card">
					<h4><i class="fa fa-building"></i> Seller status</h4>
					<table class="table table-condensed table-bordered" style="margin-bottom:10px;">
						<tr><td>Legal name</td><td><strong><?php echo epc_erp_h($seller['seller_name'] ?: '—'); ?></strong></td></tr>
						<tr><td>TRN</td><td><?php echo epc_erp_h($seller['seller_trn'] ?: '— configure in Seller profile'); ?></td></tr>
						<tr><td>Peppol endpoint</td><td><code><?php echo epc_erp_h($seller['seller_peppol_endpoint'] ?: '0235:__________'); ?></code></td></tr>
						<tr><td>ASP</td><td><?php echo epc_erp_h($einvDash['asp_name'] ?: '— not selected'); ?></td></tr>
						<tr><td>Specification</td><td><small><code><?php echo epc_erp_h($const['specification_id']); ?></code></small></td></tr>
					</table>
					<p class="text-muted" style="margin:0;font-size:12.5px;">Next incomplete step is highlighted in red above. Onboard via <a href="https://tax.gov.ae" target="_blank" rel="noopener">EmaraTax</a> → E-Invoicing.</p>
				</div>
			</div>
		</div>

	<?php elseif ($einvSection === 'invoices' || ($einvSection === 'view' && $viewDocId > 0)): ?>
		<?php
		if ($viewDocId > 0):
			$doc = epc_einvoice_get_document($db_link, $viewDocId);
			if (!$doc):
				echo '<div class="alert alert-danger">Document not found.</div>';
			else:
				$sellerD = $doc['seller'];
				$buyerD = $doc['buyer'];
		?>
			<p><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'invoices')); ?>" class="btn btn-default btn-sm"><i class="fa fa-arrow-left"></i> Back to list</a>
			<button type="button" class="btn btn-primary btn-sm" onclick="epcEinvDownloadXml(<?php echo (int)$doc['id']; ?>)"><i class="fa fa-download"></i> Download PINT-AE XML</button>
			<?php if ($doc['validation_ok'] && !in_array($doc['status'], array('submitted', 'accepted', 'queued'), true)): ?>
				<button type="button" class="btn btn-success btn-sm" onclick="epcEinvSubmit(<?php echo (int)$doc['id']; ?>)"><i class="fa fa-cloud-upload"></i> Submit to ASP</button>
			<?php endif; ?>
			<?php if ((string) ($doc['invoice_type_code'] ?? '380') !== '381'): ?>
				<button type="button" class="btn btn-warning btn-sm" onclick="epcEinvCreditNote(<?php echo (int)$doc['id']; ?>)"><i class="fa fa-undo"></i> Issue credit note (381)</button>
			<?php endif; ?>
			</p>

			<?php if (!$doc['validation_ok']): ?>
				<div class="alert alert-warning"><strong>Validation errors:</strong>
					<ul style="margin:8px 0 0;"><?php foreach ($doc['validation_errors'] as $err): ?><li><?php echo epc_erp_h($err); ?></li><?php endforeach; ?></ul>
				</div>
			<?php endif; ?>
			<?php
			$bcBadge = '';
			try {
				$bcFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
				if (is_file($bcFile)) {
					require_once $bcFile;
					list($bcType, $bcId) = epc_bc_bos_einvoice_record_keys($doc);
					$bcBadge = epc_bc_bos_document_badge_html($bcType, $bcId, array('show_uid' => true));
				}
			} catch (Throwable $e) {
				$bcBadge = '';
			}
			if ($bcBadge !== ''):
			?>
				<div class="alert alert-info" style="margin-bottom:14px">
					<strong><i class="fa fa-link"></i> Blockchain BOS proof</strong>
					<span style="margin-left:10px"><?php echo $bcBadge; ?></span>
					<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'blockchain_proofs', $date_from_str, $date_to_str)); ?>" class="btn btn-default btn-xs" style="margin-left:8px">All proofs</a>
				</div>
			<?php endif; ?>

			<?php
			$einvRowVer = 0;
			if (function_exists('epc_erp_version_get')) {
				$einvRowVer = epc_erp_version_get($db_link, 'epc_einvoice_documents', (int) $doc['id']);
			} elseif (!empty($doc['row_version'])) {
				$einvRowVer = (int) $doc['row_version'];
			}
			?>
			<div class="epc-einvoice-preview well" style="background:#fff;padding:24px;border:1px solid #cbd5e1;"
				data-erp-entity="invoice"
				data-erp-entity-id="<?php echo (int) $doc['id']; ?>"
				data-erp-row-version="<?php echo (int) $einvRowVer; ?>">
				<h3 style="text-align:center;margin-top:0;"><?php echo ((string)($doc['invoice_type_code'] ?? '380') === '381') ? 'Tax Credit Note' : 'Tax Invoice'; ?></h3>
				<p style="text-align:center;color:#64748b;font-size:12px;">
					<code><?php echo epc_erp_h($const['business_process']); ?></code> ·
					<code><?php echo epc_erp_h($const['specification_id']); ?></code>
				</p>
				<div class="row">
					<div class="col-sm-6">
						<h5><strong>SELLER</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Name</td><td><?php echo epc_erp_h($sellerD['seller_name'] ?? ''); ?></td></tr>
							<tr><td>TRN</td><td><?php echo epc_erp_h($sellerD['seller_trn'] ?? ''); ?></td></tr>
							<tr><td>Legal reg.</td><td><?php echo epc_erp_h($sellerD['seller_legal_reg_no'] ?? ''); ?> (<?php echo epc_erp_h($sellerD['seller_legal_reg_type'] ?? 'TL'); ?>)</td></tr>
							<tr><td>Authority</td><td><?php echo epc_erp_h($sellerD['seller_authority_name'] ?? ''); ?></td></tr>
							<tr><td>Address</td><td><?php echo epc_erp_h(($sellerD['seller_address_line1'] ?? '') . ', ' . ($sellerD['seller_city'] ?? '') . ', UAE'); ?></td></tr>
							<tr><td>Electronic address</td><td><code><?php echo epc_erp_h($sellerD['seller_peppol_endpoint'] ?? ''); ?></code></td></tr>
						</table>
					</div>
					<div class="col-sm-6">
						<h5><strong>INVOICE METADATA</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Invoice number</td><td><strong><?php echo epc_erp_h($doc['invoice_number']); ?></strong></td></tr>
							<tr><td>Issue date</td><td><?php echo epc_erp_h(date('Y-m-d', (int)$doc['issue_date'])); ?></td></tr>
							<tr><td>Type code</td><td><?php echo epc_erp_h($doc['invoice_type_code']); ?></td></tr>
							<tr><td>Currency</td><td><?php echo epc_erp_h($doc['currency_code']); ?></td></tr>
							<tr><td>Due date</td><td><?php echo epc_erp_h(date('Y-m-d', (int)$doc['payment_due_date'])); ?></td></tr>
							<tr><td>Transaction code</td><td><code><?php echo epc_erp_h($doc['transaction_type_code']); ?></code></td></tr>
							<tr><td>UUID</td><td><small><code><?php echo epc_erp_h($doc['uuid']); ?></code></small></td></tr>
							<tr><td>Status</td><td><span class="label label-<?php echo $doc['status'] === 'accepted' ? 'success' : ($doc['status'] === 'rejected' ? 'danger' : 'info'); ?>"><?php echo epc_erp_h(strtoupper($doc['status'])); ?></span></td></tr>
							<?php if ($bcBadge !== ''): ?>
							<tr><td>Blockchain</td><td><?php echo $bcBadge; ?></td></tr>
							<?php endif; ?>
						</table>
					</div>
				</div>
				<div class="row">
					<div class="col-sm-6">
						<h5><strong>BUYER</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Name</td><td><?php echo epc_erp_h($buyerD['buyer_name'] ?? ''); ?></td></tr>
							<tr><td>TRN</td><td><?php echo epc_erp_h($buyerD['buyer_trn'] ?? ''); ?></td></tr>
							<tr><td>Address</td><td><?php echo epc_erp_h(($buyerD['buyer_address_line1'] ?? '') . ', ' . ($buyerD['buyer_city'] ?? '')); ?></td></tr>
							<tr><td>Electronic address</td><td><code><?php echo epc_erp_h($buyerD['buyer_peppol_endpoint'] ?? ''); ?></code></td></tr>
						</table>
					</div>
					<div class="col-sm-6">
						<h5><strong>PAYMENT</strong></h5>
						<table class="table table-condensed" style="font-size:13px;">
							<tr><td>Mode</td><td>Bank transfer (<?php echo epc_erp_h($doc['payment_means_code']); ?>)</td></tr>
							<tr><td>Bank account</td><td><?php echo epc_erp_h($doc['bank_account'] ?: '—'); ?></td></tr>
							<tr><td>Terms</td><td><?php echo epc_erp_h($doc['payment_terms'] ?: '—'); ?></td></tr>
							<tr><td>Order</td><td><?php if (!empty($epc_erp_cp_links)): ?><a href="/<?php echo epc_erp_h($GLOBALS['DP_Config']->backend_dir); ?>/shop/orders/order?id=<?php echo (int)$doc['order_id']; ?>">#<?php echo (int)$doc['order_id']; ?></a><?php else: ?>#<?php echo (int)$doc['order_id']; ?><?php endif; ?></td></tr>
						</table>
					</div>
				</div>

				<table class="table table-bordered table-condensed" style="font-size:12px;margin-top:16px;">
					<thead><tr>
						<th>No.</th><th>Item</th><th>Description</th><th>Qty</th><th>UoM</th><th>Unit AED</th>
						<th>Subtotal</th><th>Tax type</th><th>Rate</th><th>VAT AED</th><th>Gross AED</th>
					</tr></thead>
					<tbody>
					<?php foreach ($doc['lines'] as $ln): ?>
						<tr>
							<td><?php echo (int)$ln['line_no']; ?></td>
							<td><?php echo epc_erp_h($ln['item_name']); ?></td>
							<td><?php echo epc_erp_h($ln['item_description']); ?></td>
							<td><?php echo epc_erp_h(number_format((float)$ln['quantity'], 0)); ?></td>
							<td><?php echo epc_erp_h($ln['uom_code']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($ln['unit_price']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($ln['line_net']); ?></td>
							<td><?php echo epc_erp_h($taxCats[$ln['tax_category']]['label'] ?? $ln['tax_category']); ?></td>
							<td><?php echo epc_erp_h(number_format((float)$ln['tax_rate'], 2)); ?>%</td>
							<td class="text-right"><?php echo epc_erp_money($ln['vat_line_aed']); ?></td>
							<td class="text-right"><?php echo epc_erp_money($ln['gross_amount']); ?></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>

				<div class="row">
					<div class="col-sm-6">
						<h5>VAT breakdown</h5>
						<table class="table table-condensed table-bordered">
							<thead><tr><th>Tax type</th><th>Taxable</th><th>Rate</th><th>VAT</th></tr></thead>
							<tbody>
							<?php foreach ($doc['tax_breakdown'] as $tb): ?>
								<tr>
									<td><?php echo epc_erp_h($tb['label'] ?? $tb['tax_category']); ?></td>
									<td class="text-right"><?php echo epc_erp_money($tb['taxable_amount']); ?></td>
									<td><?php echo epc_erp_h(number_format((float)$tb['tax_rate'], 2)); ?>%</td>
									<td class="text-right"><?php echo epc_erp_money($tb['tax_amount']); ?></td>
								</tr>
							<?php endforeach; ?>
							</tbody>
						</table>
					</div>
					<div class="col-sm-6">
						<table class="table" style="font-size:14px;">
							<tr><td>Total net amount</td><td class="text-right"><strong><?php echo epc_erp_money($doc['subtotal_ex_vat']); ?></strong></td></tr>
							<tr><td>Total excluding VAT</td><td class="text-right"><?php echo epc_erp_money($doc['subtotal_ex_vat']); ?></td></tr>
							<tr><td>Total VAT amount</td><td class="text-right"><?php echo epc_erp_money($doc['total_vat']); ?></td></tr>
							<tr><td>Total including VAT</td><td class="text-right"><strong><?php echo epc_erp_money($doc['total_incl_vat']); ?></strong></td></tr>
							<tr><td>(Less) Paid amount</td><td class="text-right"><?php echo epc_erp_money($doc['paid_amount']); ?></td></tr>
							<tr class="active"><td><strong>Total payable</strong></td><td class="text-right"><strong><?php echo epc_erp_money($doc['amount_due']); ?> AED</strong></td></tr>
						</table>
					</div>
				</div>

				<?php if (!empty($doc['events'])): ?>
					<h5><i class="fa fa-history"></i> Transmission log</h5>
					<table class="table table-condensed table-striped">
						<thead><tr><th>Time</th><th>Event</th><th>Status</th><th>Message</th></tr></thead>
						<tbody>
						<?php foreach ($doc['events'] as $ev): ?>
							<tr>
								<td><?php echo epc_erp_h(date('Y-m-d H:i', (int)$ev['time_created'])); ?></td>
								<td><?php echo epc_erp_h($ev['event_type']); ?></td>
								<td><?php echo epc_erp_h($ev['status']); ?></td>
								<td><?php echo epc_erp_h($ev['message']); ?></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
				<?php endif; ?>
			</div>
		<?php endif; else: ?>
			<div style="display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:10px;margin-bottom:12px;">
				<h4 style="margin:0;"><i class="fa fa-list"></i> Electronic invoices</h4>
				<div style="display:flex;flex-wrap:wrap;gap:8px;">
					<button type="button" class="btn btn-default btn-sm" id="epc_einv_poll_asp"><i class="fa fa-refresh"></i> Poll ASP statuses</button>
					<a class="btn btn-primary btn-sm" href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'create')); ?>"><i class="fa fa-plus"></i> Generate</a>
				</div>
			</div>
			<?php $docs = epc_einvoice_list_documents($db_link, $date_from, $date_to, 150); ?>
			<table class="table table-striped table-bordered table-condensed">
				<thead><tr>
					<th>Invoice</th><th>Type</th><th>Date</th><th>Order</th><th>Customer</th><th>Ex VAT</th><th>VAT</th><th>Incl VAT</th><th>Due</th><th>Status</th><th></th>
				</tr></thead>
				<tbody>
				<?php foreach ($docs as $d): ?>
					<tr>
						<td><strong><?php echo epc_erp_h($d['invoice_number']); ?></strong><br><small class="text-muted"><code><?php echo epc_erp_h(substr($d['uuid'], 0, 8)); ?>…</code></small></td>
						<td><span class="label label-<?php echo ((string)($d['invoice_type_code'] ?? '380') === '381') ? 'warning' : 'info'; ?>"><?php echo epc_erp_h((string)($d['invoice_type_code'] ?? '380')); ?></span></td>
						<td><?php echo epc_erp_h(date('Y-m-d', (int)$d['issue_date'])); ?></td>
						<td><?php echo (int)$d['order_id'] ? ('#' . (int)$d['order_id']) : '—'; ?></td>
						<td><?php echo (int)$d['user_id'] ? ('ID ' . (int)$d['user_id']) : 'Guest'; ?></td>
						<td><?php echo epc_erp_money($d['subtotal_ex_vat']); ?></td>
						<td><?php echo epc_erp_money($d['total_vat']); ?></td>
						<td><?php echo epc_erp_money($d['total_incl_vat']); ?></td>
						<td><?php echo epc_erp_money($d['amount_due']); ?></td>
						<td><span class="label label-default"><?php echo epc_erp_h($d['status']); ?></span></td>
						<td><a class="btn btn-xs btn-primary" href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'view', 'einv_doc=' . (int)$d['id'])); ?>">View</a></td>
					</tr>
				<?php endforeach; ?>
				<?php if (!$docs): ?><tr><td colspan="11" class="text-muted text-center">No e-invoices in this period. Generate from a completed order.</td></tr><?php endif; ?>
				</tbody>
			</table>
		<?php endif; ?>

	<?php elseif ($einvSection === 'create'): ?>
		<h4><i class="fa fa-plus-circle"></i> Generate electronic Tax Invoice from order</h4>
		<p class="text-muted">Builds PINT-AE XML with all mandatory fields. Buyer Peppol ID defaults to <code>0235:9900000098</code> if customer not onboarded.</p>
		<form id="epc_einv_form_create" class="form-horizontal well">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-group">
				<label class="col-sm-3 control-label">Order ID</label>
				<div class="col-sm-4">
					<input type="number" name="order_id" class="form-control" value="<?php echo $viewOrderId > 0 ? (int)$viewOrderId : ''; ?>" required placeholder="e.g. 1234">
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Transaction scenarios</label>
				<div class="col-sm-8">
					<?php foreach ($flags as $key => $label): ?>
						<label class="checkbox-inline"><input type="checkbox" name="flag_<?php echo epc_erp_h($key); ?>" value="1"> <?php echo epc_erp_h($label); ?></label><br>
					<?php endforeach; ?>
					<p class="help-block">8-digit transaction type code built from checked flags (MoF mandatory field #5).</p>
				</div>
			</div>
			<div class="form-group">
				<div class="col-sm-offset-3 col-sm-8">
					<button type="submit" class="btn btn-primary"><i class="fa fa-magic"></i> Generate &amp; validate</button>
				</div>
			</div>
		</form>

	<?php elseif ($einvSection === 'seller'): ?>
		<h4><i class="fa fa-building"></i> Seller profile (mandatory fields 10–20)</h4>
		<form id="epc_einv_form_seller" class="form-horizontal">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<?php
			$sfields = array(
				'seller_name' => 'Legal name',
				'seller_trn' => 'TRN (15 digits)',
				'seller_tin' => 'TIN (10 digits — auto from TRN if blank)',
				'seller_legal_reg_no' => 'Trade license / legal registration no.',
				'seller_legal_reg_type' => 'Registration type (TL/EID/PAS/CD)',
				'seller_authority_name' => 'Authority name (e.g. Dubai Economy and Tourism)',
				'seller_address_line1' => 'Address line 1',
				'seller_city' => 'City',
				'seller_emirate' => 'Emirate / subdivision',
				'seller_country_code' => 'Country code',
				'seller_phone' => 'Phone',
				'seller_email' => 'Email',
				'seller_bank_account' => 'Bank account (payment)',
				'payment_terms' => 'Payment terms',
			);
			foreach ($sfields as $k => $lbl):
				$val = epc_einvoice_get_setting($db_link, $k, $seller[$k] ?? '');
			?>
			<div class="form-group">
				<label class="col-sm-3 control-label"><?php echo epc_erp_h($lbl); ?></label>
				<div class="col-sm-6"><input type="text" name="<?php echo epc_erp_h($k); ?>" class="form-control" value="<?php echo epc_erp_h($val); ?>"></div>
			</div>
			<?php endforeach; ?>
			<div class="form-group">
				<label class="col-sm-3 control-label">VAT registered (FTA)</label>
				<div class="col-sm-6">
					<?php
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';
					$coProf = epc_uae_company_profile($db_link);
					$vatRegOn = !empty($coProf['vat_registered']);
					?>
					<label class="checkbox-inline">
						<input type="checkbox" name="company_vat_registered" value="1" <?php echo $vatRegOn ? 'checked' : ''; ?> />
						Company is VAT-registered in UAE (required for output / input VAT)
					</label>
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Peppol endpoint</label>
				<div class="col-sm-6">
					<p class="form-control-static"><code><?php echo epc_erp_h($seller['seller_peppol_endpoint'] ?: '0235:' . epc_einvoice_tin_from_trn($seller['seller_trn'])); ?></code></p>
					<p class="help-block">Auto: 0235 + TIN (first 10 digits of TRN). Country must stay <strong>AE</strong> and TRN 15 digits for FTA tax invoices.</p>
				</div>
			</div>
			<div class="form-group"><div class="col-sm-offset-3 col-sm-6"><button type="submit" class="btn btn-primary">Save seller profile</button></div></div>
		</form>

	<?php elseif ($einvSection === 'buyers'): ?>
		<h4><i class="fa fa-users"></i> Buyer Peppol / TRN profiles</h4>
		<p class="text-muted">B2B buyers need TRN and Peppol endpoint (<code>0235:TIN</code>). If not onboarded, system uses <code><?php echo epc_erp_h($const['endpoint_not_onboarded']); ?></code>.</p>
		<?php
		$buyerEdit = $viewUserId > 0 ? epc_einvoice_buyer_profile($db_link, $viewUserId) : null;
		$buyerList = $db_link->query(
			'SELECT b.*, u.`email` FROM `epc_einvoice_buyer_profiles` b
			LEFT JOIN `users` u ON u.`user_id` = b.`user_id`
			ORDER BY b.`time_updated` DESC LIMIT 100'
		)->fetchAll(PDO::FETCH_ASSOC);
		?>
		<form id="epc_einv_form_buyer" class="form-horizontal well">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-group">
				<label class="col-sm-2 control-label">Customer user ID</label>
				<div class="col-sm-3"><input type="number" name="user_id" class="form-control" value="<?php echo $buyerEdit ? (int)$buyerEdit['user_id'] : ''; ?>" required></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Buyer name</label>
				<div class="col-sm-4"><input type="text" name="buyer_name" class="form-control" value="<?php echo epc_erp_h($buyerEdit['buyer_name'] ?? ''); ?>"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">TRN</label>
				<div class="col-sm-4"><input type="text" name="trn" class="form-control" value="<?php echo epc_erp_h($buyerEdit['trn'] ?? ''); ?>" placeholder="15-digit TRN"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Legal reg. / type</label>
				<div class="col-sm-3"><input type="text" name="legal_reg_no" class="form-control" value="<?php echo epc_erp_h($buyerEdit['legal_reg_no'] ?? ''); ?>"></div>
				<div class="col-sm-2"><select name="legal_reg_type" class="form-control">
					<?php foreach (array('TL', 'EID', 'PAS', 'CD') as $t): ?>
						<option value="<?php echo $t; ?>" <?php echo ($buyerEdit['legal_reg_type'] ?? 'TL') === $t ? 'selected' : ''; ?>><?php echo $t; ?></option>
					<?php endforeach; ?>
				</select></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Address / city</label>
				<div class="col-sm-4"><input type="text" name="address_line1" class="form-control" value="<?php echo epc_erp_h($buyerEdit['address_line1'] ?? ''); ?>"></div>
				<div class="col-sm-2"><input type="text" name="city" class="form-control" value="<?php echo epc_erp_h($buyerEdit['city'] ?? 'Dubai'); ?>"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-2 control-label">Peppol endpoint</label>
				<div class="col-sm-4"><input type="text" name="peppol_endpoint" class="form-control" value="<?php echo epc_erp_h($buyerEdit['peppol_endpoint'] ?? ''); ?>" placeholder="0235:1245780912"></div>
				<div class="col-sm-3"><label class="checkbox-inline"><input type="checkbox" name="buyer_onboarded" value="1" <?php echo !empty($buyerEdit['buyer_onboarded']) ? 'checked' : ''; ?>> Onboarded on Peppol</label></div>
			</div>
			<div class="form-group"><div class="col-sm-offset-2"><button type="submit" class="btn btn-primary">Save buyer profile</button></div></div>
		</form>
		<table class="table table-condensed table-bordered">
			<thead><tr><th>User</th><th>Name</th><th>TRN</th><th>Peppol</th><th>Onboarded</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($buyerList as $b): ?>
				<tr>
					<td><?php echo (int)$b['user_id']; ?></td>
					<td><?php echo epc_erp_h($b['buyer_name']); ?></td>
					<td><?php echo epc_erp_h($b['trn'] ?: '—'); ?></td>
					<td><code><?php echo epc_erp_h($b['peppol_endpoint'] ?: '—'); ?></code></td>
					<td><?php echo (int)$b['buyer_onboarded'] ? 'Yes' : 'No'; ?></td>
					<td><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'buyers', 'einv_user=' . (int)$b['user_id'])); ?>">Edit</a></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>

	<?php elseif ($einvSection === 'asp'): ?>
		<h4><i class="fa fa-cloud-upload"></i> Accredited Service Provider (ASP) &amp; FTA reporting</h4>
		<div class="alert alert-warning">
			<strong>5-corner model:</strong> Your ASP validates XML, transmits to buyer's ASP (Corner 3), and reports Tax Data to FTA (Corner 5) in parallel.
			Compliance obligation remains with you as supplier — select one ASP for send + receive via <a href="https://tax.gov.ae" target="_blank" rel="noopener">EmaraTax</a>.
		</div>
		<form id="epc_einv_form_asp" class="form-horizontal well">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<div class="form-group">
				<label class="col-sm-3 control-label">ASP name</label>
				<div class="col-sm-5"><input type="text" name="asp_name" class="form-control" value="<?php echo epc_erp_h(epc_einvoice_get_setting($db_link, 'asp_name', '')); ?>" placeholder="Your accredited ASP"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">Transmission mode</label>
				<div class="col-sm-4">
					<select name="asp_api_mode" class="form-control">
						<option value="manual" <?php echo epc_einvoice_get_setting($db_link, 'asp_api_mode') === 'manual' ? 'selected' : ''; ?>>Manual — download XML, upload via ASP portal</option>
						<option value="api" <?php echo epc_einvoice_get_setting($db_link, 'asp_api_mode') === 'api' ? 'selected' : ''; ?>>API — automated queue (configure URL)</option>
					</select>
				</div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">ASP API base URL</label>
				<div class="col-sm-6"><input type="url" name="asp_api_url" class="form-control" value="<?php echo epc_erp_h(epc_einvoice_get_setting($db_link, 'asp_api_url', '')); ?>" placeholder="https://asp.example/api"></div>
			</div>
			<div class="form-group">
				<label class="col-sm-3 control-label">ASP API key</label>
				<div class="col-sm-6"><input type="password" name="asp_api_key" class="form-control" value="<?php echo epc_erp_h(epc_einvoice_get_setting($db_link, 'asp_api_key', '')); ?>" placeholder="Bearer token from your ASP" autocomplete="off"></div>
			</div>
			<div class="form-group"><div class="col-sm-offset-3"><button type="submit" class="btn btn-primary">Save ASP settings</button></div></div>
		</form>
		<h5>Predefined Peppol endpoints (MoF scenarios)</h5>
		<table class="table table-condensed table-bordered">
			<tr><td>Buyer not onboarded</td><td><code><?php echo epc_erp_h($const['endpoint_not_onboarded']); ?></code></td></tr>
			<tr><td>Deemed supply</td><td><code><?php echo epc_erp_h($const['endpoint_deemed_supply']); ?></code></td></tr>
			<tr><td>Exports (buyer no Peppol ID)</td><td><code><?php echo epc_erp_h($const['endpoint_exports']); ?></code></td></tr>
		</table>

	<?php elseif ($einvSection === 'guide'): ?>
		<div class="epc-einv-panel-card">
			<h4 style="margin-top:0;"><i class="fa fa-book"></i> UAE e-Invoicing complete guide</h4>
			<p style="margin:0;font-size:13px;line-height:1.5;">
				This ERP module implements the MoF <strong>Electronic Invoicing Guidelines V1.0 (23 Feb 2026)</strong>,
				<strong>Mandatory Fields V1.0</strong>, and the Peppol <strong>PINT-AE</strong> billing specification
				(<code><?php echo epc_erp_h($const['specification_id']); ?></code>) under Ministerial Decisions
				<strong>243/2025</strong> and <strong>244/2025</strong>.
			</p>
			<p style="margin:10px 0 0;font-size:13px;">
				Module map:
				<strong><?php echo (int) $einvReadyCount; ?> / <?php echo (int) count($einvModuleMap); ?></strong> capability areas ready ·
				Readiness checklist <strong><?php echo (int) $readiness['percent']; ?>%</strong> ·
				<a href="https://www.mof.gov.ae/eInvoicing" target="_blank" rel="noopener">mof.gov.ae/eInvoicing</a> ·
				<a href="<?php echo epc_erp_h($legislationUrl); ?>" target="_blank" rel="noopener">FTA legislation</a>
			</p>
		</div>

		<div class="epc-einv-panel-card">
			<h4><i class="fa fa-check-circle"></i> What is fully implemented in ERP</h4>
			<table class="table table-bordered table-condensed" style="margin:0;font-size:12.5px;">
				<thead>
					<tr>
						<th style="width:16%;">Area</th>
						<th>MoF / Peppol requirement</th>
						<th>ERP capability</th>
						<th style="width:10%;">Status</th>
						<th style="width:10%;"></th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($einvModuleMap as $mm): ?>
					<tr>
						<td><strong><?php echo epc_erp_h($mm['area']); ?></strong></td>
						<td><?php echo epc_erp_h($mm['requirement']); ?></td>
						<td><?php echo epc_erp_h($mm['erp']); ?></td>
						<td>
							<?php if (($mm['status'] ?? '') === 'ready'): ?>
								<span class="label label-success">Ready</span>
							<?php else: ?>
								<span class="label label-warning">Setup</span>
							<?php endif; ?>
						</td>
						<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_einv_url($einvBase, (string) $mm['section'])); ?>">Open</a></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>

		<div class="row">
			<div class="col-md-6">
				<div class="epc-einv-panel-card">
					<h4><i class="fa fa-calendar"></i> Implementation timeline (MD 244/2025)</h4>
					<table class="table table-bordered table-condensed">
						<thead><tr><th>Phase</th><th>ASP by</th><th>Go-live</th></tr></thead>
						<tbody>
							<tr><td>Pilot (invitation)</td><td>—</td><td>1 Jul 2026</td></tr>
							<tr><td>Voluntary (all businesses)</td><td>—</td><td>1 Jul 2026</td></tr>
							<tr><td>Mandatory · revenue ≥ AED 50M</td><td>31 Jul 2026</td><td>1 Jan 2027</td></tr>
							<tr><td>Mandatory · revenue &lt; AED 50M</td><td>31 Mar 2027</td><td>1 Jul 2027</td></tr>
							<tr><td>Government entities</td><td>31 Mar 2027</td><td>1 Oct 2027</td></tr>
						</tbody>
					</table>
					<p class="text-muted" style="margin:0;font-size:12px;">Today is inside the voluntary window — configure seller + ASP now and start testing PINT-AE XML.</p>
				</div>
				<div class="epc-einv-panel-card">
					<h4><i class="fa fa-sitemap"></i> 5-corner Peppol model</h4>
					<ol style="font-size:13px;line-height:1.55;margin-bottom:0;">
						<li><strong>Corner 1 — You (supplier)</strong> create the e-invoice in this ERP.</li>
						<li><strong>Corner 2 — Your ASP</strong> validates PINT-AE XML and routes it on Peppol.</li>
						<li><strong>Corner 3 — Buyer ASP</strong> receives and delivers to the buyer.</li>
						<li><strong>Corner 4 — Buyer</strong> gets the structured invoice.</li>
						<li><strong>Corner 5 — FTA</strong> receives Tax Data reporting from your ASP in parallel.</li>
					</ol>
					<p class="text-muted" style="margin:10px 0 0;font-size:12px;">Compliance stays with you as supplier — appoint <em>one</em> ASP for send + receive via EmaraTax.</p>
				</div>
			</div>
			<div class="col-md-6">
				<div class="epc-einv-panel-card">
					<h4><i class="fa fa-road"></i> How to use this ERP module</h4>
					<ol style="font-size:13px;line-height:1.55;">
						<li>Read this guide, then fetch FTA legislation from the Overview bar.</li>
						<li>Complete <a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'seller')); ?>">Seller profile</a> (15-digit TRN → Peppol <code>0235:TIN</code>).</li>
						<li>Capture <a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'buyers')); ?>">Buyer Peppol / TRN</a> for B2B customers (or use MoF fallback endpoints).</li>
						<li>Select your <a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'asp')); ?>">ASP</a> — manual portal upload or live API (URL + key).</li>
						<li><a href="<?php echo epc_erp_h(epc_einv_url($einvBase, 'create')); ?>">Generate</a> a Tax Invoice (380) from a completed order; set transaction scenario flags.</li>
						<li>Open the invoice → download XML → <strong>Submit to ASP</strong>. Poll statuses from the invoice list.</li>
						<li>Issue a <strong>Credit Note (381)</strong> from the invoice view when you need an output-tax reduction.</li>
					</ol>
					<p style="margin:0;font-size:12.5px;">Also available: Sales → Invoices, Document Control print templates, Blockchain BOS proof badges on submitted docs.</p>
				</div>
				<div class="epc-einv-panel-card">
					<h4><i class="fa fa-file-text-o"></i> Document types &amp; tax categories</h4>
					<ul style="font-size:13px;">
						<li><strong>380 Tax Invoice</strong> — taxable supplies (TRN required)</li>
						<li><strong>381 Tax Credit Note</strong> — linked reversal / partial credit</li>
						<li><strong>Commercial invoice</strong> — non-VAT / non-taxable supplies (same pipeline)</li>
					</ul>
					<table class="table table-condensed table-bordered" style="margin-bottom:0;">
						<?php foreach ($taxCats as $code => $tc): ?>
							<tr><td><code><?php echo epc_erp_h($code); ?></code></td><td><?php echo epc_erp_h($tc['label']); ?></td></tr>
						<?php endforeach; ?>
					</table>
				</div>
			</div>
		</div>

		<div class="epc-einv-panel-card">
			<h4><i class="fa fa-list-ol"></i> Mandatory fields validated before ASP submission</h4>
			<p class="text-muted" style="font-size:12.5px;">Aligned to MoF Mandatory Fields V1.0 (23 Feb 2026). Line-level name, qty, UoM, net, tax category, rate and VAT are also enforced.</p>
			<div class="row">
				<?php
				$mandatory = epc_einvoice_mandatory_field_map();
				$chunks = array_chunk($mandatory, (int) ceil(count($mandatory) / 2), true);
				foreach ($chunks as $chunk):
				?>
				<div class="col-md-6">
					<ul style="font-size:12.5px;columns:1;">
						<?php foreach ($chunk as $k => $lbl): ?><li><?php echo epc_erp_h($lbl); ?> <span class="text-muted">(<code><?php echo epc_erp_h($k); ?></code>)</span></li><?php endforeach; ?>
					</ul>
				</div>
				<?php endforeach; ?>
			</div>
		</div>

		<div class="epc-einv-panel-card">
			<h4><i class="fa fa-shield"></i> Retention, penalties &amp; official links</h4>
			<ul style="font-size:13px;line-height:1.55;margin-bottom:8px;">
				<li>Retain e-invoice data <strong>5 years</strong> (7 for real estate) and reproduce for FTA on request — ERP keeps XML + transmission log.</li>
				<li>Appointing an ASP late or failing to transmit can attract FTA administrative penalties (e.g. AED 5,000 / month for non-appointment under published penalty rules).</li>
				<li>Data must be accessible “within the State” for FTA — cloud storage is fine if reproducible on demand.</li>
			</ul>
			<p style="margin:0;font-size:12.5px;">
				<a href="https://www.mof.gov.ae/eInvoicing" target="_blank" rel="noopener">MoF e-Invoicing hub</a> ·
				<a href="https://tax.gov.ae" target="_blank" rel="noopener">EmaraTax / tax.gov.ae</a> ·
				<a href="<?php echo epc_erp_h($legislationUrl); ?>" target="_blank" rel="noopener">FTA legislation.aspx</a> ·
				<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, $erpArea ?? 'tax') . '&tax_panel=legislation'); ?>">ERP tax library</a>
			</p>
		</div>
	<?php endif; ?>
</div>

<script>
(function(){
	var erpPostUrl = <?php echo json_encode($einvAjaxUrl); ?>;
	var erpAjaxUrl = erpPostUrl;
	function parseJsonResponse(r) {
		return r.text().then(function(t) {
			var trimmed = (t || '').trim();
			if (trimmed.charAt(0) === '<') {
				throw new Error('Server returned HTML instead of JSON — hard-refresh the page (Ctrl+F5) and try again.');
			}
			try { return JSON.parse(trimmed); }
			catch (e) {
				throw new Error('Server returned invalid JSON (HTTP ' + r.status + '). ' + trimmed.substring(0, 120));
			}
		});
	}
	function post(action, data, cb) {
		var fd = new FormData();
		fd.append('action', action);
		for (var k in data) { if (data.hasOwnProperty(k)) fd.append(k, data[k]); }
		fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(cb).catch(function(e){ alert(e.message || 'Request failed'); });
	}
	function bindForm(id, action, extra) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(ev) {
			ev.preventDefault();
			var data = extra ? extra(f) : {};
			new FormData(f).forEach(function(v,k){ data[k]=v; });
			post(action, data, function(res) {
				alert(res.message || (res.status ? 'OK' : 'Error'));
				if (res.status && res.redirect) location.href = res.redirect;
				else if (res.status) location.reload();
			});
		});
	}
	bindForm('epc_einv_form_create', 'einvoice_create', function(f) {
		var flags = {};
		f.querySelectorAll('input[type=checkbox]').forEach(function(cb) {
			if (cb.name.indexOf('flag_') === 0 && cb.checked) flags[cb.name.replace('flag_','')] = '1';
		});
		return { transaction_flags: JSON.stringify(flags) };
	});
	bindForm('epc_einv_form_seller', 'einvoice_save_seller');
	bindForm('epc_einv_form_buyer', 'einvoice_save_buyer');
	bindForm('epc_einv_form_asp', 'einvoice_save_asp');
	window.epcEinvSubmit = function(id) {
		if (!confirm('Submit this e-invoice to your ASP for exchange and FTA reporting?')) return;
		post('einvoice_submit', { document_id: id, csrf_guard_key: <?php echo json_encode($csrf); ?> }, function(res) {
			alert(res.message || '');
			if (res.status) location.reload();
		});
	};
	window.epcEinvCreditNote = function(id) {
		var reason = prompt('Credit note reason (e.g. Sales return, pricing adjustment):', 'Sales return');
		if (reason === null) return;
		if (!confirm('Issue Electronic Tax Credit Note (type 381) as a full reversal of this invoice?')) return;
		post('einvoice_credit_note', {
			document_id: id,
			reason: reason || 'Sales return',
			csrf_guard_key: <?php echo json_encode($csrf); ?>
		}, function(res) {
			alert(res.message || '');
			if (res.status && res.redirect) location.href = res.redirect;
			else if (res.status) location.reload();
		});
	};
	window.epcEinvDownloadXml = function(id) {
		var dlBase = <?php echo json_encode($erpUrl); ?>;
		var sep = dlBase.indexOf('?') >= 0 ? '&' : '?';
		window.open(dlBase + sep + 'action=einvoice_download_xml&document_id=' + id + '&csrf_guard_key=' + encodeURIComponent(<?php echo json_encode($csrf); ?>), '_blank');
	};
	var pollBtn = document.getElementById('epc_einv_poll_asp');
	if (pollBtn) {
		pollBtn.addEventListener('click', function() {
			pollBtn.disabled = true;
			pollBtn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Polling…';
			post('einvoice_poll_asp', { csrf_guard_key: <?php echo json_encode($csrf); ?> }, function(res) {
				pollBtn.disabled = false;
				pollBtn.innerHTML = '<i class="fa fa-refresh"></i> Poll ASP statuses';
				alert(res.message || (res.status ? 'Poll complete' : 'Poll failed'));
				if (res.status) location.reload();
			});
		});
	}
	var fetchBtn = document.getElementById('epc_einv_fetch_legislation');
	var fetchStatus = document.getElementById('epc_einv_leg_status');
	if (fetchBtn) {
		fetchBtn.addEventListener('click', function() {
			fetchBtn.classList.add('is-busy');
			fetchBtn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Fetching FTA legislation…';
			if (fetchStatus) {
				fetchStatus.textContent = 'Contacting tax.gov.ae/en/legislation.aspx — this can take a minute…';
			}
			var fd = new FormData();
			fd.append('action', 'uae_tax_fta_fetch');
			fd.append('force', '1');
			fd.append('csrf_guard_key', <?php echo json_encode($csrf); ?>);
			fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(parseJsonResponse)
				.then(function(res) {
					fetchBtn.classList.remove('is-busy');
					fetchBtn.innerHTML = '<i class="fa fa-refresh"></i> Fetch new legislation for e-invoice';
					var msg = res.message || (res.status ? 'Legislation updated' : 'Fetch failed');
					var nNew = res.new_count != null ? res.new_count
						: (res.new_since_last ? res.new_since_last.length : null);
					var nAll = res.legislation ? res.legislation.length : null;
					if (fetchStatus) {
						fetchStatus.innerHTML = msg
							+ (nAll != null ? (' · ' + nAll + ' item(s) cached') : '')
							+ (nNew != null ? (' · <span class="text-success">' + nNew + ' new</span>') : '');
					}
					if (res.status || res.ok) {
						setTimeout(function() { location.reload(); }, 700);
					} else {
						alert(msg);
					}
				})
				.catch(function(e) {
					fetchBtn.classList.remove('is-busy');
					fetchBtn.innerHTML = '<i class="fa fa-refresh"></i> Fetch new legislation for e-invoice';
					var m = e.message || 'Request failed';
					if (fetchStatus) { fetchStatus.textContent = m; }
					alert(m);
				});
		});
	}
})();
</script>
