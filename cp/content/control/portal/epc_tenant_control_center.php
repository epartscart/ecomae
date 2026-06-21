<?php
/**
 * Super CP — Tenant control center (all tenants, credentials, on/off toggle).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_hub_helpers.php';

function epc_tcc_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

if (!epc_portal_is_super_cp_host()) {
	echo '<div class="alert alert-warning">Tenant control is available on www.ecomae.com Super CP only.</div>';
	return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	global $DP_Config;
	echo '<div class="alert alert-warning">Please <a href="/' . epc_tcc_h((string) $DP_Config->backend_dir) . '/">log in to Super CP</a>.</div>';
	return;
}

global $db_link;
if (!isset($db_link) || !($db_link instanceof PDO)) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

$pdo = $db_link;
epc_portal_tenant_control_ensure_schema($pdo);
$tenants = epc_portal_tenant_control_list_all($pdo);
$backend = (string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp');
$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_portal.php';
$hubUrl = '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=tenants';
$demoUrl = '/' . $backend . '/control/portal/epc_demo_tenants_manage';
$authSettingsUrl = '/' . $backend . '/control/portal/epc_cp_auth_settings';
$activeCount = 0;
$registryCount = 0;
$registryTenants = array();
foreach ($tenants as $t) {
	if (!empty($t['in_registry'])) {
		$registryCount++;
		$registryTenants[] = array(
			'site_key' => (string) ($t['site_key'] ?? ''),
			'label' => (string) ($t['trade_name'] ?? $t['site_key'] ?? ''),
			'type' => (string) ($t['type_label'] ?? ''),
			'is_demo' => !empty($t['is_demo']) ? 1 : 0,
		);
	}
	if (!empty($t['is_active_flag']) && empty($t['access_blocked'])) {
		$activeCount++;
	}
}
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260601tcc">
<style>
#epc-tenant-control-center .epc-tcc-pwd-plain { display: none; font-family: monospace; font-size: 12px; color: #0f172a; background: #f8fafc; padding: 1px 4px; border-radius: 3px; }
#epc-tenant-control-center .epc-tcc-pwd-cell.is-revealed .epc-tcc-pwd-plain { display: inline; }
#epc-tenant-control-center .epc-tcc-pwd-cell.is-revealed .epc-tcc-pwd-mask { display: none; }
</style>

<div class="col-lg-12 epc-erp-shell" id="epc-tenant-control-center" data-ajax-url="<?php echo epc_tcc_h($ajaxUrl); ?>">
	<div class="hpanel">
		<div class="panel-body">
			<div style="background:linear-gradient(135deg,#0c4a6e,#0369a1);color:#fff;border-radius:12px;padding:20px;margin-bottom:18px">
				<h3 style="margin:0 0 8px;color:#fff"><i class="fa fa-sliders"></i> Tenant control center</h3>
				<p style="margin:0;opacity:.92">All commerce, demo, and ERP-only tenants — operator credentials and immediate on/off. Internal Super CP only.</p>
			</div>

			<div class="row" style="margin-bottom:16px">
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Listed</div><strong><?php echo count($tenants); ?></strong></div></div>
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">In registry</div><strong><?php echo (int) $registryCount; ?></strong></div></div>
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Accessible</div><strong><?php echo (int) $activeCount; ?></strong></div></div>
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Platform DB</div><code style="font-size:11px">ecomae</code></div></div>
			</div>

			<div id="epc-tcc-flash" class="alert" style="display:none"></div>

			<p>
				<a class="btn btn-default btn-sm" href="<?php echo epc_tcc_h($hubUrl); ?>"><i class="fa fa-sitemap"></i> Tenant hub</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_tcc_h($demoUrl); ?>"><i class="fa fa-flask"></i> Demo tenants</a>
				<a class="btn btn-default btn-sm" href="<?php echo epc_tcc_h($authSettingsUrl); ?>"><i class="fa fa-sign-in"></i> Modern auth</a>
				<a class="btn btn-default btn-sm" href="/<?php echo epc_tcc_h($backend); ?>/control/portal/industry_settings"><i class="fa fa-cubes"></i> ERP module packs</a>
			</p>
			<p class="text-muted small" style="margin-top:-6px">ERP-only tenants: configure granular modules in <strong>Portal → Industry settings</strong> (presets: Full ERP, HR only, Customs + Logistics). Push to client DB via <em>Also apply to tenant hostname</em>. Probe: <code>/epc-erp-modules-probe.php?token=…&amp;site_key=…</code></p>

			<div class="panel panel-default" id="epc-demo-access-panel" style="margin-bottom:20px;border-color:#0ea5e9">
				<div class="panel-heading" style="background:#f0f9ff">
					<strong><i class="fa fa-key"></i> Demo access control</strong>
					<span class="text-muted small"> — CP login &amp; ERP demo credentials (registry tenants)</span>
				</div>
				<div class="panel-body">
					<div class="row">
						<div class="col-md-4">
							<label class="control-label">Tenant</label>
							<select class="form-control" id="epc-dac-tenant">
								<option value="">— Select tenant —</option>
								<?php foreach ($registryTenants as $rt): ?>
								<option value="<?php echo epc_tcc_h($rt['site_key']); ?>" data-demo="<?php echo (int) $rt['is_demo']; ?>">
									<?php echo epc_tcc_h($rt['label']); ?> (<?php echo epc_tcc_h($rt['site_key']); ?>) — <?php echo epc_tcc_h($rt['type']); ?>
								</option>
								<?php endforeach; ?>
							</select>
						</div>
						<div class="col-md-4">
							<label class="control-label">CP login email</label>
							<input type="email" class="form-control" id="epc-dac-email" placeholder="operator@example.com" autocomplete="off">
						</div>
						<div class="col-md-4">
							<label class="control-label">New password <span class="text-muted">(optional on save)</span></label>
							<input type="text" class="form-control" id="epc-dac-password" placeholder="Leave blank to keep current" autocomplete="new-password">
						</div>
					</div>
					<div class="row" style="margin-top:12px">
						<div class="col-md-4">
							<label class="checkbox" style="font-weight:normal;margin-top:8px">
								<input type="checkbox" id="epc-dac-is-demo"> Mark as <strong>demo</strong> tenant (sandbox)
							</label>
						</div>
						<div class="col-md-8 text-right" style="margin-top:4px">
							<button type="button" class="btn btn-default" id="epc-dac-load"><i class="fa fa-refresh"></i> Load users</button>
							<button type="button" class="btn btn-warning" id="epc-dac-reset"><i class="fa fa-random"></i> Generate password</button>
							<button type="button" class="btn btn-primary" id="epc-dac-save"><i class="fa fa-save"></i> Save</button>
						</div>
					</div>
					<div id="epc-dac-meta" class="text-muted small" style="margin-top:10px;display:none"></div>
					<div id="epc-dac-pwd-once" class="alert alert-success" style="display:none;margin-top:10px"></div>
					<div id="epc-dac-users-wrap" style="display:none;margin-top:12px">
						<strong>CP backend users</strong>
						<table class="table table-condensed table-bordered" style="margin-top:6px;font-size:12px">
							<thead><tr><th>ID</th><th>Email</th><th>Groups</th><th>Unlocked</th></tr></thead>
							<tbody id="epc-dac-users"></tbody>
						</table>
					</div>
					<p class="text-muted small" style="margin:10px 0 0">Passwords use tenant CP auth (<code>md5(password + secret)</code>). Generated passwords show once here; also stored in registry <code>operator_temp_password</code> for operator reference. Changes are audit-logged on the platform DB.</p>
				</div>
			</div>

			<div class="table-responsive">
				<table class="table table-striped table-bordered table-condensed" style="font-size:13px">
					<thead>
						<tr>
							<th>Type</th>
							<th>Site key</th>
							<th>Hostname</th>
							<th>DB</th>
							<th>ERP pack</th>
							<th>Status</th>
							<th>On</th>
							<th>Login email</th>
							<th>ERP login URL</th>
							<th>Password</th>
							<th>Links</th>
						</tr>
					</thead>
					<tbody>
					<?php foreach ($tenants as $t):
						$key = (string) ($t['site_key'] ?? '');
						$type = (string) ($t['tenant_type'] ?? 'commerce');
						$badge = epc_portal_tenant_control_type_badge_class($type);
						$inReg = !empty($t['in_registry']);
						$isOn = !empty($t['is_active_flag']);
						$rowClass = !empty($t['access_blocked']) ? 'warning' : '';
						$hasPwd = trim((string) ($t['stored_password'] ?? '')) !== '';
						$urls = is_array($t['urls'] ?? null) ? $t['urls'] : array();
						?>
						<tr class="<?php echo epc_tcc_h($rowClass); ?>" data-site-key="<?php echo epc_tcc_h($key); ?>">
							<td><span class="label <?php echo epc_tcc_h($badge); ?>"><?php echo epc_tcc_h($t['type_label'] ?? ''); ?></span></td>
							<td><code><?php echo epc_tcc_h($key); ?></code><?php if (!$inReg): ?> <span class="text-muted" title="Template only">*</span><?php endif; ?></td>
							<td><?php echo epc_tcc_h($t['hostname'] ?? ''); ?></td>
							<td><code><?php echo epc_tcc_h($t['db_name'] ?? ''); ?></code></td>
							<td>
								<?php
								$packLabel = (string) ($t['erp_pack_label'] ?? '');
								if ($packLabel !== ''): ?>
								<span class="label label-info" title="<?php echo epc_tcc_h((string) ($t['erp_pack_id'] ?? '')); ?>"><?php echo epc_tcc_h($packLabel); ?></span>
								<?php elseif ($type === 'erp_only'): ?>
								<span class="text-muted">—</span>
								<?php else: ?>
								<span class="text-muted">n/a</span>
								<?php endif; ?>
							</td>
							<td>
								<?php
								$st = (string) ($t['status_display'] ?? $t['status'] ?? '');
								if ($st === 'disabled') {
									echo '<span class="text-danger">Off</span>';
								} elseif ($st === 'expired') {
									echo '<span class="text-warning">Expired</span>';
								} else {
									echo epc_tcc_h($t['status'] ?? $st);
								}
								?>
							</td>
							<td>
								<?php if ($inReg): ?>
								<label class="epc-tcc-toggle" style="margin:0;cursor:pointer">
									<input type="checkbox" class="epc-tcc-active-cb" data-key="<?php echo epc_tcc_h($key); ?>"<?php echo $isOn ? ' checked' : ''; ?>>
									<span class="text-muted small"><?php echo $isOn ? 'ON' : 'OFF'; ?></span>
								</label>
								<?php else: ?>
								<span class="text-muted">—</span>
								<?php endif; ?>
							</td>
							<td>
								<?php $em = (string) ($t['admin_email'] ?? ''); ?>
								<?php if ($em !== ''): ?><a href="mailto:<?php echo epc_tcc_h($em); ?>"><?php echo epc_tcc_h($em); ?></a><?php else: ?><span class="text-muted">—</span><?php endif; ?>
							</td>
							<td>
								<?php
								$erpLogin = (string) ($t['erp_login_url'] ?? $urls['erp_login'] ?? $urls['client_erp'] ?? '');
								if ($erpLogin !== ''): ?>
								<a href="<?php echo epc_tcc_h($erpLogin); ?>" target="_blank" rel="noopener" class="small"><?php echo epc_tcc_h($erpLogin); ?></a>
								<?php else: ?><span class="text-muted">—</span><?php endif; ?>
							</td>
							<td class="epc-tcc-pwd-cell" data-key="<?php echo epc_tcc_h($key); ?>">
								<?php if ($hasPwd): ?>
								<span class="epc-tcc-pwd-mask">••••••••</span>
								<span class="epc-tcc-pwd-plain"></span>
								<button type="button" class="btn btn-xs btn-link epc-tcc-reveal" data-key="<?php echo epc_tcc_h($key); ?>">Show</button>
								<?php elseif ($inReg): ?>
								<button type="button" class="btn btn-xs btn-warning epc-tcc-reset" data-key="<?php echo epc_tcc_h($key); ?>">Reset</button>
								<?php else: ?>
								<span class="text-muted" title="Shared tenant commerce DB — use platform operator account">platform op</span>
								<?php endif; ?>
							</td>
							<td style="white-space:nowrap">
								<?php if (!empty($urls['storefront'])): ?>
								<a class="btn btn-xs btn-default" target="_blank" rel="noopener" href="<?php echo epc_tcc_h($urls['storefront']); ?>">Store</a>
								<?php endif; ?>
								<?php if (!empty($urls['cp'])): ?>
								<a class="btn btn-xs btn-primary" target="_blank" rel="noopener" href="<?php echo epc_tcc_h(!empty($urls['cp_autologin']) ? $urls['cp_autologin'] : $urls['cp']); ?>">CP</a>
								<?php endif; ?>
								<?php if (!empty($urls['client_erp'])): ?>
								<a class="btn btn-xs btn-info" target="_blank" rel="noopener" href="<?php echo epc_tcc_h($urls['client_erp']); ?>">ERP</a>
								<?php endif; ?>
							</td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
			<p class="text-muted small">* Template tenants share the default commerce database until registered in Tenant Hub. Toggle and password reset require a registry row.</p>
		</div>
	</div>
</div>

<script>
(function () {
	var root = document.getElementById('epc-tenant-control-center');
	var ajaxUrl = root ? (root.getAttribute('data-ajax-url') || '') : '';
	var flash = document.getElementById('epc-tcc-flash');
	function showFlash(ok, msg) {
		if (!flash) return;
		flash.className = 'alert alert-' + (ok ? 'success' : 'danger');
		flash.textContent = msg;
		flash.style.display = 'block';
	}
	function post(action, data, cb) {
		if (!ajaxUrl) {
			showFlash(false, 'AJAX URL missing — reload the page');
			return;
		}
		var body = new FormData();
		body.append('action', action);
		for (var k in data) { if (Object.prototype.hasOwnProperty.call(data, k)) body.append(k, data[k]); }
		fetch(ajaxUrl, { method: 'POST', body: body, credentials: 'same-origin' })
			.then(function (r) {
				return r.text().then(function (text) {
					var res = null;
					try {
						res = text ? JSON.parse(text) : null;
					} catch (e) {
						var snippet = String(text || '').replace(/\s+/g, ' ').trim().slice(0, 180);
						throw new Error(snippet || ('HTTP ' + r.status));
					}
					if (!r.ok && res && res.message) {
						throw new Error(res.message);
					}
					if (!r.ok) {
						throw new Error('HTTP ' + r.status);
					}
					return res || {};
				});
			})
			.then(cb)
			.catch(function (err) {
				showFlash(false, (err && err.message) ? err.message : 'Request failed');
			});
	}
	function bindReveal(btn) {
		btn.addEventListener('click', function (e) {
			e.preventDefault();
			var cell = btn.closest('.epc-tcc-pwd-cell');
			if (!cell) return;
			var plain = cell.querySelector('.epc-tcc-pwd-plain');
			if (!plain) return;
			// Hide if currently shown.
			if (cell.classList.contains('is-revealed')) {
				cell.classList.remove('is-revealed');
				plain.textContent = '';
				btn.textContent = 'Show';
				return;
			}
			// Reveal: fetch the credential on demand (audited server-side); never
			// embed plaintext in the page source.
			if (!ajaxUrl) { showFlash(false, 'No endpoint'); return; }
			var key = btn.getAttribute('data-key') || cell.getAttribute('data-key') || '';
			if (!key) return;
			var prev = btn.textContent;
			btn.textContent = '…';
			btn.disabled = true;
			var body = new URLSearchParams();
			body.append('action', 'tenant_reveal_password');
			body.append('site_key', key);
			fetch(ajaxUrl, { method: 'POST', body: body, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function (res) {
					btn.disabled = false;
					if (res && res.status && res.password) {
						plain.textContent = res.password;
						cell.classList.add('is-revealed');
						btn.textContent = 'Hide';
					} else {
						btn.textContent = prev;
						showFlash(false, (res && res.message) ? res.message : 'Could not reveal');
					}
				})
				.catch(function () { btn.disabled = false; btn.textContent = prev; showFlash(false, 'Request failed'); });
		});
	}
	document.querySelectorAll('.epc-tcc-reveal').forEach(bindReveal);
	document.querySelectorAll('.epc-tcc-active-cb').forEach(function (cb) {
		cb.addEventListener('change', function () {
			var key = cb.getAttribute('data-key');
			var active = cb.checked ? '1' : '0';
			cb.disabled = true;
			post('tenant_set_active', { site_key: key, active: active }, function (res) {
				cb.disabled = false;
				showFlash(!!res.status, res.message || (res.status ? 'Updated' : 'Failed'));
				if (res.status) {
					var span = cb.parentNode.querySelector('.text-muted');
					if (span) span.textContent = cb.checked ? 'ON' : 'OFF';
				} else {
					cb.checked = !cb.checked;
				}
			});
		});
	});
	var dacTenant = document.getElementById('epc-dac-tenant');
	var dacEmail = document.getElementById('epc-dac-email');
	var dacPwd = document.getElementById('epc-dac-password');
	var dacDemo = document.getElementById('epc-dac-is-demo');
	var dacMeta = document.getElementById('epc-dac-meta');
	var dacPwdOnce = document.getElementById('epc-dac-pwd-once');
	var dacUsersWrap = document.getElementById('epc-dac-users-wrap');
	var dacUsers = document.getElementById('epc-dac-users');
	function dacKey() { return dacTenant ? dacTenant.value : ''; }
	function showPwdOnce(pwd, email) {
		if (!dacPwdOnce) return;
		if (!pwd) { dacPwdOnce.style.display = 'none'; return; }
		dacPwdOnce.innerHTML = '<strong>New password (copy now):</strong> <code>' + String(pwd).replace(/&/g,'&amp;').replace(/</g,'&lt;') + '</code>'
			+ (email ? ' — login <code>' + String(email).replace(/&/g,'&amp;').replace(/</g,'&lt;') + '</code>' : '');
		dacPwdOnce.style.display = 'block';
	}
	function renderDacUsers(users) {
		if (!dacUsers || !dacUsersWrap) return;
		dacUsers.innerHTML = '';
		if (!users || !users.length) {
			dacUsersWrap.style.display = 'block';
			dacUsers.innerHTML = '<tr><td colspan="4" class="text-muted">No backend CP users in tenant DB</td></tr>';
			return;
		}
		users.forEach(function (u) {
			var tr = document.createElement('tr');
			tr.innerHTML = '<td>' + (u.user_id || '') + '</td><td>' + (u.email || '') + '</td><td>' + (u.groups || '') + '</td><td>' + (u.unlocked ? 'yes' : 'no') + '</td>';
			dacUsers.appendChild(tr);
		});
		dacUsersWrap.style.display = 'block';
	}
	function applyDacTenant(res) {
		var t = res && res.tenant ? res.tenant : null;
		if (!t || !t.ok) {
			showFlash(false, (res && res.message) || (t && t.message) || 'Load failed');
			return;
		}
		if (dacEmail) dacEmail.value = t.login_email || '';
		if (dacDemo) dacDemo.checked = !!t.is_demo;
		if (dacMeta) {
			var links = t.urls || {};
			dacMeta.textContent = (t.trade_name || t.site_key) + ' · ' + (t.hostname || '') + ' · ' + (t.tenant_type || '')
				+ (t.tenant_db_ok ? '' : (' · tenant DB unreachable' + (t.tenant_db_error ? ': ' + t.tenant_db_error : '')));
			dacMeta.style.display = 'block';
		}
		renderDacUsers(t.cp_users || []);
		showFlash(true, 'Loaded');
	}
	function dacLoad() {
		var key = dacKey();
		if (!key) { showFlash(false, 'Select a tenant'); return; }
		post('tenant_demo_access_load', { site_key: key }, function (res) {
			applyDacTenant(res);
		});
	}
	if (dacTenant) {
		dacTenant.addEventListener('change', function () {
			if (dacPwdOnce) dacPwdOnce.style.display = 'none';
			if (dacPwd) dacPwd.value = '';
			var opt = dacTenant.options[dacTenant.selectedIndex];
			if (dacDemo && opt) dacDemo.checked = opt.getAttribute('data-demo') === '1';
			if (dacKey()) dacLoad();
		});
	}
	var dacLoadBtn = document.getElementById('epc-dac-load');
	if (dacLoadBtn) dacLoadBtn.addEventListener('click', dacLoad);
	var dacSaveBtn = document.getElementById('epc-dac-save');
	if (dacSaveBtn) dacSaveBtn.addEventListener('click', function () {
		var key = dacKey();
		if (!key) { showFlash(false, 'Select a tenant'); return; }
		var email = dacEmail ? dacEmail.value.trim() : '';
		if (!email) { showFlash(false, 'Enter login email'); return; }
		var data = { site_key: key, email: email, is_demo: (dacDemo && dacDemo.checked) ? '1' : '0' };
		if (dacPwd && dacPwd.value.trim() !== '') data.password = dacPwd.value.trim();
		dacSaveBtn.disabled = true;
		post('tenant_demo_access_save', data, function (res) {
			dacSaveBtn.disabled = false;
			showFlash(!!res.status, res.message || '');
			if (res.status) {
				if (res.password) showPwdOnce(res.password, res.email);
				if (dacPwd) dacPwd.value = '';
				dacLoad();
			}
		});
	});
	var dacResetBtn = document.getElementById('epc-dac-reset');
	if (dacResetBtn) dacResetBtn.addEventListener('click', function () {
		var key = dacKey();
		if (!key) { showFlash(false, 'Select a tenant'); return; }
		if (!confirm('Generate a new CP password for this tenant?')) return;
		dacResetBtn.disabled = true;
		post('tenant_demo_access_save', { site_key: key, reset_password: '1' }, function (res) {
			dacResetBtn.disabled = false;
			showFlash(!!res.status, res.message || '');
			if (res.status && res.password) {
				showPwdOnce(res.password, res.email);
				if (dacEmail && res.email) dacEmail.value = res.email;
				dacLoad();
			}
		});
	});

	document.querySelectorAll('.epc-tcc-reset').forEach(function (btn) {
		btn.addEventListener('click', function () {
			if (!confirm('Generate new CP password for this tenant?')) return;
			var key = btn.getAttribute('data-key');
			btn.disabled = true;
			post('tenant_reset_password', { site_key: key }, function (res) {
				btn.disabled = false;
				showFlash(!!res.status, res.message || '');
				if (res.status && res.password) {
					var row = btn.closest('tr');
					var cell = row ? row.querySelector('.epc-tcc-pwd-cell') : null;
					if (cell) {
						var pwd = String(res.password || '');
						cell.setAttribute('data-pwd', pwd);
						cell.classList.remove('is-revealed');
						cell.innerHTML = '<span class="epc-tcc-pwd-mask">••••••••</span><span class="epc-tcc-pwd-plain"></span>'
							+ '<button type="button" class="btn btn-xs btn-link epc-tcc-reveal">Show</button>';
						var plain = cell.querySelector('.epc-tcc-pwd-plain');
						if (plain) plain.textContent = pwd;
						var nb = cell.querySelector('.epc-tcc-reveal');
						if (nb) bindReveal(nb);
					}
				}
			});
		});
	});
})();
</script>
