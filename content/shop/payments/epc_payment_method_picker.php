<?php
/**
 * Storefront payment method picker (multi-gateway).
 * Expects $db_link. Outputs HTML + sets window.EPC_PAY_HANDLERS.
 */
if (!isset($db_link) || !($db_link instanceof PDO)) {
	return;
}
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_helpers.php';

$epcPayMethods = epc_payment_list_selectable($db_link);
$regionLabels = epc_payment_region_labels();
if (count($epcPayMethods) <= 1) {
	// Still expose for JS even with one method
}
?>
<div class="epc-pay-method-picker" style="margin:12px 0 16px;padding:12px 14px;border:1px solid #e2e8f0;border-radius:10px;background:#f8fafc;">
	<label for="epc_pay_handler" style="display:block;font-weight:700;font-size:13px;color:#0f172a;margin:0 0 8px;">Pay with</label>
	<select id="epc_pay_handler" class="form-control" style="max-width:420px;">
		<?php foreach ($epcPayMethods as $m):
			$region = $regionLabels[$m['region']] ?? $m['region'];
			$label = $m['name'] . ($m['region'] === 'crypto' ? ' · Crypto' : '') . ' (' . $region . ')';
		?>
		<option value="<?php echo epc_payment_h($m['handler']); ?>" <?php echo !empty($m['active']) ? 'selected' : ''; ?>>
			<?php echo epc_payment_h($label); ?>
		</option>
		<?php endforeach; ?>
	</select>
	<p style="margin:8px 0 0;font-size:12px;color:#64748b;">Cards, BNPL, wallets, Pakistan methods, and cryptocurrency when enabled in CP.</p>
</div>
<script>
window.EPC_PAY_HANDLERS = <?php echo json_encode(array_map(function ($m) {
	return array('handler' => $m['handler'], 'name' => $m['name'], 'region' => $m['region'], 'active' => (int)$m['active']);
}, $epcPayMethods)); ?>;
window.epcSelectedPayHandler = function () {
	var el = document.getElementById('epc_pay_handler');
	return el ? String(el.value || '') : '';
};
</script>
