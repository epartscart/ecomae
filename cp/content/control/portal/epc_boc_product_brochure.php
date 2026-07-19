<?php
/**
 * Super CP / BOC — product brochure launcher (opens marketing brochure).
 * Content URL: control/portal/epc_boc_product_brochure
 */
defined('_ASTEXE_') or die('No access');

$portalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
if (is_file($portalFile)) {
	require_once $portalFile;
}

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
if (!$isSuper) {
	echo '<div class="alert alert-warning">Product brochure (platform) is available on Super CP only. Tenant operators: use <a href="/brochure" target="_blank" rel="noopener">/brochure</a> or <a href="/brochure-cp" target="_blank" rel="noopener">/brochure-cp</a>.</div>';
	return;
}

$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
$cpBrochure = '/' . $backend . '/control/cp_brochure';
$frame = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
if (is_file($frame)) {
	require_once $frame;
	if (function_exists('epc_cp_page_frame_open')) {
		epc_cp_page_frame_open(array('class' => 'epc-boc-brochure'));
	}
}
?>
<div class="epc-portal-settings">
	<div class="hero">
		<h2><i class="fa fa-file-text-o"></i> Product brochure</h2>
		<p style="margin:0;opacity:.92">Share the graphical marketing brochure with prospects, or open the full live CP function deck for demos and training.</p>
	</div>
	<div class="hpanel">
		<div class="panel-heading"><h4>Open brochure</h4></div>
		<div class="panel-body">
			<p>
				<a class="btn btn-primary" href="/brochure" target="_blank" rel="noopener"><i class="fa fa-file-text-o"></i> Product brochure</a>
				<a class="btn btn-default" href="<?php echo htmlspecialchars($cpBrochure, ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener"><i class="fa fa-book"></i> Full CP brochure</a>
				<a class="btn btn-default" href="/brochure/cp" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Public CP brochure URL</a>
			</p>
			<p class="text-muted" style="margin:0">Public URLs: <code>/brochure</code> · <code>/brochure/cp</code> · In-CP: <code><?php echo htmlspecialchars($cpBrochure, ENT_QUOTES, 'UTF-8'); ?></code></p>
		</div>
	</div>
</div>
<?php
if (function_exists('epc_cp_page_frame_close')) {
	epc_cp_page_frame_close();
}
