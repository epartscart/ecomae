<?php
defined('_ASTEXE_') or die('No access');
?>

<style>
.epc-logistics-grid {
	display: grid;
	gap: 14px;
	grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
}
.epc-logistics-card {
	align-items: flex-start;
	background: #fff;
	border: 1px solid #e5e7eb;
	border-radius: 10px;
	box-shadow: 0 8px 22px rgba(15, 23, 42, .06);
	color: #1f2937;
	display: flex;
	gap: 14px;
	min-height: 112px;
	padding: 18px;
	text-decoration: none !important;
	transition: transform .15s ease, box-shadow .15s ease, border-color .15s ease;
}
.epc-logistics-card:hover,
.epc-logistics-card:focus {
	border-color: #2563eb;
	box-shadow: 0 12px 28px rgba(37, 99, 235, .12);
	color: #111827;
	transform: translateY(-1px);
}
.epc-logistics-icon {
	align-items: center;
	border-radius: 12px;
	color: #fff;
	display: inline-flex;
	flex: 0 0 52px;
	font-size: 24px;
	height: 52px;
	justify-content: center;
	width: 52px;
}
.epc-logistics-card strong {
	display: block;
	font-size: 15px;
	margin: 2px 0 7px;
}
.epc-logistics-card span {
	color: #64748b;
	display: block;
	font-size: 12px;
	line-height: 1.45;
}
</style>

<div class="hpanel">
	<div class="panel-heading hbuilt">
		Logistics — delivery &amp; fulfilment
		<span class="pull-right">
			<a class="btn btn-primary btn-xs" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/guide"><i class="fa fa-book"></i> Guide</a>
			<a class="btn btn-default btn-xs" href="/<?php echo $DP_Config->backend_dir; ?>/shop/channels/guide"><i class="fa fa-plug"></i> Channels guide</a>
		</span>
	</div>
	<div class="panel-body">
		<p class="text-muted">Warehouses, stock, delivery methods, and international carriers for <strong>all customer orders</strong> (website checkout and imported marketplace orders).</p>
		<div class="epc-logistics-grid">
			<a class="epc-logistics-card" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/carriers">
				<i class="epc-logistics-icon fas fa-shipping-fast" style="background:#0f766e;"></i>
				<span><strong>Carriers &amp; shipments</strong>20+ worldwide partners — rates at checkout and labels from order card.</span>
			</a>
			<a class="epc-logistics-card" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/offices">
				<i class="epc-logistics-icon fas fa-store" style="background:#2563eb;"></i>
				<span><strong>Shops / pickup points</strong>Manage pickup points, addresses, phones, warehouse links, and geo settings.</span>
			</a>
			<a class="epc-logistics-card" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/storages">
				<i class="epc-logistics-icon fas fa-warehouse" style="background:#0f766e;"></i>
				<span><strong>Warehouses</strong>Create own stock, price-list warehouses, supplier API, 1C/ERP style interfaces.</span>
			</a>
			<a class="epc-logistics-card" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/sposoby-polucheniya">
				<i class="epc-logistics-icon fas fa-truck" style="background:#ea580c;"></i>
				<span><strong>Delivery methods</strong>Configure pickup, address delivery, SDEK, DPD, Boxberry, and simple delivery.</span>
			</a>
			<a class="epc-logistics-card" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/stock">
				<i class="epc-logistics-icon fas fa-pallet" style="background:#7c3aed;"></i>
				<span><strong>Stock management</strong>Edit availability, prices, and warehouse stock records for catalog products.</span>
			</a>
			<a class="epc-logistics-card" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/storages/groups">
				<i class="epc-logistics-icon fas fa-layer-group" style="background:#0891b2;"></i>
				<span><strong>Warehouse groups</strong>Group warehouses for easier management and display control.</span>
			</a>
			<a class="epc-logistics-card" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices">
				<i class="epc-logistics-icon fas fa-file-upload" style="background:#16a34a;"></i>
				<span><strong>Price list manager</strong>Upload and manage supplier price lists used by warehouse interfaces.</span>
			</a>
		</div>
	</div>
</div>
