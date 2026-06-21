<?php
/**
 * CP order card — WhatsApp share (customer, sales, supplier LPO).
 */
defined('_ASTEXE_') or die('No access');

if (empty($order) || empty($order_id) || !isset($db_link)) {
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';

global $DP_Config;
$items = epc_wa_order_items($db_link, (int)$order_id);
$salesDigits = epc_wa_sales_digits($DP_Config);

$customerPhone = '';
if (!empty($customer_profile['phone'])) {
	$customerPhone = epc_wa_digits((string)$customer_profile['phone']);
} elseif (!empty($order['phone_not_auth'])) {
	$customerPhone = epc_wa_digits((string)$order['phone_not_auth']);
}

$customerMsg = epc_wa_order_customer_message($DP_Config, (int)$order_id, $order, $items);
$salesMsg = epc_wa_order_sales_message($DP_Config, (int)$order_id, $order, $items, 'staff');
$customerHref = $customerPhone !== '' ? epc_wa_share_url($customerPhone, $customerMsg) : '';
$salesHref = epc_wa_share_url($salesDigits, $salesMsg);
$lpoGroups = epc_wa_order_lpo_groups($db_link, $DP_Config, (int)$order_id, $items);

echo epc_wa_styles();
?>
<div class="epc-wa-share-panel">
	<h5><i class="fa fa-whatsapp"></i> WhatsApp share</h5>
	<p class="text-muted">Sales line: <strong><?php echo epc_wa_h(epc_wa_sales_display($DP_Config)); ?></strong> — bilingual EN/AR messages.</p>
	<div class="epc-wa-share-row">
		<?php
		if ($customerHref !== '') {
			echo epc_wa_button($customerHref, 'Message customer', 'btn-success', 'Send order summary to customer on WhatsApp');
		} else {
			echo '<span class="text-muted" style="font-size:12px;">No customer phone — add phone on order or user profile.</span>';
		}
		echo epc_wa_button($salesHref, 'Share with sales', 'btn-success', 'Order summary to sales WhatsApp');
		?>
	</div>
	<?php if (!empty($lpoGroups)) { ?>
	<div style="margin-top:10px;">
		<strong style="font-size:12px;color:#475569;">Supplier LPO (share text):</strong>
		<div class="epc-wa-share-row">
			<?php foreach ($lpoGroups as $g) {
				$label = 'LPO: ' . $g['storage_name'];
				if ($g['target_label'] === 'supplier') {
					$label .= ' → supplier';
				}
				echo epc_wa_button($g['wa_href'], $label, 'btn-default', $g['lpo_message']);
			} ?>
		</div>
	</div>
	<?php } ?>
</div>
