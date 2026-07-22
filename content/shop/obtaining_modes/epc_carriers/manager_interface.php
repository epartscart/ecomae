<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';
$catalog = epc_channel_carriers_catalog();
$carrier = isset($how_get_json['carrier']) ? (string)$how_get_json['carrier'] : 'dhl';
$cname = isset($catalog[$carrier]['name']) ? $catalog[$carrier]['name'] : strtoupper($carrier);
$order_id = isset($order_record['id']) ? (int)$order_record['id'] : 0;
$shipments = array();
if ($order_id > 0 && isset($db_link) && $db_link instanceof PDO) {
	epc_channel_ensure_schema($db_link);
	$st = $db_link->prepare('SELECT * FROM `epc_carrier_shipments` WHERE `order_id` = ? ORDER BY `id` DESC');
	$st->execute(array($order_id));
	$shipments = $st->fetchAll(PDO::FETCH_ASSOC);
}
?>
<p><b><?php echo epc_channel_h(translate_str_by_id($obtain_mode['caption'])); ?></b> — <?php echo epc_channel_h($cname); ?></p>
<table class="table table-condensed">
<tr><td><?php echo epc_channel_h($how_get_json['city'] ?? ''); ?>, <?php echo epc_channel_h($how_get_json['country'] ?? ''); ?></td></tr>
<tr><td><?php echo epc_channel_h($how_get_json['address'] ?? ''); ?></td></tr>
<tr><td><?php echo epc_channel_h($how_get_json['phone'] ?? ''); ?></td></tr>
</table>

<?php if (!empty($shipments)): ?>
<table class="table table-bordered table-condensed">
<thead><tr><th>Carrier</th><th>Tracking</th><th>Status</th><th>Cost</th></tr></thead>
<tbody>
<?php foreach ($shipments as $s): ?>
<tr>
	<td><?php echo epc_channel_h(strtoupper($s['carrier_code'])); ?></td>
	<td><?php if ($s['label_url']): ?><a href="<?php echo epc_channel_h($s['label_url']); ?>" target="_blank"><?php echo epc_channel_h($s['tracking_number']); ?></a><?php else: ?><?php echo epc_channel_h($s['tracking_number']); ?><?php endif; ?></td>
	<td><?php echo epc_channel_h($s['status']); ?></td>
	<td><?php echo epc_channel_money($s['cost']); ?> <?php echo epc_channel_h($s['currency']); ?></td>
</tr>
<?php endforeach; ?>
</tbody>
</table>
<?php else: ?>
<div class="well well-sm">
	<p>Create a demo shipment label (worldwide carriers — DHL, FedEx, Aramex, UPS, and more):</p>
	<form id="epc_cp_create_shipment" class="form-inline">
		<input type="hidden" name="order_id" value="<?php echo $order_id; ?>">
		<select name="carrier_code" class="form-control input-sm">
			<?php foreach ($catalog as $code => $c): ?>
				<option value="<?php echo epc_channel_h($code); ?>" <?php echo $code === $carrier ? 'selected' : ''; ?>><?php echo epc_channel_h($c['name']); ?></option>
			<?php endforeach; ?>
		</select>
		<input type="number" step="0.1" name="weight_kg" class="form-control input-sm" value="<?php echo epc_channel_h($how_get_json['weight_kg'] ?? '1.5'); ?>" placeholder="kg">
		<button type="submit" class="btn btn-sm btn-primary">Create demo label</button>
	</form>
	<div id="epc_ship_msg" class="text-muted" style="margin-top:8px;"></div>
</div>
<script>
(function(){
	var f = document.getElementById('epc_cp_create_shipment');
	if (!f) return;
	f.addEventListener('submit', function(ev){
		ev.preventDefault();
		var fd = new FormData(f);
		fd.append('action', 'create_shipment');
		fetch('/<?php echo htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/logistics/carriers', { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function(r){ return r.json(); })
			.then(function(j){
				document.getElementById('epc_ship_msg').textContent = j.message || (j.status ? 'OK' : 'Error');
				if (j.status) setTimeout(function(){ location.reload(); }, 900);
			});
	});
})();
</script>
<?php endif; ?>
