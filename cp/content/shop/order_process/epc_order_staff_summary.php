<?php
/**
 * CP order page — staff summary (client block + margin totals), same data as admin e-mail.
 */
defined('_ASTEXE_') or die('No access');
if (empty($order) || empty($order_id)) {
	return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';

$crm_id = epc_crm_user_id_for_customer((int)$customer_id);
?>
<style>
.epc-order-staff { margin-bottom: 18px; padding: 14px 16px; background: #f8fafc; border: 1px solid #dce4ef; border-radius: 8px; }
.epc-order-staff h4 { margin: 0 0 10px; font-size: 15px; font-weight: 700; color: #172536; }
.epc-order-staff table.epc-kv td { padding: 4px 10px 4px 0; font-size: 13px; vertical-align: top; }
.epc-order-staff table.epc-kv td:first-child { font-weight: 700; width: 160px; color: #475569; }
.epc-order-staff .epc-totals { margin-top: 12px; text-align: right; font-size: 13px; }
.epc-order-staff .epc-totals div { margin: 4px 0; }
</style>
<div class="epc-order-staff">
	<h4>Order intelligence (staff)</h4>
	<?php
	echo epc_build_customer_profile_html((int)$customer_id, $order);
	if ($crm_id > 0) {
		echo '<p style="font-size:12px;color:#64748b;margin:8px 0 0;">Relationship manager user ID: <strong>' . (int)$crm_id . '</strong></p>';
	}
	?>
	<div class="epc-totals">
		<div>Sale (ex VAT): <strong><?php echo number_format($order_sale_sum_without_vat, 2, '.', ','); ?> AED</strong></div>
		<div>Purchase: <strong><?php echo number_format($order_purchase_sum_without_vat, 2, '.', ','); ?> AED</strong></div>
		<div>Margin: <strong style="color:#166534;"><?php echo number_format($order_profit_without_vat, 2, '.', ','); ?> AED</strong> (<?php echo number_format($order_margin_percent_without_vat, 2); ?>%)</div>
		<div>VAT 5% on sale: <strong><?php echo number_format($order_sale_sum_without_vat * 0.05, 2, '.', ','); ?> AED</strong></div>
	</div>
</div>
