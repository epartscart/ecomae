<?php
/**
 * CP — Power BI step-by-step operator guide (Portal level).
 * Available on Super CP and tenant CP.
 *
 * Open via /cp/control/portal/epc_power_bi_guide (CMS route).
 */
if (!defined('_ASTEXE_')) {
	$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== ''
		? ('?' . $_SERVER['QUERY_STRING'])
		: '';
	header('Location: /cp/control/portal/epc_power_bi_guide' . $qs, true, 302);
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_power_bi.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

function epc_pbig_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
$steps = epc_power_bi_guide_steps();
$caps = epc_power_bi_capabilities();
$datasets = epc_power_bi_dataset_catalog('https://www.ecomae.com');
$configUrl = '/' . $backend . '/control/portal/epc_power_bi';
$keysUrl = '/' . $backend . '/control/portal/epc_api_documentation_guide';
$hubUrl = '/' . $backend . '/control/portal/epc_integrations_hub';

epc_cp_page_frame_open(array('class' => 'epc-power-bi-guide'));
?>
<style>
.epc-pbig-step{border:1px solid #e2e8f0;border-radius:10px;padding:16px 18px;margin:0 0 14px;background:#fff}
.epc-pbig-step h4{margin:0 0 8px;font-size:16px;color:#0f172a}
.epc-pbig-step p{margin:0 0 8px;color:#334155;line-height:1.55}
.epc-pbig-step ul{margin:8px 0 0;padding-left:18px;color:#475569}
.epc-pbig-step code,.epc-pbig-step pre{font-size:12px}
.epc-pbig-step pre{background:#0f172a;color:#e2e8f0;padding:12px 14px;border-radius:8px;overflow:auto;margin:10px 0 0}
.epc-pbig-num{display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border-radius:50%;background:#f2c811;color:#111;font-weight:700;font-size:13px;margin-right:8px}
</style>
<div class="epc-portal-settings epc-power-bi-guide">
	<div class="hero">
		<h2><i class="fa fa-book"></i> Power BI — step-by-step guide</h2>
		<p style="margin:0;opacity:.92">
			Connect Microsoft Power BI to your tenant ERP data from Control Panel.
			Follow the steps below in order — Desktop refresh works today without Azure AD.
		</p>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Quick links</h4></div>
		<div class="panel-body">
			<a class="btn btn-primary btn-sm" href="<?php echo epc_pbig_h($configUrl); ?>"><i class="fa fa-bar-chart"></i> Power BI settings</a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_pbig_h($keysUrl); ?>"><i class="fa fa-key"></i> API keys guide</a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_pbig_h($hubUrl); ?>"><i class="fa fa-plug"></i> Integrations hub</a>
			<a class="btn btn-default btn-sm" href="https://www.ecomae.com/epc-api/v1/health" target="_blank" rel="noopener"><i class="fa fa-heartbeat"></i> API health</a>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>At a glance</h4></div>
		<div class="panel-body">
			<div class="row">
				<div class="col-md-6">
					<p><strong>Works now</strong></p>
					<ul>
						<li>Power BI Desktop / Service Web connector</li>
						<li>JSON + CSV datasets with <code>X-API-Key</code></li>
						<li>CP workspace / report ID storage</li>
						<li>Optional <code>*.powerbi.com</code> iframe preview</li>
					</ul>
				</div>
				<div class="col-md-6">
					<p><strong>Needs your Microsoft account later</strong></p>
					<ul>
						<?php foreach ($caps['needs_customer_credentials'] as $k => $why) { ?>
						<li><?php echo epc_pbig_h($why); ?></li>
						<?php } ?>
					</ul>
				</div>
			</div>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Step-by-step</h4></div>
		<div class="panel-body">
			<?php foreach ($steps as $i => $step) {
				$n = $i + 1;
				?>
			<div class="epc-pbig-step" id="step-<?php echo (int) $n; ?>">
				<h4><span class="epc-pbig-num"><?php echo (int) $n; ?></span><?php echo epc_pbig_h(preg_replace('/^Step\s+\d+\s+[—\-]\s*/u', '', $step['title'])); ?></h4>
				<p><?php echo $step['body']; ?></p>
				<?php if (!empty($step['tips'])) { ?>
				<ul>
					<?php foreach ($step['tips'] as $tip) {
						$isCmd = (strpos($tip, 'curl ') === 0) || (strpos($tip, 'http') === 0) || (strpos($tip, '…/') === 0);
						if ($isCmd) { ?>
					<li><code><?php echo epc_pbig_h($tip); ?></code></li>
						<?php } else { ?>
					<li><?php echo $tip; ?></li>
						<?php }
					} ?>
				</ul>
				<?php } ?>
			</div>
			<?php } ?>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Dataset cheat sheet</h4></div>
		<div class="panel-body">
			<table class="table table-striped">
				<thead>
					<tr><th>#</th><th>Dataset</th><th>URL</th><th>Formats</th></tr>
				</thead>
				<tbody>
				<?php foreach ($datasets as $j => $ds) { ?>
					<tr>
						<td><?php echo (int) ($j + 1); ?></td>
						<td><strong><?php echo epc_pbig_h($ds['name']); ?></strong><br><span class="text-muted"><?php echo epc_pbig_h($ds['description']); ?></span></td>
						<td><code style="word-break:break-all;font-size:11px;"><?php echo epc_pbig_h($ds['path']); ?></code></td>
						<td><?php echo epc_pbig_h(implode(', ', $ds['formats'])); ?></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
			<pre># Copy-paste starter for Power BI Desktop (CSV)
curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXX" \
  "https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv"</pre>
			<p class="text-muted" style="margin:10px 0 0">Full technical notes: <code>docs/POWER_BI.md</code> in the platform repo.</p>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Checklist</h4></div>
		<div class="panel-body">
			<ol>
				<li>☐ API key created with <code>read:bi</code> (or <code>read:erp</code> / <code>read:*</code>)</li>
				<li>☐ <code>/powerbi/catalog</code> returns <code>ok: true</code> with the key</li>
				<li>☐ Power BI Desktop loads at least KPIs CSV</li>
				<li>☐ Report published; scheduled refresh succeeds once</li>
				<li>☐ (Optional) Workspace / report IDs saved under Portal → Power BI</li>
				<li>☐ (Optional) Embed URL preview shows in CP</li>
			</ol>
			<p>
				<a class="btn btn-primary" href="<?php echo epc_pbig_h($configUrl); ?>"><i class="fa fa-arrow-right"></i> Go to Power BI settings</a>
			</p>
		</div>
	</div>
</div>
<?php
epc_cp_page_frame_close();
