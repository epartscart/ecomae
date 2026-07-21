<?php
/**
 * CP — Auto Workshop / Garage operator guide.
 * CMS: /cp/control/portal/epc_autoworkshop_guide
 */
if (!defined('_ASTEXE_')) {
	$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== ''
		? ('?' . $_SERVER['QUERY_STRING'])
		: '';
	header('Location: /cp/control/portal/epc_autoworkshop_guide' . $qs, true, 302);
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

function epc_awg_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
$domain = rtrim((string) ($GLOBALS['DP_Config']->domain_path ?? ''), '/');
$storefrontUrl = ($domain !== '' ? $domain : '') . '/auto-workshop';
$workshopCpUrl = '/' . $backend . '/shop/workshop/workshop';
$checkinUrl = $workshopCpUrl . '?tab=checkin';
$boardUrl = $workshopCpUrl . '?tab=board';
$erpUrl = '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
$katalogUrl = '/katalog-laximo';
$ordersUrl = '/' . $backend . '/shop/orders/orders';
$smsUrl = '/' . $backend . '/control/sms-operatory';
$commsUrl = '/' . $backend . '/control/communications';

epc_cp_page_frame_open(array(
	'class' => 'epc-autoworkshop-guide',
	'hero' => array(
		'badge' => 'Garage management',
		'title' => 'Auto Workshop Online — operator guide',
		'sub' => 'Professional repair workshop: check-in → job card (parts + labour) → estimate → bay/tech → QC → ready → handover. Linked to storefront booking and Client ERP.',
		'actions' => array(
			array('url' => $boardUrl, 'label' => 'Open floor board', 'icon' => 'fa-th-large', 'primary' => true),
			array('url' => $checkinUrl, 'label' => 'New check-in', 'icon' => 'fa-car'),
			array('url' => $storefrontUrl, 'label' => 'Public site', 'icon' => 'fa-external-link'),
		),
	),
));
?>
<style>
.epc-awg{--ink:#0f172a;--muted:#64748b;--line:#e2e8f0;--accent:#0e7490}
.epc-awg-flow{display:flex;flex-wrap:wrap;gap:8px;margin:0;padding:0;list-style:none}
.epc-awg-flow li{background:#ecfeff;border:1px solid #a5f3fc;border-radius:999px;padding:6px 12px;font-size:12px;font-weight:600;color:#155e75}
.epc-awg-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:12px;margin:0 0 14px}
.epc-awg-card{background:#fff;border:1px solid var(--line);border-radius:12px;padding:14px 16px;box-shadow:0 4px 14px rgba(15,23,42,.04)}
.epc-awg-card h4{margin:0 0 8px;font-size:14px;color:var(--ink)}
.epc-awg-card p,.epc-awg-card li{font-size:12px;color:#334155;line-height:1.5;margin:0}
.epc-awg-card ol{margin:0;padding-left:18px}
.epc-awg-num{display:inline-flex;width:22px;height:22px;border-radius:50%;background:var(--accent);color:#fff;font-size:11px;font-weight:700;align-items:center;justify-content:center;margin-right:6px}
.epc-awg-panel{background:#fff;border:1px solid var(--line);border-radius:12px;padding:14px 16px;margin:0 0 14px}
.epc-awg-panel h3{margin:0 0 10px;font-size:15px}
</style>

<div class="epc-awg">
	<div class="epc-awg-panel">
		<h3>Process flow</h3>
		<ul class="epc-awg-flow">
			<li>1 Check-in</li>
			<li>2 Estimate</li>
			<li>3 Approved</li>
			<li>4 In progress</li>
			<li>5 QC</li>
			<li>6 Ready</li>
			<li>7 Delivered</li>
		</ul>
	</div>

	<div class="epc-awg-panel">
		<h3>Quick links</h3>
		<a class="btn btn-primary btn-sm" href="<?php echo epc_awg_h($boardUrl); ?>"><i class="fa fa-th-large"></i> Floor board</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($checkinUrl); ?>"><i class="fa fa-car"></i> Check-in</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($storefrontUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-globe"></i> Book / track (site)</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($katalogUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-search"></i> OEM catalog</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Parts orders</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($erpUrl); ?>"><i class="fa fa-university"></i> Client ERP</a>
		<a class="btn btn-default btn-sm" href="<?php echo epc_awg_h($commsUrl); ?>"><i class="fa fa-envelope"></i> Test e-mail/SMS</a>
	</div>

	<div class="epc-awg-grid">
		<div class="epc-awg-card">
			<h4><span class="epc-awg-num">1</span>Open the desk</h4>
			<p>Go to <strong>Workshop &amp; service</strong>. The floor board shows every open job by status. Empty desks auto-load UAE demo jobs so you can click through immediately.</p>
		</div>
		<div class="epc-awg-card">
			<h4><span class="epc-awg-num">2</span>Check in the vehicle</h4>
			<p>Use <strong>Check-in</strong>: plate, customer phone, complaint, optional VIN/make/model/odometer. Assign bay + technician when known. Optional first labour/part lines.</p>
		</div>
		<div class="epc-awg-card">
			<h4><span class="epc-awg-num">3</span>Build the job card</h4>
			<p>Click a card → add parts and labour (hours × rate). VAT defaults to 5%. Totals recalculate automatically. Move status to Estimate / Approved.</p>
		</div>
		<div class="epc-awg-card">
			<h4><span class="epc-awg-num">4</span>Repair → QC → ready</h4>
			<p>Set <strong>In progress</strong> while work runs, <strong>QC</strong> for final check, then <strong>Ready</strong> for collection. Mark <strong>Delivered</strong> after handover.</p>
		</div>
		<div class="epc-awg-card">
			<h4><span class="epc-awg-num">5</span>Customer booking</h4>
			<p>Public page <code>/auto-workshop</code> lets customers request service and track by job number or plate. New bookings land in Check-in.</p>
		</div>
		<div class="epc-awg-card">
			<h4><span class="epc-awg-num">6</span>Parts &amp; invoice</h4>
			<p>Pull OEM fitment via Laximo / Parts search; raise supplier orders from Orders. Job card holds the billable total — post formal invoice in Client ERP when needed. Notify via Communications / SMS Operators.</p>
		</div>
	</div>

	<div class="epc-awg-panel">
		<h3>What the module covers</h3>
		<div class="row">
			<div class="col-md-6">
				<ul>
					<li>Kanban floor board (garage statuses)</li>
					<li>Job cards with parts + labour + VAT</li>
					<li>Bay / ramp and technician assignment</li>
					<li>Vehicle plate / VIN / odometer context</li>
				</ul>
			</div>
			<div class="col-md-6">
				<ul>
					<li>Storefront book + track</li>
					<li>Demo seed for training</li>
					<li>Links to OEM catalog, orders, ERP</li>
					<li>Ready for e-mail/SMS customer updates</li>
				</ul>
			</div>
		</div>
		<p class="text-muted" style="margin:12px 0 0;font-size:12px">
			Industry pack: <code>automotive_workshop</code> ·
			CP: <code>/<?php echo epc_awg_h($backend); ?>/shop/workshop/workshop</code> ·
			Site: <code>/auto-workshop</code>
		</p>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
