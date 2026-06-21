<?php
/**
 * Customer management — guide.
 */
defined('_ASTEXE_') or die('No access');
?>

<h4><i class="fa fa-sitemap"></i> Customer lifecycle</h4>
<ol>
	<li><strong>Registration</strong> — Customer registers on shop. B2B: approve via <a href="<?php echo epc_cm_h($approvalsUrl); ?>">Approvals</a> tab.</li>
	<li><strong>Customer profile</strong> — Tab <em>Customers</em>: buyer name, TRN, address, Peppol endpoint (UAE e-invoicing mandatory fields for B2B).</li>
	<li><strong>Orders</strong> — Tab <em>Orders</em> or <a href="<?php echo epc_cm_h($ordersUrl); ?>">CP Orders</a>. Sale prices ex VAT; 5% output VAT on UAE sales.</li>
	<li><strong>Advance payment</strong> — Tab <em>Advances</em>: record customer prepayment (credit on customer ledger).</li>
	<li><strong>Tax invoice</strong> — Tab <em>Invoices</em>: generate UAE e-invoice (PINT-AE) from order. Full ASP submission in <a href="<?php echo epc_cm_h($erpUrl); ?>?tab=einvoice">ERP E-Invoicing</a>.</li>
	<li><strong>Returns</strong> — Tab <em>Returns</em>: view return requests; process in Orders CP.</li>
</ol>

<h4><i class="fa fa-file-text-o"></i> Mandatory e-invoice buyer fields</h4>
<p>For B2B UAE customers, complete on the customer profile: buyer name, TRN, legal registration, address line 1, city, emirate, country AE, Peppol electronic address (0235:TIN).</p>

<h4>Where this differs from shop menus</h4>
<p>Customer-related settings were scattered across Users, Orders, and Finance. This panel centralises customer master data, orders overview, invoices, advances, and returns in one place.</p>
