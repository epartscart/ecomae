<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';

$catalog = epc_channel_carriers_catalog();
$cookie_key = 'how_get_epc_carriers';
$how_get = array('carrier' => 'dhl', 'service' => 'EXPRESS', 'city' => 'Dubai', 'country' => 'AE', 'address' => '', 'phone' => '', 'weight_kg' => '1.5', 'rate' => 0);
if (isset($_COOKIE[$cookie_key])) {
	$decoded = json_decode($_COOKIE[$cookie_key], true);
	if (is_array($decoded)) {
		$how_get = array_merge($how_get, $decoded);
	}
}
$demo_rate = epc_channel_demo_rate($how_get['carrier'], (float)$how_get['weight_kg'], $how_get['country']);
?>
<style>
.epc-carrier-rates { margin: 12px 0; }
.epc-carrier-rates label { display: block; padding: 8px 12px; border: 1px solid #dce4ef; border-radius: 8px; margin-bottom: 8px; cursor: pointer; }
.epc-carrier-rates label.active { border-color: #2563eb; background: #eff6ff; }
.epc-carrier-rates input { margin-right: 8px; }
</style>
<p class="lead">International carriers — DHL, FedEx, Aramex, UPS</p>
<p class="text-muted">Demo rates shown until live API keys are configured in CP → Logistics → Carriers.</p>

<table class="table">
<tr><td>Destination city</td><td><input class="form-control" id="epc_c_city" type="text" value="<?php echo epc_channel_h($how_get['city']); ?>"></td></tr>
<tr><td>Country (ISO)</td><td><input class="form-control" id="epc_c_country" type="text" value="<?php echo epc_channel_h($how_get['country']); ?>" maxlength="2"></td></tr>
<tr><td>Address</td><td><input class="form-control" id="epc_c_address" type="text" value="<?php echo epc_channel_h($how_get['address']); ?>"></td></tr>
<tr><td>Phone</td><td><input class="form-control" id="epc_c_phone" type="text" value="<?php echo epc_channel_h($how_get['phone']); ?>"></td></tr>
<tr><td>Weight (kg)</td><td><input class="form-control" id="epc_c_weight" type="number" step="0.1" min="0.1" value="<?php echo epc_channel_h($how_get['weight_kg']); ?>" onchange="epcCarrierRecalc()"></td></tr>
</table>

<div class="epc-carrier-rates" id="epc_carrier_rates">
<?php foreach ($catalog as $code => $c): ?>
	<?php foreach ($c['services'] as $svc => $svc_name): ?>
		<?php $rate = epc_channel_demo_rate($code, (float)$how_get['weight_kg'], $how_get['country']); ?>
		<label class="<?php echo ($how_get['carrier'] === $code && $how_get['service'] === $svc) ? 'active' : ''; ?>">
			<input type="radio" name="epc_carrier_pick" value="<?php echo epc_channel_h($code . '|' . $svc); ?>"
				<?php echo ($how_get['carrier'] === $code && $how_get['service'] === $svc) ? 'checked' : ''; ?>
				onchange="epcCarrierPick(this)">
			<strong><?php echo epc_channel_h($c['name']); ?></strong> — <?php echo epc_channel_h($svc_name); ?>
			<span class="pull-right"><?php echo epc_channel_money($rate); ?> AED</span>
		</label>
	<?php endforeach; ?>
<?php endforeach; ?>
</div>

<?php require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/users_agreement_module.php'; ?>
<div class="text-center">
	<a class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="epcCarrierNext();">Continue</a>
</div>
<script>
function epcCarrierPick(el) {
	document.querySelectorAll('.epc-carrier-rates label').forEach(function(l){ l.classList.remove('active'); });
	el.closest('label').classList.add('active');
}
function epcCarrierRecalc() { /* rates refresh on next page load */ }
function epcCarrierNext() {
	if (typeof check_user_agreement === 'function' && !check_user_agreement()) return;
	var pick = document.querySelector('input[name="epc_carrier_pick"]:checked');
	if (!pick) { alert('Select a carrier service'); return; }
	var parts = pick.value.split('|');
	var how_get = {
		mode: <?php echo (int)$current_obtain_mode; ?>,
		carrier: parts[0],
		service: parts[1],
		city: encodeURIComponent(document.getElementById('epc_c_city').value),
		country: encodeURIComponent(document.getElementById('epc_c_country').value),
		address: encodeURIComponent(document.getElementById('epc_c_address').value),
		phone: encodeURIComponent(document.getElementById('epc_c_phone').value),
		weight_kg: document.getElementById('epc_c_weight').value
	};
	document.cookie = 'how_get_epc_carriers=' + encodeURIComponent(JSON.stringify(how_get)) + '; path=/; max-age=86400';
	if (typeof next_step_checkout === 'function') { next_step_checkout(); }
	else { location.href = '<?php echo isset($multilang_params["lang_href"]) ? $multilang_params["lang_href"] : ""; ?>/shop/checkout_confirm'; }
}
</script>
