<?php
// Dump static catalogs for PhpPlanQ1PlusJson.cs (no leftover .php sibling paths).
define('_ASTEXE_', 1);
$root = rtrim($argv[1] ?? '/workspace', '/');
require $root . '/content/shop/docpart/epc_price_extra_fields.php';
require $root . '/content/shop/channels/epc_channel_schema.php';
require $root . '/content/shop/channels/epc_channel_helpers.php';
echo json_encode(array(
	'extra' => epc_price_extra_field_catalog(),
	'carriers' => epc_channel_carriers_catalog(),
	'marketplaces' => epc_channel_marketplaces_catalog(),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
