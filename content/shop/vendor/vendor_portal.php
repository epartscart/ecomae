<?php
/**
 * Frontend Vendor Portal — login / dashboard (no CP access).
 * URL: /vendor
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/vendor/epc_vendor_access.php';

global $DP_Config, $db_link;

$userId = (int) DP_User::getUserId();
$loggedIn = $userId > 0;
$account = null;
$canAccess = false;
if ($loggedIn && isset($db_link) && $db_link instanceof PDO) {
	$account = epc_vendor_get_account($db_link, $userId);
	$canAccess = $account ? epc_vendor_user_can_access($db_link, $userId) : false;
}
$urls = epc_vendor_urls();
$userSession = $loggedIn ? DP_User::getUserSession() : null;
?>
<link rel="stylesheet" href="/content/shop/vendor/epc_vendor_portal.css?v=20260719vp1">
<section class="epc-vp" id="epc-vendor-portal">
<?php if (!$loggedIn) { ?>
	<div class="epc-vp__wrap">
		<div class="epc-vp__card">
			<div class="epc-vp__brand">eParts<span>Cart</span> Vendor</div>
			<h1>Vendor portal</h1>
			<p class="epc-vp__lead">Sign in to upload your price list and manage your warehouse offers — all from the storefront.</p>
			<?php if (!empty($_GET['registered'])) { ?>
			<div class="epc-vp__alert epc-vp__alert--ok">Registration complete. Sign in with your vendor email and password to upload prices.</div>
			<?php } ?>
			<div class="epc-vp__login panel panel-primary">
				<?php
				$login_form_postfix = 'vendor_portal';
				require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
				?>
			</div>
			<p class="epc-vp__foot">
				New seller? <a href="<?php echo htmlspecialchars($urls['register'], ENT_QUOTES, 'UTF-8'); ?>">Register as a vendor</a>
			</p>
		</div>
	</div>
<?php } elseif (!$account) { ?>
	<div class="epc-vp__wrap">
		<div class="epc-vp__card">
			<h1>Not a vendor yet</h1>
			<p class="epc-vp__lead">Your account is signed in, but it is not linked to a vendor warehouse.</p>
			<p><a class="epc-vp__btn" href="<?php echo htmlspecialchars($urls['register'], ENT_QUOTES, 'UTF-8'); ?>">Register as a vendor</a></p>
		</div>
	</div>
<?php } elseif (!$canAccess) { ?>
	<div class="epc-vp__wrap">
		<div class="epc-vp__card">
			<h1>Account pending</h1>
			<p class="epc-vp__lead">Your vendor registration (<strong><?php echo htmlspecialchars((string) $account['vendor_short'], ENT_QUOTES, 'UTF-8'); ?></strong>) is not active yet. Status: <?php echo htmlspecialchars((string) $account['status'], ENT_QUOTES, 'UTF-8'); ?>.</p>
		</div>
	</div>
<?php } else {
	$history = epc_vendor_upload_history($db_link, $account, 25);
	$priceIds = epc_vendor_price_ids($db_link, $account);
	$skuCount = 0;
	if ($priceIds) {
		try {
			$in = implode(',', array_map('intval', $priceIds));
			$skuCount = (int) $db_link->query("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` IN ($in)")->fetchColumn();
		} catch (Exception $e) {
		}
	}
	?>
	<div class="epc-vp__dash">
		<header class="epc-vp__hero">
			<div>
				<div class="epc-vp__brand">eParts<span>Cart</span> Vendor</div>
				<h1><?php echo htmlspecialchars((string) $account['vendor_full'], ENT_QUOTES, 'UTF-8'); ?></h1>
				<p>Code <strong><?php echo htmlspecialchars((string) $account['vendor_short'], ENT_QUOTES, 'UTF-8'); ?></strong>
					· Warehouse #<?php echo (int) $account['storage_id']; ?></p>
			</div>
			<div class="epc-vp__hero-actions">
				<a class="epc-vp__btn" href="<?php echo htmlspecialchars($urls['upload'], ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-upload"></i> Upload prices</a>
			</div>
		</header>

		<div class="epc-vp__kpi">
			<div class="epc-vp__kpi-card">
				<span>SKU rows</span>
				<strong><?php echo number_format($skuCount); ?></strong>
			</div>
			<div class="epc-vp__kpi-card">
				<span>Price lists</span>
				<strong><?php echo count($priceIds); ?></strong>
			</div>
			<div class="epc-vp__kpi-card">
				<span>Uploads</span>
				<strong><?php echo count($history); ?></strong>
			</div>
			<div class="epc-vp__kpi-card">
				<span>Status</span>
				<strong class="ok">Approved</strong>
			</div>
		</div>

		<div class="epc-vp__panel">
			<div class="epc-vp__panel-head">
				<h2>Recent uploads</h2>
				<a href="<?php echo htmlspecialchars($urls['upload'], ENT_QUOTES, 'UTF-8'); ?>">New upload →</a>
			</div>
			<?php if (!$history) { ?>
				<p class="epc-vp__empty">No uploads yet. Upload a CSV/Excel price file to publish parts under your vendor code.</p>
			<?php } else { ?>
				<table class="epc-vp__table">
					<thead>
						<tr>
							<th>When</th>
							<th>File</th>
							<th>List</th>
							<th>Rows</th>
							<th>Status</th>
						</tr>
					</thead>
					<tbody>
					<?php foreach ($history as $h) { ?>
						<tr>
							<td><?php echo !empty($h['created_at']) ? htmlspecialchars((string) $h['created_at'], ENT_QUOTES, 'UTF-8') : '—'; ?></td>
							<td><?php echo htmlspecialchars((string) ($h['original_filename'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo htmlspecialchars((string) ($h['price_name'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo (int) ($h['rows_imported'] ?? 0); ?></td>
							<td><span class="epc-vp__badge epc-vp__badge--<?php echo htmlspecialchars((string) ($h['status'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars((string) ($h['status'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></span></td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			<?php } ?>
		</div>
	</div>
<?php } ?>
</section>
<script>
(function(){
	// After storefront login AJAX, reload portal dashboard.
	var obs = new MutationObserver(function(){
		if (document.cookie.indexOf('u_id=') !== -1 && document.getElementById('epc-vendor-portal')) {
			/* soft hint — login form usually reloads page */
		}
	});
	try { obs.observe(document.body, {childList:true, subtree:true}); } catch(e) {}
})();
</script>
