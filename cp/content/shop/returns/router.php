<?php
/**
 * Returns module router — list / detail / reasons & statuses.
 */
defined('_ASTEXE_') or die('No access');

$page = isset($_GET['page']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['page'])) : 'list';
$action = isset($_GET['action']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['action'])) : '';

// Deep links: ?return_id=N opens detail.
if (!empty($_GET['return_id']) && ($page === 'list' || $page === '')) {
	$page = 'detail';
}

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

$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
?>
<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">Returns</div>
		<div class="panel-body">
			<p class="text-muted" style="margin:0">Returns module files are missing on this host.</p>
			<p style="margin:12px 0 0"><a href="/<?php echo $backend; ?>/shop/orders/orders">Orders</a></p>
		</div>
	</div>
</div>
<?php
