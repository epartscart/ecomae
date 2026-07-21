<?php
/**
 * CP File Manager — elFinder UI for /content/files/
 * Route: /cp/filemanager
 *
 * Eval-safe: no inline <script>/<style> in the main pane (CP <base href> breaks those).
 * Assets load via epc_cp_page_assets (CSS in head, JS after footer jQuery reload).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

$user_session = DP_User::getAdminSession();
$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}
$backendH = htmlspecialchars($backend, ENT_QUOTES, 'UTF-8');

$ver = (function_exists('epc_cp_page_asset_version') ? epc_cp_page_asset_version() : '20260721') . 'fm2';
$cssPath = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/filemanager/epc_filemanager.css';
$jsPath = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/filemanager/epc_filemanager.js';
$cssVer = is_file($cssPath) ? (string) filemtime($cssPath) : $ver;
$jsVer = is_file($jsPath) ? (string) filemtime($jsPath) : $ver;

if (function_exists('epc_cp_register_page_assets')) {
	epc_cp_register_page_assets(
		array(
			'/' . $backend . '/lib/elfinder/css/elfinder.min.css',
			'/' . $backend . '/lib/elfinder/css/theme.css',
			'/' . $backend . '/content/filemanager/epc_filemanager.css?v=' . rawurlencode($cssVer),
		),
		array(
			'/' . $backend . '/lib/elfinder/js/elfinder.min.js',
			'/' . $backend . '/content/filemanager/epc_filemanager_config.php?v=' . rawurlencode($ver),
			'/' . $backend . '/content/filemanager/epc_filemanager.js?v=' . rawurlencode($jsVer),
		)
	);
}

if (function_exists('epc_cp_page_frame_open')) {
	epc_cp_page_frame_open(array(
		'class' => 'epc-filemanager-frame',
		'hero' => array(
			'badge' => 'Files',
			'title' => 'File manager',
			'sub' => 'Browse and manage files under /content/files — images, PDFs, and media used across the storefront and CP.',
			'actions' => array(
				array(
					'url' => '/' . $backend,
					'label' => 'Control panel',
					'icon' => 'fa-home',
				),
			),
		),
	));
}

require_once 'content/control/actions_alert.php';
?>

<div class="epc-filemanager">
	<div class="epc-filemanager__toolbar" role="toolbar" aria-label="File manager actions">
		<a class="epc-fm-btn" href="/<?php echo $backendH; ?>"><i class="fa fa-home"></i> Control panel</a>
		<a class="epc-fm-btn" href="/<?php echo $backendH; ?>/shop/catalogue/catalogue_editor"><i class="fa fa-sitemap"></i> Catalogue</a>
		<a class="epc-fm-btn" href="/content/files/" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open /content/files</a>
	</div>

	<p class="epc-filemanager__hint">
		Upload images (JPEG, PNG, GIF) and PDFs. Hidden/dot files are locked. Changes apply immediately under <code>/content/files/</code>.
	</p>

	<div class="epc-filemanager__pane">
		<div class="epc-filemanager__pane-h">
			<h3>Server files</h3>
			<span>Root: /content/files</span>
		</div>
		<div
			id="elfinder"
			class="epc-fm-loading"
			aria-live="polite"
			data-csrf="<?php echo htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>"
			data-connector="/<?php echo $backendH; ?>/lib/elfinder/php/connector.php"
		>Loading file manager…</div>
	</div>
</div>

<?php
if (function_exists('epc_cp_page_frame_close')) {
	epc_cp_page_frame_close();
}
?>
