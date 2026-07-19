<?php
/**
 * Frontend vendor price upload — CSV/Excel, no CP.
 * URL: /vendor/upload
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/vendor/epc_vendor_access.php';

global $DP_Config, $db_link;
$urls = epc_vendor_urls();
$userId = (int) DP_User::getUserId();
if ($userId <= 0 || !($db_link instanceof PDO) || !epc_vendor_user_can_upload($db_link, $userId)) {
	echo '<script>location = ' . json_encode($urls['home']) . ';</script>';
	echo '<p style="padding:24px;">Please <a href="' . htmlspecialchars($urls['home'], ENT_QUOTES, 'UTF-8') . '">sign in to the vendor portal</a> first.</p>';
	return;
}
$account = epc_vendor_get_account($db_link, $userId);
$userSession = DP_User::getUserSession();
$csrf = htmlspecialchars((string) ($userSession['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
$welcome = isset($_GET['welcome']);
?>
<link rel="stylesheet" href="/content/shop/vendor/epc_vendor_portal.css?v=20260719vpUae1">
<section class="epc-vp" id="epc-vendor-upload"
	data-ajax="/content/shop/vendor/ajax_vendor_ingest.php"
	data-sample="/content/shop/vendor/epc_vendor_sample.php"
	data-csrf="<?php echo $csrf; ?>">
	<div class="epc-vp__dash">
		<header class="epc-vp__hero epc-vp__hero--compact">
			<div>
				<a class="epc-vp__back" href="<?php echo htmlspecialchars($urls['home'], ENT_QUOTES, 'UTF-8'); ?>">← Vendor portal</a>
				<h1>Upload price list</h1>
				<p>Publishes under <strong><?php echo htmlspecialchars((string) $account['vendor_short'], ENT_QUOTES, 'UTF-8'); ?></strong>
					(<?php echo htmlspecialchars((string) $account['vendor_full'], ENT_QUOTES, 'UTF-8'); ?>)</p>
			</div>
		</header>

		<?php if ($welcome) { ?>
		<div class="epc-vp__alert epc-vp__alert--ok">Welcome! Your vendor account is ready. Upload your first price file below.</div>
		<?php } ?>

		<div class="epc-vp__panel">
			<h2>File format</h2>
			<p class="epc-vp__muted">CSV or Excel. Required columns: <code>Brand</code>, <code>Article</code>, <code>Price</code>. Optional: Name, Qty, Data type (inventory/sales/purchase), Delivery. Vendor columns are optional — your account is used automatically.</p>
			<p><a class="epc-vp__link" id="epc-vp-sample" href="/content/shop/vendor/epc_vendor_sample.php">Download sample CSV</a></p>

			<form id="epc-vp-upload-form" class="epc-vp__form" enctype="multipart/form-data">
				<label>Default data type</label>
				<select name="data_type" id="epc-vp-data-type">
					<option value="inventory">Inventory (stock)</option>
					<option value="sales">Sales</option>
					<option value="purchase">Purchase</option>
				</select>
				<label>Price file *</label>
				<input type="file" name="price_file" id="epc-vp-file" accept=".csv,.xls,.xlsx,text/csv" required />
				<button type="submit" class="epc-vp__btn" id="epc-vp-submit"><i class="fa fa-cloud-upload"></i> Upload &amp; publish</button>
			</form>
			<div id="epc-vp-msg" class="epc-vp__alert" hidden></div>
			<div id="epc-vp-result" class="epc-vp__result" hidden></div>
		</div>
	</div>
</section>
<script src="/content/shop/vendor/epc_vendor_portal.js?v=20260719vp1"></script>
