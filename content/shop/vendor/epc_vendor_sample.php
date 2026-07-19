<?php
/**
 * Download sample CSV for frontend vendor uploads.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_multivendor_price_ingest.php';

if ((int) DP_User::getUserId() <= 0) {
	http_response_code(403);
	header('Content-Type: text/plain; charset=utf-8');
	exit("Sign in as a vendor to download the sample.\n");
}

$csv = epc_vendor_portal_sample_csv();
header('Content-Type: text/csv; charset=utf-8');
header('Content-Disposition: attachment; filename="epc-vendor-price-sample.csv"');
header('Cache-Control: no-store');
echo $csv;
exit;
