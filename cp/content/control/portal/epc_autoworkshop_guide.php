<?php
/**
 * CP — Auto Workshop Online operator guide (Portal / Config).
 *
 * CMS route: /cp/control/portal/epc_autoworkshop_guide
 */
if (!defined('_ASTEXE_')) {
	$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== ''
		? ('?' . $_SERVER['QUERY_STRING'])
		: '';
	header('Location: /cp/control/portal/epc_autoworkshop_guide' . $qs, true, 302);
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

function epc_awg_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
$domain = rtrim((string) ($GLOBALS['DP_Config']->domain_path ?? '/'), '/');
if ($domain === '') {
	$domain = '';
}
$storefrontUrl = ($domain !== '' ? $domain : '') . '/auto-workshop';
$workshopCpUrl = '/' . $backend . '/shop/workshop/workshop';
$erpUrl = '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
$partsUrl = '/' . $backend . '/shop/orders/orders';
$katalogUrl = '/katalog-laximo';

$steps = array(
	array(
		'title' => 'Open the workshop desk',
		'body' => 'Use <strong>Workshop &amp; service</strong> in Control Panel for day-to-day job cards, or open the public <strong>Auto Workshop Online</strong> page for customers and advisors.',
	),
	array(
		'title' => 'Check in the vehicle',
		'body' => 'Capture plate / VIN, customer, odometer, and complaint. VIN decode (Laximo) helps lock the correct catalogue before parts are pulled.',
	),
	array(
		'title' => 'Build the job card',
		'body' => 'Add labour lines (hours × rate) and parts from warehouse / OEM search. Send an estimate for approval before work starts when required.',
	),
	array(
		'title' => 'Repair → QC → invoice',
		'body' => 'Track status through repair and QC. Invoice parts + labour; warranty jobs can post to a warranty provision in Client ERP.',
	),
	array(
		'title' => 'Keep history',
		'body' => 'Vehicle history stays on the job card trail so the next visit starts with prior parts, labour, and warranty notes.',
	),
);

epc_cp_page_frame_open(array(
	'class' => 'epc-autoworkshop-guide',
	'hero' => array(
		'badge' => 'Auto Workshop Online',
		'title' => 'Workshop & service — operator guide',
		'sub' => 'Vehicle check-in → job card (parts + labour) → estimate → repair → QC → invoice. Linked to your storefront catalogue and Client ERP.',
		'actions' => array(
			array('url' => $storefrontUrl, 'label' => 'View site', 'icon' => 'fa-external-link', 'primary' => true),
			array('url' => $workshopCpUrl, 'label' => 'Workshop CP', 'icon' => 'fa-wrench'),
			array('url' => $erpUrl, 'label' => 'Client ERP', 'icon' => 'fa-university'),
		),
	),
));
?>
<style>
.epc-awg-step{border:1px solid #e2e8f0;border-radius:10px;padding:16px 18px;margin:0 0 12px;background:#fff}
.epc-awg-step h4{margin:0 0 8px;font-size:15px;color:#0f172a}
.epc-awg-step p{margin:0;color:#334155;line-height:1.55}
.epc-awg-num{display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border-radius:50%;background:#0ea5e9;color:#fff;font-weight:700;font-size:13px;margin-right:8px}
.epc-awg-flow{display:flex;flex-wrap:wrap;gap:8px;margin:0;padding:0;list-style:none}
.epc-awg-flow li{background:#f1f5f9;border:1px solid #e2e8f0;border-radius:999px;padding:6px 12px;font-size:12px;color:#334155}
</style>

<div class="epc-portal-settings epc-autoworkshop-guide">
	<div class="hpanel">
		<div class="panel-heading"><h4>Process flow</h4></div>
		<div class="panel-body">
			<ul class="epc-awg-flow">
				<li>Vehicle check-in</li>
				<li>Job card (parts + labour)</li>
				<li>Estimate approval</li>
				<li>Repair</li>
				<li>QC &amp; test</li>
				<li>Invoice + warranty</li>
			</ul>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Quick links</h4></div>
		<div class="panel-body">
			<a class="btn btn-primary btn-sm" href="<?php echo epc_awg_h($storefrontUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> View site — Auto Workshop Online</a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($workshopCpUrl); ?>"><i class="fa fa-wrench"></i> Workshop &amp; service (CP)</a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($erpUrl); ?>"><i class="fa fa-university"></i> Client ERP</a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($katalogUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-car"></i> OEM catalog (Laximo)</a>
			<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($partsUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Step-by-step</h4></div>
		<div class="panel-body">
			<?php foreach ($steps as $i => $step) { ?>
			<div class="epc-awg-step">
				<h4><span class="epc-awg-num"><?php echo (int) ($i + 1); ?></span><?php echo epc_awg_h($step['title']); ?></h4>
				<p><?php echo $step['body']; ?></p>
			</div>
			<?php } ?>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>What this module covers</h4></div>
		<div class="panel-body">
			<div class="row">
				<div class="col-md-6">
					<ul>
						<li>Job cards with parts + labour</li>
						<li>Vehicle / VIN context for fitment</li>
						<li>Estimate approval before work</li>
						<li>Link to warehouse stock &amp; OEM search</li>
					</ul>
				</div>
				<div class="col-md-6">
					<ul>
						<li>QC &amp; delivery status</li>
						<li>Invoice into Client ERP</li>
						<li>Warranty / sublet notes</li>
						<li>Service history per vehicle</li>
					</ul>
				</div>
			</div>
			<p class="text-muted" style="margin:12px 0 0">
				Industry pack: <code>automotive_workshop</code> (garage process + labour hours + warranty).
				Storefront URL: <code>/auto-workshop</code> · CP: <code>/<?php echo $backend; ?>/shop/workshop/workshop</code>
			</p>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
