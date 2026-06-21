<?php
/**
 * Super CP — Tax Toolkit: worldwide jurisdiction kits, tenant assignment by site country.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';

function epc_ttkm_h($v): string
{
	return epc_tax_toolkit_h($v);
}

function epc_ttkm_render_rate($v): string
{
	if ($v === null || $v === '') {
		return '—';
	}
	return epc_ttkm_h((string) $v) . '%';
}

function epc_ttkm_render_section_cards(array $rules, string $section): string
{
	$html = '';
	if ($section === 'indirect') {
		$ind = $rules['indirect'] ?? array();
		foreach (array('vat', 'gst', 'sales_tax', 'excise') as $key) {
			if ($key === 'excise' && !empty($ind['excise']) && is_array($ind['excise'])) {
				foreach ($ind['excise'] as $ex) {
					$html .= '<div class="epc-ttk-card"><h5>Excise — ' . epc_ttkm_h($ex['category'] ?? 'item') . '</h5>';
					if (isset($ex['rate'])) {
						$html .= '<div class="rate">' . epc_ttkm_render_rate($ex['rate']) . '</div>';
					}
					$html .= '<p class="muted">' . epc_ttkm_h($ex['notes'] ?? '') . '</p></div>';
				}
				continue;
			}
			if (empty($ind[$key]) || !is_array($ind[$key])) {
				continue;
			}
			$row = $ind[$key];
			$html .= '<div class="epc-ttk-card"><h5>' . epc_ttkm_h(strtoupper($key)) . ' — ' . epc_ttkm_h($row['label'] ?? $key) . '</h5>';
			$html .= '<div class="rate">' . epc_ttkm_render_rate($row['rate'] ?? null) . '</div>';
			if (!empty($row['reg_label'])) {
				$html .= '<p class="muted">Reg: ' . epc_ttkm_h($row['reg_label']) . '</p>';
			}
			if (!empty($row['delegate_uae_vat'])) {
				$html .= '<p class="muted"><strong>Delegates to epc_uae_vat</strong></p>';
			}
			$html .= '</div>';
		}
		if ($html === '' && isset($rules['standard_rate'])) {
			$html .= '<div class="epc-ttk-card"><h5>Standard rate</h5><div class="rate">' . epc_ttkm_render_rate($rules['standard_rate']) . '</div></div>';
		}
	} elseif ($section === 'direct') {
		$direct = $rules['direct'] ?? array();
		foreach (array('corporate_tax' => 'Corporate income tax (CIT)', 'income_tax' => 'Personal / business income tax') as $k => $label) {
			$row = $direct[$k] ?? array();
			$html .= '<div class="epc-ttk-card"><h5>' . epc_ttkm_h($label) . '</h5>';
			$html .= '<div class="rate">' . epc_ttkm_render_rate($row['rate'] ?? null) . '</div>';
			if (!empty($row['threshold']) || !empty($row['threshold_aed'])) {
				$thr = $row['threshold'] ?? $row['threshold_aed'];
				$cur = $row['threshold_currency'] ?? '';
				$html .= '<p class="muted">Threshold: ' . epc_ttkm_h((string) $thr) . ' ' . epc_ttkm_h($cur) . '</p>';
			}
			if (!empty($row['free_zone_qfzp'])) {
				$html .= '<p class="muted">QFZP free zone flag available</p>';
			}
			if (!empty($row['notes'])) {
				$html .= '<p class="muted">' . epc_ttkm_h($row['notes']) . '</p>';
			}
			$html .= '</div>';
		}
	} elseif ($section === 'trade') {
		$trade = $rules['trade'] ?? array();
		$importRules = $rules['import_rules'] ?? array();
		$exportRules = $rules['export_rules'] ?? array();
		$html .= '<div class="epc-ttk-card"><h5>Import duty (default)</h5><div class="rate">' . epc_ttkm_render_rate($trade['import_duty_default'] ?? $importRules['import_duty_rate'] ?? null) . '</div></div>';
		$html .= '<div class="epc-ttk-card"><h5>Export VAT treatment</h5><p class="muted">' . epc_ttkm_h($trade['export_vat_treatment'] ?? ($exportRules['zero_rate_export'] ? 'zero_rated' : 'standard')) . '</p></div>';
		$html .= '<div class="epc-ttk-card"><h5>Reverse charge B2B</h5><p class="muted">' . (!empty($trade['reverse_charge_b2b']) || !empty($importRules['reverse_charge_b2b']) ? 'Yes' : 'No') . '</p></div>';
		if (!empty($trade['notes'])) {
			$html .= '<div class="epc-ttk-card"><h5>Trade notes</h5><p class="muted">' . epc_ttkm_h($trade['notes']) . '</p></div>';
		}
	} elseif ($section === 'international') {
		$intl = $rules['international'] ?? array();
		$dtt = $rules['double_taxation'] ?? array();
		$html .= '<div class="epc-ttk-card"><h5>Foreign tax credit (FTC)</h5><p class="muted">' . (!empty($intl['ftc_available']) ? 'Eligible — verify treaty' : 'Configure manually') . '</p></div>';
		$html .= '<div class="epc-ttk-card"><h5>Double taxation</h5><p class="muted">' . epc_ttkm_h($dtt['rules'] ?? $intl['notes'] ?? '') . '</p>';
		if (!empty($dtt['credit_method'])) {
			$html .= '<p class="muted">Credit method: ' . epc_ttkm_h($dtt['credit_method']) . '</p>';
		}
		$html .= '</div>';
		if (!empty($intl['dtt_countries']) && is_array($intl['dtt_countries'])) {
			$html .= '<div class="epc-ttk-card"><h5>DTT reference countries</h5><p class="muted">' . epc_ttkm_h(implode(', ', $intl['dtt_countries'])) . '</p></div>';
		}
		$wht = $rules['withholding'] ?? array();
		foreach ($wht as $w) {
			$html .= '<div class="epc-ttk-card"><h5>WHT — ' . epc_ttkm_h($w['type'] ?? '') . '</h5>';
			if (isset($w['rate'])) {
				$html .= '<div class="rate">' . epc_ttkm_render_rate($w['rate']) . '</div>';
			}
			$html .= '<p class="muted">' . epc_ttkm_h($w['notes'] ?? '') . '</p></div>';
		}
	} elseif ($section === 'erp') {
		$hooks = $rules['erp_hooks'] ?? array();
		foreach ($hooks as $k => $v) {
			$label = ucwords(str_replace('_', ' ', $k));
			$display = is_bool($v) ? ($v ? 'Yes' : 'No') : (string) $v;
			$html .= '<div class="epc-ttk-card"><h5>' . epc_ttkm_h($label) . '</h5><p class="muted">' . epc_ttkm_h($display) . '</p></div>';
		}
	}
	return $html !== '' ? $html : '<p class="text-muted">No structured data for this section.</p>';
}

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
	echo '<div class="alert alert-warning">Tax Toolkit management is available on <strong>www.ecomae.com</strong> Super CP only.</div>';
	return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	global $DP_Config;
	echo '<div class="alert alert-warning">Please <a href="/' . epc_ttkm_h((string) $DP_Config->backend_dir) . '/">log in to Super CP</a>.</div>';
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}
epc_tax_toolkit_ensure_schema($pdo);

$backend = (string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp');
$pageBase = '/' . $backend . '/control/portal/epc_tax_toolkit_manage';
$token = 'epartscart-deploy-2026';
$flash = null;
$viewKit = isset($_GET['kit']) ? (string) $_GET['kit'] : '';
$filterQ = trim((string) ($_GET['q'] ?? ''));

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
	$action = (string) ($_POST['epc_ttk_action'] ?? '');
	try {
		if ($action === 'install') {
			$code = trim((string) ($_POST['kit_code'] ?? ''));
			$def = !empty($_POST['set_default']);
			epc_tax_toolkit_install($pdo, $code, $def, (int) DP_User::getAdminId());
			$flash = array('ok' => true, 'message' => 'Installed kit ' . $code . ($def ? ' (tenant default)' : '') . '.');
		} elseif ($action === 'install_all') {
			$n = epc_tax_toolkit_install_all_kits($pdo, (int) DP_User::getAdminId());
			$flash = array('ok' => true, 'message' => 'Installed ' . $n . ' jurisdiction kits. Tenant default preserved.');
		} elseif ($action === 'assign_tenant') {
			$country = strtoupper(trim((string) ($_POST['country_code'] ?? '')));
			$kit = trim((string) ($_POST['kit_code'] ?? ''));
			$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? 'platform'))));
			epc_tax_toolkit_assign_tenant($pdo, $country, $kit, '', $siteKey !== '' ? $siteKey : 'platform');
			$flash = array('ok' => true, 'message' => 'Tenant kit saved for site_key=' . $siteKey . ' → ' . ($kit !== '' ? $kit : 'auto from ' . $country) . '.');
		} elseif ($action === 'migrate') {
			$result = epc_tax_toolkit_migrate_tenant($pdo, 'platform', 'www.ecomae.com');
			$flash = array(
				'ok' => !empty($result['ok']),
				'message' => !empty($result['ok'])
					? 'Platform tenant migrated: ' . $result['kit_code'] . ' (' . $result['country_code'] . ')'
					: ('Migration failed: ' . ($result['error'] ?? 'unknown')),
			);
		} elseif ($action === 'migrate_all') {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit_cp_install.php';
			$cfg = $GLOBALS['DP_Config'];
			$rows = array();
			if (function_exists('epc_portal_list_tenants')) {
				foreach (epc_portal_list_tenants($pdo) as $t) {
					if ((string) ($t['status'] ?? '') !== 'live') {
						continue;
					}
					$tpdo = epc_tax_toolkit_setup_connect(
						array('db' => $t['db_name'], 'user' => $t['db_user'], 'pass' => $t['db_password']),
						$cfg
					);
					if (!$tpdo instanceof PDO) {
						continue;
					}
					epc_tax_toolkit_seed_kits($tpdo);
					$siteKey = (string) ($t['site_key'] ?? '');
					$m = epc_tax_toolkit_migrate_tenant($tpdo, $siteKey, (string) ($t['hostname'] ?? ''));
					$rows[] = $siteKey . ':' . ($m['kit_code'] ?? '?');
				}
			}
			$flash = array('ok' => true, 'message' => 'Migrated ' . count($rows) . ' live tenants — ' . implode(', ', array_slice($rows, 0, 8)) . (count($rows) > 8 ? '…' : ''));
		} elseif ($action === 'update_kit') {
			$code = trim((string) ($_POST['kit_code'] ?? ''));
			$result = epc_tax_toolkit_refresh_kit_rules($pdo, $code, (int) DP_User::getAdminId(), true);
			$flash = array('ok' => true, 'message' => 'Updated ' . $code . ' — ' . ($result['changelog'] ?? ''));
			$viewKit = $code;
		} elseif ($action === 'update_all') {
			epc_tax_toolkit_refresh_all_kits($pdo, (int) DP_User::getAdminId());
			$flash = array('ok' => true, 'message' => 'Refreshed all jurisdiction kits from seed catalog (+ UAE FTA sync where applicable).');
		}
	} catch (Exception $e) {
		$flash = array('ok' => false, 'message' => $e->getMessage());
	}
}

$catalog = epc_tax_toolkit_list_catalog($pdo);
$installed = epc_tax_toolkit_list_installed($pdo);
$installedCodes = array();
foreach ($installed as $i) {
	$installedCodes[$i['kit_code']] = true;
}
$counts = epc_tax_toolkit_profile_counts($pdo);
$tenantCtx = epc_tax_toolkit_tenant_context($pdo);
$tenantProfile = epc_tax_toolkit_get_tenant_profile($pdo, 'platform');
$liveTenants = function_exists('epc_portal_list_tenants') ? epc_portal_list_tenants($pdo) : array();

if ($filterQ !== '') {
	$q = strtolower($filterQ);
	$catalog = array_values(array_filter($catalog, function ($kit) use ($q) {
		$hay = strtolower($kit['kit_code'] . ' ' . $kit['name'] . ' ' . implode(' ', $kit['country_codes']));
		return strpos($hay, $q) !== false;
	}));
}

$viewKitRow = $viewKit !== '' ? epc_tax_toolkit_get_kit($pdo, $viewKit) : null;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-portal-settings',
	'hero' => array(
		'badge' => 'Super CP',
		'title' => 'Tax Toolkit',
		'sub' => 'Complete business taxation — VAT/GST, corporate tax, import/export, double taxation, FTC, and ERP hooks. Tax resolves from tenant jurisdiction.',
	),
));
?>

<div class="epc-portal-settings">
	<div class="hpanel">
		<div class="panel-body">
			<div class="epc-ttk-hero">
				<h3><i class="fa fa-balance-scale"></i> Tax Toolkit — Complete Business Tax</h3>
				<p style="margin:0;opacity:.92">Worldwide jurisdiction kits covering <strong>indirect tax</strong> (VAT/GST/sales tax), <strong>corporate income tax</strong>, <strong>import/export &amp; customs</strong>, <strong>withholding</strong>, <strong>double taxation &amp; foreign tax credits</strong>, and <strong>ERP purchase/sales/profit hooks</strong>. Tax resolves from <strong>tenant country</strong> — not customer country.</p>
			</div>

			<div class="epc-ttk-detail-head" style="margin-bottom:16px">
				<form method="post" style="display:inline">
					<input type="hidden" name="epc_ttk_action" value="update_all">
					<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-refresh"></i> Update tax data (all kits)</button>
				</form>
				<span class="text-muted small">Refreshes seed rates + UAE FTA legislation sync</span>
			</div>

			<div class="row" style="margin-bottom:16px">
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Catalog kits</div><strong><?php echo (int) $counts['catalog']; ?></strong></div></div>
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Installed</div><strong><?php echo (int) $counts['installed']; ?></strong></div></div>
				<div class="col-sm-3"><div class="well well-sm text-center"><div class="text-muted small">Platform tenant kit</div><strong><code><?php echo epc_ttkm_h($tenantCtx['kit_code']); ?></code></strong></div></div>
				<div class="col-sm-3"><div class="well well-sm"><div class="text-muted small">Setup</div><a href="https://www.ecomae.com/epc-tax-toolkit-setup-all.php?token=<?php echo epc_ttkm_h($token); ?>&amp;apply=1&amp;migrate=1" target="_blank" rel="noopener">setup-all?apply=1&amp;migrate=1</a></div></div>
			</div>

			<?php if ($flash !== null): ?>
			<div class="alert alert-<?php echo !empty($flash['ok']) ? 'success' : 'danger'; ?>"><?php echo epc_ttkm_h($flash['message'] ?? ''); ?></div>
			<?php endif; ?>

			<p>
				<form method="post" style="display:inline"><input type="hidden" name="epc_ttk_action" value="install_all"><button type="submit" class="btn btn-success btn-sm"><i class="fa fa-download"></i> Install all <?php echo (int) $counts['catalog']; ?> kits</button></form>
				<form method="post" style="display:inline;margin-left:8px" onsubmit="return confirm('Assign tenant kits on all live tenant DBs from registry country?');"><input type="hidden" name="epc_ttk_action" value="migrate_all"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-globe"></i> Migrate all live tenants</button></form>
				<form method="post" style="display:inline;margin-left:8px"><input type="hidden" name="epc_ttk_action" value="migrate"><button type="submit" class="btn btn-default btn-sm"><i class="fa fa-refresh"></i> Migrate platform DB</button></form>
			</p>

			<?php if ($viewKitRow):
				$rules = $viewKitRow['rules'] ?? array();
				$updates = epc_tax_toolkit_list_updates($pdo, $viewKitRow['kit_code'], 5);
				$citRate = $rules['direct']['corporate_tax']['rate'] ?? null;
				$indirectRate = $rules['standard_rate'] ?? null;
				$lastUpd = $rules['last_updated'] ?? '—';
				$source = $rules['source'] ?? 'seed';
			?>
			<div class="hpanel">
				<div class="panel-heading">
					<div class="epc-ttk-detail-head">
						<h4 style="margin:0">Kit — <?php echo epc_ttkm_h($viewKitRow['name']); ?></h4>
						<form method="post" class="btn-update">
							<input type="hidden" name="epc_ttk_action" value="update_kit">
							<input type="hidden" name="kit_code" value="<?php echo epc_ttkm_h($viewKitRow['kit_code']); ?>">
							<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-cloud-download"></i> Update tax data</button>
						</form>
					</div>
				</div>
				<div class="panel-body">
					<div class="epc-ttk-meta">
						<span><strong>Code:</strong> <code><?php echo epc_ttkm_h($viewKitRow['kit_code']); ?></code></span>
						<span><strong>Indirect:</strong> <?php echo epc_ttkm_render_rate($indirectRate); ?></span>
						<span><strong>CIT:</strong> <?php echo epc_ttkm_render_rate($citRate); ?></span>
						<span><strong>Last updated:</strong> <?php echo epc_ttkm_h($lastUpd); ?></span>
						<span class="epc-ttk-badge epc-ttk-badge--source"><?php echo epc_ttkm_h($source); ?></span>
					</div>
					<?php if (!empty($rules['limitations']) || !empty($rules['phase2_note'])): ?>
					<div class="epc-ttk-limitation">
						<i class="fa fa-info-circle"></i>
						<?php echo epc_ttkm_h(is_array($rules['limitations'] ?? null) ? implode(' ', $rules['limitations']) : ($rules['phase2_note'] ?? '')); ?>
					</div>
					<?php endif; ?>
					<div class="epc-ttk-tabs" role="tablist">
						<button type="button" class="epc-ttk-tab is-active" data-ttk-tab="indirect">Indirect</button>
						<button type="button" class="epc-ttk-tab" data-ttk-tab="direct">Direct</button>
						<button type="button" class="epc-ttk-tab" data-ttk-tab="trade">Trade</button>
						<button type="button" class="epc-ttk-tab" data-ttk-tab="international">International</button>
						<button type="button" class="epc-ttk-tab" data-ttk-tab="erp">ERP hooks</button>
						<button type="button" class="epc-ttk-tab" data-ttk-tab="raw">Raw JSON</button>
					</div>
					<?php foreach (array('indirect', 'direct', 'trade', 'international', 'erp') as $sec): ?>
					<div class="epc-ttk-panel<?php echo $sec === 'indirect' ? ' is-active' : ''; ?>" data-ttk-panel="<?php echo epc_ttkm_h($sec); ?>">
						<div class="epc-ttk-cards"><?php echo epc_ttkm_render_section_cards($rules, $sec); ?></div>
					</div>
					<?php endforeach; ?>
					<div class="epc-ttk-panel" data-ttk-panel="raw">
						<div class="epc-ttk-rules"><?php echo epc_ttkm_h(json_encode($rules, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE)); ?></div>
					</div>
					<?php if (!empty($updates)): ?>
					<div class="epc-ttk-changelog">
						<strong>Update history</strong>
						<ul>
						<?php foreach ($updates as $u): ?>
							<li><?php echo epc_ttkm_h(date('Y-m-d H:i', (int) ($u['time_updated'] ?? 0))); ?> — <?php echo epc_ttkm_h($u['source'] ?? ''); ?>: <?php echo epc_ttkm_h($u['changelog'] ?? ''); ?></li>
						<?php endforeach; ?>
						</ul>
					</div>
					<?php endif; ?>
					<p style="margin-top:14px"><a href="<?php echo epc_ttkm_h($pageBase); ?>">← Back to kit list</a></p>
				</div>
			</div>
			<script>
			(function(){
				var tabs = document.querySelectorAll('.epc-ttk-tab');
				var panels = document.querySelectorAll('.epc-ttk-panel');
				tabs.forEach(function(tab){
					tab.addEventListener('click', function(){
						var id = tab.getAttribute('data-ttk-tab');
						tabs.forEach(function(t){ t.classList.remove('is-active'); });
						panels.forEach(function(p){ p.classList.remove('is-active'); });
						tab.classList.add('is-active');
						var panel = document.querySelector('.epc-ttk-panel[data-ttk-panel="'+id+'"]');
						if(panel) panel.classList.add('is-active');
					});
				});
			})();
			</script>
			<?php endif; ?>

			<div class="hpanel">
				<div class="panel-heading"><h4>Platform tenant assignment (ecomae)</h4></div>
				<div class="panel-body">
					<p class="text-muted" style="font-size:13px">Current: <code><?php echo epc_ttkm_h($tenantCtx['kit_code']); ?></code> · country <?php echo epc_ttkm_h($tenantCtx['country_code']); ?> · source <?php echo epc_ttkm_h($tenantCtx['source']); ?></p>
					<form method="post" class="form-inline">
						<input type="hidden" name="epc_ttk_action" value="assign_tenant">
						<input type="hidden" name="site_key" value="platform">
						<label>Country</label>
						<input type="text" name="country_code" class="form-control input-sm" value="<?php echo epc_ttkm_h($tenantCtx['country_code']); ?>" maxlength="8" style="width:70px" required>
						<label style="margin-left:10px">Kit</label>
						<select name="kit_code" class="form-control input-sm">
							<option value="">— Auto from country —</option>
							<?php foreach (epc_tax_toolkit_list_catalog($pdo) as $kit): ?>
							<option value="<?php echo epc_ttkm_h($kit['kit_code']); ?>"<?php echo $tenantCtx['kit_code'] === $kit['kit_code'] ? ' selected' : ''; ?>><?php echo epc_ttkm_h($kit['kit_code']); ?></option>
							<?php endforeach; ?>
						</select>
						<button type="submit" class="btn btn-primary btn-sm" style="margin-left:10px">Save platform tenant kit</button>
					</form>
				</div>
			</div>

			<?php if (!empty($liveTenants)): ?>
			<div class="hpanel">
				<div class="panel-heading"><h4>Live tenant registry</h4></div>
				<div class="panel-body" style="overflow-x:auto">
					<table class="table table-striped table-condensed">
						<thead><tr><th>Site key</th><th>Host</th><th>Expected country</th><th>Suggested kit</th></tr></thead>
						<tbody>
						<?php foreach ($liveTenants as $t):
							if ((string) ($t['status'] ?? '') !== 'live') {
								continue;
							}
							$sk = (string) ($t['site_key'] ?? '');
							$known = epc_tax_toolkit_known_tenant_countries();
							$cc = $known[$sk] ?? 'AE';
							$suggest = epc_tax_toolkit_country_to_kit_code($cc);
						?>
						<tr>
							<td><code><?php echo epc_ttkm_h($sk); ?></code></td>
							<td><?php echo epc_ttkm_h($t['hostname'] ?? ''); ?></td>
							<td><?php echo epc_ttkm_h($cc); ?></td>
							<td><code><?php echo epc_ttkm_h($suggest); ?></code></td>
						</tr>
						<?php endforeach; ?>
						</tbody>
					</table>
					<p class="text-muted small">Run <em>Migrate all live tenants</em> to push kits into each tenant MySQL DB from ERP company country / portal settings / registry defaults.</p>
				</div>
			</div>
			<?php endif; ?>

			<div class="hpanel">
				<div class="panel-heading"><h4>Jurisdiction kits (<?php echo count($catalog); ?> shown)</h4></div>
				<div class="panel-body">
					<form method="get" class="epc-ttk-filter form-inline">
						<input type="text" name="q" class="form-control input-sm" placeholder="Filter country or kit code…" value="<?php echo epc_ttkm_h($filterQ); ?>">
						<button type="submit" class="btn btn-default btn-sm">Filter</button>
						<?php if ($filterQ !== ''): ?><a class="btn btn-link btn-sm" href="<?php echo epc_ttkm_h($pageBase); ?>">Clear</a><?php endif; ?>
					</form>
					<div class="epc-ttk-kit-list">
					<?php foreach ($catalog as $kit): ?>
					<div class="epc-ttk-kit<?php echo !empty($installedCodes[$kit['kit_code']]) ? ' installed' : ''; ?>">
						<h5><?php echo epc_ttkm_h($kit['name']); ?>
							<?php if (!empty($installedCodes[$kit['kit_code']])): ?><span class="epc-ttk-badge epc-ttk-badge--installed">Installed</span><?php endif; ?>
							<?php foreach ($installed as $ins) {
								if ($ins['kit_code'] === $kit['kit_code'] && !empty($ins['is_default'])) {
									echo '<span class="epc-ttk-badge epc-ttk-badge--default">Tenant default</span>';
								}
							} ?>
						</h5>
						<p class="text-muted" style="margin:0 0 8px;font-size:13px">
							<code><?php echo epc_ttkm_h($kit['kit_code']); ?></code> ·
							<?php echo epc_ttkm_h($kit['tax_type']); ?> ·
							VAT/GST <?php echo epc_ttkm_h((string) ($kit['rules']['standard_rate'] ?? 0)); ?>% ·
							CIT <?php
							$cit = $kit['rules']['direct']['corporate_tax']['rate'] ?? null;
							echo $cit !== null ? epc_ttkm_h((string) $cit) . '%' : '—';
							?> ·
							<?php echo epc_ttkm_h($kit['country_codes'][0] ?? ''); ?>
						</p>
						<a class="btn btn-default btn-xs" href="<?php echo epc_ttkm_h($pageBase); ?>?kit=<?php echo epc_ttkm_h(urlencode($kit['kit_code'])); ?>">View rules</a>
						<?php if (empty($installedCodes[$kit['kit_code']])): ?>
						<form method="post" style="display:inline">
							<input type="hidden" name="epc_ttk_action" value="install">
							<input type="hidden" name="kit_code" value="<?php echo epc_ttkm_h($kit['kit_code']); ?>">
							<button type="submit" class="btn btn-success btn-xs">Install</button>
						</form>
						<?php else: ?>
						<form method="post" style="display:inline">
							<input type="hidden" name="epc_ttk_action" value="install">
							<input type="hidden" name="kit_code" value="<?php echo epc_ttkm_h($kit['kit_code']); ?>">
							<label style="font-weight:normal;font-size:12px;margin:0 6px"><input type="checkbox" name="set_default" value="1"> Set as tenant default</label>
							<button type="submit" class="btn btn-warning btn-xs">Re-install / set default</button>
						</form>
						<?php endif; ?>
					</div>
					<?php endforeach; ?>
					</div>
				</div>
			</div>

			<div class="well well-sm">
				<strong>Operator workflow</strong>
				<ol style="margin:8px 0 0;padding-left:18px;font-size:13px">
					<li>Run setup-all with <em>apply=1&amp;migrate=1</em> to seed ~244 country kits on every live tenant DB.</li>
					<li>Click <em>Install all kits</em> on platform DB (catalog reference).</li>
					<li>Click <em>Migrate all live tenants</em> — assigns kit from tenant ERP country / portal settings / registry.</li>
					<li>Override platform or individual tenant kit above; customer country no longer changes tax.</li>
				</ol>
			</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
