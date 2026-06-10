<?php
/**
 * Procurement — step-by-step guide.
 */
defined('_ASTEXE_') or die('No access');

$dash = epc_procurement_dashboard($db_link);
?>

<div class="epc-proc-note">
	<strong>Procurement panel</strong> handles suppliers, purchase bills, payments, and advances.
	<strong>Warehouse/storages</strong> are separate — they hold price lists and stock, not legal supplier data.
</div>

<h4><i class="fa fa-sitemap"></i> End-to-end procurement flow</h4>
<ol>
	<li><strong>Supplier master</strong> — Tab <em>Suppliers</em>: legal name, TRN, country, address, payment terms. Required for UAE input VAT on purchases.</li>
	<li><strong>Price source</strong> — Parts prices come from warehouse price lists (<a href="<?php echo epc_proc_h($priceUrl); ?>">Price management</a>). Link warehouse optionally on supplier profile.</li>
	<li><strong>Purchase bill</strong> — Tab <em>Purchase bills</em>: record supplier invoice (ex VAT). VAT <?php echo epc_proc_h(number_format($vatRate, 2)); ?>% added for UAE VAT-registered suppliers.</li>
	<li><strong>Advance payment</strong> — Tab <em>Advances</em>: pay supplier before goods/invoice (prepayment).</li>
	<li><strong>Payment</strong> — Tab <em>Payments</em>: settle payable balance when invoice is due.</li>
	<li><strong>Fulfillment</strong> — Tab <em>Fulfillment</em> + <a href="<?php echo epc_proc_h($erpUrl); ?>?tab=fulfilment">ERP Fulfilment</a>: supplier paid → goods in → deliver to customer.</li>
	<li><strong>GL / VAT</strong> — Purchases post to ERP payables and UAE VAT input. See <a href="<?php echo epc_proc_h($erpUrl); ?>?tab=vat_return">ERP UAE VAT</a>.</li>
</ol>

<h4><i class="fa fa-warehouse"></i> Warehouse vs supplier</h4>
<table class="table table-bordered table-condensed">
	<thead><tr><th>Warehouse (storage)</th><th>Supplier (procurement)</th></tr></thead>
	<tbody>
		<tr><td>Price list, stock location, catalog source</td><td>Legal entity, TRN, purchase invoice, payable</td></tr>
		<tr><td><a href="<?php echo epc_proc_h($storagesUrl); ?>">Logistics → Storages</a></td><td>This panel → Suppliers</td></tr>
		<tr><td>Many warehouses possible</td><td>One supplier record per vendor; optional warehouse link</td></tr>
	</tbody>
</table>

<h4><i class="fa fa-check-square-o"></i> UAE e-invoicing (purchase side)</h4>
<p>For B2B purchases, ensure supplier TRN and address are complete on the supplier profile.
Seller-side e-invoices for your sales are in <a href="<?php echo epc_proc_h($erpUrl); ?>?tab=einvoice">ERP → E-Invoicing</a>.</p>

<h4>Live snapshot</h4>
<ul>
	<li>Suppliers: <?php echo (int)$dash['suppliers']; ?> (<?php echo (int)$dash['suppliers_with_trn']; ?> with TRN)</li>
	<li>Purchase bills: <?php echo (int)$dash['purchase_invoices']; ?></li>
	<li>Payable: <?php echo epc_proc_money($dash['payable_balance']); ?> AED</li>
	<li>Advances: <?php echo epc_proc_money($dash['advances_paid']); ?> AED</li>
</ul>
