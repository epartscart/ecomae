<?php
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';
$catalog = epc_channel_carriers_catalog();
$carrier = isset($how_get_json['carrier']) ? (string)$how_get_json['carrier'] : 'dhl';
$service = isset($how_get_json['service']) ? (string)$how_get_json['service'] : '';
$cname = isset($catalog[$carrier]['name']) ? $catalog[$carrier]['name'] : strtoupper($carrier);
$sname = isset($catalog[$carrier]['services'][$service]) ? $catalog[$carrier]['services'][$service] : $service;
$rate = epc_channel_demo_rate($carrier, isset($how_get_json['weight_kg']) ? (float)$how_get_json['weight_kg'] : 1, isset($how_get_json['country']) ? $how_get_json['country'] : 'AE');
?>
<p class="lead">Delivery — <?php echo epc_channel_h(translate_str_by_id($obtain_mode['caption'])); ?></p>
<table class="table">
<tr><th>Carrier &amp; service</th></tr>
<tr><td><?php echo epc_channel_h($cname); ?> — <?php echo epc_channel_h($sname); ?> (est. <?php echo epc_channel_money($rate); ?> AED)</td></tr>
<tr><td><?php echo epc_channel_h($how_get_json['city'] ?? ''); ?>, <?php echo epc_channel_h($how_get_json['country'] ?? ''); ?></td></tr>
<tr><td><?php echo epc_channel_h($how_get_json['address'] ?? ''); ?></td></tr>
<tr><td>Phone: <?php echo epc_channel_h($how_get_json['phone'] ?? ''); ?></td></tr>
<tr><td>Weight: <?php echo epc_channel_h($how_get_json['weight_kg'] ?? '1'); ?> kg</td></tr>
</table>
