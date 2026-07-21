<?php
/**
 * Direct sample CSV download for Multi-vendor CP upload.
 * No session/CSRF — template only (safe public sample rows).
 */
declare(strict_types=1);

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

header('Content-Type: text/csv; charset=utf-8');
header('Content-Disposition: attachment; filename="epc-multivendor-sample.csv"');
header('Cache-Control: no-store, no-cache, must-revalidate');
header('Pragma: no-cache');
header('X-Content-Type-Options: nosniff');

$root = isset($_SERVER['DOCUMENT_ROOT']) ? (string) $_SERVER['DOCUMENT_ROOT'] : '';
$ingest = $root . '/content/shop/docpart/epc_multivendor_price_ingest.php';
if ($root !== '' && is_file($ingest)) {
	require_once $ingest;
	if (function_exists('epc_multivendor_sample_csv')) {
		echo epc_multivendor_sample_csv();
		exit;
	}
}

// Fallback if ingest helper is unavailable on this host.
echo "Brand,Article,Name,Qty,Price,\"Vendor full name\",\"Vendor short\",\"Data type\",Delivery,\"Engine code\",\"Country code\",Size,\"Cross reference\",\"OE number\",\"Other information\"\n";
echo "TOYOTA,446610010,\"PAD KIT, DISC BRAKE\",8,103.51,\"S-UAE Trading LLC\",S-UAE,inventory,0,2JZGE,JP,\"15\"\"\",04465-YZZD2,044650K090,\"Ceramic; front\"\n";
echo "AISIN,DT068,\"WATER PUMP\",3,45.00,\"R-UAE Spare Parts FZE\",R-UAE,inventory,0,1KZTE,TH,STD,16100-69355,1610069355,\n";
echo "DENSO,0671007450,FILTER,12,18.00,\"S-UAE Trading LLC\",S-UAE,sales,0,3L,JP,,,,\n";
echo "DENSO,0671007450,FILTER,5,22.50,\"S-UAE Trading LLC\",S-UAE,sales,0,3L,JP,,,,\n";
echo "DENSO,0671007450,FILTER,2,29.90,\"S-UAE Trading LLC\",S-UAE,sales,0,3L,JP,,,,\n";
