<?php
/**
 * Super CP — Platform health checkup (professional operator report).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
if (!$isSuper) {
	echo '<div class="alert alert-warning">Platform health checkup is available on <strong>Super CP</strong> (www.ecomae.com) only.</div>';
	return;
}

function epc_phc_h($v)
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$token = function_exists('epc_deploy_token') ? epc_deploy_token() : 'epartscart-deploy-2026';
if (!function_exists('epc_deploy_token')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
	$token = epc_deploy_token();
}
$apiUrl = 'https://www.ecomae.com/epc-platform-health-checkup-api.php?token=' . rawurlencode($token);
$lastRunKey = 'epc_platform_health_checkup_last_run';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array('class' => 'epc-phc'));
?>
<div class="epc-portal-settings epc-phc" id="epc_phc_root">
	<div class="hpanel">
		<div class="panel-heading">
			<h2><i class="fas fa-heartbeat"></i> Platform health checkup</h2>
			<p class="text-muted">Tenant URLs, SSL, ERP DB isolation, nginx, backups, indexing, and VPS hints — run on demand for go-live and weekly ops.</p>
		</div>
		<div class="panel-body">
			<div class="epc-phc__toolbar">
				<button type="button" class="btn btn-primary" id="epc_phc_run"><i class="fas fa-play"></i> Run checkup</button>
				<button type="button" class="btn btn-default" id="epc_phc_export_csv"><i class="fas fa-download"></i> Export CSV</button>
				<span class="epc-phc__status" id="epc_phc_last_run">Last run: —</span>
				<span id="epc_phc_loading"><i class="fas fa-spinner fa-spin"></i> Running probes…</span>
			</div>
			<div id="epc_phc_results">
				<p class="text-muted">Click <strong>Run checkup</strong> to load the latest report from the platform API.</p>
			</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
