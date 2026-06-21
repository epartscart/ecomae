<?php
/**
 * Super CP — Platform governance (policies, rules, system protocol health).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_governance.php';

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
if (!$isSuper) {
	echo '<div class="alert alert-warning">Platform governance is available on <strong>Super CP</strong> (www.ecomae.com) only.</div>';
	return;
}

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	global $DP_Config;
	try {
		$pdo = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Platform database unavailable.</div>';
		return;
	}
}

epc_platform_governance_seed($pdo);
$rules = epc_platform_governance_list_rules($pdo);
$categories = epc_platform_governance_categories();
$byCat = array();
foreach ($rules as $r) {
	$cat = (string) ($r['category'] ?? 'tenant');
	if (!isset($byCat[$cat])) {
		$byCat[$cat] = array();
	}
	$byCat[$cat][] = $r;
}

function epc_pg_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = $GLOBALS['DP_Config']->backend_dir;
$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_platform_governance.php';
$token = function_exists('epc_deploy_token') ? epc_deploy_token() : 'epartscart-deploy-2026';
if (!function_exists('epc_deploy_token')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
	$token = epc_deploy_token();
}
$healthApi = 'https://www.ecomae.com/epc-platform-governance-health-api.php?token=' . rawurlencode($token);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array('class' => 'epc-pg'));
?>
<div class="epc-portal-settings epc-pg" id="epc_pg_root">
	<div class="hpanel">
		<div class="panel-heading">
			<h2><i class="fas fa-gavel"></i> Platform governance</h2>
			<p class="text-muted">System protocol, principles, and enforcement rules for tenants, API, ERP-only, demos, branding, and tax compliance.</p>
		</div>
		<div class="panel-body">
			<div class="epc-pg__tabs">
				<button type="button" class="btn btn-primary epc-pg-tab" data-tab="rules">Rules &amp; policies</button>
				<button type="button" class="btn btn-default epc-pg-tab" data-tab="protocol">System protocol</button>
			</div>

			<div id="epc_pg_tab_rules">
				<p class="text-muted"><?php echo count($rules); ?> rules in platform DB · seeded defaults apply on first load.</p>
				<?php foreach ($categories as $code => $meta) {
					if (empty($byCat[$code])) {
						continue;
					}
					?>
				<section style="margin-bottom: 24px;">
					<h3><i class="fa <?php echo epc_pg_h($meta['icon']); ?>"></i> <?php echo epc_pg_h($meta['label']); ?></h3>
					<table>
						<thead>
							<tr>
								<th>Active</th>
								<th>Rule</th>
								<th>Enforcement</th>
								<th>Scope</th>
								<th>Module</th>
							</tr>
						</thead>
						<tbody>
						<?php foreach ($byCat[$code] as $rule) {
							$enf = (string) $rule['enforcement'];
							$badge = $enf === 'advisory' ? 'badge-adv' : ($enf === 'blocked' ? 'badge-blk' : 'badge-req');
							$link = (string) ($rule['module_link'] ?? '');
							?>
							<tr data-rule-key="<?php echo epc_pg_h($rule['rule_key']); ?>">
								<td>
									<input type="checkbox" class="epc-pg-active" <?php echo !empty($rule['active']) ? 'checked' : ''; ?>>
								</td>
								<td>
									<strong><?php echo epc_pg_h($rule['title']); ?></strong><br>
									<small class="text-muted"><?php echo epc_pg_h($rule['description']); ?></small>
								</td>
								<td>
									<select class="form-control input-sm epc-pg-enforcement" style="max-width:120px">
										<?php foreach (epc_platform_governance_enforcement_levels() as $lv) { ?>
										<option value="<?php echo epc_pg_h($lv); ?>"<?php echo $enf === $lv ? ' selected' : ''; ?>><?php echo epc_pg_h($lv); ?></option>
										<?php } ?>
									</select>
									<span class="<?php echo $badge; ?>"><?php echo epc_pg_h($enf); ?></span>
								</td>
								<td><code><?php echo epc_pg_h($rule['scope']); ?></code></td>
								<td>
									<?php if ($link !== '') { ?>
									<a href="<?php echo epc_pg_h($link); ?>" target="_blank" rel="noopener">Open</a>
									<?php } else { ?>
									—
									<?php } ?>
								</td>
							</tr>
						<?php } ?>
						</tbody>
					</table>
				</section>
				<?php } ?>
			</div>

			<div id="epc_pg_tab_protocol" style="display:none">
				<p>Startup checks: CP login, FTA legislation reachability, catalog proxy JSON, and all platform surfaces.</p>
				<button type="button" class="btn btn-primary" id="epc_pg_run_health"><i class="fas fa-play"></i> Run health check</button>
				<span id="epc_pg_health_loading" style="display:none;margin-left:10px"><i class="fas fa-spinner fa-spin"></i> Probing…</span>
				<div id="epc_pg_health_results" class="epc-pg__health-grid"></div>
			</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
