<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';
$catalog = epc_channel_carriers_catalog();
$carrier = isset($how_get_json['carrier']) ? (string)$how_get_json['carrier'] : 'dhl';
$cname = isset($catalog[$carrier]['name']) ? $catalog[$carrier]['name'] : strtoupper($carrier);
$shipments = array();
if (isset($db_link) && $db_link instanceof PDO && isset($order_id)) {
	$st = $db_link->prepare('SELECT * FROM `epc_carrier_shipments` WHERE `order_id` = ? ORDER BY `id` DESC');
	$st->execute(array((int)$order_id));
	$shipments = $st->fetchAll(PDO::FETCH_ASSOC);
}
?>
<p class="lead">Delivery — <?php echo epc_channel_h($cname); ?></p>
<table class="table">
<tr><td><?php echo epc_channel_h($how_get_json['city'] ?? ''); ?>, <?php echo epc_channel_h($how_get_json['country'] ?? ''); ?></td></tr>
<tr><td><?php echo epc_channel_h($how_get_json['address'] ?? ''); ?></td></tr>
<tr><td>Phone: <?php echo epc_channel_h($how_get_json['phone'] ?? ''); ?></td></tr>
</table>
<?php if (!empty($shipments)): ?>
<h5>Tracking</h5>
<ul>
<?php foreach ($shipments as $s): ?>
	<li>
		<?php echo epc_channel_h(strtoupper($s['carrier_code'])); ?>:
		<?php if (!empty($s['label_url'])): ?>
			<a href="<?php echo epc_channel_h($s['label_url']); ?>" target="_blank" rel="noopener"><?php echo epc_channel_h($s['tracking_number']); ?></a>
		<?php else: ?>
			<?php echo epc_channel_h($s['tracking_number']); ?>
		<?php endif; ?>
		(<?php echo epc_channel_h($s['status']); ?>)
	</li>
<?php endforeach; ?>
</ul>
<?php endif; ?>
