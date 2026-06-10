<?php
/**
 * Super CP — demo sandbox tenants (extend / convert / delete).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';

function epc_demo_manage_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

if (!epc_portal_is_super_cp_host()) {
	echo '<div class="alert alert-warning">Demo tenants management is available on www.ecomae.com Super CP only.</div>';
	return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	global $DP_Config;
	echo '<div class="alert alert-warning">Please <a href="/' . epc_demo_manage_h((string) $DP_Config->backend_dir) . '/">log in to Super CP</a>.</div>';
	return;
}

global $db_link;
if (!isset($db_link) || !($db_link instanceof PDO)) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

$pdo = $db_link;
$flash = null;
$backend = (string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp');
$hubUrl = '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=demos';
$tccUrl = '/' . $backend . '/control/portal/epc_tenant_control_center';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
	$action = (string) ($_POST['epc_demo_action'] ?? '');
	$key = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_POST['site_key'] ?? '')));
	if ($action === 'extend' && $key !== '') {
		$flash = epc_portal_demo_extend($pdo, $key, 3);
	} elseif ($action === 'convert' && $key !== '') {
		$flash = epc_portal_demo_convert($pdo, $key);
	} elseif ($action === 'delete' && $key !== '') {
		$flash = epc_portal_demo_force_delete($pdo, $key);
	}
}

$demos = epc_portal_demo_list($pdo);
$active = epc_portal_demo_count_active($pdo);
$max = epc_portal_demo_max_active();
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260601demo">

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-body">
			<div style="background:linear-gradient(135deg,#4c1d95,#7c3aed);color:#fff;border-radius:12px;padding:20px;margin-bottom:18px">
				<h3 style="margin:0 0 8px;color:#fff"><i class="fa fa-flask"></i> Demo sandbox tenants</h3>
				<p style="margin:0;opacity:.92">AI-provisioned <?php echo (int) epc_portal_demo_days(); ?>-day demos on www.ecomae.com — isolated MySQL per tenant. Extend +3 days, convert to live, or force delete.</p>
			</div>

			<div class="row" style="margin-bottom:16px">
				<div class="col-sm-4"><div class="well well-sm text-center"><div class="text-muted small">Active demos</div><strong><?php echo (int) $active; ?> / <?php echo (int) $max; ?></strong></div></div>
				<div class="col-sm-4"><div class="well well-sm text-center"><div class="text-muted small">Listed</div><strong><?php echo count($demos); ?></strong></div></div>
				<div class="col-sm-4"><div class="well well-sm text-center"><div class="text-muted small">Expire cron</div><code style="font-size:11px">/epc-demo-expire-cron.php</code></div></div>
			</div>

			<?php if ($flash !== null): ?>
			<div class="alert alert-<?php echo !empty($flash['ok']) ? 'success' : 'danger'; ?>"><?php echo epc_demo_manage_h($flash['message'] ?? ''); ?></div>
			<?php endif; ?>

			<p>
				<a class="btn btn-default btn-sm" href="<?php echo epc_demo_manage_h($hubUrl); ?>"><i class="fa fa-sitemap"></i> Tenant hub</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_demo_manage_h($tccUrl); ?>"><i class="fa fa-sliders"></i> Tenant control center</a>
				<a class="btn btn-default btn-sm" href="https://www.ecomae.com/platform/demo" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Marketing demo wizard</a>
			</p>

			<p class="text-muted small">Demo CP scope: <code>https://www.ecomae.com/cp/demo/{site_key}/</code> — credentials from platform registry <code>operator_temp_password</code> (Super CP operators only).</p>

			<div class="table-responsive">
				<table class="table table-striped table-bordered" style="font-size:12px">
					<thead>
						<tr>
							<th>Company</th>
							<th>Industry</th>
							<th>Demo URL</th>
							<th>CP login</th>
							<th>Login credentials</th>
							<th>Created</th>
							<th>Expires</th>
							<th>Status</th>
							<th>Actions</th>
						</tr>
					</thead>
					<tbody>
					<?php if ($demos === array()): ?>
						<tr><td colspan="10" class="text-muted">No demo tenants yet. Prospects request via <a href="https://www.ecomae.com/platform/demo">/platform/demo</a>.</td></tr>
					<?php else: foreach ($demos as $d):
						$key = (string) $d['site_key'];
						$exp = (int) ($d['demo_expires_at'] ?? 0);
						$created = (int) ($d['created_at_ts'] ?? 0);
						$email = (string) ($d['admin_email'] ?? '');
						$pwd = (string) ($d['stored_password'] ?? '');
						$urls = is_array($d['urls'] ?? null) ? $d['urls'] : array();
						$storeUrl = (string) ($urls['storefront'] ?? '');
						$cpLoginUrl = 'https://www.ecomae.com' . (string) ($urls['cp_login'] ?? epc_portal_demo_cp_login_url($key));
						$cpScopedUrl = 'https://www.ecomae.com' . epc_portal_demo_cp_path_prefix() . $key . '/';
						$cpOpenUrl = (string) ($urls['cp_autologin'] ?? epc_portal_demo_cp_autologin_url($key));
						if ($cpOpenUrl === '' || $cpOpenUrl === $cpLoginUrl) {
							$cpOpenUrl = $cpScopedUrl;
						}
						$credLine = trim($email . ($email !== '' && $pwd !== '' ? ' / ' : '') . $pwd);
						$rowClass = '';
						if (!empty($d['expired'])) {
							$rowClass = 'danger';
						} elseif (!empty($d['suspended'])) {
							$rowClass = 'warning';
						}
						$statusLabel = 'Active';
						$statusClass = 'label-success';
						if (!empty($d['expired'])) {
							$statusLabel = 'Expired';
							$statusClass = 'label-danger';
						} elseif (!empty($d['suspended'])) {
							$statusLabel = 'Suspended';
							$statusClass = 'label-warning';
						}
						?>
						<tr class="<?php echo epc_demo_manage_h($rowClass); ?>">
							<td>
								<?php echo epc_demo_manage_h($d['trade_name'] ?? ''); ?>
								<br><code><?php echo epc_demo_manage_h($key); ?></code>
							</td>
							<td><?php echo epc_demo_manage_h($d['industry_code'] ?? ''); ?></td>
							<td style="max-width:180px;word-break:break-all">
								<?php if ($storeUrl !== ''): ?>
								<a href="<?php echo epc_demo_manage_h($storeUrl); ?>" target="_blank" rel="noopener"><?php echo epc_demo_manage_h($d['demo_hostname'] ?? $storeUrl); ?></a>
								<?php else: ?>
								<span class="text-muted">ERP-only</span>
								<?php endif; ?>
							</td>
							<td style="max-width:220px;word-break:break-all">
								<a href="<?php echo epc_demo_manage_h($cpScopedUrl); ?>" target="_blank" rel="noopener"><?php echo epc_demo_manage_h($cpScopedUrl); ?></a>
								<br><a class="btn btn-xs btn-default" style="margin-top:4px" target="_blank" rel="noopener" href="<?php echo epc_demo_manage_h($cpOpenUrl); ?>" title="Super CP auto-login into demo tenant CP"><i class="fa fa-sign-in"></i> Auto-login</a>
							</td>
							<td style="min-width:200px">
								<?php if ($email !== '' || $pwd !== ''): ?>
								<div style="margin-bottom:4px"><strong>User:</strong> <?php if ($email !== ''): ?><code><?php echo epc_demo_manage_h($email); ?></code><?php else: ?><span class="text-muted">—</span><?php endif; ?></div>
								<div><strong>Pass:</strong>
									<?php if ($pwd !== ''): ?>
									<code class="epc-demo-pwd-plain"><?php echo epc_demo_manage_h($pwd); ?></code>
									<button type="button" class="btn btn-xs btn-default epc-demo-copy" data-copy="<?php echo epc_demo_manage_h($pwd); ?>"><i class="fa fa-clipboard"></i> Copy pass</button>
									<?php if ($credLine !== ''): ?>
									<button type="button" class="btn btn-xs btn-link epc-demo-copy-all" data-copy="<?php echo epc_demo_manage_h($credLine); ?>">Copy all</button>
									<?php endif; ?>
									<?php else: ?>
									<span class="text-muted">Not stored</span>
									<a class="btn btn-xs btn-warning" href="<?php echo epc_demo_manage_h($tccUrl); ?>?site_key=<?php echo epc_demo_manage_h($key); ?>">Reset in TCC</a>
									<?php endif; ?>
								</div>
								<?php else: ?>
								<span class="text-muted">No credentials on file</span>
								<?php endif; ?>
							</td>
							<td><?php echo $created > 0 ? epc_demo_manage_h(date('Y-m-d H:i', $created)) : '—'; ?></td>
							<td>
								<?php echo $exp > 0 ? epc_demo_manage_h(date('Y-m-d H:i', $exp)) : '—'; ?>
								<br><small class="text-muted"><?php echo (int) ($d['days_left'] ?? 0); ?>d left</small>
							</td>
							<td><span class="label <?php echo epc_demo_manage_h($statusClass); ?>"><?php echo epc_demo_manage_h($statusLabel); ?></span></td>
							<td style="white-space:nowrap">
								<?php if ($storeUrl !== ''): ?>
								<a class="btn btn-xs btn-default" target="_blank" rel="noopener" href="<?php echo epc_demo_manage_h($storeUrl); ?>">Store</a>
								<?php endif; ?>
								<a class="btn btn-xs btn-primary" target="_blank" rel="noopener" href="<?php echo epc_demo_manage_h($cpOpenUrl); ?>" title="Open demo CP for <?php echo epc_demo_manage_h($key); ?>"><i class="fa fa-external-link"></i> Open CP</a>
								<form method="post" style="display:inline" onsubmit="return confirm('Extend +3 days?')">
									<input type="hidden" name="epc_demo_action" value="extend">
									<input type="hidden" name="site_key" value="<?php echo epc_demo_manage_h($key); ?>">
									<button type="submit" class="btn btn-xs btn-success">+3d</button>
								</form>
								<form method="post" style="display:inline" onsubmit="return confirm('Convert to live tenant?')">
									<input type="hidden" name="epc_demo_action" value="convert">
									<input type="hidden" name="site_key" value="<?php echo epc_demo_manage_h($key); ?>">
									<button type="submit" class="btn btn-xs btn-primary">Live</button>
								</form>
								<form method="post" style="display:inline" onsubmit="return confirm('Delete demo and drop DB?')">
									<input type="hidden" name="epc_demo_action" value="delete">
									<input type="hidden" name="site_key" value="<?php echo epc_demo_manage_h($key); ?>">
									<button type="submit" class="btn btn-xs btn-danger">Delete</button>
								</form>
							</td>
						</tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
</div>
<script src="/<?php echo epc_demo_manage_h($backend); ?>/content/control/portal/epc_demo_tenants_manage.js?v=20260607"></script>
