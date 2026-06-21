<?php
/**
 * Super CP — tenant hub (DNS-only tenants on ecomae platform).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

function epc_tenant_hub_render_main(): void
{
	global $db_link;
	global $DP_Config;
	global $user_session;

	if (!epc_portal_is_super_cp_host()) {
		echo '<div class="alert alert-warning">Tenant hub is available on the ecomae platform control panel only (<a href="https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard">www.ecomae.com/cp</a>).</div>';
		return;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (!DP_User::isAdmin()) {
		echo '<div class="alert alert-warning">Please <a href="/' . htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8') . '/">log in to Super CP</a> to use Tenant hub.</div>';
		return;
	}
	if (!isset($db_link) || !($db_link instanceof PDO)) {
		echo '<div class="alert alert-danger">Database unavailable.</div>';
		return;
	}

$backend = (string) $DP_Config->backend_dir;
$hubUrl = '/' . $backend . '/shop/tenant_hub/tenant_hub';
$portalSettings = '/' . $backend . '/control/portal/industry_settings';
$failoverGuide = '/' . $backend . '/control/portal/epc_platform_failover_guide';
$demoManageUrl = '/' . $backend . '/control/portal/epc_demo_tenants_manage';
$erpOnlyGuide = '/' . $backend . '/control/portal/epc_erp_only_onboard_guide';
$platformErpUrl = '/' . $backend . '/platform-erp/';
$marketingUrl = 'https://www.ecomae.com/';
$stats = epc_th_platform_stats($db_link);
$tenants = epc_th_list_tenants($db_link);
$tab = isset($_GET['tab']) ? (string) $_GET['tab'] : 'onboard';
$flash = null;
$launchChecklist = null;
$templates = epc_portal_tenant_templates();
$statuses = epc_portal_tenant_statuses();

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_th_intro'])) {
	$submittedBy = '';
	if (!empty($user_session['user_id'])) {
		$submittedBy = 'user:' . (int) $user_session['user_id'];
	}
	$result = epc_th_onboard_client($db_link, $_POST, $submittedBy);
	$flash = $result;
	if (!empty($result['checklist'])) {
		$launchChecklist = $result['checklist'];
	}
	$tenants = epc_th_list_tenants($db_link);
	$stats = epc_th_platform_stats($db_link);
	$tab = 'onboard';
}
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_th_add'])) {
	$result = epc_th_add_tenant($db_link, array(
		'site_key' => $_POST['site_key'] ?? '',
		'hostname' => $_POST['hostname'] ?? '',
		'industry_code' => $_POST['industry_code'] ?? 'auto_parts',
		'status' => $_POST['status'] ?? 'draft',
		'trade_name' => $_POST['trade_name'] ?? '',
		'hub_name' => $_POST['hub_name'] ?? '',
		'from_email' => $_POST['from_email'] ?? '',
		'db_name' => $_POST['db_name'] ?? '',
		'db_user' => $_POST['db_user'] ?? '',
		'db_password' => $_POST['db_password'] ?? '',
		'notes' => $_POST['notes'] ?? '',
	));
	$flash = $result;
	$tenants = epc_th_list_tenants($db_link);
	$stats = epc_th_platform_stats($db_link);
}
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_th_status'])) {
	$flash = epc_th_update_tenant_status($db_link, (string) ($_POST['site_key'] ?? ''), (string) ($_POST['status'] ?? ''));
	$tenants = epc_th_list_tenants($db_link);
	$stats = epc_th_platform_stats($db_link);
}

$dnsHost = isset($_GET['dns']) ? preg_replace('/[^a-z0-9.-]/', '', (string) $_GET['dns']) : '';
$dnsInfo = $dnsHost !== '' ? epc_portal_tenant_dns_instructions($dnsHost) : null;
$probe = null;
if ($tab === 'health' && isset($_GET['probe'])) {
	$probe = epc_th_probe_url('https://' . preg_replace('/[^a-z0-9.-]/', '', (string) $_GET['probe']) . '/');
}
?>

<?php
$epcThCssVer = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260606textfit1';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_professional_shell.php';
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=<?php echo epc_th_h($epcThCssVer); ?>">
<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_console.php';
$bocOperator = (class_exists('DP_User') && method_exists('DP_User', 'getName') && (string) DP_User::getName() !== '') ? (string) DP_User::getName() : 'Operator';
$bocOpId = (class_exists('DP_User') && method_exists('DP_User', 'getUserId')) ? (int) DP_User::getUserId() : 0;
$bocNav = function_exists('epc_boc_nav_for_user') ? epc_boc_nav_for_user($db_link, $bocOpId) : (function_exists('epc_boc_nav') ? epc_boc_nav() : array());
epc_boc_console_open(array('active' => 'tenant_hub', 'title' => 'Tenant hub / onboard', 'base' => '/' . trim($backend, '/'), 'operator' => $bocOperator, 'env' => 'Production', 'nav' => $bocNav, 'scope' => 'All units · Fleet'));
?>

<div class="col-lg-12 epc-erp-shell epc-th-shell">
	<div class="hpanel">
		<div class="panel-body">

			<div class="epc-th-hero">
				<span class="epc-th-hero__badge">Tenant hub</span>
				<h3><i class="fa fa-sitemap"></i> DNS-only multi-tenant platform</h3>
				<p class="epc-th-hero__sub">One codebase on <strong>www.ecomae.com</strong>. Clients keep GoDaddy domains — you only add an A record. No separate CloudPanel site per client.</p>
			</div>

			<div class="epc-th-kpi">
				<div class="epc-th-kpi__card epc-cp-card epc-cp-stat"><div class="epc-th-kpi__label">Registered</div><div class="epc-th-kpi__val" data-epc-stat><?php echo (int) $stats['tenants_total']; ?></div></div>
				<div class="epc-th-kpi__card epc-cp-card epc-cp-stat"><div class="epc-th-kpi__label">Live</div><div class="epc-th-kpi__val epc-th-kpi__val--success" data-epc-stat><?php echo (int) $stats['tenants_live']; ?></div></div>
				<div class="epc-th-kpi__card epc-cp-card epc-cp-stat"><div class="epc-th-kpi__label">Awaiting DNS</div><div class="epc-th-kpi__val epc-th-kpi__val--warn" data-epc-stat><?php echo (int) $stats['tenants_dns_pending']; ?></div></div>
				<div class="epc-th-kpi__card epc-th-kpi__card--mono epc-cp-card epc-cp-stat"><div class="epc-th-kpi__label">Platform IP</div><div class="epc-th-kpi__val epc-th-kpi__val--sm" data-epc-stat><?php echo epc_th_h($stats['platform_ip']); ?></div></div>
			</div>

			<div class="epc-th-tabs epc-cp-tabs--pill epc-th-tabs--compact">
				<a class="btn btn-sm <?php echo $tab === 'onboard' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard"><i class="fa fa-rocket"></i> Onboard client</a>
				<a class="btn btn-sm <?php echo $tab === 'tenants' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_th_h($hubUrl); ?>?tab=tenants">Tenants</a>
				<a class="btn btn-sm <?php echo $tab === 'dns' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_th_h($hubUrl); ?>?tab=dns">GoDaddy DNS</a>
				<a class="btn btn-sm <?php echo $tab === 'guide' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_th_h($hubUrl); ?>?tab=guide">Guide</a>
				<a class="btn btn-sm <?php echo $tab === 'health' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_th_h($hubUrl); ?>?tab=health">Health</a>
				<a class="btn btn-sm <?php echo $tab === 'demos' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_th_h($hubUrl); ?>?tab=demos"><i class="fa fa-flask"></i> Demos</a>
				<a class="btn btn-sm <?php echo $tab === 'social' ? 'btn-primary' : 'btn-default'; ?>" href="<?php echo epc_th_h($hubUrl); ?>?tab=social"><i class="fa fa-share-alt"></i> Social</a>
				<span class="epc-th-tabs__secondary">
					<a class="btn btn-sm btn-default" href="https://www.ecomae.com/platform/faq" target="_blank" rel="noopener"><i class="fa fa-question-circle"></i> Platform FAQ</a>
					<a class="btn btn-sm btn-default" href="<?php echo epc_th_h($portalSettings); ?>"><i class="fa fa-cog"></i> Settings</a>
					<a class="btn btn-sm btn-default" href="<?php echo epc_th_h($failoverGuide); ?>"><i class="fa fa-shield"></i> Failover</a>
				</span>
			</div>

			<?php if ($flash !== null): ?>
				<div class="alert alert-<?php echo !empty($flash['ok']) ? 'success' : 'danger'; ?>">
					<?php echo epc_th_h($flash['message'] ?? ''); ?>
					<?php if (!empty($flash['ok']) && (!empty($flash['erp_url']) || !empty($flash['cp_url']))): ?>
					<br><a class="btn btn-xs btn-info" style="margin-top:8px" target="_blank" href="<?php echo epc_th_h(!empty($flash['erp_url']) ? $flash['erp_url'] : $flash['cp_url']); ?>"><i class="fa fa-external-link"></i> Client ERP</a>
					<?php if (!empty($flash['erp_url'])): ?>
					<a class="btn btn-xs btn-success" style="margin-top:8px" target="_blank" href="<?php echo epc_th_h($flash['erp_url']); ?>"><i class="fa fa-university"></i> ERP shell</a>
					<?php endif; ?>
					<?php if (!empty($flash['erp_url'])): ?>
					<a class="btn btn-xs btn-success" style="margin-top:8px" target="_blank" href="<?php echo epc_th_h($flash['erp_url']); ?>"><i class="fa fa-university"></i> ERP shell</a>
					<?php endif; ?>
					<?php if (!empty($flash['site_key'])): ?>
					<a class="btn btn-xs btn-default" style="margin-top:8px" href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard&amp;checklist=<?php echo epc_th_h($flash['site_key']); ?>"><i class="fa fa-check-square-o"></i> Launch checklist</a>
					<?php endif; ?>
					<?php endif; ?>
				</div>
			<?php endif; ?>

			<?php if ($tab === 'onboard'): ?>
				<?php include $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_onboard_panel.php'; ?>

			<?php elseif ($tab === 'demos'): ?>
				<?php
				$demoManagePhp = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/control/portal/epc_demo_tenants_manage.php';
				if (is_file($demoManagePhp)) {
					include $demoManagePhp;
				} else {
					echo '<div class="alert alert-info">Open <a href="' . epc_th_h($demoManageUrl) . '">Demo tenants</a> in Portal menu.</div>';
				}
				?>

			<?php elseif ($tab === 'tenants'): ?>

				<div class="panel panel-default epc-th-panel">
					<div class="panel-heading"><i class="fa fa-plus"></i> Quick register (advanced — use <a href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard">Onboard client</a> for full intro)</div>
					<div class="panel-body">
						<form method="post" class="epc-th-register-form">
							<input type="hidden" name="epc_th_add" value="1">
							<div class="row">
								<div class="col-md-3 form-group">
									<label>Quick template</label>
									<select class="form-control" id="epc_th_template" onchange="epcThApplyTemplate(this)">
										<option value="">— custom —</option>
										<?php foreach ($templates as $tk => $tpl): ?>
										<option value="<?php echo epc_th_h($tk); ?>"><?php echo epc_th_h($tpl['label']); ?></option>
										<?php endforeach; ?>
									</select>
								</div>
								<div class="col-md-2 form-group">
									<label>Site key</label>
									<input class="form-control" name="site_key" id="epc_th_site_key" placeholder="epartscart" required>
								</div>
								<div class="col-md-3 form-group">
									<label>Hostname</label>
									<input class="form-control" name="hostname" id="epc_th_hostname" placeholder="www.client.com" required>
								</div>
								<div class="col-md-2 form-group">
									<label>Industry</label>
									<select class="form-control" name="industry_code" id="epc_th_industry">
										<?php
										$industryGroups = epc_portal_industries_grouped(epc_portal_settings_industries());
										foreach ($industryGroups as $grp) {
											if (empty($grp['industries'])) {
												continue;
											}
											?>
										<optgroup label="<?php echo epc_th_h($grp['name']); ?>">
											<?php foreach ($grp['industries'] as $ind) { ?>
											<option value="<?php echo epc_th_h($ind['code']); ?>"><?php echo epc_th_h($ind['name']); ?></option>
											<?php } ?>
										</optgroup>
										<?php } ?>
									</select>
								</div>
								<div class="col-md-2 form-group">
									<label>Status</label>
									<select class="form-control" name="status">
										<?php foreach ($statuses as $sk => $sl): ?>
										<option value="<?php echo epc_th_h($sk); ?>"<?php echo $sk === 'draft' ? ' selected' : ''; ?>><?php echo epc_th_h($sl); ?></option>
										<?php endforeach; ?>
									</select>
								</div>
							</div>
							<div class="row">
								<div class="col-md-3 form-group">
									<label>Trade name</label>
									<input class="form-control" name="trade_name" id="epc_th_trade">
								</div>
								<div class="col-md-3 form-group">
									<label>DB name</label>
									<input class="form-control" name="db_name" id="epc_th_db" placeholder="tenant_db">
								</div>
								<div class="col-md-3 form-group">
									<label>DB user</label>
									<input class="form-control" name="db_user" placeholder="same as db name">
								</div>
								<div class="col-md-3 form-group">
									<label>DB password</label>
									<input class="form-control" name="db_password" type="password" autocomplete="new-password">
								</div>
							</div>
							<button type="submit" class="btn btn-success"><i class="fa fa-save"></i> Save tenant</button>
						</form>
						<p class="text-muted small" style="margin:12px 0 0">Register here first. When ready, set status to <em>Awaiting DNS</em>, configure GoDaddy, then set <em>Live</em>.</p>
					</div>
				</div>

				<script>
				var epcThTemplates = <?php echo json_encode($templates); ?>;
				function epcThApplyTemplate(sel) {
					var t = epcThTemplates[sel.value];
					if (!t) return;
					document.getElementById('epc_th_site_key').value = sel.value;
					document.getElementById('epc_th_hostname').value = t.hostname || '';
					document.getElementById('epc_th_industry').value = t.industry || 'auto_parts';
					document.getElementById('epc_th_trade').value = t.trade_name || '';
					document.getElementById('epc_th_db').value = sel.value.replace(/[^a-z0-9_]/g, '');
				}
				</script>

				<div class="table-responsive epc-th-table-wrap">
				<table class="table table-striped table-hover epc-th-table">
					<thead>
						<tr>
							<th>Key</th>
							<th>Hostname</th>
							<th>Industry</th>
							<th>Status</th>
							<th>DB</th>
							<th>Intro</th>
							<th>Actions</th>
						</tr>
					</thead>
					<tbody>
					<?php if (count($tenants) === 0): ?>
						<tr><td colspan="7" class="text-muted">No tenants yet — use <a href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard">Onboard client</a> to register the first client.</td></tr>
					<?php endif; ?>
					<?php foreach ($tenants as $t):
						$badge = 'epc-th-badge-draft';
						if ($t['status'] === 'live') {
							$badge = 'epc-th-badge-live';
						} elseif ($t['status'] === 'dns_pending') {
							$badge = 'epc-th-badge-dns';
						} ?>
						<tr>
							<td class="epc-th-table__key"><code><?php echo epc_th_h($t['site_key']); ?></code><?php if (!empty($t['is_demo_tenant'])): ?> <span class="label label-info">demo</span><?php endif; ?></td>
							<td class="epc-th-table__host"><strong><?php echo epc_th_h($t['hostname']); ?></strong></td>
							<td class="epc-th-table__industry">
								<?php echo epc_th_h($t['industry_name']); ?>
								<?php if (!empty($t['ecosystem_name'])) { ?>
								<br><small class="text-muted"><?php echo epc_th_h($t['ecosystem_name']); ?></small>
								<?php } ?>
							</td>
							<td><span class="label <?php echo epc_th_h($badge); ?>"><?php echo epc_th_h($t['status_label']); ?></span></td>
							<td class="epc-th-table__db"><code style="color:<?php echo !empty($t['db_connect_ok']) ? '#166534' : '#dc2626'; ?>"><?php echo epc_th_h($t['db_name']); ?></code><?php if (empty($t['db_connect_ok'])): ?><br><small class="text-danger">DB connect failed</small><?php endif; ?></td>
							<td><?php if ($t['intro_done']): ?><span class="label label-success">Done</span><?php else: ?><a href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard&amp;edit=<?php echo epc_th_h($t['site_key']); ?>">Add intro</a><?php endif; ?></td>
							<td class="epc-th-table__actions">
								<div class="epc-th-table__actions-inner">
								<a class="btn btn-xs btn-default" href="<?php echo epc_th_h($hubUrl); ?>?tab=dns&amp;dns=<?php echo epc_th_h($t['hostname']); ?>">DNS</a>
								<?php if ($t['status'] === 'live'): ?>
								<?php if (!empty($t['storefront_url'])): ?>
								<a class="btn btn-xs btn-default" target="_blank" href="<?php echo epc_th_h($t['storefront_url']); ?>">Store</a>
								<?php endif; ?>
								<?php if (!empty($t['erp_url'])): ?>
								<a class="btn btn-xs btn-info" target="_blank" href="<?php echo epc_th_h($t['erp_url']); ?>">ERP</a>
								<?php endif; ?>
								<?php if (!empty($t['cp_url'])): ?>
								<a class="btn btn-xs btn-primary" target="_blank" href="<?php echo epc_th_h($t['cp_url']); ?>">CP</a>
								<?php endif; ?>
								<?php endif; ?>
								<form method="post">
									<input type="hidden" name="epc_th_status" value="1">
									<input type="hidden" name="site_key" value="<?php echo epc_th_h($t['site_key']); ?>">
									<select name="status" class="input-sm form-control" onchange="this.form.submit()" title="Change status">
										<?php foreach ($statuses as $sk => $sl): ?>
										<option value="<?php echo epc_th_h($sk); ?>"<?php echo $t['status'] === $sk ? ' selected' : ''; ?>><?php echo epc_th_h($sk); ?></option>
										<?php endforeach; ?>
									</select>
								</form>
								</div>
							</td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
				</div>

			<?php elseif ($tab === 'dns'): ?>
				<h4><i class="fa fa-globe"></i> GoDaddy DNS — link client domain to ecomae</h4>
				<p>Clients keep the domain at GoDaddy. You do <strong>not</strong> create a separate CloudPanel site per client — only add DNS + a domain alias on <code>www.ecomae.com</code>.</p>
				<div class="well">
					<strong>Platform IP (A record target):</strong> <code><?php echo epc_th_h($stats['platform_ip']); ?></code>
				</div>
				<?php if ($dnsInfo !== null): ?>
					<h5>Steps for <code><?php echo epc_th_h($dnsInfo['hostname']); ?></code></h5>
					<ol>
						<?php foreach ($dnsInfo['steps'] as $step): ?>
						<li><?php echo epc_th_h($step); ?></li>
						<?php endforeach; ?>
					</ol>
				<?php else: ?>
					<p class="text-muted">Pick a tenant → DNS, or select hostname:</p>
					<ul>
						<?php foreach ($tenants as $t): ?>
						<li><a href="<?php echo epc_th_h($hubUrl); ?>?tab=dns&amp;dns=<?php echo epc_th_h($t['hostname']); ?>"><?php echo epc_th_h($t['hostname']); ?></a></li>
						<?php endforeach; ?>
					</ul>
				<?php endif; ?>

			<?php elseif ($tab === 'social'): ?>
				<?php
				$panelFile = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/control/portal/epc_social_media_hub_panel.php';
				$cssVer = function_exists('epc_cp_page_asset_version') ? epc_cp_page_asset_version() : '20260608social1';
				if (is_file($panelFile)) {
					echo '<link rel="stylesheet" href="/content/general_pages/epc_social_media_hub_css.php?v=' . epc_th_h($cssVer) . '">';
					echo '<script src="/content/general_pages/epc_social_media_hub_config.php?v=' . epc_th_h($cssVer) . '"></script>';
					echo '<script src="/content/general_pages/epc_social_media_hub_js.php?v=' . epc_th_h($cssVer) . '"></script>';
					require_once $panelFile;
					epc_social_media_render_hub(array('is_super' => true, 'embed_tenant_hub' => true));
				} else {
					echo '<div class="alert alert-info">Open <a href="/' . epc_th_h($backend) . '/control/portal/epc_social_media_hub">Social media hub</a> in the Portal menu.</div>';
				}
				?>

			<?php elseif ($tab === 'health'): ?>
				<?php if ($probe !== null && isset($_GET['probe'])): ?>
					<div class="alert alert-<?php echo $probe['ok'] ? 'success' : 'warning'; ?>">
						<strong><?php echo epc_th_h((string) $_GET['probe']); ?></strong> —
						HTTP <?php echo (int) $probe['http_code']; ?> · <?php echo (int) $probe['ms']; ?> ms
					</div>
				<?php endif; ?>
				<p>Probe live tenants only (status = Live).</p>
				<ul>
					<?php foreach ($tenants as $t):
						if ($t['status'] !== 'live') {
							continue;
						}
						$p = epc_th_probe_url($t['storefront_url']); ?>
					<li><strong><?php echo epc_th_h($t['hostname']); ?></strong> — HTTP <?php echo (int) $p['http_code']; ?> (<?php echo (int) $p['ms']; ?> ms)</li>
					<?php endforeach; ?>
				</ul>

			<?php else: ?>
				<h4><i class="fa fa-book"></i> Platform operator guide</h4>
				<div class="alert alert-info">
					<strong>Fast path:</strong> <a href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard">Onboard client</a> — one intro form registers the tenant and seeds portal settings so the client CP can go live as soon as DNS is ready.
				</div>
				<?php foreach (epc_portal_onboard_guide_steps() as $i => $step): ?>
				<div class="epc-th-step" style="border-left:4px solid #0ea5e9;padding:12px 16px;margin:12px 0;background:#f8fafc;border-radius:0 6px 6px 0">
					<h5 style="margin:0 0 6px;font-weight:700">Step <?php echo (int) ($i + 1); ?> — <?php echo epc_th_h($step['title']); ?></h5>
					<div><?php echo $step['body']; ?></div>
				</div>
				<?php endforeach; ?>
				<p class="text-muted">Do not host clients as separate CloudPanel sites. Remove old sites after DNS cutover to free disk.</p>
			<?php endif; ?>

		</div>
	</div>
</div>
<?php
epc_boc_console_close();
}

epc_tenant_hub_render_main();
