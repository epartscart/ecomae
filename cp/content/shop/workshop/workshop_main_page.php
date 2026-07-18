<?php
/**
 * CP — Workshop & service desk (shop/workshop/workshop).
 */
if (!defined('_ASTEXE_')) {
	header('Location: /cp/shop/workshop/workshop', true, 302);
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

function epc_ws_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
$guideUrl = '/' . $backend . '/control/portal/epc_autoworkshop_guide';
$erpUrl = '/' . $backend . '/shop/finance/erp?epc_erp_shell=1';
$ordersUrl = '/' . $backend . '/shop/orders/orders';
$pricesUrl = '/' . $backend . '/shop/prices';
$storefront = rtrim((string) ($GLOBALS['DP_Config']->domain_path ?? ''), '/') . '/auto-workshop';

epc_cp_page_frame_open(array(
	'class' => 'epc-workshop-cp',
	'hero' => array(
		'badge' => 'Workshop',
		'title' => 'Workshop & service',
		'sub' => 'Operational desk for Auto Workshop Online — job cards, parts, labour, and handover.',
		'actions' => array(
			array('url' => $guideUrl, 'label' => 'Operator guide', 'icon' => 'fa-book', 'primary' => true),
			array('url' => $storefront, 'label' => 'View site', 'icon' => 'fa-external-link'),
			array('url' => $erpUrl, 'label' => 'Client ERP', 'icon' => 'fa-university'),
		),
	),
));
?>
<div class="epc-workshop-cp">
	<div class="row">
		<div class="col-md-4">
			<div class="hpanel">
				<div class="panel-body">
					<h4><i class="fa fa-car"></i> Check-in</h4>
					<p class="text-muted">Start from customer / vehicle context, then attach OEM parts from search or Laximo.</p>
					<a class="btn btn-default btn-sm" href="<?php echo epc_ws_h($ordersUrl); ?>">Open orders</a>
				</div>
			</div>
		</div>
		<div class="col-md-4">
			<div class="hpanel">
				<div class="panel-body">
					<h4><i class="fa fa-cogs"></i> Parts &amp; pricing</h4>
					<p class="text-muted">Pull stock and supplier prices into the job card before estimate approval.</p>
					<a class="btn btn-default btn-sm" href="<?php echo epc_ws_h($pricesUrl); ?>">Price lists</a>
				</div>
			</div>
		</div>
		<div class="col-md-4">
			<div class="hpanel">
				<div class="panel-body">
					<h4><i class="fa fa-file-text-o"></i> Invoice in ERP</h4>
					<p class="text-muted">Close the job with parts + labour; warranty lines follow the automotive workshop pack.</p>
					<a class="btn btn-default btn-sm" href="<?php echo epc_ws_h($erpUrl); ?>">Client ERP</a>
				</div>
			</div>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading"><h4>Next steps</h4></div>
		<div class="panel-body">
			<ol>
				<li>Read the <a href="<?php echo epc_ws_h($guideUrl); ?>">Auto Workshop Online guide</a>.</li>
				<li>Publish / review the storefront page at <a href="<?php echo epc_ws_h($storefront); ?>" target="_blank" rel="noopener">/auto-workshop</a>.</li>
				<li>Use Client ERP for labour rates, warranty provision, and final invoicing.</li>
			</ol>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
