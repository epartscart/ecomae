<?php
/**
 * Returns module router — list / detail / reasons & statuses.
 * Kept lightweight so /cp/shop/returns-manager does not fatal when the
 * original Docpart returns pack files are absent from the docroot.
 */
defined('_ASTEXE_') or die('No access');

$page = isset($_GET['page']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['page'])) : 'list';
$action = isset($_GET['action']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['action'])) : '';

$listFile = __DIR__ . '/returns_list.php';
$detailFile = __DIR__ . '/return_detail.php';
$reasonsFile = __DIR__ . '/reasons_statuses.php';

if ($page === 'reasons_statuses' && is_file($reasonsFile)) {
	require $reasonsFile;
	return;
}
if ($page === 'detail' && is_file($detailFile)) {
	require $detailFile;
	return;
}
if (is_file($listFile)) {
	require $listFile;
	return;
}

// Graceful empty state when the full returns pack is not installed.
$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
?>
<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Returns</div>
		<div class="panel-body">
			<p class="text-muted" style="margin:0 0 12px">
				The full returns workflow pack (list / detail / reasons &amp; statuses) is not installed on this host.
			</p>
			<p style="margin:0">
				Use
				<a href="/<?php echo $backend; ?>/shop/orders/orders">Orders</a>
				for order-level refunds, or
				<a href="/<?php echo $backend; ?>/control/portal/epc_boc_command_center">Command Center</a>
				for fleet operations.
			</p>
			<?php if ($page === 'reasons_statuses' || $action !== ''): ?>
			<p class="text-warning" style="margin:12px 0 0">Requested view: <code><?php echo htmlspecialchars($page . ($action !== '' ? '/' . $action : ''), ENT_QUOTES, 'UTF-8'); ?></code></p>
			<?php endif; ?>
		</div>
	</div>
</div>
<?php
// Do not return — CP pages are eval()'d inside the template shell.
