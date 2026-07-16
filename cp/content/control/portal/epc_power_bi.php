<?php
/**
 * CP — Power BI connector & workspace config.
 * Super CP: any tenant. Tenant CP: current site_key only.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_power_bi.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

function epc_pbi_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$isSuper = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');

$pdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Platform database unavailable.</div>';
	return;
}
epc_power_bi_ensure_schema($pdo);

$tenants = function_exists('epc_portal_list_tenants') ? epc_portal_list_tenants($pdo) : array();
$siteKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($_GET['site_key'] ?? $_POST['site_key'] ?? '')));
if ($siteKey === '' && !$isSuper) {
	$host = function_exists('epc_portal_host') ? epc_portal_host() : '';
	foreach ($tenants as $t) {
		$h = preg_replace('/^www\./', '', strtolower((string) ($t['hostname'] ?? '')));
		$hh = preg_replace('/^www\./', '', strtolower($host));
		if ($h !== '' && $h === $hh) {
			$siteKey = (string) ($t['site_key'] ?? '');
			break;
		}
	}
}
if ($siteKey === '' && $tenants) {
	$siteKey = (string) ($tenants[0]['site_key'] ?? 'epartscart');
}

$flash = '';
$flashType = 'info';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_pbi_action'])) {
	$action = (string) $_POST['epc_pbi_action'];
	$postKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($_POST['site_key'] ?? $siteKey)));
	if (!$isSuper && $postKey !== $siteKey) {
		$flash = 'Not allowed to edit another tenant.';
		$flashType = 'danger';
	} elseif ($action === 'save_config') {
		$res = epc_power_bi_configure($pdo, $postKey, array(
			'workspace_id' => (string) ($_POST['workspace_id'] ?? ''),
			'azure_tenant_id' => (string) ($_POST['azure_tenant_id'] ?? ''),
			'default_report_id' => (string) ($_POST['default_report_id'] ?? ''),
			'default_dataset_id' => (string) ($_POST['default_dataset_id'] ?? ''),
			'embed_url' => (string) ($_POST['embed_url'] ?? ''),
			'embed_mode' => (string) ($_POST['embed_mode'] ?? 'none'),
			'notes' => (string) ($_POST['notes'] ?? ''),
		));
		$siteKey = $postKey;
		$flash = !empty($res['ok']) ? 'Power BI config saved for ' . $postKey . '.' : 'Save failed.';
		$flashType = !empty($res['ok']) ? 'success' : 'danger';
	} elseif ($action === 'add_report') {
		epc_power_bi_register_report($pdo, $postKey, array(
			'report_id' => (string) ($_POST['report_id'] ?? ''),
			'report_name' => (string) ($_POST['report_name'] ?? 'Report'),
			'dataset_id' => (string) ($_POST['dataset_id'] ?? ''),
			'category' => (string) ($_POST['category'] ?? 'finance'),
			'embed_url' => (string) ($_POST['report_embed_url'] ?? ''),
		));
		$siteKey = $postKey;
		$flash = 'Report registered.';
		$flashType = 'success';
	}
}

$config = epc_power_bi_config_get($pdo, $siteKey) ?: array(
	'workspace_id' => '',
	'azure_tenant_id' => '',
	'default_report_id' => '',
	'default_dataset_id' => '',
	'embed_url' => '',
	'embed_mode' => 'none',
	'notes' => '',
	'active' => 0,
);
$reports = epc_power_bi_reports_list($pdo, $siteKey);
$caps = epc_power_bi_capabilities();
$datasets = epc_power_bi_dataset_catalog('https://www.ecomae.com');
$embed = epc_power_bi_embed_resolve($pdo, $siteKey);

epc_cp_page_frame_open(array('class' => 'epc-power-bi'));
?>
<div class="epc-portal-settings epc-power-bi">
	<div class="hero">
		<h2><i class="fa fa-bar-chart"></i> Power BI</h2>
		<p style="margin:0;opacity:.92">
			Connect Microsoft Power BI to live tenant ERP/commerce data via API key — no Azure AD required for Desktop refresh.
			Secure embed tokens wait until you supply Azure credentials.
		</p>
	</div>

	<?php if ($flash !== '') { ?>
	<div class="alert alert-<?php echo epc_pbi_h($flashType); ?>"><?php echo epc_pbi_h($flash); ?></div>
	<?php } ?>

	<div class="hpanel">
		<div class="panel-heading"><h4>What is possible now</h4></div>
		<div class="panel-body">
			<div class="row">
				<div class="col-md-6">
					<h5>Available without Microsoft login</h5>
					<ul>
						<?php foreach ($caps['available_now'] as $k => $v) {
							if (!$v) {
								continue;
							} ?>
						<li><code><?php echo epc_pbi_h($k); ?></code></li>
						<?php } ?>
					</ul>
				</div>
				<div class="col-md-6">
					<h5>Needs your Azure / Power BI Pro</h5>
					<ul>
						<?php foreach ($caps['needs_customer_credentials'] as $k => $why) { ?>
						<li><strong><?php echo epc_pbi_h($k); ?></strong> — <?php echo epc_pbi_h($why); ?></li>
						<?php } ?>
					</ul>
				</div>
			</div>
			<ol>
				<?php foreach ($caps['connect_guide'] as $step) { ?>
				<li><?php echo epc_pbi_h($step); ?></li>
				<?php } ?>
			</ol>
			<p>
				<a class="btn btn-default btn-sm" href="/<?php echo $backend; ?>/control/portal/epc_api_documentation_guide"><i class="fa fa-key"></i> API keys &amp; scopes</a>
				<a class="btn btn-default btn-sm" href="https://www.ecomae.com/epc-api/v1/powerbi/catalog" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Dataset catalog (needs key)</a>
			</p>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Power BI datasets (Web connector)</h4></div>
		<div class="panel-body">
			<table class="table table-striped">
				<thead>
					<tr><th>Dataset</th><th>URL</th><th>Formats</th><th>Scope</th></tr>
				</thead>
				<tbody>
				<?php foreach ($datasets as $ds) { ?>
					<tr>
						<td><strong><?php echo epc_pbi_h($ds['name']); ?></strong><br><span class="text-muted"><?php echo epc_pbi_h($ds['description']); ?></span></td>
						<td><code style="word-break:break-all;font-size:11px;"><?php echo epc_pbi_h($ds['path']); ?></code></td>
						<td><?php echo epc_pbi_h(implode(', ', $ds['formats'])); ?></td>
						<td><code><?php echo epc_pbi_h($ds['scope']); ?></code></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
			<pre style="background:#f8fafc;border:1px solid #e2e8f0;padding:12px;border-radius:6px;font-size:12px;"># Example — KPI CSV for Power BI Desktop
curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXX" \
  "https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv"</pre>
		</div>
	</div>

	<?php if ($isSuper && $tenants) { ?>
	<form method="get" class="form-inline" style="margin-bottom:12px;">
		<label>Tenant</label>
		<select name="site_key" class="form-control input-sm" onchange="this.form.submit()">
			<?php foreach ($tenants as $t) {
				$sk = (string) ($t['site_key'] ?? '');
				if ($sk === '') {
					continue;
				} ?>
			<option value="<?php echo epc_pbi_h($sk); ?>"<?php echo $sk === $siteKey ? ' selected' : ''; ?>>
				<?php echo epc_pbi_h(($t['trade_name'] ?? $sk) . ' (' . $sk . ')'); ?>
			</option>
			<?php } ?>
		</select>
	</form>
	<?php } ?>

	<div class="hpanel">
		<div class="panel-heading"><h4>Workspace config — <?php echo epc_pbi_h($siteKey); ?></h4></div>
		<div class="panel-body">
			<form method="post" class="form-horizontal">
				<input type="hidden" name="epc_pbi_action" value="save_config">
				<input type="hidden" name="site_key" value="<?php echo epc_pbi_h($siteKey); ?>">
				<div class="form-group">
					<label class="col-sm-3 control-label">Workspace ID</label>
					<div class="col-sm-9"><input class="form-control" name="workspace_id" value="<?php echo epc_pbi_h($config['workspace_id'] ?? ''); ?>" placeholder="Power BI workspace GUID"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Azure tenant ID</label>
					<div class="col-sm-9"><input class="form-control" name="azure_tenant_id" value="<?php echo epc_pbi_h($config['azure_tenant_id'] ?? ''); ?>" placeholder="Optional — for future embed tokens"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Default report ID</label>
					<div class="col-sm-9"><input class="form-control" name="default_report_id" value="<?php echo epc_pbi_h($config['default_report_id'] ?? ''); ?>"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Default dataset ID</label>
					<div class="col-sm-9"><input class="form-control" name="default_dataset_id" value="<?php echo epc_pbi_h($config['default_dataset_id'] ?? ''); ?>"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Embed mode</label>
					<div class="col-sm-9">
						<select class="form-control" name="embed_mode">
							<?php foreach (array('none' => 'None (API datasets only)', 'url' => 'URL iframe (publish-to-web / share link)', 'azure' => 'Azure embed (needs credentials)') as $val => $label) { ?>
							<option value="<?php echo epc_pbi_h($val); ?>"<?php echo (($config['embed_mode'] ?? '') === $val) ? ' selected' : ''; ?>><?php echo epc_pbi_h($label); ?></option>
							<?php } ?>
						</select>
					</div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Embed URL</label>
					<div class="col-sm-9"><input class="form-control" name="embed_url" value="<?php echo epc_pbi_h($config['embed_url'] ?? ''); ?>" placeholder="https://app.powerbi.com/view?r=..."></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Notes</label>
					<div class="col-sm-9"><input class="form-control" name="notes" value="<?php echo epc_pbi_h($config['notes'] ?? ''); ?>"></div>
				</div>
				<div class="form-group">
					<div class="col-sm-offset-3 col-sm-9">
						<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save config</button>
					</div>
				</div>
			</form>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Registered reports</h4></div>
		<div class="panel-body">
			<?php if (!$reports) { ?>
			<p class="text-muted">No reports registered yet.</p>
			<?php } else { ?>
			<table class="table">
				<thead><tr><th>Name</th><th>Report ID</th><th>Category</th><th>Embed</th></tr></thead>
				<tbody>
				<?php foreach ($reports as $r) { ?>
					<tr>
						<td><?php echo epc_pbi_h($r['report_name'] ?? ''); ?></td>
						<td><code><?php echo epc_pbi_h($r['report_id'] ?? ''); ?></code></td>
						<td><?php echo epc_pbi_h($r['category'] ?? ''); ?></td>
						<td><?php echo !empty($r['embed_url']) ? 'yes' : '—'; ?></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
			<?php } ?>

			<form method="post" class="form-horizontal" style="margin-top:16px;">
				<input type="hidden" name="epc_pbi_action" value="add_report">
				<input type="hidden" name="site_key" value="<?php echo epc_pbi_h($siteKey); ?>">
				<div class="form-group">
					<label class="col-sm-3 control-label">Report name</label>
					<div class="col-sm-9"><input class="form-control" name="report_name" required placeholder="Executive finance"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Report ID</label>
					<div class="col-sm-9"><input class="form-control" name="report_id" placeholder="GUID from Power BI"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Dataset ID</label>
					<div class="col-sm-9"><input class="form-control" name="dataset_id"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Category</label>
					<div class="col-sm-9"><input class="form-control" name="category" value="finance"></div>
				</div>
				<div class="form-group">
					<label class="col-sm-3 control-label">Report embed URL</label>
					<div class="col-sm-9"><input class="form-control" name="report_embed_url" placeholder="Optional https://app.powerbi.com/..."></div>
				</div>
				<div class="form-group">
					<div class="col-sm-offset-3 col-sm-9">
						<button type="submit" class="btn btn-default"><i class="fa fa-plus"></i> Add report</button>
					</div>
				</div>
			</form>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Embed preview</h4></div>
		<div class="panel-body">
			<?php if (!empty($embed['ok']) && !empty($embed['url'])) { ?>
			<iframe src="<?php echo epc_pbi_h($embed['url']); ?>" title="Power BI" style="width:100%;min-height:520px;border:1px solid #e2e8f0;border-radius:8px;" allowfullscreen></iframe>
			<?php } else { ?>
			<div class="alert alert-info" style="margin:0;">
				<strong><?php echo epc_pbi_h($embed['phase'] ?? 'not_ready'); ?></strong> —
				<?php echo epc_pbi_h($embed['error'] ?? 'Save an embed URL or connect Desktop to the datasets above.'); ?>
			</div>
			<?php } ?>
		</div>
	</div>
</div>
<?php
epc_cp_page_frame_close();
