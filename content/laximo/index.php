<?php
/**
 * Laximo Catalog - Main entry point
 * Loaded via CMS page "/katalog-laximo"
 *
 * IMPORTANT: This file is embedded into the site template and eval()'d.
 * - Do not use return/exit (aborts the whole page render).
 * - Always leave the parser in HTML mode at the end (close with ?>).
 *
 * Two-section storefront UI (VIN search + brand grid) is the default catalogs view.
 * Deep Guayaquil tasks (vehicle/unit/…) still use the SDK when task != catalogs.
 */

$task = isset($_GET['task']) ? strtolower(trim((string) $_GET['task'])) : '';
$forceGuayaquil = isset($_GET['guayaquil']) && (string) $_GET['guayaquil'] === '1';
$useAjaxLanding = !$forceGuayaquil && ($task === '' || $task === 'catalogs');

if ($useAjaxLanding) {
	$langPrefix = '';
	$uri = (string) ($_SERVER['REQUEST_URI'] ?? '');
	if (preg_match('#^/(en|ar|ru|de|fr|es|it|pt|tr|zh|ja|ko)(/|$)#i', $uri, $m)) {
		$langPrefix = '/' . strtolower($m[1]);
	}
	?>
	<link rel="stylesheet" href="/api/Laximo/laximo.css?v=20260717lax2" type="text/css" />
	<div class="epc-laximo-page">
		<div class="epc-laximo-page__intro">
			<h1 style="margin:0 0 8px;font-size:22px;">OEM vehicle catalog</h1>
			<p style="margin:0 0 16px;color:#64748b;font-size:14px;max-width:640px;">
				Search by VIN / frame, or browse manufacturer catalogs. Two sections below load from Laximo.
			</p>
		</div>
		<div id="Laximo_container">
			<div class="laximo-loading">
				<div class="spinner"></div>
				<p>Loading Laximo Catalog…</p>
			</div>
		</div>
	</div>
	<script>
		window.EPC_LAXIMO_BASE = <?php echo json_encode($langPrefix . '/katalog-laximo', JSON_UNESCAPED_SLASHES); ?>;
	</script>
	<script src="/api/laximo_storefront.js?v=20260717lax242"></script>
	<script>
		jQuery(function () {
			if (window.Laximo && typeof Laximo.init === 'function') {
				Laximo.init();
			}
		});
	</script>
	<?php
} else {
	// Default to catalogs view when no task is specified in the URL.
	// The Guayaquil router parses $_SERVER['REQUEST_URI'], so we also inject into the URI.
	if (!isset($_GET['task']) || $_GET['task'] === '') {
		$_GET['task'] = 'catalogs';
		if (strpos($_SERVER['REQUEST_URI'], 'task=') === false) {
			$_SERVER['REQUEST_URI'] .= (strpos($_SERVER['REQUEST_URI'], '?') !== false ? '&' : '?') . 'task=catalogs';
		}
	}

	$laximoRoot = $_SERVER['DOCUMENT_ROOT'] . '/content/laximo';

	if (file_exists($laximoRoot . '/com_guayaquil/router.php')) {
		try {
			set_include_path(get_include_path() . PATH_SEPARATOR . $laximoRoot . '/');
			spl_autoload_register(function ($class) {
				$path = $_SERVER['DOCUMENT_ROOT'] . '/content/laximo/';
				$file = preg_replace('/guayaquil/', 'com_guayaquil', $class);
				$file = str_replace('\\', '/', $file);
				if (file_exists($path . $file . '.php')) {
					require_once($path . $file . '.php');
				}
			});
			if (file_exists($laximoRoot . '/vendor/autoload.php')) {
				require_once($laximoRoot . '/vendor/autoload.php');
			}
			require_once($laximoRoot . '/com_guayaquil/index.php');
		} catch (\Throwable $e) {
			echo '<div style="padding:20px;color:#c00;">Laximo error: ' . htmlspecialchars($e->getMessage())
				. ' in ' . basename($e->getFile()) . ':' . $e->getLine() . '</div>';
			echo '<p><a href="' . htmlspecialchars(strtok((string) ($_SERVER['REQUEST_URI'] ?? '/katalog-laximo'), '?'), ENT_QUOTES, 'UTF-8')
				. '">Open catalog home (VIN + brands)</a></p>';
		}
	} else {
		echo '<div style="padding:20px;color:#c00;">Laximo SDK not available. Please contact support.</div>';
	}
}
?>
